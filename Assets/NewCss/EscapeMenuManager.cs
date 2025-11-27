using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ESC menü yöneticisi - oyun içi pause menüsü, ayarlar ve çıkış işlemlerini yönetir.  
/// Network desteği ile multiplayer senkronizasyonu sağlar. 
/// </summary>
public class EscapeMenuManager : NetworkBehaviour
{
    #region Constants

    private const string LOG_PREFIX = "[EscapeMenu]";

    #endregion

    #region Enums

    public enum MenuState
    {
        Closed,
        MainMenu,
        Options,
        Credits
    }


    #endregion

    #region Serialized Fields - Main Menu

    [Header("=== MAIN MENU PANEL ===")]
    [SerializeField, Tooltip("Ana menü paneli")]
    public GameObject escapeMenuPanel;

    [SerializeField, Tooltip("Devam et butonu")]
    public Button continueButton;

    [SerializeField, Tooltip("Ayarlar butonu")]
    public Button optionsButton;

    [SerializeField, Tooltip("Krediler butonu")]
    public Button creditsButton;

    [SerializeField, Tooltip("Oyundan çık butonu")]
    public Button exitGameButton;

    [SerializeField, Tooltip("Kapat butonu")]
    public Button closeButton;

    #endregion

    #region Serialized Fields - Sub Panels

    [Header("=== SUB PANELS ===")]
    [SerializeField, Tooltip("Ayarlar paneli")]
    public GameObject optionsPanel;

    [SerializeField, Tooltip("Krediler paneli")]
    public GameObject creditsPanel;

    #endregion

    #region Serialized Fields - Back Buttons

    [Header("=== BACK BUTTONS ===")]
    [SerializeField, Tooltip("Ayarlardan geri butonu")]
    public Button optionsBackButton;

    [SerializeField, Tooltip("Kredilerden geri butonu")]
    public Button creditsBackButton;

    #endregion

    #region Serialized Fields - References

    [Header("=== EXTERNAL REFERENCES ===")]
    [SerializeField, Tooltip("Next Day UI Manager referansı")]
    public NewCss.NextDayUIManager nextDayUIManager;

    #endregion

    #region Private Fields

    private MenuState _currentState = MenuState.Closed;
    private bool _isExiting;
    private float _previousTimeScale = 1f;

    #endregion

    #region Public Properties

    /// <summary>
    /// Menü açık mı?
    /// </summary>
    public bool IsMenuOpen => _currentState != MenuState.Closed;

    /// <summary>
    /// Çıkış işlemi devam ediyor mu?
    /// </summary>
    public bool IsExiting => _isExiting;

    /// <summary>
    /// Mevcut menü durumu
    /// </summary>
    public MenuState CurrentState => _currentState;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        if (!ValidateReferences())
        {
            enabled = false;
            return;
        }

        CacheTimeScale();
        FindNextDayUIManager();
        InitializeMenu();
        SetupButtonListeners();
    }

    private void Update()
    {
        if (_isExiting) return;

        if (ShouldBlockEscapeKey())
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            HandleEscapeKey();
        }
    }

    private void OnDestroy()
    {
        CleanupButtonListeners();
        RestoreTimeScale();
    }

    #endregion

    #region Initialization

    private bool ValidateReferences()
    {
        bool hasRequiredReferences = escapeMenuPanel != null &&
                                      continueButton != null &&
                                      closeButton != null &&
                                      optionsButton != null &&
                                      creditsButton != null &&
                                      exitGameButton != null;

        if (!hasRequiredReferences)
        {
            LogError("UI references missing in inspector! Disabling script.");
            return false;
        }

        return true;
    }

    private void CacheTimeScale()
    {
        _previousTimeScale = Time.timeScale;
    }

    private void FindNextDayUIManager()
    {
        if (nextDayUIManager == null)
        {
            nextDayUIManager = FindObjectOfType<NewCss.NextDayUIManager>();
        }
    }

    private void InitializeMenu()
    {
        SetPanelActive(escapeMenuPanel, false);
        SetPanelActive(optionsPanel, false);
        SetPanelActive(creditsPanel, false);

        _currentState = MenuState.Closed;
        _isExiting = false;
    }

    #endregion

    #region Button Setup

    private void SetupButtonListeners()
    {
        // Main menu buttons
        AddButtonListener(continueButton, () => RequestMenuAction(false));
        AddButtonListener(closeButton, () => RequestMenuAction(false));
        AddButtonListener(optionsButton, OpenOptionsPanel);
        AddButtonListener(creditsButton, OpenCreditsPanel);
        AddButtonListener(exitGameButton, RequestExitGame);

        // Back buttons
        AddButtonListener(optionsBackButton, CloseOptionsPanel);
        AddButtonListener(creditsBackButton, CloseCreditsPanel);
    }

    private void AddButtonListener(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null)
        {
            button.onClick.AddListener(action);
        }
    }

    private void CleanupButtonListeners()
    {
        RemoveButtonListener(continueButton);
        RemoveButtonListener(closeButton);
        RemoveButtonListener(optionsButton);
        RemoveButtonListener(creditsButton);
        RemoveButtonListener(exitGameButton);
        RemoveButtonListener(optionsBackButton);
        RemoveButtonListener(creditsBackButton);
    }

    private void RemoveButtonListener(Button button)
    {
        button?.onClick.RemoveAllListeners();
    }

    #endregion

    #region Input Handling

    private bool ShouldBlockEscapeKey()
    {
        // Next Day UI açıksa ESC'yi engelle
        return IsNextDayUIActive();
    }

    private bool IsNextDayUIActive()
    {
        return nextDayUIManager != null && nextDayUIManager.IsUIActive();
    }

    private void HandleEscapeKey()
    {
        switch (_currentState)
        {
            case MenuState.Closed:
                RequestMenuAction(true);
                break;

            case MenuState.MainMenu:
                RequestMenuAction(false);
                break;

            case MenuState.Options:
            case MenuState.Credits:
                ReturnToMainMenuPanel();
                break;
        }
    }

    #endregion

    #region Menu Actions

    private void RequestMenuAction(bool open)
    {
        if (IsNetworkActive())
        {
            MenuActionServerRpc(open);
        }
        else
        {
            ExecuteMenuAction(open);
        }
    }

    private void ExecuteMenuAction(bool open)
    {
        if (open)
        {
            OpenMenuLocal();
        }
        else
        {
            CloseMenuLocal();
        }
    }

    private void OpenMenuLocal()
    {
        if (IsMenuOpen) return;

        TransitionToState(MenuState.MainMenu);
        PauseGame();

        LogDebug($"[Client {GetClientId()}] Menu opened - Game paused");
    }

    private void CloseMenuLocal()
    {
        if (!IsMenuOpen) return;

        TransitionToState(MenuState.Closed);
        ResumeGame();

        LogDebug($"[Client {GetClientId()}] Menu closed - Game resumed");
    }

    #endregion

    #region State Management

    private void TransitionToState(MenuState newState)
    {
        _currentState = newState;
        UpdatePanelVisibility();
    }

    private void UpdatePanelVisibility()
    {
        SetPanelActive(escapeMenuPanel, _currentState == MenuState.MainMenu);
        SetPanelActive(optionsPanel, _currentState == MenuState.Options);
        SetPanelActive(creditsPanel, _currentState == MenuState.Credits);
    }

    private static void SetPanelActive(GameObject panel, bool active)
    {
        if (panel != null)
        {
            panel.SetActive(active);
        }
    }

    #endregion

    #region Sub Panel Navigation

    private void OpenOptionsPanel()
    {
        if (_isExiting) return;

        TransitionToState(MenuState.Options);
    }

    private void CloseOptionsPanel()
    {
        if (_isExiting) return;

        TransitionToState(MenuState.MainMenu);
    }

    private void OpenCreditsPanel()
    {
        if (_isExiting) return;

        TransitionToState(MenuState.Credits);
    }

    private void CloseCreditsPanel()
    {
        if (_isExiting) return;

        TransitionToState(MenuState.MainMenu);
    }

    private void ReturnToMainMenuPanel()
    {
        if (_isExiting) return;

        if (_currentState != MenuState.MainMenu && _currentState != MenuState.Closed)
        {
            TransitionToState(MenuState.MainMenu);
        }
    }

    #endregion

    #region Time Control

    private void PauseGame()
    {
        Time.timeScale = 0f;
    }

    private void ResumeGame()
    {
        Time.timeScale = 1f;
    }

    private void RestoreTimeScale()
    {
        Time.timeScale = _previousTimeScale;
    }

    #endregion

    #region Exit Game

    private void RequestExitGame()
    {
        if (_isExiting) return;

        LogDebug($"[Client {GetClientId()}] Exit button pressed");

        if (IsNetworkActive())
        {
            ExitGameServerRpc();
        }
        else
        {
            ExecuteLocalExit();
        }
    }

    private void ExecuteLocalExit()
    {
        _isExiting = true;
        CloseMenuLocal();
        ExitHelper.Instance.StartExitProcess(false, false);
    }

    #endregion

    #region Network Helpers

    private bool IsNetworkActive()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    }

    private string GetClientId()
    {
        if (IsNetworkActive())
        {
            return NetworkManager.Singleton.LocalClientId.ToString();
        }
        return "No Network";
    }

    private bool IsLocalClientHost()
    {
        return NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
    }

    #endregion

    #region Server RPCs

    [ServerRpc(RequireOwnership = false)]
    private void MenuActionServerRpc(bool open)
    {
        MenuActionClientRpc(open);
    }

    [ServerRpc(RequireOwnership = false)]
    private void ExitGameServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        bool senderIsHost = IsSenderHost(senderClientId);

        LogDebug($"[Server] Exit request from client {senderClientId}. IsHost={senderIsHost}");

        if (senderIsHost)
        {
            HandleHostExit();
        }
        else
        {
            HandleClientExit(senderClientId);
        }
    }

    private bool IsSenderHost(ulong senderClientId)
    {
        return NetworkManager.Singleton.IsServer &&
               senderClientId == NetworkManager.Singleton.LocalClientId;
    }

    private void HandleHostExit()
    {
        LogDebug("[Server] Host is exiting - notifying all clients");
        NotifyAllClientsExitClientRpc(true);
    }

    private void HandleClientExit(ulong clientId)
    {
        LogDebug($"[Server] Client {clientId} is leaving");

        var clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { clientId }
            }
        };

        NotifyAllClientsExitClientRpc(false, clientRpcParams);
    }

    #endregion

    #region Client RPCs

    [ClientRpc]
    private void MenuActionClientRpc(bool open)
    {
        ExecuteMenuAction(open);
    }

    [ClientRpc]
    private void NotifyAllClientsExitClientRpc(bool isHostShutdown, ClientRpcParams clientRpcParams = default)
    {
        LogDebug($"[Client {GetClientId()}] Exit notification received. HostShutdown={isHostShutdown}");

        bool amIHost = IsLocalClientHost();

        _isExiting = true;
        CloseMenuLocal();

        ExitHelper.Instance.StartExitProcess(amIHost, isHostShutdown);
    }

    #endregion

    #region Public API

    /// <summary>
    /// Menüyü açar
    /// </summary>
    public void OpenMenu()
    {
        if (!_isExiting)
        {
            RequestMenuAction(true);
        }
    }

    /// <summary>
    /// Menüyü kapatır
    /// </summary>
    public void CloseMenu()
    {
        if (!_isExiting)
        {
            RequestMenuAction(false);
        }
    }

    /// <summary>
    /// Menüyü toggle eder
    /// </summary>
    public void ToggleMenu()
    {
        if (!_isExiting)
        {
            RequestMenuAction(!IsMenuOpen);
        }
    }

    /// <summary>
    /// Menüyü zorla kapatır (animasyonsuz)
    /// </summary>
    public void ForceCloseMenu()
    {
        _currentState = MenuState.Closed;
        UpdatePanelVisibility();
        ResumeGame();
    }

    #endregion

    #region Logging

    private void LogDebug(string message)
    {
        Debug.Log($"{LOG_PREFIX} {message}");
    }

    private void LogError(string message)
    {
        Debug.LogError($"{LOG_PREFIX} {message}");
    }

    #endregion

    #region Editor Debug

#if UNITY_EDITOR
    [ContextMenu("Open Menu")]
    private void DebugOpenMenu()
    {
        OpenMenu();
    }

    [ContextMenu("Close Menu")]
    private void DebugCloseMenu()
    {
        CloseMenu();
    }

    [ContextMenu("Toggle Menu")]
    private void DebugToggleMenu()
    {
        ToggleMenu();
    }

    [ContextMenu("Force Close Menu")]
    private void DebugForceCloseMenu()
    {
        ForceCloseMenu();
    }

    [ContextMenu("Open Options")]
    private void DebugOpenOptions()
    {
        OpenOptionsPanel();
    }

    [ContextMenu("Open Credits")]
    private void DebugOpenCredits()
    {
        OpenCreditsPanel();
    }

    [ContextMenu("Debug: Print State")]
    private void DebugPrintState()
    {
        Debug.Log($"{LOG_PREFIX} === ESCAPE MENU STATE ===");
        Debug.Log($"Current State: {_currentState}");
        Debug.Log($"Is Menu Open: {IsMenuOpen}");
        Debug.Log($"Is Exiting: {_isExiting}");
        Debug.Log($"Time Scale: {Time.timeScale}");
        Debug.Log($"Previous Time Scale: {_previousTimeScale}");
        Debug.Log($"Is Network Active: {IsNetworkActive()}");
        Debug.Log($"Client ID: {GetClientId()}");
        Debug.Log($"Is Host: {IsLocalClientHost()}");
        Debug.Log($"Next Day UI Active: {IsNextDayUIActive()}");
    }
#endif

    #endregion
}