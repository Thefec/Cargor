using NewCss;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Oyuncu envanter sistemi - Item alma, bırakma, fırlatma ve raf/masa etkileşimlerini yönetir. 
/// Thread-safe item kilitleme, network senkronizasyonu ve görsel geri bildirim sistemleri içerir.
/// </summary>
public class PlayerInventory : NetworkBehaviour
{
    #region Serialized Fields

    [Header("=== DETECTION SETTINGS ===")]
    [SerializeField, Range(1f, 10f), Tooltip("Item algılama menzili")]
    private float detectionRange = 3f;

    [SerializeField, Range(0.01f, 0.1f), Tooltip("Algılama güncelleme aralığı (saniye)")]
    private float updateInterval = 0.02f;

    [SerializeField, Tooltip("Algılanacak layer'lar")]
    private LayerMask itemLayerMask = -1;

    [Header("=== DETECTION CENTER ===")]
    [SerializeField, Tooltip("Özel algılama merkezi transform'u")]
    private Transform detectionCenter;

    [SerializeField, Tooltip("Algılama offset'i (detectionCenter null ise kullanılır)")]
    private Vector3 detectionOffset = Vector3.up;

    [SerializeField, Tooltip("Özel algılama merkezi kullan")]
    private bool useCustomDetectionCenter;

    [Header("=== CONE DETECTION ===")]
    [SerializeField, Tooltip("Koni tabanlı algılama kullan")]
    private bool useConeDetection = true;

    [SerializeField, Range(15f, 180f), Tooltip("Algılama koni açısı")]
    private float coneAngle = 45f;

    [SerializeField, Tooltip("Dikey açıyı yoksay (2. 5D oyunlar için)")]
    private bool ignoreVerticalAngle = true;

    [Header("=== ITEM PRIORITY ===")]
    [SerializeField, Tooltip("Layer tabanlı öncelik sistemi kullan")]
    private bool useItemPriority = true;

    [SerializeField, Tooltip("Öncelik sıralaması (ilk = en yüksek öncelik)")]
    private string[] priorityLayers = { "GroundItem", "TableItem", "ShelfItem" };

    [Header("=== OUTLINE SETTINGS ===")]
    [SerializeField, Tooltip("Hedeflenen item'ın outline rengi")]
    private Color outlineColor = Color.yellow;

    [SerializeField, Range(0.5f, 10f), Tooltip("Outline kalınlığı")]
    private float outlineWidth = 2f;

    [SerializeField, Tooltip("Outline modu")]
    private Outline.Mode outlineMode = Outline.Mode.OutlineAll;

    [Header("=== AUDIO SETTINGS ===")]
    [SerializeField] private AudioClip pickupSound;
    [SerializeField] private AudioClip dropSound;
    [SerializeField] private AudioClip placeOnTableSound;
    [SerializeField] private AudioClip placeOnShelfSound;
    [SerializeField] private AudioClip takeFromTableSound;
    [SerializeField] private AudioClip takeFromShelfSound;

    [SerializeField, Range(0f, 1f), Tooltip("Envanter ses seviyesi")]
    private float inventorySoundVolume = 0.5f;

    [Header("=== HOLD POSITION ===")]
    [SerializeField, Tooltip("Item tutma pozisyonu")]
    private Transform holdPosition;

    [SerializeField, Tooltip("Hold position arama ismi")]
    private string holdPositionName = "HoldPosition";

    [SerializeField, Tooltip("Player animator referansı")]
    private Animator playerAnimator;

    [Header("=== DROP POSITION ===")]
    [SerializeField, Tooltip("Item bırakma pozisyonu")]
    private Transform dropPosition;

    [SerializeField, Tooltip("Drop position arama ismi")]
    private string dropPositionName = "DropPosition";

    [SerializeField, Tooltip("Varsayılan bırakma offset'i")]
    private Vector3 defaultDropOffset = Vector3.forward * 1.5f;

    #endregion

    #region Constants

    private const float PICKUP_LOCK_DURATION = 2f;
    private const float INTERACTION_COOLDOWN = 0.1f;
    private const float ANIMATION_TIMEOUT = 2f;
    private const float DELAYED_DESPAWN_TIME = 0.1f;
    private const float DELAYED_PICKUP_ENABLE_TIME = 0.2f;
    private const int COLLIDER_BUFFER_SIZE = 30;
    private const float MIN_SCROLL_THRESHOLD = 0.01f;

    #endregion

    #region Static Fields (Thread-Safe Item Locking)

    private static readonly Dictionary<ulong, float> s_itemPickupLocks = new();
    private static readonly object s_lockObject = new();
    private static bool s_cleanupStarted;

    #endregion

    #region Network Variables

    private readonly NetworkVariable<bool> _hasItem = new(false);
    private readonly NetworkVariable<int> _currentItemID = new(-1);

    #endregion

    #region Private Fields

    // Components
    private AudioSource _audioSource;
    private UnifiedSettingsManager _settingsManager;
    private PlayerMovement _playerMovement;

    // Item Targeting
    private NetworkWorldItem _targetedItem;
    private NetworkWorldItem _previousTargetedItem;
    private readonly List<NetworkWorldItem> _itemsInRange = new();
    private readonly HashSet<NetworkWorldItem> _previousFrameItems = new();

    // Shelf Item System
    private NetworkWorldItem _targetedShelfItem;
    private NetworkWorldItem _previousTargetedShelfItem;
    private readonly List<NetworkWorldItem> _availableShelfItems = new();
    private int _currentShelfItemIndex;

    // Held Item
    private GameObject _heldItemVisual;
    private ItemData _currentItemData;

    // State Flags
    private bool _isAnimating;
    private bool _isProcessingInteraction;

    // Detection System
    private Collider[] _colliderBuffer;
    private Coroutine _rangeUpdateCoroutine;

    #endregion

    #region Public Properties

    public bool HasItem => _hasItem.Value;
    public ItemData CurrentItemData => _currentItemData;
    public Transform HoldPosition => holdPosition;
    public Transform DropPosition => dropPosition;
    public NetworkWorldItem TargetedItem => _targetedItem;
    public bool IsProcessingInteraction => _isProcessingInteraction;
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

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        InitializeAudioSource();
        _colliderBuffer = new Collider[COLLIDER_BUFFER_SIZE];
    }

    private void Start()
    {
        CacheComponents();
        ValidatePositions();
        StartDetectionSystem();
        StartLockCleanupSystem();
    }

    private void OnEnable()
    {
        SubscribeToNetworkEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromNetworkEvents();
        CleanupOutlines();
    }

    private void OnDestroy()
    {
        StopDetectionSystem();
        CleanupOutlines();
        CleanupServerLocks();
    }

    private void Update()
    {
        if (!IsOwner) return;

        UpdateShelfItemSystem();
        HandleInput();
    }

    #endregion

    #region Initialization

    private void InitializeAudioSource()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }

        ConfigureAudioSource(_audioSource);
    }

    private static void ConfigureAudioSource(AudioSource source)
    {
        source.spatialBlend = 0.5f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 1f;
        source.maxDistance = 15f;
        source.playOnAwake = false;
    }

    private void CacheComponents()
    {
        _settingsManager = FindObjectOfType<UnifiedSettingsManager>();
        _playerMovement = GetComponent<PlayerMovement>();
    }

    private void ValidatePositions()
    {
        if (holdPosition == null)
        {
            holdPosition = FindTransformByName(holdPositionName, "Hand");
            if (holdPosition == null)
            {
                Debug.LogWarning($"[PlayerInventory] Hold position '{holdPositionName}' not found!");
            }
        }

        if (dropPosition == null)
        {
            dropPosition = FindTransformByName(dropPositionName, null);
            if (dropPosition == null)
            {
                Debug.LogWarning($"[PlayerInventory] Drop position '{dropPositionName}' not found!  Using default offset.");
            }
        }
    }

    private Transform FindTransformByName(string primaryName, string fallbackName)
    {
        // 1. Direct child search
        var result = transform.Find(primaryName);
        if (result != null) return result;

        // 2. Recursive search in children
        foreach (Transform child in GetComponentsInChildren<Transform>())
        {
            if (child.name == primaryName) return child;
        }

        // 3. Global search
        var globalObject = GameObject.Find(primaryName);
        if (globalObject != null) return globalObject.transform;

        // 4. Fallback search
        if (!string.IsNullOrEmpty(fallbackName))
        {
            return transform.Find(fallbackName);
        }

        return null;
    }

    private void StartDetectionSystem()
    {
        if (IsOwner)
        {
            _rangeUpdateCoroutine = StartCoroutine(RangeDetectionLoop());
        }
    }

    private void StopDetectionSystem()
    {
        if (_rangeUpdateCoroutine != null)
        {
            StopCoroutine(_rangeUpdateCoroutine);
            _rangeUpdateCoroutine = null;
        }
    }

    private void StartLockCleanupSystem()
    {
        if (IsServer && !s_cleanupStarted)
        {
            StartCoroutine(LockCleanupLoop());
            s_cleanupStarted = true;
        }
    }

    private void SubscribeToNetworkEvents()
    {
        _hasItem.OnValueChanged += HandleHasItemChanged;
        _currentItemID.OnValueChanged += HandleCurrentItemChanged;
    }

    private void UnsubscribeFromNetworkEvents()
    {
        _hasItem.OnValueChanged -= HandleHasItemChanged;
        _currentItemID.OnValueChanged -= HandleCurrentItemChanged;
    }

    #endregion

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

    #region Shelf Item System

    private void UpdateShelfItemSystem()
    {
        var nearbyShelf = GetNearbyShelf();
        bool shouldShowShelfItems = nearbyShelf != null && nearbyShelf.HasItem() && !_hasItem.Value;

        if (shouldShowShelfItems)
        {
            // Initialize shelf items if not already done
            if (_availableShelfItems.Count == 0)
            {
                UpdateTargetedShelfItem();
            }

            HandleMouseWheelInput();
        }
        else
        {
            ClearShelfItemTargeting();
        }
    }

    private void UpdateTargetedShelfItem()
    {
        // Clear previous outline - ✅ Güvenli check
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

        var nearbyShelf = GetNearbyShelf();
        if (nearbyShelf == null || !nearbyShelf.HasItem() || _hasItem.Value)
        {
            ClearShelfItemTargeting();
            return;
        }

        var shelfItems = nearbyShelf.GetAllShelfItems();
        if (shelfItems == null || shelfItems.Length == 0)
        {
            ClearShelfItemTargeting();
            return;
        }

        // ✅ DEĞİŞTİRİLDİ: Destroyed item'ları temizle
        foreach (var item in _availableShelfItems.ToArray()) // ToArray() ile kopya oluştur
        {
            if (!IsValidWorldItem(item))
            {
                _availableShelfItems.Remove(item);
            }
            else if (!System.Array.Exists(shelfItems, x => x == item))
            {
                try
                {
                    RemoveOutlineFromItem(item);
                }
                catch { /* Ignore */ }
                _availableShelfItems.Remove(item);
            }
        }

        // Rebuild available items list
        _availableShelfItems.Clear();

        foreach (var item in shelfItems)
        {
            if (IsValidWorldItem(item))
            {
                _availableShelfItems.Add(item);
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

    #region Input Handling

    private void HandleInput()
    {
        if (_isProcessingInteraction) return;
        if (IsMinigameActive())
        {
            Debug.Log("[PlayerInventory] Minigame active - inputs blocked!");
            return;
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            HandlePickupInteraction();
        }
        else if (Input.GetKeyDown(KeyCode.F))
        {
            HandleDropInteraction();
        }
        else if (Input.GetMouseButtonDown(0))
        {
            HandleThrowInteraction();
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
            // Priority 1: Shelf item (selected via mouse wheel)
            // ✅ DEĞİŞTİRİLDİ: Daha güvenli null check
            if (IsValidShelfItem(_targetedShelfItem))
            {
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
            // ✅ DEĞİŞTİRİLDİ: Daha güvenli null check
            if (IsValidWorldItem(_targetedItem))
            {
                Debug.Log($"[PlayerInventory] Attempting to pickup targeted item: {_targetedItem.name}");
                _isProcessingInteraction = true;
                RequestPickupServerRpc(_targetedItem.NetworkObjectId);
                PlaySound(SoundType.Pickup);
                return;
            }
        }

        // Priority 3: Table interaction
        var nearbyTable = GetNearbyTable();
        if (nearbyTable != null)
        {
            _isProcessingInteraction = true;
            PlaySound(_hasItem.Value ? SoundType.PlaceOnTable : SoundType.TakeFromTable);
            nearbyTable.InteractWithTable(this);
            StartCoroutine(ResetInteractionFlagAfterDelay());
            return;
        }

        Debug.Log("[PlayerInventory] No valid interaction target found");
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
        var networkedShelf = GetNearbyNetworkedShelf();

        Debug.Log($"[PlayerInventory] F pressed - HasItem: {_hasItem.Value}, ItemData: {_currentItemData?.itemName ?? "null"}");
        Debug.Log($"[PlayerInventory] Shelf: {(nearbyShelf != null ? nearbyShelf.name : "None")}, NetworkedShelf: {(networkedShelf != null ? "Found" : "None")}");

        // NetworkedShelf kısıtlaması kontrolü
        if (networkedShelf != null && !networkedShelf.CanPlaceItems())
        {
            Debug.Log("[PlayerInventory] Cannot place items on NetworkedShelf - doing normal drop");
            PerformNormalDrop();
            return;
        }

        // ShelfState yerleştirme kontrolü
        if (nearbyShelf != null)
        {
            // ✅ DEĞİŞTİRİLDİ: Client-side kontrol sadece bilgilendirme amaçlı
            // Asıl kontrol server'da yapılacak
            bool canPlace = CanPlaceBoxOnShelf(nearbyShelf);
            Debug.Log($"[PlayerInventory] Client-side canPlace check: {canPlace}");

            if (canPlace)
            {
                Debug.Log($"[PlayerInventory] Calling RequestPlaceOnShelfServerRpc");
                _isProcessingInteraction = true;
                RequestPlaceOnShelfServerRpc();
                PlaySound(SoundType.PlaceOnShelf);
                StartCoroutine(ResetInteractionFlagAfterDelay());
                return;
            }
            else
            {
                Debug.Log("[PlayerInventory] Cannot place on shelf (not a full box) - doing normal drop");
            }
        }

        // Default: Normal drop
        PerformNormalDrop();
    }

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

    private bool IsMinigameActive()
    {
        var nearbyTable = GetNearbyTable();
        if (nearbyTable != null)
        {
            var minigame = nearbyTable.GetComponentInChildren<BoxingMinigameManager>();
            if (minigame != null && minigame.IsMinigameActive)
            {
                return true;
            }
        }

        return false;
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

    #region Server RPCs - Shelf Interaction
    [ServerRpc(RequireOwnership = false)]
    private void RequestTakeFromShelfServerRpc(ulong itemNetworkId, ServerRpcParams rpcParams = default)
    {
        var requesterClientId = rpcParams.Receive.SenderClientId;
        Debug.Log($"[PlayerInventory] Server: Client {requesterClientId} wants to take item {itemNetworkId} from shelf");

        if (_hasItem.Value)
        {
            Debug.LogWarning($"[PlayerInventory] Client {requesterClientId} already has an item!");
            ResetProcessingInteractionClientRpc();
            return;
        }

        // Get player transform
        if (!TryGetPlayerTransform(requesterClientId, out var playerTransform))
        {
            ResetProcessingInteractionClientRpc();
            return;
        }

        // Find shelf
        var nearbyShelf = FindNearestShelfForTransform(playerTransform);
        if (nearbyShelf == null)
        {
            Debug.LogError($"[PlayerInventory] No shelf found near client {requesterClientId}!");
            ResetProcessingInteractionClientRpc();
            return;
        }

        if (!nearbyShelf.HasItem())
        {
            Debug.LogWarning("[PlayerInventory] Shelf is empty!");
            ResetProcessingInteractionClientRpc();
            return;
        }

        Debug.Log("[PlayerInventory] Calling ShelfState.TakeItemFromShelfServerRpc");

        try
        {
            nearbyShelf.TakeItemFromShelfServerRpc(requesterClientId, itemNetworkId, rpcParams);

            // ✅ YENİ: Client'a shelf item targeting'i temizlemesini söyle
            ClearShelfTargetingClientRpc(requesterClientId);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PlayerInventory] Error in TakeItemFromShelfServerRpc: {e.Message}\n{e.StackTrace}");
        }
        finally
        {
            ResetProcessingInteractionClientRpc();
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
    private void PerformNormalDropServer(Transform playerTransform)
    {
        if (_currentItemData == null) return;

        var worldItemPrefab = GetWorldItemPrefab(_currentItemData);
        if (worldItemPrefab == null) return;

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
        }

        ClearInventoryState();

        if (_playerMovement != null)
        {
            _playerMovement.SetCarrying(false);
        }

        StartDropAnimationClientRpc();
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
            ResetProcessingInteractionClientRpc();
            return;
        }

        // Player transform bul
        if (!TryGetPlayerTransform(requesterClientId, out var playerTransform))
        {
            Debug.LogError($"[PlayerInventory] ❌ Player transform not found for client {requesterClientId}");
            ResetProcessingInteractionClientRpc();
            return;
        }

        Debug.Log($"[PlayerInventory] Found player at position {playerTransform.position}");

        // ✅ DEĞİŞTİRİLDİ: Shelf bul - daha geniş arama
        var nearbyShelf = FindNearbyShelfWithRangeCheck(playerTransform);
        if (nearbyShelf == null)
        {
            Debug.LogError($"[PlayerInventory] ❌ No shelf in range for client {requesterClientId}!");
            ResetProcessingInteractionClientRpc();
            return;
        }

        Debug.Log($"[PlayerInventory] ✅ Found shelf: {nearbyShelf.name}");

        // Shelf dolu mu?
        if (nearbyShelf.IsFull())
        {
            Debug.Log("[PlayerInventory] ❌ Shelf is FULL!");
            ResetProcessingInteractionClientRpc();
            return;
        }

        // ✅ DEĞİŞTİRİLDİ: Server-side BoxInfo kontrolü
        if (!CanPlaceBoxOnShelfServerSide(_currentItemData))
        {
            Debug.Log("[PlayerInventory] ❌ Can only place FULL boxes on shelf!");
            // Normal drop yap
            PerformNormalDropServer(playerTransform);
            ResetProcessingInteractionClientRpc();
            return;
        }

        // World item spawn et
        var worldItemPrefab = GetWorldItemPrefab(_currentItemData);
        if (worldItemPrefab == null)
        {
            Debug.LogError("[PlayerInventory] ❌ World item prefab is NULL!");
            ResetProcessingInteractionClientRpc();
            return;
        }

        var spawnPos = playerTransform.position + Vector3.up * 0.5f;
        var spawnedItem = Instantiate(worldItemPrefab, spawnPos, Quaternion.identity);
        var networkObject = spawnedItem.GetComponent<NetworkObject>();

        if (networkObject == null)
        {
            Debug.LogError("[PlayerInventory] ❌ NetworkObject component missing!");
            Destroy(spawnedItem);
            ResetProcessingInteractionClientRpc();
            return;
        }

        networkObject.Spawn();

        // World item ayarla
        var worldItem = spawnedItem.GetComponent<NetworkWorldItem>();
        if (worldItem != null)
        {
            worldItem.SetItemData(_currentItemData);

            // ✅ YENİ: BoxInfo'yu prefab'dan kopyala
            var worldBoxInfo = spawnedItem.GetComponent<BoxInfo>();
            var prefabBoxInfo = worldItemPrefab.GetComponent<BoxInfo>();
            if (worldBoxInfo != null && prefabBoxInfo != null)
            {
                worldBoxInfo.isFull = true; // Zaten kontrol ettik, full olmalı
                worldBoxInfo.boxType = prefabBoxInfo.boxType;
            }

            worldItem.DisablePickup();
        }

        Debug.Log("[PlayerInventory] ✅ Calling ShelfState.PlaceItemOnShelfFromServer");
        nearbyShelf.PlaceItemOnShelfFromServer(new NetworkObjectReference(networkObject), requesterClientId);

        // Player inventory temizle
        ClearInventoryState();

        if (_playerMovement != null)
        {
            _playerMovement.SetCarrying(false);
        }

        StartDropAnimationClientRpc();

        // Quest güncelle
        QuestManager.Instance?.IncrementQuestProgress(QuestType.PlaceOnShelf);

        Debug.Log($"[PlayerInventory] ✅ Item placed on shelf successfully by client {requesterClientId}!");
        ResetProcessingInteractionClientRpc();
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

    #region Client RPCs

    [ClientRpc]
    private void OnItemPickedUpClientRpc(ulong itemNetworkId)
    {
        // Remove item from local tracking
        NetworkWorldItem itemToRemove = null;

        for (int i = _itemsInRange.Count - 1; i >= 0; i--)
        {
            if (_itemsInRange[i] != null && _itemsInRange[i].NetworkObjectId == itemNetworkId)
            {
                itemToRemove = _itemsInRange[i];
                _itemsInRange.RemoveAt(i);
                break;
            }
        }

        // Update targeting
        if (_targetedItem != null && _targetedItem.NetworkObjectId == itemNetworkId)
        {
            RemoveOutlineFromItem(_targetedItem);
            _targetedItem = null;
            UpdateTargetedItem();
        }

        // Cleanup picked up item
        if (itemToRemove != null)
        {
            RemoveOutlineFromItem(itemToRemove);
            if (itemToRemove.gameObject != null)
            {
                itemToRemove.gameObject.SetActive(false);
            }
        }

        // Disable pickup on network object
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(itemNetworkId, out var networkObject))
        {
            var worldItem = networkObject.GetComponent<NetworkWorldItem>();
            worldItem?.DisablePickup();
        }
    }

    [ClientRpc]
    private void ResetProcessingInteractionClientRpc()
    {
        _isProcessingInteraction = false;
    }

    [ClientRpc]
    private void StartPickupAnimationClientRpc()
    {
        StartCoroutine(PickupAnimationCoroutine());
    }

    [ClientRpc]
    private void StartDropAnimationClientRpc()
    {
        StartCoroutine(DropAnimationCoroutine());
    }

    [ClientRpc]
    private void StartThrowAnimationClientRpc()
    {
        StartCoroutine(ThrowAnimationCoroutine());
    }

    [ClientRpc]
    private void ClearHeldItemVisualClientRpc()
    {
        DestroyHeldItemVisual();
    }

    #endregion

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
        }

        _isAnimating = false;
    }

    private IEnumerator DropAnimationCoroutine()
    {
        _isAnimating = true;

        _playerMovement?.SetCarrying(false);
        DestroyHeldItemVisual();

        yield return new WaitForSeconds(0.05f);

        _isAnimating = false;
    }

    private IEnumerator ThrowAnimationCoroutine()
    {
        _isAnimating = true;

        _playerMovement?.SetCarrying(false);

        yield return new WaitForSeconds(0.05f);

        // Wait for network sync
        float timeout = 0f;
        while (_hasItem.Value && timeout < ANIMATION_TIMEOUT)
        {
            timeout += Time.deltaTime;
            yield return null;
        }

        DestroyHeldItemVisual();

        yield return new WaitForSeconds(0.05f);

        _isAnimating = false;
    }

    #endregion

    #region Network Event Handlers

    private void HandleHasItemChanged(bool previousValue, bool newValue)
    {
        if (IsOwner) return; // Owner handles this locally

        if (newValue && !previousValue)
        {
            StartCoroutine(PickupAnimationCoroutine());
        }
        else if (!newValue && previousValue)
        {
            StartCoroutine(DropAnimationCoroutine());
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
            PreserveBoxInfoOnWorldItem(worldItem.gameObject);
            StartCoroutine(DelayedEnablePickup(worldItem));

            if (force != Vector3.zero)
            {
                worldItem.SetThrowForce(force);
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

    private void PreserveBoxInfoOnWorldItem(GameObject worldItem)
    {
        if (_heldItemVisual == null) return;

        var heldBoxInfo = _heldItemVisual.GetComponent<BoxInfo>();
        var worldBoxInfo = worldItem.GetComponent<BoxInfo>();

        if (heldBoxInfo != null && worldBoxInfo != null)
        {
            worldBoxInfo.isFull = heldBoxInfo.isFull;
            worldBoxInfo.boxType = heldBoxInfo.boxType;
            Debug.Log($"[PlayerInventory] Preserved box info on world item: isFull={worldBoxInfo.isFull}");
        }
    }

    private static GameObject GetWorldItemPrefab(ItemData itemData)
    {
        return itemData?.worldPrefab;
    }

    #endregion

    #region Item Data Management

    private static ItemData GetItemDataFromID(int itemID)
    {
        var allItems = Resources.LoadAll<ItemData>("Items");

        foreach (var item in allItems)
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

    #region Thread-Safe Item Locking

    private static bool IsItemLocked(ulong itemNetworkId)
    {
        lock (s_lockObject)
        {
            if (!s_itemPickupLocks.TryGetValue(itemNetworkId, out float lockTime))
            {
                return false;
            }

            bool isLocked = (Time.time - lockTime) < PICKUP_LOCK_DURATION;
            if (!isLocked)
            {
                s_itemPickupLocks.Remove(itemNetworkId);
            }

            return isLocked;
        }
    }

    private static bool TryLockItem(ulong itemNetworkId)
    {
        lock (s_lockObject)
        {
            if (s_itemPickupLocks.TryGetValue(itemNetworkId, out float lockTime))
            {
                if ((Time.time - lockTime) < PICKUP_LOCK_DURATION)
                {
                    Debug.LogWarning($"[PlayerInventory] Item {itemNetworkId} is already locked by another player!");
                    return false;
                }
            }

            s_itemPickupLocks[itemNetworkId] = Time.time;
            Debug.Log($"[PlayerInventory] Item {itemNetworkId} locked at {Time.time}");
            return true;
        }
    }

    private static void UnlockItem(ulong itemNetworkId)
    {
        lock (s_lockObject)
        {
            if (s_itemPickupLocks.Remove(itemNetworkId))
            {
                Debug.Log($"[PlayerInventory] Lock removed for item {itemNetworkId}");
            }
        }
    }

    private IEnumerator LockCleanupLoop()
    {
        var waitInterval = new WaitForSeconds(1f);

        while (true)
        {
            yield return waitInterval;

            var expiredKeys = new List<ulong>();
            float currentTime = Time.time;

            lock (s_lockObject)
            {
                foreach (var kvp in s_itemPickupLocks)
                {
                    if (currentTime - kvp.Value > PICKUP_LOCK_DURATION)
                    {
                        expiredKeys.Add(kvp.Key);
                    }
                }

                foreach (ulong key in expiredKeys)
                {
                    s_itemPickupLocks.Remove(key);
                    Debug.Log($"[PlayerInventory] Lock expired for item {key}");
                }
            }
        }
    }

    private void CleanupServerLocks()
    {
        if (IsServer)
        {
            lock (s_lockObject)
            {
                s_itemPickupLocks.Clear();
            }
        }
    }

    #endregion

    #region Audio System

    private enum SoundType
    {
        Pickup,
        Drop,
        PlaceOnTable,
        PlaceOnShelf,
        TakeFromTable,
        TakeFromShelf
    }

    private void PlaySound(SoundType soundType)
    {
        var clip = GetAudioClip(soundType);
        if (clip == null || _audioSource == null) return;

        UpdateAudioVolume();
        _audioSource.PlayOneShot(clip);

        if (IsOwner)
        {
            PlayInventorySoundServerRpc((int)soundType);
        }
    }

    private AudioClip GetAudioClip(SoundType soundType)
    {
        return soundType switch
        {
            SoundType.Pickup => pickupSound,
            SoundType.Drop => dropSound,
            SoundType.PlaceOnTable => placeOnTableSound ?? dropSound,
            SoundType.PlaceOnShelf => placeOnShelfSound ?? dropSound,
            SoundType.TakeFromTable => takeFromTableSound ?? pickupSound,
            SoundType.TakeFromShelf => takeFromShelfSound ?? pickupSound,
            _ => null
        };
    }

    private void UpdateAudioVolume()
    {
        if (_audioSource == null) return;

        float finalVolume = inventorySoundVolume;

        if (_settingsManager != null)
        {
            finalVolume *= _settingsManager.GetSFXVolume() * _settingsManager.GetMasterVolume();
        }

        _audioSource.volume = finalVolume;
    }

    [ServerRpc]
    private void PlayInventorySoundServerRpc(int soundTypeIndex)
    {
        PlayInventorySoundClientRpc(soundTypeIndex);
    }

    [ClientRpc]
    private void PlayInventorySoundClientRpc(int soundTypeIndex)
    {
        if (IsOwner || _audioSource == null) return;

        var clip = GetAudioClip((SoundType)soundTypeIndex);
        if (clip != null)
        {
            UpdateAudioVolume();
            _audioSource.PlayOneShot(clip);
        }
    }

    #endregion

    #region Public API - External Interactions

    public void DropItemToPosition(Vector3 position, Action<NetworkObject> onDropped)
    {
        if (_hasItem.Value)
        {
            StartCoroutine(DropItemToPositionCoroutine(position, onDropped));
        }
    }

    private IEnumerator DropItemToPositionCoroutine(Vector3 position, Action<NetworkObject> onDropped)
    {
        if (_currentItemData == null)
        {
            yield break;
        }

        var worldItemPrefab = GetWorldItemPrefab(_currentItemData);
        if (worldItemPrefab == null)
        {
            yield break;
        }

        var spawnedItem = Instantiate(worldItemPrefab, position, Quaternion.identity);
        var networkObject = spawnedItem.GetComponent<NetworkObject>();

        if (networkObject != null)
        {
            networkObject.Spawn();

            var worldItem = spawnedItem.GetComponent<NetworkWorldItem>();
            if (worldItem != null)
            {
                worldItem.SetItemData(_currentItemData);
                worldItem.EnablePickup();
            }

            _hasItem.Value = false;
            _currentItemID.Value = -1;

            _playerMovement?.SetCarrying(false);
            DestroyHeldItemVisual();

            onDropped?.Invoke(networkObject);
        }

        yield return null;
    }

    public void PickupItemFromTable(NetworkObject itemNetworkObject, Action onPickedUp)
    {
        if (!_hasItem.Value)
        {
            StartCoroutine(PickupItemFromTableCoroutine(itemNetworkObject, onPickedUp));
        }
    }

    private IEnumerator PickupItemFromTableCoroutine(NetworkObject itemNetworkObject, Action onPickedUp)
    {
        var worldItem = itemNetworkObject.GetComponent<NetworkWorldItem>();
        if (worldItem == null)
        {
            yield break;
        }

        var itemData = worldItem.ItemData;

        itemNetworkObject.Despawn();

        _hasItem.Value = true;
        _currentItemID.Value = GetItemID(itemData);

        _playerMovement?.SetCarrying(true);

        yield return new WaitForSeconds(0.01f);

        SpawnHeldItemVisual();
        onPickedUp?.Invoke();
    }

    [ServerRpc(RequireOwnership = false)]
    public void ClearCurrentItemServerRpc()
    {
        if (!_hasItem.Value) return;

        _hasItem.Value = false;
        _currentItemID.Value = -1;

        _playerMovement?.SetCarrying(false);
        ClearHeldItemVisualClientRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    public void SetInventoryStateServerRpc(bool hasItemValue, int itemID)
    {
        _hasItem.Value = hasItemValue;
        _currentItemID.Value = itemID;

        _playerMovement?.SetCarrying(hasItemValue);

        if (hasItemValue)
        {
            StartPickupAnimationClientRpc();
        }
        else
        {
            ClearHeldItemVisualClientRpc();
            StartDropAnimationClientRpc();
        }

        Debug.Log($"[PlayerInventory] Inventory state set: hasItem={hasItemValue}, itemID={itemID}");
    }

    [ServerRpc(RequireOwnership = false)]
    public void TriggerDropAnimationServerRpc()
    {
        StartDropAnimationClientRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    public void GiveItemDirectlyServerRpc(int itemID)
    {
        if (_hasItem.Value) return;

        var itemData = GetItemDataFromID(itemID);
        if (itemData == null) return;

        _hasItem.Value = true;
        _currentItemID.Value = itemID;

        _playerMovement?.SetCarrying(true);
        StartPickupAnimationClientRpc();

        Debug.Log($"[PlayerInventory] Item given directly to player: {itemData.itemName}");
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