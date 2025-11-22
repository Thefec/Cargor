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
    [SerializeField] private CanvasGroup tutorialCanvasGroup; // Opsiyonel - fade için
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

        // Audio source oluştur
        if (typingSound != null && typingSoundSource == null)
        {
            typingSoundSource = gameObject.AddComponent<AudioSource>();
            typingSoundSource.playOnAwake = false;
            typingSoundSource.volume = typingSoundVolume;
        }

        // ✅ UI'yi baştan aktif yap ama text'i temizle
        InitializeUI();

        // Player'ı otomatik bul
        if (playerInventory == null)
        {
            StartCoroutine(FindLocalPlayer());
        }

        // Tutorial'ı başlat
        StartCoroutine(StartTutorialSequence());
    }

    /// <summary>
    /// UI'yi başlangıçta hazırla
    /// </summary>
    private void InitializeUI()
    {
        if (tutorialUI != null)
        {
            // UI'yi aktif yap
            tutorialUI.SetActive(true);
        }

        // Canvas Group varsa alpha'yı 1 yap (fade yok)
        if (tutorialCanvasGroup != null)
        {
            tutorialCanvasGroup.alpha = 1f;
        }

        // Text'i boşalt (başlangıçta görünmesin)
        if (instructionText != null)
        {
            instructionText.text = "";
        }

        // Skip hint'i gizle
        if (skipHintText != null)
        {
            skipHintText.gameObject.SetActive(false);
        }

        if (showDebugLogs)
            Debug.Log("✅ Tutorial UI initialized (always visible)");
    }

    private void Update()
    {
        // Skip tuşu kontrolü
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
            // ✅ FADE YOK - UI zaten açık, sadece text değiştir

            // Skip hint göster (WaitForTime tipindeyse)
            if (showSkipHint && skipHintText != null && currentStep.conditionType == TutorialConditionType.WaitForTime)
            {
                skipHintText.text = skipHintMessage.Replace("[SPACE]", $"[{skipKey}]");
                skipHintText.gameObject.SetActive(true);
            }

            // Typewriter efekti
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

        // ✅ Text'i direkt temizle (önceki yazı kalmasın)
        instructionText.text = "";

        for (int i = 0; i < fullText.Length; i++)
        {
            // Skip kontrolü
            if (skipTyping)
            {
                instructionText.text = fullText;
                break;
            }

            instructionText.text += fullText[i];

            // Yazma sesi çal
            if (typingSound != null && typingSoundSource != null && i % 2 == 0)
            {
                typingSoundSource.PlayOneShot(typingSound, typingSoundVolume);
            }

            // Noktalama işaretlerinde duraklama
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
        // ✅ FADE YOK - Sadece text'i temizle

        // Skip hint'i gizle
        if (skipHintText != null)
        {
            skipHintText.gameObject.SetActive(false);
        }

        // Text'i temizle (kısa süre için)
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

            case TutorialConditionType.WaitForTime:
                float elapsedTime = Time.time - currentStep.stepStartTime;
                bool timeComplete = elapsedTime >= currentStep.waitDuration;

                if (showDebugLogs && timeComplete)
                    Debug.Log($"⏰ Wait time completed: {elapsedTime:F1}s / {currentStep.waitDuration}s");

                return timeComplete;

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

        // Kapıları kontrol et
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
        // ✅ Fade yok, sadece kısa bekleme
        yield return StartCoroutine(HideInstruction());

        yield return new WaitForSeconds(0.3f); // Kısa geçiş

        isTransitioning = false;

        StartStep(currentStepIndex + 1);
    }

    private void CompleteTutorial()
    {
        if (showDebugLogs)
            Debug.Log("🎉 Tutorial completed!");

        // ✅ Tutorial bitince text'i temizle
        if (instructionText != null)
        {
            instructionText.text = "Tutorial tamamlandı!";
        }

        // Skip hint'i gizle
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
    // TutorialManager.cs içine EKLE (CompleteCurrentStep metodundan ÖNCE):

    /// <summary>
    /// ✅ Minigame tamamlandığında BoxingMinigameManager tarafından çağrılır
    /// </summary>
    public void OnMinigameCompleted()
    {
        if (currentStep != null && currentStep.conditionType == TutorialConditionType.CompleteMinigame)
        {
            if (showDebugLogs)
                Debug.Log("✅ Minigame completed - marking step as done");

            // Step'i tamamlanmış olarak işaretle
            currentStep.isCompleted = true;

            // Bir sonraki frame'de step'i tamamla
            StartCoroutine(CompleteStepNextFrame());
        }
    }

    // TutorialManager.cs içine EKLE:

    /// <summary>
    /// Masa etkileşimi olduğunda Table tarafından çağrılır
    /// </summary>
    public void OnTableInteraction(bool isPlacing)
    {
        if (currentStep == null) return;

        if (isPlacing && currentStep.conditionType == TutorialConditionType.PlaceOnTable)
        {
            if (showDebugLogs)
                Debug.Log("📦 Item placed on table - step will complete");
        }
        else if (!isPlacing && currentStep.conditionType == TutorialConditionType.TakeFromTable)
        {
            if (showDebugLogs)
                Debug.Log("📦 Item taken from table - step will complete");
        }
    }

    public void OnBoxTakenFromShelf(NetworkedShelf.BoxType boxType)
    {
        if (currentStep == null) return;

        if (currentStep.conditionType == TutorialConditionType.PickupItem)
        {
            // "Box" kelimesi içeren item alındı mı kontrol et
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
        yield return null; // Bir frame bekle

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