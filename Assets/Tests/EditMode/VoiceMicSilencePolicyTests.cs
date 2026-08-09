using NUnit.Framework;
using NewCss.Voice.Core;

/// <summary>
/// 2026-08-09 teşhisinin yan bulgusu: "1.5 sn + 0 bayt = mikrofon yok" tespiti KALICI bir kilit
/// kuruyordu. Steam'in gürültü kapısı (noiseGateLevel) sessizlikte zaten 0 bayt döndürdüğü için
/// sessizce PTT'ye basıp bırakan oyuncu oturum sonuna kadar kilitleniyordu — üstelik kulaklığını
/// açan / Steam ayarını düzelten oyuncu hiçbir şekilde geri dönemiyordu.
///
/// Bu testler baskının GEÇİCİ olduğunu ve uyarının spam'e dönüşmediğini doğrular.
/// Eşikler SABİTLERDEN türetiliyor — ayar değişince testler kırılmasın (bkz. VoiceBufferPolicyTests).
/// </summary>
public class VoiceMicSilencePolicyTests
{
    private static double Threshold => VoiceMicSilencePolicy.SilenceThresholdSeconds;
    private static double Cooldown => VoiceMicSilencePolicy.RetryCooldownSeconds;

    [Test]
    public void ThresholdRelationships_AreSane()
    {
        // Cooldown eşikten belirgin biçimde uzun olmalı: aksi hâlde "yeniden dene" penceresi
        // tespitin kendisinden kısa olur ve oyuncu uyarıyı görmeden döngüye girer.
        Assert.Greater(Cooldown, Threshold);
        Assert.Greater(Threshold, 0.0);
    }

    [Test]
    public void FreshPolicy_DoesNotIgnorePtt()
    {
        var p = new VoiceMicSilencePolicy();
        Assert.IsFalse(p.ShouldIgnorePtt(0.0));
        Assert.IsFalse(p.IsSuppressed);
        Assert.IsFalse(p.HasWarnedThisSession);
    }

    [Test]
    public void SilenceShorterThanThreshold_DoesNotSuppress()
    {
        var p = new VoiceMicSilencePolicy();
        Assert.IsFalse(p.NoteSilentSample(now: 10.0, burstElapsedSeconds: Threshold - 0.01));
        Assert.IsFalse(p.IsSuppressed);
        Assert.IsFalse(p.ShouldIgnorePtt(10.0));
    }

    [Test]
    public void SilenceAtThreshold_SuppressesAndAsksForWarningExactlyOnce()
    {
        var p = new VoiceMicSilencePolicy();

        // İlk tespit: çağırana "uyarıyı logla" der.
        Assert.IsTrue(p.NoteSilentSample(now: 10.0, burstElapsedSeconds: Threshold));
        Assert.IsTrue(p.IsSuppressed);
        Assert.IsTrue(p.HasWarnedThisSession);

        // Aynı burst içinde tekrar sorulursa ikinci kez log İSTEMEZ (Console spam'i olurdu).
        Assert.IsFalse(p.NoteSilentSample(now: 10.1, burstElapsedSeconds: Threshold + 0.1));
    }

    [Test]
    public void Suppressed_IgnoresPttUntilCooldownExpires()
    {
        var p = new VoiceMicSilencePolicy();
        p.NoteSilentSample(now: 10.0, burstElapsedSeconds: Threshold);

        Assert.IsTrue(p.ShouldIgnorePtt(10.0));
        Assert.IsTrue(p.ShouldIgnorePtt(10.0 + Cooldown - 0.01));

        // Cooldown dolunca baskı KENDİLİĞİNDEN kalkar — bu düzeltmenin bütün amacı bu.
        Assert.IsFalse(p.ShouldIgnorePtt(10.0 + Cooldown));
        Assert.IsFalse(p.IsSuppressed);
    }

    [Test]
    public void AfterCooldown_CanSuppressAgainButDoesNotWarnTwice()
    {
        var p = new VoiceMicSilencePolicy();
        p.NoteSilentSample(now: 10.0, burstElapsedSeconds: Threshold);
        p.ShouldIgnorePtt(10.0 + Cooldown); // baskı kalktı

        // Mikrofon hâlâ sessizse tekrar baskıya girer (boşuna mikrofon açıp durmayalım)...
        Assert.IsFalse(p.NoteSilentSample(now: 100.0, burstElapsedSeconds: Threshold),
            "İkinci tespitte tekrar log istenmemeli — uyarı oturum başına bir kez.");
        Assert.IsTrue(p.IsSuppressed);
        Assert.IsTrue(p.ShouldIgnorePtt(100.0));
    }

    [Test]
    public void DataReceived_ClearsSuppressionImmediately()
    {
        var p = new VoiceMicSilencePolicy();
        p.NoteSilentSample(now: 10.0, burstElapsedSeconds: Threshold);
        Assert.IsTrue(p.IsSuppressed);

        // Kullanıcı kulaklığı açtı / Steam gürültü kapısını düşürdü: cooldown'un bitmesini bekleme.
        p.NoteDataReceived();
        Assert.IsFalse(p.IsSuppressed);
        Assert.IsFalse(p.ShouldIgnorePtt(10.1));
    }

    [Test]
    public void Reset_ClearsSuppressionAndWarningFlag()
    {
        var p = new VoiceMicSilencePolicy();
        p.NoteSilentSample(now: 10.0, burstElapsedSeconds: Threshold);

        p.Reset();

        Assert.IsFalse(p.IsSuppressed);
        Assert.IsFalse(p.HasWarnedThisSession);
        // Reset sonrası ilk tespit yeniden log ister (dev aracı "temiz sayfa" demektir).
        Assert.IsTrue(p.NoteSilentSample(now: 20.0, burstElapsedSeconds: Threshold));
    }
}
