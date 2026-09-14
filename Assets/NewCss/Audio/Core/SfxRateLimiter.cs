namespace NewCss.Audio
{
    /// <summary>
    /// SfxBus.Play'in "çok sık çalma" korumasının motor-bağımsız çekirdeği (bkz.
    /// plans/ses-tasarimi.md — CounterTick en çok hissedilen juice ama sık çalarsa rahatsız eder).
    /// UnityEngine bağımsız (NewCss.Audio.Core.asmdef, noEngineReferences=true — AudioVolumeMath /
    /// NumberRoller ile aynı desen), EditMode testinden Unity başlatmadan çağrılabilir.
    /// </summary>
    public static class SfxRateLimiter
    {
        /// <summary>
        /// minInterval &lt;= 0 ise sınır yok (her zaman true). Aksi halde son çalınmadan bu yana
        /// geçen süre minInterval'i karşılamıyorsa false döner (o çağrı yok sayılmalı).
        /// </summary>
        public static bool ShouldPlay(float lastPlayedTime, float now, float minInterval)
        {
            if (minInterval <= 0f) return true;
            return (now - lastPlayedTime) >= minInterval;
        }
    }
}
