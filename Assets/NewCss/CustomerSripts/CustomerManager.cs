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
        public const int DEFAULT_QUEUE_SIZE = 2;
        private const float DEFAULT_SPAWN_START_HOUR = 8f;
        private const float DEFAULT_SPAWN_END_HOUR = 17f;
        /// <summary>
        /// 17:30 — kuyruktaki müşterilerin çıkışa zorlandığı saat. PhoneCallManager de okur:
        /// zaman atlaması bu eşiği aşacaksa çağrıyı reddeder (QA bulgusu 2026-08-29).
        /// </summary>
        public const float CUSTOMER_EXIT_HOUR = 17.5f;

        public static event System.Action OnDailyCustomersCalculated;

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

        [Tooltip("Sabirli Musteriler perki: min/maxWaitTime carpani (CustomerAI.InitializeServerState'te okunur). Perk yoksa 1f.")]
        public float patienceMultiplier = 1f;

        [Tooltip("Sabirli Musteriler perki FAZ4 tercih: CustomerAI.interactionTime carpani (2sn -> 1.2sn). Perk yoksa 1f.")]
        public float interactionTimeMultiplier = 1f;

        #endregion

        #region Serialized Fields - Economy Settings

        [Header("=== ECONOMY SETTINGS ===")]
        [SerializeField, Tooltip("Tüm ekonomi sabitlerini içeren ScriptableObject (kota eğrisi + varış aralığı). Boşsa Resources/EkonomiAyarlari'dan otomatik yüklenir.")]
        private GameEconomySettings economySettings;

        #endregion

        #region Serialized Fields - Quota & Spawn Timing Settings

        [Header("=== QUOTA & SPAWN TIMING SETTINGS ===")]
        [SerializeField, Tooltip("Günlük minimum müşteri sayısı (güvenlik clamp'i)")]
        private int _minCustomersPerDay = 1;

        [SerializeField, Tooltip("Günlük maksimum müşteri sayısı (Soft Cap - performans koruması)")]
        private int _maxCustomersPerDay = 50;

        [SerializeField, Range(0f, 1f), Tooltip("Müşteriler arası varış aralığına uygulanan rastgelelik payı (0 = hep aynı aralık, 0.2 = ±%20 jitter). PlateUp geçişi (2026-08-29): mutlak-saat rastgeleliğinden aralık jitter'ına taşındı, alan aynı kaldı.")]
        public float spawnTimeRandomness = 0.2f;

        #endregion

        #region Runtime Multipliers (set by other systems)

        /// <summary>
        /// Event sistemi tarafından set edilen müşteri çarpanı (örn: MARKETING DAY = +%20)
        /// </summary>
        [HideInInspector]
        public float eventCustomerMultiplier = 1f;

        /// <summary>
        /// ARTIK KULLANILMIYOR (PlateUp geçişi 2026-08-29): kota artık GameEconomySettings'teki
        /// P-indeksli gün eğrisinden (dailyCustomerCountP1..P4) okunuyor, düz çarpan değil.
        /// Alan yalnızca DifficultyManager.ApplyCustomerSettings:430'un derlenmesi için duruyor —
        /// orası da bu alanı yazmayı bırakacak şekilde ayrı bir işte temizlenmeli (rapora bakın).
        /// </summary>
        [HideInInspector]
        public float playerCountMultiplier = 1f;

        #endregion



        #region Serialized Fields - Queue & Tables

        [Header("=== QUEUE & TABLES ===")]
        [SerializeField, Tooltip("Kuyruk waypoint yöneticisi")]
        public QueueWaypoint queueWaypoint;

        [SerializeField, Tooltip("k noktas")]
        public Transform exitPoint;

        [SerializeField, Tooltip("Brakma masas")]
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
        // PlateUp geçişi (2026-08-29): mutlak-saat listesi (_scheduledSpawnTimes) yerine
        // "bir sonraki spawn en erken ne zaman olabilir" eşiği — DayCycleManager.elapsedTime
        // (real saniye) domeninde. Bkz. AdvanceNextSpawnThreshold.
        private float _nextSpawnAllowedElapsedTime;
        private bool _dayInitialized;
        private bool _customersExitedToday; // Gün sonu müşteri çıkışı yapıldı mı
        private bool _dayCompletionGraceStarted; // Erken gün-bitişi kapanış payı başladı mı
        private bool _dayFastForwarded;          // Gün sonuna sarma bir kez yapıldı mı (tek atış)
        private float _dayCompletionGraceRemaining;

        #endregion

        #region Private Fields - Product History

        private readonly Queue<int> _recentProductIndices = new();
        private int _lastProductIndex = -1; // Ardışık tekrar engeli için

        #endregion

        #region Private Fields - Color Bag

        /// <summary>
        /// Renk-önce dengeli torba: [Red, Yellow, Blue] karıştırılmış, replacement'sız çekilir.
        /// Her 3 müşteride üç renk de garanti gelir.
        /// </summary>
        private readonly List<BoxInfo.BoxType> _customerColorBag = new();

        /// <summary>
        /// "Orta" kayırma dengesi: dengeli torba ANA kaynaktır; mevcut tıra yalnız FAVOR_CHANCE
        /// olasılığıyla hafif meyledilir (tek tır dururken ~yarısı o renge uyar, gerisi çeşitli).
        /// Nihai renk üzerinde art arda en fazla MAX_CONSECUTIVE_SAME_COLOR aynı renge izin verilir.
        /// </summary>
        private const int MAX_CONSECUTIVE_SAME_COLOR = 2;
        private const float FAVOR_CHANCE = 0.35f;
        private BoxInfo.BoxType _lastCustomerColor;
        private int _consecutiveColorCount;

        /// <summary>
        /// Renk→index map cache'i. Tüm müşteriler aynı productPrefabs içeriğini taşıdığından ilk
        /// kurulumda hesaplanıp tekrar kullanılır; prefab sayısı değişirse (guard) yeniden kurulur.
        /// </summary>
        private Dictionary<BoxInfo.BoxType, List<int>> _colorIndexMap;
        private int _colorIndexMapProductCount = -1;

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
        /// Sırada henüz spawnlanmamış müşteri var mı? (Telefon sistemi için)
        /// </summary>
        public bool HasUnspawnedCustomers => _customersSpawnedToday < _todaysTotalCustomers;

        /// <summary>
        /// Kuyruk dolu mu?
        /// </summary>
        public bool IsQueueFull => _customerQueue.Count >= maxQueueSize ||
                                    (queueWaypoint != null && _customerQueue.Count >= queueWaypoint.WaypointCount);

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
            if (Instance != this) return;

            LocalizationHelper.OnLocaleChanged += OnLocaleChanged;
            
            if (IsServer)
            {
                SubscribeToDayCycleEvents();
                InitializeDailyCustomers();
            }
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
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
            AssignFreeServiceStations();
            CheckEndOfDayCustomerExit();
            TickEarlyDayCompletionGrace();
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
            _dayInitialized = true;
            _customersExitedToday = false; // Yeni gün için çıkış flag'ini sıfırla
            _dayCompletionGraceStarted = false;  // Erken gün-bitişi guard'ını sıfırla
            _dayFastForwarded = false;           // Yeni gün: sarma tek-atış bayrağını sıfırla
            _dayCompletionGraceRemaining = 0f;
            _customerColorBag.Clear(); // Renk torbasını sıfırla (gün başı yeni torba)
            _consecutiveColorCount = 0; // Renk streak'ini sıfırla (gün başı)

            InitializeSpawnTiming();
            UpdateRemainingCustomersUI();

            LogDebug($"Day {currentDay} - Total customers scheduled: {_todaysTotalCustomers}");
            LogDebug($"Spawn window {spawnStartHour:F1}-{spawnEndHour:F1}, arrival interval base: {GetCustomerArrivalInterval():F1}s");

            OnDailyCustomersCalculated?.Invoke();
        }

        /// <summary>
        /// Gün-numarası eğrisine göre müşteri sayısı hesaplama (PlateUp geçişi 2026-08-29).
        /// Kapasite (raf/masa) etkisi tamamen kalktı — kota artık GameEconomySettings'teki
        /// P×gün tablosundan (dailyCustomerCountP1..P4) okunuyor. Server-only çalışır.
        /// </summary>
        private int CalculateTodaysCustomerCount(int currentDay)
        {
            int playerCount = DifficultyManager.Instance != null ? DifficultyManager.Instance.PlayerCount : 1;
            var settings = GetEconomySettings();
            int baseCount = settings != null ? settings.GetDailyCustomerCount(currentDay, playerCount) : 4;

            // Event çarpanı (örn. MARKETING DAY) tabanın üzerine uygulanır.
            float multipliedCount = baseCount * eventCustomerMultiplier;

            // Clamp: asla 0/negatif olmasın, soft cap'i aşmasın
            int finalCount = Mathf.Clamp(Mathf.RoundToInt(multipliedCount), _minCustomersPerDay, _maxCustomersPerDay);

            LogDebug($"Quota calc: day={currentDay}, players={playerCount}, base={baseCount}, " +
                     $"eventMult={eventCustomerMultiplier:F2}, final={finalCount}");

            return finalCount;
        }

        /// <summary>
        /// Gün başında spawn zamanlamasını sıfırlar. PlateUp geçişi (2026-08-29): mutlak-saat
        /// listesi yerine "önceki gelişten customerArrivalInterval saniye sonra sıradaki" akışı.
        /// İlk müşteri, mesai saatleri açılır açılmaz (IsWithinSpawningHours) hemen gelebilsin
        /// diye eşik "şimdi"ye ayarlanır.
        /// </summary>
        private void InitializeSpawnTiming()
        {
            _nextSpawnAllowedElapsedTime = DayCycleManager.Instance != null ? DayCycleManager.Instance.elapsedTime : 0f;
        }

        /// <summary>
        /// Oyuncu sayısına göre temel varış aralığını (saniye) döndürür.
        /// </summary>
        private float GetCustomerArrivalInterval()
        {
            int playerCount = DifficultyManager.Instance != null ? DifficultyManager.Instance.PlayerCount : 1;
            var settings = GetEconomySettings();
            return settings != null ? settings.GetCustomerArrivalIntervalSeconds(playerCount) : 22f;
        }

        /// <summary>
        /// Bir müşteri spawn olduktan sonra sıradakinin en erken ne zaman gelebileceğini
        /// (DayCycleManager.elapsedTime domeninde) hesaplar. Wave sistemi rush hour'da aralığı
        /// kısaltır (yüksek spawnRateMultiplier = daha sık geliş); spawnTimeRandomness eskiden
        /// mutlak-saate uygulanan jitter'ı artık doğrudan aralığa uygular.
        /// </summary>
        private void AdvanceNextSpawnThreshold()
        {
            float elapsed = DayCycleManager.Instance != null ? DayCycleManager.Instance.elapsedTime : 0f;
            float baseInterval = GetCustomerArrivalInterval();

            float jitter = Random.Range(-baseInterval * spawnTimeRandomness, baseInterval * spawnTimeRandomness);
            float interval = Mathf.Max(1f, baseInterval + jitter);

            if (enableWaveSystem && waveSettings != null)
            {
                float spawnRateMultiplier = waveSettings.GetSpawnRateMultiplier(GetCurrentTime());
                if (spawnRateMultiplier > 0f)
                {
                    interval /= spawnRateMultiplier;
                }
            }

            _nextSpawnAllowedElapsedTime = elapsed + interval;
        }

        #endregion

        #region Spawn Logic

        private void TrySpawnScheduledCustomer()
        {
            if (!CanSpawnScheduledCustomer()) return;

            float elapsed = DayCycleManager.Instance.elapsedTime;

            if (elapsed >= _nextSpawnAllowedElapsedTime)
            {
                TryExecuteSpawn(GetCurrentTime());
            }
        }

        private bool CanSpawnScheduledCustomer()
        {
            if (DayCycleManager.Instance == null) return false;
            if (!IsWithinSpawningHours()) return false;
            if (!HasUnspawnedCustomers) return false;
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

        /// <summary>
        /// Spawn gerçekten yapıldıysa true döner. Dönüş değeri ÖNEMLİ: telefon yolu
        /// (ForceSpawnNextCustomer → PhoneCallManager) ödülü/cooldown'u yalnız gerçek bir
        /// spawn olduğunda vermeli. IsQueueFull sayı-tabanlı, GetNextAvailableQueueIndex ise
        /// pozisyon-tabanlı; ikisi kayarsa guard'lar geçip spawn yine de başarısız olabilir
        /// (QA bulgusu 2026-08-29 — eskiden burası void'di ve çağıran koşulsuz "başarılı" sayıyordu).
        /// </summary>
        private bool TryExecuteSpawn(float currentTime)
        {
            int nextQueueIndex = GetNextAvailableQueueIndex();

            if (nextQueueIndex == -1)
            {
                LogDebug("TryExecuteSpawn: no free queue slot, spawn skipped.");
                return false;
            }

            SpawnCustomer(nextQueueIndex);
            _customersSpawnedToday++;
            AdvanceNextSpawnThreshold();

            LogDebug($"Customer {_customersSpawnedToday}/{_todaysTotalCustomers} spawned at time {currentTime:F2}");
            return true;
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
        /// GameEconomySettings referansını döndürür; Inspector'dan bağlanmamışsa
        /// Resources/EkonomiAyarlari'dan yükleyip cache'ler (Truck.cs'teki desenle aynı).
        /// </summary>
        private GameEconomySettings GetEconomySettings()
        {
            if (economySettings == null)
            {
                economySettings = Resources.Load<GameEconomySettings>("EkonomiAyarlari");
            }
            return economySettings;
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
                ApplyMissedQuotaPenalty();
                _customersExitedToday = true;
                LogDebug($"End of day customer exit triggered at {currentTime:F2}");
            }
        }

        /// <summary>
        /// Kuyruktaki tüm müşterileri çıkışa yönlendirir. PlateUp gün-sonu cezası (§E,
        /// plans/plateup-musteri-telefon.md, 2026-08-29): servis edilmeden (WaitingForPickup
        /// DEĞİL) çıkışa zorlanan her müşteri için GameStateManager.OnCustomerLost() çağrılır.
        /// Sabrı bitip HandleTimeUp'tan (zaten OnCustomerLost çağırmış) geçen müşteriler normal
        /// akışta bu noktaya ulaşmadan önce kuyruktan çıkmış olur (TransitionToExit →
        /// NotifyCustomerDone senkron olarak kaldırıyor); yine de CustomerAI.HasTimedOut ile
        /// çifte-sayıma karşı savunma amaçlı kontrol edilir (plan §E'nin açık isteği).
        /// </summary>
        private void ForceAllCustomersToExit()
        {
            if (_customerQueue.Count == 0) return;

            LogDebug($"Forcing {_customerQueue.Count} customers to exit");

            foreach (var customer in _customerQueue.ToList())
            {
                if (customer == null) continue;

                // ForceExitDueToEndOfDay durumu Exiting'e çevirmeden ÖNCE yakala.
                bool shouldPenalize = !customer.HasFinishedService && !customer.HasTimedOut;

                customer.ForceExitDueToEndOfDay();

                if (shouldPenalize && GameStateManager.Instance != null)
                {
                    GameStateManager.Instance.OnCustomerLost();
                }
            }
        }

        /// <summary>
        /// PlateUp gün-sonu cezası (§E): hiç spawn olmamış kalan kota müşterileri için, servis
        /// edilmeden kaçan müşterilerden (customerLostPrestigePenalty=-0.4) AYRI ve daha hafif bir
        /// prestij cezası (customerMissedQuotaPrestigePenalty=-0.2) uygular. Yalnızca
        /// CheckEndOfDayCustomerExit'in 17:30 yolundan çağrılır — erken gün bitişinde
        /// (CheckEarlyDayCompletion) zaten HasUnspawnedCustomers=false şartı arandığı için bu
        /// yol hiç tetiklenmez.
        /// </summary>
        private void ApplyMissedQuotaPenalty()
        {
            int missedCount = _todaysTotalCustomers - _customersSpawnedToday;
            if (missedCount <= 0) return;
            if (GameStateManager.Instance == null) return;

            for (int i = 0; i < missedCount; i++)
            {
                GameStateManager.Instance.OnCustomerQuotaMissed();
            }

            LogDebug($"Missed quota penalty applied for {missedCount} unspawned customer(s).");
        }

        #endregion

        #region Queue Index Management

        private int GetNextAvailableQueueIndex()
        {
            int maxIndex = maxQueueSize;
            if (queueWaypoint != null)
            {
                maxIndex = Mathf.Min(queueWaypoint.WaypointCount, maxQueueSize);
            }

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
            if (!HasUnspawnedCustomers)
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

            // Mevcut zamanı göndererek spawnla. Sonucu OLDUĞU GİBİ ilet — eskiden burası
            // TryExecuteSpawn'ın sessiz başarısızlığını yutup koşulsuz true dönüyordu, yani
            // telefon hiç müşteri gelmeden ödül+cooldown+zaman-atlaması ödeyebiliyordu.
            bool spawned = TryExecuteSpawn(GetCurrentTime());
            LogDebug($"ForceSpawnNextCustomer: spawned={spawned}");
            return spawned;
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

            var customerAI = customerObject.GetComponent<CustomerAI>();
            if (customerAI == null)
            {
                LogError("Customer prefab has no CustomerAI component!");
                Destroy(customerObject);
                return;
            }

            SetupCustomerAI(customerAI, queueIndex);

            // F10 fix: buff verildiği anda sahnede olmayan (BuffManager listesi zaten dolmuşken sonradan
            // spawn olan) müşteriler CustomerWaitTime buff'ını hiç almıyordu. Spawn() ÇAĞRILMADAN ÖNCE
            // uygulanmalı: CustomerAI.InitializeServerState, Spawn() sırasında senkron çalışan
            // OnNetworkSpawn içinden minWaitTime/maxWaitTime'ı okuyup _actualWaitTime'ı hesaplıyor.
            // Çift-uygulama guard BuffManager.ApplyActiveBuffsTo içinde (bkz. Assets/Scripts/Quest/Buff/BuffManager.cs).
            Quest.BuffManager.Instance?.ApplyActiveBuffsTo(customerAI);

            networkObject.Spawn();

            // Rastgele kıyafet/görünüm: Spawn() ÇAĞRILDIKTAN SONRA uygulanır. NetworkVariable'lar
            // Spawn() sırasında InitializeVariables() ile NetworkBehaviour'a bağlanır; bundan önce
            // .Value set etmek fonksiyonel olarak zararsız olsa da NGO'nun "NetworkVariable is written
            // to, but doesn't know its NetworkBehaviour yet" uyarısını basıyordu (her customer spawn'ında
            // ~10 log). Mesh swap zaten OnNetworkSpawn -> ApplyAllCustomizations() ile güncel .Value'yu
            // okuyup OnValueChanged event'iyle tetiklendiğinden sıralamayı buraya almak sonucu bozmaz.
            customerObject.GetComponent<CustomerMeshSwapper>()?.RandomizeOutfit();

            SetupCustomerClientRpc(networkObject.NetworkObjectId, queueIndex);

            _customerQueue.Add(customerAI);
            
            if (queueWaypoint != null)
            {
                customerAI.SetQueueTarget(queueWaypoint.GetWaypointPosition(queueIndex), queueIndex);
            }
        }

        #endregion

        #region Customer Setup

        private void SetupCustomerAI(CustomerAI ai, int queueIndex)
        {
            ai.isPrefabMode = false;
            ai.manager = this;
            ai.exitPoint = exitPoint;
            // dropOffTable BURADA atanmıyor: 2 paralel istasyon modelinde hangi masayı
            // kullanacağı AssignFreeServiceStations()'ta (kuyruğa girdikten sonra, boş bir
            // istasyon bulununca) belirlenir — bkz. CustomerAI.AssignServiceStation().

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
            // Servis istasyonunu serbest bırak (atanmamışsa no-op) — müşteri kuyruktan hangi
            // sebeple ayrılırsa ayrılsın (tamamlandı/timeout/gün sonu) tek funnel burası.
            ReleaseServiceStation(customer);

            if (!_customerQueue.Remove(customer))
            {
                return;
            }

            _customersRemainingToday--;
            UpdateRemainingCustomersUI();
            UpdateRemainingCustomersClientRpc(_customersRemainingToday);

            AdvanceQueue();
            CheckEarlyDayCompletion();

            LogDebug($"Customer left.  Remaining today: {_customersRemainingToday}");
        }

        /// <summary>
        /// PlateUp erken gün-bitişi (2026-08-29, plan §C): günün kotası tükenip son müşteri de
        /// çıktığında, kısa bir kapanış payının ardından günü sona sarar. Üç koşul BİRDEN
        /// aranır (tekil sayaç desenkronize olsa bile güvenli): kalan kota <= 0, kuyruk boş,
        /// henüz spawn olmamış müşteri yok. İkinci bir gün-bitiş yolu AÇMAZ — yalnızca
        /// DayCycleManager._networkElapsedTime'ı doldurur, ProcessDayEnd()'in tek koşulunu
        /// (elapsedTime >= CurrentDayDuration) doğal yoldan tetikler; kira/break room/gün sonu
        /// ekranı/IsTimeUp exploit guard'ı değişmeden çalışmaya devam eder.
        /// </summary>
        private void CheckEarlyDayCompletion()
        {
            if (!IsServer) return;
            if (_dayCompletionGraceStarted) return;
            if (_customersRemainingToday > 0) return;
            if (_customerQueue.Count > 0) return;
            if (HasUnspawnedCustomers) return;

            _dayCompletionGraceStarted = true;
            _dayCompletionGraceRemaining = GetEconomySettings() != null
                ? GetEconomySettings().dayEndGraceSeconds
                : 30f;

            LogDebug($"All customers served/exited — day will fast-forward to end after {_dayCompletionGraceRemaining}s grace period.");
        }

        /// <summary>
        /// Kapanış payını Update() üzerinden işletir. BİLİNÇLİ olarak coroutine DEĞİL:
        /// obje/komponent bir kare deaktif olursa Unity coroutine'i öldürür, ama
        /// _dayCompletionGraceStarted bayrağı true takılı kalacağı için CheckEarlyDayCompletion
        /// bir daha hiç denemez ve erken bitiş SESSİZCE ölürdü (aynı sınıf hata:
        /// UnifiedSettingsManager._isLocalizationChanging, 2026-08-22). Bu dosyadaki
        /// CheckEndOfDayCustomerExit de aynı Update+bayrak desenini kullanıyor.
        /// </summary>
        private void TickEarlyDayCompletionGrace()
        {
            if (!_dayCompletionGraceStarted || _dayFastForwarded) return;

            _dayCompletionGraceRemaining -= Time.deltaTime;
            if (_dayCompletionGraceRemaining > 0f) return;

            _dayFastForwarded = true;
            DayCycleManager.Instance?.FastForwardToEndOfDay();
            LogDebug("Grace period over — fast-forwarding to end of day.");
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
            if (queueWaypoint == null) return;

            for (int i = 0; i < _customerQueue.Count; i++)
            {
                var customer = _customerQueue[i];

                if (customer.isPrefabMode) continue;
                if (customer.GetTargetQueueIndex() == i) continue;

                customer.SetQueueTarget(queueWaypoint.GetWaypointPosition(i), i);
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
        /// Müşteriye masa atar ve servisi başlatır. AssignFreeServiceStations (ve tek-masa
        /// fallback'i) tarafından çağrılır — server-only.
        /// </summary>
        public void AssignDropOffTable(CustomerAI customer, DisplayTable table)
        {
            if (customer == null || table == null) return;

            customer.AssignServiceStation(table);
        }

        #endregion

        #region Service Station Management

        /// <summary>
        /// serviceTables ile paralel dizi: index i'deki masa şu an hangi müşteri tarafından
        /// kullanılıyor (null = boş). economy-rebuild FAZ4 §B.4 — 2 paralel istasyon: eskiden
        /// yalnızca kuyruğun başındaki müşteri (IsFirstInQueue) servise girebiliyordu; bu tek
        /// masa çekişmesi çok-oyunculu prestij/gün'ü ters ölçekliyordu (bkz. plan §A.3).
        /// </summary>
        private CustomerAI[] _stationOccupants;

        private void EnsureStationOccupantsInitialized()
        {
            int stationCount = serviceTables != null ? serviceTables.Length : 0;
            if (_stationOccupants == null || _stationOccupants.Length != stationCount)
            {
                _stationOccupants = new CustomerAI[stationCount];
            }
        }

        /// <summary>
        /// Kuyrukta bekleyen müşterilere, kuyruk sırasına göre boş servis istasyonu atar.
        /// Server-only; CustomerManager.Update()'ten her frame çağrılır.
        /// </summary>
        private void AssignFreeServiceStations()
        {
            if (_customerQueue.Count == 0) return;

            EnsureStationOccupantsInitialized();

            if (_stationOccupants.Length == 0)
            {
                // Sahne hâlâ eski tek-masa kurulumundaysa (serviceTables boş) eski davranışa düş:
                // yalnızca kuyruğun başındaki müşteri servise girebilir.
                var first = _customerQueue[0];
                if (dropOffTable != null && first != null && first.IsWaitingForService)
                {
                    AssignDropOffTable(first, dropOffTable);
                }
                return;
            }

            // Kuyruk sırasına göre (index 0 önce) boş istasyonlara ata — fairness için sıralı kalır.
            SortQueueByIndex();

            foreach (var customer in _customerQueue)
            {
                if (customer == null || !customer.IsWaitingForService) continue;

                int freeIndex = FindFreeStationIndex();
                if (freeIndex == -1) break; // hiç boş istasyon kalmadı

                _stationOccupants[freeIndex] = customer;
                AssignDropOffTable(customer, serviceTables[freeIndex]);
            }
        }

        private int FindFreeStationIndex()
        {
            for (int i = 0; i < _stationOccupants.Length; i++)
            {
                if (_stationOccupants[i] == null && serviceTables[i] != null)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Müşteri kuyruktan ayrıldığında (tamamladı / timeout / gün sonu zorlaması) istasyonunu
        /// serbest bırakır. Server-only; atanmamış müşteride veya boş dizide no-op (güvenli).
        /// </summary>
        private void ReleaseServiceStation(CustomerAI customer)
        {
            if (customer == null || _stationOccupants == null) return;

            for (int i = 0; i < _stationOccupants.Length; i++)
            {
                if (_stationOccupants[i] == customer)
                {
                    _stationOccupants[i] = null;
                    return;
                }
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

        #region Return Box Mode Assignment (Faz A — gün 5 iade)

        /// <summary>
        /// Bu müşteri İade (BoxRequest) moduna mı girmeli? Gün eşiği + oran kontrolü saf
        /// <see cref="PostRentFeatureUnlocks.ShouldEnterBoxRequestMode"/> içinde; burada sadece
        /// gerçek currentDay/roll sağlanır (DraftPool.MaxUnlockedTier çağrı deseniyle aynı).
        /// </summary>
        public bool ShouldAssignBoxRequestMode()
        {
            int currentDay = DayCycleManager.Instance != null ? DayCycleManager.Instance.currentDay : 0;
            return PostRentFeatureUnlocks.ShouldEnterBoxRequestMode(currentDay, Random.value);
        }

        /// <summary>İade modundaki müşterinin isteyeceği kutu rengini rastgele seçer (3 renk, eşit ağırlık).</summary>
        public BoxInfo.BoxType PickRandomReturnBoxType()
        {
            return (BoxInfo.BoxType)Random.Range(0, 3);
        }

        #endregion

        #region Dual Item Mode Assignment (Faz B — gün 9 2-item)

        /// <summary>
        /// Bu müşteri 2-item (dual item) modunda mı olmalı? Gün eşiği kontrolü saf
        /// <see cref="PostRentFeatureUnlocks.IsDualItemUnlocked"/> içinde (gün eşiği çağrı deseni
        /// ShouldAssignBoxRequestMode ile aynı). ProductSupply/BoxRequest mod seçiminden
        /// BAĞIMSIZ ayrı bir eksen — bir müşteri hem BoxRequest hem dual-item olabilir, hem
        /// ProductSupply hem dual-item olabilir.
        /// </summary>
        public bool ShouldAssignDualItemMode()
        {
            int currentDay = DayCycleManager.Instance != null ? DayCycleManager.Instance.currentDay : 0;
            return PostRentFeatureUnlocks.IsDualItemUnlocked(currentDay);
        }

        #endregion

        #region Product Assignment

        /// <summary>
        /// Önce renk seçer (dengeli torba + mevcut tıra hafif kayırma), sonra o renkten rastgele
        /// bir ürün prefabı index'i döndürür. Ardışık aynı ürün seçimini kesinlikle engeller.
        /// Renk→index eşlemesi <paramref name="productPrefabs"/> üzerinden (ProductInfo.productType ile)
        /// runtime kurulur/cache'lenir — sabit değildir, prefab listesi değişirse otomatik güncellenir.
        /// </summary>
        public int GetRandomProductIndexExcludingRecent(GameObject[] productPrefabs)
        {
            int productCount = productPrefabs?.Length ?? 0;
            if (productCount <= 0) return -1;
            if (productCount == 1) return 0;

            Dictionary<BoxInfo.BoxType, List<int>> colorIndexMap = GetOrBuildColorIndexMap(productPrefabs);

            BoxInfo.BoxType chosenColor = PickCustomerColor(colorIndexMap);
            List<int> pool = colorIndexMap.TryGetValue(chosenColor, out var indices) && indices.Count > 0
                ? indices
                : Enumerable.Range(0, productCount).ToList(); // güvenlik ağı: map boşsa tüm ürünlere düş

            var candidates = BuildProductCandidatesFromPool(pool);
            int chosen = SelectProductIndex(candidates, pool);
            AddToProductHistory(chosen);

            return chosen;
        }

        /// <summary>
        /// Bir sonraki müşteri rengini belirler ("Orta" denge):
        /// 1) Ana kaynak dengeli torba (1:1:1); mevcut tıra yalnız FAVOR_CHANCE olasılığıyla hafif meyil.
        /// 2) Nihai renk üzerinde art arda MAX_CONSECUTIVE_SAME_COLOR sınırı zorlanır (uzun aynı-renk serisi
        ///    olamaz — sınır aşılacaksa farklı bir renge zorlanır).
        /// </summary>
        private BoxInfo.BoxType PickCustomerColor(Dictionary<BoxInfo.BoxType, List<int>> colorIndexMap)
        {
            BoxInfo.BoxType? favoredColor = GetFavoredHangarTruckColor();
            bool canFavor = favoredColor.HasValue &&
                colorIndexMap.TryGetValue(favoredColor.Value, out var favoredCandidates) &&
                favoredCandidates.Count > 0;

            // Ana kaynak: dengeli torba. Sadece bazen (FAVOR_CHANCE) mevcut tıra hafif meyil.
            BoxInfo.BoxType chosen = (canFavor && Random.value < FAVOR_CHANCE)
                ? favoredColor.Value
                : DrawNextCustomerColorFromBag();

            // Nihai renk cap'i: art arda sınırı aşacaksa farklı renge zorla.
            if (chosen == _lastCustomerColor && _consecutiveColorCount >= MAX_CONSECUTIVE_SAME_COLOR)
            {
                chosen = PickDifferentColor(chosen, colorIndexMap);
            }

            // Streak takibi (nihai renk üzerinde).
            if (chosen == _lastCustomerColor)
            {
                _consecutiveColorCount++;
            }
            else
            {
                _lastCustomerColor = chosen;
                _consecutiveColorCount = 1;
            }

            return chosen;
        }

        /// <summary>
        /// Verilen renkten farklı, ürünü olan bir rengi rastgele döndürür (art arda cap'i için).
        /// Başka uygun renk yoksa aynı rengi döndürür (güvenlik ağı).
        /// </summary>
        private BoxInfo.BoxType PickDifferentColor(BoxInfo.BoxType avoid, Dictionary<BoxInfo.BoxType, List<int>> colorIndexMap)
        {
            _favoredColorScratch.Clear();
            foreach (var kvp in colorIndexMap)
            {
                if (kvp.Key != avoid && kvp.Value.Count > 0)
                {
                    _favoredColorScratch.Add(kvp.Key);
                }
            }

            if (_favoredColorScratch.Count == 0) return avoid;
            return _favoredColorScratch[Random.Range(0, _favoredColorScratch.Count)];
        }

        /// <summary>
        /// Hangarda kapasitesi olan (Truck present + dolu değil) tırların renklerinden birini rastgele
        /// döndürür (çoklu hangar için hangar-0 sabit bias'ı önlenir), hiç yoksa null.
        /// </summary>
        private BoxInfo.BoxType? GetFavoredHangarTruckColor()
        {
            TruckSpawner spawner = TruckSpawner.Instance;
            if (spawner == null || spawner.hangarSpawnPoints == null) return null;

            _favoredColorScratch.Clear();
            for (int i = 0; i < spawner.hangarSpawnPoints.Count; i++)
            {
                Truck truck = spawner.GetTruckAtHangar(i);
                if (truck != null && !truck.IsFull)
                {
                    _favoredColorScratch.Add(truck.requestedBoxType);
                }
            }

            if (_favoredColorScratch.Count == 0) return null;
            return _favoredColorScratch[Random.Range(0, _favoredColorScratch.Count)];
        }

        private readonly List<BoxInfo.BoxType> _favoredColorScratch = new();

        /// <summary>
        /// Renk-önce dengeli torbadan (replacement'sız) bir sonraki rengi çeker.
        /// Torba boşsa [Red, Yellow, Blue] ile yeniden doldurup karıştırır.
        /// </summary>
        private BoxInfo.BoxType DrawNextCustomerColorFromBag()
        {
            if (_customerColorBag.Count == 0)
            {
                _customerColorBag.Add(BoxInfo.BoxType.Red);
                _customerColorBag.Add(BoxInfo.BoxType.Yellow);
                _customerColorBag.Add(BoxInfo.BoxType.Blue);
                ShuffleColorBag(_customerColorBag);
            }

            int lastIndex = _customerColorBag.Count - 1;
            BoxInfo.BoxType color = _customerColorBag[lastIndex];
            _customerColorBag.RemoveAt(lastIndex);
            return color;
        }

        private static void ShuffleColorBag(List<BoxInfo.BoxType> bag)
        {
            for (int i = bag.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (bag[i], bag[j]) = (bag[j], bag[i]);
            }
        }

        /// <summary>
        /// productPrefabs dizisini tarayarak renk→ürün-index map'i kurar (ProductInfo.productType ile).
        /// Sonuç, aynı dizi referansı için weak-cache'lenir (runtime bir kez kurulur, sabit değildir).
        /// </summary>
        private Dictionary<BoxInfo.BoxType, List<int>> GetOrBuildColorIndexMap(GameObject[] productPrefabs)
        {
            // Aynı ürün sayısıyla daha önce kurulduysa yeniden kullan (tüm müşteriler aynı listeyi taşır).
            if (_colorIndexMap != null && _colorIndexMapProductCount == productPrefabs.Length)
            {
                return _colorIndexMap;
            }

            var map = new Dictionary<BoxInfo.BoxType, List<int>>
            {
                { BoxInfo.BoxType.Red, new List<int>() },
                { BoxInfo.BoxType.Yellow, new List<int>() },
                { BoxInfo.BoxType.Blue, new List<int>() },
            };

            for (int i = 0; i < productPrefabs.Length; i++)
            {
                GameObject prefab = productPrefabs[i];
                if (prefab == null) continue;

                ProductInfo productInfo = prefab.GetComponent<ProductInfo>();
                if (productInfo == null)
                {
                    LogWarning($"Product prefab '{prefab.name}' has no ProductInfo, skipped in color map.");
                    continue;
                }

                BoxInfo.BoxType boxType = ProductTypeToBoxType(productInfo.productType);
                map[boxType].Add(i);
            }

            _colorIndexMap = map;
            _colorIndexMapProductCount = productPrefabs.Length;
            return map;
        }

        /// <summary>
        /// Table.IsValidBoxProductCombination ile birebir eşleşen ürün→kutu renk eşlemesi:
        /// Toy→Red, Clothing→Yellow, Glass→Blue.
        /// </summary>
        private static BoxInfo.BoxType ProductTypeToBoxType(ProductInfo.ProductType productType)
        {
            return productType switch
            {
                ProductInfo.ProductType.Toy => BoxInfo.BoxType.Red,
                ProductInfo.ProductType.Clothing => BoxInfo.BoxType.Yellow,
                ProductInfo.ProductType.Glass => BoxInfo.BoxType.Blue,
                _ => BoxInfo.BoxType.Red,
            };
        }

        private List<int> BuildProductCandidatesFromPool(List<int> pool)
        {
            var candidates = new List<int>(pool.Count);

            foreach (int i in pool)
            {
                // Önce ardışık tekrarı kesinlikle engelle
                if (i == _lastProductIndex && pool.Count > 1)
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

        private int SelectProductIndex(List<int> candidates, List<int> pool)
        {
            // Eğer hiç aday kalmadıysa, sadece son ürünü hariç tut
            if (candidates.Count == 0)
            {
                var fallbackCandidates = new List<int>();
                foreach (int i in pool)
                {
                    // Son ürünü kesinlikle ekleme (2'den fazla ürün varsa)
                    if (i != _lastProductIndex || pool.Count <= 1)
                    {
                        fallbackCandidates.Add(i);
                    }
                }

                if (fallbackCandidates.Count > 0)
                {
                    return fallbackCandidates[Random.Range(0, fallbackCandidates.Count)];
                }

                // Son çare: havuzdan herhangi birini seç
                return pool[Random.Range(0, pool.Count)];
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
        /// Kalan mteri saysn dndrr
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

            float elapsed = DayCycleManager.Instance != null ? DayCycleManager.Instance.elapsedTime : 0f;
            Debug.Log($"Next spawn allowed at elapsedTime={_nextSpawnAllowedElapsedTime:F1}s " +
                      $"(current={elapsed:F1}s), HasUnspawnedCustomers={HasUnspawnedCustomers}, " +
                      $"ArrivalIntervalBase={GetCustomerArrivalInterval():F1}s");
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
                AdvanceNextSpawnThreshold();
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
            Debug.Log($"Has Unspawned Customers: {HasUnspawnedCustomers} (Next spawn threshold: {_nextSpawnAllowedElapsedTime:F1}s)");
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

            if (queueWaypoint == null || queueWaypoint.WaypointCount == 0)
            {
                Debug.Log("No queue waypoint manager assigned!");
                return;
            }

            for (int i = 0; i < queueWaypoint.WaypointCount; i++)
            {
                bool isOccupied = IsQueueSlotOccupied(i);
                string status = isOccupied ? "[OCCUPIED]" : "[EMPTY]";
                var wp = queueWaypoint.GetWaypoint(i);
                string posName = wp != null ? wp.name : "NULL";
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
            if (queueWaypoint == null) return;

            Gizmos.color = Color.cyan;
            for (int i = 0; i < queueWaypoint.WaypointCount; i++)
            {
                var pos = queueWaypoint.GetWaypointPosition(i);
                if (pos == Vector3.zero && queueWaypoint.GetWaypoint(i) == null) continue;

                Gizmos.DrawWireSphere(pos, 0.5f);
                UnityEditor.Handles.Label(pos + Vector3.up, $"Queue {i}");

                // Draw line between queue positions
                if (i > 0)
                {
                    Transform prevH = queueWaypoint.GetWaypoint(i - 1);
                    if (prevH != null)
                    {
                        Gizmos.DrawLine(prevH.position, pos);
                    }
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