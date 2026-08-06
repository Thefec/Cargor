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
using NewCss;

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
        return r;
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

        // §B.3 kira
        r.ExpectIntArray("baseRentByPlayerCount", eco.baseRentByPlayerCount, new[] { 500, 1000, 1450, 1800 });
        r.ExpectFloat("rentGrowthMultiplier", eco.rentGrowthMultiplier, 1.35f);
        r.Expect("rentIntervalDays", eco.rentIntervalDays, 4);

        // §B.5 tır
        r.ExpectIntArray("hangarStayDurationByPlayerCount", eco.hangarStayDurationByPlayerCount, new[] { 120, 60, 40, 30 });
        r.ExpectIntArray("truckCargoMinByPlayerCount", eco.truckCargoMinByPlayerCount, new[] { 1, 2, 2, 2 });
        r.ExpectIntArray("truckCargoMaxExclusiveByPlayerCount", eco.truckCargoMaxExclusiveByPlayerCount, new[] { 3, 4, 5, 6 });
        r.Expect("rewardPerBox", eco.rewardPerBox, 50);
        r.Expect("penaltyPerBox", eco.penaltyPerBox, 40);

        // §B.3 prestij
        r.ExpectFloat("prestigePerBonus", eco.prestigePerBonus, 8f);
        r.ExpectFloat("bonusPerTier", eco.bonusPerTier, 5f);
        r.ExpectFloat("customerServedPrestigeBonus", eco.customerServedPrestigeBonus, 0.4f);
        r.ExpectFloat("customerLostPrestigePenalty", eco.customerLostPrestigePenalty, -0.4f);
        r.ExpectFloat("wrongProductPrestigePenalty", eco.wrongProductPrestigePenalty, -0.08f);
        r.ExpectFloat("boxDropPrestigePenalty", eco.boxDropPrestigePenalty, -0.04f);
        r.ExpectFloat("wrongDeliveryPrestigePenalty", eco.wrongDeliveryPrestigePenalty, -0.16f);

        // §B.6 telefon
        r.ExpectFloat("phoneRingChancePerHour (legacy fallback)", eco.phoneRingChancePerHour, 0.20f);
        r.ExpectArray("phoneRingChanceByPlayerCount", eco.phoneRingChanceByPlayerCount,
                      new[] { 0.20f, 0.25f, 0.30f, 0.35f });
        r.ExpectFloat("phoneRingEventMultiplier", eco.phoneRingEventMultiplier, 2.0f);
        r.Expect("callMoneyReward", eco.callMoneyReward, 20);
        r.ExpectFloat("callPrestigeReward", eco.callPrestigeReward, 0.4f);

        // Yardımcı metodlar gerçekten doğru okuyor mu (dizi ↔ metod tutarlılığı)
        r.Expect("GetBaseRent(1)", eco.GetBaseRent(1), 500);
        r.Expect("GetBaseRent(4)", eco.GetBaseRent(4), 1800);
        r.Expect("GetBaseRent(9) [clamp]", eco.GetBaseRent(9), 1800);
        r.Expect("GetBaseRent(0) [clamp]", eco.GetBaseRent(0), 500);
        r.ExpectFloat("GetHangarStayDuration(1)", eco.GetHangarStayDuration(1), 120f);
        r.ExpectFloat("GetPhoneRingChancePerHour(3)", eco.GetPhoneRingChancePerHour(3), 0.30f);
        r.Expect("GetTruckCargoRange(1)", eco.GetTruckCargoRange(1), (1, 3));
        r.Expect("GetTruckCargoRange(4)", eco.GetTruckCargoRange(4), (2, 6));

        // Kira formülü: baseRent × growth^cycle × scaledMultiplier
        r.ExpectFloat("CalculateRent(1P, dönem 0)", eco.CalculateRent(1, 0), 500f, 0.01f);
        r.ExpectFloat("CalculateRent(4P, dönem 2)", eco.CalculateRent(4, 2), 1800f * 1.35f * 1.35f, 0.5f);

        // ── PERK SIZINTISI ────────────────────────────────────────────────────
        // Bu alanlar PerkEffect tarafından RUNTIME'DA doğrudan yazılıyor ve hiçbir yerde
        // geri alınmıyor. Taban değerden sapmışsa asset kalıcı olarak bozulmuş demektir.
        r.ExpectPristine("gracePaymentPercent", eco.gracePaymentPercent, 0.8f, "leveraged_rent / all_in");
        r.ExpectPristine("rentScaledMultiplier", eco.rentScaledMultiplier, 1f, "leveraged_rent");
        r.ExpectPristine("rewardVolatility", eco.rewardVolatility, 0f, "high_volatility");
        r.ExpectPristine("rewardVolatilityMean", eco.rewardVolatilityMean, 1f, "high_volatility");
        r.ExpectPristine("phoneRingPerkBonus", eco.phoneRingPerkBonus, 0f, "phone_line");
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

        // §B.9 tier ödül/ceza tablosu
        var expected = new Dictionary<int, (float money, float moneyPen, float prestige, float prestigePen)>
        {
            { 0, (28f, 15f, 1.4f, 0.8f) },
            { 1, (60f, 27f, 3f, 1.36f) },
            { 2, (150f, 53f, 7.5f, 2.66f) },
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
