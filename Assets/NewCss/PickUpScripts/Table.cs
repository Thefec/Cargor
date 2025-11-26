using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System.Collections;

namespace NewCss
{
    public class Table : NetworkBehaviour
    {
        [Header("Table Settings")]
        [SerializeField] private string tableID = "";
        [SerializeField] private Transform itemPlacePoint;

        [Header("Interaction Box Settings")]
        [SerializeField] private Vector3 interactionBoxSize = new Vector3(2f, 2f, 2f);
        [SerializeField] private Vector3 interactionBoxOffset = Vector3.zero;
        [SerializeField] private bool showInteractionRange = true;

        private NetworkVariable<TableState> tableState = new NetworkVariable<TableState>(
            new TableState { isEmpty = true, itemNetworkId = 0, isItemBoxed = false },
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private GameObject currentItemOnTable;
        private static List<Table> allTables = new List<Table>();
        private BoxCollider interactionTrigger;

        #region Unity Lifecycle

        private void Awake()
        {
            if (string.IsNullOrEmpty(tableID))
            {
                tableID = $"Table_{GetInstanceID()}";
            }

            if (itemPlacePoint == null)
            {
                itemPlacePoint = transform;
                Debug.LogWarning($"Table {tableID}: No itemPlacePoint set, using transform");
            }
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
                Debug.Log($"Table {tableID}: Box interaction trigger added");
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
        /// ✅ FIX: Transform bazlı range kontrolü - HEM HOST HEM CLIENT için çalışır
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
        /// ✅ FIX: ClientId'den Transform bulup kontrol et
        /// </summary>
        private bool IsPlayerInRange(ulong clientId)
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
            if (!allTables.Contains(this))
            {
                allTables.Add(this);
            }

            tableState.OnValueChanged += OnTableStateChanged;

            if (IsServer)
            {
                var initialState = new TableState
                {
                    isEmpty = true,
                    itemNetworkId = 0,
                    isItemBoxed = false
                };
                tableState.Value = initialState;
            }

            Debug.Log($"Table {tableID} spawned - Total tables: {allTables.Count}");
        }

        public override void OnNetworkDespawn()
        {
            tableState.OnValueChanged -= OnTableStateChanged;

            if (allTables.Contains(this))
            {
                allTables.Remove(this);
            }
        }

        private void OnDestroy()
        {
            if (allTables.Contains(this))
            {
                allTables.Remove(this);
            }
        }

        #endregion

        #region Network State Management

        private void OnTableStateChanged(TableState oldState, TableState newState)
        {
            Debug.Log($"Table {tableID} state changed - isEmpty: {newState.isEmpty}, itemNetworkId: {newState.itemNetworkId}");
            UpdateVisualState(newState);
        }

        private void UpdateVisualState(TableState state)
        {
            if (state.isEmpty)
            {
                if (currentItemOnTable != null)
                {
                    Debug.Log($"Table {tableID}: Clearing visual item");
                    currentItemOnTable = null;
                }
            }
            else
            {
                if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(state.itemNetworkId, out NetworkObject itemNetObj))
                {
                    currentItemOnTable = itemNetObj.gameObject;
                    PositionItemOnTable(currentItemOnTable);
                    Debug.Log($"Table {tableID}: Item positioned on table");
                }
            }
        }

        private void PositionItemOnTable(GameObject item)
        {
            if (item == null || itemPlacePoint == null) return;

            Vector3 targetPosition = itemPlacePoint.position;
            targetPosition.y += 0.5f;

            item.transform.position = targetPosition;
            item.transform.rotation = itemPlacePoint.rotation;

            Rigidbody rb = item.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }

        #endregion

        #region Public Interface

        public bool IsEmpty => tableState.Value.isEmpty;
        public bool HasItem => !tableState.Value.isEmpty;
        public bool IsItemBoxed => tableState.Value.isItemBoxed;

        public bool CanPlaceItem()
        {
            return IsEmpty;
        }

        public bool CanTakeItem()
        {
            return HasItem;
        }

        public void InteractWithTable(PlayerInventory player)
        {
            if (!IsServer)
            {
                Debug.Log($"Table {tableID}: Client requesting interaction");
                RequestInteractionServerRpc(player.NetworkObjectId);
                return;
            }

            ProcessTableInteraction(player);
        }

        #endregion

        #region Server RPCs

        /// <summary>
        /// ✅ FIX: Transform bazlı range kontrolü kullanılıyor
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        private void RequestInteractionServerRpc(ulong playerNetworkId, ServerRpcParams rpcParams = default)
        {
            ulong requesterClientId = rpcParams.Receive.SenderClientId;

            Debug.Log($"📥 Table {tableID}: Interaction requested by client {requesterClientId}");

            // ✅ Player object bul
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(requesterClientId, out var client) ||
                client.PlayerObject == null)
            {
                Debug.LogError($"❌ Table {tableID}: Player object not found for client {requesterClientId}");
                return;
            }

            Transform playerTransform = client.PlayerObject.transform;

            // ✅ Transform bazlı range kontrolü
            if (!IsPlayerInRange(playerTransform))
            {
                Debug.LogWarning($"❌ Table {tableID}: Client {requesterClientId} is NOT in range! Distance: {Vector3.Distance(playerTransform.position, transform.position)}");
                return;
            }

            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(playerNetworkId, out NetworkObject playerObj))
            {
                Debug.LogError($"❌ Table {tableID}: Player NetworkObject not found");
                return;
            }

            PlayerInventory player = playerObj.GetComponent<PlayerInventory>();
            if (player == null)
            {
                Debug.LogError($"❌ Table {tableID}: PlayerInventory component not found");
                return;
            }

            ProcessTableInteraction(player);
        }

        private void ProcessTableInteraction(PlayerInventory player)
        {
            Debug.Log($"Table {tableID}: Processing interaction - Player has item: {player.HasItem}, Table has item: {HasItem}");

            if (player.HasItem)
            {
                if (CanPlaceItem())
                {
                    Debug.Log($"✅ Table {tableID}: Placing item from player {player.OwnerClientId}");
                    PlaceItemOnTable(player);
                }
                else
                {
                    Debug.Log($"🎮 Table {tableID}: Attempting boxing for player {player.OwnerClientId}");
                    TryBoxingInteraction(player);
                }
            }
            else
            {
                if (CanTakeItem())
                {
                    Debug.Log($"✅ Table {tableID}: Taking item from table for player {player.OwnerClientId}");
                    TakeItemFromTable(player);
                }
                else
                {
                    Debug.Log($"⚠️ Table {tableID}: No interaction possible");
                }
            }
        }

        private void PlaceItemOnTable(PlayerInventory player)
        {
            Debug.Log($"📤 Table {tableID}: Placing item for client {player.OwnerClientId}");

            ItemData playerItemData = player.CurrentItemData;
            if (playerItemData == null)
            {
                Debug.LogError($"❌ Table {tableID}: Player has no valid item data");
                return;
            }

            player.SetInventoryStateServerRpc(false, -1);
            player.TriggerDropAnimationServerRpc();

            StartCoroutine(SpawnItemOnTableCoroutine(playerItemData));

            if (TutorialManager.Instance != null)
            {
                TutorialManager.Instance.OnTableInteraction(true);
            }
        }

        private IEnumerator SpawnItemOnTableCoroutine(ItemData itemData)
        {
            yield return new WaitForSeconds(0.1f);

            if (itemData.worldPrefab == null)
            {
                Debug.LogError($"❌ Table {tableID}: Item {itemData.itemName} has no world prefab");
                yield break;
            }

            Vector3 spawnPos = itemPlacePoint.position;
            spawnPos.y += 1f;

            GameObject worldItem = Instantiate(itemData.worldPrefab, spawnPos, itemPlacePoint.rotation);
            NetworkObject netObj = worldItem.GetComponent<NetworkObject>();

            if (netObj != null)
            {
                netObj.Spawn();

                NetworkWorldItem worldItemComponent = worldItem.GetComponent<NetworkWorldItem>();
                if (worldItemComponent != null)
                {
                    worldItemComponent.SetItemData(itemData);
                    worldItemComponent.DisablePickup();
                }

                var newState = new TableState
                {
                    isEmpty = false,
                    itemNetworkId = netObj.NetworkObjectId,
                    isItemBoxed = false
                };
                tableState.Value = newState;

                Debug.Log($"✅ Table {tableID}: Item {itemData.itemName} placed successfully");
            }
            else
            {
                Debug.LogError($"❌ Table {tableID}: World item has no NetworkObject component");
                Destroy(worldItem);
            }
        }

        private void TakeItemFromTable(PlayerInventory player)
        {
            var state = tableState.Value;
            if (state.isEmpty)
            {
                Debug.Log($"⚠️ Table {tableID}: Cannot take item - table is empty");
                return;
            }

            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(state.itemNetworkId, out NetworkObject itemNetObj))
            {
                Debug.LogError($"❌ Table {tableID}: Item NetworkObject not found");
                return;
            }

            NetworkWorldItem worldItem = itemNetObj.GetComponent<NetworkWorldItem>();
            if (worldItem == null)
            {
                Debug.LogError($"❌ Table {tableID}: NetworkWorldItem component not found");
                return;
            }

            ItemData itemData = worldItem.ItemData;
            if (itemData == null)
            {
                Debug.LogError($"❌ Table {tableID}: Item has no ItemData");
                return;
            }

            player.GiveItemDirectlyServerRpc(itemData.itemID);
            itemNetObj.Despawn(true);

            var newState = new TableState
            {
                isEmpty = true,
                itemNetworkId = 0,
                isItemBoxed = false
            };
            tableState.Value = newState;

            Debug.Log($"✅ Table {tableID}: Item {itemData.itemName} taken by player {player.OwnerClientId}");

            if (TutorialManager.Instance != null)
            {
                TutorialManager.Instance.OnTableInteraction(false);
            }
        }

        /// <summary>
        /// ✅ FIX: Boxing minigame başlatma düzeltildi - PlayerNetworkId gönderiliyor
        /// </summary>
        private void TryBoxingInteraction(PlayerInventory player)
        {
            var state = tableState.Value;

            if (state.isItemBoxed)
            {
                Debug.Log($"⚠️ Table {tableID}: Item is already boxed");
                return;
            }

            ItemData playerItemData = player.CurrentItemData;
            if (playerItemData?.visualPrefab == null)
            {
                Debug.Log($"⚠️ Table {tableID}: Player has no valid item for boxing");
                return;
            }

            BoxInfo playerBox = playerItemData.visualPrefab.GetComponent<BoxInfo>();
            if (playerBox == null || playerBox.isFull)
            {
                Debug.Log($"⚠️ Table {tableID}: Player doesn't have an empty box");
                return;
            }

            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(state.itemNetworkId, out NetworkObject tableItemNetObj))
            {
                Debug.LogError($"❌ Table {tableID}: Table item not found for boxing");
                return;
            }

            ProductInfo tableProduct = tableItemNetObj.GetComponent<ProductInfo>();
            if (tableProduct == null)
            {
                Debug.Log($"⚠️ Table {tableID}: Table item is not a product, cannot box");
                return;
            }

            if (!IsValidBoxProductCombination(playerBox.boxType, tableProduct.productType))
            {
                Debug.Log($"❌ Table {tableID}: Box type {playerBox.boxType} doesn't match product type {tableProduct.productType}");
                NotifyBoxingFailedClientRpc(player.OwnerClientId);
                return;
            }

            BoxingMinigameManager minigame = GetComponentInChildren<BoxingMinigameManager>();
            if (minigame == null)
            {
                Debug.LogError($"❌ Table {tableID}: BoxingMinigameManager not found!");
                return;
            }

            Debug.Log($"🎮 Table {tableID}: Starting minigame for client {player.OwnerClientId}");

            // ✅ FIX: PlayerNetworkObjectId de gönderiliyor
            StartMinigameClientRpc(player.OwnerClientId, player.NetworkObjectId, (int)playerBox.boxType, playerItemData.itemID);
        }

        #endregion

        #region Client RPCs

        /// <summary>
        /// ✅ FIX: PlayerNetworkObjectId ile player bulunuyor
        /// </summary>
        [ClientRpc]
        private void StartMinigameClientRpc(ulong targetClientId, ulong playerNetworkObjectId, int boxTypeInt, int itemDataID)
        {
            Debug.Log($"📥 CLIENT {NetworkManager.Singleton.LocalClientId}: Received StartMinigameClientRpc - Target: {targetClientId}, PlayerNetID: {playerNetworkObjectId}");

            // Sadece hedef client minigame'i başlatır
            if (NetworkManager.Singleton.LocalClientId != targetClientId)
            {
                Debug.Log($"⏩ Skipping minigame start - not target client");
                return;
            }

            Debug.Log($"🎮 CLIENT {targetClientId}: I AM the target!  Starting minigame...");

            // ✅ NetworkObjectId ile player'ı bul
            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(playerNetworkObjectId, out NetworkObject playerObj))
            {
                Debug.LogError($"❌ CLIENT {targetClientId}: Player NetworkObject {playerNetworkObjectId} not found!");
                return;
            }

            PlayerInventory player = playerObj.GetComponent<PlayerInventory>();
            if (player == null)
            {
                Debug.LogError($"❌ CLIENT {targetClientId}: PlayerInventory not found!");
                return;
            }

            // ✅ IsOwner ZORUNLU kontrolü
            if (!player.IsOwner)
            {
                Debug.LogError($"❌ CLIENT {targetClientId}: Player is NOT owned by this client!");
                return;
            }

            // ItemData'yı bul
            ItemData itemData = GetItemDataFromID(itemDataID);
            if (itemData == null)
            {
                Debug.LogError($"❌ CLIENT {targetClientId}: ItemData with ID {itemDataID} not found!");
                return;
            }

            BoxInfo.BoxType boxType = (BoxInfo.BoxType)boxTypeInt;

            BoxingMinigameManager minigame = GetComponentInChildren<BoxingMinigameManager>();
            if (minigame == null)
            {
                Debug.LogError($"❌ CLIENT {targetClientId}: BoxingMinigameManager not found!");
                return;
            }

            Debug.Log($"✅ CLIENT {targetClientId}: All checks passed - Starting minigame for {boxType} box");
            minigame.StartMinigame(player, boxType, itemData);
        }

        [ClientRpc]
        private void NotifyBoxingFailedClientRpc(ulong targetClientId)
        {
            if (NetworkManager.Singleton.LocalClientId == targetClientId)
            {
                Debug.Log($"❌ Table {tableID}: Boxing failed - box and product don't match");
            }
        }

        #endregion

        #region Minigame Callbacks

        public void CompleteBoxingSuccess(PlayerInventory player, BoxInfo.BoxType boxType)
        {
            if (!IsServer) return;

            Debug.Log($"✅ Table {tableID}: Boxing SUCCESS for {boxType} by client {player.OwnerClientId}");

            player.SetInventoryStateServerRpc(false, -1);
            player.TriggerDropAnimationServerRpc();

            var state = tableState.Value;
            if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(state.itemNetworkId, out NetworkObject productNetObj))
            {
                productNetObj.Despawn(true);
            }

            StartCoroutine(SpawnBoxedProductAfterMinigame(boxType));

            if (QuestManager.Instance != null && IsServer)
            {
                QuestManager.Instance.IncrementQuestProgress(QuestType.PackageBoxes, boxType);
            }
        }

        public void CompleteBoxingFailure(PlayerInventory player)
        {
            if (!IsServer) return;

            Debug.Log($"❌ Table {tableID}: Boxing FAILED for client {player.OwnerClientId}");

            player.SetInventoryStateServerRpc(false, -1);
            player.TriggerDropAnimationServerRpc();

            NotifyBoxingFailedClientRpc(player.OwnerClientId);
        }

        private IEnumerator SpawnBoxedProductAfterMinigame(BoxInfo.BoxType boxType)
        {
            yield return new WaitForSeconds(0.3f);

            ItemData boxedProductData = GetBoxedProductData(boxType);
            if (boxedProductData != null)
            {
                yield return StartCoroutine(SpawnBoxedProductCoroutine(boxedProductData));
                Debug.Log($"✅ Table {tableID}: Boxed product spawned: {boxedProductData.itemName}");
            }
        }

        private IEnumerator SpawnBoxedProductCoroutine(ItemData boxedProductData)
        {
            Vector3 spawnPos = itemPlacePoint.position;
            spawnPos.y += 1f;

            GameObject boxedItem = Instantiate(boxedProductData.worldPrefab, spawnPos, itemPlacePoint.rotation);
            NetworkObject netObj = boxedItem.GetComponent<NetworkObject>();

            if (netObj != null)
            {
                netObj.Spawn();

                NetworkWorldItem worldItem = boxedItem.GetComponent<NetworkWorldItem>();
                if (worldItem != null)
                {
                    worldItem.SetItemData(boxedProductData);
                    worldItem.DisablePickup();
                }

                var newState = new TableState
                {
                    isEmpty = false,
                    itemNetworkId = netObj.NetworkObjectId,
                    isItemBoxed = true
                };
                tableState.Value = newState;
            }

            yield return null;
        }

        #endregion

        #region Helper Methods

        private bool IsValidBoxProductCombination(BoxInfo.BoxType boxType, ProductInfo.ProductType productType)
        {
            return (productType == ProductInfo.ProductType.Toy && boxType == BoxInfo.BoxType.Red) ||
                   (productType == ProductInfo.ProductType.Clothing && boxType == BoxInfo.BoxType.Yellow) ||
                   (productType == ProductInfo.ProductType.Glass && boxType == BoxInfo.BoxType.Blue);
        }

        private ItemData GetBoxedProductData(BoxInfo.BoxType boxType)
        {
            string itemName = boxType switch
            {
                BoxInfo.BoxType.Red => "RedBoxFull",
                BoxInfo.BoxType.Yellow => "YellowBoxFull",
                BoxInfo.BoxType.Blue => "BlueBoxFull",
                _ => ""
            };

            if (!string.IsNullOrEmpty(itemName))
            {
                return Resources.Load<ItemData>($"Items/{itemName}");
            }

            return null;
        }

        private ItemData GetItemDataFromID(int itemID)
        {
            ItemData[] allItems = Resources.LoadAll<ItemData>("Items");
            foreach (ItemData item in allItems)
            {
                if (item.itemID == itemID)
                {
                    return item;
                }
            }
            return null;
        }

        public static Table GetTableByID(string id)
        {
            return allTables.Find(t => t != null && t.tableID == id);
        }

        public static List<Table> GetAllTables()
        {
            allTables.RemoveAll(t => t == null);
            return new List<Table>(allTables);
        }

        #endregion

        #region Debug & Gizmos

        [ContextMenu("Debug Table State")]
        public void DebugTableState()
        {
            Debug.Log($"=== TABLE {tableID} DEBUG ===");
            Debug.Log($"Network State - IsServer: {IsServer}, IsClient: {IsClient}");
            Debug.Log($"Table State - Empty: {IsEmpty}, HasItem: {HasItem}, IsBoxed: {IsItemBoxed}");
            Debug.Log($"NetworkObjectId: {(tableState.Value.isEmpty ? "None" : tableState.Value.itemNetworkId.ToString())}");
            Debug.Log($"Current Item Object: {(currentItemOnTable != null ? currentItemOnTable.name : "null")}");
            Debug.Log($"============================");
        }

        private void OnDrawGizmosSelected()
        {
            if (!showInteractionRange) return;

            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Matrix4x4 rotationMatrix = Matrix4x4.TRS(transform.position + transform.TransformDirection(interactionBoxOffset), transform.rotation, Vector3.one);
            Gizmos.matrix = rotationMatrix;
            Gizmos.DrawCube(Vector3.zero, interactionBoxSize);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(Vector3.zero, interactionBoxSize);

            Gizmos.matrix = Matrix4x4.identity;

            if (itemPlacePoint != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(itemPlacePoint.position, Vector3.one * 0.3f);
                Gizmos.DrawLine(transform.position, itemPlacePoint.position);

#if UNITY_EDITOR
                UnityEditor.Handles.Label(itemPlacePoint.position + Vector3.up * 0.5f, tableID);
#endif
            }

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position + transform.TransformDirection(interactionBoxOffset), 0.1f);
        }

        #endregion
    }

    [System.Serializable]
    public struct TableState : INetworkSerializable
    {
        public bool isEmpty;
        public ulong itemNetworkId;
        public bool isItemBoxed;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref isEmpty);
            serializer.SerializeValue(ref itemNetworkId);
            serializer.SerializeValue(ref isItemBoxed);
        }
    }
}