using Unity.Netcode;
using UnityEngine;

namespace NewCss
{
    public class NetworkedShelf : NetworkBehaviour
    {
        [Header("Item Data")]
        public ItemData redBoxItemData;
        public ItemData blueBoxItemData;
        public ItemData yellowBoxItemData;

        [Header("Box Slots (Positions on the shelf)")]
        public Transform redBoxSlot;
        public Transform blueBoxSlot;
        public Transform yellowBoxSlot;

        [Header("Respawn Settings")]
        [SerializeField] private float respawnDelay = 1f;
        [SerializeField] private bool enableAutoRespawn = true;

        [Header("Shelf Behavior")]
        [SerializeField] private bool allowPlacingItems = false;

        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true; // ✅ YENİ

        private NetworkVariable<ulong> redBoxNetworkId = new NetworkVariable<ulong>(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private NetworkVariable<ulong> blueBoxNetworkId = new NetworkVariable<ulong>(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private NetworkVariable<ulong> yellowBoxNetworkId = new NetworkVariable<ulong>(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private NetworkObject redBoxObject;
        private NetworkObject blueBoxObject;
        private NetworkObject yellowBoxObject;

        // ✅ YENİ: Son kutu sayılarını takip et (kutu alındığını anlamak için)
        private int lastRedBoxCount = 0;
        private int lastBlueBoxCount = 0;
        private int lastYellowBoxCount = 0;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                CheckAndSpawnBox(redBoxSlot, redBoxItemData, BoxType.Red);
                CheckAndSpawnBox(blueBoxSlot, blueBoxItemData, BoxType.Blue);
                CheckAndSpawnBox(yellowBoxSlot, yellowBoxItemData, BoxType.Yellow);

                // ✅ Başlangıç sayılarını kaydet
                UpdateBoxCounts();
            }

            redBoxNetworkId.OnValueChanged += OnRedBoxChanged;
            blueBoxNetworkId.OnValueChanged += OnBlueBoxChanged;
            yellowBoxNetworkId.OnValueChanged += OnYellowBoxChanged;
        }

        public override void OnNetworkDespawn()
        {
            redBoxNetworkId.OnValueChanged -= OnRedBoxChanged;
            blueBoxNetworkId.OnValueChanged -= OnBlueBoxChanged;
            yellowBoxNetworkId.OnValueChanged -= OnYellowBoxChanged;
        }

        private void Update()
        {
            if (!CanPerformNetworkOperations() || !enableAutoRespawn) return;

            CheckAndRespawnIfNeeded();

            // ✅ YENİ: Kutu alınma kontrolü
            CheckForBoxTaken();
        }

        // ✅ YENİ: Kutu alınma kontrolü
        private void CheckForBoxTaken()
        {
            int currentRedCount = HasRedBox ? 1 : 0;
            int currentBlueCount = HasBlueBox ? 1 : 0;
            int currentYellowCount = HasYellowBox ? 1 : 0;

            // Kırmızı kutu alındı mı?
            if (lastRedBoxCount > currentRedCount)
            {
                if (showDebugLogs)
                    Debug.Log("📦 Red box taken from shelf!");

                NotifyTutorialBoxTaken(BoxType.Red);
            }

            // Mavi kutu alındı mı?
            if (lastBlueBoxCount > currentBlueCount)
            {
                if (showDebugLogs)
                    Debug.Log("📦 Blue box taken from shelf!");

                NotifyTutorialBoxTaken(BoxType.Blue);
            }

            // Sarı kutu alındı mı?
            if (lastYellowBoxCount > currentYellowCount)
            {
                if (showDebugLogs)
                    Debug.Log("📦 Yellow box taken from shelf!");

                NotifyTutorialBoxTaken(BoxType.Yellow);
            }

            // Son sayıları güncelle
            lastRedBoxCount = currentRedCount;
            lastBlueBoxCount = currentBlueCount;
            lastYellowBoxCount = currentYellowCount;
        }

        // ✅ YENİ: Tutorial'a kutu alındığını bildir
        private void NotifyTutorialBoxTaken(BoxType boxType)
        {
            if (TutorialManager.Instance != null)
            {
                TutorialManager.Instance.OnBoxTakenFromShelf(boxType);

                if (showDebugLogs)
                    Debug.Log($"📚 Tutorial notified: {boxType} box taken from shelf");
            }
        }

        // ✅ YENİ: Mevcut kutu sayılarını güncelle
        private void UpdateBoxCounts()
        {
            lastRedBoxCount = HasRedBox ? 1 : 0;
            lastBlueBoxCount = HasBlueBox ? 1 : 0;
            lastYellowBoxCount = HasYellowBox ? 1 : 0;
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
            if (networkObject != null && networkObject.IsSpawned)
            {
                float distance = Vector3.Distance(networkObject.transform.position, slot.position);
                return distance < 0.5f;
            }

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
                GameObject spawnedBox = Instantiate(itemData.worldPrefab, slot.position, slot.rotation);
                NetworkObject networkObject = spawnedBox.GetComponent<NetworkObject>();

                if (networkObject != null)
                {
                    networkObject.Spawn();

                    NetworkWorldItem worldItem = spawnedBox.GetComponent<NetworkWorldItem>();
                    if (worldItem != null)
                    {
                        worldItem.SetItemData(itemData);
                        worldItem.EnablePickup();
                    }

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

                    if (showDebugLogs)
                        Debug.Log($"Spawned {boxType} box at {slot.name} with NetworkObjectId: {networkObject.NetworkObjectId}");

                    // ✅ Spawn sonrası sayıları güncelle
                    UpdateBoxCounts();
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"Failed to spawn {boxType} box: {ex.Message}");
                var failedObject = slot.GetComponentInChildren<NetworkObject>();
                if (failedObject != null)
                    Destroy(failedObject.gameObject);
            }
        }

        private void OnRedBoxChanged(ulong previousValue, ulong newValue)
        {
            UpdateLocalReference(BoxType.Red, newValue);
            UpdateBoxCounts(); // ✅ Değişim olunca güncelle
        }

        private void OnBlueBoxChanged(ulong previousValue, ulong newValue)
        {
            UpdateLocalReference(BoxType.Blue, newValue);
            UpdateBoxCounts(); // ✅ Değişim olunca güncelle
        }

        private void OnYellowBoxChanged(ulong previousValue, ulong newValue)
        {
            UpdateLocalReference(BoxType.Yellow, newValue);
            UpdateBoxCounts(); // ✅ Değişim olunca güncelle
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

        public bool CanPlaceItems()
        {
            return allowPlacingItems;
        }

        public enum BoxType
        {
            Red,
            Blue,
            Yellow
        }

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

        public bool HasRedBox => redBoxObject != null && redBoxObject.IsSpawned;
        public bool HasBlueBox => blueBoxObject != null && blueBoxObject.IsSpawned;
        public bool HasYellowBox => yellowBoxObject != null && yellowBoxObject.IsSpawned;
    }
}