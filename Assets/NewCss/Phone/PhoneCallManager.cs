using Unity.Netcode;
using UnityEngine;

namespace NewCss
{
    /// <summary>
    /// Telefon Sistemi V4 - DIŞARI ARAMA (PlateUp geçişi, plan §D,
    /// plans/plateup-musteri-telefon.md, 2026-08-29). Telefon artık ÇALMAZ; oyuncu telefon
    /// alanındayken E'yi BASILI TUTARAK (feature/plateup-day-cycle, 2026-08-30 — eskiden tek
    /// basış anında çağırıyordu, kullanıcı bunu yanlış anlaşılabilir buldu) sıradaki müşteriyi
    /// çağırır: bar boştan dolmaya başlar (StartDialServerRpc/CompleteDialServerRpc,
    /// phoneDialHoldSeconds), erken bırakılırsa iptal olur (CancelDialServerRpc). Dolunca
    /// çağrı gerçekleşir: bedel gün saatinin (DayCycleManager.SkipTime) ileri sarılması,
    /// karşılığında küçük bir para + prestij ödülü. Çağrı sonrası, ARDINDAN, mevcut
    /// server-authoritative cooldown (3s taban, kullanıcı isteği 2026-08-30) devreye girer —
    /// aynı bar dolu'dan boşa döner
    /// (bkz. commit f9a3f1b "bedava-para exploit" — client'a güvenilmez, guard'lar server'da).
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
        private float PhoneCooldownSecondsBase => economySettings != null ? economySettings.phoneCooldownSeconds : 3f;
        private float PhoneCooldownPerkBonusSeconds => economySettings != null ? economySettings.phoneCooldownPerkBonusSeconds : 0f;
        private int   CallMoneyReward => economySettings != null ? economySettings.callMoneyReward : 0;
        private float CallPrestigeReward => economySettings != null ? economySettings.callPrestigeReward : 0.4f;

        // UX/his parametresi (EKONOMİK DEĞİL — para/süre/ödül/multiplier değil, saf input-timing).
        private float PhoneDialHoldSeconds => economySettings != null ? economySettings.phoneDialHoldSeconds : 1f;

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

        // Server-only "basili tutma" (dialing) kilidi. NetworkVariable DEGIL — diger client'larin
        // "biri ceviriyor" gormesi bilincli olarak kapsam disi (basitlik icin, plan
        // feature/plateup-day-cycle 2026-08-30). ulong.MaxValue = kimse cevirmiyor.
        private ulong _dialingClientId = ulong.MaxValue;
        private float _dialStartTime = -1f;

        // Client-only: yalnizca YEREL oyuncunun kendi bar animasyonu icin.
        private bool _isDialingLocally;
        private float _localDialStartTime;

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
            if (_isOnCooldown.Value && Time.time >= _cooldownEndTime)
            {
                _isOnCooldown.Value = false;
            }

            // Savunma agi: Complete/Cancel hic gelmediyse (ör. client disconnect oldu, basili tutma
            // sirasinda) dialing kilidi sonsuza dek takili kalmasin. +2f tolerans pay: normal
            // akiste Complete, hold suresi dolar dolmaz gelir; bu yalnizca kacan paket/disconnect
            // senaryosu icin son çare.
            if (_dialingClientId != ulong.MaxValue && Time.time - _dialStartTime > PhoneDialHoldSeconds + 2f)
            {
                LogDebug("Dial lock force-released (Client " + _dialingClientId + " — timeout/disconnect?).");
                _dialingClientId = ulong.MaxValue;
            }
        }

        /// <summary>
        /// Efektif cooldown süresi: taban değer (phoneCooldownSeconds, 3f — kullanıcı isteği
        /// 2026-08-30, eski 20f çok uzundu) eksi phone_line perkinin mutlak-atama azaltması
        /// (phoneCooldownPerkBonusSeconds=1f — economist round10 U5, 2026-08-30). CUSTOMER
        /// SUPPORT ARTIK BURAYA DOKUNMUYOR (economist round10 U3, 2026-08-30): cooldown ×0.5
        /// mekanik olarak NO-OP idi (HasUnspawnedCustomers günlük kota tavanı cooldown süresinden
        /// bağımsız, bkz. phone_cooldown_perk_event_stacking_2026-08-29.md) ama "pozitif" event
        /// tabelasıyla oyuncuyu spam'e çağırıyordu. Event etkisi artık GetEffectiveTimeSkipMinutes
        /// üzerinden GERÇEK bir kaldıraca (zaman maliyeti ×0.5) taşındı.
        /// </summary>
        private float GetEffectiveCooldownSeconds()
        {
            return Mathf.Max(1f, PhoneCooldownSecondsBase - PhoneCooldownPerkBonusSeconds);
        }

        /// <summary>
        /// Efektif zaman-atlama miktarı (dakika): TimeSkipAmountMinutes (P-bazlı taban ×
        /// phoneTimeSkipPerkMultiplier, phone_line perki dahil), CUSTOMER SUPPORT günü ayrıca
        /// ×0.5 (economist round10 U3, 2026-08-30 — eski cooldown-indirimi NO-OP'un yerine geçti).
        /// ValidateCallGuards (17:30 guard) VE ExecuteCall (gerçek SkipTime çağrısı) İKİSİ DE bu
        /// metodu kullanmalı — aksi halde guard yanlış hesaplar (bkz. round10 §2 U3 notu).
        /// </summary>
        private float GetEffectiveTimeSkipMinutes()
        {
            float minutes = TimeSkipAmountMinutes;

            bool eventActive = EventEffectManager.Instance != null &&
                                EventEffectManager.Instance.IsEventActive(CUSTOMER_SUPPORT_EVENT);
            if (eventActive)
            {
                // §B.5 (2026-09-25): "arama zaman atlatmıyor" — eski ×0.5 (yarı zaman maliyeti) yerine
                // ×0 (hiç zaman maliyeti yok).
                minutes *= 0f;
            }

            return minutes;
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
            if (_isDialingLocally)
            {
                UpdateLocalDial();
                return;
            }

            if (IsOnCooldown) return;

            if (InputBindingManager.GetActionDown(InputBindingManager.GameAction.Interact))
            {
                LogDebug("Dial started (hold)");
                _isDialingLocally = true;
                _localDialStartTime = Time.time;
                StartDialServerRpc();
            }
        }

        /// <summary>
        /// Basili-tutma karesi: E hala basiliysa (GetAction, GetActionDown DEGIL) bari doldur;
        /// dolunca CompleteDialServerRpc gonder. Birakildiysa CancelDialServerRpc ile iptal et.
        /// Sunucu reddederse (DialRejectedClientRpc) bu bayrak disaridan da false'a cekilir.
        /// </summary>
        private void UpdateLocalDial()
        {
            if (InputBindingManager.GetAction(InputBindingManager.GameAction.Interact))
            {
                float fill = Mathf.Clamp01((Time.time - _localDialStartTime) / PhoneDialHoldSeconds);
                phoneWaitBar?.SetFillAmount(fill);

                if (fill >= 1f)
                {
                    _isDialingLocally = false;
                    CompleteDialServerRpc();
                }
            }
            else
            {
                _isDialingLocally = false;
                CancelDialServerRpc();
                phoneWaitBar?.HideBar();
            }
        }

        #endregion

        #region Server Logic - Call

        /// <summary>
        /// Cagri yapilabilir mi? Hem StartDial (basili tutmaya baslarken) hem CompleteDial
        /// (basili tutma bitince, dunya durumu degismis olabilecegi icin TEKRAR) tarafindan
        /// cagrilir — guard sirasi/mantigi V4'ten (tek-basisli-cagri) BIREBIR korunuyor.
        /// </summary>
        private bool ValidateCallGuards(ulong clientId, string context)
        {
            if (!IsWithinBusinessHours())
            {
                LogDebug("Call rejected (" + context + "): outside business hours (Client " + clientId + ")");
                return false;
            }

            if (CustomerManager.Instance == null || !CustomerManager.Instance.HasUnspawnedCustomers)
            {
                LogDebug("Call rejected (" + context + "): no unspawned customers left today (Client " + clientId + ")");
                return false;
            }

            if (CustomerManager.Instance.IsQueueFull)
            {
                LogDebug("Call rejected (" + context + "): queue full (Client " + clientId + ")");
                return false;
            }

            if (Time.time < _cooldownEndTime)
            {
                LogDebug("Call rejected (" + context + "): cooldown active (Client " + clientId + ")");
                return false;
            }

            // Zaman atlaması gunu musteri-cikis saatinin (17:30) otesine sicratacaksa cagriyi
            // reddet. Aksi halde az once cagrilan musteri, ayni karede CustomerManager'in
            // gun-sonu kesimine yakalanip servis edilemeden CEZA uretirdi: oyuncu hem +0.4
            // arama prestijini hem -0.4 kayip cezasini gorurdu (QA bulgusu 2026-08-29).
            // IsWithinBusinessHours yetmiyor: phoneEndHour=18, cikis esigi ise 17.5.
            float timeSkipMinutes = GetEffectiveTimeSkipMinutes();
            if (DayCycleManager.Instance != null &&
                DayCycleManager.Instance.PredictTimeAfterSkip(timeSkipMinutes) >= CustomerManager.CUSTOMER_EXIT_HOUR)
            {
                LogDebug("Call rejected (" + context + "): time skip would pass customer exit hour (Client " + clientId + ")");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Guard'lar gectikten SONRA gercek cagriyi yapar: spawn, zaman atlamasi, odul,
        /// cooldown baslatma. Eski V4 CallNextCustomerServerRpc'nin govdesi — kod tekrarini
        /// onlemek icin buraya cikarildi (feature/plateup-day-cycle, 2026-08-30).
        /// </summary>
        private void ExecuteCall(ulong clientId)
        {
            float timeSkipMinutes = GetEffectiveTimeSkipMinutes();

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

        /// <summary>
        /// LEGACY (V4 tek-basis) — artik hicbir client bunu cagirmiyor (basili-tutma modeli
        /// StartDial/CompleteDial kullaniyor), ama imza uyumlulugu icin silinmedi.
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        private void CallNextCustomerServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (!ValidateCallGuards(clientId, "legacy-instant")) return;
            ExecuteCall(clientId);
        }

        #endregion

        #region Server Logic - Dial (basili tutma)

        [ServerRpc(RequireOwnership = false)]
        private void StartDialServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;

            if (_dialingClientId != ulong.MaxValue)
            {
                LogDebug("Dial rejected: another client is already dialing (Client " + clientId + ")");
                RejectDial(clientId);
                return;
            }

            if (!ValidateCallGuards(clientId, "start-dial"))
            {
                RejectDial(clientId);
                return;
            }

            _dialingClientId = clientId;
            _dialStartTime = Time.time;
            LogDebug("Dial started by Client " + clientId);
        }

        [ServerRpc(RequireOwnership = false)]
        private void CompleteDialServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;

            if (clientId != _dialingClientId)
            {
                // Yetkisiz tamamlama denemesi (cevireni baskasi degistirmis olabilir, ya da
                // StartDial hic kabul edilmemisti — client bunu bilmiyor olabilir). Sessizce yok say.
                LogDebug("Complete-dial rejected: Client " + clientId + " is not the current dialer.");
                return;
            }

            // KRİTİK (f9a3f1b dersi): client'in "yeterince bekledim" beyanina GUVENME, sunucu
            // kendi zaman damgasindan hesaplasin. -0.05f tolerans: RTT/frame kaymasi icin kucuk pay.
            if (Time.time - _dialStartTime < PhoneDialHoldSeconds - 0.05f)
            {
                LogDebug("Complete-dial rejected: held for less than required time (Client " + clientId + ") — possible early-complete cheat.");
                _dialingClientId = ulong.MaxValue; // kilit takili kalmasin
                return;
            }

            // Basili tutma sirasinda dunya durumu degismis olabilir (kuyruk dolmus, kota
            // bitmis, saat ilerlemis) — TUM guard'lari TEKRAR kontrol et.
            if (!ValidateCallGuards(clientId, "complete-dial"))
            {
                _dialingClientId = ulong.MaxValue;
                RejectDial(clientId); // client'in bari 1'de takili kalmasin
                return;
            }

            _dialingClientId = ulong.MaxValue;
            ExecuteCall(clientId);
        }

        [ServerRpc(RequireOwnership = false)]
        private void CancelDialServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;

            if (clientId == _dialingClientId)
            {
                _dialingClientId = ulong.MaxValue;
                _dialStartTime = -1f;
                LogDebug("Dial cancelled by Client " + clientId);
            }
            // Yetkisiz/gec-gelen iptal cagrisi (baskasi ceviriyor, ya da zaten cozulmus) — sessizce yok say.
        }

        /// <summary>
        /// StartDial veya CompleteDial sunucuda reddedildiginde, isteyen client'a (SADECE ONA)
        /// bildirir — aksi halde client'in local bari StartDial'in kabul edildigini varsayip
        /// dolmaya devam eder, CompleteDial da sessizce reddedilir ve bar 1'de takili kalir.
        /// </summary>
        private void RejectDial(ulong clientId)
        {
            var clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
            };
            DialRejectedClientRpc(clientRpcParams);
        }

        [ClientRpc]
        private void DialRejectedClientRpc(ClientRpcParams rpcParams = default)
        {
            if (!_isDialingLocally) return;

            _isDialingLocally = false;
            phoneWaitBar?.HideBar();
            LogDebug("Dial rejected by server — local bar reset.");
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

                // Oyuncu basili tutarken alandan cikarsa Update() artik HandleInput'u cagirmaz
                // (yukarida _playerInPhoneArea guard'i var) — dial burada elle iptal edilmezse
                // sunucu tarafinda kilit yalnizca +2f timeout savunma agiyla acilirdi.
                if (_isDialingLocally)
                {
                    _isDialingLocally = false;
                    CancelDialServerRpc();
                    phoneWaitBar?.HideBar();
                }
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
