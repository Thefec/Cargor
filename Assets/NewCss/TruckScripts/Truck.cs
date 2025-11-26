using UnityEngine;
using Unity.Netcode;
using TMPro;
using System.Collections;

namespace NewCss
{
    public class Truck : NetworkBehaviour
    {
        [Header("Truck Request Settings")]
        public BoxInfo.BoxType requestedBoxType;
        public int requiredCargo;

        private NetworkVariable<int> deliveredCount = new NetworkVariable<int>(0);
        private NetworkVariable<BoxInfo.BoxType> networkRequestedBoxType = new NetworkVariable<BoxInfo.BoxType>(BoxInfo.BoxType.Red);
        private NetworkVariable<int> networkRequiredCargo = new NetworkVariable<int>(1);
        private NetworkVariable<bool> isComplete = new NetworkVariable<bool>(false);
        private NetworkVariable<bool> isEntering = new NetworkVariable<bool>(true);

        // ✅ YENİ: Animator state'lerini network-sync için
        private NetworkVariable<bool> isPlayingExitAnimation = new NetworkVariable<bool>(false);
        private NetworkVariable<bool> isPlayingEnterAnimation = new NetworkVariable<bool>(false);

        [Header("UI")]
        public TextMeshProUGUI truckText;

        [Header("Collider Settings")]
        public GameObject triggerColliderObject;

        [Header("Truck Parts - Colors")]
        public GameObject truckBody;
        public GameObject leftDoor;
        public GameObject rightDoor;

        [Header("Animation Settings")]
        public Animator truckAnimator;

        [Header("Movement Settings")]
        public Transform entryPoint;
        public Transform exitPoint;

        [Header("Exit Animation Settings")]
        public float exitDelay = 5f;

        [Header("Money Rewards/Penalties")]
        public int rewardPerBox = 50;
        public int penaltyPerBox = 60;

        [Header("Prestige Bonus Settings")]
        [Tooltip("Prestige required for each bonus tier (default: 10)")]
        public float prestigePerBonus = 10f;

        [Tooltip("Money bonus per box for each prestige tier (default: 5)")]
        public int bonusPerTier = 5;

        [Header("Audio Settings")]
        public AudioSource enterAudioSource;
        public AudioClip enterAnimationClip;

        public AudioSource exitDelayAudioSource;
        public AudioClip exitDelayClip;

        public AudioSource exitAudioSource;
        public AudioClip exitAnimationClip;

        private bool hasPreInitialized = false;

        [HideInInspector] public int hangarIndex = 0;

        public void PreInitialize(BoxInfo.BoxType reqType, int reqAmount)
        {
            requestedBoxType = reqType;
            requiredCargo = reqAmount;
            hasPreInitialized = true;
        }

        public override void OnNetworkSpawn()
        {
            deliveredCount.OnValueChanged += OnDeliveredCountChanged;
            networkRequestedBoxType.OnValueChanged += OnRequestedBoxTypeChanged;
            networkRequiredCargo.OnValueChanged += OnRequiredCargoChanged;
            isComplete.OnValueChanged += OnIsCompleteChanged;
            isEntering.OnValueChanged += OnIsEnteringChanged;

            // ✅ YENİ: Animator state değişikliklerini dinle
            isPlayingExitAnimation.OnValueChanged += OnExitAnimationChanged;
            isPlayingEnterAnimation.OnValueChanged += OnEnterAnimationChanged;

            SetupTriggerCollider();
            AutoFindAudioSources();

            if (hasPreInitialized)
            {
                UpdateUIText();
                SetTruckColors();
            }

            if (!IsServer)
            {
                if (!hasPreInitialized)
                {
                    requestedBoxType = networkRequestedBoxType.Value;
                    requiredCargo = networkRequiredCargo.Value;
                }
                UpdateUIText();
                SetTruckColors();
            }

            if (IsServer)
            {
                StartEnterAnimation();
            }
        }

        public override void OnNetworkDespawn()
        {
            deliveredCount.OnValueChanged -= OnDeliveredCountChanged;
            networkRequestedBoxType.OnValueChanged -= OnRequestedBoxTypeChanged;
            networkRequiredCargo.OnValueChanged -= OnRequiredCargoChanged;
            isComplete.OnValueChanged -= OnIsCompleteChanged;
            isEntering.OnValueChanged -= OnIsEnteringChanged;
            isPlayingExitAnimation.OnValueChanged -= OnExitAnimationChanged;
            isPlayingEnterAnimation.OnValueChanged -= OnEnterAnimationChanged;
        }

        [ServerRpc]
        public void InitializeServerRpc(BoxInfo.BoxType reqType, int reqAmount)
        {
            networkRequestedBoxType.Value = reqType;
            networkRequiredCargo.Value = reqAmount;
            deliveredCount.Value = 0;
            isComplete.Value = false;
            isEntering.Value = true;

            requestedBoxType = reqType;
            requiredCargo = reqAmount;

            UpdateVisualsClientRpc(reqType, reqAmount);
        }

        [ClientRpc]
        private void UpdateVisualsClientRpc(BoxInfo.BoxType reqType, int reqAmount)
        {
            requestedBoxType = reqType;
            requiredCargo = reqAmount;

            UpdateUIText();
            SetTruckColors();
        }

        private void OnDeliveredCountChanged(int previousValue, int newValue)
        {
            UpdateUIText();
        }

        private void OnRequestedBoxTypeChanged(BoxInfo.BoxType previousValue, BoxInfo.BoxType newValue)
        {
            requestedBoxType = newValue;
            SetTruckColors();
            UpdateUIText();
        }

        private void OnRequiredCargoChanged(int previousValue, int newValue)
        {
            requiredCargo = newValue;
            UpdateUIText();
        }

        private void OnIsCompleteChanged(bool previousValue, bool newValue)
        {
            if (newValue && !previousValue)
            {
                if (IsServer)
                {
                    StartCoroutine(ExitSequence());
                }
            }
        }

        private void OnIsEnteringChanged(bool previousValue, bool newValue)
        {
        }

        /// <summary>
        /// ✅ YENİ: Enter animation değişikliğini tüm client'larda uygula
        /// </summary>
        private void OnEnterAnimationChanged(bool previousValue, bool newValue)
        {
            if (newValue && truckAnimator != null)
            {
                Debug.Log($"🚚 CLIENT {(IsServer ? "SERVER" : NetworkManager.Singleton.LocalClientId.ToString())}: Enter animation başlatılıyor");
                truckAnimator.SetBool("DoExit", false);

                // Client'ta da coroutine başlat (sadece görsel için)
                if (!IsServer)
                {
                    StartCoroutine(WaitForEnterAnimationComplete());
                }
            }
        }

        /// <summary>
        /// ✅ YENİ: Exit animation değişikliğini tüm client'larda uygula
        /// </summary>
        private void OnExitAnimationChanged(bool previousValue, bool newValue)
        {
            if (newValue && truckAnimator != null)
            {
                Debug.Log($"🚚 CLIENT {(IsServer ? "SERVER" : NetworkManager.Singleton.LocalClientId.ToString())}: Exit animation başlatılıyor");
                truckAnimator.SetBool("DoExit", true);

                // Client'ta da coroutine başlat (sadece görsel için)
                if (!IsServer)
                {
                    StartCoroutine(WaitForExitAnimationComplete());
                }
            }
        }

        private void SetupTriggerCollider()
        {
            GameObject colliderObj = triggerColliderObject != null ? triggerColliderObject : gameObject;

            Collider col = colliderObj.GetComponent<Collider>();
            if (col == null)
            {
                return;
            }

            col.isTrigger = true;

            TruckTrigger trigger = colliderObj.GetComponent<TruckTrigger>();
            if (trigger == null)
            {
                trigger = colliderObj.AddComponent<TruckTrigger>();
            }

            trigger.mainTruck = this;
        }

        /// <summary>
        /// ✅ FIX: Enter animation'ı server'da başlat ve network-sync yap
        /// </summary>
        private void StartEnterAnimation()
        {
            if (!IsServer) return;

            Debug.Log("🚚 SERVER: Enter animation başlatılıyor");

            // ✅ Tüm client'lara bildir
            isPlayingEnterAnimation.Value = true;
            PlayEnterAnimationSoundClientRpc();

            if (truckAnimator != null)
            {
                truckAnimator.SetBool("DoExit", false);
                StartCoroutine(WaitForEnterAnimationComplete());
            }
            else
            {
                isEntering.Value = false;
                isPlayingEnterAnimation.Value = false;
            }
        }

        private IEnumerator WaitForEnterAnimationComplete()
        {
            if (truckAnimator != null)
            {
                yield return new WaitForEndOfFrame();

                while (!truckAnimator.GetCurrentAnimatorStateInfo(0).IsName("Enter") ||
                       truckAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1.0f)
                {
                    yield return null;
                }
            }

            if (IsServer)
            {
                isEntering.Value = false;
                isPlayingEnterAnimation.Value = false;
                Debug.Log("🚚 SERVER: Enter animation tamamlandı");
            }
        }

        private void UpdateUIText()
        {
            if (truckText != null)
            {
                truckText.text = $"{requestedBoxType}: {deliveredCount.Value}/{requiredCargo}";
            }
        }

        private void SetTruckColors()
        {
            Color targetColor = GetColorForBoxType(requestedBoxType);

            SetObjectColor(truckBody, targetColor);
            SetObjectColor(leftDoor, targetColor);
            SetObjectColor(rightDoor, targetColor);
        }

        private Color GetColorForBoxType(BoxInfo.BoxType boxType)
        {
            switch (boxType)
            {
                case BoxInfo.BoxType.Red:
                    return Color.red;
                case BoxInfo.BoxType.Yellow:
                    return Color.yellow;
                case BoxInfo.BoxType.Blue:
                    return Color.blue;
                default:
                    return Color.white;
            }
        }

        private void SetObjectColor(GameObject obj, Color color)
        {
            if (obj == null) return;

            var renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = new Material(renderer.material);
                renderer.material.color = color;
            }
        }

        private int CalculateRewardWithPrestige()
        {
            int baseReward = rewardPerBox;

            if (PrestigeManager.Instance != null)
            {
                float currentPrestige = PrestigeManager.Instance.GetPrestige();
                int prestigeTiers = Mathf.FloorToInt(currentPrestige / prestigePerBonus);
                int prestigeBonus = prestigeTiers * bonusPerTier;
                int totalReward = baseReward + prestigeBonus;

                Debug.Log($"[Truck] Base: {baseReward}, Prestige: {currentPrestige:F1}, Tiers: {prestigeTiers}, Bonus: {prestigeBonus}, Total: {totalReward}");

                return totalReward;
            }

            return baseReward;
        }

        public int GetCurrentPrestigeBonus()
        {
            if (PrestigeManager.Instance != null)
            {
                float currentPrestige = PrestigeManager.Instance.GetPrestige();
                int prestigeTiers = Mathf.FloorToInt(currentPrestige / prestigePerBonus);
                return prestigeTiers * bonusPerTier;
            }
            return 0;
        }

        [ServerRpc(RequireOwnership = false)]
        public void HandleDeliveryServerRpc(BoxInfo.BoxType boxType, bool isFull)
        {
            if (isComplete.Value || isEntering.Value)
                return;

            if (isFull && boxType == networkRequestedBoxType.Value)
            {
                deliveredCount.Value++;

                int totalReward = CalculateRewardWithPrestige();

                if (MoneySystem.Instance != null)
                {
                    MoneySystem.Instance.AddMoney(totalReward);
                }

                if (deliveredCount.Value >= networkRequiredCargo.Value)
                {
                    if (QuestManager.Instance != null)
                    {
                        QuestManager.Instance.IncrementQuestProgress(QuestType.DeliverTrucks);
                    }
                    isComplete.Value = true;
                }
            }
            else if (isFull)
            {
                if (MoneySystem.Instance != null)
                {
                    MoneySystem.Instance.SpendMoney(penaltyPerBox);
                }
            }
        }

        public void ForceExitDueToTime()
        {
            if (!IsServer) return;

            if (isEntering.Value || isComplete.Value)
                return;

            isComplete.Value = true;
            StopAllCoroutines();
            StartCoroutine(ExitSequence());
        }

        private IEnumerator ExitSequence()
        {
            Debug.Log("🚚 SERVER: Exit sequence başladı");

            if (IsServer)
            {
                PlayExitDelaySoundClientRpc();
            }

            yield return new WaitForSeconds(exitDelay);

            StartExitAnimation();
        }

        /// <summary>
        /// ✅ FIX: Exit animation'ı server'da başlat ve network-sync yap
        /// </summary>
        private void StartExitAnimation()
        {
            if (!IsServer) return;

            Debug.Log("🚚 SERVER: Exit animation başlatılıyor");

            // ✅ Önce sesi durdur
            StopExitDelaySoundClientRpc();

            // ✅ Exit animation sesini çal
            PlayExitAnimationSoundClientRpc();

            // ✅ Tüm client'lara animation başladığını bildir
            isPlayingExitAnimation.Value = true;

            if (truckAnimator != null)
            {
                truckAnimator.SetBool("DoExit", true);
                StartCoroutine(WaitForExitAnimationComplete());
            }
            else
            {
                CompleteTruckExit();
            }
        }

        private IEnumerator WaitForExitAnimationComplete()
        {
            if (truckAnimator != null)
            {
                yield return new WaitForEndOfFrame();

                while (!truckAnimator.GetCurrentAnimatorStateInfo(0).IsName("Exit") ||
                       truckAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1.0f)
                {
                    yield return null;
                }
            }

            // ✅ Sadece server despawn etsin
            if (IsServer)
            {
                Debug.Log("🚚 SERVER: Exit animation tamamlandı - Despawn ediliyor");
                CompleteTruckExit();
            }
            else
            {
                Debug.Log($"🚚 CLIENT {NetworkManager.Singleton.LocalClientId}: Exit animation tamamlandı");
            }
        }

        private void CompleteTruckExit()
        {
            if (!IsServer) return;

            if (TruckSpawner.Instance != null)
            {
                TruckSpawner.Instance.OnTruckDestroyed(hangarIndex);
            }

            GetComponent<NetworkObject>().Despawn();
        }

        [ClientRpc]
        private void PlayEnterAnimationSoundClientRpc()
        {
            if (enterAudioSource != null && enterAnimationClip != null)
            {
                enterAudioSource.PlayOneShot(enterAnimationClip);
                Debug.Log($"🔊 CLIENT {(IsServer ? "SERVER" : NetworkManager.Singleton.LocalClientId.ToString())}: Enter sound çalınıyor");
            }
        }

        [ClientRpc]
        private void PlayExitDelaySoundClientRpc()
        {
            if (exitDelayAudioSource != null && exitDelayClip != null)
            {
                exitDelayAudioSource.clip = exitDelayClip;
                exitDelayAudioSource.loop = true;
                exitDelayAudioSource.Play();
                Debug.Log($"🔊 CLIENT {(IsServer ? "SERVER" : NetworkManager.Singleton.LocalClientId.ToString())}: Exit delay sound çalınıyor");
            }
        }

        [ClientRpc]
        private void StopExitDelaySoundClientRpc()
        {
            if (exitDelayAudioSource != null && exitDelayAudioSource.isPlaying)
            {
                exitDelayAudioSource.Stop();
                Debug.Log($"🔊 CLIENT {(IsServer ? "SERVER" : NetworkManager.Singleton.LocalClientId.ToString())}: Exit delay sound durduruluyor");
            }
        }

        [ClientRpc]
        private void PlayExitAnimationSoundClientRpc()
        {
            if (exitAudioSource != null && exitAnimationClip != null)
            {
                exitAudioSource.PlayOneShot(exitAnimationClip);
                Debug.Log($"🔊 CLIENT {(IsServer ? "SERVER" : NetworkManager.Singleton.LocalClientId.ToString())}: Exit animation sound çalınıyor");
            }
        }

        private void AutoFindAudioSources()
        {
            if (enterAudioSource == null || exitDelayAudioSource == null || exitAudioSource == null)
            {
                AudioSource[] sources = GetComponentsInChildren<AudioSource>(true);
                if (sources != null && sources.Length > 0)
                {
                    if (enterAudioSource == null && sources.Length > 0)
                        enterAudioSource = sources[0];

                    if (exitDelayAudioSource == null && sources.Length > 1)
                        exitDelayAudioSource = sources[1];
                    else if (exitDelayAudioSource == null)
                        exitDelayAudioSource = enterAudioSource;

                    if (exitAudioSource == null && sources.Length > 2)
                        exitAudioSource = sources[2];
                    else if (exitAudioSource == null)
                        exitAudioSource = enterAudioSource;
                }
            }
        }
    }
}