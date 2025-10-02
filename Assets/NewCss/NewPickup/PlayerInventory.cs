using Unity;
using Unity.Netcode;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using NewCss;

public class PlayerInventory : NetworkBehaviour
{
    [Header("Range Detection Settings")] [SerializeField]
    private float detectionRange = 3f;

    [SerializeField] private float updateInterval = 0.02f;
    [SerializeField] private LayerMask itemLayerMask = -1;

    [Header("Detection Center Settings")] [SerializeField]
    private Transform detectionCenter; // Manuel olarak atanabilir

    [SerializeField] private Vector3 detectionOffset = Vector3.up * 1f; // Y ekseni offset'i
    [SerializeField] private bool useCustomDetectionCenter = false; // Inspector'dan kontrol edilebilir


    [Header("Outline Settings")] [SerializeField]
    private Color outlineColor = Color.yellow;

    [SerializeField] private float outlineWidth = 2f;
    [SerializeField] private Outline.Mode outlineMode = Outline.Mode.OutlineAll;

    [Header("References")] [SerializeField]
    private Transform holdPosition;

    [SerializeField] private string holdPositionName = "HoldPosition";
    [SerializeField] private Animator playerAnimator;

    [Header("Drop Settings")] [SerializeField]
    private Transform dropPosition;

    [SerializeField] private string dropPositionName = "DropPosition";
    [SerializeField] private Vector3 defaultDropOffset = Vector3.forward * 1.5f;

    private NetworkVariable<bool> hasItem = new NetworkVariable<bool>(false);
    private NetworkVariable<int> currentItemID = new NetworkVariable<int>(-1);

    private NetworkWorldItem targetedItem;
    private NetworkWorldItem previousTargetedItem;
    private GameObject heldItemVisual;
    private ItemData currentItemData;
    private bool isAnimating = false;
    private bool isProcessingInteraction = false;

    private List<NetworkWorldItem> itemsInRange = new List<NetworkWorldItem>();

    private PlayerMovement playerMovement;

    private Collider[] colliderBuffer = new Collider[30];
    private HashSet<NetworkWorldItem> previousFrameItems = new HashSet<NetworkWorldItem>();
    private Coroutine rangeUpdateCoroutine;
    private float lastUpdateTime;

    void Start()
    {
        ValidateHoldPosition();
        ValidateDropPosition();

        playerMovement = GetComponent<PlayerMovement>();


        if (IsOwner)
        {
            rangeUpdateCoroutine = StartCoroutine(UpdateRangeDetection());
        }
    }

    private void OnDestroy()
    {
        if (rangeUpdateCoroutine != null)
        {
            StopCoroutine(rangeUpdateCoroutine);
        }

        ClearAllOutlines();
    }

    private IEnumerator UpdateRangeDetection()
    {
        while (true)
        {
            UpdateItemsInRange();
            yield return new WaitForSeconds(updateInterval);
        }
    }

    private void UpdateItemsInRange()
    {
        previousFrameItems.Clear();
        previousFrameItems.UnionWith(itemsInRange);

        // YENİ: Detection center pozisyonunu kullan
        Vector3 detectionPos = GetDetectionCenterPosition();
        
        int hitCount = Physics.OverlapSphereNonAlloc(
            detectionPos, // transform.position yerine detectionPos kullan
            detectionRange,
            colliderBuffer,
            itemLayerMask
        );

        HashSet<NetworkWorldItem> currentFrameItems = new HashSet<NetworkWorldItem>();

        for (int i = 0; i < hitCount; i++)
        {
            if (colliderBuffer[i] == null) continue;

            NetworkWorldItem worldItem = colliderBuffer[i].GetComponent<NetworkWorldItem>();
            if (worldItem != null && 
                worldItem.CanBePickedUp && 
                worldItem.NetworkObject != null &&
                worldItem.NetworkObject.IsSpawned &&
                worldItem.ItemData != null)
            {
                currentFrameItems.Add(worldItem);

                if (!previousFrameItems.Contains(worldItem))
                {
                    OnItemEnterRange(worldItem);
                }
            }
        }

        foreach (NetworkWorldItem item in previousFrameItems)
        {
            if (item == null || 
                !item.CanBePickedUp || 
                item.NetworkObject == null || 
                !item.NetworkObject.IsSpawned ||
                !currentFrameItems.Contains(item))
            {
                OnItemExitRange(item);
            }
        }
    }
    private Vector3 GetDetectionCenterPosition()
    {
        if (useCustomDetectionCenter && detectionCenter != null)
        {
            // Manuel olarak atanmış detection center kullan
            return detectionCenter.position;
        }
        else
        {
            // Transform pozisyonu + offset kullan
            return transform.position + detectionOffset;
        }
    }

    public void OnItemEnterRange(NetworkWorldItem item)
    {
        if (!IsOwner) return;

        if (item != null &&
            item.CanBePickedUp &&
            item.NetworkObject != null &&
            item.NetworkObject.IsSpawned &&
            item.ItemData != null && // ÇÖZÜM: ItemData kontrolü ekle
            !itemsInRange.Contains(item))
        {
            Debug.Log($"Item entered range: {item.ItemData.itemName}");
            itemsInRange.Add(item);
            UpdateTargetedItem();
        }
    }

    public void OnItemExitRange(NetworkWorldItem item)
    {
        if (!IsOwner) return;

        if (itemsInRange.Contains(item))
        {
            itemsInRange.Remove(item);
            RemoveOutlineFromItem(item);
            UpdateTargetedItem();
        }
    }

    private void UpdateTargetedItem()
    {
        itemsInRange.RemoveAll(item =>
            item == null || item.NetworkObject == null || !item.NetworkObject.IsSpawned || !item.CanBePickedUp);

        if (previousTargetedItem != null)
        {
            RemoveOutlineFromItem(previousTargetedItem);
        }

        if (itemsInRange.Count > 0)
        {
            NetworkWorldItem closestItem = null;
            float closestDistance = float.MaxValue;
            
            // YENİ: Detection center pozisyonunu mesafe hesaplamasında da kullan
            Vector3 detectionPos = GetDetectionCenterPosition();

            foreach (NetworkWorldItem item in itemsInRange)
            {
                if (item != null && item.CanBePickedUp && item.NetworkObject != null && item.NetworkObject.IsSpawned)
                {
                    float sqrDistance = Vector3.SqrMagnitude(item.transform.position - detectionPos);
                    if (sqrDistance < closestDistance)
                    {
                        closestDistance = sqrDistance;
                        closestItem = item;
                    }
                }
            }

            previousTargetedItem = targetedItem;
            targetedItem = closestItem;

            if (targetedItem != null)
            {
                AddOutlineToItem(targetedItem);
            }
        }
        else
        {
            previousTargetedItem = targetedItem;
            targetedItem = null;
        }

        UpdateUI();
    }

    private void AddOutlineToItem(NetworkWorldItem item)
    {
        if (item == null || !item.CanBePickedUp || item.NetworkObject == null || !item.NetworkObject.IsSpawned) return;

        Outline outline = item.GetComponent<Outline>();
        if (outline == null)
        {
            outline = item.gameObject.AddComponent<Outline>();
        }

        outline.OutlineMode = outlineMode;
        outline.OutlineColor = outlineColor;
        outline.OutlineWidth = outlineWidth;
        outline.enabled = true;
    }

    private void RemoveOutlineFromItem(NetworkWorldItem item)
    {
        if (item == null) return;

        Outline outline = item.GetComponent<Outline>();
        if (outline != null)
        {
            outline.enabled = false;
        }
    }

    private void ClearAllOutlines()
    {
        foreach (NetworkWorldItem item in itemsInRange)
        {
            if (item != null)
            {
                RemoveOutlineFromItem(item);
            }
        }

        if (targetedItem != null)
        {
            RemoveOutlineFromItem(targetedItem);
        }
    }

    private void UpdateUI()
    {
        if (targetedItem != null)
        {
        }
        else
        {
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        HandleInput();
    }

    private Table GetNearbyTable()
    {
        // YENİ: Detection center pozisyonunu kullan
        Vector3 detectionPos = GetDetectionCenterPosition();
        Collider[] colliders = Physics.OverlapSphere(detectionPos, detectionRange);
        
        foreach (var collider in colliders)
        {
            Table table = collider.GetComponent<Table>();
            if (table != null)
            {
                return table;
            }
        }
        return null;
    }

    // Yeni method: Yakındaki shelf'i bul
    private ShelfState GetNearbyShelf()
    {
        // YENİ: Detection center pozisyonunu kullan
        Vector3 detectionPos = GetDetectionCenterPosition();
        Collider[] colliders = Physics.OverlapSphere(detectionPos, detectionRange);
        
        foreach (var collider in colliders)
        {
            ShelfState shelf = collider.GetComponent<ShelfState>();
            if (shelf != null)
            {
                return shelf;
            }
        }
        return null;
    }

    // Yeni method: Box tipini kontrol et
    private bool CanPlaceBoxOnShelf(ShelfState shelf)
    {
        if (currentItemData == null) return false;

        // Önce held item visual'dan kontrol et
        if (heldItemVisual != null)
        {
            BoxInfo boxInfo = heldItemVisual.GetComponent<BoxInfo>();
            if (boxInfo != null)
            {
                Debug.Log($"Box found in held visual: {boxInfo.boxType}, isFull: {boxInfo.isFull}");
                return boxInfo.isFull;
            }
        }

        // Visual yoksa item data'nın prefab'ından kontrol et
        if (currentItemData.visualPrefab != null)
        {
            BoxInfo boxInfo = currentItemData.visualPrefab.GetComponent<BoxInfo>();
            if (boxInfo != null)
            {
                Debug.Log($"Box found in item data: {boxInfo.boxType}, isFull: {boxInfo.isFull}");
                return boxInfo.isFull;
            }
        }

        // World prefab'dan da kontrol et
        if (currentItemData.worldPrefab != null)
        {
            BoxInfo boxInfo = currentItemData.worldPrefab.GetComponent<BoxInfo>();
            if (boxInfo != null)
            {
                Debug.Log($"Box found in world prefab: {boxInfo.boxType}, isFull: {boxInfo.isFull}");
                return boxInfo.isFull;
            }
        }

        Debug.Log("No BoxInfo component found on item");
        return false;
    }

    private Vector3 GetDropPosition()
    {
        if (dropPosition != null)
        {
            return dropPosition.position;
        }
        else
        {
            return transform.position + transform.TransformDirection(defaultDropOffset);
        }
    }

    public void DropItemToPosition(Vector3 position, System.Action<NetworkObject> onDropped)
    {
        if (hasItem.Value)
        {
            StartCoroutine(DropItemToPositionCoroutine(position, onDropped));
        }
    }

    private IEnumerator DropItemToPositionCoroutine(Vector3 position, System.Action<NetworkObject> onDropped)
    {
        if (currentItemData != null)
        {
            GameObject worldItemPrefab = GetWorldItemPrefab(currentItemData);

            if (worldItemPrefab != null)
            {
                GameObject spawnedItem = Instantiate(worldItemPrefab, position, Quaternion.identity);
                NetworkObject networkObject = spawnedItem.GetComponent<NetworkObject>();

                if (networkObject != null)
                {
                    networkObject.Spawn();

                    NetworkWorldItem worldItem = spawnedItem.GetComponent<NetworkWorldItem>();
                    if (worldItem != null)
                    {
                        worldItem.SetItemData(currentItemData);
                        worldItem.EnablePickup();
                    }

                    hasItem.Value = false;
                    currentItemID.Value = -1;

                    if (playerMovement != null)
                    {
                        playerMovement.SetCarrying(false);
                    }

                    DestroyHeldItemVisual();

                    onDropped?.Invoke(networkObject);
                }
            }
        }

        yield return null;
    }

    public void PickupItemFromTable(NetworkObject itemNetworkObject, System.Action onPickedUp)
    {
        if (!hasItem.Value)
        {
            StartCoroutine(PickupItemFromTableCoroutine(itemNetworkObject, onPickedUp));
        }
    }

    private IEnumerator PickupItemFromTableCoroutine(NetworkObject itemNetworkObject, System.Action onPickedUp)
    {
        NetworkWorldItem worldItem = itemNetworkObject.GetComponent<NetworkWorldItem>();
        if (worldItem != null)
        {
            ItemData itemData = worldItem.ItemData;

            itemNetworkObject.Despawn();

            hasItem.Value = true;
            currentItemID.Value = GetItemID(itemData);

            if (playerMovement != null)
            {
                playerMovement.SetCarrying(true);
            }

            yield return new WaitForSeconds(0.01f);
            SpawnHeldItemVisual();

            onPickedUp?.Invoke();
        }

        yield return null;
    }

    private IEnumerator ResetInteractionFlag()
    {
        yield return new WaitForSeconds(0.1f);
        isProcessingInteraction = false;
    }

    [ServerRpc(RequireOwnership = false)]
    public void ClearCurrentItemServerRpc()
    {
        if (hasItem.Value)
        {
            hasItem.Value = false;
            currentItemID.Value = -1;

            if (playerMovement != null)
            {
                playerMovement.SetCarrying(false);
            }

            ClearHeldItemVisualClientRpc();
        }
    }

    [ClientRpc]
    private void ClearHeldItemVisualClientRpc()
    {
        DestroyHeldItemVisual();
    }

    private void HandleInput()
    {
        if (isProcessingInteraction)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            Debug.Log(
                $"E pressed. HasItem: {hasItem.Value}, TargetedItem: {(targetedItem != null ? targetedItem.name : "null")}");

            // Önce shelf'den alma kontrolü yap
            if (!hasItem.Value)
            {
                ShelfState nearbyShelf = GetNearbyShelf();
                if (nearbyShelf != null && nearbyShelf.HasItem())
                {
                    isProcessingInteraction = true;
                    RequestTakeFromShelfServerRpc();
                    return;
                }
            }

            // YENİ ÖNCELIK SIRASI: Önce targeted item kontrolü yap
            if (!hasItem.Value && targetedItem != null)
            {
                Debug.Log($"Attempting to pickup targeted item: {targetedItem.name}");
                isProcessingInteraction = true;
                RequestPickupServerRpc(targetedItem.NetworkObjectId);
                return; // Burada return ekliyoruz ki table kontrolüne geçmesin
            }

            // Table kontrolü - sadece targeted item yoksa
            Table nearbyTable = GetNearbyTable();
            if (nearbyTable != null)
            {
                isProcessingInteraction = true;
                nearbyTable.InteractWithTable(this);
                StartCoroutine(ResetInteractionFlag());
            }
            else
            {
                Debug.Log("No valid interaction target found");
            }
        }

        if (Input.GetKeyDown(KeyCode.F))
        {
            if (hasItem.Value && !isAnimating)
            {
                // Debug için shelf kontrolü
                ShelfState nearbyShelf = GetNearbyShelf();
                Debug.Log($"F pressed - Nearby shelf: {(nearbyShelf != null ? "Found" : "Not found")}");

                if (nearbyShelf != null)
                {
                    bool canPlace = CanPlaceBoxOnShelf(nearbyShelf);
                    Debug.Log($"Can place box on shelf: {canPlace}");

                    if (canPlace)
                    {
                        Debug.Log("Attempting to place on shelf...");
                        // Rafa yerleştir
                        isProcessingInteraction = true;
                        RequestPlaceOnShelfServerRpc();
                    }
                    else
                    {
                        Debug.Log("Cannot place box - doing normal drop");
                        // Normal drop
                        isProcessingInteraction = true;
                        RequestDropServerRpc();
                    }
                }
                else
                {
                    Debug.Log("No shelf nearby - doing normal drop");
                    // Normal drop
                    isProcessingInteraction = true;
                    RequestDropServerRpc();
                }
            }
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (hasItem.Value && !isAnimating)
            {
                Vector3 throwDirection = (transform.forward + Vector3.up * 0.3f).normalized;
                isProcessingInteraction = true;
                RequestThrowServerRpc(throwDirection);
            }
        }
    }

    // Yeni ServerRpc: Raftan alma
    [ServerRpc]
    private void RequestTakeFromShelfServerRpc()
    {
        if (hasItem.Value)
        {
            ResetProcessingInteractionClientRpc();
            return;
        }

        ShelfState nearbyShelf = GetNearbyShelf();
        if (nearbyShelf == null || !nearbyShelf.HasItem())
        {
            Debug.Log("No shelf nearby or shelf is empty!");
            ResetProcessingInteractionClientRpc();
            return;
        }

        // Shelf'den item al
        nearbyShelf.TakeItemFromShelfServerRpc();

        ResetProcessingInteractionClientRpc();
    }

    // Yeni ServerRpc: Rafa yerleştirme
    [ServerRpc]
    private void RequestPlaceOnShelfServerRpc()
    {
        if (!hasItem.Value)
        {
            Debug.Log("No item to place on shelf!");
            ResetProcessingInteractionClientRpc();
            return;
        }

        ShelfState nearbyShelf = GetNearbyShelf();
        if (nearbyShelf == null)
        {
            Debug.Log("No nearby shelf found!");
            ResetProcessingInteractionClientRpc();
            return;
        }

        if (nearbyShelf.IsFull())
        {
            Debug.Log("Shelf is full!");
            ResetProcessingInteractionClientRpc();
            return;
        }

        if (!CanPlaceBoxOnShelf(nearbyShelf))
        {
            Debug.Log("Can only place full boxes on shelf!");
            ResetProcessingInteractionClientRpc();
            return;
        }

        // World item spawn et
        GameObject worldItemPrefab = GetWorldItemPrefab(currentItemData);
        if (worldItemPrefab != null)
        {
            Vector3 spawnPos = transform.position + Vector3.up * 0.5f; // Geçici pozisyon
            GameObject spawnedItem = Instantiate(worldItemPrefab, spawnPos, Quaternion.identity);
            NetworkObject networkObject = spawnedItem.GetComponent<NetworkObject>();

            if (networkObject != null)
            {
                networkObject.Spawn();

                NetworkWorldItem worldItem = spawnedItem.GetComponent<NetworkWorldItem>();
                if (worldItem != null)
                {
                    worldItem.SetItemData(currentItemData);

                    // BoxInfo'yu koru
                    BoxInfo worldBoxInfo = spawnedItem.GetComponent<BoxInfo>();
                    if (worldBoxInfo != null && heldItemVisual != null)
                    {
                        BoxInfo heldBoxInfo = heldItemVisual.GetComponent<BoxInfo>();
                        if (heldBoxInfo != null)
                        {
                            worldBoxInfo.isFull = heldBoxInfo.isFull;
                            worldBoxInfo.boxType = heldBoxInfo.boxType;
                        }
                    }

                    worldItem.DisablePickup(); // Rafta olan itemlar alınamaz
                }

                // Shelf'e yerleştir - doğru parametreyi gönder
                Debug.Log("Calling PlaceItemOnShelfServerRpc...");
                nearbyShelf.PlaceItemOnShelfServerRpc(new NetworkObjectReference(networkObject));

                // Player'dan item'ı kaldır
                hasItem.Value = false;
                currentItemID.Value = -1;

                if (playerMovement != null)
                {
                    playerMovement.SetCarrying(false);
                }

                StartDropAnimationClientRpc();
                Debug.Log("Item placed on shelf successfully!");
            }
            else
            {
                Debug.LogError("Failed to get NetworkObject component!");
            }
        }
        else
        {
            Debug.LogError("World item prefab is null!");
        }

        ResetProcessingInteractionClientRpc();
    }

    [ServerRpc]
    private void RequestPickupServerRpc(ulong itemNetworkId)
    {
        Debug.Log($"RequestPickupServerRpc called for item: {itemNetworkId}");

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(itemNetworkId,
                out NetworkObject networkObject))
        {
            NetworkWorldItem worldItem = networkObject.GetComponent<NetworkWorldItem>();

            if (worldItem != null && worldItem.CanBePickedUp && !hasItem.Value)
            {
                Debug.Log($"Attempting to pickup item: {worldItem.ItemData?.itemName}");

                ItemData itemData = worldItem.ItemData;

                // ÇÖZÜM: Extra null check
                if (itemData != null)
                {
                    hasItem.Value = true;
                    currentItemID.Value = GetItemID(itemData);

                    OnItemPickedUpClientRpc(itemNetworkId);
                    StartCoroutine(DelayedDespawn(worldItem, itemData));

                    Debug.Log($"Item picked up successfully: {itemData.itemName}");
                }
                else
                {
                    Debug.LogError("ItemData is null!");
                }
            }
            else
            {
                Debug.LogWarning(
                    $"Cannot pickup item. CanBePickedUp: {worldItem?.CanBePickedUp}, HasItem: {hasItem.Value}");
            }
        }
        else
        {
            Debug.LogError($"NetworkObject not found for ID: {itemNetworkId}");
        }

        ResetProcessingInteractionClientRpc();
    }

    private IEnumerator DelayedDespawn(NetworkWorldItem worldItem, ItemData itemData)
    {
        yield return new WaitForSeconds(0.1f);

        if (worldItem != null && worldItem.NetworkObject != null && worldItem.NetworkObject.IsSpawned)
        {
            worldItem.NetworkObject.Despawn();
        }

        StartPickupAnimationClientRpc();
    }

    [ServerRpc]
    private void RequestDropServerRpc()
    {
        if (hasItem.Value)
        {
            Vector3 dropPos = GetDropPosition();
            SpawnWorldItem(dropPos, Vector3.zero);

            hasItem.Value = false;
            currentItemID.Value = -1;

            StartDropAnimationClientRpc();
        }

        ResetProcessingInteractionClientRpc();
    }

    [ServerRpc]
    private void RequestThrowServerRpc(Vector3 throwDirection)
    {
        if (hasItem.Value)
        {
            Vector3 throwPos = GetDropPosition() + Vector3.up * 1.0f;

            float throwForceAmount = currentItemData != null ? currentItemData.throwForce : 15f;
            Vector3 throwForce = throwDirection * throwForceAmount;

            SpawnWorldItem(throwPos, throwForce);

            hasItem.Value = false;
            currentItemID.Value = -1;

            StartThrowAnimationClientRpc();
        }

        ResetProcessingInteractionClientRpc();
    }

    [ClientRpc]
    private void OnItemPickedUpClientRpc(ulong itemNetworkId)
    {
        NetworkWorldItem itemToRemove = null;

        for (int i = itemsInRange.Count - 1; i >= 0; i--)
        {
            if (itemsInRange[i] != null && itemsInRange[i].NetworkObjectId == itemNetworkId)
            {
                itemToRemove = itemsInRange[i];
                itemsInRange.RemoveAt(i);
                break;
            }
        }

        if (targetedItem != null && targetedItem.NetworkObjectId == itemNetworkId)
        {
            RemoveOutlineFromItem(targetedItem);
            targetedItem = null;
            UpdateTargetedItem();
        }

        if (itemToRemove != null)
        {
            RemoveOutlineFromItem(itemToRemove);

            if (itemToRemove.gameObject != null)
            {
                itemToRemove.gameObject.SetActive(false);
            }
        }

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(itemNetworkId,
                out NetworkObject networkObject))
        {
            if (networkObject != null && networkObject.gameObject != null)
            {
                networkObject.gameObject.SetActive(false);
            }
        }
    }

    [ClientRpc]
    private void ResetProcessingInteractionClientRpc()
    {
        isProcessingInteraction = false;
    }

    [ClientRpc]
    private void StartPickupAnimationClientRpc()
    {
        StartCoroutine(PickupAnimation());
    }

    [ClientRpc]
    private void StartDropAnimationClientRpc()
    {
        StartCoroutine(DropAnimation());
    }

    [ClientRpc]
    private void StartThrowAnimationClientRpc()
    {
        StartCoroutine(ThrowAnimationWithSync());
    }

    private IEnumerator PickupAnimation()
    {
        isAnimating = true;

        if (playerMovement != null)
        {
            playerMovement.SetCarrying(true);
        }

        float timeout = 0f;
        while (currentItemData == null && timeout < 2f)
        {
            timeout += Time.deltaTime;
            yield return null;
        }

        if (currentItemData != null)
        {
            Debug.Log($"Spawning visual for item: {currentItemData.itemName}");
            SpawnHeldItemVisual();
        }
        else
        {
            Debug.LogError("Failed to get currentItemData for visual spawning");
        }

        isAnimating = false;
    }

    private IEnumerator DropAnimation()
    {
        isAnimating = true;

        if (playerMovement != null)
        {
            playerMovement.SetCarrying(false);
        }

        DestroyHeldItemVisual();

        yield return new WaitForSeconds(0.05f);

        isAnimating = false;
    }

    private IEnumerator ThrowAnimationWithSync()
    {
        isAnimating = true;

        if (playerMovement != null)
        {
            playerMovement.SetCarrying(false);
        }

        yield return new WaitForSeconds(0.05f);

        float timeoutTimer = 0f;
        const float timeout = 2f;

        while (hasItem.Value && timeoutTimer < timeout)
        {
            timeoutTimer += Time.deltaTime;
            yield return null;
        }

        DestroyHeldItemVisual();

        yield return new WaitForSeconds(0.05f);

        isAnimating = false;
    }

    private void OnHasItemChanged(bool previousValue, bool newValue)
    {
        if (newValue && !previousValue)
        {
            if (!IsOwner)
            {
                StartCoroutine(PickupAnimation());
            }
        }
        else if (!newValue && previousValue)
        {
            if (!IsOwner)
            {
                StartCoroutine(DropAnimation());
            }
        }
    }

    private void OnEnable()
    {
        hasItem.OnValueChanged += OnHasItemChanged;
        currentItemID.OnValueChanged += OnCurrentItemChanged;
    }

    private void OnDisable()
    {
        hasItem.OnValueChanged -= OnHasItemChanged;
        currentItemID.OnValueChanged -= OnCurrentItemChanged;

        ClearAllOutlines();
    }

    private void OnCurrentItemChanged(int previousValue, int newValue)
    {
        if (newValue != -1)
        {
            currentItemData = GetItemDataFromID(newValue);

            if (hasItem.Value && heldItemVisual == null && currentItemData != null)
            {
                SpawnHeldItemVisual();
            }
        }
        else
        {
            currentItemData = null;
        }
    }

    private void ValidateHoldPosition()
    {
        if (holdPosition == null)
        {
            FindHoldPositionByName();
        }
    }

    private void ValidateDropPosition()
    {
        if (dropPosition == null)
        {
            FindDropPositionByName();
        }
    }

    private void FindDropPositionByName()
    {
        dropPosition = transform.Find(dropPositionName);

        if (dropPosition == null)
        {
            Transform[] childTransforms = GetComponentsInChildren<Transform>();
            foreach (Transform child in childTransforms)
            {
                if (child.name == dropPositionName)
                {
                    dropPosition = child;
                    break;
                }
            }
        }

        if (dropPosition == null)
        {
            GameObject dropObject = GameObject.Find(dropPositionName);
            if (dropObject != null)
            {
                dropPosition = dropObject.transform;
            }
        }

        if (dropPosition == null)
        {
            Debug.LogWarning($"Drop position '{dropPositionName}' not found! Using default offset.");
        }
    }

    private void FindHoldPositionByName()
    {
        holdPosition = transform.Find(holdPositionName);

        if (holdPosition == null)
        {
            Transform[] childTransforms = GetComponentsInChildren<Transform>();
            foreach (Transform child in childTransforms)
            {
                if (child.name == holdPositionName)
                {
                    holdPosition = child;
                    break;
                }
            }
        }

        if (holdPosition == null)
        {
            GameObject holdObject = GameObject.Find(holdPositionName);
            if (holdObject != null)
            {
                holdPosition = holdObject.transform;
            }
        }

        if (holdPosition == null)
        {
            Transform handTransform = transform.Find("Hand");
            if (handTransform != null)
            {
                holdPosition = handTransform;
            }
        }
    }

    private void SpawnHeldItemVisual()
    {
        if (currentItemData != null && holdPosition != null)
        {
            DestroyHeldItemVisual();

            if (currentItemData.visualPrefab != null)
            {
                heldItemVisual = Instantiate(currentItemData.visualPrefab, holdPosition);
                heldItemVisual.transform.SetParent(holdPosition, false);
                heldItemVisual.transform.localPosition = Vector3.zero;
                heldItemVisual.transform.localRotation = Quaternion.identity;

                // BoxInfo durumunu koru
                PreserveBoxInfo();

                DisablePhysicsComponents(heldItemVisual);
                DisableColliders(heldItemVisual);
                SetLayerRecursively(heldItemVisual, LayerMask.NameToLayer("Default"));
            }
        }
    }

    // Yeni method: BoxInfo durumunu koruma
    private void PreserveBoxInfo()
    {
        if (heldItemVisual == null) return;

        BoxInfo heldBoxInfo = heldItemVisual.GetComponent<BoxInfo>();
        if (heldBoxInfo != null)
        {
            // Eğer bu item daha önce rafa konmuşsa, muhtemelen dolu bir box'tı
            // ItemData'dan veya başka bir kaynaktan bu bilgiyi almaya çalış

            // Geçici çözüm: Eğer item name'inde "Full" geçiyorsa dolu kabul et
            if (currentItemData.itemName.ToLower().Contains("full") ||
                currentItemData.itemName.ToLower().Contains("dolu"))
            {
                heldBoxInfo.isFull = true;
                Debug.Log($"Set box as full based on item name: {currentItemData.itemName}");
            }
            else
            {
                // Varsayılan olarak prefab'daki değeri koru
                BoxInfo originalBoxInfo = currentItemData.visualPrefab.GetComponent<BoxInfo>();
                if (originalBoxInfo != null)
                {
                    heldBoxInfo.isFull = originalBoxInfo.isFull;
                    heldBoxInfo.boxType = originalBoxInfo.boxType;
                    Debug.Log($"Preserved box info from prefab: isFull={heldBoxInfo.isFull}");
                }
            }
        }
    }

    private void DisablePhysicsComponents(GameObject obj)
    {
        Rigidbody[] rigidbodies = obj.GetComponentsInChildren<Rigidbody>();
        foreach (Rigidbody rb in rigidbodies)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.detectCollisions = false;
        }

        Rigidbody2D[] rigidbodies2D = obj.GetComponentsInChildren<Rigidbody2D>();
        foreach (Rigidbody2D rb2d in rigidbodies2D)
        {
            rb2d.isKinematic = true;
            rb2d.gravityScale = 0;
        }

        Joint[] joints = obj.GetComponentsInChildren<Joint>();
        foreach (Joint joint in joints)
        {
            DestroyImmediate(joint);
        }
    }

    private void DisableColliders(GameObject obj)
    {
        Collider[] colliders = obj.GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }

        Collider2D[] colliders2D = obj.GetComponentsInChildren<Collider2D>();
        foreach (Collider2D col2d in colliders2D)
        {
            col2d.enabled = false;
        }
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    private void DestroyHeldItemVisual()
    {
        if (heldItemVisual != null)
        {
            Destroy(heldItemVisual);
            heldItemVisual = null;
        }
    }

    private void SpawnWorldItem(Vector3 position, Vector3 force)
    {
        if (currentItemData != null)
        {
            GameObject worldItemPrefab = GetWorldItemPrefab(currentItemData);

            if (worldItemPrefab != null)
            {
                GameObject spawnedItem = Instantiate(worldItemPrefab, position, Quaternion.identity);
                NetworkObject networkObject = spawnedItem.GetComponent<NetworkObject>();

                if (networkObject != null)
                {
                    networkObject.Spawn();

                    NetworkWorldItem worldItem = spawnedItem.GetComponent<NetworkWorldItem>();
                    if (worldItem != null)
                    {
                        worldItem.SetItemData(currentItemData);

                        // BoxInfo durumunu koru
                        PreserveBoxInfoOnWorldItem(worldItem.gameObject);

                        // ÇÖZÜM: Pickup'ı delay ile etkinleştir
                        StartCoroutine(DelayedEnablePickup(worldItem));

                        if (force != Vector3.zero)
                        {
                            worldItem.SetThrowForce(force);
                        }
                    }
                }
            }
        }
    }

    // 2. YENİ METHOD: Delayed pickup enable
    private IEnumerator DelayedEnablePickup(NetworkWorldItem worldItem)
    {
        // İtem spawn olduktan sonra kısa bir süre bekle
        yield return new WaitForSeconds(0.2f);

        if (worldItem != null && worldItem.NetworkObject != null && worldItem.NetworkObject.IsSpawned)
        {
            worldItem.EnablePickup();
            Debug.Log($"Pickup enabled for item: {worldItem.ItemData?.itemName}");
        }
    }

// 3. YENİ METHOD: World item'da box info'yu koruma
    private void PreserveBoxInfoOnWorldItem(GameObject worldItem)
    {
        if (heldItemVisual == null) return;

        BoxInfo heldBoxInfo = heldItemVisual.GetComponent<BoxInfo>();
        BoxInfo worldBoxInfo = worldItem.GetComponent<BoxInfo>();

        if (heldBoxInfo != null && worldBoxInfo != null)
        {
            worldBoxInfo.isFull = heldBoxInfo.isFull;
            worldBoxInfo.boxType = heldBoxInfo.boxType;
            Debug.Log($"Preserved box info on world item: isFull={worldBoxInfo.isFull}");
        }
    }

    private GameObject GetWorldItemPrefab(ItemData itemData)
    {
        return itemData.worldPrefab;
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
    [ServerRpc(RequireOwnership = false)]
    public void SetInventoryStateServerRpc(bool hasItemValue, int itemID)
    {
        hasItem.Value = hasItemValue;
        currentItemID.Value = itemID;

        if (playerMovement != null)
        {
            playerMovement.SetCarrying(hasItemValue);
        }

        // Eğer item temizleniyorsa, tüm client'larda visual'ı temizle
        if (!hasItemValue)
        {
            ClearHeldItemVisualClientRpc();
        }

        Debug.Log($"Inventory state set: hasItem={hasItemValue}, itemID={itemID}");
    }


    [ServerRpc(RequireOwnership = false)]
    public void TriggerDropAnimationServerRpc()
    {
        // Tüm client'larda drop animasyonunu başlat
        StartDropAnimationClientRpc();
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void GiveItemDirectlyServerRpc(int itemID)
    {
        if (hasItem.Value) return; // Zaten item var

        ItemData itemData = GetItemDataFromID(itemID);
        if (itemData == null) return;

        // Item'ı oyuncuya ver
        hasItem.Value = true;
        currentItemID.Value = itemID;

        if (playerMovement != null)
        {
            playerMovement.SetCarrying(true);
        }

        // Client'lara animation başlatması için bildir
        StartPickupAnimationClientRpc();
    
        Debug.Log($"Item given directly to player: {itemData.itemName}");
    }

    private int GetItemID(ItemData itemData)
    {
        if (itemData == null)
        {
            return -1;
        }

        return itemData.itemID;
    }

    private void OnDrawGizmosSelected()
    {
        // YENİ: Detection center'dan gizmo çiz
        Vector3 detectionPos = GetDetectionCenterPosition();
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(detectionPos, detectionRange);

        // Detection center'ı göster
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(detectionPos, Vector3.one * 0.2f);

        if (targetedItem != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(detectionPos, targetedItem.transform.position);
        }

        if (dropPosition != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(dropPosition.position, Vector3.one * 0.5f);
            Gizmos.DrawLine(transform.position, dropPosition.position);
        }
        
        // Detection offset'ini göster
        if (!useCustomDetectionCenter)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, detectionPos);
        }
    }

    public bool HasItem => hasItem.Value;
    public ItemData CurrentItemData => currentItemData;
    public Transform HoldPosition => holdPosition;
    public Transform DropPosition => dropPosition;
    public NetworkWorldItem TargetedItem => targetedItem;
    public bool IsProcessingInteraction => isProcessingInteraction;
    public float DetectionRange => detectionRange;

    public Color OutlineColor
    {
        get => outlineColor;
        set => outlineColor = value;
    }

    public float OutlineWidth
    {
        get => outlineWidth;
        set => outlineWidth = value;
    }

    public Outline.Mode OutlineMode
    {
        get => outlineMode;
        set => outlineMode = value;
    }
}