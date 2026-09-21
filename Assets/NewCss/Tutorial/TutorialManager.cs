using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Localization.Settings;
using NewCss;
using NewCss.Audio;

/// <summary>
/// Tutorial yönetim sistemi - adım adım tutorial akışı, UI yönetimi ve koşul kontrollerini sağlar.
/// Typewriter efekti, highlight sistemi, kapı entegrasyonu ve çoklu dil desteği içerir.
///
/// Çekirdek döngü kapsamı (bkz plans/tutorial-rewrite.md) — güncel 10 adımlık akış
/// (tam tablo + Inspector değerleri plans/tutorial-rewrite.md'de):
///   0. Hoş geldin (PressKey)                 5. Masadan al (TakeFromTable)
///   1. Müşteriyle konuş + al (TakeFromTable) 6. Rafa koy (PlaceOnShelf)
///   2. Masaya bırak (PlaceOnTable)           7. Bekle (WaitForTime)
///   3. Raftan kutu al (TakeFromShelf)        8. Raftan tekrar al (TakeFromShelf)
///   4. Masaya bırak → otomatik paketle       9. Tıra teslim et (DeliverToTruck)
/// Bu liste yalnız referans/öneridir — `tutorialSteps` alanı koddan YAML/kod
/// üzerinden doldurulmaz; adımların Inspector'da elle eklenmesi/sıralanması
/// gerekir (Unity Editor işi, bu script'in kapsamı dışında).
/// </summary>
public class TutorialManager : NetworkBehaviour
{
    #region Constants

    private const string LOG_PREFIX = "[TutorialManager]";
    private const float PLAYER_SEARCH_INTERVAL = 0.5f;
    private const int MAX_PLAYER_SEARCH_ATTEMPTS = 20;
    private const float CONDITION_CHECK_INTERVAL = 0.1f;
    private const float TUTORIAL_START_DELAY = 1f;

    private const float TEXT_FONT_SIZE_MIN = 18f;
    private const float TEXT_FONT_SIZE_MAX = 36f;

    private const float PUNCTUATION_DELAY_MULTIPLIER = 8f;
    private const float COMMA_DELAY_MULTIPLIER = 4f;
    private const float SPACE_DELAY_MULTIPLIER = 0.5f;

    private const float HIGHLIGHT_OUTLINE_WIDTH = 5f;

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

    #endregion

    #region Serialized Fields - References

    [Header("=== PLAYER REFERENCE ===")]
    [SerializeField, Tooltip("Oyuncu envanteri")]
    private PlayerInventory playerInventory;

    [Header("=== TUTORIAL CUSTOMER BYPASS ===")]
    [SerializeField, Tooltip("Tutorial'daki tek müşteri. Sahnede CustomerManager olmadığından " +
        "CustomerAI hiçbir zaman doğal yoldan Service state'ine geçmez (bkz. AssignFreeServiceStations, " +
        "CustomerAI.cs) — bu yüzden burada elle atanıp server'da AssignServiceStation ile bypass edilir. " +
        "Boş bırakılırsa bypass hiçbir şey yapmaz (production/CustomerManager akışı etkilenmez).")]
    private CustomerAI tutorialCustomer;

    [SerializeField, Tooltip("tutorialCustomer'a atanacak sipariş masası (DisplayTable).")]
    private DisplayTable tutorialDropOffTable;

    #endregion

    #region Serialized Fields - Visual Helpers

    [Header("=== VISUAL HELPERS ===")]
    [SerializeField, Tooltip("Highlight prefab'ı")]
    private GameObject highlightPrefab;

    [SerializeField, Tooltip("Highlight rengi")]
    private Color highlightColor = Color.yellow;

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
    private bool _pressKeyDetected;
    private bool _boxSealed;
    // ResetConditionFlags'te SIFIRLANMAZ: bant alinir alinmaz (TakeTape->SealBox gecisinden once,
    // ~0.1s poll penceresinde) yapilan bantlama olayi kacmasin, yoksa SealBox adimi kalici takilir.
    // Bantlama fiziksel olarak ancak PackItem sonrasi mumkun (Table.IsAwaitingSeal), erken set olamaz.
    private bool _sealEventSeen;
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
        StartCoroutine(AssignTutorialCustomerServiceStationWhenReady());
    }

    private void Update()
    {
        HandleSkipInput();
        HandlePressKeyCondition();
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        RemoveHighlight();
        NewCss.LocalizationHelper.OnLocaleChanged -= RefreshCurrentStepText;
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
            AudioRouting.Route(typingSoundSource, AudioCategory.SFX);
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

    /// <summary>
    /// Tutorial-only bypass: Tutorial.unity'de CustomerManager.AssignFreeServiceStations akışı
    /// çalışmadığından (sahnede CustomerManager yok), tutorialCustomer hiçbir zaman doğal yoldan
    /// Service state'ine geçmez ve oyuncu E'ye bassa da RequestInteractionServerRpc içindeki
    /// state guard'ı yüzünden hiçbir şey olmaz. Bu coroutine server'da tutorialCustomer'ın
    /// NetworkObject'i spawn olur olmaz AssignServiceStation'ı (CustomerAI.cs, zaten public ve
    /// IsServer-guard'lı, production API'si) doğrudan çağırır. CustomerManager'a veya normal
    /// AssignFreeServiceStations akışına hiç dokunmaz — sadece bu tek müşteriyi bypass eder.
    /// </summary>
    private IEnumerator AssignTutorialCustomerServiceStationWhenReady()
    {
        if (tutorialCustomer == null || tutorialDropOffTable == null)
        {
            LogDebug("tutorialCustomer/tutorialDropOffTable atanmamış - Service bypass atlanıyor");
            yield break;
        }

        // Yalnızca server'da anlamlı; AssignServiceStation zaten IsServer guard'lı ama
        // NetworkManager.Singleton null olabileceğinden burada erken çıkıyoruz.
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            yield break;
        }

        const float timeout = 10f;
        float elapsed = 0f;
        while (!tutorialCustomer.IsSpawned && elapsed < timeout)
        {
            yield return null;
            elapsed += Time.deltaTime;
        }

        if (!tutorialCustomer.IsSpawned)
        {
            Debug.LogWarning($"{LOG_PREFIX} tutorialCustomer {timeout}s içinde spawn olmadı - Service bypass iptal edildi");
            yield break;
        }

        tutorialCustomer.AssignServiceStation(tutorialDropOffTable);
        LogDebug("Tutorial customer doğrudan Service state'ine atandı (CustomerManager bypass)");
    }

    private IEnumerator InitializeLocalizationAndStartTutorial()
    {
        // Localization hazır olana kadar bekle
        yield return new WaitUntil(() =>
            LocalizationSettings.InitializationOperation.IsValid() &&
            LocalizationSettings.InitializationOperation.IsDone);

        NewCss.LocalizationHelper.OnLocaleChanged += RefreshCurrentStepText;

        LogDebug("Localization initialized.");

        // Tutorial'ı başlat
        yield return StartTutorialSequenceCoroutine();
    }

    #endregion

    #region Localization

    /// <summary>
    /// Adımın talimat metnini StringTable'dan çözer, tuş referansını (rebind'e duyarlı)
    /// ve skip tuşunu yerlerine yerleştirir.
    /// </summary>
    private string ResolveStepText(TutorialStep step)
    {
        if (step == null || string.IsNullOrEmpty(step.instructionLocalizationKey)) return "";

        string interactKey = InputBindingManager.GetBindingDisplayName(InputBindingManager.GameAction.Interact);
        string raw = NewCss.LocalizationHelper.GetLocalizedStringFormat(step.instructionLocalizationKey, interactKey);
        return raw.Replace("[SPACE]", $"[{skipKey}]");
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
            string localizedText = ResolveStepText(_currentStep);
            StartCoroutine(ShowInstructionCoroutine(localizedText));
        }
        else
        {
            // Doğrudan metni güncelle
            instructionText.text = ResolveStepText(_currentStep);
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

        string message = NewCss.LocalizationHelper.GetLocalizedString("TutorialSkipHint");
        skipHintText.text = message.Replace("[SPACE]", $"[{skipKey}]");
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

    /// <summary>
    /// PressKey koşulunu Update()'te frame-doğru şekilde takip eder.
    /// IsStepConditionMet() bu koşulu 0.1s aralıklarla poll ettiği için doğrudan
    /// Input.GetKeyDown okusa çoğu basışı kaçırır (GetKeyDown yalnız o frame true) —
    /// bu yüzden bayrak burada, her frame, tutulur. Typewriter hâlâ yazıyorsa basış
    /// (skip mantığıyla tutarlı olarak) yalnız metni tamamlar, adımı bitirmez.
    /// </summary>
    private void HandlePressKeyCondition()
    {
        if (_currentStep == null || _currentStep.conditionType != TutorialConditionType.PressKey) return;
        if (_isTyping) return;

        if (Input.GetKeyDown(_currentStep.requiredKey))
        {
            _pressKeyDetected = true;
        }
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
        string localizedText = ResolveStepText(_currentStep);
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
        _pressKeyDetected = false;
        _boxSealed = false;

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

        // Typewriter efektini hemen durdur, eski adimin metin/skip-hint'ini temizle
        // (eskiden ayri HideInstructionCoroutine yapiyordu - bkz. asagidaki not).
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

        LogDebug($"Step {_currentStepIndex + 1} completed: {_currentStep.stepName}");

        _currentStep.isCompleted = true;
        _currentStep.onStepComplete?.Invoke();
        OnStepCompleted?.Invoke(_currentStepIndex, _currentStep);

        RemoveHighlight();
        NotifyDoorsOfStepCompletion(_currentStepIndex);

        // Bir sonraki adima HEMEN gec (eskiden StartCoroutine(TransitionToNextStepCoroutine())
        // ile HideInstructionCoroutine + STEP_TRANSITION_DELAY (0.3sn) kadar gecikmeliydi).
        // O gecikme penceresinde yapilan bir fiziksel aksiyon (orn. paketlenmis kutuyu masadan
        // alma) hala ESKI adimin kosuluna gore degerlendirilip kayboluyordu - StartStep() flag'leri
        // sifirladiginda event bir daha ateslenmedigi icin yeni adim asla tamamlanamiyordu
        // (kullanici bulgusu: "metin bitmeden aldim, 2. kapi acilmadi", 2026-09-11).
        _isTransitioning = false;
        StartStep(_currentStepIndex + 1);
    }

    private void CompleteTutorial()
    {
        LogDebug("🎉 Tutorial completed!");

        if (instructionText != null)
        {
            // Lokalize edilmiş tamamlanma mesajı
            instructionText.text = NewCss.LocalizationHelper.GetLocalizedString("TutorialCompleted");
        }

        if (skipHintText != null)
        {
            skipHintText.gameObject.SetActive(false);
        }

        // _currentStep tamamlanmadan sonra null'lanmazsa Update() içindeki
        // HandleSkipInput bayat referansla çalışmaya devam eder (skip tuşu
        // tamamlama mantığını tekrar tetikler) ve olası bir OnLocaleChanged
        // tetiklenmesinde RefreshCurrentStepText "Tutorial completed!" mesajını
        // eski adım metniyle ezer.
        _currentStep = null;

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
            TutorialConditionType.PressKey => _pressKeyDetected,
            TutorialConditionType.InteractWithCustomer => tutorialCustomer != null && tutorialCustomer.HasInteracted,
            TutorialConditionType.SealBox => _boxSealed || _sealEventSeen,
            // CompleteMinigame ve Custom: kapsam dışı, çekirdek-döngü tutorial'ında
            // kullanılmıyor (bkz plans/tutorial-rewrite.md). Enum'dan silinmedi,
            // ileride minigame/özel adım eklenirse hazır kalsın diye.
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
        string message = NewCss.LocalizationHelper.GetLocalizedString("TutorialSkipHint");
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
        // Kapsam dışı, çekirdek-döngü tutorial'ında kullanılmıyor
        // (bkz plans/tutorial-rewrite.md) — çağrılmıyor ama kaldırılmadı.
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
    /// Açık+dolu kutu bantlanıp kapatıldığında çağrılır (Table.PerformSealing başarı yolu)
    /// </summary>
    public void OnBoxSealed()
    {
        _sealEventSeen = true;
        if (_currentStep == null) return;
        if (_currentStep.conditionType != TutorialConditionType.SealBox) return;

        LogDebug("📦 Box sealed with tape - marking step for completion");
        _boxSealed = true;
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
        _sealEventSeen = false;

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

    [ContextMenu("Debug: Print State")]
    private void DebugPrintState()
    {
        Debug.Log($"{LOG_PREFIX} === TUTORIAL MANAGER STATE ===");
        Debug.Log($"Is Tutorial Level: {isTutorialLevel}");
        Debug.Log($"Is Tutorial Active: {IsTutorialActive}");
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
            Debug.Log($"  Localization Key: {_currentStep.instructionLocalizationKey}");

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
            Debug.Log($"      Key: {step.instructionLocalizationKey}");
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