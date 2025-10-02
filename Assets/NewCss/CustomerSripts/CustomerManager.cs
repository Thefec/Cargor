using Unity.Netcode;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using System.Collections;

namespace NewCss
{
    public class CustomerManager : NetworkBehaviour
    {
        [Header("Spawn Settings")]
        public GameObject customerPrefab;
        public Transform spawnPoint;
        public float minSpawnInterval = 1f;
        public float maxSpawnInterval = 3f;
        public int maxQueueSize = 3;

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

        private List<CustomerAI> customerQueue = new List<CustomerAI>();
        private float lastSpawnTime = -Mathf.Infinity;

        void Update()
        {
            if (IsServer && IsWithinSpawningHours())
            {
                TrySpawnCustomer();
            }
        }

        #region Spawning Logic
        private bool IsWithinSpawningHours()
        {
            if (DayCycleManager.Instance == null) return false;
            
            float currentTime = DayCycleManager.Instance.CurrentTime;
            return currentTime >= spawnStartHour && currentTime <= spawnEndHour;
        }

        private void TrySpawnCustomer()
        {
            if (customerQueue.Count >= maxQueueSize || customerQueue.Count >= queuePositions.Length)
                return;

            if (Time.time - lastSpawnTime < GetEffectiveSpawnInterval())
                return;

            int nextQueueIndex = GetNextAvailableQueueIndex();
            if (nextQueueIndex != -1)
            {
                SpawnCustomer(nextQueueIndex);
                lastSpawnTime = Time.time;
            }
        }

        private float GetEffectiveSpawnInterval()
        {
            float prestij = PrestigeManager.Instance?.GetPrestige() ?? 0f;
            float bonusOrani = Mathf.Clamp(prestij * 0.005f, 0f, 0.80f);
            float baseInterval = Random.Range(minSpawnInterval, maxSpawnInterval);
            
            return baseInterval * (1f - bonusOrani);
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
                AdvanceQueue();
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

        public string GetSpawningStatusInfo()
        {
            if (DayCycleManager.Instance == null)
                return "DayCycleManager not found";

            float currentTime = DayCycleManager.Instance.CurrentTime;
            bool canSpawn = IsWithinSpawningHours();

            return $"Current Time: {currentTime:F2}, Spawn Window: {spawnStartHour:F1}-{spawnEndHour:F1}, Can Spawn: {canSpawn}";
        }
        #endregion

        #region Debug Tools
        [ContextMenu("Spawn Test Customer")]
        private void SpawnTestCustomer()
        {
            if (!IsServer) return;

            if (!IsWithinSpawningHours())
                return;

            int availableIndex = GetNextAvailableQueueIndex();
            if (availableIndex != -1)
                SpawnCustomer(availableIndex);
        }

        [ContextMenu("Show Spawning Status")]
        private void ShowSpawningStatus()
        {
            Debug.Log(GetSpawningStatusInfo());
        }
        #endregion
    }
}