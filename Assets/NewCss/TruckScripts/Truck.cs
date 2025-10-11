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

        // Network Variables - ÖNEMLİ: Default değerler set et
        private NetworkVariable<int> deliveredCount = new NetworkVariable<int>(0);
        private NetworkVariable<BoxInfo.BoxType> networkRequestedBoxType = new NetworkVariable<BoxInfo.BoxType>(BoxInfo.BoxType.Red);
        private NetworkVariable<int> networkRequiredCargo = new NetworkVariable<int>(1);
        private NetworkVariable<bool> isComplete = new NetworkVariable<bool>(false);
        private NetworkVariable<bool> isEntering = new NetworkVariable<bool>(true);

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

        // ÖNEMLİ: Pre-initialize için değişken ekle
        private bool hasPreInitialized = false;

        // ÖNEMLİ: Hangar tracking
        [HideInInspector] public int hangarIndex = 0; // Bu kamyonun hangi hangardan geldiği

        // ÖNEMLİ: Spawn öncesi initialize metodu
        public void PreInitialize(BoxInfo.BoxType reqType, int reqAmount)
        {
            requestedBoxType = reqType;
            requiredCargo = reqAmount;
            hasPreInitialized = true;

            Debug.Log($"PreInitialize called: {reqType}, {reqAmount}");
        }

        public override void OnNetworkSpawn()
        {
            Debug.Log($"Truck OnNetworkSpawn - IsServer: {IsServer}, HasPreInit: {hasPreInitialized}");

            // Network variable subscription'ları ekle
            deliveredCount.OnValueChanged += OnDeliveredCountChanged;
            networkRequestedBoxType.OnValueChanged += OnRequestedBoxTypeChanged;
            networkRequiredCargo.OnValueChanged += OnRequiredCargoChanged;
            isComplete.OnValueChanged += OnIsCompleteChanged;
            isEntering.OnValueChanged += OnIsEnteringChanged;

            SetupTriggerCollider();

            // ÖNEMLİ: Pre-initialized değerleri kullan
            if (hasPreInitialized)
            {
                UpdateUIText();
                SetTruckColors();
            }

            // Client için başlangıç değerlerini güncelle
            if (!IsServer)
            {
                // Eğer pre-init değerleri yoksa network variable'lardan al
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
            // Unsubscribe from network variable changes
            deliveredCount.OnValueChanged -= OnDeliveredCountChanged;
            networkRequestedBoxType.OnValueChanged -= OnRequestedBoxTypeChanged;
            networkRequiredCargo.OnValueChanged -= OnRequiredCargoChanged;
            isComplete.OnValueChanged -= OnIsCompleteChanged;
            isEntering.OnValueChanged -= OnIsEnteringChanged;
        }

        [ServerRpc]
        public void InitializeServerRpc(BoxInfo.BoxType reqType, int reqAmount)
        {
            Debug.Log($"InitializeServerRpc called: {reqType}, {reqAmount}");

            networkRequestedBoxType.Value = reqType;
            networkRequiredCargo.Value = reqAmount;
            deliveredCount.Value = 0;
            isComplete.Value = false;
            isEntering.Value = true;

            // Update local values
            requestedBoxType = reqType;
            requiredCargo = reqAmount;

            // Update visuals on all clients
            UpdateVisualsClientRpc(reqType, reqAmount);
        }

        [ClientRpc]
        private void UpdateVisualsClientRpc(BoxInfo.BoxType reqType, int reqAmount)
        {
            Debug.Log($"UpdateVisualsClientRpc called: {reqType}, {reqAmount}");

            requestedBoxType = reqType;
            requiredCargo = reqAmount;

            UpdateUIText();
            SetTruckColors();
        }

        // Network Variable Change Handlers
        private void OnDeliveredCountChanged(int previousValue, int newValue)
        {
            UpdateUIText();
        }

        private void OnRequestedBoxTypeChanged(BoxInfo.BoxType previousValue, BoxInfo.BoxType newValue)
        {
            Debug.Log($"OnRequestedBoxTypeChanged: {previousValue} -> {newValue}");
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
                // Truck became complete
                if (IsServer)
                {
                    StartCoroutine(ExitSequence());
                }
            }
        }

        private void OnIsEnteringChanged(bool previousValue, bool newValue)
        {
            // Handle entering state changes if needed
        }

        private void SetupTriggerCollider()
        {
            GameObject colliderObj = triggerColliderObject != null ? triggerColliderObject : gameObject;

            Collider col = colliderObj.GetComponent<Collider>();
            if (col == null)
            {
                Debug.LogError($"[NETWORK TRUCK] No Collider found on {colliderObj.name}!");
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

        private void StartEnterAnimation()
        {
            if (truckAnimator != null)
            {
                truckAnimator.SetBool("DoExit", false);
                StartCoroutine(WaitForEnterAnimationComplete());
            }
            else
            {
                isEntering.Value = false;
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
            }
        }

        private void UpdateUIText()
        {
            if (truckText != null)
            {
                truckText.text = $"{requestedBoxType}: {deliveredCount.Value}/{requiredCargo}";
                Debug.Log($"UI Updated: {truckText.text}");
            }
        }

        private void SetTruckColors()
        {
            Color targetColor = GetColorForBoxType(requestedBoxType);
            Debug.Log($"Setting truck colors to: {requestedBoxType} ({targetColor})");

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

        [ServerRpc(RequireOwnership = false)]
        public void HandleDeliveryServerRpc(BoxInfo.BoxType boxType, bool isFull)
        {
            if (isComplete.Value || isEntering.Value)
                return;

            if (isFull && boxType == networkRequestedBoxType.Value)
            {
                // Correct box
                deliveredCount.Value++;

                // Add money (assuming MoneySystem is also networked)
                if (MoneySystem.Instance != null)
                {
                    MoneySystem.Instance.AddMoney(rewardPerBox);
                }

                if (deliveredCount.Value >= networkRequiredCargo.Value)
                {
                    isComplete.Value = true;
                }
            }
            else if (isFull)
            {
                // Incorrect box
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
            yield return new WaitForSeconds(exitDelay);
            StartExitAnimation();
        }

        private void StartExitAnimation()
        {
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

            CompleteTruckExit();
        }

        private void CompleteTruckExit()
        {
            if (IsServer)
            {
                if (TruckSpawner.Instance != null)
                {
                    // Hangar index'i kullanarak doğru hangarı bilgilendir
                    TruckSpawner.Instance.OnTruckDestroyed(hangarIndex);
                }

                GetComponent<NetworkObject>().Despawn();
            }
        }
    }
}