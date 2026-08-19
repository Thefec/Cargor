using UnityEngine;

namespace NewCss
{
    /// <summary>
    /// Perk etkilerinin dokunduğu sistemlere referans taşıyıcı.
    /// UpgradePanel.BuildPerkContext() tarafından kurulur.
    ///
    /// perk-revival (2026-08-19): Truck/PlayerMovement alanları BİLİNÇLİ OLARAK burada YOK.
    /// Eskiden UpgradePanel'in sahne SerializeField'ları (aslında Truck/Character PREFAB
    /// referansları — bkz. plans/perk-revival.md §1.1) buradan yazılıyordu; canlı sahne
    /// instance'larına hiç ulaşmıyordu (6 ölü perk). Artık tır/oyuncu perkleri ayrı giriş
    /// noktalarından (ApplyToTruck / ApplyToPlayer) canlı instance parametresiyle çağrılıyor —
    /// bkz. UpgradePanel.ApplyPerkToAllLiveTrucks / ApplyLivePerksToTruck / ApplyPerkToOwnedPlayer
    /// / ApplyLivePerksToPlayer.
    /// </summary>
    public class PerkContext
    {
        public CustomerManager CustomerManager;
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

            // perk-revival: prestige_broker/fast_hangar/gambler_case (tamamen Truck) ve
            // agile_crew/energetic_crew (tamamen PlayerMovement) buradan ÇIKARILDI — canlı
            // instance'a ApplyToTruck/ApplyToPlayer üzerinden uygulanıyor (çağıran taraf
            // UpgradePanel.ApplyUpgradeEffect: WritesToTruck/WritesToPlayer/NeedsGenericApply).
            // all_in BURADA KALDI ama artık yalnız Economy (gracePaymentPercent) parçasını yapar —
            // ödül parçası ApplyToTruck'a taşındı (bkz. ApplyAllIn ve ApplyAllInRewardToTruck).
            switch (effectId)
            {
                case "cheap_rent":        ApplyCheapRent(level, ctx); break;
                case "prestige_master":   ApplyPrestigeMaster(level, ctx); break;
                case "patient_customers": ApplyPatientCustomers(level, ctx); break;
                case "long_queue":        ApplyLongQueue(level, ctx); break;
                case "leveraged_rent":    ApplyLeveragedRent(level, ctx); break;
                case "high_volatility":   ApplyHighVolatility(level, ctx); break;
                case "all_in":            ApplyAllIn(level, ctx); break;
                case "emergency_brake":   ApplyEmergencyBrake(level, ctx); break;
                case "phone_line":        ApplyPhoneLine(level, ctx); break;
                case "overtime":          ApplyOvertime(level, ctx); break;
                case "bulk_buy":          ApplyBulkBuy(level, ctx); break;
                case "prestige_broker":
                case "fast_hangar":
                case "gambler_case":
                case "agile_crew":
                case "energetic_crew":
                    // Tamamen ApplyToTruck/ApplyToPlayer'a taşındı — burada no-op (uyarı basma).
                    break;
                default:
                    Debug.LogWarning($"[PerkEffect] Bilinmeyen effectId: '{effectId}'");
                    break;
            }
        }

        /// <summary>
        /// Tır perklerini (prestige_broker/fast_hangar/gambler_case/all_in-ödül) CANLI bir tır
        /// instance'ına uygular. Çağıran: UpgradePanel.ApplyPerkToAllLiveTrucks (satın alma anı,
        /// sahadaki tüm tırlar) ve UpgradePanel.ApplyLivePerksToTruck (Truck.OnNetworkSpawn — SO
        /// okumasından SONRA, event çarpanından ÖNCE; bkz. plans/perk-revival.md §2.1).
        /// Idempotent: level'dan mutlak değer hesaplar.
        /// </summary>
        public static void ApplyToTruck(string effectId, int level, Truck truck, PerkContext ctx)
        {
            if (truck == null || ctx == null) return;

            switch (effectId)
            {
                case "prestige_broker": ApplyPrestigeBrokerToTruck(level, truck); break;
                case "fast_hangar":     ApplyFastHangarToTruck(level, truck, ctx); break;
                case "gambler_case":    ApplyGamblerCaseToTruck(level, truck, ctx); break;
                case "all_in":          ApplyAllInRewardToTruck(level, truck, ctx); break;
            }
        }

        /// <summary>
        /// Oyuncu perklerini (agile_crew/energetic_crew) CANLI bir PlayerMovement instance'ına
        /// uygular. Çağıran: UpgradePanel.ApplyPerkToOwnedPlayer (satın alma anı, bu peer'in kendi
        /// owned player'ı) ve UpgradePanel.ApplyLivePerksToPlayer (PlayerMovement.OnNetworkSpawn,
        /// late-join). Idempotent.
        /// </summary>
        public static void ApplyToPlayer(string effectId, int level, PlayerMovement player)
        {
            if (player == null) return;

            switch (effectId)
            {
                case "agile_crew":     ApplyAgileCrewToPlayer(level, player); break;
                case "energetic_crew": ApplyEnergeticCrewToPlayer(level, player); break;
            }
        }

        /// <summary>effectId canlı Truck instance'ına mı yazıyor? UpgradePanel dispatch için.</summary>
        public static bool WritesToTruck(string effectId)
        {
            switch (effectId)
            {
                case "prestige_broker":
                case "fast_hangar":
                case "gambler_case":
                case "all_in":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>effectId canlı PlayerMovement instance'ına mı yazıyor? UpgradePanel dispatch için.</summary>
        public static bool WritesToPlayer(string effectId)
        {
            switch (effectId)
            {
                case "agile_crew":
                case "energetic_crew":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// effectId, genel PerkContext (Economy/CustomerManager/DayCycle/Panel) üzerinden Apply()
        /// çağrısına ihtiyaç duyuyor mu? prestige_broker/fast_hangar/gambler_case/agile_crew/
        /// energetic_crew TAMAMEN Truck/Player'a taşındığı için burada false — Apply() bu id'ler
        /// için artık hiçbir şey yapmıyor, gereksiz çağrıyı atlamak için (uyarı basmaz ama boşuna
        /// switch girer). all_in dahil DİĞER HERKES true (all_in'in Economy parçası hâlâ Apply()'da).
        /// </summary>
        public static bool NeedsGenericApply(string effectId)
        {
            switch (effectId)
            {
                case "prestige_broker":
                case "fast_hangar":
                case "gambler_case":
                case "agile_crew":
                case "energetic_crew":
                    return false;
                default:
                    return true;
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

        // Prestij Simsarı: Truck.bonusPerTier 5 → 5.5 → 6 (her seviye +0.5). perk-revival: artık
        // ApplyToTruck üzerinden canlı tıra yazılıyor (bkz. ApplyToTruck switch'i).
        private static void ApplyPrestigeBrokerToTruck(int level, Truck truck)
        {
            truck.bonusPerTier = 5f + 0.5f * level;
        }

        // Prestij Ustası: customerServedPrestigeBonus 0.4 → 0.52 → 0.64 (her seviye +0.12). FAZ4: taban ×2
        // olunca perkin göreli gücü düşmesin diye etki de ×2 (bkz. plans/economy-rebuild-2026-07-30-faz4-final.md §B.3).
        private static void ApplyPrestigeMaster(int level, PerkContext ctx)
        {
            if (ctx.Economy == null) return;
            ctx.Economy.customerServedPrestigeBonus = 0.4f + 0.12f * level;
        }

        // Hızlı Hangar (relic): hangarStayDuration taban(Economy P-bazlı GetHangarStayDuration) × 1.30.
        // Taban artık oyuncu sayısına göre (1P=90..4P=30); perk P-uygun tabana uygulanır ki
        // sapma P'ye göre sabit %30 kalsın (eski flat-30 taban P-bazlı dizide yanlış olurdu).
        private static void ApplyFastHangarToTruck(int level, Truck truck, PerkContext ctx)
        {
            if (ctx.Economy == null || level <= 0) return;
            int pc = DifficultyManager.Instance != null ? DifficultyManager.Instance.PlayerCount : 1;
            truck.hangarStayDuration = ctx.Economy.GetHangarStayDuration(pc) * 1.30f;
        }

        // Enerjik Ekip (relic): staminaRegenRate 1 → 2.5 (mevcut mekaniğe bağlanış, ekonomik değer
        // değil). perk-revival: artık ApplyToPlayer üzerinden owned player'a yazılıyor.
        private static void ApplyEnergeticCrewToPlayer(int level, PlayerMovement player)
        {
            if (level <= 0) return;
            player.staminaRegenRate = 1f + 1.5f;
        }

        // Çevik Ekip (relic): moveSpeed 5 → 5.75 (+%15, mevcut mekaniğe bağlanış)
        private static void ApplyAgileCrewToPlayer(int level, PlayerMovement player)
        {
            if (level <= 0) return;
            player.moveSpeed = 5f * 1.15f;
        }

        // Sabırlı Müşteriler (relic, DOKUNUŞ-5, FAZ4 §B.7 tercih): sabır bağlayıcı kısıt değil,
        // servis döngüsü süresini kısaltmak prestij darboğazına doğrudan dokunuyor. Eski
        // patienceMultiplier (+%25 bekleme) yerine CustomerAI.interactionTime × 0.6 (2sn → 1.2sn),
        // CustomerManager.interactionTimeMultiplier üzerinden (bkz.
        // plans/economy-rebuild-2026-07-30-faz4-final.md §B.7).
        private static void ApplyPatientCustomers(int level, PerkContext ctx)
        {
            if (ctx.CustomerManager == null || level <= 0) return;
            ctx.CustomerManager.interactionTimeMultiplier = 0.6f;
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
        // perk-revival: artık ApplyToTruck üzerinden canlı tıra yazılıyor. gambler_case ve all_in
        // draft'ta EXCLUSIVE_EFFECT_GROUPS'ta (UpgradePanel.cs:216) — ikisi asla birlikte aktif
        // olmadığından rewardPerBox üzerinde çakışma yok.
        private static void ApplyGamblerCaseToTruck(int level, Truck truck, PerkContext ctx)
        {
            if (ctx.Economy == null || level <= 0) return;
            truck.rewardPerBox = Mathf.RoundToInt(ctx.Economy.rewardPerBox * 1.30f);
            truck.penaltyPerBox = Mathf.RoundToInt(ctx.Economy.penaltyPerBox * 1.55f);
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
        // (GameEconomySettings.CalculateRent). FAZ4 §B.7 upgrade turu — tercih edilen etki
        // değişikliği uygulandı: bedel artık prestij cezası ×2 DEĞİL, gracePaymentPercent=0
        // (grace period iptali, all_in ile aynı mekanik — ikisi EXCLUSIVE_EFFECT_GROUPS'ta
        // aynı grupta). Kira indirimi 0.8 → 0.75 (kira %25 düşer). Eski
        // customerLostPrestigePenalty = -0.8f satırı (FAZ4 §B.3 fallback) bu satırla kalkar.
        private static void ApplyLeveragedRent(int level, PerkContext ctx)
        {
            if (ctx.Economy == null || level <= 0) return;
            ctx.Economy.rentScaledMultiplier = 0.75f;
            ctx.Economy.gracePaymentPercent = 0f;
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
        // perk-revival: SPLIT — Economy (kalıcı SO, tek instance, güvenle burada kalır) parçası
        // burada; Truck ödül parçası ApplyAllInRewardToTruck'a taşındı (canlı instance gerekir).
        private static void ApplyAllIn(int level, PerkContext ctx)
        {
            if (ctx.Economy == null || level <= 0) return;
            ctx.Economy.gracePaymentPercent = 0f;
        }

        // Kelle Koltukta — ödül parçası: rewardPerBox +%25, CANLI tıra (ApplyToTruck üzerinden).
        private static void ApplyAllInRewardToTruck(int level, Truck truck, PerkContext ctx)
        {
            if (ctx.Economy == null || level <= 0) return;
            truck.rewardPerBox = Mathf.RoundToInt(ctx.Economy.rewardPerBox * 1.25f);
        }

        // DOKUNUŞ-3: Acil Fren (relic). İflası 1 kez önleyen tek-kullanımlık bayrak;
        // DayCycleManager.TryProcessMoneyCheck() rent-fail dalında tüketilir.
        private static void ApplyEmergencyBrake(int level, PerkContext ctx)
        {
            if (ctx.DayCycle == null || level <= 0) return;
            ctx.DayCycle.insuranceAvailable = true;
        }

        // Mesai Saati (relic, NOT bölümü — soyut büyüklük, economist onayı gerekmez):
        // günün gerçek süresini hafifçe uzatır. Taban × 1.125 (~+%12.5, 200 → 225sn) gameplay
        // tercihi; sapma sayılmaz çünkü fiyat (300 TL, T1) sabit ve büyüklük ekonomik bir değer
        // değil. Çarpımsal + idempotent: DayCycleManager.SetOvertimeMultiplier level 0'da 1f'e
        // döner, bu yüzden HandleUpgradeLevelsChanged tekrar tetiklense bile süre sürüklenmez.
        private static void ApplyOvertime(int level, PerkContext ctx)
        {
            if (ctx.DayCycle == null) return;
            ctx.DayCycle.SetOvertimeMultiplier(level > 0 ? 1.125f : 1f);
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
