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
        private int initialWeeklyCost = 1000;
        private int minWeeklyIncrease = 200;
        private int maxWeeklyIncrease = 600;
        private int weeklyCost;

        [Header("Time Settings")]
        public float realDurationInSeconds = 160f;

        [Header("Dynamic Duration Settings")]
        [Tooltip("Duration increase per day after day 3")]
        public float dailyDurationIncrease = 10f;

        [Header("UI Elements")]
        public TextMeshProUGUI dayTimeText;
        public GameObject dayEndScreen;

        // Network Variables
        private NetworkVariable<float> networkElapsedTime = new NetworkVariable<float>(0f);
        private NetworkVariable<int> networkCurrentDay = new NetworkVariable<int>(1);
        private NetworkVariable<bool> networkIsDayOver = new NetworkVariable<bool>(false);
        private NetworkVariable<bool> networkIsBreakRoomReady = new NetworkVariable<bool>(false);
        private NetworkVariable<int> networkWeeklyCost = new NetworkVariable<int>(1000);

        // Public properties
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
        private bool moneyCheckDone = false;

        // ============================================
        // PERİYODİK KONTROL FLAG'LERİ - YENİ EKLENEN
        // ============================================
        private bool dayStartChecked = false;
        private bool dayMiddleChecked = false;
        private bool dayEndChecked = false;

        public static event Action OnWeeklyEvent;
        public int CurrentHour => currentHour;

        [Tooltip("Start and end hours calculated only for UI")]
        public int startHour = 7, endHour = 15;

        public float CurrentDayDuration
        {
            get
            {
                if (networkCurrentDay.Value <= 3)
                {
                    return realDurationInSeconds;
                }
                else
                {
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
                Instance = null;
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

            // Periyodik kontrol flag'lerini resetle
            dayStartChecked = false;
            dayMiddleChecked = false;
            dayEndChecked = false;

            UpdateDayTimeUI();
            if (dayEndScreen != null)
                dayEndScreen.SetActive(false);

            Debug.Log("Day cycle reset completed");
        }

        public override void OnNetworkSpawn()
        {
            Debug.Log($"DayCycleManager OnNetworkSpawn - IsServer: {IsServer}, IsHost: {NetworkManager.Singleton.IsHost}");

            base.OnNetworkSpawn();

            if (IsServer)
            {
                networkWeeklyCost.Value = initialWeeklyCost;
                weeklyCost = initialWeeklyCost;
                Debug.Log("Server: Weekly cost initialized");
            }

            networkElapsedTime.OnValueChanged += OnElapsedTimeChanged;
            networkCurrentDay.OnValueChanged += OnCurrentDayChanged;
            networkIsDayOver.OnValueChanged += OnDayOverChanged;
            networkIsBreakRoomReady.OnValueChanged += OnBreakRoomReadyChanged;

            ResetDayCycle();

            Debug.Log("DayCycleManager network spawn completed");
        }

        public override void OnNetworkDespawn()
        {
            networkElapsedTime.OnValueChanged -= OnElapsedTimeChanged;
            networkCurrentDay.OnValueChanged -= OnCurrentDayChanged;
            networkIsDayOver.OnValueChanged -= OnDayOverChanged;
            networkIsBreakRoomReady.OnValueChanged -= OnBreakRoomReadyChanged;

            base.OnNetworkDespawn();
        }

        void Start()
        {
            Debug.Log($"DayCycleManager Start - IsServer: {IsServer}");

            if (IsServer)
            {
                ResetDayCycle();
                Debug.Log("Server initialized day cycle variables");
            }

            UpdateDayTimeUI();
            if (dayEndScreen != null)
                dayEndScreen.SetActive(false);
        }

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
            if (scene.name != "MainMenu" && scene.name.Contains("Game"))
            {
                Debug.Log($"Game scene {scene.name} loaded, resetting day cycle");
                if (IsServer)
                {
                    ResetDayCycle();
                }
            }
        }

        // ============================================
        // PERİYODİK OYUNCU KONTROLÜ FONKSİYONLARI - YENİ
        // ============================================

        /// <summary>
        /// Gün başında oyuncu sayısını kontrol et
        /// </summary>
        private void CheckPlayersOnDayStart()
        {
            Debug.Log("[DayCycle] ⏰ Gün başladı - Oyuncu sayısı kontrol ediliyor");

            if (BreakRoomManager.Instance != null)
            {
                BreakRoomManager.Instance.CheckAndUpdateLobbyPlayers();
            }
            else
            {
                Debug.LogWarning("[DayCycle] BreakRoomManager.Instance bulunamadı!");
            }
        }

        /// <summary>
        /// Gün ortasında oyuncu sayısını kontrol et
        /// </summary>
        private void CheckPlayersOnDayMiddle()
        {
            Debug.Log("[DayCycle] ⏰ Gün ortası - Oyuncu sayısı kontrol ediliyor");

            if (BreakRoomManager.Instance != null)
            {
                BreakRoomManager.Instance.CheckAndUpdateLobbyPlayers();
            }
            else
            {
                Debug.LogWarning("[DayCycle] BreakRoomManager.Instance bulunamadı!");
            }
        }

        /// <summary>
        /// Gün sonunda oyuncu sayısını kontrol et
        /// </summary>
        private void CheckPlayersOnDayEnd()
        {
            Debug.Log("[DayCycle] ⏰ Gün bitti - Oyuncu sayısı kontrol ediliyor");

            if (BreakRoomManager.Instance != null)
            {
                BreakRoomManager.Instance.CheckAndUpdateLobbyPlayers();
            }
            else
            {
                Debug.LogWarning("[DayCycle] BreakRoomManager.Instance bulunamadı!");
            }
        }

        void Update()
        {
            UpdateDayTimeUI();

            if (!IsSpawned) return;
            if (!IsServer) return;
            if (networkIsDayOver.Value) return;

            networkElapsedTime.Value += Time.deltaTime;

            // ========================================
            // PERİYODİK OYUNCU SAYISI KONTROLLERI - YENİ
            // ========================================

            // Gün başlangıcı kontrolü (ilk 0.5 saniye)
            if (networkElapsedTime.Value <= 0.5f && !dayStartChecked)
            {
                CheckPlayersOnDayStart();
                dayStartChecked = true;
            }

            // Gün ortası kontrolü (günün yarısında)
            float halfDuration = CurrentDayDuration / 2f;
            if (networkElapsedTime.Value >= halfDuration &&
                networkElapsedTime.Value < (halfDuration + 0.5f) &&
                !dayMiddleChecked)
            {
                CheckPlayersOnDayMiddle();
                dayMiddleChecked = true;
            }

            // Gün sonu kontrolü (gün bittiğinde)
            if (networkElapsedTime.Value >= CurrentDayDuration && !dayEndChecked)
            {
                CheckPlayersOnDayEnd();
                dayEndChecked = true;
            }

            // ========================================
            // MEVCUT GÜN SONU LOJİĞİ
            // ========================================

            if (networkElapsedTime.Value < CurrentDayDuration)
            {
                moneyCheckDone = false;
                return;
            }

            if (!moneyCheckDone && GameStateManager.Instance != null)
            {
                if (!GameStateManager.Instance.IsDayOver)
                {
                    GameStateManager.Instance.CheckMoneyAtDayEnd();
                    moneyCheckDone = true;

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
            float currentElapsedTime = networkElapsedTime.Value;
            float progress = Mathf.Clamp01(currentElapsedTime / CurrentDayDuration);
            float totalMinutes = (endHour - startHour) * 60f;
            float currentMinutes = progress * totalMinutes;

            currentHour = startHour + Mathf.FloorToInt(currentMinutes / 60f);
            int minute = Mathf.FloorToInt(currentMinutes % 60f);

            currentHour = Mathf.Clamp(currentHour, startHour, endHour);

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

            // ========================================
            // PERİYODİK KONTROL FLAG'LERİNİ RESETLE - YENİ
            // ========================================
            dayStartChecked = false;
            dayMiddleChecked = false;
            dayEndChecked = false;

            Debug.Log($"Day {networkCurrentDay.Value} started - Duration: {CurrentDayDuration} seconds");

            if (networkCurrentDay.Value % 6 == 0)
            {
                int delta = UnityEngine.Random.Range(minWeeklyIncrease, maxWeeklyIncrease + 1);
                weeklyCost = Mathf.RoundToInt(weeklyCost * 1.2f);
                weeklyCost += delta;
                networkWeeklyCost.Value = weeklyCost;

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

                TriggerWeeklyEventClientRpc();

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

            OnNewDay?.Invoke();
            TriggerNewDayEventClientRpc();

            UpdateDayTimeUI();
            HideDayEndScreenClientRpc();

            // ========================================
            // YENİ GÜN BAŞLARKEN OYUNCU SAYISINI GÜNCELLE - YENİ
            // ========================================
            var breakRoomManager = FindObjectOfType<NewCss.BreakRoomManager>();
            if (breakRoomManager != null)
            {
                breakRoomManager.requiredPlayers = breakRoomManager.GetSteamLobbyPlayerCount();
                Debug.Log($"BreakRoomManager.requiredPlayers güncellendi: {breakRoomManager.requiredPlayers}");
            }
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
        }

        public void CallNextDay()
        {
            if (IsServer)
                NextDay();
            else
                NextDayServerRpc();
        }

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