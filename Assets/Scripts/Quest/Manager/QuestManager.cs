using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace NewCss.Quest
{
    /// <summary>
    /// Ana görev yöneticisi - görev ataması, ilerleme takibi ve ödül/ceza dağıtımını yönetir
    /// Server-authoritative tasarım ile network senkronizasyonu sağlar
    /// </summary>
    public class QuestManager : NetworkBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[QuestManager]";
        private const int DAILY_QUEST_COUNT = 3;

        #endregion

        #region Singleton

        public static QuestManager Instance { get; private set; }

        #endregion

        #region Events

        /// <summary>
        /// Yeni görevler atandığında tetiklenir
        /// </summary>
        public static event Action OnQuestsAssigned;

        /// <summary>
        /// Görev durumu değiştiğinde tetiklenir
        /// </summary>
        public static event Action<string, QuestStatus> OnQuestStatusChanged;

        /// <summary>
        /// Görev ilerlemesi güncellendiğinde tetiklenir
        /// </summary>
        public static event Action<string, int, int> OnQuestProgressUpdated;

        #endregion

        #region Serialized Fields

        [Header("=== QUEST DATABASE ===")]
        [SerializeField, Tooltip("Tüm mevcut görevler")]
        private List<QuestData> allQuests = new List<QuestData>();

        [Header("=== SETTINGS ===")]
        [SerializeField, Tooltip("Debug loglarını göster")]
        private bool showDebugLogs = true;

        #endregion

        #region Network Variables

        private NetworkList<QuestProgress> _dailyQuests;
        private readonly NetworkVariable<int> _currentQuestTier = new(0);

        #endregion

        #region Private Fields

        private Dictionary<string, QuestData> _questDatabase;
        private bool _isSubscribedToDayCycle;

        #endregion

        #region Public Properties

        /// <summary>
        /// Mevcut görev tier'ı (UpgradePanel'den)
        /// </summary>
        public int CurrentQuestTier => _currentQuestTier.Value;

        /// <summary>
        /// Günlük görev sayısı
        /// </summary>
        public int DailyQuestCount => _dailyQuests?.Count ?? 0;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeSingleton();
            InitializeNetworkList();
            BuildQuestDatabase();
        }

        private void OnDestroy()
        {
            CleanupSingleton();
            UnsubscribeFromDayCycleEvents();
        }

        #endregion

        #region Network Lifecycle

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            SubscribeToNetworkEvents();
            SubscribeToDayCycleEvents();
            SubscribeToGameEvents();

            if (IsServer)
            {
                AssignDailyQuests();
            }

            Debug.Log($"{LOG_PREFIX} Spawned - IsServer: {IsServer}");
        }

        public override void OnNetworkDespawn()
        {
            UnsubscribeFromNetworkEvents();
            UnsubscribeFromDayCycleEvents();
            UnsubscribeFromGameEvents();

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
                Debug.LogWarning($"{LOG_PREFIX} Duplicate instance detected, destroying...");
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

        private void InitializeNetworkList()
        {
            _dailyQuests = new NetworkList<QuestProgress>();
        }

        private void BuildQuestDatabase()
        {
            _questDatabase = new Dictionary<string, QuestData>();

            foreach (var quest in allQuests)
            {
                if (quest != null && !string.IsNullOrEmpty(quest.questId))
                {
                    _questDatabase[quest.questId] = quest;
                }
            }

            LogDebug($"Quest database built: {_questDatabase.Count} quests");
        }

        private void SubscribeToNetworkEvents()
        {
            _dailyQuests.OnListChanged += HandleDailyQuestsChanged;
            _currentQuestTier.OnValueChanged += HandleQuestTierChanged;
        }

        private void UnsubscribeFromNetworkEvents()
        {
            if (_dailyQuests != null)
            {
                _dailyQuests.OnListChanged -= HandleDailyQuestsChanged;
            }

            _currentQuestTier.OnValueChanged -= HandleQuestTierChanged;
        }

        private void SubscribeToDayCycleEvents()
        {
            if (_isSubscribedToDayCycle) return;

            DayCycleManager.OnNewDay += HandleNewDay;
            _isSubscribedToDayCycle = true;
        }

        private void UnsubscribeFromDayCycleEvents()
        {
            if (!_isSubscribedToDayCycle) return;

            DayCycleManager.OnNewDay -= HandleNewDay;
            _isSubscribedToDayCycle = false;
        }

        private void SubscribeToGameEvents()
        {
            // Subscribe to game events for quest tracking
            QuestTracker.OnMinigameCompleted += HandleMinigameCompleted;
            QuestTracker.OnBoxPlacedOnShelf += HandleBoxPlacedOnShelf;
            QuestTracker.OnTruckCompleted += HandleTruckCompleted;
            QuestTracker.OnCustomerServed += HandleCustomerServed;
            QuestTracker.OnCustomerTimeout += HandleCustomerTimeout;
            QuestTracker.OnToyPacked += HandleToyPacked;
        }

        private void UnsubscribeFromGameEvents()
        {
            QuestTracker.OnMinigameCompleted -= HandleMinigameCompleted;
            QuestTracker.OnBoxPlacedOnShelf -= HandleBoxPlacedOnShelf;
            QuestTracker.OnTruckCompleted -= HandleTruckCompleted;
            QuestTracker.OnCustomerServed -= HandleCustomerServed;
            QuestTracker.OnCustomerTimeout -= HandleCustomerTimeout;
            QuestTracker.OnToyPacked -= HandleToyPacked;
        }

        #endregion

        #region Event Handlers

        private void HandleDailyQuestsChanged(NetworkListEvent<QuestProgress> changeEvent)
        {
            switch (changeEvent.Type)
            {
                case NetworkListEvent<QuestProgress>.EventType.Add:
                case NetworkListEvent<QuestProgress>.EventType.Clear:
                    OnQuestsAssigned?.Invoke();
                    break;

                case NetworkListEvent<QuestProgress>.EventType.Value:
                    var quest = changeEvent.Value;
                    OnQuestStatusChanged?.Invoke(quest.questId.ToString(), quest.status);
                    OnQuestProgressUpdated?.Invoke(quest.questId.ToString(), quest.currentProgress, quest.targetProgress);
                    break;
            }
        }

        private void HandleQuestTierChanged(int previousValue, int newValue)
        {
            LogDebug($"Quest tier changed: {previousValue} -> {newValue}");
        }

        private void HandleNewDay()
        {
            if (!IsServer) return;

            LogDebug("New day - processing incomplete quests and assigning new ones");

            // Apply penalties for incomplete accepted quests
            ApplyPenaltiesForIncompleteQuests();

            // Assign new daily quests
            AssignDailyQuests();
        }

        #endregion

        #region Quest Tracking Event Handlers

        private void HandleMinigameCompleted()
        {
            if (!IsServer) return;
            UpdateQuestProgress(QuestType.CompleteMinigame, BoxInfo.BoxType.Red, 1);
        }

        private void HandleBoxPlacedOnShelf(BoxInfo.BoxType boxType)
        {
            if (!IsServer) return;
            UpdateQuestProgress(QuestType.PlaceBoxOnShelf, boxType, 1);
        }

        private void HandleTruckCompleted()
        {
            if (!IsServer) return;
            UpdateQuestProgress(QuestType.CompleteTruck, BoxInfo.BoxType.Red, 1);
        }

        private void HandleCustomerServed()
        {
            if (!IsServer) return;
            UpdateQuestProgress(QuestType.ServeCustomer, BoxInfo.BoxType.Red, 1);
        }

        private void HandleCustomerTimeout()
        {
            if (!IsServer) return;
            UpdateQuestProgress(QuestType.IgnoreCustomer, BoxInfo.BoxType.Red, 1);
        }

        private void HandleToyPacked(BoxInfo.BoxType boxType)
        {
            if (!IsServer) return;
            UpdateQuestProgress(QuestType.PackToy, boxType, 1);
        }

        #endregion

        #region Quest Assignment

        private void AssignDailyQuests()
        {
            if (!IsServer) return;

            // Clear existing quests
            _dailyQuests.Clear();

            // Get available quests based on tier
            var availableQuests = GetAvailableQuestsForTier();

            if (availableQuests.Count == 0)
            {
                LogDebug("No available quests for current tier!");
                return;
            }

            // Randomly select 3 quests
            var selectedQuests = SelectRandomQuests(availableQuests, DAILY_QUEST_COUNT);

            foreach (var quest in selectedQuests)
            {
                var progress = new QuestProgress(quest.questId, quest.requirement.targetCount);
                _dailyQuests.Add(progress);

                LogDebug($"Assigned quest: {quest.questTitle} (Tier: {quest.tier})");
            }

            NotifyQuestsAssignedClientRpc();
        }

        private List<QuestData> GetAvailableQuestsForTier()
        {
            var available = new List<QuestData>();
            int maxTier = _currentQuestTier.Value;

            foreach (var quest in allQuests)
            {
                if (quest != null && (int)quest.tier <= maxTier)
                {
                    available.Add(quest);
                }
            }

            return available;
        }

        private List<QuestData> SelectRandomQuests(List<QuestData> available, int count)
        {
            var selected = new List<QuestData>();
            var pool = new List<QuestData>(available);

            int selectCount = Mathf.Min(count, pool.Count);

            for (int i = 0; i < selectCount; i++)
            {
                int randomIndex = UnityEngine.Random.Range(0, pool.Count);
                selected.Add(pool[randomIndex]);
                pool.RemoveAt(randomIndex);
            }

            return selected;
        }

        [ClientRpc]
        private void NotifyQuestsAssignedClientRpc()
        {
            OnQuestsAssigned?.Invoke();
        }

        #endregion

        #region Quest Progress

        private void UpdateQuestProgress(QuestType questType, BoxInfo.BoxType boxType, int amount)
        {
            if (!IsServer) return;

            for (int i = 0; i < _dailyQuests.Count; i++)
            {
                var progress = _dailyQuests[i];

                // Skip if not active
                if (progress.status != QuestStatus.Active) continue;

                // Get quest data
                if (!_questDatabase.TryGetValue(progress.questId.ToString(), out QuestData questData)) continue;

                // Check quest type match
                if (questData.questType != questType) continue;

                // Check box type if required
                if (questData.requirement.requireSpecificBoxType && questData.requirement.requiredBoxType != boxType) continue;

                // Update progress
                progress.currentProgress += amount;

                // Check if completed
                if (progress.IsCompleted)
                {
                    progress.status = QuestStatus.Completed;
                    LogDebug($"Quest completed: {questData.questTitle}");
                }

                _dailyQuests[i] = progress;
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Görevi kabul eder
        /// </summary>
        public void AcceptQuest(int slotIndex)
        {
            if (!IsServer)
            {
                AcceptQuestServerRpc(slotIndex);
                return;
            }

            AcceptQuestInternal(slotIndex);
        }

        /// <summary>
        /// Görev ödülünü toplar
        /// </summary>
        public void CollectQuestReward(int slotIndex)
        {
            if (!IsServer)
            {
                CollectQuestRewardServerRpc(slotIndex);
                return;
            }

            CollectQuestRewardInternal(slotIndex);
        }

        /// <summary>
        /// Belirli slot'taki görev bilgisini döndürür
        /// </summary>
        public QuestData GetQuestData(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _dailyQuests.Count) return null;

            var progress = _dailyQuests[slotIndex];

            if (_questDatabase.TryGetValue(progress.questId.ToString(), out QuestData questData))
            {
                return questData;
            }

            return null;
        }

        /// <summary>
        /// Belirli slot'taki görev ilerlemesini döndürür
        /// </summary>
        public QuestProgress GetQuestProgress(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _dailyQuests.Count)
            {
                return default;
            }

            return _dailyQuests[slotIndex];
        }

        /// <summary>
        /// Quest tier'ını ayarlar (UpgradePanel tarafından çağrılır)
        /// </summary>
        public void SetQuestTier(int tier)
        {
            if (!IsServer)
            {
                SetQuestTierServerRpc(tier);
                return;
            }

            _currentQuestTier.Value = tier;
        }

        #endregion

        #region Server RPCs

        [ServerRpc(RequireOwnership = false)]
        private void AcceptQuestServerRpc(int slotIndex)
        {
            AcceptQuestInternal(slotIndex);
        }

        [ServerRpc(RequireOwnership = false)]
        private void CollectQuestRewardServerRpc(int slotIndex)
        {
            CollectQuestRewardInternal(slotIndex);
        }

        [ServerRpc(RequireOwnership = false)]
        private void SetQuestTierServerRpc(int tier)
        {
            _currentQuestTier.Value = tier;
        }

        #endregion

        #region Internal Methods

        private void AcceptQuestInternal(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _dailyQuests.Count) return;

            var progress = _dailyQuests[slotIndex];

            if (progress.status != QuestStatus.Available) return;

            progress.status = QuestStatus.Active;
            _dailyQuests[slotIndex] = progress;

            LogDebug($"Quest accepted: {progress.questId}");
        }

        private void CollectQuestRewardInternal(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _dailyQuests.Count) return;

            var progress = _dailyQuests[slotIndex];

            if (progress.status != QuestStatus.Completed) return;

            // Get quest data
            if (!_questDatabase.TryGetValue(progress.questId.ToString(), out QuestData questData)) return;

            // Apply rewards
            ApplyRewards(questData.rewards);

            // Update status
            progress.status = QuestStatus.Collected;
            _dailyQuests[slotIndex] = progress;

            LogDebug($"Quest reward collected: {questData.questTitle}");
        }

        private void ApplyPenaltiesForIncompleteQuests()
        {
            for (int i = 0; i < _dailyQuests.Count; i++)
            {
                var progress = _dailyQuests[i];

                // Only apply penalty to accepted but not completed quests
                if (progress.status != QuestStatus.Active) continue;

                // Get quest data
                if (!_questDatabase.TryGetValue(progress.questId.ToString(), out QuestData questData)) continue;

                // Apply penalties
                ApplyPenalties(questData.penalties);

                // Update status
                progress.status = QuestStatus.Failed;
                _dailyQuests[i] = progress;

                LogDebug($"Quest failed, penalty applied: {questData.questTitle}");
            }
        }

        private void ApplyRewards(List<QuestReward> rewards)
        {
            if (rewards == null) return;

            foreach (var reward in rewards)
            {
                ApplyRewardOrPenalty(reward, false);
            }
        }

        private void ApplyPenalties(List<QuestReward> penalties)
        {
            if (penalties == null) return;

            // Check for penalty reduction buff
            float penaltyMultiplier = 1f;
            if (BuffManager.Instance != null)
            {
                float reduction = BuffManager.Instance.GetBuffAmount(BuffType.PenaltyReduction);
                penaltyMultiplier = 1f - (reduction / 100f);
                penaltyMultiplier = Mathf.Max(0f, penaltyMultiplier);
            }

            foreach (var penalty in penalties)
            {
                ApplyRewardOrPenalty(penalty, true, penaltyMultiplier);
            }
        }

        private void ApplyRewardOrPenalty(QuestReward reward, bool isPenalty, float multiplier = 1f)
        {
            float amount = reward.amount * multiplier;

            switch (reward.rewardType)
            {
                case RewardType.Money:
                    if (MoneySystem.Instance != null)
                    {
                        MoneySystem.Instance.ModifyMoney((int)amount);
                    }
                    break;

                case RewardType.Prestige:
                    if (PrestigeManager.Instance != null)
                    {
                        PrestigeManager.Instance.ModifyPrestige(amount);
                    }
                    break;

                case RewardType.MaxStamina:
                case RewardType.MoveSpeed:
                case RewardType.CustomerWaitTime:
                case RewardType.WalkSpeed:
                case RewardType.StaminaRegenRate:
                case RewardType.DayDuration:
                case RewardType.MaxQueueSize:
                case RewardType.PenaltyReduction:
                    ApplyPermanentBuff(reward, amount);
                    break;

                case RewardType.TempMoneyBoost:
                case RewardType.TempSpeedBoost:
                    ApplyTemporaryBuff(reward, amount);
                    break;
            }
        }

        private void ApplyPermanentBuff(QuestReward reward, float amount)
        {
            if (BuffManager.Instance == null) return;

            BuffType buffType = reward.rewardType switch
            {
                RewardType.MaxStamina => BuffType.MaxStamina,
                RewardType.MoveSpeed => BuffType.MoveSpeed,
                RewardType.CustomerWaitTime => BuffType.CustomerWaitTime,
                RewardType.WalkSpeed => BuffType.WalkSpeed,
                RewardType.StaminaRegenRate => BuffType.StaminaRegenRate,
                RewardType.DayDuration => BuffType.DayDuration,
                RewardType.MaxQueueSize => BuffType.MaxQueueSize,
                RewardType.PenaltyReduction => BuffType.PenaltyReduction,
                _ => BuffType.MoveSpeed
            };

            BuffManager.Instance.AddPermanentBuff(buffType, amount);
        }

        private void ApplyTemporaryBuff(QuestReward reward, float amount)
        {
            if (BuffManager.Instance == null) return;

            BuffType buffType = reward.rewardType switch
            {
                RewardType.TempMoneyBoost => BuffType.TempMoneyPerBox,
                RewardType.TempSpeedBoost => BuffType.TempSpeedBoost,
                _ => BuffType.TempMoneyPerBox
            };

            BuffManager.Instance.AddTemporaryBuff(buffType, amount, reward.durationDays);
        }

        #endregion

        #region Logging

        private void LogDebug(string message)
        {
            if (showDebugLogs)
            {
                Debug.Log($"{LOG_PREFIX} {message}");
            }
        }

        #endregion

        #region Editor Debug

#if UNITY_EDITOR
        [ContextMenu("Debug: Print Daily Quests")]
        private void DebugPrintDailyQuests()
        {
            Debug.Log($"{LOG_PREFIX} === DAILY QUESTS ({_dailyQuests.Count}) ===");

            for (int i = 0; i < _dailyQuests.Count; i++)
            {
                var progress = _dailyQuests[i];
                var questData = GetQuestData(i);
                string title = questData != null ? questData.questTitle : "Unknown";

                Debug.Log($"  [{i}] {title}: {progress.status} - {progress.GetProgressText()}");
            }
        }

        [ContextMenu("Debug: Force Assign New Quests")]
        private void DebugForceAssignNewQuests()
        {
            if (IsServer)
            {
                AssignDailyQuests();
            }
        }

        [ContextMenu("Debug: Print Quest Database")]
        private void DebugPrintQuestDatabase()
        {
            Debug.Log($"{LOG_PREFIX} === QUEST DATABASE ({_questDatabase.Count}) ===");

            foreach (var kvp in _questDatabase)
            {
                Debug.Log($"  {kvp.Key}: {kvp.Value.questTitle} (Tier: {kvp.Value.tier})");
            }
        }
#endif

        #endregion
    }
}
