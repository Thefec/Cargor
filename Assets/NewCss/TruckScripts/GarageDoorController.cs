using UnityEngine;

namespace NewCss
{
    public class GarageDoorController : MonoBehaviour
    {
        [Header("Garaj Kapısı Ayarları")]
        [Tooltip("Kapının açılacağı saat (örnek: 7.5 = 07:30, 8.25 = 08:15)")]
        public float openTime = 7.5f;

        [Tooltip("Kapının kapanacağı saat (örnek: 14.5 = 14:30, 16.75 = 16:45)")]
        public float closeTime = 14.5f;

        [Header("Referanslar")]
        public Animator doorAnimator;

        // YENİ EKLENDİ: Animasyon kliplerini buraya sürükleyin
        [Tooltip("Kapının açılma animasyon klibi")]
        public AnimationClip openAnimation;
        [Tooltip("Kapının kapanma animasyon klibi")]
        public AnimationClip closeAnimation;

        [Header("Motor Sesi")]
        public AudioClip motorSound; // Motor ses dosyası
        [Range(0f, 1f)]
        [Tooltip("0 = 2D (Herkes Duyar), 1 = 3D (Yakındakiler Duyar)")]
        public float spatialBlend = 0.7f;
        [Range(0f, 100f)]
        [Tooltip("3D ses için maksimum duyulma mesafesi")]
        public float maxHearingDistance = 20f;
        [Range(0f, 1f)]
        [Tooltip("Motor ses seviyesi")]
        public float motorVolume = 0.8f;

        private AudioSource motorAudioSource;

        [Header("Debug")]
        [SerializeField] private bool isOpen = false;
        [SerializeField] private bool hasOpenedToday = false;
        [SerializeField] private bool hasClosedToday = false;
        [SerializeField] private DoorState currentDoorState = DoorState.Closed;

        // Kapı durumları
        public enum DoorState
        {
            Closed,     // StartIdle state'inde - kapalı
            Open,       // Open state'inde - açık durumda kalıyor
            Opening,    // Açılma animasyonu oynatılıyor
            Closing     // Close animasyonu oynatılıyor
        }

        // Private değişkenler
        private DayCycleManager dayCycleManager;
        private int lastCheckedDay = 0;

        // Bu değişken artık dinamik olarak ayarlanacak (varsayılan değer olarak kalabilir)
        private float doorAnimationDuration = 2f;
        private float animationTimer = 0f;

        void Start()
        {
            // DayCycleManager referansını al
            dayCycleManager = DayCycleManager.Instance;

            if (dayCycleManager == null)
            {
                return;
            }

            if (doorAnimator == null)
            {
                return;
            }

            // GÜNCELLEME: Animasyon kliplerinin atanıp atanmadığını kontrol edelim
            if (openAnimation == null || closeAnimation == null)
            {
                Debug.LogError("GarageDoorController: Lütfen 'Open Animation' ve 'Close Animation' kliplerini Inspector üzerinden atayın!", this);
                // Varsayılan süreyi (2f) kullanmaya devam edecek
            }

            // AudioSource component'ini al veya ekle
            InitializeAudioSource();

            // Yeni gün eventine abone ol
            DayCycleManager.OnNewDay += OnNewDay;

            // İlk günün kontrollerini sıfırla
            ResetDailyFlags();

            // Başlangıçta kapı kapalı durumda
            InitializeDoorState();
        }

        void Update()
        {
            if (dayCycleManager == null || doorAnimator == null) return;

            // Günün değişip değişmediğini kontrol et
            if (dayCycleManager.currentDay != lastCheckedDay)
            {
                ResetDailyFlags();
                lastCheckedDay = dayCycleManager.currentDay;
            }

            // Animasyon timer'ını güncelle
            UpdateAnimationTimer();

            // Mevcut saati al (DayCycleManager'dan)
            float currentGameTime = GetCurrentGameTime();

            // Açılma zamanını kontrol et
            if (!hasOpenedToday && currentGameTime >= openTime && currentDoorState == DoorState.Closed)
            {
                StartOpenDoor();
                hasOpenedToday = true;
            }

            // Kapanma zamanını kontrol et
            if (!hasClosedToday && currentGameTime >= closeTime && currentDoorState == DoorState.Open)
            {
                StartCloseDoor();
                hasClosedToday = true;
            }
        }

        private void InitializeAudioSource()
        {
            motorAudioSource = GetComponent<AudioSource>();
            if (motorAudioSource == null)
            {
                motorAudioSource = gameObject.AddComponent<AudioSource>();
            }

            // AudioSource ayarları
            motorAudioSource.playOnAwake = false;
            motorAudioSource.loop = true; // Loop açık - animasyon boyunca çalacak
            motorAudioSource.spatialBlend = spatialBlend;
            motorAudioSource.volume = motorVolume;
            motorAudioSource.minDistance = 1f;
            motorAudioSource.maxDistance = maxHearingDistance;
            motorAudioSource.rolloffMode = AudioRolloffMode.Linear;
            motorAudioSource.clip = motorSound;
        }

        private void UpdateAnimationTimer()
        {
            // Açılma animasyonu kontrolü
            if (currentDoorState == DoorState.Opening)
            {
                animationTimer += Time.deltaTime;

                // GÜNCELLEME: Artık 'doorAnimationDuration' doğru süreyi tutacak
                if (animationTimer >= doorAnimationDuration)
                {
                    // Açılma animasyonu bitti
                    currentDoorState = DoorState.Open;
                    isOpen = true;
                    animationTimer = 0f;

                    // Motor sesini durdur
                    StopMotorSound();
                }
            }
            // Kapanma animasyonu kontrolü
            else if (currentDoorState == DoorState.Closing)
            {
                animationTimer += Time.deltaTime;

                // GÜNCELLEME: Artık 'doorAnimationDuration' doğru süreyi tutacak
                if (animationTimer >= doorAnimationDuration)
                {
                    // Kapanma animasyonu bitti, kapalı duruma geç
                    currentDoorState = DoorState.Closed;
                    isOpen = false;
                    animationTimer = 0f;

                    // Motor sesini durdur
                    StopMotorSound();
                }
            }
        }

        private void InitializeDoorState()
        {
            // Oyun başlangıcında kapı her zaman StartIdle (kapalı) durumunda
            currentDoorState = DoorState.Closed;
            isOpen = false;
            animationTimer = 0f;

            // Animator'ı StartIdle state'ine zorla (eğer gerekirse)
            doorAnimator.Rebind();
            doorAnimator.Update(0f);

            // Sesi durdur (güvenlik için)
            StopMotorSound();
        }

        private float GetCurrentGameTime()
        {
            return dayCycleManager.CurrentTime;
        }

        private string GetTimeString(float time)
        {
            int hours = Mathf.FloorToInt(time);
            int minutes = Mathf.FloorToInt((time - hours) * 60f);
            return $"{hours:D2}:{minutes:D2}";
        }

        private void StartOpenDoor()
        {
            if (currentDoorState == DoorState.Closed)
            {
                doorAnimator.SetTrigger("DoOpen");
                currentDoorState = DoorState.Opening; // Opening state'ine geç
                animationTimer = 0f;

                // GÜNCELLEME: Animasyon süresini klipten al
                if (openAnimation != null)
                {
                    doorAnimationDuration = openAnimation.length;
                }
                else
                {
                    doorAnimationDuration = 2f; // Fallback
                    Debug.LogWarning("Open Animation klibi atanmamış! Varsayılan süre (2s) kullanılıyor.");
                }

                // Motor sesini başlat (loop olarak çalacak)
                PlayMotorSound();
            }
        }

        private void StartCloseDoor()
        {
            if (currentDoorState == DoorState.Open)
            {
                doorAnimator.SetTrigger("DoClose");
                currentDoorState = DoorState.Closing;
                animationTimer = 0f;

                // GÜNCELLEME: Animasyon süresini klipten al
                if (closeAnimation != null)
                {
                    doorAnimationDuration = closeAnimation.length;
                }
                else
                {
                    doorAnimationDuration = 2f; // Fallback
                    Debug.LogWarning("Close Animation klibi atanmamış! Varsayılan süre (2s) kullanılıyor.");
                }

                // Motor sesini başlat (loop olarak çalacak)
                PlayMotorSound();
            }
        }

        private void PlayMotorSound()
        {
            if (motorAudioSource != null && motorSound != null)
            {
                if (!motorAudioSource.isPlaying)
                {
                    motorAudioSource.Play();
                }
            }
        }

        private void StopMotorSound()
        {
            if (motorAudioSource != null && motorAudioSource.isPlaying)
            {
                motorAudioSource.Stop();
            }
        }

        private void ResetDailyFlags()
        {
            hasOpenedToday = false;
            hasClosedToday = false;

            // Yeni gün başladığında kapının kapalı olduğundan emin ol
            if (currentDoorState != DoorState.Closed && currentDoorState != DoorState.Closing)
            {
                ForceCloseDoor();
            }
        }

        private void OnNewDay()
        {
            ResetDailyFlags();
        }

        void OnDestroy()
        {
            DayCycleManager.OnNewDay -= OnNewDay;

            // Sesi durdur
            StopMotorSound();
        }

        // Manuel kontrol metodları (test amaçlı)
        [ContextMenu("Kapıyı Aç")]
        public void ForceOpenDoor()
        {
            if (currentDoorState == DoorState.Closed)
            {
                StartOpenDoor();
            }
        }

        [ContextMenu("Kapıyı Kapat")]
        public void ForceCloseDoor()
        {
            if (currentDoorState == DoorState.Open)
            {
                StartCloseDoor();
            }
            else if (currentDoorState == DoorState.Opening)
            {
                // Eğer açılma sırasında kapatma komutu gelirse
                doorAnimator.SetTrigger("DoClose");
                currentDoorState = DoorState.Closing;
                animationTimer = 0f;

                // GÜNCELLEME: Manuel kapatmada da süreyi ayarla
                if (closeAnimation != null)
                {
                    doorAnimationDuration = closeAnimation.length;
                }
                else
                {
                    doorAnimationDuration = 2f; // Fallback
                    Debug.LogWarning("Close Animation klibi atanmamış! Varsayılan süre (2s) kullanılıyor.");
                }

                // Ses zaten çalıyor, devam etsin
            }
        }

        // Inspector'da mevcut durumu görmek için
        void OnValidate()
        {
            if (openTime < 0) openTime = 0;
            if (openTime > 24) openTime = 24;
            if (closeTime < 0) closeTime = 0;
            if (closeTime > 24) closeTime = 24;

            // Bu satırı silebilir veya bırakabilirsiniz, artık çok kritik değil
            // if (doorAnimationDuration < 0.1f) doorAnimationDuration = 0.1f;
        }

        // Public property'ler (diğer scriptler için)
        public bool IsOpen => isOpen;
        public DoorState CurrentState => currentDoorState;
    }
}