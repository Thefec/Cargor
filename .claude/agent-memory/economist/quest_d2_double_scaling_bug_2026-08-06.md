---
name: quest-d2-double-scaling-bug-2026-08-06
description: FAZ4 D2 (targetCount x ECONOMY_SCALE) 07-29 turunun zaten P-kalibreli flat hedefleriyle CIFTE OLCEKLENIYOR - Hard/Medium/Easy Shelf-Pack tamamlanma orani 2P-4P'de coker
metadata:
  type: project
---

**Bağlam**: Kontrol kapısı D2'yi (`plans/economy-rebuild-2026-07-30-faz4-final.md` §B.9,
`etkinHedef = target × ECONOMY_SCALE[P]`) [[quest_hard_targetcount_retune_2026-07-29]]'daki
flat `targetCount` (Hard 12/5, Medium 7/3, Easy 4/2) üzerine uygularsa çifte ölçekleme
şüphesi taşıdı. Node ile `sim.js` v3.1'in kendi `truckThroughput().productionCapPerDay` +
`questCompletionProb()` fonksiyonları kullanılarak ölçüldü (dosya değiştirilmedi, ayrı script).

## Kök neden

07-29 turundaki `targetCount` zaten P-flat/tek-değer seçilmişti çünkü üretim kapasitesi
(`productionCapPerDay`) P ile doğal olarak ~1:1.93:2.74:3.45 oranında büyüyor — bu büyüme
TEK bir sabit hedefi 3P/4P'de %85 bandına oturtmaya yetiyordu (P=1 tabanına göre "büyüt"
DEĞİL, tüm bantlarda çalışan flat değer). D2 bu flat hedefi TEKRAR `ECONOMY_SCALE
{1,2.00,2.95,3.70}` ile çarpınca `hedef(P)≈hedef(1)×scale(P)` oluyor, kapasite de zaten
`≈kapasite(1)×scale(P)` — oran neredeyse SABİTLENİYOR (~1P seviyesine geri düşüyor), yani
çok oyunculu takımlar üretim kapasitesinden hiçbir avantaj görmüyor.

Plan metni "%5-7 zorlaşır (kabul)" diye tahmin etmişti — gerçek ölçüm %60-85 COLLAPSE.

## Ölçüm (renksiz Shelf/Pack tamamlanma olasılığı, D2 öncesi -> sonrası)

| P | Easy(g4) | Medium(g6) | Hard(g12) |
|---|---|---|---|
| 1 | 0.58→0.58 | 0.26→0.26 | 0.15→0.15 |
| 2 | 0.87→0.56 | 0.72→0.24 | 0.48→0.14 |
| 3 | 0.87→0.51 | 0.87→0.22 | 0.76→0.13 |
| 4 | 0.87→0.52 | 0.87→0.23 | 0.87→0.13 |

Renk-kilitli quest'lerde aynı desen, daha kötü (Hard 4P: 0.77→0.08).

## Karar: D2'nin hariç tutma listesi Shelf/Pack'i de kapsayacak şekilde GENİŞLETİLDİ

Plan zaten `CompleteTruck`/`AnswerPhone`'u (kargo zaten P-bazlı ölçekleniyor / telefon
P-flat arz) hariç tutmuştu ama aynı gerekçe `PlaceBoxOnShelf`(type1)/`PackToy`(type3) için
uygulanmamıştı — oysa bunların arzı (`productionCapPerDay`) TAM AYNI ŞEKİLDE P ile
ölçekleniyor. **Karar: `QuestManager.cs CalculateEffectiveTargetCount` type1+type3'ü de
hariç tutsun** — mevcut 30 asset katalogda D2 pratikte NO-OP olmalı, Hard/Medium/Easy
`targetCount` [[quest_hard_targetcount_retune_2026-07-29]] + [[quest_tier_redesign_2026-07-25]]
değerlerinde (12/5, 7/3, 4/2) HİÇ değişmeden kalsın. (d) seçeneği (07-29'u geri alıp D2 tam
vektörle uygulamak) teorik eşdeğer ama capacity büyümesi (1.93/2.74/3.45) ECONOMY_SCALE
(2.00/2.95/3.70)'den ~%3-7 sapıyor — flat değerler zaten doğru sonucu verdiği için gereksiz
dolaylı yol.

İlişkili: [[quest_hard_targetcount_retune_2026-07-29]] (D2'nin üstüne bindiği taban),
[[quest_tier_redesign_2026-07-25]] (box-capacity modeli kaynağı), [[truck_hangar_window_cap]]
(aynı "optimistic üst sınır" felsefesi).
