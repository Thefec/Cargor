---
name: draft-offer-day-seeded-2026-09-25
description: Kart teklifi YALNIZ gün ile tohumlanıyor (canlı UpgradePanel + sim.py) → her koşuda aynı gün aynı 3 kart; mc40k'daki acgozlu/mantikli Acil Fren farkının büyük kısmı bu artefakt
metadata:
  type: project
---

Canlı `UpgradePanel.cs:1343` teklif RNG'si `new System.Random(currentDay * 73856093)`, reroll `:1533` `(currentDay*73856093) ^ ((reroll+1)*19349663)`.
Maç/koşu seed'i YOK → uygunluk dizisi aynıysa her koşuda gün N'de aynı 3 kart gelir. sim.py `shop()` aynısını yapıyor (`random.Random(day * 73856093)`).

Sonuçlar:
- "Statik his" için ayrı bir kaynak: kart draft'ı koşudan koşuya değişmiyor (en azından gün 1'de birebir aynı).
- Ölçüm artefaktı: 2026-09-24 mc40k'da zayıf P3/P4 acgozlu %97 vs mantikli %35-44 farkı (Acil Fren belirleyiciliği) büyük ölçüde sabit teklif sırasından. Koşu-bazlı rastgele teklifle (event_kart harness `offerRand`) P3 zayıf: acgozlu kayıp %9, mantikli %29 (fark ~20 puan, 54 değil).
- Kart ekleme/çıkarma (disabledInDraft değişimi) uygunluk dizisini değiştirir → TÜM günlerin teklifi kayar; sabit tohumla "hangi kart alındı" ölçümleri kartın gücünü değil teklif takvimini ölçer.

**Why:** Kart dengesi ve "Acil Fren bağımlılığı" kararları bu ölçümlere dayanıyor; sabit teklif yanlış sonuç veriyordu.
**How to apply:** Kart/strateji ölçümlerinde `offerRand` (koşu-tohumlu teklif) kullan, sabit-teklif sonucunu yalnız "canlı bugün" referansı olarak ver. Canlıya öneri: teklif tohumuna koşu seed'ini kat (takvim seed'i zaten var). Bkz. [[mc40k-2026-09-24-findings]], [[event-kart-tasarim-2026-09-25]].
