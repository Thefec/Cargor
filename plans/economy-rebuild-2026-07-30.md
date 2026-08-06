# Cargor Ekonomi Yeniden Kurulumu — FAZ 1 / 3: TEMEL

**Tarih:** 2026-07-30
**Dal:** `feature/economy-balance-round`
**Kapsam:** Değer envanteri + verim (throughput) modeli + 1P/2P/3P/4P gelir tabanı.
**Kapsam DIŞI:** Prestij eğrisi / event / kira ayarı (FAZ 2), upgrade + quest fiyatlandırma (FAZ 3).
**Yöntem:** Her sayı 2026-07-30'da canlı `.cs` / `.asset` / `.prefab` / `.unity` dosyasından yeniden okundu.
Eski rapor ve hafıza dosyaları yalnızca "nereye bakmalıyım" ipucu olarak kullanıldı, veri olarak kabul edilmedi.
**Sim:** `tools/economy-sim/sim.js` v3 olarak sıfırdan yazıldı (`node tools/economy-sim/sim.js`).

---

## §1 DEĞER ENVANTERİ

### 1.1 Kira (GameEconomySettings + EkonomiAyarlari.asset)

| Değer | Canlı sayı | Kaynak | P-bazlı? |
|---|---|---|---|
| `baseRentByPlayerCount` | 500 / 900 / 1200 / 1500 | `EkonomiAyarlari.asset:15` (hex `f4010000 84030000 b0040000 dc050000`) | **EVET** |
| `rentGrowthMultiplier` | 1.15 | `EkonomiAyarlari.asset:16` | hayır |
| `rentIntervalDays` | 4 | `EkonomiAyarlari.asset:18` | hayır |
| `gracePaymentPercent` | 0.8 | `EkonomiAyarlari.asset:19` | hayır |
| `rentScaledMultiplier` | 1.0 (perk yoksa) | `EkonomiAyarlari.asset:20` | hayır |
| Kira formülü | `baseRent × 1.15^cycle × rentScaledMultiplier` | `GameEconomySettings.cs:158-163` | — |
| Kira kapısı | `currentDay % 4 == 0` | `DayCycleManager.cs:519` | hayır |
| Grace: 1 kez, `%80` alınır, ödenmiş sayılır | — | `DayCycleManager.cs:537-546` | hayır |
| 2. ödeyememe → Game Over | — | `DayCycleManager.cs:559-570` | hayır |

Kira serisi (Python/Node ile hesaplandı, dönem 0-3):

| P | gün 4 | gün 8 | gün 12 | gün 16 | 16-gün toplam |
|---|---|---|---|---|---|
| 1 | 500 | 575 | 661 | 760 | **2 496** |
| 2 | 900 | 1035 | 1190 | 1369 | **4 494** |
| 3 | 1200 | 1380 | 1587 | 1825 | **5 992** |
| 4 | 1500 | 1725 | 1984 | 2281 | **7 490** |

### 1.2 Tır / teslimat

| Değer | Canlı sayı | Kaynak | P-bazlı? |
|---|---|---|---|
| `rewardPerBox` | **50 TL** | `EkonomiAyarlari.asset:21` | hayır |
| `penaltyPerBox` (yanlış RENK teslimi) | 40 TL | `EkonomiAyarlari.asset:22` | hayır |
| `prestigePerBonus` | 4 | `EkonomiAyarlari.asset:25` | hayır |
| `bonusPerTier` | 5 TL | `EkonomiAyarlari.asset:26` | hayır |
| Kutu başı ödül formülü | `50 + floor(prestij/4)×5` | `Truck.cs:610-645` | hayır |
| `hangarStayDurationByPlayerCount` | **90 / 60 / 40 / 30 sn** | `EkonomiAyarlari.asset:24` (hex `5a 3c 28 1e`) | **EVET** |
| `hangarStayDuration` (legacy skaler) | 30 sn — dizi boş/null ise fallback | `EkonomiAyarlari.asset:23`, `GameEconomySettings.cs:147-153` | hayır |
| `exitDelay` | 5 sn | `Truck.prefab:196` | hayır |
| `respawnDelayRange` | 3–5 sn (ort 4) | `The Main Office.unity:36776` | hayır |
| Kargo miktarı | `Random.Range(2, 6)` int → **{2,3,4,5}, ort 3.5** | `TruckSpawner.cs:37-38, 517` | hayır |
| Tır rengi | 3-renk torbası (Yellow/Blue/Red), tekrarsız çekiliş | `TruckSpawner.cs:537-560` | hayır |
| Tır çalışma saatleri | **08:00 – 17:00** (9 oyun-saati) | `The Main Office.unity:36778-36779` | hayır |
| Hangar slotu sayısı | 3 (`requiredUpgradeLevel` 0 / 1 / 2) | `The Main Office.unity:36763-36775` | hayır |
| Başlangıçta aktif hangar | **1** (level 0) | `TruckSpawner.cs:389-402` | hayır |
| `rewardVolatility` | 0 (perk kapalı) | `EkonomiAyarlari.asset:27` | hayır |
| Tır çıkışı | "dolunca **VEYA** süre bitince" | `Truck.cs:372-407, 708-716` | — |

### 1.3 Gün döngüsü

| Değer | Canlı sayı | Kaynak | P-bazlı? |
|---|---|---|---|
| `MAX_DAYS` | 16 | `DayCycleManager.cs:35` | hayır |
| `realDurationInSeconds` | **200 sn** (sahne) | `The Main Office.unity:15995` — `.cs:50` default 160 (bkz. AYRIŞMA A2) | hayır |
| `dailyDurationIncrease` | 10 sn/gün | `The Main Office.unity:15996` | hayır |
| `DYNAMIC_DURATION_START_DAY` | 3 | `DayCycleManager.cs:36` | hayır |
| `startHour` / `endHour` | **07:00 / 18:00** (11 oyun-saati) | `The Main Office.unity:15997-15998` | hayır |
| Gün süresi formülü | `gün≤3 → 200`; aksi `200 + (gün-3)×10` | `DayCycleManager.cs:183-197` | — |

Gün takvimi (sim §0):

| gün | gerçek sn | sn / oyun-saati | tır penceresi (sn) | müşteri penceresi (sn) |
|---|---|---|---|---|
| 1–3 | 200 | 18.2 | 163.6 | 163.6 |
| 4 | 210 | 19.1 | 171.8 | 171.8 |
| 8 | 250 | 22.7 | 204.5 | 204.5 |
| 12 | 290 | 26.4 | 237.3 | 237.3 |
| 16 | 330 | 30.0 | 270.0 | 270.0 |

**16 günün toplam gerçek süresi = 4 110 sn ≈ 68.5 dakika oynanış.**

### 1.4 Müşteri / prestij

| Değer | Canlı sayı | Kaynak | P-bazlı? |
|---|---|---|---|
| Talep formülü | `(interactables×2 + storeLevel×2 + rand(-2..3)) × eventMult × playerMult` | `CustomerManager.cs:391-417` | **EVET** (playerMult) |
| `_shelfMultiplier` / `_levelMultiplier` | 2 / 2 | `The Main Office.unity:68602-68603` | hayır |
| `_minVariance` / `_maxVariance` | −2 / +3 (ort +0.5) | `The Main Office.unity:68605-68606` | hayır |
| `_minCustomersPerDay` / `_maxCustomersPerDay` | 1 / 50 | `The Main Office.unity:68607-68608` | hayır |
| `playerCountMultiplier` | **1.0 / 1.3 / 1.6 / 1.9** = `1+(P-1)×0.3` | `DifficultyManager.cs:430` | **EVET** |
| `maxQueueSize` | **2** (sahne) | `The Main Office.unity:68600` — `.cs` default 3 (AYRIŞMA A3) | hayır |
| Wave `maxCustomers` (saat dilimi) | 4 / 6 / 2 / 3 / 4 / 2 | `WaveSettings.cs:17-22` | hayır |
| Wave `spawnRateMultiplier` | 1.0 / 1.5 / 0.5 / 0.8 / 1.3 / 0.6 | `WaveSettings.cs:17-22` | hayır |
| Müşteri spawn saatleri | 08:00 – 17:00 | `The Main Office.unity:68617-68618` | hayır |
| Müşteri çıkış saati | 17:30 | `CustomerManager.cs:23` | hayır |
| **Müşteri sabrı** | **15–20 sn (ort 17.5)** | `Customer.prefab:2305-2306` | **HAYIR** (AYRIŞMA A4) |
| `interactionTime` | 2 sn | `Customer.prefab:2307` | hayır |
| Servis kuyruğu | **SERİ** — yalnız `IsFirstInQueue` servis edilir | `CustomerAI.cs:582` | — |
| Kuyruk dolu → spawn **atlanır** (cezasız) | — | `CustomerManager.cs:516` | — |
| `customerServedPrestigeBonus` | +0.2 | `EkonomiAyarlari.asset:36` | hayır |
| `customerLostPrestigePenalty` | **−0.6** | `EkonomiAyarlari.asset:35` | hayır |
| `wrongProductPrestigePenalty` | −0.04 | `EkonomiAyarlari.asset:37` | hayır |
| `wrongDeliveryPrestigePenalty` | −0.08 | `EkonomiAyarlari.asset:39` | hayır |
| `boxDropPrestigePenalty` | −0.02 | `EkonomiAyarlari.asset:38` | hayır |
| `boxDropMoneyPenalty` | 5 TL | `EkonomiAyarlari.asset:29` | hayır |
| `startingPrestige` | 6 | `The Main Office.unity:25234` | hayır |
| `maxPrestige` | 100 | `The Main Office.unity:25235` | hayır |
| `prestigePerCustomer` | 4 | `The Main Office.unity:25237` | hayır |
| `baseCustomerCapacity` / `maxCustomerCapacity` | 1 / 20 | `The Main Office.unity:25238-25239` | hayır |
| Prestij ≤ 0 (clamp öncesi) → Game Over | — | `PrestigeManager.cs:154-157` | — |

### 1.5 Telefon

| Değer | Canlı sayı | Kaynak | P-bazlı? |
|---|---|---|---|
| `phoneRingChancePerHour` | **0.30** | `EkonomiAyarlari.asset:30` | **HAYIR** (AYRIŞMA A5) |
| `phoneRingEventMultiplier` | 1.5 (CUSTOMER SUPPORT) | `EkonomiAyarlari.asset:31` | hayır |
| Çalma olasılığı formülü | `clamp(base×mult + perkBonus, 0, 0.65)` | `PhoneCallManager.cs:264-271` | hayır |
| `callMoneyReward` | 20 TL | `EkonomiAyarlari.asset:33` | hayır |
| `callPrestigeReward` | 0.2 | `EkonomiAyarlari.asset:34` | hayır |
| `ringDuration` | 25 **gerçek** sn | `The Main Office.unity:14158` | hayır |
| Telefon saatleri | 08:00 – 18:00 → **10 saatlik zar/gün** | `The Main Office.unity:14156-14157`, `PhoneCallManager.cs:245-250` | hayır |

Türev: **beklenen 3.0 çalma/gün**, 25 sn × 3 = **75 sn/gün ekranda çalıyor** (gün 1'de günün %37.5'i).
Tam yanıtla 60 TL + 0.6 prestij/gün; sim `strict` %50 → 30 TL, `optimistic` %85 → 51 TL.

### 1.6 Etkinlikler (Event)

| Değer | Canlı sayı | Kaynak |
|---|---|---|
| Havuz büyüklüğü | 16 event | `EventCalendarUI.cs:160-177` |
| Event'siz ilk günler | 3 (event'ler gün 4'ten başlar) | `EventCalendarUI.cs:25, 674` |
| Event aralığı | `rng.Next(1, 4)` → 1–3 gün | `EventCalendarUI.cs:23-24, 683` |
| Kira günleri event almaz | `currentDay % 4 == 0 → continue` | `EventCalendarUI.cs:688` |
| İlk 2 event garanti POZİTİF, 3. garanti NEGATİF | — | `EventCalendarUI.cs:701-714` |
| FESTIVAL DAY bonusu | **kiranın %10–20'si** (sabit TL değil!) | `EventEffectManager.cs:404-408` |
| `festivalBonusMin/Max` (100/300) | yalnız DayCycleManager erişilemezse **fallback** | `EventEffectManager.cs:411-415` |
| DELIVERY BONUS | `rewardPerBox ×1.2` | `EventEffectManager.cs:148` |
| GOLDEN BOX DAY | ödül ×1.3, müşteri ×1.2, exitDelay ×0.8, hız ×1.2, stamina ×0.8 | `EventEffectManager.cs:230-242` |
| MARKETING DAY | ödül **×0.7**, müşteri ×1.2 | `EventEffectManager.cs:300-312` |
| BUSY DAY | müşteri ×1.3 | `EventEffectManager.cs:140` |
| RAINY DAY | müşteri ×0.8 | `EventEffectManager.cs:294` |
| RELAXED DAY | sabır ×1.3 **+ müşteri ×0.7 (açıklamada YOK)** | `EventEffectManager.cs:178-182` |
| ANGRY CUSTOMERS | sabır ×0.7 | `EventEffectManager.cs:164` |
| SLOW LOGISTICS / EXPRESS CARGO | `exitDelay` ×1.5 / ×0.7 | `EventEffectManager.cs:191, 205` |
| SURPRISE AUDIT | tüm cezalar ×2 (isim-tabanlı) | `EventEffectManager.cs:314-327` |
| OPPORTUNITY DAY | upgrade maliyeti ×0.8 | `EventEffectManager.cs:255` |
| HEAVY BOXES / FATIGUE PROBLEM | hız ×0.8 / stamina ×0.6, sprint ×0.7, müşteri ×0.8 | `EventEffectManager.cs:221-222, 264-266` |

Beklenen sıklık: gün 4-16 aralığında ortalama 2 günde bir aday, kira günleri düşünce **~5 event / 16 gün**.

### 1.7 Oyuncu sayısı ölçekleme (DifficultyManager)

| Değer | Canlı sayı (prefab) | Kaynak | Fiilen etkili? |
|---|---|---|---|
| `baseStartingMoney` | 500 | `DifficultyManager.prefab:75` | EVET |
| `moneyMultiplierPerPlayer` | **1.0** → başlangıç parası P'den bağımsız 500 TL | `DifficultyManager.prefab:81` | EVET |
| `upgradeCostMultiplierPerPlayer` | 1.15 → 1.00 / 1.15 / 1.32 / 1.52 | `DifficultyManager.prefab:85` | EVET (FAZ 3 girdisi) |
| `playerCountMultiplier` (müşteri) | 1.0 / 1.3 / 1.6 / 1.9 | `DifficultyManager.cs:430` | EVET |
| `baseCustomerCount` 10 + `customerCountPerPlayer` 2 | 10/12/14/16 | `DifficultyManager.prefab:74,80` | **HAYIR** — hiçbir yere yazılmıyor (A6) |
| `basePhoneCallChance` 0.2 + `phoneChancePerPlayer` 0.1 | 0.2/0.3/0.4/0.5 | `DifficultyManager.prefab:76,82` | **HAYIR** — `SetCallChance` gövdesi boş (A5) |
| `baseMinPatience` 8 / `baseMaxPatience` 14 / `patienceReductionPerPlayer` 2 | — | `DifficultyManager.prefab:77,78,83` | **HAYIR** — prefab müşteriye ulaşmıyor (A4) |
| `staminaDrainMultiplierPerPlayer` | 1.1 | `DifficultyManager.prefab:84` | stamina (ekonomi dışı) |

### 1.8 Quest (30 canlı asset)

| Değer | Canlı sayı | Kaynak |
|---|---|---|
| Asset sayısı | **30** — Easy 11 / Medium 10 / Hard 9 | `Assets/Resources/Quests/*.asset` |
| Ödül modeli | 4 SABİT alan (`moneyReward` / `prestigeReward` / `moneyPenalty` / `prestigePenalty`) | `QuestData.cs:48-58` |
| Havuz/rastgele seçim | **KALDIRILDI** (`RerollSelection` artık no-op tazeleme) | `QuestData.cs:205-222` |
| Günde teklif | 3 | `QuestManager.cs:17` |
| Günde kabul | **1** | `QuestManager.cs:691-696` |
| Tier kapısı | `_currentQuestTier` başlangıç **0** → yalnız Easy; "Görev Kademesi" upgrade'i açar | `QuestManager.cs:70, 471-485`, `UpgradePanel.cs:783` |
| Ödül/ceza zamanı | **gün sonunda otomatik** — "Topla" adımı yok | `QuestManager.cs:757-788` |
| **Ödemenin gerçek anı** | `DayCycleManager.OnNewDay` → yani **BİR SONRAKİ günün başında**, o günün kira kontrolünden SONRA | `QuestManager.cs:356-365` + `DayCycleManager.cs:483-489` |
| Renk kilidi ilerleme oranı | tam **1/3** (3-renk torbası) | `TruckSpawner.cs:537-560`, `CustomerManager.cs:988-1011` |
| Buff | 30 asset'in **hepsinde `hasBuff: 0`** — hiç buff yok | asset dump |
| Var olmayan quest tipleri | `CompleteMinigame(0)`, `MakePackagingMistake(5)`, `CompleteSpecificColorTruck(6)` → **0 asset** | asset dump (A12) |

Canlı ödül/ceza tablosu (asset'ten birebir):

| tier | tip | hedef | renk kilidi | para ödül | prestij ödül | para ceza | prestij ceza |
|---|---|---|---|---|---|---|---|
| Easy | Truck | 1 | — | 28 | 0.7 | 15 | 0.4 |
| Easy | Shelf | 4 / 6 | — | 18 / 28 | 0.4 / 0.7 | 10 / 15 | 0.2 / 0.4 |
| Easy | Shelf | 2 | Red/Yellow/Blue | 18 | 0.4 | 10 | 0.2 |
| Easy | Pack | 4 | — | 18 | 0.4 | 10 | 0.2 |
| Easy | Pack | 2 | Toy/Cloth/Glass | 18 | 0.4 | 10 | 0.2 |
| Easy | Phone | 2 | — | 22 | 0.5 | 12 | 0.2 |
| Medium | Truck | 2 | — | 52 | 1.2 | 29 | 0.6 |
| Medium | Shelf/Pack | 7 | — | 34 | 0.8 | 19 | 0.4 |
| Medium | Shelf/Pack | 3 | renk | 34 | 0.8 | 19 | 0.4 |
| Medium | Phone | 3 | — | 40 | 1.0 | 22 | 0.5 |
| Hard | Truck | 3 | — | 86 | 2.3 | 47 | 1.2 |
| Hard | Shelf/Pack | **12** | — | 57 | 1.5 | 31 | 0.8 |
| Hard | Shelf/Pack | 5 | renk | 57 | 1.5 | 31 | 0.8 |
| Hard | Phone | — | — | **YOK** | — | — | — |

### 1.9 Para sistemi

| Değer | Canlı sayı | Kaynak |
|---|---|---|
| `MoneySystem.startingMoney` (sahne) | **50 000** | `The Main Office.unity:4734` — **DEBUG DEĞERİ** (AYRIŞMA A1) |
| `DifficultyManager` başlangıç parası | 500 | `DifficultyManager.prefab:75` |
| Para taban sınırı | `Mathf.Max(0, ...)` → **negatife inmez** | `MoneySystem.cs:91` |

### 1.10 .cs default'u ile canlı asset/sahne değeri ÇELİŞEN alanlar (ayrı liste)

| Alan | `.cs` default | CANLI (asset / sahne / prefab) | Kazanan |
|---|---|---|---|
| `realDurationInSeconds` | 160 | **200** (sahne) | sahne |
| `maxQueueSize` | 3 (`DEFAULT_QUEUE_SIZE`) | **2** (sahne) | sahne |
| `CustomerAI.minWaitTime` / `maxWaitTime` | 10 / 20 | **15 / 20** (prefab) | prefab |
| `CustomerAI.interactionTime` | 5 | **2** (prefab) | prefab |
| `DifficultyManager.baseMinPatience` | 35 | **8** (prefab) | prefab (ama ölü yol) |
| `DifficultyManager.baseMaxPatience` | 55 | **14** (prefab) | prefab (ama ölü yol) |
| `DifficultyManager.customerCountPerPlayer` | 5 | **2** (prefab) | prefab (ama ölü yol) |
| `DifficultyManager.basePhoneCallChance` | 0.3 | **0.2** (prefab) | prefab (ama ölü yol) |
| `DifficultyManager.patienceReductionPerPlayer` | 5 | **2** (prefab) | prefab (ama ölü yol) |
| `Truck.hangarStayDuration` | 120 | Awake'te SO'dan **90/60/40/30** yazılır | SO |
| `Truck.rewardPerBox` / `penaltyPerBox` | 50 / 40 | prefab'da **10 / 2** ama Awake'te SO'dan **50 / 40** yazılır | SO |
| `MoneySystem.startingMoney` | 500 | **50 000** (sahne) | sahne (bkz. A1) |

---

## §2 VERİM (THROUGHPUT) MODELİ

### 2.0 Temel gerçek — kod ile yeniden doğrulandı

**PARA YALNIZ TIRDAN GELİR.**
- `grep -c "AddMoney|ModifyMoney"` → `CustomerAI.cs` = **0**, `CustomerManager.cs` = **0** (`money` kelimesi bile geçmiyor).
- Tek büyük kaynak: `Truck.cs:571-576 ProcessSuccessfulDelivery` → `MoneySystem.AddMoney(CalculateRewardWithPrestige())`.
- Küçük yardımcı kaynaklar: telefon (`callMoneyReward` 20 TL), FESTIVAL DAY (kira %10-20), quest ödülü.
- Müşteri servisinin ekonomik rolü: **yalnız prestij** → prestij → kutu-başı ödül tier'ı → **dolaylı** para.

Sonuç: `gelir ≈ (tıra teslim edilen doğru kutu) × (50 + floor(prestij/4)×5)`. Gün 8, 2P optimistic'te
tırın gelir payı **%90** (1P %84 → 4P %95); telefon P'den bağımsız 51 TL olduğu için tek oyuncuda
görece daha büyük bir yardım (gelirin %13'ü), 4 oyuncuda ihmal edilebilir (%4).

### 2.1 Zaman zinciri (hepsi koddan)

```
gün süresi D(gün) = 200                        , gün ≤ 3          [unity:15995]
              = 200 + (gün-3)×10               , gün > 3          [cs:183-197]
1 oyun-saati  = D / (18-7) = D / 11 gerçek sn
tır penceresi = D × (17-8)/11 = D × 9/11                          [unity:36778-36779]
müşteri penc. = D × (17-8)/11 = D × 9/11                          [unity:68617-68618]
telefon zarı  = 18-8 = 10 zar/gün, her biri %30                   [cs:245-250]
```

### 2.2 Tır / hangar devir döngüsü

```
prodRate      = kutuDk/oyuncu × (P × emekPayı) / 60          [kutu/sn]
fillRate      = prodRate                       (STRICT)
              = prodRate × handoverSpeedup(3)  (OPTIMISTIC, ön-stok var)
her kargo c ∈ {2,3,4,5} için:
    fillTime  = c / fillRate
    bekleme   = min(hangarStay(P), fillTime)         ← "dolunca VEYA süre bitince"
    teslim    = min(c, fillRate × hangarStay(P))
devirSüresi   = ort(bekleme) + ÖLÜ_SÜRE
tır/gün       = tırPenceresi / devirSüresi × hangarSayısı
kutu/gün      = min( tır/gün × ort(teslim) , ÜRETİM_TAVANI )
ÜRETİM_TAVANI = prodRate × D            (OPTIMISTIC, tüm gün ön-stok)
              = prodRate × tırPenceresi (STRICT, ön-stok yok)
```

| Girdi | Kaynak |
|---|---|
| `hangarStay(P)` = 90/60/40/30 | **KOD** (`EkonomiAyarlari.asset:24`) |
| `ÖLÜ_SÜRE` = 5 (exitDelay) + 4 (ort respawn) = **9 sn** | **KOD** (`Truck.prefab:196`, `unity:36776`) |
| Animasyon tamponu **+6 sn** → toplam ölü süre 15 sn | **VARSAYIM** (animator klip süresi kodda sayısallaşmıyor) |
| kargo {2,3,4,5} ort 3.5 | **KOD** (`TruckSpawner.cs:37-38,517`) |
| `kutuDk/oyuncu` = 2.0 (Normal) / 1.2 (Yavaş) / 3.0 (Hızlı) | **VARSAYIM** — kodda zamanlı üretim kapısı YOK |
| `handoverSpeedup` = 3.0 | **VARSAYIM** |
| `emekPayı(tır)` = 0.6 (STRICT), 1.0 (OPTIMISTIC) | **VARSAYIM** |

### 2.3 SORU: "Tır penceresi cap — günde kaç tır?" — CEVAP

**Mekanik tavan (doldurma anında olsaydı, 1 hangar):** `tırPenceresi / 15 sn`
= gün 1: **10.9 tır/gün** · gün 8: **13.6** · gün 16: **18.0**. (2 hangar → ×2, 3 hangar → ×3.)

**Fiilen ulaşılan tır/gün (1 hangar, Normal senaryo):**

| P | hangarStay | bant | gün 1 | gün 8 | gün 16 | kutu/gün (gün 8) |
|---|---|---|---|---|---|---|
| 1 | 90 sn | STRICT | 1.56 | 1.95 | 2.57 | 3.5 |
| 1 | 90 sn | OPTIMISTIC | 1.14 | 1.43 | 1.89 | 5.0 |
| 2 | 60 sn | STRICT | 2.26 | 2.82 | 3.72 | 6.5 |
| 2 | 60 sn | OPTIMISTIC | 2.29 | 2.86 | 3.77 | 10.0 |
| 3 | 40 sn | STRICT | 3.07 | 3.84 | 5.06 | 8.8 |
| 3 | 40 sn | OPTIMISTIC | 3.43 | 4.29 | 5.66 | 15.0 |
| 4 | 30 sn | STRICT | 3.74 | 4.68 | 6.17 | 10.8 |
| 4 | 30 sn | OPTIMISTIC | 4.57 | 5.71 | 7.54 | 20.0 |

**Kesin cevap: mekanik tavan (10.9–18 tır/gün) HİÇBİR senaryoda bağlayıcı DEĞİL.**
Fiilen ulaşılan 1.1–7.5 tır/gün, tavanın **%10–%42'si**. Bağlayıcı darboğaz **insan üretim hızı**
(OPTIMISTIC bant) veya **tek tırın dolma süresi** (STRICT bant). Bu, projenin en uzun süredir
açık yapısal sorusunun sayısal cevabı: *hangar penceresi bir tavan değil, bol bir tampon.*

**Doğal sonuç — hangar sayısı (Truck upgrade) neredeyse değersiz** (gün 8, Normal, kutu/gün):

| P | 1 hangar | 2 hangar | 3 hangar | üretim tavanı |
|---|---|---|---|---|
| 1 | STR 3.5 / OPT 5.0 | STR 4.1 / OPT 5.0 | STR 4.1 / OPT 5.0 | 5.0 |
| 2 | STR 6.5 / OPT 10.0 | STR 8.2 / OPT 10.0 | STR 8.2 / OPT 10.0 | 10.0 |
| 3 | STR 8.8 / OPT 15.0 | STR 12.3 / OPT 15.0 | STR 12.3 / OPT 15.0 | 15.0 |
| 4 | STR 10.8 / OPT 20.0 | STR 16.4 / OPT 20.0 | STR 16.4 / OPT 20.0 | 20.0 |

OPTIMISTIC bantta 2. ve 3. hangar **sıfır** gelir katıyor; STRICT bantta 2. hangar +%17–52,
3. hangar **sıfır** (üretim tavanına çarpıyor). → FAZ 3 için: Truck upgrade'in fiyatı 2. hangar
seviyesinde durmalı, 3. hangar tek başına satılabilir bir değer taşımıyor.

### 2.4 Kutu/dakika insan verimi (VARSAYIM katmanı)

Kodda paketleme/kapatma için zamanlı bir kapı **yok** (`Table.cs`'de yalnız `ITEM_SPAWN_DELAY = 0.1s`);
üretim tamamen oyuncu hareketi + etkileşimi. Referans: `PlayerMovement.moveSpeed = 5`,
`sprintSpeed = 7` (`PlayerMovement.cs:32,35`).

Bir günün **yalnızca 200–330 gerçek saniye** olması bu varsayımı çok duyarlı hale getiriyor:
2.0 kutu/dk/oyuncu ile 1 oyuncu tüm gün boyunca yalnız **6.7–11 kutu** üretebiliyor.

| senaryo | kutu/dk/oyuncu | 1P gün 8 üretim | 4P gün 8 üretim |
|---|---|---|---|
| Yavaş | 1.2 | 5.0 kutu/gün | 20.0 kutu/gün |
| Normal | 2.0 | 8.3 kutu/gün | 33.3 kutu/gün |
| Hızlı | 3.0 | 12.5 kutu/gün | 50.0 kutu/gün |

> Yukarıdaki "üretim tavanı" satırları emek payı (0.6/1.0) uygulandıktan sonraki değerlerdir.

### 2.5 Müşteri talebi ve SERİ servis tavanı (v2'ye göre büyük düzeltme)

Kod iki sert kısıt getiriyor:
1. **Servis SERİ**: `CustomerAI.ProcessWaitingInQueue` yalnız `manager.IsFirstInQueue(this)` iken
   `BeginService()` çağırıyor (`CustomerAI.cs:580-586`) → aynı anda TEK müşteri servis edilebilir.
2. **Kuyruk dolu → spawn atlanır**: `CustomerManager.cs:516` (`if (IsQueueFull) return false`).
   Spawn olmayan müşteri **hiç gelmez, ceza üretmez**. Yani talep bir tavan değil, bir HAVUZ.

```
seriTavan   = müşteriPenceresi / servisDöngüSüresi      (mekanik)
emekTavanı  = (P × müşteriEmekPayı) × müşteriPenceresi / servisEmekSüresi
servisEdilen = min(talep, seriTavan, emekTavanı)
spawnOlan    = min(talep, seriTavan + maxQueueSize(2))
kaçan        = spawnOlan − servisEdilen        → her biri −0.6 prestij
hiçGelmeyen  = talep − spawnOlan               → CEZASIZ
```
`servisDöngüSüresi` 18 sn (Normal) ve `servisEmekSüresi` 15 sn (Normal) **VARSAYIM**;
müşteri sabrı 15–20 sn (`Customer.prefab:2305-2306`) bu aralığın üst sınırını veriyor.

Gün 8 sonuçları:

| P | talep | seriTavan | servis edilen | kaçan | hiç gelmeyen | net prestij/gün |
|---|---|---|---|---|---|---|
| 1 | 9 | 11.4 | STR 5.5 / OPT 9.0 | STR 3.5 / OPT 0 | 0 | **STR −1.04** / OPT +1.80 |
| 2 | 11 | 11.4 | STR 10.9 / OPT 11.0 | 0.1 / 0 | 0 | +2.13 / +2.20 |
| 3 | 14 | 11.4 | 11.4 | 2.0 | 0.6 | +1.07 |
| 4 | 16 | 11.4 | 11.4 | 2.0 | 2.6 | +1.07 |

**Kritik bulgu:** `seriTavan ≈ 11.4 müşteri/gün` P'den **bağımsız** (mekanik). Talep 3P'de 14, 4P'de 16'ya
çıktığı için 3P/4P'de her gün ~2 müşteri kaçıyor (−1.2 prestij) ve prestij kazancı 2P'ye göre **daha az**
oluyor. Yani oyuncu ekledikçe prestij kazancı artmıyor, **azalıyor** — ters ölçekleme.
(Sim §6-7: son prestij 1P 47.1 → 2P 47.7 → 3P 42.5 → **4P 37.1**.)

---

## §3 1P/2P/3P/4P GELİR TABANI  ← FAZ 2 + FAZ 3 GİRDİSİ

> ⚠️ **BU BÖLÜM FAZ 4'TE REVİZE EDİLDİ — aşağıdaki sayıları KULLANMA.**
> İki modelleme hatası bulundu ve `sim.js` v3.1'de düzeltildi: (D1) `startingActiveInteractables`
> 3 değil **5** (sahnede 4 aktif `ShelfState` + 1 `DisplayTable`) → talep 1P 9→13, 4P 16→24;
> (D2) **masa çekişmesi** modelde yoktu (sahnede seviye 0'da yalnız 1 `Table` aktif ve tek item taşıyor)
> → aşağıdaki tablo 2P-4P'de %7-13 fazla iyimser.
> **Düzeltilmiş taban:** OPT kümülatif 1P **6 019** / 2P **10 438** / 3P **14 444** / 4P **17 744**,
> gelir ölçeği **1 : 1.73 : 2.40 : 2.95**; 1P STRICT iflası gün 4 → **gün 3** (sebep kira değil prestij).
> Güncel tablo ve nihai değer seti: **`plans/economy-rebuild-2026-07-30-faz4-final.md` §A / §B.**

Koşu ayarları: Normal senaryo, **1 hangar**, quest AÇIK (tier gate = Easy), telefon AÇIK,
event KAPALI, **yeniden yatırım KAPALI** (`upgradeSpendRatio = 0`, FAZ 3 öncesi temiz taban).

### 3.1 Günlük net para (TL/gün)

| P | bant | gün 1 | gün 4 | gün 8 | gün 12 | gün 16 | ort/gün |
|---|---|---|---|---|---|---|---|
| 1 | **OPTIMISTIC** | 224 | 280 | 369 | 497 | 645 | 405 |
| 2 | **OPTIMISTIC** | 398 | 470 | 680 | 931 | 1224 | 734 |
| 3 | **OPTIMISTIC** | 571 | 673 | 919 | 1208 | 1626 | 990 |
| 4 | **OPTIMISTIC** | 745 | 873 | 1202 | 1587 | 2028 | 1276 |
| 1 | **STRICT** | 152 | 145 | *(iflas g4)* | — | — | 147 |
| 2 | **STRICT** | 255 | 301 | 380 | 534 | 715 | 433 |
| 3 | **STRICT** | 336 | 396 | 502 | 666 | 905 | 556 |
| 4 | **STRICT** | 403 | 473 | 603 | 803 | 1034 | 654 |

### 3.2 16 günün kümülatifi ve kira karşılaştırması

| P | bant | kümülatif net (16 gün) | 16-gün kira toplamı | **oran (gelir/kira)** | iflas | son kasa | son prestij |
|---|---|---|---|---|---|---|---|
| 1 | OPTIMISTIC | **6 488** | 2 496 | **2.60** | yok | 4 494 | 47.1 |
| 2 | OPTIMISTIC | **11 736** | 4 494 | **2.61** | yok | 7 742 | 47.7 |
| 3 | OPTIMISTIC | **15 841** | 5 992 | **2.64** | yok | 10 349 | 42.5 |
| 4 | OPTIMISTIC | **20 413** | 7 490 | **2.73** | yok | 13 425 | 37.1 |
| 1 | STRICT | **588** | 2 496 | **0.24** | **GÜN 4** | 587 | 0 |
| 2 | STRICT | **6 933** | 4 494 | **1.54** | yok | 2 938 | 39.3 |
| 3 | STRICT | **8 889** | 5 992 | **1.48** | yok | 3 394 | 37.2 |
| 4 | STRICT | **10 467** | 7 490 | **1.40** | yok | 3 476 | 32.4 |

### 3.3 Ölçekleme sağlığı

```
OPTIMISTIC gelir ölçeği (1P = 1.00):  1.00 / 1.81 / 2.44 / 3.15
KİRA ölçeği            (1P = 1.00):  1.00 / 1.80 / 2.40 / 3.00
```
**Kira ölçeklemesi gelir ölçeklemesiyle neredeyse birebir örtüşüyor** (maks. sapma %5).
`baseRentByPlayerCount = {500, 900, 1200, 1500}` iyi kalibre edilmiş — FAZ 2'de dokunmaya gerek yok.

STRICT bantta gelir ölçeği daha dik (1P kırılgan, 2P'ye geçişte 11.8× artış) → **1P STRICT tek
gerçek iflas riski**. STRICT bantta pay 2P 1.54 → 4P 1.40 doğru yönde (çok oyuncu = daha zor).

### 3.4 Kira baskısı (kira / o günün net geliri; 4.0 = tam dengeli 4-günlük döngü)

| P | bant | gün 4 | gün 8 | gün 12 | gün 16 |
|---|---|---|---|---|---|
| 1 | OPTIMISTIC | 1.76 | 1.55 | 1.33 | 1.18 |
| 2 | OPTIMISTIC | 1.91 | 1.52 | 1.28 | 1.12 |
| 3 | OPTIMISTIC | 1.78 | 1.50 | 1.31 | 1.12 |
| 4 | OPTIMISTIC | 1.72 | 1.44 | 1.25 | 1.12 |
| 1 | STRICT | 3.45 | *(iflas)* | — | — |
| 2 | STRICT | 2.99 | 2.72 | 2.23 | 1.91 |
| 3 | STRICT | 3.03 | 2.75 | 2.38 | 2.02 |
| 4 | STRICT | 3.17 | 2.86 | 2.47 | 2.21 |

**Yorum:** OPTIMISTIC bantta kira 1.1–1.9 günlük gelire denk → 4 günün ~%35-45'i kirayı ödüyor,
gerisi birikiyor. Kira baskısı zamanla **düşüyor** (1.76 → 1.18): gelir eğrisi (`rewardPerBox`
prestij tier'ı + gün uzaması) kiranın 1.15^cycle'ından **daha hızlı** büyüyor. Bu, geç oyunun
gevşediğini gösteriyor — FAZ 2'nin ana konusu.

### 3.5 Yavaş senaryo (1.2 kutu/dk/oyuncu) — kırılganlık sınırı

| P | bant | iflas | ort/gün | kümülatif 16 | son prestij |
|---|---|---|---|---|---|
| 1 | OPTIMISTIC | yok | 187 | 2 994 | 31.6 |
| 2 | OPTIMISTIC | yok | 332 | 5 311 | 31.3 |
| 3 | OPTIMISTIC | yok | 464 | 7 417 | 27.0 |
| 4 | OPTIMISTIC | yok | 593 | 9 480 | 26.4 |
| 1 | STRICT | **GÜN 3** (prestij) | 82 | 247 | 0 |
| 2 | STRICT | **GÜN 8** | 138 | 1 104 | 0 |
| 3 | STRICT | **GÜN 12** | 230 | 2 755 | 15.7 |
| 4 | STRICT | **GÜN 8** | 245 | 1 956 | 11.7 |

Yavaş + STRICT kombinasyonunda 4 senaryonun **4'ü de** kaybediyor. Bunun ana sebebi para değil
**prestij kanaması**: `customerLostPrestigePenalty = −0.6` kaçan müşteri × +0.2 servis edilen
oranıyla net negatif hale geliyor (3:1 asimetri). FAZ 2'nin ikinci ana konusu.

---

## §4 AYRIŞMA LİSTESİ (kod ↔ kod, kod ↔ sim, kod ↔ hafıza)

### KRİTİK

**A1 — `MoneySystem.startingMoney = 50 000` sahnede.**
`The Main Office.unity:4734`. `MoneySystem.OnNetworkSpawn` (`MoneySystem.cs:45-47`) bu değeri
`_currentMoney`'e yazıyor; `DifficultyManager.ApplyMoneySettings` (`cs:448-471`) sonra
`SetMoney(500)` ile düzeltmeye çalışıyor ama **yalnızca `HasGameEverStarted == false` ise**.
İki sistem arasında sıra garantisi yok. Bu bir debug/test kalıntısı; ürüne bu değerle çıkarsa
tüm ekonomi anlamsızlaşır. **FAZ 2 öncesi doğrulanmalı.**

**A4 — P-bazlı müşteri sabrı tamamen ÖLÜ.**
`DifficultyManager.ApplyCustomerSettings` (`cs:436-443`) `FindObjectsOfType<CustomerAI>()` ile
sahnedeki örnekleri yamalıyor. Sahnede `CustomerAI` **yok** (`grep minWaitTime "The Main Office.unity"`
= 0 sonuç); müşteriler `CustomerManager.SpawnCustomer` içinde prefab'tan Instantiate ediliyor
(`cs:680`). Yani `baseMinPatience 8 / baseMaxPatience 14 / patienceReductionPerPlayer 2` hiç
uygulanmıyor. **Canlı sabır sabit 15–20 sn ve oyuncu sayısından bağımsız.**

**A5 — `PhoneCallManager.SetCallChance` gövdesi BOŞ.**
`PhoneCallManager.cs:425`: `public void SetCallChance(float newChance) { }`.
`DifficultyManager.ApplyPhoneSettings` (`cs:474-481`) bunu çağırıyor ve log basıyor → sahte yeşil.
**Telefon şansı P'den bağımsız sabit 0.30.**

### ÖNEMLİ

**A2 — `realDurationInSeconds`:** `.cs:50` default 160, sahne 200. Sahne kazanır.
**v2 sim 160 kullanıyordu → tüm eski verim hesapları %25 düşük.** v3'te düzeltildi.

**A3 — `maxQueueSize`:** `.cs:57` default 3 (`DEFAULT_QUEUE_SIZE`), sahne **2**. Sahne kazanır.
Seri servis + 2'lik kuyruk = prestij tavanının gerçek kaynağı.

**A6 — `ScaledCustomerCount` ölü.** `DifficultyManager.CalculateScaledCustomerCount` (`cs:299-303`,
10 + 2/oyuncu) hiçbir sisteme yazılmıyor — yalnız `GetDifficultyInfo` log/UI (`cs:558`).
Gerçek talep `CustomerManager.CalculateTodaysCustomerCount` × `playerCountMultiplier`.

**A9 — `Truck.prefab` gizli düşük ödül.** `Truck.prefab:197-198` `rewardPerBox: 10`,
`penaltyPerBox: 2`. `OnNetworkSpawn` `Resources.Load<GameEconomySettings>("EkonomiAyarlari")`
başarılıysa 50/40 ile eziyor (`Truck.cs:206-218`). **Resources yüklemesi başarısız olursa
kutu başı ödül 10 TL'ye düşer** — latent, sessiz %80 gelir kaybı riski.

**A10 — Event tip etiketleri ile etkiler tutarsız.**
- `RAINY DAY` → `EventType.Positive` (`EventCalendarUI.cs:174`) ama etkisi müşteri ×0.8
  (`EventEffectManager.cs:294`) = prestij geliri −%20. Pozitif değil.
- `RELAXED DAY` → açıklaması yalnız "sabır +%30" diyor ama kodda ayrıca
  `dailyCustomerMultiplier = 0.7` var (`EventEffectManager.cs:182`) = **gizli %30 müşteri kesintisi**.
  Oyuncuya söylenmeyen bir ceza.

**A14 — Medium/Hard quest'lerin 19'u upgrade arkasında.** `_currentQuestTier` başlangıç 0
(`QuestManager.cs:70`); yalnız "Görev Kademesi" upgrade'i `SetQuestTier` çağırıyor
(`UpgradePanel.cs:783`). Upgrade alınmazsa 30 asset'in **11'i** oynanabilir. FAZ 3 girdisi.

**A16 — Gün 16'da kabul edilen quest ASLA kapanmıyor.** Settlement `DayCycleManager.OnNewDay`'e
bağlı (`QuestManager.cs:356`); gün 16 `MAX_DAYS` olduğu için gün 17 geçişi yok. Ne ödül ne ceza →
son gün quest kabul etmek **bedava opsiyon** (ceza riski sıfır). Küçük ama gerçek bir exploit.

### HAFIZA DÜZELTMELERİ (kendi kayıtlarım bayat çıktı)

**A11 — `missing_events_g9.md` "FESTIVAL DAY hâlâ sabit TL" diyor → YANLIŞ.**
`EventEffectManager.cs:404-408` artık `Random.Range(currentRent × 0.10, currentRent × 0.20)`
kullanıyor. `festivalBonusMin/Max` (100/300) yalnızca `DayCycleManager.Instance == null` fallback'i.
FAZ 2 P2 maddesi **kapandı**.

**A12 — `quest_answerphone_colortruck_2026-07-25.md` "CompleteSpecificColorTruck bağlandı, target 1/2/3"
diyor → canlı asset setinde YOK.** 30 asset'in hiçbirinde `requireSpecificTruckColor: 1` yok
(hepsi 0). `QuestType 6` için **0 asset** var. Aynı şekilde `CompleteMinigame(0)` ve
`MakePackagingMistake(5)` için de 0 asset.

**A13 — `quest_fixed_reward_table_2026-07-28.md` "7 quest/tier, 30 toplam" diyor → dağılım eşit DEĞİL.**
Canlı: Easy **11** / Medium **10** / Hard **9**. Ayrıca **Hard tier'da telefon quest'i YOK**
(Easy ve Medium'da var) — tier ilerledikçe quest tipi çeşitliliği azalıyor.

**A17 — Dosya adı/ID ile `targetCount` çelişiyor.** `Q_Hard_2_Shelf.asset` id'si `hard_shelf_10`
ama `targetCount: 12`; `Q_Hard_4_Pack.asset` id'si `hard_pack_10` ama `targetCount: 12`
(2026-07-29 retune uygulanmış, isim güncellenmemiş). Gelecekte ID'ye güvenip sayı okuma.

### ZARARSIZ

**A8 — `EkonomiAyarlari.asset:17` `wealthTaxRate: 0.1` öksüz.** `GameEconomySettings.cs`'de karşılık
gelen alan yok (9d2c3b0'da kaldırıldı). Unity'nin kullanılmayan serialized alan davranışı, etkisi yok.

**A15 — `DifficultyManager.prefab` ile `.cs` default'ları 5 alanda çelişiyor** (bkz. §1.10).
Hepsi ölü yollara besleniyor, ekonomik etkisi yok — ama `.cs`'yi okuyan biri yanlış sayıyı
gerçek sanabilir.

**A18 — `ApplyCustomerSettings` yorumu yanlış.** `DifficultyManager.cs:429` "1P=1.0, 2P=1.3,
3P=1.6, 4P=2.0" diyor ama formül `1 + (P-1)×0.3` → 4P = **1.9**.

---

## §5 FAZ 2 / FAZ 3 İÇİN DEVİR NOTLARI

**FAZ 2 (prestij / event / kira) için:**
1. `baseRentByPlayerCount` **doğru** — kira ölçeği gelir ölçeğiyle %5 içinde örtüşüyor. Dokunma.
2. Asıl kira sorunu **eğri değil eğim**: kira baskısı 1.76 → 1.18'e *düşüyor* (§3.4). `1.15^cycle`
   gelir büyümesinin altında kalıyor → geç oyun gevşiyor.
3. **Prestij ters ölçekleniyor** (§2.5): seri servis tavanı 11.4 müşteri/gün P'den bağımsız, talep ise
   P ile 1.9×'a çıkıyor → 3P/4P her gün ~2 müşteri kaybediyor ve 2P'den DAHA AZ prestij kazanıyor.
   `customerLostPrestigePenalty = −0.6` vs `+0.2` (3:1 asimetri) bunu ölüm sarmalına çeviriyor.
   Kaldıraçlar: seri servis (kod değişikliği), `maxQueueSize` (2→3), ceza/ödül asimetrisi, talep formülü.
4. Prestij hiçbir senaryoda **tavana (100) ulaşmıyor** (en yüksek son değer 47.7). Tavan artık
   ölü bir güvenlik payı; asıl sorun prestij birikiminin yavaşlığı.
5. Event'lerde 2 tutarsızlık düzeltilmeli (A10): RAINY DAY yanlış tip, RELAXED DAY gizli ceza.

**FAZ 3 (upgrade / quest fiyatlandırma) için:**
1. **3. hangar değersiz** (§2.3): OPTIMISTIC'te 2. ve 3. hangar sıfır gelir; STRICT'te 2. hangar
   +%17-52, 3. hangar sıfır. Truck upgrade'i 2 seviyede bitir veya 3. seviyeye başka bir etki bağla.
2. Fiyat referansı (Normal, 1 hangar, quest+telefon açık, yeniden yatırım yok):
   **1P 405 / 2P 734 / 3P 990 / 4P 1276 TL ortalama günlük net** (OPTIMISTIC),
   **1P 147 / 2P 433 / 3P 556 / 4P 654** (STRICT). `upgradeCostMultiplierPerPlayer = 1.15`
   → maliyet ölçeği 1.00/1.15/1.32/1.52; gelir ölçeği 1.00/1.81/2.44/3.15 → **çok oyuncuda upgrade
   göreli olarak ÇOK ucuz** (gelir 3.15× artarken maliyet yalnız 1.52× artıyor).
3. **Hard tier `targetCount = 12`** shelf/pack quest'leri gün 8'de 2P için %40, 1P için erişilemez
   (üretim 5-10 kutu/gün). Renk-kilitli Hard (hedef 5, /3 = 15 etkin) EV'si **negatif** (−7.6 TL).
   Hard tier fiyatlandırması üretim tavanına göre yeniden yapılmalı.
4. **Görev Kademesi upgrade'i quest içeriğinin %63'ünü açıyor** (19/30 asset) — draft havuzundaki
   en yüksek içerik-kilidi. Fiyatı buna göre.
5. Gün 16 quest'i hiç kapanmıyor (A16) → son gün quest'i risksiz. Küçük ama düzeltilmeli.

**Her iki faz için ortak uyarı:** 1 oyun günü yalnızca **200–330 gerçek saniye**. Tüm 16 günlük
koşu **~68 dakika**. Bu, "kutu/dakika insan verimi" varsayımını ekonominin en duyarlı girdisi
yapıyor: 1.2 → 2.0 kutu/dk (%67 artış) 1P kümülatif gelirini 2 994 → 6 488 TL'ye (%117) çıkarıyor.
**Playtest ile gerçek kutu/dk ölçülmeden FAZ 2/3'te mutlak TL değerleri kesinleştirilmemeli** —
oranlar (kira/gelir payı, upgrade/gelir payı) ölçülene kadar daha güvenilir bir dil.

---

## §6 SİM KULLANIMI

```
node tools/economy-sim/sim.js          # 13 bölümlü tam çıktı
```
Programatik: `require('./tools/economy-sim/sim.js')` →
`runSim(P, {scenario, mode, numHangars, questsEnabled, questTier, phoneEnabled, upgradeSpendRatio})`,
`truckThroughput`, `customerThroughput`, `phoneIncome`, `questCompletionProb`, `SRC`, `ASSUMED`,
`QUEST_ASSETS`.

`SRC` = koddan okunan gerçek değerler (her satırda `dosya:satır`).
`ASSUMED` = kodda karşılığı olmayan insan-verimi varsayımları (ayrı blok, bilinçli olarak izole).
