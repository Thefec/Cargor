using System;

namespace NewCss.Audio
{
    /// <summary>
    /// İki parça arası eşit-güç (equal-power) çapraz geçiş ses seviyeleri. Lineer fade yerine
    /// eşit-güç kullanılıyor: iki kaynağın toplam algılanan yüksekliği geçişin ortasında
    /// çökmüyor (klasik lineer crossfade'in "orta nokta çukuru" sorunu). t=0 saf çıkan parça,
    /// t=1 saf giren parça. UnityEngine bağımsız (bkz. NewCss.Audio.Core.asmdef,
    /// noEngineReferences=true) — Mathf yerine System.Math kullanır.
    /// </summary>
    public static class MusicCrossfadeMath
    {
        public static void GetVolumes(float t, out float outgoingVolume, out float incomingVolume)
        {
            t = Clamp01(t);
            double angle = t * Math.PI / 2.0;
            outgoingVolume = (float)Math.Cos(angle);
            incomingVolume = (float)Math.Sin(angle);
        }

        /// <summary>
        /// Bir sonraki parçaya geçişin BAŞLAMASI gereken zaman damgası (saniye, çalan parçanın
        /// başından itibaren). Parça crossfadeDuration'dan kısaysa negatife düşmesin diye 0'a
        /// klipleniyor — geçiş parça başlar başlamaz tetiklenir.
        /// </summary>
        public static float CrossfadeStartTime(float trackLength, float crossfadeDuration)
        {
            float start = trackLength - crossfadeDuration;
            return start < 0f ? 0f : start;
        }

        private static float Clamp01(float v)
        {
            if (v < 0f) return 0f;
            if (v > 1f) return 1f;
            return v;
        }
    }
}
