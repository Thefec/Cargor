using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace NewCss
{
    /// <summary>
    /// M��teri y�netim sistemi - m��teri spawn'lama, kuyruk y�netimi ve g�nl�k m��teri takibini sa�lar. 
    /// Server-authoritative tasar�m ile network senkronizasyonu i�erir.
    /// </summary>
    public class CustomerManager : NetworkBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[CustomerManager]";
        private const int DEFAULT_QUEUE_SIZE = 3;
        private const float DEFAULT_SPAWN_START_HOUR = 8f;
        private const float DEFAULT_SPAWN_END_HOUR = 17f;
        private const float CUSTOMER_EXIT_HOUR = 17.5f; // 17:30 - Müşterilerin çıkışa yönlendirileceği saat

        #endregion

        #region Singleton

        public static CustomerManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        #endregion

        #region Serialized Fields - Spawn Settings

        [Header("=== SPAWN SETTINGS ===")]
        [SerializeField, Tooltip("M��teri prefab'�")]
        public GameObject customerPrefab;

        [SerializeField, Tooltip("Spawn noktas�")]
        public Transform spawnPoint;

        [SerializeField, Tooltip("Maksimum kuyruk boyutu")]
        public int maxQueueSize = DEFAULT_QUEUE_SIZE;

        #endregion

        #region Serialized Fields - Capacity-Based Spawn Settings

        [Header("=== CAPACITY-BASED SPAWN SETTINGS ===")]
        [SerializeField, Tooltip("Her aktif raf/masa başına müşteri katkısı")]
        private float _shelfMultiplier = 2f;

        [SerializeField, Tooltip("Mağaza seviyesi başına müşteri katkısı")]
        private float _levelMultiplier = 2f;

        [SerializeField, Tooltip("Mağaza seviyesi (ileride XP sistemine bağlanacak)")]
        private int _storeLevel = 1;

        [SerializeField, Tooltip("Rastgele sapma alt sınırı")]
        private int _minVariance = -2;

        [SerializeField, Tooltip("Rastgele sapma üst sınırı")]
        private int _maxVariance = 3;

        [SerializeField, Tooltip("Günlük minimum müşteri sayısı")]
        private int _minCustomersPerDay = 1;

        [SerializeField, Tooltip("Günlük maksimum müşteri sayısı (Soft Cap - performans koruması)")]
        private int _maxCustomersPerDay = 50;

        [SerializeField, Range(0f, 1f), Tooltip("Spawn zamanı rastgeleliği")]
        public float spawnTimeRandomness = 0.2f;

        #endregion

        #region Runtime Multipliers (set by other systems)

        /// <summary>
        /// Event sistemi tarafından set edilen müşteri çarpanı (örn: INTENSIVE DAY = 1.5)
        /// </summary>
        [HideInInspector]
        public float eventCustomerMultiplier = 1f;

        /// <summary>
        /// DifficultyManager tarafından set edilen oyuncu sayısı çarpanı
        /// </summary>
        [HideInInspector]
        public float playerCountMultiplier = 1f;

        #endregion

        #region Backward Compatibility (Legacy - kullanılmıyor, EventEffectManager uyumu için)

        // Eski lineer sistem field'leri - artık formülde kullanılmıyor.
        // EventEffectManager ve DifficultyManager bu field'lere doğrudan yazmak yerine
        // eventCustomerMultiplier ve playerCountMultiplier kullanmalı.
        [HideInInspector] public int baseCustomersPerDay = 10;
        [HideInInspector] public int customerIncreasePerDay = 2;

        #endregion

        #region Serialized Fields - Queue & Tables

        [Header("=== QUEUE & TABLES ===")]
        [SerializeField, Tooltip("Kuyruk pozisyonlar�")]
        public Transform[] queuePositions;

        [SerializeField, Tooltip("��k�� noktas�")]
        public Transform exitPoint;

        [SerializeField, Tooltip("B�rakma masas�")]
        public DisplayTable dropOffTable;

        [SerializeField, Tooltip("Servis masalar�")]
        public DisplayTable[] serviceTables;

        #endregion


        #region Serialized Fields - Time Settings

        [Header("=== TIME SETTINGS ===")]
        [SerializeField, Tooltip("Spawn ba�lang�� saati (24 saat format�)")]
        public float spawnStartHour = DEFAULT_SPAWN_START_HOUR;

        [SerializeField, Tooltip("Spawn biti� saati (24 saat format�)")]
        public float spawnEndHour = DEFAULT_SPAWN_END_HOUR;

        #endregion

        #region Serialized Fields - Wave Settings

        [Header("=== WAVE/RUSH HOUR SETTINGS ===")]
        [SerializeField, Tooltip("Wave settings ScriptableObject")]
        public WaveSettings waveSettings;

        [SerializeField, Tooltip("Enable wave system")]
        public bool enableWaveSystem = true;

        #endregion

        #region Serialized Fields - UI Settings

        [Header("=== UI SETTINGS ===")]
        [SerializeField, Tooltip("Kalan m��teri say�s� text'i")]
        public TextMeshProUGUI remainingCustomersText;

        #endregion

        #region Serialized Fields - Product Assignment

        [Header("=== PRODUCT ASSIGNMENT ===")]
        [SerializeField, Tooltip("Son kullan�lan �r�n ge�mi�i boyutu")]
        public int recentProductHistorySize = 3;

        #endregion

        #region Serialized Fields - Debug

        [Header("=== DEBUG ===")]
        [SerializeField, Tooltip("Debug loglar�n� g�ster")]
        private bool showDebugLogs = true;

        #endregion

        #region Private Fields - Queue

        private readonly List<CustomerAI> _customerQueue = new();

        #endregion

        #region Private Fields - Daily Tracking

        private int _todaysTotalCustomers;
        private int _customersSpawnedToday;
        private int _customersRemainingToday;
        private readonly List<float> _scheduledSpawnTimes = new();
        private int _nextScheduledIndex;
        private bool _dayInitialized;
        private bool _customersExitedToday; // Gün sonu müşteri çıkışı yapıldı mı

        #endregion

        #region Private Fields - Product History

        private readonly Queue<int> _recentProductIndices = new();
        private int _lastProductIndex = -1; // Ardışık tekrar engeli için

        #endregion

        #region Private Fields - Deterministic Loot

        private int _deterministicLootCounter; // Her 3 müşteriden 1'i tır rengine uygun ürün bırakır

        #endregion

        #region Public Properties

        /// <summary>
        /// Mevcut kuyruk boyutu
        /// </summary>
        public int QueueSize => _customerQueue.Count;

        /// <summary>
        /// Bug�n toplam m��teri say�s�
        /// </summary>
        public int TodaysTotalCustomers => _todaysTotalCustomers;

        /// <summary>
        /// Bug�n spawn olan m��teri say�s�
        /// </summary>
        public int CustomersSpawnedToday => _customersSpawnedToday;

        /// <summary>
        /// Kalan m��teri say�s�
        /// </summary>
        public int CustomersRemainingToday => _customersRemainingToday;

        /// <summary>
        /// M��teri spawn edilebilir mi?
        /// </summary>
        public bool CanSpawnCustomers => IsWithinSpawningHours();

        /// <summary>
        /// Kuyruk dolu mu?
        /// </summary>
        public bool IsQueueFull => _customerQueue.Count >= maxQueueSize ||
                                    _customerQueue.Count >= queuePositions.Length;

        /// <summary>
        /// Şu an rush hour mu?
        /// </summary>
        public bool IsRushHour => enableWaveSystem && waveSettings != null && waveSettings.IsRushHour(GetCurrentTime());

        /// <summary>
        /// Mevcut zaman dilimi adı
        /// </summary>
        public string CurrentPeriodName => enableWaveSystem && waveSettings != null 
            ? waveSettings.GetPeriodForTime(GetCurrentTime()).periodName 
            : "Normal";

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            LocalizationHelper.OnLocaleChanged += OnLocaleChanged;
            
            if (IsServer)
            {
                SubscribeToDayCycleEvents();
                InitializeDailyCustomers();
            }
        }

        private void OnDestroy()
        {
            LocalizationHelper.OnLocaleChanged -= OnLocaleChanged;
            
            if (IsServer)
            {
                UnsubscribeFromDayCycleEvents();
            }
        }
        
        private void OnLocaleChanged()
        {
            // Refresh UI when locale changes
            UpdateRemainingCustomersUI();
        }

        private void Update()
        {
            if (!IsServer) return;

            EnsureDayInitialized();
            TrySpawnScheduledCustomer();
            CheckEndOfDayCustomerExit();
        }

        #endregion

        #region Event Subscriptions

        private void SubscribeToDayCycleEvents()
        {
            DayCycleManager.OnNewDay += HandleNewDay;
        }

        private void UnsubscribeFromDayCycleEvents()
        {
            DayCycleManager.OnNewDay -= HandleNewDay;
        }

        #endregion

        #region Day Cycle Handler

        private void HandleNewDay()
        {
            if (!IsServer) return;

            LogDebug("New day started - reinitializing customers");
            InitializeDailyCustomers();
        }

        private void EnsureDayInitialized()
        {
            if (!_dayInitialized && DayCycleManager.Instance != null)
            {
                InitializeDailyCustomers();
            }
        }

        #endregion

        #region Daily Customer Management

        private void InitializeDailyCustomers()
        {
            if (DayCycleManager.Instance == null)
            {
                LogWarning("DayCycleManager not found, cannot initialize daily customers");
                return;
            }

            int currentDay = DayCycleManager.Instance.currentDay;

            _todaysTotalCustomers = CalculateTodaysCustomerCount(currentDay);
            _customersSpawnedToday = 0;
            _customersRemainingToday = _todaysTotalCustomers;
            _nextScheduledIndex = 0;
            _dayInitialized = true;
            _customersExitedToday = false; // Yeni gün için çıkış flag'ini sıfırla
            _deterministicLootCounter = 0; // Deterministic loot sayacını sıfırla

            CalculateSpawnSchedule();
            UpdateRemainingCustomersUI();

            LogDebug($"Day {currentDay} - Total customers scheduled: {_todaysTotalCustomers}");
            LogDebug($"Spawn times calculated between {spawnStartHour:F1} and {spawnEndHour:F1}");
        }

        /// <summary>
        /// Kapasite ve itibar bazlı müşteri sayısı hesaplama.
        /// Formül: (ActiveInteractables × shelfMult + StoreLevel × levelMult + Random) × eventMult × playerMult
        /// Tüm katsayılar Inspector'dan ayarlanabilir. Server-only çalışır.
        /// </summary>
        private int CalculateTodaysCustomerCount(int currentDay)
        {
            // Eski lineer formül (devre dışı):
            // return baseCustomersPerDay + ((currentDay - 1) * customerIncreasePerDay);

            // 1. Sahnedeki aktif etkileşim noktalarını say
            int activeInteractables = CountActiveInteractables();

            // 2. Temel kapasite hesabı
            float capacityBase = (activeInteractables * _shelfMultiplier) + (_storeLevel * _levelMultiplier);

            // 3. Rastgele sapma ekle (her gün farklı hissetsin)
            float randomVariance = Random.Range(_minVariance, _maxVariance + 1);

            // 4. Ham sonuç
            float rawCount = capacityBase + randomVariance;

            // 5. Event ve oyuncu sayısı çarpanlarını uygula
            float multipliedCount = rawCount * eventCustomerMultiplier * playerCountMultiplier;

            // 6. Clamp: asla 0/negatif olmasın, soft cap'i aşmasın
            int finalCount = Mathf.Clamp(Mathf.RoundToInt(multipliedCount), _minCustomersPerDay, _maxCustomersPerDay);

            LogDebug($"Capacity calc: interactables={activeInteractables}, level={_storeLevel}, " +
                     $"capacityBase={capacityBase:F1}, random={randomVariance}, " +
                     $"eventMult={eventCustomerMultiplier:F2}, playerMult={playerCountMultiplier:F2}, " +
                     $"raw={multipliedCount:F1}, final={finalCount}");

            return finalCount;
        }

        /// <summary>
        /// Sahnedeki tüm aktif etkileşim noktalarını (raf + masa) sayar.
        /// Server-only: Sadece gün başında bir kez çağrılır (performans güvenli).
        /// </summary>
        private int CountActiveInteractables()
        {
            int count = 0;

            // ShelfState: Oyuncunun item koyabileceği raflar
            var shelves = FindObjectsOfType<ShelfState>();
            count += shelves.Length;

            // DisplayTable: Müşteri servis masaları
            var tables = FindObjectsOfType<DisplayTable>();
            count += tables.Length;

            return count;
        }

        private void CalculateSpawnSchedule()
        {
            _scheduledSpawnTimes.Clear();

            if (_todaysTotalCustomers <= 0)
            {
                LogWarning("No customers scheduled for today");
                return;
            }

            float spawnWindow = spawnEndHour - spawnStartHour;
            float baseInterval = spawnWindow / _todaysTotalCustomers;

            for (int i = 0; i < _todaysTotalCustomers; i++)
            {
                float spawnTime = CalculateSpawnTime(i, baseInterval);
                _scheduledSpawnTimes.Add(spawnTime);
            }

            _scheduledSpawnTimes.Sort();

            LogSpawnTimesDebug();
        }

        private float CalculateSpawnTime(int index, float baseInterval)
        {
            float baseSpawnTime = spawnStartHour + (index * baseInterval) + (baseInterval * 0.5f);
            float randomOffset = Random.Range(-baseInterval * spawnTimeRandomness, baseInterval * spawnTimeRandomness);
            float spawnTime = Mathf.Clamp(baseSpawnTime + randomOffset, spawnStartHour, spawnEndHour);

            // Apply wave system spawn rate modifier
            if (enableWaveSystem && waveSettings != null)
            {
                float spawnRateMultiplier = waveSettings.GetSpawnRateMultiplier(spawnTime);
                // Higher multiplier = faster spawn = reduce interval
                if (spawnRateMultiplier > 0)
                {
                    float adjustment = (baseInterval * (1f - (1f / spawnRateMultiplier))) * 0.5f;
                    spawnTime = Mathf.Clamp(spawnTime - adjustment, spawnStartHour, spawnEndHour);
                }
            }

            return spawnTime;
        }

        private void LogSpawnTimesDebug()
        {
            if (!showDebugLogs) return;

            LogDebug("First 5 spawn times:");
            int displayCount = Mathf.Min(5, _scheduledSpawnTimes.Count);
            for (int i = 0; i < displayCount; i++)
            {
                LogDebug($"  Customer {i + 1}: {_scheduledSpawnTimes[i]:F2}");
            }
        }

        #endregion

        #region Spawn Logic

        private void TrySpawnScheduledCustomer()
        {
            if (!CanSpawnScheduledCustomer()) return;

            float currentTime = GetCurrentTime();

            if (currentTime >= _scheduledSpawnTimes[_nextScheduledIndex])
            {
                TryExecuteSpawn(currentTime);
            }
        }

        private bool CanSpawnScheduledCustomer()
        {
            if (DayCycleManager.Instance == null) return false;
            if (!IsWithinSpawningHours()) return false;
            if (_nextScheduledIndex >= _scheduledSpawnTimes.Count) return false;
            if (IsQueueFull) return false;

            // Check wave system queue limit
            if (enableWaveSystem && waveSettings != null)
            {
                float currentTime = GetCurrentTime();
                int waveMaxCustomers = waveSettings.GetMaxCustomersForTime(currentTime);
                if (_customerQueue.Count >= waveMaxCustomers)
                {
                    return false;
                }
            }

            return true;
        }

        private void TryExecuteSpawn(float currentTime)
        {
            int nextQueueIndex = GetNextAvailableQueueIndex();

            if (nextQueueIndex == -1) return;

            SpawnCustomer(nextQueueIndex);
            _customersSpawnedToday++;
            _nextScheduledIndex++;

            LogDebug($"Customer {_customersSpawnedToday}/{_todaysTotalCustomers} spawned at time {currentTime:F2}");
        }

        private bool IsWithinSpawningHours()
        {
            if (DayCycleManager.Instance == null) return false;

            float currentTime = GetCurrentTime();
            return currentTime >= spawnStartHour && currentTime <= spawnEndHour;
        }

        private float GetCurrentTime()
        {
            return DayCycleManager.Instance?.CurrentTime ?? 0f;
        }

        /// <summary>
        /// Gün sonu yaklaştığında (17:30) tüm müşterileri çıkışa yönlendirir
        /// </summary>
        private void CheckEndOfDayCustomerExit()
        {
            if (_customersExitedToday) return;
            if (DayCycleManager.Instance == null) return;

            float currentTime = GetCurrentTime();

            // 17:30 veya sonrası olduğunda tüm müşterileri çıkışa yönlendir
            if (currentTime >= CUSTOMER_EXIT_HOUR)
            {
                ForceAllCustomersToExit();
                _customersExitedToday = true;
                LogDebug($"End of day customer exit triggered at {currentTime:F2}");
            }
        }

        /// <summary>
        /// Kuyruktaki tüm müşterileri çıkışa yönlendirir
        /// </summary>
        private void ForceAllCustomersToExit()
        {
            if (_customerQueue.Count == 0) return;

            LogDebug($"Forcing {_customerQueue.Count} customers to exit");

            // Tüm müşterilerin bekleme süresini sıfırla ve çıkışa yönlendir
            foreach (var customer in _customerQueue.ToList())
            {
                if (customer != null)
                {
                    customer.ForceExitDueToEndOfDay();
                }
            }
        }

        #endregion

        #region Queue Index Management

        private int GetNextAvailableQueueIndex()
        {
            int maxIndex = Mathf.Min(queuePositions.Length, maxQueueSize);

            for (int i = 0; i < maxIndex; i++)
            {
                if (!IsQueueSlotOccupied(i))
                {
                    return i;
                }
            }

            return -1;
        }

        private bool IsQueueSlotOccupied(int index)
        {
            foreach (var customer in _customerQueue)
            {
                if (customer.GetTargetQueueIndex() == index)
                {
                    return true;
                }
            }
            return false;
        }

        #endregion

        #region Customer Spawning

        /// <summary>
        /// Kalan müşteri var mı? (Kota kontrolü için)
        /// </summary>
        public bool HasRemainingCustomers => _customersRemainingToday > 0;

        /// <summary>
        /// Bir sonraki planlanmış müşteriyi anında spawnlamaya zorlar.
        /// Eğer spawn zamanı henüz gelmemişse bile spawnlar.
        /// </summary>
        /// <returns>Spawn başarılı ise true, değilse false</returns>
        public bool ForceSpawnNextCustomer()
        {
            if (!IsServer) return false;
            if (!IsWithinSpawningHours()) 
            {
                LogDebug("ForceSpawnNextCustomer: Outside spawning hours.");
                return false;
            }
            if (_nextScheduledIndex >= _scheduledSpawnTimes.Count) 
            {
                LogDebug("ForceSpawnNextCustomer: No more scheduled customers.");
                return false;
            }

            // Kuyruk dolu mu?
            if (IsQueueFull)
            {
                 LogDebug("ForceSpawnNextCustomer: Queue full, skipping spawn.");
                 return false;
            }

            // Mevcut zamanı göndererek spawnla
            TryExecuteSpawn(GetCurrentTime());
            LogDebug("ForceSpawnNextCustomer: Executed forced spawn.");
            return true;
        }

        private void SpawnCustomer(int queueIndex)
        {
            if (customerPrefab == null || spawnPoint == null)
            {
                LogError("Customer prefab or spawn point is null!");
                return;
            }

            var customerObject = Instantiate(customerPrefab, spawnPoint.position, Quaternion.identity);
            var networkObject = customerObject.GetComponent<NetworkObject>();

            if (networkObject == null)
            {
                LogError("Customer prefab has no NetworkObject component!");
                Destroy(customerObject);
                return;
            }

            networkObject.Spawn();

            var customerAI = customerObject.GetComponent<CustomerAI>();
            if (customerAI == null)
            {
                LogError("Customer prefab has no CustomerAI component!");
                networkObject.Despawn();
                return;
            }

            SetupCustomerAI(customerAI, queueIndex);
            SetupCustomerClientRpc(networkObject.NetworkObjectId, queueIndex);

            _customerQueue.Add(customerAI);
            customerAI.SetQueueTarget(queuePositions[queueIndex].position, queueIndex);
        }

        #endregion

        #region Customer Setup

        private void SetupCustomerAI(CustomerAI ai, int queueIndex)
        {
            ai.isPrefabMode = false;
            ai.manager = this;
            ai.exitPoint = exitPoint;
            ai.dropOffTable = dropOffTable;

            SetupCustomerComponents(ai);
        }

        private void SetupCustomerComponents(CustomerAI ai)
        {
            SetupNavigation(ai);
            SetupUI(ai);
            SetupAnimationAndPhysics(ai);
        }

        private void SetupNavigation(CustomerAI ai)
        {
            var navAgent = ai.GetComponent<NavMeshAgent>();
            if (navAgent != null)
            {
                navAgent.enabled = true;
            }
        }

        private void SetupUI(CustomerAI ai)
        {
            var canvas = ai.GetComponentInChildren<Canvas>();
            if (canvas != null)
            {
                canvas.gameObject.SetActive(!ai.hideCanvasUntilTimer);
                ai.waitCanvas = canvas;
            }

            var waitBar = ai.GetComponentInChildren<WaitBar>();
            if (waitBar != null)
            {
                waitBar.HideBar();
                ai.waitBar = waitBar;
            }
        }

        private void SetupAnimationAndPhysics(CustomerAI ai)
        {
            var animator = ai.GetComponent<Animator>();
            if (animator != null)
            {
                animator.enabled = true;
            }

            var collider = ai.GetComponent<SphereCollider>();
            if (collider != null)
            {
                collider.enabled = true;
                collider.isTrigger = true;
            }
        }

        [ClientRpc]
        private void SetupCustomerClientRpc(ulong networkObjectId, int queueIndex)
        {
            StartCoroutine(SetupCustomerOnClientCoroutine(networkObjectId, queueIndex));
        }

        private IEnumerator SetupCustomerOnClientCoroutine(ulong networkObjectId, int queueIndex)
        {
            yield return null; // Wait for NetworkObject registration

            if (!TryGetSpawnedNetworkObject(networkObjectId, out var netObj))
            {
                yield break;
            }

            var ai = netObj.GetComponent<CustomerAI>();
            if (ai != null)
            {
                SetupCustomerAI(ai, queueIndex);
            }
        }

        private bool TryGetSpawnedNetworkObject(ulong networkObjectId, out NetworkObject netObj)
        {
            netObj = null;

            if (NetworkManager.Singleton == null) return false;
            if (NetworkManager.Singleton.SpawnManager == null) return false;

            return NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out netObj);
        }

        #endregion

        #region Queue Management

        /// <summary>
        /// M��teri i�lemi tamamland���nda �a�r�l�r
        /// </summary>
        public void NotifyCustomerDone(CustomerAI customer)
        {
            if (!_customerQueue.Remove(customer))
            {
                return;
            }

            _customersRemainingToday--;
            UpdateRemainingCustomersUI();
            UpdateRemainingCustomersClientRpc(_customersRemainingToday);

            AdvanceQueue();

            LogDebug($"Customer left.  Remaining today: {_customersRemainingToday}");
        }

        private void AdvanceQueue()
        {
            SortQueueByIndex();
            ReassignQueuePositions();
        }

        private void SortQueueByIndex()
        {
            _customerQueue.Sort((a, b) => a.GetTargetQueueIndex().CompareTo(b.GetTargetQueueIndex()));
        }

        private void ReassignQueuePositions()
        {
            for (int i = 0; i < _customerQueue.Count; i++)
            {
                var customer = _customerQueue[i];

                if (customer.isPrefabMode) continue;
                if (customer.GetTargetQueueIndex() == i) continue;

                customer.SetQueueTarget(queuePositions[i].position, i);
            }
        }

        /// <summary>
        /// M��teri kuyru�un ba��nda m� kontrol eder
        /// </summary>
        public bool IsFirstInQueue(CustomerAI ai)
        {
            return _customerQueue.Count > 0 && _customerQueue[0] == ai;
        }

        /// <summary>
        /// M��teriye masa atar
        /// </summary>
        public void AssignDropOffTable(CustomerAI customer, DisplayTable table)
        {
            if (customer != null && table != null)
            {
                customer.dropOffTable = table;
            }
        }

        #endregion

        #region UI Management

        private void UpdateRemainingCustomersUI()
        {
            if (remainingCustomersText != null)
            {
                string label = LocalizationHelper.GetLocalizedString("ExpectedCustomers");
                remainingCustomersText.text = $"{label}\n{_customersRemainingToday}";
            }
        }

        [ClientRpc]
        private void UpdateRemainingCustomersClientRpc(int remainingCount)
        {
            if (remainingCustomersText != null)
            {
                string label = LocalizationHelper.GetLocalizedString("ExpectedCustomers");
                remainingCustomersText.text = $"{label}\n{remainingCount}";
            }
        }

        #endregion

        #region Product Assignment

        /// <summary>
        /// Son kullanılan ürünleri hariç tutarak rastgele ürün index'i döndürür. 
        /// Ardışık aynı ürün seçimini kesinlikle engeller.
        /// Deterministic loot: Her 3 müşteriden 1'i sıradaki tır rengine uygun ürün bırakır.
        /// </summary>
        public int GetRandomProductIndexExcludingRecent(int productCount)
        {
            if (productCount <= 0) return -1;
            if (productCount == 1) return 0;

            _deterministicLootCounter++;

            // Deterministic loot: Her 3. müşteri (1, 4, 7, ...) tır rengine uygun ürün bırakır
            if (_deterministicLootCounter % 3 == 1)
            {
                int deterministicIndex = GetDeterministicProductIndex(productCount);
                if (deterministicIndex >= 0)
                {
                    AddToProductHistory(deterministicIndex);
                    LogDebug($"Deterministic loot assigned: product index {deterministicIndex}");
                    return deterministicIndex;
                }
            }

            var candidates = BuildProductCandidates(productCount);
            int chosen = SelectProductIndex(candidates, productCount);
            AddToProductHistory(chosen);

            return chosen;
        }

        /// <summary>
        /// Sıradaki kamyon rengine uygun ürün index'ini döndürür
        /// </summary>
        private int GetDeterministicProductIndex(int productCount)
        {
            if (TruckSpawner.Instance == null) return -1;

            BoxInfo.BoxType? nextTruckColor = TruckSpawner.Instance.NextTruckColor;
            if (!nextTruckColor.HasValue) return -1;

            // BoxType sıralamasına göre ürün index'i (Red=0, Yellow=1, Blue=2)
            int targetIndex = (int)nextTruckColor.Value;

            // Ürün sayısı yeterli mi kontrol et
            if (targetIndex < productCount)
            {
                return targetIndex;
            }

            return -1;
        }

        private List<int> BuildProductCandidates(int productCount)
        {
            var candidates = new List<int>(productCount);

            for (int i = 0; i < productCount; i++)
            {
                // Önce ardışık tekrarı kesinlikle engelle
                if (i == _lastProductIndex && productCount > 1)
                {
                    continue;
                }

                // Sonra recent history'e bak
                if (!_recentProductIndices.Contains(i))
                {
                    candidates.Add(i);
                }
            }

            return candidates;
        }

        private int SelectProductIndex(List<int> candidates, int productCount)
        {
            // Eğer hiç aday kalmadıysa, sadece son ürünü hariç tut
            if (candidates.Count == 0)
            {
                var fallbackCandidates = new List<int>();
                for (int i = 0; i < productCount; i++)
                {
                    // Son ürünü kesinlikle ekleme (2'den fazla ürün varsa)
                    if (i != _lastProductIndex || productCount <= 1)
                    {
                        fallbackCandidates.Add(i);
                    }
                }

                if (fallbackCandidates.Count > 0)
                {
                    return fallbackCandidates[Random.Range(0, fallbackCandidates.Count)];
                }

                // Son çare: herhangi birini seç (tek ürün varsa)
                return Random.Range(0, productCount);
            }

            return candidates[Random.Range(0, candidates.Count)];
        }

        private void AddToProductHistory(int productIndex)
        {
            _lastProductIndex = productIndex; // Ardışık engeli için kaydet

            _recentProductIndices.Enqueue(productIndex);

            // History size kontrolü
            int maxHistorySize = Mathf.Max(1, recentProductHistorySize);
            while (_recentProductIndices.Count > maxHistorySize)
            {
                _recentProductIndices.Dequeue();
            }
        }

        /// <summary>
        /// Ürün geçmişini temizler
        /// </summary>
        public void ClearProductHistory()
        {
            _recentProductIndices.Clear();
            _lastProductIndex = -1;
        }

        #endregion

        #region Public API

        /// <summary>
        /// M��teri spawn iste�i (ServerRpc)
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void RequestCustomerSpawnServerRpc()
        {
            if (!IsWithinSpawningHours()) return;

            int availableIndex = GetNextAvailableQueueIndex();
            if (availableIndex != -1)
            {
                SpawnCustomer(availableIndex);
            }
        }

        /// <summary>
        /// Kuyruk boyutunu d�nd�r�r
        /// </summary>
        public int GetQueueSize() => QueueSize;

        /// <summary>
        /// Kalan m��teri say�s�n� d�nd�r�r
        /// </summary>
        public int GetRemainingCustomers() => _customersRemainingToday;

        /// <summary>
        /// Spawn durumu bilgisini d�nd�r�r
        /// </summary>
        public string GetSpawningStatusInfo()
        {
            if (DayCycleManager.Instance == null)
            {
                return "DayCycleManager not found";
            }

            return BuildStatusInfoString();
        }

        private string BuildStatusInfoString()
        {
            float currentTime = GetCurrentTime();
            bool canSpawn = IsWithinSpawningHours();

            return $"Day {DayCycleManager.Instance.currentDay}\n" +
                   $"Current Time: {currentTime:F2}\n" +
                   $"Spawn Window: {spawnStartHour:F1}-{spawnEndHour:F1}\n" +
                   $"Customers Today: {_customersSpawnedToday}/{_todaysTotalCustomers}\n" +
                   $"Remaining: {_customersRemainingToday}\n" +
                   $"Queue Size: {QueueSize}/{maxQueueSize}\n" +
                   $"Can Spawn: {canSpawn}";
        }

        #endregion

        #region Logging

        private void LogDebug(string message)
        {
            if (showDebugLogs)
            {
                Debug.Log($"{LOG_PREFIX} {message}");
            }
        }

        private void LogWarning(string message)
        {
            Debug.LogWarning($"{LOG_PREFIX} {message}");
        }

        private void LogError(string message)
        {
            Debug.LogError($"{LOG_PREFIX} {message}");
        }

        #endregion

        #region Editor Debug

#if UNITY_EDITOR
        [ContextMenu("Show Daily Customer Info")]
        private void DebugShowDailyCustomerInfo()
        {
            Debug.Log(GetSpawningStatusInfo());

            if (_scheduledSpawnTimes.Count > 0)
            {
                Debug.Log("\nScheduled spawn times:");
                for (int i = 0; i < _scheduledSpawnTimes.Count; i++)
                {
                    string status = i < _nextScheduledIndex ? "[SPAWNED]" : "[PENDING]";
                    Debug.Log($"  Customer {i + 1}: {_scheduledSpawnTimes[i]:F2} {status}");
                }
            }
        }

        [ContextMenu("Force Spawn Next Customer")]
        private void DebugForceSpawnNextCustomer()
        {
            if (!IsServer) return;

            int availableIndex = GetNextAvailableQueueIndex();
            if (availableIndex != -1)
            {
                SpawnCustomer(availableIndex);
                _customersSpawnedToday++;

                if (_nextScheduledIndex < _scheduledSpawnTimes.Count)
                {
                    _nextScheduledIndex++;
                }
            }
        }

        [ContextMenu("Reset Daily Customers")]
        private void DebugResetDailyCustomers()
        {
            if (!IsServer) return;
            InitializeDailyCustomers();
        }

        [ContextMenu("Clear Queue")]
        private void DebugClearQueue()
        {
            if (!IsServer) return;

            foreach (var customer in _customerQueue.ToList())
            {
                if (customer != null)
                {
                    var netObj = customer.GetComponent<NetworkObject>();
                    if (netObj != null && netObj.IsSpawned)
                    {
                        netObj.Despawn();
                    }
                }
            }

            _customerQueue.Clear();
            LogDebug("Queue cleared");
        }

        [ContextMenu("Spawn 5 Customers")]
        private void DebugSpawn5Customers()
        {
            if (!IsServer) return;

            for (int i = 0; i < 5; i++)
            {
                int availableIndex = GetNextAvailableQueueIndex();
                if (availableIndex == -1) break;

                SpawnCustomer(availableIndex);
                _customersSpawnedToday++;
            }
        }

        [ContextMenu("Debug: Print State")]
        private void DebugPrintState()
        {
            Debug.Log($"{LOG_PREFIX} === CUSTOMER MANAGER STATE ===");
            Debug.Log($"Is Server: {IsServer}");
            Debug.Log($"Day Initialized: {_dayInitialized}");
            Debug.Log($"Today's Total: {_todaysTotalCustomers}");
            Debug.Log($"Spawned Today: {_customersSpawnedToday}");
            Debug.Log($"Remaining Today: {_customersRemainingToday}");
            Debug.Log($"Queue Size: {QueueSize}/{maxQueueSize}");
            Debug.Log($"Next Scheduled Index: {_nextScheduledIndex}/{_scheduledSpawnTimes.Count}");
            Debug.Log($"Is Within Spawning Hours: {IsWithinSpawningHours()}");
            Debug.Log($"Is Queue Full: {IsQueueFull}");
            Debug.Log($"Recent Product History: {string.Join(", ", _recentProductIndices)}");

            Debug.Log($"--- Queue Contents ---");
            for (int i = 0; i < _customerQueue.Count; i++)
            {
                var customer = _customerQueue[i];
                Debug.Log($"  [{i}] {(customer != null ? customer.name : "NULL")} - QueueIndex: {customer?.GetTargetQueueIndex() ?? -1}");
            }
        }

        [ContextMenu("Debug: Print Queue Positions")]
        private void DebugPrintQueuePositions()
        {
            Debug.Log($"{LOG_PREFIX} === QUEUE POSITIONS ===");

            if (queuePositions == null || queuePositions.Length == 0)
            {
                Debug.Log("No queue positions assigned!");
                return;
            }

            for (int i = 0; i < queuePositions.Length; i++)
            {
                bool isOccupied = IsQueueSlotOccupied(i);
                string status = isOccupied ? "[OCCUPIED]" : "[EMPTY]";
                string posName = queuePositions[i] != null ? queuePositions[i].name : "NULL";
                Debug.Log($"  [{i}] {posName} {status}");
            }
        }

        private void OnDrawGizmosSelected()
        {
            DrawQueueGizmos();
            DrawSpawnPointGizmo();
            DrawExitPointGizmo();
        }

        private void DrawQueueGizmos()
        {
            if (queuePositions == null) return;

            Gizmos.color = Color.cyan;
            for (int i = 0; i < queuePositions.Length; i++)
            {
                if (queuePositions[i] == null) continue;

                Gizmos.DrawWireSphere(queuePositions[i].position, 0.5f);
                UnityEditor.Handles.Label(queuePositions[i].position + Vector3.up, $"Queue {i}");

                // Draw line between queue positions
                if (i > 0 && queuePositions[i - 1] != null)
                {
                    Gizmos.DrawLine(queuePositions[i - 1].position, queuePositions[i].position);
                }
            }
        }

        private void DrawSpawnPointGizmo()
        {
            if (spawnPoint == null) return;

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(spawnPoint.position, 0.75f);
            UnityEditor.Handles.Label(spawnPoint.position + Vector3.up, "SPAWN");
        }

        private void DrawExitPointGizmo()
        {
            if (exitPoint == null) return;

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(exitPoint.position, 0.75f);
            UnityEditor.Handles.Label(exitPoint.position + Vector3.up, "EXIT");
        }
#endif

        #endregion
    }
}