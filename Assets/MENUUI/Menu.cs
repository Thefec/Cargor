using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine;
using Unity.Netcode;

public class Menu : MonoBehaviour
{
    [Header("Ana Menü Butonları")]
    public Button playOnlineButton;
    public Button playOfflineButton;
    public Button settingsButton;
    public Button creditsButton;
    public Button quitButton;

    [Header("Host/Join Menü Butonları")]
    public Button hostButton;
    public Button joinButton;
    public Button exitHostJoinButton;

    [Header("Sosyal Medya Butonları")]
    public Button discordButton;
    public Button twitterButton;
    public Button instagramButton;

    [Header("UI Panelleri")]
    public GameObject mainMenuPanel;
    public GameObject hostJoinPanel;
    public GameObject settingsPanel;
    public GameObject creditsPanel;

    [Header("Version Text")]
    public TextMeshProUGUI versionText;

    [Header("Sahne İsimleri")]
    public string mapSelectionSceneName = "MapSelection";
    public string onlineRoomSceneName = "OnlineRoom";

    [Header("Settings Panel Butonları")]
    public Button backFromSettingsButton;
    public Button saveSettingsButton;

    [Header("Credits Panel Butonları")]
    public Button backFromCreditsButton;

    [Header("Sosyal Medya Linkleri")]
    public string discordURL = "https://discord.gg/yourdiscord";
    public string twitterURL = "https://twitter.com/youraccount";
    public string instagramURL = "https://instagram.com/youraccount";

    [Header("Oyun Ayarları")]
    public string gameVersion = "v1.0.0";

    [Header("Settings Manager")]
    public UnifiedSettingsManager settingsManager; // LocaleSelector yerine UnifiedSettingsManager

    // Panel durumlarını takip etmek için
    private bool isMainMenuActive = true;
    private bool isHostJoinMenuActive = false;

    // Lobi ID'si
    private string generatedLobbyId;

    void Start()
    {
        InitializeMenu();
        SetupButtonListeners();
        SetGameVersion();
        FindSettingsManager();
    }

    void FindSettingsManager()
    {
        if (settingsManager == null)
        {
            settingsManager = FindObjectOfType<UnifiedSettingsManager>();
        }
    }

    void InitializeMenu()
    {
        ShowMainMenu();
    }

    void SetupButtonListeners()
    {
        // Ana menü butonları
        if (playOnlineButton != null)
            playOnlineButton.onClick.AddListener(ShowHostJoinMenu);

        if (playOfflineButton != null)
            playOfflineButton.onClick.AddListener(PlayOffline);

        if (settingsButton != null)
            settingsButton.onClick.AddListener(OpenSettings);

        if (creditsButton != null)
            creditsButton.onClick.AddListener(OpenCredits);

        if (quitButton != null)
            quitButton.onClick.AddListener(QuitGame);

        // Host/Join butonları
        if (hostButton != null)
            hostButton.onClick.AddListener(CreateLobby);

        if (joinButton != null)
            joinButton.onClick.AddListener(JoinLobby);

        if (exitHostJoinButton != null)
            exitHostJoinButton.onClick.AddListener(ExitHostJoinMenu);

        // Settings panel butonları
        if (backFromSettingsButton != null)
            backFromSettingsButton.onClick.AddListener(BackFromSettings);

        if (saveSettingsButton != null)
            saveSettingsButton.onClick.AddListener(SaveSettings);

        // Credits geri butonu
        if (backFromCreditsButton != null)
        {
            backFromCreditsButton.onClick.RemoveAllListeners();
            backFromCreditsButton.onClick.AddListener(CloseCredits);
        }

        // Sosyal medya butonları
        if (discordButton != null)
            discordButton.onClick.AddListener(OpenDiscord);

        if (twitterButton != null)
            twitterButton.onClick.AddListener(OpenTwitter);

        if (instagramButton != null)
            instagramButton.onClick.AddListener(OpenInstagram);
    }

    void SetGameVersion()
    {
        if (versionText != null)
        {
            versionText.text = gameVersion;
        }
    }

    // PANEL YÖNETİM SİSTEMİ
    void ShowMainMenu()
    {
        Debug.Log("Ana menü gösteriliyor");

        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(true);

        if (hostJoinPanel != null)
            hostJoinPanel.SetActive(false);

        CloseAllOverlayPanels();

        isMainMenuActive = true;
        isHostJoinMenuActive = false;
    }

    void ShowHostJoinMenu()
    {
        Debug.Log("Host/Join menüsü gösteriliyor");

        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(false);

        if (hostJoinPanel != null)
            hostJoinPanel.SetActive(true);

        CloseAllOverlayPanels();

        isMainMenuActive = false;
        isHostJoinMenuActive = true;
    }

    public void ExitHostJoinMenu()
    {
        Debug.Log("Host/Join menüsünden ana menüye dönülüyor");
        ShowMainMenu();
    }

    void ShowSettingsPanel()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(true);

        if (creditsPanel != null)
            creditsPanel.SetActive(false);

        Debug.Log("Ayarlar paneli gösteriliyor");
    }

    void ShowCreditsPanel()
    {
        if (creditsPanel != null)
            creditsPanel.SetActive(true);

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        Debug.Log("Krediler paneli gösteriliyor");
    }

    void CloseAllOverlayPanels()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
        if (creditsPanel != null)
            creditsPanel.SetActive(false);
    }

    void HideAllPanels()
    {
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(false);
        if (hostJoinPanel != null)
            hostJoinPanel.SetActive(false);
        CloseAllOverlayPanels();
    }

    // Ana Menü Fonksiyonları
    public void PlayOnline()
    {
        ShowHostJoinMenu();
    }

    public void PlayOffline()
    {
        Debug.Log("Offline oyun başlatılıyor...");
        if (!string.IsNullOrEmpty(mapSelectionSceneName))
        {
            SceneManager.LoadScene(mapSelectionSceneName);
        }
        else
        {
            Debug.LogWarning("Map Selection sahne ismi belirtilmemiş!");
        }
    }

    // HOST/JOIN FONKSİYONLARI
    public void CreateLobby()
    {
        Debug.Log("Host oluşturuluyor...");

        generatedLobbyId = GenerateLobbyId();
        Debug.Log($"Lobby ID: {generatedLobbyId}");

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.StartHost();
            HideAllPanels();

            if (!string.IsNullOrEmpty(onlineRoomSceneName))
            {
                SceneManager.LoadScene(onlineRoomSceneName);
            }
        }
        else
        {
            Debug.LogError("NetworkManager bulunamadı!");
        }
    }

    public void JoinLobby()
    {
        string lobbyId = "";

        if (string.IsNullOrEmpty(lobbyId))
        {
            Debug.LogWarning("Lobby ID boş olamaz!");
            return;
        }

        if (lobbyId.Length != 6)
        {
            Debug.LogWarning("Lobby ID 6 karakter olmalı!");
            return;
        }

        Debug.Log($"Lobby'e katılınıyor: {lobbyId}");

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.StartClient();
            HideAllPanels();

            if (!string.IsNullOrEmpty(onlineRoomSceneName))
            {
                SceneManager.LoadScene(onlineRoomSceneName);
            }
        }
        else
        {
            Debug.LogError("NetworkManager bulunamadı!");
        }
    }

    private string GenerateLobbyId()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        char[] result = new char[6];

        for (int i = 0; i < 6; i++)
        {
            result[i] = chars[UnityEngine.Random.Range(0, chars.Length)];
        }

        return new string(result);
    }

    public void OpenSettings()
    {
        ShowSettingsPanel();
    }

    public void OpenCredits()
    {
        ShowCreditsPanel();
    }

    public void QuitGame()
    {
        Debug.Log("Oyundan çıkılıyor...");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
    }

    // Settings Panel Fonksiyonları
    public void BackFromSettings()
    {
        // UnifiedSettingsManager'daki kaydedilmemiş değişiklikleri geri al
        if (settingsManager != null && settingsManager.HasUnsavedChanges())
        {
            settingsManager.OnBackButtonPressed();
            Debug.Log("Ayarlar panelinden çıkış: Kaydedilmemiş değişiklikler geri alındı.");
        }

        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    public void SaveSettings()
    {
        // UnifiedSettingsManager'daki değişiklikleri kaydet
        if (settingsManager != null)
        {
            settingsManager.SaveAllSettings();
            Debug.Log("Ayarlar kaydedildi.");
        }

        Debug.Log("Tüm ayarlar kaydedildi");
    }

    // Credits Panel Fonksiyonları
    public void CloseCredits()
    {
        if (creditsPanel != null)
            creditsPanel.SetActive(false);
    }

    // ESKI FONKSİYONLAR (Uyumluluk için)
    public void CreateRoom()
    {
        CreateLobby();
    }

    public void JoinRoom()
    {
        JoinLobby();
    }

    public void BackToMainMenu()
    {
        ShowMainMenu();
    }

    // Sosyal Medya Fonksiyonları
    public void OpenDiscord()
    {
        Debug.Log("Discord açılıyor...");
        Application.OpenURL(discordURL);
    }

    public void OpenTwitter()
    {
        Debug.Log("Twitter açılıyor...");
        Application.OpenURL(twitterURL);
    }

    public void OpenInstagram()
    {
        Debug.Log("Instagram açılıyor...");
        Application.OpenURL(instagramURL);
    }

    // Yardımcı Fonksiyonlar
    public void SetGameVersion(string newVersion)
    {
        gameVersion = newVersion;
        SetGameVersion();
    }

    // ESC tuşu ile panelleri kapatma
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (settingsPanel != null && settingsPanel.activeInHierarchy)
            {
                BackFromSettings();
            }
            else if (creditsPanel != null && creditsPanel.activeInHierarchy)
            {
                CloseCredits();
            }
            else if (isHostJoinMenuActive)
            {
                ExitHostJoinMenu();
            }
        }
    }

    void OnDestroy()
    {
        // Button listener'ları temizle
        if (playOnlineButton != null)
            playOnlineButton.onClick.RemoveAllListeners();

        if (playOfflineButton != null)
            playOfflineButton.onClick.RemoveAllListeners();

        if (settingsButton != null)
            settingsButton.onClick.RemoveAllListeners();

        if (creditsButton != null)
            creditsButton.onClick.RemoveAllListeners();

        if (quitButton != null)
            quitButton.onClick.RemoveAllListeners();

        if (hostButton != null)
            hostButton.onClick.RemoveAllListeners();

        if (joinButton != null)
            joinButton.onClick.RemoveAllListeners();

        if (exitHostJoinButton != null)
            exitHostJoinButton.onClick.RemoveAllListeners();

        if (backFromSettingsButton != null)
            backFromSettingsButton.onClick.RemoveAllListeners();

        if (saveSettingsButton != null)
            saveSettingsButton.onClick.RemoveAllListeners();

        if (backFromCreditsButton != null)
            backFromCreditsButton.onClick.RemoveAllListeners();

        if (discordButton != null)
            discordButton.onClick.RemoveAllListeners();

        if (twitterButton != null)
            twitterButton.onClick.RemoveAllListeners();

        if (instagramButton != null)
            instagramButton.onClick.RemoveAllListeners();
    }
}