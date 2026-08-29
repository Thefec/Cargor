---
name: plateup-customer-quota-2026-08-29
description: PlateUp gecisi icin dailyCustomerCountByDay/arrivalInterval/timeSkip/telefon/ceza + rewardPerBoxByPlayerCount degerleri. v2 -- kota=PARA TAVANI kanitla dogrulandi, model tamamen yeniden kuruldu.
metadata:
  type: project
---

**Gorev**: `plans/plateup-musteri-telefon.md` Is 0. **v2 (bu surum GECERLI)** -- ilk turdaki
"kota ile para bagli degil" iddiasi koordinator tarafindan KOD ILE CURUTULDU ve dogrulandi,
model bu duzeltmeyle bastan kuruldu. Tum kod `tools/economy-sim/sim.js` `PLATEUP` blogunda
(fonksiyonlar: `plateUpCeiling/plateUpQuota/plateUpArrivalInterval/plateUpTimeSkipMinutes/
plateUpDayOutcome/plateUpBoxSupply/runSimPlateUp`, CLI bolum 13-17).

## ⭐ KANITLANMIS TEMEL GERCEK (v1'i GECERSIZ KILAR) -- kota = gunluk gelir tavani
- `CustomerAI.cs:1442-1444 PlaceProductOnDropOffTable`: musteri KENDISI
  `Instantiate(productPrefabs[productIndex], ...)` yapiyor -- ProductSupply modu (cs:77,83
  "müşteri bir ÜRÜN bırakır, oyuncu alır").
- `PickUpScripts/ShelfState.cs`: SIFIR `Instantiate` -- raf sadece DEPOLUYOR, URETMIYOR.
- `TableScripts/Shelf.cs` (NetworkedShelf) auto-respawn eden BOS kutular sagliyor (1sn
  respawn) ama bunlar `boxDropMoneyPenalty` notunun da dogruladigi gibi ICI BOS/degersiz --
  degeri veren PRODUCT, tek kaynagi musteri. `productPrefabs`/`ProductSupply` TUM projede
  sadece 3 dosyada geciyor (CustomerAI, CustomerManager, PostRentFeatureUnlocks) -- bagimsiz
  bir restock/warehouse spawner YOK.
- Zincir: **musteri -> urun (Instantiate) -> oyuncu paketler -> tir -> `Truck.cs:643
  AddMoney`.** SONUC: gunluk kota = gunluk kutu arzinin (= gelirin) GERCEK ust siniri.
  `truckThroughput()` (emek/masa/hangar) sadece bu arzi ISLEME HIZINI modelliyor, arzin
  KENDISINI degil -- ikisinin MINIMUMU gercek kutu sayisi (`plateUpBoxSupply`).
- [[money_comes_only_from_trucks]] GUNCELLENMELI: "musteri sifir para verir" hala DOGRU
  (musteri DOGRUDAN AddMoney cagirmiyor) ama "musteri talebi parayi sinirlamiyor" artik
  YANLIS -- musteri sayisi ARTIK dolayli ama SIKI bir para tavani (urun kaynagi uzerinden).

## ⭐ İKİNCİ DUZELTME -- 1 istasyon ANA SENARYO (fallback degil)
Koordinator sahnedeki `serviceTables[1]={fileID:0}` bosluğunu BAGIMSIZ DOGRULADI ve "2. masa
eklenmesi garanti degil" dedi. Tum asagidaki tablo `SRC.serviceStations=1` (canli) ile
hesaplandi -- 2-istasyon onerisi arka planda dursun ama YAYINLANACAK deger BUDUR.

**Yapisal sonuc**: 1 istasyonda musteri servis TAVANI (`plateUpCeiling`) P3'ten itibaren
"istasyon-slotu" ile DOYUYOR (labor degil) -- yani **P3 ve P4 urun-arzi P2'ye NEREDEYSE ESIT**
kaliyor (gun16: P2=12, P3=P4=13). Ama `baseRentByPlayerCount` P ile 3.6x buyuyor
(500->1800). Bu, rent P ile buyurken gelir-tavani DUZLESEN bir YAPISAL ACIK yaratiyor --
flat `rewardPerBox=50` ile test edilince (sim.js CLI bolum14) Normal senaryoda hicbir P
iflas etmiyor (prestij-tier bonusu organik olarak yeterli) AMA bu tavanin ne kadar dar
oldugunu gosteriyor; asagidaki lever bunu GENISLETIYOR.

## SECILEN LEVER: rewardPerBoxByPlayerCount (YENI, P-bazli)
```
rewardPerBoxByPlayerCount = [50, 55, 70, 88]   // index = P-1, flat rewardPerBox=50'nin YERINE
```
**Turetim**: her rent dongusu (gun1-4/5-8/9-12/13-16) icin gerekli gunluk gelir =
`(kira/4) x behavioralMargin(1.176=1/0.85)`, gerceklesecek kutu sayisi = `avgKota x
errorFactor(0.83=1-wrongDeliveryRate.Normal-physicalDropRate.Normal)`, gerekli rewardPerBox =
gerekli gelir / gerceklesecek kutu (sim.js CLI 13c, tum P/dongu kombinasyonlari icin
hesaplandi, EN KOTU donguyu karsilayacak sekilde yuvarlandi: P1=51→50(zaten yeterli, mevcut
canli deger), P2=53.2→55, P3=72.5→70(hafif altta, promosyon+quest+telefon payi tampon
sayildi), P4=90→88).

**Neden bu lever, digerleri degil (tek satir gerekce)**:
- ~~Kotayi P3/4 icin daha da buyutmek~~ -- MEKANIK OLARAK IMKANSIZ, zaten 1-istasyon
  tavanina carpiyorlar (istasyon eklenmeden buyutulemez).
- ~~rentGrowthMultiplier'i tekrar dusurmek~~ -- 2026-08-20'de 1.35->1.20 KONTROL-onayli
  tuned edildi, P1/P2 zaten saglikli; global buyume oranini oynatmak o turu riske atar VE
  asil sorunu (rent P ile buyuyor, urun-arzi buyumuyor) COZMEZ (P-BAGIMSIZ bir parametre,
  P-BAGIMLI bir acigi kapatamaz).
- ~~Prestij-tier bonusuna guvenmek~~ -- organik yardimci (zaten flat-50 testinde Normal
  senaryoyu kurtardi) ama P'ye gore FARKLILASTIRILAMIYOR (tek global deger) ve ERKEN
  GUNLERDE (dusuk baslangic prestiji=12) yetersiz.
- **rewardPerBoxByPlayerCount** rent'in P ile buyudugu ORANI DOGRUDAN telafi eden TEK
  surgical, P-bazli, mevcut kod-deseniyle (`baseRentByPlayerCount` gibi) tutarli lever.

## SIM DOGRULAMASI (`runSimPlateUp`, sim.js CLI bolum 14-17)
- **Normal (strict+optimistic), TUM P**: `rewardPerBoxByPlayerCount` ile iflas YOK, saglikli
  kasa marji (P4 Normal-strict sonKasa=6366, Normal-optimistic=7635).
- **rentGrowthMultiplier REGRESYON KONTROLU** (bolum 17): 1.20 DEGISTIRILMEDI, Normal-strict
  P1-P4 hepsi saglikli -- 2026-08-20 karari BOZULMADI.
- **Slow+strict (en kotu senaryo, ONCEDEN DE EN ZAYIF band)**: OLD `runSim` (mevcut canli
  modelin esdegeri) ZATEN P1-P4 hepsini iflas ettiriyordu (gun11/12/8/8) -- bu YENI bir
  regresyon DEGIL, ONCEDEN VAR OLAN bilinen zayiflik ([[rent_growth_1_35_deficit_2026-08-20]]:
  "SLOW-STRICT g'den bağımsız erken ölüyor, tek sabitle çözülmez"). YENI modelde P4 TAMAMEN
  KURTULUYOR (iflas yok, sonKasa=217 dar ama hayatta), P3 gun8->gun12'ye erteleniyor
  (iyilesme, tam cozum degil), P1/P2 ayni gunde basarisiz kaliyor (KOTULESME YOK, reward
  P1'de zaten degismedi/P2'de hafif arttı ama yetmedi). **KOK NEDEN reward DEGIL**: Slow
  senaryoda `mekanikTavan` (truckThroughput -- boxesPerMinPerPlayer=1.2, labor payi) kotanin
  COK ALTINDA kaliyor (ör. P2 gun8: kota=9, mekanikTavan=3.9) -- yani Slow+strict'te asil
  darbogaz URETIM HIZI, musteri-kotasi/reward DEGIL. Reward'i P2 icin 65'e kadar cikarmak bile
  (test edildi) day12 basarisizligini KAPATAMADI -- fazla reward pompalamak yerine bu, AYRI
  bir "dusuk-P Slow-strict taban kira" sorunu olarak birakildi (mevcut hafiza girisiyle
  tutarli, tek sabitle cozulemez).

## GUNCEL DEGER TABLOSU (1-istasyon, YAYINLANACAK)

### dailyCustomerCountByDay (P-bazli)
| gun | P1 | P2 | P3 | P4 |
|---|---|---|---|---|
| 1-3 | 4 | 7 | 8 | 8 |
| 4-5 | 4 | 8 | 8-9 | 8-9 |
| 6-7 | 4 | 9 | 9 | 9 |
| 8-10 | 5 | 9-10 | 10 | 10 |
| 11-12 | 5 | 10-11 | 11 | 11 |
| 13-14 | 6 | 11 | 12 | 12 |
| 15-16 | 6 | 12 | 12-13 | 12-13 |
Tam dizi: `node tools/economy-sim/sim.js` CLI 13a. **P3≈P4≈P2+1** -- yuvarlama hatasi degil,
1-istasyon mekanik doygunlugu (yukarida acikladi). P-olcekleme icin DUZ CARPAN kullanma
(eski `playerCountMultiplier=1+(P-1)*0.3` de gecersiz) -- P-indeksli int[4] dizi YAYINLA.

### customerArrivalInterval (P-bazli, saniye, gunlere gore ORTALAMA -- ampirik, kapali form artik gecersiz)
| P | 1 | 2 | 3 | 4 |
|---|---|---|---|---|
| sn | 44 | 22 | 21 | 21 |
(v1'deki kapali-form 44.1/P SADECE labor-baglayici rejimde geçerliydi; P3/4'te istasyon-slotu
baglayici oldugundan artik ~P2 ile ayni. sim.js CLI 13b.)

### timeSkipAmount (P-bazli, oyun-dakikasi, gün8 referans donusumle)
| P | 1 | 2 | 3 | 4 |
|---|---|---|---|---|
| dakika | 115 | 59 | 55 | 55 |

### Diger 5 madde -- ETKILENMEDI, ONCEKI GEREKCE GECERLI
- **phoneCooldownSeconds = 20 sn (flat)**: degismedi; P3/4'te artik dogal aralikla (21s)
  neredeyse esit oldugundan telefon o P'lerde MARJINAL fayda saglar (zararsiz, sadece
  onemsiz) -- P1/2'de hala anlamli hizlandirici.
- **callMoneyReward=20 / callPrestigeReward=0.4**: degismedi, ana gelir denklemine girmiyor.
- **dayEndGraceSeconds=30**: degismedi; kucuk mutlak kota sayilariyla (5-13/gun) HER KUTU
  artik orantisal olarak daha degerli -- grace'in "yarim tiri kurtarma" faydasi daha da
  onemli hale geldi, degeri arttirmaya gerek yok.
- **missedQuotaPrestigePenalty=-0.2 / customerLostPrestigePenalty=-0.4 (degismedi)**: yeni
  1-istasyon dusuk-kota degerleriyle YENIDEN DOGRULANDI -- `runSimPlateUp` sonPrestij hicbir
  test edilen senaryoda (Normal/Slow x strict/optimistic x 1P-4P) 0'a yaklasmadi (en dusuk
  gozlemlenen sonPrestij ~33.9, P1/Slow/strict).

## Sim.js degisiklikleri (additive, mevcut runSim DEGISMEDI, commit'siz)
`tools/economy-sim/sim.js`: `PLATEUP` sabiti + `plateUpBoxSupply` + `runSimPlateUp` (yeni,
runSim'in kota-baglantili varyanti) + CLI bolum 13(a/b/c)-17. `node tools/economy-sim/sim.js`
ile calistirilip dogrulandi (2026-08-29, koordinator geri bildirimi sonrasi v2).

## Handoff notu (gameplay'e)
`rewardPerBoxByPlayerCount` yeni bir P-bazli config -- uygulanirsa `EconomyInvariantCheck.cs`
ve GDD.md ekonomi bolumu de guncellenmeli (CLAUDE.md kurali: "Bir değeri değiştirirsen orayı
da güncelle").

Iliskili: [[money_comes_only_from_trucks]] (KISMEN GUNCEL DEGIL, yukarida not dus),
[[serial_customer_service_ceiling]] (2-istasyon COZUMUNUN KOKENI, sahne hala eksik),
[[rent_growth_1_35_deficit_2026-08-20]] (Slow-strict'in "tek sabitle çözülmez" ONCEKI
tespiti bu turda DOGRULANDI, kotulesmedi), [[dead_wiring_p_scaling]], [[sim_resync_2026-08-19]]
