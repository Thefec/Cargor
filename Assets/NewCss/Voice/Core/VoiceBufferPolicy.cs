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
        /// <summary>
        /// Burst ORTASINDA underrun'dan sonra playout'un yeniden başlaması için gereken doluluk.
        ///
        /// NEDEN <see cref="TargetDelayMs"/>'den küçük (asimetrik kapı / histerezis): playout kapısı
        /// AĞ JITTER'ı için tasarlandı, ama Steam'in kendi ses-aktivitesi tespiti var — konuşurken
        /// duraklayınca Steam hiç veri vermiyor, paket üretilmiyor ve buffer doğal olarak kuruyor.
        /// Bu "kaynak boşluğu"nu bir ağ tıkanıklığı gibi ele alıp kapıyı TAM 120 ms'e yeniden kurmak,
        /// her kelime arasında sonraki kelimenin ilk 120 ms'ini yutuyordu → duyulan belirti:
        /// "ses kesik kesik geliyor" (2026-08-08 teşhisi: under=14, over=6).
        ///
        /// Çözüm: burst BAŞINDA tam <see cref="TargetDelayMs"/> beklenir (gerçek jitter koruması orada
        /// gerekli), burst ORTASINDAKİ duraklamadan sonra tek paket yetmesi için bu küçük eşik kullanılır.
        /// 40 ms, yakalama aralığından (33.3 ms @ 30 Hz) biraz büyük — yani "bir paket + pay".
        /// Burst bittiğinde eşik tam hedefe geri döner (VoiceRingBuffer.NotifyBurstEnded).
        /// </summary>
        public const double ResumeDelayMs = 40.0;

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

        public static int ResumeDelaySamples(int sampleRate) => MillisecondsToSamples(ResumeDelayMs, sampleRate);

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
