using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace NewCss
{
    /// <summary>
    /// Network destekli raf sistemi - kutuların spawn, respawn ve takibini yönetir. 
    /// Her renk için ayrı slot ve otomatik respawn özelliği sunar.
    /// </summary>
    public class NetworkedShelf : NetworkBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[NetworkedShelf]";
        private const float SLOT_OCCUPATION_DISTANCE = 0.5f;
        private const float SLOT_OVERLAP_RADIUS = 0.3f;
        private const float GIZMO_SIZE = 0.5f;

        #endregion

        #region Enums

        public enum BoxType
        {
            Red,
            Blue,
            Yellow
        }

        #endregion

        #region Serialized Fields

        [Header("=== ITEM DATA ===")]
        [SerializeField, Tooltip("Kırmızı kutu item data")]
        public ItemData redBoxItemData;

        [SerializeField, Tooltip("Mavi kutu item data")]
        public ItemData blueBoxItemData;

        [SerializeField, Tooltip("Sarı kutu item data")]
        public ItemData yellowBoxItemData;

        [Header("=== BOX SLOTS ===")]
        [SerializeField, Tooltip("Kırmızı kutu slot pozisyonu")]
        public Transform redBoxSlot;

        [SerializeField, Tooltip("Mavi kutu slot pozisyonu")]
        public Transform blueBoxSlot;

        [SerializeField, Tooltip("Sarı kutu slot pozisyonu")]
        public Transform yellowBoxSlot;

        [Header("=== RESPAWN SETTINGS ===")]
        [SerializeField, Tooltip("Respawn gecikmesi (saniye)")]
        private float respawnDelay = 1f;

        [SerializeField, Tooltip("Otomatik respawn aktif mi?")]
        private bool enableAutoRespawn = true;

        [Header("=== SHELF BEHAVIOR ===")]
        [SerializeField, Tooltip("Rafa item yerleştirmeye izin ver")]
        private bool allowPlacingItems = false;

        [Header("=== DEBUG ===")]
        [SerializeField, Tooltip("Debug loglarını göster")]
        private bool showDebugLogs = true;

        #endregion

        #region Network Variables

        private readonly NetworkVariable<ulong> _redBoxNetworkId = new(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<ulong> _blueBoxNetworkId = new(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<ulong> _yellowBoxNetworkId = new(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        #endregion

        #region Private Fields

        // Local references to spawned objects
        private NetworkObject _redBoxObject;
        private NetworkObject _blueBoxObject;
        private NetworkObject _yellowBoxObject;

        // Box count tracking for detecting when boxes are taken
        private BoxCountState _lastBoxCounts;

        // Pending respawn tracking
        private readonly HashSet<BoxType> _pendingRespawns = new();

        #endregion

        #region Nested Types

        private struct BoxCountState
        {
            public int RedCount;
            public int BlueCount;
            public int YellowCount;

            public static BoxCountState Create(bool hasRed, bool hasBlue, bool hasYellow)
            {
                return new BoxCountState
                {
                    RedCount = hasRed ? 1 : 0,
                    BlueCount = hasBlue ? 1 : 0,
                    YellowCount = hasYellow ? 1 : 0
                };
            }
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// Kırmızı kutu var mı?
        /// </summary>
        public bool HasRedBox => IsBoxSpawned(_redBoxObject);

        /// <summary>
        /// Mavi kutu var mı?
        /// </summary>
        public bool HasBlueBox => IsBoxSpawned(_blueBoxObject);

        /// <summary>
        /// Sarı kutu var mı?
        /// </summary>
        public bool HasYellowBox => IsBoxSpawned(_yellowBoxObject);

        /// <summary>
        /// Item yerleştirmeye izin veriyor mu?
        /// </summary>
        public bool AllowPlacingItems => allowPlacingItems;

        /// <summary>
        /// Otomatik respawn aktif mi? 
        /// </summary>
        public bool AutoRespawnEnabled => enableAutoRespawn;

        #endregion

        #region Unity Lifecycle

        private void Update()
        {
            if (!CanPerformNetworkOperations()) return;

            if (enableAutoRespawn)
            {
                CheckAndRespawnIfNeeded();
            }

            CheckForBoxTaken();
        }

        #endregion

        #region Network Lifecycle

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            SubscribeToNetworkEvents();

            if (IsServer)
            {
                InitializeAllBoxes();
            }
        }

        public override void OnNetworkDespawn()
        {
            UnsubscribeFromNetworkEvents();
            CancelAllPendingRespawns();

            base.OnNetworkDespawn();
        }

        #endregion

        #region Initialization

        private void InitializeAllBoxes()
        {
            SpawnBoxIfNeeded(BoxType.Red);
            SpawnBoxIfNeeded(BoxType.Blue);
            SpawnBoxIfNeeded(BoxType.Yellow);

            UpdateBoxCounts();
        }

        #endregion

        #region Network Event Subscriptions

        private void SubscribeToNetworkEvents()
        {
            _redBoxNetworkId.OnValueChanged += HandleRedBoxChanged;
            _blueBoxNetworkId.OnValueChanged += HandleBlueBoxChanged;
            _yellowBoxNetworkId.OnValueChanged += HandleYellowBoxChanged;
        }

        private void UnsubscribeFromNetworkEvents()
        {
            _redBoxNetworkId.OnValueChanged -= HandleRedBoxChanged;
            _blueBoxNetworkId.OnValueChanged -= HandleBlueBoxChanged;
            _yellowBoxNetworkId.OnValueChanged -= HandleYellowBoxChanged;
        }

        #endregion

        #region Network Event Handlers

        private void HandleRedBoxChanged(ulong previousValue, ulong newValue)
        {
            UpdateLocalReference(BoxType.Red, newValue);
            UpdateBoxCounts();
        }

        private void HandleBlueBoxChanged(ulong previousValue, ulong newValue)
        {
            UpdateLocalReference(BoxType.Blue, newValue);
            UpdateBoxCounts();
        }

        private void HandleYellowBoxChanged(ulong previousValue, ulong newValue)
        {
            UpdateLocalReference(BoxType.Yellow, newValue);
            UpdateBoxCounts();
        }

        private void UpdateLocalReference(BoxType boxType, ulong networkId)
        {
            NetworkObject networkObject = null;

            if (networkId != 0 && NetworkManager.Singleton?.SpawnManager != null)
            {
                NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkId, out networkObject);
            }

            SetBoxObject(boxType, networkObject);
        }

        #endregion

        #region Box Tracking

        private void CheckForBoxTaken()
        {
            var currentCounts = BoxCountState.Create(HasRedBox, HasBlueBox, HasYellowBox);

            // Check each box type
            CheckSingleBoxTaken(BoxType.Red, _lastBoxCounts.RedCount, currentCounts.RedCount);
            CheckSingleBoxTaken(BoxType.Blue, _lastBoxCounts.BlueCount, currentCounts.BlueCount);
            CheckSingleBoxTaken(BoxType.Yellow, _lastBoxCounts.YellowCount, currentCounts.YellowCount);

            // Update last counts
            _lastBoxCounts = currentCounts;
        }

        private void CheckSingleBoxTaken(BoxType boxType, int lastCount, int currentCount)
        {
            if (lastCount > currentCount)
            {
                LogDebug($"📦 {boxType} box taken from shelf!");
                NotifyTutorialBoxTaken(boxType);
            }
        }

        private void UpdateBoxCounts()
        {
            _lastBoxCounts = BoxCountState.Create(HasRedBox, HasBlueBox, HasYellowBox);
        }

        #endregion

        #region Auto Respawn

        private void CheckAndRespawnIfNeeded()
        {
            CheckAndQueueRespawn(BoxType.Red);
            CheckAndQueueRespawn(BoxType.Blue);
            CheckAndQueueRespawn(BoxType.Yellow);
        }

        private void CheckAndQueueRespawn(BoxType boxType)
        {
            // Skip if already pending
            if (_pendingRespawns.Contains(boxType))
            {
                return;
            }

            var slot = GetSlotForBoxType(boxType);
            var networkObject = GetBoxObject(boxType);

            if (!IsSlotOccupied(slot, networkObject))
            {
                ClearBoxState(boxType);
                QueueRespawn(boxType);
            }
        }

        private void QueueRespawn(BoxType boxType)
        {
            _pendingRespawns.Add(boxType);

            // Use coroutine-free delayed call
            Invoke(GetRespawnMethodName(boxType), respawnDelay);
        }

        private string GetRespawnMethodName(BoxType boxType)
        {
            return boxType switch
            {
                BoxType.Red => nameof(RespawnRedBox),
                BoxType.Blue => nameof(RespawnBlueBox),
                BoxType.Yellow => nameof(RespawnYellowBox),
                _ => throw new ArgumentOutOfRangeException(nameof(boxType))
            };
        }

        private void RespawnRedBox()
        {
            ExecuteRespawn(BoxType.Red);
        }

        private void RespawnBlueBox()
        {
            ExecuteRespawn(BoxType.Blue);
        }

        private void RespawnYellowBox()
        {
            ExecuteRespawn(BoxType.Yellow);
        }

        private void ExecuteRespawn(BoxType boxType)
        {
            _pendingRespawns.Remove(boxType);

            if (!CanPerformNetworkOperations())
            {
                LogWarning($"Cannot respawn {boxType} box - network not ready");
                return;
            }

            var slot = GetSlotForBoxType(boxType);
            var networkObject = GetBoxObject(boxType);

            if (!IsSlotOccupied(slot, networkObject))
            {
                SpawnBoxIfNeeded(boxType);
            }
        }

        private void CancelAllPendingRespawns()
        {
            CancelInvoke(nameof(RespawnRedBox));
            CancelInvoke(nameof(RespawnBlueBox));
            CancelInvoke(nameof(RespawnYellowBox));
            _pendingRespawns.Clear();
        }

        #endregion

        #region Spawn Logic

        private void SpawnBoxIfNeeded(BoxType boxType)
        {
            if (!CanPerformNetworkOperations()) return;

            var slot = GetSlotForBoxType(boxType);
            var itemData = GetItemDataForBoxType(boxType);
            var networkObject = GetBoxObject(boxType);

            if (itemData == null || itemData.worldPrefab == null)
            {
                LogWarning($"{boxType} box has no valid item data");
                return;
            }

            if (!IsSlotOccupied(slot, networkObject))
            {
                SpawnBoxAtSlot(slot, itemData, boxType);
            }
        }

        private void SpawnBoxAtSlot(Transform slot, ItemData itemData, BoxType boxType)
        {
            if (!CanPerformNetworkOperations()) return;

            try
            {
                // Instantiate and spawn
                GameObject spawnedBox = Instantiate(itemData.worldPrefab, slot.position, slot.rotation);
                NetworkObject networkObject = spawnedBox.GetComponent<NetworkObject>();

                if (networkObject == null)
                {
                    LogError($"{boxType} box prefab has no NetworkObject component");
                    Destroy(spawnedBox);
                    return;
                }

                networkObject.Spawn();

                // Configure world item
                ConfigureSpawnedBox(spawnedBox, itemData);

                // Update state
                SetBoxState(boxType, networkObject);

                LogDebug($"Spawned {boxType} box at {slot.name} with NetworkObjectId: {networkObject.NetworkObjectId}");

                // Update counts
                UpdateBoxCounts();
            }
            catch (Exception ex)
            {
                LogWarning($"Failed to spawn {boxType} box: {ex.Message}");
                CleanupFailedSpawn(slot);
            }
        }

        private void ConfigureSpawnedBox(GameObject spawnedBox, ItemData itemData)
        {
            var worldItem = spawnedBox.GetComponent<NetworkWorldItem>();
            if (worldItem != null)
            {
                worldItem.SetItemData(itemData);
                worldItem.EnablePickup();
            }
        }

        private void CleanupFailedSpawn(Transform slot)
        {
            var failedObject = slot.GetComponentInChildren<NetworkObject>();
            if (failedObject != null)
            {
                Destroy(failedObject.gameObject);
            }
        }

        #endregion

        #region Slot Occupation Check

        private bool IsSlotOccupied(Transform slot, NetworkObject networkObject)
        {
            if (slot == null) return false;

            // Check tracked object
            if (IsTrackedObjectAtSlot(slot, networkObject))
            {
                return true;
            }

            // Check via physics overlap
            return IsAnyItemAtSlot(slot);
        }

        private bool IsTrackedObjectAtSlot(Transform slot, NetworkObject networkObject)
        {
            if (!IsBoxSpawned(networkObject))
            {
                return false;
            }

            float distance = Vector3.Distance(networkObject.transform.position, slot.position);
            return distance < SLOT_OCCUPATION_DISTANCE;
        }

        private bool IsAnyItemAtSlot(Transform slot)
        {
            Collider[] colliders = Physics.OverlapSphere(slot.position, SLOT_OVERLAP_RADIUS);

            foreach (var collider in colliders)
            {
                var worldItem = collider.GetComponent<NetworkWorldItem>();
                if (worldItem != null && IsBoxSpawned(worldItem.NetworkObject))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsBoxSpawned(NetworkObject networkObject)
        {
            return networkObject != null && networkObject.IsSpawned;
        }

        #endregion

        #region State Management

        private void SetBoxState(BoxType boxType, NetworkObject networkObject)
        {
            ulong networkId = networkObject?.NetworkObjectId ?? 0;

            switch (boxType)
            {
                case BoxType.Red:
                    _redBoxNetworkId.Value = networkId;
                    _redBoxObject = networkObject;
                    break;
                case BoxType.Blue:
                    _blueBoxNetworkId.Value = networkId;
                    _blueBoxObject = networkObject;
                    break;
                case BoxType.Yellow:
                    _yellowBoxNetworkId.Value = networkId;
                    _yellowBoxObject = networkObject;
                    break;
            }
        }

        private void ClearBoxState(BoxType boxType)
        {
            SetBoxState(boxType, null);
        }

        private void SetBoxObject(BoxType boxType, NetworkObject networkObject)
        {
            switch (boxType)
            {
                case BoxType.Red:
                    _redBoxObject = networkObject;
                    break;
                case BoxType.Blue:
                    _blueBoxObject = networkObject;
                    break;
                case BoxType.Yellow:
                    _yellowBoxObject = networkObject;
                    break;
            }
        }

        private NetworkObject GetBoxObject(BoxType boxType)
        {
            return boxType switch
            {
                BoxType.Red => _redBoxObject,
                BoxType.Blue => _blueBoxObject,
                BoxType.Yellow => _yellowBoxObject,
                _ => null
            };
        }

        private Transform GetSlotForBoxType(BoxType boxType)
        {
            return boxType switch
            {
                BoxType.Red => redBoxSlot,
                BoxType.Blue => blueBoxSlot,
                BoxType.Yellow => yellowBoxSlot,
                _ => null
            };
        }

        private ItemData GetItemDataForBoxType(BoxType boxType)
        {
            return boxType switch
            {
                BoxType.Red => redBoxItemData,
                BoxType.Blue => blueBoxItemData,
                BoxType.Yellow => yellowBoxItemData,
                _ => null
            };
        }

        #endregion

        #region Network Validation

        private bool CanPerformNetworkOperations()
        {
            return NetworkManager.Singleton != null &&
                   NetworkManager.Singleton.IsListening &&
                   IsSpawned &&
                   IsServer;
        }

        #endregion

        #region Notifications

        private void NotifyTutorialBoxTaken(BoxType boxType)
        {
            if (TutorialManager.Instance == null) return;

            TutorialManager.Instance.OnBoxTakenFromShelf(boxType);

            LogDebug($"📚 Tutorial notified: {boxType} box taken from shelf");
        }

        #endregion

        #region Public API

        /// <summary>
        /// Tüm kutuları zorla respawn eder
        /// </summary>
        public void ForceRespawnAll()
        {
            if (!IsServer) return;

            ForceRespawnBox(BoxType.Red);
            ForceRespawnBox(BoxType.Blue);
            ForceRespawnBox(BoxType.Yellow);
        }

        /// <summary>
        /// Belirli bir kutuyu zorla respawn eder
        /// </summary>
        public void ForceRespawnBox(BoxType boxType)
        {
            if (!IsServer) return;

            // Despawn existing
            DespawnBox(boxType);

            // Spawn new
            SpawnBoxIfNeeded(boxType);
        }

        /// <summary>
        /// Item yerleştirmeye izin veriyor mu kontrolü (backward compatibility)
        /// </summary>
        public bool CanPlaceItems()
        {
            return allowPlacingItems;
        }

        private void DespawnBox(BoxType boxType)
        {
            var networkObject = GetBoxObject(boxType);

            if (IsBoxSpawned(networkObject))
            {
                networkObject.Despawn();
            }

            ClearBoxState(boxType);
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

        #region Editor & Debug

#if UNITY_EDITOR
        [ContextMenu("Force Respawn All")]
        private void DebugForceRespawnAll()
        {
            ForceRespawnAll();
        }

        [ContextMenu("Debug: Print State")]
        private void DebugPrintState()
        {
            Debug.Log($"{LOG_PREFIX} === SHELF STATE ===");
            Debug.Log($"Has Red Box: {HasRedBox}");
            Debug.Log($"Has Blue Box: {HasBlueBox}");
            Debug.Log($"Has Yellow Box: {HasYellowBox}");
            Debug.Log($"Auto Respawn: {enableAutoRespawn}");
            Debug.Log($"Pending Respawns: {string.Join(", ", _pendingRespawns)}");
        }

        private void OnDrawGizmosSelected()
        {
            DrawSlotGizmo(redBoxSlot, Color.red);
            DrawSlotGizmo(blueBoxSlot, Color.blue);
            DrawSlotGizmo(yellowBoxSlot, Color.yellow);
        }

        private void DrawSlotGizmo(Transform slot, Color color)
        {
            if (slot == null) return;

            Gizmos.color = color;
            Gizmos.DrawWireCube(slot.position, Vector3.one * GIZMO_SIZE);

            // Draw occupation sphere
            Gizmos.color = new Color(color.r, color.g, color.b, 0.3f);
            Gizmos.DrawSphere(slot.position, SLOT_OVERLAP_RADIUS);
        }
#endif

        #endregion
    }
}