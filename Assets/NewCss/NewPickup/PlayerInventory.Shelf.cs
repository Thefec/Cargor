using NewCss;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;



public partial class PlayerInventory : NetworkBehaviour
{
    #region Shelf Item System

    private void UpdateShelfItemSystem()
    {
        var nearbyShelf = GetNearbyShelf();
        var nearbyTutorialShelf = GetNearbyTutorialShelf(); // YENİ

        // Normal ShelfState veya TutorialShelfState kontrolü
        bool hasNormalShelfItems = nearbyShelf != null && nearbyShelf.HasItem() && !_hasItem.Value;
        bool hasTutorialShelfItems = nearbyTutorialShelf != null && nearbyTutorialShelf.HasItem && !_hasItem.Value;

        bool shouldShowShelfItems = hasNormalShelfItems || hasTutorialShelfItems;

        if (shouldShowShelfItems)
        {
            // Initialize shelf items if not already done
            if (_availableShelfItems.Count == 0)
            {
                UpdateTargetedShelfItemWithTutorialSupport(); // YENİ METOD
            }

            HandleMouseWheelInput();
        }
        else
        {
            ClearShelfItemTargeting();
        }
    }
    private void UpdateTargetedShelfItemWithTutorialSupport()
    {
        // Clear previous outline
        if (_previousTargetedShelfItem != null)
        {
            try
            {
                if (_previousTargetedShelfItem.gameObject != null)
                {
                    RemoveOutlineFromItem(_previousTargetedShelfItem);
                }
            }
            catch { /* Destroyed object, ignore */ }
        }

        // Rebuild available items list
        _availableShelfItems.Clear();

        // Normal ShelfState'ten item'ları al
        var nearbyShelf = GetNearbyShelf();
        if (nearbyShelf != null && nearbyShelf.HasItem() && !_hasItem.Value)
        {
            var shelfItems = nearbyShelf.GetAllShelfItems();
            if (shelfItems != null)
            {
                foreach (var item in shelfItems)
                {
                    if (IsValidWorldItem(item))
                    {
                        _availableShelfItems.Add(item);
                    }
                }
            }
        }

        // TutorialShelfState'ten item'ları al
        var nearbyTutorialShelf = GetNearbyTutorialShelf();
        if (nearbyTutorialShelf != null && nearbyTutorialShelf.HasItem && !_hasItem.Value)
        {
            var tutorialShelfItems = nearbyTutorialShelf.GetAllShelfItems();
            if (tutorialShelfItems != null)
            {
                foreach (var item in tutorialShelfItems)
                {
                    if (IsValidWorldItem(item) && !_availableShelfItems.Contains(item))
                    {
                        _availableShelfItems.Add(item);
                    }
                }
            }
        }

        if (_availableShelfItems.Count == 0)
        {
            ClearShelfItemTargeting();
            return;
        }

        // Clamp index to valid range
        _currentShelfItemIndex = Mathf.Clamp(_currentShelfItemIndex, 0, _availableShelfItems.Count - 1);

        // Update targeted item
        _previousTargetedShelfItem = _targetedShelfItem;
        _targetedShelfItem = _availableShelfItems[_currentShelfItemIndex];

        if (IsValidWorldItem(_targetedShelfItem))
        {
            AddOutlineToItem(_targetedShelfItem);
            Debug.Log($"[PlayerInventory] Targeted shelf item [{_currentShelfItemIndex + 1}/{_availableShelfItems.Count}]: {_targetedShelfItem.ItemData?.itemName}");
        }
    }

    private void UpdateTargetedShelfItem()
    {
        UpdateTargetedShelfItemWithTutorialSupport();
    }

    private void HandleMouseWheelInput()
    {
        if (_availableShelfItems.Count == 0) return;

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) <= MIN_SCROLL_THRESHOLD) return;

        // Clear previous outline
        if (_targetedShelfItem != null)
        {
            RemoveOutlineFromItem(_targetedShelfItem);
        }

        // Update index with wrapping
        if (scroll > 0f)
        {
            _currentShelfItemIndex = (_currentShelfItemIndex + 1) % _availableShelfItems.Count;
            Debug.Log($"[PlayerInventory] Scroll UP - Index: {_currentShelfItemIndex}/{_availableShelfItems.Count}");
        }
        else
        {
            _currentShelfItemIndex = (_currentShelfItemIndex - 1 + _availableShelfItems.Count) % _availableShelfItems.Count;
            Debug.Log($"[PlayerInventory] Scroll DOWN - Index: {_currentShelfItemIndex}/{_availableShelfItems.Count}");
        }

        // Apply new outline
        if (_currentShelfItemIndex >= 0 && _currentShelfItemIndex < _availableShelfItems.Count)
        {
            _targetedShelfItem = _availableShelfItems[_currentShelfItemIndex];
            if (_targetedShelfItem != null)
            {
                AddOutlineToItem(_targetedShelfItem);
                Debug.Log($"[PlayerInventory] Selected item: {_targetedShelfItem.ItemData?.itemName}");
            }
        }
    }

    private void ClearShelfItemTargeting()
    {
        // ✅ DEĞİŞTİRİLDİ: Güvenli cleanup
        if (_targetedShelfItem != null)
        {
            try
            {
                if (_targetedShelfItem.gameObject != null)
                {
                    RemoveOutlineFromItem(_targetedShelfItem);
                }
            }
            catch { /* Destroyed, ignore */ }
            _targetedShelfItem = null;
        }

        if (_previousTargetedShelfItem != null)
        {
            try
            {
                if (_previousTargetedShelfItem.gameObject != null)
                {
                    RemoveOutlineFromItem(_previousTargetedShelfItem);
                }
            }
            catch { /* Destroyed, ignore */ }
            _previousTargetedShelfItem = null;
        }

        foreach (var item in _availableShelfItems.ToArray())
        {
            if (item != null)
            {
                try
                {
                    if (item.gameObject != null)
                    {
                        RemoveOutlineFromItem(item);
                    }
                }
                catch { /* Destroyed, ignore */ }
            }
        }

        _availableShelfItems.Clear();
        _currentShelfItemIndex = 0;
    }

    #endregion
    #region Nearby Object Detection

    private Table GetNearbyTable()
    {
        var detectionPos = GetDetectionCenterPosition();
        var colliders = Physics.OverlapSphere(detectionPos, detectionRange);

        foreach (var collider in colliders)
        {
            var table = collider.GetComponent<Table>();
            if (table != null && IsPositionInCone(table.transform.position))
            {
                return table;
            }
        }

        return null;
    }

    private ShelfState GetNearbyShelf()
    {
        return FindNearestShelfForTransform(transform);
    }
    private TutorialShelfState GetNearbyTutorialShelf()
    {
        return FindNearestTutorialShelfForTransform(transform);
    }
    private TutorialShelfState FindNearestTutorialShelfForTransform(Transform playerTransform)
    {
        if (playerTransform == null) return null;

        var colliders = Physics.OverlapSphere(playerTransform.position, detectionRange);
        TutorialShelfState closestShelf = null;
        float closestDistance = float.MaxValue;

        foreach (var collider in colliders)
        {
            var shelf = collider.GetComponent<TutorialShelfState>();
            if (shelf != null && shelf.IsPlayerInRange(playerTransform))
            {
                float distance = Vector3.Distance(playerTransform.position, shelf.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestShelf = shelf;
                }
            }
        }

        if (closestShelf != null)
        {
            Debug.Log($"[PlayerInventory] Found nearby TutorialShelf at distance: {closestDistance:F2}");
        }

        return closestShelf;
    }

    private ShelfState FindNearestShelfForTransform(Transform playerTransform)
    {
        if (playerTransform == null) return null;

        var colliders = Physics.OverlapSphere(playerTransform.position, detectionRange);
        ShelfState closestShelf = null;
        float closestDistance = float.MaxValue;

        foreach (var collider in colliders)
        {
            var shelf = collider.GetComponent<ShelfState>();
            if (shelf != null && shelf.IsPlayerInRange(playerTransform))
            {
                float distance = Vector3.Distance(playerTransform.position, shelf.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestShelf = shelf;
                }
            }
        }

        if (closestShelf != null)
        {
            Debug.Log($"[PlayerInventory] Found nearby shelf at distance: {closestDistance:F2}");
        }

        return closestShelf;
    }

    private ShelfState FindNearbyShelfForPosition(Vector3 position, float range)
    {
        var colliders = Physics.OverlapSphere(position, range);
        ShelfState closestShelf = null;
        float closestDistance = float.MaxValue;

        foreach (var collider in colliders)
        {
            var shelf = collider.GetComponent<ShelfState>();
            if (shelf != null)
            {
                float distance = Vector3.Distance(position, shelf.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestShelf = shelf;
                }
            }
        }

        return closestShelf;
    }

    private NetworkedShelf GetNearbyNetworkedShelf()
    {
        var detectionPos = GetDetectionCenterPosition();
        var colliders = Physics.OverlapSphere(detectionPos, detectionRange);

        foreach (var collider in colliders)
        {
            var networkedShelf = collider.GetComponent<NetworkedShelf>();
            if (networkedShelf != null && IsPositionInCone(networkedShelf.transform.position))
            {
                return networkedShelf;
            }
        }

        return null;
    }

    private bool CanPlaceBoxOnShelf(ShelfState shelf)
    {
        if (_currentItemData == null) return false;

        // ✅ YENİ: Önce ItemData üzerinden kontrol et (Server-side için)
        // Item adından kontrol
        var itemName = _currentItemData.itemName.ToLower();
        if (itemName.Contains("full") || itemName.Contains("dolu") || itemName.Contains("boxfull"))
        {
            Debug.Log($"[PlayerInventory] CanPlaceBoxOnShelf: TRUE (item name contains full/dolu)");
            return true;
        }

        // ✅ YENİ: Prefab'lardan BoxInfo kontrolü (Server-side için çalışır)
        var prefabsToCheck = new[] { _currentItemData.worldPrefab, _currentItemData.visualPrefab };

        foreach (var prefab in prefabsToCheck)
        {
            if (prefab != null)
            {
                var boxInfo = prefab.GetComponent<BoxInfo>();
                if (boxInfo != null)
                {
                    Debug.Log($"[PlayerInventory] CanPlaceBoxOnShelf: Prefab BoxInfo found - isFull: {boxInfo.isFull}");
                    return boxInfo.isFull;
                }
            }
        }

        // Held item visual kontrolü (Client-side için)
        if (_heldItemVisual != null)
        {
            var boxInfo = _heldItemVisual.GetComponent<BoxInfo>();
            if (boxInfo != null)
            {
                Debug.Log($"[PlayerInventory] CanPlaceBoxOnShelf: HeldItemVisual BoxInfo - isFull: {boxInfo.isFull}");
                return boxInfo.isFull;
            }
        }

        Debug.Log("[PlayerInventory] CanPlaceBoxOnShelf: FALSE (no BoxInfo found)");
        return false;
    }

    #endregion
    #region Server RPCs - Shelf Interaction
    [ServerRpc(RequireOwnership = false)]
    private void RequestTakeFromShelfServerRpc(ulong itemNetworkId, ServerRpcParams rpcParams = default)
    {
        var requesterClientId = rpcParams.Receive.SenderClientId;
        Debug.Log($"[PlayerInventory] Server: Client {requesterClientId} wants to take item {itemNetworkId} from shelf");

        if (_hasItem.Value)
        {
            Debug.LogWarning($"[PlayerInventory] Client {requesterClientId} already has an item!");
            ResetProcessingInteractionForClientRpc(requesterClientId);
            return;
        }

        // Get player transform
        if (!TryGetPlayerTransform(requesterClientId, out var playerTransform))
        {
            Debug.LogError($"[PlayerInventory] Player transform not found for client {requesterClientId}");
            ResetProcessingInteractionForClientRpc(requesterClientId);
            return;
        }

        // Find shelf
        var nearbyShelf = FindNearestShelfForTransform(playerTransform);
        if (nearbyShelf == null)
        {
            Debug.LogError($"[PlayerInventory] No shelf found near client {requesterClientId}!");
            ResetProcessingInteractionForClientRpc(requesterClientId);
            return;
        }

        if (!nearbyShelf.HasItem())
        {
            Debug.LogWarning("[PlayerInventory] Shelf is empty!");
            ResetProcessingInteractionForClientRpc(requesterClientId);
            return;
        }

        Debug.Log("[PlayerInventory] Calling ShelfState.TakeItemFromShelfServerRpc");

        try
        {
            nearbyShelf.TakeItemFromShelfServerRpc(requesterClientId, itemNetworkId, rpcParams);

            // Client'a shelf item targeting'i temizlemesini söyle
            ClearShelfTargetingClientRpc(requesterClientId);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PlayerInventory] Error in TakeItemFromShelfServerRpc: {e.Message}\n{e.StackTrace}");
        }
        finally
        {
            ResetProcessingInteractionForClientRpc(requesterClientId);
        }
    }
    [ClientRpc]
    private void ClearShelfTargetingClientRpc(ulong targetClientId)
    {
        if (NetworkManager.Singleton.LocalClientId != targetClientId) return;

        Debug.Log($"[PlayerInventory] Client {targetClientId}: Clearing shelf targeting after take");
        ClearShelfItemTargeting();
    }

    private bool CanPlaceBoxOnShelfServerSide(ItemData itemData)
    {
        if (itemData == null) return false;

        // Item adından kontrol
        var itemName = itemData.itemName.ToLower();
        if (itemName.Contains("full") || itemName.Contains("dolu") || itemName.Contains("boxfull"))
        {
            Debug.Log($"[PlayerInventory] Server: Item name indicates full box: {itemData.itemName}");
            return true;
        }

        // Prefab'lardan BoxInfo kontrolü
        var prefabsToCheck = new[] { itemData.worldPrefab, itemData.visualPrefab };

        foreach (var prefab in prefabsToCheck)
        {
            if (prefab != null)
            {
                var boxInfo = prefab.GetComponent<BoxInfo>();
                if (boxInfo != null)
                {
                    Debug.Log($"[PlayerInventory] Server: Prefab BoxInfo found - isFull: {boxInfo.isFull}, type: {boxInfo.boxType}");
                    return boxInfo.isFull;
                }
            }
        }

        Debug.Log("[PlayerInventory] Server: No BoxInfo found in prefabs");
        return false;
    }
    private ShelfState FindNearbyShelfWithRangeCheck(Transform playerTransform)
    {
        if (playerTransform == null) return null;

        var colliders = Physics.OverlapSphere(playerTransform.position, detectionRange + 2f); // Biraz daha geniş ara
        ShelfState closestShelf = null;
        float closestDistance = float.MaxValue;

        foreach (var collider in colliders)
        {
            var shelf = collider.GetComponent<ShelfState>();
            if (shelf == null) continue;

            // ✅ ÖNEMLİ: IsPlayerInRange kontrolü
            if (!shelf.IsPlayerInRange(playerTransform))
            {
                Debug.Log($"[PlayerInventory] Shelf {shelf.name} found but player NOT in range");
                continue;
            }

            float distance = Vector3.Distance(playerTransform.position, shelf.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestShelf = shelf;
            }
        }

        if (closestShelf != null)
        {
            Debug.Log($"[PlayerInventory] ✅ Found shelf {closestShelf.name} at distance {closestDistance:F2}");
        }

        return closestShelf;
    }

    /// <summary>
    /// Server tarafında normal drop işlemi
    /// </summary>
    private void PerformNormalDropServer(Transform playerTransform, ulong clientId)
    {
        if (_currentItemData == null)
        {
            Debug.LogError("[PlayerInventory] ❌ PerformNormalDropServer: _currentItemData is null!");
            ResetProcessingInteractionForClientRpc(clientId);
            return;
        }

        var worldItemPrefab = GetWorldItemPrefab(_currentItemData);
        if (worldItemPrefab == null)
        {
            Debug.LogError("[PlayerInventory] ❌ PerformNormalDropServer: worldItemPrefab is null!");
            ResetProcessingInteractionForClientRpc(clientId);
            return;
        }

        Vector3 dropPos = playerTransform.position + playerTransform.forward * 1.5f;
        dropPos.y += 0.5f;

        var spawnedItem = Instantiate(worldItemPrefab, dropPos, Quaternion.identity);
        var networkObject = spawnedItem.GetComponent<NetworkObject>();

        if (networkObject != null)
        {
            networkObject.Spawn();

            var worldItem = spawnedItem.GetComponent<NetworkWorldItem>();
            if (worldItem != null)
            {
                worldItem.SetItemData(_currentItemData);
                StartCoroutine(DelayedEnablePickup(worldItem));
            }

            Debug.Log($"[PlayerInventory] ✅ Normal drop completed at {dropPos}");
        }

        // Inventory temizle
        ClearInventoryState();

        // Client'a özel bildirimler
        ClearHeldItemForClientRpc(clientId);
        StartDropAnimationForClientRpc(clientId);
        ResetProcessingInteractionForClientRpc(clientId);
    }

    [ClientRpc]
    private void ResetProcessingInteractionForClientRpc(ulong targetClientId)
    {
        if (NetworkManager.Singleton.LocalClientId == targetClientId)
        {
            _isProcessingInteraction = false;
            Debug.Log($"[PlayerInventory] Client {targetClientId}: _isProcessingInteraction reset to false");
        }
    }

    [ClientRpc]
    private void ClearHeldItemForClientRpc(ulong targetClientId)
    {
        if (NetworkManager.Singleton.LocalClientId == targetClientId)
        {
            DestroyHeldItemVisual();
            Debug.Log($"[PlayerInventory] Client {targetClientId}: Held item visual cleared");
        }
    }

    [ClientRpc]
    private void StartDropAnimationForClientRpc(ulong targetClientId)
    {
        if (NetworkManager.Singleton.LocalClientId == targetClientId)
        {
            SafeStartAnimation(DropAnimationCoroutine());
        }
    }

    [ClientRpc]
    private void StartPickupAnimationForClientRpc(ulong targetClientId)
    {
        if (NetworkManager.Singleton.LocalClientId == targetClientId)
        {
            SafeStartAnimation(PickupAnimationCoroutine());
        }
    }


    [ServerRpc(RequireOwnership = false)]
    private void RequestPlaceOnShelfServerRpc(ServerRpcParams rpcParams = default)
    {
        var requesterClientId = rpcParams.Receive.SenderClientId;
        Debug.Log($"[PlayerInventory] 📥 SERVER: RequestPlaceOnShelfServerRpc - Client {requesterClientId}");

        // Player kontrolü
        if (!_hasItem.Value)
        {
            Debug.Log($"[PlayerInventory] ❌ Client {requesterClientId} has no item to place!");
            ResetProcessingInteractionForClientRpc(requesterClientId);
            return;
        }

        // Player transform bul
        if (!TryGetPlayerTransform(requesterClientId, out var playerTransform))
        {
            Debug.LogError($"[PlayerInventory] ❌ Player transform not found for client {requesterClientId}");
            ResetProcessingInteractionForClientRpc(requesterClientId);
            return;
        }

        Debug.Log($"[PlayerInventory] Found player at position {playerTransform.position}");

        // Shelf bul
        var nearbyShelf = FindNearbyShelfWithRangeCheck(playerTransform);
        if (nearbyShelf == null)
        {
            Debug.LogWarning($"[PlayerInventory] ⚠️ No shelf in range for client {requesterClientId} - doing normal drop");
            PerformNormalDropServer(playerTransform, requesterClientId);
            return;
        }

        Debug.Log($"[PlayerInventory] ✅ Found shelf: {nearbyShelf.name}");

        // Shelf dolu mu?
        if (nearbyShelf.IsFull())
        {
            Debug.Log("[PlayerInventory] ❌ Shelf is FULL!  - doing normal drop");
            PerformNormalDropServer(playerTransform, requesterClientId);
            return;
        }

        // Server-side BoxInfo kontrolü
        if (!CanPlaceBoxOnShelfServerSide(_currentItemData))
        {
            Debug.Log("[PlayerInventory] ❌ Can only place FULL boxes on shelf!  - doing normal drop");
            PerformNormalDropServer(playerTransform, requesterClientId);
            return;
        }

        // World item spawn et
        var worldItemPrefab = GetWorldItemPrefab(_currentItemData);
        if (worldItemPrefab == null)
        {
            Debug.LogError("[PlayerInventory] ❌ World item prefab is NULL!");
            ResetProcessingInteractionForClientRpc(requesterClientId);
            return;
        }

        var spawnPos = playerTransform.position + Vector3.up * 0.5f;
        var spawnedItem = Instantiate(worldItemPrefab, spawnPos, Quaternion.identity);
        var networkObject = spawnedItem.GetComponent<NetworkObject>();

        if (networkObject == null)
        {
            Debug.LogError("[PlayerInventory] ❌ NetworkObject component missing!");
            Destroy(spawnedItem);
            ResetProcessingInteractionForClientRpc(requesterClientId);
            return;
        }

        networkObject.Spawn();

        // World item ayarla
        var worldItem = spawnedItem.GetComponent<NetworkWorldItem>();
        if (worldItem != null)
        {
            worldItem.SetItemData(_currentItemData);

            // BoxInfo'yu prefab'dan kopyala
            var worldBoxInfo = spawnedItem.GetComponent<BoxInfo>();
            var prefabBoxInfo = worldItemPrefab.GetComponent<BoxInfo>();
            if (worldBoxInfo != null && prefabBoxInfo != null)
            {
                worldBoxInfo.isFull = true;
                worldBoxInfo.boxType = prefabBoxInfo.boxType;
            }

            worldItem.DisablePickup();
        }

        Debug.Log("[PlayerInventory] ✅ Calling ShelfState.PlaceItemOnShelfFromServer");
        nearbyShelf.PlaceItemOnShelfFromServer(new NetworkObjectReference(networkObject), requesterClientId);

        // Player inventory temizle
        ClearInventoryState();

        // Tüm client'lara animasyon bildir
        ClearHeldItemForClientRpc(requesterClientId);
        StartDropAnimationForClientRpc(requesterClientId);


        Debug.Log($"[PlayerInventory] ✅ Item placed on shelf successfully by client {requesterClientId}!");
        ResetProcessingInteractionForClientRpc(requesterClientId);
    }

    private bool TryGetPlayerTransform(ulong clientId, out Transform playerTransform)
    {
        playerTransform = null;

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
        {
            Debug.LogError($"[PlayerInventory] Client {clientId} not found in ConnectedClients!");
            return false;
        }

        if (client.PlayerObject == null)
        {
            Debug.LogError($"[PlayerInventory] PlayerObject is null for client {clientId}!");
            return false;
        }

        playerTransform = client.PlayerObject.transform;
        Debug.Log($"[PlayerInventory] Found player transform for client {clientId} at {playerTransform.position}");
        return true;
    }

    private void CopyBoxInfoToWorldItem(GameObject spawnedItem)
    {
        var worldBoxInfo = spawnedItem.GetComponent<BoxInfo>();
        if (worldBoxInfo == null || _heldItemVisual == null) return;

        var heldBoxInfo = _heldItemVisual.GetComponent<BoxInfo>();
        if (heldBoxInfo != null)
        {
            worldBoxInfo.isFull = heldBoxInfo.isFull;
            worldBoxInfo.boxType = heldBoxInfo.boxType;
            Debug.Log($"[PlayerInventory] BoxInfo copied - isFull: {worldBoxInfo.isFull}, type: {worldBoxInfo.boxType}");
        }
    }

    #endregion
}