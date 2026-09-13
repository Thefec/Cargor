using System;

namespace NewCss
{
    /// <summary>
    /// HUD'daki para/prestij gibi sayıları "1 2 3 4..." diye sayarak hedefe taşıyan,
    /// sahne/prefab bağımlılığı olmayan sade motor (MonoBehaviour DEĞİL).
    /// Dışarıdan her frame Tick(deltaTime) ile beslenir; UnityEngine'e bağımlı değildir,
    /// bu yüzden EditMode testinden doğrudan çağrılabilir (bkz. NewCss.UI.Core.asmdef,
    /// noEngineReferences=true — Voice/Core ve Rooms/Core ile aynı desen).
    /// </summary>
    public class NumberRoller
    {
        private const float BaseDuration = 0.35f;
        private const float MaxDuration = 0.6f;

        // Büyük sıçramalarda süre hafif uzasın ama MaxDuration'da tavanlansın.
        private const float DurationPerUnitDiff = 0.0015f;

        private const float Epsilon = 0.0001f;

        private float _from;
        private float _to;
        private float _displayed;
        private float _duration;
        private float _elapsed;
        private bool _isAnimating;

        /// <summary>O anki gösterilecek değer (animasyon sırasında tam sayıya yuvarlanmış).</summary>
        public float Displayed => _displayed;

        public bool IsAnimating => _isAnimating;

        public NumberRoller(float initial = 0f)
        {
            _displayed = initial;
            _from = initial;
            _to = initial;
        }

        /// <summary>
        /// Yeni hedefe doğru sayma animasyonunu başlatır. Animasyon zaten sürüyorsa
        /// o anki gösterilen değerden devam eder — zıplama olmaz.
        /// </summary>
        public void SetTarget(float value)
        {
            _from = _displayed;
            _to = value;
            _elapsed = 0f;

            float diff = Math.Abs(_to - _from);

            if (diff < Epsilon)
            {
                _displayed = _to;
                _isAnimating = false;
                return;
            }

            _duration = Math.Min(BaseDuration + diff * DurationPerUnitDiff, MaxDuration);
            _isAnimating = true;
        }

        /// <summary>Animasyonu bir kare ilerletir. IsAnimating false ise no-op.</summary>
        public void Tick(float deltaTime)
        {
            if (!_isAnimating) return;

            _elapsed += deltaTime;
            float t = _duration > 0f ? Clamp01(_elapsed / _duration) : 1f;

            if (t >= 1f)
            {
                // Float sürüklenmesi kalmasın: son kare hedefe TAM oturur.
                _displayed = _to;
                _isAnimating = false;
                return;
            }

            float eased = EaseOutCubic(t);
            float value = _from + (_to - _from) * eased;

            // Ara değerler gerçekten "sayarak" ilerlesin (3.72 değil, tam sayı).
            _displayed = (float)Math.Round(value, MidpointRounding.AwayFromZero);
        }

        /// <summary>Animasyonsuz anında hedefe oturtur (ör. ilk spawn değeri).</summary>
        public void SnapTo(float value)
        {
            _from = value;
            _to = value;
            _displayed = value;
            _elapsed = 0f;
            _isAnimating = false;
        }

        private static float EaseOutCubic(float t)
        {
            float u = t - 1f;
            return 1f + u * u * u;
        }

        private static float Clamp01(float v)
        {
            if (v < 0f) return 0f;
            if (v > 1f) return 1f;
            return v;
        }
    }
}
