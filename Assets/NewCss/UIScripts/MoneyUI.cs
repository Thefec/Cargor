using TMPro;
using UnityEngine;
using NewCss.Audio;

namespace NewCss
{
    public class MoneyUI : MonoBehaviour
    {
        [SerializeField] TMP_Text moneyText;

        // moneyText'in objesinde NumberRollDisplay varsa sayaç animasyonu + flaş/punch
        // üzerinden gider; yoksa eski "$<sayı>" davranışına sessizce düşülür.
        private NumberRollDisplay _rollDisplay;
        private bool _rollDisplayCached;

        // Faz B (plans/ses-tasarimi.md §3.2): MoneyEarned — bağlantı noktası hazır, klip henüz
        // seçilmedi. OnMoneyChanged NetworkVariable.OnValueChanged üzerinden zaten her client'ta
        // replike tetikleniyor (yeni RPC gerekmez), ARTIŞ tespiti için önceki tutarı burada tutuyoruz.
        private int _lastKnownAmount;
        private bool _lastKnownAmountCached;

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
            // Faz B: klip henüz seçilmedi — MoneyEarned SfxLibrary'de bilerek boş slot
            // (AudioSlotValidator raporlar). SetValue'dan ÖNCE değerlendir: yalnızca gerçek bir
            // artış (rent/upgrade harcaması gibi azalışlar DEĞİL) tetiklesin.
            if (_lastKnownAmountCached && newAmount > _lastKnownAmount)
            {
                SfxBus.Play(SfxId.MoneyEarned);
            }
            _lastKnownAmount = newAmount;
            _lastKnownAmountCached = true;

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
            // İlk gösterim / anlık senkron — "kazanma" sayılmaz, sesi tetiklemeden başlangıç
            // değerini senkronla (aksi halde oturum başında sahte bir "para kazandın" sesi çalar).
            _lastKnownAmount = newAmount;
            _lastKnownAmountCached = true;

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