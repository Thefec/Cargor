using UnityEngine;
using Unity.Netcode;

namespace NewCss
{
    [RequireComponent(typeof(AudioSource))]
    public class BoxFallPenalty : MonoBehaviour
    {
        [Header("Economy Settings")]
        [Tooltip("Tüm ekonomi sabitlerini içeren ScriptableObject")]
        public GameEconomySettings economySettings;

        // Ceza değerleri artık merkezi SO'dan okunur (prefab başına gömülü değer YOK — tutarlı).
        // SO atanmamışsa Awake'de Resources.Load fallback devreye girer; o da yoksa güvenli varsayılan.
        private int dropMoneyPenalty => economySettings != null ? economySettings.boxDropMoneyPenalty : 5;
        private float dropPrestigePenalty => economySettings != null ? economySettings.boxDropPrestigePenalty : -0.05f;

        [Header("Sound Settings")]
        [Tooltip("Sound to play when box hits the ground")]
        public AudioClip boxDropSound;

        [Tooltip("Volume of the drop sound (0-1)")]
        [Range(0f, 1f)]
        public float soundVolume = 1f;

        [Tooltip("Minimum velocity for sound to play")]
        public float minVelocityForSound = 2f;

        [Tooltip("Use 2D sound instead of 3D spatial sound")]
        public bool use2DSound = false;

        [Tooltip("Maximum distance for 3D sound (only if use2DSound is false)")]
        public float maxSoundDistance = 50f;

        private bool penaltyApplied = false;
        private Rigidbody rb;
        private AudioSource audioSource;

        // Kırılma eşiğiyle (BoxDestroyOnCollisionNetcode.impactSpeedThreshold = 3) hizalı:
        // bu hızın ALTINDA çarpma (yumuşak bırakma) ne kırar ne de cezalandırır.
        private float impactSpeedThreshold = 3f;

        private void Awake()
        {
            if (economySettings == null)
            {
                economySettings = Resources.Load<GameEconomySettings>("EkonomiAyarlari");
            }

            rb = GetComponent<Rigidbody>();
            audioSource = GetComponent<AudioSource>();

            // AudioSource ayarlarını yap
            if (audioSource != null)
            {
                audioSource.playOnAwake = false;
                audioSource.volume = soundVolume;

                if (use2DSound)
                {
                    // 2D ses - her yerden aynı şekilde duyulur
                    audioSource.spatialBlend = 0f;
                }
                else
                {
                    // 3D ses - uzaklığa göre azalır
                    audioSource.spatialBlend = 1f;
                    audioSource.maxDistance = maxSoundDistance;
                    audioSource.rolloffMode = AudioRolloffMode.Linear;
                    audioSource.minDistance = 1f;
                }
            }
        }

        private void Start()
        {
            ValidateComponents();
        }

        // Geriye dönük uyumluluk (dışarıdan çağıran yok): ceza artık salt çarpma-hızına
        // dayalıdır; fırlatma/bırakma ayrımı yapılmaz. İmzalar korundu.
        public void SetThrown() { }

        public void SetThrownWithVelocity() { }

        private void OnCollisionEnter(Collision collision)
        {
            if (penaltyApplied) return;

            // Çarpma hızı — eşiğin altındaki yumuşak temaslar (nazik bırakma) cezasızdır.
            float impactVelocity = collision.relativeVelocity.magnitude;
            if (impactVelocity < impactSpeedThreshold) return;

            // Yüzey-bağımsız: yer/duvar/raf fark etmez; sert çarpma = ceza.
            // (Eski IsGroundCollision kısıtı kaldırıldı → tutarlı, öngörülebilir davranış.)
            PlayDropSound(impactVelocity);
            ApplyPenalty();
        }

        private void PlayDropSound(float impactVelocity)
        {
            // Eğer ses dosyası atanmışsa ve minimum hız aşılmışsa
            if (boxDropSound != null && audioSource != null && impactVelocity >= minVelocityForSound)
            {
                // Ses ayarlarını güncelle
                if (use2DSound)
                {
                    // 2D ses için sabit volume
                    audioSource.volume = soundVolume;
                }
                else
                {
                    // 3D ses için çarpma hızına göre volume (opsiyonel)
                    float velocityFactor = Mathf.Clamp01(impactVelocity / 10f);
                    // Minimum 0.5 volume garantisi
                    audioSource.volume = Mathf.Max(soundVolume * velocityFactor, soundVolume * 0.5f);
                }

                // Sesi çal
                audioSource.PlayOneShot(boxDropSound, soundVolume);

                Debug.Log($"Playing drop sound with velocity: {impactVelocity}, volume: {audioSource.volume}");
            }
        }

        private void ApplyPenalty()
        {
            // Ekonomik ceza (para/prestij) yalnizca server'da uygulanir.
            // Aksi halde her client kendi ServerRpc'sini tetikleyip cezayi
            // client sayisi kadar tekrarlar. Ses zaten bu guard'in disinda
            // kaliyor (OnCollisionEnter'da PlayDropSound ayri cagriliyor).
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            {
                return;
            }

            // P-pickup fix: eskiden penaltyApplied yalnizca MoneySystem basariyla cezalandirinca
            // latch'leniyordu. MoneySystem.Instance o an gecici null ise latch hic set olmuyor ve
            // bu carpmaya ait ceza sessizce sonraki (alakasiz) bir carpmaya "kayiyordu". Dogru
            // davranis: bu carpma ceza hakkini burada, sonuc ne olursa olsun tuketir - sistemler
            // musait degilse ceza O ANDA atlanir (log'lanir), sonraya tasinmaz.
            penaltyApplied = true;

            // SURPRISE AUDIT günü tüm cezalar 2× (yoksa 1×).
            float penaltyMult = EventEffectManager.Instance != null
                ? EventEffectManager.Instance.GetPenaltyMultiplier() : 1f;

            if (MoneySystem.Instance != null)
            {
                try
                {
                    MoneySystem.Instance.SpendMoney(Mathf.RoundToInt(dropMoneyPenalty * penaltyMult));
                }
                catch (System.Exception)
                {
                }
            }
            else
            {
                Debug.LogWarning($"{gameObject.name}: MoneySystem.Instance null - para cezasi bu carpma icin atlandi (latch yok).");
            }

            if (PrestigeManager.Instance != null)
            {
                try
                {
                    PrestigeManager.Instance.ModifyPrestige(dropPrestigePenalty * penaltyMult);
                }
                catch (System.Exception)
                {
                }
            }
            else
            {
                Debug.LogWarning($"{gameObject.name}: PrestigeManager.Instance null - prestij cezasi bu carpma icin atlandi (latch yok).");
            }

            OnPenaltyApplied();
        }

        private void OnPenaltyApplied()
        {
            // Override this method or add events here for visual/audio feedback
            // Example: Play additional sound effect, show particle effect, etc.
        }

        public void ResetPenalty()
        {
            penaltyApplied = false;
        }

        private void ValidateComponents()
        {
            if (audioSource == null)
            {
                Debug.LogWarning($"AudioSource component missing on {gameObject.name}");
            }

            if (boxDropSound == null)
            {
                Debug.LogWarning($"Box drop sound not assigned on {gameObject.name}");
            }
        }

        public void TestPenalty()
        {
            penaltyApplied = false;
            ApplyPenalty();
        }

        public string GetState()
        {
            return $"PenaltyApplied: {penaltyApplied}, Velocity: {(rb != null ? rb.linearVelocity.magnitude.ToString("F2") : "No RB")}";
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = penaltyApplied ? Color.red : Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
    }
}
