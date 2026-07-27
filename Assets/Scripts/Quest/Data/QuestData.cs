using System.Collections.Generic;
using UnityEngine;

namespace NewCss.Quest
{
    /// <summary>
    /// ScriptableObject - Görev tanımı
    ///
    /// Ödül/ceza artık HAVUZDAN RASTGELE seçilmiyor: her görev kendi para/prestij ödülünü,
    /// para/prestij cezasını ve (varsa) tek bir buff'ını doğrudan Inspector'da taşır.
    /// Yani kartta ne yazacağı ve oyuncuya ne verileceği birebir buradaki alanlardır.
    ///
    /// Çalışma zamanı arayüzü (SelectedRewards / SelectedPenalties) korundu; QuestManager ve
    /// QuestSlotUI bu listeleri okumaya devam ediyor, listeler yalnızca aşağıdaki alanlardan üretiliyor.
    /// </summary>
    [CreateAssetMenu(fileName = "NewQuest", menuName = "Cargor/Quest Data", order = 1)]
    public class QuestData : ScriptableObject
    {
        #region Serialized Fields

        [Header("=== BASIC INFO ===")]
        [Tooltip("Benzersiz görev ID'si (boş bırakılırsa otomatik üretilir). AYNI ID'DEN İKİ GÖREV OLMAMALI.")]
        public string questId;

        [Tooltip("Görev başlığı - kartın üstünde yazar")]
        public string questTitle;

        [Tooltip("Görev açıklaması - kartta bu yazar. BOŞ bırakılırsa görev tipi ve hedeften otomatik metin üretilir.")]
        [TextArea(2, 4)]
        public string questDescription;

        [Tooltip("Görev kartı ikonu. Boş bırakılırsa quest slot'undaki varsayılan ikon korunur.")]
        public Sprite icon;

        [Header("=== TIER & TYPE ===")]
        [Tooltip("Görev zorluk tier'ı. Oyuncunun 'Görev Kademesi' seviyesi bu tier'a eşit/büyükse havuza girer.")]
        public QuestTier tier = QuestTier.Easy;

        [Tooltip("Görev türü - hangi sistem takip edecek")]
        public QuestType questType;

        [Header("=== REQUIREMENTS ===")]
        [Tooltip("Görev gereksinimleri (hedef sayı, renk/kategori filtresi)")]
        public QuestRequirement requirement;

        [Header("=== ODUL ===")]
        [Tooltip("Para ödülü. 0 = para ödülü yok (kartta para kutusu gizlenir).")]
        public float moneyReward;

        [Tooltip("Prestij ödülü. Ondalık yazılabilir (0.4 / 0.8 / 1.4). 0 = prestij ödülü yok.")]
        public float prestigeReward;

        [Header("=== CEZA (gorev kabul edilip tamamlanamazsa) ===")]
        [Tooltip("Para cezası. POZİTİF yaz - oyuncudan düşülür. 0 = para cezası yok.")]
        public float moneyPenalty;

        [Tooltip("Prestij cezası. POZİTİF yaz - oyuncudan düşülür. 0 = prestij cezası yok.")]
        public float prestigePenalty;

        [Header("=== BUFF / EK ETKI (opsiyonel) ===")]
        [Tooltip("Bu görevde para/prestij dışında bir etki var mı? Kapalıysa kartın ek etki kutusu hiç görünmez.")]
        public bool hasBuff;

        [Tooltip("Etkinin türü. Para/prestij için bunu KULLANMA - yukarıdaki ödül/ceza alanlarını kullan.")]
        public RewardType buffType = RewardType.TempMoneyBoost;

        [Tooltip("Etki miktarı (ör. TempSpeedBoost için hız artışı, PenaltyReduction için yüzde)")]
        public float buffAmount;

        [Tooltip("Kaç gün sürecek (yalnızca geçici buff türleri için)")]
        public int buffDurationDays = 1;

        [Tooltip("Kartta bu etkinin yanında yazacak metin (ör. '2 gün boyunca daha hızlı koş'). Boş bırakılırsa tür+miktardan otomatik üretilir.")]
        public string buffDescription;

        [Tooltip("İşaretlenirse bu etki ödül değil CEZA tarafında uygulanır (görev tamamlanamazsa).")]
        public bool buffIsPenalty;

        #endregion

        #region Private Fields

        // Yukarıdaki alanlardan üretilen çalışma zamanı listeleri (QuestManager + UI bunları okur)
        private List<QuestReward> _selectedRewards;
        private List<QuestReward> _selectedPenalties;

        #endregion

        #region Public Properties

        /// <summary>
        /// Bu görevin verdiği ödüller (para / prestij / varsa buff).
        /// </summary>
        public List<QuestReward> SelectedRewards
        {
            get
            {
                EnsureBuilt();
                return _selectedRewards;
            }
        }

        /// <summary>
        /// Bu görevin uyguladığı cezalar (para / prestij / varsa ceza tarafındaki etki).
        /// </summary>
        public List<QuestReward> SelectedPenalties
        {
            get
            {
                EnsureBuilt();
                return _selectedPenalties;
            }
        }

        #endregion

        #region Validation

        private void OnValidate()
        {
            ValidateQuestId();

            // Inspector'da değer değişince çalışma zamanı listeleri bayat kalmasın
            _selectedRewards = null;
            _selectedPenalties = null;
        }

        private void ValidateQuestId()
        {
            if (string.IsNullOrEmpty(questId))
            {
                questId = $"quest_{System.Guid.NewGuid().ToString().Substring(0, 8)}";
            }
        }

        #endregion

        #region Reward / Penalty Building

        /// <summary>
        /// Listeler henüz üretilmediyse Inspector alanlarından üretir.
        /// </summary>
        private void EnsureBuilt()
        {
            if (_selectedRewards != null && _selectedPenalties != null) return;

            Build();
        }

        private void Build()
        {
            _selectedRewards = new List<QuestReward>();
            _selectedPenalties = new List<QuestReward>();

            // --- ÖDÜL ---
            if (!Mathf.Approximately(moneyReward, 0f))
            {
                _selectedRewards.Add(MakeEntry(RewardType.Money, Mathf.Abs(moneyReward), 0, null));
            }

            if (!Mathf.Approximately(prestigeReward, 0f))
            {
                _selectedRewards.Add(MakeEntry(RewardType.Prestige, Mathf.Abs(prestigeReward), 0, null));
            }

            // --- CEZA (pozitif girilir, negatif uygulanır; yanlışlıkla eksi yazılsa da işaret bozulmaz) ---
            if (!Mathf.Approximately(moneyPenalty, 0f))
            {
                _selectedPenalties.Add(MakeEntry(RewardType.Money, -Mathf.Abs(moneyPenalty), 0, null));
            }

            if (!Mathf.Approximately(prestigePenalty, 0f))
            {
                _selectedPenalties.Add(MakeEntry(RewardType.Prestige, -Mathf.Abs(prestigePenalty), 0, null));
            }

            // --- BUFF / EK ETKİ ---
            if (hasBuff && !Mathf.Approximately(buffAmount, 0f))
            {
                // Buff'ın işareti serbest: negatif de yazılabilir (ör. -20 hız = debuff).
                var buff = MakeEntry(buffType, buffAmount, buffDurationDays, buffDescription);

                if (buffIsPenalty)
                {
                    _selectedPenalties.Add(buff);
                }
                else
                {
                    _selectedRewards.Add(buff);
                }
            }
        }

        private static QuestReward MakeEntry(RewardType type, float amount, int durationDays, string customDescription)
        {
            return new QuestReward
            {
                rewardType = type,
                amount = amount,
                durationDays = durationDays,
                customDescription = customDescription
            };
        }

        /// <summary>
        /// Listeleri yeniden üretir. Ödüller artık sabit olduğu için bu bir "reroll" değil,
        /// sadece tazeleme; isim ve imza QuestManager/QuestProgress akışıyla uyumlu kalsın diye korundu.
        /// </summary>
        public void RerollSelection()
        {
            Build();
        }

        /// <summary>
        /// Eski rastgele-havuz modelinde server'ın ürettiği seed ile deterministik seçim yapılırdı.
        /// Ödüller artık asset'te sabit olduğu için seed'in bir etkisi yok; her client aynı değerleri
        /// zaten aynı asset'ten okuyor. İmza QuestManager.GetQuestData() akışı için korunuyor.
        /// </summary>
        public void RerollSelection(int seed)
        {
            EnsureBuilt();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Kartta gösterilecek açıklama: elle yazılmışsa o, yazılmamışsa gereksinimden üretilen metin.
        /// </summary>
        public string GetFullDescription()
        {
            if (!string.IsNullOrWhiteSpace(questDescription))
            {
                return questDescription;
            }

            return requirement != null ? requirement.GetDescription(questType) : string.Empty;
        }

        /// <summary>
        /// Ödüllerin tek satırlık özeti (log ve eski birleşik UI alanı için)
        /// </summary>
        public string GetRewardsSummary()
        {
            return Summarize(SelectedRewards, "Ödül Yok");
        }

        /// <summary>
        /// Cezaların tek satırlık özeti (log ve eski birleşik UI alanı için)
        /// </summary>
        public string GetPenaltiesSummary()
        {
            return Summarize(SelectedPenalties, "Ceza Yok");
        }

        private static string Summarize(List<QuestReward> entries, string emptyText)
        {
            if (entries == null || entries.Count == 0)
            {
                return emptyText;
            }

            var descriptions = new List<string>();
            foreach (var entry in entries)
            {
                if (entry != null) descriptions.Add(entry.GetDescription());
            }

            return descriptions.Count > 0 ? string.Join(", ", descriptions) : emptyText;
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
            Build();

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
