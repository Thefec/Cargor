---
name: sim-resync-2026-08-19
description: sim.js v3.2 resync -- FAZ4 sonrasi asset/scene ile hic senkron edilmemis 11+ sabit duzeltildi, iflas paterni degisti
metadata:
  type: project
---

`tools/economy-sim/sim.js` SRC basligi FAZ4 (2026-07-30) sonrasi hic resync edilmemisti. Dal `fix/sim-resync`'te (main'den, henuz merge/commit edilmedi -- commit'i kullanici/kontrol yapacak) 2026-08-19'da duzeltildi. Tek kaynak sirasi: `Assets/Resources/EkonomiAyarlari.asset` (SO gercek serialize deger) -> `GameEconomySettings.cs` -> sahne `The Main Office.unity`.

**Duzeltilen sabitler (deger degisenler):**
| Sabit | Eski (sim) | Yeni (canli) |
|---|---|---|
| baseRentByPlayerCount | [500,900,1200,1500] | [500,1000,1450,1800] |
| rentGrowthMultiplier | 1.15 | 1.35 |
| hangarStayByPlayerCount | [90,60,40,30] | [120,60,40,30] |
| prestigePerBonus | 4 | 8 |
| phoneRingEventMultiplier | 1.5 | 2.0 |
| callPrestigeReward | 0.2 | 0.4 |
| customerLostPrestigePenalty | -0.6 | -0.4 |
| customerServedPrestigeBonus | 0.2 | 0.4 |
| wrongProductPrestigePenalty | -0.04 | -0.08 |
| boxDropPrestigePenalty | -0.02 | -0.04 |
| wrongDeliveryPrestigePenalty | -0.08 | -0.16 |
| startingPrestige | 6 | 12 |
| phoneRingDuration | 25 | 15 |
| moneyMultiplierPerPlayer | 1.0 | 1.2 |
| moneySystemSceneStartingMoney | 50000 (debug) | 500 (debug artik yok, duzeltilmis) |
| upgradeCostMultiplierPerPlayer | 1.15 (skaler) | [1.00,2.00,2.95,3.70] (P-dizisi, kullanilmiyor) |

**MODEL GAP (sabit degil, dokunulmadi -- bkz [[quest_d2_double_scaling_bug_2026-08-06]] tarzi bir "wiring eksik" durumu):**
- Tir kargosu artik P-bazli (`truckCargoMinByPlayerCount`/`MaxExclusive` asset'te var: 1P{1,3) ort 1.5, 2P{2,4) ort 3, 3P{2,5) ort 3.5, 4P{2,6) ort 4) ama sim hala TUM P'ler icin flat {2,3,4,5} ort 3.5 kullaniyor (`truckThroughput`'ta `cargoValues` param VAR ama call site hicbir yerde P-bazli doldurmuyor). 1P icin sim gelirini ~2.3x FAZLA tahmin ediyor olabilir.
- Telefon çalma sansı da artik P-bazli (`phoneRingChanceByPlayerCount` cs default {0.20,0.25,0.30,0.35}, asset'te override YOK) ama `phoneIncome()` playerCount almiyor, hep tek skaler kullaniyor (0.30->0.20 duzeltildi ama hala flat).
- Perkler: sim hic perk modellemiyor (rentScaledMultiplier=1, rewardVolatility=0 "perk yoksa" varsayimi zaten dogru taban). 2026-08-19 perk canlanmasi (`d122e4c`) bu nedenle sim'i etkilemiyor -- kontrol edildi, degisiklik gerekmedi.

**Onemli SONUC (resync ONCESI/SONRASI davranis degisikligi -- FAZ4 kararlari bu yeni haliyle DOGRULANMAMIS):**
- STRICT bant, TABAN KOSU: ONCESI 1P GUN 3'te iflas ediyordu, 2P/3P/4P hayatta kaliyordu. SONRASI 1P artik hayatta kaliyor AMA 4P GUN 16'da (son kira odemesinde) iflas ediyor -- yeni bir basarisizlik modu.
- YAVAS senaryo optimistic bantta: ONCESI hepsi hayatta; SONRASI 2P/3P/4P hepsi GUN 16'da iflas ediyor.
- Kok neden: `rentGrowthMultiplier` 1.15->1.35 gun16'da (4. kira dongusu, cycle=3) kumulatif carpani 1.52x'ten 2.46x'e cikariyor -- kira son cyclede cok sert sicriyor. `baseRentByPlayerCount` da 2P-4P icin ~%11-20 yukselmis. Bu ikisi net gelirdeki iyilesmeleri (prestij/serve bonus artislari, moneyMult 1.2) gec-oyunda eziyor.
- **Uyari**: FAZ4 (bkz [[faz4_final_value_set_2026-07-30]]) kira {500,1000,1450,1800}+g1.35 kararini zaten VERMISTI (asset'te dogru), ama o karar sim ile HIC dogrulanmamis -- sim o zaman hala eski degerleri tasiyordu. Yani "gun16 4P iflas" riski FAZ4 kararindan beri var olabilir, hic playtest/sim ile gorulmemis. **Playtest'te ozellikle 4P grubunun gun 13-16 arasi nakit akisi izlenmeli.**

Ilgili: [[faz4_final_value_set_2026-07-30]], [[rent_death_spiral]], [[sim_v31_table_contention]]
