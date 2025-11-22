using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System.Linq;
using System;

namespace NewCss
{
    public class QuestManager : NetworkBehaviour
    {
        public static QuestManager Instance { get; private set; }

        [Header("Quest Pool")]
        [SerializeField] private List<QuestData> allQuests = new List<QuestData>();

        [Header("Daily Quest Settings")]
        [SerializeField] private int dailyQuestCount = 2;

        private NetworkVariable<int> currentQuestTier = new NetworkVariable<int>(1);
        private NetworkList<QuestProgress> dailyQuests;

        public static event Action<QuestProgress> OnQuestProgressUpdated;
        public static event Action OnDailyQuestsGenerated;
        public static event Action<int, int, int> OnQuestRewardClaimed;
        public static event Action<int> OnQuestTierChanged;
        public static event Action<int, int, float> OnQuestPenaltyApplied; // ✨ YENİ: questID, money, prestige

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            dailyQuests = new NetworkList<QuestProgress>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                GenerateDailyQuests();
                DayCycleManager.OnNewDay += OnNewDay;
            }

            dailyQuests.OnListChanged += OnQuestListChanged;
            currentQuestTier.OnValueChanged += OnTierChanged;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                DayCycleManager.OnNewDay -= OnNewDay;
            }

            if (dailyQuests != null)
            {
                dailyQuests.OnListChanged -= OnQuestListChanged;
            }

            currentQuestTier.OnValueChanged -= OnTierChanged;
        }

        private void OnTierChanged(int previousValue, int newValue)
        {
            OnQuestTierChanged?.Invoke(newValue);
        }

        private void OnNewDay()
        {
            if (!IsServer) return;

            // ✅ GÜN SONU - Önce cezaları uygula
            ApplyPenaltiesForIncompletedQuests();

            // Sonra yeni görevleri oluştur
            GenerateDailyQuests();
        }

        /// <summary>
        /// ✨ YENİ: Gün sonunda kabul edilip tamamlanmayan görevlere ceza
        /// </summary>
        private void ApplyPenaltiesForIncompletedQuests()
        {
            if (!IsServer) return;

            for (int i = 0; i < dailyQuests.Count; i++)
            {
                QuestProgress progress = dailyQuests[i];

                // Ceza koşulları:
                // 1. Kabul edilmiş
                // 2. Tamamlanmamış
                // 3. Ceza daha önce uygulanmamış
                if (progress.isAccepted && !progress.isCompleted && !progress.isPenaltyApplied)
                {
                    QuestData questData = GetQuestData(progress.questID);
                    if (questData == null) continue;

                    // Para cezası uygula
                    if (questData.moneyPenalty > 0 && MoneySystem.Instance != null)
                    {
                        MoneySystem.Instance.SpendMoney(questData.moneyPenalty);
                    }

                    // Prestij cezası uygula
                    if (questData.prestigePenalty > 0 && PrestigeManager.Instance != null)
                    {
                        PrestigeManager.Instance.ModifyPrestige(-questData.prestigePenalty);
                    }

                    // Ceza uygulandı olarak işaretle
                    progress.isPenaltyApplied = true;
                    dailyQuests[i] = progress;

                    // Event tetikle
                    OnQuestPenaltyApplied?.Invoke(progress.questID, questData.moneyPenalty, questData.prestigePenalty);
                    NotifyPenaltyAppliedClientRpc(progress.questID, questData.questName, questData.moneyPenalty, questData.prestigePenalty);
                }
            }
        }

        [ClientRpc]
        private void NotifyPenaltyAppliedClientRpc(int questID, string questName, int moneyPenalty, float prestigePenalty)
        {
            OnQuestPenaltyApplied?.Invoke(questID, moneyPenalty, prestigePenalty);
        }

        [ContextMenu("Generate Daily Quests")]
        public void GenerateDailyQuests()
        {
            if (!IsServer) return;

            dailyQuests.Clear();

            if (allQuests.Count == 0)
            {
                return;
            }

            List<QuestData> availableQuests = allQuests
                .Where(q => (int)q.questTier <= currentQuestTier.Value)
                .ToList();

            if (availableQuests.Count == 0)
            {
                return;
            }

            List<QuestData> selectedQuests = SelectQuestsWithTierWeight(availableQuests);

            foreach (var questData in selectedQuests)
            {
                QuestProgress progress = new QuestProgress
                {
                    questID = questData.questID,
                    currentProgress = 0,
                    requiredAmount = questData.requiredAmount,
                    isAccepted = false,
                    isCompleted = false,
                    isRewardClaimed = false,
                    isPenaltyApplied = false // ✨ YENİ
                };

                dailyQuests.Add(progress);
            }

            OnDailyQuestsGenerated?.Invoke();
            NotifyQuestsGeneratedClientRpc();
        }

        private List<QuestData> SelectQuestsWithTierWeight(List<QuestData> availableQuests)
        {
            List<QuestData> selected = new List<QuestData>();

            var tier1Quests = availableQuests.Where(q => q.questTier == QuestTier.Easy).ToList();
            var tier2Quests = availableQuests.Where(q => q.questTier == QuestTier.Medium).ToList();
            var tier3Quests = availableQuests.Where(q => q.questTier == QuestTier.Hard).ToList();

            for (int i = 0; i < dailyQuestCount; i++)
            {
                float random = UnityEngine.Random.value;
                QuestData selectedQuest = null;

                if (random < 0.6f && tier1Quests.Count > 0)
                {
                    selectedQuest = tier1Quests[UnityEngine.Random.Range(0, tier1Quests.Count)];
                    tier1Quests.Remove(selectedQuest);
                }
                else if (random < 0.9f && tier2Quests.Count > 0)
                {
                    selectedQuest = tier2Quests[UnityEngine.Random.Range(0, tier2Quests.Count)];
                    tier2Quests.Remove(selectedQuest);
                }
                else if (tier3Quests.Count > 0)
                {
                    selectedQuest = tier3Quests[UnityEngine.Random.Range(0, tier3Quests.Count)];
                    tier3Quests.Remove(selectedQuest);
                }
                else
                {
                    var remaining = new List<QuestData>();
                    remaining.AddRange(tier1Quests);
                    remaining.AddRange(tier2Quests);
                    remaining.AddRange(tier3Quests);

                    if (remaining.Count > 0)
                    {
                        selectedQuest = remaining[UnityEngine.Random.Range(0, remaining.Count)];
                    }
                }

                if (selectedQuest != null)
                {
                    selected.Add(selectedQuest);
                }
            }

            return selected;
        }

        [ClientRpc]
        private void NotifyQuestsGeneratedClientRpc()
        {
            OnDailyQuestsGenerated?.Invoke();
        }

        [ServerRpc(RequireOwnership = false)]
        public void UpgradeQuestTierServerRpc()
        {
            if (currentQuestTier.Value < 3)
            {
                currentQuestTier.Value++;
                GenerateDailyQuests();
                NotifyTierUpgradedClientRpc(currentQuestTier.Value);
            }
        }

        [ClientRpc]
        private void NotifyTierUpgradedClientRpc(int newTier)
        {
        }

        public int GetCurrentQuestTier()
        {
            return currentQuestTier.Value;
        }

        [ServerRpc(RequireOwnership = false)]
        public void AcceptQuestServerRpc(int questID, ServerRpcParams rpcParams = default)
        {
            for (int i = 0; i < dailyQuests.Count; i++)
            {
                if (dailyQuests[i].questID == questID)
                {
                    QuestProgress progress = dailyQuests[i];
                    progress.isAccepted = true;
                    dailyQuests[i] = progress;

                    NotifyQuestAcceptedClientRpc(questID);
                    return;
                }
            }
        }

        [ClientRpc]
        private void NotifyQuestAcceptedClientRpc(int questID)
        {
        }

        public void IncrementQuestProgress(QuestType questType, BoxInfo.BoxType boxType = BoxInfo.BoxType.Red)
        {
            if (!IsServer) return;

            for (int i = 0; i < dailyQuests.Count; i++)
            {
                QuestProgress progress = dailyQuests[i];

                if (!progress.isAccepted || progress.isCompleted) continue;

                QuestData questData = GetQuestData(progress.questID);
                if (questData == null) continue;

                bool shouldIncrement = false;

                switch (questType)
                {
                    case QuestType.PackageBoxes:
                        if (questData.questType == QuestType.PackageBoxes && questData.targetBoxType == boxType)
                            shouldIncrement = true;
                        break;

                    case QuestType.ServeCustomers:
                    case QuestType.DeliverTrucks:
                    case QuestType.PlaceOnShelf:
                        if (questData.questType == questType)
                            shouldIncrement = true;
                        break;
                }

                if (shouldIncrement)
                {
                    progress.currentProgress++;

                    if (progress.currentProgress >= progress.requiredAmount)
                    {
                        progress.isCompleted = true;
                    }

                    dailyQuests[i] = progress;
                    OnQuestProgressUpdated?.Invoke(progress);
                }
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void ClaimRewardServerRpc(int questID, ServerRpcParams rpcParams = default)
        {
            for (int i = 0; i < dailyQuests.Count; i++)
            {
                if (dailyQuests[i].questID == questID)
                {
                    QuestProgress progress = dailyQuests[i];

                    if (!progress.isCompleted || progress.isRewardClaimed)
                    {
                        return;
                    }

                    QuestData questData = GetQuestData(questID);
                    if (questData == null) return;

                    if (MoneySystem.Instance != null)
                        MoneySystem.Instance.AddMoney(questData.moneyReward);

                    if (PrestigeManager.Instance != null)
                        PrestigeManager.Instance.AddPrestige(questData.prestigeReward);

                    progress.isRewardClaimed = true;
                    dailyQuests[i] = progress;

                    OnQuestRewardClaimed?.Invoke(questID, questData.moneyReward, (int)questData.prestigeReward);
                    NotifyRewardClaimedClientRpc(questID, questData.moneyReward, questData.prestigeReward);
                    return;
                }
            }
        }

        [ClientRpc]
        private void NotifyRewardClaimedClientRpc(int questID, int money, float prestige)
        {
            OnQuestRewardClaimed?.Invoke(questID, money, (int)prestige);
        }

        private void OnQuestListChanged(NetworkListEvent<QuestProgress> changeEvent)
        {
            if (changeEvent.Type == NetworkListEvent<QuestProgress>.EventType.Value)
            {
                OnQuestProgressUpdated?.Invoke(changeEvent.Value);
            }
        }

        public QuestData GetQuestData(int questID)
        {
            return allQuests.Find(q => q.questID == questID);
        }

        public List<QuestProgress> GetDailyQuests()
        {
            List<QuestProgress> questList = new List<QuestProgress>();

            foreach (var quest in dailyQuests)
            {
                questList.Add(quest);
            }

            return questList;
        }

        public int GetQuestCount()
        {
            return dailyQuests.Count;
        }

        [ContextMenu("Debug Quest Status")]
        public void DebugQuestStatus()
        {
            for (int i = 0; i < dailyQuests.Count; i++)
            {
                QuestProgress p = dailyQuests[i];
                QuestData data = GetQuestData(p.questID);
            }
        }

        // ✨ YENİ: Manuel ceza testi
        [ContextMenu("Force Apply Penalties NOW")]
        public void ForceApplyPenalties()
        {
            if (IsServer)
            {
                ApplyPenaltiesForIncompletedQuests();
            }
        }

        [ContextMenu("Force Upgrade to Tier 2")]
        public void ForceUpgradeToTier2()
        {
            if (IsServer)
            {
                currentQuestTier.Value = 2;
                GenerateDailyQuests();
            }
        }

        [ContextMenu("Force Upgrade to Tier 3")]
        public void ForceUpgradeToTier3()
        {
            if (IsServer)
            {
                currentQuestTier.Value = 3;
                GenerateDailyQuests();
            }
        }
    }
}