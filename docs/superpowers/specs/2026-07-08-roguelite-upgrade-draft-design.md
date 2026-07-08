# Tasarım: Roguelite Upgrade Draft + Perk Sistemi

> **Durum:** ✅ TASARIM TAMAM (2026-07-08). Bölüm 1 (draft mekaniği) + Bölüm 2 (perk roster & tier) + Bölüm 3 (birleşik ekonomi yapısı) onaylı. Sıradaki: economist tüm değerleri sıfırdan fiyatlar → writing-plans → gameplay → qa → kontrol.
> **Sahip:** Müdür (brainstorming) → economist (değerler) → gameplay (uygulama) → kontrol
> **Kaynak dosyalar:** `Assets/NewCss/UpgradeScripts/UpgradePanel.cs`, `GameEconomySettings.cs`, `ItemType.cs`

## Amaç
Mağaza upgrade sistemini "tüm upgrade'leri listele + seviye yükselt" düzeninden **roguelite draft**'a çevirmek: her gün havuzdan **3 rastgele kart**. Ezber/düz progression'ı kırmak, çeşitlilik ve kaos katmak. Aynı zamanda sıkıcı düz stat çarpanlarını (stamina, kutu-başı-para vb.) kaldırıp yerine **daha dengeli, tier'lı bir perk havuzu** kurmak. Havuz **kolay genişleyebilir** olacak (v1 = 20 kart; sonra büyütülecek).

---

## Bölüm 1 — Draft Mekaniği (onaylı)

1. **Gün sonu, oyuncu masa/tezgah trigger alanına girince** panel açılır (mevcut açılış davranışı korunur). Paneli **açan oyuncuya** gösterilir.
2. Panel "tüm upgrade listesi" yerine **3 kart** gösterir.
3. **3 kartlık teklif server'da üretilir** (`NetworkList<int>` ~ `_dailyOffer`), günlük yenilenir. Herkes aynı 3 kartı görür, satın almalar senkron kalır. Garanti: 3'ü **farklı** upgrade/perk + **max olanlar hariç** + **o an kilidi açık tier'dan** (bkz. Bölüm 2.3).
4. **Satın alma: parası yeten hepsini alır.** Her kart o tur 1 kez. Satın alma → mevcut `_pendingUpgrades` mekaniği → **ertesi gün aktif**.
5. **Reroll:** Panelde "Yenile" butonu, para karşılığı 3 kartı yeniden çeker. Her reroll'da fiyat artar (economist belirler). v1'e dahil.
6. **RNG:** Tier içinde saf rastgele. Garantiler: teklifte tekrar yok, max'a ulaşan düşer, kilitli tier gelmez.

### Korunan sistemler (dokunulmuyor)
Satın alma RPC'leri, pending→aktifleşme, level objeleri, garaj kapıları, network senkron, para kontrolü, lokalizasyon. **Yeni parçalar:** "havuzdan 3 seç (tier-filtreli) + senkronla + reroll" katmanı + veri-güdümlü perk tanımları.

---

## Bölüm 2 — Perk Roster & Tier Sistemi (onaylı)

### 2.1 Roster karakteri (onaylı kararlar)
- **Dengeli karışım:** ~yarı güvenli stat, ~yarı risk/relic.
- **Yapı:** çoğu tek-seferlik relic + birkaç seviyeli perk.
- **Risk kartları gerçek trade-off taşır** (kalıcı bedel), sadece varyans değil.
- **v1 = 16 perk** (genişlemeye açık; sonra 25-30 hedefleniyor).

### 2.2 v1 Perk Roster (16 perk)

Not: Tüm sayısal değerler (±%X, seviye adedi, fiyat) **economist**'e ait — aşağıdaki tier ataması ve seviyeli/tek ayrımı taslaktır, economist kesinleştirir.

**A) Güvenli stat / QoL (9)**
| # | Perk | Kaldıraç | Yapı | Taslak tier |
|---|------|----------|------|-------------|
| 1 | Hızlı Hangar | `hangarStayDuration` ↑ (tır daha uzun kalır) | tek | T2 |
| 2 | Ucuz Kira | `rentGrowthMultiplier` ↓ | seviyeli (2-3) | **T3 (OP — geç kilit)** |
| 3 | Telefon Hattı | `maxCallsPerHour` +1 / `callReward` ↑ | tek | T1 |
| 4 | Prestij Ustası | `customerServedPrestigeBonus` ↑ | seviyeli (2-3) | T2 |
| 5 | Mesai Saati | gün biraz uzar (`timeSkipAmount` benzeri) | tek | T1 |
| 6 | Enerjik Ekip *(eski Stamina)* | `staminaRegenRate` ↑ | tek | T1 |
| 7 | Çevik Ekip *(yeni)* | oyuncu hareket hızı ↑ | tek | T1 |
| 8 | Sabırlı Müşteriler *(eski Customer)* | müşteri bekleme (patience) ↑ | tek | T1 |
| 9 | Uzun Kuyruk *(eski Queue)* | `maxQueueSize` ↑ | tek | T1 |

**B) Risk / gerçek trade-off (5)**
| # | Perk | Kazanç | Bedel | Yapı | Taslak tier |
|---|------|--------|-------|------|-------------|
| 10 | Kumarbaz Kasası | kutu ödülü +%X | kutu cezası +%X | tek | T2 |
| 11 | Kaldıraçlı Kira | kira −%X | prestij cezaları ×2 | tek | **T3** |
| 12 | Yüksek Volatilite | ortalama ödül +%X | her kutu ödülü ±%Y rastgele | tek | T2 |
| 13 | Kelle Koltukta | gelir +%X (büyük) | kira grace (ödeme toleransı) iptal | tek | **T3 (uç)** |
| 14 | Acil Fren (sigorta) | iflası bir kez önler | o gün geliri 0 + prestij −X | tek relic | T2 |

**C) Sinerji / ekonomi kaldıracı (2)**
| # | Perk | Etki | Yapı | Taslak tier |
|---|------|------|------|-------------|
| 15 | Prestij Simsarı | prestij→para dönüşümü ↑ (`bonusPerTier`) | seviyeli (2) | **T3** |
| 16 | Toplu Alım | sonraki draft'ta bir kart −%50 indirimli | tek | T1 |

### 2.3 Güç Tier'ı + Kilit (onaylı)
- Perk'ler **3 güç tier'ına** ayrılır: **T1** (erken-güvenli), **T2** (orta), **T3** (geç-güçlü).
- **T2/T3 perk'ler bir eşik geçilince havuza girer** — eşik: gün ve/veya mağaza seviyesi ve/veya prestij (economist belirler). Örn. T2 ~gün 5+ / mağaza sv 2+, T3 ~gün 9+.
- Amaç: **Ucuz Kira gibi OP perk'lerin 1-3. günde çıkmasını engellemek.** RNG/kaos, o an **kilidi açık tier havuzu içinde** korunur (Slay the Spire "act" havuzu mantığı).
- Fiziksel omurga upgrade'ler (bkz. Bölüm 3) tier'sız — her zaman havuzda (max olmayan seviyeleri).

---

## Bölüm 3 — Birleşik Ekonomi Yapısı (onaylı)

Kullanıcı kararı (2026-07-08): mevcut upgrade'lerden **fiziksel/görsel olanları** koru, **soyut stat çarpanlarını** kaldırıp sıfırdan dengeli perk havuzuna dönüştür.

### 3.1 Seviyeli omurga (4 upgrade — draft havuzunda seviyeli kart olarak kalır)
| Upgrade | Tip | Max sv | Not |
|---|---|---|---|
| Raf (Storage) | fiziksel (dünyaya raf ekler) | 10 | kapasite formülü |
| Masa (Table) | fiziksel (paketleme masası) | 2 | |
| Hangar Kapısı (Truck) | fiziksel (garaj kapısı açar) | 2 | |
| Görev Tier (Quest) | içerik (kullanıcı korumak istedi) | 2 | ⚠️ görev sistemi PASİF (EV≈0) — economist ya sembolik fiyat verir ya "sistem aktifleşene kadar havuza girmesin" der |

### 3.2 Kaldırılan upgrade'ler (artık upgrade değil)
Stamina, Kutu-başı-para (`rewardPerBox`), Kuyruk, Müşteri Sabrı, Su (Water). Bunların işlevi — anlamlı olanları — **yeni perk havuzunda farklı isim/kimlikle** geri döner (Enerjik Ekip = stamina, Sabırlı Müşteriler = patience, Uzun Kuyruk = queue). Su tamamen kaldırılır (achievement-only, ekonomik değersiz). Düz `rewardPerBox +10/seviye` **geri gelmez** (sıkıcı olduğu için kaldırılıyordu; yerini Kumarbaz Kasası / Yüksek Volatilite gibi ilginç ödül-kaldıraçları alır).

### 3.3 Sonuç havuz
**4 seviyeli omurga + 16 perk = 20 kartlık v1 havuzu.** Draft her gün bu havuzdan (max olmayanlar + kilidi açık tier) 3 kart çeker.

---

## Bölüm 4 — Fiyatlandırma Modeli (onaylı)

- **Sabit fiyat** (anlık paranın yüzdesi DEĞİL). Her perk tier'ına göre sabit TL fiyatı. Omurga upgrade'lerle tutarlı, exploit yok, dengelenebilir.
- Kaba tier bandı (economist kesinleştirir): T1 ~düşük, T2 ~orta, T3 ~yüksek. Tier zaten "geç perk = pahalı" etkisini doğal verir; ayrıca ölçekleme gerekmez.
- **Reroll fiyatı** economist'e ait; her reroll'da artan eğri.

### Taşınan kontrol bulguları (Faz 2 denetiminden — birleşik fiyatlamada baştan çözülecek)
economist'in önceki Faz 2 raporu bu birleşik yapıya soğuruluyor. kontrol'ün 2 yapısal bulgusu yeni hesapta baştan doğru kurulmalı:
1. **Bütçe fizibilitesi:** "hepsini al" senaryosu, harcamanın kendi `wealthTax` yüküyle (kümülatif harcamanın %10'u / kira dönemi, `GameEconomySettings.cs:113`) test edilmeli. Önceki rapor bunu tutarsız hesaplamıştı (düşük gelirde aslında açık veriyordu). Sonuç ya "sıkı ama mümkün" olmalı ya da "hepsini almak kasıtlı olarak imkansız" diye açıkça yazılmalı.
2. **Fiyat modeli–kod uyumu:** Kod maliyeti `baseCost + level × costStep` (doğrusal) üretir. Doğrusal-olmayan seviyeli fiyat dizileri (örn. Raf 200/220/220/…) ya doğrusal yaklaşımla verilmeli ya da "seviye-bazlı fiyat dizisi için küçük kod değişikliği gerekli" notu düşülmeli.

---

## Bölüm 5 — Mimari & Genişleyebilirlik (onaylı)

- **Veri-güdümlü perk tanımları:** Perk'ler ScriptableObject / Inspector listesi olarak tanımlanır. **Yeni perk eklemek = veri girişi, kod değişikliği değil.** (Yol A — UpgradePanel Inspector-driven; `UpgradeAssets.GetCost()` = ölü kod / Yol B, kullanılmaz.)
- Her perk tanımı taşır: id, isim (lokalize), açıklama, tier (T1/T2/T3), kilit eşiği, tip (tek/seviyeli), max seviye, fiyat(lar), etki referansı (hangi ekonomik kaldıraca ne kadar dokunuyor), trade-off (varsa).
- Draft katmanı bu listeden filtreleyip (max olmayan + kilidi açık tier) 3 seçer. Yeni perk eklendiğinde draft otomatik dahil eder.
- v1 = 16 perk; mimari 25-30'a kadar ek koda ihtiyaç duymadan büyümeli.

---

## İş akışı & sıradaki adımlar
1. **economist:** 4 omurga + 16 perk + reroll için sıfırdan **sabit fiyat** + tier eşikleri + her risk perk'inin trade-off büyüklüğü (±%X) + seviyeli perk seviye adetleri. Bölüm 4'teki 2 kontrol bulgusunu baştan çöz. → **kontrol**.
2. **writing-plans:** spec → uygulama planı.
3. **gameplay:** veri-güdümlü perk tanımları + tier-filtreli draft + reroll (Inspector/sahne YAML + gerekli minimal kod). → **kontrol**.
4. **qa:** senkron, satın alma, tier kilidi, iflas/sigorta senaryoları. → **kontrol**.
5. Unity'de 1/2/4 kişi test.

Sayısal değer (perk gücü, reroll fiyatı, tier eşiği) gereken her şey **economist**'ten geçer. Her çıktı **kontrol**'den ONAY alır.
