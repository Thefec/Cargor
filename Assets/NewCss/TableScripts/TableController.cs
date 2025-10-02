// TableController.cs
using UnityEngine;
using System;

namespace NewCss
{
    public class TableController : MonoBehaviour
    {
        [Tooltip("Child objects increasing from 0 (SlotLevel0, SlotLevel1, SlotLevel2, ...)")]
        public GameObject[] levels;

        private int _currentLevel = 0;
        public int CurrentLevel => _currentLevel;

        void Start()
        {
            // Activate only level 0 at the start
            UpdateVisual();

            // Subscribe to UpgradeManager event
            UpgradeManager.Instance.OnUpgradePurchased += OnUpgradePurchased;
        }

        void OnDestroy()
        {
            if (UpgradeManager.Instance != null)
                UpgradeManager.Instance.OnUpgradePurchased -= OnUpgradePurchased;
        }

        private void OnUpgradePurchased(ItemType itemType)
        {
            // Check for upgrades that affect tables here
            switch (itemType)
            {
                // Example: if your enum includes TableSlotsIncrease_1, _2, _3, etc.
                case ItemType.TableSlotsIncrease_1:
                    SetLevel(1);
                    break;
                case ItemType.TableSlotsIncrease_2:
                    SetLevel(2);
                    break;

                default:
                    return;
            }
        }

        /// <summary>
        /// Sets the table's level index and updates the visual.
        /// </summary>
        private void SetLevel(int level)
        {
            _currentLevel = Mathf.Clamp(level, 0, levels.Length - 1);
            UpdateVisual();
        }

        /// <summary>
        /// Activates all level objects with index <= currentLevel, deactivates others.
        /// </summary>
        private void UpdateVisual()
        {
            for (int i = 0; i < levels.Length; i++)
                levels[i].SetActive(i <= _currentLevel);
        }
    }
}