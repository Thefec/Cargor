using System;

namespace NewCss.Audio
{
    /// <summary>
    /// Gün fazı -&gt; müzik ailesi kararında kullanılan eşikler (bkz.
    /// plans/ses-tasarimi.md Faz C brief madde 3). MusicDirector Inspector'dan bunu besler —
    /// playtest'te değişecek değerler burada, kodda gömülü sabit DEĞİL.
    /// [Serializable]: UnityEngine referansı gerektirmez (sadece attribute), bu yüzden
    /// NewCss.Audio.Core'un noEngineReferences kuralını bozmaz — struct hâlâ System.dışında
    /// hiçbir engine tipine bağımlı değil.
    /// </summary>
    [Serializable]
    public struct MusicPhaseThresholds
    {
        /// <summary>Günün bitmesine bu kadar saniye veya daha az kalınca Tension aileye geçilir.</summary>
        public float TensionSeconds;

        /// <summary>Müşteri kuyruğu bu değere ulaşınca/geçince Busy aileye geçilir. Tension
        /// kontrolünden SONRA değerlendirilir — son saniyelerde kuyruk kalabalık olsa da
        /// Tension önceliklidir (bkz. MusicPhaseSelector).</summary>
        public int BusyQueueThreshold;

        public static MusicPhaseThresholds Default => new MusicPhaseThresholds
        {
            TensionSeconds = 30f,
            BusyQueueThreshold = 3
        };
    }
}
