namespace NewCss.Quest
{
    /// <summary>
    /// Buff türleri
    /// </summary>
    public enum BuffType
    {
        // Permanent buffs
        MaxStamina = 0,
        MoveSpeed = 1,
        CustomerWaitTime = 2,
        WalkSpeed = 3,
        StaminaRegenRate = 4,
        DayDuration = 5,
        MaxQueueSize = 6,
        PenaltyReduction = 7,

        // Temporary buffs (gün bazlı)
        TempMoneyPerBox = 100,
        TempSpeedBoost = 101
    }
}
