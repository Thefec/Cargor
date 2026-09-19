# 01 — Parametreler (Keşif çıktısı)

**Tarih:** 2026-09-18 · **Dal:** `fix/difficulty-scaling-and-dead-code` · **Yöntem:** kod + `Assets/Resources/EkonomiAyarlari.asset` + sahne (`Assets/Scenes/The Main Office.unity`) + prefab'lar doğrudan okundu. Hiçbir değer tahmin değildir; kaynağı olmayan "BULUNAMADI" yazılmıştır.

**Öncelik kuralı:** sahne/prefab/asset YAML'ındaki değer ≠ kod default'u ise **YAML esas**. Asset'te anahtar hiç yoksa kod initializer canlı (Unity eksik anahtarı yazmaz; float[]'a elle hex yazılmaz).

Kısaltmalar: `GES` = `Assets/NewCss/GameEconomySettings.cs`, `ASSET` = `Assets/Resources/EkonomiAyarlari.asset`, `SCENE` = `Assets/Scenes/The Main Office.unity`, `DCM` = `Assets/NewCss/UIScripts/DayCycleManager.cs`, `CM` = `Assets/NewCss/CustomerSripts/CustomerManager.cs`, `CAI` = `Assets/NewCss/CustomerSripts/CustomerAI.cs`, `DM` = `Assets/NewCss/GameState/DifficultyManager.cs`, `UP` = `Assets/NewCss/UpgradeScripts/UpgradePanel.cs`, `PE` = `Assets/NewCss/UpgradeScripts/PerkEffect.cs`, `EEM` = `Assets/NewCss/Events/EventEffectManager.cs`, `ECU` = `Assets/NewCss/Events/EventCalendarUI.cs`, `PCM` = `Assets/NewCss/Phone/PhoneCallManager.cs`, `GSM` = `Assets/NewCss/GameState/GameStateManager.cs`, `TS` = `Assets/NewCss/TruckScripts/TruckSpawner.cs`, P = oyuncu sayısı.

---

## A. Para — başlangıç, kira, iflas

| Parametre | Değer | Birim | Kaynak | Ne işe yarar |
|---|---|---|---|---|
| `startingMoney` | 500 | TL | `MoneySystem.cs:12`; SCENE `startingMoney: 500` | Kasa açılışı (P-ölçekleme aşağıda ezer) |
| `baseStartingMoney` | 500 | TL | `DM:36`; `Assets/DifficultyManager.prefab:75` | P-ölçekli başlangıç tabanı |
| `moneyMultiplierPerPlayer` | 1.2 | çarpan/ek oyuncu (üstel) | `DM:61`; prefab aynı | `ScaledStartingMoney = 500×1.2^(P−1)` → **500 / 600 / 720 / 864** (`DM:362-371`), `MoneySystem.SetMoney` ile yazılır (`DM:505-529`) |
| Para tabanı | 0 | TL | `MoneySystem.cs:91` `Mathf.Max(0, …)` | Kasa eksiye inmez; 0'dayken cezalar yutulur. **İflas yalnız kira kapısında** |
| `baseRentByPlayerCount` | {290, 650, 1140, 1630} | TL | `GES:21`; ASSET:15 hex `22010000 8a020000 74040000 5e060000` (=aynı) | Dönem-0 kira |
| `rentGrowthMultiplier` | 1.20 | çarpan/dönem | `GES:24`; ASSET:16 | Her ödemede kira ×1.2 |
| `rentIntervalDays` | 4 | gün | `GES:27`; ASSET:17 | Kira günleri 4, 8, 12, 16 (`DCM:601` `day % 4 == 0`) |
| `rentScaledMultiplier` | 1.0 | çarpan | `GES:33`; ASSET:19 | Kaldıraçlı Kira perki 0.75 yazar (`PE:325`) |
| Kira formülü | `base(P) × 1.2^cycle × rentScaledMultiplier` | TL | `GES:229-234`; `DCM:662-686` | `cycle = _rentPaymentCount` (0,1,2,3). **wealthTax YOK** (eski raporlardaki %10 vergi kaldırılmış) |
| Kira tablosu (perksiz) | 1P 290/348/418/501 · 2P 650/780/936/1123 · 3P 1140/1368/1642/1970 · 4P 1630/1956/2347/2817 | TL | hesap (formülden) | Toplam 16 gün: 1P 1557 · 2P 3489 · 3P 6120 · 4P 8750 |
| `gracePaymentPercent` | 0.8 | oran | `GES:30`; ASSET:18 | Para yetmezse **oyun boyu 1 kez** eldekinin %80'i alınır, ödenmiş sayılır (`DCM:619-628`, `_graceUsed`) |
| Acil Fren (perk) | prestij −2 | prestij | `DCM:84,629-640` | Grace bittiyse ve `insuranceAvailable` ise kira silinir, perk tükenir |
| İflas | — | — | `DCM:641-653` → `GSM.TriggerLose` | Grace de sigorta da yoksa ve para < kira → Game Over |
| Kira zamanı | gün sonu (elapsed ≥ süre) | — | `DCM:555-583` | Kira, o günün geliri kasaya girdikten **sonra** çekilir |
| Kazanma | gün 16 tamamlanınca | — | `GSM:704-720`, `DCM:37 MAX_DAYS=16` | Prestij/kira ek şart değil; gün 16 kirası ödenmişse kazanılır |

## B. Zaman — gün döngüsü

| Parametre | Değer | Birim | Kaynak | Ne işe yarar |
|---|---|---|---|---|
| `realDurationInSeconds` | 200 | gerçek sn | `DCM:53`; SCENE `realDurationInSeconds: 200` | Gün 1-3 süresi |
| `dailyDurationIncrease` | 10 | sn/gün | `DCM:56`; SCENE 10 | Gün d>3: `200 + (d−3)×10` (`DCM:194-208`) → gün 16 = **330 s** |
| `DYNAMIC_DURATION_START_DAY` | 3 | gün | `DCM:38` | Uzama gün 4'ten başlar |
| `startHour` / `endHour` | 7 / 18 | oyun saati | `DCM:59,62`; SCENE 7/18 | 11 oyun-saati; 1 oyun-saat = süre/11 gerçek sn (gün 1: 18.2 s; gün 16: 30 s) |
| Mesai Saati perki | ×1.125 | çarpan | `PE:375`, `DCM:478-491` | Taban 200 → 225 s (artış +10/gün aynı kalır, tabana uygulanmaz: `RecomputeDayDuration` yalnız `_baseRealDuration×mult`) |
| Tır saatleri | 8 → 17 | oyun saati | `TS:71,74`; SCENE | 17:00'de tüm tırlar zorla çıkar (`TS:316-321`) |
| Müşteri spawn penceresi | 8 → 17 (dahil) | oyun saati | `CM:21-22`, SCENE `spawnStartHour/EndHour` | `IsWithinSpawningHours` (`CM:521-527`) |
| `CUSTOMER_EXIT_HOUR` | 17.5 | oyun saati | `CM:27` | Kuyruktakiler zorla çıkar (−0.4), spawn olmamış kota −0.2 (`CM:550-618`) |
| Telefon saatleri | 8 → 18 | oyun saati | `PCM:40,43`; SCENE | Ayrıca 17:30 tahmin guard'ı (`PCM:428-434`) |
| Upgrade paneli | ≥ 10:00 | oyun saati | `UP:138` | Günün ilk ~%27'sinde alışveriş yapılamaz |
| `dayEndGraceSeconds` | 30 | gerçek sn | `GES:59`; ASSET:20 | Kota bitip kuyruk boşalınca 30 s sonra gün sarılır (`CM:900+`, `DCM:464-471`) |
| Erken bitiş şartı | kota tükendi ∧ kuyruk boş ∧ spawn edilmemiş yok | — | `CM:900-906` | Bu yolda missed-quota cezası tetiklenmez |

## C. Müşteri — kota, varış, sabır, servis

| Parametre | Değer | Birim | Kaynak | Ne işe yarar |
|---|---|---|---|---|
| `dailyCustomerCountP1` | {4,4,4,4,4,4,4,5,5,5,5,5,6,6,6,6} | müşteri/gün (index=gün−1) | `GES:44` (ASSET'te YOK → kod canlı) | 1P kota |
| `dailyCustomerCountP2` | {7,7,7,8,8,9,9,9,10,10,10,11,11,11,12,12} | " | `GES:47` | 2P kota |
| `dailyCustomerCountP3` | {8,8,8,8,9,9,9,10,10,10,11,11,12,12,12,13} | " | `GES:50` | 3P kota |
| `dailyCustomerCountP4` | aynı dizi (P3 ile eşit) | " | `GES:53` | 4P kota — **P3 = P4** (tek istasyon doygunluğu gerekçesiyle bilinçli) |
| Kota formülü | `clamp(round(tablo × eventMult), 1, 50)` | müşteri | `CM:394-410` | `eventCustomerMultiplier` (EEM `dailyCustomerMultiplier`) |
| `_minCustomersPerDay` / `_maxCustomersPerDay` | 1 / 50 | müşteri | `CM:83,86` | Güvenlik clamp |
| `customerArrivalIntervalByPlayerCount` | {44, 22, 21, 21} | gerçek sn | `GES:56` (ASSET'te YOK) | Ardışık spawn'lar arası taban aralık |
| `spawnTimeRandomness` | 0.2 | ±oran | `CM:89`; SCENE 0.2 | Aralığa simetrik jitter (`CM:444-445`) |
| Wave çarpanı | aralık ÷ `spawnRateMultiplier` | — | `CM:447-454`; `Assets/Scenes/WaveSettings.asset` | 08-12 ×1.0 (max kuyruk 4) · 12-14 ×1.5 (6) · 14-15 ×0.5 (2) · 15-16 ×0.8 (3) · 16-17 ×1.3 (4) · 17-18 ×0.6 (2) |
| Spawn engelleri | kuyruk ≥ `maxQueueSize` ∨ kuyruk ≥ wave `maxCustomers` ∨ kota bitti ∨ saat dışı | — | `CM:475-494` | Engellenen spawn **kaybolmaz, ertelenir** |
| `maxQueueSize` | **2** | müşteri | SCENE `maxQueueSize: 2` (kod `DEFAULT_QUEUE_SIZE=2`, `CM:20`) | Kuyruk kapasitesi; wave max'ları (4/6) hiç bağlayıcı değil (2 < hepsi) |
| Uzun Kuyruk perki | 2+2 = 4 | müşteri | `PE:265` | `disabledInDraft: 1` → **draft'a hiç girmez** (ölü) |
| Sabır tabanı | min 15 / max 20 | gerçek sn | `DM:43,46`; `Assets/DifficultyManager.prefab:76-78` (aynı) | Müşteri bekleme süresi `Random(min,max)` (`CAI:547`) |
| `patienceReductionPerPlayer` | 2 | sn/ek oyuncu | `DM:66`; prefab 2 | 1P 15-20 · 2P 13-18 · 3P 11-16 · 4P 9-14 (floor 5/10 `DM:373-385`); spawn anında yazılır `CM:735-736` |
| `patienceMultiplier` | 1.0 | çarpan | `CM:64`; SCENE 1 | Sabırlı Müşteriler perki artık buna DEĞİL, etkileşim süresine yazıyor |
| `interactionTime` | **2** | gerçek sn | `Assets/ithappy/…/Customer.prefab:2327` (**PREFAB OVERRIDE**, kod default 5 `CAI:117`) | Oyuncunun E ile servis etkileşimi süresi |
| `interactionTimeMultiplier` | 1.0 | çarpan | `CM:67`; SCENE 1 | Sabırlı Müşteriler perki 0.6 yazar (`PE:258`) → 1.2 s |
| Etkin etkileşim süresi | `2 × mult × dualMult` | sn | `CAI:1184-1196` | dualMult: tedarik-dual 1.3, iade-dual 1.15/bacak (`PostRentFeatureUnlocks.cs:60,74`) |
| Servis istasyonu | **1 fiilen** (dizi 2 slot, biri `{fileID: 0}`) | adet | SCENE:87881-87883 `serviceTables`; `CM:1006-1049 AssignFreeServiceStations/FindFreeStationIndex` (null slot atlanır) | Paralel-istasyon kodu VAR ama sahnede 2. masa bağlı değil → aynı anda **tek müşteri** servis alır; P bunu değiştirmez. P3/P4 doygunluğunun kökü |
| Kuyruk bekleme | kuyruktaki (istasyon dışı) müşteri de sabır sayacında | — | `CAI:936-973` | Kuyruk 2 → en fazla 1 istasyonda + 1 bekleyen |
| Müşteri başarı | +0.4 prestij, **0 TL** | — | `CAI:1392-1399`, `GES:150` | Müşteri para vermez |
| Yanlış ürün | −0.20 prestij, müşteri anında çıkar | — | `CAI:1405-1413`, `GES:153` | SURPRISE AUDIT ×2 |
| Sabır bitti (kaçış) | −0.4 prestij | — | `CAI:975-1005` → `GSM:633-659`, `GES:144` | |
| Ürün arzı | tedarik müşterisi 1 ürün (gün 9+: 2 ürün) | ürün | `CAI:1226-1228 PlaceProductCoroutine`; `PostRentFeatureUnlocks.cs:25,48` | Ürün = kutunun hammaddesi; **müşteri = tek ürün kaynağı** |
| İade modu | gün ≥5, %25 müşteri | oran | `PostRentFeatureUnlocks.cs:16,22`; `CAI:556-573` | Müşteri ürün BIRAKMAZ; istenen renkte **paketlenmiş kutuyu oyuncunun elinden alır** (`CAI:1300-1322`) → kutu stoktan silinir, para yok, +0.4 prestij |
| Dual-item | gün ≥9, %100 | — | `PostRentFeatureUnlocks.cs:25,48-51` | Tedarik: 2 ürün, etkileşim ×1.3; iade: 2 renk, 2 ayrı E, ×1.15/bacak |

## D. Tır / teslimat — tek para kaynağı

| Parametre | Değer | Birim | Kaynak | Ne işe yarar |
|---|---|---|---|---|
| `rewardPerBoxByPlayerCount` | {50, 55, 70, 88} | TL/kutu | `GES:71` (ASSET'te YOK → canlı) | Doğru kutu tabanı; `Truck.OnNetworkSpawn` kopyalar (`Truck.cs:249-257`) |
| `rewardPerBox` (legacy) | 50 | TL | `GES:68`; ASSET:21 | Yalnız dizi boşsa |
| `penaltyPerBox` | 40 | TL | `GES:74`; ASSET:22 | Yanlış renk teslim (`Truck.cs:668-686`), AUDIT ×2 |
| `wrongDeliveryPrestigePenalty` | −0.16 | prestij | `GES:159`; ASSET:38 | Yanlış teslimde ek |
| `prestigePerBonus` | 8 | prestij/tier | `GES:83`; ASSET:25 | (Truck prefab'ta 4 yazar ama SO ezer — `EconomyInvariantCheck.cs:395-402` notu) |
| `bonusPerTier` | 5 | TL/tier | `GES:86`; ASSET:26 | Prestij Simsarı 5.5/6 (`PE:205`) |
| Kutu ödülü formülü | `rewardPerBox(P) + floor(prestij/8)×5` | TL | `Truck.cs:706-742` | prestij 12→+5, 16→+10, 100→+60. Event çarpanı `truck.rewardPerBox`'a `(int)` cast ile uygulanır (`EEM:522`) |
| Volatilite | 0 / mean 1 | — | `GES:89,92`; ASSET | Perk: ±0.35, mean 1.15 (`PE:334-335`; `Truck.cs:723-733`) |
| `hangarStayDurationByPlayerCount` | {120, 60, 40, 30} | gerçek sn | `GES:80`; ASSET:24 hex 78/3c/28/1e | Tır giriş animasyonu bitince sayaç başlar (`Truck.cs:347-354, 394-408`); dolunca **veya** süre bitince kalkar (`:425`) |
| Hızlı Hangar perki | ×1.30 | çarpan | `PE:219-224` | |
| `truckCargoMin/MaxExclusive` | {1,2,2,2} / {3,4,5,6} | kutu | `GES:95,98`; ASSET:29-30 | Kargo: 1P 1-2 · 2P 2-3 · 3P 2-4 · 4P 2-5 (uniform) |
| `exitDelay` | **2** | gerçek sn | `Assets/NewCss/TruckScripts/Truck_Anim (2).prefab` `exitDelay: 2` (**PREFAB OVERRIDE**, kod 5 `Truck.cs:102`; sahne `truckPrefab` guid `7269fc87…` = bu prefab) | Kalkış öncesi bekleme; event `exitDelayMultiplier` ile çarpılır |
| `respawnDelayRange` | 3-5 | gerçek sn | `TS:64`; SCENE {3,5} | Tır gidince yenisi |
| Giriş/çıkış animasyon süresi | BULUNAMADI | sn | Animator klip (`Truck.cs` sayısallaştırmıyor) | VARSAYIM gerekli (sim.js 6 s tampon kullanıyor) |
| Hangar sayısı | 3 tanımlı, 1 açık | adet | SCENE `requiredUpgradeLevel: 0/1/2` | "Ek Hangar" maxLevel 1 → **3. hangar ulaşılamaz** |
| Renk dağılımı | 3 renkli torba (her 3 tırda 3 renk) | — | `TS:631-657` | Kutu rengi ürün kategorisinden gelir (`Table.cs:1118-1120`) |
| Karışık tır | gün ≥13, 2-3 renk (%50) | — | `TS:521-591`, `PostRentFeatureUnlocks.cs:28` | Her renk ≥1 |
| Kutu düşürme | −5 TL, −0.04 prestij | — | `GES:107,156`; `BoxFallPenalty.cs:41,126-179` | Çarpma hızı ≥3 m/s; AUDIT ×2; boş kutu bedava |
| Paketleme masası | 1 aktif (sv0), tavan 2 | adet | SCENE "Paketleme İstasyonu" levelObjects (GDD §33.3) | Masa tek ürün taşır (`Table.cs:66,99`), `ITEM_SPAWN_DELAY 0.1` (`:17`) — zamanlı kapı yok, hız = insan |

## E. Prestij

| Parametre | Değer | Kaynak | Not |
|---|---|---|---|
| `startingPrestige` | **12** | `PrestigeManager.cs:16`; SCENE 12 | |
| `maxPrestige` | 100 | `:19`; SCENE 100 | `ModifyPrestige` clamp (`:160`) |
| Kayıp | ham ≤ 0 → `TriggerLose` | `:154-157` | Clamp öncesi kontrol |
| `prestigePerCustomer` / base / max kapasite | 4 / 1 / 20 | `:26,29,32`; SCENE 4 | `GetCustomerCapacity()` — tüketicisi: bkz. §K (ölü mü teyit) |
| Kaynaklar (+) | servis +0.4 (`GES:150`), telefon +0.4 (`GES:132`), quest +0.6/1.2/3.0, Prestij Ustası 0.52/0.64 (`PE:213`) | | |
| Kaynaklar (−) | kaçış −0.4, kota kaçırma −0.2, yanlış ürün −0.20, yanlış teslim −0.16, düşme −0.04, Acil Fren −2, quest −0.32/−0.4/−0.6 | `GES:144-159` | AUDIT günü hepsi ×2 (`EEM:455-459`) |
| Etki | kutu ödülü tier'ı (yukarı) | `Truck.cs:735-742` | Başka ekonomik etkisi YOK (kapasite formülü tüketilmiyorsa) |

## F. Telefon (V4 — dışarı arama)

| Parametre | Değer | Kaynak | Not |
|---|---|---|---|
| `timeSkipAmountByPlayerCount` | {115, 49, 47, 47} | `GES:117` (ASSET'te YOK) | Çağrı başına atlanan "dakika" |
| SkipTime dönüşümü | `dk × (realDurationInSeconds/11)/60` gerçek sn | `DCM:421-436` | **`realDurationInSeconds` (200, günden bağımsız taban) kullanır, `CurrentDayDuration` değil** → 115 dk = 34.8 s; 49 = 14.8 s; 47 = 14.2 s. Gün 16'da (330 s) aynı sn daha az oyun-dakikasına denk gelir |
| `phoneTimeSkipPerkMultiplier` | 1 → perk 0.80 | `GES:120`; `PE:308` | |
| CUSTOMER SUPPORT | ×0.5 | `PCM:311-323 GetEffectiveTimeSkipMinutes` | Perk ile çarpımsal (0.8×0.5=0.4) |
| `phoneCooldownSeconds` | 3 | `GES:123` | Perk bonus 0→1 s (`GES:126`, `PE:309`), min 1 (`PCM:299-302`) |
| `callMoneyReward` | **0** | `GES:129`; ASSET:32 | 2026-09-18'de 20→0 |
| `callPrestigeReward` | 0.4 | `GES:132`; ASSET:33 | |
| Guard'lar | saat 8-18 ∧ spawn edilmemiş kota var ∧ kuyruk dolu değil ∧ cooldown yok ∧ atlama 17:30'u geçmiyor | `PCM:397-437` | Spawn başarısızsa ödül/atlama yok (`PCM:448-455`) |
| Etkisi | sıradaki kota müşterisini hemen spawn eder | `CM:669-696` | Kotayı ARTIRMAZ, öne çeker |

## G. Upgrade / perk (draft)

**Mekanik:** günde 3 kart (`DraftPool.cs:13`), seed = gün (`UP:1306`), T2 gün≥5, T3 gün≥9 (`DraftPool.cs:11-12`), omurgalar tier'sız hep havuzda (`DraftPool.cs:29`), max seviyeye ulaşan düşer, `disabledInDraft` düşer, `requiresQuestSystem` → quest asset varsa girer (`UP:1351-1362`). Dışlama: {gambler_case, all_in}, {leveraged_rent, all_in} (`UP:215-221`).
**Maliyet:** `round((baseCost + level×costStep) [×0.5 Toplu Alım] × eventMult × Pmult)` (`UP:1611-1623`); `Pmult = {1.00, 2.00, 2.95, 3.70}` (`DM:73`, prefab override yok); Görev Kademesi P-çarpanından muaf (`UP:1645-1651 isQuestTierUpgrade`). OPPORTUNITY DAY ×0.8.
**Reroll:** {50, 90, 160, 290, 525} × Pmult, günlük sıfırlanır (`RerollCurve.cs`, `UP:1532-1537`).
**Aktivasyon gecikmesi:** satın alınan kart `_pendingUpgrades`'e girer ve **ertesi gün başında** aktifleşir (`UP:1221-1264 ActivatePendingUpgradesServerRpc`, `dayPurchased < currentDay`). Yani gün d'de alınan kart gün d+1'den itibaren çalışır; gün 16'da alım anlamsız.

Sahnedeki 25 tanım (`SCENE:27162-27676`), fiyat = 1P (Pmult=1):

| # | Kart | Tür | Tier | maxLv | baseCost | costStep | Fiyat sırası (1P) | Etki (kod) | Durum |
|---|---|---|---|---|---|---|---|---|---|
| 1 | Geniş Ambar | omurga | — | 2 | 60 | 30 | 60, 90 | Raf seviye objesi açar (depo alanı) — kotaya/gelire doğrudan etki YOK | havuzda |
| 2 | Paketleme İstasyonu | omurga | — | 1 | 150 | 150 | 150 | 2. paketleme masası | havuzda |
| 3 | Geniş Kuyruk | omurga | — | 3 | 250 | 100 | — | maxQueueSize | **disabledInDraft** |
| 4 | Sağlam Kasa | omurga | — | 3 | 300 | 100 | — | eski Money | **disabled** |
| 5 | Dinç Ekip | omurga | — | 3 | 100 | 75 | — | stamina | **disabled** |
| 6 | Ek Hangar | omurga | — | 1 | 200 | 100 | 200 | 2. hangar (`TS.SetTruckUpgradeLevel`) | havuzda |
| 7 | Su Sebili | omurga | — | 1 | 500 | 200 | — | achievement | **disabled** |
| 8 | Güler Yüz | omurga | — | 3 | 300 | 200 | — | eski Customer | **disabled** |
| 9 | Görev Kademesi | omurga | — | 2 | 80 | 20 | 80, 100 (P-muaf) | Quest tier 1/2 (`UP:1187-1191`) | requiresQuest (aktif: 30 asset var) |
| 10 | Ucuz Kira (`cheap_rent`) | perk | T3 | 3 | 130 | 30 | 130, 160, 190 | growth 1.20→1.17→1.14→1.11 (`PE:198`) | havuzda |
| 11 | Prestij Simsarı (`prestige_broker`) | perk | T3 | 2 | 130 | 15 | 130, 145 | bonusPerTier 5→5.5→6 (`PE:205`) | havuzda |
| 12 | Prestij Ustası (`prestige_master`) | perk | T2 | 2 | 175 | 25 | 175, 200 | servis prestiji 0.4→0.52→0.64 (`PE:213`) | havuzda |
| 13 | Hızlı Hangar (`fast_hangar`) | perk | T2 | 1 | 120 | 0 | 120 | hangar süresi ×1.3 (`PE:223`) | havuzda |
| 14 | Enerjik Ekip (`energetic_crew`) | perk | T1 | 1 | 100 | 0 | 100 | staminaRegen 2.5 (`PE:239`) | havuzda |
| 15 | Çevik Ekip (`agile_crew`) | perk | T1 | 1 | 180 | 0 | 180 | moveSpeed 5→5.75 (`PE:247`) | havuzda |
| 16 | Sabırlı Müşteriler (`patient_customers`) | perk | T1 | 1 | 120 | 0 | 120 | etkileşim 2→1.2 s (`PE:258`) | havuzda |
| 17 | Uzun Kuyruk (`long_queue`) | perk | T1 | 1 | 240 | 0 | — | kuyruk 2→4 | **disabledInDraft** |
| 18 | Kumarbaz Kasası (`gambler_case`) | perk | T2 | 1 | 350 | 0 | 350 | ödül ×1.30, ceza ×1.55 (`PE:292-293`) | havuzda |
| 19 | Telefon Hattı (`phone_line`) | perk | T1 | 1 | 160 | 0 | 160 | timeSkip ×0.8, cooldown −1 s (`PE:308-309`) | havuzda |
| 20 | Mesai Saati (`overtime`) | perk | T1 | 1 | 300 | 0 | 300 | gün ×1.125 (`PE:375`) | havuzda |
| 21 | Kaldıraçlı Kira (`leveraged_rent`) | perk | T3 | 1 | 300 | 0 | 300 | kira ×0.75, grace=0 (`PE:325-326`) | havuzda |
| 22 | Yüksek Volatilite (`high_volatility`) | perk | T2 | 1 | 320 | 0 | 320 | ödül ×U(0.80,1.50) (mean 1.15) (`PE:334-335`) | havuzda |
| 23 | Acil Fren (`emergency_brake`) | perk | T1 | 1 | 250 | 0 | 250 | 1 kez iflas önler, −2 prestij (`PE:364`, `DCM:629-640`) | havuzda |
| 24 | Kelle Koltukta (`all_in`) | perk | T3 | 1 | 320 | 0 | 320 | ödül ×1.25, grace=0 (`PE:344,356`) | havuzda |
| 25 | Toplu Alım (`bulk_buy`) | perk | T1 | 1 | 80 | 0 | 80 | ertesi draft'ta 1 kart −%50 (`PE:383`, `UP:1320-1332`) | havuzda |

Havuz özeti: **19 aktif kart** (4 omurga + 15 perk), 6 ölü. Tam set (1P, tüm seviyeler) = 150+150+200+180+480+275+375+120+100+180+120+350+160+300+300+320+250+320+80 = **4.410 TL** (Görev Kademesi P-muaf; diğerleri ×Pmult).

## H. Event'ler

Takvim (`ECU:22-27, 739-787`): ilk 3 gün event yok; sonraki event günü = önceki + U{1,2}; **kira günleri (4/8/12/16) atlanır**; ilk 2 event pozitif havuzdan, 3. event negatif havuzdan, sonrakiler 16'lık havuzdan uniform. Seed server'da rastgele (`ECU:343`). 16 günde tipik **6-8 event günü**.

| Event | Tip | Etki (`EEM:130-360`) |
|---|---|---|
| BUSY DAY | − | kota ×1.35, sabır ×0.85 |
| DELIVERY BONUS | + | kutu ödülü ×1.20 |
| ANGRY CUSTOMERS | − | sabır ×0.60, kota ×1.10 |
| RELAXED DAY | + | sabır ×1.30 |
| SLOW LOGISTICS | − | ödül ×0.92, exitDelay ×1.5 |
| EXPRESS CARGO | + | ödül ×1.08, exitDelay ×0.7 |
| HEAVY BOXES | − | hız ×0.85, sprint ×0.8 |
| GOLDEN BOX DAY | + | ödül ×1.15, kota ×1.15, exitDelay ×0.8, hız ×1.08, sprint ×1.2, stamina regen ×0.8 |
| OPPORTUNITY DAY | + | upgrade maliyeti ×0.8 |
| FATIGUE PROBLEM | − | hız ×0.9, sprint ×0.7, regen ×0.6, kota ×0.85 |
| VIP SERVICE | + | ödül ×1.12 |
| RAINY DAY | − | kota ×0.8 |
| MARKETING DAY | − | ödül ×0.7, kota ×1.2 |
| SURPRISE AUDIT | − | tüm cezalar ×2 (`GetPenaltyMultiplier`) |
| FESTIVAL DAY | + | gün başı +U(%10, %20) × o anki kira (`EEM:401-421`); fallback 100-300 |
| CUSTOMER SUPPORT | + | telefon time-skip ×0.5 |

Not: ödül çarpanı `(int)(rewardPerBox × mult)` — tabana uygulanır, prestij tier bonusuna değil (`Truck.cs:706-711`: bonus ayrı toplanır).

## I. Quest

| Parametre | Değer | Kaynak |
|---|---|---|
| Günlük teklif | 3 (her açık tier'dan 1 + dolgu; best-of-K fizibilite) | `QuestManager.cs:26,591-675` |
| Günlük kabul | en fazla 1 | `QuestManager.cs:948 _hasAcceptedToday` |
| Tier başlangıcı | 0 (Easy); Görev Kademesi ile 1/2 | `UP:1187-1191`, `QuestManager.cs:911-920` |
| Ödül/ceza (30 asset, grep ile doğrulandı) | Easy +28 TL/+0.6 · ceza −15/−0.32 (11 asset) · Medium +60/+1.2 · −20/−0.4 (10) · Hard +150/+3.0 · −30/−0.6 (9) | `Assets/Resources/Quests/*.asset` |
| Hedefler | tır 1/2/3, raf 4-12, paket 4-12, telefon 1/2; renk-kilitli varyantlar 2/3/5 | sim.js `QUEST_ASSETS` (asset'lerle tutarlı) |
| Ödeme zamanı | gün sonu settle, ödül **ertesi gün başında** kasaya | `QuestManager.cs:1023-1054` (sim.js not #4) |

## J. Kira-sonrası özellik günleri

Gün 5 iade (%25), gün 9 dual-item (%100), gün 13 karışık tır — `PostRentFeatureUnlocks.cs:16-28`.

## K. Kod ≠ YAML çelişkileri, ölü kablolar, teyitler

| Konu | Kod | Canlı (YAML) | Karar |
|---|---|---|---|
| `CustomerAI.interactionTime` | 5 (`CAI:117`) | **2** (Customer.prefab:2327) | 2 esas |
| `Truck.exitDelay` | 5 (`Truck.cs:102`) | **2** (Truck_Anim (2).prefab) | 2 esas; **sim.js SRC4 5 kullanıyor → BAYAT** |
| `Truck.prestigePerBonus` prefab | 4 | SO 8 ezer (`Truck.cs:255`) | 8 |
| `DM.customerCountPerPlayer` | 5 | prefab 2 | Önemsiz: `ScaledCustomerCount` **ÖLÜ** (tüketicisi yok, GDD §19.1 notu) |
| `PrestigeManager.GetCustomerCapacity()` | 1+floor(prestij/4), max 20 | — | **ÖLÜ**: `Assets/**/*.cs` grep'inde PrestigeManager dışında tüketici yok (yalnız UI metni). Prestijin tek ekonomik etkisi kutu ödülü tier'ı |
| `CalculateRent` fallback | 290/650/1140/1630 g1.2 (`DCM:678-679`) | — | Senkron, risk yok |
| Sahne `startingMoney` | 500 | 500 | Eski 50 000 debug değeri **GİTMİŞ** |
| Quest ceza (sim.js QR) | Med 27/0.55, Hard 53/1.05 | asset Med 20/0.4, Hard 30/0.6 | **sim.js BAYAT**, asset esas |

## L. `tools/economy-sim/sim.js` (v5.1) çapraz kontrol

Uyumlu: kira, kota, aralık, ödül, hangar, kargo, telefon, prestij cezaları, gün süresi, wave, iade/dual/karışık günleri, başlangıç parası, upgrade P-çarpanı.
Sapmalar: (1) `exitDelay 5` → canlı 2; (2) quest Medium/Hard ceza eski; (3) sim event'leri hiç modellemiyor; (4) sim deterministik (RNG yok) — "MC" sonuçları varyans içermez (bkz. `.claude/agent-memory/economist/a1_determinism_audit_2026-09-18.md`); (5) sim müşteri sabrı/kaçışını modellemiyor (kayıplar alt sınır).

## M. Eski raporlara göre ne değişmiş (2026-07 → bugün)

| Konu | 2026-07 (ECONOMY_BALANCE_REPORT / UPGRADE_PRICING v3.2) | Bugün |
|---|---|---|
| Kira | 500/900/1200/1500, g=1.3 (→1.15 önerisi), +%10 wealthTax | 290/650/1140/1630, g=1.20, vergi yok |
| Başlangıç parası | 100/500 çelişkisi, ×0.85/oyuncu | 500 × 1.2^(P−1) |
| Prestij ölçeği | start 5→15, ceza −2/−1.5 (240-skala) | 100-skala: start 12, ceza −0.4, servis +0.4 |
| Müşteri talebi | raf×3 + seviye×2 + rand, max 50 | PlateUp gün tablosu 4-13 |
| Ödül | 50 flat (+5/10 prestij) | P-bazlı 50/55/70/88 (+5/8 prestij) |
| Perk fiyatları | örn. Prestij Simsarı 510/505, Kelle Koltukta 800, Ucuz Kira 130/160/190 | 130/145, 320, 130/160/190 (çoğu ucuzladı) |
| Telefon | çalan telefon %30/saat, +20 TL | dışarı arama, zaman bedeli, 0 TL |
| Sonuç iddiası | 1P/2P gün 8-12 iflas | economist R12: 16/16 hücre hayatta; A1: neredeyse kaybedilemez |

## N. Açık sorular / varsayım adayları (Aşama 2'de parametrik bırakılacak)

1. Tır giriş/çıkış animasyon süresi — BULUNAMADI (klip). Varsayım: 6 s toplam (sim.js ile aynı).
2. İnsan süreleri: ürün al → paketle → kutuyu rafa/tıra taşı (kodda zamanlı kapı yok). Varsayım profilleri Aşama 2'de.
3. ~~Servis seri kısıtı / `GetCustomerCapacity`~~ — ÇÖZÜLDÜ: tek istasyon fiilen (§C), kapasite formülü ölü (§K).
4. Oyuncuların telefon kullanım oranı ve draft'ta hangi kartı seçtiği — strateji profilleri (Aşama 3).
5. Hata oranları (yanlış ürün / yanlış teslim / düşürme) — profil bazlı varsayım.
