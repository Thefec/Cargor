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

    /// <summary>Item bıraktı mı? </summary>
    DropItem,

    /// <summary>Masaya koydu mu?</summary>
    PlaceOnTable,

    /// <summary>Masadan aldı mı? </summary>
    TakeFromTable,

    /// <summary>Rafa koydu mu?</summary>
    PlaceOnShelf,

    /// <summary>Raftan kutu aldı mı? </summary>
    TakeFromShelf,

    /// <summary>Minigame tamamladı mı? </summary>
    CompleteMinigame,

    /// <summary>Belirli bir trigger'a girdi mi?</summary>
    EnterTrigger,

    /// <summary>Belirli süre geçti mi?</summary>
    WaitForTime,

    /// <summary>Belirli tuşa bastı mı? </summary>
    PressKey,

    /// <summary>Araca kutu teslim etti mi?</summary>
    DeliverToTruck,

    /// <summary>Müşteriyle etkileşime girdi mi (E ile)? </summary>
    InteractWithCustomer,

    /// <summary>Özel koşul</summary>
    Custom,

    /// <summary>Açık+dolu kutuyu bantla kapattı mı (Table.PerformSealing başarı yolu)? </summary>
    SealBox
}

/// <summary>
/// Tutorial adımı - tek bir tutorial adımının tüm bilgilerini içerir. 
/// Çoklu dil desteği ile Türkçe ve İngilizce metinler içerir.
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

    #region Display Settings - Localized

    [Header("=== LOCALIZATION ===")]
    [Tooltip("StringTable'daki key adı")]
    public string instructionLocalizationKey = "";

    #endregion

    #region Completion Conditions

    [Header("=== COMPLETION CONDITIONS ===")]
    [Tooltip("Tamamlanma koşulu türü")]
    public TutorialConditionType conditionType = TutorialConditionType.PickupItem;

    [Tooltip("WaitForTime koşulu için bekleme süresi (saniye)")]
    public float waitDuration = 3f;

    [Tooltip("Item alma koşulu için gerekli mi?")]
    public bool requiresItemPickup;

    [Tooltip("Belirli bir item adı gerekiyor mu?  (boş = herhangi bir item)")]
    public string requiredItemName = "";

    [Tooltip("PressKey koşulu için gerekli tuş")]
    public KeyCode requiredKey = KeyCode.None;

    [Tooltip("EnterTrigger koşulu için trigger tag'i")]
    public string triggerTag = "";

    #endregion

    #region Shelf Conditions

    [Header("=== SHELF CONDITIONS ===")]
    [Tooltip("TakeFromShelf koşulu için belirli kutu türü gerekiyor mu? ")]
    public bool requiresSpecificBoxType;

    [Tooltip("Gerekli kutu türü (RAF içi tip). DİKKAT: NewCss.NetworkedShelf.BoxType " +
        "enum sırası (Red=0, Blue=1, Yellow=2) ile aşağıdaki requiredTruckBoxType alanının " +
        "tipi olan NewCss.BoxInfo.BoxType (Yellow=0, Blue=1, Red=2) FARKLI SIRADA. " +
        "Bu iki enum arasında ASLA int cast yapma (örn. (BoxInfo.BoxType)(int)requiredBoxType) " +
        "— renkler karışır. Karşılaştırma/dönüşüm gerekiyorsa isim (.ToString()) veya " +
        "açık switch-case eşlemesi kullan.")]
    public NewCss.NetworkedShelf.BoxType requiredBoxType;

    #endregion

    #region Truck Delivery Conditions

    [Header("=== TRUCK DELIVERY CONDITIONS ===")]
    [Tooltip("DeliverToTruck koşulu için gerekli teslimat sayısı")]
    public int requiredDeliveryCount = 1;

    [Tooltip("DeliverToTruck koşulu için belirli kutu türü gerekiyor mu?")]
    public bool requiresSpecificBoxTypeForTruck;

    [Tooltip("Truck için gerekli kutu türü")]
    public NewCss.BoxInfo.BoxType requiredTruckBoxType;

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

    [HideInInspector]
    public int currentDeliveryCount;

    #endregion

    #region Constructors

    public TutorialStep()
    {
        stepName = "Step";
        instructionLocalizationKey = "";
        conditionType = TutorialConditionType.PickupItem;
        waitDuration = 3f;
        requiredDeliveryCount = 1;
    }

    public TutorialStep(string name, string instructionKey, TutorialConditionType condition)
    {
        stepName = name;
        instructionLocalizationKey = instructionKey;
        conditionType = condition;
        waitDuration = 3f;
        requiredDeliveryCount = 1;
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
        currentDeliveryCount = 0;
    }

    /// <summary>
    /// Adımı başlatır
    /// </summary>
    public void Start()
    {
        stepStartTime = Time.time;
        isCompleted = false;
        currentDeliveryCount = 0;
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
    /// Teslimat sayısını artırır ve tamamlanıp tamamlanmadığını döndürür
    /// </summary>
    public bool IncrementDeliveryCount()
    {
        currentDeliveryCount++;
        return currentDeliveryCount >= requiredDeliveryCount;
    }

    /// <summary>
    /// Teslimat tamamlandı mı kontrol eder
    /// </summary>
    public bool IsDeliveryComplete()
    {
        return currentDeliveryCount >= requiredDeliveryCount;
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
    /// İlerleme yüzdesini döndürür
    /// </summary>
    public float GetProgress()
    {
        switch (conditionType)
        {
            case TutorialConditionType.WaitForTime:
                if (waitDuration <= 0f) return isCompleted ? 1f : 0f;
                return Mathf.Clamp01(GetElapsedTime() / waitDuration);

            case TutorialConditionType.DeliverToTruck:
                if (requiredDeliveryCount <= 0) return isCompleted ? 1f : 0f;
                return Mathf.Clamp01((float)currentDeliveryCount / requiredDeliveryCount);

            default:
                return isCompleted ? 1f : 0f;
        }
    }

    /// <summary>
    /// Teslimat durumunu string olarak döndürür
    /// </summary>
    public string GetDeliveryStatusText()
    {
        return $"{currentDeliveryCount}/{requiredDeliveryCount}";
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

        if (string.IsNullOrEmpty(instructionLocalizationKey))
            return false;

        switch (conditionType)
        {
            case TutorialConditionType.WaitForTime:
                return waitDuration > 0f;

            case TutorialConditionType.PressKey:
                return requiredKey != KeyCode.None;

            case TutorialConditionType.EnterTrigger:
                return !string.IsNullOrEmpty(triggerTag);

            case TutorialConditionType.DeliverToTruck:
                return requiredDeliveryCount > 0;

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

        if (string.IsNullOrEmpty(instructionLocalizationKey))
            return "Instruction localization key is empty";

        switch (conditionType)
        {
            case TutorialConditionType.WaitForTime when waitDuration <= 0f:
                return "Wait duration must be greater than 0";

            case TutorialConditionType.PressKey when requiredKey == KeyCode.None:
                return "Required key is not set";

            case TutorialConditionType.EnterTrigger when string.IsNullOrEmpty(triggerTag):
                return "Trigger tag is not set";

            case TutorialConditionType.DeliverToTruck when requiredDeliveryCount <= 0:
                return "Required delivery count must be greater than 0";

            default:
                return null;
        }
    }

    #endregion

    #region Debug

    public override string ToString()
    {
        string status = isCompleted ? "[COMPLETED]" : "[PENDING]";
        string extra = conditionType == TutorialConditionType.DeliverToTruck
            ? $" ({currentDeliveryCount}/{requiredDeliveryCount})"
            : "";
        return $"{status} {stepName} ({conditionType}){extra}";
    }

    #endregion
}