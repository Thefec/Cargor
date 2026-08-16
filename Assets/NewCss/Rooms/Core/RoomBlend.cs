namespace NewCss.Rooms.Core
{
    /// <summary>
    /// Oda geçişinde iki-kutulu çapraz-geçiş (cross-fade) için saf ilerleme + yumuşatma
    /// matematiği. Saf/statik, state TUTMAZ. UnityEngine.Mathf KULLANILAMAZ (motor
    /// referanssız assembly) — bkz. RoomFade.cs deseni.
    ///
    /// RoomFade'den farkı: RoomFade "hız" (birim/sn, MoveTowards) esasına dayanır; burada
    /// kullanıcı "süre" (saniye) dilinde konuşuyor (~1-2 saniyelik yumuşak aydınlanma) →
    /// Advance() lineer, roomBlendDuration saniyede 0'dan 1'e TAM ulaşır. Şablona giden ham
    /// değer lineerdir; shader'a giden değer ayrıca Smoothstep01 ile yumuşatılır (başlangıç/
    /// bitiş yavaş — "hafif aydınlanma" hissi).
    /// </summary>
    public static class RoomBlend
    {
        /// <summary>
        /// "progress" (0..1) değerini "dt" kadar, "duration" saniyede tamamlanacak şekilde
        /// lineer ilerletir. Hedefi (1f) asla geçmez, tam üstüne oturur (RoomFade.Step'teki
        /// "tam hedefe kilitlenme" ilkesiyle aynı — H7 sözleşmesi). duration &lt;= 0 ise
        /// (yanlış ayar / sıfır bölme) anında 1f döner.
        /// </summary>
        public static float Advance(float progress, float dt, float duration)
        {
            if (progress >= 1f)
            {
                return 1f;
            }

            if (duration <= 0f)
            {
                return 1f;
            }

            float next = progress + dt / duration;
            if (next >= 1f)
            {
                return 1f;
            }

            return next <= 0f ? 0f : next;
        }

        /// <summary>
        /// Klasik smoothstep (3t² - 2t³). Girdi [0,1] dışındaysa önce clamp'lenir; t=0 ve
        /// t=1'de TAM 0f/1f döner (H7 ile aynı ilke — shader'a giden değer asla asimptotik
        /// kalmaz, iki-kutulu karışım sözleşmesi bozulmaz).
        /// </summary>
        public static float Smoothstep01(float t)
        {
            float clamped = t <= 0f ? 0f : (t >= 1f ? 1f : t);
            return clamped * clamped * (3f - 2f * clamped);
        }
    }
}
