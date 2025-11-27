using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace NewCss.UIScripts
{
    /// <summary>
    /// Network destekli karakter özelleştirme UI sistemi.   
    /// Trigger bazlı etkileşim, animasyonlu panel açma/kapama ve karakter kustomizasyon kaydetme özellikleri sunar.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class NetworkCharacterCusUI : MonoBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[CharacterCusUI]";
        private const string CHARACTER_TAG = "Character";
        private const float DEFAULT_OPEN_ANIMATION_DURATION = 0.5f;
        private const float DEFAULT_CLOSE_ANIMATION_DURATION = 0.25f;

        // Animator triggers
        private const string TRIGGER_OPEN = "Open";
        private const string TRIGGER_CLOSE = "Close";

        #endregion

        #region Serialized Fields - UI References

        [Header("=== UI REFERENCES ===")]
        [SerializeField, Tooltip("Açılacak UI paneli")]
        public GameObject uiPanel;

        [SerializeField, Tooltip("Etkileşim butonu")]
        public GameObject interactionButton;

        [SerializeField, Tooltip("Kapatma butonu")]
        public Button closeButton;

        #endregion

        #region Serialized Fields - Animation

        [Header("=== ANIMATION SETTINGS ===")]
        [SerializeField, Tooltip("UI panel animator'ı")]
        public Animator uiAnimator;

        [SerializeField, Tooltip("Kapatma animasyon clip adı")]
        public string closeAnimationClipName = "SlideOut";

        [SerializeField, Tooltip("Açma animasyon clip adı")]
        public string openAnimationClipName = "SlideIn";

        #endregion

        #region Serialized Fields - Interaction

        [Header("=== INTERACTION SETTINGS ===")]
        [SerializeField, Tooltip("Karakter kameraya dönme hızı (0 = anında)")]
        public float rotationSpeed = 0f;

        [SerializeField, Tooltip("Etkileşim için gereken maksimum saat")]
        private int CurrentTime = 14;

        #endregion

        #region Private Fields - Character References

        private NetworkObject _currentCharacterNetwork;
        private Transform _currentCharacter;
        private PlayerMovement _currentPlayerMovement;
        private NetworkCharacterMeshSwapper _characterMeshSwapper;

        #endregion

        #region Private Fields - UI State

        private Button _buttonComponent;
        private bool _isPanelOpen;
        private bool _isAnimating;

        #endregion

        #region Private Fields - Cached Animation Durations

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
            InitializeUIPanel();
            InitializeInteractionButton();
            InitializeCloseButton();
            InitializeAnimator();
            CacheAnimationDurations();
        }

        private void InitializeUIPanel()
        {
            if (uiPanel == null)
            {
                LogWarning("uiPanel not assigned!");
                return;
            }

            uiPanel.SetActive(false);
        }

        private void InitializeInteractionButton()
        {
            if (interactionButton == null)
            {
                LogWarning("interactionButton not assigned!");
                return;
            }

            interactionButton.SetActive(false);
            _buttonComponent = interactionButton.GetComponent<Button>();

            if (_buttonComponent != null)
            {
                _buttonComponent.onClick.AddListener(HandleInteractionButtonClicked);
            }
            else
            {
                LogWarning("Interaction button doesn't have a Button component!");
            }
        }

        private void InitializeCloseButton()
        {
            if (closeButton != null)
            {
                closeButton.onClick.AddListener(HandleCloseButtonClicked);
            }
            else
            {
                LogWarning("Close button not assigned!  Panel won't be closable.");
            }
        }

        private void InitializeAnimator()
        {
            if (uiAnimator != null) return;

            if (uiPanel != null)
            {
                uiAnimator = uiPanel.GetComponent<Animator>();

                if (uiAnimator == null)
                {
                    LogWarning("uiAnimator not assigned and uiPanel has no Animator!");
                }
            }
        }

        private void CacheAnimationDurations()
        {
            if (uiAnimator == null) return;

            var controller = uiAnimator.runtimeAnimatorController;
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
                   clipName.Contains("SlideIn") ||
                   clipName == openAnimationClipName;
        }

        private bool IsCloseAnimationClip(string clipName)
        {
            return clipName.Contains("Close") ||
                   clipName.Contains("SlideOut") ||
                   clipName == closeAnimationClipName;
        }

        #endregion

        #region Trigger Detection

        private void OnTriggerEnter(Collider other)
        {
            if (!ValidateCharacterTrigger(other, out NetworkObject networkObject))
            {
                return;
            }

            CacheCharacterReferences(other, networkObject);
            TryShowInteractionButton();
        }

        private void OnTriggerExit(Collider other)
        {
            var networkObject = other.GetComponent<NetworkObject>();

            if (networkObject == null || networkObject != _currentCharacterNetwork)
            {
                return;
            }

            if (!networkObject.IsOwner || !other.CompareTag(CHARACTER_TAG))
            {
                return;
            }

            // Panel açıksa kapat
            if (_isPanelOpen)
            {
                CloseUI();
            }

            ClearCharacterReferences();
            HideInteractionButton();
        }

        private bool ValidateCharacterTrigger(Collider other, out NetworkObject networkObject)
        {
            networkObject = other.GetComponent<NetworkObject>();

            if (networkObject == null)
            {
                return false;
            }

            if (!networkObject.IsOwner)
            {
                return false;
            }

            if (!other.CompareTag(CHARACTER_TAG))
            {
                return false;
            }

            return true;
        }

        #endregion

        #region Character Reference Management

        private void CacheCharacterReferences(Collider other, NetworkObject networkObject)
        {
            _currentCharacterNetwork = networkObject;
            _currentCharacter = other.transform;

            // Cache components
            _characterMeshSwapper = FindComponent<NetworkCharacterMeshSwapper>(other);
            _currentPlayerMovement = FindComponent<PlayerMovement>(other);
        }

        private void ClearCharacterReferences()
        {
            _currentCharacterNetwork = null;
            _currentCharacter = null;
            _currentPlayerMovement = null;
            _characterMeshSwapper = null;
        }

        private T FindComponent<T>(Collider other) where T : Component
        {
            var component = other.GetComponent<T>();

            if (component == null)
            {
                component = other.GetComponentInChildren<T>();
            }

            if (component == null)
            {
                component = other.GetComponentInParent<T>();
            }

            return component;
        }

        #endregion

        #region Interaction Button

        private void TryShowInteractionButton()
        {
            if (interactionButton == null)
            {
                LogWarning("interactionButton not assigned!");
                return;
            }

            if (_isPanelOpen)
            {
                return;
            }

            int currentHour = GetCurrentHour();
            LogDebug($"Current Hour: {currentHour}, Required Time: {CurrentTime}");

            if (currentHour <= CurrentTime)
            {
                interactionButton.SetActive(true);
            }
            else
            {
                LogDebug($"Button not shown - Current hour ({currentHour}) > required time ({CurrentTime})");
            }
        }

        private void HideInteractionButton()
        {
            if (interactionButton != null)
            {
                interactionButton.SetActive(false);
            }
        }

        /// <summary>
        /// Zaman koşulunu kontrol eder ve butonu günceller
        /// </summary>
        public void CheckTimeCondition()
        {
            if (_currentCharacterNetwork == null || !_currentCharacterNetwork.IsOwner)
            {
                return;
            }

            if (interactionButton == null || uiPanel == null || _isPanelOpen)
            {
                return;
            }

            int currentHour = GetCurrentHour();
            interactionButton.SetActive(currentHour <= CurrentTime);
        }

        #endregion

        #region Button Event Handlers

        private void HandleInteractionButtonClicked()
        {
            if (!CanOpenPanel())
            {
                return;
            }

            OpenUI();
        }

        private void HandleCloseButtonClicked()
        {
            if (_isAnimating)
            {
                LogDebug("Animation in progress, ignoring close request");
                return;
            }

            CloseUI();
        }

        private bool CanOpenPanel()
        {
            if (_currentCharacterNetwork == null || !_currentCharacterNetwork.IsOwner)
            {
                LogWarning("Cannot interact - no valid local player character");
                return false;
            }

            if (_isAnimating)
            {
                LogDebug("Animation in progress, ignoring interaction");
                return false;
            }

            if (_isPanelOpen)
            {
                LogDebug("Panel already open, ignoring interaction");
                return false;
            }

            return true;
        }

        #endregion

        #region Open UI

        private void OpenUI()
        {
            // Rotate character
            if (_currentCharacter != null)
            {
                RotateCharacterToCamera(_currentCharacter);
            }

            // Lock movement
            SetPlayerMovementLock(true);

            _isPanelOpen = true;
            _isAnimating = true;

            // Open panel
            if (uiPanel == null)
            {
                LogWarning("uiPanel not assigned!");
                _isAnimating = false;
                return;
            }

            PrepareAndOpenPanel();
        }

        private void PrepareAndOpenPanel()
        {
            // Reset animator state
            if (uiAnimator != null)
            {
                uiAnimator.Rebind();
                uiAnimator.Update(0f);
                ResetAnimatorTriggers();
            }

            // Activate panel
            uiPanel.SetActive(true);

            // Hide interaction button
            HideInteractionButton();

            // Notify customization UI
            NotifyCustomizationUI();

            // Start animation
            StartCoroutine(PlayOpenAnimationCoroutine());
        }

        private void NotifyCustomizationUI()
        {
            if (_currentCharacterNetwork == null) return;

            var customizationUI = uiPanel.GetComponent<NetworkCharacterCustomizationUI>();
            if (customizationUI != null)
            {
                customizationUI.OnCharacterSpawned(_currentCharacterNetwork.gameObject);
            }
        }

        private IEnumerator PlayOpenAnimationCoroutine()
        {
            // Wait one frame for panel to properly activate
            yield return null;

            if (uiAnimator != null)
            {
                uiAnimator.SetTrigger(TRIGGER_OPEN);

                float duration = GetOpenAnimationDuration();
                yield return new WaitForSeconds(duration);
            }

            _isAnimating = false;
            LogDebug("Open animation completed");
        }

        #endregion

        #region Close UI

        /// <summary>
        /// UI'ı kapatır (animasyonlu)
        /// </summary>
        public void CloseUI()
        {
            if (uiPanel == null || !_isPanelOpen)
            {
                LogDebug("Panel not open or null, cannot close");
                return;
            }

            if (_isAnimating)
            {
                LogDebug("Animation in progress, ignoring close request");
                return;
            }

            // Save customization before closing
            SaveCharacterCustomization();

            _isPanelOpen = false;
            _isAnimating = true;

            if (uiAnimator != null)
            {
                ResetAnimatorTriggers();
                uiAnimator.SetTrigger(TRIGGER_CLOSE);
                StartCoroutine(PlayCloseAnimationCoroutine());
            }
            else
            {
                CompleteClose();
            }

            // Show interaction button if conditions met
            TryShowInteractionButtonAfterClose();
        }

        private IEnumerator PlayCloseAnimationCoroutine()
        {
            float duration = GetCloseAnimationDuration();
            yield return new WaitForSeconds(duration);

            CompleteClose();
        }

        private void CompleteClose()
        {
            if (uiPanel != null)
            {
                uiPanel.SetActive(false);
            }

            SetPlayerMovementLock(false);
            _isAnimating = false;

            LogDebug("Close animation completed");
        }

        private void TryShowInteractionButtonAfterClose()
        {
            if (_currentCharacterNetwork == null || !_currentCharacterNetwork.IsOwner)
            {
                return;
            }

            if (interactionButton == null)
            {
                return;
            }

            int currentHour = GetCurrentHour();
            if (currentHour <= CurrentTime)
            {
                interactionButton.SetActive(true);
            }
        }

        /// <summary>
        /// UI'ı zorla kapatır ve movement'ı unlock eder
        /// </summary>
        public void ForceCloseAndUnlock()
        {
            if (!_isPanelOpen) return;

            SaveCharacterCustomization();

            _isPanelOpen = false;
            _isAnimating = false;

            SetPlayerMovementLock(false);

            if (uiPanel != null)
            {
                uiPanel.SetActive(false);
            }

            TryShowInteractionButtonAfterClose();

            LogDebug("Force close and unlock completed");
        }

        #endregion

        #region Animation Helpers

        private void ResetAnimatorTriggers()
        {
            if (uiAnimator == null) return;

            uiAnimator.ResetTrigger(TRIGGER_OPEN);
            uiAnimator.ResetTrigger(TRIGGER_CLOSE);
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

        #region Player Movement

        private void SetPlayerMovementLock(bool locked)
        {
            if (_currentPlayerMovement == null)
            {
                LogWarning("PlayerMovement component not found!");
                return;
            }

            _currentPlayerMovement.LockMovement(locked);
            _currentPlayerMovement.LockAllInteractions(locked);

            LogDebug($"Player movement {(locked ? "locked" : "unlocked")}");
        }

        #endregion

        #region Character Rotation

        private void RotateCharacterToCamera(Transform character)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                LogError("Main Camera not found!");
                return;
            }

            Vector3 direction = cam.transform.position - character.position;
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(direction);

            if (rotationSpeed <= 0f)
            {
                character.rotation = targetRotation;
            }
            else
            {
                character.rotation = Quaternion.Slerp(
                    character.rotation,
                    targetRotation,
                    rotationSpeed * Time.deltaTime
                );
            }
        }

        #endregion

        #region Customization Management

        private void SaveCharacterCustomization()
        {
            if (_characterMeshSwapper == null)
            {
                LogWarning("NetworkCharacterMeshSwapper not found - cannot save customization!");
                return;
            }

            if (_currentCharacterNetwork == null || !_currentCharacterNetwork.IsOwner)
            {
                return;
            }

            _characterMeshSwapper.SaveCustomizationData();
            LogDebug("Character customization saved!");
        }

        /// <summary>
        /// Manuel kaydetme butonu için
        /// </summary>
        public void SaveCustomizationManually()
        {
            SaveCharacterCustomization();
        }

        /// <summary>
        /// Kustomizasyonu varsayılana sıfırlar
        /// </summary>
        public void ResetCustomization()
        {
            if (_characterMeshSwapper == null)
            {
                return;
            }

            if (_currentCharacterNetwork == null || !_currentCharacterNetwork.IsOwner)
            {
                return;
            }

            _characterMeshSwapper.ResetToDefaults();
            LogDebug("Character customization reset to defaults!");
        }

        #endregion

        #region Animation Event Callbacks

        /// <summary>
        /// Animation Event callback - açılma animasyonu tamamlandığında
        /// </summary>
        public void OnOpenAnimationComplete()
        {
            _isAnimating = false;
            LogDebug("Open animation completed via Animation Event");
        }

        /// <summary>
        /// Animation Event callback - kapanma animasyonu tamamlandığında
        /// </summary>
        public void OnCloseAnimationComplete()
        {
            CompleteClose();
            LogDebug("Close animation completed via Animation Event");
        }

        #endregion

        #region Utility

        private int GetCurrentHour()
        {
            return DayCycleManager.Instance?.CurrentHour ?? 0;
        }

        #endregion

        #region Cleanup

        private void Cleanup()
        {
            // Remove listeners
            if (_buttonComponent != null)
            {
                _buttonComponent.onClick.RemoveListener(HandleInteractionButtonClicked);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(HandleCloseButtonClicked);
            }

            // Save if panel is open
            if (_isPanelOpen)
            {
                SaveCharacterCustomization();
                SetPlayerMovementLock(false);
            }

            _isAnimating = false;
        }

        #endregion

        #region Logging

        private void LogDebug(string message)
        {
            Debug.Log($"{LOG_PREFIX} {message}");
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

        #region Editor Debug

#if UNITY_EDITOR
        [ContextMenu("Open UI")]
        private void DebugOpenUI()
        {
            if (_currentCharacterNetwork != null)
            {
                OpenUI();
            }
            else
            {
                LogWarning("No character in range to open UI");
            }
        }

        [ContextMenu("Close UI")]
        private void DebugCloseUI()
        {
            CloseUI();
        }

        [ContextMenu("Force Close")]
        private void DebugForceClose()
        {
            ForceCloseAndUnlock();
        }

        [ContextMenu("Save Customization")]
        private void DebugSaveCustomization()
        {
            SaveCustomizationManually();
        }

        [ContextMenu("Reset Customization")]
        private void DebugResetCustomization()
        {
            ResetCustomization();
        }

        [ContextMenu("Debug: Print State")]
        private void DebugPrintState()
        {
            Debug.Log($"{LOG_PREFIX} === UI STATE ===");
            Debug.Log($"Is Panel Open: {_isPanelOpen}");
            Debug.Log($"Is Animating: {_isAnimating}");
            Debug.Log($"Can Interact: {CanInteract}");
            Debug.Log($"Has Character: {_currentCharacterNetwork != null}");
            Debug.Log($"Has Movement: {_currentPlayerMovement != null}");
            Debug.Log($"Has MeshSwapper: {_characterMeshSwapper != null}");
            Debug.Log($"Current Hour: {GetCurrentHour()}");
            Debug.Log($"Required Hour: {CurrentTime}");
            Debug.Log($"Cached Open Duration: {_cachedOpenDuration}");
            Debug.Log($"Cached Close Duration: {_cachedCloseDuration}");
        }
#endif

        #endregion
    }
}