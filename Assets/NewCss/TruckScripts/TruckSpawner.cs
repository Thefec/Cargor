using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace NewCss
{
    [System.Serializable]
    public class HangarSpawnPoint
    {
        public Transform spawnPoint;
        public Transform exitPoint;
        public int requiredUpgradeLevel; // Bu hangar için gereken upgrade seviyesi
        [HideInInspector] public bool isActive = false;
    }

    public class TruckSpawner : NetworkBehaviour
    {
        public static TruckSpawner Instance;
        private Dictionary<int, Coroutine> hangarRespawnCoroutines = new Dictionary<int, Coroutine>();

        [Header("Truck Settings")]
        [Tooltip("Truck prefab reference (must have NetworkObject component)")]
        public GameObject truckPrefab;

        [Header("Hangar Spawn Points")]
        [Tooltip("Tüm hangar spawn noktaları (Level 0, 1, 2...)")]
        public List<HangarSpawnPoint> hangarSpawnPoints = new List<HangarSpawnPoint>();

        [Tooltip("Random delay before spawning new truck")]
        public Vector2 respawnDelayRange = new Vector2(3f, 5f);
        [Tooltip("Threshold distance from spawnPoint")]
        public float hangarThreshold = 5f;

        [Header("Working Hours")]
        public int truckStartHour = 8;
        public int truckEndHour = 14;

        // Network Variables
        private NetworkVariable<bool> hasNotifiedEndOfService = new NetworkVariable<bool>(false);

        // Her hangar için ayrı kamyon takibi
        private Dictionary<int, Truck> activeTrucks = new Dictionary<int, Truck>();
        private NetworkVariable<int> currentTruckUpgradeLevel = new NetworkVariable<int>(0);

        void Awake()
        {
            if (Instance == null)
                Instance = this;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                // İlk başta sadece Level 0 hangarı aktif et
                UpdateActiveHangars(0);
                SpawnTrucksForActiveHangars();
            }

            // Client tarafında da upgrade level değişikliklerini dinle
            currentTruckUpgradeLevel.OnValueChanged += OnTruckUpgradeLevelChanged;
        }

        public override void OnNetworkDespawn()
        {
            currentTruckUpgradeLevel.OnValueChanged -= OnTruckUpgradeLevelChanged;
        }

        private void OnTruckUpgradeLevelChanged(int previousValue, int newValue)
        {
            if (IsServer)
            {
                UpdateActiveHangars(newValue);
                SpawnTrucksForActiveHangars();
            }
        }

        // UpgradePanel'den çağrılacak metod
        public void SetTruckUpgradeLevel(int level)
        {
            if (IsServer)
            {
                currentTruckUpgradeLevel.Value = level;
            }
        }

        private void UpdateActiveHangars(int upgradeLevel)
        {
            Debug.Log($"UpdateActiveHangars called with level: {upgradeLevel}");

            foreach (var hangar in hangarSpawnPoints)
            {
                bool shouldBeActive = hangar.requiredUpgradeLevel <= upgradeLevel;

                if (hangar.isActive != shouldBeActive)
                {
                    hangar.isActive = shouldBeActive;
                    Debug.Log($"Hangar at level {hangar.requiredUpgradeLevel} set to active: {shouldBeActive}");
                }
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

            // Tüm kamyonları yok et
            foreach (var kvp in activeTrucks.ToList())
            {
                if (kvp.Value != null)
                {
                    var netObj = kvp.Value.GetComponent<NetworkObject>();
                    if (netObj != null && netObj.IsSpawned)
                        netObj.Despawn();
                }
            }
            activeTrucks.Clear();

            // Tüm coroutine'leri durdur
            foreach (var kvp in hangarRespawnCoroutines.ToList())
            {
                if (kvp.Value != null)
                    StopCoroutine(kvp.Value);
            }
            hangarRespawnCoroutines.Clear();

            // Reset flags
            hasNotifiedEndOfService.Value = false;

            // Aktif hangarlar için yeni kamyonlar spawn et
            if (IsWithinWorkingHours())
            {
                SpawnTrucksForActiveHangars();
            }
        }

        void Update()
        {
            if (!IsServer) return;

            int currentHour = DayCycleManager.Instance.CurrentHour;

            // Çalışma saatleri dışında
            if (currentHour >= truckEndHour)
            {
                // Tüm kamyonları zorla çıkar
                foreach (var kvp in activeTrucks.ToList())
                {
                    if (kvp.Value != null)
                    {
                        kvp.Value.ForceExitDueToTime();
                    }
                }

                if (!hasNotifiedEndOfService.Value)
                {
                    hasNotifiedEndOfService.Value = true;
                }

                // Tüm coroutine'leri durdur
                foreach (var kvp in hangarRespawnCoroutines.ToList())
                {
                    if (kvp.Value != null)
                    {
                        StopCoroutine(kvp.Value);
                        hangarRespawnCoroutines.Remove(kvp.Key);
                    }
                }
                return;
            }

            // Çalışma saatleri öncesi
            if (currentHour < truckStartHour)
            {
                foreach (var kvp in hangarRespawnCoroutines.ToList())
                {
                    if (kvp.Value != null)
                    {
                        StopCoroutine(kvp.Value);
                        hangarRespawnCoroutines.Remove(kvp.Key);
                    }
                }
                return;
            }

            // Çalışma saatleri içinde - her hangar için kontrol
            for (int i = 0; i < hangarSpawnPoints.Count; i++)
            {
                var hangar = hangarSpawnPoints[i];
                if (!hangar.isActive) continue;

                // Bu hangar için kamyon var mı ve hala hangar içinde mi kontrol et
                if (activeTrucks.ContainsKey(i) && activeTrucks[i] != null)
                {
                    float dist = Vector3.Distance(activeTrucks[i].transform.position, hangar.spawnPoint.position);
                    if (dist < hangarThreshold)
                        continue; // Hala hangar içinde, yeni spawn etme
                }

                // Bu hangar için kamyon yok ve respawn coroutine'i de çalışmıyorsa
                if ((!activeTrucks.ContainsKey(i) || activeTrucks[i] == null) &&
                    !hangarRespawnCoroutines.ContainsKey(i))
                {
                    hangarRespawnCoroutines[i] = StartCoroutine(SpawnTruckAfterDelay(i));
                }
            }
        }

        private bool IsWithinWorkingHours()
        {
            int currentHour = DayCycleManager.Instance.CurrentHour;
            return currentHour >= truckStartHour && currentHour < truckEndHour;
        }

        IEnumerator SpawnTruckAfterDelay(int hangarIndex)
        {
            float delay = Random.Range(respawnDelayRange.x, respawnDelayRange.y);
            yield return new WaitForSeconds(delay);

            if (IsWithinWorkingHours() && hangarIndex < hangarSpawnPoints.Count)
            {
                var hangar = hangarSpawnPoints[hangarIndex];
                if (hangar.isActive)
                {
                    SpawnTruckAtHangar(hangarIndex);
                }
            }

            hangarRespawnCoroutines.Remove(hangarIndex);
        }

        private void SpawnTrucksForActiveHangars()
        {
            if (!IsServer) return;
            if (!IsWithinWorkingHours()) return;

            for (int i = 0; i < hangarSpawnPoints.Count; i++)
            {
                var hangar = hangarSpawnPoints[i];
                if (hangar.isActive && (!activeTrucks.ContainsKey(i) || activeTrucks[i] == null))
                {
                    SpawnTruckAtHangar(i);
                }
            }
        }

        private void SpawnTruckAtHangar(int hangarIndex)
        {
            if (!IsServer) return;
            if (hangarIndex >= hangarSpawnPoints.Count) return;

            var hangar = hangarSpawnPoints[hangarIndex];
            if (!hangar.isActive) return;

            if (truckPrefab != null && hangar.spawnPoint != null)
            {
                Debug.Log($"Spawning truck at hangar {hangarIndex} (Level {hangar.requiredUpgradeLevel})");

                // Random değerleri oluştur
                BoxInfo.BoxType reqType = (BoxInfo.BoxType)Random.Range(0, 3);
                int reqAmount = Random.Range(3, 7);

                GameObject truckObj = Instantiate(truckPrefab, hangar.spawnPoint.position, hangar.spawnPoint.rotation);
                NetworkObject networkObj = truckObj.GetComponent<NetworkObject>();

                if (networkObj != null)
                {
                    Truck truckScript = truckObj.GetComponent<Truck>();

                    // Spawn öncesi initialize
                    if (truckScript != null)
                    {
                        truckScript.PreInitialize(reqType, reqAmount);
                        truckScript.hangarIndex = hangarIndex; // Kamyonun hangi hangardan geldiğini kaydet
                    }

                    // Network spawn
                    networkObj.Spawn();

                    // Kamyonu listeye ekle
                    activeTrucks[hangarIndex] = truckScript;

                    // Exit point'i set et
                    if (hangar.exitPoint != null)
                        truckScript.exitPoint = hangar.exitPoint;

                    // Spawn sonrası initialize
                    StartCoroutine(InitializeTruckAfterSpawn(truckScript, reqType, reqAmount));
                }
            }
        }

        private IEnumerator InitializeTruckAfterSpawn(Truck truck, BoxInfo.BoxType reqType, int reqAmount)
        {
            yield return new WaitForEndOfFrame();
            truck.InitializeServerRpc(reqType, reqAmount);
        }

        public void OnTruckDestroyed(int hangarIndex)
        {
            if (!IsServer) return;

            if (activeTrucks.ContainsKey(hangarIndex))
            {
                activeTrucks.Remove(hangarIndex);
                Debug.Log($"Truck destroyed at hangar {hangarIndex}");
            }
        }

        // Debug metodu
        [ContextMenu("Log Active Hangars")]
        private void LogActiveHangars()
        {
            Debug.Log($"Current Truck Upgrade Level: {currentTruckUpgradeLevel.Value}");
            for (int i = 0; i < hangarSpawnPoints.Count; i++)
            {
                var hangar = hangarSpawnPoints[i];
                Debug.Log($"Hangar {i}: Required Level = {hangar.requiredUpgradeLevel}, IsActive = {hangar.isActive}, Has Truck = {activeTrucks.ContainsKey(i)}");
            }
        }
    }
}