using NUnit.Framework;
using NewCss.Audio;

/// <summary>
/// MusicCrossfadeMath (bkz. Assets/NewCss/Audio/Core/MusicCrossfadeMath.cs) — eşit-güç
/// (equal-power) çapraz geçiş matematiği. UnityEngine bağımsız, Unity başlatılmadan çalışır.
/// Faz C'de test kapsamı bu turdan önce SIFIRDI (bkz. plans/ses-tasarimi.md Faz C brief madde 4).
/// </summary>
public class MusicCrossfadeMathTests
{
    private const float Tolerance = 0.0001f;

    [Test]
    public void GetVolumes_AtStart_OutgoingFull_IncomingZero()
    {
        MusicCrossfadeMath.GetVolumes(0f, out float outVol, out float inVol);

        Assert.AreEqual(1f, outVol, Tolerance);
        Assert.AreEqual(0f, inVol, Tolerance);
    }

    [Test]
    public void GetVolumes_AtEnd_OutgoingZero_IncomingFull()
    {
        MusicCrossfadeMath.GetVolumes(1f, out float outVol, out float inVol);

        Assert.AreEqual(0f, outVol, Tolerance);
        Assert.AreEqual(1f, inVol, Tolerance);
    }

    [Test]
    public void GetVolumes_AtMidpoint_IsEqualPower_NotLinearMidpointDip()
    {
        // Eşit-güç crossfade: out^2 + in^2 == 1 her t için (lineer fade'de bu ortada 0.5 olur,
        // burada ~0.707/0.707 olmalı — "orta nokta çukuru" olmadığının kanıtı).
        MusicCrossfadeMath.GetVolumes(0.5f, out float outVol, out float inVol);

        Assert.AreEqual(0.70710678f, outVol, 0.001f);
        Assert.AreEqual(0.70710678f, inVol, 0.001f);
    }

    [Test]
    public void GetVolumes_SumOfSquares_IsAlwaysOne_AcrossRange()
    {
        for (float t = 0f; t <= 1f; t += 0.1f)
        {
            MusicCrossfadeMath.GetVolumes(t, out float outVol, out float inVol);
            float sumOfSquares = outVol * outVol + inVol * inVol;
            Assert.AreEqual(1f, sumOfSquares, 0.001f, $"t={t} için toplam güç 1 olmalı");
        }
    }

    [Test]
    public void GetVolumes_BelowZero_ClampsToStart()
    {
        MusicCrossfadeMath.GetVolumes(-0.5f, out float outVol, out float inVol);

        Assert.AreEqual(1f, outVol, Tolerance);
        Assert.AreEqual(0f, inVol, Tolerance);
    }

    [Test]
    public void GetVolumes_AboveOne_ClampsToEnd()
    {
        MusicCrossfadeMath.GetVolumes(1.5f, out float outVol, out float inVol);

        Assert.AreEqual(0f, outVol, Tolerance);
        Assert.AreEqual(1f, inVol, Tolerance);
    }

    [Test]
    public void CrossfadeStartTime_NormalTrack_ReturnsLengthMinusCrossfade()
    {
        float start = MusicCrossfadeMath.CrossfadeStartTime(trackLength: 180f, crossfadeDuration: 2.5f);

        Assert.AreEqual(177.5f, start, Tolerance);
    }

    [Test]
    public void CrossfadeStartTime_TrackShorterThanCrossfade_ClampsToZero()
    {
        // Parça crossfadeDuration'dan kısaysa negatife düşmemeli — geçiş parça başlar başlamaz
        // tetiklenmeli (bkz. sınıf başı yorum).
        float start = MusicCrossfadeMath.CrossfadeStartTime(trackLength: 1f, crossfadeDuration: 2.5f);

        Assert.AreEqual(0f, start, Tolerance);
    }

    [Test]
    public void CrossfadeStartTime_ExactlyEqualToCrossfadeDuration_ReturnsZero()
    {
        float start = MusicCrossfadeMath.CrossfadeStartTime(trackLength: 2.5f, crossfadeDuration: 2.5f);

        Assert.AreEqual(0f, start, Tolerance);
    }
}
