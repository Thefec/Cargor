using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace NewCss
{
    /// <summary>
    /// Tutorial için özelleştirilmiş kamyon sistemi. 
    /// Rafa item konulana kadar kapılar kapalı, konulduktan sonra açılır.
    /// TutorialManager ile entegre çalışır.
    /// </summary>
    public class TutorialTruck : NetworkBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[TutorialTruck]";
        private const string DOOR_OPEN_ANIM_BOOL = "DoorsOpen";

        #endregion

        #region Serialized Fields - Request Settings

        [Header("=== TUTORIAL TRUCK SETTINGS ===")]
        [SerializeField, Tooltip("İstenen kutu türü")]
        public BoxInfo.BoxType requestedBoxType = BoxInfo.BoxType.Red;

        [SerializeField, Tooltip("Gerekli kargo sayısı")]
        public int requiredCargo = 1;

        #endregion

        #region Serialized Fields - UI

        [Header("=== UI ===")]
        [SerializeField, Tooltip("Kamyon durum text'i")]
        public TextMeshProUGUI truckText;

        [SerializeField, Tooltip("Talimat text'i (opsiyonel - TutorialManager kullanılabilir)")]
        public TextMeshProUGUI instructionText;

        #endregion

        #region Serialized Fields - Collider

        [Header("=== COLLIDER SETTINGS ===")]
        [SerializeField, Tooltip("Trigger collider object")]
        public GameObject triggerColliderObject;

        #endregion

        #region Serialized Fields - Visual

        [Header("=== TRUCK PARTS ===")]
        [SerializeField] public GameObject truckBody;
        [SerializeField] public GameObject leftDoor;
        [SerializeField] public GameObject rightDoor;

        [Header("=== DOOR SETTINGS ===")]
        [SerializeField, Tooltip("Sol kapı açık rotasyonu")]
        private Vector3 leftDoorOpenRotation = new Vector3(0f, -110f, 0f);

        [SerializeField, Tooltip("Sağ kapı açık rotasyonu")]
        private Vector3 rightDoorOpenRotation = new Vector3(0f, 110f, 0f);

        [SerializeField, Tooltip("Kapı açılma hızı")]
        private float doorOpenSpeed = 2f;

        [SerializeField, Tooltip("Kapı animatörü (opsiyonel - animator varsa kullanılır)")]
        public Animator doorAnimator;

        #endregion

        #region Serialized Fields - Audio

        [Header("=== AUDIO SETTINGS ===")]
        [SerializeField] public AudioSource doorAudioSource;
        [SerializeField] public AudioClip doorOpenClip;
        [SerializeField] public AudioClip itemDeliveredClip;
        [SerializeField] public AudioClip wrongItemClip;

        #endregion

        #region Serialized Fields - Tutorial Integration

        [Header("=== TUTORIAL INTEGRATION ===")]
        [SerializeField, Tooltip("Rafa koyma step index'i (bu step tamamlandığında kapılar açılır)")]
        private int placeOnShelfStepIndex = 2;

        [SerializeField, Tooltip("Araca atma step index'i")]
        private int deliverToTruckStepIndex = 3;

        [SerializeField, Tooltip("Tutorial tamamlandığında otomatik kapan")]
        private bool autoCloseOnTutorialComplete = true;

        #endregion

        #region Network Variables

        private readonly NetworkVariable<int> _deliveredCount = new(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> _areDoorsOpen = new(false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> _isDeliveryComplete = new(false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> _isActive = new(true,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        #endregion

        #region Private Fields

        private Vector3 _leftDoorClosedRotation;
        private Vector3 _rightDoorClosedRotation;
        private Coroutine _doorAnimationCoroutine;
        private bool _isSubscribedToTutorial;

        #endregion

        #region Public Properties

        /// <summary>
        /// Teslim edilen kargo sayısı
        /// </summary>
        public int DeliveredCount => _deliveredCount.Value;

        /// <summary>
        /// Kapılar açık mı? 
        /// </summary>
        public bool AreDoorsOpen => _areDoorsOpen.Value;

        /// <summary>
        /// Teslimat tamamlandı mı?
        /// </summary>
        public bool IsDeliveryComplete => _isDeliveryComplete.Value;

        /// <summary>
        /// Truck aktif mi?
        /// </summary>
        public bool IsActive => _isActive.Value;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            CacheInitialDoorRotations();
        }

        private void OnDestroy()
        {
            UnsubscribeFromTutorialEvents();
        }

        #endregion

        #region Network Lifecycle

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            SubscribeToNetworkEvents();
            SetupTriggerCollider();
            SetTruckColors();
            UpdateUIText();

            // Tutorial event'lerine abone ol
            SubscribeToTutorialEvents();

            // Başlangıçta kapıları kapalı tut
            if (IsServer)
            {
                _areDoorsOpen.Value = false;
            }

            // İlk kapı durumunu ayarla
            SetDoorsInstant(false);

            LogDebug("TutorialTruck spawned - Doors CLOSED, waiting for PlaceOnShelf step");
        }

        public override void OnNetworkDespawn()
        {
            UnsubscribeFromNetworkEvents();
            UnsubscribeFromTutorialEvents();
            base.OnNetworkDespawn();
        }

        #endregion

        #region Initialization

        private void CacheInitialDoorRotations()
        {
            if (leftDoor != null)
            {
                _leftDoorClosedRotation = leftDoor.transform.localEulerAngles;
            }

            if (rightDoor != null)
            {
                _rightDoorClosedRotation = rightDoor.transform.localEulerAngles;
            }
        }

        private void SetupTriggerCollider()
        {
            GameObject colliderObj = triggerColliderObject != null ? triggerColliderObject : gameObject;
            Collider col = colliderObj.GetComponent<Collider>();

            if (col == null) return;

            col.isTrigger = true;

            TutorialTruckTrigger trigger = colliderObj.GetComponent<TutorialTruckTrigger>();
            if (trigger == null)
            {
                trigger = colliderObj.AddComponent<TutorialTruckTrigger>();
            }
            trigger.tutorialTruck = this;
        }

        #endregion

        #region Event Subscriptions

        private void SubscribeToNetworkEvents()
        {
            _deliveredCount.OnValueChanged += HandleDeliveredCountChanged;
            _areDoorsOpen.OnValueChanged += HandleDoorsOpenChanged;
            _isDeliveryComplete.OnValueChanged += HandleDeliveryCompleteChanged;
        }

        private void UnsubscribeFromNetworkEvents()
        {
            _deliveredCount.OnValueChanged -= HandleDeliveredCountChanged;
            _areDoorsOpen.OnValueChanged -= HandleDoorsOpenChanged;
            _isDeliveryComplete.OnValueChanged -= HandleDeliveryCompleteChanged;
        }

        private void SubscribeToTutorialEvents()
        {
            if (_isSubscribedToTutorial) return;
            if (TutorialManager.Instance == null) return;

            TutorialManager.Instance.OnStepCompleted += HandleTutorialStepCompleted;
            TutorialManager.Instance.OnTutorialCompleted += HandleTutorialCompleted;
            _isSubscribedToTutorial = true;

            LogDebug("Subscribed to TutorialManager events");
        }

        private void UnsubscribeFromTutorialEvents()
        {
            if (!_isSubscribedToTutorial) return;
            if (TutorialManager.Instance == null) return;

            TutorialManager.Instance.OnStepCompleted -= HandleTutorialStepCompleted;
            TutorialManager.Instance.OnTutorialCompleted -= HandleTutorialCompleted;
            _isSubscribedToTutorial = false;
        }

        #endregion

        #region Network Event Handlers

        private void HandleDeliveredCountChanged(int previousValue, int newValue)
        {
            UpdateUIText();
            LogDebug($"Delivered count changed: {previousValue} -> {newValue}");
        }

        private void HandleDoorsOpenChanged(bool previousValue, bool newValue)
        {
            LogDebug($"Doors state changed: {(newValue ? "OPEN" : "CLOSED")}");
            AnimateDoors(newValue);
        }

        private void HandleDeliveryCompleteChanged(bool previousValue, bool newValue)
        {
            if (newValue)
            {
                LogDebug("🎉 Tutorial truck delivery COMPLETE!");
                NotifyTutorialDeliveryComplete();
            }
        }

        #endregion

        #region Tutorial Event Handlers

        private void HandleTutorialStepCompleted(int stepIndex, TutorialStep step)
        {
            LogDebug($"Tutorial step {stepIndex} completed: {step.stepName}");

            // PlaceOnShelf step'i tamamlandığında kapıları aç
            if (stepIndex == placeOnShelfStepIndex)
            {
                LogDebug("PlaceOnShelf step completed - Opening truck doors!");
                OpenDoorsServerRpc();
            }
        }

        private void HandleTutorialCompleted()
        {
            LogDebug("Tutorial completed!");

            if (autoCloseOnTutorialComplete && IsServer)
            {
                _isActive.Value = false;
            }
        }

        #endregion

        #region Door Control

        [ServerRpc(RequireOwnership = false)]
        public void OpenDoorsServerRpc()
        {
            if (!IsServer) return;
            if (_areDoorsOpen.Value) return;

            _areDoorsOpen.Value = true;
            LogDebug("Server: Doors OPENED");
        }

        [ServerRpc(RequireOwnership = false)]
        public void CloseDoorsServerRpc()
        {
            if (!IsServer) return;
            if (!_areDoorsOpen.Value) return;

            _areDoorsOpen.Value = false;
            LogDebug("Server: Doors CLOSED");
        }

        private void AnimateDoors(bool open)
        {
            // Animator varsa kullan
            if (doorAnimator != null)
            {
                doorAnimator.SetBool(DOOR_OPEN_ANIM_BOOL, open);
                PlayDoorSound();
                return;
            }

            // Manual animasyon
            if (_doorAnimationCoroutine != null)
            {
                StopCoroutine(_doorAnimationCoroutine);
            }

            _doorAnimationCoroutine = StartCoroutine(AnimateDoorsCoroutine(open));
        }

        private IEnumerator AnimateDoorsCoroutine(bool open)
        {
            PlayDoorSound();

            Vector3 leftTargetRotation = open ? leftDoorOpenRotation : _leftDoorClosedRotation;
            Vector3 rightTargetRotation = open ? rightDoorOpenRotation : _rightDoorClosedRotation;

            float elapsed = 0f;
            float duration = 1f / doorOpenSpeed;

            Vector3 leftStartRotation = leftDoor != null ? leftDoor.transform.localEulerAngles : Vector3.zero;
            Vector3 rightStartRotation = rightDoor != null ? rightDoor.transform.localEulerAngles : Vector3.zero;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

                if (leftDoor != null)
                {
                    leftDoor.transform.localEulerAngles = Vector3.Lerp(leftStartRotation, leftTargetRotation, t);
                }

                if (rightDoor != null)
                {
                    rightDoor.transform.localEulerAngles = Vector3.Lerp(rightStartRotation, rightTargetRotation, t);
                }

                yield return null;
            }

            // Final pozisyon
            if (leftDoor != null)
            {
                leftDoor.transform.localEulerAngles = leftTargetRotation;
            }

            if (rightDoor != null)
            {
                rightDoor.transform.localEulerAngles = rightTargetRotation;
            }

            LogDebug($"Door animation complete - Doors are now {(open ? "OPEN" : "CLOSED")}");
        }

        private void SetDoorsInstant(bool open)
        {
            Vector3 leftTargetRotation = open ? leftDoorOpenRotation : _leftDoorClosedRotation;
            Vector3 rightTargetRotation = open ? rightDoorOpenRotation : _rightDoorClosedRotation;

            if (leftDoor != null)
            {
                leftDoor.transform.localEulerAngles = leftTargetRotation;
            }

            if (rightDoor != null)
            {
                rightDoor.transform.localEulerAngles = rightTargetRotation;
            }
        }

        private void PlayDoorSound()
        {
            if (doorAudioSource != null && doorOpenClip != null)
            {
                doorAudioSource.PlayOneShot(doorOpenClip);
            }
        }

        #endregion

        #region Delivery Handling

        /// <summary>
        /// TutorialTruckTrigger tarafından çağrılır
        /// </summary>
        public void HandleItemDelivery(BoxInfo.BoxType boxType, bool isFull)
        {
            if (!IsServer) return;

            HandleDeliveryServerRpc(boxType, isFull);
        }

        [ServerRpc(RequireOwnership = false)]
        public void HandleDeliveryServerRpc(BoxInfo.BoxType boxType, bool isFull, ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;

            // Kapılar kapalıysa teslimat kabul etme
            if (!_areDoorsOpen.Value)
            {
                LogDebug("❌ Delivery rejected - Doors are CLOSED!");
                return;
            }

            // Zaten tamamlandıysa kabul etme
            if (_isDeliveryComplete.Value)
            {
                LogDebug("❌ Delivery rejected - Already complete!");
                return;
            }

            // Aktif değilse kabul etme
            if (!_isActive.Value)
            {
                LogDebug("❌ Delivery rejected - Truck not active!");
                return;
            }

            if (isFull && boxType == requestedBoxType)
            {
                ProcessSuccessfulDelivery();
            }
            else if (isFull)
            {
                ProcessWrongDelivery(boxType);
            }
            else
            {
                LogDebug("❌ Delivery rejected - Box is not full!");
            }
        }

        private void ProcessSuccessfulDelivery()
        {
            _deliveredCount.Value++;

            LogDebug($"✅ Successful delivery! Count: {_deliveredCount.Value}/{requiredCargo}");

            // Ses çal
            PlayDeliverySuccessSoundClientRpc();

            // Tamamlandı mı kontrol et
            if (_deliveredCount.Value >= requiredCargo)
            {
                CompleteDelivery();
            }
        }

        private void ProcessWrongDelivery(BoxInfo.BoxType wrongType)
        {
            LogDebug($"❌ Wrong box type!  Expected: {requestedBoxType}, Got: {wrongType}");

            // Yanlış item sesi çal
            PlayWrongItemSoundClientRpc();
        }

        private void CompleteDelivery()
        {
            _isDeliveryComplete.Value = true;
            LogDebug("🎉 All cargo delivered!");
        }

        private void NotifyTutorialDeliveryComplete()
        {
            // TutorialManager'a bildir - bu şekilde current step otomatik tamamlanacak
            if (TutorialManager.Instance != null)
            {
                var currentStep = TutorialManager.Instance.CurrentStep;
                if (currentStep != null && currentStep.conditionType == TutorialConditionType.Custom)
                {
                    // Custom condition için manuel tamamlama
                    TutorialManager.Instance.ForceCompleteCurrentStep();
                }
            }
        }

        #endregion

        #region Client RPCs

        [ClientRpc]
        private void PlayDeliverySuccessSoundClientRpc()
        {
            if (doorAudioSource != null && itemDeliveredClip != null)
            {
                doorAudioSource.PlayOneShot(itemDeliveredClip);
            }
        }

        [ClientRpc]
        private void PlayWrongItemSoundClientRpc()
        {
            if (doorAudioSource != null && wrongItemClip != null)
            {
                doorAudioSource.PlayOneShot(wrongItemClip);
            }
        }

        #endregion

        #region UI Update

        private void UpdateUIText()
        {
            if (truckText != null)
            {
                string statusText = !_areDoorsOpen.Value
                    ? $"{requestedBoxType}: Kapılar Kapalı"
                    : $"{requestedBoxType}: {_deliveredCount.Value}/{requiredCargo}";

                truckText.text = statusText;
            }
        }

        #endregion

        #region Visual Update

        private void SetTruckColors()
        {
            Color targetColor = GetColorForBoxType(requestedBoxType);

            SetObjectColor(truckBody, targetColor);
            SetObjectColor(leftDoor, targetColor);
            SetObjectColor(rightDoor, targetColor);
        }

        private static Color GetColorForBoxType(BoxInfo.BoxType boxType)
        {
            return boxType switch
            {
                BoxInfo.BoxType.Red => Color.red,
                BoxInfo.BoxType.Yellow => Color.yellow,
                BoxInfo.BoxType.Blue => Color.blue,
                _ => Color.white
            };
        }

        private static void SetObjectColor(GameObject obj, Color color)
        {
            if (obj == null) return;

            var renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = new Material(renderer.material);
                renderer.material.color = color;
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Kapıları manuel olarak açar (TutorialManager dışından kullanım için)
        /// </summary>
        public void ForceOpenDoors()
        {
            if (IsServer)
            {
                _areDoorsOpen.Value = true;
            }
            else
            {
                OpenDoorsServerRpc();
            }
        }

        /// <summary>
        /// Tutorial truck'ı sıfırlar
        /// </summary>
        public void ResetTruck()
        {
            if (!IsServer) return;

            _deliveredCount.Value = 0;
            _areDoorsOpen.Value = false;
            _isDeliveryComplete.Value = false;
            _isActive.Value = true;

            UpdateUIText();
            LogDebug("Tutorial truck RESET");
        }

        /// <summary>
        /// Belirli bir step index'i için kapıları açar
        /// </summary>
        public void SetPlaceOnShelfStepIndex(int stepIndex)
        {
            placeOnShelfStepIndex = stepIndex;
        }

        #endregion

        #region Logging

        private void LogDebug(string message)
        {
            Debug.Log($"{LOG_PREFIX} {message}");
        }

        #endregion

        #region Editor Debug

#if UNITY_EDITOR
        [ContextMenu("Debug: Open Doors")]
        private void DebugOpenDoors()
        {
            if (Application.isPlaying)
            {
                ForceOpenDoors();
            }
            else
            {
                SetDoorsInstant(true);
            }
        }

        [ContextMenu("Debug: Close Doors")]
        private void DebugCloseDoors()
        {
            if (Application.isPlaying && IsServer)
            {
                _areDoorsOpen.Value = false;
            }
            else
            {
                SetDoorsInstant(false);
            }
        }

        [ContextMenu("Debug: Simulate Delivery")]
        private void DebugSimulateDelivery()
        {
            if (Application.isPlaying && IsServer)
            {
                ProcessSuccessfulDelivery();
            }
        }

        [ContextMenu("Debug: Reset Truck")]
        private void DebugResetTruck()
        {
            if (Application.isPlaying)
            {
                ResetTruck();
            }
        }

        [ContextMenu("Debug: Print State")]
        private void DebugPrintState()
        {
            Debug.Log($"{LOG_PREFIX} === TUTORIAL TRUCK STATE ===");
            Debug.Log($"Requested Box Type: {requestedBoxType}");
            Debug.Log($"Required Cargo: {requiredCargo}");
            Debug.Log($"Delivered Count: {_deliveredCount.Value}");
            Debug.Log($"Doors Open: {_areDoorsOpen.Value}");
            Debug.Log($"Delivery Complete: {_isDeliveryComplete.Value}");
            Debug.Log($"Is Active: {_isActive.Value}");
            Debug.Log($"PlaceOnShelf Step Index: {placeOnShelfStepIndex}");
            Debug.Log($"Subscribed to Tutorial: {_isSubscribedToTutorial}");
        }
#endif

        #endregion
    }
}