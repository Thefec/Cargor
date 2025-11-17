using Unity.Netcode;
using System;

[Serializable]
public struct QuestProgress : INetworkSerializable, IEquatable<QuestProgress>
{
    public int questID;
    public int currentProgress;
    public int requiredAmount;
    public bool isAccepted;
    public bool isCompleted;
    public bool isRewardClaimed;
    public bool isPenaltyApplied; // ✨ YENİ: Ceza uygulandı mı?

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref questID);
        serializer.SerializeValue(ref currentProgress);
        serializer.SerializeValue(ref requiredAmount);
        serializer.SerializeValue(ref isAccepted);
        serializer.SerializeValue(ref isCompleted);
        serializer.SerializeValue(ref isRewardClaimed);
        serializer.SerializeValue(ref isPenaltyApplied); // ✨ YENİ
    }

    public bool Equals(QuestProgress other)
    {
        return questID == other.questID &&
               currentProgress == other.currentProgress &&
               requiredAmount == other.requiredAmount &&
               isAccepted == other.isAccepted &&
               isCompleted == other.isCompleted &&
               isRewardClaimed == other.isRewardClaimed &&
               isPenaltyApplied == other.isPenaltyApplied; // ✨ YENİ
    }

    public override bool Equals(object obj)
    {
        return obj is QuestProgress other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(questID, currentProgress, requiredAmount, isAccepted, isCompleted, isRewardClaimed, isPenaltyApplied);
    }

    public static bool operator ==(QuestProgress left, QuestProgress right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(QuestProgress left, QuestProgress right)
    {
        return !left.Equals(right);
    }
}