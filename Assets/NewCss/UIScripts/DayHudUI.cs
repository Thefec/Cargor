using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace NewCss
{
    /// <summary>
    /// Gün HUD'u: dolan zaman pastası (radial) + gün/gece ikonu + kira satırı.
    /// Salt-okunur bileşen — DayCycleManager / MoneySystem NetworkVariable'larını
    /// yalnızca okur, hiçbir şey yazmaz. NetworkBehaviour DEĞİL.
    /// Kasa event'ine abone OLMAZ: değerler her karede okunup cache ile karşılaştırılır.
    /// Böylece MoneySystem despawn/respawn olsa bile kopacak bir abonelik yoktur.
    /// </summary>
    public class DayHudUI : MonoBehaviour
    {
        private const string RENT_DUE_KEY = "RentDueToday";
        private const string RENT_DUE_FALLBACK = "Rent due today!";

        [Header("Time Pie")]
        [SerializeField] private Image timePie;
        [SerializeField] private Image timeIcon;
        [SerializeField] private Sprite sunSprite;
        [SerializeField] private Sprite moonSprite;
        [SerializeField] private Color dayColor = new Color(1f, 0.75f, 0.25f);
        [SerializeField] private Color eveningColor = new Color(0.55f, 0.55f, 0.95f);
        [SerializeField] private float eveningThreshold = 0.75f;

        [Header("Rent Line")]
        [SerializeField] private TextMeshProUGUI rentText;
        [SerializeField] private Color okColor = new Color(0.1804f, 0.5451f, 0.2275f); // #2E8B3A
        [SerializeField] private Color shortColor = new Color(0.7843f, 0.1961f, 0.1686f); // #C8322B

        private bool _isEvening;
        private bool _iconInitialized;

        // Kira satırı cache — yalnız değişince yeniden yazılır (per-frame string alloc yok).
        private int _cachedDayInCycle = int.MinValue;
        private int _cachedMoney = int.MinValue;
        private int _cachedRent = int.MinValue;
        private bool _cachedIsRentDay;
        private bool _rentLineDirty = true;

        private void OnEnable()
        {
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            _rentLineDirty = true;
        }

        private void OnDisable()
        {
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        }

        private void Update()
        {
            var day = DayCycleManager.Instance;
            if (day == null) return; // spawn öncesi — sessizce bekle

            UpdateTimePie(day.DayProgress01);
            UpdateRentLine(day);
        }

        private void OnLocaleChanged(Locale locale)
        {
            _rentLineDirty = true;
        }

        private void UpdateTimePie(float progress01)
        {
            if (timePie != null)
            {
                timePie.fillAmount = progress01;
            }

            bool evening = progress01 >= eveningThreshold;
            if (evening != _isEvening || !_iconInitialized)
            {
                _isEvening = evening;
                _iconInitialized = true;
                if (timeIcon != null)
                {
                    var sprite = evening ? moonSprite : sunSprite;
                    if (timeIcon.sprite != sprite)
                    {
                        timeIcon.sprite = sprite;
                    }
                    // Sahnede alpha 0 bırakılmış olsa bile sprite atanınca görünür olsun.
                    timeIcon.enabled = sprite != null;
                    if (sprite != null && timeIcon.color.a < 1f)
                    {
                        var c = timeIcon.color;
                        c.a = 1f;
                        timeIcon.color = c;
                    }
                }
            }

            if (timePie != null)
            {
                // Eşikten önceki son %10'da akşam rengine yumuşak geçiş.
                float t = Mathf.InverseLerp(eveningThreshold - 0.1f, eveningThreshold, progress01);
                timePie.color = Color.Lerp(dayColor, eveningColor, t);
            }
        }

        private void UpdateRentLine(DayCycleManager day)
        {
            var money = MoneySystem.Instance;
            int currentMoney = money != null ? money.CurrentMoney : 0;
            int rent = day.NextRentAmount;
            bool isRentDay = day.IsRentDay;
            int dayInCycle = day.DayInRentCycle;

            if (!_rentLineDirty &&
                dayInCycle == _cachedDayInCycle &&
                currentMoney == _cachedMoney &&
                rent == _cachedRent &&
                isRentDay == _cachedIsRentDay)
            {
                return;
            }

            _cachedDayInCycle = dayInCycle;
            _cachedMoney = currentMoney;
            _cachedRent = rent;
            _cachedIsRentDay = isRentDay;
            _rentLineDirty = false;

            if (rentText == null) return;

            // Server kira önizlemesini henüz yayınlamadıysa (0) yanıltıcı "0/0" gösterme.
            if (rent <= 0)
            {
                rentText.text = string.Empty;
                return;
            }

            rentText.text = isRentDay
                ? $"{GetRentDueText()}   {currentMoney}/{rent}"
                : $"{dayInCycle}/{day.RentIntervalDays}   {currentMoney}/{rent}";

            rentText.color = currentMoney < rent ? shortColor : okColor;
        }

        private static string GetRentDueText()
        {
            string s = LocalizationHelper.GetLocalizedString(RENT_DUE_KEY);
            // GetLocalizedString anahtar bulunamazsa key'in kendisini döner — o durumda İngilizce fallback.
            return string.IsNullOrEmpty(s) || s == RENT_DUE_KEY ? RENT_DUE_FALLBACK : s;
        }
    }
}
