using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine;
using Unity.Netcode;

public class Menu : MonoBehaviour
{
    [Header("Ana Men� Butonlar�")]
    public Button playOnlineButton;
    public Button playOfflineButton;
    public Button settingsButton;
    public Button creditsButton;
    public Button quitButton;

    [Header("Host/Join Men� Butonlar�")]
    public Button hostButton;
    public Button joinButton;
    public Button exitHostJoinButton;

    [Header("Sosyal Medya Butonlar�")]
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

    [Header("Sahne �simleri")]
    public string mapSelectionSceneName = "MapSelection";
    public string onlineRoomSceneName = "OnlineRoom";

    [Header("Settings Panel Butonlar�")]
    public Button backFromSettingsButton;
    public Button saveSettingsButton;

    [Header("Credits Panel Butonlar�")]
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

    [Header("Oyun Ayarlar�")]
    public string gameVersion = "v1.0.0";

    [Header("Settings Manager")]
    public UnifiedSettingsManager settingsManager;

    // ? YEN�: SteamManager referans�
    [Header("Steam Manager")]
    public SteamManager steamManager;

    // Panel durumlar�n� takip etmek i�in
    private bool isMainMenuActive = true;
    private bool isHostJoinMenuActive = false;

    void Start()
    {
        InitializeMenu();
        SetupButtonListeners();
        SetGameVersion();
        FindSettingsManager();
        FindSteamManager(); // ? YEN�
    }

    void FindSettingsManager()
    {
        if (settingsManager == null)
        {
            settingsManager = FindObjectOfType<UnifiedSettingsManager>();
        }
    }

    // ? YEN�: SteamManager'� otomatik bul
    void FindSteamManager()
    {
        if (steamManager == null)
        {
            steamManager = FindObjectOfType<SteamManager>();

            if (steamManager == null)
            {
                Debug.LogWarning("?? SteamManager bulunamad�! Online �zellikler �al��mayabilir.");
            }
        }
    }

    void InitializeMenu()
    {
        ShowMainMenu();
    }

    void SetupButtonListeners()
    {
        // Ana men� butonlar�
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

        // ? DE���T�R�LD�: Host/Join butonlar� art�k k�pr� metodlar� �a��r�yor
        if (hostButton != null)
        {
            // Inspector'daki OnClick olaylar�n� temizle
            hostButton.onClick.RemoveAllListeners();
            hostButton.onClick.AddListener(() => { PlayButtonSound(); CreateLobbyWithSound(); });
        }

        if (joinButton != null)
        {
            // Inspector'daki OnClick olaylar�n� temizle
            joinButton.onClick.RemoveAllListeners();
            joinButton.onClick.AddListener(() => { PlayButtonSound(); JoinLobbyWithSound(); });
        }

        if (exitHostJoinButton != null)
            exitHostJoinButton.onClick.AddListener(() => { PlayButtonSound(); ExitHostJoinMenu(); });

        // Settings panel butonlar�
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

        // Sosyal medya butonlar�
        if (discordButton != null)
            discordButton.onClick.AddListener(() => { PlayButtonSound(); OpenDiscord(); });

        if (twitterButton != null)
            twitterButton.onClick.AddListener(() => { PlayButtonSound(); OpenTwitter(); });

        if (instagramButton != null)
            instagramButton.onClick.AddListener(() => { PlayButtonSound(); OpenInstagram(); });
    }

    // ? YEN�: Ses efekti ile lobi olu�turma
    void CreateLobbyWithSound()
    {
        if (steamManager != null)
        {
            Debug.Log("?? Host lobi olu�turuluyor (Steam)...");
            steamManager.HostLobby();
        }
        else
        {
            Debug.LogError("? SteamManager bulunamad�! Lobi olu�turulam�yor.");
        }
    }

    // ? YEN�: Ses efekti ile lobiye kat�lma
    void JoinLobbyWithSound()
    {
        if (steamManager != null)
        {
            Debug.Log("?? Lobiye kat�lma i�lemi ba�lat�l�yor (Steam)...");
            steamManager.JoinLobbyWithID();
        }
        else
        {
            Debug.LogError("? SteamManager bulunamad�! Lobiye kat�l�nam�yor.");
        }
    }

    // Buton ses efekti metodu
    void PlayButtonSound()
    {
        if (buttonClickSound == null) return;

        // Ses seviyesini hesapla (settingsManager'dan al�nacak)
        float finalVolume = buttonSoundVolume;

        if (settingsManager != null)
        {
            finalVolume *= settingsManager.GetSFXVolume() * settingsManager.GetMasterVolume();
        }

        // AudioSource �ncelik s�ras�: uiAudioSource -> sfxAudioSource -> Yeni AudioSource
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
            // Ge�ici AudioSource olu�tur
            AudioSource.PlayClipAtPoint(buttonClickSound, Camera.main.transform.position, finalVolume);
        }
    }

    // Hover ses efekti metodu (opsiyonel)
    void PlayHoverSound()
    {
        if (buttonHoverSound == null) return;

        float finalVolume = buttonSoundVolume * 0.5f; // Hover sesi daha d���k

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

    // PANEL Y�NET�M S�STEM�
    void ShowMainMenu()
    {
        Debug.Log("Ana men� g�steriliyor");

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
        Debug.Log("Host/Join men�s� g�steriliyor");

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
        Debug.Log("Host/Join men�s�nden ana men�ye d�n�l�yor");
        ShowMainMenu();
    }

    void ShowSettingsPanel()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(true);

        if (creditsPanel != null)
            creditsPanel.SetActive(false);

        Debug.Log("Ayarlar paneli g�steriliyor");
    }

    void ShowCreditsPanel()
    {
        if (creditsPanel != null)
            creditsPanel.SetActive(true);

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        Debug.Log("Krediler paneli g�steriliyor");
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

    // Ana Men� Fonksiyonlar�
    public void PlayOnline()
    {
        ShowHostJoinMenu();
    }

    public void PlayOffline()
    {
        Debug.Log("Offline oyun ba�lat�l�yor...");
        if (!string.IsNullOrEmpty(mapSelectionSceneName))
        {
            SceneManager.LoadScene(mapSelectionSceneName);
        }
        else
        {
            Debug.LogWarning("Map Selection sahne ismi belirtilmemi�!");
        }
    }

    // ?? ESK� FONKS�YONLAR - Art�k kullan�lm�yor (geriye d�n�k uyumluluk i�in)
    public void CreateLobby()
    {
        Debug.LogWarning("?? CreateLobby() deprecated! CreateLobbyWithSound() kullan�l�yor.");
        CreateLobbyWithSound();
    }

    public void JoinLobby()
    {
        Debug.LogWarning("?? JoinLobby() deprecated! JoinLobbyWithSound() kullan�l�yor.");
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
        Debug.Log("Oyundan ��k�l�yor...");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Settings Panel Fonksiyonlar�
    public void BackFromSettings()
    {
        if (settingsManager != null && settingsManager.HasUnsavedChanges())
        {
            settingsManager.OnBackButtonPressed();
            Debug.Log("Ayarlar panelinden ��k��: Kaydedilmemi� de�i�iklikler geri al�nd�.");
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

        Debug.Log("T�m ayarlar kaydedildi");
    }

    // Credits Panel Fonksiyonlar�
    public void CloseCredits()
    {
        if (creditsPanel != null)
            creditsPanel.SetActive(false);
    }

    // ESKI FONKS�YONLAR (Uyumluluk i�in)
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

    // Sosyal Medya Fonksiyonlar�
    public void OpenDiscord()
    {
        Debug.Log("Discord a��l�yor...");
        Application.OpenURL(discordURL);
    }

    public void OpenTwitter()
    {
        Debug.Log("Twitter a��l�yor...");
        Application.OpenURL(twitterURL);
    }

    public void OpenInstagram()
    {
        Debug.Log("Instagram a��l�yor...");
        Application.OpenURL(instagramURL);
    }

    // Yard�mc� Fonksiyonlar
    public void SetGameVersion(string newVersion)
    {
        gameVersion = newVersion;
        SetGameVersion();
    }

    // ESC tu�u ile panelleri kapatma
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (settingsPanel != null && settingsPanel.activeInHierarchy)
            {
                PlayButtonSound(); // ESC tu�u i�in de ses
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