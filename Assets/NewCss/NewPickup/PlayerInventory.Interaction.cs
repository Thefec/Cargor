using NewCss;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;



public partial class PlayerInventory : NetworkBehaviour
{
    #region Input Handling

    private void HandleInput()
    {
        if (_isProcessingInteraction) return;

        // Animasyon devam ederken input'ları engelle
        if (_isAnimating) return;

        // Spam koruması
        if (Time.time - _lastInputTime < INPUT_SPAM_COOLDOWN) return;

        if (InputBindingManager.GetActionDown(InputBindingManager.GameAction.Pickup))
        {
            _lastInputTime = Time.time;
            HandlePickupInteraction();
        }
        else if (InputBindingManager.GetActionDown(InputBindingManager.GameAction.Drop))
        {
            _lastInputTime = Time.time;
            HandleDropInteraction();
        }
        else if (InputBindingManager.GetActionDown(InputBindingManager.GameAction.Interact))
        {
            _lastInputTime = Time.time;
            HandleEInteraction();
        }
    }

    private void HandleEInteraction()
    {
        // Burada sadece müşteri, telefon vb gibi E tuşuna özel etkileşimler olabilir.
        // Masa vb eşya işlemleri Left/Right click'e taşındı.
    }
    [ServerRpc(RequireOwnership = false)]
    private void RequestTakeFromTutorialShelfServerRpc(ulong itemNetworkId, ServerRpcParams rpcParams = default)
    {
        var requesterClientId = rpcParams.Receive.SenderClientId;
        Debug.Log($"[PlayerInventory] Server: Client {requesterClientId} wants to take item {itemNetworkId} from TutorialShelf");

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

        // Find TutorialShelfState
        var nearbyTutorialShelf = FindNearbyTutorialShelfWithRangeCheck(playerTransform);
        if (nearbyTutorialShelf == null)
        {
            Debug.LogError($"[PlayerInventory] No TutorialShelf found near client {requesterClientId}!");
            ResetProcessingInteractionForClientRpc(requesterClientId);
            return;
        }

        if (!nearbyTutorialShelf.HasItem)
        {
            Debug.LogWarning("[PlayerInventory] TutorialShelf is empty!");
            ResetProcessingInteractionForClientRpc(requesterClientId);
            return;
        }

        Debug.Log("[PlayerInventory] Calling TutorialShelfState.TakeItemFromShelfServerRpc");

        try
        {
            nearbyTutorialShelf.TakeItemFromShelfServerRpc(requesterClientId, itemNetworkId, rpcParams);

            // Client'a shelf item targeting'i temizlemesini söyle
            ClearShelfTargetingClientRpc(requesterClientId);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PlayerInventory] Error in TakeItemFromTutorialShelfServerRpc: {e.Message}\n{e.StackTrace}");
        }
        finally
        {
            ResetProcessingInteractionForClientRpc(requesterClientId);
        }
    }

    private void HandlePickupInteraction()
    {
        // ✅ YENİ: Null ve destroyed check
        string targetedItemName = "null";
        string targetedShelfItemName = "null";

        try
        {
            if (_targetedItem != null && _targetedItem.gameObject != null)
                targetedItemName = _targetedItem.name;
        }
        catch { targetedItemName = "destroyed"; }

        try
        {
            if (_targetedShelfItem != null && _targetedShelfItem.gameObject != null)
                targetedShelfItemName = _targetedShelfItem.name;
        }
        catch { targetedShelfItemName = "destroyed"; }

        Debug.Log($"[PlayerInventory] E pressed.  HasItem: {_hasItem.Value}, TargetedItem: {targetedItemName}, TargetedShelfItem: {targetedShelfItemName}");

        if (!_hasItem.Value)
        {
            // Priority 1: Shelf item (selected via mouse wheel) - Normal ShelfState veya TutorialShelfState
            if (IsValidShelfItem(_targetedShelfItem))
            {
                // Önce TutorialShelfState kontrol et
                var nearbyTutorialShelf = GetNearbyTutorialShelf();
                if (nearbyTutorialShelf != null && nearbyTutorialShelf.HasItem)
                {
                    _isProcessingInteraction = true;
                    RequestTakeFromTutorialShelfServerRpc(_targetedShelfItem.NetworkObject.NetworkObjectId);
                    PlaySound(SoundType.TakeFromShelf);
                    return;
                }

                // Sonra normal ShelfState kontrol et
                var nearbyShelf = GetNearbyShelf();
                if (nearbyShelf != null)
                {
                    _isProcessingInteraction = true;
                    RequestTakeFromShelfServerRpc(_targetedShelfItem.NetworkObject.NetworkObjectId);
                    PlaySound(SoundType.TakeFromShelf);
                    return;
                }
            }

            // Priority 2: Ground item
            if (IsValidWorldItem(_targetedItem))
            {
                Debug.Log($"[PlayerInventory] Attempting to pickup targeted item: {_targetedItem.name}");
                _isProcessingInteraction = true;
                RequestPickupServerRpc(_targetedItem.NetworkObjectId);
                PlaySound(SoundType.Pickup);
                return;
            }
        }

        // Priority 3: Table interaction (Masadan alma)
        if (!_hasItem.Value)
        {
            var nearbyTable = GetNearbyTable();
            if (nearbyTable != null)
            {
                _isProcessingInteraction = true;
                PlaySound(SoundType.TakeFromTable);
                nearbyTable.InteractWithTable(this);
                StartCoroutine(ResetInteractionFlagAfterDelay());
                return;
            }
        }

        Debug.Log("[PlayerInventory] No valid pickup target found");
    }

    private bool IsValidWorldItem(NetworkWorldItem item)
    {
        if (item == null) return false;

        try
        {
            // GameObject destroyed mu kontrol et
            if (item.gameObject == null) return false;

            // NetworkObject kontrol et
            if (item.NetworkObject == null) return false;
            if (!item.NetworkObject.IsSpawned) return false;

            return true;
        }
        catch
        {
            return false;
        }
    }
    private bool IsValidShelfItem(NetworkWorldItem item)
    {
        if (!IsValidWorldItem(item)) return false;

        try
        {
            // NetworkObjectId erişilebilir mi?
            _ = item.NetworkObject.NetworkObjectId;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void HandleDropInteraction()
    {
        if (!_hasItem.Value || _isAnimating) return;

        var nearbyShelf = GetNearbyShelf();
        var nearbyTutorialShelf = GetNearbyTutorialShelf(); // YENİ
        var networkedShelf = GetNearbyNetworkedShelf();

        Debug.Log($"[PlayerInventory] F pressed - HasItem: {_hasItem.Value}, ItemData: {_currentItemData?.itemName ?? "null"}");
        Debug.Log($"[PlayerInventory] Shelf: {(nearbyShelf != null ? nearbyShelf.name : "None")}, TutorialShelf: {(nearbyTutorialShelf != null ? nearbyTutorialShelf.name : "None")}, NetworkedShelf: {(networkedShelf != null ? "Found" : "None")}");

        // NetworkedShelf kısıtlaması kontrolü
        if (networkedShelf != null && !networkedShelf.CanPlaceItems())
        {
            Debug.Log("[PlayerInventory] Cannot place items on NetworkedShelf - doing normal drop");
            PerformNormalDrop();
            return;
        }

        // YENİ: TutorialShelfState yerleştirme - ÖNCE KONTROL ET
        if (nearbyTutorialShelf != null && !nearbyTutorialShelf.IsFull)
        {
            Debug.Log($"[PlayerInventory] TutorialShelf found, sending to server for validation");
            _isProcessingInteraction = true;
            RequestPlaceOnTutorialShelfServerRpc(); // YENİ RPC
            PlaySound(SoundType.PlaceOnShelf);
            return;
        }

        // ShelfState yerleştirme
        if (nearbyShelf != null)
        {
            Debug.Log($"[PlayerInventory] Shelf found, sending to server for validation");
            _isProcessingInteraction = true;
            RequestPlaceOnShelfServerRpc();
            PlaySound(SoundType.PlaceOnShelf);
            return;
        }

        // Table yerleştirme (Masaya koyma)
        var nearbyTable = GetNearbyTable();
        if (nearbyTable != null)
        {
            _isProcessingInteraction = true;
            PlaySound(SoundType.PlaceOnTable);
            nearbyTable.InteractWithTable(this);
            StartCoroutine(ResetInteractionFlagAfterDelay());
            return;
        }

        // Default: Normal drop
        PerformNormalDrop();
    }
    #region Server RPCs - Tutorial Shelf Interaction

    [ServerRpc(RequireOwnership = false)]
    private void RequestPlaceOnTutorialShelfServerRpc(ServerRpcParams rpcParams = default)
    {
        var requesterClientId = rpcParams.Receive.SenderClientId;
        Debug.Log($"[PlayerInventory] 📥 SERVER: RequestPlaceOnTutorialShelfServerRpc - Client {requesterClientId}");

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

        // TutorialShelfState bul
        var nearbyTutorialShelf = FindNearbyTutorialShelfWithRangeCheck(playerTransform);
        if (nearbyTutorialShelf == null)
        {
            Debug.LogWarning($"[PlayerInventory] ⚠️ No TutorialShelf in range for client {requesterClientId} - doing normal drop");
            PerformNormalDropServer(playerTransform, requesterClientId);
            return;
        }

        Debug.Log($"[PlayerInventory] ✅ Found TutorialShelf: {nearbyTutorialShelf.name}");

        // Shelf dolu mu? 
        if (nearbyTutorialShelf.IsFull)
        {
            Debug.Log("[PlayerInventory] ❌ TutorialShelf is FULL!  - doing normal drop");
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
                worldBoxInfo.isFull = prefabBoxInfo.isFull;
                worldBoxInfo.boxType = prefabBoxInfo.boxType;
            }

            worldItem.DisablePickup();
        }

        Debug.Log("[PlayerInventory] ✅ Calling TutorialShelfState.PlaceItemOnShelfFromServer");
        nearbyTutorialShelf.PlaceItemOnShelfFromServer(new NetworkObjectReference(networkObject), requesterClientId);

        // Player inventory temizle
        ClearInventoryState();

        // Client'lara animasyon bildir
        ClearHeldItemForClientRpc(requesterClientId);
        StartDropAnimationForClientRpc(requesterClientId);

        Debug.Log($"[PlayerInventory] ✅ Item placed on TutorialShelf successfully by client {requesterClientId}!");
        ResetProcessingInteractionForClientRpc(requesterClientId);
    }

    private TutorialShelfState FindNearbyTutorialShelfWithRangeCheck(Transform playerTransform)
    {
        if (playerTransform == null) return null;

        var colliders = Physics.OverlapSphere(playerTransform.position, detectionRange + 2f);
        TutorialShelfState closestShelf = null;
        float closestDistance = float.MaxValue;

        foreach (var collider in colliders)
        {
            var shelf = collider.GetComponent<TutorialShelfState>();
            if (shelf == null) continue;

            if (!shelf.IsPlayerInRange(playerTransform))
            {
                Debug.Log($"[PlayerInventory] TutorialShelf {shelf.name} found but player NOT in range");
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
            Debug.Log($"[PlayerInventory] ✅ Found TutorialShelf {closestShelf.name} at distance {closestDistance:F2}");
        }

        return closestShelf;
    }

    #endregion

    private void HandleThrowInteraction()
    {
        if (!_hasItem.Value || _isAnimating) return;

        var throwDirection = (transform.forward + Vector3.up * 0.3f).normalized;
        _isProcessingInteraction = true;
        RequestThrowServerRpc(throwDirection);
        PlaySound(SoundType.Drop);
    }

    private void PerformNormalDrop()
    {
        Debug.Log("[PlayerInventory] Performing normal drop");
        _isProcessingInteraction = true;
        RequestDropServerRpc();
        PlaySound(SoundType.Drop);
        StartCoroutine(ResetInteractionFlagAfterDelay());
    }

    private IEnumerator ResetInteractionFlagAfterDelay()
    {
        yield return new WaitForSeconds(INTERACTION_COOLDOWN);
        _isProcessingInteraction = false;
    }



    #endregion
    #region Server RPCs - Pickup

    [ServerRpc]
    private void RequestPickupServerRpc(ulong itemNetworkId)
    {
        Debug.Log($"[PlayerInventory] RequestPickupServerRpc called for item: {itemNetworkId}");

        // Validate lock
        if (IsItemLocked(itemNetworkId))
        {
            Debug.LogWarning($"[PlayerInventory] Item {itemNetworkId} is locked!");
            ResetProcessingInteractionClientRpc();
            return;
        }

        // Validate network object
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(itemNetworkId, out var networkObject))
        {
            Debug.LogError($"[PlayerInventory] NetworkObject not found for ID: {itemNetworkId}");
            ResetProcessingInteractionClientRpc();
            return;
        }

        if (networkObject == null || !networkObject.IsSpawned)
        {
            Debug.LogWarning($"[PlayerInventory] Item {itemNetworkId} is already despawned!");
            ResetProcessingInteractionClientRpc();
            return;
        }

        var worldItem = networkObject.GetComponent<NetworkWorldItem>();
        if (worldItem == null || !worldItem.CanBePickedUp || _hasItem.Value)
        {
            Debug.LogWarning($"[PlayerInventory] Cannot pickup: WorldItem={worldItem != null}, CanPickup={worldItem?.CanBePickedUp}, HasItem={_hasItem.Value}");
            ResetProcessingInteractionClientRpc();
            return;
        }

        // Try to lock item
        if (!TryLockItem(itemNetworkId))
        {
            Debug.LogWarning($"[PlayerInventory] Failed to lock item {itemNetworkId}");
            ResetProcessingInteractionClientRpc();
            return;
        }

        Debug.Log($"[PlayerInventory] Item {itemNetworkId} locked for client {OwnerClientId}");

        var itemData = worldItem.ItemData;
        if (itemData == null)
        {
            Debug.LogError("[PlayerInventory] ItemData is null!");
            UnlockItem(itemNetworkId);
            worldItem.EnablePickup();
            ResetProcessingInteractionClientRpc();
            return;
        }

        // Update state
        _hasItem.Value = true;
        _currentItemID.Value = GetItemID(itemData);

        OnItemPickedUpClientRpc(itemNetworkId);
        StartCoroutine(DelayedDespawnAndUnlock(worldItem, itemNetworkId));

        Debug.Log($"[PlayerInventory] Item picked up successfully: {itemData.itemName}");
        ResetProcessingInteractionClientRpc();
    }

    private IEnumerator DelayedDespawnAndUnlock(NetworkWorldItem worldItem, ulong itemNetworkId)
    {
        yield return new WaitForSeconds(DELAYED_DESPAWN_TIME);

        if (worldItem != null && worldItem.NetworkObject != null && worldItem.NetworkObject.IsSpawned)
        {
            worldItem.NetworkObject.Despawn();
            Debug.Log($"[PlayerInventory] Item {itemNetworkId} despawned");
        }

        UnlockItem(itemNetworkId);
        StartPickupAnimationClientRpc();
    }

    #endregion
    #region Server RPCs - Drop & Throw

    [ServerRpc]
    private void RequestDropServerRpc()
    {
        if (!_hasItem.Value)
        {
            ResetProcessingInteractionClientRpc();
            return;
        }

        var dropPos = GetDropPositionInternal();
        SpawnWorldItemAtPosition(dropPos, Vector3.zero);

        ClearInventoryState();
        StartDropAnimationClientRpc();
        ResetProcessingInteractionClientRpc();
    }

    [ServerRpc]
    private void RequestThrowServerRpc(Vector3 throwDirection)
    {
        if (!_hasItem.Value)
        {
            ResetProcessingInteractionClientRpc();
            return;
        }

        var throwPos = GetDropPositionInternal() + Vector3.up;
        float throwForce = _currentItemData?.throwForce ?? 15f;
        var force = throwDirection * throwForce;

        SpawnWorldItemAtPosition(throwPos, force);

        ClearInventoryState();
        StartThrowAnimationClientRpc();
        ResetProcessingInteractionClientRpc();
    }

    private Vector3 GetDropPositionInternal()
    {
        return dropPosition != null
            ? dropPosition.position
            : transform.position + transform.TransformDirection(defaultDropOffset);
    }

    private void ClearInventoryState()
    {
        _hasItem.Value = false;
        _currentItemID.Value = -1;
    }

    #endregion
}