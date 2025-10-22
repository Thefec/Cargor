using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Localization.Settings;
using System.Collections;
using System.Collections.Generic;

public class UnifiedSettingsManager : MonoBehaviour
{
    [Header("UI Components - Shared")]
    public Button saveButton;
    public Button backButton;

    [Header("Quality Settings")]
    public TMP_Dropdown qualityDropdown;

    [Header("Language Settings")]
    public TMP_Dropdown languageDropdown;

    [Header("Screen Mode Settings")]
    public TMP_Dropdown screenModeDropdown;

    [Header("Resolution Settings")]
    public TMP_Dropdown resolutionDropdown;

    [Header("FPS Settings")]
    public TMP_Dropdown fpsDropdown;
    public Toggle vSyncToggle;

    [Header("Audio Settings")]
    public Slider masterVolumeSlider;
    public TextMeshProUGUI masterVolumeText;
    public Slider musicVolumeSlider;
    public TextMeshProUGUI musicVolumeText;
    public Slider sfxVolumeSlider;
    public TextMeshProUGUI sfxVolumeText;

    [Header("Audio Sources")]
    public AudioSource musicAudioSource;
    public AudioSource sfxAudioSource;
    public AudioSource uiAudioSource; // ✨ YENİ: UI sesleri için

    [Header("UI Sound Effects")] // ✨ YENİ BÖLÜM
    public AudioClip buttonClickSound;
    public AudioClip dropdownClickSound; // Dropdown için ayrı ses (opsiyonel)
    public AudioClip sliderChangeSound; // Slider için ses (opsiyonel)
    [Range(0f, 1f)]
    public float uiSoundVolume = 0.8f;

    [Header("FPS Options")]
    public int[] fpsOptions = { 30, 60, 90, 120, 140, 160 };
    public int defaultFPSIndex = 1; // 60 FPS default
    public bool defaultVSyncEnabled = true;

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

    // Screen Mode Enum
    public enum ScreenMode
    {
        Windowed = 0,
        FullscreenWindowed = 1,
        FullscreenExclusive = 2
    }

    // Resolution data
    private Resolution[] availableResolutions;
    private List<Resolution> filteredResolutions;

    // Current (saved) values
    private int currentQualityLevel;
    private int currentLocaleID;
    private ScreenMode currentScreenMode;
    private int currentFPSIndex;
    private bool currentVSyncEnabled;
    private int currentResolutionIndex;
    private float currentMasterVolume;
    private float currentMusicVolume;
    private float currentSFXVolume;

    // Selected (preview - unsaved) values
    private int selectedQualityLevel;
    private int selectedLocaleID;
    private ScreenMode selectedScreenMode;
    private int selectedFPSIndex;
    private bool selectedVSyncEnabled;
    private int selectedResolutionIndex;
    private float selectedMasterVolume;
    private float selectedMusicVolume;
    private float selectedSFXVolume;

    private bool hasUnsavedChanges = false;
    private bool localizationActive = false;

    // ✨ YENİ: Slider ses throttling için
    private float lastSliderSoundTime = 0f;
    private const float sliderSoundCooldown = 0.1f;

    private IEnumerator Start()
    {
        // Setup all dropdowns and buttons
        SetupQualityDropdown();
        SetupScreenModeDropdown();
        SetupResolutionDropdown();
        SetupFPSDropdown();
        SetupVSyncToggle();
        SetupAudioSliders();
        SetupLanguageDropdown();
        SetupButtons();

        // Wait for localization system
        yield return new WaitUntil(() =>
            LocalizationSettings.InitializationOperation.IsValid() &&
            LocalizationSettings.InitializationOperation.IsDone);

        // Load all saved settings
        LoadAllSettings();
    }

    #region Setup Methods

    void SetupQualityDropdown()
    {
        if (qualityDropdown == null) return;

        qualityDropdown.ClearOptions();
        List<string> qualityOptions = new List<string>();
        string[] qualityNames = QualitySettings.names;

        for (int i = 0; i < qualityNames.Length; i++)
        {
            qualityOptions.Add(qualityNames[i]);
        }

        qualityDropdown.AddOptions(qualityOptions);
        qualityDropdown.onValueChanged.AddListener((value) => {
            PlayUISound(dropdownClickSound ?? buttonClickSound); // ✨ Ses efekti
            OnQualityChanged(value);
        });
    }

    void SetupLanguageDropdown()
    {
        if (languageDropdown == null) return;

        languageDropdown.ClearOptions();
        List<string> languageNames = new List<string> { "Türkçe", "English" };

        foreach (string languageName in languageNames)
        {
            languageDropdown.options.Add(new TMP_Dropdown.OptionData(languageName));
        }

        languageDropdown.RefreshShownValue();
        languageDropdown.onValueChanged.AddListener((value) => {
            PlayUISound(dropdownClickSound ?? buttonClickSound); // ✨ Ses efekti
            OnLanguageChanged(value);
        });
    }

    void SetupScreenModeDropdown()
    {
        if (screenModeDropdown == null) return;

        screenModeDropdown.ClearOptions();
        screenModeDropdown.AddOptions(GetLocalizedScreenModeOptions());
        screenModeDropdown.onValueChanged.AddListener((value) => {
            PlayUISound(dropdownClickSound ?? buttonClickSound); // ✨ Ses efekti
            OnScreenModeChanged(value);
        });
    }

    void SetupResolutionDropdown()
    {
        if (resolutionDropdown == null) return;

        // Tüm çözünürlükleri al
        availableResolutions = Screen.resolutions;
        filteredResolutions = new List<Resolution>();

        // Benzersiz çözünürlükleri filtrele
        Resolution previousResolution = new Resolution();
        foreach (Resolution resolution in availableResolutions)
        {
            if (resolution.width != previousResolution.width ||
                resolution.height != previousResolution.height)
            {
                filteredResolutions.Add(resolution);
                previousResolution = resolution;
            }
        }

        // Dropdown'u doldur
        resolutionDropdown.ClearOptions();
        List<string> options = new List<string>();

        foreach (Resolution resolution in filteredResolutions)
        {
            string resolutionString = GetResolutionDisplayName(resolution);
            options.Add(resolutionString);
        }

        resolutionDropdown.AddOptions(options);
        resolutionDropdown.onValueChanged.AddListener((value) => {
            PlayUISound(dropdownClickSound ?? buttonClickSound); // ✨ Ses efekti
            OnResolutionChanged(value);
        });
    }

    List<string> GetLocalizedScreenModeOptions()
    {
        bool isTurkish = LocalizationSettings.SelectedLocale != null &&
                        LocalizationSettings.SelectedLocale.Identifier.Code == "tr";

        if (isTurkish)
        {
            return new List<string>
            {
                "Pencere Modu",
                "Tam Ekran (Çerçeveli)",
                "Tam Ekran (Çerçevesiz)"
            };
        }
        else
        {
            return new List<string>
            {
                "Windowed",
                "Fullscreen (Windowed)",
                "Fullscreen (Exclusive)"
            };
        }
    }

    void SetupFPSDropdown()
    {
        if (fpsDropdown == null) return;

        fpsDropdown.ClearOptions();
        List<string> options = new List<string>();
        foreach (int fps in fpsOptions)
            options.Add(fps + " FPS");

        fpsDropdown.AddOptions(options);
        fpsDropdown.onValueChanged.AddListener((value) => {
            PlayUISound(dropdownClickSound ?? buttonClickSound); // ✨ Ses efekti
            OnFPSChanged(value);
        });
    }

    void SetupVSyncToggle()
    {
        if (vSyncToggle == null) return;
        vSyncToggle.onValueChanged.AddListener((value) => {
            PlayUISound(buttonClickSound); // ✨ Ses efekti
            OnVSyncChanged(value);
        });
    }

    void SetupAudioSliders()
    {
        // Master Volume Slider
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.minValue = 0f;
            masterVolumeSlider.maxValue = 1f;
            masterVolumeSlider.onValueChanged.AddListener((value) => {
                PlaySliderSound(); // ✨ Slider sesi (throttled)
                OnMasterVolumeChanged(value);
            });
        }

        // Music Volume Slider
        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.minValue = 0f;
            musicVolumeSlider.maxValue = 1f;
            musicVolumeSlider.onValueChanged.AddListener((value) => {
                PlaySliderSound(); // ✨ Slider sesi (throttled)
                OnMusicVolumeChanged(value);
            });
        }

        // SFX Volume Slider
        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.minValue = 0f;
            sfxVolumeSlider.maxValue = 1f;
            sfxVolumeSlider.onValueChanged.AddListener((value) => {
                PlaySliderSound(); // ✨ Slider sesi (throttled)
                OnSFXVolumeChanged(value);
            });
        }
    }

    void SetupButtons()
    {
        if (saveButton != null)
        {
            saveButton.onClick.AddListener(() => {
                PlayUISound(buttonClickSound); // ✨ Ses efekti
                SaveAllSettings();
            });
        }

        if (backButton != null)
        {
            backButton.onClick.AddListener(() => {
                PlayUISound(buttonClickSound); // ✨ Ses efekti
                OnBackButtonPressed();
            });
        }

        UpdateSaveButtonState();
    }

    #endregion

    #region UI Sound Effects ✨ YENİ

    /// <summary>
    /// Buton ve dropdown sesleri için
    /// </summary>
    void PlayUISound(AudioClip clip)
    {
        if (clip == null) return;

        // SFX ve Master volume'ü kullan
        float finalVolume = uiSoundVolume * selectedSFXVolume * selectedMasterVolume;

        if (uiAudioSource != null)
        {
            uiAudioSource.PlayOneShot(clip, finalVolume);
        }
        else if (sfxAudioSource != null)
        {
            sfxAudioSource.PlayOneShot(clip, finalVolume);
        }
        else
        {
            // Geçici AudioSource oluştur
            AudioSource.PlayClipAtPoint(clip, Camera.main.transform.position, finalVolume);
        }
    }

    /// <summary>
    /// Slider değişimleri için (çok sık çalmayı önlemek için throttled)
    /// </summary>
    void PlaySliderSound()
    {
        // Çok sık ses çalmasın diye cooldown
        if (Time.time - lastSliderSoundTime < sliderSoundCooldown)
            return;

        lastSliderSoundTime = Time.time;

        // Slider ses varsa onu çal, yoksa button sesini kullan
        AudioClip clipToPlay = sliderChangeSound != null ? sliderChangeSound : buttonClickSound;

        if (clipToPlay == null) return;

        // Slider sesi daha düşük olsun
        float finalVolume = uiSoundVolume * 0.3f * selectedSFXVolume * selectedMasterVolume;

        if (uiAudioSource != null)
        {
            uiAudioSource.PlayOneShot(clipToPlay, finalVolume);
        }
        else if (sfxAudioSource != null)
        {
            sfxAudioSource.PlayOneShot(clipToPlay, finalVolume);
        }
    }

    #endregion

    #region Load Settings

    void LoadAllSettings()
    {
        // Load Quality
        int savedQuality = PlayerPrefs.GetInt(PREF_QUALITY, QualitySettings.GetQualityLevel());
        savedQuality = Mathf.Clamp(savedQuality, 0, QualitySettings.names.Length - 1);
        currentQualityLevel = savedQuality;
        selectedQualityLevel = savedQuality;
        if (qualityDropdown != null)
            qualityDropdown.SetValueWithoutNotify(savedQuality);
        QualitySettings.SetQualityLevel(savedQuality, true);

        // Load Language
        currentLocaleID = PlayerPrefs.GetInt(PREF_LOCALE, 0);
        currentLocaleID = Mathf.Clamp(currentLocaleID, 0, LocalizationSettings.AvailableLocales.Locales.Count - 1);
        selectedLocaleID = currentLocaleID;
        if (languageDropdown != null)
            languageDropdown.SetValueWithoutNotify(currentLocaleID);
        ChangeLocalePreview(currentLocaleID);

        // Load Screen Mode
        int savedScreenMode = PlayerPrefs.GetInt(PREF_SCREEN_MODE, (int)ScreenMode.FullscreenExclusive);
        currentScreenMode = (ScreenMode)savedScreenMode;
        selectedScreenMode = currentScreenMode;
        if (screenModeDropdown != null)
            screenModeDropdown.SetValueWithoutNotify((int)currentScreenMode);

        // Load Resolution
        int savedResolutionIndex = PlayerPrefs.GetInt(PREF_RESOLUTION, GetCurrentResolutionIndex());
        savedResolutionIndex = Mathf.Clamp(savedResolutionIndex, 0, filteredResolutions.Count - 1);
        currentResolutionIndex = savedResolutionIndex;
        selectedResolutionIndex = savedResolutionIndex;
        if (resolutionDropdown != null)
            resolutionDropdown.SetValueWithoutNotify(currentResolutionIndex);

        // Apply Screen Mode and Resolution
        ApplyResolutionAndScreenMode(currentResolutionIndex, currentScreenMode);

        // Load FPS Settings
        currentFPSIndex = PlayerPrefs.GetInt(PREF_FPS, defaultFPSIndex);
        currentFPSIndex = Mathf.Clamp(currentFPSIndex, 0, fpsOptions.Length - 1);
        currentVSyncEnabled = PlayerPrefs.GetInt(PREF_VSYNC, defaultVSyncEnabled ? 1 : 0) == 1;
        selectedFPSIndex = currentFPSIndex;
        selectedVSyncEnabled = currentVSyncEnabled;

        if (fpsDropdown != null)
            fpsDropdown.SetValueWithoutNotify(currentFPSIndex);
        if (vSyncToggle != null)
            vSyncToggle.SetIsOnWithoutNotify(currentVSyncEnabled);

        UpdateFPSDropdownInteractable(!currentVSyncEnabled);
        ApplyFPSSettings(currentFPSIndex, currentVSyncEnabled);

        // Load Audio Settings
        currentMasterVolume = PlayerPrefs.GetFloat(PREF_MASTER_VOLUME, 0.5f);
        currentMusicVolume = PlayerPrefs.GetFloat(PREF_MUSIC_VOLUME, 0.5f);
        currentSFXVolume = PlayerPrefs.GetFloat(PREF_SFX_VOLUME, 0.5f);

        selectedMasterVolume = currentMasterVolume;
        selectedMusicVolume = currentMusicVolume;
        selectedSFXVolume = currentSFXVolume;

        if (masterVolumeSlider != null)
            masterVolumeSlider.SetValueWithoutNotify(currentMasterVolume);
        if (musicVolumeSlider != null)
            musicVolumeSlider.SetValueWithoutNotify(currentMusicVolume);
        if (sfxVolumeSlider != null)
            sfxVolumeSlider.SetValueWithoutNotify(currentSFXVolume);

        ApplyAudioSettings(currentMasterVolume, currentMusicVolume, currentSFXVolume);
        UpdateAudioTexts();

        hasUnsavedChanges = false;
        UpdateSaveButtonState();

        Debug.Log("Tüm ayarlar yüklendi.");
    }

    #endregion

    #region Change Handlers

    void OnQualityChanged(int newQualityIndex)
    {
        selectedQualityLevel = newQualityIndex;
        QualitySettings.SetQualityLevel(newQualityIndex, true);
        CheckForChanges();
        Debug.Log($"Kalite değiştirildi: {QualitySettings.names[newQualityIndex]} (Kaydedilmedi)");
    }

    void OnLanguageChanged(int localeID)
    {
        selectedLocaleID = localeID;
        ChangeLocalePreview(localeID);
        CheckForChanges();
    }

    void OnScreenModeChanged(int modeIndex)
    {
        if (modeIndex < 0 || modeIndex > 2) return;

        selectedScreenMode = (ScreenMode)modeIndex;
        ApplyResolutionAndScreenMode(selectedResolutionIndex, selectedScreenMode);
        CheckForChanges();
        Debug.Log($"Ekran modu değiştirildi: {GetScreenModeName(selectedScreenMode)} (Kaydedilmedi)");
    }

    void OnResolutionChanged(int index)
    {
        if (index < 0 || index >= filteredResolutions.Count) return;

        selectedResolutionIndex = index;
        ApplyResolutionAndScreenMode(selectedResolutionIndex, selectedScreenMode);
        CheckForChanges();
        Debug.Log($"Çözünürlük değiştirildi: {GetResolutionDisplayName(filteredResolutions[selectedResolutionIndex])} (Kaydedilmedi)");
    }

    void OnFPSChanged(int index)
    {
        if (selectedVSyncEnabled) return;
        if (index < 0 || index >= fpsOptions.Length) return;

        selectedFPSIndex = index;
        ApplyFPSSettings(selectedFPSIndex, selectedVSyncEnabled);
        CheckForChanges();
        Debug.Log($"FPS değiştirildi: {fpsOptions[selectedFPSIndex]} (Kaydedilmedi)");
    }

    void OnVSyncChanged(bool isOn)
    {
        selectedVSyncEnabled = isOn;
        UpdateFPSDropdownInteractable(!selectedVSyncEnabled);
        ApplyFPSSettings(selectedFPSIndex, selectedVSyncEnabled);
        CheckForChanges();
        Debug.Log($"VSync değiştirildi: {selectedVSyncEnabled} (Kaydedilmedi)");
    }

    void OnMasterVolumeChanged(float volume)
    {
        selectedMasterVolume = volume;
        ApplyAudioSettings(selectedMasterVolume, selectedMusicVolume, selectedSFXVolume);
        UpdateAudioTexts();
        CheckForChanges();
    }

    void OnMusicVolumeChanged(float volume)
    {
        selectedMusicVolume = volume;
        ApplyAudioSettings(selectedMasterVolume, selectedMusicVolume, selectedSFXVolume);
        UpdateAudioTexts();
        CheckForChanges();
    }

    void OnSFXVolumeChanged(float volume)
    {
        selectedSFXVolume = volume;
        ApplyAudioSettings(selectedMasterVolume, selectedMusicVolume, selectedSFXVolume);
        UpdateAudioTexts();
        CheckForChanges();
    }

    #endregion

    #region Apply Methods

    void ApplyResolutionAndScreenMode(int resolutionIndex, ScreenMode screenMode)
    {
        if (resolutionIndex < 0 || resolutionIndex >= filteredResolutions.Count) return;

        Resolution resolution = filteredResolutions[resolutionIndex];

        // En yüksek refresh rate'i bul
        int maxRefreshRate = 60;
        foreach (Resolution res in availableResolutions)
        {
            if (res.width == resolution.width && res.height == resolution.height)
            {
                if (res.refreshRate > maxRefreshRate)
                    maxRefreshRate = res.refreshRate;
            }
        }

        // FullScreenMode'u belirle
        FullScreenMode fullScreenMode = FullScreenMode.ExclusiveFullScreen;
        switch (screenMode)
        {
            case ScreenMode.Windowed:
                fullScreenMode = FullScreenMode.Windowed;
                break;
            case ScreenMode.FullscreenWindowed:
                fullScreenMode = FullScreenMode.FullScreenWindow;
                break;
            case ScreenMode.FullscreenExclusive:
                fullScreenMode = FullScreenMode.ExclusiveFullScreen;
                break;
        }

        Screen.SetResolution(resolution.width, resolution.height, fullScreenMode, maxRefreshRate);
        Debug.Log($"✅ Ayarlar uygulandı: Çözünürlük={resolution.width}x{resolution.height}, Mod={screenMode}, RefreshRate={maxRefreshRate}Hz");
    }

    void ApplyFPSSettings(int fpsIndex, bool vSyncEnabled)
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

    void ApplyAudioSettings(float masterVolume, float musicVolume, float sfxVolume)
    {
        // Unity AudioListener master volume
        AudioListener.volume = masterVolume;

        // Music AudioSource
        if (musicAudioSource != null)
        {
            musicAudioSource.volume = musicVolume * masterVolume;
        }

        // SFX AudioSource
        if (sfxAudioSource != null)
        {
            sfxAudioSource.volume = sfxVolume * masterVolume;
        }
    }

    void UpdateAudioTexts()
    {
        if (masterVolumeText != null)
            masterVolumeText.text = Mathf.RoundToInt(selectedMasterVolume * 100).ToString();

        if (musicVolumeText != null)
            musicVolumeText.text = Mathf.RoundToInt(selectedMusicVolume * 100).ToString();

        if (sfxVolumeText != null)
            sfxVolumeText.text = Mathf.RoundToInt(selectedSFXVolume * 100).ToString();
    }

    void ChangeLocalePreview(int localeID)
    {
        if (localizationActive) return;
        StartCoroutine(SetLocalePreview(localeID));
    }

    private IEnumerator SetLocalePreview(int _localeID)
    {
        localizationActive = true;

        yield return new WaitUntil(() =>
            LocalizationSettings.InitializationOperation.IsValid() &&
            LocalizationSettings.InitializationOperation.IsDone);

        var locales = LocalizationSettings.AvailableLocales.Locales;
        if (_localeID >= 0 && _localeID < locales.Count)
        {
            LocalizationSettings.SelectedLocale = locales[_localeID];
            yield return new WaitForEndOfFrame();
            RefreshLocalizedUI();
            RefreshScreenModeDropdown();
        }

        localizationActive = false;
    }

    private void RefreshScreenModeDropdown()
    {
        if (screenModeDropdown == null) return;

        int currentValue = screenModeDropdown.value;
        screenModeDropdown.ClearOptions();
        screenModeDropdown.AddOptions(GetLocalizedScreenModeOptions());
        screenModeDropdown.SetValueWithoutNotify(currentValue);
    }

    private void RefreshLocalizedUI()
    {
        var localizedComponents = FindObjectsOfType<UnityEngine.Localization.Components.LocalizeStringEvent>();
        foreach (var component in localizedComponents)
        {
            component.RefreshString();
        }
    }

    #endregion

    #region Save & Reset

    public void SaveAllSettings()
    {
        if (!hasUnsavedChanges)
        {
            Debug.Log("Kaydedilecek değişiklik yok.");
            return;
        }

        // Save all settings
        currentQualityLevel = selectedQualityLevel;
        currentLocaleID = selectedLocaleID;
        currentScreenMode = selectedScreenMode;
        currentResolutionIndex = selectedResolutionIndex;
        currentFPSIndex = selectedFPSIndex;
        currentVSyncEnabled = selectedVSyncEnabled;
        currentMasterVolume = selectedMasterVolume;
        currentMusicVolume = selectedMusicVolume;
        currentSFXVolume = selectedSFXVolume;

        PlayerPrefs.SetInt(PREF_QUALITY, currentQualityLevel);
        PlayerPrefs.SetInt(PREF_LOCALE, currentLocaleID);
        PlayerPrefs.SetInt(PREF_SCREEN_MODE, (int)currentScreenMode);
        PlayerPrefs.SetInt(PREF_RESOLUTION, currentResolutionIndex);
        PlayerPrefs.SetInt(PREF_FPS, currentFPSIndex);
        PlayerPrefs.SetInt(PREF_VSYNC, currentVSyncEnabled ? 1 : 0);
        PlayerPrefs.SetFloat(PREF_MASTER_VOLUME, currentMasterVolume);
        PlayerPrefs.SetFloat(PREF_MUSIC_VOLUME, currentMusicVolume);
        PlayerPrefs.SetFloat(PREF_SFX_VOLUME, currentSFXVolume);
        PlayerPrefs.Save();

        hasUnsavedChanges = false;
        UpdateSaveButtonState();

        Debug.Log($"✅ Tüm ayarlar kaydedildi! Master: {currentMasterVolume:F2}, Music: {currentMusicVolume:F2}, SFX: {currentSFXVolume:F2}");
    }

    public void OnBackButtonPressed()
    {
        if (hasUnsavedChanges)
        {
            ResetToSavedSettings();
            Debug.Log("Kaydedilmemiş değişiklikler geri alındı.");
        }
    }

    void ResetToSavedSettings()
    {
        // Reset all selections to saved values
        selectedQualityLevel = currentQualityLevel;
        selectedLocaleID = currentLocaleID;
        selectedScreenMode = currentScreenMode;
        selectedResolutionIndex = currentResolutionIndex;
        selectedFPSIndex = currentFPSIndex;
        selectedVSyncEnabled = currentVSyncEnabled;
        selectedMasterVolume = currentMasterVolume;
        selectedMusicVolume = currentMusicVolume;
        selectedSFXVolume = currentSFXVolume;

        // Update UI (without triggering listeners)
        if (qualityDropdown != null)
            qualityDropdown.SetValueWithoutNotify(currentQualityLevel);
        if (languageDropdown != null)
            languageDropdown.SetValueWithoutNotify(currentLocaleID);
        if (screenModeDropdown != null)
            screenModeDropdown.SetValueWithoutNotify((int)currentScreenMode);
        if (resolutionDropdown != null)
            resolutionDropdown.SetValueWithoutNotify(currentResolutionIndex);
        if (fpsDropdown != null)
            fpsDropdown.SetValueWithoutNotify(currentFPSIndex);
        if (vSyncToggle != null)
            vSyncToggle.SetIsOnWithoutNotify(currentVSyncEnabled);
        if (masterVolumeSlider != null)
            masterVolumeSlider.SetValueWithoutNotify(currentMasterVolume);
        if (musicVolumeSlider != null)
            musicVolumeSlider.SetValueWithoutNotify(currentMusicVolume);
        if (sfxVolumeSlider != null)
            sfxVolumeSlider.SetValueWithoutNotify(currentSFXVolume);

        // Reapply saved settings
        QualitySettings.SetQualityLevel(currentQualityLevel, true);
        ChangeLocalePreview(currentLocaleID);
        ApplyResolutionAndScreenMode(currentResolutionIndex, currentScreenMode);
        UpdateFPSDropdownInteractable(!currentVSyncEnabled);
        ApplyFPSSettings(currentFPSIndex, currentVSyncEnabled);
        ApplyAudioSettings(currentMasterVolume, currentMusicVolume, currentSFXVolume);
        UpdateAudioTexts();

        hasUnsavedChanges = false;
        UpdateSaveButtonState();
    }

    #endregion

    #region Helper Methods

    void CheckForChanges()
    {
        hasUnsavedChanges = (selectedQualityLevel != currentQualityLevel) ||
                           (selectedLocaleID != currentLocaleID) ||
                           (selectedScreenMode != currentScreenMode) ||
                           (selectedResolutionIndex != currentResolutionIndex) ||
                           (selectedFPSIndex != currentFPSIndex) ||
                           (selectedVSyncEnabled != currentVSyncEnabled) ||
                           !Mathf.Approximately(selectedMasterVolume, currentMasterVolume) ||
                           !Mathf.Approximately(selectedMusicVolume, currentMusicVolume) ||
                           !Mathf.Approximately(selectedSFXVolume, currentSFXVolume);

        UpdateSaveButtonState();
    }

    void UpdateSaveButtonState()
    {
        if (saveButton != null)
        {
            saveButton.interactable = hasUnsavedChanges;

            ColorBlock colors = saveButton.colors;
            if (hasUnsavedChanges)
            {
                colors.normalColor = Color.green;
                colors.highlightedColor = Color.green * 0.8f;
            }
            else
            {
                colors.normalColor = Color.gray;
                colors.highlightedColor = Color.gray * 0.8f;
            }
            saveButton.colors = colors;
        }
    }

    void UpdateFPSDropdownInteractable(bool enabled)
    {
        if (fpsDropdown != null)
            fpsDropdown.interactable = enabled;
    }

    string GetScreenModeName(ScreenMode mode)
    {
        bool isTurkish = LocalizationSettings.SelectedLocale != null &&
                        LocalizationSettings.SelectedLocale.Identifier.Code == "tr";

        if (isTurkish)
        {
            switch (mode)
            {
                case ScreenMode.Windowed: return "Pencere Modu";
                case ScreenMode.FullscreenWindowed: return "Tam Ekran (Çerçeveli)";
                case ScreenMode.FullscreenExclusive: return "Tam Ekran (Çerçevesiz)";
                default: return "Bilinmeyen";
            }
        }
        else
        {
            switch (mode)
            {
                case ScreenMode.Windowed: return "Windowed";
                case ScreenMode.FullscreenWindowed: return "Fullscreen (Windowed)";
                case ScreenMode.FullscreenExclusive: return "Fullscreen (Exclusive)";
                default: return "Unknown";
            }
        }
    }

    string GetResolutionDisplayName(Resolution resolution)
    {
        string aspectRatio = GetAspectRatio(resolution.width, resolution.height);
        string qualityLabel = GetResolutionQualityLabel(resolution.height);

        return $"{resolution.width} x {resolution.height} {qualityLabel} ({aspectRatio})";
    }

    string GetAspectRatio(int width, int height)
    {
        int gcd = GCD(width, height);
        int ratioWidth = width / gcd;
        int ratioHeight = height / gcd;

        return $"{ratioWidth}:{ratioHeight}";
    }

    int GCD(int a, int b)
    {
        while (b != 0)
        {
            int temp = b;
            b = a % b;
            a = temp;
        }
        return a;
    }

    string GetResolutionQualityLabel(int height)
    {
        if (height >= 2160) return "[4K]";
        if (height >= 1440) return "[2K]";
        if (height >= 1080) return "[Full HD]";
        if (height >= 720) return "[HD]";
        return "";
    }

    int GetCurrentResolutionIndex()
    {
        Resolution currentRes = Screen.currentResolution;

        for (int i = 0; i < filteredResolutions.Count; i++)
        {
            if (filteredResolutions[i].width == currentRes.width &&
                filteredResolutions[i].height == currentRes.height)
            {
                return i;
            }
        }

        return filteredResolutions.Count - 1;
    }

    #endregion

    #region Public API

    public bool HasUnsavedChanges()
    {
        return hasUnsavedChanges;
    }

    public string GetCurrentQualityName()
    {
        return QualitySettings.names[currentQualityLevel];
    }

    public string GetCurrentScreenModeName()
    {
        return GetScreenModeName(currentScreenMode);
    }

    public string GetCurrentResolutionName()
    {
        if (currentResolutionIndex >= 0 && currentResolutionIndex < filteredResolutions.Count)
            return GetResolutionDisplayName(filteredResolutions[currentResolutionIndex]);
        return "Unknown";
    }

    public string GetCurrentFPSName()
    {
        return fpsOptions[currentFPSIndex] + " FPS";
    }

    public float GetMasterVolume()
    {
        return currentMasterVolume;
    }

    public float GetMusicVolume()
    {
        return currentMusicVolume;
    }

    public float GetSFXVolume()
    {
        return currentSFXVolume;
    }

    #endregion

    void OnDisable()
    {
        if (hasUnsavedChanges)
        {
            ResetToSavedSettings();
            Debug.Log("Menü kapatıldı. Kaydedilmemiş değişiklikler geri alındı.");
        }
    }

    void OnDestroy()
    {
        // Remove all listeners
        if (qualityDropdown != null)
            qualityDropdown.onValueChanged.RemoveAllListeners();
        if (languageDropdown != null)
            languageDropdown.onValueChanged.RemoveAllListeners();
        if (screenModeDropdown != null)
            screenModeDropdown.onValueChanged.RemoveAllListeners();
        if (resolutionDropdown != null)
            resolutionDropdown.onValueChanged.RemoveAllListeners();
        if (fpsDropdown != null)
            fpsDropdown.onValueChanged.RemoveAllListeners();
        if (vSyncToggle != null)
            vSyncToggle.onValueChanged.RemoveAllListeners();
        if (masterVolumeSlider != null)
            masterVolumeSlider.onValueChanged.RemoveAllListeners();
        if (musicVolumeSlider != null)
            musicVolumeSlider.onValueChanged.RemoveAllListeners();
        if (sfxVolumeSlider != null)
            sfxVolumeSlider.onValueChanged.RemoveAllListeners();
        if (saveButton != null)
            saveButton.onClick.RemoveAllListeners();
        if (backButton != null)
            backButton.onClick.RemoveAllListeners();
    }
}