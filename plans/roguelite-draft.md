# 🎲 Roguelite Upgrade Draft Sistemi

> **Durum**: 🚧 UYGULAMA DEVAM EDİYOR (subagent-driven) — 2026-07-08, 2. oturum sonu
> **Branch**: `feature/roguelite-upgrade-draft` (base `1f40742`)
> **İlgili**: [../PLAN.md](../PLAN.md) · [economy-balance.md](economy-balance.md)

Kullanıcı isteği: mağazayı "tüm upgrade'leri listele" düzeninden **gün sonu 3 rastgele kart** draft'ına çevir + yeni perk'ler ekleyerek çeşitlilik/kaos kat.

- **Tasarım dosyası:** `docs/superpowers/specs/2026-07-08-roguelite-upgrade-draft-design.md`
- **Uygulama planı:** `docs/superpowers/plans/2026-07-08-roguelite-upgrade-draft.md`
- **Fiyat/tier/etki kaynağı:** `UPGRADE_PRICING_REPORT.md` v3.2 (kontrol ONAY'lı, genel toplam 9945 TL)
- **Ledger:** `.superpowers/sdd/progress.md`

---

## Onaylanan tasarım özeti

✅ **TASARIM TAMAM (2026-07-08).** Bölüm 1 (mekanik) + 2 (16-perk roster & tier) + 3 (birleşik ekonomi) + 4 (sabit fiyat) + 5 (veri-güdümlü mimari) onaylandı.

- **Draft:** gün sonu masa trigger'ında panel açılır; içerik 3 kart; server-authoritative senkron; parası yeten hepsini alır; ertesi gün aktif; reroll (artan fiyat); RNG **kilidi açık tier içinde**.
- **Birleşik yapı:** fiziksel omurga KALIR (Raf 10, Masa 2, Hangar 2, Görev Tier 2 — kullanıcı Quest'i korumak istedi ama sistem pasif); soyut statlar (Stamina, rewardPerBox, Kuyruk, Sabır, Su) KALDIRILDI → yerine **16 yeni perk** (9 güvenli / 5 risk-trade-off / 2 sinerji). Su tamamen silindi.
- **Tier + kilit:** perk'ler T1/T2/T3; **T2 gün≥5; T3 gün≥9** (sadece gün-bazlı — storeLevel/prestij OR koşulları kaldırıldı).
- **Fiyat:** sabit (yüzde değil), tier bandına göre. **Mimari:** veri-güdümlü, kolay genişleyen (v1=20 kart havuzu, hedef 25-30).

## Ekonomi kilidi

✅ **EKONOMİ KİLİTLENDİ (2026-07-08):** economist v3.2 → kontrol **ONAY** (3 turda: tur1 7 bulgu, tur2 Prestij Simsarı, tur3 ONAY). Tam tablo `UPGRADE_PRICING_REPORT.md` v3.2. Özet:
- **Omurga:** Raf 200→290 (doğrusal, 10sv, top 2450), Masa 360/470, Hangar 300/700. Görev Tier feature-flag ile havuz dışı (sistem pasif).
- **16 perk:** T1 (150-240), T2 (220-380), T3 (Ucuz Kira 130/160/190, Kaldıraçlı Kira 350, Kelle Koltukta 800, Prestij Simsarı 510/505). Genel toplam 9945 TL.
- **Reroll:** 50/90/160/290/525 (×1.8, günlük sıfırlanır).
- **Bütçe fizibilitesi:** 1P düşük gelir "hepsini al" kasıtlı imkansız (seçim zorunlu); 2P sıkı; orta-yüksek mümkün.

**⚠️ Thread A (Faz 2 fiyat raporu) buraya SOĞURULDU:** Stamina/Money/Queue kaldırıldığı için ayrıca düzeltilmedi; economist 4 omurga + 16 perk + reroll'u tek seferde sıfırdan fiyatladı.

---

## Uygulama durumu

✅ **UYGULAMA PLANI HAZIR (2026-07-08):** 10 task (Task 0-9). Mevcut UpgradePanel mimarisi (NetworkList seviyeler, `_pendingUpgrades` ertesi-gün-aktif, CalculateFinalCost) korunuyor; üstüne draft+tier+reroll katmanı biniyor.

**Yürütme modeli:** subagent-driven. Her task'ı **gameplay** uygular, müdür (controller) diff'i doğrular; **son kontrol whole-branch ONAY kapısı** en sonda.

### ✅ Tamamlanan task'lar (hepsi commit'li, controller diff-doğrulamalı)
| Task | Commit | İş |
|---|---|---|
| 0 | `d5d010f` | `bonusPerTier` int→float + Mathf.RoundToInt (Truck.cs, GameEconomySettings.cs) |
| 1 | `847908e` | `PerkTier`/`PerkKind` enum'ları + `UpgradeDefinition`'a kind/tier/effectId/requiresQuestSystem |
| 2 | `60db470` | `DraftPool.cs` (tier+max filtresi, 3-kart seçim) + izole `NewCss.Roguelite.asmdef` + EditMode testleri; PerkTier.cs asmdef'e taşındı |
| 3 | `5cc6675` | `RerollCurve.cs` (50/90/160/290/525, 5+ tavan) → `NewCss.Roguelite` asmdef + EditMode testi. Meta'yı Unity üretti. **Plan sapması:** dosya `UpgradeScripts/` yerine `Roguelite/`'a kondu (test asmdef `overrideReferences` yüzünden aksi halde göremezdi). |
| 4 | `b59ccde` | `_dailyOffer` NetworkList + `_rerollCountToday`/`_questSystemActive` NetworkVariable + `GenerateDailyOfferServer` (server-only, gün-seed'li RNG, tier+max+quest eligibility) + `HandleDailyOfferChanged`→`RebuildDraftEntries` stub. OnNetworkSpawn(server)+HandleNewDay entegre. Batchmode derleme temiz, 6/6. **✅ Play doğrulandı: teklif `[2,5,7]` 3 index üretti.** Debug log commit `9b2b8c7` (Task 6/7'de kaldırılacak). |
| 5 | `d8b33ff` | `RebuildDraftEntries` artık sadece `_dailyOffer`'daki (≤3) kartı kurar (mevcut `BuildSingleEntry`; `EntryUI.UpgradeIndex` = gerçek index → satın alma zinciri korunur). OnNetworkSpawn + panel-open `BuildEntries` yerine bunu çağırır. **✅ Local Play doğrulandı: panel 3 kart gösteriyor, error yok.** |
| 6 | `b458a9d` | Reroll butonu: `OnReroll`+`RerollServerRpc` (server-auth, `RerollCurve` 50/90/160/290/525, seed'e reroll-count XOR'lu, `_rerollCountToday` günlük 0'lanır), `rerollButton`/`rerollCostText` SerializeField, `RefreshRerollUI` (sayaç+para+draft tetikli, null-guard). `BuildEligibility(int,out PerkTier)` ortak helper'a çıkarıldı. Task 4 debug log borcu silindi. **qa:** 1 önemli (listener birikmesi→`OnNetworkDespawn` RemoveListener) + panel-açık server guard düzeltildi. **kontrol: ONAY.** Play/multiplayer teyidi manuel. |

### Mimari karar (yeni oturum bilmeli)
Saf-mantık dosyaları (`PerkTier`, `DraftPool`, + Task 3'te `RerollCurve`) `Assets/NewCss/Roguelite/` altında **izole `NewCss.Roguelite` asmdef**'inde (autoReferenced=true → Assembly-CSharp/UpgradePanel otomatik görür). Test asmdef'i (`Assets/Tests/EditMode/Cargor.Tests.EditMode.asmdef`) bunu referanslar. `PerkEffect.cs` (Task 7) Assembly-CSharp'ta kalır (Truck/CustomerManager'a bağımlı).

### ✅ KRİTİK RİSK KAPANDI — Unity teyidi geçti (2026-07-08, 3. oturum)
Unity 6000.4.3f1 batchmode (`-runTests -testPlatform EditMode`) ile doğrulandı:
- **Derleme yeşil:** hiç `error CS` yok (Assembly-CSharp'taki Task 0-1 değişiklikleri dahil). Log'daki uyarılar sadece projede önceden var olan NGO `CS0618`/`CS0114` — bizim kodla ilgisiz.
- **Meta/GUID temiz:** çakışma yok; elle üretilen asmdef zinciri çözüldü (`Cargor.Tests.EditMode.dll` derlenip koştu).
- **DraftPoolTests 4/4 geçti** (MaxUnlockedTier, IsEligible, SelectOffer×2).

### ⚠️ gameplay'e taşınan 2 uygulama kalemi (kontrol notu)
1. `bonusPerTier` kodda `int` idi (`Truck.cs:105`, `GameEconomySettings.cs:54`) — Prestij Simsarı 5.5/6 için int→float + yuvarlama gerekti. **Task 0'da yapıldı.**
2. `Truck_Anim (2).prefab:972` rewardPerBox:20 override — QA prefab/asset override zincirini teyit etmeli (projenin bilinen tuzağı).

---

## ⏭️ Sıradaki (buradan devam)
1. ✅ **Unity teyidi geçti** — Task 0-2 derlendi ve testler geçti (batchmode EditMode 4/4).
2. ✅ **Task 3 bitti** (`5cc6675`) — RerollCurve + testi, batchmode 6/6.
3. ✅ **Task 4 bitti** (`b59ccde`) — server-auth `_dailyOffer` üretimi, Play-doğrulandı (`[2,5,7]`). Geçici debug log `9b2b8c7` (Task 6/7'de kaldır).
4. ✅ **Task 5 bitti** (`d8b33ff`) — panel 3-kart draft (`RebuildDraftEntries` gerçek görünüm), local Play-doğrulandı (3 kart geldi).
5. ✅ **Task 6 bitti** (`b458a9d`) — reroll butonu, kontrol ONAY. **Task 7 (aktif):** 16-perk effect registry (`PerkEffect.cs`, Assembly-CSharp). **Task 8:** Inspector/sahne veri girişi (fiyatlar v3.2'den). **Task 9:** qa + ölü kod + prefab override.
5. **Son:** kontrol whole-branch ONAY → Unity 1/2/4 kişi test.

> **Not (Task 4+):** Bu task'lar NetworkList/NetworkBehaviour + sahne/prefab içeriyor — saf EditMode ile tam doğrulanamaz; PlayMode veya gerçek Unity oturumu gerekir. Batchmode EditMode sadece derleme + saf-mantık testlerini kapsar.

> Task brief'leri önceki oturumun scratchpad'indeydi (devretmez) — plandan yeniden üretilir.
