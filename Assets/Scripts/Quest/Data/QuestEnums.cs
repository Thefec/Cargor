namespace NewCss.Quest
{
    /// <summary>
    /// Görev zorluk tier'ı
    /// </summary>
    public enum QuestTier
    {
        Easy = 0,
        Medium = 1,
        Hard = 2
    }

    /// <summary>
    /// Görev durumu
    /// </summary>
    public enum QuestStatus
    {
        Available = 0,
        Active = 1,
        Completed = 2,
        Collected = 3,
        Failed = 4
    }

    /// <summary>
    /// Görev türü - hangi sistem takip edecek
    /// </summary>
    public enum QuestType
    {
        CompleteMinigame = 0,
        PlaceBoxOnShelf = 1,
        CompleteTruck = 2,
        ServeCustomer = 3,
        IgnoreCustomer = 4,
        PackToy = 5
    }

    /// <summary>
    /// Ödül/Ceza türü
    /// </summary>
    public enum RewardType
    {
        Money = 0,
        Prestige = 1,
        MaxStamina = 2,
        MoveSpeed = 3,
        CustomerWaitTime = 4,
        WalkSpeed = 5,
        StaminaRegenRate = 6,
        DayDuration = 7,
        MaxQueueSize = 8,
        TempMoneyBoost = 9,
        TempSpeedBoost = 10,
        PenaltyReduction = 11
    }
}
