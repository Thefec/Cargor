using UnityEngine;

namespace NewCss
{
    /// <summary>
    /// Obje aktif edildiğinde küçükten büyüğe "pop" animasyonu oynatır.
    /// SetActive(true) çağıran kodu değiştirmeye gerek yok; animasyon OnEnable
    /// üzerinden kendiliğinden tetiklenir (müşteri wait bar canvas'ı gibi).
    /// </summary>
    [DisallowMultipleComponent]
    public class UIPopInAnimation : MonoBehaviour
    {
        [Header("Pop Ayarları")]
        [Tooltip("Animasyon süresi (saniye). 0 = animasyon yok.")]
        [SerializeField, Min(0f)] private float duration = 0.25f;

        [Tooltip("Başlangıç ölçeği (hedef ölçeğin çarpanı). 0 = tamamen yokluktan büyür.")]
        [SerializeField, Range(0f, 1f)] private float startScale = 0f;

        [Tooltip("Hedefi aşma miktarı. 0 = taşma yok (ease-out cubic), 1.7 ≈ %10 taşma.")]
        [SerializeField, Range(0f, 4f)] private float overshoot = 1.70158f;

        [Tooltip("Time.timeScale = 0 iken de oynasın (duraklatma/menü).")]
        [SerializeField] private bool useUnscaledTime = true;

        private Vector3 _baseScale = Vector3.one;
        private bool _baseScaleCached;
        private float _elapsed;
        private bool _playing;

        private void Awake()
        {
            CacheBaseScale();
        }

        /// <summary>
        /// Prefab'daki gerçek ölçeği bir kez sakla. Animasyon ortasında obje
        /// kapanırsa küçülmüş ölçeği "gerçek ölçek" sanmamak için tek seferlik.
        /// </summary>
        private void CacheBaseScale()
        {
            if (_baseScaleCached) return;

            _baseScale = transform.localScale;
            _baseScaleCached = true;
        }

        private void OnEnable()
        {
            CacheBaseScale();

            if (duration <= 0f)
            {
                transform.localScale = _baseScale;
                _playing = false;
                return;
            }

            _elapsed = 0f;
            _playing = true;
            transform.localScale = _baseScale * startScale;
        }

        private void OnDisable()
        {
            // Animasyon yarıda kesilirse obje küçük ölçekte kalmasın.
            _playing = false;
            transform.localScale = _baseScale;
        }

        private void Update()
        {
            if (!_playing) return;

            _elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(duration > 0f ? _elapsed / duration : 1f);

            if (t >= 1f)
            {
                transform.localScale = _baseScale;
                _playing = false;
                return;
            }

            float eased = EaseOutBack(t);
            transform.localScale = _baseScale * Mathf.LerpUnclamped(startScale, 1f, eased);
        }

        /// <summary>
        /// Standart ease-out-back: sona doğru hedefi biraz aşıp yerine oturur.
        /// overshoot = 0 iken saf ease-out cubic'e döner.
        /// </summary>
        private float EaseOutBack(float t)
        {
            float c1 = overshoot;
            float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }
}
