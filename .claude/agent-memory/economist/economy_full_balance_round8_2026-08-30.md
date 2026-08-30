---
name: economy-full-balance-round8-2026-08-30
description: Round 8 quest sistemi - sim.js QUEST_ASSETS odul tablosu BAYAT (2026-08-06'da degisti), "Gorev Kademesi" upgrade'i STRICT bantta NEGATIF (havuz seyrelmesi), quest prestiji para odulunun 0.23-1.88 kati gizli deger, AnswerPhone quest'i telefon spam'ini odullendiriyor, D2 no-op DOGRULANDI, gun-16 exploit'i KAPANMIS
metadata:
  type: project
---

# Round 8 — Quest sistemi ekonomisi (2026-08-30, Opus)

**Kapsam:** `Assets/Scripts/Quest` (legacy klasor) + `Assets/Resources/Quests` (30 asset) ödülleri
Round 1-7'de kalibre edilen ana ekonomiyle (kutu ödülü, kira, prestij, telefon) tutarlı mı.

**Kod DEĞİŞMEDİ** (`.cs`/`.asset`/sahne/`sim.js` hiçbiri). Ölçüm scratchpad harness'ı ile:
`sim.js`'in export ettiği `SRC4`/`ASSUMED`/`ASSUMED4`/`fullCustomerDay`/`truckThroughputWindowed`/
`questCompletionProb` kullanılarak `runFullSim`'in **quest-parametreli kopyası** yazıldı;
`questModel:'sim'` modunda **48/48 hücrede (16 hücre × 3 questTier) `runFullSim` ile birebir aynı**
sonuç verdiği doğrulanarak kalibre edildi.

**Kullanılan kira: CANLI `{500,1000,1450,1800}` / g=1.20** — Round 3 önerisi HÂLÂ UYGULANMADI
(kod okunarak çapraz kontrol edildi). Telefon: `ASSUMED4.phoneUseRate` (strict 0.60 / opt 0.10)
varsayılan; §5'te 5 orana taranarak izole edildi.

---

## 1. ⭐ `sim.js` QUEST_ASSETS ÖDÜL TABLOSU BAYAT — Round 4 §3 bu yüzden yanlış çerçevelendi

`tools/economy-sim/sim.js:494-531` 2026-07-28 tablosunu (`quest_fixed_reward_table_2026-07-28`)
taşıyor. Canlı asset'ler **2026-08-06'da (`975f011`, FAZ4 §D#6) yeniden yazıldı** ve artık
tier başına **DÜZ** (grup ayrımı base/premium/phone KALKTI — 30 asset'in hepsi tier'ının değerini alıyor):

| tier | sim.js (bayat, base) | CANLI asset | para ×  | prestij × |
|---|---|---|---|---|
| Easy   | 18 TL / 0.4 prestij | **28 / 1.4** | ×1.56 | **×3.50** |
| Medium | 34 / 0.8            | **60 / 3.0** | ×1.76 | **×3.75** |
| Hard   | 57 / 1.5            | **150 / 7.5**| ×2.63 | **×5.00** |

Ceza (POZİTİF yazılır, `QuestData.Build` `-Mathf.Abs()` uygular): Easy 15/0.8 · Medium 27/1.36 ·
Hard 53/2.66. Ceza/ödül oranı tier ile **DÜŞÜYOR** (0.54 → 0.45 → 0.35) — yani Hard tier hem
2.5× ödüllü hem oransal olarak daha az cezalı.

`targetCount`'lar sim.js ile canlı asset'te AYNI (Hard 12/5/3, Medium 7/3/2, Easy 4/2/6/1) —
yalnız ödül/ceza kolonu bayat.

**Round 4 §3 sonucu ("Görev Kademesi L2 ölçülebilir değeri SIFIR") reprodüksiyonu:** bayat tabloyla
`simT1 === simT2` **8/8 STRICT hücrede** doğru, **8/8 OPTIMISTIC hücrede YANLIŞ** (T2 %6-15 daha yüksek).
Yani Round 4'ün bulgusu bant-bağımlıydı, evrensel değil. Canlı tabloyla durum §3'te çok daha kötü çıkıyor.

**Round 10 aksiyonu:** `sim.js:494-531` ödül kolonları canlı asset'lerle senkronlansın, yoksa her
gelecek quest ölçümü yanlış çıkar (aynı sınıf: Round 1'in `runSim` bayatlığı).

---

## 2. ⭐ QUEST PRESTİJİ, PARA ÖDÜLÜNÜN 0.23-1.88 KATI — GÖRÜNMEZ VE DAHA BÜYÜK TERİM

Round 6 §1'in dönüşümüyle (1 prestij = `bonusPerTier/prestigePerBonus` = 0.625 TL/kutu ×
kalan gün) ölçülen, **gün 8'de 1 prestij puanının parasal değeri**: strict 4.5-22 TL, optimistic 19-37.5 TL.

Buna göre **Hard quest'in 7.5 prestijinin gün-8 parasal karşılığı**:

| bant | 7.5 prestij ≈ | görünen para | prestij/para |
|---|---|---|---|
| P1 Slow/strict   | 34 TL  | 150 | 0.23 |
| P1 Normal/strict | 56 TL  | 150 | 0.38 |
| P2 Normal/strict | 116 TL | 150 | 0.78 |
| P4 Normal/strict | 165 TL | 150 | **1.10** |
| P2/P3/P4 Normal/optimistic | 255-281 TL | 150 | **1.68-1.88** |

Yani optimistic bantta bir Hard quest'in gerçek değeri ~150 TL değil **~400 TL**, ve bunun
**%65'i kartta "+7.5 prestij" olarak yazan görünmez terim**. Aynı şey Easy (1.4p = 6-53 TL) ve
Medium (3.0p = 14-113 TL) için de geçerli.

**Enflasyon kanıtı:** questTier=2'de 16 günlük quest prestiji **1.6-48.1 puan** (`startingPrestige=12`'nin
0.1-4 katı, `maxPrestige=100`'ün %48'i). Prestij tavanına çarpan hücre sayısı **quest'siz 3/12 →
quest'li 6/12'ye çıkıyor** — Round 6 §6'nın "`prestige_master`'a baş boşluğu kalmamış" bulgusunu
quest sistemi tek başına ikiye katlıyor.

**ÖNERİ (DEĞER):** quest prestij ödülü+cezası **×0.4**:

| tier | prestij ödül (yeni) | prestij ceza (yeni) | para ödül/ceza (DEĞİŞMEZ) |
|---|---|---|---|
| Easy   | 1.4 → **0.6**  | 0.8  → **0.32** | 28 / 15 |
| Medium | 3.0 → **1.2**  | 1.36 → **0.55** | 60 / 27 |
| Hard   | 7.5 → **3.0**  | 2.66 → **1.05** | 150 / 53 |

Ölçülen etki (12 çözünür hücre): prestij tavanı vuran hücre **6/12 → 3/12** (quest'siz tabana geri),
ortalama quest prestiji 24.5 → 9.8, quest'in final kasaya katkısı **+%38 → +%28**.
Para ödülüne DOKUNULMUYOR — §6'da gösterildiği gibi para kolu zaten "bonus" bandında.

Alternatif ×0.25 de denendi: tavan sonucu aynı (3/12), kasa etkisi +%26 — ×0.4 seçildi çünkü
prestij kolunun tamamen sıfırlanması (×0) bile kasa etkisini yalnız +%20'ye indiriyor (yani
kalan etki quest PARASININ kaldıraç etkisi, prestij değil) ve ×0.4 hâlâ "quest prestij verir"
tematik vaadini koruyor.

---

## 3. ⭐⭐ "GÖREV KADEMESİ" UPGRADE'İ STRICT BANTTA **NEGATİF** — ödenmiş bir kötüleştirme

Sahne: `The Main Office.unity`, "Görev Kademesi" — `baseCost: 80`, `costStep: 20`, `maxLevel: 2`,
`requiresQuestSystem: 1`, `disabledInDraft: 0` (AKTİF). Fiyat `DifficultyManager.UpgradeCostMultiplier`
{1, 2, 2.95, 3.7} ile çarpılıyor → L1 = 80/160/236/296 TL, L2 = 100/200/295/370 TL.

`QuestManager.SelectDailyQuestsStratified` (cs:496-544) ve `SetQuestTierInternal` (cs:744-754,
**yalnız-artar, geri alınamaz**) canlı davranışıyla, TAM ENUMERASYONLU teklif modeli
(tier0: 1 garanti + 2 dolgu = 3 Easy çekilişi; tier1: 1E+1M+1 dolgu; tier2: 1E+1M+1H):

| hücre | T0 | T1 | T2 | L1 net (değer−fiyat) | L2 net |
|---|---|---|---|---|---|
| P1 Normal/strict | **188** | 165 | **147** | −103 | −118 |
| P2 Normal/strict | **826** | 891 | 818 | −95 | −273 |
| P3 Normal/strict | **737** | 778 | 717 | −195 | −356 |
| P4 Normal/strict | **1181**| 1268| 1158 | −209 | **−480** |
| P1 Normal/optimistic | 3420 | 3564 | **3870** | +64 | +206 |
| P2 Normal/optimistic | 8908 | 9461 | **10268**| +393 | +607 |
| P3 Normal/optimistic | 9994 |10617| **11557**| +387 | +645 |
| P4 Normal/optimistic |11327 |11950| **12887**| +327 | +567 |
| P1/P2/P3/P4 Slow/optimistic | — | — | — | −35…+121 | −69…+78 |

**4/4 Normal/strict hücrede `T2 < T0`**: oyuncu 180×costMult TL ödeyip **daha az** parayla bitiriyor.
P1'de gerileme −%22, P4'te −%2 ama üstüne 666 TL ödenmiş. Hard tier STRICT'te **16 günün 0'ında**
seçiliyor (§4).

### Kök neden: TİER KİLİDİ HAVUZ SEYRELTİYOR, ve GERİ ALINAMIYOR

3 teklif slotu sabit (`DAILY_QUEST_COUNT = 3`, cs:17). Tier 0'da havuz 11 Easy → 3 çekilişin
**hepsi tamamlanabilir**. Tier 2'de havuz 30, bunun **19'u (Medium+Hard) STRICT bantta negatif EV**
(§4) → oyuncunun gördüğü "iyi" teklif sayısı 3'ten 1'e düşüyor.

**D1 SUÇLU DEĞİL — tam tersine hafifletici.** 3 dağıtım varyantı Monte-Carlo ile karşılaştırıldı:

| varyant | en kötü L2 net (strict) |
|---|---|
| **D1** (canlı: her tier'dan 1 garanti) | **−479** |
| `none` (D1 öncesi saf rastgele 3) | −486 |
| `topOnly` (yalnız en üst tier garanti) | −556 |

D1 en az kötü olanı; tuzağı yaratan şey slot sayısının SABİT kalması.

### ÖNERİ (MEKANİK, değer değil): teklif slotu tier ile ARTSIN

`DAILY_QUEST_COUNT` 3 sabit → **`3 + CurrentQuestTier`** (3 / 4 / 5 teklif). Böylece üst tier
açmak mevcut seçenekleri **silmiyor, ekliyor**. Ölçüm (aynı harness, `variant='additive'`):

- `T1 ≥ T0` **12/12 hücrede** (canlı: 8/12)
- `T2 ≥ T0` (brüt) **11/12** (canlı: 8/12); Normal/strict'te gerileme tamamen kapanıyor
- optimistic'te L2 net +463…+1059 (canlı +206…+645) — üst bant da iyileşiyor

**Kalan sorun FİYATLANDIRMA, ayrı madde:** additive slot'la bile L1/L2 net STRICT'te negatif
(−75…−389), çünkü **16 günlük TOPLAM quest parası STRICT'te 38-281 TL**, upgrade ise 180-666 TL.
Kök neden yapısal: **quest ödülleri P-DÜZ (28/60/150 herkese), upgrade fiyatı P-ÖLÇEKLİ (×1/2/2.95/3.7)**,
oysa `rewardPerBoxByPlayerCount` {50,55,70,88} P ile büyüyor. İki seçenek:
  (a) Görev Kademesi'ni `UpgradeCostMultiplier`'dan MUAF tut (içerik kilidi, kapasite değil) →
      P4'te 666 → 180 TL, Normal/strict net −613 → −127;
  (b) quest para ödüllerini `rewardPerBoxByPlayerCount` gibi P-ölçekle.
  (a) daha ucuz ve daha az yan etkili; **(a) öneriliyor.**

---

## 4. TİER MERDİVENİ İKİ REJİMLİ: optimistic'te "hepsi plato", strict'te "hepsi negatif"

Gün 8 kapasitesiyle 30 quest'in tek tek tamamlanma olasılığı (`questCompletionProb`):

**P2 Normal/OPTIMISTIC** (üretim 14.7/gün, tır 5.88): **30 quest'in 27'si TAM AYNI p=0.874'te**
(doygunluk platosu). `targetCount` tamamen anlamsız — Easy renk-kilitli-2 ile Hard raf-12 aynı
olasılıkta. Sıralama yalnızca tier ödülüne indirgeniyor: Hard 150 > Medium 60 > Easy 28.
**Karar yok, trade-off yok.** (İstisna: `hard_shelf_10`/`hard_pack_10` p=0.697, `hard_truck_3` p=0.874.)

**P2 Normal/STRICT** (üretim 4.0/gün, tır 0.73): sıralama tersine dönüyor —
pozitif EV'li **yalnız 4 quest** var: `med_phone_3` (+35 TL), `easy_phone_2` (+15.6),
`easy_shelf_4` / `easy_pack_4` (+4.3). Diğer 26'sı negatif. **9 Hard quest'in HEPSİ −40…−46 TL.**

Ödül büyümesi / zorluk büyümesi oranı (gün 8, telefonsuz quest'ler):

| bant | p(Easy) | p(Med) | p(Hard) | ödül/zorluk Med | ödül/zorluk Hard |
|---|---|---|---|---|---|
| Normal/strict P1-P4 | 0.10-0.51 | 0.04-0.27 | 0.02-0.11 | 4.1-5.6 | **5.9-6.4** |
| Normal/optimistic P2-P4 | 0.874 | 0.874 | 0.61-0.874 | **2.14** | 2.5-3.6 |

Yani ödül her tier'da zorluğun **2.1-6.4 katı** hızla büyüyor. Hiçbir SABİT ödül tablosu
4 kutu/gün ile 26 kutu/gün arasında gezen bir kapasite bandını doğru fiyatlayamaz — bu
Round 2 §3/§4'ün "STRICT bandın ~%25-30 mekanik verim açığı" bulgusunun quest yüzeyindeki izdüşümü.
6 aday ödül tablosu tarandı (ödül ×1.6/×1.8, prestij /3, Hard hedef ×0.6-0.67, düz ceza oranı):
**hiçbiri STRICT'te L2'yi pozitife çevirmiyor** — sorun tabloda değil, kapasite tasarımında ve
slot mekaniğinde (§3).

---

## 5. ⭐ `AnswerPhone` QUEST'İ, ROUND 7'NİN "AZ TELEFON KULLAN" DERSİNİN TERSİNİ ÖDÜLLENDİRİYOR

`QuestTracker.NotifyPhoneAnswered` **canlı ve bağlı**: `PhoneCallManager.cs:469` (V4 DIŞARI arama
akışının içinde, `ExecuteCall` sonunda). Yani quest "telefonu aç" değil fiilen **"telefonu çevir"**
sayıyor — isim (`AnswerPhone`) V3 bayatlığı.

Telefon oranı `u`'ya göre izole edildi (Round 7 §9 talebi — quest bulgularını telefon
artifaktından ayırmak). `easy_phone_2` (hedef 2 çağrı) ve `med_phone_3` (hedef 3 çağrı) tek
`type=4` asset'ler; havuzdan çıkarılıp tekrar koşuldu:

| u (telefon oranı) | telefon quest'inin final kasaya katkısı (Normal bandı, 8 hücre) |
|---|---|
| %0   | −6 … −13 TL (ihmal) |
| %10  | −6 … −117 TL |
| **%25 (Round 7 optimali)** | **−3 … −79 TL** |
| %60  | **+20 … +118 TL** |
| %100 | +21 … +148 TL |

Yani **telefon quest'i yalnız oyuncu telefonu Round 7'nin zararlı bulduğu oranda (u≥%60)
kullanırsa kâr ediyor**; Round 7'nin öğretilebilir kuralında (u=%10-25, "her ~4 müşteriden birini
çağır") tamamlanamıyor ve küçük bir tuzak oluyor.

Daha sert hâli: **STRICT bantta u=%60'ta telefon quest'i havuzdaki TEK pozitif-EV Medium quest'i**
(`med_phone_3` +35 TL, ikinci sıradaki `easy_shelf_4` +4.3 TL). Yani "kötü oynayan" oyuncu için
quest sistemi telefon spam'ini ödüllendiren tek kaldıraç oluyor — Round 5 §2'nin CUSTOMER SUPPORT
bulgusuyla **aynı sınıf tasarım kokusu**, farklı sistemde tekrarlanıyor.

**ÖNERİ:** Round 7'nin `timeSkipAmountByPlayerCount={115,49,47,47}` önerisi uygulandıktan sonra
telefon quest hedefleri Round 7'nin optimal bandına oturtulsun: `easy_phone_2` hedef **2 → 1**,
`med_phone_3` hedef **3 → 2** (P1 günde ~1, P2-P4 günde ~2.5 çağrı yapıyor). Aksi hâlde bu iki
asset ya ölü (optimistic, p=0.06-0.13) ya da anti-öğretici (strict) kalıyor.

---

## 6. QUEST PARASI DOĞRU BÜYÜKLÜKTE — ama son kasaya kaldıraçlı biniyor

| bant | tır TL/gün | quest TL/gün | quest/tır | kabul edilen gün |
|---|---|---|---|---|
| Normal/strict P1-P4 | 93-513 | 2-13 | **%2.2-2.5** | %19-96 |
| Slow/strict P1-P4 | 47-251 | 2-5 | %2.1-4.3 | **%14-39** |
| Normal/optimistic | 371-1336 | 27-60 | %4.5-7.4 | %68-100 |
| Slow/optimistic | 226-879 | 14-43 | %4.9-6.4 | %65-99 |

Tasarım niyeti bandı (`quest_tier_redesign_2026-07-25`: quest-EV/günlük-çekirdek-gelir %0.3-4.6)
STRICT'te **korunuyor**, optimistic'te **%60 aşılıyor** (%7.4) ama alarm seviyesinde değil.

**AMA final kasaya etkisi çok daha büyük: +%19…+%61.** Sebep yapısal ve masum değil: kira brüt
gelirin çoğunu süpürdüğü için final kasa ince bir bakiye; gelirin %5'i bakiyenin %30'u oluyor.
Ablasyon (12 çözünür hücre): bu deltanın **%10-37 puanı quest PARASINDAN**, **%7-36 puanı quest
PRESTİJİNDEN** geliyor (§2). Prestij ×0.4 önerisi ortalama deltayı +%38 → +%28'e indiriyor.

Metodoloji notu: karar kuralı "prestij-farkında" (para EV + prestij EV × marjinal prestij değeri)
ve "yalnız para" olarak İKİ kez koşuldu; **12/12 hücrede birebir aynı sonuç** verdi → bulgular
prestij değerleme sezgisine bağımlı değil.

---

## 7. D2 ÇİFTE ÖLÇEKLEME BUG'I: KAPANMIŞ, ama tetiği kurulu duruyor

`quest_d2_double_scaling_bug_2026-08-06` kararı **UYGULANMIŞ**. `QuestManager.CalculateEffectiveTargetCount`
(cs:569-583) dört tipi muaf tutuyor: `AnswerPhone`(4), `CompleteTruck`(2), `PlaceBoxOnShelf`(1),
`PackToy`(3). **Canlı 30 asset'in hepsi bu dört tipten biri** (asset taraması: type ∈ {1,2,3,4}) →
D2 bugün **tam NO-OP**, `targetCount`'lar 2026-07-29 değerlerinde (Hard 12/5/3, Med 7/3/2, Easy 4/2/6/1).

⚠️ **Ama muafiyet listesi TİP-BAZLI, kapsam-bazlı değil.** Muaf OLMAYAN üç tip:
`CompleteMinigame`(0), `MakePackagingMistake`(5), `CompleteSpecificColorTruck`(6).
Bunlardan **`CompleteSpecificColorTruck` tetikleyicisi CANLI** (`Truck.cs:656`
`NotifySpecificColorTruckCompleted(requestedBoxType)`), yalnız **asset'i yok**.
`quest_completetruck_color_constraint` bu quest tipini katalog planında tutuyor →
**biri renk-kilitli tır quest'i asset'i eklerse D2 bug'ı aynen geri gelir** (tır arzı da P ile
ölçekleniyor, ×3.70 çifte sayım olur). Diğer iki tipin tetikleyicisinin hiç çağıranı yok
(`NotifyMinigameCompleted`, `NotifyPackagingMistake` — tüm `Assets/` içinde okuyucusuz üretici).

**ÖNERİ (KOD, ucuz):** muafiyet listesine `CompleteSpecificColorTruck` de eklensin (arzı
P-ölçekli), veya listeyi tersine çevirip "yalnız şu tipler ölçeklenir" beyaz-listesi yapılsın.

---

## 8. GÜN-16 EXPLOIT'İ KAPANMIŞ; kalan artık ihmal edilebilir

`quest_fixed_reward_table_2026-07-28`'in "gün 16'da kabul edilen quest asla kapanmıyor" exploit'i
**2026-08-06'da düzeltilmiş**: `QuestManager.SettleAcceptedQuestsOnGameEnd()` (cs:839-845),
`DayCycleManager.cs:779-781` win dalından çağrılıyor, idempotent.

**Kalan artık:** çağrı sırası `CheckWinCondition` → `GameStateManager.TriggerWin` → **sonra**
settlement (`DayCycleManager.cs:769-781`; `GameStateManager.cs:740` yorumu da bunu doğruluyor,
`TriggerLose` oyun bittikten sonra no-op). Yani gün 16'da quest CEZASININ sonuca etkisi YOK →
rasyonel oyuncu gün 16'da tamamlanma olasılığına bakmadan **en yüksek ödüllü (Hard) quest'i alır**.

Ölçülen büyüklük: "dürüst EV" ile "cezasız EV" farkı **+6.7 … +13.6 TL** = final kasanın
**%0.1-8.2'si** (en yüksek P1 Normal/strict'te, çünkü orada kasa zaten 147 TL).
**Denge riski YOK, düzeltme önerilmiyor** — not olarak bırakılıyor.

---

## 9. BUFF ALT SİSTEMİ: tam kurulu, tam bağlı, **sıfır içerik**

30 asset'in **hepsinde `hasBuff: 0`**. Üretici tarafta `QuestData.Build` (cs:178-191) ve
`QuestManager.ApplyPermanentBuff`/`ApplyTemporaryBuff` (cs:956-988) hazır; tüketici tarafta
`BuffManager.Instance.ApplyActiveBuffsTo(...)` **canlı çağrılıyor** (`PlayerMovement.cs:246`,
`CustomerManager.cs:740`). Yani `BuffData`/`BuffManager`/`BuffType` + 12 `RewardType` varyantı
+ `ApplyPenalties`'teki `PenaltyReduction` çarpanı (cs:904-910) **tamamen ölü içerikli**.

Ekonomik etkisi **tam sıfır** (ölçüm gereksiz). İki seçenek: ya en az 3-5 asset'e buff yazılsın
(o zaman economist'e değer sorulmalı — `MaxQueueSize`/`DayDuration`/`CustomerWaitTime` gibi
tipler doğrudan Round 2-5 kaldıraçlarına dokunuyor, dikkatli fiyatlanmalı), ya da alt sistem
"gelecek için rezerve" diye açıkça etiketlensin. **Karar tasarım tarafında, ekonomist önerisi yok.**

`quest_fixed_reward_table_2026-07-28`'in diğer iki tespiti de doğrulandı: tier dağılımı
**11 Easy / 10 Medium / 9 Hard** (7/tier değil), **Hard'da telefon quest'i YOK** (tier yükseldikçe
tip çeşitliliği azalıyor: Easy 4 tip → Hard 3 tip).

---

## 10. `sim.js` QUEST MODELİNİN 3 AÇIĞI (Round 10 girdisi)

1. **Ödül tablosu bayat** (§1) — en kritik.
2. **`questDailyDecision` (cs:560-583) havuz seyrelmesine KÖR.** Tüm havuzu para-EV'ye göre
   sıralayıp ilk `dailyQuestOffered` tanesinin ORTALAMASINI alıyor; yani "oyuncu her gün havuzun
   en iyi 3'ünü görür" varsayıyor. Canlı `SelectDailyQuestsStratified` RASTGELE çekiyor →
   tier açıldıkça iyi teklif görme olasılığı düşüyor. §3'ün tuzağı bu yüzden Round 4'te
   "değeri sıfır" olarak görünmüştü, "negatif" olarak değil. Ayrıca ortalama yerine MAKSİMUM
   alınmalı (günde 1 kabul limiti var).
3. **Gün-16 settlement'i modellemiyor** (`questSettlePending` gün 17'ye taşıyor, hiç yatmıyor) —
   canlı kod 2026-08-06'da düzeltildi. Etkisi küçük (§8) ama model canlı koddan sapıyor.
4. `ASSUMED.questExecutionFriction` {strict 0.75, optimistic 0.92} ve doygunluk eğrisi
   (`ratio≥1.5 → 0.95` platosu) hiç ölçüme dayanmıyor — §4'teki "optimistic'te 27/30 quest aynı
   olasılıkta" sonucu **doğrudan bu platodan** geliyor. Playtest verisi gelene kadar §4'ün yönü
   güvenilir, büyüklüğü değil.

---

## Özet: Round 10'a giden öneri listesi

| # | tür | öneri | gerekçe |
|---|---|---|---|
| R8-1 | **MEKANİK** | `QuestManager.DAILY_QUEST_COUNT` 3 → `3 + CurrentQuestTier` | §3: tier kilidi bugün seçenek SİLİYOR; 4/4 Normal/strict hücrede ödenmiş kötüleştirme |
| R8-2 | **DEĞER** | Quest prestij ödül+ceza **×0.4** (Easy 0.6/0.32, Med 1.2/0.55, Hard 3.0/1.05); para tablosu DEĞİŞMEZ | §2: görünmez terim para ödülünün 1.9 katına çıkmış, prestij tavanı vuran hücre 3→6 |
| R8-3 | **FİYAT** | "Görev Kademesi"ni `UpgradeCostMultiplier`'dan MUAF tut (80/100 TL sabit) | §3: quest ödülleri P-düz ama fiyat P-ölçekli; P4'te 666 TL, 16 günlük quest geliri 205 TL |
| R8-4 | **DEĞER** | `easy_phone_2` hedef 2→**1**, `med_phone_3` hedef 3→**2** (Round 7 telefon değişikliğinden SONRA) | §5: bugün yalnız u≥%60 telefon spam'inde tamamlanıyor; Round 7'nin dersinin tersini öğretiyor |
| R8-5 | **KOD** | `CalculateEffectiveTargetCount` muafiyetine `CompleteSpecificColorTruck` eklensin | §7: tetikleyici canlı, asset eklenirse D2 bug'ı geri gelir |
| R8-6 | **SİM** | `sim.js:494-531` ödül kolonları senkronlansın + `questDailyDecision` gerçek çekiliş/maks modeline geçsin | §1, §10 |
| — | **NOT** | Gün-16 cezasızlığı (§8, %0.1-8.2) ve buff alt sisteminin boşluğu (§9) — değişiklik ÖNERİLMİYOR | |

**Sıra bağımlılığı:** R8-4, Round 7'nin `timeSkipAmountByPlayerCount` değişikliğinden SONRA
uygulanmalı ve telefon quest'i o değişiklikle birlikte tekrar ölçülmeli.
R8-1/R8-2/R8-3 birbirinden bağımsız, hepsi Round 3 kirasından bağımsız ölçüldü (canlı kira ile).

İlişkili: [[quest_fixed_reward_table_2026-07-28]] (ÖDÜL KOLONU ARTIK BAYAT, bu dosya üstüne yazar),
[[quest_d2_double_scaling_bug_2026-08-06]] (kapandı, §7), [[quest_tier_redesign_2026-07-25]] (EV bandı referansı),
[[quest_hard_targetcount_retune_2026-07-29]] (canlı targetCount kaynağı, doğrulandı),
[[economy_full_balance_round6_2026-08-30]] (prestij→para dönüşümü), [[economy_full_balance_round7_2026-08-30]] (telefon oranı),
[[economy_full_balance_round4_2026-08-30]] (§3'ün ilk tespiti), [[economy_full_balance_round2_2026-08-30]] (STRICT mekanik açık).
