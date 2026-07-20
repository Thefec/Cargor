---
name: upgrade-legacy-backbones
description: Roguelite draft havuzunda hâlâ duran 9 eski omurga (kind:0) — bazıları bozuk/ölü/duplike; Money aktif zararlı (reward'ı 50'nin altına çeker), Customer wiring'siz ölü, Water/Quest Tier değersiz
metadata:
  type: project
---

**Kod+scene ile doğrulandı (2026-07-20 upgrade ROI turu).** Roguelite tasarım dokümanı
(bkz [[roguelite_perk_pricing]]) "yalnız 3 omurga kalsın (Storage/Table/Truck), Money/Stamina/
Queue/Water/Customer/Quest Tier kaldırılsın veya perke dönüşsün" dedi AMA
`The Main Office.unity` (UpgradePanel.upgrades listesi, ~satır 21593-22061) hâlâ 9 omurgayı
kind:0 (LeveledBackbone) olarak barındırıyor. `DraftPool.IsEligible` (DraftPool.cs:27)
kind:0'ı KOŞULSUZ eligible sayıyor → 9 eski omurga her gün 3-kartlık teklifi seyreltiyor.

**Omurga durumları (baseCost/costStep/maxLevel → toplam 1P maliyet):**
- **Storage** 50/10/9 (810) — çalışır ama gelir kaldıracı DEĞİL (üretim-bound dünyada demand asla bağlamaz); değeri prestij-hızlanması + fiziksel raf. Ucuz, zararsız.
- **Table** 100/150/3 (750) — levelObjects (fiziksel paketleme istasyonu); potansiyel üretim-hızı kaldıracı ama modellenmedi, playtest-belirsiz.
- **Truck** 200/100/2 (500) — +1 hangar. OPTIMISTIC modelde gelir ROI=**0** (üretim bağlayıcı, hangar kabul kapasitesi değil); STRICT modelde ROI **27-73x**. Değeri tamamen model-bağımlı, playtest gerekir.
- **Money** 300/100/3 (1200) — **AKTİF ZARARLI**: `ApplyMoneyUpgrade` (UpgradePanel.cs:714) `Truck.rewardPerBox = TruckValue(15) + level*10` = 25/35/45, taban 50'nin ALTINDA. Ayrıca `InitializeMoneyBaseValue` (cs:427) Money kartı teklifte ise spawn'da rewardPerBox'ı 15'e çeker. Satın alınırsa gelir DÜŞER. Eski ekonomiden (base~15) kalma. KALDIR veya effectId'ye taşı.
- **Stamina** 100/75/3 (525) — `staminaRegenRate=1+0.5*level` (L3→2.5). `energetic_crew` perki (1+1.5=2.5) ile AYNI sonuç, DUPLİKE.
- **Queue** 250/100/3 (1050) — `maxQueueSize=3+level`. `long_queue` perki (3+2=5) ile DUPLİKE/ÇAKIŞAN (ikisi de aynı alanı set eder, son uygulanan kazanır).
- **Water** 500/200/1 (500) — kozmetik, ekonomik değer 0 (kategori 7, bkz [[upgrade_pricing_framework]]).
- **Customer** 300/200/3 (1500) — `ApplyUpgradeEffect` switch'inde CASE YOK + effectId boş → hiçbir mekanik etki YOK (ÖLÜ). contentText "patience artar" der ama wiring yok. `patient_customers` perki gerçek işi yapar. 1500 TL boşa.
- **Quest Tier** 200/150/2 (550) — quest sistemi pasif, EV≈0. Roguelite dokümanı bunu havuzdan çıkardı sanılıyordu ama scene'de kind:0, requiresQuestSystem:0 → hâlâ teklif ediliyor.

**Öneri (P0/P1):** Money(zararlı)/Customer(ölü)/Water(değersiz)/Quest Tier(pasif) draft havuzundan
çıkarılmalı (ya sil ya requiresQuestSystem-benzeri flag ile gizle). Stamina↔energetic_crew ve
Queue↔long_queue duplikasyonundan biri kaldırılmalı. Aksi halde 3-kartlık draft %36+ olasılıkla
anlamsız/zararlı kart gösteriyor — "doğrulanmamış upgrade sistemi" denetim bulgusunun kökü bu.

**How to apply:** Upgrade ROI/denge analizinde önce "bu upgrade GERÇEKTEN teklif ediliyor mu ve
etkisi kablolu mu" diye scene listesi + ApplyUpgradeEffect switch + PerkEffect switch'ini kontrol et.
Fiyat türetmeden önce upgrade'in ÖLÜ/DUPLİKE/ZARARLI olmadığını doğrula.

İlişkili: [[upgrade_pricing_framework]], [[roguelite_perk_pricing]], [[upgrade_dual_system]], [[money_comes_only_from_trucks]]
