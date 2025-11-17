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
            Debug.Log($"Quest Tier changed: {previousValue} → {newValue}");
            OnQuestTierChanged?.Invoke(newValue);
        }

        private void OnNewDay()
        {
            if (!IsServer) return;

            // ✅ GÜN SONU - Önce cezaları uygula
            ApplyPenaltiesForIncompletedQuests();

            // Sonra yeni görevleri oluştur
            Debug.Log("New day started - Resetting quests");
            GenerateDailyQuests();
        }

        /// <summary>
        /// ✨ YENİ: Gün sonunda kabul edilip tamamlanmayan görevlere ceza
        /// </summary>
        private void ApplyPenaltiesForIncompletedQuests()
        {
            if (!IsServer) return;

            Debug.Log("=== CHECKING QUEST PENALTIES ===");

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
                        Debug.Log($"💀 Money penalty applied: -{questData.moneyPenalty}$ for quest '{questData.questName}'");
                    }

                    // Prestij cezası uygula
                    if (questData.prestigePenalty > 0 && PrestigeManager.Instance != null)
                    {
                        PrestigeManager.Instance.ModifyPrestige(-questData.prestigePenalty);
                        Debug.Log($"💀 Prestige penalty applied: -{questData.prestigePenalty} for quest '{questData.questName}'");
                    }

                    // Ceza uygulandı olarak işaretle
                    progress.isPenaltyApplied = true;
                    dailyQuests[i] = progress;

                    // Event tetikle
                    OnQuestPenaltyApplied?.Invoke(progress.questID, questData.moneyPenalty, questData.prestigePenalty);
                    NotifyPenaltyAppliedClientRpc(progress.questID, questData.questName, questData.moneyPenalty, questData.prestigePenalty);
                }
            }

            Debug.Log("=== QUEST PENALTIES DONE ===");
        }

        [ClientRpc]
        private void NotifyPenaltyAppliedClientRpc(int questID, string questName, int moneyPenalty, float prestigePenalty)
        {
            Debug.Log($"⚠️ PENALTY: Quest '{questName}' not completed!");
            Debug.Log($"💰 Money lost: {moneyPenalty}$");
            Debug.Log($"⭐ Prestige lost: {prestigePenalty}");

            OnQuestPenaltyApplied?.Invoke(questID, moneyPenalty, prestigePenalty);
        }

        [ContextMenu("Generate Daily Quests")]
        public void GenerateDailyQuests()
        {
            if (!IsServer) return;

            dailyQuests.Clear();

            if (allQuests.Count == 0)
            {
                Debug.LogError("QuestManager: No quests in pool!");
                return;
            }

            List<QuestData> availableQuests = allQuests
                .Where(q => (int)q.questTier <= currentQuestTier.Value)
                .ToList();

            if (availableQuests.Count == 0)
            {
                Debug.LogWarning($"No quests available for tier {currentQuestTier.Value}!");
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
                Debug.Log($"✅ Generated {questData.questTier} quest: {questData.questName}");
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
            Debug.Log("Daily quests generated!");
        }

        [ServerRpc(RequireOwnership = false)]
        public void UpgradeQuestTierServerRpc()
        {
            if (currentQuestTier.Value < 3)
            {
                currentQuestTier.Value++;
                Debug.Log($"✅ Quest Tier upgraded to: {currentQuestTier.Value}");
                GenerateDailyQuests();
                NotifyTierUpgradedClientRpc(currentQuestTier.Value);
            }
        }

        [ClientRpc]
        private void NotifyTierUpgradedClientRpc(int newTier)
        {
            Debug.Log($"🎉 Quest Tier unlocked: Tier {newTier}!");
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

                    Debug.Log($"Quest {questID} accepted by client {rpcParams.Receive.SenderClientId}");
                    NotifyQuestAcceptedClientRpc(questID);
                    return;
                }
            }
        }

        [ClientRpc]
        private void NotifyQuestAcceptedClientRpc(int questID)
        {
            Debug.Log($"Quest {questID} accepted!");
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
                        Debug.Log($"✅ Quest {questData.questName} COMPLETED!");
                    }

                    dailyQuests[i] = progress;
                    OnQuestProgressUpdated?.Invoke(progress);

                    Debug.Log($"Quest {questData.questName}: {progress.currentProgress}/{progress.requiredAmount}");
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
                        Debug.LogWarning($"Cannot claim reward for quest {questID}");
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

                    Debug.Log($"✅ Reward claimed for {questData.questName}: {questData.moneyReward}$ + {questData.prestigeReward} prestige");

                    OnQuestRewardClaimed?.Invoke(questID, questData.moneyReward, (int)questData.prestigeReward);
                    NotifyRewardClaimedClientRpc(questID, questData.moneyReward, questData.prestigeReward);
                    return;
                }
            }
        }

        [ClientRpc]
        private void NotifyRewardClaimedClientRpc(int questID, int money, float prestige)
        {
            Debug.Log($"Reward claimed: {money}$ + {prestige} prestige");
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
            Debug.Log("=== DAILY QUESTS STATUS ===");
            Debug.Log($"Current Tier: {currentQuestTier.Value}");
            for (int i = 0; i < dailyQuests.Count; i++)
            {
                QuestProgress p = dailyQuests[i];
                QuestData data = GetQuestData(p.questID);
                Debug.Log($"[{i}] {data?.questName ?? "Unknown"} (Tier {data?.questTier}) - {p.currentProgress}/{p.requiredAmount}");
                Debug.Log($"    Accepted: {p.isAccepted} | Completed: {p.isCompleted} | Claimed: {p.isRewardClaimed} | Penalty: {p.isPenaltyApplied}");
            }
            Debug.Log("===========================");
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