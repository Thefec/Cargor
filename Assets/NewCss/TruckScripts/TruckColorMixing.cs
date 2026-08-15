using System;
using UnityEngine;

namespace NewCss
{
    /// <summary>
    /// Karışık tır (gün 13+ <see cref="PostRentFeatureUnlocks.MIXED_TRUCK_UNLOCK_DAY"/>) için saf mantık —
    /// renk baskınlığı, ağırlıklı RGB karışımı, teslimat sayaç azaltma ve miktar dağıtımı.
    /// Scene/network bağımsız (DraftPool.cs deseni), EditMode test edilebilir.
    /// </summary>
    public static class TruckColorMixing
    {
        /// <summary>
        /// En çok istenen renk (eşitlikte Red &gt; Yellow &gt; Blue önceliği). Tek renkli talepte
        /// (diğer ikisi 0) doğrudan o rengi döner — tek-renk davranışı bu fonksiyonun özel bir hâlidir.
        /// </summary>
        public static BoxInfo.BoxType GetDominantColor(int redCount, int yellowCount, int blueCount)
        {
            if (redCount >= yellowCount && redCount >= blueCount) return BoxInfo.BoxType.Red;
            if (yellowCount >= blueCount) return BoxInfo.BoxType.Yellow;
            return BoxInfo.BoxType.Blue;
        }

        /// <summary>
        /// Ağırlıklı RGB karışımı: (red×redColor + yellow×yellowColor + blue×blueColor) / toplam.
        /// Toplam 0 ise (edge case) beyaz döner. Alpha her zaman 1'e sabitlenir.
        /// </summary>
        public static Color GetColorMix(int redCount, int yellowCount, int blueCount,
            Color redColor, Color yellowColor, Color blueColor)
        {
            int total = redCount + yellowCount + blueCount;
            if (total <= 0) return Color.white;

            Color mix = (redColor * redCount + yellowColor * yellowCount + blueColor * blueCount) / total;
            mix.a = 1f;
            return mix;
        }

        /// <summary>
        /// Gelen kutunun rengine karşılık gelen kalan sayaç &gt;0 ise 1 azaltır ve true döner
        /// (kabul edildi). Sayaç 0/negatifse false (çağıran yanlış-teslimat yoluna düşmeli).
        /// </summary>
        public static bool TryConsumeDelivery(ref int remainingRed, ref int remainingYellow, ref int remainingBlue,
            BoxInfo.BoxType boxType)
        {
            switch (boxType)
            {
                case BoxInfo.BoxType.Red:
                    if (remainingRed <= 0) return false;
                    remainingRed--;
                    return true;
                case BoxInfo.BoxType.Yellow:
                    if (remainingYellow <= 0) return false;
                    remainingYellow--;
                    return true;
                case BoxInfo.BoxType.Blue:
                    if (remainingBlue <= 0) return false;
                    remainingBlue--;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Toplam kutu miktarını verilen renk sayısına (1-3 arası) dağıtır, her renk en az 1 kutu
        /// alır, kalan rastgele dağıtılır. totalAmount &lt; colorCount ise colorCount'a clamp edilir.
        /// </summary>
        public static int[] SplitAmount(int totalAmount, int colorCount, System.Random rng)
        {
            colorCount = Mathf.Clamp(colorCount, 1, 3);
            totalAmount = Mathf.Max(totalAmount, colorCount);

            int[] counts = new int[colorCount];
            for (int i = 0; i < colorCount; i++) counts[i] = 1;

            int remaining = totalAmount - colorCount;
            for (int i = 0; i < remaining; i++)
            {
                counts[rng.Next(colorCount)]++;
            }

            return counts;
        }
    }
}
