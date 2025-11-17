using NewCss;
using UnityEngine;

[CreateAssetMenu(fileName = "New Quest", menuName = "Quests/Quest Data")]
public class QuestData : ScriptableObject
{
    [Header("Quest Info")]
    public string questName;
    [TextArea(3, 6)]
    public string questDescription;
    public int questID;

    [Header("Quest Tier")]
    public QuestTier questTier;

    [Header("Quest Type")]
    public QuestType questType;

    [Header("Quest Requirements")]
    public int requiredAmount;
    public BoxInfo.BoxType targetBoxType;

    [Header("Rewards")]
    public int moneyReward;
    public float prestigeReward;

    // ✨ YENİ: Ceza sistemi
    [Header("Penalties (if not completed)")]
    [Tooltip("Para cezası (kabul edilip tamamlanmazsa)")]
    public int moneyPenalty;

    [Tooltip("Prestij cezası (kabul edilip tamamlanmazsa)")]
    public float prestigePenalty;
}

public enum QuestType
{
    PackageBoxes,
    ServeCustomers,
    DeliverTrucks,
    PlaceOnShelf
}

public enum QuestTier
{
    Easy = 1,
    Medium = 2,
    Hard = 3
}