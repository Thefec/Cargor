using NUnit.Framework;
using NewCss.Rooms.Core;

/// <summary>
/// NewCss.Rooms.Core (motor referanssız) için headless EditMode testleri.
/// Plan §10'daki 6 maddenin hepsini kapsar — özellikle flicker regresyon testi
/// (sınırı ±1 cm ile 10 kez geçen yol → tam 1 geçiş).
/// </summary>
public class RoomResolverTests
{
    [Test]
    public void RoomBox_Contains_IsInclusiveAtBoundary()
    {
        var box = new RoomBox(0f, 0f, 0f, 10f, 10f, 10f);

        // Sınırlar dahil ("&gt;=" / "&lt;=") — plan §S0 madde 2.
        Assert.IsTrue(box.Contains(0f, 5f, 5f), "min sınırı dahil olmalı");
        Assert.IsTrue(box.Contains(10f, 5f, 5f), "max sınırı dahil olmalı");
        Assert.IsTrue(box.Contains(5f, 5f, 5f), "iç nokta içeride olmalı");

        Assert.IsFalse(box.Contains(10.01f, 5f, 5f), "max'ın az dışı hariç olmalı");
        Assert.IsFalse(box.Contains(-0.01f, 5f, 5f), "min'in az dışı hariç olmalı");
    }

    [Test]
    public void Resolve_OverlappingExpandedBoxes_PrefersCurrent()
    {
        // A ve B tam x=10'da komşu; margin genişletmesiyle sorguda çakışırlar.
        var roomA = new RoomEntry(1, new RoomBox(0f, 0f, 0f, 10f, 10f, 10f));
        var roomB = new RoomEntry(2, new RoomBox(10f, 0f, 0f, 20f, 10f, 10f));
        var entries = new[] { roomA, roomB };

        // Ortak sınır noktası her iki genişletilmiş kutuda da — kararsız bölge.
        Assert.AreEqual(1, RoomResolver.Resolve(1, 10f, 5f, 5f, entries), "current=A iken A korunmalı");
        Assert.AreEqual(2, RoomResolver.Resolve(2, 10f, 5f, 5f, entries), "current=B iken B korunmalı");
    }

    [Test]
    public void Resolve_NoBoxMatches_KeepsCurrent_NeverReturnsSentinel()
    {
        var entries = new[] { new RoomEntry(1, new RoomBox(0f, 0f, 0f, 10f, 10f, 10f)) };

        // Sorgu noktası hiçbir genişletilmiş kutuda değil (çok uzakta).
        int result = RoomResolver.Resolve(7, 1000f, 1000f, 1000f, entries);

        Assert.AreEqual(7, result, "H8: hiçbir kutuda değilken current aynen dönmeli");
        Assert.AreNotEqual(RoomResolver.NoRoomSentinel, result, "H8: -1 DÖNMEMELİ");
    }

    [Test]
    public void Resolve_ZeroRooms_ReturnsSentinel_NoException()
    {
        Assert.AreEqual(RoomResolver.NoRoomSentinel, RoomResolver.Resolve(3, 0f, 0f, 0f, new RoomEntry[0]),
            "0 kutu (örn. Tutorial) sentinel dönmeli");

        Assert.DoesNotThrow(() => RoomResolver.Resolve(3, 0f, 0f, 0f, null),
            "null girdi de exception atmamalı (Tutorial güvenliği)");
    }

    [Test]
    public void Resolve_JitterAcrossBoundary_ResolvesExactlyOneTransition()
    {
        // A: x[-10..0], B: x[0..10] — ortak sınır x=0.
        var roomA = new RoomEntry(1, new RoomBox(-10f, 0f, 0f, 0f, 10f, 10f));
        var roomB = new RoomEntry(2, new RoomBox(0f, 0f, 0f, 10f, 10f, 10f));
        var entries = new[] { roomA, roomB };

        // A'nın derininden başla, sınırı ±1 cm ile 10 kez geç (kapı ağzı titremesi),
        // sonra B'nin derinine ilerle. Naif (histerezissiz) bir çözücü burada onlarca
        // kez görünürlük durumunu değiştirirdi — bu test tam olarak onu engeller.
        float[] path =
        {
            -5f,
            -0.01f, 0.01f, -0.01f, 0.01f, -0.01f,
            0.01f, -0.01f, 0.01f, -0.01f, 0.01f,
            5f,
        };

        int current = 1; // A'da başlıyoruz
        int transitions = 0;
        foreach (float x in path)
        {
            int next = RoomResolver.Resolve(current, x, 5f, 5f, entries);
            if (next != current)
            {
                transitions++;
            }
            current = next;
        }

        Assert.AreEqual(1, transitions, "titreşim boyunca tam 1 gerçek geçiş olmalı (flicker regresyonu)");
        Assert.AreEqual(2, current, "yol sonunda B'de bitmeli");
    }

    [Test]
    public void RoomFade_Step_ReachesExactZero_FromNearOne()
    {
        const float dt = 1f / 60f;
        const float speed = 3f;
        const int maxSteps = 1000;

        float cur = 0.9f;
        int steps = 0;
        while (cur != 0f && steps < maxSteps)
        {
            cur = RoomFade.Step(cur, false, dt, speed);
            steps++;
        }

        // H7: Lerp semantiğinde bu asla tam 0f olmaz (asimptotik). MoveTowards ile
        // sonlu adımda tam kilitlenmeli.
        Assert.AreEqual(0f, cur, "fade sonlu adımda tam 0f'a kilitlenmeli, asimptotik kalmamalı");
        Assert.Less(steps, maxSteps, "hedefe tavan sayıya çarpmadan, sonlu adımda ulaşmalı");
    }

    // ── Oda geçişinde iki-kutulu çapraz-geçiş (RoomBlend) — kullanıcı şikâyeti: harita
    // görünümü basılıyken oda değişince griden renge ANİ sıçrama yerine yumuşak aydınlanma. ──

    [Test]
    public void RoomBlend_Advance_ReachesExactOne_FromZero()
    {
        const float dt = 1f / 60f;
        const float duration = 1.2f; // RoomViewController.roomBlendDuration varsayılanıyla aynı büyüklük mertebesi
        const int maxSteps = 1000;

        float progress = 0f;
        int steps = 0;
        while (progress != 1f && steps < maxSteps)
        {
            progress = RoomBlend.Advance(progress, dt, duration);
            steps++;
        }

        // H7 ilkesiyle aynı: lineer ilerleme hedefi asla geçmez, sonlu adımda TAM 1f'e kilitlenir.
        Assert.AreEqual(1f, progress, "blend sonlu adımda tam 1f'e kilitlenmeli, asimptotik kalmamalı");
        Assert.Less(steps, maxSteps, "hedefe tavan sayıya çarpmadan, sonlu adımda ulaşmalı");
    }

    [Test]
    public void RoomBlend_Smoothstep01_IsExactAtBoundaries()
    {
        // Sözleşme: t=0/t=1'de TAM 0f/1f — shader'a giden değer iki-kutulu karışımda
        // asimptotik takılı kalırsa (H7'nin bu turdaki karşılığı) geçiş hiç bitmezmiş gibi görünür.
        Assert.AreEqual(0f, RoomBlend.Smoothstep01(0f), "t=0'da tam 0 dönmeli");
        Assert.AreEqual(1f, RoomBlend.Smoothstep01(1f), "t=1'de tam 1 dönmeli");

        // Domain dışı girdiler (savunma amaçlı) clamp'lenmeli, exception atmamalı.
        Assert.AreEqual(0f, RoomBlend.Smoothstep01(-0.5f), "negatif girdi 0'a clamp'lenmeli");
        Assert.AreEqual(1f, RoomBlend.Smoothstep01(1.5f), "1'i aşan girdi 1'e clamp'lenmeli");
    }

    [Test]
    public void RoomBlend_Smoothstep01_IsMonotonicNonDecreasing()
    {
        float previous = RoomBlend.Smoothstep01(0f);
        for (float t = 0.1f; t <= 1.0001f; t += 0.1f)
        {
            float current = RoomBlend.Smoothstep01(t);
            Assert.GreaterOrEqual(current, previous, "smoothstep artan girdide azalmamalı");
            previous = current;
        }
    }
}
