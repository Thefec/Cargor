using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace NewCss
{
    /// <summary>
    /// Generic object pool for NetworkObjects to reduce GC pressure and improve performance.
    /// Supports pre-warming and automatic expansion.
    /// </summary>
    public class NetworkObjectPool : NetworkBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[NetworkObjectPool]";

        #endregion

        #region Serialized Fields

        [Header("=== POOL SETTINGS ===")]
        [SerializeField, Tooltip("Prefab to pool")]
        private GameObject prefab;

        [SerializeField, Tooltip("Number of objects to pre-instantiate")]
        private int preWarmCount = 10;

        [SerializeField, Tooltip("Maximum pool size (0 = unlimited)")]
        private int maxPoolSize = 50;

        [Header("=== DEBUG ===")]
        [SerializeField, Tooltip("Show debug logs")]
        private bool showDebugLogs = false;

        #endregion

        #region Private Fields

        private Queue<NetworkObject> _availableObjects;
        private HashSet<NetworkObject> _activeObjects;
        private Transform _poolContainer;

        #endregion

        #region Public Properties

        /// <summary>
        /// Number of available objects in the pool
        /// </summary>
        public int AvailableCount => _availableObjects.Count;

        /// <summary>
        /// Number of currently active objects
        /// </summary>
        public int ActiveCount => _activeObjects.Count;

        /// <summary>
        /// Total number of objects (available + active)
        /// </summary>
        public int TotalCount => _availableObjects.Count + _activeObjects.Count;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Initialize collections with initial capacity to avoid reallocations
            int initialCapacity = Mathf.Max(10, preWarmCount);
            
            CreatePoolContainer();
        }

        #endregion

        #region Network Lifecycle

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                PreWarmPool();
            }
        }

        public override void OnNetworkDespawn()
        {
            CleanupPool();
            base.OnNetworkDespawn();
        }

        #endregion

        #region Initialization

        private void CreatePoolContainer()
        {
            // Initialize collections with initial capacity to avoid reallocations
            int initialCapacity = Mathf.Max(10, preWarmCount);
            _availableObjects = new Queue<NetworkObject>(initialCapacity);
            _activeObjects = new HashSet<NetworkObject>(initialCapacity);

            _poolContainer = new GameObject($"{name}_Pool").transform;
            _poolContainer.SetParent(transform);
            _poolContainer.gameObject.SetActive(false); // Hide pool container
        }

        private void PreWarmPool()
        {
            if (prefab == null)
            {
                LogWarning("Prefab not assigned, cannot pre-warm pool");
                return;
            }

            LogDebug($"Pre-warming pool with {preWarmCount} objects");

            for (int i = 0; i < preWarmCount; i++)
            {
                CreateNewPooledObject();
            }

            LogDebug($"Pool pre-warmed. Available: {AvailableCount}");
        }

        #endregion

        #region Pool Operations

        /// <summary>
        /// Get an object from the pool. Creates a new one if pool is empty.
        /// </summary>
        public NetworkObject Get()
        {
            if (!IsServer)
            {
                LogWarning("Get() called on client - pool operations are server-only");
                return null;
            }

            NetworkObject obj = GetFromPoolOrCreate();
            
            if (obj != null)
            {
                ActivateObject(obj);
                _activeObjects.Add(obj);
            }

            return obj;
        }

        /// <summary>
        /// Return an object to the pool
        /// </summary>
        public void Return(NetworkObject obj)
        {
            if (!IsServer)
            {
                LogWarning("Return() called on client - pool operations are server-only");
                return;
            }

            if (obj == null) return;

            if (!_activeObjects.Remove(obj))
            {
                LogWarning("Attempted to return an object that wasn't from this pool");
                return;
            }

            DeactivateObject(obj);

            // Check if pool has space
            if (maxPoolSize > 0 && _availableObjects.Count >= maxPoolSize)
            {
                // Pool is full, destroy the object
                if (obj.IsSpawned)
                {
                    obj.Despawn(true);
                }
                else
                {
                    Destroy(obj.gameObject);
                }
                LogDebug("Pool full, destroyed returned object");
            }
            else
            {
                // Add back to pool
                _availableObjects.Enqueue(obj);
                LogDebug($"Object returned to pool. Available: {AvailableCount}, Active: {ActiveCount}");
            }
        }

        /// <summary>
        /// Clear all pooled objects
        /// </summary>
        public void Clear()
        {
            if (!IsServer) return;

            // Despawn all active objects
            foreach (var obj in _activeObjects)
            {
                if (obj != null && obj.IsSpawned)
                {
                    obj.Despawn(true);
                }
            }
            _activeObjects.Clear();

            // Destroy all available objects
            while (_availableObjects.Count > 0)
            {
                var obj = _availableObjects.Dequeue();
                if (obj != null)
                {
                    Destroy(obj.gameObject);
                }
            }

            LogDebug("Pool cleared");
        }

        #endregion

        #region Helper Methods

        private NetworkObject GetFromPoolOrCreate()
        {
            // Try to get from pool, skip null objects
            int nullCheckCount = 0;
            const int MAX_NULL_CHECKS = 10; // Prevent infinite loop
            
            while (_availableObjects.Count > 0 && nullCheckCount < MAX_NULL_CHECKS)
            {
                var obj = _availableObjects.Dequeue();
                
                // Validate object
                if (obj != null && obj.gameObject != null)
                {
                    return obj;
                }
                
                LogWarning("Found null object in pool, skipping");
                nullCheckCount++;
            }

            // Pool is empty or all objects were null, create new
            return CreateNewPooledObject();
        }

        private NetworkObject CreateNewPooledObject()
        {
            if (prefab == null)
            {
                LogError("Cannot create pooled object - prefab is null");
                return null;
            }

            GameObject instance = Instantiate(prefab, _poolContainer);
            NetworkObject netObj = instance.GetComponent<NetworkObject>();

            if (netObj == null)
            {
                LogError("Prefab does not have NetworkObject component");
                Destroy(instance);
                return null;
            }

            instance.SetActive(false);
            _availableObjects.Enqueue(netObj);

            LogDebug($"Created new pooled object. Total in pool: {_availableObjects.Count}");

            return netObj;
        }

        private void ActivateObject(NetworkObject obj)
        {
            if (obj == null) return;

            obj.gameObject.SetActive(true);
            obj.transform.SetParent(null); // Remove from pool container

            LogDebug($"Activated pooled object. Active: {ActiveCount}");
        }

        private void DeactivateObject(NetworkObject obj)
        {
            if (obj == null) return;

            // Despawn if spawned
            if (obj.IsSpawned)
            {
                obj.Despawn(false); // Don't destroy
            }

            obj.gameObject.SetActive(false);
            obj.transform.SetParent(_poolContainer); // Return to pool container
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = Quaternion.identity;
        }

        private void CleanupPool()
        {
            Clear();
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

        private void LogWarning(string message)
        {
            Debug.LogWarning($"{LOG_PREFIX} {message}");
        }

        private void LogError(string message)
        {
            Debug.LogError($"{LOG_PREFIX} {message}");
        }

        #endregion

        #region Editor & Debug

#if UNITY_EDITOR
        [ContextMenu("Debug: Print Pool State")]
        private void DebugPrintState()
        {
            Debug.Log($"{LOG_PREFIX} === POOL STATE ===");
            Debug.Log($"Prefab: {(prefab != null ? prefab.name : "NULL")}");
            Debug.Log($"Available: {AvailableCount}");
            Debug.Log($"Active: {ActiveCount}");
            Debug.Log($"Total: {TotalCount}");
            Debug.Log($"Pre-warm Count: {preWarmCount}");
            Debug.Log($"Max Pool Size: {maxPoolSize}");
        }

        [ContextMenu("Debug: Clear Pool")]
        private void DebugClearPool()
        {
            Clear();
        }
#endif

        #endregion
    }
}
