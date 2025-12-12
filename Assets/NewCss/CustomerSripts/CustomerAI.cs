using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace NewCss
{
    /// <summary>
    /// Müşteri AI davranışlarını yöneten sınıf. 
    /// Kuyruk yönetimi, oyuncu etkileşimi, ürün yerleştirme ve çıkış işlemlerini kontrol eder. 
    /// Network senkronizasyonu ile multiplayer desteği sağlar.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(SphereCollider))]
    public class CustomerAI : NetworkBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[CustomerAI]";
        private const string PLAYER_TAG = "Character";
        private const string ANIMATOR_SPEED_PARAM = "Speed";
        private const float DESTINATION_THRESHOLD = 0.1f;
        private const float PICKUP_CHECK_INTERVAL = 0.1f;
        private const float PRODUCT_PLACEMENT_DELAY = 0.1f;

        #endregion

        #region Enums

        private enum CustomerState
        {
            MovingToQueue = 0,
            WaitingInQueue = 1,
            Service = 2,
            WaitingForPickup = 3,
            Exiting = 4
        }

        #endregion

        #region Serialized Fields

        [Header("=== PLAYER REFERENCE ===")]
        [SerializeField, Tooltip("Cache'lenmiş player movement referansı")]
        public PlayerMovement cachedPlayerMovement;
        [Header("=== PRODUCT ASSIGNMENT ===")]
        [SerializeField, Tooltip("Son kullanılan ürün geçmişi boyutu")]
        public int recentProductHistorySize = 5; // 3'ten 5'e çıkar

        [Header("=== PREFAB MODE ===")]
        [SerializeField, Tooltip("Prefab modunda mı? (AI devre dışı)")]
        public bool isPrefabMode = false;

        [Header("=== WAIT BAR ===")]
        [SerializeField, Tooltip("Bekleme çubuğu referansı")]
        public WaitBar waitBar;

        [Header("=== CANVAS SETTINGS ===")]
        [SerializeField, Tooltip("Bekleme canvas'ı")]
        public Canvas waitCanvas;

        [SerializeField, Tooltip("Timer başlayana kadar canvas'ı gizle")]
        public bool hideCanvasUntilTimer = true;

        [Header("=== WAIT TIME SETTINGS ===")]
        [SerializeField, Tooltip("Minimum bekleme süresi")]
        public float minWaitTime = 10f;

        [SerializeField, Tooltip("Maximum bekleme süresi")]
        public float maxWaitTime = 20f;

        [SerializeField, Tooltip("Etkileşim süresi")]
        public float interactionTime = 5f;

        [Header("=== INTERACTION SETTINGS ===")]
        [SerializeField, Tooltip("Etkileşim menzili")]
        public float interactionRange = 2f;

        #region Private Fields - Product History

        private readonly Queue<int> _recentProductIndices = new();
        private int _lastProductIndex = -1; // Ardışık tekrar engeli için

#endregion


        [Header("=== INTERACTION SOUNDS ===")]
        [SerializeField, Tooltip("Etkileşim sesleri")]
        public AudioClip[] interactionSounds;

        [Header("=== PRODUCTS ===")]
        [SerializeField, Tooltip("Ürün prefab'ları")]
        public GameObject[] productPrefabs;

        [Header("=== ITEM DETECTION ===")]
        [SerializeField, Tooltip("Item algılama layer mask'i")]
        public LayerMask itemLayerMask = -1;

        [SerializeField, Tooltip("Algılama yarıçapı")]
        public float detectionRadius = 1.5f;

        [Header("=== MANAGER & POINTS ===")]
        [SerializeField, Tooltip("Müşteri yöneticisi")]
        public CustomerManager manager;

        [SerializeField, Tooltip("Çıkış noktası")]
        public Transform exitPoint;

        [Header("=== TABLES ===")]
        [SerializeField, Tooltip("Hedef masalar")]
        public DisplayTable[] targetTables;

        [SerializeField, Tooltip("Ana hedef masa (legacy)")]
        public DisplayTable targetTable;

        [SerializeField, Tooltip("Ürün bırakma masası")]
        public DisplayTable dropOffTable;

        #endregion

        #region Network Variables

        private readonly NetworkVariable<ulong> _networkPlayerInRangeClientId = new(ulong.MaxValue);
        private readonly NetworkVariable<bool> _networkWaitTimeStarted = new(false);
        private readonly NetworkVariable<bool> _networkIsInInteraction = new(false);
        private readonly NetworkVariable<float> _networkWaitBarTime = new(0f);
        private readonly NetworkVariable<bool> _networkShowCanvas = new(false);
        private readonly NetworkVariable<float> _networkAnimatorSpeed = new(0f);
        private readonly NetworkVariable<int> _networkState = new(0);
        private readonly NetworkVariable<int> _networkAssignedProductIndex = new(-1);
        private readonly NetworkVariable<ulong> _networkPlacedProductId = new(0);

        #endregion

        #region Private Fields

        // Components
        private NavMeshAgent _navAgent;
        private Animator _animator;
        private AudioSource _audioSource;
        private SphereCollider _interactionCollider;

        // State
        private CustomerState _state = CustomerState.MovingToQueue;
        private Vector3 _queueTarget;
        private int _targetQueueIndex = -1;
        private int _assignedProductIndex = -1;

        // Interaction
        private bool _hasInteracted;
        private bool _isInInteraction;
        private bool _hasTimedOut;
        private bool _waitTimeStarted;
        private float _actualWaitTime;

        // Player tracking
        private ulong _interactingPlayerId = ulong.MaxValue;
        private PlayerMovement _interactingPlayer;

        // Product
        private GameObject _placedProduct;
        private NetworkObject _placedProductNetworkObject;

        #endregion

        #region Public Properties

        /// <summary>
        /// Hedef kuyruk index'i
        /// </summary>
        public int TargetQueueIndex => _targetQueueIndex;

        /// <summary>
        /// Etkileşim tamamlandı mı? 
        /// </summary>
        public bool HasInteracted => _hasInteracted;

        /// <summary>
        /// Mevcut durum
        /// </summary>
        public int CurrentState => (int)_state;

        #endregion

        #region Network Lifecycle

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (!IsServer)
            {
                SubscribeToNetworkEvents();
            }

            InitializeComponents();

            if (IsServer)
            {
                InitializeServerState();
            }
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer)
            {
                UnsubscribeFromNetworkEvents();
            }

            base.OnNetworkDespawn();
        }

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            if (!IsSpawned) return;
            InitializeComponents();
        }

        private void Update()
        {
            if (isPrefabMode) return;

            if (IsServer)
            {
                ServerUpdate();
            }

            if (IsClient)
            {
                ClientUpdate();
            }
        }

        #endregion

        #region Initialization

        private void InitializeComponents()
        {
            _navAgent = GetComponent<NavMeshAgent>();
            _animator = GetComponent<Animator>();

            InitializeAudioSource();
            InitializeInteractionCollider();
            InitializeWaitBar();
            InitializeCanvas();

            if (isPrefabMode)
            {
                SetPrefabModeComponents(true);
            }
        }

        private void InitializeAudioSource()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }

            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 1f;
        }

        private void InitializeInteractionCollider()
        {
            _interactionCollider = GetComponent<SphereCollider>();
            _interactionCollider.isTrigger = true;
            _interactionCollider.radius = interactionRange;
        }

        private void InitializeWaitBar()
        {
            if (waitBar == null)
            {
                waitBar = GetComponentInChildren<WaitBar>();
            }

            if (waitBar == null)
            {
                waitBar = GetComponent<WaitBar>();
            }
        }

        private void InitializeCanvas()
        {
            if (waitCanvas == null)
            {
                waitCanvas = GetComponentInChildren<Canvas>();
            }

            if (hideCanvasUntilTimer && waitCanvas != null)
            {
                waitCanvas.gameObject.SetActive(false);
            }
        }

        private void InitializeServerState()
        {
            _actualWaitTime = Random.Range(minWaitTime, maxWaitTime);

            if (manager != null && HasValidProductPrefabs())
            {
                _assignedProductIndex = manager.GetRandomProductIndexExcludingRecent(productPrefabs.Length);
                _networkAssignedProductIndex.Value = _assignedProductIndex;

                Debug.Log($"{LOG_PREFIX} Spawned with product index: {_assignedProductIndex} ({productPrefabs[_assignedProductIndex].name})");
            }
        }

        private bool HasValidProductPrefabs()
        {
            return productPrefabs != null && productPrefabs.Length > 0;
        }

        #endregion

        #region Network Event Subscriptions

        private void SubscribeToNetworkEvents()
        {
            _networkWaitTimeStarted.OnValueChanged += HandleWaitTimeStartedChanged;
            _networkIsInInteraction.OnValueChanged += HandleInteractionStateChanged;
            _networkWaitBarTime.OnValueChanged += HandleWaitBarTimeChanged;
            _networkShowCanvas.OnValueChanged += HandleShowCanvasChanged;
            _networkAnimatorSpeed.OnValueChanged += HandleAnimatorSpeedChanged;
            _networkState.OnValueChanged += HandleStateChanged;
            _networkAssignedProductIndex.OnValueChanged += HandleAssignedProductIndexChanged;
        }

        private void UnsubscribeFromNetworkEvents()
        {
            _networkWaitTimeStarted.OnValueChanged -= HandleWaitTimeStartedChanged;
            _networkIsInInteraction.OnValueChanged -= HandleInteractionStateChanged;
            _networkWaitBarTime.OnValueChanged -= HandleWaitBarTimeChanged;
            _networkShowCanvas.OnValueChanged -= HandleShowCanvasChanged;
            _networkAnimatorSpeed.OnValueChanged -= HandleAnimatorSpeedChanged;
            _networkState.OnValueChanged -= HandleStateChanged;
            _networkAssignedProductIndex.OnValueChanged -= HandleAssignedProductIndexChanged;
        }

        #endregion

        #region Server Update

        private void ServerUpdate()
        {
            UpdateAnimator();
            CheckWaitTimeExpired();
            ProcessCurrentState();
        }

        private void ProcessCurrentState()
        {
            switch (_state)
            {
                case CustomerState.MovingToQueue:
                    ProcessMovingToQueue();
                    break;
                case CustomerState.WaitingInQueue:
                    ProcessWaitingInQueue();
                    break;
                case CustomerState.Service:
                    // Server sadece state'i yönetir
                    break;
                case CustomerState.WaitingForPickup:
                    // Coroutine ile yönetiliyor
                    break;
                case CustomerState.Exiting:
                    ProcessExiting();
                    break;
            }
        }

        private void UpdateAnimator()
        {
            float targetSpeed = 0f;

            if (_state == CustomerState.MovingToQueue || _state == CustomerState.Exiting)
            {
                targetSpeed = _navAgent.velocity.magnitude;
            }

            float normalizedSpeed = _navAgent.speed > 0f ? targetSpeed / _navAgent.speed : 0f;
            _networkAnimatorSpeed.Value = normalizedSpeed;

            if (_animator != null)
            {
                _animator.SetFloat(ANIMATOR_SPEED_PARAM, normalizedSpeed);
            }
        }

        #endregion

        #region Client Update

        private void ClientUpdate()
        {
            if (_state == CustomerState.Service && !_hasInteracted)
            {
                CheckForInteractionInput();
            }
        }

        private void CheckForInteractionInput()
        {
            if (!CheckIfLocalPlayerInRange()) return;

            if (Input.GetKeyDown(KeyCode.E))
            {
                RequestInteractionServerRpc(NetworkManager.Singleton.LocalClientId);
            }
        }

        private bool CheckIfLocalPlayerInRange()
        {
            if (NetworkManager.Singleton == null) return false;

            var localClientId = NetworkManager.Singleton.LocalClientId;
            var playerTransform = FindPlayerTransform(localClientId);

            if (playerTransform == null) return false;

            float distance = Vector3.Distance(transform.position, playerTransform.position);
            return distance <= interactionRange;
        }

        private Transform FindPlayerTransform(ulong clientId)
        {
            foreach (var netObj in FindObjectsOfType<NetworkObject>())
            {
                if (netObj.OwnerClientId == clientId && netObj.CompareTag(PLAYER_TAG))
                {
                    return netObj.transform;
                }
            }

            return null;
        }

        #endregion

        #region Queue Management

        public void SetQueueTarget(Vector3 target, int queueIndex)
        {
            if (isPrefabMode) return;

            _queueTarget = target;
            _targetQueueIndex = queueIndex;

            if (_navAgent == null)
            {
                _navAgent = GetComponent<NavMeshAgent>();
            }

            _navAgent.SetDestination(_queueTarget);
            SetState(CustomerState.MovingToQueue);
        }

        private void ProcessMovingToQueue()
        {
            if (HasReachedDestination())
            {
                SetState(CustomerState.WaitingInQueue);

                if (!_waitTimeStarted)
                {
                    StartWaitTime();
                }
            }
        }

        private void ProcessWaitingInQueue()
        {
            if (manager != null && manager.IsFirstInQueue(this))
            {
                BeginService();
            }
        }

        private bool HasReachedDestination()
        {
            return !_navAgent.pathPending &&
                   _navAgent.remainingDistance <= _navAgent.stoppingDistance + DESTINATION_THRESHOLD;
        }

        public int GetTargetQueueIndex() => _targetQueueIndex;

        #endregion

        #region Wait Time Management

        private void StartWaitTime()
        {
            _waitTimeStarted = true;
            _hasTimedOut = false;
            _hasInteracted = false;
            _isInInteraction = false;

            // Network sync
            _networkWaitTimeStarted.Value = true;
            _networkShowCanvas.Value = true;
            _networkWaitBarTime.Value = _actualWaitTime;
            _networkIsInInteraction.Value = false;

            // UI
            if (hideCanvasUntilTimer && waitCanvas != null)
            {
                waitCanvas.gameObject.SetActive(true);
            }

            if (waitBar != null)
            {
                waitBar.StartWaitBar(_actualWaitTime);
            }
        }

        private void CheckWaitTimeExpired()
        {
            if (_isInInteraction || _hasTimedOut || _state == CustomerState.Exiting ||
                _hasInteracted || _state == CustomerState.WaitingForPickup)
            {
                return;
            }

            if (_waitTimeStarted && waitBar != null && waitBar.GetRemainingTime() <= 0)
            {
                HandleTimeUp();
            }
        }

        private void HandleTimeUp()
        {
            if (_hasTimedOut || _state == CustomerState.Exiting) return;

            _hasTimedOut = true;

            // Etkileşimi iptal et
            if (_isInInteraction)
            {
                CancelCurrentInteraction();
            }

            // UI gizle
            HideWaitUI();

            // Prestige cezası
            ApplyPrestigePenalty();

            // Çıkışa geç
            TransitionToExit();
        }

        private void CancelCurrentInteraction()
        {
            StopAllCoroutines();
            _isInInteraction = false;
            _networkIsInInteraction.Value = false;

            if (_interactingPlayerId != ulong.MaxValue)
            {
                UnlockSpecificPlayerClientRpc(_interactingPlayerId);
                ClearInteractingPlayer();
            }
        }

        private void HideWaitUI()
        {
            _networkShowCanvas.Value = false;

            if (waitBar != null)
            {
                waitBar.HideBar();
            }

            if (waitCanvas != null)
            {
                waitCanvas.gameObject.SetActive(false);
            }
        }

        private void ApplyPrestigePenalty()
        {
            if (!_hasInteracted && PrestigeManager.Instance != null)
            {
                PrestigeManager.Instance.ModifyPrestige(-0.03f);
            }

            // Notify quest system about customer timeout
            if (!_hasInteracted)
            {
                Quest.QuestTracker.NotifyCustomerTimeout();
            }
        }

        #endregion

        #region Service & Interaction

        private void BeginService()
        {
            SetState(CustomerState.Service);
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestInteractionServerRpc(ulong requestingPlayerId, ServerRpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;

            // Güvenlik kontrolü
            if (senderClientId != requestingPlayerId)
            {
                Debug.LogWarning($"{LOG_PREFIX} Client {senderClientId} tried to interact as {requestingPlayerId}!");
                return;
            }

            // State kontrolü
            if (_state != CustomerState.Service || _hasInteracted || _isInInteraction)
            {
                return;
            }

            // Range kontrolü
            if (ValidatePlayerInRange(senderClientId))
            {
                StartInteraction(senderClientId);
            }
        }

        private bool ValidatePlayerInRange(ulong playerId)
        {
            foreach (var netObj in FindObjectsOfType<NetworkObject>())
            {
                if (netObj.OwnerClientId != playerId || !netObj.CompareTag(PLAYER_TAG))
                {
                    continue;
                }

                float distance = Vector3.Distance(transform.position, netObj.transform.position);
                if (distance <= interactionRange)
                {
                    _interactingPlayer = GetPlayerMovement(netObj);
                    return true;
                }
            }

            return false;
        }

        private PlayerMovement GetPlayerMovement(NetworkObject netObj)
        {
            var playerMovement = netObj.GetComponent<PlayerMovement>();

            if (playerMovement == null)
            {
                playerMovement = netObj.GetComponentInChildren<PlayerMovement>();
            }

            if (playerMovement == null)
            {
                playerMovement = netObj.GetComponentInParent<PlayerMovement>();
            }

            return playerMovement;
        }

        private void StartInteraction(ulong playerId = ulong.MaxValue)
        {
            if (_hasInteracted || _isInInteraction) return;

            if (playerId != ulong.MaxValue)
            {
                _interactingPlayerId = playerId;
            }

            _hasInteracted = true;
            _isInInteraction = true;

            // Network sync
            _networkIsInInteraction.Value = true;
            _networkWaitBarTime.Value = interactionTime;

            // UI
            if (waitBar != null)
            {
                waitBar.StartWaitBar(interactionTime);
            }

            // Ses
            PlayRandomInteractionSound();

            // Player'ı kilitle
            if (_interactingPlayerId != ulong.MaxValue)
            {
                LockSpecificPlayerClientRpc(_interactingPlayerId);
            }

            // Timer başlat
            StartCoroutine(InteractionTimerCoroutine());
        }

        private IEnumerator InteractionTimerCoroutine()
        {
            yield return new WaitForSeconds(interactionTime);
            CompleteInteraction();
        }

        private void CompleteInteraction()
        {
            if (_hasTimedOut) return;

            _isInInteraction = false;
            _networkIsInInteraction.Value = false;

            // UI gizle
            HideWaitUI();

            _waitTimeStarted = false;
            _networkWaitTimeStarted.Value = false;

            // Ürün yerleştir (Coroutine ile)
            StartCoroutine(PlaceProductCoroutine());
        }

        private IEnumerator PlaceProductCoroutine()
        {
            // Ürünü yerleştir
            _placedProduct = PlaceProductOnDropOffTable();

            // Kısa bir gecikme - physics'in settle olması için
            yield return new WaitForSeconds(PRODUCT_PLACEMENT_DELAY);

            if (_placedProduct != null)
            {
                // Physics'i tekrar kontrol et ve sabitle
                EnsureProductIsStable(_placedProduct);

                HandleSuccessfulInteraction();
                StartCoroutine(WaitForProductPickupCoroutine());
            }
            else
            {
                HandleFailedInteraction();
                TransitionToExit();
            }

            // Player'ı unlock et
            UnlockInteractingPlayer();
        }

        private void HandleSuccessfulInteraction()
        {
            // Prestige ödülü
            if (PrestigeManager.Instance != null)
            {
                PrestigeManager.Instance.ModifyPrestige(0.05f);
            }

            // Notify quest system
            Quest.QuestTracker.NotifyCustomerServed();
        }

        private void HandleFailedInteraction()
        {
            if (PrestigeManager.Instance != null)
            {
                PrestigeManager.Instance.ModifyPrestige(-0.03f);
            }
        }

        private void UnlockInteractingPlayer()
        {
            if (_interactingPlayerId != ulong.MaxValue)
            {
                UnlockSpecificPlayerClientRpc(_interactingPlayerId);
                ClearInteractingPlayer();
            }
        }

        private void ClearInteractingPlayer()
        {
            _interactingPlayerId = ulong.MaxValue;
            _interactingPlayer = null;
        }

        #endregion

        #region Product Placement - DÜZELTILMIŞ

        private GameObject PlaceProductOnDropOffTable()
        {
            if (dropOffTable == null || !HasValidProductPrefabs())
            {
                Debug.LogWarning($"{LOG_PREFIX} Cannot place product:  dropOffTable or productPrefabs is null");
                return null;
            }

            int productIndex = GetProductIndex();

            // Slot pozisyonunu al
            var slotPoints = dropOffTable.SlotPoints;
            int currentItemCount = dropOffTable.ItemCount;

            Vector3 spawnPosition;
            Quaternion spawnRotation;
            Transform parentSlot = null;

            if (slotPoints != null && slotPoints.Length > currentItemCount && slotPoints[currentItemCount] != null)
            {
                parentSlot = slotPoints[currentItemCount];
                spawnPosition = parentSlot.position;
                spawnRotation = parentSlot.rotation;
            }
            else
            {
                // Fallback
                spawnPosition = dropOffTable.transform.position + Vector3.up * 0.5f;
                spawnRotation = dropOffTable.transform.rotation;
                Debug.LogWarning($"{LOG_PREFIX} No valid slot found, using fallback position");
            }

            // Ürünü oluştur
            var product = Instantiate(productPrefabs[productIndex], spawnPosition, spawnRotation);

            // ÖNEMLİ:  Fizik ayarlarını HEMEN yap (spawn öncesi)
            var rb = product.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.constraints = RigidbodyConstraints.FreezeAll;
            }

            // ItemFreezeSystem varsa devre dışı bırak
            var freezeSystem = product.GetComponent<ItemFreezeSystem>();
            if (freezeSystem != null)
            {
                freezeSystem.enabled = false;
            }

            // Parent ayarla
            if (parentSlot != null)
            {
                product.transform.SetParent(parentSlot, true);
                product.transform.localPosition = Vector3.zero;
                product.transform.localRotation = Quaternion.identity;
            }
            else
            {
                product.transform.SetParent(dropOffTable.transform, true);
            }

            // Network spawn
            var networkObject = product.GetComponent<NetworkObject>();
            if (networkObject != null)
            {
                networkObject.Spawn();

                // Spawn sonrası fizik ayarlarını tekrar uygula
                StartCoroutine(EnsureProductFrozenAfterSpawn(product));
            }

            // DisplayTable'a kaydet
            dropOffTable.PlaceItemInstance(product);

            Debug.Log($"{LOG_PREFIX} Placed product:  {productPrefabs[productIndex].name} at slot {currentItemCount}");

            return product;
        }
        private IEnumerator EnsureProductFrozenAfterSpawn(GameObject product)
        {
            yield return null; // Bir frame bekle

            if (product == null) yield break;

            var rb = product.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.constraints = RigidbodyConstraints.FreezeAll;
            }

            var freezeSystem = product.GetComponent<ItemFreezeSystem>();
            if (freezeSystem != null)
            {
                freezeSystem.enabled = false;
            }
        }


        private IEnumerator SyncProductParentAfterSpawn(NetworkObject networkObject, Transform parentSlot)
        {
            // Bir frame bekle
            yield return null;

            if (networkObject != null && networkObject.IsSpawned)
            {
                // Pozisyonu ve rotation'ı tekrar ayarla
                networkObject.transform.SetParent(parentSlot, true);
                networkObject.transform.localPosition = Vector3.zero;
                networkObject.transform.localRotation = Quaternion.identity;

                // Client'lara sync et
                SyncProductTransformClientRpc(
                    networkObject.NetworkObjectId,
                    parentSlot.position,
                    parentSlot.rotation
                );
            }
        }

        [ClientRpc]
        private void SyncProductTransformClientRpc(ulong productNetworkId, Vector3 position, Quaternion rotation)
        {
            if (IsServer) return;

            // Network object'i bul
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(productNetworkId, out var networkObject))
            {
                // Physics'i devre dışı bırak
                DisableProductPhysics(networkObject.gameObject);

                // Transform'u ayarla
                networkObject.transform.position = position;
                networkObject.transform.rotation = rotation;
            }
        }

        private (Transform slotTransform, int slotIndex) GetProductSlotInfo()
        {
            var slotPoints = dropOffTable.SlotPoints;

            if (slotPoints == null || slotPoints.Length == 0)
            {
                Debug.LogError($"{LOG_PREFIX} SlotPoints is null or empty");
                return (null, -1);
            }

            int slotIndex = dropOffTable.ItemCount;

            if (slotIndex >= slotPoints.Length)
            {
                Debug.LogError($"{LOG_PREFIX} No available slot.  ItemCount: {slotIndex}, SlotPoints:  {slotPoints.Length}");
                return (null, -1);
            }

            var slot = slotPoints[slotIndex];

            if (slot == null)
            {
                Debug.LogError($"{LOG_PREFIX} Slot at index {slotIndex} is null");
                return (null, -1);
            }

            return (slot, slotIndex);
        }

        private int GetProductIndex()
        {
            if (_assignedProductIndex >= 0 && _assignedProductIndex < productPrefabs.Length)
            {
                return _assignedProductIndex;
            }

            return Random.Range(0, productPrefabs.Length);
        }

        /// <summary>
        /// Ürünün physics'ini tamamen devre dışı bırakır
        /// </summary>
        private void DisableProductPhysics(GameObject product)
        {
            if (product == null) return;

            // Rigidbody'leri işle
            var rigidbodies = product.GetComponentsInChildren<Rigidbody>(true);
            foreach (var rb in rigidbodies)
            {
                // Önce velocity'leri sıfırla
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;

                // Kinematic yap
                rb.isKinematic = true;
                rb.useGravity = false;

                // Constraints ekle
                rb.constraints = RigidbodyConstraints.FreezeAll;

                // Interpolation kapat
                rb.interpolation = RigidbodyInterpolation.None;

                // Sleep state'e al
                rb.Sleep();
            }

            // ItemFreezeSystem varsa devre dışı bırak
            var freezeSystems = product.GetComponentsInChildren<ItemFreezeSystem>(true);
            foreach (var freezeSystem in freezeSystems)
            {
                freezeSystem.enabled = false;
            }

            // Collider'ları trigger yap (opsiyonel - pickup için gerekebilir)
            // Eğer pickup için collider gerekiyorsa bu kısmı yorum satırı yap
            /*
            var colliders = product.GetComponentsInChildren<Collider>(true);
            foreach (var col in colliders)
            {
                col.isTrigger = true;
            }
            */
        }

        /// <summary>
        /// Ürünün stabil olduğundan emin olur
        /// </summary>
        private void EnsureProductIsStable(GameObject product)
        {
            if (product == null) return;

            var rigidbodies = product.GetComponentsInChildren<Rigidbody>(true);
            foreach (var rb in rigidbodies)
            {
                // Tekrar kontrol et
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.constraints = RigidbodyConstraints.FreezeAll;
                rb.Sleep();
            }

            // Transform'u parent'a göre sıfırla
            if (product.transform.parent != null)
            {
                product.transform.localPosition = Vector3.zero;
                product.transform.localRotation = Quaternion.identity;
            }
        }

        private IEnumerator WaitForProductPickupCoroutine()
        {
            SetState(CustomerState.WaitingForPickup);

            yield return null;

            while (_placedProduct != null && !_hasTimedOut)
            {
                // Ürün hala var mı?
                if (_placedProduct == null)
                {
                    Debug.Log($"{LOG_PREFIX} Product was picked up (null check)");
                    break;
                }

                // Ürün hala masada mı?
                if (!IsProductStillOnTable())
                {
                    Debug.Log($"{LOG_PREFIX} Product was picked up (table check)");
                    _placedProduct = null;
                    break;
                }

                // NetworkObject check
                if (_placedProductNetworkObject != null && !_placedProductNetworkObject.IsSpawned)
                {
                    Debug.Log($"{LOG_PREFIX} Product NetworkObject was despawned");
                    _placedProduct = null;
                    break;
                }

                yield return new WaitForSeconds(PICKUP_CHECK_INTERVAL);
            }

            TransitionToExit();
        }

        private bool IsProductStillOnTable()
        {
            if (_placedProduct == null)
            {
                return false;
            }

            // Parent kontrolü
            if (_placedProduct.transform.parent == null)
            {
                return false;
            }

            // dropOffTable kontrolü
            if (dropOffTable == null)
            {
                return false;
            }

            return IsChildOfDropOffTable(_placedProduct);
        }

        private bool IsChildOfDropOffTable(GameObject product)
        {
            if (dropOffTable == null || product == null) return false;

            var parent = product.transform.parent;

            while (parent != null)
            {
                // Ana tablo kontrolü
                if (parent == dropOffTable.transform)
                {
                    return true;
                }

                // Slot kontrolü
                var slotPoints = dropOffTable.SlotPoints;
                if (slotPoints != null)
                {
                    foreach (var slot in slotPoints)
                    {
                        if (slot != null && parent == slot)
                        {
                            return true;
                        }
                    }
                }

                parent = parent.parent;
            }

            return false;
        }

        #endregion

        #region Exit Management

        private void TransitionToExit()
        {
            StopAllCoroutines();
            SetState(CustomerState.Exiting);

            if (manager != null)
            {
                manager.NotifyCustomerDone(this);
            }

            if (_navAgent != null && exitPoint != null)
            {
                _navAgent.SetDestination(exitPoint.position);
            }
        }

        /// <summary>
        /// Gün sonu nedeniyle zorla çıkışa yönlendirir (CustomerManager tarafından çağrılır)
        /// </summary>
        public void ForceExitDueToEndOfDay()
        {
            if (_state == CustomerState.Exiting) return;

            Debug.Log($"{LOG_PREFIX} Customer forced to exit due to end of day");

            // Mevcut etkileşimleri iptal et
            if (_isInInteraction)
            {
                CancelCurrentInteraction();
            }

            // Bekleme UI'ını gizle
            HideWaitUI();

            // Çıkışa yönlendir
            TransitionToExit();
        }

        private void ProcessExiting()
        {
            if (HasReachedDestination())
            {
                DespawnCustomer();
            }
        }

        private void DespawnCustomer()
        {
            if (IsServer && NetworkObject != null)
            {
                NetworkObject.Despawn();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        #endregion

        #region State Management

        private void SetState(CustomerState newState)
        {
            _state = newState;
            _networkState.Value = (int)newState;
        }

        #endregion

        #region Audio

        private void PlayRandomInteractionSound()
        {
            if (interactionSounds == null || interactionSounds.Length == 0)
            {
                Debug.LogWarning($"{LOG_PREFIX} No interaction sounds assigned!");
                return;
            }

            int randomIndex = Random.Range(0, interactionSounds.Length);
            var selectedClip = interactionSounds[randomIndex];

            if (selectedClip == null) return;

            if (_audioSource != null)
            {
                _audioSource.PlayOneShot(selectedClip);
            }

            PlaySoundClientRpc(randomIndex);
        }

        [ClientRpc]
        private void PlaySoundClientRpc(int soundIndex)
        {
            if (IsServer) return;

            if (interactionSounds == null || soundIndex < 0 || soundIndex >= interactionSounds.Length)
            {
                return;
            }

            if (_audioSource != null && interactionSounds[soundIndex] != null)
            {
                _audioSource.PlayOneShot(interactionSounds[soundIndex]);
            }
        }

        #endregion

        #region Player Lock/Unlock

        [ClientRpc]
        private void LockSpecificPlayerClientRpc(ulong targetPlayerId)
        {
            if (NetworkManager.Singleton.LocalClientId != targetPlayerId) return;

            var playerMovement = FindLocalPlayerMovement();
            if (playerMovement != null)
            {
                Debug.Log($"{LOG_PREFIX} [Client {targetPlayerId}] Locking movement");
                playerMovement.LockMovement(true);
                playerMovement.LockAllInteractions(true);
            }
        }

        [ClientRpc]
        private void UnlockSpecificPlayerClientRpc(ulong targetPlayerId)
        {
            if (NetworkManager.Singleton.LocalClientId != targetPlayerId) return;

            var playerMovement = FindLocalPlayerMovement();
            if (playerMovement != null)
            {
                Debug.Log($"{LOG_PREFIX} [Client {targetPlayerId}] Unlocking movement");
                playerMovement.LockMovement(false);
                playerMovement.LockAllInteractions(false);
            }
        }

        private PlayerMovement FindLocalPlayerMovement()
        {
            foreach (var netObj in FindObjectsOfType<NetworkObject>())
            {
                if (netObj.IsOwner && netObj.CompareTag(PLAYER_TAG))
                {
                    return GetPlayerMovement(netObj);
                }
            }

            return null;
        }

        #endregion

        #region Prefab Mode

        public void SetPrefabMode(bool prefabMode)
        {
            isPrefabMode = prefabMode;
            SetPrefabModeComponents(prefabMode);
        }

        private void SetPrefabModeComponents(bool enabled)
        {
            if (_navAgent != null)
            {
                _navAgent.enabled = !enabled;
            }

            if (waitCanvas != null)
            {
                waitCanvas.gameObject.SetActive(!enabled);
            }
        }

        #endregion

        #region Network Event Handlers

        private void HandleWaitTimeStartedChanged(bool previousValue, bool newValue)
        {
            if (IsServer) return;

            if (newValue && hideCanvasUntilTimer && waitCanvas != null)
            {
                waitCanvas.gameObject.SetActive(true);
            }
        }

        private void HandleInteractionStateChanged(bool previousValue, bool newValue)
        {
            if (IsServer) return;
            _isInInteraction = newValue;
        }

        private void HandleWaitBarTimeChanged(float previousValue, float newValue)
        {
            if (IsServer) return;

            if (waitBar != null && newValue > 0)
            {
                waitBar.StartWaitBar(newValue);
            }
        }

        private void HandleShowCanvasChanged(bool previousValue, bool newValue)
        {
            if (IsServer) return;

            if (waitCanvas != null)
            {
                waitCanvas.gameObject.SetActive(newValue);
            }
        }

        private void HandleAnimatorSpeedChanged(float previousValue, float newValue)
        {
            if (IsServer) return;

            if (_animator != null)
            {
                _animator.SetFloat(ANIMATOR_SPEED_PARAM, newValue);
            }
        }

        private void HandleStateChanged(int previousValue, int newValue)
        {
            if (IsServer) return;
            _state = (CustomerState)newValue;
        }

        private void HandleAssignedProductIndexChanged(int previousValue, int newValue)
        {
            if (IsServer) return;

            _assignedProductIndex = newValue;
            Debug.Log($"{LOG_PREFIX} [Client] Received product index: {_assignedProductIndex}");
        }

        #endregion

        #region Debug & Editor

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Interaction range
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactionRange);

            // Detection radius
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, detectionRadius);

            // Debug info
            DrawDebugLabel();
        }

        private void DrawDebugLabel()
        {
            string debugInfo = BuildDebugInfo();
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2, debugInfo);
        }

        private string BuildDebugInfo()
        {
            var info = $"State: {_state}\nInteracted: {_hasInteracted}";

            if (IsServer)
            {
                info += $"\nWait Time: {_actualWaitTime:F1}s";

                if (_assignedProductIndex >= 0 && HasValidProductPrefabs() && _assignedProductIndex < productPrefabs.Length)
                {
                    info += $"\nProduct: {productPrefabs[_assignedProductIndex].name} ({_assignedProductIndex})";
                }

                if (_placedProduct != null)
                {
                    info += $"\nPlaced:  {_placedProduct.name}";
                    info += $"\nParent: {(_placedProduct.transform.parent != null ? _placedProduct.transform.parent.name : "null")}";
                }

                if (_interactingPlayerId != ulong.MaxValue)
                {
                    info += $"\nInteracting:  {_interactingPlayerId}";
                }
            }

            return info;
        }
#endif

        #endregion
    }
}