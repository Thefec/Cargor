# Oyuncu sayısı kilidi + düşen oyuncu para sıfırlama + client hangar yazısı (2026-09-24)

## Context
Kullanıcı yerel Multiplayer Play Mode testi (Tools ▸ Cargor ▸ Local Coop Test) ile 3 bulgu verdi:
1. **Client'ta hangar kapısı üstündeki "x/y" kutu sayısı yazmıyor.** Kök neden (kodla doğrulandı): `Truck.garageDoor` düz public alan, yalnız server `TruckSpawner.cs:494`'te atıyor → client'ta null → `Truck.UpdateUIText` (:894) `garageDoor?.SetRequestedCargoText` hiç çalışmıyor. Hafıza sınıfı: [[unity-netcode-plain-field-not-replicated]].
2. **2. oyuncu çıkınca kasa 660 → 500'e düştü (KANITLI, kullanıcı testi).** `DifficultyManager.HandlePlayerConnectionChanged` → `ApplyDifficultySettings` → `ApplyMoneySettings` (:505) `SetMoney(ScaledStartingMoney)`. Koruma `GameStateManager.HasGameEverStarted` hiç true olmuyor: tek yazan `SteamManager.cs:627` (`LoadEventCompleted`), ama SteamManager DontDestroyOnLoad DEĞİL → harita yüklenince yok oluyor, event'i hiç almıyor.
3. **Çık-gir istismarı:** roster düşen oyuncuyu siliyor (`GameStateManager.cs:208-221`), `LateJoinGuard` eski üyenin geri girmesine izin veriyor. Kira (`DayCycleManager.GetPlayerCount` → roster), upgrade fiyatı (`DifficultyManager.UpgradeCostMultiplier`), müşteri/tır/telefon/perk ölçekleri (`DifficultyManager.PlayerCount` — CustomerManager, TruckSpawner, Truck, PhoneCallManager, PerkEffect) hepsi anlık sayıya bakıyor → kira günü çık, ucuz öde, geri gir.

**Kullanıcı kararı:** Oyuncu sayısı **koşu başında kilitlenir**, 16 gün boyunca DÜŞMEZ (gerçekten ayrılan oyuncunun payını kalanlar ödemeye devam eder).

## Uygulama (gameplay)
### A. Koşu-kilitli oyuncu sayısı — `Assets/NewCss/GameState/DifficultyManager.cs`
- Koşu içinde `_networkPlayerCount` **asla azalmaz**: `HandlePlayerConnectionChanged` ve `InitializePlayerCount` yeni değeri `max(mevcut, yeni)` olarak yazar (disconnect artık sayıyı düşürmez; spawn anında henüz bağlanmamış/yüklenmemiş bir client sonradan bağlanırsa sayı yükselebilir — "kilitle" kararının pratik eşdeğeri, geç yüklenen client'a karşı sağlam). Kilit koşu başına: DifficultyManager harita sahnesiyle birlikte doğup öldüğü için yeni oturumda sıfırdan başlar — bunu doğrula (menüdeki ikinci DifficultyManager kopyası `MainMenu.unity`'de de var; harita kopyasının Instance olduğunu ve menü kopyasının sayısını taşımadığını kontrol et).
- `PlayerCount` public API aynı kalır → tüm tüketiciler (CustomerManager, TruckSpawner, Truck, PhoneCallManager, PerkEffect, UpgradePanel) otomatik kilitli sayıyı görür.

### B. Kira aynı kilitli sayıyı kullanır — `Assets/NewCss/UIScripts/DayCycleManager.cs` `GetPlayerCount()` (~:703)
- Önce `DifficultyManager.Instance.PlayerCount`, yoksa mevcut roster/ConnectedClients fallback'i. (Break Room / Win-Lose isim listesi roster'ı kullanmaya devam eder — dokunma.)
- Not: bu dosyada `feature/day-hud` dalının commit'lenmemiş HUD değişiklikleri var (`_networkNextRent`, `RefreshNextRentPreview` vb.) — onlara dokunma, yalnız `GetPlayerCount`.

### C. Para yalnız "dokunulmamışken" başlangıca çekilir — `DifficultyManager.ApplyMoneySettings`
- Ölü `HasGameEverStarted` korumasına güvenme. Yeni kural: `oldStart = moneySystem.startingMoney` (atamadan ÖNCE yakala); `SetMoney(ScaledStartingMoney)` yalnız `moneySystem.CurrentMoney == oldStart` VE `DayCycleManager.Instance == null || currentDay == 1` ise. Aksi halde yalnız `startingMoney` alanı güncellenir + log. (A ile disconnect zaten tetiklemez; bu, sayı yükselirken kazanılmış parayı silmemenin ikinci güvencesi.)
- `HasGameEverStarted` başka yerde kullanılıyor mu (SteamManager.cs:651) — davranışını değiştirme, sadece raporla.

### D. Client hangar yazısı — `Assets/NewCss/TruckScripts/Truck.cs` + `TruckSpawner.cs`
- Truck'a `NetworkVariable<int> _hangarIndex` (-1 default, write Server). Server `TruckSpawner.cs:~494`'te `garageDoor` atadığı yerde index'i de yazar (spawn öncesi yazılıyorsa NV spawn payload'unda gider — sıralamayı doğrula; hafıza: server'ın spawn ÖNCESİ yazdığı DÜZ alan gitmez, NV gider).
- Client `OnNetworkSpawn` + `_hangarIndex.OnValueChanged`'de index'ten `garageDoor`'u çözer (TruckSpawner'daki hangar listesinden — yapısını oku) ve `UpdateUIText()` çağırır. `OnNetworkDespawn` temizliği client'ta da çalışsın.

## Kapı
qa (A-D, özellikle: NV yazımı yalnız server, late-join, sayı hiç azalmıyor mu, para korumasının kenar durumları) → kontrol ONAY. Ekonomik değer değişmiyor → economist yok.

## Doğrulama (kullanıcı, Local Coop Test + MPPM)
1. Host başlat (500/290) → client bağlan → 600/650. Client HUD'da da aynı. Client'ta tır gelince hangar kapısında "0/N" görünür, teslimatta artar.
2. Para kazan (≠600) → client'ta ■ Durdur → kasa DEĞİŞMEZ, kira 650 kalır, upgrade fiyatları düşmez.
3. Client tekrar bağlan → sayı 2 kalır, kasa değişmez.
