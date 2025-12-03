using System.Collections.Generic;
using UnityEngine;

namespace NewCss.Quest
{
    /// <summary>
    /// ScriptableObject - Görev tanımı
    /// Unity Editor'den kolayca görev oluşturma sağlar
    /// </summary>
    [CreateAssetMenu(fileName = "NewQuest", menuName = "Cargor/Quest Data", order = 1)]
    public class QuestData : ScriptableObject
    {
        #region Constants

        private const int MAX_REWARDS = 2;
        private const int MAX_PENALTIES = 2;

        #endregion

        #region Serialized Fields

        [Header("=== BASIC INFO ===")]
        [Tooltip("Benzersiz görev ID'si")]
        public string questId;

        [Tooltip("Görev başlığı")]
        public string questTitle;

        [Tooltip("Görev açıklaması")]
        [TextArea(2, 4)]
        public string questDescription;

        [Header("=== TIER & TYPE ===")]
        [Tooltip("Görev zorluk tier'ı")]
        public QuestTier tier = QuestTier.Easy;

        [Tooltip("Görev türü")]
        public QuestType questType;

        [Header("=== REQUIREMENTS ===")]
        [Tooltip("Görev gereksinimleri")]
        public QuestRequirement requirement;

        [Header("=== REWARDS ===")]
        [Tooltip("Görev ödülleri (maks. 2)")]
        public List<QuestReward> rewards = new List<QuestReward>();

        [Header("=== PENALTIES ===")]
        [Tooltip("Başarısızlık cezaları (maks. 2)")]
        public List<QuestReward> penalties = new List<QuestReward>();

        #endregion

        #region Validation

        private void OnValidate()
        {
            ValidateQuestId();
            ValidateRewardsAndPenalties();
        }

        private void ValidateQuestId()
        {
            if (string.IsNullOrEmpty(questId))
            {
                questId = $"quest_{System.Guid.NewGuid().ToString().Substring(0, 8)}";
            }
        }

        private void ValidateRewardsAndPenalties()
        {
            // Limit rewards to MAX_REWARDS
            if (rewards.Count > MAX_REWARDS)
            {
                rewards = rewards.GetRange(0, MAX_REWARDS);
                Debug.LogWarning($"[QuestData] {questId}: Rewards limited to {MAX_REWARDS}");
            }

            // Limit penalties to MAX_PENALTIES
            if (penalties.Count > MAX_PENALTIES)
            {
                penalties = penalties.GetRange(0, MAX_PENALTIES);
                Debug.LogWarning($"[QuestData] {questId}: Penalties limited to {MAX_PENALTIES}");
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Tam görev açıklamasını döndürür
        /// </summary>
        public string GetFullDescription()
        {
            if (requirement == null)
            {
                return questDescription;
            }

            return requirement.GetDescription(questType);
        }

        /// <summary>
        /// Ödüllerin özet açıklamasını döndürür
        /// </summary>
        public string GetRewardsSummary()
        {
            if (rewards == null || rewards.Count == 0)
            {
                return "Ödül Yok";
            }

            var descriptions = new List<string>();
            foreach (var reward in rewards)
            {
                descriptions.Add(reward.GetDescription());
            }

            return string.Join(", ", descriptions);
        }

        /// <summary>
        /// Cezaların özet açıklamasını döndürür
        /// </summary>
        public string GetPenaltiesSummary()
        {
            if (penalties == null || penalties.Count == 0)
            {
                return "Ceza Yok";
            }

            var descriptions = new List<string>();
            foreach (var penalty in penalties)
            {
                descriptions.Add(penalty.GetDescription());
            }

            return string.Join(", ", descriptions);
        }

        #endregion

        #region Editor Debug

#if UNITY_EDITOR
        [ContextMenu("Generate Random ID")]
        private void GenerateRandomId()
        {
            questId = $"quest_{System.Guid.NewGuid().ToString().Substring(0, 8)}";
            UnityEditor.EditorUtility.SetDirty(this);
        }

        [ContextMenu("Print Quest Info")]
        private void PrintQuestInfo()
        {
            Debug.Log($"=== QUEST: {questTitle} ===\n" +
                      $"ID: {questId}\n" +
                      $"Tier: {tier}\n" +
                      $"Type: {questType}\n" +
                      $"Description: {GetFullDescription()}\n" +
                      $"Rewards: {GetRewardsSummary()}\n" +
                      $"Penalties: {GetPenaltiesSummary()}");
        }
#endif

        #endregion
    }
}
