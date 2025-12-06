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

        [Header("=== PROGRESS BAR (Devre Dışı) ===")]
        [SerializeField, Tooltip("İlerleme çubuğu dolgu - artık kullanılmıyor")]
        private Image progressFill;

        [Header("=== TIER INDICATOR (Devre Dışı) ===")]
        [SerializeField, Tooltip("Zorluk tier göstergesi - artık kullanılmıyor")]
        private Image tierIndicator;

        [SerializeField, Tooltip("Easy tier rengi")]
        private Color easyColor = Color.green;

        [SerializeField, Tooltip("Medium tier rengi")]
        private Color mediumColor = Color.yellow;

        [SerializeField, Tooltip("Hard tier rengi")]
        private Color hardColor = Color.red;

        [Header("=== STATUS INDICATOR (Devre Dışı) ===")]
        [SerializeField, Tooltip("Durum göstergesi - artık kullanılmıyor")]
        private GameObject completedIndicator;

        [SerializeField, Tooltip("Aktif durum göstergesi - artık kullanılmıyor")]
        private GameObject activeIndicator;

        [Header("=== PROGRESS TEXT COLORS ===")]
        [SerializeField, Tooltip("Tamamlandı rengi")]
        private Color completedColor = new Color(0.2f, 0.8f, 0.2f); // Yeşil

        [SerializeField, Tooltip("Tamamlanamadı rengi")]
        private Color failedColor = new Color(0.9f, 0.2f, 0.2f); // Kırmızı

        [SerializeField, Tooltip("Normal ilerleme rengi")]
        private Color normalProgressColor = Color.white;

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
            HideRemovedElements();
            LocalizationHelper.OnLocaleChanged += OnLocaleChanged;
        }

        private void OnDestroy()
        {
            RemoveButtonListeners();
            LocalizationHelper.OnLocaleChanged -= OnLocaleChanged;
        }

        private void OnLocaleChanged()
        {
            // Refresh UI when locale changes
            if (_currentQuestData != null)
            {
                UpdateUI();
            }
            else
            {
                SetEmptyState();
            }
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

        /// <summary>
        /// Kaldırılan UI elementlerini gizler (Progress Bar, Tier Indicator, Status Indicators)
        /// </summary>
        private void HideRemovedElements()
        {
            // Progress Bar'ı gizle
            if (progressFill != null)
            {
                progressFill.gameObject.SetActive(false);
            }

            // Tier Indicator'ı gizle
            if (tierIndicator != null)
            {
                tierIndicator.gameObject.SetActive(false);
            }

            // Status Indicator'ları gizle
            if (completedIndicator != null)
            {
                completedIndicator.SetActive(false);
            }

            if (activeIndicator != null)
            {
                activeIndicator.SetActive(false);
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
        }

        #endregion

        #region UI Updates

        private void UpdateUI()
        {
            UpdateTextElements();
            UpdateProgressUI();
            UpdateButtonStates();
            HideRemovedElements(); // Her güncellemede kaldırılan elementleri gizle
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
                string rewardLabel = LocalizationHelper.GetLocalizedString("Quest_Reward");
                rewardsText.text = $"{rewardLabel}: {_currentQuestData.GetRewardsSummary()}";
            }

            if (penaltiesText != null)
            {
                string penaltyLabel = LocalizationHelper.GetLocalizedString("Quest_Penalty");
                penaltiesText.text = $"{penaltyLabel}: {_currentQuestData.GetPenaltiesSummary()}";
            }
        }

        private void UpdateProgressUI()
        {
            if (progressText == null) return;

            // Duruma göre progress text'i güncelle
            switch (_currentProgress.status)
            {
                case QuestStatus.Available:
                    // Available durumunda boş bırak
                    progressText.text = "";
                    progressText.color = normalProgressColor;
                    break;

                case QuestStatus.Active:
                    // Aktif durumda ilerlemeyi göster (örn: 0/5)
                    progressText.text = _currentProgress.GetProgressText();
                    progressText.color = normalProgressColor;
                    break;

                case QuestStatus.Completed:
                case QuestStatus.Collected:
                    // Tamamlandı - yeşil renk
                    progressText.text = LocalizationHelper.GetLocalizedString("Quest_Completed");
                    progressText.color = completedColor;
                    break;

                case QuestStatus.Failed:
                    // Tamamlanamadı - kırmızı renk
                    progressText.text = LocalizationHelper.GetLocalizedString("Quest_Failed");
                    progressText.color = failedColor;
                    break;

                default:
                    progressText.text = "";
                    progressText.color = normalProgressColor;
                    break;
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

        private void SetEmptyState()
        {
            if (titleText != null) titleText.text = LocalizationHelper.GetLocalizedString("Quest_NoQuest");
            if (descriptionText != null) descriptionText.text = "";
            if (rewardsText != null) rewardsText.text = "";
            if (penaltiesText != null) penaltiesText.text = "";
            if (progressText != null) 
            {
                progressText.text = "";
                progressText.color = normalProgressColor;
            }

            if (acceptButton != null) acceptButton.gameObject.SetActive(false);
            if (collectButton != null) collectButton.gameObject.SetActive(false);

            HideRemovedElements();
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
