using System;
using NUnit.Framework;
using NewCss.Voice.Core;

public class VoiceRingBufferTests
{
    [Test]
    public void Write_Read_FillAndDrainRoundTrip()
    {
        var ring = new VoiceRingBuffer(capacitySamples: 100, targetDelaySamples: 10, overrunCeilingSamples: 50, fadeSamples: 0);
        float[] source = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        ring.Write(source, 0, 10);

        float[] dest = new float[10];
        bool produced = ring.Read(dest, 0, 10);

        Assert.IsTrue(produced);
        CollectionAssert.AreEqual(source, dest);
    }

    // ─── Asimetrik kapı (histerezis) — 2026-08-08 "ses kesik kesik geliyor" bug'ının regresyonu ───
    // Kök neden: playout kapısı HER underrun'da tam hedefe yeniden kuruluyordu. Steam'in ses-aktivitesi
    // tespiti yüzünden konuşma duraklamalarında paket üretilmiyor, buffer doğal olarak kuruyor —
    // yani her kelime arası bir underrun. Sonuç: sonraki kelimenin ilk 120 ms'i yutuluyordu.

    [Test]
    public void Read_MidBurstUnderrun_ReopensAtResumeThresholdNotFullTarget()
    {
        // target 20, resume 5. Kapıyı aç, kurut, sonra SADECE 5 örnek yaz → çalmaya devam etmeli.
        var ring = new VoiceRingBuffer(capacitySamples: 100, targetDelaySamples: 20,
                                       overrunCeilingSamples: 60, fadeSamples: 0, resumeDelaySamples: 5);
        var dest = new float[30]; // en büyük Read(count)=20'den büyük olmalı

        ring.Write(new float[20], 0, 20);
        Assert.IsTrue(ring.Read(dest, 0, 10), "kapı tam hedefte açılmalıydı");
        Assert.IsFalse(ring.IsInResumeMode, "henüz underrun yok");

        // Kalan 10 örneği 20 isteyerek tüket → underrun
        Assert.IsFalse(ring.Read(dest, 0, 20), "underrun sessizlik döndürmeli");
        Assert.AreEqual(1, ring.UnderrunCount);
        Assert.IsTrue(ring.IsInResumeMode, "burst ortası underrun sonrası devam moduna geçmeliydi");

        // Tam hedefin (20) ALTINDA, devam eşiğinin (5) üstünde veri gelsin
        ring.Write(new float[6], 0, 6);
        Assert.IsTrue(ring.Read(dest, 0, 6),
            "devam eşiği kadar veri varken playout yeniden başlamalıydı — eski davranışta 20 beklenip ses yutuluyordu");
    }

    [Test]
    public void Read_AfterBurstEnded_RequiresFullTargetAgain()
    {
        var ring = new VoiceRingBuffer(capacitySamples: 100, targetDelaySamples: 20,
                                       overrunCeilingSamples: 60, fadeSamples: 0, resumeDelaySamples: 5);
        var dest = new float[30];

        ring.Write(new float[20], 0, 20);
        ring.Read(dest, 0, 20);                 // kapı açıldı ve tam kurudu
        Assert.IsFalse(ring.Read(dest, 0, 10)); // underrun → devam modu
        Assert.IsTrue(ring.IsInResumeMode);

        ring.NotifyBurstEnded();                // konuşmacı sustu

        // NotifyBurstEnded bayrağı bir sonraki Read'de tüketilir; devam eşiği kadar veri YETMEMELİ
        ring.Write(new float[6], 0, 6);
        Assert.IsFalse(ring.Read(dest, 0, 6),
            "yeni burst tam hedefi beklemeli — jitter koruması burst başında gerekli");
        Assert.IsFalse(ring.IsInResumeMode, "burst sonu devam modunu sıfırlamalıydı");

        ring.Write(new float[14], 0, 14);       // toplam 20 = tam hedef
        Assert.IsTrue(ring.Read(dest, 0, 20), "tam hedef dolunca açılmalı");
    }

    [Test]
    public void Constructor_ResumeDelayDefaultsToTarget_PreservesLegacySymmetricBehaviour()
    {
        // resumeDelaySamples verilmezse eski (simetrik) davranış korunur — mevcut testler bu yüzden
        // 4 argümanla kurulmaya devam ediyor ve değişmedi.
        var ring = new VoiceRingBuffer(capacitySamples: 100, targetDelaySamples: 20,
                                       overrunCeilingSamples: 60, fadeSamples: 0);
        var dest = new float[30];

        ring.Write(new float[20], 0, 20);
        ring.Read(dest, 0, 20);
        Assert.IsFalse(ring.Read(dest, 0, 10)); // underrun
        Assert.IsTrue(ring.IsInResumeMode);

        ring.Write(new float[6], 0, 6);
        Assert.IsFalse(ring.Read(dest, 0, 6), "resume eşiği = target olduğu için 6 örnek yetmemeli");
    }

    [Test]
    public void Write_Read_WrapAroundPreservesData()
    {
        // Kapasite 8, üç ardışık 4'lük yaz/oku turu toplamda fiziksel diziyi iki kez sarmalıyor —
        // dairesel indekslemenin (Wrap) her turda doğru veriyi verdiğini doğrular.
        var ring = new VoiceRingBuffer(capacitySamples: 8, targetDelaySamples: 0, overrunCeilingSamples: 8, fadeSamples: 0);
        float[] dest = new float[4];

        ring.Write(new float[] { 1, 2, 3, 4 }, 0, 4);
        ring.Read(dest, 0, 4);
        CollectionAssert.AreEqual(new float[] { 1, 2, 3, 4 }, dest);

        ring.Write(new float[] { 5, 6, 7, 8 }, 0, 4);
        ring.Read(dest, 0, 4);
        CollectionAssert.AreEqual(new float[] { 5, 6, 7, 8 }, dest);

        ring.Write(new float[] { 9, 10, 11, 12 }, 0, 4);
        ring.Read(dest, 0, 4);
        CollectionAssert.AreEqual(new float[] { 9, 10, 11, 12 }, dest);
    }

    [Test]
    public void Read_BeforeGateReachesTarget_ReturnsSilenceWithoutCountingAsUnderrun()
    {
        var ring = new VoiceRingBuffer(capacitySamples: 100, targetDelaySamples: 20, overrunCeilingSamples: 50, fadeSamples: 0);
        ring.Write(new float[] { 1, 2, 3, 4, 5 }, 0, 5); // hedefin (20) altında

        float[] dest = { 9, 9, 9, 9, 9 };
        bool produced = ring.Read(dest, 0, 5);

        Assert.IsFalse(produced);
        Assert.IsFalse(ring.IsGateOpen);
        Assert.AreEqual(0, ring.UnderrunCount); // kapı hiç açılmadı: bu bir underrun değil, normal prebuffer bekleyişi
        CollectionAssert.AreEqual(new float[] { 0, 0, 0, 0, 0 }, dest);
    }

    [Test]
    public void Read_Underrun_ReturnsSilenceAndRearmsGateForNextFill()
    {
        var ring = new VoiceRingBuffer(capacitySamples: 100, targetDelaySamples: 10, overrunCeilingSamples: 50, fadeSamples: 0);
        ring.Write(new float[10], 0, 10);

        float[] dest = new float[10];
        Assert.IsTrue(ring.Read(dest, 0, 10)); // kapı açıldı, buffer tam boşaldı

        // Buffer boşken 5 örnek istenirse underrun oluşmalı
        float[] underrunDest = { 1, 1, 1, 1, 1 };
        bool produced = ring.Read(underrunDest, 0, 5);

        Assert.IsFalse(produced);
        Assert.AreEqual(1, ring.UnderrunCount);
        Assert.IsFalse(ring.IsGateOpen); // kapı yeniden kuruldu
        CollectionAssert.AreEqual(new float[] { 0, 0, 0, 0, 0 }, underrunDest);

        // Kapı yeniden kurulduğu için tekrar hedef kadar dolmadan çalmaz
        ring.Write(new float[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, 0, 10);
        float[] refilled = new float[10];
        Assert.IsTrue(ring.Read(refilled, 0, 10));
        CollectionAssert.AreEqual(new float[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, refilled);
    }

    [Test]
    public void Write_Overrun_DropsOldestAndResetsAvailableToExactTargetDelay()
    {
        var ring = new VoiceRingBuffer(capacitySamples: 1000, targetDelaySamples: 100, overrunCeilingSamples: 300, fadeSamples: 0);

        ring.Write(new float[250], 0, 250); // used=250, henüz tavanın (300) altında
        Assert.AreEqual(0, ring.OverrunCount);

        ring.Write(new float[100], 0, 100); // used+count=350 > 300 tavanı -> overrun tetiklenir

        Assert.AreEqual(1, ring.OverrunCount);
        Assert.AreEqual(250, ring.DroppedSamples);
        // En yeniyi atmak gecikmeyi sonsuza büyütür (klasik hata) — bu yüzden doğru davranış
        // doluluğu TAM hedefe (100) resetlemektir, sıfıra veya tavana değil.
        Assert.AreEqual(100, ring.AvailableSamples);
    }

    [Test]
    public void Read_FadeIn_AppliesOverExactlyFadeSamplesCount()
    {
        var ring = new VoiceRingBuffer(capacitySamples: 100, targetDelaySamples: 5, overrunCeilingSamples: 50, fadeSamples: 4);
        ring.Write(new float[] { 1, 1, 1, 1, 1 }, 0, 5);

        float[] dest = new float[5];
        ring.Read(dest, 0, 5);

        // İlk 4 örnek (fadeSamples) kosinüs eğrisiyle 0->1 arası kısılmış olmalı, 5. örnek (fade
        // penceresinin dışında) tamamen ham (1.0) kalmalı — fade'in fazlaya taşmadığının kanıtı.
        for (int i = 0; i < 4; i++)
            Assert.Less(dest[i], 1.0f, $"index {i} fade uygulanmış olmalı");
        Assert.AreEqual(1.0f, dest[4], 1e-4f, "fade penceresi dışındaki örnek ham kalmalı");

        // Artan (monoton) bir fade-in eğrisi bekleniyor
        Assert.Less(dest[0], dest[1]);
        Assert.Less(dest[1], dest[2]);
        Assert.Less(dest[2], dest[3]);
    }

    [Test]
    public void Read_UnderrunFadeOut_AppliesOverExactlyAvailableSamplesWhenFewerThanFadeWindow()
    {
        var ring = new VoiceRingBuffer(capacitySamples: 100, targetDelaySamples: 3, overrunCeilingSamples: 50, fadeSamples: 2);
        ring.Write(new float[] { 1, 1, 1 }, 0, 3);

        float[] warmup = new float[3];
        ring.Read(warmup, 0, 3); // kapıyı aç ve buffer'ı tamamen boşalt (fade-in burada devreye girer, ilgisiz)

        // Şimdi sadece 1 örnek var (< fadeSamples=2, < istenen count=3) -> underrun, fade tek
        // örnek üzerinden kısıtlanmalı (Math.Min(fadeSamples, haveSamples)).
        ring.Write(new float[] { 1 }, 0, 1);

        float[] dest = { 9, 9, 9 };
        bool produced = ring.Read(dest, 0, 3);

        Assert.IsFalse(produced);
        Assert.AreEqual(1, ring.UnderrunCount);
        Assert.Greater(dest[0], 0f);
        Assert.Less(dest[0], 1f); // tek örnek fade ile kısılmış, sıfıra ASLA atlamıyor
        Assert.AreEqual(0f, dest[1]);
        Assert.AreEqual(0f, dest[2]);
    }
}
