using System;
using System.Collections.Generic;
using NUnit.Framework;
using NewCss;

public class DraftPoolTests
{
    [Test]
    public void MaxUnlockedTier_GatesByDay()
    {
        Assert.AreEqual(PerkTier.T1, DraftPool.MaxUnlockedTier(1));
        Assert.AreEqual(PerkTier.T1, DraftPool.MaxUnlockedTier(4));
        Assert.AreEqual(PerkTier.T2, DraftPool.MaxUnlockedTier(5));
        Assert.AreEqual(PerkTier.T2, DraftPool.MaxUnlockedTier(8));
        Assert.AreEqual(PerkTier.T3, DraftPool.MaxUnlockedTier(9));
        Assert.AreEqual(PerkTier.T3, DraftPool.MaxUnlockedTier(16));
    }

    [Test]
    public void IsEligible_ExcludesMaxedAndLockedTierAndInactiveQuest()
    {
        Assert.IsTrue(DraftPool.IsEligible(0, PerkTier.T1, PerkKind.LeveledBackbone, false, 3, 10, PerkTier.T1, true));
        Assert.IsFalse(DraftPool.IsEligible(0, PerkTier.T1, PerkKind.LeveledBackbone, false, 10, 10, PerkTier.T1, true));
        Assert.IsFalse(DraftPool.IsEligible(1, PerkTier.T3, PerkKind.Perk, false, 0, 1, PerkTier.T1, true));
        Assert.IsTrue(DraftPool.IsEligible(1, PerkTier.T2, PerkKind.Perk, false, 0, 1, PerkTier.T2, true));
        Assert.IsFalse(DraftPool.IsEligible(2, PerkTier.T1, PerkKind.LeveledBackbone, true, 0, 2, PerkTier.T3, false));
    }

    [Test]
    public void SelectOffer_ReturnsDistinctEligibleUpToCount()
    {
        var eligible = new List<bool> { true, false, true, true, true };
        var offer = DraftPool.SelectOffer(eligible, 3, new Random(12345));
        Assert.AreEqual(3, offer.Count);
        CollectionAssert.AllItemsAreUnique(offer);
        foreach (var i in offer) Assert.IsTrue(eligible[i]);
    }

    [Test]
    public void SelectOffer_FewerEligibleThanCount_ReturnsAllEligible()
    {
        var eligible = new List<bool> { true, false, true, false, false };
        var offer = DraftPool.SelectOffer(eligible, 3, new Random(1));
        Assert.AreEqual(2, offer.Count);
    }

    [Test]
    public void SelectOffer_ExclusionGroup_NeverPicksTwoFromSameGroup()
    {
        // indices 1 ve 3 aynı dışlama grubunda (gambler_case/all_in gibi) — hiçbir teklifte birlikte olmamalı
        var eligible = new List<bool> { true, true, true, true, true };
        var groups = new List<IReadOnlyList<int>> { new List<int> { 1, 3 } };
        for (int seed = 0; seed < 200; seed++)
        {
            var offer = DraftPool.SelectOffer(eligible, 3, new Random(seed), groups);
            Assert.IsFalse(offer.Contains(1) && offer.Contains(3),
                $"seed {seed}: dışlama grubu ihlal edildi (1 ve 3 birlikte seçildi)");
            CollectionAssert.AllItemsAreUnique(offer);
            foreach (var i in offer) Assert.IsTrue(eligible[i]);
        }
    }

    [Test]
    public void SelectOffer_OverlappingExclusionGroups_RespectsEveryGroup()
    {
        // Bir index BİRDEN FAZLA gruba üye olabilir: index 3 = `all_in`, hem {1,3} (gambler_case/all_in)
        // hem {2,3} (leveraged_rent/all_in) grubunda. Regresyon: groupOf tek grup tuttuğu sürece ikinci
        // atama ilkini eziyordu ve {1,3} dışlaması sessizce kayboluyordu.
        var eligible = new List<bool> { true, true, true, true, true };
        var groups = new List<IReadOnlyList<int>>
        {
            new List<int> { 1, 3 },
            new List<int> { 2, 3 },
        };

        bool sawThree = false;
        for (int seed = 0; seed < 500; seed++)
        {
            var offer = DraftPool.SelectOffer(eligible, 3, new Random(seed), groups);

            Assert.IsFalse(offer.Contains(1) && offer.Contains(3),
                $"seed {seed}: 1. grup ihlal edildi (1 ve 3 birlikte)");
            Assert.IsFalse(offer.Contains(2) && offer.Contains(3),
                $"seed {seed}: 2. grup ihlal edildi (2 ve 3 birlikte)");

            CollectionAssert.AllItemsAreUnique(offer);
            if (offer.Contains(3)) sawThree = true;
        }

        // Çok gruplu index tamamen elenmemeli — dışlama, yasaklama değil.
        Assert.IsTrue(sawThree, "index 3 hiçbir teklifte çıkmadı; dışlama fazla agresif.");
    }

    [Test]
    public void SelectOffer_NullExclusionGroups_MatchesLegacyOverload()
    {
        // 4-parametreli overload, gruplar null iken 3-parametreliyle bit-bit aynı sonucu vermeli (determinizm)
        var eligible = new List<bool> { true, false, true, true, true };
        var a = DraftPool.SelectOffer(eligible, 3, new Random(777));
        var b = DraftPool.SelectOffer(eligible, 3, new Random(777), null);
        CollectionAssert.AreEqual(a, b);
    }

    [Test]
    public void RerollCurve_MatchesApprovedTable()
    {
        Assert.AreEqual(50,  RerollCurve.CostForReroll(0));
        Assert.AreEqual(90,  RerollCurve.CostForReroll(1));
        Assert.AreEqual(160, RerollCurve.CostForReroll(2));
        Assert.AreEqual(290, RerollCurve.CostForReroll(3));
        Assert.AreEqual(525, RerollCurve.CostForReroll(4));
        Assert.AreEqual(525, RerollCurve.CostForReroll(7)); // 5+ tavan
        Assert.AreEqual(50,  RerollCurve.CostForReroll(-1)); // negatif guard
    }
}
