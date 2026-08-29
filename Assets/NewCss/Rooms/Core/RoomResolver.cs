namespace NewCss.Rooms.Core
{
    /// <summary>
    /// Bir oda kutusu + o kutunun ait olduğu oda id'si. Aynı roomId'yi birden çok
    /// RoomEntry paylaşabilir (L şekilli oda = 2 kutu, tek id) — plan §3.1/§H5.
    /// </summary>
    public readonly struct RoomEntry
    {
        public readonly int RoomId;
        public readonly RoomBox Box;

        public RoomEntry(int roomId, RoomBox box)
        {
            RoomId = roomId;
            Box = box;
        }
    }

    /// <summary>
    /// "Bu nokta hangi odada" sorusunun tek gerçek mantığı. Saf/statik, state
    /// TUTMAZ — çağıran taraf (RoomRegistry) "current" oda id'sini kendi saklar.
    ///
    /// Yapışkan/histerezis sözleşmesi (plan §3.1, §10):
    ///  - kutular sorguda "margin" ile genişletilir (komşular çakışsın);
    ///  - hiçbir genişletilmiş kutuda değilse "current" AYNEN döner (asla -1 üretmez);
    ///  - >=1 genişletilmiş kutu eşleşirse ve bunlardan biri "current" ise, "current"
    ///    korunur (kararsız bölgede fikir değiştirmez);
    ///  - "current" eşleşenler arasında yoksa ilk/deterministik eşleşme döner;
    ///  - 0 kutu (oda sistemi hiç kurulmamış, örn. Tutorial) → NoRoomSentinel,
    ///    exception ATMAZ.
    /// </summary>
    public static class RoomResolver
    {
        /// <summary>Varsayılan sorgu genişletmesi — kapı ağzı titremesini önler.</summary>
        public const float DefaultMargin = 0.35f;

        /// <summary>"Oda yok" sentinel'i: 0 kutu girdisinde ve başlangıç "current" değeri olarak kullanılır.</summary>
        public const int NoRoomSentinel = -1;

        public static int Resolve(int current, float x, float y, float z, RoomEntry[] entries, float margin = DefaultMargin)
        {
            if (entries == null || entries.Length == 0)
            {
                return NoRoomSentinel;
            }

            bool anyMatch = false;
            bool currentMatched = false;
            int firstMatch = NoRoomSentinel;

            for (int i = 0; i < entries.Length; i++)
            {
                RoomBox expanded = entries[i].Box.Expanded(margin);
                if (!expanded.Contains(x, y, z))
                {
                    continue;
                }

                if (!anyMatch)
                {
                    firstMatch = entries[i].RoomId;
                }
                anyMatch = true;

                if (entries[i].RoomId == current)
                {
                    currentMatched = true;
                }
            }

            if (!anyMatch)
            {
                // Hiçbir genişletilmiş kutuda değil — kapı ağzı/koridor arası. H8: current korunur.
                return current;
            }

            if (currentMatched)
            {
                // Mevcut oda hâlâ (genişletilmiş) eşleşenler arasında — histerezis onu tercih eder.
                return current;
            }

            return firstMatch;
        }
    }
}
