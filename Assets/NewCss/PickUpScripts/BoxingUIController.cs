using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

namespace NewCss
{
    public class BoxingUIController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject uiPanel;
        [SerializeField] private TextMeshProUGUI promptText;
        [SerializeField] private TextMeshProUGUI feedbackText;
        [SerializeField] private Image[] progressIndicators;

        [Header("Arrow Images - RED")]
        [SerializeField] private Image redUpImage;
        [SerializeField] private Image redDownImage;
        [SerializeField] private Image redLeftImage;
        [SerializeField] private Image redRightImage;

        [Header("Arrow Images - YELLOW")]
        [SerializeField] private Image yellowUpImage;
        [SerializeField] private Image yellowDownImage;
        [SerializeField] private Image yellowLeftImage;
        [SerializeField] private Image yellowRightImage;

        [Header("Arrow Images - BLUE")]
        [SerializeField] private Image blueUpImage;
        [SerializeField] private Image blueDownImage;
        [SerializeField] private Image blueLeftImage;
        [SerializeField] private Image blueRightImage;

        private BoxInfo.BoxType currentBoxType;
        private Image currentActiveImage;

        // ✅ YENİ: Dinamik fade süreleri
        private float fadeInDuration = 0.15f;
        private float fadeOutDuration = 0.15f;

        void Awake()
        {
            if (uiPanel != null)
            {
                uiPanel.SetActive(false);
            }

            HideAllArrows();

            if (progressIndicators != null && progressIndicators.Length != 3)
            {
                Debug.LogWarning($"Progress indicators count is {progressIndicators.Length}, should be 3!");
            }
        }

        /// <summary>
        /// ✅ YENİ: Fade hızlarını dışarıdan ayarla
        /// </summary>
        public void SetFadeDurations(float fadeIn, float fadeOut)
        {
            fadeInDuration = fadeIn;
            fadeOutDuration = fadeOut;
            Debug.Log($"Fade durations set - In: {fadeIn}s, Out: {fadeOut}s");
        }

        public void ShowUI(BoxInfo.BoxType boxType)
        {
            currentBoxType = boxType;

            if (uiPanel != null)
            {
                uiPanel.SetActive(true);
            }

            if (promptText != null)
            {
                promptText.text = "Yön tuşlarını takip edin! (3 FARKLI tuş)";
                promptText.gameObject.SetActive(true);
            }

            if (feedbackText != null)
            {
                feedbackText.gameObject.SetActive(false);
            }

            HideAllArrows();
            ResetProgressIndicators();
        }

        public void HideUI()
        {
            if (uiPanel != null)
            {
                uiPanel.SetActive(false);
            }

            HideAllArrows();
        }

        public void ShowKey(KeyCode key)
        {
            Image targetImage = GetImageForKey(key, currentBoxType);
            if (targetImage != null)
            {
                targetImage.gameObject.SetActive(true);
                currentActiveImage = targetImage;

                StartCoroutine(FadeInImage(targetImage));
            }
        }

        public void HideKey()
        {
            if (currentActiveImage != null)
            {
                StartCoroutine(FadeOutImage(currentActiveImage));
                currentActiveImage = null;
            }
        }

        private IEnumerator FadeInImage(Image image)
        {
            if (image == null) yield break;

            Color color = image.color;
            color.a = 0f;
            image.color = color;

            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                color.a = Mathf.Lerp(0f, 1f, elapsed / fadeInDuration);
                image.color = color;
                yield return null;
            }

            color.a = 1f;
            image.color = color;
        }

        private IEnumerator FadeOutImage(Image image)
        {
            if (image == null) yield break;

            Color color = image.color;
            float startAlpha = color.a;

            float elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                color.a = Mathf.Lerp(startAlpha, 0f, elapsed / fadeOutDuration);
                image.color = color;
                yield return null;
            }

            color.a = 0f;
            image.color = color;
            image.gameObject.SetActive(false);
        }

        public void ShowInputPrompt()
        {
            if (promptText != null)
            {
                promptText.text = "Şimdi sıra sizde! (3 FARKLI tuş)";
            }
        }

        public void ShowFeedback(bool isCorrect, int stepIndex)
        {
            if (feedbackText != null)
            {
                feedbackText.text = isCorrect ? $"✓ DOĞRU! ({stepIndex + 1}/3)" : "✗ YANLIŞ!";
                feedbackText.color = isCorrect ? Color.green : Color.red;
                feedbackText.gameObject.SetActive(true);

                StartCoroutine(HideFeedbackDelayed(0.3f));
            }

            if (isCorrect && progressIndicators != null && stepIndex < progressIndicators.Length)
            {
                progressIndicators[stepIndex].color = Color.green;
            }
        }

        public void ShowFailure()
        {
            if (feedbackText != null)
            {
                feedbackText.text = "❌ BAŞARISIZ!\nKutu parçalandı!";
                feedbackText.color = Color.red;
                feedbackText.gameObject.SetActive(true);
            }

            if (promptText != null)
            {
                promptText.gameObject.SetActive(false);
            }
        }

        private IEnumerator HideFeedbackDelayed(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (feedbackText != null)
            {
                feedbackText.gameObject.SetActive(false);
            }
        }

        private Image GetImageForKey(KeyCode key, BoxInfo.BoxType boxType)
        {
            return boxType switch
            {
                BoxInfo.BoxType.Red => key switch
                {
                    KeyCode.UpArrow => redUpImage,
                    KeyCode.DownArrow => redDownImage,
                    KeyCode.LeftArrow => redLeftImage,
                    KeyCode.RightArrow => redRightImage,
                    _ => null
                },
                BoxInfo.BoxType.Yellow => key switch
                {
                    KeyCode.UpArrow => yellowUpImage,
                    KeyCode.DownArrow => yellowDownImage,
                    KeyCode.LeftArrow => yellowLeftImage,
                    KeyCode.RightArrow => yellowRightImage,
                    _ => null
                },
                BoxInfo.BoxType.Blue => key switch
                {
                    KeyCode.UpArrow => blueUpImage,
                    KeyCode.DownArrow => blueDownImage,
                    KeyCode.LeftArrow => blueLeftImage,
                    KeyCode.RightArrow => blueRightImage,
                    _ => null
                },
                _ => null
            };
        }

        private void HideAllArrows()
        {
            if (redUpImage != null) redUpImage.gameObject.SetActive(false);
            if (redDownImage != null) redDownImage.gameObject.SetActive(false);
            if (redLeftImage != null) redLeftImage.gameObject.SetActive(false);
            if (redRightImage != null) redRightImage.gameObject.SetActive(false);

            if (yellowUpImage != null) yellowUpImage.gameObject.SetActive(false);
            if (yellowDownImage != null) yellowDownImage.gameObject.SetActive(false);
            if (yellowLeftImage != null) yellowLeftImage.gameObject.SetActive(false);
            if (yellowRightImage != null) yellowRightImage.gameObject.SetActive(false);

            if (blueUpImage != null) blueUpImage.gameObject.SetActive(false);
            if (blueDownImage != null) blueDownImage.gameObject.SetActive(false);
            if (blueLeftImage != null) blueLeftImage.gameObject.SetActive(false);
            if (blueRightImage != null) blueRightImage.gameObject.SetActive(false);

            currentActiveImage = null;
        }

        private void ResetProgressIndicators()
        {
            if (progressIndicators == null) return;

            for (int i = 0; i < Mathf.Min(progressIndicators.Length, 3); i++)
            {
                if (progressIndicators[i] != null)
                {
                    progressIndicators[i].color = Color.white;
                }
            }
        }
    }
}