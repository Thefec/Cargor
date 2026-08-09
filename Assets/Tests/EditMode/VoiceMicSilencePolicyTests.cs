using NUnit.Framework;
using NewCss.Voice.Core;

/// <summary>
/// 2026-08-09'un İKİ ayrı bulgusu bu sınıfta birleşiyor:
///
/// (1) Teşhis turu: "1.5 sn + 0 bayt = mikrofon yok" tespiti KALICI bir kilit kuruyordu. Steam'in
///     gürültü kapısı sessizlikte zaten 0 bayt döndürdüğü için sessizce PTT'ye basıp bırakan oyuncu
///     oturum sonuna kadar kilitleniyordu → baskı GEÇİCİ yapıldı (cooldown).
/// (2) Çift-makine testi: oyuncu 1 dakika SORUNSUZ konuştuktan sonra, konuşmanın ortasında
///     "Ses algılanmıyor" çıktı. Sebep: kural yalnızca içinde bulunduğu burst'e bakıyordu, yeni bir
///     burst'ün ilk 1.5 sn'si sessiz geçince (düşünme molası / kapının ilk heceyi yutması) tetikleniyordu.
///     → İki yeni koruma: Steam bir kez bayt verdiyse tespit tamamen kapanır, ve tek sessiz burst
///     yetmez, ARDIŞIK iki burst gerekir.
///
/// Eşikler SABİTLERDEN türetiliyor — ayar değişince testler kırılmasın (bkz. VoiceBufferPolicyTests).
/// </summary>
public class VoiceMicSilencePolicyTests
{
    private static double Threshold => VoiceMicSilencePolicy.SilenceThresholdSeconds;
    private static double Cooldown => VoiceMicSilencePolicy.RetryCooldownSeconds;
    private static int BurstsNeeded => VoiceMicSilencePolicy.SilentBurstsBeforeSuppress;

    /// <summary>Bastan sona sessiz bir burst yasatir. Donus: uyari istendi mi.</summary>
    private static bool SilentBurst(VoiceMicSilencePolicy p, double now)
    {
        p.NoteBurstStarted();
        p.NoteSilentSample(now, Threshold * 0.5);            // esik dolmadan: hicbir sey olmamali
        return p.NoteSilentSample(now, Threshold);           // esik doldu
    }

    [Test]
    public void ThresholdRelationships_AreSane()
    {
        Assert.Greater(Cooldown, Threshold, "Cooldown esikten uzun olmali.");
        Assert.Greater(Threshold, 0.0);
        Assert.GreaterOrEqual(BurstsNeeded, 2, "Tek burst yetmemeli — yanlis pozitifin kaynagi buydu.");
    }

    [Test]
    public void FreshPolicy_DoesNotIgnorePtt()
    {
        var p = new VoiceMicSilencePolicy();
        Assert.IsFalse(p.ShouldIgnorePtt(0.0));
        Assert.IsFalse(p.IsSuppressed);
        Assert.IsFalse(p.HasWarnedThisSession);
        Assert.IsFalse(p.EverReceivedDataThisSession);
    }

    [Test]
    public void SingleSilentBurst_DoesNotSuppress()
    {
        var p = new VoiceMicSilencePolicy();
        Assert.IsFalse(SilentBurst(p, 10.0), "Tek sessiz burst baskiya yetmemeli.");
        Assert.IsFalse(p.IsSuppressed);
        Assert.IsFalse(p.ShouldIgnorePtt(10.0));
    }

    [Test]
    public void ConsecutiveSilentBursts_SuppressAndWarnExactlyOnce()
    {
        var p = new VoiceMicSilencePolicy();

        for (int i = 0; i < BurstsNeeded - 1; i++)
            Assert.IsFalse(SilentBurst(p, 10.0 + i), $"{i + 1}. burst'te henuz baski olmamali.");

        Assert.IsTrue(SilentBurst(p, 20.0), "Esik sayida sessiz burst'te uyari istenmeli.");
        Assert.IsTrue(p.IsSuppressed);
        Assert.IsTrue(p.HasWarnedThisSession);

        // Ayni burst icinde tekrar sorulursa ikinci kez log ISTEMEZ (Console spam'i olurdu).
        Assert.IsFalse(p.NoteSilentSample(20.1, Threshold + 0.1));
    }

    /// <summary>Cift-makine testinde yasanan birebir senaryo.</summary>
    [Test]
    public void AfterAnyDataInSession_NeverSuppressesAgain()
    {
        var p = new VoiceMicSilencePolicy();

        p.NoteBurstStarted();
        p.NoteDataReceived(); // oyuncu bir dakika sorunsuz konustu
        Assert.IsTrue(p.EverReceivedDataThisSession);

        // Sonraki burst'lerin hepsi bastan sona sessiz olsa BILE artik "mikrofon yok" denemez:
        // mikrofonun varligi kanitlandi, bu yalnizca dusunme molasi / gurultu kapisi.
        for (int i = 0; i < BurstsNeeded + 3; i++)
            Assert.IsFalse(SilentBurst(p, 100.0 + i));

        Assert.IsFalse(p.IsSuppressed);
        Assert.IsFalse(p.ShouldIgnorePtt(200.0));
        Assert.IsFalse(p.HasWarnedThisSession, "Hic uyari verilmemis olmali.");
    }

    [Test]
    public void NonConsecutiveSilentBursts_DoNotAccumulate()
    {
        var p = new VoiceMicSilencePolicy();

        SilentBurst(p, 10.0);   // 1. sessiz burst
        p.NoteDataReceived();   // arada ses geldi -> sayac sifirlanmali

        // Bu noktadan sonra zaten EverReceivedData devrede; sayacin sifirlandigini da dogrula.
        Assert.IsFalse(SilentBurst(p, 30.0));
        Assert.IsFalse(p.IsSuppressed);
    }

    [Test]
    public void Suppressed_IgnoresPttUntilCooldownExpires()
    {
        var p = new VoiceMicSilencePolicy();
        for (int i = 0; i < BurstsNeeded; i++) SilentBurst(p, 10.0);
        Assert.IsTrue(p.IsSuppressed);

        Assert.IsTrue(p.ShouldIgnorePtt(10.0));
        Assert.IsTrue(p.ShouldIgnorePtt(10.0 + Cooldown - 0.01));

        // Cooldown dolunca baski KENDILIGINDEN kalkar.
        Assert.IsFalse(p.ShouldIgnorePtt(10.0 + Cooldown));
        Assert.IsFalse(p.IsSuppressed);
    }

    [Test]
    public void DataReceived_ClearsSuppressionImmediately()
    {
        var p = new VoiceMicSilencePolicy();
        for (int i = 0; i < BurstsNeeded; i++) SilentBurst(p, 10.0);
        Assert.IsTrue(p.IsSuppressed);

        // Kullanici kulakligi acti / Steam gurultu kapisini dusurdu: cooldown'u bekleme.
        p.NoteDataReceived();
        Assert.IsFalse(p.IsSuppressed);
        Assert.IsFalse(p.ShouldIgnorePtt(10.1));
    }

    [Test]
    public void Reset_ClearsEverythingIncludingDataProof()
    {
        var p = new VoiceMicSilencePolicy();
        p.NoteDataReceived();
        for (int i = 0; i < BurstsNeeded; i++) SilentBurst(p, 10.0);

        p.Reset();

        Assert.IsFalse(p.IsSuppressed);
        Assert.IsFalse(p.HasWarnedThisSession);
        Assert.IsFalse(p.EverReceivedDataThisSession);

        // Reset sonrasi tespit yeniden calisir (dev araci "temiz sayfa" demektir).
        for (int i = 0; i < BurstsNeeded - 1; i++) Assert.IsFalse(SilentBurst(p, 20.0 + i));
        Assert.IsTrue(SilentBurst(p, 40.0));
    }
}
