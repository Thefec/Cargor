using Unity.Netcode;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using TMPro; // TextMeshPro için

namespace NewCss
{
    public class CustomerManager : NetworkBehaviour
    {
        [Header("Spawn Settings")]
        public GameObject customerPrefab;
        public Transform spawnPoint;
        public int maxQueueSize = 3;

        [Header("Daily Customer Settings")]
        [Tooltip("Ýlk gün kaç müþteri gelecek")]
        public int baseCustomersPerDay = 10;
        [Tooltip("Her geçen gün müþteri sayýsý ne kadar artacak")]
        public int customerIncreasePerDay = 2;
        [Tooltip("Spawn zamanlarýna rastgelelik ekle (0-1 arasý, 0 = tam eþit daðýlým)")]
        [Range(0f, 1f)]
        public float spawnTimeRandomness = 0.2f;

        [Header("Queue & Tables")]
        public Transform[] queuePositions;
        public Transform exitPoint;
        public DisplayTable dropOffTable;
        public DisplayTable[] serviceTables;

        [Header("Time Settings")]
        [Tooltip("Hour when customers start spawning (24-hour format)")]
        public float spawnStartHour = 8f;
        [Tooltip("Hour when customers stop spawning (24-hour format)")]
        public float spawnEndHour = 14f;

        [Header("UI Settings")]
        [Tooltip("Kalan müþteri sayýsýný gösterecek TMP Text")]
        public TextMeshProUGUI remainingCustomersText;

        [Header("Product Assignment Settings")]
        [Tooltip("How many recently used product indices to remember and avoid repeating")]
        public int recentProductHistorySize = 3;

        private List<CustomerAI> customerQueue = new List<CustomerAI>();

        // Daily spawn tracking
        private int todaysTotalCustomers;
        private int customersSpawnedToday;
        private int customersRemainingToday; // YENI: Kalan müþteri sayýsý
        private List<float> scheduledSpawnTimes = new List<float>();
        private int nextScheduledIndex = 0;
        private bool dayInitialized = false;

        // Recent products history (used to avoid giving same product repeatedly)
        private Queue<int> recentProductIndices = new Queue<int>();

        void Start()
        {
            if (IsServer)
            {
                // DayCycleManager event'ini dinle
                DayCycleManager.OnNewDay += OnNewDay;

                // Ýlk günü baþlat
                InitializeDailyCustomers();
            }
        }

        void OnDestroy()
        {
            if (IsServer)
            {
                DayCycleManager.OnNewDay -= OnNewDay;
            }
        }

        void Update()
        {
            if (!IsServer) return;

            // Gün henüz baþlamadýysa init et
            if (!dayInitialized && DayCycleManager.Instance != null)
            {
                InitializeDailyCustomers();
            }

            if (IsWithinSpawningHours())
            {
                TrySpawnScheduledCustomer();
            }
        }

        #region Daily Customer Management
        private void OnNewDay()
        {
            if (IsServer)
            {
                Debug.Log("New day started - reinitializing customers");
                InitializeDailyCustomers();
            }
        }

        private void InitializeDailyCustomers()
        {
            if (DayCycleManager.Instance == null)
            {
                Debug.LogWarning("DayCycleManager not found, cannot initialize daily customers");
                return;
            }

            int currentDay = DayCycleManager.Instance.currentDay;

            // Bugün toplam kaç müþteri gelecek
            todaysTotalCustomers = baseCustomersPerDay + ((currentDay - 1) * customerIncreasePerDay);
            customersSpawnedToday = 0;
            customersRemainingToday = todaysTotalCustomers; // YENI: Baþlangýçta tüm müþteriler kalan
            nextScheduledIndex = 0;
            dayInitialized = true;

            // Spawn zamanlarýný hesapla
            CalculateSpawnSchedule();

            // UI'ý güncelle
            UpdateRemainingCustomersUI();

            Debug.Log($"Day {currentDay} - Total customers scheduled: {todaysTotalCustomers}");
            Debug.Log($"Spawn times calculated between {spawnStartHour:F1} and {spawnEndHour:F1}");
        }

        private void CalculateSpawnSchedule()
        {
            scheduledSpawnTimes.Clear();

            if (todaysTotalCustomers <= 0)
            {
                Debug.LogWarning("No customers scheduled for today");
                return;
            }

            float spawnWindow = spawnEndHour - spawnStartHour;
            float baseInterval = spawnWindow / todaysTotalCustomers;

            for (int i = 0; i < todaysTotalCustomers; i++)
            {
                // Temel spawn zamaný
                float baseSpawnTime = spawnStartHour + (i * baseInterval) + (baseInterval * 0.5f);

                // Rastgelelik ekle (spawn zamanlarýný biraz deðiþtir)
                float randomOffset = Random.Range(-baseInterval * spawnTimeRandomness, baseInterval * spawnTimeRandomness);
                float spawnTime = Mathf.Clamp(baseSpawnTime + randomOffset, spawnStartHour, spawnEndHour);

                scheduledSpawnTimes.Add(spawnTime);
            }

            // Zamanlarý sýrala
            scheduledSpawnTimes.Sort();

            // Debug: Ýlk 5 spawn zamanýný göster
            Debug.Log("First 5 spawn times:");
            for (int i = 0; i < Mathf.Min(5, scheduledSpawnTimes.Count); i++)
            {
                Debug.Log($"  Customer {i + 1}: {scheduledSpawnTimes[i]:F2}");
            }
        }

        private void TrySpawnScheduledCustomer()
        {
            if (DayCycleManager.Instance == null) return;

            // Tüm müþteriler spawn olduysa
            if (nextScheduledIndex >= scheduledSpawnTimes.Count)
                return;

            // Kuyruk doluysa bekle
            if (customerQueue.Count >= maxQueueSize || customerQueue.Count >= queuePositions.Length)
                return;

            float currentTime = DayCycleManager.Instance.CurrentTime;

            // Þimdiki zaman, bir sonraki scheduled spawn zamanýný geçtiyse spawn et
            if (currentTime >= scheduledSpawnTimes[nextScheduledIndex])
            {
                int nextQueueIndex = GetNextAvailableQueueIndex();
                if (nextQueueIndex != -1)
                {
                    SpawnCustomer(nextQueueIndex);
                    customersSpawnedToday++;
                    nextScheduledIndex++;

                    Debug.Log($"Customer {customersSpawnedToday}/{todaysTotalCustomers} spawned at time {currentTime:F2}");
                }
            }
        }
        #endregion

        #region Spawning Logic
        private bool IsWithinSpawningHours()
        {
            if (DayCycleManager.Instance == null) return false;

            float currentTime = DayCycleManager.Instance.CurrentTime;
            return currentTime >= spawnStartHour && currentTime <= spawnEndHour;
        }

        private int GetNextAvailableQueueIndex()
        {
            for (int i = 0; i < Mathf.Min(queuePositions.Length, maxQueueSize); i++)
            {
                if (!IsQueueSlotOccupied(i))
                    return i;
            }
            return -1;
        }

        private bool IsQueueSlotOccupied(int index)
        {
            foreach (var customer in customerQueue)
            {
                if (customer.GetTargetQueueIndex() == index)
                    return true;
            }
            return false;
        }

        private void SpawnCustomer(int queueIndex)
        {
            var customerObject = Instantiate(customerPrefab, spawnPoint.position, Quaternion.identity);
            var networkObject = customerObject.GetComponent<NetworkObject>();
            networkObject.Spawn();

            var customerAI = customerObject.GetComponent<CustomerAI>();
            if (customerAI == null)
            {
                Destroy(customerObject);
                return;
            }

            SetupCustomerAI(customerAI, queueIndex);
            SetupCustomerClientRpc(networkObject.NetworkObjectId, queueIndex);

            customerQueue.Add(customerAI);
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
            // Navigation
            var navAgent = ai.GetComponent<NavMeshAgent>();
            if (navAgent != null) navAgent.enabled = true;

            // UI
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

            // Animation & Physics
            var animator = ai.GetComponent<Animator>();
            if (animator != null) animator.enabled = true;

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
            StartCoroutine(SetupCustomerOnClient(networkObjectId, queueIndex));
        }

        private IEnumerator SetupCustomerOnClient(ulong networkObjectId, int queueIndex)
        {
            yield return null; // Wait for NetworkObject registration

            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out var netObj))
            {
                var ai = netObj.GetComponent<CustomerAI>();
                if (ai != null)
                {
                    SetupCustomerAI(ai, queueIndex);
                }
            }
        }
        #endregion

        #region Queue Management
        public void NotifyCustomerDone(CustomerAI customer)
        {
            if (customerQueue.Remove(customer))
            {
                // YENI: Müþteri çýktýðýnda kalan sayýyý azalt ve UI'ý güncelle
                customersRemainingToday--;
                UpdateRemainingCustomersUI();
                UpdateRemainingCustomersClientRpc(customersRemainingToday);

                AdvanceQueue();

                Debug.Log($"Customer left. Remaining today: {customersRemainingToday}");
            }
        }

        private void AdvanceQueue()
        {
            customerQueue.Sort((a, b) => a.GetTargetQueueIndex().CompareTo(b.GetTargetQueueIndex()));

            for (int i = 0; i < customerQueue.Count; i++)
            {
                var customer = customerQueue[i];
                if (!customer.isPrefabMode && customer.GetTargetQueueIndex() != i)
                {
                    customer.SetQueueTarget(queuePositions[i].position, i);
                }
            }
        }

        public bool IsFirstInQueue(CustomerAI ai)
        {
            return customerQueue.Count > 0 && customerQueue[0] == ai;
        }

        public void AssignDropOffTable(CustomerAI customer, DisplayTable table)
        {
            if (customer != null && table != null)
            {
                customer.dropOffTable = table;
            }
        }
        #endregion

        #region UI Management
        // YENI: Server'da UI'ý güncelle
        private void UpdateRemainingCustomersUI()
        {
            if (remainingCustomersText != null)
            {
                remainingCustomersText.text = $"{customersRemainingToday}";
            }
        }

        // YENI: Tüm client'larda UI'ý güncelle
        [ClientRpc]
        private void UpdateRemainingCustomersClientRpc(int remainingCount)
        {
            if (remainingCustomersText != null)
            {
                remainingCustomersText.text = $" {remainingCount}";
            }
        }
        #endregion

        #region External Interface
        [ServerRpc(RequireOwnership = false)]
        public void RequestCustomerSpawnServerRpc()
        {
            if (!IsWithinSpawningHours())
                return;

            int availableIndex = GetNextAvailableQueueIndex();
            if (availableIndex != -1)
                SpawnCustomer(availableIndex);
        }

        public int GetQueueSize() => customerQueue.Count;

        public bool CanSpawnCustomers() => IsWithinSpawningHours();

        // YENI: Kalan müþteri sayýsýný döndür
        public int GetRemainingCustomers() => customersRemainingToday;

        /// <summary>
        /// Returns a random product index in range [0, productCount). Attempts to avoid indices
        /// included in the recent-product history. The chosen index will be added to history.
        /// </summary>
        public int GetRandomProductIndexExcludingRecent(int productCount)
        {
            if (productCount <= 0) return -1;
            if (productCount == 1) return 0;

            // Build candidate list excluding recent indices
            List<int> candidates = new List<int>(productCount);
            for (int i = 0; i < productCount; i++)
            {
                if (!recentProductIndices.Contains(i))
                    candidates.Add(i);
            }

            int chosen;
            if (candidates.Count == 0)
            {
                // All indices are in recent history -> pick any index
                chosen = Random.Range(0, productCount);
            }
            else
            {
                chosen = candidates[Random.Range(0, candidates.Count)];
            }

            // Add to history queue
            recentProductIndices.Enqueue(chosen);
            while (recentProductIndices.Count > Mathf.Max(1, recentProductHistorySize))
            {
                recentProductIndices.Dequeue();
            }

            return chosen;
        }

        public string GetSpawningStatusInfo()
        {
            if (DayCycleManager.Instance == null)
                return "DayCycleManager not found";

            float currentTime = DayCycleManager.Instance.CurrentTime;
            bool canSpawn = IsWithinSpawningHours();

            return $"Day {DayCycleManager.Instance.currentDay}\n" +
                   $"Current Time: {currentTime:F2}\n" +
                   $"Spawn Window: {spawnStartHour:F1}-{spawnEndHour:F1}\n" +
                   $"Customers Today: {customersSpawnedToday}/{todaysTotalCustomers}\n" +
                   $"Remaining: {customersRemainingToday}\n" +
                   $"Can Spawn: {canSpawn}";
        }
        #endregion

        #region Debug Tools
        [ContextMenu("Show Daily Customer Info")]
        private void ShowDailyCustomerInfo()
        {
            Debug.Log(GetSpawningStatusInfo());

            if (scheduledSpawnTimes.Count > 0)
            {
                Debug.Log("\nScheduled spawn times:");
                for (int i = 0; i < scheduledSpawnTimes.Count; i++)
                {
                    string spawned = i < nextScheduledIndex ? "[SPAWNED]" : "[PENDING]";
                    Debug.Log($"  Customer {i + 1}: {scheduledSpawnTimes[i]:F2} {spawned}");
                }
            }
        }

        [ContextMenu("Force Spawn Next Customer")]
        private void ForceSpawnNextCustomer()
        {
            if (!IsServer) return;

            int availableIndex = GetNextAvailableQueueIndex();
            if (availableIndex != -1)
            {
                SpawnCustomer(availableIndex);
                customersSpawnedToday++;
                if (nextScheduledIndex < scheduledSpawnTimes.Count)
                    nextScheduledIndex++;
            }
        }

        [ContextMenu("Reset Daily Customers")]
        private void ResetDailyCustomers()
        {
            if (!IsServer) return;
            InitializeDailyCustomers();
        }
        #endregion
    }
}