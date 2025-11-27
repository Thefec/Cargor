using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace NewCss.UIScripts
{
    /// <summary>
    /// Ofis terminali - oyuncuların upgrade paneline erişmesini sağlar. 
    /// Trigger bazlı etkileşim, animasyonlu panel açma/kapama ve network desteği sunar.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class OfficeTerminal : MonoBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[OfficeTerminal]";
        private const string CHARACTER_TAG = "Character";
        private const int PANEL_OPEN_HOUR = 10;
        private const float DEFAULT_OPEN_ANIMATION_DURATION = 0.5f;
        private const float DEFAULT_CLOSE_ANIMATION_DURATION = 0.3f;

        // Animator trigger names
        private const string TRIGGER_OPEN = "Open";
        private const string TRIGGER_CLOSE = "Close";

        #endregion

        #region Serialized Fields

        [Header("=== PANEL SETTINGS ===")]
        [SerializeField, Tooltip("Upgrade panel GameObject")]
        public GameObject upgradePanel;

        [SerializeField, Tooltip("Panel animator component")]
        public Animator panelAnimator;

        [Header("=== BUTTON SETTINGS ===")]
        [SerializeField, Tooltip("Close button")]
        public Button closeButton;

        [Header("=== ANIMATION SETTINGS ===")]
        [SerializeField, Tooltip("Close animation clip name")]
        public string closeAnimationClipName = "ShopExit";

        [SerializeField, Tooltip("Open animation clip name")]
        public string openAnimationClipName = "ShopOpening";

        [Header("=== INTERACTION SETTINGS ===")]
        [SerializeField, Tooltip("Minimum hour to open panel")]
        private int minimumHourToOpen = PANEL_OPEN_HOUR;

        [SerializeField, Tooltip("Show debug logs")]
        private bool showDebugLogs = true;

        #endregion

        #region Private Fields

        private bool _isPanelOpen;
        private bool _isAnimating;
        private PlayerMovement _localPlayerMovement;
        private Collider _triggerCollider;

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

        /// <summary>
        /// Etkileşim mümkün mü?
        /// </summary>
        public bool CanInteract => !_isPanelOpen && !_isAnimating;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            Initialize();
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        #endregion

        #region Initialization

        private void Initialize()
        {
            SetupCollider();
            SetupPanel();
            SetupAnimator();
            SetupCloseButton();
            CacheAnimationDurations();
        }

        private void SetupCollider()
        {
            _triggerCollider = GetComponent<Collider>();
            _triggerCollider.isTrigger = true;
        }

        private void SetupPanel()
        {
            if (upgradePanel != null)
            {
                upgradePanel.SetActive(false);
            }
            else
            {
                LogWarning("upgradePanel is not assigned!");
            }
        }

        private void SetupAnimator()
        {
            if (panelAnimator != null) return;

            if (upgradePanel != null)
            {
                panelAnimator = upgradePanel.GetComponent<Animator>();

                if (panelAnimator == null)
                {
                    LogWarning("panelAnimator not assigned and upgradePanel has no Animator!");
                }
            }
        }

        private void SetupCloseButton()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(HandleCloseButtonClicked);
            }
            else
            {
                LogWarning("Close button not assigned!");
            }
        }

        private void CacheAnimationDurations()
        {
            if (panelAnimator == null) return;

            var controller = panelAnimator.runtimeAnimatorController;
            if (controller == null) return;

            foreach (var clip in controller.animationClips)
            {
                if (IsOpenAnimationClip(clip.name))
                {
                    _cachedOpenDuration = clip.length;
                }
                else if (IsCloseAnimationClip(clip.name))
                {
                    _cachedCloseDuration = clip.length;
                }
            }
        }

        private bool IsOpenAnimationClip(string clipName)
        {
            return clipName.Contains("Open") ||
                   clipName.Contains("Opening") ||
                   clipName == openAnimationClipName;
        }

        private bool IsCloseAnimationClip(string clipName)
        {
            return clipName.Contains("Exit") ||
                   clipName.Contains("Close") ||
                   clipName == closeAnimationClipName;
        }

        #endregion

        #region Trigger Handlers

        private void OnTriggerEnter(Collider other)
        {
            LogDebug($"TriggerEnter: {other.name}, tag={other.tag}");

            if (!ValidateCharacterTrigger(other, out NetworkObject networkObject, out PlayerMovement playerMovement))
            {
                return;
            }

            LogDebug("Local player entered range");

            // Store reference
            _localPlayerMovement = playerMovement;

            // Check if can open
            TryOpenPanel();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag(CHARACTER_TAG)) return;

            var networkObject = other.GetComponent<NetworkObject>();
            if (networkObject == null || !networkObject.IsOwner)
            {
                return;
            }

            LogDebug("Local player exited range");

            // Panel stays open - only closes via button
        }

        private bool ValidateCharacterTrigger(Collider other, out NetworkObject networkObject, out PlayerMovement playerMovement)
        {
            networkObject = null;
            playerMovement = null;

            // Tag check
            if (!other.CompareTag(CHARACTER_TAG))
            {
                LogDebug("Tag mismatch");
                return false;
            }

            // Network object check
            networkObject = other.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                LogWarning("Character has no NetworkObject component!");
                return false;
            }

            // Owner check
            if (!networkObject.IsOwner)
            {
                LogDebug("Not the local player, ignoring");
                return false;
            }

            // PlayerMovement check
            playerMovement = other.GetComponent<PlayerMovement>();
            if (playerMovement == null)
            {
                LogWarning("PlayerMovement component not found on character!");
            }

            return true;
        }

        #endregion

        #region Panel Open/Close

        private void TryOpenPanel()
        {
            // Validation
            if (!CanOpenPanel())
            {
                return;
            }

            // Time check
            if (!IsCorrectTimeToOpen())
            {
                LogDebug($"Hour is less than {minimumHourToOpen}, panel not opening");
                return;
            }

            LogDebug($"Hour is {minimumHourToOpen} or above, opening panel");
            OpenPanel();
        }

        private bool CanOpenPanel()
        {
            if (upgradePanel == null)
            {
                LogError("upgradePanel is not assigned!");
                return false;
            }

            if (_isAnimating)
            {
                LogDebug("Animation in progress, ignoring");
                return false;
            }

            if (_isPanelOpen)
            {
                LogDebug("Panel already open, ignoring");
                return false;
            }

            return true;
        }

        private bool IsCorrectTimeToOpen()
        {
            if (DayCycleManager.Instance == null)
            {
                LogWarning("DayCycleManager not found, allowing open");
                return true;
            }

            int currentHour = DayCycleManager.Instance.CurrentHour;
            LogDebug($"CurrentHour = {currentHour}");

            return currentHour >= minimumHourToOpen;
        }

        /// <summary>
        /// Panel'i açar
        /// </summary>
        public void OpenPanel()
        {
            if (_isPanelOpen || _isAnimating)
            {
                LogDebug("Panel already open or animating, ignoring open request");
                return;
            }

            _isPanelOpen = true;
            _isAnimating = true;

            // Lock player movement
            LockPlayerMovement(true);

            // Activate panel
            upgradePanel.SetActive(true);

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
        /// Panel'i kapatır
        /// </summary>
        public void ClosePanel()
        {
            if (!_isPanelOpen || _isAnimating)
            {
                LogDebug("Panel not open or animating, ignoring close request");
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
                upgradePanel.SetActive(false);
                _isAnimating = false;
                LogDebug("Panel closed without animation");
            }

            // Clear reference
            _localPlayerMovement = null;
        }

        #endregion

        #region Player Movement Control

        private void LockPlayerMovement(bool locked)
        {
            if (_localPlayerMovement == null)
            {
                LogDebug("No local player movement reference, skipping lock");
                return;
            }

            _localPlayerMovement.LockMovement(locked);
            LogDebug($"Local player movement {(locked ? "locked" : "unlocked")}");
        }

        #endregion

        #region Animation Coroutines

        private IEnumerator PlayOpenAnimationCoroutine()
        {
            // Wait one frame for panel to properly activate
            yield return null;

            // Reset and set triggers
            ResetAnimatorTriggers();
            panelAnimator.SetTrigger(TRIGGER_OPEN);

            // Wait for animation
            float duration = GetOpenAnimationDuration();
            LogDebug($"Playing open animation ({duration}s)");

            yield return new WaitForSeconds(duration);

            _isAnimating = false;
            LogDebug("Open animation completed");
        }

        private IEnumerator PlayCloseAnimationCoroutine()
        {
            // Reset and set triggers
            ResetAnimatorTriggers();
            panelAnimator.SetTrigger(TRIGGER_CLOSE);

            // Wait for animation
            float duration = GetCloseAnimationDuration();
            LogDebug($"Waiting {duration}s for close animation...");

            yield return new WaitForSeconds(duration);

            // Deactivate panel after animation
            if (upgradePanel != null)
            {
                upgradePanel.SetActive(false);
                LogDebug("Panel disabled after close animation");
            }

            _isAnimating = false;
        }

        private void ResetAnimatorTriggers()
        {
            if (panelAnimator == null) return;

            panelAnimator.ResetTrigger(TRIGGER_OPEN);
            panelAnimator.ResetTrigger(TRIGGER_CLOSE);
        }

        private float GetOpenAnimationDuration()
        {
            if (_cachedOpenDuration > 0)
            {
                return _cachedOpenDuration;
            }

            return DEFAULT_OPEN_ANIMATION_DURATION;
        }

        private float GetCloseAnimationDuration()
        {
            if (_cachedCloseDuration > 0)
            {
                return _cachedCloseDuration;
            }

            return DEFAULT_CLOSE_ANIMATION_DURATION;
        }

        #endregion

        #region Event Handlers

        private void HandleCloseButtonClicked()
        {
            LogDebug("Close button clicked");
            ClosePanel();
        }

        #endregion

        #region Animation Event Callbacks

        /// <summary>
        /// Animation Event callback - açılma animasyonu tamamlandığında çağrılır
        /// </summary>
        public void OnOpenAnimationComplete()
        {
            _isAnimating = false;
            LogDebug("Open animation completed via Animation Event");
        }

        /// <summary>
        /// Animation Event callback - kapanma animasyonu tamamlandığında çağrılır
        /// </summary>
        public void OnCloseAnimationComplete()
        {
            if (upgradePanel != null)
            {
                upgradePanel.SetActive(false);
            }

            _isAnimating = false;
            LogDebug("Close animation completed via Animation Event");
        }

        #endregion

        #region Cleanup

        private void Cleanup()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(HandleCloseButtonClicked);
            }

            StopAllCoroutines();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Panel'i zorla kapatır (animasyon beklemeden)
        /// </summary>
        public void ForceClosePanel()
        {
            StopAllCoroutines();

            _isPanelOpen = false;
            _isAnimating = false;

            LockPlayerMovement(false);

            if (upgradePanel != null)
            {
                upgradePanel.SetActive(false);
            }

            _localPlayerMovement = null;

            LogDebug("Panel force closed");
        }

        /// <summary>
        /// Panel'i toggle eder
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
            Debug.LogWarning($"{LOG_PREFIX} {message}", this);
        }

        private void LogError(string message)
        {
            Debug.LogError($"{LOG_PREFIX} {message}", this);
        }

        #endregion

        #region Editor & Debug

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

        [ContextMenu("Force Close Panel")]
        private void DebugForceClosePanel()
        {
            ForceClosePanel();
        }

        [ContextMenu("Toggle Panel")]
        private void DebugTogglePanel()
        {
            TogglePanel();
        }

        [ContextMenu("Debug: Print State")]
        private void DebugPrintState()
        {
            Debug.Log($"{LOG_PREFIX} === TERMINAL STATE ===");
            Debug.Log($"Is Panel Open: {_isPanelOpen}");
            Debug.Log($"Is Animating: {_isAnimating}");
            Debug.Log($"Can Interact: {CanInteract}");
            Debug.Log($"Has Local Player: {_localPlayerMovement != null}");
            Debug.Log($"Cached Open Duration: {_cachedOpenDuration}");
            Debug.Log($"Cached Close Duration: {_cachedCloseDuration}");
        }

        private void OnDrawGizmosSelected()
        {
            DrawTriggerGizmo();
        }

        private void DrawTriggerGizmo()
        {
            var col = GetComponent<Collider>();
            if (col == null) return;

            Gizmos.color = _isPanelOpen
                ? new Color(0f, 1f, 0f, 0.3f)
                : new Color(1f, 1f, 0f, 0.3f);

            if (col is BoxCollider box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else if (col is SphereCollider sphere)
            {
                Gizmos.DrawSphere(transform.position + sphere.center, sphere.radius);
                Gizmos.DrawWireSphere(transform.position + sphere.center, sphere.radius);
            }
        }
#endif

        #endregion
    }
}