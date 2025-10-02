using UnityEngine;
using Unity.Netcode;
using System.Collections;

namespace NewCss
{
    public class TruckSpawner : NetworkBehaviour
    {
        public static TruckSpawner Instance;
        private Coroutine respawnCoroutine;

        [Header("Truck Settings")]
        [Tooltip("Truck prefab reference (must have NetworkObject component)")]
        public GameObject truckPrefab;
        [Tooltip("Spawn point for the truck")]
        public Transform spawnPoint;
        [Tooltip("Transform representing the hangar exit point")]
        public Transform exitPoint;
        [Tooltip("Random delay before spawning new truck")]
        public Vector2 respawnDelayRange = new Vector2(3f, 5f);
        [Tooltip("Threshold distance from spawnPoint")]
        public float hangarThreshold = 5f;

        [Header("Working Hours")]
        public int truckStartHour = 8;
        public int truckEndHour = 14;

        // Network Variables
        private NetworkVariable<bool> hasCurrentTruck = new NetworkVariable<bool>(false);
        private NetworkVariable<bool> hasNotifiedEndOfService = new NetworkVariable<bool>(false);
        private NetworkVariable<BoxInfo.BoxType> networkRequestedBoxType = new NetworkVariable<BoxInfo.BoxType>(BoxInfo.BoxType.Red);
        private NetworkVariable<int> networkRequiredCargo = new NetworkVariable<int>(1);
        
        private Truck currentTruckScript;

        void Awake()
        {
            if (Instance == null)
                Instance = this;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                SpawnNewTruck();
            }
        }

        void OnEnable()
        {
            DayCycleManager.OnNewDay += HandleNextDay;
        }

        void OnDisable()
        {
            DayCycleManager.OnNewDay -= HandleNextDay;
        }

        private void HandleNextDay()
        {
            Debug.Log($"TruckSpawner.HandleNextDay called on server: {IsServer}");

            if (!IsServer) return;

            // Destroy current truck
            if (currentTruckScript != null)
            {
                var netObj = currentTruckScript.GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsSpawned)
                    netObj.Despawn();
                currentTruckScript = null;
            }

            // Stop respawn coroutine
            if (respawnCoroutine != null)
            {
                StopCoroutine(respawnCoroutine);
                respawnCoroutine = null;
            }

            // Reset flags
            hasNotifiedEndOfService.Value = false;
            hasCurrentTruck.Value = false;

            // Spawn new truck if within working hours
            if (IsWithinWorkingHours())
            {
                SpawnNewTruck();
            }
        }

        void Update()
        {
            if (!IsServer) return;

            int currentHour = DayCycleManager.Instance.CurrentHour;

            // Outside working hours
            if (currentHour >= truckEndHour)
            {
                if (currentTruckScript != null)
                {
                    currentTruckScript.ForceExitDueToTime();
                    
                    if (!hasNotifiedEndOfService.Value)
                    {
                        hasNotifiedEndOfService.Value = true;
                    }
                }

                if (respawnCoroutine != null)
                {
                    StopCoroutine(respawnCoroutine);
                    respawnCoroutine = null;
                }
                return;
            }

            // Before working hours
            if (currentHour < truckStartHour)
            {
                if (respawnCoroutine != null)
                {
                    StopCoroutine(respawnCoroutine);
                    respawnCoroutine = null;
                }
                return;
            }

            // During working hours
            if (currentTruckScript != null)
            {
                float dist = Vector3.Distance(currentTruckScript.transform.position, spawnPoint.position);
                if (dist < hangarThreshold)
                    return;
            }
            
            if (!hasCurrentTruck.Value && respawnCoroutine == null)
            {
                respawnCoroutine = StartCoroutine(SpawnTruckAfterDelay());
            }
        }

        private bool IsWithinWorkingHours()
        {
            int currentHour = DayCycleManager.Instance.CurrentHour;
            return currentHour >= truckStartHour && currentHour < truckEndHour;
        }

        IEnumerator SpawnTruckAfterDelay()
        {
            float delay = Random.Range(respawnDelayRange.x, respawnDelayRange.y);
            yield return new WaitForSeconds(delay);
            
            if (IsWithinWorkingHours())
            {
                SpawnNewTruck();
            }
            
            respawnCoroutine = null;
        }

        public void SpawnNewTruck()
        {
            if (!IsServer) return;
    
            if (!IsWithinWorkingHours())
                return;

            if (truckPrefab != null && spawnPoint != null)
            {
                // ÖNEMLİ: İlk önce random değerleri generate et
                BoxInfo.BoxType reqType = (BoxInfo.BoxType)Random.Range(0, 3);
                int reqAmount = Random.Range(3, 7);

                GameObject truckObj = Instantiate(truckPrefab, spawnPoint.position, spawnPoint.rotation);
                NetworkObject networkObj = truckObj.GetComponent<NetworkObject>();
        
                if (networkObj != null)
                {
                    // Truck script'ini al
                    currentTruckScript = truckObj.GetComponent<Truck>();
                    
                    // ÖNEMLİ: Spawn ÖNCESI değerleri set et
                    if (currentTruckScript != null)
                    {
                        currentTruckScript.PreInitialize(reqType, reqAmount);
                    }

                    // Network object'i spawn et
                    networkObj.Spawn();
                    
                    hasCurrentTruck.Value = true;

                    if (exitPoint != null)
                        currentTruckScript.exitPoint = exitPoint;

                    // Spawn sonrasında da initialize et (güvenlik için)
                    StartCoroutine(InitializeTruckAfterSpawn(currentTruckScript, reqType, reqAmount));
                }
            }
        }

        private IEnumerator InitializeTruckAfterSpawn(Truck truck, BoxInfo.BoxType reqType, int reqAmount)
        {
            // Bir frame bekle ki spawn işlemi tam olarak tamamlansın
            yield return new WaitForEndOfFrame();
            
            // Initialize'i tekrar çağır (güvenlik için)
            truck.InitializeServerRpc(reqType, reqAmount);
        }

        public void OnTruckDestroyed()
        {
            if (!IsServer) return;
            
            currentTruckScript = null;
            hasCurrentTruck.Value = false;
        }
    }
}