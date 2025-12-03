using System;
using Unity.Netcode;

namespace NewCss.Quest
{
    /// <summary>
    /// Buff veri yapısı - Network üzerinden senkronize edilir
    /// </summary>
    [Serializable]
    public struct BuffData : INetworkSerializable, IEquatable<BuffData>
    {
        public BuffType buffType;
        public float amount;
        public int remainingDays;  // 0 = kalıcı, > 0 = geçici
        public bool isActive;

        public BuffData(BuffType type, float value, int days = 0)
        {
            buffType = type;
            amount = value;
            remainingDays = days;
            isActive = true;
        }

        /// <summary>
        /// Buff kalıcı mı?
        /// </summary>
        public bool IsPermanent => remainingDays <= 0;

        /// <summary>
        /// Buff süresi doldu mu?
        /// </summary>
        public bool IsExpired => !IsPermanent && remainingDays <= 0;

        /// <summary>
        /// Buff'ın açıklamasını döndürür
        /// </summary>
        public string GetDescription()
        {
            string prefix = amount >= 0 ? "+" : "";
            string duration = IsPermanent ? "(Kalıcı)" : $"({remainingDays} gün kaldı)";

            return buffType switch
            {
                BuffType.MaxStamina => $"{prefix}{amount:F0} Maks. Stamina {duration}",
                BuffType.MoveSpeed => $"{prefix}{amount:F1} Hareket Hızı {duration}",
                BuffType.CustomerWaitTime => $"{prefix}{amount:F0}s Müşteri Bekleme {duration}",
                BuffType.WalkSpeed => $"{prefix}{amount:F1} Yürüme Hızı {duration}",
                BuffType.StaminaRegenRate => $"{prefix}{amount:F1} Stamina Yenilenme {duration}",
                BuffType.DayDuration => $"{prefix}{amount:F0}s Gün Süresi {duration}",
                BuffType.MaxQueueSize => $"{prefix}{amount:F0} Maks. Müşteri {duration}",
                BuffType.PenaltyReduction => $"{prefix}{amount:F0}% Ceza Azaltma {duration}",
                BuffType.TempMoneyPerBox => $"{prefix}{amount:F0} Kutu Başı Para {duration}",
                BuffType.TempSpeedBoost => $"{prefix}{amount:F1} Hız {duration}",
                _ => $"{prefix}{amount} {duration}"
            };
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref buffType);
            serializer.SerializeValue(ref amount);
            serializer.SerializeValue(ref remainingDays);
            serializer.SerializeValue(ref isActive);
        }

        public bool Equals(BuffData other)
        {
            return buffType == other.buffType &&
                   UnityEngine.Mathf.Approximately(amount, other.amount) &&
                   remainingDays == other.remainingDays &&
                   isActive == other.isActive;
        }

        public override bool Equals(object obj) => obj is BuffData other && Equals(other);
        public override int GetHashCode() => HashCode.Combine((int)buffType, amount, remainingDays, isActive);
        public static bool operator ==(BuffData left, BuffData right) => left.Equals(right);
        public static bool operator !=(BuffData left, BuffData right) => !left.Equals(right);
    }
}
