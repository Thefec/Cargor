using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace NewCss
{
    /// <summary>
    /// Kamyon yönetimi - kargo teslimatı, animasyonlar ve ödül sistemini yönetir. 
    /// Network senkronizasyonu ile multiplayer desteği sağlar.
    /// </summary>
    public class Truck : NetworkBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[Truck]";
        private const string ENTER_ANIM_STATE = "Enter";
        private const string EXIT_ANIM_STATE = "Exit";
        private const string EXIT_ANIM_BOOL = "DoExit";

        #endregion

        #region Serialized Fields - Request Settings

        [Header("=== TRUCK REQUEST SETTINGS ===")]
        [SerializeField]
        public BoxInfo.BoxType requestedBoxType;

        [SerializeField]
        public int requiredCargo;

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

        [Header("=== TRUCK PARTS - COLORS ===")]
        [SerializeField] public GameObject truckBody;
        [SerializeField] public GameObject leftDoor;
        [SerializeField] public GameObject rightDoor;

        #endregion

        #region Serialized Fields - Animation

        [Header("=== ANIMATION SETTINGS ===")]
        [SerializeField, Tooltip("Kamyon animator'ı")]
        public Animator truckAnimator;

        #endregion

        #region Serialized Fields - Movement

        [Header("=== MOVEMENT SETTINGS ===")]
        [SerializeField] public Transform entryPoint;
        [SerializeField] public Transform exitPoint;

        #endregion

        #region Serialized Fields - Exit

        [Header("=== EXIT ANIMATION SETTINGS ===")]
        [SerializeField, Tooltip("Çıkış gecikmesi")]
        public float exitDelay = 5f;

        #endregion

        #region Serialized Fields - Rewards

        [Header("=== MONEY REWARDS/PENALTIES ===")]
        [SerializeField, Tooltip("Kutu başına ödül")]
        public int rewardPerBox = 50;

        [SerializeField, Tooltip("Kutu başına ceza")]
        public int penaltyPerBox = 60;

        [Header("=== PRESTIGE BONUS SETTINGS ===")]
        [SerializeField, Tooltip("Her bonus tier için gereken prestige")]
        public float prestigePerBonus = 10f;

        [SerializeField, Tooltip("Her tier için kutu başına bonus")]
        public int bonusPerTier = 5;

        #endregion

        #region Serialized Fields - Audio

        [Header("=== AUDIO SETTINGS ===")]
        [SerializeField] public AudioSource enterAudioSource;
        [SerializeField] public AudioClip enterAnimationClip;
        [SerializeField] public AudioSource exitDelayAudioSource;
        [SerializeField] public AudioClip exitDelayClip;
        [SerializeField] public AudioSource exitAudioSource;
        [SerializeField] public AudioClip exitAnimationClip;

        #endregion

        #region Network Variables

        private readonly NetworkVariable<int> _deliveredCount = new(0);
        private readonly NetworkVariable<BoxInfo.BoxType> _networkRequestedBoxType = new(BoxInfo.BoxType.Red);
        private readonly NetworkVariable<int> _networkRequiredCargo = new(1);
        private readonly NetworkVariable<bool> _isComplete = new(false);
        private readonly NetworkVariable<bool> _isEntering = new(true);
        private readonly NetworkVariable<bool> _isPlayingExitAnimation = new(false);
        private readonly NetworkVariable<bool> _isPlayingEnterAnimation = new(false);

        #endregion

        #region Private Fields

        private bool _hasPreInitialized;

        [HideInInspector]
        public int hangarIndex;

        #endregion

        #region Public Properties

        /// <summary>
        /// Teslim edilen kargo sayısı
        /// </summary>
        public int DeliveredCount => _deliveredCount.Value;

        /// <summary>
        /// İstenen kargo sayısı
        /// </summary>
        public int RequiredCargo => _networkRequiredCargo.Value;

        /// <summary>
        /// Teslimat tamamlandı mı? 
        /// </summary>
        public bool IsComplete => _isComplete.Value;

        /// <summary>
        /// Giriş animasyonu oynatılıyor mu?
        /// </summary>
        public bool IsEntering => _isEntering.Value;

        /// <summary>
        /// Mevcut prestige bonusu
        /// </summary>
        public int CurrentPrestigeBonus => CalculatePrestigeBonus();

        #endregion

        #region Pre-Initialization

        /// <summary>
        /// Network spawn öncesi başlatma
        /// </summary>
        public void PreInitialize(BoxInfo.BoxType reqType, int reqAmount)
        {
            requestedBoxType = reqType;
            requiredCargo = reqAmount;
            _hasPreInitialized = true;
        }

        #endregion

        #region Network Lifecycle

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            SubscribeToNetworkEvents();
            SetupTriggerCollider();
            AutoFindAudioSources();

            if (_hasPreInitialized)
            {
                UpdateUIText();
                SetTruckColors();
            }

            if (!IsServer)
            {
                SyncFromNetworkValues();
            }

            if (IsServer)
            {
                StartEnterAnimation();
            }
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
            _networkRequestedBoxType.OnValueChanged += HandleRequestedBoxTypeChanged;
            _networkRequiredCargo.OnValueChanged += HandleRequiredCargoChanged;
            _isComplete.OnValueChanged += HandleIsCompleteChanged;
            _isEntering.OnValueChanged += HandleIsEnteringChanged;
            _isPlayingExitAnimation.OnValueChanged += HandleExitAnimationChanged;
            _isPlayingEnterAnimation.OnValueChanged += HandleEnterAnimationChanged;
        }

        private void UnsubscribeFromNetworkEvents()
        {
            _deliveredCount.OnValueChanged -= HandleDeliveredCountChanged;
            _networkRequestedBoxType.OnValueChanged -= HandleRequestedBoxTypeChanged;
            _networkRequiredCargo.OnValueChanged -= HandleRequiredCargoChanged;
            _isComplete.OnValueChanged -= HandleIsCompleteChanged;
            _isEntering.OnValueChanged -= HandleIsEnteringChanged;
            _isPlayingExitAnimation.OnValueChanged -= HandleExitAnimationChanged;
            _isPlayingEnterAnimation.OnValueChanged -= HandleEnterAnimationChanged;
        }

        #endregion

        #region Network Event Handlers

        private void HandleDeliveredCountChanged(int previousValue, int newValue)
        {
            UpdateUIText();
        }

        private void HandleRequestedBoxTypeChanged(BoxInfo.BoxType previousValue, BoxInfo.BoxType newValue)
        {
            requestedBoxType = newValue;
            SetTruckColors();
            UpdateUIText();
        }

        private void HandleRequiredCargoChanged(int previousValue, int newValue)
        {
            requiredCargo = newValue;
            UpdateUIText();
        }

        private void HandleIsCompleteChanged(bool previousValue, bool newValue)
        {
            if (newValue && !previousValue && IsServer)
            {
                StartCoroutine(ExitSequenceCoroutine());
            }
        }

        private void HandleIsEnteringChanged(bool previousValue, bool newValue)
        {
            // Reserved for future use
        }

        private void HandleEnterAnimationChanged(bool previousValue, bool newValue)
        {
            if (!newValue || truckAnimator == null) return;

            LogDebug($"Enter animation starting on {(IsServer ? "SERVER" : $"CLIENT {NetworkManager.Singleton.LocalClientId}")}");
            truckAnimator.SetBool(EXIT_ANIM_BOOL, false);

            if (!IsServer)
            {
                StartCoroutine(WaitForEnterAnimationCoroutine());
            }
        }

        private void HandleExitAnimationChanged(bool previousValue, bool newValue)
        {
            if (!newValue || truckAnimator == null) return;

            LogDebug($"Exit animation starting on {(IsServer ? "SERVER" : $"CLIENT {NetworkManager.Singleton.LocalClientId}")}");
            truckAnimator.SetBool(EXIT_ANIM_BOOL, true);

            if (!IsServer)
            {
                StartCoroutine(WaitForExitAnimationCoroutine());
            }
        }

        #endregion

        #region Initialization

        private void SyncFromNetworkValues()
        {
            if (!_hasPreInitialized)
            {
                requestedBoxType = _networkRequestedBoxType.Value;
                requiredCargo = _networkRequiredCargo.Value;
            }
            UpdateUIText();
            SetTruckColors();
        }

        private void SetupTriggerCollider()
        {
            GameObject colliderObj = triggerColliderObject != null ? triggerColliderObject : gameObject;
            Collider col = colliderObj.GetComponent<Collider>();

            if (col == null) return;

            col.isTrigger = true;

            TruckTrigger trigger = colliderObj.GetComponent<TruckTrigger>();
            if (trigger == null)
            {
                trigger = colliderObj.AddComponent<TruckTrigger>();
            }
            trigger.mainTruck = this;
        }

        private void AutoFindAudioSources()
        {
            if (enterAudioSource != null && exitDelayAudioSource != null && exitAudioSource != null)
            {
                return;
            }

            AudioSource[] sources = GetComponentsInChildren<AudioSource>(true);
            if (sources == null || sources.Length == 0) return;

            enterAudioSource ??= sources.Length > 0 ? sources[0] : null;
            exitDelayAudioSource ??= sources.Length > 1 ? sources[1] : enterAudioSource;
            exitAudioSource ??= sources.Length > 2 ? sources[2] : enterAudioSource;
        }

        #endregion

        #region Server RPCs

        [ServerRpc]
        public void InitializeServerRpc(BoxInfo.BoxType reqType, int reqAmount)
        {
            _networkRequestedBoxType.Value = reqType;
            _networkRequiredCargo.Value = reqAmount;
            _deliveredCount.Value = 0;
            _isComplete.Value = false;
            _isEntering.Value = true;

            requestedBoxType = reqType;
            requiredCargo = reqAmount;

            UpdateVisualsClientRpc(reqType, reqAmount);
        }

        [ServerRpc(RequireOwnership = false)]
        public void HandleDeliveryServerRpc(BoxInfo.BoxType boxType, bool isFull)
        {
            if (_isComplete.Value || _isEntering.Value)
            {
                return;
            }

            if (isFull && boxType == _networkRequestedBoxType.Value)
            {
                ProcessSuccessfulDelivery();
            }
            else if (isFull)
            {
                ProcessWrongDelivery();
            }
        }

        #endregion

        #region Client RPCs

        [ClientRpc]
        private void UpdateVisualsClientRpc(BoxInfo.BoxType reqType, int reqAmount)
        {
            requestedBoxType = reqType;
            requiredCargo = reqAmount;

            UpdateUIText();
            SetTruckColors();
        }

        [ClientRpc]
        private void PlayEnterAnimationSoundClientRpc()
        {
            PlaySound(enterAudioSource, enterAnimationClip);
            LogDebug($"Enter sound playing on {GetClientIdentifier()}");
        }

        [ClientRpc]
        private void PlayExitDelaySoundClientRpc()
        {
            if (exitDelayAudioSource != null && exitDelayClip != null)
            {
                exitDelayAudioSource.clip = exitDelayClip;
                exitDelayAudioSource.loop = true;
                exitDelayAudioSource.Play();
                LogDebug($"Exit delay sound playing on {GetClientIdentifier()}");
            }
        }

        [ClientRpc]
        private void StopExitDelaySoundClientRpc()
        {
            if (exitDelayAudioSource != null && exitDelayAudioSource.isPlaying)
            {
                exitDelayAudioSource.Stop();
                LogDebug($"Exit delay sound stopped on {GetClientIdentifier()}");
            }
        }

        [ClientRpc]
        private void PlayExitAnimationSoundClientRpc()
        {
            PlaySound(exitAudioSource, exitAnimationClip);
            LogDebug($"Exit animation sound playing on {GetClientIdentifier()}");
        }

        #endregion

        #region Delivery Processing

        private void ProcessSuccessfulDelivery()
        {
            _deliveredCount.Value++;

            int totalReward = CalculateRewardWithPrestige();
            MoneySystem.Instance?.AddMoney(totalReward);

            if (_deliveredCount.Value >= _networkRequiredCargo.Value)
            {
                CompleteDelivery();
            }
        }

        private void ProcessWrongDelivery()
        {
            MoneySystem.Instance?.SpendMoney(penaltyPerBox);
        }

        private void CompleteDelivery()
        {
            QuestManager.Instance?.IncrementQuestProgress(QuestType.DeliverTrucks);
            _isComplete.Value = true;
        }

        #endregion

        #region Reward Calculation

        private int CalculateRewardWithPrestige()
        {
            int baseReward = rewardPerBox;
            int prestigeBonus = CalculatePrestigeBonus();
            int totalReward = baseReward + prestigeBonus;

            LogDebug($"Base: {baseReward}, Prestige Bonus: {prestigeBonus}, Total: {totalReward}");

            return totalReward;
        }

        private int CalculatePrestigeBonus()
        {
            if (PrestigeManager.Instance == null) return 0;

            float currentPrestige = PrestigeManager.Instance.GetPrestige();
            int prestigeTiers = Mathf.FloorToInt(currentPrestige / prestigePerBonus);
            return prestigeTiers * bonusPerTier;
        }

        /// <summary>
        /// Mevcut prestige bonusunu döndürür (public accessor)
        /// </summary>
        public int GetCurrentPrestigeBonus()
        {
            return CalculatePrestigeBonus();
        }

        #endregion

        #region Enter Animation

        private void StartEnterAnimation()
        {
            if (!IsServer) return;

            LogDebug("Starting enter animation");

            _isPlayingEnterAnimation.Value = true;
            PlayEnterAnimationSoundClientRpc();

            if (truckAnimator != null)
            {
                truckAnimator.SetBool(EXIT_ANIM_BOOL, false);
                StartCoroutine(WaitForEnterAnimationCoroutine());
            }
            else
            {
                _isEntering.Value = false;
                _isPlayingEnterAnimation.Value = false;
            }
        }

        private IEnumerator WaitForEnterAnimationCoroutine()
        {
            if (truckAnimator != null)
            {
                yield return new WaitForEndOfFrame();

                while (!IsAnimationComplete(ENTER_ANIM_STATE))
                {
                    yield return null;
                }
            }

            if (IsServer)
            {
                _isEntering.Value = false;
                _isPlayingEnterAnimation.Value = false;
                LogDebug("Enter animation completed");
            }
        }

        #endregion

        #region Exit Animation

        /// <summary>
        /// Zamana bağlı zorla çıkış
        /// </summary>
        public void ForceExitDueToTime()
        {
            if (!IsServer) return;
            if (_isEntering.Value || _isComplete.Value) return;

            _isComplete.Value = true;
            StopAllCoroutines();
            StartCoroutine(ExitSequenceCoroutine());
        }

        private IEnumerator ExitSequenceCoroutine()
        {
            LogDebug("Exit sequence started");

            PlayExitDelaySoundClientRpc();

            yield return new WaitForSeconds(exitDelay);

            StartExitAnimation();
        }

        private void StartExitAnimation()
        {
            if (!IsServer) return;

            LogDebug("Starting exit animation");

            StopExitDelaySoundClientRpc();
            PlayExitAnimationSoundClientRpc();

            _isPlayingExitAnimation.Value = true;

            if (truckAnimator != null)
            {
                truckAnimator.SetBool(EXIT_ANIM_BOOL, true);
                StartCoroutine(WaitForExitAnimationCoroutine());
            }
            else
            {
                CompleteTruckExit();
            }
        }

        private IEnumerator WaitForExitAnimationCoroutine()
        {
            if (truckAnimator != null)
            {
                yield return new WaitForEndOfFrame();

                while (!IsAnimationComplete(EXIT_ANIM_STATE))
                {
                    yield return null;
                }
            }

            if (IsServer)
            {
                LogDebug("Exit animation completed - Despawning");
                CompleteTruckExit();
            }
            else
            {
                LogDebug($"Exit animation completed on CLIENT {NetworkManager.Singleton.LocalClientId}");
            }
        }

        private void CompleteTruckExit()
        {
            if (!IsServer) return;

            TruckSpawner.Instance?.OnTruckDestroyed(hangarIndex);
            GetComponent<NetworkObject>().Despawn();
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
            if (truckText != null)
            {
                truckText.text = $"{requestedBoxType}: {_deliveredCount.Value}/{requiredCargo}";
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

        #region Audio Helpers

        private static void PlaySound(AudioSource source, AudioClip clip)
        {
            if (source != null && clip != null)
            {
                source.PlayOneShot(clip);
            }
        }

        #endregion

        #region Utility

        private string GetClientIdentifier()
        {
            return IsServer ? "SERVER" : $"CLIENT {NetworkManager.Singleton.LocalClientId}";
        }

        #endregion

        #region Logging

        private void LogDebug(string message)
        {
            Debug.Log($"{LOG_PREFIX} {message}");
        }

        #endregion
    }
}