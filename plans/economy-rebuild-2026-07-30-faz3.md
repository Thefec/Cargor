# Cargor Ekonomi Yeniden Kurulumu — FAZ 3 / 3: UPGRADE + PERK + QUEST

**Tarih:** 2026-07-30
**Dal:** `feature/economy-balance-round`
**Kapsam:** 25 upgrade/perk fiyatlandırması · `upgradeCostMultiplierPerPlayer` · 30 quest asset'inin EV yeniden çapalanması.
**Girdi:** `plans/economy-rebuild-2026-07-30.md` §3 gelir tabanı (FAZ 1). Eski upgrade/quest raporları veri olarak KULLANILMADI.
**Yöntem:** Her upgrade `The Main Office.unity` + `UpgradePanel.cs` + `PerkEffect.cs`'ten yeniden okundu. Değerler
`tools/economy-sim/sim.js` (salt-oku) üzerinde her perk için ayrı izole koşuyla ölçüldü; masa çekişmesi ve quest EV'si
ayrı Python modelleriyle hesaplandı. **Hiçbir `Assets/` dosyası ve sim.js DEĞİŞTİRİLMEDİ** — bu tur salt hesap.

> **Ölçek uyarısı (FAZ 1'den devralındı):** bir oyun günü yalnız 200–330 GERÇEK saniye; `kutu/dk` en duyarlı girdi.
> Bu yüzden aşağıdaki tüm sonuçlar **oran** cinsinden ifade edildi (ROI = değer/fiyat, gelir-günü cinsinden payback,
> içeriğin bütçeye kapsama oranı). Playtest ile gerçek kutu/dk ölçülünce tüm mutlak TL'ler **tek katsayıyla** kayar,
> oranlar ve sıralama korunur.

---

## §0 YÖNETİCİ ÖZETİ — 6 yapısal bulgu

| # | Bulgu | Kanıt |
|---|---|---|
| **B1** | **Yalnız 2 şey değer üretiyor:** insan üretim hızı ve TL/kutu. Kapasite artıran her şey (hangar sayısı, hangar süresi, kuyruk uzunluğu, raf sayısı, müşteri sabrı) **sıfır** değerde çünkü bağlayıcı kısıtlar üretim hızı ve seri servis döngüsü. | §2.1, §2.4 |
| **B2** | **3 kart AKTİF ZARARLI** (negatif TL): `Uzun Kuyruk` (−449…−5392), `Kaldıraçlı Kira` (−255…−2187), `Mesai Saati` (−612…−1937, kod bug'ı). `Geniş Ambar` 1P'de −163. | §2.3, §3 |
| **B3** | `upgradeCostMultiplierPerPlayer = 1.15` yanlış: içerik kapsaması 1P'de **%209**, 4P'de **%98** → çok oyuncu tüm içeriği alıyor, solo yarısını. Doğru ölçek **[1.00 / 1.80 / 2.45 / 3.15]**. | §4 |
| **B4** | `Paketleme İstasyonu` **oyunun tek gerçek üretim upgrade'i** ama sahnede yalnız **2 levelObject** var, `maxLevel: 3` → seviye 2-3 için 650 TL karşılığı **hiçbir şey** alınıyor. | §2.2 |
| **B5** | Quest sistemi kaçışın **%1.5–3.2**'si. `Görev Kademesi` **her oyuncu sayısında negatif-değerli** (1P −89 TL, 2P L2 −22 TL) çünkü üst tier havuzu **seyreltiyor** ve hedefler 1P kapasitesinin üstünde. | §5.1 |
| **B6** | `PlaceBoxOnShelf` quest'leri (13/30 asset) **exploit'e açık**: dolu bir kutu rafa koyulup geri alınıp tekrar koyulunca event her seferinde tetikleniyor → tek kutu ile sınırsız ilerleme. | §5.4 |

---

## §1 CANLI UPGRADE ENVANTERİ (25 kart)

Maliyet formülü **LİNEER**: `finalCost = (baseCost + level × costStep) × eventMult × difficultyMult`
(`UpgradePanel.cs:1186-1197`). `bulk_buy` işaretlediyse ×0.5. Ertelenmiş aktivasyon: satın alınan gün DEĞİL, **ertesi gün** aktif (`UpgradePanel.cs:811-853`).

### 1.1 Omurgalar (`kind: 0`) — 9 kart, 4'ü aktif

| displayName | maxLev | baseCost/step | Toplam | Draft | Gerçek etkisi | sahne satırı |
|---|---|---|---|---|---|---|
| Geniş Ambar | **9** | 50 / 10 | 810 | AÇIK | levelObjects → 10 `StorageRack` (`ShelfState`), her seviye +1 aktif raf | 21176–21186 |
| Paketleme İstasyonu | **3** | 100 / 150 | 750 | AÇIK | levelObjects → **yalnız 2** `Table` (paketleme masası). Seviye 2-3 ETKİSİZ | 21206–21216 |
| Geniş Kuyruk | 3 | 250 / 100 | 1050 | **KAPALI** | `maxQueueSize = 3+level` | 21227–21237 |
| Sağlam Kasa | 3 | 300 / 100 | 1200 | **KAPALI** | `ApplyMoneyUpgrade` **NO-OP** (gövde bilinçli boş) | 21250–21260 |
| Dinç Ekip | 3 | 100 / 75 | 525 | **KAPALI** | staminaRegen (3 levelObject de AYNI fileID) | 21272–21282 |
| Ek Hangar | **2** | 200 / 100 | 500 | AÇIK | garaj kapıları + `TruckSpawner.SetTruckUpgradeLevel` → 1/2/3 hangar | 21294–21304 |
| Su Sebili | 1 | 500 / 200 | 500 | **KAPALI** | yok | 21319–21328 |
| Güler Yüz | 3 | 300 / 200 | 1500 | **KAPALI** | yok | 21340–21349 |
| Görev Kademesi | **2** | 80 / 20 | 180 | AÇIK (`requiresQuestSystem`) | `QuestManager.SetQuestTier(level)` | 21362–21372 |

**Aktif omurga içeriği toplam 2 240 TL (1P) — bunun 1 460 TL'si (%65) hiçbir ölçülebilir değer üretmiyor.**

### 1.2 Perkler (`kind: 1`) — 16 kart

| displayName | effectId | tier | maxLev | baseCost/step | Kod etkisi (`PerkEffect.cs`) | sahne satırı |
|---|---|---|---|---|---|---|
| Ucuz Kira | cheap_rent | T2 | 3 | 130 / 30 | `rentGrowthMultiplier = 1.15 − 0.03×lvl` (:75-79) | 21381–21392 |
| Prestij Simsarı | prestige_broker | T2 | 2 | **510 / −5** | `Truck.bonusPerTier = 5 + 0.5×lvl` (:82-86) | 21401–21411 |
| Prestij Ustası | prestige_master | T1 | 2 | 280 / 100 | `customerServedPrestigeBonus = 0.2 + 0.06×lvl` (:89-93) | 21420–21431 |
| Hızlı Hangar | fast_hangar | T1 | 1 | 280 | `hangarStayDuration = taban(P) × 1.30` (:98-103) | 21440–21450 |
| Enerjik Ekip | energetic_crew | T0 | 1 | 160 | staminaRegen 1 → 2.5 (:106-110) | 21460–21469 |
| Çevik Ekip | agile_crew | T0 | 1 | 180 | `moveSpeed 5 → 5.75` (+%15) (:113-117) | 21479–21488 |
| Sabırlı Müşteriler | patient_customers | T0 | 1 | 220 | `patienceMultiplier = 1.25` (:121-125) | 21498–21507 |
| Uzun Kuyruk | long_queue | T0 | 1 | 240 | `maxQueueSize = 3+2 = 5` (:128-132) | 21517–21526 |
| Kumarbaz Kasası | gambler_case | T1 | 1 | 400 | ödül ×1.30 (65), ceza ×1.55 (62) (:137-142) | 21536–21546 |
| Telefon Hattı | phone_line | T0 | 1 | 160 | `phoneRingPerkBonus = 0.15` (:148-152) | 21556–21566 |
| Mesai Saati | overtime | T0 | 1 | 300 | **`realDurationInSeconds = 180`** (:196-200) | 21576–21586 |
| Kaldıraçlı Kira | leveraged_rent | T2 | 1 | 350 | kira ×0.8 + `customerLostPrestigePenalty = −1.2` (:161-166) | 21596–21606 |
| Yüksek Volatilite | high_volatility | T1 | 1 | 450 | `rewardVolatility 0.35`, ort ×1.15 (:170-175) | 21616–21626 |
| Acil Fren | emergency_brake | T1 | 1 | 250 | `DayCycle.insuranceAvailable = true` (:187-191) | 21636–21646 |
| Kelle Koltukta | all_in | T2 | 1 | 800 | ödül ×1.25 (63) + `gracePaymentPercent = 0` (:178-183) | 21656–21666 |
| Toplu Alım | bulk_buy | T0 | 1 | 150 | sonraki taslakta 1 karta −%50 (:204-208) | 21676–21685 |

Tier kilidi: T1 hep açık, T2 gün ≥5, T3 gün ≥9 (`DraftPool.cs:11-20`). **Sahnede T3 (tier: 2 = `PerkTier.T3`) olan kartlar gün 9'dan önce çıkmaz.** Reroll: 50/90/160/290/525, günlük sıfırlanır (`RerollCurve.cs:8`, `UpgradePanel.cs:900`).
Günlük satın alma limiti **YOK** — parası varsa oyuncu 3 kartın 3'ünü de alabilir.

---

## §2 GERÇEK DEĞER ÖLÇÜMÜ

### 2.0 Metrik ve hedef ROI çerçevesi

**Metrik:** `netWorth` = upgrade gün 1'de alınıp gün 2'de aktifleşirse 16 gün sonundaki kasa farkı (kira dahil).
`d7` sütunu = gün 6'da alınırsa kalan günlerin toplamı (tipik olarak netWorth'ün %70-78'i).

**Hedef ROI (nominal, netWorth/fiyat):**

| Sınıf | Nominal ROI | Gerekçe |
|---|---|---|
| Motor / üretim (en erken alınır, gerçekleşen ≈ 0.85×nominal) | **3.5** | Erken alınır, uzun süre çalışır; oyunun "büyüme hissi" buradan gelir |
| Ödül-çarpanı relic (T1/T2, orta koşu, gerçekleşen ≈ 0.72×) | **3.0** | Risksiz ve pasif → motordan pahalı olmalı |
| Prestij perkleri (bileşik büyür) | **3.0** | Bileşik olduğu için "her zaman ilk al" no-brainer'ına dönüşmesin |
| Yardımcı / sigorta / konfor | **2.0** | EV değil varyans satıyor |

**Neden 3.0-3.5 ve neden bu bir enflasyon riski değil:** netWorth gün-1 (maksimum) tabanında ölçülüyor.
Gerçek satın alma günü 4-8 arası olduğu için **gerçekleşen ROI ≈ 0.70-0.85 × nominal ≈ 2.1-3.0**.
Toplam içerik maliyeti harcanabilir bütçenin **%103-106**'sına oturduğunda (bkz. §4.2) tüm bütçeyi upgrade'e yatıran
oyuncu geliri ~2× büyütür — tatmin edici ama kaçak değil.

### 2.1 Ek Hangar — 2. hangar sadece STRICT'te, 3. hangar HİÇ değersiz

| | 1P | 2P | 3P | 4P |
|---|---|---|---|---|
| L1 (1→2 hangar), OPTIMISTIC | **0** | **0** | **0** | **0** |
| L1 (1→2 hangar), STRICT | 57 | 1 390 | 3 119 | 5 135 |
| L2 (2→3 hangar), her iki bant | **0** | **0** | **0** | **0** |

OPTIMISTIC bantta üretim tavanı bağlıyor (ön-stok var → tırı doldurmak sorun değil), bu yüzden hangar sayısı hiç
işe yaramıyor. STRICT bantta 2. hangar paralel doldurma sağladığı için gerçek katkı veriyor. **3. hangar her iki
bantta da sıfır** — üretim tavanına çarpıyor (FAZ 1 §2.3 ile birebir aynı sonuç, bağımsız doğrulama).

### 2.2 Paketleme İstasyonu — tek gerçek üretim kaldıracı, ama yarısı yok

`Table.cs` **tek item** taşıyor (`TableState{isEmpty, itemNetworkId, isItemBoxed}`) ve **paketleme yalnız masada**
yapılıyor (`Table.cs:763-781`). Sahnede `Table` bileşeni taşıyan **tam 2 GameObject** var ve ikisi de Paketleme
İstasyonu'nun `levelObjects`'i (fileID 729050603, 457085722). Seviye 0'da yalnız **1 masa** aktif
(`UpgradePanel.InitializeLevelObjects` → `UpdateLevelObjects(..., 0)`).

Yani takımın TÜM kutu üretimi tek bir masadan **seri** geçiyor. Bunu sonlu-kaynak kuyruk modeliyle (machine-repairman,
`Z + S = 30 sn` çevrim, `c` masa) ölçtüm:

| masa meşgul süresi S | 1P | 2P | 3P | 4P |
|---|---|---|---|---|
| **4 sn** → 2. masanın üretim kazancı | %0.0 | %1.8 | %3.8 | %6.3 |
| **6 sn** (taban varsayım) | %0.0 | **%4.0** | **%8.8** | **%14.7** |
| **8 sn** | %0.0 | %7.1 | %15.7 | %26.2 |

TL değeri (S = 6 sn, OPTIMISTIC / STRICT netWorth):

| | 1P | 2P | 3P | 4P |
|---|---|---|---|---|
| Paketleme L1, OPT | **0** | 391 | 1 274 | 2 729 |
| Paketleme L1, STRICT | 0 | 219 | 623 | 1 332 |
| Paketleme L1, OPT (S=8 sn) | 0 | 693 | 2 273 | 4 808 |

> **S = masa meşgul süresi, en önemli ölçülmesi gereken playtest sayısı.** Ürünü masaya koy → kutula →
> paketlenmişi al arasındaki süre. 4 sn ile 8 sn arasında bu upgrade'in değeri **4×** değişiyor.

**Seviye 2 ve 3 hiçbir şey yapmıyor** (3. ve 4. `Table` sahnede yok) → 250 + 400 = **650 TL boşa**.

### 2.3 Geniş Ambar — sıfır ya da negatif değerde (9 seviye boyunca)

`CustomerManager.CountActiveInteractables()` (`cs:423-436`) `FindObjectsOfType<ShelfState>() + <DisplayTable>()`
sayıyor — **inaktif objeler sayılmaz**. Sahnede 13 `ShelfState` var: 10'u Geniş Ambar'ın levelObjects'i, 3'ü daima
aktif. + 1 `DisplayTable`. Yani **seviye 0'da aktif interactable = 4 raf + 1 masa = 5** (FAZ 1 §2.5 bunu 3 varsaymış,
"~tahmin" etiketiyle — düzeltilmeli).

Talep = `round((I×2 + storeLevel×2 + 0.5) × pMult)`, `storeLevel` hiç değişmiyor (sabit 1, `CustomerManager.cs:74`).

| Ambar seviyesi | 1P talep | 2P | 3P | 4P | seri servis tavanı |
|---|---|---|---|---|---|
| 0 (canlı) | 12 | 16 | 20 | 24 | **11.4 / gün** |
| 1 | 14 | 19 | 23 | 28 | 11.4 |
| 9 | 30 | 40 | 49 | 50 (clamp) | 11.4 |

**Talep HER oyuncu sayısında seri servis tavanının çok üstünde.** Raf eklemek servis edilen müşteriyi artırmıyor;
yalnızca 1P'de "kaçan müşteri"yi 0.64 → 2.0'a çıkarıyor (−0.82 prestij/gün).

| | 1P | 2P | 3P | 4P |
|---|---|---|---|---|
| Geniş Ambar L1 netWorth (OPT) | **−163** | 0 | 0 | 0 |
| Geniş Ambar L2..L9 ek netWorth | −28 | 0 | 0 | 0 |

Ayrıca **draft havuzu kalitesi sorunu:** 36 satın-alma olayının 9'u (%25) bu ölü karta ait. Her gün 3 karttan
biri bu olabilir.

### 2.4 Perk-perk ölçüm tablosu (netWorth, OPTIMISTIC / STRICT)

| Perk (etkin değişiklik) | 1P | 2P | 3P | 4P | STR 2P | STR 4P |
|---|---|---|---|---|---|---|
| `prestige_master` L1 (0.2→0.26) | 432 | 961 | 1 389 | 1 736 | 522 | 1 063 |
| `prestige_master` L2 kümülatif | 904 | 1 899 | 2 788 | 3 626 | 1 117 | 2 048 |
| `prestige_broker` L1 (5→5.5) | 180 | 331 | 487 | 650 | 150 | 291 |
| `prestige_broker` L2 kümülatif | 359 | 661 | 974 | 1 299 | 299 | 583 |
| `agile_crew` (+%10 üretim) | 518 | 976 | 1 448 | 1 930 | 547 | 907 |
| `agile_crew` (+%15 üretim) | 774 | 1 462 | 2 171 | 2 786 | 819 | 1 359 |
| `gambler_case` | 868 | 1 736 | 2 604 | 3 472 | 1 127 | 1 867 |
| `all_in` | 941 | 1 881 | 2 821 | 3 762 | 1 221 | 2 022 |
| `high_volatility` | 812 | 1 581 | 2 358 | 3 145 | 928 | 1 604 |
| `phone_line` | 655 | 873 | 1 021 | 1 220 | 343 | 541 |
| `cheap_rent` L1 / L2 / L3 kümülatif | 107 / 209 / 308 | 193 / 378 / 557 | 257 / 504 / 743 | 321 / 630 / 928 | aynı | aynı |
| `fast_hangar` | **0** | **0** | **0** | **0** | 198 | 725 |
| `patient_customers` | **0** | **0** | **0** | **0** | 0 | 0 |
| `energetic_crew` | **0** | **0** | **0** | **0** | 0 | 0 |
| `bulk_buy` (≈ alınan kartın %50'si) | ~150 | ~270 | ~368 | ~473 | ~150 | ~250 |
| `emergency_brake` | 0 (iflas yok) | 0 | 0 | 0 | ~900 | ~1 300 |
| **`long_queue`** | **−449** | **−2 186** | **−3 786** | **−5 392** | **−865** | **−2 803** |
| **`leveraged_rent`** | **−255** | **−891** | **−1 479** | **−2 187** | **−525** | **−402** |
| **`overtime` (CANLI HÂLİ)** | **−612** | **−977** | **−1 453** | **−1 937** | **−650** | **−1 002** |
| `overtime` (200→225 düzeltilirse) | 815 | 1 420 | 2 020 | 2 382 | 764 | 1 331 |

---

## §3 ÜÇ AKTİF ZARARLI KART (öncelik: P0)

### 3.1 `overtime` (Mesai Saati) — KOD BUG'I, 300 TL karşılığı −%10 gün

`PerkEffect.cs:196-200`
```csharp
private static void ApplyOvertime(int level, PerkContext ctx)
{
    if (ctx.DayCycle == null || level <= 0) return;
    ctx.DayCycle.realDurationInSeconds = 160f + 20f;   // = 180
}
```
Sahnedeki canlı `realDurationInSeconds` **200** (`The Main Office.unity:15995`). `CurrentDayDuration` =
`realDurationInSeconds + (gün−3)×10` (`DayCycleManager.cs:183-197`), yani perk **kalan her günü 20 sn KISALTIYOR**
(−%10 gün → −%10 gelir). Eski `.cs` default'u (160) üzerine yazılmış bir sabit; 2026-07-30 itibarıyla ölü sayı.

- **Mevcut:** −612 … −1 937 TL, fiyat 300 TL → toplam zarar 900–2 240 TL.
- **Düzeltme (gameplay):** tabanı önbelleğe alıp `= _baseRealDuration * 1.125f` (idempotent olmalı — `*=` KULLANMA,
  `HandleUpgradeLevelsChanged` tüm client'larda tetikleniyor).
- **Fiyat:** düzeltmeden sonra netWorth 815–2 382 → 1P-eşdeğer ~800 → ROI 3.0 hedefinde **fiyat 300 TL DOĞRU, dokunma.**
- Dosya: `Assets/NewCss/UpgradeScripts/PerkEffect.cs:196-200`

### 3.2 `long_queue` (Uzun Kuyruk) — mekanik yanlış hedefe basıyor

Kuyruk uzunluğu bir darboğaz DEĞİL; darboğaz seri servis (`CustomerAI.cs:582`, aynı anda tek müşteri).
Kuyruk büyüdükçe daha fazla müşteri sahneye giriyor ama servis kapasitesi sabit → fazlası **sabrı dolup kaçıyor**
(`customerLostPrestigePenalty = −0.6` her biri). Kuyruk dolu olduğunda spawn ATLANIYOR ve o müşteri **cezasız**
(`CustomerManager.cs:516`) — yani kısa kuyruk bir koruma kalkanı.

Ölçüm: 2 → 5 kuyruk = 1P −449, 4P **−5 392** TL. STRICT bantta 1P ve 2P'de **iflasa** sebep oluyor.
Ek olarak `PerkEffect.cs:131` mutlak yazıyor (`DEFAULT_QUEUE_SIZE + 2 = 5`), canlı sahne değeri 2 olduğu için
fiilen **+3**, yorumda yazan +2 değil.

- **Öneri A (tercih edilen):** etkiyi değiştir — kuyruk uzunluğu değil **servis hızı**. `CustomerAI.interactionTime`
  2 → 1.2 sn (veya seri servisi 2 paralel slota çıkar). Bu, prestij darboğazına saldıran ilk perk olur.
- **Öneri B (kod dokunuşu yoksa):** sahnede `disabledInDraft: 0 → 1` (satır **21531**). Fiyat/etki tartışmasına girme,
  havuzdan çıkar.
- Dosya: `The Main Office.unity:21531` + `Assets/NewCss/UpgradeScripts/PerkEffect.cs:128-132`

### 3.3 `leveraged_rent` (Kaldıraçlı Kira) — bedeli doymuş döngüye bağlanmış

`PerkEffect.cs:161-166` kira ×0.8 karşılığında `customerLostPrestigePenalty`'yi −0.6 → −1.2 yapıyor.
Ama **her oyuncu sayısı zaten günde ~2 müşteri kaybediyor** (§2.3), yani bedel garantili ve P ile büyüyen
prestij kanaması; kazanç ise yalnız 16 günde 4 kira ödemesinin %20'si.

| | 1P | 2P | 3P | 4P |
|---|---|---|---|---|
| kira tasarrufu | +499 | +899 | +1 198 | +1 498 |
| prestij bedelinin TL karşılığı | −754 | −1 790 | −2 677 | −3 685 |
| **net** | **−255** | **−891** | **−1 479** | **−2 187** |

- **Öneri:** bedeli müşteri döngüsünden çıkar. Yeni tanım: **kira ×0.75 + `gracePaymentPercent = 0`**
  (ucuz kira, af yok — tematik olarak "kaldıraç"a birebir uyuyor ve bedel kuyruk mekaniğine değil oyuncunun
  nakit yönetimine bağlı). Değer: 1P +624 … 4P +1 873.
- **Fiyat:** 350 → **220** (ROI ≈ 2.8, sigorta-karşıtı risk sınıfı).
- `all_in` de `gracePaymentPercent = 0` yazıyor → ikisini **aynı dışlama grubuna** koy (`BuildExclusionGroups`).
- Dosya: `PerkEffect.cs:161-166`, sahne satır 21606 (baseCost).

---

## §4 `upgradeCostMultiplierPerPlayer` — mevcut 1.15 YANLIŞ

### 4.1 Ölçek karşılaştırması

```
gelir ölçeği (FAZ1 §3.3)        : 1.00 / 1.81 / 2.44 / 3.15
kira ölçeği (baseRentByPlayer)  : 1.00 / 1.80 / 2.40 / 3.00
CANLI upgrade (1.15^(P-1))      : 1.00 / 1.15 / 1.32 / 1.52   ← ekonominin geri kalanından KOPUK
```

Sonuç — **tüm içeriğin maliyeti / harcanabilir bütçe** (kapsama oranı, OPTIMISTIC):

| | 1P | 2P | 3P | 4P |
|---|---|---|---|---|
| CANLI (×1.15) | **%209** | %132 | %112 | **%98** |
| ÖNERİ (dizi) | %105 | %105 | %105 | %103 |

Solo oyuncu içeriğin ancak **yarısını** alabiliyor, 4 oyuncu **tamamını**. Aynı çarpıklık ROI'de de görünüyor:
`gambler_case` ROI'si 1.15 ile 2.17 → 5.71 (P ile 2.6× artıyor); önerilen dizi ile 2.17 → 2.76 (neredeyse sabit).

### 4.2 Öneri

**Tercih edilen (kod dokunuşu):** `DifficultyManager`'a `baseRentByPlayerCount` deseninde bir dizi ekle:

```csharp
// DifficultyManager.cs — CalculateUpgradeCostMultiplier() yerine
[SerializeField] private float[] upgradeCostByPlayerCount = { 1.00f, 1.80f, 2.45f, 3.15f };
private float CalculateUpgradeCostMultiplier()
    => upgradeCostByPlayerCount[Mathf.Clamp(_cachedPlayerCount - 1, 0, upgradeCostByPlayerCount.Length - 1)];
```
- Dosya: `Assets/NewCss/GameState/DifficultyManager.cs:348-356`, `DifficultyManager.prefab:85`

**Kod dokunuşu istenmiyorsa (tek skaler):** `upgradeCostMultiplierPerPlayer = 1.15 → **1.47**`
→ 1.00 / 1.47 / 2.16 / 3.18, gelir ölçeğinden ortalama **%7.8** sapma (1.45 → %9.2; 1.50 → %8.0).
`[Range(1f, 2f)]` sınırı içinde. Tek kusuru 2P'de %19 fazla ucuz kalması.

### 4.3 Reroll de ölçeklenmiyor

`RerollCurve.CostForReroll` düz tablo (50/90/160/290/525) ve `UpgradePanel.cs:1071-1074` doğrudan
`SpendMoney(cost)` çağırıyor → **P çarpanı uygulanmıyor.** 4P'de reroll göreli olarak 3.15× ucuz.
- **Öneri:** reroll maliyetine de aynı çarpanı uygula (`RerollCurve.CostForReroll(i) * DifficultyManager.Instance.UpgradeCostMultiplier`).
- Tablonun kendisi doğru kalibre: 1P'de 2 reroll/gün × 16 gün = 2 240 TL, harcanabilir bütçenin %56'sı — sağlıklı bir para çıkışı (sink).
- Dosya: `Assets/NewCss/UpgradeScripts/UpgradePanel.cs:1057, 1071, 1100`

---

## §5 QUEST — EV YENİDEN ÇAPALAMASI

### 5.1 Teşhis: sistem kaçışın %1.5–3.2'si ve tier upgrade'i NEGATİF

Doğru mekanik modellendi: havuz = `tier ≤ maxTier` (`QuestManager.cs:471-485`), günde **3 kart** çekiliyor
(iadesiz, `SelectRandomQuests`), oyuncu **1** kabul ediyor (`cs:691`), reddetmek **bedava**.
Bu yüzden günlük EV = `E[max(0, 3 kartın en iyisi)]` — 4 060 kombinasyonun tamamı enumerate edildi.

**Günlük quest EV'si (OPTIMISTIC, gün 8) ve günlük net gelire oranı:**

| | T0 (Easy) | T1 (+Medium) | T2 (+Hard) |
|---|---|---|---|
| 1P | 11.7 TL (%3.2) | **7.6 (%2.0)** | **5.7 (%1.5)** |
| 2P | 18.5 (%2.7) | 21.9 (%3.2) | **20.1 (%2.9)** |
| 3P | 18.5 (%2.0) | 27.9 (%3.0) | 31.3 (%3.4) |
| 4P | 18.5 (%1.5) | 27.9 (%2.3) | 38.8 (%3.2) |

**16 günün kümülatifi ve `Görev Kademesi`'nin marjinal değeri:**

| | T0 toplam | T1 toplam | T2 toplam | L1 marj (gün 6'da) | L2 marj (gün 10'da) |
|---|---|---|---|---|---|
| 1P | 190 | 131 | 102 | **−39** | **−15** |
| 2P | 291 | 355 | 333 | +60 | **−4** |
| 3P | 295 | 439 | 508 | +94 | +56 |
| 4P | 295 | 446 | 619 | +94 | +100 |

Fiyat 80 / 100 (× P-çarpanı) → ROI: 1P **−0.5 / −0.3**, 2P 0.4 / **−0.1**, 3P 0.5 / 0.2, 4P 0.4 / 0.3.
**`Görev Kademesi` hiçbir oyuncu sayısında kârlı değil.** İki sebep:

1. **HAVUZ SEYRELMESİ.** Üst tier açılınca alt tier kartları havuzda kalıyor (`tier ≤ maxTier`). T2'de 30 kartlık
   havuzun 11'i Easy. 3 kartlık çekilişte "en iyi" çoğu zaman bir Easy kart oluyor → Hard'ın ödülü hiç görülmüyor.
2. **ERİŞİLEMEZ HEDEFLER.** 1P günlük üretim gün 8'de yalnız **5.0 kutu**. Aşağıdaki tabloda 1P'de hangi hedefin
   hangi tamamlanma olasılığını verdiği (arz/hedef oranından):

| arz tipi | 1P arz/gün | hedef 1 | 2 | 3 | 4 | 5 | 6 |
|---|---|---|---|---|---|---|---|
| üretim (raf/paket) | 5.00 | %87 | %87 | %87 | %71 | %55 | %40 |
| üretim + RENK kilidi (÷3) | 1.67 | %87 | %40 | %19 | %11 | %8 | %6 |
| tam dolan tır | 1.43 | %83 | %30 | %15 | %9 | %6 | %4 |
| telefon yanıtı | 2.55 | %87 | %73 | %41 | %25 | %16 | %12 |

Canlı Hard hedefleri (renksiz **12**, renk-kilitli **5**, tır **3**) 1P'de sırasıyla %11, %8, %15 → **EV −21 … −28 TL**.
Renk-kilitli Hard 2P'de bile **−7.6 TL** (FAZ 1 bulgusu doğrulandı).

### 5.2 Tuzak kart listesi (OPTIMISTIC, gün 8 — EV negatif olanlar)

| quest | tier | hedef | renk | ödül/ceza | 1P EV | 2P EV | 3P EV | 4P EV |
|---|---|---|---|---|---|---|---|---|
| `hard_truck_3` | Hard | 3 | — | 86/47 | **−27.7** | +20.4 | +63.2 | +69.2 |
| `hard_shelf_10` (hedef 12) | Hard | 12 | — | 57/31 | **−21.0** | +4.0 | +31.7 | +45.9 |
| `hard_pack_10` (hedef 12) | Hard | 12 | — | 57/31 | **−21.0** | +4.0 | +31.7 | +45.9 |
| `hard_*_renk` ×6 | Hard | 5 | E | 57/31 | **−24.3** | **−7.6** | +17.6 | +36.5 |
| `med_*_renk` ×6 | Medium | 3 | E | 34/19 | **−8.8** | +14.0 | +27.3 | +27.3 |
| `med_truck_2` | Medium | 2 | — | 52/29 | **−4.6** | +38.1 | +41.8 | +41.8 |
| `med_shelf_7` / `med_pack_7` | Medium | 7 | — | 34/19 | **−3.0** | +24.9 | +27.3 | +27.3 |
| `med_phone_3` | Medium | 3 | — | 40/22 | +3.5 | +3.5 | +3.5 | +3.5 |
| `easy_*_renk` ×6 | Easy | 2 | E | 18/10 | **+1.1** | +14.5 | +14.5 | +14.5 |
| `easy_shelf_6` | Easy | 6 | — | 28/15 | **+2.1** | +22.6 | +22.6 | +22.6 |

**19/30 asset 1P'de negatif ya da sıfıra yakın EV taşıyor.** Ayrıca `med_phone_3` **her** oyuncu sayısında +3.5 —
telefon yanıtı P'den bağımsız (0.30 sabit şans, `SetCallChance` gövdesi boş) olduğu için hedef 3 asla ölçeklenmiyor.

### 5.3 ÖNERİ — 3 yapısal düzeltme + yeni ödül tablosu

#### D1. Havuz politikası: **her tier'dan 1 kart** (tek en yüksek etkili düzeltme)

`QuestManager.SelectRandomQuests` yerine: 3 teklifin **her biri farklı bir tier'dan** (T2'de 1 Easy + 1 Medium + 1 Hard;
T1'de 1 Easy + 1 Medium + 1 rastgele; T0'da 3 Easy). Böylece üst tier'ın ödülü **her gün** masada oluyor ve
`Görev Kademesi` gerçek bir kilit açıyor.
- Dosya: `Assets/Scripts/Quest/Manager/QuestManager.cs:471-499`

#### D2. `targetCount` P-ölçekli olsun (ekonominin aynı ölçek vektörüyle)

Asset'lerdeki `targetCount` **1 oyuncu için** yazılsın, çalışma zamanında ölçeklensin:
```
etkinHedef = max(1, round(targetCount × ECONOMY_SCALE[P]))   // ECONOMY_SCALE = {1.00, 1.80, 2.45, 3.15}
```
**İSTİSNA: `AnswerPhone` quest'leri ölçeklenmez** — telefon çalma şansı P'den bağımsız (arz 2.55/gün, sabit).

Doğrulama — bu kural tamamlanma olasılığını her P'de sabit tutuyor:

| quest tipi | 1P hedef | 2P | 3P | 4P | her P'de p |
|---|---|---|---|---|---|
| üretim (raf/paket) Easy | 3 | 5 | 7 | 9 | %87 |
| üretim Medium | 4 | 7 | 10 | 13 | %71 |
| üretim Hard | 5 | 9 | 12 | 16 | %55 |
| renk-kilitli | 1 | 2 | 2 | 3 | %87 |
| tır | 1 | 2 | 2 | 3 | %83–87 |
| telefon (**ölçeklenmez**) | 1 | 1 | 1 | 1 | %87 |

- Dosya: `Assets/Scripts/Quest/Manager/QuestManager.cs` (QuestProgress kurulumu, `cs:462`) + `QuestData.cs:48-58` civarı

#### D3. Tier başına TEK ödül/ceza çifti + tier'e göre AZALAN ceza oranı

Tuzak kart problemi ödül dağınıklığından geliyor (Easy içinde 18 ve 28 TL, Hard içinde aynı 57 TL üç ayrı zorlukta).
Her tier tek çift kullansın. Ceza oranı üst tier'da **düşsün** — üst tier daha düşük olasılıkla tamamlanıyor,
sabit oran korunursa EV çöküyor:

`EV = R × (p(1+c) − c)`, `c = ceza/ödül`

| tier | hedef p | ceza oranı c | **ödül** | **ceza** | prestij +/− | EV | 1P gün-8 netinin %'si | başabaş p |
|---|---|---|---|---|---|---|---|---|
| **Easy** | 0.87 | %55 | **28** | **15** | +0.70 / −0.40 | **22.4** | %6.1 | %35 |
| **Medium** | 0.71 | %45 | **60** | **27** | +1.50 / −0.68 | **34.8** | %9.4 | %31 |
| **Hard** | 0.55 | %35 | **150** | **53** | +3.75 / −1.33 | **58.7** | %15.9 | %26 |

Prestij ödülü para/40 oranında tutuldu (canlı asset'lerin 37-43 oranıyla uyumlu, 0–100 skalasında).

**Doğrulama — D1+D2+D3 birlikte:**

| | günlük EV T0 | T1 | T2 | `Görev Kademesi` L1 (gün 6, 10 gün) | L2 (gün 10, 7 gün) |
|---|---|---|---|---|---|
| değer | 22.4 | 34.8 | 58.7 | **+124 TL** (fiyat 80 → ROI 1.5) | **+167 TL** (fiyat 100 → ROI 1.7) |

16 günlük kümülatif quest geliri (T0 gün 1-5 / T1 gün 6-9 / T2 gün 10-16): **662 TL = 1P koşusunun %10.2'si.**

#### D4. Ödül P-ölçeklemesi (opsiyonel ama önerilir)

Ödül sabit kalırsa quest sistemi P ile **gerileyen** bir kaldıraç oluyor: 1P %10.2 → 4P %3.2.
Bu bir hata değil, bilinçli bir tercih olabilir (1P STRICT tek gerçek iflas senaryosu — FAZ 1 §3.5 — ve quest onun
can simidi). Ama P-nötr istenirse **aynı** ölçek vektörünü uygula:

| | 1P | 2P | 3P | 4P |
|---|---|---|---|---|
| ödül sabit → koşu payı | %10.2 | %5.6 | %4.2 | %3.2 |
| ödül × {1.00, 1.80, 2.45, 3.15} → koşu payı | %10.2 | **%10.1** | **%10.2** | **%10.2** |

> **Tek ölçek vektörü fikri:** kira (zaten ≈), upgrade maliyeti, reroll maliyeti ve quest ödülü aynı
> `ECONOMY_SCALE_BY_PLAYERS = {1.00, 1.80, 2.45, 3.15}` dizisini kullansın. Tek yerden ayarlanabilir bir ekonomi ölçeği.

#### D5. Ödül/ceza asimetrisi — mevcut %55 caydırıcı DEĞİL, kalması doğru

| ceza / ödül | EV > 0 için gereken p |
|---|---|
| %35 (Hard önerisi) | %25.9 |
| %45 (Medium önerisi) | %31.0 |
| **%55 (canlı)** | **%35.5** |
| %100 | %50.0 |
| %150 | %60.0 |

%55 asimetri "açıkça imkânsız değilse kabul et" eşiği (p > %35) demek. Kabul bedava ve hedef ekranda görünüyor,
yani karar zaten bilgi-tam; eşiği %50'ye çıkarmak (ceza = ödül) kararı derinleştirmez, yalnız RNG/event
tilt'ini artırır. **Asimetriyi %55 civarında tut, tier yükseldikçe DÜŞÜR** (D3). Dokunulacak tek şey ödülün
kendisi ve hedefler.

### 5.4 `PlaceBoxOnShelf` EXPLOIT'i (P0, ekonomik değil mekanik)

`ShelfState.PlaceItemInSlot` her yerleştirmede event basıyor, **dedup yok**:
```csharp
// ShelfState.cs:604-609
var boxInfo = item.GetComponent<BoxInfo>();
if (boxInfo != null) Quest.QuestTracker.NotifyBoxPlacedOnShelf(boxInfo.boxType);
```
Yalnız **dolu** kutu rafa konabiliyor (`PlayerInventory.Shelf.cs:330-371` → `return boxInfo.isFull`), yani
1 gerçek üretim gerekiyor. Ama `RequestTakeFromShelfServerRpc` kutuyu geri veriyor ve tekrar koymak event'i
**yeniden** tetikliyor → **tek dolu kutu ile hedef 12 bile ~30 saniyede tamamlanır.**

Etki alanı: `questType: 1` olan **13/30 asset** (Easy 5, Medium 4, Hard 4). Yani içeriğin %43'ünde tamamlanma
olasılığı fiilen ~1.0. Exploit bilinirse günlük quest EV'si 1P'de 11.7 → **28.5 TL**'ye çıkıyor (T2'de) ve tüm
zorluk ayarı anlamsızlaşıyor.

- **Öneri (gameplay):** aynı `NetworkObject` id'si için tekrar sayma — quest ilerlemesinde işlenen kutu id'lerini
  bir HashSet'te tut, ya da event'i yalnız "yeni spawn edilmiş / tırdan gelmemiş" kutu için bas.
- Dosya: `Assets/NewCss/PickUpScripts/ShelfState.cs:604-609` + `Assets/Scripts/Quest/Manager/QuestTracker.cs:34-38`

### 5.5 Gün 16 quest'i hiç kapanmıyor (cezasız bedava opsiyon)

Settlement `DayCycleManager.OnNewDay` → `QuestManager.HandleNewDay` → `SettleAcceptedQuestsForDayEnd`
(`QuestManager.cs:356-365`) zincirine bağlı. Gün 16 `MAX_DAYS` olduğu için gün 17 geçişi yok → ne ödül ne ceza.
Önerilen Hard cezası 53 TL / −1.33 prestij olduğu için exploit'in değeri de büyüyor.

- **Öneri (mekanik, ekonomik değer gerekmez):** `DayCycleManager`'ın oyun-sonu (gün 16 bitişi) akışına
  `SettleAcceptedQuestsForDayEnd()` çağrısını ekle — `OnNewDay`'e değil, **gün süresi dolduğunda** tetiklenen
  son-gün dalına. Alternatif (daha ucuz): gün 16'da quest **teklif edilmesin** (`QuestManager.AssignDailyQuests`
  başında `if (currentDay >= MAX_DAYS) return;`).
- Dosya: `Assets/Scripts/Quest/Manager/QuestManager.cs:356-365, 440-451`

### 5.6 Ölçülemeyen ama düzeltilmesi gereken küçük kalemler

| Konu | Durum | Öneri |
|---|---|---|
| `hasBuff: 0` — 30 asset'in hepsinde | Buff sistemi (`BuffManager.cs`) hiç beslenmiyor | Ya Hard tier'a 1-2 buff bağla (ör. "bugün ödül +%10") ya da UI'dan kaldır |
| `CompleteMinigame(0)`, `MakePackagingMistake(5)`, `CompleteSpecificColorTruck(6)` | **0 asset** | `QuestEnums` içinde ölü tip bırakma; ya asset yaz ya enum'dan çıkar |
| Hard tier'da **telefon quest'i yok** | Easy/Medium'da var | Kasıtlıysa sorun yok; telefon arzı P-flat olduğu için Hard'a UYGUN DEĞİL — kasıtlı bırak |
| `Q_Hard_2_Shelf.asset` id'si `hard_shelf_10` ama `targetCount: 12` | isim/ID senkron değil | ID'leri yeni hedeflere göre yenile (ID'ye güvenip sayı okuma) |

---

## §6 UYGULAMA TABLOSU — MEVCUT → ÖNERİLEN

Tüm `baseCost` değerleri **1 oyuncu** cinsindendir; çalışma zamanında `ECONOMY_SCALE[P]` ile çarpılır.
ROI sütunları önerilen fiyat + önerilen P-çarpanı ile hesaplanmıştır.

### 6.1 Omurgalar

| Upgrade | Alan | MEVCUT | **ÖNERİ** | Gerekçe (sayı) | Dosya:satır |
|---|---|---|---|---|---|
| **Geniş Ambar** | `maxLevel` | 9 | **2** | L1 değeri 1P −163 / 2P-4P 0; L2-L9 ek değer ≈ 0. 9 seviye draft havuzunun %25'ini ölü kartla dolduruyor | `unity:21184` |
| | `baseCost` | 50 | **60** | Kalan 2 seviye "fiziksel stok tamponu" hissi için; ölçülebilir gelir katkısı yok | `unity:21185` |
| | `costStep` | 10 | **30** | Toplam 810 → 150 TL | `unity:21186` |
| **Paketleme İst.** | `maxLevel` | 3 | **1** | Sahnede yalnız 2 `Table` var; seviye 2-3 için 650 TL karşılığı hiçbir obje aktifleşmiyor | `unity:21214` |
| | `baseCost` | 100 | **150** | 1P-eşdeğer değer 535 TL, hedef ROI 3.5 → 153. Oyunun tek gerçek üretim upgrade'i | `unity:21215` |
| | `contentText` | "adds +1 table" | **"+1 paketleme masası (2+ oyuncuda etkili)"** | 1P'de değeri 0 → tuzak olmasın | `unity:21208` |
| | (art işi) | — | 3./4. masa eklenirse `maxLevel 3`, `costStep 200` geri gelir | 4 masaya kadar 4P'de %26'ya varan üretim kazancı var | — |
| **Ek Hangar** | `maxLevel` | 2 | **1** | 3. hangar her iki bantta **0 TL**. Alternatif: seviye 2'ye BAŞKA bir etki bağla (ör. `Random.Range(2,6)` → `(3,7)` kargo) | `unity:21302` |
| | `baseCost` | 200 | **200 (dokunma)** | STRICT 1P-eşdeğer değer 933 TL / ROI 3.5 → 233; OPT'ta 0. İki bandın ortalaması 200'ü doğruluyor | `unity:21303` |
| **Görev Kademesi** | `baseCost/step` | 80 / 20 | **80 / 20 (dokunma)** | §5.3 düzeltmeleri UYGULANDIKTAN SONRA ROI 1.5 / 1.7. Şu anki hâliyle ROI negatif — **fiyat değil içerik sorunu** | `unity:21371-21372` |
| Kapalı 5 omurga | — | `disabledInDraft: 1` | **dokunma** | Doğru karar; `Sağlam Kasa`'nın NO-OP gövdesi de doğru (eski hâli kutu ödülünü 50 → 25'e DÜŞÜRÜYORDU) | — |

### 6.2 Perkler

| Perk | Alan | MEVCUT | **ÖNERİ** | Yeni ROI (1P/2P/3P/4P) | Gerekçe | Dosya:satır |
|---|---|---|---|---|---|---|
| `all_in` | baseCost | **800** | **320** | 2.9 / 3.3 / 3.6 / 3.7 | Değer 941–3 762; 800 TL'de ROI 1.18–1.49. Etkisi `gambler_case` ile neredeyse aynı (+%25 vs +%30) ama grace'i de siliyor → gambler'dan UCUZ olmalı | `unity:21666` |
| `prestige_broker` | baseCost / costStep | **510 / −5** | **130 / +15** | 2.6 / 2.7 / 2.9 / 3.0 | ROI 0.35–0.84 (en kötü kart). `costStep: −5` seviye 2'yi seviye 1'den UCUZ yapıyor — açık hata | `unity:21410-21411` |
| | *(etki)* | `bonusPerTier += 0.5/lvl` | **`+= 1.0/lvl` (5→6→7)** | — | 130 TL'lik bir T3 kartı anlamsız derecede küçük; etkiyi 2× yapıp fiyatı 130'da tutmak daha iyi bir kart üretir. Prestij 47'de kutu ödülü 105 → 127 (+%21) | `PerkEffect.cs:82-86` |
| `high_volatility` | baseCost | 450 | **320** | 2.5 / 2.7 / 3.0 / 3.1 | Değer 812–3 145 → ROI 1.80–2.22. `gambler_case` ile aynı bantta olmalı (etkisi prestij tier'ıyla da ölçekleniyor → geç oyunda daha iyi) | `unity:21626` |
| `gambler_case` | baseCost | 400 | **350** | 2.5 / 2.8 / 3.0 / 3.1 | ROI 2.17–2.76, hedef 3.0. Yanlış-renk oranı varsayımına duyarlı (Normal %12 → Yavaş %22'de değeri düşer) | `unity:21546` |
| `prestige_master` | baseCost / costStep | 280 / 100 | **175 / 25** | 2.4 / 2.8 / 3.0 / 3.1 | Değer L1 432–1 736, L2 kümülatif 904–3 626. Mutlak TL'de en güçlü perk ama 280'de ROI 1.5–2.0 | `unity:21430-21431` |
| `cheap_rent` | baseCost / costStep | 130 / 30 | **90 / 30** | 1.5 / 1.5 / 1.5 / 1.4 | 3 seviye tasarrufu 1P 308 TL / 4P 928 TL; 480 TL fiyat → ROI 0.52–1.03. T3 (gün 9+) olduğu için ilk 2 kira dönemi zaten ödenmiş → gerçek tasarruf daha da az | `unity:21391-21392` |
| | *(etki, tercih edilen)* | `rentGrowthMultiplier −0.03/lvl` | **`rentScaledMultiplier` −%7.5/lvl (1.00→0.925→0.85→0.775)** | — | Büyüme üssüne dokunmak 16 günde yalnız 4 ödemeyi etkiliyor → yapısal olarak zayıf. Düz % indirim tüm kirayı etkiler, tasarruf 1P 549 / 4P 1 642. **DİKKAT:** `leveraged_rent` de `rentScaledMultiplier` yazıyor → çarpışma, çarpımsal birleştir | `PerkEffect.cs:75-79` |
| `leveraged_rent` | baseCost | 350 | **220** | 2.8 / 2.8 / 2.8 / 2.7 | Etki değişikliği şartıyla (§3.3). Mevcut hâliyle her P'de negatif | `unity:21606` |
| | *(etki)* | kira×0.8 + `customerLostPrestigePenalty=−1.2` | **kira×0.75 + `gracePaymentPercent=0`** | — | Bedeli doymuş müşteri döngüsünden çıkar; `all_in` ile aynı dışlama grubuna al | `PerkEffect.cs:161-166` |
| `fast_hangar` | baseCost | 280 | **120** | 0 / 0.9 / 1.6 / 1.9 (STRICT) | OPT bantta değer **0** (hangar süresi bağlayıcı değil, FAZ1 §2.3: tır penceresi %10-42 doluluk). STRICT 1P-eşdeğer 134 TL | `unity:21450` |
| | *(alternatif)* | `hangarStay ×1.30` | üretime dokunan bir etkiye taşı | — | Hangar-zamanlaması perkleri **yapısal olarak değersiz** — tavan 2.4–9× fazla geniş | `PerkEffect.cs:98-103` |
| `patient_customers` | baseCost | 220 | **120** | 0 (EV yok, varyans azaltır) | Sabır bağlayıcı kısıt değil; seri servis döngüsü bağlayıcı. Ölçülebilir TL etkisi 0 | `unity:21507` |
| | *(alternatif, ÖNERİLİR)* | `patienceMultiplier=1.25` | **`interactionTime` 2 → 1.2 sn** (seri döngüyü kısaltır) | — | Seri servis tavanı 11.4 → 13.6 müşteri/gün → +0.45 prestij/gün. Prestij darboğazına saldıran ilk perk olur | `PerkEffect.cs:121-125` |
| `energetic_crew` | baseCost | 160 | **100** | 0 (konfor) | Stamina'nın ekonomik modeli yok; kapalı `Dinç Ekip` omurgasının duplikesi | `unity:21469` |
| `bulk_buy` | baseCost | 150 | **80** | 1.9 (her P'de sabit) | Değeri tam olarak "aldığın kartın %50'si" → 150 TL'de ROI 1.00, yani nakit-nötr bir kart | `unity:21685` |
| `long_queue` | `disabledInDraft` | 0 | **1** *(veya etkiyi değiştir)* | — | −449 … −5 392 TL; STRICT'te iflasa sebep. §3.2 | `unity:21531` |
| `overtime` | *(kod)* | `= 160+20` (=180) | **`= taban × 1.125` (200→225)** | 2.7 / 2.6 / 2.7 / 2.5 | Canlı hâli günü 200→180 kısaltıyor (−%10 gelir). Düzeltildikten sonra **fiyat 300 doğru** | `PerkEffect.cs:196-200` |
| `agile_crew` | baseCost | 180 | **180 (dokunma)** | 2.9 / 3.0 / 3.3 / 3.4 | Yeni P-çarpanıyla hedefe oturuyor. +%15 moveSpeed → +%10 üretim varsayımıyla; +%15 üretim çıkarsa ROI 4.3-5.2 olur → **playtest'te ölçülmeli** | — |
| `phone_line` | baseCost | 160 | **160 (dokunma)** | 4.1 / 3.0 / 2.6 / 2.4 | P-flat fayda olduğu için ROI'nin P ile düşmesi doğru (solo kartı). **UYARI:** çalma şansı 0.30→0.45 ekranda çalma süresini 75 → 112 sn/gün'e çıkarıyor (gün 1'in %56'sı) — bu zaman maliyeti modelde YOK, playtest'te ölçülmeli | — |
| `emergency_brake` | baseCost | 250 | **250 (dokunma)** | 0 (OPT) / 2.0 (STRICT) | Kasıtlı olarak banda bağlı bir sigorta. **Ama `tier: 1` (T2, gün 5) YANLIŞ** — 1P STRICT gün **4**'te iflas ediyor, perk ihtiyaç anından SONRA açılıyor → `tier: 0` yap | `unity:21649` |

### 6.3 Toplam etki

| | MEVCUT | ÖNERİ |
|---|---|---|
| Toplam içerik maliyeti (1P) | 8 335 TL | **4 210 TL** |
| Kapsama oranı 1P / 2P / 3P / 4P | %209 / %132 / %112 / %98 | **%105 / %105 / %105 / %103** |
| Negatif değerli kart sayısı | 3 (+1 kısmen) | **0** |
| Ölü satın-alma olayı (değer = 0) | 14 seviye (Ambar L2-9, Paketleme L2-3, Hangar L2, fast_hangar, patient, energetic) | **3** (patient/energetic/emergency — bilinçli konfor+sigorta) |

---

## §7 KABUL SIRASI (uygulama önceliği)

| Öncelik | İş | Neden önce |
|---|---|---|
| **P0** | `overtime` kod düzeltmesi (`PerkEffect.cs:196-200`) | 300 TL karşılığı 900–2 240 TL kaybettiriyor |
| **P0** | `long_queue` havuzdan çıkar (`unity:21531`) | STRICT'te iflasa sebep |
| **P0** | `PlaceBoxOnShelf` dedup (`ShelfState.cs:604-609`) | 13/30 quest'i anlamsızlaştırıyor; quest ayarı bunun üstüne yapılamaz |
| **P1** | `upgradeCostMultiplierPerPlayer` → dizi `{1.00, 1.80, 2.45, 3.15}` | Tek başına tüm ROI tablosunu P-tutarlı yapıyor |
| **P1** | `maxLevel` kısmaları (Ambar 9→2, Paketleme 3→1, Hangar 2→1) | 14 ölü satın-alma olayını havuzdan çıkarıyor, draft kalitesi |
| **P1** | Quest D1 (her tier'dan 1 kart) + D2 (P-ölçekli hedef) + D3 (tier ödül tablosu) | Üçü BİRLİKTE uygulanmalı; ayrı ayrı işe yaramaz |
| **P2** | Perk fiyat düzeltmeleri (§6.2) | Bağımsız, tek tek uygulanabilir |
| **P2** | `leveraged_rent` / `cheap_rent` / `patient_customers` etki değişiklikleri | Kod dokunuşu gerektiriyor, fiyat düzeltmesinden ayrılabilir |
| **P2** | `emergency_brake` `tier: 1 → 0`, reroll P-ölçeklemesi, gün-16 settlement | Küçük, bağımsız |

---

## §8 FAZ 1'E GERİ BİLDİRİM (sim düzeltmesi — sim.js'e DOKUNULMADI)

| Alan | FAZ 1 / sim değeri | CANLI değer | Etki |
|---|---|---|---|
| `ASSUMED.startingActiveInteractables` | 3 ("~tahmin") | **5** (4 aktif `ShelfState` + 1 `DisplayTable`) | Talep 1P 9→12, 2P 11→16, 3P 14→20, 4P 16→24. Yani **her** oyuncu sayısı seri servis tavanının üstünde; 1P bile günde 0.64 müşteri kaybediyor (FAZ1 "1P OPT'ta kayıp 0" diyordu) |
| Masa çekişmesi | modelde YOK (`prodRate = kutuDk × P × emekPayı`) | Sahnede **1 aktif `Table`**, tek item taşıyor | FAZ 1 §3 gelir tablosu 2P-4P için **%4–26 fazla iyimser** (Paketleme L1 alınmadan) |

Bu iki düzeltme FAZ 1 §3 tablosunun *sıralamasını* değiştirmiyor, seviyesini biraz aşağı çekiyor.
FAZ 3'ün tüm ROI hesapları bu düzeltmelerle yapıldı (izole `require` ile `ASSUMED` bellekte güncellenip koşuldu).

## §9 ⚠️ FAZ 2 İLE ÇAKIŞMA — KOŞULLU DÜZELTMELER (uygulamadan ÖNCE oku)

FAZ 2 paralel yürütüldü ve **FAZ 1 gelir tabanını değiştiren** bir paket önerdi
(`plans/economy-rebuild-2026-07-30-faz2.md`): prestij çift-çarpan, kira `g=1.35` + P-ölçeği
`{500,1000,1550,2150}`, **2 paralel servis istasyonu**, **P-bazlı kargo**. Yeni taban:
kümülatif net **1P 6 154 / 2P 12 251 / 3P 19 173 / 4P 26 352**, gelir ölçeği **1 : 1.99 : 3.12 : 4.28**,
upgrade bütçesi (kira sonrası fazla) **1P 2 838 / 2P 5 618 / 3P 8 891 / 4P 12 091**.

Aşağıdaki 6 FAZ 3 kalemi FAZ 2 paketi uygulanırsa **değişir**. Diğer her şey (özellikle §3'teki 3 zararlı
kart, §4.3 reroll, §5.4 exploit, §6.1 maxLevel kısmaları) FAZ 2'den **bağımsız** ve olduğu gibi geçerli.

| # | Kalem | FAZ 3 (FAZ 1 tabanı) | **FAZ 2 paketi sonrası** |
|---|---|---|---|
| **C1** | `ECONOMY_SCALE_BY_PLAYERS` | {1.00, 1.80, 2.45, 3.15} | **{1.00, 2.00, 3.10, 4.25}** — yeni gelir ölçeğine ±%1 oturuyor. FAZ 2'nin tek-skaler önerisi 1.62 → 1.00/1.62/2.62/4.25, 2P'de %19 / 3P'de %16 fazla ucuz. **Dizi kullan.** |
| **C2** | Toplam içerik maliyeti (1P) | 4 210 TL (kapsama %105) | Aynı fiyatlarla kapsama **%148** (her P'de tutarlı). Kapsamayı %105'e çekmek için 1P içeriği **~2 980 TL** olmalı → §6 fiyatlarını **×0.71**. **Karar önerisi: fiyatları OLDUĞU GİBİ bırak, %148 kapsamayı kabul et** — oyuncu içeriğin ~2/3'ünü alabilir, bu roguelite draft'ı için daha iyi bir seçim baskısı üretir (FAZ 2 da "STRICT'te fazla ~0 → upgrade lüks olmalı" diyor). |
| **C3** | **`Geniş Ambar`** | maxLevel 9 → **2**, değer ≤ 0 | **DEĞER KAZANIYOR.** 2 istasyonla seri tavan 11.4 → 22.7; talep (12/16/20/24) tavanın ALTINA düşüyor → raf eklemek gerçekten müşteri kazandırıyor: L1 prestij/gün **1P +0.80 · 2P +1.20 · 3P +0.93 · 4P −0.35**. 1P'de L1'in TL değeri ≈ **+300**. → **maxLevel 9 → 3** (4P'de zaten negatife dönüyor), `baseCost 50 → 100`, `costStep 10 → 50` (100/150/200 = 450 TL, ROI ~2.0). Kart metnine "3+ oyuncuda etkisi azalır" notu. |
| **C4** | `cheap_rent` / `leveraged_rent` | fiyat 130→**90** / 350→**220** | Kira toplamı 2 497→3 316 (1P) ve 7 490→**14 261** (4P), büyüme üssü 1.15→1.35 → kira perkleri **%26–82 daha değerli**. `cheap_rent` L3 kümülatif tasarruf 309/557/743/928 → **392/785/1 217/1 688**. → `cheap_rent` **baseCost 90 → 130 (yani MEVCUT değeri koru, dokunma)** ve **etki değişikliğine (`rentScaledMultiplier`) GEREK KALMAZ** — `g=1.35` üssü zaten tıraşlanacak kadar büyük. `leveraged_rent` **220 → 300**, etki değişikliği (bedeli müşteri döngüsünden çıkar) **hâlâ geçerli ve gerekli**. |
| **C5** | `prestige_master` | 280 → **175** | `customerServedPrestigeBonus` tabanı 0.2 → **0.4** olunca perkin `+0.06/lvl`'i göreli olarak %30 → **%15**'e düşüyor → değeri **yarıya** iniyor. İki seçenek: (a) etkiyi de ×2 yap (`+0.06 → +0.12/lvl`, 0.4→0.52→0.64) ve **fiyat 175'te kalsın**; (b) etkiye dokunma, fiyat **175 → 90**. **(a) tercih edilir** — perkin gücü FAZ 2 ölçeğiyle senkron kalır. `prestige_broker` etkilenmiyor (ödül eğrisi ~aynı kalıyor). |
| **C6** | Quest tır hedefleri | tır 1P = 1.43/gün → hedef 1 (p %83) | P-bazlı kargo ile 1P tam dolan tır **1.43 → 3.33**: hedef 1→%87, **2→%87**, 3→%62, 4→%40. → `CompleteTruck` quest'leri gerçek bir tier merdiveni kazanıyor: **Easy tır 1, Medium tır 2, Hard tır 3** (p %87/%87/%62) ve **P-ölçekleme tır quest'lerine UYGULANMAZ** (kargo boyutu zaten P ile büyüyor, çift sayma olur). FAZ 2 da bunu doğruluyor: "1P STRICT tamDolanTir 0 → 2.27 ⇒ Easy '1 tır tamamla' ilk kez mümkün". |
| **C7** | Quest prestij ödül/cezası | Easy +0.70/−0.40, Med +1.50/−0.68, Hard +3.75/−1.33 | FAZ 2 "quest prestij alanları **×2**" diyor (0-100 skalası birikim senkronu). → **Easy +1.40/−0.80 · Medium +3.00/−1.36 · Hard +7.50/−2.66**. Para ödülleri (28/60/150) **değişmez** — para EV'si prestij paketinden bağımsız. |

**Uygulama sırası kararı:** FAZ 2 paketi FAZ 3'ten ÖNCE uygulanmalı (FAZ 2 kendi içinde bölünemez bir bütün).
FAZ 3'ün FAZ 2'den bağımsız kalemleri (P0 listesi, §7) **paralel** gidebilir.

---

## §10 ÖLÇÜLMESİ GEREKEN 4 PLAYTEST SAYISI (duyarlılık sırasına göre)

1. **`kutu/dk/oyuncu`** — FAZ 1 uyarısı; 1.2 → 2.0 arası 1P kümülatifini %117 değiştiriyor.
2. **Masa meşgul süresi `S`** — 4 sn ↔ 8 sn arası `Paketleme İstasyonu`'nun değerini **4×** değiştiriyor (§2.2).
3. **`agile_crew`'in üretime yansıması** — +%15 moveSpeed kaç % üretim? +%10 ile ROI 2.9, +%15 ile 4.3.
4. **Telefon yanıtlamanın oyuncu-saniyesi maliyeti** — `phone_line` gün 1'de ekranda 112 sn çalma üretiyor;
   yanıtlama üretimden ne kadar zaman çalıyor? Bu sayı `phone_line`'ı pozitiften negatife çevirebilir.
