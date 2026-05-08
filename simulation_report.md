# Cargor — Full Game Design & Balance Report

## 1. Sistem Envanteri

### 1A — Müşteri Spawn & Bekleme
| Parametre | Değer | Dosya | Satır |
|-----------|-------|-------|-------|
| Günlük müşteri sayısı formülü | (activeInteractables * _shelfMultiplier) + (_storeLevel * _levelMultiplier) + randomVariance | CustomerManager.cs | ~195 |
| Müşteri spawn intervali | spawnWindow / todaysTotalCustomers | CustomerManager.cs | ~220 |
| Müşteri sabır süresi (patience/waitTime) | Min 10s, Max 20s | CustomerAI.cs | ~33 |
| Müşteri kaçırma tetikleyicisi | `HandleTimeUp()` (Wait bar expires) | CustomerAI.cs | ~332 |
| Oyun sonu koşulu | Reaching Day 16 without losing customers (TriggerWin) | GameStateManager.cs | ~170 |

### 1B — Raf & Masa Sistemi
| Parametre | Değer | Dosya | Satır |
|-----------|-------|-------|-------|
| Rafa koymadan yere bırakma cezası | 10 Para | BoxFallPenalty.cs | ~10 |
| Masa doluluk kapasitesi | SlotPoints length (based on Upgrade) | DisplayTable.cs | ~46 |
| Rafın oynanışa katkısı | Shelf multipliers increase daily customer count | CustomerManager.cs | ~195 |

### 1C — Araç (Truck) Sistemi
| Parametre | Değer | Dosya | Satır |
|-----------|-------|-------|-------|
| Araç bekleme süresi | exitDelay = 5s (After completion or end of day) | Truck.cs | ~42 |
| Araç kaçırma cezası | penaltyPerBox = 60 (Yanlış teslimat cezası) | Truck.cs | ~56 |
| Araç varış sıklığı/düzeni | respawnDelayRange = (3f, 5f) | TruckSpawner.cs | ~36 |

### 1D — Ekonomi & Upgrade Sistemi
| Parametre | Değer | Dosya | Satır |
|-----------|-------|-------|-------|
| Başlangıç parası | 100000 (Test değeri), 100 (DifficultyManager base) | MoneySystem.cs, DifficultyManager.cs | ~12, ~28 |
| Müşteri başına kazanç | rewardPerBox = 50 | Truck.cs | ~53 |
| Upgrade fiyatları (liste) | MoreCapacity_1: 100, MoreCapacity_2: 200, vb. | UpgradeAssets.cs | ~6 |
| Upgrade etkileri (liste) | StaminaValue, TruckValue, Queue vb. | UpgradePanel.cs | ~166 |
| Mevcut ceza türleri ve miktarları | Düşürme (10), Yanlış teslim (60) | BoxFallPenalty.cs, Truck.cs | ~10, ~56 |

### 1E — Event & Görev Sistemi
| Parametre | Değer | Dosya | Satır |
|-----------|-------|-------|-------|
| Event tetikleme sıklığı | Her gün (1 event per day) | EventEffectManager.cs | ~150 |
| İyi event listesi | DELIVERY BONUS, RELAXED DAY, EXPRESS CARGO, GOLDEN BOX DAY, OPPORTUNITY DAY vb. | EventEffectManager.cs | ~25 |
| Kötü event listesi | INTENSIVE DAY, ANGRY CUSTOMERS, SLOW LOGISTICS, HEAVY BOXES, FATIGUE PROBLEM vb. | EventEffectManager.cs | ~25 |
| Nötr event ve etkisi | VIP SERVICE | EventEffectManager.cs | ~25 |
| Görev ödül/ceza miktarları | Money, Temp/Perm Buffs (Multiplier: 1.0) | QuestManager.cs | ~260 |

### 1F — Difficulty Manager
1P: x1.0, 2P: x1.3, 3P: x1.6, 4P: x2.0 customer multiplier.
Ölçeklenen Parametreler:
- Müşteri Sayısı: `baseCustomerCount + (players-1)*customerCountPerPlayer` (Bypass'lanmış, kapasite tabanlı sistem aktif)
- Başlangıç Parası: `baseStartingMoney * (0.85 ^ (players-1))`
- Phone Call Chance: `baseChance + (players-1)*0.1`
- Minimum Sabır: `10 - (players-1)*2` (Min 5s)
- Maksimum Sabır: `20 - (players-1)*2` (Min 10s)
- Stamina Regen: `/ (1.1 ^ (players-1))`
- Upgrade Cost: `* (1.15 ^ (players-1))`

## 2. 16-Günlük "Sıfır Upgrade" Simülasyonu

### Başlangıç Parası: 500

#### 1 Oyuncu Senaryosu
**Patience:** 10.0s - 20.0s (Ort. 15.0s) | **Start Money:** 500

| Gün | Müşteri Sayısı | Kazanç (est.) | Süre | Ara/Interval | Birikimli Para | Risk Durumu | Durum |
|-----|----------------|---------------|------|--------------|----------------|-------------|-------|
| 1 | 8 | 400 | 160s | 16.0s | 900 | Düşük | ✅ |
| 2 | 8 | 400 | 160s | 16.0s | 1300 | Düşük | ✅ |
| 3 | 8 | 400 | 160s | 16.0s | 1700 | Düşük | ✅ |
| 4 | 8 | 400 | 170s | 17.0s | 2100 | Düşük | ✅ |
| 5 | 8 | 400 | 180s | 18.0s | 2500 | Düşük | ✅ |
| 6 | 8 | 400 | 190s | 19.0s | 2900 | Düşük | ✅ |
| 7 | 8 | 400 | 200s | 20.0s | 3300 | Düşük | ✅ |
| 8 | 8 | 400 | 210s | 21.0s | 3700 | Düşük | ✅ |
| 9 | 8 | 400 | 220s | 22.0s | 4100 | Düşük | ✅ |
| 10 | 8 | 400 | 230s | 23.0s | 4500 | Düşük | ✅ |
| 11 | 8 | 400 | 240s | 24.0s | 4900 | Düşük | ✅ |
| 12 | 8 | 400 | 250s | 25.0s | 5300 | Düşük | ✅ |
| 13 | 8 | 400 | 260s | 26.0s | 5700 | Düşük | ✅ |
| 14 | 8 | 400 | 270s | 27.0s | 6100 | Düşük | ✅ |
| 15 | 8 | 400 | 280s | 28.0s | 6500 | Düşük | ✅ |
| 16 | 8 | 400 | 290s | 29.0s | 6900 | Düşük | ✅ |

#### 2 Oyuncu Senaryosu
**Patience:** 8.0s - 18.0s (Ort. 13.0s) | **Start Money:** 425

| Gün | Müşteri Sayısı | Kazanç (est.) | Süre | Ara/Interval | Birikimli Para | Risk Durumu | Durum |
|-----|----------------|---------------|------|--------------|----------------|-------------|-------|
| 1 | 11 | 550 | 160s | 11.6s | 975 | Orta | ⚠️ |
| 2 | 11 | 550 | 160s | 11.6s | 1525 | Orta | ⚠️ |
| 3 | 11 | 550 | 160s | 11.6s | 2075 | Orta | ⚠️ |
| 4 | 11 | 550 | 170s | 12.4s | 2625 | Orta | ⚠️ |
| 5 | 11 | 550 | 180s | 13.1s | 3175 | Düşük | ✅ |
| 6 | 11 | 550 | 190s | 13.8s | 3725 | Düşük | ✅ |
| 7 | 11 | 550 | 200s | 14.5s | 4275 | Düşük | ✅ |
| 8 | 11 | 550 | 210s | 15.3s | 4825 | Düşük | ✅ |
| 9 | 11 | 550 | 220s | 16.0s | 5375 | Düşük | ✅ |
| 10 | 11 | 550 | 230s | 16.7s | 5925 | Düşük | ✅ |
| 11 | 11 | 550 | 240s | 17.5s | 6475 | Düşük | ✅ |
| 12 | 11 | 550 | 250s | 18.2s | 7025 | Düşük | ✅ |
| 13 | 11 | 550 | 260s | 18.9s | 7575 | Düşük | ✅ |
| 14 | 11 | 550 | 270s | 19.6s | 8125 | Düşük | ✅ |
| 15 | 11 | 550 | 280s | 20.4s | 8675 | Düşük | ✅ |
| 16 | 11 | 550 | 290s | 21.1s | 9225 | Düşük | ✅ |

#### 3 Oyuncu Senaryosu
**Patience:** 6.0s - 16.0s (Ort. 11.0s) | **Start Money:** 361

| Gün | Müşteri Sayısı | Kazanç (est.) | Süre | Ara/Interval | Birikimli Para | Risk Durumu | Durum |
|-----|----------------|---------------|------|--------------|----------------|-------------|-------|
| 1 | 13 | 650 | 160s | 9.8s | 1011 | Orta | ⚠️ |
| 2 | 13 | 650 | 160s | 9.8s | 1661 | Orta | ⚠️ |
| 3 | 13 | 650 | 160s | 9.8s | 2311 | Orta | ⚠️ |
| 4 | 13 | 650 | 170s | 10.5s | 2961 | Orta | ⚠️ |
| 5 | 13 | 650 | 180s | 11.1s | 3611 | Düşük | ✅ |
| 6 | 13 | 650 | 190s | 11.7s | 4261 | Düşük | ✅ |
| 7 | 13 | 650 | 200s | 12.3s | 4911 | Düşük | ✅ |
| 8 | 13 | 650 | 210s | 12.9s | 5561 | Düşük | ✅ |
| 9 | 13 | 650 | 220s | 13.5s | 6211 | Düşük | ✅ |
| 10 | 13 | 650 | 230s | 14.2s | 6861 | Düşük | ✅ |
| 11 | 13 | 650 | 240s | 14.8s | 7511 | Düşük | ✅ |
| 12 | 13 | 650 | 250s | 15.4s | 8161 | Düşük | ✅ |
| 13 | 13 | 650 | 260s | 16.0s | 8811 | Düşük | ✅ |
| 14 | 13 | 650 | 270s | 16.6s | 9461 | Düşük | ✅ |
| 15 | 13 | 650 | 280s | 17.2s | 10111 | Düşük | ✅ |
| 16 | 13 | 650 | 290s | 17.8s | 10761 | Düşük | ✅ |

#### 4 Oyuncu Senaryosu
**Patience:** 5.0s - 14.0s (Ort. 9.5s) | **Start Money:** 307

| Gün | Müşteri Sayısı | Kazanç (est.) | Süre | Ara/Interval | Birikimli Para | Risk Durumu | Durum |
|-----|----------------|---------------|------|--------------|----------------|-------------|-------|
| 1 | 16 | 800 | 160s | 8.0s | 1107 | Orta | ⚠️ |
| 2 | 16 | 800 | 160s | 8.0s | 1907 | Orta | ⚠️ |
| 3 | 16 | 800 | 160s | 8.0s | 2707 | Orta | ⚠️ |
| 4 | 16 | 800 | 170s | 8.5s | 3507 | Orta | ⚠️ |
| 5 | 16 | 800 | 180s | 9.0s | 4307 | Orta | ⚠️ |
| 6 | 16 | 800 | 190s | 9.5s | 5107 | Düşük | ✅ |
| 7 | 16 | 800 | 200s | 10.0s | 5907 | Düşük | ✅ |
| 8 | 16 | 800 | 210s | 10.5s | 6707 | Düşük | ✅ |
| 9 | 16 | 800 | 220s | 11.0s | 7507 | Düşük | ✅ |
| 10 | 16 | 800 | 230s | 11.5s | 8307 | Düşük | ✅ |
| 11 | 16 | 800 | 240s | 12.0s | 9107 | Düşük | ✅ |
| 12 | 16 | 800 | 250s | 12.5s | 9907 | Düşük | ✅ |
| 13 | 16 | 800 | 260s | 13.0s | 10707 | Düşük | ✅ |
| 14 | 16 | 800 | 270s | 13.5s | 11507 | Düşük | ✅ |
| 15 | 16 | 800 | 280s | 14.0s | 12307 | Düşük | ✅ |
| 16 | 16 | 800 | 290s | 14.5s | 13107 | Düşük | ✅ |

### Başlangıç Parası: 1000

#### 1 Oyuncu Senaryosu
**Patience:** 10.0s - 20.0s (Ort. 15.0s) | **Start Money:** 1000

| Gün | Müşteri Sayısı | Kazanç (est.) | Süre | Ara/Interval | Birikimli Para | Risk Durumu | Durum |
|-----|----------------|---------------|------|--------------|----------------|-------------|-------|
| 1 | 8 | 400 | 160s | 16.0s | 1400 | Düşük | ✅ |
| 2 | 8 | 400 | 160s | 16.0s | 1800 | Düşük | ✅ |
| 3 | 8 | 400 | 160s | 16.0s | 2200 | Düşük | ✅ |
| 4 | 8 | 400 | 170s | 17.0s | 2600 | Düşük | ✅ |
| 5 | 8 | 400 | 180s | 18.0s | 3000 | Düşük | ✅ |
| 6 | 8 | 400 | 190s | 19.0s | 3400 | Düşük | ✅ |
| 7 | 8 | 400 | 200s | 20.0s | 3800 | Düşük | ✅ |
| 8 | 8 | 400 | 210s | 21.0s | 4200 | Düşük | ✅ |
| 9 | 8 | 400 | 220s | 22.0s | 4600 | Düşük | ✅ |
| 10 | 8 | 400 | 230s | 23.0s | 5000 | Düşük | ✅ |
| 11 | 8 | 400 | 240s | 24.0s | 5400 | Düşük | ✅ |
| 12 | 8 | 400 | 250s | 25.0s | 5800 | Düşük | ✅ |
| 13 | 8 | 400 | 260s | 26.0s | 6200 | Düşük | ✅ |
| 14 | 8 | 400 | 270s | 27.0s | 6600 | Düşük | ✅ |
| 15 | 8 | 400 | 280s | 28.0s | 7000 | Düşük | ✅ |
| 16 | 8 | 400 | 290s | 29.0s | 7400 | Düşük | ✅ |

#### 2 Oyuncu Senaryosu
**Patience:** 8.0s - 18.0s (Ort. 13.0s) | **Start Money:** 850

| Gün | Müşteri Sayısı | Kazanç (est.) | Süre | Ara/Interval | Birikimli Para | Risk Durumu | Durum |
|-----|----------------|---------------|------|--------------|----------------|-------------|-------|
| 1 | 11 | 550 | 160s | 11.6s | 1400 | Orta | ⚠️ |
| 2 | 11 | 550 | 160s | 11.6s | 1950 | Orta | ⚠️ |
| 3 | 11 | 550 | 160s | 11.6s | 2500 | Orta | ⚠️ |
| 4 | 11 | 550 | 170s | 12.4s | 3050 | Orta | ⚠️ |
| 5 | 11 | 550 | 180s | 13.1s | 3600 | Düşük | ✅ |
| 6 | 11 | 550 | 190s | 13.8s | 4150 | Düşük | ✅ |
| 7 | 11 | 550 | 200s | 14.5s | 4700 | Düşük | ✅ |
| 8 | 11 | 550 | 210s | 15.3s | 5250 | Düşük | ✅ |
| 9 | 11 | 550 | 220s | 16.0s | 5800 | Düşük | ✅ |
| 10 | 11 | 550 | 230s | 16.7s | 6350 | Düşük | ✅ |
| 11 | 11 | 550 | 240s | 17.5s | 6900 | Düşük | ✅ |
| 12 | 11 | 550 | 250s | 18.2s | 7450 | Düşük | ✅ |
| 13 | 11 | 550 | 260s | 18.9s | 8000 | Düşük | ✅ |
| 14 | 11 | 550 | 270s | 19.6s | 8550 | Düşük | ✅ |
| 15 | 11 | 550 | 280s | 20.4s | 9100 | Düşük | ✅ |
| 16 | 11 | 550 | 290s | 21.1s | 9650 | Düşük | ✅ |

#### 3 Oyuncu Senaryosu
**Patience:** 6.0s - 16.0s (Ort. 11.0s) | **Start Money:** 722

| Gün | Müşteri Sayısı | Kazanç (est.) | Süre | Ara/Interval | Birikimli Para | Risk Durumu | Durum |
|-----|----------------|---------------|------|--------------|----------------|-------------|-------|
| 1 | 13 | 650 | 160s | 9.8s | 1372 | Orta | ⚠️ |
| 2 | 13 | 650 | 160s | 9.8s | 2022 | Orta | ⚠️ |
| 3 | 13 | 650 | 160s | 9.8s | 2672 | Orta | ⚠️ |
| 4 | 13 | 650 | 170s | 10.5s | 3322 | Orta | ⚠️ |
| 5 | 13 | 650 | 180s | 11.1s | 3972 | Düşük | ✅ |
| 6 | 13 | 650 | 190s | 11.7s | 4622 | Düşük | ✅ |
| 7 | 13 | 650 | 200s | 12.3s | 5272 | Düşük | ✅ |
| 8 | 13 | 650 | 210s | 12.9s | 5922 | Düşük | ✅ |
| 9 | 13 | 650 | 220s | 13.5s | 6572 | Düşük | ✅ |
| 10 | 13 | 650 | 230s | 14.2s | 7222 | Düşük | ✅ |
| 11 | 13 | 650 | 240s | 14.8s | 7872 | Düşük | ✅ |
| 12 | 13 | 650 | 250s | 15.4s | 8522 | Düşük | ✅ |
| 13 | 13 | 650 | 260s | 16.0s | 9172 | Düşük | ✅ |
| 14 | 13 | 650 | 270s | 16.6s | 9822 | Düşük | ✅ |
| 15 | 13 | 650 | 280s | 17.2s | 10472 | Düşük | ✅ |
| 16 | 13 | 650 | 290s | 17.8s | 11122 | Düşük | ✅ |

#### 4 Oyuncu Senaryosu
**Patience:** 5.0s - 14.0s (Ort. 9.5s) | **Start Money:** 614

| Gün | Müşteri Sayısı | Kazanç (est.) | Süre | Ara/Interval | Birikimli Para | Risk Durumu | Durum |
|-----|----------------|---------------|------|--------------|----------------|-------------|-------|
| 1 | 16 | 800 | 160s | 8.0s | 1414 | Orta | ⚠️ |
| 2 | 16 | 800 | 160s | 8.0s | 2214 | Orta | ⚠️ |
| 3 | 16 | 800 | 160s | 8.0s | 3014 | Orta | ⚠️ |
| 4 | 16 | 800 | 170s | 8.5s | 3814 | Orta | ⚠️ |
| 5 | 16 | 800 | 180s | 9.0s | 4614 | Orta | ⚠️ |
| 6 | 16 | 800 | 190s | 9.5s | 5414 | Düşük | ✅ |
| 7 | 16 | 800 | 200s | 10.0s | 6214 | Düşük | ✅ |
| 8 | 16 | 800 | 210s | 10.5s | 7014 | Düşük | ✅ |
| 9 | 16 | 800 | 220s | 11.0s | 7814 | Düşük | ✅ |
| 10 | 16 | 800 | 230s | 11.5s | 8614 | Düşük | ✅ |
| 11 | 16 | 800 | 240s | 12.0s | 9414 | Düşük | ✅ |
| 12 | 16 | 800 | 250s | 12.5s | 10214 | Düşük | ✅ |
| 13 | 16 | 800 | 260s | 13.0s | 11014 | Düşük | ✅ |
| 14 | 16 | 800 | 270s | 13.5s | 11814 | Düşük | ✅ |
| 15 | 16 | 800 | 280s | 14.0s | 12614 | Düşük | ✅ |
| 16 | 16 | 800 | 290s | 14.5s | 13414 | Düşük | ✅ |

### Başlangıç Parası: 5000

#### 1 Oyuncu Senaryosu
**Patience:** 10.0s - 20.0s (Ort. 15.0s) | **Start Money:** 5000

| Gün | Müşteri Sayısı | Kazanç (est.) | Süre | Ara/Interval | Birikimli Para | Risk Durumu | Durum |
|-----|----------------|---------------|------|--------------|----------------|-------------|-------|
| 1 | 8 | 400 | 160s | 16.0s | 5400 | Düşük | ✅ |
| 2 | 8 | 400 | 160s | 16.0s | 5800 | Düşük | ✅ |
| 3 | 8 | 400 | 160s | 16.0s | 6200 | Düşük | ✅ |
| 4 | 8 | 400 | 170s | 17.0s | 6600 | Düşük | ✅ |
| 5 | 8 | 400 | 180s | 18.0s | 7000 | Düşük | ✅ |
| 6 | 8 | 400 | 190s | 19.0s | 7400 | Düşük | ✅ |
| 7 | 8 | 400 | 200s | 20.0s | 7800 | Düşük | ✅ |
| 8 | 8 | 400 | 210s | 21.0s | 8200 | Düşük | ✅ |
| 9 | 8 | 400 | 220s | 22.0s | 8600 | Düşük | ✅ |
| 10 | 8 | 400 | 230s | 23.0s | 9000 | Düşük | ✅ |
| 11 | 8 | 400 | 240s | 24.0s | 9400 | Düşük | ✅ |
| 12 | 8 | 400 | 250s | 25.0s | 9800 | Düşük | ✅ |
| 13 | 8 | 400 | 260s | 26.0s | 10200 | Düşük | ✅ |
| 14 | 8 | 400 | 270s | 27.0s | 10600 | Düşük | ✅ |
| 15 | 8 | 400 | 280s | 28.0s | 11000 | Düşük | ✅ |
| 16 | 8 | 400 | 290s | 29.0s | 11400 | Düşük | ✅ |

#### 2 Oyuncu Senaryosu
**Patience:** 8.0s - 18.0s (Ort. 13.0s) | **Start Money:** 4250

| Gün | Müşteri Sayısı | Kazanç (est.) | Süre | Ara/Interval | Birikimli Para | Risk Durumu | Durum |
|-----|----------------|---------------|------|--------------|----------------|-------------|-------|
| 1 | 11 | 550 | 160s | 11.6s | 4800 | Orta | ⚠️ |
| 2 | 11 | 550 | 160s | 11.6s | 5350 | Orta | ⚠️ |
| 3 | 11 | 550 | 160s | 11.6s | 5900 | Orta | ⚠️ |
| 4 | 11 | 550 | 170s | 12.4s | 6450 | Orta | ⚠️ |
| 5 | 11 | 550 | 180s | 13.1s | 7000 | Düşük | ✅ |
| 6 | 11 | 550 | 190s | 13.8s | 7550 | Düşük | ✅ |
| 7 | 11 | 550 | 200s | 14.5s | 8100 | Düşük | ✅ |
| 8 | 11 | 550 | 210s | 15.3s | 8650 | Düşük | ✅ |
| 9 | 11 | 550 | 220s | 16.0s | 9200 | Düşük | ✅ |
| 10 | 11 | 550 | 230s | 16.7s | 9750 | Düşük | ✅ |
| 11 | 11 | 550 | 240s | 17.5s | 10300 | Düşük | ✅ |
| 12 | 11 | 550 | 250s | 18.2s | 10850 | Düşük | ✅ |
| 13 | 11 | 550 | 260s | 18.9s | 11400 | Düşük | ✅ |
| 14 | 11 | 550 | 270s | 19.6s | 11950 | Düşük | ✅ |
| 15 | 11 | 550 | 280s | 20.4s | 12500 | Düşük | ✅ |
| 16 | 11 | 550 | 290s | 21.1s | 13050 | Düşük | ✅ |

#### 3 Oyuncu Senaryosu
**Patience:** 6.0s - 16.0s (Ort. 11.0s) | **Start Money:** 3612

| Gün | Müşteri Sayısı | Kazanç (est.) | Süre | Ara/Interval | Birikimli Para | Risk Durumu | Durum |
|-----|----------------|---------------|------|--------------|----------------|-------------|-------|
| 1 | 13 | 650 | 160s | 9.8s | 4262 | Orta | ⚠️ |
| 2 | 13 | 650 | 160s | 9.8s | 4912 | Orta | ⚠️ |
| 3 | 13 | 650 | 160s | 9.8s | 5562 | Orta | ⚠️ |
| 4 | 13 | 650 | 170s | 10.5s | 6212 | Orta | ⚠️ |
| 5 | 13 | 650 | 180s | 11.1s | 6862 | Düşük | ✅ |
| 6 | 13 | 650 | 190s | 11.7s | 7512 | Düşük | ✅ |
| 7 | 13 | 650 | 200s | 12.3s | 8162 | Düşük | ✅ |
| 8 | 13 | 650 | 210s | 12.9s | 8812 | Düşük | ✅ |
| 9 | 13 | 650 | 220s | 13.5s | 9462 | Düşük | ✅ |
| 10 | 13 | 650 | 230s | 14.2s | 10112 | Düşük | ✅ |
| 11 | 13 | 650 | 240s | 14.8s | 10762 | Düşük | ✅ |
| 12 | 13 | 650 | 250s | 15.4s | 11412 | Düşük | ✅ |
| 13 | 13 | 650 | 260s | 16.0s | 12062 | Düşük | ✅ |
| 14 | 13 | 650 | 270s | 16.6s | 12712 | Düşük | ✅ |
| 15 | 13 | 650 | 280s | 17.2s | 13362 | Düşük | ✅ |
| 16 | 13 | 650 | 290s | 17.8s | 14012 | Düşük | ✅ |

#### 4 Oyuncu Senaryosu
**Patience:** 5.0s - 14.0s (Ort. 9.5s) | **Start Money:** 3071

| Gün | Müşteri Sayısı | Kazanç (est.) | Süre | Ara/Interval | Birikimli Para | Risk Durumu | Durum |
|-----|----------------|---------------|------|--------------|----------------|-------------|-------|
| 1 | 16 | 800 | 160s | 8.0s | 3871 | Orta | ⚠️ |
| 2 | 16 | 800 | 160s | 8.0s | 4671 | Orta | ⚠️ |
| 3 | 16 | 800 | 160s | 8.0s | 5471 | Orta | ⚠️ |
| 4 | 16 | 800 | 170s | 8.5s | 6271 | Orta | ⚠️ |
| 5 | 16 | 800 | 180s | 9.0s | 7071 | Orta | ⚠️ |
| 6 | 16 | 800 | 190s | 9.5s | 7871 | Düşük | ✅ |
| 7 | 16 | 800 | 200s | 10.0s | 8671 | Düşük | ✅ |
| 8 | 16 | 800 | 210s | 10.5s | 9471 | Düşük | ✅ |
| 9 | 16 | 800 | 220s | 11.0s | 10271 | Düşük | ✅ |
| 10 | 16 | 800 | 230s | 11.5s | 11071 | Düşük | ✅ |
| 11 | 16 | 800 | 240s | 12.0s | 11871 | Düşük | ✅ |
| 12 | 16 | 800 | 250s | 12.5s | 12671 | Düşük | ✅ |
| 13 | 16 | 800 | 260s | 13.0s | 13471 | Düşük | ✅ |
| 14 | 16 | 800 | 270s | 13.5s | 14271 | Düşük | ✅ |
| 15 | 16 | 800 | 280s | 14.0s | 15071 | Düşük | ✅ |
| 16 | 16 | 800 | 290s | 14.5s | 15871 | Düşük | ✅ |

## 3. Upgrade Değer Analizi

| Upgrade | Hangi Zorluğu Çözer? | 16 Günü Etkiler mi? | Amorti Süresi | Etiket |
|---------|----------------------|---------------------|---------------|--------|
| **Capacity (Raf)** | Müşteri sayısını artırır, gelir potansiyelini yükseltir | Hayır, aksine artan müşteri sayısıyla zaman baskısı yaratır | 2-3 Gün | **Gereksiz/Riskli** (0 Upgrade ile bitirme hedefleniyorsa) |
| **Table Slots** | Müşteri birikmesini önler, queue akışını hızlandırır | Evet, 3-4P'de artan baskıyı azaltır | Dolaylı (Müşteri kaçırmayı önler) | **Zorunlu** (Özellikle MP'de) |
| **Queue Capacity** | Daha fazla bekleme alanı, "Angry Customers" eventinde hayat kurtarır | Evet | Dolaylı | **Faydalı** |
| **Stamina Regen** | Hızlı hareket, taşıma süresini kısaltır | Evet | Dolaylı | **Faydalı** |
| **Truck (Money/Reward)** | Kutu başına geliri artırır | Evet, ekonomi rahatlar | 1-2 Gün (Çok Hızlı) | **Zorunlu/Faydalı** |

> **Sonuç:** Müşteri sayısının Capacity (Raf) sayısına bağlı olması, bu upgrade'i almayı cezalandıran bir mekanik yaratıyor. Kazanma koşulu "hiç müşteri kaçırmama" olduğu için, oyuncunun kapasitesini artırmaması oyunu kazanmasını garantiliyor.

## 4. Raf Sistemi Analizi

**Soru 1: Yere bırakma cezası (-10) caydırıcı mı?**
16 günde ~150 müşteri gelse, maksimum ceza 1500 Para olur. İlk günlerde 500 para ile başlayan bir oyuncu için -10 küçük bir miktar, dolayısıyla yere atıp geçmek stratejik olarak mantıklı (hız > para).

**Soru 2: Raf kullanmanın avantajı var mı?**
Şu anki durumda raflar sadece "interactable" sayısını artırarak müşteri getirisini yükseltiyor. Oyuncu raf almazsa müşteri sayısı az kalır ve oyun çok rahat biter.

**Soru 3: Rafı anlamlı kılmak için ne yapılabilir?**
- **Öneri 1:** Araçlar sadece raftan yüklenmeli. Yere atılan mallar çöp sayılmalı veya Truck scriptinde kabul edilmemeli.
- **Öneri 2:** Yere bırakılan item'lar zamanla değer kaybetmeli (örn: saniyede -1 Para).
- **Öneri 3:** Rafta duran ürünler ekstra Prestige/Para vermeli (Bonus Multiplier).

## 5. Gameplay Loop Analizi

**5A — Döngü Kaç Adım?**
1. Müşteriden al, 2. Masaya bırak, 3. Kutula, 4. Araca veya rafa götür (Toplam: 4 Eylem, ~5-8 Saniye).

**5B — Döngü 16 Gün Yeter mi?**
Şu anki varyasyon (Kutu renkleri, eventler) sınırlı. Özellikle oyuncu upgrade almazsa her gün tamamen aynı geçiyor. Eksik varyasyon: Farklı ağırlıkta ürünler, acil siparişler veya özel müşteriler (örn: düşük sabırlı VIP).

**5C — Çok Oyunculu Dinamik**
4 kişide müşteri baskısı x2'ye çıkarken (DifficultyManager), oyuncuların iş gücü x4 oluyor. Yani oyun 4 kişiyle çok daha kolay hale geliyor.

## 6. Kritik Tasarım Soruları — Yanıtlar

**S1: Upgradeler yapmaya değer mi?**
Capacity upgrade'leri **ZARARLI**. Müşteri sayısını artırarak kaybetme riskini yükseltiyor. Stamina ve Queue faydalı.

**S2: Raflar anlamlı bir mekanik mi?**
Hayır. Ceza ödeyip yere atmak daha karlı.

**S3: 16 günlük koşul gergin mi?**
Oyuncu raf almazsa müşteri gelmiyor (her gün ~8 müşteri). Hiçbir risk yok. Oyuncu raf alırsa risk aniden yükseliyor.

**S4: 1 Saat hedef süre gerçekçi mi?**
Ortalama gün süresi 200s x 16 = 3200s (~53 Dakika). Evet, gerçekçi.

**S5: Event sistemi dengeli mi?**
Event çarpanları düşük. Kötü eventlerin cezalandırıcılığı az (Wait time 0.7x'e düşüyor, ama interval hala geniş). Etkiler %50-%100 artırılabilir.

**S6: Görev sistemi kumar hissi veriyor mu?**
Mevcut ceza mekaniği net olarak gözükmüyor. Eğer görev yapılamazsa sadece küçük bir ceza uygulanıyor. Ödüller cazip ama risk hissi düşük.

**S7: 4 oyuncuyla kolay mı?**
Evet. `patience` ve `customer_count` ölçekleniyor ama 4 oyuncunun işgücü x4. x2 Müşteri (4P için) yeterince zorlamıyor.

## 7. Interactive Plan — Öneriler

### 7A — Rafı Anlamlı Kılma
```
Öneri: Yere Bırakma Mekaniğini Kaldırma (veya Katılaştırma)
Değiştirilecek: BoxFallPenalty.cs
Mevcut: dropMoneyPenalty = 10
Önerilen: dropMoneyPenalty = 50, Box destroy edilir.
Etki: Oyuncu raf almak zorunda kalır.
Risk: Oyun çok zorlaşabilir.
```

### 7B — Upgrade Sistemini Dengeli Kılma
```
Öneri: Müşteri Spawn Formülünü Değiştirme
Değiştirilecek: CustomerManager.cs
Mevcut: activeInteractables * _shelfMultiplier
Önerilen: Gün sayısına bağlı sabit artış (örn: Day * 1.5). Raf sayısı sadece capacity'i etkilemeli.
Etki: Oyuncu raf almamayı bir sömürü taktiği olarak kullanamaz.
Risk: Dengeleme gerektirebilir.
```

### 7C — Gameplay Döngüsüne Varyasyon
```
Öneri: Acil Müşteri Mekaniği
Değiştirilecek: CustomerManager.cs / CustomerAI.cs
Önerilen: %10 ihtimalle gelen müşterilerin sabrı %50 daha az olsun ama 2x para versin.
Etki: Tekdüzeliği kırar.
```

### 7D — Kazanma Koşulu
```
Öneri: 3 Strike Mekaniği
Değiştirilecek: GameStateManager.cs
Önerilen: 1 kaçırmada bitmek yerine 3 hak verilmeli.
Etki: 16 günlük süre için daha insaflı olur.
```

## 8. Öncelik Matrisi

| Öneri | Etki | Uygulama Kolaylığı | Öncelik |
|-------|------|--------------------|---------|
| Müşteri Spawn Formülünü Günden Alma | Yüksek | Kolay | **P1** |
| Yere Bırakma Cezasının Artırılması | Orta | Kolay | **P1** |
| 3 Strike (Hak) Sistemi | Yüksek | Orta | **P2** |
| Acil Müşteri Mekaniği | Orta | Orta | **P3** |

## 9. Sektör Örnekleri: PlateUp! ve Overcooked! Analizi

Kargo ve restoran yönetimi oyunlarında (PlateUp! ve Overcooked!), Cargor'da karşılaştığımız tasarım sorunları (tezgah/raf kullanımı, zorluk ölçeklemesi, döngü tekrarı ve gerilim) şu şekilde çözülmüştür:

### 1. Raf/Tezgah Kullanımı ve Yere Eşya Bırakma
**Sorun:** Cargor'da oyuncu ufak bir ceza (-10) ödeyip ürünü yere atabiliyor, bu da rafları anlamsız kılıyor.
**PlateUp! & Overcooked Çözümü:**
- **Yere Atmanın İmkansızlığı / Ciddi Cezası:** Bu oyunlarda genel kural, eşyaların **sadece tezgahlara** konabilmesidir. Eğer bir ürün yere düşerse (Overcooked'da uçurumdan düşmek vb.) ürün yok olur veya ciddi bir zaman kaybı yaratır.
- **Tezgah (Counter) Yönetimi:** PlateUp!'ta alan çok kısıtlıdır. Oyuncunun elindeki ürünü bırakabileceği tezgahlar sınırlıdır. Oyuncu tezgah alanını doğru yönetmezse kilitlenir. Tezgah satın almak bir lüks değil, operasyonun büyümesi için bir zorunluluktur.
**Cargor'a Uyarlama:** Yere ürün bırakma cezası çok sertleştirilmeli (ürün yok olmalı, ya da ceza geliri sıfırlayacak kadar büyük olmalı). Veya yere eşya bırakmak tamamen engellenip, oyuncu elindeki eşyayla sadece rafa/masaya veya kamyona yönlendirilmeli. Böylece raf (capacity) upgrade'i zorunlu hale gelir.

### 2. Çok Oyunculu Ölçekleme (Multiplayer Scaling)
**Sorun:** Cargor'da 4 oyunculu mod, oyuncu gücü x4 artmasına rağmen zorluğun yeterince artmaması sebebiyle çok kolay kalıyor.
**PlateUp! & Overcooked Çözümü:**
- **Asimetrik Harita Tasarımı (Overcooked):** Overcooked, oyuncuları ayırır. Örneğin; malzemeler bir tarafta, kesme tahtaları diğer taraftadır. 4 kişi oynarken mekanik olarak herkesin bir şeyler taşıması ve fırlatması gerekir. Koordinasyon bozulduğunda oyun zorlaşır.
- **Müşteri/Sipariş Ölçeklemesi (PlateUp!):** PlateUp!, oyuncu sayısı arttıkça sadece müşteri sayısını artırmakla kalmaz, yemeklerin karmaşıklığını da artırabilir veya müşteri gruplarının boyutunu büyütür. Ayrıca, kalabalık oynamak mutfakta çarpışmalara (tıkanıklığa) sebep olur.
**Cargor'a Uyarlama:** Sadece müşteri sayısını x2 yapmak yerine, 4P modunda **aynı anda** farklı renk kutular isteyen, farklı kamyonlara eşya taşıtmayı zorlayan veya oyuncuların birbiriyle çarpışmasını/dar yollardan geçmesini gerektiren bölüm tasarımları (veya görevler) kullanılabilir.

### 3. Gameplay Loop Varyasyonu
**Sorun:** 16 gün boyunca Al → Kutula → Yükle döngüsü tekrar ediyor.
**PlateUp! & Overcooked Çözümü:**
- **Dinamik Tarifler ve Yeni Engeller:** Her iki oyunda da ilerledikçe tarifler zorlaşır (Örn: Sadece et pişirmek yerine, soğan doğra + et pişir + ekmeğe koy). Bölümlerde hareket eden bantlar, yangınlar, dönen zeminler çıkar.
- **Kart Seçimi (PlateUp! Roguelite):** PlateUp!, her günün sonunda oyuncuya kalıcı bir pozitif veya negatif kart seçtirir (Örn: "Müşteriler sipariş değiştirir" vs "Müşteriler daha çok bahşiş verir"). Bu, run'ın kaderini belirler.
**Cargor'a Uyarlama:** Kutulama adımına ufak bir ekstra adım eklenebilir (Örn: Kırılacak eşyalar için ekstra bantlama). Event sistemi, PlateUp'taki roguelite kart sistemine benzer şekilde, oyuncunun gün sonunda seçeceği kalıcı mutasyonlar haline getirilebilir.

### 4. Kazanma Koşulu ve Gerilim (Patience / Wait Time)
**Sorun:** 1 müşteri kaçırmama koşulu, oyuncu az kapasiteyle (raf almayarak) oynarsa çok risksiz.
**PlateUp! & Overcooked Çözümü:**
- **PlateUp! (1 Kaçırma = Game Over):** PlateUp!'ta da bir müşteri çok beklerse oyun biter. Ancak oyun, oyuncuyu **büyümeye zorlar**. Franchise kurmak için çok müşteri çekmek ve yeni masalar almak zorundasınızdır. Oyuncu bilerek az masa alıp ilerleyemez, çünkü oyun gün geçtikçe müşteri sayısını formülle (franchise/gün sayısı) artırır.
- **Overcooked (Yıldız Sistemi):** Overcooked süre bitiminde puana göre 1-3 yıldız verir. Kaybetmek, yıldız sınırını geçememektir.
**Cargor'a Uyarlama:** PlateUp!'ı referans alıyorsanız, müşteri sayısını oyuncunun aldığı raf sayısından (interactables) **bağımsız** hale getirmelisiniz. Müşteri sayısı gün sayısına bağlı olarak agresif şekilde artmalı. Böylece oyuncu 16 günü atlatmak için **mecburen** raf, masa ve hız upgrade'leri satın almak zorunda kalır. Müşteri kaçırma riskini bu şekilde stresli ve kaçınılmaz bir hale getirebilirsiniz.

> **Özet PlateUp! Formülü:** Oyun, oyuncuyu sürekli sınırlarını zorlamaya iter. Cargor'daki en büyük tasarım boşluğu, `CapacityBase` formülünün oyuncunun aldığı eşyalara bağlı olmasıdır. Bu formül, PlateUp'taki gibi "Güne/Seviyeye bağlı sürekli artan talep" olarak değiştirilirse, oyunun roguelite/management gerilimi mükemmel şekilde çalışacaktır.
