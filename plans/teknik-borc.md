# 🧹 TEKNİK BORÇ ENVANTERİ

> Kaynak: 2026-08-31 salt-okunur denetim turu (3 paralel ajan: post-merge ekonomi regresyonu ·
> client WASD statik analizi · repo hijyen taraması). Bulguların **hepsi grep ile doğrulandı**;
> "şüpheli" yazanlar sahne/prefab taraması gerektiriyordu ve bir kısmı bu turda kesinleşti.
>
> **Bu dosya bir yapılacaklar listesi değil, bir envanterdir.** Bir maddeye girmeden önce
> hâlâ geçerli olduğunu doğrula — repo bu bulgulardan sonra değişmiş olabilir.

---

## ✅ Bu turda KAPANANLAR (2026-08-31, commit `328e7e7` + `69b92c4`)

| Ne | Nasıl kapandı |
|---|---|
| 🔴 `successCallSound` silinmiş (telefonun başarılı arama sesi ölü) | `e669e33`'ten 97 satırlık AudioSource bloğu birebir geri kondu |
| `GetBaseRent` guard'sız (gün sonu kira yolunda `IndexOutOfRangeException` riski) | Kardeş getter deseniyle boş-dizi guard'ı + `FallbackBaseRent` |
| Ölü `playerCountMultiplier` (0 okuyucu) | Alan + tek yazarı silindi |
| WASD teşhis kodu build'de derlenmiyordu | Guard `#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD` + `LockMovement` çağıran izi |
| `GDD.md` bayat kira fallback uyarısı | Düzeltildi, dört-kopya senkron kuralı yazıldı |
| `sim.js` aynı dosyada üç oyun gerçekliği | Ölü `SRC` + `PLATEUP` silindi, 2297→1258 satır, çıktı bit-birebir aynı |
| `plans/devam.md` 113 satıra şişmiş | 58 satıra indi, eskiler `plans/archive/devam-2026-08-oncesi.md` |
| Playtest maddeleri 5 ayrı yerde dağınık | `plans/playtest-checklist.md` |

## ✅ Bu turda ÇÜRÜTÜLEN şüpheler (silmeyin!)

- **`StaminaBarUI` ölü değil** — `Assets/ithappy/Creative_Characters_FREE/Saved_Characters/Character.prefab`'a
  bağlı (guid `dd153a2d0a30ba440b10d41cd695ce1f`). C# tarafında 0 referans olması yanıltıcıydı.
- **`TestingNetcodeUI` · `NetworkDebugger` · `LocalCoopTestBootstrap`** — hiçbir sahne/prefab'a
  bağlı DEĞİL, yani derlenip build'e giriyor ama **hiç çalışmıyor**. Toplam ~8 KB kaynak.
  Build şişmesi endişesi geçersiz; silmek isteğe bağlı, aciliyeti yok.

---

## 🔴 AÇIK — yüksek değer

### 1. Break-room kilidi client'ta sahipsiz (WASD bug'ının en olası kök nedeni)
`BreakRoomManager.LockAllPlayersMovementClientRpc(true)` kilidi **her peer'e** koyuyor; açan tek
otomatik yol `NextDayUIManager.Update`'teki kenar tetikleme (`if (!IsUIActive()) { if (_wasActive)
Unlock(); }`). Panel o peer'de hiç açılmadıysa `_wasActive` false kalır ve **kilit hiç açılmaz**.
`CheckIfAllPlayersPresent` her break-room `OnTriggerEnter`'ında yeniden kilit yayınlıyor.
**Önce playtest** (`plans/playtest-checklist.md` B1) — kanıt gelmeden düzeltme yazma.
Ayrıca `NextDayUIManager.cs:307-315`: `nextDayPanel` bağlanmamışsa `IsUIActive()` sürekli true
döner → sahnede bu alanın bağlı olduğu doğrulanmalı.

### 2. Bir oyuncunun ESC'si herkesi donduruyor
`EscapeMenuManager` menüyü `MenuActionServerRpc`(RequireOwnership=false) → `MenuActionClientRpc`
ile **tüm peer'lerde** açıyor ve `Time.timeScale = 0` yapıyor. Co-op'ta olmaması gereken davranış;
tasarım kararı mı yoksa bug mı, kullanıcıya sorulmalı.

### 3. Çift animasyon senkronu
`PlayerMovement.cs:601-613` her frame `UpdateAnimationServerRpc` çağırıyor (kilitliyken de,
`HandleLockedState` üzerinden) → oyuncu başına ~60 ServerRpc + 60 ClientRpc/sn. Prefab'da
**zaten `NetworkAnimator` var** ve aynı 4 parametreyi senkronluyor. Gereksiz ağ yükü.

---

## 🟡 AÇIK — orta

- **`UITriggerZone.cs:34`** `"Player"` tag'i arıyor, oyuncu prefab'ının tag'i `"Character"` →
  not defteri tetikçisi muhtemelen hiç çalışmıyor. Aynı hata `CameraFollow.cs:402-420`
  `FindWithTag("Player")` fallback'inde.
- **`Character.prefab:14091`** — `CharacterController` ile aynı objede **non-kinematic Rigidbody**
  (`m_IsKinematic: 0`, `m_UseGravity: 1`). Klasik çakışma; "girdi var, pozisyon değişmiyor"
  tablosunda ikinci sırada bakılacak şey.
- **Tutorial mantığı iki klasöre bölünmüş** — `Assets/Tutorialassets/*.cs` (7 script, 85
  `Debug.Log`) ile `Assets/NewCss/Tutorial/*.cs` (3 script). Hangisinin canlı olduğu belirsiz;
  sahne/prefab taraması gerek (gameplay departmanı).
- **Üçlü varyantlar:** `StaminaBar` / `StaminaBarUI` / `NetworkStaminaBarUI` ·
  `CharacterCusUI.cs`(`NetworkCharacterCusUI`) / `CharacterCustomizationUI.cs` /
  `MainMenuCustomizationUI.cs`. Dosya adı ≠ sınıf adı olması grep'i de zorlaştırıyor.
- **~400 guard'sız `Debug.Log`** (`Assets/NewCss/` altında 769'un ~364'ü guard'lı). En yoğunları
  `PlayerSpawner.cs` (24), `PlayerMovement.cs` (24), `CustomerAI.cs` (21). Steam build'inde
  Player.log şişiyor, gerçek hata gürültüde kayboluyor.

---

## 🟢 AÇIK — düşük / kozmetik

- `InputBindingManager.GetActionDisplayName` — 0 çağrı yeri, hardcoded Türkçe. Ölü.
  ⚠️ `plans/telsiz-*.md` (3 yerde) bu metodu canlıymış gibi anlatıyor — plan da güncellenmeli.
- `CustomerManager.remainingCustomersText` + iki yazma bloğu — HUD kullanıcı tarafından
  istenmedi, sahnede `fileID: 0`. Kalıcı olarak ulaşılmaz kod.
- `PhoneWaitBar.cs:11-12, 52` yorumları hâlâ V3 modelini (`ringDuration`, `_isRinging`) anlatıyor.
- Sahnede öksüz `playerCountMultiplier: 1` anahtarı (`:87509`) — ilk sahne kaydında düşer.
- Kökteki `ECONOMY_BALANCE_REPORT.md` ve `UPGRADE_PRICING_REPORT.md` — v3.2/FAZ1 dönemi,
  11 round sonrası tüm sayıları bayat, GDD.md ile yarışıyor. Arşive taşınmalı.
- `plans/` kökünde 30+ dosya, `plans/archive/` yalnız 5. Bitmiş iş dosyaları arşivlenmemiş.
  ⚠️ Taşımadan önce PLAN.md ve `.claude/.../memory/*.md` içindeki yolları güncelle — kırılırlar.
- `docs/superpowers/plans/` ile `plans/` — iki ayrı plan klasörü, kalıcı kafa karışıklığı.

---

## 🛡️ Sigorta — ✅ KURULDU (2026-08-31)

Bu proje `successCallSound` sınıfı bir sessiz ölümü **iki kez** yedi (2026-08-13 hiç
bağlanmamıştı; 2026-08-31 `e669e33` AudioSource'u sildi). Üçüncüyü önlemek için
`EconomyInvariantCheck`'e — sahneyi zaten `OpenScene` ile açtığı için maliyeti sıfır —
`successCallSound` bağlılık assert'i eklendi.

**Assert'in gerçekten ateşlediği kontrollü deneyle kanıtlandı:** alan sahnede kasıtlı olarak
`{fileID: 0}` yapıldı → denetçi `❌ DEĞER SAPMASI — 1 kontrol` verdi ve tam nedeni yazdı;
sonra sahne geri alındı, denetçi yeniden `✅ 200 kontrol temiz`. ("Assert yazıldı" ≠ "assert
ateşliyor" — bu projede ayar sabitlerine yazılan assert'lerin hiç çalışmadığı görülmüştü.)

**Genişletilebilir:** aynı desen `PhoneWaitBar.barContainer`, quest kart alanları, garaj kapısı
`requestedCargoText` gibi "bağlanmazsa sessizce ölen" alanlar için de tekrarlanabilir.
