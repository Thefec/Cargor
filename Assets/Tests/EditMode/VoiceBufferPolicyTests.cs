using NUnit.Framework;
using NewCss.Voice.Core;

public class VoiceBufferPolicyTests
{
    [Test]
    public void MillisecondsToSamples_SamplesToMilliseconds_RoundTrip()
    {
        int samples = VoiceBufferPolicy.MillisecondsToSamples(120.0, 48000);
        Assert.AreEqual(5760, samples);

        double ms = VoiceBufferPolicy.SamplesToMilliseconds(samples, 48000);
        Assert.AreEqual(120.0, ms, 0.001);
    }

    [Test]
    public void TargetDelaySamples_ScalesWithSampleRate_NeverHardcodes48000()
    {
        // Beklenen değer SABİTTEN türetiliyor, elle yazılmıyor: TargetDelayMs bir AYAR knob'u
        // (2026-08-08'de ölçüme dayanarak 120→260 değişti) ve testin onu her değişiminde kırılması
        // gerekmiyor. Test edilen davranış "örnek sayısı ms ve sample rate ile doğru ölçekleniyor mu",
        // ayarın kendisi değil.
        int expected48 = (int)(VoiceBufferPolicy.TargetDelayMs / 1000.0 * 48000);
        int expected24 = (int)(VoiceBufferPolicy.TargetDelayMs / 1000.0 * 24000);

        Assert.AreEqual(expected48, VoiceBufferPolicy.TargetDelaySamples(48000));
        Assert.AreEqual(expected24, VoiceBufferPolicy.TargetDelaySamples(24000));
        Assert.AreEqual(expected48, expected24 * 2, "48 kHz'de 24 kHz'in tam iki katı örnek olmalı");
    }

    [Test]
    public void Thresholds_AreOrderedAndFitCapacity()
    {
        // Ayar değerleri arasındaki İLİŞKİLER değişmez olmalı — hangi sayılar seçilirse seçilsin.
        Assert.Less(VoiceBufferPolicy.ResumeDelayMs, VoiceBufferPolicy.TargetDelayMs,
            "devam eşiği hedeften küçük olmalı, yoksa asimetrik kapının anlamı kalmaz");
        Assert.Greater(VoiceBufferPolicy.OverrunCeilingMs, VoiceBufferPolicy.TargetDelayMs,
            "tavan hedeften büyük olmalı");
        Assert.Less(VoiceBufferPolicy.OverrunCeilingMs, 1000.0,
            "tavan slot kapasitesini (RadioVoiceSpeakerSlot.TargetBufferSeconds = 1.0s) aşamaz");
        Assert.Less(VoiceBufferPolicy.DriftLowerMs, VoiceBufferPolicy.DriftUpperMs);
        Assert.Greater(VoiceBufferPolicy.DriftLowerMs, 0.0);
    }

    [Test]
    public void EvaluateDrift_BelowSustainDuration_ReturnsNoneRegardlessOfOccupancy()
    {
        // Aşırı doluluk/boşluk olsa da 1 s'den kısa sürede tepki verilmemeli — anlık jitter'a
        // tepki vermek titremeyi azaltmaz, arttırır.
        Assert.AreEqual(VoiceDriftAction.None, VoiceBufferPolicy.EvaluateDrift(500.0, 0.5));
        Assert.AreEqual(VoiceDriftAction.None, VoiceBufferPolicy.EvaluateDrift(5.0, 0.9));
    }

    // Aşağıdaki üç test de eşikleri SABİTTEN türetiyor. Önceden mutlak sayılar (200 / 50 / 120)
    // yazılmıştı ve TargetDelayMs 120→260 olunca üçü de kırıldı — oysa DAVRANIŞ hiç değişmemişti,
    // yalnızca ayar değişmişti. Test bir ayar knob'unun değerini değil, karar mantığını doğrulamalı.
    private const double Sustained = VoiceBufferPolicy.DriftSustainSeconds + 0.5;

    [Test]
    public void EvaluateDrift_AboveUpperThresholdSustained_ReturnsDropChunk()
    {
        Assert.AreEqual(VoiceDriftAction.DropChunk,
            VoiceBufferPolicy.EvaluateDrift(VoiceBufferPolicy.DriftUpperMs + 1.0, Sustained));
    }

    [Test]
    public void EvaluateDrift_BelowLowerThresholdSustained_ReturnsInsertSilence()
    {
        Assert.AreEqual(VoiceDriftAction.InsertSilence,
            VoiceBufferPolicy.EvaluateDrift(VoiceBufferPolicy.DriftLowerMs - 1.0, Sustained));
    }

    [Test]
    public void EvaluateDrift_WithinBandSustained_ReturnsNone()
    {
        // Bandın tam ortası — hangi eşikler seçilirse seçilsin "dokunma" demeli.
        double mid = (VoiceBufferPolicy.DriftLowerMs + VoiceBufferPolicy.DriftUpperMs) / 2.0;
        Assert.AreEqual(VoiceDriftAction.None, VoiceBufferPolicy.EvaluateDrift(mid, Sustained));
    }
}
