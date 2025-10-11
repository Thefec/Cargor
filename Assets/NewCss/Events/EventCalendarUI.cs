using TMPro;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine.UI;

namespace NewCss
{
    public class EventCalendarUI : NetworkBehaviour
    {
        [Header("Calendar Cells")]
        public List<TMP_Text> dayNumberTexts = new List<TMP_Text>(); // 16 items
        public Transform[] eventSpawnPoints = new Transform[16];     // 1 position per cell

        [Header("Event Prefab")]
        public GameObject eventTextPrefab;

        [Header("Calendar Panel")]
        public GameObject calendarPanel;

        [Header("Exit Button")]
        public Button exitButton; // Exit butonu referansı

        [Header("Animation Settings")]
        [Tooltip("Animator component for panel animations")]
        public Animator panelAnimator;

        [Tooltip("Name of the close animation clip (used to determine length)")]
        public string closeAnimationClipName = "DateExit";

        [Tooltip("Name of the open animation clip (used to determine length)")]
        public string openAnimationClipName = "DateOpening";

        // Event types enum
        public enum EventType { Positive, Negative, Neutral }

        // Event class
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

        // Event list
        private List<GameEvent> allEvents = new List<GameEvent>
        {
            new GameEvent("BUSY DAY", EventType.Negative, "CUSTOMER SPAWN RATE INCREASES BY 30%."),
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
            new GameEvent("VIP SERVICE", EventType.Positive, "10% CHANCE BOXES ARE PERFECT AND EARN 10% MORE."),
            new GameEvent("SURPRISE AUDIT", EventType.Negative, "ALL FAULTY OPERATIONS PENALIZE DOUBLE."),
            new GameEvent("RAINY DAY", EventType.Positive, "20% FEWER CUSTOMERS ARRIVE."),
            new GameEvent("MARKETING DAY", EventType.Negative, "20% MORE CUSTOMERS, BUT 30% LESS EARNINGS."),
            new GameEvent("CUSTOMER SUPPORT", EventType.Negative, "RECEPTION PHONE RINGS 30% MORE OFTEN."),
            new GameEvent("FESTIVAL DAY", EventType.Positive, "RANDOM BONUS IS EARNED AT DAY START.")
        };

        private List<int> randomEventDays = new List<int>();
        private Dictionary<int, GameEvent> eventsByDay = new Dictionary<int, GameEvent>();

        [Header("Control")]
        public int startDay = 1; // Day counter
        private List<GameObject> spawnedEventTexts = new List<GameObject>();

        // Animation control
        private bool isPanelOpen = false;
        private bool isAnimating = false;

        // Player reference
        private PlayerMovement currentPlayer;

        private void Awake()
        {
            // Auto-assign animator if not set
            if (panelAnimator == null && calendarPanel != null)
            {
                panelAnimator = calendarPanel.GetComponent<Animator>();
                if (panelAnimator == null)
                    Debug.LogWarning("panelAnimator not assigned and calendarPanel has no Animator!", this);
            }

            // Exit button listener ekle
            if (exitButton != null)
            {
                exitButton.onClick.AddListener(OnExitButtonClicked);
            }
            else
            {
                Debug.LogWarning("Exit Button not assigned in EventCalendarUI!", this);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // Sadece local player'ın karakteri ise UI'ı aç
            if (other.CompareTag("Character"))
            {
                // NetworkBehaviour olan karakter kontrolü
                var networkObject = other.GetComponent<NetworkObject>();
                if (networkObject != null && networkObject.IsOwner)
                {
                    // Player referansını sakla
                    currentPlayer = other.GetComponent<PlayerMovement>();

                    // Eğer animasyon sırasındaysa bekle
                    if (isAnimating)
                    {
                        Debug.Log("Animation in progress, ignoring trigger enter");
                        return;
                    }

                    // Player hareketini kilitle
                    if (currentPlayer != null)
                    {
                        currentPlayer.LockMovement(true);
                    }

                    ShowCalendar();
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            // Trigger'dan çıkınca otomatik kapatma yok artık
            // Sadece player referansını temizle
            if (other.CompareTag("Character"))
            {
                var networkObject = other.GetComponent<NetworkObject>();
                if (networkObject != null && networkObject.IsOwner)
                {
                    // Player referansını kontrol et ve eşleşiyorsa temizle
                    var player = other.GetComponent<PlayerMovement>();
                    if (player == currentPlayer)
                    {
                        // Panel açıksa ve exit ediliyorsa, paneli kapat
                        if (isPanelOpen)
                        {
                            CloseCalendarAndUnlockPlayer();
                        }
                    }
                }
            }
        }

        // Exit button için callback
        private void OnExitButtonClicked()
        {
            Debug.Log("Exit button clicked");
            CloseCalendarAndUnlockPlayer();
        }

        // UI'ı kapat ve player'ı unlock et
        private void CloseCalendarAndUnlockPlayer()
        {
            if (!isPanelOpen) return;

            // UI'ı kapat
            HideCalendar();

            // Player hareketini geri aç
            if (currentPlayer != null)
            {
                currentPlayer.LockMovement(false);
            }
        }

        private void ShowCalendar()
        {
            if (isPanelOpen || isAnimating)
            {
                Debug.Log("Panel already open or animating, ignoring open request");
                return;
            }

            isPanelOpen = true;
            isAnimating = true;

            if (calendarPanel != null)
            {
                // Panel'i aktif et
                calendarPanel.SetActive(true);



                if (panelAnimator != null)
                {
                    // Animator state'ini temizle
                    panelAnimator.ResetTrigger("Open");
                    panelAnimator.ResetTrigger("Close");

                    // Açılma animasyonunu başlat
                    StartCoroutine(DelayedOpenAnimation());
                }
                else
                {
                    // Animator yoksa direkt aç
                    isAnimating = false;
                    Debug.Log("Calendar opened without animation");
                }
            }
        }

        private void HideCalendar()
        {
            if (!isPanelOpen || isAnimating)
            {
                Debug.Log("Panel not open or animating, ignoring close request");
                return;
            }

            isPanelOpen = false;
            isAnimating = true;


            if (calendarPanel != null)
            {
                if (panelAnimator != null)
                {
                    // Kapatma animasyonunu başlat
                    panelAnimator.ResetTrigger("Open");
                    panelAnimator.SetTrigger("Close");

                    // Animasyon bitene kadar bekle, sonra panel'i deaktif et
                    StartCoroutine(DisablePanelAfterAnimation());
                }
                else
                {
                    // Animator yoksa direkt kapat
                    calendarPanel.SetActive(false);
                    isAnimating = false;
                    Debug.Log("Calendar closed without animation");
                }
            }
        }

        // Geciktirilmiş açılma animasyonu
        private IEnumerator DelayedOpenAnimation()
        {
            // Bir frame bekle ki panel düzgün aktif olsun
            yield return null;

            if (panelAnimator != null)
            {
                panelAnimator.SetTrigger("Open");

                // Açılma animasyonu bitene kadar bekle
                yield return StartCoroutine(WaitForOpenAnimation());
            }
            else
            {
                isAnimating = false;
            }
        }

        // Açılma animasyonunun bitmesini bekle
        private IEnumerator WaitForOpenAnimation()
        {
            float waitTime = 0.5f; // default fallback

            if (panelAnimator != null)
            {
                // Animation clip uzunluğunu bul
                var ac = panelAnimator.runtimeAnimatorController;
                if (ac != null)
                {
                    foreach (var clip in ac.animationClips)
                    {
                        if (clip.name.Contains("Open") || clip.name.Contains("Opening") ||
                            clip.name == openAnimationClipName)
                        {
                            waitTime = clip.length;
                            break;
                        }
                    }
                }
            }

            yield return new WaitForSeconds(waitTime);

            // Açılma animasyonu bitti
            isAnimating = false;
            Debug.Log("Calendar open animation completed");
        }

        // Kapatma animasyonu bitince panel'i deaktif et
        private IEnumerator DisablePanelAfterAnimation()
        {
            float waitTime = 0.3f; // default fallback

            if (panelAnimator != null)
            {
                // Kapatma animasyon clip uzunluğunu bul
                var ac = panelAnimator.runtimeAnimatorController;
                if (ac != null)
                {
                    foreach (var clip in ac.animationClips)
                    {
                        if (clip.name.Contains("Exit") || clip.name.Contains("Close") ||
                            clip.name == closeAnimationClipName)
                        {
                            waitTime = clip.length;
                            break;
                        }
                    }
                }
            }

            Debug.Log($"Waiting {waitTime} seconds for close animation...");
            yield return new WaitForSeconds(waitTime);

            // Animasyon bittikten sonra panel'i deaktif et
            if (calendarPanel != null)
            {
                calendarPanel.SetActive(false);
                Debug.Log("Calendar panel disabled after close animation");
            }

            isAnimating = false;
        }

        // BONUS: Animation Event callback'leri (opsiyonel)
        public void OnOpenAnimationComplete()
        {
            isAnimating = false;
            Debug.Log("Calendar open animation completed via Animation Event");
        }

        public void OnCloseAnimationComplete()
        {
            if (calendarPanel != null)
                calendarPanel.SetActive(false);

            isAnimating = false;
            Debug.Log("Calendar close animation completed via Animation Event");
        }

        void Start()
        {
            if (calendarPanel != null)
                calendarPanel.SetActive(false);

            // Generate initial events
            GenerateInitialEvents();
            UpdateCalendarUI();

            // Subscribe to OnNewDay event if calendar should update as days pass:
            DayCycleManager.OnNewDay += OnNewDayHandler;
        }

        private void OnDestroy()
        {
            DayCycleManager.OnNewDay -= OnNewDayHandler;

            // Exit button listener'ını temizle
            if (exitButton != null)
            {
                exitButton.onClick.RemoveListener(OnExitButtonClicked);
            }
        }

        public override void OnNetworkSpawn()
        {
            // Network object spawn edildiğinde çalışır
            base.OnNetworkSpawn();
        }

        private void GenerateInitialEvents()
        {
            // Pre-generate events for first 50-100 days
            int maxDay = startDay + 100;
            int currentDay = startDay;
            int eventCount = 0;

            // Separate event types
            List<GameEvent> positiveEvents = allEvents.FindAll(e => e.type == EventType.Positive);
            List<GameEvent> negativeEvents = allEvents.FindAll(e => e.type == EventType.Negative);

            while (currentDay < maxDay)
            {
                // Add 3-4 days interval
                int interval = Random.Range(3, 5);
                currentDay += interval;

                // Rent day check (multiples of 6)
                if (currentDay % 6 == 0)
                {
                    // If rent day, shift event 1 day forward or backward
                    int adjustment = Random.Range(0, 2) == 0 ? -1 : 1;
                    int adjustedDay = currentDay + adjustment;

                    // Check shifted day is not rent day and positive
                    if (adjustedDay % 6 != 0 && adjustedDay > 0)
                    {
                        currentDay = adjustedDay;
                    }
                    else
                    {
                        // If both +1 and -1 are rent days, move 2 days forward
                        currentDay += 2;
                    }
                }

                // Add event day to list
                if (!randomEventDays.Contains(currentDay))
                {
                    randomEventDays.Add(currentDay);

                    GameEvent selectedEvent;

                    if (eventCount < 2)
                    {
                        // First 2 events guaranteed positive
                        selectedEvent = positiveEvents[Random.Range(0, positiveEvents.Count)];
                    }
                    else if (eventCount == 2)
                    {
                        // 3rd event guaranteed negative (to balance first 2 positive)
                        selectedEvent = negativeEvents[Random.Range(0, negativeEvents.Count)];
                    }
                    else
                    {
                        // After 4th event, random
                        selectedEvent = allEvents[Random.Range(0, allEvents.Count)];
                    }

                    // Assign event to day
                    eventsByDay[currentDay] = selectedEvent;
                    eventCount++;
                }
            }

            // Sort list
            randomEventDays.Sort();
        }

        private void OnNewDayHandler()
        {
            // Get day count from DayCycleManager
            startDay = DayCycleManager.Instance != null ? DayCycleManager.Instance.currentDay : startDay;
            UpdateCalendarUI();
        }

        public void UpdateCalendarUI()
        {
            // 1. Clear old texts
            foreach (var obj in spawnedEventTexts)
            {
                if (obj != null)
                    Destroy(obj);
            }
            spawnedEventTexts.Clear();

            // 2. Write day numbers and place events
            for (int i = 0; i < dayNumberTexts.Count; i++)
            {
                int currentDay = startDay + i;
                dayNumberTexts[i].text = currentDay.ToString();

                // Check random event
                bool hasRandomEvent = randomEventDays.Contains(currentDay);
                bool isRentDay = currentDay % 6 == 0;

                // Place random event if exists
                if (hasRandomEvent && eventsByDay.ContainsKey(currentDay))
                {
                    GameEvent gameEvent = eventsByDay[currentDay];

                    var go = Instantiate(eventTextPrefab, eventSpawnPoints[i].position, Quaternion.identity, eventSpawnPoints[i]);
                    var tmp = go.GetComponent<TMP_Text>();
                    tmp.text = gameEvent.name;
                    tmp.color = Color.white; // All events white color

                    spawnedEventTexts.Add(go);
                }

                // Place rent day
                if (isRentDay)
                {
                    // Instead of eventSpawnPoints[i].position, move down on Y axis a bit
                    Vector3 rentPosition = eventSpawnPoints[i].position;
                    var goRent = Instantiate(eventTextPrefab, rentPosition, Quaternion.identity, eventSpawnPoints[i]);
                    var tmp = goRent.GetComponent<TMP_Text>();
                    tmp.text = "Rent Day";
                    tmp.color = Color.red;
                    spawnedEventTexts.Add(goRent);
                }
            }
        }

        // Call this function when a new day starts
        public void AdvanceCalendar(int daysPassed)
        {
            startDay += daysPassed;
            UpdateCalendarUI();
        }

        // Get event info for a specific day
        public GameEvent GetEventForDay(int day)
        {
            return eventsByDay.ContainsKey(day) ? eventsByDay[day] : null;
        }

        // For debug - print events to console
        [System.Obsolete("Used for debugging")]
        public void PrintEventDays()
        {
            foreach (int day in randomEventDays)
            {
                if (day >= startDay && day < startDay + 50 && eventsByDay.ContainsKey(day))
                {
                    GameEvent evt = eventsByDay[day];
                }
            }
        }
    }
}