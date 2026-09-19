using TMPro;
using UnityEngine;
using NewCss.Audio;

namespace NewCss
{
    /// <summary>
    /// Bir TMP_Text'e takılıp değeri "sayarak" (count-up) gösterir, artış/azalışta
    /// renk flaşı + punch scale tetikler. NumberRoller'ı sürer (bkz. Core/NumberRoller.cs).
    /// Sahnede bu componentin eklenmesi/bağlanması bu iş kapsamında YAPILMADI — bkz. rapor.
    ///
    /// Faz B (bkz. plans/ses-tasarimi.md §3): sayaç her tam sayıyı geçtiğinde SfxId.CounterTick
    /// çalar. En çok hissedilen juice olduğu için hız sınırlı (COUNTER_TICK_MIN_INTERVAL) ve
    /// hafif pitch varyasyonlu — hızlı sayarken her rakamda çalıp rahatsız etmesin.
    /// </summary>
    [DisallowMultipleComponent]
    public class NumberRollDisplay : MonoBehaviour
    {
        private const float COUNTER_TICK_MIN_INTERVAL = 0.05f;
        private const float COUNTER_TICK_PITCH_VARIATION = 0.08f;

        [Header("Hedef Text")]
        [Tooltip("Sayının yazılacağı TMP_Text. Boş bırakılırsa bu objedeki TMP_Text otomatik alınır.")]
        [SerializeField] private TMP_Text targetText;

        [Header("Format")]
        [SerializeField] private string prefix = "";
        [SerializeField] private string suffix = "";
        [Tooltip("Gösterilecek ondalık basamak sayısı.")]
        [SerializeField, Min(0)] private int decimals = 0;

        [Header("Zamanlama")]
        [Tooltip("Time.timeScale = 0 iken de sayaç ilerlesin (duraklatma/menü).")]
        [SerializeField] private bool useUnscaledTime = true;

        [Header("Renk Flaşı")]
        [SerializeField] private Color increaseColor = new Color(0.4f, 0.9f, 0.4f);
        [SerializeField] private Color decreaseColor = new Color(0.9f, 0.3f, 0.3f);
        [SerializeField, Min(0f)] private float flashDuration = 0.35f;

        [Header("Punch Scale")]
        [SerializeField, Min(1f)] private float punchScale = 1.10f;
        [SerializeField, Min(0f)] private float punchDuration = 0.25f;

        private NumberRoller _roller;

        // Prefab/sahnedeki gerçek renk ve ölçek bir kez cache'lenir — UIPopInAnimation'daki
        // _baseScaleCached deseniyle aynı: animasyon ortasında obje kapanırsa bozuk değer
        // "gerçek değer" sanılmasın.
        private Color _baseColor = Color.white;
        private bool _baseColorCached;
        private Vector3 _baseScale = Vector3.one;
        private bool _baseScaleCached;

        private bool _hasValue;
        private float _lastTarget;

        // CounterTick: bir önceki karede gösterilen tam sayı — değiştiğinde (animasyon sırasında)
        // "tık" sesi tetiklenir. SetValueInstant'ta senkronlanır ki spawn/ilk değer tık çalmasın.
        private int _lastTickInt;
        private bool _lastTickIntCached;

        private bool _flashing;
        private float _flashElapsed;
        private Color _flashFromColor;

        private bool _punching;
        private float _punchElapsed;

        private void Awake()
        {
            EnsureReady();
        }

        /// <summary>
        /// Awake calismadan (ornegin ayni frame Instantiate edilip hemen SetValue
        /// cagrilirsa) gelen cagrilarda NRE olmasin diye tembel kurulum.
        /// </summary>
        private void EnsureReady()
        {
            if (targetText == null)
                targetText = GetComponent<TMP_Text>();

            CacheBase();

            if (_roller == null)
                _roller = new NumberRoller();
        }

        private void OnDisable()
        {
            // Animasyon yarıda kesilirse renk/ölçek gerçek değerinde kalsın.
            _flashing = false;
            _punching = false;

            if (_baseColorCached && targetText != null)
                targetText.color = _baseColor;
            if (_baseScaleCached)
                transform.localScale = _baseScale;
        }

        private void CacheBase()
        {
            if (_baseColorCached) return;

            if (targetText != null)
                _baseColor = targetText.color;
            _baseScale = transform.localScale;

            _baseColorCached = true;
            _baseScaleCached = true;
        }

        /// <summary>Yeni değere animasyonlu geçer; artışta yeşil, azalışta kırmızı flaş + punch atar.</summary>
        public void SetValue(float value)
        {
            EnsureReady();

            if (!_hasValue)
            {
                SetValueInstant(value);
                return;
            }

            float delta = value - _lastTarget;
            _lastTarget = value;
            _roller.SetTarget(value);

            if (delta > 0f)
            {
                TriggerFlash(increaseColor);
                TriggerPunch();
            }
            else if (delta < 0f)
            {
                TriggerFlash(decreaseColor);
                TriggerPunch();
            }
        }

        /// <summary>Animasyonsuz anında oturtur (ör. spawn/ilk değer). Görsel efekt tetiklemez.</summary>
        public void SetValueInstant(float value)
        {
            EnsureReady();

            _lastTarget = value;
            _hasValue = true;
            _roller.SnapTo(value);

            _flashing = false;
            _punching = false;
            if (targetText != null)
                targetText.color = _baseColor;
            transform.localScale = _baseScale;

            // Spawn/ilk değer "tık" çalmasın — tam sayıyı burada senkronla, Update() bunu
            // bir sonraki animasyondan önceki gerçek başlangıç sanır.
            _lastTickInt = Mathf.RoundToInt(_roller.Displayed);
            _lastTickIntCached = true;

            WriteText();
        }

        private void Update()
        {
            if (_roller == null) return;

            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

            _roller.Tick(dt);
            WriteText();
            TickCounterSound();

            TickFlash(dt);
            TickPunch(dt);
        }

        /// <summary>Animasyon sırasında gösterilen tam sayı değiştiğinde CounterTick çalar.
        /// SnapTo/instant değerlerde (animasyon yok) tetiklenmez — sadece "sayarak" ilerlerken.</summary>
        private void TickCounterSound()
        {
            if (!_roller.IsAnimating) return;

            int currentInt = Mathf.RoundToInt(_roller.Displayed);
            if (_lastTickIntCached && currentInt == _lastTickInt) return;

            _lastTickInt = currentInt;
            _lastTickIntCached = true;

            SfxBus.Play(SfxId.CounterTick, COUNTER_TICK_MIN_INTERVAL, COUNTER_TICK_PITCH_VARIATION);
        }

        private void WriteText()
        {
            if (targetText == null) return;

            string formatted = _roller.Displayed.ToString("F" + decimals);
            targetText.text = $"{prefix}{formatted}{suffix}";
        }

        private void TriggerFlash(Color color)
        {
            if (targetText == null || flashDuration <= 0f) return;

            _flashFromColor = color;
            _flashElapsed = 0f;
            _flashing = true;
            targetText.color = _flashFromColor;
        }

        private void TickFlash(float dt)
        {
            if (!_flashing || targetText == null) return;

            _flashElapsed += dt;
            float t = Mathf.Clamp01(_flashElapsed / flashDuration);

            if (t >= 1f)
            {
                targetText.color = _baseColor;
                _flashing = false;
                return;
            }

            // Ease-out: flaş rengi hızlı görünür, orijinale yumuşak döner.
            float eased = 1f - (1f - t) * (1f - t);
            targetText.color = Color.Lerp(_flashFromColor, _baseColor, eased);
        }

        private void TriggerPunch()
        {
            if (punchDuration <= 0f) return;

            _punchElapsed = 0f;
            _punching = true;
        }

        private void TickPunch(float dt)
        {
            if (!_punching) return;

            _punchElapsed += dt;
            float t = Mathf.Clamp01(_punchElapsed / punchDuration);

            if (t >= 1f)
            {
                transform.localScale = _baseScale;
                _punching = false;
                return;
            }

            // 0 -> punchScale -> 1 üçgen eğrisi: yarı sürede tepe noktasına çıkar, geri iner.
            float curve = t < 0.5f ? (t / 0.5f) : (1f - (t - 0.5f) / 0.5f);
            float scale = Mathf.LerpUnclamped(1f, punchScale, curve);
            transform.localScale = _baseScale * scale;
        }
    }
}
