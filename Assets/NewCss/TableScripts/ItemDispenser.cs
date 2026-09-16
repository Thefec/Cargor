using System;
using Unity.Netcode;
using UnityEngine;

namespace NewCss
{
    /// <summary>
    /// Tek-slotlu, jenerik item dispenser'ı. NetworkedShelf'in (Assets/NewCss/TableScripts/Shelf.cs)
    /// spawn/respawn desenini tek bir ItemData + tek bir Transform slotu için kullanır.
    /// 3 renge sabit olmayan araç/tool item'ları (ör. bant) için.
    /// </summary>
    public class ItemDispenser : NetworkBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[ItemDispenser]";
        private const float SLOT_OCCUPATION_DISTANCE = 0.5f;
        private const float SLOT_OVERLAP_RADIUS = 0.3f;
        private const float GIZMO_SIZE = 0.5f;

        #endregion

        #region Serialized Fields

        [Header("=== ITEM DATA ===")]
        [SerializeField, Tooltip("Dispenser'ın spawn edeceği item data")]
        private ItemData itemData;

        [Header("=== SLOT ===")]
        [SerializeField, Tooltip("Item spawn noktası")]
        private Transform itemSlot;

        [Header("=== RESPAWN SETTINGS ===")]
        [SerializeField, Tooltip("Respawn gecikmesi (saniye)")]
        private float respawnDelay = 1f;

        [SerializeField, Tooltip("Otomatik respawn aktif mi?")]
        private bool enableAutoRespawn = true;

        [Header("=== DEBUG ===")]
        [SerializeField, Tooltip("Debug loglarını göster")]
        private bool showDebugLogs = true;

        #endregion

        #region Network Variables

        private readonly NetworkVariable<ulong> _itemNetworkId = new(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        #endregion

        #region Private Fields

        private NetworkObject _itemObject;
        private bool _pendingRespawn;
        private int _lastItemCount;

        #endregion

        #region Public Properties

        /// <summary>
        /// Slotta şu an item var mı?
        /// </summary>
        public bool HasItem => IsItemSpawned(_itemObject);

        /// <summary>
        /// Otomatik respawn aktif mi?
        /// </summary>
        public bool AutoRespawnEnabled => enableAutoRespawn;

        #endregion

        #region Unity Lifecycle

        private void Update()
        {
            CheckForItemTaken();

            if (!CanPerformNetworkOperations()) return;

            if (enableAutoRespawn)
            {
                CheckAndRespawnIfNeeded();
            }
        }

        #endregion

        #region Network Lifecycle

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            SubscribeToNetworkEvents();

            if (IsServer)
            {
                InitializeItem();
            }
        }

        public override void OnNetworkDespawn()
        {
            UnsubscribeFromNetworkEvents();
            CancelPendingRespawn();

            base.OnNetworkDespawn();
        }

        #endregion

        #region Initialization

        private void InitializeItem()
        {
            SpawnItemIfNeeded();
            UpdateItemCount();
        }

        #endregion

        #region Network Event Subscriptions

        private void SubscribeToNetworkEvents()
        {
            _itemNetworkId.OnValueChanged += HandleItemChanged;
        }

        private void UnsubscribeFromNetworkEvents()
        {
            _itemNetworkId.OnValueChanged -= HandleItemChanged;
        }

        #endregion

        #region Network Event Handlers

        private void HandleItemChanged(ulong previousValue, ulong newValue)
        {
            UpdateLocalReference(newValue);
            UpdateItemCount();
        }

        private void UpdateLocalReference(ulong networkId)
        {
            NetworkObject networkObject = null;

            if (networkId != 0 && NetworkManager.Singleton?.SpawnManager != null)
            {
                NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkId, out networkObject);
            }

            _itemObject = networkObject;
        }

        #endregion

        #region Item Tracking

        private void CheckForItemTaken()
        {
            if (!IsSpawned) return;

            int currentCount = HasItem ? 1 : 0;

            if (_lastItemCount > currentCount)
            {
                LogDebug("📦 Item taken from dispenser!");
            }

            _lastItemCount = currentCount;
        }

        private void UpdateItemCount()
        {
            _lastItemCount = HasItem ? 1 : 0;
        }

        #endregion

        #region Auto Respawn

        private void CheckAndRespawnIfNeeded()
        {
            if (_pendingRespawn) return;

            if (!IsSlotOccupied())
            {
                ClearItemState();
                QueueRespawn();
            }
        }

        private void QueueRespawn()
        {
            _pendingRespawn = true;

            // Use coroutine-free delayed call
            Invoke(nameof(ExecuteRespawn), respawnDelay);
        }

        private void ExecuteRespawn()
        {
            _pendingRespawn = false;

            if (!CanPerformNetworkOperations())
            {
                LogWarning("Cannot respawn item - network not ready");
                return;
            }

            if (!IsSlotOccupied())
            {
                SpawnItemIfNeeded();
            }
        }

        private void CancelPendingRespawn()
        {
            CancelInvoke(nameof(ExecuteRespawn));
            _pendingRespawn = false;
        }

        #endregion

        #region Spawn Logic

        private void SpawnItemIfNeeded()
        {
            if (!CanPerformNetworkOperations()) return;

            if (itemData == null || itemData.worldPrefab == null)
            {
                LogWarning("Dispenser has no valid item data");
                return;
            }

            if (!IsSlotOccupied())
            {
                SpawnItemAtSlot();
            }
        }

        private void SpawnItemAtSlot()
        {
            if (!CanPerformNetworkOperations()) return;

            if (itemSlot == null)
            {
                LogError("Dispenser has no item slot assigned");
                return;
            }

            try
            {
                // Instantiate and spawn
                GameObject spawnedItem = Instantiate(itemData.worldPrefab, itemSlot.position, itemSlot.rotation);
                NetworkObject networkObject = spawnedItem.GetComponent<NetworkObject>();

                if (networkObject == null)
                {
                    LogError("Dispenser item prefab has no NetworkObject component");
                    Destroy(spawnedItem);
                    return;
                }

                networkObject.Spawn();

                // Configure world item
                ConfigureSpawnedItem(spawnedItem);

                // Update state
                SetItemState(networkObject);

                // Start smooth spawn animation via centralized helper
                ItemPlacementAnimator.PlayOn(this, spawnedItem);

                LogDebug($"Spawned item at {itemSlot.name} with NetworkObjectId: {networkObject.NetworkObjectId}");

                // Update counts
                UpdateItemCount();
            }
            catch (Exception ex)
            {
                LogWarning($"Failed to spawn item: {ex.Message}");
                CleanupFailedSpawn();
            }
        }

        private void ConfigureSpawnedItem(GameObject spawnedItem)
        {
            var worldItem = spawnedItem.GetComponent<NetworkWorldItem>();
            if (worldItem != null)
            {
                worldItem.SetItemData(itemData);
                worldItem.EnablePickup();
            }
        }

        private void CleanupFailedSpawn()
        {
            if (itemSlot == null) return;

            var failedObject = itemSlot.GetComponentInChildren<NetworkObject>();
            if (failedObject != null)
            {
                Destroy(failedObject.gameObject);
            }
        }

        #endregion

        #region Slot Occupation Check

        private bool IsSlotOccupied()
        {
            if (itemSlot == null) return false;

            // Check tracked object
            if (IsTrackedObjectAtSlot())
            {
                return true;
            }

            // Check via physics overlap
            return IsAnyItemAtSlot();
        }

        private bool IsTrackedObjectAtSlot()
        {
            if (!IsItemSpawned(_itemObject))
            {
                return false;
            }

            float distance = Vector3.Distance(_itemObject.transform.position, itemSlot.position);
            return distance < SLOT_OCCUPATION_DISTANCE;
        }

        private bool IsAnyItemAtSlot()
        {
            Collider[] colliders = Physics.OverlapSphere(itemSlot.position, SLOT_OVERLAP_RADIUS);

            foreach (var collider in colliders)
            {
                var worldItem = collider.GetComponentInParent<NetworkWorldItem>();
                if (worldItem != null && IsItemSpawned(worldItem.NetworkObject))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsItemSpawned(NetworkObject networkObject)
        {
            return networkObject != null && networkObject.IsSpawned;
        }

        #endregion

        #region State Management

        private void SetItemState(NetworkObject networkObject)
        {
            _itemNetworkId.Value = networkObject?.NetworkObjectId ?? 0;
            _itemObject = networkObject;
        }

        private void ClearItemState()
        {
            SetItemState(null);
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

        #region Public API

        /// <summary>
        /// Mevcut item'ı zorla respawn eder
        /// </summary>
        public void ForceRespawn()
        {
            if (!IsServer) return;

            DespawnItem();
            SpawnItemIfNeeded();
        }

        private void DespawnItem()
        {
            if (IsItemSpawned(_itemObject))
            {
                _itemObject.Despawn();
            }

            ClearItemState();
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
        [ContextMenu("Force Respawn")]
        private void DebugForceRespawn()
        {
            ForceRespawn();
        }

        private void OnDrawGizmosSelected()
        {
            if (itemSlot == null) return;

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(itemSlot.position, Vector3.one * GIZMO_SIZE);

            // Draw occupation sphere
            Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
            Gizmos.DrawSphere(itemSlot.position, SLOT_OVERLAP_RADIUS);
        }
#endif

        #endregion
    }
}
