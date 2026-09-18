# Tasarım Denetimi Sentezi — 2026-09-18

Kaynaklar: A1-determinism, A2-repetition, A3-mechanics, A4-feedback, A5-benchmark, B1-netcode (`docs/playtest/denetim-2026-09-18/`).

## 1. Tek cümlelik teşhis

Oyun statik hissettiriyor çünkü koşu pratikte kaybedilemez (A1), 16 günün 11'i yapısal olarak birbirinin aynısı (A2), zorluk oyuncu sayısına hiç ölçeklenmiyor (B1+A3), en baskın strateji olan telefon bedelsiz kazandırıyor (A3), yükseltme havuzunun dörtte biri erişilemez (A3) ve en büyük ödül ile en büyük ceza sessiz (A4) — yani sistem zaten gerilim üretmiyor, ara sıra üretse bile oyuncu bunu hissedecek geri bildirimi almıyor.

## 2. Nedensel zincir

1. **Koşu pratik olarak kaybedilemez.** A1: gerçekçi gürültüde (σ=0.15) test edilen 10 ekonomi hücresinin tamamında 40.000 denemede 0 iflas, %100 kazanma. Aşırı stres testinde bile (σ=0.90) kazanma oranı en kötü %98.3'e düşüyor, iflaslar yalnızca gün 12/16 kira kapılarında oluyor — gün 4/8'de sıfır (A1 §1-2).

2. **Zorluk oyuncu sayısına hiç ölçeklenmiyor — iki bağımsız neden, ikisi de ayrı ayrı düzeltilmeli:**
   - B1 bulgu 1: `DifficultyManager.cs:172-184,248-289,520-534` — oyuncu sayısı `OnNetworkSpawn`'da yalnızca BİR KEZ okunuyor, `ConnectedClientsList` değişince yenilenmiyor (geç katılım/ayrılmada bayatlıyor, ekonomi çitleme riski).
   - A3: `DifficultyManager.cs:122-146,425-441` — `ApplyCustomerSettings` doğru değeri yazsa bile bu sadece bir kez, `CustomerManager` henüz müşteri spawn etmeden çalışıyor; `CustomerManager.cs:706`'daki sürekli `Instantiate` her yeni müşteriyi prefab'ın statik 15/20s patience değeriyle dünyaya getirip yazılan değeri eziyor.
   - Sadece birini düzeltmek yetmez: B1 tek başına düzelse bile A3'teki ezilme sorunu kalır; A3 tek başına düzelse bile bayat P-sayısını okur.

3. **Telefon koşulsuz ödüyor, baskın strateji haline geliyor.** A3 §3: `PhoneCallManager` — arama, doğal müşteri servisiyle aynı prestij (+0.4) kazandırırken +20 TL koşulsuz ödüyor ve zaman kazandırıyor; GDD'nin kendi ekonomi simülasyonu bazı bantlarda (Slow/strict P1-P2) optimal oyunun "%100 arama" olduğunu söylüyor — zaman-para takas kararı spam-butona dönüşüyor.

4. **16 günün 11'i yapısal olarak değişmiyor.** A2: Gün 1-3 birebir aynı (kota, olaysız, T0 quest tavanı, 200s); gün 6-7/10-11/14-15 her biri bir önceki açılış gününün (5/9/13) düz devamı — sadece süre (+10s/gün) ve kota (+0-1 müşteri) artıyor. Üç gerçek mekanik açılışı (dönüş modu, çift-ürün, karışık kamyon) tek seferlik kapılar; tekrar eden yeni bir karar üretmiyorlar.

5. **Yükseltme havuzunun dörtte biri kalıcı devre dışı, build derinliği türün en sığı.** A3: `DraftPool.cs:27` `IsEligible` filtresi 25 yükseltmeden 6'sını (Geniş Kuyruk, Sağlam Kasa, Dinç Ekip, Su Sebili, Güler Yüz, Uzun Kuyruk) hiçbir teklifte göstermiyor. A5: PlateUp günde ~15 build kararı sunarken Cargor'un günde-en-fazla-1 kart draft'ı zaten türün en sığ sistemi; devre dışı 6 kart bunu daha da sığlaştırıyor.

6. **En büyük ödül ve en büyük ceza ikisi de sessiz.** A4: Doğru teslimat (`Truck.cs:650`, `SfxId.CorrectItem` boş slot) ve para kazanma (`MoneyUI.cs:70`, `SfxId.MoneyEarned` boş slot) sessiz — bilerek boş bırakılmış, klip seçimi bekleniyor. Müşteri sabırsızlanıp öfkeyle ayrılması (`CustomerAI.cs:965-985 HandleTimeUp`) hem sessiz hem görsel düz — hiç `SfxBus` çağrısı yok; bu ise kazara sessiz, hiçbir yorum kasıtlı işaretlememiş.

## 3. Öncelikli iş listesi (etki/efor sırası)

1. **[BUG, orta boy]** Zorluk oyuncu sayısına ölçeklenmiyor — iki parçalı düzeltme gerekiyor. Yer: `DifficultyManager.cs:172-184,248-289,425-441,520-534`; `CustomerManager.cs:706`. Neden: A1'in "kaybedilemez oyun" bulgusunun P-ölçekleme ayağı; tek başlayıp arkadaş davet etme ekonomi çitlemesi olarak kullanılabilir (B1). Bağlantı event'lerine abone olma + spawn-zamanlı per-customer ayarlama, birlikte gitmeli.

2. **[BUG, küçük]** Telefonun koşulsuz +20 TL ödemesi. Yer: `PhoneCallManager.cs` (`ExecuteCall`), GDD §14.4. Neden: zaman-para takasını spam-butona indirgiyor; GDD'nin kendi simülasyonu bunu zaten sorun olarak işaretlemiş. **Bu economist'in çağrısı** — gameplay ekonomik değer uydurmasın, önce economist'e danışılmalı.

3. **[BUG, küçük]** En büyük ödül/ceza anlarında ses eksikliği. Yer: `SfxLibrary.asset` id 8/9 boş; `CustomerAI.cs:965-985` hiç `SfxBus` çağrısı yok. Neden: A4'ün en yüksek maliyetli 4 bulgusunun 3'ü burada; ikisi zaten bilerek boş, müşteri kaybı sessizliği kazara. graphics-ui/gameplay işbirliği, küçük iş.

4. **[TASARIM KARARI, büyük]** 11/16 gün yapısal olarak aynı. Yer: `DayCycleManager.cs`, `GameEconomySettings.cs`, `PostRentFeatureUnlocks.cs`. A2+A5 birlikte gösteriyor: tür kıyaslarının "her gün yeni bir şey" ritmi yok. Roadmap kararı — gameplay+economist ortak planlaması gerekir, tek oturumda çözülmez.

5. **[TASARIM KARARI, küçük-orta]** 6 kalıcı devre dışı yükseltme. Yer: `UpgradePanel.cs:76`, `DraftPool.cs:27`. Şu an "ne kes ne kullan" limbo'da — bitirilip havuza sokulmalı ya da silinmeli.

6. **[TASARIM KARARI, büyük]** Kaybedilemezlik/gerilim eksikliğinin kökü. Yer: `sim.js:507` (`rentGrowthMultiplier=1.20`), `EventCalendarUI.cs` (16 sabit olay), iflas kapıları sadece gün 12/16. A1'in ana bulgusu: "gün 5 kararı belirliyor" hipotezi reddedildi, gerçek sorun tüm 16 gün boyunca risk yokluğu. Economist'e gitmeli, tek başına kod değişikliği değil.

## 4. Kes listesi

- `DifficultyManager.cs:122-146` (`ScaledCustomerCount`/`ScaledMinPatience`/vb.) — dosya dışında hiç tüketicisi yok (A3), GDD zaten neden çalışmadığını dokümante etmek zorunda kalmış. Ya #1 fix'iyle gerçekten bağlanmalı ya da ~150 satır ölü kod olarak silinmeli.
- `rewardPerBox` skaler alanı (`GameEconomySettings`) — per-player-count dizisi hep dolu olduğu için asla okunmuyor, inspector gürültüsü (A3).
- `NetworkObjectPool.cs` — doğru yazılmış, server-gated, ama proje genelinde sıfır çağrı yeri (B1 bulgu 5). Bağlanmalı ya da silinmeli.
- `LobbyManager.cs` (Assets/MENUUI/Lobby) — "GameScene" yüklüyor, SteamManager'ın kullandığı sahneyle uyuşmuyor, relay servisi bağlanmamış (B1 bulgu 6), hiçbir sahne/prefab referansı yok. Muhtemelen ölü — devops/qa ile teyit edip silinmeli.
- `IsGoldenBoxDay()`/`IsVIPServiceDay()` ve kota-artıran 4 olay (BUSY DAY, MARKETING DAY, ANGRY CUSTOMERS, GOLDEN BOX DAY) — okuyucusu yok/mekanik olarak etkisiz (A2, GDD §15.2). 16 olayın kalitesini düşürüyorlar.

## 5. Dürüst sınırlar

- "Eğlenceli mi" sorusuna bu denetim cevap veremez — hiçbir model oyunu oynamadı, bulgular kod okumasından geliyor. Gerçek playtest gerekir.
- A1'in varyans katmanı sentetik: `sim.js`'de sıfır `Math.random` var (grep doğrulandı); rapordaki gürültü sayıları bu oturumda yazılmış bir driver'ın eklediği ±15%/Bernoulli katmanlarından geliyor, oyunun gerçek stokastikliği değil.
- A1 quota-progress@day5 korelasyonu ölçülemedi (varyansı tam sıfır, closed-form matematik) — modelleme artefaktı, oyun sinyali değil.
- Olay sisteminin ekonomiye etkisi `runFullSim` tarafından hiç modellenmiyor — 16 olayın gerçek koşu içi etkisi test edilemedi.
- B1 bulgu 2 (ServerRpc clientId spoofing) ve bulgu 3 (geç katılan müşteri kurulumu) bugünkü prefab varsayılanlarıyla gözlemlenebilir hataya yol açmıyor — canlı Unity sahnesi/2-makine co-op testi olmadan doğrulanamaz.
- A4'teki `remainingCustomersText`'in sahne YAML'ında gerçekten bağlı olup olmadığı bu oturumda yeniden doğrulanmadı — sahne kontrolü gerekiyor.
- A4'teki Truck enter/exit ses alanları ve `BoxFallPenalty.boxDropSound`'un instance bazında boş olup olmadığı 2026-09-14 taramasına dayanıyor, canlı yeniden taranmadı (Unity Editor batchmode gerektirir).
- A5 tür kıyaslaması yalnızca GDD.md metnine dayanıyor, kodun bu vaatleri birebir karşılayıp karşılamadığı (örn. mağaza düzeninin gerçekten sabit mi olduğu) satır satır doğrulanmadı.
