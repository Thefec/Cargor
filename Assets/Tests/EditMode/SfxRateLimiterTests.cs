using NUnit.Framework;
using NewCss.Audio;

/// <summary>
/// SfxRateLimiter (bkz. Assets/NewCss/Audio/Core/SfxRateLimiter.cs) — SfxBus'ın "çok sık çalma"
/// korumasının çekirdek mantığı. UnityEngine bağımsız (NewCss.Audio.Core.asmdef,
/// noEngineReferences=true), Unity başlatmadan çalışır — AudioVolumeMathTests ile aynı desen.
/// </summary>
public class SfxRateLimiterTests
{
    [Test]
    public void ShouldPlay_ZeroInterval_AlwaysTrue()
    {
        Assert.IsTrue(SfxRateLimiter.ShouldPlay(lastPlayedTime: 100f, now: 100.001f, minInterval: 0f));
    }

    [Test]
    public void ShouldPlay_NegativeInterval_AlwaysTrue()
    {
        Assert.IsTrue(SfxRateLimiter.ShouldPlay(lastPlayedTime: 100f, now: 100f, minInterval: -1f));
    }

    [Test]
    public void ShouldPlay_ElapsedLessThanInterval_False()
    {
        Assert.IsFalse(SfxRateLimiter.ShouldPlay(lastPlayedTime: 10f, now: 10.02f, minInterval: 0.05f));
    }

    [Test]
    public void ShouldPlay_ElapsedExactlyInterval_True()
    {
        Assert.IsTrue(SfxRateLimiter.ShouldPlay(lastPlayedTime: 10f, now: 10.05f, minInterval: 0.05f));
    }

    [Test]
    public void ShouldPlay_ElapsedMoreThanInterval_True()
    {
        Assert.IsTrue(SfxRateLimiter.ShouldPlay(lastPlayedTime: 10f, now: 11f, minInterval: 0.05f));
    }

    [Test]
    public void ShouldPlay_NeverPlayedBefore_IsCallerResponsibility()
    {
        // NOT: SfxBus "hiç çalınmamış" durumunu Dictionary'de kayıt YOK olarak tutar ve bu
        // durumda ShouldPlay'i hiç ÇAĞIRMAZ (bkz. SfxBus.TryResolveAndGate — TryGetValue false
        // dönerse doğrudan true kabul eder). ShouldPlay'in KENDİSİ lastPlayedTime=0/now=0 ile
        // çağrılırsa (elapsed=0) minInterval'in altında kalıp False döner — bu metodun günlük
        // (naive) davranışı, "ilk çağrı" ayrıcalığı SfxBus'ta, burada değil.
        Assert.IsFalse(SfxRateLimiter.ShouldPlay(lastPlayedTime: 0f, now: 0f, minInterval: 0.05f));
    }
}
