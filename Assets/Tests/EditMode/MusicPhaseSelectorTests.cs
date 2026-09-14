using NUnit.Framework;
using NewCss.Audio;

/// <summary>
/// MusicPhaseSelector (bkz. Assets/NewCss/Audio/Core/MusicPhaseSelector.cs) — gün fazından
/// müzik ailesine saf karar mantığı. UnityEngine/DayCycleManager/CustomerManager bağımsız,
/// Unity başlatılmadan çalışır. Faz C'de test kapsamı bu turdan önce SIFIRDI (bkz.
/// plans/ses-tasarimi.md Faz C brief madde 4).
///
/// Öncelik sözleşmesi (MusicPhaseSelector.cs başı): Closing &gt; Tension &gt; Busy &gt; Main.
/// </summary>
public class MusicPhaseSelectorTests
{
    private static readonly MusicPhaseThresholds Thresholds = MusicPhaseThresholds.Default; // Tension=30s, Busy>=3

    [Test]
    public void SelectGameplayFamily_DayOver_ReturnsClosing_RegardlessOfOtherInputs()
    {
        // isDayOver true iken kuyruk çok kalabalık ve süre hâlâ bol olsa bile Closing kazanmalı
        // (en yüksek öncelik).
        var family = MusicPhaseSelector.SelectGameplayFamily(
            isDayOver: true, elapsedSeconds: 10f, dayDurationSeconds: 600f,
            customerQueueSize: 99, thresholds: Thresholds);

        Assert.AreEqual(MusicFamily.Closing, family);
    }

    [Test]
    public void SelectGameplayFamily_RemainingTimeAtTensionThreshold_ReturnsTension()
    {
        // remaining == TensionSeconds -> koşul "<=" olduğu için Tension'a girmeli (sınır dahil).
        var family = MusicPhaseSelector.SelectGameplayFamily(
            isDayOver: false, elapsedSeconds: 570f, dayDurationSeconds: 600f,
            customerQueueSize: 0, thresholds: Thresholds);

        Assert.AreEqual(MusicFamily.Tension, family);
    }

    [Test]
    public void SelectGameplayFamily_JustBeforeTensionThreshold_StaysMain()
    {
        var family = MusicPhaseSelector.SelectGameplayFamily(
            isDayOver: false, elapsedSeconds: 569f, dayDurationSeconds: 600f,
            customerQueueSize: 0, thresholds: Thresholds);

        Assert.AreEqual(MusicFamily.Main, family);
    }

    [Test]
    public void SelectGameplayFamily_TensionOverridesBusy_KasitliOncelik()
    {
        // Son saniyelerde kuyruk kalabalık olsa bile Tension önceliklidir (MusicPhaseSelector.cs
        // başındaki yorum: "KASITLI").
        var family = MusicPhaseSelector.SelectGameplayFamily(
            isDayOver: false, elapsedSeconds: 590f, dayDurationSeconds: 600f,
            customerQueueSize: 10, thresholds: Thresholds);

        Assert.AreEqual(MusicFamily.Tension, family);
    }

    [Test]
    public void SelectGameplayFamily_QueueAtBusyThreshold_ReturnsBusy()
    {
        var family = MusicPhaseSelector.SelectGameplayFamily(
            isDayOver: false, elapsedSeconds: 10f, dayDurationSeconds: 600f,
            customerQueueSize: 3, thresholds: Thresholds);

        Assert.AreEqual(MusicFamily.Busy, family);
    }

    [Test]
    public void SelectGameplayFamily_QueueBelowBusyThreshold_ReturnsMain()
    {
        var family = MusicPhaseSelector.SelectGameplayFamily(
            isDayOver: false, elapsedSeconds: 10f, dayDurationSeconds: 600f,
            customerQueueSize: 2, thresholds: Thresholds);

        Assert.AreEqual(MusicFamily.Main, family);
    }

    [Test]
    public void SelectGameplayFamily_ZeroDayDuration_TreatedAsDataNotReady_SkipsTension()
    {
        // dayDurationSeconds <= 0 -> gün henüz kurulmamış/geçersiz veri; "kalan süre 0" durumu
        // Tension olarak YORUMLANMAMALI (bkz. MusicPhaseSelector.cs satır 25-26 yorumu).
        var family = MusicPhaseSelector.SelectGameplayFamily(
            isDayOver: false, elapsedSeconds: 0f, dayDurationSeconds: 0f,
            customerQueueSize: 0, thresholds: Thresholds);

        Assert.AreEqual(MusicFamily.Main, family);
    }

    [Test]
    public void SelectGameplayFamily_NegativeDayDuration_TreatedAsDataNotReady_NoThrow()
    {
        var family = MusicPhaseSelector.SelectGameplayFamily(
            isDayOver: false, elapsedSeconds: 0f, dayDurationSeconds: -5f,
            customerQueueSize: 5, thresholds: Thresholds);

        Assert.AreEqual(MusicFamily.Busy, family, "süre verisi geçersizken de kuyruk kontrolü çalışmaya devam etmeli");
    }
}
