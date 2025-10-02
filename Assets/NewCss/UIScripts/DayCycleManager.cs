using UnityEngine;
using TMPro;
using System;
using Unity.Netcode;

namespace NewCss
{
    public class DayCycleManager : NetworkBehaviour
    {
        public static DayCycleManager Instance { get; private set; }
        public static event Action OnNewDay;

        [Header("Weekly Cost Settings")]
        private int initialWeeklyCost = 1000;    // Week 1 cost
        private int minWeeklyIncrease = 200;     // Minimum increase
        private int maxWeeklyIncrease = 600;     // Maximum increase
        private int weeklyCost;                   // Current weekly cost

        [Header("Time Settings")]
        public float realDurationInSeconds = 160f; // Base duration for first 3 days
        [Header("Dynamic Duration Settings")]
        [Tooltip("Duration increase per day after day 3")]
        public float dailyDurationIncrease = 10f; // 10 seconds per day after day 3

        [Header("UI Elements")]
        public TextMeshProUGUI dayTimeText;
        public GameObject dayEndScreen;

        // Network Variables for time synchronization
        private NetworkVariable<float> networkElapsedTime = new NetworkVariable<float>(0f);
        private NetworkVariable<int> networkCurrentDay = new NetworkVariable<int>(1);
        private NetworkVariable<bool> networkIsDayOver = new NetworkVariable<bool>(false);
        private NetworkVariable<bool> networkIsBreakRoomReady = new NetworkVariable<bool>(false);
        private NetworkVariable<int> networkWeeklyCost = new NetworkVariable<int>(1000);

        // Public properties using network variables
        public bool IsDayOver => networkIsDayOver.Value;
        public bool isBreakRoomReady
        {
            get => networkIsBreakRoomReady.Value;
            set
            {
                if (IsServer)
                    networkIsBreakRoomReady.Value = value;
                else
                    SetBreakRoomReadyServerRpc(value);
            }
        }
        public float elapsedTime => networkElapsedTime.Value;
        public int currentDay => networkCurrentDay.Value;
        public int WeeklyCost => networkWeeklyCost.Value;
        public int GetCurrentWeeklyCost()
        {
            return networkWeeklyCost.Value;
        }

        private int currentHour;
        private bool lunchNotified;

        private bool moneyCheckDone = false; // Yeni field ekle
        public static event Action OnWeeklyEvent;
        public int CurrentHour => currentHour;

        [Tooltip("Start and end hours calculated only for UI")]
        public int startHour = 7, endHour = 15;

        // Dynamic property for current day duration
        public float CurrentDayDuration
        {
            get
            {
                if (networkCurrentDay.Value <= 3)
                {
                    // İlk 3 gün sabit süre
                    return realDurationInSeconds;
                }
                else
                {
                    // 3. günden sonra her gün 10 saniye eklenir
                    int extraDays = networkCurrentDay.Value - 3;
                    return realDurationInSeconds + (extraDays * dailyDurationIncrease);
                }
            }
        }

        public bool IsTimeUp => networkElapsedTime.Value >= CurrentDayDuration;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                // DontDestroyOnLoad(gameObject); // BU SATIRI KALDIR!
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null; // Instance'ı temizle
            }
        }
        public float CurrentTime
        {
            get
            {
                float progress = Mathf.Clamp01(networkElapsedTime.Value / CurrentDayDuration);
                float totalHours = endHour - startHour;
                return startHour + (progress * totalHours);
            }
        }

        // RESET FUNCTIONALITY
        public void ResetDayCycle()
        {
            Debug.Log("=== RESETTING DAY CYCLE ===");

            if (IsServer)
            {
                networkElapsedTime.Value = 0f;
                networkCurrentDay.Value = 1;
                networkIsDayOver.Value = false;
                networkIsBreakRoomReady.Value = false;
                networkWeeklyCost.Value = initialWeeklyCost;
            }

            weeklyCost = initialWeeklyCost;
            currentHour = startHour;
            lunchNotified = false;
            moneyCheckDone = false;

            UpdateDayTimeUI();
            if (dayEndScreen != null)
                dayEndScreen.SetActive(false);

            Debug.Log("Day cycle reset completed");
        }

        public override void OnNetworkSpawn()
        {
            Debug.Log($"DayCycleManager OnNetworkSpawn - IsServer: {IsServer}, IsHost: {NetworkManager.Singleton.IsHost}");

            base.OnNetworkSpawn();

            // Initialize weekly cost
            if (IsServer)
            {
                networkWeeklyCost.Value = initialWeeklyCost;
                weeklyCost = initialWeeklyCost;
                Debug.Log("Server: Weekly cost initialized");
            }

            // Subscribe to network variable changes
            networkElapsedTime.OnValueChanged += OnElapsedTimeChanged;
            networkCurrentDay.OnValueChanged += OnCurrentDayChanged;
            networkIsDayOver.OnValueChanged += OnDayOverChanged;
            networkIsBreakRoomReady.OnValueChanged += OnBreakRoomReadyChanged;

            // Reset day cycle when network spawns
            ResetDayCycle();

            Debug.Log("DayCycleManager network spawn completed");
        }

        public override void OnNetworkDespawn()
        {
            // Unsubscribe from network variable changes
            networkElapsedTime.OnValueChanged -= OnElapsedTimeChanged;
            networkCurrentDay.OnValueChanged -= OnCurrentDayChanged;
            networkIsDayOver.OnValueChanged -= OnDayOverChanged;
            networkIsBreakRoomReady.OnValueChanged -= OnBreakRoomReadyChanged;

            base.OnNetworkDespawn();
        }

        void Start()
        {
            Debug.Log($"DayCycleManager Start - IsServer: {IsServer}");

            // Her Start'ta day cycle'ı resetle
            if (IsServer)
            {
                ResetDayCycle();
                Debug.Log("Server initialized day cycle variables");
            }

            UpdateDayTimeUI();
            if (dayEndScreen != null)
                dayEndScreen.SetActive(false);
        }

        // SCENE RELOAD DETECTION
        void OnEnable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDisable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            // Eğer oyun scene'i yeniden yüklendiyse day cycle'ı resetle
            if (scene.name != "MainMenu" && scene.name.Contains("Game")) // Oyun scene'inizi buraya yazın
            {
                Debug.Log($"Game scene {scene.name} loaded, resetting day cycle");
                if (IsServer)
                {
                    ResetDayCycle();
                }
            }
        }

        void Update()
        {
            UpdateDayTimeUI();

            if (!IsSpawned) return;
            if (!IsServer) return;
            if (networkIsDayOver.Value) return;

            networkElapsedTime.Value += Time.deltaTime;

            // Dinamik süre kontrolü - CurrentDayDuration kullan
            if (networkElapsedTime.Value < CurrentDayDuration)
            {
                moneyCheckDone = false; // Gün devam ederken reset
                return;
            }

            // GÜN SONU - Önce para kontrolü yap (sadece bir kez)
            if (!moneyCheckDone && GameStateManager.Instance != null)
            {
                // SADECE OYUN DEVAM EDİYORSA para kontrolü yap
                if (!GameStateManager.Instance.IsDayOver)
                {
                    GameStateManager.Instance.CheckMoneyAtDayEnd();
                    moneyCheckDone = true;

                    // Eğer oyun bittiyse (kaybedildiyse), devam etme
                    if (GameStateManager.Instance.IsDayOver)
                    {
                        Debug.Log("Game ended after money check - stopping day cycle");
                        return;
                    }
                }
                else
                {
                    Debug.Log("Game already ended - skipping money check");
                    moneyCheckDone = true;
                    return;
                }
            }

            if (!networkIsBreakRoomReady.Value)
            {
                UpdateWarningTextClientRpc();
                return;
            }

            networkIsDayOver.Value = true;
            ShowDayEndScreenClientRpc();
        }

        void UpdateDayTimeUI()
        {
            // Network değişkeninin değerini al
            float currentElapsedTime = networkElapsedTime.Value;

            // Progress hesapla (0-1 arası) - CurrentDayDuration kullan
            float progress = Mathf.Clamp01(currentElapsedTime / CurrentDayDuration);

            // Toplam dakika sayısını hesapla
            float totalMinutes = (endHour - startHour) * 60f;
            float currentMinutes = progress * totalMinutes;

            // Saat ve dakikayı hesapla
            currentHour = startHour + Mathf.FloorToInt(currentMinutes / 60f);
            int minute = Mathf.FloorToInt(currentMinutes % 60f);

            // Saat sınırlarını kontrol et
            currentHour = Mathf.Clamp(currentHour, startHour, endHour);

            // UI'yı güncelle - gün süresini de göster (debug için)
            if (dayTimeText != null)
            {
                dayTimeText.text = $"Day {networkCurrentDay.Value}  {currentHour:D2}:{minute:D2} ({CurrentDayDuration}s)";
            }
        }

        [ServerRpc(RequireOwnership = false)]
        void SetBreakRoomReadyServerRpc(bool ready)
        {
            networkIsBreakRoomReady.Value = ready;
        }

        [ClientRpc]
        void UpdateWarningTextClientRpc()
        {
            if (dayTimeText != null)
                dayTimeText.text = "Day Over. Go to Break Room.";
        }

        [ClientRpc]
        void ShowDayEndScreenClientRpc()
        {
            // GameStateManager kontrolünü kaldır
            if (dayEndScreen != null)
                dayEndScreen.SetActive(true);
        }

        [ServerRpc(RequireOwnership = false)]
        public void NextDayServerRpc()
        {
            NextDay();
        }

        public void NextDay()
        {
            if (!IsServer) return;

            networkElapsedTime.Value = 0f;
            networkCurrentDay.Value++;

            // Debug log - yeni gün süresi
            Debug.Log($"Day {networkCurrentDay.Value} started - Duration: {CurrentDayDuration} seconds");

            // Kira günü mü kontrol et (6, 12, 18, 24, 30)
            if (networkCurrentDay.Value % 6 == 0)
            {
                // Weekly cost'u hesapla ve güncelle
                int delta = UnityEngine.Random.Range(minWeeklyIncrease, maxWeeklyIncrease + 1);
                weeklyCost = Mathf.RoundToInt(weeklyCost * 1.2f);
                weeklyCost += delta;
                networkWeeklyCost.Value = weeklyCost;

                // Para çekme işlemi
                if (MoneySystem.Instance != null)
                {
                    MoneySystem.Instance.SpendMoney(weeklyCost);
                    Debug.Log($"RENT DAY {networkCurrentDay.Value}: Rent paid - {weeklyCost} coins");
                    Debug.Log($"Remaining money: {MoneySystem.Instance.CurrentMoney}");
                }

                Debug.Log($"▶ Weekly Rent Event: Day {networkCurrentDay.Value}\n" +
                          $"- Cost increase: +{delta}\n" +
                          $"- New weekly cost: {weeklyCost}\n" +
                          $"- This is rent payment #{networkCurrentDay.Value / 6} of 5");

                // Notify clients about weekly event
                TriggerWeeklyEventClientRpc();

                // *** BU SATIRI EKLEYİN ***
                // Kira ödendikten sonra GameStateManager'ı bilgilendir
                Debug.Log("Calling GameStateManager.CheckGameState after rent payment");
                if (GameStateManager.Instance != null)
                {
                    Debug.Log("GameStateManager.Instance found, calling CheckGameState");
                    GameStateManager.Instance.CheckGameState();
                }
                else
                {
                    Debug.LogError("GameStateManager.Instance is NULL! GameStateManager not found in scene!");
                }
            }

            Debug.Log($"Day {networkCurrentDay.Value} started (server)");

            networkIsDayOver.Value = false;
            networkIsBreakRoomReady.Value = false;

            // Event'i tetikle
            OnNewDay?.Invoke();

            // Client'lara haber ver
            TriggerNewDayEventClientRpc();

            UpdateDayTimeUI();
            HideDayEndScreenClientRpc();
        }

        [ClientRpc]
        void TriggerWeeklyEventClientRpc()
        {
            OnWeeklyEvent?.Invoke();
        }

        [ClientRpc]
        void TriggerNewDayEventClientRpc()
        {
            OnNewDay?.Invoke();
        }

        [ClientRpc]
        void HideDayEndScreenClientRpc()
        {
            if (dayEndScreen != null)
                dayEndScreen.SetActive(false);
        }

        // Network variable change handlers
        private void OnElapsedTimeChanged(float previousValue, float newValue)
        {
            UpdateDayTimeUI();
        }

        private void OnCurrentDayChanged(int previousValue, int newValue)
        {
            UpdateDayTimeUI();
        }

        private void OnDayOverChanged(bool previousValue, bool newValue)
        {
            if (dayEndScreen != null)
                dayEndScreen.SetActive(newValue);
        }

        private void OnBreakRoomReadyChanged(bool previousValue, bool newValue)
        {
            // İsteğe bağlı: Break room ready durumu değiştiğinde yapılacak işlemler
        }

        // Public method for external scripts to call NextDay
        public void CallNextDay()
        {
            if (IsServer)
                NextDay();
            else
                NextDayServerRpc();
        }

        // Helper methods for debugging
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
                networkElapsedTime.Value = CurrentDayDuration;
                networkIsBreakRoomReady.Value = true;
            }
        }

        [ContextMenu("Force Start Time")]
        public void ForceStartTime()
        {
            if (IsServer)
            {
                networkElapsedTime.Value = 0f;
                networkIsDayOver.Value = false;
                networkIsBreakRoomReady.Value = false;
            }
        }

        [ContextMenu("Reset Day Cycle")]
        public void ForceResetDayCycle()
        {
            ResetDayCycle();
        }
    }
}