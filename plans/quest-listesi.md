# 📋 Görev (Quest) Listesi — Oyun İçinde Alınabilen Tüm Görevler

> Kaynak: `Assets/Resources/Quests/*.asset` (canlı havuz) + `Assets/Scripts/Quest/`
> Durum tarihi: **2026-07-26** — dosyadan okunarak üretildi, elle yazılmadı.
> **Toplam 30 görev** (Easy 11 · Medium 10 · Hard 9) + silinmeyi bekleyen 5 eski asset.

---

## Sistem nasıl çalışıyor (listeyi okumadan önce)

| Kural | Değer | Kod |
|---|---|---|
| Günde teklif edilen görev | **3 tane** (havuzdan rastgele) | `QuestManager.DAILY_QUEST_COUNT = 3` |
| Günde kabul edilebilen | **1 tane, takım geneli** | `_hasAcceptedToday` (NetworkVariable) |
| Hangi tier'lar havuza girer | `quest.tier <= Görev Kademesi seviyesi` | `GetAvailableQuestsForTier()` |
| Ödül | Ödül havuzundan **tekrarsız rastgele 2 giriş** | `MAX_SELECTED_REWARDS = 2` |
| Ceza | Ceza havuzundan **tekrarsız rastgele 2 giriş** | `MAX_SELECTED_PENALTIES = 2` |
| Seçim senkronu | Sunucu `rewardSeed` üretir, client aynı seed'le birebir aynı seçimi görür | `QuestProgress(.., rewardSeed)` |
| **İkon** | Her görevin `QuestData.icon` slotu var; boşsa prefab'ın varsayılan sprite'ı kalır | `QuestSlotUI.UpdateIcon()` |

**Tier kapısı — "Görev Kademesi" upgrade'i:**

| Görev Kademesi seviyesi | Açılan tier'lar | Havuz boyutu |
|---|---|---|
| 0 (satın alınmadı) | Easy | 11 |
| 1 | Easy + Medium | 21 |
| 2 | Easy + Medium + Hard | 30 |

⚠️ **Ödül sütunlarındaki değerler HAVUZDUR, hepsi birden verilmez.** Her görev atandığında havuzdan 2 farklı giriş çekilir.

---

## 🟢 EASY (tier 0) — 11 görev

**Ortak ödül havuzu:** `10 para` · `15 para` · `25 para` · `0.4 prestij` · `0.8 prestij`
**Ortak ceza havuzu:** `-5 para` · `-10 para` · `-15 para` · `-0.2 prestij` · `-0.4 prestij`

| Dosya | İsim | Açıklama | Tip | Hedef | Renk/Kategori |
|---|---|---|---|---|---|
| `Q_Easy_1_Truck` | **Tek Sefer** | 1 tır tamamla | CompleteTruck | 1 | — |
| `Q_Easy_2_Shelf` | **Raf Düzeni** | Rafa 4 kutu yerleştir | PlaceBoxOnShelf | 4 | — |
| `Q_Easy_11_ShelfBig` 🆕 | **Vardiya Sonu Düzeni** | Rafa 6 kutu yerleştir | PlaceBoxOnShelf | 6 | — |
| `Q_Easy_3_ShelfRed` | **Kırmızı Reyon** | Rafa 2 kırmızı kutu yerleştir | PlaceBoxOnShelf | 2 | Kırmızı |
| `Q_Easy_7_ShelfYellow` 🆕 | **Sarı Reyon** | Rafa 2 sarı kutu yerleştir | PlaceBoxOnShelf | 2 | Sarı |
| `Q_Easy_8_ShelfBlue` 🆕 | **Mavi Reyon** | Rafa 2 mavi kutu yerleştir | PlaceBoxOnShelf | 2 | Mavi |
| `Q_Easy_4_Pack` | **Paket Mesaisi** | 4 paket hazırla | PackToy | 4 | — |
| `Q_Easy_5_PackToy` | **Oyuncakçı** | 2 oyuncak paketi hazırla | PackToy | 2 | Oyuncak (Kırmızı) |
| `Q_Easy_9_PackCloth` 🆕 | **Askılık** | 2 giysi paketi hazırla | PackToy | 2 | Giysi (Sarı) |
| `Q_Easy_10_PackGlass` 🆕 | **Dikkatli Eller** | 2 cam paketi hazırla | PackToy | 2 | Cam (Mavi) |
| `Q_Easy_6_Phone` | **Santral Vardiyası** | 2 telefona cevap ver | AnswerPhone | 2 | — |

---

## 🟡 MEDIUM (tier 1) — 10 görev

**Ortak ödül havuzu:** `20 para` · `30 para` · `40 para` · `0.8 prestij` · `1.4 prestij`
**Ortak ceza havuzu:** `-14 para` · `-18 para` · `-22 para` · `-0.4 prestij` · `-0.8 prestij`

| Dosya | İsim | Açıklama | Tip | Hedef | Renk/Kategori |
|---|---|---|---|---|---|
| `Q_Medium_1_Truck` | **Çifte Sevkiyat** | 2 tır tamamla | CompleteTruck | 2 | — |
| `Q_Medium_2_Shelf` | **Depo Seferberliği** | Rafa 7 kutu yerleştir | PlaceBoxOnShelf | 7 | — |
| `Q_Medium_3_ShelfBlue` | **Mavi Koridor** | Rafa 3 mavi kutu yerleştir | PlaceBoxOnShelf | 3 | Mavi |
| `Q_Medium_7_ShelfYellow` 🆕 | **Sarı Koridor** | Rafa 3 sarı kutu yerleştir | PlaceBoxOnShelf | 3 | Sarı |
| `Q_Medium_8_ShelfRed` 🆕 | **Kırmızı Koridor** | Rafa 3 kırmızı kutu yerleştir | PlaceBoxOnShelf | 3 | Kırmızı |
| `Q_Medium_4_Pack` | **Bant Hızı** | 7 paket hazırla | PackToy | 7 | — |
| `Q_Medium_5_PackCloth` | **Tekstil Siparişi** | 3 giysi paketi hazırla | PackToy | 3 | Giysi (Sarı) |
| `Q_Medium_9_PackToy` 🆕 | **Oyuncak Siparişi** | 3 oyuncak paketi hazırla | PackToy | 3 | Oyuncak (Kırmızı) |
| `Q_Medium_10_PackGlass` 🆕 | **Cam Sevkiyat** | 3 cam paketi hazırla | PackToy | 3 | Cam (Mavi) |
| `Q_Medium_6_Phone` | **Müşteri Hattı** | 3 telefona cevap ver | AnswerPhone | 3 | — |

---

## 🔴 HARD (tier 2) — 9 görev

**Ortak ödül havuzu:** `35 para` · `50 para` · `65 para` · `1.6 prestij` · `2.4 prestij`
**Ortak ceza havuzu:** `-25 para` · `-30 para` · `-35 para` · `-0.8 prestij` · `-1.2 prestij`

| Dosya | İsim | Açıklama | Tip | Hedef | Renk/Kategori |
|---|---|---|---|---|---|
| `Q_Hard_1_Truck` | **Üçlü Vardiya** | 3 tır tamamla | CompleteTruck | 3 | — |
| `Q_Hard_2_Shelf` | **Tam Kapasite** | Rafa 10 kutu yerleştir | PlaceBoxOnShelf | 10 | — |
| `Q_Hard_3_ShelfYellow` | **Sarı Alarm** | Rafa 4 sarı kutu yerleştir | PlaceBoxOnShelf | 4 | Sarı |
| `Q_Hard_6_ShelfBlue` 🆕 | **Mavi Alarm** | Rafa 4 mavi kutu yerleştir | PlaceBoxOnShelf | 4 | Mavi |
| `Q_Hard_7_ShelfRed` 🆕 | **Kırmızı Alarm** | Rafa 4 kırmızı kutu yerleştir | PlaceBoxOnShelf | 4 | Kırmızı |
| `Q_Hard_4_Pack` | **Paket Fırtınası** | 10 paket hazırla | PackToy | 10 | — |
| `Q_Hard_5_PackGlass` | **Kırılacak Eşya** | 4 cam paketi hazırla | PackToy | 4 | Cam (Mavi) |
| `Q_Hard_8_PackToy` 🆕 | **Oyuncak Fabrikası** | 4 oyuncak paketi hazırla | PackToy | 4 | Oyuncak (Kırmızı) |
| `Q_Hard_9_PackCloth` 🆕 | **Tekstil Sevkiyatı** | 4 giysi paketi hazırla | PackToy | 4 | Giysi (Sarı) |

> **Hard'da telefon görevi neden yok:** telefon geliri oyuncu sayısından bağımsız sabit (10 zar/gün × %30 ≈ 3 çağrı). Hard'a yakışan hedef (4+) için tamamlanma ~%35'e düşüyor ve beklenen değer **negatife** (-2.4) iniyor — ölü/tuzak kart olurdu. Bu yüzden Hard 9 görevde bırakıldı, sayıyı doldurmak için tuzak kart basılmadı.

---

## ⚠️ ESKİ GÖREVLER — hâlâ havuzda (silinmesi planlanmıştı)

Bu 5 asset `Assets/Resources/Quests/` içinde **duruyor** ve `CollectQuestAssets()` klasörün tamamını yüklediği için **şu an oyunda çıkabiliyorlar.** Yani gerçek havuz 30 değil **35**, Easy tarafı 16 görev.

| Dosya | İsim | Açıklama | Tip | Hedef | Ödül havuzu | Ceza havuzu |
|---|---|---|---|---|---|---|
| `easy1` | Seçici Paket Canavarı | 5 kırmızı kutu paketle | PackToy | 5 (Oyuncak) | 10 / 20 / 15 para · 0.4 / 0.8 prestij | -10 / -15 / -5 para · -0.8 / -0.4 prestij |
| `easy2` | Yetiştirici | 2 Tır tamamla | CompleteTruck | 2 | 20 / 16 / 8 para · 0.4 / 0.8 prestij | -6 / -12 / -18 para · -0.4 / -0.8 prestij |
| `easy3` | Depo Takipçisi | Rafa 5 kutu koy | PlaceBoxOnShelf | 5 | 15 / 25 / 35 para · 0.4 / 0.8 prestij | -15 / -20 / -10 para · -0.4 / -0.8 prestij |
| `easy4` | Mavi Raf Düzeni | Rafa 4 adet Mavi kutu koy | PlaceBoxOnShelf | 4 (Mavi) | 10 / 18 / 26 para · 0.4 / 0.6 prestij | -8 / -14 / -20 para · -0.4 / -0.2 prestij |
| `easy5` | Sarı Kutu Ustası | 3 adet Sarı oyuncak paketle | PackToy | 3 (Sarı=**Giysi**) | 12 / 20 / 28 para · 0.4 / 0.8 prestij | -10 / -15 / -20 para · -0.4 / -0.6 prestij |

**Neden silinmeleri planlandı:**
- `easy2` = "2 tır" ama **Easy** tier'da → `Çifte Sevkiyat` (Medium) ile aynı işi çok daha ucuza ödüllendiriyor, tier merdivenini bozuyor.
- `easy3` (5 kutu) ile `Raf Düzeni` (4 kutu), `easy4` ile artık `Mavi Reyon`/`Mavi Koridor` çakışıyor.
- `easy5`'in metni "**Sarı oyuncak**" diyor ama kodda Sarı = **Giysi** kategorisi (Oyuncak = Kırmızı) → oyuncuya yanlış talimat veriyor.
- `easy3` cezası (-20 para'ya kadar) yeni Easy tavanından (-15) sert.

> Artık silinmeleri **güvenli**: Easy havuzu 6'dan 11'e çıktığı için bunları kaldırmak erken oyun çeşitliliğini düşürmüyor.

---

## 📊 Özet

Havuzdan **tekrarsız 2 farklı** giriş çekiliyor (Fisher-Yates shuffle → ilk 2), yani "iki kere 25 para" mümkün değil. İki para da çekilebilir, iki prestij de, karışık da:

| Tier | Görev sayısı | Ödül: para | Ödül: prestij | Ceza: para | Ceza: prestij |
|---|---|---|---|---|---|
| Easy | 11 (+5 eski) | 0 – 40 | 0 – 1.2 | 0 – -25 | 0 – -0.6 |
| Medium | 10 | 0 – 70 | 0 – 2.2 | 0 – -40 | 0 – -1.2 |
| Hard | 9 | 0 – 115 | 0 – 4.0 | 0 – -65 | 0 – -2.0 |

("0 para" = o atamada havuzdan iki prestij çekilmiş demek; ikisi birden sıfır olamaz.)

**Görev tipi dağılımı:**

| Tip | Easy | Medium | Hard | Toplam |
|---|---|---|---|---|
| CompleteTruck (tır tamamla) | 1 | 1 | 1 | 3 |
| PlaceBoxOnShelf (rafa koy) | 5 | 4 | 4 | 13 |
| PackToy (paketle) | 4 | 4 | 4 | 12 |
| AnswerPhone (telefon) | 1 | 1 | 0 | 2 |
| **Toplam** | **11** | **10** | **9** | **30** |

**Tasarım kuralı:** her tier, canlı 4 fiilin (tır / raf / paket / telefon) tüm geçerli renk-kategori kombinasyonlarını kapsıyor. Raf ve paket 4'er varyant veriyor (renksiz + 3 renk), tır renk kilitlenemediği için 1, telefon 1.

---

## Yazılmayan / planda bekleyen görevler

| Görev | Tip | Neden basılmadı |
|---|---|---|
| Renk-özel tır görevleri (×3) | CompleteSpecificColorTruck | Tetikleyici canlı ama tamamlanma oranı en iyi %40, tek oyuncuda %8 → tuzak kart. **Tır penceresi cap işine bağlandı**, o denge turu bitince tek seferde basılacak. |
| Hard telefon görevi | AnswerPhone | Beklenen değer negatif (-2.4) → ölü kart. |

**Kullanılmayan görev tipleri (kodda var, hiçbir görev kullanmıyor):** `CompleteMinigame`, `MakePackagingMistake`.

---

## Renk ↔ kategori eşlemesi (referans)

| Kutu rengi | Enum | Paketleme kategorisi |
|---|---|---|
| Sarı | `BoxType.Yellow` (0) | Giysi (Clothing) |
| Mavi | `BoxType.Blue` (1) | Cam (Glass) |
| Kırmızı | `BoxType.Red` (2) | Oyuncak (Toy) |

Kaynak: `BoxInfo.cs:7-12`, `Table.cs:832`
