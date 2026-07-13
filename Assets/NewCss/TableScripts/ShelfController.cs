using System;
using UnityEngine;

namespace NewCss.UIScripts
{
    /// <summary>
    /// Raf kapasite seviye kontrolc�s�.
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
        [SerializeField, Tooltip("Debug loglar�n� g�ster")]
        private bool showDebugLogs;

        #endregion

        #region Private Fields

        private int _currentLevel;

        #endregion

        #region Events

        /// <summary>
        /// Seviye de�i�ti�inde tetiklenir (previousLevel, newLevel)
        /// </summary>
        public event Action<int, int> OnLevelChanged;

        /// <summary>
        /// Maksimum seviyeye ula��ld���nda tetiklenir
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
        /// Y�kseltme yap�labilir mi?
        /// </summary>
        public bool CanUpgrade => !IsMaxLevel;

        /// <summary>
        /// Toplam seviye say�s�
        /// </summary>
        public int TotalLevels => levels?.Length ?? 0;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            Initialize();
        }

        #endregion

        #region Initialization

        private void Initialize()
        {
            ValidateLevels();
            UpdateVisual();
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

        #region Level Management

        /// <summary>
        /// Raf� bir seviye y�kseltir
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
        /// Seviyeyi ayarlar ve g�rseli g�nceller
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
        /// Seviyeyi zorla ayarlar (clamp olmadan, ge�ersiz de�erler i�in uyar� verir)
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
        /// Seviyeyi bir azalt�r
        /// </summary>
        public void DecreaseLevel()
        {
            if (_currentLevel > MIN_LEVEL)
            {
                SetLevel(_currentLevel - 1);
            }
        }

        /// <summary>
        /// Seviyeyi s�f�rlar
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
        /// G�rsel durumu g�nceller - mevcut seviye ve alt�ndaki t�m objeleri aktif eder
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
        /// G�rseli zorla g�nceller
        /// </summary>
        public void ForceRefreshVisual()
        {
            UpdateVisual();
        }

        #endregion

        #region Query Methods

        /// <summary>
        /// Belirli bir seviyenin aktif olup olmad���n� kontrol eder
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
        /// Belirli bir seviye objesini d�nd�r�r
        /// </summary>
        public GameObject GetLevelObject(int level)
        {
            if (level < 0 || level >= TotalLevels)
            {
                return null;
            }

            return levels[level];
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