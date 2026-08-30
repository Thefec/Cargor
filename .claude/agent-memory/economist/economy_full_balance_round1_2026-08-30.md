---
name: economy-full-balance-round1-2026-08-30
description: Round 1 - sim.js v4.0 runFullSim kanonik birlesik model; eski runSim ARTIK CANLI KODU YANSITMIYOR (4 kirik varsayim); telefon V4 beceri-ters trap; Slow-strict tum P'ler gun 12'de iflas. NOT: §3 (phone perk 0f) HATALIYDI, Round 2'de duzeltildi
metadata:
  type: project
---

# Round 1 — Birleşik simülasyon modeli (2026-08-30)

Takip dosyası: `plans/economy-full-balance-2026-08-30.md`.
Yeni fonksiyon: `runFullSim(playerCount, opts)` — `tools/economy-sim/sim.js`, `SRC4`/`ASSUMED4`
blokları. CLI: `node tools/economy-sim/sim.js` → blok **18-23**. Eski `runSim`/`runSimPlateUp`
SİLİNMEDİ (blok 0-17 aynen çalışıyor).

## 1. ESKİ `runSim` ARTIK CANLI KODU YANSITMIYOR — Round 2+ KULLANMASIN

**Kural: Round 2'den itibaren TEK geçerli fonksiyon `runFullSim`.** Dört kırık varsayım:

1. **Kota modeli ölü.** `runSim.customerDemand()` talebi hâlâ kapasite formülünden
   (raf×2 + storeLevel×2 + varyans) türetiyor. `CustomerManager.CalculateTodaysCustomerCount`
   (cs:403-419) bunu 2026-08-29'da SİLDİ; artık yalnız `GetDailyCustomerCount(day, P)` okuyor.
2. **Telefon modeli ölü.** `phoneIncome()` `phoneRingChancePerHour`/`phoneRingEventMultiplier`/
   `phoneRingPerkBonus`'a dayanıyor. Bu üç alan `GameEconomySettings.cs`'te ARTIK YOK
   (asset'te ölü anahtar olarak duruyorlar). Telefon V4 "dışarı ARAMA", pasif çalma değil.
3. **Gün uzunluğu sabit varsayılıyor.** Canlı kod kota tükenince günü erken bitiriyor
   (`CustomerManager.CheckEarlyDayCompletion` cs:896-910 → `DayCycleManager.FastForwardToEndOfDay`).
4. **"1 müşteri = 1 ürün" varsayımı yanlış.** Gün 5+ müşterilerin %25'i İADE modunda
   (SIFIR ürün); gün 9+ tedarik müşterisi İKİ ürün bırakıyor
   (`PostRentFeatureUnlocks.cs:16,22,25` + `CustomerAI.PlaceProductCoroutine:1339`).
   Ayrıca kargo aralığı P-bazlı ({1,2}/{2,3}/{2,3,4}/{2,3,4,5}, `TruckSpawner.cs:613,624`),
   `runSim` hâlâ sabit {2,3,4,5} kullanıyor.

Sapma büyüklüğü (Normal/strict finalCash): P1 −76%, P2 −57%, P3 −57%, P4 −5%.
Slow/strict'te iflas GÜNÜ bile ayrışıyor (runSim P3/P4 gün 8 der, v4 gün 12 der).

## 2. Unity serileştirme tuzağı — asset'te OLMAYAN anahtarlar

`Assets/Resources/EkonomiAyarlari.asset` şu anahtarları **hiç içermiyor** → C# field-initializer
değeri CANLIDIR: `dailyCustomerCountP1..P4`, `customerArrivalIntervalByPlayerCount`,
`rewardPerBoxByPlayerCount`, `timeSkipAmountByPlayerCount`, `phoneCooldownSeconds`,
`phoneCooldownPerkBonusSeconds`, `phoneDialHoldSeconds`, `customerMissedQuotaPrestigePenalty`.
(Doğru yöntem — bkz. hafıza notu "Unity YAML float[] tuzağı".) Tersi de var: asset'te duran
`phoneRingChancePerHour: 0.2`/`phoneRingEventMultiplier: 2`/`phoneRingPerkBonus: 0` C# sınıfında
YOK → hiçbir şey yapmıyor.

## 3. ~~`phoneCooldownPerkBonusSeconds` 0f, 10f DEĞİL~~ — BU BULGU HATALIYDI (düzeltildi 2026-08-30, Round 2)

Doğrusu: `GameEconomySettings.cs:123` **satın-alma-öncesi statik varsayılanı** 0f (her perk
için normal, trivial). `PerkEffect.cs:68` (`case "phone_line"`) + `:301`
(`ctx.Economy.phoneCooldownPerkBonusSeconds = 10f;`) perk alınınca mutlak atamayı yapıyor —
perk TAM BAĞLI ve ÇALIŞIYOR. Gerçek sorun: `Mathf.Max(1f, 3 - 10) = 1f`, yani yeni taban
(3sn) altına **çakılma**. Round 7 orijinal çerçevesiyle ("taban altı, yeni değer öner")
devam etmeli. Bkz. [[phone_cooldown_perk_event_stacking_2026-08-29]],
[[economy_full_balance_round2_2026-08-30]].

## 4. 16 senaryo — `runFullSim` taban koşusu (perk YOK, upgrade YOK)

| senaryo | bant | P | iflas | kazandı | sonKasa | sonPrestij |
|---|---|---|---|---|---|---|
| Normal | strict | 1 | yok | EVET | 146 | 60.6 |
| Normal | strict | 2 | yok | EVET | 781 | 100 |
| Normal | strict | 3 | yok | EVET | 692 | 100 |
| Normal | strict | 4 | yok | EVET | 1068 | 100 |
| Normal | optimistic | 1 | yok | EVET | 3297 | 48.9 |
| Normal | optimistic | 2 | yok | EVET | 8379 | 81.1 |
| Normal | optimistic | 3 | yok | EVET | 9385 | 83.7 |
| Normal | optimistic | 4 | yok | EVET | 10718 | 83.7 |
| Slow | strict | 1 | **GÜN 16** | hayır | 567 | 39.6 |
| Slow | strict | 2 | **GÜN 12** | hayır | 1137 | 54.6 |
| Slow | strict | 3 | **GÜN 12** | hayır | 1569 | 70.1 |
| Slow | strict | 4 | **GÜN 12** | hayır | 1997 | 70.6 |
| Slow | optimistic | 1 | yok | EVET | 1052 | 47.4 |
| Slow | optimistic | 2 | yok | EVET | 2879 | 63.3 |
| Slow | optimistic | 3 | yok | EVET | 3242 | 60.7 |
| Slow | optimistic | 4 | yok | EVET | 3690 | 63.3→60.7 |

Slow+strict kırıklığı DEVAM EDİYOR ama **profili değişti**: eskiden P3/P4 gün 8'de ölüyordu,
şimdi tüm P'ler 12'ye kadar dayanıp 3. kira duvarında (×1.44) topluca ölüyor. Normal/strict
P1 marjı çok ince (146 TL kasa) — Round 2/3'ün ana adayı.

## 5. YENİ BULGU — Telefon V4 "beceri-ters trap"

`PhoneCallManager.ExecuteCall` (cs:432-465) sırayla: müşteri spawn → `SkipTime(timeSkipMinutes)`
→ +20 TL → +0.4 prestij. `timeSkipAmountByPlayerCount={115,59,55,55}` oyun-DAKİKASI, tasarım
gereği doğal varış aralığının (44/22/21/21 GERÇEK sn) oyun-zamanı karşılığına eşitlenmişti.

Sonuç: her çağrı ~1 doğal aralık kadar **GERÇEK saniyeyi yok ediyor** (oyun saati anında
ilerliyor), yani tırın üretim/yükleme penceresi kısalıyor. 20 TL bunun karşılığı DEĞİL.

Telefon açık/kapalı net etki (blok 23):
- **STRICT**: Normal P1 −81%, P2 −58%, P3 −79%, P4 −79% (768→146, 1844→781, 3308→692, 5087→1068)
- **OPTIMISTIC**: +3% … +21% (hafif POZİTİF — orada üretim penceresi bağlayıcı değil)

Yani mekanik, zaten en çok zorlanan oyuncuyu cezalandırıyor; iyi oyuncuya ödül veriyor.
%100 kullanımda P1/P3/P4 iflas ediyor. **Round 7 için birincil konu bu, cooldown değil.**
(Not: `phoneCooldownSeconds` 20→3 düşüşü bu etkiyi ŞİDDETLENDİRİR — cooldown artık
spam'ı sınırlamıyor, sınırlayan tek şey kota ve 17:30 guard'ı.)

## 6. YENİ BULGU — "kota = gelir tavanı" premisi STRICT bantta YANLIŞ

`runSimPlateUp`'ın kurucu varsayımı (2026-08-29): kota, günlük kutu arzının gerçek tavanı.
v4 ölçümü: STRICT bantta **mekanik işleme tavanı çok daha düşük** — 4P/Slow/strict'te
ürün arzı 7-12 kutu/gün iken mekanik tavan 2.3-3.6 kutu/gün. Yani ürünler masada birikiyor,
kota bağlayıcı DEĞİL. `rewardPerBoxByPlayerCount={50,55,70,88}` kota-tabanlı türetildiği için
STRICT bantta ihtiyacın altında kalıyor. Round 2/3 bunu kira eğrisiyle birlikte ele almalı.

**Model açığı (Round 1'de modellenmedi, not düşülüyor):** ürünler alınmazsa DisplayTable
slotları dolar → `PlaceProductOnDropOffTable` null döner → `HandleFailedInteraction`.
Gerçek oyunda bu ek bir kayıp kanalı; sim bunu saymıyor (yani v4 bile İYİMSER).

## 7. `runFullSim`'in modellediği zincir (Round 2+ için referans)

kota Q(gün,P) → varış aralığı I×waveFactor(1.0392, `WaveSettings.asset` aktif) →
spawn kapısı (`HasUnspawnedCustomers` && `!IsQueueFull`, maxQueueSize=2) → TEK istasyon seri
servis (`serviceTables[1]`={fileID:0}) → 17:30 kesimi (kaçan −0.4 / hiç spawn olmayan −0.2) →
erken gün bitişi (+30sn grace) → gün aktif süresi − telefon zaman-atlaması → tır penceresi →
`min(mekanik tavan, ürün arzı)` → `GetRewardPerBox(P) + floor(prestij/8)×5` → kira (her 4 gün,
×1.20^döngü, 1 kez grace %80) → gün 16 + prestij>0 = kazanma (`GameStateManager.cs:696-712`).

`boxesPerMinPerPlayer` (Normal 2.0 / Slow 1.2 / Fast 3.0) DEĞİŞTİRİLMEDİ — `ASSUMED`'dan
aynen kullanılıyor. Yeni tek davranış varsayımı: `ASSUMED4.phoneUseRate` (strict 0.60 /
optimistic 0.10) — eski `ASSUMED.phoneAnswerRate` V3 pasif telefonundu, V4'te anlamsız.

İlgili: [[plateup_customer_quota_2026-08-29]], [[rent_growth_1_35_deficit_2026-08-20]],
[[money_comes_only_from_trucks]], [[post_rent_features_pricing_2026-08-15]],
[[sim_resync_2026-08-19]]
