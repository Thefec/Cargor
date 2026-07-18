---
name: prestige_cap_retune_2026-07-18
description: maxPrestige=150 hala erken doluyor (2P/3P/4P gun8-11) - 240 onerisi + node duyarlilik tablosu, playtest'e birakilan geç-oyun enflasyon tradeoffu
metadata:
  type: project
---

2026-07-18 (`plans/economy-balance-round.md`, birleşik ekonomi turu). [[prestige_cap_bug_and_fix]]'in
"3P/4P gün 9-14'te tavana çarpıyor" tespitinin **devamı** — bu turda somut yeni değer önerisi verildi.

**Kök neden değişmedi:** `customerServedPrestigeBonus` tır kapasitesinden bağımsız (`demandAdjusted`
üzerinden, [[prestige_cap_bug_and_fix]]) büyüyor, bu yüzden `playerCountMultiplierCoeff` ile
büyüyen talep prestiji de hızlandırıyor.

**Node duyarlılık tablosu (Normal senaryo, quest kapalı, `runSim` `prestigeCapHitDay`):**

| P | maxPrestige=150 (mevcut) | maxPrestige=240 (öneri) | maxPrestige=300 |
|---|---|---|---|
| 1P | hiç (organik ~190) | hiç | hiç |
| 2P | gün 11 | gün 15 | hiç (organik ~272) |
| 3P | gün 9 | gün 13 | gün 16 |
| 4P | gün 8 | gün 13 | gün 15 |

1P'nin organik tavanı ~190, 2P'nin ~272 — bu tavanların ÜSTÜNDE bir `maxPrestige` o oyuncu sayısını
hiç etkilemiyor (node ile 220/260/300 hepsinde 1P finalPrestige=189.8 sabit çıktı, doğrulandı).

**Trade-off:** `bonusPerTier=5` sabit kalırsa maxPrestige=240'ta 4P'nin maksimum kutu-başı bonusu
120 TL (taban 50'nin 3.4 katı, mevcut 150 tavanında 2.5 kat) — geç-oyun enflasyonunu büyütüyor,
iflas riski yaratmıyor (sonKasa +59-60%, sağlıklı) ama **playtest'e bağlı bir tasarım kararı**.

**Öneri: 240 (orta yol).** 280-300 "hiç kimse hiç dolmasın" hedefini tam karşılar ama enflasyonu
daha da büyütür (4P ~4x taban). Kesin sayı kullanıcı/kontrol playtest sonrası seçmeli.

**Why:** Kullanıcı "prestij pacing ↔ tır penceresi ↔ hangar" bağımlılığını BİRLİKTE (ORTAK) ele
almak istedi — bu kayıt o paketin prestij tarafını somutlaştırıyor, tır tarafı için bkz.
[[fast_hangar_perk_bug]] ve [[truck_hangar_window_cap]].

**How to apply:** maxPrestige değiştirilirse `UPGRADE_PRICING_REPORT.md` §4.6'daki (Prestij Simsarı/
Ustası) "emergent 70-130 TL/box" aralığı da yukarı kayacağı için o raporun value/price oranları
(2.48x-2.49x hedef bandı) yeniden doğrulanmalı — bu turda YAPILMADI, ayrı takip gerektirir.

İlişkili: [[prestige_cap_bug_and_fix]], [[truck_hangar_window_cap]], [[fast_hangar_perk_bug]],
[[roguelite_perk_pricing]]
