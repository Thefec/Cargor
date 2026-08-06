---
name: quest-hard-targetcount-retune-2026-07-29
description: Hard tier targetCount (is miktari) Medium'a gore para-buyumesiyle eslesecek sekilde yeniden olceklendi (odul/ceza SABIT kaldi) - colorless 10->12, renk-kilitli 4->5, Truck degismedi (3) - musteri renk-torbasi TAM 1/3 kod-dogrulandi
metadata:
  type: project
---

**Bağlam**: 2026-07-29, müdür veriye dayalı tespit: Medium→Hard geçişinde iş miktarı (targetCount)
+%33-50 büyürken ödül +%65-68 büyüyor (renksiz Shelf/Pack 7→10 = +43%, renk-kilitli 3→4 = +33%,
Truck 2→3 = +50%; para 34→57 = +68%, premium 52→86 = +65%). Görev: SADECE targetCount düzelt,
ödül/ceza [[quest_fixed_reward_table_2026-07-28]] sabit kalsın.

## Yeni kod-doğrulama: müşteri renk-torbası TAM 1/3 (önceki tur varsayımdı, bu tur KESİN)

`CustomerManager.cs:1020 DrawNextCustomerColorFromBag()` — Red/Yellow/Blue'yu bir torbaya koyup
shuffle'lar, replacement'sız çeker, boşalınca yeniden doldurur. Ürün rengi (Toy/Clothing/Glass)
ÖNCE renk seçilip SONRA o renkteki ürün prefabları arasından seçiliyor (`PickCustomerColor` →
`GetOrBuildColorIndexMap`), yani renk dağılımı prefab SAYISINA bağlı DEĞİL (Toy 4 prefab,
Clothing/Glass 2'şer prefab olsa da fark etmez — renk önce seçiliyor). `FAVOR_CHANCE` ile mevcut
tır rengine hafif meyil + `MAX_CONSECUTIVE_SAME_COLOR` art-arda sınırı var ama bunlar küçük
düzeltmeler, temel dağılım dengeli. **[[quest_tier_redesign_2026-07-25]]'teki "playtest-doğrulanmamış
~1/3" notu artık KESİN — renk-kilitli hedefler colorless'ın güvenle 1/3'ü olarak modellenebilir.**

## Yeni metodoloji: box-capacity modeli (fullTrucksPerDay yerine doğru birim)

Önceki tur (`quest_tier_redesign`) Shelf/Pack (kutu-birimi) hedefleri `fullTrucksPerDayEstimate`
(tır-birimi) ile kıyaslamıştı — birim uyuşmazlığı, kabaca yaklaşıklık. Bu tur DAHA DOĞRU bir
proxy kullanıldı: `boxCapacityPerDay(P, boxesPerMin, day) = boxesPerMin * P * (dayDuration(day)/60)`
— günün TAMAMI kutu üretimine ayrılmış gibi varsayılan üst sınır (aynı "optimistic" felsefesi,
[[truck_hangar_window_cap]] ile tutarlı). Renk-kilitli hedefler için bu kapasitenin **/3**'ü
kullanıldı (yukarıdaki torba kanıtıyla artık meşru). Tamamlanma-oranı step-fonksiyonu KORUNDU
(cap/target ≥1.5→85%, ≥1.0→65%, ≥0.5→40%, ≥0.25→20%, altı→8%, [[quest_tier_redesign_2026-07-25]]
ile aynı). CompleteTruck için doğru birim zaten `fullTrucksPerDayEstimate` (tır-birimi), değişmedi.

Gün uzunlukları KOD-DOĞRULANMIŞ (`DayCycleManager.cs`): gün1=160s, gün8=210s, gün12=250s, gün16=290s
(realDurationInSeconds=160 + (day-3)*10, dynamicDurationStartDay=3).

## Sonuç tablosu (9 Hard asset, SADECE targetCount değişti)

| Dosya | Tip | Eski→Yeni targetCount | Gerekçe |
|---|---|---|---|
| `Q_Hard_1_Truck` | CompleteTruck | **3 → 3 (değişmedi)** | 4'e çıkarmak 2P/3P/4P tamamlanma bandını 65%→40%'a düşürüyor (node-doğrulandı); Truck zaten "premium" ödül grubunda (×1.5) hangar-pencere riskini fiyatlıyor, targetCount'la ayrıca cezalandırmaya gerek yok |
| `Q_Hard_2_Shelf` (renksiz) | PlaceBoxOnShelf | **10 → 12** | iş-büyüme %71 (7→12), para-büyüme %68 (34→57) ile eşleşiyor; 3P/4P Normal 85% bandında kalıyor |
| `Q_Hard_4_Pack` (renksiz) | PackToy | **10 → 12** | yukarıdakiyle aynı gerekçe (aynı kapasite havuzu, aynı tip formül) |
| `Q_Hard_3_ShelfYellow` | PlaceBoxOnShelf (renk) | **4 → 5** | iş-büyüme %67 (3→5), para-büyüme %68 ile neredeyse birebir eşleşiyor (en iyi eşleşen çift) |
| `Q_Hard_6_ShelfBlue` | PlaceBoxOnShelf (renk) | **4 → 5** | aynı gerekçe |
| `Q_Hard_7_ShelfRed` | PlaceBoxOnShelf (renk) | **4 → 5** | aynı gerekçe |
| `Q_Hard_5_PackGlass` | PackToy (renk) | **4 → 5** | aynı gerekçe |
| `Q_Hard_8_PackToy` | PackToy (renk) | **4 → 5** | aynı gerekçe |
| `Q_Hard_9_PackCloth` | PackToy (renk) | **4 → 5** | aynı gerekçe |

## Medium tier: DEĞİŞİKLİK GEREKMİYOR

Easy→Medium büyüme oranı (renksiz iş %75 [4→7] vs para %89 [18→34], renk-kilitli iş %50 [2→3]
vs para %89) zaten [[quest_tier_redesign_2026-07-25]]'te playtest-öncesi kabul edilmiş bir
kademe idi ve iş/para büyüme farkı (~15-40pp) Medium→Hard'daki kadar SIÇRAMALI değil — Medium
zaten "2P+ rahat, 1P zorlu" bandında sim-doğrulanmış duruyor. Bu tur SADECE Hard'ın Medium'a göre
orantısız sıçraması düzeltiliyor, zincirin geri kalanına dokunulmadı.

## Yapılabilirlik (node-doğrulandı, gün12 temsili Hard günü)

Yeni değerlerle (12 renksiz / 5 renkli) tamamlanma bandı: 1P her zaman aynı kaldı ya da hafif
düştü (40%→40% Normal, 40%→20% Yavaş renksiz) — **1P zaten Hard'a fiilen erişemiyordu, bu tur onu
değiştirmedi, sadece mevcut durumu netleştirdi.** 3P/4P Normal 85% bandında SABİT kaldı (hiçbir
yeni "duvar" yok). 2P ve *-Yavaş senaryolarda bir basamak düşüş var (örn. 2P Normal renksiz
85%→65%), ama hiçbir kombinasyon 8% (neredeyse-imkansız) bandına DÜŞMEDİ — en kötü yeni durum 20%
(1P Yavaş), bu zaten eski değerlerle de yakın bandtaydı. **Kabul opsiyonel olduğu için** (rasyonel
takım EV negatifse Hard'ı hiç almaz, `questDailyEV` mantığı) tamamlanamayan-quest cezası riski
YOK — düşen completion% sadece "o gün Hard'ı seçmezler" anlamına geliyor, cezalandırma değil.

## Ekonomiye etki (nokta 4)

Naif "her zaman kabul edilir" varsayımıyla (worst-case, gerçekte rasyonel ret devrede) günlük Hard
EV ortalaması: renksiz 31.7→22.4 TL (Δ-9.4), renkli 22.4→20.1 TL (Δ-2.2) — **düşüş yönünde, artış
değil**. Quest'in toplam ekonomideki payı zaten küçük (mevcut bant %0.6-4.8, [[quest_tier_redesign_2026-07-25]]
%0.3-3.9 ölçümüyle uyumlu) olduğundan bu küçülme bandın ÜST ucunu aşağı çekebilir ama YENİ bir
enflasyon riski yaratmıyor — tam tersi, muhafazakarlaştırıyor. `sim.js` henüz 30-quest sabit-model
için güncellenmedi (bkz [[quest_fixed_reward_table_2026-07-28]] madde 4); bu turun hesapları sim.js
DIŞINDA ayrı bir node script ile yapıldı (box-capacity modeli), ileride sim.js'e entegre edilebilir.

## Denge riskleri / notlar

1. **box-capacity modeli "tüm gün sadece kutu üretimine ayrılmış" varsayıyor** (optimistic üst
   sınır, gerçek oyuncular müşteri/tır/yürüme işleriyle de meşgul) — gerçek tamamlanma oranları
   muhtemelen burada hesaplanandan DAHA DÜŞÜK olacak, yani tablo iyimser taraf. Playtest'te
   completion oranları modelden daha düşük çıkarsa şaşırtıcı değil.
2. **Truck hedefi kasıtlı olarak DEĞİŞMEDİ** — eğer playtest'te Truck/Shelf/Pack arasında "neden
   tır aynı kaldı" hissi oluşursa, bu bilinçli bir karar (feasibility kanıtıyla), zorla eşitlemeye
   çalışılmadı.
3. **Renk-kilitli 6 asset'in hepsi AYNI +1 mutlak artışı alıyor** (4→5) — mutlak fark küçük görünse
   de yüzde büyüme artık doğru (bkz tablo); "neden hep +1" sorusu playtest'te gelirse cevap bu
   notta.

İlişkili: [[quest_fixed_reward_table_2026-07-28]] (ödül/ceza sabit değerler, DEĞİŞMEDİ),
[[quest_tier_redesign_2026-07-25]] (orijinal targetCount türetme metodolojisi, bu turun temeli),
[[quest_completetruck_color_constraint]] (renk↔ürün eşlemesi, renk-torbası bulgusuyla ilişkili),
[[quest_answerphone_colortruck_2026-07-25]] (önceki "~1/3" varsayımı, bu turda KESİNLEŞTİ),
[[truck_hangar_window_cap]] (box-capacity modelinin "optimistic" felsefesinin kaynağı).
