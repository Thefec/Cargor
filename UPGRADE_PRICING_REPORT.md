
# Cargor — Upgrade / Perk Fiyatlandırma Raporu (v3.2 — Roguelite Draft Yapısı, kontrol düzeltmesi TUR 3/3)

**Hazırlayan:** Economist subagent
**Tarih:** 2026-07-08 (v3.2 — kontrol/Fable 5'in TUR 2 DÜZELTME GEREKLİ kararı üzerine dar kapsamlı revizyon, tur 3/3 — SON tur)
**Kapsam:** `docs/superpowers/specs/2026-07-08-roguelite-upgrade-draft-design.md` Bölüm 2-4'ün sayısal karşılığı: 4 kalan omurga + 16 yeni perk + reroll eğrisi + tier kilit eşikleri + bütçe fizibilitesi.
**Not:** Bu bir rapordur, kod değişikliği içermez. Uygulama Yol A (Inspector/sahne YAML + veri-güdümlü perk tanımları), gameplay departmanına devredilecek.

> **v3, v2'nin yerini alır.** v2'deki Money/Stamina/Queue/Water/Customer birer "upgrade" olarak KALDIRILDI (spec Bölüm 3.2) — işlevleri yeni perk kimlikleriyle geri döndü (§2). v2'nin ham hesap dosyası referans için dosyanın sonunda korunuyor.

> **v3.2 DEĞİŞİKLİK ÖZETİ (kontrol turu 2/3 sonrası, bu turun tek konusu Prestij Simsarı + 2 küçük düzeltme):**
> 1. **KRİTİK — Prestij Simsarı değer/fiyat patlaması çözüldü.** İki kaldıraç birlikte uygulandı: (a) etki küçültüldü — `bonusPerTier` basamağı 5→7→9 (delta 2/4) yerine **5→5.5→6** (delta 0.5/1, %75 küçültme) yapıldı; (b) fiyat yukarı çekildi — Lv1 300→**510 TL**, Lv2 450→**505 TL** (toplam 750→**1015 TL**). Sonuç: en kötü senaryo (4P) değer/fiyat oranı 18x'ten **2.48x-2.49x**'e indi, diğer T3 perklerin bandına (~≤2.5x) girdi. Detay ve tam hesap: **§4.6 (güncellendi)**.
> 2. **KRİTİK — Rapor içi çelişki giderildi.** §4.6/§9 madde 9'daki value/price hesabı önceden yanlışlıkla Prestij USTASI'nın fiyatlarıyla (280/660) yapılmıştı; şimdi Prestij SİMSARI'nın kendi (yeni) fiyatlarıyla (510/1015) yeniden hesaplandı.
> 3. **KÜÇÜK — Kaldıraçlı Kira'nın "-%20" tanımı netleştirildi.** §4.3'e tek cümlelik uygulama tanımı eklendi: indirim `CalculateRent`'in yalnızca `scaledRent` bileşenine uygulanıyor, `wealthTax` bileşenine dokunmuyor (`GameEconomySettings.cs:109-115`).
> 4. Bu turda dokunulmayan (zaten ONAY almış) konular: Ucuz Kira tam tablosu, T3 tek-koşullu gate, Raf doğrusal dizisi, §9 madde 4 (storeLevel), §9 madde 3/7 — hiçbiri değiştirilmedi.

<details>
<summary>v3.1 DEĞİŞİKLİK ÖZETİ (kontrol turu 1/3 sonrası — arşiv)</summary>

>
> 1. **§3'e Ucuz Kira (#2, T3) tam fiyat tablosu eklendi** — daha önce sadece toplamlarda örtük geçiyordu, artık 3 seviyeli tam satır + `rentGrowthMultiplier` basamakları var.
> 2. **§5 T3 kilidi VE'siz/OR'suz, SADECE gün≥9 olarak sadeleştirildi** — `storeLevel` (kodda çalışmıyor, bkz. bulgu) ve `prestij≥30` (çok erken tetiklenebiliyor) koşulları kaldırıldı. §4.0 ve §4.3 bu tek koşula göre yeniden hesaplandı.
> 3. **§2.1 Raf dizisi gerçek doğrusal diziye çevrildi** (200,210,...,290 — `baseCost=200, costStep=10`), eski kademeli (200/220/220/240...) dizi ile çelişen §0 iddiası artık gerçeğe uyuyor.
> 4. **§9 madde 4 çözüldü** (storeLevel eşiği tamamen kaldırıldığı için artık konu dışı).
> 5. **§9 madde 3 ve Prestij Simsarı gerekçesi node ile yeniden hesaplandı** — kutu ödülü tavanının kodda olmadığı (yalnızca `GameEconomySettings.RunSimulation`'daki simülasyon-içi geçici değişken kırpması, gerçek `PrestigeManager.currentPrestige`'de kırpma YOK) gösterildi; gerçekçi 16 günlük simülasyonla emergent aralık (70-130 TL/kutu, senaryoya göre) ve Prestij Simsarı'nın oyuncu sayısına göre değer patlaması (1P'de zar zor kâr, 4P'de 18x) belgelendi.
> 6. **§7.2 kırık referans düzeltildi** → §9 madde 3'e işaret ediyor.
> 7. **§9 madde 7'ye Yüksek Volatilite playtest notu eklendi.**

</details>

---

## 0. Baz alınan sabitler (Faz 1, değişmedi)

| Parametre | Değer | Kaynak |
|---|---|---|
| startingMoney | 500 TL | `MoneySystem.cs`, `DifficultyManager.cs` (artık ikisi de 500 — [[money_config_conflict]] bulgusu çözülmüş görünüyor, doğrulandı) |
| rentGrowthMultiplier | 1.15 | `EkonomiAyarlari.asset` |
| wealthTaxRate | 0.10 | `EkonomiAyarlari.asset`, `GameEconomySettings.cs:113` |
| rentIntervalDays | 4 | |
| baseRent [1P,2P,3P,4P] | 500 / 900 / 1200 / 1500 | |
| gracePaymentPercent | 0.80 | Kelle Koltukta bu güvenlik ağını iptal eder (§4.13) |
| rewardPerBox (taban) | 50 TL + `floor(prestij/10)×5` TL, **kodda üst sınır YOK** | `Truck.cs:590-597`, `bonusPerTier=5`, `prestigePerBonus=10`. Bkz. §9 madde 3 — "tavan 75/100" iddiası v3'te yanlıştı, gerçek `PrestigeManager.currentPrestige` hiçbir yerde kırpılmıyor (`ModifyPrestige`, satır 150-155). Sadece `GameEconomySettings.RunSimulation()` (editör simülasyon aracı, gerçek oyun değil) kendi yerel `prestige` değişkenini 0-100 arası kırpıyor (satır 199) — bu bir simülasyon-aracı artefaktı, gerçek kural değil. |
| penaltyPerBox | 40 TL | |
| startingPrestige | 15.0 | `PrestigeManager.cs` — [[prestige_fragility]] önerisi uygulanmış |
| customerLostPrestigePenalty | -1.5 | aynı yerde uygulanmış |

Kod maliyet formülü (`UpgradePanel.cs:746`): `finalCost = round((baseCost + level × costStep) × costMultiplier)` — **DOĞRUSAL**. Aşağıdaki tüm seviyeli fiyat dizileri bu formüle **doğrudan uyacak şekilde** (`baseCost`, `costStep` çıkarılabilir) tasarlandı; ayrıca not düşülmedikçe küçük kod değişikliği gerekmiyor.

---

## 1. Yapı özeti

- **4 kalan omurga** (tier'sız, her zaman havuzda): Raf (max 10), Masa (max 2), Hangar (max 2), Görev Tier (max 2, **draft havuzundan tamamen çıkarıldı** — bkz. §6).
- **16 yeni perk**, 3 güç tier'ında (T1/T2/T3), tier kilidi ile korunuyor (§5).
- Fiyatlar **sabit TL** (anlık paranın yüzdesi değil), tier bandına göre kabaca ölçekleniyor + tier içi göreli güce göre ince ayar.

---

## 2. Omurga fiyatları (Raf / Masa / Hangar) — v2'den taşındı, DEĞİŞMEDİ

Bu üç omurganın gerçek `maxLevel`'ı ve karakteri roguelite geçişiyle değişmedi, fiyatlar aynen korunuyor.

### 2.1 Raf (Storage, maxLevel 10) — hedef payback ~1.7 gün **[v3.1 DÜZELTME — kritik bulgu (b)]**

> **Neden değişti:** v3'teki kademeli dizi (200/220/220/240/260/260/280/280/300/300 → adımlar 20/0/20/0/20/0/20/0/20) `UpgradePanel.cs:746`'daki `finalCost = round((baseCost + level×costStep) × costMultiplier)` formülüyle **üretilemiyordu** (kod, seviye başına ayrı bir dizi alanı desteklemiyor — `UpgradePanel.cs:54-57`). §0'daki "tüm diziler doğrusal formüle uyuyor" iddiası bu satırla çelişiyordu. Aşağıdaki dizi gerçek doğrusal formülle **birebir üretilebilir**, kod değişikliği gerekmez.

| Sv | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
|---|---|---|---|---|---|---|---|---|---|---|
| Fiyat (TL) | 200 | 210 | 220 | 230 | 240 | 250 | 260 | 270 | 280 | 290 |

`baseCost=200, costStep=10` (tam doğrusal, `currentLevel` 0'dan başladığı için Sv1 fiyatı = `baseCost`, Sv10 fiyatı = `baseCost + 9×costStep`). Toplam (hepsi): **2450 TL** (eski dizinin 2560 TL'sinden **-110 TL, -4.3%**). Payback hedefi orantılı olarak hafifçe iyileşiyor: `1.78 × (2450/2560) ≈ 1.70 gün` — pratikte fark edilir bir sapma değil, hedef aralıkta (~1.7-1.8 gün) kalıyor.

### 2.2 Masa (Table, maxLevel 2) — hedef payback 2.0 gün

| Sv | 1 | 2 |
|---|---|---|
| Fiyat (TL) | 360 | 470 |

Toplam: **830 TL**. `baseCost=360, costStep=110` (doğrusal, koda tam uyumlu).

### 2.3 Hangar Kapısı (Truck, maxLevel 2) — hedef payback ~2.3 gün

| Sv | 1 (2. kapı) | 2 (3. kapı) |
|---|---|---|
| Fiyat (TL) | 300 | 700 |

Toplam: **1000 TL**. `baseCost=300, costStep=400`.

> Not: `MoreCapacity_4+` bedava bug'ı (`upgrade_dual_system` belleği) hâlâ geçerli olabilir — gameplay/QA'ya tekrar hatırlatılıyor, roguelite geçişinde Yol A'ya (Inspector-driven `UpgradePanel`) tam geçilirse bu ölü kod yolu (`UpgradeAssets.GetCost()`) tamamen devre dışı bırakılmalı.

---

## 3. Perk fiyatları (16 perk, tam tablo)

Fiyatlandırma mantığı: T1 ~1.5 gün payback / T2 ~2.0-2.2 gün / T3 ~2.5-3.0 gün hedefi; niceliksel değeri hesaplanabilen perkler (Ucuz Kira, Kaldıraçlı Kira, Kumarbaz Kasası, Yüksek Volatilite, Kelle Koltukta, Prestij Simsarı) gerçek TL/gün değerinden türetildi, kalanlar (hareket hızı, sabır, mesai saati gibi soyut etkiler) tier bandı içinde göreli güce göre konumlandırıldı — bu, spec Bölüm 4'ün izin verdiği yaklaşım ("tier zaten pahalılık etkisini doğal verir").

### A) Güvenli / QoL (9 perk)

| # | Perk | Tier | Fiyat | Not |
|---|---|---|---|---|
| 3 | Telefon Hattı | T1 | **160 TL** | maxCallsPerHour +1 veya callReward ↑ |
| 5 | Mesai Saati | T1 | **200 TL** | gün süresi hafif uzar |
| 6 | Enerjik Ekip (eski Stamina) | T1 | **160 TL** | eski 3 seviyenin toplam etkisi (140 TL) tek relике sıkıştırıldı |
| 7 | Çevik Ekip (yeni) | T1 | **180 TL** | hareket hızı ↑ |
| 8 | Sabırlı Müşteriler (eski Customer) | T1 | **220 TL** | eski 2 seviyenin (250 TL) etkisi tek relike sıkıştırıldı |
| 9 | Uzun Kuyruk (eski Queue) | T1 | **240 TL** | eski 4 seviyenin (350 TL) etkisi tek relike sıkıştırıldı |
| 16 | Toplu Alım | T1 | **150 TL** | sonraki draft'ta 1 kart −%50 |
| 1 | Hızlı Hangar | T2 | **280 TL** | hangarStayDuration ↑ (~%30), Truck omurgasının "hafif" versiyonu |
| 4 | Prestij Ustası (seviyeli 2) | T2 | Lv1 **280** / Lv2 **380** | `customerServedPrestigeBonus`: 0.5→0.65→0.8. `baseCost=280, costStep=100` |

**T1 toplam (7 tek-seferlik perk):** 160+200+160+180+220+240+150 = **1310 TL**
**Hızlı Hangar + Prestij Ustası (T2 güvenli grup, tam):** 280+660 = **940 TL**

### B) Risk / gerçek trade-off (5 perk) — büyüklükler node ile hesaplandı, §4'te gerekçe

| # | Perk | Tier | Fiyat | Kazanç | Bedel |
|---|---|---|---|---|---|
| 10 | Kumarbaz Kasası | T2 | **220 TL** | kutu ödülü +%30 | kutu cezası +%55 |
| 12 | Yüksek Volatilite | T2 | **300 TL** | ortalama ödül +%15 | tek kutu ±%35 rastgele |
| 14 | Acil Fren (sigorta) | T2 | **250 TL** | iflası 1 kez önler | o gün geliri 0 + prestij −5 |
| 11 | Kaldıraçlı Kira | T3 | **350 TL** | kira −%20 (kalıcı) | prestij kaybı cezası ×2 |
| 13 | Kelle Koltukta | T3 | **800 TL** | tüm gelir +%25 (kalıcı) | grace period tamamen iptal |

**T2 risk toplam:** 220+300+250 = **770 TL** | **T3 risk toplam:** 350+800 = **1150 TL**

### C) Sinerji / ekonomi kaldıracı (3 perk) **[v3.1 — Ucuz Kira eklendi, kritik bulgu (1)]**

> **Neden eklendi:** v3'te perk #2 (Ucuz Kira) hiçbir tabloda tek satır olarak yer almıyordu, sadece §3.1/§5/§8.2'deki toplamlarda ve dolaylı anlatımda geçiyordu. Kontrol bunu 16 perkin **en kritik olanı** (spec'te "OP-potansiyelli" etiketli) olarak işaretledi — gameplay departmanının bunu Inspector'a girebilmesi için tam satır gerekiyor.

| # | Perk | Tier | Seviye | Fiyat | `rentGrowthMultiplier` basamağı |
|---|---|---|---|---|---|
| 2 | **Ucuz Kira** (seviyeli 3) | T3 | Lv1 | **130 TL** | 1.15 → **1.12** |
| | | | Lv2 | **160 TL** | 1.12 → **1.09** |
| | | | Lv3 | **190 TL** | 1.09 → **1.06** |
| 15 | Prestij Simsarı (seviyeli 2) | T3 | Lv1 | **510 TL** *(v3.2: 300→510)* | `bonusPerTier`: 5→**5.5** *(v3.2: 5→7'den küçültüldü)* |
| | | | Lv2 | **505 TL** *(v3.2: 450→505)* | `bonusPerTier`: 5.5→**6** *(v3.2: 7→9'dan küçültüldü)* |
| 16 | (Toplu Alım — yukarıda A'da sayıldı) | — | — | — | — |

**Ucuz Kira:** `baseCost=130, costStep=30` — kod formülüyle (`finalCost = baseCost + level×costStep`) birebir üretilir (Lv1=130, Lv2=130+30=160, Lv3=130+60=190). Toplam: **480 TL** (değişmedi, v3'ün örtük toplamıyla tutarlı). Etki: her seviye kira büyüme çarpanını kalıcı olarak bir basamak düşürür (3 seviye toplamda 1.15→1.06). Tam gerekçe ve oyuncu-sayısı bazlı tasarruf tablosu için bkz. **§4.0 (yeni)**.

**Prestij Simsarı:** `baseCost=510, costStep=-5` *(v3.2: fiyat 300→510/450→505, `costStep` artık negatif çünkü Lv2 marjinal maliyeti Lv1'den 5 TL ucuz — bu istisnai, çünkü fiyat artık `UpgradePanel.cs:746`'daki tek-`costStep` doğrusal formülden değil, doğrudan node ile hesaplanan hedef value/price oranından türetildi; gameplay bu perki iki ayrı sabit fiyat olarak (level dizisi) Inspector'a girmeli, doğrusal formüle zorlamamalı)*. Gerçek (kod: uncapped) değer teyidi ve fiyat/etki revizyon gerekçesi için bkz. **§4.6 (v3.2'de güncellendi)**.

### 3.1 Tam fiyat tablosu (özet, tek bakışta)

| Kategori | Toplam TL |
|---|---|
| Omurga (Storage+Table+Truck) | 4280 *(v3.1: Raf 2560→2450, -110 TL, bkz. §2.1)* |
| T1 perk (7 adet) | 1310 |
| T2 perk (5 adet: Hızlı Hangar, Prestij Ustası×2sv, Kumarbaz Kasası, Yüksek Volatilite, Acil Fren) | 1710 |
| T3 perk (4 adet: Ucuz Kira×3sv, Kaldıraçlı Kira, Kelle Koltukta, Prestij Simsarı×2sv) | 2645 *(v3.2: Prestij Simsarı 750→1015, +265 TL, bkz. §4.6)* |
| **GENEL TOPLAM (hepsi maksimum, Görev Tier hariç)** | **9945 TL** *(v3.1'in 9680'inden +265 TL, sadece Prestij Simsarı düzeltmesinden)* |

---

## 4. Risk perklerinin trade-off büyüklükleri — gerekçe ve node hesabı

### 4.0 Ucuz Kira — tam gerekçe (yeni, kritik bulgu (1) çözümü)

**Kilit varsayımı (bkz. §5 güncel T3 kuralı):** T3 artık **sadece gün≥9** ile açılıyor (VE değil, tek koşul). En erken alım günü 9 olduğu için, kalan kira ödemeleri **sadece gün 12 (cycle 2) ve gün 16 (cycle 3)** — 4 kira döneminden yalnızca son 2'si etkilenebiliyor. Bu varsayım aşağıdaki tüm hesaplara temel oluşturuyor.

Rent formülü (`CalculateRent`): `rentAmount = baseRent × rentGrowthMultiplier^cycle + totalUpgradeValue × wealthTaxRate`. `wealthTax` terimi perkten etkilenmediği için tasarruf hesabında sadeleşiyor — sadece `baseRent × (1.15^cycle − yeniMult^cycle)` farkı kalıyor.

Node ile oyuncu sayısına göre marjinal + kümülatif tasarruf (gün 12 + gün 16 toplamı):

| Oyuncu | baseRent | Lv1 tasarrufu (1.15→1.12) | Lv2 marjinal (1.12→1.09) | Lv3 marjinal (1.09→1.06) | **Toplam (3 sv)** |
|---|---|---|---|---|---|
| 1P | 500 | 92 TL | 88 TL | 84 TL | **264 TL** |
| 2P | 900 | 166 TL | 159 TL | 152 TL | **476 TL** |
| 3P | 1200 | 221 TL | 211 TL | 202 TL | **635 TL** |
| 4P | 1500 | 276 TL | 264 TL | 253 TL | **793 TL** |

**Fiyat/değer teyidi:** Toplam fiyat 480 TL. 2P (temsili orta senaryo) için toplam tasarruf 476 TL → **ratio ≈ 0.99, neredeyse tam denk (fair-value) bir alışveriş** — ne bariz kâr ne bariz zarar. 1P'de (264 TL tasarruf / 480 TL fiyat = **0.55x**) perk **zarar ediyor** — 1P oyuncusu bu perki almamalı, bu doğru bir sinyal (1P zaten en kırılgan segment, [[rent_death_spiral]]). 4P'de (793/480 = **1.65x**) perk **iyi bir kâr** sağlıyor ama patlayıcı değil.

**Sonuç: "OP" etiketi artık gerçekle örtüşmüyor.** Gün≥9 kilidi + son-2-dönem sınırlaması, Ucuz Kira'yı 1P'de marjinal-negatif, 2P'de dengeli, sadece 4P'de iyi bir yatırım haline getiriyor — bu oyuncu-sayısı asimetrisi §9 madde 3'te ayrıca not ediliyor, ama perk tek başına artık bir "otomatik al" değil.

### 4.1 Kumarbaz Kasası (+%30 ödül / +%55 ceza)

EV formülü: `EV = (1-hataOranı)×ödül - hataOranı×ceza`. `GameEconomySettings.RunSimulation()` içindeki bot-hata varsayımı (`brokenBoxes = deliveriesMade/5`) **%20 hata oranına** karşılık geliyor — bunu referans aldım:

| Hata oranı | EV (normal) | EV (Kumarbaz) | Fark |
|---|---|---|---|
| 0% (mükemmel oyuncu) | 50.0 | 65.0 | **+15.0** |
| 20% (oyun içi varsayılan bot hatası) | 32.0 | 39.6 | **+7.6** |
| 40% (kötü/panik oyuncu) | 14.0 | 14.2 | +0.2 (nötr) |
| 45%+ | 9.5 | 7.85 | **negatif** |

**Sonuç:** Tipik oyuncu (~%20 hata) için gerçek pozitif kazanç (+%24 EV artışı), ama beceri düştükçe (%40+ hata) kâr sıfırlanıp negatife dönüyor — **gerçek beceri-bazlı trade-off**, sömürülemez (kör kör her zaman alınası bir "bedava kazanç" değil).

### 4.2 Yüksek Volatilite (ort. +%15 / tek kutu ±%35)

50 TL taban kutuda: ortalama 57.5 TL/kutu, ama tek kutu aralığı **[37.4, 77.6] TL**. Ortalama her zaman pozitif (garanti +%15) — buradaki "risk" ekonomik değil **nakit akışı** riski: dar kasa marjında olan bir günde düşük uçta arka arkaya gelmek geçici sıkıntı yaratabilir. Kumarbaz'dan farklı olarak burada EV her zaman pozitif, sadece varyans var — bu yüzden T2'de Kumarbaz'dan biraz daha pahalı (300 vs 220) fiyatlandı: garanti pozitif EV, sadece dalgalanma riski taşıması Kumarbaz'ın "gerçek kayıp riski"nden daha güvenli, dolayısıyla daha değerli.

### 4.3 Kaldıraçlı Kira (kira −%20 / prestij cezası ×2) **[v3.1 — tek koşullu T3 gate ile teyit edildi]**

**[v3.2 — KÜÇÜK bulgu 3 çözümü] "-%20" uygulama tanımı:** İndirim `CalculateRent` (`GameEconomySettings.cs:109-115`) çıktısının yalnızca `scaledRent` bileşenine (`baseRent × rentGrowthMultiplier^cycle`) uygulanır — `wealthTax` (`totalUpgradeValue × wealthTaxRate`) bileşenine dokunmaz, yani `finalRent = scaledRent × 0.8 + wealthTax`. Aşağıdaki tasarruf hesapları zaten bu (muhafazakâr) tanıma göre yapıldı — sadece rapora açıkça yazılmamıştı, şimdi netleşti.

- Kazanç: T3 kilidi artık **kesinlikle gün≥9** (§5, VE/OR yok) → en erken alım gün 9, kalan **tam olarak 2 kira dönemi** (gün 12, gün 16) etkileniyor. Node ile 2P örneğinde: gün12 kira 1190 TL, gün16 kira 1369 TL, %20 tasarruf = **512 TL toplam** — bu rakam v3'teki tahminle birebir uyuşuyor, gate netleştirmesi sonucu değişmedi (zaten gün≥9 idi, sadece OR dalları kaldırıldı).
- Diğer oyuncu sayıları: 1P **284 TL**, 3P **682 TL**, 4P **853 TL** tasarruf. Fiyat 350 TL sabit → 2P'de ratio 512/350=**1.46x** (iyi ama patlayıcı değil), 1P'de 284/350=**0.81x** (hafif zararına, tutarlı — 1P zaten en fragile segment), 4P'de 853/350=**2.44x** (güçlü kâr, ama bedel de — prestij kırılganlığı — sabit kalıyor).
- Bedel: `customerLostPrestigePenalty` -1.5 → -3.0'a çıkar. `lossToZero = ceil(startingPrestige/|penalty|)`: normalde **10 kayıp** gerekirken, bu perkle **5 kayıp** prestiji sıfırlıyor. Maksimum eşzamanlı rush (GDD 9.2, 6 müşteri) neredeyse tamamının kaçması tek bir dalgada oyunu bitirebilir hale geliyor.
- **Bu gerçek bir bedel** — [[prestige_fragility]] belleğindeki eşik mantığıyla birebir örtüşüyor. T3 kilidi (gün 9+, artık tek koşul) sayesinde oyuncu bu riski alırken muhtemelen zaten belirli bir prestij tamponuna sahip, ama yine de büyük bir rush dalgası karşısında kırılganlık ikiye katlanıyor. Fiyat (350 TL) değişmiyor — mevcut değer/risk dengesi gate netleştirmesinden sonra da geçerliliğini koruyor.

### 4.4 Kelle Koltukta (+%25 gelir / grace period iptal)

- Kazanç büyük ve kalıcı: 20 kutu/gün varsayımıyla ~250 TL/gün ekstra, kalan oyun boyunca (T3 kilidi gün 9+ olsa bile 7+ gün kalıyor → 1750+ TL toplam potansiyel).
- Bedel: `gracePaymentPercent=0.8` güvenlik ağı **tamamen devre dışı** kalıyor — kira ödenemezse artık kısmi ödeme yok, doğrudan iflas. Bu, oyunun tek "ikinci şans" mekanizmasının o oyuncu için kalıcı olarak silinmesi demek — **en sert T3 perk**, bu yüzden fiyatı da grup içinde en yüksek (800 TL, diğer T3'lerin 2 katından fazla).
- **Uyarı:** Bu perk Acil Fren (sigorta, §4.5) ile aynı anda alınırsa ilginç bir sinerji oluşur (sigorta grace'in yerini bir kez alabilir) — kontrol/QA'nın bu kombinasyonu playtestte doğrulaması önerilir.

### 4.5 Acil Fren (sigorta)

Tek seferlik "iflası önler" relik — gerçek bedeli o günün gelirinin sıfırlanması + prestij -5 (startingPrestige=15'in üçte biri, hafif ama hissedilir bir ceza). T2 bandında en ucuzlardan (250 TL) çünkü çoğu oyunda hiç tetiklenmeyebilir (sigorta primi mantığı) — tetiklendiğinde değeri "sonsuz" (oyunu bitmekten kurtarıyor) ama garanti değil.

### 4.6 Prestij Simsarı — değer/fiyat düzeltmesi (v3.2, KRİTİK 1 + KRİTİK 2 çözümü, TUR 3/3 — SON tur)

**[v3.2] Kontrol turu 2'nin bulgusu:** v3.1'de Prestij Simsarı'nın değer/fiyat oranı 2P'de ~5.9x, 4P'de ~18x'e çıkıyordu — havuzdaki diğer tüm perkler 0.55x-2.44x bandındayken bu perk koşulsuz "otomatik al" kartına dönüşüyordu. Ayrıca v3.1'in kendi hesabı yanlış fiyat bazı kullanıyordu (Prestij USTASI'nın 280/660 fiyatlarıyla bölünmüştü, oysa Simsarı'nın kendi fiyatı 300/450=750 idi) — bu KRİTİK 2 bulgusuydu. Aşağıda **her iki bulgu birlikte** çözülüyor: doğru fiyat bazıyla yeniden hesap + hem etki hem fiyat düzeltmesi.

**Seçilen kaldıraç: (a) + (b) birlikte.**
- **(a) Etki küçültüldü:** `bonusPerTier` basamağı **5→7→9** (Lv1 delta=+2, Lv1+Lv2 kümülatif delta=+4) yerine **5→5.5→6** (Lv1 delta=+0.5, kümülatif delta=+1) yapıldı — yani orijinal etkinin **%25'ine** indirildi (delta 4 kat küçüldü). Bu, kontrolün örnek olarak verdiği "5→6→7" (yarıya indirme) tek başına yeterli olmadığı için (bkz. aşağıdaki node hesabı) daha agresif uygulandı. `bonusPerTier` artık ondalıklı bir tasarım parametresi (kod tarafında `float` olarak taşınabilir, `Truck.cs:590-597`'deki formülde katsayı olarak çarpılıyor, tamsayı zorunluluğu yok).
- **(b) Fiyat yukarı çekildi:** Lv1 **300→510 TL**, Lv2 **450→505 TL** (toplam **750→1015 TL**).

**Neden sadece (a) veya sadece (b) yetmiyordu (node ile doğrulama):**
- Sadece (a) (etkiyi yarıya indirip fiyatı sabit 750 TL bırakmak): 4P'de oran hâlâ ~9x (18x'in yarısı) — hedefin çok üzerinde.
- Sadece (b) (etkiyi değiştirmeden sadece fiyatı yükseltmek): 4P'de 2.5x'e inmek için toplam fiyatın **~4044 TL**'ye çıkması gerekiyordu — bu, en pahalı diğer T3 perkin (Kelle Koltukta, 800 TL) 5 katı, tier içi fiyat mimarisini anlamsızlaştırırdı.
- **(a)+(b) birlikte** (etki %75 küçültülüp fiyat ~750→1015'e çekilerek) her iki aşırılığı da önlüyor: fiyat hâlâ makul bir T3 üst-bandı (Kelle Koltukta'nın ~1.27 katı, aşırı değil) ve etki küçültmesi de ölçüsüz değil (delta 4 kat, `bonusPerTier`in taban değeri 5'e görece hâlâ anlamlı bir artış — 5→6 toplamda).

**Node hesabı — yeni değerler (orijinal emergent tablo, delta orantılı ölçeklendi çünkü değer = delta × tier × teslimat sayısı, doğrusal):**

| Senaryo | Lv1 tek başına (yeni, delta+0.5) | Lv1+Lv2 birlikte (yeni, delta+1) |
|---|---|---|
| 1P yavaş | 58.5 TL | 116.75 TL |
| 1P orta | 85.75 TL | 171.25 TL |
| 2P orta | 411.75 TL | 823.25 TL |
| 4P iyi | 1263.75 TL | 2527.25 TL |

**Yeni fiyat/değer oranları (Lv1 fiyatı 510 TL, kümülatif Lv1+Lv2 fiyatı 1015 TL):**

| Senaryo | Lv1 oranı | Lv1+Lv2 oranı |
|---|---|---|
| 1P yavaş | 0.11x | 0.12x |
| 1P orta | 0.17x | 0.17x |
| 2P orta | 0.81x | 0.81x |
| 4P iyi (**en kötü senaryo**) | **2.48x** | **2.49x** |

**Sonuç:** En kötü senaryo (4P) oranı 18x → **2.48x-2.49x**'e indi — kontrolün istediği "diğer T3'lerle aynı banda, en kötü senaryoda ~≤2.5x" hedefine (küçük bir güvenlik payıyla, 2.5x'in hemen altında) ulaşıldı. 1P'de perk artık belirgin biçimde zararına (0.11x-0.17x) — bu KABUL EDİLDİ (talimatın izin verdiği gibi, Ucuz Kira emsaliyle tutarlı: 1P zaten en kırılgan segment, bu perki almamalı). 2P'de de artık makul-altı bir yatırım (0.81x, hafif zararına) — bu, perkin artık "her player count'ta otomatik al" olmaktan çıkıp yalnızca 3P-4P'nin üst ucunda (iyi senaryoda) kâra geçen gerçek bir "büyük takım sinerji kaldıracı"na dönüştüğünü gösteriyor, spec'in kategori tanımına (§3-C, "sinerji/ekonomi kaldıracı") artık daha uygun.

**Kalan not (kapsam dışı, gelecek tur önerisi olarak §9'da kalıyor):** Fiyat hâlâ sabit TL (oyuncu sayısına göre ölçeklenmiyor) olduğu için 1P/2P'de zararına, 4P'de sınırda-kabul-edilebilir bir asimetri var. Bu, Ucuz Kira'nın §4.0'daki asimetrisiyle aynı kategoride (kasıtlı, playtestte izlenmeli) — mimari değişikliği (oyuncu sayısına göre ölçeklenen fiyat) bu turun kapsamı dışında kalmaya devam ediyor, çünkü artık patlayıcı değil, sadece hafif asimetrik.

---

### 4.6-ARŞİV — v3.1'deki eski (düzeltilen) analiz

> **[v3.2 uyarı]** Aşağıdaki analiz **tarihsel referans** amaçlıdır — orijinal `bonusPerTier` etkisiyle (delta +2/+4) yapıldı ve fiyat bazı **yanlıştı** (KRİTİK 2: burada kullanılan "280/660" aslında Prestij USTASI'nın fiyatı, Simsarı'nın v3.1 fiyatı 300/750 idi). Geçerli, güncel analiz ve doğru fiyat bazı için yukarıdaki asıl **§4.6** bölümüne bakın. Bu arşiv yalnızca "neden değiştirdik" sorusuna şeffaflık sağlamak için korunuyor.

**Kaynak düzeltmesi:** v3'ün "tavan 75, Prestij Simsarı Lv2 ile 140" hesabı, `GameEconomySettings.RunSimulation()`'daki (editör-only simülasyon aracı, `Truck.cs`/`PrestigeManager.cs` gerçek kodunda karşılığı yok) yerel `prestige = Mathf.Clamp(prestige, 0, 100)` satırına dayanıyordu. Gerçek `PrestigeManager.ModifyPrestige()` prestiji **hiçbir yerde kırpmıyor** — kutu ödülü teorik olarak sınırsız büyüyebilir. Kontrol'ün doğru işaret ettiği gibi bu bir varsayım hatasıydı; ama node ile gerçekçi bir 16-günlük simülasyon koşturulduğunda (kırpmasız, `customerServedPrestigeBonus=0.5`, `boxDropPrestigePenalty=-0.05`, gerçekçi teslimat hacimleriyle) **emergent** (kodda sabitlenmemiş, ama pratikte oluşan) bir aralık ortaya çıkıyor:

| Senaryo | Gün 16 prestij tier'i | rewardPerBox (perksiz, taban `bonusPerTier=5`) |
|---|---|---|
| 1P yavaş/kötü | tier 4 | **70 TL** |
| 1P orta | tier 5 | **75 TL** |
| 2P orta (temsili) | tier 10 | **100 TL** |
| 4P iyi | tier 16 | **130 TL** |

Yani kontrol'ün "fiili tavan ~100 TL" tahmini **2P temsili senaryo için doğrulandı**, ama bu kodda sabit bir tavan değil — oyuncu sayısı/beceriye göre 70-130 TL arasında **emergent** bir aralık. "Tavan 75" iddiası (v3 §0) yanlıştı, düzeltildi (bkz. §0).

**Prestij Simsarı'nın gerçek TL değeri** (gün≥9'dan itibaren alındığı varsayımıyla, `bonusPerTier` deltasının kalan oyun boyunca her teslimatta çarpan etkisi):

| Senaryo | Lv1 tek başına (+2/tier, gün9+) | Lv1+Lv2 birlikte (+4/tier, gün9+) |
|---|---|---|
| 1P yavaş | **234 TL** | 467 TL |
| 1P orta | **343 TL** | 685 TL |
| 2P orta | **1647 TL** | 3293 TL |
| 4P iyi | **5055 TL** | 10109 TL |

**Sonuç:** Fiyat (Lv1 280 TL, Lv1+Lv2 660 TL) 1P'de zar zor kârlı (234/280=0.84x, 343/280=1.2x) — makul. Ama 2P'de 1647/280=**5.9x**, 4P'de 5055/280=**18x** — bu perk, teslimat hacmiyle çarpımsal olarak büyüdüğü için **oyuncu sayısı arttıkça patlayıcı biçimde değerleniyor** (zaten güçlü olan takımları daha da güçlendiren bir "sinerji kaldıracı" — kategori adına uygun ama büyüklüğü v3'te öngörülenden çok daha yüksek). **Dar kapsam kararı:** Bu turda fiyat değiştirilmiyor (kapsamı aşar, oyuncu-sayısına göre ölçeklenen fiyatlandırma mimarisi gerektirir), ama **yeni bir orta-önem exploit riski** olarak §9'a eklendi — gameplay/kontrol'ün gelecek bir turda (fiyatı oyuncu sayısına göre ölçeklendirme veya `bonusPerTier` artışını sabit yerine yüzdesel yapma) değerlendirmesi öneriliyor.

---

## 5. Tier kilit eşikleri

| Tier | Kilit koşulu | Gerekçe |
|---|---|---|
| T1 | Her zaman açık (gün 1'den itibaren) | Güvenli/ucuz, erken oyun deneyimini çeşitlendirir |
| T2 | **Gün ≥ 5** | İlk kira döneminin (gün 4) hemen ardından — oyuncu bir kira ödemesi deneyimlemiş, orta güçte perklere hazır |
| T3 | **Gün ≥ 9** (tek koşul, ZORUNLU) | İkinci kira döneminden sonra (gün 8), oyuncunun elinde gerçek bir nakit tamponu ve prestij geçmişi birikmiş olmalı |

> **[v3.1 DÜZELTME — kritik bulgu (2) ve önemli bulgu (4)]** v3'teki T2/T3 kilitleri "mağaza seviyesi≥2/3" ve "prestij≥30" OR koşulları içeriyordu. Bunlar iki nedenle kaldırıldı:
> 1. **`storeLevel` gerçek oyunda çalışmıyor.** `CustomerManager.cs:71`'de `_storeLevel` sabit `1` olarak tanımlı, onu artıran bir XP/seviye sistemi kodda yok (`GameEconomySettings.cs:255`'teki `storeLevel = 1 + floor(totalUpgradeValue/600)` de yalnızca editör simülasyon aracının yerel değişkeni, gerçek oyunu etkilemiyor). "mağaza seviyesi≥2/3" koşulu pratikte **asla tetiklenmiyor** — ölü kod yolu.
> 2. **`prestij≥30` çok erken tetiklenebiliyordu.** `customerServedPrestigeBonus=0.5`, `startingPrestige=15` ile prestij 30'a ulaşmak için sadece **30 doğru teslimat** yeterli — bu gün 4-6 gibi erken bir noktada gerçekleşebilir (2P orta senaryoda gün 3'te tier zaten 26.6'ya ulaşıyor, bkz. §4.6 tablosu). Bu, T3'ün "gün≥9 garantisi" iddiasıyla doğrudan çelişiyordu ve Ucuz Kira'nın 4 kira döneminden 3'ünü (sadece 1'i değil) etkileyebilmesi riskini doğuruyordu — §3/§4.0'daki 480 TL fiyat gerekçesi bu durumda geçersiz kalırdı.
>
> **Karar: T3 artık sadece `Gün ≥ 9` (VE/OR yok, tek koşul).** Bu hem storeLevel'in ölü kod sorununu (gameplay'in gerçek bir seviye sistemi kurmasına gerek kalmadan) çözüyor hem de Ucuz Kira/Kaldıraçlı Kira'nın "son 2 dönem" varsayımını matematiksel olarak garanti altına alıyor (bkz. §4.0, §4.3 — fiyatlar bu netleşen kurala göre teyit edildi).

**Ucuz Kira (T3, eski "OP-potansiyelli" etiketi) günlerde 1-8 KESİNLİKLE çıkamaz** — gün ≥9 kilidiyle garanti altına alınmış, artık tek koşul olduğu için gate atlanamaz. Bu kilit sayesinde Ucuz Kira sadece **son 2 kira dönemini** (gün 12, 16) etkileyebiliyor — §4.0'daki fiyat/değer teyidi (2P'de ratio≈0.99, 1P'de 0.55x) bu netleşen kurala göre yapıldı. 1P'de bu perkin marjinal (hatta hafif zararına) değer taşıdığı uyarısı için bkz. **§9 madde 3**.

RNG kuralı (spec 1.6) korunuyor: kilidi açık tier havuzu içinde saf rastgele, teklifte tekrar yok, max'a ulaşan düşer.

---

## 6. Görev Tier kararı — NET KARAR

**Karar: Görev Tier draft havuzundan TAMAMEN ÇIKARILDI, sistem reaktive edilene kadar hiç teklif edilmeyecek.**

Gerekçe: Görev sistemi pasif olduğu sürece bu upgrade'in EV'si tam olarak sıfır. Roguelite draft mimarisi zaten "kilidi açık tier havuzu"ndan filtreleme yapıyor (spec Bölüm 2.3) — bu mekanizma bir "tier eşiği" yerine bir **boolean sistem-aktif bayrağı** ile genelleştirilerek Görev Tier'a uygulanabilir: `questSystemActive == false` iken bu kart havuzdan tamamen filtrelenir, tıpkı "kilidi kapalı T3" gibi davranır ama gün/seviye eşiği yerine bir feature-flag'e bağlıdır.

Bu, önceki v2 raporundaki "Seçenek A" (askıya al) kararının roguelite mimarisine uyarlanmış hali — **sembolik fiyat vermiyoruz**, çünkü artık draft mekanizması zaten "bazı kartlar bazen havuzda değildir" mantığını doğal olarak destekliyor; sembolik bir fiyat koyup UI'da özel olarak gizlemeye çalışmaktansa, aynı filtreleme altyapısını (tier kilidi) kullanmak hem daha tutarlı hem daha az ek iş. Görev sistemi reaktive edildiğinde: `questSystemActive = true` yapılır, bayrak kaldırılır, gerçek EV-bazlı fiyatlandırma (v2 raporunun 9. bölümünde arşivlenmiş EV=tier×250 TL yaklaşımı) devreye alınabilir.

---

## 7. Reroll fiyat eğrisi

Her gün sıfırlanır, günlük bağımsız artan eğri (×1.8/reroll):

| Reroll # | Fiyat |
|---|---|
| 1 | **50 TL** |
| 2 | **90 TL** |
| 3 | **160 TL** |
| 4 | **290 TL** |
| 5 | **525 TL** |

**Gerekçe:** İlk reroll (50 TL) erken oyunda bile önemsiz bir maliyet — draft'ı bozacak kadar ucuz değil (bir T1 perkin ~1/3'ü) ama caydırıcı da değil. ×1.8 büyüme oranı: 3. reroll'da bile (160 TL) hâlâ en ucuz T1 perkten daha pahalı olmaya başlıyor, bu doğal bir "çok fazla reroll'lamayı" caydırma etkisi yaratıyor, 5. reroll'da (525 TL) bir T3 perk fiyatına ulaşıyor — pratik üst sınır burada oluşuyor (gameplay isterse 4-5 rerolldan sonra sabit fiyata kilitleyebilir, ekonomik olarak zorunlu değil).

---

## 8. Bütçe fizibilitesi — DÜRÜST hesap (kontrol bulgusu (a) çözümü)

Önceki v2/Faz2 raporlarının **"tier kilidini yok sayan, eşit-dağılım" modeli yanıltıcıydı** (bkz. altta §8.1). Bu sürümde iki model karşılaştırıldı: naif (kilitsiz) ve **kilit-farkında (gate-aware)** — ikincisi gerçek oyun deneyimini yansıtıyor.

### 8.1 Kilit-farkında (gate-aware) simülasyon — asıl geçerli sonuç

Yöntem: Omurga+T1 havuzu gün 1'den, T2 havuzu gün 5'ten, T3 havuzu gün 9'dan itibaren harcanabilir kabul edilip, her gün kasadan (100 TL güvenlik payı bırakarak) mümkün olduğunca harcama yapıldı; her kira gününde `CalculateRent` (gerçek formül, kümülatif harcamanın %10'u wealthTax) uygulandı.

| Senaryo | Oyuncu | Günlük gelir | Sonuç |
|---|---|---|---|
| Düşük gelir | 1P | 600 TL/gün | **İFLAS gün 12** (kasa -1165 TL) |
| Düşük gelir | 2P | 1000 TL/gün | Hayatta kalıyor, **son kasa sadece 92 TL** (aşırı sıkı) |
| Orta gelir | 1P | 900 TL/gün | Hayatta kalıyor, son kasa 489 TL (sıkı ama rahat) |
| Orta gelir | 2P | 1500 TL/gün | Hayatta kalıyor, son kasa 8092 TL (rahat) |
| Yüksek gelir | 1P | 1500 TL/gün | Hayatta kalıyor, son kasa 10089 TL (rahat) |

**Sonuç (net rakamla):** 16 günlük oyunda 20 karttan (Görev Tier hariç) **hepsini almak toplamda 9945 TL** tutuyor (v3.2: Prestij Simsarı fiyat düzeltmesi +265 TL, bkz. §3.1/§4.6 — bu rakam §4.6'daki değer/fiyat düzeltmesinin doğrudan sonucu, stres testinin sonucunu değiştirmiyor çünkü zaten en kısıtlı senaryo 1P düşük-gelirdi ve o senaryoda oyuncu bu kadar pahalı bir T3 perkine gün 12'den önce ulaşamıyor). Bu, **1P'nin düşük-gelir senaryosunda (600 TL/gün) matematiksel olarak imkansız** — gün 12'de iflas ediyor, tier kilidi zaten erken aşırı harcamayı önlemesine rağmen. **2P ve üstü, ya da 1P'nin orta/yüksek gelir senaryosunda "sıkı ama mümkün"** (2P düşük gelirde son kasa sadece 92 TL — pratikte "her şeyi almak" bir güvenlik marjı bırakmıyor, tek bir kötü rush dalgası veya bir kira gecikmesi oyunu bitirebilir).

**Yorum:** Bu istenen bir sonuçtur, kaza değil — roguelite draft zaten "3 kart/gün, sınırlı seçim" mantığıyla çalışıyor; oyuncunun 16 günde 20 kartın TAMAMINI görme/alma ihtimali zaten düşük (RNG + tier kilidi + reroll maliyeti). Bu simülasyon bir üst-sınır stres testidir: "eğer her şey teklif edilseydi ve her şeyi almaya çalışsaydınız" sorusuna cevap veriyor. Gerçek oynanışta oyuncu muhtemelen 20 karttan 10-14'ünü görüp bir kısmını seçici alacak — bu senaryo o günlük seçimin **tam bütçe baskısı yaratacak kadar sıkı** olduğunu, ama tek bir "hepsini toplama" zorunluluğu olmadığını gösteriyor.

**Gameplay/kontrol'e not:** 1P modunun düşük-gelir ucu (600 TL/gün — GDD'nin "yavaş/kötü oyuncu" botunun temsil ettiği alt sınır) hâlâ en kırılgan segment; bu [[rent_death_spiral]] bulgusuyla tutarlı (1P her zaman en hassas oyuncu sayısı). Roguelite yapısı bu kırılganlığı çözmüyor, sadece taşıyor — düşük gelirli 1P oyuncu zaten "her şeyi almaya çalışmamalı," bu perk sisteminde de geçerli, playtestte doğrulanmalı.

### 8.2 Rent-stack riski kontrolü

Ucuz Kira (Lv3, 1.15→1.06) + Kaldıraçlı Kira (-%20) aynı anda alınırsa (ikisi de T3, aynı draft'ta çıkmaları mümkün): 2P örneğinde kira **%32-37 arası indirim** görüyor (cycle 2: %32, cycle 3: %37.4) — bu, sıfıra yakın bir kira değil, iki ayrı kaldıracın makul toplamı. **Exploit değil**, ama gameplay/QA'nın bu ikilinin birlikte çıktığı senaryoyu playtestte özellikle test etmesi öneriliyor (özellikle Kaldıraçlı Kira'nın prestij cezası ×2 bedeliyle birlikte).

---

## 9. Duvar hissi / exploit riskleri özeti (v3)

1. **1P düşük-gelir "hepsini alma" senaryosu matematiksel olarak imkansız** (§8.1) — tasarım gereği kabul edilebilir, ama playtestte doğrulanmalı.
2. **Kelle Koltukta (800 TL) grubun en pahalısı ve en riskli perk'i** — grace period'un kalıcı iptali geri döndürülemez bir bedel, oyuncuya net uyarı gösterilmeli (UI'da "GERİ ALINAMAZ" etiketi önerilir, ekonomik değil ama UX riski).
3. **Ucuz Kira'nın fiyatı player-count'a göre asimetrik değer taşıyor — artık kesin sayılarla teyitli [v3.1]:** Sabit 480 TL fiyat, gün≥9 tek-koşullu T3 kilidiyle netleşen "son 2 kira dönemi" varsayımı altında: **1P'de tasarruf/fiyat = 0.55x (zararına)**, **2P'de 0.99x (tam denk)**, **3P'de 1.32x**, **4P'de 1.65x (kârlı)** (bkz. §4.0). Perk fiyatları oyuncu sayısına göre ölçeklenmiyor (omurga upgrade'lerle tutarlı bir tasarım kararı) ama bu, 1P için gerçekten kötü bir "deal", 4P için iyi bir "deal" yaratıyor. Kasıtlı bırakıldı, artık "OP" değil (gate + fiyat teyidiyle çözüldü) — ama 1P oyuncusuna bu perkin **zararına** olabileceği playtestte/UI'da hissettirilmeli.
4. **`MoreCapacity_4+` bedava bug'ı** hâlâ düzeltilmemiş olabilir (`upgrade_dual_system` belleği) — Yol A'ya geçişte bu ölü kod yolu tamamen kapatılmalı.
5. **Kaldıraçlı Kira'nın prestij cezası ×2'si**, [[prestige_fragility]] bulgusundaki eşik mantığını yarıya indiriyor (10→5 kayıpla sıfırlanma) — T3 kilidiyle (gün 9+, artık tek koşul) kısmen yumuşatılmış olsa da gerçek bir kırılganlık, playtestte özellikle rush yoğun günlerde test edilmeli.
6. **Kumarbaz Kasası** yalnızca ~%20 hata oranına kadar pozitif EV taşıyor — bu iyi bir tasarım (gerçek beceri eşiği var) ama UI'da oyuncuya bu eşiğin sezgisel olarak anlatılması gerekiyor (aksi halde "her zaman iyi" sanıp kötü oyuncularda zararına dönebilir, bu istenen risk ama sürpriz olmamalı).
7. **Reroll eğrisi** (50→525) 5. rerolldan sonra bir T3 perk fiyatına eşitleniyor — üst sınır doğal oluşuyor, ek cap gerekmez ama gameplay izlemeli.
8. **[v3.1 yeni] Yüksek Volatilite'nin EV'si her koşulda garanti +%15** (§4.2) — hata oranından bağımsız, otomatik alım mantıklı ("her zaman iyi bir kart"), spec'in Bölüm 4 "gerçek trade-off" ilkesiyle gerilim yaratıyor (Kumarbaz Kasası'nın aksine burada gerçek bir kayıp riski yok, sadece nakit akışı varyansı var). Bloklayıcı değil, ama **playtestte izlenmeli** — eğer oyuncular bunu sistematik olarak Kumarbaz Kasası yerine "bedava üstün seçenek" olarak görüyorsa, varyansı artırıp/ortalamayı düşürerek gerçek bir trade-off'a çevirmek gerekebilir.
9. **[v3.2 ÇÖZÜLDÜ, önceki v3.1 patlaması] Prestij Simsarı artık diğer T3'lerle aynı bantta.** v3.1'de 2P'de 5.9x, 4P'de 18x değer/fiyat oranına ulaşıyordu (yanlış fiyat bazıyla hesaplanmıştı, KRİTİK 2). v3.2'de hem etki küçültüldü (`bonusPerTier` 5→7→9 yerine 5→5.5→6) hem fiyat yükseltildi (750→1015 TL toplam) — yeni oranlar: 1P 0.11x-0.17x (zararına, kabul edilebilir), 2P 0.81x-0.82x, 4P (en kötü senaryo) **2.48x-2.49x** — hedef banda (~≤2.5x) girdi. Detay: **§4.6**. Kalan hafif asimetri (fiyat oyuncu sayısına göre ölçeklenmiyor) bilinçli bırakıldı, Ucuz Kira'daki (§9 madde 3) asimetriyle aynı kategoride, patlayıcı olmadığı için bloklayıcı değil.

---

## 10. Sonraki adım önerisi

- **writing-plans**'a: bu rapor + spec Bölüm 5 (mimari) → uygulama planı.
- **gameplay**'e: veri-güdümlü perk tanımları (id, tier, kilit koşulu, tip, fiyat(lar), etki referansı, trade-off) + tier-filtreli draft + reroll + Görev Tier'ın draft'tan tam filtrelenmesi (§6, feature-flag mantığı).
- **QA**'ya: rent-stack senaryosu (§8.2), Kelle Koltukta+Acil Fren kombinasyonu, `MoreCapacity_4+` bug teyidi.
- **kontrol**'e: bu rapor + spec + bu özet → ONAY/DÜZELTME kapısı.

---

---

# [ARŞİV] v2 Raporu (2026-07-08, roguelite öncesi yapı — artık kısmen geçersiz)

> Aşağıdaki içerik, roguelite draft geçişinden ÖNCEKİ "tüm upgrade'leri listele" yapısına aittir. Money/Stamina/Queue/Water/Customer artık ayrı upgrade değil (yukarıda §1 not); Quest Tier kararı yukarıda §6'da GÜNCELLENDİ (v2'nin "Seçenek A" önerisi burada "draft'tan tam filtrele" olarak somutlaştırıldı). Storage/Table/Truck fiyat tabloları hâlâ geçerli ve yukarıda §2'ye taşındı. Bu bölüm yalnızca tarihsel referans ve hesap şeffaflığı için korunuyor.

## 0. Baz alınan Faz 1 sabitleri (değişmedi)

| Parametre | Değer |
|---|---|
| startingMoney | 500 TL |
| rentGrowthMultiplier | 1.15 |
| wealthTaxRate | 0.10 |
| rentIntervalDays | 4 |
| baseRent [1P,2P,3P,4P] | 500 / 900 / 1200 / 1500 |
| rewardPerBox | 50 TL (prestij tier'larla 75 TL'ye çıkar) |
| penaltyPerBox | 40 TL |
| Kapasite formülü | `clamp((raf×3 + mağazaSeviyesi×2 + Random(-2,+3)) × oyuncuMult, 1, 50)` |

Prestij tier'a göre kutu başı gelir (rewardAt fonksiyonu, `prestigePerBonus=10`, `bonusPerTier=5`, tavan 75 TL):

| Prestij | 0-9 | 10-19 | 20-29 | 30-39 | 40-49 | 50+ |
|---|---|---|---|---|---|---|
| TL/kutu | 50 | 55 | 60 | 65 | 70 | 75 |

## 1. Gerçek envanter (v2, roguelite öncesi — artık kısmen kaldırıldı)

| Upgrade | Ne yapar | maxLevel | Roguelite'ta durumu |
|---|---|---|---|
| Storage (raf) | Kapasite formülüne raf ekler | 10 | **Omurga olarak korundu** (§2.1) |
| Table (masa) | Ek paketleme masası slotu | 2 | **Omurga olarak korundu** (§2.2) |
| Queue (kuyruk) | maxQueueSize artırır | 4 | **Perk'e dönüştü** (Uzun Kuyruk, T1, §3) |
| Money (gelir) | rewardPerBox +10/seviye | 3 | **Kaldırıldı**, yerine Kumarbaz Kasası/Yüksek Volatilite |
| Stamina | staminaRegenRate artırır | 3 | **Perk'e dönüştü** (Enerjik Ekip, T1, §3) |
| Truck (hangar) | 2. ve 3. hangar kapısı açar | 2 | **Omurga olarak korundu** (§2.3) |
| Quest Tier | Görev zorluk+ödül — sistem PASİF | 2 | **Draft'tan tam filtrelendi** (§6) |
| Water | Sadece achievement | 1 | **Kaldırıldı** (spec 3.2) |
| Customer | Müşteri bekleme süresi (patience) artırır | 2 | **Perk'e dönüştü** (Sabırlı Müşteriler, T1, §3) |

*(v2'nin geri kalan tam hesap detayları — masa/kuyruk/para/stamina/quest/water/customer için ayrı payback tabloları — bu commit öncesi git geçmişinde korunuyor; roguelite yapısına taşınan sonuçlar yukarıdaki §2-§3'te güncel haliyle mevcut.)*
