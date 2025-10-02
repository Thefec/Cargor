using Unity.Netcode;
using UnityEngine;

namespace NewCss
{
    public class NetworkedShelf : NetworkBehaviour
    {
        [Header("Item Data")] public ItemData redBoxItemData;
        public ItemData blueBoxItemData;
        public ItemData yellowBoxItemData;

        [Header("Box Slots (Positions on the shelf)")]
        public Transform redBoxSlot;

        public Transform blueBoxSlot;
        public Transform yellowBoxSlot;

        [Header("Respawn Settings")] [SerializeField]
        private float respawnDelay = 1f; // Respawn gecikmesi

        [SerializeField] private bool enableAutoRespawn = true; // Otomatik respawn açık/kapalı

        // Her slot için network object referanslarını tutuyoruz
        private NetworkVariable<ulong> redBoxNetworkId = new NetworkVariable<ulong>(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private NetworkVariable<ulong> blueBoxNetworkId = new NetworkVariable<ulong>(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private NetworkVariable<ulong> yellowBoxNetworkId = new NetworkVariable<ulong>(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // Local referanslar (performans için)
        private NetworkObject redBoxObject;
        private NetworkObject blueBoxObject;
        private NetworkObject yellowBoxObject;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                // Server başladığında tüm slotları kontrol et ve gerekirse spawn et
                CheckAndSpawnBox(redBoxSlot, redBoxItemData, BoxType.Red);
                CheckAndSpawnBox(blueBoxSlot, blueBoxItemData, BoxType.Blue);
                CheckAndSpawnBox(yellowBoxSlot, yellowBoxItemData, BoxType.Yellow);
            }

            // Network variable değişikliklerini dinle
            redBoxNetworkId.OnValueChanged += OnRedBoxChanged;
            blueBoxNetworkId.OnValueChanged += OnBlueBoxChanged;
            yellowBoxNetworkId.OnValueChanged += OnYellowBoxChanged;
        }

        public override void OnNetworkDespawn()
        {
            // Event'leri temizle
            redBoxNetworkId.OnValueChanged -= OnRedBoxChanged;
            blueBoxNetworkId.OnValueChanged -= OnBlueBoxChanged;
            yellowBoxNetworkId.OnValueChanged -= OnYellowBoxChanged;
        }

        private void Update()
        {
            if (!CanPerformNetworkOperations() || !enableAutoRespawn) return;

            // Sadece server'da çalış ve otomatik respawn aktifse
            CheckAndRespawnIfNeeded();
        }

        private bool IsNetworkActive()
        {
            return NetworkManager.Singleton != null &&
                   (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient) &&
                   NetworkManager.Singleton.IsListening;
        }


        private void CheckAndRespawnIfNeeded()
        {
            if (!CanPerformNetworkOperations()) return;

            // Her slotu kontrol et, eğer boşsa respawn et
            if (!IsSlotOccupied(redBoxSlot, redBoxObject))
            {
                redBoxObject = null;
                redBoxNetworkId.Value = 0;
                Invoke(nameof(RespawnRedBox), respawnDelay);
            }

            if (!IsSlotOccupied(blueBoxSlot, blueBoxObject))
            {
                blueBoxObject = null;
                blueBoxNetworkId.Value = 0;
                Invoke(nameof(RespawnBlueBox), respawnDelay);
            }

            if (!IsSlotOccupied(yellowBoxSlot, yellowBoxObject))
            {
                yellowBoxObject = null;
                yellowBoxNetworkId.Value = 0;
                Invoke(nameof(RespawnYellowBox), respawnDelay);
            }
        }

        private bool IsSlotOccupied(Transform slot, NetworkObject networkObject)
        {
            // Network object hala geçerli mi kontrol et
            if (networkObject != null && networkObject.IsSpawned)
            {
                // Obje hala slota yakın mı kontrol et
                float distance = Vector3.Distance(networkObject.transform.position, slot.position);
                return distance < 0.5f; // 0.5 unit tolerans
            }

            // Slot içinde başka bir NetworkWorldItem var mı kontrol et
            Collider[] colliders = Physics.OverlapSphere(slot.position, 0.3f);
            foreach (var collider in colliders)
            {
                NetworkWorldItem worldItem = collider.GetComponent<NetworkWorldItem>();
                if (worldItem != null && worldItem.NetworkObject != null && worldItem.NetworkObject.IsSpawned)
                {
                    return true;
                }
            }

            return false;
        }

        private void RespawnRedBox()
        {
            if (!CanPerformNetworkOperations())
            {
                Debug.LogWarning("Cannot respawn red box - network not ready");
                return;
            }

            if (!IsSlotOccupied(redBoxSlot, redBoxObject))
            {
                CheckAndSpawnBox(redBoxSlot, redBoxItemData, BoxType.Red);
            }
        }

        private void RespawnBlueBox()
        {
            if (!CanPerformNetworkOperations())
            {
                Debug.LogWarning("Cannot respawn blue box - network not ready");
                return;
            }

            if (!IsSlotOccupied(blueBoxSlot, blueBoxObject))
            {
                CheckAndSpawnBox(blueBoxSlot, blueBoxItemData, BoxType.Blue);
            }
        }

        private void RespawnYellowBox()
        {
            if (!CanPerformNetworkOperations())
            {
                Debug.LogWarning("Cannot respawn yellow box - network not ready");
                return;
            }

            if (!IsSlotOccupied(yellowBoxSlot, yellowBoxObject))
            {
                CheckAndSpawnBox(yellowBoxSlot, yellowBoxItemData, BoxType.Yellow);
            }
        }

        private void CheckAndSpawnBox(Transform slot, ItemData itemData, BoxType boxType)
        {
            if (!CanPerformNetworkOperations()) return;
            if (itemData == null || itemData.worldPrefab == null) return;

            if (!IsSlotOccupied(slot, GetNetworkObjectForType(boxType)))
            {
                SpawnBoxAtSlot(slot, itemData, boxType);
            }
        }

        private NetworkObject GetNetworkObjectForType(BoxType boxType)
        {
            switch (boxType)
            {
                case BoxType.Red: return redBoxObject;
                case BoxType.Blue: return blueBoxObject;
                case BoxType.Yellow: return yellowBoxObject;
                default: return null;
            }
        }

        private void SpawnBoxAtSlot(Transform slot, ItemData itemData, BoxType boxType)
        {
            if (!CanPerformNetworkOperations()) return;

            try
            {
                // World prefab'ı spawn et
                GameObject spawnedBox = Instantiate(itemData.worldPrefab, slot.position, slot.rotation);
                NetworkObject networkObject = spawnedBox.GetComponent<NetworkObject>();

                if (networkObject != null)
                {
                    networkObject.Spawn();

                    // NetworkWorldItem component'ini ayarla
                    NetworkWorldItem worldItem = spawnedBox.GetComponent<NetworkWorldItem>();
                    if (worldItem != null)
                    {
                        worldItem.SetItemData(itemData);
                        worldItem.EnablePickup();
                    }

                    // Network variable'ı güncelle ve local referansı sakla
                    switch (boxType)
                    {
                        case BoxType.Red:
                            redBoxNetworkId.Value = networkObject.NetworkObjectId;
                            redBoxObject = networkObject;
                            break;
                        case BoxType.Blue:
                            blueBoxNetworkId.Value = networkObject.NetworkObjectId;
                            blueBoxObject = networkObject;
                            break;
                        case BoxType.Yellow:
                            yellowBoxNetworkId.Value = networkObject.NetworkObjectId;
                            yellowBoxObject = networkObject;
                            break;
                    }

                    Debug.Log(
                        $"Spawned {boxType} box at {slot.name} with NetworkObjectId: {networkObject.NetworkObjectId}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Failed to spawn {boxType} box: {ex.Message}");
                // Hatalı objeyi temizle
                var failedObject = slot.GetComponentInChildren<NetworkObject>();
                if (failedObject != null)
                    Destroy(failedObject.gameObject);
            }
        }

        // Network variable değişiklik event'leri
        private void OnRedBoxChanged(ulong previousValue, ulong newValue)
        {
            UpdateLocalReference(BoxType.Red, newValue);
        }

        private void OnBlueBoxChanged(ulong previousValue, ulong newValue)
        {
            UpdateLocalReference(BoxType.Blue, newValue);
        }

        private void OnYellowBoxChanged(ulong previousValue, ulong newValue)
        {
            UpdateLocalReference(BoxType.Yellow, newValue);
        }

        private bool CanPerformNetworkOperations()
        {
            return NetworkManager.Singleton != null &&
                   NetworkManager.Singleton.IsListening &&
                   IsSpawned &&
                   IsServer;
        }


        private void UpdateLocalReference(BoxType boxType, ulong networkId)
        {
            NetworkObject networkObject = null;

            if (networkId != 0 && NetworkManager.Singleton != null && NetworkManager.Singleton.SpawnManager != null)
            {
                NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkId, out networkObject);
            }

            switch (boxType)
            {
                case BoxType.Red:
                    redBoxObject = networkObject;
                    break;
                case BoxType.Blue:
                    blueBoxObject = networkObject;
                    break;
                case BoxType.Yellow:
                    yellowBoxObject = networkObject;
                    break;
            }
        }

        // Public metodlar - dışarıdan kullanım için
        public void ForceRespawnAll()
        {
            if (!IsServer) return;

            ForceRespawnBox(BoxType.Red);
            ForceRespawnBox(BoxType.Blue);
            ForceRespawnBox(BoxType.Yellow);
        }

        public void ForceRespawnBox(BoxType boxType)
        {
            if (!IsServer) return;

            Transform slot;
            ItemData itemData;

            switch (boxType)
            {
                case BoxType.Red:
                    slot = redBoxSlot;
                    itemData = redBoxItemData;
                    if (redBoxObject != null && redBoxObject.IsSpawned)
                        redBoxObject.Despawn();
                    redBoxObject = null;
                    redBoxNetworkId.Value = 0;
                    break;
                case BoxType.Blue:
                    slot = blueBoxSlot;
                    itemData = blueBoxItemData;
                    if (blueBoxObject != null && blueBoxObject.IsSpawned)
                        blueBoxObject.Despawn();
                    blueBoxObject = null;
                    blueBoxNetworkId.Value = 0;
                    break;
                case BoxType.Yellow:
                    slot = yellowBoxSlot;
                    itemData = yellowBoxItemData;
                    if (yellowBoxObject != null && yellowBoxObject.IsSpawned)
                        yellowBoxObject.Despawn();
                    yellowBoxObject = null;
                    yellowBoxNetworkId.Value = 0;
                    break;
                default:
                    return;
            }

            SpawnBoxAtSlot(slot, itemData, boxType);
        }

        // Box türleri için enum
        public enum BoxType
        {
            Red,
            Blue,
            Yellow
        }

        // Gizmo çizimi (editörde slotları görmek için)
        private void OnDrawGizmosSelected()
        {
            if (redBoxSlot != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(redBoxSlot.position, Vector3.one * 0.5f);
            }

            if (blueBoxSlot != null)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireCube(blueBoxSlot.position, Vector3.one * 0.5f);
            }

            if (yellowBoxSlot != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(yellowBoxSlot.position, Vector3.one * 0.5f);
            }
        }

        // Inspector'da gösterilecek bilgiler
        public bool HasRedBox => redBoxObject != null && redBoxObject.IsSpawned;
        public bool HasBlueBox => blueBoxObject != null && blueBoxObject.IsSpawned;
        public bool HasYellowBox => yellowBoxObject != null && yellowBoxObject.IsSpawned;
    }
}