using System;
using NUnit.Framework;
using NewCss.Audio;

/// <summary>
/// AudioVolumeMath (bkz. Assets/NewCss/Audio/Core/AudioVolumeMath.cs) — dB dönüşümü ve
/// kategori -&gt; mixer grup/parametre adı çözümlemesi. UnityEngine bağımsız olduğu için
/// (NewCss.Audio.Core.asmdef, noEngineReferences=true) Unity'yi başlatmadan çalışır.
///
/// Grup/parametre adları burada BİLEREK literal string ile doğrulanıyor: bunlar tunable bir
/// denge değeri değil, Assets/Editor/AudioMixerSetup.cs'nin CargorMixer.mixer içinde kurduğu
/// grup adları ve exposed parametrelerle birebir eşleşmesi gereken bir SÖZLEŞME. Uyuşmazlık
/// sessizce sesin üçüncü kez ölmesine yol açar (bkz. GDD.md §27 CAUTION).
/// </summary>
public class AudioVolumeMathTests
{
    [Test]
    public void LinearToDecibel_FullVolume_IsZeroDb()
    {
        Assert.AreEqual(0f, AudioVolumeMath.LinearToDecibel(1f), 0.0001f);
    }

    [Test]
    public void LinearToDecibel_Zero_ClipsToMinDecibel_NoInfinity()
    {
        float db = AudioVolumeMath.LinearToDecibel(0f);

        Assert.AreEqual(AudioVolumeMath.MinDecibel, db, 0.0001f, "Log10(0) = -Infinity tuzağına düşülmemeli");
        Assert.IsFalse(float.IsInfinity(db));
        Assert.IsFalse(float.IsNaN(db));
    }

    [Test]
    public void LinearToDecibel_NegativeInput_ClipsToMinDecibel_NoNaN()
    {
        float db = AudioVolumeMath.LinearToDecibel(-0.5f);

        Assert.AreEqual(AudioVolumeMath.MinDecibel, db, 0.0001f);
        Assert.IsFalse(float.IsNaN(db));
    }

    [Test]
    public void LinearToDecibel_AboveOne_ClipsToMaxDecibel()
    {
        float db = AudioVolumeMath.LinearToDecibel(2f);

        Assert.AreEqual(AudioVolumeMath.MaxDecibel, db, 0.0001f);
    }

    [Test]
    public void LinearToDecibel_IsMonotonicallyIncreasing()
    {
        float previous = AudioVolumeMath.MinDecibel;

        for (float linear = 0.05f; linear <= 1f; linear += 0.05f)
        {
            float db = AudioVolumeMath.LinearToDecibel(linear);
            Assert.GreaterOrEqual(db, previous, $"linear={linear} için dB azalmamalı");
            previous = db;
        }
    }

    [Test]
    public void LinearToDecibel_NeverExceedsDeclaredRange()
    {
        float[] samples = { -10f, -1f, 0f, 0.0001f, 0.5f, 0.999f, 1f, 1.5f, 100f };

        foreach (float linear in samples)
        {
            float db = AudioVolumeMath.LinearToDecibel(linear);
            Assert.GreaterOrEqual(db, AudioVolumeMath.MinDecibel);
            Assert.LessOrEqual(db, AudioVolumeMath.MaxDecibel);
        }
    }

    [Test]
    public void GroupNameFor_MatchesMixerSetupContract()
    {
        Assert.AreEqual("Music", AudioVolumeMath.GroupNameFor(AudioCategory.Music));
        Assert.AreEqual("SFX", AudioVolumeMath.GroupNameFor(AudioCategory.SFX));
    }

    [Test]
    public void ExposedParamFor_MatchesMixerSetupContract()
    {
        Assert.AreEqual("MusicVol", AudioVolumeMath.ExposedParamFor(AudioCategory.Music));
        Assert.AreEqual("SfxVol", AudioVolumeMath.ExposedParamFor(AudioCategory.SFX));
    }

    [Test]
    public void GroupNameFor_UnknownCategory_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AudioVolumeMath.GroupNameFor((AudioCategory)999));
    }
}
