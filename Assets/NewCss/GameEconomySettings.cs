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
        public float rentGrowthMultiplier = 1.35f;

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
        //  TELEFON  (PhoneCallManager - REAKTİF V3)
        // ─────────────────────────────────────────────────────────────

        [Header("=== TELEFON AYARLARI ===")]

        [Tooltip("Mesai saatleri içinde her oyun-saati değiştiğinde telefonun çalma olasılığı (0-1). LEGACY skaler: yalnız phoneRingChanceByPlayerCount boş/null ise fallback olarak kullanılır.")]
        public float phoneRingChancePerHour = 0.20f;

        [Tooltip("Oyuncu sayısına göre saatlik çalma olasılığı (1P,2P,3P,4P). index = oyuncuSayısı-1. FAZ4 §B.6: telefon gelirinin toplam gelirdeki payını 1P %9.2 → 4P %4.9'a taşır; solo yardımı bilinçli korunur.")]
        public float[] phoneRingChanceByPlayerCount = { 0.20f, 0.25f, 0.30f, 0.35f };

        [Tooltip("CUSTOMER SUPPORT etkinliği günü çalma olasılığına uygulanan çarpan")]
        public float phoneRingEventMultiplier = 2.0f;

        [Tooltip("Telefon Hattı perki aktifken saatlik çalma olasılığına eklenen additive bonus")]
        public float phoneRingPerkBonus = 0f;

        [Tooltip("Telefon açıldığında verilen para ödülü (TL)")]
        public int callMoneyReward = 20;

        [Tooltip("Telefon açıldığında verilen prestij ödülü")]
        public float callPrestigeReward = 0.4f;

        // ─────────────────────────────────────────────────────────────
        //  PRESTİJ CEZA / ÖDÜL  (GameStateManager, CustomerAI, BoxFallPenalty)
        // ─────────────────────────────────────────────────────────────

        [Header("=== PRESTİJ AYARLARI ===")]

        [Tooltip("Müşteri kaçtığında (bekleme süresi dolunca) uygulanan prestige cezası (negatif olmalı)")]
        public float customerLostPrestigePenalty = -0.4f;

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
        /// Oyuncu sayısına göre saatlik telefon çalma olasılığını döndürür. playerCount 1-4 arası;
        /// dışarıda en yakın uç değer. Dizi boş/null ise legacy skaler phoneRingChancePerHour'a
        /// düşer (güvenli fallback).
        /// </summary>
        public float GetPhoneRingChancePerHour(int playerCount)
        {
            if (phoneRingChanceByPlayerCount == null || phoneRingChanceByPlayerCount.Length == 0)
                return phoneRingChancePerHour;
            int index = Mathf.Clamp(playerCount - 1, 0, phoneRingChanceByPlayerCount.Length - 1);
            return phoneRingChanceByPlayerCount[index];
        }

    }
}
