using System;
using UnityEngine;

namespace NewCss
{
    /// <summary>
    /// Masa slot seviye kontrolcüsü - upgrade sistemine göre masa seviyelerini yönetir.  
    /// UpgradeManager event'lerini dinleyerek görsel güncellemeleri yapar.
    /// </summary>
    public class TableController : MonoBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[TableController]";

        #endregion

        #region Serialized Fields

        [Header("=== LEVEL OBJECTS ===")]
        [SerializeField, Tooltip("Seviye objeleri (SlotLevel0, SlotLevel1, SlotLevel2...)")]
        public GameObject[] levels;

        [Header("=== DEBUG ===")]
        [SerializeField, Tooltip("Debug loglarýný göster")]
        private bool showDebugLogs;

        #endregion

        #region Private Fields

        private int _currentLevel;

        #endregion

        #region Events

        /// <summary>
        /// Seviye deðiþtiðinde tetiklenir
        /// </summary>
        public event Action<int> OnLevelChanged;

        #endregion

        #region Public Properties

        /// <summary>
        /// Mevcut seviye
        /// </summary>
        public int CurrentLevel => _currentLevel;

        /// <summary>
        /// Maksimum seviye
        /// </summary>
        public int MaxLevel => levels != null ? levels.Length - 1 : 0;

        /// <summary>
        /// Maksimum seviyede mi?
        /// </summary>
        public bool IsMaxLevel => _currentLevel >= MaxLevel;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            Initialize();
        }

        private void OnDestroy()
        {
            UnsubscribeFromUpgradeManager();
        }

        #endregion

        #region Initialization

        private void Initialize()
        {
            ValidateLevels();
            UpdateVisual();
            SubscribeToUpgradeManager();
        }

        private void ValidateLevels()
        {
            if (levels == null || levels.Length == 0)
            {
                LogWarning("No level objects assigned!");
                return;
            }

            // Check for null entries
            for (int i = 0; i < levels.Length; i++)
            {
                if (levels[i] == null)
                {
                    LogWarning($"Level object at index {i} is null!");
                }
            }
        }

        #endregion

        #region Event Subscriptions

        private void SubscribeToUpgradeManager()
        {
            if (UpgradeManager.Instance == null)
            {
                LogWarning("UpgradeManager instance not found!");
                return;
            }

            UpgradeManager.Instance.OnUpgradePurchased += HandleUpgradePurchased;
            LogDebug("Subscribed to UpgradeManager events");
        }

        private void UnsubscribeFromUpgradeManager()
        {
            if (UpgradeManager.Instance != null)
            {
                UpgradeManager.Instance.OnUpgradePurchased -= HandleUpgradePurchased;
            }
        }

        #endregion

        #region Upgrade Event Handler

        private void HandleUpgradePurchased(ItemType itemType)
        {
            int? targetLevel = GetTargetLevelForUpgrade(itemType);

            if (targetLevel.HasValue)
            {
                SetLevel(targetLevel.Value);
                LogDebug($"Upgrade {itemType} applied - Level set to {targetLevel.Value}");
            }
        }

        private int? GetTargetLevelForUpgrade(ItemType itemType)
        {
            return itemType switch
            {
                ItemType.TableSlotsIncrease_1 => 1,
                ItemType.TableSlotsIncrease_2 => 2,
                // Yeni upgrade'ler buraya eklenebilir
                // ItemType.TableSlotsIncrease_3 => 3,
                _ => null
            };
        }

        #endregion

        #region Level Management

        /// <summary>
        /// Seviyeyi ayarlar ve görseli günceller
        /// </summary>
        public void SetLevel(int level)
        {
            int previousLevel = _currentLevel;
            _currentLevel = ClampLevel(level);

            if (previousLevel != _currentLevel)
            {
                UpdateVisual();
                OnLevelChanged?.Invoke(_currentLevel);
                LogDebug($"Level changed: {previousLevel} -> {_currentLevel}");
            }
        }

        /// <summary>
        /// Seviyeyi bir artýrýr
        /// </summary>
        public void IncreaseLevel()
        {
            if (!IsMaxLevel)
            {
                SetLevel(_currentLevel + 1);
            }
        }

        /// <summary>
        /// Seviyeyi bir azaltýr
        /// </summary>
        public void DecreaseLevel()
        {
            if (_currentLevel > 0)
            {
                SetLevel(_currentLevel - 1);
            }
        }

        /// <summary>
        /// Seviyeyi sýfýrlar
        /// </summary>
        public void ResetLevel()
        {
            SetLevel(0);
        }

        private int ClampLevel(int level)
        {
            return Mathf.Clamp(level, 0, MaxLevel);
        }

        #endregion

        #region Visual Update

        /// <summary>
        /// Görsel durumu günceller - mevcut seviye ve altýndaki tüm objeleri aktif eder
        /// </summary>
        private void UpdateVisual()
        {
            if (levels == null || levels.Length == 0)
            {
                return;
            }

            for (int i = 0; i < levels.Length; i++)
            {
                SetLevelObjectActive(i, i <= _currentLevel);
            }
        }

        private void SetLevelObjectActive(int index, bool active)
        {
            if (levels[index] != null)
            {
                levels[index].SetActive(active);
            }
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
        [ContextMenu("Set Level 0")]
        private void DebugSetLevel0()
        {
            SetLevel(0);
        }

        [ContextMenu("Set Level 1")]
        private void DebugSetLevel1()
        {
            SetLevel(1);
        }

        [ContextMenu("Set Level 2")]
        private void DebugSetLevel2()
        {
            SetLevel(2);
        }

        [ContextMenu("Increase Level")]
        private void DebugIncreaseLevel()
        {
            IncreaseLevel();
        }

        [ContextMenu("Decrease Level")]
        private void DebugDecreaseLevel()
        {
            DecreaseLevel();
        }

        [ContextMenu("Reset Level")]
        private void DebugResetLevel()
        {
            ResetLevel();
        }

        [ContextMenu("Debug: Print State")]
        private void DebugPrintState()
        {
            Debug.Log($"{LOG_PREFIX} === TABLE CONTROLLER STATE ===");
            Debug.Log($"Current Level: {_currentLevel}");
            Debug.Log($"Max Level: {MaxLevel}");
            Debug.Log($"Is Max Level: {IsMaxLevel}");
            Debug.Log($"Level Objects: {(levels != null ? levels.Length : 0)}");

            if (levels != null)
            {
                for (int i = 0; i < levels.Length; i++)
                {
                    bool isActive = levels[i] != null && levels[i].activeSelf;
                    Debug.Log($"  Level {i}: {(levels[i] != null ? levels[i].name : "NULL")} - Active: {isActive}");
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (levels == null || levels.Length == 0) return;

            // Draw level indicator
            Vector3 labelPos = transform.position + Vector3.up * 2f;
            UnityEditor.Handles.Label(labelPos, $"Table Level: {_currentLevel}/{MaxLevel}");
        }
#endif

        #endregion
    }
}