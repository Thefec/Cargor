using System;

namespace NewCss.Voice.Core
{
    /// <summary>
    /// HUD'daki bir konuşmacı satırının görünürlük zamanlamasını hesaplayan saf (UnityEngine'siz)
    /// durum makinesi. Satır aktifken tam görünür (alpha=1); aktiflik kesilince <see cref="HoldSeconds"/>
    /// kadar tam görünür TUTULUR, ardından <see cref="FadeSeconds"/> boyunca alpha 1→0 lineer iner.
    ///
    /// NEDEN AYRI BİR HOLD SÜRESİ: konuşmacının slot'u zaten kendi 400ms'lik burst-timeout'una sahip
    /// (VoiceSequenceTracker.IsBurstStale, RadioVoicePlayback.Tick) — ama o süre "sesin ne zaman
    /// durduğunu" belirlemek için var, "satırın ekranda ne kadar okunabilir kalacağını" belirlemek
    /// için DEĞİL. İki kaygı kasıtlı olarak ayrı tutuldu: HUD tarafı MonoBehaviour'da bu timer'ı
    /// kullanır, ses tarafına hiç dokunmaz (plan §UI ve ayarlar: "titreme önleme").
    ///
    /// Saf mantık olduğu için burada test edilir (Core kuralı: UnityEngine referansı YOK) — MonoBehaviour
    /// tarafı sadece <see cref="ComputeAlpha"/> çıktısını okuyup bir CanvasGroup.alpha'ya yazar.
    /// </summary>
    public sealed class VoiceHudRowTimer
    {
        public double HoldSeconds { get; }
        public double FadeSeconds { get; }

        private double _lastActiveTime = double.NegativeInfinity;
        private bool _everActive;

        public VoiceHudRowTimer(double holdSeconds, double fadeSeconds)
        {
            if (holdSeconds < 0) throw new ArgumentOutOfRangeException(nameof(holdSeconds));
            if (fadeSeconds < 0) throw new ArgumentOutOfRangeException(nameof(fadeSeconds));

            HoldSeconds = holdSeconds;
            FadeSeconds = fadeSeconds;
        }

        /// <summary>Konuşmacı bu frame aktifse (paket alıyor) çağrılır — hold/fade sayacını en yeni ana sıfırlar.</summary>
        public void MarkActive(double now)
        {
            _lastActiveTime = now;
            _everActive = true;
        }

        /// <summary>0 (tamamen solmuş) .. 1 (tam görünür) arası alpha. Hiç aktif olunmadıysa 0.</summary>
        public float ComputeAlpha(double now)
        {
            if (!_everActive) return 0f;

            double sinceActive = now - _lastActiveTime;
            if (sinceActive <= HoldSeconds) return 1f;

            double fadeElapsed = sinceActive - HoldSeconds;
            if (FadeSeconds <= 0.0 || fadeElapsed >= FadeSeconds) return 0f;

            return (float)(1.0 - fadeElapsed / FadeSeconds);
        }

        /// <summary>Satır tamamen solmuş ve havuza geri verilebilir mi (en az bir kez aktif olmuş VE artık alpha=0).</summary>
        public bool IsFullyFaded(double now) => _everActive && ComputeAlpha(now) <= 0f;

        /// <summary>Havuza geri dönerken (RadioHudController) çağrılır — yeni bir konuşmacıya atanmadan önce sıfırlanır.</summary>
        public void Reset()
        {
            _lastActiveTime = double.NegativeInfinity;
            _everActive = false;
        }
    }
}
