using UnityEngine;
using Unity.Netcode;

namespace NewCss.UIScripts
{
    /// <summary>
    /// Sayfalý UI paneli açmak için trigger zone kontrolcüsü. 
    /// Collider'a girince otomatik açýlýr, çýkýnca kapanýr.
    /// Unity Netcode ile uyumlu. 
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class PagedUIPanelTrigger : MonoBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[PagedUIPanelTrigger]";
        private const string CHARACTER_TAG = "Character";

        #endregion

        #region Serialized Fields

        [Header("=== PANEL REFERENCE ===")]
        [SerializeField, Tooltip("Açýlacak sayfalý UI paneli")]
        private PagedUIPanel pagedUIPanel;

        [Header("=== TRIGGER SETTINGS ===")]
        [SerializeField, Tooltip("Giriþte otomatik aç")]
        private bool openOnEnter = true;

        [SerializeField, Tooltip("Çýkýþta otomatik kapat")]
        private bool closeOnExit = true;

        [Header("=== DEBUG ===")]
        [SerializeField, Tooltip("Debug loglarýný göster")]
        private bool showDebugLogs = true;

        #endregion

        #region Private Fields

        private bool _isPlayerInRange;
        private PlayerMovement _localPlayerMovement;
        private Collider _triggerCollider;

        #endregion

        #region Public Properties

        public bool IsPlayerInRange => _isPlayerInRange;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            Initialize();
        }

        #endregion

        #region Initialization

        private void Initialize()
        {
            SetupCollider();
            ValidateReferences();
        }

        private void SetupCollider()
        {
            _triggerCollider = GetComponent<Collider>();

            if (_triggerCollider != null)
            {
                _triggerCollider.isTrigger = true;
            }
            else
            {
                LogWarning("Collider bulunamadý!");
            }
        }

        private void ValidateReferences()
        {
            if (pagedUIPanel == null)
            {
                LogWarning("PagedUIPanel referansý atanmamýþ!");
            }
        }

        #endregion

        #region Trigger Handlers

        private void OnTriggerEnter(Collider other)
        {
            if (!ValidateCharacterTrigger(other, out NetworkObject networkObject, out PlayerMovement playerMovement))
            {
                return;
            }

            _isPlayerInRange = true;
            _localPlayerMovement = playerMovement;

            LogDebug("Oyuncu trigger alanýna girdi");

            if (pagedUIPanel != null)
            {
                pagedUIPanel.SetLocalPlayer(playerMovement);

                if (openOnEnter && !pagedUIPanel.IsPanelOpen)
                {
                    pagedUIPanel.OpenPanel();
                    LogDebug("Panel otomatik açýldý");
                }
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

            _isPlayerInRange = false;
            _localPlayerMovement = null;

            LogDebug("Oyuncu trigger alanýndan çýktý");

            if (pagedUIPanel != null && closeOnExit && pagedUIPanel.IsPanelOpen)
            {
                pagedUIPanel.ClosePanel();
                LogDebug("Panel otomatik kapatýldý");
            }
        }

        private bool ValidateCharacterTrigger(Collider other, out NetworkObject networkObject, out PlayerMovement playerMovement)
        {
            networkObject = null;
            playerMovement = null;

            // Tag kontrolü
            if (!other.CompareTag(CHARACTER_TAG))
            {
                return false;
            }

            // NetworkObject kontrolü (Unity Netcode)
            networkObject = other.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                networkObject = other.GetComponentInParent<NetworkObject>();
            }

            if (networkObject == null)
            {
                LogWarning("Character üzerinde NetworkObject bulunamadý!");
                return false;
            }

            // Sadece local owner için çalýþ
            if (!networkObject.IsOwner)
            {
                LogDebug("Local player deðil, görmezden geliniyor");
                return false;
            }

            // PlayerMovement kontrolü
            playerMovement = other.GetComponent<PlayerMovement>();
            if (playerMovement == null)
            {
                playerMovement = other.GetComponentInParent<PlayerMovement>();
            }

            if (playerMovement == null)
            {
                LogWarning("PlayerMovement component bulunamadý!");
            }

            return true;
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
        [ContextMenu("Debug: Print State")]
        private void DebugPrintState()
        {
            Debug.Log($"{LOG_PREFIX} === TRIGGER STATE ===");
            Debug.Log($"Is Player In Range: {_isPlayerInRange}");
            Debug.Log($"Has Panel Reference: {pagedUIPanel != null}");
            Debug.Log($"Open On Enter: {openOnEnter}");
            Debug.Log($"Close On Exit: {closeOnExit}");
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