# 🔄 Loading Screen — Sahne Geçişlerine Yayma

**Durum:** PLAN — kullanıcı onayı alındı (mimari), **implementasyon BAŞLAMADI**
**Tarih:** 2026-09-19
**Dal:** `fix/difficulty-scaling-and-dead-code` (loading screen temel işi burada, commit edilmemiş)

---

## Hedef

Loading screen şu an sadece **menü → harita** geçişinde görünüyor. Dört geçişin hepsinde görünsün:

| # | Geçiş | Çağrı yeri | Şu an |
|---|-------|-----------|-------|
| 1 | Menü → Harita | `SteamManager.cs:1623` (NGO `SceneManager.LoadScene`) | ✅ var |
| 2 | Harita → Menü | `GameStateManager.cs:941` (`ExitToMenuCoroutine`) | ❌ yok |
| 3 | Menü → Tutorial | `Menu.cs:771,777` (`ConfirmTutorial`) | ❌ yok |
| 4 | Tutorial → Menü | `ExitHelper.cs:82` | ❌ yok |

---

## İki engel (plan bunlara göre şekillendi)

### Engel 1 — Ekran sahneyle birlikte ölüyor
`LoadScreen`, `MainMenu.unity`'nin Canvas'ının çocuğu ve `DontDestroyOnLoad` değil.
Geçiş #1'in çalışmasının tek sebebi o sırada MainMenu'nün hâlâ yüklü olması.
Geçiş #2 ve #4'te ekranı gösterecek taraf Tutorial / The Main Office sahnesi — orada `LoadScreen` yok.

→ **Çözüm:** Ekran kalıcı bir singleton'a taşınacak (kullanıcı onayı: "Kalıcı singleton").

### Engel 2 — `SceneManager.LoadScene` senkron
Senkron yüklemede Unity kareyi bitirmeden sahneyi değiştirir; araya render girmez.
Ekranı açıp hemen `LoadScene` çağırırsan ekran **hiç görünmez**.

**AMA** denetimde şu çıktı — bu engel sanıldığı kadar büyük değil:
- Geçiş #2 (`ExitToMenuCoroutine`) zaten coroutine, içinde `WaitForSecondsRealtime(0.1f)` + network shutdown var.
- Geçiş #4 (`ExitHelper`) zaten coroutine, içinde ~0.3 + 0.5 + 0.2 sn beklemeler var.
- Geçiş #3'ün `SlideOut(...)` dalı animasyonlu, kare geçiyor. Ama `_tutorialConfirmRt == null` dalı
  doğrudan `LoadScene` çağırıyor — **o dal en az bir kare beklemeli.**

→ **Çözüm:** Ekranı coroutine'in BAŞINDA aç. `LoadSceneAsync`'e çevirmek zorunlu değil ama
tercih edilir (bar gerçek ilerleme gösterir, takılma olmaz). Geçiş #1'e DOKUNULMAYACAK —
orası NGO'nun `NetworkManager.Singleton.SceneManager.LoadScene`'i, server-authoritative,
tüm client'ları senkronlar. Async'e çevirmek netcode davranışını bozar.

---

## Tasarım

### Yeni: `Assets/NewCss/UIScripts/LoadingScreen.cs`
`DontDestroyOnLoad` singleton. Projedeki mevcut desenle aynı (`SfxBus` → `Resources/Audio/SfxLibrary`,
`AudioRouting` → `DontDestroyOnLoad` kök obje).

```
static void Show(string mesaj)      // ekranı aç, taban mesajı ayarla
static void Hide()                  // ekranı kapat
static void SetProgress(float 0-1)  // barı sür
static IEnumerator LoadSceneRoutine(string sahneAdi, string mesaj)
                                    // Show → 1 kare bekle → LoadSceneAsync → bar → Hide
```

- İlk `Show()` çağrısında `Resources.Load<GameObject>("UI/LoadingScreen")` ile prefab'ı instantiate eder,
  `DontDestroyOnLoad` yapar. Sonraki çağrılar aynı örneği kullanır.
- Metin/nokta animasyonu mantığı `SteamManager`'daki **tek render yolu** deseninden taşınacak
  (taban mesaj + nokta sayısı ayrı state, tek yazar). Bkz. `SteamManager.RenderLoadingText`.
- Kendi Canvas'ı olacak, `sortingOrder` yüksek (her şeyin üstünde).

### Yeni: `Assets/Resources/UI/LoadingScreen.prefab`
Mevcut `MainMenu.unity > Canvas > LoadScreen` içeriğinden üretilecek:
karartma `Image` (4000x4000, koyu gri a=0.63) + `LoadingStatusText` (TMP) + `LoadingProgressSlider`.
Spinner **dahil edilmeyecek** — kullanıcı 2026-09-19'da kaldırılmasını istedi.

### Çağıran tarafı değişiklikleri
| Dosya | Değişiklik |
|-------|-----------|
| `GameStateManager.ExitToMenuCoroutine` | Coroutine başında `LoadingScreen.Show("Menüye dönülüyor")`; sondaki `LoadScene` → `LoadSceneRoutine` |
| `ExitHelper` (satır ~60-82) | Aynı kalıp; `Show()` en başta (network temizliği ekranın arkasında olsun) |
| `Menu.ConfirmTutorial` | Her iki dalda (`SlideOut` callback'i + null dalı) `LoadSceneRoutine` kullan — null dalındaki tek-kare sorunu böyle kapanır |
| `SteamManager` | `loadingScreen`/`loadingText`/`loadingProgressBar` serialize alanları KALDIRILMAZ (sahne bağları bozulmasın); bunun yerine `SetLoadingScreenActive`/`UpdateLoadingProgress` gövdeleri `LoadingScreen` singleton'ına delege edilir. Geçiş #1 akışı aynen kalır. |

### Sahne temizliği
`MainMenu.unity > Canvas > LoadScreen` artık gereksiz — prefab devraldıktan sonra kaldırılabilir.
**Ama aynı turda yapılmayacak:** önce singleton çalıştığı doğrulansın, sahne temizliği ayrı adım.
(Bu projede sahne düzenlemesi en kırılgan taraf — `successCallSound` vakası.)

---

## Adımlar

1. `LoadingScreen.cs` yaz (singleton + render yolu + `LoadSceneRoutine`).
2. `Resources/UI/LoadingScreen.prefab` üret (Unity MCP ile, mevcut `LoadScreen` içeriğinden).
3. `SteamManager`'ı singleton'a delege et — geçiş #1 hâlâ çalışmalı (regresyon testi).
4. Geçiş #2 (`GameStateManager`) bağla.
5. Geçiş #4 (`ExitHelper`) bağla.
6. Geçiş #3 (`Menu.ConfirmTutorial`) bağla — iki dalı da.
7. `kontrol` kapısı.
8. (Ayrı tur) `MainMenu.unity`'den eski `LoadScreen`'i kaldır.

---

## Riskler / açık sorular

- **Tutorial ve The Main Office sahnelerinde Canvas çakışması:** singleton kendi Canvas'ını
  getiriyor; o sahnelerdeki mevcut Canvas'larla `sortingOrder` yarışı olabilir. Doğrulanmalı.
- **`ExitHelper` `DontDestroyOnLoad`:** kendisi zaten kalıcı (satır 22, 36). Singleton'la çifte
  yaşam döngüsü sorunu çıkmamalı ama kontrol edilmeli.
- **Geçiş #1 regresyonu:** `SteamManager`'ın loading akışı bu oturumda yeni düzeltildi
  (tek render yolu, `StopLoadingDots`). Delegasyon sırasında bozulmamalı.
- **Play Mode doğrulaması yapılamıyor:** projede `execute_code`'u engelleyen bir derleyici
  çakışması var (`AsyncMethodBuilderAttribute` çift tanım). Her adım kullanıcı tarafından
  Unity'de elle test edilmeli.

---

## Kapsam DIŞI (bu turda yapılmayacak)

- `Menu.cs:753` `PlayOffline()` → `SceneManager.LoadScene("MapSelection")`. **"MapSelection" sahnesi
  ne projede ne build ayarlarında var** — o butona basılırsa oyun çöker. Ayrı bug, ayrı iş.
- Yükleme mesajlarının dil karışıklığı ("Preparing" / "Loading Scene" / "Ready!" İngilizce,
  "Lobi oluşturuluyor…" / "Hazır!" Türkçe) ve bozuk boşluklar ("Connecting.. .").
  Hiçbiri lokalizasyon sistemine bağlı değil, düz string. Ayrı iş.
