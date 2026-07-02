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
        public float rentGrowthMultiplier = 1.3f;

        [Tooltip("Satın alınan upgrade değerinin yüzde kaçı kira vergisi olarak eklenir (0-1)")]
        public float wealthTaxRate = 0.1f;

        [Tooltip("Kaç günde bir kira alınır")]
        public int rentIntervalDays = 4;

        [Tooltip("Grace period'da oyuncudan alınan para yüzdesi (0-1). Kalan para oyuncuda kalır.")]
        public float gracePaymentPercent = 0.8f;

        // ─────────────────────────────────────────────────────────────
        //  TIR / TESLİMAT  (Truck)
        // ─────────────────────────────────────────────────────────────

        [Header("=== TIR / TESLİMAT AYARLARI ===")]

        [Tooltip("Doğru kutu tesliminde kutu başına ödül (TL)")]
        public int rewardPerBox = 50;

        [Tooltip("Yanlış renk kutu tesliminde kutu başına ceza (TL)")]
        public int penaltyPerBox = 60;

        [Tooltip("Tırın hangarda bekleme süresi (saniye). Süre dolunca boş da olsa kalkar.")]
        public float hangarStayDuration = 120f;

        [Tooltip("Her prestige bonusu için gereken prestige miktarı")]
        public float prestigePerBonus = 10f;

        [Tooltip("Her prestige katmanında kutu başına eklenen bonus (TL)")]
        public int bonusPerTier = 5;

        // ─────────────────────────────────────────────────────────────
        //  TELEFON  (PhoneCallManager)
        // ─────────────────────────────────────────────────────────────

        [Header("=== TELEFON AYARLARI ===")]

        [Tooltip("Başarılı telefon aramasında verilen para ödülü (TL)")]
        public int callReward = 10;

        [Tooltip("Başarılı aramada atlanacak oyun içi süre (dakika)")]
        public float timeSkipAmount = 20f;

        [Tooltip("Başarılı aramadan sonra bir sonraki aramaya kadar bekleme süresi (saniye)")]
        public float postCallCooldown = 30f;

        [Tooltip("Saatte yapılabilecek maksimum arama sayısı")]
        public int maxCallsPerHour = 2;

        // ─────────────────────────────────────────────────────────────
        //  PRESTİJ CEZA / ÖDÜL  (GameStateManager, CustomerAI, BoxFallPenalty)
        // ─────────────────────────────────────────────────────────────

        [Header("=== PRESTİJ AYARLARI ===")]

        [Tooltip("Müşteri kaçtığında (bekleme süresi dolunca) uygulanan prestige cezası (negatif olmalı)")]
        public float customerLostPrestigePenalty = -2f;

        [Tooltip("Müşteriye başarılı servis yapıldığında kazanılan prestige bonusu")]
        public float customerServedPrestigeBonus = 0.5f;

        [Tooltip("Müşteriye yanlış ürün gösterildiğinde uygulanan prestige cezası (negatif olmalı)")]
        public float wrongProductPrestigePenalty = -0.1f;

        [Tooltip("Kutu yere düştüğünde uygulanan prestige cezası (negatif olmalı)")]
        public float boxDropPrestigePenalty = -0.05f;

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
        /// Kira dönemine ve toplam upgrade değerine göre hesaplanmış kira miktarını döndürür.
        /// </summary>
        public float CalculateRent(int playerCount, int rentCycle, float totalUpgradeValue)
        {
            float baseRent = GetBaseRent(playerCount);
            float scaledRent = baseRent * Mathf.Pow(rentGrowthMultiplier, rentCycle);
            float wealthTax  = totalUpgradeValue * wealthTaxRate;
            return scaledRent + wealthTax;
        }
    }
}
