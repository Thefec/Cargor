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
            int currentLevel, int maxLevel, PerkTier maxUnlocked, bool questActive,
            bool disabledInDraft = false)
        {
            if (currentLevel >= maxLevel) return false;
            if (disabledInDraft) return false;
            if (requiresQuest && !questActive) return false;
            if (kind == PerkKind.LeveledBackbone) return true;
            return (int)tier <= (int)maxUnlocked;
        }

        /// <summary>eligibility[i]==true olan index'lerden en fazla count farklı, rastgele seç.</summary>
        public static List<int> SelectOffer(IReadOnlyList<bool> eligibility, int count, Random rng)
            => SelectOffer(eligibility, count, rng, null);

        /// <summary>
        /// eligibility[i]==true olan index'lerden en fazla count farklı, rastgele seç.
        /// exclusionGroups: her biri "aynı grup içinden aynı teklifte en fazla 1 index seçilebilir"
        /// kuralını taşır (örn. gambler_case/all_in). 2'den az elemanlı gruplar etkisizdir.
        /// Seçim algoritması, exclusionGroups null/boş olduğunda önceki davranışla bit-bit aynı
        /// sonucu üretir (partial Fisher-Yates ilk `count` konumu, atlama olmadığı sürece değişmez) →
        /// determinizm/seed uyumluluğu korunur.
        /// </summary>
        public static List<int> SelectOffer(IReadOnlyList<bool> eligibility, int count, Random rng,
            IReadOnlyList<IReadOnlyList<int>> exclusionGroups)
        {
            var pool = new List<int>();
            for (int i = 0; i < eligibility.Count; i++)
                if (eligibility[i]) pool.Add(i);

            Dictionary<int, int> groupOf = null;
            if (exclusionGroups != null)
            {
                for (int g = 0; g < exclusionGroups.Count; g++)
                {
                    var group = exclusionGroups[g];
                    if (group == null || group.Count < 2) continue;
                    groupOf ??= new Dictionary<int, int>();
                    foreach (var idx in group) groupOf[idx] = g;
                }
            }

            var usedGroups = groupOf != null ? new HashSet<int>() : null;
            var result = new List<int>();

            for (int i = 0; i < pool.Count && result.Count < count; i++)
            {
                int j = i + rng.Next(pool.Count - i);
                (pool[i], pool[j]) = (pool[j], pool[i]);

                int candidate = pool[i];
                if (groupOf != null && groupOf.TryGetValue(candidate, out int gid) && !usedGroups.Add(gid))
                    continue; // grubun bir üyesi zaten seçildi, bu adayı atla

                result.Add(candidate);
            }
            return result;
        }
    }
}
