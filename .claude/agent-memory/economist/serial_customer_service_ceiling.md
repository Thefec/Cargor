---
name: serial-customer-service-ceiling
description: Musteri servisi SERI (yalniz IsFirstInQueue) + maxQueueSize=2 -> ~11.4 musteri/gun P-BAGIMSIZ tavan; talep P ile 1.9x buyudugu icin 3P/4P 2P'den DAHA AZ prestij kazaniyor (ters olcekleme)
metadata:
  type: project
---

**2026-07-30 kod-dogrulandi (FAZ1 sifirdan tur).** Prestij tarafinda daha once modellenmemis
iki sert kisit var:

1. **Servis SERI.** `CustomerAI.ProcessWaitingInQueue` (`CustomerAI.cs:580-586`) yalniz
   `manager.IsFirstInQueue(this)` iken `BeginService()` cagiriyor. Ayni anda TEK musteri servis
   edilebilir -- oyuncu sayisi bunu DEGISTIRMEZ.
2. **Kuyruk dolu -> spawn ATLANIR, CEZASIZ.** `CustomerManager.cs:516` (`if (IsQueueFull) return false`).
   Spawn olmayan musteri hic gelmez, `customerLostPrestigePenalty` uretmez.
   `maxQueueSize` sahnede **2** (`unity:68600`; .cs default 3 = DEFAULT_QUEUE_SIZE).

=> **Talep bir TAVAN degil, bir HAVUZ. Prestij gelirini SERI SERVIS KAPASITESI belirler.**

## Ters olcekleme (gun 8, Normal)
| P | talep | seri tavan | servis edilen | kacan | net prestij/gun |
|---|---|---|---|---|---|
| 1 | 9 | 11.4 | STR 5.5 / OPT 9.0 | STR 3.5 / OPT 0 | **STR -1.04** / OPT +1.80 |
| 2 | 11 | 11.4 | 10.9 / 11.0 | 0.1 / 0 | +2.13 / +2.20 |
| 3 | 14 | 11.4 | 11.4 | 2.0 | +1.07 |
| 4 | 16 | 11.4 | 11.4 | 2.0 | +1.07 |

Seri tavan ~11.4 musteri/gun ve P'den BAGIMSIZ (mekanik). Talep ise `playerCountMultiplier` ile
1.9x'a cikiyor. Sonuc: **3P/4P her gun ~2 musteri kaybediyor ve 2P'den DAHA AZ prestij kazaniyor.**
16-gun sonu prestij: 1P 47.1 / 2P 47.7 / 3P 42.5 / 4P 37.1 (dusuyor!).

`customerLostPrestigePenalty = -0.6` vs `customerServedPrestigeBonus = +0.2` = **3:1 asimetri**.
Yavas+STRICT senaryosunda 1P-4P'nin 4'u de kaybediyor ve sebep para degil PRESTIJ KANAMASI.

## Yan bulgu: prestij tavani (100) ULASILMIYOR
Hicbir senaryoda tavana carpilmiyor; en yuksek son deger 47.7. `maxPrestige=100` artik olu bir
guvenlik payi. Asil sorun tavan degil BIRIKIM HIZI. ([[prestige_100_rescale_2026-07-20]] rescale'i
dogru yondeydi ama tavanin kendisi artik baglayici degil.)

**Why:** v2 sim tum `demandAdjusted` icin prestij veriyordu (yalniz %3-8 flat kayip) -> prestij
gelirini ciddi FAZLA tahmin ediyordu. [[money_comes_only_from_trucks]] icindeki "modelleme
boslugu / playtest-bagimli" notu bu turda KOD ILE cozuldu: seri servis gercek, tavan gercek.

## ✅ COZUM BULUNDU (2026-07-30, FAZ2) -- 2 PARALEL ISTASYON
Varyant taramasi (gun 8, prestij/gun): mevcut 1.80/2.20/**1.07**/**1.07** ->
kuyruk 3 tek basina 1.80/2.20/**0.69**/**0.47** (**DAHA KOTU!**) ->
**2 paralel istasyon 1.80/2.20/2.80/3.20 (MONOTON ARTAN)**. 4 istasyon 2 ile ayni (talep doymus).

⚠️ **`maxQueueSize` BUYUTME.** Kuyruk buyutmek TEK BASINA prestiji DUSURUR: daha fazla musteri
spawn olur, servis kapasitesi artmadigi icin fazlasi kacar ve -0.6 yer (4P son prestij 37.1->29.8,
kumulatif gelir 20 413->18 619). `maxQueueSize=2` KALSIN.

🔑 **Iskelet ZATEN YARIM VAR**: `CustomerManager.cs:124` `public DisplayTable[] serviceTables;`
(sahnede 1 eleman, `The Main Office.unity:68615-68616`) + `cs:873 AssignDropOffTable(CustomerAI,
DisplayTable)` — **ikisinin de SIFIR cagirani var** (`grep serviceTables` = yalniz tanim satiri).
Yani bu yeni sistem degil, **yarim birakilmis kablolamanin tamamlanmasi**: serviceTables'a 2. masa,
`IsFirstInQueue` (`cs:865-868`) yerine "bos istasyon var mi", `AssignDropOffTable` fiilen cagrilsin.

Asimetri karari: `served 0.2->0.4` + `lost -0.6->-0.5` = **1.25:1** (3:1 degil, 1:1 de degil —
kacan musteri bir HATA, cezasi hafifce agir kalmali).

Sonuc (paket sonrasi prestij/gun ort): **1P 4.28 / 2P 5.25 / 3P 6.62 / 4P 7.59** — monoton artan.
Detay: [[faz2_prestige_rent_event_2026-07-30]].

**How to apply:** Prestij kaldıraci ararken "musteri sayisini artir" veya "kuyrugu buyut" ONERME --
seri servis tavani ilkini yutar, ikincisi geri teper. Tek dogru kaldirac PARALEL SERVIS.

Iliskili: [[economy_rebuild_faz1_2026-07-30]], [[prestige_100_rescale_2026-07-20]],
[[money_comes_only_from_trucks]], [[dead_wiring_p_scaling]] (musteri sabri P-bazli DEGIL)
