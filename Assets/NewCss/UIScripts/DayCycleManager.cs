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

        /// <summary>
        /// Haftalık kira ödemesi yapıldığında tetiklenir (her 6 günde bir)
        /// </summary>
        public static event Action OnWeeklyEvent;

        #endregion

        #region Constants

        private const int INITIAL_WEEKLY_COST = 1000;
        private const int MIN_WEEKLY_INCREASE = 200;
        private const int MAX_WEEKLY_INCREASE = 600;
        private const float WEEKLY_COST_MULTIPLIER = 1.2f;
        private const int DAYS_PER_WEEK = 6;
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

        #region Network Variables

        private readonly NetworkVariable<float> _networkElapsedTime = new(0f);
        private readonly NetworkVariable<int> _networkCurrentDay = new(1);
        private readonly NetworkVariable<bool> _networkIsDayOver = new(false);
        private readonly NetworkVariable<bool> _networkIsBreakRoomReady = new(false);
        private readonly NetworkVariable<int> _networkWeeklyCost = new(INITIAL_WEEKLY_COST);

        #endregion

        #region Private Fields

        private int _localWeeklyCost;
        private int _currentHour;
        private bool _lunchNotified;
        private bool _moneyCheckCompleted;

        // Periyodik kontrol flag'leri
        private PeriodicCheckState _periodicChecks;

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
        /// Mevcut haftalık kira maliyeti
        /// </summary>
        public int WeeklyCost => _networkWeeklyCost.Value;

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
            UpdateUI();

            if (!IsSpawned || !IsServer || _networkIsDayOver.Value)
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
            _networkWeeklyCost.Value = INITIAL_WEEKLY_COST;
            _localWeeklyCost = INITIAL_WEEKLY_COST;
            Debug.Log($"{LOG_PREFIX} Server: Weekly cost initialized to {INITIAL_WEEKLY_COST}");
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
            _networkWeeklyCost.Value = INITIAL_WEEKLY_COST;
        }

        private void ResetLocalState()
        {
            _localWeeklyCost = INITIAL_WEEKLY_COST;
            _currentHour = startHour;
            _lunchNotified = false;
            _moneyCheckCompleted = false;
            _periodicChecks.Reset();
        }

        private void AdvanceTime()
        {
            _networkElapsedTime.Value += Time.deltaTime;
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

            // Para kontrolü yap
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

        private bool TryProcessMoneyCheck()
        {
            if (GameStateManager.Instance == null)
            {
                _moneyCheckCompleted = true;
                return true;
            }

            if (GameStateManager.Instance.IsDayOver)
            {
                Debug.Log($"{LOG_PREFIX} Game already ended - skipping money check");
                _moneyCheckCompleted = true;
                return false;
            }

            GameStateManager.Instance.CheckMoneyAtDayEnd();
            _moneyCheckCompleted = true;

            if (GameStateManager.Instance.IsDayOver)
            {
                Debug.Log($"{LOG_PREFIX} Game ended after money check - stopping day cycle");
                return false;
            }

            return true;
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

            // Haftalık kira kontrolü
            ProcessWeeklyRentIfNeeded();

            // Gün durumlarını sıfırla
            _networkIsDayOver.Value = false;
            _networkIsBreakRoomReady.Value = false;

            // Event'leri tetikle
            OnNewDay?.Invoke();
            TriggerNewDayEventClientRpc();

            // UI güncelle
            UpdateUI();
            HideDayEndScreenClientRpc();

            // Break room oyuncu sayısını güncelle
            UpdateBreakRoomPlayerCount();
        }

        private void ProcessWeeklyRentIfNeeded()
        {
            int day = _networkCurrentDay.Value;

            if (day % DAYS_PER_WEEK != 0)
            {
                return;
            }

            // Kira artışı hesapla
            int randomIncrease = UnityEngine.Random.Range(MIN_WEEKLY_INCREASE, MAX_WEEKLY_INCREASE + 1);
            _localWeeklyCost = Mathf.RoundToInt(_localWeeklyCost * WEEKLY_COST_MULTIPLIER);
            _localWeeklyCost += randomIncrease;
            _networkWeeklyCost.Value = _localWeeklyCost;

            // Kirayı öde
            if (MoneySystem.Instance != null)
            {
                MoneySystem.Instance.SpendMoney(_localWeeklyCost);
                Debug.Log($"{LOG_PREFIX} RENT DAY {day}: Rent paid - {_localWeeklyCost} coins");
                Debug.Log($"{LOG_PREFIX} Remaining money: {MoneySystem.Instance.CurrentMoney}");
            }

            int rentPaymentNumber = day / DAYS_PER_WEEK;
            Debug.Log($"{LOG_PREFIX} ▶ Weekly Rent Event: Day {day}\n" +
                      $"- Cost increase: +{randomIncrease}\n" +
                      $"- New weekly cost: {_localWeeklyCost}\n" +
                      $"- This is rent payment #{rentPaymentNumber} of 5");

            // Haftalık event'i tetikle
            TriggerWeeklyEventClientRpc();

            // Oyun durumunu kontrol et
            CheckGameStateAfterRent();
        }

        private void CheckGameStateAfterRent()
        {
            Debug.Log($"{LOG_PREFIX} Calling GameStateManager.CheckGameState after rent payment");

            if (GameStateManager.Instance != null)
            {
                Debug.Log($"{LOG_PREFIX} GameStateManager.Instance found, calling CheckGameState");
                GameStateManager.Instance.CheckGameState();
            }
            else
            {
                Debug.LogError($"{LOG_PREFIX} GameStateManager. Instance is NULL!");
            }
        }

        private void UpdateBreakRoomPlayerCount()
        {
            var breakRoomManager = FindObjectOfType<BreakRoomManager>();

            if (breakRoomManager != null)
            {
                breakRoomManager.requiredPlayers = breakRoomManager.GetSteamLobbyPlayerCount();
                Debug.Log($"{LOG_PREFIX} BreakRoomManager.requiredPlayers updated: {breakRoomManager.requiredPlayers}");
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

            var timeInfo = CalculateDisplayTime();
            dayTimeText.text = FormatTimeDisplay(timeInfo);
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
            string template = LocalizationHelper.GetLocalizedString(LOC_KEY_DAY_FORMAT);
            try
            {
                return string.Format(template, _networkCurrentDay.Value, timeInfo.hour.ToString("D2"), timeInfo.minute.ToString("D2"), CurrentDayDuration);
            }
            catch
            {
                // Fallback if format string is invalid
                return $"Day {_networkCurrentDay.Value}  {timeInfo.hour:D2}:{timeInfo.minute:D2} ({CurrentDayDuration}s)";
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
        private void TriggerWeeklyEventClientRpc()
        {
            OnWeeklyEvent?.Invoke();
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

        /// <summary>
        /// Mevcut haftalık kira maliyetini döndürür
        /// </summary>
        public int GetCurrentWeeklyCost()
        {
            return _networkWeeklyCost.Value;
        }

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
                      $"Weekly Cost: {_networkWeeklyCost.Value}\n" +
                      $"Current Hour: {_currentHour}");
        }
#endif

        #endregion
    }
}