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
}
