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
    /// Müþteri yönetim sistemi - müþteri spawn'lama, kuyruk yönetimi ve günlük müþteri takibini saðlar. 
    /// Server-authoritative tasarým ile network senkronizasyonu içerir.
    /// </summary>
    public class CustomerManager : NetworkBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[CustomerManager]";
        private const int DEFAULT_QUEUE_SIZE = 3;
        private const float DEFAULT_SPAWN_START_HOUR = 8f;
        private const float DEFAULT_SPAWN_END_HOUR = 14f;

        #endregion

        #region Serialized Fields - Spawn Settings

        [Header("=== SPAWN SETTINGS ===")]
        [SerializeField, Tooltip("Müþteri prefab'ý")]
        public GameObject customerPrefab;

        [SerializeField, Tooltip("Spawn noktasý")]
        public Transform spawnPoint;

        [SerializeField, Tooltip("Maksimum kuyruk boyutu")]
        public int maxQueueSize = DEFAULT_QUEUE_SIZE;

        #endregion

        #region Serialized Fields - Daily Customer Settings

        [Header("=== DAILY CUSTOMER SETTINGS ===")]
        [SerializeField, Tooltip("Ýlk gün müþteri sayýsý")]
        public int baseCustomersPerDay = 10;

        [SerializeField, Tooltip("Günlük müþteri artýþý")]
        public int customerIncreasePerDay = 2;

        [SerializeField, Range(0f, 1f), Tooltip("Spawn zamaný rastgeleliði")]
        public float spawnTimeRandomness = 0.2f;

        #endregion

        #region Serialized Fields - Queue & Tables

        [Header("=== QUEUE & TABLES ===")]
        [SerializeField, Tooltip("Kuyruk pozisyonlarý")]
        public Transform[] queuePositions;

        [SerializeField, Tooltip("Çýkýþ noktasý")]
        public Transform exitPoint;

        [SerializeField, Tooltip("Býrakma masasý")]
        public DisplayTable dropOffTable;

        [SerializeField, Tooltip("Servis masalarý")]
        public DisplayTable[] serviceTables;

        #endregion

        #region Serialized Fields - Time Settings

        [Header("=== TIME SETTINGS ===")]
        [SerializeField, Tooltip("Spawn baþlangýç saati (24 saat formatý)")]
        public float spawnStartHour = DEFAULT_SPAWN_START_HOUR;

        [SerializeField, Tooltip("Spawn bitiþ saati (24 saat formatý)")]
        public float spawnEndHour = DEFAULT_SPAWN_END_HOUR;

        #endregion

        #region Serialized Fields - UI Settings

        [Header("=== UI SETTINGS ===")]
        [SerializeField, Tooltip("Kalan müþteri sayýsý text'i")]
        public TextMeshProUGUI remainingCustomersText;

        #endregion

        #region Serialized Fields - Product Assignment

        [Header("=== PRODUCT ASSIGNMENT ===")]
        [SerializeField, Tooltip("Son kullanýlan ürün geçmiþi boyutu")]
        public int recentProductHistorySize = 3;

        #endregion

        #region Serialized Fields - Debug

        [Header("=== DEBUG ===")]
        [SerializeField, Tooltip("Debug loglarýný göster")]
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

        #endregion

        #region Private Fields - Product History

        private readonly Queue<int> _recentProductIndices = new();

        #endregion

        #region Public Properties

        /// <summary>
        /// Mevcut kuyruk boyutu
        /// </summary>
        public int QueueSize => _customerQueue.Count;

        /// <summary>
        /// Bugün toplam müþteri sayýsý
        /// </summary>
        public int TodaysTotalCustomers => _todaysTotalCustomers;

        /// <summary>
        /// Bugün spawn olan müþteri sayýsý
        /// </summary>
        public int CustomersSpawnedToday => _customersSpawnedToday;

        /// <summary>
        /// Kalan müþteri sayýsý
        /// </summary>
        public int CustomersRemainingToday => _customersRemainingToday;

        /// <summary>
        /// Müþteri spawn edilebilir mi?
        /// </summary>
        public bool CanSpawnCustomers => IsWithinSpawningHours();

        /// <summary>
        /// Kuyruk dolu mu?
        /// </summary>
        public bool IsQueueFull => _customerQueue.Count >= maxQueueSize ||
                                    _customerQueue.Count >= queuePositions.Length;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            if (IsServer)
            {
                SubscribeToDayCycleEvents();
                InitializeDailyCustomers();
            }
        }

        private void OnDestroy()
        {
            if (IsServer)
            {
                UnsubscribeFromDayCycleEvents();
            }
        }

        private void Update()
        {
            if (!IsServer) return;

            EnsureDayInitialized();
            TrySpawnScheduledCustomer();
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

            CalculateSpawnSchedule();
            UpdateRemainingCustomersUI();

            LogDebug($"Day {currentDay} - Total customers scheduled: {_todaysTotalCustomers}");
            LogDebug($"Spawn times calculated between {spawnStartHour:F1} and {spawnEndHour:F1}");
        }

        private int CalculateTodaysCustomerCount(int currentDay)
        {
            return baseCustomersPerDay + ((currentDay - 1) * customerIncreasePerDay);
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
            return Mathf.Clamp(baseSpawnTime + randomOffset, spawnStartHour, spawnEndHour);
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
        /// Müþteri iþlemi tamamlandýðýnda çaðrýlýr
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
        /// Müþteri kuyruðun baþýnda mý kontrol eder
        /// </summary>
        public bool IsFirstInQueue(CustomerAI ai)
        {
            return _customerQueue.Count > 0 && _customerQueue[0] == ai;
        }

        /// <summary>
        /// Müþteriye masa atar
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
                remainingCustomersText.text = $"{_customersRemainingToday}";
            }
        }

        [ClientRpc]
        private void UpdateRemainingCustomersClientRpc(int remainingCount)
        {
            if (remainingCustomersText != null)
            {
                remainingCustomersText.text = $"{remainingCount}";
            }
        }

        #endregion

        #region Product Assignment

        /// <summary>
        /// Son kullanýlan ürünleri hariç tutarak rastgele ürün index'i döndürür
        /// </summary>
        public int GetRandomProductIndexExcludingRecent(int productCount)
        {
            if (productCount <= 0) return -1;
            if (productCount == 1) return 0;

            var candidates = BuildProductCandidates(productCount);
            int chosen = SelectProductIndex(candidates, productCount);
            AddToProductHistory(chosen);

            return chosen;
        }

        private List<int> BuildProductCandidates(int productCount)
        {
            var candidates = new List<int>(productCount);

            for (int i = 0; i < productCount; i++)
            {
                if (!_recentProductIndices.Contains(i))
                {
                    candidates.Add(i);
                }
            }

            return candidates;
        }

        private int SelectProductIndex(List<int> candidates, int productCount)
        {
            if (candidates.Count == 0)
            {
                // All indices in recent history - pick any
                return Random.Range(0, productCount);
            }

            return candidates[Random.Range(0, candidates.Count)];
        }

        private void AddToProductHistory(int productIndex)
        {
            _recentProductIndices.Enqueue(productIndex);

            int maxHistorySize = Mathf.Max(1, recentProductHistorySize);
            while (_recentProductIndices.Count > maxHistorySize)
            {
                _recentProductIndices.Dequeue();
            }
        }

        /// <summary>
        /// Ürün geçmiþini temizler
        /// </summary>
        public void ClearProductHistory()
        {
            _recentProductIndices.Clear();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Müþteri spawn isteði (ServerRpc)
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
        /// Kuyruk boyutunu döndürür
        /// </summary>
        public int GetQueueSize() => QueueSize;

        /// <summary>
        /// Kalan müþteri sayýsýný döndürür
        /// </summary>
        public int GetRemainingCustomers() => _customersRemainingToday;

        /// <summary>
        /// Spawn durumu bilgisini döndürür
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