using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

namespace NewCss
{
    public class ShelfState : NetworkBehaviour
    {
        [Header("Shelf Configuration")]
        public Transform[] shelfSlots;

        [Header("Interaction Settings")]
        [SerializeField] private Vector3 interactionBoxSize = new Vector3(3f, 2f, 2f);
        [SerializeField] private Vector3 interactionBoxOffset = Vector3.zero;
        [SerializeField] private LayerMask playerLayer;
        [SerializeField] private bool showInteractionRange = true;

        private NetworkList<NetworkObjectReference> _slotItems;
        private BoxCollider interactionTrigger;

        private void Awake()
        {
            _slotItems = new NetworkList<NetworkObjectReference>();
        }

        private void Start()
        {
            SetupInteractionTrigger();
        }

        private void SetupInteractionTrigger()
        {
            BoxCollider[] boxColliders = GetComponents<BoxCollider>();
            bool hasTrigger = false;

            foreach (BoxCollider col in boxColliders)
            {
                if (col.isTrigger)
                {
                    hasTrigger = true;
                    interactionTrigger = col;
                    break;
                }
            }

            if (!hasTrigger)
            {
                interactionTrigger = gameObject.AddComponent<BoxCollider>();
                interactionTrigger.isTrigger = true;
                interactionTrigger.size = interactionBoxSize;
                interactionTrigger.center = interactionBoxOffset;
                Debug.Log($"ShelfState: Box interaction trigger added with size {interactionBoxSize}");
            }
            else
            {
                interactionTrigger.size = interactionBoxSize;
                interactionTrigger.center = interactionBoxOffset;
            }
        }

        private void OnValidate()
        {
            if (Application.isPlaying && interactionTrigger != null)
            {
                interactionTrigger.size = interactionBoxSize;
                interactionTrigger.center = interactionBoxOffset;
            }
        }

        /// <summary>
        /// ✅ Transform bazlı range kontrolü - HEM HOST HEM CLIENT için çalışır
        /// </summary>
        public bool IsPlayerInRange(Transform playerTransform)
        {
            if (playerTransform == null) return false;

            Vector3 localPoint = transform.InverseTransformPoint(playerTransform.position);
            Vector3 halfSize = interactionBoxSize * 0.5f;
            Vector3 offset = interactionBoxOffset;

            bool inBox = Mathf.Abs(localPoint.x - offset.x) <= halfSize.x &&
                         Mathf.Abs(localPoint.y - offset.y) <= halfSize.y &&
                         Mathf.Abs(localPoint.z - offset.z) <= halfSize.z;

            return inBox;
        }

        /// <summary>
        /// ✅ ClientId'den Transform bulup kontrol et
        /// </summary>
        public bool IsPlayerInRange(ulong clientId)
        {
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client) ||
                client.PlayerObject == null)
            {
                return false;
            }

            return IsPlayerInRange(client.PlayerObject.transform);
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                for (int i = 0; i < shelfSlots.Length; i++)
                {
                    _slotItems.Add(new NetworkObjectReference());
                }
            }

            _slotItems.OnListChanged += OnSlotItemsChanged;
        }

        public override void OnNetworkDespawn()
        {
            if (_slotItems != null)
            {
                _slotItems.OnListChanged -= OnSlotItemsChanged;
            }
            base.OnNetworkDespawn();
        }

        private void OnSlotItemsChanged(NetworkListEvent<NetworkObjectReference> changeEvent)
        {
            UpdateVisualState();
        }

        private void UpdateVisualState()
        {
            for (int i = 0; i < _slotItems.Count && i < shelfSlots.Length; i++)
            {
                if (_slotItems[i].TryGet(out NetworkObject networkObj) && networkObj != null)
                {
                    GameObject item = networkObj.gameObject;
                    item.transform.SetParent(shelfSlots[i]);
                    item.transform.localPosition = Vector3.zero;
                    item.transform.localRotation = Quaternion.identity;

                    var rb = item.GetComponent<Rigidbody>();
                    if (rb != null) rb.isKinematic = true;

                    var col = item.GetComponent<Collider>();
                    if (col != null) col.enabled = false;
                }
            }
        }

        public bool IsFull()
        {
            if (_slotItems.Count < shelfSlots.Length) return false;

            for (int i = 0; i < _slotItems.Count; i++)
            {
                if (!_slotItems[i].TryGet(out NetworkObject networkObj) || networkObj == null)
                    return false;
            }
            return true;
        }

        private int FindEmptySlotIndex()
        {
            for (int i = 0; i < _slotItems.Count && i < shelfSlots.Length; i++)
            {
                if (!_slotItems[i].TryGet(out NetworkObject networkObj) || networkObj == null)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// ✅ PLACE ITEM - RequireOwnership = false + doğru range kontrolü
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void PlaceItemOnShelfServerRpc(NetworkObjectReference itemRef, ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;

            ulong requesterClientId = rpcParams.Receive.SenderClientId;

            Debug.Log($"📥 SERVER: PlaceItemOnShelfServerRpc - Client {requesterClientId}");

            // ✅ Player'ı bul
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(requesterClientId, out var client) ||
                client.PlayerObject == null)
            {
                Debug.LogWarning($"❌ SERVER: Player object not found for client {requesterClientId}");
                return;
            }

            Transform playerTransform = client.PlayerObject.transform;

            // ✅ Range kontrolü
            if (!IsPlayerInRange(playerTransform))
            {
                Debug.LogWarning($"❌ SERVER: Player {requesterClientId} NOT in shelf range! Distance: {Vector3.Distance(playerTransform.position, transform.position):F2}");
                return;
            }

            // ✅ Boş slot bul
            int slotIndex = FindEmptySlotIndex();
            if (slotIndex == -1)
            {
                Debug.LogWarning($"❌ SERVER: Shelf is FULL!");
                return;
            }

            // ✅ Item'ı slot'a yerleştir
            _slotItems[slotIndex] = itemRef;

            if (itemRef.TryGet(out NetworkObject networkObj) && networkObj != null)
            {
                GameObject item = networkObj.gameObject;
                item.transform.SetParent(shelfSlots[slotIndex]);
                item.transform.localPosition = Vector3.zero;
                item.transform.localRotation = Quaternion.identity;

                var rb = item.GetComponent<Rigidbody>();
                if (rb != null) rb.isKinematic = true;

                var col = item.GetComponent<Collider>();
                if (col != null) col.enabled = false;

                Debug.Log($"✅ SERVER: Item placed on shelf by client {requesterClientId} at slot {slotIndex}");
            }
        }

        /// <summary>
        /// ✅ TAKE ITEM - RequireOwnership = false + doğru range kontrolü
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void TakeItemFromShelfServerRpc(ulong requesterClientId, ulong itemNetworkId, ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;

            Debug.Log($"📥 SERVER: TakeItemFromShelfServerRpc - Client {requesterClientId} wants item {itemNetworkId}");

            // ✅ Player'ı bul
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(requesterClientId, out var client) ||
                client.PlayerObject == null)
            {
                Debug.LogError($"❌ SERVER: Player object not found for client {requesterClientId}");
                return;
            }

            Transform playerTransform = client.PlayerObject.transform;

            // ✅ Range kontrolü
            if (!IsPlayerInRange(playerTransform))
            {
                Debug.LogWarning($"❌ SERVER: Player {requesterClientId} NOT in shelf range! Distance: {Vector3.Distance(playerTransform.position, transform.position):F2}");
                return;
            }

            // ✅ PlayerInventory kontrolü
            var playerInventory = client.PlayerObject.GetComponent<PlayerInventory>();
            if (playerInventory == null)
            {
                Debug.LogError($"❌ SERVER: PlayerInventory not found for client {requesterClientId}");
                return;
            }

            if (playerInventory.HasItem)
            {
                Debug.Log($"⚠️ SERVER: Player {requesterClientId} already has an item");
                return;
            }

            // ✅ Item'ı bul
            int targetSlotIndex = -1;
            for (int i = 0; i < _slotItems.Count; i++)
            {
                if (_slotItems[i].TryGet(out NetworkObject networkObj) &&
                    networkObj != null &&
                    networkObj.NetworkObjectId == itemNetworkId)
                {
                    targetSlotIndex = i;
                    break;
                }
            }

            if (targetSlotIndex == -1)
            {
                Debug.LogError($"❌ SERVER: Item {itemNetworkId} NOT found on shelf!");
                return;
            }

            // ✅ Item'ı al
            if (_slotItems[targetSlotIndex].TryGet(out NetworkObject targetNetworkObj) && targetNetworkObj != null)
            {
                NetworkWorldItem worldItem = targetNetworkObj.GetComponent<NetworkWorldItem>();
                if (worldItem == null || worldItem.ItemData == null)
                {
                    Debug.LogError($"❌ SERVER: WorldItem or ItemData is null in slot {targetSlotIndex}!");
                    return;
                }

                int itemID = worldItem.ItemData.itemID;
                Debug.Log($"✅ SERVER: Taking item from shelf slot {targetSlotIndex}, ItemID: {itemID}");

                // ✅ Slot'u temizle
                _slotItems[targetSlotIndex] = new NetworkObjectReference();

                // ✅ Item'ı despawn et
                targetNetworkObj.Despawn();

                // ✅ Player'a item ver
                playerInventory.SetInventoryStateServerRpc(true, itemID);

                Debug.Log($"✅ SERVER: Item successfully given to player {requesterClientId}");
            }
        }

        /// <summary>
        /// ✅ Raftaki tüm itemları döndür (Mouse scroll için)
        /// </summary>
        public NetworkWorldItem[] GetAllShelfItems()
        {
            List<NetworkWorldItem> items = new List<NetworkWorldItem>();

            for (int i = 0; i < _slotItems.Count; i++)
            {
                if (_slotItems[i].TryGet(out NetworkObject networkObj) && networkObj != null)
                {
                    NetworkWorldItem worldItem = networkObj.GetComponent<NetworkWorldItem>();
                    if (worldItem != null)
                    {
                        items.Add(worldItem);
                    }
                }
            }

            return items.ToArray();
        }

        public bool HasItem()
        {
            for (int i = 0; i < _slotItems.Count; i++)
            {
                if (_slotItems[i].TryGet(out NetworkObject networkObj) && networkObj != null)
                    return true;
            }
            return false;
        }

        [ContextMenu("Debug Slot States")]
        private void DebugSlotStates()
        {
            Debug.Log($"=== SHELF DEBUG ===");
            Debug.Log($"Total Slots: {shelfSlots.Length}");
            Debug.Log($"Network List Count: {_slotItems.Count}");

            for (int i = 0; i < _slotItems.Count; i++)
            {
                bool hasItem = _slotItems[i].TryGet(out NetworkObject networkObj) && networkObj != null;
                Debug.Log($"Slot {i}: {(hasItem ? networkObj.name : "Empty")}");
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!showInteractionRange) return;

            // ✅ Box range gösterimi
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            Matrix4x4 rotationMatrix = Matrix4x4.TRS(
                transform.position + transform.TransformDirection(interactionBoxOffset),
                transform.rotation,
                Vector3.one
            );
            Gizmos.matrix = rotationMatrix;
            Gizmos.DrawCube(Vector3.zero, interactionBoxSize);

            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(Vector3.zero, interactionBoxSize);

            Gizmos.matrix = Matrix4x4.identity;

            // ✅ Slot pozisyonları
            if (shelfSlots != null)
            {
                Gizmos.color = Color.yellow;
                foreach (Transform slot in shelfSlots)
                {
                    if (slot != null)
                    {
                        Gizmos.DrawWireCube(slot.position, Vector3.one * 0.2f);
                        Gizmos.DrawLine(transform.position, slot.position);
                    }
                }
            }

            // ✅ Merkez nokta
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position + transform.TransformDirection(interactionBoxOffset), 0.1f);
        }
    }
}