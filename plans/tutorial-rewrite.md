# 🎓 Tutorial Sistemi — Sıfırdan Yeniden Yazım

> Dal: `feature/tutorial-rewrite` · Başlangıç: 2026-08-31 · Onay: kullanıcı ("devam et başla")

## Neden

Ana oyun haritasında (`The Main Office`) kodlanmış bekleyen iş kalmadı, tek açık kapı playtest. Tutorial ise 2 aydır dokunulmamış: sahne içeriği neredeyse boş (tek statik FBX harita + 9 küçük prefab, 430 GameObject'in çoğu ayarlar-menü UI kopyası), script'ler iki dağınık klasöre bölünmüş.

## Onaylanan Kapsam
1. Script mimarisi + adım akışı + level tümü kapsamda (sadece level değil).
2. İçerik: **yalnız çekirdek döngü**. Telefon/quest/oda-görünürlük tutorial'a girmez.
   - **Güncelleme (2026-09-02):** çekirdek döngünün tanımı oyunla birlikte değişti — artık müşteri etkileşimiyle başlıyor (bkz. Adım 6 içerik tablosu). Bu hâlâ "çekirdek döngü" kapsamında, telefon/quest/oda-görünürlük istisnası hâlâ geçerli.
3. İki script klasörü (`Assets/Tutorialassets/` + `Assets/NewCss/Tutorial/`) → tek klasörde (`Assets/NewCss/Tutorial/`) birleşir.

Detaylı gerekçe/riskler: plan onayı sırasında yazılan tam plan → `C:\Users\cicek\.claude\plans\imdi-senden-unu-istiyorum-robust-piglet.md` (yerel plan dosyası, repo dışı — referans için burada özetleniyor).

## Adımlar

### Adım 1 — gameplay: Klasör birleştirme (izole commit)
`git mv` ile guid korunarak taşı:
- `Assets/Tutorialassets/TutorialManager.cs(.meta)`
- `Assets/Tutorialassets/TutorialStep.cs(.meta)`
- `Assets/Tutorialassets/TutorialDoor.cs(.meta)`
- `Assets/Tutorialassets/TutorialGarageDoorController.cs(.meta)`
- `Assets/Tutorialassets/TutorialShelfState.cs(.meta)`
- `Assets/Tutorialassets/TutorialTruckSpawner.cs(.meta)`

→ hedef: `Assets/NewCss/Tutorial/`. Dokunulmayacak: `TutorialPlayerSpawner.cs`, `TutorialTruck.cs`, `TutorialTruckTrigger.cs` (zaten orada), `Truck_Anim (2) 1.prefab`. Bu commit SADECE dosya taşıma içerir, başka kod değişikliği yok.

### Adım 2 — gameplay: Sadeleştirme (ayrı commit)
- `TutorialStep.cs`: `requiredBoxType` alanına netleştirici tooltip (NetworkedShelf.BoxType vs BoxInfo.BoxType karışıklığı uyarısı).
- `TutorialManager.cs`: kapsam dışı condition tiplerinin (CompleteMinigame, Custom) switch-case'inde "desteklenmiyor" notu.
- Çekirdek döngü adım iskeleti (~8 adım, plan dosyasındaki sıra) için varsayılan öneri — nihai doldurma kullanıcıda (Inspector).

### Adım 3 — graphics-ui (Adım 1-2 ile paralel, bağımsız)
Highlight/outline materyali, skip-hint UI cilası — varsa görsel kırıklık giderilir.

### Adım 4 — qa
Guid kırılmadı mı (grep), BoxType kıyaslamaları hâlâ enum-to-enum mı, RPC server-only guard'ları korundu mu.

### Adım 5 — kontrol
Dal-sonu tek toplu ONAY kapısı, en fazla 3 tur.

### Adım 6 — kullanıcı (manuel, Editor)
- **Güncelleme (2026-09-01):** kullanıcı sahneden eski ışık + statik NPC/manken'i sildi. "Yeni obje yok" notu artık geçersiz — ışık için graphics-ui'a ayrı görev verildi (bkz. Adım 6b, tamamlandı).
- **Güncelleme (2026-09-02) — DÜZELTME:** silinen "karakter" manken değil, gerçek **Customer NPC**'siymiş (tag `Customer`, tam rig + `CustomerAI.cs`, `BoxAttachRig`). Kullanıcı ayrıca sahnedeki item'ları (masa/raf/kutu) da sildiğini belirtti — kod incelemesiyle tam obje listesi ve gerçek oynanış akışı çıkarıldı (aşağıya bak). Eski 9 adımlık taslak **geçersiz**, yerine 12 adımlık gerçek-akış taslağı geçti.
- ✅ **Sahne objeleri + kod bypass'ları tamamlandı (2026-09-02, bkz. altındaki blok)** — müdür bunu kendi yaptı (kullanıcı "sen yap" dedi), gameplay+qa+kontrol (2 tur) akışından geçti, kontrol ONAY verdi.
- **Güncelleme (2026-09-09, 2):** kullanıcı "bu değişiklikleri sen yapamaz mısın" dedi — Unity açıkken müdür Editor'e doğrudan komut gönderemez (aynı projeye ikinci Unity process'i giremez), bu yüzden `TutorialSceneSetup.cs`'e yeni bir menü komutu eklendi: **`Tools/Cargor/Tutorial/Setup Tutorial Steps`**. Bu komut 10 adımın TR/EN metnini, koşul tiplerini, `requiredKey`/`requiredBoxType`/`waitDuration`/`requiredDeliveryCount`/`requiredTruckBoxType` alanlarını VE her adımın `objectToHighlight`'ını (ilgili sahne objesine otomatik) yazıp sahneyi kaydediyor.
- ✅ **Güncelleme (2026-09-09, 3) — İÇERİK YAZIMI TAMAMLANDI.** Kullanıcı Unity'yi kapatınca müdür komutu kendi headless (`-executeMethod TutorialSceneSetup.SetupSteps -quit`) çalıştırdı. İlk koşumda bulgu: Unity array büyürken yeni elemanları son elemandan **kopyalıyor** (fresh default değil) — `requiresItemPickup`/`triggerTag`/`requiredItemName` gibi set edilmeyen alanlarda kalıntı veri kalmıştı (işlevsel zararı yok ama kirli). Kod düzeltilip (bu 3 alan artık açıkça sıfırlanıyor) ikinci kez koşuldu, temiz çıktı. **Doğrulama:** `Tutorial.unity` diff'i yalnız 10 adımın verisini içeriyor, 0 `error CS`, EditMode **79/79** geçti. Inspector'a elle veri girme ihtiyacı kalmadı.
- Kapı1(adım-index 2)/Kapı2(adım-index 5)'in fiziksel "oda" varsayımı artık geçerli değil (adım sayısı değişti) — haritada kapı konumlarını yeni 10 adımlık sıraya göre gözden geçir (**hâlâ açık, kullanıcı Editor işi**)
- `Tutorial.unity` açıp Console'da Missing Script kontrolü (**hâlâ açık, kullanıcı Editor işi**)

**Gerçek oynanış akışı (kod doğrulamalı, 2026-09-02):** müşteri gelir → E ile etkileşim → siparişini **DisplayTable**'a (sipariş masası) bırakır → oyuncu ürünü alır, **ayrı bir paketleme masasına** (`Table.cs`) taşır, bırakır → rafta boş kutu alır, paketleme masasına götürür → E'ye basınca kutu+ürün **otomatik paketlenir** (kod içi not: "Anında paketleme - minigame YOK"; kutu tipi ürünle eşleşmeli: Toy→Red, Clothing→Yellow, Glass→Blue) → paketlenmiş kutuyu masadan alıp **aynı rafa** geri koyar → tır gelince raftan paketi tekrar alıp teslim eder.

**Sahneye eklenen objeler ve kod değişiklikleri (2026-09-02, gameplay+qa+kontrol, 2 tur):**
| Obje/Değişiklik | Kaynak/Dosya | Not |
|---|---|---|
| TutorialCustomer | `Customer.prefab` instance | `CustomerAI.dropOffTable` → TutorialDisplayTable'a bağlı |
| TutorialDisplayTable | `Cube.006 (8).prefab` instance | `slotPoints` = 2 yeni child Transform |
| TutorialPackingTable | `Cube.012 (3).prefab` instance | `tableID="301"`, DisplayTable'dan **ayrı** obje |
| TutorialShelf | primitive Cube + `TutorialShelfState` | `shelfSlots`=3 child, `acceptedBoxType=Red`, `requireSpecificBoxType=1` |
| **Kod: Customer Service-state bypass** | `TutorialManager.cs` (+53 satır) | `CustomerManager` sahnede yok — yeni coroutine server'da `AssignServiceStation()`'ı doğrudan çağırıyor (round 1'de kontrol bunu kritik bulgu olarak yakaladı, round 2'de düzeltildi) |
| **Kod: raf otomatik stoklama** | `TutorialShelfState.cs` (+94/-3 satır) | Production `Shelf.cs` gibi kendini stoklamıyordu — `initialStockItemData`(=RedBox.asset)/`initialStockCount` + `SpawnInitialStock()` eklendi (round 1 kritik bulgu, round 2'de düzeltildi) |
| Kurulum aracı | `Assets/Editor/TutorialSceneSetup.cs` | Batchmode'da tekrar çalıştırılabilir, idempotent (repoda bırakıldı) |
| Tır | zaten sahnede (`TutorialTruck`) | dokunulmadı |

**Kapanmamış küçük notlar (kontrol ONAY verdi, blocker değil, ileride akılda tutulsun):**
- `TutorialShelfState.SpawnInitialStock`: `stockCount` yalnız `shelfSlots.Length`'e clamp'leniyor, `maxItemCount`'a değil — şu anki `1/1` config'de sorun yok, ileride biri bu değerleri ayırırsa sessiz taşma riski.
- Customer bypass coroutine'i `NetworkManager.Singleton` yoksa sessizce çıkıyor (log yok) — normal sahne-geçişli akışta sorun değil, Tutorial.unity doğrudan Play'lenirse teşhisi zorlaşır.

**Güncelleme (2026-09-09) — İÇERİK SIFIRDAN YENİDEN YAZILDI, 12→10 adıma indi.** Kullanıcı Inspector'a elle girmeye başlarken akışı kendi cümleleriyle yeniden tarif etti; müdür kodu (`Table.cs`, `TutorialStep.cs`, `TutorialManager.IsStepConditionMet()`) tekrar okuyup **iki gerçek bug buldu**: `TutorialConditionType.PressKey` ve `EnterTrigger` `IsValid()`'de doğrulanıyordu ama `IsStepConditionMet()`'in switch'inde **hiç case'i yoktu** — bu tipteki bir adım hiçbir zaman otomatik tamamlanmazdı (eski 12 adımlık taslağın 0. ve 1. adımları fiilen çalışmayacaktı, hiç playtest edilmediği için yakalanmamıştı). `PressKey` `TutorialManager.cs`'de düzeltildi (Update()'te frame-doğru `_pressKeyDetected` bayrağı, typewriter'ı önce bitirir sonra adımı tamamlar — skip mantığıyla tutarlı). `EnterTrigger` düzeltilmedi, kapsam dışı bırakıldı: eski adım 1'in ("yürü") işlevi bir sonraki eyleme gömüldü. Ayrıca eski adım 3'ün "PickupItem" koşul ismi yanlıştı — `Table.cs:NotifyTutorialManager` gerçekte `TakeFromTable`/`PlaceOnTable` çağırıyor, `PickupItem` farklı bir mekanik (dünyadan doğrudan item alma, `PlayerInventory.HasItem`).

**Öğretim sırası içerik taslağı (10 adım, index 0-9, kod-doğrulamalı çalışan koşullarla):**

| # | Koşul | TR | EN | Ekstra alanlar |
|---|---|---|---|---|
| 0 | PressKey | Cargor'a hoş geldin! Eşyaları müşterilerden alıp araçlara teslim edeceksin. Devam etmek için [SPACE]'e bas. | Welcome to Cargor! You'll pick up items from customers and deliver them to trucks. Press [SPACE] to continue. | `requiredKey=Space` |
| 1 | TakeFromTable | Müşteriye gidip E'ye basarak siparişini al, sonra masanın önünde tekrar E'ye basarak ürünü al. | Walk up to the customer and press E, then press E again at the table to pick up the item. | — |
| 2 | PlaceOnTable | Ürünü paketleme masasına götür ve önünde E'ye basarak bırak. | Carry the item to the packing table and press E to place it down. | — |
| 3 | TakeFromShelf | Raftan uygun kutuyu almak için önünde E'ye bas. | Press E at the shelf to take the right box. | `requiresSpecificBoxType=✓`, `requiredBoxType=Red` |
| 4 | PlaceOnTable | Kutuyu paketleme masasına götür, E'ye bas — ürün otomatik paketlenecek. | Bring the box to the packing table and press E — the item will be packed automatically. | — |
| 5 | TakeFromTable | Paketlenmiş kutuyu almak için masanın önünde tekrar E'ye bas. | Press E again at the table to pick up the packed box. | — |
| 6 | PlaceOnShelf | Kutuyu rafa yerleştirmek için E'ye bas. | Press E to place the box on the shelf. | — |
| 7 | WaitForTime | Araç geliyor, biraz bekle... | The truck is arriving, hold on... | `waitDuration=2-3` |
| 8 | TakeFromShelf | Paketi tekrar almak için rafın önünde E'ye bas. | Press E at the shelf again to take the package. | `requiresSpecificBoxType=✓`, `requiredBoxType=Red` |
| 9 | DeliverToTruck | Paketi tırın arkasına götür ve fırlat. | Carry the package to the back of the truck and throw it in. | `requiredDeliveryCount=1`, `requiresSpecificBoxTypeForTruck=✓`, `requiredTruckBoxType=Red` |

Liste index 9'da bitince `CompleteTutorial()` otomatik tetiklenir ("Tutorial tamamlandı!" mesajı) — ayrı bir kapanış adımına gerek yok. ⚠️ Adım 3/8'deki `requiredBoxType` `NetworkedShelf.BoxType` (Red=0), adım 9'daki `requiredTruckBoxType` `BoxInfo.BoxType` (Red=2) — **farklı enum sırası, isimden seç, sayı kopyalama.**

Eski 12/13 adımlık taslak (2026-09-02) bu tabloyla **değiştirildi**, artık geçersiz.

### Adım 6b — graphics-ui: Işık kurulumu (yeni, 2026-09-01) ✅ TAMAMLANDI
Tutorial.unity'e ana sahne (`The Main Office`) ile tutarlı directional light + URP volume eklendi (fileID 990010001-990010007). Müdür diff'i grep ile doğruladı (temiz ekleme, mevcut objelere dokunmamış), kullanıcı Editor'de görsel olarak onayladı. Kontrol kapısı gerekmedi (kozmetik/küçük iş).

### Adım 7 — playtest
`plans/playtest-checklist.md`'ye T-serisi (T1-T9) eklenecek.

## Kapsam Dışı (bilinçli)
- `TutorialShelfState.TakeItemFromShelfServerRpc` client-spoof borcu (`plans/release-push.md` G7) — ayrı iş.
- WASD client bug'ı — gözlemlenir, çözülmeye çalışılmaz.
- Telefon/quest/oda-görünürlük öğretimi — organik öğrenmeye bırakılır.

## Durum
- [x] Adım 1 — klasör birleştirme (`af65bf1`, 6 script + meta, guid doğrulandı, 0 CS)
- [x] Adım 2 — sadeleştirme (`d234880`, BoxType tooltip + kapsam dışı condition notu)
- [x] Adım 3 — graphics-ui cila (`5dbae9e`, stale `_currentStep` sonrası tutorial-bitti bug'ı düzeltildi — CompleteTutorial'da null atanmıyordu, skip/dil-değiştirme ile tekrar tetiklenebiliyordu)
- [x] Adım 4 — qa (temiz PASS, 1 bloklayıcı-olmayan not: `DebugCompleteTutorial()` context-menu debug aracı, dal-öncesi zaten var olan davranış)
- [x] Adım 5 — kontrol ONAY (bağımsız doğrulandı: guid, content-preserving taşıma, `_currentStep=null` fix, kapsam taşması yok, BoxType tooltip doğruluğu, headless derleme, departman ayrımı — bulgu yok)
- [x] Adım 6b — graphics-ui ışık kurulumu (Main Office referanslı, kullanıcı görsel onayladı, 2026-09-02)
- [x] Adım 6 (sahne objeleri + kod) — gameplay 2 tur + qa 2 tur + kontrol ONAY (2026-09-02): Customer/DisplayTable/PackingTable/Shelf eklendi, Customer Service-state bypass + raf otomatik-stoklama kodu yazıldı, headless derleme temiz
- [x] Adım 6 (10 adımlık içerik + highlight wiring) — müdür `TutorialSceneSetup.SetupSteps` ile headless yazdı (2026-09-09), 0 CS, EditMode 79/79, kontrol kapısı koşulmadı (KÜÇÜK/içerik işi sayıldı — ekonomik değer yok, kritik sisteme dokunmuyor)
- [x] Adım 6 kalan: Missing Script kontrolü temiz; kapı1/kapı2 index'leri düzeltildi (garaj kapısı 7/8→6/9, TutorialDoor "Kapı1" 2→1) — detay `plans/devam.md` 2026-09-11
- [x] Adım 6 (dinamik müşteri spawn) — statik önceden-yerleştirilmiş Customer yerine `TutorialCustomerManager.cs` (adım 1'de spawn) getirildi, spawn/masaya-bakma/NavMesh düzeltmeleri dahil
- [x] Adım 7 — playtest UÇTAN UCA BAŞARILI (2026-09-11, kullanıcı: "tutorial ı tamamen bitirdim bi sorunla karşılaşmadan harika"). Toplam 3 playtest turunda bulunan bug'lar: 10 gerçek bug (`7bf5f2a`, commit'li) + 2. turda 3 bug daha (rastgele ürün rengi, kutu (0,0,0)'a ışınlanma, paketleme sonrası kapı açılmama — commit'siz) + ESC menü (panel başta görünüyordu, ESC çalışmıyordu — `closeButton` referansı boştu, script `ValidateReferences()` başarısız olup kendini kapatıyordu). Headless doğrulama: 0 CS, EditMode **79/79**. Kontrol kapısı sırada.
