---
name: hangar_stay_duration_per_player
description: hangarStayDuration (30s sabit) -> oyuncu sayisina gore 90/60/40/30s onerisi; STRICT modelde P1 icin buyuk kazanc kaniti, OPTIMISTIC modelde etkisiz
metadata:
  type: project
---

2026-07-20 analizi (kullanici "30s cok kisa, ozellikle 1P'de" dedi). Kanit:
`tools/economy-sim/sim.js` `truckCapStrict`/`truckCapOptimistic` sweep edildi
(ECONOMY.hangarStayDuration mutate edilerek, node ile).

**Temel bulgu — STRICT modelde dogal dolus suresi (fillTime=cargo/rate) oyuncu
sayisiyla ters oranti:** Normal senaryo (2.0 kutu/dk/oyuncu), ort. kargo 3.5:
- P1: fillTime(avg cargo)=105s, fillTime(max cargo=5)=150s → 30s'de sadece ~1
  kutu teslim edilip tir yariya bile dolmadan kalkiyor.
- P2: 52.5s / 75s
- P3: 35s / 50s
- P4: 26.3s / 37.5s → 30s ZATEN neredeyse yeterli.

**STRICT throughput saturasyon noktalari** (gun8, Normal, numHangars=1, tam
ondalik sweep ile bulundu — cap = cyclesPerDay × avgDeliverable, duration arttikca
sifir MALIYETLE artiyor cunku Truck.cs zaten "timer OR full" ile erken kalkiyor,
STRICT modelde uzun sure asla throughput'u DUSURMUYOR sadece diminishing return
veriyor):
- P1: 30s→3.818 kutu/gun (%76 doygunluk) ... 150s→5.011 (tam doygunluk, %100)
- P2: 30s→7.636 (%86) ... 75-80s→8.909 (%100)
- P3: 30s→11.118 (%92) ... 50-60s→12.027 (%100)
- P4: 30s→14.182 (%97) ... 40-50s→14.579 (%100) — zaten neredeyse doygun

**ONERILEN degerler** (~%97-99 doygunluk yakalayan "diz noktasi", tam
doygunluga gitmeyip asiri uzun bekleme/pacing riskini sinirlar):

| Oyuncu | Mevcut | Onerilen | %doygunluk (Normal) | %doygunluk (Yavas) |
|---|---|---|---|---|
| 1P | 30s | **90s** | %97 | %93 |
| 2P | 30s | **60s** | %98 | %93 |
| 3P | 30s | **40s** | %98 | — |
| 4P | 30s | **30s (degismez)** | %97 | — |

Yon kullanicinin sezgisiyle DOGRULANDI: 1P uzun, 4P kisa (mevcut deger zaten
dogru). Cap-nedenli TERS yon (memory kaydinda bahsedilen ihtimal) CIKMADI —
sebep: [[truck_hangar_window_cap]] bulgusu OPTIMISTIC/uretim-tavanli darbogazla
ilgiliydi (kac tir/gun), bu analiz ise TEK bir tirin ne kadar surede DOLDUGUYLA
ilgili — farkli mekanizma, cakismiyor.

**OPTIMISTIC modelde (birincil model) hangarStayDuration TAMAMEN ETKISIZ**:
`truckCapOptimistic()` fonksiyonu bu degeri hic kullanmiyor (cyclesPerDayMax
sadece OVERHEAD_TOTAL'a bagli, "stok hazir, dolum aninda" varsayimi). Yani
OPTIMISTIC dunya dogruysa bu degisikligin GUNLUK ALTIN TAVANINA etkisi SIFIR
— sadece `fullTrucksPerDayEstimate` (easy2 quest tamamlanma orani, bkz
[[quest_reward_balance]]) ve oyuncu hissi/pacing'i etkiler.

**Iflas/enflasyon riski testi** (STRICT modda tam 16-gun sim, baseline 30s
sabit vs onerilen): P1-4 Normal'de iflas yok her iki tarafta da, kasa artisi
mutedil (P1: 327→634 TL, P4: degismedi). Yavas senaryo STRICT'te HER IKI
tarafta da (baseline dahil) kira gunu iflasiyla bitiyor — bu onerilen
degisiklikten BAGIMSIZ, onceden var olan STRICT-model kirilganligi (oyun
OPTIMISTIC/on-stok varsayimina gore dengelenmis gorunuyor). Enflasyon/exploit
riski YOK: OPTIMISTIC'te sifir etki, STRICT'te en kotu ihtimalde bile mutedil
kazanc.

**fast_hangar perk etkilesimi** (bkz [[fast_hangar_perk_bug]]): perk ×1.30
uygulandiginda dogal olarak azalan getiri var (doygunluga yakin oldugu icin):
P1 90s→117s ile +0.11 kutu/gun, P4 30s→39s ile +0.40 kutu/gun (%97'den
%100'e sadece kirpinti). **Perk'in HARDCODED 120s tabani** kodda hala eski
degeri kullaniyor (bilinen bug) — bu array eklendiginde perk artik
`GetHangarStayDuration(playerCount) × 1.30` tabanindan hesaplanmali, yoksa
hata (once 30s tek deger vs 120s hardcode = 4x sapma) P sayisina gore
degisken sekilde devam eder (P1'de 90 vs 120 = %33 sapma, P4'te 30 vs 120 =
4x sapma). Ayni PR'da duzeltilmesi onerilir.

**Kablolama onerisi**: `baseRentByPlayerCount` (GameEconomySettings.cs:21) +
`GetBaseRent()` desenini birebir tekrarla — YENI
`float[] hangarStayDurationByPlayerCount = {90,60,40,30}` + `GetHangarStayDuration(int
playerCount)` helper GameEconomySettings.cs'e eklenir. DifficultyManager'a
`CalculateScaledHangarStay()` EKLEME — o desen (satir ~299-356) surekli-olcekli
formuller icin (musteri sayisi/para carpani gibi), hangarStayDuration ise
zaten tasarimci-ayarlanan DISKRET per-playercount deger (rent gibi), array+getter
idiomu ile mimariye daha uygun. Truck.cs:214 `hangarStayDuration=
economySettings.hangarStayDuration` satiri `economySettings.GetHangarStayDuration(
DifficultyManager.Instance.PlayerCount)` olarak degismeli (DifficultyManager
OnNetworkSpawn sirasinda zaten populated olmali, lobby sonrasi tir spawn oldugu
icin).

**Playtest-bagimli belirsizlik**: STRICT vs OPTIMISTIC hangisinin gercek
oyuncu davranisina yakin oldugu hala DOGRULANMADI (bkz
[[truck_hangar_window_cap]]). Eger gercek davranis STRICT'e yakinsa onerilen
degerler throughput'u agirlikli sekilde artirir (P1 icin +31%); OPTIMISTIC'e
yakinsa etkisi sadece pacing/quest-completion'da hissedilir, ekonomik tavana
dokunmaz. Iki durumda da RISK YOK, sadece FAYDA BUYUKLUGU belirsiz.

**Why:** Kullanici "1P'de 30s cok kisa" sezgisini analiz istedi; ilk kez
hangarStayDuration'in P-bazli ayrisiklastirilmasi modellendi (onceki
[[truck_hangar_window_cap]] turu tek sabit deger uzerinden calisiyordu).

**How to apply:** Gameplay hangarStayDurationByPlayerCount dizisini ekleyip
fast_hangar perk fix'ini AYNI PR'da yaparsa bu spec dogrudan uygulanabilir.
Playtest sonrasi STRICT/OPTIMISTIC netlesirse degerler ince ayar gerektirebilir
— tam doygunluga (150/75/50/40) gitmek pacing riski tasir, mevcut oneri bilincli
olarak %97-99 diz noktasinda birakildi.

Iliskili: [[truck_hangar_window_cap]] (tir kapasitesi tavani, farkli mekanizma),
[[fast_hangar_perk_bug]] (bu turda tekrar dogrulanan bagimli bug),
[[quest_reward_balance]] (easy2 fullTrucksPerDay bagimliligi)
