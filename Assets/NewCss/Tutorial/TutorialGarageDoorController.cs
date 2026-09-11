using System;
using UnityEngine;

namespace NewCss
{
    /// <summary>
    /// Tutorial için özelleştirilmiş garaj kapısı kontrolcüsü. 
    /// TutorialManager ile entegre çalışır, belirli step'lerde açılıp kapanır.
    /// </summary>
    public class TutorialGarageDoorController : MonoBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[TutorialGarageDoor]";

        #endregion

        #region Enums

        public enum DoorState
        {
            Closed,
            Open,
            Opening,
            Closing
        }

        #endregion

        #region Serialized Fields - Animation

        [Header("=== ANIMATION SETTINGS ===")]
        [SerializeField, Tooltip("Kapı animatörü")]
        private Animator doorAnimator;

        [SerializeField, Tooltip("Açılma animasyon klibi")]
        private AnimationClip openAnimation;

        [SerializeField, Tooltip("Kapanma animasyon klibi")]
        private AnimationClip closeAnimation;

        [SerializeField, Tooltip("Açma trigger adı")]
        private string openTriggerName = "DoOpen";

        [SerializeField, Tooltip("Kapama trigger adı")]
        private string closeTriggerName = "DoClose";

        #endregion

        #region Serialized Fields - Audio

        [Header("=== AUDIO SETTINGS ===")]
        [SerializeField, Tooltip("Motor ses dosyası")]
        private AudioClip motorSound;

        [SerializeField, Range(0f, 1f), Tooltip("Spatial blend (0=2D, 1=3D)")]
        private float spatialBlend = 0.7f;

        [SerializeField, Range(0f, 100f), Tooltip("Maksimum duyulma mesafesi")]
        private float maxHearingDistance = 20f;

        [SerializeField, Range(0f, 1f), Tooltip("Motor ses seviyesi")]
        private float motorVolume = 0.8f;

        #endregion

        #region Serialized Fields - Tutorial Integration

        [Header("=== TUTORIAL INTEGRATION ===")]
        [SerializeField, Tooltip("Kapının açılacağı step index (PlaceOnShelf step'i)")]
        private int openOnStepIndex = 2;

        [SerializeField, Tooltip("Kapının kapanacağı step index (Truck gittikten sonra)")]
        private int closeOnStepIndex = 4;

        [SerializeField, Tooltip("Truck spawner referansı")]
        private TutorialTruckSpawner truckSpawner;

        #endregion

        #region Serialized Fields - Debug

        [Header("=== DEBUG ===")]
        [SerializeField, Tooltip("Debug loglarını göster")]
        private bool showDebugLogs = true;

        #endregion

        #region Private Fields

        private AudioSource _motorAudioSource;
        private DoorState _currentState = DoorState.Closed;
        private float _animationTimer;
        private float _currentAnimationDuration;
        private bool _isSubscribedToTutorial;

        #endregion

        #region Events

        public event Action OnDoorOpenComplete;
        public event Action OnDoorCloseComplete;

        #endregion

        #region Public Properties

        public DoorState CurrentState => _currentState;
        public bool IsOpen => _currentState == DoorState.Open;
        public bool IsClosed => _currentState == DoorState.Closed;
        public bool IsAnimating => _currentState == DoorState.Opening || _currentState == DoorState.Closing;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            InitializeAudioSource();
            InitializeDoorState();
            SubscribeToTutorialEvents();
        }

        private void Update()
        {
            UpdateAnimationTimer();
        }

        private void OnDestroy()
        {
            UnsubscribeFromTutorialEvents();
            StopMotorSound();
        }

        #endregion

        #region Initialization

        private void InitializeAudioSource()
        {
            _motorAudioSource = GetComponent<AudioSource>();
            if (_motorAudioSource == null)
            {
                _motorAudioSource = gameObject.AddComponent<AudioSource>();
            }

            _motorAudioSource.playOnAwake = false;
            _motorAudioSource.loop = true;
            _motorAudioSource.spatialBlend = spatialBlend;
            _motorAudioSource.volume = motorVolume;
            _motorAudioSource.minDistance = 1f;
            _motorAudioSource.maxDistance = maxHearingDistance;
            _motorAudioSource.rolloffMode = AudioRolloffMode.Linear;
            _motorAudioSource.clip = motorSound;
        }

        private void InitializeDoorState()
        {
            _currentState = DoorState.Closed;
            _animationTimer = 0f;

            if (doorAnimator != null)
            {
                doorAnimator.Rebind();
                doorAnimator.Update(0f);
            }

            LogDebug("Door initialized - CLOSED");
        }

        #endregion

        #region Tutorial Event Subscriptions

        private void SubscribeToTutorialEvents()
        {
            if (_isSubscribedToTutorial) return;

            // TutorialManager henüz hazır olmayabilir, bir frame bekle
            StartCoroutine(SubscribeWhenReady());
        }

        private System.Collections.IEnumerator SubscribeWhenReady()
        {
            // TutorialManager'ın hazır olmasını bekle
            while (TutorialManager.Instance == null)
            {
                yield return null;
            }

            TutorialManager.Instance.OnStepCompleted += HandleTutorialStepCompleted;
            _isSubscribedToTutorial = true;

            LogDebug("Subscribed to TutorialManager events");
        }

        private void UnsubscribeFromTutorialEvents()
        {
            if (!_isSubscribedToTutorial) return;
            if (TutorialManager.Instance == null) return;

            TutorialManager.Instance.OnStepCompleted -= HandleTutorialStepCompleted;
            _isSubscribedToTutorial = false;
        }

        #endregion

        #region Tutorial Event Handlers

        private void HandleTutorialStepCompleted(int stepIndex, TutorialStep step)
        {
            LogDebug($"Tutorial step {stepIndex} completed: {step.stepName}");

            // PlaceOnShelf step'i tamamlandığında kapıyı aç
            if (stepIndex == openOnStepIndex && _currentState == DoorState.Closed)
            {
                LogDebug($"PlaceOnShelf step completed - Opening garage door!");
                OpenDoor();
            }
            // Truck gittikten sonra kapıyı kapat
            else if (stepIndex == closeOnStepIndex && _currentState == DoorState.Open)
            {
                LogDebug($"Truck delivery step completed - Closing garage door!");
                CloseDoor();
            }
        }

        #endregion

        #region Animation Timer

        private void UpdateAnimationTimer()
        {
            if (_currentState == DoorState.Opening)
            {
                _animationTimer += Time.deltaTime;

                if (_animationTimer >= _currentAnimationDuration)
                {
                    CompleteOpenAnimation();
                }
            }
            else if (_currentState == DoorState.Closing)
            {
                _animationTimer += Time.deltaTime;

                if (_animationTimer >= _currentAnimationDuration)
                {
                    CompleteCloseAnimation();
                }
            }
        }

        private void CompleteOpenAnimation()
        {
            _currentState = DoorState.Open;
            _animationTimer = 0f;
            StopMotorSound();

            LogDebug("Door OPEN animation complete");

            OnDoorOpenComplete?.Invoke();

            // Kapı açıldığında truck'ı spawn et
            SpawnTruck();
        }

        private void CompleteCloseAnimation()
        {
            _currentState = DoorState.Closed;
            _animationTimer = 0f;
            StopMotorSound();

            LogDebug("Door CLOSE animation complete");

            OnDoorCloseComplete?.Invoke();
        }

        #endregion

        #region Door Control

        public void OpenDoor()
        {
            if (_currentState != DoorState.Closed)
            {
                LogDebug($"Cannot open door - current state: {_currentState}");
                return;
            }

            LogDebug("Opening garage door.. .");

            _currentState = DoorState.Opening;
            _animationTimer = 0f;

            // Animasyon süresini al
            _currentAnimationDuration = openAnimation != null ? openAnimation.length : 2f;

            // Animator trigger
            if (doorAnimator != null)
            {
                doorAnimator.SetTrigger(openTriggerName);
            }

            PlayMotorSound();
        }

        public void CloseDoor()
        {
            if (_currentState != DoorState.Open)
            {
                LogDebug($"Cannot close door - current state: {_currentState}");
                return;
            }

            LogDebug("Closing garage door...");

            _currentState = DoorState.Closing;
            _animationTimer = 0f;

            // Animasyon süresini al
            _currentAnimationDuration = closeAnimation != null ? closeAnimation.length : 2f;

            // Animator trigger
            if (doorAnimator != null)
            {
                doorAnimator.SetTrigger(closeTriggerName);
            }

            PlayMotorSound();
        }

        public void ForceOpenDoor()
        {
            if (_currentState == DoorState.Closed || _currentState == DoorState.Closing)
            {
                _currentState = DoorState.Closed; // Reset state
                OpenDoor();
            }
        }

        public void ForceCloseDoor()
        {
            if (_currentState == DoorState.Open || _currentState == DoorState.Opening)
            {
                _currentState = DoorState.Open; // Reset state
                CloseDoor();
            }
        }

        #endregion

        #region Truck Spawning

        private void SpawnTruck()
        {
            if (truckSpawner == null)
            {
                LogDebug("⚠️ TruckSpawner reference is null!");
                return;
            }

            LogDebug("Spawning tutorial truck...");
            truckSpawner.SpawnTutorialTruck();
        }

        #endregion

        #region Audio

        private void PlayMotorSound()
        {
            if (_motorAudioSource != null && motorSound != null && !_motorAudioSource.isPlaying)
            {
                _motorAudioSource.Play();
            }
        }

        private void StopMotorSound()
        {
            if (_motorAudioSource != null && _motorAudioSource.isPlaying)
            {
                _motorAudioSource.Stop();
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Truck gidince kapıyı kapatmak için çağrılır
        /// </summary>
        public void OnTruckExited()
        {
            LogDebug("Truck exited - Closing garage door");

            if (_currentState == DoorState.Open)
            {
                CloseDoor();
            }
        }

        /// <summary>
        /// Step index'lerini dinamik olarak ayarla
        /// </summary>
        public void SetStepIndices(int openStep, int closeStep)
        {
            openOnStepIndex = openStep;
            closeOnStepIndex = closeStep;
        }

        #endregion

        #region Logging

        private void LogDebug(string message)
        {
            if (showDebugLogs)
            {
                Debug.Log($"{LOG_PREFIX} {message}");
            }
        }

        #endregion

        #region Editor Debug

#if UNITY_EDITOR
        [ContextMenu("Force Open Door")]
        private void DebugForceOpen()
        {
            ForceOpenDoor();
        }

        [ContextMenu("Force Close Door")]
        private void DebugForceClose()
        {
            ForceCloseDoor();
        }

        [ContextMenu("Debug: Print State")]
        private void DebugPrintState()
        {
            Debug.Log($"{LOG_PREFIX} === GARAGE DOOR STATE ===");
            Debug.Log($"Current State: {_currentState}");
            Debug.Log($"Animation Timer: {_animationTimer:F2}");
            Debug.Log($"Open Step Index: {openOnStepIndex}");
            Debug.Log($"Close Step Index: {closeOnStepIndex}");
            Debug.Log($"Has Truck Spawner: {truckSpawner != null}");
        }
#endif

        #endregion
    }
}