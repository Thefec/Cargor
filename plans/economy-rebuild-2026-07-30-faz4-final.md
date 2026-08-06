# Cargor Ekonomi Yeniden Kurulumu — FAZ 4 / 4: UZLAŞTIRMA + NİHAİ DEĞER SETİ

**Tarih:** 2026-07-30 · **Dal:** `feature/economy-balance-round`
**Girdi:** FAZ 1 (`economy-rebuild-2026-07-30.md`), FAZ 2 (`-faz2.md`), FAZ 3 (`-faz3.md`), `tools/economy-sim/sim.js` v3.1
**Bu dosya UYGULANACAK NİHAİ SETTİR.** Uygulayan kişi başka faz raporuna bakmak zorunda değil.
FAZ 2 / FAZ 3 raporları arşiv/gerekçe olarak kalır; **çeliştikleri her yerde bu dosya kazanır.**

**Bu turda `Assets/` altında hiçbir dosya değiştirilmedi.** Yalnız `tools/economy-sim/sim.js` düzeltildi.

---

## §A DÜZELTİLMİŞ GELİR TABANI + SİM'DE NE DEĞİŞTİ

### A.1 Sim'de tam olarak ne değişti (v3.0 → v3.1)

| # | Değişiklik | Kanıt (bağımsız doğrulandı, FAZ 3 iddiası olarak kabul edilmedi) |
|---|---|---|
| **D1** | `ASSUMED.startingActiveInteractables` 3 → **5**, artık `SRC.activeInteractablesAtLevel0` (varsayım değil, sahne verisi) | Sahnede `ShelfState` (guid `d02b1bd2…`) = **13** örnek; 10'u "Geniş Ambar" `levelObjects` (`unity:21193-21202`), 3'ü bağımsız. `DisplayTable` (guid `c22e4241…`) = **1**. `UpgradePanel.UpdateLevelObjects` `SetActive(i <= currentLevel)` + `InitializeLevelObjects` level 0 → `levelObjects`'ten yalnız `[0]` aktif. `CountActiveInteractables` (`CustomerManager.cs:423-436`) `FindObjectsOfType` kullanıyor → **inaktifleri saymaz**. ⇒ 3 + 1 + 1 = **5** |
| **D2** | **Masa çekişmesi** modele eklendi (v3.0'da hiç yoktu): `tableContentionEfficiency()` — M/M/c//P sonlu-kaynak kuyruğu; yeni parametre `ASSUMED.tableBusySeconds` (S = 6 sn, VARSAYIM) | Sahnede `Table` (guid `8656889b…`) = **tam 2** örnek, ikisi de "Paketleme İstasyonu" `levelObjects`'i → seviye 0'da **1 masa aktif**. `Table` tek item taşıyor (`Table.cs:57`), paketleme yalnız masada (`Table.cs:763-781`) ⇒ takımın tüm üretimi tek masadan **seri** geçiyor |
| **D3** *(FAZ 4'te eklendi)* | `SRC.serviceStations = 1` (canlı) + `customerThroughput(..., serviceStations)` parametresi | Canlı = 1: `CustomerAI.cs:582` yalnız `IsFirstInQueue` iken `BeginService`. FAZ 2'nin "2 paralel istasyon" önerisini **ölçülebilir** kıldı (önce elle hesaplanıyordu) |
| **D4** *(FAZ 4'te eklendi)* | `truckThroughput(..., cargoValues)` + `runSim({cargoValues})` | FAZ 2'nin "P-bazlı kargo" önerisini ölçülebilir kıldı; canlı varsayılan `{2,3,4,5}` değişmedi |

**Canlı değerlerle CLI çıktısı D3/D4'ten etkilenmedi** (yeni parametreler canlı değerlere default'lanıyor).
`node tools/economy-sim/sim.js` hatasız koşuyor.

### A.2 Düzeltilmiş gelir tabanı — CANLI değerler, paket ÖNCESİ (günlük net TL)

| P | bant | gün 1 | gün 4 | gün 8 | gün 12 | gün 16 | **kümülatif 16** | 16-gün kira | oran | iflas |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | OPT | 224 | 262 | 347 | 446 | 616 | **6 019** | 2 496 | 2.41 | yok |
| 2 | OPT | 384 | 455 | 615 | 800 | 1 012 | **10 438** | 4 494 | 2.32 | yok |
| 3 | OPT | 527 | 622 | 847 | 1 112 | 1 415 | **14 444** | 5 992 | 2.41 | yok |
| 4 | OPT | 649 | 762 | 1 045 | 1 377 | 1 758 | **17 744** | 7 490 | 2.37 | yok |
| 1 | STRICT | 152 | — | — | — | — | **430** | — | — | **GÜN 3** (prestij) |
| 2 | STRICT | 247 | 289 | 338 | 452 | 581 | **6 045** | 4 494 | 1.35 | yok |
| 3 | STRICT | 312 | 368 | 465 | 616 | 790 | **8 097** | 5 992 | 1.35 | yok |
| 4 | STRICT | 355 | 418 | 531 | 705 | 907 | **9 208** | 7 490 | 1.23 | yok |

**FAZ 1 §3.1/§3.2'ye göre fark:** OPT kümülatif **−7% … −13%** (en çok 4P'de, masa çekişmesi çok
oyuncuyu vuruyor). 1P STRICT iflası **gün 4 → gün 3**'e kaydı ve sebebi kira değil **prestij**
(talep 9 → 13 çıktı, 1P servis kapasitesi değişmedi).

```
DÜZELTİLMİŞ gelir ölçeği (OPT):  1.00 : 1.73 : 2.40 : 2.95     (FAZ 1: 1.00 : 1.81 : 2.44 : 3.15)
```

### A.3 En önemli yapısal sonuç: masa çekişmesi çok-oyunculu ölçeklemeyi düzleştiriyor

Çekişme verimliliği η (S = masa meşgul süresi, 1 masa):

| S | 1P | 2P | 3P | 4P |
|---|---|---|---|---|
| 4 sn | 1.000 | 0.983 | 0.962 | 0.938 |
| **6 sn (taban)** | 1.000 | **0.962** | **0.916** | **0.862** |
| 8 sn | 1.000 | 0.934 | 0.856 | 0.771 |

**S, `kutu/dk`'dan sonra ekonominin 2. en duyarlı sayısı.** S=8'de 4P geliri S=6'ya göre %8 daha düşer
ve önerilen kira ölçeği sınırda kalır (bkz. §E).

---

## §B NİHAİ DEĞER LİSTESİ

Kısaltmalar: **[F2]** FAZ 2'den değişmeden geldi · **[F2✎]** FAZ 2'nin değeri **REVİZE edildi** ·
**[F3]** FAZ 3'ten değişmeden · **[F3✎]** FAZ 3 revize · **[F4]** bu turda ilk kez belirlendi.

### B.1 Kira (`Assets/Resources/EkonomiAyarlari.asset` + `Assets/NewCss/GameEconomySettings.cs`)

| Alan | dosya:satır | mevcut | **yeni** | kaynak | gerekçe (tek cümle) |
|---|---|---|---|---|---|
| `baseRentByPlayerCount` | `EkonomiAyarlari.asset:15` · `GameEconomySettings.cs:21` · fallback `DayCycleManager.cs:593` | 500/900/1200/1500 | **500 / 1000 / 1450 / 1800** | **[F2✎]** | FAZ 2'nin `{500,1000,1550,2150}`'si paket-sonrası gelir ölçeğini `1:1.99:3.12:4.28` sanıyordu; masa çekişmesi düzeltmesiyle gerçek ölçek `1:2.04:2.94:3.67` → 3P/4P aşırı vergilendirilirdi (oran yayılımı 0.32; yenisinde **0.04**). |
| `rentGrowthMultiplier` | `asset:16` · `cs:24` | 1.15 | **1.35** | **[F2]** | Gelir her kira döngüsünde ×1.32-1.38 büyüyor; 1.15 makası açıyor, 1.35 kira baskısını 16 gün boyunca **düz** tutuyor (1.89→1.96). |
| `rentIntervalDays` / `gracePaymentPercent` | `asset:18-19` | 4 / 0.8 | **değişmedi** | — | Grace bir kez kurtarıyor, kalibrasyon buna göre yapıldı. |
| `wealthTaxRate` | `asset:17` | 0.1 | **SİL** (öksüz) | [F2] | `.cs` karşılığı yok (9d2c3b0'da kaldırıldı), etkisi sıfır. |

**Nihai kira serisi (g = 1.35):**

| P | gün 4 | gün 8 | gün 12 | gün 16 | 16-gün toplam |
|---|---|---|---|---|---|
| 1 | 500 | 675 | 911 | 1 230 | **3 316** |
| 2 | 1 000 | 1 350 | 1 823 | 2 460 | **6 633** |
| 3 | 1 450 | 1 958 | 2 643 | 3 568 | **9 619** |
| 4 | 1 800 | 2 430 | 3 281 | 4 429 | **11 940** |

### B.2 Başlangıç parası

| Alan | dosya:satır | mevcut | **yeni** | kaynak | gerekçe |
|---|---|---|---|---|---|
| `MoneySystem.startingMoney` (sahne) | `The Main Office.unity:4734` | ~~50 000~~ **500** | — **çözüldü** | — | Müdür uyguladı, listeden düştü. |
| `moneyMultiplierPerPlayer` | `DifficultyManager.prefab:81` · `cs:61` | 1.0 | **1.2** | **[F2✎]** | Ölçüt "gün-4 kirası sonrası kasa P'den bağımsız olsun": 1.2 → STRICT kasa 572/599/622/724 (≈düz); FAZ 2'nin 1.35'i yeni yumuşak kira ölçeğiyle 4P'ye 2× tampon verirdi (572/674/813/1090). |

### B.3 Prestij paketi (birikim ×2 + eşik ×2) — **§B.4 ile AYRILAMAZ**

| Alan | dosya:satır | mevcut | **yeni** | kaynak | gerekçe |
|---|---|---|---|---|---|
| `customerServedPrestigeBonus` | `asset:36` · `cs:105` | 0.2 | **0.4** | [F2] | Birikim ×2: prestij 0-100 skalasının tamamını kullansın. |
| `customerLostPrestigePenalty` | `asset:35` · `cs:102` | −0.6 | **−0.4** | **[F2✎]** | Düzeltilmiş talep (13/16/20/24) ile FAZ 2'nin −0.5'i **1P STRICT'i gün 7'de öldürüyor**; −0.4 (1:1 asimetri) tüm bantlarda iflası kaldırıyor, 4P disiplinini bozmuyor. |
| `wrongDeliveryPrestigePenalty` | `asset:39` · `cs:114` | −0.08 | **−0.16** | [F2] | Ölçek senkronu (×2). |
| `wrongProductPrestigePenalty` | `asset:37` · `cs:108` | −0.04 | **−0.08** | [F2] | Ölçek senkronu. |
| `boxDropPrestigePenalty` | `asset:38` · `cs:111` | −0.02 | **−0.04** | [F2] | Ölçek senkronu. |
| `callPrestigeReward` | `asset:34` · `cs:93` | 0.2 | **0.4** | [F2] | Ölçek senkronu. |
| `prestigePerBonus` | `asset:25` · `cs:54` | 4 | **8** | [F2] | Eşik ×2 → ödül eğrisi gelir-nötr kalır. |
| `startingPrestige` | `unity:25234` · `PrestigeManager.cs:16` | 6 | **12** | [F2] | Ölçek senkronu. |
| `maxPrestige` | `unity:25235` · `PrestigeManager.cs:19` | 100 | **100 (değişmedi)** | [F2] | 110'a çıkarmak tavan gününü yalnız +1-2 gün öteliyor (4P 10→11) ve geç-oyun gelirini %2-5 şişiriyor; playtest'te "geç oyun düz" hissi çıkarsa 110 ilk aday (bkz. §E). |
| Perk senkronu — `prestige_master` | `PerkEffect.cs:89-93` | `0.2f + 0.06f*lvl` | **`0.4f + 0.12f*lvl`** | [F3 C5(a)] | Taban ×2 olunca perkin göreli gücü %30→%15'e düşerdi; etkiyi de ×2 yapıp **fiyatı 175'te tut**. |
| Perk senkronu — `leveraged_rent` | `PerkEffect.cs:161-166` | `= -1.2f` | **`= -0.8f`** *(etki değişikliği yapılmazsa)* | **[F2✎]** | Yeni taban −0.4'ün 2 katı; ama tercih edilen çözüm §B.7'deki **etki değişikliği** (bu satır tamamen kalkar). |

### B.4 Müşteri servisi — **2 PARALEL İSTASYON (paketin kalbi, ayrılamaz)**

| İş | dosya:satır | mevcut | **hedef** | kaynak | gerekçe |
|---|---|---|---|---|---|
| Paralel servis istasyonu | `CustomerManager.cs:124` (`serviceTables[]`), `:865-868` (`IsFirstInQueue`), `:873` (`AssignDropOffTable`, sıfır çağıran) + `CustomerAI.cs:580-586` + sahne `unity:68615-68616` (dizi 1 elemanlı) | 1 (seri) | **2** | **[F2 — FAZ 4'te DOĞRULANDI]** | Ölçüm (gün 8, prestij/gün OPT): 1 istasyon **3.73 / 3.55 / 3.55 / 3.55** (düz-ters) → 2 istasyon **5.20 / 6.40 / 8.00 / 8.45** (monoton artan). Tek düzeltme ters ölçeklemeyi çeviriyor. |
| `maxQueueSize` | `unity:68600` · `CustomerManager.cs:20` `DEFAULT_QUEUE_SIZE` | sahne 2, `.cs` 3 | **2** (`.cs` default'u da 2) | [F2] | Kuyruk büyütmek TEK BAŞINA prestiji düşürüyor; 2 istasyondan sonra kuyruk 2 vs 5 farkı 1P-3P'de **0**, 4P'de **−519 TL** → 2 her durumda güvenli. |
| Müşteri sabrı `minWaitTime/maxWaitTime` | `Customer.prefab:2305-2306` | 15 / 20 | **24 / 32** | [F2] | Yavaş bantta servis emeği 25 sn > sabır 17.5 sn = yapısal imkânsızlık; sim'de ölçülmez, `serviceCycleSeconds` varsayımına dayanır (playtest kalemi). |
| Talep formülü `_shelfMultiplier` | `unity:68602` | 2 | **2 (değişmedi)** | [F4] | 1.5'e indirmek 1P STRICT'i kurtarıyor ama Geniş Ambar'ın marjinal etkisini de yarıya indiriyor; aynı kurtarma `customerLostPrestigePenalty = −0.4` ile TEK float değiştirerek sağlandı. |

### B.5 Bekleme süreleri / tır

| Alan | dosya:satır | mevcut | **yeni** | kaynak | gerekçe |
|---|---|---|---|---|---|
| `hangarStayDurationByPlayerCount` | `asset:24` · `cs:51` | 90/60/40/30 | **120 / 60 / 40 / 30** | [F2] | 1P STRICT'te en küçük kargo bile dolmuyordu (`fillTime(2)=100 > 90`) → "1 tır tamamla" quest'i imkânsızdı; güçlü takıma maliyeti sıfır (OPT'ta inert). |
| Kargo aralığı (P-bazlı) | `TruckSpawner.cs:37-38, 517` + `GameEconomySettings`'e yeni dizi | `Range(2,6)` hepsinde | **1P `{1,2}` · 2P `{2,3}` · 3P `{2,3,4}` · 4P `{2,3,4,5}`** | [F2] | **Gelir-nötr doğrulandı** (kümülatif fark ≤ %1.5); tam dolan tır/gün OPT **3.33 / 3.85 / 4.58 / 4.92** olur, 1P STRICT 0 → 2.27. `Random.Range(int,int)` üst sınır HARİÇ. |
| `exitDelay` / `respawnDelayRange` | `Truck.prefab:196` · `unity:36776` | 5 / 3-5 | **değişmedi** | [F2] | OPTIMISTIC'te sıfır etki, STRICT'te ±%3-6 → ekonomi kaldıracı değil. |
| `Truck.prefab` fallback ödül/ceza | `Truck.prefab:197-198` | 10 / 2 | **50 / 40** | [F2 A9] | `Resources.Load` başarısız olursa kutu ödülü 10 TL'ye düşer = sessiz %80 gelir kaybı. |
| `rewardPerBox` / `penaltyPerBox` / `bonusPerTier` | `asset:21,22,26` | 50 / 40 / 5 | **değişmedi** | [F1] | Tüm kalibrasyon bunların üstüne kuruldu. |
| `realDurationInSeconds` `.cs` default | `DayCycleManager.cs:50` | 160 | **200** (sahneyle hizala) | [F2 A2] | Ayrık kalırsa bir sahne reset'i ekonomiyi sessizce %25 kaydırır. |

### B.6 Telefon — **OPSİYONEL TASARIM KARARI, bug fix DEĞİL**

> ⚠️ FAZ 1'in "A5 `SetCallChance` boş gövde = bug" tespiti **yanlış çerçevelemeydi**.
> `PhoneCallManager.cs:421-424` yorumu bunun bilinçli olduğunu söylüyor: reaktif V3 tasarımında şans
> doğrudan `GameEconomySettings.phoneRingChancePerHour`'dan okunuyor, `DifficultyManager.ScaledPhoneCallChance`
> ölü yol. Telefonu P-bazlı yapmak bir **tasarım değişikliğidir.**

| Alan | dosya:satır | mevcut | **öneri** | kaynak | gerekçe |
|---|---|---|---|---|---|
| `ringDuration` | `unity:14158` · `PhoneCallManager.cs:40` | 25 sn | **15 sn** | [F2] | 3 çalma/gün × 25 sn = günün %37.5'i telefon çalıyor; ekran-zamanı hijyeni, gelir etkisi yok. |
| `phoneRingChancePerHour` | `asset:30` | 0.30 | **0.20** + YENİ `phoneRingChanceByPlayerCount = {0.20, 0.25, 0.30, 0.35}` | [F2, **opsiyonel**] | Gelir payını 1P %9.2 → 4P %4.9'a taşır (solo yardımı bilinçli korunur). **Uygulanmazsa** ekonomi bozulmaz; nihai tablo bu öneriyle koşuldu, sabit 0.30 ile 1P geliri ~%3 artar. |
| `SetCallChance` boş gövdesi | `PhoneCallManager.cs:425` | boş | **SİL** (P-bazlı yapılmayacaksa) veya doldur | [F4] | Bug değil ama `DifficultyManager.ApplyPhoneSettings` sahte yeşil log basıyor. |

### B.7 Upgrade / perk

| Alan | dosya:satır | mevcut | **yeni** | kaynak | gerekçe |
|---|---|---|---|---|---|
| **`upgradeCostByPlayerCount`** (YENİ dizi) | `DifficultyManager.cs:348-356` · `prefab:85` | tek skaler 1.15 | **DİZİ `{1.00, 2.00, 2.95, 3.70}`** | **[F2✎ + F3✎]** | **§C'deki karar** — bkz. B.8. |
| `Geniş Ambar` `maxLevel` / `baseCost` / `costStep` | `unity:21184-21186` | 9 / 50 / 10 | **2 / 60 / 30** (toplam 150) | **[F3✎, F3-C3 GERİ ALINDI]** | FAZ 3 C3 "2 istasyonla değer kazanıyor, maxLevel 3 / 450 TL" diyordu; düzeltilmiş talepte ölçüm **1P −176 · 2P +278 · 3P −63 · 4P −79 TL** → yalnız 2P L1 pozitif. Kart bir *ekonomi* kartı değil, fiziksel stok tamponu. |
| `Paketleme İstasyonu` `maxLevel` / `baseCost` | `unity:21214-21215` | 3 / 100 | **1 / 150** | [F3] | Sahnede 2 `Table` var → seviye 2-3 hiçbir obje aktifleştirmiyor. Nihai pakette L1 değeri **0 / 476 / 1 535 / 3 123 TL** (S=6). |
| `Ek Hangar` `maxLevel` / `baseCost` | `unity:21302-21303` | 2 / 200 | **1 / 200** | [F3] | 3. hangar her iki bantta 0 TL; 2. hangar yalnız STRICT'te değerli. |
| `Görev Kademesi` | `unity:21371-21372` | 80 / 20 | **80 / 20 (dokunma)** | [F3] | Fiyat değil içerik sorunu; §B.9'daki quest düzeltmeleri sonrası ROI 1.5 / 1.7. |
| `all_in` | `unity:21666` | 800 | **320** | [F3] | ROI 1.18-1.49 → 2.9-3.7. |
| `prestige_broker` baseCost/costStep + etki | `unity:21410-21411` · `PerkEffect.cs:82-86` | 510 / **−5** / `+0.5/lvl` | **130 / +15 / `+1.0/lvl`** | [F3] | `costStep: −5` seviye 2'yi seviye 1'den ucuz yapıyor (açık hata); etki 2× + fiyat 130 = anlamlı kart. |
| `high_volatility` | `unity:21626` | 450 | **320** | [F3] | ROI 1.80-2.22 → 2.5-3.1. |
| `gambler_case` | `unity:21546` | 400 | **350** | [F3] | ROI 2.17-2.76 → hedef 3.0. |
| `prestige_master` | `unity:21430-21431` | 280 / 100 | **175 / 25** (+ etki ×2, bkz. B.3) | [F3] | Mutlak TL'de en güçlü perk; 280'de ROI 1.5-2.0. |
| `cheap_rent` | `unity:21391-21392` | 130 / 30 | **130 / 30 (dokunma)** ve `rentScaledMultiplier` etki değişikliği **GEREKMİYOR** | [F3 C4] | `g` 1.15→1.35 olunca kira toplamı 1P 2 497→3 316, 4P 7 490→**11 940** → perk kendiliğinden %26-60 değerlendi. |
| `leveraged_rent` baseCost + etki | `unity:21606` · `PerkEffect.cs:161-166` | 350 · kira×0.8 + `lostPenalty=−1.2` | **300** · **kira×0.75 + `gracePaymentPercent = 0`** | [F3 C4] | Bedeli doymuş müşteri döngüsünden çıkar (mevcut hâli her P'de negatif); `all_in` ile **aynı dışlama grubuna** al (ikisi de grace siliyor). |
| `fast_hangar` | `unity:21450` | 280 | **120** | [F3] | OPT'ta değer 0 (hangar penceresi bağlayıcı değil), yalnız STRICT'te işe yarıyor. |
| `patient_customers` | `unity:21507` · `PerkEffect.cs:121-125` | 220 | **120** · *(tercih: etkiyi `interactionTime` 2 → 1.2 sn yap)* | [F3] | Sabır bağlayıcı kısıt değil; servis döngüsünü kısaltmak prestij darboğazına saldıran ilk perk olur. |
| `energetic_crew` | `unity:21469` | 160 | **100** | [F3] | Stamina'nın ekonomik modeli yok; kapalı `Dinç Ekip` omurgasının duplikesi. |
| `bulk_buy` | `unity:21685` | 150 | **80** | [F3] | Değeri tam "aldığın kartın %50'si" → 150'de nakit-nötr. |
| `long_queue` | `unity:21531` · `PerkEffect.cs:128-132` | `disabledInDraft: 0` | **1** *(veya etkiyi değiştir)* | **[F3✎ — şiddeti düştü]** | 2 istasyondan SONRA zarar **−449…−5 392 TL değil**, ölçüm: 1P/2P/3P **0**, 4P **−519**. Artık "aktif zararlı" değil **ölü/hafif zararlı** kart → P0 değil P2. |
| `overtime` | `PerkEffect.cs:196-200` | `= 160f + 20f` (=180) | **`= _baseRealDuration × 1.125f` (200 → 225)**, fiyat **300 (dokunma)** | [F3] | Canlı hâli günü %10 KISALTIYOR; fix deseni §C.1'de. |
| `agile_crew` / `phone_line` / `emergency_brake` | — | 180 / 160 / 250 | **dokunma** | [F3] | Yeni P-çarpanıyla hedefe oturuyorlar. `emergency_brake` `tier: 1 → 0` önerisi **artık zayıf gerekçeli** (1P STRICT nihai pakette iflas etmiyor) → zararsız, düşük öncelik. |
| Reroll maliyeti | `UpgradePanel.cs:1057, 1071, 1100` · `RerollCurve.cs:8` | P çarpanı YOK | **P çarpanı uygula** (tablo 50/90/160/290/525 aynı) | [F3] | 4P'de reroll göreli 3.7× ucuz kalıyor. **Uyarı:** 1P'de günde 2 reroll × 16 gün = 2 240 TL = harcanabilir bütçenin **%75'i** (FAZ 3'te %56'ydı, bütçe daraldı) — playtest'te izle. |

### B.8 `upgradeCostMultiplierPerPlayer` çakışması — **KARAR: DİZİ**

FAZ 2 tek skaler **1.62**, FAZ 3 dizi **{1.00, 2.00, 3.10, 4.25}** demişti. Düzeltilmiş gelir ölçeğiyle
ikisi de bayat. Ölçüm:

```
gelir ölçeği OPT    : 1.00 : 2.04 : 2.94 : 3.67
gelir ölçeği STRICT : 1.00 : 2.28 : 3.55 : 4.60
```

| Aday | ölçek | OPT ölçeğinden sapma |
|---|---|---|
| skaler 1.50 | 1.00 / 1.50 / 2.25 / 3.38 | 0 / **−26.5%** / **−23.5%** / −8.0% |
| skaler 1.543 (en iyi geometrik uyum) | 1.00 / 1.54 / 2.38 / 3.67 | 0 / **−24.5%** / **−19.1%** / −0.1% |
| skaler 1.65 | 1.00 / 1.65 / 2.72 / 4.49 | 0 / −19.1% / −7.6% / **+22.2%** |
| **DİZİ {1.00, 2.00, 2.95, 3.70}** ✅ | — | **0 / −2.0% / +0.2% / +0.7%** |

**KARAR: DİZİ `{1.00, 2.00, 2.95, 3.70}`.**
**Gerekçe (kod değişikliğini haklı çıkaran):** gelir ölçeği 1→2 arasında dik, sonra düzleşiyor
(`2.04 → 2.94 → 3.67` = ×1.44, ×1.25). Geometrik bir `m^(P-1)` bu şekli **yapısal olarak** üretemez;
en iyi skaler bile 2P/3P'de **%19-25** sapıyor — bu, 2-3 oyunculu takıma tüm içerik kataloğunda
bir kademe ucuz upgrade demek. Ayrıca aynı dizi quest ödülü ve reroll için de kullanılacak
(tek `ECONOMY_SCALE_BY_PLAYERS` kaynağı).

**Uygulama:** `DifficultyManager.cs:348-356` `CalculateUpgradeCostMultiplier()` yerine
`baseRentByPlayerCount` desenindeki dizi + clamp'li getter; `prefab:85`'teki `[Range(1f,2f)] float`
alanı **kaldırılır** (aksi hâlde Inspector'da ölü bir alan kalır).

**Kod değişikliği reddedilirse tek kabul edilebilir skaler: `1.55`** (→ 1.00/1.55/2.40/3.72);
kabul edilen sapma **2P −24%, 3P −18%** — yani 2-3 oyunculu takım için upgrade'ler bilinçli olarak
ucuz kalır, 1P ve 4P doğru fiyatlanır. Bu bir tolerans değil, **kabul edilen bir denge borcu**.

### B.9 Quest (30 canlı asset — `Assets/Resources/Quests/`)

> P0 raf exploit'i (`BoxInfo.countedForShelfQuest` + `ShelfState.cs:604-612` dedup) **KAPANDI** →
> FAZ 3'ün quest tablosunun ön koşulu karşılandı, tablo uygulanabilir.

| Kalem | mevcut | **yeni** | kaynak | gerekçe |
|---|---|---|---|---|
| **D1 — havuz politikası** (`QuestManager.cs:471-499`) | 3 teklif, `tier ≤ maxTier` havuzundan rastgele | **3 teklifin her biri FARKLI tier'dan** (T2: 1 Easy + 1 Med + 1 Hard) | [F3] | Üst tier açılınca havuz seyreliyor, Hard ödülü hiç masaya gelmiyor → `Görev Kademesi` negatif değerli. |
| **D2 — `targetCount` P-ölçekli** (`QuestManager.cs:462` civarı) | sabit | `etkinHedef = max(1, round(target × ECONOMY_SCALE[P]))`, **`AnswerPhone` ve `CompleteTruck` ölçeklenmez** | **[F3✎]** | Ölçek vektörü `{1.00, 2.00, 2.95, 3.70}`; gerçek üretim ölçeği `1 : 1.92 : 2.75 : 3.45` olduğu için hedefler 3P/4P'de ~%5-7 zorlaşır (kabul: çok oyuncu biraz daha zor olsun). Tır quest'i kargo boyutu P ile küçüldüğü için **çift sayma olur**, telefon arzı P-flat. |
| **D3 — tier başına TEK ödül/ceza çifti** | Easy 18/28, Med 34/52, Hard 57/86 dağınık | **Easy 28 / −15 · Medium 60 / −27 · Hard 150 / −53** | [F3] | Ceza oranı tier yükseldikçe düşer (%55 → %45 → %35), EV 22.4 / 34.8 / 58.7 TL. 1P gün-8 net geliri **352 TL** (FAZ 3 varsayımı 367 — %4 fark, tablo geçerli). |
| **D3-prestij — ×2 senkron** | Easy 0.7/−0.4, Med 1.2/−0.6, Hard 2.3/−1.2 | **Easy +1.40 / −0.80 · Medium +3.00 / −1.36 · Hard +7.50 / −2.66** | [F3 C7] | Prestij birikimi ×2 olduğu için quest prestij katkısı aksi hâlde yarıya düşer. Para ödülleri prestij paketinden **bağımsız**, değişmez. |
| **D4 — ödül P-ölçeklemesi** (opsiyonel) | sabit ödül | ödül × `ECONOMY_SCALE[P]` | [F3] | Sabit bırakılırsa quest 1P koşusunun %10.2'si, 4P'nin %3.2'si (bilinçli solo can simidi olabilir). |
| **`CompleteTruck` tier merdiveni** | Easy 1 / Med 2 / Hard 3 | **Easy 1 · Medium 2 · Hard 3 (aynen kalsın)** | [F3 C6] | P-bazlı kargo ile tam dolan tır/gün OPT **3.33 / 3.85 / 4.58 / 4.92** → p ≈ %87 / %87 / %62. |
| Gün 16 settlement | ödül/ceza hiç yatmıyor | `AssignDailyQuests` başında `if (currentDay >= MAX_DAYS) return;` **veya** gün-16 bitişine `SettleAcceptedQuestsForDayEnd()` | [F3] | Son gün quest'i cezasız bedava opsiyon. |
| `hasBuff` (30/30 asset'te 0) | buff yok | Hard tier'a 1-2 buff bağla **ya da** UI'dan kaldır | [F3] | Ölü sistem oyuncuya görünüyor. |

### B.10 Event'ler — **FAZ 2 §3.2 aynen geçerli, gelir ölçeğine bağlı değil**

Değişmedi: GOLDEN BOX (`rewardPerBox 1.3→1.15`, `moveSpeed 1.2→1.08`, `customer 1.2→1.15`),
CUSTOMER SUPPORT (`phoneRingEventMultiplier 1.5→2.0`), FATIGUE (`customer 0.8→0.85` + `moveSpeed 0.9` EKLE),
HEAVY BOXES (`moveSpeed 0.8→0.85`), VIP SERVICE (RNG kaldır, `rewardPerBox 1.12`),
BUSY DAY (`customer 1.3→1.35` + `waitTime 0.85` EKLE), **RAINY DAY `EventType.Positive → Negative`**
(`EventCalendarUI.cs:174`), SLOW LOGISTICS (`rewardPerBox 0.92` EKLE), EXPRESS CARGO (`rewardPerBox 1.08` EKLE),
ANGRY (`waitTime 0.7→0.6`, `customer 1.1`), **RELAXED DAY gizli `dailyCustomer 0.7` cezasını KALDIR**
(`EventEffectManager.cs:182`), MARKETING / DELIVERY BONUS / SURPRISE AUDIT / FESTIVAL DAY **dokunma**.
Dosya: `Assets/NewCss/Events/EventEffectManager.cs` `InitializeEventMultipliers()` blokları.

⚠️ **SURPRISE AUDIT** (tüm cezalar ×2) prestij cezaları ×2 olunca kendiliğinden güçlenir — ek ayar yok.

### B.11 Nihai beklenen tablo (tüm paket uygulanmış hâlde)

Normal senaryo, 1 hangar, 1 masa, yeniden yatırım kapalı, S = 6 sn:

| P | bant | g1 | g4 | g8 | g12 | g16 | **kümülatif** | kira | **oran** | kira baskısı (4 dönem) | son prestij | tavan günü |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | OPT | 207 | 264 | 352 | 480 | 628 | **6 299** | 3 316 | **1.90** | 1.89 / 1.92 / 1.90 / 1.96 | 100 | 16 |
| 2 | OPT | 376 | 517 | 775 | 1 037 | 1 227 | **12 852** | 6 633 | **1.94** | 1.93 / 1.74 / 1.76 / 2.00 | 100 | 12 |
| 3 | OPT | 527 | 723 | 1 089 | 1 532 | 1 734 | **18 541** | 9 619 | **1.93** | 2.01 / 1.80 / 1.73 / 2.06 | 100 | 11 |
| 4 | OPT | 657 | 899 | 1 358 | 1 914 | 2 167 | **23 135** | 11 940 | **1.94** | 2.00 / 1.79 / 1.71 / 2.04 | 100 | 10 |
| 1 | STRICT | 138 | 149 | 159 | 182 | 205 | **2 658** | 3 316 | **0.80** | 3.36 / 4.25 / 5.01 / 6.00 | 8.4 | — |
| 2 | STRICT | 240 | 260 | 332 | 446 | 610 | **6 059** | 6 633 | **0.91** | 3.85 / 4.07 / 4.09 / 4.03 | 63.1 | — |
| 3 | STRICT | 311 | 367 | 500 | 740 | 1 025 | **9 424** | 9 619 | **0.98** | 3.95 / 3.92 / 3.57 / 3.48 | 100 | 16 |
| 4 | STRICT | 360 | 457 | 701 | 1 045 | 1 184 | **12 228** | 11 940 | **1.02** | 3.94 / 3.47 / 3.14 / 3.74 | 100 | 11 |

**Yavaş senaryo:** OPT oran 0.80 / 1.00 / 0.99 / 1.04 (hiç iflas yok) · STRICT hepsi kaybediyor
(gün 5-12, bilinçli başarısızlık durumu).

**Sağlanan hedefler:** ① OPT kira baskısı 16 gün boyunca **düz** (1.71-2.06), oyuncu sayıları arası
oran yayılımı **0.04** ② prestij/gün P ile **monoton artan** ③ **hiçbir bantta prestij ölümü yok**
(mevcut: 1P STRICT gün 3) ④ 1P STRICT hâlâ en zor bant (0.80) ama kaybetmiyor.

**FAZ 3'e devredilen bütçe** (kira sonrası kümülatif fazla, Normal+OPT):
**1P 2 983 · 2P 6 219 · 3P 8 922 · 4P 11 195 TL** → oran `1 : 2.08 : 2.99 : 3.75` (maliyet dizisiyle uyumlu).
FAZ 3 §6 fiyat listesi toplam içerik maliyeti 1P **4 210 TL** → **kapsama %141** (her P'de %135-141).
**FAZ 3 fiyatları ölçeklenmedi** — bütçe FAZ 3'ün varsaydığı 2 838 TL'den yalnız %5 farklı.
STRICT bantta fazla **negatif** (−658 / −574 / −195 / +288) ⇒ upgrade'ler zayıf takım için **lüks**.

---

## §C ÖN KOŞULLAR — ekonomi değerinden ÖNCE düzelmesi gereken kod işleri

### C.1 `overtime` ters etki (P0) — fix deseni

`PerkEffect.cs:199`:
```csharp
ctx.DayCycle.realDurationInSeconds = 160f + 20f;   // = 180, sahne tabanı 200 → gün %10 KISALIYOR
```
**Çakışma:** `BuffManager.cs:550` aynı alana `realDurationInSeconds += amount` ile **kalıcı buff** yazıyor.
Yani alan iki sahipli: biri mutlak atıyor, diğeri birikimli ekliyor. `HandleUpgradeLevelsChanged` tüm
client'larda tetiklendiği için `*=` da kullanılamaz (idempotent değil).

**Önerilen desen — "taban + katkı yeniden hesaplama" (tek yazıcı):**
1. `DayCycleManager`'a `[SerializeField] float _baseRealDuration` ekle; `Awake`'te bir kez
   `_baseRealDuration = realDurationInSeconds` (sahne değeri 200) ile doldur ve **bir daha yazma**.
2. İki katkıyı ayrı sakla: `_overtimeMultiplier` (perk, default 1.0) ve `_buffDurationBonusSeconds`
   (BuffManager'ın topladığı saniye, default 0).
3. Tek bir `RecomputeDayDuration()`:
   `realDurationInSeconds = _baseRealDuration * _overtimeMultiplier + _buffDurationBonusSeconds;`
4. `ApplyOvertime(level, ctx)` → `_overtimeMultiplier = (level > 0) ? 1.125f : 1f; RecomputeDayDuration();`
   (200 → **225**, +%12.5). Level 0'da 1.0'a **geri dönmesi** şart — perk kaldırıldığında/yeniden
   uygulandığında sürüklenme olmasın.
5. `BuffManager.cs:550` → `_buffDurationBonusSeconds += amount; RecomputeDayDuration();`
   (doğrudan `realDurationInSeconds`'a yazmayı bırak.)
6. **Etkileşim kararı:** perk **çarpımsal**, buff **toplamsal**, sıra `taban × perk + buff`.
   Gerekçe: perk kalıcı bir yüzde iyileştirme, buff geçici bir mutlak ekleme; bu sıra buff'ın
   değerini perk seviyesine bağımlı kılmaz (playtest'te iki kaynak ayrı ayrı okunabilir).

⚠️ Fix'ten önce fiyat **300 TL yanlış** (kart −612…−1 937 TL değerinde); fix'ten sonra **300 doğru**.

### C.2 Ölü müşteri sabrı kablolaması (P1)

`DifficultyManager.ApplyCustomerSettings` (`cs:436-443`) `FindObjectsOfType<CustomerAI>()` ile sahnedeki
örnekleri yamalıyor; **sahnede `CustomerAI` yok** — müşteriler `CustomerManager.cs:680`'de prefab'tan
Instantiate ediliyor. `baseMinPatience 8 / baseMaxPatience 14 / patienceReductionPerPlayer 2` hiç
uygulanmıyor. §B.4'teki 24/32 değerleri **prefab'a** yazılmalı (`Customer.prefab:2305-2306`); ya kablolama
onarılsın ya da bu üç prefab alanı silinsin — şu hâliyle `.cs` okuyan biri yanlış sayıyı gerçek sanıyor.

### C.3 `prestige_broker` negatif `costStep` (P1)

`unity:21411` `costStep: -5` → seviye 2, seviye 1'den **ucuz**. Lineer maliyet formülü
(`UpgradePanel.cs:1186-1197`) negatif adımı engellemiyor. Değer **+15** yapılmalı (§B.7) ve
`UpgradePanel`'e `costStep < 0` için bir uyarı/clamp eklenmesi önerilir.

### C.4 Sahne ↔ `.cs` default hizalaması (P1, denge değişikliği YOK)

| Alan | `.cs` default | canlı | yapılacak |
|---|---|---|---|
| `DayCycleManager.realDurationInSeconds` | 160 | 200 | `.cs` → 200 |
| `CustomerManager.DEFAULT_QUEUE_SIZE` | 3 | 2 | `.cs` → 2 |
| `Truck.prefab` `rewardPerBox`/`penaltyPerBox` | 50/40 (SO) | prefab 10/2 | prefab → 50/40 |
| `GameEconomySettings` default'ları | eski | §B'deki yeni değerler | asset ile birlikte `.cs` default'ları da güncelle |
| `DayCycleManager.cs:593` kira fallback'i | 500/900/1200/1500 | — | → 500/1000/1450/1800 |

Gerekçe: bir prefab/sahne reset'i bugün ekonomiyi **sessizce %25 kaydırıyor**.

### C.5 Kozmetik / doğruluk (P2)

- `DifficultyManager.cs:429` yorumu "4P=2.0" diyor, formül `1+(P-1)×0.3` = **1.9**.
- `GameStateManager.cs:645-659` doc yorumu "with prestige > 0" diyor, kod prestije **bakmıyor**.
- `EventEffectManager.cs:633-638 IsGoldenBoxDay()` sıfır çağıran, ölü.
- `PrestigeManager.cs:11-12` `OnPrestigeChanged` / `OnCustomerCapacityChanged` sıfır abone;
  `cs:26-32, 108-124, 199-202` müşteri kapasitesi zinciri dekoratif.
- `Q_Hard_2_Shelf` / `Q_Hard_4_Pack` id'leri `hard_*_10` ama `targetCount: 12` — ID'ye güvenip sayı okuma.

---

## §D UYGULAMA SIRASI + COMMIT BÖLÜNMESİ

| # | Commit | İçerik | Bağımlılık |
|---|---|---|---|
| **1** | `fix(economy): overtime ters etki + prestige_broker negatif costStep + default hizalama` | §C.1 fix deseni · §C.3 · §C.4 tablosu · §C.5 kozmetik | Yok. **Ekonomi değeri içermez** — tek başına merge edilebilir, kontrol kapısı hafif. |
| **2** | `feat(customer): 2 paralel servis istasyonu` | `CustomerManager.cs:124/865-868/873` + `CustomerAI.cs:580-586` + sahne `serviceTables` dizisi 2 eleman | Yok, ama **#3 ondan önce merge EDİLMEMELİ** |
| **3** | **`balance(economy): kira P-ölçeği + eğim + prestij paketi`** ⚠️ **BÖLÜNEMEZ** | §B.1 kira (`{500,1000,1450,1800}`, g=1.35) · §B.2 `moneyMultiplierPerPlayer 1.2` · §B.3 prestij paketinin **9 alanı + 2 perk senkronu** · §B.5 `hangarStay {120,60,40,30}` | **#2 ZORUNLU ÖN KOŞUL.** Prestij paketi 2 istasyon olmadan 3P/4P'yi ters ölçekler; kira ölçeği prestij paketi olmadan 4P'yi haksız cezalandırır; `moneyMultiplierPerPlayer` yeni kira ölçeğine kalibre. **Üçü aynı commit.** |
| **4** | `feat(truck): P-bazlı kargo aralığı` | §B.5 kargo dizileri + `GameEconomySettings` getter'ları | #3 (aynı PR olabilir; gelir-nötr olduğu için ayrı commit'te izlenebilir olması tercih edilir) |
| **5** | `balance(upgrade): maliyet P-dizisi + maxLevel kısmaları + perk fiyatları` | §B.7 tamamı + §B.8 dizi + reroll P-ölçeklemesi | **#3 ZORUNLU** (fiyatlar #3 sonrası bütçeye kalibre) |
| **6** | `balance(quest): tier ödül tablosu + havuz politikası + P-ölçekli hedef` | §B.9 D1+D2+D3 + prestij ×2 + gün-16 settlement | **#3 ZORUNLU** (prestij ×2 senkronu). D1+D2+D3 **birlikte** — ayrı ayrı işe yaramaz |
| **7** | `balance(events): 12 event yeniden dengeleme + RAINY tip + RELAXED gizli ceza` | §B.10 | #3 (prestij cezaları ×2 SURPRISE AUDIT'i etkiliyor) |
| **8** | *(opsiyonel/tasarım)* `feat(phone): P-bazlı çalma şansı + ringDuration 15` | §B.6 | Bağımsız. **Tasarım kararı — ürün sahibi onayı gerekir, bug fix değil.** |

**Ayrılamaz çiftler (aynı commit zorunlu):**
`kira P-ölçeği ↔ rentGrowth ↔ prestij paketi ↔ moneyMultiplierPerPlayer` (#3) ·
`quest D1 ↔ D2 ↔ D3` (#6) · `prestij paketi ↔ quest prestij ×2` (#3 → #6 zinciri) ·
`prestij taban ×2 ↔ prestige_master/leveraged_rent perk senkronu` (#3 içinde).

**Kırmızı çizgi:** #3 merge edilip #2 edilmezse **3P/4P prestij ters ölçeklemesi geri gelir ve
kira ölçeği onları haksız cezalandırır** — bu kombinasyon mevcut durumdan DAHA KÖTÜ.

---

## §E PLAYTEST'TE ÖLÇÜLECEKLER (duyarlılık sırasıyla)

| # | Ölçülecek | Neden ilk sırada | Yanlışsa ne olur |
|---|---|---|---|
| **1** | **`kutu/dk/oyuncu`** (varsayım 2.0 Normal / 1.2 Yavaş / 3.0 Hızlı) | Kodda zamanlı üretim kapısı yok; tüm ekonominin tek çarpanı | 1.2 → 2.0 arası 1P kümülatifini **%117** değiştiriyor; tüm TL değerleri tek katsayıyla kayar, **oranlar ve sıralama korunur** |
| **2** | **Masa meşgul süresi `S`** (varsayım 6 sn): ürünü masaya koy → kutula → paketlenmişi al | Çok-oyunculu gelirin tamamı buna bağlı | S=8'de kira oranları OPT 1.90/1.88/1.81/**1.74**'e düşer (hâlâ güvenli); alternatif kira ölçeği `{500,1050,1500,1850}` S=8'de **2P/3P/4P STRICT'i gün 16'da iflas ettiriyordu** — bu yüzden daha yumuşak `{500,1000,1450,1800}` seçildi. S=8 ölçülürse `Paketleme İstasyonu` 150 → ~250 TL |
| **3** | **`serviceCycleSeconds` / `serviceLaborSeconds`** (18 / 15 sn): bir müşteriyi baştan sona servis etme süresi | Prestijin (dolayısıyla ödül tier'ının ve ölüm kapısının) tek girdisi; sabır önerisi (24-32 sn) buna bağlı | Servis 12 sn çıkarsa seri tavan 11.4 → 17 olur, 1P STRICT rahatlar ve `customerLostPrestigePenalty` −0.5'e geri çekilebilir |
| **4** | **2 istasyonun gerçek davranışı** (#2 commit sonrası): 3P/4P'de günde kaç müşteri kaçıyor? | Modelin en büyük yapısal iddiası | Kaçan >3/gün ise talep formülü (`_shelfMultiplier 2 → 1.5`) yedek kaldıraç — hazır ve ölçülü |
| **5** | **`agile_crew` +%15 moveSpeed → kaç % üretim?** | 180 TL'lik kartın ROI'si 2.9 ↔ 4.3 arasında oynuyor | +%15 üretim çıkarsa fiyat 180 → 250 |
| **6** | **Prestij tavanı doluluk günü** (beklenen OPT: 1P 16 · 2P 12 · 3P 11 · 4P 10) | Tavan dolduktan sonra ödül eğrisi düzleşiyor | 4P gün 10'da doluyor ve geç oyun "düz" hissediliyorsa **`maxPrestige` 100 → 110** (tavan günü −/14/12/11, kümülatif +%1-5) |
| **7** | **Animasyon tamponu** (varsayım 6 sn, tır giriş/çıkış klibi) | STRICT bantta ±%6-16 | Klip 2 sn ise STRICT gelirleri %10 yukarı kayar, kira ölçeği bir kademe sertleşebilir |
| **8** | **Reroll harcaması** (1P'de günde 2 reroll = bütçenin %75'i) | Yeni bütçe daraldı (FAZ 3'te %56'ydı) | Oyuncular gerçekten 2/gün reroll ediyorsa upgrade kapsaması %141 → ~%60'a düşer; reroll eğrisi yumuşatılmalı |

**Ölçülemeyenler (bilinçli olarak modelde YOK):** stamina (FATIGUE PROBLEM / HEAVY BOXES gerçek etkisi),
telefon yanıtlamanın oyuncu-saniyesi maliyeti (`phone_line`'ı pozitiften negatife çevirebilir),
STRICT ↔ OPTIMISTIC hangisinin gerçek olduğu (iki bant da raporlandı, tek sayı verilmedi).

---

## §F SİM KULLANIMI (v3.1)

```
node tools/economy-sim/sim.js                      # canli degerlerle 13 bolumlu tam cikti
```
Programatik: `require('./tools/economy-sim/sim.js')` → `runSim(P, opts)` ·
`opts = { scenario, mode, numHangars, questsEnabled, questTier, phoneEnabled, upgradeSpendRatio,`
`packingTables, serviceStations, cargoValues }`.
`SRC` = koddan okunan gerçek değerler (her satırda `dosya:satır`) · `ASSUMED` = insan-verimi varsayımları.
Bu raporun tüm sayıları `SRC`/`ASSUMED` bellekte geçici olarak değiştirilerek üretildi;
`sim.js`'in **canlı** default'ları değiştirilmedi (CLI çıktısı hâlâ *mevcut* ekonomiyi gösterir).
