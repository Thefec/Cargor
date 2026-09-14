using System;
using System.Collections.Generic;

namespace NewCss.Audio
{
    /// <summary>
    /// Bir müzik ailesindeki N parçanın çalma sırasını karıştırır; aynı parçanın bir geçiş
    /// döngüsünün sonundan bir sonraki döngünün başına ARKA ARKAYA gelmesini önler (bkz.
    /// plans/ses-tasarimi.md Faz C brief madde 2, "aynı parça arka arkaya gelmesin"). Tek bir
    /// karıştırılmış tur içinde zaten hiçbir parça tekrar etmez (permütasyon) — riskli olan
    /// tek nokta tur sınırıdır, bu sınıf sadece onu ele alır.
    ///
    /// System.Random dışarıdan verilir -&gt; EditMode testinde seed'li deterministik doğrulama
    /// mümkün. UnityEngine bağımsız (bkz. NewCss.Audio.Core.asmdef, noEngineReferences=true).
    /// </summary>
    public static class MusicPlaylistShuffler
    {
        /// <summary>Fisher-Yates ile 0..trackCount-1 indekslerini karıştırır.</summary>
        public static List<int> Shuffle(int trackCount, Random rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (trackCount <= 0) return new List<int>();

            var indices = new List<int>(trackCount);
            for (int i = 0; i < trackCount; i++) indices.Add(i);

            for (int i = indices.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (indices[i], indices[j]) = (indices[j], indices[i]);
            }

            return indices;
        }

        /// <summary>
        /// Yeni bir tur karıştırır; ilk parça önceki turun SON parçasıyla aynıysa (arka arkaya
        /// tekrar) ilk pozisyonu rastgele başka biriyle takas eder. trackCount &lt;= 1 iken
        /// takas edilecek başka pozisyon yoktur — tekrar bu durumda kaçınılmazdır, dokunulmaz
        /// (tek parçalı aile: Menu, Closing).
        /// </summary>
        public static List<int> ShuffleAvoidingBoundaryRepeat(int trackCount, Random rng, int previousLastIndex)
        {
            var shuffled = Shuffle(trackCount, rng);

            if (shuffled.Count > 1 && shuffled[0] == previousLastIndex)
            {
                int swapWith = rng.Next(1, shuffled.Count);
                (shuffled[0], shuffled[swapWith]) = (shuffled[swapWith], shuffled[0]);
            }

            return shuffled;
        }
    }
}
