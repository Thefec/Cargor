using Unity.Netcode;
using UnityEngine;

namespace NewCss
{
    /// <summary>
    /// Tutorial için özelleştirilmiş raf sistemi. 
    /// PlaceOnShelf tutorial step'i ile entegre çalışır.
    /// Item yerleştirildiğinde TutorialManager'a bildirim gönderir.
    /// </summary>
    public class TutorialShelfState : NetworkBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[TutorialShelfState]";
        private const float SLOT_GIZMO_SIZE = 0.2f;
        private const float CENTER_GIZMO_RADIUS = 0.1f;

        #endregion

        #region Serialized Fields - Shelf Configuration

        [Header("=== SHELF CONFIGURATION ===")]
        [SerializeField, Tooltip("Raftaki item slotlarının transform'ları")]
        public Transform[] shelfSlots;

        #endregion

        #region Serialized Fields - Interaction Settings

        [Header("=== INTERACTION SETTINGS ===")]
        [SerializeField, Tooltip("Etkileşim alanı boyutu")]
        private Vector3 interactionBoxSize = new Vector3(3f, 2f, 2f);

        [SerializeField, Tooltip("Etkileşim alanı offset'i")]
        private Vector3 interactionBoxOffset = Vector3.zero;

        [SerializeField, Tooltip("Oyuncu layer mask'i")]
        private LayerMask playerLayer;

        [SerializeField, Tooltip("Etkileşim alanını Gizmo ile göster")]
        private bool showInteractionRange = true;

        #endregion

        #region Serialized Fields - Tutorial Settings

        [Header("=== TUTORIAL SETTINGS ===")]
        [SerializeField, Tooltip("Sadece belirli kutu türünü kabul et")]
        private bool requireSpecificBoxType;

        [SerializeField, Tooltip("Kabul edilen kutu türü")]
        private BoxInfo.BoxType acceptedBoxType = BoxInfo.BoxType.Red;

        [SerializeField, Tooltip("Maksimum item sayısı (0 = sınırsız)")]
        private int maxItemCount = 1;

        #endregion

        #region Serialized Fields - Debug

        [Header("=== DEBUG ===")]
        [SerializeField, Tooltip("Debug loglarını göster")]
        private bool showDebugLogs = true;

        #endregion

        #region Network Variables

        private NetworkList<NetworkObjectReference> _slotItems;

        private readonly NetworkVariable<int> _itemCount = new(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        #endregion

        #region Private Fields

        private BoxCollider _interactionTrigger;

        #endregion

        #region Public Properties

        /// <summary>
        /// Etkileşim alanı boyutu
        /// </summary>
        public Vector3 InteractionBoxSize => interactionBoxSize;

        /// <summary>
        /// Etkileşim alanı offset'i
        /// </summary>
        public Vector3 InteractionBoxOffset => interactionBoxOffset;

        /// <summary>
        /// Toplam slot sayısı
        /// </summary>
        public int SlotCount => shelfSlots?.Length ?? 0;

        /// <summary>
        /// Mevcut item sayısı
        /// </summary>
        public int ItemCount => _itemCount.Value;

        /// <summary>
        /// Raf dolu mu?
        /// </summary>
        public bool IsFull => maxItemCount > 0 && _itemCount.Value >= maxItemCount;

        /// <summary>
        /// Rafta item var mı?
        /// </summary>
        public bool HasItem => _itemCount.Value > 0;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeNetworkList();
        }

        private void Start()
        {
            SetupInteractionTrigger();
        }

        private void OnValidate()
        {
            UpdateInteractionTriggerInEditor();
        }

        #endregion

        #region Network Lifecycle

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                InitializeSlots();
            }

            SubscribeToNetworkEvents();

            LogDebug("TutorialShelfState spawned");
        }

        public override void OnNetworkDespawn()
        {
            UnsubscribeFromNetworkEvents();
            base.OnNetworkDespawn();
        }

        #endregion

        #region Initialization

        private void InitializeNetworkList()
        {
            _slotItems = new NetworkList<NetworkObjectReference>();
        }

        private void InitializeSlots()
        {
            if (shelfSlots == null || shelfSlots.Length == 0)
            {
                LogDebug("No shelf slots configured - using single virtual slot");
                _slotItems.Add(new NetworkObjectReference());
                return;
            }

            for (int i = 0; i < shelfSlots.Length; i++)
            {
                _slotItems.Add(new NetworkObjectReference());
            }

            LogDebug($"Initialized {shelfSlots.Length} slots");
        }

        private void SubscribeToNetworkEvents()
        {
            if (_slotItems != null)
            {
                _slotItems.OnListChanged += HandleSlotItemsChanged;
            }

            _itemCount.OnValueChanged += HandleItemCountChanged;
        }

        private void UnsubscribeFromNetworkEvents()
        {
            if (_slotItems != null)
            {
                _slotItems.OnListChanged -= HandleSlotItemsChanged;
            }

            _itemCount.OnValueChanged -= HandleItemCountChanged;
        }

        #endregion

        #region Network Event Handlers

        private void HandleSlotItemsChanged(NetworkListEvent<NetworkObjectReference> changeEvent)
        {
            UpdateVisualState();
        }

        private void HandleItemCountChanged(int previousValue, int newValue)
        {
            LogDebug($"Item count changed: {previousValue} -> {newValue}");
        }

        #endregion

        #region Interaction Trigger Setup

        private void SetupInteractionTrigger()
        {
            _interactionTrigger = FindOrCreateInteractionTrigger();
            ConfigureInteractionTrigger(_interactionTrigger);
        }

        private BoxCollider FindOrCreateInteractionTrigger()
        {
            var boxColliders = GetComponents<BoxCollider>();

            foreach (var collider in boxColliders)
            {
                if (collider.isTrigger)
                {
                    return collider;
                }
            }

            var newTrigger = gameObject.AddComponent<BoxCollider>();
            newTrigger.isTrigger = true;
            LogDebug($"Box interaction trigger created with size {interactionBoxSize}");

            return newTrigger;
        }

        private void ConfigureInteractionTrigger(BoxCollider trigger)
        {
            if (trigger == null) return;

            trigger.size = interactionBoxSize;
            trigger.center = interactionBoxOffset;
        }

        private void UpdateInteractionTriggerInEditor()
        {
            if (!Application.isPlaying || _interactionTrigger == null) return;

            ConfigureInteractionTrigger(_interactionTrigger);
        }

        #endregion

        #region Range Detection

        /// <summary>
        /// Transform bazlı range kontrolü
        /// </summary>
        public bool IsPlayerInRange(Transform playerTransform)
        {
            if (playerTransform == null) return false;

            Vector3 localPoint = transform.InverseTransformPoint(playerTransform.position);
            Vector3 halfSize = interactionBoxSize * 0.5f;

            return IsPointInsideBox(localPoint, interactionBoxOffset, halfSize);
        }

        /// <summary>
        /// ClientId bazlı range kontrolü
        /// </summary>
        public bool IsPlayerInRange(ulong clientId)
        {
            if (!TryGetPlayerTransform(clientId, out Transform playerTransform))
            {
                return false;
            }

            return IsPlayerInRange(playerTransform);
        }

        private static bool IsPointInsideBox(Vector3 point, Vector3 center, Vector3 halfExtents)
        {
            return Mathf.Abs(point.x - center.x) <= halfExtents.x &&
                   Mathf.Abs(point.y - center.y) <= halfExtents.y &&
                   Mathf.Abs(point.z - center.z) <= halfExtents.z;
        }

        private static bool TryGetPlayerTransform(ulong clientId, out Transform playerTransform)
        {
            playerTransform = null;

            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
            {
                return false;
            }

            if (client.PlayerObject == null)
            {
                return false;
            }

            playerTransform = client.PlayerObject.transform;
            return true;
        }

        #endregion

        #region Slot Management

        /// <summary>
        /// İlk boş slot index'ini bulur
        /// </summary>
        private int FindEmptySlotIndex()
        {
            int maxIndex = Mathf.Min(_slotItems.Count, shelfSlots?.Length ?? 1);

            for (int i = 0; i < maxIndex; i++)
            {
                if (!IsSlotOccupied(i))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Belirtilen slot'un dolu olup olmadığını kontrol eder
        /// </summary>
        private bool IsSlotOccupied(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slotItems.Count)
            {
                return false;
            }

            return _slotItems[slotIndex].TryGet(out NetworkObject networkObj) && networkObj != null;
        }

        /// <summary>
        /// Belirtilen NetworkObjectId'ye sahip item'ın slot index'ini bulur
        /// </summary>
        private int FindSlotIndexByNetworkId(ulong networkObjectId)
        {
            for (int i = 0; i < _slotItems.Count; i++)
            {
                if (_slotItems[i].TryGet(out NetworkObject networkObj) &&
                    networkObj != null &&
                    networkObj.NetworkObjectId == networkObjectId)
                {
                    return i;
                }
            }

            return -1;
        }

        #endregion

        #region Visual State Management

        private void UpdateVisualState()
        {
            int maxIndex = Mathf.Min(_slotItems.Count, shelfSlots?.Length ?? 0);

            for (int i = 0; i < maxIndex; i++)
            {
                UpdateSlotVisual(i);
            }
        }

        private void UpdateSlotVisual(int slotIndex)
        {
            if (shelfSlots == null || slotIndex >= shelfSlots.Length) return;

            if (!_slotItems[slotIndex].TryGet(out NetworkObject networkObj) || networkObj == null)
            {
                return;
            }

            var item = networkObj.gameObject;
            var slot = shelfSlots[slotIndex];

            item.transform.SetParent(slot);
            item.transform.localPosition = Vector3.zero;
            item.transform.localRotation = Quaternion.identity;

            DisableItemPhysics(item);
        }

        private static void DisableItemPhysics(GameObject item)
        {
            var rb = item.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
            }

            var col = item.GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = false;
            }
        }

        private static void EnableItemPhysics(GameObject item)
        {
            var rb = item.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
            }

            var col = item.GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = true;
            }
        }

        #endregion

        #region Server RPCs - Place Item

        /// <summary>
        /// Rafa item yerleştirir (Tutorial versiyonu)
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void PlaceItemOnShelfServerRpc(NetworkObjectReference itemRef, ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;

            ulong requesterClientId = rpcParams.Receive.SenderClientId;
            LogDebug($"📥 PlaceItemOnShelfServerRpc - Client {requesterClientId}");

            // Box category validation - only allow Box category items on shelf
            if (!ValidateItemIsBox(itemRef))
            {
                LogDebug("❌ Only Box category items can be placed on shelf!");
                return;
            }

            // Validation
            if (!ValidatePlaceItemRequest(requesterClientId, itemRef, out Transform playerTransform, out BoxInfo boxInfo))
            {
                return;
            }

            // Boş slot bul
            int slotIndex = FindEmptySlotIndex();
            if (slotIndex == -1)
            {
                LogDebug("❌ Shelf is FULL!");
                return;
            }

            // Item'ı yerleştir
            PlaceItemInSlot(itemRef, slotIndex, requesterClientId, boxInfo);
        }

        /// <summary>
        /// Server-side version - PlayerInventory tarafından çağrılır
        /// </summary>
        public void PlaceItemOnShelfFromServer(NetworkObjectReference itemRef, ulong requesterClientId)
        {
            if (!IsServer) return;

            LogDebug($"📥 PlaceItemOnShelfFromServer - Client {requesterClientId}");

            // Dolu mu kontrol et
            if (IsFull)
            {
                LogDebug("❌ Shelf is FULL!");
                return;
            }

            // Box category validation - only allow Box category items on shelf
            if (!ValidateItemIsBox(itemRef))
            {
                LogDebug("❌ Only Box category items can be placed on shelf!");
                return;
            }

            // BoxInfo kontrolü (opsiyonel)
            BoxInfo boxInfo = null;
            if (itemRef.TryGet(out NetworkObject netObj) && netObj != null)
            {
                boxInfo = netObj.GetComponent<BoxInfo>();

                // Belirli kutu türü gerekiyorsa kontrol et
                if (requireSpecificBoxType && boxInfo != null && boxInfo.boxType != acceptedBoxType)
                {
                    LogDebug($"❌ Wrong box type! Expected: {acceptedBoxType}, Got: {boxInfo.boxType}");
                    return;
                }
            }

            // Boş slot bul
            int slotIndex = FindEmptySlotIndex();
            if (slotIndex == -1)
            {
                LogDebug("❌ No empty slot found!");
                return;
            }

            // Item'ı yerleştir
            PlaceItemInSlot(itemRef, slotIndex, requesterClientId, boxInfo);
        }

        /// <summary>
        /// Validates that the item is a Box category item.
        /// Only Box category items can be placed on shelves.
        /// </summary>
        private bool ValidateItemIsBox(NetworkObjectReference itemRef)
        {
            if (!itemRef.TryGet(out NetworkObject networkObj) || networkObj == null)
            {
                LogDebug("❌ Invalid item reference for box validation!");
                return false;
            }

            // Check NetworkWorldItem for ItemData category
            var worldItem = networkObj.GetComponent<NetworkWorldItem>();
            if (worldItem != null && worldItem.ItemData != null)
            {
                if (worldItem.ItemData.itemCategory == ItemCategory.Box)
                {
                    LogDebug("✅ Item category is Box - allowed on shelf");
                    return true;
                }
                else
                {
                    LogDebug($"❌ Item category is {worldItem.ItemData.itemCategory} - NOT allowed on shelf (only Box category)");
                    return false;
                }
            }

            // Fallback: Check for BoxInfo component
            var boxInfo = networkObj.GetComponent<BoxInfo>();
            if (boxInfo != null)
            {
                LogDebug("✅ BoxInfo component found - allowed on shelf");
                return true;
            }

            LogDebug("❌ No valid category information found - NOT allowed on shelf");
            return false;
        }

        private bool ValidatePlaceItemRequest(ulong clientId, NetworkObjectReference itemRef, out Transform playerTransform, out BoxInfo boxInfo)
        {
            playerTransform = null;
            boxInfo = null;

            // Dolu mu kontrol et
            if (IsFull)
            {
                LogDebug("❌ Shelf is FULL!");
                return false;
            }

            // Player'ı bul
            if (!TryGetPlayerTransform(clientId, out playerTransform))
            {
                LogDebug($"❌ Player object not found for client {clientId}");
                return false;
            }

            // Range kontrolü
            if (!IsPlayerInRange(playerTransform))
            {
                float distance = Vector3.Distance(playerTransform.position, transform.position);
                LogDebug($"❌ Player {clientId} NOT in shelf range!  Distance: {distance:F2}");
                return false;
            }

            // Item'ı kontrol et
            if (!itemRef.TryGet(out NetworkObject netObj) || netObj == null)
            {
                LogDebug("❌ Invalid item reference!");
                return false;
            }

            // BoxInfo kontrolü
            boxInfo = netObj.GetComponent<BoxInfo>();

            // Belirli kutu türü gerekiyorsa kontrol et
            if (requireSpecificBoxType && boxInfo != null && boxInfo.boxType != acceptedBoxType)
            {
                LogDebug($"❌ Wrong box type! Expected: {acceptedBoxType}, Got: {boxInfo.boxType}");
                return false;
            }

            return true;
        }

        private void PlaceItemInSlot(NetworkObjectReference itemRef, int slotIndex, ulong clientId, BoxInfo boxInfo)
        {
            _slotItems[slotIndex] = itemRef;
            _itemCount.Value++;

            if (itemRef.TryGet(out NetworkObject networkObj) && networkObj != null)
            {
                var item = networkObj.gameObject;

                // Slot varsa transform ayarla
                if (shelfSlots != null && slotIndex < shelfSlots.Length)
                {
                    var slot = shelfSlots[slotIndex];
                    item.transform.SetParent(slot);
                    item.transform.localPosition = Vector3.zero;
                    item.transform.localRotation = Quaternion.identity;
                }

                // Physics devre dışı
                DisableItemPhysics(item);

                string boxTypeStr = boxInfo != null ? boxInfo.boxType.ToString() : "Unknown";
                LogDebug($"✅ Item ({boxTypeStr}) placed on shelf by client {clientId} at slot {slotIndex}");
            }

            // TutorialManager'a bildir
            NotifyTutorialItemPlaced(boxInfo);
        }

        #endregion

        #region Server RPCs - Take Item

        /// <summary>
        /// Raftan item alır
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void TakeItemFromShelfServerRpc(ulong requesterClientId, ulong itemNetworkId, ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;

            LogDebug($"📥 TakeItemFromShelfServerRpc - Client {requesterClientId} wants item {itemNetworkId}");

            // Validation
            if (!ValidateTakeItemRequest(requesterClientId, itemNetworkId, out PlayerInventory playerInventory, out int slotIndex))
            {
                return;
            }

            // Item'ı al
            TakeItemFromSlot(slotIndex, playerInventory, requesterClientId);
        }

        private bool ValidateTakeItemRequest(ulong clientId, ulong itemNetworkId, out PlayerInventory playerInventory, out int slotIndex)
        {
            playerInventory = null;
            slotIndex = -1;

            // Player'ı bul
            if (!TryGetPlayerTransform(clientId, out Transform playerTransform))
            {
                LogDebug($"❌ Player object not found for client {clientId}");
                return false;
            }

            // Range kontrolü
            if (!IsPlayerInRange(playerTransform))
            {
                float distance = Vector3.Distance(playerTransform.position, transform.position);
                LogDebug($"❌ Player {clientId} NOT in shelf range! Distance: {distance:F2}");
                return false;
            }

            // PlayerInventory kontrolü
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
            {
                LogDebug($"❌ Client {clientId} not found");
                return false;
            }

            playerInventory = client.PlayerObject.GetComponent<PlayerInventory>();
            if (playerInventory == null)
            {
                LogDebug($"❌ PlayerInventory not found for client {clientId}");
                return false;
            }

            if (playerInventory.HasItem)
            {
                LogDebug($"⚠️ Player {clientId} already has an item");
                return false;
            }

            // Item'ı bul
            slotIndex = FindSlotIndexByNetworkId(itemNetworkId);
            if (slotIndex == -1)
            {
                LogDebug($"❌ Item {itemNetworkId} NOT found on shelf!");
                return false;
            }

            return true;
        }

        private void TakeItemFromSlot(int slotIndex, PlayerInventory playerInventory, ulong clientId)
        {
            if (!_slotItems[slotIndex].TryGet(out NetworkObject networkObj) || networkObj == null)
            {
                LogDebug($"❌ NetworkObject is null at slot {slotIndex}");
                return;
            }

            var worldItem = networkObj.GetComponent<NetworkWorldItem>();
            if (worldItem == null || worldItem.ItemData == null)
            {
                LogDebug($"❌ WorldItem or ItemData is null at slot {slotIndex}");
                return;
            }

            int itemID = worldItem.ItemData.itemID;
            LogDebug($"✅ Taking item from slot {slotIndex}, ItemID: {itemID}");

            // Slot'u temizle
            _slotItems[slotIndex] = new NetworkObjectReference();
            _itemCount.Value--;

            // Item'ı despawn et
            networkObj.Despawn();

            // Player'a item ver
            playerInventory.SetInventoryStateServerRpc(true, itemID);

            LogDebug($"✅ Item successfully given to player {clientId}");
        }

        #endregion

        #region Tutorial Notification

        private void NotifyTutorialItemPlaced(BoxInfo boxInfo)
        {
            if (TutorialManager.Instance == null)
            {
                LogDebug("TutorialManager. Instance is null - skipping notification");
                return;
            }

            // TutorialManager'a bildir
            TutorialManager.Instance.OnItemPlacedOnShelf();

            string boxTypeStr = boxInfo != null ? boxInfo.boxType.ToString() : "Unknown";
            LogDebug($"📚 Tutorial notified: {boxTypeStr} item placed on shelf");
        }

        #endregion

        #region Public API

        /// <summary>
        /// Rafı temizler
        /// </summary>
        public void ClearShelf()
        {
            if (!IsServer) return;

            for (int i = 0; i < _slotItems.Count; i++)
            {
                if (_slotItems[i].TryGet(out NetworkObject netObj) && netObj != null)
                {
                    netObj.Despawn();
                }
                _slotItems[i] = new NetworkObjectReference();
            }

            _itemCount.Value = 0;
            LogDebug("Shelf cleared");
        }

        /// <summary>
        /// Kabul edilen kutu türünü ayarlar
        /// </summary>
        public void SetAcceptedBoxType(BoxInfo.BoxType boxType, bool required = true)
        {
            acceptedBoxType = boxType;
            requireSpecificBoxType = required;
            LogDebug($"Accepted box type set to: {boxType} (Required: {required})");
        }

        /// <summary>
        /// Raftaki tüm item'ları döndürür
        /// </summary>
        public NetworkWorldItem[] GetAllShelfItems()
        {
            var items = new System.Collections.Generic.List<NetworkWorldItem>();

            for (int i = 0; i < _slotItems.Count; i++)
            {
                if (_slotItems[i].TryGet(out NetworkObject networkObj) && networkObj != null)
                {
                    var worldItem = networkObj.GetComponent<NetworkWorldItem>();
                    if (worldItem != null)
                    {
                        items.Add(worldItem);
                    }
                }
            }

            return items.ToArray();
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
        [ContextMenu("Debug: Print State")]
        private void DebugPrintState()
        {
            Debug.Log($"{LOG_PREFIX} === TUTORIAL SHELF STATE ===");
            Debug.Log($"Total Slots: {SlotCount}");
            Debug.Log($"Item Count: {_itemCount.Value}");
            Debug.Log($"Is Full: {IsFull}");
            Debug.Log($"Has Item: {HasItem}");
            Debug.Log($"Max Item Count: {maxItemCount}");
            Debug.Log($"Require Specific Box Type: {requireSpecificBoxType}");
            Debug.Log($"Accepted Box Type: {acceptedBoxType}");

            if (_slotItems == null) return;

            for (int i = 0; i < _slotItems.Count; i++)
            {
                string status = IsSlotOccupied(i)
                    ? (_slotItems[i].TryGet(out NetworkObject obj) ? obj.name : "Unknown")
                    : "Empty";

                Debug.Log($"  Slot {i}: {status}");
            }
        }

        [ContextMenu("Clear Shelf")]
        private void DebugClearShelf()
        {
            if (Application.isPlaying)
            {
                ClearShelf();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!showInteractionRange) return;

            DrawInteractionBoxGizmo();
            DrawSlotGizmos();
            DrawCenterPointGizmo();
        }

        private void DrawInteractionBoxGizmo()
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);

            var rotationMatrix = Matrix4x4.TRS(
                transform.position + transform.TransformDirection(interactionBoxOffset),
                transform.rotation,
                Vector3.one
            );

            Gizmos.matrix = rotationMatrix;
            Gizmos.DrawCube(Vector3.zero, interactionBoxSize);

            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(Vector3.zero, interactionBoxSize);

            Gizmos.matrix = Matrix4x4.identity;
        }

        private void DrawSlotGizmos()
        {
            if (shelfSlots == null) return;

            Gizmos.color = Color.yellow;

            foreach (var slot in shelfSlots)
            {
                if (slot == null) continue;

                Gizmos.DrawWireCube(slot.position, Vector3.one * SLOT_GIZMO_SIZE);
                Gizmos.DrawLine(transform.position, slot.position);
            }
        }

        private void DrawCenterPointGizmo()
        {
            Gizmos.color = Color.red;
            Vector3 centerPos = transform.position + transform.TransformDirection(interactionBoxOffset);
            Gizmos.DrawWireSphere(centerPos, CENTER_GIZMO_RADIUS);
        }
#endif

        #endregion
    }
}