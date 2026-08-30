---
name: economy-full-balance-round11-2026-08-30
description: Round 11 - Round 10 U6 (DailyQuestTargetCount=3+tier) 16/16 hucrede OYUNDA NO-OP oldugu kanitlandi (UI 3 slotta kirpiyor, garanti pickler hep ilk 3'te); 3-slot kisitini koruyan alternatif tasarim = tier slotlarinda "K aday cek, en uygununu teklif et" (K=3/2/1, adaptif fizibilite siralamasi) -> odenmis kotulesme 4/16 -> 0/16; ikincil oneri Hard/Medium para cezasi 53/27 -> 30/20 (naif oyuncu tuzagi)
metadata:
  type: project
---

# Round 11 — "Görev Kademesi" 3-slot kısıtı altında yeniden tasarım (2026-08-30, Opus)

Takip: `plans/economy-full-balance-2026-08-30.md`. **Oyun kodu DEĞİŞMEDİ** (`.cs`/`.asset`/sahne
yalnız OKUNDU). Değişen tek dosya: `tools/economy-sim/sim.js` (§7).

**Bu round Round 10'dan farklı bir tabanda koştu:** Round 10'un 12 UYGULA maddesinin
hepsi bu arada gerçek koda işlenmiş. Kod okunarak doğrulandı:
`baseRentByPlayerCount={290,650,1140,1630}` (`GameEconomySettings.cs:21`),
`timeSkipAmountByPlayerCount={115,49,47,47}` (`:117`), `phoneTimeSkipPerkMultiplier` alanı var (`:120`),
`wrongProductPrestigePenalty=-0.20` (`:153`), `PerkEffect.cs:194` `1.20f`, `:308-309` `0.80f`/`1f`,
`UpgradePanel.cs:1638-1644` Görev Kademesi `UpgradeCostMultiplier`'dan MUAF,
30/30 quest asset'inde prestij ×0.4 (Easy 0.6/0.32 · Med 1.2/0.55 · Hard 3.0/1.05),
`Q_Easy_6_Phone.targetCount=1` · `Q_Medium_6_Phone.targetCount=2`.
`sim.js` bunların hepsine resenkronlandı (§7) — **Round 8/10'un quest rakamları artık bayat**.

---

## 1. ⭐⭐ U6 (`DailyQuestTargetCount = 3 + tier`) OYUNDA **16/16 HÜCREDE NO-OP** — kanıt

qa/kontrol'ün bulgusu ölçümle doğrulandı ve mekanizması tam olarak şu:

`SelectDailyQuestsStratified` (`QuestManager.cs:507-555`) önce **her açık tier'dan 1 garanti pick**
ekliyor (`selected` listesinin BAŞINA), sonra kalan `DailyQuestTargetCount - selected.Count`
slotu havuzdan dolduruyor. `QuestUIController.cs:407` ise
`Mathf.Min(questSlots.Count, DailyQuestCount)` ile kırpıyor ve sahnede
(`The Main Office.unity:90392-90395`) **tam 3 `QuestSlotUI`** var.

Sonuç: oyuncunun gördüğü **ilk 3 teklif her tier'da aynı**:

| CurrentQuestTier | `_dailyQuests` (dqc=3+tier) | oyuncunun GÖRDÜĞÜ ilk 3 | dqc=3 olsaydı |
|---|---|---|---|
| 0 | [E, E, E] | [E, E, E] | **aynı** |
| 1 | [E, M, dolgu, dolgu] | [E, M, dolgu] | **aynı** |
| 2 | [E, M, H, dolgu, dolgu] | [E, M, H] | **aynı** |

Index ≥3'teki quest'ler hiçbir slota bağlı değil → `AcceptQuest` çağrılamaz → `Available`
kalırlar → `SettleAcceptedQuestsForDayEnd` (`cs:872-882`) `Available` durumunu **atlıyor**,
yani ceza da vermiyorlar. Tam ölü ağırlık.

**Ölçüm:** `runFullSim`'de `dailyQuestCount` 3→5 (tier 2) ve 3→4 (tier 1) değiştirildiğinde
final kasa **16/16 hücrede BİREBİR AYNI**. `questUiSlots` 5'e çıkarılırsa fark açılıyor
(N/s P2: 4445 → 4467) — yani model doğru, oyun gerçekten kırpıyor.

→ **U6'yı geri almak sıfır ekonomik regresyon taşır.** Ama Round 8'in asıl sorunu geri gelir (§2).

---

## 2. TABAN DURUM (U6 geri alındıktan sonra): ödenmiş kötüleştirme **4/16 hücre**

`ledgerMode` (grace/0-kırpma kırılmalarından arı), telefon %20, fiyat U9 sonrası P-DÜZ
(L1 = 80 TL, L2 kümülatif = 180 TL):

| hücre | T0 | T1 | T2 | brüt L1 | brüt L2 | net L1 | net L2 |
|---|---|---|---|---|---|---|---|
| N/s P1 | 2106 | 2102 | **2091** | **−4** | **−15** | −84 | −195 |
| N/s P2 | 4441 | 4456 | 4445 | +15 | +4 | −65 | −176 |
| N/s P3 | 5710 | 5874 | 5908 | +164 | +198 | +84 | +18 |
| N/s P4 | 6783 | 6970 | 7055 | +187 | +272 | +107 | +92 |
| N/o P1-P4 | 4620…10366 | | | +91…+470 | +357…+868 | +11…+390 | +177…+688 |
| S/s P1 | 484 | 476 | **473** | **−8** | **−11** | −88 | −191 |
| S/s P2 | 612 | 554 | **534** | **−58** | **−78** | −138 | −258 |
| S/s P3 | 433 | 430 | **413** | **−3** | **−20** | −83 | −200 |
| S/s P4 | 304 | 356 | 351 | +52 | +47 | −28 | −133 |
| S/o P1-P4 | 2291…3211 | | | +28…+196 | +63…+453 | −52…+116 | −117…+273 |

**T2 < T0: 4/16 · T1 < T0: 4/16.** (Round 8'in −118…−480 ve Round 10'un −30…−87 rakamları
her ikisi de bayat: quest prestij ×0.4 uygulandıktan sonra büyüklük **−4…−78 TL**'ye indi.
Sonuç aynı yönde, aciliyet daha düşük.)

### 2b. ⭐ KÖK NEDEN "HAVUZ SEYRELMESİ" DEĞİL — **KAYBEDİLEN EASY ÇEKİLİŞİ**

Round 8'in "havuz 11→30'a genişliyor, dolgu seyreliyor" çerçevesi **yanlış yerdeydi**.
dqc=3'te tier 2'de **dolgu slotu hiç çalışmıyor** (3 garanti pick 3 slotu doldurur).
Gerçek mekanizma:

- **T0:** 3 slotun 3'ü de Easy çekilişi → oyuncu **3 Easy teklifin EN İYİSİNİ** görür.
- **T1:** [Easy, Medium, dolgu] → **~1.5 Easy** çekilişi.
- **T2:** [Easy, Medium, Hard] → **1 Easy** çekilişi.

STRICT bantta Medium/Hard slotları negatif-EV olduğu için fiilen ÖLÜ slot; geriye 3 yerine
1 Easy çekilişi kalır. Gün-8 EV ayrıştırması (TL/gün, kabul kararı EV>0 kuralıyla):

| hücre | T0 (3 Easy çekiliş) | tek Easy çekiliş | **çeşitlilik kaybı** | T2 canlı [E,M,H] | Medium+Hard katkısı |
|---|---|---|---|---|---|
| N/s P1 | 5.1 | 2.0 | **−3.1** | 3.4 | +1.4 |
| N/s P2 | 13.2 | 10.1 | **−3.1** | 10.4 | +0.3 |
| S/s P2 | 6.6 | 2.8 | **−3.8** | 3.2 | +0.4 |
| N/o P2 | 19.9 | 16.0 | −3.9 | 31.6 | **+15.6** |
| N/o P4 | 20.8 | 18.2 | −2.6 | 34.2 | **+16.0** |

Yani: kayıp her bantta aynı (~3 TL/gün), kazanç bant-bağımlı (strict +0.3…1.4, optimistic +16).
**Doğru çözüm kaybı kapatmak, yeni slot eklemek değil.**

---

## 3. ⭐⭐ ÖNERİ **R11-1 (MEKANİK)** — 3 slot KORUNUR, slot **KALİTESİ** kademeyle artar

> Tier slotu için **K aday çekilir, oyuncunun bugün en yapabileceği olan teklif edilir.**
> K, o tier'ın üst tier'lara kaptırdığı slot sayısı kadardır — yani kademe seçenek SİLMEZ,
> kaybettiği çeşitliliği geri verir.

**K tablosu (formül değil, birebir tablo — belirsizlik bırakmasın):**

| `CurrentQuestTier` | Easy slotu K | Medium slotu K | Hard slotu K |
|---|---|---|---|
| 0 | **1** (bugünkü davranış, DEĞİŞMEZ) | — | — |
| 1 | **3** | 1 | — |
| 2 | **3** | **2** | 1 |

Easy'nin K=3 olması T0'daki 3 çekilişi birebir geri verir; Medium T2'de Hard'a 1 slot
kaptırdığı için K=2; Hard hiç kaptırmadığı için K=1 (vitrin slotu gerçek bir kumar kalır).

**"En yapabileceği" ölçütü — ADAPTİF (önerilen):** tier içinde ödül/ceza DÜZ olduğu için
(28/15 · 60/27 · 150/53) tier içi EV sıralaması = **tamamlanabilirlik sıralaması**. Oyun bunu
biliyor: `QuestManager` zaten `HandleBoxPlacedOnShelf` / `HandleTruckCompleted` /
`HandleToyPacked` / `HandlePhoneAnswered` (`cs:396-419`) event'lerine abone. Tip başına
**dünkü gerçekleşen sayaç** tutulup skor `dünkü arz ÷ (effectiveTarget × (renk-kilitli ? 3 : 1))`
ile hesaplanır; K aday arasından en yüksek skorlu teklif edilir.

### Ölçülen etki (16 hücre, 5 farklı koşum senaryosu)

| senaryo | T2<T0 CANLI | T2<T0 **R11-1** | min(T2−T0) | T0 değişen hücre | yeni iflas |
|---|---|---|---|---|---|
| ledger, telefon %20 | 4 | **0** | +2 | 0/16 | 0 |
| gerçek kurallar (grace VAR) | 4 | **0** | +2 | 0/16 | 0 |
| gerçek kurallar (grace YOK) | 4 | **0** | +2 | 0/16 | 0 |
| ledger, telefon %0 | 4 | **0** | +1 | 0/16 | 0 |
| ledger, telefon %50 | 4 | **0** | +56 | 0/16 | 0 |

**T1<T0 de 4/16 → 0/16.** `questTier=0` sonuçları **16/16 hücrede birebir değişmiyor** —
yani upgrade'i hiç almayan oyuncu bu değişiklikten HİÇ etkilenmiyor. Enflasyon riski yok:
en yüksek T2 brüt kazancı 868 → 906 TL (**+%4**).

| hücre | brüt L1 (canlı → R11-1) | brüt L2 (canlı → R11-1) |
|---|---|---|
| N/s P1 | −4 → **+36** | −15 → **+45** |
| N/s P2 | +15 → **+39** | +4 → **+46** |
| N/s P3 / P4 | +164/+187 → +176/+200 | +198/+272 → +232/+300 |
| N/o P1-P4 | +91…+470 → +163…+481 | +357…+868 → +446…+906 |
| S/s P1 | −8 → **+4** | −11 → **+2** |
| S/s P2 | −58 → **+20** | −78 → **+15** |
| S/s P3 | −3 → **+32** | −20 → **+33** |
| S/s P4 | +52 → +79 | +47 → +98 |
| S/o P1-P4 | +28…+196 → +108…+286 | +63…+453 → +200…+570 |

### 3b. YEDEK PLAN (adaptif sayaç fazla iş gelirse): **statik zorluk skoru**
`skor = effectiveTarget × (renk-kilitli ? 3 : 1)` (tip ağırlığı OLMADAN, düz).
Sonuç: T2<T0 **1/16** (kalan hücre S/s P2, −20 TL), ortalama brüt L2 **154.6 vs adaptifin 168.2**
(ideal kazancın **%81'i**). Tip ağırlıklı varyantlar (tır 0.356 / telefon 0.24, 16 hücre
medyanından kalibre) da denendi: **2/16**, aynı seviye.
⚠️ Statik skorun kırıldığı yer: **Slow/strict P2-P4'te tır arzı SIFIR** ama statik skor
`easy_truck_1`'i (hedef 1) "en kolay" sayıp sık teklif ediyor. Adaptif skor aynı hücrede onu
**son sıraya** (%0.1 seçilme) atıyor. Bu yüzden **adaptif tercih edilmeli**; statik ancak
"tır/telefon tiplerini skorlamada dışla" kuralıyla güvenli.

### 3c. Çeşitlilik kontrolü (dejenerasyon riski ölçüldü)
Easy slotu best-of-3 altında teklif dağılımı (gün 8): **en sık quest %24.9**, ilk 3 toplam %62,
11 asset'in **8'i** hâlâ görünüyor. Optimistic bantta 27/30 quest aynı olasılık platosunda
(Round 8 §4) olduğu için sıralama neredeyse rastgele → çeşitlilik orada tamamen korunuyor.
**Dejenerasyon riski yok.**

---

## 4. ÖNERİ **R11-2 (DEĞER, ikincil)** — Hard/Medium **para cezası** 53/27 → **30/20**

R11-1 "rasyonel" oyuncuyu (yalnız EV>0 teklifi kabul eden) kurtarıyor. **Naif oyuncu**
(kartta en yüksek ödülü görüp koşulsuz kabul eden) için ayrı bir tuzak ölçüldü:

**Gün 8, koşulsuz Hard kabul eden oyuncunun günlük EV'si (TL/gün):**

| hücre | ceza 53 (CANLI) | ceza 30 | ceza 22 |
|---|---|---|---|
| N/s P1 | **−40.4** | −18.8 | −11.3 |
| N/s P2 | −28.7 | −8.5 | −1.4 |
| N/o P2 | −6.7 | **+11.1** | +17.2 |
| N/o P3 / P4 | **−1.1** | **+16.0** | +21.9 |
| S/s P1 | **−49.2** | −26.6 | −18.8 |
| S/s P2 | −42.4 | −20.6 | −13.0 |
| S/o P2-P4 | −12.7…−17.1 | +1.8…+5.7 | +8.4…+12.2 |

⭐ **Bugün Hard tier'ı 16/16 hücrede naif kabul edene NEGATİF** — en iyi bantta bile (N/o P4)
−1.1 TL/gün. Yani 150 TL'lik ödül hiçbir oyuncu için "körü körüne alınabilir" değil; Hard tier
fiilen yalnız "eminim" anında alınan bir enstrüman. Bu, kartın vaadiyle çelişiyor.

Ceza 30'a inince tablo sağlıklı bir **beceri gradyanına** dönüyor: iyi giden bantlarda (6/16)
körü körüne almak kârlı, zorlanan bantlarda değil.

| tier | moneyPenalty | prestigePenalty | ödül/ceza oranı |
|---|---|---|---|
| Easy | 15 (DEĞİŞMEZ) | 0.32 (DEĞİŞMEZ) | 0.54 |
| Medium | **27 → 20** | **0.55 → 0.40** | 0.45 → **0.33** |
| Hard | **53 → 30** | **1.05 → 0.60** | 0.35 → **0.20** |

(Prestij cezası, mevcut para/prestij ceza oranı ~49 korunarak türetildi. **Para ÖDÜLLERİ ve
prestij ÖDÜLLERİ DEĞİŞMEZ** — Round 10 U7'nin ×0.4 prestij kalibrasyonu bozulmasın.)

**Rasyonel oyuncu üzerindeki maliyeti (R11-1 üstüne):** ortalama brüt L2 348 → 394 TL (**+%13**),
en yüksek 906 → 998 (**+%10**), T2<T0 yine **0/16**. Enflasyon küçük ve üst bantta yoğunlaşıyor.

**Karar notu:** R11-2 OPSİYONEL. Yalnız R11-1 uygulanırsa parent'ın sorduğu "negatif EV"
sorunu (rasyonel oyuncu) **tamamen** çözülür. R11-2 naif oyuncunun tuzağını kapatır ama
19 asset dosyasına dokunur ve üst bandı %10-13 şişirir. **Ekonomist önerisi: ikisi de uygulansın**
— naif-oyuncu tuzağı, "ödenmiş kötüleştirme" ile aynı sınıf bir tasarım hatası ve tek başına
bırakılırsa playtest'te "Hard görevler işe yaramıyor" geri bildirimi olarak geri gelir.

---

## 5. **UYGULAMA** — ölçümle reddedilen 6 alternatif

| # | alternatif | ölçüm | RED gerekçesi |
|---|---|---|---|
| X1 | **Sahneye 2 UI slotu daha ekle** (dqc=3+tier'ı gerçekten canlandır) | `uiSlots=5, dqc=5`: T2<T0 hâlâ **2/16** (S/s P1 −6, S/s P2 −50) | Sahne/UI işi GEREKTİRİYOR ve sorunu **çözmüyor**; R11-1 (3 slot) daha iyi sonuç veriyor (0/16). Not: R11-1 ileride slot eklenirse de uyumlu ve daha iyi (S/s P2: −50 → +30). |
| X2 | **`showcase`**: 1 slot en üst tier + 2 slot alt tier havuzundan | T2<T0 **5/16** (kötüleşti), ort. net L2 79 (taban 97) | Garanti Medium'u siliyor, Easy çekilişini 1.05'e ancak çıkarıyor. Net kayıp. |
| X3 | **`showcaseEasy`**: 1 üst tier + 2 slot yalnız Easy | T2<T0 4/16, ort. net L2 **61** | 2 Easy çekilişi 3'ü telafi etmiyor; üstelik Medium'u tamamen öldürüyor (içerik kaybı). |
| X4 | **Tier-ağırlıklı dolgu** (Easy %60 / Med %25 / Hard %15) | T2<T0 **5/16**, ort. net L2 88 | Yalnız 2 dolgu slotu var; ağırlık ne olursa olsun 3 Easy çekilişi geri gelmiyor. |
| X5 | **TÜM slotlara K=1+tier** (Hard dahil curate) | T2<T0 2/16 ama **N/o brüt L2 868 → 1564 (+%80)** | Round 10 §4-R6'daki "2. servis istasyonu" ile **aynı sınıf hata**: zayıf bandı çözmüyor, güçlü bandı patlatıyor. |
| X6 | **Fiyat indirimi** (80/20 → 60/15 … 30/10) | net>0 hücre 10/16 → yalnız 11/16; **max ROI 5.0x → 12.9x** | Pozitif hücre sayısını neredeyse hiç değiştirmiyor ama Round 4'ün "underpriced no-brainer" tuzağını yaratıyor. **`baseCost=80` / `costStep=20` KALSIN.** |

**Ayrıca not:** R11-1 sonrası kalan net-negatif hücreler (L2 için 6/16, hepsi strict) bir TUZAK
DEĞİL, normal **fırsat maliyeti**: brüt katkı artık 16/16 hücrede ≥0 (en düşük +2 TL), yalnız
80/180 TL'lik fiyatı o bantta çıkarmıyor. Düşük kapasiteli bantta quest gelirinin küçük olması
Round 2 §3'ün yapısal mekanik açığının izdüşümü, quest tasarımının hatası değil.

---

## 6. GAMEPLAY DEPARTMANINA — birebir değişiklik listesi

### D1 (ZORUNLU, önce bu) — U6'yı geri al: `QuestManager.cs:117`
```csharp
// ESKI (Round 10 U6):
private int DailyQuestTargetCount => BASE_DAILY_QUEST_COUNT + _currentQuestTier.Value;
// YENI:
private int DailyQuestTargetCount => BASE_DAILY_QUEST_COUNT;
```
Beraberinde bayatlayan 3 yorum bloğu güncellenmeli:
`QuestManager.cs:111-116` (property XML doc'u), `QuestManager.cs:502-505`
(`SelectDailyQuestsStratified` doc'u "DailyQuestTargetCount = 3 + CurrentQuestTier, U6" diyor),
`QuestUIController.cs:405-406` ("Tier arttıkça DailyQuestCount büyür").
`BASE_DAILY_QUEST_COUNT` sabiti (`cs:19`) `DAILY_QUEST_COUNT` olarak geri adlandırılabilir (opsiyonel).
**Ekonomik etki: SIFIR (§1, 16/16 hücrede ölçüldü).**

### D2 (ANA ÖNERİ, R11-1) — `SelectDailyQuestsStratified` (`QuestManager.cs:507-555`)
`cs:527`'deki tek `tierPool[Random.Range(0, tierPool.Count)]` çekilişi, **K adaylı** çekilişle
değişsin. K değeri §3'teki tabloya göre (`t` = slotun tier'ı, `maxTier` = `_currentQuestTier.Value`):
`K = (maxTier == 0) ? 1 : (t == 0 ? 3 : Mathf.Max(1, maxTier - t + 1))`
→ tier 0: {E:1} · tier 1: {E:3, M:1} · tier 2: {E:3, M:2, H:1}.
Adaylar arasından **en yüksek fizibilite skorlu** olan seçilir; `usedIds` kontrolü korunur;
K aday aynı quest'e denk gelirse doğal olarak tek aday gibi davranır.
**Dolgu slotu (`cs:532-552`) DEĞİŞMEZ** (K=1, uniform) — tier 1'de tek dolgu slotu var, tier 2'de hiç.

Fizibilite skoru için gereken yeni durum (~15 satır): `QuestManager`'da 4 günlük sayaç
(`_shelfToday`, `_trucksToday`, `_packedToday`, `_phoneToday`), `HandleBoxPlacedOnShelf`/
`HandleTruckCompleted`/`HandleToyPacked`/`HandlePhoneAnswered` (`cs:396-419`) içinde artırılır;
`AssignDailyQuests` (`cs:~440`) içinde **önce snapshot alınır, sonra sıfırlanır** (dünkü değerler
o günün seçiminde kullanılır). Skor:
`arz(dünkü, tipe göre) / (effectiveTarget × (renk-kilitli ? 3f : 1f))`, `effectiveTarget` =
`CalculateEffectiveTargetCount(quest)`. **Gün 1'de geçmiş yok** → o gün K=1'e düş (mevcut davranış).

⚠️ Adaptif sayaç istenmezse §3b'deki statik skora düşülebilir
(`effectiveTarget × (renk-kilitli ? 3 : 1)`, düşük = kolay) — ama o zaman `CompleteTruck` ve
`AnswerPhone` tipleri skorlamadan **hariç tutulmalı** (arzı 0 olabiliyor, statik skor bunu göremiyor).

### D3 (İKİNCİL, R11-2) — 19 quest asset'inde ceza kolonu
`Assets/Resources/Quests/Q_Medium_*.asset` (10 dosya): `moneyPenalty: 27 → 20`,
`prestigePenalty: 0.55 → 0.4`.
`Assets/Resources/Quests/Q_Hard_*.asset` (9 dosya): `moneyPenalty: 53 → 30`,
`prestigePenalty: 1.05 → 0.6`.
**Easy 11 dosya DEĞİŞMEZ. Tüm `moneyReward`/`prestigeReward` alanları DEĞİŞMEZ.**

`Assets/Editor/EconomyInvariantCheck.cs:383-387` quest tier sözlüğü buna göre güncellenmeli
(**yalnız ceza kolonları**): `{1:(60, 20, 1.2, 0.40)}`, `{2:(150, 30, 3.0, 0.60)}`.
D1 ve D2 için invariant güncellemesi **GEREKMİYOR** (denetleyicide `DAILY_QUEST_COUNT` /
`targetCount` assert'i yok).

### D4 (ucuz sigorta, öneri) — yeni invariant
`EconomyInvariantCheck`'e "sahnedeki `QuestUIController.questSlots` sayısı ==
`BASE_DAILY_QUEST_COUNT`" kontrolü eklensin. §1'deki hata sınıfı (kod UI'nin gösterebileceğinden
fazla teklif üretiyor) bir daha sessizce geçmesin.

---

## 7. `sim.js` değişiklikleri (v5.0 → **v5.1**)

1. **RESENKRON** (Round 10 uygulaması sonrası): `SRC4.baseRentByPlayerCount={290,650,1140,1630}`,
   `SRC4.timeSkipAmountByPlayerCount={115,49,47,47}`, `SRC4.wrongProductPrestigePenalty=-0.20`,
   `QR` prestij kolonları ×0.4 (0.6/0.32 · 1.2/0.55 · 3.0/1.05), `easy_phone_2.target 2→1`,
   `med_phone_3.target 3→2`. ⚠️ **Round 8/10'un quest tabloları bu resenkrondan ÖNCE üretildi.**
2. **YENİ `SRC4.questUiSlots = 3`** — sahnedeki `QuestSlotUI` sayısı; `questDailyDecision` teklif
   listesini `min(dailyQuestCount, uiSlots)` ile kırpıyor. U6 no-op'unun modellenmesi bu.
3. **YENİ `buildQuestSlots(...)`** — teklif üretim varyant motoru. Slot artık
   `{entries:[{q,p}], k}` dağılımı; `k>1` best-of-K'yı sıralama istatistiğiyle (CDF^K) uyguluyor.
   Varyantlar: `live` (canlı), `bestOfTier` (**R11-1, adaptif**), `bestOfTierStatic`,
   `easyBestOnly(Static)`, `allBestOfTierStatic`, `ladderK`, `showcase`, `showcaseEasy`,
   `tierWeighted`, `liveBestOfK`. `selectionOpts`: `easyK`, `kCap`, `bestOfK`, `scoreFn`,
   `fillTierWeights`, `baseBestOfK`.
4. **YENİ `staticBestOfKPmf` + `questStaticDifficulty`** — statik skorla best-of-K seçim pmf'i
   (`P(seçilen=j) = Q_j^K − Q_{j+1}^K`); tip ağırlıkları 16 hücre × 16 gün medyanından
   kalibre (raf/paket 1.0 · tır 0.356 · telefon 0.24).
5. `runFullSim` yeni `opts`: `questSelection`, `questSelectionOpts`, `questUiSlots`.

**R11-1'in sim çağrısı:**
`runFullSim(P, {scenario, mode, questTier, questSelection:'bestOfTier', questSelectionOpts:{easyK:3}})`

---

## 8. Bu round'da ÇÜRÜYEN / DEĞİŞEN önceki bulgular

1. **Round 10 U6 uygulandı ama OYUNDA NO-OP** (§1). "5/16 → 3/16 iyileşme" ölçümü, sim'in UI
   kırpmasını modellememesinden geliyordu. `sim.js` v5.1 bu körlüğü kapattı.
2. **Round 8 §3'ün "tier kilidi HAVUZU SEYRELTİYOR" çerçevesi yanlış yerdeydi** — dqc=3'te
   tier 2'de dolgu slotu hiç çalışmıyor. Gerçek mekanizma: **kaybedilen Easy çekilişi** (§2b).
3. **Round 8'in "−118…−480 TL" ve Round 10'un "−30…−87 TL" büyüklükleri BAYAT** — quest prestij
   ×0.4 uygulandıktan sonra gerçek büyüklük **−4…−78 TL** (4/16 hücre).
4. **YENİ, hiçbir round'da görülmemiş:** Hard tier, koşulsuz kabul eden oyuncu için
   **16/16 hücrede negatif** (en iyi bantta bile −1.1 TL/gün) — §4.
5. Round 8 §5'in "telefon quest'i spam'i ödüllendiriyor" riski, R11-1'in **adaptif** skoruyla
   kendi kendini düzeltiyor: telefon kullanmayan oyuncuda arz=0 → telefon quest'i son sıraya
   düşüyor, teklif edilmiyor. (Statik skorda bu güvenlik YOK — §3b uyarısı.)

## 9. Kalan model açıkları
1. Adaptif skor sim'de **bugünün** kapasitesiyle hesaplandı, oyunda **dünün** sayacıyla
   hesaplanacak. Gün uzunluğu günde ~10 sn arttığı için sapma küçük (<%5) ama sıfır değil.
2. `questCompletionProb` doygunluk platosu (`ratio≥1.5 → 0.95`) ve `questExecutionFriction`
   hâlâ ölçüme dayanmıyor (Round 8 §10.4) → §3/§4'ün YÖNÜ güvenilir, BÜYÜKLÜĞÜ değil.
3. Naif-oyuncu modeli "her gün koşulsuz en yüksek ödülü al" — gerçek oyuncu 2-3 başarısızlıktan
   sonra öğrenir; §4 bu yüzden **üst sınır** zarardır.
4. Teklif çeşitliliği (§3c) EV/fizibilite eşitliklerinde deterministik sıralama varsayıyor;
   oyunda eşitlik rastgele bozulacağı için gerçek çeşitlilik ölçülenden **daha iyi** olacak.

İlgili: [[economy_full_balance_round8_2026-08-30]] (§3'ün çerçevesi düzeltildi),
[[economy_full_balance_round10_2026-08-30]] (U6 geri alınıyor, diğer 11 madde ayakta),
[[quest_fixed_reward_table_2026-07-28]], [[quest_tier_redesign_2026-07-25]],
[[quest_hard_targetcount_retune_2026-07-29]], [[quest_d2_double_scaling_bug_2026-08-06]],
[[upgrade_pricing_framework]], [[economy_full_balance_round4_2026-08-30]] (§3'ün ilk tespiti).
