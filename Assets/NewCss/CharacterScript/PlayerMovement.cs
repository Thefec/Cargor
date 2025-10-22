using Unity.Netcode;
using UnityEngine;

namespace NewCss
{
    public class PlayerMovement : NetworkBehaviour
    {
        [Header("Movement Settings")]
        public float moveSpeed = 5f;
        public float sprintSpeed = 7f;
        public float exhaustedSpeed = 3f;

        [Header("Sprint Settings")]
        public float sprintDuration = 3f;
        public float sprintCooldown = 3f;
        public float staminaRegenRate = 1f;

        [Header("Audio Settings")]
        public AudioClip[] walkSounds;
        public AudioClip[] runSounds;
        [Range(0f, 1f)]
        public float footstepVolume = 0.5f;

        private CharacterController controller;
        private Animator animator;
        private AudioSource audioSource;

        private float currentStamina;
        private float cooldownTimer = 0f;

        private bool isSprinting = false;
        private bool isInCooldown = false;
        private bool isMovementLocked = false;

        private Vector3 velocity;
        private float rotationSpeed = 10f;
        private float gravity = 9.81f;

        public float CurrentStamina => currentStamina;
        public float MaxStamina => sprintDuration;
        public float CooldownTime => cooldownTimer;
        public float MaxCooldown => sprintCooldown;
        public bool IsInCooldown => isInCooldown;
        private bool isCarrying = false;
        private bool interactionsLocked = false;

        private NetworkVariable<float> networkX = new NetworkVariable<float>();
        private NetworkVariable<float> networkZ = new NetworkVariable<float>();
        private NetworkVariable<bool> networkIsRunning = new NetworkVariable<bool>();
        private NetworkVariable<bool> networkIsCarrying = new NetworkVariable<bool>();

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            animator = GetComponent<Animator>();

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            audioSource.spatialBlend = 1f;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.minDistance = 1f;
            audioSource.maxDistance = 15f;
            audioSource.volume = footstepVolume;
        }

        void Start()
        {
            currentStamina = sprintDuration;
        }

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                CameraFollow cameraScript = FindObjectOfType<CameraFollow>();
                if (cameraScript != null)
                {
                    cameraScript.SetTarget(this.transform);
                }
            }
        }

        void Update()
        {
            if (!IsOwner) return;

            if (isMovementLocked)
            {
                isSprinting = false;

                if (animator != null)
                {
                    animator.SetFloat("X", 0f);
                    animator.SetFloat("Z", 0f);
                    animator.SetBool("IsRun", false);
                }

                UpdateAnimationServerRpc(0f, 0f, false);
                ApplyGravityOnly();
                return;
            }

            HandleStamina();
            MoveCharacter();
            UpdateAnimator();
        }

        private void ApplyGravityOnly()
        {
            if (controller == null) return;

            if (!controller.isGrounded)
                velocity.y -= gravity * Time.deltaTime;
            else
                velocity.y = -2f;

            controller.Move(velocity * Time.deltaTime);
        }

        // ═══════════════════════════════════════════════════════
        // 🔊 ANIMATION EVENT - Animasyon tarafından çağrılır
        // ═══════════════════════════════════════════════════════
        public void OnFootstep()
        {
            if (!IsOwner || isMovementLocked) return;

            PlayFootstepSound();
        }

        void PlayFootstepSound()
        {
            if (audioSource == null) return;

            AudioClip[] soundArray;

            // Koşma veya yürüme sesini seç
            if (isSprinting && !isInCooldown)
            {
                soundArray = runSounds != null && runSounds.Length > 0 ? runSounds : walkSounds;
            }
            else
            {
                soundArray = walkSounds;
            }

            if (soundArray == null || soundArray.Length == 0)
            {
                Debug.LogWarning("Ses dizisi boş! Walk Sounds veya Run Sounds ekleyin.");
                return;
            }

            // Rastgele ses seç ve çal
            AudioClip clip = soundArray[Random.Range(0, soundArray.Length)];
            if (clip != null)
            {
                audioSource.PlayOneShot(clip, footstepVolume);

                // Network üzerinden de çal
                PlayFootstepServerRpc();
            }
        }

        [ServerRpc]
        void PlayFootstepServerRpc()
        {
            PlayFootstepClientRpc();
        }

        [ClientRpc]
        void PlayFootstepClientRpc()
        {
            if (!IsOwner && audioSource != null)
            {
                AudioClip[] soundArray = (isSprinting && !isInCooldown) ?
                    (runSounds != null && runSounds.Length > 0 ? runSounds : walkSounds) : walkSounds;

                if (soundArray != null && soundArray.Length > 0)
                {
                    AudioClip clip = soundArray[Random.Range(0, soundArray.Length)];
                    if (clip != null)
                    {
                        audioSource.PlayOneShot(clip, footstepVolume);
                    }
                }
            }
        }

        public void SetCarrying(bool carry)
        {
            isCarrying = carry;
            if (IsOwner)
            {
                SetCarryingServerRpc(carry);
            }
        }

        [ServerRpc]
        void SetCarryingServerRpc(bool carry)
        {
            SetCarryingClientRpc(carry);
        }

        [ClientRpc]
        void SetCarryingClientRpc(bool carry)
        {
            if (animator != null)
            {
                animator.SetBool("IsPickup", carry);
            }
        }

        void UpdateAnimator()
        {
            if (!IsOwner) return;

            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            UpdateAnimationServerRpc(h, v, isSprinting);
        }

        [ServerRpc]
        void UpdateAnimationServerRpc(float x, float z, bool isRunning)
        {
            UpdateAnimationClientRpc(x, z, isRunning);
        }

        [ClientRpc]
        void UpdateAnimationClientRpc(float x, float z, bool isRunning)
        {
            if (animator != null)
            {
                animator.SetFloat("X", x, 0.05f, Time.deltaTime);
                animator.SetFloat("Z", z, 0.05f, Time.deltaTime);
                animator.SetBool("IsRun", isRunning);
            }
        }

        public void LockAllInteractions(bool locked)
        {
            interactionsLocked = locked;
        }

        void HandleInteractionInput()
        {
            if (interactionsLocked)
                return;
        }

        void HandleStamina()
        {
            if (isMovementLocked)
            {
                isSprinting = false;
                return;
            }

            if (isInCooldown)
            {
                isSprinting = false;
                cooldownTimer -= Time.deltaTime;
                if (cooldownTimer <= 0f)
                {
                    isInCooldown = false;
                    currentStamina = sprintDuration;
                }
            }
            else
            {
                float h = Input.GetAxisRaw("Horizontal");
                float v = Input.GetAxisRaw("Vertical");
                bool isMoving = Mathf.Abs(h) > 0.1f || Mathf.Abs(v) > 0.1f;

                if (Input.GetKey(KeyCode.LeftShift) && currentStamina > 0f && isMoving)
                {
                    isSprinting = true;
                    currentStamina -= Time.deltaTime;
                    if (currentStamina <= 0f)
                    {
                        currentStamina = 0f;
                        isInCooldown = true;
                        cooldownTimer = sprintCooldown;
                    }
                }
                else
                {
                    isSprinting = false;
                    currentStamina += Time.deltaTime * staminaRegenRate;
                    if (currentStamina > sprintDuration)
                        currentStamina = sprintDuration;
                }
            }
        }

        void MoveCharacter()
        {
            if (controller == null) return;

            if (isMovementLocked)
            {
                ApplyGravityOnly();
                return;
            }

            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            Vector3 dir = new Vector3(h, 0, v).normalized;

            float targetSpeed;
            if (isSprinting && !isInCooldown)
            {
                targetSpeed = sprintSpeed;
            }
            else if (isInCooldown)
            {
                targetSpeed = exhaustedSpeed;
            }
            else
            {
                targetSpeed = moveSpeed;
            }

            if (dir.magnitude >= 0.1f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

                controller.Move(dir * targetSpeed * Time.deltaTime);
            }

            if (!controller.isGrounded)
                velocity.y -= gravity * Time.deltaTime;
            else
                velocity.y = -2f;

            controller.Move(velocity * Time.deltaTime);
        }

        public void LockMovement(bool locked)
        {
            isMovementLocked = locked;

            if (locked && IsOwner)
            {
                isSprinting = false;

                if (animator != null)
                {
                    animator.SetFloat("X", 0f);
                    animator.SetFloat("Z", 0f);
                    animator.SetBool("IsRun", false);
                }

                UpdateAnimationServerRpc(0f, 0f, false);
            }
        }
    }
}