using TMPro;
using UnityEngine;

namespace NewCss
{
    public class MoneyUI : MonoBehaviour
    {
        [SerializeField] TMP_Text moneyText;

        // moneyText'in objesinde NumberRollDisplay varsa sayaç animasyonu + flaş/punch
        // üzerinden gider; yoksa eski "$<sayı>" davranışına sessizce düşülür.
        private NumberRollDisplay _rollDisplay;
        private bool _rollDisplayCached;

        public void Initialize(MoneySystem sys)
        {
            sys.OnMoneyChanged += UpdateText;
            UpdateTextInstant(sys.CurrentMoney);
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
            // Ilk gosterim: 0'dan acilis degerine saymasin, aninda otursun.
            UpdateTextInstant(MoneySystem.Instance.CurrentMoney);
        }

        void OnDestroy()
        {
            // Unsubscribe when the scene closes or object is destroyed
            if (MoneySystem.Instance != null)
                MoneySystem.Instance.OnMoneyChanged -= UpdateText;
        }

        private NumberRollDisplay RollDisplay
        {
            get
            {
                if (!_rollDisplayCached)
                {
                    _rollDisplay = moneyText != null ? moneyText.GetComponent<NumberRollDisplay>() : null;
                    _rollDisplayCached = true;
                }
                return _rollDisplay;
            }
        }

        void UpdateText(int newAmount)
        {
            var roll = RollDisplay;
            if (roll != null)
            {
                roll.SetValue(newAmount);
                return;
            }

            if (moneyText != null)
                moneyText.text = $"${newAmount}";
        }

        void UpdateTextInstant(int newAmount)
        {
            var roll = RollDisplay;
            if (roll != null)
            {
                roll.SetValueInstant(newAmount);
                return;
            }

            if (moneyText != null)
                moneyText.text = $"${newAmount}";
        }
    }
}