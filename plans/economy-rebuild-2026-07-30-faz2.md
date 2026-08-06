# Cargor Ekonomi Yeniden Kurulumu — FAZ 2: KİRA EĞRİSİ + PRESTİJ + EVENT + BEKLEME SÜRELERİ

**Tarih:** 2026-07-30 · **Dal:** `feature/economy-balance-round`
**Girdi:** `plans/economy-rebuild-2026-07-30.md` (FAZ 1 — §1 envanter, §2 verim modeli, §3 gelir tabanı)
**Kapsam DIŞI:** upgrade + quest fiyatlandırma (FAZ 3, paralel yürüyor)
**Bu tur SALT HESAP + ÖNERİ.** Hiçbir `Assets/` dosyası, `sim.js` veya FAZ 1 raporu değiştirilmedi.
Hesap script'leri: scratchpad `faz2.js` / `faz2b.js` / `faz2c.js` (sim.js'i yalnız okur).

> **Model paritesi:** FAZ 2 hesapları FAZ 1 sim'iyle ±0–3.3% içinde doğrulandı (`faz2b.js` §0 sanity).
> FAZ 1 konvansiyonu korundu: `laborShareTruck = 0.6` her iki bantta tıra uygulanır; bantlar arası
> fark ön-stok (`handoverSpeedup`) + müşteri emek çakışmasıdır.

---

## §0 ÖNCELİK SIRASI (tek başına en çok düzelten önce)

| # | Değişiklik | Tek başına ne düzeltiyor | Risk |
|---|---|---|---|
| **1** | **Prestij asimetrisi + paralel servis** (§2) | 1P STRICT'te oyunun TEK kira-dışı game-over kaynağını kapatır (gün 5 prestij ölümü → hayatta kalır). 3P/4P ters ölçeklemesini tersine çevirir. | Orta (kod: servis istasyonu kablolama) |
| **2** | **Kira P-ölçeği `{500,1000,1550,2150}`** (§1.3) | #1'in ZORUNLU eşi. Uygulanmazsa 4P easy-mode olur (P'ler arası yayılım 0.01 → 0.79). | Düşük (tek dizi) |
| **3** | **Kira eğimi `rentGrowthMultiplier` 1.15 → 1.35** (§1.2) | Geç-oyun gevşemesini kapatır: baskı 1.76→1.18 (düşen) yerine 1.83→2.05 (düz/hafif yükselen). | Düşük (tek float) |
| **4** | **A1: sahne `startingMoney` 50000 → 500** (§5) | Yayın engelleyici. Bu değerle tüm ekonomi anlamsız. | Yok |
| **5** | **P-bazlı kargo aralığı** (§4.4) | Tır/gün tasarım hedefini tutturur (1.4→3.3 tır/gün 1P). **Gelir-nötr** (kutu/gün değişmiyor). 1P'nin "1 tır tamamla" quest'ini mümkün kılar. | Düşük |
| **6** | **Müşteri sabrı 15-20 → 24-32 sn** (§4.3) | Yavaş bandın prestij kanamasının kök nedeni: sabır 17.5s < Yavaş servis emeği 25s → yapısal imkânsızlık. | Düşük (prefab) |
| **7** | **Telefon `ringDuration` 25→15 + P-bazlı şans** (§4.5) | Telefon günün %37.5'ini kaplıyor. Ekran-zamanı hijyeni. | Düşük |
| **8** | **Event yeniden dengeleme** (§3) | Yalnız GOLDEN BOX DAY gerçekten bozuk (+47%). Gerisi cila. | Düşük |
| **9** | **A2/A3/A9 default hizalama** (§5) | Denge değişikliği YOK; gelecekte sessiz %25 kaymayı önler. | Yok |

---

## §1 KİRA EĞRİSİ

### 1.1 Hedef bandın tanımı ve gerekçesi

Ölçüt: **kira baskısı = o kira gününün kirası / o günün net geliri.** Kira aralığı 4 gün olduğundan
`4.0` = o günün kirası 4 günlük gelirin tamamı (ilerleme sıfır, kesin ölüm), `1.0` = gelirin %25'i
(çok rahat).

**HEDEF BAND: her kira gününde 1.80–2.10, oyuncu sayıları arası yayılım ≤ 0.15.**
Yani 4 günlük gelir penceresinin **%45–52'si** kiraya gider, kalanı upgrade'e (FAZ 3) ve tampona.

Kümülatif ölçüt (16 günün toplam net geliri / 4 kiranın toplamı):

| Bant | Hedef | Gerekçe |
|---|---|---|
| Normal + OPTIMISTIC | **1.80 – 1.90** | "Kazanılabilir ama rahat değil": gelirin %54'ü kiraya gider |
| Normal + STRICT | **0.95 – 1.10** | Sınırda; grace period bir kez kurtarır |
| Yavaş + OPTIMISTIC | **0.92 – 1.00** | Yavaş ama organize takım kıl payı kazanır |
| Yavaş + STRICT | **kayıp KABUL** | Oyunun bilinçli başarısızlık durumu |

**Neden "yükselen" band DEĞİL (ilk sezgimin düzeltmesi):** yükselen baskı (gün16'da 2.2–2.5)
`rentGrowthMultiplier ≈ 1.45` gerektiriyor; o değerde Yavaş+OPTIMISTIC 0.84–0.95'e düşüp
**kaybetmeye başlıyor** (`faz2c.js` §2, ölçek C/g=1.40 satırı: `Slow OPT 0.82 / X16 / X16 / X12`).
Gerilim artışı **oran** ile değil **mutlak kira** ile verilmeli: 1P kirası 500 → 1230 TL (×2.46),
4P kirası 2150 → 5290 TL. Oyuncu bunu tırmanış olarak hisseder; oran sabit kalır ki alt bant
yaşayabilsin.

### 1.2 Eğim: `rentGrowthMultiplier` 1.15 → **1.35**

**Gerekçe (sayıyla):** gelir her kira döngüsünde **×1.32–1.38** büyüyor (`faz2.js` §1), kira ise
yalnız ×1.15 → her döngüde makas %15–20 açılıyor. Gelir büyümesinin kaynağı iki bileşen:
gün uzunluğu ×1.57 (200→330 sn) ve kutu-başı ödül ×1.42–1.75 (prestij tier'ı).

Sweep (`faz2c.js` §2, prestij paketi + P-ölçeği B uygulanmış hâlde):

| `rentGrowth` | Normal OPT (1P/2P/3P/4P) | Normal STRICT | Yavaş OPT | 2P baskı g4/g8/g12/g16 |
|---|---|---|---|---|
| 1.15 (mevcut) | 2.47 2.73 3.20 3.52 | 1.31 1.60 1.81 1.85 | 1.28 1.44 1.66 1.76 | 1.80 / 1.45 / 1.22 / **1.13 ↓düşen** |
| 1.30 | 1.99 1.98 2.00 1.98 | 1.06 1.16 1.13 1.04 | 1.03 1.05 1.04 0.99 | 1.80 / 1.63 / 1.56 / 1.63 |
| **1.35 ✅** | **1.86 1.85 1.86 1.85** | **0.99 1.08 1.06 0.97** | **0.97 0.98 0.97 0.93** | **2.00 / 1.89 / 1.87 / 2.02 düz** |
| 1.40 | 1.73 1.72 1.74 1.73 | 0.92 1.01 0.99 0.91 | 0.90 0.91 0.90 **X16** | 1.80 / 1.76 / 1.81 / 2.03 |

**1.35 seçildi:** üç bandın hepsi hedefte, Yavaş+OPT hâlâ kazanıyor (0.93–0.98), hiçbir iflas yok.
1.40'ta Yavaş+OPT 4P gün 16'da iflas ediyor → çok sert.

> **Tarihsel not:** `rentGrowthMultiplier` daha önce 1.3 → 1.15'e düşürülmüştü (ölüm sarmalı).
> O karar `realDurationInSeconds = 160` ve prestij gelirini fazla tahmin eden bir modelle alınmıştı.
> Düzeltilmiş tabanda (200 sn, seri servis, telefon geliri) 1.35 güvenli — üç senaryo bandında
> sıfır iflas. Bu bilinçli bir geri dönüş, unutkanlık değil.

**Dokunulacak:** `Assets/Resources/EkonomiAyarlari.asset:16` (`rentGrowthMultiplier: 1.15` → `1.35`)
ve `Assets/NewCss/GameEconomySettings.cs:24` default'u da 1.35 yapılmalı (senkron kalsın).

### 1.3 P-ölçeği: `{500, 900, 1200, 1500}` → **`{500, 1000, 1550, 2150}`**

**FAZ 1'de "P-ölçeği doğru, dokunma" demiştim — o tespit paket ÖNCESİ gelir eğrisi için doğruydu:**

```
FAZ 1 gelir ölçeği (paket öncesi):  1 : 1.81 : 2.44 : 3.15
FAZ 1 kira ölçeği:                  1 : 1.80 : 2.40 : 3.00   → örtüşüyordu ✅
```

Prestij paketi (§2) çok-oyunculu geliri yükseltiyor (3P +21%, 4P +29%) çünkü onlara haksız
uygulanan prestij cezasını kaldırıyor. Yeni ölçek (`faz2c.js` §1):

```
Paket SONRASI gelir ölçeği (OPT):    1 : 1.99 : 3.12 : 4.28
Paket SONRASI gelir ölçeği (STRICT): 1 : 2.19 : 3.32 : 4.24
Eski kira ölçeği 1:1.8:2.4:3.0 ile  → 4P EASY MODE
```

Kanıt — P'ler arası oran yayılımı (Normal+OPT, g=1.35):

| Kira ölçeği | 1P / 2P / 3P / 4P oran | Yayılım |
|---|---|---|
| MEVCUT 500/900/1200/1500 | 1.86 / 2.05 / 2.41 / **2.65** | **0.79** ❌ |
| A 500/1000/1500/2000 | 1.86 / 1.85 / 1.93 / 1.99 | 0.14 |
| **B 500/1000/1550/2150 ✅** | **1.86 / 1.85 / 1.86 / 1.85** | **0.01** ✅ |
| C 550/1100/1650/2250 | 1.69 / 1.68 / 1.75 / 1.77 | 0.09 (band altı) |

**Dokunulacak:** `Assets/Resources/EkonomiAyarlari.asset:15` — `baseRentByPlayerCount` hex
`f401000084030000b0040000dc050000` → **`f4010000e8030000` + `1e060000` + `6e080000`**
(= 500 / 1000 / 1550 / 2150). Inspector'dan girmek daha güvenli.
Ayrıca `Assets/NewCss/GameEconomySettings.cs:21` default'u ve
`Assets/NewCss/UIScripts/DayCycleManager.cs:593` fallback satırındaki `500/900/1200/1500` de
güncellenmeli (fallback yolu da doğru olsun).

⚠️ **#2 ve #1 AYRILAMAZ.** Prestij paketi olmadan P-ölçeğini değiştirmek 4P'yi haksızca cezalandırır;
P-ölçeği olmadan prestij paketini uygulamak 4P'yi easy-mode yapar. Aynı PR'da gitmeli.

### 1.4 Nihai kira tablosu (uygulandıktan sonra)

| P | gün 4 | gün 8 | gün 12 | gün 16 | 16-gün toplam |
|---|---|---|---|---|---|
| 1 | 500 | 675 | 911 | 1230 | **3 316** |
| 2 | 1000 | 1350 | 1823 | 2460 | **6 633** |
| 3 | 1550 | 2093 | 2825 | 3814 | **10 282** |
| 4 | 2150 | 2903 | 3918 | 5290 | **14 261** |

---

## §2 PRESTİJ SİSTEMİ (SIFIRDAN)

### 2.1 ÖNCE: prestijin oyundaki GERÇEK işlevi (kod ile teyit)

`grep` ile tüm tüketiciler tarandı. Prestijin **yalnız iki** işlevi var:

1. **Kutu başı ödül tier'ı** — `Truck.cs:610-645 CalculateRewardWithPrestige/CalculatePrestigeBonus`:
   `ödül = rewardPerBox + floor(prestij / prestigePerBonus) × bonusPerTier`. **Tek ekonomik işlev.**
2. **Ölüm kapısı** — `PrestigeManager.cs:154-157`: clamp ÖNCESİ `prestij ≤ 0` → `TriggerLose()`.

**Ölü/dekoratif olanlar:**
- `GetCustomerCapacity()` / `currentCustomerCapacity` — **sıfır dış tüketici**. `OnCustomerCapacityChanged`
  event'inin **sıfır abonesi** var (`PrestigeManager.cs:12, 103`). Tek kullanım bir UI metni
  (`"Capacity: N"`, `cs:181-187`). → `prestigePerCustomer=4`, `baseCustomerCapacity=1`,
  `maxCustomerCapacity=20` **dekoratif**.
- `OnPrestigeChanged` — **sıfır abonesi** var (`cs:11, 91`).
- **Kazanma koşulu prestiji KONTROL ETMİYOR.** `GameStateManager.cs:645-659 CheckWinCondition`
  yalnız `currentDay >= MAX_DAYS` bakıyor; doc yorumu "with prestige > 0" diyor ama kod bakmıyor.
  Prestij yalnız koşu ORTASINDA 0'a düşerse öldürüyor.

**Karar temeli:** prestijin tek ekonomik işlevi ödül tier'ı ⇒ prestij birikimini artırmak DOĞRUDAN
gelir enflasyonu demek ⇒ kira eğimiyle çelişir. Bu yüzden birikim ve tier eşiği **birlikte** ele alındı.

### 2.2 Karar: BİRİKİM mi TAVAN mı? → **İKİSİ BİRLİKTE, gelir-nötr biçimde**

**Sorun:** `maxPrestige = 100` hiç ulaşılmıyor (maks 47.7) → tavan ölü. Ama tavanı indirmek
(60/70/80) tavana **çok erken** çarptırıyor (gün 7-15, `faz2b.js` §6) → geç oyunda ilerleme durur.

**Çözüm — çift çarpan:** prestij birikimini **×2**, tier eşiğini de **×2** yap.
`prestigePerBonus 4 → 8` ile `served 0.2 → 0.4` birlikte uygulandığında **ödül eğrisi neredeyse
aynı kalır** (gelir-nötr) ama prestij SAYISI 0–100 skalasının tamamını kullanır ve tavan ilk kez
bağlayıcı olur.

**Tavan `maxPrestige = 100` DEĞİŞMİYOR.** Doğrulama (`faz2b.js` §6): tavan adayları

| maxPrestige | 1P tavan günü | 2P | 3P | 4P | hüküm |
|---|---|---|---|---|---|
| 60 | 11 | 9 | 8 | 7 | çok erken — geç oyun ölü |
| 80 | 15 | 13 | 11 | 9 | erken |
| **100 ✅** | **ulaşmıyor (86)** | **16** | **13** | **12** | hedef: 4P önce, 1P hiç |

### 2.3 Üç kırık noktanın çözümü

#### (a) Ters ölçekleme: SERİ servis + `maxQueueSize=2`

Kök neden: `CustomerAI.cs:580-586` yalnız `manager.IsFirstInQueue(this)` iken servis başlıyor
(`CustomerManager.cs:865-868`) → aynı anda TEK müşteri. Seri tavan ≈ **11.4 müşteri/gün ve
P'den bağımsız**; talep ise `playerCountMultiplier` ile 1.9×'a çıkıyor.

Varyant taraması (`faz2.js` §5, gün 8, prestij/gün):

| Varyant | 1P | 2P | 3P | 4P | hüküm |
|---|---|---|---|---|---|
| MEVCUT (1 istasyon, kuyruk 2) | 1.80 | 2.20 | **1.07** | **1.07** | ters ölçekleniyor ❌ |
| kuyruk 3 (tek başına) | 1.80 | 2.20 | **0.69** | **0.47** | **DAHA KÖTÜ** ❌ |
| kuyruk 4 (tek başına) | 1.80 | 2.20 | 0.55 | 0.30 | daha da kötü ❌ |
| **2 paralel istasyon ✅** | 1.80 | 2.20 | **2.80** | **3.20** | monoton artan ✅ |
| 4 paralel istasyon | 1.80 | 2.20 | 2.80 | 3.20 | 2 ile aynı (talep doymuş) |

> **Kuyruk büyütmek TEK BAŞINA prestiji DÜŞÜRÜR** — daha fazla müşteri spawn olur, servis
> kapasitesi artmadığı için fazlası kaçar ve −0.6 ceza yer. (`faz2.js` §13: kuyruk 3 ile
> 4P son prestij 37.1 → 29.8, kümülatif gelir 20 413 → 18 619.)

**ÖNERİ: `maxQueueSize = 2` KALSIN, çözüm 2 PARALEL SERVİS İSTASYONU.**

🔑 **İskelet ZATEN VAR, kablolanmamış:** `CustomerManager.cs:124` `public DisplayTable[] serviceTables;`
ve `cs:873 AssignDropOffTable(CustomerAI, DisplayTable)` — **ikisinin de sıfır çağıranı var**
(`grep serviceTables` = yalnız tanım satırı). Sahnede `serviceTables` dizisinde **1 eleman** var
(`The Main Office.unity:68615-68616`). Yani bu yeni sistem değil, **yarım bırakılmış kablolamanın
tamamlanması**:
- `serviceTables` dizisine ikinci bir `DisplayTable` eklenir (sahne),
- `IsFirstInQueue` yerine "boş istasyon var mı" mantığı: kuyruğun ilk `serviceTables.Length`
  müşterisi servis edilebilir olur,
- `AssignDropOffTable` fiilen çağrılır (şu an ölü).

**Dokunulacak:** `Assets/NewCss/CustomerSripts/CustomerManager.cs:124, 865-868, 873` +
`Assets/NewCss/CustomerSripts/CustomerAI.cs:580-586` + sahne `serviceTables` dizisi (2 eleman).
Bu bir **gameplay kod işi**, ekonomi değeri değil — economist yalnız hedefi verir: **2 istasyon.**

#### (b) Asimetri: `−0.6` kayıp vs `+0.2` servis (3:1)

Sweep (`faz2.js` §6): kayıp cezası tek başına sweep edilirse Yavaş+STRICT 1P `IFLAS gün 3` →
`−0.2` (1:1) ile `gün 16`'ya kadar yaşıyor. Ama 2P/3P/4P ölümleri KİRA kaynaklı, asimetriyle
düzelmiyor.

**ÖNERİ (birikim ×2 ile birlikte):**
- `customerServedPrestigeBonus` **0.2 → 0.4**
- `customerLostPrestigePenalty` **−0.6 → −0.5**
- → yeni asimetri **1.25 : 1** (hâlâ ceza yönlü, ama ölüm sarmalı değil)

Neden tam 1:1 değil: kaçan müşteri bir HATA; cezası ödülden hafifçe ağır olmalı ki dikkat
teşvik edilsin. 1.25:1, 3:1'in ölümcül sarmalını kırarken caydırıcılığı koruyor.

Tutarlılık için ×2 ölçeklenecek diğer prestij kalemleri (aksi hâlde ceza/ödül dengesi kayar):

| Alan | Mevcut | Öneri | Kaynak |
|---|---|---|---|
| `customerServedPrestigeBonus` | 0.2 | **0.4** | `EkonomiAyarlari.asset:36` / `GameEconomySettings.cs:105` |
| `customerLostPrestigePenalty` | −0.6 | **−0.5** | `asset:35` / `cs:102` |
| `wrongDeliveryPrestigePenalty` | −0.08 | **−0.16** | `asset:39` / `cs:114` |
| `wrongProductPrestigePenalty` | −0.04 | **−0.08** | `asset:37` / `cs:108` |
| `boxDropPrestigePenalty` | −0.02 | **−0.04** | `asset:38` / `cs:111` |
| `callPrestigeReward` | 0.2 | **0.4** | `asset:34` / `cs:93` |
| `startingPrestige` | 6 | **12** | `The Main Office.unity:25234` / `PrestigeManager.cs:16` |
| `prestigePerBonus` | 4 | **8** | `asset:25` / `cs:54` |
| `maxPrestige` | 100 | **100 (değişmez)** | `unity:25235` / `PrestigeManager.cs:19` |

⚠️ **FAZ 3 ÇAPRAZ BAĞIMLILIK:** 30 quest asset'inin `prestigeReward` / `prestigePenalty` alanları da
**×2** olmalı (Easy 0.7→1.4, Medium 1.2→2.4, Hard 2.3→4.6 vb.), yoksa quest prestij katkısı
yarıya düşer. Bu FAZ 3'ün dosyası — burada yalnız çarpan bildiriliyor.

⚠️ **PERK SENKRONU:** `Assets/NewCss/UpgradeScripts/PerkEffect.cs:92` (`Prestij Ustası`:
`customerServedPrestigeBonus = 0.2f + 0.06f*level`) ve `cs:165` (`Kaldıraçlı Kira`:
`customerLostPrestigePenalty = -1.2f`) hardcoded tabanlar taşıyor → yeni tabana göre
`0.4f + 0.12f*level` ve `-1.0f` olmalı.

#### (c) Tavan ölü → §2.2'de çözüldü (tavan 100 kalır, birikim ×2 + eşik ×2)

### 2.4 HEDEF PRESTİJ EĞRİSİ (uygulanacak tablo)

**Normal senaryo, OPTIMISTIC bant, 1 hangar** (`faz2c.js` §4):

| P | gün 1 | gün 4 | gün 8 | gün 12 | gün 16 | prestij/gün ort | ödül/kutu son | tavan günü |
|---|---|---|---|---|---|---|---|---|
| 1 | 16.2 | 30.3 | 49.0 | 67.7 | **86.2** | 4.28 | 100 (×2.0) | — |
| 2 | 17.1 | 33.8 | 56.0 | 78.1 | **100** | 5.25 | 105 (×2.1) | **16** |
| 3 | 18.4 | 39.0 | 66.2 | 93.3 | **100** | 6.62 | 110 (×2.2) | **13** |
| 4 | 19.3 | 42.5 | 73.3 | **100** | 100 | 7.59 | 110 (×2.2) | **12** |

**Normal senaryo, STRICT bant:**

| P | gün 1 | gün 4 | gün 8 | gün 12 | gün 16 | prestij/gün ort | ödül/kutu son |
|---|---|---|---|---|---|---|---|
| 1 | 12.6 | 15.6 | 21.5 | 29.8 | **40.6** | 1.58 | 70 |
| 2 | 15.2 | 25.9 | 44.1 | 64.3 | **84.4** | 4.38 | 95 |
| 3 | 17.4 | 35.0 | 60.3 | 85.5 | **100** (g15) | 6.07 | 110 |
| 4 | 18.9 | 40.6 | 69.3 | 97.9 | **100** (g13) | 7.10 | 110 |

**Sağlanan hedefler:** ① prestij/gün P ile MONOTON ARTAR (4.28 → 7.59; mevcut: 1.80/2.20/1.07/1.07)
② tavan gün 12–16'da bağlayıcı, 4P en önce 1P hiç ③ ödül/kutu ×2.0–2.2 (kira eğimiyle uyumlu)
④ hiçbir bantta prestij ölümü yok (1P STRICT 40.6'da bitiyor; mevcut: gün 5'te 0).

---

## §3 EVENT DENGESİ (SIFIRDAN)

### 3.1 Hedef bandın tanımı

| Sınıf | Gelir sapması | Prestij sapması | Gerekçe |
|---|---|---|---|
| **Ağır (major)** | ±15–25% | — | "Bugün farklı bir gün" hissi |
| **Hafif (minor)** | ±5–12% | — | Fark edilir ama koşuyu belirlemez |
| **Prestij event'i** | ~0% | günlük prestijin **≥%15'i** (≥0.8) | Para vermeyen müşteri tarafı |
| **YASAK** | >±30% | — | Tek gün koşuyu belirlememeli |
| **YASAK** | <±5% **ve** prestij <%15 | — | Ölçülemez = oyuncu fark etmez |

Referans: günlük prestij kazancı paket sonrası ~5.5 (2P gün 8).

### 3.2 Ölçüm ve öneri tablosu

Ölçüm: gün 8, Normal, OPTIMISTIC, 1P/2P/3P/4P, nihai paket üzerinde (`faz2c.js` §5).

| Event | Tip | MEVCUT gelir% (maks) | MEVCUT hüküm | ÖNERİLEN değişiklik | ÖNERİ gelir% | ÖNERİ prestij (pay) |
|---|---|---|---|---|---|---|
| **GOLDEN BOX DAY** | Poz | **+47.1** | 🔴 **AĞIR** | `rewardPerBox 1.3→1.15`, `playerMoveSpeed 1.2→1.08`, `dailyCustomer 1.2→1.15` | **+16…+23** | 1.2 (22%) |
| **CUSTOMER SUPPORT** | Poz | +4.7 | 🟠 zayıf | `phoneRingEventMultiplier 1.5→2.0` | **+9…+16** | 1.02 (19%) |
| **FATIGUE PROBLEM** | Neg | −6.2 | 🟠 zayıf | `dailyCustomer 0.8→0.85`, **`playerMoveSpeed 1.0→0.9` EKLE** | **−14…−15** | 0.8 (15%) |
| **HEAVY BOXES** | Neg | −19.0 | 🟢 sağlıklı | `playerMoveSpeed 0.8→0.85` (hafif yumuşat) | −13…−14 | 0 |
| **MARKETING DAY** | Neg | −11…−18 | 🟢 sağlıklı | **DEĞİŞİKLİK YOK** | −11…−18 | 1.2 (22%) |
| **DELIVERY BONUS** | Poz | +12.3 | 🟢 sağlıklı | **DEĞİŞİKLİK YOK** | +12.3 | 0 |
| **SURPRISE AUDIT** | Neg | −7…−12 | 🟢 sağlıklı | **DEĞİŞİKLİK YOK** (prestij cezaları ×2 olunca kendiliğinden güçlenir) | −7…−12 | 0 |
| **VIP SERVICE** | Poz | **+1.3** | 🔴 **ÖLÇÜLEMEZ** | `isVIPServiceDay` RNG'sini KALDIR, `rewardPerBox 1.0→1.12` | **+7** | 0 |
| **BUSY DAY** | Neg | +6.2 (POZİTİF!) | 🔴 **YANLIŞ İŞARET** | `dailyCustomer 1.3→1.35` + **`customerWaitTime 1.0→0.85` EKLE** | +6 / 0 (4P) | 2.4 (**44%**) |
| **RAINY DAY** | **Poz** | −6.2 | 🔴 **YANLIŞ TİP** | **`EventType.Positive → Negative`** (etki aynı) | −6.2 | 1.2 (22%) |
| **SLOW LOGISTICS** | Neg | **0** (1P–3P) | 🔴 **ÖLÇÜLEMEZ** | `exitDelay 1.5` KALSIN + **`rewardPerBox 1.0→0.92` EKLE** | **−4.5…−5** | 0 |
| **EXPRESS CARGO** | Poz | **0** (1P–3P) | 🔴 **ÖLÇÜLEMEZ** | `exitDelay 0.7` KALSIN + **`rewardPerBox 1.0→1.08` EKLE** | **+4.5…+4.9** | 0 |
| **ANGRY CUSTOMERS** | Neg | 0 | 🟠 zayıf | `customerWaitTime 0.7→0.6` + `dailyCustomer 1.0→1.1` | 0 | 0.8 (15%) |
| **RELAXED DAY** | Poz | −6…−11 (NEGATİF!) | 🔴 **GİZLİ CEZA** | **`dailyCustomer 0.7→1.0` (gizli cezayı KALDIR)**, `customerWaitTime 1.3→1.5`, `rewardPerBox 1.0→1.10` | **+6** | 0 (merhamet event'i) |
| **FESTIVAL DAY** | Poz | +13…+15 | 🟢 sağlıklı | **DEĞİŞİKLİK YOK** (kira %10-20 kendiliğinden ölçekleniyor) | +13…+15 | 0 |
| **OPPORTUNITY DAY** | Poz | 0 | ⚪ **FAZ 3 ÖLÇER** | upgrade maliyeti ×0.8 — bu turda ölçülemez | — | — |

### 3.3 Kritik event bulguları

**① `SLOW LOGISTICS` / `EXPRESS CARGO` yapısal olarak ölçülemez.** İkisi de yalnız `exitDelay`'i
değiştiriyor; FAZ 1'de kanıtlandığı gibi tır zamanlaması OPTIMISTIC bantta **tamamen inert**
(gelir üretim-bound). `faz2.js` §8b: `exitDelay` 5→3 veya 5→8 OPTIMISTIC'te kutu/gün'ü
**hiç değiştirmiyor** (5/10/15/20 sabit). Bu yüzden ikisine de küçük bir `rewardPerBoxMultiplier`
eklenmeli — mevcut alanlarla, yeni sistem gerekmiyor.

**② `VIP SERVICE` matematiksel olarak neredeyse hiç.** `EventEffectManager.cs:523` ve `:613`:
%10 şans **TIR BAŞINA** (kutu başına değil) `rewardPerBox × 1.1`. 1 hangar ⇒ beklenen etki
`0.10 × 10% = +1%`. Açıklaması ("10% CHANCE BOXES ARE PERFECT AND EARN 10% MORE",
`EventCalendarUI.cs:172`) "kutu" diyor, kodda "perfect box" mekaniği **hiç yok**.

**③ `isGoldenBoxDay` bayrağı ölü.** `EventEffectManager.cs:633-638 IsGoldenBoxDay()` — dış çağıranı
yok. GOLDEN BOX DAY yine de çalışıyor (çarpanlar üzerinden), sadece bayrak boşta. Temizlik.

**④ `BUSY DAY` paket sonrası POZİTİF hale geliyor.** 2 paralel istasyonla kaçan müşteri sıfırlanınca
"%30 daha fazla müşteri" saf kâr olur (+2.4 prestij = günlük prestijin %44'ü). `EventType.Negative`
olarak kalması için mutlaka sabır kısıtı (`customerWaitTime 0.85`) eklenmeli — o zaman
"güçlü takım kâr eder, zayıf takım müşteri kaçırır" (beceri-kapılı, iyi tasarım).

**⑤ `RELAXED DAY` şu an NET NEGATİF.** Tipi Pozitif, açıklaması yalnız "sabır +%30" ama kodda
`dailyCustomerMultiplier = 0.7` de var (`EventEffectManager.cs:182`) → ölçülen etki **−6…−11% gelir**.
Oyuncuya söylenmeyen bir ceza; kaldırılmalı.

**⑥ `FATIGUE PROBLEM` kısmen modellenemez.** Asıl etkisi `staminaRegenRateMultiplier = 0.6` ve
`playerSprintSpeed 0.7`; sim stamina modellemiyor. Ölçülen −6% yalnız `dailyCustomer 0.8`'den geliyor.
Önerilen `playerMoveSpeed 0.9` eklemesi etkiyi ölçülebilir kılar (−14%), ama gerçek stamina etkisi
**playtest gerektirir**.

**Dokunulacak:** `Assets/NewCss/Events/EventEffectManager.cs` — `InitializeEventMultipliers()`
içindeki ilgili bloklar: BUSY `:132-144`, DELIVERY BONUS `:146-158`, ANGRY `:160-172`,
RELAXED `:174-186`, SLOW LOG `:188-200`, EXPRESS `:202-214`, HEAVY `:216-228`,
GOLDEN `:230-242`, OPPORTUNITY `:244-256`, FATIGUE `:258-270`, VIP `:272-284`,
RAINY `:286-298`, MARKETING `:300-312`. VIP RNG: `:523` ve `:613`.
Tip düzeltmesi (RAINY): `Assets/NewCss/Events/EventCalendarUI.cs:174`.

---

## §4 BEKLEME SÜRELERİ + GÜNLÜK TIR SAYISI

### 4.1 `hangarStayDuration` — cömert mi, doğru mu? → **neredeyse doğru, yalnız 1P kısa**

Duyarlılık taraması (`faz2.js` §8, gün 8, Normal, "STRICT / OPTIMISTIC" kutu/gün):

| P | mevcut | stay ×0.5 | **stay MEVCUT** | stay ×1.5 | stay ×2 |
|---|---|---|---|---|---|
| 1 | 90 sn | 3.07 / **5** | **3.51 / 5** | 3.66 / **5** | 3.72 / **5** |
| 2 | 60 sn | 5.45 / **10** | **6.49 / 10** | 6.84 / **10** | 6.97 / **10** |
| 3 | 40 sn | 7.01 / **15** | **8.82 / 15** | 9.48 / **15** | 9.73 / **15** |
| 4 | 30 sn | 8.18 / **20** | **10.75 / 20** | 11.74 / **20** | 12.14 / **20** |

**İki net sonuç:**
1. **OPTIMISTIC kolonu tamamen SABİT** → `hangarStayDuration` güçlü oynayan takım için
   **tamamen inert**. Gelir tavanını belirlemiyor.
2. **"Ölü bekleme" YOK.** `Truck.cs:372-407` "dolunca VEYA süre bitince" mantığıyla çalışıyor;
   tır dolduğu an kalkıyor. Süreyi uzatmak boş bekleme yaratmıyor, yalnız yavaş takıma tampon veriyor.
   Süreyi kısaltmak ise yalnız zayıf takımı cezalandırıyor (STRICT'te −13…−24%).

Doygunluk: ×1.5 → ×2 kazancı yalnız **+1.6…+3.4%** ⇒ mevcut değerler ~%95-97 doygunlukta,
**savurgan DEĞİL**.

**TEK DEĞİŞİKLİK ÖNERİSİ — 1P: 90 → 120 sn.** Gerekçe: 1P STRICT'te en küçük kargo bile dolmuyor
(`fillTime(kargo 2) = 100 sn > 90 sn`) ⇒ `tamDolanTır/gün = 0` ⇒ Easy "1 tır tamamla" quest'i
**matematiksel olarak imkânsız**. Önerilen P-bazlı kargoyla (`{1,2}`, §4.4) 120 sn tüm 1P kargo
değerlerini doldurulabilir yapıyor: `fillTime(1)=50`, `fillTime(2)=100` — ikisi de ≤120.
Güçlü takıma maliyeti **SIFIR** (OPTIMISTIC inert).

**Öneri:** `{90, 60, 40, 30}` → **`{120, 60, 40, 30}`**
**Dokunulacak:** `Assets/Resources/EkonomiAyarlari.asset:24` (hex `5a0000003c000000280000001e000000`
→ `780000003c000000280000001e000000`) + `Assets/NewCss/GameEconomySettings.cs:51` default.

### 4.2 `exitDelay` + `respawnDelay` ölü süresi → **DEĞİŞTİRME**

Ölçüm (`faz2.js` §8b, gün 8, Normal, kutu/gün STRICT / OPTIMISTIC):

| Ayar | 1P | 2P | 3P | 4P |
|---|---|---|---|---|
| MEVCUT 5+4+6 = 15 sn | 3.51 / **5** | 6.49 / **10** | 8.82 / **15** | 10.75 / **20** |
| anim yok 5+4 = 9 sn | 3.72 / **5** | 7.07 / **10** | 9.94 / **15** | 12.46 / **20** |
| exitDelay 3 → 13 sn | 3.57 / **5** | 6.67 / **10** | 9.16 / **15** | 11.27 / **20** |
| exitDelay 8 → 18 sn | 3.41 / **5** | 6.23 / **10** | 8.35 / **15** | 10.06 / **20** |

OPTIMISTIC'te **sıfır etki**; STRICT'te `exitDelay` 5→3 yalnız +2…+5%, 5→8 −3…−6%.
⇒ **Ekonomi kaldıracı değil, "hissiyat" değeri.** `exitDelay = 5` ve `respawnDelay = 3-5`
oldukları gibi kalsın.

⚠️ **Tek gerçek fırsat animasyon tamponunda:** varsayılan 6 sn'lik giriş/çıkış animasyonu
kaldırılsa STRICT'te **+6…+16%** kazanç var. Ama bu değer **kod-doğrulanmış değil** (animator klip
süresi sayısallaşmıyor). **Playtest'te gerçek klip süresi ölçülmeden dokunulmamalı.**
Kaynak: `Assets/Figma/Screens/Truck.prefab:196` (`exitDelay: 5`),
`The Main Office.unity:36776` (`respawnDelayRange: {x:3, y:5}`).

### 4.3 Müşteri sabrı → **15-20 → 24-32 sn (P'den BAĞIMSIZ tek değer)**

**Kök neden analizi** (`faz2.js` §10): sabır, kuyruk başındaki müşterinin servis edilmesi için
verilen süre. Servis emeği ile karşılaştırma:

| Senaryo | servis emeği | sabır (ort 17.5) | **marj** |
|---|---|---|---|
| Normal | 15 sn | 17.5 sn | **+2.5 sn** (çok dar) |
| Yavaş | 25 sn | 17.5 sn | **−7.5 sn** ⛔ **YAPISAL İMKÂNSIZ** |

Yavaş bandın prestij kanamasının kök nedeni bu: sabır, işin kendisinden kısa.

**Öneri: `minWaitTime 15 → 24`, `maxWaitTime 20 → 32`** (ort 28) ⇒ Normal marj +13 sn,
Yavaş marj +3 sn.

**Neden P-bazlı DEĞİL** (üç gerekçe):
1. Kablolama **ÖLÜ** — `DifficultyManager.cs:436-443` `FindObjectsOfType<CustomerAI>()` kullanıyor,
   sahnede `CustomerAI` yok, müşteriler `CustomerManager.cs:680`'de prefab'tan Instantiate ediliyor.
   Canlandırmak kod işi.
2. 2 paralel istasyonla servis kapasitesi **zaten P ile ölçekleniyor**; sabrın da ölçeklenmesi gereksiz.
3. `DifficultyManager`'ın tasarımı sabrı P ile **AZALTIYOR** (`patienceReductionPerPlayer`) →
   §2.3(a)'da düzelttiğim çok-oyunculu cezayı geri getirir.

**Yine de P-bazlı canlandırılırsa** önerilen değerler (asla Yavaş servis emeğinin (25 sn) altına
inmemek kuralıyla): `minWaitTime` **30 / 27 / 24 / 21**, `maxWaitTime` **38 / 35 / 32 / 29**
(ort 34 / 31 / 28 / 25).
**Dokunulacak:** `Assets/ithappy/Creative_Characters_FREE/Saved_Characters/Customer.prefab:2305-2306`.
(P-bazlı istenirse `GameEconomySettings`'e `customerPatienceByPlayerCount` dizisi +
`hangarStayDurationByPlayerCount` deseninde getter — `GameEconomySettings.cs:147-153` idiomu.)

### 4.4 GÜNLÜK TIR SAYISI — tasarım hedefi ve nasıl tutturulur

**Tasarım hedefi (mekanik tavan değil):** her tır bir "tamamlama anı"; 200–330 sn'lik bir günde
~45–60 sn'de bir doruk uygun. Ayrıca Hard quest 3 tır istiyor ⇒ ≥3-4 tam tır/gün gerekli.

> **HEDEF: 1P 3 · 2P 4 · 3P 5 · 4P 6 TAM tır/gün** (gün 8, Normal, OPTIMISTIC)

Mevcut durum: 1.43 / 2.86 / 4.29 / 5.71 — 1P ve 2P hedefin altında; **1P STRICT'te 0**.

**Kaldıraç kargo aralığı, hangar süresi değil.** `tır/gün = üretim / ortKargo`; üretim insan-bound
(değiştirilemez), ortKargo tasarım değeri. Şu an `Random.Range(2,6)` → `{2,3,4,5}` ort **3.5**,
tüm oyuncu sayıları için AYNI. Hedef için gereken ort kargo: **1.67 / 2.50 / 3.00 / 3.33**.

**ÖNERİ: kargo aralığı P-bazlı olsun** (`hangarStayDurationByPlayerCount` deseniyle birebir):

| P | mevcut | **öneri** | ort kargo | tam tır/gün OPT | hedef | kutu/gün |
|---|---|---|---|---|---|---|
| 1 | `Range(2,6)` = {2,3,4,5} | **`Range(1,3)` = {1,2}** | 1.5 | **3.33** | 3 | 5 → **5** |
| 2 | `Range(2,6)` = {2,3,4,5} | **`Range(2,4)` = {2,3}** | 2.5 | **4.00** | 4 | 10 → **10** |
| 3 | `Range(2,6)` = {2,3,4,5} | **`Range(2,5)` = {2,3,4}** | 3.0 | **5.00** | 5 | 15 → **15** |
| 4 | `Range(2,6)` = {2,3,4,5} | **`Range(2,6)` = {2,3,4,5} (değişmez)** | 3.5 | **5.71** | 6 | 20 → **20** |

🔑 **Bu değişiklik GELİR-NÖTR:** kutu/gün hiç değişmiyor (5/10/15/20 → 5/10/15/20), çünkü darboğaz
üretim. Sadece aynı kutular **daha çok, daha küçük tıra** bölünüyor. Saf pacing/hissiyat kazancı.

**Yan fayda:** 1P STRICT `tamDolanTır/gün` **0 → 2.27** ⇒ Easy "1 tır tamamla" quest'i ilk kez
mümkün oluyor (§4.1'deki 120 sn ile birlikte).

**Dokunulacak:** `Assets/NewCss/TruckScripts/TruckSpawner.cs:37-38`
(`MIN_CARGO_AMOUNT = 2` / `MAX_CARGO_AMOUNT = 6` const'ları) ve `cs:517`
(`Random.Range(MIN_CARGO_AMOUNT, MAX_CARGO_AMOUNT)`). `GameEconomySettings`'e
`cargoMinByPlayerCount = {1,2,2,2}` + `cargoMaxExclusiveByPlayerCount = {3,4,5,6}` dizileri
ve getter'lar eklenmesi önerilir (mevcut `GetHangarStayDuration` deseni, `cs:147-153`).
`Random.Range(int,int)` üst sınırın HARİÇ olduğu unutulmamalı.

**Nihai tır/gün tablosu (öneriler uygulandıktan sonra, gün 8 Normal):**

| P | tır/gün OPT | tam tır/gün OPT | tır/gün STRICT | tam tır/gün STRICT |
|---|---|---|---|---|
| 1 | 3.33 | **3.33** | 2.27 | **2.27** (was 0) |
| 2 | 4.00 | **4.00** | 2.92 | 1.46 |
| 3 | 5.00 | **5.00** | 3.88 | 1.29 |
| 4 | 5.71 | **5.71** | 4.68 | 1.17 |

Mekanik tavan (10.9–18 tır/gün) hâlâ bağlayıcı değil — bu bilinçli: tavan tampon olmalı, duvar değil.

### 4.5 Telefon → `ringDuration` 25 → **15 sn**, şans P-bazlı **{0.20, 0.25, 0.30, 0.35}**

**Sorun 1 — ekran zamanı.** `phoneRingChancePerHour = 0.30` × 10 saatlik zar = **3.0 çalma/gün**;
× `ringDuration = 25 sn` = **75 sn/gün**, yani gün 1'in (200 sn) **%37.5'i** telefon çalıyor.

**Sorun 2 — P'den bağımsız** (`PhoneCallManager.cs:425` `SetCallChance` gövdesi BOŞ).
Gelir payı bu yüzden 1P'de %13.8, 4P'de %4.2 — tesadüfi bir solo yardımı.

Tarama (`faz2.js` §11):

| şans | çalma/gün | ekran meşguliyeti (gün 8) | para/gün | gelir payı 1P → 4P |
|---|---|---|---|---|
| 0.15 | 1.5 | 37.5 sn (%15) | 25.5 | %6.9 → %2.1 |
| **0.20** | 2.0 | 50 sn (%20) | 34.0 | %9.2 → %2.8 |
| 0.30 (mevcut) | 3.0 | 75 sn (%30) | 51.0 | %13.8 → %4.2 |
| 0.50 | 5.0 | 125 sn (%50) | 85.0 | %23.0 → %7.1 |

**Öneri:** `ringDuration` **25 → 15 sn** + şans P-bazlı **{0.20, 0.25, 0.30, 0.35}**
⇒ meşguliyet 30 / 37.5 / 45 / 52.5 sn = günün **%15 / %19 / %22 / %26**'sı; gelir payı
%9.2 / %6.3 / %5.5 / %4.9 (solo yardımı korunuyor, bilinçli).
Yön DifficultyManager'ın ölü tasarımıyla aynı (daha çok oyuncu = daha çok çağrı kaldırabilir) —
ama %50 yerine %35'te durduruldu (ekran-zamanı tavanı).

**Dokunulacak:** `Assets/Resources/EkonomiAyarlari.asset:30` (`phoneRingChancePerHour: 0.3` → `0.2`)
+ YENİ `phoneRingChanceByPlayerCount = {0.20, 0.25, 0.30, 0.35}` dizisi + getter
(`GameEconomySettings.cs`, `GetHangarStayDuration` deseni) ve
`PhoneCallManager.cs:264-271 GetEffectiveRingChance()` bu getter'ı çağırsın.
`ringDuration`: `The Main Office.unity:14158` (`25` → `15`) + `PhoneCallManager.cs:40` default.
**`SetCallChance` (cs:425) ya doldurulmalı ya SİLİNMELİ** — boş gövde sahte yeşil log basıyor.

---

## §5 KRİTİK AYRIŞMA FIX'LERİ — tek doğru değer

| # | Ayrışma | Tek doğru değer | Kazanan dosya | Gerekçe |
|---|---|---|---|---|
| **A1** | `MoneySystem.startingMoney`: sahne **50 000**, config 500 | **500** | **Sahne** `The Main Office.unity:4734` → `500` | Debug kalıntısı. 500 doğrulandı: `DifficultyManager.prefab:75` `baseStartingMoney = 500`. `MoneySystem.cs:45-47` sahne değerini `_currentMoney`'e yazıyor; `DifficultyManager.cs:448-471` yalnız `HasGameEverStarted == false` ise düzeltiyor → sıra garantisi yok. `MoneySystem.cs:12` default'u da 500 (uyumlu). **YAYIN ENGELLEYİCİ.** |
| **A1-b** | Başlangıç parası P'den bağımsız (500), kira P-ölçeği 4.3× | `moneyMultiplierPerPlayer` **1.0 → 1.35** → 500 / 675 / 911 / 1230 | `DifficultyManager.prefab:81` | Kira P-ölçeği 4.3×'a çıkarken başlangıç tamponu sabit kalırsa 4P gün-4 duvarına çarpar: düz 500 ile Normal+STRICT 4P gün 4 sonrası kasa yalnız **269 TL**; 1.35 ile **~1 059 TL** (`faz2c.js` §6). Alan `[Range(0.5f,1.5f)]` içinde, yeni alan gerekmez. |
| **A2** | `realDurationInSeconds`: `.cs` **160**, sahne **200** | **200** | **Sahne kazanır**; `DayCycleManager.cs:50` default'u **160 → 200** yapılmalı | 200 sn ⇒ 16 gün = **68.5 dk** oturum (160 ⇒ 57.8 dk). 68 dk bir roguelite koşusu için doğru. Kritik: default'lar ayrık kalırsa bir prefab/sahne reset'i ekonomiyi sessizce **%25** kaydırır (`faz2.js` §13: kümülatif gelir 6 488 → 5 179). |
| **A3** | `maxQueueSize`: `.cs` **3**, sahne **2** | **2** | **Sahne kazanır**; `CustomerManager.cs:20` `DEFAULT_QUEUE_SIZE` **3 → 2** | Kuyruk büyütmek TEK BAŞINA prestiji DÜŞÜRÜR (§2.3a: 4P son prestij 37.1 → 29.8). 2 paralel istasyon uygulandıktan SONRA kuyruk 2 vs 3 farksız olur (talep < servis kapasitesi). Yani 2 her durumda güvenli. |
| **A9** | `Truck.prefab` `rewardPerBox: 10`, `penaltyPerBox: 2` | **50 / 40** | `Assets/Figma/Screens/Truck.prefab:197-198` | `[HideInInspector]` alanlar; `Truck.cs:206-218` `Resources.Load("EkonomiAyarlari")` başarılıysa 50/40 ile eziyor. Yükleme başarısız olursa kutu-başı ödül 10 TL'ye düşer = **sessiz %80 gelir kaybı**. Fallback doğru değeri taşımalı. |

**Ek hijyen (denge etkisi yok):**
- `EkonomiAyarlari.asset:17` `wealthTaxRate: 0.1` — öksüz serialized alan, karşılık gelen `.cs`
  alanı yok. Silinebilir.
- `EventEffectManager.cs:633-638 IsGoldenBoxDay()` — sıfır çağıran, ölü. Silinebilir.
- `PrestigeManager.cs:11-12` `OnPrestigeChanged` / `OnCustomerCapacityChanged` — sıfır abone.
- `PrestigeManager.cs:26-32, 108-124, 199-202` müşteri kapasitesi zinciri — dekoratif (yalnız UI metni).
- `DifficultyManager.cs:429` yorumu "4P=2.0" diyor, formül `1+(P-1)×0.3` = **1.9**. Yorum yanlış.
- `GameStateManager.cs:645-659 CheckWinCondition` doc yorumu "with prestige > 0" diyor ama kod
  prestije **bakmıyor** — yorum düzeltilmeli (ya da koşul eklenmeli; şu an prestij yalnız koşu
  ortasında 0'a düşerse öldürüyor).

---

## §6 UYGULAMA SONRASI BEKLENEN TABLO (FAZ 3 girdisi)

Nihai paket: kira `{500,1000,1550,2150}` + `g=1.35` + prestij paketi + 2 istasyon + sabır 28 +
P-bazlı kargo + telefon paketi. Normal senaryo, 1 hangar, yeniden yatırım kapalı (`faz2c.js` §3).

| P | bant | net g1 | net g8 | net g16 | **kümülatif 16** | kira toplam | **oran** | son prestij | tavan günü |
|---|---|---|---|---|---|---|---|---|---|
| 1 | OPT | 207 | 359 | 601 | **6 154** | 3 316 | **1.86** | 86.2 | — |
| 2 | OPT | 389 | 716 | 1 216 | **12 251** | 6 633 | **1.85** | 100 | 16 |
| 3 | OPT | 571 | 1 117 | 1 888 | **19 173** | 10 282 | **1.86** | 100 | 13 |
| 4 | OPT | 753 | 1 563 | 2 502 | **26 352** | 14 261 | **1.85** | 100 | 12 |
| 1 | STRICT | 138 | 195 | 286 | **3 280** | 3 316 | **0.99** | 40.6 | — |
| 2 | STRICT | 248 | 401 | 704 | **7 174** | 6 633 | **1.08** | 84.4 | — |
| 3 | STRICT | 335 | 616 | 1 106 | **10 875** | 10 282 | **1.06** | 100 | 15 |
| 4 | STRICT | 408 | 797 | 1 349 | **13 891** | 14 261 | **0.97** | 100 | 13 |

**Yavaş senaryo:** OPT 0.93–0.98 (hepsi kıl payı kazanıyor) · STRICT hepsi kaybediyor
(gün 8–16, bilinçli başarısızlık durumu).

**FAZ 3'e devredilen bütçe:** kira ödendikten sonra kalan kümülatif fazla =
**1P 2 838 · 2P 5 618 · 3P 8 891 · 4P 12 091 TL** (Normal+OPT). Upgrade fiyatlandırması bu
bütçeye sığmalı. STRICT bantta fazla **≈0** — yani upgrade'ler zayıf takım için lüks olmalı, zorunlu değil.

**FAZ 3 için üç çapraz bağımlılık:**
1. Quest asset'lerinin `prestigeReward` / `prestigePenalty` alanları **×2** (birikim ×2 ile senkron).
2. `upgradeCostMultiplierPerPlayer = 1.15` (maliyet ölçeği 1.00/1.15/1.32/1.52) artık gelir ölçeği
   **1 : 1.99 : 3.12 : 4.28** ile karşılaştırılmalı → çok oyuncuda upgrade göreli olarak **çok ucuz**.
   Doğru maliyet ölçeği için `upgradeCostMultiplierPerPlayer ≈ 1.62` (1.00/1.62/2.62/4.25) gerekir.
3. `OPPORTUNITY DAY` (upgrade maliyeti ×0.8) event'inin gerçek etkisi ancak FAZ 3'ün fiyatlarıyla
   ölçülebilir — §3.2'de "FAZ 3 ÖLÇER" olarak bırakıldı.

---

## §7 ÖLÇÜLEMEYEN / PLAYTEST GEREKTİRENLER

| Konu | Neden ölçülemedi | Ne zaman netleşir |
|---|---|---|
| `kutu/dk/oyuncu` (1.2 / 2.0 / 3.0) | Kodda zamanlı üretim kapısı yok | Playtest — **en duyarlı girdi**, tek katsayıyla tüm tablo kayar |
| Animasyon tamponu (6 sn varsayım) | Animator klip süresi sayısallaşmıyor | Klip süresini ölç → STRICT'te ±%6-16 |
| STRICT vs OPTIMISTIC hangisi gerçek | Ön-stoklama davranışı kod-doğrulanmadı | Playtest — iki bant da rapor edildi, tek sayı verilmedi |
| Stamina (FATIGUE PROBLEM, HEAVY BOXES) | Sim stamina modellemiyor | Playtest |
| `serviceCycleSeconds` / `serviceLaborSeconds` (18 / 15 sn) | Kodda servis süresi kapısı yok | Playtest — sabır önerisi (§4.3) bu ikisine bağlı |
