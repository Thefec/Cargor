using System;
using System.Collections;
using UnityEngine;

namespace NewCss
{
    /// <summary>
    /// Eşya yerleştirme animasyonları için static utility sınıfı.
    /// Tüm etkileşimli yüzeylerde (raf, masa, sergi masası) standart "scale-up" animasyonu sağlar.
    /// DRY prensibi gereği animasyon mantığı burada merkezi olarak yönetilir.
    /// </summary>
    public static class ItemPlacementAnimator
    {
        #region Constants

        /// <summary>
        /// Varsayılan animasyon süresi (saniye)
        /// </summary>
        public const float DEFAULT_DURATION = 0.3f;

        private const string LOG_PREFIX = "[ItemPlacementAnimator]";

        #endregion

        #region Public API

        /// <summary>
        /// Bir MonoBehaviour üzerinden scale-up animasyonunu başlatır.
        /// Coroutine ownership host MonoBehaviour'a aittir.
        /// </summary>
        /// <param name="host">Coroutine'i çalıştıracak MonoBehaviour (null/disabled kontrolü yapılır)</param>
        /// <param name="target">Animasyon uygulanacak GameObject</param>
        /// <param name="duration">Animasyon süresi (saniye)</param>
        /// <returns>Başlatılan Coroutine veya başlatılamazsa null</returns>
        public static Coroutine PlayOn(MonoBehaviour host, GameObject target, float duration = DEFAULT_DURATION)
        {
            // Host validation
            if (host == null)
            {
                Debug.LogWarning($"{LOG_PREFIX} Cannot play animation - host is null");
                return null;
            }

            if (!host.isActiveAndEnabled)
            {
                Debug.LogWarning($"{LOG_PREFIX} Cannot play animation - host '{host.name}' is not active/enabled");
                return null;
            }

            // Target validation
            if (target == null)
            {
                Debug.LogWarning($"{LOG_PREFIX} Cannot play animation - target is null");
                return null;
            }

            return host.StartCoroutine(AnimateScaleUp(target, duration));
        }

        /// <summary>
        /// Scale-up animasyonu coroutine'i.
        /// Objeyi Vector3.zero'dan orijinal scale'ine SmoothStep easing ile büyütür.
        /// Her frame'de null kontrolü yapar — obje yok olursa güvenli şekilde durur.
        /// </summary>
        /// <param name="target">Animasyon uygulanacak GameObject</param>
        /// <param name="duration">Animasyon süresi (saniye)</param>
        /// <param name="onComplete">Animasyon tamamlandığında çağrılacak callback (opsiyonel)</param>
        public static IEnumerator AnimateScaleUp(GameObject target, float duration = DEFAULT_DURATION, Action onComplete = null)
        {
            // Early exit if target is already gone
            if (target == null) yield break;

            // Capture the original scale before zeroing
            Vector3 originalScale = target.transform.localScale;
            target.transform.localScale = Vector3.zero;

            float elapsed = 0f;

            while (elapsed < duration)
            {
                // Null check every frame — object may be destroyed mid-animation
                if (target == null) yield break;

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // SmoothStep easing: t² * (3 - 2t) for natural feel
                float smoothT = t * t * (3f - 2f * t);

                target.transform.localScale = Vector3.Lerp(Vector3.zero, originalScale, smoothT);
                yield return null;
            }

            // Final snap to exact scale (avoid floating point drift)
            if (target != null)
            {
                target.transform.localScale = originalScale;
            }

            // Invoke completion callback
            onComplete?.Invoke();
        }

        #endregion
    }
}
