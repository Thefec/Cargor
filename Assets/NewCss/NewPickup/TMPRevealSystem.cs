using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace NewCss
{
    /// <summary>
    /// C tuþuna basýlý tutunca belirtilen TMP elementlerini gösterir. 
    /// Tuþ býrakýldýðýnda gizler.  Inspector'dan TMP listesi yönetilebilir.
    /// </summary>
    public class TMPRevealSystem : MonoBehaviour
    {
        #region Serialized Fields

        [Header("=== INPUT SETTINGS ===")]
        [SerializeField, Tooltip("TMP'leri göstermek için kullanýlacak tuþ")]
        private KeyCode revealKey = KeyCode.C;

        [Header("=== TMP REFERENCES ===")]
        [SerializeField, Tooltip("Gösterilecek/gizlenecek TMP listesi")]
        private List<TextMeshProUGUI> tmpElements = new List<TextMeshProUGUI>();

        [Header("=== TRANSITION SETTINGS ===")]
        [SerializeField, Tooltip("Smooth geçiþ kullan")]
        private bool useFadeTransition = true;

        [SerializeField, Range(1f, 20f), Tooltip("Fade geçiþ hýzý")]
        private float fadeSpeed = 10f;

        [Header("=== OPTIONAL SETTINGS ===")]
        [SerializeField, Tooltip("Oyun baþladýðýnda TMP'leri gizle")]
        private bool hideOnStart = true;

        [SerializeField, Tooltip("Canvas Group kullan (tüm child'larý etkiler)")]
        private List<CanvasGroup> canvasGroups = new List<CanvasGroup>();

        #endregion

        #region Private Fields

        private bool _isRevealing;
        private float _currentAlpha;
        private float _targetAlpha;

        // Her TMP için orijinal alpha deðerlerini sakla
        private Dictionary<TextMeshProUGUI, float> _originalAlphas = new Dictionary<TextMeshProUGUI, float>();
        private Dictionary<CanvasGroup, float> _originalCanvasAlphas = new Dictionary<CanvasGroup, float>();

        #endregion

        #region Public Properties

        /// <summary>
        /// TMP'ler þu anda görünür mü?
        /// </summary>
        public bool IsRevealing => _isRevealing;

        /// <summary>
        /// TMP element listesi
        /// </summary>
        public List<TextMeshProUGUI> TMPElements => tmpElements;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            Initialize();
        }

        private void Update()
        {
            HandleInput();
            UpdateVisibility();
        }

        #endregion

        #region Initialization

        private void Initialize()
        {
            // Orijinal alpha deðerlerini kaydet
            foreach (var tmp in tmpElements)
            {
                if (tmp != null)
                {
                    _originalAlphas[tmp] = tmp.alpha;
                }
            }

            foreach (var canvasGroup in canvasGroups)
            {
                if (canvasGroup != null)
                {
                    _originalCanvasAlphas[canvasGroup] = canvasGroup.alpha;
                }
            }

            // Baþlangýçta gizle
            if (hideOnStart)
            {
                _currentAlpha = 0f;
                _targetAlpha = 0f;
                SetAllAlpha(0f);
            }
            else
            {
                _currentAlpha = 1f;
                _targetAlpha = 1f;
            }
        }

        #endregion

        #region Input Handling

        private void HandleInput()
        {
            // C tuþuna basýlý tutulduðunda göster
            _isRevealing = Input.GetKey(revealKey);
            _targetAlpha = _isRevealing ? 1f : 0f;
        }

        #endregion

        #region Visibility Update

        private void UpdateVisibility()
        {
            if (useFadeTransition)
            {
                // Smooth geçiþ
                _currentAlpha = Mathf.Lerp(_currentAlpha, _targetAlpha, fadeSpeed * Time.deltaTime);

                // Çok küçük deðerleri sýfýrla
                if (_currentAlpha < 0.01f && _targetAlpha == 0f)
                {
                    _currentAlpha = 0f;
                }
                // Çok büyük deðerleri tamamla
                else if (_currentAlpha > 0.99f && _targetAlpha == 1f)
                {
                    _currentAlpha = 1f;
                }
            }
            else
            {
                // Anýnda geçiþ
                _currentAlpha = _targetAlpha;
            }

            SetAllAlpha(_currentAlpha);
        }

        private void SetAllAlpha(float alpha)
        {
            // TMP elementlerini güncelle
            foreach (var tmp in tmpElements)
            {
                if (tmp != null)
                {
                    // Orijinal alpha ile çarp
                    float originalAlpha = _originalAlphas.ContainsKey(tmp) ? _originalAlphas[tmp] : 1f;
                    tmp.alpha = alpha * originalAlpha;
                }
            }

            // Canvas Group'larý güncelle
            foreach (var canvasGroup in canvasGroups)
            {
                if (canvasGroup != null)
                {
                    float originalAlpha = _originalCanvasAlphas.ContainsKey(canvasGroup) ? _originalCanvasAlphas[canvasGroup] : 1f;
                    canvasGroup.alpha = alpha * originalAlpha;

                    // Alpha 0 iken etkileþimi kapat
                    canvasGroup.interactable = alpha > 0.01f;
                    canvasGroup.blocksRaycasts = alpha > 0.01f;
                }
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Yeni TMP elementi ekle
        /// </summary>
        public void AddTMPElement(TextMeshProUGUI tmp)
        {
            if (tmp != null && !tmpElements.Contains(tmp))
            {
                tmpElements.Add(tmp);
                _originalAlphas[tmp] = tmp.alpha;

                // Mevcut duruma göre alpha ayarla
                tmp.alpha = _currentAlpha * _originalAlphas[tmp];

                Debug.Log($"[TMPRevealSystem] TMP added: {tmp.name}");
            }
        }

        /// <summary>
        /// TMP elementi kaldýr
        /// </summary>
        public void RemoveTMPElement(TextMeshProUGUI tmp)
        {
            if (tmp != null && tmpElements.Contains(tmp))
            {
                // Orijinal alpha'ya döndür
                if (_originalAlphas.ContainsKey(tmp))
                {
                    tmp.alpha = _originalAlphas[tmp];
                    _originalAlphas.Remove(tmp);
                }

                tmpElements.Remove(tmp);
                Debug.Log($"[TMPRevealSystem] TMP removed: {tmp.name}");
            }
        }

        /// <summary>
        /// Canvas Group ekle
        /// </summary>
        public void AddCanvasGroup(CanvasGroup group)
        {
            if (group != null && !canvasGroups.Contains(group))
            {
                canvasGroups.Add(group);
                _originalCanvasAlphas[group] = group.alpha;
                group.alpha = _currentAlpha * _originalCanvasAlphas[group];

                Debug.Log($"[TMPRevealSystem] CanvasGroup added: {group.name}");
            }
        }

        /// <summary>
        /// Canvas Group kaldýr
        /// </summary>
        public void RemoveCanvasGroup(CanvasGroup group)
        {
            if (group != null && canvasGroups.Contains(group))
            {
                if (_originalCanvasAlphas.ContainsKey(group))
                {
                    group.alpha = _originalCanvasAlphas[group];
                    _originalCanvasAlphas.Remove(group);
                }

                canvasGroups.Remove(group);
                Debug.Log($"[TMPRevealSystem] CanvasGroup removed: {group.name}");
            }
        }

        /// <summary>
        /// Tüm TMP'leri temizle
        /// </summary>
        public void ClearAllElements()
        {
            // Orijinal alpha'lara döndür
            foreach (var tmp in tmpElements)
            {
                if (tmp != null && _originalAlphas.ContainsKey(tmp))
                {
                    tmp.alpha = _originalAlphas[tmp];
                }
            }

            foreach (var group in canvasGroups)
            {
                if (group != null && _originalCanvasAlphas.ContainsKey(group))
                {
                    group.alpha = _originalCanvasAlphas[group];
                }
            }

            tmpElements.Clear();
            canvasGroups.Clear();
            _originalAlphas.Clear();
            _originalCanvasAlphas.Clear();

            Debug.Log("[TMPRevealSystem] All elements cleared");
        }

        /// <summary>
        /// Manuel olarak göster/gizle
        /// </summary>
        public void SetRevealState(bool reveal)
        {
            _isRevealing = reveal;
            _targetAlpha = reveal ? 1f : 0f;
        }

        /// <summary>
        /// Reveal tuþunu deðiþtir
        /// </summary>
        public void SetRevealKey(KeyCode newKey)
        {
            revealKey = newKey;
            Debug.Log($"[TMPRevealSystem] Reveal key changed to: {newKey}");
        }

        #endregion

        #region Editor

#if UNITY_EDITOR
        [ContextMenu("Debug: Print Element Count")]
        private void DebugPrintElementCount()
        {
            Debug.Log($"[TMPRevealSystem] TMP Elements: {tmpElements.Count}, Canvas Groups: {canvasGroups.Count}");
        }

        [ContextMenu("Debug: Force Show")]
        private void DebugForceShow()
        {
            _currentAlpha = 1f;
            _targetAlpha = 1f;
            SetAllAlpha(1f);
        }

        [ContextMenu("Debug: Force Hide")]
        private void DebugForceHide()
        {
            _currentAlpha = 0f;
            _targetAlpha = 0f;
            SetAllAlpha(0f);
        }

        private void OnValidate()
        {
            // Editor'da null elemanlarý temizle
            tmpElements.RemoveAll(item => item == null);
            canvasGroups.RemoveAll(item => item == null);
        }
#endif

        #endregion
    }
}