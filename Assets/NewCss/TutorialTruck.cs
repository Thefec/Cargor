using System;
using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace NewCss
{
    /// <summary>
    /// Tutorial için özelleştirilmiş kamyon sistemi. 
    /// Giriş animasyonu ile gelir, kutu teslimi alır, çıkış animasyonu ile gider.
    /// TutorialManager ile entegre çalışır.
    /// </summary>
    public class TutorialTruck : NetworkBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[TutorialTruck]";
        private const string ENTER_ANIM_STATE = "Enter";
        private const string EXIT_ANIM_STATE = "Exit";
        private const string EXIT_ANIM_BOOL = "DoExit";

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

        #endregion

        #region Serialized Fields - Animation

        [Header("=== ANIMATION SETTINGS ===")]
        [SerializeField, Tooltip("Kamyon animator'ı")]
        public Animator truckAnimator;

        [SerializeField, Tooltip("Çıkış gecikmesi (saniye)")]
        public float exitDelay = 2f;

        #endregion

        #region Serialized Fields - Audio

        [Header("=== AUDIO SETTINGS ===")]
        [SerializeField] public AudioSource audioSource;
        [SerializeField] public AudioClip enterAnimationClip;
        [SerializeField] public AudioClip exitAnimationClip;
        [SerializeField] public AudioClip itemDeliveredClip;
        [SerializeField] public AudioClip wrongItemClip;

        #endregion

        #region Serialized Fields - Debug

        [Header("=== DEBUG ===")]
        [SerializeField, Tooltip("Debug loglarını göster")]
        private bool showDebugLogs = true;

        #endregion

        #region Network Variables

        private readonly NetworkVariable<int> _deliveredCount = new(0,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> _isEntering = new(true,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> _isExiting = new(false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> _isDeliveryComplete = new(false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> _isReadyForDelivery = new(false,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        #endregion

        #region Events

        public event Action OnTruckArrived;
        public event Action OnDeliveryComplete;
        public event Action OnTruckExitComplete;

        #endregion

        #region Public Properties

        public int DeliveredCount => _deliveredCount.Value;
        public bool IsEntering => _isEntering.Value;
        public bool IsExiting => _isExiting.Value;
        public bool IsDeliveryComplete => _isDeliveryComplete.Value;
        public bool IsReadyForDelivery => _isReadyForDelivery.Value;

        #endregion

        #region Network Lifecycle

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            SubscribeToNetworkEvents();
            SetupTriggerCollider();
            SetTruckColors();
            UpdateUIText();

            // Giriş animasyonunu başlat
            if (IsServer)
            {
                _isEntering.Value = true;
                StartEnterAnimation();
            }

            LogDebug("TutorialTruck spawned - Starting enter animation");
        }

        public override void OnNetworkDespawn()
        {
            UnsubscribeFromNetworkEvents();
            base.OnNetworkDespawn();
        }

        #endregion

        #region Event Subscriptions

        private void SubscribeToNetworkEvents()
        {
            _deliveredCount.OnValueChanged += HandleDeliveredCountChanged;
            _isEntering.OnValueChanged += HandleIsEnteringChanged;
            _isExiting.OnValueChanged += HandleIsExitingChanged;
            _isDeliveryComplete.OnValueChanged += HandleDeliveryCompleteChanged;
            _isReadyForDelivery.OnValueChanged += HandleReadyForDeliveryChanged;
        }

        private void UnsubscribeFromNetworkEvents()
        {
            _deliveredCount.OnValueChanged -= HandleDeliveredCountChanged;
            _isEntering.OnValueChanged -= HandleIsEnteringChanged;
            _isExiting.OnValueChanged -= HandleIsExitingChanged;
            _isDeliveryComplete.OnValueChanged -= HandleDeliveryCompleteChanged;
            _isReadyForDelivery.OnValueChanged -= HandleReadyForDeliveryChanged;
        }

        #endregion

        #region Network Event Handlers

        private void HandleDeliveredCountChanged(int previousValue, int newValue)
        {
            UpdateUIText();
            LogDebug($"Delivered count: {newValue}/{requiredCargo}");
        }

        private void HandleIsEnteringChanged(bool previousValue, bool newValue)
        {
            if (!newValue && previousValue)
            {
                LogDebug("Enter animation complete - Ready for delivery");
                OnTruckArrived?.Invoke();
            }
        }

        private void HandleIsExitingChanged(bool previousValue, bool newValue)
        {
            if (newValue && !previousValue)
            {
                LogDebug("Exit animation started");
            }
        }

        private void HandleDeliveryCompleteChanged(bool previousValue, bool newValue)
        {
            if (newValue && !previousValue)
            {
                LogDebug("🎉 Delivery complete!");
                OnDeliveryComplete?.Invoke();
            }
        }

        private void HandleReadyForDeliveryChanged(bool previousValue, bool newValue)
        {
            if (newValue)
            {
                LogDebug("Truck is now ready for delivery");
                UpdateUIText();
            }
        }

        #endregion

        #region Initialization

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

        #region Enter Animation

        private void StartEnterAnimation()
        {
            if (!IsServer) return;

            LogDebug("Starting enter animation.. .");

            PlayEnterSoundClientRpc();

            if (truckAnimator != null)
            {
                truckAnimator.SetBool(EXIT_ANIM_BOOL, false);
                StartCoroutine(WaitForEnterAnimationCoroutine());
            }
            else
            {
                // Animator yoksa direkt hazır ol
                CompleteEnterAnimation();
            }
        }

        private IEnumerator WaitForEnterAnimationCoroutine()
        {
            yield return new WaitForEndOfFrame();

            // Animasyonun tamamlanmasını bekle
            while (!IsAnimationComplete(ENTER_ANIM_STATE))
            {
                yield return null;
            }

            if (IsServer)
            {
                CompleteEnterAnimation();
            }
        }

        private void CompleteEnterAnimation()
        {
            _isEntering.Value = false;
            _isReadyForDelivery.Value = true;

            LogDebug("Enter animation complete - Truck ready for delivery!");
        }

        [ClientRpc]
        private void PlayEnterSoundClientRpc()
        {
            PlaySound(enterAnimationClip);
        }

        #endregion

        #region Delivery Handling

        /// <summary>
        /// TutorialTruckTrigger tarafından çağrılır
        /// </summary>
        public void HandleItemDelivery(BoxInfo.BoxType boxType, bool isFull)
        {
            if (!IsServer)
            {
                HandleDeliveryServerRpc(boxType, isFull);
                return;
            }

            ProcessDeliveryInternal(boxType, isFull);
        }

        [ServerRpc(RequireOwnership = false)]
        public void HandleDeliveryServerRpc(BoxInfo.BoxType boxType, bool isFull)
        {
            ProcessDeliveryInternal(boxType, isFull);
        }

        private void ProcessDeliveryInternal(BoxInfo.BoxType boxType, bool isFull)
        {
            if (!IsServer) return;

            // Hazır değilse teslimat kabul etme
            if (!_isReadyForDelivery.Value)
            {
                LogDebug("❌ Delivery rejected - Truck not ready!");
                return;
            }

            // Zaten tamamlandıysa kabul etme
            if (_isDeliveryComplete.Value)
            {
                LogDebug("❌ Delivery rejected - Already complete!");
                return;
            }

            // Çıkış yapıyorsa kabul etme
            if (_isExiting.Value)
            {
                LogDebug("❌ Delivery rejected - Truck is exiting!");
                return;
            }

            if (isFull && boxType == requestedBoxType)
            {
                ProcessSuccessfulDelivery(boxType);
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

        private void ProcessSuccessfulDelivery(BoxInfo.BoxType boxType)
        {
            _deliveredCount.Value++;

            LogDebug($"✅ Successful delivery!  Count: {_deliveredCount.Value}/{requiredCargo}");

            // TutorialManager'a bildir
            if (TutorialManager.Instance != null)
            {
                TutorialManager.Instance.OnBoxDeliveredToTruck(boxType);
            }

            PlayDeliverySuccessSoundClientRpc();

            // Tamamlandı mı kontrol et
            if (_deliveredCount.Value >= requiredCargo)
            {
                CompleteDelivery();
            }
        }

        private void ProcessWrongDelivery(BoxInfo.BoxType wrongType)
        {
            LogDebug($"❌ Wrong box type! Expected: {requestedBoxType}, Got: {wrongType}");

            PlayWrongItemSoundClientRpc();
        }

        private void CompleteDelivery()
        {
            _isDeliveryComplete.Value = true;
            _isReadyForDelivery.Value = false;

            LogDebug("🎉 All cargo delivered!  Starting exit sequence...");

            // Çıkış sekansını başlat
            StartCoroutine(ExitSequenceCoroutine());
        }

        [ClientRpc]
        private void PlayDeliverySuccessSoundClientRpc()
        {
            PlaySound(itemDeliveredClip);
        }

        [ClientRpc]
        private void PlayWrongItemSoundClientRpc()
        {
            PlaySound(wrongItemClip);
        }

        #endregion

        #region Exit Animation

        private IEnumerator ExitSequenceCoroutine()
        {
            LogDebug($"Exit sequence starting in {exitDelay} seconds...");

            yield return new WaitForSeconds(exitDelay);

            StartExitAnimation();
        }

        private void StartExitAnimation()
        {
            if (!IsServer) return;

            LogDebug("Starting exit animation...");

            _isExiting.Value = true;

            PlayExitSoundClientRpc();

            if (truckAnimator != null)
            {
                truckAnimator.SetBool(EXIT_ANIM_BOOL, true);
                StartCoroutine(WaitForExitAnimationCoroutine());
            }
            else
            {
                CompleteExitAnimation();
            }
        }

        private IEnumerator WaitForExitAnimationCoroutine()
        {
            yield return new WaitForEndOfFrame();

            while (!IsAnimationComplete(EXIT_ANIM_STATE))
            {
                yield return null;
            }

            if (IsServer)
            {
                CompleteExitAnimation();
            }
        }

        private void CompleteExitAnimation()
        {
            LogDebug("Exit animation complete - Despawning truck");

            OnTruckExitComplete?.Invoke();

            // Truck'ı despawn et
            if (IsServer && NetworkObject != null && NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn();
            }
        }

        [ClientRpc]
        private void PlayExitSoundClientRpc()
        {
            PlaySound(exitAnimationClip);
        }

        #endregion

        #region Animation Helpers

        private bool IsAnimationComplete(string stateName)
        {
            if (truckAnimator == null) return true;

            var stateInfo = truckAnimator.GetCurrentAnimatorStateInfo(0);
            return stateInfo.IsName(stateName) && stateInfo.normalizedTime >= 1.0f;
        }

        #endregion

        #region UI Update

        private void UpdateUIText()
        {
            if (truckText == null) return;

            string statusText;

            if (_isEntering.Value)
            {
                statusText = $"{requestedBoxType}: Geliyor... ";
            }
            else if (_isExiting.Value)
            {
                statusText = $"{requestedBoxType}: Gidiyor...";
            }
            else if (_isDeliveryComplete.Value)
            {
                statusText = $"{requestedBoxType}: Tamamlandı!";
            }
            else
            {
                statusText = $"{requestedBoxType}: {_deliveredCount.Value}/{requiredCargo}";
            }

            truckText.text = statusText;
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

        #region Audio

        private void PlaySound(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
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
        [ContextMenu("Debug: Simulate Delivery")]
        private void DebugSimulateDelivery()
        {
            if (Application.isPlaying && IsServer)
            {
                ProcessSuccessfulDelivery(requestedBoxType);
            }
        }

        [ContextMenu("Debug: Force Exit")]
        private void DebugForceExit()
        {
            if (Application.isPlaying && IsServer)
            {
                StartExitAnimation();
            }
        }

        [ContextMenu("Debug: Print State")]
        private void DebugPrintState()
        {
            Debug.Log($"{LOG_PREFIX} === TUTORIAL TRUCK STATE ===");
            Debug.Log($"Requested Box Type: {requestedBoxType}");
            Debug.Log($"Required Cargo: {requiredCargo}");
            Debug.Log($"Delivered Count: {_deliveredCount.Value}");
            Debug.Log($"Is Entering: {_isEntering.Value}");
            Debug.Log($"Is Ready For Delivery: {_isReadyForDelivery.Value}");
            Debug.Log($"Is Delivery Complete: {_isDeliveryComplete.Value}");
            Debug.Log($"Is Exiting: {_isExiting.Value}");
        }
#endif

        #endregion
    }
}