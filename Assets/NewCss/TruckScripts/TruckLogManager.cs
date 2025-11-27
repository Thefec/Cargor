using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace NewCss
{
    /// <summary>
    /// Kamyon teslimat log yöneticisi - günlük teslimat kayıtlarını tutar ve gösterir.
    /// </summary>
    public class TruckLogManager : MonoBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[TruckLog]";

        #endregion

        #region Singleton

        public static TruckLogManager Instance { get; private set; }

        #endregion

        #region Serialized Fields

        [Header("=== UI SETTINGS ===")]
        [SerializeField, Tooltip("Log text UI elementi")]
        public TextMeshProUGUI logText;

        #endregion

        #region Private Fields

        private readonly List<string> _dailyLogs = new();

        #endregion

        #region Public Properties

        /// <summary>
        /// Günlük log sayısı
        /// </summary>
        public int LogCount => _dailyLogs.Count;

        /// <summary>
        /// Günlük loglar (readonly)
        /// </summary>
        public IReadOnlyList<string> DailyLogs => _dailyLogs;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            InitializeSingleton();
        }

        #endregion

        #region Initialization

        private void InitializeSingleton()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Yeni log entry ekler
        /// </summary>
        /// <param name="logEntry">Log metni</param>
        public void AddLogEntry(string logEntry)
        {
            if (string.IsNullOrEmpty(logEntry))
            {
                return;
            }

            _dailyLogs.Add(logEntry);
            UpdateLogUI();

            LogDebug($"Log added: {logEntry}");
        }

        /// <summary>
        /// Kamyon teslimat logu ekler
        /// </summary>
        /// <param name="truck">Teslimatı tamamlanan kamyon</param>
        public void AddTruckDeliveryLog(Truck truck)
        {
            if (truck == null)
            {
                return;
            }

            string logEntry = FormatTruckDeliveryLog(truck);
            AddLogEntry(logEntry);
        }

        /// <summary>
        /// Günlük logları temizler
        /// </summary>
        public void ClearDailyLog()
        {
            _dailyLogs.Clear();
            UpdateLogUI();

            LogDebug("Daily logs cleared");
        }

        /// <summary>
        /// Belirli bir log entry'sini döndürür
        /// </summary>
        public string GetLogEntry(int index)
        {
            if (index < 0 || index >= _dailyLogs.Count)
            {
                return null;
            }

            return _dailyLogs[index];
        }

        #endregion

        #region UI Update

        private void UpdateLogUI()
        {
            if (logText == null)
            {
                return;
            }

            logText.text = string.Join("\n", _dailyLogs);
        }

        #endregion

        #region Formatting

        private static string FormatTruckDeliveryLog(Truck truck)
        {
            return $"[{System.DateTime.Now:HH:mm}] {truck.requestedBoxType} - " +
                   $"{truck.DeliveredCount}/{truck.RequiredCargo} delivered";
        }

        #endregion

        #region Logging

        private void LogDebug(string message)
        {
            Debug.Log($"{LOG_PREFIX} {message}");
        }

        #endregion

        #region Editor Debug

#if UNITY_EDITOR
        [ContextMenu("Clear Logs")]
        private void DebugClearLogs()
        {
            ClearDailyLog();
        }

        [ContextMenu("Add Test Log")]
        private void DebugAddTestLog()
        {
            AddLogEntry($"Test log entry at {System.DateTime.Now:HH:mm:ss}");
        }

        [ContextMenu("Debug: Print State")]
        private void DebugPrintState()
        {
            Debug.Log($"{LOG_PREFIX} === TRUCK LOG STATE ===");
            Debug.Log($"Log Count: {_dailyLogs.Count}");
            Debug.Log($"Has UI: {logText != null}");

            for (int i = 0; i < _dailyLogs.Count; i++)
            {
                Debug.Log($"  [{i}]: {_dailyLogs[i]}");
            }
        }
#endif

        #endregion
    }
}