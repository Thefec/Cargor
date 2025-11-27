using System;
using System.Collections.Generic;
using UnityEngine;

namespace NewCss.UIScripts
{
    /// <summary>
    /// Raf kapasite seviye kontrolcüsü - upgrade sistemine göre raf kapasitesini yönetir.   
    /// UpgradeManager event'lerini dinleyerek görsel güncellemeleri yapar.
    /// </summary>
    public class ShelfController : MonoBehaviour
    {
        #region Constants

        private const string LOG_PREFIX = "[ShelfController]";
        private const int MIN_LEVEL = 0;

        #endregion

        #region Serialized Fields

        [Header("=== LEVEL OBJECTS ===")]
        [SerializeField, Tooltip("Seviye objeleri (Level0, Level1, Level2...)")]
        public GameObject[] levels;

        [Header("=== DEBUG ===")]
        [SerializeField, Tooltip("Debug loglarýný göster")]
        private bool showDebugLogs;

        #endregion

        #region Private Fields

        private int _currentLevel;

        // Capacity upgrade mapping cache
        private static readonly Dictionary<ItemType, int> CapacityUpgradeMapping = new()
        {
            { ItemType.MoreCapacity_1, 1 },
            { ItemType.MoreCapacity_2, 2 },
            { ItemType.MoreCapacity_3, 3 },
            { ItemType.MoreCapacity_4, 4 },
            { ItemType. MoreCapacity_5, 5 },
            { ItemType. MoreCapacity_6, 6 },
            { ItemType.MoreCapacity_7, 7 },
            { ItemType.MoreCapacity_8, 8 },
            { ItemType.MoreCapacity_9, 9 },
            { ItemType.MoreCapacity_10, 10 },
            { ItemType.MoreCapacity_11, 11 },
            { ItemType.MoreCapacity_12, 12 },
            { ItemType. MoreCapacity_13, 13 },
            { ItemType. MoreCapacity_14, 14 },
            { ItemType.MoreCapacity_15, 15 }
        };

        #endregion

        #region Events

        /// <summary>
        /// Seviye deðiþtiðinde tetiklenir (previousLevel, newLevel)
        /// </summary>
        public event Action<int, int> OnLevelChanged;

        /// <summary>
        /// Maksimum seviyeye ulaþýldýðýnda tetiklenir
        /// </summary>
        public event Action OnMaxLevelReached;

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

        /// <summary>
        /// Yükseltme yapýlabilir mi?
        /// </summary>
        public bool CanUpgrade => !IsMaxLevel;

        /// <summary>
        /// Toplam seviye sayýsý
        /// </summary>
        public int TotalLevels => levels?.Length ?? 0;

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

            int nullCount = 0;
            for (int i = 0; i < levels.Length; i++)
            {
                if (levels[i] == null)
                {
                    LogWarning($"Level object at index {i} is null!");
                    nullCount++;
                }
            }

            if (nullCount > 0)
            {
                LogWarning($"Total {nullCount} null level objects found!");
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

        /// <summary>
        /// Upgrade satýn alýndýðýnda çaðrýlýr
        /// </summary>
        public void HandleUpgradePurchased(ItemType itemType)
        {
            if (TryGetTargetLevel(itemType, out int targetLevel))
            {
                SetLevel(targetLevel);
                LogDebug($"Capacity upgrade {itemType} applied - Level set to {targetLevel}");
            }
        }

        private bool TryGetTargetLevel(ItemType itemType, out int targetLevel)
        {
            return CapacityUpgradeMapping.TryGetValue(itemType, out targetLevel);
        }

        // Backward compatibility - keeping the original method name
        public void OnUpgradePurchased(ItemType itemType)
        {
            HandleUpgradePurchased(itemType);
        }

        #endregion

        #region Level Management

        /// <summary>
        /// Rafý bir seviye yükseltir
        /// </summary>
        public void UpgradeShelf()
        {
            if (!CanUpgrade)
            {
                LogDebug("Cannot upgrade - already at max level");
                return;
            }

            SetLevel(_currentLevel + 1);
        }

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
                NotifyLevelChanged(previousLevel, _currentLevel);
                LogDebug($"Level changed: {previousLevel} -> {_currentLevel}");
            }
        }

        /// <summary>
        /// Seviyeyi zorla ayarlar (clamp olmadan, geçersiz deðerler için uyarý verir)
        /// </summary>
        public void ForceSetLevel(int level)
        {
            if (level < MIN_LEVEL || level > MaxLevel)
            {
                LogWarning($"Invalid level {level}. Valid range: {MIN_LEVEL}-{MaxLevel}");
                return;
            }

            SetLevel(level);
        }

        /// <summary>
        /// Seviyeyi bir azaltýr
        /// </summary>
        public void DecreaseLevel()
        {
            if (_currentLevel > MIN_LEVEL)
            {
                SetLevel(_currentLevel - 1);
            }
        }

        /// <summary>
        /// Seviyeyi sýfýrlar
        /// </summary>
        public void ResetLevel()
        {
            SetLevel(MIN_LEVEL);
        }

        /// <summary>
        /// Maksimum seviyeye ayarlar
        /// </summary>
        public void SetToMaxLevel()
        {
            SetLevel(MaxLevel);
        }

        private int ClampLevel(int level)
        {
            return Mathf.Clamp(level, MIN_LEVEL, MaxLevel);
        }

        #endregion

        #region Event Notifications

        private void NotifyLevelChanged(int previousLevel, int newLevel)
        {
            OnLevelChanged?.Invoke(previousLevel, newLevel);

            if (newLevel >= MaxLevel)
            {
                OnMaxLevelReached?.Invoke();
                LogDebug("Max level reached!");
            }
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

        /// <summary>
        /// Görseli zorla günceller
        /// </summary>
        public void ForceRefreshVisual()
        {
            UpdateVisual();
        }

        #endregion

        #region Query Methods

        /// <summary>
        /// Belirli bir seviyenin aktif olup olmadýðýný kontrol eder
        /// </summary>
        public bool IsLevelActive(int level)
        {
            if (level < 0 || level >= TotalLevels)
            {
                return false;
            }

            return level <= _currentLevel;
        }

        /// <summary>
        /// Belirli bir seviye objesini döndürür
        /// </summary>
        public GameObject GetLevelObject(int level)
        {
            if (level < 0 || level >= TotalLevels)
            {
                return null;
            }

            return levels[level];
        }

        /// <summary>
        /// Sonraki seviyeye geçmek için gereken upgrade item type'ýný döndürür
        /// </summary>
        public ItemType? GetNextUpgradeItemType()
        {
            if (IsMaxLevel)
            {
                return null;
            }

            int nextLevel = _currentLevel + 1;

            foreach (var kvp in CapacityUpgradeMapping)
            {
                if (kvp.Value == nextLevel)
                {
                    return kvp.Key;
                }
            }

            return null;
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
        [ContextMenu("Upgrade Shelf")]
        private void DebugUpgradeShelf()
        {
            UpgradeShelf();
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

        [ContextMenu("Set to Max Level")]
        private void DebugSetToMaxLevel()
        {
            SetToMaxLevel();
        }

        [ContextMenu("Set Level 0")]
        private void DebugSetLevel0() => SetLevel(0);

        [ContextMenu("Set Level 5")]
        private void DebugSetLevel5() => SetLevel(5);

        [ContextMenu("Set Level 10")]
        private void DebugSetLevel10() => SetLevel(10);

        [ContextMenu("Set Level 15")]
        private void DebugSetLevel15() => SetLevel(15);

        [ContextMenu("Refresh Visual")]
        private void DebugRefreshVisual()
        {
            ForceRefreshVisual();
        }

        [ContextMenu("Debug: Print State")]
        private void DebugPrintState()
        {
            Debug.Log($"{LOG_PREFIX} === SHELF CONTROLLER STATE ===");
            Debug.Log($"Current Level: {_currentLevel}");
            Debug.Log($"Max Level: {MaxLevel}");
            Debug.Log($"Is Max Level: {IsMaxLevel}");
            Debug.Log($"Can Upgrade: {CanUpgrade}");
            Debug.Log($"Total Levels: {TotalLevels}");
            Debug.Log($"Next Upgrade: {GetNextUpgradeItemType()?.ToString() ?? "None"}");

            if (levels != null)
            {
                Debug.Log($"--- Level Objects ---");
                for (int i = 0; i < levels.Length; i++)
                {
                    bool isActive = levels[i] != null && levels[i].activeSelf;
                    string status = i <= _currentLevel ? "ACTIVE" : "INACTIVE";
                    Debug.Log($"  [{i}] {(levels[i] != null ? levels[i].name : "NULL")} - {status}");
                }
            }
        }

        [ContextMenu("Debug: Print Upgrade Mapping")]
        private void DebugPrintUpgradeMapping()
        {
            Debug.Log($"{LOG_PREFIX} === CAPACITY UPGRADE MAPPING ===");
            foreach (var kvp in CapacityUpgradeMapping)
            {
                Debug.Log($"  {kvp.Key} -> Level {kvp.Value}");
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (levels == null || levels.Length == 0) return;

            Vector3 labelPos = transform.position + Vector3.up * 2f;
            UnityEditor.Handles.Label(labelPos, $"Shelf Level: {_currentLevel}/{MaxLevel}");
        }
#endif

        #endregion
    }
}