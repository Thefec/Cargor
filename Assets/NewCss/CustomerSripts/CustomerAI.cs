using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
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
        private const float PRODUCT_POSITION_TOLERANCE = 0.5f; // Distance threshold for checking if product is still on table

        // İade (BoxRequest) görsel göstergesi — Kural 1 (havuz belgesi): özellik görsel olarak
        // apaçık olmalı. waitCanvas'ın world-space anchoredPosition'ı (Customer.prefab, Canvas
        // RectTransform) y≈2.54; gösterge onun biraz üstünde durur ki wait bar ile çakışmasın.
        private const float RETURN_INDICATOR_HEIGHT = 2.9f;
        private const float RETURN_INDICATOR_SCALE = 0.3f;
        private const float RETURN_INDICATOR_OFFSET_X = 0.28f;

        // Kutu paletiyle birebir aynı (NetworkWorldItem.cs GetColorForBoxType, BoxDestructionEffectManager.cs
        // redBoxColor/yellowBoxColor/blueBoxColor, Truck.cs TruckBlue) — ACES tonemapping altında
        // mavi→mor kaymasını önlemek için Blue kasıtlı olarak doygun DEĞİL (bkz. Truck.cs yorum).
        private static readonly Color ReturnIndicatorRedColor = new Color(0.8f, 0.2f, 0.2f);
        private static readonly Color ReturnIndicatorYellowColor = new Color(0.9f, 0.8f, 0.2f);
        private static readonly Color ReturnIndicatorBlueColor = new Color(0.2f, 0.4f, 0.8f);

        private static readonly int ReturnIndicatorBaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ReturnIndicatorColorId = Shader.PropertyToID("_Color");

        [Header("=== OUTLINE SETTINGS ===")]
        [SerializeField, Tooltip("Outline eklenecek hedef obje (boş bırakılırsa bu obje kullanılır)")]
        private GameObject outlineTarget; // Inspector'dan ayarlayabilirsiniz

        [SerializeField, Tooltip("Outline rengi")]
        private Color outlineColor = Color.green;

        [SerializeField, Tooltip("Outline kalınlığı")]
        private float outlineWidth = 5f;

        [SerializeField, Tooltip("Outline modu")]
        private Outline.Mode outlineMode = Outline.Mode.OutlineAll;

        private Outline _outline;

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

        /// <summary>
        /// Etkileşim yönü. ProductSupply = mevcut davranış (müşteri bir ÜRÜN bırakır, oyuncu alır).
        /// BoxRequest = iade (Faz A, gün 5+): müşteri bir KUTU rengi ister, oyuncu getirir teslim eder.
        /// İkisi karışmasın diye ayrı — BoxRequest modu _assignedProductIndex'i KULLANMAZ.
        /// </summary>
        private enum InteractionMode
        {
            ProductSupply = 0,
            BoxRequest = 1
        }

        #endregion

        #region Serialized Fields



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




        [Header("=== INTERACTION SOUNDS ===")]
        [SerializeField, Tooltip("Etkileşim sesleri")]
        public AudioClip[] interactionSounds;

        [Header("=== PRODUCTS ===")]
        [SerializeField, Tooltip("Ürün prefab'ları")]
        public GameObject[] productPrefabs;



        [Header("=== MANAGER & POINTS ===")]
        [SerializeField, Tooltip("Müşteri yöneticisi")]
        public CustomerManager manager;

        [SerializeField, Tooltip("Çıkış noktası")]
        public Transform exitPoint;

        [Header("=== TABLES ===")]

        [SerializeField, Tooltip("Ürün bırakma masası")]
        public DisplayTable dropOffTable;

        [Header("=== ECONOMY SETTINGS ===")]
        [SerializeField, Tooltip("Tüm ekonomi sabitlerini içeren ScriptableObject")]
        private GameEconomySettings economySettings;

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

        // Faz A — iade (BoxRequest) modu, gün 5+. ProductSupply modundan bağımsız ayrı alanlar.
        private readonly NetworkVariable<int> _networkInteractionMode = new((int)InteractionMode.ProductSupply);
        private readonly NetworkVariable<int> _networkRequestedReturnBoxType = new((int)BoxInfo.BoxType.Red);

        // Faz B — 2-item (dual item) modu, gün 9+. Mod seçiminden bağımsız ayrı eksen.
        private readonly NetworkVariable<bool> _networkIsDualItemMode = new(false);
        private readonly NetworkVariable<int> _networkAssignedProductIndex2 = new(-1);
        private readonly NetworkVariable<int> _networkRequestedReturnBoxType2 = new((int)BoxInfo.BoxType.Red);
        private readonly NetworkVariable<bool> _networkFirstReturnColorFulfilled = new(false);

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

        // Faz A — iade (BoxRequest) modu, gün 5+ (ayrı, ProductSupply alanlarıyla karışmaz).
        private InteractionMode _interactionMode = InteractionMode.ProductSupply;
        private BoxInfo.BoxType _requestedReturnBoxType = BoxInfo.BoxType.Red;

        // Faz B — 2-item (dual item) modu, gün 9+. ProductSupply/BoxRequest'in İKİSİNDE de
        // kullanılan opsiyonel "2. kalem" alanları — tekil alanları listeye çevirmek yerine
        // düşük riskli genişletme (bkz. plan §Faz B): kullanılmıyorsa -1/varsayılan kalır.
        private bool _isDualItemMode;
        private int _assignedProductIndex2 = -1;
        private BoxInfo.BoxType _requestedReturnBoxType2 = BoxInfo.BoxType.Red;
        private bool _firstReturnColorFulfilled;

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
        private int _placedProductSlotIndex = -1; // Cache slot index for faster checking

        // Faz B — 2-item (dual item) modu, gün 9+. İkinci ürün için ayrı takip alanları.
        private GameObject _placedProduct2;
        private NetworkObject _placedProductNetworkObject2;
        private int _placedProductSlotIndex2 = -1;

        // İade görsel göstergesi (Kural 1 — mekanik yazılı kural değil görsel olmalı). Yalnız
        // yerel/client-side: networked state (_interactionMode vb.) zaten senkron, bu sadece onu
        // okuyup küçük renkli birer küre gösteriyor. Dedicated server'da (IsClient false) hiç
        // oluşturulmaz — gereksiz GameObject/mesh yok.
        private GameObject _returnIndicatorRoot;
        private GameObject _returnIndicatorSphere1;
        private GameObject _returnIndicatorSphere2;
        private Renderer _returnIndicatorRenderer1;
        private Renderer _returnIndicatorRenderer2;

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

        /// <summary>
        /// Kuyrukta bekliyor ve henüz bir servis istasyonuna atanmamış mı? (CustomerManager
        /// AssignFreeServiceStations() bu bayrağı okuyarak boş istasyon ataması yapar.)
        /// </summary>
        public bool IsWaitingForService => _state == CustomerState.WaitingInQueue;

        /// <summary>
        /// Bu müşteri İade (BoxRequest) modunda mı? (Faz A, gün 5+). Networked — client'ta da
        /// doğru değeri okur. Graphics-UI departmanı görsel gösterge için bunu kullanabilir.
        /// </summary>
        public bool IsBoxRequestMode => _interactionMode == InteractionMode.BoxRequest;

        /// <summary>
        /// İade modunda müşterinin istediği kutu rengi. IsBoxRequestMode false iken anlamsızdır.
        /// </summary>
        public BoxInfo.BoxType RequestedReturnBoxType => _requestedReturnBoxType;

        /// <summary>
        /// Bu müşteri 2-item (dual item) modunda mı? (Faz B, gün 9+). Networked. Mod seçiminden
        /// bağımsız — hem ProductSupply hem BoxRequest ile birleşebilir.
        /// </summary>
        public bool IsDualItemMode => _isDualItemMode;

        /// <summary>
        /// Dual-item BoxRequest'te istenen ikinci renk. IsDualItemMode false veya IsBoxRequestMode
        /// false iken anlamsızdır.
        /// </summary>
        public BoxInfo.BoxType RequestedReturnBoxType2 => _requestedReturnBoxType2;

        /// <summary>
        /// Dual-item BoxRequest'te ilk renk zaten teslim edildi mi (müşteri ikinci rengi
        /// bekliyor)? Graphics-UI departmanı "1/2 teslim edildi" göstergesi için kullanabilir.
        /// </summary>
        public bool HasDeliveredFirstReturnItem => _firstReturnColorFulfilled;

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

            // G10 fix: event başladıktan sonra spawn olan customer da aktif event çarpanlarını alsın.
            // OnActiveEventChanged her peer'de yerel çalıştığından bu da her peer'de çağrılmalı.
            EventEffectManager.Instance?.ApplyEventEffectToNewObject(gameObject);

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

        private void Awake()
        {
            if (economySettings == null)
            {
                economySettings = Resources.Load<GameEconomySettings>("EkonomiAyarlari");
            }
        }

        private void Start()
        {
            if (!IsSpawned) return;
            InitializeComponents();

            // Outline setup'ı güvenli hale getir
            SetupOutlineSafe();
        }

        private void SetupOutlineSafe()
        {
            try
            {
                // Sadece Body mesh'ini bul
                GameObject targetObject = null;

                if (outlineTarget != null)
                {
                    targetObject = outlineTarget;
                }
                else
                {
                    // Body isimli child objeyi bul
                    Transform bodyTransform = transform.Find("Body_011");
                    if (bodyTransform == null)
                    {
                        // Alternatif isimler dene
                        foreach (Transform child in transform)
                        {
                            if (child.name.Contains("Body") || child.name.Contains("body"))
                            {
                                bodyTransform = child;
                                break;
                            }
                        }
                    }

                    if (bodyTransform != null)
                    {
                        targetObject = bodyTransform.gameObject;
                    }
                }

                if (targetObject == null)
                {
                    Debug.LogWarning($"{LOG_PREFIX} Body mesh not found for outline on {gameObject.name}");
                    return;
                }

                // Outline ekle
                _outline = targetObject.GetComponent<Outline>();
                if (_outline == null)
                {
                    var renderer = targetObject.GetComponent<Renderer>();
                    if (renderer == null)
                    {
                        Debug.LogWarning($"{LOG_PREFIX} No renderer found on {targetObject.name}");
                        return;
                    }

                    // Mesh'in Read/Write enabled olup olmadığını kontrol et
                    var meshFilter = targetObject.GetComponent<MeshFilter>();
                    if (meshFilter != null && meshFilter.sharedMesh != null)
                    {
                        if (!meshFilter.sharedMesh.isReadable)
                        {
                            Debug.LogWarning($"{LOG_PREFIX} Mesh '{meshFilter.sharedMesh.name}' is not readable.  Skipping outline.  Enable Read/Write in import settings.");
                            return;
                        }
                    }

                    _outline = targetObject.AddComponent<Outline>();
                    _outline.OutlineMode = outlineMode;
                    _outline.OutlineColor = outlineColor;
                    _outline.OutlineWidth = outlineWidth;
                    _outline.enabled = false;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"{LOG_PREFIX} Could not setup outline:  {e.Message}");
                _outline = null;
            }
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
            InitializeReturnIndicator();

            if (!IsServer)
            {
                SyncReturnStateFromNetworkValues();
            }

            UpdateReturnIndicatorVisual();

            if (isPrefabMode)
            {
                SetPrefabModeComponents(true);
            }
        }

        /// <summary>
        /// QA bulgusu #1: NGO'da ilk spawn payload'ındaki NetworkVariable değerleri client'ta
        /// OnNetworkSpawn() çağrılmadan ÖNCE deserialize edilir; SubscribeToNetworkEvents()
        /// (OnValueChanged aboneliği, OnNetworkSpawn içinde) bu ilk senkron olayını kaçırır —
        /// non-host client'larda local mirror alanları spawn anında default kalıp iade
        /// göstergesini yanlış/gizli çizebiliyordu. Truck.cs SyncFromNetworkValues ile aynı
        /// desen: server hariç, .Value'ları doğrudan okuyup local mirror'ları senkronlar.
        /// </summary>
        private void SyncReturnStateFromNetworkValues()
        {
            _interactionMode = (InteractionMode)_networkInteractionMode.Value;
            _requestedReturnBoxType = (BoxInfo.BoxType)_networkRequestedReturnBoxType.Value;
            _isDualItemMode = _networkIsDualItemMode.Value;
            _requestedReturnBoxType2 = (BoxInfo.BoxType)_networkRequestedReturnBoxType2.Value;
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
            // Sabirli Musteriler perki (patient_customers): CustomerManager.patienceMultiplier ile olceklenir (default 1f).
            float patienceMultiplier = manager != null ? manager.patienceMultiplier : 1f;
            _actualWaitTime = Random.Range(minWaitTime, maxWaitTime) * patienceMultiplier;

            // Faz B — 2-item (dual item) modu, gün 9+. ProductSupply/BoxRequest seçiminden
            // BAĞIMSIZ ayrı bir eksen — mod seçiminden ÖNCE belirlenir, aşağıdaki iki dalda da kullanılır.
            _isDualItemMode = manager != null && manager.ShouldAssignDualItemMode();
            _networkIsDualItemMode.Value = _isDualItemMode;

            // Faz A — iade (BoxRequest) modu, gün 5+ (%25 economist kararı). Bu modda müşteri
            // _assignedProductIndex KULLANMAZ, kendi ayrı alanını kullanır (bkz. Enums region).
            if (manager != null && manager.ShouldAssignBoxRequestMode())
            {
                _interactionMode = InteractionMode.BoxRequest;
                _requestedReturnBoxType = manager.PickRandomReturnBoxType();
                _networkInteractionMode.Value = (int)_interactionMode;
                _networkRequestedReturnBoxType.Value = (int)_requestedReturnBoxType;

                if (_isDualItemMode)
                {
                    // Faz B — ikinci renk isteği. Bağımsız çekim: aynı renk tekrar çıkabilir
                    // (economist "2 farklı renk OLABİLİR" dedi, zorunlu farklılık şartı yok).
                    _requestedReturnBoxType2 = manager.PickRandomReturnBoxType();
                    _networkRequestedReturnBoxType2.Value = (int)_requestedReturnBoxType2;
                }

                Debug.Log($"{LOG_PREFIX} Spawned in BoxRequest (iade) mode, requested color: {_requestedReturnBoxType}" +
                    (_isDualItemMode ? $" + {_requestedReturnBoxType2} (dual item, gün 9+)" : ""));
            }
            else if (manager != null && HasValidProductPrefabs())
            {
                _assignedProductIndex = manager.GetRandomProductIndexExcludingRecent(productPrefabs);
                _networkAssignedProductIndex.Value = _assignedProductIndex;

                if (_isDualItemMode)
                {
                    // Faz B — ikinci ürün. GetRandomProductIndexExcludingRecent kendi geçmişini
                    // günceller (AddToProductHistory) — art arda çağrı pratikte farklı ürüne
                    // meyilli ama garanti değil, sorun teşkil etmiyor (aynı ürün 2 kez de geçerli).
                    _assignedProductIndex2 = manager.GetRandomProductIndexExcludingRecent(productPrefabs);
                    _networkAssignedProductIndex2.Value = _assignedProductIndex2;
                }

                Debug.Log($"{LOG_PREFIX} Spawned with product index: {_assignedProductIndex}" +
                    (_isDualItemMode ? $" + {_assignedProductIndex2} (dual item, gün 9+)" : "") +
                    $" ({productPrefabs[_assignedProductIndex].name})");
            }

            // Host'ta (IsServer && IsClient) bu değerler client-side OnValueChanged handler'ları
            // ÜZERİNDEN değil doğrudan burada set edildiği için gösterge de burada tetiklenmeli.
            UpdateReturnIndicatorVisual();
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
            _networkInteractionMode.OnValueChanged += HandleInteractionModeChanged;
            _networkRequestedReturnBoxType.OnValueChanged += HandleRequestedReturnBoxTypeChanged;
            _networkIsDualItemMode.OnValueChanged += HandleIsDualItemModeChanged;
            _networkAssignedProductIndex2.OnValueChanged += HandleAssignedProductIndex2Changed;
            _networkRequestedReturnBoxType2.OnValueChanged += HandleRequestedReturnBoxType2Changed;
            _networkFirstReturnColorFulfilled.OnValueChanged += HandleFirstReturnColorFulfilledChanged;
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
            _networkInteractionMode.OnValueChanged -= HandleInteractionModeChanged;
            _networkRequestedReturnBoxType.OnValueChanged -= HandleRequestedReturnBoxTypeChanged;
            _networkIsDualItemMode.OnValueChanged -= HandleIsDualItemModeChanged;
            _networkAssignedProductIndex2.OnValueChanged -= HandleAssignedProductIndex2Changed;
            _networkRequestedReturnBoxType2.OnValueChanged -= HandleRequestedReturnBoxType2Changed;
            _networkFirstReturnColorFulfilled.OnValueChanged -= HandleFirstReturnColorFulfilledChanged;
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
            bool playerInRange = CheckIfLocalPlayerInRange();

            // Outline'ı aç/kapa
            if (_outline != null)
            {
                _outline.enabled = playerInRange && !_hasInteracted && _state == CustomerState.Service;
            }

            if (!playerInRange) return;

            if (InputBindingManager.GetActionDown(InputBindingManager.GameAction.Interact))
            {
                RequestInteractionServerRpc(NetworkManager.Singleton.LocalClientId);
            }
        }

        private bool CheckIfLocalPlayerInRange()
        {
            if (NetworkManager.Singleton == null) return false;

            var playerTransform = GetLocalPlayerTransform();

            if (playerTransform == null) return false;

            float distance = Vector3.Distance(transform.position, playerTransform.position);
            return distance <= interactionRange;
        }

        private Transform GetLocalPlayerTransform()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
            {
                 return NetworkManager.Singleton.LocalClient.PlayerObject.transform;
            }
            return null;
        }

        #endregion

        #region Return Indicator (İade Görsel Ayrımı — Kural 1)

        /// <summary>
        /// İade (BoxRequest) modundaki müşteriyi ProductSupply modundakinden görsel olarak ayırmak
        /// için başının üstüne 1-2 küçük renkli küre oluşturur. Sadece client tarafında (IsClient) —
        /// networked state (IsBoxRequestMode/RequestedReturnBoxType) zaten senkron, bu yalnızca o
        /// veriyi okuyup gösteriyor. Idempotent: birden fazla çağrılırsa (OnNetworkSpawn + Start
        /// ikisi de InitializeComponents'i tetikliyor) tekrar oluşturmaz.
        /// </summary>
        private void InitializeReturnIndicator()
        {
            if (!IsClient || _returnIndicatorRoot != null) return;

            _returnIndicatorRoot = new GameObject("ReturnIndicator");
            _returnIndicatorRoot.transform.SetParent(transform, false);
            _returnIndicatorRoot.transform.localPosition = new Vector3(0f, RETURN_INDICATOR_HEIGHT, 0f);

            _returnIndicatorSphere1 = CreateReturnIndicatorSphere(
                "ReturnIndicatorColor1", new Vector3(-RETURN_INDICATOR_OFFSET_X, 0f, 0f), out _returnIndicatorRenderer1);
            _returnIndicatorSphere2 = CreateReturnIndicatorSphere(
                "ReturnIndicatorColor2", new Vector3(RETURN_INDICATOR_OFFSET_X, 0f, 0f), out _returnIndicatorRenderer2);

            // İlk gerçek durum UpdateReturnIndicatorVisual() ile netleşene kadar gizli kalsın.
            _returnIndicatorRoot.SetActive(false);
        }

        /// <summary>
        /// Basit, procedural bir gösterge küresi oluşturur (yeni sanat asseti/sprite gerekmiyor —
        /// bkz. görev talimatı). Collider kaldırılır (fizik/etkileşimle karışmasın), gölge/probe
        /// kapatılır (performans — birçok müşteri aynı anda sahnede olabilir).
        /// </summary>
        private GameObject CreateReturnIndicatorSphere(string name, Vector3 localOffset, out Renderer renderer)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = name;
            sphere.transform.SetParent(_returnIndicatorRoot.transform, false);
            sphere.transform.localPosition = localOffset;
            sphere.transform.localScale = Vector3.one * RETURN_INDICATOR_SCALE;

            var collider = sphere.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            renderer = sphere.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

                // URP unlit tercih edilir (basit renkli rozet, ışıklandırma gereksiz); bulunamazsa
                // sırasıyla Lit, sonra her zaman var olan Sprites/Default'a düşer.
                var shader = Shader.Find("Universal Render Pipeline/Unlit")
                    ?? Shader.Find("Universal Render Pipeline/Lit")
                    ?? Shader.Find("Sprites/Default");

                if (shader != null)
                {
                    renderer.material = new Material(shader);
                }
            }

            return sphere;
        }

        /// <summary>
        /// Networked state'i (_interactionMode, _requestedReturnBoxType, _isDualItemMode,
        /// _requestedReturnBoxType2, _firstReturnColorFulfilled — hepsi zaten senkron) okuyup
        /// göstergeyi buna göre günceller. ProductSupply modunda gösterge tamamen gizli kalır
        /// (Kural 1'in ayrımı budur). Dual-item'de ilk renk teslim edilince o küre gizlenir,
        /// yalnız bekleyen ikinci renk kalır.
        /// </summary>
        private void UpdateReturnIndicatorVisual()
        {
            if (_returnIndicatorRoot == null) return;

            bool show = IsBoxRequestMode;
            if (_returnIndicatorRoot.activeSelf != show)
            {
                _returnIndicatorRoot.SetActive(show);
            }

            if (!show) return;

            bool showFirst = !(_isDualItemMode && _firstReturnColorFulfilled);
            if (_returnIndicatorSphere1 != null)
            {
                _returnIndicatorSphere1.SetActive(showFirst);
            }
            if (showFirst)
            {
                SetReturnIndicatorColor(_returnIndicatorRenderer1, GetReturnIndicatorColor(_requestedReturnBoxType));
            }

            if (_returnIndicatorSphere2 != null)
            {
                _returnIndicatorSphere2.SetActive(_isDualItemMode);
            }
            if (_isDualItemMode)
            {
                SetReturnIndicatorColor(_returnIndicatorRenderer2, GetReturnIndicatorColor(_requestedReturnBoxType2));
            }
        }

        private static void SetReturnIndicatorColor(Renderer renderer, Color color)
        {
            if (renderer == null || renderer.material == null) return;

            var mat = renderer.material; // CreateReturnIndicatorSphere zaten örnek (instance) material atadı
            if (mat.HasProperty(ReturnIndicatorBaseColorId)) mat.SetColor(ReturnIndicatorBaseColorId, color);
            if (mat.HasProperty(ReturnIndicatorColorId)) mat.SetColor(ReturnIndicatorColorId, color);
            mat.color = color;
        }

        private static Color GetReturnIndicatorColor(BoxInfo.BoxType boxType)
        {
            return boxType switch
            {
                BoxInfo.BoxType.Red => ReturnIndicatorRedColor,
                BoxInfo.BoxType.Yellow => ReturnIndicatorYellowColor,
                BoxInfo.BoxType.Blue => ReturnIndicatorBlueColor,
                _ => Color.white
            };
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
            // İstasyon ataması artık CustomerManager.Update() → AssignFreeServiceStations()
            // tarafından merkezi ve kuyruk-sıralı olarak yapılıyor (2 paralel istasyon deseni,
            // economy-rebuild FAZ4 §D#2). Bu metot yalnızca state dispatch'i için var; müşteri
            // AssignServiceStation() çağrılana kadar bu state'te bekler.
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

            // NEW: Customer lost - trigger game over
            if (IsServer && GameStateManager.Instance != null)
            {
                GameStateManager.Instance.OnCustomerLost();
            }

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



        #endregion

        #region Service & Interaction

        private void BeginService()
        {
            SetState(CustomerState.Service);
        }

        /// <summary>
        /// CustomerManager (server-only, AssignFreeServiceStations) tarafından bu müşteriye boş
        /// bir servis istasyonu bulunduğunda çağrılır: masayı atar ve servisi başlatır. 2 paralel
        /// istasyon deseninin giriş noktası — dropOffTable sadece server-side kullanıldığından
        /// (interaction akışı ServerRpc içinden yürür) client'a ayrıca senkron edilmesi gerekmez.
        /// </summary>
        public void AssignServiceStation(DisplayTable table)
        {
            if (!IsServer || table == null) return;

            dropOffTable = table;
            BeginService();
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

            // Outline'ı kapat
            if (_outline != null)
            {
                _outline.enabled = false;
            }

            if (playerId != ulong.MaxValue)
            {
                _interactingPlayerId = playerId;
            }

            _hasInteracted = true;
            _isInInteraction = true;

            // Network sync
            _networkIsInInteraction.Value = true;
            _networkWaitBarTime.Value = EffectiveInteractionTime;

            // UI
            if (waitBar != null)
            {
                waitBar.StartWaitBar(EffectiveInteractionTime);
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
            yield return new WaitForSeconds(EffectiveInteractionTime);
            CompleteInteraction();
        }

        // Sabirli Musteriler perki (patient_customers) FAZ4 tercih: CustomerManager.interactionTimeMultiplier
        // ile olceklenir (default 1f). bkz. plans/economy-rebuild-2026-07-30-faz4-final.md SS B.7.
        // Faz B — 2-item (dual item) modu, gün 9+: manager.interactionTimeMultiplier'ın YANINA ayrı
        // bir çarpı olarak eklenir (onu DEĞİŞTİRMEZ). BoxRequest-dual'de her ayrı teslimat (1. ve
        // 2. renk) kendi InteractionTimerCoroutine'ini ayrı ayrı çalıştırdığından bu çarpan HER
        // teslimat turuna uygulanır (ProductSupply-dual'de ise tek turluk teslimat — 2 ürün aynı
        // anda masaya konur, tek E-etkileşimi).
        //
        // economist düzeltmesi (2026-08-15, QA bulgusu üzerine): ProductSupply-dual ×1.3
        // (DUAL_ITEM_INTERACTION_TIME_MULTIPLIER) bir KOMPRESYON ödülünü modelliyor (2 ürünü tek
        // etkileşimde toplamak); BoxRequest-dual'da oyuncu tek seferde tek kutu taşıdığından bu
        // kompresyon yok — aynı ×1.3'ü İKİ bacağa da uygulamak saf çifte ceza olurdu. BoxRequest-dual
        // bunun yerine AYRI ve daha düşük DUAL_ITEM_BOXREQUEST_INTERACTION_TIME_MULTIPLIER (×1.15,
        // bacak başına) kullanır. bkz. .claude/agent-memory/economist/post_rent_features_pricing_2026-08-15.md
        // "DÜZELTME (2026-08-15, QA bulgusu üzerine)".
        private float EffectiveInteractionTime
        {
            get
            {
                float dualMultiplier = 1f;
                if (_isDualItemMode)
                {
                    dualMultiplier = _interactionMode == InteractionMode.BoxRequest
                        ? PostRentFeatureUnlocks.DUAL_ITEM_BOXREQUEST_INTERACTION_TIME_MULTIPLIER
                        : PostRentFeatureUnlocks.DUAL_ITEM_INTERACTION_TIME_MULTIPLIER;
                }

                return interactionTime * (manager != null ? manager.interactionTimeMultiplier : 1f) * dualMultiplier;
            }
        }

        private void CompleteInteraction()
        {
            if (_hasTimedOut) return;

            _isInInteraction = false;
            _networkIsInInteraction.Value = false;

            // Outline'ı kapat
            if (_outline != null)
            {
                _outline.enabled = false;
            }

            // UI gizle
            HideWaitUI();

            _waitTimeStarted = false;
            _networkWaitTimeStarted.Value = false;

            if (_interactionMode == InteractionMode.BoxRequest)
            {
                // Faz A — iade: ürün yerleştirme yok, oyuncunun elindeki kutu doğrudan kontrol edilir.
                ProcessReturnBoxInteraction();
            }
            else
            {
                // Ürün yerleştir (Coroutine ile)
                StartCoroutine(PlaceProductCoroutine());
            }
        }

        /// <summary>
        /// Faz A — iade (BoxRequest) etkileşiminin sonucu: etkileşimi başlatan oyuncu şu anda
        /// istenen renkte bir kutu taşıyorsa başarı (kutu elinden alınır, prestij ödülü — mevcut
        /// customerServedPrestigeBonus, HandleSuccessfulInteraction ile birebir aynı), aksi halde
        /// hata (mevcut wrongProductPrestigePenalty). Para YOK (economist kararı — tek para kaynağı tır).
        /// Ürün yerleştirme/pickup coroutine'i yok: teslimat anında tamamlanır.
        ///
        /// Faz B — 2-item (dual item) modu, gün 9+: PlayerInventory tek seferde tek eşya taşıyor
        /// (bkz. PlayerInventory.cs _currentItemID/_currentItemData tekil alanlar, HasItem/CurrentItemData
        /// property'leri tekil) — bu yüzden 2. renk AYRI bir E-etkileşimi (ayrı bir sefer) olarak
        /// gerçekleşir. İlk renk doğru teslim edilince interaction BİTMEZ: müşteri Service state'inde
        /// kalır, oyuncu serbest bırakılır ve ikinci E-etkileşimine yeniden izin verilir
        /// (RearmForSecondReturnInteraction). Yalnız İKİ renk de tamamlanınca HandleSuccessfulInteraction
        /// çağrılır — prestij ödülü müşteri başına BİR KEZ verilir (economist: ekstra ödül yok).
        /// Yanlış renk (1. veya 2. turda) her zaman anında hata + çıkış — kısmi başarı yok.
        /// </summary>
        private void ProcessReturnBoxInteraction()
        {
            BoxInfo.BoxType targetColor = (_isDualItemMode && _firstReturnColorFulfilled)
                ? _requestedReturnBoxType2
                : _requestedReturnBoxType;

            bool success = TryConsumeMatchingReturnBox(targetColor);

            if (success && _isDualItemMode && !_firstReturnColorFulfilled)
            {
                // İlk renk teslim edildi, ikinci renk bekleniyor — interaction'ı bitirme.
                _firstReturnColorFulfilled = true;
                _networkFirstReturnColorFulfilled.Value = true;
                // Host'ta OnValueChanged client-only handler'dan geçmediği için burada da tetikle.
                UpdateReturnIndicatorVisual();

                UnlockInteractingPlayer();
                RearmForSecondReturnInteraction();
                return;
            }

            if (success)
            {
                HandleSuccessfulInteraction();
            }
            else
            {
                HandleFailedInteraction();
            }

            UnlockInteractingPlayer();
            TransitionToExit();
        }

        /// <summary>
        /// Faz B — dual-item BoxRequest'te ilk renk teslim edildikten sonra müşteriyi ikinci
        /// E-etkileşimine hazır hale getirir (server-only). Customer Service state'inde kalır.
        /// QA bulgusu #2 düzeltmesi: CompleteInteraction() _waitTimeStarted'ı false yapıp sabır
        /// sayacını durdurduğu için burada YENİDEN başlatılmazsa müşteri 2. renk için sonsuza
        /// dek sabırsızlanmadan bekler (StartWaitTime() ile aynı deseni tekrar kullanıyoruz —
        /// _hasInteracted zaten false yapıp _waitTimeStarted'ı tekrar true'ya çeker, waitBar'ı
        /// _actualWaitTime ile yeniden başlatır, network mirror'ları senkron eder).
        /// </summary>
        private void RearmForSecondReturnInteraction()
        {
            StartWaitTime();
        }

        /// <summary>
        /// Etkileşimi başlatan oyuncunun elindeki item'ın rengi <paramref name="targetColor"/> ile
        /// eşleşiyor mu kontrol eder; eşleşiyorsa oyuncunun elinden kutuyu alır
        /// (SetInventoryStateServer) ve true döner.
        /// </summary>
        private bool TryConsumeMatchingReturnBox(BoxInfo.BoxType targetColor)
        {
            var playerInventory = GetInteractingPlayerInventory();
            if (playerInventory == null || !playerInventory.HasItem)
            {
                return false;
            }

            var itemData = playerInventory.CurrentItemData;
            if (itemData == null || itemData.visualPrefab == null)
            {
                return false;
            }

            var boxInfo = itemData.visualPrefab.GetComponent<BoxInfo>();
            if (boxInfo == null || boxInfo.boxType != targetColor)
            {
                return false;
            }

            playerInventory.SetInventoryStateServer(false, -1);
            return true;
        }

        /// <summary>
        /// _interactingPlayerId'ye karşılık gelen PlayerInventory'yi bulur (ValidatePlayerInRange'in
        /// kullandığı NetworkManager.ConnectedClients deseniyle aynı, bkz. Table.cs/ShelfState.cs).
        /// </summary>
        private PlayerInventory GetInteractingPlayerInventory()
        {
            if (_interactingPlayerId == ulong.MaxValue) return null;
            if (NetworkManager.Singleton == null) return null;

            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(_interactingPlayerId, out var client))
            {
                return null;
            }

            if (client.PlayerObject == null) return null;

            var inventory = client.PlayerObject.GetComponent<PlayerInventory>();
            if (inventory == null)
            {
                inventory = client.PlayerObject.GetComponentInChildren<PlayerInventory>();
            }

            return inventory;
        }

        private IEnumerator PlaceProductCoroutine()
        {
            // Ürünü yerleştir
            _placedProduct = PlaceProductOnDropOffTable(_assignedProductIndex, out _placedProductSlotIndex);

            // Faz B — 2-item (dual item) modu, gün 9+: ikinci ürünü de aynı masaya (DisplayTable
            // zaten çoklu slot destekliyor, masa tarafında değişiklik gerekmedi). Tek E-etkileşimi
            // ile İKİ ürün de bırakılır (BoxRequest'in ayrı-sefer kısıtından farklı — burada
            // oyuncunun elinde bir şey taşıması gerekmiyor, müşteri ürünü bırakıyor).
            if (_isDualItemMode && _placedProduct != null)
            {
                _placedProduct2 = PlaceProductOnDropOffTable(_assignedProductIndex2, out _placedProductSlotIndex2);
                if (_placedProduct2 == null)
                {
                    Debug.LogWarning($"{LOG_PREFIX} Dual-item 2. ürün yerleştirilemedi (masa dolu olabilir) — sadece 1. ürünle devam ediliyor.");
                }
            }

            // Kısa bir gecikme - physics'in settle olması için
            yield return new WaitForSeconds(PRODUCT_PLACEMENT_DELAY);

            if (_placedProduct != null)
            {
                // Physics'i tekrar kontrol et ve sabitle
                EnsureProductIsStable(_placedProduct);
                if (_placedProduct2 != null)
                {
                    EnsureProductIsStable(_placedProduct2);
                }

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
                float bonus = economySettings != null ? economySettings.customerServedPrestigeBonus : 0.2f;
                PrestigeManager.Instance.ModifyPrestige(bonus);
            }

            // KALDIRILDI: Customer served quest tracking artık yok
            // Quest.QuestTracker.NotifyCustomerServed();
        }

        private void HandleFailedInteraction()
        {
            if (PrestigeManager.Instance != null)
            {
                float penalty = economySettings != null ? economySettings.wrongProductPrestigePenalty : -0.04f;
                // SURPRISE AUDIT günü tüm cezalar 2× (yoksa 1×).
                float penaltyMult = EventEffectManager.Instance != null
                    ? EventEffectManager.Instance.GetPenaltyMultiplier() : 1f;
                PrestigeManager.Instance.ModifyPrestige(penalty * penaltyMult);
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

        /// <summary>
        /// Belirtilen ürünü drop-off masasına yerleştirir. <paramref name="productIndex"/> geçersizse
        /// (örn. -1, atanmamış) rastgele bir ürüne düşer. Faz B — 2-item modunda hem 1. hem 2. ürün
        /// için (farklı productIndex/slotIndex ile) İKİ KEZ çağrılır; DisplayTable çoklu slot
        /// desteklediğinden masa tarafında değişiklik gerekmiyor.
        /// </summary>
        private GameObject PlaceProductOnDropOffTable(int productIndex, out int placedSlotIndex)
        {
            placedSlotIndex = -1;

            if (dropOffTable == null || !HasValidProductPrefabs())
            {
                Debug.LogWarning($"{LOG_PREFIX} Cannot place product: dropOffTable or productPrefabs is null");
                return null;
            }

            // Masa dolu mu kontrol et
            if (dropOffTable.IsFull)
            {
                Debug.LogWarning($"{LOG_PREFIX} Drop-off table is full! Cannot place product.");
                return null;
            }

            if (productIndex < 0 || productIndex >= productPrefabs.Length)
            {
                productIndex = Random.Range(0, productPrefabs.Length);
            }

            // ✅ 1. Slot index'ini DisplayTable.ItemCount üzerinden al
            //    PlaceItemInstance() aynı index'i kullanacak çünkü _placedItems.Count == ItemCount
            int slotIndex = dropOffTable.ItemCount;
            placedSlotIndex = slotIndex; // Cache for later checking

            // ✅ 2. Ürünü doğrudan hedef slot pozisyon/rotasyonunda oluştur.
            //    Böylece NetworkTransform (Interpolate:1) client'ta origin->slot "kayması"
            //    üretmez; spawn zaten doğru yerde olur (PlaceItemInstance idempotent olarak
            //    aynı pozisyonu tekrar set eder, delta ~0).
            var slotPoints = dropOffTable.SlotPoints;
            Transform targetSlot = (slotPoints != null && slotIndex >= 0 && slotIndex < slotPoints.Length)
                ? slotPoints[slotIndex]
                : null;

            var product = targetSlot != null
                ? Instantiate(productPrefabs[productIndex], targetSlot.position, targetSlot.rotation)
                : Instantiate(productPrefabs[productIndex]);

            // ✅ 3. Fizik ayarlarını yap
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

            // ✅ 4. NetworkObject spawn et
            var networkObject = product.GetComponent<NetworkObject>();
            if (networkObject != null)
            {
                networkObject.Spawn();
            }

            // ✅ 5. DisplayTable'a kaydet — bu hem pozisyonu doğru slot'a ayarlar hem ItemCount'u artırır
            //    PlaceItemInstance, _placedItems.Count'u kullanarak slotPoints[slotIndex]'e yerleştirir.
            //    Animasyonu burada oynatma (playAnimation:false) — server-only çalışır ve client'lara
            //    hiç replike edilmezdi. Animasyon ClientRpc ile tüm client'larda (host dahil) tetiklenir.
            dropOffTable.PlaceItemInstance(product, playAnimation: false);

            // ✅ 6. Spawn sonrası pozisyonu sabitle (network replication sonrası kayma önlemi)
            StartCoroutine(EnsureProductFrozenAfterSpawn(product));

            // ✅ 7. Yerleştirme animasyonunu (küçükten büyüğe scale-up) tüm client'larda tetikle
            if (networkObject != null)
            {
                PlayPlacementAnimationClientRpc(networkObject.NetworkObjectId);
            }

            Debug.Log($"{LOG_PREFIX} Placed product: {productPrefabs[productIndex].name} at slot {slotIndex}, position {product.transform.position}");

            return product;
        }

        /// <summary>
        /// Tüm client'larda ürün yerleştirme animasyonunu (küçükten büyüğe scale) tetikler.
        /// Race condition koruması: obje henüz client'ta spawn olmamışsa animasyon sessizce atlanır.
        /// </summary>
        [ClientRpc]
        private void PlayPlacementAnimationClientRpc(ulong networkObjectId)
        {
            if (NetworkManager.Singleton?.SpawnManager == null) return;

            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(
                    networkObjectId, out NetworkObject netObj))
            {
                Debug.Log($"{LOG_PREFIX} ⚠️ PlayPlacementAnimation: NetworkObject {networkObjectId} not found on this client (race condition - safe to ignore)");
                return;
            }

            if (netObj == null || netObj.gameObject == null) return;

            ItemPlacementAnimator.PlayOn(this, netObj.gameObject);
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

            // NavMeshAgent'ı durdur — müşteri yerinde kalmalı
            if (_navAgent != null)
            {
                _navAgent.ResetPath();
                _navAgent.isStopped = true;
            }

            // NetworkObject referansını cache'le
            if (_placedProduct != null)
            {
                _placedProductNetworkObject = _placedProduct.GetComponent<NetworkObject>();
            }
            if (_placedProduct2 != null)
            {
                _placedProductNetworkObject2 = _placedProduct2.GetComponent<NetworkObject>();
            }

            yield return null;

            // Müşteri, İKİ ürün de alınana kadar süresiz bekleyecek (dual item modunda değilse
            // _placedProduct2 zaten hep null'dur, döngü tek ürünle Faz A davranışına eşdeğerdir).
            while (_placedProduct != null || _placedProduct2 != null)
            {
                if (_placedProduct != null && !IsItemStillOnTable(_placedProduct))
                {
                    Debug.Log($"{LOG_PREFIX} Product 1 was picked up (table check)");
                    _placedProduct = null;
                }
                else if (_placedProduct != null && _placedProductNetworkObject != null && !_placedProductNetworkObject.IsSpawned)
                {
                    Debug.Log($"{LOG_PREFIX} Product 1 NetworkObject was despawned");
                    _placedProduct = null;
                }

                if (_placedProduct2 != null && !IsItemStillOnTable(_placedProduct2))
                {
                    Debug.Log($"{LOG_PREFIX} Product 2 was picked up (table check)");
                    _placedProduct2 = null;
                }
                else if (_placedProduct2 != null && _placedProductNetworkObject2 != null && !_placedProductNetworkObject2.IsSpawned)
                {
                    Debug.Log($"{LOG_PREFIX} Product 2 NetworkObject was despawned");
                    _placedProduct2 = null;
                }

                if (_placedProduct == null && _placedProduct2 == null)
                {
                    break;
                }

                yield return new WaitForSeconds(PICKUP_CHECK_INTERVAL);
            }

            TransitionToExit();
        }

        /// <summary>
        /// Verilen ürün nesnesi hâlâ dropOffTable'ın kendi item listesinde mi? (Pozisyon
        /// karşılaştırması yerine doğrudan referans kontrolü — Faz B'de 1. ve 2. ürün için
        /// paylaşımlı kullanılır.)
        /// </summary>
        private bool IsItemStillOnTable(GameObject item)
        {
            if (item == null || dropOffTable == null)
            {
                return false;
            }

            var allItems = dropOffTable.GetAllItems();
            foreach (var tableItem in allItems)
            {
                if (tableItem == item)
                {
                    return true;
                }
            }

            return false;
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

            // NavMeshAgent'ı aktifleştir (WaitingForPickup'ta durdurulmuş olabilir)
            if (_navAgent != null)
            {
                _navAgent.isStopped = false;
            }

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

            if (_returnIndicatorRoot != null)
            {
                if (enabled)
                {
                    _returnIndicatorRoot.SetActive(false);
                }
                else
                {
                    UpdateReturnIndicatorVisual();
                }
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

        private void HandleInteractionModeChanged(int previousValue, int newValue)
        {
            if (IsServer) return;
            _interactionMode = (InteractionMode)newValue;
            UpdateReturnIndicatorVisual();
        }

        private void HandleRequestedReturnBoxTypeChanged(int previousValue, int newValue)
        {
            if (IsServer) return;
            _requestedReturnBoxType = (BoxInfo.BoxType)newValue;
            UpdateReturnIndicatorVisual();
        }

        private void HandleIsDualItemModeChanged(bool previousValue, bool newValue)
        {
            if (IsServer) return;
            _isDualItemMode = newValue;
            UpdateReturnIndicatorVisual();
        }

        private void HandleAssignedProductIndex2Changed(int previousValue, int newValue)
        {
            if (IsServer) return;
            _assignedProductIndex2 = newValue;
        }

        private void HandleRequestedReturnBoxType2Changed(int previousValue, int newValue)
        {
            if (IsServer) return;
            _requestedReturnBoxType2 = (BoxInfo.BoxType)newValue;
            UpdateReturnIndicatorVisual();
        }

        private void HandleFirstReturnColorFulfilledChanged(bool previousValue, bool newValue)
        {
            if (IsServer) return;
            _firstReturnColorFulfilled = newValue;
            UpdateReturnIndicatorVisual();
        }

        #endregion

        #region Debug & Editor

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Interaction range
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactionRange);

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
            var info = $"State: {_state}\nInteracted: {_hasInteracted}\nMode: {_interactionMode}";

            if (_isDualItemMode)
            {
                info += $"\nDualItem: true (1st done: {_firstReturnColorFulfilled})";
            }

            if (IsServer)
            {
                info += $"\nWait Time: {_actualWaitTime:F1}s";

                if (_interactionMode == InteractionMode.BoxRequest)
                {
                    info += $"\nRequested: {_requestedReturnBoxType}" +
                        (_isDualItemMode ? $" + {_requestedReturnBoxType2}" : "");
                }
                else if (_assignedProductIndex >= 0 && HasValidProductPrefabs() && _assignedProductIndex < productPrefabs.Length)
                {
                    info += $"\nProduct: {productPrefabs[_assignedProductIndex].name} ({_assignedProductIndex})";
                    if (_isDualItemMode && _assignedProductIndex2 >= 0 && _assignedProductIndex2 < productPrefabs.Length)
                    {
                        info += $" + {productPrefabs[_assignedProductIndex2].name} ({_assignedProductIndex2})";
                    }
                }

                if (_placedProduct != null)
                {
                    info += $"\nPlaced:  {_placedProduct.name}";
                    info += $"\nParent: {(_placedProduct.transform.parent != null ? _placedProduct.transform.parent.name : "null")}";
                }
                if (_placedProduct2 != null)
                {
                    info += $"\nPlaced2: {_placedProduct2.name}";
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