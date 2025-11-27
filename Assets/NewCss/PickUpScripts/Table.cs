using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace NewCss
{
    /// <summary>
    /// Masa yönetimi - item yerleştirme, alma ve kutulamayı (boxing) yönetir. 
    /// Network senkronizasyonu ile multiplayer desteği sağlar.
    /// </summary>
    public class Table : NetworkBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[Table]";
        private const float ITEM_SPAWN_DELAY = 0.1f;
        private const float BOXED_PRODUCT_SPAWN_DELAY = 0.3f;
        private const float ITEM_HEIGHT_OFFSET = 0.5f;
        private const float SPAWN_HEIGHT_OFFSET = 1f;
        private const float PLACE_POINT_GIZMO_SIZE = 0.3f;
        private const float CENTER_GIZMO_RADIUS = 0.1f;

        #endregion

        #region Serialized Fields

        [Header("=== TABLE SETTINGS ===")]
        [SerializeField, Tooltip("Benzersiz masa ID'si")]
        private string tableID = "";

        [SerializeField, Tooltip("Item yerleştirme noktası")]
        private Transform itemPlacePoint;

        [Header("=== INTERACTION BOX SETTINGS ===")]
        [SerializeField, Tooltip("Etkileşim kutusu boyutu")]
        private Vector3 interactionBoxSize = new Vector3(2f, 2f, 2f);

        [SerializeField, Tooltip("Etkileşim kutusu offset'i")]
        private Vector3 interactionBoxOffset = Vector3.zero;

        [SerializeField, Tooltip("Etkileşim alanını Gizmo ile göster")]
        private bool showInteractionRange = true;

        #endregion

        #region Network Variables

        private readonly NetworkVariable<TableState> _tableState = new(
            new TableState { isEmpty = true, itemNetworkId = 0, isItemBoxed = false },
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        #endregion

        #region Private Fields

        private GameObject _currentItemOnTable;
        private BoxCollider _interactionTrigger;

        // Static table registry
        private static readonly List<Table> _allTables = new();

        #endregion

        #region Public Properties

        /// <summary>
        /// Masa ID'si
        /// </summary>
        public string TableID => tableID;

        /// <summary>
        /// Masa boş mu?
        /// </summary>
        public bool IsEmpty => _tableState.Value.isEmpty;

        /// <summary>
        /// Masada item var mı?
        /// </summary>
        public bool HasItem => !_tableState.Value.isEmpty;

        /// <summary>
        /// Masadaki item kutulanmış mı?
        /// </summary>
        public bool IsItemBoxed => _tableState.Value.isItemBoxed;

        /// <summary>
        /// Masadaki mevcut item (null olabilir)
        /// </summary>
        public GameObject CurrentItem => _currentItemOnTable;

        /// <summary>
        /// Item yerleştirilebilir mi?
        /// </summary>
        public bool CanPlaceItem => IsEmpty;

        /// <summary>
        /// Item alınabilir mi?
        /// </summary>
        public bool CanTakeItem => HasItem;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeTableID();
            InitializeItemPlacePoint();
        }

        private void Start()
        {
            SetupInteractionTrigger();
        }

        private void OnDestroy()
        {
            UnregisterTable();
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

            RegisterTable();
            SubscribeToNetworkEvents();

            if (IsServer)
            {
                InitializeTableState();
            }

            LogDebug($"Spawned - Total tables: {_allTables.Count}");
        }

        public override void OnNetworkDespawn()
        {
            UnsubscribeFromNetworkEvents();
            UnregisterTable();

            base.OnNetworkDespawn();
        }

        #endregion

        #region Initialization

        private void InitializeTableID()
        {
            if (string.IsNullOrEmpty(tableID))
            {
                tableID = $"Table_{GetInstanceID()}";
            }
        }

        private void InitializeItemPlacePoint()
        {
            if (itemPlacePoint == null)
            {
                itemPlacePoint = transform;
                LogWarning("No itemPlacePoint set, using transform");
            }
        }

        private void InitializeTableState()
        {
            _tableState.Value = new TableState
            {
                isEmpty = true,
                itemNetworkId = 0,
                isItemBoxed = false
            };
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

            foreach (var col in boxColliders)
            {
                if (col.isTrigger)
                {
                    return col;
                }
            }

            // Create new trigger
            var newTrigger = gameObject.AddComponent<BoxCollider>();
            newTrigger.isTrigger = true;
            LogDebug("Box interaction trigger added");

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

        #region Table Registry

        private void RegisterTable()
        {
            if (!_allTables.Contains(this))
            {
                _allTables.Add(this);
            }
        }

        private void UnregisterTable()
        {
            _allTables.Remove(this);
        }

        /// <summary>
        /// ID'ye göre masa bul
        /// </summary>
        public static Table GetTableByID(string id)
        {
            return _allTables.Find(t => t != null && t.tableID == id);
        }

        /// <summary>
        /// Tüm masaları döndür
        /// </summary>
        public static List<Table> GetAllTables()
        {
            _allTables.RemoveAll(t => t == null);
            return new List<Table>(_allTables);
        }

        #endregion

        #region Network Event Subscriptions

        private void SubscribeToNetworkEvents()
        {
            _tableState.OnValueChanged += HandleTableStateChanged;
        }

        private void UnsubscribeFromNetworkEvents()
        {
            _tableState.OnValueChanged -= HandleTableStateChanged;
        }

        #endregion

        #region Range Detection

        /// <summary>
        /// Transform bazlı range kontrolü - HEM HOST HEM CLIENT için çalışır
        /// </summary>
        public bool IsPlayerInRange(Transform playerTransform)
        {
            if (playerTransform == null) return false;

            Vector3 localPoint = transform.InverseTransformPoint(playerTransform.position);
            Vector3 halfSize = interactionBoxSize * 0.5f;

            return IsPointInsideBox(localPoint, interactionBoxOffset, halfSize);
        }

        /// <summary>
        /// ClientId'den Transform bulup kontrol et
        /// </summary>
        private bool IsPlayerInRange(ulong clientId)
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

        #region Network State Management

        private void HandleTableStateChanged(TableState oldState, TableState newState)
        {
            LogDebug($"State changed - isEmpty: {newState.isEmpty}, itemNetworkId: {newState.itemNetworkId}");
            UpdateVisualState(newState);
        }

        private void UpdateVisualState(TableState state)
        {
            if (state.isEmpty)
            {
                ClearVisualItem();
            }
            else
            {
                TryDisplayItem(state.itemNetworkId);
            }
        }

        private void ClearVisualItem()
        {
            if (_currentItemOnTable != null)
            {
                LogDebug("Clearing visual item");
                _currentItemOnTable = null;
            }
        }

        private void TryDisplayItem(ulong itemNetworkId)
        {
            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(itemNetworkId, out NetworkObject itemNetObj))
            {
                return;
            }

            _currentItemOnTable = itemNetObj.gameObject;
            PositionItemOnTable(_currentItemOnTable);
            LogDebug("Item positioned on table");
        }

        private void PositionItemOnTable(GameObject item)
        {
            if (item == null || itemPlacePoint == null) return;

            Vector3 targetPosition = itemPlacePoint.position;
            targetPosition.y += ITEM_HEIGHT_OFFSET;

            item.transform.position = targetPosition;
            item.transform.rotation = itemPlacePoint.rotation;

            // Disable physics
            var rb = item.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }

        private void SetTableState(bool isEmpty, ulong itemNetworkId = 0, bool isBoxed = false)
        {
            _tableState.Value = new TableState
            {
                isEmpty = isEmpty,
                itemNetworkId = itemNetworkId,
                isItemBoxed = isBoxed
            };
        }

        #endregion

        #region Public Interaction API

        /// <summary>
        /// Masa ile etkileşim başlatır
        /// </summary>
        public void InteractWithTable(PlayerInventory player)
        {
            if (!IsServer)
            {
                LogDebug("Client requesting interaction");
                RequestInteractionServerRpc(player.NetworkObjectId);
                return;
            }

            // Host'ta doğrudan çağrılırsa, player.OwnerClientId kullan
            ProcessTableInteraction(player, player.OwnerClientId);
        }

        #endregion

        #region Server RPCs

        [ServerRpc(RequireOwnership = false)]
        private void RequestInteractionServerRpc(ulong playerNetworkId, ServerRpcParams rpcParams = default)
        {
            ulong requesterClientId = rpcParams.Receive.SenderClientId;

            LogDebug($"📥 Interaction requested by client {requesterClientId}");

            if (!ValidateInteractionRequest(requesterClientId, playerNetworkId, out PlayerInventory player))
            {
                return;
            }

            // requesterClientId'yi kullan (rpcParams'tan alınan doğru client ID)
            ProcessTableInteraction(player, requesterClientId);
        }

        private bool ValidateInteractionRequest(ulong clientId, ulong playerNetworkId, out PlayerInventory player)
        {
            player = null;

            // Get player transform
            if (!TryGetPlayerTransform(clientId, out Transform playerTransform))
            {
                LogError($"Player object not found for client {clientId}");
                return false;
            }

            // Range check
            if (!IsPlayerInRange(playerTransform))
            {
                float distance = Vector3.Distance(playerTransform.position, transform.position);
                LogWarning($"Client {clientId} is NOT in range!  Distance: {distance}");
                return false;
            }

            // Get PlayerInventory
            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(playerNetworkId, out NetworkObject playerObj))
            {
                LogError("Player NetworkObject not found");
                return false;
            }

            player = playerObj.GetComponent<PlayerInventory>();
            if (player == null)
            {
                LogError("PlayerInventory component not found");
                return false;
            }

            return true;
        }

        #endregion

        #region Interaction Processing

        private void ProcessTableInteraction(PlayerInventory player, ulong requesterClientId)
        {
            LogDebug($"Processing interaction - Player has item: {player.HasItem}, Table has item: {HasItem}");

            if (player.HasItem)
            {
                ProcessPlayerHasItem(player, requesterClientId);
            }
            else
            {
                ProcessPlayerHasNoItem(player);
            }
        }

        private void ProcessPlayerHasItem(PlayerInventory player, ulong requesterClientId)
        {
            if (CanPlaceItem)
            {
                LogDebug($"✅ Placing item from player {requesterClientId}");
                PlaceItemOnTable(player);
            }
            else
            {
                LogDebug($"🎮 Attempting boxing for player {requesterClientId}");
                TryBoxingInteraction(player, requesterClientId);
            }
        }

        private void ProcessPlayerHasNoItem(PlayerInventory player)
        {
            if (CanTakeItem)
            {
                LogDebug($"✅ Taking item from table for player {player.OwnerClientId}");
                TakeItemFromTable(player);
            }
            else
            {
                LogDebug("⚠️ No interaction possible");
            }
        }

        #endregion

        #region Place Item

        private void PlaceItemOnTable(PlayerInventory player)
        {
            LogDebug($"📤 Placing item for client {player.OwnerClientId}");

            ItemData playerItemData = player.CurrentItemData;
            if (playerItemData == null)
            {
                LogError("Player has no valid item data");
                return;
            }

            // Clear player inventory
            player.SetInventoryStateServerRpc(false, -1);
            player.TriggerDropAnimationServerRpc();

            // Spawn item on table
            StartCoroutine(SpawnItemOnTableCoroutine(playerItemData));

            // Notify tutorial
            NotifyTutorialManager(true);
        }

        private IEnumerator SpawnItemOnTableCoroutine(ItemData itemData)
        {
            yield return new WaitForSeconds(ITEM_SPAWN_DELAY);

            if (itemData.worldPrefab == null)
            {
                LogError($"Item {itemData.itemName} has no world prefab");
                yield break;
            }

            Vector3 spawnPos = CalculateSpawnPosition();
            GameObject worldItem = Instantiate(itemData.worldPrefab, spawnPos, itemPlacePoint.rotation);

            var netObj = worldItem.GetComponent<NetworkObject>();
            if (netObj == null)
            {
                LogError("World item has no NetworkObject component");
                Destroy(worldItem);
                yield break;
            }

            netObj.Spawn();
            ConfigureWorldItem(worldItem, itemData);
            SetTableState(false, netObj.NetworkObjectId, false);

            LogDebug($"✅ Item {itemData.itemName} placed successfully");
        }

        private Vector3 CalculateSpawnPosition()
        {
            Vector3 spawnPos = itemPlacePoint.position;
            spawnPos.y += SPAWN_HEIGHT_OFFSET;
            return spawnPos;
        }

        private void ConfigureWorldItem(GameObject worldItem, ItemData itemData)
        {
            var worldItemComponent = worldItem.GetComponent<NetworkWorldItem>();
            if (worldItemComponent != null)
            {
                worldItemComponent.SetItemData(itemData);
                worldItemComponent.DisablePickup();
            }
        }

        #endregion

        #region Take Item

        private void TakeItemFromTable(PlayerInventory player)
        {
            var state = _tableState.Value;

            if (state.isEmpty)
            {
                LogDebug("⚠️ Cannot take item - table is empty");
                return;
            }

            if (!TryGetTableItem(state.itemNetworkId, out NetworkObject itemNetObj, out NetworkWorldItem worldItem))
            {
                return;
            }

            ItemData itemData = worldItem.ItemData;
            if (itemData == null)
            {
                LogError("Item has no ItemData");
                return;
            }

            // Give item to player and despawn
            player.GiveItemDirectlyServerRpc(itemData.itemID);
            itemNetObj.Despawn(true);

            // Clear table state
            SetTableState(true);

            LogDebug($"✅ Item {itemData.itemName} taken by player {player.OwnerClientId}");

            // Notify tutorial
            NotifyTutorialManager(false);
        }

        private bool TryGetTableItem(ulong itemNetworkId, out NetworkObject itemNetObj, out NetworkWorldItem worldItem)
        {
            itemNetObj = null;
            worldItem = null;

            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(itemNetworkId, out itemNetObj))
            {
                LogError("Item NetworkObject not found");
                return false;
            }

            worldItem = itemNetObj.GetComponent<NetworkWorldItem>();
            if (worldItem == null)
            {
                LogError("NetworkWorldItem component not found");
                return false;
            }

            return true;
        }

        #endregion

        #region Boxing Interaction

        private void TryBoxingInteraction(PlayerInventory player, ulong requesterClientId)
        {
            var state = _tableState.Value;

            // Validation
            if (!ValidateBoxingRequest(player, state, out BoxInfo playerBox, out ProductInfo tableProduct))
            {
                return;
            }

            // Box-Product compatibility check
            if (!IsValidBoxProductCombination(playerBox.boxType, tableProduct.productType))
            {
                LogDebug($"❌ Box type {playerBox.boxType} doesn't match product type {tableProduct.productType}");
                NotifyBoxingFailedClientRpc(requesterClientId);
                return;
            }

            // Find minigame manager
            var minigame = GetComponentInChildren<BoxingMinigameManager>();
            if (minigame == null)
            {
                LogError("BoxingMinigameManager not found!");
                return;
            }

            // Start minigame - requesterClientId kullan (doğru client ID)
            ItemData playerItemData = player.CurrentItemData;
            LogDebug($"🎮 Starting minigame for client {requesterClientId}");
            StartMinigameClientRpc(requesterClientId, player.NetworkObjectId, (int)playerBox.boxType, playerItemData.itemID);
        }

        private bool ValidateBoxingRequest(PlayerInventory player, TableState state, out BoxInfo playerBox, out ProductInfo tableProduct)
        {
            playerBox = null;
            tableProduct = null;

            // Already boxed check
            if (state.isItemBoxed)
            {
                LogDebug("⚠️ Item is already boxed");
                return false;
            }

            // Player item check
            ItemData playerItemData = player.CurrentItemData;
            if (playerItemData?.visualPrefab == null)
            {
                LogDebug("⚠️ Player has no valid item for boxing");
                return false;
            }

            // Box check
            playerBox = playerItemData.visualPrefab.GetComponent<BoxInfo>();
            if (playerBox == null || playerBox.isFull)
            {
                LogDebug("⚠️ Player doesn't have an empty box");
                return false;
            }

            // Table item check
            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(state.itemNetworkId, out NetworkObject tableItemNetObj))
            {
                LogError("Table item not found for boxing");
                return false;
            }

            tableProduct = tableItemNetObj.GetComponent<ProductInfo>();
            if (tableProduct == null)
            {
                LogDebug("⚠️ Table item is not a product, cannot box");
                return false;
            }

            return true;
        }

        private bool IsValidBoxProductCombination(BoxInfo.BoxType boxType, ProductInfo.ProductType productType)
        {
            return (productType == ProductInfo.ProductType.Toy && boxType == BoxInfo.BoxType.Red) ||
                   (productType == ProductInfo.ProductType.Clothing && boxType == BoxInfo.BoxType.Yellow) ||
                   (productType == ProductInfo.ProductType.Glass && boxType == BoxInfo.BoxType.Blue);
        }

        #endregion

        #region Client RPCs

        [ClientRpc]
        private void StartMinigameClientRpc(ulong targetClientId, ulong playerNetworkObjectId, int boxTypeInt, int itemDataID)
        {
            LogDebug($"📥 CLIENT {NetworkManager.Singleton.LocalClientId}: Received StartMinigameClientRpc - Target: {targetClientId}");

            // Only target client processes
            if (NetworkManager.Singleton.LocalClientId != targetClientId)
            {
                LogDebug("⏩ Skipping minigame start - not target client");
                return;
            }

            LogDebug($"🎮 CLIENT {targetClientId}: I AM the target!  Starting minigame...");

            if (!ValidateMinigameStart(targetClientId, playerNetworkObjectId, itemDataID,
                out PlayerInventory player, out ItemData itemData))
            {
                return;
            }

            BoxInfo.BoxType boxType = (BoxInfo.BoxType)boxTypeInt;

            var minigame = GetComponentInChildren<BoxingMinigameManager>();
            if (minigame == null)
            {
                LogError($"CLIENT {targetClientId}: BoxingMinigameManager not found!");
                return;
            }

            LogDebug($"✅ CLIENT {targetClientId}: All checks passed - Starting minigame for {boxType} box");
            minigame.StartMinigame(player, boxType, itemData);
        }

        private bool ValidateMinigameStart(ulong targetClientId, ulong playerNetworkObjectId, int itemDataID,
            out PlayerInventory player, out ItemData itemData)
        {
            player = null;
            itemData = null;

            // Find player
            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(playerNetworkObjectId, out NetworkObject playerObj))
            {
                LogError($"CLIENT {targetClientId}: Player NetworkObject {playerNetworkObjectId} not found!");
                return false;
            }

            player = playerObj.GetComponent<PlayerInventory>();
            if (player == null)
            {
                LogError($"CLIENT {targetClientId}: PlayerInventory not found!");
                return false;
            }

            // Ownership check
            if (!player.IsOwner)
            {
                LogError($"CLIENT {targetClientId}: Player is NOT owned by this client!");
                return false;
            }

            // Find ItemData
            itemData = GetItemDataFromID(itemDataID);
            if (itemData == null)
            {
                LogError($"CLIENT {targetClientId}: ItemData with ID {itemDataID} not found!");
                return false;
            }

            return true;
        }

        [ClientRpc]
        private void NotifyBoxingFailedClientRpc(ulong targetClientId)
        {
            if (NetworkManager.Singleton.LocalClientId == targetClientId)
            {
                LogDebug($"❌ Boxing failed - box and product don't match");
            }
        }

        #endregion

        #region Minigame Callbacks

        /// <summary>
        /// Boxing başarılı olduğunda çağrılır
        /// </summary>
        public void CompleteBoxingSuccess(PlayerInventory player, BoxInfo.BoxType boxType)
        {
            if (!IsServer) return;

            LogDebug($"✅ Boxing SUCCESS for {boxType} by client {player.OwnerClientId}");

            // Clear player inventory
            player.SetInventoryStateServerRpc(false, -1);
            player.TriggerDropAnimationServerRpc();

            // Despawn original product
            DespawnCurrentTableItem();

            // Spawn boxed product
            StartCoroutine(SpawnBoxedProductCoroutine(boxType));

            // Update quest
            NotifyQuestManager(boxType);
        }

        /// <summary>
        /// Boxing başarısız olduğunda çağrılır
        /// </summary>
        public void CompleteBoxingFailure(PlayerInventory player)
        {
            if (!IsServer) return;

            LogDebug($"❌ Boxing FAILED for client {player.OwnerClientId}");

            player.SetInventoryStateServerRpc(false, -1);
            player.TriggerDropAnimationServerRpc();

            NotifyBoxingFailedClientRpc(player.OwnerClientId);
        }

        private void DespawnCurrentTableItem()
        {
            var state = _tableState.Value;

            if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(state.itemNetworkId, out NetworkObject productNetObj))
            {
                productNetObj.Despawn(true);
            }
        }

        private IEnumerator SpawnBoxedProductCoroutine(BoxInfo.BoxType boxType)
        {
            yield return new WaitForSeconds(BOXED_PRODUCT_SPAWN_DELAY);

            ItemData boxedProductData = GetBoxedProductData(boxType);
            if (boxedProductData == null)
            {
                LogError($"Boxed product data not found for {boxType}");
                yield break;
            }

            Vector3 spawnPos = CalculateSpawnPosition();
            GameObject boxedItem = Instantiate(boxedProductData.worldPrefab, spawnPos, itemPlacePoint.rotation);

            var netObj = boxedItem.GetComponent<NetworkObject>();
            if (netObj == null)
            {
                Destroy(boxedItem);
                yield break;
            }

            netObj.Spawn();
            ConfigureWorldItem(boxedItem, boxedProductData);
            SetTableState(false, netObj.NetworkObjectId, true);

            LogDebug($"✅ Boxed product spawned: {boxedProductData.itemName}");
        }

        #endregion

        #region Helper Methods

        private ItemData GetBoxedProductData(BoxInfo.BoxType boxType)
        {
            string itemName = boxType switch
            {
                BoxInfo.BoxType.Red => "RedBoxFull",
                BoxInfo.BoxType.Yellow => "YellowBoxFull",
                BoxInfo.BoxType.Blue => "BlueBoxFull",
                _ => ""
            };

            if (string.IsNullOrEmpty(itemName))
            {
                return null;
            }

            return Resources.Load<ItemData>($"Items/{itemName}");
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

        #endregion

        #region Notifications

        private void NotifyTutorialManager(bool isPlacing)
        {
            TutorialManager.Instance?.OnTableInteraction(isPlacing);
        }

        private void NotifyQuestManager(BoxInfo.BoxType boxType)
        {
            if (QuestManager.Instance != null && IsServer)
            {
                QuestManager.Instance.IncrementQuestProgress(QuestType.PackageBoxes, boxType);
            }
        }

        #endregion

        #region Logging

        private void LogDebug(string message)
        {
            Debug.Log($"{LOG_PREFIX} {tableID}: {message}");
        }

        private void LogWarning(string message)
        {
            Debug.LogWarning($"{LOG_PREFIX} {tableID}: {message}");
        }

        private void LogError(string message)
        {
            Debug.LogError($"{LOG_PREFIX} {tableID}: {message}");
        }

        #endregion

        #region Debug & Editor

        [ContextMenu("Debug Table State")]
        public void DebugTableState()
        {
            Debug.Log($"=== TABLE {tableID} DEBUG ===");
            Debug.Log($"Network State - IsServer: {IsServer}, IsClient: {IsClient}");
            Debug.Log($"Table State - Empty: {IsEmpty}, HasItem: {HasItem}, IsBoxed: {IsItemBoxed}");
            Debug.Log($"NetworkObjectId: {(_tableState.Value.isEmpty ? "None" : _tableState.Value.itemNetworkId.ToString())}");
            Debug.Log($"Current Item Object: {(_currentItemOnTable != null ? _currentItemOnTable.name : "null")}");
            Debug.Log($"============================");
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!showInteractionRange) return;

            DrawInteractionBoxGizmo();
            DrawItemPlacePointGizmo();
            DrawCenterPointGizmo();
        }

        private void DrawInteractionBoxGizmo()
        {
            // Filled box
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);

            var rotationMatrix = Matrix4x4.TRS(
                transform.position + transform.TransformDirection(interactionBoxOffset),
                transform.rotation,
                Vector3.one
            );

            Gizmos.matrix = rotationMatrix;
            Gizmos.DrawCube(Vector3.zero, interactionBoxSize);

            // Wire box
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(Vector3.zero, interactionBoxSize);

            Gizmos.matrix = Matrix4x4.identity;
        }

        private void DrawItemPlacePointGizmo()
        {
            if (itemPlacePoint == null) return;

            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(itemPlacePoint.position, Vector3.one * PLACE_POINT_GIZMO_SIZE);
            Gizmos.DrawLine(transform.position, itemPlacePoint.position);

            UnityEditor.Handles.Label(itemPlacePoint.position + Vector3.up * 0.5f, tableID);
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

    #region Network Serializable Struct

    /// <summary>
    /// Masa durumunu network üzerinden senkronize eden struct
    /// </summary>
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

    #endregion
}