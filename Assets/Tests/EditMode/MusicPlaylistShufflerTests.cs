using System;
using System.Collections.Generic;
using NUnit.Framework;
using NewCss.Audio;

/// <summary>
/// MusicPlaylistShuffler (bkz. Assets/NewCss/Audio/Core/MusicPlaylistShuffler.cs) — Faz C
/// müzik sisteminin karıştırma çekirdeği. UnityEngine bağımsız (NewCss.Audio.Core.asmdef,
/// noEngineReferences=true), Unity başlatılmadan çalışır. Faz C'de test kapsamı bu turdan
/// önce SIFIRDI (bkz. plans/ses-tasarimi.md Faz C brief madde 4).
/// </summary>
public class MusicPlaylistShufflerTests
{
    [Test]
    public void Shuffle_NullRng_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => MusicPlaylistShuffler.Shuffle(4, null));
    }

    [Test]
    public void Shuffle_ZeroTrackCount_ReturnsEmptyList()
    {
        var result = MusicPlaylistShuffler.Shuffle(0, new Random(1));

        Assert.IsNotNull(result);
        Assert.AreEqual(0, result.Count);
    }

    [Test]
    public void Shuffle_NegativeTrackCount_ReturnsEmptyList_NoThrow()
    {
        var result = MusicPlaylistShuffler.Shuffle(-3, new Random(1));

        Assert.AreEqual(0, result.Count);
    }

    [Test]
    public void Shuffle_ReturnsPermutation_AllIndicesPresentExactlyOnce()
    {
        var result = MusicPlaylistShuffler.Shuffle(6, new Random(42));

        Assert.AreEqual(6, result.Count);
        var seen = new HashSet<int>(result);
        Assert.AreEqual(6, seen.Count, "her indeks tam bir kez görünmeli (permütasyon)");
        for (int i = 0; i < 6; i++) Assert.IsTrue(seen.Contains(i));
    }

    [Test]
    public void Shuffle_SeededRng_IsDeterministic()
    {
        var a = MusicPlaylistShuffler.Shuffle(8, new Random(1234));
        var b = MusicPlaylistShuffler.Shuffle(8, new Random(1234));

        CollectionAssert.AreEqual(a, b, "aynı seed aynı sırayı üretmeli — EditMode determinizm gereksinimi");
    }

    [Test]
    public void ShuffleAvoidingBoundaryRepeat_NeverStartsWithPreviousLastIndex_AcrossManyRounds()
    {
        // Riskli olan tek nokta tur sınırıdır (bkz. sınıf başı yorum) — çok turlu bir
        // simülasyonla ilk pozisyonun hiçbir zaman bir önceki turun son parçasıyla aynı
        // olmadığını doğrula (trackCount > 1).
        var rng = new Random(7);
        int previousLastIndex = -1;
        const int trackCount = 5;

        for (int round = 0; round < 500; round++)
        {
            var shuffled = MusicPlaylistShuffler.ShuffleAvoidingBoundaryRepeat(trackCount, rng, previousLastIndex);

            Assert.AreEqual(trackCount, shuffled.Count);
            if (previousLastIndex >= 0)
            {
                Assert.AreNotEqual(previousLastIndex, shuffled[0],
                    $"tur {round}: ilk parça bir önceki turun son parçasıyla aynı olmamalı");
            }

            previousLastIndex = shuffled[shuffled.Count - 1];
        }
    }

    [Test]
    public void ShuffleAvoidingBoundaryRepeat_ResultIsStillPermutation_AfterSwap()
    {
        // previousLastIndex=0 ile zorla swap tetiklensin (Shuffle sonucu tesadüfen 0 ile
        // başlamayabilir ama seed 99 ile 0'la başladığı doğrulanmış); permütasyon bozulmamalı.
        var shuffled = MusicPlaylistShuffler.ShuffleAvoidingBoundaryRepeat(6, new Random(99), previousLastIndex: 0);

        var seen = new HashSet<int>(shuffled);
        Assert.AreEqual(6, seen.Count);
    }

    [Test]
    public void ShuffleAvoidingBoundaryRepeat_SingleTrack_RepeatUnavoidable_NoThrow()
    {
        // trackCount <= 1 -> takas edilecek başka pozisyon yok, tekrar kaçınılmaz (Menu/Closing
        // gibi tek parçalı aileler). Sınıf başı yorum: "dokunulmaz".
        var shuffled = MusicPlaylistShuffler.ShuffleAvoidingBoundaryRepeat(1, new Random(5), previousLastIndex: 0);

        Assert.AreEqual(1, shuffled.Count);
        Assert.AreEqual(0, shuffled[0]);
    }

    [Test]
    public void ShuffleAvoidingBoundaryRepeat_ZeroTracks_ReturnsEmpty_NoThrow()
    {
        var shuffled = MusicPlaylistShuffler.ShuffleAvoidingBoundaryRepeat(0, new Random(5), previousLastIndex: -1);

        Assert.AreEqual(0, shuffled.Count);
    }
}
