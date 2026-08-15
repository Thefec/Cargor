namespace NewCss
{
    /// <summary>
    /// Kira sonrası (gün 5/9/13) sabit özellik unlock günleri — <see cref="DraftPool"/>'daki
    /// T2_UNLOCK_DAY/T3_UNLOCK_DAY deseniyle tutarlı statik sabitler (SO'ya değil, const'a).
    /// Saf, scene/network bağımsız — currentDay/roll gibi ilkel değerler dışarıdan verilir.
    ///
    /// Faz A (gün 5, iade/BoxRequest) bu dosyadaki RETURN_* sabitlerini kullanır.
    /// DUAL_ITEM_*/MIXED_TRUCK_* sabitleri Faz B/C için burada baştan birlikte tanımlandı
    /// (üç fazın günleri aynı anda kararlaştırıldığı için, ayrı PR'larda çakışma olmasın diye)
    /// ama Faz A onları henüz KULLANMIYOR — o fazlar ayrı görevler.
    /// </summary>
    public static class PostRentFeatureUnlocks
    {
        /// <summary>Gün 5 (1. kira sonrası): İade (BoxRequest) modu açılır.</summary>
        public const int RETURN_UNLOCK_DAY = 5;

        /// <summary>
        /// İade moduna giren müşteri oranı — economist kararı (%20-30 aralığında %25 seçildi,
        /// bkz. .claude/agent-memory/economist/post_rent_features_pricing_2026-08-15.md karar #1).
        /// </summary>
        public const float RETURN_MODE_CHANCE = 0.25f;

        /// <summary>Gün 9 (2. kira sonrası): 2 kalem modu açılır (Faz B, henüz kullanılmıyor).</summary>
        public const int DUAL_ITEM_UNLOCK_DAY = 9;

        /// <summary>Gün 13 (3. kira sonrası): karışık renk tır modu açılır (Faz C, henüz kullanılmıyor).</summary>
        public const int MIXED_TRUCK_UNLOCK_DAY = 13;

        /// <summary>
        /// Bugün İade (BoxRequest) modu havuza girmiş olmalı mı? Saf fonksiyon: gün eşiği geçilmiş
        /// VE verilen roll (çağıran taraf Random.value versin) oran altında mı kontrol eder.
        /// </summary>
        /// <param name="currentDay">Oyunun mevcut günü (1-tabanlı).</param>
        /// <param name="roll">[0,1) aralığında rastgele çekilmiş değer.</param>
        public static bool ShouldEnterBoxRequestMode(int currentDay, float roll)
        {
            if (currentDay < RETURN_UNLOCK_DAY) return false;
            return roll < RETURN_MODE_CHANCE;
        }
    }
}
