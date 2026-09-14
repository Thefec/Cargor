namespace NewCss.Audio
{
    /// <summary>
    /// Gün fazından müzik ailesine saf karar mantığı (bkz. plans/ses-tasarimi.md Faz C brief
    /// madde 3). DayCycleManager/CustomerManager'a DOĞRUDAN bağımlı DEĞİL — MusicDirector bu
    /// sistemlerden okuduğu değerleri buraya parametre olarak geçirir. UnityEngine bağımsız
    /// (bkz. NewCss.Audio.Core.asmdef, noEngineReferences=true) — EditMode testinden Unity
    /// başlatmadan çağrılabilir.
    ///
    /// Öncelik sırası (üstteki alttakini ezer): Closing &gt; Tension &gt; Busy &gt; Main.
    /// Tension'ın Busy'yi ezmesi KASITLI: son ~30 saniyede müşteri kuyruğu kalabalık olsa
    /// bile gerilim müziği çalmalı (plan: "son ~30 saniye Tension").
    /// </summary>
    public static class MusicPhaseSelector
    {
        public static MusicFamily SelectGameplayFamily(
            bool isDayOver,
            float elapsedSeconds,
            float dayDurationSeconds,
            int customerQueueSize,
            MusicPhaseThresholds thresholds)
        {
            if (isDayOver) return MusicFamily.Closing;

            // dayDurationSeconds <= 0 -> gün henüz kurulmamış/geçersiz veri; Tension'ı
            // tetiklememek için bu dalı atla (0 kalan süre "gerilim" değil "veri yok" demek).
            if (dayDurationSeconds > 0f)
            {
                float remaining = dayDurationSeconds - elapsedSeconds;
                if (remaining <= thresholds.TensionSeconds) return MusicFamily.Tension;
            }

            if (customerQueueSize >= thresholds.BusyQueueThreshold) return MusicFamily.Busy;

            return MusicFamily.Main;
        }
    }
}
