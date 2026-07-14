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

            // Initialize() zaten MoneySystem.OnNetworkSpawn'dan garanti cagriliyor ve
            // burada abone olur; ayni event'e ikinci kez abone olmayi onlemek icin
            // burada tekrar subscribe ETME (cift UpdateText cagrisini onler).
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