using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NewCss.Quest
{
    /// <summary>
    /// Tek görev slot'u için UI kontrolü
    /// Her slot görev başlığı, açıklama, ilerleme ve butonları içerir
    /// </summary>
    public class QuestSlotUI : MonoBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[QuestSlotUI]";

        #endregion

        #region Serialized Fields

        [Header("=== TEXT ELEMENTS ===")]
        [SerializeField, Tooltip("Görev başlığı")]
        private TMP_Text titleText;

        [SerializeField, Tooltip("Görev açıklaması")]
        private TMP_Text descriptionText;

        [SerializeField, Tooltip("Ödüller metni")]
        private TMP_Text rewardsText;

        [SerializeField, Tooltip("Cezalar metni")]
        private TMP_Text penaltiesText;

        [SerializeField, Tooltip("İlerleme metni")]
        private TMP_Text progressText;

        [Header("=== BUTTONS ===")]
        [SerializeField, Tooltip("Kabul Et butonu")]
        private Button acceptButton;

        [SerializeField, Tooltip("Topla butonu")]
        private Button collectButton;

        [Header("=== PROGRESS BAR ===")]
        [SerializeField, Tooltip("İlerleme çubuğu dolgu")]
        private Image progressFill;

        [Header("=== TIER INDICATOR ===")]
        [SerializeField, Tooltip("Zorluk tier göstergesi")]
        private Image tierIndicator;

        [SerializeField, Tooltip("Easy tier rengi")]
        private Color easyColor = Color.green;

        [SerializeField, Tooltip("Medium tier rengi")]
        private Color mediumColor = Color.yellow;

        [SerializeField, Tooltip("Hard tier rengi")]
        private Color hardColor = Color.red;

        [Header("=== STATUS INDICATOR ===")]
        [SerializeField, Tooltip("Durum göstergesi")]
        private GameObject completedIndicator;

        [SerializeField, Tooltip("Aktif durum göstergesi")]
        private GameObject activeIndicator;

        #endregion

        #region Private Fields

        private int _slotIndex;
        private QuestData _currentQuestData;
        private QuestProgress _currentProgress;

        #endregion

        #region Public Properties

        /// <summary>
        /// Slot index'i
        /// </summary>
        public int SlotIndex => _slotIndex;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            SetupButtonListeners();
        }

        private void OnDestroy()
        {
            RemoveButtonListeners();
        }

        #endregion

        #region Initialization

        private void SetupButtonListeners()
        {
            if (acceptButton != null)
            {
                acceptButton.onClick.AddListener(OnAcceptClicked);
            }

            if (collectButton != null)
            {
                collectButton.onClick.AddListener(OnCollectClicked);
            }
        }

        private void RemoveButtonListeners()
        {
            if (acceptButton != null)
            {
                acceptButton.onClick.RemoveListener(OnAcceptClicked);
            }

            if (collectButton != null)
            {
                collectButton.onClick.RemoveListener(OnCollectClicked);
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Slot'u görev verileriyle günceller
        /// </summary>
        public void Setup(int slotIndex, QuestData questData, QuestProgress progress)
        {
            _slotIndex = slotIndex;
            _currentQuestData = questData;
            _currentProgress = progress;

            if (questData == null)
            {
                SetEmptyState();
                return;
            }

            UpdateUI();
        }

        /// <summary>
        /// İlerlemeyi günceller
        /// </summary>
        public void UpdateProgress(QuestProgress progress)
        {
            _currentProgress = progress;
            UpdateProgressUI();
            UpdateButtonStates();
            UpdateStatusIndicators();
        }

        #endregion

        #region UI Updates

        private void UpdateUI()
        {
            UpdateTextElements();
            UpdateTierIndicator();
            UpdateProgressUI();
            UpdateButtonStates();
            UpdateStatusIndicators();
        }

        private void UpdateTextElements()
        {
            if (titleText != null)
            {
                titleText.text = _currentQuestData.questTitle;
            }

            if (descriptionText != null)
            {
                descriptionText.text = _currentQuestData.GetFullDescription();
            }

            if (rewardsText != null)
            {
                rewardsText.text = $"Ödül: {_currentQuestData.GetRewardsSummary()}";
            }

            if (penaltiesText != null)
            {
                penaltiesText.text = $"Ceza: {_currentQuestData.GetPenaltiesSummary()}";
            }
        }

        private void UpdateTierIndicator()
        {
            if (tierIndicator == null) return;

            tierIndicator.color = _currentQuestData.tier switch
            {
                QuestTier.Easy => easyColor,
                QuestTier.Medium => mediumColor,
                QuestTier.Hard => hardColor,
                _ => easyColor
            };
        }

        private void UpdateProgressUI()
        {
            if (progressText != null)
            {
                progressText.text = _currentProgress.GetProgressText();
            }

            if (progressFill != null)
            {
                progressFill.fillAmount = _currentProgress.ProgressPercent;
            }
        }

        private void UpdateButtonStates()
        {
            bool canAccept = _currentProgress.status == QuestStatus.Available;
            bool canCollect = _currentProgress.status == QuestStatus.Completed;

            if (acceptButton != null)
            {
                acceptButton.gameObject.SetActive(canAccept);
                acceptButton.interactable = canAccept;
            }

            if (collectButton != null)
            {
                collectButton.gameObject.SetActive(canCollect);
                collectButton.interactable = canCollect;
            }
        }

        private void UpdateStatusIndicators()
        {
            if (completedIndicator != null)
            {
                completedIndicator.SetActive(_currentProgress.status == QuestStatus.Completed ||
                                            _currentProgress.status == QuestStatus.Collected);
            }

            if (activeIndicator != null)
            {
                activeIndicator.SetActive(_currentProgress.status == QuestStatus.Active);
            }
        }

        private void SetEmptyState()
        {
            if (titleText != null) titleText.text = "Görev Yok";
            if (descriptionText != null) descriptionText.text = "";
            if (rewardsText != null) rewardsText.text = "";
            if (penaltiesText != null) penaltiesText.text = "";
            if (progressText != null) progressText.text = "";

            if (progressFill != null) progressFill.fillAmount = 0f;

            if (acceptButton != null) acceptButton.gameObject.SetActive(false);
            if (collectButton != null) collectButton.gameObject.SetActive(false);

            if (completedIndicator != null) completedIndicator.SetActive(false);
            if (activeIndicator != null) activeIndicator.SetActive(false);
        }

        #endregion

        #region Button Handlers

        private void OnAcceptClicked()
        {
            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.AcceptQuest(_slotIndex);
                Debug.Log($"{LOG_PREFIX} Quest accepted at slot {_slotIndex}");
            }
        }

        private void OnCollectClicked()
        {
            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.CollectQuestReward(_slotIndex);
                Debug.Log($"{LOG_PREFIX} Quest reward collected at slot {_slotIndex}");
            }
        }

        #endregion

        #region Editor Debug

#if UNITY_EDITOR
        [ContextMenu("Debug: Print State")]
        private void DebugPrintState()
        {
            Debug.Log($"{LOG_PREFIX} === SLOT {_slotIndex} STATE ===");
            Debug.Log($"Quest: {(_currentQuestData != null ? _currentQuestData.questTitle : "None")}");
            Debug.Log($"Status: {_currentProgress.status}");
            Debug.Log($"Progress: {_currentProgress.GetProgressText()}");
        }
#endif

        #endregion
    }
}
