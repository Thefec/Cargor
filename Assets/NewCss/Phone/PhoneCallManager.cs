using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace NewCss
{
    /// <summary>
    /// Telefon Sistemi V2 - Oyuncu inisiyatifinde (E basÄ±lÄ± tut) mÃ¼ÅŸteri Ã§aÄŸÄ±rma.
    /// Kota kontrolÃ¼, mesai saati kÄ±sÄ±tlamasÄ±, Ã¶dÃ¼l sistemi ve anti-spam korumasÄ± iÃ§erir.
    /// </summary>
    public class PhoneCallManager : NetworkBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[PhoneCall]";
        private const string PLAYER_TAG = "Character";

        #endregion

        #region Singleton

        public static PhoneCallManager Instance { get; private set; }

        #endregion

        #region Serialized Fields

        [Header("=== PHONE CALL SETTINGS ===")]
        [SerializeField, Tooltip("Telefonu kullanmak icin basili tutulmasi gereken sure (saniye)")]
        private float holdDuration = 2f;

        [Header("=== TIME RESTRICTIONS ===")]
        [SerializeField, Tooltip("Telefon kullanilabilir baslangic saati")]
        private int phoneStartHour = 8;

        [SerializeField, Tooltip("Telefon kullanilabilir bitis saati")]
        private int phoneEndHour = 18;

        [Header("=== ECONOMY SETTINGS ===")]
        [SerializeField, Tooltip("Tüm ekonomi sabitlerini içeren ScriptableObject")]
        private GameEconomySettings economySettings;

        // Backward-compat properties — SO yoksa hard-coded fallback
        private float timeSkipAmount  => economySettings != null ? economySettings.timeSkipAmount  : 20f;
        private float postCallCooldown=> economySettings != null ? economySettings.postCallCooldown : 30f;
        private int   maxCallsPerHour => economySettings != null ? economySettings.maxCallsPerHour  : 2;
        private int   callReward      => economySettings != null ? economySettings.callReward       : 10;

        [Header("=== AUDIO ===")]
        [SerializeField, Tooltip("Basarili cagri sesi")]
        private AudioSource successCallSound;

        [SerializeField, Tooltip("Basarisiz cagri sesi (mesai disi, kota doldu vs)")]
        private AudioSource failCallSound;


        [Header("=== WAIT BAR SYSTEM ===")]
        [SerializeField, Tooltip("Telefon bekleme cubugu")]
        private PhoneWaitBar phoneWaitBar;

        [Header("=== INTERACTION SETTINGS ===")]
        [SerializeField, Tooltip("Telefon collider'i")]
        private Collider phoneCollider;

        [SerializeField, Tooltip("Oyuncu tag'i")]
        private string playerTag = PLAYER_TAG;

        #endregion

        #region Private Fields

        private bool _playerInPhoneArea;
        private float _currentHoldTimer;
        private bool _isActionExecuted;
        private bool _isNetworkReady;

        // Anti-spam (client-side, sadece UI/feedback icin — asil kontrol serverda yapilir)
        private float _cooldownTimer;
        private bool _isOnCooldown;
        private int _callsThisHour;
        private int _lastCallHour = -1;

        // Anti-spam (server-side, per-client — asil guvenlik katmani, hackli client bunu atlayamaz)
        private class ServerCallState
        {
            public float CooldownEndTime;
            public int CallsThisHour;
            public int LastCallHour = -1;
        }

        private readonly Dictionary<ulong, ServerCallState> _serverCallStates = new Dictionary<ulong, ServerCallState>();

        private ServerCallState GetOrCreateServerCallState(ulong clientId)
        {
            if (!_serverCallStates.TryGetValue(clientId, out ServerCallState state))
            {
                state = new ServerCallState();
                _serverCallStates[clientId] = state;
            }
            return state;
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

        private void Update()
        {
            if (!_isNetworkReady) return;

            // Cooldown timer
            if (_isOnCooldown)
            {
                _cooldownTimer -= Time.deltaTime;
                if (_cooldownTimer <= 0f)
                {
                    _isOnCooldown = false;
                    _cooldownTimer = 0f;
                }
            }

            // Saatlik limit reset
            UpdateHourlyReset();

            // Sadece local player etkileÅŸimi yÃ¶netir
            if (_playerInPhoneArea && NetworkManager.Singleton.IsClient)
            {
                HandleInput();
            }
        }

        private void OnDestroy()
        {
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
            InitializeWaitBar();
            SetupPhoneCollider();
        }

        public override void OnNetworkDespawn()
        {
            _isNetworkReady = false;
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

        private void InitializeWaitBar()
        {
            if (phoneWaitBar == null)
            {
                phoneWaitBar = GetComponentInChildren<PhoneWaitBar>();
            }
            phoneWaitBar?.HideBar();
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

        #region Validation Checks

        /// <summary>
        /// Telefon kullanilabilir mi? Tum kontrolleri yapar.
        /// </summary>
        private bool CanUsePhone(out string reason)
        {
            // 1. Mesai saati kontrolu
            if (!IsWithinBusinessHours())
            {
                reason = "Mesai saatleri disinda (sadece " + phoneStartHour + ":00 - " + phoneEndHour + ":00 arasi)";
                return false;
            }

            // 2. Cooldown kontrolu
            if (_isOnCooldown)
            {
                reason = "Cooldown: " + _cooldownTimer.ToString("F0") + "s kaldi";
                return false;
            }

            // 3. Saatlik limit kontrolu
            if (_callsThisHour >= maxCallsPerHour)
            {
                reason = "Saatlik limit doldu (" + maxCallsPerHour + "/" + maxCallsPerHour + ")";
                return false;
            }

            // 4. Müşteri spawn kotası kontrolü
            if (CustomerManager.Instance != null && !CustomerManager.Instance.HasUnspawnedCustomers)
            {
                reason = "Bugün için çağırılabilecek başka müşteri kalmadı";
                return false;
            }

            // 5. Kuyruk kontrolu
            if (CustomerManager.Instance != null && CustomerManager.Instance.IsQueueFull)
            {
                reason = "Musteri kuyrugu dolu";
                return false;
            }

            reason = "";
            return true;
        }

        private bool IsWithinBusinessHours()
        {
            if (DayCycleManager.Instance == null) return false;
            int currentHour = DayCycleManager.Instance.CurrentHour;
            return currentHour >= phoneStartHour && currentHour < phoneEndHour;
        }

        private void UpdateHourlyReset()
        {
            if (DayCycleManager.Instance == null) return;
            int currentHour = DayCycleManager.Instance.CurrentHour;

            if (currentHour != _lastCallHour)
            {
                _callsThisHour = 0;
                _lastCallHour = currentHour;
            }
        }

        #endregion

        #region Input Handling

        private void HandleInput()
        {
            // Kontrol et: telefon kullanilabilir mi?
            if (!CanUsePhone(out string reason))
            {
                // Etkileşime basilsa bile bar gosterme
                if (InputBindingManager.GetActionDown(InputBindingManager.GameAction.Interact) && reason.Length > 0)
                {
                    LogDebug("Telefon kullanilamaz: " + reason);
                    PlayFailSound();
                }
                return;
            }

            // Oyuncu Etkileşim tusuna basili tutuyor mu?
            if (InputBindingManager.GetAction(InputBindingManager.GameAction.Interact))
            {
                if (!_isActionExecuted)
                {
                    _currentHoldTimer += Time.deltaTime;
                    float progress = Mathf.Clamp01(_currentHoldTimer / holdDuration);

                    // Bari guncelle
                    phoneWaitBar?.SetFillAmount(progress);

                    // Sure doldu mu?
                    if (_currentHoldTimer >= holdDuration)
                    {
                        LogDebug("Hold duration reached, executing call action");
                        ExecuteCallAction();
                    }
                }
            }
            else
            {
                // Tusu birakti, resetle
                if (_currentHoldTimer > 0.1f)
                {
                    LogDebug("E key released at " + _currentHoldTimer.ToString("F2") + "s (needed " + holdDuration.ToString("F2") + "s)");
                }
                ResetHoldState();
            }
        }

        private void ResetHoldState()
        {
            _currentHoldTimer = 0f;
            _isActionExecuted = false;
            phoneWaitBar?.HideBar();
        }

        private void ExecuteCallAction()
        {
            _isActionExecuted = true;
            _currentHoldTimer = 0f;
            phoneWaitBar?.HideBar();

            // ServerRpc gonder
            RequestCallServerRpc();
        }

        #endregion

        #region Server Logic

        [ServerRpc(RequireOwnership = false)]
        private void RequestCallServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            LogDebug("Call requested by Client " + clientId);

            // Server-side kontrol: mesai saati
            if (!IsWithinBusinessHours())
            {
                LogDebug("Server rejected: Outside business hours");
                CallFailedClientRpc(clientId);
                return;
            }

            // Server-side kontrol: unspawned count
            if (CustomerManager.Instance == null || !CustomerManager.Instance.HasUnspawnedCustomers)
            {
                LogDebug("Server rejected: No actual unspawned customers left");
                CallFailedClientRpc(clientId);
                return;
            }

            // Server-side anti-spam (asil guvenlik katmani — client-side kontroller sadece UI icindir,
            // hackli/spamlanan RPC istekleri burada engellenir)
            ServerCallState callState = GetOrCreateServerCallState(clientId);
            int currentHour = DayCycleManager.Instance != null ? DayCycleManager.Instance.CurrentHour : -1;

            // Saatlik reset
            if (currentHour != callState.LastCallHour)
            {
                callState.CallsThisHour = 0;
                callState.LastCallHour = currentHour;
            }

            // Cooldown kontrolu
            if (Time.time < callState.CooldownEndTime)
            {
                LogDebug("Server rejected: Client " + clientId + " on cooldown (" + (callState.CooldownEndTime - Time.time).ToString("F1") + "s left)");
                CallFailedClientRpc(clientId);
                return;
            }

            // Saatlik limit kontrolu
            if (callState.CallsThisHour >= maxCallsPerHour)
            {
                LogDebug("Server rejected: Client " + clientId + " hit hourly limit (" + callState.CallsThisHour + "/" + maxCallsPerHour + ")");
                CallFailedClientRpc(clientId);
                return;
            }

            // 1. Musteri spawnla
            bool spawnSuccess = false;
            if (CustomerManager.Instance != null)
            {
                spawnSuccess = CustomerManager.Instance.ForceSpawnNextCustomer();
            }

            if (!spawnSuccess)
            {
                LogDebug("Server rejected: Spawn failed");
                CallFailedClientRpc(clientId);
                return;
            }

            // 2. Zamani ileri sar
            if (DayCycleManager.Instance != null)
            {
                DayCycleManager.Instance.SkipTime(timeSkipAmount);
            }

            // 3. Odul ver
            if (MoneySystem.Instance != null)
            {
                MoneySystem.Instance.AddMoney(callReward);
            }

            // Server-side anti-spam state guncelle (basarili cagri)
            callState.CooldownEndTime = Time.time + postCallCooldown;
            callState.CallsThisHour++;
            callState.LastCallHour = currentHour;

            // 4. Basari efektleri
            CallSucceededClientRpc(clientId, callReward);

            LogDebug("Call successful! Reward: " + callReward + ", TimeSkip: " + timeSkipAmount + "min");
        }

        #endregion

        #region Client RPCs

        [ClientRpc]
        private void CallSucceededClientRpc(ulong callerClientId, int reward)
        {
            // Basari sesi herkeste calsin
            if (successCallSound != null)
            {
                successCallSound.Play();
            }

            // Cooldown sadece arayan oyuncuda baslasin
            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.LocalClientId == callerClientId)
            {
                StartCooldown();
                _callsThisHour++;
                LogDebug("Call succeeded! +" + reward + " para. Cooldown: " + postCallCooldown + "s. Calls this hour: " + _callsThisHour + "/" + maxCallsPerHour);
            }
        }

        [ClientRpc]
        private void CallFailedClientRpc(ulong callerClientId)
        {
            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.LocalClientId == callerClientId)
            {
                PlayFailSound();
                LogDebug("Call failed (server rejected)");
            }
        }

        #endregion

        #region Cooldown

        private void StartCooldown()
        {
            _isOnCooldown = true;
            _cooldownTimer = postCallCooldown;
        }

        #endregion

        #region Audio

        private void PlayFailSound()
        {
            if (failCallSound != null)
            {
                failCallSound.Play();
            }
        }

        #endregion

        #region Trigger Detection

        private void OnTriggerEnter(Collider other)
        {
            if (IsLocalPlayer(other))
            {
                _playerInPhoneArea = true;
                LogDebug("Player entered phone area");
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (IsLocalPlayer(other))
            {
                _playerInPhoneArea = false;
                ResetHoldState();
                LogDebug("Player exited phone area");
            }
        }

        private bool IsLocalPlayer(Collider other)
        {
            if (!other.CompareTag(playerTag)) return false;
            var netObj = other.GetComponent<NetworkObject>();
            return netObj != null && netObj.IsOwner && netObj.IsLocalPlayer;
        }

        #endregion

        #region Legacy Compatibility

        /// <summary>
        /// Legacy - DifficultyManager uyumu icin
        /// </summary>
        public void SetCallChance(float newChance) { }
        public void SetCustomerSupportActive(bool active) { }

        #endregion

        #region Logging

        private void LogDebug(string message)
        {
            Debug.Log(LOG_PREFIX + " " + message);
        }

        #endregion
    }
}
