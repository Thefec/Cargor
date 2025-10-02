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

    [Header("FPS Settings")]
    public TMP_Dropdown fpsDropdown;
    public Toggle vSyncToggle;

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

    // Screen Mode Enum
    public enum ScreenMode
    {
        Windowed = 0,
        FullscreenWindowed = 1,
        FullscreenExclusive = 2
    }

    // Current (saved) values
    private int currentQualityLevel;
    private int currentLocaleID;
    private ScreenMode currentScreenMode;
    private int currentFPSIndex;
    private bool currentVSyncEnabled;

    // Selected (preview - unsaved) values
    private int selectedQualityLevel;
    private int selectedLocaleID;
    private ScreenMode selectedScreenMode;
    private int selectedFPSIndex;
    private bool selectedVSyncEnabled;

    private bool hasUnsavedChanges = false;
    private bool localizationActive = false;

    private IEnumerator Start()
    {
        // Setup all dropdowns and buttons
        SetupQualityDropdown();
        SetupScreenModeDropdown();
        SetupFPSDropdown();
        SetupVSyncToggle();
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
        qualityDropdown.onValueChanged.AddListener(OnQualityChanged);
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
        languageDropdown.onValueChanged.AddListener(OnLanguageChanged);
    }

    void SetupScreenModeDropdown()
    {
        if (screenModeDropdown == null) return;

        screenModeDropdown.ClearOptions();
        screenModeDropdown.AddOptions(GetLocalizedScreenModeOptions());
        screenModeDropdown.onValueChanged.AddListener(OnScreenModeChanged);
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
        fpsDropdown.onValueChanged.AddListener(OnFPSChanged);
    }

    void SetupVSyncToggle()
    {
        if (vSyncToggle == null) return;
        vSyncToggle.onValueChanged.AddListener(OnVSyncChanged);
    }

    void SetupButtons()
    {
        if (saveButton != null)
        {
            saveButton.onClick.AddListener(SaveAllSettings);
        }

        if (backButton != null)
        {
            backButton.onClick.AddListener(OnBackButtonPressed);
        }

        UpdateSaveButtonState();
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
        ApplyScreenMode(currentScreenMode);

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
        Debug.Log($"Kalite deðiþtirildi: {QualitySettings.names[newQualityIndex]} (Kaydedilmedi)");
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
        ApplyScreenMode(selectedScreenMode);
        CheckForChanges();
        Debug.Log($"Ekran modu deðiþtirildi: {GetScreenModeName(selectedScreenMode)} (Kaydedilmedi)");
    }

    void OnFPSChanged(int index)
    {
        if (selectedVSyncEnabled) return;
        if (index < 0 || index >= fpsOptions.Length) return;

        selectedFPSIndex = index;
        ApplyFPSSettings(selectedFPSIndex, selectedVSyncEnabled);
        CheckForChanges();
        Debug.Log($"FPS deðiþtirildi: {fpsOptions[selectedFPSIndex]} (Kaydedilmedi)");
    }

    void OnVSyncChanged(bool isOn)
    {
        selectedVSyncEnabled = isOn;
        UpdateFPSDropdownInteractable(!selectedVSyncEnabled);
        ApplyFPSSettings(selectedFPSIndex, selectedVSyncEnabled);
        CheckForChanges();
        Debug.Log($"VSync deðiþtirildi: {selectedVSyncEnabled} (Kaydedilmedi)");
    }

    #endregion

    #region Apply Methods

    void ApplyScreenMode(ScreenMode mode)
    {
        switch (mode)
        {
            case ScreenMode.Windowed:
                Screen.fullScreenMode = FullScreenMode.Windowed;
                break;
            case ScreenMode.FullscreenWindowed:
                Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
                break;
            case ScreenMode.FullscreenExclusive:
                Screen.fullScreenMode = FullScreenMode.ExclusiveFullScreen;
                break;
        }
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
            RefreshScreenModeDropdown(); // Ekran modu dropdown'unu güncelle
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
            Debug.Log("Kaydedilecek deðiþiklik yok.");
            return;
        }

        // Save all settings
        currentQualityLevel = selectedQualityLevel;
        currentLocaleID = selectedLocaleID;
        currentScreenMode = selectedScreenMode;
        currentFPSIndex = selectedFPSIndex;
        currentVSyncEnabled = selectedVSyncEnabled;

        PlayerPrefs.SetInt(PREF_QUALITY, currentQualityLevel);
        PlayerPrefs.SetInt(PREF_LOCALE, currentLocaleID);
        PlayerPrefs.SetInt(PREF_SCREEN_MODE, (int)currentScreenMode);
        PlayerPrefs.SetInt(PREF_FPS, currentFPSIndex);
        PlayerPrefs.SetInt(PREF_VSYNC, currentVSyncEnabled ? 1 : 0);
        PlayerPrefs.Save();

        hasUnsavedChanges = false;
        UpdateSaveButtonState();

        Debug.Log("Tüm ayarlar kaydedildi!");
    }

    public void OnBackButtonPressed()
    {
        if (hasUnsavedChanges)
        {
            ResetToSavedSettings();
            Debug.Log("Kaydedilmemiþ deðiþiklikler geri alýndý.");
        }

        // Buraya menü kapatma kodunuzu ekleyebilirsiniz
    }

    void ResetToSavedSettings()
    {
        // Reset all selections to saved values
        selectedQualityLevel = currentQualityLevel;
        selectedLocaleID = currentLocaleID;
        selectedScreenMode = currentScreenMode;
        selectedFPSIndex = currentFPSIndex;
        selectedVSyncEnabled = currentVSyncEnabled;

        // Update UI (without triggering listeners)
        if (qualityDropdown != null)
            qualityDropdown.SetValueWithoutNotify(currentQualityLevel);
        if (languageDropdown != null)
            languageDropdown.SetValueWithoutNotify(currentLocaleID);
        if (screenModeDropdown != null)
            screenModeDropdown.SetValueWithoutNotify((int)currentScreenMode);
        if (fpsDropdown != null)
            fpsDropdown.SetValueWithoutNotify(currentFPSIndex);
        if (vSyncToggle != null)
            vSyncToggle.SetIsOnWithoutNotify(currentVSyncEnabled);

        // Reapply saved settings
        QualitySettings.SetQualityLevel(currentQualityLevel, true);
        ChangeLocalePreview(currentLocaleID);
        ApplyScreenMode(currentScreenMode);
        UpdateFPSDropdownInteractable(!currentVSyncEnabled);
        ApplyFPSSettings(currentFPSIndex, currentVSyncEnabled);

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
                           (selectedFPSIndex != currentFPSIndex) ||
                           (selectedVSyncEnabled != currentVSyncEnabled);

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

    public string GetCurrentFPSName()
    {
        return fpsOptions[currentFPSIndex] + " FPS";
    }

    #endregion

    void OnDisable()
    {
        if (hasUnsavedChanges)
        {
            ResetToSavedSettings();
            Debug.Log("Menü kapatýldý. Kaydedilmemiþ deðiþiklikler geri alýndý.");
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
        if (fpsDropdown != null)
            fpsDropdown.onValueChanged.RemoveAllListeners();
        if (vSyncToggle != null)
            vSyncToggle.onValueChanged.RemoveAllListeners();
        if (saveButton != null)
            saveButton.onClick.RemoveAllListeners();
        if (backButton != null)
            backButton.onClick.RemoveAllListeners();
    }
}