using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace NewCss.Quest
{
    /// <summary>
    /// Görev panelini açmak için trigger zone ve buton kontrolcüsü
    /// OfficeTerminal tarzı etkileşim sağlar
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class QuestTriggerZone : MonoBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[QuestTriggerZone]";
        private const string CHARACTER_TAG = "Character";

        #endregion

        #region Serialized Fields

        [Header("=== UI REFERENCES ===")]
        [SerializeField, Tooltip("Görev paneli UI kontrolcüsü")]
        private QuestUIController questUIController;

        [SerializeField, Tooltip("Etkileşim butonu")]
        private Button interactionButton;

        [SerializeField, Tooltip("Buton container (gizlenecek/gösterilecek)")]
        private GameObject buttonContainer;

        [Header("=== SETTINGS ===")]
        [SerializeField, Tooltip("Debug loglarını göster")]
        private bool showDebugLogs = true;

        #endregion

        #region Private Fields

        private bool _isPlayerInRange;
        private PlayerMovement _localPlayerMovement;
        private Collider _triggerCollider;

        #endregion

        #region Public Properties

        /// <summary>
        /// Oyuncu menzilde mi?
        /// </summary>
        public bool IsPlayerInRange => _isPlayerInRange;

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

        private void Update()
        {
            // Check for E key press when in range
            if (_isPlayerInRange && Input.GetKeyDown(KeyCode.E))
            {
                OnInteractionButtonClicked();
            }
        }

        #endregion

        #region Initialization

        private void Initialize()
        {
            SetupCollider();
            SetupButton();
            HideButton();
        }

        private void SetupCollider()
        {
            _triggerCollider = GetComponent<Collider>();
            _triggerCollider.isTrigger = true;
        }

        private void SetupButton()
        {
            if (interactionButton != null)
            {
                interactionButton.onClick.AddListener(OnInteractionButtonClicked);
            }
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

            _isPlayerInRange = true;
            _localPlayerMovement = playerMovement;

            ShowButton();

            // Set player reference for UI controller
            if (questUIController != null)
            {
                questUIController.SetLocalPlayer(playerMovement);
            }
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

            _isPlayerInRange = false;
            _localPlayerMovement = null;

            HideButton();

            // Close panel if open
            if (questUIController != null && questUIController.IsPanelOpen)
            {
                questUIController.ClosePanel();
            }
        }

        private bool ValidateCharacterTrigger(Collider other, out NetworkObject networkObject, out PlayerMovement playerMovement)
        {
            networkObject = null;
            playerMovement = null;

            // Tag check
            if (!other.CompareTag(CHARACTER_TAG))
            {
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

        #region Button Management

        private void ShowButton()
        {
            if (buttonContainer != null)
            {
                buttonContainer.SetActive(true);
            }
            else if (interactionButton != null)
            {
                interactionButton.gameObject.SetActive(true);
            }

            LogDebug("Button shown");
        }

        private void HideButton()
        {
            if (buttonContainer != null)
            {
                buttonContainer.SetActive(false);
            }
            else if (interactionButton != null)
            {
                interactionButton.gameObject.SetActive(false);
            }

            LogDebug("Button hidden");
        }

        private void OnInteractionButtonClicked()
        {
            if (!_isPlayerInRange)
            {
                LogDebug("Not in range, ignoring interaction");
                return;
            }

            if (questUIController == null)
            {
                LogWarning("QuestUIController not assigned!");
                return;
            }

            LogDebug("Interaction button clicked - toggling panel");
            questUIController.TogglePanel();
        }

        #endregion

        #region Cleanup

        private void Cleanup()
        {
            if (interactionButton != null)
            {
                interactionButton.onClick.RemoveListener(OnInteractionButtonClicked);
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
            Debug.LogWarning($"{LOG_PREFIX} {message}");
        }

        #endregion

        #region Editor Debug

#if UNITY_EDITOR
        [ContextMenu("Show Button")]
        private void DebugShowButton()
        {
            ShowButton();
        }

        [ContextMenu("Hide Button")]
        private void DebugHideButton()
        {
            HideButton();
        }

        [ContextMenu("Debug: Print State")]
        private void DebugPrintState()
        {
            Debug.Log($"{LOG_PREFIX} === TRIGGER ZONE STATE ===");
            Debug.Log($"Is Player In Range: {_isPlayerInRange}");
            Debug.Log($"Has Local Player: {_localPlayerMovement != null}");
            Debug.Log($"Has UI Controller: {questUIController != null}");
            Debug.Log($"Has Interaction Button: {interactionButton != null}");
        }

        private void OnDrawGizmosSelected()
        {
            DrawTriggerGizmo();
        }

        private void DrawTriggerGizmo()
        {
            var col = GetComponent<Collider>();
            if (col == null) return;

            Gizmos.color = _isPlayerInRange
                ? new Color(0f, 1f, 0f, 0.3f)
                : new Color(0f, 0.5f, 1f, 0.3f);

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
