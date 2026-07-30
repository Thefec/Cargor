using System;
using Unity.Netcode;
using UnityEngine;
using TMPro;
using Steamworks.Data;

namespace NewCss
{
    /// <summary>
    /// Oyuncu sayısına göre dinamik zorluk yönetimi.
    /// Server-authoritative tasarım ile network senkronizasyonu sağlar.
    /// </summary>
    public class DifficultyManager : NetworkBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[DifficultyManager]";
        private const int MIN_PLAYERS = 1;
        private const int MAX_PLAYERS = 4;

        #endregion

        #region Singleton

        public static DifficultyManager Instance { get; private set; }

        #endregion

        #region Serialized Fields - Base Values

        [Header("=== BASE VALUES (1 Player) ===")]
        [SerializeField, Tooltip("Daily customer count for single player")]
        private int baseCustomerCount = 10;

        [SerializeField, Tooltip("Starting money for single player")]
        private int baseStartingMoney = 500;

        [SerializeField, Tooltip("Phone call chance for single player")]
        [Range(0f, 1f)]
        private float basePhoneCallChance = 0.3f;

        [SerializeField, Tooltip("Customer minimum patience time for single player (seconds)")]
        private float baseMinPatience = 35f;
 
        [SerializeField, Tooltip("Customer maximum patience time for single player (seconds)")]
        private float baseMaxPatience = 55f;

        [SerializeField, Tooltip("Stamina regeneration rate for single player")]
        private float baseStaminaRegenRate = 1f;

        #endregion

        #region Serialized Fields - Scaling Factors

        [Header("=== SCALING FACTORS (Per Additional Player) ===")]
        [SerializeField, Tooltip("Additional customers per player")]
        private int customerCountPerPlayer = 5;

        [SerializeField, Tooltip("Money multiplier per player (1 = no change)")]
        [Range(0.5f, 1.5f)]
        private float moneyMultiplierPerPlayer = 1.2f;

        [SerializeField, Tooltip("Additional phone chance per player")]
        [Range(0f, 0.3f)]
        private float phoneChancePerPlayer = 0.1f;

        [SerializeField, Tooltip("Patience reduction per player (seconds)")]
        private float patienceReductionPerPlayer = 5f;

        [SerializeField, Tooltip("Stamina drain multiplier per player")]
        [Range(1f, 2f)]
        private float staminaDrainMultiplierPerPlayer = 1.1f;

        [SerializeField, Tooltip("Upgrade cost multiplier per player")]
        [Range(1f, 2f)]
        private float upgradeCostMultiplierPerPlayer = 1.15f;

        #endregion

        #region Serialized Fields - UI

        [Header("=== UI REFERENCES ===")]
        [SerializeField, Tooltip("Difficulty indicator text (in Lobby)")]
        private TextMeshProUGUI difficultyIndicatorText;

        #endregion

        #region Network Variables

        private readonly NetworkVariable<int> _networkPlayerCount = new(1,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        #endregion

        #region Private Fields

        private int _cachedPlayerCount = 1;

        #endregion

        #region Events

        /// <summary>
        /// Zorluk seviyesi değiştiğinde tetiklenir
        /// </summary>
        public static event Action<int> OnDifficultyChanged;

        #endregion

        #region Public Properties

        /// <summary>
        /// Mevcut oyuncu sayısı
        /// </summary>
        public int PlayerCount => _networkPlayerCount.Value;

        /// <summary>
        /// Zorluk seviyesi adı
        /// </summary>
        public string DifficultyName => GetDifficultyName(_networkPlayerCount.Value);

        /// <summary>
        /// Ölçeklenmiş günlük müşteri sayısı
        /// </summary>
        public int ScaledCustomerCount => CalculateScaledCustomerCount();

        /// <summary>
        /// Ölçeklenmiş başlangıç parası
        /// </summary>
        public int ScaledStartingMoney => CalculateScaledStartingMoney();

        /// <summary>
        /// Ölçeklenmiş telefon çalma olasılığı
        /// </summary>
        public float ScaledPhoneCallChance => CalculateScaledPhoneCallChance();

        /// <summary>
        /// Ölçeklenmiş müşteri minimum bekleme süresi
        /// </summary>
        public float ScaledMinPatience => CalculateScaledMinPatience();

        /// <summary>
        /// Ölçeklenmiş müşteri maximum bekleme süresi
        /// </summary>
        public float ScaledMaxPatience => CalculateScaledMaxPatience();

        /// <summary>
        /// Ölçeklenmiş stamina yenilenme hızı
        /// </summary>
        public float ScaledStaminaRegenRate => CalculateScaledStaminaRegenRate();

        /// <summary>
        /// Upgrade maliyet çarpanı
        /// </summary>
        public float UpgradeCostMultiplier => CalculateUpgradeCostMultiplier();

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeSingleton();
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            CleanupSingleton();
        }

        #endregion

        #region Network Lifecycle

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            SubscribeToNetworkEvents();

            if (IsServer)
            {
                InitializePlayerCount();
            }

            UpdateDifficultyUI();
        }

        public override void OnNetworkDespawn()
        {
            UnsubscribeFromNetworkEvents();
            base.OnNetworkDespawn();
        }

        #endregion

        #region Singleton Management

        private void InitializeSingleton()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
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

        #endregion

        #region Event Subscriptions

        private void SubscribeToNetworkEvents()
        {
            _networkPlayerCount.OnValueChanged += HandlePlayerCountChanged;
        }

        private void UnsubscribeFromNetworkEvents()
        {
            _networkPlayerCount.OnValueChanged -= HandlePlayerCountChanged;
        }

        #endregion

        #region Network Event Handlers

        private void HandlePlayerCountChanged(int previousValue, int newValue)
        {
            _cachedPlayerCount = newValue;
            LogDebug($"Player count changed: {previousValue} -> {newValue}");

            UpdateDifficultyUI();
            ApplyDifficultySettings();
            OnDifficultyChanged?.Invoke(newValue);
        }

        #endregion

        #region Initialization

        private void InitializePlayerCount()
        {
            int playerCount = GetLobbyPlayerCount();
            _networkPlayerCount.Value = Mathf.Clamp(playerCount, MIN_PLAYERS, MAX_PLAYERS);
            _cachedPlayerCount = _networkPlayerCount.Value;

            LogDebug($"Initialized with {_networkPlayerCount.Value} players");
            ApplyDifficultySettings();
        }

        private int GetLobbyPlayerCount()
        {
            // Try to get from Steam lobby
            if (LobbySaver.instance != null && LobbySaver.instance.CurrentLobby.HasValue)
            {
                try
                {
                    Lobby lobby = LobbySaver.instance.CurrentLobby.Value;
                    int memberCount = 0;
                    foreach (var member in lobby.Members)
                    {
                        memberCount++;
                    }
                    if (memberCount > 0)
                    {
                        return memberCount;
                    }
                }
                catch (Exception ex)
                {
                    LogWarning($"Error getting lobby member count: {ex.Message}");
                }
            }

            // Fallback to NetworkManager connected clients
            if (NetworkManager.Singleton != null)
            {
                return NetworkManager.Singleton.ConnectedClientsList.Count;
            }

            return MIN_PLAYERS;
        }

        #endregion

        #region Difficulty Calculation

        private int CalculateScaledCustomerCount()
        {
            int additionalPlayers = _cachedPlayerCount - 1;
            return baseCustomerCount + (additionalPlayers * customerCountPerPlayer);
        }

        private int CalculateScaledStartingMoney()
        {
            // Each additional player reduces per-player money to maintain balance
            float multiplier = 1f;
            for (int i = 1; i < _cachedPlayerCount; i++)
            {
                multiplier *= moneyMultiplierPerPlayer;
            }
            return Mathf.RoundToInt(baseStartingMoney * multiplier);
        }

        private float CalculateScaledPhoneCallChance()
        {
            int additionalPlayers = _cachedPlayerCount - 1;
            float scaledChance = basePhoneCallChance + (additionalPlayers * phoneChancePerPlayer);
            return Mathf.Clamp01(scaledChance);
        }

        private float CalculateScaledMinPatience()
        {
            int additionalPlayers = _cachedPlayerCount - 1;
            float scaledPatience = baseMinPatience - (additionalPlayers * patienceReductionPerPlayer);
            return Mathf.Max(5f, scaledPatience); // Minimum 5 seconds
        }

        private float CalculateScaledMaxPatience()
        {
            int additionalPlayers = _cachedPlayerCount - 1;
            float scaledPatience = baseMaxPatience - (additionalPlayers * patienceReductionPerPlayer);
            return Mathf.Max(10f, scaledPatience); // Minimum 10 seconds
        }

        private float CalculateScaledStaminaRegenRate()
        {
            // Higher drain means lower regen rate
            float drainMultiplier = 1f;
            for (int i = 1; i < _cachedPlayerCount; i++)
            {
                drainMultiplier *= staminaDrainMultiplierPerPlayer;
            }
            return baseStaminaRegenRate / drainMultiplier;
        }

        private float CalculateUpgradeCostMultiplier()
        {
            float multiplier = 1f;
            for (int i = 1; i < _cachedPlayerCount; i++)
            {
                multiplier *= upgradeCostMultiplierPerPlayer;
            }
            return multiplier;
        }

        private string GetDifficultyName(int playerCount)
        {
            return playerCount switch
            {
                1 => "Easy",
                2 => "Normal",
                3 => "Hard",
                4 => "Expert",
                _ => "Unknown"
            };
        }

        #endregion

        #region Apply Settings

        private void ApplyDifficultySettings()
        {
            if (!IsServer) return;

            // Start coroutine to apply settings with delay
            StartCoroutine(ApplyDifficultySettingsDelayed());
        }

        private System.Collections.IEnumerator ApplyDifficultySettingsDelayed()
        {
            const float maxWaitTime = 5f;
            const float checkInterval = 0.1f;
            float elapsedTime = 0f;

            // Wait for all systems to be ready
            while (elapsedTime < maxWaitTime)
            {
                bool allReady = true;

                // Check MoneySystem
                if (MoneySystem.Instance == null)
                {
                    allReady = false;
                }

                // Check PhoneCallManager
                if (PhoneCallManager.Instance == null)
                {
                    allReady = false;
                }

                if (allReady)
                {
                    break;
                }

                yield return new WaitForSeconds(checkInterval);
                elapsedTime += checkInterval;
            }

            // Apply settings even if not all systems are ready (timeout)
            ApplyCustomerSettings();
            ApplyMoneySettings();
            ApplyPhoneSettings();
            ApplyStaminaSettings();

            LogDebug($"Applied difficulty settings for {_cachedPlayerCount} players (waited {elapsedTime:F2}s)");
        }

        private void ApplyCustomerSettings()
        {
            // Kapasite bazlı sisteme oyuncu sayısı çarpanını set et
            var customerManager = FindObjectOfType<CustomerManager>();
            if (customerManager != null)
            {
                // Oyuncu sayısına göre çarpan: 1P=1.0, 2P=1.3, 3P=1.6, 4P=2.0
                float playerMultiplier = 1f + ((_cachedPlayerCount - 1) * 0.3f);
                customerManager.playerCountMultiplier = playerMultiplier;
                LogDebug($"Player count multiplier set to: {playerMultiplier:F2} ({_cachedPlayerCount} players)");
            }

            // Find CustomerAI prefabs and update patience
            var customerAIs = FindObjectsOfType<CustomerAI>();
            float minPatience = ScaledMinPatience;
            float maxPatience = ScaledMaxPatience;
            foreach (var ai in customerAIs)
            {
                ai.minWaitTime = minPatience;
                ai.maxWaitTime = maxPatience;
            }
            
            LogDebug($"Customer patience set to: {minPatience}s - {maxPatience}s");
        }

        private void ApplyMoneySettings()
        {
            var moneySystem = MoneySystem.Instance;
            if (moneySystem != null)
            {
                moneySystem.startingMoney = ScaledStartingMoney;

                // Guard: only force-set the player's current money when the game
                // hasn't actually started yet (lobby / initial setup / player-count
                // changes before gameplay begins). Once GameStateManager reports the
                // game has started, calling SetMoney() here would wipe out the
                // player's earned money mid-run, so we only update the
                // startingMoney field above and leave current money untouched.
                bool gameAlreadyStarted = GameStateManager.Instance != null && GameStateManager.Instance.HasGameEverStarted;
                if (!gameAlreadyStarted)
                {
                    moneySystem.SetMoney(ScaledStartingMoney);
                    LogDebug($"Starting money set to: {ScaledStartingMoney}");
                }
                else
                {
                    LogDebug($"Game already started - skipped SetMoney, updated startingMoney field to: {ScaledStartingMoney}");
                }
            }
        }

        private void ApplyPhoneSettings()
        {
            var phoneManager = PhoneCallManager.Instance;
            if (phoneManager != null)
            {
                phoneManager.SetCallChance(ScaledPhoneCallChance);
                LogDebug($"Phone call chance set to: {ScaledPhoneCallChance:P0}");
            }
        }

        private void ApplyStaminaSettings()
        {
            var players = FindObjectsOfType<PlayerMovement>();
            float staminaRegen = ScaledStaminaRegenRate;
            foreach (var player in players)
            {
                player.staminaRegenRate = staminaRegen;
            }
            LogDebug($"Stamina regen rate set to: {staminaRegen:F2}");
        }

        #endregion

        #region UI Update

        private void UpdateDifficultyUI()
        {
            if (difficultyIndicatorText == null) return;

            string difficultyName = GetDifficultyName(_cachedPlayerCount);
            string localizedText = LocalizationHelper.GetLocalizedString("DifficultyIndicator");

            try
            {
                // Format: "Current Difficulty: {0} Players ({1})"
                difficultyIndicatorText.text = string.Format(localizedText, _cachedPlayerCount, difficultyName);
            }
            catch
            {
                // Fallback
                difficultyIndicatorText.text = $"Current Difficulty: {_cachedPlayerCount} Players ({difficultyName})";
            }
        }

        /// <summary>
        /// Zorluk göstergesini günceller (UI için)
        /// </summary>
        public void SetDifficultyIndicatorText(TextMeshProUGUI textComponent)
        {
            difficultyIndicatorText = textComponent;
            UpdateDifficultyUI();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Oyuncu sayısını manuel olarak ayarlar (test için)
        /// </summary>
        public void SetPlayerCount(int count)
        {
            if (!IsServer) return;

            _networkPlayerCount.Value = Mathf.Clamp(count, MIN_PLAYERS, MAX_PLAYERS);
        }

        /// <summary>
        /// Oyuncu sayısını lobiden yeniden yükler
        /// </summary>
        public void RefreshPlayerCount()
        {
            if (!IsServer) return;

            InitializePlayerCount();
        }

        /// <summary>
        /// Zorluk bilgisini string olarak döndürür
        /// </summary>
        public string GetDifficultyInfo()
        {
            return $"Players: {_cachedPlayerCount}\n" +
                   $"Difficulty: {DifficultyName}\n" +
                   $"Customers/Day: {ScaledCustomerCount}\n" +
                   $"Starting Money: {ScaledStartingMoney}\n" +
                   $"Phone Chance: {ScaledPhoneCallChance:P0}\n" +
                   $"Patience: {ScaledMinPatience:F1}s - {ScaledMaxPatience:F1}s\n" +
                   $"Upgrade Cost: x{UpgradeCostMultiplier:F2}";
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
        [ContextMenu("Print Difficulty Info")]
        private void DebugPrintInfo()
        {
            Debug.Log(GetDifficultyInfo());
        }

        [ContextMenu("Set 1 Player")]
        private void DebugSet1Player() => SetPlayerCount(1);

        [ContextMenu("Set 2 Players")]
        private void DebugSet2Players() => SetPlayerCount(2);

        [ContextMenu("Set 3 Players")]
        private void DebugSet3Players() => SetPlayerCount(3);

        [ContextMenu("Set 4 Players")]
        private void DebugSet4Players() => SetPlayerCount(4);

        [ContextMenu("Refresh From Lobby")]
        private void DebugRefresh() => RefreshPlayerCount();
#endif

        #endregion
    }
}
