using UnityEngine;

namespace NewCss
{
    public class DayLightController : MonoBehaviour
    {
        [Header("Light Settings")]
        public Light directionalLight;
        
        [Header("Intensity Settings")]
        [Range(0f, 5f)]
        public float startIntensity = 3f;  // Gün başı intensity
        [Range(0f, 5f)]
        public float endIntensity = 0f;       // Gün sonu intensity
        
        [Header("Sun Position Settings")]
        [Tooltip("Güneşin doğduğu pozisyon (X rotasyon açısı)")]
        public float sunriseRotationX = -180f;  // Doğma pozisyonu
        
        [Tooltip("Güneşin öğlen pozisyonu (X rotasyon açısı)")]
        public float noonRotationX = 0f;        // Öğlen pozisyonu (12:00)
        
        [Tooltip("Güneşin battığı pozisyon (X rotasyon açısı)")]
        public float sunsetRotationX = 180f;    // Batma pozisyonu
        
        [Tooltip("Güneşin Y ekseni rotasyonu (sabit)")]
        public float sunYRotation = 0f;
        
        [Tooltip("Güneşin Z ekseni rotasyonu (sabit)")]
        public float sunZRotation = 0f;
        
        [Header("Optional: Color Settings")]
        public bool changeColor = false;
        public Color startColor = Color.white;     // Gün başı renk
        public Color endColor = new Color(1f, 0.5f, 0f); // Gün sonu renk (turuncu)
        
        [Header("Smooth Transition")]
        public bool smoothTransition = true;
        public float transitionSpeed = 2f;
        
        private float targetIntensity;
        private Color targetColor;
        private Vector3 targetRotation;
        
        void Start()
        {
            // Eğer directional light atanmamışsa, otomatik olarak bul
            if (directionalLight == null)
            {
                directionalLight = FindObjectOfType<Light>();
                if (directionalLight != null && directionalLight.type != LightType.Directional)
                {
                    // Directional light değilse, directional olanı bul
                    Light[] allLights = FindObjectsOfType<Light>();
                    foreach (Light light in allLights)
                    {
                        if (light.type == LightType.Directional)
                        {
                            directionalLight = light;
                            break;
                        }
                    }
                }
            }
            
            if (directionalLight == null)
            {
                Debug.LogError("DayLightController: Directional Light bulunamadı! Lütfen directionalLight field'ına atayın.");
                enabled = false;
                return;
            }
            
            // Başlangıç değerlerini ayarla
            directionalLight.intensity = startIntensity;
            directionalLight.transform.rotation = Quaternion.Euler(sunriseRotationX, sunYRotation, sunZRotation);
            
            if (changeColor)
            {
                directionalLight.color = startColor;
            }
            
            Debug.Log($"DayLightController başlatıldı. Light: {directionalLight.name}");
        }
        
        void Update()
        {
            // DayCycleManager kontrolü
            if (DayCycleManager.Instance == null)
                return;
                
            // Gün progress'ini al (0-1 arası)
            float dayProgress = GetDayProgress();
            
            // Target intensity'yi hesapla
            targetIntensity = Mathf.Lerp(startIntensity, endIntensity, dayProgress);
            
            // Target rotation'ı hesapla
            float targetRotationX = CalculateSunRotation(dayProgress);
            targetRotation = new Vector3(targetRotationX, sunYRotation, sunZRotation);
            
            // Target color'u hesapla (eğer renk değişimi aktifse)
            if (changeColor)
            {
                targetColor = Color.Lerp(startColor, endColor, dayProgress);
            }
            
            // Smooth transition kullan
            if (smoothTransition)
            {
                directionalLight.intensity = Mathf.Lerp(
                    directionalLight.intensity, 
                    targetIntensity, 
                    Time.deltaTime * transitionSpeed
                );
                
                // Rotation'ı smooth olarak değiştir
                directionalLight.transform.rotation = Quaternion.Slerp(
                    directionalLight.transform.rotation,
                    Quaternion.Euler(targetRotation),
                    Time.deltaTime * transitionSpeed
                );
                
                if (changeColor)
                {
                    directionalLight.color = Color.Lerp(
                        directionalLight.color, 
                        targetColor, 
                        Time.deltaTime * transitionSpeed
                    );
                }
            }
            else
            {
                // Direk değer ata
                directionalLight.intensity = targetIntensity;
                directionalLight.transform.rotation = Quaternion.Euler(targetRotation);
                
                if (changeColor)
                {
                    directionalLight.color = targetColor;
                }
            }
        }
        
        private float CalculateSunRotation(float dayProgress)
        {
            // dayProgress: 0 = 07:00, 0.455 = 12:00, 1 = 18:00
            // Toplam 11 saat (07:00-18:00), öğlen 5 saat sonra (12:00)
            
            float noonProgress = 5f / 11f; // ~0.455 - öğlen zamanı (5 saat / 11 saat)
            
            // 07:00-12:00 arası (dayProgress 0 - 0.625)
            if (dayProgress <= noonProgress)
            {
                float morningProgress = dayProgress / noonProgress; // 0-1 arası normalize et
                return Mathf.Lerp(sunriseRotationX, noonRotationX, morningProgress);
            }
            // 12:00-18:00 arası (dayProgress ~0.455 - 1)
            else
            {
                float afternoonProgress = (dayProgress - noonProgress) / (1f - noonProgress); // 0-1 arası normalize et
                return Mathf.Lerp(noonRotationX, sunsetRotationX, afternoonProgress);
            }
        }
        
        private float GetDayProgress()
        {
            if (DayCycleManager.Instance == null)
                return 0f;
                
            // DayCycleManager'dan elapsed time ve total duration al
            float elapsedTime = DayCycleManager.Instance.elapsedTime;
            float totalDuration = DayCycleManager.Instance.realDurationInSeconds;
            
            // Progress hesapla (0-1 arası)
            float progress = Mathf.Clamp01(elapsedTime / totalDuration);
            
            return progress;
        }
        
        // Gün yeniden başladığında çağrılacak
        void OnEnable()
        {
            if (DayCycleManager.Instance != null)
            {
                DayCycleManager.OnNewDay += OnNewDay;
            }
        }
        
        void OnDisable()
        {
            if (DayCycleManager.Instance != null)
            {
                DayCycleManager.OnNewDay -= OnNewDay;
            }
        }
        
        private void OnNewDay()
        {
            // Yeni gün başladığında değerleri başlangıç durumuna resetle
            if (directionalLight != null)
            {
                directionalLight.intensity = startIntensity;
                directionalLight.transform.rotation = Quaternion.Euler(sunriseRotationX, sunYRotation, sunZRotation);
                
                if (changeColor)
                {
                    directionalLight.color = startColor;
                }
            }
            
            Debug.Log("DayLightController: Yeni gün başladı, ışık ve pozisyon resetlendi.");
        }
        
        // Inspector'da test etmek için
        [ContextMenu("Test Day Progress")]
        void TestDayProgress()
        {
            if (Application.isPlaying && DayCycleManager.Instance != null)
            {
                float progress = GetDayProgress();
                float currentTime = DayCycleManager.Instance.CurrentTime;
                float rotationX = CalculateSunRotation(progress);
                
                // Gerçek saati hesapla (7 + progress * 11)
                float realTime = 7f + (progress * 11f);
                
                Debug.Log($"Day Progress: {progress:F3} ({progress * 100:F1}%)");
                Debug.Log($"Real Game Time: {realTime:F1}:00");
                Debug.Log($"DayCycle CurrentTime: {currentTime:F1}");
                Debug.Log($"Sun Rotation X: {rotationX:F1}°");
                Debug.Log($"Current Intensity: {directionalLight.intensity:F3}");
                Debug.Log($"Target Intensity: {targetIntensity:F3}");
                
                // Hangi aşamada olduğumuzu göster
                if (progress <= 0.625f)
                    Debug.Log(">> MORNING PHASE (Sunrise to Noon)");
                else
                    Debug.Log(">> AFTERNOON PHASE (Noon to Sunset)");
            }
        }
        
        [ContextMenu("Reset Light to Start")]
        void ResetLightToStart()
        {
            if (directionalLight != null)
            {
                directionalLight.intensity = startIntensity;
                directionalLight.transform.rotation = Quaternion.Euler(sunriseRotationX, sunYRotation, sunZRotation);
                if (changeColor)
                {
                    directionalLight.color = startColor;
                }
            }
        }
        
        [ContextMenu("Set Light to Noon")]
        void SetLightToNoon()
        {
            if (directionalLight != null)
            {
                directionalLight.transform.rotation = Quaternion.Euler(noonRotationX, sunYRotation, sunZRotation);
            }
        }
        
        [ContextMenu("Set Light to End")]
        void SetLightToEnd()
        {
            if (directionalLight != null)
            {
                directionalLight.intensity = endIntensity;
                directionalLight.transform.rotation = Quaternion.Euler(sunsetRotationX, sunYRotation, sunZRotation);
                if (changeColor)
                {
                    directionalLight.color = endColor;
                }
            }
        }
    }
}