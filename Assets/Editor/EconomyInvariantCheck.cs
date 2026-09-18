// FAZ4 ekonomi değerlerinin nöbetçisi.
//
// İki işi var:
//   1. REGRESYON: plans/economy-rebuild-2026-07-30-faz4-final.md §B'de kilitlenen değerler
//      hâlâ yerinde mi? Biri diziyi/alanı yanlışlıkla değiştirirse burası bağırır.
//   2. BOZULMA TESPİTİ: PerkEffect, perk satın alınınca GameEconomySettings (ScriptableObject)
//      ve Truck (prefab) ALANLARINI DOĞRUDAN yazıyor. Bunlar kalıcı asset'ler — Editor'de
//      Play mode'dan çıkınca değerler geri gelmiyor ve diske yazılabiliyor.
//      PLAY-TEST SONRASI BUNU ÇALIŞTIR: perk'lerin ekonomiyi kalıcı bozup bozmadığını söyler.
//
// Kullanım:
//   Editor          → menü: Cargor / Ekonomi Değerlerini Doğrula
//   Komut satırı    → Unity.exe -batchmode -nographics -quit -projectPath . \
//                       -executeMethod EconomyInvariantCheck.RunFromCommandLine -logFile -
//                     (hata varsa exit code 1)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using NewCss;   // PerkTier de bu namespace'te (asmdef adı NewCss.Roguelite ama rootNamespace NewCss)

public static class EconomyInvariantCheck
{
    private const string ECONOMY_ASSET = "EkonomiAyarlari";
    private const string DIFFICULTY_PREFAB = "Assets/DifficultyManager.prefab";
    private const string TRUCK_PREFAB_GUID = "7269fc87ad078194d9a92fb25280efa9";

    private sealed class Report
    {
        public readonly List<string> Failures = new();
        public readonly List<string> Corruption = new();
        public int Checked;

        public void Expect(string label, object actual, object expected)
        {
            Checked++;
            if (!Equals(actual, expected))
                Failures.Add($"{label}: beklenen {Fmt(expected)}, bulunan {Fmt(actual)}");
        }

        public void ExpectFloat(string label, float actual, float expected, float tolerance = 0.0001f)
        {
            Checked++;
            if (Mathf.Abs(actual - expected) > tolerance)
                Failures.Add($"{label}: beklenen {Fmt(expected)}, bulunan {Fmt(actual)}");
        }

        public void ExpectArray(string label, IReadOnlyList<float> actual, float[] expected)
        {
            Checked++;
            if (actual == null) { Failures.Add($"{label}: dizi NULL"); return; }
            if (actual.Count != expected.Length)
            {
                Failures.Add($"{label}: uzunluk {expected.Length} olmalı, {actual.Count} bulundu " +
                             (actual.Count == 0 ? "(YAML'a elle hex yazılmış olabilir — float[] için o format ÇALIŞMAZ)" : ""));
                return;
            }
            for (int i = 0; i < expected.Length; i++)
            {
                if (Mathf.Abs(actual[i] - expected[i]) > 0.0001f)
                {
                    Failures.Add($"{label}: beklenen [{string.Join(", ", expected)}], bulunan [{string.Join(", ", actual)}]");
                    return;
                }
            }
        }

        public void ExpectIntArray(string label, IReadOnlyList<int> actual, int[] expected)
        {
            Checked++;
            if (actual == null) { Failures.Add($"{label}: dizi NULL"); return; }
            if (!actual.SequenceEqual(expected))
                Failures.Add($"{label}: beklenen [{string.Join(", ", expected)}], bulunan [{(actual.Count == 0 ? "BOŞ" : string.Join(", ", actual))}]");
        }

        /// <summary>Perk'in kalıcı asset'e sızdırdığı değerleri ayrı raporlar — bu bir denge hatası değil, bir BOZULMA.</summary>
        public void ExpectPristine(string label, float actual, float authored, string culprit)
        {
            Checked++;
            if (Mathf.Abs(actual - authored) > 0.0001f)
                Corruption.Add($"{label}: taban {Fmt(authored)} olmalı, {Fmt(actual)} bulundu — `{culprit}` perki yazmış olabilir");
        }

        private static string Fmt(object v) =>
            v is float f ? f.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) : Convert.ToString(v);
    }

    [MenuItem("Cargor/Ekonomi Değerlerini Doğrula")]
    public static void RunFromMenu()
    {
        var report = Run();
        Debug.Log(Format(report));
    }

    public static void RunFromCommandLine()
    {
        var report = Run();
        string text = Format(report);

        if (report.Failures.Count > 0 || report.Corruption.Count > 0)
        {
            Debug.LogError(text);
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log(text);
        EditorApplication.Exit(0);
    }

    private static Report Run()
    {
        var r = new Report();
        CheckEconomySettings(r);
        CheckDifficultyManager(r);
        CheckTruckPrefab(r);
        CheckQuestAssets(r);
        CheckScene(r);
        return r;
    }

    // ── Sahne (The Main Office) ───────────────────────────────────────────────
    // FAZ4 §D#5'in çoğu değeri SAHNEDE yaşıyor (25 upgrade'in fiyatları, maxLevel'leri,
    // tier'ları). SO ve prefab'ları denetleyip sahneyi atlamak en büyük yüzeyi açıkta bırakırdı.
    private const string MAIN_SCENE = "Assets/Scenes/The Main Office.unity";

    private static void CheckScene(Report r)
    {
        var previouslyOpen = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path;
        UnityEngine.SceneManagement.Scene scene;

        try
        {
            scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                MAIN_SCENE, UnityEditor.SceneManagement.OpenSceneMode.Single);
        }
        catch (Exception e)
        {
            r.Failures.Add($"Sahne açılamadı ({MAIN_SCENE}): {e.Message}");
            return;
        }

        if (!scene.IsValid()) { r.Failures.Add($"Sahne geçersiz: {MAIN_SCENE}"); return; }

        var roots = scene.GetRootGameObjects();

        // --- Prestij ---
        var prestige = FindInScene<PrestigeManager>(roots);
        if (prestige == null) r.Failures.Add("Sahnede PrestigeManager yok");
        else
        {
            r.ExpectFloat("sahne PrestigeManager.startingPrestige", prestige.startingPrestige, 12f);
            r.ExpectFloat("sahne PrestigeManager.maxPrestige", prestige.maxPrestige, 100f);
        }

        // --- Gün süresi ---
        var dayCycle = FindInScene<DayCycleManager>(roots);
        if (dayCycle == null) r.Failures.Add("Sahnede DayCycleManager yok");
        else
            r.ExpectFloat("sahne DayCycleManager.realDurationInSeconds", dayCycle.realDurationInSeconds, 200f);

        // --- Telefon ---
        var phone = FindInScene<PhoneCallManager>(roots);
        if (phone == null) r.Failures.Add("Sahnede PhoneCallManager yok");
        else
        {
            // V4 (2026-08-29): ringDuration alanı kalktı (dışarı arama modeli, çalma yok).
            // phoneStartHour/phoneEndHour hâlâ mesai penceresini sınırlıyor.
            r.Expect("sahne PhoneCallManager.phoneStartHour", ReadPrivate<int>(phone, "phoneStartHour"), 8);
            r.Expect("sahne PhoneCallManager.phoneEndHour", ReadPrivate<int>(phone, "phoneEndHour"), 18);

            // SO bağlanmazsa §D'nin tamamı inert kalıyor (hard-coded fallback'ler devreye girer).
            var so = ReadPrivate<GameEconomySettings>(phone, "economySettings");
            if (so == null)
                r.Failures.Add("sahne PhoneCallManager.economySettings BAĞLANMAMIŞ — " +
                               "P-bazlı timeSkipAmount, cooldown ve callPrestigeReward devre dışı kalır");

            // SESSİZ ÖLÜM SİGORTASI (2026-08-31). Bu alan İKİ KEZ koptu ve iki kez de aylarca
            // fark edilmedi: (1) 2026-08-13 hiç bağlanmamıştı, (2) 2026-08-31 — `e669e33`
            // sahneyi V4 için yeniden serileştirirken AudioSource'u (&424799598) sildi, alan
            // {fileID: 0}'a düştü. Kullanım yeri null-guard'lı (PhoneCallManager.cs:606) →
            // ne hata ne uyarı, sadece sessizlik. Telefon V4'te ANA etkileşim olduğu için
            // ses geri bildiriminin kaybı gerçek bir oyun kusuru.
            // Ekonomik bir değer değil ama denetçi sahneyi zaten açtığı için en ucuz yer burası.
            if (ReadPrivate<AudioSource>(phone, "successCallSound") == null)
                r.Failures.Add("sahne PhoneCallManager.successCallSound BAĞLANMAMIŞ — " +
                               "başarılı arama sesi çalmaz, kod null-guard'lı olduğu için " +
                               "hiçbir hata vermeden SESSİZCE ölür (bkz. e669e33 regresyonu)");
        }

        // --- Quest UI slotları (R11 D4 sigortası, 2026-08-30) ---
        // economy_full_balance_round11_2026-08-30.md §1: QuestManager bir gün en fazla
        // QuestManager.BASE_DAILY_QUEST_COUNT (3, private const) kadar teklif üretir; sahnede
        // bundan FAZLA veya AZ QuestSlotUI olursa (a) fazlası hiç gösterilemez - sessiz NO-OP
        // (round10 U6'nın tam düştüğü tuzak), (b) azı teklifleri kırpar. 3, QuestManager'daki
        // sabitle EL İLE senkron tutulmalı - burada reflection'la okunamaz (private const, IL'e
        // gömülür).
        var questUi = FindInScene<NewCss.Quest.QuestUIController>(roots);
        if (questUi == null) r.Failures.Add("Sahnede QuestUIController yok");
        else
        {
            var slots = ReadPrivate<List<NewCss.Quest.QuestSlotUI>>(questUi, "questSlots");
            r.Expect("sahne QuestUIController.questSlots.Count (== QuestManager.BASE_DAILY_QUEST_COUNT)",
                     slots?.Count ?? -1, 3);
        }

        // --- Upgrade listesi (FAZ4 §B.7'de kısılan omurgalar + perk bayrakları) ---
        var panel = FindInScene<UpgradePanel>(roots);
        if (panel == null) { r.Failures.Add("Sahnede UpgradePanel yok"); return; }

        var upgrades = ReadPrivate<List<UpgradeDefinition>>(panel, "upgrades");
        if (upgrades == null || upgrades.Count == 0)
        {
            r.Failures.Add("sahne UpgradePanel.upgrades listesi BOŞ");
            return;
        }

        r.Checked++;

        CheckUpgrade(r, upgrades, "Geniş Ambar",          maxLevel: 2, baseCost: 60,  costStep: 30);
        CheckUpgrade(r, upgrades, "Paketleme İstasyonu",  maxLevel: 1, baseCost: 150);
        CheckUpgrade(r, upgrades, "Ek Hangar",            maxLevel: 1, baseCost: 200);

        CheckPerkFlag(r, upgrades, "emergency_brake", expectTier: PerkTier.T1, expectDisabled: null);
        CheckPerkFlag(r, upgrades, "long_queue",     expectTier: null,          expectDisabled: true);

        if (!string.IsNullOrEmpty(previouslyOpen) && previouslyOpen != MAIN_SCENE)
        {
            try { UnityEditor.SceneManagement.EditorSceneManager.OpenScene(previouslyOpen); }
            catch { /* açılış sahnesini geri yükleyemedik — denetim sonucunu etkilemez */ }
        }
    }

    private static void CheckUpgrade(Report r, List<UpgradeDefinition> list, string displayName,
                                     int maxLevel, int baseCost, int? costStep = null)
    {
        var u = list.FirstOrDefault(x => x != null && x.displayName == displayName);
        if (u == null)
        {
            r.Failures.Add($"sahne upgrade bulunamadı: \"{displayName}\" " +
                           "(displayName eşleşmesi — sahnede yeniden adlandırıldıysa burayı güncelle)");
            return;
        }

        r.Expect($"upgrade \"{displayName}\".maxLevel", u.maxLevel, maxLevel);
        r.Expect($"upgrade \"{displayName}\".baseCost", u.baseCost, baseCost);
        if (costStep.HasValue)
            r.Expect($"upgrade \"{displayName}\".costStep", u.costStep, costStep.Value);
    }

    private static void CheckPerkFlag(Report r, List<UpgradeDefinition> list, string effectId,
                                      PerkTier? expectTier, bool? expectDisabled)
    {
        var u = list.FirstOrDefault(x => x != null && x.effectId == effectId);
        if (u == null) { r.Failures.Add($"sahne perk bulunamadı: effectId \"{effectId}\""); return; }

        if (expectTier.HasValue)
            r.Expect($"perk \"{effectId}\".tier", u.tier, expectTier.Value);
        if (expectDisabled.HasValue)
            r.Expect($"perk \"{effectId}\".disabledInDraft", u.disabledInDraft, expectDisabled.Value);
    }

    private static T FindInScene<T>(GameObject[] roots) where T : Component
    {
        foreach (var root in roots)
        {
            var found = root.GetComponentInChildren<T>(true);
            if (found != null) return found;
        }
        return null;
    }

    // ── GameEconomySettings (Assets/Resources/EkonomiAyarlari.asset) ──────────
    private static void CheckEconomySettings(Report r)
    {
        var eco = Resources.Load<GameEconomySettings>(ECONOMY_ASSET);
        if (eco == null)
        {
            r.Failures.Add($"{ECONOMY_ASSET} yüklenemedi (Assets/Resources/ altında mı?)");
            return;
        }

        // §B.3 kira — economist round10 U1 (2026-08-30): Slow/strict iflasını kurtarmak için
        // {500,1000,1450,1800} → {290,650,1140,1630} (bkz. GameEconomySettings.cs tooltip).
        r.ExpectIntArray("baseRentByPlayerCount", eco.baseRentByPlayerCount, new[] { 290, 650, 1140, 1630 });
        r.ExpectFloat("rentGrowthMultiplier", eco.rentGrowthMultiplier, 1.20f);
        r.Expect("rentIntervalDays", eco.rentIntervalDays, 4);

        // PlateUp geçişi §A/§B (2026-08-29, plans/plateup-musteri-telefon.md) — müşteri kotası
        r.ExpectIntArray("dailyCustomerCountP1", eco.dailyCustomerCountP1,
                          new[] { 4, 4, 4, 4, 4, 4, 4, 5, 5, 5, 5, 5, 6, 6, 6, 6 });
        r.ExpectIntArray("dailyCustomerCountP2", eco.dailyCustomerCountP2,
                          new[] { 7, 7, 7, 8, 8, 9, 9, 9, 10, 10, 10, 11, 11, 11, 12, 12 });
        r.ExpectIntArray("dailyCustomerCountP3", eco.dailyCustomerCountP3,
                          new[] { 8, 8, 8, 8, 9, 9, 9, 10, 10, 10, 11, 11, 12, 12, 12, 13 });
        r.ExpectIntArray("dailyCustomerCountP4", eco.dailyCustomerCountP4,
                          new[] { 8, 8, 8, 8, 9, 9, 9, 10, 10, 10, 11, 11, 12, 12, 12, 13 });
        r.ExpectArray("customerArrivalIntervalByPlayerCount", eco.customerArrivalIntervalByPlayerCount,
                      new[] { 44f, 22f, 21f, 21f });
        r.ExpectFloat("dayEndGraceSeconds", eco.dayEndGraceSeconds, 30f);

        r.Expect("GetDailyCustomerCount(1,1)", eco.GetDailyCustomerCount(1, 1), 4);
        r.Expect("GetDailyCustomerCount(16,1)", eco.GetDailyCustomerCount(16, 1), 6);
        r.Expect("GetDailyCustomerCount(16,4)", eco.GetDailyCustomerCount(16, 4), 13);
        r.Expect("GetDailyCustomerCount(99,2) [clamp]", eco.GetDailyCustomerCount(99, 2), 12);
        r.ExpectFloat("GetCustomerArrivalIntervalSeconds(1)", eco.GetCustomerArrivalIntervalSeconds(1), 44f);
        r.ExpectFloat("GetCustomerArrivalIntervalSeconds(4)", eco.GetCustomerArrivalIntervalSeconds(4), 21f);

        // §B.5 tır
        r.ExpectIntArray("hangarStayDurationByPlayerCount", eco.hangarStayDurationByPlayerCount, new[] { 120, 60, 40, 30 });
        r.ExpectIntArray("truckCargoMinByPlayerCount", eco.truckCargoMinByPlayerCount, new[] { 1, 2, 2, 2 });
        r.ExpectIntArray("truckCargoMaxExclusiveByPlayerCount", eco.truckCargoMaxExclusiveByPlayerCount, new[] { 3, 4, 5, 6 });
        r.Expect("rewardPerBox", eco.rewardPerBox, 50);
        r.Expect("penaltyPerBox", eco.penaltyPerBox, 40);
        r.ExpectIntArray("rewardPerBoxByPlayerCount", eco.rewardPerBoxByPlayerCount, new[] { 50, 55, 70, 88 });
        r.Expect("GetRewardPerBox(1)", eco.GetRewardPerBox(1), 50);
        r.Expect("GetRewardPerBox(2)", eco.GetRewardPerBox(2), 55);
        r.Expect("GetRewardPerBox(3)", eco.GetRewardPerBox(3), 70);
        r.Expect("GetRewardPerBox(4)", eco.GetRewardPerBox(4), 88);

        // §B.3 prestij
        r.ExpectFloat("prestigePerBonus", eco.prestigePerBonus, 8f);
        r.ExpectFloat("bonusPerTier", eco.bonusPerTier, 5f);
        r.ExpectFloat("customerServedPrestigeBonus", eco.customerServedPrestigeBonus, 0.4f);
        r.ExpectFloat("customerLostPrestigePenalty", eco.customerLostPrestigePenalty, -0.4f);
        r.ExpectFloat("wrongProductPrestigePenalty", eco.wrongProductPrestigePenalty, -0.20f); // U12
        r.ExpectFloat("boxDropPrestigePenalty", eco.boxDropPrestigePenalty, -0.04f);
        r.ExpectFloat("wrongDeliveryPrestigePenalty", eco.wrongDeliveryPrestigePenalty, -0.16f);

        // §D telefon V4 (PlateUp geçişi, 2026-08-29) — P2-P4 economist round10 U2 (2026-08-30)
        r.ExpectArray("timeSkipAmountByPlayerCount", eco.timeSkipAmountByPlayerCount,
                      new[] { 115f, 49f, 47f, 47f });
        r.ExpectFloat("phoneCooldownSeconds", eco.phoneCooldownSeconds, 3f); // kullanıcı isteği 2026-08-30, eski 20f çok uzundu
        r.Expect("callMoneyReward", eco.callMoneyReward, 0); // 2026-09-18 tasarim denetimi #2: kosulsuz para kaldirildi, prestij + musteri cagirma kaldi
        r.ExpectFloat("callPrestigeReward", eco.callPrestigeReward, 0.4f);
        r.ExpectFloat("GetTimeSkipAmountMinutes(1)", eco.GetTimeSkipAmountMinutes(1), 115f); // P1 bilerek sabit
        r.ExpectFloat("GetTimeSkipAmountMinutes(4)", eco.GetTimeSkipAmountMinutes(4), 47f); // U2

        // §E gün-sonu cezası
        r.ExpectFloat("customerMissedQuotaPrestigePenalty", eco.customerMissedQuotaPrestigePenalty, -0.2f);

        // Yardımcı metodlar gerçekten doğru okuyor mu (dizi ↔ metod tutarlılığı) — U1
        r.Expect("GetBaseRent(1)", eco.GetBaseRent(1), 290);
        r.Expect("GetBaseRent(4)", eco.GetBaseRent(4), 1630);
        r.Expect("GetBaseRent(9) [clamp]", eco.GetBaseRent(9), 1630);
        r.Expect("GetBaseRent(0) [clamp]", eco.GetBaseRent(0), 290);
        r.ExpectFloat("GetHangarStayDuration(1)", eco.GetHangarStayDuration(1), 120f);
        r.Expect("GetTruckCargoRange(1)", eco.GetTruckCargoRange(1), (1, 3));
        r.Expect("GetTruckCargoRange(4)", eco.GetTruckCargoRange(4), (2, 6));

        // Kira formülü: baseRent × growth^cycle × scaledMultiplier — U1
        r.ExpectFloat("CalculateRent(1P, dönem 0)", eco.CalculateRent(1, 0), 290f, 0.01f);
        r.ExpectFloat("CalculateRent(4P, dönem 2)", eco.CalculateRent(4, 2), 1630f * 1.20f * 1.20f, 0.5f);

        // ── PERK SIZINTISI ────────────────────────────────────────────────────
        // Bu alanlar PerkEffect tarafından RUNTIME'DA doğrudan yazılıyor ve hiçbir yerde
        // geri alınmıyor. Taban değerden sapmışsa asset kalıcı olarak bozulmuş demektir.
        r.ExpectPristine("gracePaymentPercent", eco.gracePaymentPercent, 0.8f, "leveraged_rent / all_in");
        r.ExpectPristine("rentScaledMultiplier", eco.rentScaledMultiplier, 1f, "leveraged_rent");
        r.ExpectPristine("rewardVolatility", eco.rewardVolatility, 0f, "high_volatility");
        r.ExpectPristine("rewardVolatilityMean", eco.rewardVolatilityMean, 1f, "high_volatility");
        r.ExpectPristine("phoneCooldownPerkBonusSeconds", eco.phoneCooldownPerkBonusSeconds, 0f, "phone_line");
        r.ExpectPristine("phoneTimeSkipPerkMultiplier", eco.phoneTimeSkipPerkMultiplier, 1f, "phone_line"); // U4
    }

    // ── DifficultyManager prefab ──────────────────────────────────────────────
    private static void CheckDifficultyManager(Report r)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DIFFICULTY_PREFAB);
        if (prefab == null) { r.Failures.Add($"{DIFFICULTY_PREFAB} yüklenemedi"); return; }

        var dm = prefab.GetComponent<DifficultyManager>();
        if (dm == null) { r.Failures.Add("DifficultyManager component'i prefab'da yok"); return; }

        var arr = ReadPrivate<float[]>(dm, "upgradeCostMultiplierByPlayerCount");
        r.ExpectArray("upgradeCostMultiplierByPlayerCount", arr, new[] { 1.00f, 2.00f, 2.95f, 3.70f });

        r.ExpectFloat("moneyMultiplierPerPlayer", ReadPrivate<float>(dm, "moneyMultiplierPerPlayer"), 1.2f);
        r.Expect("baseStartingMoney", ReadPrivate<int>(dm, "baseStartingMoney"), 500);
    }

    // ── Truck prefab ──────────────────────────────────────────────────────────
    private static void CheckTruckPrefab(Report r)
    {
        string path = AssetDatabase.GUIDToAssetPath(TRUCK_PREFAB_GUID);
        if (string.IsNullOrEmpty(path)) { r.Failures.Add("Truck prefab GUID çözülemedi"); return; }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var truck = prefab != null ? prefab.GetComponent<Truck>() : null;
        if (truck == null) { r.Failures.Add($"Truck component'i bulunamadı ({path})"); return; }

        // Bu alanlar Truck.Awake'de HER spawn'da economySettings'ten yeniden okunuyor
        // (Truck.cs:211-218), yani prefab değeri yalnızca "SO yüklenemezse" fallback'i.
        // Yine de kontrol ediyoruz: PerkEffect bu alanlara PREFAB üzerinden yazıyor
        // (UpgradePanel.Truck bir prefab referansı, sahne objesi değil), dolayısıyla
        // sapma varsa (a) prefab diske yazılmış, (b) o perk zaten çalışmıyor demektir.
        r.ExpectPristine("Truck.rewardPerBox (prefab fallback)", truck.rewardPerBox, 50f, "gambler_case / all_in");
        r.ExpectPristine("Truck.penaltyPerBox (prefab fallback)", truck.penaltyPerBox, 40f, "gambler_case");
        r.ExpectPristine("Truck.bonusPerTier (prefab fallback)", truck.bonusPerTier, 5f, "prestige_broker");
    }

    // ── Quest asset'leri ──────────────────────────────────────────────────────
    private static void CheckQuestAssets(Report r)
    {
        var quests = Resources.LoadAll<NewCss.Quest.QuestData>("Quests");
        r.Expect("quest asset sayısı", quests.Length, 30);
        if (quests.Length == 0) return;

        // §B.9 tier ödül/ceza tablosu — economist round10 U7 (2026-08-30): prestij ödül+ceza
        // kolonu ×0.4 (maxPrestige=100 tavanına çarpan hücre 3/16 → 0/16, v5 sim). PARA
        // kolonu (money/moneyPen) o turda DEĞİŞMEDİ.
        // R11-2 (economist round11, 2026-08-30): Medium/Hard CEZA kolonu (yalnız ceza — ödüller
        // sabit): Hard moneyPen 53→30, prestigePen 1.05→0.60; Medium moneyPen 27→20,
        // prestigePen 0.55→0.40. Gerekçe: koşulsuz kabul eden "naif oyuncu" için Hard tier
        // 16/16 hücrede negatif EV'ydi (en iyi bantta bile -1.1 TL/gün) — ceza indirimi bunu
        // sağlıklı bir beceri gradyanına çeviriyor. Easy tier DEĞİŞMEDİ.
        var expected = new Dictionary<int, (float money, float moneyPen, float prestige, float prestigePen)>
        {
            { 0, (28f, 15f, 0.6f, 0.32f) },
            { 1, (60f, 20f, 1.2f, 0.4f) },
            { 2, (150f, 30f, 3f, 0.6f) },
        };

        var tierCounts = new Dictionary<int, int> { { 0, 0 }, { 1, 0 }, { 2, 0 } };
        var ids = new HashSet<string>();

        foreach (var q in quests)
        {
            int tier = (int)q.tier;
            if (!expected.TryGetValue(tier, out var e))
            {
                r.Failures.Add($"{q.questId}: bilinmeyen tier {tier}");
                continue;
            }

            tierCounts[tier]++;
            if (!ids.Add(q.questId)) r.Failures.Add($"questId tekrar ediyor: {q.questId}");

            r.ExpectFloat($"{q.questId}.moneyReward", q.moneyReward, e.money);
            r.ExpectFloat($"{q.questId}.moneyPenalty", q.moneyPenalty, e.moneyPen);
            r.ExpectFloat($"{q.questId}.prestigeReward", q.prestigeReward, e.prestige);
            r.ExpectFloat($"{q.questId}.prestigePenalty", q.prestigePenalty, e.prestigePen);

            // Ceza alanları POZİTİF girilir; kod -Mathf.Abs() uygular. Negatif girilirse
            // çift-negatif olup CEZA ÖDÜLE dönüşme tuzağı var.
            if (q.moneyPenalty < 0f || q.prestigePenalty < 0f)
                r.Failures.Add($"{q.questId}: ceza alanı NEGATİF girilmiş (pozitif olmalı)");

            if (string.IsNullOrWhiteSpace(q.questTitle))
                r.Failures.Add($"{q.questId}: başlık boş");
        }

        r.Expect("Easy tier sayısı", tierCounts[0], 11);
        r.Expect("Medium tier sayısı", tierCounts[1], 10);
        r.Expect("Hard tier sayısı", tierCounts[2], 9);
    }

    // ── yardımcı ──────────────────────────────────────────────────────────────
    private static T ReadPrivate<T>(object target, string fieldName)
    {
        var f = target.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (f == null) return default;
        var v = f.GetValue(target);
        return v is T typed ? typed : default;
    }

    private static string Format(Report r)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== EKONOMİ DEĞER DENETİMİ (FAZ4 §B) ===");
        sb.AppendLine($"{r.Checked} kontrol çalıştı.");
        sb.AppendLine();

        if (r.Corruption.Count > 0)
        {
            sb.AppendLine($"🔴 ASSET BOZULMASI — {r.Corruption.Count} alan:");
            sb.AppendLine("   PerkEffect kalıcı asset'lere doğrudan yazıyor ve hiçbir yerde geri almıyor.");
            sb.AppendLine("   Bu değerler bir sonraki oyunu da etkiler. Git'ten geri alın:");
            sb.AppendLine("   git checkout -- Assets/Resources/EkonomiAyarlari.asset \\");
            sb.AppendLine("                   \"Assets/NewCss/TruckScripts/Truck_Anim (2).prefab\"");
            sb.AppendLine();
            foreach (var c in r.Corruption) sb.AppendLine($"   · {c}");
            sb.AppendLine();
        }

        if (r.Failures.Count > 0)
        {
            sb.AppendLine($"❌ DEĞER SAPMASI — {r.Failures.Count} kontrol:");
            foreach (var f in r.Failures) sb.AppendLine($"   · {f}");
            sb.AppendLine();
        }

        if (r.Corruption.Count == 0 && r.Failures.Count == 0)
            sb.AppendLine("✅ Tüm değerler FAZ4 §B ile uyumlu, asset bozulması yok.");

        return sb.ToString();
    }
}
