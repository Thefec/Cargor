using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine;
using Unity.Netcode;

public class Menu : MonoBehaviour
{
    [Header("Ana Menü Butonlarý")]
    public Button playOnlineButton;
    public Button playOfflineButton;
    public Button settingsButton;
    public Button creditsButton;
    public Button quitButton;

    [Header("Host/Join Menü Butonlarý")]
    public Button hostButton;
    public Button joinButton;
    public Button exitHostJoinButton;

    [Header("Sosyal Medya Butonlarý")]
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

    [Header("Sahne Ýsimleri")]
    public string mapSelectionSceneName = "MapSelection";
    public string onlineRoomSceneName = "OnlineRoom";

    [Header("Settings Panel Butonlarý")]
    public Button backFromSettingsButton;
    public Button saveSettingsButton;

    [Header("Credits Panel Butonlarý")]
    public Button backFromCreditsButton;

    [Header("Sosyal Medya Linkleri")]
    public string discordURL = "https://discord.gg/yourdiscord";
    public string twitterURL = "https://twitter.com/youraccount";
    public string instagramURL = "https://instagram.com/youraccount";

    [Header("Audio Sources")]
    public AudioSource musicAudioSource;
    public AudioSource sfxAudioSource;
    public AudioSource uiAudioSource;

    [Header("UI Sound Effects")]
    public AudioClip buttonClickSound;
    public AudioClip buttonHoverSound;
    [Range(0f, 1f)]
    public float buttonSoundVolume = 1f;

    [Header("Oyun Ayarlarý")]
    public string gameVersion = "v1.0.0";

    [Header("Settings Manager")]
    public UnifiedSettingsManager settingsManager;

    // ? YENÝ: SteamManager referansý
    [Header("Steam Manager")]
    public SteamManager steamManager;

    // Panel durumlarýný takip etmek için
    private bool isMainMenuActive = true;
    private bool isHostJoinMenuActive = false;

    void Start()
    {
        InitializeMenu();
        SetupButtonListeners();
        SetGameVersion();
        FindSettingsManager();
        FindSteamManager(); // ? YENÝ
    }

    void FindSettingsManager()
    {
        if (settingsManager == null)
        {
            settingsManager = FindObjectOfType<UnifiedSettingsManager>();
        }
    }

    // ? YENÝ: SteamManager'ý otomatik bul
    void FindSteamManager()
    {
        if (steamManager == null)
        {
            steamManager = FindObjectOfType<SteamManager>();

            if (steamManager == null)
            {
                Debug.LogWarning("?? SteamManager bulunamadý! Online özellikler çalýþmayabilir.");
            }
        }
    }

    void InitializeMenu()
    {
        ShowMainMenu();
    }

    void SetupButtonListeners()
    {
        // Ana menü butonlarý
        if (playOnlineButton != null)
            playOnlineButton.onClick.AddListener(() => { PlayButtonSound(); ShowHostJoinMenu(); });

        if (playOfflineButton != null)
            playOfflineButton.onClick.AddListener(() => { PlayButtonSound(); PlayOffline(); });

        if (settingsButton != null)
            settingsButton.onClick.AddListener(() => { PlayButtonSound(); OpenSettings(); });

        if (creditsButton != null)
            creditsButton.onClick.AddListener(() => { PlayButtonSound(); OpenCredits(); });

        if (quitButton != null)
            quitButton.onClick.AddListener(() => { PlayButtonSound(); QuitGame(); });

        // ? DEÐÝÞTÝRÝLDÝ: Host/Join butonlarý artýk köprü metodlarý çaðýrýyor
        if (hostButton != null)
        {
            // Inspector'daki OnClick olaylarýný temizle
            hostButton.onClick.RemoveAllListeners();
            hostButton.onClick.AddListener(() => { PlayButtonSound(); CreateLobbyWithSound(); });
        }

        if (joinButton != null)
        {
            // Inspector'daki OnClick olaylarýný temizle
            joinButton.onClick.RemoveAllListeners();
            joinButton.onClick.AddListener(() => { PlayButtonSound(); JoinLobbyWithSound(); });
        }

        if (exitHostJoinButton != null)
            exitHostJoinButton.onClick.AddListener(() => { PlayButtonSound(); ExitHostJoinMenu(); });

        // Settings panel butonlarý
        if (backFromSettingsButton != null)
            backFromSettingsButton.onClick.AddListener(() => { PlayButtonSound(); BackFromSettings(); });

        if (saveSettingsButton != null)
            saveSettingsButton.onClick.AddListener(() => { PlayButtonSound(); SaveSettings(); });

        // Credits geri butonu
        if (backFromCreditsButton != null)
        {
            backFromCreditsButton.onClick.RemoveAllListeners();
            backFromCreditsButton.onClick.AddListener(() => { PlayButtonSound(); CloseCredits(); });
        }

        // Sosyal medya butonlarý
        if (discordButton != null)
            discordButton.onClick.AddListener(() => { PlayButtonSound(); OpenDiscord(); });

        if (twitterButton != null)
            twitterButton.onClick.AddListener(() => { PlayButtonSound(); OpenTwitter(); });

        if (instagramButton != null)
            instagramButton.onClick.AddListener(() => { PlayButtonSound(); OpenInstagram(); });
    }

    // ? YENÝ: Ses efekti ile lobi oluþturma
    void CreateLobbyWithSound()
    {
        if (steamManager != null)
        {
            Debug.Log("?? Host lobi oluþturuluyor (Steam)...");
            steamManager.HostLobby();
        }
        else
        {
            Debug.LogError("? SteamManager bulunamadý! Lobi oluþturulamýyor.");
        }
    }

    // ? YENÝ: Ses efekti ile lobiye katýlma
    void JoinLobbyWithSound()
    {
        if (steamManager != null)
        {
            Debug.Log("?? Lobiye katýlma iþlemi baþlatýlýyor (Steam)...");
            steamManager.JoinLobbyWithID();
        }
        else
        {
            Debug.LogError("? SteamManager bulunamadý! Lobiye katýlýnamýyor.");
        }
    }

    // Buton ses efekti metodu
    void PlayButtonSound()
    {
        if (buttonClickSound == null) return;

        // Ses seviyesini hesapla (settingsManager'dan alýnacak)
        float finalVolume = buttonSoundVolume;

        if (settingsManager != null)
        {
            finalVolume *= settingsManager.GetSFXVolume() * settingsManager.GetMasterVolume();
        }

        // AudioSource öncelik sýrasý: uiAudioSource -> sfxAudioSource -> Yeni AudioSource
        if (uiAudioSource != null)
        {
            uiAudioSource.PlayOneShot(buttonClickSound, finalVolume);
        }
        else if (sfxAudioSource != null)
        {
            sfxAudioSource.PlayOneShot(buttonClickSound, finalVolume);
        }
        else
        {
            // Geçici AudioSource oluþtur
            AudioSource.PlayClipAtPoint(buttonClickSound, Camera.main.transform.position, finalVolume);
        }
    }

    // Hover ses efekti metodu (opsiyonel)
    void PlayHoverSound()
    {
        if (buttonHoverSound == null) return;

        float finalVolume = buttonSoundVolume * 0.5f; // Hover sesi daha düþük

        if (settingsManager != null)
        {
            finalVolume *= settingsManager.GetSFXVolume() * settingsManager.GetMasterVolume();
        }

        if (uiAudioSource != null)
        {
            uiAudioSource.PlayOneShot(buttonHoverSound, finalVolume);
        }
        else if (sfxAudioSource != null)
        {
            sfxAudioSource.PlayOneShot(buttonHoverSound, finalVolume);
        }
    }

    void SetGameVersion()
    {
        if (versionText != null)
        {
            versionText.text = gameVersion;
        }
    }

    // PANEL YÖNETÝM SÝSTEMÝ
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

    // Ana Menü Fonksiyonlarý
    public void PlayOnline()
    {
        ShowHostJoinMenu();
    }

    public void PlayOffline()
    {
        Debug.Log("Offline oyun baþlatýlýyor...");
        if (!string.IsNullOrEmpty(mapSelectionSceneName))
        {
            SceneManager.LoadScene(mapSelectionSceneName);
        }
        else
        {
            Debug.LogWarning("Map Selection sahne ismi belirtilmemiþ!");
        }
    }

    // ?? ESKÝ FONKSÝYONLAR - Artýk kullanýlmýyor (geriye dönük uyumluluk için)
    public void CreateLobby()
    {
        Debug.LogWarning("?? CreateLobby() deprecated! CreateLobbyWithSound() kullanýlýyor.");
        CreateLobbyWithSound();
    }

    public void JoinLobby()
    {
        Debug.LogWarning("?? JoinLobby() deprecated! JoinLobbyWithSound() kullanýlýyor.");
        JoinLobbyWithSound();
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
        Debug.Log("Oyundan çýkýlýyor...");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Settings Panel Fonksiyonlarý
    public void BackFromSettings()
    {
        if (settingsManager != null && settingsManager.HasUnsavedChanges())
        {
            settingsManager.OnBackButtonPressed();
            Debug.Log("Ayarlar panelinden çýkýþ: Kaydedilmemiþ deðiþiklikler geri alýndý.");
        }

        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    public void SaveSettings()
    {
        if (settingsManager != null)
        {
            settingsManager.SaveAllSettings();
            Debug.Log("Ayarlar kaydedildi.");
        }

        Debug.Log("Tüm ayarlar kaydedildi");
    }

    // Credits Panel Fonksiyonlarý
    public void CloseCredits()
    {
        if (creditsPanel != null)
            creditsPanel.SetActive(false);
    }

    // ESKI FONKSÝYONLAR (Uyumluluk için)
    public void CreateRoom()
    {
        CreateLobbyWithSound();
    }

    public void JoinRoom()
    {
        JoinLobbyWithSound();
    }

    public void BackToMainMenu()
    {
        ShowMainMenu();
    }

    // Sosyal Medya Fonksiyonlarý
    public void OpenDiscord()
    {
        Debug.Log("Discord açýlýyor...");
        Application.OpenURL(discordURL);
    }

    public void OpenTwitter()
    {
        Debug.Log("Twitter açýlýyor...");
        Application.OpenURL(twitterURL);
    }

    public void OpenInstagram()
    {
        Debug.Log("Instagram açýlýyor...");
        Application.OpenURL(instagramURL);
    }

    // Yardýmcý Fonksiyonlar
    public void SetGameVersion(string newVersion)
    {
        gameVersion = newVersion;
        SetGameVersion();
    }

    // ESC tuþu ile panelleri kapatma
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (settingsPanel != null && settingsPanel.activeInHierarchy)
            {
                PlayButtonSound(); // ESC tuþu için de ses
                BackFromSettings();
            }
            else if (creditsPanel != null && creditsPanel.activeInHierarchy)
            {
                PlayButtonSound();
                CloseCredits();
            }
            else if (isHostJoinMenuActive)
            {
                PlayButtonSound();
                ExitHostJoinMenu();
            }
        }
    }

    void OnDestroy()
    {
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