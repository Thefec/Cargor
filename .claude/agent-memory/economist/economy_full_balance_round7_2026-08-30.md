---
name: economy-full-balance-round7-2026-08-30
description: Round 7 - telefon ekonomisi. Round 1/2'nin "telefon beceri-ters TRAP" bulgusu buyuk olcude MODEL ARTIFAKTI (3 hata, en buyugu SkipTime'in TABAN 200s ile donusum yapmasi); duzeltilmis v5 modeliyle oneri timeSkipAmountByPlayerCount={115,49,47,47} (P1 DEGISMEZ), callMoneyReward/callPrestigeReward DEGISMEZ, CUSTOMER SUPPORT cooldown yerine ZAMAN maliyetini yariya indirsin, phone_line perki yeni alan phoneTimeSkipPerkMultiplier=0.80 ile yeniden hedeflensin
metadata:
  type: project
---

# Round 7 — Telefon ekonomisi (2026-08-30, Opus)

Takip: `plans/economy-full-balance-2026-08-30.md`. **Kod DEĞİŞMEDİ** (`.cs`/`.asset`/sahne/
`sim.js` hiçbiri). Ölçüm scratchpad harness'ıyla: `sim.js`'in export ettiği
`SRC4`/`ASSUMED`/`quotaFor`/`truckThroughputWindowed`/`questDailyDecision` kullanılarak
`fullCustomerDay`+`runFullSim`'in **parametreli kopyası** yazıldı. Harness `model:'v4'`
modunda **80/80 hücrede (16 hücre × 5 telefon oranı) `runFullSim` ile BİREBİR aynı**
sonucu verdiği doğrulanarak kalibre edildi.

**Kullanılan kira: CANLI `{500,1000,1450,1800}` / g=1.20** — Round 3'ün `{290,650,1140,1630}`
önerisi HÂLÂ UYGULANMADI (ayrıca çapraz kontrol edildi, bkz. §7).

---

## 1. ⭐ EN ÖNEMLİ BULGU — Round 1/2'nin "telefon TRAP'i" BÜYÜK ÖLÇÜDE MODEL ARTIFAKTI

`runFullSim`'in telefon modeli üç yerde canlı koddan sapıyor. Ablasyon (aynı harness,
tek tek açılıp kapatılarak):

| varyant | Normal/strict OPT | u=100 sonucu |
|---|---|---|
| A — `runFullSim` v4 (Round 2'nin tablosu) | %0 (3/4 hücre) | 15/16 hücrede İFLAS |
| B — v5 iskeleti, v4 varsayımlarıyla | %0 (4/4) | iflas |
| C — **+ SkipTime dönüşüm düzeltmesi** | **%25 (4/4)** | iflas |
| D — **+ çift-sayım düzeltmesi** | %25 (4/4) | **iflas YOK** (551-1850 TL) |
| E — + forced-spawn varış kredisi | %25 (4/4) | iflas yok, ceza geri geldi |
| F — + drain sıkışması (**= v5, kanonik**) | %25 (4/4) | iflas yok, −52…−87% |

### 1a. SkipTime dönüşümü (en büyük hata) — CANLI KOD KANITI
`DayCycleManager.cs:425` (ve `PredictTimeAfterSkip` cs:446) saniye/oyun-dakikası oranını
**`realDurationInSeconds` (TABAN 200s)** ile hesaplıyor, günün gerçek uzunluğu
`CurrentDayDuration` (`200 + (gün−3)×10`, cs:199-205) ile DEĞİL:

```csharp
float secondsPerGameHour = realDurationInSeconds / totalGameHours;   // cs:425
float skipAmountInSeconds = minutesToSkip * secondsPerGameMinute;
```

Sonuç: **çağrı başına gerçek-saniye maliyeti günden BAĞIMSIZ ve SABİT** = `T × 0.30303 sn`.
`runFullSim` bunun yerine `phoneCalls × I` (tam bir doğal varış aralığı) yazıyordu:

| P | T (dk) | GERÇEK maliyet (sn) | doğal aralık I (sn) | gerçek oran | sim'in varsaydığı |
|---|---|---|---|---|---|
| 1 | 115 | 34.85 | 45.72 | **0.762** | 1.000 |
| 2 | 59 | 17.88 | 22.86 | **0.782** | 1.000 |
| 3 | 55 | 16.67 | 21.82 | **0.764** | 1.000 |
| 4 | 55 | 16.67 | 21.82 | **0.764** | 1.000 |

Yani sim, telefonun zaman maliyetini **~%31 fazla** faturalandırıyordu. Telefon zaten
"beklemenin %76'sı fiyatına müşteri" veriyor.

**⚠️ ROUND 10 UYARISI:** Biri bunu "bug" sanıp `CurrentDayDuration` ile düzeltirse
telefonun geç-oyun maliyeti **+%65** artar (gün 16'da 34.85→57.5 sn) ve Round 1/2'nin
TRAP'i GERÇEKTEN ortaya çıkar. **Bu davranış KORUNMALI** (sabit gerçek-maliyet telefonu
öğretilebilir kılan şeyin ta kendisi). Düzeltilmesi gereken şey yalnız
`timeSkipAmountByPlayerCount` tooltip'i: "atlanan oyun-dakikası" ifadesi yalnız gün 1-3'te
doğru; gün 16'da nominal 115 dk fiilen ~70 oyun-dakikası ilerletiyor.

### 1b. Çift-sayım
v4: `dayActive = min(dayDur, naturalEnd) − atlanan`. Erken gün-bitişi (`naturalEnd`)
bağlayıcıyken atlama İKİNCİ kez düşülüyordu. Doğrusu iki yolun **min**'i:
`min(dayDur − atlanan, naturalEnd)`, üstelik `naturalEnd` ancak kota tamamen spawn olmuş
VE kuyruk boşalmışsa açılır (`CustomerManager.CheckEarlyDayCompletion`).

### 1c. Forced-spawn kredisi
v4 `arrivalCap`'i telefondan bağımsız sabit tutuyordu; `ForceSpawnNextCustomer` doğal
varışlara EKLENİR. Bu düzeltme u=%100'ü tekrar cezalandırıyor (çağrılan ama servis
edilemeyen müşteri → `lost` −0.4), yani cezayı model artifaktı yerine GERÇEK mekanizmaya
dayandırıyor.

**Sonuç:** Telefon "beceri-ters trap" DEĞİL; ama mevcut sabitlerle hâlâ **yüksek-P /
Slow bantlarında ölçülü kullanım bile hafif negatif** (Norm/opt P3/P4 −%0/−2,
Slow/opt P2/P3/P4 −%5/−7/−13 @u=%20). Round 7'nin düzelttiği şey BU.

---

## 2. ÖNERİ PAKETİ (Round 10 uygulasın)

| sabit | dosya:satır | mevcut | ÖNERİ | gerekçe |
|---|---|---|---|---|
| `timeSkipAmountByPlayerCount` | `GameEconomySettings.cs:117` | `{115,59,55,55}` | **`{115,49,47,47}`** | maliyet/doğal-aralık oranını P2-P4'te 0.78→**0.65**'e çeker; P1 zaten doğru fiyatlı (§3) |
| `callMoneyReward` | `cs:126` | 20 | **20 (DEĞİŞMESİN)** | artırmak tepe noktayı u=%100'e kaydırıyor (§4) |
| `callPrestigeReward` | `cs:129` | 0.4 | **0.4 (DEĞİŞMESİN)** | Round 6'nın enflasyon korkusu ölçümle çürüdü (§5) |
| `phoneCooldownSeconds` | `cs:120` | 3 | **3 (DEĞİŞMESİN)** | kullanıcı kararı; ekonomik olarak zaten bağlayıcı değil |
| `phoneCooldownPerkBonusSeconds` (perkin atadığı) | `PerkEffect.cs:301` | `10f` | **`1f`** | 10f `Mathf.Max(1,3−10)` ile tabana çakılıyor; 1f → 3sn→2sn, **ekonomik değeri SIFIR, sadece his** (§6) |
| YENİ `phoneTimeSkipPerkMultiplier` | `GameEconomySettings.cs` (yeni alan) | — | varsayılan **`1f`**, `ApplyPhoneLine` **`0.80f`** atasın | perkin GERÇEK ekonomik etkisi buraya taşınsın (§6) |
| CUSTOMER SUPPORT etkisi | `PhoneCallManager.GetEffectiveCooldownSeconds:303-309` | cooldown ×0.5 | **zaman maliyeti ×0.5** (cooldown'a dokunma) | bugünkü hâli no-op + yanıltıcı (§8) |

`{115,49,47,47}` seçim kuralı: `T_P = 0.65 × I_P / 0.30303`.

| P | T eski→yeni | çağrı maliyeti sn | oran (maliyet/I) | u=%25'te günlük çağrı | 16 gün toplam telefon geliri |
|---|---|---|---|---|---|
| 1 | 115 → **115** | 34.8 (aynı) | 0.76 (aynı) | 1.2 | 385 TL |
| 2 | 59 → **49** | 17.9 → 14.8 | 0.78 → 0.65 | 2.4 | 755 TL |
| 3 | 55 → **47** | 16.7 → 14.2 | 0.76 → 0.65 | 2.5 | 800 TL |
| 4 | 55 → **47** | 16.7 → 14.2 | 0.76 → 0.65 | 2.5 | 800 TL |

---

## 3. Neden P1 DEĞİŞMİYOR (asimetri gerekçesi)

Parametrik tarama (r_P1 × r_P2-4, her biri {0.76,0.70,0.65,0.60,0.55,0.50}, 24 kombinasyon,
ledger modda 21 noktalı u ızgarası): P1'i indirmek **tepe noktayı %65-90'a fırlatıyor** ve
u=%100 cezasını −%36'dan −%8'e düşürüyor — yani P1'de telefon spam butonuna dönüşüyor.
Mevcut P1 eğrisi zaten sağlıklı: tepe %15-50, u=%20 kazancı **+%2…+25 (hepsi ≥0)**,
u=%100 cezası −%36…−63. **Bozuk olmayanı düzeltme.** Kırık olan yalnız P2-P4 (özellikle
Slow bandı: orada servis döngüsü 30sn > varış aralığı 21.8sn olduğu için müşteriyi öne
çekmek hiçbir şey kazandırmıyor, sadece gün yakıyor).

---

## 4. Neden `callMoneyReward` ARTMIYOR (kol seçimi kanıtı)

18 kombinasyon (T ölçeği × {20,30,40} TL), 12 çözünür hücre:

| T ölçeği | para | OPT dağılımı | u=%25 kazanç | u=%100 cezası (en zayıf) |
|---|---|---|---|---|
| 1.0 | 20 (mevcut) | 0%:5 25%:6 50%:1 | −22%…+24% | −36% |
| 1.0 | **30** | 0%:2 25%:8 50%:2 | −9%…+45% | **−11%** |
| 1.0 | **40** | **25%:7 75%:4 100%:1** | +4%…+70% | **0%** |
| **0.8** | **20** | **25%:9** 0%:1 75%:2 | −3%…+41% | −9% |
| 0.6 | 20 | 100%:4 50%:3 25%:5 | +8%…+55% | 0% |

**Para ödülü artırmak tepe noktayı sağa (spam'e) kaydırıyor** çünkü düz bir TL/çağrı
gelirine dönüşüyor — u ile lineer artıyor, oysa zaman maliyeti gün kısaldıkça
süperlineer ısırıyor. **Zaman maliyetini düşürmek** ise tepe noktayı yerinde tutup
eğrinin tamamını yukarı kaydırıyor. → Doğru kol ZAMAN, ödül DEĞİL.

---

## 5. `callPrestigeReward = 0.4` DEĞİŞMESİN — Round 6 §8'in korkusu ÇÜRÜDÜ

Round 6 "16 günde teorik tavan P3/P4 +64 prestij, zaman maliyeti düzeltilirse enflasyon
kaynağı olur" demişti. Öneri paketiyle ölçüm (canlı kira, önerilen T):

| hücre | prestij u=0 | u=%25 | u=%100 | tavan |
|---|---|---|---|---|
| Norm/strict P1 | 42.9 | 45.9 (+3.0) | **35.8 (−7.1)** | 100 |
| Norm/strict P3 | 76.0 | 84.8 (+8.8) | 81.2 (+5.3) | 100 |
| Norm/opt P4 | 77.3 | 86.4 (+9.1) | 83.8 (+6.5) | 100 |
| Slow/opt P1 | 44.5 | 51.9 (+7.5) | **41.8 (−2.6)** | 100 |

1. Önerilen kullanım bandında (%10-25) prestij kazancı yalnız **+1.4…+9.1 puan (+%3-13)** —
   Round 6'nın korktuğu +64 DEĞİL (o rakam u=%100'ün teorik tavanıydı).
2. **Telefon prestiji kendi kendini frenliyor**: u=%100'de 8/12 hücrede final prestij
   u=0'dan DÜŞÜK, çünkü çağrılıp servis edilemeyen müşterinin −0.4'ü çağrının +0.4'ünü
   siliyor. Yani ödül zaten "servis edebileceğin kadar çağır" diye fiyatlanmış.
3. `maxPrestige=100` **hiçbir hücrede dolmuyor** (max gözlem 86.4).
4. Düşürme denendi ve **ZARARLI**: `0.4 → 0.2` u=%20 kazancını 12 hücrenin **5'inde
   negatife** çeviriyor (Slow/opt P1-P4 ve Norm/opt P3/P4) → Round 7'nin düzelttiği
   tuzak geri geliyor. `0.3` ara değeri de 3 hücrede negatif.

⚠️ Tek kalan risk Round 6 §6'nın kendi bulgusu: `prestige_master` L1/L2 zaten 9/16 hücrede
tavanı patlatıyor. Telefon prestiji **o perkle birlikte** tavanı 2-3 gün öne çekebilir —
ama bu perkin sorunu, telefonun değil.

---

## 6. `phone_line` perki — COOLDOWN'DAN ÇEKİLİP ZAMAN MALİYETİNE BAĞLANMALI

Sahne kaydı (`The Main Office.unity:27395-27412`): relic (`kind:1`), `maxLevel:1`,
`baseCost:160`, `disabledInDraft:0` (**draft'ta AKTİF**), `contentText` **BAYAT**
("Saatte yapabileceğin telefon sipariş sayısı +1 artar" — V3 pasif telefon metni).

**Cooldown'a bağlı kalmasının ekonomik değeri SIFIR** (Round 5 §2: cooldown 16/16 hücrede
bağlayıcı değil; gerçek kapı `IsQueueFull`/`HasUnspawnedCustomers`). 160 TL'lik bir relic
hiçbir şey satın almamalı değil. Retarget seçenekleri ölçüldü (değer/maliyet, fiyat
P-costMult `{1,2,2.95,3.7}` ile ölçekli):

| varyant | değer/maliyet min–medyan–max | perkli tepe u (max) | verdict |
|---|---|---|---|
| çarpan 0.85 | 0.17 – 0.35 – 1.13x | %75 | zayıf |
| **çarpan 0.80** | **0.23 – 0.72 – 1.71x** | **%75** | ✅ |
| çarpan 0.75 | 0.23 – 0.96 – 2.21x | **%95** | dersi çözüyor |
| çarpan 0.60 | 0.54 – 2.1 – 4.50x | **%100** | spam butonu |
| düz −10 dk | 0.07 – 0.57 – 1.25x | %50 | P1'e ~sıfır (0.07x) — adil değil |

**ÖNERİ:** yeni SO alanı `phoneTimeSkipPerkMultiplier` (varsayılan `1f`),
`ApplyPhoneLine` **mutlak atama** ile `0.80f` yazsın (`PerkEffect` deseni — `+=`/`*=` YASAK,
bkz. [[perk_card_absolute_assignment_conflict]]). `phoneCooldownPerkBonusSeconds` ise
`10f → 1f` (3sn→2sn) olarak KALSIN ama **ekonomik değil, yalnız his** diye etiketlensin.

Medyan 0.72x biraz zayıf; playtest zayıf bulursa **kol FİYAT (160→120), çarpan DEĞİL** —
çarpanı düşürmek "dikkatli kullan" dersini çözüyor (0.75'te bir hücre %95'e fırlıyor).
`contentText` de güncellenmeli. ⚠️ Yeni alana yazan ikinci bir kart/perk eklenirse
mutlak atama birbirini SESSİZCE siler.

---

## 7. ⭐ DOĞRULAMA — 16 hücre × 5 kullanım oranı (Round 2 ile yan yana)

### A) Round 2'nin yayınladığı tablo (v4 modeli — ARTIK BAYAT, model hatalı)
| senaryo/bant/P | u=0 | 25% | 50% | 75% | 100% | OPT |
|---|---|---|---|---|---|---|
| Norm/strict P1 | **768** | 654 | 299 | 145 | İFLAS | 0% |
| Norm/strict P2 | 1844 | **1983** | 1199 | 100 | İFLAS | 25% |
| Norm/strict P3 | **3308** | 3174 | 1482 | İFLAS | İFLAS | 0% |
| Norm/strict P4 | **5087** | 4776 | 2275 | İFLAS | İFLAS | 0% |
| Norm/opt P1-P4 | — | — | — | — | 4/4 İFLAS | 25-50% |
| Slow/opt P1-P4 | — | — | — | — | 4/4 İFLAS | 0-50% |
→ **u=%100: 15/16 hücre İFLAS**, OPT banda göre %0-50 arası kayıyor.

### B) ÖNERİ SONRASI, düzeltilmiş v5 modeli, CANLI kira `{500,1000,1450,1800}`
| senaryo | bant | P | u=0 | u=25% | u=50% | u=75% | u=100% | **OPT** | u25 vs u0 |
|---|---|---|---|---|---|---|---|---|---|
| Normal | strict | 1 | 770 | **921** | 750 | 493 | 368 | **25%** | +20% |
| Normal | strict | 2 | 1844 | **2549** | 2266 | 1741 | 1628 | **25%** | +38% |
| Normal | strict | 3 | 3308 | **3964** | 3086 | 1945 | 1741 | **25%** | +20% |
| Normal | strict | 4 | 5087 | **5700** | 4260 | 2549 | 2191 | **25%** | +12% |
| Normal | optimistic | 1 | 3071 | **3502** | 3307 | 3042 | 2251 | **25%** | +14% |
| Normal | optimistic | 2 | 7764 | **8333** | 7741 | 6071 | 5561 | **25%** | +7% |
| Normal | optimistic | 3 | 8770 | **9022** | 7423 | 5360 | 5008 | **25%** | +3% |
| Normal | optimistic | 4 | 10102 | **10139** | 8092 | 5520 | 5071 | **25%** | +0% |
| Slow | strict | 1 | İFLAS g12 | İFLAS g16 | İFLAS g16 | İFLAS g16 | İFLAS g16 | (kırık bant) | — |
| Slow | strict | 2 | İFLAS g12 | İFLAS g12 | İFLAS g12 | İFLAS g16 | İFLAS g16 | (kırık bant) | — |
| Slow | strict | 3 | İFLAS g12 | İFLAS g12 | İFLAS g12 | İFLAS g12 | İFLAS g12 | (kırık bant) | — |
| Slow | strict | 4 | İFLAS g12 | **100** | İFLAS g12 | İFLAS g12 | İFLAS g12 | 25% | — |
| Slow | optimistic | 1 | 1097 | 1133 | **1210** | 1008 | 748 | 50% | +3% |
| Slow | optimistic | 2 | 2804 | **2835** | 2513 | 2287 | 2269 | **25%** | +1% |
| Slow | optimistic | 3 | **2673** | 2637 | 1965 | 1470 | 1461 | 0% | −1% |
| Slow | optimistic | 4 | **3121** | 2850 | 1828 | 1079 | 1066 | 0% | −9% |

**Hedefler karşılandı mı?**
- ✅ **u=%100 hiçbir çözünür hücrede İFLAS ETTİRMİYOR** (Round 2: 15/16 iflas). Tepe
  noktanın **−%33…−62 altında** kalıyor → hâlâ açıkça kötü, "dikkatli kullan" dersi duruyor.
- ✅ **"Hiç kullanma" artık 16/16'da optimal DEĞİL**; 12 çözünür hücrenin **10'unda OPT=%25**.
- ✅ STRICT bandın 4/4 hücresinde ölçülü kullanım **+%12…+38** — Round 2'de −%15…−%22'ydi.
- ⚠️ 2 hücre (Slow/opt P3/P4) hâlâ %25'te hafif negatif (−%1/−9). Ledger modda (grace
  artifaktı olmadan) 21-noktalı ızgarada bu hücrelerin **gerçek tepesi %10** ve orada
  değer POZİTİF (+%4/−%2) — yani "az kullan" kuralı orada da geçerli, sadece dozu daha küçük.
- Plato genişliği (tepenin ≥%95'i, ledger): 10 hücrede **%10-35** aralığını kapsıyor →
  **öğretilebilir tek kural: "kabaca her 4 müşteriden birini telefonla çağır"**
  (P1 günde ~1, P2-P4 günde ~2.5 çağrı).

### C) ÇAPRAZ KONTROL — Round 3'ün kirası `{290,650,1140,1630}` UYGULANIRSA
16/16 hücre çözünür oluyor; Normal bandın 8 hücresinde **OPT hâlâ %25** (kira değişikliği
telefon eğrisini bozmuyor). **AMA Slow/strict'te (Round 3'ün kurtardığı bant) OPT %80-90'a
fırlıyor** (P1 +%130, P2 +%211): o bantta tır geliri günde yalnız 174-228 TL, telefonun düz
20 TL×çağrı geliri **net gelirin %21-25'i** oluyor → "telefonu spam'le" baskın strateji.

⚠️ **ROUND 10 İÇİN ZORUNLU NOT:** Round 3 kirası uygulandıktan SONRA telefon matrisi
TEKRAR koşulmalı. Kök neden telefon sabitleri değil — Slow/strict'in mekanik verim açığı
(Round 2 §3/§4: ~%25-30 seviye açığı). Düz `callMoneyReward`'ı düşürmek çözmez (para=15'te
bile pay %20 ve OPT %85), sadece Normal bandı tekrar tuzağa çevirir. Yapısal seçenek
(ÖNERİ DEĞİL, not): çağrı ödülünü düz TL yerine `rewardPerBox`'a oranlamak.

Ek: Slow/strict P4'te C tablosunda **monotonluk kırılması** (531→300→528→87) var — bu
Round 2 §5'in grace "fakir kal" artifaktı, telefonla ilgisi yok.

---

## 8. CUSTOMER SUPPORT — ÖNERİ: cooldown yerine ZAMAN maliyetini yarıya indir

Bugünkü hâli (`PhoneCallManager.cs:303-309`, cooldown ×0.5) Round 5'in gösterdiği gibi
**mekanik NO-OP**, ama takvimde `EventType.Positive` ve metni
(`EventCalendarUI.cs:178`, "RECEPTION PHONE COOLDOWN IS CUT IN HALF") oyuncuyu o gün
telefona yüklenmeye çağırıyor. Düzeltilmiş modelle **zararı doğrulandı**
(taban u=%20, event gününde u=%100):

| | mevcut T ile ortalama | en kötü tek gün |
|---|---|---|
| bugünkü CUSTOMER SUPPORT | **−16 … −463 TL** | **g15: −654 TL** (Norm/opt P4) |

Round 5'in −45…−777 TL aralığı büyüklük mertebesi olarak doğrulandı.

**SEÇENEK A (ÖNERİLEN): o gün `timeSkipAmountByPlayerCount` ×0.5 (cooldown'a dokunma).**
Oyuncu rasyonel davrandığında (o gün u'yu yükseltir) ölçülen etki:

| hücre | ortalama event kazancı | en iyi tek gün | günlük gelirin %'si | o günün optimal u'su |
|---|---|---|---|---|
| Norm/strict P1 | +48 | g7: +78 | %24 | %100 |
| Norm/strict P2 | +142 | g1: +224 | %31 | %100 |
| Norm/strict P4 | +129 | g7: +304 | %14 | %100 |
| Norm/opt P2 | +211 | g1: +385 | %25 | %100 |
| Slow/opt P1 | +88 | g6: +119 | **%43** | %100 |
| Slow/opt P4 | +86 | g9: +107 | %12 | %80 |

→ Tek-gün etkisi **+%12…+43** (16 hücre ort. ~%23). Round 5'in event merdiveninde
sağlıklı orta sıra: FESTIVAL DAY'in (+%61, ~6x outlier) **çok altında**, MARKETING DAY
(−%19) ile aynı mertebede. **Yan etkisi yok** (yalnız o günü etkiliyor, kira/kota/prestij
kollarına dokunmuyor) ve tabelasıyla mekaniği ilk kez UYUMLU hale getiriyor.

Tasarım bonusu: oyunun **tek günü** "telefonu spam'le" doğru cevap oluyor → mekaniği
karşıtlık yoluyla öğretiyor.

**SEÇENEK B (telefonla ilgisiz bir etkiye yönlendirme): ÖNERİLMİYOR.** Event'in adı,
ikonu (`EventCustomerSupport`) ve lokalizasyon anahtarı (`EventCustomerSupportDesc`) telefona
bağlı; kopartmak 3 asset + 17 dil lokalizasyon değişikliği demek, üstelik Round 5'in
"RELAXED DAY Normal bantta tam sıfır" boşluğunu kapatmıyor.

**Uygulama notu:** `GetEffectiveCooldownSeconds`'ın `IsEventActive(CUSTOMER_SUPPORT_EVENT)`
dalı KALDIRILIP aynı kontrol `ExecuteCall`'daki `TimeSkipAmountMinutes` okumasına
taşınmalı (`PhoneCallManager.cs:434` ve guard'daki `:416` — **İKİSİ DE**, yoksa 17:30
guard'ı yanlış hesaplar). Lokalizasyon metni de değişmeli.

---

## 9. Model açıkları (Round 8+ için not)

1. Harness `wrongProductPrestigePenalty`'yi ve oyuncu tepki gecikmesini modellemiyor
   (Round 6 §9 ile aynı) → `lost` sayıları ALT SINIR.
2. `phoneUseRate` "kotanın u kadarını telefonla çek" soyutlaması; gerçek oyuncu çağrıyı
   kuyruk boşken yapar (daha akıllı) → gerçek optimal u ölçülenden biraz YÜKSEK olabilir.
   Yön güvenilir, tepe noktanın yeri ±%10.
3. `ASSUMED4.phoneUseRate` (strict 0.60 / optimistic 0.10) varsayımı ARTIK BAYAT: bu
   ölçümle strict'te de optimistic'te de gerçekçi davranış **%10-25**. Round 8+
   `runFullSim` varsayılanını kullanacaksa bunu bilmeli.
4. `sim.js`'in `runFullSim`'i bu 3 model hatasını HÂLÂ taşıyor — **Round 10 sim.js'i de
   güncellemeli**, yoksa gelecekteki her telefon ölçümü yanlış çıkar.

İlgili: [[economy_full_balance_round1_2026-08-30]], [[economy_full_balance_round2_2026-08-30]],
[[economy_full_balance_round5_2026-08-30]], [[economy_full_balance_round6_2026-08-30]],
[[phone_cooldown_perk_event_stacking_2026-08-29]], [[plateup_customer_quota_2026-08-29]],
[[perk_card_absolute_assignment_conflict]], [[money_comes_only_from_trucks]]
