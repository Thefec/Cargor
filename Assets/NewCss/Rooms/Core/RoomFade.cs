namespace NewCss.Rooms.Core
{
    /// <summary>
    /// Karartma geçiş eğrisi — saf statik yardımcı, state tutmaz.
    /// KASITLI OLARAK Lerp semantiği DEĞİL: Lerp asimptotik yaklaşır, hedefe asla
    /// tam ulaşmaz (0.9 -&gt; 0 sonsuz adımda bile ~0.002 kalıcı kalabilir). Burada
    /// "MoveTowards" mantığı kullanılır: sabit hızla ilerler, hedefi geçmeden tam
    /// hedef değere (0f/1f) kilitlenir. UnityEngine.Mathf KULLANILAMAZ (motor
    /// referanssız assembly).
    /// </summary>
    public static class RoomFade
    {
        /// <summary>
        /// "cur" değerini "target" hedefine "speed" birim/saniye hızla "dt" kadar
        /// ilerletir. Hedefi asla geçmez, tam üstüne oturur.
        ///
        /// Hedef KASITLI olarak bool değil float: karartma artık yalnız "açık/kapalı"
        /// değil — normal oyun görünümünde ara bir şiddette (0..1) durabiliyor, X'e
        /// basılınca tam güce (1) çıkıyor. Bool olsaydı bu ara durum ifade edilemezdi.
        /// </summary>
        public static float Step(float cur, float target, float dt, float speed)
        {
            if (cur == target)
            {
                return target;
            }

            float delta = speed * dt;
            if (delta <= 0f)
            {
                // dt veya speed 0/negatif ise hareket yok — mevcut değeri koru.
                return cur;
            }

            if (cur < target)
            {
                float next = cur + delta;
                return next >= target ? target : next;
            }
            else
            {
                float next = cur - delta;
                return next <= target ? target : next;
            }
        }
    }
}
