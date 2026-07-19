using System;
using Unity.Netcode;
using Unity.Collections;

namespace NewCss.Quest
{
    /// <summary>
    /// Görev ilerleme takibi için network-serializable struct
    /// </summary>
    [Serializable]
    public struct QuestProgress : INetworkSerializable, IEquatable<QuestProgress>
    {
        public FixedString64Bytes questId;
        public QuestStatus status;
        public int currentProgress;
        public int targetProgress;

        /// <summary>
        /// F9 fix: server'ın bu quest ataması için ürettiği deterministik reroll seed'i.
        /// Client, QuestData.RerollSelection(rewardSeed) çağırarak server ile birebir aynı ödül/ceza
        /// seçimini üretir (bkz. QuestManager.GetQuestData). NetworkList replikasyonu ile late-join'de
        /// de otomatik doğru gelir.
        /// </summary>
        public int rewardSeed;

        public QuestProgress(string id, int target, int seed)
        {
            questId = id;
            status = QuestStatus.Available;
            currentProgress = 0;
            targetProgress = target;
            rewardSeed = seed;
        }

        /// <summary>
        /// Görev tamamlandı mı?
        /// </summary>
        public bool IsCompleted => currentProgress >= targetProgress;

        /// <summary>
        /// İlerleme yüzdesi (0-1)
        /// </summary>
        public float ProgressPercent => targetProgress > 0 ? (float)currentProgress / targetProgress : 0f;

        /// <summary>
        /// İlerleme açıklaması
        /// </summary>
        public string GetProgressText() => $"{currentProgress}/{targetProgress}";

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref questId);
            serializer.SerializeValue(ref status);
            serializer.SerializeValue(ref currentProgress);
            serializer.SerializeValue(ref targetProgress);
            serializer.SerializeValue(ref rewardSeed);
        }

        public bool Equals(QuestProgress other)
        {
            return questId.Equals(other.questId) &&
                   status == other.status &&
                   currentProgress == other.currentProgress &&
                   targetProgress == other.targetProgress &&
                   rewardSeed == other.rewardSeed;
        }

        public override bool Equals(object obj) => obj is QuestProgress other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(questId.GetHashCode(), (int)status, currentProgress, targetProgress, rewardSeed);
        public static bool operator ==(QuestProgress left, QuestProgress right) => left.Equals(right);
        public static bool operator !=(QuestProgress left, QuestProgress right) => !left.Equals(right);
    }
}
