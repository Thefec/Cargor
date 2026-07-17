# Quest Sistemi Onarımı — Tasarım (2026-07-17)

> Durum: onaylandı (kullanıcı), implementasyon planı bekliyor.
> Dal: `feature/quest-system-repair` (main'den açıldı).
> Teşhis kaynağı: qa salt-okunur raporu (bu oturum) + müdür kod teyidi.

## 1. Amaç

Quest sistemi pasif: co-op oturumunda UI açılıyor ama "butonlar çalışmıyor", quest'ler işlemiyor.
Bu tur sistemi **çalışır minimum + mimari güvenlik** seviyesine getirir. Yenilik/denge tartışması
çalışan bir taban üstünde ayrı bir turda yapılır (kullanıcı kararı: "önce onar, sonra karar").

**Kod ölü değil, kopuk.** 3115 satır (`Assets/Scripts/Quest/`), tüm componentler sahnede, butonlar
Inspector'da atanmış, netcode deseni (`NetworkList`/`ServerRpc`/`ClientRpc`) doğru kurulmuş. Sorun
üç noktada.

## 2. Kök neden (teşhis — kanıtlı)

Kullanıcı **host/co-op** olarak test etti (netcode-başlatma sorunu elendi). "Buton çalışmıyor"
belirtisinin gerçek mekanizması:

1. **`easy1` veri seviyesinde kırık.** `Assets/Resources/Quests/easy1.asset:18` → `questType: 0`
   (`CompleteMinigame`) ama başlığı "5 kırmızı kutu paketle". `NotifyMinigameCompleted()` tetikleyicisi
   kod tabanında **hiç çağrılmıyor** → kabul edilse bile sonsuza kadar `Active`, `Collect` butonu hiç
   çıkmaz. Bu quest her gün atanan 3'ten biri.

2. **Günde-1-kabul limiti + sessiz red.** `QuestManager.AcceptQuestInternal:557-561` — oyuncu bir quest
   (örn. kırık easy1) kabul ettiyse, diğer slotların accept butonu **görünür ama basınca sessizce
   `return`** eder. `UpdateButtonStates` (`QuestSlotUI.cs:292`) yalnızca `status`'a bakar, limite bakmaz
   → buton aktif görünür, tıklama hiçbir şey yapmaz, hiçbir geri bildirim yok. **Kullanıcının gördüğü
   "buton çalışmıyor" tam olarak bu.**

3. Belirti = kırık veri (#1) + sessiz red (#2) kombinasyonu; buton kablosu ya da UI senkronu değil
   (`_dailyQuests` NetworkList, `OnListChanged` → `HandleDailyQuestsChanged` UI'ı yeniliyor, doğru).

Ek yapısal bulgular (kapsama alınan):

4. **BuffManager stacking bug.** `BuffManager.cs:180-183` — `NetworkListEvent.Value` case'inde
   `ApplyBuffEffect` **çağrılmıyor**, sadece `OnBuffUpdated` fırlatılıyor. Aynı tür buff ikinci kez
   eklenince kayıttaki `amount` büyür ama gerçek stat etkisi (hız/stamina) uygulanmaz. **Not:** fix
   yazılmadan önce `AddBuffInternal` (342-366) + `ApplyBuffEffect` (494-547) mantığı doğrulanmalı —
   effect additive mi absolute-set mi? Çift-uygulama riski varsa fix ona göre şekillenir.

5. **Mimari risk.** `QuestManager`/`BuffManager`/`NetworkObject`/`QuestTriggerZone` bir dekor prefab
   instance'ına (`Office_Computer (1)`, `The Main Office.unity:69520-69598`) **override-component**
   olarak yapıştırılmış. O prefab'da "Revert/Apply" yapılırsa dört bileşen de **sessizce, log'suz**
   silinir → tüm quest+buff+network zinciri kaybolur.

## 3. Kapsam

### İÇERİDE (bu tur)

| # | Değişiklik | Dosya | Tip | Departman |
|---|---|---|---|---|
| R1 | `easy1` questType 0→3 (PackToy); targetCount=5 ve boxType=kırmızı hedefe uygun mu doğrula | `easy1.asset` | Asset (YAML) | müdür/gameplay |
| R2 | Günlük-limit UX: accept sessiz-red'ine görünür geri bildirim + o gün quest alındıysa diğer slotların accept butonunu pasif/gri | `QuestManager` (limit durumunu UI'a expose) + `QuestSlotUI`/`QuestUIController` (buton state + mesaj) | Kod + UI | gameplay + graphics-ui |
| R3 | BuffManager stacking bug: `Value` case'inde doğru effect uygulaması (§2.4 doğrulamasına göre) | `BuffManager.cs:180-183` | Kod | gameplay |
| R4 | Kritik sistemleri `Office_Computer (1)`'den ayrı `_QuestSystem` GameObject'ine taşı | `The Main Office.unity` | Sahne (Unity) | kullanıcı + müdür yönlendirme |

### DIŞARIDA (yenilik/denge turuna) — bilinçli ertelenen

- 4 ölü tetikleyici: `NotifyMinigameCompleted`/`NotifyPhoneAnswered`/`NotifyPackagingMistake`/
  `NotifySpecificColorTruckCompleted` (çağıran yok) + `CustomerAI.cs:871` yorumlu `NotifyCustomerServed`
- `easy4`/`easy5`: sahneye bağlı değil + duplicate `questId=4` (`easy5.asset:15`)
- BuffManager late-join senkron: sonradan katılan oyuncu mevcut buff'ları almıyor
- **Ekonomik denge**: `easy3` ödülü 100-200 TL, diğerleri 8-30 TL — orantısız. economist'e **flag**
  (bu turda değer değişmez; yenilik turunda ekonomi doğrulamasıyla birlikte)

## 4. Yaklaşım detayı

**R1 (easy1 asset):** questType tek int (`0`→`3`). Ama PackToy'un `QuestData`'da hangi alanları
(targetCount, targetBoxType) kullandığı doğrulanmalı — `Table.cs:777 NotifyToyPacked(boxType)` hangi
boxType'ı yolluyor, easy1 hedefi "kırmızı" ile eşleşiyor mu. YAML editi düşük riskli ama hedef-eşleşme
doğrulanmadan yapılmaz.

**R2 (limit UX):** İki parça. (a) `QuestManager` bir "bugün quest alındı mı" durumunu client'a
görünür kılmalı (mevcut `HasAcceptedQuestToday` server-side; UI için NetworkVariable veya mevcut
`_dailyQuests` durumundan türetme). (b) `QuestSlotUI.UpdateButtonStates` bu duruma bakıp accept
butonunu `interactable=false`/gri yapmalı + accept denemesinde reddedilirse kısa mesaj
(`LocalizationHelper` deseni mevcut, `Quest_*` anahtarı). Sessiz `return` görünür olur.

**R3 (buff bug):** §2.4 doğrulaması ilk adım. Effect additive ise `Value` case'inde delta uygulanır;
absolute-set ise mevcut davranış aslında doğru olabilir (o zaman fix yok, sadece belgele). Kör fix yok.

**R4 (sahne taşıma):** YAML'dan sahne editi riskli (`[[unity-batchmode-artifacts]]` deneyimi). Kullanıcı
Unity Editor'de yapar, müdür adım-adım yönlendirir: yeni boş `_QuestSystem` GameObject → 4 bileşeni
oraya taşı (veya kes-yapıştır) → referansları (allQuests listesi, UI bağları) yeniden bağla → prefab
instance'ından override component'leri kaldır. Taşıma sonrası tüm referanslar korunmalı (GUID'ler aynı).

## 5. Başarı kriteri (doğrulama borcu — play-test)

Onarım sonrası co-op host oturumunda:
1. Panel açılır, 3 quest görünür, hiçbiri kalıcı-kırık değil (easy1 artık PackToy, ilerler)
2. Accept → quest `Active`, buton durumu güncellenir; **o gün ikinci accept denemesi görünür şekilde
   reddedilir** (sessiz değil)
3. Hedef aksiyon (kırmızı kutu paketle / rafa koy / tır tamamla) → progress artar → `Completed` →
   `Collect` butonu çıkar → basınca ödül (para/prestij/buff) uygulanır
4. Buff alınca stat etkisi gerçekten uygulanır (R3), ikinci kez alınca da
5. Prefab güvenliği: `_QuestSystem` ayrı obje, `Office_Computer` prefab'ında revert/apply sistemi
   etkilemez

## 6. İş akışı

CLAUDE.md BÜYÜK/RİSKLİ iş (netcode + ekonomik değer + sahne + çok dosya):

- **gameplay**: R2 (limit durumu) + R3 (buff bug). ServerRpc/NetworkList auth desenine dikkat
  (bkz [[cargor-roguelite-status]] G-serisi auth işleri — quest RPC'leri `RequireOwnership=false`,
  aynı exploit yüzeyi kontrol edilmeli ama bu turda kapsam-dışı, not düşülür).
- **graphics-ui**: R2 UI parçası (buton gri/pasif + mesaj).
- **müdür**: R1 asset düzeltme (KÜÇÜK, YAML) + R4 sahne yönlendirmesi.
- **economist**: R3 sonrası + easy3 orantısızlığına **flag** (değer önerisi yenilik turunda).
- **kontrol**: dal-sonu **tek toplu** ONAY kapısı (her R'yi ayrı sokma). En fazla 3 tur.
- **Unity kapalı** → R2/R3 kod değişikliği sonrası headless EditMode ile 0 derleme hatası teyidi.
  R1 asset + R4 sahne Unity gerektirir → kullanıcı Editor'de.

## 7. Riskler

| Risk | Azaltma |
|---|---|
| R1 hedef-eşleşme yanlış (PackToy boxType tutmaz) | Table.cs hook'unun yolladığı boxType doğrulanmadan asset değişmez |
| R3 kör fix çift-uygulama yaratır | AddBuffInternal + ApplyBuffEffect mantığı önce doğrulanır |
| R4 sahne editi referansları koparır | Unity Editor'de yapılır (YAML değil), GUID'ler korunur, taşıma sonrası referans teyidi |
| Quest RPC auth exploit (RequireOwnership=false) | Bu turda kapsam-dışı ama not düşüldü — güvenlik backlog'una |
| Çalışma ağacı font/materyal churn | Commit DIŞI, seçici commit ([[unity-batchmode-artifacts]]) |

## 8. Açık kalan (bu tur DIŞI)

- Yenilik/denge turu: ölü tetikleyicileri bağla, easy4/5, late-join buff, ekonomik denge (easy3)
- Ekonomi doğrulama turu (`feature/economy-verification`, spec `a522ed7`) — quest ödülleri de
  ekonomik değer; iki turu birlikte dengelemek mantıklı olabilir
- Netcode auth-hardening dalının 3 commit'i hâlâ play-test + merge bekliyor
