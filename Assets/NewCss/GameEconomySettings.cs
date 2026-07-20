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
        public int[] baseRentByPlayerCount = { 500, 900, 1200, 1500 };

        [Tooltip("Her kira döneminde kira artış çarpanı (örn: 1.3 = %30 artış)")]
        public float rentGrowthMultiplier = 1.15f;

        [Tooltip("Kaç günde bir kira alınır")]
        public int rentIntervalDays = 4;

        [Tooltip("Grace period'da oyuncudan alınan para yüzdesi (0-1). Kalan para oyuncuda kalır.")]
        public float gracePaymentPercent = 0.8f;

        [Tooltip("Kaldıraçlı Kira perki: scaledRent'e uygulanan çarpan. Perk yoksa 1f.")]
        public float rentScaledMultiplier = 1f;

        // ─────────────────────────────────────────────────────────────
        //  TIR / TESLİMAT  (Truck)
        // ─────────────────────────────────────────────────────────────

        [Header("=== TIR / TESLİMAT AYARLARI ===")]

        [Tooltip("Doğru kutu tesliminde kutu başına ödül (TL)")]
        public int rewardPerBox = 50;

        [Tooltip("Yanlış renk kutu tesliminde kutu başına ceza (TL)")]
        public int penaltyPerBox = 40;

        [Tooltip("Tırın hangarda bekleme süresi (saniye). Süre dolunca boş da olsa kalkar.")]
        public float hangarStayDuration = 30f;

        [Tooltip("Her prestige bonusu için gereken prestige miktarı")]
        public float prestigePerBonus = 4f;

        [Tooltip("Her prestige katmanında kutu başına eklenen bonus (TL)")]
        public float bonusPerTier = 5f;

        [Tooltip("Yüksek Volatilite perki: kutu başına ödül dağılımının +-yüzdesi (0 = kapalı).")]
        public float rewardVolatility = 0f;

        [Tooltip("Yüksek Volatilite perki: ortalama ödül çarpanı (RNG merkezi, EV her zaman pozitif olacak şekilde).")]
        public float rewardVolatilityMean = 1f;

        // ─────────────────────────────────────────────────────────────
        //  KUTU DÜŞME / ÇARPMA CEZASI  (BoxFallPenalty)
        // ─────────────────────────────────────────────────────────────

        [Header("=== KUTU DÜŞME AYARLARI ===")]

        [Tooltip("Kutu/ürün sert çarpma ile düştüğünde uygulanan para cezası (TL). Yüzeyden bağımsız (yer/duvar/raf aynı). Boş kutu ücretsiz olduğundan ceza yalnızca dikkatsizliği caydırır.")]
        public int boxDropMoneyPenalty = 5;

        // ─────────────────────────────────────────────────────────────
        //  TELEFON  (PhoneCallManager - REAKTİF V3)
        // ─────────────────────────────────────────────────────────────

        [Header("=== TELEFON AYARLARI ===")]

        [Tooltip("Mesai saatleri içinde her oyun-saati değiştiğinde telefonun çalma olasılığı (0-1)")]
        public float phoneRingChancePerHour = 0.30f;

        [Tooltip("CUSTOMER SUPPORT etkinliği günü çalma olasılığına uygulanan çarpan")]
        public float phoneRingEventMultiplier = 1.5f;

        [Tooltip("Telefon Hattı perki aktifken saatlik çalma olasılığına eklenen additive bonus")]
        public float phoneRingPerkBonus = 0f;

        [Tooltip("Telefon açıldığında verilen para ödülü (TL)")]
        public int callMoneyReward = 20;

        [Tooltip("Telefon açıldığında verilen prestij ödülü")]
        public float callPrestigeReward = 0.2f;

        // ─────────────────────────────────────────────────────────────
        //  PRESTİJ CEZA / ÖDÜL  (GameStateManager, CustomerAI, BoxFallPenalty)
        // ─────────────────────────────────────────────────────────────

        [Header("=== PRESTİJ AYARLARI ===")]

        [Tooltip("Müşteri kaçtığında (bekleme süresi dolunca) uygulanan prestige cezası (negatif olmalı)")]
        public float customerLostPrestigePenalty = -0.6f;

        [Tooltip("Müşteriye başarılı servis yapıldığında kazanılan prestige bonusu")]
        public float customerServedPrestigeBonus = 0.2f;

        [Tooltip("Müşteriye yanlış ürün gösterildiğinde uygulanan prestige cezası (negatif olmalı)")]
        public float wrongProductPrestigePenalty = -0.04f;

        [Tooltip("Kutu yere düştüğünde uygulanan prestige cezası (negatif olmalı)")]
        public float boxDropPrestigePenalty = -0.02f;

        [Tooltip("Tıra yanlış renk kutu teslim edildiğinde uygulanan prestige cezası (negatif olmalı). Para cezası (penaltyPerBox) ayrıca uygulanır.")]
        public float wrongDeliveryPrestigePenalty = -0.08f;

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
        /// Kira dönemine göre hesaplanmış kira miktarını döndürür.
        /// </summary>
        public float CalculateRent(int playerCount, int rentCycle)
        {
            float baseRent = GetBaseRent(playerCount);
            float scaledRent = baseRent * Mathf.Pow(rentGrowthMultiplier, rentCycle) * rentScaledMultiplier;
            return scaledRent;
        }

    }
}
