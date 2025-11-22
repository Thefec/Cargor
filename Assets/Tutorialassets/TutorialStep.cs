using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class TutorialStep
{
    [Header("Step Info")]
    public string stepName = "Step";
    public int stepIndex = 0;

    [Header("Display Settings")]
    [TextArea(3, 5)]
    public string instructionText = "Talimatı buraya yazın...";

    [Header("Completion Conditions")]
    public TutorialConditionType conditionType = TutorialConditionType.PickupItem;

    [Tooltip("WaitForTime koşulu için bekleme süresi (saniye)")]
    public float waitDuration = 3f;

    [Tooltip("Item alma koşulu için gerekli mi?")]
    public bool requiresItemPickup = false;

    [Tooltip("Belirli bir item adı gerekiyor mu? (boş = herhangi bir item)")]
    public string requiredItemName = "";

    [Header("Step Events")]
    public UnityEvent onStepStart;
    public UnityEvent onStepComplete;

    [Header("Visual Helpers")]
    public GameObject objectToHighlight;
    public Transform targetPosition;

    [HideInInspector]
    public bool isCompleted = false;

    [HideInInspector]
    public float stepStartTime = 0f;
}

public enum TutorialConditionType
{
    PickupItem,          // Item aldı mı?
    DropItem,            // Item bıraktı mı?
    PlaceOnTable,        // ✅ YENİ: Masaya koydu mu?
    TakeFromTable,       // ✅ YENİ: Masadan aldı mı?
    PlaceOnShelf,        // Rafa koydu mu?
    CompleteMinigame,    // ✅ YENİ: Minigame tamamladı mı?
    EnterTrigger,        // Belirli bir trigger'a girdi mi?
    WaitForTime,         // Belirli süre geçti mi?
    PressKey,            // Belirli tuşa bastı mı?
    Custom               // Özel koşul
}