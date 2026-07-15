# Tır–Müşteri Renk Dengesi + Tır Gecikmesi — Tasarım

**Tarih:** 2026-07-15
**Dal:** `feature/map-mechanics-balance`
**Tür:** BÜYÜK/RİSKLİ (core loop + ekonomi)

## Problem

1. **Müşteri ürünü ile tır rengi bağımsız iki rastgele akış** → oyuncu elinde bir renk kutu kalıp o renkte tır bulamıyor.
2. **"İlk 3 müşteri aynı item"** → dengesiz dağıtım.
3. Tır dolması zorunluluğu hissi + tır rotasyonu yavaş (120sn).

## Koddan ölçülen kök nedenler

### Eşleme zinciri
Müşteri ürün bırakır → oyuncu doğru renk kutuya koyar (`Table.IsValidBoxProductCombination`) → o renk tıra teslim eder.
- **Toy → Kırmızı**, **Clothing → Sarı**, **Glass → Mavi**

Enum sıraları (dikkat, uyumsuz):
- `ProductInfo.ProductType`: Toy=0, Clothing=1, Glass=2
- `BoxInfo.BoxType`: **Yellow=0, Blue=1, Red=2**

### Kök neden #1 — Ürün havuzu renk olarak çarpık (50/25/25)
`Customer.prefab` `productPrefabs[]` = 8 ürün:

| index | prefab | productType | kutu rengi |
|---|---|---|---|
| 0 | CameraNGO | 0 Toy | 🔴 Kırmızı |
| 1 | clothesNGO | 1 Clothing | 🟡 Sarı |
| 2 | cubesNGO | 0 Toy | 🔴 Kırmızı |
| 3 | cupNGO | 2 Glass | 🔵 Mavi |
| 4 | plateNGO | 2 Glass | 🔵 Mavi |
| 5 | Toy2NGO | 0 Toy | 🔴 Kırmızı |
| 6 | ToyNGO | 0 Toy | 🔴 Kırmızı |
| 7 | TshirgNGO | 1 Clothing | 🟡 Sarı |

- Renk payı: **Kırmızı %50, Sarı %25, Mavi %25**. Tır arzı %33/%33/%33.
- Ardışık-tekrar engeli (`CustomerManager.BuildProductCandidates`) **ürün index'ine** bakıyor, renge değil → 4 farklı Toy prefabı "farklı" sanılıp arka arkaya kırmızı talep üretiyor.

### Kök neden #2 — Deterministic loot bozuk
`CustomerManager.GetDeterministicProductIndex` `(int)nextTruckColor`'ı doğrudan ürün index'i olarak kullanıyor, ürün→renk zincirinden geçmiyor. Sonuç: sarı tır→Toy(kırmızı), mavi tır→Clothing(sarı) — 3'te 2 yanlış eşleme; sadece kırmızı tırda kazara tutuyor.

## Çözüm — Yaklaşım A: Renk-önce dengeli torba

**Hedef renk oranı: 1:1:1** (ödül renkten bağımsız → ekonomik nötr, adil dağıtım).

### 1. Tır rengi — dengeli torba
`TruckSpawner.RefillPlannedTruckColors`: saf `Random.Range(0,3)` yerine `[Yellow, Blue, Red]` torbası; karıştır, replacement'sız çek, bitince tekrar doldur+karıştır. → Her 3 ardışık tırda üç renk de gelir. `NextTruckColor` peek korunur.

### 2. Müşteri ürünü — önce renk, sonra prefab
`CustomerManager` ürün-atama bölümü:
- **Renk torbası:** `[Red, Yellow, Blue]` karıştırılmış, replacement'sız → her 3 müşteride üç renk de.
- **Mevcut tıra hafif kayırma:** çekilecek renk, o an hangarda kapasitesi olan bir tır rengiyse öncelenir; yoksa torba sırasına düşülür.
- Renk seçilince o renkten **rastgele prefab** (aynı prefab ardışık gelmesin — anti-repeat renk-içi index'te).
- **Renk→index listesi** bir kez `productPrefabs` taranarak çıkarılır (her prefabın `ProductInfo.productType` → kutu rengi).
- **Bozuk `_deterministicLootCounter` / `GetDeterministicProductIndex` silinir.**

### 3. Tır süresi
`GameEconomySettings.hangarStayDuration` 120→**30** (economist onayıyla). Erken-kalkış (dolunca kalkma) **korunur** (`Truck.HangarTimerCoroutine` mevcut mantık). Dolma zaten zorunlu değil — kısmi teslim kutu-başı ödüllü, cezasız.

## Ekonomik bağımlılık (economist gate)
30sn, tır rotasyonunu ~4× hızlandırır → tır-başı daha az kutu, günde daha çok tır. 16-gün hayatta-kalma dengesini bozabilir. economist ölçecek:
- `hangarStayDuration` nihai değer (30 mu, başka mı)
- `requiredCargo` aralığı (`TruckSpawner` MIN=3/MAX=7) gerekirse
- `rewardPerBox` gerekirse
- 1:1:1 oranının ekonomik nötrlüğü teyidi

## Dokunulacak dosyalar
- `Assets/NewCss/TruckScripts/TruckSpawner.cs` — `RefillPlannedTruckColors` dengeli torba
- `Assets/NewCss/CustomerSripts/CustomerManager.cs` — ürün-atama: renk torbası + renk→index map, deterministic-loot silme
- `Assets/NewCss/GameEconomySettings.cs` + `Assets/Resources/EkonomiAyarlari.asset` — süre (+gerekirse kargo/ödül)

## Riskler / kontroller
- Server-authoritative korunmalı (üretim server-only, mevcut desen).
- Renk→index map müşteri prefabı 8 ürünle senkron kalmalı (runtime tarama, sabit değil).
- Late-join: torba state'i server-side; client görsel ürünü NetworkVariable index'ten alıyor (mevcut `_networkAssignedProductIndex`).
- Değişiklik geriye dönük: kutu/tır teslim mantığı (`Table`, `Truck.ProcessDelivery`) değişmiyor.

## İş akışı
1. economist (sayılar) → 2. gameplay (uygulama) → 3. qa (inceleme) → 4. kontrol (ONAY kapısı) → play-test.
