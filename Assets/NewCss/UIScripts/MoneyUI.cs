using TMPro;
using UnityEngine;

namespace NewCss
{
    public class MoneyUI : MonoBehaviour
    {
        [SerializeField] TMP_Text moneyText;

        public void Initialize(MoneySystem sys)
        {
            sys.OnMoneyChanged += UpdateText;
            UpdateText(sys.CurrentMoney);
        }

        void Start()
        {
            if (MoneySystem.Instance == null)
            {
                enabled = false; // Disable the UI script
                return;
            }

            // Subscribe here
            MoneySystem.Instance.OnMoneyChanged += UpdateText;
            UpdateText(MoneySystem.Instance.CurrentMoney);
        }

        void OnDestroy()
        {
            // Unsubscribe when the scene closes or object is destroyed
            if (MoneySystem.Instance != null)
                MoneySystem.Instance.OnMoneyChanged -= UpdateText;
        }

        void UpdateText(int newAmount)
        {
            if (moneyText != null)
                moneyText.text = $"${newAmount}";
        }
    }
}