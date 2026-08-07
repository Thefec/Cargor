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
        Assert.AreEqual(5760, VoiceBufferPolicy.TargetDelaySamples(48000));
        Assert.AreEqual(2880, VoiceBufferPolicy.TargetDelaySamples(24000));
    }

    [Test]
    public void EvaluateDrift_BelowSustainDuration_ReturnsNoneRegardlessOfOccupancy()
    {
        // Aşırı doluluk/boşluk olsa da 1 s'den kısa sürede tepki verilmemeli — anlık jitter'a
        // tepki vermek titremeyi azaltmaz, arttırır.
        Assert.AreEqual(VoiceDriftAction.None, VoiceBufferPolicy.EvaluateDrift(500.0, 0.5));
        Assert.AreEqual(VoiceDriftAction.None, VoiceBufferPolicy.EvaluateDrift(5.0, 0.9));
    }

    [Test]
    public void EvaluateDrift_AboveUpperThresholdSustained_ReturnsDropChunk()
    {
        Assert.AreEqual(VoiceDriftAction.DropChunk, VoiceBufferPolicy.EvaluateDrift(200.0, 1.5));
    }

    [Test]
    public void EvaluateDrift_BelowLowerThresholdSustained_ReturnsInsertSilence()
    {
        Assert.AreEqual(VoiceDriftAction.InsertSilence, VoiceBufferPolicy.EvaluateDrift(50.0, 2.0));
    }

    [Test]
    public void EvaluateDrift_WithinBandSustained_ReturnsNone()
    {
        Assert.AreEqual(VoiceDriftAction.None, VoiceBufferPolicy.EvaluateDrift(120.0, 5.0));
    }
}
