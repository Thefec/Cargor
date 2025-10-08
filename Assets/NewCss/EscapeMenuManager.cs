using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public class EscapeMenuManager : NetworkBehaviour
{
    [Header("Main Menu Panel")]
    public GameObject escapeMenuPanel;
    public Button continueButton;
    public Button optionsButton;
    public Button creditsButton;
    public Button exitGameButton;
    public Button closeButton;

    [Header("Sub Panels")]
    public GameObject optionsPanel;
    public GameObject creditsPanel;

    [Header("Back Buttons")]
    public Button optionsBackButton;
    public Button creditsBackButton;

    [Header("Next Day UI Reference")]
    public NewCss.NextDayUIManager nextDayUIManager; // NextDayUIManager referansı

    private bool isMenuOpen = false;
    private GameObject currentActivePanel;
    private bool isExiting = false;
    private float previousTimeScale = 1f;

    private void Start()
    {
        if (escapeMenuPanel == null || continueButton == null || closeButton == null ||
            optionsButton == null || creditsButton == null || exitGameButton == null)
        {
            Debug.LogError("[EscapeMenuManager] UI references missing in inspector! Disable script.");
            enabled = false;
            return;
        }

        previousTimeScale = Time.timeScale;

        // NextDayUIManager'ı bul
        if (nextDayUIManager == null)
        {
            nextDayUIManager = FindObjectOfType<NewCss.NextDayUIManager>();
        }

        InitializeMenu();
        SetupButtonListeners();
    }

    private void Update()
    {
        if (isExiting) return;

        // Next Day UI açıksa ESC menüsünü engelle
        if (IsNextDayUIActive())
        {
            return; // ESC tuşu işlenmeyecek
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            HandleEscapeKey();
        }
    }

    /// <summary>
    /// Next Day UI'ının aktif olup olmadığını kontrol eder
    /// </summary>
    private bool IsNextDayUIActive()
    {
        if (nextDayUIManager != null)
        {
            return nextDayUIManager.IsUIActive();
        }
        return false;
    }

    private void InitializeMenu()
    {
        escapeMenuPanel.SetActive(false);
        optionsPanel.SetActive(false);
        creditsPanel.SetActive(false);
        isMenuOpen = false;
        currentActivePanel = null;
        isExiting = false;
    }

    private void SetupButtonListeners()
    {
        continueButton.onClick.AddListener(() => RequestMenuAction(false));
        closeButton.onClick.AddListener(() => RequestMenuAction(false));
        optionsButton.onClick.AddListener(OpenOptionsPanel);
        creditsButton.onClick.AddListener(OpenCreditsPanel);
        exitGameButton.onClick.AddListener(RequestExitGame);

        optionsBackButton.onClick.AddListener(CloseOptionsPanel);
        creditsBackButton.onClick.AddListener(CloseCreditsPanel);
    }

    private void HandleEscapeKey()
    {
        if (isMenuOpen)
        {
            if (currentActivePanel == escapeMenuPanel)
            {
                RequestMenuAction(false);
            }
            else
            {
                ReturnToMainMenuPanel();
            }
        }
        else
        {
            RequestMenuAction(true);
        }
    }

    private void RequestMenuAction(bool open)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            MenuActionServerRpc(open);
        }
        else
        {
            ExecuteMenuAction(open);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void MenuActionServerRpc(bool open)
    {
        MenuActionClientRpc(open);
    }

    [ClientRpc]
    private void MenuActionClientRpc(bool open)
    {
        ExecuteMenuAction(open);
    }

    private void ExecuteMenuAction(bool open)
    {
        if (open) OpenMenuLocal();
        else CloseMenuLocal();
    }

    private void OpenMenuLocal()
    {
        if (isMenuOpen) return;

        isMenuOpen = true;
        escapeMenuPanel.SetActive(true);
        currentActivePanel = escapeMenuPanel;
        Time.timeScale = 0f;

        Debug.Log($"[Client {GetClientId()}] Menu opened - Game paused");
    }

    private void CloseMenuLocal()
    {
        if (!isMenuOpen) return;

        isMenuOpen = false;
        escapeMenuPanel.SetActive(false);
        optionsPanel.SetActive(false);
        creditsPanel.SetActive(false);
        currentActivePanel = null;
        Time.timeScale = 1f;

        Debug.Log($"[Client {GetClientId()}] Menu closed - Game resumed");
    }

    private void OpenOptionsPanel()
    {
        if (isExiting) return;
        escapeMenuPanel.SetActive(false);
        optionsPanel.SetActive(true);
        currentActivePanel = optionsPanel;
    }

    private void CloseOptionsPanel()
    {
        if (isExiting) return;
        optionsPanel.SetActive(false);
        escapeMenuPanel.SetActive(true);
        currentActivePanel = escapeMenuPanel;
    }

    private void OpenCreditsPanel()
    {
        if (isExiting) return;
        escapeMenuPanel.SetActive(false);
        creditsPanel.SetActive(true);
        currentActivePanel = creditsPanel;
    }

    private void CloseCreditsPanel()
    {
        if (isExiting) return;
        creditsPanel.SetActive(false);
        escapeMenuPanel.SetActive(true);
        currentActivePanel = escapeMenuPanel;
    }

    private void ReturnToMainMenuPanel()
    {
        if (isExiting) return;
        if (currentActivePanel != escapeMenuPanel)
        {
            optionsPanel.SetActive(false);
            creditsPanel.SetActive(false);
            escapeMenuPanel.SetActive(true);
            currentActivePanel = escapeMenuPanel;
        }
    }

    private void RequestExitGame()
    {
        if (isExiting) return;

        Debug.Log($"[Client {GetClientId()}] Exit button pressed");

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            ExitGameServerRpc();
        }
        else
        {
            isExiting = true;
            CloseMenuLocal();
            ExitHelper.Instance.StartExitProcess(false, false);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void ExitGameServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        bool senderIsHost = NetworkManager.Singleton.IsServer &&
                           senderClientId == NetworkManager.Singleton.LocalClientId;

        Debug.Log($"[Server] Exit request from client {senderClientId}. IsHost={senderIsHost}");

        if (senderIsHost)
        {
            Debug.Log("[Server] Host is exiting - notifying all clients");
            NotifyAllClientsExitClientRpc(true);
        }
        else
        {
            Debug.Log($"[Server] Client {senderClientId} is leaving");
            var clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { senderClientId }
                }
            };
            NotifyAllClientsExitClientRpc(false, clientRpcParams);
        }
    }

    [ClientRpc]
    private void NotifyAllClientsExitClientRpc(bool isHostShutdown, ClientRpcParams clientRpcParams = default)
    {
        Debug.Log($"[Client {GetClientId()}] Exit notification received. HostShutdown={isHostShutdown}");

        bool amIHost = NetworkManager.Singleton != null &&
                      NetworkManager.Singleton.IsServer;

        isExiting = true;
        CloseMenuLocal();

        ExitHelper.Instance.StartExitProcess(amIHost, isHostShutdown);
    }

    private string GetClientId()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            return NetworkManager.Singleton.LocalClientId.ToString();
        }
        return "No Network";
    }

    private void OnDestroy()
    {
        if (continueButton != null) continueButton.onClick.RemoveAllListeners();
        if (closeButton != null) closeButton.onClick.RemoveAllListeners();
        if (optionsButton != null) optionsButton.onClick.RemoveAllListeners();
        if (creditsButton != null) creditsButton.onClick.RemoveAllListeners();
        if (exitGameButton != null) exitGameButton.onClick.RemoveAllListeners();
        if (optionsBackButton != null) optionsBackButton.onClick.RemoveAllListeners();
        if (creditsBackButton != null) creditsBackButton.onClick.RemoveAllListeners();

        Time.timeScale = previousTimeScale;
    }
}