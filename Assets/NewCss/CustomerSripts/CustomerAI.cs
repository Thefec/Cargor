using System;
using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Linq;
using Unity.Netcode;
using Random = UnityEngine.Random;

namespace NewCss
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(SphereCollider))]
    public class CustomerAI : NetworkBehaviour
    {
        [Header("Player Reference")] public PlayerMovement cachedPlayerMovement;

        [Header("Prefab Mode")] public bool isPrefabMode = false;

        private Animator animator;

        [Header("Wait Bar")] public WaitBar waitBar;

        [Header("Canvas Settings")] public Canvas waitCanvas;
        public bool hideCanvasUntilTimer = true;

        [Header("Wait Time Settings")]
        public float minWaitTime = 10f;
        public float maxWaitTime = 20f;
        private float actualWaitTime;

        public float interactionTime = 5f;

        [Header("Interaction Settings")] public float interactionRange = 2f;
        private bool playerInRange = false;

        // Etkileşime giren oyuncunun ID'sini sakla
        private ulong interactingPlayerId = ulong.MaxValue;
        private PlayerMovement interactingPlayer = null;

        [Header("Products & Table")] public GameObject[] productPrefabs;

        [Header("Tables")] public DisplayTable targetTable;

        private NavMeshAgent navAgent;
        private Vector3 queueTarget;
        private int targetQueueIndex = -1;
        private bool hasInteracted = false;
        private GameObject placedProduct;
        private bool isInInteraction = false;
        private bool hasTimedOut = false;
        private bool waitTimeStarted = false;

        [Header("Item Detection")]
        public LayerMask itemLayerMask = -1;
        public float detectionRadius = 1.5f;

        [Header("Manager & Points")] public CustomerManager manager;
        public Transform exitPoint;
        [Header("Tables")] public DisplayTable[] targetTables;
        public DisplayTable dropOffTable;

        // Network Variables for synchronization
        private NetworkVariable<bool> networkWaitTimeStarted = new NetworkVariable<bool>(false);
        private NetworkVariable<bool> networkIsInInteraction = new NetworkVariable<bool>(false);
        private NetworkVariable<float> networkWaitBarTime = new NetworkVariable<float>(0f);
        private NetworkVariable<bool> networkShowCanvas = new NetworkVariable<bool>(false);
        private NetworkVariable<float> networkAnimatorSpeed = new NetworkVariable<float>(0f);
        private NetworkVariable<int> networkState = new NetworkVariable<int>(0);

        private enum State
        {
            MovingToQueue,
            WaitingInQueue,
            Service,
            WaitingForPickup,
            Exiting
        }

        private State state = State.MovingToQueue;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // Subscribe to network variable changes on clients
            if (!IsServer)
            {
                networkWaitTimeStarted.OnValueChanged += OnWaitTimeStartedChanged;
                networkIsInInteraction.OnValueChanged += OnInteractionStateChanged;
                networkWaitBarTime.OnValueChanged += OnWaitBarTimeChanged;
                networkShowCanvas.OnValueChanged += OnShowCanvasChanged;
                networkAnimatorSpeed.OnValueChanged += OnAnimatorSpeedChanged;
                networkState.OnValueChanged += OnStateChanged;
            }

            // Initialize components for all clients
            InitializeComponents();

            // Initialize random values on server
            if (IsServer)
            {
                actualWaitTime = Random.Range(minWaitTime, maxWaitTime);
            }
        }

        void Start()
        {
            if (!IsSpawned) return;
            InitializeComponents();
        }

        private void InitializeComponents()
        {
            navAgent = GetComponent<NavMeshAgent>();
            animator = GetComponent<Animator>();

            SphereCollider sc = GetComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = interactionRange;

            if (waitBar == null)
            {
                waitBar = GetComponentInChildren<WaitBar>();
                if (waitBar == null)
                {
                    waitBar = GetComponent<WaitBar>();
                }
            }

            if (waitCanvas == null)
            {
                waitCanvas = GetComponentInChildren<Canvas>();
            }

            if (hideCanvasUntilTimer && waitCanvas != null)
            {
                waitCanvas.gameObject.SetActive(false);
            }

            if (isPrefabMode)
            {
                if (navAgent != null)
                    navAgent.enabled = false;
                if (waitCanvas != null)
                    waitCanvas.gameObject.SetActive(false);
                return;
            }
        }

        void Update()
        {
            if (isPrefabMode) return;

            // Only server handles logic
            if (IsServer)
            {
                UpdateAnimator();
                CheckWaitTimeExpired();

                switch (state)
                {
                    case State.MovingToQueue: MoveToQueue(); break;
                    case State.WaitingInQueue: CheckServiceStart(); break;
                    case State.Service: HandleService(); break;
                    case State.WaitingForPickup: break; // Coroutine ile kontrol ediliyor
                    case State.Exiting: ExitScene(); break;
                }
            }

            // Client tarafında kendi input kontrolü - sadece kendisi range içindeyse
            if (IsOwner && state == State.Service && !hasInteracted && playerInRange)
            {
                if (Input.GetKeyDown(KeyCode.E))
                {
                    // Server'a bu client'ın ID'si ile etkileşim isteği gönder
                    RequestInteractionServerRpc(NetworkManager.Singleton.LocalClientId);
                }
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestInteractionServerRpc(ulong requestingPlayerId, ServerRpcParams rpcParams = default)
        {
            // Gönderen client'ın ID'sini al
            ulong senderClientId = rpcParams.Receive.SenderClientId;

            // Güvenlik kontrolü - gönderen kendi ID'sini mi gönderiyor?
            if (senderClientId != requestingPlayerId)
            {
                Debug.LogWarning($"Client {senderClientId} tried to interact as {requestingPlayerId}!");
                return;
            }

            // Server tarafında etkileşimi kontrol et
            if (state == State.Service && !hasInteracted && !isInInteraction)
            {
                // Etkileşime giren oyuncuyu bul ve doğrula
                if (ValidatePlayerInRange(senderClientId))
                {
                    StartInteraction(senderClientId);
                }
            }
        }

        private bool ValidatePlayerInRange(ulong playerId)
        {
            // Range içindeki tüm oyuncuları kontrol et
            Collider[] colliders = Physics.OverlapSphere(transform.position, interactionRange);

            foreach (var col in colliders)
            {
                if (col.CompareTag("Character"))
                {
                    NetworkObject netObj = col.GetComponent<NetworkObject>();
                    if (netObj != null && netObj.OwnerClientId == playerId)
                    {
                        // Bu oyuncunun PlayerMovement'ını kaydet
                        interactingPlayer = col.GetComponent<PlayerMovement>();
                        if (interactingPlayer == null)
                            interactingPlayer = col.GetComponentInChildren<PlayerMovement>();
                        if (interactingPlayer == null)
                            interactingPlayer = col.GetComponentInParent<PlayerMovement>();

                        return true;
                    }
                }
            }

            return false;
        }

        private void CheckWaitTimeExpired()
        {
            if (isInInteraction || hasTimedOut || state == State.Exiting || hasInteracted || state == State.WaitingForPickup)
                return;

            if (waitTimeStarted && waitBar != null && waitBar.GetRemainingTime() <= 0)
            {
                TimeUp();
            }
        }

        private void TimeUp()
        {
            if (hasTimedOut || state == State.Exiting) return;

            hasTimedOut = true;

            if (isInInteraction)
            {
                StopAllCoroutines();
                isInInteraction = false;
                networkIsInInteraction.Value = false;

                // Etkileşimdeki oyuncunun kilidini aç
                if (interactingPlayerId != ulong.MaxValue)
                {
                    UnlockSpecificPlayerClientRpc(interactingPlayerId);
                    interactingPlayerId = ulong.MaxValue;
                    interactingPlayer = null;
                }
            }

            networkShowCanvas.Value = false;
            if (waitBar != null) waitBar.HideBar();
            if (waitCanvas != null) waitCanvas.gameObject.SetActive(false);

            if (!hasInteracted && PrestigeManager.Instance != null)
                PrestigeManager.Instance.ModifyPrestige(-0.03f);

            TransitionToExit();
        }

        private void TransitionToExit()
        {
            StopAllCoroutines();

            state = State.Exiting;
            networkState.Value = (int)State.Exiting;

            if (manager != null)
                manager.NotifyCustomerDone(this);

            if (navAgent != null && exitPoint != null)
                navAgent.SetDestination(exitPoint.position);
        }

        private void UpdateAnimator()
        {
            float targetSpeed = 0f;
            if (state == State.MovingToQueue || state == State.Exiting)
                targetSpeed = navAgent.velocity.magnitude;

            float normalized = navAgent.speed > 0f ? targetSpeed / navAgent.speed : 0f;

            networkAnimatorSpeed.Value = normalized;

            if (animator != null)
                animator.SetFloat("Speed", normalized);
        }

        public void SetQueueTarget(Vector3 target, int queueIndex)
        {
            if (isPrefabMode) return;

            queueTarget = target;
            targetQueueIndex = queueIndex;
            if (navAgent == null) navAgent = GetComponent<NavMeshAgent>();
            navAgent.SetDestination(queueTarget);

            state = State.MovingToQueue;
            networkState.Value = (int)State.MovingToQueue;
        }

        private void MoveToQueue()
        {
            if (!navAgent.pathPending && navAgent.remainingDistance <= navAgent.stoppingDistance + 0.1f)
            {
                state = State.WaitingInQueue;
                networkState.Value = (int)State.WaitingInQueue;

                if (!waitTimeStarted)
                    StartWaitTime();
            }
        }

        private void StartWaitTime()
        {
            waitTimeStarted = true;
            hasTimedOut = false;
            hasInteracted = false;
            isInInteraction = false;

            networkWaitTimeStarted.Value = true;
            networkShowCanvas.Value = true;
            networkWaitBarTime.Value = actualWaitTime;
            networkIsInInteraction.Value = false;

            if (hideCanvasUntilTimer && waitCanvas != null)
                waitCanvas.gameObject.SetActive(true);
            if (waitBar != null)
                waitBar.StartWaitBar(actualWaitTime);
        }

        private void CheckServiceStart()
        {
            bool canStartService = false;

            if (manager != null && manager.IsFirstInQueue(this))
            {
                canStartService = true;
            }

            if (canStartService)
            {
                BeginService();
            }
        }

        private void BeginService()
        {
            state = State.Service;
            networkState.Value = (int)State.Service;
        }

        private void HandleService()
        {
            // Server sadece state'i yönetir, input client'lardan gelir
            // CheckProductPickup kaldırıldı - artık coroutine ile yönetiliyor
        }

        private void StartInteraction(ulong playerId = ulong.MaxValue)
        {
            if (hasInteracted || isInInteraction) return;

            // Etkileşime giren oyuncunun ID'sini kaydet
            if (playerId != ulong.MaxValue)
            {
                interactingPlayerId = playerId;
            }

            hasInteracted = true;
            isInInteraction = true;

            networkIsInInteraction.Value = true;
            networkWaitBarTime.Value = interactionTime;

            if (waitBar != null)
                waitBar.StartWaitBar(interactionTime);

            // Sadece etkileşime giren oyuncuyu kilitle
            if (interactingPlayerId != ulong.MaxValue)
            {
                LockSpecificPlayerClientRpc(interactingPlayerId);
            }

            StartCoroutine(InteractionTimer());
        }

        private IEnumerator InteractionTimer()
        {
            yield return new WaitForSeconds(interactionTime);
            CompleteInteraction();
        }

        private void CompleteInteraction()
        {
            if (hasTimedOut) return;

            isInInteraction = false;
            networkIsInInteraction.Value = false;
            networkShowCanvas.Value = false;

            if (waitBar != null)
            {
                waitBar.HideBar();
            }
            if (waitCanvas != null) waitCanvas.gameObject.SetActive(false);

            waitTimeStarted = false;
            networkWaitTimeStarted.Value = false;

            placedProduct = PlaceOnDropOffTableAsChild();
            if (placedProduct != null)
            {
                if (PrestigeManager.Instance != null)
                    PrestigeManager.Instance.ModifyPrestige(0.05f);

                StartCoroutine(WaitForProductPickupCoroutine());
            }
            else
            {
                if (PrestigeManager.Instance != null)
                    PrestigeManager.Instance.ModifyPrestige(-0.03f);

                TransitionToExit();
            }

            // Etkileşimdeki oyuncunun kilidini aç
            if (interactingPlayerId != ulong.MaxValue)
            {
                UnlockSpecificPlayerClientRpc(interactingPlayerId);
                interactingPlayerId = ulong.MaxValue;
                interactingPlayer = null;
            }
        }

        [ClientRpc]
        private void LockSpecificPlayerClientRpc(ulong targetPlayerId)
        {
            // Sadece hedef player kendini kilitler
            if (NetworkManager.Singleton.LocalClientId == targetPlayerId)
            {
                var playerObj = GameObject.FindWithTag("Character");
                if (playerObj != null)
                {
                    var netObj = playerObj.GetComponent<NetworkObject>();
                    if (netObj != null && netObj.IsOwner)
                    {
                        var playerMovement = playerObj.GetComponent<PlayerMovement>();
                        if (playerMovement != null)
                        {
                            playerMovement.LockMovement(true);
                            playerMovement.LockAllInteractions(true);
                        }
                    }
                }
            }
        }

        [ClientRpc]
        private void UnlockSpecificPlayerClientRpc(ulong targetPlayerId)
        {
            // Sadece hedef player kilidini açar
            if (NetworkManager.Singleton.LocalClientId == targetPlayerId)
            {
                var playerObj = GameObject.FindWithTag("Character");
                if (playerObj != null)
                {
                    var netObj = playerObj.GetComponent<NetworkObject>();
                    if (netObj != null && netObj.IsOwner)
                    {
                        var playerMovement = playerObj.GetComponent<PlayerMovement>();
                        if (playerMovement != null)
                        {
                            playerMovement.LockMovement(false);
                            playerMovement.LockAllInteractions(false);
                        }
                    }
                }
            }
        }

        private IEnumerator WaitForProductPickupCoroutine()
        {
            state = State.WaitingForPickup;
            networkState.Value = (int)State.WaitingForPickup;

            yield return null;

            while (placedProduct != null && !hasTimedOut)
            {
                if (placedProduct == null)
                {
                    break;
                }

                if (placedProduct.transform.parent == null ||
                    (dropOffTable != null && !IsChildOfDropOffTable(placedProduct)))
                {
                    placedProduct = null;
                    break;
                }

                yield return new WaitForSeconds(0.1f);
            }

            TransitionToExit();
        }

        private bool IsChildOfDropOffTable(GameObject product)
        {
            if (dropOffTable == null || product == null) return false;

            Transform parent = product.transform.parent;
            while (parent != null)
            {
                if (parent == dropOffTable.transform) return true;
                if (dropOffTable.slotPoints != null)
                {
                    foreach (Transform slot in dropOffTable.slotPoints)
                    {
                        if (parent == slot) return true;
                    }
                }
                parent = parent.parent;
            }
            return false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Character"))
            {
                // Sadece owner olan player için playerInRange'i true yap
                NetworkObject netObj = other.GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsOwner)
                {
                    playerInRange = true;
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Character"))
            {
                NetworkObject netObj = other.GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsOwner)
                {
                    playerInRange = false;
                }
            }
        }

        private GameObject PlaceOnDropOffTableAsChild()
        {
            if (dropOffTable == null || productPrefabs == null || productPrefabs.Length == 0)
                return null;

            int index = Random.Range(0, productPrefabs.Length);

            Vector3 spawnPos;
            Quaternion spawnRot;

            if (dropOffTable.slotPoints != null && dropOffTable.slotPoints.Length > dropOffTable.ItemCount)
            {
                Transform targetSlot = dropOffTable.slotPoints[dropOffTable.ItemCount];
                spawnPos = targetSlot.position;
                spawnRot = targetSlot.rotation;
            }
            else
            {
                spawnPos = dropOffTable.transform.position + Vector3.up * 0.5f;
                spawnRot = Quaternion.identity;
            }

            var product = Instantiate(productPrefabs[index], spawnPos, spawnRot);

            NetworkObject networkObject = product.GetComponent<NetworkObject>();
            if (networkObject != null)
            {
                networkObject.Spawn();
            }

            if (dropOffTable.slotPoints != null && dropOffTable.slotPoints.Length > dropOffTable.ItemCount)
            {
                product.transform.SetParent(dropOffTable.slotPoints[dropOffTable.ItemCount], true);
            }
            else
            {
                product.transform.SetParent(dropOffTable.transform, true);
            }

            dropOffTable.PlaceItemInstance(product);

            return product;
        }

        private void ExitScene()
        {
            if (!navAgent.pathPending && navAgent.remainingDistance <= navAgent.stoppingDistance + 0.1f)
            {
                if (IsServer && NetworkObject != null)
                    NetworkObject.Despawn();
                else
                    Destroy(gameObject);
            }
        }

        public int GetTargetQueueIndex() => targetQueueIndex;

        public void SetPrefabMode(bool prefabMode)
        {
            isPrefabMode = prefabMode;

            if (isPrefabMode)
            {
                if (navAgent != null)
                    navAgent.enabled = false;
                if (waitCanvas != null)
                    waitCanvas.gameObject.SetActive(false);
            }
            else
            {
                if (navAgent != null)
                    navAgent.enabled = true;
            }
        }

        // Network variable change handlers
        private void OnWaitTimeStartedChanged(bool previousValue, bool newValue)
        {
            if (!IsServer && newValue)
            {
                if (hideCanvasUntilTimer && waitCanvas != null)
                    waitCanvas.gameObject.SetActive(true);
            }
        }

        private void OnInteractionStateChanged(bool previousValue, bool newValue)
        {
            if (!IsServer)
            {
                isInInteraction = newValue;
            }
        }

        private void OnWaitBarTimeChanged(float previousValue, float newValue)
        {
            if (!IsServer && waitBar != null && newValue > 0)
            {
                waitBar.StartWaitBar(newValue);
            }
        }

        private void OnShowCanvasChanged(bool previousValue, bool newValue)
        {
            if (!IsServer && waitCanvas != null)
            {
                waitCanvas.gameObject.SetActive(newValue);
            }
        }

        private void OnAnimatorSpeedChanged(float previousValue, float newValue)
        {
            if (!IsServer && animator != null)
            {
                animator.SetFloat("Speed", newValue);
            }
        }

        private void OnStateChanged(int previousValue, int newValue)
        {
            if (!IsServer)
            {
                state = (State)newValue;
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = playerInRange ? Color.green : Color.red;
            Gizmos.DrawWireSphere(transform.position, interactionRange);

            string debugInfo = $"State: {state}\nInteracted: {hasInteracted}\nInRange: {playerInRange}";
            if (IsServer)
            {
                debugInfo += $"\nWait Time: {actualWaitTime:F1}s";
                if (placedProduct != null)
                    debugInfo += $"\nPlaced Product: {placedProduct.name}";
                if (interactingPlayerId != ulong.MaxValue)
                    debugInfo += $"\nInteracting Player ID: {interactingPlayerId}";
            }

#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2, debugInfo);
#endif
        }
    }
}