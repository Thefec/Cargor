using System;
using Unity.Collections;
using Unity.Netcode;

namespace NewCss
{
    /// <summary>
    /// Server-authoritative oyuncu roster elemanı.
    /// GameStateManager.PlayerRoster (NetworkList) içinde tutulur; Break Room ve
    /// End Game (Win/Lose) UI'ları TEK bu kaynaktan oyuncu listesini okur.
    /// </summary>
    public struct PlayerRosterEntry : INetworkSerializable, IEquatable<PlayerRosterEntry>
    {
        public ulong ClientId;
        public FixedString64Bytes Name;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref Name);
        }

        public bool Equals(PlayerRosterEntry other)
        {
            return ClientId == other.ClientId && Name.Equals(other.Name);
        }

        public override bool Equals(object obj) => obj is PlayerRosterEntry other && Equals(other);

        public override int GetHashCode() => ClientId.GetHashCode();
    }
}
