using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

namespace NewCss.UIScripts
{
    /// <summary>
    /// Sayfalý UI panel kontrolcüsü. 
    /// Ýleri/Geri butonlarýyla sayfa geçiþi, animasyonlu açýlýþ/kapanýþ saðlar.
    /// Trigger zone ile entegre çalýþýr. 
    /// </summary>
    public class PagedUIPanel : MonoBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[PagedUIPanel]";
        private const float DEFAULT_OPEN_ANIMATION_DURATION = 0.3f;
        private const float DEFAULT_CLOSE_ANIMATION_DURATION = 0.2f;

        #endregion

        #region Serialized Fields

        [Header("=== PANEL SETTINGS ===")]
        [SerializeField, Tooltip("Ana panel GameObject")]
        private GameObject panelObject;

        [SerializeField, Tooltip("Panel animator (opsiyonel)")]
        private Animator panelAnimator;

        [Header("=== SAYFALAR ===")]
        [SerializeField, Tooltip("Sayfa GameObject'leri (her biri TMP Text içerir)")]
        private List<GameObject> pages = new List<GameObject>();

        [Header("=== BUTONLAR ===")]
        [SerializeField, Tooltip("Ýleri butonu")]
        private Button nextButton;

        [SerializeField, Tooltip("Geri butonu")]
        private Button previousButton;

        [SerializeField, Tooltip("Kapat butonu (opsiyonel)")]
        private Button closeButton;

        [Header("=== SAYFA GÖSTERGESÝ ===")]
        [SerializeField, Tooltip("Sayfa göstergesi text'i (opsiyonel)")]
        private TextMeshProUGUI pageIndicatorText;

        [SerializeField, Tooltip("Sayfa göstergesi formatý")]
        private string pageIndicatorFormat = "Sayfa {0} / {1}";

        [Header("=== PLAYER SETTINGS ===")]
        [SerializeField, Tooltip("Panel açýkken oyuncu hareketini kilitle")]
        private bool lockPlayerMovement = true;

        [Header("=== DEBUG ===")]
        [SerializeField, Tooltip("Debug loglarýný göster")]
        private bool showDebugLogs = true;

        #endregion

        #region Private Fields

        private int _currentPageIndex;
        private bool _isPanelOpen;
        private bool _isAnimating;
        private PlayerMovement _localPlayerMovement;

        #endregion

        #region Public Properties

        /// <summary>
        /// Panel açýk mý?
        /// </summary>
        public bool IsPanelOpen => _isPanelOpen;

        /// <summary>
        /// Animasyon devam ediyor mu?
        /// </summary>
        public bool IsAnimating => _isAnimating;

        /// <summary>
        /// Mevcut sayfa indexi
        /// </summary>
        public int CurrentPageIndex => _currentPageIndex;

        /// <summary>
        /// Toplam sayfa sayýsý
        /// </summary>
        public int TotalPages => pages.Count;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            Initialize();
        }

        private void OnDestroy()
        {
            CleanupButtonListeners();
        }

        private void Update()
        {
            HandleInput();
        }

        #endregion

        #region Initialization

        private void Initialize()
        {
            SetupButtons();
            SetupPanel();
            ValidatePages();
        }

        private void SetupButtons()
        {
            if (nextButton != null)
            {
                nextButton.onClick.AddListener(NextPage);
            }

            if (previousButton != null)
            {
                previousButton.onClick.AddListener(PreviousPage);
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(ClosePanel);
            }
        }

        private void CleanupButtonListeners()
        {
            if (nextButton != null)
            {
                nextButton.onClick.RemoveListener(NextPage);
            }

            if (previousButton != null)
            {
                previousButton.onClick.RemoveListener(PreviousPage);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(ClosePanel);
            }
        }

        private void SetupPanel()
        {
            if (panelObject != null)
            {
                panelObject.SetActive(false);
            }
        }

        private void ValidatePages()
        {
            if (pages == null || pages.Count == 0)
            {
                LogWarning("Sayfa listesi boþ!");
                return;
            }

            // Tüm sayfalarý baþlangýçta kapat
            for (int i = 0; i < pages.Count; i++)
            {
                if (pages[i] != null)
                {
                    pages[i].SetActive(false);
                }
            }

            LogDebug($"{pages.Count} sayfa bulundu");
        }

        #endregion

        #region Panel Control

        /// <summary>
        /// Paneli açar
        /// </summary>
        public void OpenPanel()
        {
            if (_isPanelOpen || _isAnimating)
            {
                LogDebug("Panel zaten açýk veya animasyon devam ediyor");
                return;
            }

            _isPanelOpen = true;
            _isAnimating = true;
            _currentPageIndex = 0;

            // Oyuncu hareketini kilitle
            if (lockPlayerMovement)
            {
                LockPlayerMovement(true);
            }

            // Paneli aktif et
            if (panelObject != null)
            {
                panelObject.SetActive(true);
            }

            // Sayfa görüntüsünü güncelle
            UpdatePageDisplay();

            // Animasyon oynat
            if (panelAnimator != null)
            {
                StartCoroutine(PlayOpenAnimationCoroutine());
            }
            else
            {
                _isAnimating = false;
                LogDebug("Panel animasyonsuz açýldý");
            }
        }

        /// <summary>
        /// Paneli kapatýr
        /// </summary>
        public void ClosePanel()
        {
            if (!_isPanelOpen || _isAnimating)
            {
                LogDebug("Panel zaten kapalý veya animasyon devam ediyor");
                return;
            }

            _isPanelOpen = false;
            _isAnimating = true;

            // Animasyon oynat veya direkt kapat
            if (panelAnimator != null)
            {
                StartCoroutine(PlayCloseAnimationCoroutine());
            }
            else
            {
                FinishClosing();
            }
        }

        private void FinishClosing()
        {
            _isAnimating = false;

            // Paneli deaktif et
            if (panelObject != null)
            {
                panelObject.SetActive(false);
            }

            // Oyuncu hareketini serbest býrak
            if (lockPlayerMovement)
            {
                LockPlayerMovement(false);
            }

            LogDebug("Panel kapatýldý");
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

        #endregion

        #region Page Navigation

        /// <summary>
        /// Sonraki sayfaya geçer
        /// </summary>
        public void NextPage()
        {
            if (_currentPageIndex < pages.Count - 1)
            {
                _currentPageIndex++;
                UpdatePageDisplay();
                LogDebug($"Sonraki sayfaya geçildi: {_currentPageIndex + 1}/{pages.Count}");
            }
        }

        /// <summary>
        /// Önceki sayfaya geçer
        /// </summary>
        public void PreviousPage()
        {
            if (_currentPageIndex > 0)
            {
                _currentPageIndex--;
                UpdatePageDisplay();
                LogDebug($"Önceki sayfaya geçildi: {_currentPageIndex + 1}/{pages.Count}");
            }
        }

        /// <summary>
        /// Belirli bir sayfaya gider
        /// </summary>
        public void GoToPage(int pageIndex)
        {
            if (pageIndex < 0 || pageIndex >= pages.Count)
            {
                LogWarning($"Geçersiz sayfa indexi: {pageIndex}");
                return;
            }

            _currentPageIndex = pageIndex;
            UpdatePageDisplay();
        }

        private void UpdatePageDisplay()
        {
            // Tüm sayfalarý gizle, sadece aktif olaný göster
            for (int i = 0; i < pages.Count; i++)
            {
                if (pages[i] != null)
                {
                    pages[i].SetActive(i == _currentPageIndex);
                }
            }

            // Buton durumlarýný güncelle
            UpdateButtonStates();

            // Sayfa göstergesini güncelle
            UpdatePageIndicator();
        }

        private void UpdateButtonStates()
        {
            if (previousButton != null)
            {
                previousButton.interactable = _currentPageIndex > 0;
            }

            if (nextButton != null)
            {
                nextButton.interactable = _currentPageIndex < pages.Count - 1;
            }
        }

        private void UpdatePageIndicator()
        {
            if (pageIndicatorText != null)
            {
                pageIndicatorText.text = string.Format(pageIndicatorFormat, _currentPageIndex + 1, pages.Count);
            }
        }

        #endregion

        #region Input Handling

        private void HandleInput()
        {
            if (!_isPanelOpen || _isAnimating) return;

            // ESC ile kapat
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                ClosePanel();
            }

            // Ok tuþlarý ile sayfa geçiþi
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            {
                NextPage();
            }
            else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            {
                PreviousPage();
            }
        }

        #endregion

        #region Player Movement Control

        /// <summary>
        /// Oyuncu referansýný ayarlar
        /// </summary>
        public void SetLocalPlayer(PlayerMovement player)
        {
            _localPlayerMovement = player;
        }

        private void LockPlayerMovement(bool locked)
        {
            if (_localPlayerMovement == null) return;

            _localPlayerMovement.enabled = !locked;
            LogDebug($"Oyuncu hareketi: {(locked ? "kilitlendi" : "serbest")}");
        }

        #endregion

        #region Animation Coroutines

        private IEnumerator PlayOpenAnimationCoroutine()
        {
            panelAnimator.SetTrigger("Open");
            yield return new WaitForSeconds(DEFAULT_OPEN_ANIMATION_DURATION);
            _isAnimating = false;
            LogDebug("Panel açýlýþ animasyonu tamamlandý");
        }

        private IEnumerator PlayCloseAnimationCoroutine()
        {
            panelAnimator.SetTrigger("Close");
            yield return new WaitForSeconds(DEFAULT_CLOSE_ANIMATION_DURATION);
            FinishClosing();
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

        [ContextMenu("Next Page")]
        private void DebugNextPage()
        {
            NextPage();
        }

        [ContextMenu("Previous Page")]
        private void DebugPreviousPage()
        {
            PreviousPage();
        }

        [ContextMenu("Debug: Print State")]
        private void DebugPrintState()
        {
            Debug.Log($"{LOG_PREFIX} === PANEL STATE ===");
            Debug.Log($"Is Panel Open: {_isPanelOpen}");
            Debug.Log($"Is Animating: {_isAnimating}");
            Debug.Log($"Current Page: {_currentPageIndex + 1}/{pages.Count}");
            Debug.Log($"Has Local Player: {_localPlayerMovement != null}");
        }
#endif

        #endregion
    }
}