using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

/// <summary>
/// Tüm oyun ayarlarını (grafik, ses, dil, çözünürlük, FPS) tek bir yerden yöneten manager. 
/// Kaydedilmemiş değişiklikleri takip eder ve geri alma özelliği sunar.
/// </summary>
public class UnifiedSettingsManager : MonoBehaviour
{
    #region Constants

    private const string LOG_PREFIX = "[SettingsManager]";

    // PlayerPrefs Keys
    private const string PREF_QUALITY = "QualityLevel";
    private const string PREF_LOCALE = "LocaleKey";
    private const string PREF_SCREEN_MODE = "ScreenMode_Setting";

    private const string PREF_VSYNC = "VSync_Setting";
    private const string PREF_MASTER_VOLUME = "MasterVolume_Setting";
    private const string PREF_MUSIC_VOLUME = "MusicVolume_Setting";
    private const string PREF_SFX_VOLUME = "SFXVolume_Setting";


    // Controls PlayerPrefs Keys
    private const string PREF_SENSITIVITY = "Sensitivity_Setting";
    private const string PREF_INVERT_Y = "InvertY_Setting";

    // Controls defaults
    private const float DEFAULT_SENSITIVITY = 1f;
    private const float MIN_SENSITIVITY = 0.1f;
    private const float MAX_SENSITIVITY = 5f;

    // Audio defaults
    private const float DEFAULT_VOLUME = 0.5f;
    private const float SLIDER_SOUND_COOLDOWN = 0.1f;
    private const float SLIDER_SOUND_VOLUME_MULTIPLIER = 0.3f;

    // Localization
    private const string TURKISH_LOCALE_CODE = "tr";

    #endregion

    #region Enums

    /// <summary>
    /// Ekran modu seçenekleri
    /// </summary>
    public enum ScreenMode
    {
        Windowed = 0,
        FullscreenWindowed = 1,
        FullscreenExclusive = 2
    }

    #endregion

    #region Serialized Fields - UI Components

    [Header("=== SHARED UI ===")]
    [SerializeField, Tooltip("Tümünü Kaydet butonu (Opsiyonel)")]
    public Button saveButton;

    [SerializeField, Tooltip("Geri butonu")]
    public Button backButton;

    [Header("=== TAB SPECIFIC SAVE BUTTONS ===")]
    [SerializeField, Tooltip("Audio sekmesi kaydet butonu")]
    public Button audioSaveButton;

    [SerializeField, Tooltip("Video sekmesi kaydet butonu")]
    public Button videoSaveButton;

    [SerializeField, Tooltip("Controls sekmesi kaydet butonu")]
    public Button controlsSaveButton;

    [Header("=== TAB SETTINGS ===")]
    [SerializeField, Tooltip("Audio sekme butonu")]
    public Button audioTabButton;

    [SerializeField, Tooltip("Video sekme butonu")]
    public Button videoTabButton;

    [SerializeField, Tooltip("Controls sekme butonu")]
    public Button controlsTabButton;

    [SerializeField, Tooltip("Audio sekme paneli")]
    public GameObject audioPanel;

    [SerializeField, Tooltip("Video sekme paneli")]
    public GameObject videoPanel;

    [SerializeField, Tooltip("Controls sekme paneli")]
    public GameObject controlsPanel;

    [Header("=== QUALITY SETTINGS ===")]
    [SerializeField, Tooltip("Grafik kalitesi slider'ı (0=VeryLow .. N=Ultra)")]
    public Slider graphicsQualitySlider;

    [SerializeField, Tooltip("Grafik kalitesi text'i (preset adı)")]
    public TextMeshProUGUI graphicsQualityText;

    [Header("=== CONTROLS SETTINGS ===")]
    [SerializeField, Tooltip("Mouse hassasiyet slider'ı")]
    public Slider sensitivitySlider;

    [SerializeField, Tooltip("Mouse hassasiyet değer text'i")]
    public TextMeshProUGUI sensitivityValueText;

    [SerializeField, Tooltip("Y ekseni tersine çevir toggle'ı")]
    public Toggle invertYAxisToggle;

    [SerializeField, Tooltip("Invert Y durum text'i (ON/OFF)")]
    public TextMeshProUGUI invertYAxisStatusText;

    [Header("=== LANGUAGE SETTINGS ===")]
    [SerializeField, Tooltip("Dil dropdown'u")]
    public TMP_Dropdown languageDropdown;

    [Header("=== SCREEN SETTINGS ===")]
    [SerializeField, Tooltip("Ekran modu dropdown'u")]
    public TMP_Dropdown screenModeDropdown;

    [Header("=== VSYNC SETTINGS ===")]
    [SerializeField, Tooltip("VSync toggle'ı")]
    public Toggle vSyncToggle;

    [Header("=== AUDIO SLIDERS ===")]
    [SerializeField, Tooltip("Master ses slider'ı")]
    public Slider masterVolumeSlider;

    [SerializeField, Tooltip("Master ses text'i")]
    public TextMeshProUGUI masterVolumeText;

    [SerializeField, Tooltip("Müzik ses slider'ı")]
    public Slider musicVolumeSlider;

    [SerializeField, Tooltip("Müzik ses text'i")]
    public TextMeshProUGUI musicVolumeText;

    [SerializeField, Tooltip("SFX ses slider'ı")]
    public Slider sfxVolumeSlider;

    [SerializeField, Tooltip("SFX ses text'i")]
    public TextMeshProUGUI sfxVolumeText;

    [Header("=== KEY BINDING UI ===")]
    [SerializeField, Tooltip("Tuş atama satırları (Controls sekmesi)")]
    public KeyBindingRow[] keyBindingRows;

    [SerializeField, Tooltip("Tuş atamalarını sıfırla butonu")]
    public Button resetBindingsButton;

    #endregion

    #region Serialized Fields - Audio Sources

    [Header("=== AUDIO SOURCES ===")]
    [SerializeField, Tooltip("Müzik AudioSource")]
    public AudioSource musicAudioSource;

    [SerializeField, Tooltip("SFX AudioSource")]
    public AudioSource sfxAudioSource;

    [SerializeField, Tooltip("UI sesleri AudioSource")]
    public AudioSource uiAudioSource;

    #endregion

    #region Serialized Fields - UI Sounds

    [Header("=== UI SOUND EFFECTS ===")]
    [SerializeField, Tooltip("Buton tıklama sesi")]
    public AudioClip buttonClickSound;

    [SerializeField, Tooltip("Dropdown tıklama sesi")]
    public AudioClip dropdownClickSound;

    [SerializeField, Tooltip("Slider değişim sesi")]
    public AudioClip sliderChangeSound;

    [SerializeField, Range(0f, 1f), Tooltip("UI ses seviyesi")]
    public float uiSoundVolume = 0.8f;

    #endregion

    #region Serialized Fields - VSync Options

    [Header("=== VSYNC OPTIONS ===")]
    [SerializeField, Tooltip("Varsayılan VSync durumu")]
    public bool defaultVSyncEnabled = true;

    [SerializeField, Tooltip("VSync kapalıyken sabit FPS")]
    public int fixedFPS = 144;

    #endregion

    #region Private Fields - Resolution Data

    // Çözünürlük otomatik algılanır (monitörün native çözünürlüğü)

    #endregion

    #region Private Fields - Saved Settings

    private SettingsData _savedSettings;
    private SettingsData _selectedSettings;

    #endregion

    private bool _hasUnsavedChanges;
    private bool _hasUnsavedAudioChanges;
    private bool _hasUnsavedVideoChanges;
    private bool _hasUnsavedControlsChanges;
    private bool _isLocalizationChanging;
    private float _lastSliderSoundTime;
    private bool _isWaitingForKey;
    private InputBindingManager.GameAction _rebindingAction;
    private Button _rebindingButton;
    private TextMeshProUGUI _rebindingText;


    #region Nested Types

    /// <summary>
    /// Tüm ayarları tutan veri yapısı
    /// </summary>
    private class SettingsData
    {
        public int QualityLevel;
        public int LocaleID;
        public ScreenMode ScreenMode;
        public bool VSyncEnabled;
        public float MasterVolume;
        public float MusicVolume;
        public float SFXVolume;

        // Controls
        public float Sensitivity;
        public bool InvertYAxis;

        public SettingsData Clone()
        {
            return new SettingsData
            {
                QualityLevel = QualityLevel,
                LocaleID = LocaleID,
                ScreenMode = ScreenMode,
                VSyncEnabled = VSyncEnabled,
                MasterVolume = MasterVolume,
                MusicVolume = MusicVolume,
                SFXVolume = SFXVolume,
                Sensitivity = Sensitivity,
                InvertYAxis = InvertYAxis
            };
        }

        public bool Equals(SettingsData other)
        {
            if (other == null) return false;

            return QualityLevel == other.QualityLevel &&
                   LocaleID == other.LocaleID &&
                   ScreenMode == other.ScreenMode &&
                   VSyncEnabled == other.VSyncEnabled &&
                   Mathf.Approximately(MasterVolume, other.MasterVolume) &&
                   Mathf.Approximately(MusicVolume, other.MusicVolume) &&
                   Mathf.Approximately(SFXVolume, other.SFXVolume) &&
                   Mathf.Approximately(Sensitivity, other.Sensitivity) &&
                   InvertYAxis == other.InvertYAxis;
        }
    }

    /// <summary>
    /// Controls sekmesinde tuş atama satırı
    /// </summary>
    [System.Serializable]
    public class KeyBindingRow
    {
        public InputBindingManager.GameAction action;
        public Button button;
        public TextMeshProUGUI keyText;
    }

    #endregion

    #region Unity Lifecycle

    private IEnumerator Start()
    {
        InputBindingManager.Initialize();
        InitializeSettingsData();
        SetupAllUI();

        yield return WaitForLocalizationInitialization();

        LoadAllSettings();
    }

    private void Update()
    {
        HandleKeyRebinding();
    }

    private void OnDisable()
    {
        HandleMenuClosed();
    }

    private void OnDestroy()
    {
        RemoveAllListeners();
    }

    #endregion

    #region Initialization

    private void InitializeSettingsData()
    {
        _savedSettings = new SettingsData();
        _selectedSettings = new SettingsData();
    }

    private void SetupAllUI()
    {
        SetupTabs();
        SetupQualitySlider();
        SetupScreenModeDropdown();
        SetupVSyncToggle();
        SetupAudioSliders();
        SetupLanguageDropdown();
        SetupControlsUI();
        SetupKeyBindingUI();
        SetupButtons();
    }

    private IEnumerator WaitForLocalizationInitialization()
    {
        yield return new WaitUntil(() =>
            LocalizationSettings.InitializationOperation.IsValid() &&
            LocalizationSettings.InitializationOperation.IsDone);
    }

    #endregion

    #region Setup Methods - Dropdowns

    private void SetupQualitySlider()
    {
        if (graphicsQualitySlider == null) return;

        int maxLevel = QualitySettings.names.Length - 1;
        graphicsQualitySlider.minValue = 0;
        graphicsQualitySlider.maxValue = maxLevel;
        graphicsQualitySlider.wholeNumbers = true;

        graphicsQualitySlider.onValueChanged.AddListener(value =>
        {
            PlaySliderSound();
            HandleQualityChanged((int)value);
        });
    }

    private void SetupLanguageDropdown()
    {
        if (languageDropdown == null) return;

        languageDropdown.ClearOptions();

        var languageNames = new List<string> { "Türkçe", "English" };
        foreach (string name in languageNames)
        {
            languageDropdown.options.Add(new TMP_Dropdown.OptionData(name));
        }

        languageDropdown.RefreshShownValue();

        languageDropdown.onValueChanged.AddListener(value =>
        {
            PlayDropdownSound();
            HandleLanguageChanged(value);
        });
    }

    private void SetupScreenModeDropdown()
    {
        if (screenModeDropdown == null) return;

        screenModeDropdown.ClearOptions();
        screenModeDropdown.AddOptions(GetLocalizedScreenModeOptions());

        screenModeDropdown.onValueChanged.AddListener(value =>
        {
            PlayDropdownSound();
            HandleScreenModeChanged(value);
        });
    }



    #endregion

    #region Setup Methods - Other Controls

    private void SetupVSyncToggle()
    {
        if (vSyncToggle == null) return;

        vSyncToggle.onValueChanged.AddListener(value =>
        {
            PlayButtonSound();
            HandleVSyncChanged(value);
        });
    }

    private void SetupAudioSliders()
    {
        SetupVolumeSlider(masterVolumeSlider, HandleMasterVolumeChanged);
        SetupVolumeSlider(musicVolumeSlider, HandleMusicVolumeChanged);
        SetupVolumeSlider(sfxVolumeSlider, HandleSFXVolumeChanged);
    }

    private void SetupVolumeSlider(Slider slider, UnityEngine.Events.UnityAction<float> callback)
    {
        if (slider == null) return;

        slider.minValue = 0f;
        slider.maxValue = 1f;

        slider.onValueChanged.AddListener(value =>
        {
            PlaySliderSound();
            callback?.Invoke(value);
        });
    }

    private void SetupButtons()
    {
        if (saveButton != null)
        {
            saveButton.onClick.AddListener(() =>
            {
                PlayButtonSound();
                SaveAllSettings();
            });
        }

        if (audioSaveButton != null)
        {
            audioSaveButton.onClick.AddListener(() =>
            {
                PlayButtonSound();
                SaveAudioSettings();
            });
        }

        if (videoSaveButton != null)
        {
            videoSaveButton.onClick.AddListener(() =>
            {
                PlayButtonSound();
                SaveVideoSettings();
            });
        }

        if (controlsSaveButton != null)
        {
            controlsSaveButton.onClick.AddListener(() =>
            {
                PlayButtonSound();
                SaveControlsSettings();
            });
        }

        if (backButton != null)
        {
            backButton.onClick.AddListener(() =>
            {
                PlayButtonSound();
                HandleBackButtonPressed();
            });
        }

        UpdateSaveButtonState();
    }

    private void SetupControlsUI()
    {
        // Sensitivity Slider
        if (sensitivitySlider != null)
        {
            sensitivitySlider.minValue = MIN_SENSITIVITY;
            sensitivitySlider.maxValue = MAX_SENSITIVITY;

            sensitivitySlider.onValueChanged.AddListener(value =>
            {
                PlaySliderSound();
                HandleSensitivityChanged(value);
            });
        }

        // Invert Y-Axis Toggle
        if (invertYAxisToggle != null)
        {
            invertYAxisToggle.onValueChanged.AddListener(value =>
            {
                PlayButtonSound();
                HandleInvertYAxisChanged(value);
            });
        }
    }

    #endregion

    #region Setup Methods - Tabs & Key Bindings

    private void SetupTabs()
    {
        if (audioTabButton != null)
            audioTabButton.onClick.AddListener(() => { PlayButtonSound(); SwitchTab(0); });
        
        if (videoTabButton != null)
            videoTabButton.onClick.AddListener(() => { PlayButtonSound(); SwitchTab(1); });
            
        if (controlsTabButton != null)
            controlsTabButton.onClick.AddListener(() => { PlayButtonSound(); SwitchTab(2); });

        // Default to Audio tab
        SwitchTab(0);
    }

    private void SetupKeyBindingUI()
    {
        if (keyBindingRows == null) return;

        foreach (var row in keyBindingRows)
        {
            if (row.button == null || row.keyText == null) continue;

            var action = row.action;
            var button = row.button;
            var text = row.keyText;

            // Mevcut tuş ismini göster
            text.text = InputBindingManager.GetBindingDisplayName(action);

            // Tıklama: rebinding başlat
            button.onClick.AddListener(() =>
            {
                PlayButtonSound();
                StartRebinding(action, button, text);
            });
        }

        if (resetBindingsButton != null)
        {
            resetBindingsButton.onClick.AddListener(() =>
            {
                PlayButtonSound();
                ResetAllBindings();
            });
        }
    }

    private void StartRebinding(InputBindingManager.GameAction action, Button button, TextMeshProUGUI text)
    {
        _isWaitingForKey = true;
        _rebindingAction = action;
        _rebindingButton = button;
        _rebindingText = text;
        text.text = "...";
    }

    private void HandleKeyRebinding()
    {
        if (!_isWaitingForKey) return;

        // ESC ile iptal
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelRebinding();
            return;
        }

        if (!Input.anyKeyDown) return;

        // Mouse butonlarını kontrol et
        for (int i = 0; i < 3; i++)
        {
            if (Input.GetMouseButtonDown(i))
            {
                if (InputBindingManager.IsMouseBound(i, _rebindingAction))
                {
                    CancelRebinding();
                    return;
                }
                else if (InputBindingManager.IsMouseBound(i))
                {
                    return; // Başka bir aksiyona atanmışsa yoksay (beklemeye devam et)
                }

                InputBindingManager.SetBinding(_rebindingAction, i);
                FinishRebinding();
                return;
            }
        }

        // Klavye tuşlarını kontrol et
        foreach (KeyCode key in System.Enum.GetValues(typeof(KeyCode)))
        {
            if (!Input.GetKeyDown(key)) continue;
            if (key == KeyCode.None || key == KeyCode.Escape) continue;
            if (key >= KeyCode.Mouse0 && key <= KeyCode.Mouse6) continue; // Mouse zaten yukarıda

            if (InputBindingManager.IsKeyBound(key, _rebindingAction))
            {
                CancelRebinding();
                return;
            }
            else if (InputBindingManager.IsKeyBound(key))
            {
                return; // Başka bir aksiyona atanmışsa yoksay (beklemeye devam et)
            }

            InputBindingManager.SetBinding(_rebindingAction, key);
            FinishRebinding();
            return;
        }
    }

    private void FinishRebinding()
    {
        if (_rebindingText != null)
            _rebindingText.text = InputBindingManager.GetBindingDisplayName(_rebindingAction);

        _isWaitingForKey = false;
        _rebindingButton = null;
        _rebindingText = null;
        PlayButtonSound();
    }

    private void CancelRebinding()
    {
        if (_rebindingText != null)
            _rebindingText.text = InputBindingManager.GetBindingDisplayName(_rebindingAction);

        _isWaitingForKey = false;
        _rebindingButton = null;
        _rebindingText = null;
    }

    private void ResetAllBindings()
    {
        InputBindingManager.ResetToDefaults();
        RefreshAllKeyBindingTexts();
        Debug.Log($"{LOG_PREFIX} Tüm tuş atamaları varsayılana sıfırlandı.");
    }

    private void RefreshAllKeyBindingTexts()
    {
        if (keyBindingRows == null) return;

        foreach (var row in keyBindingRows)
        {
            if (row.keyText != null)
                row.keyText.text = InputBindingManager.GetBindingDisplayName(row.action);
        }
    }

    /// <summary>
    /// 0: Audio, 1: Video, 2: Controls
    /// </summary>
    public void SwitchTab(int tabIndex)
    {
        if (audioPanel != null) audioPanel.SetActive(tabIndex == 0);
        if (videoPanel != null) videoPanel.SetActive(tabIndex == 1);
        if (controlsPanel != null) controlsPanel.SetActive(tabIndex == 2);
    }

    #endregion

    #region Resolution Helpers

    /// <summary>
    /// Monitörün native çözünürlüğünü döndürür
    /// </summary>
    private Resolution GetNativeResolution()
    {
        return Screen.currentResolution;
    }

    #endregion

    #region Localization Helpers

    private List<string> GetLocalizedScreenModeOptions()
    {
        bool isTurkish = IsTurkishLocale();

        if (isTurkish)
        {
            return new List<string>
            {
                "Pencere",
                "Tam Ekran",
                "Çerçevesiz"
            };
        }

        return new List<string>
        {
            "Windowed",
            "Fullscreen (Windowed)",
            "Fullscreen (Exclusive)"
        };
    }

    private string GetLocalizedScreenModeName(ScreenMode mode)
    {
        bool isTurkish = IsTurkishLocale();

        return mode switch
        {
            ScreenMode.Windowed => isTurkish ? "Pencere Modu" : "Windowed",
            ScreenMode.FullscreenWindowed => isTurkish ? "Tam Ekran (Çerçeveli)" : "Fullscreen (Windowed)",
            ScreenMode.FullscreenExclusive => isTurkish ? "Tam Ekran (Çerçevesiz)" : "Fullscreen (Exclusive)",
            _ => isTurkish ? "Bilinmeyen" : "Unknown"
        };
    }

    private bool IsTurkishLocale()
    {
        return LocalizationSettings.SelectedLocale != null &&
               LocalizationSettings.SelectedLocale.Identifier.Code == TURKISH_LOCALE_CODE;
    }

    private void RefreshScreenModeDropdownLocalization()
    {
        if (screenModeDropdown == null) return;

        int currentValue = screenModeDropdown.value;
        screenModeDropdown.ClearOptions();
        screenModeDropdown.AddOptions(GetLocalizedScreenModeOptions());
        screenModeDropdown.SetValueWithoutNotify(currentValue);
    }

    private void RefreshAllLocalizedUI()
    {
        var localizedComponents = FindObjectsOfType<UnityEngine.Localization.Components.LocalizeStringEvent>();

        foreach (var component in localizedComponents)
        {
            component.RefreshString();
        }
    }

    #endregion

    #region UI Sound Effects

    private void PlayButtonSound()
    {
        PlayUISound(buttonClickSound);
    }

    private void PlayDropdownSound()
    {
        PlayUISound(dropdownClickSound ?? buttonClickSound);
    }

    private void PlaySliderSound()
    {
        if (Time.time - _lastSliderSoundTime < SLIDER_SOUND_COOLDOWN)
        {
            return;
        }

        _lastSliderSoundTime = Time.time;

        AudioClip clip = sliderChangeSound ?? buttonClickSound;
        if (clip == null) return;

        float volume = CalculateUIVolume() * SLIDER_SOUND_VOLUME_MULTIPLIER;
        PlaySoundOnSource(clip, volume);
    }

    private void PlayUISound(AudioClip clip)
    {
        if (clip == null) return;

        float volume = CalculateUIVolume();
        PlaySoundOnSource(clip, volume);
    }

    private float CalculateUIVolume()
    {
        return uiSoundVolume * _selectedSettings.SFXVolume * _selectedSettings.MasterVolume;
    }

    private void PlaySoundOnSource(AudioClip clip, float volume)
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

    #region Load Settings

    private void LoadAllSettings()
    {
        LoadQualitySettings();
        LoadLanguageSettings();
        LoadScreenSettings();
        LoadVSyncSettings();
        LoadAudioSettings();
        LoadControlsSettings();

        // Selected = Saved (başlangıçta)
        _selectedSettings = _savedSettings.Clone();

        ApplyAllCurrentSettings();
        UpdateAllUI();

        _hasUnsavedChanges = false;
        UpdateSaveButtonState();

        Debug.Log($"{LOG_PREFIX} Tüm ayarlar yüklendi.");
    }

    private void LoadQualitySettings()
    {
        int savedQuality = PlayerPrefs.GetInt(PREF_QUALITY, QualitySettings.GetQualityLevel());
        _savedSettings.QualityLevel = Mathf.Clamp(savedQuality, 0, QualitySettings.names.Length - 1);
    }

    private void LoadLanguageSettings()
    {
        int savedLocale = PlayerPrefs.GetInt(PREF_LOCALE, 0);
        int maxLocale = LocalizationSettings.AvailableLocales.Locales.Count - 1;
        _savedSettings.LocaleID = Mathf.Clamp(savedLocale, 0, maxLocale);
    }

    private void LoadScreenSettings()
    {
        // Screen Mode - varsayılan: Tam Ekran (Çerçevesiz / Borderless)
        int savedScreenMode = PlayerPrefs.GetInt(PREF_SCREEN_MODE, (int)ScreenMode.FullscreenWindowed);
        _savedSettings.ScreenMode = (ScreenMode)savedScreenMode;
        // Çözünürlük monitörden otomatik algılanır, ayar yok
    }

    private void LoadVSyncSettings()
    {
        int savedVSync = PlayerPrefs.GetInt(PREF_VSYNC, defaultVSyncEnabled ? 1 : 0);
        _savedSettings.VSyncEnabled = savedVSync == 1;
    }

    private void LoadAudioSettings()
    {
        _savedSettings.MasterVolume = PlayerPrefs.GetFloat(PREF_MASTER_VOLUME, DEFAULT_VOLUME);
        _savedSettings.MusicVolume = PlayerPrefs.GetFloat(PREF_MUSIC_VOLUME, DEFAULT_VOLUME);
        _savedSettings.SFXVolume = PlayerPrefs.GetFloat(PREF_SFX_VOLUME, DEFAULT_VOLUME);
    }
    private void LoadControlsSettings()
    {
        _savedSettings.Sensitivity = PlayerPrefs.GetFloat(PREF_SENSITIVITY, DEFAULT_SENSITIVITY);
        _savedSettings.InvertYAxis = PlayerPrefs.GetInt(PREF_INVERT_Y, 0) == 1;
    }

    #endregion

    #region Change Handlers

    private void HandleQualityChanged(int newValue)
    {
        int maxLevel = QualitySettings.names.Length - 1;
        newValue = Mathf.Clamp(newValue, 0, maxLevel);
        _selectedSettings.QualityLevel = newValue;
        ApplyQualitySettings(newValue);
        UpdateQualityText(newValue);
        CheckForChanges();

        Debug.Log($"{LOG_PREFIX} Kalite değiştirildi: {QualitySettings.names[newValue]} (Kaydedilmedi)");
    }

    private void HandleSensitivityChanged(float newValue)
    {
        _selectedSettings.Sensitivity = newValue;
        UpdateSensitivityText();
        CheckForChanges();
    }

    private void HandleInvertYAxisChanged(bool newValue)
    {
        _selectedSettings.InvertYAxis = newValue;
        UpdateInvertYAxisText();
        CheckForChanges();
    }

    private void HandleLanguageChanged(int newValue)
    {
        _selectedSettings.LocaleID = newValue;
        StartCoroutine(ApplyLocalePreviewCoroutine(newValue));
        CheckForChanges();
    }

    private void HandleScreenModeChanged(int newValue)
    {
        if (newValue < 0 || newValue > 2) return;

        _selectedSettings.ScreenMode = (ScreenMode)newValue;
        ApplyScreenMode(_selectedSettings.ScreenMode);
        CheckForChanges();

        Debug.Log($"{LOG_PREFIX} Ekran modu değiştirildi: {GetLocalizedScreenModeName(_selectedSettings.ScreenMode)} (Kaydedilmedi)");
    }

    private void HandleVSyncChanged(bool newValue)
    {
        _selectedSettings.VSyncEnabled = newValue;
        ApplyVSyncSettings(_selectedSettings.VSyncEnabled);
        CheckForChanges();

        Debug.Log($"{LOG_PREFIX} VSync değiştirildi: {newValue} (Kaydedilmedi)");
    }

    private void HandleMasterVolumeChanged(float newValue)
    {
        _selectedSettings.MasterVolume = newValue;
        ApplyAudioSettings();
        UpdateAudioTexts();
        CheckForChanges();
    }

    private void HandleMusicVolumeChanged(float newValue)
    {
        _selectedSettings.MusicVolume = newValue;
        ApplyAudioSettings();
        UpdateAudioTexts();
        CheckForChanges();
    }

    private void HandleSFXVolumeChanged(float newValue)
    {
        _selectedSettings.SFXVolume = newValue;
        ApplyAudioSettings();
        UpdateAudioTexts();
        CheckForChanges();
    }

    private void HandleBackButtonPressed()
    {
        if (_hasUnsavedChanges)
        {
            ResetToSavedSettings();
            Debug.Log($"{LOG_PREFIX} Kaydedilmemiş değişiklikler geri alındı.");
        }
    }

    private void HandleMenuClosed()
    {
        if (_hasUnsavedChanges)
        {
            ResetToSavedSettings();
            Debug.Log($"{LOG_PREFIX} Menü kapatıldı.  Kaydedilmemiş değişiklikler geri alındı.");
        }
    }

    #endregion

    #region Apply Methods

    private void ApplyAllCurrentSettings()
    {
        ApplyQualitySettings(_savedSettings.QualityLevel);
        StartCoroutine(ApplyLocalePreviewCoroutine(_savedSettings.LocaleID));
        ApplyScreenMode(_savedSettings.ScreenMode);
        ApplyVSyncSettings(_savedSettings.VSyncEnabled);
        ApplyAudioSettings();
    }

    private void ApplyQualitySettings(int qualityLevel)
    {
        int maxLevel = QualitySettings.names.Length - 1;
        qualityLevel = Mathf.Clamp(qualityLevel, 0, maxLevel);
        QualitySettings.SetQualityLevel(qualityLevel, true);
    }

    /// <summary>
    /// Monitörün native çözünürlüğünü kullanarak ekran modunu uygular.
    /// Çözünürlük her zaman monitörden otomatik algılanır.
    /// </summary>
    private void ApplyScreenMode(ScreenMode screenMode)
    {
        Resolution nativeRes = GetNativeResolution();
        FullScreenMode fullScreenMode = ConvertToFullScreenMode(screenMode);

        Screen.SetResolution(nativeRes.width, nativeRes.height, fullScreenMode);

        Debug.Log($"{LOG_PREFIX} ✅ Ekran ayarları uygulandı: {nativeRes.width}x{nativeRes.height}, Mod={screenMode}");
    }

    private FullScreenMode ConvertToFullScreenMode(ScreenMode screenMode)
    {
        return screenMode switch
        {
            ScreenMode.Windowed => FullScreenMode.Windowed,
            ScreenMode.FullscreenWindowed => FullScreenMode.FullScreenWindow,
            ScreenMode.FullscreenExclusive => FullScreenMode.ExclusiveFullScreen,
            _ => FullScreenMode.ExclusiveFullScreen
        };
    }

    private void ApplyVSyncSettings(bool vSyncEnabled)
    {
        if (vSyncEnabled)
        {
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = -1;
        }
        else
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = fixedFPS;
        }
    }

    private void ApplyAudioSettings()
    {
        float master = _selectedSettings.MasterVolume;
        float music = _selectedSettings.MusicVolume;
        float sfx = _selectedSettings.SFXVolume;

        AudioListener.volume = master;

        if (musicAudioSource != null)
        {
            musicAudioSource.volume = music * master;
        }

        if (sfxAudioSource != null)
        {
            sfxAudioSource.volume = sfx * master;
        }
    }

    private IEnumerator ApplyLocalePreviewCoroutine(int localeID)
    {
        if (_isLocalizationChanging) yield break;

        _isLocalizationChanging = true;

        yield return WaitForLocalizationInitialization();

        var locales = LocalizationSettings.AvailableLocales.Locales;
        if (localeID >= 0 && localeID < locales.Count)
        {
            LocalizationSettings.SelectedLocale = locales[localeID];
            yield return new WaitForEndOfFrame();

            RefreshAllLocalizedUI();
            RefreshScreenModeDropdownLocalization();
        }

        _isLocalizationChanging = false;
    }

    #endregion

    #region Update UI

    private void UpdateAllUI()
    {
        UpdateDropdownsWithoutNotify();
        UpdateToggleWithoutNotify();
        UpdateSlidersWithoutNotify();
        UpdateAudioTexts();
        UpdateQualityText(_savedSettings.QualityLevel);
        UpdateSensitivityText();
        UpdateInvertYAxisText();
        RefreshAllKeyBindingTexts();
    }

    private void UpdateDropdownsWithoutNotify()
    {
        languageDropdown?.SetValueWithoutNotify(_savedSettings.LocaleID);
        screenModeDropdown?.SetValueWithoutNotify((int)_savedSettings.ScreenMode);

    }

    private void UpdateToggleWithoutNotify()
    {
        vSyncToggle?.SetIsOnWithoutNotify(_savedSettings.VSyncEnabled);
        invertYAxisToggle?.SetIsOnWithoutNotify(_savedSettings.InvertYAxis);
    }

    private void UpdateSlidersWithoutNotify()
    {
        masterVolumeSlider?.SetValueWithoutNotify(_savedSettings.MasterVolume);
        musicVolumeSlider?.SetValueWithoutNotify(_savedSettings.MusicVolume);
        sfxVolumeSlider?.SetValueWithoutNotify(_savedSettings.SFXVolume);
        graphicsQualitySlider?.SetValueWithoutNotify(_savedSettings.QualityLevel);
        sensitivitySlider?.SetValueWithoutNotify(_savedSettings.Sensitivity);
    }

    private void UpdateAudioTexts()
    {
        if (masterVolumeText != null)
            masterVolumeText.text = Mathf.RoundToInt(_selectedSettings.MasterVolume * 100).ToString();

        if (musicVolumeText != null)
            musicVolumeText.text = Mathf.RoundToInt(_selectedSettings.MusicVolume * 100).ToString();

        if (sfxVolumeText != null)
            sfxVolumeText.text = Mathf.RoundToInt(_selectedSettings.SFXVolume * 100).ToString();
    }

    private void UpdateQualityText(int qualityLevel)
    {
        if (graphicsQualityText == null) return;
        int maxLevel = QualitySettings.names.Length - 1;
        qualityLevel = Mathf.Clamp(qualityLevel, 0, maxLevel);
        graphicsQualityText.text = QualitySettings.names[qualityLevel];
    }

    private void UpdateSensitivityText()
    {
        if (sensitivityValueText != null)
            sensitivityValueText.text = _selectedSettings.Sensitivity.ToString("F1");
    }

    private void UpdateInvertYAxisText()
    {
        if (invertYAxisStatusText != null)
            invertYAxisStatusText.text = _selectedSettings.InvertYAxis ? "ON" : "OFF";
    }



    private void UpdateSaveButtonState()
    {
        ColorUtility.TryParseHtmlString("#CD5C36", out Color activeColor);

        if (saveButton != null)
        {
            saveButton.interactable = _hasUnsavedChanges;
            SetButtonColor(saveButton, _hasUnsavedChanges, activeColor);
        }

        if (audioSaveButton != null)
        {
            audioSaveButton.interactable = _hasUnsavedAudioChanges;
            SetButtonColor(audioSaveButton, _hasUnsavedAudioChanges, activeColor);
        }

        if (videoSaveButton != null)
        {
            videoSaveButton.interactable = _hasUnsavedVideoChanges;
            SetButtonColor(videoSaveButton, _hasUnsavedVideoChanges, activeColor);
        }

        if (controlsSaveButton != null)
        {
            controlsSaveButton.interactable = _hasUnsavedControlsChanges;
            SetButtonColor(controlsSaveButton, _hasUnsavedControlsChanges, activeColor);
        }
    }

    private void SetButtonColor(Button btn, bool hasChanges, Color activeColor)
    {
        ColorBlock colors = btn.colors;
        colors.normalColor = hasChanges ? activeColor : Color.gray;
        colors.highlightedColor = hasChanges ? activeColor * 0.8f : Color.gray * 0.8f;
        btn.colors = colors;
    }

    #endregion

    #region Save & Reset

    /// <summary>
    /// Tüm ayarları kaydeder
    /// </summary>
    public void SaveAllSettings()
    {
        if (!_hasUnsavedChanges)
        {
            Debug.Log($"{LOG_PREFIX} Kaydedilecek değişiklik yok.");
            return;
        }

        // Selected → Saved
        _savedSettings = _selectedSettings.Clone();

        // PlayerPrefs'e kaydet
        SaveToPlayerPrefs();

        _hasUnsavedChanges = false;
        UpdateSaveButtonState();

        Debug.Log($"{LOG_PREFIX} ✅ Tüm ayarlar kaydedildi!  " +
                  $"Master: {_savedSettings.MasterVolume:F2}, " +
                  $"Music: {_savedSettings.MusicVolume:F2}, " +
                  $"SFX: {_savedSettings.SFXVolume:F2}");
    }

    public void SaveAudioSettings()
    {
        if (!_hasUnsavedAudioChanges) return;

        _savedSettings.MasterVolume = _selectedSettings.MasterVolume;
        _savedSettings.MusicVolume = _selectedSettings.MusicVolume;
        _savedSettings.SFXVolume = _selectedSettings.SFXVolume;

        PlayerPrefs.SetFloat(PREF_MASTER_VOLUME, _savedSettings.MasterVolume);
        PlayerPrefs.SetFloat(PREF_MUSIC_VOLUME, _savedSettings.MusicVolume);
        PlayerPrefs.SetFloat(PREF_SFX_VOLUME, _savedSettings.SFXVolume);
        PlayerPrefs.Save();

        CheckForChanges();
        Debug.Log($"{LOG_PREFIX} ✅ Audio ayarları kaydedildi!");
    }

    public void SaveVideoSettings()
    {
        if (!_hasUnsavedVideoChanges) return;

        _savedSettings.QualityLevel = _selectedSettings.QualityLevel;
        _savedSettings.LocaleID = _selectedSettings.LocaleID;
        _savedSettings.ScreenMode = _selectedSettings.ScreenMode;

        _savedSettings.VSyncEnabled = _selectedSettings.VSyncEnabled;

        PlayerPrefs.SetInt(PREF_QUALITY, _savedSettings.QualityLevel);
        PlayerPrefs.SetInt(PREF_LOCALE, _savedSettings.LocaleID);
        PlayerPrefs.SetInt(PREF_SCREEN_MODE, (int)_savedSettings.ScreenMode);

        PlayerPrefs.SetInt(PREF_VSYNC, _savedSettings.VSyncEnabled ? 1 : 0);
        PlayerPrefs.Save();

        CheckForChanges();
        Debug.Log($"{LOG_PREFIX} ✅ Video ayarları kaydedildi!");
    }

    public void SaveControlsSettings()
    {
        if (!_hasUnsavedControlsChanges) return;

        _savedSettings.Sensitivity = _selectedSettings.Sensitivity;
        _savedSettings.InvertYAxis = _selectedSettings.InvertYAxis;

        PlayerPrefs.SetFloat(PREF_SENSITIVITY, _savedSettings.Sensitivity);
        PlayerPrefs.SetInt(PREF_INVERT_Y, _savedSettings.InvertYAxis ? 1 : 0);
        PlayerPrefs.Save();

        CheckForChanges();
        Debug.Log($"{LOG_PREFIX} ✅ Controls ayarları kaydedildi!");
    }

    private void SaveToPlayerPrefs()
    {
        PlayerPrefs.SetInt(PREF_QUALITY, _savedSettings.QualityLevel);
        PlayerPrefs.SetInt(PREF_LOCALE, _savedSettings.LocaleID);
        PlayerPrefs.SetInt(PREF_SCREEN_MODE, (int)_savedSettings.ScreenMode);

        PlayerPrefs.SetInt(PREF_VSYNC, _savedSettings.VSyncEnabled ? 1 : 0);
        PlayerPrefs.SetFloat(PREF_MASTER_VOLUME, _savedSettings.MasterVolume);
        PlayerPrefs.SetFloat(PREF_MUSIC_VOLUME, _savedSettings.MusicVolume);
        PlayerPrefs.SetFloat(PREF_SFX_VOLUME, _savedSettings.SFXVolume);

        // Controls
        PlayerPrefs.SetFloat(PREF_SENSITIVITY, _savedSettings.Sensitivity);
        PlayerPrefs.SetInt(PREF_INVERT_Y, _savedSettings.InvertYAxis ? 1 : 0);

        PlayerPrefs.Save();
    }

    private void ResetToSavedSettings()
    {
        // Saved → Selected
        _selectedSettings = _savedSettings.Clone();

        // UI güncelle
        UpdateAllUIFromSelected();

        // Ayarları uygula
        ApplyAllSettingsFromSaved();

        _hasUnsavedChanges = false;
        UpdateSaveButtonState();
    }

    private void UpdateAllUIFromSelected()
    {
        graphicsQualitySlider?.SetValueWithoutNotify(_selectedSettings.QualityLevel);
        UpdateQualityText(_selectedSettings.QualityLevel);
        languageDropdown?.SetValueWithoutNotify(_selectedSettings.LocaleID);
        screenModeDropdown?.SetValueWithoutNotify((int)_selectedSettings.ScreenMode);

        vSyncToggle?.SetIsOnWithoutNotify(_selectedSettings.VSyncEnabled);
        masterVolumeSlider?.SetValueWithoutNotify(_selectedSettings.MasterVolume);
        musicVolumeSlider?.SetValueWithoutNotify(_selectedSettings.MusicVolume);
        sfxVolumeSlider?.SetValueWithoutNotify(_selectedSettings.SFXVolume);
        sensitivitySlider?.SetValueWithoutNotify(_selectedSettings.Sensitivity);
        invertYAxisToggle?.SetIsOnWithoutNotify(_selectedSettings.InvertYAxis);
        UpdateSensitivityText();
        UpdateInvertYAxisText();
    }

    private void ApplyAllSettingsFromSaved()
    {
        ApplyQualitySettings(_savedSettings.QualityLevel);
        StartCoroutine(ApplyLocalePreviewCoroutine(_savedSettings.LocaleID));
        ApplyScreenMode(_savedSettings.ScreenMode);
        ApplyVSyncSettings(_savedSettings.VSyncEnabled);
        ApplyAudioSettings();
        UpdateAudioTexts();
        UpdateQualityText(_savedSettings.QualityLevel);
        UpdateSensitivityText();
        UpdateInvertYAxisText();
    }

    #endregion

    #region Change Detection

    private void CheckForChanges()
    {
        _hasUnsavedAudioChanges = !Mathf.Approximately(_selectedSettings.MasterVolume, _savedSettings.MasterVolume) ||
                                  !Mathf.Approximately(_selectedSettings.MusicVolume, _savedSettings.MusicVolume) ||
                                  !Mathf.Approximately(_selectedSettings.SFXVolume, _savedSettings.SFXVolume);

        _hasUnsavedVideoChanges = _selectedSettings.QualityLevel != _savedSettings.QualityLevel ||
                                  _selectedSettings.LocaleID != _savedSettings.LocaleID ||
                                  _selectedSettings.ScreenMode != _savedSettings.ScreenMode ||
                                  _selectedSettings.VSyncEnabled != _savedSettings.VSyncEnabled;

        _hasUnsavedControlsChanges = !Mathf.Approximately(_selectedSettings.Sensitivity, _savedSettings.Sensitivity) ||
                                     _selectedSettings.InvertYAxis != _savedSettings.InvertYAxis;

        _hasUnsavedChanges = _hasUnsavedAudioChanges || _hasUnsavedVideoChanges || _hasUnsavedControlsChanges;

        UpdateSaveButtonState();
    }

    #endregion

    #region Cleanup

    private void RemoveAllListeners()
    {
        graphicsQualitySlider?.onValueChanged.RemoveAllListeners();
        languageDropdown?.onValueChanged.RemoveAllListeners();
        screenModeDropdown?.onValueChanged.RemoveAllListeners();

        vSyncToggle?.onValueChanged.RemoveAllListeners();
        audioTabButton?.onClick.RemoveAllListeners();
        videoTabButton?.onClick.RemoveAllListeners();
        controlsTabButton?.onClick.RemoveAllListeners();
        masterVolumeSlider?.onValueChanged.RemoveAllListeners();
        musicVolumeSlider?.onValueChanged.RemoveAllListeners();
        sfxVolumeSlider?.onValueChanged.RemoveAllListeners();
        sensitivitySlider?.onValueChanged.RemoveAllListeners();
        invertYAxisToggle?.onValueChanged.RemoveAllListeners();
        resetBindingsButton?.onClick.RemoveAllListeners();
        saveButton?.onClick.RemoveAllListeners();
        audioSaveButton?.onClick.RemoveAllListeners();
        videoSaveButton?.onClick.RemoveAllListeners();
        controlsSaveButton?.onClick.RemoveAllListeners();
        backButton?.onClick.RemoveAllListeners();

        if (keyBindingRows != null)
        {
            foreach (var row in keyBindingRows)
            {
                row.button?.onClick.RemoveAllListeners();
            }
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Kaydedilmemiş değişiklik var mı?
    /// </summary>
    public bool HasUnsavedChanges() => _hasUnsavedChanges;

    /// <summary>
    /// Mevcut kalite seviyesi adı
    /// </summary>
    public string GetCurrentQualityName()
    {
        int level = Mathf.Clamp(_savedSettings.QualityLevel, 0, QualitySettings.names.Length - 1);
        return QualitySettings.names[level];
    }

    /// <summary>
    /// Mevcut ekran modu adı
    /// </summary>
    public string GetCurrentScreenModeName() => GetLocalizedScreenModeName(_savedSettings.ScreenMode);

    /// <summary>
    /// Mevcut çözünürlük adı
    /// </summary>
    public string GetCurrentResolutionName()
    {
        Resolution res = GetNativeResolution();
        return $"{res.width} x {res.height}";
    }



    /// <summary>
    /// Master ses seviyesi (0-1)
    /// </summary>
    public float GetMasterVolume() => _savedSettings.MasterVolume;

    /// <summary>
    /// Müzik ses seviyesi (0-1)
    /// </summary>
    public float GetMusicVolume() => _savedSettings.MusicVolume;

    /// <summary>
    /// SFX ses seviyesi (0-1)
    /// </summary>
    public float GetSFXVolume() => _savedSettings.SFXVolume;

    /// <summary>
    /// Mouse hassasiyet değeri
    /// </summary>
    public float GetSensitivity() => _savedSettings.Sensitivity;

    /// <summary>
    /// Y ekseni ters mi?
    /// </summary>
    public bool GetInvertYAxis() => _savedSettings.InvertYAxis;

    public void OnBackButtonPressed()
    {
        HandleBackButtonPressed();
    }
    #endregion
}