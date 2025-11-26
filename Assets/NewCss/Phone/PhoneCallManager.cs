using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using Unity.Netcode;

namespace NewCss
{
    public class PhoneCallManager : NetworkBehaviour
    {
        public static PhoneCallManager Instance { get; private set; }

        [Header("Phone Call Settings")]
        [SerializeField] private float callChance = 0.5f;
        [SerializeField] private float callDuration = 30f;
        [SerializeField] private float callAnswerDuration = 10f;
        [SerializeField] private float prestigeReward = 0.02f;
        [SerializeField] private float prestigePenalty = 0.05f;
        [SerializeField] private int startCallingHour = 8;

        [Header("UI Elements")]
        [SerializeField] private GameObject phoneCallUI;
        [SerializeField] private Button answerButton;
        [SerializeField] private TextMeshProUGUI callInfoText;
        [SerializeField] private AudioSource phoneRingSound;
        [SerializeField] private AudioSource conversationSound;

        [Header("Wait Bar System")]
        [SerializeField] private PhoneWaitBar phoneWaitBar;
        [SerializeField] private Canvas phoneCanvas;
        [SerializeField] private bool hideCanvasUntilCall = true;

        [Header("Customer Support Event Modifier")]
        [SerializeField] private float customerSupportModifier = 1.3f;

        [Header("Interaction Settings")]
        [SerializeField] private Collider phoneCollider;
        [SerializeField] private string playerTag = "Character";

        // Network Variables
        private NetworkVariable<bool> networkIsCallActive = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private NetworkVariable<bool> networkIsCallAnswered = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private NetworkVariable<bool> networkIsInPhoneConversation = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private NetworkVariable<bool> networkCustomerSupportActive = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private NetworkVariable<ulong> networkCurrentCallOwner = new NetworkVariable<ulong>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private bool[] hourlyCallChecked;
        private int lastCheckedHour = -1;
        private Coroutine callCoroutine;
        private bool playerInPhoneArea = false;
        private bool isNetworkReady = false;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                if (IsServer)
                    GetComponent<NetworkObject>().Despawn();
                return;
            }

            isNetworkReady = true;

            networkIsCallActive.OnValueChanged += OnCallActiveChanged;
            networkIsCallAnswered.OnValueChanged += OnCallAnsweredChanged;
            networkIsInPhoneConversation.OnValueChanged += OnConversationChanged;
            networkCustomerSupportActive.OnValueChanged += OnCustomerSupportChanged;
            networkCurrentCallOwner.OnValueChanged += OnCallOwnerChanged;

            UpdateUIState();
            InitializeSystem();
            SetupPhoneCollider();
        }

        public override void OnNetworkDespawn()
        {
            isNetworkReady = false;

            if (networkIsCallActive != null)
                networkIsCallActive.OnValueChanged -= OnCallActiveChanged;
            if (networkIsCallAnswered != null)
                networkIsCallAnswered.OnValueChanged -= OnCallAnsweredChanged;
            if (networkIsInPhoneConversation != null)
                networkIsInPhoneConversation.OnValueChanged -= OnConversationChanged;
            if (networkCustomerSupportActive != null)
                networkCustomerSupportActive.OnValueChanged -= OnCustomerSupportChanged;
            if (networkCurrentCallOwner != null)
                networkCurrentCallOwner.OnValueChanged -= OnCallOwnerChanged;

            if (Instance == this)
            {
                Instance = null;
            }

            base.OnNetworkDespawn();
        }

        void Awake()
        {
        }

        void Start()
        {
            var netObj = GetComponent<NetworkObject>();
            if (netObj != null && IsServer && !netObj.IsSpawned)
            {
                netObj.Spawn(true);
            }
            DayCycleManager.OnNewDay -= OnNewDay;
            DayCycleManager.OnNewDay += OnNewDay;
        }

        void OnDestroy()
        {
            DayCycleManager.OnNewDay -= OnNewDay;

            if (Instance == this)
            {
                Instance = null;
            }
        }

        #region Network Variable Change Handlers

        private void OnCallActiveChanged(bool previousValue, bool newValue)
        {
            UpdateUIState();
            if (newValue && phoneRingSound != null)
            {
                phoneRingSound.Play();
            }
            else if (!newValue && phoneRingSound != null)
            {
                phoneRingSound.Stop();
            }
        }

        private void OnCallAnsweredChanged(bool previousValue, bool newValue)
        {
            UpdateUIState();
            if (newValue && phoneRingSound != null)
            {
                phoneRingSound.Stop();
            }

            if (newValue && conversationSound != null)
            {
                conversationSound.Play();
            }
        }

        /// <summary>
        /// ✅ FIX: CustomerAI pattern - Sadece telefonu cevaplayan client lock'lanır
        /// </summary>
        private void OnConversationChanged(bool previousValue, bool newValue)
        {
            UpdateUIState();

            if (!newValue && conversationSound != null)
            {
                conversationSound.Stop();
            }

            // ✅ FIX: Sadece telefonu cevaplayan client lock'lanır
            if (NetworkManager.Singleton == null) return;

            ulong localClientId = NetworkManager.Singleton.LocalClientId;
            ulong callOwner = networkCurrentCallOwner.Value;

            Debug.Log($"[PhoneCall] Client {localClientId} - Conversation: {newValue}, CallOwner: {callOwner}");

            // ✅ SADECE telefonu cevaplayan oyuncu için lock/unlock
            if (callOwner == localClientId)
            {
                // ✅ newValue = true → LOCK, false → UNLOCK
                LockLocalPlayerMovement(newValue);
                Debug.Log($"[PhoneCall] Client {localClientId} - Movement locked: {newValue}");
            }
        }

        private void OnCustomerSupportChanged(bool previousValue, bool newValue)
        {
            Debug.Log($"Customer support changed: {newValue}");
        }

        private void OnCallOwnerChanged(ulong previousValue, ulong newValue)
        {
            UpdateUIState();
        }

        #endregion

        #region Player Movement Lock (CustomerAI Pattern)

        /// <summary>
        /// ✅ YENİ: Her client kendi player'ını bulup lock'lar (CustomerAI pattern)
        /// </summary>
        private void LockLocalPlayerMovement(bool locked)
        {
            if (NetworkManager.Singleton == null) return;

            ulong localClientId = NetworkManager.Singleton.LocalClientId;

            // ✅ Local player'ı bul
            foreach (var netObj in FindObjectsOfType<NetworkObject>())
            {
                if (netObj.IsOwner && netObj.OwnerClientId == localClientId && netObj.CompareTag(playerTag))
                {
                    var playerMovement = netObj.GetComponent<PlayerMovement>();
                    if (playerMovement == null)
                        playerMovement = netObj.GetComponentInChildren<PlayerMovement>();
                    if (playerMovement == null)
                        playerMovement = netObj.GetComponentInParent<PlayerMovement>();

                    if (playerMovement != null)
                    {
                        Debug.Log($"[PhoneCall] Client {localClientId} - Locking movement: {locked}");
                        playerMovement.LockMovement(locked);
                        playerMovement.LockAllInteractions(locked);
                        return;
                    }
                }
            }

            Debug.LogWarning($"[PhoneCall] Client {localClientId} - Could not find local player!");
        }

        #endregion

        #region UI Management

        private void UpdateUIState()
        {
            if (!isNetworkReady) return;

            bool isCallActive = networkIsCallActive.Value;
            bool isCallAnswered = networkIsCallAnswered.Value;
            bool isInConversation = networkIsInPhoneConversation.Value;
            bool isCallOwner = NetworkManager.Singleton != null &&
                              networkCurrentCallOwner.Value == NetworkManager.Singleton.LocalClientId;

            if (hideCanvasUntilCall && phoneCanvas != null)
            {
                phoneCanvas.gameObject.SetActive(isCallActive || isInConversation);
            }

            if (phoneCallUI != null)
            {
                phoneCallUI.SetActive(isCallActive || isInConversation);
            }

            if (callInfoText != null)
            {
                if (isInConversation)
                {
                    if (isCallOwner)
                    {
                        callInfoText.text = "Telefonda konuşuyor...";
                    }
                    else
                    {
                        callInfoText.text = "Başka bir oyuncu telefonda konuşuyor...";
                    }
                }
                else if (isCallActive)
                {
                    callInfoText.text = playerInPhoneArea
                        ? "Telefon çalıyor! E tuşuna basarak cevapla!"
                        : "Telefon çalıyor! Telefona yaklaşıp E tuşuna bas!";
                }
            }
        }

        #endregion

        void SetupPhoneCollider()
        {
            if (phoneCollider == null)
            {
                phoneCollider = GetComponent<Collider>() ?? GetComponentInChildren<Collider>();
            }

            if (phoneCollider != null)
            {
                phoneCollider.isTrigger = true;
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.CompareTag(playerTag))
            {
                var networkObject = other.GetComponent<NetworkObject>();
                if (networkObject != null && networkObject.IsOwner)
                {
                    playerInPhoneArea = true;
                    UpdateUIState();
                }
            }
        }

        void OnTriggerExit(Collider other)
        {
            if (other.gameObject.CompareTag(playerTag))
            {
                var networkObject = other.GetComponent<NetworkObject>();
                if (networkObject != null && networkObject.IsOwner)
                {
                    playerInPhoneArea = false;
                    UpdateUIState();
                }
            }
        }

        void InitializeSystem()
        {
            hourlyCallChecked = new bool[24];

            if (phoneCallUI != null)
                phoneCallUI.SetActive(false);

            if (hideCanvasUntilCall && phoneCanvas != null)
                phoneCanvas.gameObject.SetActive(false);

            if (answerButton != null)
                answerButton.onClick.AddListener(AnswerCall);

            if (phoneWaitBar == null)
                phoneWaitBar = GetComponentInChildren<PhoneWaitBar>();

            if (phoneCanvas == null)
                phoneCanvas = GetComponentInChildren<Canvas>();
        }

        void Update()
        {
            if (!isNetworkReady) return;

            if (IsServer)
            {
                ServerUpdate();
            }

            ClientUpdate();
        }

        void ServerUpdate()
        {
            if (!networkIsCallActive.Value && !networkIsInPhoneConversation.Value)
            {
                CheckForPhoneCall();
            }
            else if (networkIsCallActive.Value && !networkIsCallAnswered.Value)
            {
                CheckCallTimeout();
            }
            else if (networkIsInPhoneConversation.Value)
            {
                CheckConversationTimeout();
            }
        }

        void ClientUpdate()
        {
            if (networkIsCallActive.Value && !networkIsCallAnswered.Value)
            {
                if (playerInPhoneArea && Input.GetKeyDown(KeyCode.E))
                {
                    AnswerCall();
                }
            }
        }

        void CheckForPhoneCall()
        {
            if (DayCycleManager.Instance == null) return;

            int currentHour = DayCycleManager.Instance.CurrentHour;

            if (currentHour < 0 || currentHour >= 24) return;

            if (currentHour < startCallingHour)
            {
                Debug.Log($"Phone Call Check - Too early: Hour {currentHour} (calls start at {startCallingHour})");
                return;
            }

            if (currentHour == lastCheckedHour) return;

            lastCheckedHour = currentHour;

            if (hourlyCallChecked[currentHour]) return;
            hourlyCallChecked[currentHour] = true;

            float finalCallChance = callChance;
            if (networkCustomerSupportActive.Value)
            {
                finalCallChance *= customerSupportModifier;
                finalCallChance = Mathf.Min(finalCallChance, 1f);
            }

            float randomValue = Random.Range(0f, 1f);
            Debug.Log($"Phone Call Check - Hour: {currentHour}, Chance: {finalCallChance:F2}, Random: {randomValue:F2}, Start Hour: {startCallingHour}");

            if (randomValue <= finalCallChance)
            {
                Debug.Log($"Starting phone call at hour {currentHour}!");
                StartPhoneCallServer();
            }
        }

        void StartPhoneCallServer()
        {
            if (!IsServer || !isNetworkReady) return;
            if (networkIsCallActive.Value || networkIsInPhoneConversation.Value) return;

            Debug.Log("Phone call started on server");

            networkIsCallActive.Value = true;
            networkIsCallAnswered.Value = false;
            networkIsInPhoneConversation.Value = false;
            networkCurrentCallOwner.Value = 0;

            if (phoneWaitBar != null)
                phoneWaitBar.StartWaitBar(callDuration);

            OnCallStartedClientRpc();
        }

        void CheckCallTimeout()
        {
            if (phoneWaitBar != null && phoneWaitBar.GetRemainingTime() <= 0 && !networkIsCallAnswered.Value)
            {
                MissedCallServer();
            }
        }

        void CheckConversationTimeout()
        {
            if (phoneWaitBar != null && phoneWaitBar.GetRemainingTime() <= 0)
            {
                EndPhoneConversationServer();
            }
        }

        public void AnswerCall()
        {
            if (!isNetworkReady || !networkIsCallActive.Value || networkIsCallAnswered.Value || !playerInPhoneArea)
                return;

            AnswerCallServerRpc();
        }

        [ServerRpc(RequireOwnership = false)]
        private void AnswerCallServerRpc(ServerRpcParams rpcParams = default)
        {
            if (!networkIsCallActive.Value || networkIsCallAnswered.Value) return;

            ulong clientId = rpcParams.Receive.SenderClientId;

            networkIsCallAnswered.Value = true;
            networkIsInPhoneConversation.Value = true;
            networkCurrentCallOwner.Value = clientId;

            if (phoneWaitBar != null)
                phoneWaitBar.StartWaitBar(callAnswerDuration);

            OnCallAnsweredClientRpc(clientId);
        }

        void EndPhoneConversationServer()
        {
            if (!networkIsInPhoneConversation.Value) return;

            if (PrestigeManager.Instance != null && networkCurrentCallOwner.Value != 0)
            {
                GivePrestigeToPlayerClientRpc(networkCurrentCallOwner.Value, prestigeReward);
            }

            EndCallServer(false);
        }

        void MissedCallServer()
        {
            if (networkIsCallAnswered.Value) return;

            ApplyMissedCallPenaltyClientRpc();

            EndCallServer(true);
        }

        void EndCallServer(bool wasMissed)
        {
            networkIsCallActive.Value = false;
            networkIsCallAnswered.Value = false;
            networkIsInPhoneConversation.Value = false;
            networkCurrentCallOwner.Value = 0;

            if (phoneWaitBar != null)
                phoneWaitBar.HideBar();

            if (callCoroutine != null)
            {
                StopCoroutine(callCoroutine);
                callCoroutine = null;
            }

            OnCallEndedClientRpc(wasMissed);
        }

        void OnNewDay()
        {
            if (IsServer && isNetworkReady)
            {
                for (int i = 0; i < hourlyCallChecked.Length; i++)
                {
                    hourlyCallChecked[i] = false;
                }

                lastCheckedHour = -1;

                if (networkIsCallActive.Value || networkIsInPhoneConversation.Value)
                {
                    EndCallServer(false);
                }
            }
        }

        #region Client RPCs

        [ClientRpc]
        private void OnCallStartedClientRpc()
        {
            Debug.Log("Phone call started - ClientRPC");
        }

        [ClientRpc]
        private void OnCallAnsweredClientRpc(ulong answeringClientId)
        {
            Debug.Log($"Phone call answered by client {answeringClientId}");
        }

        [ClientRpc]
        private void OnCallEndedClientRpc(bool wasMissed)
        {
            Debug.Log($"Phone call ended - Missed: {wasMissed}");

            // ✅ SADECE telefonu cevaplayan client unlock edilir
            if (NetworkManager.Singleton == null) return;

            ulong localClientId = NetworkManager.Singleton.LocalClientId;
            ulong callOwner = networkCurrentCallOwner.Value;

            if (callOwner == localClientId)
            {
                Debug.Log($"[PhoneCall] Client {localClientId} - Unlocking movement after call end");
                LockLocalPlayerMovement(false);
            }
        }

        [ClientRpc]
        private void GivePrestigeToPlayerClientRpc(ulong targetClientId, float amount)
        {
            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.LocalClientId == targetClientId)
            {
                if (PrestigeManager.Instance != null)
                    PrestigeManager.Instance.AddPrestige(amount);
            }
        }

        [ClientRpc]
        private void ApplyMissedCallPenaltyClientRpc()
        {
            if (PrestigeManager.Instance != null)
                PrestigeManager.Instance.AddPrestige(-prestigePenalty);
        }

        #endregion

        #region Public Methods

        public void SetCustomerSupportActive(bool active)
        {
            if (!isNetworkReady) return;

            if (IsServer)
            {
                networkCustomerSupportActive.Value = active;
            }
            else
            {
                SetCustomerSupportActiveServerRpc(active);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void SetCustomerSupportActiveServerRpc(bool active)
        {
            networkCustomerSupportActive.Value = active;
        }

        [ContextMenu("Test Phone Call")]
        public void TestPhoneCall()
        {
            if (!isNetworkReady)
            {
                Debug.LogError("Network not ready for phone call test!");
                return;
            }

            if (IsServer)
            {
                Debug.Log("Testing phone call as server");
                StartPhoneCallServer();
            }
            else
            {
                Debug.Log("Requesting phone call test from server");
                TestPhoneCallServerRpc();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void TestPhoneCallServerRpc()
        {
            Debug.Log("Received phone call test request on server");
            StartPhoneCallServer();
        }

        #endregion

        #region Getters and Setters

        public void SetCallChance(float newChance) => callChance = Mathf.Clamp01(newChance);
        public void SetCallDuration(float newDuration) => callDuration = Mathf.Max(5f, newDuration);
        public void SetCallAnswerDuration(float newDuration) => callAnswerDuration = Mathf.Max(1f, newDuration);
        public void SetPrestigeReward(float newReward) => prestigeReward = Mathf.Max(0, newReward);
        public void SetPrestigePenalty(float newPenalty) => prestigePenalty = Mathf.Max(0, newPenalty);
        public bool IsPlayerInPhoneArea() => playerInPhoneArea;
        public bool IsCallActive() => isNetworkReady && networkIsCallActive.Value;
        public bool IsCallAnswered() => isNetworkReady && networkIsCallAnswered.Value;
        public bool IsInPhoneConversation() => isNetworkReady && networkIsInPhoneConversation.Value;
        public ulong GetCurrentCallOwner() => isNetworkReady ? networkCurrentCallOwner.Value : 0;

        #endregion
    }
}