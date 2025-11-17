using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace NewCss
{
    public class QuestSlot : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private TextMeshProUGUI questNameText;
        [SerializeField] private TextMeshProUGUI questDescriptionText;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private TextMeshProUGUI rewardText;
        [SerializeField] private TextMeshProUGUI penaltyText; // ✨ YENİ

        [Header("Buttons")]
        [SerializeField] private Button acceptButton;
        [SerializeField] private Button claimButton;

        [Header("Colors")]
        [SerializeField] private Color acceptedColor = Color.green;
        [SerializeField] private Color completedColor = Color.yellow;
        [SerializeField] private Color claimedColor = Color.gray;
        [SerializeField] private Color penaltyColor = Color.red; // ✨ YENİ

        private QuestData currentQuestData;
        private QuestProgress currentProgress;

        void Awake()
        {
            if (acceptButton != null)
                acceptButton.onClick.AddListener(OnAcceptClicked);

            if (claimButton != null)
                claimButton.onClick.AddListener(OnClaimClicked);
        }

        public void Setup(QuestData questData, QuestProgress progress)
        {
            currentQuestData = questData;
            currentProgress = progress;

            if (questNameText != null)
            {
                string tierBadge = questData.questTier switch
                {
                    QuestTier.Easy => "⭐",
                    QuestTier.Medium => "⭐⭐",
                    QuestTier.Hard => "⭐⭐⭐",
                    _ => ""
                };

                questNameText.text = $"{tierBadge} {questData.questName}";
            }

            if (questDescriptionText != null)
                questDescriptionText.text = questData.questDescription;

            UpdateProgress();
            UpdateButtons();
            UpdateRewardText();
            UpdatePenaltyText(); // ✨ YENİ
        }

        private void UpdateProgress()
        {
            if (progressText != null)
            {
                progressText.text = $"Progress: {currentProgress.currentProgress}/{currentProgress.requiredAmount}";

                if (currentProgress.isRewardClaimed)
                    progressText.color = claimedColor;
                else if (currentProgress.isCompleted)
                    progressText.color = completedColor;
                else if (currentProgress.isAccepted)
                    progressText.color = acceptedColor;
            }
        }

        private void UpdateButtons()
        {
            if (acceptButton != null)
            {
                bool canAccept = !currentProgress.isAccepted && !currentProgress.isCompleted;
                acceptButton.interactable = canAccept;
                acceptButton.gameObject.SetActive(canAccept);
            }

            if (claimButton != null)
            {
                bool canClaim = currentProgress.isCompleted && !currentProgress.isRewardClaimed;
                claimButton.interactable = canClaim;
                claimButton.gameObject.SetActive(currentProgress.isAccepted);
            }
        }

        private void UpdateRewardText()
        {
            if (rewardText != null && currentQuestData != null)
            {
                rewardText.text = $"💰 {currentQuestData.moneyReward}$  ⭐ {currentQuestData.prestigeReward:F2} Prestige";
            }
        }

        /// <summary>
        /// ✨ YENİ: Ceza metnini göster
        /// </summary>
        private void UpdatePenaltyText()
        {
            if (penaltyText != null && currentQuestData != null)
            {
                // Sadece kabul edilmiş görevlerde ceza uyarısı göster
                if (currentProgress.isAccepted && !currentProgress.isCompleted && !currentProgress.isPenaltyApplied)
                {
                    penaltyText.gameObject.SetActive(true);
                    penaltyText.color = penaltyColor;
                    penaltyText.text = $"⚠️ Penalty if not completed:\n💀 -{currentQuestData.moneyPenalty}$ | ⭐ -{currentQuestData.prestigePenalty:F2}";
                }
                else
                {
                    penaltyText.gameObject.SetActive(false);
                }
            }
        }

        private void OnAcceptClicked()
        {
            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.AcceptQuestServerRpc(currentQuestData.questID);
                Debug.Log($"Accepted quest: {currentQuestData.questName}");
            }
        }

        private void OnClaimClicked()
        {
            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.ClaimRewardServerRpc(currentQuestData.questID);
                Debug.Log($"Claimed reward for: {currentQuestData.questName}");
            }
        }
    }
}