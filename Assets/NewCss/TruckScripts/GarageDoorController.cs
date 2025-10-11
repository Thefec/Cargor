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

        [Header("Debug")]
        [SerializeField] private bool isOpen = false;
        [SerializeField] private bool hasOpenedToday = false;
        [SerializeField] private bool hasClosedToday = false;
        [SerializeField] private DoorState currentDoorState = DoorState.Closed;

        // Kapı durumları
        public enum DoorState
        {
            Closed,    // StartIdle state'inde - kapalı
            Open,      // Open state'inde - açık durumda kalıyor
            Closing    // Close animasyonu oynatılıyor
        }

        // Private değişkenler
        private DayCycleManager dayCycleManager;
        private int lastCheckedDay = 0;
        private float doorAnimationDuration = 2f; // Animasyon süresi (ayarlanabilir)
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

        private void UpdateAnimationTimer()
        {
            if (currentDoorState == DoorState.Closing)
            {
                animationTimer += Time.deltaTime;

                // Sadece kapanma animasyonu için timer - açılma animasyonu bitince Open state'inde kalacak
                if (animationTimer >= doorAnimationDuration)
                {
                    // Kapanma animasyonu bitti, kapalı duruma geç
                    currentDoorState = DoorState.Closed;
                    isOpen = false;

                    animationTimer = 0f;
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
                // Animator DoOpen trigger'ı ile Open state'ine geçecek ve orada kalacak
                currentDoorState = DoorState.Open;
                isOpen = true;

            }
        }

        private void StartCloseDoor()
        {
            if (currentDoorState == DoorState.Open)
            {
                doorAnimator.SetTrigger("DoClose");
                currentDoorState = DoorState.Closing;
                animationTimer = 0f;

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
            else if (currentDoorState == DoorState.Open)
            {
                // Eğer açılma sırasında kapatma komutu gelirse
                doorAnimator.SetTrigger("DoClose");
                currentDoorState = DoorState.Closing;
                animationTimer = 0f;
            }
        }

        // Inspector'da mevcut durumu görmek için
        void OnValidate()
        {
            if (openTime < 0) openTime = 0;
            if (openTime > 24) openTime = 24;
            if (closeTime < 0) closeTime = 0;
            if (closeTime > 24) closeTime = 24;

            if (doorAnimationDuration < 0.1f) doorAnimationDuration = 0.1f;
        }

        // Public property'ler (diğer scriptler için)
        public bool IsOpen => isOpen;
        public DoorState CurrentState => currentDoorState;
    }
}