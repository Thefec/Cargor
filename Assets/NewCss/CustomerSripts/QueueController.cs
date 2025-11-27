using UnityEngine;

namespace NewCss
{
    /// <summary>
    /// Kuyruk pozisyonu görsel kontrolcüsü - kuyruk pozisyonunun görsel elementlerini yönetir.
    /// </summary>
    public class QueuePositionVisual : MonoBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[QueuePositionVisual]";

        #endregion

        #region Serialized Fields

        [Header("=== VISUAL ELEMENTS ===")]
        [SerializeField, Tooltip("Bu pozisyona ait görsel elementler (zemin iþareti, ýþýk, vb.)")]
        public GameObject[] visualElements;

        [Header("=== POSITION INFO ===")]
        [SerializeField, Tooltip("Bu pozisyonun index'i (0'dan baþlar)")]
        public int positionIndex;

        [Header("=== SETTINGS ===")]
        [SerializeField, Tooltip("Baþlangýçta görünür mü?")]
        private bool visibleOnStart;

        [SerializeField, Tooltip("Fade animasyonu kullan")]
        private bool useFadeAnimation;

        [SerializeField, Tooltip("Fade süresi")]
        private float fadeDuration = 0.3f;

        #endregion

        #region Private Fields

        private bool _isVisible;
        private Coroutine _fadeCoroutine;

        #endregion

        #region Public Properties

        /// <summary>
        /// Görünür mü?
        /// </summary>
        public bool IsVisible => _isVisible;

        /// <summary>
        /// Pozisyon index'i
        /// </summary>
        public int PositionIndex => positionIndex;

        /// <summary>
        /// Element sayýsý
        /// </summary>
        public int ElementCount => visualElements?.Length ?? 0;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            Initialize();
        }

        #endregion

        #region Initialization

        private void Initialize()
        {
            _isVisible = visibleOnStart;
            SetVisibilityImmediate(visibleOnStart);
        }

        #endregion

        #region Public API

        /// <summary>
        /// Tüm görsel elementlerin görünürlüðünü ayarlar
        /// </summary>
        public void SetVisibility(bool isVisible)
        {
            if (_isVisible == isVisible) return;

            _isVisible = isVisible;

            if (useFadeAnimation)
            {
                StartFadeAnimation(isVisible);
            }
            else
            {
                SetVisibilityImmediate(isVisible);
            }
        }

        /// <summary>
        /// Görünürlüðü anýnda ayarlar (animasyonsuz)
        /// </summary>
        public void SetVisibilityImmediate(bool isVisible)
        {
            _isVisible = isVisible;

            if (visualElements == null) return;

            foreach (var element in visualElements)
            {
                if (element != null)
                {
                    element.SetActive(isVisible);
                }
            }
        }

        /// <summary>
        /// Görünürlüðü toggle eder
        /// </summary>
        public void ToggleVisibility()
        {
            SetVisibility(!_isVisible);
        }

        /// <summary>
        /// Görsel elementleri gösterir
        /// </summary>
        public void Show()
        {
            SetVisibility(true);
        }

        /// <summary>
        /// Görsel elementleri gizler
        /// </summary>
        public void Hide()
        {
            SetVisibility(false);
        }

        #endregion

        #region Fade Animation

        private void StartFadeAnimation(bool fadeIn)
        {
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
            }

            _fadeCoroutine = StartCoroutine(FadeCoroutine(fadeIn));
        }

        private System.Collections.IEnumerator FadeCoroutine(bool fadeIn)
        {
            // Fade in için önce aktif et
            if (fadeIn)
            {
                SetVisibilityImmediate(true);
            }

            // Fade animasyonu için CanvasGroup veya material alpha kullanýlabilir
            // Basit implementasyon - sadece gecikme ile aç/kapat
            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                // Burada alpha animasyonu yapýlabilir
                yield return null;
            }

            // Fade out için sonra deaktif et
            if (!fadeIn)
            {
                SetVisibilityImmediate(false);
            }

            _fadeCoroutine = null;
        }

        #endregion

        #region Element Management

        /// <summary>
        /// Belirli bir elementi döndürür
        /// </summary>
        public GameObject GetElement(int index)
        {
            if (visualElements == null || index < 0 || index >= visualElements.Length)
            {
                return null;
            }

            return visualElements[index];
        }

        /// <summary>
        /// Belirli bir elementin görünürlüðünü ayarlar
        /// </summary>
        public void SetElementVisibility(int index, bool isVisible)
        {
            var element = GetElement(index);
            if (element != null)
            {
                element.SetActive(isVisible);
            }
        }

        #endregion

        #region Validation

        /// <summary>
        /// Null element sayýsýný döndürür
        /// </summary>
        public int GetNullElementCount()
        {
            if (visualElements == null) return 0;

            int count = 0;
            foreach (var element in visualElements)
            {
                if (element == null) count++;
            }

            return count;
        }

        #endregion

        #region Editor Debug

#if UNITY_EDITOR
        [ContextMenu("Show")]
        private void DebugShow()
        {
            Show();
        }

        [ContextMenu("Hide")]
        private void DebugHide()
        {
            Hide();
        }

        [ContextMenu("Toggle")]
        private void DebugToggle()
        {
            ToggleVisibility();
        }

        [ContextMenu("Validate Elements")]
        private void DebugValidateElements()
        {
            Debug.Log($"{LOG_PREFIX} === VISUAL ELEMENTS VALIDATION ===");
            Debug.Log($"Position Index: {positionIndex}");
            Debug.Log($"Is Visible: {_isVisible}");
            Debug.Log($"Element Count: {ElementCount}");
            Debug.Log($"Null Elements: {GetNullElementCount()}");

            if (visualElements != null)
            {
                for (int i = 0; i < visualElements.Length; i++)
                {
                    string status = visualElements[i] != null ? "OK" : "NULL";
                    string name = visualElements[i] != null ? visualElements[i].name : "N/A";
                    bool active = visualElements[i] != null && visualElements[i].activeSelf;
                    Debug.Log($"  [{i}] {name} - {status} - Active: {active}");
                }
            }
        }

        [ContextMenu("Auto-Find Visual Elements")]
        private void DebugAutoFindElements()
        {
            var renderers = GetComponentsInChildren<Renderer>();
            var elementList = new System.Collections.Generic.List<GameObject>();

            foreach (var renderer in renderers)
            {
                if (renderer.gameObject != gameObject)
                {
                    elementList.Add(renderer.gameObject);
                }
            }

            if (elementList.Count > 0)
            {
                visualElements = elementList.ToArray();
                Debug.Log($"{LOG_PREFIX} Found and assigned {visualElements.Length} visual elements");
            }
            else
            {
                Debug.LogWarning($"{LOG_PREFIX} No visual elements found in children");
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Draw position indicator
            Gizmos.color = _isVisible ? Color.green : Color.gray;
            Gizmos.DrawWireSphere(transform.position, 0.3f);

            // Draw label
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, $"Pos {positionIndex}");

            // Draw visual element connections
            if (visualElements != null)
            {
                Gizmos.color = Color.yellow * 0.5f;
                foreach (var element in visualElements)
                {
                    if (element != null)
                    {
                        Gizmos.DrawLine(transform.position, element.transform.position);
                    }
                }
            }
        }
#endif

        #endregion
    }
}