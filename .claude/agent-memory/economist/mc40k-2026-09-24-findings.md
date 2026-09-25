---
name: mc40k-2026-09-24-findings
description: 2026-09-24 5M koşuluk kaybedilebilirlik/erken-belirlenme ölçümünün ana bulguları (zayıf profil uçurumu, Acil Fren belirleyiciliği, her-gün-event nötr)
metadata:
  type: project
---

Ölçüm: docs/economy/mc-40k-2026-09-24.md, harness tools/economy_sim/mc40k_2026_09_24.py (main 96cad54 sonrası).

- Orta/iyi profil fiilen kaybedemiyor (52/960k, 0/960k). Kayıp yalnız zayıf profilde: %1-68.
- Zayıf profil uçurum kenarında: süreler ±%20 → P3 mantikli kayıp %10 / %56 / %96. İflas yüzdesi profil varsayımına ~±40 puan duyarlı; profil playtest'le kalibre edilmeden kesin sayı verme.
- Acil Fren (kilit-muaf, kirayı tamamen affeder) zayıf takım hayatta kalmasını ~60 puan belirliyor (acgozlu %97 vs mantikli %35-44, P3/P4). ⚠️2026-09-25 DÜZELTME: bu farkın çoğu sabit (gün-tohumlu) teklif artefaktı; koşu-tohumlu teklifle fark P3 18 / P4 25 puan (bkz. [[draft-offer-day-seeded-2026-09-25]]).
- İflas: gün 4 = 0 (grace), gün 12 %66, gün 16 %24, gün 8 %11.
- Profil içinde gün 3-5 kasası kaybı tahmin etmiyor (AUC≈0,59). Gün 8'de kasa ≥ sıradaki kira → kayıp ≤%0,5.
- Kira dışı her gün event (what-if) iflası ±3 puan değiştiriyor: ekonomik açıdan nötr. "Statik his"i ancak sert event'ler çözer.

**Why:** Tasarım denetimindeki "kaybedilemezlik" kararı ve her-gün-event planı bu sayılara dayanacak. Jules bağımsız ölçüm yapıyor, karşılaştırılacak.
**How to apply:** Kaybedilebilirlik/zorluk sorularında önce bu ölçüme bak. Kod değiştiyse yeniden koş (base 40k×72 ≈ 100 dk, 11 süreç). Bkz. [[sim-sync-gotchas]].
