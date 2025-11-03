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
        // Add this field near other settings
        [Header("Phone Call Settings")]
        [SerializeField] private float callChance = 0.5f;
        [SerializeField] private float callDuration = 30f;
        [SerializeField] private float callAnswerDuration = 10f;
        [SerializeField] private float prestigeReward = 0.02f;
        [SerializeField] private float prestigePenalty = 0.05f;
        [SerializeField] private int startCallingHour = 8; // Yeni: Aramaların başlama saati

       

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

        [Header("Player Reference")]
        [SerializeField] private PlayerMovement cachedPlayerMovement;

        [Header("Interaction Settings")]
        [SerializeField] private Collider phoneCollider;
        [SerializeField] private string playerTag = "Character";

        // Network Variables
        private NetworkVariable<bool> networkIsCallActive = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private NetworkVariable<bool> networkIsCallAnswered = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private NetworkVariable<bool> networkIsInPhoneConversation = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private NetworkVariable<bool> networkCustomerSupportActive = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private NetworkVariable<ulong> networkCurrentCallOwner = new NetworkVariable<ulong>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // Modify the local variables section
        private bool[] hourlyCallChecked;
        private int lastCheckedHour = -1; // Yeni: Son kontrol edilen saat
        private Coroutine callCoroutine;
        private bool playerInPhoneArea = false;
        private bool isNetworkReady = false;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // Singleton pattern - NetworkBehaviour için düzeltildi
            if (Instance == null)
            {
                Instance = this;
                // NetworkBehaviour için DontDestroyOnLoad kullanmayın!
            }
            else if (Instance != this)
            {
                // Eğer başka bir instance varsa, bu objeyi yok et
                if (IsServer)
                    GetComponent<NetworkObject>().Despawn();
                return;
            }

            isNetworkReady = true;

            // Subscribe to network variable changes
            networkIsCallActive.OnValueChanged += OnCallActiveChanged;
            networkIsCallAnswered.OnValueChanged += OnCallAnsweredChanged;
            networkIsInPhoneConversation.OnValueChanged += OnConversationChanged;
            networkCustomerSupportActive.OnValueChanged += OnCustomerSupportChanged;
            networkCurrentCallOwner.OnValueChanged += OnCallOwnerChanged;

            // Update UI based on current network state
            UpdateUIState();

            // Initialize system after network spawn
            InitializeSystem();
            CachePlayerMovement();
            SetupPhoneCollider();
        }

        public override void OnNetworkDespawn()
        {
            isNetworkReady = false;

            // Unsubscribe from network variable changes
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

            // Clear instance if this is the current instance
            if (Instance == this)
            {
                Instance = null;
            }

            base.OnNetworkDespawn();
        }

        void Awake()
        {
            // Singleton logic moved to OnNetworkSpawn for NetworkBehaviour
        }

        void Start()
        {
            var netObj = GetComponent<NetworkObject>();
            if (netObj != null && IsServer && !netObj.IsSpawned)
            {
                netObj.Spawn(true); // true -> destroyWithScene (opsiyonel)
            }
            DayCycleManager.OnNewDay -= OnNewDay; // Önce çıkar
            DayCycleManager.OnNewDay += OnNewDay; // Sonra ekle
        }

        void OnDestroy()
        {
            DayCycleManager.OnNewDay -= OnNewDay;

            // Clear instance if this is the current instance
            if (Instance == this)
            {
                Instance = null;
            }
        }

        #region Network Variable Change Handlers

        private void OnCallActiveChanged(bool previousValue, bool newValue)
        {
            UpdateUIState();
            if (newValue && phoneRingSound != null) // newValue == true (Arama aktif oldu)
            {
                phoneRingSound.Play(); // Sesi başlat
            }
            else if (!newValue && phoneRingSound != null) // newValue == false (Arama bitti/kaçırıldı)
            {
                phoneRingSound.Stop(); // Sesi durdur
            }
        }

        private void OnCallAnsweredChanged(bool previousValue, bool newValue)
        {
            UpdateUIState();
            if (newValue && phoneRingSound != null) // newValue == true (Arama cevaplandı)
            {
                phoneRingSound.Stop(); // Çalma sesini durdur
            }

            if (newValue && conversationSound != null) // Konuşma sesi başlat
            {
                conversationSound.Play();
            }
        }

        private void OnConversationChanged(bool previousValue, bool newValue)
        {
            UpdateUIState();

            // Konuşma bittiğinde sesi durdur
            if (!newValue && conversationSound != null)
            {
                conversationSound.Stop();
            }

            // Handle player locking for the call owner
            if (NetworkManager.Singleton != null &&
                (IsOwner || networkCurrentCallOwner.Value == NetworkManager.Singleton.LocalClientId))
            {
                LockPlayer(newValue);
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

        #region UI Management

        private void UpdateUIState()
        {
            if (!isNetworkReady) return;

            bool isCallActive = networkIsCallActive.Value;
            bool isCallAnswered = networkIsCallAnswered.Value;
            bool isInConversation = networkIsInPhoneConversation.Value;
            bool isCallOwner = NetworkManager.Singleton != null &&
                              networkCurrentCallOwner.Value == NetworkManager.Singleton.LocalClientId;

            // Update canvas visibility
            if (hideCanvasUntilCall && phoneCanvas != null)
            {
                phoneCanvas.gameObject.SetActive(isCallActive || isInConversation);
            }

            // Update phone call UI
            if (phoneCallUI != null)
            {
                phoneCallUI.SetActive(isCallActive || isInConversation);
            }

            // Update call info text
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

        private void CachePlayerMovement()
        {
            if (cachedPlayerMovement != null) return;

            var playerObject = GameObject.FindWithTag("Character");
            if (playerObject == null) return;

            cachedPlayerMovement = playerObject.GetComponent<PlayerMovement>() ??
                                   playerObject.GetComponentInChildren<PlayerMovement>() ??
                                   playerObject.GetComponentInParent<PlayerMovement>() ??
                                   FindObjectOfType<PlayerMovement>();
        }

        void Update()
        {
            if (!isNetworkReady) return;

            // Only server manages call logic
            if (IsServer)
            {
                ServerUpdate();
            }

            // Client input handling
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

            // Saat 8'den önce arama yapma
            if (currentHour < startCallingHour)
            {
                Debug.Log($"Phone Call Check - Too early: Hour {currentHour} (calls start at {startCallingHour})");
                return;
            }

            // Eğer bu saat zaten kontrol edildiyse, atla
            if (currentHour == lastCheckedHour) return;

            // Yeni saate geçildi, kontrolü yap
            lastCheckedHour = currentHour;

            // Eğer bu saat için zaten arama yapıldıysa, atla
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

            // Notify all clients that a call started
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

            // Notify all clients that call was answered
            OnCallAnsweredClientRpc(clientId);
        }

        void EndPhoneConversationServer()
        {
            if (!networkIsInPhoneConversation.Value) return;

            // Give prestige reward to the player who answered
            if (PrestigeManager.Instance != null && networkCurrentCallOwner.Value != 0)
            {
                GivePrestigeToPlayerClientRpc(networkCurrentCallOwner.Value, prestigeReward);
            }

            EndCallServer(false);
        }

        private void LockPlayer(bool locked)
        {
            if (cachedPlayerMovement == null)
                CachePlayerMovement();

            if (cachedPlayerMovement != null)
            {
                cachedPlayerMovement.LockMovement(locked);
                cachedPlayerMovement.LockAllInteractions(locked);
            }
        }

        void MissedCallServer()
        {
            if (networkIsCallAnswered.Value) return;

            // Apply penalty to all players
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

            // Notify all clients that call ended
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

                lastCheckedHour = -1; // Yeni gün için sıfırla

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
            LockPlayer(false);
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
        public void SetPlayerMovement(PlayerMovement playerMovement) => cachedPlayerMovement = playerMovement;
        public bool IsPlayerInPhoneArea() => playerInPhoneArea;
        public bool IsCallActive() => isNetworkReady && networkIsCallActive.Value;
        public bool IsCallAnswered() => isNetworkReady && networkIsCallAnswered.Value;
        public bool IsInPhoneConversation() => isNetworkReady && networkIsInPhoneConversation.Value;
        public ulong GetCurrentCallOwner() => isNetworkReady ? networkCurrentCallOwner.Value : 0;

        #endregion
    }
}