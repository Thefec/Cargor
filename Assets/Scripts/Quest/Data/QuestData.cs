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

        private const int MAX_SELECTED_REWARDS = 2;
        private const int MAX_SELECTED_PENALTIES = 2;

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

        [Header("=== REWARDS POOL ===")]
        [Tooltip("Olası ödüller havuzu (buradan rastgele maks.  2 seçilir)")]
        public List<QuestReward> rewardPool = new List<QuestReward>();

        [Header("=== PENALTIES POOL ===")]
        [Tooltip("Olası cezalar havuzu (buradan rastgele maks. 2 seçilir)")]
        public List<QuestReward> penaltyPool = new List<QuestReward>();

        #endregion

        #region Private Fields

        // Runtime'da seçilen ödüller ve cezalar
        private List<QuestReward> _selectedRewards;
        private List<QuestReward> _selectedPenalties;
        private bool _isInitialized = false;

        #endregion

        #region Public Properties

        /// <summary>
        /// Seçilmiş ödüller (runtime'da rastgele seçilir)
        /// </summary>
        public List<QuestReward> SelectedRewards
        {
            get
            {
                if (!_isInitialized)
                {
                    InitializeRandomSelection();
                }
                return _selectedRewards;
            }
        }

        /// <summary>
        /// Seçilmiş cezalar (runtime'da rastgele seçilir)
        /// </summary>
        public List<QuestReward> SelectedPenalties
        {
            get
            {
                if (!_isInitialized)
                {
                    InitializeRandomSelection();
                }
                return _selectedPenalties;
            }
        }

        #endregion

        #region Validation

        private void OnValidate()
        {
            ValidateQuestId();
        }

        private void ValidateQuestId()
        {
            if (string.IsNullOrEmpty(questId))
            {
                questId = $"quest_{System.Guid.NewGuid().ToString().Substring(0, 8)}";
            }
        }

        #endregion

        #region Random Selection

        /// <summary>
        /// Havuzlardan rastgele ödül ve ceza seçer
        /// </summary>
        public void InitializeRandomSelection()
        {
            _selectedRewards = GetRandomFromPool(rewardPool, MAX_SELECTED_REWARDS);
            _selectedPenalties = GetRandomFromPool(penaltyPool, MAX_SELECTED_PENALTIES);
            _isInitialized = true;
        }

        /// <summary>
        /// Seçimleri sıfırlar ve yeniden rastgele seçim yapar
        /// </summary>
        public void RerollSelection()
        {
            _isInitialized = false;
            InitializeRandomSelection();
        }

        /// <summary>
        /// Havuzdan rastgele belirtilen sayıda eleman seçer
        /// </summary>
        private List<QuestReward> GetRandomFromPool(List<QuestReward> pool, int maxCount)
        {
            var result = new List<QuestReward>();

            if (pool == null || pool.Count == 0)
            {
                return result;
            }

            // Havuzun bir kopyasını oluştur (shuffle için)
            var tempPool = new List<QuestReward>(pool);

            // Fisher-Yates shuffle
            for (int i = tempPool.Count - 1; i > 0; i--)
            {
                int randomIndex = Random.Range(0, i + 1);
                var temp = tempPool[i];
                tempPool[i] = tempPool[randomIndex];
                tempPool[randomIndex] = temp;
            }

            // İlk maxCount kadar elemanı al
            int selectCount = Mathf.Min(maxCount, tempPool.Count);
            for (int i = 0; i < selectCount; i++)
            {
                result.Add(tempPool[i]);
            }

            return result;
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
        /// Seçilmiş ödüllerin özet açıklamasını döndürür
        /// </summary>
        public string GetRewardsSummary()
        {
            if (SelectedRewards == null || SelectedRewards.Count == 0)
            {
                return "Ödül Yok";
            }

            var descriptions = new List<string>();
            foreach (var reward in SelectedRewards)
            {
                descriptions.Add(reward.GetDescription());
            }

            return string.Join(", ", descriptions);
        }

        /// <summary>
        /// Seçilmiş cezaların özet açıklamasını döndürür
        /// </summary>
        public string GetPenaltiesSummary()
        {
            if (SelectedPenalties == null || SelectedPenalties.Count == 0)
            {
                return "Ceza Yok";
            }

            var descriptions = new List<string>();
            foreach (var penalty in SelectedPenalties)
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

        [ContextMenu("Test Random Selection")]
        private void TestRandomSelection()
        {
            RerollSelection();
            Debug.Log($"=== QUEST: {questTitle} - RANDOM TEST ===\n" +
                      $"Reward Pool: {rewardPool.Count} items\n" +
                      $"Selected Rewards: {GetRewardsSummary()}\n" +
                      $"Penalty Pool: {penaltyPool.Count} items\n" +
                      $"Selected Penalties: {GetPenaltiesSummary()}");
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