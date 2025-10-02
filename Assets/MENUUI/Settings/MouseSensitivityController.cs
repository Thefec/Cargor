using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MouseSensitivityController : MonoBehaviour
{
    [Header("UI Components")]
    public Slider sensitivitySlider;
    public TextMeshProUGUI sensitivityValueText;
    
    [Header("Sensitivity Settings")]
    [Range(0.1f, 5f)]
    public float defaultSensitivity = 1f;
    [Range(0.1f, 5f)]
    public float minSensitivity = 0.1f;
    [Range(0.1f, 5f)]
    public float maxSensitivity = 5f;
    
    [Header("Display Settings")]
    public int decimalPlaces = 1;
    public string valuePrefix = "";
    public string valueSuffix = "x";
    
    // Mouse control variables
    private Vector3 lastMousePosition;
    private Vector3 virtualMousePosition;
    private float currentSensitivity = 1f;
    private bool isControllingMouse = true;
    
    // Custom cursor (opsiyonel)
    [Header("Custom Cursor (Optional)")]
    public GameObject customCursor;
    public bool useCustomCursor = false;
    
    // PlayerPrefs key
    private const string SENSITIVITY_PREF_KEY = "MouseSensitivity";
    
    // Event
    public static System.Action<float> OnSensitivityChanged;
    public static MouseSensitivityController Instance { get; private set; }
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }
    
    void Start()
    {
        // Mouse'u serbest bırak
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = !useCustomCursor;
        
        // Virtual mouse pozisyonunu başlat
        virtualMousePosition = Input.mousePosition;
        lastMousePosition = Input.mousePosition;
        
        InitializeSlider();
        LoadSensitivity();
        SetupSliderListener();
        UpdateUI();
        
        // Custom cursor setup
        if (useCustomCursor && customCursor != null)
        {
            customCursor.SetActive(true);
        }
    }
    
    void Update()
    {
        if (isControllingMouse)
        {
            HandleMouseMovement();
        }
        
        // Custom cursor pozisyonunu güncelle
        if (useCustomCursor && customCursor != null)
        {
            customCursor.transform.position = virtualMousePosition;
        }
    }
    
    void HandleMouseMovement()
    {
        // Gerçek mouse hareketini al
        Vector3 currentMousePosition = Input.mousePosition;
        Vector3 mouseDelta = currentMousePosition - lastMousePosition;
        
        // Sensitivity uygula
        Vector3 adjustedDelta = mouseDelta * currentSensitivity;
        
        // Virtual mouse pozisyonunu güncelle
        virtualMousePosition += adjustedDelta;
        
        // Ekran sınırları içinde tut
        virtualMousePosition.x = Mathf.Clamp(virtualMousePosition.x, 0, Screen.width);
        virtualMousePosition.y = Mathf.Clamp(virtualMousePosition.y, 0, Screen.height);
        
        // Son pozisyonu kaydet
        lastMousePosition = currentMousePosition;
        
        // Mouse eventlerini simüle et (UI için)
        SimulateMouseEvents();
    }
    
    void SimulateMouseEvents()
    {
        // Virtual mouse pozisyonunda UI elementleriyle etkileşim
        // Bu kısım UI raycast için gerekli
        
        // Mouse pozisyonunu global olarak güncelle
        if (useCustomCursor)
        {
            // Custom cursor kullanıyorsak, mouse pozisyonunu gizle
            Cursor.visible = false;
        }
    }
    
    void InitializeSlider()
    {
        if (sensitivitySlider != null)
        {
            sensitivitySlider.minValue = minSensitivity;
            sensitivitySlider.maxValue = maxSensitivity;
        }
    }
    
    void SetupSliderListener()
    {
        if (sensitivitySlider != null)
        {
            sensitivitySlider.onValueChanged.AddListener(OnSensitivitySliderChanged);
        }
    }
    
    void LoadSensitivity()
    {
        float savedSensitivity = PlayerPrefs.GetFloat(SENSITIVITY_PREF_KEY, defaultSensitivity);
        savedSensitivity = Mathf.Clamp(savedSensitivity, minSensitivity, maxSensitivity);
        
        if (sensitivitySlider != null)
        {
            sensitivitySlider.value = savedSensitivity;
        }
        
        currentSensitivity = savedSensitivity;
        Debug.Log($"Mouse sensitivity yüklendi: {currentSensitivity}");
    }
    
    public void OnSensitivitySliderChanged(float value)
    {
        currentSensitivity = value;
        UpdateUI();
        OnSensitivityChanged?.Invoke(currentSensitivity);
        
        Debug.Log($"Mouse sensitivity değiştirildi: {currentSensitivity}");
    }
    
    void UpdateUI()
    {
        if (sensitivityValueText != null)
        {
            string formattedValue = currentSensitivity.ToString($"F{decimalPlaces}");
            sensitivityValueText.text = $"{valuePrefix}{formattedValue}{valueSuffix}";
        }
    }
    
    public void SaveSensitivity()
    {
        PlayerPrefs.SetFloat(SENSITIVITY_PREF_KEY, currentSensitivity);
        PlayerPrefs.Save();
        Debug.Log($"Mouse sensitivity kaydedildi: {currentSensitivity}");
    }
    
    public void ResetSensitivity()
    {
        if (sensitivitySlider != null)
        {
            sensitivitySlider.value = defaultSensitivity;
        }
        currentSensitivity = defaultSensitivity;
        UpdateUI();
        Debug.Log($"Mouse sensitivity sıfırlandı: {currentSensitivity}");
    }
    
    // Mouse kontrolünü aç/kapat
    public void EnableMouseControl(bool enable)
    {
        isControllingMouse = enable;
        if (!enable)
        {
            // Normal mouse moduna geç
            Cursor.visible = true;
            if (useCustomCursor && customCursor != null)
            {
                customCursor.SetActive(false);
            }
        }
        else
        {
            // Controlled mouse moduna geç
            Cursor.visible = !useCustomCursor;
            if (useCustomCursor && customCursor != null)
            {
                customCursor.SetActive(true);
            }
            virtualMousePosition = Input.mousePosition;
        }
    }
    
    // Virtual mouse pozisyonunu al (diğer scriptler için)
    public Vector3 GetVirtualMousePosition()
    {
        return virtualMousePosition;
    }
    
    // Virtual mouse pozisyonunu ayarla
    public void SetVirtualMousePosition(Vector3 position)
    {
        virtualMousePosition = position;
        virtualMousePosition.x = Mathf.Clamp(virtualMousePosition.x, 0, Screen.width);
        virtualMousePosition.y = Mathf.Clamp(virtualMousePosition.y, 0, Screen.height);
    }
    
    // Static methodlar
    public static float GetSensitivity()
    {
        return Instance != null ? Instance.currentSensitivity : 1f;
    }
    
    public static void SetSensitivity(float newSensitivity)
    {
        if (Instance != null)
        {
            Instance.currentSensitivity = newSensitivity;
            Instance.UpdateUI();
            if (Instance.sensitivitySlider != null)
            {
                Instance.sensitivitySlider.value = newSensitivity;
            }
            OnSensitivityChanged?.Invoke(newSensitivity);
        }
    }
    
    public static Vector3 GetMousePosition()
    {
        return Instance != null ? Instance.GetVirtualMousePosition() : Input.mousePosition;
    }
    
    void OnDestroy()
    {
        if (sensitivitySlider != null)
        {
            sensitivitySlider.onValueChanged.RemoveAllListeners();
        }
        
        if (Instance == this)
        {
            Instance = null;
        }
    }
    
    public bool HasUnsavedChanges()
    {
        float savedSensitivity = PlayerPrefs.GetFloat(SENSITIVITY_PREF_KEY, defaultSensitivity);
        return !Mathf.Approximately(currentSensitivity, savedSensitivity);
    }
    
    public void ResetToSavedSettings()
    {
        LoadSensitivity();
        UpdateUI();
        if (sensitivitySlider != null)
        {
            sensitivitySlider.value = currentSensitivity;
        }
    }
    
    // Debug için
    void OnGUI()
    {
        if (Debug.isDebugBuild)
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 100));
            GUILayout.Label($"Mouse Sensitivity: {currentSensitivity:F2}");
            GUILayout.Label($"Real Mouse: {Input.mousePosition}");
            GUILayout.Label($"Virtual Mouse: {virtualMousePosition}");
            GUILayout.Label($"Controlling: {isControllingMouse}");
            GUILayout.EndArea();
        }
    }
}

// Diğer scriptlerin virtual mouse pozisyonunu kullanması için helper class
public static class VirtualMouse
{
    public static Vector3 Position
    {
        get { return MouseSensitivityController.GetMousePosition(); }
    }
    
    public static float Sensitivity
    {
        get { return MouseSensitivityController.GetSensitivity(); }
        set { MouseSensitivityController.SetSensitivity(value); }
    }
    
    // Screen-to-world point dönüşümü
    public static Vector3 ScreenToWorldPoint(Camera camera)
    {
        return camera.ScreenToWorldPoint(Position);
    }
    
    // UI raycast için
    public static bool IsOverUI()
    {
        return UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
    }
}