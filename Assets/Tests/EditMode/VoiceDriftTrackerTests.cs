using NUnit.Framework;
using NewCss.Voice.Core;

/// <summary>
/// 2026-08-08 regresyonu: EvaluateDrift yazılmış+test edilmişti ama runtime'da HİÇ ÇAĞRILMIYORDU.
/// Belirti: kısa konuşma temiz, uzun konuşmada bozulma. Bu testler takipçinin gerçekten karar
/// ürettiğini ve gürültüye tepki vermediğini doğrular.
/// Eşikler SABİTLERDEN türetiliyor — ayar değişince testler kırılmasın (bkz. VoiceBufferPolicyTests).
/// </summary>
public class VoiceDriftTrackerTests
{
    private const double Dt = 0.1;
    private static double AboveBand => VoiceBufferPolicy.DriftUpperMs + 20.0;
    private static double BelowBand => VoiceBufferPolicy.DriftLowerMs - 20.0;
    private static double InBand =>
        (VoiceBufferPolicy.DriftLowerMs + VoiceBufferPolicy.DriftUpperMs) / 2.0;

    private static VoiceDriftAction Feed(VoiceDriftTracker t, double occupancyMs, double seconds)
    {
        var last = VoiceDriftAction.None;
        int steps = (int)(seconds / Dt);
        for (int i = 0; i < steps; i++)
        {
            var a = t.Update(occupancyMs, Dt, active: true);
            if (a != VoiceDriftAction.None) last = a;
        }
        return last;
    }

    [Test]
    public void Inactive_ResetsAndNeverActs()
    {
        var t = new VoiceDriftTracker();
        // Boşta slot: doluluk 0 (bandın çok altında) ama takip durmalı — aksi hâlde boştaki her slot
        // sürekli "sessizlik ekle" önerirdi.
        for (int i = 0; i < 100; i++)
            Assert.AreEqual(VoiceDriftAction.None, t.Update(0.0, Dt, active: false));
        Assert.AreEqual(0.0, t.SustainedSeconds);
    }

    [Test]
    public void WithinBand_NeverActs_HowLongEver()
    {
        var t = new VoiceDriftTracker();
        Assert.AreEqual(VoiceDriftAction.None, Feed(t, InBand, 10.0));
    }

    [Test]
    public void SustainedAboveBand_EventuallyDropsChunk()
    {
        var t = new VoiceDriftTracker();
        Assert.AreEqual(VoiceDriftAction.DropChunk,
            Feed(t, AboveBand, VoiceBufferPolicy.DriftSustainSeconds + 2.0));
    }

    [Test]
    public void SustainedBelowBand_EventuallyInsertsSilence()
    {
        var t = new VoiceDriftTracker();
        Assert.AreEqual(VoiceDriftAction.InsertSilence,
            Feed(t, BelowBand, VoiceBufferPolicy.DriftSustainSeconds + 2.0));
    }

    [Test]
    public void BriefExcursion_DoesNotAct()
    {
        // Sustain süresinin YARISI kadar bandın dışında kal, sonra geri dön — anlık jitter'a
        // tepki verilmemeli (titremeyi azaltmaz, arttırır).
        var t = new VoiceDriftTracker();
        Assert.AreEqual(VoiceDriftAction.None, Feed(t, AboveBand, VoiceBufferPolicy.DriftSustainSeconds * 0.5));
        Assert.AreEqual(VoiceDriftAction.None, Feed(t, InBand, 1.0));
    }

    [Test]
    public void DirectionFlip_SustainDoesNotLeakAcrossDirections()
    {
        // Yukarı yönde neredeyse tetikleyecek kadar bekle, sonra aşağı yöne geç.
        // Yukarıdan biriken süre TAŞINMAMALI: ilk gelen düzeltme DropChunk değil InsertSilence olmalı.
        var t = new VoiceDriftTracker();
        Feed(t, AboveBand, VoiceBufferPolicy.DriftSustainSeconds * 0.9);

        var action = Feed(t, BelowBand, VoiceBufferPolicy.DriftSustainSeconds + 2.0);
        Assert.AreEqual(VoiceDriftAction.InsertSilence, action,
            "yön değiştiğinde eski yönün birikmiş süresi yeni yönde tetikleme yapmamalı");
    }

    [Test]
    public void AfterActing_RequiresFullSustainAgain()
    {
        var t = new VoiceDriftTracker();
        Assert.AreEqual(VoiceDriftAction.DropChunk,
            Feed(t, AboveBand, VoiceBufferPolicy.DriftSustainSeconds + 0.5));
        // Hemen ardından tek adım: bir daha tetiklememeli (ard arda çok parça düşürülmesin).
        Assert.AreEqual(VoiceDriftAction.None, t.Update(AboveBand, Dt, active: true));
    }

    [Test]
    public void Reset_ClearsHistory()
    {
        var t = new VoiceDriftTracker();
        Feed(t, AboveBand, VoiceBufferPolicy.DriftSustainSeconds * 0.9);
        t.Reset();
        Assert.AreEqual(0.0, t.SustainedSeconds);
        Assert.AreEqual(VoiceDriftAction.None, t.Update(AboveBand, Dt, active: true));
    }
}
