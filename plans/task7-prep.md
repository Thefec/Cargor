# 🧩 Task 7 Prep — 16-Perk Effect Registry (uygulamadan önce hazır brief)

> **Amaç:** Sıfırlama sonrası gameplay'e verilecek Task 7 delegasyonunu kısaltmak. Tüm alan adları koddan **doğrulandı**, tüm değerler `UPGRADE_PRICING_REPORT.md` v3.2'den **verbatim**. Bu doküman hazır olduğu için gameplay sıfırdan türetme yapmayacak.
> **Kaynak:** rapor §3 (fiyat/tier), §4 (risk trade-off büyüklükleri). Uygulama planı Task 7: `docs/superpowers/plans/2026-07-08-roguelite-upgrade-draft.md`.
> **Durum:** kod YAZILMADI — hazırlık. İş: `PerkEffect.cs` (Assembly-CSharp) + `UpgradePanel.ApplyUpgradeEffect` delegasyonu + 4 gerçek kod dokunuşu.

---

## Hook noktası (doğrulandı)
`UpgradePanel.cs:600` `ApplyUpgradeEffect(EntryUI entry, int level)` şu an `switch(upgradeName)` ile eski 4 upgrade'e (QUEUE/STAMINA/MONEY/QUEST_TIER) dağıtıyor. **Task 7:** metodun başına:
```csharp
string effectId = entry.Definition.effectId;
if (!string.IsNullOrEmpty(effectId)) { PerkEffect.Apply(effectId, level, BuildPerkContext()); return; }
// ...mevcut switch (geriye uyum) aynen kalır
```
`BuildPerkContext()` → mevcut alanlardan doldur: `Truck`, `CustomerManager`, `PlayerMovement` UpgradePanel'de zaten var (switch metodları kullanıyor: `ApplyQueueUpgrade`→CustomerManager, `ApplyStaminaUpgrade`→PlayerMovement, `ApplyMoneyUpgrade`→Truck).
⚠️ **Çözülecek:** `GameEconomySettings` referansı UpgradePanel'de doğrudan yok. Perk'lerin çoğu buna dokunuyor. Kaynak: `Truck.economySettings` (Truck.cs:213 `economySettings.penaltyPerBox` okuyor) üzerinden eriş veya sahnedeki tekil SO'yu bağla. gameplay ilk adımda bunu netleştirsin.

## Level semantiği
- **Relic (tek-seferlik) perk:** `maxLevel=1`; effect `level>=1` ise uygula, `level<=0` no-op.
- **Seviyeli perk:** `level` 1..maxLevel; formül `level` ile ölçekler.

---

## 16 perk — effectId → alan → değer (hepsi doğrulandı)

### Basit kaldıraç (level-lineer / relic) — ekstra kod dokunuşu YOK
| # | Perk | effectId | Tier | Fiyat | maxLvl | Alan (doğrulandı) | Formül (rapordan) |
|---|---|---|---|---|---|---|---|
| 2 | Ucuz Kira | `cheap_rent` | T3 | base130/step30 | 3 | `GameEconomySettings.rentGrowthMultiplier` (=1.15f) | `1.15f - 0.03f*level` (Lv1→1.12, Lv2→1.09, Lv3→1.06) |
| 15 | Prestij Simsarı | `prestige_broker` | T3 | [510,505] | 2 | `Truck.bonusPerTier` (float, Task 0) | `5f + 0.5f*level` (Lv1→5.5, Lv2→6) |
| 4 | Prestij Ustası | `prestige_master` | T2 | base280/step100 | 2 | `GameEconomySettings.customerServedPrestigeBonus` (=0.5f) | `0.5f + 0.15f*level` (Lv1→0.65, Lv2→0.8) |
| 1 | Hızlı Hangar | `fast_hangar` | T2 | 280 | 1 | `Truck.hangarStayDuration` (=120f) | relic: `120f*1.30f`=156 |
| 6 | Enerjik Ekip | `energetic_crew` | T1 | 160 | 1 | `PlayerMovement.staminaRegenRate` (=1f) | relic: `+= 1.5f` *(soyut — bkz. NOT)* |
| 7 | Çevik Ekip | `agile_crew` | T1 | 180 | 1 | `PlayerMovement.moveSpeed` (=5f) | relic: `*= 1.15f` *(soyut — bkz. NOT)* |
| 8 | Sabırlı Müşteriler | `patient_customers` | T1 | 220 | 1 | `CustomerAI.minWaitTime`/`maxWaitTime` (10/20) | relic: `*=1.25f` *(bkz. DOKUNUŞ-5)* |
| 9 | Uzun Kuyruk | `long_queue` | T1 | 240 | 1 | `CustomerManager.maxQueueSize` | relic: `+= 2` |
| 10 | Kumarbaz Kasası | `gambler_case` | T2 | 220 | 1 | `GameEconomySettings.rewardPerBox`+`penaltyPerBox` | relic: reward `*1.30`, penalty `*1.55` (RoundToInt) |
| 13 | Kelle Koltukta | `all_in` | T3 | 800 | 1 | `GameEconomySettings.rewardPerBox`+`gracePaymentPercent` | relic: reward `*1.25`, `gracePaymentPercent=0f` |
| 3 | Telefon Hattı | `phone_line` | T1 | 160 | 1 | `GameEconomySettings.maxCallsPerHour`(=2)/`callReward`(=10) | relic: `maxCallsPerHour += 1` *(soyut — bkz. NOT)* |

### Gerçek kod dokunuşu GEREKTİRENLER (4-5 kalem — planla, qa ile netleş)
| # | Perk | effectId | Tier | Fiyat | Dokunuş |
|---|---|---|---|---|---|
| 11 | Kaldıraçlı Kira | `leveraged_rent` | T3 | 350 | **DOKUNUŞ-1:** `GameEconomySettings`'e `public float rentScaledMultiplier = 1f;` ekle; `CalculateRent` (GameEconomySettings.cs:112 `scaledRent = baseRent*Pow(...)`) → `scaledRent *= rentScaledMultiplier`. Effect: `rentScaledMultiplier=0.8f` (−%20, yalnız scaledRent, wealthTax'e dokunma — rapor §4.3) + `customerLostPrestigePenalty *= 2f` (=−1.5→−3.0). |
| 12 | Yüksek Volatilite | `high_volatility` | T2 | 300 | **DOKUNUŞ-2:** per-delivery ±%35 RNG + ort. +%15. `GameEconomySettings`'e `rewardVolatility=0.35f` + `rewardVolatilityMean=1.15f` bayrağı; kutu ödülü dağıtıldığı yerde (Truck ödül kodu) okunup uygulanır. EV her zaman pozitif (rapor §4.2). |
| 14 | Acil Fren | `emergency_brake` | T2 | 250 | **DOKUNUŞ-3:** iflası 1 kez önleyen tek-kullanımlık bayrak. Rent/game-over zincirine (GameEconomySettings.cs:219 grace / GameStateManager lose paneli) `bool insuranceAvailable` ekle; tetiklenince o gün geliri 0 + prestij −5, bayrak tükenir. |
| 16 | Toplu Alım | `bulk_buy` | T1 | 150 | **DOKUNUŞ-4:** `UpgradePanel`'de `_nextDraftDiscountCard` bayrağı; sonraki draft'ta 1 kartın `CalculateFinalCost`'una −%50. |
| — | *(Sabırlı Müşteriler)* | *(patient_customers)* | — | — | **DOKUNUŞ-5:** `minWaitTime`/`maxWaitTime` per-customer-instance (CustomerAI prefab alanı), global tek nokta yok. Ya spawn'da okunan bir `CustomerManager` çarpanı ekle, ya prefab default'unu runtime güncelle. gameplay mekaniği netleştirsin. |

---

## NOT — soyut büyüklükler (economist onayı GEREKMEZ)
Rapor §3: Çevik Ekip (hız), Sabırlı (sabır), Mesai, Telefon, Enerjik — bunların **fiyatı tier bandından sabit**; büyüklük "mevcut mekaniğe bağlanış", yeni ekonomik değer değil. gameplay makul bir sayı seçer (yukarıdaki formüller öneri). **Mesai Saati** (`overtime`, T1, 200 TL): `DayCycleManager.endHour`(=18) veya `realDurationInSeconds`(=160) hafif artır — relic; effectId'yi tabloya ekle, DayCycle dokunuşu küçük.

## Uygulama sırası önerisi (Task 7 alt-adımları)
1. `PerkEffect.cs` iskelet + `PerkContext` + `ApplyUpgradeEffect` delegasyonu + `BuildPerkContext` (GameEconomySettings kaynağını çöz).
2. Basit kaldıraç 11 perk (yukarıdaki ilk tablo) — tek commit, düşük risk.
3. DOKUNUŞ-1 (rentScaledMultiplier) + DOKUNUŞ-4 (bulk_buy) — izole, test edilebilir.
4. DOKUNUŞ-2 (volatilite RNG) + DOKUNUŞ-3 (acil fren) — en riskli, ayrı ele al, qa senaryo testi.
5. DOKUNUŞ-5 (patience mekaniği) netleştir.
6. qa → kontrol (whole-branch değil, Task 7 için ayrı kapı — ama dal-sonu toplu ONAY da olabilir, CLAUDE.md kural 4).

> Değerler bu dokümanda sabit; gameplay UYDURMASIN. Sapma gerekirse (yeni ekonomik değer) → economist.
