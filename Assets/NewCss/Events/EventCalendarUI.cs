using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace NewCss
{
    /// <summary>
    /// Event takvimi UI sistemi - oyun eventlerini, kira günlerini ve takvim görünümünü yönetir. 
    /// Animasyonlu açma/kapama, trigger tabanlı etkileşim ve network desteği sağlar.
    /// </summary>
    public class EventCalendarUI : NetworkBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[EventCalendar]";
        private const string CHARACTER_TAG = "Character";
        private const string RENT_DAY_TEXT = "Rent Day";
        private const int CALENDAR_CELL_COUNT = 16;
        private const int RENT_DAY_INTERVAL = 6;
        private const int EVENT_INTERVAL_MIN = 3;
        private const int EVENT_INTERVAL_MAX = 5;
        private const int INITIAL_POSITIVE_EVENT_COUNT = 2;
        private const int GUARANTEED_NEGATIVE_EVENT_INDEX = 2;
        private const int MAX_PREGENERATED_DAYS = 100;
        private const float DEFAULT_OPEN_ANIMATION_DURATION = 0.5f;
        private const float DEFAULT_CLOSE_ANIMATION_DURATION = 0.3f;

        // Animator triggers
        private const string TRIGGER_OPEN = "Open";
        private const string TRIGGER_CLOSE = "Close";

        #endregion

        #region Enums

        public enum EventType
        {
            Positive,
            Negative,
            Neutral
        }

        #endregion

        #region Nested Classes

        [System.Serializable]
        public class GameEvent
        {
            public string name;
            public EventType type;
            public string description;

            public GameEvent(string name, EventType type, string description)
            {
                this.name = name;
                this.type = type;
                this.description = description;
            }
        }

        #endregion

        #region Serialized Fields - Calendar Cells

        [Header("=== CALENDAR CELLS ===")]
        [SerializeField, Tooltip("Gün numarası text'leri (16 adet)")]
        public List<TMP_Text> dayNumberTexts = new();

        [SerializeField, Tooltip("Event spawn noktaları (16 adet)")]
        public Transform[] eventSpawnPoints = new Transform[CALENDAR_CELL_COUNT];

        #endregion

        #region Serialized Fields - Prefabs

        [Header("=== EVENT PREFAB ===")]
        [SerializeField, Tooltip("Event text prefab'ı")]
        public GameObject eventTextPrefab;

        #endregion

        #region Serialized Fields - UI

        [Header("=== CALENDAR PANEL ===")]
        [SerializeField, Tooltip("Takvim paneli")]
        public GameObject calendarPanel;

        [SerializeField, Tooltip("Çıkış butonu")]
        public Button exitButton;

        #endregion

        #region Serialized Fields - Animation

        [Header("=== ANIMATION SETTINGS ===")]
        [SerializeField, Tooltip("Panel animator'ı")]
        public Animator panelAnimator;

        [SerializeField, Tooltip("Kapatma animasyon clip adı")]
        public string closeAnimationClipName = "DateExit";

        [SerializeField, Tooltip("Açma animasyon clip adı")]
        public string openAnimationClipName = "DateOpening";

        #endregion

        #region Serialized Fields - Control

        [Header("=== CONTROL ===")]
        [SerializeField, Tooltip("Başlangıç günü")]
        public int startDay = 1;

        #endregion

        #region Private Fields - Events

        private readonly List<GameEvent> _allEvents = new()
        {
            new GameEvent("BUSY DAY", EventType.Negative, "CUSTOMER SPAWN RATE INCREASES BY 30%. "),
            new GameEvent("DELIVERY BONUS", EventType.Positive, "EARN 20% MORE MONEY PER DELIVERY."),
            new GameEvent("ANGRY CUSTOMERS", EventType.Negative, "CUSTOMER PATIENCE DECREASES BY 30%."),
            new GameEvent("RELAXED DAY", EventType.Positive, "CUSTOMER PATIENCE INCREASES BY 10%."),
            new GameEvent("SLOW LOGISTICS", EventType.Negative, "TRUCK MOVEMENT SPEED DECREASES BY 20%."),
            new GameEvent("EXPRESS CARGO", EventType.Positive, "TRUCKS LEAVE THE SCENE 10% FASTER."),
            new GameEvent("HEAVY BOXES", EventType.Negative, "OVERALL MOVEMENT SPEED SLOWS DOWN BY 10%."),
            new GameEvent("GOLDEN BOX DAY", EventType.Positive, "EACH CORRECT DELIVERED BOX EARNS EXTRA 5%."),
            new GameEvent("OPPORTUNITY DAY", EventType.Positive, "UPGRADE COSTS DECREASE BY 10%."),
            new GameEvent("FATIGUE PROBLEM", EventType.Negative, "STAMINA REGENERATION TIME INCREASES BY 30%."),
            new GameEvent("QUOTA DAY", EventType.Neutral, "ALL TRUCKS REQUEST ONLY ONE COLOR OF BOX."),
            new GameEvent("VIP SERVICE", EventType. Positive, "10% CHANCE BOXES ARE PERFECT AND EARN 10% MORE."),
            new GameEvent("SURPRISE AUDIT", EventType.Negative, "ALL FAULTY OPERATIONS PENALIZE DOUBLE. "),
            new GameEvent("RAINY DAY", EventType. Positive, "20% FEWER CUSTOMERS ARRIVE. "),
            new GameEvent("MARKETING DAY", EventType. Negative, "20% MORE CUSTOMERS, BUT 30% LESS EARNINGS."),
            new GameEvent("CUSTOMER SUPPORT", EventType. Negative, "RECEPTION PHONE RINGS 30% MORE OFTEN."),
            new GameEvent("FESTIVAL DAY", EventType. Positive, "RANDOM BONUS IS EARNED AT DAY START.")
        };

        private readonly List<int> _randomEventDays = new();
        private readonly Dictionary<int, GameEvent> _eventsByDay = new();
        private readonly List<GameObject> _spawnedEventTexts = new();

        #endregion

        #region Private Fields - State

        private bool _isPanelOpen;
        private bool _isAnimating;
        private PlayerMovement _currentPlayer;

        #endregion

        #region Private Fields - Cached Animation Durations

        private float _cachedOpenDuration = -1f;
        private float _cachedCloseDuration = -1f;

        #endregion

        #region Public Properties

        /// <summary>
        /// Panel açık mı? 
        /// </summary>
        public bool IsPanelOpen => _isPanelOpen;

        /// <summary>
        /// Animasyon devam ediyor mu?
        /// </summary>
        public bool IsAnimating => _isAnimating;

        /// <summary>
        /// Mevcut gün
        /// </summary>
        public int CurrentDay => startDay;

        /// <summary>
        /// Tüm event günleri
        /// </summary>
        public IReadOnlyList<int> EventDays => _randomEventDays;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeAnimator();
            InitializeExitButton();
            CacheAnimationDurations();
        }

        private void Start()
        {
            InitializeCalendarPanel();
            GenerateInitialEvents();
            UpdateCalendarUI();
            SubscribeToDayCycleEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeFromDayCycleEvents();
            CleanupExitButton();
            ClearSpawnedEventTexts();
        }

        #endregion

        #region Network Lifecycle

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
        }

        #endregion

        #region Initialization

        private void InitializeAnimator()
        {
            if (panelAnimator != null) return;

            if (calendarPanel != null)
            {
                panelAnimator = calendarPanel.GetComponent<Animator>();

                if (panelAnimator == null)
                {
                    LogWarning("panelAnimator not assigned and calendarPanel has no Animator!");
                }
            }
        }

        private void InitializeExitButton()
        {
            if (exitButton != null)
            {
                exitButton.onClick.AddListener(HandleExitButtonClicked);
            }
            else
            {
                LogWarning("Exit Button not assigned!");
            }
        }

        private void InitializeCalendarPanel()
        {
            if (calendarPanel != null)
            {
                calendarPanel.SetActive(false);
            }
        }

        private void CacheAnimationDurations()
        {
            if (panelAnimator == null) return;

            var controller = panelAnimator.runtimeAnimatorController;
            if (controller == null) return;

            foreach (var clip in controller.animationClips)
            {
                if (IsOpenAnimationClip(clip.name))
                {
                    _cachedOpenDuration = clip.length;
                }
                else if (IsCloseAnimationClip(clip.name))
                {
                    _cachedCloseDuration = clip.length;
                }
            }
        }

        private bool IsOpenAnimationClip(string clipName)
        {
            return clipName.Contains("Open") ||
                   clipName.Contains("Opening") ||
                   clipName == openAnimationClipName;
        }

        private bool IsCloseAnimationClip(string clipName)
        {
            return clipName.Contains("Exit") ||
                   clipName.Contains("Close") ||
                   clipName == closeAnimationClipName;
        }

        #endregion

        #region Event Subscriptions

        private void SubscribeToDayCycleEvents()
        {
            DayCycleManager.OnNewDay += HandleNewDay;
        }

        private void UnsubscribeFromDayCycleEvents()
        {
            DayCycleManager.OnNewDay -= HandleNewDay;
        }

        #endregion

        #region Trigger Detection

        private void OnTriggerEnter(Collider other)
        {
            if (!ValidateCharacterTrigger(other, out PlayerMovement playerMovement))
            {
                return;
            }

            if (_isAnimating)
            {
                LogDebug("Animation in progress, ignoring trigger enter");
                return;
            }

            _currentPlayer = playerMovement;
            LockPlayerMovement(true);
            ShowCalendar();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag(CHARACTER_TAG)) return;

            var networkObject = other.GetComponent<NetworkObject>();
            if (networkObject == null || !networkObject.IsOwner) return;

            var player = other.GetComponent<PlayerMovement>();
            if (player != _currentPlayer) return;

            if (_isPanelOpen)
            {
                CloseCalendarAndUnlockPlayer();
            }
        }

        private bool ValidateCharacterTrigger(Collider other, out PlayerMovement playerMovement)
        {
            playerMovement = null;

            if (!other.CompareTag(CHARACTER_TAG)) return false;

            var networkObject = other.GetComponent<NetworkObject>();
            if (networkObject == null || !networkObject.IsOwner) return false;

            playerMovement = other.GetComponent<PlayerMovement>();
            return true;
        }

        #endregion

        #region Panel Open/Close

        /// <summary>
        /// Takvimi gösterir
        /// </summary>
        public void ShowCalendar()
        {
            if (_isPanelOpen || _isAnimating)
            {
                LogDebug("Panel already open or animating, ignoring open request");
                return;
            }

            _isPanelOpen = true;
            _isAnimating = true;

            if (calendarPanel == null)
            {
                _isAnimating = false;
                return;
            }

            calendarPanel.SetActive(true);

            if (panelAnimator != null)
            {
                ResetAnimatorTriggers();
                StartCoroutine(PlayOpenAnimationCoroutine());
            }
            else
            {
                _isAnimating = false;
                LogDebug("Calendar opened without animation");
            }
        }

        /// <summary>
        /// Takvimi gizler
        /// </summary>
        public void HideCalendar()
        {
            if (!_isPanelOpen || _isAnimating)
            {
                LogDebug("Panel not open or animating, ignoring close request");
                return;
            }

            _isPanelOpen = false;
            _isAnimating = true;

            if (calendarPanel == null)
            {
                _isAnimating = false;
                return;
            }

            if (panelAnimator != null)
            {
                ResetAnimatorTriggers();
                panelAnimator.SetTrigger(TRIGGER_CLOSE);
                StartCoroutine(PlayCloseAnimationCoroutine());
            }
            else
            {
                calendarPanel.SetActive(false);
                _isAnimating = false;
                LogDebug("Calendar closed without animation");
            }
        }

        private void CloseCalendarAndUnlockPlayer()
        {
            if (!_isPanelOpen) return;

            HideCalendar();
            LockPlayerMovement(false);
        }

        #endregion

        #region Animation Coroutines

        private IEnumerator PlayOpenAnimationCoroutine()
        {
            yield return null; // Wait one frame

            panelAnimator.SetTrigger(TRIGGER_OPEN);

            float duration = GetOpenAnimationDuration();
            yield return new WaitForSeconds(duration);

            _isAnimating = false;
            LogDebug("Calendar open animation completed");
        }

        private IEnumerator PlayCloseAnimationCoroutine()
        {
            float duration = GetCloseAnimationDuration();

            LogDebug($"Waiting {duration} seconds for close animation...");
            yield return new WaitForSeconds(duration);

            if (calendarPanel != null)
            {
                calendarPanel.SetActive(false);
                LogDebug("Calendar panel disabled after close animation");
            }

            _isAnimating = false;
        }

        private void ResetAnimatorTriggers()
        {
            if (panelAnimator == null) return;

            panelAnimator.ResetTrigger(TRIGGER_OPEN);
            panelAnimator.ResetTrigger(TRIGGER_CLOSE);
        }

        private float GetOpenAnimationDuration()
        {
            return _cachedOpenDuration > 0 ? _cachedOpenDuration : DEFAULT_OPEN_ANIMATION_DURATION;
        }

        private float GetCloseAnimationDuration()
        {
            return _cachedCloseDuration > 0 ? _cachedCloseDuration : DEFAULT_CLOSE_ANIMATION_DURATION;
        }

        #endregion

        #region Animation Event Callbacks

        /// <summary>
        /// Animation Event callback - açılma animasyonu tamamlandığında
        /// </summary>
        public void OnOpenAnimationComplete()
        {
            _isAnimating = false;
            LogDebug("Calendar open animation completed via Animation Event");
        }

        /// <summary>
        /// Animation Event callback - kapanma animasyonu tamamlandığında
        /// </summary>
        public void OnCloseAnimationComplete()
        {
            if (calendarPanel != null)
            {
                calendarPanel.SetActive(false);
            }

            _isAnimating = false;
            LogDebug("Calendar close animation completed via Animation Event");
        }

        #endregion

        #region Player Movement

        private void LockPlayerMovement(bool locked)
        {
            if (_currentPlayer == null) return;

            _currentPlayer.LockMovement(locked);
        }

        #endregion

        #region Button Handlers

        private void HandleExitButtonClicked()
        {
            LogDebug("Exit button clicked");
            CloseCalendarAndUnlockPlayer();
        }

        private void CleanupExitButton()
        {
            if (exitButton != null)
            {
                exitButton.onClick.RemoveListener(HandleExitButtonClicked);
            }
        }

        #endregion

        #region Event Generation

        private void GenerateInitialEvents()
        {
            int maxDay = startDay + MAX_PREGENERATED_DAYS;
            int currentDay = startDay;
            int eventCount = 0;

            var positiveEvents = _allEvents.FindAll(e => e.type == EventType.Positive);
            var negativeEvents = _allEvents.FindAll(e => e.type == EventType.Negative);

            while (currentDay < maxDay)
            {
                currentDay += Random.Range(EVENT_INTERVAL_MIN, EVENT_INTERVAL_MAX);
                currentDay = AdjustForRentDay(currentDay);

                if (_randomEventDays.Contains(currentDay)) continue;

                _randomEventDays.Add(currentDay);

                GameEvent selectedEvent = SelectEventByCount(eventCount, positiveEvents, negativeEvents);
                _eventsByDay[currentDay] = selectedEvent;

                eventCount++;
            }

            _randomEventDays.Sort();
        }

        private int AdjustForRentDay(int day)
        {
            if (day % RENT_DAY_INTERVAL != 0) return day;

            int adjustment = Random.Range(0, 2) == 0 ? -1 : 1;
            int adjustedDay = day + adjustment;

            if (adjustedDay % RENT_DAY_INTERVAL != 0 && adjustedDay > 0)
            {
                return adjustedDay;
            }

            return day + 2;
        }

        private GameEvent SelectEventByCount(int eventCount, List<GameEvent> positiveEvents, List<GameEvent> negativeEvents)
        {
            if (eventCount < INITIAL_POSITIVE_EVENT_COUNT)
            {
                return positiveEvents[Random.Range(0, positiveEvents.Count)];
            }

            if (eventCount == GUARANTEED_NEGATIVE_EVENT_INDEX)
            {
                return negativeEvents[Random.Range(0, negativeEvents.Count)];
            }

            return _allEvents[Random.Range(0, _allEvents.Count)];
        }

        #endregion

        #region Day Cycle Handler

        private void HandleNewDay()
        {
            if (DayCycleManager.Instance != null)
            {
                startDay = DayCycleManager.Instance.currentDay;
            }

            UpdateCalendarUI();
        }

        #endregion

        #region Calendar UI Update

        /// <summary>
        /// Takvim UI'ını günceller
        /// </summary>
        public void UpdateCalendarUI()
        {
            ClearSpawnedEventTexts();
            PopulateCalendarCells();
        }

        private void ClearSpawnedEventTexts()
        {
            foreach (var obj in _spawnedEventTexts)
            {
                if (obj != null)
                {
                    Destroy(obj);
                }
            }
            _spawnedEventTexts.Clear();
        }

        private void PopulateCalendarCells()
        {
            int cellCount = Mathf.Min(dayNumberTexts.Count, eventSpawnPoints.Length);

            for (int i = 0; i < cellCount; i++)
            {
                int day = startDay + i;

                UpdateDayNumberText(i, day);
                TrySpawnEventText(i, day);
                TrySpawnRentDayText(i, day);
            }
        }

        private void UpdateDayNumberText(int index, int day)
        {
            if (dayNumberTexts[index] != null)
            {
                dayNumberTexts[index].text = day.ToString();
            }
        }

        private void TrySpawnEventText(int index, int day)
        {
            if (!_randomEventDays.Contains(day)) return;
            if (!_eventsByDay.TryGetValue(day, out GameEvent gameEvent)) return;

            var eventObject = SpawnEventText(index, gameEvent.name, Color.white);
            if (eventObject != null)
            {
                _spawnedEventTexts.Add(eventObject);
            }
        }

        private void TrySpawnRentDayText(int index, int day)
        {
            if (day % RENT_DAY_INTERVAL != 0) return;

            var rentObject = SpawnEventText(index, RENT_DAY_TEXT, Color.red);
            if (rentObject != null)
            {
                _spawnedEventTexts.Add(rentObject);
            }
        }

        private GameObject SpawnEventText(int index, string text, Color color)
        {
            if (eventTextPrefab == null || eventSpawnPoints[index] == null) return null;

            var spawnPoint = eventSpawnPoints[index];
            var eventObject = Instantiate(eventTextPrefab, spawnPoint.position, Quaternion.identity, spawnPoint);

            var tmpText = eventObject.GetComponent<TMP_Text>();
            if (tmpText != null)
            {
                tmpText.text = text;
                tmpText.color = color;
            }

            return eventObject;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Takvimi belirtilen gün sayısı kadar ilerletir
        /// </summary>
        public void AdvanceCalendar(int daysPassed)
        {
            startDay += daysPassed;
            UpdateCalendarUI();
        }

        /// <summary>
        /// Belirli bir gün için event bilgisini döndürür
        /// </summary>
        public GameEvent GetEventForDay(int day)
        {
            return _eventsByDay.TryGetValue(day, out GameEvent gameEvent) ? gameEvent : null;
        }

        /// <summary>
        /// Belirli bir günün kira günü olup olmadığını kontrol eder
        /// </summary>
        public bool IsRentDay(int day)
        {
            return day % RENT_DAY_INTERVAL == 0;
        }

        /// <summary>
        /// Belirli bir günde event olup olmadığını kontrol eder
        /// </summary>
        public bool HasEventOnDay(int day)
        {
            return _eventsByDay.ContainsKey(day);
        }

        /// <summary>
        /// Bugünün event'ini döndürür
        /// </summary>
        public GameEvent GetTodayEvent()
        {
            int today = DayCycleManager.Instance?.currentDay ?? startDay;
            return GetEventForDay(today);
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

        #endregion

        #region Editor Debug

#if UNITY_EDITOR
        [ContextMenu("Show Calendar")]
        private void DebugShowCalendar()
        {
            ShowCalendar();
        }

        [ContextMenu("Hide Calendar")]
        private void DebugHideCalendar()
        {
            HideCalendar();
        }

        [ContextMenu("Advance 1 Day")]
        private void DebugAdvance1Day()
        {
            AdvanceCalendar(1);
        }

        [ContextMenu("Advance 7 Days")]
        private void DebugAdvance7Days()
        {
            AdvanceCalendar(7);
        }

        [ContextMenu("Regenerate Events")]
        private void DebugRegenerateEvents()
        {
            _randomEventDays.Clear();
            _eventsByDay.Clear();
            GenerateInitialEvents();
            UpdateCalendarUI();
        }

        [ContextMenu("Debug: Print State")]
        private void DebugPrintState()
        {
            Debug.Log($"{LOG_PREFIX} === CALENDAR STATE ===");
            Debug.Log($"Is Panel Open: {_isPanelOpen}");
            Debug.Log($"Is Animating: {_isAnimating}");
            Debug.Log($"Start Day: {startDay}");
            Debug.Log($"Total Event Days: {_randomEventDays.Count}");
            Debug.Log($"Spawned Event Texts: {_spawnedEventTexts.Count}");
            Debug.Log($"Has Current Player: {_currentPlayer != null}");
        }

        [ContextMenu("Debug: Print Events")]
        private void DebugPrintEvents()
        {
            Debug.Log($"{LOG_PREFIX} === EVENTS ===");
            foreach (int day in _randomEventDays)
            {
                if (day >= startDay && day < startDay + 50 && _eventsByDay.TryGetValue(day, out GameEvent evt))
                {
                    Debug.Log($"Day {day}: {evt.name} ({evt.type})");
                }
            }
        }

        [ContextMenu("Debug: Print Rent Days")]
        private void DebugPrintRentDays()
        {
            Debug.Log($"{LOG_PREFIX} === RENT DAYS (next 30 days) ===");
            for (int i = 0; i < 30; i++)
            {
                int day = startDay + i;
                if (IsRentDay(day))
                {
                    Debug.Log($"Day {day}: RENT DAY");
                }
            }
        }
#endif

        #endregion
    }
}