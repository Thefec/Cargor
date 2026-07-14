# Roguelite Upgrade Draft + Perk Sistemi — Uygulama Planı

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **Cargor iş akışı notu:** Bu plan **gameplay** departmanı tarafından uygulanır. Her task çıktısı **qa** ve zorunlu **kontrol** kapısından geçer. Ekonomik değer gereken her yerde değerler `UPGRADE_PRICING_REPORT.md` v3.2'den ALINIR — yeni değer uydurulmaz; sapma gerekiyorsa önce economist'e danışılır.

**Goal:** Mağaza upgrade panelini "tüm listeyi göster" düzeninden, gün sonu havuzdan 3 rastgele kart çeken, tier-kilitli, reroll'lu, server-authoritative bir **roguelite draft**'a çevirmek; sıkıcı stat çarpanlarını kaldırıp veri-güdümlü **16 perk** havuzu eklemek.

**Architecture:** Mevcut `UpgradePanel` (NetworkBehaviour, `NetworkList<int> _upgradeLevels/_visualUpgradeLevels`, `_pendingUpgrades`, `CalculateFinalCost`, `_pendingUpgrades → ertesi gün aktif` mekaniği) korunur. Üstüne 3 yeni katman eklenir: (1) **veri-güdümlü perk tanımı** (`PerkDefinition` + tier + effect tipi, mevcut `UpgradeDefinition`'ı genişleterek), (2) **server-authoritative günlük teklif** (`NetworkList<int> _dailyOffer`, tier-filtreli seçim), (3) **reroll**. Perk efektleri, mevcut `ApplyUpgradeEffect` switch'inin veri-güdümlü bir **effect registry**'ye genişletilmesiyle uygulanır.

**Tech Stack:** Unity 6000.4.3f1, URP, Netcode for GameObjects (server-authoritative), C#, TextMeshPro, Unity Localization, Unity Test Framework (EditMode saf-mantık testleri için).

## Global Constraints

- **Değer kaynağı:** Tüm fiyat/tier/etki büyüklükleri `UPGRADE_PRICING_REPORT.md` v3.2'den verbatim. Genel toplam 9945 TL; omurga Raf base=200/step=10, Masa 360/470, Hangar 300/700; reroll 50/90/160/290/525 (×1.8, günlük sıfırlanır).
- **Tier kilidi:** T1 her zaman açık; **T2 gün ≥ 5**; **T3 gün ≥ 9** (tek koşul, gün-bazlı, VE/OR yok). Kaynak: rapor §5.
- **Server-authoritative:** Teklif üretimi, reroll, satın alma yalnızca server'da; client'lar `NetworkList`/RPC ile senkron. Mevcut `_pendingUpgrades → HandleNewDay → ActivatePendingUpgradesServerRpc` (ertesi gün aktif) mekaniği korunur.
- **Uygulama yolu:** Yol A (Inspector/sahne YAML + veri-güdümlü). `Assets/NewCss/UpgradeScripts/UpgradeAssets.cs` + `ItemType.cs` + `UpgradeManager.cs` = ölü kod (Yol B) — bu planda KULLANILMAZ; roguelite geçişi tamamlanınca ayrı bir temizlik task'ıyla devre dışı bırakılır (Task 9).
- **Localization:** Her perk `displayNameLocKey` + `contentTextLocKey` taşır (mevcut `UpgradeDefinition` deseni). Yeni perkler için loc key'ler eklenir.
- **Panel açılışı korunur:** Gün sonu, `GetCurrentHour() >= PANEL_OPEN_HOUR (10)`, oyuncu masa/tezgah trigger'ına girince `TogglePanel()`. Değişen tek şey içerik (tüm liste → 3 kart).

---

## Dosya Yapısı

**Değiştirilecek:**
- `Assets/NewCss/UpgradeScripts/UpgradePanel.cs` — draft katmanı, tier filtresi, reroll, effect registry genişletme. Ana dosya; büyük olduğundan (1185 satır) yeni saf-mantık parçaları ayrı dosyalara çıkarılır (aşağıdaki "Create").
- `Assets/NewCss/TruckScripts/Truck.cs:105` — `bonusPerTier` int→float (Task 0).
- `Assets/NewCss/GameEconomySettings.cs:54` — `bonusPerTier` int→float (Task 0, aynada).
- `Assets/NewCss/Events/EventEffectManager.cs:356,446` — ödül `(int)` cast'lerinin float bonusPerTier'la uyumu (Task 0).

**Oluşturulacak:**
- `Assets/NewCss/UpgradeScripts/PerkTier.cs` — `enum PerkTier { T1, T2, T3 }` + `PerkKind { LeveledBackbone, Perk }`.
- `Assets/NewCss/UpgradeScripts/DraftPool.cs` — saf-mantık: tier filtresi + max filtresi + 3-kart rastgele seçim (scene/network'ten bağımsız, EditMode testli).
- `Assets/NewCss/UpgradeScripts/RerollCurve.cs` — saf-mantık: reroll fiyat eğrisi (EditMode testli).
- `Assets/NewCss/UpgradeScripts/PerkEffect.cs` — perk etki uygulama registry'si (id → Action).
- `Assets/Tests/EditMode/DraftPoolTests.cs` — DraftPool + RerollCurve + tier gating testleri.
- `Assets/Tests/EditMode/Cargor.Tests.EditMode.asmdef` — test assembly (yoksa).

**Veri (Inspector/asset, kod değil):**
- `UpgradePanel.upgrades` listesine 16 perk + tier alanları + Görev Tier feature-flag. Task 8'de doldurulur.

---

## Task 0: `bonusPerTier` int→float + yuvarlama kuralı

**Neden ilk:** Prestij Simsarı'nın 5→5.5→6 basamağı `bonusPerTier`'ın float olmasını gerektiriyor (kontrol notu). İzole, küçük, diğer her şeyi engelliyor.

**Files:**
- Modify: `Assets/NewCss/TruckScripts/Truck.cs:105` (`public int bonusPerTier = 5;` → `public float bonusPerTier = 5f;`)
- Modify: `Assets/NewCss/TruckScripts/Truck.cs:187` civarı (`rewardPerBoxActual = rewardPerBox + (prestigeTiers * bonusPerTier)` → float çarpım + `Mathf.RoundToInt`)
- Modify: `Assets/NewCss/GameEconomySettings.cs:54` (`public int bonusPerTier = 5;` → `float`)
- Modify: `Assets/NewCss/GameEconomySettings.cs:187` (aynı formül, sim tarafı)
- Modify: `Assets/NewCss/Events/EventEffectManager.cs:356,446` (ödül `(int)` cast'leri — float ara değerle uyum, son cast `Mathf.RoundToInt`)

**Interfaces:**
- Produces: `Truck.bonusPerTier` artık `float`; kutu ödülü hesabı `Mathf.RoundToInt(rewardPerBox + prestigeTiers * bonusPerTier)`.

- [ ] **Step 1: `Truck.cs` alan tipini değiştir**

`Assets/NewCss/TruckScripts/Truck.cs:105`:
```csharp
[HideInInspector] public float bonusPerTier = 5f;
```

- [ ] **Step 2: `Truck.cs` ödül formülünü float-güvenli + yuvarlamalı yap**

`Truck.cs` içinde `rewardPerBox + (prestigeTiers * bonusPerTier)` kullanılan yeri bul (ödülün gerçek kutu ödülüne çevrildiği satır) ve şuna çevir:
```csharp
int rewardPerBoxActual = Mathf.RoundToInt(rewardPerBox + prestigeTiers * bonusPerTier);
```
Kural: prestij tier'ı × float bonusPerTier kesirli olabilir; **her kutu ödülü int'e yuvarlanır** (banker's değil, `Mathf.RoundToInt` — 0.5 yukarı). Ekonomik etki ≤ ±0.5 TL/kutu (kontrol: ihmal edilebilir).

- [ ] **Step 3: `GameEconomySettings.cs` mirror**

`GameEconomySettings.cs:54` → `public float bonusPerTier = 5f;`. Satır 187'deki `rewardPerBoxActual = rewardPerBox + (prestigeTiers * bonusPerTier)` → `Mathf.RoundToInt(...)`. Bu editör-simülasyon tarafı; oyun etkisi yok ama tutarlılık için aynı yuvarlama.

- [ ] **Step 4: `EventEffectManager.cs` cast uyumu**

`EventEffectManager.cs:356` ve `:446`'daki ödül `(int)` cast'lerini incele: eğer `bonusPerTier` içeren bir ara ifadeyi kesiyorlarsa, `(int)` → `Mathf.RoundToInt(...)` yap (aşağı-kesme yerine yuvarlama). İçermiyorsa dokunma, sadece derleme uyumunu doğrula.

- [ ] **Step 5: Derleme doğrulaması**

Unity Editor'ı aç, Console'da 0 derleme hatası olduğunu doğrula. (Bu değişiklik saf tip değişikliği; EditMode testi gereksiz, ama `Truck` prefab'ında serialize edilmiş `bonusPerTier` değerinin hâlâ 5 göründüğünü Inspector'da teyit et — `[HideInInspector]` olduğundan reset riski düşük ama kontrol et.)

- [ ] **Step 6: Commit**

```bash
git add Assets/NewCss/TruckScripts/Truck.cs Assets/NewCss/GameEconomySettings.cs Assets/NewCss/Events/EventEffectManager.cs
git commit -m "refactor: bonusPerTier int->float with rounding for Prestige Broker perk"
```

---

## Task 1: Perk tier + kind veri modeli

**Files:**
- Create: `Assets/NewCss/UpgradeScripts/PerkTier.cs`
- Modify: `Assets/NewCss/UpgradeScripts/UpgradePanel.cs:20-66` (`UpgradeDefinition`'a tier/kind/effectId/questFlag alanları ekle)

**Interfaces:**
- Produces: `enum PerkTier { T1, T2, T3 }`, `enum PerkKind { LeveledBackbone, Perk }`; `UpgradeDefinition` yeni alanlar: `PerkTier tier`, `PerkKind kind`, `string effectId`, `bool requiresQuestSystem`.

- [ ] **Step 1: Enum dosyasını oluştur**

`Assets/NewCss/UpgradeScripts/PerkTier.cs`:
```csharp
namespace NewCss
{
    /// <summary>Perk güç tier'ı — havuz kilidi buna göre (rapor §5).</summary>
    public enum PerkTier { T1 = 0, T2 = 1, T3 = 2 }

    /// <summary>Omurga (fiziksel, tier'sız, hep havuzda) mi yoksa perk mi.</summary>
    public enum PerkKind { LeveledBackbone = 0, Perk = 1 }
}
```

- [ ] **Step 2: `UpgradeDefinition`'a alanları ekle**

`UpgradePanel.cs`, `UpgradeDefinition` class'ı (satır 20-66) içine, `=== COST & LEVELS ===` header'ından sonra ekle:
```csharp
        [Header("=== ROGUELITE ===")]
        [Tooltip("Omurga mı perk mi? Omurga tier'sız, her zaman havuzda.")]
        public PerkKind kind = PerkKind.LeveledBackbone;

        [Tooltip("Perk güç tier'ı (T1 hep açık, T2 gün>=5, T3 gün>=9). Omurga için yok sayılır.")]
        public PerkTier tier = PerkTier.T1;

        [Tooltip("Effect registry anahtarı (PerkEffect.cs). Boşsa mevcut displayName switch'i kullanılır.")]
        public string effectId;

        [Tooltip("True ise görev sistemi aktif olana kadar havuza girmez (Görev Tier).")]
        public bool requiresQuestSystem;
```

- [ ] **Step 3: Derleme doğrulaması**

Unity Console 0 hata. Mevcut `upgrades` listesindeki her giriş yeni alanları default (`LeveledBackbone`, `T1`, boş effectId, false) alır — geri uyumlu.

- [ ] **Step 4: Commit**

```bash
git add Assets/NewCss/UpgradeScripts/PerkTier.cs Assets/NewCss/UpgradeScripts/UpgradePanel.cs
git commit -m "feat: add perk tier/kind data model to UpgradeDefinition"
```

---

## Task 2: DraftPool saf-mantık (tier filtresi + max filtresi + 3 seçim)

**Files:**
- Create: `Assets/NewCss/UpgradeScripts/DraftPool.cs`
- Create: `Assets/Tests/EditMode/Cargor.Tests.EditMode.asmdef` (yoksa)
- Create: `Assets/Tests/EditMode/DraftPoolTests.cs`

**Interfaces:**
- Produces:
  - `static PerkTier DraftPool.MaxUnlockedTier(int currentDay)` — gün≥9→T3, gün≥5→T2, yoksa T1.
  - `static bool DraftPool.IsEligible(int index, PerkTier tier, PerkKind kind, bool requiresQuest, int currentLevel, int maxLevel, PerkTier maxUnlocked, bool questActive)` — bir kartın havuza uygun olup olmadığı.
  - `static List<int> DraftPool.SelectOffer(IReadOnlyList<bool> eligibility, int count, System.Random rng)` — uygun index'lerden `count` farklı rastgele seç.

- [ ] **Step 1: Failing test yaz**

`Assets/Tests/EditMode/DraftPoolTests.cs`:
```csharp
using System;
using System.Collections.Generic;
using NUnit.Framework;
using NewCss;

public class DraftPoolTests
{
    [Test]
    public void MaxUnlockedTier_GatesByDay()
    {
        Assert.AreEqual(PerkTier.T1, DraftPool.MaxUnlockedTier(1));
        Assert.AreEqual(PerkTier.T1, DraftPool.MaxUnlockedTier(4));
        Assert.AreEqual(PerkTier.T2, DraftPool.MaxUnlockedTier(5));
        Assert.AreEqual(PerkTier.T2, DraftPool.MaxUnlockedTier(8));
        Assert.AreEqual(PerkTier.T3, DraftPool.MaxUnlockedTier(9));
        Assert.AreEqual(PerkTier.T3, DraftPool.MaxUnlockedTier(16));
    }

    [Test]
    public void IsEligible_ExcludesMaxedAndLockedTierAndInactiveQuest()
    {
        // Omurga, max değil, hep uygun
        Assert.IsTrue(DraftPool.IsEligible(0, PerkTier.T1, PerkKind.LeveledBackbone, false, 3, 10, PerkTier.T1, true));
        // Max seviyeye ulaşmış → hariç
        Assert.IsFalse(DraftPool.IsEligible(0, PerkTier.T1, PerkKind.LeveledBackbone, false, 10, 10, PerkTier.T1, true));
        // T3 perk, gün<9 (maxUnlocked=T1) → hariç
        Assert.IsFalse(DraftPool.IsEligible(1, PerkTier.T3, PerkKind.Perk, false, 0, 1, PerkTier.T1, true));
        // T2 perk, maxUnlocked=T2 → uygun
        Assert.IsTrue(DraftPool.IsEligible(1, PerkTier.T2, PerkKind.Perk, false, 0, 1, PerkTier.T2, true));
        // Quest gerektiren, quest pasif → hariç
        Assert.IsFalse(DraftPool.IsEligible(2, PerkTier.T1, PerkKind.LeveledBackbone, true, 0, 2, PerkTier.T3, false));
    }

    [Test]
    public void SelectOffer_ReturnsDistinctEligibleUpToCount()
    {
        var eligible = new List<bool> { true, false, true, true, true };
        var rng = new Random(12345);
        var offer = DraftPool.SelectOffer(eligible, 3, rng);
        Assert.AreEqual(3, offer.Count);
        CollectionAssert.AllItemsAreUnique(offer);
        foreach (var i in offer) Assert.IsTrue(eligible[i]);
    }

    [Test]
    public void SelectOffer_FewerEligibleThanCount_ReturnsAllEligible()
    {
        var eligible = new List<bool> { true, false, true, false, false };
        var offer = DraftPool.SelectOffer(eligible, 3, new Random(1));
        Assert.AreEqual(2, offer.Count);
    }
}
```

- [ ] **Step 2: asmdef oluştur (yoksa)**

`Assets/Tests/EditMode/Cargor.Tests.EditMode.asmdef` — mevcut `NewCss` assembly adını referansla (gerçek asmdef adını `Assets/NewCss/` altında ara; yoksa NewCss asmdef'siz Assembly-CSharp'ta demektir, o durumda test asmdef `"Assembly-CSharp"` referansı + `"UNITY_INCLUDE_TESTS"` gerektirir). Şablon:
```json
{
  "name": "Cargor.Tests.EditMode",
  "references": ["UnityEngine.TestRunner", "UnityEditor.TestRunner"],
  "includePlatforms": ["Editor"],
  "overrideReferences": true,
  "precompiledReferences": ["nunit.framework.dll"],
  "defineConstraints": ["UNITY_INCLUDE_TESTS"],
  "autoReferenced": false
}
```
Not: `NewCss` kodu asmdef'liyse `references`'a onun adını ekle; değilse test kodunu Assembly-CSharp'a erişecek şekilde yapılandır (gameplay Unity'de doğrular).

- [ ] **Step 3: Test'in FAIL ettiğini doğrula**

Unity Test Runner (Window > General > Test Runner > EditMode > Run All). Beklenen: FAIL — `DraftPool` tanımlı değil.

- [ ] **Step 4: `DraftPool.cs` implementasyonu**

`Assets/NewCss/UpgradeScripts/DraftPool.cs`:
```csharp
using System;
using System.Collections.Generic;

namespace NewCss
{
    /// <summary>
    /// Roguelite draft havuz seçimi — saf mantık, scene/network bağımsız (rapor §5, spec 1.6).
    /// </summary>
    public static class DraftPool
    {
        public const int T2_UNLOCK_DAY = 5;
        public const int T3_UNLOCK_DAY = 9;
        public const int OFFER_COUNT = 3;

        public static PerkTier MaxUnlockedTier(int currentDay)
        {
            if (currentDay >= T3_UNLOCK_DAY) return PerkTier.T3;
            if (currentDay >= T2_UNLOCK_DAY) return PerkTier.T2;
            return PerkTier.T1;
        }

        public static bool IsEligible(int index, PerkTier tier, PerkKind kind, bool requiresQuest,
            int currentLevel, int maxLevel, PerkTier maxUnlocked, bool questActive)
        {
            if (currentLevel >= maxLevel) return false;            // max'a ulaşmış
            if (requiresQuest && !questActive) return false;       // Görev Tier feature-flag
            if (kind == PerkKind.LeveledBackbone) return true;     // omurga tier'sız
            return (int)tier <= (int)maxUnlocked;                  // perk tier kilidi
        }

        /// <summary>eligibility[i]==true olan index'lerden en fazla count farklı, rastgele seç.</summary>
        public static List<int> SelectOffer(IReadOnlyList<bool> eligibility, int count, Random rng)
        {
            var pool = new List<int>();
            for (int i = 0; i < eligibility.Count; i++)
                if (eligibility[i]) pool.Add(i);

            // Fisher-Yates ile ilk count'u karıştır
            for (int i = 0; i < pool.Count && i < count; i++)
            {
                int j = i + rng.Next(pool.Count - i);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }

            var result = new List<int>();
            for (int i = 0; i < pool.Count && i < count; i++) result.Add(pool[i]);
            return result;
        }
    }
}
```

- [ ] **Step 5: Test'in PASS ettiğini doğrula**

Test Runner > Run All. Beklenen: 4 test PASS.

- [ ] **Step 6: Commit**

```bash
git add Assets/NewCss/UpgradeScripts/DraftPool.cs Assets/Tests/EditMode/
git commit -m "feat: add tier-gated draft pool selection logic with EditMode tests"
```

---

## Task 3: RerollCurve saf-mantık

**Files:**
- Create: `Assets/NewCss/UpgradeScripts/RerollCurve.cs`
- Modify: `Assets/Tests/EditMode/DraftPoolTests.cs` (reroll testleri ekle)

**Interfaces:**
- Produces: `static int RerollCurve.CostForReroll(int rerollIndexThisDay)` — 0-index'li: 0→50, 1→90, 2→160, 3→290, 4→525; sonrası son değeri korur (525).

- [ ] **Step 1: Failing test ekle**

`DraftPoolTests.cs` içine:
```csharp
    [Test]
    public void RerollCurve_MatchesApprovedTable()
    {
        Assert.AreEqual(50,  RerollCurve.CostForReroll(0));
        Assert.AreEqual(90,  RerollCurve.CostForReroll(1));
        Assert.AreEqual(160, RerollCurve.CostForReroll(2));
        Assert.AreEqual(290, RerollCurve.CostForReroll(3));
        Assert.AreEqual(525, RerollCurve.CostForReroll(4));
        Assert.AreEqual(525, RerollCurve.CostForReroll(7)); // 5+ tavan
    }
```

- [ ] **Step 2: FAIL doğrula** — Test Runner, `RerollCurve` yok.

- [ ] **Step 3: `RerollCurve.cs`**

`Assets/NewCss/UpgradeScripts/RerollCurve.cs`:
```csharp
namespace NewCss
{
    /// <summary>Reroll fiyat eğrisi — rapor §7 (50/90/160/290/525, ×1.8, günlük sıfırlanır).</summary>
    public static class RerollCurve
    {
        // Onaylı sabit tablo (rapor v3.2 §7). Ondalık ×1.8 yuvarlamasını yeniden hesaplamak yerine
        // tabloyu birebir sabitliyoruz — kontrol bu değerleri onayladı.
        private static readonly int[] Costs = { 50, 90, 160, 290, 525 };

        public static int CostForReroll(int rerollIndexThisDay)
        {
            if (rerollIndexThisDay < 0) rerollIndexThisDay = 0;
            if (rerollIndexThisDay >= Costs.Length) return Costs[Costs.Length - 1];
            return Costs[rerollIndexThisDay];
        }
    }
}
```

- [ ] **Step 4: PASS doğrula** — Test Runner > Run All.

- [ ] **Step 5: Commit**

```bash
git add Assets/NewCss/UpgradeScripts/RerollCurve.cs Assets/Tests/EditMode/DraftPoolTests.cs
git commit -m "feat: add reroll cost curve with tests"
```

---

## Task 4: Server-authoritative günlük teklif (`_dailyOffer` + reroll sayacı)

**Files:**
- Modify: `Assets/NewCss/UpgradeScripts/UpgradePanel.cs` (NetworkList `_dailyOffer`, `_rerollCountToday`, `_questSystemActive`; teklif üretimi; HandleNewDay entegrasyonu)

**Interfaces:**
- Consumes: `DraftPool.MaxUnlockedTier`, `DraftPool.IsEligible`, `DraftPool.SelectOffer`, `RerollCurve.CostForReroll`, `Task 1` alanları.
- Produces:
  - `NetworkList<int> _dailyOffer` — o günkü 3 (veya daha az) upgrade index'i.
  - `NetworkVariable<int> _rerollCountToday`.
  - `void GenerateDailyOfferServer()` (server-only) — teklifi üretir, `_dailyOffer`'ı doldurur, `_rerollCountToday=0`.

- [ ] **Step 1: Network alanlarını ekle**

`UpgradePanel.cs` `#region Network Variables` içine:
```csharp
        private NetworkList<int> _dailyOffer;
        private readonly NetworkVariable<int> _rerollCountToday = new(0);
        private readonly NetworkVariable<bool> _questSystemActive = new(false); // Görev Tier feature-flag
```
`InitializeNetworkLists()` içine: `_dailyOffer = new NetworkList<int>();`

- [ ] **Step 2: Teklif üretimini yaz (server-only)**

`UpgradePanel.cs` `#region Purchase System` yakınına yeni region:
```csharp
        #region Draft Offer

        private void GenerateDailyOfferServer()
        {
            if (!IsServer) return;
            int currentDay = DayCycleManager.Instance != null ? DayCycleManager.Instance.currentDay : 1;
            PerkTier maxUnlocked = DraftPool.MaxUnlockedTier(currentDay);
            bool questActive = _questSystemActive.Value;

            var eligibility = new List<bool>(upgrades.Count);
            for (int i = 0; i < upgrades.Count; i++)
            {
                var def = upgrades[i];
                int visLevel = GetVisualLevel(i);
                eligibility.Add(DraftPool.IsEligible(
                    i, def.tier, def.kind, def.requiresQuestSystem,
                    visLevel, def.maxLevel, maxUnlocked, questActive));
            }

            var rng = new System.Random(unchecked(currentDay * 73856093));
            var offer = DraftPool.SelectOffer(eligibility, DraftPool.OFFER_COUNT, rng);

            _dailyOffer.Clear();
            foreach (var idx in offer) _dailyOffer.Add(idx);
            _rerollCountToday.Value = 0;
        }

        #endregion
```
Not: RNG seed'i `currentDay` tabanlı — aynı gün host yeniden üretse aynı teklif çıkar (deterministik senkron). Reroll farklı seed kullanır (Task 6).

- [ ] **Step 3: Yeni gün entegrasyonu**

`HandleNewDay()` (satır 491) server kolunu genişlet:
```csharp
        private void HandleNewDay()
        {
            if (IsServer)
            {
                ActivatePendingUpgradesServerRpc();
                GenerateDailyOfferServer();
            }
        }
```
Ayrıca ilk teklif için `OnNetworkSpawn`'ın server kolunda (`InitializeUpgradeLevels()`'den sonra) `GenerateDailyOfferServer()` çağır.

- [ ] **Step 4: `_dailyOffer` değişince UI yenile**

`SubscribeToNetworkEvents()` içine `_dailyOffer.OnListChanged += HandleDailyOfferChanged;` ekle (Unsubscribe'da simetrik). Handler:
```csharp
        private void HandleDailyOfferChanged(NetworkListEvent<int> _)
        {
            RebuildDraftEntries();
        }
```
`RebuildDraftEntries()` Task 5'te tanımlanır (şimdilik `RefreshAllUpgradeUI();` çağıran bir stub bırak, Task 5 dolduracak).

- [ ] **Step 5: Derleme + Play doğrulaması**

Unity'de host olarak sahneyi Play'e al; Console'da `_dailyOffer` bir gün geçince (veya spawn'da) 3 index ile dolduğunu logla/doğrula. EditMode testi yok (network davranışı); Play'de gözlem.

- [ ] **Step 6: Commit**

```bash
git add Assets/NewCss/UpgradeScripts/UpgradePanel.cs
git commit -m "feat: server-authoritative daily draft offer generation"
```

---

## Task 5: Panel UI — tüm liste yerine 3 kart

**Files:**
- Modify: `Assets/NewCss/UpgradeScripts/UpgradePanel.cs` (`BuildEntries` → sadece `_dailyOffer`'daki index'ler için kart kur; `OnBuy` teklif index'iyle çalışsın)

**Interfaces:**
- Consumes: `_dailyOffer`, mevcut `BuildSingleEntry`, `EntryUI`.
- Produces: `void RebuildDraftEntries()` — `_dailyOffer`'daki (≤3) upgrade için kart kurar; mevcut `_entries` semantiği korunur (`EntryUI.UpgradeIndex` = gerçek `upgrades` index'i).

- [ ] **Step 1: `RebuildDraftEntries` yaz**

Mevcut `BuildEntries()`'i koru ama draft modunda kullanılmayacak. Yeni:
```csharp
        private void RebuildDraftEntries()
        {
            ClearEntries();
            for (int k = 0; k < _dailyOffer.Count; k++)
            {
                int upgradeIndex = _dailyOffer[k];
                if (upgradeIndex < 0 || upgradeIndex >= upgrades.Count) continue;
                BuildSingleEntry(upgradeIndex); // mevcut metod, gerçek index'le
            }
        }
```
`BuildSingleEntry(int index)` zaten `EntryUI.UpgradeIndex = index` ve `OnBuy(upgradeIndex)` bağlıyor (satır 803-838) — teklif index'i gerçek `upgrades` index'i olduğundan satın alma/pending/effect zinciri değişmeden çalışır.

- [ ] **Step 2: `OnNetworkSpawn` ve panel açılışında draft kur**

`OnNetworkSpawn`'daki `BuildEntries()` çağrısını `RebuildDraftEntries()` ile değiştir (ama `_dailyOffer` server'dan senkronlanana kadar boş olabilir → `HandleDailyOfferChanged` zaten yeniden kuracak). `HandlePanelStateChanged`'de panel açılırken `RebuildDraftEntries()` çağır (güncel teklifi göstermek için).

- [ ] **Step 3: Play doğrulaması**

Host + 1 client ile Play. Gün sonu paneli aç: her iki oyuncuda da **aynı 3 kart** görünmeli. Bir kartı satın al → ertesi gün aktif olduğunu (mevcut pending mekaniği) ve o kartın ertesi gün havuzdan düştüğünü (max'a ulaşmadıysa tekrar gelebilir) doğrula.

- [ ] **Step 4: Commit**

```bash
git add Assets/NewCss/UpgradeScripts/UpgradePanel.cs
git commit -m "feat: draft panel shows 3 offered cards instead of full list"
```

---

## Task 6: Reroll butonu

**Files:**
- Modify: `Assets/NewCss/UpgradeScripts/UpgradePanel.cs` (reroll RPC + fiyat + para düşme)
- Modify: Panel prefabı/sahne — "Yenile" butonu + fiyat text (Inspector, Task 8 ile birlikte bağlanır)

**Interfaces:**
- Consumes: `RerollCurve.CostForReroll`, `_rerollCountToday`, `MoneySystem`.
- Produces: `void OnReroll()` (UI butonu), `RerollServerRpc()`.

- [ ] **Step 1: Reroll mantığı**

```csharp
        #region Reroll

        public void OnReroll()
        {
            int cost = RerollCurve.CostForReroll(_rerollCountToday.Value);
            if (MoneySystem.Instance == null || MoneySystem.Instance.CurrentMoney < cost) return;
            RerollServerRpc();
        }

        [ServerRpc(RequireOwnership = false)]
        private void RerollServerRpc()
        {
            int cost = RerollCurve.CostForReroll(_rerollCountToday.Value);
            if (MoneySystem.Instance == null || MoneySystem.Instance.CurrentMoney < cost) return;

            MoneySystem.Instance.SpendMoney(cost);

            int currentDay = DayCycleManager.Instance != null ? DayCycleManager.Instance.currentDay : 1;
            PerkTier maxUnlocked = DraftPool.MaxUnlockedTier(currentDay);
            var eligibility = new List<bool>(upgrades.Count);
            for (int i = 0; i < upgrades.Count; i++)
            {
                var def = upgrades[i];
                eligibility.Add(DraftPool.IsEligible(i, def.tier, def.kind, def.requiresQuestSystem,
                    GetVisualLevel(i), def.maxLevel, maxUnlocked, _questSystemActive.Value));
            }
            // reroll sayacını seed'e kat → farklı sonuç
            var rng = new System.Random(unchecked((currentDay * 73856093) ^ ((_rerollCountToday.Value + 1) * 19349663)));
            var offer = DraftPool.SelectOffer(eligibility, DraftPool.OFFER_COUNT, rng);

            _dailyOffer.Clear();
            foreach (var idx in offer) _dailyOffer.Add(idx);
            _rerollCountToday.Value += 1;
        }

        #endregion
```

- [ ] **Step 2: Reroll fiyat UI'ı**

`_rerollCountToday.OnValueChanged`'e abone ol; reroll butonu text'ini `RerollCurve.CostForReroll(_rerollCountToday.Value)` ile güncelle, para yetmiyorsa `interactable=false`. Buton referansı `[SerializeField] private Button rerollButton;` + `[SerializeField] private TMP_Text rerollCostText;` ekle, `OnNetworkSpawn`'da `rerollButton.onClick.AddListener(OnReroll)`.

- [ ] **Step 3: Play doğrulaması**

Reroll'a bas → 3 kart değişir, para 50 düşer, ikinci reroll 90 ister; ertesi gün sayaç sıfırlanır (`GenerateDailyOfferServer` → `_rerollCountToday=0`). Client'ta da senkron.

- [ ] **Step 4: Commit**

```bash
git add Assets/NewCss/UpgradeScripts/UpgradePanel.cs
git commit -m "feat: reroll button with increasing cost curve"
```

---

## Task 7: Perk effect registry (16 perk etkisi)

**Files:**
- Create: `Assets/NewCss/UpgradeScripts/PerkEffect.cs`
- Modify: `Assets/NewCss/UpgradeScripts/UpgradePanel.cs` (`ApplyUpgradeEffect` → `effectId` varsa registry'ye delege)

**Interfaces:**
- Consumes: `Truck`, `CustomerManager`, `PlayerMovement`, `GameEconomySettings`, `PrestigeManager`, Phone referansları (mevcut manager alanları + gerekenler eklenir).
- Produces: `PerkEffect.Apply(string effectId, int level, PerkContext ctx)` — effectId'ye göre ilgili kaldıracı `level`'a göre uygular.

> **Değer kaynağı:** her effect'in büyüklüğü `UPGRADE_PRICING_REPORT.md` v3.2 §3-§4'ten. Örn. Kumarbaz +%30/+%55, Yüksek Volatilite +%15/±%35, Ucuz Kira rentGrowthMult basamağı 1.15→1.12→1.09→1.06, Prestij Simsarı bonusPerTier 5→5.5→6, Prestij Ustası 0.5→0.65→0.8.

- [ ] **Step 1: `PerkContext` + registry iskeleti**

`Assets/NewCss/UpgradeScripts/PerkEffect.cs`:
```csharp
using UnityEngine;

namespace NewCss
{
    /// <summary>Perk etkilerinin dokunduğu sistemlere referans taşıyıcı.</summary>
    public class PerkContext
    {
        public Truck Truck;
        public CustomerManager CustomerManager;
        public PlayerMovement PlayerMovement;
        public GameEconomySettings Economy;   // rent/grace/penalty kaldıraçları
        // Gerekirse: PhoneManager, PrestigeManager referansları eklenir
    }

    /// <summary>
    /// effectId → kaldıraç uygulaması. Değerler UPGRADE_PRICING_REPORT.md v3.2'den.
    /// level: 0 = etkisiz (henüz alınmamış), 1..maxLevel = seviye.
    /// </summary>
    public static class PerkEffect
    {
        public static void Apply(string effectId, int level, PerkContext ctx)
        {
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
                // gambler_case, high_volatility, leveraged_rent, all_in, emergency_brake,
                // phone_line, overtime, bulk_buy: aşağıdaki adımlarda
            }
        }
        // metod gövdeleri sonraki adımlarda
    }
}
```

- [ ] **Step 2: Basit kaldıraç etkileri (level-lineer)**

Aşağıdaki metodları ekle (değerler rapordan). `level` 1 tabanlı; relic (tek-seferlik) perklerde level 0/1.
```csharp
        // Ucuz Kira: rentGrowthMultiplier 1.15 → 1.12 → 1.09 → 1.06 (her seviye -0.03)
        private static void ApplyCheapRent(int level, PerkContext ctx)
        {
            if (ctx.Economy == null) return;
            ctx.Economy.rentGrowthMultiplier = 1.15f - 0.03f * level;
        }

        // Prestij Simsarı: bonusPerTier 5 → 5.5 → 6 (her seviye +0.5)
        private static void ApplyPrestigeBroker(int level, PerkContext ctx)
        {
            if (ctx.Truck == null) return;
            ctx.Truck.bonusPerTier = 5f + 0.5f * level;
        }

        // Prestij Ustası: customerServedPrestigeBonus 0.5 → 0.65 → 0.8 (her seviye +0.15)
        private static void ApplyPrestigeMaster(int level, PerkContext ctx)
        {
            if (ctx.Economy == null) return;
            ctx.Economy.customerServedPrestigeBonus = 0.5f + 0.15f * level;
        }

        // Hızlı Hangar (relic): hangarStayDuration +%30
        private static void ApplyFastHangar(int level, PerkContext ctx)
        {
            if (ctx.Truck == null || level <= 0) return;
            ctx.Truck.hangarStayDuration = 120f * 1.30f;
        }

        // Enerjik Ekip (relic): staminaRegenRate belirgin artış (eski 3 sv toplamı ~+1.5)
        private static void ApplyEnergeticCrew(int level, PerkContext ctx)
        {
            if (ctx.PlayerMovement == null || level <= 0) return;
            ctx.PlayerMovement.staminaRegenRate += 1.5f;
        }

        // Uzun Kuyruk (relic): maxQueueSize +2 (eski 4 sv etkisi tek relике)
        private static void ApplyLongQueue(int level, PerkContext ctx)
        {
            if (ctx.CustomerManager == null || level <= 0) return;
            ctx.CustomerManager.maxQueueSize += 2;
        }
```
> **gameplay notu:** Çevik Ekip (hareket hızı), Sabırlı Müşteriler (patience), Hızlı Hangar için ilgili alanların gerçek isimleri `PlayerMovement`, `CustomerAI`/`CustomerManager`, `Truck` içinde doğrulanmalı; yoksa economist'e değil, mevcut alan API'sine göre bağlanır (bu değerler ekonomik büyüklük değil, mevcut mekaniğin var/yok bağlanışıdır — economist onayı gerekmez, çünkü fiyat rapordan sabit).

- [ ] **Step 3: Risk perkleri (Kumarbaz, Volatilite, Kaldıraçlı Kira, Kelle Koltukta, Acil Fren)**

Bu perkler `rewardPerBox`/`penaltyPerBox`/`gracePaymentPercent`/rent kaldıraçlarına dokunur. Değerler rapor §4:
```csharp
        // Kumarbaz Kasası (relic): ödül +%30, ceza +%55
        private static void ApplyGamblerCase(int level, PerkContext ctx)
        {
            if (ctx.Economy == null || level <= 0) return;
            ctx.Economy.rewardPerBox  = Mathf.RoundToInt(ctx.Economy.rewardPerBox * 1.30f);
            ctx.Economy.penaltyPerBox = Mathf.RoundToInt(ctx.Economy.penaltyPerBox * 1.55f);
        }

        // Kaldıraçlı Kira (relic): kira scaledRent -%20 (GameEconomySettings.CalculateRent'te uygulanır)
        //   → GameEconomySettings'e bir çarpan alanı eklenir: public float rentScaledMultiplier = 1f;
        //   CalculateRent: scaledRent = baseRent * pow(...) * rentScaledMultiplier
        private static void ApplyLeveragedRent(int level, PerkContext ctx)
        {
            if (ctx.Economy == null || level <= 0) return;
            ctx.Economy.rentScaledMultiplier = 0.8f;             // -%20 sadece scaledRent
            ctx.Economy.customerLostPrestigePenalty *= 2f;       // bedel: prestij cezası ×2
        }

        // Kelle Koltukta (relic): gelir +%25, grace period iptal
        private static void ApplyAllIn(int level, PerkContext ctx)
        {
            if (ctx.Economy == null || level <= 0) return;
            ctx.Economy.rewardPerBox = Mathf.RoundToInt(ctx.Economy.rewardPerBox * 1.25f);
            ctx.Economy.gracePaymentPercent = 0f;                // güvenlik ağı kalkar
        }
```
> **Kod değişikliği gerekli:** `GameEconomySettings`'e `rentScaledMultiplier` (Kaldıraçlı Kira) ve `CalculateRent`'te `scaledRent *= rentScaledMultiplier` satırı. Yüksek Volatilite ve Kumarbaz'ın "her kutu ±%35 / EV" davranışı ödül hesaplandığı yerde (`Truck` ödül dağıtımı) uygulanır — bu bir per-delivery rastgelelik, bir flag ile (`ctx.Economy.rewardVolatility = 0.35f`) işaretlenip Truck ödül kodunda okunur. Acil Fren (iflas önleme) rent/game-over zincirinde tek-kullanımlık bir bayrak gerektirir — GameStateManager/rent ödeme akışına `bool insuranceAvailable` eklenir. Bu üç davranış küçük ama gerçek kod dokunuşudur; gameplay bunları qa ile netleştirir.

- [ ] **Step 4: Utility perkleri (Telefon, Mesai, Toplu Alım)**

Telefon Hattı (`maxCallsPerHour`/`callReward`), Mesai Saati (gün süresi), Toplu Alım (sonraki draft'ta 1 kart -%50 → `_pendingDiscount` bayrağı). Phone/DayCycle API'lerini oku, rapor §3 büyüklükleriyle bağla. Toplu Alım için `UpgradePanel`'de `_nextDraftDiscountCard` bayrağı + `CalculateFinalCost`'ta uygulama.

- [ ] **Step 5: `ApplyUpgradeEffect` delegasyonu**

`UpgradePanel.cs:555` `ApplyUpgradeEffect`'i genişlet: `effectId` doluysa `PerkEffect.Apply(entry.Definition.effectId, level, BuildPerkContext())` çağır; boşsa mevcut switch (geriye uyum). `BuildPerkContext()` mevcut manager alanlarından `PerkContext` kurar.

- [ ] **Step 6: Play doğrulaması (perk başına)**

Her perk için: satın al → ertesi gün aktifleş → ilgili kaldıracın gerçekten değiştiğini doğrula (örn. Ucuz Kira sonrası kira hesabı düşük, Prestij Simsarı sonrası kutu ödülü yüksek). qa bu doğrulamayı senaryo bazında yapar.

- [ ] **Step 7: Commit**

```bash
git add Assets/NewCss/UpgradeScripts/PerkEffect.cs Assets/NewCss/UpgradeScripts/UpgradePanel.cs Assets/NewCss/GameEconomySettings.cs
git commit -m "feat: data-driven perk effect registry for 16 perks"
```

---

## Task 8: Perk verisini gir (Inspector/sahne) + omurga fiyatları + Görev Tier flag

**Files:**
- Modify: `Assets/Scenes/The Main Office.unity` (UpgradePanel `upgrades` listesi — Inspector'da) VEYA panel prefabı, hangisi canonical'sa.
- Modify: Localization tabloları (yeni perk loc key'leri).

**Bu task kod değil veri girişidir — Unity Editor'da yapılır.**

- [ ] **Step 1: Omurga fiyatlarını güncelle**

`upgrades` listesinde Raf/Storage: `baseCost=200, costStep=10, maxLevel=10, kind=LeveledBackbone`. Masa: `baseCost=360, costStep=110, maxLevel=2`. Hangar/Truck: `baseCost=300, costStep=400, maxLevel=2`. (Rapor §2.)

- [ ] **Step 2: 16 perki ekle**

Her biri için `UpgradeDefinition`: displayName, loc key'ler, `kind=Perk`, `tier` (rapor §3: T1/T2/T3), `effectId` (Task 7 anahtarları), `baseCost`/`costStep`/`maxLevel` (rapor §3: Ucuz Kira base=130/step=30/max=3; Prestij Simsarı iki ayrı fiyat 510/505 → base=510/step=-5; Prestij Ustası base=280/step=100/max=2; tek-seferlik relic'ler max=1). Görsel `levelObjects` gerekmeyen perkler için boş bırak.

- [ ] **Step 3: Görev Tier'ı flag'le**

Görev Tier girişinde `requiresQuestSystem=true`. `_questSystemActive` default `false` → havuza girmez (rapor §6). Reaktivasyon: server'da `_questSystemActive.Value=true`.

- [ ] **Step 4: Reroll butonu + kaldırılan upgrade'leri temizle**

Panel prefabına "Yenile" butonu + fiyat text bağla (Task 6 SerializeField'ları). Kaldırılan eski upgrade'leri (Money, Water) listeden çıkar; Stamina/Queue/Customer artık perk kimlikleriyle (Enerjik Ekip/Uzun Kuyruk/Sabırlı Müşteriler) yeniden temsil edildiğinden eski girişleri kaldır.

- [ ] **Step 5: Play doğrulaması**

Yeni oyun → gün 1-4 sadece T1+omurga çıkıyor; gün 5'te T2 açılıyor; gün 9'da T3 (Ucuz Kira dahil) açılıyor. Fiyatlar rapordaki tabloyla eşleşiyor. Görev Tier hiç çıkmıyor.

- [ ] **Step 6: Commit**

```bash
git add "Assets/Scenes/The Main Office.unity" Assets/**/Localization*
git commit -m "data: wire 16 perks + backbone prices + tier gating + reroll UI (report v3.2)"
```

---

## Task 9: QA senaryoları + ölü kod kapatma + prefab override teyidi

**Files:**
- Verify: `Assets/NewCss/UpgradeScripts/UpgradeAssets.cs`, `ItemType.cs`, `UpgradeManager.cs` (ölü kod — Yol B)
- Verify: `Truck_Anim (2).prefab:972` (rewardPerBox override)

- [ ] **Step 1: Ölü kod teyidi**

`UpgradeAssets.GetCost()`, `UpgradeManager` çağrılıyor mu? (`Grep` `.Buy(`, `GetCost(`). Çağrılmıyorsa roguelite'ta tamamen devre dışı; `MoreCapacity_4+` bedava bug'ı artık erişilemez. Bir güvenlik notu/`[Obsolete]` bırak.

- [ ] **Step 2: Prefab override zinciri**

`Truck_Anim (2).prefab` `rewardPerBox:20` serialize edilmiş; `Truck.cs:212` runtime'da `economySettings`'ten üzerine yazıyor mu doğrula. Yazıyorsa sorun yok; yazmıyorsa prefab değerini 50'ye düzelt.

- [ ] **Step 3: Senaryo testleri (qa)**

Rapor §8.2/§9: (a) Ucuz Kira + Kaldıraçlı Kira aynı draft'ta → kira %32-37 iniyor, prestij cezası ×2 hissediliyor mu. (b) Kelle Koltukta + Acil Fren kombinasyonu. (c) 1P düşük gelirde "hepsini alamama" (kasıtlı). (d) Reroll sonrası senkron (host+client aynı 3 kart). (e) Satın alma → ertesi gün aktifleşme + max'a ulaşan perkin havuzdan düşmesi.

- [ ] **Step 4: kontrol kapısı**

Tüm task'ların çıktısı **kontrol** denetiminden ONAY alır. DÜZELTME GEREKLİ → ilgili task'a geri döner.

---

## Self-Review

**Spec coverage:** Bölüm 1 (draft mekaniği)→Task 4-6; Bölüm 2 (16 perk + tier)→Task 1,2,7,8; Bölüm 3 (birleşik yapı: omurga korunur, statlar perk'e)→Task 8; Bölüm 4 (sabit fiyat + kontrol'ün 2 bulgusu)→Task 0 (bonusPerTier), Task 8 (Raf doğrusal dizi, kod-uyumlu fiyat); Bölüm 5 (veri-güdümlü mimari)→Task 1,7. Görev Tier flag→Task 7,8. Reroll→Task 3,6. **Tüm spec bölümleri kapsandı.**

**Placeholder scan:** Risk perkleri (Task 7 Step 3-4) bazı kaldıraçların gerçek alan adlarının Unity'de doğrulanmasını gerektiriyor (Volatilite per-delivery RNG, Acil Fren iflas bayrağı, Phone/patience alanları) — bunlar "TODO" değil, mevcut kod API'sine bağlanacak bilinen dokunuşlar; ekonomik büyüklükler rapordan sabit, uydurma yok. gameplay bunları qa ile netleştirir; sapma çıkarsa (yeni ekonomik değer gerekirse) economist'e döner.

**Type consistency:** `DraftPool.IsEligible/SelectOffer/MaxUnlockedTier`, `RerollCurve.CostForReroll`, `PerkTier/PerkKind`, `PerkEffect.Apply(string,int,PerkContext)`, `PerkContext` alanları task'lar arası tutarlı. `_dailyOffer`/`_rerollCountToday`/`_questSystemActive` Task 4'te tanımlanıp 5/6'da tüketiliyor.

---

## Execution Handoff

Plan `docs/superpowers/plans/2026-07-08-roguelite-upgrade-draft.md`'ye kaydedildi. Cargor iş akışında uygulama **gameplay** departmanına, denetim **qa** + **kontrol**'e ait. İki yürütme seçeneği aşağıda (müdür kullanıcıya sunar).
