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
        /// effectId Truck.rewardPerBox'a mı yazıyor? QA-fix (event-perk çakışması, bkz.
        /// ApplyGamblerCaseToTruck yorumu): SADECE bu iki id'nin yazdığı alan
        /// (EventEffectManager.EventMultipliers.rewardPerBoxMultiplier) event tarafından da
        /// çarpılıyor. prestige_broker (bonusPerTier), fast_hangar (hangarStayDuration) ve
        /// gambler_case'in penaltyPerBox yarısı HİÇBİR event alanında yok (grep ile doğrulandı) —
        /// rebase'e ihtiyaçları yok. UpgradePanel.ApplyPerkToAllLiveTrucks bu predicate'i satın
        /// alma anında EventEffectManager.RebaseTruckRewardPerBox çağrısı gerekip gerekmediğine
        /// karar vermek için kullanır.
        /// </summary>
        public static bool AffectsEventTruckReward(string effectId)
        {
            return effectId == "gambler_case" || effectId == "all_in";
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

        // Ucuz Kira: rentGrowthMultiplier 1.20 → 1.17 → 1.14 → 1.11 (her seviye -0.03).
        // economist round10 U11 (2026-08-30): eski 1.15f sabiti bayat bir tabana (eski
        // rentGrowthMultiplier) göre kalibre edilmişti; canlı taban 1.20 olduğu için kod perki
        // niyet edilenden 2.55-2.61 kat güçlü uyguluyordu (kazanç 171-959 TL, niyet 67-371 TL).
        // Formül artık canlı tabandan başlıyor.
        private static void ApplyCheapRent(int level, PerkContext ctx)
        {
            if (ctx.Economy == null) return;
            ctx.Economy.rentGrowthMultiplier = 1.20f - 0.03f * level;
        }

        // Prestij Simsarı: Truck.bonusPerTier 5 → 5.5 → 6 (her seviye +0.5). perk-revival: artık
        // ApplyToTruck üzerinden canlı tıra yazılıyor (bkz. ApplyToTruck switch'i).
        private static void ApplyPrestigeBrokerToTruck(int level, Truck truck)
        {
            truck.bonusPerTier = 5f + 0.5f * level;
        }

        // Prestij Ustası: customerServedPrestigeBonus 0.4 → 0.75 → 1.10 (her seviye +0.35). FAZ4: taban ×2
        // olunca perkin göreli gücü düşmesin diye etki de ×2 (bkz. plans/economy-rebuild-2026-07-30-faz4-final.md §B.3).
        // Ö-B fix (2026-09-24, docs/economy/ob-kart-duzeltmeleri-2026-09-24.md §2): eski adım
        // 0.12f zayıftı — prestijin tek ekonomik karşılığı ödül kademesi (⌊prestij/8⌋×5 TL/kutu),
        // 2 seviye 16 güne kadar ancak +4..+20 prestij (0.5-2.5 kademe) getiriyor, fiyatı (370-937
        // TL) karşılamıyordu. Yeni adım 0.35f (sim'de en az bir P'de net pozitif hedefine uydu;
        // adım 0.4-0.6 denendi, 2P iyi'de Kumarbaz/Ek Hangar'ı geçiyordu — reddedildi).
        private static void ApplyPrestigeMaster(int level, PerkContext ctx)
        {
            if (ctx.Economy == null) return;
            ctx.Economy.customerServedPrestigeBonus = 0.4f + 0.35f * level;
        }

        // Hızlı Hangar (relic): hangarStayDuration taban(Economy P-bazlı GetHangarStayDuration) × 0.9.
        // Taban oyuncu sayısına göre (1P=120..4P=30); perk P-uygun tabana uygulanır ki sapma
        // P'ye göre sabit oranda kalsın.
        //
        // YÖN DEĞİŞİKLİĞİ (ekonomi denetimi 2026-09-18, docs/economy/05-oneriler.md Ö3): eski
        // çarpan 1.30f idi, yani bekleme süresini UZATIYORDU. Uzun bekleme tırın dolma şansını
        // artırıyor ama günlük tır devrini düşürüyor; ölçümde bu perk oyunun TEK negatif kartıydı
        // (2P orta: -5.3 TL/gün, kartı alan oyuncu para verip zarar ediyordu). Süreyi kısaltmak
        // kartı adına ve sahne açıklamasına ("hangar daha hızlı döner") da uygun hale getirir.
        //
        // Ö-B fix (2026-09-24, docs/economy/ob-kart-duzeltmeleri-2026-09-24.md §1/§2): sim ↔ oyun
        // kök-neden analizi gösterdi ki gelir tırın hangarda durduğu saniyeyle sınırlı — eski
        // 0.75× çarpanla kalış payı öyle kısalıyordu ki tır çoğu zaman yarı dolu kalkıyordu (3P
        // orta −255, 4P orta −671 TL saf efekt). Üç değişiklik BİRLİKTE: (1) çarpan 0.75→0.9
        // (kalış payını geri açar), (2) respawn gecikmesi sıfırlanır (TruckSpawner.
        // respawnDelayMultiplier), (3) çıkış animasyonu beklemesi kısalır (Truck.
        // perkExitDelayMultiplier) — ölü süreyi (respawn 3-5sn + exitDelay 2sn) azaltarak kalış
        // payının oranını yükseltir. Yalnız çarpanı düşürmek (respawn/exit'e dokunmadan) tek
        // başına yetmiyordu (sweep_fh_check.csv: 2P/3P orta hâlâ negatif) — exitDelay 0 şarttı.
        // qa fix (2026-09-24): idempotent — level<=0 artık erken ÇIKMIYOR, mutlak/ters değerlere
        // döner (ApplyOvertime'daki `level > 0 ? X : Y` deseniyle aynı). Bugünkü çağrı grafiğinde
        // bu fonksiyon level<=0 ile hiç tetiklenmiyor (ApplyLivePerksToTruck/ApplyPerkToAllLiveTrucks
        // ikisi de `level<=0` filtresinden geçirip çağırmıyor, relic'ler geri alınamıyor) — ama
        // sınıfın tepesindeki idempotent kural TÜM Apply* metodları için geçerli (perk-mutates-
        // persistent-assets hafıza sınıfı: bu varsayım ileride bozulursa perkExitDelayMultiplier/
        // respawnDelayMultiplier sessizce 0'da kalıp tır sonsuza dek "perkli" davranmasın).
        private static void ApplyFastHangarToTruck(int level, Truck truck, PerkContext ctx)
        {
            truck.perkExitDelayMultiplier = level > 0 ? 0f : 1f;

            if (TruckSpawner.Instance != null)
            {
                TruckSpawner.Instance.respawnDelayMultiplier = level > 0 ? 0f : 1f;
            }

            if (ctx.Economy != null && level > 0)
            {
                int pc = DifficultyManager.Instance != null ? DifficultyManager.Instance.PlayerCount : 1;
                truck.hangarStayDuration = ctx.Economy.GetHangarStayDuration(pc) * 0.9f;
            }
        }

        // Enerjik Ekip (relic): staminaRegenRate 1 → 2.5 (mevcut mekaniğe bağlanış, ekonomik değer
        // değil). perk-revival: artık ApplyToPlayer üzerinden owned player'a yazılıyor.
        //
        // QA-fix NOT (event-rebase burada BİLİNÇLİ OLARAK YOK): staminaRegenRate event'lerin
        // staminaRegenRateMultiplier'ıyla da çarpılıyor (EventEffectManager). Bu metod hem satın
        // alma anında (mevcut oyuncu, event ZATEN aktif olabilir → rebase GEREKİR) hem de
        // PlayerMovement.OnNetworkSpawn'dan (yeni/late-join oyuncu, event varsa
        // EventEffectManager kendi late-join yakalamasıyla ayrıca ilgilenir → rebase burada
        // yapılırsa YANLIŞ sırada ikinci kez çarpardı) çağrılıyor. Bu yüzden rebase çağrısı BURADA
        // değil, çağıranda: UpgradePanel.ApplyPerkToOwnedPlayer (yalnız satın alma anı).
        private static void ApplyEnergeticCrewToPlayer(int level, PlayerMovement player)
        {
            if (level <= 0) return;
            player.staminaRegenRate = 1f + 1.5f;
        }

        // Çevik Ekip (relic): moveSpeed 5 → 5.75 (+%15, mevcut mekaniğe bağlanış). Event-rebase
        // notu için bkz. ApplyEnergeticCrewToPlayer üstündeki QA-fix yorumu — aynı gerekçe.
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
        // draft'ta EXCLUSIVE_EFFECT_GROUPS'ta (UpgradePanel.cs:216) — bu perk-perk çakışmasını
        // önler, rewardPerBox üzerinde iki PERKİN çakışması diye bir durum yok.
        //
        // QA-fix NOT (event-rebase burada BİLİNÇLİ OLARAK YOK): asıl çakışma perk-perk değil
        // perk-EVENT arasında — rewardPerBox aynı zamanda EventEffectManager'ın
        // rewardPerBoxMultiplier'ıyla da çarpılıyor (penaltyPerBox hiçbir event alanında YOK, o
        // yarı risksiz). Bu metod HEM satın alma anında (mevcut tır, event aktifse rebase GEREKİR)
        // HEM Truck.OnNetworkSpawn'dan (yeni tır — hemen ardından zaten çalışan
        // EventEffectManager.ApplyEventEffectToNewObject bu YENİ tırı doğru şekilde bir kez
        // çarpıyor; burada da rebase edilirse ÇİFT çarpım olurdu) çağrılıyor. Rebase çağrısı bu
        // yüzden BURADA değil, çağıranda: UpgradePanel.ApplyPerkToAllLiveTrucks (yalnız satın alma
        // anı, sahadaki MEVCUT tırlar).
        private static void ApplyGamblerCaseToTruck(int level, Truck truck, PerkContext ctx)
        {
            if (ctx.Economy == null || level <= 0) return;
            // PlateUp reward-lever fix (2026-08-29, kontrol turu bulgusu): taban artık düz
            // rewardPerBox (hep 50) DEĞİL, GetRewardPerBox(playerCount) — aksi halde P3/P4'te
            // bu perk alınınca ödül 70/88 tabanından değil 50'den yeniden kurulur ve reward-lever
            // ile kapatılmaya çalışılan gelir açığı geri açılırdı. Desen: ApplyFastHangarToTruck.
            int pc = DifficultyManager.Instance != null ? DifficultyManager.Instance.PlayerCount : 1;
            truck.rewardPerBox = Mathf.RoundToInt(ctx.Economy.GetRewardPerBox(pc) * 1.30f);
            truck.penaltyPerBox = Mathf.RoundToInt(ctx.Economy.penaltyPerBox * 1.55f);
        }

        // Telefon Hattı (relic): PhoneCallManager V4'e geçti (dışarı arama modeli, çalma
        // olasılığı kavramı tamamen kalktı — bkz. plans/plateup-musteri-telefon.md §D,
        // 2026-08-29). Eski hedef alan (phoneRingPerkBonus, kaldırıldı) yerine perk artık İKİ
        // alana yazıyor (economist round10 U4/U5, 2026-08-30):
        //  - phoneTimeSkipPerkMultiplier = 0.80f: GERÇEK ekonomik kaldıraç — çağrı başına zaman
        //    maliyeti %20 azalır (değer/maliyet 0-0.62-1.51x, v5 sim ile doğrulandı).
        //  - phoneCooldownPerkBonusSeconds = 1f (eski 10f): taban 3f'e (phoneCooldownSeconds)
        //    göre zaten Mathf.Max(1, 3-10) ile tabana çakılıyordu — ekonomik değeri SIFIR, yalnız
        //    his/etiket amaçlı, mutlak atama (idempotent) korunuyor.
        private static void ApplyPhoneLine(int level, PerkContext ctx)
        {
            if (ctx.Economy == null || level <= 0) return;
            ctx.Economy.phoneTimeSkipPerkMultiplier = 0.80f;
            ctx.Economy.phoneCooldownPerkBonusSeconds = 1f;
        }

        // ─────────────────────────────────────────────────────────────
        //  Gerçek kod dokunuşu gerektirenler — task7-prep.md tablo 2
        // ─────────────────────────────────────────────────────────────

        // DOKUNUŞ-1: Kaldıraçlı Kira (relic). rentScaledMultiplier scaledRent'e uygulanır
        // (GameEconomySettings.CalculateRent). FAZ4 §B.7 upgrade turu — tercih edilen etki
        // değişikliği uygulandı: bedel artık prestij cezası ×2 DEĞİL, grace period iptali
        // (all_in ile aynı mekanik — ikisi EXCLUSIVE_EFFECT_GROUPS'ta aynı grupta). Kira indirimi
        // 0.8 → 0.75 (kira %25 düşer). Eski customerLostPrestigePenalty = -0.8f satırı (FAZ4 §B.3
        // fallback) bu satırla kalkar.
        // Ö-C fix (2026-09-24, docs/economy/ekonomi-sifirdan-2026-09-23.md §4): eskiden
        // gracePaymentPercent = 0f yazılıyordu — bu, DayCycleManager.TryProcessMoneyCheck'in grace
        // dalına GİRMESİNE izin verip %0'ını alıp kirayı "ödenmiş" sayıyordu, yani perkin bedeli
        // fiilen bedava kira oluyordu (bug). Doğrusu: grace dalına hiç girilmemesi — bayrakla.
        private static void ApplyLeveragedRent(int level, PerkContext ctx)
        {
            if (ctx.Economy == null || level <= 0) return;
            ctx.Economy.rentScaledMultiplier = 0.75f;
            ctx.Economy.graceDisabled = true;
        }

        // DOKUNUŞ-2: Yüksek Volatilite (relic). Per-delivery ±%35 RNG, ort. +%15 — Truck.
        // ApplyRewardVolatility() içinde (server-only) okunur. EV her zaman pozitif (rapor §4.2).
        private static void ApplyHighVolatility(int level, PerkContext ctx)
        {
            if (ctx.Economy == null || level <= 0) return;
            ctx.Economy.rewardVolatility = 0.35f;
            ctx.Economy.rewardVolatilityMean = 1.15f;
        }

        // Kelle Koltukta (relic): gelir +%25, grace period iptal (graceDisabled=true — Ö-C fix,
        // bkz. ApplyLeveragedRent üstündeki yorum: eskiden gracePaymentPercent=0 yazıyordu, bu da
        // grace dalına girip %0 alarak kirayı bedava "ödenmiş" sayıyordu).
        // perk-revival: SPLIT — Economy (kalıcı SO, tek instance, güvenle burada kalır) parçası
        // burada; Truck ödül parçası ApplyAllInRewardToTruck'a taşındı (canlı instance gerekir).
        private static void ApplyAllIn(int level, PerkContext ctx)
        {
            if (ctx.Economy == null || level <= 0) return;
            ctx.Economy.graceDisabled = true;
        }

        // Kelle Koltukta — ödül parçası: rewardPerBox +%25, CANLI tıra (ApplyToTruck üzerinden).
        // Event-rebase notu için bkz. ApplyGamblerCaseToTruck üstündeki QA-fix yorumu — aynı
        // gerekçe (rebase çağrısı UpgradePanel.ApplyPerkToAllLiveTrucks'ta, burada değil).
        private static void ApplyAllInRewardToTruck(int level, Truck truck, PerkContext ctx)
        {
            if (ctx.Economy == null || level <= 0) return;
            // PlateUp reward-lever fix (2026-08-29) — bkz. ApplyGamblerCaseToTruck üstündeki yorum,
            // aynı gerekçe: taban GetRewardPerBox(playerCount) olmalı, düz rewardPerBox değil.
            int pc = DifficultyManager.Instance != null ? DifficultyManager.Instance.PlayerCount : 1;
            truck.rewardPerBox = Mathf.RoundToInt(ctx.Economy.GetRewardPerBox(pc) * 1.25f);
        }

        // DOKUNUŞ-3: Acil Fren (relic). İflası 1 kez önleyen tek-kullanımlık bayrak;
        // DayCycleManager.TryProcessMoneyCheck() rent-fail dalında tüketilir.
        private static void ApplyEmergencyBrake(int level, PerkContext ctx)
        {
            if (ctx.DayCycle == null || level <= 0) return;
            ctx.DayCycle.insuranceAvailable = true;
        }

        // Mesai Saati: günün gerçek süresini hafifçe uzatır. Taban × 1.125 (~+%12.5, 200 → 225sn),
        // DEĞİŞMEDİ. Çarpımsal + idempotent: DayCycleManager.SetOvertimeMultiplier level 0'da 1f'e
        // döner, bu yüzden HandleUpgradeLevelsChanged tekrar tetiklense bile süre sürüklenmez.
        // Ö-B fix (2026-09-24, docs/economy/ob-kart-duzeltmeleri-2026-09-24.md §2 — artık economist
        // spec'i, eski "soyut büyüklük, economist onayı gerekmez" notu GEÇERSİZ): saf efekt sim'de
        // ≈0 çıkıyordu çünkü orta/iyi takım kotayı günün %77-100'ünde erken bitirip uzamış saate
        // hiç ulaşmıyordu. Düzeltme: kota bitince günü sarma payına (CustomerManager.
        // overtimeGraceBonusSeconds, +15 sn) da ek yapıyor — artık gerçekten "gün uzuyor".
        private static void ApplyOvertime(int level, PerkContext ctx)
        {
            if (ctx.DayCycle == null) return;
            ctx.DayCycle.SetOvertimeMultiplier(level > 0 ? 1.125f : 1f);

            if (ctx.CustomerManager != null)
            {
                ctx.CustomerManager.overtimeGraceBonusSeconds = level > 0 ? 15f : 0f;
            }
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
