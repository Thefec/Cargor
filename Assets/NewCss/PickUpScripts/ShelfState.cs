using Unity.Netcode;
using UnityEngine;

namespace NewCss
{
    public class ShelfState : NetworkBehaviour
    {
        [Header("Shelf Configuration")]
        public Transform[] shelfSlots; // Raf üzerindeki boş noktalar

        // NetworkVariable ile client-server sync
        private NetworkList<NetworkObjectReference> _slotItems;

        private void Awake()
        {
            _slotItems = new NetworkList<NetworkObjectReference>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // Slot sayısını başlat
            if (IsServer)
            {
                // Slot sayısını shelfSlots array'ine göre ayarla
                for (int i = 0; i < shelfSlots.Length; i++)
                {
                    _slotItems.Add(new NetworkObjectReference());
                }
            }

            // Client'larda değişiklikleri dinle
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

        // Network list değişikliklerini handle et
        private void OnSlotItemsChanged(NetworkListEvent<NetworkObjectReference> changeEvent)
        {
            if (IsClient && !IsServer) // Sadece client'larda çalışsın
            {
                UpdateVisualState();
            }
        }

        // Görsel durumu güncelle (client-side)
        private void UpdateVisualState()
        {
            for (int i = 0; i < _slotItems.Count && i < shelfSlots.Length; i++)
            {
                if (_slotItems[i].TryGet(out NetworkObject networkObj) && networkObj != null)
                {
                    // Item varsa slot pozisyonuna yerleştir
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

        // Rafın tamamen dolu olup olmadığını kontrol eder
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

        // En yakın boş slotu bulur
        private int FindEmptySlotIndex()
        {
            for (int i = 0; i < _slotItems.Count && i < shelfSlots.Length; i++)
            {
                if (!_slotItems[i].TryGet(out NetworkObject networkObj) || networkObj == null)
                    return i;
            }
            return -1;
        }

        // Item rafta bir slota yerleştirilir (Server RPC)
        [ServerRpc(RequireOwnership = false)]
        public void PlaceItemOnShelfServerRpc(NetworkObjectReference itemRef)
        {
            if (!IsServer) return;

            int slotIndex = FindEmptySlotIndex();
            if (slotIndex == -1) return; // Yer yok

            // Network list'i güncelle
            _slotItems[slotIndex] = itemRef;

            // Server'da görsel güncellemeyi yap
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

                // Client'lara bilgi gönder
                PlaceItemClientRpc(itemRef, slotIndex);
            }
        }

        // Client'lara item yerleştirme bilgisi gönder
        [ClientRpc]
        private void PlaceItemClientRpc(NetworkObjectReference itemRef, int slotIndex)
        {
            if (IsServer) return; // Server'da zaten yapıldı

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

        // En yakın slottaki ürünü raftan alır (Server RPC)
        [ServerRpc(RequireOwnership = false)]
        public void TakeItemFromShelfServerRpc(ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;

            for (int i = 0; i < _slotItems.Count; i++)
            {
                if (_slotItems[i].TryGet(out NetworkObject networkObj) && networkObj != null)
                {
                    NetworkObjectReference itemRef = _slotItems[i];

                    // Slot'u temizle
                    _slotItems[i] = new NetworkObjectReference();

                    // Item'ı serbest bırak
                    GameObject item = networkObj.gameObject;
                    item.transform.SetParent(null);

                    var rb = item.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.isKinematic = false;
                        rb.useGravity = true;
                    }

                    var col = item.GetComponent<Collider>();
                    if (col != null) col.enabled = true;

                    // Item'ı pickup edilebilir hale getir
                    NetworkWorldItem worldItem = item.GetComponent<NetworkWorldItem>();
                    if (worldItem != null)
                    {
                        worldItem.EnablePickup();
                    }

                    // Client'lara bilgi gönder
                    TakeItemClientRpc(itemRef, i, rpcParams.Receive.SenderClientId);

                    // Item'ı requester player'ına otomatik olarak ver
                    GiveItemToPlayerServerRpc(itemRef, rpcParams.Receive.SenderClientId);
                    return;
                }
            }
        }

        // Yeni method: Item'ı player'a otomatik olarak ver
        [ServerRpc(RequireOwnership = false)]
        private void GiveItemToPlayerServerRpc(NetworkObjectReference itemRef, ulong playerClientId)
        {
            if (!IsServer) return;

            if (!itemRef.TryGet(out NetworkObject itemNetworkObj) || itemNetworkObj == null) return;

            // 1. Ownership'i isteyene ver
            itemNetworkObj.ChangeOwnership(playerClientId);

            // 2. Hedef client'a sadece ona pickup yaptıracak ClientRpc gönder
            var clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { playerClientId }
                }
            };

            GiveItemToRequesterClientRpc(itemRef, clientRpcParams);
        }

        [ClientRpc]
        private void GiveItemToRequesterClientRpc(NetworkObjectReference itemRef, ClientRpcParams clientRpcParams = default)
        {
            // Bu method sadece hedef client'ta çalışacak
            if (!itemRef.TryGet(out NetworkObject itemNetworkObj) || itemNetworkObj == null) return;

            // Local player'ın PlayerObject'ini al
            var localPlayerObj = NetworkManager.Singleton.LocalClient.PlayerObject;
            if (localPlayerObj == null) return;

            var playerInventory = localPlayerObj.GetComponent<PlayerInventory>();
            if (playerInventory == null) return;

            // PlayerInventory içindeki PickupItemFromTable'ın "client-side local pickup" yapacak şekilde olması gerekiyor.
            // (Örn: transform parent -> player's hand, collider kapat, rb kinematic vb. işlerini burada yapın)
            playerInventory.PickupItemFromTable(itemNetworkObj, null);
        }

        private System.Collections.IEnumerator DelayedGiveToPlayer(PlayerInventory playerInventory, NetworkObject itemNetworkObj)
        {
            yield return new WaitForSeconds(0.1f);

            if (playerInventory != null && itemNetworkObj != null)
            {
                playerInventory.PickupItemFromTable(itemNetworkObj, null);
            }
        }

        // Client'lara item alma bilgisi gönder
        [ClientRpc]
        private void TakeItemClientRpc(NetworkObjectReference itemRef, int slotIndex, ulong requesterClientId)
        {
            if (IsServer) return; // Server'da zaten yapıldı

            if (itemRef.TryGet(out NetworkObject networkObj) && networkObj != null)
            {
                GameObject item = networkObj.gameObject;
                item.transform.SetParent(null);

                var rb = item.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.useGravity = true;
                }

                var col = item.GetComponent<Collider>();
                if (col != null) col.enabled = true;
            }
        }

        // Slotların item'ı olup olmadığını kontrol eder
        public bool HasItem()
        {
            for (int i = 0; i < _slotItems.Count; i++)
            {
                if (_slotItems[i].TryGet(out NetworkObject networkObj) && networkObj != null)
                    return true;
            }
            return false;
        }

        // Public wrapper metodlar (client'ların çağırabileceği)
        public void PlaceItem(GameObject item)
        {
            var networkObj = item.GetComponent<NetworkObject>();
            if (networkObj != null)
            {
                PlaceItemOnShelfServerRpc(networkObj);
            }
        }

        public void TakeItem()
        {
            TakeItemFromShelfServerRpc();
        }

        // Debug için slot durumlarını göster
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