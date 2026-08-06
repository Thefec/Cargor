---
name: economy-rebuild-faz1-2026-07-30
description: FAZ1 sifirdan temel -- sim.js v3, gun suresi 200s (160 degil), gunde 1.1-7.5 tir (mekanik tavan 10.9-18 BAGLAYICI DEGIL), 1P/2P/3P/4P gelir tabani, kira olcegi DOGRU
metadata:
  type: project
---

**Tarih: 2026-07-30. Rapor: `plans/economy-rebuild-2026-07-30.md`. Sim: `tools/economy-sim/sim.js` (v3, sifirdan).**

Ekonominin SIFIRDAN yeniden hesaplandigi turun TEMEL fazi. Tum degerler canli
`.cs`/`.asset`/`.prefab`/`.unity`'den yeniden okundu; eski raporlar veri olarak kullanilmadi.

## Kesinlesen sayilar (Normal senaryo, 1 hangar, quest+telefon acik, yeniden yatirim KAPALI)

**Gun suresi: 200s (gun 1-3), +10s/gun gun 4'ten sonra -> gun 16 = 330s. 16 gun toplam ~68.5 DAKIKA.**
Eski sim 160s kullaniyordu (.cs default) -> tum eski verim hesaplari %25 dusuk.

| P | bant | ort gunluk net | kumulatif 16 gun | 16-gun kira | gelir/kira |
|---|---|---|---|---|---|
| 1 | OPTIMISTIC | 405 TL | 6 488 | 2 496 | 2.60 |
| 2 | OPTIMISTIC | 734 TL | 11 736 | 4 494 | 2.61 |
| 3 | OPTIMISTIC | 990 TL | 15 841 | 5 992 | 2.64 |
| 4 | OPTIMISTIC | 1 276 TL | 20 413 | 7 490 | 2.73 |
| 1 | STRICT | 147 TL | 588 (IFLAS gun 4) | 2 496 | 0.24 |
| 2 | STRICT | 433 TL | 6 933 | 4 494 | 1.54 |
| 3 | STRICT | 556 TL | 8 889 | 5 992 | 1.48 |
| 4 | STRICT | 654 TL | 10 467 | 7 490 | 1.40 |

## "Gunde kac tir?" -- PROJENIN EN UZUN ACIK SORUSU CEVAPLANDI

**Mekanik tavan (1 hangar) = tirPenceresi/15s = 10.9 (gun1) / 13.6 (gun8) / 18.0 (gun16) tir/gun.
Fiilen ulasilan = 1.1-7.5 tir/gun = tavanin %10-42'si. YANI TIR PENCERESI TAVANI BAGLAYICI DEGIL.**
Gercek darbogaz: insan uretim hizi (OPTIMISTIC) veya tek tirin dolma suresi (STRICT).

**Dogal sonuc: hangar sayisi neredeyse degersiz.** Gun 8 kutu/gun -- OPTIMISTIC'te 1h=2h=3h
(uretim tavanina carpiyor, 2. ve 3. hangar SIFIR katki); STRICT'te 2. hangar +%17-52, 3. hangar SIFIR.
=> Truck upgrade 2 seviyede bitmeli veya 3. seviyeye baska etki baglanmali.

## Kira olcegi DOGRU -- dokunma
```
OPTIMISTIC gelir olcegi (1P=1): 1.00 / 1.81 / 2.44 / 3.15
KIRA olcegi             (1P=1): 1.00 / 1.80 / 2.40 / 3.00   (maks sapma %5)
```
Asil kira sorunu egri degil EGIM: kira baskisi (kira/gunluk net) gun4 1.76 -> gun16 1.18'e
DUSUYOR, yani gelir buyumesi 1.15^cycle'in ustunde -> gec oyun gevsiyor.

## Para yalniz tirdan (yeniden kod-dogrulandi)
`grep -c "AddMoney|ModifyMoney"` CustomerAI.cs = 0, CustomerManager.cs = 0 ("money" kelimesi bile yok).
Gun 8 gelir payi: tir %84 (1P) -> %95 (4P). Telefon P-BAGIMSIZ 51 TL/gun -> 1P'de gelirin %13'u
(gizli solo yardimi), 4P'de %4.

## En duyarli varsayim
Bir gun yalniz 200-330 GERCEK saniye. `kutuDk/oyuncu` 1.2 -> 2.0 (%67 artis) 1P kumulatifini
2 994 -> 6 488 TL (%117) yapiyor. Kodda zamanli uretim kapisi YOK (Table.cs'de yalniz
ITEM_SPAWN_DELAY=0.1s) -> tamamen oyuncu becerisi. **Playtest ile gercek kutu/dk olculmeden
mutlak TL kesinlestirilmemeli; oranlar (kira/gelir, upgrade/gelir) daha guvenilir dil.**

**How to apply:** FAZ 2 (prestij/event/kira) ve FAZ 3 (upgrade/quest fiyat) bu tabloyu girdi
olarak alir. Yeni bir gelir tahmini yapmadan once sim'i kosur, tabloyu buradan alintila.
Sim'de `SRC` = koddan okunan gercek deger (her satirda dosya:satir), `ASSUMED` = insan-verimi
varsayimlari (bilinclice ayri blok).

Iliskili: [[truck_hangar_window_cap]] (bu turda sayiyla kapandi),
[[serial_customer_service_ceiling]] (prestij tarafinin yeni tavani),
[[dead_wiring_p_scaling]] (P-bazli sandigimiz ama olu olan yollar),
[[money_comes_only_from_trucks]]
