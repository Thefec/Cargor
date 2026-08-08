using System;

namespace NewCss.Voice.Core
{
    /// <summary>
    /// Jitter buffer'ın doluluk ORTALAMASINI izleyip yavaş sürüklenmeyi (drift) düzeltme kararı üretir.
    ///
    /// NEDEN GEREKLİ — 2026-08-08 teşhisi: <see cref="VoiceBufferPolicy.EvaluateDrift"/> yazılmış ve
    /// test edilmişti ama runtime'da HİÇBİR YERDEN ÇAĞRILMIYORDU (ölü kod). Yani sıfır drift düzeltmesi
    /// çalışıyordu. Belirti: kısa konuşma temiz, UZUN konuşmada bozulma. Mekanizma: asimetrik kapı
    /// yüzünden ilk underrun'dan sonra slot burst'ün kalanı boyunca <see cref="VoiceBufferPolicy.ResumeDelayMs"/>
    /// (120 ms) marjıyla çalışıyor, <see cref="VoiceBufferPolicy.TargetDelayMs"/> (260 ms) ile değil —
    /// yani giderek daha kırılgan hâle geliyor ve marjı geri toplayacak bir mekanizma yok.
    ///
    /// Bu sınıf o mekanizma: doluluk ortalaması hedef bandın dışına çıkıp ORADA KALIRSA (anlık jitter'a
    /// tepki verilmez, <see cref="VoiceBufferPolicy.DriftSustainSeconds"/> boyunca sürmesi gerekir)
    /// tek bir düzeltme adımı önerir — fazlaysa parça düşür, azsa sessizlik ekle.
    ///
    /// SAF MANTIK: motor referansı yok, zaman parametre olarak gelir → EditMode'da test edilebilir.
    /// Tek thread'den (ana thread) kullanılmak üzere tasarlandı, kilit içermez.
    /// </summary>
    public sealed class VoiceDriftTracker
    {
        /// <summary>
        /// Doluluk ortalamasının yumuşatma katsayısı (EMA). Küçük değer = daha yavaş/kararlı ortalama.
        /// Anlık doluluk her frame paket gelişine göre zıplıyor; drift kararı bu gürültüye değil
        /// EĞİLİME bakmalı.
        /// </summary>
        private const double SmoothingPerSecond = 2.0;

        private double _averageMs;
        private bool _hasSample;

        /// <summary>Ortalamanın bandın AYNI tarafında kesintisiz kaç saniyedir durduğu.</summary>
        private double _sustainedSeconds;

        /// <summary>Sürenin hangi yön için biriktiği — yön değişirse sayaç sıfırlanır.</summary>
        private VoiceDriftAction _sustainedDirection = VoiceDriftAction.None;

        public double AverageMs => _averageMs;
        public double SustainedSeconds => _sustainedSeconds;

        /// <summary>Yeni konuşmacı / yeniden yapılandırma: geçmişi tamamen at.</summary>
        public void Reset()
        {
            _averageMs = 0.0;
            _hasSample = false;
            _sustainedSeconds = 0.0;
            _sustainedDirection = VoiceDriftAction.None;
        }

        /// <summary>
        /// Her frame (ana thread) çağrılır. <paramref name="active"/> false ise (slot boşta ya da
        /// playout kapısı kapalı) takip DURUR ve geçmiş sıfırlanır — aksi hâlde boştaki bir slotun
        /// doluluğu 0 olduğu için sürekli "sessizlik ekle" önerilirdi.
        /// </summary>
        /// <returns>Uygulanacak tek adımlık düzeltme; genellikle <see cref="VoiceDriftAction.None"/>.</returns>
        public VoiceDriftAction Update(double occupancyMs, double deltaSeconds, bool active)
        {
            if (!active)
            {
                Reset();
                return VoiceDriftAction.None;
            }

            if (deltaSeconds <= 0.0) return VoiceDriftAction.None;

            // EMA — ilk örnekte doğrudan o değere otur (aksi hâlde 0'dan yavaşça tırmanır ve
            // konuşmanın başında yanlışlıkla "sessizlik ekle" önerilir).
            if (!_hasSample)
            {
                _averageMs = occupancyMs;
                _hasSample = true;
            }
            else
            {
                double alpha = 1.0 - Math.Exp(-SmoothingPerSecond * deltaSeconds);
                if (alpha > 1.0) alpha = 1.0;
                _averageMs += (occupancyMs - _averageMs) * alpha;
            }

            // Bandın hangi tarafındayız? (Süreden bağımsız yön tespiti için sustain'i aşan bir
            // değerle sorguluyoruz — EvaluateDrift hem bandı hem süreyi birlikte denetliyor.)
            var direction = VoiceBufferPolicy.EvaluateDrift(_averageMs, VoiceBufferPolicy.DriftSustainSeconds + 1.0);

            if (direction == VoiceDriftAction.None)
            {
                // Banda geri döndü — biriken süre geçersiz.
                _sustainedSeconds = 0.0;
                _sustainedDirection = VoiceDriftAction.None;
                return VoiceDriftAction.None;
            }

            if (direction != _sustainedDirection)
            {
                _sustainedDirection = direction;
                _sustainedSeconds = 0.0;
            }

            _sustainedSeconds += deltaSeconds;
            if (_sustainedSeconds < VoiceBufferPolicy.DriftSustainSeconds) return VoiceDriftAction.None;

            // Düzeltme uygulanıyor: süreyi sıfırla, böylece bir sonraki adım için yeniden
            // DriftSustainSeconds beklenir (tek seferde ard arda çok parça düşürülmesin).
            _sustainedSeconds = 0.0;
            return direction;
        }
    }
}
