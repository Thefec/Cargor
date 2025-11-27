using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Tutorial koşul türleri
/// </summary>
public enum TutorialConditionType
{
    /// <summary>Item aldı mı? </summary>
    PickupItem,

    /// <summary>Item bıraktı mı?</summary>
    DropItem,

    /// <summary>Masaya koydu mu?</summary>
    PlaceOnTable,

    /// <summary>Masadan aldı mı?</summary>
    TakeFromTable,

    /// <summary>Rafa koydu mu? </summary>
    PlaceOnShelf,

    /// <summary>Raftan kutu aldı mı?</summary>
    TakeFromShelf,

    /// <summary>Minigame tamamladı mı?</summary>
    CompleteMinigame,

    /// <summary>Belirli bir trigger'a girdi mi?</summary>
    EnterTrigger,

    /// <summary>Belirli süre geçti mi? </summary>
    WaitForTime,

    /// <summary>Belirli tuşa bastı mı?</summary>
    PressKey,

    /// <summary>Özel koşul</summary>
    Custom
}

/// <summary>
/// Tutorial adımı - tek bir tutorial adımının tüm bilgilerini içerir. 
/// </summary>
[Serializable]
public class TutorialStep
{
    #region Step Info

    [Header("=== STEP INFO ===")]
    [Tooltip("Adım adı")]
    public string stepName = "Step";

    [Tooltip("Adım index'i")]
    public int stepIndex;

    #endregion

    #region Display Settings

    [Header("=== DISPLAY SETTINGS ===")]
    [TextArea(3, 5)]
    [Tooltip("Gösterilecek talimat metni")]
    public string instructionText = "Talimatı buraya yazın...";

    #endregion

    #region Completion Conditions

    [Header("=== COMPLETION CONDITIONS ===")]
    [Tooltip("Tamamlanma koşulu türü")]
    public TutorialConditionType conditionType = TutorialConditionType.PickupItem;

    [Tooltip("WaitForTime koşulu için bekleme süresi (saniye)")]
    public float waitDuration = 3f;

    [Tooltip("Item alma koşulu için gerekli mi? ")]
    public bool requiresItemPickup;

    [Tooltip("Belirli bir item adı gerekiyor mu? (boş = herhangi bir item)")]
    public string requiredItemName = "";

    [Tooltip("PressKey koşulu için gerekli tuş")]
    public KeyCode requiredKey = KeyCode.None;

    [Tooltip("EnterTrigger koşulu için trigger tag'i")]
    public string triggerTag = "";

    #endregion

    #region Shelf Conditions

    [Header("=== SHELF CONDITIONS ===")]
    [Tooltip("TakeFromShelf koşulu için belirli kutu türü gerekiyor mu?")]
    public bool requiresSpecificBoxType;

    [Tooltip("Gerekli kutu türü (Red, Yellow, Blue)")]
    public NewCss.NetworkedShelf.BoxType requiredBoxType;

    #endregion

    #region Step Events

    [Header("=== STEP EVENTS ===")]
    [Tooltip("Adım başladığında")]
    public UnityEvent onStepStart;

    [Tooltip("Adım tamamlandığında")]
    public UnityEvent onStepComplete;

    #endregion

    #region Visual Helpers

    [Header("=== VISUAL HELPERS ===")]
    [Tooltip("Vurgulanacak obje")]
    public GameObject objectToHighlight;

    [Tooltip("Hedef pozisyon")]
    public Transform targetPosition;

    [Tooltip("Ok/işaretçi göster")]
    public bool showArrow;

    [Tooltip("Hedef UI elementi")]
    public RectTransform targetUIElement;

    #endregion

    #region Runtime State (Hidden)

    [HideInInspector]
    public bool isCompleted;

    [HideInInspector]
    public float stepStartTime;

    #endregion

    #region Constructors

    public TutorialStep()
    {
        stepName = "Step";
        instructionText = "Talimatı buraya yazın...";
        conditionType = TutorialConditionType.PickupItem;
        waitDuration = 3f;
    }

    public TutorialStep(string name, string instruction, TutorialConditionType condition)
    {
        stepName = name;
        instructionText = instruction;
        conditionType = condition;
        waitDuration = 3f;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Adımı sıfırlar
    /// </summary>
    public void Reset()
    {
        isCompleted = false;
        stepStartTime = 0f;
    }

    /// <summary>
    /// Adımı başlatır
    /// </summary>
    public void Start()
    {
        stepStartTime = Time.time;
        isCompleted = false;
        onStepStart?.Invoke();
    }

    /// <summary>
    /// Adımı tamamlar
    /// </summary>
    public void Complete()
    {
        isCompleted = true;
        onStepComplete?.Invoke();
    }

    /// <summary>
    /// Geçen süreyi döndürür
    /// </summary>
    public float GetElapsedTime()
    {
        return Time.time - stepStartTime;
    }

    /// <summary>
    /// Kalan süreyi döndürür (WaitForTime için)
    /// </summary>
    public float GetRemainingTime()
    {
        if (conditionType != TutorialConditionType.WaitForTime)
            return 0f;

        float remaining = waitDuration - GetElapsedTime();
        return Mathf.Max(0f, remaining);
    }

    /// <summary>
    /// İlerleme yüzdesini döndürür (WaitForTime için)
    /// </summary>
    public float GetProgress()
    {
        if (conditionType != TutorialConditionType.WaitForTime || waitDuration <= 0f)
            return isCompleted ? 1f : 0f;

        return Mathf.Clamp01(GetElapsedTime() / waitDuration);
    }

    #endregion

    #region Validation

    /// <summary>
    /// Adımın geçerli olup olmadığını kontrol eder
    /// </summary>
    public bool IsValid()
    {
        if (string.IsNullOrEmpty(stepName))
            return false;

        if (string.IsNullOrEmpty(instructionText))
            return false;

        switch (conditionType)
        {
            case TutorialConditionType.WaitForTime:
                return waitDuration > 0f;

            case TutorialConditionType.PressKey:
                return requiredKey != KeyCode.None;

            case TutorialConditionType.EnterTrigger:
                return !string.IsNullOrEmpty(triggerTag);

            default:
                return true;
        }
    }

    /// <summary>
    /// Validasyon hata mesajını döndürür
    /// </summary>
    public string GetValidationError()
    {
        if (string.IsNullOrEmpty(stepName))
            return "Step name is empty";

        if (string.IsNullOrEmpty(instructionText))
            return "Instruction text is empty";

        switch (conditionType)
        {
            case TutorialConditionType.WaitForTime when waitDuration <= 0f:
                return "Wait duration must be greater than 0";

            case TutorialConditionType.PressKey when requiredKey == KeyCode.None:
                return "Required key is not set";

            case TutorialConditionType.EnterTrigger when string.IsNullOrEmpty(triggerTag):
                return "Trigger tag is not set";

            default:
                return null;
        }
    }

    #endregion

    #region Debug

    public override string ToString()
    {
        string status = isCompleted ? "[COMPLETED]" : "[PENDING]";
        return $"{status} {stepName} ({conditionType})";
    }

    #endregion
}