using NUnit.Framework;
using NewCss.Voice.Core;

public class VoiceSequenceTrackerTests
{
    [Test]
    public void Evaluate_Uint16Wraparound_AcceptsForwardWrap()
    {
        var tracker = new VoiceSequenceTracker();

        Assert.AreEqual(VoicePacketDecision.Accept, tracker.Evaluate(65533, VoicePacketFlags.BurstStart, 0.0));
        Assert.AreEqual(VoicePacketDecision.Accept, tracker.Evaluate(65534, VoicePacketFlags.Continuation, 0.01));
        Assert.AreEqual(VoicePacketDecision.Accept, tracker.Evaluate(65535, VoicePacketFlags.Continuation, 0.02));
        // 65535 -> 0 sarması: naif ">" burada kırılırdı (0 < 65535 görünür), işaretli fark doğru okumalı
        Assert.AreEqual(VoicePacketDecision.Accept, tracker.Evaluate(0, VoicePacketFlags.Continuation, 0.03));
    }

    [Test]
    public void Evaluate_Duplicate_IsIgnored()
    {
        var tracker = new VoiceSequenceTracker();
        tracker.Evaluate(10, VoicePacketFlags.BurstStart, 0.0);

        var decision = tracker.Evaluate(10, VoicePacketFlags.Continuation, 0.01);

        Assert.AreEqual(VoicePacketDecision.Duplicate, decision);
    }

    [Test]
    public void Evaluate_InWindowReorder_IsAcceptedButDoesNotRewindSequenceBase()
    {
        var tracker = new VoiceSequenceTracker(reorderWindow: 8);
        tracker.Evaluate(20, VoicePacketFlags.BurstStart, 0.0);
        tracker.Evaluate(25, VoicePacketFlags.Continuation, 0.01);

        var reordered = tracker.Evaluate(22, VoicePacketFlags.Continuation, 0.02);
        Assert.AreEqual(VoicePacketDecision.Reordered, reordered);

        // sıra bozukluğu ilerlemeyi durdurmamalı: 25'in bir sonrası (26) hâlâ normal Accept
        var next = tracker.Evaluate(26, VoicePacketFlags.Continuation, 0.03);
        Assert.AreEqual(VoicePacketDecision.Accept, next);
    }

    [Test]
    public void Evaluate_OutOfWindowReorder_IsDropped()
    {
        var tracker = new VoiceSequenceTracker(reorderWindow: 8);
        tracker.Evaluate(100, VoicePacketFlags.BurstStart, 0.0);

        var decision = tracker.Evaluate(90, VoicePacketFlags.Continuation, 0.01); // 10 geride, pencere 8

        Assert.AreEqual(VoicePacketDecision.Dropped, decision);
    }

    [Test]
    public void Evaluate_BurstEndThenNewBurstStart_ResetsSequenceBaseUnconditionally()
    {
        var tracker = new VoiceSequenceTracker();
        tracker.Evaluate(5, VoicePacketFlags.BurstStart, 0.0);
        tracker.Evaluate(6, VoicePacketFlags.BurstEnd, 0.01);

        // Yeni burst çok daha düşük bir sequence'la başlıyor (normalde Dropped sayılırdı) ama
        // BurstStart bayrağı koşulsuz reset anlamına gelir.
        var decision = tracker.Evaluate(2, VoicePacketFlags.BurstStart, 0.5);

        Assert.AreEqual(VoicePacketDecision.Accept, decision);
    }

    [Test]
    public void IsBurstStale_WithoutBurstEndFlag_TripsAfterTimeout()
    {
        var tracker = new VoiceSequenceTracker(burstTimeoutSeconds: 0.4);
        tracker.Evaluate(1, VoicePacketFlags.BurstStart, 0.0);

        Assert.IsFalse(tracker.IsBurstStale(0.3));
        Assert.IsTrue(tracker.IsBurstStale(0.5));
    }

    [Test]
    public void IsBurstStale_WhenBurstAlreadyEnded_AlwaysFalse()
    {
        var tracker = new VoiceSequenceTracker(burstTimeoutSeconds: 0.4);
        tracker.Evaluate(1, VoicePacketFlags.BurstStart | VoicePacketFlags.BurstEnd, 0.0);

        Assert.IsFalse(tracker.IsBurstStale(1000.0));
    }
}
