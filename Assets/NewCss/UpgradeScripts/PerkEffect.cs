using UnityEngine;

namespace NewCss
{
    /// <summary>
    /// Perk etkilerinin dokunduğu sistemlere referans taşıyıcı.
    /// UpgradePanel.BuildPerkContext() tarafından kurulur.
    /// </summary>
    public class PerkContext
    {
        public Truck Truck;
        public CustomerManager CustomerManager;
        public PlayerMovement PlayerMovement;
        public GameEconomySettings Economy;
        public DayCycleManager DayCycle;
        public UpgradePanel Panel;
    }

    /// <summary>
    /// effectId → kaldıraç uygulaması. Değerler UPGRADE_PRICING_REPORT.md v3.2'den (verbatim),
    /// bkz. plans/task7-prep.md.
    ///
    /// Not: Bu dosya bilinçli olarak Assets/NewCss/UpgradeScripts/ altında (Assembly-CSharp), Roguelite/
    /// klasöründe DEĞİL — NewCss.Roguelite.asmdef (DraftPool/RerollCurve/PerkTier) izole/saf-mantık
    /// bir assembly ve Assembly-CSharp'a (Truck/CustomerManager/PlayerMovement/GameEconomySettings/
    /// DayCycleManager/UpgradePanel'in yaşadığı yer) referans veremez (derleme sırası: Assembly-CSharp
    /// asmdef'lerden SONRA derlenir). PerkEffect bu tiplere doğrudan bağımlı olduğundan Assembly-CSharp
    /// içinde kalmalı; sadece PerkTier enum'ı (NewCss.Roguelite) iki tarafta da kullanılabiliyor.
    ///
    /// Önemli: UpgradePanel.HandleUpgradeLevelsChanged (NetworkList.OnListChanged) tüm client'larda
    /// tetiklenir — bu yalnızca server'a özgü bir çağrı değildir. Bu yüzden her Apply* metodu
    /// IDEMPOTENT olmalıdır: level'dan MUTLAK bir hedef değer hesaplar, mevcut alan değerine
    /// += / *= yapmaz. Ekonomik state'i fiilen değiştiren (RNG, iflas kurtarma, kira) tek-kullanımlık
    /// davranışlar server-only noktalarda uygulanır (Truck.ApplyRewardVolatility, DayCycleManager
    /// rent akışı) — PerkEffect burada sadece "bayrağı/çarpanı" kurar.
    ///
    /// level: 0 = etkisiz (bu yola normalde girilmez, HandleUpgradeLevelsChanged sadece seviye
    /// artışında tetiklenir), 1..maxLevel = seviye. Relic (tek-seferlik) perklerde maxLevel=1.
    /// </summary>
    public static class PerkEffect
    {
        public static void Apply(string effectId, int level, PerkContext ctx)
        {
            if (ctx == null) return;

            switch (effectId)
            {
                case "cheap_rent":        ApplyCheapRent(level, ctx); break;
                case "prestige_broker":   ApplyPrestigeBroker(level, ctx); break;
                case "prestige_master":   ApplyPrestigeMaster(level, ctx); break;
                case "fast_hangar":       ApplyFastHangar(level, ctx); break;
                case "energetic_crew":    ApplyEnergeticCrew(level, ctx); break;
                case "agile_crew":        ApplyAgileCrew(level, ctx); break;
                case "patient_customers": ApplyPatientCustomers(level, ctx); break;
                case "long_queue":        ApplyLongQueue(level, ctx); break;
                case "gambler_case":      ApplyGamblerCase(level, ctx); break;
                case "leveraged_rent":    ApplyLeveragedRent(level, ctx); break;
                case "high_volatility":   ApplyHighVolatility(level, ctx); break;
                case "all_in":            ApplyAllIn(level, ctx); break;
                case "emergency_brake":   ApplyEmergencyBrake(level, ctx); break;
                case "phone_line":        ApplyPhoneLine(level, ctx); break;
                case "overtime":          ApplyOvertime(level, ctx); break;
                case "bulk_buy":          ApplyBulkBuy(level, ctx); break;
                default:
                    Debug.LogWarning($"[PerkEffect] Bilinmeyen effectId: '{effectId}'");
                    break;
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  Basit kaldıraç (level-lineer / relic) — task7-prep.md tablo 1
        // ─────────────────────────────────────────────────────────────

        // Ucuz Kira: rentGrowthMultiplier 1.15 → 1.12 → 1.09 → 1.06 (her seviye -0.03)
        private static void ApplyCheapRent(int level, PerkContext ctx)
        {
            if (ctx.Economy == null) return;
            ctx.Economy.rentGrowthMultiplier = 1.15f - 0.03f * level;
        }

        // Prestij Simsarı: Truck.bonusPerTier 5 → 5.5 → 6 (her seviye +0.5)
        private static void ApplyPrestigeBroker(int level, PerkContext ctx)
        {
            if (ctx.Truck == null) return;
            ctx.Truck.bonusPerTier = 5f + 0.5f * level;
        }

        // Prestij Ustası: customerServedPrestigeBonus 0.2 → 0.26 → 0.32 (her seviye +0.06). 100-skala rescale (×0.4).
        private static void ApplyPrestigeMaster(int level, PerkContext ctx)
        {
            if (ctx.Economy == null) return;
            ctx.Economy.customerServedPrestigeBonus = 0.2f + 0.06f * level;
        }

        // Hızlı Hangar (relic): hangarStayDuration taban(Economy.hangarStayDuration) × 1.30.
        // Not: Eskiden 120f hardcode tabandan hesaplanıyordu (canlı taban Economy.hangarStayDuration=30
        // ile uyumsuz, bug); diğer Apply* metodları gibi Economy'den okunacak şekilde düzeltildi.
        private static void ApplyFastHangar(int level, PerkContext ctx)
        {
            if (ctx.Truck == null || ctx.Economy == null || level <= 0) return;
            ctx.Truck.hangarStayDuration = ctx.Economy.hangarStayDuration * 1.30f;
        }

        // Enerjik Ekip (relic): staminaRegenRate 1 → 2.5 (mevcut mekaniğe bağlanış, ekonomik değer değil)
        private static void ApplyEnergeticCrew(int level, PerkContext ctx)
        {
            if (ctx.PlayerMovement == null || level <= 0) return;
            ctx.PlayerMovement.staminaRegenRate = 1f + 1.5f;
        }

        // Çevik Ekip (relic): moveSpeed 5 → 5.75 (+%15, mevcut mekaniğe bağlanış)
        private static void ApplyAgileCrew(int level, PerkContext ctx)
        {
            if (ctx.PlayerMovement == null || level <= 0) return;
            ctx.PlayerMovement.moveSpeed = 5f * 1.15f;
        }

        // Sabırlı Müşteriler (relic, DOKUNUŞ-5): CustomerManager.patienceMultiplier ile
        // CustomerAI.InitializeServerState()'te min/maxWaitTime ölçeklenir (+%25).
        private static void ApplyPatientCustomers(int level, PerkContext ctx)
        {
            if (ctx.CustomerManager == null || level <= 0) return;
            ctx.CustomerManager.patienceMultiplier = 1.25f;
        }

        // Uzun Kuyruk (relic): maxQueueSize 3(=DEFAULT_QUEUE_SIZE) → 5 (+2)
        private static void ApplyLongQueue(int level, PerkContext ctx)
        {
            if (ctx.CustomerManager == null || level <= 0) return;
            ctx.CustomerManager.maxQueueSize = CustomerManager.DEFAULT_QUEUE_SIZE + 2;
        }

        // Kumarbaz Kasası (relic): ödül +%30, ceza +%55. Truck.rewardPerBox/penaltyPerBox gerçek
        // tüketilen değerlerdir (Economy.rewardPerBox sadece OnNetworkSpawn'da bir kez kopyalanır);
        // idempotent kalması için her zaman Economy'nin (sabit) baz değerinden yeniden hesaplanır.
        private static void ApplyGamblerCase(int level, PerkContext ctx)
        {
            if (ctx.Economy == null || ctx.Truck == null || level <= 0) return;
            ctx.Truck.rewardPerBox = Mathf.RoundToInt(ctx.Economy.rewardPerBox * 1.30f);
            ctx.Truck.penaltyPerBox = Mathf.RoundToInt(ctx.Economy.penaltyPerBox * 1.55f);
        }

        // Telefon Hattı (relic): PhoneCallManager reaktif V3'e geçti (kontenjan kavramı yok,
        // saatlik çalma OLASILIĞI var). Eski hedef alan (maxCallsPerHour, kaldırıldı) yerine
        // eski perk gücü (+%50, 2→3) additive bonus olarak phoneRingPerkBonus'a taşındı.
        // economist onaylı additive model (izole eşdeğer 0.30+0.15=0.45).
        private static void ApplyPhoneLine(int level, PerkContext ctx)
        {
            if (ctx.Economy == null || level <= 0) return;
            ctx.Economy.phoneRingPerkBonus = 0.15f;
        }

        // ─────────────────────────────────────────────────────────────
        //  Gerçek kod dokunuşu gerektirenler — task7-prep.md tablo 2
        // ─────────────────────────────────────────────────────────────

        // DOKUNUŞ-1: Kaldıraçlı Kira (relic). rentScaledMultiplier scaledRent'e uygulanır
        // (GameEconomySettings.CalculateRent). Bedel: prestij
        // cezası ×2 (taban -0.6 → -1.2, mutlak değer — idempotent; 100-skala rescale).
        private static void ApplyLeveragedRent(int level, PerkContext ctx)
        {
            if (ctx.Economy == null || level <= 0) return;
            ctx.Economy.rentScaledMultiplier = 0.8f;
            ctx.Economy.customerLostPrestigePenalty = -1.2f;
        }

        // DOKUNUŞ-2: Yüksek Volatilite (relic). Per-delivery ±%35 RNG, ort. +%15 — Truck.
        // ApplyRewardVolatility() içinde (server-only) okunur. EV her zaman pozitif (rapor §4.2).
        private static void ApplyHighVolatility(int level, PerkContext ctx)
        {
            if (ctx.Economy == null || level <= 0) return;
            ctx.Economy.rewardVolatility = 0.35f;
            ctx.Economy.rewardVolatilityMean = 1.15f;
        }

        // Kelle Koltukta (relic): gelir +%25, grace period iptal (gracePaymentPercent=0).
        private static void ApplyAllIn(int level, PerkContext ctx)
        {
            if (ctx.Economy == null || ctx.Truck == null || level <= 0) return;
            ctx.Truck.rewardPerBox = Mathf.RoundToInt(ctx.Economy.rewardPerBox * 1.25f);
            ctx.Economy.gracePaymentPercent = 0f;
        }

        // DOKUNUŞ-3: Acil Fren (relic). İflası 1 kez önleyen tek-kullanımlık bayrak;
        // DayCycleManager.TryProcessMoneyCheck() rent-fail dalında tüketilir.
        private static void ApplyEmergencyBrake(int level, PerkContext ctx)
        {
            if (ctx.DayCycle == null || level <= 0) return;
            ctx.DayCycle.insuranceAvailable = true;
        }

        // Mesai Saati (relic, NOT bölümü — soyut büyüklük, economist onayı gerekmez):
        // günün gerçek süresini hafifçe uzatır. +20sn (~%12.5) gameplay tercihi; sapma sayılmaz
        // çünkü fiyat (200 TL, T1) sabit ve büyüklük ekonomik bir değer değil.
        private static void ApplyOvertime(int level, PerkContext ctx)
        {
            if (ctx.DayCycle == null || level <= 0) return;
            ctx.DayCycle.realDurationInSeconds = 160f + 20f;
        }

        // DOKUNUŞ-4: Toplu Alım (relic). Bir sonraki draft teklifindeki rastgele 1 karta -%50
        // uygular (UpgradePanel._pendingBulkBuyDiscount → GenerateDailyOfferServer'da tüketilir).
        private static void ApplyBulkBuy(int level, PerkContext ctx)
        {
            if (ctx.Panel == null || level <= 0) return;
            ctx.Panel.MarkNextDraftDiscount();
        }
    }
}
