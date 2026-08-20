---
name: rent-growth-1-35-deficit-2026-08-20
description: rentGrowthMultiplier=1.35 sim-resync sonrasi 4P/STRICT gun16 iflasina yol aciyor; kok neden dogrulandi, 1.20 onerisi sim ile test edildi
metadata:
  type: project
---

## Bulgu (2026-08-20, sim.js v3.2 resync sonrasi ikinci tur)

`node tools/economy-sim/sim.js` tablo 7/8: STRICT-Normal bantta 4P tam GUN 16'da (son kira odemesinde) iflas ediyor; SLOW senaryoda optimistic bantta 2P/3P/4P GUN 16'da, strict bantta ise cok daha erken (gun 8-12) iflas ediyor.

**Kok neden dogrulandi (pencere analizi, kira / son-4-gunluk-net oran):** STRICT-Normal bantta bu oran OYUN BOYUNCA MONOTONIK ARTIYOR — 1P 0.85->1.45, 2P 0.97->1.04, 3P 1.10->1.13, 4P 1.20->1.22 (gun4->gun16). Yani `rentGrowthMultiplier=1.35` bilesik buyumesi, STRICT bantta gelirin buyume hizini yapisal olarak asiyor; 4P zaten gun4'ten itibaren su altinda (oran 1.20) ve gun16'da biriken kasa tamponu tukeniyor (cashBeforeRent 4223 TL, gereken kira 4429 TL — sadece 206 TL / %4.6 kisa, ama grace zaten gun12'de harcanmisti).

**rentGrowthMultiplier'in TEK BASINA yeterliligi test edildi (SRC objesi uzerinde parametrik tarama):**
- g=1.35 (mevcut): Normal bantta yalniz strict-4P@gun16 batar
- g=1.30: Normal bantta SIFIR iflas (tum P, tum bant)
- g=1.25: Normal bantta sifir iflas + Slow-optimistic'te de sifir iflas AMA Slow-optimistic-2P kasa marji sadece 52 TL (bicak sirti)
- **g=1.20: Normal-strict tum P finalCash >= 534, Slow-optimistic tum P finalCash >= 358 — saglikli marj, tavsiye edilen deger**
- g<=1.0 dahi: SLOW-STRICT bantta (1P gun11, 2P gun12, 3P/4P gun8) iflas DEVAM EDIYOR — bu senaryo buyume egrisinden BAGIMSIZ, cunku gun4'teki (cycle 0, g^0=1) taban kira bile bu bandin gelirini asiyor (oran gun4'te 1.57-2.18). Yani SLOW-STRICT (hem yavas uretim HEM tir penceresine on-stoksuz girme) `baseRentByPlayerCount`'u ~%50+ kesmeden duzelmiyor — bu da diger butun bantlari (ozellikle optimistic) anlamsizca kolaylastirir. **Bu persona (yavas+stratejisiz) kasitli "en kotu durum" olarak birakilmasi onerilir**, ayri bir "ilk kira gunu indirimi" gibi onboarding mekanigi istenirse ayri incelenmeli — tek sabit degisikligiyle cozulmez.

## Karar / Oneri
`rentGrowthMultiplier` 1.35 -> **1.20** (`Assets/Resources/EkonomiAyarlari.asset:16`, `GameEconomySettings.cs:24`, sim `SRC.rentGrowthMultiplier`). `baseRentByPlayerCount` ve `gracePaymentPercent` DEGISMEDI — tek lever yeterli.

## Neden bu deger onaylandi
[[rent_death_spiral]] notundaki 1.35 karari FAZ2'de (eski gun suresi + tahmini prestij tabani ile) verilmisti, FAZ3/4 rescale'lerinden (moneyMult, lost cezasi, prestij 0-100, cargo degerleri) sonra hic yeniden sim edilmemisti. `plans/devam.md` 2026-08-19 uyarisi ("FAZ4'un kira egrisi karari hic dogru sim ile test edilmemis olabilir") **DOGRULANDI** — 1.35, FAZ4-sonrasi gercek gelir egrisine gore fazla dikti.

## How to apply
Playtest'te 4P'nin gun13-16 nakit akisi hala izlenmeli (g=1.20'de bile 1P-strict oran gun16'da 1.02 ile en dar marja sahip — cozum degil, tasarim gercekliligi: solo oyun ekonomi-olcegi kazanmiyor). SLOW-STRICT bantini "kazanilabilir" yapmaya calisirken `rentGrowthMultiplier`'a dokunma — ispatlandi ki isliyor degil.
