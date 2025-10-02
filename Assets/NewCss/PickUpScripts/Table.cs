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
        [SerializeField] private float interactionRange = 2f;

        // Network synchronized table state
        private NetworkVariable<TableState> tableState = new NetworkVariable<TableState>(
            new TableState { isEmpty = true, itemNetworkId = 0, isItemBoxed = false },
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        // Local references
        private GameObject currentItemOnTable;
        private static List<Table> allTables = new List<Table>();

        #region Unity Lifecycle

        private void Awake()
        {
            // Auto-generate table ID if empty
            if (string.IsNullOrEmpty(tableID))
            {
                tableID = $"Table_{GetInstanceID()}";
            }

            // Ensure we have a place point
            if (itemPlacePoint == null)
            {
                itemPlacePoint = transform;
                Debug.LogWarning($"Table {tableID}: No itemPlacePoint set, using transform");
            }
        }

        public override void OnNetworkSpawn()
        {
            // Add to static list
            if (!allTables.Contains(this))
            {
                allTables.Add(this);
            }

            // Listen for state changes
            tableState.OnValueChanged += OnTableStateChanged;

            // Initialize table state on server
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
                // Table is empty
                if (currentItemOnTable != null)
                {
                    Debug.Log($"Table {tableID}: Clearing visual item");
                    currentItemOnTable = null;
                }
            }
            else
            {
                // Table has an item - find and position it
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

            // Position the item
            Vector3 targetPosition = itemPlacePoint.position;
            targetPosition.y += 0.5f; // Slight offset above table

            item.transform.position = targetPosition;
            item.transform.rotation = itemPlacePoint.rotation;

            // Make item kinematic when on table
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
                Debug.Log($"Table {tableID}: Not server, requesting interaction via RPC");
                RequestInteractionServerRpc(player.NetworkObjectId);
                return;
            }

            ProcessTableInteraction(player);
        }

        #endregion

        #region Server RPCs

        [ServerRpc(RequireOwnership = false)]
        private void RequestInteractionServerRpc(ulong playerNetworkId)
        {
            Debug.Log($"Table {tableID}: Interaction requested by player {playerNetworkId}");

            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(playerNetworkId, out NetworkObject playerObj))
            {
                Debug.LogError($"Table {tableID}: Player NetworkObject not found");
                return;
            }

            PlayerInventory player = playerObj.GetComponent<PlayerInventory>();
            if (player == null)
            {
                Debug.LogError($"Table {tableID}: PlayerInventory component not found");
                return;
            }

            ProcessTableInteraction(player);
        }

        private void ProcessTableInteraction(PlayerInventory player)
        {
            Debug.Log($"Table {tableID}: Processing interaction - Player has item: {player.HasItem}, Table has item: {HasItem}");

            if (player.HasItem)
            {
                // Player has item, try to place it
                if (CanPlaceItem())
                {
                    Debug.Log($"Table {tableID}: Placing item from player");
                    PlaceItemOnTable(player);
                }
                else
                {
                    Debug.Log($"Table {tableID}: Table is full, checking boxing options");
                    TryBoxingInteraction(player);
                }
            }
            else
            {
                // Player doesn't have item, try to take from table
                if (CanTakeItem())
                {
                    Debug.Log($"Table {tableID}: Taking item from table");
                    TakeItemFromTable(player);
                }
                else
                {
                    Debug.Log($"Table {tableID}: No interaction possible - table is empty and player has no item");
                }
            }
        }

        private void PlaceItemOnTable(PlayerInventory player)
        {
            Debug.Log($"Table {tableID}: Starting place item process");

            // Get player's current item data
            ItemData playerItemData = player.CurrentItemData;
            if (playerItemData == null)
            {
                Debug.LogError($"Table {tableID}: Player has no valid item data");
                return;
            }

            // Clear player's item first
            player.SetInventoryStateServerRpc(false, -1);
            player.TriggerDropAnimationServerRpc();

            // Spawn world item on table
            StartCoroutine(SpawnItemOnTableCoroutine(playerItemData));
        }

        private IEnumerator SpawnItemOnTableCoroutine(ItemData itemData)
        {
            yield return new WaitForSeconds(0.1f);

            if (itemData.worldPrefab == null)
            {
                Debug.LogError($"Table {tableID}: Item {itemData.itemName} has no world prefab");
                yield break;
            }

            // Calculate spawn position
            Vector3 spawnPos = itemPlacePoint.position;
            spawnPos.y += 1f; // Spawn slightly above to let it settle

            // Instantiate and spawn
            GameObject worldItem = Instantiate(itemData.worldPrefab, spawnPos, itemPlacePoint.rotation);
            NetworkObject netObj = worldItem.GetComponent<NetworkObject>();

            if (netObj != null)
            {
                netObj.Spawn();

                // Set item data
                NetworkWorldItem worldItemComponent = worldItem.GetComponent<NetworkWorldItem>();
                if (worldItemComponent != null)
                {
                    worldItemComponent.SetItemData(itemData);
                    worldItemComponent.DisablePickup(); // Items on table can't be picked up directly
                }

                // Update table state
                var newState = new TableState
                {
                    isEmpty = false,
                    itemNetworkId = netObj.NetworkObjectId,
                    isItemBoxed = false
                };
                tableState.Value = newState;

                Debug.Log($"Table {tableID}: Item {itemData.itemName} placed successfully");
            }
            else
            {
                Debug.LogError($"Table {tableID}: World item has no NetworkObject component");
                Destroy(worldItem);
            }
        }

        private void TakeItemFromTable(PlayerInventory player)
        {
            var state = tableState.Value;
            if (state.isEmpty)
            {
                Debug.Log($"Table {tableID}: Cannot take item - table is empty");
                return;
            }

            // Find the item on table
            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(state.itemNetworkId, out NetworkObject itemNetObj))
            {
                Debug.LogError($"Table {tableID}: Item NetworkObject not found");
                return;
            }

            NetworkWorldItem worldItem = itemNetObj.GetComponent<NetworkWorldItem>();
            if (worldItem == null)
            {
                Debug.LogError($"Table {tableID}: NetworkWorldItem component not found");
                return;
            }

            ItemData itemData = worldItem.ItemData;
            if (itemData == null)
            {
                Debug.LogError($"Table {tableID}: Item has no ItemData");
                return;
            }

            // Give item to player
            player.GiveItemDirectlyServerRpc(itemData.itemID);

            // Remove item from world
            itemNetObj.Despawn(true);

            // Update table state
            var newState = new TableState
            {
                isEmpty = true,
                itemNetworkId = 0,
                isItemBoxed = false
            };
            tableState.Value = newState;

            Debug.Log($"Table {tableID}: Item {itemData.itemName} taken by player");
        }

        private void TryBoxingInteraction(PlayerInventory player)
        {
            var state = tableState.Value;

            // Check if table item can be boxed
            if (state.isItemBoxed)
            {
                Debug.Log($"Table {tableID}: Item is already boxed");
                return;
            }

            // Check if player has a suitable box
            ItemData playerItemData = player.CurrentItemData;
            if (playerItemData?.visualPrefab == null)
            {
                Debug.Log($"Table {tableID}: Player has no valid item for boxing");
                return;
            }

            BoxInfo playerBox = playerItemData.visualPrefab.GetComponent<BoxInfo>();
            if (playerBox == null || playerBox.isFull)
            {
                Debug.Log($"Table {tableID}: Player doesn't have an empty box");
                return;
            }

            // Get table item info
            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(state.itemNetworkId, out NetworkObject tableItemNetObj))
            {
                Debug.LogError($"Table {tableID}: Table item not found for boxing");
                return;
            }

            ProductInfo tableProduct = tableItemNetObj.GetComponent<ProductInfo>();
            if (tableProduct == null)
            {
                Debug.Log($"Table {tableID}: Table item is not a product, cannot box");
                return;
            }

            // Check if box and product match
            if (!IsValidBoxProductCombination(playerBox.boxType, tableProduct.productType))
            {
                Debug.Log($"Table {tableID}: Box type {playerBox.boxType} doesn't match product type {tableProduct.productType}");
                NotifyBoxingFailedClientRpc(player.NetworkObjectId);
                return;
            }

            // Perform boxing
            StartCoroutine(PerformBoxingCoroutine(player, playerBox.boxType, tableProduct));
        }

        private IEnumerator PerformBoxingCoroutine(PlayerInventory player, BoxInfo.BoxType boxType, ProductInfo product)
        {
            Debug.Log($"Table {tableID}: Starting boxing process");

            // Remove player's box
            player.SetInventoryStateServerRpc(false, -1);
            player.TriggerDropAnimationServerRpc();

            // Remove current product from table
            var state = tableState.Value;
            if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(state.itemNetworkId, out NetworkObject productNetObj))
            {
                productNetObj.Despawn(true);
            }

            yield return new WaitForSeconds(0.2f);

            // Spawn boxed product
            ItemData boxedProductData = GetBoxedProductData(boxType);
            if (boxedProductData != null)
            {
                yield return StartCoroutine(SpawnBoxedProductCoroutine(boxedProductData));
                Debug.Log($"Table {tableID}: Boxing completed successfully");
            }
            else
            {
                Debug.LogError($"Table {tableID}: Failed to get boxed product data for {boxType}");
                // Reset table state on failure
                var resetState = new TableState { isEmpty = true, itemNetworkId = 0, isItemBoxed = false };
                tableState.Value = resetState;
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

                // Update table state
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

        #region Client RPCs

        [ClientRpc]
        private void NotifyBoxingFailedClientRpc(ulong playerNetworkId)
        {
            Debug.Log($"Table {tableID}: Boxing failed - box and product don't match");
            // Here you could show a UI message or play a sound effect
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
            // Draw interaction range
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactionRange);

            // Draw item place point
            if (itemPlacePoint != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(itemPlacePoint.position, Vector3.one * 0.3f);
                Gizmos.DrawLine(transform.position, itemPlacePoint.position);

                // Show table ID
#if UNITY_EDITOR
                UnityEditor.Handles.Label(itemPlacePoint.position + Vector3.up * 0.5f, tableID);
#endif
            }
        }

        #endregion
    }

    // Simplified network serializable struct
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