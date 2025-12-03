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

        public QuestProgress(string id, int target)
        {
            questId = id;
            status = QuestStatus.Available;
            currentProgress = 0;
            targetProgress = target;
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
        }

        public bool Equals(QuestProgress other)
        {
            return questId.Equals(other.questId) &&
                   status == other.status &&
                   currentProgress == other.currentProgress &&
                   targetProgress == other.targetProgress;
        }

        public override bool Equals(object obj) => obj is QuestProgress other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(questId.GetHashCode(), (int)status, currentProgress, targetProgress);
        public static bool operator ==(QuestProgress left, QuestProgress right) => left.Equals(right);
        public static bool operator !=(QuestProgress left, QuestProgress right) => !left.Equals(right);
    }
}
