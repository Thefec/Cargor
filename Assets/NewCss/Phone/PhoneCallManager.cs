using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace NewCss
{
    /// <summary>
    /// Telefon çağrı sistemi - rastgele telefon çağrıları, cevaplama mekaniği ve prestige ödül/ceza sistemini yönetir. 
    /// Server-authoritative tasarım ile multiplayer senkronizasyonu sağlar.
    /// </summary>
    public class PhoneCallManager : NetworkBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[PhoneCall]";
        private const string PLAYER_TAG = "Character";
        private const int HOURS_IN_DAY = 24;
        private const float MIN_CALL_DURATION = 5f;
        private const float MIN_ANSWER_DURATION = 1f;

        // UI Text
        private const string TEXT_IN_CONVERSATION = "Telefonda konuşuyor...";
        private const string TEXT_OTHER_IN_CONVERSATION = "Başka bir oyuncu telefonda konuşuyor...";
        private const string TEXT_CALL_NEARBY = "Telefon çalıyor! E tuşuna basarak cevapla!";
        private const string TEXT_CALL_FAR = "Telefon çalıyor!   Telefona yaklaşıp E tuşuna bas!";

        #endregion

        #region Singleton

        public static PhoneCallManager Instance { get; private set; }

        #endregion

        #region Serialized Fields - Call Settings

        [Header("=== PHONE CALL SETTINGS ===")]
        [SerializeField, Range(0f, 1f), Tooltip("Saatlik çağrı şansı")]
        private float callChance = 0.5f;

        [SerializeField, Tooltip("Çağrı cevaplama süresi (saniye)")]
        private float callDuration = 30f;

        [SerializeField, Tooltip("Konuşma süresi (saniye)")]
        private float callAnswerDuration = 10f;

        [SerializeField, Tooltip("Başarılı çağrı prestige ödülü")]
       private float prestigeReward = 0.02f;

        [SerializeField, Tooltip("Kaçırılan çağrı prestige cezası")]
        private float prestigePenalty = 0.05f;

        [SerializeField, Tooltip("Çağrıların başlayacağı saat")]
        private int startCallingHour = 8;

        #endregion

        #region Serialized Fields - UI Elements

        [Header("=== UI ELEMENTS ===")]
        [SerializeField, Tooltip("Telefon UI paneli")]
        private GameObject phoneCallUI;

        [SerializeField, Tooltip("Cevaplama butonu")]
        private Button answerButton;

        [SerializeField, Tooltip("Çağrı bilgi text'i")]
        private TextMeshProUGUI callInfoText;

        #endregion

        #region Serialized Fields - Audio

        [Header("=== AUDIO ===")]
        [SerializeField, Tooltip("Telefon zil sesi")]
        private AudioSource phoneRingSound;

        [SerializeField, Tooltip("Konuşma sesi")]
        private AudioSource conversationSound;

        #endregion

        #region Serialized Fields - Wait Bar

        [Header("=== WAIT BAR SYSTEM ===")]
        [SerializeField, Tooltip("Telefon bekleme çubuğu")]
        private PhoneWaitBar phoneWaitBar;

        [SerializeField, Tooltip("Telefon canvas'ı")]
        private Canvas phoneCanvas;

        [SerializeField, Tooltip("Çağrı gelene kadar canvas'ı gizle")]
        private bool hideCanvasUntilCall = true;

        #endregion

        #region Serialized Fields - Modifiers

        [Header("=== EVENT MODIFIERS ===")]
        [SerializeField, Tooltip("Customer support event çarpanı")]
        private float customerSupportModifier = 1.3f;

        #endregion

        #region Serialized Fields - Interaction

        [Header("=== INTERACTION SETTINGS ===")]
        [SerializeField, Tooltip("Telefon collider'ı")]
        private Collider phoneCollider;

        [SerializeField, Tooltip("Oyuncu tag'i")]
        private string playerTag = PLAYER_TAG;

        #endregion

        #region Network Variables

        private readonly NetworkVariable<bool> _networkIsCallActive = new(false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> _networkIsCallAnswered = new(false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> _networkIsInPhoneConversation = new(false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> _networkCustomerSupportActive = new(false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<ulong> _networkCurrentCallOwner = new(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        #endregion

        #region Private Fields

        private bool[] _hourlyCallChecked;
        private int _lastCheckedHour = -1;
        private Coroutine _callCoroutine;
        private bool _playerInPhoneArea;
        private bool _isNetworkReady;

        #endregion

        #region Public Properties

        /// <summary>
        /// Oyuncu telefon alanında mı?
        /// </summary>
        public bool IsPlayerInPhoneArea => _playerInPhoneArea;

        /// <summary>
        /// Çağrı aktif mi?
        /// </summary>
        public bool IsCallActive => _isNetworkReady && _networkIsCallActive.Value;

        /// <summary>
        /// Çağrı cevaplanmış mı?
        /// </summary>
        public bool IsCallAnswered => _isNetworkReady && _networkIsCallAnswered.Value;

        /// <summary>
        /// Telefon konuşması devam ediyor mu?
        /// </summary>
        public bool IsInPhoneConversation => _isNetworkReady && _networkIsInPhoneConversation.Value;

        /// <summary>
        /// Mevcut çağrıyı cevaplayan oyuncu ID'si
        /// </summary>
        public ulong CurrentCallOwner => _isNetworkReady ? _networkCurrentCallOwner.Value : 0;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Empty - initialization happens in OnNetworkSpawn
        }

        private void Start()
        {
            TryAutoSpawnNetworkObject();
            SubscribeToDayCycleEvents();
        }

        private void Update()
        {
            if (!_isNetworkReady) return;

            if (IsServer)
            {
                ServerUpdate();
            }

            ClientUpdate();
        }

        private void OnDestroy()
        {
            UnsubscribeFromDayCycleEvents();
            CleanupSingleton();
        }

        #endregion

        #region Network Lifecycle

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (!InitializeSingleton())
            {
                return;
            }

            _isNetworkReady = true;

            SubscribeToNetworkEvents();
            InitializeSystem();
            SetupPhoneCollider();
            UpdateUIState();
        }

        public override void OnNetworkDespawn()
        {
            _isNetworkReady = false;

            UnsubscribeFromNetworkEvents();
            CleanupSingleton();

            base.OnNetworkDespawn();
        }

        #endregion

        #region Singleton Management

        private bool InitializeSingleton()
        {
            if (Instance == null)
            {
                Instance = this;
                return true;
            }

            if (Instance != this)
            {
                if (IsServer)
                {
                    GetComponent<NetworkObject>().Despawn();
                }
                return false;
            }

            return true;
        }

        private void CleanupSingleton()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        #endregion

        #region Initialization

        private void TryAutoSpawnNetworkObject()
        {
            var netObj = GetComponent<NetworkObject>();
            if (netObj != null && IsServer && !netObj.IsSpawned)
            {
                netObj.Spawn(true);
            }
        }

        private void InitializeSystem()
        {
            _hourlyCallChecked = new bool[HOURS_IN_DAY];

            InitializeUI();
            InitializeWaitBar();
        }

        private void InitializeUI()
        {
            SetPhoneUIActive(false);
            SetPhoneCanvasActive(false);

            if (answerButton != null)
            {
                answerButton.onClick.AddListener(AnswerCall);
            }
        }

        private void InitializeWaitBar()
        {
            if (phoneWaitBar == null)
            {
                phoneWaitBar = GetComponentInChildren<PhoneWaitBar>();
            }

            if (phoneCanvas == null)
            {
                phoneCanvas = GetComponentInChildren<Canvas>();
            }
        }

        private void SetupPhoneCollider()
        {
            if (phoneCollider == null)
            {
                phoneCollider = GetComponent<Collider>() ?? GetComponentInChildren<Collider>();
            }

            if (phoneCollider != null)
            {
                phoneCollider.isTrigger = true;
            }
        }

        #endregion

        #region Event Subscriptions

        private void SubscribeToNetworkEvents()
        {
            _networkIsCallActive.OnValueChanged += HandleCallActiveChanged;
            _networkIsCallAnswered.OnValueChanged += HandleCallAnsweredChanged;
            _networkIsInPhoneConversation.OnValueChanged += HandleConversationChanged;
            _networkCustomerSupportActive.OnValueChanged += HandleCustomerSupportChanged;
            _networkCurrentCallOwner.OnValueChanged += HandleCallOwnerChanged;
        }

        private void UnsubscribeFromNetworkEvents()
        {
            _networkIsCallActive.OnValueChanged -= HandleCallActiveChanged;
            _networkIsCallAnswered.OnValueChanged -= HandleCallAnsweredChanged;
            _networkIsInPhoneConversation.OnValueChanged -= HandleConversationChanged;
            _networkCustomerSupportActive.OnValueChanged -= HandleCustomerSupportChanged;
            _networkCurrentCallOwner.OnValueChanged -= HandleCallOwnerChanged;
        }

        private void SubscribeToDayCycleEvents()
        {
            DayCycleManager.OnNewDay -= HandleNewDay;
            DayCycleManager.OnNewDay += HandleNewDay;
        }

        private void UnsubscribeFromDayCycleEvents()
        {
            DayCycleManager.OnNewDay -= HandleNewDay;
        }

        #endregion

        #region Network Event Handlers

        private void HandleCallActiveChanged(bool previousValue, bool newValue)
        {
            UpdateUIState();

            if (newValue)
            {
                PlayRingSound();
            }
            else
            {
                StopRingSound();
            }
        }

        private void HandleCallAnsweredChanged(bool previousValue, bool newValue)
        {
            UpdateUIState();

            if (newValue)
            {
                StopRingSound();
                PlayConversationSound();
            }
        }

        private void HandleConversationChanged(bool previousValue, bool newValue)
{
    UpdateUIState();

    if (! newValue)
    {
        StopConversationSound();
    }

    // ✅ DEĞİŞTİRİLDİ: Movement lock işlemini KALDIR - ClientRpc'de yapılacak
    // HandleMovementLockForCallOwner(newValue); // KALDIRILDI
}

        private void HandleCustomerSupportChanged(bool previousValue, bool newValue)
        {
            LogDebug($"Customer support changed: {newValue}");
        }

        private void HandleCallOwnerChanged(ulong previousValue, ulong newValue)
        {
            UpdateUIState();

            // ✅ YENİ: Call owner değiştiğinde movement lock
            // Bu metod NetworkVariable değiştiğinde çağrılır
            // Yeni owner varsa ve konuşma aktifse, lock yap
            if (newValue != 0 && _networkIsInPhoneConversation.Value)
            {
                HandleMovementLockForCallOwner(true);
            }
        }

        #endregion

        #region Player Movement Lock

        private void HandleMovementLockForCallOwner(bool shouldLock)
        {
            if (NetworkManager.Singleton == null) return;

            ulong localClientId = NetworkManager.Singleton.LocalClientId;
            ulong callOwner = _networkCurrentCallOwner.Value;

            LogDebug($"HandleMovementLock - LocalClient: {localClientId}, CallOwner: {callOwner}, ShouldLock: {shouldLock}");

            // ✅ DEĞİŞTİRİLDİ: Sadece telefonu cevaplayan oyuncu için lock/unlock
            if (callOwner != localClientId)
            {
                LogDebug($"Client {localClientId} is NOT the call owner ({callOwner}), skipping lock");
                return;
            }

            LogDebug($"Client {localClientId} IS the call owner, applying lock: {shouldLock}");
            LockLocalPlayerMovement(shouldLock);
        }

        private void LockLocalPlayerMovement(bool locked)
        {
            if (NetworkManager.Singleton == null) return;

            ulong localClientId = NetworkManager.Singleton.LocalClientId;

            LogDebug($"LockLocalPlayerMovement - Client {localClientId}, Locked: {locked}");

            // ✅ DEĞİŞTİRİLDİ: ConnectedClients kullan, FindObjectsOfType değil
            if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(localClientId, out var client))
            {
                LogWarning($"Client {localClientId} not found in ConnectedClients!");
                return;
            }

            if (client.PlayerObject == null)
            {
                LogWarning($"PlayerObject is null for client {localClientId}!");
                return;
            }

            var playerMovement = client.PlayerObject.GetComponent<PlayerMovement>();

            if (playerMovement == null)
            {
                playerMovement = client.PlayerObject.GetComponentInChildren<PlayerMovement>();
            }

            if (playerMovement != null)
            {
                playerMovement.LockMovement(locked);
                playerMovement.LockAllInteractions(locked);
                LogDebug($"✅ Client {localClientId} - Movement locked: {locked}");
            }
            else
            {
                LogError($"❌ Client {localClientId} - PlayerMovement component not found!");
            }
        }

        private PlayerMovement FindPlayerMovement(NetworkObject netObj)
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

        #endregion

        #region UI Management

        private void UpdateUIState()
        {
            if (!_isNetworkReady) return;

            bool isCallActive = _networkIsCallActive.Value;
            bool isInConversation = _networkIsInPhoneConversation.Value;
            bool showUI = isCallActive || isInConversation;

            // Canvas visibility
            if (hideCanvasUntilCall)
            {
                SetPhoneCanvasActive(showUI);
            }

            // UI panel visibility
            SetPhoneUIActive(showUI);

            // Update info text
            UpdateCallInfoText();
        }

        private void UpdateCallInfoText()
        {
            if (callInfoText == null) return;

            bool isInConversation = _networkIsInPhoneConversation.Value;
            bool isCallActive = _networkIsCallActive.Value;
            bool isCallOwner = IsLocalClientCallOwner();

            if (isInConversation)
            {
                callInfoText.text = isCallOwner ? TEXT_IN_CONVERSATION : TEXT_OTHER_IN_CONVERSATION;
            }
            else if (isCallActive)
            {
                callInfoText.text = _playerInPhoneArea ? TEXT_CALL_NEARBY : TEXT_CALL_FAR;
            }
        }

        private bool IsLocalClientCallOwner()
        {
            return NetworkManager.Singleton != null &&
                   _networkCurrentCallOwner.Value == NetworkManager.Singleton.LocalClientId;
        }

        private void SetPhoneUIActive(bool active)
        {
            if (phoneCallUI != null)
            {
                phoneCallUI.SetActive(active);
            }
        }

        private void SetPhoneCanvasActive(bool active)
        {
            if (phoneCanvas != null)
            {
                phoneCanvas.gameObject.SetActive(active);
            }
        }

        #endregion

        #region Trigger Detection

        private void OnTriggerEnter(Collider other)
        {
            if (!IsLocalPlayer(other)) return;

            _playerInPhoneArea = true;
            UpdateUIState();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsLocalPlayer(other)) return;

            _playerInPhoneArea = false;
            UpdateUIState();
        }

        private bool IsLocalPlayer(Collider other)
        {
            if (!other.gameObject.CompareTag(playerTag))
            {
                return false;
            }

            var networkObject = other.GetComponent<NetworkObject>();
            return networkObject != null && networkObject.IsOwner;
        }

        #endregion

        #region Server Update

        private void ServerUpdate()
        {
            if (!_networkIsCallActive.Value && !_networkIsInPhoneConversation.Value)
            {
                CheckForPhoneCall();
            }
            else if (_networkIsCallActive.Value && !_networkIsCallAnswered.Value)
            {
                CheckCallTimeout();
            }
            else if (_networkIsInPhoneConversation.Value)
            {
                CheckConversationTimeout();
            }
        }

        private void CheckForPhoneCall()
        {
            if (DayCycleManager.Instance == null) return;

            int currentHour = DayCycleManager.Instance.CurrentHour;

            if (!IsValidHour(currentHour)) return;
            if (currentHour < startCallingHour) return;
            if (currentHour == _lastCheckedHour) return;
            if (_hourlyCallChecked[currentHour]) return;

            _lastCheckedHour = currentHour;
            _hourlyCallChecked[currentHour] = true;

            TryStartRandomCall(currentHour);
        }

        private bool IsValidHour(int hour)
        {
            return hour >= 0 && hour < HOURS_IN_DAY;
        }

        private void TryStartRandomCall(int currentHour)
        {
            float finalCallChance = CalculateCallChance();
            float randomValue = Random.Range(0f, 1f);

            LogDebug($"Phone Call Check - Hour: {currentHour}, Chance: {finalCallChance:F2}, Random: {randomValue:F2}");

            if (randomValue <= finalCallChance)
            {
                LogDebug($"Starting phone call at hour {currentHour}!");
                StartPhoneCallServer();
            }
        }

        private float CalculateCallChance()
        {
            float finalChance = callChance;

            if (_networkCustomerSupportActive.Value)
            {
                finalChance *= customerSupportModifier;
                finalChance = Mathf.Min(finalChance, 1f);
            }

            return finalChance;
        }

        private void CheckCallTimeout()
        {
            if (phoneWaitBar != null && phoneWaitBar.GetRemainingTime() <= 0 && !_networkIsCallAnswered.Value)
            {
                MissedCallServer();
            }
        }

        private void CheckConversationTimeout()
        {
            if (phoneWaitBar != null && phoneWaitBar.GetRemainingTime() <= 0)
            {
                EndPhoneConversationServer();
            }
        }

        #endregion

        #region Client Update

        private void ClientUpdate()
        {
            if (!_networkIsCallActive.Value || _networkIsCallAnswered.Value)
            {
                return;
            }

            if (_playerInPhoneArea && Input.GetKeyDown(KeyCode.E))
            {
                AnswerCall();
            }
        }

        #endregion

        #region Call Management - Server

        private void StartPhoneCallServer()
        {
            if (!IsServer || !_isNetworkReady) return;
            if (_networkIsCallActive.Value || _networkIsInPhoneConversation.Value) return;

            LogDebug("Phone call started on server");

            _networkIsCallActive.Value = true;
            _networkIsCallAnswered.Value = false;
            _networkIsInPhoneConversation.Value = false;
            _networkCurrentCallOwner.Value = 0;

            phoneWaitBar?.StartWaitBar(callDuration);

            OnCallStartedClientRpc();
        }

        private void EndPhoneConversationServer()
        {
            if (!_networkIsInPhoneConversation.Value) return;

            // Give prestige reward
            if (PrestigeManager.Instance != null && _networkCurrentCallOwner.Value != 0)
            {
                GivePrestigeToPlayerClientRpc(_networkCurrentCallOwner.Value, prestigeReward);
            }

            EndCallServer(wasMissed: false);
        }

        private void MissedCallServer()
        {
            if (_networkIsCallAnswered.Value) return;

            ApplyMissedCallPenaltyClientRpc();
            EndCallServer(wasMissed: true);
        }

        private void EndCallServer(bool wasMissed)
        {
            LogDebug($"EndCallServer - WasMissed: {wasMissed}, CurrentOwner: {_networkCurrentCallOwner.Value}");

            // ✅ YENİ: Önce ClientRpc çağır, sonra state'leri reset et
            OnCallEndedClientRpc(wasMissed);

            // State'leri reset et
            _networkIsCallActive.Value = false;
            _networkIsCallAnswered.Value = false;
            _networkIsInPhoneConversation.Value = false;
            _networkCurrentCallOwner.Value = 0;

            phoneWaitBar?.HideBar();

            if (_callCoroutine != null)
            {
                StopCoroutine(_callCoroutine);
                _callCoroutine = null;
            }
        }

        #endregion

        #region Call Actions

        /// <summary>
        /// Telefonu cevaplar
        /// </summary>
        public void AnswerCall()
        {
            if (!_isNetworkReady) return;
            if (!_networkIsCallActive.Value) return;
            if (_networkIsCallAnswered.Value) return;
            if (!_playerInPhoneArea) return;

            AnswerCallServerRpc();
        }

        [ServerRpc(RequireOwnership = false)]
        private void AnswerCallServerRpc(ServerRpcParams rpcParams = default)
        {
            if (!_networkIsCallActive.Value || _networkIsCallAnswered.Value) return;

            ulong clientId = rpcParams.Receive.SenderClientId;

            LogDebug($"AnswerCallServerRpc - Client {clientId} answering the call");

            // ✅ DEĞİŞTİRİLDİ: Önce owner'ı set et, sonra state'leri değiştir
            _networkCurrentCallOwner.Value = clientId;
            _networkIsCallAnswered.Value = true;
            _networkIsInPhoneConversation.Value = true;

            phoneWaitBar?.StartWaitBar(callAnswerDuration);

            // ✅ ClientRpc ile movement lock'u uygula
            OnCallAnsweredClientRpc(clientId);
        }

        #endregion

        #region Day Cycle Event

        private void HandleNewDay()
        {
            if (!IsServer || !_isNetworkReady) return;

            ResetHourlyChecks();

            if (_networkIsCallActive.Value || _networkIsInPhoneConversation.Value)
            {
                EndCallServer(wasMissed: false);
            }
        }

        private void ResetHourlyChecks()
        {
            for (int i = 0; i < _hourlyCallChecked.Length; i++)
            {
                _hourlyCallChecked[i] = false;
            }
            _lastCheckedHour = -1;
        }

        #endregion

        #region Audio

        private void PlayRingSound()
        {
            if (phoneRingSound != null)
            {
                phoneRingSound.Play();
            }
        }

        private void StopRingSound()
        {
            if (phoneRingSound != null)
            {
                phoneRingSound.Stop();
            }
        }

        private void PlayConversationSound()
        {
            if (conversationSound != null)
            {
                conversationSound.Play();
            }
        }

        private void StopConversationSound()
        {
            if (conversationSound != null)
            {
                conversationSound.Stop();
            }
        }

        #endregion

        #region Client RPCs

        [ClientRpc]
        private void OnCallStartedClientRpc()
        {
            LogDebug("Phone call started - ClientRPC");
        }

        [ClientRpc]
        private void OnCallAnsweredClientRpc(ulong answeringClientId)
        {
            LogDebug($"OnCallAnsweredClientRpc - AnsweringClient: {answeringClientId}, LocalClient: {NetworkManager.Singleton?.LocalClientId}");

            // ✅ YENİ: Telefonu cevaplayan client'ın movement'ını kilitle
            if (NetworkManager.Singleton == null) return;

            ulong localClientId = NetworkManager.Singleton.LocalClientId;

            if (answeringClientId == localClientId)
            {
                LogDebug($"✅ Client {localClientId} answered the call - Locking movement");
                LockLocalPlayerMovement(true);
            }
            else
            {
                LogDebug($"Client {localClientId} did NOT answer the call (answerer: {answeringClientId})");
            }
        }

        [ClientRpc]
        private void OnCallEndedClientRpc(bool wasMissed)
        {
            LogDebug($"OnCallEndedClientRpc - Missed: {wasMissed}");

            if (NetworkManager.Singleton == null) return;

            ulong localClientId = NetworkManager.Singleton.LocalClientId;

            // ✅ DEĞİŞTİRİLDİ: Her zaman local player'ın lock'unu kaldır
            // Çünkü call owner bilgisi reset edilmiş olabilir
            // Sadece konuşmada olan oyuncu zaten locked olacak
            LockLocalPlayerMovement(false);

            LogDebug($"Client {localClientId} - Movement unlocked after call end");
        }

        [ClientRpc]
        private void GivePrestigeToPlayerClientRpc(ulong targetClientId, float amount)
        {
            if (NetworkManager.Singleton == null) return;
            if (NetworkManager.Singleton.LocalClientId != targetClientId) return;

            PrestigeManager.Instance?.AddPrestige(amount);
        }

        [ClientRpc]
        private void ApplyMissedCallPenaltyClientRpc()
        {
            PrestigeManager.Instance?.AddPrestige(-prestigePenalty);
        }

        #endregion

        #region Public API

        /// <summary>
        /// Customer support aktifliğini ayarlar
        /// </summary>
        public void SetCustomerSupportActive(bool active)
        {
            if (!_isNetworkReady) return;

            if (IsServer)
            {
                _networkCustomerSupportActive.Value = active;
            }
            else
            {
                SetCustomerSupportActiveServerRpc(active);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void SetCustomerSupportActiveServerRpc(bool active)
        {
            _networkCustomerSupportActive.Value = active;
        }

        /// <summary>
        /// Çağrı şansını ayarlar
        /// </summary>
        public void SetCallChance(float newChance)
        {
            callChance = Mathf.Clamp01(newChance);
        }

        /// <summary>
        /// Çağrı cevaplama süresini ayarlar
        /// </summary>
        public void SetCallDuration(float newDuration)
        {
            callDuration = Mathf.Max(MIN_CALL_DURATION, newDuration);
        }

        /// <summary>
        /// Konuşma süresini ayarlar
        /// </summary>
        public void SetCallAnswerDuration(float newDuration)
        {
            callAnswerDuration = Mathf.Max(MIN_ANSWER_DURATION, newDuration);
        }

        /// <summary>
        /// Prestige ödülünü ayarlar
        /// </summary>
        public void SetPrestigeReward(float newReward)
        {
            prestigeReward = Mathf.Max(0, newReward);
        }

        /// <summary>
        /// Prestige cezasını ayarlar
        /// </summary>
        public void SetPrestigePenalty(float newPenalty)
        {
            prestigePenalty = Mathf.Max(0, newPenalty);
        }

        #endregion

        #region Test Methods

        /// <summary>
        /// Test amaçlı telefon çağrısı başlatır
        /// </summary>
        [ContextMenu("Test Phone Call")]
        public void TestPhoneCall()
        {
            if (!_isNetworkReady)
            {
                LogError("Network not ready for phone call test!");
                return;
            }

            if (IsServer)
            {
                LogDebug("Testing phone call as server");
                StartPhoneCallServer();
            }
            else
            {
                LogDebug("Requesting phone call test from server");
                TestPhoneCallServerRpc();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void TestPhoneCallServerRpc()
        {
            LogDebug("Received phone call test request on server");
            StartPhoneCallServer();
        }

        #endregion

        #region Logging

        private void LogDebug(string message)
        {
            Debug.Log($"{LOG_PREFIX} {message}");
        }

        private void LogWarning(string message)
        {
            Debug.LogWarning($"{LOG_PREFIX} {message}");
        }

        private void LogError(string message)
        {
            Debug.LogError($"{LOG_PREFIX} {message}");
        }

        #endregion

        #region Editor Debug

#if UNITY_EDITOR
        [ContextMenu("Debug: Print State")]
        private void DebugPrintState()
        {
            Debug.Log($"{LOG_PREFIX} === PHONE CALL STATE ===");
            Debug.Log($"Network Ready: {_isNetworkReady}");
            Debug.Log($"Call Active: {_networkIsCallActive.Value}");
            Debug.Log($"Call Answered: {_networkIsCallAnswered.Value}");
            Debug.Log($"In Conversation: {_networkIsInPhoneConversation.Value}");
            Debug.Log($"Call Owner: {_networkCurrentCallOwner.Value}");
            Debug.Log($"Player In Area: {_playerInPhoneArea}");
            Debug.Log($"Customer Support Active: {_networkCustomerSupportActive.Value}");
            Debug.Log($"Last Checked Hour: {_lastCheckedHour}");
        }

        [ContextMenu("Debug: Reset Hourly Checks")]
        private void DebugResetHourlyChecks()
        {
            if (IsServer)
            {
                ResetHourlyChecks();
                LogDebug("Hourly checks reset");
            }
        }
#endif

        #endregion
    }
}