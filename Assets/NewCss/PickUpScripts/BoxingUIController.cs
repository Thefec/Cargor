using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NewCss
{
    /// <summary>
    /// Kutu paketleme minigame UI kontrolcüsü.  
    /// Yön tuşu gösterimleri, progress indicator'lar ve feedback sistemini yönetir.
    /// </summary>
    public class BoxingUIController : MonoBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[BoxingUI]";
        private const int REQUIRED_PROGRESS_INDICATORS = 3;
        private const int TOTAL_STEPS = 3;
        private const float DEFAULT_FADE_DURATION = 0.15f;
        private const float DEFAULT_FEEDBACK_HIDE_DELAY = 0.3f;

        // Localized strings
        private const string PROMPT_WATCH = "Yön tuşlarını takip edin! (3 FARKLI tuş)";
        private const string PROMPT_INPUT = "Şimdi sıra sizde! (3 FARKLI tuş)";
        private const string FEEDBACK_CORRECT_FORMAT = "✓ DOĞRU! ({0}/3)";
        private const string FEEDBACK_WRONG = "✗ YANLIŞ!";
        private const string FEEDBACK_FAILURE = "❌ BAŞARISIZ!\nKutu parçalandı!";

        #endregion

        #region Serialized Fields - UI References

        [Header("=== UI REFERENCES ===")]
        [SerializeField, Tooltip("Ana UI paneli")]
        private GameObject uiPanel;

        [SerializeField, Tooltip("Talimat text'i")]
        private TextMeshProUGUI promptText;

        [SerializeField, Tooltip("Geri bildirim text'i")]
        private TextMeshProUGUI feedbackText;

        [SerializeField, Tooltip("İlerleme göstergeleri (3 adet)")]
        private Image[] progressIndicators;

        #endregion

        #region Serialized Fields - Red Arrows

        [Header("=== ARROW IMAGES - RED ===")]
        [SerializeField] private Image redUpImage;
        [SerializeField] private Image redDownImage;
        [SerializeField] private Image redLeftImage;
        [SerializeField] private Image redRightImage;

        #endregion

        #region Serialized Fields - Yellow Arrows

        [Header("=== ARROW IMAGES - YELLOW ===")]
        [SerializeField] private Image yellowUpImage;
        [SerializeField] private Image yellowDownImage;
        [SerializeField] private Image yellowLeftImage;
        [SerializeField] private Image yellowRightImage;

        #endregion

        #region Serialized Fields - Blue Arrows

        [Header("=== ARROW IMAGES - BLUE ===")]
        [SerializeField] private Image blueUpImage;
        [SerializeField] private Image blueDownImage;
        [SerializeField] private Image blueLeftImage;
        [SerializeField] private Image blueRightImage;

        #endregion

        #region Serialized Fields - Settings

        [Header("=== ANIMATION SETTINGS ===")]
        [SerializeField, Tooltip("Fade in süresi")]
        private float fadeInDuration = DEFAULT_FADE_DURATION;

        [SerializeField, Tooltip("Fade out süresi")]
        private float fadeOutDuration = DEFAULT_FADE_DURATION;

        [SerializeField, Tooltip("Feedback gizlenme gecikmesi")]
        private float feedbackHideDelay = DEFAULT_FEEDBACK_HIDE_DELAY;

        [Header("=== DEBUG ===")]
        [SerializeField, Tooltip("Debug loglarını göster")]
        private bool showDebugLogs;

        #endregion

        #region Private Fields

        private BoxInfo.BoxType _currentBoxType;
        private Image _currentActiveImage;
        private Coroutine _fadeCoroutine;
        private Coroutine _feedbackCoroutine;

        // Cached arrow mappings
        private Dictionary<BoxInfo.BoxType, Dictionary<KeyCode, Image>> _arrowMappings;

        #endregion

        #region Public Properties

        /// <summary>
        /// UI görünür mü?
        /// </summary>
        public bool IsVisible => uiPanel != null && uiPanel.activeSelf;

        /// <summary>
        /// Mevcut kutu tipi
        /// </summary>
        public BoxInfo.BoxType CurrentBoxType => _currentBoxType;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            Initialize();
        }

        private void OnDisable()
        {
            StopAllActiveCoroutines();
        }

        #endregion

        #region Initialization

        private void Initialize()
        {
            BuildArrowMappings();
            InitializeUIState();
            ValidateProgressIndicators();
        }

        private void BuildArrowMappings()
        {
            _arrowMappings = new Dictionary<BoxInfo.BoxType, Dictionary<KeyCode, Image>>
            {
                [BoxInfo.BoxType.Red] = new Dictionary<KeyCode, Image>
                {
                    [KeyCode.UpArrow] = redUpImage,
                    [KeyCode.DownArrow] = redDownImage,
                    [KeyCode.LeftArrow] = redLeftImage,
                    [KeyCode.RightArrow] = redRightImage
                },
                [BoxInfo.BoxType.Yellow] = new Dictionary<KeyCode, Image>
                {
                    [KeyCode.UpArrow] = yellowUpImage,
                    [KeyCode.DownArrow] = yellowDownImage,
                    [KeyCode.LeftArrow] = yellowLeftImage,
                    [KeyCode.RightArrow] = yellowRightImage
                },
                [BoxInfo.BoxType.Blue] = new Dictionary<KeyCode, Image>
                {
                    [KeyCode.UpArrow] = blueUpImage,
                    [KeyCode.DownArrow] = blueDownImage,
                    [KeyCode.LeftArrow] = blueLeftImage,
                    [KeyCode.RightArrow] = blueRightImage
                }
            };
        }

        private void InitializeUIState()
        {
            SetPanelActive(false);
            HideAllArrows();
        }

        private void ValidateProgressIndicators()
        {
            if (progressIndicators == null) return;

            if (progressIndicators.Length != REQUIRED_PROGRESS_INDICATORS)
            {
                LogWarning($"Progress indicators count is {progressIndicators.Length}, should be {REQUIRED_PROGRESS_INDICATORS}!");
            }
        }

        #endregion

        #region Public API - Show/Hide UI

        /// <summary>
        /// UI'ı gösterir ve başlangıç durumuna getirir
        /// </summary>
        public void ShowUI(BoxInfo.BoxType boxType)
        {
            _currentBoxType = boxType;

            SetPanelActive(true);
            ShowPromptText(PROMPT_WATCH);
            HideFeedbackText();
            HideAllArrows();
            ResetProgressIndicators();

            LogDebug($"UI shown for box type: {boxType}");
        }

        /// <summary>
        /// UI'ı gizler
        /// </summary>
        public void HideUI()
        {
            StopAllActiveCoroutines();
            SetPanelActive(false);
            HideAllArrows();

            LogDebug("UI hidden");
        }

        #endregion

        #region Public API - Key Display

        /// <summary>
        /// Belirtilen tuşun görselini gösterir
        /// </summary>
        public void ShowKey(KeyCode key)
        {
            Image targetImage = GetImageForKey(key, _currentBoxType);

            if (targetImage == null)
            {
                LogWarning($"No image found for key {key} and box type {_currentBoxType}");
                return;
            }

            targetImage.gameObject.SetActive(true);
            _currentActiveImage = targetImage;

            StartFadeIn(targetImage);

            LogDebug($"Showing key: {key}");
        }

        /// <summary>
        /// Mevcut aktif tuş görselini gizler
        /// </summary>
        public void HideKey()
        {
            if (_currentActiveImage == null) return;

            StartFadeOut(_currentActiveImage);
            _currentActiveImage = null;

            LogDebug("Key hidden");
        }

        #endregion

        #region Public API - Prompts & Feedback

        /// <summary>
        /// Input prompt'unu gösterir
        /// </summary>
        public void ShowInputPrompt()
        {
            ShowPromptText(PROMPT_INPUT);
        }

        /// <summary>
        /// Tuş basma geri bildirimini gösterir
        /// </summary>
        public void ShowFeedback(bool isCorrect, int stepIndex)
        {
            string feedbackMessage = isCorrect
                ? string.Format(FEEDBACK_CORRECT_FORMAT, stepIndex + 1)
                : FEEDBACK_WRONG;

            Color feedbackColor = isCorrect ? Color.green : Color.red;

            ShowFeedbackText(feedbackMessage, feedbackColor);
            StartHideFeedbackDelayed();

            if (isCorrect)
            {
                UpdateProgressIndicator(stepIndex, Color.green);
            }

            LogDebug($"Feedback: {(isCorrect ? "Correct" : "Wrong")} - Step {stepIndex + 1}/{TOTAL_STEPS}");
        }

        /// <summary>
        /// Başarısızlık ekranını gösterir
        /// </summary>
        public void ShowFailure()
        {
            ShowFeedbackText(FEEDBACK_FAILURE, Color.red);
            HidePromptText();

            LogDebug("Failure shown");
        }

        #endregion

        #region Public API - Settings

        /// <summary>
        /// Fade sürelerini ayarlar
        /// </summary>
        public void SetFadeDurations(float fadeIn, float fadeOut)
        {
            fadeInDuration = Mathf.Max(0.01f, fadeIn);
            fadeOutDuration = Mathf.Max(0.01f, fadeOut);

            LogDebug($"Fade durations set - In: {fadeIn}s, Out: {fadeOut}s");
        }

        #endregion

        #region Arrow Management

        private Image GetImageForKey(KeyCode key, BoxInfo.BoxType boxType)
        {
            if (!_arrowMappings.TryGetValue(boxType, out var keyMappings))
            {
                return null;
            }

            keyMappings.TryGetValue(key, out Image image);
            return image;
        }

        private void HideAllArrows()
        {
            foreach (var boxTypeMapping in _arrowMappings.Values)
            {
                foreach (var image in boxTypeMapping.Values)
                {
                    SetImageActive(image, false);
                }
            }

            _currentActiveImage = null;
        }

        private static void SetImageActive(Image image, bool active)
        {
            if (image != null)
            {
                image.gameObject.SetActive(active);
            }
        }

        #endregion

        #region Fade Animations

        private void StartFadeIn(Image image)
        {
            StopFadeCoroutine();
            _fadeCoroutine = StartCoroutine(FadeInCoroutine(image));
        }

        private void StartFadeOut(Image image)
        {
            StopFadeCoroutine();
            _fadeCoroutine = StartCoroutine(FadeOutCoroutine(image));
        }

        private void StopFadeCoroutine()
        {
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
                _fadeCoroutine = null;
            }
        }

        private IEnumerator FadeInCoroutine(Image image)
        {
            if (image == null) yield break;

            SetImageAlpha(image, 0f);

            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(0f, 1f, elapsed / fadeInDuration);
                SetImageAlpha(image, alpha);
                yield return null;
            }

            SetImageAlpha(image, 1f);
            _fadeCoroutine = null;
        }

        private IEnumerator FadeOutCoroutine(Image image)
        {
            if (image == null) yield break;

            float startAlpha = image.color.a;

            float elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(startAlpha, 0f, elapsed / fadeOutDuration);
                SetImageAlpha(image, alpha);
                yield return null;
            }

            SetImageAlpha(image, 0f);
            image.gameObject.SetActive(false);
            _fadeCoroutine = null;
        }

        private static void SetImageAlpha(Image image, float alpha)
        {
            if (image == null) return;

            Color color = image.color;
            color.a = alpha;
            image.color = color;
        }

        #endregion

        #region UI Element Management

        private void SetPanelActive(bool active)
        {
            if (uiPanel != null)
            {
                uiPanel.SetActive(active);
            }
        }

        private void ShowPromptText(string text)
        {
            if (promptText == null) return;

            promptText.text = text;
            promptText.gameObject.SetActive(true);
        }

        private void HidePromptText()
        {
            if (promptText != null)
            {
                promptText.gameObject.SetActive(false);
            }
        }

        private void ShowFeedbackText(string text, Color color)
        {
            if (feedbackText == null) return;

            feedbackText.text = text;
            feedbackText.color = color;
            feedbackText.gameObject.SetActive(true);
        }

        private void HideFeedbackText()
        {
            if (feedbackText != null)
            {
                feedbackText.gameObject.SetActive(false);
            }
        }

        private void StartHideFeedbackDelayed()
        {
            StopFeedbackCoroutine();
            _feedbackCoroutine = StartCoroutine(HideFeedbackDelayedCoroutine());
        }

        private void StopFeedbackCoroutine()
        {
            if (_feedbackCoroutine != null)
            {
                StopCoroutine(_feedbackCoroutine);
                _feedbackCoroutine = null;
            }
        }

        private IEnumerator HideFeedbackDelayedCoroutine()
        {
            yield return new WaitForSeconds(feedbackHideDelay);

            HideFeedbackText();
            _feedbackCoroutine = null;
        }

        #endregion

        #region Progress Indicators

        private void UpdateProgressIndicator(int index, Color color)
        {
            if (!IsValidProgressIndex(index)) return;

            if (progressIndicators[index] != null)
            {
                progressIndicators[index].color = color;
            }
        }

        private void ResetProgressIndicators()
        {
            if (progressIndicators == null) return;

            int count = Mathf.Min(progressIndicators.Length, TOTAL_STEPS);

            for (int i = 0; i < count; i++)
            {
                if (progressIndicators[i] != null)
                {
                    progressIndicators[i].color = Color.white;
                }
            }
        }

        private bool IsValidProgressIndex(int index)
        {
            return progressIndicators != null &&
                   index >= 0 &&
                   index < progressIndicators.Length;
        }

        #endregion

        #region Coroutine Management

        private void StopAllActiveCoroutines()
        {
            StopFadeCoroutine();
            StopFeedbackCoroutine();
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

        #region Editor & Debug

#if UNITY_EDITOR
        [ContextMenu("Show UI (Red Box)")]
        private void DebugShowRedUI()
        {
            ShowUI(BoxInfo.BoxType.Red);
        }

        [ContextMenu("Show UI (Blue Box)")]
        private void DebugShowBlueUI()
        {
            ShowUI(BoxInfo.BoxType.Blue);
        }

        [ContextMenu("Show UI (Yellow Box)")]
        private void DebugShowYellowUI()
        {
            ShowUI(BoxInfo.BoxType.Yellow);
        }

        [ContextMenu("Hide UI")]
        private void DebugHideUI()
        {
            HideUI();
        }

        [ContextMenu("Test: Show Up Arrow")]
        private void DebugShowUpArrow()
        {
            ShowKey(KeyCode.UpArrow);
        }

        [ContextMenu("Test: Show Correct Feedback")]
        private void DebugShowCorrectFeedback()
        {
            ShowFeedback(true, 0);
        }

        [ContextMenu("Test: Show Wrong Feedback")]
        private void DebugShowWrongFeedback()
        {
            ShowFeedback(false, 0);
        }

        [ContextMenu("Test: Show Failure")]
        private void DebugShowFailure()
        {
            ShowFailure();
        }

        [ContextMenu("Debug: Print State")]
        private void DebugPrintState()
        {
            Debug.Log($"{LOG_PREFIX} === UI STATE ===");
            Debug.Log($"Is Visible: {IsVisible}");
            Debug.Log($"Current Box Type: {_currentBoxType}");
            Debug.Log($"Has Active Image: {_currentActiveImage != null}");
            Debug.Log($"Fade In Duration: {fadeInDuration}s");
            Debug.Log($"Fade Out Duration: {fadeOutDuration}s");
        }
#endif

        #endregion
    }
}