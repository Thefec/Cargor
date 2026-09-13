using NUnit.Framework;
using NewCss;

/// <summary>
/// NumberRoller (bkz. Assets/NewCss/UIScripts/Core/NumberRoller.cs) HUD sayaç motoru.
/// Süre/eğri sabitlerine mutlak assert atılmıyor — davranış doğrulanıyor (bitince hedefe
/// tam oturma, ara değerlerin tam sayı+monoton olması, SnapTo'nun anında olması,
/// animasyon ortasında yeni hedefin zıplamadan devam etmesi).
/// </summary>
public class NumberRollerTests
{
    // Gerçek animasyon süresi 0.6sn'yi geçmiyor (spec); bolca pay bırakılıyor.
    private const float GenerousDuration = 2f;
    private const float Step = 0.05f;

    private static void RunToCompletion(NumberRoller roller, float maxSeconds = GenerousDuration)
    {
        float elapsed = 0f;
        while (roller.IsAnimating && elapsed < maxSeconds)
        {
            roller.Tick(Step);
            elapsed += Step;
        }
    }

    [Test]
    public void SetTarget_EventuallySettlesExactlyOnTarget()
    {
        var roller = new NumberRoller(0f);
        roller.SetTarget(500f);

        RunToCompletion(roller);

        Assert.IsFalse(roller.IsAnimating, "süre dolduktan sonra animasyon bitmiş olmalı");
        Assert.AreEqual(500f, roller.Displayed, "float sürüklenmesi olmadan hedefe TAM oturmalı");
    }

    [Test]
    public void SetTarget_IntermediateValuesAreWholeNumbersAndMonotonic()
    {
        var roller = new NumberRoller(0f);
        roller.SetTarget(1000f);

        float previous = roller.Displayed;
        while (roller.IsAnimating)
        {
            roller.Tick(Step);
            float current = roller.Displayed;

            // Tam sayı: "1 2 3 4" görünsün, 3.72 değil.
            Assert.AreEqual(current, System.Math.Round(current), 0.0001f,
                "ara değer tam sayı olmalı");

            // Artan hedefe giderken geri gitmemeli (monoton).
            Assert.GreaterOrEqual(current, previous, "ara değerler geriye sıçramamalı");
            previous = current;
        }

        Assert.AreEqual(1000f, roller.Displayed);
    }

    [Test]
    public void SetTarget_DecreasingValue_IsMonotonicDownward()
    {
        var roller = new NumberRoller(1000f);
        roller.SetTarget(200f);

        float previous = roller.Displayed;
        while (roller.IsAnimating)
        {
            roller.Tick(Step);
            float current = roller.Displayed;
            Assert.LessOrEqual(current, previous, "azalan hedefte ara değerler yukarı sıçramamalı");
            previous = current;
        }

        Assert.AreEqual(200f, roller.Displayed);
    }

    [Test]
    public void SnapTo_IsInstantAndNotAnimating()
    {
        var roller = new NumberRoller(0f);
        roller.SetTarget(999f);
        roller.Tick(0.05f); // animasyonu ortasına al

        roller.SnapTo(42f);

        Assert.IsFalse(roller.IsAnimating, "SnapTo animasyonu tamamen iptal etmeli");
        Assert.AreEqual(42f, roller.Displayed);

        // Bir sonraki Tick hiçbir şeyi değiştirmemeli (animasyon yok).
        roller.Tick(0.05f);
        Assert.AreEqual(42f, roller.Displayed);
    }

    [Test]
    public void SetTarget_MidAnimation_RetargetsFromCurrentDisplayedValue_NoJump()
    {
        var roller = new NumberRoller(0f);
        roller.SetTarget(100f);

        // Animasyonu bir miktar ilerlet, hedefe varmadan yeni hedef ver.
        roller.Tick(0.1f);
        Assert.IsTrue(roller.IsAnimating, "test anlamlı olsun diye animasyon hâlâ sürüyor olmalı");
        float displayedBeforeRetarget = roller.Displayed;

        roller.SetTarget(50f);

        // Zıplama olmamalı: yeni animasyonun başlangıcı, retarget anındaki gösterilen
        // değerle aynı olmalı (ilk Tick'te ondan büyük bir sıçrama görülmemeli).
        roller.Tick(0.001f);
        Assert.LessOrEqual(System.Math.Abs(roller.Displayed - displayedBeforeRetarget), 5f,
            "yeniden hedefleme, o anki gösterilen değerden yumuşak devam etmeli, eski hedeften değil");

        RunToCompletion(roller);
        Assert.AreEqual(50f, roller.Displayed, "sonunda YENİ hedefe tam oturmalı");
    }

    [Test]
    public void SetTarget_SameValueAsCurrent_DoesNotAnimate()
    {
        var roller = new NumberRoller(10f);
        roller.SetTarget(10f);

        Assert.IsFalse(roller.IsAnimating, "zaten o değerdeyse animasyon başlamamalı");
        Assert.AreEqual(10f, roller.Displayed);
    }
}
