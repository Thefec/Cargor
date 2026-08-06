using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace NewCss
{
    /// <summary>
    /// Raf durumunu ve item yerleştirme/alma işlemlerini yöneten network-aware sınıf. 
    /// Box collider tabanlı etkileşim alanı ve slot bazlı item yönetimi sağlar.
    /// </summary>
    public class ShelfState : NetworkBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[ShelfState]";
        private const float SLOT_GIZMO_SIZE = 0.2f;
        private const float CENTER_GIZMO_RADIUS = 0.1f;

        #endregion

        #region Serialized Fields

        [Header("=== SHELF CONFIGURATION ===")]
        [SerializeField, Tooltip("Raftaki item slotlarının transform'ları")]
        public Transform[] shelfSlots;

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

        #region Network Variables

        private NetworkList<NetworkObjectReference> _slotItems;

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
        /// Dolu slot sayısı
        /// </summary>
        public int OccupiedSlotCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _slotItems.Count; i++)
                {
                    if (IsSlotOccupied(i))
                    {
                        count++;
                    }
                }
                return count;
            }
        }

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
                Debug.LogWarning($"{LOG_PREFIX} No shelf slots configured!");
                return;
            }

            for (int i = 0; i < shelfSlots.Length; i++)
            {
                _slotItems.Add(new NetworkObjectReference());
            }

            Debug.Log($"{LOG_PREFIX} Initialized {shelfSlots.Length} slots");
        }

        private void SubscribeToNetworkEvents()
        {
            if (_slotItems != null)
            {
                _slotItems.OnListChanged += HandleSlotItemsChanged;
            }
        }

        private void UnsubscribeFromNetworkEvents()
        {
            if (_slotItems != null)
            {
                _slotItems.OnListChanged -= HandleSlotItemsChanged;
            }
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
            // Mevcut trigger collider'ı ara
            var boxColliders = GetComponents<BoxCollider>();

            foreach (var collider in boxColliders)
            {
                if (collider.isTrigger)
                {
                    return collider;
                }
            }

            // Bulunamadıysa yeni oluştur
            var newTrigger = gameObject.AddComponent<BoxCollider>();
            newTrigger.isTrigger = true;
            Debug.Log($"{LOG_PREFIX} Box interaction trigger created with size {interactionBoxSize}");

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
        /// Transform bazlı range kontrolü - HEM HOST HEM CLIENT için çalışır
        /// </summary>
        /// <param name="playerTransform">Kontrol edilecek oyuncu transform'u</param>
        /// <returns>Oyuncu etkileşim alanı içinde mi?</returns>
        public bool IsPlayerInRange(Transform playerTransform)
        {
            if (playerTransform == null) return false;

            // World position'ı local space'e çevir
            Vector3 localPoint = transform.InverseTransformPoint(playerTransform.position);
            Vector3 halfSize = interactionBoxSize * 0.5f;

            // Box içinde mi kontrol et
            bool isInBox = IsPointInsideBox(localPoint, interactionBoxOffset, halfSize);

            return isInBox;
        }

        /// <summary>
        /// ClientId'den Transform bulup range kontrolü yapar
        /// </summary>
        /// <param name="clientId">Kontrol edilecek client ID</param>
        /// <returns>Oyuncu etkileşim alanı içinde mi? </returns>
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
        /// Raf dolu mu kontrol eder
        /// </summary>
        public bool IsFull()
        {
            if (_slotItems == null || _slotItems.Count < shelfSlots.Length)
            {
                return false;
            }

            for (int i = 0; i < _slotItems.Count; i++)
            {
                if (!IsSlotOccupied(i))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Rafta herhangi bir item var mı kontrol eder
        /// </summary>
        public bool HasItem()
        {
            if (_slotItems == null) return false;

            for (int i = 0; i < _slotItems.Count; i++)
            {
                if (IsSlotOccupied(i))
                {
                    return true;
                }
            }

            return false;
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
        /// İlk boş slot index'ini bulur
        /// </summary>
        private int FindEmptySlotIndex()
        {
            int maxIndex = Mathf.Min(_slotItems.Count, shelfSlots.Length);

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

        /// <summary>
        /// Raftaki tüm item'ları döndürür (Mouse scroll için)
        /// </summary>
        public NetworkWorldItem[] GetAllShelfItems()
        {
            var items = new List<NetworkWorldItem>();

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

        #region Visual State Management

        private void HandleSlotItemsChanged(NetworkListEvent<NetworkObjectReference> changeEvent)
        {
            UpdateVisualState();
        }

        private void UpdateVisualState()
        {
            int maxIndex = Mathf.Min(_slotItems.Count, shelfSlots.Length);

            for (int i = 0; i < maxIndex; i++)
            {
                UpdateSlotVisual(i);
            }
        }

        private void UpdateSlotVisual(int slotIndex)
        {
            if (!_slotItems[slotIndex].TryGet(out NetworkObject networkObj) || networkObj == null)
            {
                return;
            }

            var item = networkObj.gameObject;
            var slot = shelfSlots[slotIndex];

            // Transform ayarları
            item.transform.SetParent(slot);
            item.transform.localPosition = Vector3.zero;
            item.transform.localRotation = Quaternion.identity;

            // Physics devre dışı bırak
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

        #region Client RPCs

        /// <summary>
        /// Tüm client'larda yerleştirme animasyonunu tetikler (büyükten küçüğe scale animasyonu).
        /// </summary>
        [ClientRpc]
        private void PlayPlacementAnimationClientRpc(ulong networkObjectId)
        {
            if (NetworkManager.Singleton?.SpawnManager == null) return;

            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(
                    networkObjectId, out NetworkObject netObj))
            {
                Debug.Log($"{LOG_PREFIX} ⚠️ PlayPlacementAnimation: NetworkObject {networkObjectId} not found on this client (race condition - safe to ignore)");
                return;
            }

            if (netObj == null || netObj.gameObject == null) return;

            ItemPlacementAnimator.PlayOn(this, netObj.gameObject);
        }

        #endregion

        #region Server RPCs - Place Item

        /// <summary>
        /// Server-side version that bypasses validation.
        /// Used when PlayerInventory has already performed validation.
        /// </summary>
        public void PlaceItemOnShelfFromServer(NetworkObjectReference itemRef, ulong requesterClientId)
        {
            if (!IsServer) return;

            Debug.Log($"{LOG_PREFIX} 📥 PlaceItemOnShelfFromServer - Client {requesterClientId}");

            // Box category validation - only allow Box category items on shelf
            if (!ValidateItemIsBox(itemRef))
            {
                Debug.LogWarning($"{LOG_PREFIX} ❌ Only Box category items can be placed on shelf!");
                return;
            }

            // Boş slot bul
            int slotIndex = FindEmptySlotIndex();
            if (slotIndex == -1)
            {
                Debug.LogWarning($"{LOG_PREFIX} ❌ Shelf is FULL!");
                return;
            }

            // Item'ı yerleştir
            PlaceItemInSlot(itemRef, slotIndex, requesterClientId);
        }

        /// <summary>
        /// Validates that the item is a Box category item.
        /// Only Box category items can be placed on shelves.
        /// </summary>
        private bool ValidateItemIsBox(NetworkObjectReference itemRef)
        {
            if (!itemRef.TryGet(out NetworkObject networkObj) || networkObj == null)
            {
                Debug.LogWarning($"{LOG_PREFIX} ❌ Invalid item reference for box validation!");
                return false;
            }

            // Check NetworkWorldItem for ItemData category
            var worldItem = networkObj.GetComponent<NetworkWorldItem>();
            if (worldItem != null && worldItem.ItemData != null)
            {
                if (worldItem.ItemData.itemCategory == ItemCategory.Box)
                {
                    Debug.Log($"{LOG_PREFIX} ✅ Item category is Box - allowed on shelf");
                    return true;
                }
                else
                {
                    Debug.LogWarning($"{LOG_PREFIX} ❌ Item category is {worldItem.ItemData.itemCategory} - NOT allowed on shelf (only Box category)");
                    return false;
                }
            }

            // Fallback: Check for BoxInfo component
            var boxInfo = networkObj.GetComponent<BoxInfo>();
            if (boxInfo != null)
            {
                Debug.Log($"{LOG_PREFIX} ✅ BoxInfo component found - allowed on shelf");
                return true;
            }

            Debug.LogWarning($"{LOG_PREFIX} ❌ No valid category information found - NOT allowed on shelf");
            return false;
        }

        private bool ValidatePlaceItemRequest(ulong clientId, out Transform playerTransform)
        {
            playerTransform = null;

            // Player'ı bul
            if (!TryGetPlayerTransform(clientId, out playerTransform))
            {
                Debug.LogWarning($"{LOG_PREFIX} ❌ Player object not found for client {clientId}");
                return false;
            }

            // Range kontrolü
            if (!IsPlayerInRange(playerTransform))
            {
                float distance = Vector3.Distance(playerTransform.position, transform.position);
                Debug.LogWarning($"{LOG_PREFIX} ❌ Player {clientId} NOT in shelf range!  Distance: {distance:F2}");
                return false;
            }

            return true;
        }

        private void PlaceItemInSlot(NetworkObjectReference itemRef, int slotIndex, ulong clientId)
        {
            _slotItems[slotIndex] = itemRef;

            if (itemRef.TryGet(out NetworkObject networkObj) && networkObj != null)
            {
                var item = networkObj.gameObject;
                var slot = shelfSlots[slotIndex];

                // Transform ayarla
                item.transform.SetParent(slot);
                item.transform.localPosition = Vector3.zero;
                item.transform.localRotation = Quaternion.identity;

                // Physics devre dışı
                DisableItemPhysics(item);

                Debug.Log($"{LOG_PREFIX} ✅ Item placed on shelf by client {clientId} at slot {slotIndex}");

                // Yerleştirme animasyonunu tüm clientlarda tetikle
                PlayPlacementAnimationClientRpc(networkObj.NetworkObjectId);

                // Notify quest system if this is a box.
                // Her kutu YALNIZCA BİR KEZ sayılır: dedup olmadan "rafa koy → geri al →
                // tekrar koy" döngüsü tek kutuyla en yüksek quest hedefini bile saniyeler
                // içinde tamamlıyordu (raf tipi görevler = 30 assetin 13'ü).
                var boxInfo = item.GetComponent<BoxInfo>();
                if (boxInfo != null && !boxInfo.countedForShelfQuest)
                {
                    boxInfo.countedForShelfQuest = true;
                    Quest.QuestTracker.NotifyBoxPlacedOnShelf(boxInfo.boxType);
                }
            }
        }

        #endregion

        #region Server RPCs - Take Item

        /// <summary>
        /// Raftan item alır (RequireOwnership = false - tüm client'lar çağırabilir)
        /// </summary>
        public void TakeItemFromShelfServer(ulong requesterClientId, ulong itemNetworkId)
        {
            if (!IsServer) return;

            Debug.Log($"{LOG_PREFIX} 📥 TakeItemFromShelfServerRpc - Client {requesterClientId} wants item {itemNetworkId}");

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
                Debug.LogError($"{LOG_PREFIX} ❌ Player object not found for client {clientId}");
                return false;
            }

            // Range kontrolü
            if (!IsPlayerInRange(playerTransform))
            {
                float distance = Vector3.Distance(playerTransform.position, transform.position);
                Debug.LogWarning($"{LOG_PREFIX} ❌ Player {clientId} NOT in shelf range! Distance: {distance:F2}");
                return false;
            }

            // PlayerInventory kontrolü
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
            {
                Debug.LogError($"{LOG_PREFIX} ❌ Client {clientId} not found");
                return false;
            }

            playerInventory = client.PlayerObject.GetComponent<PlayerInventory>();
            if (playerInventory == null)
            {
                Debug.LogError($"{LOG_PREFIX} ❌ PlayerInventory not found for client {clientId}");
                return false;
            }

            if (playerInventory.HasItem)
            {
                Debug.Log($"{LOG_PREFIX} ⚠️ Player {clientId} already has an item");
                return false;
            }

            // Item'ı bul
            slotIndex = FindSlotIndexByNetworkId(itemNetworkId);
            if (slotIndex == -1)
            {
                Debug.LogError($"{LOG_PREFIX} ❌ Item {itemNetworkId} NOT found on shelf!");
                return false;
            }

            return true;
        }

        private void TakeItemFromSlot(int slotIndex, PlayerInventory playerInventory, ulong clientId)
        {
            if (!_slotItems[slotIndex].TryGet(out NetworkObject networkObj) || networkObj == null)
            {
                Debug.LogError($"{LOG_PREFIX} ❌ NetworkObject is null at slot {slotIndex}");
                return;
            }

            var worldItem = networkObj.GetComponent<NetworkWorldItem>();
            if (worldItem == null || worldItem.ItemData == null)
            {
                Debug.LogError($"{LOG_PREFIX} ❌ WorldItem or ItemData is null at slot {slotIndex}");
                return;
            }

            int itemID = worldItem.ItemData.itemID;
            Debug.Log($"{LOG_PREFIX} ✅ Taking item from slot {slotIndex}, ItemID: {itemID}");

            // Slot'u temizle
            _slotItems[slotIndex] = new NetworkObjectReference();

            // Item'ı despawn et
            networkObj.Despawn();

            // Player'a item ver
            playerInventory.SetInventoryStateServer(true, itemID);

            Debug.Log($"{LOG_PREFIX} ✅ Item successfully given to player {clientId}");
        }

        #endregion

        #region Debug & Editor

        [ContextMenu("Debug Slot States")]
        private void DebugSlotStates()
        {
            Debug.Log($"{LOG_PREFIX} === SHELF DEBUG ===");
            Debug.Log($"Total Slots: {shelfSlots?.Length ?? 0}");
            Debug.Log($"Network List Count: {_slotItems?.Count ?? 0}");
            Debug.Log($"Occupied Slots: {OccupiedSlotCount}");
            Debug.Log($"Is Full: {IsFull()}");
            Debug.Log($"Has Item: {HasItem()}");

            if (_slotItems == null) return;

            for (int i = 0; i < _slotItems.Count; i++)
            {
                string status = IsSlotOccupied(i)
                    ? (_slotItems[i].TryGet(out NetworkObject obj) ? obj.name : "Unknown")
                    : "Empty";

                Debug.Log($"  Slot {i}: {status}");
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!showInteractionRange) return;

            DrawInteractionBoxGizmo();
            DrawSlotGizmos();
            DrawCenterPointGizmo();
        }

        private void DrawInteractionBoxGizmo()
        {
            // Yarı saydam dolgu
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);

            var rotationMatrix = Matrix4x4.TRS(
                transform.position + transform.TransformDirection(interactionBoxOffset),
                transform.rotation,
                Vector3.one
            );

            Gizmos.matrix = rotationMatrix;
            Gizmos.DrawCube(Vector3.zero, interactionBoxSize);

            // Çerçeve
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