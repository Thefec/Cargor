# Cargor Ekonomi Denge Analizi Raporu

**Tarih**: 2026-07-07
**Kapsam**: `startingMoney` gerçek değeri, prestij kırılganlığı, yanlış-teslim/doğru-teslim ceza asimetrisi
**Yöntem**: `Assets/NewCss/GameEconomySettings.cs` içindeki `RunSimulation()` mantığı Node.js'te birebir portlanarak 16 günlük 1P/2P/4P simülasyonları çalıştırıldı (Python bu makinede kurulu değil; hesaplamalar yine bir interpreter üzerinden yapıldı, kafadan hesap yapılmadı). Betikler: bkz. rapor sonu "Kullanılan betikler".

---

## 0. Doğrulanan kod çelişkisi (öncelikle düzeltilmeli)

Kodda **iki farklı başlangıç parası kaynağı** var ve birbiriyle çelişiyor:

| Kaynak | Değer | Notlar |
|--------|-------|--------|
| `MoneySystem.cs:12` `startingMoney` | **100.000 TL** | Açıkça "Test için" yorumu var — shipping değeri değil. |
| `DifficultyManager.cs:36` `baseStartingMoney` | **100 TL** | `ScaledStartingMoney` ile oyuncu sayısına göre **düşürülüyor**: 1P=100, 2P=85, 3P=72, 4P=61 (×0.85 oyuncu başı). |
| GDD.md simülasyon varsayımı (bölüm 31.1) | **500 TL** | Tüm 1P/2P/4P senaryoları bu değeri baz alıyor. |

`DifficultyManager.ApplyDifficultySettings()` çalıştığında `moneySystem.startingMoney = ScaledStartingMoney` satırıyla gerçek oyunda kullanılan değer **100 TL'nin altına** düşüyor (4P'de 61 TL) — bu, hem "100.000 test değeri" hem de GDD'nin "500 TL" varsayımıyla çelişiyor. Hangi sistemin gerçekte devrede olduğu netleşmeden hiçbir startingMoney önerisi tam güvenilir olmaz; **gameplay/QA bu çelişkiyi teyit etmeli**.

Ayrıca **oyuncu sayısı arttıkça başlangıç parasının azaltılması (×0.85/oyuncu) ile aynı anda kira taban değerinin artması (500→1500) ters yönlü iki zorluk katmanının üst üste binmesidir** — bu tasarım riskini madde 2'de detaylandırıyorum.

---

## 1. startingMoney — Önerilen Değer

### Hesaplama: Gün 1-4 doğal gelir vs. Gün 4 kirası (upgrade yapılmadan, hız=2.0 kutu/dk/oyuncu)

| Oyuncu | Gün 1-4 doğal gelir | Gün 4 kirası | Fark (startingMoney'siz) |
|--------|---------------------|--------------|---------------------------|
| 1P | 643 TL | 500 TL | **+143 TL** |
| 2P | 1.160 TL | 900 TL | **+260 TL** |
| 4P | 1.160 TL | 1.500 TL | **-340 TL** |

> Not: 2P ve 4P gün 1-4 arası **aynı geliri** üretiyor çünkü bu erken evrede gelir oyuncu hızıyla değil, `expectedCustomers` tavanıyla (raf+seviye formülü) sınırlı. Yani 4 oyuncu aynı işi paylaşıp daha erken bitiriyor ama **daha fazla kazanmıyor**, buna rağmen kirası 3 kat daha yüksek. Bu, co-op grupları solo oyunculardan orantısız şekilde daha riskli bir açılışa sokuyor.

### startingMoney'nin gün-4 kirasını grace period'a hiç dokunmadan karşılaması için gereken minimum

| startingMoney | 1P | 2P | 4P |
|---------------|-----|-----|-----|
| 0 | Geçer | Geçer | **Grace/iflas riski** |
| 300 | Geçer | Geçer | **Grace/iflas riski** (40 TL açık) |
| **500** | Geçer (+643 pay) | Geçer (+260 pay) | **Geçer** (+160 pay) |
| 800 | Geçer | Geçer | Geçer (daha fazla pay) |

**Sonuç**: 500 TL, tüm oyuncu sayıları için gün-4 kirasını grace period'u hiç yormadan karşılıyor ve GDD'nin zaten varsaydığı değerle örtüşüyor. 800 TL'ye çıkmanın gün-4 için somut bir faydası yok (500 zaten yeterli marj bırakıyor) — sadece erken oyunu gereksiz rahatlatıp ilerleyen zorluk eğrisini geciktirir.

### Kritik bulgu — startingMoney tek başına yeterli değil

16 günlük tam simülasyonda `startingMoney` değerini 100 → 500 → 800 → 1000 → 1500 aralığında değiştirdim; **1P ve 2P her durumda sırasıyla gün 12 ve gün 8'de iflas ediyor** (bkz. Ek-A). Sebep: `rentGrowthMultiplier=1.3` ile kira her dönem %30 büyürken, `wealthTaxRate=0.10` yatırılan her upgrade'i **kalıcı ek kira yüküne** çeviriyor. Gelir tavanı (`expectedCustomers`, max 50) bu bileşik büyümeyi büyük mağaza seviyelerine ulaşmadan yakalayamıyor. Bu, **startingMoney'den bağımsız, yapısal bir ölüm sarmalı** — 1500 TL ile başlasanız bile 1P/2P aynı günlerde iflas ediyor.

**Doğrulama testi**: `rentGrowthMultiplier` 1.3 → 1.15 düşürülüp `startingMoney=500` ile tekrar koşulduğunda **1P, 2P ve 4P'nin hepsi 16 günü sağlıklı bir kasa bakiyesiyle (172 / 553 / 1.833 TL) tamamlıyor.**

### Öneri

| Parametre | Mevcut | Önerilen | Gerekçe |
|-----------|--------|----------|---------|
| `MoneySystem.startingMoney` | 100.000 (test) | **500 TL** | GDD varsayımıyla uyumlu, gün-4 kirasını tüm oyuncu sayılarında grace'siz karşılıyor, gereksiz erken-oyun rahatlığı yaratmıyor |
| `DifficultyManager.baseStartingMoney` | 100 | **500 TL** (MoneySystem ile senkron) | İki kaynak çelişkisini gider |
| `DifficultyManager.moneyMultiplierPerPlayer` | 0.85 (azaltıyor) | **1.0** (sabit 500, oyuncu sayısından bağımsız) | Kira zaten oyuncu sayısına göre artıyor (500→1500); parayı AYRICA azaltmak çifte ceza oluyor |
| `GameEconomySettings.rentGrowthMultiplier` | 1.3 | **1.15** | startingMoney'den bağımsız yapısal iflas sarmalını çözen tek parametre; testte 1P/2P/4P'nin hepsini 16 gün hayatta tutuyor |

> Bu son satır talep edilen 3 maddenin dışında ama görev tanımındaki "ölüm sarmalı riski" analizini doğrudan karşılıyor — startingMoney'i tek başına yükseltmek (500→800→1500) sorunu çözmüyor, kira büyüme oranı asıl kaldıraç.

---

## 2. Prestij Kırılganlığı — Önerilen Değer

### Mevcut durum
`startingPrestige = 5.0`, `customerLostPrestigePenalty = -2.0`. GDD 6.4 zaten "3 kaçış = oyun biter" riskini not etmiş; simülasyon bunu doğruladı ve rush dalgası bağlamında niceledi.

### Tek dalga risk tablosu (0 başarılı servis varsayımıyla — oyuncu dalga boyunca tamamen ezildiği en kötü senaryo)

| startingPrestige | Kaç kaçan müşteri prestiji sıfırlar | Max eşzamanlı 6 içindeki risk oranı |
|-------------------|--------------------------------------|----------------------------------------|
| **5.0 (mevcut)** | **3** | **%50** (6 müşteriden 3'ü kaçarsa oyun biter) |
| 8.0 | 4 | %67 |
| 10.0 | 5 | %83 |
| 15.0 | 8 (>6) | **%100 güvenli** — tüm dalga kaçsa bile hayatta |

### Servis-arası dengelenmiş senaryo (dalga sırasında bir miktar başarılı servis de yapılıyorsa)

| startingPrestige | Dalga arası 0 servis | 2 servis | 4 servis |
|-------------------|----------------------|----------|----------|
| 5.0 (mevcut) | 3. kayıpta biter | 5. kayıpta biter | pratikte bitmiyor |
| 15.0 (öneri) | 8. kayıpta biter | 15. kayıpta biter | pratikte bitmiyor |

**Sorun**: Öğle Rush dalgasında (max 6 eşzamanlı, GDD 9.2) yeni bir oyuncu grubu ilk günlerde (henüz sabır/kuyruk upgrade'i almamış, ilk çeyrek saatte) **hiçbir hata yapmadan** bile şansızlık/gecikme yüzünden 3 müşteri kaybederse anında oyunu kaybediyor — bu "adil olmayan" bir ölüm, çünkü ceza kaynağı oyuncu becerisinden çok dalga yoğunluğu + spawn RNG'sine bağlı.

Ek not: `PrestigeManager.GetCustomerCapacity()` (1 + floor(prestij/10), maks 20) formülü prestij=5'te kapasiteyi **1**'e düşürüyor, ama bu değeri hiçbir yerde (CustomerManager/wave spawn) tüketen kod bulunamadı — yani şu an **UI'da görünen ama spawn'ı gerçekte kısıtlamayan ölü bir mekanik**. Bu QA/gameplay tarafından doğrulanmalı; eğer ileride gerçekten bağlanırsa, prestij=5 iken kapasite=1 olması 6 kişilik rush dalgasıyla doğrudan çelişir.

### Öneri

| Parametre | Mevcut | Önerilen | Gerekçe |
|-----------|--------|----------|---------|
| `startingPrestige` | 5.0 | **15.0** | Tek bir kötü rush dalgası (6/6 kaçış, 0 servis) artık öldürmüyor; hâlâ 3 kayıpta ciddi baskı hissettiriyor |
| `customerLostPrestigePenalty` | -2.0 | **-1.5** | Servis/kayıp oranını 4:1'den **3:1**'e yumuşatıyor — hâlâ anlamlı bir ceza ama tek hata zinciri affedilmez olmaktan çıkıyor |

Kombine etki: `15.0` başlangıç + `-1.5` ceza → sıfırlanma için gereken kayıp sayısı = `ceil(15/1.5) = 10`, yani maksimum eşzamanlı 6 müşterinin tamamı kaçsa bile (10 > 6) tek bir dalga oyunu bitiremiyor. Yine de kümülatif ihmal (birden fazla dalga boyu kötü oynama) cezalandırılmaya devam ediyor — bu istenen "beceri gerektiren risk", istenmeyen "RNG'den ölüm" değil.

---

## 3. Yanlış Teslim / Doğru Teslim Asimetrisi — Önerilen Değer

### Mevcut durum
`rewardPerBox = 50`, `penaltyPerBox = 60` → oran **1.20** (1 yanlış teslim, 1.2 doğru teslimin gelirini siliyor). Bu ceza yalnızca **tıra yanlış renk kutu teslimi** (Truck.cs) için geçerli; kutu düşürme (`boxDropPrestigePenalty` -0.05, -10 TL) ve yanlış ürün gösterme (`wrongProductPrestigePenalty` -0.1) ayrı, çok daha küçük cezalar — bunlarda değişiklik önerilmiyor.

### Prestij tier'lerine göre oran değişimi

| Prestij Tier | Doğru Teslim Ödülü | Mevcut Ceza (60) → Oran | Önerilen Ceza (40) → Oran |
|---|---|---|---|
| 0 (yeni oyuncu) | 50 | 60 → **1.20** | 40 → **0.80** |
| 1 | 55 | 60 → 1.09 | 40 → 0.73 |
| 2 | 60 | 60 → 1.00 | 40 → 0.67 |
| 3 | 65 | 60 → 0.92 | 40 → 0.62 |
| 5+ | 75 | 60 → 0.80 | 40 → 0.53 |

**Gözlem**: Ceza-ödül oranı prestij yükseldikçe kendiliğinden iyileşiyor (0.80'e kadar düşüyor) çünkü ödül prestijle büyürken ceza sabit kalıyor. Ama tam da **en çok hata yapan yeni oyuncular** (prestij 0-9, tier 0) en kötü oranla (1.20) karşılaşıyor — deneyim eğrisinin en dik olduğu yerde ceza en ağır. Bu, GDD 10.2'nin övdüğü "riskli ama hızlı fırlatma/co-op oynanışını" tam da öğrenme aşamasında caydırıyor.

### Öneri

| Parametre | Mevcut | Önerilen | Gerekçe |
|-----------|--------|----------|---------|
| `penaltyPerBox` | 60 | **40** | Oranı 1.20'den 0.80'e çeker; yanlış teslim hâlâ gerçek bir maliyet ama artık doğru teslimden daha ucuz — riskli/hızlı co-op oynanışını cezalandırmak yerine dengeler |

Alternatif (daha muhafazakâr): `penaltyPerBox = 50` (tam nötr, oran 1.00, tier 0'da "1 yanlış = 1 doğru" basit okunabilir kural). 40 önerisi daha agresif co-op teşviki içindir; ekip risk toleransını düşük tutmak isterse 50 de kabul edilebilir bir ara adımdır.

---

## Death-Spiral Risk Özeti

| Risk | Kaynak | Ciddiyet | Çözüm |
|------|--------|----------|-------|
| **Yapısal kira sarmalı** | `rentGrowthMultiplier=1.3` bileşik büyüme + `wealthTaxRate=0.1` kalıcı vergi, gelir tavanına (`expectedCustomers≤50`) çarpıyor | **Kritik** — startingMoney'den bağımsız, 1P/2P her koşulda gün 8-12'de iflas ediyor | `rentGrowthMultiplier → 1.15` |
| **Co-op başlangıç dengesizliği** | Daha fazla oyuncu = daha az başlangıç parası (×0.85/oyuncu) + daha yüksek kira (500→1500), ama erken oyun geliri oyuncu sayısından bağımsız (müşteri tavanı sınırlı) | **Yüksek** | `moneyMultiplierPerPlayer → 1.0` |
| **Prestij RNG-ölümü** | `startingPrestige=5.0` iken tek bir rush dalgasında (max 6 eşzamanlı) 3 kayıp yeterli | **Yüksek** — beceri değil, spawn zamanlaması belirleyici | `startingPrestige→15.0`, `customerLostPrestigePenalty→-1.5` |
| **Grace period tükenmesi** | Grace tüm oyun boyunca yalnızca 1 kez kullanılabiliyor; erken (gün 4) kötü şans yüzünden harcanırsa gün 12-16'daki çok daha büyük kiralarda hiç güvenlik ağı kalmıyor | Orta | Değerlendirme önerisi: Grace hakkını oyun yarısında (gün 8) bir kez daha yenilemek ya da kademeli maliyet (%80 → %90 ikinci kullanımda) |
| **Erken oyunda ceza/ödül asimetrisi** | `penaltyPerBox=60 > rewardPerBox=50` tam da tier 0'da (yeni oyuncu) en kötü oranda | Orta | `penaltyPerBox → 40` |
| **Dead-code prestij kapasitesi** | `PrestigeManager.GetCustomerCapacity()` formülü (prestij=5→kapasite=1) hiçbir spawn sistemine bağlı değil görünüyor | Bilgi/QA flag | Gameplay/QA doğrulasın; bağlıysa ciddi çelişki |
| **Simülasyon-gerçek formül farkı** | `RunSimulation()` içindeki `activeInteractables×2+storeLevel×2` formülü, GDD 9.6'daki gerçek `shelfMult=3.0` formülünden farklı (daha düşük müşteri sayısı üretiyor) | Bilgi | `RunSimulation()` gerçek `GameEconomySettings` çarpanlarını (`shelfMultiplier`, `levelMultiplier`) kullanacak şekilde güncellenmeli — aksi halde editor simülasyonu gerçek oyundan daha iyimser sonuç verir |

---

## Özet Tablo — Önerilen Değer Değişiklikleri

| Dosya/Alan | Mevcut | Önerilen |
|---|---|---|
| `MoneySystem.cs` → `startingMoney` | 100.000 (test) | **500** |
| `DifficultyManager.cs` → `baseStartingMoney` | 100 | **500** |
| `DifficultyManager.cs` → `moneyMultiplierPerPlayer` | 0.85 | **1.0** |
| `EkonomiAyarlari.asset` → `rentGrowthMultiplier` | 1.3 | **1.15** |
| `PrestigeManager.cs` → `startingPrestige` | 5.0 | **15.0** |
| `EkonomiAyarlari.asset` → `customerLostPrestigePenalty` | -2.0 | **-1.5** |
| `EkonomiAyarlari.asset` → `penaltyPerBox` | 60 | **40** |

Bu değişiklikler gameplay departmanı tarafından uygulanmalı; uygulamadan sonra qa subagent'ın 16 günlük 1P/2P/4P senaryolarını yeniden test etmesi önerilir.

---

## Kullanılan Betikler (referans, geçici)

- `.../scratchpad/econ_sim.js` — `GameEconomySettings.RunSimulation()` birebir port, startingMoney taraması, gün-16 uzatma
- `.../scratchpad/econ_sim2.js` — gün 1-4 doğal gelir/kira karşılaştırması, rentGrowthMultiplier duyarlılık testi, prestij kırılganlık matrisi
- `.../scratchpad/econ_sim3.js` — growth=1.15 doğrulama koşuları

(Bu dosyalar geçici scratchpad dizininde, proje deposunun bir parçası değildir.)
