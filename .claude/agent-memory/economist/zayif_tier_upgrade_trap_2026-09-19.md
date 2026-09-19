---
name: zayif-tier-upgrade-trap-2026-09-19
description: Ö1+Ö2+Ö3 sonrasi bile zayif-beceri oyuncu icin herhangi bir kart almak (acgozlu/mantikli) neredeyse kesin iflasa goturuyor; hic almamaktan cok daha kotu — Ö1-Ö10'da hic raporlanmamis
metadata:
  type: project
---

**Bulgu (2026-09-19, build-hazirlik denetimi sirasinda kesfedildi).** `tools/economy_sim/sim.py`
canli config ile kosulunca (`--runs 150/300`), `zayif` beceri profili + `acgozlu`/`mantikli`
upgrade stratejisi hucrelerinde kayip orani **%89-100** — ayni beceride `hic` (upgrade almama)
stratejisi %19.0-68.0 arasinda kaliyor. Yani zayif oyuncu icin kart almak neredeyse kesin
iflasa esdeger, hicbir sey almamaktan defalarca daha kotu.

**Kanit — `tools/economy_sim/results/matrix.csv` (baz) vs `matrix_post_uygulama.csv` (Ö1+Ö2+Ö3 sonrasi), N=150-300:**

| Hucre | Baz kayip | Post-uygulama kayip | Degisim |
|---|---|---|---|
| P1_zayif_hic | %20-22 | %19.0-19.7 | ~ayni |
| P1_zayif_acgozlu | **%100** | **%100** | degismedi |
| P1_zayif_mantikli | %89.3-93.3 | %91.7-93.3 | degismedi |
| P2_zayif_acgozlu | %99.3-100 | %99.3-100 | degismedi |
| P3_zayif_acgozlu | **%100** | **%100** | degismedi |
| P4_zayif_acgozlu | %98.0-99.3 | %99.0-99.3 | degismedi |

Olum zamanlamasi da acikliyor: `zayif_hic` cogunlukla gun 16'da (gec, marjinal) iflas ederken,
`zayif_acgozlu/mantikli` **gun 8-12'de** iflas ediyor (`bankrupt@8`/`bankrupt@12` bucket'lari
baskin) — kart alimi erken kasayi bosaltiyor, kira kapisina para kalmiyor.

**Neden Ö1-Ö10 bunu yakalamadi.** `05-oneriler.md`'nin tum ozet tablolari (REC paketi, A3/A4
karsilastirmalari) yalniz **"orta/mantikli"** ve **"iyi/mantikli"** hucrelerini raporluyor;
`zayif` beceri hic bir baslikta gorunmuyor. "Kart almak ilk kez kazandiriyor" verdikti (Ö1-3
sonrasi) yalniz orta/iyi beceri icin gecerli — zayif icin hic olculup raporlanmamis.

**Onem/ciddiyet.** Yeni/zayif oyuncu tam olarak roguelite kart sisteminin hedef kitlesidir
(yardim arayan, deneyen oyuncu); mevcut haliyle sistem onu cezalandiriyor. Bu, "statik/olaysiz
his" tasarim denetiminin (2026-09-18) bulgusuyla ayni ailede ama farkli bir sorun: oyun
kaybedilemez DEGIL, tam tersi — belirli bir profil icin nerdeyse kaybedilebilir/kesin kaybediliyor,
ve bu profil tam da korunmasi gereken profil.

**Onerilmedi, olculmedi — sonraki tur adayi (Ö11):** zayif profilin upgrade satin alma esigini
(rezerv kurali) modelde/oyunda daha muhafazakar yap, ya da ilk 1-2 kart icin "geri odemesiz
deneme" / kucuk baslangic fiyati sun. Once kok nedeni dogrula: zayif profil parametresi
(`tools/economy_sim/config.json` icindeki `zayif` beceri tanimi — hata orani/verimlilik) kartlarin
ROI'sini hesaba katmadan agresif satin aliyor mu, yoksa kartlarin kendisi mi (fiyat/etki) zayif
icin orantisiz agir? `03-simulasyon-sonuclari.md` §5'teki 19 kart amortisman tablosuna zayif
sutunu eklenerek ayristirilmali.

**How to apply:** Ö4-Ö10 veya yeni bir ekonomi turu planlanirken bu satiri once oku — "kart almak
kazandiriyor" iddiasini HER ZAMAN beceri kirilimiyla birlikte dogrula, yalniz orta/iyi'ye
bakip zayifi atlama (bu tur bu hatayi yapmisti).

Iliskili: [[economy_full_balance_round10_2026-08-30]], [[a1_determinism_audit_2026-09-18]]
