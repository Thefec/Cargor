using NewCss;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;



public partial class PlayerInventory : NetworkBehaviour
{
    #region Animation Coroutines

    private IEnumerator PickupAnimationCoroutine()
    {
        _isAnimating = true;

        _playerMovement?.SetCarrying(true);

        // Wait for item data to sync
        float timeout = 0f;
        while (_currentItemData == null && timeout < ANIMATION_TIMEOUT)
        {
            timeout += Time.deltaTime;
            yield return null;
        }

        if (_currentItemData != null)
        {
            Debug.Log($"[PlayerInventory] Spawning visual for item: {_currentItemData.itemName}");
            SpawnHeldItemVisual();
        }
        else
        {
            Debug.LogError("[PlayerInventory] Failed to get currentItemData for visual spawning");
            // Item data gelmezse carrying state'i sıfırla
            _playerMovement?.SetCarrying(false);
        }

        _isAnimating = false;
        _currentAnimationCoroutine = null;
    }

    private IEnumerator DropAnimationCoroutine()
    {
        _isAnimating = true;

        // Önce visual'ı temizle
        DestroyHeldItemVisual();

        // Sonra carrying state'i kapat
        _playerMovement?.SetCarrying(false);

        yield return new WaitForSeconds(0.05f);

        _isAnimating = false;
        _currentAnimationCoroutine = null;

        // Güvenlik kontrolü - _hasItem false ise carrying kesinlikle false olmalı
        if (!_hasItem.Value)
        {
            _playerMovement?.SetCarrying(false);
        }
    }

    private IEnumerator ThrowAnimationCoroutine()
    {
        _isAnimating = true;

        // Önce visual'ı temizle
        DestroyHeldItemVisual();

        // Carrying state'i hemen kapat
        _playerMovement?.SetCarrying(false);

        yield return new WaitForSeconds(0.05f);

        // Wait for network sync
        float timeout = 0f;
        while (_hasItem.Value && timeout < ANIMATION_TIMEOUT)
        {
            timeout += Time.deltaTime;
            yield return null;
        }

        yield return new WaitForSeconds(0.05f);

        _isAnimating = false;
        _currentAnimationCoroutine = null;

        // Güvenlik kontrolü
        if (!_hasItem.Value)
        {
            _playerMovement?.SetCarrying(false);
        }
    }

    private void SafeStartAnimation(IEnumerator animationCoroutine)
    {
        // Önceki animasyon varsa iptal et
        if (_currentAnimationCoroutine != null)
        {
            StopCoroutine(_currentAnimationCoroutine);
            _currentAnimationCoroutine = null;
        }

        // Flag'leri sıfırla
        _isAnimating = false;

        // Yeni animasyonu başlat
        _currentAnimationCoroutine = StartCoroutine(animationCoroutine);
    }

    #endregion
    #region Network Event Handlers

    private void HandleHasItemChanged(bool previousValue, bool newValue)
    {
        if (IsOwner) return; // Owner handles this locally

        if (newValue && !previousValue)
        {
            SafeStartAnimation(PickupAnimationCoroutine());
        }
        else if (!newValue && previousValue)
        {
            SafeStartAnimation(DropAnimationCoroutine());
        }

        // Ekstra güvenlik: hasItem false ise kesinlikle carrying kapalı olmalı
        if (!newValue)
        {
            _playerMovement?.SetCarrying(false);
            DestroyHeldItemVisual();
        }
    }

    private void HandleCurrentItemChanged(int previousValue, int newValue)
    {
        if (newValue != -1)
        {
            _currentItemData = GetItemDataFromID(newValue);

            if (_hasItem.Value && _heldItemVisual == null && _currentItemData != null)
            {
                SpawnHeldItemVisual();
            }
        }
        else
        {
            _currentItemData = null;
        }
    }

    #endregion
    #region Item Visual Management

    private void SpawnHeldItemVisual()
    {
        if (_currentItemData == null || holdPosition == null) return;

        DestroyHeldItemVisual();

        if (_currentItemData.visualPrefab == null) return;

        _heldItemVisual = Instantiate(_currentItemData.visualPrefab, holdPosition);
        _heldItemVisual.transform.SetParent(holdPosition, false);
        _heldItemVisual.transform.localPosition = Vector3.zero;
        _heldItemVisual.transform.localRotation = Quaternion.identity;

        PreserveBoxInfo();
        DisablePhysicsOnObject(_heldItemVisual);
        DisableCollidersOnObject(_heldItemVisual);
        SetLayerRecursively(_heldItemVisual, LayerMask.NameToLayer("Default"));
    }

    private void PreserveBoxInfo()
    {
        if (_heldItemVisual == null) return;

        var heldBoxInfo = _heldItemVisual.GetComponent<BoxInfo>();
        if (heldBoxInfo == null) return;

        // Check item name for full box indication
        var itemName = _currentItemData.itemName.ToLower();
        if (itemName.Contains("full") || itemName.Contains("dolu"))
        {
            heldBoxInfo.isFull = true;
            Debug.Log($"[PlayerInventory] Set box as full based on item name: {_currentItemData.itemName}");
            return;
        }

        // Copy from original prefab
        var originalBoxInfo = _currentItemData.visualPrefab.GetComponent<BoxInfo>();
        if (originalBoxInfo != null)
        {
            heldBoxInfo.isFull = originalBoxInfo.isFull;
            heldBoxInfo.boxType = originalBoxInfo.boxType;
            Debug.Log($"[PlayerInventory] Preserved box info from prefab: isFull={heldBoxInfo.isFull}");
        }
    }

    private void DestroyHeldItemVisual()
    {
        if (_heldItemVisual != null)
        {
            Destroy(_heldItemVisual);
            _heldItemVisual = null;
        }
    }

    private static void DisablePhysicsOnObject(GameObject obj)
    {
        // Disable 3D rigidbodies
        foreach (var rb in obj.GetComponentsInChildren<Rigidbody>())
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.detectCollisions = false;
        }

        // Disable 2D rigidbodies
        foreach (var rb2d in obj.GetComponentsInChildren<Rigidbody2D>())
        {
            rb2d.isKinematic = true;
            rb2d.gravityScale = 0;
        }

        // Remove joints
        foreach (var joint in obj.GetComponentsInChildren<Joint>())
        {
            DestroyImmediate(joint);
        }
    }

    private static void DisableCollidersOnObject(GameObject obj)
    {
        foreach (var col in obj.GetComponentsInChildren<Collider>())
        {
            col.enabled = false;
        }

        foreach (var col2d in obj.GetComponentsInChildren<Collider2D>())
        {
            col2d.enabled = false;
        }
    }

    private static void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    #endregion
    #region World Item Spawning

    private void SpawnWorldItemAtPosition(Vector3 position, Vector3 force)
    {
        if (_currentItemData == null) return;

        var worldItemPrefab = GetWorldItemPrefab(_currentItemData);
        if (worldItemPrefab == null) return;

        var spawnedItem = Instantiate(worldItemPrefab, position, Quaternion.identity);
        var networkObject = spawnedItem.GetComponent<NetworkObject>();

        if (networkObject == null) return;

        networkObject.Spawn();

        var worldItem = spawnedItem.GetComponent<NetworkWorldItem>();
        if (worldItem != null)
        {
            worldItem.SetItemData(_currentItemData);
            ApplyServerAuthoritativeBoxInfo(spawnedItem, worldItemPrefab);
            StartCoroutine(DelayedEnablePickup(worldItem));

            if (force != Vector3.zero)
            {
                worldItem.SetThrowForce(force);

                // ─── FIRLATMA İŞARETLEME ─────────────────────────────
                // Fırlatılan eşyalar çarpışma sonrası parçalanır.
                // Bırakılan eşyalar güvenle yere konulur.
                worldItem.MarkAsThrown();

                // Kutu ise BoxDestroyOnCollisionNetcode'u da işaretle
                var boxDestroy = spawnedItem.GetComponent<BoxDestroyOnCollisionNetcode>();
                if (boxDestroy != null)
                {
                    boxDestroy.MarkAsThrown();
                }
            }
        }
    }

    private IEnumerator DelayedEnablePickup(NetworkWorldItem worldItem)
    {
        yield return new WaitForSeconds(DELAYED_PICKUP_ENABLE_TIME);

        if (worldItem != null && worldItem.NetworkObject != null && worldItem.NetworkObject.IsSpawned)
        {
            worldItem.EnablePickup();
            Debug.Log($"[PlayerInventory] Pickup enabled for item: {worldItem.ItemData?.itemName}");
        }
    }

    /// <summary>
    /// Dünya kutusunun BoxInfo (isFull/boxType) değerini SERVER-OTORİTER kaynaktan set eder.
    /// Bu metot SpawnWorldItemAtPosition ServerRpc içinden çağrılır (RequestDropServerRpc /
    /// RequestThrowServerRpc), yani sunucu tarafında çalışır ve _heldItemVisual client-only bir
    /// görsel objedir (null olabilir, spawn zamanlamasına bağlıdır). Bu yüzden asıl kaynak olarak
    /// worldItemPrefab'ın (== _currentItemData.worldPrefab) kendi BoxInfo'su kullanılır — aynı
    /// desen PlayerInventory.Shelf.cs:676-682 ve PlayerInventory.Interaction.cs:342-348'de de
    /// kullanılıyor. _heldItemVisual sadece prefab'ta BoxInfo yoksa fallback olarak devreye girer.
    /// </summary>
    private void ApplyServerAuthoritativeBoxInfo(GameObject worldItem, GameObject worldItemPrefab)
    {
        var worldBoxInfo = worldItem.GetComponent<BoxInfo>();
        if (worldBoxInfo == null) return;

        var prefabBoxInfo = worldItemPrefab != null ? worldItemPrefab.GetComponent<BoxInfo>() : null;

        if (prefabBoxInfo != null)
        {
            worldBoxInfo.isFull = prefabBoxInfo.isFull;
            worldBoxInfo.boxType = prefabBoxInfo.boxType;
            Debug.Log($"[PlayerInventory] Server-authoritative BoxInfo applied from worldPrefab: isFull={worldBoxInfo.isFull}");
            return;
        }

        // Fallback: prefab'ta BoxInfo yoksa (beklenmedik durum), _heldItemVisual'dan dene.
        if (_heldItemVisual == null) return;

        var heldBoxInfo = _heldItemVisual.GetComponent<BoxInfo>();
        if (heldBoxInfo != null)
        {
            worldBoxInfo.isFull = heldBoxInfo.isFull;
            worldBoxInfo.boxType = heldBoxInfo.boxType;
            Debug.Log($"[PlayerInventory] Fallback: preserved box info from _heldItemVisual: isFull={worldBoxInfo.isFull}");
        }
    }

    private static GameObject GetWorldItemPrefab(ItemData itemData)
    {
        return itemData?.worldPrefab;
    }

    #endregion
    #region Item Data Management

    private static ItemData[] s_cachedItems;

    private static ItemData GetItemDataFromID(int itemID)
    {
        if (s_cachedItems == null)
        {
            s_cachedItems = Resources.LoadAll<ItemData>("Items");
        }

        foreach (var item in s_cachedItems)
        {
            if (item.itemID == itemID)
            {
                return item;
            }
        }

        return null;
    }

    private static int GetItemID(ItemData itemData)
    {
        return itemData?.itemID ?? -1;
    }

    #endregion
}