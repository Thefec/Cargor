# 🗺️ Cargor — Yol Haritası & Sprint Planı (referans)

> **Oluşturulma**: 6 Temmuz 2026 · Kaynak: `Assets/NewCss` + `Assets/Scripts` kod taraması ↔ `GDD.md`
> **Not**: Bu orijinal yol haritası. Aktif iş şu an ekonomi + roguelite üzerinde — bkz. [../PLAN.md](../PLAN.md).
> Sprint 0-3 planı büyük ölçüde referans; ekonomi/roguelite bunun üstüne öncelik aldı.

---

## 1. 🔍 Mevcut Durum Değerlendirmesi (Kod Taraması)

Genel tablo beklenenden **sağlıklı**. GDD'de "eksik" görünen birçok sistem aslında yazılmış; sorun daha çok **dosya yolu tutarsızlığı, commit'lenmemiş yeniden yapılanma ve doğrulanmamış tamamlık**.

### Var ve çalışır görünen sistemler (kodu mevcut)
- Gün döngüsü (`UIScripts/DayCycleManager.cs`), aydınlatma (`DayLightController.cs`)
- Ekonomi ayarları (`GameEconomySettings.cs` + `Resources/EkonomiAyarlari.asset`)
- Para (`UIScripts/MoneySystem.cs`), prestij (`CustomerSripts/PrestigeManager.cs`), kota (`QuotaManager.cs`)
- Tır sistemi (`TruckScripts/Truck.cs`, `TruckSpawner.cs`, `GarageDoorController.cs`)
- Müşteri + kapasite-bazlı spawn (`CustomerManager.cs`: `CountActiveInteractables`, `_storeLevel`, `_shelfMultiplier`, `_levelMultiplier`)
- Pickup/envanter v2 (6 parçalı `NewPickup/PlayerInventory.*`), raf/masa (`TableScripts/`)
- Upgrade (`UpgradeScripts/`), telefon (`Phone/`), event (`Events/`)
- Quest sistemi — `Assets/Scripts/Quest/` (Data, Manager, UI, Buff) + `BuffManager`
- Zorluk (`GameState/DifficultyManager.cs`), oyun durumu (`GameState/GameStateManager.cs`)
- Steam (`Steam/`), Discord (`Assets/Scripts/Discord/`), lokalizasyon (`Localization/`)
- Tutorial (`Assets/Tutorialassets/TutorialManager.cs` + `NewCss/Tutorial/` spawner'ları)

### Tespit edilen boşluklar / riskler
| # | Bulgu | Şiddet | Not |
|---|-------|--------|-----|
| R1 | Commit'lenmemiş büyük yeniden yapılanma (dosyalar NewCss kökünden alt klasörlere) | 🔴 Yüksek | ✅ Çözüldü — baseline commit'lendi |
| R2 | GDD ↔ kod yol tutarsızlığı (MoneySystem, DayCycleManager, Quest, TutorialManager yolları) | 🟡 Orta | GDD güncellenmeli |
| R3 | Doğrulanmamış tamamlık — kod var ama runtime davranışları test edilmedi | 🟡 Orta | Unity içi test + qa |
| R4 | Ekonomi değerleri doğrulanmadı | 🟡 Orta | ✅ economist doğruladı (Faz 1) |
| R5 | Otomatik test yok | 🟢 Düşük-Orta | EditMode testleri başladı (roguelite) |
| R6 | Kod organizasyonu ikiliği (NewCss vs Scripts; PickUpScripts v1 + NewPickup v2) | 🟢 Düşük | Teknik borç, ertelendi |

---

## 2. 🎯 Öncelik Sırası
1. **Stabilizasyon** (R1) — commit'lenmemiş taşımalar üstüne iş riskli.
2. **Doğrulama & denetim** (R3, R4) — "var" dediklerimizin çalıştığını kanıtla; ekonomiyi kilitle.
3. **Belge senkronu** (R2) — GDD'yi gerçeğe hizala.
4. **Cilalama & borç** (R5, R6) — test kapsamı ve organizasyon.

---

## 3. 🏃 Sprint Planı

### Sprint 0 — Stabilizasyon → devops
- Working tree incele (meta/GUID bütünlüğü), Unity derleme doğrula, anlamlı commit, `.gitignore` netleştir.
- **Çıktı**: Temiz, derlenen, commit'lenmiş baz.

### Sprint 1 — Doğrulama & Denetim (paralel) → qa + economist
- qa: Çekirdek döngü denetimi (`OnNewDay` aboneleri GDD 3.4; Kota→GameOver, prestij→GameOver, kira→grace zincirleri GDD 21.2).
- economist: `EkonomiAyarlari.asset` + `GameEconomySettings.cs` ↔ GDD 4/5/6/31. Kapasite spawn (GDD 9.6) + simülasyon (GDD 31.3).
- qa: PickUpScripts (v1) vs NewPickup (v2) — hangisi canlı, ölü kod var mı?

### Sprint 2 — Belge Senkronu → assistant
- GDD dosya yollarını gerçekle güncelle (R2), Sprint 1 bulgularını "Bilinen Riskler"e işle.

### Sprint 3 — Cilalama & Teknik Borç
- gameplay: kopuk event zincirleri; economist: sapma düzeltmeleri; graphics-ui: UI/HUD eksikleri (GDD 26.2); devops: organizasyon kararı + EditMode testleri.

---

## 4. 🏢 Departman Dağılım Tablosu
| İş | Departman | Sprint |
|----|-----------|--------|
| Stabilizasyon, commit, derleme, git, organizasyon | **devops** | 0, 3 |
| Kod denetimi, event zinciri & GameOver doğrulama, ölü kod | **qa** | 1 |
| Ekonomi değeri doğrulama, denge, simülasyon | **economist** | 1, 3 |
| GDD güncelleme, rapor derleme, PLAN senkron | **assistant** | 2 |
| Kopuk mekanik tamamlama, gameplay düzeltmeleri | **gameplay** | 3 |
| UI/HUD eksikleri, görsel cila | **graphics-ui** | 3 |

**Kural**: Ekonomik değer gereken her işte önce economist. Önemli kod değişikliğinden sonra qa. Her çıktı kontrol'den geçer.
