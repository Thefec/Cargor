using System;
using Unity.Netcode;
using UnityEngine;

namespace NewCss
{
    /// <summary>
    /// Tutorial için truck spawn sistemi. 
    /// Garaj kapýsý açýldýðýnda truck'ý spawn eder.
    /// </summary>
    public class TutorialTruckSpawner : NetworkBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[TutorialTruckSpawner]";

        #endregion

        #region Serialized Fields - Prefab

        [Header("=== TRUCK PREFAB ===")]
        [SerializeField, Tooltip("Tutorial truck prefab'ý")]
        private GameObject tutorialTruckPrefab;

        #endregion

        #region Serialized Fields - Spawn Settings

        [Header("=== SPAWN SETTINGS ===")]
        [SerializeField, Tooltip("Spawn pozisyonu")]
        private Transform spawnPoint;

        [SerializeField, Tooltip("Spawn rotasyonu (opsiyonel)")]
        private Transform spawnRotationReference;

        [Header("=== ROTATION OFFSET ===")]
        [SerializeField, Tooltip("Rotasyon offset'i (Euler angles)")]
        private Vector3 rotationOffset = new Vector3(-90f, 0f, 0f);

        [SerializeField, Tooltip("Rotasyon offset'i kullan")]
        private bool useRotationOffset = true;

        #endregion

        #region Serialized Fields - Truck Settings

        [Header("=== TRUCK CONFIGURATION ===")]
        [SerializeField, Tooltip("Ýstenen kutu türü")]
        private BoxInfo.BoxType requestedBoxType = BoxInfo.BoxType.Red;

        [SerializeField, Tooltip("Gerekli kargo sayýsý")]
        private int requiredCargo = 1;

        #endregion

        #region Serialized Fields - References

        [Header("=== REFERENCES ===")]
        [SerializeField, Tooltip("Garaj kapýsý referansý")]
        private TutorialGarageDoorController garageDoor;

        #endregion

        #region Serialized Fields - Debug

        [Header("=== DEBUG ===")]
        [SerializeField, Tooltip("Debug loglarýný göster")]
        private bool showDebugLogs = true;

        #endregion

        #region Private Fields

        private TutorialTruck _spawnedTruck;
        private bool _hasSpawnedTruck;

        #endregion

        #region Events

        public event Action<TutorialTruck> OnTruckSpawned;
        public event Action OnTruckExited;

        #endregion

        #region Public Properties

        public TutorialTruck SpawnedTruck => _spawnedTruck;
        public bool HasSpawnedTruck => _hasSpawnedTruck;

        #endregion

        #region Network Lifecycle

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            ValidateReferences();
        }

        #endregion

        #region Validation

        private void ValidateReferences()
        {
            if (tutorialTruckPrefab == null)
            {
                Debug.LogError($"{LOG_PREFIX} Tutorial truck prefab is not assigned!");
            }

            if (spawnPoint == null)
            {
                Debug.LogWarning($"{LOG_PREFIX} Spawn point not assigned, using this transform");
                spawnPoint = transform;
            }
        }

        #endregion

        #region Spawn Methods

        /// <summary>
        /// Tutorial truck'ýný spawn eder
        /// </summary>
        public void SpawnTutorialTruck()
        {
            if (!IsServer)
            {
                SpawnTutorialTruckServerRpc();
                return;
            }

            SpawnTruckInternal();
        }

        [ServerRpc(RequireOwnership = false)]
        private void SpawnTutorialTruckServerRpc()
        {
            SpawnTruckInternal();
        }

        private void SpawnTruckInternal()
        {
            if (!IsServer)
            {
                LogDebug("SpawnTruckInternal called on client - ignoring");
                return;
            }

            if (_hasSpawnedTruck && _spawnedTruck != null)
            {
                LogDebug("Truck already spawned - ignoring spawn request");
                return;
            }

            if (tutorialTruckPrefab == null)
            {
                Debug.LogError($"{LOG_PREFIX} Cannot spawn - prefab is null!");
                return;
            }

            // Spawn pozisyonu
            Vector3 spawnPosition = spawnPoint != null ? spawnPoint.position : transform.position;

            // Spawn rotasyonu - DÜZELTME BURADA
            Quaternion spawnRotation = CalculateSpawnRotation();

            LogDebug($"Spawning tutorial truck at {spawnPosition} with rotation {spawnRotation.eulerAngles}");

            // Truck'ý instantiate et
            GameObject truckObj = Instantiate(tutorialTruckPrefab, spawnPosition, spawnRotation);

            // NetworkObject'i spawn et
            NetworkObject networkObject = truckObj.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                Debug.LogError($"{LOG_PREFIX} Truck prefab has no NetworkObject component!");
                Destroy(truckObj);
                return;
            }

            networkObject.Spawn();

            // TutorialTruck component'ini al ve yapýlandýr
            _spawnedTruck = truckObj.GetComponent<TutorialTruck>();
            if (_spawnedTruck != null)
            {
                ConfigureTruck(_spawnedTruck);
                SubscribeToTruckEvents(_spawnedTruck);
            }

            _hasSpawnedTruck = true;

            LogDebug($"Tutorial truck spawned successfully - NetworkObjectId: {networkObject.NetworkObjectId}");

            // Event'i tetikle
            OnTruckSpawned?.Invoke(_spawnedTruck);

            // Client'lara bildir
            NotifyTruckSpawnedClientRpc(networkObject.NetworkObjectId);
        }

        /// <summary>
        /// Spawn rotasyonunu hesaplar - rotasyon offset'i ile birlikte
        /// </summary>
        private Quaternion CalculateSpawnRotation()
        {
            Quaternion baseRotation;

            // Temel rotasyonu belirle
            if (spawnRotationReference != null)
            {
                baseRotation = spawnRotationReference.rotation;
            }
            else if (spawnPoint != null)
            {
                baseRotation = spawnPoint.rotation;
            }
            else
            {
                baseRotation = transform.rotation;
            }

            // Rotasyon offset'i uygula
            if (useRotationOffset)
            {
                Quaternion offsetRotation = Quaternion.Euler(rotationOffset);
                return baseRotation * offsetRotation;
            }

            return baseRotation;
        }

        [ClientRpc]
        private void NotifyTruckSpawnedClientRpc(ulong networkObjectId)
        {
            LogDebug($"Truck spawned notification received - NetworkObjectId: {networkObjectId}");

            // Client'ta truck referansýný bul
            if (_spawnedTruck == null && NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject netObj))
            {
                _spawnedTruck = netObj.GetComponent<TutorialTruck>();
                _hasSpawnedTruck = true;
            }
        }

        private void ConfigureTruck(TutorialTruck truck)
        {
            truck.requestedBoxType = requestedBoxType;
            truck.requiredCargo = requiredCargo;

            LogDebug($"Truck configured - BoxType: {requestedBoxType}, RequiredCargo: {requiredCargo}");
        }

        #endregion

        #region Truck Event Subscriptions

        private void SubscribeToTruckEvents(TutorialTruck truck)
        {
            if (truck == null) return;

            truck.OnTruckExitComplete += HandleTruckExited;
        }

        private void UnsubscribeFromTruckEvents(TutorialTruck truck)
        {
            if (truck == null) return;

            truck.OnTruckExitComplete -= HandleTruckExited;
        }

        private void HandleTruckExited()
        {
            LogDebug("Truck exit complete - notifying garage door");

            _hasSpawnedTruck = false;
            _spawnedTruck = null;

            // Garaj kapýsýný kapat
            if (garageDoor != null)
            {
                garageDoor.OnTruckExited();
            }

            OnTruckExited?.Invoke();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Spawner'ý sýfýrlar (yeni tutorial için)
        /// </summary>
        public void ResetSpawner()
        {
            if (_spawnedTruck != null)
            {
                UnsubscribeFromTruckEvents(_spawnedTruck);

                if (IsServer && _spawnedTruck.NetworkObject != null && _spawnedTruck.NetworkObject.IsSpawned)
                {
                    _spawnedTruck.NetworkObject.Despawn();
                }
            }

            _spawnedTruck = null;
            _hasSpawnedTruck = false;

            LogDebug("Spawner reset");
        }

        /// <summary>
        /// Rotasyon offset'ini ayarlar
        /// </summary>
        public void SetRotationOffset(Vector3 offset)
        {
            rotationOffset = offset;
            LogDebug($"Rotation offset set to: {offset}");
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

        #endregion

        #region Editor Debug

#if UNITY_EDITOR
        [ContextMenu("Spawn Tutorial Truck")]
        private void DebugSpawnTruck()
        {
            if (Application.isPlaying)
            {
                SpawnTutorialTruck();
            }
        }

        [ContextMenu("Reset Spawner")]
        private void DebugResetSpawner()
        {
            if (Application.isPlaying)
            {
                ResetSpawner();
            }
        }

        [ContextMenu("Debug: Print State")]
        private void DebugPrintState()
        {
            Debug.Log($"{LOG_PREFIX} === SPAWNER STATE ===");
            Debug.Log($"Has Spawned Truck: {_hasSpawnedTruck}");
            Debug.Log($"Spawned Truck: {(_spawnedTruck != null ? _spawnedTruck.name : "null")}");
            Debug.Log($"Requested Box Type: {requestedBoxType}");
            Debug.Log($"Required Cargo: {requiredCargo}");
            Debug.Log($"Spawn Point: {(spawnPoint != null ? spawnPoint.name : "null")}");
            Debug.Log($"Rotation Offset: {rotationOffset}");
            Debug.Log($"Use Rotation Offset: {useRotationOffset}");
        }

        private void OnDrawGizmosSelected()
        {
            if (spawnPoint == null) return;

            Vector3 spawnPos = spawnPoint.position;

            // Spawn pozisyonunu göster
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(spawnPos, 0.5f);

            // Hesaplanan rotasyonu göster
            Quaternion finalRotation = CalculateSpawnRotation();

            // Forward yönü (truck'ýn gideceði yön)
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(spawnPos, finalRotation * Vector3.forward * 3f);

            // Up yönü
            Gizmos.color = Color.green;
            Gizmos.DrawRay(spawnPos, finalRotation * Vector3.up * 2f);

            // Right yönü
            Gizmos.color = Color.red;
            Gizmos.DrawRay(spawnPos, finalRotation * Vector3.right * 2f);

            // Truck'ýn yaklaþýk boyutunu göster
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Matrix4x4 rotationMatrix = Matrix4x4.TRS(spawnPos, finalRotation, Vector3.one);
            Gizmos.matrix = rotationMatrix;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(2f, 2f, 5f)); // Yaklaþýk truck boyutu
            Gizmos.matrix = Matrix4x4.identity;
        }
#endif

        #endregion
    }
}