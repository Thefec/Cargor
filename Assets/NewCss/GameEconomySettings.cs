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
        public float hangarStayDuration = 120f;

        [Tooltip("Her prestige bonusu için gereken prestige miktarı")]
        public float prestigePerBonus = 10f;

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
        public float customerLostPrestigePenalty = -1.5f;

        [Tooltip("Müşteriye başarılı servis yapıldığında kazanılan prestige bonusu")]
        public float customerServedPrestigeBonus = 0.5f;

        [Tooltip("Müşteriye yanlış ürün gösterildiğinde uygulanan prestige cezası (negatif olmalı)")]
        public float wrongProductPrestigePenalty = -0.1f;

        [Tooltip("Kutu yere düştüğünde uygulanan prestige cezası (negatif olmalı)")]
        public float boxDropPrestigePenalty = -0.05f;

        [Tooltip("Tıra yanlış renk kutu teslim edildiğinde uygulanan prestige cezası (negatif olmalı). Para cezası (penaltyPerBox) ayrıca uygulanır.")]
        public float wrongDeliveryPrestigePenalty = -0.2f;

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

        // ─────────────────────────────────────────────────────────────
        //  SANAL BOT SİMÜLASYONU (UNITY CONTEXT MENU)
        // ─────────────────────────────────────────────────────────────
#if UNITY_EDITOR
        [ContextMenu("Simülasyon: 1 Oyuncu (Normal Hız)")]
        public void Simulate1PlayerNormal()
        {
            RunSimulation(1, 2.0f, 0.5f, 500f);
        }

        [ContextMenu("Simülasyon: 2 Oyuncu (Normal Hız)")]
        public void Simulate2PlayersNormal()
        {
            RunSimulation(2, 2.0f, 0.5f, 500f);
        }

        [ContextMenu("Simülasyon: 4 Oyuncu (Normal Hız)")]
        public void Simulate4PlayersNormal()
        {
            RunSimulation(4, 2.0f, 0.5f, 500f);
        }

        [ContextMenu("Simülasyon: 1 Oyuncu (Yavaş/Kötü Hız)")]
        public void Simulate1PlayerSlow()
        {
            RunSimulation(1, 1.2f, 0.3f, 500f);
        }

        /// <summary>
        /// Fizik ve grafik olmadan, 15 günlük oyunu arka planda saniyeler içinde simüle eder.
        /// </summary>
        private void RunSimulation(int playerCount, float deliveriesPerMin, float upgradeSpendingRatio, float startingCash)
        {
            float cash = startingCash;
            float prestige = 5.0f;
            float totalUpgradeValue = 0.0f;
            int rentCycle = 0;
            bool graceUsed = false;
            int activeInteractables = 3; // Başlangıçta 2 raf, 1 masa
            int storeLevel = 1;

            Debug.Log($"<color=cyan><b>=== Cargor Ekonomi Simülasyonu Başladı ===</b></color>\n" +
                      $"Oyuncu Sayısı: {playerCount} | Oyuncu Başı Hız: {deliveriesPerMin} kutu/dk | Başlangıç Kasası: {startingCash} TL");

            for (int day = 1; day <= 15; day++)
            {
                // Gün süresi hesabı (DayCycleManager mantığı)
                float duration = day <= 3 ? 160f : 160f + (day - 3) * 10f;
                float durationMins = duration / 60f;

                // Beklenen müşteri hesabı
                int expectedCustomers = activeInteractables * 2 + storeLevel * 2;
                expectedCustomers = Mathf.Clamp(expectedCustomers, 1, 50);

                // Günlük kota (QuotaManager)
                int quota = Mathf.CeilToInt(expectedCustomers * 0.8f);
                quota = Mathf.Max(1, quota);

                // Oyuncuların yapabileceği maksimum teslimat
                float maxPossibleDeliveries = durationMins * deliveriesPerMin * playerCount;
                
                // Gerçekleşen teslimat (müşteri sayısı veya oyuncu hızı limiti)
                float deliveriesMade = Mathf.Min(expectedCustomers, maxPossibleDeliveries);

                // Bot hatası: Ortalama her 5 teslimattan 1'inde kutu kırılsın/düşürülsün (%20 hata oranı)
                float brokenBoxes = Mathf.Floor(deliveriesMade / 5f);
                float correctDeliveries = deliveriesMade - brokenBoxes;

                // Prestij tier hesabı (Truck.cs mantığı)
                int prestigeTiers = Mathf.FloorToInt(prestige / prestigePerBonus);
                int rewardPerBoxActual = Mathf.RoundToInt(rewardPerBox + (prestigeTiers * bonusPerTier));

                // Günlük gelir / ceza
                float revenue = correctDeliveries * rewardPerBoxActual;
                float penalty = brokenBoxes * penaltyPerBox;
                float netDayEarnings = revenue - penalty;

                float cashBeforeRent = cash + netDayEarnings;

                // Prestij güncelleme (CustomerAI ve BoxFallPenalty mantığı)
                prestige += correctDeliveries * customerServedPrestigeBonus;
                prestige += brokenBoxes * boxDropPrestigePenalty; // Negatif ceza
                prestige = Mathf.Clamp(prestige, 0f, 100f);

                float rentAmount = 0f;
                float rentPaid = 0f;
                float upgradesBought = 0f;
                bool isRentDay = day % rentIntervalDays == 0;

                if (isRentDay)
                {
                    rentAmount = CalculateRent(playerCount, rentCycle);

                    if (cashBeforeRent >= rentAmount)
                    {
                        rentPaid = rentAmount;
                        cash = cashBeforeRent - rentPaid;
                        rentCycle++;
                    }
                    else if (!graceUsed)
                    {
                        // Grace Period devreye girer
                        rentPaid = cashBeforeRent * gracePaymentPercent;
                        cash = cashBeforeRent - rentPaid;
                        graceUsed = true;
                        Debug.LogWarning($"<b>[GÜN {day}] KIRALIK KORUMA (Grace Period) AKTİF!</b> Kira: {rentAmount:F1} TL | Alınan para: {rentPaid:F1} TL | Kalan Kasa: {cash:F1} TL");
                        rentCycle++;
                    }
                    else
                    {
                        Debug.LogError($"<color=red><b>=== OYUN BİTTİ (İFLAS) ===</b></color>\n" +
                                       $"Gün: {day} | Kira: {rentAmount:F1} TL | Eldeki Para: {cashBeforeRent:F1} TL | Grace önceden kullanılmıştı.");
                        return;
                    }
                }
                else
                {
                    cash = cashBeforeRent;
                }

                // Prestij sıfırlanma kontrolü (GameStateManager mantığı)
                if (prestige <= 0f)
                {
                    Debug.LogError($"<color=red><b>=== OYUN BİTTİ (PRESTİJ SIFIRLANDI) ===</b></color>\n" +
                                   $"Gün: {day} | Prestij: {prestige:F2}");
                    return;
                }

                // Gün sonunda upgrade satın alma (rent günü değilse ve kasa > 200 ise)
                if (!isRentDay && cash > 200f)
                {
                    float surplus = cash - 200f;
                    upgradesBought = surplus * upgradeSpendingRatio;
                    cash -= upgradesBought;
                    totalUpgradeValue += upgradesBought;

                    // Biriken upgrade değerine göre dükkanı büyüt
                    activeInteractables = 3 + Mathf.FloorToInt(totalUpgradeValue / 300f);
                    storeLevel = 1 + Mathf.FloorToInt(totalUpgradeValue / 600f);
                }

                Debug.Log($"<b>[GÜN {day}]</b> Süre: {duration}s | Müşteri: {expectedCustomers} | Doğru: {correctDeliveries:F1} | Kırık: {brokenBoxes:F1} | " +
                          $"Gelir: +{netDayEarnings:F1} TL | Kira: {rentAmount:F1} TL | Harcanan Upgrade: {upgradesBought:F1} TL | Kasa: {cash:F1} TL | Prestij: {prestige:F1}");
            }

            Debug.Log($"<color=green><b>=== TEBRİKLER! 15 GÜNLÜK SİMÜLASYON BAŞARIYLA TAMAMLANDI ===</b></color>\n" +
                      $"Son Kasa: {cash:F1} TL | Toplam Upgrade: {totalUpgradeValue:F1} TL | Son Prestij: {prestige:F2}");
        }
#endif
    }
}
