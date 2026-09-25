using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
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
        private const int CALENDAR_CELL_COUNT = 16;
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

        /// <summary>
        /// Bant seçim havuzunu daraltmak için kullanılan şiddet etiketi (bkz.
        /// docs/economy/her-gun-event-ve-kart-tasarimi-2026-09-24.md §C). Öğretici bant (gün 1-3)
        /// yalnızca Light pozitifleri kullanır; kira sonrası bantlar negatif havuzu Light→Medium→Hard
        /// olarak kademeli açar.
        /// </summary>
        public enum EventSeverity
        {
            Light,
            Medium,
            Hard
        }

        #endregion

        #region Nested Classes

        [System.Serializable]
        public class GameEvent
        {
            public string name;
            public string nameLocKey;
            public EventType type;
            public EventSeverity severity;
            public string description;
            public string descLocKey;

            /// <summary>Bu event yalnızca [minDay, maxDay] (dahil, mutlak gün numarası) aralığında seçilebilir.</summary>
            public int minDay;
            public int maxDay;

            public GameEvent(string name, string nameLocKey, EventType type, EventSeverity severity,
                string description, string descLocKey, int minDay = 1, int maxDay = int.MaxValue)
            {
                this.name = name;
                this.nameLocKey = nameLocKey;
                this.type = type;
                this.severity = severity;
                this.description = description;
                this.descLocKey = descLocKey;
                this.minDay = minDay;
                this.maxDay = maxDay;
            }

            /// <summary>
            /// Gets the localized event name
            /// </summary>
            public string GetLocalizedName()
            {
                if (string.IsNullOrEmpty(nameLocKey))
                    return name;
                return LocalizationHelper.GetLocalizedString(nameLocKey);
            }

            /// <summary>
            /// Gets the localized event description
            /// </summary>
            public string GetLocalizedDescription()
            {
                if (string.IsNullOrEmpty(descLocKey))
                    return description;
                return LocalizationHelper.GetLocalizedString(descLocKey);
            }
        }

        #endregion

        #region Serialized Fields - Calendar Cells

        [Header("=== CALENDAR CELLS ===")]
        // `dayNumberTexts` KALDIRILDI: gün numaraları (1-16) artık sahnede duvar takviminin
        // üstüne sabit yazılı, kodun onları runtime'da doldurmasına gerek yok.

        [SerializeField, Tooltip("Her günün event adının YAZILACAĞI TextMesh (16 adet, index 0 = gün 1). " +
                                 "Sahnedeki hazır TextMesh'ler buraya atanır; kod hiçbir obje ÜRETMEZ, " +
                                 "yalnız bu alanların .text'ini doldurur. O gün event yoksa alan boşaltılır.")]
        public TMP_Text[] eventTexts = new TMP_Text[CALENDAR_CELL_COUNT];

        [SerializeField, Tooltip("Gün arka plan imajları (16 adet, index 0 = gün 1). " +
                                 "Opsiyonel — atanmayan slotlar bugünü vurgulamaz, başka bir şey bozulmaz.")]
        public Image[] dayBackgrounds = new Image[CALENDAR_CELL_COUNT];

        #endregion

        #region Serialized Fields - Colors

        [Header("=== DAY HIGHLIGHT COLORS ===")]
        [SerializeField, Tooltip("Bugünün arka plan rengi")]
        public Color currentDayColor = Color.green;

        [SerializeField, Tooltip("Normal günlerin arka plan rengi")]
        public Color normalDayColor = Color.white;

        [Header("=== EVENT TYPE TEXT COLORS (2026-09-25, graphics-ui Faz 2) ===")]
        [SerializeField, Tooltip("Positive event adının rengi (bkz. WriteEventText).")]
        public Color positiveEventTextColor = new Color(0.16f, 0.55f, 0.16f);

        [SerializeField, Tooltip("Negative event adının rengi.")]
        public Color negativeEventTextColor = new Color(0.75f, 0.15f, 0.15f);

        [SerializeField, Tooltip("Neutral event adının rengi (pozitif/negatif ile karışmasın diye amber).")]
        public Color neutralEventTextColor = new Color(0.85f, 0.6f, 0.05f);

        [Header("=== DAY BOX COLOR MARKER (GOLDEN BOX DAY / MONOCHROME DAY, Faz 2) ===")]
        [SerializeField, Tooltip("GetColorForDay() BoxInfo.BoxType.Red döndürünce hücrede gösterilen işaret rengi. " +
                                 "NetworkWorldItem.cs/GetColorForBoxType ile aynı palet.")]
        public Color redBoxDayColor = new Color(0.8f, 0.2f, 0.2f);

        [SerializeField, Tooltip("GetColorForDay() BoxInfo.BoxType.Yellow döndürünce hücrede gösterilen işaret rengi.")]
        public Color yellowBoxDayColor = new Color(0.9f, 0.8f, 0.2f);

        [SerializeField, Tooltip("GetColorForDay() BoxInfo.BoxType.Blue döndürünce hücrede gösterilen işaret rengi.")]
        public Color blueBoxDayColor = new Color(0.2f, 0.4f, 0.8f);

        #endregion

        #region Serialized Fields - Prefabs

        // `eventTextPrefab` KALDIRILDI: event yazısı artık prefab'tan üretilmiyor,
        // sahnedeki hazır TextMesh'e yazılıyor (bkz. eventTexts).

        #endregion

        #region Serialized Fields - UI

        [Header("=== CALENDAR PANEL ===")]
        [SerializeField, Tooltip("Takvim paneli")]
        public GameObject calendarPanel;

        [SerializeField, Tooltip("Çıkış butonu")]
        public Button exitButton;

        [Header("=== HAVA RAPORU (FORECAST) BUTONU — 2026-09-25, graphics-ui Faz 2 ===")]
        [SerializeField, Tooltip("HAVA RAPORU butonu. Görünürlük/interactable koşulu (RefreshForecastButtonState): " +
                                 "UpgradePanel.Instance.IsForecastCardOwned && IsForecastAvailableThisPeriod && " +
                                 "IsTomorrowNegative. Atanmazsa buton hiç çalışmaz (opsiyonel, sessiz no-op).")]
        public Button forecastButton;

        [SerializeField, Tooltip("Forecast butonunun etiketi (opsiyonel). Atanırsa 'ForecastButtonLabel' " +
                                 "loc key'i ile doldurulur — StringTable'a bu anahtar müdür tarafından eklenmeli.")]
        public TMP_Text forecastButtonLabel;

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

        // Katalog: docs/economy/her-gun-event-ve-kart-tasarimi-2026-09-24.md §B (22 event) +
        // KULLANICI KARARI S5 (Karışık Sevkiyat, 23. event). Sıra korunur, yeni event'ler SONA
        // eklenir (bkz. plans/her-gun-event-ve-kart-yenileme.md "İKİSİNİN DE SONUNA" kuralı) —
        // EventEffectManager.eventNames ile aynı sırada olmalı.
        private readonly List<GameEvent> _allEvents = new()
        {
            // ---- Mevcut 16 (değerleri/şiddeti §B ile güncellendi) ----
            new GameEvent("BUSY DAY", "EventBusyDay", EventType.Neutral, EventSeverity.Medium, "CUSTOMER SPAWN RATE INCREASES BY 35%, PATIENCE DECREASES BY 15%.", "EventBusyDayDesc"),
            new GameEvent("DELIVERY BONUS", "EventDeliveryBonus", EventType.Positive, EventSeverity.Light, "EARN 20% MORE MONEY PER DELIVERY.", "EventDeliveryBonusDesc"),
            new GameEvent("ANGRY CUSTOMERS", "EventAngryCustomers", EventType.Negative, EventSeverity.Light, "CUSTOMER PATIENCE DECREASES BY 40%, 10% MORE CUSTOMERS ARRIVE.", "EventAngryCustomersDesc"),
            new GameEvent("RELAXED DAY", "EventRelaxedDay", EventType.Positive, EventSeverity.Light, "CUSTOMER PATIENCE INCREASES BY 50%, ALL PENALTIES HALVED.", "EventRelaxedDayDesc"),
            new GameEvent("SLOW LOGISTICS", "EventSlowLogistics", EventType.Negative, EventSeverity.Light, "TRUCKS TAKE TWICE AS LONG TO LEAVE AND EARN 10% LESS PER BOX.", "EventSlowLogisticsDesc"),
            new GameEvent("EXPRESS CARGO", "EventExpressCargo", EventType.Positive, EventSeverity.Light, "TRUCKS LEAVE THE SCENE 50% FASTER, EARN 10% MORE PER BOX, AND STAY 20% LONGER IN THE HANGAR.", "EventExpressCargoDesc"),
            new GameEvent("HEAVY BOXES", "EventHeavyBoxes", EventType.Negative, EventSeverity.Medium, "MOVEMENT SPEED DECREASES BY 15% AND SPRINT SPEED BY 20%.", "EventHeavyBoxesDesc"),
            new GameEvent("GOLDEN BOX DAY", "EventGoldenBoxDay", EventType.Positive, EventSeverity.Light, "TODAY'S COLOR BOXES EARN 60% MORE.", "EventGoldenBoxDayDesc"),
            new GameEvent("OPPORTUNITY DAY", "EventOpportunityDay", EventType.Positive, EventSeverity.Light, "UPGRADE COSTS DECREASE BY 30%.", "EventOpportunityDayDesc"),
            new GameEvent("FATIGUE PROBLEM", "EventFatigueProblem", EventType.Negative, EventSeverity.Light, "MOVEMENT SPEED DECREASES BY 10%, SPRINT SPEED BY 30%; STAMINA REGENERATES 40% SLOWER.", "EventFatigueProblemDesc"),
            new GameEvent("VIP SERVICE", "EventVipService", EventType.Positive, EventSeverity.Medium, "ALL CUSTOMERS REQUEST 2 ITEMS TODAY.", "EventVipServiceDesc", minDay: 1, maxDay: 8),
            new GameEvent("SURPRISE AUDIT", "EventSurpriseAudit", EventType.Negative, EventSeverity.Medium, "ALL FAULTY OPERATIONS PENALIZE DOUBLE.", "EventSurpriseAuditDesc"),
            new GameEvent("RAINY DAY", "EventRainyDay", EventType.Negative, EventSeverity.Medium, "20% FEWER CUSTOMERS ARRIVE.", "EventRainyDayDesc"),
            new GameEvent("MARKETING DAY", "EventMarketingDay", EventType.Negative, EventSeverity.Hard, "20% MORE CUSTOMERS, BUT 30% LESS EARNINGS.", "EventMarketingDayDesc"),
            new GameEvent("CUSTOMER SUPPORT", "EventCustomerSupport", EventType.Positive, EventSeverity.Light, "RECEPTION PHONE CALLS DON'T COST ANY TIME.", "EventCustomerSupportDesc"),
            new GameEvent("FESTIVAL DAY", "EventFestivalDay", EventType.Positive, EventSeverity.Medium, "RANDOM BONUS IS EARNED AT DAY START.", "EventFestivalDayDesc", minDay: 5),

            // ---- Yeni 7 (6 planlanan + Karışık Sevkiyat, KULLANICI KARARI S5) — SONA eklendi ----
            new GameEvent("MONOCHROME DAY", "EventMonochromeDay", EventType.Positive, EventSeverity.Light, "ALL TRUCKS AND CUSTOMERS WANT TODAY'S SINGLE COLOR.", "EventMonochromeDayDesc"),
            new GameEvent("QUEST DAY", "EventQuestDay", EventType.Positive, EventSeverity.Light, "COMPLETED QUEST REWARDS (MONEY + PRESTIGE) ARE TRIPLED.", "EventQuestDayDesc"),
            new GameEvent("RUSH BONUS", "EventRushBonus", EventType.Positive, EventSeverity.Medium, "BOXES DELIVERED IN THE FIRST HALF OF A TRUCK'S HANGAR TIME EARN 40% MORE.", "EventRushBonusDesc"),
            new GameEvent("IMPATIENT DRIVERS", "EventImpatientDrivers", EventType.Negative, EventSeverity.Medium, "TRUCKS STAY 40% LESS TIME IN THE HANGAR.", "EventImpatientDriversDesc"),
            new GameEvent("RETURN WAVE", "EventReturnWave", EventType.Negative, EventSeverity.Hard, "CUSTOMERS REQUESTING A RETURN NEARLY DOUBLE.", "EventReturnWaveDesc", minDay: 5),
            new GameEvent("SUPPLY STRIKE", "EventSupplyStrike", EventType.Negative, EventSeverity.Hard, "30% FEWER CUSTOMERS ARRIVE AND EARNINGS DROP 10%.", "EventSupplyStrikeDesc"),
            new GameEvent("MIXED SHIPMENT", "EventMixedShipment", EventType.Neutral, EventSeverity.Medium, "TODAY'S TRUCKS CAN REQUEST MIXED COLORS.", "EventMixedShipmentDesc", minDay: 5, maxDay: 11),
        };

        private readonly List<int> _randomEventDays = new();
        private readonly Dictionary<int, GameEvent> _eventsByDay = new();

        /// <summary>
        /// GOLDEN BOX DAY / MONOCHROME DAY günleri için takvim üretimi sırasında AYNI seed'den
        /// çekilen "günün rengi". §B "Netcode" notu: server ile client'lar aynı seed'den aynı rengi
        /// üretir, ayrıca senkron gerekmez.
        /// </summary>
        private readonly Dictionary<int, BoxInfo.BoxType> _dayColorByDay = new();

        #endregion

        #region Private Fields - Rent

        private GameEconomySettings _economySettings;

        private int RentIntervalDays
        {
            get
            {
                if (_economySettings == null)
                {
                    _economySettings = Resources.Load<GameEconomySettings>("EkonomiAyarlari");
                }
                return _economySettings != null ? _economySettings.rentIntervalDays : 4;
            }
        }

        private bool IsRentDay(int day) => day % RentIntervalDays == 0;

        #endregion

        #region Private Fields - State

        private bool _isPanelOpen;
        private bool _isAnimating;
        private PlayerMovement _currentPlayer;

        /// <summary>eventTexts'in event-dışı orijinal rengi (bkz. CacheDefaultEventTextColors).</summary>
        private Color[] _defaultEventTextColors;

        private readonly NetworkVariable<int> _calendarSeed = new NetworkVariable<int>(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> _calendarBaseDay = new NetworkVariable<int>(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private bool _calendarGenerated;

        // HAVA RAPORU (forecast, §D, 2026-09-25, gameplay-B kartı — UpgradePanel.OnForecastConsumedServer):
        // gün-1 indeksli (index = day-1), CALENDAR_CELL_COUNT uzunlukta, -1 = override yok. Takvim
        // üretimi seed'den DETERMİNİSTİK olduğu için (server/client aynı sonucu üretir) override'ı
        // ayrı bir NetworkList'te tutmak gerekiyor — üretimi yeniden çalıştırmak override'ı kaybeder.
        // NetworkList late-join client'a TAM state sync sağladığından host/client/geç-katılan
        // otomatik tutarlı olur (ekstra ClientRpc gerekmez). UpgradePanel'deki NetworkList kuruluş
        // deseniyle aynı: field initializer DEĞİL, Awake()'te construct edilir (bkz. InitializeAnimator vb.).
        private NetworkList<int> _forecastOverrideEventIndex;
        // Paralel liste: override edilen event GOLDEN BOX DAY/MONOCHROME DAY ise o günün rengi
        // (BoxInfo.BoxType). -1 = renk yok/gerekmez.
        private NetworkList<int> _forecastOverrideColorIndex;

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
            InitializeForecastButton();
            CacheAnimationDurations();
            CacheDefaultEventTextColors();
            ValidateCellWiring();

            _forecastOverrideEventIndex = new NetworkList<int>();
            _forecastOverrideColorIndex = new NetworkList<int>();
        }

#if UNITY_EDITOR
        /// <summary>
        /// Dizileri Inspector'da her zaman tam 16 slot olarak tutar (index 0 = gün 1).
        /// Elle boyut değiştirilirse mevcut atamalar korunarak 16'ya geri çekilir.
        /// </summary>
        private void OnValidate()
        {
            ResizePreservingAssignments(ref eventTexts);
            ResizePreservingAssignments(ref dayBackgrounds);
        }

        private static void ResizePreservingAssignments<T>(ref T[] array) where T : class
        {
            if (array != null && array.Length == CALENDAR_CELL_COUNT) return;

            var resized = new T[CALENDAR_CELL_COUNT];
            if (array != null)
            {
                int copyCount = Mathf.Min(array.Length, CALENDAR_CELL_COUNT);
                for (int i = 0; i < copyCount; i++) resized[i] = array[i];
            }
            array = resized;
        }
#endif

        /// <summary>
        /// Atanmamış takvim slotlarını TEK bir uyarıda bildirir. Bu hata sınıfı sessizdi:
        /// eventTexts'te boş bırakılan gün, o gün event olsa bile ekranda hiçbir şey
        /// göstermiyor ve hiçbir log basmıyordu.
        /// </summary>
        private void ValidateCellWiring()
        {
            if (eventTexts == null || eventTexts.Length == 0)
            {
                LogWarning("eventTexts hiç atanmamış — takvimde hiçbir event yazısı görünmeyecek.");
                return;
            }

            var missing = new List<int>();
            for (int i = 0; i < eventTexts.Length && i < CALENDAR_CELL_COUNT; i++)
            {
                if (eventTexts[i] == null) missing.Add(startDay + i);
            }

            if (missing.Count > 0)
            {
                LogWarning($"eventTexts eksik — şu günlerde event yazısı GÖRÜNMEYECEK: " +
                           $"{string.Join(", ", missing)}. (Inspector: EventCalendarUI > Calendar Cells)");
            }
        }

        private void Start()
        {
            InitializeCalendarPanel();
            UpdateCalendarUI();
            SubscribeToDayCycleEvents();
            SubscribeToLocaleEvents();
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            UnsubscribeFromDayCycleEvents();
            UnsubscribeFromLocaleEvents();
            CleanupExitButton();
            CleanupForecastButton();
            ClearSpawnedEventTexts();
        }

        #endregion

        #region Network Lifecycle

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                // Seed ve base-day AYNI frame'de set edilir ki client'a tutarlı bir snapshot gitsin
                // (üretim artık mutable startDay'e değil, üretim anındaki bu sabit base-day'e bağlı).
                _calendarBaseDay.Value = startDay;
                _calendarSeed.Value = UnityEngine.Random.Range(1, int.MaxValue);
                EnsureCalendarGenerated();

                // HAVA RAPORU: -1 (override yok) ile CALENDAR_CELL_COUNT eleman — server-only yazım,
                // NetworkList late-join client'a otomatik tam-state sync sağlar.
                for (int i = 0; i < CALENDAR_CELL_COUNT; i++)
                {
                    _forecastOverrideEventIndex.Add(-1);
                    _forecastOverrideColorIndex.Add(-1);
                }

                UpgradePanel.OnForecastConsumedServer += HandleForecastConsumedServer;
            }
            else
            {
                if (_calendarSeed.Value > 0)
                {
                    EnsureCalendarGenerated();
                }
                else
                {
                    _calendarSeed.OnValueChanged += HandleCalendarSeedChanged;
                }
            }

            // Her peer (host dahil) takvim UI'ını override değiştiğinde tazeler.
            _forecastOverrideEventIndex.OnListChanged += HandleForecastOverrideChanged;
        }

        public override void OnNetworkDespawn()
        {
            _calendarSeed.OnValueChanged -= HandleCalendarSeedChanged;
            _forecastOverrideEventIndex.OnListChanged -= HandleForecastOverrideChanged;
            if (IsServer)
            {
                UpgradePanel.OnForecastConsumedServer -= HandleForecastConsumedServer;
            }
            base.OnNetworkDespawn();
        }

        private void HandleForecastOverrideChanged(NetworkListEvent<int> changeEvent)
        {
            UpdateCalendarUI();
        }

        private void HandleCalendarSeedChanged(int previousValue, int newValue)
        {
            if (newValue <= 0) return;

            EnsureCalendarGenerated();
            _calendarSeed.OnValueChanged -= HandleCalendarSeedChanged;
        }

        /// <summary>
        /// Takvimi (seed hazırsa) tek seferlik üretir ve UI'ı günceller. Çift-üretimi guard'lar.
        /// </summary>
        private void EnsureCalendarGenerated()
        {
            if (_calendarGenerated) return;
            if (_calendarSeed.Value <= 0) return;

            GenerateInitialEvents(_calendarSeed.Value, _calendarBaseDay.Value);
            _calendarGenerated = true;
            UpdateCalendarUI();
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

        /// <summary>
        /// HAVA RAPORU butonu — opsiyonel (bkz. forecastButton tooltip). Atanmamışsa sessizce
        /// no-op kalır, exitButton'daki gibi zorunlu bir uyarı BASILMAZ çünkü buton sahnede henüz
        /// eklenmemiş olabilir (bkz. rapor).
        /// </summary>
        private void InitializeForecastButton()
        {
            if (forecastButton != null)
            {
                forecastButton.onClick.AddListener(HandleForecastButtonClicked);
            }
        }

        private void CleanupForecastButton()
        {
            if (forecastButton != null)
            {
                forecastButton.onClick.RemoveListener(HandleForecastButtonClicked);
            }
        }

        private void HandleForecastButtonClicked()
        {
            UpgradePanel.Instance?.RequestUseForecast();
        }

        /// <summary>
        /// HAVA RAPORU butonunun görünürlük/interactable durumunu üç koşulun BİRLİKTE AND'iyle
        /// belirler (bkz. forecastButton tooltip). UpdateCalendarUI'dan çağrılır — bu da gün
        /// değişimi, forecast override NetworkList değişimi ve locale değişiminde zaten tetikleniyor.
        /// </summary>
        private void RefreshForecastButtonState()
        {
            if (forecastButton == null) return;

            bool visible = UpgradePanel.Instance != null
                           && UpgradePanel.Instance.IsForecastCardOwned
                           && UpgradePanel.Instance.IsForecastAvailableThisPeriod
                           && IsTomorrowNegative;

            forecastButton.gameObject.SetActive(visible);
            forecastButton.interactable = visible;

            if (forecastButtonLabel != null)
            {
                forecastButtonLabel.text = LocalizationHelper.GetLocalizedString("ForecastButtonLabel");
            }
        }

        /// <summary>
        /// eventTexts'in sahnedeki ORİJİNAL (event-dışı) rengini bir kere yakalar. WriteEventText
        /// bir event günü için rengi değiştirdiğinde, event'siz/kira günü dönüşünde buraya geri
        /// dönülür — ClearSpawnedEventTexts yalnız .text'i boşaltır, .color'a dokunmaz.
        /// </summary>
        private void CacheDefaultEventTextColors()
        {
            if (eventTexts == null) return;

            _defaultEventTextColors = new Color[eventTexts.Length];
            for (int i = 0; i < eventTexts.Length; i++)
            {
                if (eventTexts[i] != null) _defaultEventTextColors[i] = eventTexts[i].color;
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

        private void SubscribeToLocaleEvents()
        {
            LocalizationSettings.SelectedLocaleChanged += HandleLocaleChanged;
        }

        private void UnsubscribeFromLocaleEvents()
        {
            LocalizationSettings.SelectedLocaleChanged -= HandleLocaleChanged;
        }

        private void HandleLocaleChanged(Locale newLocale)
        {
            LogDebug($"Locale changed to: {newLocale?.Identifier.Code ?? "null"}");
            UpdateCalendarUI();
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
            RefreshForecastButtonState();

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

        /// <summary>
        /// Bant tanımı (§C): baseDay'e göre GÖRECELİ gün ofsetleri (0 = baseDay = gün 1) + slot
        /// dizisi. 'P' = pozitif/takas havuzu, 'N' = negatif havuz, 'X' = %50/%50 yazı-tura.
        /// posLightOnly = true ise pozitif havuz yalnız EventSeverity.Light'a daralır (öğretici bant).
        /// negSeverities = null ise o bantta hiç negatif seçilmez (öğretici bant, N/X hiç yok).
        /// </summary>
        private readonly struct DayBand
        {
            public readonly int[] dayOffsets;
            public readonly char[] slots;
            public readonly bool posLightOnly;
            public readonly EventSeverity[] negSeverities;

            public DayBand(int[] dayOffsets, char[] slots, bool posLightOnly, EventSeverity[] negSeverities)
            {
                this.dayOffsets = dayOffsets;
                this.slots = slots;
                this.posLightOnly = posLightOnly;
                this.negSeverities = negSeverities;
            }
        }

        private static readonly DayBand[] DayBands =
        {
            // Öğretici: gün 1-3. Yalnız hafif pozitifler, negatif yok.
            new DayBand(new[] { 0, 1, 2 }, new[] { 'P', 'P', 'P' }, true, null),
            // Kira-1 sonrası: gün 5-7. Negatif havuz yalnız hafif.
            new DayBand(new[] { 4, 5, 6 }, new[] { 'P', 'N', 'X' }, false, new[] { EventSeverity.Light }),
            // Kira-2 sonrası: gün 9-11. Negatif havuz hafif+orta.
            new DayBand(new[] { 8, 9, 10 }, new[] { 'P', 'N', 'X' }, false, new[] { EventSeverity.Light, EventSeverity.Medium }),
            // Final: gün 13-15. Negatif havuz orta+sert.
            new DayBand(new[] { 12, 13, 14 }, new[] { 'P', 'N', 'N' }, false, new[] { EventSeverity.Medium, EventSeverity.Hard }),
        };

        /// <summary>
        /// Takvimi bant kuralına göre (§C) üretir: kira-dışı her gün (1-3, 5-7, 9-11, 13-15) tam 1
        /// event, koşu içinde tekrar yok, seed'li ve deterministik (host/client aynı takvimi üretir —
        /// System.Random kullanır, UnityEngine.Random KULLANILMAZ, peer'ler arası senkron gerekir).
        /// baseDay, üretim anında server'ın senkronladığı SABİT gündür — mutable `startDay` alanı
        /// KULLANILMAZ, çünkü late-join client'larda `startDay` katılım anının günü olabilir ve
        /// server'ın üretimde kullandığı base'den sapar (takvim divergence'ı).
        /// </summary>
        private void GenerateInitialEvents(int seed, int baseDay)
        {
            _randomEventDays.Clear();
            _eventsByDay.Clear();
            _dayColorByDay.Clear();

            System.Random rng = new System.Random(seed);
            var usedEventNames = new HashSet<string>();

            foreach (DayBand band in DayBands)
            {
                char[] slots = (char[])band.slots.Clone();
                ShuffleSlots(slots, rng);

                for (int i = 0; i < band.dayOffsets.Length; i++)
                {
                    int day = baseDay + band.dayOffsets[i];
                    char slot = slots[i];

                    // Kira günü asla event almaz (emniyet — bant tanımları zaten kira günlerine denk gelmez).
                    if (IsRentDay(day)) continue;

                    bool wantPositive = slot == 'P' || (slot == 'X' && rng.Next(2) == 0);

                    GameEvent selected = wantPositive
                        ? PickFromPool(rng, isNegativePool: false, band.posLightOnly ? new[] { EventSeverity.Light } : null, day, usedEventNames)
                        : PickFromPool(rng, isNegativePool: true, band.negSeverities, day, usedEventNames);

                    if (selected == null) continue; // havuz tükendi (beklenmez, 12 slot / 23 event) — o gün event'siz kalır

                    usedEventNames.Add(selected.name);
                    _randomEventDays.Add(day);
                    _eventsByDay[day] = selected;

                    if (selected.name == "GOLDEN BOX DAY" || selected.name == "MONOCHROME DAY")
                    {
                        _dayColorByDay[day] = (BoxInfo.BoxType)rng.Next(0, 3);
                    }
                }
            }

            _randomEventDays.Sort();
        }

        /// <summary>
        /// Tip (pozitif/takas havuzu vs negatif havuz) korunarak, şiddet kısıtı + gün kısıtı +
        /// kullanılmamışlık ile aday listesi daraltılır. Aday kalmazsa şiddet kısıtı gevşetilir
        /// (§C emniyet kuralı, tip kısıtı korunur); o da boşsa gün kısıtı da gevşetilir (son çare).
        /// </summary>
        private GameEvent PickFromPool(System.Random rng, bool isNegativePool, EventSeverity[] allowedSeverities, int day, HashSet<string> usedEventNames)
        {
            List<GameEvent> candidates = FilterPool(isNegativePool, allowedSeverities, day, usedEventNames, ignoreDayRange: false);
            if (candidates.Count == 0)
            {
                candidates = FilterPool(isNegativePool, null, day, usedEventNames, ignoreDayRange: false);
            }
            if (candidates.Count == 0)
            {
                candidates = FilterPool(isNegativePool, null, day, usedEventNames, ignoreDayRange: true);
            }
            if (candidates.Count == 0) return null;

            return candidates[rng.Next(0, candidates.Count)];
        }

        private List<GameEvent> FilterPool(bool isNegativePool, EventSeverity[] allowedSeverities, int day, HashSet<string> usedEventNames, bool ignoreDayRange)
        {
            var result = new List<GameEvent>();
            foreach (GameEvent e in _allEvents)
            {
                bool typeMatches = isNegativePool ? e.type == EventType.Negative : e.type != EventType.Negative;
                if (!typeMatches) continue;
                if (usedEventNames.Contains(e.name)) continue;
                if (!ignoreDayRange && (day < e.minDay || day > e.maxDay)) continue;
                if (allowedSeverities != null && System.Array.IndexOf(allowedSeverities, e.severity) < 0) continue;

                result.Add(e);
            }
            return result;
        }

        private static void ShuffleSlots(char[] slots, System.Random rng)
        {
            for (int i = slots.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (slots[i], slots[j]) = (slots[j], slots[i]);
            }
        }

        #endregion

        #region Forecast (Hava Raporu — gameplay-B kartı arayüzü)

        /// <summary>
        /// UpgradePanel.OnForecastConsumedServer server-side tetiklenince çağrılır (bkz. UpgradePanel.cs
        /// RequestUseForecast/UseForecastServerRpc — kart sahipliği/dönem kilidi ORADA kontrol edildi).
        /// Yarının event'i (override dahil) Negative DEĞİLSE no-op (idempotent güvenlik — buton UI'da
        /// yalnız yarın negatifken görünür ama çift-tık/gecikme senaryosuna karşı savunmacı).
        /// Takvim seed'den deterministik üretildiği için override AYRI bir NetworkList'te tutulur —
        /// _allEvents/_eventsByDay'i DEĞİŞTİRMEZ (yeniden üretim override'ı kaybetmesin diye).
        /// </summary>
        private void HandleForecastConsumedServer()
        {
            if (!IsServer) return;

            int tomorrow = (DayCycleManager.Instance != null ? DayCycleManager.Instance.currentDay : startDay) + 1;
            if (tomorrow < 1 || tomorrow > CALENDAR_CELL_COUNT) return;
            if (IsRentDay(tomorrow)) return;

            GameEvent currentTomorrowEvent = GetEventForDay(tomorrow);
            if (currentTomorrowEvent == null || currentTomorrowEvent.type != EventType.Negative) return;

            // Bu koşuda kullanılmış TÜM event adları (orijinal takvim + önceki forecast override'ları)
            // — "koşuda henüz çekilmemiş" kuralı korunur.
            var usedEventNames = new HashSet<string>();
            foreach (GameEvent e in _eventsByDay.Values) usedEventNames.Add(e.name);
            for (int i = 0; i < _forecastOverrideEventIndex.Count; i++)
            {
                int idx = _forecastOverrideEventIndex[i];
                if (idx >= 0 && idx < _allEvents.Count) usedEventNames.Add(_allEvents[idx].name);
            }

            bool lightOnly = GetPosLightOnlyForDay(tomorrow);

            // Bu seçim koşu-tohumlu DETERMİNİZM gerektirmez (sonuç NetworkList ile replike edilir,
            // her peer sonucu okur — kendi üretmez), UnityEngine.Random güvenle kullanılabilir.
            System.Random rng = new System.Random(UnityEngine.Random.Range(1, int.MaxValue));
            GameEvent replacement = PickFromPool(rng, isNegativePool: false,
                lightOnly ? new[] { EventSeverity.Light } : null, tomorrow, usedEventNames);
            if (replacement == null) return; // havuz tükendi (olağan dışı) — takvim değişmeden kalır

            int replacementIndex = _allEvents.IndexOf(replacement);
            if (replacementIndex < 0) return;

            _forecastOverrideEventIndex[tomorrow - 1] = replacementIndex;

            if (replacement.name == "GOLDEN BOX DAY" || replacement.name == "MONOCHROME DAY")
            {
                _forecastOverrideColorIndex[tomorrow - 1] = UnityEngine.Random.Range(0, 3);
            }
        }

        /// <summary>Verilen günün bandı öğreticiyse (posLightOnly) true; bant bulunamazsa false (kısıtsız).</summary>
        private bool GetPosLightOnlyForDay(int day)
        {
            foreach (DayBand band in DayBands)
            {
                foreach (int offset in band.dayOffsets)
                {
                    if (_calendarBaseDay.Value + offset == day) return band.posLightOnly;
                }
            }
            return false;
        }

        private GameEvent GetForecastOverrideEvent(int day)
        {
            if (day < 1 || day > CALENDAR_CELL_COUNT) return null;
            int i = day - 1;
            if (i >= _forecastOverrideEventIndex.Count) return null;

            int idx = _forecastOverrideEventIndex[i];
            return (idx >= 0 && idx < _allEvents.Count) ? _allEvents[idx] : null;
        }

        private bool TryGetForecastOverrideColor(int day, out BoxInfo.BoxType color)
        {
            color = default;
            if (day < 1 || day > CALENDAR_CELL_COUNT) return false;
            int i = day - 1;
            if (i >= _forecastOverrideColorIndex.Count) return false;

            int c = _forecastOverrideColorIndex[i];
            if (c < 0) return false;

            color = (BoxInfo.BoxType)c;
            return true;
        }

        /// <summary>
        /// HAVA RAPORU buton görünürlüğü/tıklanabilirliği için: yarının event'i (override dahil)
        /// Negative mi? Kart sahipliği/dönem hakkı BURADA bilinmiyor — UI tarafı
        /// UpgradePanel.IsForecastCardOwned && IsForecastAvailableThisPeriod && IsTomorrowNegative
        /// üçünü BİRLİKTE kontrol etmeli. SerializeField/buton sahnede henüz yok — graphics-ui/müdür
        /// bağlayacak (bkz. rapor).
        /// </summary>
        public bool IsTomorrowNegative
        {
            get
            {
                int tomorrow = (DayCycleManager.Instance != null ? DayCycleManager.Instance.currentDay : startDay) + 1;
                GameEvent evt = GetEventForDay(tomorrow);
                return evt != null && evt.type == EventType.Negative;
            }
        }

        #endregion

        #region Day Cycle Handler

        private void HandleNewDay()
        {
            // startDay ARTIK burada güncellenmez: takvim sanatı sabit (dayBackgrounds[i] = daima
            // "Gün i+1" görseli, bkz. PopulateCalendarCells/HighlightCurrentDay). startDay'i her
            // gün currentDay'e eşitlemek index 0'ı sürekli "bugün" gibi yeşil gösteriyordu.
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
            HighlightCurrentDay();
            RefreshForecastButtonState();
        }

        /// <summary>
        /// Tüm gün hücrelerinin event yazısını boşaltır. Obje YOK EDİLMEZ — TextMesh'ler
        /// sahnenin kalıcı parçası, yalnız içerikleri temizlenir. Renk BURADA SIFIRLANMAZ —
        /// WriteEventText event yoksa/kira günüyse rengi kendi orijinaline (_defaultEventTextColors)
        /// döndürür, aksi halde önceki bir Positive/Negative/Neutral rengi kalıcı olarak yapışır kalırdı.
        /// </summary>
        private void ClearSpawnedEventTexts()
        {
            if (eventTexts == null) return;

            for (int i = 0; i < eventTexts.Length; i++)
            {
                if (eventTexts[i] != null) eventTexts[i].text = string.Empty;
            }
        }

        private void PopulateCalendarCells()
        {
            if (eventTexts == null) return;

            int cellCount = Mathf.Min(eventTexts.Length, CALENDAR_CELL_COUNT);

            for (int i = 0; i < cellCount; i++)
            {
                WriteEventText(i, i + 1);
            }
        }

        /// <summary>
        /// O günde event varsa adını hücrenin TextMesh'ine yazar. Kira günüyse (event asla
        /// atanmaz, bkz. GenerateInitialEvents) "Kira Günü" etiketini yazar. İkisi de yoksa
        /// hiçbir şey yapmaz — hücre <see cref="ClearSpawnedEventTexts"/> ile zaten boşaltılmıştır.
        /// </summary>
        private void WriteEventText(int index, int day)
        {
            if (eventTexts[index] == null) return;

            // GetEventForDay HAVA RAPORU override'ını da kapsar (bkz. Forecast bölgesi) — takvim
            // metni override sonrası yenilenen event'i gösterir.
            GameEvent gameEvent = GetEventForDay(day);
            if (gameEvent != null)
            {
                eventTexts[index].color = GetEventTypeTextColor(gameEvent.type);
                eventTexts[index].text = BuildDayColorMarker(day) + gameEvent.GetLocalizedName();
                return;
            }

            ResetEventTextColor(index);

            if (IsRentDay(day))
            {
                eventTexts[index].text = LocalizationHelper.GetLocalizedString("RentDay");
            }
        }

        /// <summary>Positive/Negative/Neutral event tipine göre metin rengi (bkz. Faz 2 renk alanları).</summary>
        private Color GetEventTypeTextColor(EventType type)
        {
            switch (type)
            {
                case EventType.Positive: return positiveEventTextColor;
                case EventType.Negative: return negativeEventTextColor;
                default: return neutralEventTextColor;
            }
        }

        /// <summary>
        /// GOLDEN BOX DAY / MONOCHROME DAY gibi günlerde GetColorForDay() bir BoxInfo.BoxType
        /// döndürüyorsa, hücreye event adının ÖNÜNE rich-text renkli bir kare işareti ekler
        /// (■). Yeni bir UI objesi gerektirmez — mevcut eventTexts TextMesh'i m_isRichText: 1
        /// olduğu için <color=#RRGGBB> etiketini doğrudan render eder.
        /// </summary>
        private string BuildDayColorMarker(int day)
        {
            BoxInfo.BoxType? dayColor = GetColorForDay(day);
            if (!dayColor.HasValue) return string.Empty;

            Color markerColor = dayColor.Value switch
            {
                BoxInfo.BoxType.Red => redBoxDayColor,
                BoxInfo.BoxType.Yellow => yellowBoxDayColor,
                BoxInfo.BoxType.Blue => blueBoxDayColor,
                _ => Color.white
            };

            return $"<color=#{ColorUtility.ToHtmlStringRGB(markerColor)}>■</color> ";
        }

        /// <summary>Event/kira etiketi olmayan bir hücrenin rengini sahnedeki orijinaline döndürür.</summary>
        private void ResetEventTextColor(int index)
        {
            if (_defaultEventTextColors == null || index >= _defaultEventTextColors.Length) return;
            eventTexts[index].color = _defaultEventTextColors[index];
        }

        /// <summary>
        /// Highlights the current day in the calendar
        /// </summary>
        private void HighlightCurrentDay()
        {
            int currentDay = DayCycleManager.Instance?.currentDay ?? 1;
            
            for (int i = 0; i < dayBackgrounds.Length && i < CALENDAR_CELL_COUNT; i++)
            {
                int day = i + 1;
                if (dayBackgrounds[i] != null)
                {
                    dayBackgrounds[i].color = (day == currentDay) ? currentDayColor : normalDayColor;
                }
            }
        }

        // `SpawnEventText` KALDIRILDI: event yazısı artık prefab'tan üretilmiyor, sahnedeki
        // hazır TextMesh'e yazılıyor (bkz. WriteEventText).

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
        /// Belirli bir gün için event bilgisini döndürür. HAVA RAPORU (forecast) override'ı varsa
        /// (bkz. _forecastOverrideEventIndex) takvimde üretilmiş orijinal event yerine ONU döndürür.
        /// </summary>
        public GameEvent GetEventForDay(int day)
        {
            GameEvent overrideEvent = GetForecastOverrideEvent(day);
            if (overrideEvent != null) return overrideEvent;

            return _eventsByDay.TryGetValue(day, out GameEvent gameEvent) ? gameEvent : null;
        }

        /// <summary>
        /// Belirli bir günde event olup olmadığını kontrol eder
        /// </summary>
        public bool HasEventOnDay(int day)
        {
            return GetEventForDay(day) != null;
        }

        /// <summary>
        /// Bugünün event'ini döndürür
        /// </summary>
        public GameEvent GetTodayEvent()
        {
            int today = DayCycleManager.Instance?.currentDay ?? startDay;
            return GetEventForDay(today);
        }

        /// <summary>
        /// Takvim (server'da senkron üretim, client'ta seed replikasyonu sonrası) üretildi mi?
        /// EventEffectManager'ın gün-1 event'ini koşu başında güvenle aktif edebilmesi için —
        /// iki NetworkObject'in OnNetworkSpawn sırası garanti değildir, bkz. EventEffectManager.
        /// </summary>
        public bool IsCalendarGenerated => _calendarGenerated;

        /// <summary>
        /// GOLDEN BOX DAY / MONOCHROME DAY günü için takvim üretimindeki aynı seed'den çekilmiş
        /// "günün rengi". O gün böyle bir event yoksa null. HAVA RAPORU override'ı GOLDEN BOX DAY/
        /// MONOCHROME DAY'e denk gelirse o günün rengi de override listesinden okunur.
        /// </summary>
        public BoxInfo.BoxType? GetColorForDay(int day)
        {
            if (TryGetForecastOverrideColor(day, out BoxInfo.BoxType overrideColor)) return overrideColor;

            return _dayColorByDay.TryGetValue(day, out BoxInfo.BoxType color) ? color : (BoxInfo.BoxType?)null;
        }

        /// <summary>
        /// SERİNLİK (cooler, §D, 2026-09-25): EventEffectManager'ın bir event'i hafifletip
        /// hafifletmeyeceğine (yalnız Negative) karar vermesi için katalogdan tip çözer. Bulunamazsa null.
        /// </summary>
        public EventType? GetEventTypeByName(string eventName)
        {
            foreach (GameEvent e in _allEvents)
            {
                if (e.name == eventName) return e.type;
            }
            return null;
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
            // Editor-only debug tool, not part of runtime network flow.
            int debugSeed = _calendarSeed.Value > 0 ? _calendarSeed.Value : UnityEngine.Random.Range(1, int.MaxValue);
            GenerateInitialEvents(debugSeed, startDay);
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
#endif

        #endregion
    }
}