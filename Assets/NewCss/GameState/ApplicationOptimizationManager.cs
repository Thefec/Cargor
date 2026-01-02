using System;
using UnityEngine;
using Unity.Netcode;

namespace NewCss
{
    /// <summary>
    /// Steam overlay ve Alt-Tab optimizasyonlarını yönetir.
    /// Network paketlerinin frame-bound olması nedeniyle FPS'i çok düşürmez.
    /// Facepunch Steamworks entegrasyonunu bozmadan çalışır.
    /// </summary>
    public class ApplicationOptimizationManager : MonoBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[AppOptimization]";
        private const int BACKGROUND_FRAMERATE = 30; // Alt-Tab'da 30 FPS (network için gerekli minimum)
        private const int FOCUSED_FRAMERATE = -1; // Odaktayken sınırsız (VSync'e bırak)

        #endregion

        #region Singleton

        private static ApplicationOptimizationManager _instance;
        public static ApplicationOptimizationManager Instance => _instance;

        #endregion

        #region Serialized Fields

        [Header("=== OPTIMIZATION SETTINGS ===")]
        [SerializeField, Tooltip("Background'da hedef FPS (30-60 arası önerilir)")]
        [Range(15, 60)]
        private int backgroundFrameRate = BACKGROUND_FRAMERATE;

        [SerializeField, Tooltip("Odaktayken hedef FPS (-1 = sınırsız)")]
        private int focusedFrameRate = FOCUSED_FRAMERATE;

        [SerializeField, Tooltip("Arka planda çalışmayı etkinleştir")]
        private bool runInBackground = true;

        [SerializeField, Tooltip("Steam overlay optimizasyonunu etkinleştir")]
        private bool enableSteamOverlayOptimization = true;

        [Header("=== DEBUG ===")]
        [SerializeField, Tooltip("Debug loglarını göster")]
        private bool showDebugLogs = false;

        #endregion

        #region Private Fields

        private bool _wasFocused = true;
        private bool _isSteamOverlayActive;
        private int _originalVSyncCount;
        private int _originalTargetFrameRate;

        #endregion

        #region Public Properties

        /// <summary>
        /// Uygulama odakta mı?
        /// </summary>
        public bool IsFocused => Application.isFocused;

        /// <summary>
        /// Steam overlay açık mı?
        /// </summary>
        public bool IsSteamOverlayActive => _isSteamOverlayActive;

        /// <summary>
        /// Background modunda mı?
        /// </summary>
        public bool IsInBackground => !Application.isFocused || _isSteamOverlayActive;

        #endregion

        #region Events

        /// <summary>
        /// Uygulama odak durumu değiştiğinde
        /// </summary>
        public static event Action<bool> OnFocusChanged;

        /// <summary>
        /// Steam overlay durumu değiştiğinde
        /// </summary>
        public static event Action<bool> OnSteamOverlayChanged;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeSingleton();
            StoreOriginalSettings();
            ApplyBackgroundSettings();
        }

        private void Start()
        {
            SubscribeToSteamEvents();
            ApplyInitialOptimizations();
        }

        private void OnDestroy()
        {
            UnsubscribeFromSteamEvents();
            RestoreOriginalSettings();
            CleanupSingleton();
        }

        private void Update()
        {
            CheckFocusChange();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            HandleFocusChange(hasFocus);
        }

        private void OnApplicationPause(bool isPaused)
        {
            // iOS/Android için - PC'de genellikle çağrılmaz
            if (isPaused)
            {
                ApplyBackgroundOptimizations();
            }
            else
            {
                ApplyFocusedOptimizations();
            }
        }

        #endregion

        #region Singleton Management

        private void InitializeSingleton()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void CleanupSingleton()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        #endregion

        #region Initialization

        private void StoreOriginalSettings()
        {
            _originalVSyncCount = QualitySettings.vSyncCount;
            _originalTargetFrameRate = Application.targetFrameRate;
        }

        private void RestoreOriginalSettings()
        {
            QualitySettings.vSyncCount = _originalVSyncCount;
            Application.targetFrameRate = _originalTargetFrameRate;
        }

        private void ApplyBackgroundSettings()
        {
            // Kritik: Arka planda çalışmayı etkinleştir
            Application.runInBackground = runInBackground;
            LogDebug($"Application.runInBackground = {runInBackground}");
        }

        private void ApplyInitialOptimizations()
        {
            // Başlangıçta odaktaysak focused ayarları uygula
            if (Application.isFocused)
            {
                ApplyFocusedOptimizations();
            }
            else
            {
                ApplyBackgroundOptimizations();
            }
        }

        #endregion

        #region Steam Events

        private void SubscribeToSteamEvents()
        {
            if (!enableSteamOverlayOptimization) return;

            try
            {
                // Facepunch Steamworks overlay events
                Steamworks.SteamFriends.OnGameOverlayActivated += HandleSteamOverlayActivated;
                LogDebug("Subscribed to Steam overlay events");
            }
            catch (Exception ex)
            {
                LogWarning($"Could not subscribe to Steam events: {ex.Message}");
            }
        }

        private void UnsubscribeFromSteamEvents()
        {
            try
            {
                Steamworks.SteamFriends.OnGameOverlayActivated -= HandleSteamOverlayActivated;
            }
            catch
            {
                // Ignore - Steam may not be initialized
            }
        }

        private void HandleSteamOverlayActivated(bool isActive)
        {
            _isSteamOverlayActive = isActive;
            LogDebug($"Steam overlay {(isActive ? "opened" : "closed")}");

            if (isActive)
            {
                ApplyBackgroundOptimizations();
            }
            else
            {
                ApplyFocusedOptimizations();
            }

            OnSteamOverlayChanged?.Invoke(isActive);
        }

        #endregion

        #region Focus Management

        private void CheckFocusChange()
        {
            bool currentFocus = Application.isFocused;

            if (currentFocus != _wasFocused)
            {
                HandleFocusChange(currentFocus);
            }
        }

        private void HandleFocusChange(bool hasFocus)
        {
            if (_wasFocused == hasFocus) return;

            _wasFocused = hasFocus;
            LogDebug($"Application focus: {hasFocus}");

            if (hasFocus && !_isSteamOverlayActive)
            {
                ApplyFocusedOptimizations();
            }
            else
            {
                ApplyBackgroundOptimizations();
            }

            OnFocusChanged?.Invoke(hasFocus);
        }

        #endregion

        #region Optimization Application

        private void ApplyFocusedOptimizations()
        {
            // Odaktayken normal ayarlar
            Application.targetFrameRate = focusedFrameRate;

            LogDebug($"Applied focused optimizations - TargetFPS: {focusedFrameRate}");
        }

        private void ApplyBackgroundOptimizations()
        {
            // KRITIK: Network paketleri frame-bound olduğu için FPS'i çok düşürme!
            // 30 FPS minimum - bu kopmaları ve lag'i önler
            Application.targetFrameRate = backgroundFrameRate;

            LogDebug($"Applied background optimizations - TargetFPS: {backgroundFrameRate}");
        }

        #endregion

        #region Public API

        /// <summary>
        /// Background FPS'i ayarlar
        /// </summary>
        public void SetBackgroundFrameRate(int fps)
        {
            backgroundFrameRate = Mathf.Clamp(fps, 15, 60);

            if (IsInBackground)
            {
                Application.targetFrameRate = backgroundFrameRate;
            }
        }

        /// <summary>
        /// Focused FPS'i ayarlar
        /// </summary>
        public void SetFocusedFrameRate(int fps)
        {
            focusedFrameRate = fps;

            if (!IsInBackground)
            {
                Application.targetFrameRate = focusedFrameRate;
            }
        }

        /// <summary>
        /// Mevcut durumu döndürür
        /// </summary>
        public string GetStatusInfo()
        {
            return $"Focused: {IsFocused}\n" +
                   $"Steam Overlay: {IsSteamOverlayActive}\n" +
                   $"In Background: {IsInBackground}\n" +
                   $"Target FPS: {Application.targetFrameRate}\n" +
                   $"Run In Background: {Application.runInBackground}";
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

        #endregion

        #region Editor Debug

#if UNITY_EDITOR
        [ContextMenu("Print Status")]
        private void DebugPrintStatus()
        {
            Debug.Log(GetStatusInfo());
        }

        [ContextMenu("Simulate Alt-Tab")]
        private void DebugSimulateAltTab()
        {
            HandleFocusChange(false);
        }

        [ContextMenu("Simulate Focus Regained")]
        private void DebugSimulateFocusRegained()
        {
            HandleFocusChange(true);
        }

        [ContextMenu("Simulate Steam Overlay Open")]
        private void DebugSimulateOverlayOpen()
        {
            HandleSteamOverlayActivated(true);
        }

        [ContextMenu("Simulate Steam Overlay Close")]
        private void DebugSimulateOverlayClose()
        {
            HandleSteamOverlayActivated(false);
        }
#endif

        #endregion
    }
}
