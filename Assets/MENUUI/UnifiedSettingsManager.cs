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
    private const string PREF_FPS = "FPS_Setting";
    private const string PREF_VSYNC = "VSync_Setting";
    private const string PREF_RESOLUTION = "Resolution_Setting";
    private const string PREF_MASTER_VOLUME = "MasterVolume_Setting";
    private const string PREF_MUSIC_VOLUME = "MusicVolume_Setting";
    private const string PREF_SFX_VOLUME = "SFXVolume_Setting";

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
    [SerializeField, Tooltip("Kaydet butonu")]
    public Button saveButton;

    [SerializeField, Tooltip("Geri butonu")]
    public Button backButton;

    [Header("=== QUALITY SETTINGS ===")]
    [SerializeField, Tooltip("Grafik kalitesi dropdown'u")]
    public TMP_Dropdown qualityDropdown;

    [Header("=== LANGUAGE SETTINGS ===")]
    [SerializeField, Tooltip("Dil dropdown'u")]
    public TMP_Dropdown languageDropdown;

    [Header("=== SCREEN SETTINGS ===")]
    [SerializeField, Tooltip("Ekran modu dropdown'u")]
    public TMP_Dropdown screenModeDropdown;

    [SerializeField, Tooltip("Çözünürlük dropdown'u")]
    public TMP_Dropdown resolutionDropdown;

    [Header("=== FPS SETTINGS ===")]
    [SerializeField, Tooltip("FPS dropdown'u")]
    public TMP_Dropdown fpsDropdown;

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

    #region Serialized Fields - FPS Options

    [Header("=== FPS OPTIONS ===")]
    [SerializeField, Tooltip("FPS seçenekleri")]
    public int[] fpsOptions = { 30, 60, 90, 120, 140, 160 };

    [SerializeField, Tooltip("Varsayılan FPS index'i")]
    public int defaultFPSIndex = 1;

    [SerializeField, Tooltip("Varsayılan VSync durumu")]
    public bool defaultVSyncEnabled = true;

    #endregion

    #region Private Fields - Resolution Data

    private Resolution[] _availableResolutions;
    private List<Resolution> _filteredResolutions;

    #endregion

    #region Private Fields - Saved Settings

    private SettingsData _savedSettings;
    private SettingsData _selectedSettings;

    #endregion

    #region Private Fields - State

    private bool _hasUnsavedChanges;
    private bool _isLocalizationChanging;
    private float _lastSliderSoundTime;

    #endregion

    #region Nested Types

    /// <summary>
    /// Tüm ayarları tutan veri yapısı
    /// </summary>
    private class SettingsData
    {
        public int QualityLevel;
        public int LocaleID;
        public ScreenMode ScreenMode;
        public int ResolutionIndex;
        public int FPSIndex;
        public bool VSyncEnabled;
        public float MasterVolume;
        public float MusicVolume;
        public float SFXVolume;

        public SettingsData Clone()
        {
            return new SettingsData
            {
                QualityLevel = QualityLevel,
                LocaleID = LocaleID,
                ScreenMode = ScreenMode,
                ResolutionIndex = ResolutionIndex,
                FPSIndex = FPSIndex,
                VSyncEnabled = VSyncEnabled,
                MasterVolume = MasterVolume,
                MusicVolume = MusicVolume,
                SFXVolume = SFXVolume
            };
        }

        public bool Equals(SettingsData other)
        {
            if (other == null) return false;

            return QualityLevel == other.QualityLevel &&
                   LocaleID == other.LocaleID &&
                   ScreenMode == other.ScreenMode &&
                   ResolutionIndex == other.ResolutionIndex &&
                   FPSIndex == other.FPSIndex &&
                   VSyncEnabled == other.VSyncEnabled &&
                   Mathf.Approximately(MasterVolume, other.MasterVolume) &&
                   Mathf.Approximately(MusicVolume, other.MusicVolume) &&
                   Mathf.Approximately(SFXVolume, other.SFXVolume);
        }
    }

    #endregion

    #region Unity Lifecycle

    private IEnumerator Start()
    {
        InitializeSettingsData();
        SetupAllUI();

        yield return WaitForLocalizationInitialization();

        LoadAllSettings();
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
        _filteredResolutions = new List<Resolution>();
    }

    private void SetupAllUI()
    {
        SetupQualityDropdown();
        SetupScreenModeDropdown();
        SetupResolutionDropdown();
        SetupFPSDropdown();
        SetupVSyncToggle();
        SetupAudioSliders();
        SetupLanguageDropdown();
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

    private void SetupQualityDropdown()
    {
        if (qualityDropdown == null) return;

        qualityDropdown.ClearOptions();

        var options = new List<string>(QualitySettings.names);
        qualityDropdown.AddOptions(options);

        qualityDropdown.onValueChanged.AddListener(value =>
        {
            PlayDropdownSound();
            HandleQualityChanged(value);
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

    private void SetupResolutionDropdown()
    {
        if (resolutionDropdown == null) return;

        BuildFilteredResolutionList();
        PopulateResolutionDropdown();

        resolutionDropdown.onValueChanged.AddListener(value =>
        {
            PlayDropdownSound();
            HandleResolutionChanged(value);
        });
    }

    private void SetupFPSDropdown()
    {
        if (fpsDropdown == null) return;

        fpsDropdown.ClearOptions();

        var options = new List<string>();
        foreach (int fps in fpsOptions)
        {
            options.Add($"{fps} FPS");
        }

        fpsDropdown.AddOptions(options);

        fpsDropdown.onValueChanged.AddListener(value =>
        {
            PlayDropdownSound();
            HandleFPSChanged(value);
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

    #endregion

    #region Resolution Helpers

    private void BuildFilteredResolutionList()
    {
        _availableResolutions = Screen.resolutions;
        _filteredResolutions.Clear();

        Resolution previousResolution = default;

        foreach (Resolution resolution in _availableResolutions)
        {
            bool isDifferent = resolution.width != previousResolution.width ||
                               resolution.height != previousResolution.height;

            if (isDifferent)
            {
                _filteredResolutions.Add(resolution);
                previousResolution = resolution;
            }
        }
    }

    private void PopulateResolutionDropdown()
    {
        resolutionDropdown.ClearOptions();

        var options = new List<string>();
        foreach (Resolution resolution in _filteredResolutions)
        {
            options.Add(FormatResolutionDisplayName(resolution));
        }

        resolutionDropdown.AddOptions(options);
    }

    private string FormatResolutionDisplayName(Resolution resolution)
    {
        string aspectRatio = CalculateAspectRatio(resolution.width, resolution.height);
        string qualityLabel = GetResolutionQualityLabel(resolution.height);

        return $"{resolution.width} x {resolution.height} {qualityLabel} ({aspectRatio})";
    }

    private string CalculateAspectRatio(int width, int height)
    {
        int gcd = CalculateGCD(width, height);
        return $"{width / gcd}:{height / gcd}";
    }

    private int CalculateGCD(int a, int b)
    {
        while (b != 0)
        {
            int temp = b;
            b = a % b;
            a = temp;
        }
        return a;
    }

    private string GetResolutionQualityLabel(int height)
    {
        if (height >= 2160) return "[4K]";
        if (height >= 1440) return "[2K]";
        if (height >= 1080) return "[Full HD]";
        if (height >= 720) return "[HD]";
        return "";
    }

    private int FindCurrentResolutionIndex()
    {
        Resolution currentRes = Screen.currentResolution;

        for (int i = 0; i < _filteredResolutions.Count; i++)
        {
            if (_filteredResolutions[i].width == currentRes.width &&
                _filteredResolutions[i].height == currentRes.height)
            {
                return i;
            }
        }

        return _filteredResolutions.Count - 1;
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
                "Pencere Modu",
                "Tam Ekran (Çerçeveli)",
                "Tam Ekran (Çerçevesiz)"
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
        LoadFPSSettings();
        LoadAudioSettings();

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
        // Screen Mode
        int savedScreenMode = PlayerPrefs.GetInt(PREF_SCREEN_MODE, (int)ScreenMode.FullscreenExclusive);
        _savedSettings.ScreenMode = (ScreenMode)savedScreenMode;

        // Resolution
        int savedResolution = PlayerPrefs.GetInt(PREF_RESOLUTION, FindCurrentResolutionIndex());
        _savedSettings.ResolutionIndex = Mathf.Clamp(savedResolution, 0, _filteredResolutions.Count - 1);
    }

    private void LoadFPSSettings()
    {
        int savedFPS = PlayerPrefs.GetInt(PREF_FPS, defaultFPSIndex);
        _savedSettings.FPSIndex = Mathf.Clamp(savedFPS, 0, fpsOptions.Length - 1);

        int savedVSync = PlayerPrefs.GetInt(PREF_VSYNC, defaultVSyncEnabled ? 1 : 0);
        _savedSettings.VSyncEnabled = savedVSync == 1;
    }

    private void LoadAudioSettings()
    {
        _savedSettings.MasterVolume = PlayerPrefs.GetFloat(PREF_MASTER_VOLUME, DEFAULT_VOLUME);
        _savedSettings.MusicVolume = PlayerPrefs.GetFloat(PREF_MUSIC_VOLUME, DEFAULT_VOLUME);
        _savedSettings.SFXVolume = PlayerPrefs.GetFloat(PREF_SFX_VOLUME, DEFAULT_VOLUME);
    }

    #endregion

    #region Change Handlers

    private void HandleQualityChanged(int newValue)
    {
        _selectedSettings.QualityLevel = newValue;
        ApplyQualitySettings(_selectedSettings.QualityLevel);
        CheckForChanges();

        Debug.Log($"{LOG_PREFIX} Kalite değiştirildi: {QualitySettings.names[newValue]} (Kaydedilmedi)");
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
        ApplyResolutionAndScreenMode(_selectedSettings.ResolutionIndex, _selectedSettings.ScreenMode);
        CheckForChanges();

        Debug.Log($"{LOG_PREFIX} Ekran modu değiştirildi: {GetLocalizedScreenModeName(_selectedSettings.ScreenMode)} (Kaydedilmedi)");
    }

    private void HandleResolutionChanged(int newValue)
    {
        if (newValue < 0 || newValue >= _filteredResolutions.Count) return;

        _selectedSettings.ResolutionIndex = newValue;
        ApplyResolutionAndScreenMode(_selectedSettings.ResolutionIndex, _selectedSettings.ScreenMode);
        CheckForChanges();

        Debug.Log($"{LOG_PREFIX} Çözünürlük değiştirildi: {FormatResolutionDisplayName(_filteredResolutions[newValue])} (Kaydedilmedi)");
    }

    private void HandleFPSChanged(int newValue)
    {
        if (_selectedSettings.VSyncEnabled) return;
        if (newValue < 0 || newValue >= fpsOptions.Length) return;

        _selectedSettings.FPSIndex = newValue;
        ApplyFPSSettings(_selectedSettings.FPSIndex, _selectedSettings.VSyncEnabled);
        CheckForChanges();

        Debug.Log($"{LOG_PREFIX} FPS değiştirildi: {fpsOptions[newValue]} (Kaydedilmedi)");
    }

    private void HandleVSyncChanged(bool newValue)
    {
        _selectedSettings.VSyncEnabled = newValue;
        UpdateFPSDropdownInteractable(!newValue);
        ApplyFPSSettings(_selectedSettings.FPSIndex, _selectedSettings.VSyncEnabled);
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
        ApplyResolutionAndScreenMode(_savedSettings.ResolutionIndex, _savedSettings.ScreenMode);
        ApplyFPSSettings(_savedSettings.FPSIndex, _savedSettings.VSyncEnabled);
        ApplyAudioSettings();
    }

    private void ApplyQualitySettings(int qualityLevel)
    {
        QualitySettings.SetQualityLevel(qualityLevel, true);
    }

    private void ApplyResolutionAndScreenMode(int resolutionIndex, ScreenMode screenMode)
    {
        if (resolutionIndex < 0 || resolutionIndex >= _filteredResolutions.Count) return;

        Resolution resolution = _filteredResolutions[resolutionIndex];
        int maxRefreshRate = FindMaxRefreshRate(resolution);
        FullScreenMode fullScreenMode = ConvertToFullScreenMode(screenMode);

        Screen.SetResolution(resolution.width, resolution.height, fullScreenMode, maxRefreshRate);

        Debug.Log($"{LOG_PREFIX} ✅ Ekran ayarları uygulandı: {resolution.width}x{resolution.height}, Mod={screenMode}, RefreshRate={maxRefreshRate}Hz");
    }

    private int FindMaxRefreshRate(Resolution targetResolution)
    {
        int maxRefreshRate = 60;

        foreach (Resolution res in _availableResolutions)
        {
            if (res.width == targetResolution.width &&
                res.height == targetResolution.height &&
                res.refreshRate > maxRefreshRate)
            {
                maxRefreshRate = res.refreshRate;
            }
        }

        return maxRefreshRate;
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

    private void ApplyFPSSettings(int fpsIndex, bool vSyncEnabled)
    {
        if (vSyncEnabled)
        {
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = -1;
        }
        else
        {
            QualitySettings.vSyncCount = 0;
            if (fpsIndex >= 0 && fpsIndex < fpsOptions.Length)
            {
                Application.targetFrameRate = fpsOptions[fpsIndex];
            }
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
        UpdateFPSDropdownInteractable(!_savedSettings.VSyncEnabled);
        UpdateAudioTexts();
    }

    private void UpdateDropdownsWithoutNotify()
    {
        qualityDropdown?.SetValueWithoutNotify(_savedSettings.QualityLevel);
        languageDropdown?.SetValueWithoutNotify(_savedSettings.LocaleID);
        screenModeDropdown?.SetValueWithoutNotify((int)_savedSettings.ScreenMode);
        resolutionDropdown?.SetValueWithoutNotify(_savedSettings.ResolutionIndex);
        fpsDropdown?.SetValueWithoutNotify(_savedSettings.FPSIndex);
    }

    private void UpdateToggleWithoutNotify()
    {
        vSyncToggle?.SetIsOnWithoutNotify(_savedSettings.VSyncEnabled);
    }

    private void UpdateSlidersWithoutNotify()
    {
        masterVolumeSlider?.SetValueWithoutNotify(_savedSettings.MasterVolume);
        musicVolumeSlider?.SetValueWithoutNotify(_savedSettings.MusicVolume);
        sfxVolumeSlider?.SetValueWithoutNotify(_savedSettings.SFXVolume);
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

    private void UpdateFPSDropdownInteractable(bool enabled)
    {
        if (fpsDropdown != null)
        {
            fpsDropdown.interactable = enabled;
        }
    }

    private void UpdateSaveButtonState()
    {
        if (saveButton == null) return;

        saveButton.interactable = _hasUnsavedChanges;

        ColorBlock colors = saveButton.colors;
        colors.normalColor = _hasUnsavedChanges ? Color.green : Color.gray;
        colors.highlightedColor = _hasUnsavedChanges ? Color.green * 0.8f : Color.gray * 0.8f;
        saveButton.colors = colors;
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

    private void SaveToPlayerPrefs()
    {
        PlayerPrefs.SetInt(PREF_QUALITY, _savedSettings.QualityLevel);
        PlayerPrefs.SetInt(PREF_LOCALE, _savedSettings.LocaleID);
        PlayerPrefs.SetInt(PREF_SCREEN_MODE, (int)_savedSettings.ScreenMode);
        PlayerPrefs.SetInt(PREF_RESOLUTION, _savedSettings.ResolutionIndex);
        PlayerPrefs.SetInt(PREF_FPS, _savedSettings.FPSIndex);
        PlayerPrefs.SetInt(PREF_VSYNC, _savedSettings.VSyncEnabled ? 1 : 0);
        PlayerPrefs.SetFloat(PREF_MASTER_VOLUME, _savedSettings.MasterVolume);
        PlayerPrefs.SetFloat(PREF_MUSIC_VOLUME, _savedSettings.MusicVolume);
        PlayerPrefs.SetFloat(PREF_SFX_VOLUME, _savedSettings.SFXVolume);
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
        qualityDropdown?.SetValueWithoutNotify(_selectedSettings.QualityLevel);
        languageDropdown?.SetValueWithoutNotify(_selectedSettings.LocaleID);
        screenModeDropdown?.SetValueWithoutNotify((int)_selectedSettings.ScreenMode);
        resolutionDropdown?.SetValueWithoutNotify(_selectedSettings.ResolutionIndex);
        fpsDropdown?.SetValueWithoutNotify(_selectedSettings.FPSIndex);
        vSyncToggle?.SetIsOnWithoutNotify(_selectedSettings.VSyncEnabled);
        masterVolumeSlider?.SetValueWithoutNotify(_selectedSettings.MasterVolume);
        musicVolumeSlider?.SetValueWithoutNotify(_selectedSettings.MusicVolume);
        sfxVolumeSlider?.SetValueWithoutNotify(_selectedSettings.SFXVolume);
    }

    private void ApplyAllSettingsFromSaved()
    {
        ApplyQualitySettings(_savedSettings.QualityLevel);
        StartCoroutine(ApplyLocalePreviewCoroutine(_savedSettings.LocaleID));
        ApplyResolutionAndScreenMode(_savedSettings.ResolutionIndex, _savedSettings.ScreenMode);
        UpdateFPSDropdownInteractable(!_savedSettings.VSyncEnabled);
        ApplyFPSSettings(_savedSettings.FPSIndex, _savedSettings.VSyncEnabled);
        ApplyAudioSettings();
        UpdateAudioTexts();
    }

    #endregion

    #region Change Detection

    private void CheckForChanges()
    {
        _hasUnsavedChanges = !_selectedSettings.Equals(_savedSettings);
        UpdateSaveButtonState();
    }

    #endregion

    #region Cleanup

    private void RemoveAllListeners()
    {
        qualityDropdown?.onValueChanged.RemoveAllListeners();
        languageDropdown?.onValueChanged.RemoveAllListeners();
        screenModeDropdown?.onValueChanged.RemoveAllListeners();
        resolutionDropdown?.onValueChanged.RemoveAllListeners();
        fpsDropdown?.onValueChanged.RemoveAllListeners();
        vSyncToggle?.onValueChanged.RemoveAllListeners();
        masterVolumeSlider?.onValueChanged.RemoveAllListeners();
        musicVolumeSlider?.onValueChanged.RemoveAllListeners();
        sfxVolumeSlider?.onValueChanged.RemoveAllListeners();
        saveButton?.onClick.RemoveAllListeners();
        backButton?.onClick.RemoveAllListeners();
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
    public string GetCurrentQualityName() => QualitySettings.names[_savedSettings.QualityLevel];

    /// <summary>
    /// Mevcut ekran modu adı
    /// </summary>
    public string GetCurrentScreenModeName() => GetLocalizedScreenModeName(_savedSettings.ScreenMode);

    /// <summary>
    /// Mevcut çözünürlük adı
    /// </summary>
    public string GetCurrentResolutionName()
    {
        if (_savedSettings.ResolutionIndex >= 0 && _savedSettings.ResolutionIndex < _filteredResolutions.Count)
        {
            return FormatResolutionDisplayName(_filteredResolutions[_savedSettings.ResolutionIndex]);
        }
        return "Unknown";
    }

    /// <summary>
    /// Mevcut FPS adı
    /// </summary>
    public string GetCurrentFPSName() => $"{fpsOptions[_savedSettings.FPSIndex]} FPS";

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

    public void OnBackButtonPressed()
    {
        HandleBackButtonPressed();
    }
    #endregion
}