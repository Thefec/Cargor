---
name: faz2-prestige-rent-event-2026-07-30
description: FAZ2 kararlari -- kira g=1.35 + P-olcegi {500,1000,1550,2150}, prestij cift-carpan (served 0.4 / ppb 8, gelir-notr), 2 paralel servis istasyonu, P-bazli kargo (gelir-notr tir/gun fix), event bandi
metadata:
  type: project
---

**Rapor: `plans/economy-rebuild-2026-07-30-faz2.md`. Girdi: [[economy_rebuild_faz1_2026-07-30]].**
Hesap: scratchpad `faz2.js`/`faz2b.js`/`faz2c.js` (sim.js'i yalniz OKUR). Model paritesi sim.js ile ±0-3.3%.

## KARARLAR (oncelik sirasiyla)

**1. Prestij cift-carpan cozumu (gelir-notr).** `served 0.2->0.4` + `prestigePerBonus 4->8`
BIRLIKTE => odul egrisi ~ayni kalir ama prestij SAYISI 0-100 skalasini kullanir ve `maxPrestige=100`
ILK KEZ baglayici olur. `lost -0.6->-0.5` (asimetri 3:1 -> **1.25:1**). Diger prestij kalemleri x2
(wrongDel -0.16, wrongProduct -0.08, boxDrop -0.04, callPrestige 0.4, startingPrestige 12).
**Tavani INDIRME** — 60/70/80 tavana gun 7-15'te carptiriyor (gec oyun olur).

**2. Kira P-olcegi {500,900,1200,1500} -> {500,1000,1550,2150}.** FAZ1'de "P-olcegi dogru" demistim;
o paket ONCESI gelir egrisi (1:1.81:2.44:3.15) icin DOGRUYDU. Prestij paketi cok-oyunculu geliri
yukseltiyor (3P +21%, 4P +29%) -> yeni olcek **1:1.99:3.12:4.28**. Eski kira olcegiyle 4P easy-mode
(P yayilimi 0.79); yeni olcekle yayilim **0.01**. **#1 ve #2 AYRILAMAZ, ayni PR.**

**3. `rentGrowthMultiplier` 1.15 -> 1.35.** Gelir donguсe x1.32-1.38 buyuyor, kira x1.15 -> makas
%15-20/dongu aciliyor (baski 1.76 -> 1.18 DUSUYOR). 1.35 ile baski **duz ~1.85-2.05**.
**"Yukselen band" hedefimi TERK ETTIM**: g=1.45 gerekiyor, o degerde Yavas+OPTIMISTIC kaybediyor.
Gerilim ORAN ile degil MUTLAK kira ile verilecek (1P 500->1230, 4P 2150->5290).
NOT: 1.3 daha once olum sarmali diye 1.15'e dusurulmustu — o karar 160s gun + fazla-tahmin edilen
prestij geliriyle alinmisti; duzeltilmis tabanda 1.35 uc bantta SIFIR iflas veriyor. Bilincli geri donus.

**4. 2 PARALEL SERVIS ISTASYONU** (kuyruk buyutme DEGIL). `maxQueueSize` **2 KALSIN** —
buyutmek tek basina prestiji DUSURUYOR (4P son prestij 37.1 -> 29.8). Iskelet ZATEN YARIM VAR:
`CustomerManager.cs:124 serviceTables` dizisi (sahnede 1 eleman, `unity:68615`) + `cs:873
AssignDropOffTable` — **ikisinin de SIFIR cagirani var**. Yani yeni sistem degil, kablolama tamamlama.

**5. P-BAZLI KARGO = tir/gun'un gercek kaldiraci, GELIR-NOTR.**
`Range(2,6)` sabit -> **1P `Range(1,3)` / 2P `Range(2,4)` / 3P `Range(2,5)` / 4P `Range(2,6)`**
(ort kargo 1.5/2.5/3.0/3.5). Tam tir/gun 1.43/2.86/4.29/5.71 -> **3.33/4.0/5.0/5.71**
(tasarim hedefi 3/4/5/6). **kutu/gun DEGISMIYOR** (5/10/15/20) cunku darbogaz uretim.
Yan fayda: 1P STRICT tamDolanTir **0 -> 2.27** => Easy "1 tir tamamla" quest'i ilk kez mumkun.

## BEKLEME SURELERI — hukumler
- `hangarStayDuration` {90,60,40,30} **neredeyse dogru**, yalniz **1P 90->120**. OPTIMISTIC bantta
  TAMAMEN INERT (kutu/gun stay x0.5..x2 boyunca SABIT 5/10/15/20). "Olu bekleme" YOK (tir dolunca
  kalkiyor). 1P'de 90s'de en kucuk kargo bile dolmuyor (fillTime 100s) -> tamTir=0.
- `exitDelay` 5 + `respawnDelay` 3-5: **DOKUNMA**. OPT'ta sifir etki, STRICT'te ±3-6%. Ekonomi
  kaldiraci degil, hissiyat degeri. Tek firsat 6s anim tamponu (STRICT +6-16%) ama KOD-DOGRULANMAMIS.
- **Musteri sabri 15-20 -> 24-32 sn, P'den BAGIMSIZ.** Kok neden: sabir 17.5s < Yavas servis emegi
  25s -> **yapisal imkansizlik** = Yavas bandin prestij kanamasi. P-bazli YAPMA: kablolama olu
  ([[dead_wiring_p_scaling]]), 2 istasyonla kapasite zaten P ile olcekleniyor, ve DifficultyManager'in
  yonu (P arttikca sabir AZALIR) duzeltilen cok-oyunculu cezayi geri getirir.
- **Telefon**: `ringDuration` 25->**15** (3 calma x 25s = gunun %37.5'i!), sans P-bazli
  **{0.20,0.25,0.30,0.35}**. `SetCallChance` govdesi BOS ya doldurulmali ya silinmeli.

## EVENT — band ve kritik bulgular
Hedef: agir ±15-25%, hafif ±5-12%, prestij-event'i gunluk prestijin >=%15'i. YASAK: >±30% veya
(<±5% VE prestij <%15).
- **GOLDEN BOX DAY +47% = TEK gercekten bozuk event** -> rew 1.3->1.15, move 1.2->1.08, cust 1.2->1.15 (+16..23%).
- **SLOW LOGISTICS / EXPRESS CARGO yapisal olarak OLCULEMEZ (%0)**: yalniz `exitDelay` degistiriyorlar,
  o da OPT bantta inert. Kucuk `rewardPerBoxMultiplier` (0.92 / 1.08) eklenmeli.
- **VIP SERVICE +1.3%**: %10 sans **TIR BASINA** (kutu basina degil) x1.1 = beklenen +1%. RNG kaldirilip
  duz `rew 1.12` yapilmali. Aciklama "boxes" diyor, kodda "perfect box" mekanigi HIC YOK.
- **RELAXED DAY net NEGATIF**: tipi Poz, aciklamasi yalniz "sabir +%30" ama kodda gizli
  `dailyCustomerMultiplier=0.7` (`EventEffectManager.cs:182`) -> olculen -6..-11%.
- **RAINY DAY yanlis tip**: Positive ama etkisi musteri x0.8 = prestij -%20 -> Negative olmali.
- **BUSY DAY paket sonrasi POZITIF hale geliyor** (2 istasyonla kacan sifirlaniyor) -> Negative
  kalmasi icin `customerWaitTime 0.85` eklenmeli (beceri-kapili tasarim).
- `isGoldenBoxDay` bayragi OLU (`EventEffectManager.cs:633`, sifir cagiran) — event yine calisiyor.

## AYRISMA KARARLARI (tek dogru deger)
| A1 sahne `startingMoney` 50000 -> **500** (YAYIN ENGELLEYICI) | A1-b `moneyMultiplierPerPlayer`
1.0 -> **1.35** (500/675/911/1230; kira 4.3x'a cikarken tampon sabit kalirsa 4P STRICT gun4'te
269 TL'de kaliyor) | A2 `realDurationInSeconds` **200** (sahne kazanir, `.cs:50` default 160->200;
ayrik kalirsa reset ekonomiyi %25 kaydirir) | A3 `maxQueueSize` **2** (sahne kazanir,
`DEFAULT_QUEUE_SIZE` 3->2) | A9 `Truck.prefab:197-198` **50/40** (Resources yuklemesi patlarsa
10 TL/kutu = sessiz %80 gelir kaybi) |

## NIHAI TABLO (FAZ3 girdisi, Normal+OPT, 1 hangar)
kumulatif net **1P 6 154 / 2P 12 251 / 3P 19 173 / 4P 26 352**; oran **1.86/1.85/1.86/1.85**;
son prestij **86.2/100/100/100** (tavan gunu -/16/13/12).
Kira sonrasi FAZLA (upgrade butcesi): **1P 2 838 / 2P 5 618 / 3P 8 891 / 4P 12 091**.
STRICT'te fazla ~0 -> upgrade zayif takim icin LUKS olmali, zorunlu degil.
**FAZ3 uyarisi:** `upgradeCostMultiplierPerPlayer=1.15` (1.00/1.15/1.32/1.52) yeni gelir olcegi
1:1.99:3.12:4.28 ile kiyaslandiginda cok-oyunculuda upgrade COK UCUZ -> ~**1.62** olmali.
Quest `prestigeReward`/`prestigePenalty` alanlari da **x2** olmali (birikim x2 senkronu).

**How to apply:** Bu paket BUTUN halinde uygulanmali; parcali uygulama denge bozar (ozellikle
prestij paketi olmadan kira P-olcegi veya tersi). Uygulama oncesi #1'in kod isi (2 istasyon)
gameplay departmanina, kalan sayilar asset/sahne duzenlemesine gider.

Iliskili: [[economy_rebuild_faz1_2026-07-30]], [[prestige_function_surface]],
[[serial_customer_service_ceiling]], [[dead_wiring_p_scaling]], [[rent_death_spiral]],
[[truck_hangar_window_cap]], [[missing_events_g9]]
