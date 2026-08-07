using System;

namespace NewCss.Voice.Core
{
    /// <summary>Doluluk/süre gözlemine göre jitter buffer'ın alması gereken düzeltme kararı.</summary>
    public enum VoiceDriftAction
    {
        None,
        DropChunk,
        InsertSilence,
    }

    /// <summary>
    /// Telsiz jitter buffer'ının eşik sabitleri + ms&lt;-&gt;örnek dönüşümleri + saf drift karar
    /// fonksiyonu. Hiçbir yerde örnekleme hızı hardcode EDİLMEZ — proje `AudioManager.asset`'te
    /// `m_SampleRate: 0` (sistem varsayılanı) kullanıyor, yani 48000 sabiti yanlış donanımda
    /// perde kaymasına yol açar. Bu yüzden her ms/örnek dönüşümü çağıranın verdiği `sampleRate`'e
    /// göre hesaplanır.
    /// </summary>
    public static class VoiceBufferPolicy
    {
        /// <summary>
        /// Hedef gecikme (prebuffer). DSP bloğu ~21.3 ms × en az 3 pay + 1 yakalama aralığı (33 ms)
        /// + Steam relay jitter'ı toplamından türetildi. Dev-tunable sabit; kullanıcı ayarı DEĞİL.
        /// </summary>
        public const double TargetDelayMs = 120.0;

        /// <summary>Bu doluluğun üstünde en eski örnekler atılır (overrun).</summary>
        public const double OverrunCeilingMs = TargetDelayMs + 250.0;

        /// <summary>Sürekli bu doluluğun üstünde kalınırsa (1 s) küçük parçalar düşürülerek gecikme geri çekilir.</summary>
        public const double DriftUpperMs = TargetDelayMs + 60.0;

        /// <summary>Sürekli bu doluluğun altında kalınırsa (1 s) küçük sessizlik parçaları eklenerek gecikme büyütülür.</summary>
        public const double DriftLowerMs = TargetDelayMs - 40.0;

        /// <summary>Drift eşikleri ancak bu kadar süre sürdürülürse tetiklenir — anlık jitter'a tepki vermek titremeyi azaltmaz, arttırır.</summary>
        public const double DriftSustainSeconds = 1.0;

        /// <summary>Drift düzeltmesinde düşürülen/eklenen parça boyutu.</summary>
        public const double ChunkMs = 10.0;

        public static int MillisecondsToSamples(double milliseconds, int sampleRate)
        {
            if (sampleRate <= 0) return 0;
            return (int)Math.Round(milliseconds / 1000.0 * sampleRate);
        }

        public static double SamplesToMilliseconds(int samples, int sampleRate)
        {
            if (sampleRate <= 0) return 0.0;
            return samples * 1000.0 / sampleRate;
        }

        public static int TargetDelaySamples(int sampleRate) => MillisecondsToSamples(TargetDelayMs, sampleRate);

        public static int OverrunCeilingSamples(int sampleRate) => MillisecondsToSamples(OverrunCeilingMs, sampleRate);

        public static int ChunkSamples(int sampleRate) => MillisecondsToSamples(ChunkMs, sampleRate);

        /// <summary>
        /// Saf karar fonksiyonu: doluluk ortalaması (ms) + bu durumun ne kadar sürdüğü (s) →
        /// "düşür" / "ekle" / "dokunma". Saf olduğu için (yan etkisiz, sadece girdiye bağlı)
        /// headless testte hiçbir mock/zaman simülasyonu gerekmeden doğrudan sınanabilir.
        /// </summary>
        public static VoiceDriftAction EvaluateDrift(double averageOccupancyMs, double sustainedSeconds)
        {
            if (sustainedSeconds < DriftSustainSeconds) return VoiceDriftAction.None;
            if (averageOccupancyMs >= DriftUpperMs) return VoiceDriftAction.DropChunk;
            if (averageOccupancyMs <= DriftLowerMs) return VoiceDriftAction.InsertSilence;
            return VoiceDriftAction.None;
        }
    }
}
