# Gün HUD'u: zaman pastası + güneş/ay ikonu + kira satırı

## Context
Playtest'te oyuncular kiraya ayrılmış parayla kart alıp iflas tuzağına düştü (economist sıfırdan raporu, `docs/economy/ekonomi-sifirdan-2026-09-23.md`). Ekranda ne günün ne kadarının kaldığı ne de kiranın yaklaştığı görünüyor. Sol üstteki "Gün N" yazısı (`ClockImg/ClockText`) genişletilecek:
1. Yazının **sağına** güneş/ay ikonu + **dolan** pizza-dilimi (radyal) bar → günün hangi anında olunduğu.
2. Yazının **altına** kira satırı: `1/4   500/785` → kira döngüsünde kaçıncı gün + kasa/gelecek kira.

Kullanıcı kararları: bar **dolan** (geçen süre) · ikon gün boyu **güneş, son %25'te ay** (bar rengi de uyarıya kayar) · ikonlar **AI ile üretilecek** (Unity MCP `generate_image`) · alt satır **Kasa / Kira** (kasa<kira kırmızı, ≥ yeşil, kira günü "Bugün kira!" vurgusu).

## Keşif bulguları (tasarımı belirleyen)
- `DayCycleManager.cs` (`Assets/NewCss/UIScripts/`): `_networkElapsedTime` + `_networkCurrentDay` zaten NetworkVariable → pasta client'ta doğru hesaplanır (`elapsedTime / CurrentDayDuration`). `CurrentDayDuration` gün numarasından deterministik.
- **Kira client'ta hesaplanamaz:** `CalculateRent()` (:661) → `_rentPaymentCount` (:105) düz server alanı, replike değil; `GetPlayerCount()` (:703) client'ta `ConnectedClientsList`'e düşer (client'ta geçersiz); perk'ler `economySettings.rentScaledMultiplier`'ı server'da değiştirir. → **Server-yazar `NetworkVariable<int> _networkNextRent`** gerekir. Ayrıca `CalculateRent()` her çağrıda `Debug.Log` basıyor → sessiz iç varyant gerekir.
- Kasa: `MoneySystem.CurrentMoney` NetworkVariable + `OnMoneyChanged` event'i (`Assets/NewCss/UIScripts/MoneySystem.cs:22,25`).
- Kira günü kuralı: `currentDay % rentIntervalDays == 0` (:601), interval `economySettings.rentIntervalDays` (4) → gün 4/8/12/16.
- **Mevcut bug (bu işte düzeltilecek):** `FormatTimeDisplay` (:936) sabit İngilizce `"Day {N}"` yazıyor; `ClockText` üstündeki `LocalizeStringEvent` (sahne `The Main Office.unity` ~:181779, anahtar `Day1`, TR değeri bozuk "dAY 1") dil değişiminde/OnEnable'da üstüne yazıyor — Lobi ID hatasıyla aynı sınıf. `DayFormat` anahtarı eski debug formatı (`Gün {0} {1}:{2} ({3}s)`), kullanılmıyor.
- Yeniden kullanılacak: `LocalizationHelper.GetLocalizedString / GetLocalizedStringFormat` (`Assets/NewCss/Localization/LocalizationHelper.cs:79,113`), 17 dil tablo ekleme scripti deseni (bu oturumda Err* anahtarlarında kullanıldı: Shared Data'ya `m_Entries` sonuna, her `StringTable_xx.asset`'e `references:` öncesine; id'ler `144849164114263160+`).
- Tutorial sahnesinde `dayTimeText` yok → kapsam yalnız `The Main Office`.

## Uygulama

### A. Server verisi — `DayCycleManager.cs` (gameplay)
- `NetworkVariable<int> _networkNextRent` (read Everyone, write Server) + `public int NextRentAmount => _networkNextRent.Value`.
- `CalculateRent()`'i `CalculateRent(bool log)`'a böl; mevcut çağıranlar log'lu kalır, önizleme sessiz.
- Server'da `Update` içindeki mevcut UI throttle bloğunda (`UI_UPDATE_INTERVAL`): `int r = CalculateRent(false); if (r != _networkNextRent.Value) _networkNextRent.Value = r;` → gün değişimi, kira ödemesi (`_rentPaymentCount++`), perk alımı, roster değişimi hepsini tek yerden yakalar; yalnız değişince ağa yazar.
- Salt-okunur yardımcılar: `public int RentIntervalDays`, `public int DayInRentCycle => ((currentDay-1) % RentIntervalDays) + 1`, `public bool IsRentDay`, `public float DayProgress01`.
- `FormatTimeDisplay` → `LocalizationHelper.GetLocalizedStringFormat("DayLabel", day)`; dil değişiminde `_lastDisplayedDay = -1` ile zorla yenile (`LocalizationSettings.SelectedLocaleChanged`).
- Ekonomi değeri YOK (yalnız mevcut kirayı gösteriyor) → economist gerekmez.

### B. HUD bileşeni — yeni `Assets/NewCss/UIScripts/DayHudUI.cs` (graphics-ui)
- Alanlar: `Image timePie` (Filled, Radial360, origin Top, clockwise), `Image timeIcon`, `Sprite sunSprite, moonSprite`, `TextMeshProUGUI rentText`, `Color dayColor/eveningColor`, `float eveningThreshold = 0.75f`, `Color okColor/shortColor`.
- Her frame (ucuz): `timePie.fillAmount = DayProgress01`; `progress >= 0.75` → ay sprite'ı + bar rengi `eveningColor`'a lerp.
- Kira satırı yalnız değişince yeniden yazılır (gün / kasa / NextRent / dil değişimi cache'i): `"{DayInRentCycle}/{interval}   {money}/{rent}"`, kasa<kira → `shortColor`, değilse `okColor`; `IsRentDay` → `RentDueToday` metni (`"Bugün kira!  500/785"`). Gün 16 son kira sonrası gizle / gösterme mantığı: son kira günü de gösterilir, sonrası oyun biter.
- `OnMoneyChanged`'e abone/abonelikten çık (OnEnable/OnDisable), DayCycleManager null-guard.

### C. Sahne + görseller — `The Main Office.unity` (graphics-ui, Unity MCP ile; Unity açık)
- `ClockText` üstündeki `LocalizeStringEvent` component'i **kaldır** (kod yazıyor artık).
- `ClockImg` altına: `TimePie` (Image Filled Radial360 + arka plan halka), içinde `TimeIcon`; `ClockText` altına `RentText` (SpaceGrotesk-Bold TR SDF, `9b06d1fb...`). Gerekirse `ClockImg` boyutu büyütülür; layout 1920x1080 referansla.
- İkonlar: MCP `generate_image` ile düz renkli, oyunun UI stiline uygun `Sun.png` + `Moon.png` (şeffaf arka plan, ~256px) → `Assets/UI/DayHud/`; Sprite (2D and UI) import. Pasta için Unity yerleşik `UISprite`/daire sprite'ı ya da basit üretilmiş daire.
- Dinamik font atlası (Montserrat vb.) değişiklikleri commit dışı.

### D. Lokalizasyon — 17 dil (müdür, mevcut script deseni)
- Yeni anahtarlar: `DayLabel` = "Gün {0}" / "Day {0}" …, `RentDueToday` = "Bugün kira!" / "Rent due today!" …
- Tüm glifler U+0000–017F içinde (SpaceGrotesk TR kapsamı) — script assert'ü ile doğrula.

## Delegasyon & kapı (BÜYÜK iş: DayCycleManager kritik sistem + birden çok dosya)
1. gameplay → A · graphics-ui → B+C (paralel; B, A'nın public API isimlerine göre yazılır) · müdür → D.
2. qa → A+B incelemesi (özellikle: NetworkVariable yazımının yalnız server'da olması, late-join'de ilk değer, abonelik sızıntısı, per-frame alloc yok).
3. kontrol → dal-sonu tek ONAY kapısı. Dal: `feature/day-hud`.

## Doğrulama
- Unity MCP: derleme 0 hata (`refresh_unity` + `read_console`), `validate_script`.
- Editör Play (host): Gün 1'de pasta boştan dolmaya başlar; %75'te ay + renk; gün sonunda tam. Alt satır `1/4 720/1140` (3P) — kart alınca kasa anında güncellenir, kasa<kira kırmızı. Gün 4'te "Bugün kira!", kira ödendikten sonra gün 5'te `1/4` ve yeni (x1.20) kira.
- Dil değiştir (TR↔EN): "Gün 1"/"Day 1" doğru, "dAY 1" hiç görünmez.
- 2-PC (kullanıcı): client'ta kira tutarı host ile aynı (asıl risk buydu), geç katılan client'ta ilk karede doğru değer.
