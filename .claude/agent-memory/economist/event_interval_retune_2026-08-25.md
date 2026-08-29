---
name: event-interval-retune-2026-08-25
description: EVENT_INTERVAL_MIN/MAX 1/3 -> 1/2 onerisi; kritik bulgu -- sim.js event siklarini HIC modellemiyor (SRC.event* alanlari dekoratif)
metadata:
  type: project
---

## Bulgu 1 (kritik, kod-kanitli): sim.js event sikligini PARA AKISINA hic baglamiyor

`tools/economy-sim/sim.js` SRC objesinde `eventFreeDays`/`eventIntervalMin`/`eventIntervalMax`/`eventSkipRentDays`/`eventPoolSize`
alanlari VAR ve `EventCalendarUI.cs`'teki gercek degerlerle senkron (1/3/3/true/16), AMA ana gunluk simulasyon dongusunde
(satir ~619-730, `runScenario` icindeki `for (day=1..maxDays)` bloğu) hicbir yerde okunmuyor. `customerDemand()` ve
`phoneIncome()` fonksiyonlarinin `eventCustomerMult`/`eventSupportActive` parametreleri VAR ama tum cagri siteleri
(satir 630, 632, 838, 855, 860) bunlari varsayilan (1 / false) birakiyor. **Ampirik dogrulama**: `eventIntervalMax`'i
3'ten 2'ye gecici degistirip `node sim.js` calistirdim, `diff` **0 satir** -- cikti harfiyen ayni. Yani bu alanlar
saf DOKUMANTASYON/senkron-takip amacli, ekonomik etkileri YOK.

**Sonuc**: Event sikligi degisikliginin sim.js finalCash/bankruptDay tablolarina hicbir etkisi YOKTUR ve OLAMAZ
(kod degismeden). Bu nedenle "SLOW-STRICT bandini kotulestirmeden artir" kosulu sim acisindan TRIVIAL saglaniyor
(hic degismiyor) -- ama bu sim'in event etkisini modellemedigi icin, gercek oyunda hicbir garanti VERMIYOR.
Gercek oyunda event etkileri `EventEffectManager.cs`'te CANLI (rewardPerBoxMultiplier, dailyCustomerMultiplier,
customerWaitTimeMultiplier vb., tek-gunluk multiplier'lar, ~%8-35 araliginda). Daha sik event = bu multiplier'larin
gunluk gelire varyansini artirir; pozitif/negatif event sayisi esit (8/8) oldugu icin ORTALAMA etki ~notr olmali
ama VARYANS artar (RNG-bagimli iyi/kotu run farki buyur). Bu, sim disi bir risk -- playtest'te izlenmeli, sim ile
olculemez.

## Bulgu 2: Monte Carlo ile event yogunlugu (Python, C#'daki tam algoritma birebir)

16 gunluk takvimde: ilk 3 gun event-free, kira gunleri (4/8/12/16) event ALAMAZ (soft-skip, interval sifirlanmaz),
kalan 9 "musait" gun (5,6,7,9,10,11,13,14,15) var. `current_day += rng.randint(min,max)` mantigiyla 200k
trial Monte Carlo:

| MIN/MAX | Ort. event/16gun | Dagilim (n_event: %) |
|---|---|---|
| 1/3 (mevcut) | 4.50 | 3:%18, 4:%35, 5:%30, 6:%14, 7:%3 |
| **1/2 (onerilen)** | **6.07** | 3:%1, 4:%6, 5:%20, **6:%40**, 7:%26, 8:%7 |
| 1/1 (asiri, ONERILMEZ) | 9.00 (sabit) | 9:%100 -- musait 9 gunun HEPSI event olur, "surpriz" hissi kaybolur |

## Karar / Oneri
`EVENT_INTERVAL_MIN`=1 (degismedi), `EVENT_INTERVAL_MAX`=3 -> **2**. Ortalama event sayisi 4.5 -> 6.07 (+35%),
9 musait gunun ~%67'sinde event (once ~%50). Kullanicinin "biraz daha sik" talebini karsilar, MIN/MAX=1/1 gibi
her gun event olan asiri uca gitmez (o zaman "event" kavraminin ayirt ediciligi kaybolur, her gun ayni takvim
deseni olusur -- 200 satir MC ile 1/1 = %100 sabit 9 event, hicbir varyasyon yok).

## How to apply
- Kod degisikligi TEK satir: `Assets/NewCss/Events/EventCalendarUI.cs:24` `EVENT_INTERVAL_MAX = 3` -> `2`.
- `tools/economy-sim/sim.js:216` sync-yorumu da guncellenmeli (`eventIntervalMax: 2`) AMA bu SADECE dokumantasyon
  senkronu icin -- hicbir hesaplamayi degistirmez (bkz Bulgu 1).
- SLOW-STRICT bandi (bkz [[rent_growth_1_35_deficit_2026-08-20]]) bu degisiklikten ETKILENMEZ (sim event
  modellemedigi icin garanti trivial) -- ama bu ayni zamanda sim'in bu riski hic GOREMEDIGI anlamina gelir.
  Playtest'te ozellikle negatif-event-yogun bir 16-gunluk run'da (kotu sanslarda uzt uste 2-3 negatif event)
  4P/STRICT gibi zaten dar-marjli banda ekstra darbe gelip gelmedigi izlenmeli.
- Eger ileride event etkisini sim'e gercekten baglamak istenirse: gunluk ortalama ~%(pozitif ortalama - negatif
  ortalama)/2 ~ notr etkiyi customerDemand/truckRevenue'ya carpan olarak eklemek gerekir -- bu ayri bir
  model-genisletme isi, bu turda YAPILMADI (kapsam disi, kullanici yalniz frekans sordu).

Ilgili: [[rent_growth_1_35_deficit_2026-08-20]], [[sim_resync_2026-08-19]], [[missing_events_g9]]
