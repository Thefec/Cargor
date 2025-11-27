using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Ana menü sistemi - Panel yönetimi, buton etkileşimleri, sosyal medya bağlantıları ve ses efektlerini yönetir.
/// </summary>
public class Menu : MonoBehaviour
{
    #region Constants

    private const string LOG_PREFIX = "[MainMenu]";
    private const float HOVER_SOUND_VOLUME_MULTIPLIER = 0.5f;

    // Scene Names
    private const string SCENE_MAP_SELECTION = "MapSelection";
    private const string SCENE_TUTORIAL = "Tutorial";

    #endregion

    #region Enums

    private enum MenuState
    {
        MainMenu,
        HostJoinMenu,
        Settings,
        Credits
    }

    #endregion

    #region Serialized Fields - Main Menu Buttons

    [Header("=== ANA MENÜ BUTONLARI ===")]
    [SerializeField, Tooltip("Online oyna butonu")]
    public Button playOnlineButton;

    [SerializeField, Tooltip("Offline oyna butonu")]
    public Button playOfflineButton;

    [SerializeField, Tooltip("Tutorial butonu")]
    public Button tutorialButton;

    [SerializeField, Tooltip("Ayarlar butonu")]
    public Button settingsButton;

    [SerializeField, Tooltip("Krediler butonu")]
    public Button creditsButton;

    [SerializeField, Tooltip("Çıkış butonu")]
    public Button quitButton;

    #endregion

    #region Serialized Fields - Host/Join Buttons

    [Header("=== HOST/JOIN MENÜ BUTONLARI ===")]
    [SerializeField, Tooltip("Host butonu")]
    public Button hostButton;

    [SerializeField, Tooltip("Join butonu")]
    public Button joinButton;

    [SerializeField, Tooltip("Geri butonu")]
    public Button exitHostJoinButton;

    #endregion

    #region Serialized Fields - Social Media Buttons

    [Header("=== SOSYAL MEDYA BUTONLARI ===")]
    [SerializeField, Tooltip("Discord butonu")]
    public Button discordButton;

    [SerializeField, Tooltip("Steam sayfası butonu")]
    public Button steamPageButton;

    [SerializeField, Tooltip("Instagram butonu")]
    public Button instagramButton;

    #endregion

    #region Serialized Fields - UI Panels

    [Header("=== UI PANELLERİ ===")]
    [SerializeField, Tooltip("Ana menü paneli")]
    public GameObject mainMenuPanel;

    [SerializeField, Tooltip("Host/Join paneli")]
    public GameObject hostJoinPanel;

    [SerializeField, Tooltip("Ayarlar paneli")]
    public GameObject settingsPanel;

    [SerializeField, Tooltip("Krediler paneli")]
    public GameObject creditsPanel;

    #endregion

    #region Serialized Fields - Settings/Credits Panel Buttons

    [Header("=== PANEL BUTONLARI ===")]
    [SerializeField, Tooltip("Ayarlardan geri butonu")]
    public Button backFromSettingsButton;

    [SerializeField, Tooltip("Ayarları kaydet butonu")]
    public Button saveSettingsButton;

    [SerializeField, Tooltip("Kredilerden geri butonu")]
    public Button backFromCreditsButton;

    #endregion

    #region Serialized Fields - Version & URLs

    [Header("=== VERSİYON & BAĞLANTILAR ===")]
    [SerializeField, Tooltip("Versiyon text'i")]
    public TextMeshProUGUI versionText;

    [SerializeField, Tooltip("Oyun versiyonu")]
    public string gameVersion = "v1.0. 0";

    [Header("=== SOSYAL MEDYA LİNKLERİ ===")]
    [SerializeField, Tooltip("Discord URL")]
    public string discordURL = "https://discord.gg/yourdiscord";

    [SerializeField, Tooltip("Steam sayfa URL")]
    public string steamPageURL = "https://store. steampowered.com/app/YOURAPPID";

    [SerializeField, Tooltip("Instagram URL")]
    public string instagramURL = "https://instagram.com/youraccount";

    #endregion

    #region Serialized Fields - Audio

    [Header("=== AUDIO SOURCES ===")]
    [SerializeField, Tooltip("Müzik AudioSource")]
    public AudioSource musicAudioSource;

    [SerializeField, Tooltip("SFX AudioSource")]
    public AudioSource sfxAudioSource;

    [SerializeField, Tooltip("UI AudioSource")]
    public AudioSource uiAudioSource;

    [Header("=== UI SES EFEKTLERİ ===")]
    [SerializeField, Tooltip("Buton tıklama sesi")]
    public AudioClip buttonClickSound;

    [SerializeField, Tooltip("Buton hover sesi")]
    public AudioClip buttonHoverSound;

    [SerializeField, Range(0f, 1f), Tooltip("Buton ses seviyesi")]
    public float buttonSoundVolume = 1f;

    #endregion

    #region Serialized Fields - Managers

    [Header("=== MANAGER REFERANSLARI ===")]
    [SerializeField, Tooltip("Ayarlar manager'ı")]
    public UnifiedSettingsManager settingsManager;

    [SerializeField, Tooltip("Steam manager'ı")]
    public SteamManager steamManager;

    #endregion

    #region Private Fields

    private MenuState _currentState = MenuState.MainMenu;

    #endregion

    #region Public Properties

    /// <summary>
    /// Ana menü aktif mi?  (backward compatibility)
    /// </summary>
    public bool isMainMenuActive => _currentState == MenuState.MainMenu;

    /// <summary>
    /// Host/Join menüsü aktif mi? (backward compatibility)
    /// </summary>
    public bool isHostJoinMenuActive => _currentState == MenuState.HostJoinMenu;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        Initialize();
    }

    private void Update()
    {
        HandleEscapeInput();
    }

    private void OnDestroy()
    {
        RemoveAllButtonListeners();
    }

    #endregion

    #region Initialization

    private void Initialize()
    {
        FindManagers();
        SetupAllButtonListeners();
        UpdateVersionText();
        TransitionToState(MenuState.MainMenu);
    }

    private void FindManagers()
    {
        FindSettingsManager();
        FindSteamManager();
    }

    private void FindSettingsManager()
    {
        if (settingsManager != null) return;

        settingsManager = FindObjectOfType<UnifiedSettingsManager>();
    }

    private void FindSteamManager()
    {
        if (steamManager != null) return;

        steamManager = FindObjectOfType<SteamManager>();

        if (steamManager == null)
        {
            Debug.LogWarning($"{LOG_PREFIX} ⚠️ SteamManager bulunamadı!  Online özellikler çalışmayabilir.");
        }
    }

    #endregion

    #region Button Setup

    private void SetupAllButtonListeners()
    {
        SetupMainMenuButtons();
        SetupHostJoinButtons();
        SetupPanelButtons();
        SetupSocialMediaButtons();
    }

    private void SetupMainMenuButtons()
    {
        SetupButton(playOnlineButton, () => TransitionToState(MenuState.HostJoinMenu));
        SetupButton(playOfflineButton, PlayOffline);
        SetupButton(tutorialButton, PlayTutorial);
        SetupButton(settingsButton, () => TransitionToState(MenuState.Settings));
        SetupButton(creditsButton, () => TransitionToState(MenuState.Credits));
        SetupButton(quitButton, QuitGame);
    }

    private void SetupHostJoinButtons()
    {
        SetupButtonWithClear(hostButton, ExecuteHostLobby);
        SetupButtonWithClear(joinButton, ExecuteJoinLobby);
        SetupButton(exitHostJoinButton, () => TransitionToState(MenuState.MainMenu));
    }

    private void SetupPanelButtons()
    {
        SetupButton(backFromSettingsButton, BackFromSettings);
        SetupButton(saveSettingsButton, SaveSettings);
        SetupButtonWithClear(backFromCreditsButton, CloseCredits);
    }

    private void SetupSocialMediaButtons()
    {
        SetupButton(discordButton, OpenDiscord);
        SetupButton(steamPageButton, OpenSteamPage);
        SetupButton(instagramButton, OpenInstagram);
    }

    private void SetupButton(Button button, System.Action action)
    {
        if (button == null) return;

        button.onClick.AddListener(() =>
        {
            PlayButtonSound();
            action?.Invoke();
        });
    }

    private void SetupButtonWithClear(Button button, System.Action action)
    {
        if (button == null) return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            PlayButtonSound();
            action?.Invoke();
        });
    }

    #endregion

    #region State Management

    private void TransitionToState(MenuState newState)
    {
        _currentState = newState;

        switch (newState)
        {
            case MenuState.MainMenu:
                ShowMainMenuPanel();
                break;
            case MenuState.HostJoinMenu:
                ShowHostJoinPanel();
                break;
            case MenuState.Settings:
                ShowSettingsPanel();
                break;
            case MenuState.Credits:
                ShowCreditsPanel();
                break;
        }

        Debug.Log($"{LOG_PREFIX} State changed to: {newState}");
    }

    #endregion

    #region Panel Management

    private void ShowMainMenuPanel()
    {
        SetPanelActive(mainMenuPanel, true);
        SetPanelActive(hostJoinPanel, false);
        CloseAllOverlayPanels();
    }

    private void ShowHostJoinPanel()
    {
        SetPanelActive(mainMenuPanel, false);
        SetPanelActive(hostJoinPanel, true);
        CloseAllOverlayPanels();
    }

    private void ShowSettingsPanel()
    {
        SetPanelActive(settingsPanel, true);
        SetPanelActive(creditsPanel, false);
    }

    private void ShowCreditsPanel()
    {
        SetPanelActive(creditsPanel, true);
        SetPanelActive(settingsPanel, false);
    }

    private void CloseAllOverlayPanels()
    {
        SetPanelActive(settingsPanel, false);
        SetPanelActive(creditsPanel, false);
    }

    private void HideAllPanels()
    {
        SetPanelActive(mainMenuPanel, false);
        SetPanelActive(hostJoinPanel, false);
        CloseAllOverlayPanels();
    }

    private static void SetPanelActive(GameObject panel, bool active)
    {
        if (panel != null)
        {
            panel.SetActive(active);
        }
    }

    #endregion

    #region Lobby Operations

    private void ExecuteHostLobby()
    {
        if (steamManager != null)
        {
            Debug.Log($"{LOG_PREFIX} 🎮 Host lobi oluşturuluyor (Steam)...");
            steamManager.HostLobby();
        }
        else
        {
            Debug.LogError($"{LOG_PREFIX} ❌ SteamManager bulunamadı! Lobi oluşturulamıyor.");
        }
    }

    private void ExecuteJoinLobby()
    {
        if (steamManager != null)
        {
            Debug.Log($"{LOG_PREFIX} 🎮 Lobiye katılma işlemi başlatılıyor (Steam)...");
            steamManager.JoinLobbyWithID();
        }
        else
        {
            Debug.LogError($"{LOG_PREFIX} ❌ SteamManager bulunamadı! Lobiye katılınamıyor.");
        }
    }

    #endregion

    #region Audio

    private void PlayButtonSound()
    {
        PlaySound(buttonClickSound, buttonSoundVolume);
    }

    private void PlayHoverSound()
    {
        PlaySound(buttonHoverSound, buttonSoundVolume * HOVER_SOUND_VOLUME_MULTIPLIER);
    }

    private void PlaySound(AudioClip clip, float baseVolume)
    {
        if (clip == null) return;

        float finalVolume = CalculateFinalVolume(baseVolume);
        PlaySoundOnAvailableSource(clip, finalVolume);
    }

    private float CalculateFinalVolume(float baseVolume)
    {
        float volume = baseVolume;

        if (settingsManager != null)
        {
            volume *= settingsManager.GetSFXVolume() * settingsManager.GetMasterVolume();
        }

        return volume;
    }

    private void PlaySoundOnAvailableSource(AudioClip clip, float volume)
    {
        if (uiAudioSource != null)
        {
            uiAudioSource.PlayOneShot(clip, volume);
        }
        else if (sfxAudioSource != null)
        {
            sfxAudioSource.PlayOneShot(clip, volume);
        }
        else if (Camera.main != null)
        {
            AudioSource.PlayClipAtPoint(clip, Camera.main.transform.position, volume);
        }
    }

    #endregion

    #region Input Handling

    private void HandleEscapeInput()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        PlayButtonSound();

        switch (_currentState)
        {
            case MenuState.Settings:
                BackFromSettings();
                break;

            case MenuState.Credits:
                CloseCredits();
                break;

            case MenuState.HostJoinMenu:
                TransitionToState(MenuState.MainMenu);
                break;
        }
    }

    #endregion

    #region Public API - Menu Actions

    /// <summary>
    /// Ana menüyü gösterir
    /// </summary>
    public void ShowMainMenu()
    {
        TransitionToState(MenuState.MainMenu);
    }

    /// <summary>
    /// Host/Join menüsünü gösterir
    /// </summary>
    public void ShowHostJoinMenu()
    {
        TransitionToState(MenuState.HostJoinMenu);
    }

    /// <summary>
    /// Host/Join menüsünden çıkar
    /// </summary>
    public void ExitHostJoinMenu()
    {
        Debug.Log($"{LOG_PREFIX} Host/Join menüsünden ana menüye dönülüyor");
        TransitionToState(MenuState.MainMenu);
    }

    /// <summary>
    /// Online oyun menüsünü açar
    /// </summary>
    public void PlayOnline()
    {
        TransitionToState(MenuState.HostJoinMenu);
    }

    /// <summary>
    /// Offline oyunu başlatır
    /// </summary>
    public void PlayOffline()
    {
        Debug.Log($"{LOG_PREFIX} Offline oyun başlatılıyor...");
        SceneManager.LoadScene(SCENE_MAP_SELECTION);
    }

    /// <summary>
    /// Tutorial'ı başlatır
    /// </summary>
    public void PlayTutorial()
    {
        Debug.Log($"{LOG_PREFIX} Tutorial seviyesi yükleniyor...");
        SceneManager.LoadScene(SCENE_TUTORIAL);
    }

    /// <summary>
    /// Ayarları açar
    /// </summary>
    public void OpenSettings()
    {
        TransitionToState(MenuState.Settings);
    }

    /// <summary>
    /// Kredileri açar
    /// </summary>
    public void OpenCredits()
    {
        TransitionToState(MenuState.Credits);
    }

    /// <summary>
    /// Oyundan çıkar
    /// </summary>
    public void QuitGame()
    {
        Debug.Log($"{LOG_PREFIX} Oyundan çıkılıyor...");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    #endregion

    #region Public API - Settings

    /// <summary>
    /// Ayarlardan geri döner
    /// </summary>
    public void BackFromSettings()
    {
        if (settingsManager != null && settingsManager.HasUnsavedChanges())
        {
            settingsManager.OnBackButtonPressed();
            Debug.Log($"{LOG_PREFIX} Ayarlar panelinden çıkış: Kaydedilmemiş değişiklikler geri alındı.");
        }

        SetPanelActive(settingsPanel, false);

        // Ana state'e göre geri dön
        if (_currentState == MenuState.Settings)
        {
            _currentState = MenuState.MainMenu;
        }
    }

    /// <summary>
    /// Ayarları kaydeder
    /// </summary>
    public void SaveSettings()
    {
        if (settingsManager != null)
        {
            settingsManager.SaveAllSettings();
            Debug.Log($"{LOG_PREFIX} Tüm ayarlar kaydedildi.");
        }
    }

    #endregion

    #region Public API - Credits

    /// <summary>
    /// Krediler panelini kapatır
    /// </summary>
    public void CloseCredits()
    {
        SetPanelActive(creditsPanel, false);

        if (_currentState == MenuState.Credits)
        {
            _currentState = MenuState.MainMenu;
        }
    }

    #endregion

    #region Public API - Social Media

    /// <summary>
    /// Discord'u açar
    /// </summary>
    public void OpenDiscord()
    {
        Debug.Log($"{LOG_PREFIX} Discord açılıyor...");
        Application.OpenURL(discordURL);
    }

    /// <summary>
    /// Steam sayfasını açar
    /// </summary>
    public void OpenSteamPage()
    {
        Debug.Log($"{LOG_PREFIX} Steam sayfası açılıyor...");
        Application.OpenURL(steamPageURL);
    }

    /// <summary>
    /// Instagram'ı açar
    /// </summary>
    public void OpenInstagram()
    {
        Debug.Log($"{LOG_PREFIX} Instagram açılıyor...");
        Application.OpenURL(instagramURL);
    }

    #endregion

    #region Public API - Version

    /// <summary>
    /// Versiyon text'ini günceller
    /// </summary>
    public void SetGameVersion(string newVersion)
    {
        gameVersion = newVersion;
        UpdateVersionText();
    }

    private void UpdateVersionText()
    {
        if (versionText != null)
        {
            versionText.text = gameVersion;
        }
    }

    #endregion

    #region Public API - Backward Compatibility

    /// <summary>
    /// Lobi oluşturur (deprecated - ExecuteHostLobby kullanın)
    /// </summary>
    [System.Obsolete("CreateLobby is deprecated. Use ExecuteHostLobby instead.")]
    public void CreateLobby()
    {
        Debug.LogWarning($"{LOG_PREFIX} ⚠️ CreateLobby() deprecated!  ExecuteHostLobby() kullanılıyor.");
        ExecuteHostLobby();
    }

    /// <summary>
    /// Lobiye katılır (deprecated - ExecuteJoinLobby kullanın)
    /// </summary>
    [System.Obsolete("JoinLobby is deprecated. Use ExecuteJoinLobby instead.")]
    public void JoinLobby()
    {
        Debug.LogWarning($"{LOG_PREFIX} ⚠️ JoinLobby() deprecated! ExecuteJoinLobby() kullanılıyor.");
        ExecuteJoinLobby();
    }

    /// <summary>
    /// Oda oluşturur (deprecated - backward compatibility)
    /// </summary>
    public void CreateRoom()
    {
        ExecuteHostLobby();
    }

    /// <summary>
    /// Odaya katılır (deprecated - backward compatibility)
    /// </summary>
    public void JoinRoom()
    {
        ExecuteJoinLobby();
    }

    /// <summary>
    /// Ana menüye döner
    /// </summary>
    public void BackToMainMenu()
    {
        TransitionToState(MenuState.MainMenu);
    }

    /// <summary>
    /// Ses ile lobi oluşturur (internal)
    /// </summary>
    private void CreateLobbyWithSound()
    {
        ExecuteHostLobby();
    }

    /// <summary>
    /// Ses ile lobiye katılır (internal)
    /// </summary>
    private void JoinLobbyWithSound()
    {
        ExecuteJoinLobby();
    }

    #endregion

    #region Cleanup

    private void RemoveAllButtonListeners()
    {
        RemoveButtonListener(playOnlineButton);
        RemoveButtonListener(playOfflineButton);
        RemoveButtonListener(tutorialButton);
        RemoveButtonListener(settingsButton);
        RemoveButtonListener(creditsButton);
        RemoveButtonListener(quitButton);
        RemoveButtonListener(hostButton);
        RemoveButtonListener(joinButton);
        RemoveButtonListener(exitHostJoinButton);
        RemoveButtonListener(backFromSettingsButton);
        RemoveButtonListener(saveSettingsButton);
        RemoveButtonListener(backFromCreditsButton);
        RemoveButtonListener(discordButton);
        RemoveButtonListener(steamPageButton);
        RemoveButtonListener(instagramButton);
    }

    private static void RemoveButtonListener(Button button)
    {
        button?.onClick.RemoveAllListeners();
    }



    #endregion
}