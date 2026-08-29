using Unity.Netcode;
using UnityEngine;

namespace NewCss
{
    /// <summary>
    /// Telefon Sistemi V4 - DIŞARI ARAMA (PlateUp geçişi, plan §D,
    /// plans/plateup-musteri-telefon.md, 2026-08-29). Telefon artık ÇALMAZ; oyuncu telefon
    /// alanındayken E'ye basarak sıradaki müşteriyi hemen çağırır. Bedel: gün saati
    /// (DayCycleManager.SkipTime) ileri sarılır. Karşılığında küçük bir para + prestij ödülü
    /// verilir. Spam'i önlemek için server-authoritative bir cooldown var (bkz. commit
    /// f9a3f1b "bedava-para exploit" — client'a güvenilmez, guard'lar server'da).
    /// V3'ün saatlik-zar-atma / reaktif çalma modeli tamamen kaldırıldı (git geçmişi).
    /// </summary>
    public class PhoneCallManager : NetworkBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[PhoneCall]";
        private const string PLAYER_TAG = "Character";
        private const string CUSTOMER_SUPPORT_EVENT = "CUSTOMER SUPPORT";

        #endregion

        #region Singleton

        public static PhoneCallManager Instance { get; private set; }

        #endregion

        #region Serialized Fields

        [Header("=== TIME RESTRICTIONS ===")]
        [SerializeField, Tooltip("Telefonun kullanılabileceği başlangıç saati")]
        private int phoneStartHour = 8;

        [SerializeField, Tooltip("Telefonun kullanılabileceği bitiş saati")]
        private int phoneEndHour = 18;

        [Header("=== ECONOMY SETTINGS ===")]
        [SerializeField, Tooltip("Tüm ekonomi sabitlerini içeren ScriptableObject")]
        private GameEconomySettings economySettings;

        // Backward-compat fallback'ler — SO atanmamissa hard-coded degerler kullanilir.
        private float PhoneCooldownSecondsBase => economySettings != null ? economySettings.phoneCooldownSeconds : 20f;
        private float PhoneCooldownPerkBonusSeconds => economySettings != null ? economySettings.phoneCooldownPerkBonusSeconds : 0f;
        private int   CallMoneyReward => economySettings != null ? economySettings.callMoneyReward : 20;
        private float CallPrestigeReward => economySettings != null ? economySettings.callPrestigeReward : 0.4f;

        private float TimeSkipAmountMinutes
        {
            get
            {
                int playerCount = DifficultyManager.Instance != null ? DifficultyManager.Instance.PlayerCount : 1;
                return economySettings != null ? economySettings.GetTimeSkipAmountMinutes(playerCount) : 115f;
            }
        }

        [Header("=== AUDIO ===")]
        [SerializeField, Tooltip("Basarili cagri (musteri cagrildi) sesi")]
        private AudioSource successCallSound;

        [Header("=== VISUAL INDICATOR ===")]
        [SerializeField, Tooltip("Cooldown gostergesi (eski 'calma' bari yeniden kullanildi — artik cooldown doldukca 1->0 dolar)")]
        private PhoneWaitBar phoneWaitBar;

        [Header("=== INTERACTION SETTINGS ===")]
        [SerializeField, Tooltip("Telefon collider'i")]
        private Collider phoneCollider;

        [SerializeField, Tooltip("Oyuncu tag'i")]
        private string playerTag = PLAYER_TAG;

        #endregion

        #region Network State

        // Server-write, everyone-read: telefon su an cooldown'da mi? (UI aynasi — asil kapi
        // kontrolu server-only _cooldownEndTime'a bakar, bkz. asagisi).
        private readonly NetworkVariable<bool> _isOnCooldown = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // Gecerli cooldown'un toplam suresi (perk/event'e gore degisebildigi icin sabit degil) —
        // client bari bununla baslatir (gec katilan da dahil, bkz. OnNetworkSpawn).
        private readonly NetworkVariable<float> _cooldownDuration = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public bool IsOnCooldown => _isOnCooldown.Value;

        #endregion

        #region Private Fields

        private bool _playerInPhoneArea;
        private bool _isNetworkReady;

        // Server-only kaynak: RPC guard'i BUNA bakar (NetworkVariable yalnizca UI aynasi,
        // bir kare gecikmeli olabilir). Time.time tabanli — yalnizca server kendi degerini
        // kendi icinde karsilastirir, cross-client senkron gerekmez.
        private float _cooldownEndTime = -1f;

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
                ServerUpdateCooldown();
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
            WarnOnMissingReferences();

            _isOnCooldown.OnValueChanged += HandleCooldownChanged;
            // Late-join: netvar zaten true olarak spawn olabilir, OnValueChanged geriye dönük tetiklenmez.
            HandleCooldownChanged(false, _isOnCooldown.Value);

            DayCycleManager.OnNewDay += HandleNewDay;
        }

        public override void OnNetworkDespawn()
        {
            _isNetworkReady = false;
            _isOnCooldown.OnValueChanged -= HandleCooldownChanged;
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

        /// <summary>
        /// Bagli olmayan inspector alanlarini bir kez uyarir. Bu alanlarin hepsi kullanim
        /// noktasinda null-guard'li: atanmadiklarinda sistem HATA VERMEZ, sadece sessizlesir
        /// (cevaplama sesi calmaz, cooldown bari gorunmez). Bir oyun gunu boyunca "telefon hic
        /// calmadi" sanilmasinin sebebi tam olarak buydu (2026-08-13) — V4'te de ayni sinif
        /// sessiz-bug riskini tekrarlamasin diye korunuyor.
        /// </summary>
        private void WarnOnMissingReferences()
        {
            if (successCallSound == null)
                Debug.LogWarning(LOG_PREFIX + " successCallSound atanmamis — cagri sesi calmayacak.");
            if (phoneWaitBar == null)
                Debug.LogWarning(LOG_PREFIX + " phoneWaitBar atanmamis — cooldown bari gosterilmeyecek.");
            if (phoneCollider == null)
                Debug.LogWarning(LOG_PREFIX + " phoneCollider bulunamadi — telefon KULLANILAMAZ.");
        }

        #endregion

        #region Server Logic - Cooldown

        private void ServerUpdateCooldown()
        {
            if (!_isOnCooldown.Value) return;
            if (Time.time >= _cooldownEndTime)
            {
                _isOnCooldown.Value = false;
            }
        }

        /// <summary>
        /// Efektif cooldown süresi: taban değer (economist, phoneCooldownSeconds=20 flat) eksi
        /// phone_line perkinin mutlak-atama azaltması, CUSTOMER SUPPORT günü yarıya iner.
        /// phoneCooldownPerkBonusSeconds=10f economist onaylı (2026-08-29,
        /// .claude/agent-memory/economist/phone_cooldown_perk_event_stacking_2026-08-29.md) —
        /// perk/event kotayı büyütmediğinden ("HasUnspawnedCustomers" tavanına çarpar),
        /// cooldown kısaltmanın ekonomik etkisi yok, çarpışma riski yok.
        /// </summary>
        private float GetEffectiveCooldownSeconds()
        {
            float cooldown = Mathf.Max(1f, PhoneCooldownSecondsBase - PhoneCooldownPerkBonusSeconds);

            bool eventActive = EventEffectManager.Instance != null &&
                                EventEffectManager.Instance.IsEventActive(CUSTOMER_SUPPORT_EVENT);
            if (eventActive)
            {
                cooldown *= 0.5f;
            }

            return cooldown;
        }

        private void HandleNewDay()
        {
            if (!IsServer) return;
            _cooldownEndTime = -1f;
            _isOnCooldown.Value = false;
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
            if (IsOnCooldown) return;

            if (InputBindingManager.GetActionDown(InputBindingManager.GameAction.Interact))
            {
                LogDebug("Call requested");
                CallNextCustomerServerRpc();
            }
        }

        #endregion

        #region Server Logic - Call

        [ServerRpc(RequireOwnership = false)]
        private void CallNextCustomerServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;

            if (!IsWithinBusinessHours())
            {
                LogDebug("Call rejected: outside business hours (Client " + clientId + ")");
                return;
            }

            if (CustomerManager.Instance == null || !CustomerManager.Instance.HasUnspawnedCustomers)
            {
                LogDebug("Call rejected: no unspawned customers left today (Client " + clientId + ")");
                return;
            }

            if (CustomerManager.Instance.IsQueueFull)
            {
                LogDebug("Call rejected: queue full (Client " + clientId + ")");
                return;
            }

            if (Time.time < _cooldownEndTime)
            {
                LogDebug("Call rejected: cooldown active (Client " + clientId + ")");
                return;
            }

            // Zaman atlaması gunu musteri-cikis saatinin (17:30) otesine sicratacaksa cagriyi
            // reddet. Aksi halde az once cagrilan musteri, ayni karede CustomerManager'in
            // gun-sonu kesimine yakalanip servis edilemeden CEZA uretirdi: oyuncu hem +0.4
            // arama prestijini hem -0.4 kayip cezasini gorurdu (QA bulgusu 2026-08-29).
            // IsWithinBusinessHours yetmiyor: phoneEndHour=18, cikis esigi ise 17.5.
            float timeSkipMinutes = TimeSkipAmountMinutes;
            if (DayCycleManager.Instance != null &&
                DayCycleManager.Instance.PredictTimeAfterSkip(timeSkipMinutes) >= CustomerManager.CUSTOMER_EXIT_HOUR)
            {
                LogDebug("Call rejected: time skip would pass customer exit hour (Client " + clientId + ")");
                return;
            }

            bool spawned = CustomerManager.Instance.ForceSpawnNextCustomer();
            if (!spawned)
            {
                // Guard'lar geçti ama spawn yine de başarısız oldu (ör. iç saat penceresi farklı
                // bir kenar durumu) — plan §D: ödül/cooldown VERME.
                LogDebug("Call rejected: ForceSpawnNextCustomer returned false (Client " + clientId + ")");
                return;
            }

            float effectiveCooldown = GetEffectiveCooldownSeconds();
            _cooldownEndTime = Time.time + effectiveCooldown;
            _cooldownDuration.Value = effectiveCooldown;
            _isOnCooldown.Value = true;

            DayCycleManager.Instance?.SkipTime(timeSkipMinutes);

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

            // Quest sistemi: AnswerPhone tetikleyicisi burada — ServerRpc (server-only), rakip
            // oyuncuların aynı çağrıyı ikinci kez saymasını yukarıdaki cooldown guard'ı engelliyor.
            Quest.QuestTracker.NotifyPhoneAnswered();

            LogDebug("Call placed by Client " + clientId + "! +" + moneyReward + " TL, +" + prestigeReward +
                     " prestij, +" + timeSkipMinutes + " dk zaman atladi (cooldown=" + effectiveCooldown + "s).");
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

        private void HandleCooldownChanged(bool previousValue, bool currentValue)
        {
            if (currentValue)
            {
                phoneWaitBar?.StartCountdown(_cooldownDuration.Value);
            }
            else
            {
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
