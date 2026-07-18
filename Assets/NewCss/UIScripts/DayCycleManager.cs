using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;
using TMPro;

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
        [Tooltip("Bir günün gerçek süre karşılığı (saniye)")]
        public float realDurationInSeconds = 160f;

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

        // ── Acil Fren perki (emergency_brake) — server-authoritative, tek kullanımlık ──
        private const float EMERGENCY_BRAKE_PRESTIGE_PENALTY = -5f;

        /// <summary>Acil Fren perki satın alındığında true olur; bir kez tetiklenip tükenir.</summary>
        public bool insuranceAvailable = false;

        #endregion

        #region Network Variables

        private readonly NetworkVariable<float> _networkElapsedTime = new(0f);
        private readonly NetworkVariable<int> _networkCurrentDay = new(1);
        private readonly NetworkVariable<bool> _networkIsDayOver = new(false);
        private readonly NetworkVariable<bool> _networkIsBreakRoomReady = new(false);

        #endregion

        #region Private Fields

        private int _currentHour;
        private bool _lunchNotified;
        private bool _moneyCheckCompleted;
        private int _rentPaymentCount;   // Kaçıncı kira ödemesi
        private bool _graceUsed;         // İlk kira affı kullanıldı mı
        private bool _gameOverStopProcessing; // Game-over sonrası Update() işleme döngüsünü durdurur (server-only)

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

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeSingleton();
            if (economySettings == null)
            {
                economySettings = Resources.Load<GameEconomySettings>("EkonomiAyarlari");
            }
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
            _graceUsed = false;
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
        /// İlk kirada grace period: para yetmezse eldekinin %80'i alınır.
        /// 2+ kirada para yetmezse Game Over.
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
            else if (!_graceUsed)
            {
                // İlk kira affı — eldeki paranın %80'i alınır, ödenmiş sayılır
                float gracePct   = economySettings != null ? economySettings.gracePaymentPercent : 0.8f;
                int graceAmount = Mathf.RoundToInt(currentMoney * gracePct);
                MoneySystem.Instance.SpendMoney(graceAmount);
                _graceUsed = true;
                _rentPaymentCount++;
                Debug.Log($"{LOG_PREFIX} Grace period used. Took {graceAmount} ({gracePaymentPercent * 100}% of {currentMoney}). Needed: {rentAmount}");
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

            _moneyCheckCompleted = true;
            return true;
        }

        /// <summary>
        /// Kira hesaplama: TemelKira × rentGrowthMultiplier^dönem × rentScaledMultiplier
        /// </summary>
        private int CalculateRent()
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
                Debug.LogWarning($"{LOG_PREFIX} economySettings atanmamış! Fallback değerler kullanılıyor.");
                int baseRent    = playerCount == 1 ? 500 : playerCount == 2 ? 900 : playerCount == 3 ? 1200 : 1500;
                float scaled    = baseRent * Mathf.Pow(1.3f, _rentPaymentCount);
                finalRent       = scaled;
            }

            int result = Mathf.RoundToInt(finalRent);
            Debug.Log($"{LOG_PREFIX} Rent calc result: {result} (Players: {playerCount}, Cycle: {_rentPaymentCount})");
            return result;
        }

        /// <summary>
        /// Dış sistemler (örn. EventEffectManager/FESTIVAL DAY) için o anki kira döngüsüne göre
        /// hesaplanan güncel kira miktarını döndürür (henüz ödenmemiş/gerçekleşmemiş olsa bile).
        /// Server-authoritative değildir salt-okunur bir hesaplamadır; CalculateRent() ile aynı formülü kullanır.
        /// </summary>
        public int GetCurrentRentAmount() => CalculateRent();

        /// <summary>
        /// Kira hesabı için oyuncu sayısını döndürür.
        /// Tek doğruluk kaynağı server-authoritative roster'dır (GameStateManager._playerRoster,
        /// Break Room / Win-Lose ekranlarının da okuduğu kaynak). ConnectedClientsList anlık
        /// bağlantı listesidir ve kira tick'inden hemen önce disconnect olan bir oyuncu yüzünden
        /// roster ile tutarsız düşebilir (bkz. N7); bu yüzden yalnızca roster erişilemezse/boşsa
        /// fallback olarak kullanılır.
        /// </summary>
        private int GetPlayerCount()
        {
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

            // Zaman ve gün sayacını güncelle
            _networkElapsedTime.Value = 0f;
            _networkCurrentDay.Value++;

            // Periyodik kontrolleri sıfırla
            _periodicChecks.Reset();

            Debug.Log($"{LOG_PREFIX} Day {_networkCurrentDay.Value} started - Duration: {CurrentDayDuration} seconds");

            // Gün durumlarını sıfırla
            _networkIsDayOver.Value = false;
            _networkIsBreakRoomReady.Value = false;

            // Check win condition after completing day (when moving to next day after day 16)
            if (_networkCurrentDay.Value >= MAX_DAYS)
            {
                Debug.Log($"{LOG_PREFIX} Completed day {MAX_DAYS}! Checking win condition...");
                CheckWinCondition();
            }

            // Event'leri tetikle
            OnNewDay?.Invoke();
            TriggerNewDayEventClientRpc();

            // UI güncelle
            UpdateUI();
            HideDayEndScreenClientRpc();

            // Break room oyuncu sayısını güncelle
            UpdateBreakRoomPlayerCount();
        }

        private void CheckWinCondition()
        {
            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.CheckWinCondition();
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

        private string FormatTimeDisplay((int hour, int minute) timeInfo)
        {
            // Only display the day number as "Day N"
            return $"Day {_networkCurrentDay.Value}";
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
        }

        private void HandleDayOverChanged(bool previousValue, bool newValue)
        {
            SetDayEndScreenActive(newValue);
        }

        private void HandleBreakRoomReadyChanged(bool previousValue, bool newValue)
        {
            // Gerekirse burada ek işlem yapılabilir
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