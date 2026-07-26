---
name: quest-tier-redesign-2026-07-25
description: Easy/Medium/Hard 3-tier quest reward/penalty/targetCount tablosu (easy1-5 tam yeniden tasarim, sim-EV dogrulandi) - 15 quest onerisi, limit KORUNDU, Gorev Tier reaktivasyon fiyat onerisi
metadata:
  type: project
---

**Bağlam**: 2026-07-25, `plans/quest-redesign-2026-07-25.md` (müdür yapısal çerçeve) + bu turda
economist §7'yi doldurdu. Kullanıcı bir sonraki Unity oturumunda TÜM mevcut quest'leri
(easy1-5) silip bu tabloyla değiştirecek. [[quest_reward_balance]] ve [[quest_easy4_5_duplicate]]
bu değişiklik uygulanınca TARİHSEL hale gelir (Easy tier'ın YENİ halini bu dosya temsil eder).

## Kritik ön-bulgu (kod, YENİ — önceki quest turları bunu hiç kontrol etmemişti)

**CompleteTruck (questType=2) rengi ASLA kilitlenemez.** `QuestManager.cs:348-351`
`HandleTruckCompleted()` → `UpdateQuestProgress(QuestType.CompleteTruck, BoxInfo.BoxType.Red, 1)`
— renk parametresi HARDCODED Red, gerçek tır kargosunun rengini TAŞIMIYOR (`QuestTracker.OnTruckCompleted`
parametresiz bir `Action`). `UpdateQuestProgress` (satır 488) `requireSpecificBoxType` kontrolünü
TÜM tiplere uyguluyor — yani bir CompleteTruck quest'i `requireSpecificBoxType=true` + renk≠Red
ile oluşturulursa **sonsuza kadar tamamlanamaz** (sessiz soft-lock: oyuncu kabul eder, işi yapar,
gün sonu yine de ceza yer). **KURAL: Yeni 15 quest'in TÜMÜNDE CompleteTruck asset'lerinin
`requirement.requireSpecificBoxType` alanı FALSE kalmalı.** Renk varyasyonu sadece PlaceBoxOnShelf
ve PackToy'da güvenli (plan doc §6 madde 6 ile uyumlu, ama CompleteTruck istisnasını netleştirdi).

**Sabit ürün↔kutu-rengi eşlemesi** (`Table.cs:828-832 IsValidBoxProductCombination`,
`CustomerManager.cs:1093-1095`): **Toy→Red, Clothing→Yellow, Glass→Blue**, İSTİSNASIZ (yanlış
kombinasyon paketleme eylemini reddeder, kutu kırılır). PackToy renk-kilitli quest'lerde bu yüzden
"renk" aslında "ürün kategorisi" demek — "X Kırmızı oyuncak paketle" = Toy paketle. Eski
`easy5.asset`'in "Sarı oyuncak" (yellow TOY) ifadesi bu eşlemeyle ÇELİŞİYORDU (Toy hiçbir zaman
sarı olamaz) — mekanik olarak kırık değildi (event her paketlemede rengi doğru taşıyor, ilerleme
olurdu) ama metin yanlış etikettiydi. Yeni quest'lerin açıklama metninde bu eşlemeye uyulmalı.

## Tier reward/penalty havuzu (TEK şablon/tier, tüm quest'ler o tier'da AYNI havuzu paylaşır)

Havuz şekli KORUNDU: 3 Money + 2 Prestige = 5 öge, pick-2 (`p=0.4`, [[quest_reward_balance]]
formülü). Ceza = ödülün **~%60'ı (Money) / ~%50-55'i (Prestige)** — HER İKİ kaynak türünde de
ödülden kesin küçük (eski sistemde prestij cezası ödülle TAM SİMETRİKTİ, 0.48=-0.48; bu tur
düzeltti, artık Money ile tutarlı "ceza < ödül" kuralı var, task'ın açık isteğiyle uyumlu).

| Tier | rewardPool (Money) | rewardPool (Prestige) | penaltyPool (Money) | penaltyPool (Prestige) |
|---|---|---|---|---|
| Easy | 10 / 15 / 25 | 0.4 / 0.8 | -5 / -10 / -15 | -0.2 / -0.4 |
| Medium | 20 / 30 / 40 | 0.8 / 1.4 | -14 / -18 / -22 | -0.4 / -0.8 |
| Hard | 35 / 50 / 65 | 1.6 / 2.4 | -25 / -30 / -35 | -0.8 / -1.2 |

**EV tablosu** (node, pick-2-of-5 formülü, `durationDays=0` hepsi Money/Prestige):

| Tier | rewardMoneyEV | rewardPrestigeEV | penaltyMoneyEV | penaltyPrestigeEV | ceza/ödül oranı |
|---|---|---|---|---|---|
| Easy | 20.0 | 0.48 | -12.0 | -0.24 | 60% / 50% |
| Medium | 36.0 (1.8x) | 0.88 (1.83x) | -21.6 | -0.48 | 60% / 55% |
| Hard | 60.0 (1.67x Med, 3.0x Easy) | 1.60 (1.82x Med, 3.33x Easy) | -36.0 | -0.80 | 60% / 50% |

Easy EV=20.0, eski easy1/2/3 ortalamasına (~21.9) çok yakın — tutarlı devam, sürpriz sıçrama yok.
Prestige reward EV: Easy=tier-eşiğinin(4) %12'si, Medium %22, Hard %40 — hiçbiri tek başına
prestij tier'ını atlatmıyor (ana prestij kaynağı müşteri servisi kalıyor, bkz [[money_comes_only_from_trucks]]).

**Worst-case tek-gün ceza** (2 en büyük Money öge birlikte çekilirse, EV değil gerçek çekim):
Easy -25, Medium -40, Hard -65 TL. En kırılgan senaryoda bile (1P Yavaş gün1, core=144 TL) Hard
worst-case %45'i geçmiyor — hiçbir quest tek başına bir günlük çekirdek geliri AŞMIYOR (2026-07-18
kriterine uyumlu, node ile doğrulandı).

## targetCount + quest sayısı (tier başına 5 asset, toplam 15)

Kompozisyon: 1× CompleteTruck (renksiz, ZORUNLU) + 2× PlaceBoxOnShelf (1 renksiz + 1 renk-kilitli)
+ 2× PackToy (1 renksiz + 1 renk-kilitli). Renk ötelemesi (örnek, ekonomik değil, serbest
değiştirilebilir): Easy=Mavi/Kırmızı, Medium=Sarı/Mavi, Hard=Kırmızı/Sarı.

| Tier | CompleteTruck | Shelf/Pack (renksiz) | Shelf/Pack (1 renk) |
|---|---|---|---|
| Easy | 1 | 4 | 2 |
| Medium | 2 | 7 | 3 |
| Hard | 3 | 10 | 4 |

**Türetme metodolojisi**: `tools/economy-sim/sim.js` `truckCapOptimistic`/`fullTrucksPerDayEstimate`
kutu/tır throughput'una karşı, `easy2CompletionRate`'in AYNI step-fonksiyonu (1.5x→%85, 1.0x→%65,
0.5x→%40, 0.25x→%20, altı→%8) 3 canlı tipin HEPSİNE genellendi (yeni bir model değil, mevcut
validasyonlu şeklin tekrar kullanımı). Renk-kilitli hedef ≈ renksiz hedefin **%40-50'si**
(varsayım: 3 renk ~eşit dağılıyor [Red/Blue/Yellow], **PLAYTEST-DOĞRULANMAMIŞ** — gerçek ürün-spawn
dağılımı kaynağa kadar izlenmedi, sadece Toy/Clothing/Glass eşit şans varsayıldı).

Sonuç tamamlanma-oranı gradyanı (temsili gün: Easy=gün1, Medium=gün8, Hard=gün12, Normal senaryo):
1P hiçbir tier'da SIFIRLANMIYOR (en kötü %20-40, "zor ama imkansız değil"); Easy tüm P için
rahat (%65-85); Medium 2P+ rahat (%65-85), 1P zorlu (%40); Hard 3P+ rahat (%65-85), 2P orta (%65),
1P zorlu (%40) — **Hard fiilen 2P+ içerik, 1P erişebilir ama nadiren** (bilinçli, Ucuz Kira/Prestij
Simsarı ile aynı "1P asimetrik zayıf" kategorisi, [[roguelite_perk_pricing]] emsaliyle tutarlı).

Eski easy2 (CompleteTruck target=2) 1P için ağırdı (tamamlanma ~%20, gün8 fullTrucks=0.88);
yeni Easy CompleteTruck target=1 bunu düzeltiyor (1P gün1 %40'a çıkıyor). Eski easy1/3 (target=5)
throughput'a karşı hiç doğrulanmamıştı — yeni target=4 biraz daha güvenli, ilk kez bu çapraz-kontrol
yapıldı.

## Sim-EV özeti — ekonomiye oranı (mevcut ~%1-5 baseline'ı bozmuyor mu?)

Rasyonel-en-iyi-seçim modeliyle (mevcut `questDailyEV()` mantığı, sadece 3 tier'a genellendi),
quest günlük EV'si / o günün `netEarnings` (quest hariç çekirdek gelir) oranı **tüm tier×gün×P
kombinasyonlarında %0.3 – %3.9 bandında** kaldı (node, Normal senaryo, gün1-16 aralığı tarandı).
Mevcut Easy-only baseline (~%1-5, [[quest_reward_balance]]) korunuyor VE Medium/Hard eklenmesine
rağmen ÜST SINIR aşılmıyor — **enflasyon riski YOK**. Yavaş/kötü senaryoda (stres testi) EV daha da
küçülüyor veya sıfırlanıyor (rasyonel takım düşük tamamlanma ihtimalinde kabul etmiyor).

## Limit kararı: 3 teklif + 1 kabul/gün KORUNDU (değişiklik önerilmiyor)

Kümülatif havuz modeli (`GetAvailableQuestsForTier`: tier açıldıkça ALT tier'lar havuzdan
ÇIKMIYOR, toplanıyor) nedeniyle Hard açıkken günlük 3 çekilişte havuz 15 öğeye çıkıyor (5+5+5).
Hipergeometrik hesap (node): **P(o gün ≥1 Hard teklif edilir) = %73.6**, P(≥1 Medium-veya-Hard) =
%97.8 — yeterince sık, "duvar hissi" yaratacak kadar seyrek değil. 5/5/5 kompozisyonu ayrıca
Easy-tek-başına dönemde (Medium/Hard hiç açılmadan önce, oyunun BAŞINDAN itibaren potansiyel
haftalarca) `C(5,3)=10` farklı günlük kombinasyon veriyor (3/3/3 sadece 1 sabit kombinasyon —
"her gün aynı 3", tekdüze). Alternatif kompozisyonlar (4/4/4, 5/4/3, 3/4/5) test edildi, 5/5/5
hem Easy-çeşitliliği hem Hard-erişilebilirliği en dengeli karşılıyor. **DAILY_QUEST_COUNT=3
ve "günde 1 kabul" kod sabitlerine dokunma gerekmiyor.**

## Hard tier ROI / "Görev Tier" upgrade fiyatı

**Önemli bağlam**: "Görev Tier" upgrade'i şu an draft havuzundan TAMAMEN ÇIKARILMIŞ durumda
(`UPGRADE_PRICING_REPORT.md` §6, `_questSystemActive` feature-flag false, bkz
[[roguelite_perk_pricing]]) — bu turun kapsamı dışı ama task açıkça ROI sorduğu için hesaplandı.
maxLevel=2 (Level1=Medium aç, Level2=Hard aç, `SetQuestTier(level)`).

Multivariate hipergeometrik ile (node, seyrelme dahil GERÇEKÇİ EV, 5/5/5 havuz):
- **Level1 (Medium aç) günlük EV kazancı ≈ +11.2 TL/gün** (2P/3P/4P hepsi aynı, Medium 2P+'de
  zaten tam-plato tamamlanma oranına ulaşıyor).
- **Level2 (Hard aç, Medium zaten açık) günlük EK kazanç: 3P/4P ≈ +14.1 TL/gün, 2P ≈ +0.0 TL/gün**
  (2P'de Hard'ın daha zor hedefi tamamlanma oranını bir basamak düşürüyor [%85→%65], büyüyen ödül
  havuzu bunu SADECE 3P/4P'de fazlasıyla telafi ediyor — 2P'de yaklaşık başabaş. Küçük/gürültü
  seviyesinde bir fark, ~1 TL/gün, zorla "düzeltilmedi" — [[roguelite_perk_pricing]]'teki Ucuz
  Kira/Prestij Simsarı 1P-asimetrisiyle aynı kategoride kabul edilebilir nüans, playtest izlesin).

**Sonuç: evet, ödül sıçraması bir fiyatı haklı çıkarır AMA sadece DÜŞÜK bir fiyatı** — quest
sistemi bilinçli olarak küçük tutulduğu için (günde 1 kabul limiti) EV kazançları küçük (11-14
TL/gün). Eski arşivlenmiş "EV=tier×250 TL" sezgisi (`UPGRADE_PRICING_REPORT.md` §6) bu yeni,
kasıtlı-küçük quest EV dünyasına göre ESKİ/YANLIŞ KALİBRE — 250-750 TL bandı asla payback
almaz (~gün 100+). **Öneri (reaktivasyon olursa): Level1 ≈ 80 TL, Level2 ≈ 100 TL** (toplam 180 TL,
T1-bandı, mevcut T2/T3 backbone fiyatlarının [250-800] ÇOK altında) — 3P/4P için ~7-8 gün payback,
2P için Level1 makul (~7 gün) ama Level2 marjinal (gerçek ekonomik değeri yok, satın alırsa "içerik
çeşitliliği" için, ROI için değil). 1P her iki seviye için de zayıf/negatif değer (Hard'a pratikte
erişemiyor) — bu upgrade fiyatlandırma kararı ayrı bir upgrade-pricing turunun kapsamı, burada
sadece EV kanıtı bırakılıyor.

## Denge riskleri özeti

1. **CompleteTruck renk kilidi = kalıcı soft-lock riski** (yukarıda, KRİTİK — asset oluştururken
   dikkat edilmeli, kod değişikliği YOK bu turda).
2. **Renk-kilitli hedef sayıları playtest-doğrulanmamış varsayıma dayanıyor** (~1/3 renk dağılımı).
   Gerçek dağılım farklıysa (örn. Toy/Red daha sık spawn oluyorsa) renk-kilitli quest'ler
   modellenen değerden daha kolay/zor olabilir.
3. **1P Hard tier'a fiilen erişemiyor** (kasıtlı, mevcut asimetri paternine uyumlu, ama gameplay/UI
   bu beklentiyi oyuncuya iletmeli — "Görev Tier" satın almadan önce 1P'ye bunun sınırlı fayda
   getireceği hissettirilmeli, [[roguelite_perk_pricing]] madde 3 ile aynı UX notu).
4. **Level2 (Hard) 2P için ROI'si ~sıfır** (yukarıda) — playtest'te 2P takımların Hard'ı gerçekten
   "değerli" bulup bulmadığı izlenmeli.
5. **Enflasyon riski YOK** (quest EV / core income oranı tüm senaryolarda %4'ün altında kaldı).
6. **Duvar hissi riski düşük** — tier-arası büyüme oranı 1.67-1.83x bandında (ne çok küçük/hissiz
   ne patlayıcı), tamamlanma-oranı gradyanı kademeli (adım fonksiyonu, uçurum yok).

İlişkili: [[quest_reward_balance]] (eski Easy-only baseline, artık tarihsel), [[quest_easy4_5_duplicate]]
(artık tarihsel), [[prestige_100_rescale_2026-07-20]] (prestij ölçeği bağlamı), [[money_comes_only_from_trucks]]
(prestij vs para kaynağı ayrımı), [[roguelite_perk_pricing]] (Görev Tier'ın draft'tan çıkarılma kararı
+ 1P-asimetri emsali), [[truck_hangar_window_cap]] ve [[hangar_stay_duration_per_player]] (throughput
kaynağı), [[q3_tempmoneyperbox_dead]] / [[q8_buff_stacking_policy]] (bu turda kapsam dışı bırakılan
buff-tipi ödüller, hâlâ geçerli).
