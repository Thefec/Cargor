using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Ana menü sistemi.
///
/// ÖNEMLİ: Bu script MainMenuPanel üzerindedir.
/// mainMenuPanel asla SetActive(false) YAPILMAZ — aksi hâlde coroutine'ler durur.
///
/// Hiyerarşi:
///   MainMenuPanel → mainMenuSlidePanel (butonlar stagger ile soldan gelir)
///   HAJ           → hajBlackBG (instant) | hajBackButton (instant) | hostOrJoinPanel (top→down)
///   J             → jBlackBG   (instant) | jBackButton   (instant) | joinPanel       (top→down)
/// </summary>
public class Menu : MonoBehaviour
{
    #region Constants
    private const string LOG_PREFIX  = "[MainMenu]";
    private const float  SLIDE_DUR   = 0.35f;
    private const float  STAGGER_DLY = 0.08f;
    private const string SCENE_MAP   = "MapSelection";
    private const string SCENE_TUT   = "Tutorial";
    #endregion

    #region Enums
    private enum MenuState { MainMenu, HostJoinMenu, JoinRoomPanel, Settings, Credits, TutorialConfirm }
    #endregion

    // ── Inspector ────────────────────────────────────────────

    #region Main Menu
    [Header("=== ANA MENÜ ===")]
    [SerializeField] public GameObject    mainMenuPanel;
    [SerializeField] public RectTransform mainMenuSlidePanel;  // Buttons parent (VerticalLayoutGroup'lu)
    [SerializeField] public Button hostGameButton;
    [SerializeField] public Button tutorialButton;
    [SerializeField] public Button settingsButton;
    [SerializeField] public Button creditsButton;
    [SerializeField] public Button quitButton;
    #endregion

    #region HAJ Group
    [Header("=== HAJ GRUBU ===")]
    [SerializeField] public GameObject    hajGroup;
    [SerializeField] public GameObject    hajBlackBG;
    [SerializeField] public Button        hajBackButton;
    [SerializeField] public RectTransform hostOrJoinPanel;
    [SerializeField] public Button createRoomButton;
    [SerializeField] public Button joinRoomButton;
    #endregion

    #region J Group
    [Header("=== J GRUBU ===")]
    [SerializeField] public GameObject    jGroup;
    [SerializeField] public GameObject    jBlackBG;
    [SerializeField] public Button        jBackButton;
    [SerializeField] public RectTransform joinPanel;
    [SerializeField] public TMP_InputField roomCodeInputField;
    [SerializeField] public Button confirmEntryButton;
    #endregion

    #region Overlays
    [Header("=== OVERLAY PANELLERİ ===")]
    [SerializeField] public GameObject tutorialConfirmPanel;
    [SerializeField] public Button tutorialConfirmYesButton;
    [SerializeField] public Button tutorialConfirmNoButton;
    [SerializeField] public GameObject settingsPanel;
    [SerializeField] public GameObject creditsPanel;
    [SerializeField] public Button backFromSettingsButton;
    [SerializeField] public Button saveSettingsButton;
    [SerializeField] public Button backFromCreditsButton;
    #endregion

    #region Social / Version / Audio / Managers
    [Header("=== SOSYAL MEDYA ===")]
    [SerializeField] public Button discordButton;
    [SerializeField] public Button steamPageButton;
    [SerializeField] public Button instagramButton;

    [Header("=== VERSİYON ===")]
    [SerializeField] public TextMeshProUGUI versionText;
    [SerializeField] public string gameVersion  = "v1.0.0";
    [SerializeField] public string discordURL   = "https://discord.gg/yourdiscord";
    [SerializeField] public string steamPageURL = "https://store.steampowered.com/app/YOURAPPID";
    [SerializeField] public string instagramURL = "https://instagram.com/youraccount";

    [Header("=== AUDIO ===")]
    [SerializeField] public AudioSource musicAudioSource;
    [SerializeField] public AudioSource sfxAudioSource;
    [SerializeField] public AudioSource uiAudioSource;
    [SerializeField] public AudioClip   buttonClickSound;
    [SerializeField] public AudioClip   buttonHoverSound;
    [SerializeField, Range(0f, 1f)] public float buttonSoundVolume = 1f;

    [Header("=== MANAGERS ===")]
    [SerializeField] public UnifiedSettingsManager settingsManager;
    [SerializeField] public SteamManager           steamManager;
    #endregion

    // ── Private ───────────────────────────────────────────────

    #region Private Fields
    private MenuState _state = MenuState.MainMenu;
    private Coroutine _slideCoroutine;
    private Coroutine _staggerCoroutine;

    // Cached rest pozisyonlar (Start'ta, herhangi bir SetActive'den önce alınır)
    private Vector2 _slidePanelRestPos;   // mainMenuSlidePanel container'ının rest pos'u
    private Vector2 _hajRestPos;
    private Vector2 _jRestPos;
    #endregion

    #region Properties
    public bool isMainMenuActive     => _state == MenuState.MainMenu;
    public bool isHostJoinMenuActive => _state == MenuState.HostJoinMenu;
    #endregion

    // ── Unity Lifecycle ───────────────────────────────────────

    private void Start()
    {
        FindManagers();
        CacheRestPositions();
        SetupButtons();
        UpdateVersionText();
        InitPanels();
        GoTo(MenuState.MainMenu);
    }

    private void Update()    { HandleEscape(); }
    private void OnDestroy() { RemoveListeners(); }

    // ── Init ─────────────────────────────────────────────────

    private void FindManagers()
    {
        if (settingsManager == null) settingsManager = FindObjectOfType<UnifiedSettingsManager>();
        if (steamManager    == null)
        {
            steamManager = FindObjectOfType<SteamManager>();
            if (steamManager == null) Debug.LogWarning($"{LOG_PREFIX} SteamManager bulunamadı!");
        }
    }

    private void CacheRestPositions()
    {
        // Container'ların kendi pozisyonlarını sakla.
        // Buton çocukları cache'lenmez — LayoutGroup Start()'tan sonra hesaplar,
        // doğru pozisyonlar StaggerCoroutine içinde yield return null'dan sonra okunur.
        if (mainMenuSlidePanel != null) _slidePanelRestPos = mainMenuSlidePanel.anchoredPosition;
        if (hostOrJoinPanel    != null) _hajRestPos        = hostOrJoinPanel.anchoredPosition;
        if (joinPanel          != null) _jRestPos          = joinPanel.anchoredPosition;
    }

    private void InitPanels()
    {
        // mainMenuPanel asla SetActive(false) yapılmaz (Menu script burada).
        // Container'ı ekranın soluna taşı — görünmez ama gameobject aktif kalır.
        HideMainMenuInstant();

        // HAJ: panel off-screen üst, grup kapalı
        SetOffscreenTop(hostOrJoinPanel, _hajRestPos);
        Go(hajGroup, false);

        // J: panel off-screen üst, grup kapalı
        SetOffscreenTop(joinPanel, _jRestPos);
        Go(jGroup, false);

        // Overlay'ler
        Go(settingsPanel,        false);
        Go(creditsPanel,         false);
        Go(tutorialConfirmPanel, false);
    }

    // ── Button Setup ──────────────────────────────────────────

    private void SetupButtons()
    {
        Btn(hostGameButton,           () => GoTo(MenuState.HostJoinMenu));
        Btn(tutorialButton,           () => GoTo(MenuState.TutorialConfirm));
        Btn(settingsButton,           () => GoTo(MenuState.Settings));
        Btn(creditsButton,            () => GoTo(MenuState.Credits));
        Btn(quitButton,               QuitGame);
        Btn(hajBackButton,            () => GoTo(MenuState.MainMenu));
        Btn(createRoomButton,         ExecuteCreateRoom);
        Btn(joinRoomButton,           () => GoTo(MenuState.JoinRoomPanel));
        Btn(jBackButton,              () => GoTo(MenuState.HostJoinMenu));
        Btn(confirmEntryButton,       ExecuteConfirmEntry);
        Btn(tutorialConfirmYesButton, ConfirmTutorial);
        Btn(tutorialConfirmNoButton,  CancelTutorial);
        Btn(backFromSettingsButton,   BackFromSettings);
        Btn(saveSettingsButton,       SaveSettings);
        Btn(backFromCreditsButton,    CloseCredits);
        Btn(discordButton,            OpenDiscord);
        Btn(steamPageButton,          OpenSteamPage);
        Btn(instagramButton,          OpenInstagram);
    }

    private void Btn(Button b, System.Action a)
    {
        if (b == null) return;
        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(() => { PlayButtonSound(); a?.Invoke(); });
    }

    // ── State Machine ─────────────────────────────────────────

    private void GoTo(MenuState next)
    {
        MenuState prev = _state;
        _state = next;
        switch (next)
        {
            case MenuState.MainMenu:        ShowMainMenu(prev);   break;
            case MenuState.HostJoinMenu:    ShowHAJ(prev);        break;
            case MenuState.JoinRoomPanel:   ShowJoin();           break;
            case MenuState.Settings:        ShowOverlay(settingsPanel);        break;
            case MenuState.Credits:         ShowOverlay(creditsPanel);         break;
            case MenuState.TutorialConfirm: ShowOverlay(tutorialConfirmPanel); break;
        }
        Debug.Log($"{LOG_PREFIX} {prev} → {next}");
    }

    // ── Panel Show / Hide ─────────────────────────────────────

    // ─ MAIN MENU ─────────────────────────────────────────────

    private void ShowMainMenu(MenuState prev)
    {
        CloseOverlays();

        if (prev == MenuState.HostJoinMenu)
        {
            SlideOut(hostOrJoinPanel, _hajRestPos, () =>
            {
                Go(hajGroup, false);
                StartStaggeredMainMenu();
            });
        }
        else if (prev == MenuState.JoinRoomPanel)
        {
            SlideOut(joinPanel, _jRestPos, () =>
            {
                Go(jGroup,   false);
                Go(hajGroup, false);
                StartStaggeredMainMenu();
            });
        }
        else
        {
            StartStaggeredMainMenu();
        }
    }

    // ─ HAJ ───────────────────────────────────────────────────

    private void ShowHAJ(MenuState prev)
    {
        CloseOverlays();

        if (prev == MenuState.JoinRoomPanel)
        {
            SlideOut(joinPanel, _jRestPos, () =>
            {
                Go(jGroup, false);
                ActivateHAJGroup();
            });
            return;
        }

        HideMainMenuInstant();
        ActivateHAJGroup();
    }

    private void ActivateHAJGroup()
    {
        SetOffscreenTop(hostOrJoinPanel, _hajRestPos);
        Go(hajGroup,   true);
        Go(hajBlackBG, true);
        if (hajBackButton != null) hajBackButton.gameObject.SetActive(true);
        SlideIn(hostOrJoinPanel, _hajRestPos);
    }

    // ─ JOIN ROOM ─────────────────────────────────────────────

    private void ShowJoin()
    {
        if (roomCodeInputField != null) roomCodeInputField.text = "";

        SlideOut(hostOrJoinPanel, _hajRestPos, () =>
        {
            Go(hajBlackBG, false);
            if (hajBackButton != null) hajBackButton.gameObject.SetActive(false);
            Go(hajGroup, false);

            SetOffscreenTop(joinPanel, _jRestPos);
            Go(jGroup,   true);
            Go(jBlackBG, true);
            if (jBackButton != null) jBackButton.gameObject.SetActive(true);
            SlideIn(joinPanel, _jRestPos);
        });
    }

    // ─ OVERLAYS ──────────────────────────────────────────────

    private void ShowOverlay(GameObject panel)
    {
        Go(settingsPanel,        false);
        Go(creditsPanel,         false);
        Go(tutorialConfirmPanel, false);
        Go(panel, true);
    }

    private void CloseOverlays()
    {
        Go(settingsPanel,        false);
        Go(creditsPanel,         false);
        Go(tutorialConfirmPanel, false);
    }

    // ── Ana Menü Stagger Animasyonu ───────────────────────────

    /// <summary>
    /// mainMenuSlidePanel container'ını ekranın soluna taşır (instant).
    /// SetActive kullanılmaz. LayoutGroup aktif kalır.
    /// </summary>
    private void HideMainMenuInstant()
    {
        if (mainMenuSlidePanel == null) return;

        var lg = mainMenuSlidePanel.GetComponent<LayoutGroup>();
        if (lg != null) lg.enabled = true;

        mainMenuSlidePanel.anchoredPosition = new Vector2(_slidePanelRestPos.x - 9999f, _slidePanelRestPos.y);
    }

    private void StartStaggeredMainMenu()
    {
        if (_staggerCoroutine != null) StopCoroutine(_staggerCoroutine);
        _staggerCoroutine = StartCoroutine(StaggerCoroutine());
    }

    /// <summary>
    /// Butonları soldan sağa, yukarıdan aşağıya stagger ile getirir.
    ///   1. Container rest pozisyonuna alınır
    ///   2. yield return null → LayoutGroup çocukların GERÇEK pozisyonlarını hesaplar
    ///   3. LayoutGroup kapatılır (animasyon süresince manuel kontrol)
    ///   4. Butonlar sola alınır, sırayla slide-in başlar
    ///   5. Animasyon biter → LayoutGroup geri açılır
    /// </summary>
    private IEnumerator StaggerCoroutine()
    {
        if (mainMenuSlidePanel == null) yield break;

        // 1. Container'ı doğru konuma al
        mainMenuSlidePanel.anchoredPosition = _slidePanelRestPos;

        // 2. Bir frame bekle — LayoutGroup bu sürede çocukları düzenler
        yield return null;

        // 3. Aktif çocukların layout tarafından hesaplanmış GERÇEK pozisyonlarını oku
        var items = new List<(RectTransform rt, Vector2 rest)>();
        for (int i = 0; i < mainMenuSlidePanel.childCount; i++)
        {
            var child = mainMenuSlidePanel.GetChild(i) as RectTransform;
            if (child != null && child.gameObject.activeSelf)
                items.Add((child, child.anchoredPosition));
        }

        if (items.Count == 0) yield break;

        float canvasW = GetCanvasW();
        if (canvasW <= 0f) canvasW = Screen.width;

        // 4. LayoutGroup'u kapat
        var lg = mainMenuSlidePanel.GetComponent<LayoutGroup>();
        if (lg != null) lg.enabled = false;

        // 5. Tüm butonları ekranın soluna taşı
        foreach (var (rt, rest) in items)
            rt.anchoredPosition = new Vector2(rest.x - canvasW, rest.y);

        // 6. Her butonu sırayla slide-in başlat (yukarıdan aşağıya)
        foreach (var (rt, rest) in items)
        {
            StartCoroutine(SlideCoroutine(rt, rt.anchoredPosition, rest, null));
            yield return new WaitForSecondsRealtime(STAGGER_DLY);
        }

        // 7. Son buton animasyonu bitince LayoutGroup'u geri aç
        yield return new WaitForSecondsRealtime(SLIDE_DUR + 0.05f);
        if (lg != null) lg.enabled = true;
        _staggerCoroutine = null;
    }

    // ── Dikey Slide (HAJ / J) ─────────────────────────────────

    private void SetOffscreenTop(RectTransform panel, Vector2 rest)
    {
        if (panel == null) return;
        panel.anchoredPosition = new Vector2(rest.x, rest.y + GetCanvasH());
    }

    private void SlideIn(RectTransform panel, Vector2 rest)
    {
        if (panel == null) return;
        KickCoroutine(SlideCoroutine(panel, panel.anchoredPosition, rest, null));
    }

    private void SlideOut(RectTransform panel, Vector2 rest, System.Action onComplete = null)
    {
        if (panel == null || !panel.gameObject.activeInHierarchy)
        {
            onComplete?.Invoke();
            return;
        }
        Vector2 exit = new Vector2(rest.x, rest.y + GetCanvasH());
        KickCoroutine(SlideCoroutine(panel, rest, exit, onComplete));
    }

    // ── Coroutine Core ────────────────────────────────────────

    private void KickCoroutine(IEnumerator routine)
    {
        if (_slideCoroutine != null) StopCoroutine(_slideCoroutine);
        _slideCoroutine = StartCoroutine(routine);
    }

    private IEnumerator SlideCoroutine(RectTransform rect, Vector2 from, Vector2 to, System.Action onComplete)
    {
        float elapsed = 0f;
        while (elapsed < SLIDE_DUR)
        {
            elapsed += Time.unscaledDeltaTime;
            float t     = Mathf.Clamp01(elapsed / SLIDE_DUR);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            rect.anchoredPosition = Vector2.Lerp(from, to, eased);
            yield return null;
        }
        rect.anchoredPosition = to;
        _slideCoroutine = null;
        onComplete?.Invoke();
    }

    private float GetCanvasW()
    {
        Canvas c = GetComponentInParent<Canvas>();
        return c != null ? c.GetComponent<RectTransform>().rect.width : 0f;
    }

    private float GetCanvasH()
    {
        Canvas c = GetComponentInParent<Canvas>();
        return c != null ? c.GetComponent<RectTransform>().rect.height : 0f;
    }

    // ── Utility ───────────────────────────────────────────────
    private static void Go(GameObject go, bool active) { if (go != null) go.SetActive(active); }

    // ── Lobby ─────────────────────────────────────────────────

    private void ExecuteCreateRoom()
    {
        if (steamManager != null) steamManager.HostLobby();
        else Debug.LogError($"{LOG_PREFIX} SteamManager yok!");
    }

    private void ExecuteConfirmEntry()
    {
        if (roomCodeInputField == null) { Debug.LogError($"{LOG_PREFIX} Input field yok!"); return; }
        string code = roomCodeInputField.text.Trim();
        if (string.IsNullOrEmpty(code)) { Debug.LogWarning($"{LOG_PREFIX} Kod boş!"); return; }
        if (steamManager != null) steamManager.JoinLobbyWithCode(code);
        else Debug.LogError($"{LOG_PREFIX} SteamManager yok!");
    }

    // ── Audio ─────────────────────────────────────────────────

    public void PlayButtonSound() => PlaySound(buttonClickSound, buttonSoundVolume);

    private void PlaySound(AudioClip clip, float vol)
    {
        if (clip == null) return;
        if (settingsManager != null) vol *= settingsManager.GetSFXVolume() * settingsManager.GetMasterVolume();
        if      (uiAudioSource  != null) uiAudioSource.PlayOneShot(clip, vol);
        else if (sfxAudioSource != null) sfxAudioSource.PlayOneShot(clip, vol);
        else if (Camera.main    != null) AudioSource.PlayClipAtPoint(clip, Camera.main.transform.position, vol);
    }

    // ── Escape ────────────────────────────────────────────────

    private void HandleEscape()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;
        PlayButtonSound();
        switch (_state)
        {
            case MenuState.JoinRoomPanel:   GoTo(MenuState.HostJoinMenu); break;
            case MenuState.HostJoinMenu:    GoTo(MenuState.MainMenu);     break;
            case MenuState.Settings:        BackFromSettings();           break;
            case MenuState.Credits:         CloseCredits();               break;
            case MenuState.TutorialConfirm: CancelTutorial();             break;
        }
    }

    // ── Public API ────────────────────────────────────────────

    public void ShowMainMenuPublic()     => GoTo(MenuState.MainMenu);
    public void ShowHostJoinMenuPublic() => GoTo(MenuState.HostJoinMenu);
    public void ExitHostJoinMenu()       => GoTo(MenuState.MainMenu);
    public void PlayOnline()             => GoTo(MenuState.HostJoinMenu);
    public void BackToMainMenu()         => GoTo(MenuState.MainMenu);
    public void OpenSettings()           => GoTo(MenuState.Settings);
    public void OpenCredits()            => GoTo(MenuState.Credits);
    public void PlayTutorial()           => GoTo(MenuState.TutorialConfirm);
    public void ShowTutorialConfirm()    => GoTo(MenuState.TutorialConfirm);
    public void CreateRoom()             => ExecuteCreateRoom();
    public void JoinRoom()               => GoTo(MenuState.JoinRoomPanel);

    public void PlayOffline()
    {
        Debug.Log($"{LOG_PREFIX} Offline başlatılıyor...");
        SceneManager.LoadScene(SCENE_MAP);
    }

    public void QuitGame()
    {
        Debug.Log($"{LOG_PREFIX} Çıkılıyor...");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void ConfirmTutorial()
    {
        Go(tutorialConfirmPanel, false);
        SceneManager.LoadScene(SCENE_TUT);
    }

    public void CancelTutorial()
    {
        Go(tutorialConfirmPanel, false);
        GoTo(MenuState.MainMenu);
    }

    public void BackFromSettings()
    {
        if (settingsManager != null && settingsManager.HasUnsavedChanges())
            settingsManager.OnBackButtonPressed();
        Go(settingsPanel, false);
        if (_state == MenuState.Settings) _state = MenuState.MainMenu;
    }

    public void SaveSettings()
    {
        if (settingsManager != null) settingsManager.SaveAllSettings();
    }

    public void CloseCredits()
    {
        Go(creditsPanel, false);
        if (_state == MenuState.Credits) _state = MenuState.MainMenu;
    }

    public void SetGameVersion(string v) { gameVersion = v; UpdateVersionText(); }
    private void UpdateVersionText() { if (versionText != null) versionText.text = gameVersion; }

    public void OpenDiscord()   => Application.OpenURL(discordURL);
    public void OpenSteamPage() => Application.OpenURL(steamPageURL);
    public void OpenInstagram() => Application.OpenURL(instagramURL);

    // ── Cleanup ───────────────────────────────────────────────

    private void RemoveListeners()
    {
        foreach (var b in new[] {
            hostGameButton, tutorialButton, settingsButton, creditsButton, quitButton,
            hajBackButton, createRoomButton, joinRoomButton,
            jBackButton, confirmEntryButton,
            tutorialConfirmYesButton, tutorialConfirmNoButton,
            backFromSettingsButton, saveSettingsButton, backFromCreditsButton,
            discordButton, steamPageButton, instagramButton })
            b?.onClick.RemoveAllListeners();
    }
}