using System;
using Steamworks;
using UnityEngine;

namespace Utils
{
    /// <summary>
    /// Steam overlay durumunu takip eden singleton utility sınıfı.
    /// Overlay açıldığında/kapandığında tüm sistemlere event-based bildirim gönderir.
    /// Thread-safe implementation.
    /// </summary>
    public static class SteamOverlayDetector
    {
        #region Events

        /// <summary>
        /// Overlay durumu değiştiğinde tetiklenir.
        /// </summary>
        public static event Action<bool> OnOverlayStateChanged;

        #endregion

        #region Properties

        /// <summary>
        /// Steam overlay şu anda açık mı?
        /// </summary>
        public static bool IsOverlayActive { get; private set; }

        /// <summary>
        /// Detector initialize edildi mi?
        /// </summary>
        public static bool IsInitialized { get; private set; }

        #endregion

        #region Private Fields

        private static readonly object _lock = new object();

        #endregion

        #region Initialization

        /// <summary>
        /// Steam overlay detector'ı başlatır.
        /// </summary>
        public static void Initialize()
        {
            lock (_lock)
            {
                if (IsInitialized)
                {
                    Debug.LogWarning("[SteamOverlayDetector] Already initialized.");
                    return;
                }

                if (!SteamClient.IsValid)
                {
                    Debug.LogError("[SteamOverlayDetector] SteamClient is not valid. Cannot initialize.");
                    return;
                }

                try
                {
                    // Steam overlay event'ine subscribe ol
                    SteamFriends.OnGameOverlayActivated += OnGameOverlayActivated;
                    IsInitialized = true;
                    Debug.Log("[SteamOverlayDetector] Initialized successfully.");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[SteamOverlayDetector] Failed to initialize: {ex}");
                }
            }
        }

        /// <summary>
        /// Detector'ı temizler.
        /// </summary>
        public static void Shutdown()
        {
            lock (_lock)
            {
                if (!IsInitialized)
                    return;

                try
                {
                    SteamFriends.OnGameOverlayActivated -= OnGameOverlayActivated;
                    IsOverlayActive = false;
                    IsInitialized = false;
                    OnOverlayStateChanged = null;
                    Debug.Log("[SteamOverlayDetector] Shutdown complete.");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[SteamOverlayDetector] Error during shutdown: {ex}");
                }
            }
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Steam overlay aktif/deaktif olduğunda çağrılır.
        /// </summary>
        private static void OnGameOverlayActivated(bool active)
        {
            lock (_lock)
            {
                if (IsOverlayActive == active)
                    return;

                IsOverlayActive = active;

                Debug.Log($"[SteamOverlayDetector] Overlay state changed: {(active ? "ACTIVE" : "INACTIVE")}");

                // Event'i trigger et (main thread'de)
                UnityMainThreadDispatcher.Enqueue(() =>
                {
                    OnOverlayStateChanged?.Invoke(active);
                });
            }
        }

        #endregion
    }

    /// <summary>
    /// Unity main thread'de action'ları çalıştırmak için yardımcı sınıf.
    /// </summary>
    public class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static UnityMainThreadDispatcher _instance;
        private static readonly object _instanceLock = new object();
        private static readonly System.Collections.Generic.Queue<Action> _executionQueue = new System.Collections.Generic.Queue<Action>();

        public static void Enqueue(Action action)
        {
            if (action == null)
                return;

            lock (_executionQueue)
            {
                _executionQueue.Enqueue(action);
            }

            // Instance yoksa oluştur - thread-safe singleton pattern
            if (_instance == null)
            {
                lock (_instanceLock)
                {
                    if (_instance == null)
                    {
                        var go = new GameObject("UnityMainThreadDispatcher");
                        _instance = go.AddComponent<UnityMainThreadDispatcher>();
                        DontDestroyOnLoad(go);
                    }
                }
            }
        }

        private void Update()
        {
            lock (_executionQueue)
            {
                while (_executionQueue.Count > 0)
                {
                    try
                    {
                        _executionQueue.Dequeue()?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[UnityMainThreadDispatcher] Error executing action: {ex}");
                    }
                }
            }
        }

        private void OnDestroy()
        {
            lock (_instanceLock)
            {
                if (_instance == this)
                {
                    _instance = null;
                }
            }
        }
    }
}
