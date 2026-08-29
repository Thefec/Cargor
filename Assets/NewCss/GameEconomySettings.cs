using UnityEngine;

namespace NewCss
{
    /// <summary>
    /// Tüm oyun ekonomisi sabitlerini tek bir yerden yönetir.
    /// Inspector'dan Play Mode'da bile anlık değişiklik yapılabilir.
    ///
    /// Oluşturmak için: Project panelinde sağ tık → Create → Cargor → Ekonomi Ayarları
    /// </summary>
    [CreateAssetMenu(fileName = "EkonomiAyarlari", menuName = "Cargor/Ekonomi Ayarlari")]
    public class GameEconomySettings : ScriptableObject
    {
        // ─────────────────────────────────────────────────────────────
        //  KİRA SİSTEMİ  (DayCycleManager)
        // ─────────────────────────────────────────────────────────────

        [Header("=== KİRA AYARLARI ===")]

        [Tooltip("Oyuncu sayısına göre temel kira miktarları (1P, 2P, 3P, 4P)")]
        public int[] baseRentByPlayerCount = { 500, 1000, 1450, 1800 };

        [Tooltip("Her kira döneminde kira artış çarpanı (örn: 1.3 = %30 artış)")]
        public float rentGrowthMultiplier = 1.20f;

        [Tooltip("Kaç günde bir kira alınır")]
        public int rentIntervalDays = 4;

        [Tooltip("Grace period'da oyuncudan alınan para yüzdesi (0-1). Kalan para oyuncuda kalır.")]
        public float gracePaymentPercent = 0.8f;

        [Tooltip("Kaldıraçlı Kira perki: scaledRent'e uygulanan çarpan. Perk yoksa 1f.")]
        public float rentScaledMultiplier = 1f;

        // ─────────────────────────────────────────────────────────────
        //  MÜŞTERİ KOTASI  (CustomerManager — PlateUp gün-numarası eğrisi)
        //  plans/plateup-musteri-telefon.md §A/§B, economist v2 2026-08-29
        //  (.claude/agent-memory/economist/plateup_customer_quota_2026-08-29.md)
        // ─────────────────────────────────────────────────────────────

        [Header("=== MÜŞTERİ KOTASI (PlateUp) ===")]

        [Tooltip("1 oyunculu günlük müşteri kotası eğrisi. index = gün-1 (gün 1..16). Kapasite (raf/masa) artık etkisiz — kaynak: tools/economy-sim/sim.js plateUpQuota(day,1).")]
        public int[] dailyCustomerCountP1 = { 4, 4, 4, 4, 4, 4, 4, 5, 5, 5, 5, 5, 6, 6, 6, 6 };

        [Tooltip("2 oyunculu günlük müşteri kotası eğrisi. index = gün-1.")]
        public int[] dailyCustomerCountP2 = { 7, 7, 7, 8, 8, 9, 9, 9, 10, 10, 10, 11, 11, 11, 12, 12 };

        [Tooltip("3 oyunculu günlük müşteri kotası eğrisi. index = gün-1.")]
        public int[] dailyCustomerCountP3 = { 8, 8, 8, 8, 9, 9, 9, 10, 10, 10, 11, 11, 12, 12, 12, 13 };

        [Tooltip("4 oyunculu günlük müşteri kotası eğrisi. index = gün-1. P3 ile neredeyse aynı — yuvarlama hatası değil, 1-istasyon mekanik doygunluğu (economist notu, plateup_customer_quota_2026-08-29.md).")]
        public int[] dailyCustomerCountP4 = { 8, 8, 8, 8, 9, 9, 9, 10, 10, 10, 11, 11, 12, 12, 12, 13 };

        [Tooltip("Oyuncu sayısına göre müşteriler arası temel varış aralığı (saniye). index = oyuncuSayısı-1. WaveSettings.GetSpawnRateMultiplier bu aralığı ölçekler (rush hour = kısa aralık).")]
        public float[] customerArrivalIntervalByPlayerCount = { 44f, 22f, 21f, 21f };

        [Tooltip("Kota bitip son müşteri de çıktığında, gün sonuna sarılmadan önceki kapanış payı (saniye) — oyuncuya yarım yüklü tırı taşıma fırsatı tanır (DayCycleManager.FastForwardToEndOfDay).")]
        public float dayEndGraceSeconds = 30f;

        // ─────────────────────────────────────────────────────────────
        //  TIR / TESLİMAT  (Truck)
        // ─────────────────────────────────────────────────────────────

        [Header("=== TIR / TESLİMAT AYARLARI ===")]

        [Tooltip("Doğru kutu tesliminde kutu başına ödül (TL). LEGACY skaler: yalnız rewardPerBoxByPlayerCount boş/null ise fallback olarak kullanılır.")]
        public int rewardPerBox = 50;

        [Tooltip("Oyuncu sayısına göre doğru kutu tesliminde kutu başına ödül (TL). index = oyuncuSayısı-1. Tek servis masası nedeniyle P3/P4 kotası P2 ile aynı kalırken kira 3.6x büyüdüğü için ödül artırıldı (economist plateup_customer_quota_2026-08-29.md).")]
        public int[] rewardPerBoxByPlayerCount = { 50, 55, 70, 88 };

        [Tooltip("Yanlış renk kutu tesliminde kutu başına ceza (TL)")]
        public int penaltyPerBox = 40;

        [Tooltip("Tırın hangarda bekleme süresi (saniye). Süre dolunca boş da olsa kalkar. LEGACY skaler: yalnız hangarStayDurationByPlayerCount boş/null ise fallback olarak kullanılır.")]
        public float hangarStayDuration = 30f;

        [Tooltip("Oyuncu sayısına göre tır hangar bekleme süresi (saniye). index = oyuncuSayısı-1. 1P uzun (yavaş üretim → tır dolsun; 30s'de yarı-boş kalkıyordu), 4P kısa. economist P-bazlı denge 2026-07-20 (bkz. .claude/agent-memory/economist/hangar_stay_duration_per_player.md); 1P 90→120 FAZ4 (fillTime(2)=100 > 90 idi).")]
        public int[] hangarStayDurationByPlayerCount = { 120, 60, 40, 30 };

        [Tooltip("Her prestige bonusu için gereken prestige miktarı")]
        public float prestigePerBonus = 8f;

        [Tooltip("Her prestige katmanında kutu başına eklenen bonus (TL)")]
        public float bonusPerTier = 5f;

        [Tooltip("Yüksek Volatilite perki: kutu başına ödül dağılımının +-yüzdesi (0 = kapalı).")]
        public float rewardVolatility = 0f;

        [Tooltip("Yüksek Volatilite perki: ortalama ödül çarpanı (RNG merkezi, EV her zaman pozitif olacak şekilde).")]
        public float rewardVolatilityMean = 1f;

        [Tooltip("Oyuncu sayısına göre tır kargo miktarı ALT sınırı (dahil). index = oyuncuSayısı-1. FAZ4: gelir-nötr P-bazlı kargo (bkz. plans/economy-rebuild-2026-07-30-faz4-final.md §B.5).")]
        public int[] truckCargoMinByPlayerCount = { 1, 2, 2, 2 };

        [Tooltip("Oyuncu sayısına göre tır kargo miktarı ÜST sınırı (HARİÇ — Random.Range(int,int) semantiği). index = oyuncuSayısı-1.")]
        public int[] truckCargoMaxExclusiveByPlayerCount = { 3, 4, 5, 6 };

        // ─────────────────────────────────────────────────────────────
        //  KUTU DÜŞME / ÇARPMA CEZASI  (BoxFallPenalty)
        // ─────────────────────────────────────────────────────────────

        [Header("=== KUTU DÜŞME AYARLARI ===")]

        [Tooltip("Kutu/ürün sert çarpma ile düştüğünde uygulanan para cezası (TL). Yüzeyden bağımsız (yer/duvar/raf aynı). Boş kutu ücretsiz olduğundan ceza yalnızca dikkatsizliği caydırır.")]
        public int boxDropMoneyPenalty = 5;

        // ─────────────────────────────────────────────────────────────
        //  TELEFON  (PhoneCallManager V4 - DIŞARI ARAMA, PlateUp geçişi)
        //  plans/plateup-musteri-telefon.md §D, economist v2 2026-08-29
        // ─────────────────────────────────────────────────────────────

        [Header("=== TELEFON AYARLARI (V4) ===")]

        [Tooltip("Oyuncu sayısına göre telefonla müşteri çağrıldığında atlanan oyun-dakikası. index = oyuncuSayısı-1. Kaynak: economist plateup_customer_quota_2026-08-29.md v2 (gün8 referans dönüşümle).")]
        public float[] timeSkipAmountByPlayerCount = { 115f, 59f, 55f, 55f };

        [Tooltip("Telefon çağrıları arası server-authoritative cooldown (saniye). Düz — oyuncu sayısından bağımsız (economist: P3/4'te doğal varış aralığıyla neredeyse eşit, marjinal fayda).")]
        public float phoneCooldownSeconds = 20f;

        [Tooltip("phone_line perki aktifken cooldown'dan mutlak olarak düşülen saniye (idempotent atama — PerkEffect.ApplyPhoneLine). 10f economist onaylı (2026-08-29): perk kotayı büyütmediği için ekonomik etkisi yok.")]
        public float phoneCooldownPerkBonusSeconds = 0f;

        [Tooltip("Telefonla müşteri çağrıldığında verilen para ödülü (TL)")]
        public int callMoneyReward = 20;

        [Tooltip("Telefonla müşteri çağrıldığında verilen prestij ödülü")]
        public float callPrestigeReward = 0.4f;

        // ─────────────────────────────────────────────────────────────
        //  PRESTİJ CEZA / ÖDÜL  (GameStateManager, CustomerAI, BoxFallPenalty)
        // ─────────────────────────────────────────────────────────────

        [Header("=== PRESTİJ AYARLARI ===")]

        [Tooltip("Müşteri kaçtığında (bekleme süresi dolunca VEYA gün sonunda servis edilmeden çıkarıldığında) uygulanan prestige cezası (negatif olmalı)")]
        public float customerLostPrestigePenalty = -0.4f;

        [Tooltip("Gün sonunda hiç spawn olmamış kalan kota müşterisi başına uygulanan (daha hafif) prestij cezası (negatif olmalı). customerLostPrestigePenalty ile KARIŞTIRILMAZ — bkz. GameStateManager.OnCustomerQuotaMissed. plans/plateup-musteri-telefon.md §E, economist v2 2026-08-29.")]
        public float customerMissedQuotaPrestigePenalty = -0.2f;

        [Tooltip("Müşteriye başarılı servis yapıldığında kazanılan prestige bonusu")]
        public float customerServedPrestigeBonus = 0.4f;

        [Tooltip("Müşteriye yanlış ürün gösterildiğinde uygulanan prestige cezası (negatif olmalı)")]
        public float wrongProductPrestigePenalty = -0.08f;

        [Tooltip("Kutu yere düştüğünde uygulanan prestige cezası (negatif olmalı)")]
        public float boxDropPrestigePenalty = -0.04f;

        [Tooltip("Tıra yanlış renk kutu teslim edildiğinde uygulanan prestige cezası (negatif olmalı). Para cezası (penaltyPerBox) ayrıca uygulanır.")]
        public float wrongDeliveryPrestigePenalty = -0.16f;

        // ─────────────────────────────────────────────────────────────
        //  ETKİNLİK (Event)
        // ─────────────────────────────────────────────────────────────

        [Header("=== ETKİNLİK AYARLARI ===")]

        [Tooltip("FESTIVAL DAY: gün başında verilen rastgele para bonusu alt sınırı (TL)")]
        public int festivalBonusMin = 100;

        [Tooltip("FESTIVAL DAY: gün başında verilen rastgele para bonusu üst sınırı (TL)")]
        public int festivalBonusMax = 300;

        // ─────────────────────────────────────────────────────────────
        //  YARDIMCI METODLAR
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Oyuncu sayısına göre temel kira miktarını döndürür.
        /// playerCount 1-4 arası olmalıdır. Dışarıdaki değerlerde en yakın uç değer kullanılır.
        /// </summary>
        public int GetBaseRent(int playerCount)
        {
            int index = Mathf.Clamp(playerCount - 1, 0, baseRentByPlayerCount.Length - 1);
            return baseRentByPlayerCount[index];
        }

        /// <summary>
        /// Oyuncu sayısına göre tır hangar bekleme süresini (saniye) döndürür.
        /// playerCount 1-4 arası; dışarıda en yakın uç değer. Dizi boş/null ise legacy
        /// skaler hangarStayDuration'a düşer (güvenli fallback).
        /// </summary>
        public float GetHangarStayDuration(int playerCount)
        {
            if (hangarStayDurationByPlayerCount == null || hangarStayDurationByPlayerCount.Length == 0)
                return hangarStayDuration;
            int index = Mathf.Clamp(playerCount - 1, 0, hangarStayDurationByPlayerCount.Length - 1);
            return hangarStayDurationByPlayerCount[index];
        }

        /// <summary>
        /// Oyuncu sayısına göre doğru kutu tesliminde kutu başına ödülü döndürür.
        /// playerCount 1-4 arası; dışarıda en yakın uç değer. Dizi boş/null ise legacy
        /// skaler rewardPerBox'a düşer (güvenli fallback).
        /// </summary>
        public int GetRewardPerBox(int playerCount)
        {
            if (rewardPerBoxByPlayerCount == null || rewardPerBoxByPlayerCount.Length == 0)
                return rewardPerBox;
            int index = Mathf.Clamp(playerCount - 1, 0, rewardPerBoxByPlayerCount.Length - 1);
            return rewardPerBoxByPlayerCount[index];
        }

        /// <summary>
        /// Kira dönemine göre hesaplanmış kira miktarını döndürür.
        /// </summary>
        public float CalculateRent(int playerCount, int rentCycle)
        {
            float baseRent = GetBaseRent(playerCount);
            float scaledRent = baseRent * Mathf.Pow(rentGrowthMultiplier, rentCycle) * rentScaledMultiplier;
            return scaledRent;
        }

        /// <summary>
        /// Oyuncu sayısına göre tır kargo miktarı aralığını döndürür (min dahil, maxExclusive hariç —
        /// doğrudan Random.Range(int,int) semantiğiyle uyumlu). playerCount 1-4 arası; dışarıda en
        /// yakın uç değer. Dizi boş/null ise legacy sabit {2,6} aralığına düşer (güvenli fallback).
        /// </summary>
        public (int min, int maxExclusive) GetTruckCargoRange(int playerCount)
        {
            if (truckCargoMinByPlayerCount == null || truckCargoMinByPlayerCount.Length == 0 ||
                truckCargoMaxExclusiveByPlayerCount == null || truckCargoMaxExclusiveByPlayerCount.Length == 0)
            {
                return (2, 6);
            }

            int minIndex = Mathf.Clamp(playerCount - 1, 0, truckCargoMinByPlayerCount.Length - 1);
            int maxIndex = Mathf.Clamp(playerCount - 1, 0, truckCargoMaxExclusiveByPlayerCount.Length - 1);
            return (truckCargoMinByPlayerCount[minIndex], truckCargoMaxExclusiveByPlayerCount[maxIndex]);
        }

        /// <summary>
        /// Oyuncu sayısına göre telefonla müşteri çağrıldığında atlanan oyun-dakikasını döndürür.
        /// playerCount 1-4 arası; dışarıda en yakın uç değer. Dizi boş/null ise güvenli fallback
        /// olarak 115 (1P değeri) döner.
        /// </summary>
        public float GetTimeSkipAmountMinutes(int playerCount)
        {
            if (timeSkipAmountByPlayerCount == null || timeSkipAmountByPlayerCount.Length == 0)
                return 115f;
            int index = Mathf.Clamp(playerCount - 1, 0, timeSkipAmountByPlayerCount.Length - 1);
            return timeSkipAmountByPlayerCount[index];
        }

        /// <summary>
        /// Gün numarası ve oyuncu sayısına göre günlük müşteri kotasını döndürür (PlateUp
        /// gün-numarası eğrisi). day 1-16 arası, playerCount 1-4 arası; dışarıda en yakın uç
        /// değere clamp'lenir. İlgili dizi boş/null ise güvenli fallback olarak 4 döner.
        /// </summary>
        public int GetDailyCustomerCount(int day, int playerCount)
        {
            int[] curve = GetQuotaCurve(playerCount);
            if (curve == null || curve.Length == 0) return 4;
            int dayIndex = Mathf.Clamp(day - 1, 0, curve.Length - 1);
            return curve[dayIndex];
        }

        private int[] GetQuotaCurve(int playerCount)
        {
            return Mathf.Clamp(playerCount, 1, 4) switch
            {
                1 => dailyCustomerCountP1,
                2 => dailyCustomerCountP2,
                3 => dailyCustomerCountP3,
                _ => dailyCustomerCountP4,
            };
        }

        /// <summary>
        /// Oyuncu sayısına göre müşteriler arası temel varış aralığını (saniye) döndürür.
        /// playerCount 1-4 arası; dışarıda en yakın uç değer. Dizi boş/null ise güvenli
        /// fallback 22s (2P referans değeri) döner.
        /// </summary>
        public float GetCustomerArrivalIntervalSeconds(int playerCount)
        {
            if (customerArrivalIntervalByPlayerCount == null || customerArrivalIntervalByPlayerCount.Length == 0)
                return 22f;
            int index = Mathf.Clamp(playerCount - 1, 0, customerArrivalIntervalByPlayerCount.Length - 1);
            return customerArrivalIntervalByPlayerCount[index];
        }

    }
}
