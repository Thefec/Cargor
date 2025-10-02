using UnityEngine;
using NewCss;

namespace NewCss.UIScripts
{
    public class ShelfController : MonoBehaviour
    {
        [Tooltip("Child objects increasing from 0 (Level0, Level1, Level2, Level3)")]
        public GameObject[] levels;

        private int _currentLevel = 0;
        // Inside ShelfController.cs
        public int CurrentLevel => _currentLevel;

        void Start()
        {
            UpdateVisual();
            UpgradeManager.Instance.OnUpgradePurchased += OnUpgradePurchased;
        }

        /// <summary>
        /// Upgrade to the next level
        /// </summary>
        public void UpgradeShelf()
        {
            int next = _currentLevel + 1;
            SetLevel(next);
        }

        void OnDestroy()
        {
            if (UpgradeManager.Instance != null)
                UpgradeManager.Instance.OnUpgradePurchased -= OnUpgradePurchased;
        }

        public void OnUpgradePurchased(ItemType itemType)
        {
            switch (itemType)
            {
                case ItemType.MoreCapacity_1:
                    SetLevel(1);
                    break;
                case ItemType.MoreCapacity_2:
                    SetLevel(2);
                    break;
                case ItemType.MoreCapacity_3:
                    SetLevel(3);
                    break;
                case ItemType.MoreCapacity_4:
                    SetLevel(4);
                    break;
                case ItemType.MoreCapacity_5:
                    SetLevel(5);
                    break;
                case ItemType.MoreCapacity_6:
                    SetLevel(6);
                    break;
                case ItemType.MoreCapacity_7:
                    SetLevel(7);
                    break;
                case ItemType.MoreCapacity_8:
                    SetLevel(8);
                    break;
                case ItemType.MoreCapacity_9:
                    SetLevel(9);
                    break;
                case ItemType.MoreCapacity_10:
                    SetLevel(10);
                    break;
                case ItemType.MoreCapacity_11:
                    SetLevel(11);
                    break;
                case ItemType.MoreCapacity_12:
                    SetLevel(12);
                    break;
                case ItemType.MoreCapacity_13:
                    SetLevel(13);
                    break;
                case ItemType.MoreCapacity_14:
                    SetLevel(14);
                    break;
                case ItemType.MoreCapacity_15:
                    SetLevel(15);
                    break;
                default:
                    return;
            }
        }

        private void SetLevel(int level)
        {
            _currentLevel = Mathf.Clamp(level, 0, levels.Length - 1);
            UpdateVisual();
        }

        private void UpdateVisual()
        {
            // Now i <= _currentLevel keeps all previous levels active as well
            for (int i = 0; i < levels.Length; i++)
            {
                levels[i].SetActive(i <= _currentLevel);
            }
        }
    }
}
