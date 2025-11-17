using NewCss;
using System.Collections;
using System.Collections.Generic;
using Unity;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Audio;

public class PlayerInventory : NetworkBehaviour
{
    [Header("Range Detection Settings")]
    [SerializeField] private float detectionRange = 3f;
    [SerializeField] private float updateInterval = 0.02f;
    [SerializeField] private LayerMask itemLayerMask = -1;

    [Header("Detection Center Settings")]
    [SerializeField] private Transform detectionCenter;
    [SerializeField] private Vector3 detectionOffset = Vector3.up * 1f;
    [SerializeField] private bool useCustomDetectionCenter = false;

    [Header("Cone Detection Settings")]
    [SerializeField] private bool useConeDetection = true;
    [SerializeField] private float coneAngle = 45f;
    [SerializeField] private bool ignoreVerticalAngle = true;

    [Header("Item Priority Settings")]
    [SerializeField] private bool useItemPriority = true;
    [SerializeField]
    private string[] priorityLayers = new string[]
    {
        "GroundItem",
        "TableItem",
        "ShelfItem"
    };

    [Header("Outline Settings")]
    [SerializeField] private Color outlineColor = Color.yellow;
    [SerializeField] private float outlineWidth = 2f;
    [SerializeField] private Outline.Mode outlineMode = Outline.Mode.OutlineAll;

    [Header("Audio Settings")]
    [SerializeField] private AudioClip pickupSound;
    [SerializeField] private AudioClip dropSound;
    [SerializeField] private AudioClip placeOnTableSound;
    [SerializeField] private AudioClip placeOnShelfSound;
    [SerializeField] private AudioClip takeFromTableSound;
    [SerializeField] private AudioClip takeFromShelfSound;
    [Range(0f, 1f)]
    [SerializeField] private float inventorySoundVolume = 0.5f;

    private AudioSource audioSource;
    private UnifiedSettingsManager settingsManager;

    [Header("References")]
    [SerializeField] private Transform holdPosition;
    [SerializeField] private string holdPositionName = "HoldPosition";
    [SerializeField] private Animator playerAnimator;

    [Header("Drop Settings")]
    [SerializeField] private Transform dropPosition;
    [SerializeField] private string dropPositionName = "DropPosition";
    [SerializeField] private Vector3 defaultDropOffset = Vector3.forward * 1.5f;

    private static readonly Dictionary<ulong, float> itemPickupLocks = new Dictionary<ulong, float>();
    private static readonly object itemLock = new object();
    private static bool cleanupStarted = false;
    private const float PICKUP_LOCK_DURATION = 2f;

    private NetworkVariable<bool> hasItem = new NetworkVariable<bool>(false);
    private NetworkVariable<int> currentItemID = new NetworkVariable<int>(-1);

    private NetworkWorldItem targetedItem;
    private NetworkWorldItem previousTargetedItem;

    // ✨ YENİ: Shelf item sistemi
    private NetworkWorldItem targetedShelfItem;
    private NetworkWorldItem previousTargetedShelfItem;
    private List<NetworkWorldItem> availableShelfItems = new List<NetworkWorldItem>();
    private int currentShelfItemIndex = 0;

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

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.spatialBlend = 0.5f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.minDistance = 1f;
        audioSource.maxDistance = 15f;
        audioSource.playOnAwake = false;

        settingsManager = FindObjectOfType<UnifiedSettingsManager>();
        UpdateAudioVolume();
    }

    void Start()
    {
        ValidateHoldPosition();
        ValidateDropPosition();
        playerMovement = GetComponent<PlayerMovement>();

        if (IsOwner)
        {
            rangeUpdateCoroutine = StartCoroutine(UpdateRangeDetection());
        }

        if (IsServer && !cleanupStarted)
        {
            StartCoroutine(CleanupExpiredLocks());
            cleanupStarted = true;
        }
    }

    private IEnumerator CleanupExpiredLocks()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);

            List<ulong> expiredKeys = new List<ulong>();
            float currentTime = Time.time;

            lock (itemLock)
            {
                foreach (var kvp in itemPickupLocks)
                {
                    if (currentTime - kvp.Value > PICKUP_LOCK_DURATION)
                    {
                        expiredKeys.Add(kvp.Key);
                    }
                }

                foreach (ulong key in expiredKeys)
                {
                    itemPickupLocks.Remove(key);
                    Debug.Log($"Lock expired for item {key}");
                }
            }
        }
    }

    private bool IsItemLocked(ulong itemNetworkId)
    {
        lock (itemLock)
        {
            if (!itemPickupLocks.TryGetValue(itemNetworkId, out float lockTime))
                return false;

            bool isLocked = (Time.time - lockTime) < PICKUP_LOCK_DURATION;
            if (!isLocked)
            {
                itemPickupLocks.Remove(itemNetworkId);
            }
            return isLocked;
        }
    }

    private bool TryLockItem(ulong itemNetworkId)
    {
        lock (itemLock)
        {
            if (itemPickupLocks.TryGetValue(itemNetworkId, out float lockTime))
            {
                if ((Time.time - lockTime) < PICKUP_LOCK_DURATION)
                {
                    Debug.LogWarning($"Item {itemNetworkId} is already locked by another player!");
                    return false;
                }
                else
                {
                    itemPickupLocks[itemNetworkId] = Time.time;
                    Debug.Log($"Item {itemNetworkId} lock overwritten at {Time.time}");
                    return true;
                }
            }
            else
            {
                itemPickupLocks[itemNetworkId] = Time.time;
                Debug.Log($"Item {itemNetworkId} locked at {Time.time}");
                return true;
            }
        }
    }

    private void OnDestroy()
    {
        if (rangeUpdateCoroutine != null)
        {
            StopCoroutine(rangeUpdateCoroutine);
        }

        ClearAllOutlines();

        if (IsServer)
        {
            itemPickupLocks.Clear();
        }
    }

    private IEnumerator UpdateRangeDetection()
    {
        while (true)
        {
            UpdateItemsInRange();
            yield return new WaitForSeconds(updateInterval);
        }
    }

    private bool IsPositionInCone(Vector3 targetPosition)
    {
        if (!useConeDetection) return true;

        Vector3 detectionPos = GetDetectionCenterPosition();
        Vector3 toTarget = targetPosition - detectionPos;

        if (ignoreVerticalAngle)
        {
            toTarget.y = 0;
            Vector3 forward = transform.forward;
            forward.y = 0;

            if (toTarget.sqrMagnitude < 0.001f || forward.sqrMagnitude < 0.001f)
                return false;

            toTarget.Normalize();
            forward.Normalize();

            float angle = Vector3.Angle(forward, toTarget);
            return angle <= (coneAngle / 2f);
        }
        else
        {
            toTarget.Normalize();
            float angle = Vector3.Angle(transform.forward, toTarget);
            return angle <= (coneAngle / 2f);
        }
    }

    private bool IsItemInCone(NetworkWorldItem item)
    {
        if (item == null) return false;
        return IsPositionInCone(item.transform.position);
    }

    private float GetItemPriority(NetworkWorldItem item)
    {
        if (!useItemPriority || priorityLayers == null || priorityLayers.Length == 0)
            return 1f;

        string itemLayerName = LayerMask.LayerToName(item.gameObject.layer);

        for (int i = 0; i < priorityLayers.Length; i++)
        {
            if (priorityLayers[i] == itemLayerName)
            {
                float priority = 100f / Mathf.Pow(2, i);
                return priority;
            }
        }

        return 1f;
    }

    private void UpdateItemsInRange()
    {
        previousFrameItems.Clear();
        previousFrameItems.UnionWith(itemsInRange);

        Vector3 detectionPos = GetDetectionCenterPosition();

        int hitCount = Physics.OverlapSphereNonAlloc(
            detectionPos,
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
                worldItem.ItemData != null &&
                IsItemInCone(worldItem))
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

    void UpdateAudioVolume()
    {
        if (audioSource == null) return;

        float finalVolume = inventorySoundVolume;

        if (settingsManager != null)
        {
            finalVolume *= settingsManager.GetSFXVolume() * settingsManager.GetMasterVolume();
        }

        audioSource.volume = finalVolume;
    }

    private void PlayPickupSound()
    {
        PlayInventorySound(pickupSound);
    }

    private void PlayDropSound()
    {
        PlayInventorySound(dropSound);
    }

    private void PlayPlaceOnTableSound()
    {
        PlayInventorySound(placeOnTableSound != null ? placeOnTableSound : dropSound);
    }

    private void PlayPlaceOnShelfSound()
    {
        PlayInventorySound(placeOnShelfSound != null ? placeOnShelfSound : dropSound);
    }

    private void PlayTakeFromTableSound()
    {
        PlayInventorySound(takeFromTableSound != null ? takeFromTableSound : pickupSound);
    }

    private void PlayTakeFromShelfSound()
    {
        PlayInventorySound(takeFromShelfSound != null ? takeFromShelfSound : pickupSound);
    }

    private void PlayInventorySound(AudioClip clip)
    {
        if (audioSource == null || clip == null) return;

        UpdateAudioVolume();
        audioSource.PlayOneShot(clip);

        if (IsOwner)
        {
            PlayInventorySoundServerRpc(GetClipIndex(clip));
        }
    }

    private int GetClipIndex(AudioClip clip)
    {
        if (clip == pickupSound) return 0;
        if (clip == dropSound) return 1;
        if (clip == placeOnTableSound) return 2;
        if (clip == placeOnShelfSound) return 3;
        if (clip == takeFromTableSound) return 4;
        if (clip == takeFromShelfSound) return 5;
        return -1;
    }

    private AudioClip GetClipFromIndex(int index)
    {
        return index switch
        {
            0 => pickupSound,
            1 => dropSound,
            2 => placeOnTableSound,
            3 => placeOnShelfSound,
            4 => takeFromTableSound,
            5 => takeFromShelfSound,
            _ => null
        };
    }

    [ServerRpc]
    private void PlayInventorySoundServerRpc(int clipIndex)
    {
        PlayInventorySoundClientRpc(clipIndex);
    }

    [ClientRpc]
    private void PlayInventorySoundClientRpc(int clipIndex)
    {
        if (!IsOwner && audioSource != null)
        {
            AudioClip clip = GetClipFromIndex(clipIndex);
            if (clip != null)
            {
                UpdateAudioVolume();
                audioSource.PlayOneShot(clip);
            }
        }
    }

    private Vector3 GetDetectionCenterPosition()
    {
        if (useCustomDetectionCenter && detectionCenter != null)
        {
            return detectionCenter.position;
        }
        else
        {
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
            item.ItemData != null &&
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
            Vector3 detectionPos = GetDetectionCenterPosition();
            NetworkWorldItem bestItem = null;
            float bestScore = float.MinValue;

            foreach (NetworkWorldItem item in itemsInRange)
            {
                if (item == null || !item.CanBePickedUp || item.NetworkObject == null || !item.NetworkObject.IsSpawned)
                    continue;

                if (!IsItemInCone(item))
                    continue;

                Vector3 itemPos = item.transform.position;
                Vector3 playerPos = detectionPos;

                if (ignoreVerticalAngle)
                {
                    itemPos.y = playerPos.y;
                }

                float distance = Vector3.Distance(itemPos, playerPos);
                float priority = GetItemPriority(item);
                float distanceScore = 1f / (distance + 0.1f);
                float finalScore = (priority * 100f) + distanceScore;

                if (finalScore > bestScore)
                {
                    bestScore = finalScore;
                    bestItem = item;
                }
            }

            previousTargetedItem = targetedItem;
            targetedItem = bestItem;

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
    }

    // ✨ YENİ: Shelf itemları güncelle ve mouse tekerleği ile seç
    // ✨ YENİ: Shelf itemları güncelle ve mouse tekerleği ile seç
    // ✨ YENİ: Shelf itemları güncelle ve mouse tekerleği ile seç (KONİ KONTROLÜ KALDIRILDI)
    private void UpdateTargetedShelfItem()
    {
        // Önceki outline'ları temizle
        if (previousTargetedShelfItem != null)
        {
            RemoveOutlineFromItem(previousTargetedShelfItem);
        }

        // Shelf kontrolü
        ShelfState nearbyShelf = GetNearbyShelf();
        if (nearbyShelf == null || !nearbyShelf.HasItem() || hasItem.Value)
        {
            // Temizlik yap
            if (targetedShelfItem != null)
            {
                RemoveOutlineFromItem(targetedShelfItem);
            }

            targetedShelfItem = null;
            previousTargetedShelfItem = null;

            foreach (NetworkWorldItem item in availableShelfItems)
            {
                if (item != null)
                {
                    RemoveOutlineFromItem(item);
                }
            }
            availableShelfItems.Clear();
            currentShelfItemIndex = 0;
            return;
        }

        // Raftaki tüm itemları al (KONİ FİLTRESİ YOK!)
        NetworkWorldItem[] shelfItems = nearbyShelf.GetAllShelfItems();
        if (shelfItems == null || shelfItems.Length == 0)
        {
            // Temizlik yap
            if (targetedShelfItem != null)
            {
                RemoveOutlineFromItem(targetedShelfItem);
            }

            targetedShelfItem = null;
            previousTargetedShelfItem = null;
            availableShelfItems.Clear();
            currentShelfItemIndex = 0;
            return;
        }

        // Önceki listedeki itemların outline'larını temizle
        foreach (NetworkWorldItem item in availableShelfItems)
        {
            if (item != null && !System.Array.Exists(shelfItems, x => x == item))
            {
                RemoveOutlineFromItem(item);
            }
        }

        // Listeyi güncelle - ARTIK TÜM SHELF ITEMLARI (KONİ KONTROLÜ YOK)
        availableShelfItems.Clear();

        foreach (NetworkWorldItem item in shelfItems)
        {
            if (item == null || item.NetworkObject == null || !item.NetworkObject.IsSpawned)
                continue;

            // ✅ KONİ KONTROLÜ KALDIRILDI - Tüm itemlar ekleniyor
            availableShelfItems.Add(item);
        }

        // Hiç item yoksa çık
        if (availableShelfItems.Count == 0)
        {
            if (targetedShelfItem != null)
            {
                RemoveOutlineFromItem(targetedShelfItem);
            }

            targetedShelfItem = null;
            previousTargetedShelfItem = null;
            currentShelfItemIndex = 0;
            return;
        }

        // Index sınırı kontrolü
        if (currentShelfItemIndex >= availableShelfItems.Count)
        {
            currentShelfItemIndex = 0;
        }
        else if (currentShelfItemIndex < 0)
        {
            currentShelfItemIndex = availableShelfItems.Count - 1;
        }

        // Seçili item'ı ayarla
        previousTargetedShelfItem = targetedShelfItem;
        targetedShelfItem = availableShelfItems[currentShelfItemIndex];

        // Outline ekle
        if (targetedShelfItem != null)
        {
            AddOutlineToItem(targetedShelfItem);
            Debug.Log($"✅ Targeted shelf item [{currentShelfItemIndex + 1}/{availableShelfItems.Count}]: {targetedShelfItem.ItemData?.itemName}");
        }
    }

    // ✨ YENİ: Mouse tekerleği ile item seçimi
    // ✨ YENİ: Mouse tekerleği ile item seçimi
    // ✨ YENİ: Mouse tekerleği ile item seçimi
    private void HandleMouseWheel()
    {
        if (availableShelfItems.Count == 0) return; // Item yoksa çık

        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (Mathf.Abs(scroll) > 0.01f) // Minimum eşik değeri
        {
            // Önceki outline'ı temizle
            if (targetedShelfItem != null)
            {
                RemoveOutlineFromItem(targetedShelfItem);
            }

            if (scroll > 0f) // Yukarı kaydır
            {
                currentShelfItemIndex++;
                if (currentShelfItemIndex >= availableShelfItems.Count)
                {
                    currentShelfItemIndex = 0; // Başa dön
                }
                Debug.Log($"🔼 Scrolled UP - Index: {currentShelfItemIndex}/{availableShelfItems.Count}");
            }
            else if (scroll < 0f) // Aşağı kaydır
            {
                currentShelfItemIndex--;
                if (currentShelfItemIndex < 0)
                {
                    currentShelfItemIndex = availableShelfItems.Count - 1; // Sona dön
                }
                Debug.Log($"🔽 Scrolled DOWN - Index: {currentShelfItemIndex}/{availableShelfItems.Count}");
            }

            // Yeni item'a outline ekle
            if (currentShelfItemIndex >= 0 && currentShelfItemIndex < availableShelfItems.Count)
            {
                targetedShelfItem = availableShelfItems[currentShelfItemIndex];
                if (targetedShelfItem != null)
                {
                    AddOutlineToItem(targetedShelfItem);
                    Debug.Log($"✅ Selected item: {targetedShelfItem.ItemData?.itemName}");
                }
            }
        }
    }

    private void AddOutlineToItem(NetworkWorldItem item)
    {
        if (item == null || item.NetworkObject == null || !item.NetworkObject.IsSpawned) return;

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

        if (targetedShelfItem != null)
        {
            RemoveOutlineFromItem(targetedShelfItem);
        }

        foreach (NetworkWorldItem item in availableShelfItems)
        {
            if (item != null)
            {
                RemoveOutlineFromItem(item);
            }
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        // ✅ Shelf sistem kontrolü - sadece rafın range'indeyken çalışsın (KONİ YOK)
        ShelfState nearbyShelf = GetNearbyShelf();

        if (nearbyShelf != null && nearbyShelf.HasItem() && !hasItem.Value)
        {
            // Shelf itemları güncelle (İLK KEZ)
            if (availableShelfItems.Count == 0)
            {
                UpdateTargetedShelfItem();
            }

            // Mouse tekerleği kontrolü
            HandleMouseWheel();
        }
        else
        {
            // Raftan uzaklaşınca veya item aldığımızda outline'ları temizle
            if (targetedShelfItem != null)
            {
                RemoveOutlineFromItem(targetedShelfItem);
                targetedShelfItem = null;
            }

            if (previousTargetedShelfItem != null)
            {
                RemoveOutlineFromItem(previousTargetedShelfItem);
                previousTargetedShelfItem = null;
            }

            // Listeyi temizle
            foreach (NetworkWorldItem item in availableShelfItems)
            {
                if (item != null)
                {
                    RemoveOutlineFromItem(item);
                }
            }
            availableShelfItems.Clear();
            currentShelfItemIndex = 0;
        }

        HandleInput();
    }




    private Table GetNearbyTable()
    {
        Vector3 detectionPos = GetDetectionCenterPosition();
        Collider[] colliders = Physics.OverlapSphere(detectionPos, detectionRange);

        foreach (var collider in colliders)
        {
            Table table = collider.GetComponent<Table>();
            if (table != null && IsPositionInCone(table.transform.position))
            {
                return table;
            }
        }
        return null;
    }

    private ShelfState GetNearbyShelf()
    {
        Vector3 detectionPos = GetDetectionCenterPosition();
        Collider[] colliders = Physics.OverlapSphere(detectionPos, detectionRange);

        foreach (var collider in colliders)
        {
            ShelfState shelf = collider.GetComponent<ShelfState>();
            if (shelf != null && IsPositionInCone(shelf.transform.position))
            {
                return shelf;
            }
        }
        return null;
    }

    private NetworkedShelf GetNearbyNetworkedShelf()
    {
        Vector3 detectionPos = GetDetectionCenterPosition();
        Collider[] colliders = Physics.OverlapSphere(detectionPos, detectionRange);

        foreach (var collider in colliders)
        {
            NetworkedShelf networkedShelf = collider.GetComponent<NetworkedShelf>();
            if (networkedShelf != null && IsPositionInCone(networkedShelf.transform.position))
            {
                return networkedShelf;
            }
        }
        return null;
    }

    private bool CanPlaceBoxOnShelf(ShelfState shelf)
    {
        if (currentItemData == null) return false;

        if (heldItemVisual != null)
        {
            BoxInfo boxInfo = heldItemVisual.GetComponent<BoxInfo>();
            if (boxInfo != null)
            {
                Debug.Log($"Box found in held visual: {boxInfo.boxType}, isFull: {boxInfo.isFull}");
                return boxInfo.isFull;
            }
        }

        if (currentItemData.visualPrefab != null)
        {
            BoxInfo boxInfo = currentItemData.visualPrefab.GetComponent<BoxInfo>();
            if (boxInfo != null)
            {
                Debug.Log($"Box found in item data: {boxInfo.boxType}, isFull: {boxInfo.isFull}");
                return boxInfo.isFull;
            }
        }

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

        // ✅ Minigame aktifken E, F, Mouse0 engellenir
        if (IsMinigameActive())
        {
            Debug.Log("⚠️ Minigame active - E, F, Mouse blocked!");
            return;
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            Debug.Log($"E pressed. HasItem: {hasItem.Value}, TargetedItem: {(targetedItem != null ? targetedItem.name : "null")}, TargetedShelfItem: {(targetedShelfItem != null ? targetedShelfItem.name : "null")}");

            if (!hasItem.Value)
            {
                // ✨ ÖNCELİK 1: Mouse tekerleği ile seçilen shelf item
                if (targetedShelfItem != null && targetedShelfItem.NetworkObject != null)
                {
                    ShelfState nearbyShelf = GetNearbyShelf();
                    if (nearbyShelf != null)
                    {
                        isProcessingInteraction = true;
                        RequestTakeFromShelfServerRpc(targetedShelfItem.NetworkObject.NetworkObjectId);
                        PlayTakeFromShelfSound();
                        return;
                    }
                }

                // ÖNCELİK 2: Yerden item alma
                if (targetedItem != null)
                {
                    Debug.Log($"Attempting to pickup targeted item: {targetedItem.name}");
                    isProcessingInteraction = true;
                    RequestPickupServerRpc(targetedItem.NetworkObjectId);
                    PlayPickupSound();
                    return;
                }
            }

            // Masa etkileşimi
            Table nearbyTable = GetNearbyTable();
            if (nearbyTable != null)
            {
                isProcessingInteraction = true;

                if (hasItem.Value)
                {
                    PlayPlaceOnTableSound();
                }
                else
                {
                    PlayTakeFromTableSound();
                }

                nearbyTable.InteractWithTable(this);
                StartCoroutine(ResetInteractionFlag());
                return;
            }

            Debug.Log("No valid interaction target found");
        }

        if (Input.GetKeyDown(KeyCode.F))
        {
            if (hasItem.Value && !isAnimating)
            {
                ShelfState nearbyShelf = GetNearbyShelf();
                NetworkedShelf networkedShelf = GetNearbyNetworkedShelf();

                Debug.Log($"F pressed - Nearby shelf: {(nearbyShelf != null ? "Found" : "Not found")}");
                Debug.Log($"F pressed - Nearby networked shelf: {(networkedShelf != null ? "Found" : "Not found")}");

                if (networkedShelf != null && !networkedShelf.CanPlaceItems())
                {
                    Debug.Log("Cannot place items on NetworkedShelf - doing normal drop");
                    isProcessingInteraction = true;
                    RequestDropServerRpc();
                    PlayDropSound();
                    return;
                }

                if (nearbyShelf != null)
                {
                    bool canPlace = CanPlaceBoxOnShelf(nearbyShelf);
                    Debug.Log($"Can place box on shelf: {canPlace}");

                    if (canPlace)
                    {
                        Debug.Log("Attempting to place on shelf...");
                        isProcessingInteraction = true;
                        RequestPlaceOnShelfServerRpc();
                        PlayPlaceOnShelfSound();
                    }
                    else
                    {
                        Debug.Log("Cannot place box - doing normal drop");
                        isProcessingInteraction = true;
                        RequestDropServerRpc();
                        PlayDropSound();
                    }
                }
                else
                {
                    Debug.Log("No shelf nearby - doing normal drop");
                    isProcessingInteraction = true;
                    RequestDropServerRpc();
                    PlayDropSound();
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
                PlayDropSound();
            }
        }
    }
    /// <summary>
    /// Yakındaki masada minigame aktif mi kontrol eder
    /// </summary>
    private bool IsMinigameActive()
    {
        Table nearbyTable = GetNearbyTable();
        if (nearbyTable != null)
        {
            BoxingMinigameManager minigame = nearbyTable.GetComponentInChildren<BoxingMinigameManager>();
            if (minigame != null && minigame.IsMinigameActive)
            {
                return true;
            }
        }
        return false;
    }

    // ✨ YENİ: NetworkObjectId ile shelf'ten al
    // ✨ YENİ: NetworkObjectId ile shelf'ten al
    [ServerRpc]
    private void RequestTakeFromShelfServerRpc(ulong itemNetworkId, ServerRpcParams rpcParams = default)
    {
        ulong requesterClientId = rpcParams.Receive.SenderClientId;

        Debug.Log($"RequestTakeFromShelfServerRpc called by client {requesterClientId} for item {itemNetworkId}");

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

        try
        {
            Debug.Log($"Requesting item {itemNetworkId} from shelf for client {requesterClientId}");
            nearbyShelf.TakeItemFromShelfServerRpc(requesterClientId, itemNetworkId, rpcParams);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error in RequestTakeFromShelfServerRpc: {e.Message}");
        }
        finally
        {
            ResetProcessingInteractionClientRpc();
        }
    }

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

        GameObject worldItemPrefab = GetWorldItemPrefab(currentItemData);
        if (worldItemPrefab != null)
        {
            Vector3 spawnPos = transform.position + Vector3.up * 0.5f;
            GameObject spawnedItem = Instantiate(worldItemPrefab, spawnPos, Quaternion.identity);
            NetworkObject networkObject = spawnedItem.GetComponent<NetworkObject>();

            if (networkObject != null)
            {
                networkObject.Spawn();

                NetworkWorldItem worldItem = spawnedItem.GetComponent<NetworkWorldItem>();
                if (worldItem != null)
                {
                    worldItem.SetItemData(currentItemData);

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

                    worldItem.DisablePickup();
                }

                Debug.Log("Calling PlaceItemOnShelfServerRpc...");
                nearbyShelf.PlaceItemOnShelfServerRpc(new NetworkObjectReference(networkObject));

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
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.IncrementQuestProgress(QuestType.PlaceOnShelf);
        }

        ResetProcessingInteractionClientRpc();
    }

    [ServerRpc]
    private void RequestPickupServerRpc(ulong itemNetworkId)
    {
        Debug.Log($"RequestPickupServerRpc called for item: {itemNetworkId}");

        if (IsItemLocked(itemNetworkId))
        {
            Debug.LogWarning($"Item {itemNetworkId} is locked! Cannot pickup.");
            ResetProcessingInteractionClientRpc();
            return;
        }

        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(itemNetworkId, out NetworkObject networkObject))
        {
            Debug.LogError($"NetworkObject not found for ID: {itemNetworkId}");
            ResetProcessingInteractionClientRpc();
            return;
        }

        if (networkObject == null || !networkObject.IsSpawned)
        {
            Debug.LogWarning($"Item {itemNetworkId} is already despawned!");
            ResetProcessingInteractionClientRpc();
            return;
        }

        NetworkWorldItem worldItem = networkObject.GetComponent<NetworkWorldItem>();

        if (worldItem == null || !worldItem.CanBePickedUp || hasItem.Value)
        {
            Debug.LogWarning($"Cannot pickup: WorldItem={worldItem != null}, CanPickup={worldItem?.CanBePickedUp}, HasItem={hasItem.Value}");
            ResetProcessingInteractionClientRpc();
            return;
        }

        if (!TryLockItem(itemNetworkId))
        {
            Debug.LogWarning($"Failed to lock item {itemNetworkId}");
            ResetProcessingInteractionClientRpc();
            return;
        }

        Debug.Log($"✅ Item {itemNetworkId} locked for client {OwnerClientId}");

        ItemData itemData = worldItem.ItemData;
        if (itemData == null)
        {
            Debug.LogError("ItemData is null!");
            itemPickupLocks.Remove(itemNetworkId);
            worldItem.EnablePickup();
            ResetProcessingInteractionClientRpc();
            return;
        }

        hasItem.Value = true;
        currentItemID.Value = GetItemID(itemData);

        OnItemPickedUpClientRpc(itemNetworkId);

        StartCoroutine(DelayedDespawnWithUnlock(worldItem, itemData, itemNetworkId));

        Debug.Log($"✅ Item picked up successfully: {itemData.itemName}");

        ResetProcessingInteractionClientRpc();
    }

    private IEnumerator DelayedDespawnWithUnlock(NetworkWorldItem worldItem, ItemData itemData, ulong itemNetworkId)
    {
        yield return new WaitForSeconds(0.1f);

        if (worldItem != null && worldItem.NetworkObject != null && worldItem.NetworkObject.IsSpawned)
        {
            worldItem.NetworkObject.Despawn();
            Debug.Log($"Item {itemNetworkId} despawned");
        }

        if (itemPickupLocks.ContainsKey(itemNetworkId))
        {
            itemPickupLocks.Remove(itemNetworkId);
            Debug.Log($"Lock removed for item {itemNetworkId} after despawn");
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

        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(itemNetworkId, out NetworkObject networkObject))
        {
            var worldItem = networkObject.GetComponent<NetworkWorldItem>();
            if (worldItem != null)
            {
                worldItem.DisablePickup();
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

        // ✨ Shelf item outline temizleme
        if (targetedShelfItem != null)
        {
            RemoveOutlineFromItem(targetedShelfItem);
            targetedShelfItem = null;
        }

        if (previousTargetedShelfItem != null)
        {
            RemoveOutlineFromItem(previousTargetedShelfItem);
            previousTargetedShelfItem = null;
        }
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

                PreserveBoxInfo();

                DisablePhysicsComponents(heldItemVisual);
                DisableColliders(heldItemVisual);
                SetLayerRecursively(heldItemVisual, LayerMask.NameToLayer("Default"));
            }
        }
    }

    private void PreserveBoxInfo()
    {
        if (heldItemVisual == null) return;

        BoxInfo heldBoxInfo = heldItemVisual.GetComponent<BoxInfo>();
        if (heldBoxInfo != null)
        {
            if (currentItemData.itemName.ToLower().Contains("full") ||
                currentItemData.itemName.ToLower().Contains("dolu"))
            {
                heldBoxInfo.isFull = true;
                Debug.Log($"Set box as full based on item name: {currentItemData.itemName}");
            }
            else
            {
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

                        PreserveBoxInfoOnWorldItem(worldItem.gameObject);

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

    private IEnumerator DelayedEnablePickup(NetworkWorldItem worldItem)
    {
        yield return new WaitForSeconds(0.2f);

        if (worldItem != null && worldItem.NetworkObject != null && worldItem.NetworkObject.IsSpawned)
        {
            worldItem.EnablePickup();
            Debug.Log($"Pickup enabled for item: {worldItem.ItemData?.itemName}");
        }
    }

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

        if (hasItemValue)
        {
            StartPickupAnimationClientRpc();
        }
        else
        {
            ClearHeldItemVisualClientRpc();
            StartDropAnimationClientRpc();
        }

        Debug.Log($"Inventory state set: hasItem={hasItemValue}, itemID={itemID}, starting animation: {(hasItemValue ? "pickup" : "drop")}");
    }

    [ServerRpc(RequireOwnership = false)]
    public void TriggerDropAnimationServerRpc()
    {
        StartDropAnimationClientRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    public void GiveItemDirectlyServerRpc(int itemID)
    {
        if (hasItem.Value) return;

        ItemData itemData = GetItemDataFromID(itemID);
        if (itemData == null) return;

        hasItem.Value = true;
        currentItemID.Value = itemID;

        if (playerMovement != null)
        {
            playerMovement.SetCarrying(true);
        }

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
        Vector3 detectionPos = GetDetectionCenterPosition();

        Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
        Gizmos.DrawWireSphere(detectionPos, detectionRange);

        if (useConeDetection)
        {
            Gizmos.color = Color.cyan;

            Vector3 forward = transform.forward;
            if (ignoreVerticalAngle)
            {
                forward.y = 0;
                forward.Normalize();
            }

            float halfAngle = coneAngle / 2f;

            Vector3 leftBoundary = Quaternion.Euler(0, -halfAngle, 0) * forward * detectionRange;
            Gizmos.DrawLine(detectionPos, detectionPos + leftBoundary);

            Vector3 rightBoundary = Quaternion.Euler(0, halfAngle, 0) * forward * detectionRange;
            Gizmos.DrawLine(detectionPos, detectionPos + rightBoundary);

            Gizmos.color = Color.green;
            Gizmos.DrawLine(detectionPos, detectionPos + forward * detectionRange);

            Gizmos.color = Color.cyan;
            Vector3 previousPoint = detectionPos + leftBoundary;
            int segments = 15;
            for (int i = 1; i <= segments; i++)
            {
                float angle = -halfAngle + (coneAngle * i / segments);
                Vector3 direction = Quaternion.Euler(0, angle, 0) * forward * detectionRange;
                Vector3 point = detectionPos + direction;
                Gizmos.DrawLine(previousPoint, point);
                previousPoint = point;
            }
        }

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(detectionPos, Vector3.one * 0.2f);

        if (itemsInRange != null && itemsInRange.Count > 0)
        {
            foreach (NetworkWorldItem item in itemsInRange)
            {
                if (item == null || !IsItemInCone(item)) continue;

                float priority = GetItemPriority(item);

                if (priority >= 100f)
                    Gizmos.color = Color.green;
                else if (priority >= 50f)
                    Gizmos.color = Color.yellow;
                else if (priority >= 25f)
                    Gizmos.color = new Color(1f, 0.5f, 0f);
                else
                    Gizmos.color = Color.red;

                Gizmos.DrawWireSphere(item.transform.position, 0.2f);
            }
        }

        if (targetedItem != null)
        {
            float priority = GetItemPriority(targetedItem);
            Gizmos.color = priority >= 50f ? Color.green : Color.yellow;
            Gizmos.DrawLine(detectionPos, targetedItem.transform.position);
            Gizmos.DrawWireSphere(targetedItem.transform.position, 0.4f);
        }

        if (dropPosition != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(dropPosition.position, 0.3f);
        }

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