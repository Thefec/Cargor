---
name: upgrade-dual-system
description: Cargor'da iki paralel/çakışan upgrade sistemi var (UpgradePanel Inspector-driven vs ItemType/UpgradeAssets statik-cost); MoreCapacity_4+ şu an bedava
metadata:
  type: project
---

Cargor'da iki ayrı upgrade satın alma yolu tespit edildi (2026-07-07 itibarıyla):

1. **`UpgradePanel.cs`** — `UpgradeDefinition` listesi, Inspector'da veri girilir (baseCost/costStep/maxLevel kodda görünmez). Kapsadığı upgrade'ler: Kuyruk (`maxQueueSize`), Stamina (`staminaRegenRate`), Para (`rewardPerBox` +10/seviye), Tır/Hangar (`GarageDoorController[]`), Görev Tier.
2. **`ItemType.cs` + `UpgradeAssets.cs` + `UpgradeManager.cs`** — statik `switch` ile sabit fiyat. Kapsadığı upgrade'ler: `MoreCapacity_1..15` (raf sayısı, `ShelfController.cs` üzerinden), `TableSlotsIncrease_1..2` (kutulama masası, `TableController.cs`), `QueueCapacity_1..3` (kuyruk — sistem 1'deki "Kuyruk" ile **çakışıyor olabilir**).

**Kritik bug:** `UpgradeAssets.GetCost()` içinde `MoreCapacity_4` ve sonrası (Lv4-15) için fiyat tanımlı değil, `default: return 0` koluna düşüyor → **4. seviyeden sonra raflar bedava**.

**Why:** Bu, upgrade fiyatlandırma raporu hazırlarken (`UPGRADE_PRICING_REPORT.md`) koddan katalog çıkarırken bulundu; GDD.md bölüm 13 sadece Lv1-3 için formül veriyor, kodun gerçek durumunu yansıtmıyordu.

**How to apply:** Gelecekte upgrade ekonomisiyle ilgili herhangi bir analiz/fiyatlandırma yapılırken önce hangi sistemin (A: UpgradePanel, B: ItemType) canonical olduğu QA/gameplay ile teyit edilmeli; iki sistem için de aynı anda fiyat üretmek yanlış olabilir eğer biri dead code ise. `MoreCapacity_4+` fiyatsız kaldığı sürece raf ekonomisi hesapları (kapasite formülü, capture rate vb.) pratikte "bedava raf" riskiyle bozulabilir — bu düzeltilmeden final balance testleri yapılmamalı.

İlgili rapor: `UPGRADE_PRICING_REPORT.md` (proje kökü). İlişkili: [[rent_death_spiral]]
