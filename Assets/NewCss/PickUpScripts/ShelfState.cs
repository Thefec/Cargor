using Unity.Netcode;
using UnityEngine;

namespace NewCss
{
    public class ShelfState : NetworkBehaviour
    {
        [Header("Shelf Configuration")]
        public Transform[] shelfSlots;

        private NetworkList<NetworkObjectReference> _slotItems;

        private void Awake()
        {
            _slotItems = new NetworkList<NetworkObjectReference>();
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
            if (IsClient && !IsServer)
            {
                UpdateVisualState();
            }
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
                    item.transform.rotation = Quaternion.identity;

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

        [ServerRpc(RequireOwnership = false)]
        public void PlaceItemOnShelfServerRpc(NetworkObjectReference itemRef)
        {
            if (!IsServer) return;

            int slotIndex = FindEmptySlotIndex();
            if (slotIndex == -1) return;

            _slotItems[slotIndex] = itemRef;

            if (itemRef.TryGet(out NetworkObject networkObj) && networkObj != null)
            {
                GameObject item = networkObj.gameObject;
                item.transform.SetParent(shelfSlots[slotIndex]);
                item.transform.localPosition = Vector3.zero;
                item.transform.rotation = Quaternion.identity;

                var rb = item.GetComponent<Rigidbody>();
                if (rb != null) rb.isKinematic = true;

                var col = item.GetComponent<Collider>();
                if (col != null) col.enabled = false;

                PlaceItemClientRpc(itemRef, slotIndex);
            }
        }

        [ClientRpc]
        private void PlaceItemClientRpc(NetworkObjectReference itemRef, int slotIndex)
        {
            if (IsServer) return;

            if (itemRef.TryGet(out NetworkObject networkObj) && networkObj != null)
            {
                GameObject item = networkObj.gameObject;
                item.transform.SetParent(shelfSlots[slotIndex]);
                item.transform.localPosition = Vector3.zero;
                item.transform.rotation = Quaternion.identity;

                var rb = item.GetComponent<Rigidbody>();
                if (rb != null) rb.isKinematic = true;

                var col = item.GetComponent<Collider>();
                if (col != null) col.enabled = false;
            }
        }

        // DÜZELTME: requesterClientId parametresi eklendi
        [ServerRpc(RequireOwnership = false)]
        public void TakeItemFromShelfServerRpc(ulong requesterClientId, ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;

            Debug.Log($"TakeItemFromShelfServerRpc called for client {requesterClientId}");

            try
            {
                // Player'ı ve inventory'yi bul
                if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(requesterClientId, out var client) ||
                    client.PlayerObject == null)
                {
                    Debug.LogError($"Player object not found for client: {requesterClientId}");
                    return;
                }

                var playerInventory = client.PlayerObject.GetComponent<PlayerInventory>();
                if (playerInventory == null)
                {
                    Debug.LogError($"PlayerInventory not found for client: {requesterClientId}");
                    return;
                }

                // Player'ın zaten item'ı varsa işlemi iptal et
                if (playerInventory.HasItem)
                {
                    Debug.Log($"Player {requesterClientId} already has an item, cannot take from shelf");
                    return;
                }

                // İlk dolu slotu bul ve item'ı al
                for (int i = 0; i < _slotItems.Count; i++)
                {
                    if (_slotItems[i].TryGet(out NetworkObject networkObj) && networkObj != null)
                    {
                        NetworkWorldItem worldItem = networkObj.GetComponent<NetworkWorldItem>();
                        if (worldItem == null || worldItem.ItemData == null)
                        {
                            Debug.LogError($"WorldItem or ItemData is null in slot {i}!");
                            continue;
                        }

                        int itemID = worldItem.ItemData.itemID;
                        Debug.Log($"Taking item from shelf slot {i}, ItemID: {itemID}, giving to client: {requesterClientId}");

                        // Slot'u temizle
                        _slotItems[i] = new NetworkObjectReference();

                        // Item'ı despawn et
                        networkObj.Despawn();

                        // Item'ı player'a ver
                        playerInventory.SetInventoryStateServerRpc(true, itemID);

                        // Tüm client'lara bildir
                        TakeItemFromShelfClientRpc(requesterClientId, itemID);

                        Debug.Log($"Item successfully given to player {requesterClientId}");
                        return;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error in TakeItemFromShelfServerRpc: {e.Message}");
            }

            Debug.Log("No items found on shelf");
        }


        [ClientRpc]
        private void TakeItemFromShelfClientRpc(ulong targetPlayerClientId, int itemID)
        {
            // Her client kendi player'ını kontrol eder
            if (NetworkManager.Singleton.LocalClientId == targetPlayerClientId)
            {
                Debug.Log($"Client {targetPlayerClientId} received item pickup confirmation");
            }
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

        public void PlaceItem(GameObject item)
        {
            var networkObj = item.GetComponent<NetworkObject>();
            if (networkObj != null)
            {
                PlaceItemOnShelfServerRpc(networkObj);
            }
        }

        // DÜZELTME: Bu metod artık kullanılmamalı, TakeItem(ulong clientId) kullanılmalı
        [System.Obsolete("Use TakeItem(ulong clientId) instead")]
        public void TakeItem()
        {
            Debug.LogWarning("TakeItem() without clientId is deprecated! Use TakeItem(ulong clientId)");
        }

        // YENİ: Client ID parametreli versiyon
        public void TakeItem(ulong clientId)
        {
            TakeItemFromShelfServerRpc(clientId);
        }

        [ContextMenu("Debug Slot States")]
        private void DebugSlotStates()
        {
            for (int i = 0; i < _slotItems.Count; i++)
            {
                bool hasItem = _slotItems[i].TryGet(out NetworkObject networkObj) && networkObj != null;
                Debug.Log($"Slot {i}: {(hasItem ? networkObj.name : "Empty")}");
            }
        }
    }
}