using NUnit.Framework;
using NewCss.Voice.Core;

public class VoiceHudRowTimerTests
{
    private const double Hold = 0.3;
    private const double Fade = 0.2;

    [Test]
    public void NeverActive_AlphaIsZero_AndNotFullyFaded()
    {
        var timer = new VoiceHudRowTimer(Hold, Fade);

        Assert.AreEqual(0f, timer.ComputeAlpha(0.0));
        // Hiç aktif olmamış bir satır "solmuş" sayılmaz — havuza asla atanmamış demektir, HUD
        // controller bunun üzerinden bir satırı erken serbest bırakmamalı.
        Assert.IsFalse(timer.IsFullyFaded(0.0));
    }

    [Test]
    public void JustMarkedActive_AlphaIsFull()
    {
        var timer = new VoiceHudRowTimer(Hold, Fade);
        timer.MarkActive(10.0);

        Assert.AreEqual(1f, timer.ComputeAlpha(10.0));
    }

    [Test]
    public void WithinHoldWindow_AlphaStaysFull_InclusiveBoundary()
    {
        var timer = new VoiceHudRowTimer(Hold, Fade);
        timer.MarkActive(0.0);

        Assert.AreEqual(1f, timer.ComputeAlpha(Hold - 0.01));
        Assert.AreEqual(1f, timer.ComputeAlpha(Hold)); // sınır dahil (<=)
    }

    [Test]
    public void MidFade_AlphaIsBetweenZeroAndOne()
    {
        var timer = new VoiceHudRowTimer(Hold, Fade);
        timer.MarkActive(0.0);

        float alpha = timer.ComputeAlpha(Hold + Fade / 2.0);

        Assert.AreEqual(0.5f, alpha, 0.001f);
    }

    [Test]
    public void FadeComplete_AlphaIsZero_AndFullyFaded()
    {
        var timer = new VoiceHudRowTimer(Hold, Fade);
        timer.MarkActive(0.0);

        double doneAt = Hold + Fade;

        Assert.AreEqual(0f, timer.ComputeAlpha(doneAt));
        Assert.IsTrue(timer.IsFullyFaded(doneAt));
        Assert.IsTrue(timer.IsFullyFaded(doneAt + 100.0)); // fade bittikten uzun süre sonra da solmuş kalır
    }

    [Test]
    public void RepeatedMarkActive_DuringBurst_ResetsHoldWindowEachTime()
    {
        // 30Hz'de gelen ardışık paketler her frame MarkActive çağırır — bu, satırın burst
        // SÜRERKEN hiç sönmemesini garantiler (tıkırdayan akış listeyi yanıp söndürmemeli).
        var timer = new VoiceHudRowTimer(Hold, Fade);

        timer.MarkActive(0.0);
        timer.MarkActive(0.2); // hold (0.3) dolmadan tekrar aktif
        timer.MarkActive(0.4); // yine dolmadan tekrar aktif

        // Son MarkActive'den (0.4) hold süresi kadar sonrası hâlâ tam görünür olmalı.
        Assert.AreEqual(1f, timer.ComputeAlpha(0.4 + Hold));
    }

    [Test]
    public void ZeroFadeSeconds_CutsOffImmediatelyAfterHold_NoDivideByZero()
    {
        var timer = new VoiceHudRowTimer(Hold, 0.0);
        timer.MarkActive(0.0);

        Assert.AreEqual(1f, timer.ComputeAlpha(Hold));
        Assert.AreEqual(0f, timer.ComputeAlpha(Hold + 0.001));
    }

    [Test]
    public void Reset_ReturnsTimerToNeverActiveState()
    {
        var timer = new VoiceHudRowTimer(Hold, Fade);
        timer.MarkActive(0.0);
        Assert.AreEqual(1f, timer.ComputeAlpha(0.0));

        timer.Reset();

        Assert.AreEqual(0f, timer.ComputeAlpha(0.0));
        Assert.IsFalse(timer.IsFullyFaded(0.0));
    }

    [Test]
    public void NegativeHoldOrFade_ThrowsArgumentOutOfRange()
    {
        Assert.Throws<System.ArgumentOutOfRangeException>(() => new VoiceHudRowTimer(-1.0, Fade));
        Assert.Throws<System.ArgumentOutOfRangeException>(() => new VoiceHudRowTimer(Hold, -1.0));
    }
}
