using Unity.Netcode;
using UnityEngine;
using NewCss.Audio;

namespace NewCss
{
    /// <summary>
    /// Oyuncu hareket sistemi - network destekli karakter hareketi, sprint, stamina ve ses yönetimini sağlar. 
    /// </summary>
    public class PlayerMovement : NetworkBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[PlayerMovement]";
        private const float DEFAULT_GRAVITY = 9.81f;
        private const float GROUNDED_VELOCITY = -2f;
        private const float ANIMATION_DAMP_TIME = 0.05f;
        private const float MOVEMENT_THRESHOLD = 0.1f;
        private const float TABLE_DETECTION_RADIUS = 5f;

        // Animator Parameters
        private const string ANIM_PARAM_X = "X";
        private const string ANIM_PARAM_Z = "Z";
        private const string ANIM_PARAM_IS_RUN = "IsRun";
        private const string ANIM_PARAM_IS_PICKUP = "IsPickup";

        #endregion

        #region Serialized Fields - Movement

        [Header("=== MOVEMENT SETTINGS ===")]
        [SerializeField, Tooltip("Normal hareket hızı")]
        public float moveSpeed = 5f;

        [SerializeField, Tooltip("Sprint hızı")]
        public float sprintSpeed = 7f;

        [SerializeField, Tooltip("Yorgunluk hızı")]
        public float exhaustedSpeed = 3f;

        [SerializeField, Tooltip("Dönme hızı")]
        private float rotationSpeed = 10f;

        [SerializeField, Tooltip("Yerçekimi")]
        private float gravity = DEFAULT_GRAVITY;

        #endregion

        #region Serialized Fields - Sprint

        [Header("=== SPRINT SETTINGS ===")]
        [SerializeField, Tooltip("Sprint süresi")]
        public float sprintDuration = 3f;

        [SerializeField, Tooltip("Sprint bekleme süresi")]
        public float sprintCooldown = 3f;

        [SerializeField, Tooltip("Stamina yenilenme hızı")]
        public float staminaRegenRate = 1f;

        #endregion

        #region Serialized Fields - Audio

        [Header("=== AUDIO SETTINGS ===")]
        [SerializeField, Tooltip("Yürüme sesleri")]
        public AudioClip[] walkSounds;

        [SerializeField, Tooltip("Koşma sesleri")]
        public AudioClip[] runSounds;

        [SerializeField, Range(0f, 1f), Tooltip("Adım sesi seviyesi")]
        public float footstepVolume = 0.2f;

        #endregion

        #region Private Fields - Components

        private CharacterController _controller;
        private Animator _animator;
        private AudioSource _audioSource;
        private UnifiedSettingsManager _settingsManager;

        #endregion

        #region Private Fields - State

        private float _currentStamina;
        private float _cooldownTimer;
        private bool _isSprinting;
        private bool _isInCooldown;
        private bool _isMovementLocked;
        private bool _isCarrying;
        private bool _interactionsLocked;
        private Vector3 _velocity;

        #endregion

        #region Network Variables

        private readonly NetworkVariable<float> _networkX = new();
        private readonly NetworkVariable<float> _networkZ = new();
        private readonly NetworkVariable<bool> _networkIsRunning = new();
        private readonly NetworkVariable<bool> _networkIsCarrying = new();

        #endregion

        #region Public Properties - Stamina

        /// <summary>
        /// Mevcut stamina
        /// </summary>
        public float CurrentStamina => _currentStamina;

        /// <summary>
        /// Maksimum stamina
        /// </summary>
        public float MaxStamina => sprintDuration;

        /// <summary>
        /// Stamina yüzdesi (0-1)
        /// </summary>
        public float StaminaPercent => _currentStamina / sprintDuration;

        #endregion

        #region Public Properties - Cooldown

        /// <summary>
        /// Mevcut bekleme süresi
        /// </summary>
        public float CooldownTime => _cooldownTimer;

        /// <summary>
        /// Maksimum bekleme süresi
        /// </summary>
        public float MaxCooldown => sprintCooldown;

        /// <summary>
        /// Bekleme süresinde mi? 
        /// </summary>
        public bool IsInCooldown => _isInCooldown;

        /// <summary>
        /// Cooldown yüzdesi (0-1)
        /// </summary>
        public float CooldownPercent => _isInCooldown ? _cooldownTimer / sprintCooldown : 0f;

        #endregion

        #region Public Properties - State

        /// <summary>
        /// Sprint yapıyor mu?
        /// </summary>
        public bool IsSprinting => _isSprinting;

        /// <summary>
        /// Hareket kilitli mi?
        /// </summary>
        public bool IsMovementLocked => _isMovementLocked;

        /// <summary>
        /// Taşıyor mu?
        /// </summary>
        public bool IsCarrying => _isCarrying;

        /// <summary>
        /// Etkileşimler kilitli mi?
        /// </summary>
        public bool InteractionsLocked => _interactionsLocked;

        /// <summary>
        /// Hareket ediyor mu?
        /// </summary>
        public bool IsMoving => GetMovementInput().magnitude >= MOVEMENT_THRESHOLD;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeComponents();
            InitializeAudioSource();
            FindSettingsManager();
            UpdateAudioVolume();
        }

        private void Start()
        {
            _currentStamina = sprintDuration;
        }

        private void Update()
        {
            // GEÇİCİ TEŞHİS — kök neden bulunur bulunmaz SİL (bkz. MovementDiagnostics yorumu).
            // IsOwner kontrolünden ÖNCE: "hiç log yok" da bir bilgi (Update hiç koşmuyor demek).
            // GUARD ZORUNLU: MovementDiagnostics/LogMovementDiagnostics tanımları da aynı guard
            // altında — guard'sız bırakılırsa Release Player Build'de derleme kırılır.
            // 2026-08-31: guard UNITY_EDITOR'dan DEVELOPMENT_BUILD'i de kapsayacak şekilde
            // genişletildi. Eskisi ile çift makine testinden VERİ ÇIKMIYORDU: ikinci makinedeki
            // client standalone koşuyorsa UNITY_EDITOR tanımsızdır, log hiç derlenmezdi.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (MovementDiagnostics) LogMovementDiagnostics();
#endif

            if (!IsOwner) return;

            if (_isMovementLocked)
            {
                HandleLockedState();
                return;
            }

            HandleStamina();
            MoveCharacter();
            UpdateAnimator();
        }

        #endregion

        #region Network Lifecycle

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsOwner)
            {
                SetupCamera();

                // perk-revival (bkz. plans/perk-revival.md §2): late-join oyuncusu bu ana kadar
                // satın alınmış agile_crew/energetic_crew perklerini kaçırmasın. F10 fix'teki
                // BuffManager deseniyle aynı sorun sınıfı, ama kapsam SADECE owned player (her peer
                // kendi owned player'ını EventEffectManager.GetOwnedPlayer() ile bulduğu gibi) —
                // bu yüzden IsOwner bloğunun İÇİNDE, BuffManager çağrısının aksine tüm player
                // instance'ları için değil.
                // stamina-backbone-live fix: "Dinç Ekip" omurga yükseltmesi de aynı yoldan (bkz.
                // UpgradePanel.ApplyLivePerksToPlayer) canlı instance'a mutlak yazılıyor — BuffManager
                // += yapmadan ÖNCE burada olmalı (aksi halde backbone'un mutlak ataması buff'ları silerdi).
                UpgradePanel.Instance?.ApplyLivePerksToPlayer(this);
            }

            // F10 fix: late-join oyuncusu veya BuffManager'ın buff listesi zaten dolmuşken sonradan spawn
            // olan oyuncu, o ana kadar verilmiş MaxStamina/MoveSpeed/WalkSpeed/StaminaRegenRate buff'larını
            // hiç almıyordu (BuffManager push-once FindObjectsOfType modeli, bu obje o an sahnede yoktu).
            // Çift-uygulama guard BuffManager.ApplyActiveBuffsTo içinde (bkz. Assets/Scripts/Quest/Buff/BuffManager.cs).
            Quest.BuffManager.Instance?.ApplyActiveBuffsTo(this);
        }

        #endregion

        #region Initialization

        private void InitializeComponents()
        {
            _controller = GetComponent<CharacterController>();
            _animator = GetComponent<Animator>();

            if (_controller == null)
            {
                Debug.LogError($"{LOG_PREFIX} CharacterController not found!");
            }
        }

        private void InitializeAudioSource()
        {
            _audioSource = GetComponent<AudioSource>();

            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }

            AudioRouting.Route(_audioSource, AudioCategory.SFX);
            ConfigureAudioSource();
        }

        private void ConfigureAudioSource()
        {
            _audioSource.spatialBlend = 0.5f;
            _audioSource.rolloffMode = AudioRolloffMode.Linear;
            _audioSource.minDistance = 1f;
            _audioSource.maxDistance = 15f;
        }

        private void FindSettingsManager()
        {
            _settingsManager = FindObjectOfType<UnifiedSettingsManager>();
        }

        private void SetupCamera()
        {
            CameraFollow cameraScript = FindObjectOfType<CameraFollow>();

            if (cameraScript != null)
            {
                cameraScript.SetTarget(transform);
            }
        }

        #endregion

        #region Audio Management

        private void UpdateAudioVolume()
        {
            if (_audioSource == null) return;

            float finalVolume = CalculateFinalVolume();
            _audioSource.volume = finalVolume;
        }

        /// <summary>
        /// DÜZELTME (QA, çifte ölçekleme): SFX/Master kısılması artık AudioRouting ile SFX
        /// mixer grubuna yönlendirilen _audioSource üzerinden CargorMixer'da uygulanıyor
        /// (bkz. UnifiedSettingsManager.ApplyMixerCategoryVolume). Burada AYRICA
        /// GetSFXVolume()*GetMasterVolume() çarpılırsa ses iki kez kısılır — kaynak volume'u
        /// SADECE tasarım değerini (footstepVolume) taşır.
        /// </summary>
        private float CalculateFinalVolume()
        {
            return footstepVolume;
        }

        /// <summary>
        /// Animation Event - Adım sesi çal
        /// </summary>
        public void OnFootstep()
        {
            if (!IsOwner || _isMovementLocked) return;

            PlayFootstepSound();
        }

        private void PlayFootstepSound()
        {
            if (_audioSource == null) return;

            UpdateAudioVolume();

            AudioClip[] soundArray = GetFootstepSoundArray();

            if (soundArray == null || soundArray.Length == 0)
            {
                Debug.LogWarning($"{LOG_PREFIX} Sound array is empty!  Add Walk Sounds or Run Sounds.");
                return;
            }

            AudioClip clip = GetRandomClip(soundArray);

            if (clip != null)
            {
                _audioSource.PlayOneShot(clip);
                PlayFootstepServerRpc();
            }
        }

        private AudioClip[] GetFootstepSoundArray()
        {
            if (_isSprinting && !_isInCooldown)
            {
                return runSounds != null && runSounds.Length > 0 ? runSounds : walkSounds;
            }

            return walkSounds;
        }

        private AudioClip GetRandomClip(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0) return null;

            return clips[Random.Range(0, clips.Length)];
        }

        [ServerRpc]
        private void PlayFootstepServerRpc()
        {
            PlayFootstepClientRpc();
        }

        [ClientRpc]
        private void PlayFootstepClientRpc()
        {
            if (IsOwner) return;
            if (_audioSource == null) return;

            UpdateAudioVolume();

            AudioClip[] soundArray = GetFootstepSoundArray();
            AudioClip clip = GetRandomClip(soundArray);

            if (clip != null)
            {
                _audioSource.PlayOneShot(clip);
            }
        }

        #endregion

        #region Movement

        private void MoveCharacter()
        {
            if (_controller == null) return;

            if (_isMovementLocked)
            {
                ApplyGravityOnly();
                return;
            }

            Vector2 input = GetMovementInput();
            Vector3 direction = new Vector3(input.x, 0, input.y).normalized;

            float targetSpeed = GetCurrentSpeed();

            if (direction.magnitude >= MOVEMENT_THRESHOLD)
            {
                RotateTowardsDirection(direction);
                // Move()'un dönüşü normalde atılıyor; teşhis için saklanıyor. Her yönde "Sides" dönüp
                // pozisyon değişmiyorsa oyuncu geometriye/başka bir kapsüle SIKIŞMIŞ demektir —
                // "girdi ölü" ile "fiziksel engel" ayrımını yapan tek ölçüm bu. Move() çağrısının
                // kendisi HER ZAMAN çalışmalı (gerçek hareket budur) — sadece _lastMoveFlags'e
                // saklama kısmı teşhis-only (_lastMoveFlags tanımı aynı guard altında).
                var moveFlags = _controller.Move(direction * targetSpeed * Time.deltaTime);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                _lastMoveFlags = moveFlags;
#endif
            }

            ApplyGravity();
        }

        private Vector2 GetMovementInput()
        {
            float h = 0f;
            if (InputBindingManager.GetAction(InputBindingManager.GameAction.MoveRight)) h += 1f;
            if (InputBindingManager.GetAction(InputBindingManager.GameAction.MoveLeft)) h -= 1f;

            float v = 0f;
            if (InputBindingManager.GetAction(InputBindingManager.GameAction.MoveUp)) v += 1f;
            if (InputBindingManager.GetAction(InputBindingManager.GameAction.MoveDown)) v -= 1f;

            return new Vector2(h, v);
        }

        private float GetCurrentSpeed()
        {
            if (_isSprinting && !_isInCooldown)
            {
                return sprintSpeed;
            }

            if (_isInCooldown)
            {
                return exhaustedSpeed;
            }

            return moveSpeed;
        }

        private void RotateTowardsDirection(Vector3 direction)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        private void ApplyGravity()
        {
            if (!_controller.isGrounded)
            {
                _velocity.y -= gravity * Time.deltaTime;
            }
            else
            {
                _velocity.y = GROUNDED_VELOCITY;
            }

            _controller.Move(_velocity * Time.deltaTime);
        }

        private void ApplyGravityOnly()
        {
            if (_controller == null) return;

            if (!_controller.isGrounded)
            {
                _velocity.y -= gravity * Time.deltaTime;
            }
            else
            {
                _velocity.y = GROUNDED_VELOCITY;
            }

            _controller.Move(_velocity * Time.deltaTime);
        }

        private void HandleLockedState()
        {
            _isSprinting = false;

            if (_animator != null)
            {
                _animator.SetFloat(ANIM_PARAM_X, 0f);
                _animator.SetFloat(ANIM_PARAM_Z, 0f);
                _animator.SetBool(ANIM_PARAM_IS_RUN, false);
            }

            UpdateAnimationServerRpc(0f, 0f, false);
            ApplyGravityOnly();
        }

        #endregion




        #region Stamina Management

        private void HandleStamina()
        {
            if (_isMovementLocked)
            {
                _isSprinting = false;
                return;
            }

            if (_isInCooldown)
            {
                HandleCooldownState();
            }
            else
            {
                HandleNormalStaminaState();
            }
        }

        private void HandleCooldownState()
        {
            _isSprinting = false;
            _cooldownTimer -= Time.deltaTime;

            if (_cooldownTimer <= 0f)
            {
                ExitCooldown();
            }
        }

        private void HandleNormalStaminaState()
        {
            bool isMoving = IsMoving;
            bool wantsSprint = InputBindingManager.GetAction(InputBindingManager.GameAction.Sprint);

            if (wantsSprint && _currentStamina > 0f && isMoving)
            {
                ConsumeStamina();
            }
            else
            {
                RegenerateStamina();
            }
        }

        private void ConsumeStamina()
        {
            _isSprinting = true;
            _currentStamina -= Time.deltaTime;

            if (_currentStamina <= 0f)
            {
                EnterCooldown();
            }
        }

        private void RegenerateStamina()
        {
            _isSprinting = false;
            _currentStamina += Time.deltaTime * staminaRegenRate;

            if (_currentStamina > sprintDuration)
            {
                _currentStamina = sprintDuration;
            }
        }

        private void EnterCooldown()
        {
            _currentStamina = 0f;
            _isInCooldown = true;
            _cooldownTimer = sprintCooldown;
        }

        private void ExitCooldown()
        {
            _isInCooldown = false;
            _currentStamina = sprintDuration;
        }

        #endregion

        #region Animation

        private void UpdateAnimator()
        {
            if (!IsOwner) return;

            Vector2 input = GetMovementInput();
            UpdateAnimationServerRpc(input.x, input.y, _isSprinting);
        }

        [ServerRpc]
        private void UpdateAnimationServerRpc(float x, float z, bool isRunning)
        {
            UpdateAnimationClientRpc(x, z, isRunning);
        }

        [ClientRpc]
        private void UpdateAnimationClientRpc(float x, float z, bool isRunning)
        {
            if (_animator == null) return;

            _animator.SetFloat(ANIM_PARAM_X, x, ANIMATION_DAMP_TIME, Time.deltaTime);
            _animator.SetFloat(ANIM_PARAM_Z, z, ANIMATION_DAMP_TIME, Time.deltaTime);
            _animator.SetBool(ANIM_PARAM_IS_RUN, isRunning);
        }

        #endregion

        #region Carrying State

        /// <summary>
        /// Taşıma durumunu ayarlar
        /// </summary>
        public void SetCarrying(bool carry)
        {
            _isCarrying = carry;

            if (IsOwner)
            {
                SetCarryingServerRpc(carry);
            }
        }

        [ServerRpc]
        private void SetCarryingServerRpc(bool carry)
        {
            SetCarryingClientRpc(carry);
        }

        [ClientRpc]
        private void SetCarryingClientRpc(bool carry)
        {
            if (_animator != null)
            {
                _animator.SetBool(ANIM_PARAM_IS_PICKUP, carry);
            }
        }

        #endregion

        #region Public API - Locking

        /// <summary>
        /// Hareketi kilitler/açar
        /// </summary>
        public void LockMovement(bool locked)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // GEÇİCİ TEŞHİS (2026-08-31) — kök neden bulununca SİL.
            // "locked=True" bilgisi tek başına yetmiyor: break-room kilidi, müşteri etkileşimi
            // kilidi ve panel kilidi aynı bayrağı yazıyor. Ayıran tek şey ÇAĞIRAN.
            if (MovementDiagnostics && IsLocalPlayer)
            {
                Debug.Log($"{LOG_PREFIX} [TESHIS] LockMovement({locked}) cagiran:\n" +
                          StackTraceUtility.ExtractStackTrace());
            }
#endif

            _isMovementLocked = locked;

            if (locked && IsOwner)
            {
                ResetMovementState();
            }
        }

        /// <summary>
        /// Tüm etkileşimleri kilitler/açar
        /// </summary>
        public void LockAllInteractions(bool locked)
        {
            _interactionsLocked = locked;
        }

        private void ResetMovementState()
        {
            _isSprinting = false;

            if (_animator != null)
            {
                _animator.SetFloat(ANIM_PARAM_X, 0f);
                _animator.SetFloat(ANIM_PARAM_Z, 0f);
                _animator.SetBool(ANIM_PARAM_IS_RUN, false);
            }

            UpdateAnimationServerRpc(0f, 0f, false);
        }

        #endregion

        #region Public API - Stamina

        /// <summary>
        /// Staminayı belirli bir miktara ayarlar
        /// </summary>
        public void SetStamina(float amount)
        {
            _currentStamina = Mathf.Clamp(amount, 0f, sprintDuration);
        }

        /// <summary>
        /// Staminayı tamamen doldurur
        /// </summary>
        public void RefillStamina()
        {
            _currentStamina = sprintDuration;
            _isInCooldown = false;
            _cooldownTimer = 0f;
        }

        /// <summary>
        /// Staminayı tamamen boşaltır
        /// </summary>
        public void DrainStamina()
        {
            EnterCooldown();
        }

        #endregion

        #region Editor Debug

// DEVELOPMENT_BUILD de kapsanıyor: WASD teşhisi (aşağıdaki GEÇİCİ TEŞHİS bölgesi) çift makine
// testinde Development Build alınan client'ta da log basabilsin diye. Release build'e girmez.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [ContextMenu("Refill Stamina")]
        private void DebugRefillStamina()
        {
            RefillStamina();
        }

        [ContextMenu("Drain Stamina")]
        private void DebugDrainStamina()
        {
            DrainStamina();
        }

        [ContextMenu("Lock Movement")]
        private void DebugLockMovement()
        {
            LockMovement(true);
        }

        [ContextMenu("Unlock Movement")]
        private void DebugUnlockMovement()
        {
            LockMovement(false);
        }

        [ContextMenu("Toggle Carrying")]
        private void DebugToggleCarrying()
        {
            SetCarrying(!_isCarrying);
        }

        #region GEÇİCİ TEŞHİS (2026-08-09) — kök neden bulununca BU BÖLÜMÜ SİL

        /// <summary>
        /// Çift makine testi: client'ta WASD hiç çalışmadı, host'ta sorun yoktu. Statik inceleme
        /// yedi hipotezi eledi (sahiplik, tuş atamaları, server-authority, timeScale, NextDayUI kilidi,
        /// aynı noktada spawn, Awake istisnası) — geriye çalışma anı ölçümü kaldı.
        /// Sahibin makinesinde saniyede BİR satır basar; Player.log'da hangi kapının kapalı olduğunu
        /// tek turda gösterir. Üretimde kalmamalı: bulgudan sonra bu bölge ve Update'teki çağrı silinir.
        /// </summary>
        private const bool MovementDiagnostics = true;

        private double _nextDiagTime;
        private Vector3 _diagLastPos;
        private CollisionFlags _lastMoveFlags;

        private void LogMovementDiagnostics()
        {
            if (!IsLocalPlayer) return; // sahiplik bozuksa bile kendi karakterimizi yakalar
            if (Time.unscaledTimeAsDouble < _nextDiagTime) return;
            _nextDiagTime = Time.unscaledTimeAsDouble + 1.0;

            Vector3 pos = transform.position;
            Vector2 input = GetMovementInput();
            string ctrl = _controller == null
                ? "NULL"
                : (_controller.enabled ? "enabled" : "DISABLED");

            Debug.Log($"{LOG_PREFIX} [TESHIS] owner={IsOwner} locked={_isMovementLocked} controller={ctrl} " +
                      $"input=({input.x:0.##},{input.y:0.##}) speed={GetCurrentSpeed():0.##} " +
                      $"cooldown={_isInCooldown} stamina={_currentStamina:0.##} timeScale={Time.timeScale:0.##} " +
                      $"pos={pos.x:0.##},{pos.y:0.##},{pos.z:0.##} delta1s={(pos - _diagLastPos).magnitude:0.###} " +
                      $"moveFlags={_lastMoveFlags} " +
                      // Ayıraçlar: interactionsLocked → müşteri etkileşimi kilidi mi (o ikisini
                      // birlikte set eder) · rawWASD+focus → girdi hiç okunmuyor mu · moveSpeed →
                      // hız çarpanı üst üste binip 0'a mı yaklaştı · spawn/owner → sahiplik bozuk mu.
                      $"interactionsLocked={_interactionsLocked} moveSpeed={moveSpeed:0.##} " +
                      $"rawWASD={Input.GetKey(KeyCode.W)}{Input.GetKey(KeyCode.A)}{Input.GetKey(KeyCode.S)}{Input.GetKey(KeyCode.D)} " +
                      $"focus={Application.isFocused} spawned={IsSpawned} ownerId={OwnerClientId} " +
                      $"localId={(NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : ulong.MaxValue)} " +
                      $"isLocalPlayer={IsLocalPlayer}");

            _diagLastPos = pos;
        }

        #endregion

        [ContextMenu("Debug: Print State")]
        private void DebugPrintState()
        {
            Debug.Log($"{LOG_PREFIX} === PLAYER MOVEMENT STATE ===");
            Debug.Log($"Is Owner: {IsOwner}");
            Debug.Log($"Is Movement Locked: {_isMovementLocked}");
            Debug.Log($"Is Interactions Locked: {_interactionsLocked}");
            Debug.Log($"Is Sprinting: {_isSprinting}");
            Debug.Log($"Is In Cooldown: {_isInCooldown}");
            Debug.Log($"Is Carrying: {_isCarrying}");
            Debug.Log($"Is Moving: {IsMoving}");
            Debug.Log($"Current Stamina: {_currentStamina:F2}/{sprintDuration}");
            Debug.Log($"Stamina Percent: {StaminaPercent:P0}");
            Debug.Log($"Cooldown Timer: {_cooldownTimer:F2}/{sprintCooldown}");
            Debug.Log($"Current Speed: {GetCurrentSpeed():F2}");
            Debug.Log($"Has Controller: {_controller != null}");
            Debug.Log($"Has Animator: {_animator != null}");
            Debug.Log($"Has Audio Source: {_audioSource != null}");
        }

        [ContextMenu("Debug: Print Speed Info")]
        private void DebugPrintSpeedInfo()
        {
            Debug.Log($"{LOG_PREFIX} === SPEED INFO ===");
            Debug.Log($"Move Speed: {moveSpeed}");
            Debug.Log($"Sprint Speed: {sprintSpeed}");
            Debug.Log($"Exhausted Speed: {exhaustedSpeed}");
            Debug.Log($"Current Speed: {GetCurrentSpeed()}");
            Debug.Log($"Rotation Speed: {rotationSpeed}");
        }

        private void OnDrawGizmosSelected()
        {
            // Table detection radius
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, TABLE_DETECTION_RADIUS);

            // Movement direction
            if (Application.isPlaying && IsOwner)
            {
                Vector2 input = GetMovementInput();
                if (input.magnitude > MOVEMENT_THRESHOLD)
                {
                    Gizmos.color = _isSprinting ? Color.red : Color.green;
                    Vector3 dir = new Vector3(input.x, 0, input.y).normalized;
                    Gizmos.DrawRay(transform.position + Vector3.up, dir * 2f);
                }
            }
        }
#endif

        #endregion
    }
}