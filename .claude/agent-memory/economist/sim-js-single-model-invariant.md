---
name: sim-js-single-model-invariant
description: sim.js artik TEK oyun modeli tasir (SRC4 + runFullSim); 2026-08-31'de ikinci/ucuncu olu model silindi, bu tekillik korunmali
metadata:
  type: project
---

`tools/economy-sim/sim.js` **tek bir oyun gercekligi** tasir: sabitler `SRC4`,
giris noktasi `runFullSim(playerCount, opts)`. Baska giris noktasi YOK.

2026-08-31'de dosya 2297 -> 1258 satira indirildi. Silinenler: `const SRC` +
`runSim` (v3.1, PlateUp oncesi) ve `const PLATEUP` + `runSimPlateUp` (2026-08-29
oneri modeli), bunlara bagli `truckThroughput` / `customerDemand` /
`customerThroughput` / `phoneIncome` / `packingTablesForLevel` / `hangarStayFor` /
`dayDurationSec` ailesi / `CARGO_VALUES` / `PHONE_ROLLS_PER_DAY`, ve CLI
bloklari 0-17 + 21.

**Why:** iki blok da CANLI KODDA ARTIK OLMAYAN mekanikleri simule ediyordu ve
sessizce yanlis analiz uretiyordu — `SRC.baseRentByPlayerCount [500,1000,1450,1800]`
(canli [290,650,1140,1630]), V3 telefon alanlari (`phoneRingChancePerHour` vb.,
hem `.cs`'ten hem `EkonomiAyarlari.asset`'ten silinmis), kapasite tabanli musteri
talebi (`shelfMultiplier`/`levelMultiplier`/`playerCountMultCoeff`;
`CountActiveInteractables` silinmis, kota artik gun egrisinden).

**How to apply:**
- Yeni bir model kurma ihtiyaci dogarsa **eskisini ayni commit'te sil**; "eski
  modeli karsilastirma icin biraktim" bir sonraki turda yanlis blogun okunmasina
  yol aciyor. Karsilastirma degeri raporda/hafizada durur, kodda degil.
- CLI blok numaralari (18-23) **bilerek yeniden numaralandirilmadi**: Round 1-12
  raporlari "blok 18-23" diye atif yapiyor.
- `SRC4.reference` alt-objesi sim'in OKUMADIGI belge degerleridir (musteri sabri,
  olay takvimi, upgrade carpani, festival, quest kapilari). Modele girdi yapma.
- Refactor sonrasi **zorunlu kanit**: `runFullSim` matrisini (senaryo x bant x P x
  phoneUseRate) once/sonra JSON'a dok ve byte-byte esitligini dogrula. Bu turda
  168 kosum + tum turev fonksiyonlar AYNI cikti.

Gecmis analiz raporlarinin ana arsivi burada DEGIL, repo kokunde:
`.claude/agent-memory/economist/` (Round 1-12, `economy_full_balance_round*.md`).
Ilgili: [[cargor-economy-status]]
