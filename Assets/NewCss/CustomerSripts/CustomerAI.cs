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
            WaitingForPickup,  // Customer waits until their child item is taken
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

            CachePlayerMovement();
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

        private void CachePlayerMovement()
        {
            var playerObject = GameObject.FindWithTag("Character");
            if (playerObject == null) return;

            cachedPlayerMovement = playerObject.GetComponent<PlayerMovement>();
            if (cachedPlayerMovement == null)
                cachedPlayerMovement = playerObject.GetComponentInChildren<PlayerMovement>();
            if (cachedPlayerMovement == null)
                cachedPlayerMovement = playerObject.GetComponentInParent<PlayerMovement>();
            if (cachedPlayerMovement == null)
                cachedPlayerMovement = FindObjectOfType<PlayerMovement>();
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
                    case State.WaitingForPickup: CheckProductPickup(); break;
                    case State.Exiting: ExitScene(); break;
                }
            }
            // Client tarafında da input kontrolü (sadece owner için)
            else if (state == State.Service && !hasInteracted && playerInRange)
            {
                HandleClientInteractionInput();
            }
        }

        // Client tarafında input kontrolü
        private void HandleClientInteractionInput()
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                RequestInteractionServerRpc();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestInteractionServerRpc()
        {
            // Server tarafında tekrar kontrol et
            if (state == State.Service && !hasInteracted && playerInRange && !isInInteraction)
            {
                StartInteraction();
            }
        }

        private void CheckWaitTimeExpired()
        {
            // Don't check timeout if customer has been successfully served or is waiting for pickup
            if (isInInteraction || hasTimedOut || state == State.Exiting || hasInteracted || state == State.WaitingForPickup) 
                return;
        
            // Only timeout customers who are still waiting to be served
            if (waitTimeStarted && waitBar != null && waitBar.GetRemainingTime() <= 0)
            {
                TimeUp();
            }
        }

        private void TimeUp()
        {
            if (hasTimedOut || state == State.Exiting) return;
    
            hasTimedOut = true;

            // Eğer etkileşim devam ediyorsa, önce onu bitir
            if (isInInteraction)
            {
                StopAllCoroutines(); // InteractionTimer'ı durdur
                isInInteraction = false;
                networkIsInInteraction.Value = false;
                LockPlayer(false);
            }

            // Canvas ve bar'ı gizle
            networkShowCanvas.Value = false;
            if (waitBar != null) waitBar.HideBar();
            if (waitCanvas != null) waitCanvas.gameObject.SetActive(false);

            // Prestige düşür
            if (!hasInteracted && PrestigeManager.Instance != null)
                PrestigeManager.Instance.ModifyPrestige(-0.03f);
    
            // Doğrudan exit'e geç
            TransitionToExit();
        }
        
        private void TransitionToExit()
        {
            // Tüm coroutine'ları durdur
            StopAllCoroutines();
    
            // State'i güncelle
            state = State.Exiting;
            networkState.Value = (int)State.Exiting;
    
            // Manager'a bildir
            if (manager != null)
                manager.NotifyCustomerDone(this);
    
            // Exit point'e git
            if (navAgent != null && exitPoint != null)
                navAgent.SetDestination(exitPoint.position);
        }

        private void UpdateAnimator()
        {
            float targetSpeed = 0f;
            if (state == State.MovingToQueue || state == State.Exiting)
                targetSpeed = navAgent.velocity.magnitude;

            float normalized = navAgent.speed > 0f ? targetSpeed / navAgent.speed : 0f;

            // Update network variable for animator speed
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

            // Update network variables
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
            // Server tarafında input kontrolü
            if (!hasInteracted && playerInRange && !isInInteraction)
            {
                if (Input.GetKeyDown(KeyCode.E))
                {
                    StartInteraction();
                }
            }
        }

        private void StartInteraction()
        {
            if (hasInteracted || isInInteraction) return;
            
            hasInteracted = true;
            isInInteraction = true;

            // Update network variables
            networkIsInInteraction.Value = true;
            networkWaitBarTime.Value = interactionTime;

            if (waitBar != null)
                waitBar.StartWaitBar(interactionTime);
            
            LockPlayer(true);
            StartCoroutine(InteractionTimer());
        }

        private IEnumerator InteractionTimer()
        {
            yield return new WaitForSeconds(interactionTime);
            CompleteInteraction();
        }

        private void CompleteInteraction()
        {
            // Already timed out check
            if (hasTimedOut) return;

            isInInteraction = false;

            // Network variables update
            networkIsInInteraction.Value = false;
            networkShowCanvas.Value = false;

            // Hide and stop the wait bar since service is complete  
            if (waitBar != null) 
            {
                waitBar.HideBar();
            }
            if (waitCanvas != null) waitCanvas.gameObject.SetActive(false);

            // Reset wait timer since customer has been served
            waitTimeStarted = false;
            networkWaitTimeStarted.Value = false;

            // Place product as child of table
            placedProduct = PlaceOnDropOffTableAsChild();
            if (placedProduct != null)
            {
                if (PrestigeManager.Instance != null)
                    PrestigeManager.Instance.ModifyPrestige(0.05f);

                // FIX: Product yerleştirildikten sonra coroutine başlat
                StartCoroutine(WaitForProductPickupCoroutine());
            }
            else
            {
                if (PrestigeManager.Instance != null)
                    PrestigeManager.Instance.ModifyPrestige(-0.03f);

                TransitionToExit();
            }

            LockPlayer(false);
        }

        // FIX: Coroutine ile product pickup kontrolü
        private IEnumerator WaitForProductPickupCoroutine()
        {
            state = State.WaitingForPickup;
            networkState.Value = (int)State.WaitingForPickup;

            // Bir frame bekle ki product düzgün yerleşsin
            yield return null;

            while (placedProduct != null && !hasTimedOut)
            {
                // Product hala mevcut mu kontrol et
                if (placedProduct == null)
                {
                    break;
                }

                // Parent kontrolü - product hala table'ın child'ı mı?
                if (placedProduct.transform.parent == null || 
                    (dropOffTable != null && !IsChildOfDropOffTable(placedProduct)))
                {
                    placedProduct = null;
                    break;
                }

                yield return new WaitForSeconds(0.1f); // Her 0.1 saniyede kontrol et
            }

            // Pickup tamamlandı veya timeout oldu
            TransitionToExit();
        }

        // FIX: Daha güvenilir child kontrolü
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

        private void CheckProductPickup()
        {
            // Bu metod artık kullanılmıyor, coroutine kullanıyoruz
            // Ama backward compatibility için bırakıyoruz
        }

        // FIX: Network üzerinden tüm oyuncuları kilitle/kilit aç
        private void LockPlayer(bool locked)
        {
            // Sadece etkileşime giren player'ı kilitle
            if (IsServer && cachedPlayerMovement != null)
            {
                // Etkileşime giren player'ın NetworkObject ID'sini al
                ulong interactingPlayerId = GetInteractingPlayerId();
                LockSpecificPlayerClientRpc(locked, interactingPlayerId);
            }
        }
        private ulong GetInteractingPlayerId()
        {
            // Etkileşime giren player'ın ID'sini bul
            // Önce cached player movement'tan dene
            if (cachedPlayerMovement != null && cachedPlayerMovement.NetworkObject != null)
            {
                return cachedPlayerMovement.NetworkObject.OwnerClientId;
            }
    
            // Eğer cache yoksa, range içindeki player'ı bul
            Collider[] playersInRange = Physics.OverlapSphere(transform.position, interactionRange);
            foreach (var collider in playersInRange)
            {
                if (collider.CompareTag("Character"))
                {
                    var networkObject = collider.GetComponent<NetworkObject>();
                    if (networkObject != null)
                    {
                        // Player movement'ı cache'le
                        cachedPlayerMovement = collider.GetComponent<PlayerMovement>();
                        return networkObject.OwnerClientId;
                    }
                }
            }
    
            return NetworkManager.Singleton.LocalClientId; // Fallback
        }

        [ClientRpc]
        private void LockSpecificPlayerClientRpc(bool locked, ulong targetPlayerId)
        {
            // Sadece belirtilen player ID'sine sahip client kendini kitlesin
            if (NetworkManager.Singleton.LocalClientId == targetPlayerId)
            {
                if (cachedPlayerMovement == null)
                    CachePlayerMovement();

                if (cachedPlayerMovement != null)
                {
                    cachedPlayerMovement.LockMovement(locked);
                    cachedPlayerMovement.LockAllInteractions(locked);
                }
            }
        }
        

        [ClientRpc]
        private void LockPlayerClientRpc(bool locked)
        {
            // Her client kendi player'ını kontrol etsin
            if (cachedPlayerMovement == null)
                CachePlayerMovement();

            // Sadece kendi owner olan player'ını kilitle
            if (cachedPlayerMovement != null && cachedPlayerMovement.IsOwner)
            {
                cachedPlayerMovement.LockMovement(locked);
                cachedPlayerMovement.LockAllInteractions(locked);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Character"))
            {
                playerInRange = true;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Character"))
            {
                playerInRange = false;
            }
        }

        private GameObject PlaceOnDropOffTableAsChild()
        {
            if (dropOffTable == null || productPrefabs == null || productPrefabs.Length == 0)
                return null;

            int index = Random.Range(0, productPrefabs.Length);
            
            // Slot pozisyonunu al
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

            // Product'ı spawn et
            var product = Instantiate(productPrefabs[index], spawnPos, spawnRot);

            // NetworkObject kontrolü
            NetworkObject networkObject = product.GetComponent<NetworkObject>();
            if (networkObject != null)
            {
                networkObject.Spawn();
            }

            // ÖNEMLİ: Product'ı table'ın child'ı yap
            if (dropOffTable.slotPoints != null && dropOffTable.slotPoints.Length > dropOffTable.ItemCount)
            {
                product.transform.SetParent(dropOffTable.slotPoints[dropOffTable.ItemCount], true);
            }
            else
            {
                product.transform.SetParent(dropOffTable.transform, true);
            }

            // DisplayTable'a item'ı ekle
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

        // Network variable change handlers (for clients)
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

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public void DebugWaitBarStatus()
        {
        }

        // Debug metodları
        void OnDrawGizmosSelected()
        {
            Gizmos.color = playerInRange ? Color.green : Color.red;
            Gizmos.DrawWireSphere(transform.position, interactionRange);
            
            // State ve timing bilgilerini göster
            string debugInfo = $"State: {state}\nInteracted: {hasInteracted}\nInRange: {playerInRange}";
            if (IsServer)
            {
                debugInfo += $"\nWait Time: {actualWaitTime:F1}s";
                if (placedProduct != null)
                    debugInfo += $"\nPlaced Product: {placedProduct.name}";
            }
            
            #if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2, debugInfo);
            #endif
        }
    }
}