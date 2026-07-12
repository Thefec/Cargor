using NewCss;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;


public partial class PlayerInventory : NetworkBehaviour
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

    private Coroutine _currentAnimationCoroutine;
    private float _lastInputTime;
    private const float INPUT_SPAM_COOLDOWN = 0.15f;

    #endregion
    #region Static Fields (Thread-Safe Item Locking)

    private static readonly Dictionary<ulong, float> s_itemPickupLocks = new();
    private static readonly object s_lockObject = new();
    private static bool s_cleanupStarted;
    private static bool s_serverStoppedSubscribed;

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
    
    // Throw Mechanic - Orta tık ile fırlatma

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

        // Animasyon temizliği
        if (_currentAnimationCoroutine != null)
        {
            StopCoroutine(_currentAnimationCoroutine);
            _currentAnimationCoroutine = null;
        }
        _isAnimating = false;

        // Carrying state'i sıfırla
        _playerMovement?.SetCarrying(false);
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        StopDetectionSystem();
        CleanupOutlines();
        CleanupServerLocks();
    }

    private void Update()
    {
        if (!IsOwner) return;

        UpdateShelfItemSystem();
        HandleThrowInput();
        HandleInput();
    }

    private void HandleThrowInput()
    {
        if (!_hasItem.Value || _isProcessingInteraction || _isAnimating) return;

        if (InputBindingManager.GetActionDown(InputBindingManager.GameAction.Throw))
        {
            _lastInputTime = Time.time;
            HandleThrowInteraction();
        }
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

            // Host menüye dönüp yeniden host açtığında s_cleanupStarted bir daha
            // resetlenmediği için loop yeniden başlamıyordu (G19). Server tamamen
            // durduğunda static state'i resetleyip bir sonraki host açılışına hazırlıyoruz.
            if (!s_serverStoppedSubscribed && NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnServerStopped += HandleServerStoppedResetLockState;
                s_serverStoppedSubscribed = true;
            }
        }
    }

    private static void HandleServerStoppedResetLockState(bool wasHost)
    {
        lock (s_lockObject)
        {
            s_itemPickupLocks.Clear();
        }
        s_cleanupStarted = false;

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnServerStopped -= HandleServerStoppedResetLockState;
        }
        s_serverStoppedSubscribed = false;
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
        // Sadece owner'ın flag'ini sıfırla
        if (IsOwner)
        {
            _isProcessingInteraction = false;
        }
    }
    [ClientRpc]
    private void ResetProcessingInteractionTargetedClientRpc(ClientRpcParams clientRpcParams = default)
    {
        _isProcessingInteraction = false;
    }


    [ClientRpc]
    private void StartPickupAnimationClientRpc()
    {
        SafeStartAnimation(PickupAnimationCoroutine());
    }

    [ClientRpc]
    private void StartDropAnimationClientRpc()
    {
        SafeStartAnimation(DropAnimationCoroutine());
    }

    [ClientRpc]
    private void StartThrowAnimationClientRpc()
    {
        SafeStartAnimation(ThrowAnimationCoroutine());
    }

    [ClientRpc]
    private void ClearHeldItemVisualClientRpc()
    {
        DestroyHeldItemVisual();
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
}