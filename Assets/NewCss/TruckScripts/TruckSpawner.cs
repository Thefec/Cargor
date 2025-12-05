using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace NewCss
{
    /// <summary>
    /// Hangar spawn noktası bilgileri
    /// </summary>
    [System.Serializable]
    public class HangarSpawnPoint
    {
        [Tooltip("Spawn noktası transform'u")]
        public Transform spawnPoint;

        [Tooltip("Çıkış noktası transform'u")]
        public Transform exitPoint;

        [Tooltip("Bu hangar için gereken upgrade seviyesi")]
        public int requiredUpgradeLevel;

        [HideInInspector]
        public bool isActive;
    }

    /// <summary>
    /// Kamyon spawn yöneticisi - hangar bazlı kamyon oluşturma ve yönetimini sağlar. 
    /// Upgrade sistemi entegrasyonu ve çalışma saatleri kontrolü içerir.
    /// </summary>
    public class TruckSpawner : NetworkBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[TruckSpawner]";
        private const int MIN_CARGO_AMOUNT = 3;
        private const int MAX_CARGO_AMOUNT = 7;
        private const int BOX_TYPE_COUNT = 3;

        #endregion

        #region Singleton

        public static TruckSpawner Instance { get; private set; }

        #endregion

        #region Serialized Fields

        [Header("=== TRUCK SETTINGS ===")]
        [SerializeField, Tooltip("Kamyon prefab'ı (NetworkObject gerekli)")]
        public GameObject truckPrefab;

        [Header("=== HANGAR SPAWN POINTS ===")]
        [SerializeField, Tooltip("Tüm hangar spawn noktaları")]
        public List<HangarSpawnPoint> hangarSpawnPoints = new();

        [SerializeField, Tooltip("Yeni kamyon spawn gecikmesi aralığı")]
        public Vector2 respawnDelayRange = new Vector2(3f, 5f);

        [SerializeField, Tooltip("Hangar içi mesafe eşiği")]
        public float hangarThreshold = 5f;

        [Header("=== WORKING HOURS ===")]
        [SerializeField, Tooltip("Kamyon başlangıç saati")]
        public int truckStartHour = 8;

        [SerializeField, Tooltip("Kamyon bitiş saati")]
        public int truckEndHour = 17;

        #endregion

        #region Network Variables

        private readonly NetworkVariable<bool> _hasNotifiedEndOfService = new(false);
        private readonly NetworkVariable<int> _currentTruckUpgradeLevel = new(0);

        #endregion

        #region Private Fields

        private readonly Dictionary<int, Truck> _activeTrucks = new();
        private readonly Dictionary<int, Coroutine> _hangarRespawnCoroutines = new();

        #endregion

        #region Public Properties

        /// <summary>
        /// Mevcut truck upgrade seviyesi
        /// </summary>
        public int CurrentUpgradeLevel => _currentTruckUpgradeLevel.Value;

        /// <summary>
        /// Aktif kamyon sayısı
        /// </summary>
        public int ActiveTruckCount => _activeTrucks.Count(kvp => kvp.Value != null);

        /// <summary>
        /// Çalışma saatleri içinde mi? 
        /// </summary>
        public bool IsWithinWorkingHours => CheckWorkingHours();

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeSingleton();
        }

        private void OnEnable()
        {
            SubscribeToDayCycleEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromDayCycleEvents();
        }

        private void Update()
        {
            if (!IsServer) return;

            ProcessServerUpdate();
        }

        #endregion

        #region Network Lifecycle

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            SubscribeToNetworkEvents();

            if (IsServer)
            {
                InitializeHangars();
            }
        }

        public override void OnNetworkDespawn()
        {
            UnsubscribeFromNetworkEvents();
            base.OnNetworkDespawn();
        }

        #endregion

        #region Initialization

        private void InitializeSingleton()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void InitializeHangars()
        {
            UpdateActiveHangars(0);
            SpawnTrucksForActiveHangars();
        }

        #endregion

        #region Event Subscriptions

        private void SubscribeToNetworkEvents()
        {
            _currentTruckUpgradeLevel.OnValueChanged += HandleTruckUpgradeLevelChanged;
        }

        private void UnsubscribeFromNetworkEvents()
        {
            _currentTruckUpgradeLevel.OnValueChanged -= HandleTruckUpgradeLevelChanged;
        }

        private void SubscribeToDayCycleEvents()
        {
            DayCycleManager.OnNewDay += HandleNextDay;
        }

        private void UnsubscribeFromDayCycleEvents()
        {
            DayCycleManager.OnNewDay -= HandleNextDay;
        }

        #endregion

        #region Network Event Handlers

        private void HandleTruckUpgradeLevelChanged(int previousValue, int newValue)
        {
            if (!IsServer) return;

            LogDebug($"Upgrade level changed: {previousValue} -> {newValue}");
            UpdateActiveHangars(newValue);
            SpawnTrucksForActiveHangars();
        }

        #endregion

        #region Day Cycle Handler

        private void HandleNextDay()
        {
            LogDebug($"HandleNextDay called on server: {IsServer}");

            if (!IsServer) return;

            DespawnAllTrucks();
            StopAllRespawnCoroutines();
            ResetDayFlags();

            if (IsWithinWorkingHours)
            {
                SpawnTrucksForActiveHangars();
            }
        }

        private void DespawnAllTrucks()
        {
            foreach (var kvp in _activeTrucks.ToList())
            {
                if (kvp.Value == null) continue;

                var netObj = kvp.Value.GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsSpawned)
                {
                    netObj.Despawn();
                }
            }
            _activeTrucks.Clear();
        }

        private void StopAllRespawnCoroutines()
        {
            foreach (var kvp in _hangarRespawnCoroutines.ToList())
            {
                if (kvp.Value != null)
                {
                    StopCoroutine(kvp.Value);
                }
            }
            _hangarRespawnCoroutines.Clear();
        }

        private void ResetDayFlags()
        {
            _hasNotifiedEndOfService.Value = false;
        }

        #endregion

        #region Server Update

        private void ProcessServerUpdate()
        {
            int currentHour = GetCurrentHour();

            if (currentHour >= truckEndHour)
            {
                HandleAfterWorkingHours();
                return;
            }

            if (currentHour < truckStartHour)
            {
                HandleBeforeWorkingHours();
                return;
            }

            HandleDuringWorkingHours();
        }

        private void HandleAfterWorkingHours()
        {
            ForceExitAllTrucks();
            NotifyEndOfServiceIfNeeded();
            StopAllRespawnCoroutines();
        }

        private void HandleBeforeWorkingHours()
        {
            StopAllRespawnCoroutines();
        }

        private void HandleDuringWorkingHours()
        {
            for (int i = 0; i < hangarSpawnPoints.Count; i++)
            {
                ProcessHangar(i);
            }
        }

        private void ProcessHangar(int hangarIndex)
        {
            var hangar = hangarSpawnPoints[hangarIndex];

            if (!hangar.isActive) return;

            // Kamyon hangar içinde mi kontrol et
            if (IsTruckAtHangar(hangarIndex))
            {
                return;
            }

            // Kamyon yok ve respawn coroutine çalışmıyorsa
            if (!HasActiveTruck(hangarIndex) && !HasPendingRespawn(hangarIndex))
            {
                QueueRespawn(hangarIndex);
            }
        }

        private bool IsTruckAtHangar(int hangarIndex)
        {
            if (!_activeTrucks.TryGetValue(hangarIndex, out Truck truck) || truck == null)
            {
                return false;
            }

            float distance = Vector3.Distance(
                truck.transform.position,
                hangarSpawnPoints[hangarIndex].spawnPoint.position
            );

            return distance < hangarThreshold;
        }

        private bool HasActiveTruck(int hangarIndex)
        {
            return _activeTrucks.TryGetValue(hangarIndex, out Truck truck) && truck != null;
        }

        private bool HasPendingRespawn(int hangarIndex)
        {
            return _hangarRespawnCoroutines.ContainsKey(hangarIndex);
        }

        #endregion

        #region Truck Exit Management

        private void ForceExitAllTrucks()
        {
            foreach (var kvp in _activeTrucks.ToList())
            {
                kvp.Value?.ForceExitDueToTime();
            }
        }

        private void NotifyEndOfServiceIfNeeded()
        {
            if (!_hasNotifiedEndOfService.Value)
            {
                _hasNotifiedEndOfService.Value = true;
            }
        }

        #endregion

        #region Hangar Management

        private void UpdateActiveHangars(int upgradeLevel)
        {
            LogDebug($"UpdateActiveHangars called with level: {upgradeLevel}");

            foreach (var hangar in hangarSpawnPoints)
            {
                bool shouldBeActive = hangar.requiredUpgradeLevel <= upgradeLevel;

                if (hangar.isActive != shouldBeActive)
                {
                    hangar.isActive = shouldBeActive;
                    LogDebug($"Hangar at level {hangar.requiredUpgradeLevel} set to active: {shouldBeActive}");
                }
            }
        }

        #endregion

        #region Truck Spawning

        private void SpawnTrucksForActiveHangars()
        {
            if (!IsServer) return;
            if (!IsWithinWorkingHours) return;

            for (int i = 0; i < hangarSpawnPoints.Count; i++)
            {
                var hangar = hangarSpawnPoints[i];

                if (hangar.isActive && !HasActiveTruck(i))
                {
                    SpawnTruckAtHangar(i);
                }
            }
        }

        private void QueueRespawn(int hangarIndex)
        {
            _hangarRespawnCoroutines[hangarIndex] = StartCoroutine(SpawnTruckAfterDelayCoroutine(hangarIndex));
        }

        private IEnumerator SpawnTruckAfterDelayCoroutine(int hangarIndex)
        {
            float delay = Random.Range(respawnDelayRange.x, respawnDelayRange.y);
            yield return new WaitForSeconds(delay);

            if (IsWithinWorkingHours && hangarIndex < hangarSpawnPoints.Count)
            {
                var hangar = hangarSpawnPoints[hangarIndex];
                if (hangar.isActive)
                {
                    SpawnTruckAtHangar(hangarIndex);
                }
            }

            _hangarRespawnCoroutines.Remove(hangarIndex);
        }

        private void SpawnTruckAtHangar(int hangarIndex)
        {
            if (!IsServer) return;
            if (hangarIndex >= hangarSpawnPoints.Count) return;

            var hangar = hangarSpawnPoints[hangarIndex];
            if (!hangar.isActive) return;
            if (truckPrefab == null || hangar.spawnPoint == null) return;

            LogDebug($"Spawning truck at hangar {hangarIndex} (Level {hangar.requiredUpgradeLevel})");

            // Generate random values
            var truckData = GenerateRandomTruckData();

            // Instantiate and spawn
            GameObject truckObj = Instantiate(truckPrefab, hangar.spawnPoint.position, hangar.spawnPoint.rotation);
            NetworkObject networkObj = truckObj.GetComponent<NetworkObject>();

            if (networkObj == null)
            {
                LogError("Truck prefab has no NetworkObject component!");
                Destroy(truckObj);
                return;
            }

            Truck truckScript = truckObj.GetComponent<Truck>();

            // Pre-initialize
            truckScript?.PreInitialize(truckData.boxType, truckData.cargoAmount);
            if (truckScript != null)
            {
                truckScript.hangarIndex = hangarIndex;
            }

            // Network spawn
            networkObj.Spawn();

            // Register truck
            _activeTrucks[hangarIndex] = truckScript;

            // Set exit point
            if (hangar.exitPoint != null && truckScript != null)
            {
                truckScript.exitPoint = hangar.exitPoint;
            }

            // Post-spawn initialization
            if (truckScript != null)
            {
                StartCoroutine(InitializeTruckAfterSpawnCoroutine(truckScript, truckData.boxType, truckData.cargoAmount));
            }
        }

        private (BoxInfo.BoxType boxType, int cargoAmount) GenerateRandomTruckData()
        {
            BoxInfo.BoxType boxType = (BoxInfo.BoxType)Random.Range(0, BOX_TYPE_COUNT);
            int cargoAmount = Random.Range(MIN_CARGO_AMOUNT, MAX_CARGO_AMOUNT);
            return (boxType, cargoAmount);
        }

        private IEnumerator InitializeTruckAfterSpawnCoroutine(Truck truck, BoxInfo.BoxType reqType, int reqAmount)
        {
            yield return new WaitForEndOfFrame();
            truck.InitializeServerRpc(reqType, reqAmount);
        }

        #endregion

        #region Public API

        /// <summary>
        /// Truck upgrade seviyesini ayarlar
        /// </summary>
        public void SetTruckUpgradeLevel(int level)
        {
            if (IsServer)
            {
                _currentTruckUpgradeLevel.Value = level;
            }
        }

        /// <summary>
        /// Kamyon yok edildiğinde çağrılır
        /// </summary>
        public void OnTruckDestroyed(int hangarIndex)
        {
            if (!IsServer) return;

            if (_activeTrucks.Remove(hangarIndex))
            {
                LogDebug($"Truck destroyed at hangar {hangarIndex}");
            }
        }

        /// <summary>
        /// Belirli bir hangardaki kamyonu döndürür
        /// </summary>
        public Truck GetTruckAtHangar(int hangarIndex)
        {
            return _activeTrucks.TryGetValue(hangarIndex, out Truck truck) ? truck : null;
        }

        #endregion

        #region Utility

        private bool CheckWorkingHours()
        {
            int currentHour = GetCurrentHour();
            return currentHour >= truckStartHour && currentHour < truckEndHour;
        }

        private int GetCurrentHour()
        {
            return DayCycleManager.Instance?.CurrentHour ?? 0;
        }

        #endregion

        #region Logging

        private void LogDebug(string message)
        {
            Debug.Log($"{LOG_PREFIX} {message}");
        }

        private void LogError(string message)
        {
            Debug.LogError($"{LOG_PREFIX} {message}");
        }

        #endregion

        #region Editor Debug

#if UNITY_EDITOR
        [ContextMenu("Log Active Hangars")]
        private void DebugLogActiveHangars()
        {
            Debug.Log($"{LOG_PREFIX} Current Truck Upgrade Level: {_currentTruckUpgradeLevel.Value}");

            for (int i = 0; i < hangarSpawnPoints.Count; i++)
            {
                var hangar = hangarSpawnPoints[i];
                Debug.Log($"Hangar {i}: Required Level = {hangar.requiredUpgradeLevel}, " +
                          $"IsActive = {hangar.isActive}, " +
                          $"Has Truck = {HasActiveTruck(i)}");
            }
        }

        [ContextMenu("Force Spawn All Trucks")]
        private void DebugForceSpawnAll()
        {
            if (IsServer)
            {
                SpawnTrucksForActiveHangars();
            }
        }

        [ContextMenu("Force Exit All Trucks")]
        private void DebugForceExitAll()
        {
            if (IsServer)
            {
                ForceExitAllTrucks();
            }
        }

        [ContextMenu("Debug: Print State")]
        private void DebugPrintState()
        {
            Debug.Log($"{LOG_PREFIX} === TRUCK SPAWNER STATE ===");
            Debug.Log($"Is Server: {IsServer}");
            Debug.Log($"Upgrade Level: {_currentTruckUpgradeLevel.Value}");
            Debug.Log($"Active Trucks: {ActiveTruckCount}");
            Debug.Log($"Pending Respawns: {_hangarRespawnCoroutines.Count}");
            Debug.Log($"Is Within Working Hours: {IsWithinWorkingHours}");
            Debug.Log($"Current Hour: {GetCurrentHour()}");
            Debug.Log($"Working Hours: {truckStartHour}:00 - {truckEndHour}:00");
        }
#endif

        #endregion
    }
}