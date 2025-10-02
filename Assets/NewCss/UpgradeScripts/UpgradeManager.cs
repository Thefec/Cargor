using System;
using System.Collections.Generic;
using UnityEngine;

namespace NewCss
{
    public class UpgradeManager : MonoBehaviour
    {
        public static UpgradeManager Instance { get; private set; }

        private HashSet<ItemType> _purchased = new HashSet<ItemType>();

        /// <summary>
        /// Invoked when an upgrade is purchased.
        /// </summary>
        public event Action<ItemType> OnUpgradePurchased;

        void Awake()
        {
            if (Instance == null) { 
                Instance = this;
                DontDestroyOnLoad(gameObject);
            } else { 
                Destroy(gameObject);
            }
        }

        public bool CanBuy(ItemType t)
        {
            int cost = UpgradeAssets.GetCost(t);
            return !_purchased.Contains(t) && MoneySystem.Instance.CurrentMoney >= cost;
        }

        public bool Buy(ItemType t)
        {
            int cost = UpgradeAssets.GetCost(t);
            if (!CanBuy(t)) return false;

            // Money deduction handled by MoneySystem
            MoneySystem.Instance.SpendMoney(cost);

            _purchased.Add(t);
            OnUpgradePurchased?.Invoke(t);
            Debug.Log($"Upgrade purchased: {UpgradeAssets.GetName(t)}");
            return true;
        }

        public bool IsPurchased(ItemType t) => _purchased.Contains(t);
    }
}