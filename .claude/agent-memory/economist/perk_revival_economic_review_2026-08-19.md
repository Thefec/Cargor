---
name: perk-revival-economic-review-2026-08-19
description: 6 ölü perk canlanma etkisi — 5/6 fiyat zaten FAZ4-kalibreli, prestige_broker kod/plan çelişkisi, gambler_case+high_volatility stacking riski
metadata:
  type: project
---

**Bağlam:** `plans/perk-revival.md` — 6 perk (`gambler_case`, `all_in`, `prestige_broker`,
`fast_hangar`, `agile_crew`, `energetic_crew`) prefab'a yazıp canlı instance'a ulaşmadığı için
fiilen ölüydü, gameplay tarafı canlandırıyor (dal `feature/perk-revival`). Bu not canlanma
ÖNCESİ ekonomik inceleme turudur.

## 1. Fiyatlar zaten FAZ4-kalibreli (5/6) — DEĞİŞİKLİK GEREKMİYOR

`Assets/Scenes/The Main Office.unity` içindeki fiyatlar `f38d523f` (2026-08-06,
"balance(scene): FAZ4 sahne değerleri") ile `plans/economy-rebuild-2026-07-30-faz4-final.md`
§B.7'deki hedeflere ÇEKİLMİŞ, v3.2/upgrade-roi-2026-07-20 raporlarının ESKİ sayıları DEĞİL:

| Perk | v3.2 rapor (2026-07-08, bayat) | FAZ4 §B.7 hedef = CANLI sahne |
|---|---|---|
| `fast_hangar` | 280 | **120** |
| `energetic_crew` | 160 | **100** |
| `agile_crew` | 180 | **180 (aynı)** |
| `gambler_case` | 220 (→400 ara adım) | **350** |
| `all_in` (Kelle Koltukta) | 800 | **320** |

Etkiler (PerkEffect.cs) bu 5 perk için FAZ4 B.7 ile UYUMLU — kod değişikliği gerekmiyor.
Canlandırma sonrası ekonomik davranış FAZ4'ün zaten öngördüğü ROI bandında olmalı.

## 2. `prestige_broker` — KOD/PLAN ÇELİŞKİSİ (tek gerçek uyuşmazlık)

FAZ4 §B.7 satırı: `130 / +15 / +1.0/lvl` (fiyat 510+505=1015→130+145=275 TL'ye düşürülürken
etkinin bonusPerTier deltası **ikiye katlanması** (+0.5/lvl → +1.0/lvl, yani 5→6→7) gerekiyordu
— "etki 2× + fiyat 130 = anlamlı kart" gerekçesiyle. `f38d523f` yalnız SAHNE fiyatını
(`baseCost:130, costStep:15`) uyguladı; **`PerkEffect.cs:82-86` hâlâ eski etkiyi taşıyor**
(`5f + 0.5f * level` → 5→5.5→6), FAZ4'ün istediği `1.0f * level` DEĞİL.

**Sim bulgusu** (day-9 T3 gate hesaba katılmadan, üst-sınır tahmini, `tools/economy-sim/sim.js`
+ düzeltilmiş SRC): mevcut zayıf etki (5→6) bile yeni ucuz fiyatta (275 TL) değer/fiyat oranını
**1.24x(1P)–4.20x(4P)**'e çıkarıyor — FAZ4'ün başka T3 perkleri için hedeflediği ~2.5x tavanın
ÜSTÜNDE. FAZ4'ün istediği ikiye katlanmış etki (5→7) uygulanırsa oran **2.47x–8.39x**'e fırlıyor
— bu, v3.2'nin (kontrol turu 2/3) bir kez düzelttiği "Prestij Simsarı 18x patlaması" sorununu
AYNEN yeniden üretir.

**KARAR ÖNERİSİ:** `PerkEffect.cs`'i FAZ4 B.7'nin `+1.0/lvl` talimatına göre DEĞİŞTİRME —
mevcut `+0.5/lvl` etkiyi koru, sadece fiyatı (zaten uygulanmış 130/145) bırak. Days≥9 gate'i
tam modelleyen bir sim turu yapılmadan etkiyi ikiye katlamak riskli; mevcut haliyle bile üst
sınırda biraz cömert ama kabul edilebilir aralıkta (T3 kilidi + kısa kalan gün sayısı fiili
değeri aşağı çeker). Gameplay `PerkEffect.cs:82-86`'ya DOKUNMASIN.

## 3. `gambler_case` net EV — pozitif, davranışsal (RNG değil)

`ProcessWrongDelivery` (`Truck.cs:647-659`) yalnız OYUNCU yanlış renk kutu teslim ederse
tetikleniyor — RNG değil, beceri-bağımlı. EV formülü `(1-e)×50×1.30 − e×40×1.55` (python ile
doğrulandı, rapor §4.1 ile birebir eşleşti):

| Hata oranı e | EV (perksiz) | EV (gambler) | Fark |
|---|---|---|---|
| 0.12 (sim ASSUMED.Normal) | 39.2 | 49.8 | **+10.6 (+27%)** |
| 0.20 | 32.0 | 39.6 | +7.6 |
| 0.405 | — | — | **breakeven** (gambler=perksiz) |
| 0.512 | — | 0 | gambler EV=0 |

Tipik oyuncu (e≈0.12-0.20) için net pozitif, kötü oyuncu (e>%40) için negatife dönüyor —
gerçek beceri eşiği, exploit değil.

## 4. RİSK — `gambler_case` + `high_volatility` stacking (field çakışması YOK, EKONOMİK süperadditif VAR)

`UpgradePanel.cs:214-220` `EXCLUSIVE_EFFECT_GROUPS` yalnız `{gambler_case, all_in}` ve
`{leveraged_rent, all_in}`'i dışlıyor — `gambler_case`+`high_volatility` dışlanmıyor, ikisi de
tier=1 (gün≥5 açık) ve FARKLI alanlara yazıyor (`Truck.rewardPerBox/penaltyPerBox` vs
`Economy.rewardVolatility/-Mean`) → silent-overwrite YOK, ama `Truck.ApplyRewardVolatility()`
(`Truck.cs:682-692`) `CalculateRewardWithPrestige`'in ÇIKTISINI çarpıyor — yani iki perk
MULTİPLİKATİF birleşiyor (ceza tarafında sadece gambler'in +%55'i kalıyor, volatilite cezaya
dokunmuyor):

| e | gambler tek | gambler+high_volatility (ort.) | fark |
|---|---|---|---|
| 0.12 | 49.8 (+27%) | 58.3 (**+49%**) | +8.6 |
| breakeven vs perksiz | e=0.405 | **e=0.529** | risk eşiği yükseliyor (perk daha "güvenli" hale geliyor, tasarım amacına aykırı) |

İki perk ayrı ayrı fiyatlandırıldı (350+320=670 TL) ama birlikte ne fiyatlandırıldı ne test
edildi. **Öneri:** kod değişikliği DEĞİL ama playtest'te izlenmeli; eğer oyuncular sistematik
olarak ikisini birlikte alıp gambler'in "gerçek risk" karakterini boşaltıyorsa, ya
`EXCLUSIVE_EFFECT_GROUPS`'a üçüncü bir grup eklenmeli ya da `high_volatility`'nin
`rewardVolatilityMean`'i gambler ile birlikteyken düşürülmeli (playtest-bağımlı, şimdilik
bloklayıcı değil — bkz. UPGRADE_PRICING_REPORT.md §9 madde 8'in zaten işaretlediği
"her zaman iyi kart" gerilimiyle aynı aile).

## 5. `agile_crew` (+%15 hız) → kutu/dk/oyuncu etkisi (sim, üst-sınır varsayımı)

`sim.js` `boxesPerMinPerPlayer` moveSpeed'den bağımsız SABİT bir VARSAYIM (kodda zamanlı kapı
yok) — moveSpeed'in üretime 1:1 yansıdığı varsayımıyla (üst sınır) `boxesPerMinPerPlayer × 1.15`
koşuldu (düzeltilmiş SRC ile, bkz. §6):

| P | OPT delta | OPT % | STRICT delta | STRICT % |
|---|---|---|---|---|
| 1 | +819 | +13.8% | +358 | +13.0% |
| 2 | +1282 | +12.2% | +765 | +11.9% |
| 3 | +1578 | +10.9% | +849 | +10.2% |
| 4 | +1622 | +9.1% | (baseline iflas ediyor, güvenilmez) | — |

16 günlük kümülatif gelire ~%9-14 katkı — 180 TL sabit fiyatla tutarlı (FAZ4 "dokunma" kararı
teyit edildi, bkz. §1).

## 6. ⚠️ YENİ BULGU — `tools/economy-sim/sim.js` SRC başlığı STALE (FAZ4 sonrası hiç resync edilmemiş)

`sim.js` üstündeki `SRC` bloğu 2026-07-30 tarihli ama CANLI asset'ten (`EkonomiAyarlari.asset`,
2026-08-06 `f38d523f` sonrası) şu noktalarda SAPIYOR:

| Alan | sim.js (bayat) | canlı asset |
|---|---|---|
| `rentGrowthMultiplier` | 1.15 | **1.35** |
| `baseRentByPlayerCount` | [500,900,1200,1500] | **[500,1000,1450,1800]** |
| `prestigePerBonus` | 4 | **8** |
| `customerLostPrestigePenalty` | -0.6 | **-0.4** |
| `customerServedPrestigeBonus` | 0.2 | **0.4** |

Bu turda yalnız bu 5 alan geçici olarak scratchpad'te patch'lenip koşuldu (sim.js dosyasına
DOKUNULMADI — repo'daki dosya hâlâ bayat). **Sonraki herhangi bir sim.js koşusundan önce**
tam bir resync turu (muhtemelen `startingPrestige`/`rewardPerBox`/ceza sabitleri de dahil,
tam liste çıkarılmadı) gerekiyor; aksi halde mutlak TL sonuçları (yalnız % delta'lar değil)
güvenilmez. Bu görevde raporlanan sayılar bu yüzden ÇOĞUNLUKLA % delta cinsinden verildi
(mutlak TL'den daha dayanıklı).

İlgili: [[faz4_final_value_set_2026-07-30]] [[perk_card_absolute_assignment_conflict]]
[[upgrade_roi_2026-07-20]] [[roguelite_perk_pricing]]
