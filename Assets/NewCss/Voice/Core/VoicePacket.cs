using System;

namespace NewCss.Voice.Core
{
    /// <summary>
    /// Telsiz paket başlığındaki bayraklar. Gönderen kimliği yükte YOK (server relay sırasında
    /// ekliyor — bkz. TryWriteRelayHeader) çünkü client'ın kendi kimliğini yazması spoofing kapısı
    /// açar; server-authoritative relay bunu engelliyor.
    /// </summary>
    [Flags]
    public enum VoicePacketFlags : byte
    {
        None = 0,
        BurstStart = 1 << 0,
        BurstEnd = 1 << 1,
        Continuation = 1 << 2,
    }

    /// <summary>
    /// 4 byte'lık telsiz paket başlığının encode/decode'u: Version(1) + Flags(1) + Sequence(2, uint16 sarmalı).
    /// Sunucu relay'inin eklediği 8 byte'lık ulong clientId başlığı için de ayrı yardımcılar var
    /// (TryWriteRelayHeader/TryReadRelayHeader) — client'tan client'a asla gitmez, sadece server'ın
    /// relay çıkışında yükün önüne eklenir.
    ///
    /// Hiçbir Decode/Read fonksiyonu exception ATMAZ: ağdan gelen veri her zaman şüpheli kabul
    /// edilir (bozuk/kısa/eski build'den gelen paket) — bunlar sessizce `false` ile düşer, ana
    /// döngü hiçbir zaman try/catch'e ihtiyaç duymaz.
    /// </summary>
    public static class VoicePacket
    {
        public const int HeaderSize = 4;
        public const int RelayHeaderSize = 8;

        /// <summary>
        /// Şu anki build'in ürettiği paket versiyonu. Bilinmeyen versiyon gelirse (mixed-build —
        /// biri eski, biri yeni client) TryReadHeader sessizce `false` döner; sürüm uyuşmazlığı
        /// bir çökme değil, sadece "bu paketi anlamıyorum" hâline gelir.
        /// </summary>
        public const byte CurrentVersion = 1;

        /// <summary>
        /// 4 byte'lık başlığı buffer[offset..] konumuna yazar. Çağıran, buffer'da en az
        /// HeaderSize byte yer olduğundan emin olmalı; yetersizse false döner (exception yok).
        /// </summary>
        public static bool TryWriteHeader(byte[] buffer, int offset, int count, VoicePacketFlags flags, ushort sequence)
        {
            if (!FitsHeader(buffer, offset, count, HeaderSize)) return false;

            buffer[offset] = CurrentVersion;
            buffer[offset + 1] = (byte)flags;
            buffer[offset + 2] = (byte)(sequence & 0xFF);
            buffer[offset + 3] = (byte)((sequence >> 8) & 0xFF);
            return true;
        }

        /// <summary>
        /// 4 byte'lık başlığı okur. Kısa/bozuk buffer veya bilinmeyen Version → exception ATMADAN
        /// `false`; bu durumda out parametreleri anlamsızdır (varsayılan değerlerde bırakılır).
        /// </summary>
        public static bool TryReadHeader(byte[] buffer, int offset, int count, out VoicePacketFlags flags, out ushort sequence)
        {
            flags = VoicePacketFlags.None;
            sequence = 0;

            if (!FitsHeader(buffer, offset, count, HeaderSize)) return false;

            byte version = buffer[offset];
            if (version != CurrentVersion) return false; // mixed-build kazası sessizce düşsün

            flags = (VoicePacketFlags)buffer[offset + 1];
            sequence = (ushort)(buffer[offset + 2] | (buffer[offset + 3] << 8));
            return true;
        }

        /// <summary>
        /// Server relay'inin gönderene ait yükün önüne eklediği 8 byte'lık ulong clientId başlığı.
        /// Bu, VoicePacket'in 4 byte'lık başlığından AYRI ve SONRA gelir (relay client'a giden
        /// paket = [4B VoicePacket header][8B clientId][ses verisi]).
        /// </summary>
        public static bool TryWriteRelayHeader(byte[] buffer, int offset, int count, ulong clientId)
        {
            if (!FitsHeader(buffer, offset, count, RelayHeaderSize)) return false;

            for (int i = 0; i < RelayHeaderSize; i++)
                buffer[offset + i] = (byte)(clientId >> (8 * i));
            return true;
        }

        public static bool TryReadRelayHeader(byte[] buffer, int offset, int count, out ulong clientId)
        {
            clientId = 0;
            if (!FitsHeader(buffer, offset, count, RelayHeaderSize)) return false;

            ulong value = 0;
            for (int i = 0; i < RelayHeaderSize; i++)
                value |= ((ulong)buffer[offset + i]) << (8 * i);

            clientId = value;
            return true;
        }

        private static bool FitsHeader(byte[] buffer, int offset, int count, int headerSize)
        {
            if (buffer == null) return false;
            if (offset < 0 || count < headerSize) return false;
            if (offset + headerSize > buffer.Length) return false;
            return true;
        }
    }
}
