using Unity.Netcode;
using UnityEngine;

namespace NewCss
{
    /// <summary>
    /// Telefon Sistemi V3 - REAKTİF. Sunucu, mesai saatleri içinde her oyun-saati
    /// değiştiğinde belirli bir olasılıkla telefonu çaldırır (phoneRingChancePerHour,
    /// CUSTOMER SUPPORT etkinliği günü phoneRingEventMultiplier ile artar).
    /// Oyuncu telefon alanındayken E'ye basarak açar; açarsa para + prestij ödülü alır.
    /// Açılmazsa bir süre sonra çalma kendiliğinden durur, CEZA YOK.
    /// Müşteri spawn'ı ve zaman atlama tamamen kaldırıldı (bkz. eski V2, git geçmişi).
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

        [Header("=== TIME RESTRICTIONS ===")]
        [SerializeField, Tooltip("Telefonun calabilecegi baslangic saati")]
        private int phoneStartHour = 8;

        [SerializeField, Tooltip("Telefonun calabilecegi bitis saati")]
        private int phoneEndHour = 18;

        [Header("=== RING SETTINGS ===")]
        [SerializeField, Tooltip("Acilmayan cagrinin kendiliginden susmasine kadar gecen sure (saniye). Ceza yok.")]
        private float ringDuration = 25f;

        [Header("=== ECONOMY SETTINGS ===")]
        [SerializeField, Tooltip("Tüm ekonomi sabitlerini içeren ScriptableObject")]
        private GameEconomySettings economySettings;

        // Backward-compat fallback'ler — SO atanmamissa hard-coded degerler kullanilir
        private float PhoneRingChancePerHour => economySettings != null ? economySettings.phoneRingChancePerHour : 0.30f;
        private float PhoneRingEventMultiplier => economySettings != null ? economySettings.phoneRingEventMultiplier : 1.5f;
        private float PhoneRingPerkBonus => economySettings != null ? economySettings.phoneRingPerkBonus : 0f;
        private int   CallMoneyReward => economySettings != null ? economySettings.callMoneyReward : 20;
        private float CallPrestigeReward => economySettings != null ? economySettings.callPrestigeReward : 0.2f;

        [Header("=== AUDIO ===")]
        [SerializeField, Tooltip("Telefon calarken loop calan ses")]
        private AudioSource ringingSound;

        [SerializeField, Tooltip("Basarili cagri (acildi) sesi")]
        private AudioSource successCallSound;

        [Header("=== VISUAL INDICATOR ===")]
        [SerializeField, Tooltip("Telefon caldigini gosteren gorsel isaret (mevcut wait bar altyapisi yeniden kullanilir)")]
        private PhoneWaitBar phoneWaitBar;

        [Header("=== INTERACTION SETTINGS ===")]
        [SerializeField, Tooltip("Telefon collider'i")]
        private Collider phoneCollider;

        [SerializeField, Tooltip("Oyuncu tag'i")]
        private string playerTag = PLAYER_TAG;

        #endregion

        #region Network State

        // Server-write, everyone-read: telefon su an caliyor mu?
        private readonly NetworkVariable<bool> _isRinging = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public bool IsRinging => _isRinging.Value;

        #endregion

        #region Private Fields

        private bool _playerInPhoneArea;
        private bool _isNetworkReady;

        // Server-only: saatlik zar atma takibi + calma suresi sayaci
        private int _lastRingRollHour = -1;
        private float _ringElapsedTime;

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

            if (IsServer)
            {
                ServerUpdateRinging();
            }

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
            {
                if (_playerInPhoneArea)
                {
                    HandleInput();
                }
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

            _isRinging.OnValueChanged += HandleRingingChanged;
            // Late-join: netvar zaten true olarak spawn olabilir, OnValueChanged geriye dönük tetiklenmez.
            HandleRingingChanged(false, _isRinging.Value);

            DayCycleManager.OnNewDay += HandleNewDay;
        }

        public override void OnNetworkDespawn()
        {
            _isNetworkReady = false;
            _isRinging.OnValueChanged -= HandleRingingChanged;
            DayCycleManager.OnNewDay -= HandleNewDay;
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

        #region Server Logic - Ringing

        private void ServerUpdateRinging()
        {
            if (_isRinging.Value)
            {
                // Mesai bitince aktif calma da sessizce durur (ceza yok).
                if (!IsWithinBusinessHours())
                {
                    StopRinging();
                    return;
                }

                _ringElapsedTime += Time.deltaTime;
                if (_ringElapsedTime >= ringDuration)
                {
                    StopRinging();
                }
                return;
            }

            if (!IsWithinBusinessHours()) return;
            if (DayCycleManager.Instance == null) return;

            int currentHour = DayCycleManager.Instance.CurrentHour;
            if (currentHour == _lastRingRollHour) return;

            // Saat degisti: bu saat icin bir kez zar at.
            _lastRingRollHour = currentHour;
            TryRollRing(currentHour);
        }

        private void TryRollRing(int currentHour)
        {
            float chance = GetEffectiveRingChance();
            if (Random.value <= chance)
            {
                _isRinging.Value = true;
                _ringElapsedTime = 0f;
                LogDebug($"Phone starts ringing (chance={chance:P0}, hour={currentHour})");
            }
        }

        private float GetEffectiveRingChance()
        {
            float baseChance = PhoneRingChancePerHour;
            bool eventActive = EventEffectManager.Instance != null &&
                                EventEffectManager.Instance.IsEventActive("CUSTOMER SUPPORT");
            float multiplier = eventActive ? PhoneRingEventMultiplier : 1f;
            return Mathf.Clamp(baseChance * multiplier + PhoneRingPerkBonus, 0f, 0.65f);
        }

        private void StopRinging()
        {
            _isRinging.Value = false;
            _ringElapsedTime = 0f;
        }

        private void HandleNewDay()
        {
            if (!IsServer) return;
            StopRinging();
            _lastRingRollHour = -1;
        }

        private bool IsWithinBusinessHours()
        {
            if (DayCycleManager.Instance == null) return false;
            int currentHour = DayCycleManager.Instance.CurrentHour;
            return currentHour >= phoneStartHour && currentHour < phoneEndHour;
        }

        #endregion

        #region Input Handling

        private void HandleInput()
        {
            if (!IsRinging) return;

            if (InputBindingManager.GetActionDown(InputBindingManager.GameAction.Interact))
            {
                LogDebug("Answer requested");
                AnswerServerRpc();
            }
        }

        #endregion

        #region Server Logic - Answer

        [ServerRpc(RequireOwnership = false)]
        private void AnswerServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;

            if (!_isRinging.Value)
            {
                // Telefon zaten calmiyor (rakip oyuncu once acti / kendiliginden sustu) — sessizce yok say.
                LogDebug("Answer ignored: phone not ringing (Client " + clientId + ")");
                return;
            }

            StopRinging();

            int moneyReward = CallMoneyReward;
            float prestigeReward = CallPrestigeReward;

            if (MoneySystem.Instance != null)
            {
                MoneySystem.Instance.AddMoney(moneyReward);
            }

            if (PrestigeManager.Instance != null)
            {
                PrestigeManager.Instance.AddPrestige(prestigeReward);
            }

            CallAnsweredClientRpc(clientId, moneyReward);

            // Quest sistemi: AnswerPhone tetikleyicisi tanımlıydı ama hiçbir yerden çağrılmıyordu.
            // Burası doğru tek nokta — ServerRpc (server-only) ve yukarıdaki `!_isRinging` guard'ı
            // rakip oyuncuların aynı çağrıyı ikinci kez saymasını engelliyor.
            Quest.QuestTracker.NotifyPhoneAnswered();

            LogDebug("Call answered by Client " + clientId + "! +" + moneyReward + " TL, +" + prestigeReward + " prestij");
        }

        #endregion

        #region Client RPCs

        [ClientRpc]
        private void CallAnsweredClientRpc(ulong answeringClientId, int reward)
        {
            if (successCallSound != null)
            {
                successCallSound.Play();
            }
        }

        #endregion

        #region Client Audio/Visual

        private void HandleRingingChanged(bool previousValue, bool currentValue)
        {
            if (currentValue)
            {
                if (ringingSound != null && !ringingSound.isPlaying)
                {
                    ringingSound.Play();
                }
                phoneWaitBar?.StartCountdown(ringDuration);
            }
            else
            {
                if (ringingSound != null)
                {
                    ringingSound.Stop();
                }
                phoneWaitBar?.HideBar();
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
        /// Legacy stub — DifficultyManager (oyuncu-sayisi-olcekli cagri sansi) hala bunu cagiriyor.
        /// Reaktif V3'te calma sansi dogrudan GameEconomySettings.phoneRingChancePerHour +
        /// EventEffectManager.IsEventActive("CUSTOMER SUPPORT") ile okunuyor; DifficultyManager'in
        /// oyuncu-sayisi-olcekli degeri artik kullanilmiyor. Imza kirilmasin diye no-op birakildi.
        /// </summary>
        public void SetCallChance(float newChance) { }

        /// <summary>Legacy stub — cagiran yok, imza uyumlulugu icin korunuyor.</summary>
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
