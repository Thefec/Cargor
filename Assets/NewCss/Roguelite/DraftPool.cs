using System;
using System.Collections.Generic;

namespace NewCss
{
    /// <summary>
    /// Roguelite draft havuz seçimi — saf mantık, scene/network bağımsız (rapor §5, spec 1.6).
    /// </summary>
    public static class DraftPool
    {
        public const int T2_UNLOCK_DAY = 5;
        public const int T3_UNLOCK_DAY = 9;
        public const int OFFER_COUNT = 3;

        public static PerkTier MaxUnlockedTier(int currentDay)
        {
            if (currentDay >= T3_UNLOCK_DAY) return PerkTier.T3;
            if (currentDay >= T2_UNLOCK_DAY) return PerkTier.T2;
            return PerkTier.T1;
        }

        public static bool IsEligible(int index, PerkTier tier, PerkKind kind, bool requiresQuest,
            int currentLevel, int maxLevel, PerkTier maxUnlocked, bool questActive)
        {
            if (currentLevel >= maxLevel) return false;
            if (requiresQuest && !questActive) return false;
            if (kind == PerkKind.LeveledBackbone) return true;
            return (int)tier <= (int)maxUnlocked;
        }

        /// <summary>eligibility[i]==true olan index'lerden en fazla count farklı, rastgele seç.</summary>
        public static List<int> SelectOffer(IReadOnlyList<bool> eligibility, int count, Random rng)
        {
            var pool = new List<int>();
            for (int i = 0; i < eligibility.Count; i++)
                if (eligibility[i]) pool.Add(i);

            for (int i = 0; i < pool.Count && i < count; i++)
            {
                int j = i + rng.Next(pool.Count - i);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }

            var result = new List<int>();
            for (int i = 0; i < pool.Count && i < count; i++) result.Add(pool[i]);
            return result;
        }
    }
}
