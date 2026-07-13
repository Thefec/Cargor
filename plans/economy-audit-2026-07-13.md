# FAZ 2 — Ekonomi Denge Denetimi (2026-07-13)

> Yazan: economist. Kod DEĞİŞTİRİLMEDİ — bu rapor karar-hazır bir denetimdir.
> Sim scriptleri: `C:\Users\cicek\AppData\Local\Temp\claude\...\scratchpad\sim.js` ve `sim_wealthtax.js` (Node.js — bu makinede python yok, bkz. [[env_no_python]]).

## 0. Doğrulanan GERÇEK kod değerleri (2026-07-13)

Kaynak: `Assets/NewCss/GameEconomySettings.cs`, `Assets/Resources/EkonomiAyarlari.asset`, `DifficultyManager.cs`, `MoneySystem.cs`, `PrestigeManager.cs`, `QuotaManager.cs`, `DayCycleManager.cs`, `CustomerManager.cs`, `UpgradePanel.cs`, `UpgradeManager.cs`.

| Parametre | Değer (asset+kod, senkron) | GDD.md'de yazan |
|---|---|---|
| `baseRentByPlayerCount` | [500, 900, 1200, 1500] | aynı |
| `rentGrowthMultiplier` | **1.15** | 1.3 (GDD güncel değil) |
| `wealthTaxRate` | 0.1 (ama **etkisiz**, bkz §3) | 0.1 |
| `rewardPerBox` / `penaltyPerBox` | **50 / 40** | 50 / 60 (GDD güncel değil) |
| `prestigePerBonus` / `bonusPerTier` | 10 / 5 | aynı |
| `gracePaymentPercent` | 0.8, tek seferlik | aynı |
| `startingPrestige` (PrestigeManager) | **15.0** | 5.0 (GDD güncel değil) |
| `customerLostPrestigePenalty` | **-1.5** | -2.0 (GDD güncel değil) |
| `customerServedPrestigeBonus` / `wrongProductPrestigePenalty` / `boxDropPrestigePenalty` | 0.5 / -0.1 / -0.05 | aynı |
| `baseStartingMoney` (DifficultyManager) | **500**, `moneyMultiplierPerPlayer=1.0` → **1P=2P=3P=4P hepsi 500 TL ile başlar** | GDD "100.000 TL (test değeri)" — tamamen stale, düzeltilmeli |
| `_difficultyRatio` (QuotaManager) | 0.8 | aynı |
| `MAX_DAYS` | 16 | aynı |
| `rentIntervalDays` | 4 | aynı |
| `playerCountMultiplier` (müşteri sayısı, CustomerManager, DifficultyManager.ApplyCustomerSettings) | 1P=1.0, 2P=1.3, 3P=1.6, 4P=1.9 | GDD "2.0" yaklaşık yazmış, gerçek 1.9 |

Sonuç: Faz-1'de kararlaştırılan değerler (`plans/economy-balance.md`) **kodda ve asset'te doğru şekilde canlı**. GDD.md metni ise 4 noktada (rentGrowth, penaltyPerBox, startingPrestige, customerLostPrestigePenalty, startingMoney) eski/stale — ayrı bir küçük iş olarak GDD güncellemesi önerilir (ekonomi değişikliği değil, dokümantasyon senkronu).

---

## 1. 16-gün nakit akışı simülasyonu (1P/2P/3P/4P)

### Model ve varsayımlar (AÇIKÇA işaretli — playtest gerekli kısım)
Gerçek oyuncu "kutu/dakika" verimi kodda yok (fiziksel hareket + kuyruk + kutu taşıma zamanlamasına bağlı, gameplay/playtest verisi). Bu yüzden iki senaryo kullanıldı:
- **Normal**: 2.0 kutu/dk/oyuncu, %20 kutu hatası (kırık/düşürme), %3 müşteri kaybı (sabır dolması)
- **Yavaş/Kötü**: 1.2 kutu/dk/oyuncu, %30 hata, %8 müşteri kaybı

Müşteri sayısı formülü CustomerManager'daki gerçek formülle birebir: `(activeInteractables×2 + storeLevel×2 + variance) × playerCountMultiplier`, upgrade harcaması arttıkça `activeInteractables`/`storeLevel` büyüyor (dükkan büyümesi → kod bunu doğrudan expose etmiyor ama editor-sim'in kendi tuttuğu tutarlı varsayım; gerçek büyüme UpgradePanel'in raf/masa/hangar upgrade'lerine bağlı, benzer mertebede).

### Sonuç — NORMAL senaryo (tüm oyuncu sayıları SAĞLIKLI bitiriyor)

| P | Son kasa (gün 16) | Son prestij | İflas? | Kira ödeme günleri (4/8/12/16) sorunsuz mu |
|---|---|---|---|---|
| 1P | 237 TL | 62.5 | Hayır | Evet (grace hiç kullanılmadı) |
| 2P | 589 TL | 95.7 | Hayır | Evet |
| 3P | 1277 TL | 100 (cap, gün~13) | Hayır | Evet |
| 4P | 1661 TL | 100 (cap, gün~10) | Hayır | Evet |

Gözlem: **rent_death_spiral riski normal performansta yok** — 1.15 çarpanı + 40 TL ceza + 500 TL start dengeli. Bu, [[rent_death_spiral]] hafızasındaki önceki teşhisi ve Faz-1 düzeltmesini doğruluyor.

**Yeni bulgu — Prestij tavana çok erken çarpıyor**: 3P/4P'de prestij gün 9-13 arası 100'e (max) ulaşıp orada kalıyor. Bu, oyunun ikinci yarısında prestij ekseninde hiç gerilim/ilerleme kalmadığı anlamına gelir (kutu-başı ödül tier'ı da donuyor, tier10=+50TL sabit). Kritik değil ama pacing notu: prestij tavanı (100) veya `prestigePerCustomer`/`prestigePerBonus` kademesi 3-4P için gün 16'ya yayılacak şekilde büyütülebilir (örn. max 150 veya per-tier eşik 12-15'e çıkarılabilir). **Play-test gerekmez, saf sayı ayarı** — ama bu FAZ 2 kapsamı dışı, ayrı bir küçük iş olarak not edilir.

### Sonuç — YAVAŞ/KÖTÜ senaryo (1P ve 2P test edildi)

| P | Olay | Gün |
|---|---|---|
| 1P | Grace period gün 4 (kasa 97 TL'ye düştü), **İFLAS gün 8** (2. kira 653 TL, kasada 427 TL yetersiz, grace zaten kullanılmış, Acil Fren perki yok) | 8 |
| 2P | Grace period gün 4, **İFLAS gün 8** | 8 |

Bu **beklenen ve sağlıklı bir zorluk eğrisi** — sistem gerçekten yetersiz performansı (skill-based) cezalandırıyor, ama tek bir kötü gün onları bitirmiyor (grace + kademeli). Roguelite "Acil Fren" perki bu senaryoda satın alınmamış varsayıldı (kötü performans → az para → perk alacak bütçe yok) — bu tutarlı bir risk-yönetimi copluk noktası: perk sistemi zayıf takımların tam ihtiyaç duyduğu anda parasızlıktan o perk'i alamaması riskini taşıyor. Bilgi amaçlı not, aksiyon gerektirmiyor (roguelite perk fiyatlandırması ayrı denetlendi, bkz [[roguelite_perk_pricing]]).

---

## 2. C1 — Kota-ölümü (ölü kod) nihai öneri

### Doğrulama
`QuotaManager.CheckEndOfDayQuota()` ve `OnQuotaFailed` event'i **hâlâ hiçbir yerden çağrılmıyor/dinlenmiyor** (`DayCycleManager.ProcessDayEnd()` içinde çağrı yok — grep ile teyit edildi, tüm `Assets/NewCss` taranmış). Yani kota tutturulamasa da oyun bitmiyor; kota şu an yalnızca UI göstergesi ("Kargo: 3/5").

### KRİTİK yeni bulgu — kota formülü, gerçekçi oyuncu hızıyla test edilmeden AÇILAMAZ
Kota eşiği (`customers × 0.8`) ile müşteri sayısı formülünün büyüme hızı (dükkan büyüdükçe `activeInteractables`/`storeLevel` artıyor → müşteri sayısı hızla 50'ye (soft cap) tırmanıyor) fakat gün süresi çok yavaş büyüyor (gün≤3: 160s, sonra +10s/gün → gün16: 290s, yani sadece +81%). Simülasyonda:

```
1P, 2.0 kutu/dk varsayımıyla: 16/16 gün kota KAÇIRILIYOR (teorik maksimum teslimat kapasitesi kota altında kalıyor)
1P, kota kaçırmama için gereken min. verim: ~6 kutu/dk/oyuncu (10 saniyede 1 teslimat)
2P: ~4 kutu/dk/oyuncu gerekiyor
4P: ~3 kutu/dk/oyuncu gerekiyor
```

Bu tamamen **oyuncunun gerçek kutu/dakika verimine bağlı** — bu veri kodda yok, yalnızca playtest ile ölçülebilir. Eğer gerçek verim <4/dk ise (çok olası, kutu taşıma+kuyruk+hareket süresi var), **kota sistemi mevcut `_difficultyRatio=0.8` ile her gün kaçırılacak şekilde kalibre edilmiş demektir** — hard game-over ile açılırsa oyun günlerin çoğunda otomatik kaybettirir, skill'den bağımsız.

### NİHAİ ÖNERİ
**AÇMA (as-is). Play-test şart, kod değişimi tek başına yetersiz.** Sıralı adımlar:
1. **Önce playtest**: mevcut build'de orta-seviye dükkanla (gün ~8) 1 oyuncu gerçek kutu/dk verimini ölç.
2. Ölçülen verim `_difficultyRatio × (customers/gün_süresi_dk)` eşiğinin **altındaysa**, `_difficultyRatio`'yu 0.8'den düşür (öneri aralık: 0.4-0.6) VEYA müşteri sayısı formülündeki `storeLevel`/`activeInteractables` büyüme katsayısını gün süresi büyümesiyle daha orantılı hale getir (aynı hızda büyüsünler).
3. Kalibrasyon doğrulandıktan SONRA iki-kademeli tampon ile aktive et (kullanıcının önerdiği model):
   - **1. kaçırma (herhangi bir gün)**: UI uyarı + prestij cezası **-1.5** (customerLostPrestigePenalty ile aynı mertebe, tema tutarlı: "günü kaçırmak 1 müşteri kaybetmek gibi").
   - **Ardışık 3 kaçırma** (aralarında başarı yoksa): **GAME OVER**. Ardışık şart olmalı — kümülatif olursa (örn. 16 günde 3 kez) tek kötü haftada mahvolmuş takımı da öldürür; ardışık-3 daha affedici ve "trend" ölçer.
   - Kira sistemindeki grace period gibi **tek seferlik "kota affı"** eklenebilir (opsiyonel, kullanıcı kararına bırakılır): ardışık sayaç 3'e ulaştığında ilk seferinde sayaç sıfırlanır, ikinci seferinde gerçekten biter.
4. Dokunulacak dosya/satır: `Assets/NewCss/UIScripts/DayCycleManager.cs` (`ProcessDayEnd()` ~475-503 içine `QuotaManager.Instance.CheckEndOfDayQuota()` çağrısı ekle + `QuotaManager.OnQuotaFailed`'e abone ol → ardışık sayaç), `Assets/NewCss/QuotaManager.cs` (`_consecutiveMisses` int alanı, `CheckEndOfDayQuota()` içine sayaç mantığı, `_difficultyRatio` playtest sonrası revize).
5. **Play-test gerekir: EVET, zorunlu ön koşul** — sayısal kalibrasyon olmadan aktivasyon riskli.

---

## 3. C5 — wealthTax nihai öneri (YENİ KÖK NEDEN BULUNDU)

### Önceki teşhis (release-push.md, 2026-07-12): "wealthTaxRate hep 0 etkili, sıfır regresyon riski"
Bu teşhis **eksikti**. Gerçek kök neden daha derin:

`DayCycleManager.GetTotalUpgradeValue()` (satır 631-644) şu kaynaktan okuyor:
```csharp
foreach (ItemType t in Enum.GetValues(typeof(ItemType)))
    if (UpgradeManager.Instance.IsPurchased(t)) total += UpgradeAssets.GetCost(t);
```
Bu **"Yol B"** — `UpgradeManager`/`ItemType`/`UpgradeAssets` — **ölü/orphan sistem**. Gerçek satın alma akışı **"Yol A"**: `UpgradePanel.PurchaseUpgradeServerRpc()` → `MoneySystem.Instance.SpendMoney(serverCost)` + `_visualUpgradeLevels[...]++`. Bu akış **hiçbir yerde `UpgradeManager.Instance`'a veya `ItemType`'a dokunmuyor** (kod genelinde grep ile teyit edildi — `UpgradePanel.cs`'de böyle bir referans yok).

**Sonuç: `wealthTaxRate` değerini 0.1'den 0.03'e düşürmek HİÇBİR ŞEY DEĞİŞTİRMEZ.** `totalUpgradeValue` gerçek oyunda her zaman 0 döner (gerçek harcamalar hiç sayılmıyor), rate ne olursa olsun çarpım 0. Bu salt bir "oran ayarı" değil, **kırık kablolama (broken wiring)** sorunu.

### NİHAİ ÖNERİ — iki seçenek, kullanıcı/gameplay karar versin

**Seçenek A (önerilen, düşük risk): Temiz kaldır.**
`GameEconomySettings.wealthTaxRate` alanını ve `CalculateRent()`'teki wealthTax terimini kaldır, `GetTotalUpgradeValue()`'yu da DayCycleManager'dan sil (zaten hiçbir gözlemlenebilir davranışı yok — sıfır regresyon, çünkü şu an zaten hep 0 katkı veriyor). Ekstra: `UpgradeManager.cs`/`ItemType.cs`/`UpgradeAssets.cs` (Yol B, tamamen orphan) ayrı bir temizlik task'ı olarak işaretlenir (kod-hijyeni, ekonomi değil).

**Seçenek B (istenirse "zengine vergi" mekaniği canlansın): Doğru kabloyla bağla.**
1. `UpgradePanel`'e `public int TotalSpentTL` (NetworkVariable<int> veya sunucu-authoritative int) ekle; `PurchaseUpgradeServerRpc()` içinde `SpendMoney(serverCost)` çağrısının yanına `TotalSpentTL += serverCost` ekle.
2. `DayCycleManager.GetTotalUpgradeValue()`'yu `UpgradePanel.Instance.TotalSpentTL` okuyacak şekilde değiştir (UpgradeManager/ItemType yolunu tamamen at).
3. `wealthTaxRate`'i 0.1 yerine **0.03** yap.

Bu senaryonun etkisini (varsayımsal olarak, gerçek harcama sim'e proxy olarak "surplus×0.5 upgrade'e gider" modeliyle) test ettim — 16 gün, NORMAL performans senaryosu, güçlü/harcayan takımlarda:

| P | wealthTax=0 (son kasa) | wealthTax=0.03 | wealthTax=0.05 | wealthTax=0.1 (mevcut asset değeri, ama şu an etkisiz) |
|---|---|---|---|---|
| 1P | 568 TL | 460 TL (-19%) | 393 TL (-31%) | 237 TL (-58%) |
| 2P | 1326 TL | 1089 TL (-18%) | 940 TL (-29%) | 589 TL (-56%) |
| 4P | 3494 TL | 2911 TL (-17%) | 2538 TL (-27%) | 1661 TL (-52%) |

**Kritik güvenlik bulgusu**: "Yavaş/Kötü" (zayıf) senaryoda upgrade hiç satın alınmıyor (kasa hep <200 TL kalıyor upgrade eşiğinin altında) → `totalUpgradeValue=0` orada da → **wealthTax zayıf takımlara SIFIR ek risk bindiriyor, sadece zaten başarılı/harcayan takımların marjını kırpıyor.** Yani wealthTax mekanik olarak "başarı vergisi" gibi çalışıyor, death-spiral tetiklemiyor — [[rent_death_spiral]] riski (1.15 rentGrowth ile) YOK, çünkü tetikleyici zaten-zengin oyuncular.

**Sonuç öneri**: Seçenek A (kaldır) en düşük riskli ve en hızlı; Seçenek B (0.03 ile doğru kabloyla bağla) tasarım-niyeti daha iyi karşılıyorsa (zengin oyuncuya kira baskısı — "upgrade spam'i cezalandır" teması) uygulanabilir, ama **bu bir "değer değişikliği" değil bir kod-değişikliği (yeni alan + servertan-authoritative toplam) gerektiriyor** — gameplay departmanına B seçilirse ayrı bir küçük implementasyon task'ı olarak verilmeli. **Play-test gerekmez** (matematik zaten kesin), ama B seçilirse tek-oyunculuk QA regresyon testi (satın alma sonrası doğru toplamın biriktiğini doğrulamak) gerekir.

---

## 4. GDD'ye karşı sapma özeti

| Sistem | GDD | Kod (gerçek) | Aksiyon |
|---|---|---|---|
| Kira büyüme çarpanı | 1.3 | 1.15 | GDD güncelle (kasıtlı Faz-1 düzeltmesi) |
| Yanlış teslimat cezası | 60 TL | 40 TL | GDD güncelle (kasıtlı Faz-1 düzeltmesi) |
| Başlangıç prestiji | 5.0 | 15.0 | GDD güncelle (kasıtlı Faz-1 düzeltmesi) |
| Müşteri kaçırma cezası | -2.0 | -1.5 | GDD güncelle (kasıtlı Faz-1 düzeltmesi) |
| Başlangıç parası | "100.000 TL (test)" | 500 TL (tüm P sayısı) | GDD güncelle — stale placeholder, hiç düzeltilmemiş |
| Oyuncu sayısı müşteri çarpanı | ~2.0 (4P) | 1.9 (4P) | Küçük yuvarlama farkı, kritik değil |
| wealthTax | "aktif vergi" olarak anlatılmış | fiilen hep 0 (kırık kablolama) | Bkz §3 — karar bekliyor |
| Kota-ölümü | "gün sonu tutturulamazsa GAME OVER" | dead code, hiç tetiklenmiyor | Bkz §2 — karar bekliyor |

Not: GDD'deki 4 "kasıtlı Faz-1 düzeltmesi" satırı ekonomik risk taşımıyor — sadece dokümantasyon eski. Ayrı küçük bir iş olarak (KÜÇÜK iş eşiği, subagent gerekmez) GDD.md §4-§7 tek seferde güncellenebilir.

---

## 5. ÖNCELİKLİ implementasyon tablosu

| # | Değişiklik | Dosya:satır | Risk | Play-test gerekir mi |
|---|---|---|---|---|
| 1 | Kota gerçek oyuncu kutu/dk verimini ölç (playtest) | — (playtest) | — | **EVET — ön koşul, C1'in her adımından önce** |
| 2 | `_difficultyRatio` kalibrasyonu (0.8 → ölçülen verime göre, tahmini 0.4-0.6) | `QuotaManager.cs:50` | Orta (yanlış kalibre edilirse ya çok kolay ya imkansız) | Evet (adım 1'e bağlı) |
| 3 | Kota iki-kademeli tampon (ardışık-3 game over, ilk kaçırmada -1.5 prestij) | `DayCycleManager.cs` ProcessDayEnd ~475-503; `QuotaManager.cs` CheckEndOfDayQuota ~277-296 (yeni `_consecutiveMisses`) | Orta (yeni oyun-bitirme yolu, dikkatli test) | Evet (kalibrasyon sonrası fonksiyonel test) |
| 4 | wealthTax: Seçenek A (kaldır) — `wealthTaxRate` alanı + `CalculateRent()` terimi + `GetTotalUpgradeValue()` sil | `GameEconomySettings.cs:27,122`; `DayCycleManager.cs:585,590,598,631-644` | Düşük (zaten gözlemlenebilir etkisi yok) | Hayır |
| 4' | wealthTax: Seçenek B (doğru bağla + 0.03) — `UpgradePanel.TotalSpentTL` ekle, `GetTotalUpgradeValue()` onu okusun, rate 0.1→0.03 | `UpgradePanel.cs` PurchaseUpgradeServerRpc ~1057-1095; `DayCycleManager.cs:631-644`; `GameEconomySettings.cs:27` | Orta (yeni network-senkron alan) | QA regresyon evet, ekonomi play-test hayır (matematik kesin) |
| 5 | GDD.md §4-§7 stale değerleri güncelle (rentGrowth, penaltyPerBox, startingPrestige, customerLostPenalty, startingMoney) | `GDD.md` satır ~239,246,258,301-306,334,210 | Yok (dokümantasyon) | Hayır |
| 6 | Prestij tavanı (100) 3-4P için çok erken doluyor (gün 9-13) — pacing notu, kapsam dışı ama kayıt altına alındı | `PrestigeManager.cs:29` (`maxCustomerCapacity`) veya `prestigePerBonus`/tavan büyütme | Düşük | Hayır (saf sayı ayarı), ama tasarım kararı gerekir |

---

## Hafıza güncellemesi
`.claude/agent-memory/economist/` içine 2 yeni girdi eklendi: quota-kalibrasyon bulgusu ve wealthTax kırık-kablolama kök nedeni (aşağıda MEMORY.md'ye işlendi).
