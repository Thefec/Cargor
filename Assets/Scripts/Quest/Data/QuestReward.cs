using System;
using UnityEngine;

namespace NewCss.Quest
{
    /// <summary>
    /// Görev ödül/ceza yapısı
    /// </summary>
    [Serializable]
    public class QuestReward
    {
        [Tooltip("Ödül/Ceza türü")]
        public RewardType rewardType;

        [Tooltip("Miktar (negatif = ceza)")]
        public float amount;

        [Tooltip("Geçici buff için gün sayısı (sadece TempMoneyBoost, TempSpeedBoost için)")]
        public int durationDays;

        /// <summary>
        /// Bu bir ceza mı?
        /// </summary>
        public bool IsPenalty => amount < 0;

        /// <summary>
        /// Ödülün açıklamasını döndürür
        /// </summary>
        public string GetDescription()
        {
            string prefix = amount >= 0 ? "+" : "";

            return rewardType switch
            {
                RewardType.Money => $"{prefix}{amount:F0} Para",
                RewardType.Prestige => $"{prefix}{amount:F0} Prestij",
                RewardType.MaxStamina => $"{prefix}{amount:F0} Maks. Stamina",
                RewardType.MoveSpeed => $"{prefix}{amount:F1} Hareket Hızı",
                RewardType.CustomerWaitTime => $"{prefix}{amount:F0}s Müşteri Bekleme Süresi",
                RewardType.WalkSpeed => $"{prefix}{amount:F1} Yürüme Hızı",
                RewardType.StaminaRegenRate => $"{prefix}{amount:F1} Stamina Yenilenme",
                RewardType.DayDuration => $"{prefix}{amount:F0}s Gün Süresi",
                RewardType.MaxQueueSize => $"{prefix}{amount:F0} Maks. Müşteri",
                RewardType.TempMoneyBoost => $"{durationDays} gün +{amount:F0} Kutu Başı Para",
                RewardType.TempSpeedBoost => $"{durationDays} gün +{amount:F1} Hız",
                RewardType.PenaltyReduction => $"{prefix}{amount:F0}% Ceza Azaltma",
                _ => $"{prefix}{amount}"
            };
        }
    }
}
