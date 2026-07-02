using NewCss;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;



public partial class PlayerInventory : NetworkBehaviour
{
    #region Detection System

    private IEnumerator RangeDetectionLoop()
    {
        var waitInterval = new WaitForSeconds(updateInterval);

        while (true)
        {
            UpdateItemsInRange();
            yield return waitInterval;
        }
    }

    private void UpdateItemsInRange()
    {
        // Store previous frame items for comparison
        _previousFrameItems.Clear();

        // ✅ DEĞİŞTİRİLDİ: Destroyed item'ları atla
        foreach (var item in _itemsInRange.ToArray())
        {
            if (IsValidWorldItem(item))
            {
                _previousFrameItems.Add(item);
            }
        }

        var detectionPos = GetDetectionCenterPosition();
        var currentFrameItems = new HashSet<NetworkWorldItem>();

        // Perform overlap sphere detection
        int hitCount = Physics.OverlapSphereNonAlloc(
            detectionPos,
            detectionRange,
            _colliderBuffer,
            itemLayerMask
        );

        // Process detected colliders
        for (int i = 0; i < hitCount; i++)
        {
            var collider = _colliderBuffer[i];
            if (collider == null) continue;

            var worldItem = collider.GetComponent<NetworkWorldItem>();
            if (IsValidPickupTarget(worldItem) && IsItemInCone(worldItem))
            {
                currentFrameItems.Add(worldItem);

                if (!_previousFrameItems.Contains(worldItem))
                {
                    OnItemEnterRange(worldItem);
                }
            }
        }

        // Handle items that left range
        foreach (var item in _previousFrameItems)
        {
            if (!IsValidWorldItem(item) || !currentFrameItems.Contains(item))
            {
                OnItemExitRange(item);
            }
        }

        // ✅ YENİ: _itemsInRange'den destroyed item'ları temizle
        _itemsInRange.RemoveAll(item => !IsValidWorldItem(item));
    }

    private bool IsValidPickupTarget(NetworkWorldItem item)
    {
        if (!IsValidWorldItem(item)) return false;

        try
        {
            return item.CanBePickedUp && item.ItemData != null;
        }
        catch
        {
            return false;
        }
    }

    private Vector3 GetDetectionCenterPosition()
    {
        if (useCustomDetectionCenter && detectionCenter != null)
        {
            return detectionCenter.position;
        }

        return transform.position + detectionOffset;
    }

    #endregion
    #region Cone Detection

    private bool IsItemInCone(NetworkWorldItem item)
    {
        return item != null && IsPositionInCone(item.transform.position);
    }

    private bool IsPositionInCone(Vector3 targetPosition)
    {
        if (!useConeDetection) return true;

        var detectionPos = GetDetectionCenterPosition();
        var toTarget = targetPosition - detectionPos;
        var forward = transform.forward;

        if (ignoreVerticalAngle)
        {
            toTarget.y = 0;
            forward.y = 0;
        }

        // Check for zero vectors to avoid NaN
        if (toTarget.sqrMagnitude < 0.001f || forward.sqrMagnitude < 0.001f)
        {
            return false;
        }

        toTarget.Normalize();
        forward.Normalize();

        float angle = Vector3.Angle(forward, toTarget);
        return angle <= coneAngle * 0.5f;
    }

    #endregion
    #region Item Priority System

    private float CalculateItemPriority(NetworkWorldItem item)
    {
        if (!useItemPriority || priorityLayers == null || priorityLayers.Length == 0)
        {
            return 1f;
        }

        string itemLayerName = LayerMask.LayerToName(item.gameObject.layer);

        for (int i = 0; i < priorityLayers.Length; i++)
        {
            if (priorityLayers[i] == itemLayerName)
            {
                // Higher priority for earlier layers (exponential decay)
                return 100f / Mathf.Pow(2, i);
            }
        }

        return 1f; // Default priority for unlisted layers
    }

    private float CalculateItemScore(NetworkWorldItem item, Vector3 detectionPos)
    {
        var itemPos = item.transform.position;
        var playerPos = detectionPos;

        if (ignoreVerticalAngle)
        {
            itemPos.y = playerPos.y;
        }

        float distance = Vector3.Distance(itemPos, playerPos);
        float priority = CalculateItemPriority(item);
        float distanceScore = 1f / (distance + 0.1f);

        return (priority * 100f) + distanceScore;
    }

    #endregion
    #region Item Range Events

    public void OnItemEnterRange(NetworkWorldItem item)
    {
        if (!IsOwner) return;

        if (IsValidPickupTarget(item) && !_itemsInRange.Contains(item))
        {
            Debug.Log($"[PlayerInventory] Item entered range: {item.ItemData.itemName}");
            _itemsInRange.Add(item);
            UpdateTargetedItem();
        }
    }

    public void OnItemExitRange(NetworkWorldItem item)
    {
        if (!IsOwner) return;

        if (_itemsInRange.Remove(item))
        {
            try
            {
                if (item != null && item.gameObject != null)
                {
                    RemoveOutlineFromItem(item);
                }
            }
            catch { /* Destroyed, ignore */ }

            UpdateTargetedItem();
        }
    }

    private void UpdateTargetedItem()
    {
        // Clean up invalid items
        _itemsInRange.RemoveAll(item => !IsValidPickupTarget(item));

        // Clear previous outline
        if (_previousTargetedItem != null)
        {
            RemoveOutlineFromItem(_previousTargetedItem);
        }

        _previousTargetedItem = _targetedItem;

        if (_itemsInRange.Count == 0)
        {
            _targetedItem = null;
            return;
        }

        // Find best target
        var detectionPos = GetDetectionCenterPosition();
        NetworkWorldItem bestItem = null;
        float bestScore = float.MinValue;

        foreach (var item in _itemsInRange)
        {
            if (!IsValidPickupTarget(item) || !IsItemInCone(item))
            {
                continue;
            }

            float score = CalculateItemScore(item, detectionPos);
            if (score > bestScore)
            {
                bestScore = score;
                bestItem = item;
            }
        }

        _targetedItem = bestItem;

        if (_targetedItem != null)
        {
            AddOutlineToItem(_targetedItem);
        }
    }

    #endregion
    #region Outline System

    private void AddOutlineToItem(NetworkWorldItem item)
    {
        if (item == null || item.NetworkObject == null || !item.NetworkObject.IsSpawned)
        {
            return;
        }

        var outline = item.GetComponent<Outline>();
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

        var outline = item.GetComponent<Outline>();
        if (outline != null)
        {
            outline.enabled = false;
        }
    }

    private void CleanupOutlines()
    {
        foreach (var item in _itemsInRange)
        {
            RemoveOutlineFromItem(item);
        }

        RemoveOutlineFromItem(_targetedItem);
        RemoveOutlineFromItem(_targetedShelfItem);

        foreach (var item in _availableShelfItems)
        {
            RemoveOutlineFromItem(item);
        }
    }

    #endregion
    #region Editor Gizmos

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        var detectionPos = GetDetectionCenterPosition();

        // Detection sphere
        Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
        Gizmos.DrawWireSphere(detectionPos, detectionRange);

        // Cone detection visualization
        if (useConeDetection)
        {
            DrawConeGizmo(detectionPos);
        }

        // Detection center indicator
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(detectionPos, Vector3.one * 0.2f);

        // Items in range
        DrawItemsInRangeGizmos(detectionPos);

        // Targeted item
        DrawTargetedItemGizmo(detectionPos);

        // Drop position
        if (dropPosition != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(dropPosition.position, 0.3f);
        }

        // Detection offset line
        if (!useCustomDetectionCenter)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, detectionPos);
        }
    }

    private void DrawConeGizmo(Vector3 detectionPos)
    {
        Gizmos.color = Color.cyan;

        var forward = transform.forward;
        if (ignoreVerticalAngle)
        {
            forward.y = 0;
            forward.Normalize();
        }

        float halfAngle = coneAngle * 0.5f;

        // Cone boundaries
        var leftBoundary = Quaternion.Euler(0, -halfAngle, 0) * forward * detectionRange;
        var rightBoundary = Quaternion.Euler(0, halfAngle, 0) * forward * detectionRange;

        Gizmos.DrawLine(detectionPos, detectionPos + leftBoundary);
        Gizmos.DrawLine(detectionPos, detectionPos + rightBoundary);

        // Forward direction
        Gizmos.color = Color.green;
        Gizmos.DrawLine(detectionPos, detectionPos + forward * detectionRange);

        // Arc
        Gizmos.color = Color.cyan;
        const int segments = 15;
        var previousPoint = detectionPos + leftBoundary;

        for (int i = 1; i <= segments; i++)
        {
            float angle = -halfAngle + (coneAngle * i / segments);
            var direction = Quaternion.Euler(0, angle, 0) * forward * detectionRange;
            var point = detectionPos + direction;
            Gizmos.DrawLine(previousPoint, point);
            previousPoint = point;
        }
    }

    private void DrawItemsInRangeGizmos(Vector3 detectionPos)
    {
        if (_itemsInRange == null || _itemsInRange.Count == 0) return;

        foreach (var item in _itemsInRange)
        {
            if (item == null || !IsItemInCone(item)) continue;

            float priority = CalculateItemPriority(item);

            // Color based on priority
            if (priority >= 100f)
                Gizmos.color = Color.green;
            else if (priority >= 50f)
                Gizmos.color = Color.yellow;
            else if (priority >= 25f)
                Gizmos.color = new Color(1f, 0.5f, 0f); // Orange
            else
                Gizmos.color = Color.red;

            Gizmos.DrawWireSphere(item.transform.position, 0.2f);
        }
    }

    private void DrawTargetedItemGizmo(Vector3 detectionPos)
    {
        if (_targetedItem == null) return;

        float priority = CalculateItemPriority(_targetedItem);
        Gizmos.color = priority >= 50f ? Color.green : Color.yellow;
        Gizmos.DrawLine(detectionPos, _targetedItem.transform.position);
        Gizmos.DrawWireSphere(_targetedItem.transform.position, 0.4f);
    }
#endif

    #endregion
}