# PlateUp-tarzı müşteri döngüsü + telefonun yeniden tasarımı

## Bağlam

**Sorun.** Cargor'un gün döngüsü saat-güdümlü: gün başında kapasiteye göre bir müşteri kotası hesaplanıyor, 08:00–17:00 arasına serpiştiriliyor, gün ise **saat dolunca** bitiyor. Bunun üç somut sonucu var:

1. **Ölü bekleme.** Müşteriler erken bitse bile oyuncu 17:30'a kadar boş dükkanda bekliyor.
2. **Sessiz kayıp.** Kuyruk kapasitesi 2; sıkıştığında o günün planlanmış müşterilerinin bir kısmı hiç spawn olmadan gün bitiyor (`CustomerManager.CanSpawnScheduledCustomer:515` → `IsQueueFull` dönerse sıra ilerlemiyor). Oyuncu bunu göremiyor.
3. **Ters teşvik.** Müşteri sayısı raf/masa sayısına bağlı (`CalculateTodaysCustomerCount:395`); yükseltme yapan oyuncunun günü uzuyor.

Ayrı olarak **telefon anlamsızlaşmış.** `PhoneCallManager` V3 "reaktif" modelde: saatlik zar atıyor, çalıyor, açan +20 TL +0.4 prestij alıyor, açmamanın cezası yok. Karar içermiyor, tempoya etki etmiyor — bedava para otomatı. Dosyanın kendi yorumu (satır 12) V2'de müşteri çağırma + zaman atlama olduğunu ve söküldüğünü yazıyor.

**Hedeflenen sonuç.** PlateUp'ın gün yapısı: günün müşteri sayısı önceden belli, son müşteri çıkınca gün biter. Telefon ise oyuncunun **tempoyu hızlandırma kolu** olur — çevirince sıradaki müşteri hemen gelir, karşılığında gün saati ilerler.

## Onaylanmış kararlar

| Konu | Karar |
|---|---|
| Saat | **Kalır.** Güneş, tır saatleri, panel saat kapıları hiç değişmez. Saat sürücü değil, son tarih olur. |
| Gün bitişi | Günün kotası tükenip son müşteri çıktığında gün **erken** biter. |
| Müşteri sayısı | **Gün numarası** eğrisi × oyuncu sayısı. Kapasite (raf/masa) etkisi tamamen kalkar. |
| Geliş şekli | **Otomatik** — oyuncu çağırmadan da gelirler (PlateUp modeli). |
| Telefon | **Çalmaz.** Oyuncu çevirir → sıradaki müşteri hemen gelir + saat ilerler + küçük para/prestij ödülü. |
| Yetişememe | Servis edilmeden çıkan her müşteri **prestij cezası** (mevcut `customerLostPrestigePenalty`). |

## Yeniden kullanılacak mevcut altyapı

V2'nin parçaları silinmemiş, sadece çağıran kalmamış — sıfırdan yazılmayacak:

- `CustomerManager.ForceSpawnNextCustomer()` — `Assets/NewCss/CustomerSripts/CustomerManager.cs:649`
- `CustomerManager.HasUnspawnedCustomers` — aynı dosya, satır 260
- `DayCycleManager.SkipTime(float minutesToSkip)` — `Assets/NewCss/UIScripts/DayCycleManager.cs:418`
- `GameStateManager.OnCustomerLost()` — `Assets/NewCss/GameState/GameStateManager.cs:628` (prestij cezası + SURPRISE AUDIT 2× çarpanı zaten içinde)
- `CustomerAI.ForceExitDueToEndOfDay()` — `Assets/NewCss/CustomerSripts/CustomerAI.cs:1711`
- Telefon anti-spam server-authoritative deseni — commit `f9a3f1b` ("bedava-para exploit")

---

## A. Müşteri sayısı: gün-numarası eğrisi

`CustomerManager.CalculateTodaysCustomerCount:395` yeniden yazılır.

- **Kalkar:** `CountActiveInteractables()`, `_shelfMultiplier`, `_levelMultiplier`, `_storeLevel`, `_minVariance`/`_maxVariance` (rastgele sapma — deterministik kota dengelemeyi mümkün kılar).
- **Kalır:** `eventCustomerMultiplier` (INTENSIVE DAY), `playerCountMultiplier`, `_minCustomersPerDay`/`_maxCustomersPerDay` clamp'i.
- **Yeni:** `GameEconomySettings`'e gün→kota eğrisi. Dizi (`int[16]`) tercih edilir; formülden okunaklı ve economist'in tek tek ayarlamasına açık.

> ⚠️ **Eğrinin sayıları economist'in işi.** Kota artık günlük gelir tavanını doğrudan belirliyor ve kira %20 bileşik büyüyor (`rentGrowthMultiplier=1.20`). Eğri kira eğrisiyle tutmazsa doğrudan iflas. `tools/economy-sim/sim.js` bu değişiklikten sonra resync edilmeli — sim'in müşteri modeli de kapasite tabanlı.

## B. Geliş temposu: saate serpiştirme → aralık tabanlı

**Bu, "gün erken bitsin" kararının zorunlu teknik sonucu.** Mevcut `CalculateSpawnSchedule:442` kotayı 08:00–17:00'a eşit dağıtıyor; yani son müşteri zaten ~17:00'da geliyor ve "kota bitince gün biter" **hiç tetiklenmez**. Erken bitiş gerçek olsun diye zamanlama değişmeli:

- `_scheduledSpawnTimes` mutlak-saat listesi yerine **aralık tabanlı** akış: bir müşteri geldikten `customerArrivalInterval` saniye sonra sıradaki gelir (kuyruk doluysa bekler, mevcut `IsQueueFull` kapısı korunur).
- Günün doğal uzunluğu = `kota × aralık`. Yetişen oyuncu erken bitirir; sıkışan oyuncunun kuyruğu tıkanır ve 17:30 son tarihine yaklaşır.
- `WaveSettings` ölmez: `GetSpawnRateMultiplier` artık aralığı ölçekler (rush hour = kısa aralık). `IsRushHour`/`CurrentPeriodName` aynen çalışır.
- Aralık, kotanın gün penceresine rahat sığacağı şekilde seçilir (economist).

## C. Gün erken bitişi

- `DayCycleManager`'a server-only `FastForwardToEndOfDay()` eklenir: `_networkElapsedTime.Value = CurrentDayDuration`.
- `CustomerManager.NotifyCustomerDone:835` içinde günün işi bittiğinde tetiklenir. Bitiş koşulu üçü birden: `_customersRemainingToday <= 0` **ve** `_customerQueue.Count == 0` **ve** `!HasUnspawnedCustomers`.
- **Neden bu yol:** `ProcessDayEnd():517` yalnız `elapsedTime >= CurrentDayDuration` şartına bakıyor. Zamanı sona sarmak, kira kontrolü / break room / gün sonu ekranı / `IsTimeUp` break-room exploit guard'ının (`DayCycleManager.cs:896`) tamamını **değiştirmeden** doğru çalıştırır. İkinci bir gün-bitiş yolu açmak bu guard'ları çoğaltmayı gerektirirdi.
- **Kapanış payı (`dayEndGraceSeconds`, öneri 30 sn):** son müşteri çıktığı anda sarmak, hangarda yarım yüklü tırı olan oyuncunun o günkü kutularını yakar (para yalnız tırdan gelir). Sarmadan önce kısa bir "gün kapanıyor" geri sayımı gösterilir; bu sürede tır yüklemeye devam edilebilir.
- Sarma anında güneş/ışık sıçrar (`DayLightController`, `AutoLightController` `CurrentTime`'dan besleniyor). Grafik cilası: sarmayı 1–2 sn içinde yumuşat.

## D. Telefon V4 — dışarı arama

`Assets/NewCss/Phone/PhoneCallManager.cs` büyük ölçüde yeniden yazılır.

**Silinir:** `_isRinging` NetworkVariable, `ringDuration`, `ServerUpdateRinging`, `TryRollRing`, `GetEffectiveRingChance`, `_lastRingRollHour`, `ringingSound` loop'u, `HandleRingingChanged`.

**Kalır:** trigger alanı algılama (`OnTriggerEnter/Exit`, `IsLocalPlayer`), `InputBindingManager.GameAction.Interact` girdisi, `successCallSound`, singleton/network yaşam döngüsü, `WarnOnMissingReferences` (2026-08-13'teki "sessiz telefon" bug'ını önleyen uyarılar — korunmalı).

**Yeni akış** (`CallNextCustomerServerRpc`, server-authoritative):

```
Guard'lar:  mesai içinde mi  ·  HasUnspawnedCustomers  ·  !IsQueueFull  ·  cooldown dolmuş mu
   ↓ hepsi geçerse
ForceSpawnNextCustomer()          → sıradaki müşteri hemen gelir
DayCycleManager.SkipTime(N)       → saat N dakika ilerler   (bedel)
MoneySystem.AddMoney(...)         → callMoneyReward
PrestigeManager.AddPrestige(...)  → callPrestigeReward
Quest.QuestTracker.NotifyPhoneAnswered()
```

- `GameEconomySettings`'e `timeSkipAmount` **geri eklenir** (V2'de vardı, V3'te silindi) + `phoneCooldownSeconds`.
- Anti-spam cooldown **server'da** tutulur (client'a NetworkVariable ile yansıtılır); `f9a3f1b`'nin exploit dersine uyar.
- `PhoneWaitBar` çöpe atılmaz: geri sayım barı artık **cooldown göstergesi** olur.

**Ölü kalacak alanlar — temizlenmeli:** `phoneRingChancePerHour`, `phoneRingChanceByPlayerCount`, `phoneRingEventMultiplier`, `phoneRingPerkBonus` (`GameEconomySettings.cs:86-102`) ve bunları doğrulayan `EconomyInvariantCheck.cs:278-283`.

> ⚠️ **`phone_line` perki ölür — DOĞRULANDI, canlı bir perk.** `PerkEffect.cs:68` → `ApplyPhoneLine` → `PerkEffect.cs:294` `phoneRingPerkBonus = 0.15f` yazıyor ve sahnede gerçekten satılıyor (`The Main Office.unity:27611`, `effectId: phone_line`). Çalma şansı kalkınca perk sessizce hiçbir şey yapmaz — oyuncu para verip boş alır. Yeniden hedeflenmeli (öneri: `phoneCooldownSeconds` kısaltma).
> **Aynı turda güncellenmesi ZORUNLU:** `UpgradePanel.cs:620` (`PerkAssetSnapshot.PhoneRingPerkBonus`), `:663` (snapshot alma), `:714` (geri yazma). Alan adı/anlamı değişip snapshot tazelenmezse [[perk-mutates-persistent-assets]]'teki "event bitince perk siliniyor" hatasının aynısı tekrarlanır. `EconomyInvariantCheck.cs:306` (`ExpectPristine(... "phone_line")`) de birlikte güncellenir.
>
> ⚠️ **CUSTOMER SUPPORT event'i** telefon çalma şansını çarpıyordu (`GetEffectiveRingChance:294`); tek bağlantısı buydu. Yeni modelde ne yapacağı `EventEffectManager` tarafında yeniden tanımlanmalı (öneri: cooldown yarıya iner).

## E. Yetişememe cezası

- `CustomerManager.ForceAllCustomersToExit:584` — çıkışa zorlanan her müşteri için `GameStateManager.Instance.OnCustomerLost()` çağrılır.
- **Yalnız servis edilmemişler** cezalandırılır: `WaitingForPickup` durumundaki (işi bitmiş, çıkışa yürüyen) müşteri ceza almaz. `CustomerAI`'a bunu ayırt eden salt-okunur bir özellik gerekir.
- Hiç spawn olmamış kota müşterileri (`_scheduledSpawnTimes` kalanı) de aynı cezayı alır — kullanıcı kararı ("her yetişemediğin müşteri").
- Çifte cezalandırma tuzağı: sabrı bitip `HandleTimeUp:946` yolundan çıkan müşteri **zaten** `OnCustomerLost` çağırıyor. `_hasTimedOut` bayrağı ile gün-sonu yolunda ikinci kez sayılmamalı.

---

## Kritik riskler

1. **Ekonomi yeniden dengelenmeli — bu işin en büyük parçası.** Kota artık gelir tavanı. Kira eğrisi (`rentGrowthMultiplier=1.20`) ile gün-numarası kota eğrisinin birlikte çözülmesi gerekiyor. `sim.js`'in müşteri modeli kapasite tabanlı; resync şart. **economist turu, kod yazmadan önce.**
2. **`sim.js` event sıklığını modellemiyor** (2026-08-25'te ampirik doğrulandı). Sim'in vereceği "bant güvenli" cevabı garanti değil; playtest'te 4P/STRICT bandı izlenmeli.
3. **Erken bitiş + yarım yüklü tır — DOĞRULANDI, sanıldığından hafif.** Para tır çıkarken değil **her kutu yüklendiğinde anında** ödeniyor (`Truck.cs:640-643`: `_deliveredCount++` → `AddMoney`). Yani zamanı sona sarmak kazanılmış parayı yakmıyor; tek kayıp **hazırlanmış ama tıra taşınmamış** kutular. `dayEndGraceSeconds` bu taşımaya zaman tanımak için, kargo kurtarmak için değil.
   ⚠️ Yine de gerçek: sarma anında `CurrentHour` `truckEndHour`'a fırlar → `TruckSpawner.ProcessServerUpdate:301` bir sonraki frame `ForceExitAllTrucks()` çağırır. Grace penceresi sarmadan ÖNCE işlemeli (sarma sırasında değil), yoksa oyuncuya taşıma fırsatı kalmaz.
4. **Saat kapıları — ÖLÇÜLDÜ, risk küçük.** `UpgradePanel.cs:137` ve `OfficeTerminal.cs:20` ikisi de `PANEL_OPEN_HOUR = 10` (planın ilk taslağındaki "14:00" varsayımı yanlıştı). Gün 08:00'de başladığına göre panelin kaçırılması ancak kotanın 2 oyun-saatinden kısa sürmesiyle mümkün — `customerArrivalInterval` belirlenince tek bir aritmetik kontrolle kapanır. `CharacterCusUI:345` ise `currentHour <= 14` yani **erken biten günde daha erişilebilir** olur, risk değil.
5. **Sahne/Inspector borcu.** Kalkan SerializeField'lar (`_shelfMultiplier` vb.) sahnede override taşıyor olabilir; [[unity-scene-override-vs-code-default]] — silmeden önce YAML'da grep'lenmeli.

## Uygulama sırası

| # | İş | Departman |
|---|---|---|
| 0 | Kota eğrisi + `customerArrivalInterval` + `timeSkipAmount` + cooldown + ceza bandı; `sim.js` resync | **economist** (önkoşul) |
| 1 | A + B: `CustomerManager` kota & tempo yeniden yazımı | gameplay |
| 2 | C: `FastForwardToEndOfDay` + grace + `NotifyCustomerDone` kancası | gameplay |
| 3 | D: `PhoneCallManager` V4 + ölü ekonomi alanlarının temizliği | gameplay |
| 4 | E: gün-sonu cezası + çifte sayım guard'ı | gameplay |
| 5 | Perk/event yeniden hedefleme (`phoneRingPerkBonus`, CUSTOMER SUPPORT) | gameplay + economist |
| 6 | Kalan müşteri sayacı UI'ı, cooldown barı, "gün kapanıyor" geri sayımı, güneş sarma yumuşatma | graphics-ui |
| 7 | Saat kapılarının (risk #4) gözden geçirilmesi | gameplay |
| 8 | İnceleme → kalite kapısı | qa → kontrol |

## Doğrulama

- **Headless derleme + test:** Unity 6000.5.6f1 batchmode, 0 CS + EditMode paketinin tamamı. Unity kapalıyken koşulmalı ([[unity-headless-verify]]); Unity açıkken yanıltıcı yeşil verir.
- **`EconomyInvariantCheck`** yeni sabitlerle güncellenip temiz geçmeli (telefon çalma-şansı beklentileri silinecek).
- **Saf mantık için EditMode testi:** kota eğrisi ve "gün bitti mi" üçlü koşulu motor referanssız saf sınıflara alınırsa test edilebilir (`PostRentFeatureUnlocks` / `TruckColorMixing` deseni). ⚠️ `Cargor.Tests.EditMode` asmdef'i `Assembly-CSharp`'a referans veremiyor — saf mantık ayrı asmdef'e çıkmazsa test yazılamaz (bilinen duvar).
- **Playtest (kullanıcı, gerçek Unity):**
  1. Kota bitince gün gerçekten erken bitiyor mu, grace süresinde tır yüklenebiliyor mu
  2. Telefon: müşteri geliyor + saat ilerliyor + cooldown çalışıyor, spam edilemiyor
  3. 17:30'da yetişemeyince prestij cezası bir kez uygulanıyor (çifte sayım yok)
  4. Güneş sarması göze batıyor mu
  5. Yükseltme paneli / `CharacterCusUI` erken biten günde hâlâ açılıyor mu (risk #4)
- **Multiplayer:** host + client, gün erken bitişinin iki tarafta da senkron olduğu (elapsedTime NetworkVariable üzerinden replike oluyor, tek yazar server).
