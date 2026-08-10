namespace NewCss.Rooms.Core
{
    /// <summary>
    /// Saf, motor referanssız eksen-hizalı kutu (AABB). UnityEngine.Bounds/Vector3
    /// KULLANILMAZ — bu tip "noEngineReferences" assembly'de yaşıyor (headless test için).
    /// Sınırlar dahildir (kapsayıcı, "&gt;=" / "&lt;=") — bkz. RoomResolverTests.
    /// </summary>
    public readonly struct RoomBox
    {
        public readonly float MinX;
        public readonly float MinY;
        public readonly float MinZ;
        public readonly float MaxX;
        public readonly float MaxY;
        public readonly float MaxZ;

        public RoomBox(float minX, float minY, float minZ, float maxX, float maxY, float maxZ)
        {
            MinX = minX;
            MinY = minY;
            MinZ = minZ;
            MaxX = maxX;
            MaxY = maxY;
            MaxZ = maxZ;
        }

        /// <summary>Nokta kutunun içinde mi (sınır dahil).</summary>
        public bool Contains(float x, float y, float z)
        {
            return x >= MinX && x <= MaxX
                && y >= MinY && y <= MaxY
                && z >= MinZ && z <= MaxZ;
        }

        /// <summary>Her yöne "margin" kadar büyütülmüş yeni kutu (histerezis sorgusu için).</summary>
        public RoomBox Expanded(float margin)
        {
            return new RoomBox(
                MinX - margin, MinY - margin, MinZ - margin,
                MaxX + margin, MaxY + margin, MaxZ + margin);
        }
    }
}
