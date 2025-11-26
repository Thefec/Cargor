using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Unity.Netcode;
using NewCss;

public class TutorialManager : NetworkBehaviour
{
    [Header("Tutorial Settings")]
    [SerializeField] private bool isTutorialLevel = true;
    [SerializeField] private List<TutorialStep> tutorialSteps = new List<TutorialStep>();

    [Header("Door Management")]
    [SerializeField] private List<TutorialDoor> tutorialDoors = new List<TutorialDoor>();

    [Header("UI References")]
    [SerializeField] private GameObject tutorialUI;
    [SerializeField] private TextMeshProUGUI instructionText;
    [SerializeField] private CanvasGroup tutorialCanvasGroup;
    [SerializeField] private float fadeSpeed = 2f;

    [Header("Typewriter Effect")]
    [SerializeField] private bool enableTypewriterEffect = true;
    [SerializeField] private float typewriterSpeed = 0.05f;
    [SerializeField] private AudioClip typingSound;
    [SerializeField] private AudioSource typingSoundSource;
    [Range(0f, 1f)]
    [SerializeField] private float typingSoundVolume = 0.3f;

    [Header("Skip Settings")]
    [SerializeField] private KeyCode skipKey = KeyCode.Space;
    [SerializeField] private bool showSkipHint = true;
    [SerializeField] private TextMeshProUGUI skipHintText;
    [SerializeField] private string skipHintMessage = "Geçmek için [SPACE] tuşuna basın";

    [Header("Player Reference")]
    [SerializeField] private PlayerInventory playerInventory;

    [Header("Visual Helpers")]
    [SerializeField] private GameObject highlightPrefab;
    [SerializeField] private Color highlightColor = Color.yellow;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private int currentStepIndex = 0;
    private TutorialStep currentStep;
    private bool isTransitioning = false;
    private GameObject currentHighlight;

    // Typewriter kontrol
    private bool isTyping = false;
    private bool skipTyping = false;
    private Coroutine currentTypewriterCoroutine;

    // ✅ FIX: Table interaction tracking
    private bool tableInteractionCompleted = false;

    // Singleton
    public static TutorialManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (!isTutorialLevel)
        {
            if (tutorialUI != null)
                tutorialUI.SetActive(false);
            enabled = false;
            return;
        }

        if (typingSound != null && typingSoundSource == null)
        {
            typingSoundSource = gameObject.AddComponent<AudioSource>();
            typingSoundSource.playOnAwake = false;
            typingSoundSource.volume = typingSoundVolume;
        }

        InitializeUI();

        if (playerInventory == null)
        {
            StartCoroutine(FindLocalPlayer());
        }

        StartCoroutine(StartTutorialSequence());
    }

    /// <summary>
    /// ✅ FIX: UI text ayarlarını düzelt (overflow + auto-size)
    /// </summary>
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

        if (instructionText != null)
        {
            instructionText.text = "";

            // ✅ FIX: Text overflow ayarları
            instructionText.overflowMode = TextOverflowModes.Overflow; // Taşma izin ver (RectTransform içinde kal)
            instructionText.enableWordWrapping = true; // Kelime kaydırma aktif
            instructionText.enableAutoSizing = true; // Otomatik boyutlandırma
            instructionText.fontSizeMin = 18f; // Minimum font boyutu
            instructionText.fontSizeMax = 36f; // Maksimum font boyutu

            if (showDebugLogs)
                Debug.Log("✅ Instruction text configured with auto-sizing and word wrapping");
        }

        if (skipHintText != null)
        {
            skipHintText.gameObject.SetActive(false);

            // ✅ Skip hint için de ayarla
            skipHintText.enableWordWrapping = true;
            skipHintText.overflowMode = TextOverflowModes.Overflow;
        }

        if (showDebugLogs)
            Debug.Log("✅ Tutorial UI initialized (always visible)");
    }

    private void Update()
    {
        if (Input.GetKeyDown(skipKey))
        {
            if (isTyping)
            {
                skipTyping = true;

                if (showDebugLogs)
                    Debug.Log($"⏭️ Typewriter skipped with {skipKey}");
            }
            else if (currentStep != null && currentStep.conditionType == TutorialConditionType.WaitForTime)
            {
                if (showDebugLogs)
                    Debug.Log($"⏭️ Wait step skipped with {skipKey}");

                CompleteCurrentStep();
            }
        }
    }

    public void SetPlayerInventory(PlayerInventory player)
    {
        playerInventory = player;

        if (showDebugLogs)
            Debug.Log($"✅ Player Inventory set: {(player != null ? player.name : "null")}");
    }

    private IEnumerator FindLocalPlayer()
    {
        int attempts = 0;
        int maxAttempts = 20;

        while (attempts < maxAttempts)
        {
            yield return new WaitForSeconds(0.5f);
            attempts++;

            if (showDebugLogs)
                Debug.Log($"🔍 Searching for player... Attempt {attempts}/{maxAttempts}");

            PlayerInventory[] players = FindObjectsOfType<PlayerInventory>();

            if (showDebugLogs)
                Debug.Log($"Found {players.Length} PlayerInventory objects");

            foreach (var player in players)
            {
                if (player.IsOwner)
                {
                    playerInventory = player;

                    if (showDebugLogs)
                        Debug.Log($"✅ Local player found: {player.name}!");

                    yield break;
                }
            }
        }

        Debug.LogError($"❌ Could not find local player after {maxAttempts} attempts!");
    }

    public void RegisterDoor(TutorialDoor door)
    {
        if (!tutorialDoors.Contains(door))
        {
            tutorialDoors.Add(door);
            if (showDebugLogs)
                Debug.Log($"✅ Door registered: {door.DoorName}");
        }
    }

    private IEnumerator StartTutorialSequence()
    {
        while (playerInventory == null)
        {
            yield return new WaitForSeconds(0.5f);

            if (showDebugLogs)
                Debug.Log("⏳ Waiting for player to spawn...");
        }

        if (showDebugLogs)
            Debug.Log("✅ Player ready, starting tutorial sequence!");

        yield return new WaitForSeconds(1f);

        if (tutorialSteps.Count > 0)
        {
            StartStep(0);
        }
    }

    private void StartStep(int stepIndex)
    {
        if (stepIndex >= tutorialSteps.Count)
        {
            CompleteTutorial();
            return;
        }

        currentStepIndex = stepIndex;
        currentStep = tutorialSteps[stepIndex];

        currentStep.stepStartTime = Time.time;

        // ✅ FIX: Table interaction flag'ini sıfırla
        tableInteractionCompleted = false;

        if (showDebugLogs)
            Debug.Log($"📚 Tutorial Step {stepIndex + 1}/{tutorialSteps.Count}: {currentStep.stepName}");

        currentStep.onStepStart?.Invoke();

        StartCoroutine(ShowInstruction(currentStep.instructionText));

        if (currentStep.objectToHighlight != null)
        {
            HighlightObject(currentStep.objectToHighlight);
        }

        StartCoroutine(CheckStepCondition());
    }

    private IEnumerator ShowInstruction(string text)
    {
        if (instructionText != null)
        {
            if (showSkipHint && skipHintText != null && currentStep.conditionType == TutorialConditionType.WaitForTime)
            {
                skipHintText.text = skipHintMessage.Replace("[SPACE]", $"[{skipKey}]");
                skipHintText.gameObject.SetActive(true);
            }

            if (enableTypewriterEffect)
            {
                if (currentTypewriterCoroutine != null)
                {
                    StopCoroutine(currentTypewriterCoroutine);
                }

                currentTypewriterCoroutine = StartCoroutine(TypewriterEffect(text));
            }
            else
            {
                instructionText.text = text;
            }
        }

        yield return null;
    }

    private IEnumerator TypewriterEffect(string fullText)
    {
        isTyping = true;
        skipTyping = false;

        instructionText.text = "";

        for (int i = 0; i < fullText.Length; i++)
        {
            if (skipTyping)
            {
                instructionText.text = fullText;
                break;
            }

            instructionText.text += fullText[i];

            if (typingSound != null && typingSoundSource != null && i % 2 == 0)
            {
                typingSoundSource.PlayOneShot(typingSound, typingSoundVolume);
            }

            if (fullText[i] == '.' || fullText[i] == '!' || fullText[i] == '?')
            {
                yield return new WaitForSeconds(typewriterSpeed * 8f);
            }
            else if (fullText[i] == ',' || fullText[i] == ';')
            {
                yield return new WaitForSeconds(typewriterSpeed * 4f);
            }
            else if (fullText[i] == ' ')
            {
                yield return new WaitForSeconds(typewriterSpeed * 0.5f);
            }
            else
            {
                yield return new WaitForSeconds(typewriterSpeed);
            }
        }

        isTyping = false;
        skipTyping = false;

        if (showDebugLogs)
            Debug.Log("✅ Typewriter effect completed");
    }

    private IEnumerator HideInstruction()
    {
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

    private IEnumerator CheckStepCondition()
    {
        while (!IsStepConditionMet())
        {
            yield return new WaitForSeconds(0.1f);
        }

        CompleteCurrentStep();
    }

    /// <summary>
    /// ✅ FIX: PlaceOnTable ve TakeFromTable condition'ları eklendi
    /// </summary>
    private bool IsStepConditionMet()
    {
        if (currentStep == null || playerInventory == null)
            return false;

        switch (currentStep.conditionType)
        {
            case TutorialConditionType.PickupItem:
                if (currentStep.requiresItemPickup)
                {
                    if (!string.IsNullOrEmpty(currentStep.requiredItemName))
                    {
                        return playerInventory.HasItem &&
                               playerInventory.CurrentItemData != null &&
                               playerInventory.CurrentItemData.itemName == currentStep.requiredItemName;
                    }
                    return playerInventory.HasItem;
                }
                return false;

            case TutorialConditionType.DropItem:
                return !playerInventory.HasItem;

            // ✅ FIX: Masaya koyma kontrolü
            case TutorialConditionType.PlaceOnTable:
                return tableInteractionCompleted;

            // ✅ FIX: Masadan alma kontrolü
            case TutorialConditionType.TakeFromTable:
                return tableInteractionCompleted;

            case TutorialConditionType.WaitForTime:
                float elapsedTime = Time.time - currentStep.stepStartTime;
                bool timeComplete = elapsedTime >= currentStep.waitDuration;

                if (showDebugLogs && timeComplete)
                    Debug.Log($"⏰ Wait time completed: {elapsedTime:F1}s / {currentStep.waitDuration}s");

                return timeComplete;

            // ✅ FIX: Minigame completion
            case TutorialConditionType.CompleteMinigame:
                return currentStep.isCompleted;

            case TutorialConditionType.Custom:
                return false;

            default:
                return false;
        }
    }

    private void CompleteCurrentStep()
    {
        if (isTransitioning || currentStep == null)
            return;

        isTransitioning = true;

        if (showDebugLogs)
            Debug.Log($"✅ Step {currentStepIndex + 1} completed: {currentStep.stepName}");

        currentStep.isCompleted = true;
        currentStep.onStepComplete?.Invoke();

        RemoveHighlight();

        NotifyDoorsOfStepCompletion(currentStepIndex);

        StartCoroutine(TransitionToNextStep());
    }

    private void NotifyDoorsOfStepCompletion(int completedStepIndex)
    {
        foreach (var door in tutorialDoors)
        {
            if (door != null)
            {
                door.OnTutorialStepCompleted(completedStepIndex);
            }
        }
    }

    private IEnumerator TransitionToNextStep()
    {
        yield return StartCoroutine(HideInstruction());

        yield return new WaitForSeconds(0.3f);

        isTransitioning = false;

        StartStep(currentStepIndex + 1);
    }

    private void CompleteTutorial()
    {
        if (showDebugLogs)
            Debug.Log("🎉 Tutorial completed!");

        if (instructionText != null)
        {
            instructionText.text = "Tutorial tamamlandı!";
        }

        if (skipHintText != null)
        {
            skipHintText.gameObject.SetActive(false);
        }
    }

    private void HighlightObject(GameObject obj)
    {
        RemoveHighlight();

        if (obj == null)
            return;

        Outline outline = obj.GetComponent<Outline>();
        if (outline == null)
        {
            outline = obj.AddComponent<Outline>();
        }

        outline.OutlineMode = Outline.Mode.OutlineAll;
        outline.OutlineColor = highlightColor;
        outline.OutlineWidth = 5f;
        outline.enabled = true;

        currentHighlight = obj;
    }

    private void RemoveHighlight()
    {
        if (currentHighlight != null)
        {
            Outline outline = currentHighlight.GetComponent<Outline>();
            if (outline != null)
            {
                outline.enabled = false;
            }
            currentHighlight = null;
        }
    }

    public void ForceCompleteCurrentStep()
    {
        CompleteCurrentStep();
    }

    public void SkipToStep(int stepIndex)
    {
        StopAllCoroutines();
        RemoveHighlight();
        StartStep(stepIndex);
    }

    /// <summary>
    /// ✅ FIX: Minigame tamamlandığında BoxingMinigameManager tarafından çağrılır
    /// </summary>
    public void OnMinigameCompleted()
    {
        if (currentStep != null && currentStep.conditionType == TutorialConditionType.CompleteMinigame)
        {
            if (showDebugLogs)
                Debug.Log("✅ Minigame completed - marking step as done");

            currentStep.isCompleted = true;

            StartCoroutine(CompleteStepNextFrame());
        }
    }

    /// <summary>
    /// ✅ FIX: Table etkileşimi düzeltildi - flag kullanılıyor
    /// </summary>
    public void OnTableInteraction(bool isPlacing)
    {
        if (currentStep == null) return;

        if (isPlacing && currentStep.conditionType == TutorialConditionType.PlaceOnTable)
        {
            if (showDebugLogs)
                Debug.Log("📦 Item placed on table - marking step for completion");

            tableInteractionCompleted = true;
        }
        else if (!isPlacing && currentStep.conditionType == TutorialConditionType.TakeFromTable)
        {
            if (showDebugLogs)
                Debug.Log("📦 Item taken from table - marking step for completion");

            tableInteractionCompleted = true;
        }
    }

    public void OnBoxTakenFromShelf(NetworkedShelf.BoxType boxType)
    {
        if (currentStep == null) return;

        if (currentStep.conditionType == TutorialConditionType.PickupItem)
        {
            if (currentStep.requiresItemPickup &&
                !string.IsNullOrEmpty(currentStep.requiredItemName) &&
                currentStep.requiredItemName.Contains("Box"))
            {
                if (showDebugLogs)
                    Debug.Log($"📦 {boxType} box taken from shelf - checking tutorial step");
            }
        }
    }

    private IEnumerator CompleteStepNextFrame()
    {
        yield return null;

        if (currentStep != null && currentStep.isCompleted)
        {
            CompleteCurrentStep();
        }
    }

    public int GetCurrentStepIndex()
    {
        return currentStepIndex;
    }

    public TutorialStep GetCurrentStep()
    {
        return currentStep;
    }

    private void OnDestroy()
    {
        RemoveHighlight();
    }
}