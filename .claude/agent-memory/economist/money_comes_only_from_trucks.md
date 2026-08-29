---
name: money_comes_only_from_trucks
description: TEMEL EKONOMI GERCEGI - para YALNIZ tir kutu tesliminden (rewardPerBox x kutu) gelir; musteriler (CustomerAI) SIFIR para verir, yalniz prestij. Tir throughput'u = para tavani.
metadata:
  type: project
---

> ⚠️ **KISMEN GÜNCEL DEĞİL (2026-08-29 düzeltme, bkz [[plateup_customer_quota_2026-08-29]]).**
> "Müşteri talebi parayı DOĞRUDAN sınırlamaz" cümlesi ARTIK YANLIŞ: `CustomerAI.cs:1442-1444`
> ürünü (para zincirinin hammaddesi) MÜŞTERİNİN KENDİSİ `Instantiate` ediyor; `ShelfState.cs`
> hiç üretmiyor, sadece depoluyor — yani müşteri sayısı GÜNLÜK KUTU ARZININ (=gelirin) GERÇEK
> tavanı. Bu, ESKİ kapasite-tabanlı talep modelinde (demand 15-49, truck cap 5-32) talep HER
> ZAMAN truck cap'in üstünde olduğu için gizli kalmıştı — PlateUp'ın küçük, gerçekçi
> kota değerleriyle (5-13/gün) artık müşteri sayısı ÇOĞU ZAMAN truck throughput'tan DAHA
// SIKI bağlayıcı. "Müşteriler PARA VERMEZ" (doğrudan AddMoney çağırmazlar) hâlâ doğru — ama
> dolaylı olarak parayı SIKI sınırlıyorlar. Gelir kaldıracı ararken artık İKİSİNE BİRDEN
> bakılmalı: `min(truckThroughput, servisEdilenMüşteri)`.

**Kod ile doğrulandı (2026-07-20 holistik denetim, o turdaki ESKİ kapasite-modeli için hâlâ geçerli).** Cargor'da PARA akışı:

- **Tek para kaynağı = tır teslimi.** `Truck.cs:571 ProcessSuccessfulDelivery` →
  `MoneySystem.AddMoney(CalculateRewardWithPrestige())` = `(rewardPerBox + floor(prestij/prestigePerBonus)*bonusPerTier)`
  her doğru kutu için. Yardımcı kaynaklar: FESTIVAL bonusu (kira %10-20), telefon (callMoneyReward 20),
  quest ödülü — hepsi küçük.
- **Müşteriler (CustomerAI) PARA VERMEZ.** `CustomerAI.cs`'de `AddMoney/ModifyMoney/money` grep = 0 sonuç.
  Müşteri servisi yalnız PRESTİJ verir (`customerServedPrestigeBonus`), kaçan müşteri prestij düşürür
  (`customerLostPrestigePenalty`). Prestij → kutu-başı ödül tier'ını besler (dolaylı para etkisi).

**Sonuç — para = tır throughput'u × kutu-başı-ödül.** Müşteri talebi parayı DOĞRUDAN sınırlamaz;
prestiji (dolayısıyla ödül tier'ını) besler. Bu yüzden tır penceresi kapasitesi ([[truck_hangar_window_cap]])
= gerçek para tavanı. Sim (`sim.js`) `deliveriesAttempted=min(demand,truckCap)` yazsa da pratikte
truckCap her zaman bağlayıcı (demand 15-49 >> truckCap 5-32), yani gelir ≈ truckCap × ödül.

**Denetim sayıları (2026-07-20, Normal, optimistic model, 1 hangar):**
- Tır throughput (kutu/gün): 1P ~5.7-7.9, 2P ~11.5-15.8, 3P ~17.2-23.7, 4P ~22.9-31.6 (gün 8→16).
- Kutu-başı ödül prestijle 55→175 TL (tier 1→25, prestij 6→100'de, `maxPrestige=100` clamp ile TAVANLI).
- Talep (musteri) ~46-49'da clamp'e çarpıyor ama parayı gate'lemiyor (yalnız prestij).

**Modelleme boşluğu / risk (playtest-bağımlı):** Sim müşteri servisini tır kapasitesinden BAĞIMSIZ
modelliyor (tüm demandAdjusted için prestij verir, yalnız %3-8 flat kayıp). GERÇEKTE müşteri servisi
(raf-stoklama emeği) ile tır-yükleme emeği AYNI oyunculardan çekiliyor olabilir. Eğer öyleyse yüksek
talepte (49) az oyuncuyla çok müşteri servissiz kaçar → `customerLostPrestigePenalty` birikimi sim'in
göstermediği prestij kanamasına yol açabilir. İkinci belirsizlik: `CustomerManager` talep formülü (max 50)
ile `PrestigeManager.GetCustomerCapacity()` (max 20 eşzamanlı) arasında hangisi gerçek spawn'ı yönetiyor
belirsiz — talep sim'de fazla tahmin edilmiş olabilir. İkisi de çözülmedi, playtest gerektirir.

**How to apply:** Gelir/para analizinde ASLA "müşteri sayısı × X TL" varsayma; para = tır kutu-teslimi.
Bir gelir kaldıracı ararken tır throughput'una (hangar süresi, hangar sayısı, kutu üretim hızı, gün süresi)
bak, müşteri sayısına değil. Müşteri sayısı prestij kaldıracıdır.

İlişkili: [[truck_hangar_window_cap]], [[prestige_100_rescale_2026-07-20]], [[prestige_fragility]]
