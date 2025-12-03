using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NewCss.Quest
{
    /// <summary>
    /// Görev paneli UI kontrolcüsü
    /// 3 görev slot'unu yönetir, animasyonlu açılış/kapanış sağlar
    /// </summary>
    public class QuestUIController : MonoBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[QuestUIController]";
        private const int QUEST_SLOT_COUNT = 3;
        private const float DEFAULT_OPEN_ANIMATION_DURATION = 0.5f;
        private const float DEFAULT_CLOSE_ANIMATION_DURATION = 0.3f;

        // Animator trigger names
        private const string TRIGGER_OPEN = "Open";
        private const string TRIGGER_CLOSE = "Close";

        #endregion

        #region Serialized Fields

        [Header("=== PANEL SETTINGS ===")]
        [SerializeField, Tooltip("Ana panel GameObject")]
        private GameObject questPanel;

        [SerializeField, Tooltip("Panel animator")]
        private Animator panelAnimator;

        [Header("=== QUEST SLOTS ===")]
        [SerializeField, Tooltip("Görev slot'ları")]
        private List<QuestSlotUI> questSlots = new List<QuestSlotUI>();

        [Header("=== BUTTONS ===")]
        [SerializeField, Tooltip("Kapat butonu")]
        private Button closeButton;

        [Header("=== DEBUG ===")]
        [SerializeField, Tooltip("Debug loglarını göster")]
        private bool showDebugLogs = true;

        #endregion

        #region Private Fields

        private bool _isPanelOpen;
        private bool _isAnimating;
        private PlayerMovement _localPlayerMovement;

        // Cached animation durations
        private float _cachedOpenDuration = -1f;
        private float _cachedCloseDuration = -1f;

        #endregion

        #region Public Properties

        /// <summary>
        /// Panel açık mı?
        /// </summary>
        public bool IsPanelOpen => _isPanelOpen;

        /// <summary>
        /// Animasyon devam ediyor mu?
        /// </summary>
        public bool IsAnimating => _isAnimating;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            SubscribeToQuestEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromQuestEvents();
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        #endregion

        #region Initialization

        private void Initialize()
        {
            SetupPanel();
            SetupAnimator();
            SetupCloseButton();
            CacheAnimationDurations();
        }

        private void SetupPanel()
        {
            if (questPanel != null)
            {
                questPanel.SetActive(false);
            }
            else
            {
                LogWarning("questPanel is not assigned!");
            }
        }

        private void SetupAnimator()
        {
            if (panelAnimator != null) return;

            if (questPanel != null)
            {
                panelAnimator = questPanel.GetComponent<Animator>();
            }
        }

        private void SetupCloseButton()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(ClosePanel);
            }
        }

        private void CacheAnimationDurations()
        {
            if (panelAnimator == null) return;

            var controller = panelAnimator.runtimeAnimatorController;
            if (controller == null) return;

            foreach (var clip in controller.animationClips)
            {
                if (clip.name.Contains("Open") || clip.name.Contains("Opening"))
                {
                    _cachedOpenDuration = clip.length;
                }
                else if (clip.name.Contains("Close") || clip.name.Contains("Exit"))
                {
                    _cachedCloseDuration = clip.length;
                }
            }
        }

        private void SubscribeToQuestEvents()
        {
            QuestManager.OnQuestsAssigned += RefreshAllSlots;
            QuestManager.OnQuestProgressUpdated += HandleQuestProgressUpdated;
            QuestManager.OnQuestStatusChanged += HandleQuestStatusChanged;
        }

        private void UnsubscribeFromQuestEvents()
        {
            QuestManager.OnQuestsAssigned -= RefreshAllSlots;
            QuestManager.OnQuestProgressUpdated -= HandleQuestProgressUpdated;
            QuestManager.OnQuestStatusChanged -= HandleQuestStatusChanged;
        }

        #endregion

        #region Event Handlers

        private void HandleQuestProgressUpdated(string questId, int current, int target)
        {
            RefreshAllSlots();
        }

        private void HandleQuestStatusChanged(string questId, QuestStatus status)
        {
            RefreshAllSlots();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Paneli açar
        /// </summary>
        public void OpenPanel()
        {
            if (_isPanelOpen || _isAnimating)
            {
                LogDebug("Panel already open or animating");
                return;
            }

            _isPanelOpen = true;
            _isAnimating = true;

            // Lock player movement
            LockPlayerMovement(true);

            // Activate panel
            questPanel.SetActive(true);

            // Refresh slots
            RefreshAllSlots();

            // Play animation
            if (panelAnimator != null)
            {
                StartCoroutine(PlayOpenAnimationCoroutine());
            }
            else
            {
                _isAnimating = false;
                LogDebug("Panel opened without animation");
            }
        }

        /// <summary>
        /// Paneli kapatır
        /// </summary>
        public void ClosePanel()
        {
            if (!_isPanelOpen || _isAnimating)
            {
                LogDebug("Panel not open or animating");
                return;
            }

            _isPanelOpen = false;
            _isAnimating = true;

            // Unlock player movement
            LockPlayerMovement(false);

            // Play animation
            if (panelAnimator != null)
            {
                StartCoroutine(PlayCloseAnimationCoroutine());
            }
            else
            {
                questPanel.SetActive(false);
                _isAnimating = false;
                LogDebug("Panel closed without animation");
            }

            _localPlayerMovement = null;
        }

        /// <summary>
        /// Paneli toggle eder
        /// </summary>
        public void TogglePanel()
        {
            if (_isPanelOpen)
            {
                ClosePanel();
            }
            else
            {
                OpenPanel();
            }
        }

        /// <summary>
        /// Oyuncu referansını ayarlar
        /// </summary>
        public void SetLocalPlayer(PlayerMovement player)
        {
            _localPlayerMovement = player;
        }

        #endregion

        #region Slot Management

        private void RefreshAllSlots()
        {
            if (QuestManager.Instance == null)
            {
                LogDebug("QuestManager not found");
                return;
            }

            for (int i = 0; i < questSlots.Count && i < QUEST_SLOT_COUNT; i++)
            {
                if (questSlots[i] == null) continue;

                var questData = QuestManager.Instance.GetQuestData(i);
                var progress = QuestManager.Instance.GetQuestProgress(i);

                questSlots[i].Setup(i, questData, progress);
            }

            LogDebug($"Refreshed {questSlots.Count} slots");
        }

        #endregion

        #region Player Movement Control

        private void LockPlayerMovement(bool locked)
        {
            if (_localPlayerMovement == null)
            {
                LogDebug("No local player movement reference");
                return;
            }

            _localPlayerMovement.LockMovement(locked);
            LogDebug($"Player movement {(locked ? "locked" : "unlocked")}");
        }

        #endregion

        #region Animation Coroutines

        private IEnumerator PlayOpenAnimationCoroutine()
        {
            yield return null;

            ResetAnimatorTriggers();
            panelAnimator.SetTrigger(TRIGGER_OPEN);

            float duration = GetOpenAnimationDuration();
            LogDebug($"Playing open animation ({duration}s)");

            yield return new WaitForSeconds(duration);

            _isAnimating = false;
            LogDebug("Open animation completed");
        }

        private IEnumerator PlayCloseAnimationCoroutine()
        {
            ResetAnimatorTriggers();
            panelAnimator.SetTrigger(TRIGGER_CLOSE);

            float duration = GetCloseAnimationDuration();
            LogDebug($"Playing close animation ({duration}s)");

            yield return new WaitForSeconds(duration);

            if (questPanel != null)
            {
                questPanel.SetActive(false);
            }

            _isAnimating = false;
            LogDebug("Close animation completed");
        }

        private void ResetAnimatorTriggers()
        {
            if (panelAnimator == null) return;

            panelAnimator.ResetTrigger(TRIGGER_OPEN);
            panelAnimator.ResetTrigger(TRIGGER_CLOSE);
        }

        private float GetOpenAnimationDuration()
        {
            return _cachedOpenDuration > 0 ? _cachedOpenDuration : DEFAULT_OPEN_ANIMATION_DURATION;
        }

        private float GetCloseAnimationDuration()
        {
            return _cachedCloseDuration > 0 ? _cachedCloseDuration : DEFAULT_CLOSE_ANIMATION_DURATION;
        }

        #endregion

        #region Cleanup

        private void Cleanup()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(ClosePanel);
            }

            StopAllCoroutines();
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

        private void LogWarning(string message)
        {
            Debug.LogWarning($"{LOG_PREFIX} {message}");
        }

        #endregion

        #region Editor Debug

#if UNITY_EDITOR
        [ContextMenu("Open Panel")]
        private void DebugOpenPanel()
        {
            OpenPanel();
        }

        [ContextMenu("Close Panel")]
        private void DebugClosePanel()
        {
            ClosePanel();
        }

        [ContextMenu("Refresh Slots")]
        private void DebugRefreshSlots()
        {
            RefreshAllSlots();
        }

        [ContextMenu("Debug: Print State")]
        private void DebugPrintState()
        {
            Debug.Log($"{LOG_PREFIX} === UI CONTROLLER STATE ===");
            Debug.Log($"Is Panel Open: {_isPanelOpen}");
            Debug.Log($"Is Animating: {_isAnimating}");
            Debug.Log($"Slot Count: {questSlots.Count}");
            Debug.Log($"Has Local Player: {_localPlayerMovement != null}");
        }
#endif

        #endregion
    }
}
