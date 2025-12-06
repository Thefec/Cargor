using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using NewCss;

/// <summary>
/// Tutorial yönetim sistemi - adım adım tutorial akışı, UI yönetimi ve koşul kontrollerini sağlar. 
/// Typewriter efekti, highlight sistemi, kapı entegrasyonu ve çoklu dil desteği içerir.
/// </summary>
public class TutorialManager : NetworkBehaviour
{
    #region Constants

    private const string LOG_PREFIX = "[TutorialManager]";
    private const float PLAYER_SEARCH_INTERVAL = 0.5f;
    private const int MAX_PLAYER_SEARCH_ATTEMPTS = 20;
    private const float CONDITION_CHECK_INTERVAL = 0.1f;
    private const float STEP_TRANSITION_DELAY = 0.3f;
    private const float TUTORIAL_START_DELAY = 1f;

    private const float TEXT_FONT_SIZE_MIN = 18f;
    private const float TEXT_FONT_SIZE_MAX = 36f;

    private const float PUNCTUATION_DELAY_MULTIPLIER = 8f;
    private const float COMMA_DELAY_MULTIPLIER = 4f;
    private const float SPACE_DELAY_MULTIPLIER = 0.5f;

    private const float HIGHLIGHT_OUTLINE_WIDTH = 5f;

    private const string TURKISH_LOCALE_CODE = "tr";

    #endregion

    #region Singleton

    public static TutorialManager Instance { get; private set; }

    #endregion

    #region Serialized Fields - Settings

    [Header("=== TUTORIAL SETTINGS ===")]
    [SerializeField, Tooltip("Bu seviye tutorial mu?")]
    private bool isTutorialLevel = true;

    [SerializeField, Tooltip("Tutorial adımları")]
    private List<TutorialStep> tutorialSteps = new();

    #endregion

    #region Serialized Fields - Door Management

    [Header("=== DOOR MANAGEMENT ===")]
    [SerializeField, Tooltip("Tutorial kapıları")]
    private List<TutorialDoor> tutorialDoors = new();

    #endregion

    #region Serialized Fields - UI

    [Header("=== UI REFERENCES ===")]
    [SerializeField, Tooltip("Tutorial UI paneli")]
    private GameObject tutorialUI;

    [SerializeField, Tooltip("Talimat text'i")]
    private TextMeshProUGUI instructionText;

    [SerializeField, Tooltip("Tutorial canvas group")]
    private CanvasGroup tutorialCanvasGroup;

    [SerializeField, Tooltip("Fade hızı")]
    private float fadeSpeed = 2f;

    #endregion

    #region Serialized Fields - Typewriter

    [Header("=== TYPEWRITER EFFECT ===")]
    [SerializeField, Tooltip("Typewriter efekti aktif")]
    private bool enableTypewriterEffect = true;

    [SerializeField, Tooltip("Typewriter hızı")]
    private float typewriterSpeed = 0.05f;

    [SerializeField, Tooltip("Yazma sesi")]
    private AudioClip typingSound;

    [SerializeField, Tooltip("Ses kaynağı")]
    private AudioSource typingSoundSource;

    [SerializeField, Range(0f, 1f), Tooltip("Yazma sesi seviyesi")]
    private float typingSoundVolume = 0.3f;

    #endregion

    #region Serialized Fields - Skip Settings

    [Header("=== SKIP SETTINGS ===")]
    [SerializeField, Tooltip("Geçme tuşu")]
    private KeyCode skipKey = KeyCode.Space;

    [SerializeField, Tooltip("Geçme ipucunu göster")]
    private bool showSkipHint = true;

    [SerializeField, Tooltip("Geçme ipucu text'i")]
    private TextMeshProUGUI skipHintText;

    [SerializeField, Tooltip("Geçme ipucu mesajı - Türkçe")]
    private string skipHintMessageTR = "Geçmek için [SPACE] tuşuna basın";

    [SerializeField, Tooltip("Geçme ipucu mesajı - İngilizce")]
    private string skipHintMessageEN = "Press [SPACE] to skip";

    #endregion

    #region Serialized Fields - References

    [Header("=== PLAYER REFERENCE ===")]
    [SerializeField, Tooltip("Oyuncu envanteri")]
    private PlayerInventory playerInventory;

    #endregion

    #region Serialized Fields - Visual Helpers

    [Header("=== VISUAL HELPERS ===")]
    [SerializeField, Tooltip("Highlight prefab'ı")]
    private GameObject highlightPrefab;

    [SerializeField, Tooltip("Highlight rengi")]
    private Color highlightColor = Color.yellow;

    #endregion

    #region Serialized Fields - Localization

    [Header("=== LOCALIZATION ===")]
    [SerializeField, Tooltip("Tutorial tamamlandı mesajı - Türkçe")]
    private string tutorialCompletedMessageTR = "Tutorial tamamlandı! ";

    [SerializeField, Tooltip("Tutorial tamamlandı mesajı - İngilizce")]
    private string tutorialCompletedMessageEN = "Tutorial completed!";

    #endregion

    #region Serialized Fields - Debug

    [Header("=== DEBUG ===")]
    [SerializeField, Tooltip("Debug loglarını göster")]
    private bool showDebugLogs = true;

    #endregion

    #region Private Fields - State

    private int _currentStepIndex;
    private TutorialStep _currentStep;
    private bool _isTransitioning;
    private GameObject _currentHighlight;
    private bool _isTurkish = true;

    #endregion

    #region Private Fields - Typewriter

    private bool _isTyping;
    private bool _skipTyping;
    private Coroutine _currentTypewriterCoroutine;

    #endregion

    #region Private Fields - Conditions

    private bool _tableInteractionCompleted;
    private bool _shelfInteractionCompleted;
    private bool _shelfPlacementCompleted;
    private bool _truckDeliveryCompleted;
    private NetworkedShelf.BoxType _lastTakenBoxType;
    private BoxInfo.BoxType _lastDeliveredBoxType;

    #endregion

    #region Events

    public event Action<int, TutorialStep> OnStepStarted;
    public event Action<int, TutorialStep> OnStepCompleted;
    public event Action OnTutorialCompleted;

    #endregion

    #region Public Properties

    public int CurrentStepIndex => _currentStepIndex;
    public TutorialStep CurrentStep => _currentStep;
    public int TotalSteps => tutorialSteps.Count;
    public bool IsTutorialActive => isTutorialLevel && _currentStep != null;
    public bool IsTyping => _isTyping;
    public bool IsTransitioning => _isTransitioning;
    public bool IsTurkish => _isTurkish;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        InitializeSingleton();
    }

    private void Start()
    {
        if (!isTutorialLevel)
        {
            DisableTutorial();
            return;
        }

        InitializeAudioSource();
        InitializeUI();
        StartPlayerSearch();
        StartCoroutine(InitializeLocalizationAndStartTutorial());
    }

    private void Update()
    {
        HandleSkipInput();
        CheckLocaleChange();
    }

    private void OnDestroy()
    {
        RemoveHighlight();
    }

    #endregion

    #region Initialization

    private void InitializeSingleton()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void DisableTutorial()
    {
        if (tutorialUI != null)
        {
            tutorialUI.SetActive(false);
        }
        enabled = false;
    }

    private void InitializeAudioSource()
    {
        if (typingSound != null && typingSoundSource == null)
        {
            typingSoundSource = gameObject.AddComponent<AudioSource>();
            typingSoundSource.playOnAwake = false;
            typingSoundSource.volume = typingSoundVolume;
        }
    }

    private void InitializeUI()
    {
        if (tutorialUI != null)
        {
            tutorialUI.SetActive(true);
        }

        if (tutorialCanvasGroup != null)
        {
            tutorialCanvasGroup.alpha = 1f;
        }

        ConfigureInstructionText();
        ConfigureSkipHintText();

        LogDebug("Tutorial UI initialized");
    }

    private void ConfigureInstructionText()
    {
        if (instructionText == null) return;

        instructionText.text = "";
        instructionText.overflowMode = TextOverflowModes.Overflow;
        instructionText.enableWordWrapping = true;
        instructionText.enableAutoSizing = true;
        instructionText.fontSizeMin = TEXT_FONT_SIZE_MIN;
        instructionText.fontSizeMax = TEXT_FONT_SIZE_MAX;

        LogDebug("Instruction text configured with auto-sizing and word wrapping");
    }

    private void ConfigureSkipHintText()
    {
        if (skipHintText == null) return;

        skipHintText.gameObject.SetActive(false);
        skipHintText.enableWordWrapping = true;
        skipHintText.overflowMode = TextOverflowModes.Overflow;
    }

    private void StartPlayerSearch()
    {
        if (playerInventory == null)
        {
            StartCoroutine(FindLocalPlayerCoroutine());
        }
    }

    private IEnumerator InitializeLocalizationAndStartTutorial()
    {
        // Localization hazır olana kadar bekle
        yield return new WaitUntil(() =>
            LocalizationSettings.InitializationOperation.IsValid() &&
            LocalizationSettings.InitializationOperation.IsDone);

        // Dil durumunu güncelle
        UpdateLocaleState();

        LogDebug($"Localization initialized.  Current language: {(_isTurkish ? "Turkish" : "English")}");

        // Tutorial'ı başlat
        yield return StartTutorialSequenceCoroutine();
    }

    #endregion

    #region Localization

    /// <summary>
    /// Mevcut dil durumunu günceller
    /// </summary>
    private void UpdateLocaleState()
    {
        if (LocalizationSettings.SelectedLocale != null)
        {
            string localeCode = LocalizationSettings.SelectedLocale.Identifier.Code;
            _isTurkish = localeCode.ToLower().StartsWith(TURKISH_LOCALE_CODE);
        }
        else
        {
            _isTurkish = true; // Varsayılan Türkçe
        }
    }

    /// <summary>
    /// Dil değişikliğini kontrol eder ve gerekirse UI'ı günceller
    /// </summary>
    private void CheckLocaleChange()
    {
        if (LocalizationSettings.SelectedLocale == null) return;

        string currentLocale = LocalizationSettings.SelectedLocale.Identifier.Code;
        bool currentIsTurkish = currentLocale.ToLower().StartsWith(TURKISH_LOCALE_CODE);

        // Dil değiştiyse
        if (currentIsTurkish != _isTurkish)
        {
            _isTurkish = currentIsTurkish;
            LogDebug($"Language changed to: {(_isTurkish ? "Turkish" : "English")}");

            // Mevcut adımın metnini güncelle
            RefreshCurrentStepText();
        }
    }

    /// <summary>
    /// Mevcut adımın metnini yeniden gösterir (dil değiştiğinde)
    /// </summary>
    private void RefreshCurrentStepText()
    {
        if (_currentStep == null || instructionText == null) return;

        // Typewriter devam ediyorsa durdur ve yeni dille başlat
        if (_isTyping)
        {
            StopCurrentTypewriter();
            string localizedText = _currentStep.GetLocalizedInstruction(_isTurkish);
            StartCoroutine(ShowInstructionCoroutine(localizedText));
        }
        else
        {
            // Doğrudan metni güncelle
            instructionText.text = _currentStep.GetLocalizedInstruction(_isTurkish);
        }

        // Skip hint'i de güncelle
        UpdateSkipHintText();
    }

    /// <summary>
    /// Skip hint metnini mevcut dile göre günceller
    /// </summary>
    private void UpdateSkipHintText()
    {
        if (skipHintText == null) return;
        if (!skipHintText.gameObject.activeSelf) return;

        string message = _isTurkish ? skipHintMessageTR : skipHintMessageEN;
        skipHintText.text = message.Replace("[SPACE]", $"[{skipKey}]");
    }

    /// <summary>
    /// Lokalize edilmiş metni döndürür
    /// </summary>
    private string GetLocalizedText(string turkishText, string englishText)
    {
        if (_isTurkish)
        {
            return turkishText;
        }

        return string.IsNullOrEmpty(englishText) ? turkishText : englishText;
    }

    #endregion

    #region Input Handling

    private void HandleSkipInput()
    {
        if (!Input.GetKeyDown(skipKey)) return;

        if (_isTyping)
        {
            SkipTypewriter();
        }
        else if (CanSkipCurrentStep())
        {
            SkipWaitStep();
        }
    }

    private void SkipTypewriter()
    {
        _skipTyping = true;
        LogDebug($"Typewriter skipped with {skipKey}");
    }

    private void SkipWaitStep()
    {
        LogDebug($"Wait step skipped with {skipKey}");
        CompleteCurrentStep();
    }

    private bool CanSkipCurrentStep()
    {
        return _currentStep != null &&
               _currentStep.conditionType == TutorialConditionType.WaitForTime;
    }

    #endregion

    #region Player Finding

    public void SetPlayerInventory(PlayerInventory player)
    {
        playerInventory = player;
        LogDebug($"Player Inventory set: {(player != null ? player.name : "null")}");
    }

    private IEnumerator FindLocalPlayerCoroutine()
    {
        int attempts = 0;

        while (attempts < MAX_PLAYER_SEARCH_ATTEMPTS)
        {
            yield return new WaitForSeconds(PLAYER_SEARCH_INTERVAL);
            attempts++;

            LogDebug($"Searching for player...  Attempt {attempts}/{MAX_PLAYER_SEARCH_ATTEMPTS}");

            if (TryFindLocalPlayer())
            {
                yield break;
            }
        }

        Debug.LogError($"{LOG_PREFIX} Could not find local player after {MAX_PLAYER_SEARCH_ATTEMPTS} attempts!");
    }

    private bool TryFindLocalPlayer()
    {
        PlayerInventory[] players = FindObjectsOfType<PlayerInventory>();
        LogDebug($"Found {players.Length} PlayerInventory objects");

        foreach (var player in players)
        {
            if (player.IsOwner)
            {
                playerInventory = player;
                LogDebug($"Local player found: {player.name}!");
                return true;
            }
        }

        return false;
    }

    #endregion

    #region Door Management

    public void RegisterDoor(TutorialDoor door)
    {
        if (tutorialDoors.Contains(door)) return;

        tutorialDoors.Add(door);
        LogDebug($"Door registered: {door.DoorName}");
    }

    public void UnregisterDoor(TutorialDoor door)
    {
        if (tutorialDoors.Remove(door))
        {
            LogDebug($"Door unregistered: {door.DoorName}");
        }
    }

    private void NotifyDoorsOfStepCompletion(int completedStepIndex)
    {
        foreach (var door in tutorialDoors)
        {
            door?.OnTutorialStepCompleted(completedStepIndex);
        }
    }

    #endregion

    #region Tutorial Sequence

    private IEnumerator StartTutorialSequenceCoroutine()
    {
        yield return WaitForPlayerCoroutine();

        LogDebug("Player ready, starting tutorial sequence!");

        yield return new WaitForSeconds(TUTORIAL_START_DELAY);

        if (tutorialSteps.Count > 0)
        {
            StartStep(0);
        }
    }

    private IEnumerator WaitForPlayerCoroutine()
    {
        while (playerInventory == null)
        {
            yield return new WaitForSeconds(PLAYER_SEARCH_INTERVAL);
            LogDebug("Waiting for player to spawn...");
        }
    }

    #endregion

    #region Step Management

    private void StartStep(int stepIndex)
    {
        if (stepIndex >= tutorialSteps.Count)
        {
            CompleteTutorial();
            return;
        }

        _currentStepIndex = stepIndex;
        _currentStep = tutorialSteps[stepIndex];
        _currentStep.stepStartTime = Time.time;

        // Flag'leri sıfırla
        ResetConditionFlags();

        LogDebug($"Tutorial Step {stepIndex + 1}/{tutorialSteps.Count}: {_currentStep.stepName}");

        _currentStep.onStepStart?.Invoke();
        OnStepStarted?.Invoke(stepIndex, _currentStep);

        // Lokalize edilmiş metni göster
        string localizedText = _currentStep.GetLocalizedInstruction(_isTurkish);
        StartCoroutine(ShowInstructionCoroutine(localizedText));

        HighlightObject(_currentStep.objectToHighlight);
        StartCoroutine(CheckStepConditionCoroutine());
    }

    private void ResetConditionFlags()
    {
        _tableInteractionCompleted = false;
        _shelfInteractionCompleted = false;
        _shelfPlacementCompleted = false;
        _truckDeliveryCompleted = false;

        // Step'in kendi delivery sayacını da sıfırla
        if (_currentStep != null)
        {
            _currentStep.currentDeliveryCount = 0;
        }
    }

    private void CompleteCurrentStep()
    {
        if (_isTransitioning || _currentStep == null) return;

        _isTransitioning = true;

        // Typewriter efektini hemen durdur
        StopCurrentTypewriter();
        _isTyping = false;
        _skipTyping = false;

        LogDebug($"Step {_currentStepIndex + 1} completed: {_currentStep.stepName}");

        _currentStep.isCompleted = true;
        _currentStep.onStepComplete?.Invoke();
        OnStepCompleted?.Invoke(_currentStepIndex, _currentStep);

        RemoveHighlight();
        NotifyDoorsOfStepCompletion(_currentStepIndex);

        StartCoroutine(TransitionToNextStepCoroutine());
    }

    private IEnumerator TransitionToNextStepCoroutine()
    {
        yield return StartCoroutine(HideInstructionCoroutine());
        yield return new WaitForSeconds(STEP_TRANSITION_DELAY);

        _isTransitioning = false;
        StartStep(_currentStepIndex + 1);
    }

    private void CompleteTutorial()
    {
        LogDebug("🎉 Tutorial completed!");

        if (instructionText != null)
        {
            // Lokalize edilmiş tamamlanma mesajı
            instructionText.text = GetLocalizedText(tutorialCompletedMessageTR, tutorialCompletedMessageEN);
        }

        if (skipHintText != null)
        {
            skipHintText.gameObject.SetActive(false);
        }

        OnTutorialCompleted?.Invoke();
    }

    #endregion

    #region Condition Checking

    private IEnumerator CheckStepConditionCoroutine()
    {
        while (!IsStepConditionMet())
        {
            yield return new WaitForSeconds(CONDITION_CHECK_INTERVAL);
        }

        CompleteCurrentStep();
    }

    private bool IsStepConditionMet()
    {
        if (_currentStep == null || playerInventory == null)
            return false;

        return _currentStep.conditionType switch
        {
            TutorialConditionType.PickupItem => CheckPickupCondition(),
            TutorialConditionType.DropItem => CheckDropCondition(),
            TutorialConditionType.PlaceOnTable => _tableInteractionCompleted,
            TutorialConditionType.TakeFromTable => _tableInteractionCompleted,
            TutorialConditionType.PlaceOnShelf => _shelfPlacementCompleted,
            TutorialConditionType.TakeFromShelf => CheckTakeFromShelfCondition(),
            TutorialConditionType.DeliverToTruck => CheckDeliverToTruckCondition(),
            TutorialConditionType.WaitForTime => CheckWaitTimeCondition(),
            TutorialConditionType.CompleteMinigame => _currentStep.isCompleted,
            TutorialConditionType.Custom => _currentStep.isCompleted,
            _ => false
        };
    }

    private bool CheckPickupCondition()
    {
        if (!_currentStep.requiresItemPickup) return false;

        if (!playerInventory.HasItem) return false;

        if (string.IsNullOrEmpty(_currentStep.requiredItemName))
        {
            return true;
        }

        return playerInventory.CurrentItemData != null &&
               playerInventory.CurrentItemData.itemName == _currentStep.requiredItemName;
    }

    private bool CheckDropCondition()
    {
        return !playerInventory.HasItem;
    }

    private bool CheckTakeFromShelfCondition()
    {
        if (!_shelfInteractionCompleted)
            return false;

        if (_currentStep.requiresSpecificBoxType)
        {
            return _lastTakenBoxType == _currentStep.requiredBoxType;
        }

        return true;
    }

    private bool CheckDeliverToTruckCondition()
    {
        // Teslimat sayısı kontrolü
        if (!_currentStep.IsDeliveryComplete())
            return false;

        // Belirli kutu türü gerekiyorsa kontrol et
        if (_currentStep.requiresSpecificBoxTypeForTruck)
        {
            return _lastDeliveredBoxType == _currentStep.requiredTruckBoxType;
        }

        return true;
    }

    private bool CheckWaitTimeCondition()
    {
        float elapsedTime = Time.time - _currentStep.stepStartTime;
        bool isComplete = elapsedTime >= _currentStep.waitDuration;

        if (isComplete)
        {
            LogDebug($"Wait time completed: {elapsedTime:F1}s / {_currentStep.waitDuration}s");
        }

        return isComplete;
    }

    #endregion

    #region Instruction Display

    private IEnumerator ShowInstructionCoroutine(string text)
    {
        if (instructionText == null) yield break;

        ShowSkipHintIfNeeded();

        if (enableTypewriterEffect)
        {
            StopCurrentTypewriter();
            _currentTypewriterCoroutine = StartCoroutine(TypewriterEffectCoroutine(text));
        }
        else
        {
            instructionText.text = text;
        }

        yield return null;
    }

    private void ShowSkipHintIfNeeded()
    {
        if (!showSkipHint || skipHintText == null) return;
        if (_currentStep.conditionType != TutorialConditionType.WaitForTime) return;

        // Lokalize edilmiş skip hint mesajı
        string message = _isTurkish ? skipHintMessageTR : skipHintMessageEN;
        skipHintText.text = message.Replace("[SPACE]", $"[{skipKey}]");
        skipHintText.gameObject.SetActive(true);
    }

    private void StopCurrentTypewriter()
    {
        if (_currentTypewriterCoroutine != null)
        {
            StopCoroutine(_currentTypewriterCoroutine);
            _currentTypewriterCoroutine = null;
        }

        // State'i de sıfırla
        _isTyping = false;
        _skipTyping = false;
    }

    private IEnumerator HideInstructionCoroutine()
    {
        // Önce typewriter'ı durdur
        StopCurrentTypewriter();
        _isTyping = false;
        _skipTyping = false;

        if (skipHintText != null)
        {
            skipHintText.gameObject.SetActive(false);
        }

        if (instructionText != null)
        {
            instructionText.text = "";
        }

        yield return null;
    }

    #endregion

    #region Typewriter Effect

    private IEnumerator TypewriterEffectCoroutine(string fullText)
    {
        _isTyping = true;
        _skipTyping = false;
        instructionText.text = "";

        for (int i = 0; i < fullText.Length; i++)
        {
            if (_skipTyping)
            {
                instructionText.text = fullText;
                break;
            }

            instructionText.text += fullText[i];
            PlayTypingSound(i);

            float delay = GetCharacterDelay(fullText[i]);
            yield return new WaitForSeconds(delay);
        }

        _isTyping = false;
        _skipTyping = false;

        LogDebug("Typewriter effect completed");
    }

    private void PlayTypingSound(int charIndex)
    {
        if (typingSound == null || typingSoundSource == null) return;
        if (charIndex % 2 != 0) return;

        typingSoundSource.PlayOneShot(typingSound, typingSoundVolume);
    }

    private float GetCharacterDelay(char character)
    {
        return character switch
        {
            '.' or '!' or '?' => typewriterSpeed * PUNCTUATION_DELAY_MULTIPLIER,
            ',' or ';' => typewriterSpeed * COMMA_DELAY_MULTIPLIER,
            ' ' => typewriterSpeed * SPACE_DELAY_MULTIPLIER,
            _ => typewriterSpeed
        };
    }

    #endregion

    #region Highlight System

    private void HighlightObject(GameObject obj)
    {
        RemoveHighlight();

        if (obj == null) return;

        Outline outline = obj.GetComponent<Outline>();
        if (outline == null)
        {
            outline = obj.AddComponent<Outline>();
        }

        ConfigureOutline(outline);
        _currentHighlight = obj;
    }

    private void ConfigureOutline(Outline outline)
    {
        outline.OutlineMode = Outline.Mode.OutlineAll;
        outline.OutlineColor = highlightColor;
        outline.OutlineWidth = HIGHLIGHT_OUTLINE_WIDTH;
        outline.enabled = true;
    }

    private void RemoveHighlight()
    {
        if (_currentHighlight == null) return;

        Outline outline = _currentHighlight.GetComponent<Outline>();
        if (outline != null)
        {
            outline.enabled = false;
        }

        _currentHighlight = null;
    }

    #endregion

    #region External Notifications

    /// <summary>
    /// Minigame tamamlandığında çağrılır
    /// </summary>
    public void OnMinigameCompleted()
    {
        if (_currentStep == null) return;
        if (_currentStep.conditionType != TutorialConditionType.CompleteMinigame) return;

        LogDebug("Minigame completed - marking step as done");
        _currentStep.isCompleted = true;

        StartCoroutine(CompleteStepNextFrameCoroutine());
    }

    /// <summary>
    /// Masa etkileşimi gerçekleştiğinde çağrılır
    /// </summary>
    public void OnTableInteraction(bool isPlacing)
    {
        if (_currentStep == null) return;

        bool shouldComplete = (isPlacing && _currentStep.conditionType == TutorialConditionType.PlaceOnTable) ||
                              (!isPlacing && _currentStep.conditionType == TutorialConditionType.TakeFromTable);

        if (shouldComplete)
        {
            string action = isPlacing ? "placed on" : "taken from";
            LogDebug($"Item {action} table - marking step for completion");
            _tableInteractionCompleted = true;
        }
    }

    /// <summary>
    /// Rafa item konulduğunda çağrılır
    /// </summary>
    public void OnItemPlacedOnShelf()
    {
        if (_currentStep == null) return;
        if (_currentStep.conditionType != TutorialConditionType.PlaceOnShelf) return;

        LogDebug("📦 Item placed on shelf - marking step for completion");
        _shelfPlacementCompleted = true;
    }

    /// <summary>
    /// Raftan kutu alındığında NetworkedShelf tarafından çağrılır
    /// </summary>
    public void OnBoxTakenFromShelf(NetworkedShelf.BoxType boxType)
    {
        if (_currentStep == null) return;

        if (_currentStep.conditionType == TutorialConditionType.TakeFromShelf)
        {
            _lastTakenBoxType = boxType;
            _shelfInteractionCompleted = true;

            LogDebug($"📦 {boxType} box taken from shelf - marking step for completion");
            return;
        }

        // Backward compatibility for PickupItem condition
        if (_currentStep.conditionType == TutorialConditionType.PickupItem)
        {
            if (_currentStep.requiresItemPickup &&
                !string.IsNullOrEmpty(_currentStep.requiredItemName) &&
                _currentStep.requiredItemName.Contains("Box"))
            {
                LogDebug($"📦 {boxType} box taken from shelf - checking tutorial step");
            }
        }
    }

    /// <summary>
    /// Araca kutu teslim edildiğinde TutorialTruck tarafından çağrılır
    /// </summary>
    public void OnBoxDeliveredToTruck(BoxInfo.BoxType boxType)
    {
        if (_currentStep == null) return;
        if (_currentStep.conditionType != TutorialConditionType.DeliverToTruck) return;

        _lastDeliveredBoxType = boxType;

        // Belirli kutu türü gerekiyorsa ve yanlış türse sayma
        if (_currentStep.requiresSpecificBoxTypeForTruck && boxType != _currentStep.requiredTruckBoxType)
        {
            LogDebug($"🚛 Wrong box type delivered!  Expected: {_currentStep.requiredTruckBoxType}, Got: {boxType}");
            return;
        }

        // Teslimat sayacını artır
        bool isComplete = _currentStep.IncrementDeliveryCount();

        LogDebug($"🚛 {boxType} box delivered to truck!  Progress: {_currentStep.GetDeliveryStatusText()}");

        if (isComplete)
        {
            LogDebug("🚛 Truck delivery complete!");
            _truckDeliveryCompleted = true;
        }
    }

    /// <summary>
    /// Truck teslimatı tamamlandığında çağrılır (alternatif - tüm teslimat bittiğinde)
    /// </summary>
    public void OnTruckDeliveryComplete()
    {
        if (_currentStep == null) return;
        if (_currentStep.conditionType != TutorialConditionType.DeliverToTruck) return;

        LogDebug("🚛 Truck delivery marked as complete externally");
        _currentStep.currentDeliveryCount = _currentStep.requiredDeliveryCount;
        _truckDeliveryCompleted = true;
    }

    private IEnumerator CompleteStepNextFrameCoroutine()
    {
        yield return null;

        if (_currentStep != null && _currentStep.isCompleted)
        {
            CompleteCurrentStep();
        }
    }

    #endregion

    #region Public API

    public void ForceCompleteCurrentStep()
    {
        CompleteCurrentStep();
    }

    public void SkipToStep(int stepIndex)
    {
        if (stepIndex < 0 || stepIndex >= tutorialSteps.Count)
        {
            Debug.LogWarning($"{LOG_PREFIX} Invalid step index: {stepIndex}");
            return;
        }

        StopAllCoroutines();
        RemoveHighlight();
        StartStep(stepIndex);
    }

    public int GetCurrentStepIndex() => _currentStepIndex;

    public TutorialStep GetCurrentStep() => _currentStep;

    public TutorialStep GetStep(int index)
    {
        if (index < 0 || index >= tutorialSteps.Count)
            return null;

        return tutorialSteps[index];
    }

    public void RestartTutorial()
    {
        StopAllCoroutines();
        RemoveHighlight();

        foreach (var step in tutorialSteps)
        {
            step.isCompleted = false;
            step.currentDeliveryCount = 0;
        }

        _currentStepIndex = 0;
        _currentStep = null;
        _isTransitioning = false;

        StartCoroutine(StartTutorialSequenceCoroutine());
    }

    /// <summary>
    /// Mevcut step'in teslimat ilerlemesini döndürür
    /// </summary>
    public string GetCurrentDeliveryProgress()
    {
        if (_currentStep == null) return "";
        if (_currentStep.conditionType != TutorialConditionType.DeliverToTruck) return "";

        return _currentStep.GetDeliveryStatusText();
    }

    #endregion

    #region Logging

    private void LogDebug(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"{LOG_PREFIX} {message}");
        }
    }

    #endregion

    #region Editor Debug

#if UNITY_EDITOR
    [ContextMenu("Force Complete Current Step")]
    private void DebugForceCompleteStep()
    {
        ForceCompleteCurrentStep();
    }

    [ContextMenu("Skip to Next Step")]
    private void DebugSkipToNextStep()
    {
        SkipToStep(_currentStepIndex + 1);
    }

    [ContextMenu("Restart Tutorial")]
    private void DebugRestartTutorial()
    {
        RestartTutorial();
    }

    [ContextMenu("Complete Tutorial")]
    private void DebugCompleteTutorial()
    {
        CompleteTutorial();
    }

    [ContextMenu("Debug: Simulate Truck Delivery")]
    private void DebugSimulateTruckDelivery()
    {
        if (_currentStep != null && _currentStep.conditionType == TutorialConditionType.DeliverToTruck)
        {
            OnBoxDeliveredToTruck(_currentStep.requiredTruckBoxType);
        }
    }

    [ContextMenu("Debug: Toggle Language")]
    private void DebugToggleLanguage()
    {
        _isTurkish = !_isTurkish;
        RefreshCurrentStepText();
        LogDebug($"Language toggled to: {(_isTurkish ? "Turkish" : "English")}");
    }

    [ContextMenu("Debug: Print State")]
    private void DebugPrintState()
    {
        Debug.Log($"{LOG_PREFIX} === TUTORIAL MANAGER STATE ===");
        Debug.Log($"Is Tutorial Level: {isTutorialLevel}");
        Debug.Log($"Is Tutorial Active: {IsTutorialActive}");
        Debug.Log($"Current Language: {(_isTurkish ? "Turkish" : "English")}");
        Debug.Log($"Current Step Index: {_currentStepIndex}/{TotalSteps}");
        Debug.Log($"Current Step: {(_currentStep != null ? _currentStep.stepName : "NULL")}");
        Debug.Log($"Is Transitioning: {_isTransitioning}");
        Debug.Log($"Is Typing: {_isTyping}");
        Debug.Log($"Table Interaction Completed: {_tableInteractionCompleted}");
        Debug.Log($"Shelf Interaction Completed: {_shelfInteractionCompleted}");
        Debug.Log($"Shelf Placement Completed: {_shelfPlacementCompleted}");
        Debug.Log($"Truck Delivery Completed: {_truckDeliveryCompleted}");
        Debug.Log($"Has Player Inventory: {playerInventory != null}");
        Debug.Log($"Registered Doors: {tutorialDoors.Count}");

        if (_currentStep != null)
        {
            Debug.Log($"--- Current Step Details ---");
            Debug.Log($"  Name: {_currentStep.stepName}");
            Debug.Log($"  Condition: {_currentStep.conditionType}");
            Debug.Log($"  Is Completed: {_currentStep.isCompleted}");
            Debug.Log($"  Start Time: {_currentStep.stepStartTime:F2}");
            Debug.Log($"  TR Text: {_currentStep.instructionText}");
            Debug.Log($"  EN Text: {_currentStep.instructionTextEnglish}");

            if (_currentStep.conditionType == TutorialConditionType.DeliverToTruck)
            {
                Debug.Log($"  Delivery Progress: {_currentStep.GetDeliveryStatusText()}");
            }
        }
    }

    [ContextMenu("Debug: Print All Steps")]
    private void DebugPrintAllSteps()
    {
        Debug.Log($"{LOG_PREFIX} === ALL TUTORIAL STEPS ===");

        for (int i = 0; i < tutorialSteps.Count; i++)
        {
            var step = tutorialSteps[i];
            string status = step.isCompleted ? "[COMPLETED]" : (i == _currentStepIndex ? "[CURRENT]" : "[PENDING]");
            Debug.Log($"  [{i}] {step}");
            Debug.Log($"      TR: {step.instructionText}");
            Debug.Log($"      EN: {step.instructionTextEnglish}");
        }
    }

    [ContextMenu("Debug: Print Registered Doors")]
    private void DebugPrintDoors()
    {
        Debug.Log($"{LOG_PREFIX} === REGISTERED DOORS ===");

        foreach (var door in tutorialDoors)
        {
            if (door != null)
            {
                Debug.Log($"  - {door.DoorName}");
            }
        }
    }
#endif

    #endregion
}