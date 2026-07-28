# 🧾 Görev Ekleme Rehberi — 30 Quest, Elle Yazım

> **Amaç:** `Assets/Resources/Quests/` klasörü boş. Bu dosyaya bakarak 30 görevi tek tek Unity'de oluşturacaksın.
> **Durum tarihi:** 2026-07-28 · Kod sözleşmesi: `Assets/Scripts/Quest/Data/QuestData.cs`
> Ödül/ceza sayıları **economist onaylı** (tier EV'leri eski bantlarda: Easy ~20 · Medium ~36 · Hard ~60 TL, sapma ±4%).

---

## 1. Nasıl oluşturulur

1. Project penceresinde **`Assets/Resources/Quests/`** klasörüne gir. (Bu klasör adı ZORUNLU — `QuestManager` görevleri buradan yüklüyor. Başka klasöre koyarsan havuza girmez.)
2. Sağ tık → **Create → Cargor → Quest Data**
3. Dosya adını tablodaki **"Dosya adı"** sütunundan yaz (ör. `Q_Easy_1_Truck`).
4. Inspector'da aşağıdaki alanları tablodan doldur.
5. `icon` alanına o görevin sprite'ını sürükle (üreteceğin ikonlar).

⚠️ **`questId` her görevde BENZERSİZ olmalı.** Boş bırakırsan kod otomatik rastgele ID üretir — çalışır ama tabloda yazan ID'leri kullanmanı öneririm (log okuması kolay olur). Bir asset'i Ctrl+D ile kopyalarsan **questId de kopyalanır** → mutlaka değiştir.

---

## 2. Inspector alanları — ne işe yarıyor

| Alan | Ne yapar | Tuzak |
|---|---|---|
| **questId** | Benzersiz kimlik | Ctrl+D kopyalamada aynı kalır → çakışma |
| **questTitle** | Kartın üstündeki başlık | — |
| **questDescription** | Kartta yazan açıklama | **Boş bırakırsan** koddan otomatik metin üretilir. Otomatik metin PackToy'da "**{renk} oyuncak paketle**" diyor — giysi/cam görevlerinde yanlış olur. Bu yüzden tablodaki açıklamaları elle yaz. |
| **icon** | Kart ikonu (Sprite) | Boşsa prefab'ın varsayılan ikonu kalır |
| **tier** | Easy / Medium / Hard | Oyuncunun "Görev Kademesi" upgrade seviyesi ≥ tier ise havuza girer. Kademe 0 = sadece Easy. |
| **questType** | Hangi sistem takip edecek | Yanlış tip = görev asla ilerlemez |
| **requirement → targetCount** | Hedef sayı | — |
| **requirement → requireSpecificBoxType** | Renk filtresi açık/kapalı | Kapalıysa `requiredBoxType` ne seçilirse seçilsin **yok sayılır** |
| **requirement → requiredBoxType** | Yellow / Blue / Red | Renk↔kategori eşlemesi §4'te |
| **requireSpecificTruckColor / requiredTruckColor** | Sadece `CompleteSpecificColorTruck` için | **Bu 30 görevin hiçbirinde kullanılmıyor — kapalı bırak** |
| **moneyReward** | Para ödülü | 0 = kartta para kutusu gizlenir |
| **prestigeReward** | Prestij ödülü (ondalık serbest) | 0 = prestij kutusu gizlenir |
| **moneyPenalty** | Para cezası | **POZİTİF yaz.** Kod `-Mathf.Abs()` uyguluyor, eksi yazsan da doğru çalışır ama kafa karıştırır. |
| **prestigePenalty** | Prestij cezası | Aynı — pozitif yaz |
| **hasBuff** | Ek etki var mı | Kapalıysa kartın "ek etki" kutusu hiç görünmez. **Bu 30 görevin hepsinde KAPALI.** |

**Ceza ne zaman işler:** görevi *kabul edip* gün sonunda tamamlayamazsan. Kabul etmediğin görevin cezası yoktur.

---

## 3. Buff kullanmak istersen (bu 30 görevde yok — ileride)

`hasBuff` açıp `buffType` seçersen dikkat: **bazı türlerin oyunda karşılığı yok.**

| buffType | Çalışıyor mu | Ne yapar |
|---|---|---|
| `TempMoneyBoost` | ❌ **ÖLÜ** | Kartta yazar, **hiçbir etkisi yok** — `TempMoneyPerBox`'ı okuyan tek bir sistem yok. ⚠️ Ve bu **varsayılan değer**, yani `hasBuff`'ı açıp tür seçmezsen bunu seçmiş olursun. |
| `TempSpeedBoost` | ✅ | `buffDurationDays` gün boyunca oyuncu hızı +miktar |
| `MoveSpeed` / `WalkSpeed` | ✅ ama **KALICI** | Hiç bitmez, üst üste birikir. Her gün alınabilen bir görevde vermek ekonomiyi kırar. |
| `MaxStamina` | ✅ **KALICI** | `sprintDuration` +miktar |
| `StaminaRegenRate` | ✅ **KALICI** | — |
| `DayDuration` | ✅ **KALICI** | Gün süresi (saniye) +miktar |
| `MaxQueueSize` | ✅ **KALICI** | Kuyruk kapasitesi +miktar |
| `CustomerWaitTime` | ✅ **KALICI** | Müşteri sabrı +miktar saniye |
| `PenaltyReduction` | ✅ **KALICI** | Sonraki görev cezalarını %miktar azaltır |

`buffAmount` **negatif** yazılabilir (debuff), `buffIsPenalty` işaretlenirse etki ödül değil ceza tarafında uygulanır.
Kalıcı buff'lar `buffDurationDays`'i yok sayar. **Yeni buff'lı görev yazacaksan değerleri economist'e sor** — kalıcı buff bileşik büyüyor.

---

## 4. Renk ↔ kategori eşlemesi (ezberle, en sık yapılan hata)

| requiredBoxType | Kutu rengi | Paketleme kategorisi |
|---|---|---|
| `Yellow` | Sarı | **Giysi** |
| `Blue` | Mavi | **Cam** |
| `Red` | Kırmızı | **Oyuncak** |

Yani "3 giysi paketle" görevinde `requiredBoxType = Yellow` seçilir.

---

## 5. 🟢 EASY — 11 görev (`tier = Easy`)

Hepsinde: `requireSpecificTruckColor = kapalı`, `hasBuff = kapalı`.

| Dosya adı | questId | questTitle | questDescription (elle yaz) | questType | targetCount | Renk filtresi | Para öd. | Prestij öd. | Para ceza | Prestij ceza |
|---|---|---|---|---|---|---|---|---|---|---|
| `Q_Easy_1_Truck` | `easy_truck_1` | Tek Sefer | 1 tır tamamla | CompleteTruck | 1 | — kapalı | **28** | **0.7** | **15** | **0.4** |
| `Q_Easy_2_Shelf` | `easy_shelf_4` | Raf Düzeni | Rafa 4 kutu yerleştir | PlaceBoxOnShelf | 4 | — kapalı | **18** | **0.4** | **10** | **0.2** |
| `Q_Easy_11_ShelfBig` | `easy_shelf_6` | Vardiya Sonu Düzeni | Rafa 6 kutu yerleştir | PlaceBoxOnShelf | 6 | — kapalı | **28** | **0.7** | **15** | **0.4** |
| `Q_Easy_3_ShelfRed` | `easy_shelf_red` | Kırmızı Reyon | Rafa 2 kırmızı kutu yerleştir | PlaceBoxOnShelf | 2 | ✔ **Red** | **18** | **0.4** | **10** | **0.2** |
| `Q_Easy_7_ShelfYellow` | `easy_shelf_yellow` | Sarı Reyon | Rafa 2 sarı kutu yerleştir | PlaceBoxOnShelf | 2 | ✔ **Yellow** | **18** | **0.4** | **10** | **0.2** |
| `Q_Easy_8_ShelfBlue` | `easy_shelf_blue` | Mavi Reyon | Rafa 2 mavi kutu yerleştir | PlaceBoxOnShelf | 2 | ✔ **Blue** | **18** | **0.4** | **10** | **0.2** |
| `Q_Easy_4_Pack` | `easy_pack_4` | Paket Mesaisi | 4 paket hazırla | PackToy | 4 | — kapalı | **18** | **0.4** | **10** | **0.2** |
| `Q_Easy_5_PackToy` | `easy_pack_toy` | Oyuncakçı | 2 oyuncak paketi hazırla | PackToy | 2 | ✔ **Red** | **18** | **0.4** | **10** | **0.2** |
| `Q_Easy_9_PackCloth` | `easy_pack_cloth` | Askılık | 2 giysi paketi hazırla | PackToy | 2 | ✔ **Yellow** | **18** | **0.4** | **10** | **0.2** |
| `Q_Easy_10_PackGlass` | `easy_pack_glass` | Dikkatli Eller | 2 cam paketi hazırla | PackToy | 2 | ✔ **Blue** | **18** | **0.4** | **10** | **0.2** |
| `Q_Easy_6_Phone` | `easy_phone_2` | Santral Vardiyası | 2 telefona cevap ver | AnswerPhone | 2 | — kapalı | **22** | **0.5** | **12** | **0.2** |

---

## 6. 🟡 MEDIUM — 10 görev (`tier = Medium`)

| Dosya adı | questId | questTitle | questDescription (elle yaz) | questType | targetCount | Renk filtresi | Para öd. | Prestij öd. | Para ceza | Prestij ceza |
|---|---|---|---|---|---|---|---|---|---|---|
| `Q_Medium_1_Truck` | `med_truck_2` | Çifte Sevkiyat | 2 tır tamamla | CompleteTruck | 2 | — kapalı | **52** | **1.2** | **29** | **0.6** |
| `Q_Medium_2_Shelf` | `med_shelf_7` | Depo Seferberliği | Rafa 7 kutu yerleştir | PlaceBoxOnShelf | 7 | — kapalı | **34** | **0.8** | **19** | **0.4** |
| `Q_Medium_3_ShelfBlue` | `med_shelf_blue` | Mavi Koridor | Rafa 3 mavi kutu yerleştir | PlaceBoxOnShelf | 3 | ✔ **Blue** | **34** | **0.8** | **19** | **0.4** |
| `Q_Medium_7_ShelfYellow` | `med_shelf_yellow` | Sarı Koridor | Rafa 3 sarı kutu yerleştir | PlaceBoxOnShelf | 3 | ✔ **Yellow** | **34** | **0.8** | **19** | **0.4** |
| `Q_Medium_8_ShelfRed` | `med_shelf_red` | Kırmızı Koridor | Rafa 3 kırmızı kutu yerleştir | PlaceBoxOnShelf | 3 | ✔ **Red** | **34** | **0.8** | **19** | **0.4** |
| `Q_Medium_4_Pack` | `med_pack_7` | Bant Hızı | 7 paket hazırla | PackToy | 7 | — kapalı | **34** | **0.8** | **19** | **0.4** |
| `Q_Medium_5_PackCloth` | `med_pack_cloth` | Tekstil Siparişi | 3 giysi paketi hazırla | PackToy | 3 | ✔ **Yellow** | **34** | **0.8** | **19** | **0.4** |
| `Q_Medium_9_PackToy` | `med_pack_toy` | Oyuncak Siparişi | 3 oyuncak paketi hazırla | PackToy | 3 | ✔ **Red** | **34** | **0.8** | **19** | **0.4** |
| `Q_Medium_10_PackGlass` | `med_pack_glass` | Cam Sevkiyat | 3 cam paketi hazırla | PackToy | 3 | ✔ **Blue** | **34** | **0.8** | **19** | **0.4** |
| `Q_Medium_6_Phone` | `med_phone_3` | Müşteri Hattı | 3 telefona cevap ver | AnswerPhone | 3 | — kapalı | **40** | **1.0** | **22** | **0.5** |

---

## 7. 🔴 HARD — 9 görev (`tier = Hard`)

| Dosya adı | questId | questTitle | questDescription (elle yaz) | questType | targetCount | Renk filtresi | Para öd. | Prestij öd. | Para ceza | Prestij ceza |
|---|---|---|---|---|---|---|---|---|---|---|
| `Q_Hard_1_Truck` | `hard_truck_3` | Üçlü Vardiya | 3 tır tamamla | CompleteTruck | 3 | — kapalı | **86** | **2.3** | **47** | **1.2** |
| `Q_Hard_2_Shelf` | `hard_shelf_10` | Tam Kapasite | Rafa 10 kutu yerleştir | PlaceBoxOnShelf | 10 | — kapalı | **57** | **1.5** | **31** | **0.8** |
| `Q_Hard_3_ShelfYellow` | `hard_shelf_yellow` | Sarı Alarm | Rafa 4 sarı kutu yerleştir | PlaceBoxOnShelf | 4 | ✔ **Yellow** | **57** | **1.5** | **31** | **0.8** |
| `Q_Hard_6_ShelfBlue` | `hard_shelf_blue` | Mavi Alarm | Rafa 4 mavi kutu yerleştir | PlaceBoxOnShelf | 4 | ✔ **Blue** | **57** | **1.5** | **31** | **0.8** |
| `Q_Hard_7_ShelfRed` | `hard_shelf_red` | Kırmızı Alarm | Rafa 4 kırmızı kutu yerleştir | PlaceBoxOnShelf | 4 | ✔ **Red** | **57** | **1.5** | **31** | **0.8** |
| `Q_Hard_4_Pack` | `hard_pack_10` | Paket Fırtınası | 10 paket hazırla | PackToy | 10 | — kapalı | **57** | **1.5** | **31** | **0.8** |
| `Q_Hard_5_PackGlass` | `hard_pack_glass` | Kırılacak Eşya | 4 cam paketi hazırla | PackToy | 4 | ✔ **Blue** | **57** | **1.5** | **31** | **0.8** |
| `Q_Hard_8_PackToy` | `hard_pack_toy` | Oyuncak Fabrikası | 4 oyuncak paketi hazırla | PackToy | 4 | ✔ **Red** | **57** | **1.5** | **31** | **0.8** |
| `Q_Hard_9_PackCloth` | `hard_pack_cloth` | Tekstil Sevkiyatı | 4 giysi paketi hazırla | PackToy | 4 | ✔ **Yellow** | **57** | **1.5** | **31** | **0.8** |

> **Hard'da telefon görevi yok** — bilinçli. Telefon geliri oyuncu sayısından bağımsız sabit (~3 çağrı/gün); Hard'a yakışan 4+ hedefte tamamlanma ~%35'e düşüp beklenen değer negatife iniyor → tuzak kart olurdu.

---

## 8. Ödül mantığı — neden bu sayılar

- **base (×1):** renksiz Shelf/Pack + renk-kilitli varyantlar aynı değeri alır. Renk-kilitli görevlerin hedefi zaten düşük tutuldu (renksizin ~%40-50'si); üstüne bir de ödül primi vermek çifte telafi olurdu.
- **premium (×1.5):** `CompleteTruck` (her tier) + Easy'nin "Vardiya Sonu Düzeni" (6 kutu). Tır primi: çok kutu + hangar penceresi zamanlama riski, salt hedef sayısıyla ölçülemiyor.
- **phone (×~1.2):** `AnswerPhone` küçük prim alıyor — emek düşük ama tamamlanma oyuncunun kontrolünde değil (çağrı zarı).
- **Ceza ≈ ödülün %55'i.** Takım geneli günde 1 kabul olduğu için aynı gün ceza asla üst üste binmiyor; en ağır tek ceza 47 TL (Hard/Tır), kırılgan bir günün çekirdek gelirinin (~144 TL) epey altında.
- **Quest hâlâ bonus kalemi, ana gelir değil** — para asıl tır throughput'undan geliyor.
- ⚠️ **Bilinen kaba nokta:** base grupta hedef sayısı farkı ödüle yansımıyor (Hard'da "10 kutu" ile "4 renkli kutu" aynı 57 TL). Bilinçli basitleştirme; playtest'te "az iş = aynı para" hissi gelirse ince ayar yapılır.

---

## 9. Bitirince kontrol listesi

- [ ] 30 asset `Assets/Resources/Quests/` içinde
- [ ] 30 farklı `questId` (kopyala-yapıştırdan artık ID yok)
- [ ] Renk filtreli **18** görevde (her tier'da 6 tane) `requireSpecificBoxType` **işaretli** (işaretsizse renk yok sayılır, görev herhangi kutuyla tamamlanır)
- [ ] `requireSpecificTruckColor` hiçbirinde işaretli değil
- [ ] Bir asset seçip sağ tık → **Print Quest Info** → Console'da ödül/ceza satırı beklediğin gibi mi
- [ ] Quest kartlarındaki 3 slotun Inspector bağlantıları tam (`descriptionText` + `actionButton` en sık unutulan ikisi; `Awake`'teki `ValidateWiring()` eksikleri Console'a yazar)
- [ ] Play-test: Görev Kademesi 0'ken sadece Easy görevler teklif ediliyor mu

---

## 10. Eklemediklerimiz (bilerek)

| Görev | Neden yok |
|---|---|
| Renk-özel tır görevleri (×3, `CompleteSpecificColorTruck`) | Tetikleyici canlı ama tamamlanma en iyi %40, tek oyuncuda %8 → tuzak kart. Tır penceresi cap denge turuna bağlı. |
| Hard telefon görevi | Beklenen değer negatif |
| `CompleteMinigame`, `MakePackagingMistake` tipleri | Kodda var, hiçbir görev kullanmıyor |
