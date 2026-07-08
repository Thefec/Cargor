using UnityEngine;

namespace NewCss
{
    [RequireComponent(typeof(AudioSource))]
    public class BoxFallPenalty : MonoBehaviour
    {
        [Header("Penalty Settings")]
        [Tooltip("Money deducted when the box hits the ground")]
        public int dropMoneyPenalty = 10;

        [Header("Economy Settings")]
        [Tooltip("Tüm ekonomi sabitlerini içeren ScriptableObject")]
        public GameEconomySettings economySettings;

        // Backward-compat: SO atanmamışsa eski hard-coded değer
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

        private bool hasBeenThrown = false;
        private bool penaltyApplied = false;
        private Rigidbody rb;
        private AudioSource audioSource;
        private float throwVelocityThreshold = 1f;

        private void Awake()
        {
            if (economySettings == null)
            {
                economySettings = Resources.Load<GameEconomySettings>("EkonomiAyarlari");
            }

            rb = GetComponent<Rigidbody>();
            audioSource = GetComponent<AudioSource>();

            // AudioSource ayarlar�n� yap
            if (audioSource != null)
            {
                audioSource.playOnAwake = false;
                audioSource.volume = soundVolume;

                if (use2DSound)
                {
                    // 2D ses - her yerden ayn� �ekilde duyulur
                    audioSource.spatialBlend = 0f;
                }
                else
                {
                    // 3D ses - uzakl��a g�re azal�r
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

        public void SetThrown()
        {
            hasBeenThrown = true;
            penaltyApplied = false;
        }

        public void SetThrownWithVelocity()
        {
            if (rb != null && rb.linearVelocity.magnitude > throwVelocityThreshold)
            {
                SetThrown();
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!hasBeenThrown)
            {
                if (rb != null && rb.linearVelocity.magnitude > throwVelocityThreshold)
                {
                    SetThrown();
                }
                else
                {
                    return;
                }
            }

            if (penaltyApplied)
            {
                return;
            }

            if (IsGroundCollision(collision))
            {
                // �arpma h�z�n� al
                float impactVelocity = collision.relativeVelocity.magnitude;

                // Sesi �al
                PlayDropSound(impactVelocity);

                // Penalt� uygula
                ApplyPenalty();
            }
        }

        private bool IsGroundCollision(Collision collision)
        {
            GameObject hitObject = collision.gameObject;

            bool isGroundDirect = hitObject.CompareTag("Ground");
            bool isGroundRoot = collision.transform.root.CompareTag("Ground");
            bool isGroundLayer = hitObject.layer == LayerMask.NameToLayer("Ground");

            return isGroundDirect || isGroundRoot || isGroundLayer;
        }

        private void PlayDropSound(float impactVelocity)
        {
            // E�er ses dosyas� atanm��sa ve minimum h�z a��lm��sa
            if (boxDropSound != null && audioSource != null && impactVelocity >= minVelocityForSound)
            {
                // Ses ayarlar�n� g�ncelle
                if (use2DSound)
                {
                    // 2D ses i�in sabit volume
                    audioSource.volume = soundVolume;
                }
                else
                {
                    // 3D ses i�in �arpma h�z�na g�re volume (opsiyonel)
                    float velocityFactor = Mathf.Clamp01(impactVelocity / 10f);
                    // Minimum 0.5 volume garantisi
                    audioSource.volume = Mathf.Max(soundVolume * velocityFactor, soundVolume * 0.5f);
                }

                // Sesi �al
                audioSource.PlayOneShot(boxDropSound, soundVolume);

                Debug.Log($"Playing drop sound with velocity: {impactVelocity}, volume: {audioSource.volume}");
            }
        }

        private void ApplyPenalty()
        {
            bool penaltySuccessful = false;

            if (MoneySystem.Instance != null)
            {
                try
                {
                    MoneySystem.Instance.SpendMoney(dropMoneyPenalty);
                    penaltySuccessful = true;
                }
                catch (System.Exception e)
                {
                }
            }

            if (PrestigeManager.Instance != null)
            {
                try
                {
                    PrestigeManager.Instance.ModifyPrestige(dropPrestigePenalty);
                }
                catch (System.Exception e)
                {
                }
            }

            if (penaltySuccessful)
            {
                penaltyApplied = true;
                OnPenaltyApplied();
            }
        }

        private void OnPenaltyApplied()
        {
            // Override this method or add events here for visual/audio feedback
            // Example: Play additional sound effect, show particle effect, etc.
        }

        public void ResetPenalty()
        {
            hasBeenThrown = false;
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
            hasBeenThrown = true;
            penaltyApplied = false;
            ApplyPenalty();
        }

        public string GetState()
        {
            return $"HasBeenThrown: {hasBeenThrown}, PenaltyApplied: {penaltyApplied}, Velocity: {(rb != null ? rb.linearVelocity.magnitude.ToString("F2") : "No RB")}";
        }

        private void OnDrawGizmosSelected()
        {
            if (hasBeenThrown && !penaltyApplied)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(transform.position, 0.5f);
            }
            else if (penaltyApplied)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(transform.position, 0.5f);
            }
        }
    }
}