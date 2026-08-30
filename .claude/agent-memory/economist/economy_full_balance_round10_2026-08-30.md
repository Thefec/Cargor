---
name: economy-full-balance-round10-2026-08-30
description: Round 10 FINAL - sim.js runFullSim v5.0 (5 model hatasi duzeltildi) + Round 3/4/5/6/7/8'in TUM birikmis onerilerinin TEK birlesik kosumda dogrulanmasi; 12 UYGULA / 11 UYGULAMA kararli nihai uygulama listesi (dosya:satir + eski->yeni + EconomyInvariantCheck etkileri)
metadata:
  type: project
---

# Round 10 — Final tam-sweep + nihai uygulama listesi (2026-08-30, Opus)

Takip: `plans/economy-full-balance-2026-08-30.md`. **Oyun kodu DEĞİŞMEDİ** (`.cs`/`.asset`/
sahne yalnız OKUNDU). Değişen tek dosya: `tools/economy-sim/sim.js` (v4.0 → **v5.0**).

---

## 1. ⭐ `sim.js` `runFullSim` v5.0 — 5 MODEL HATASI DÜZELTİLDİ

| # | hata | eski (v4) | yeni (v5) | kaynak |
|---|---|---|---|---|
| 1 | telefon zaman maliyeti | `phoneCalls × I` (tam varış aralığı) | `phoneCalls × T[P]×0.30303` (TABAN 200s dönüşümü, günden bağımsız) | Round 7 §1a |
| 2 | çift sayım | `min(dayDur, naturalEnd) − atlanan` | `min(dayDur − atlanan, naturalEnd)`, erken bitiş yalnız kota tam spawn olduysa | Round 7 §1b |
| 3 | forced-spawn kredisi | `arrivalCap` telefondan bağımsız | `arrivalCap = 1 + spawnWin/I + phoneCalls` | Round 7 §1c |
| 4 | **YENİ (Round 7 harness'ında da yoktu)** — servis penceresi | atlanan saniyeler yalnız TIR penceresini kısaltıyordu | `serveWinEff = serveWin − atlanan` (servis de gerçek-zamanlı iş) | Round 10 |
| 5 | quest karar modeli | "havuzun en iyi 3'ünün ORTALAMASI" | N rastgele teklifin **MAKSİMUMU** (sıralama-istatistiği ile tam hesap) + canlı TIER-DÜZ ödül tablosu + gün-16 cezasız settlement | Round 8 §1/§10 |

Ayrıca `ASSUMED4.phoneUseRate` **0.60/0.10 → 0.20/0.20** (Round 7 §8: gerçekçi davranış her
iki bantta %10-25) ve `QUEST_ASSETS` ödül kolonu 30/30 asset grep'lenerek resenkronlandı
(Easy 28/1.4/15/0.8 · Medium 60/3.0/27/1.36 · Hard 150/7.5/53/2.66; `targetCount`/`type`/
`colorLocked` zaten doğruydu).

**Yeni parametrik `opts`** (öneriler koda dokunmadan test edilebilir):
`timeSkipAmountByPlayerCount`, `phoneTimeSkipPerkMultiplier`, `callMoneyReward`,
`callPrestigeReward`, `dailyQuestCount`, `questAssets`, `questPrestigeScale`,
`prestigePerBonus`, `wrongProductPrestigePenalty`, `wrongProductRate`, `day16Settlement`.

### 1a. Düzeltme #4'ün Round 7'ye göre yön farkı (önemli)
Round 7'nin harness'ı servis penceresini kısaltmıyordu → **optimistic bantta telefon
"bedava para" görünüyordu**. v5 ile Normal/optimistic P3/P4 ve Slow/optimistic P2-P4'te
optimum **%0** çıkıyor (Round 7 tablosu %25 diyordu). Yön aynı, dozaj daha muhafazakâr.
v5'in u=0 rakamları Round 7 tablo B ile **%1-6** içinde (fark quest karar modelinden).

---

## 2. ⭐ NİHAİ UYGULAMA LİSTESİ — **UYGULA** (12 madde)

> Yazım yeri kuralı (Round 9 §5): `EkonomiAyarlari.asset` yeni alanların çoğunu İÇERMİYOR →
> canlı olan `GameEconomySettings.cs` field initializer'ı. Asset'e yeni anahtar EKLEMEYİN
> (`float[]` hex tuzağı → sessizce BOŞ dizi). Asset'te ZATEN VAR olan anahtarlar
> (`baseRentByPlayerCount`, `wrongProductPrestigePenalty`) **hem `.cs` hem asset**'te değişmeli.

| # | ne | dosya:satır | eski → yeni | round | gerekçe (ölçüm) |
|---|---|---|---|---|---|
| **U1** | `baseRentByPlayerCount` | `GameEconomySettings.cs:21` **VE** `Assets/Resources/EkonomiAyarlari.asset:15` | `{500,1000,1450,1800}` → **`{290,650,1140,1630}`**<br>asset hex: `f4010000e8030000aa05000008070000` → **`220100008a020000740400005e060000`** | R3 | Slow/strict **4/4 hücre iflastan kurtuluyor** (grace VARKEN de YOKKEN de), final 304-612 TL, min kira-sonrası kasa 304-540. §3'e bak (yan etki UYARISI var). |
| **U2** | `timeSkipAmountByPlayerCount` | `GameEconomySettings.cs:117` (asset'te YOK) | `{115f,59f,55f,55f}` → **`{115f,49f,47f,47f}`** | R7 | v5 ile yeniden doğrulandı: Normal/strict P2-P4'te u=%20 kazancı **+18/+15/+8% → +29/+22/+14%**; u=%100 cezası −682/−1295 TL'den +710/+555'e. **P1 BİLEREK DEĞİŞMİYOR** (eğrisi zaten sağlıklı). |
| **U3** | CUSTOMER SUPPORT etkisi | `PhoneCallManager.cs:303-309` dalı KALDIR → `ExecuteCall:434` **VE** guard `:416` (İKİSİ DE) | cooldown ×0.5 → **`TimeSkipAmountMinutes` ×0.5** | R7 §8 | Bugünkü hâli mekanik NO-OP ama "pozitif" tabelasıyla spam'e çağırıyor (−16…−463 TL/gün). Yeni hâli +%12-43/gün, FESTIVAL'in çok altında. `EventCalendarUI.cs:178` metni + `EventCustomerSupportDesc` lokalizasyonu da değişmeli. |
| **U4** | YENİ SO alanı `phoneTimeSkipPerkMultiplier` | `GameEconomySettings.cs` (yeni, varsayılan `1f`, asset'e EKLEME) + `PerkEffect.ApplyPhoneLine` (cs:298-301) mutlak atama **`0.80f`** | — → `0.80f` | R7 §6 | v5 ile doğrulandı: değer/maliyet **0 – 0.62 – 1.51x** (R7: 0.23–0.72–1.43 — tutarlı). `0.75` denendi: OPT'u %45-85'e fırlatıp "dikkatli kullan" dersini çözüyor. `0.85`: medyan 0.31, 4 hücrede sıfır. **0.80 doğru nokta.** Perk zayıf bulunursa kol FİYAT (160→120), çarpan DEĞİL. |
| **U5** | `phoneCooldownPerkBonusSeconds` (perkin atadığı) | `PerkEffect.cs:301` | `10f` → **`1f`** | R7 | 10f `Mathf.Max(1,3−10)` ile tabana çakılıyor. **Ekonomik değeri SIFIR, yalnız his** — öyle etiketlensin. |
| **U6** | `DAILY_QUEST_COUNT` | `QuestManager.cs:17` | `3` → **`3 + CurrentQuestTier`** | R8 §2 | v5 karar modeliyle: `T2 < T0` (ödenmiş kötüleştirme) **5/16 hücre → 3/16** ve büyüklük −30…−87'den −1…−36'ya iniyor. Normal/strict P1/P2: −30/−87 → **+24/+26**. D1 stratification KORUNSUN. ⚠️ R8'in −118…−480 rakamı bayat karar modelinden geliyordu, gerçek zarar daha küçük — düzeltme yine de doğru yönde. |
| **U7** | quest **prestij** ödül+ceza ×0.4 | 30 asset (`Assets/Resources/Quests/*.asset`) | Easy `1.4/0.8 → 0.6/0.32` · Medium `3.0/1.36 → 1.2/0.55` · Hard `7.5/2.66 → 3.0/1.05`. **Para tablosu (28/15 · 60/27 · 150/53) DEĞİŞMEZ.** | R8 §3 | T2+dqc=5 ile `maxPrestige=100` tavanına çarpan hücre **3/16 → 0/16** (max 94). Quest'in kasa katkısı 146-1189 → 135-675 TL (para ödülü aynı kalıyor, kesilen kısım görünmez prestij kanalı). |
| **U8** | telefon quest hedefleri | `Q_Easy_6_Phone.asset` `targetCount 2→1` · `Q_Medium_6_Phone.asset` `3→2` | | R8 §5 | v5 ile ölçüldü: telefon quest'inin kasaya katkısı u=%0-20'de **−4…−166 TL**, u=%60-100'de **+148…+312 TL** (Round 7'nin dersinin TERSİ). Hedef fix'iyle u=%20 katkısı 11/16 hücrede sıfıra/pozitife dönüyor. |
| **U9** | "Görev Kademesi" fiyatı | sahne `The Main Office.unity` — `DifficultyManager.UpgradeCostMultiplier`'dan **MUAF** tut | ×`{1,2,2.95,3.7}` → **düz** | R8 §2 | Quest ödülleri **P-DÜZ**, fiyat P-ÖLÇEKLİ. Muafiyet L1 net değerini P2/P3/P4'te **+80/+156/+216 TL** iyileştiriyor; L1 net pozitif hücre 5/16 → **7/16**, L2 5/16 → **8/16**. (Kalan negatiflik strict bantta — beceri kapısı, kabul edilebilir.) |
| **U10** | `CalculateEffectiveTargetCount` muafiyet listesi | `QuestManager.cs:569-583` | + `CompleteSpecificColorTruck` (type 6) | R8 §7 | Tetikleyici CANLI (`Truck.cs:656`), yalnız asset'i yok. Renk-kilitli tır quest'i eklenirse D2 çifte-ölçekleme bug'ı aynen geri gelir. Ucuz sigorta. |
| **U11** | `cheap_rent` stale baseline | `PerkEffect.cs:194` | `1.15f - 0.03f*level` → **`1.20f - 0.03f*level`** | R4 §5 | v5 + R3 kirasıyla ölçüldü: kod L1'de **2.55-2.61 kat** niyet edilenden güçlü (kazanç 171-959 TL, niyet 67-371 TL). Müdür düzeltmesi: `long_queue` iddiası YANLIŞTI, listeye GİRMİYOR. |
| **U12** | `wrongProductPrestigePenalty` | `GameEconomySettings.cs:150` **VE** `EkonomiAyarlari.asset:38` | `-0.08f` → **`-0.20f`**<br>(tercih edilen: yanlış ürünü de `OnCustomerLost` yolundan geçir, o zaman ayrı sabit gereksiz) | R6 §4 | Ceza müşteri kaybının (−0.4) 5x altında + müşteri ANINDA çıkıyor → iade modunda **"bilerek yanlış ürün ver" baskın strateji**. Nakit etkisi ölçüldü: **%0…−1** (bir hücrede −10) → değişiklik bedava. Gerçek hedef `CustomerAI.ProcessReturnBoxInteraction` (cs:1228-1259). |

### 2b. Ucuz temizlik (ekonomik etki YOK, aynı turda yapılsın)
- `timeSkipAmountByPlayerCount` **tooltip'i** (`GameEconomySettings.cs:116`): "atlanan oyun-dakikası" yalnız gün 1-3'te doğru; gün 16'da 115 dk fiilen ~70 oyun-dakikası. (Round 7 §1a)
- `EkonomiAyarlari.asset`'te **3 ölü V3 anahtarı** SİL: `phoneRingChancePerHour`, `phoneRingEventMultiplier`, `phoneRingPerkBonus` (sınıfta karşılığı yok).
- `CustomerManager.cs:409` yorumu bayat ("örn. INTENSIVE DAY" — öyle bir event yok).
- `GameStateManager.CheckWinCondition` docstring'i (cs:691,706) kodla çelişiyor (kod yalnız `currentDay >= MAX_DAYS` bakıyor).
- `phone_line` perkinin sahne `contentText`'i V3 metni ("Saatte yapabileceğin telefon sipariş sayısı +1") — U4 ile birlikte güncellensin.

---

## 3. ⚠️ U1'İN (kira) YAN ETKİSİ — kullanıcı kararı gereken TEK nokta

Round 3, "Normal bantlar yalnız %7-18 şişiyor" demişti. **Bu rakam bayat telefon modeliyle
ve `phoneUseRate=0.60` varsayımıyla üretilmişti.** v5 + gerçekçi %20 kullanımla:

| bant | ÖNCE (canlı kira) | SONRA (R3 kira) | şişme |
|---|---|---|---|
| Normal/strict P1 | 979 | **2106** | **+115%** |
| Normal/strict P2 | 2562 | 4441 | +73% |
| Normal/strict P3 | 4046 | 5710 | +41% |
| Normal/strict P4 | 5871 | 6783 | +16% |
| Slow/strict P1-P4 | **4/4 İFLAS** | **484 / 612 / 433 / 304** | kurtarıldı |

Şişme P1'de en büyük çünkü Slow/strict P1'i kurtarmak için P1 tabanının en çok inmesi
gerekiyor ve aynı taban Normal bandını da besliyor. **Final kasa bir skor değil, upgrade
bütçesi** — yani bu, Round 4'ün perk/upgrade fiyatlandırmasını da yukarı kaydırır.

**ALTERNATİF (eşik-tabanlı, minimum-uygulanabilir): `{350, 730, 1190, 1650}`**
Slow/strict'i marj **162-197 TL** ile (cliff kenarı) kurtarır, Normal/strict P1 şişmesini
+%115 → **+%82**'ye indirir. Round 3'ün 304-612 marjı daha güvenli.
→ **ÖNERİ: `{290,650,1140,1630}` (U1) uygulansın**, playtest'te Normal bandın fazla kolay
geldiği görülürse ikinci tur ayarı `{350,730,1190,1650}` olsun. Ara değer taraması gerekirse
`runFullSim(..., {baseRentByPlayerCount:[...]})` ile tek satırda koşulabilir.

---

## 4. ⭐ **UYGULAMA** — reddedilen 11 öneri (hepsi ölçümle)

| # | öneri | round | RED gerekçesi (ölçüm) |
|---|---|---|---|
| **R1** | `callMoneyReward` 20 → 0/10/12/15 | R7/R10 | Dördü de test edildi. `0`: Normal/strict u=%20 kazancı +12/18/14/10% → **−4/+2/+1/−0%** (telefon tamamen ölüyor) ve Slow/strict 3/4 hücre TEKRAR iflas ediyor. `10`: kazançlar yarıya iniyor, Slow/strict P4 iflas. `12`/`15`: Slow/strict P1-P2 spam dominansını ÇÖZMÜYOR ama Normal bandı %4-5 zayıflatıyor. **20 KALSIN.** |
| **R2** | `callPrestigeReward` 0.4 → 0.2/0.3 | R6/R7 | Round 7'de ölçüldü, v5'te doğrulandı: `maxPrestige` hiç dolmuyor (final 22-82), 0.2'ye indirmek 5 hücrede u=%20 kazancını negatife çeviriyor. **0.4 KALSIN.** |
| **R3** | `phoneCooldownSeconds` 3 → başka değer | R7 | Cooldown 16/16 hücrede bağlayıcı DEĞİL (gerçek kapı `IsQueueFull`/`HasUnspawnedCustomers`). Ekonomik olarak nötr, kullanıcı kararı. **3 KALSIN.** |
| **R4** | `prestigePerBonus` 8 → 10 | R6 §6 | **v5 ile ölçüldü: −%7…−%111.** Slow/strict P4'ü 304 → **−32** ile İFLASA sürüklüyor, Slow/strict P3'ü −%65. Tam paket üzerine bindirildiğinde tüm bantları %7-15 kesiyor. `prestige_master`'a baş boşluğu açmak bu bedeli hak etmiyor. **REDDEDİLDİ.** |
| **R5** | `SkipTime`'ı `CurrentDayDuration`'a çevirmek | R7 §1a ⚠️ | "Bug gibi görünen" TABAN-200s dönüşümü **KORUNMALI**. Çevrilirse geç-oyun çağrı maliyeti +%65 artar (gün 16'da 34.85 → 57.5 sn) ve Round 1/2'nin TRAP'i GERÇEKTEN ortaya çıkar. **DOKUNMAYIN.** |
| **R6** | 2. servis istasyonunu açmak (`serviceTables[1]`) | eski hafıza notu | **v5 ile ölçüldü ve ÇÜRÜDÜ.** Slow/strict'i KURTARMIYOR (P1/P2/P3 hâlâ g12-16 iflas — o bantta bağlayan kol `stationCap` değil `laborCap`), ama optimistic bandı **+%18…+%267** şişiriyor (Slow/opt P4: 2299 → **8438**). Kira kesintisinin yerine geçemez; ayrı ve çok daha büyük bir denge olayı. |
| **R7** | FESTIVAL DAY tabanını değiştirmek | R5 §1 | **U1 bunu bedavaya çözüyor.** R3 kirasıyla FESTIVAL bonusu 75-423 TL = geç-gün net gelirinin **%25-32'si**, üstelik P1→P4 boyunca DÜZ. Round 5'in +%28…+109 outlier'ı kalmadı. Ayrı değişiklik gereksiz. |
| **R8** | `INITIAL_POSITIVE_EVENT_COUNT` / negatif garanti dengesi | R5 §4 | Net para katkısı +%0.6…+2.7 ve bunun ~%80-100'ü FESTIVAL'den geliyordu — U1 sonrası FESTIVAL sönümlendiği için asimetri de sönüyor. Enflasyon riski yok. **Playtest hissine bırakılsın.** |
| **R9** | Gün-16 quest settlement "exploit"i | R8 §8 | v5'te modellendi: kasanın **%0.1-8.2'si**. Denge riski yok. **Düzeltme önerilmiyor.** |
| **R10** | Kazanma koşuluna `prestij ≥ 30` kapısı | R6 §2 | **v5 ile ölçüldü ve TERS TEPİYOR.** Tam paket sonrası Slow/strict final prestij **22 / 40 / 56 / 55**. Bir ≥30 kapısı, U1 ile yeni kurtardığımız Slow/strict P1'i (22) tekrar KAYBETTİRİR. Prestij fail-state'i ölü kalsın ya da eşik ≤15 olsun. |
| **R11** | `long_queue` "stale baseline" düzeltmesi | R4 §5 | Müdür doğrulaması: iddia YANLIŞ (`CustomerManager.DEFAULT_QUEUE_SIZE + 2` sabiti dinamik okuyor, taban gerçekten 2). Yalnız yorum satırı bayat. **Kod değişikliği YOK.** |

---

## 5. `Assets/Editor/EconomyInvariantCheck.cs` — güncellenecek `Expect*` satırları

| satır | mevcut iddia | U# sonrası |
|---|---|---|
| 259 | `ExpectIntArray("baseRentByPlayerCount", ..., {500,1000,1450,1800})` | **`{290,650,1140,1630}`** (U1) |
| 305-306 | `ExpectArray("timeSkipAmountByPlayerCount", ..., {115f,59f,55f,55f})` | **`{115f,49f,47f,47f}`** (U2) |
| 311 | `ExpectFloat("GetTimeSkipAmountMinutes(4)", ..., 55f)` | **`47f`** (U2) |
| 300 | `ExpectFloat("wrongProductPrestigePenalty", ..., -0.08f)` | **`-0.20f`** (U12) |
| 317-320 | `GetBaseRent(1)=500`, `GetBaseRent(4)=1800`, `GetBaseRent(9)[clamp]=1800`, `GetBaseRent(0)[clamp]=500` | **290 / 1630 / 1630 / 290** (U1) |
| 336 | `ExpectPristine("phoneCooldownPerkBonusSeconds", ..., 0f, "phone_line")` | DEĞİŞMEZ (0f = satın-alma öncesi statik varsayılan, doğru) |
| 383-387 | quest tier ödül sözlüğü `{0:(28,15,1.4,0.8)}, {1:(60,27,3,1.36)}, {2:(150,53,7.5,2.66)}` | prestij kolonları **`1.4→0.6/0.8→0.32` · `3→1.2/1.36→0.55` · `7.5→3.0/2.66→1.05`** (U7). **Para kolonları DEĞİŞMEZ.** |
| — | `phoneTimeSkipPerkMultiplier` | **YENİ** `ExpectPristine(..., 1f, "phone_line")` eklensin (U4) |
| — | quest `targetCount` | assert YOK → U8 için değişiklik GEREKMİYOR |
| 179 | `GetTimeSkipAmountMinutes(1)=115f` | DEĞİŞMEZ (P1 bilerek sabit) |

`prestigePerBonus` (satır 296, `8f`), `penaltyPerBox` (288, `40`), `callMoneyReward` (308),
`callPrestigeReward` (309), `phoneCooldownSeconds` (307) → **HEPSİ DEĞİŞMEZ** (§4).

---

## 6. Etkileşim doğrulaması — birleşik paket 16/16 hücrede

Tam paket (U1+U2+U7+U8, telefon %20, quest tier 0, grace VAR **ve** YOK):

| hücre | ÖNCE | SONRA | grace YOK | final prestij | telefon OPT |
|---|---|---|---|---|---|
| N/s P1-P4 | 979 / 2562 / 4046 / 5871 | 2106 / 4441 / 5710 / 6783 | aynı | 43 / 77 / 81 / 80 | %15 / %20 / %20 / %15 |
| N/o P1-P4 | 3453 / 7933 / 8311 / 9384 | 4580 / 9812 / 9975 / 10296 | aynı | 52 / 81 / 82 / 82 | %30 / %10 / %0 / %0 |
| S/s P1-P4 | **İFLAS ×4** | **484 / 612 / 433 / 304** | aynı | 22 / 40 / 56 / 55 | %100 / %100 / %20 / %15 |
| S/o P1-P4 | 1164 / 2644 / 2131 / 2299 | 2291 / 4523 / 3795 / 3211 | aynı | 50 / 59 / 56 / 56 | %30 / %0 / %0 / %0 |

**Hiçbir öneri diğerini bozmuyor**; iki POZİTİF etkileşim ölçüldü:
1. U1 (kira) FESTIVAL DAY outlier'ını da söndürüyor (§4-R7).
2. U2 (telefon T) + U8 (quest hedefi) birlikte, u=%20'de quest'in telefonu spam'e itme
   baskısını 11/16 hücrede sıfırlıyor.

Bir NEGATİF etkileşim (kabul edilen, çözülmeyen):
3. **U1 sonrası Slow/strict P1/P2'de telefon OPT = %100** (spam baskın). Kök neden telefon
   sabiti DEĞİL: o bantta tır verimi mekanik-bağlı olduğu için günü kısaltmak hiçbir kutu
   kaybettirmiyor, düz 20 TL/çağrı ise günlük gelirin **%60-70'i** oluyor (gün 16, P1: telefon
   120 TL vs tır 48 TL). §4-R1'de dört farklı `callMoneyReward` denendi, hiçbiri Normal bandı
   bozmadan çözmüyor. **Kalan yapısal seçenek (ÖNERİ DEĞİL, not):** çağrı para ödülünü
   yalnızca çağrılan müşteri SERVİS EDİLİRSE ver — o zaman ödül otomatik olarak kapasiteye
   oranlanır ve spam kendini finanse edemez.

---

## 7. ⭐ Bu round'da ÇÜRÜYEN / DEĞİŞEN önceki bulgular

1. **Round 8 §2'nin "Görev Kademesi L2 net −118…−480 TL" rakamı BAYAT** (avg-of-top-3 karar
   modelinden geliyordu). Gerçek zarar **−30…−87 TL**. Sonuç (T2<T0, düzeltme gerekli) aynı,
   büyüklük 4-6 kat küçük → aciliyet düşük.
2. **Round 7 §7C'nin "Slow/strict spam dominansı Round 3 kirasının YAN ETKİSİ" atfı YANLIŞ.**
   v5'te aynı dominans CANLI kirayla da var (ledger modda OPT=%100) — kira onu YARATMIYOR,
   yalnız bandı çözünür yapıp GÖRÜNÜR kılıyor.
3. **Round 7 §7B tablosu optimistic bantta FAZLA İYİMSER** (servis penceresi kısalması
   modellenmemişti). N/o P3/P4 ve S/o P2-P4'te optimum %25 değil **%0**.
4. **Round 3'ün "Normal bantlar %7-18 şişiyor" iddiası bayat** — gerçek **%16-115** (§3).
5. **"2 paralel servis istasyonu Slow/strict'i çözer" (eski hafıza notu) ÇÜRÜDÜ** (§4-R6).
6. **"Para YALNIZ tırdan gelir" invariant'ı ARTIK DOĞRU DEĞİL.** `PhoneCallManager.
   ExecuteCall` (cs:451-457) koşulsuz `AddMoney(20)` yapıyor → telefon oyunun **ikinci ve
   koşulsuz para musluğu**. Mekanik-bağlı bantlarda birinci gelir kaynağına dönüşebiliyor (§6.3).
7. **Round 6 §2'nin "kazanma eşiği anlamlı hale getirilsin (prestij ≥ 30)" önerisi TERS
   TEPİYOR** — paket sonrası Slow/strict P1 final prestiji 22 (§4-R10).

---

## 8. Kalan model açıkları (gelecek turlar için)
1. Oyuncu tepki gecikmesi ve `HandleFailedInteraction` kaskadı modellenmiyor → `lost`/
   `missedQuota` sayıları hâlâ **ALT SINIR**.
2. `phoneUseRate` "kotanın u kadarını telefonla çek" soyutlaması; gerçek oyuncu çağrıyı
   **kuyruk boşken** yapar (daha akıllı) → gerçek optimum ölçülenin biraz üstünde olabilir.
   Öğretilebilir kural v5'te netleşti: **"boşta beklerken çevir, kuyruk doluyken çevirme"**.
3. `questCompletionProb`'un doygunluk platosu (`ratio≥1.5 → 0.95`) ve
   `ASSUMED.questExecutionFriction` hâlâ ölçüme dayanmıyor — quest sonuçlarının YÖNÜ
   güvenilir, BÜYÜKLÜĞÜ değil.
4. Event'ler `runFullSim`'de hâlâ modellenmiyor (Round 5 ayrı harness'la ölçtü).
5. `wrongProductRate` kanalı v5'te VAR ama varsayılan **0** (kapalı) — açılırsa taban koşum
   değişir, karşılaştırmalarda aynı değer kullanılmalı.

İlgili: [[economy_full_balance_round1_2026-08-30]], [[economy_full_balance_round2_2026-08-30]],
[[economy_full_balance_round3_2026-08-30]], [[economy_full_balance_round4_2026-08-30]],
[[economy_full_balance_round5_2026-08-30]], [[economy_full_balance_round6_2026-08-30]],
[[economy_full_balance_round7_2026-08-30]], [[economy_full_balance_round8_2026-08-30]],
[[economy_full_balance_round9_2026-08-30]], [[money_comes_only_from_trucks]],
[[serial_customer_service_ceiling]], [[perk_card_absolute_assignment_conflict]]
