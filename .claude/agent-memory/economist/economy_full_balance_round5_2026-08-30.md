---
name: economy-full-balance-round5-2026-08-30
description: Round 5 event sistemi dengesi — 16 event envanteri, tek-gunluk etki siralamasi, pozitif/negatif asimetrisi (3.43 vs 2.43), CUSTOMER SUPPORT mekanik NO-OP, kota carpani tek-yonlu olu, FESTIVAL DAY 6x outlier
metadata:
  type: project
---

# Round 5 — Event sistemi dengesi (2026-08-30, Opus)

**Kod DEGISMEDI** (`.cs`/`.asset`/sahne/`sim.js` hicbiri). Olcum harness'i scratchpad'te
kuruldu; `sim.js`'in export ettigi `SRC4`/`ASSUMED`/`quotaFor`/`tableContentionEfficiency`/
`questDailyDecision` kullanilarak `runFullSim`'in **event-parametreli kopyasi** yazildi.
Dogrulama: eventsiz kosumda 16/16 hucrede `runFullSim` ile **BIREBIR** ayni sonuc
(finalCash + iflas gunu). Yani taban model Round 1-4 ile ayni.

**HANGI KIRA KULLANILDI:** CANLI kod, `baseRentByPlayerCount = {500,1000,1450,1800}`,
`rentGrowthMultiplier=1.20`. **Round 3'un `{290,650,1140,1630}` onerisi HENUZ UYGULANMADI**
ve bu round'un ana tablolarinda KULLANILMADI. Kira duyarliligi yalniz **FESTIVAL DAY** icin
gerekli — cunku event'ler kira gunlerine (4/8/12/16) HIC atanmaz (`EventCalendarUI.cs:761`
`IsRentDay(currentDay) continue`), dolayisiyla diger 15 event'in gunluk etkisi kiradan
tamamen bagimsiz. FESTIVAL icin iki kira ile de tablo asagida (§4).

---

## 1. Envanter — 16 event, ne yapiyor (EventEffectManager.cs:130-361 birebir)

| # | Event | Tip (takvim) | Dokundugu sabit(ler) | Sure | Canli mi |
|---|---|---|---|---|---|
| 1 | BUSY DAY | NEG | quota ×1.35, sabir ×0.85 | 1 gun | evet (ama §5) |
| 2 | DELIVERY BONUS | POS | `Truck.rewardPerBox` ×1.2 | 1 gun | evet |
| 3 | ANGRY CUSTOMERS | NEG | sabir ×0.6, quota ×1.1 | 1 gun | evet (ama §5) |
| 4 | RELAXED DAY | POS | sabir ×1.3 | 1 gun | evet (ama Normal bantta 0) |
| 5 | SLOW LOGISTICS | NEG | reward ×0.92, `exitDelay` ×1.5 | 1 gun | evet |
| 6 | EXPRESS CARGO | POS | reward ×1.08, exitDelay ×0.7 | 1 gun | evet |
| 7 | HEAVY BOXES | NEG | moveSpeed ×0.85, sprint ×0.8 | 1 gun | evet |
| 8 | GOLDEN BOX DAY | POS | reward ×1.15, exitDelay ×0.8, move ×1.08, sprint ×1.2, staminaRegen ×0.8, quota ×1.15, `isGoldenBoxDay=true` | 1 gun | evet — **ama bayrak OLU** |
| 9 | OPPORTUNITY DAY | POS | `upgradeCostMultiplier` 0.8 | 1 gun | evet (UpgradePanel.cs:1618, sahne wiring OK) |
| 10 | FATIGUE PROBLEM | NEG | move ×0.9, sprint ×0.7, staminaRegen ×0.6, quota ×0.85 | 1 gun | evet |
| 11 | VIP SERVICE | POS | reward ×1.12, `isVIPServiceDay=true` | 1 gun | evet — **ama bayrak OLU** |
| 12 | RAINY DAY | NEG | quota ×0.8 | 1 gun | evet |
| 13 | MARKETING DAY | NEG | reward ×0.7, quota ×1.2 | 1 gun | evet |
| 14 | SURPRISE AUDIT | NEG | `GetPenaltyMultiplier()=2` (para+prestij tum cezalar) | 1 gun | evet, 5 cagri yeri |
| 15 | FESTIVAL DAY | POS | gun basi `Random.Range(kira×0.10, kira×0.20)` TL | 1 gun (tek atis) | evet |
| 16 | CUSTOMER SUPPORT | POS | telefon cooldown ×0.5 | 1 gun | **MEKANIK NO-OP (§6)** |

Cezalarin 2× oldugu 5 nokta (SURPRISE AUDIT): `Truck.cs:664` (yanlis teslim para+prestij),
`BoxFallPenalty.cs:146`, `CustomerAI.cs:1393` (yanlis urun prestij),
`GameStateManager.cs:645` (musteri kaybi prestij), `:679` (kacirilan kota prestij).

**OLU KOD:** `IsGoldenBoxDay()` (cs:702) ve `IsVIPServiceDay()` (cs:709) tum `Assets/` icinde
**hicbir yerden cagrilmiyor** (grep). Iki event'in tum ekonomik kimligi carpanlarinda;
bool bayraklar artik-kalinti. (Islevsel bosluk YOK — aciklamalardaki %15/%12 zaten
`rewardPerBoxMultiplier`'dan geliyor — ama bayraklara yeni is baglanacak sanilmasin.)

### Siklik (`EventCalendarUI.cs:23-27,741-787`, DOGRULANDI — degismemis)
`EVENT_INTERVAL_MIN=1`, `EVENT_INTERVAL_MAX=2`, `INITIAL_EVENT_FREE_DAYS=3`,
`INITIAL_POSITIVE_EVENT_COUNT=2`, `GUARANTEED_NEGATIVE_EVENT_INDEX=2`. Kira gunleri atlanir.
40k Monte Carlo (uretim dongusu birebir):

| olcum | deger |
|---|---|
| ortalama event / 16 gun | **5.86** (gunlerin %37'si) |
| pozitif / negatif | **3.43 / 2.43** |
| dagilim | 3:2% 4:9% 5:25% 6:38% 7:21% 8:5% |
| event basina beklenen goruluş | POZ **0.43**, NEG **0.30** |

---

## 2. Tek-gunluk etki buyuklugu siralamasi

O gunun net gelirine gore % sapma; 16 hucre (Normal/Slow × strict/optimistic × P1-4) ×
9 uygun gun (5,6,7,9,10,11,13,14,15) ortalamasi. `ledgerMode:true` (grace nonlineerligi yok).
**Iki sutun**, cunku telefon kullanim orani sonucu ciddi degistiriyor:

| # | Event | Tip | ort % (telefon varsayilan) | ort % (telefon KAPALI) | Ns/P2 TL/gun |
|---|---|---|---|---|---|
| 1 | **FESTIVAL DAY** | POS | **+61.1** | **+59.5** | **+218** |
| 2 | **MARKETING DAY** | NEG | **−19.3** | **−23.8** | −74 |
| 3 | GOLDEN BOX DAY | POS | +11.1 | +19.6 | −4 |
| 4 | DELIVERY BONUS | POS | +12.4 | +16.8 | +32 |
| 5 | FATIGUE PROBLEM | NEG | −10.2 | −13.1 | −15 |
| 6 | SURPRISE AUDIT | NEG | −8.7 | −12.2 | −17 |
| 7 | RAINY DAY | NEG | −9.6 | −10.0 | −40 |
| 8 | VIP SERVICE | POS | +7.2 | +9.7 | +17 |
| 9 | SLOW LOGISTICS | NEG | −6.6 | −8.9 | −22 |
| 10 | EXPRESS CARGO | POS | +5.6 | +7.5 | +17 |
| 11 | HEAVY BOXES | NEG | −5.5 | −6.5 | −19 |
| 12 | BUSY DAY | NEG | −5.1 | **+2.3** | −71 |
| 13 | ANGRY CUSTOMERS | NEG | −1.8 | **+0.7** | −19 |
| 14 | RELAXED DAY | POS | +0.3 | +0.3 | 0 |
| 15 | OPPORTUNITY DAY | POS | 0 (modellenmiyor) | 0 | 0 → gercek deger §7 |
| 16 | CUSTOMER SUPPORT | POS | **0** | **0** | **0** |

**Bant dagilimi (isaret degistiren event'ler):**

| Event | min % (bant) | max % (bant) | genislik |
|---|---|---|---|
| FESTIVAL DAY | +28.0 (Norm/opt P2) | **+108.7** (Slow/strict P1) | 80.8 |
| BUSY DAY | −28.3 (Norm/strict P4) | **+10.5** (Norm/opt P1) | 38.9 |
| GOLDEN BOX DAY | −2.1 (Norm/strict P4) | +26.9 (Norm/opt P1) | 29.0 |
| RAINY DAY | −19.7 (Norm/opt P1) | **+7.9** (Slow/strict P4) | 27.6 |
| FATIGUE PROBLEM | −22.2 (Norm/strict P1) | **+1.6** (Norm/strict P4) | 23.9 |
| ANGRY CUSTOMERS | −7.8 (Norm/strict P4) | **+4.6** (Norm/opt P2) | 12.4 |

**Sonuc: FESTIVAL DAY ~6 kat outlier.** Ikinci en guclu event'in (MARKETING) 3x'i,
medyan event'in ~8x'i. Sebep yapisal: **tek rent-bagli event** — bonus `kira×0.10-0.20`,
kira hem P ile hem `1.20^cycle` ile buyuyor, ama STRICT bantta gunluk net gelir buyumuyor.
Slow/strict'te tek gun **bir gunun gelirinin tamamini ikiye katliyor** (+95…+109%).

---

## 3. Pozitif / negatif denge

**Sayica ASIMETRIK, oyuncu LEHINE.** 8 POS / 8 NEG tanimli ama uretim mantigi asimetrik:
ilk 2 event zorla POZITIF (`INITIAL_POSITIVE_EVENT_COUNT=2`), yalniz 3. event zorla
NEGATIF (`GUARANTEED_NEGATIVE_EVENT_INDEX=2`) → kosum basina **net +1 pozitif**.
Her POZ event 0.43 kez, her NEG event 0.30 kez gorulur (**%43 frekans avantaji**).

**Guc olarak da POZITIF agir basiyor** (frekans × tek-gun TL, etkilesim yok varsayimi —
`sim.js` sikligi modellemedigi icin bu tablo TURETILMIS, yon guvenilir buyukluk yaklasik):

| bant | 16g kum. net | POZ bekl. | NEG bekl. | NET | net/kumNet |
|---|---|---|---|---|---|
| Normal/strict P1 | 2 330 | +63 | −38 | **+26** | +1.1% |
| Normal/strict P2 | 5 549 | +120 | −84 | **+36** | +0.6% |
| Normal/strict P4 | 9 866 | +233 | −173 | **+60** | +0.6% |
| Normal/opt P4 | 19 519 | +438 | −276 | **+161** | +0.8% |
| **Slow/strict P1** | 1 530 | +58 | −17 | **+41** | **+2.7%** |
| **Slow/strict P2** | 3 458 | +114 | −26 | **+88** | **+2.5%** |
| Slow/opt P4 | 12 489 | +358 | −186 | **+172** | +1.4% |

Event sistemi net bir **para KAYNAGI**, ama kucuk (kum. netin %0.6-2.7'si) → enflasyon
riski YOK. Asil sorun **buyukluk degil kompozisyon**: net katkinin ~%80-100'u tek basina
FESTIVAL DAY'den geliyor (Ns/P2: +93 TL / toplam +36 TL net → FESTIVAL cikarilirsa
event sistemi NEGATIFE doner). Yani "pozitif event" kategorisinin dengesi tek bir
kaleme yaslanmis; digerleri (RELAXED/OPPORTUNITY/CUSTOMER SUPPORT = 3 tanesi, yani
pozitiflerin %37.5'i) sim'de **olculebilir sifir**.

---

## 4. FESTIVAL DAY — Round 3 kirasina duyarlilik

Bonus TL (min-maks):

| P | g5-7 (c1) ESKI | g5-7 R3 | g13-15 (c3) ESKI | g13-15 R3 |
|---|---|---|---|---|
| 1 | 60-120 | 35-70 | 86-173 | 50-100 |
| 2 | 120-240 | 78-156 | 173-346 | 112-225 |
| 3 | 174-348 | 137-274 | 251-501 | 197-394 |
| 4 | 216-432 | 196-391 | 311-622 | 282-563 |

Tek-gun etki (% net gelir):

| bant | ESKI kira | R3 kira |
|---|---|---|
| Normal/strict P1 | +71.7 | +41.5 |
| Normal/strict P4 | +59.7 | +54.0 |
| Slow/strict P1 | **+108.7** | +63.0 |
| Slow/strict P4 | +95.7 | **+86.5** |
| Normal/opt P2 | +28.0 | +18.2 |

Round 3 kirasi FESTIVAL'i **yumusatiyor ama cozmuyor** — hala %18-87 ve hala acik ara
1 numara. Asimetrik kesinti P1'de %42 kestigi icin P1'i cok, P4'te %9 kestigi icin P4'u
az yumusatiyor → **Round 3 uygulanirsa FESTIVAL'in P-egrisi TERSINE doner**
(su an P1 en guclu, sonra P4 en guclu olur). Round 10'da birlikte degerlendirilmeli.

---

## 5. Kota kolu (`dailyCustomerMultiplier`) — TEK YONLU OLU

"INTENSIVE DAY" adinda bir event **YOK** (`CustomerManager.cs:409` yorumu bayat; canli
event listesinde boyle bir isim gecmiyor). Carpanin kendisi **CANLI ve okunuyor**:
`CustomerManager.cs:410` `float multipliedCount = baseCount * eventCustomerMultiplier;`
→ clamp(1, 50). Clamp bagliyici DEGIL (sahne: `_minCustomersPerDay=1`, `_maxCustomersPerDay=50`).

**AMA ekonomik olarak yukari yonde neredeyse OLU.** Kota artiyor, varis hizi artmiyor:
varis araligi `customerArrivalIntervalByPlayerCount` event'ten etkilenmiyor → gunluk
**varis tavani** (`1 + spawnPenceresi / I`) gun 10'da **10.66-11.12**. Kota zaten 10-13.

Gun 10, servis edilen musteri degisimi:

| bant | kota | varis tavani | taban servis | BUSY (×1.35 → 14) | Δ | RAINY (×0.8 → 8) | Δ |
|---|---|---|---|---|---|---|---|
| Normal/strict P2 | 10 | 10.66 | 9.85 | 9.85 | **0.00** | 8.00 | −1.85 |
| Normal/strict P4 | 10 | 11.12 | 10.00 | 10.26 | **+0.26** | 8.00 | −2.00 |
| Normal/opt P4 | 10 | 11.12 | 10.00 | 10.26 | **+0.26** | 8.00 | −2.00 |
| Slow/strict P2 | 10 | 10.66 | 5.91 | 5.84 | **−0.07** | 5.91 | **0.00** |
| Slow/strict P4 | 10 | 11.12 | 7.70 | 7.59 | **−0.11** | 7.70 | **0.00** |

+%35 kota → **+0.00…+0.26 musteri** (Slow'da NEGATIF, sabir kaybindan). Fazladan gelen
4 kota musterisi neredeyse tamamen `ApplyMissedQuotaPenalty` (−0.2 prestij) yakitina
donusuyor. Buna karsilik −%20 kota Normal'da gercek maliyet (−2 musteri), Slow/strict'te
**bedava** (zaten servis edilemiyorlardi).

Kol asimetrik: **azaltma calisiyor, artirma calismiyor.** Sonuc: BUSY DAY ve ANGRY
CUSTOMERS "negatif" etiketli ama telefon kapaliyken **POZITIF** (+2.3% / +0.7%);
varsayilan kosumda gorulen negatiflikleri ikincil bir telefon artifakti (kota↑ →
sabit oranla daha cok arama → daha cok GERCEK saniye yanmasi). MARKETING DAY yalniz
`reward ×0.7` kismi sayesinde gercekten negatif.

**Ek not (aciklama-mekanik uyusmazligi):** BUSY DAY lokalizasyonu "CUSTOMER SPAWN RATE
INCREASES BY 35%" diyor; kod **spawn hizini degil gunluk SAYIYI** degistiriyor. Duzeltme
istenirse dogru kol `customerArrivalInterval`'i bolmek (spawn hizi), kotayi carpmak degil —
ama bu Round 5 kapsamindaki bir deger degisikligi degil, mekanik degisikligi (Round 10 notu).

**WIRING RISKI (QA'ya):** `EventEffectManager.OnNewDayHandler` (cs:363) ve
`CustomerManager.HandleNewDay` (cs:348) **ayni statik `DayCycleManager.OnNewDay`
event'ine** abone; cagri sirasi abone olma sirasina (=`OnNetworkSpawn` sirasina) bagli,
deterministik degil. CustomerManager once kosarsa gunun kotasi **DUNKU** carpanla
hesaplanir (yani event 1 gun geriden gelir). Playtest'te `Quota calc: ... eventMult=`
log'u ile dogrulanmali.

---

## 6. CUSTOMER SUPPORT × telefon-spam — NET SONUC

**Mekanik olarak TAM NO-OP.** `PhoneCallManager.GetEffectiveCooldownSeconds` (cs:299-311)
cooldown'u yariya indiriyor: 3sn → 1.5sn (perk alinmissa `Mathf.Max(1, 3−10)=1` → 0.5sn).
Ama cooldown **hicbir bantta bagliyici degil**; gercek kapilar `ValidateCallGuards`
(cs:385-425) icinde: (a) `HasUnspawnedCustomers` = gunluk kota, (b) `IsQueueFull`
(maxQueueSize=2), (c) zaman atlamasi 17:30'u gecmesin. Ard arda arama hizinin ust siniri
**kuyruk bosalma suresi** = servis dongusu:

| bant | kuyruk bosalma | cooldown taban | cooldown CS gunu | bagliyici? |
|---|---|---|---|---|
| Normal/strict P1 | 37.5 sn | 3 sn | 1.5 sn | HAYIR |
| Normal/*/P2-P4 | 18.0-18.8 sn | 3 sn | 1.5 sn | HAYIR |
| Slow/strict P1 | 62.5 sn | 3 sn | 1.5 sn | HAYIR |
| Slow/*/P2-P4 | 24.0-31.3 sn | 3 sn | 1.5 sn | HAYIR |

16/16 hucre "HAYIR". Cooldown yalniz kuyruk BOSKEN pes pese 2 aramanin arasina giriyor —
gun basina toplam ~3 saniye, halved ~1.5 saniye. Olculebilir etki **tam sifir** (§2).

**Ama telefon sorununu HAFIFLETMIYOR, AGIRLASTIRIYOR — sinyal kanalindan.** Takvimde
"POZITIF" etiketli ve aciklamasi "RECEPTION PHONE COOLDOWN IS CUT IN HALF" — yani oyuncuya
acikca *"bugun bol bol telefon ac"* diyor. Round 2/Round 1 ise telefon kullanimini artirmanin
STRICT bantta net **−58…−81%** oldugunu gosterdi. O gun oyuncu oranini 1.0'a cikarirsa:

| bant | gun basina maliyet (u=0.6/0.1 → 1.0) |
|---|---|
| Normal/strict P1 / P2 / P3 / P4 | −45 / −118 / −228 / −340 TL |
| Normal/opt P1 / P2 / P3 / P4 | −235 / −508 / −655 / −777 TL |
| Slow/strict P1 / P2 / P3 / P4 | +3 / −8 / −74 / −144 TL |
| Slow/opt P1 / P2 / P3 / P4 | −92 / −212 / −332 / −416 TL |

Yani **oyunun en zararli tek-gun event'i, "pozitif" etiketiyle takvimde duruyor** — sadece
oyuncu tavsiyeye uyarsa. Bu Round 7'nin (telefon) girdisi: telefon ekonomisi duzeltilmeden
CUSTOMER SUPPORT'a dokunmak anlamsiz; duzeltilirse bu event'e gercek bir kol lazim
(cooldown degil — ornegin o gun `callMoneyReward` veya `timeSkipAmount` kolu).

---

## 7. OPPORTUNITY DAY — sim'de 0, gercekte kucuk ama gercek

`UpgradePanel.GetCostMultiplier()` (cs:1611-1628) event carpanini **okuyor**, sahne wiring
DOGRULANDI (`The Main Office.unity:27538 eventEffectManager: {fileID: 2110336693}`).
`runFullSim` satin alma modellemedigi icin 0 cikiyor. Gercek tasarruf
= `0.20 × (baseCost + lvl×costStep) × upgradeCostMultiplierByPlayerCount{1, 2, 2.95, 3.7}`:

| kalem (L1) | baseCost | P1 tasarruf | P2 | P3 | P4 |
|---|---|---|---|---|---|
| Geniş Ambar | 60 | 12 | 24 | 35 | 44 |
| Hızlı Hangar | 120 | 24 | 48 | 71 | 89 |
| Ek Hangar | 200 | 40 | 80 | 118 | 148 |
| Kumarbaz Kasası | 350 | 70 | 140 | 207 | 259 |
| Su Sebili | 500 | 100 | 200 | 295 | 370 |

O gun **bir sey satin alinirsa** 12-370 TL — yani orta kalemde (~200 TL taban) VIP SERVICE
veya EXPRESS CARGO ile ayni mertebede, buyuk kalemde FESTIVAL'e yaklasiyor. Satin alma
YOKSA tam sifir. Yani varyansi yuksek ama **kirik degil**; oyuncu tarafindan zamanlanabilir
(alimi o gune erteleme) → dogru bir "planlama" event'i. Deger degisikligi ONERILMIYOR.

---

## 8. Kirik banda (Slow/strict) orantisiz hasar? — HAYIR

**Hicbir event Slow/strict'in iflas gununu degistirmiyor** (16 event × 4 P × 9 gun = 576
kosum; P1 g16, P2/P3/P4 g12 sabit kaldi). Sebep: o bantta cikti **mekanik-bagli**
(uretim tavani), musteri-bagli degil — negatif event'ler zaten kullanilmayan musteri
arzini kirpiyor. Nitekim negatif event'ler orada Normal'dakinden **DAHA AZ** zarar
veriyor (ör. RAINY DAY: Normal/opt P1 −19.7% ama Slow/strict P4 **+7.9%**).

En ince Normal/strict hucrelerinde tek-event en kotu durum (16 gun sonu kasa):

| P | taban kasa | en kotu event | en kotu kasa | Δ |
|---|---|---|---|---|
| 1 | 146 | FATIGUE PROBLEM (g10) | 99 | −47 |
| 2 | 781 | BUSY DAY (g15) | 670 | −111 |
| 3 | 692 | BUSY DAY (g15) | 509 | −183 |
| 4 | 1 068 | BUSY DAY (g15) | 813 | **−255** |

Hicbir tek event **hicbir bandi iflasa surukleyemiyor**. En ince hucre (Normal/strict P1,
146 TL) bile en kotu event'i −47 TL ile atlatiyor — cunku P1 kotasi (4-6) varis tavaninin
(5.83) cok altinda, kota-kolu event'leri onu neredeyse hic etkilemiyor. (Round 2'nin
"146 TL marji SAHTE-ince, asil tampon kullanilmamis grace" tespitiyle tutarli.)

**Uyari:** en kotu gun her zaman **gun 15** cikiyor (gun 16 kirasindan hemen once) —
event takvimi gun 15'e negatif atarsa oyuncunun telafi penceresi yok. Bu bir "sikliktan"
degil "yerlesimden" gelen risk; `EventCalendarUI` kira gunlerini disliyor ama
**kira gununden ONCEKI gunu** ozel olarak korumuyor.

---

## 9. Sabir (patience) kolu — Normal bantta OLCULEBILIR SIFIR

`CustomerAI.cs:897-900` bekleme sayaci **kuyruga varista** basliyor, `_isInInteraction`
olunca duruyor (cs:951). Sure `Random.Range(15,20)` (Customer.prefab:2323-2324) × perk.
Kuyruk/servis mikro-modeli (4000 deneme), kuyrukta sabri dolan musteri orani:

| bant | kota g10 | I (sn) | ×0.6 | ×0.85 | ×1.0 | ×1.3 |
|---|---|---|---|---|---|---|
| Normal/* (tum P) | 5-10 | 21.8-45.7 | **0** | **0** | **0** | **0** |
| Slow/strict P1 | 5 | 45.7 | 39.4% | 36.8% | 34.1% | 25.9% |
| Slow/strict P2 | 10 | 22.9 | 32.6% | 27.9% | 24.7% | 21.2% |
| Slow/strict P3/P4 | 10 | 21.8 | ~11.8% | ~8.1% | ~6.8% | ~4.0% |
| Slow/opt P2-P4 | 10 | 21.8-22.9 | 7-11.8% | 3.8-8.2% | 2.6-6.8% | 0.8-4.1% |

Normal bantlarda varis araligi (21.8-45.7sn) servis suresinden (18-18.8sn) buyuk → kuyruk
hic birikmiyor → sabir carpani **hicbir sey yapmiyor**. Bu yuzden RELAXED DAY (tek kolu
sabir ×1.3) 16 hucrenin 8'inde tam sifir, ortalamada **+0.3%** — oyundaki **en zayif
event**. ANGRY CUSTOMERS'in "−%40 sabir"i da yalniz Slow bantlarda his ediliyor.

⚠️ **Model uyarisi:** mikro-model oyuncu tepki gecikmesini (masaya yurume, ürünü bulma)
iceriyor DEGIL; gercek sabir kayiplari bundan YUKSEK. Yon guvenilir, buyukluk alt sinir.
Sabir kolunun gercek gucu Unity playtest ile olculmeli.

---

## 10. Ozet — Round 10 icin ham not (bu round DEGER DEGISIKLIGI ONERMIYOR)

| bulgu | siniflandirma |
|---|---|
| FESTIVAL DAY ~6x outlier, tek rent-bagli event, Slow/strict'te tek gunde gunluk geliri ikiye katliyor | **DENGE** — %10-20 orani veya "kiranin degil gunluk ortalama gelirin yuzdesi" tabani gozden gecirilmeli |
| Pozitif event'ler %43 daha sik (2 zorla-poz vs 1 zorla-neg) | **DENGE** — `INITIAL_POSITIVE_EVENT_COUNT` / `GUARANTEED_NEGATIVE_EVENT_INDEX` |
| CUSTOMER SUPPORT mekanik NO-OP + telefon-tuzagina "pozitif" tabelasi | **TASARIM** — Round 7 ile birlikte |
| Kota carpani yukari yonde olu (varis tavani bagliyor); BUSY/ANGRY gercekte negatif degil | **MEKANIK** — kol yanlis (sayi degil aralik olmali) |
| RELAXED DAY Normal bantta tam sifir | **TASARIM** — ikinci bir kol lazim |
| `isGoldenBoxDay` / `isVIPServiceDay` bayraklari okuyucusuz olu kod | **TEMIZLIK** |
| `OnNewDay` abone sirasi → kota carpani 1 gun geriden gelebilir | **QA/BUG RISKI** |
| Negatif event kira gununden ONCEKI gune (15) dusebiliyor, telafi penceresi yok | **YERLESIM** |
| Hicbir event Slow/strict iflas gununu degistirmiyor; tek event hicbir bandi batirmiyor | **RISK YOK** (dogrulandi) |
| Event sistemi net +%0.6-2.7 para kaynagi | **ENFLASYON RISKI YOK** |

Iliskili: [[economy_full_balance_round1_2026-08-30]], [[economy_full_balance_round2_2026-08-30]],
[[economy_full_balance_round3_2026-08-30]], [[economy_full_balance_round4_2026-08-30]],
[[event_interval_retune_2026-08-25]], [[missing_events_g9]],
[[phone_cooldown_perk_event_stacking_2026-08-29]], [[plateup_customer_quota_2026-08-29]]
