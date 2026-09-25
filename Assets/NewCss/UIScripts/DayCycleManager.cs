using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;
using TMPro;
using NewCss.Quest;
using NewCss.Audio;

namespace NewCss
{
    /// <summary>
    /// Oyun içi gün/gece döngüsünü, haftalık kira ödemelerini ve zaman yönetimini kontrol eder. 
    /// Network senkronizasyonu ile tüm oyuncularda tutarlı zaman akışı sağlar.
    /// </summary>
    public class DayCycleManager : NetworkBehaviour
    {
        #region Singleton

        public static DayCycleManager Instance { get; private set; }

        #endregion

        #region Events

        /// <summary>
        /// Yeni gün başladığında tetiklenir (tüm client'larda)
        /// </summary>
        public static event Action OnNewDay;

        #endregion

        #region Constants

        public const int MAX_DAYS = 16; // Changed from 30 to 16 - made public for GameStateManager
        private const int DYNAMIC_DURATION_START_DAY = 3;
        private const float PERIODIC_CHECK_WINDOW = 0.5f;
        private const string LOG_PREFIX = "[DayCycleManager]";

        // Localization Keys
        private const string LOC_KEY_DAY_FORMAT = "DayFormat";
        private const string LOC_KEY_DAY_OVER_WARNING = "DayOverWarning";

        #endregion

        #region Serialized Fields

        [Header("=== TIME SETTINGS ===")]
        [Tooltip("Bir günün gerçek süre karşılığı (saniye). Perk/buff tarafından RecomputeDayDuration() " +
                 "üzerinden yeniden hesaplanır — doğrudan yazma (bkz. _baseRealDuration).")]
        public float realDurationInSeconds = 200f;

        [SerializeField, Tooltip("3.  günden sonra her gün eklenen ekstra süre (saniye)")]
        private float dailyDurationIncrease = 10f;

        [Tooltip("Oyun içi başlangıç saati")]
        public int startHour = 7;

        [Tooltip("Oyun içi bitiş saati")]
        public int endHour = 18;

        [Header("=== UI REFERENCES ===")]
        [SerializeField, Tooltip("Gün ve saat bilgisini gösteren text")]
        public TextMeshProUGUI dayTimeText;

        [SerializeField, Tooltip("Gün sonu ekranı")]
        public GameObject dayEndScreen;

        #endregion

        #region Serialized Fields - Rent System

        [Header("=== ECONOMY SETTINGS ===")]
        [SerializeField, Tooltip("Tüm ekonomi sabitlerini içeren ScriptableObject")]
        private GameEconomySettings economySettings;

        // ── Backward-compat kısayollar (SO'dan okunur) ──────────────────
        private int   rentIntervalDays    => economySettings != null ? economySettings.rentIntervalDays    : 4;
        private float gracePaymentPercent => economySettings != null ? economySettings.gracePaymentPercent : 0.8f;
        // Taksit perki (grace_plus): taban 1 grace hakkına economySettings.graceExtraUses eklenir.
        private int   GraceUsesAllowed    => 1 + (economySettings != null ? economySettings.graceExtraUses : 0);

        // ── Acil Fren perki (emergency_brake) — server-authoritative, tek kullanımlık ──
        private const float EMERGENCY_BRAKE_PRESTIGE_PENALTY = -2f;

        /// <summary>Acil Fren perki satın alındığında true olur; bir kez tetiklenip tükenir.</summary>
        public bool insuranceAvailable = false;

        #endregion

        #region Network Variables

        private readonly NetworkVariable<float> _networkElapsedTime = new(0f);
        private readonly NetworkVariable<int> _networkCurrentDay = new(1);
        private readonly NetworkVariable<bool> _networkIsDayOver = new(false);
        private readonly NetworkVariable<bool> _networkIsBreakRoomReady = new(false);

        /// <summary>
        /// Sıradaki (henüz ödenmemiş) kira önizlemesi — server-yazar, herkes okur.
        /// DayHudUI gibi client tarafı UI'lar CalculateRent()'i doğrudan çağıramaz
        /// (GetPlayerCount roster'a bağlı, client'ta yanlış sonuç verir).
        /// BİLİNÇLİ OLARAK IsTimeUp'ta DONAR (bkz. Update() throttle bloğu) — yalnız HUD'un
        /// "Bugün kira!" kozmetik gösterimi için, DEĞİŞTİRİLMEDİ (qa bulgusu 2026-09-24: Ö-A
        /// kilidi/etiketi bu NV'yi KULLANMAMALI, bkz. _networkReserveRent).
        /// </summary>
        private readonly NetworkVariable<int> _networkNextRent = new(0);

        /// <summary>
        /// Ö-A kilidi/"Kira fonu" etiketi için (qa fix 2026-09-24, bkz. plans/ekonomi-oa-ob-oc.md):
        /// _networkNextRent'in aksine HİÇ DONMAZ — her throttle turunda (IsTimeUp guard'ı
        /// OLMADAN) ve kira kesildikten hemen sonra (_rentPaymentCount++ sonrası,
        /// TryProcessMoneyCheck) yenilenir. Break-room bekleme penceresinde (kira az önce
        /// kesildi ama NextDay() henüz çağrılmadı) bile GÜNCEL kalır — bug: eski tasarımda
        /// UpgradePanel bu amaç için _networkNextRent'i okuyordu ve o pencerede bayat (küçük)
        /// tutarı gösteriyordu.
        /// </summary>
        private readonly NetworkVariable<int> _networkReserveRent = new(0);

        /// <summary>
        /// Ö-A kilidi/etiketi için sıradaki (henüz ödenmemiş) kira döneminin gün numarası.
        /// _rentPaymentCount'tan hesaplanır (server-only doğru), NV üzerinden herkese yayınlanır.
        /// Son kira (gün MAX_DAYS) ödendiyse 0 (kilit anlamsız — bkz. HasUpcomingRent).
        /// </summary>
        private readonly NetworkVariable<int> _networkReserveRentDay = new(0);

        #endregion

        #region Private Fields

        private int _currentHour;
        private bool _lunchNotified;
        private bool _moneyCheckCompleted;
        private int _rentPaymentCount;   // Kaçıncı kira ödemesi
        // Taksit perki (grace_plus, gameplay-B 2026-09-25): eskiden bool _graceUsed idi (tek grace
        // hakkı). Taksit +1 grace kullanımı ekliyor — bkz. TryProcessMoneyCheck'teki
        // GraceUsesAllowed (taban 1 + economySettings.graceExtraUses).
        private int _graceUsedCount;     // Kaç kez grace kullanıldı
        private bool _gameOverStopProcessing; // Game-over sonrası Update() işleme döngüsünü durdurur (server-only)

        // ── Gün süresi: taban + katkı yeniden hesaplama (tek yazıcı, overtime perk fix) ──
        // realDurationInSeconds artık DOĞRUDAN yazılmaz; _baseRealDuration Awake'te (perk/buff
        // uygulanmadan ÖNCE) bir kez sahne değerinden cache'lenir, sonra tüm değişiklikler
        // RecomputeDayDuration() üzerinden: taban × perk-çarpanı + buff-toplamı.
        private float _baseRealDuration;
        private float _overtimeMultiplier = 1f;      // overtime perki (çarpımsal), level 0 → 1f
        private float _buffDurationBonusSeconds = 0f; // BuffManager DayDuration buff'ları (toplamsal)

        // Periyodik kontrol flag'leri
        private PeriodicCheckState _periodicChecks;

        // UI update throttling
        private float _lastUIUpdateTime;
        private const float UI_UPDATE_INTERVAL = 0.1f; // Update UI max 10 times per second
        private int _lastDisplayedDay = -1;
        private int _lastDisplayedHour = -1;
        private int _lastDisplayedMinute = -1;

        #endregion

        #region Nested Types

        /// <summary>
        /// Gün içi periyodik kontrollerin durumunu tutar
        /// </summary>
        private struct PeriodicCheckState
        {
            public bool DayStartChecked;
            public bool DayMiddleChecked;
            public bool DayEndChecked;

            public void Reset()
            {
                DayStartChecked = false;
                DayMiddleChecked = false;
                DayEndChecked = false;
            }
        }

        #endregion

        #region Public Properties - BACKWARD COMPATIBLE

        /// <summary>
        /// Gün bitmiş mi?
        /// </summary>
        public bool IsDayOver => _networkIsDayOver.Value;

        /// <summary>
        /// Break room'a geçiş için hazır mı?  (lowercase - backward compatibility)
        /// </summary>
        public bool isBreakRoomReady
        {
            get => _networkIsBreakRoomReady.Value;
            set
            {
                if (IsServer)
                {
                    _networkIsBreakRoomReady.Value = value;
                }
                else
                {
                    SetBreakRoomReadyServerRpc(value);
                }
            }
        }

        /// <summary>
        /// Günün başından beri geçen süre - saniye (lowercase - backward compatibility)
        /// </summary>
        public float elapsedTime => _networkElapsedTime.Value;

        /// <summary>
        /// Mevcut gün numarası - 1'den başlar (lowercase - backward compatibility)
        /// </summary>
        public int currentDay => _networkCurrentDay.Value;

        /// <summary>
        /// Oyun içi mevcut saat (7-18 arası)
        /// </summary>
        public int CurrentHour => _currentHour;

        /// <summary>
        /// Mevcut günün toplam süresi (dinamik olarak artar)
        /// </summary>
        public float CurrentDayDuration
        {
            get
            {
                int day = _networkCurrentDay.Value;

                if (day <= DYNAMIC_DURATION_START_DAY)
                {
                    return realDurationInSeconds;
                }

                int extraDays = day - DYNAMIC_DURATION_START_DAY;
                return realDurationInSeconds + (extraDays * dailyDurationIncrease);
            }
        }

        /// <summary>
        /// Günün süresi dolmuş mu?
        /// </summary>
        public bool IsTimeUp => _networkElapsedTime.Value >= CurrentDayDuration;

        /// <summary>
        /// Oyun içi mevcut zaman (float olarak, örn: 7.5 = 07:30)
        /// </summary>
        public float CurrentTime
        {
            get
            {
                float progress = Mathf.Clamp01(_networkElapsedTime.Value / CurrentDayDuration);
                float totalHours = endHour - startHour;
                return startHour + (progress * totalHours);
            }
        }

        /// <summary>
        /// Sıradaki (henüz ödenmemiş) kira önizlemesi. Server periyodik olarak yazar,
        /// herkes NetworkVariable üzerinden okur (bkz. RefreshNextRentPreview).
        /// </summary>
        public int NextRentAmount => _networkNextRent.Value;

        /// <summary>
        /// Kira döngüsü uzunluğu (gün) — economySettings.rentIntervalDays, fallback 4.
        /// </summary>
        public int RentIntervalDays => rentIntervalDays;

        /// <summary>
        /// Mevcut kira döngüsünde kaçıncı gündeyiz (1..RentIntervalDays).
        /// </summary>
        public int DayInRentCycle => RentIntervalDays <= 0 ? 1 : ((currentDay - 1) % RentIntervalDays) + 1;

        /// <summary>
        /// Bugün kira günü mü? (currentDay % RentIntervalDays == 0)
        /// </summary>
        public bool IsRentDay => RentIntervalDays > 0 && currentDay % RentIntervalDays == 0;

        /// <summary>
        /// qa fix (2026-09-24, bkz. _networkReserveRentDay yorumu): eski sürüm currentDay'den
        /// hesaplıyordu — break-room bekleme penceresinde (kira az önce kesildi, NextDay() henüz
        /// çağrılmadı, currentDay hâlâ ESKİ günde) BAYAT (bir dönem geride) gün döndürüyordu.
        /// Artık server _rentPaymentCount'tan hesaplanan, NV üzerinden yayınlanan güncel değeri
        /// okur — tüm client'larda doğru. Son kira (gün MAX_DAYS) ödendiyse 0 (kilit anlamsız).
        /// UpgradePanel "Kira fonu: X (gün N)" satırında kullanır.
        /// </summary>
        public int NextRentDay => _networkReserveRentDay.Value;

        /// <summary>
        /// Ö-A kilidi/etiketi için DONMAYAN kira tutarı — bkz. _networkReserveRent yorumu.
        /// UpgradePanel client-side iyimser kontrol (ValidatePurchase/OnReroll) ve "Kira fonu"
        /// etiketi burayı okur. NextRentAmount'tan FARKLI: o HUD için bilinçli donuyor, bu
        /// donmuyor. 0 = henüz yayınlanmadı VEYA son kira zaten ödendi (kilit anlamsız).
        /// </summary>
        public int ReserveRentAmount => _networkReserveRent.Value;

        /// <summary>
        /// Ö-A kilidinin SERVER-AUTHORITATIVE kontrolü için (UpgradePanel.WouldViolateRentReserve,
        /// yalnız PurchaseUpgradeServerRpc/RerollServerRpc içinden — ikisi de zaten yalnız
        /// server'da çalışır). _rentPaymentCount NETWORKED DEĞİL (yalnız server'da doğru değer
        /// taşır) — bu yüzden ReserveRentAmount/_networkReserveRent NV'sine DEĞİL, doğrudan
        /// CalculateRent(false)'a bakar: throttle/donma yok, her çağrıda güncel. Client'ta
        /// (IsServer false) çağırmak YANLIŞ sonuç verir — client kodu ReserveRentAmount'ı okumalı.
        /// </summary>
        public int CurrentReserveRent => HasUpcomingRent ? CalculateRent(false) : 0;

        /// <summary>
        /// Son kira (gün MAX_DAYS) zaten ödendiyse false — o noktadan sonra "sıradaki kira"
        /// kavramı anlamsız (oyun gün MAX_DAYS'te bitiyor), kilit/etiket 0/gizli olmalı.
        /// </summary>
        private bool HasUpcomingRent => RentIntervalDays > 0 && (_rentPaymentCount * RentIntervalDays) < MAX_DAYS;

        /// <summary>
        /// Günün ne kadarının geçtiği, 0..1 arası (UI pasta bar için).
        /// </summary>
        public float DayProgress01 => CurrentDayDuration <= 0f ? 0f : Mathf.Clamp01(_networkElapsedTime.Value / CurrentDayDuration);

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeSingleton();
            if (economySettings == null)
            {
                economySettings = Resources.Load<GameEconomySettings>("EkonomiAyarlari");
            }

            // Taban süre perk/buff uygulanmadan ÖNCE cache'lenir — aksi halde şişmiş bir değeri
            // taban sanıp üstüne tekrar tekrar perk/buff eklemiş oluruz (bkz. RecomputeDayDuration).
            _baseRealDuration = realDurationInSeconds;
        }

        private void OnDestroy()
        {
            CleanupSingleton();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            LocalizationSettings.SelectedLocaleChanged += HandleLocaleChanged;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            LocalizationSettings.SelectedLocaleChanged -= HandleLocaleChanged;
        }

        private void Start()
        {
            Debug.Log($"{LOG_PREFIX} Start - IsServer: {IsServer}");

            if (IsServer)
            {
                ResetDayCycle();
                Debug.Log($"{LOG_PREFIX} Server initialized day cycle variables");
            }

            UpdateUI();
            SetDayEndScreenActive(false);
        }

        private void Update()
        {
            // Throttle UI updates - only update when values change or interval passed
            if (Time.time - _lastUIUpdateTime >= UI_UPDATE_INTERVAL)
            {
                UpdateUI();
                _lastUIUpdateTime = Time.time;

                // IsTimeUp true olduğu andan itibaren ProcessDayEnd() → TryProcessMoneyCheck()
                // o günün kirasını keser ve _rentPaymentCount++ yapar (bkz. ProcessDayEnd
                // ":599 `if (_networkElapsedTime.Value < CurrentDayDuration)` erken-çıkışı,
                // yani IsTimeUp==true olunca money-check'e girilir). Bu andan yeni gün
                // başlayana (NextDay()) kadar preview'i DONDURUYORUZ; aksi halde HUD kira
                // kesildikten hemen sonra _rentPaymentCount artmış olduğu için bir SONRAKİ
                // döngünün (x1.20) tutarını "Bugün kira!" etiketiyle gösterir.
                if (IsSpawned && IsServer && !_gameOverStopProcessing && !IsTimeUp)
                {
                    RefreshNextRentPreview();
                }

                // qa fix (2026-09-24): Ö-A kilidi/etiketi İÇİN yukarıdakinin aksine IsTimeUp
                // guard'ı YOK — HUD'un bilinçli donma davranışına DOKUNULMADI (yukarıdaki blok
                // aynı kaldı), bu SADECE reserve-lock/"Kira fonu" etiketinin ayrı, donmayan
                // kaynağını (_networkReserveRent/_networkReserveRentDay) günceller.
                if (IsSpawned && IsServer && !_gameOverStopProcessing)
                {
                    RefreshReserveRentPreview();
                }
            }

            if (!IsSpawned || !IsServer || _networkIsDayOver.Value || _gameOverStopProcessing)
            {
                return;
            }

            AdvanceTime();
            ProcessPeriodicChecks();
            ProcessDayEnd();
        }

        #endregion

        #region Network Lifecycle

        public override void OnNetworkSpawn()
        {
            Debug.Log($"{LOG_PREFIX} OnNetworkSpawn - IsServer: {IsServer}, IsHost: {NetworkManager.Singleton.IsHost}");

            base.OnNetworkSpawn();

            if (IsServer)
            {
                InitializeServerState();
            }

            SubscribeToNetworkEvents();
            ResetDayCycle();

            if (IsServer)
            {
                // ResetDayCycle() → ResetNetworkVariables()/ResetLocalState() _rentPaymentCount'u
                // 0'a, günü 1'e sıfırlıyor; önizleme hesaplamasını BUNDAN SONRA yapmalıyız, yoksa
                // sıfırlanmadan önceki (önceki oturumdan sızmış) sayaçla yanlış tutar yayınlanır.
                // Late-join / ilk spawn'da kira önizlemesi Update() ilk throttle turunu
                // beklemeden doğru değerle yayınlansın (yeni bağlanan client 0 görmesin).
                RefreshNextRentPreview();
                RefreshReserveRentPreview();
            }

            Debug.Log($"{LOG_PREFIX} Network spawn completed");
        }

        public override void OnNetworkDespawn()
        {
            UnsubscribeFromNetworkEvents();
            base.OnNetworkDespawn();
        }

        #endregion

        #region Initialization

        private void InitializeSingleton()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Debug.LogWarning($"{LOG_PREFIX} Duplicate instance detected, destroying.. .");
                Destroy(gameObject);
            }
        }

        private void CleanupSingleton()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void InitializeServerState()
        {
            Debug.Log($"{LOG_PREFIX} Server state initialized successfully");
        }

        private void SubscribeToNetworkEvents()
        {
            _networkElapsedTime.OnValueChanged += HandleElapsedTimeChanged;
            _networkCurrentDay.OnValueChanged += HandleCurrentDayChanged;
            _networkIsDayOver.OnValueChanged += HandleDayOverChanged;
            _networkIsBreakRoomReady.OnValueChanged += HandleBreakRoomReadyChanged;
        }

        private void UnsubscribeFromNetworkEvents()
        {
            _networkElapsedTime.OnValueChanged -= HandleElapsedTimeChanged;
            _networkCurrentDay.OnValueChanged -= HandleCurrentDayChanged;
            _networkIsDayOver.OnValueChanged -= HandleDayOverChanged;
            _networkIsBreakRoomReady.OnValueChanged -= HandleBreakRoomReadyChanged;
        }

        #endregion

        #region Day Cycle Core Logic

        /// <summary>
        /// Gün döngüsünü sıfırlar (yeni oyun başlatırken)
        /// </summary>
        public void ResetDayCycle()
        {
            Debug.Log($"{LOG_PREFIX} === RESETTING DAY CYCLE ===");

            if (IsServer)
            {
                ResetNetworkVariables();
            }

            ResetLocalState();
            UpdateUI();
            SetDayEndScreenActive(false);

            Debug.Log($"{LOG_PREFIX} Day cycle reset completed");
        }

        private void ResetNetworkVariables()
        {
            _networkElapsedTime.Value = 0f;
            _networkCurrentDay.Value = 1;
            _networkIsDayOver.Value = false;
            _networkIsBreakRoomReady.Value = false;
        }

        private void ResetLocalState()
        {
            _currentHour = startHour;
            _lunchNotified = false;
            _moneyCheckCompleted = false;
            _periodicChecks.Reset();

            // Kira/sigorta state'i önceki oturumdan sızmasın (yeni oyun / menüye dönüş / replay).
            _rentPaymentCount = 0;
            _graceUsedCount = 0;
            insuranceAvailable = false;
            _gameOverStopProcessing = false;
        }

        private void AdvanceTime()
        {
            _networkElapsedTime.Value += Time.deltaTime;
        }

        /// <summary>
        /// Zamanı belirli bir miktar (dakika cinsinden) ileri sarar
        /// </summary>
        public void SkipTime(float minutesToSkip)
        {
            if (!IsServer) return;

            // Dakikayı saniyeye çevir
            // realDurationInSeconds = tüm günün (startHour -> endHour) saniye karşılığı
            float totalGameHours = endHour - startHour;
            float secondsPerGameHour = realDurationInSeconds / totalGameHours;
            float secondsPerGameMinute = secondsPerGameHour / 60f;
            
            float skipAmountInSeconds = minutesToSkip * secondsPerGameMinute;

            _networkElapsedTime.Value += skipAmountInSeconds;

            Debug.Log($"{LOG_PREFIX} Time Skipped: {minutesToSkip} minutes ({skipAmountInSeconds:F2} seconds)");
        }

        /// <summary>
        /// SkipTime(minutesToSkip) çağrılsaydı CurrentTime kaç olurdu? Zamanı DEĞİŞTİRMEZ.
        /// Telefon (PhoneCallManager) bunu, atlamanın günü müşteri-çıkış saatinin ötesine
        /// sıçratıp sıçratmayacağını çağrıyı KABUL ETMEDEN ÖNCE anlamak için kullanır
        /// (QA bulgusu 2026-08-29: aksi halde telefonla çağrılan müşteri, aynı karede
        /// gün-sonu kesimine yakalanıp servis edilemeden ceza üretiyordu).
        /// SkipTime ile birebir aynı aritmetiği paylaşır — biri değişirse diğeri de değişmeli.
        /// </summary>
        public float PredictTimeAfterSkip(float minutesToSkip)
        {
            float totalGameHours = endHour - startHour;
            float secondsPerGameMinute = (realDurationInSeconds / totalGameHours) / 60f;
            float projectedElapsed = _networkElapsedTime.Value + (minutesToSkip * secondsPerGameMinute);

            float progress = Mathf.Clamp01(projectedElapsed / CurrentDayDuration);
            return startHour + (progress * totalGameHours);
        }

        /// <summary>
        /// PlateUp erken gün-bitişi (plan §C, 2026-08-29): günün müşteri kotası tükenip son
        /// müşteri de çıktığında (bkz. CustomerManager.CheckEarlyDayCompletion, kısa bir grace
        /// süresinin ardından) çağrılır. Zamanı doğrudan gün sonuna sarar — ProcessDayEnd()'in
        /// tek koşulu (elapsedTime >= CurrentDayDuration) doğal yoldan tetiklenir; kira kontrolü/
        /// break room/gün sonu ekranı/IsTimeUp exploit guard'ı BU METOTLA DEĞİŞMEDEN çalışır.
        /// İkinci bir gün-bitiş yolu açmaz, SkipTime ile aynı deseni izler.
        /// </summary>
        public void FastForwardToEndOfDay()
        {
            if (!IsServer) return;

            _networkElapsedTime.Value = CurrentDayDuration;

            Debug.Log($"{LOG_PREFIX} Fast-forwarded to end of day (customer quota completed early).");
        }

        /// <summary>
        /// Taban süre + perk çarpanı + buff toplamını tek noktadan yeniden hesaplar.
        /// realDurationInSeconds'a yazan TEK yer burasıdır (overtime perki ve BuffManager
        /// DayDuration buff'ı dahil — ikisi de bu metodu çağırır, doğrudan alana yazmaz).
        /// </summary>
        private void RecomputeDayDuration()
        {
            realDurationInSeconds = _baseRealDuration * _overtimeMultiplier + _buffDurationBonusSeconds;
        }

        /// <summary>
        /// Mesai Saati (overtime) perki için: level 0'da 1f'e (etkisiz) döner — idempotent,
        /// perk yeniden uygulansa/kaldırılsa bile süre sürüklenmez.
        /// </summary>
        public void SetOvertimeMultiplier(float multiplier)
        {
            _overtimeMultiplier = multiplier;
            RecomputeDayDuration();
        }

        /// <summary>
        /// BuffManager DayDuration buff'ları için toplamsal katkı (kaldırılırken negatif amount ile
        /// çağrılır). Doğrudan realDurationInSeconds'a yazmanın yerini alır.
        /// </summary>
        public void AddBuffDurationBonus(float amount)
        {
            _buffDurationBonusSeconds += amount;
            RecomputeDayDuration();
        }

        #endregion

        #region Periodic Player Checks

        private void ProcessPeriodicChecks()
        {
            float elapsed = _networkElapsedTime.Value;
            float duration = CurrentDayDuration;

            // Gün başlangıcı kontrolü
            if (!_periodicChecks.DayStartChecked && elapsed <= PERIODIC_CHECK_WINDOW)
            {
                PerformPlayerCheck("Gün başladı");
                _periodicChecks.DayStartChecked = true;
            }

            // Gün ortası kontrolü
            float halfDuration = duration * 0.5f;
            if (!_periodicChecks.DayMiddleChecked &&
                elapsed >= halfDuration &&
                elapsed < halfDuration + PERIODIC_CHECK_WINDOW)
            {
                PerformPlayerCheck("Gün ortası");
                _periodicChecks.DayMiddleChecked = true;
            }

            // Gün sonu kontrolü
            if (!_periodicChecks.DayEndChecked && elapsed >= duration)
            {
                PerformPlayerCheck("Gün bitti");
                _periodicChecks.DayEndChecked = true;
            }
        }

        private void PerformPlayerCheck(string checkPoint)
        {
            Debug.Log($"{LOG_PREFIX} ⏰ {checkPoint} - Oyuncu sayısı kontrol ediliyor");

            if (BreakRoomManager.Instance != null)
            {
                BreakRoomManager.Instance.CheckAndUpdateLobbyPlayers();
            }
            else
            {
                Debug.LogWarning($"{LOG_PREFIX} BreakRoomManager. Instance bulunamadı!");
            }
        }

        #endregion

        #region Day End Processing

        private void ProcessDayEnd()
        {
            // Gün henüz bitmedi
            if (_networkElapsedTime.Value < CurrentDayDuration)
            {
                _moneyCheckCompleted = false;
                return;
            }

            // Para kontrolü yap (kira sistemi)
            if (!_moneyCheckCompleted)
            {
                if (!TryProcessMoneyCheck())
                {
                    return;
                }
            }

            // Break room hazır değil
            if (!_networkIsBreakRoomReady.Value)
            {
                UpdateWarningTextClientRpc();
                return;
            }

            // Günü bitir
            _networkIsDayOver.Value = true;
            ShowDayEndScreenClientRpc();
        }

        /// <summary>
        /// Kira ödeme kontrolü — her 4 günde bir tetiklenir.
        /// İlk kirada grace period: para yetmezse eldekinin %80'i alınır — ancak
        /// economySettings.graceDisabled true ise (leveraged_rent/all_in perki) bu dal atlanır.
        /// 2+ kirada (veya grace iptal edilmişse) Acil Fren varsa o, yoksa Game Over.
        /// </summary>
        private bool TryProcessMoneyCheck()
        {
            if (MoneySystem.Instance == null)
            {
                _moneyCheckCompleted = true;
                return true;
            }

            int currentDay = _networkCurrentDay.Value;

            // Kira günü değilse geç
            if (currentDay % rentIntervalDays != 0)
            {
                _moneyCheckCompleted = true;
                return true;
            }

            int rentAmount = CalculateRent();
            int currentMoney = MoneySystem.Instance.CurrentMoney;

            Debug.Log($"{LOG_PREFIX} === RENT DAY {currentDay} === Rent: {rentAmount}, Money: {currentMoney}");

            if (currentMoney >= rentAmount)
            {
                // Tam ödeme
                MoneySystem.Instance.SpendMoney(rentAmount);
                _rentPaymentCount++;
                Debug.Log($"{LOG_PREFIX} Rent paid in full: {rentAmount}");
            }
            else if (_graceUsedCount < GraceUsesAllowed && !(economySettings != null && economySettings.graceDisabled))
            {
                // Kira affı (grace) — eldeki paranın gracePaymentPercent'i alınır, ödenmiş sayılır.
                // Taban 1 kullanım; Taksit perki (grace_plus) +1 hak ekler (GraceUsesAllowed) VE
                // gracePaymentPercent'i 0.8→0.7'ye düşürür (PerkEffect.ApplyGracePlus).
                // Ö-C fix (2026-09-24): leveraged_rent/all_in perki graceDisabled=true yazar —
                // o perklerin bedeli grace'in TAMAMEN İPTALİ olduğu için (bkz. PerkEffect.
                // ApplyLeveragedRent/ApplyAllIn yorumu) bu dal hiç çalıştırılmaz; kasa yetmezse
                // doğrudan Acil Fren'e (varsa) ya da iflasa düşer.
                float gracePct   = economySettings != null ? economySettings.gracePaymentPercent : 0.8f;
                int graceAmount = Mathf.RoundToInt(currentMoney * gracePct);
                MoneySystem.Instance.SpendMoney(graceAmount);
                _graceUsedCount++;
                _rentPaymentCount++;
                Debug.Log($"{LOG_PREFIX} Grace period used ({_graceUsedCount}/{GraceUsesAllowed}). Took {graceAmount} ({gracePct * 100}% of {currentMoney}). Needed: {rentAmount}");
            }
            else if (insuranceAvailable)
            {
                // Acil Fren perki: iflası bir kez önler, tükenir. O günün geliri sıfırlanmış sayılır
                // (para zaten yetersizdi) ve prestij cezası uygulanır — bedelsiz değil.
                insuranceAvailable = false;
                if (PrestigeManager.Instance != null)
                {
                    PrestigeManager.Instance.ModifyPrestige(EMERGENCY_BRAKE_PRESTIGE_PENALTY);
                }
                _rentPaymentCount++;
                Debug.Log($"{LOG_PREFIX} Emergency Brake perk consumed. Rent {rentAmount} waived, prestige penalty {EMERGENCY_BRAKE_PRESTIGE_PENALTY} applied.");
            }
            else
            {
                // 2. kez ödeyememe → Game Over
                Debug.Log($"{LOG_PREFIX} Cannot pay rent: {rentAmount}. Available: {currentMoney}. GAME OVER!");
                // Game-over döngüsünü durdur: Update() sadece _gameOverStopProcessing==true iken
                // ProcessDayEnd çağrısını keser. Bu set edilmezse her server frame'de buraya
                // tekrar girilip TriggerLose() + log spam olur. _networkIsDayOver BİLEREK
                // set edilmiyor: onu true yapmak HandleDayOverChanged'i tetikleyip yanlış
                // "gün bitti" ekranını açar ve NextDayServerRpc guard'ını bozar.
                _gameOverStopProcessing = true;
                GameStateManager.Instance?.TriggerLose();
                return false;
            }

            // qa fix (2026-09-24): kira kesildi (üç başarı dalından biri, _rentPaymentCount
            // artmış) — Ö-A kilidi/etiketi HEMEN bir sonraki döneme atlasın, throttle turunu
            // beklemesin (break-room bekleme penceresinde bayat gösterime karşı, bkz.
            // _networkReserveRent yorumu).
            RefreshReserveRentPreview();

            _moneyCheckCompleted = true;
            return true;
        }

        /// <summary>
        /// Kira hesaplama: TemelKira × rentGrowthMultiplier^dönem × rentScaledMultiplier
        /// </summary>
        /// <param name="log">true ise sonucu Debug.Log'a basar. HUD önizlemesi gibi yüksek
        /// frekansta (throttle'lı da olsa) çağrılan yerler log=false vermeli — konsol spam'i olmasın.</param>
        private int CalculateRent(bool log = true)
        {
            int playerCount = GetPlayerCount();

            float finalRent;
            if (economySettings != null)
            {
                finalRent = economySettings.CalculateRent(playerCount, _rentPaymentCount);
            }
            else
            {
                // Fallback: economySettings atanmamışsa eski sabit değerler
                // Senkron: asset {290,650,1140,1630} / g=1.20 ile hizalı (bkz.
                // .claude/agent-memory/economist/economy_full_balance_round10_2026-08-30.md) —
                // asset yüklenemezse sessizce eski ekonomiye düşmesin.
                if (log)
                {
                    Debug.LogWarning($"{LOG_PREFIX} economySettings atanmamış! Fallback değerler kullanılıyor.");
                }
                int baseRent    = playerCount == 1 ? 290 : playerCount == 2 ? 650 : playerCount == 3 ? 1140 : 1630;
                float scaled    = baseRent * Mathf.Pow(1.20f, _rentPaymentCount);
                finalRent       = scaled;
            }

            int result = Mathf.RoundToInt(finalRent);
            if (log)
            {
                Debug.Log($"{LOG_PREFIX} Rent calc result: {result} (Players: {playerCount}, Cycle: {_rentPaymentCount})");
            }
            return result;
        }

        /// <summary>
        /// Dış sistemler (örn. EventEffectManager/FESTIVAL DAY) için o anki kira döngüsüne göre
        /// hesaplanan güncel kira miktarını döndürür (henüz ödenmemiş/gerçekleşmemiş olsa bile).
        /// Server-authoritative değildir salt-okunur bir hesaplamadır; CalculateRent() ile aynı formülü kullanır.
        /// </summary>
        public int GetCurrentRentAmount() => CalculateRent();

        /// <summary>
        /// Server'da periyodik (throttle'lı) veya spawn anında çağrılır; _networkNextRent'i
        /// yalnız değer değiştiğinde ağa yazar (gereksiz NetworkVariable trafiği yaratmasın).
        /// HUD (DayHudUI) bu değeri NextRentAmount üzerinden client'ta okur.
        /// </summary>
        private void RefreshNextRentPreview()
        {
            int r = CalculateRent(false);
            if (r != _networkNextRent.Value)
            {
                _networkNextRent.Value = r;
            }
        }

        /// <summary>
        /// qa fix (2026-09-24): Ö-A kilidi/"Kira fonu" etiketi için _networkReserveRent/
        /// _networkReserveRentDay'i yeniler. RefreshNextRentPreview'dan FARKLI olarak:
        /// (1) Update() throttle'ında IsTimeUp guard'ı OLMADAN çağrılır (HUD'un bilinçli donma
        /// davranışı burada İSTENMİYOR), (2) TryProcessMoneyCheck'te _rentPaymentCount++
        /// olduğu anda da çağrılır (kira kesilir kesilmez etiket bir SONRAKİ döneme atlasın).
        /// Son kira zaten ödendiyse (HasUpcomingRent false) ikisi de 0 — kilit/etiket devre dışı.
        /// </summary>
        private void RefreshReserveRentPreview()
        {
            int day = HasUpcomingRent ? (_rentPaymentCount + 1) * RentIntervalDays : 0;
            int amount = HasUpcomingRent ? CalculateRent(false) : 0;

            if (day != _networkReserveRentDay.Value)
            {
                _networkReserveRentDay.Value = day;
            }
            if (amount != _networkReserveRent.Value)
            {
                _networkReserveRent.Value = amount;
            }
        }

        /// <summary>
        /// Kira hesabı için oyuncu sayısını döndürür.
        /// plans/oyuncu-sayisi-kilidi.md §B (2026-09-24): tek doğruluk kaynağı artık
        /// DifficultyManager.PlayerCount (koşu-kilitli, disconnect'te düşmez — bkz.
        /// DifficultyManager.HandlePlayerConnectionChanged). Roster ve ConnectedClientsList sayısı
        /// çık-gir/disconnect ile düşebildiği için kira artık onlara bakmıyor; yalnızca
        /// DifficultyManager hiç yoksa (ör. test sahnesi) eski roster/ConnectedClients fallback'i
        /// devreye girer. Break Room / Win-Lose isim listesi roster'ı kullanmaya devam eder — bu
        /// metod yalnız kira hesabı içindir.
        /// </summary>
        private int GetPlayerCount()
        {
            if (DifficultyManager.Instance != null)
            {
                return DifficultyManager.Instance.PlayerCount;
            }

            if (GameStateManager.Instance != null && GameStateManager.Instance.RosterPlayerCount > 0)
            {
                return GameStateManager.Instance.RosterPlayerCount;
            }

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                return NetworkManager.Singleton.ConnectedClientsList.Count;
            }
            return 1;
        }

        #endregion

        #region Next Day Logic

        /// <summary>
        /// Bir sonraki güne geçiş yapar (public API)
        /// </summary>
        public void CallNextDay()
        {
            if (IsServer)
            {
                NextDay();
            }
            else
            {
                NextDayServerRpc();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void NextDayServerRpc()
        {
            // Exploit guard: gun gercekten server tarafinda bitmis olmali
            // (kira/para kontrolu + break room hazir -> _networkIsDayOver true)
            // yoksa herhangi bir client bu RPC'yi cagirip elapsedTime'i sifirlayarak
            // o gunun kira kontrolunu hic calistirmadan gunu atlayabilir.
            if (!_networkIsDayOver.Value || !_networkIsBreakRoomReady.Value)
            {
                Debug.LogWarning($"{LOG_PREFIX} NextDayServerRpc reddedildi: gun henuz bitmedi " +
                                  $"(IsDayOver={_networkIsDayOver.Value}, IsBreakRoomReady={_networkIsBreakRoomReady.Value})");
                return;
            }

            NextDay();
        }

        /// <summary>
        /// Bir sonraki güne geçiş yapar (backward compatibility için public)
        /// </summary>
        public void NextDay()
        {
            if (!IsServer) return;

            // Kazanma kontrolü tüm NetworkVariable mutasyonlarından ÖNCE: eski akış günü
            // ilerletip süreyi sıfırladıktan SONRA kazanmayı yakalıyordu — sayaç/saat
            // değişiklikleri tüm peer'lara replike oluyor ve currentDay'i poll'layan
            // sistemler (örn. GarageDoorController.ResetDailyFlags) zafer ekranında
            // tepki veriyordu. Ulaşılacak gün değeri CheckWinCondition'a parametre
            // geçilir (tetiklenme eşiği/zamanlaması değişmedi); kazanma tetiklenirse
            // hiçbir gün-durumu yazılmadan çıkılır. _gameOverStopProcessing kaybetme
            // yolundaki gibi Update()'in tekrar girmesini keser.
            int upcomingDay = _networkCurrentDay.Value + 1;
            if (upcomingDay >= MAX_DAYS)
            {
                Debug.Log($"{LOG_PREFIX} Completed day {MAX_DAYS}! Checking win condition...");
                CheckWinCondition(upcomingDay);

                if (GameStateManager.Instance != null && GameStateManager.Instance.GameEnded)
                {
                    _gameOverStopProcessing = true;

                    // Gün 16 settlement (Faz4 §B.9): bu dal OnNewDay'i hiç tetiklemediği için
                    // QuestManager.HandleNewDay üzerinden çalışan gün-sonu settlement de hiç
                    // çalışmıyordu - son günün kabul edilmiş görevi cezasız/ödülsüz kalıyordu.
                    // Win yolunda ayrıca çağırıp son günün kabul edilmiş görevlerini kapatıyoruz.
                    if (QuestManager.Instance != null)
                    {
                        QuestManager.Instance.SettleAcceptedQuestsOnGameEnd();
                    }

                    // Gün-sonu paneli normal akışta metodun sonundaki HideDayEndScreenClientRpc
                    // ile kapanır; win erken-çıkışı o satıra ulaşmadığından panel tüm
                    // client'larda zafer ekranının altında açık kalıyordu. Yalnızca panel
                    // gizlenir — OnNewDay/TriggerNewDayEventClientRpc gibi "yeni gün" olayları
                    // bilinçli olarak tetiklenmez.
                    HideDayEndScreenClientRpc();

                    Debug.Log($"{LOG_PREFIX} Game won — no day-state mutation, no Day {upcomingDay} start.");
                    return;
                }
            }

            // Zaman ve gün sayacını güncelle
            _networkElapsedTime.Value = 0f;
            _networkCurrentDay.Value++;

            // Periyodik kontrolleri sıfırla
            _periodicChecks.Reset();

            Debug.Log($"{LOG_PREFIX} Day {_networkCurrentDay.Value} started - Duration: {CurrentDayDuration} seconds");

            // Gün durumlarını sıfırla
            _networkIsDayOver.Value = false;
            _networkIsBreakRoomReady.Value = false;

            // Update()'teki throttle IsTimeUp==true olduğundan beri (önceki günün kira kesimi
            // sırasında) preview'i dondurmuştu; yeni gün + güncel _rentPaymentCount ile bir kez
            // burada tazeliyoruz. NextDay() zaten yalnız IsServer'da çalışır (yukarıdaki erken çıkış).
            RefreshNextRentPreview();
            RefreshReserveRentPreview();

            // Event'leri tetikle
            OnNewDay?.Invoke();
            TriggerNewDayEventClientRpc();

            // UI güncelle
            UpdateUI();
            HideDayEndScreenClientRpc();

            // Break room oyuncu sayısını güncelle
            UpdateBreakRoomPlayerCount();
        }

        private void CheckWinCondition(int completedDay)
        {
            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.CheckWinCondition(completedDay);
            }
        }

        private void UpdateBreakRoomPlayerCount()
        {
            var breakRoomManager = FindObjectOfType<BreakRoomManager>();

            if (breakRoomManager != null)
            {
                // requiredPlayers artık TEK yoldan yazılıyor: BreakRoomManager.UpdateLobbyPlayers
                // (GameStateManager.OnRosterChanged event'i tarafından tetiklenir). Burada ikinci
                // bir doğrudan yazma yoluyla sessiz tutarsızlık riski almamak için sadece
                // roster/lobi senkronizasyonunu tetikliyoruz.
                breakRoomManager.CheckAndUpdateLobbyPlayers();
                Debug.Log($"{LOG_PREFIX} BreakRoomManager roster senkronizasyonu tetiklendi, requiredPlayers={breakRoomManager.requiredPlayers}");
            }
        }

        #endregion

        #region UI Management

        private void UpdateUI()
        {
            UpdateDayTimeUI();
        }

        /// <summary>
        /// UI'ı günceller (backward compatibility için public olarak da erişilebilir)
        /// </summary>
        private void UpdateDayTimeUI()
        {
            if (dayTimeText == null) return;

            // Only calculate if values might have changed
            if (_lastDisplayedDay == _networkCurrentDay.Value)
            {
                // Day hasn't changed, check if we need to update time
                var timeInfo = CalculateDisplayTime();
                
                if (_lastDisplayedHour == timeInfo.hour &&
                    _lastDisplayedMinute == timeInfo.minute)
                {
                    return; // No changes, skip update
                }

                _lastDisplayedHour = timeInfo.hour;
                _lastDisplayedMinute = timeInfo.minute;
                dayTimeText.text = FormatTimeDisplay(timeInfo);
            }
            else
            {
                // Day changed, update everything
                var timeInfo = CalculateDisplayTime();
                _lastDisplayedDay = _networkCurrentDay.Value;
                _lastDisplayedHour = timeInfo.hour;
                _lastDisplayedMinute = timeInfo.minute;
                dayTimeText.text = FormatTimeDisplay(timeInfo);
            }
        }

        private (int hour, int minute) CalculateDisplayTime()
        {
            float progress = Mathf.Clamp01(_networkElapsedTime.Value / CurrentDayDuration);
            float totalMinutes = (endHour - startHour) * 60f;
            float currentMinutes = progress * totalMinutes;

            int hour = startHour + Mathf.FloorToInt(currentMinutes / 60f);
            int minute = Mathf.FloorToInt(currentMinutes % 60f);

            hour = Mathf.Clamp(hour, startHour, endHour);
            _currentHour = hour;

            return (hour, minute);
        }

        private const string DAY_LABEL_KEY = "DayLabel";

        private string FormatTimeDisplay((int hour, int minute) timeInfo)
        {
            // Only display the day number as "Day N"
            int day = _networkCurrentDay.Value;

            // GetLocalizedString anahtar bulunamazsa key'in kendisini ("DayLabel") döndürür —
            // tablo satırı henüz eklenmemişse literal "DayLabel" ekrana sızmasın diye guard'lıyoruz.
            string template = LocalizationHelper.GetLocalizedString(DAY_LABEL_KEY);
            if (string.IsNullOrEmpty(template) || template == DAY_LABEL_KEY)
            {
                return $"Day {day}";
            }

            try
            {
                return string.Format(template, day);
            }
            catch (FormatException e)
            {
                Debug.LogWarning($"{LOG_PREFIX} DayLabel format error: {e.Message}");
                return $"Day {day}";
            }
        }

        private void SetDayEndScreenActive(bool active)
        {
            if (dayEndScreen != null)
            {
                dayEndScreen.SetActive(active);
            }
        }

        #endregion

        #region Server RPCs

        [ServerRpc(RequireOwnership = false)]
        private void SetBreakRoomReadyServerRpc(bool ready)
        {
            // N3 server-auth: client'in "herkes break room'da" iddiasina KORU KORUNE guvenme.
            // Bu RPC server'da calisir; ready=true yalnizca server kendi otoriter presence
            // verisiyle (BreakRoomManager.IsReady = server-side AreAllPlayersInRoom) + sure-bitti
            // kosulunu dogrularsa kabul edilir. Aksi halde lag-desync veya hileli client bypass'i
            // reddedilir (gunu erken atlatamaz).
            if (ready)
            {
                var breakRoom = BreakRoomManager.Instance;
                if (breakRoom == null || !breakRoom.IsReady || !IsTimeUp)
                {
                    Debug.LogWarning($"{LOG_PREFIX} SetBreakRoomReadyServerRpc reddedildi: " +
                                     $"server presence dogrulamasi basarisiz " +
                                     $"(BreakRoom={(breakRoom != null ? breakRoom.IsReady.ToString() : "null")}, IsTimeUp={IsTimeUp}).");
                    return;
                }
            }

            _networkIsBreakRoomReady.Value = ready;
        }

        #endregion

        #region Client RPCs

        [ClientRpc]
        private void UpdateWarningTextClientRpc()
        {
            if (dayTimeText != null)
            {
                dayTimeText.text = LocalizationHelper.GetLocalizedString(LOC_KEY_DAY_OVER_WARNING);
            }
        }

        [ClientRpc]
        private void ShowDayEndScreenClientRpc()
        {
            SetDayEndScreenActive(true);
            // Faz B (plans/ses-tasarimi.md §3): mevcut ClientRpc'ye tek satır — yeni RPC açmadan
            // tüm client'larda yerel çalar (bu metot zaten her peer'de tetikleniyor).
            SfxBus.Play(SfxId.DayEnd);
        }

        [ClientRpc]
        private void HideDayEndScreenClientRpc()
        {
            SetDayEndScreenActive(false);
        }

        [ClientRpc]
        private void TriggerNewDayEventClientRpc()
        {
            OnNewDay?.Invoke();
        }

        #endregion

        #region Network Event Handlers

        private void HandleElapsedTimeChanged(float previousValue, float newValue)
        {
            UpdateUI();
        }

        private void HandleCurrentDayChanged(int previousValue, int newValue)
        {
            UpdateUI();

            // Faz B (plans/ses-tasarimi.md §3): kira günü yaklaşma uyarısı. NetworkVariable
            // OnValueChanged zaten her peer'de (host dahil, TEK sefer) tetiklenir — OnNewDay
            // static event'inin aksine (server'da doğrudan + ClientRpc ile host'ta ÇİFT tetiklenir,
            // bkz. NextDay() yorumu), burada çifte çalma riski yok. Yeni gün BİR kira günüyse
            // (day % rentIntervalDays == 0) o günün başında bir kez çalar.
            if (rentIntervalDays > 0 && newValue != previousValue && newValue % rentIntervalDays == 0)
            {
                SfxBus.Play(SfxId.RentWarning);
            }
        }

        private void HandleDayOverChanged(bool previousValue, bool newValue)
        {
            SetDayEndScreenActive(newValue);
        }

        private void HandleBreakRoomReadyChanged(bool previousValue, bool newValue)
        {
            // N10: server-auth ready durumu artık HER peer'e NetworkVariable ile yayılıyor.
            // UI/hareket-kilidi senkronunu burada tetikle ki yerel trigger tespitini lag'de
            // kaçırmış client'lar da Next Day UI'ını görsün (server-auth state ↔ yerel UI
            // kopukluğu kapandı). Idempotent; false tarafı mevcut reset/unlock akışlarında.
            if (newValue)
            {
                BreakRoomManager.Instance?.OnBreakRoomReadyStateSynced(true);
            }
        }

        #endregion

        #region Scene Management

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (IsGameScene(scene.name))
            {
                Debug.Log($"{LOG_PREFIX} Game scene {scene.name} loaded, resetting day cycle");

                if (IsServer)
                {
                    ResetDayCycle();
                }
            }
        }

        private static bool IsGameScene(string sceneName)
        {
            return sceneName != "MainMenu" && sceneName.Contains("Game");
        }

        #endregion

        #region Localization

        private void HandleLocaleChanged(Locale newLocale)
        {
            Debug.Log($"{LOG_PREFIX} Locale changed to: {newLocale?.Identifier.Code ?? "null"}");
            // UpdateDayTimeUI gün değişmediği sürece yazmayı atlar (cache); dil değişince
            // gün aynı kalsa bile metin ("Gün 1"/"Day 1") yeniden yazılmalı.
            _lastDisplayedDay = -1;
            UpdateUI();
        }

        #endregion

        #region Public API - Backward Compatibility

        #endregion

        #region Editor & Debug

        [ContextMenu("Force Next Day")]
        public void ForceNextDay()
        {
            CallNextDay();
        }

        [ContextMenu("Force Day End")]
        public void ForceDayEnd()
        {
            if (IsServer)
            {
                _networkElapsedTime.Value = CurrentDayDuration;
                _networkIsBreakRoomReady.Value = true;
            }
        }

        [ContextMenu("Force Start Time")]
        public void ForceStartTime()
        {
            if (IsServer)
            {
                _networkElapsedTime.Value = 0f;
                _networkIsDayOver.Value = false;
                _networkIsBreakRoomReady.Value = false;
            }
        }

        [ContextMenu("Reset Day Cycle")]
        public void ForceResetDayCycle()
        {
            ResetDayCycle();
        }

#if UNITY_EDITOR
        [ContextMenu("Debug: Print State")]
        public void DebugPrintState()
        {
            Debug.Log($"{LOG_PREFIX} === DEBUG STATE ===\n" +
                      $"Current Day: {_networkCurrentDay.Value}\n" +
                      $"Elapsed Time: {_networkElapsedTime.Value:F2}s / {CurrentDayDuration}s\n" +
                      $"Is Day Over: {_networkIsDayOver.Value}\n" +
                      $"Is Break Room Ready: {_networkIsBreakRoomReady.Value}\n" +
                      $"Current Hour: {_currentHour}");
        }
#endif

        #endregion
    }
}