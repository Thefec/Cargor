# Tutorial — Bantlama Masası + 2 Yeni Adım

> Durum: UYGULANDI (2026-09-21), kontrol ONAY 2. tur, commit yok, playtest bekliyor. Dal: `fix/playtest-feedback-ui-interaction` üstünde devam (veya ayrı dal — kullanıcı kararı).

## Neden
Tutorial şu an **5. adımda tıkanıyor**: `PerformInstantBoxing` kutuyu "Open" spawn ediyor (`Table.cs:853`), açık+dolu kutu `IsAwaitingSeal()` → `CanTakeItem=false` (`Table.cs:125, 925-932`); bantlamak için elde `TapeInfo`'lu item lazım (`:604, 618-622`) ama `Tutorial.unity`'de bant kaynağı YOK. TutorialTruck da sadece `isFull` kutu kabul ediyor (`TutorialTruck.cs:349`). Ayrıca `TutorialPackItem` metni "ürün otomatik paketlenecek" diyor — yanlış.

## Kullanıcı kararları
- 2 yeni adım: **TakeTape** + **SealBox** (PackItem'dan sonra).
- Metinler **17 dilin hepsinde**.

## Adımlar

### A. Bant istasyonu (Tutorial.unity) — graphics-ui veya müdür (Unity MCP ile)
Main Office `BT` (`The Main Office.unity:34617`) prefab değil; aynı yapıyı Tutorial'da sahne kökünde kur:
- Kök `TapeStation`: MeshFilter+MeshRenderer (Office_Acc mesh `{fileID: -5741174448488666723, guid: c1794ec238bf7344c956fdbcf73223ce}` + BT'nin materyali), BoxCollider (BT'den kopya), layer 8, tag `Table`, **NetworkObject (yeni benzersiz GlobalObjectIdHash — editör üretsin, elle YAML'da mevcut 11 hash ve 2334558369/1518149970 kullanılmaz)**, `ItemDispenser` (itemData=`Assets/Resources/Items/DuctTape.asset`, respawnDelay 1, autoRespawn 1).
- Child `TapePoint` (NetworkObject YOK), masa üstünden ~0.3 m yukarıda, dünya rotasyonu dik (bant `itemSlot.rotation` ile spawn olur, `ItemDispenser.cs:260`).
- Konum: `TutorialPackingTable` (-45.92, 2.4, -16.62) yanında, ~2 m yana; rotasyon/scale paketleme masasıyla uyumlu. **Kullanıcı Editor'da görsel olarak son konumu ayarlar.**
- DuctTapeNGO zaten `DefaultNetworkPrefabs.asset:273`'te → NetworkPrefabs'a ekleme gerekmez.
- Uygulama yolu: Unity MCP (`manage_gameobject`/`manage_components`), Tutorial sahnesi açılıp kaydedilir. TMP okuma tuzağı burada yok ama sahne kaydından sonra `git diff` ile beklenmeyen fileID düşüşü taranır.

### B. SealBox bildirimi (kod) — gameplay
- `TutorialConditionType`'a `SealBox` ekle (`TutorialStep.cs:8-47`) — enum SONUNA ekle (serialize int değerleri kaymasın).
- `TutorialManager.OnBoxSealed()` (mevcut `OnTableInteraction` desenine göre, `TutorialManager.cs:968-981`).
- `Table.PerformSealing` (`Table.cs:943-979`) başarı yolunda server'da `TutorialManager.Instance?.OnBoxSealed()` çağır (mevcut `NotifyTutorialManager` deseniyle, `:1156`). Tutorial host'ta koşuyor (`TutorialPlayerSpawner.cs:73`); Main Office'te `TutorialManager.Instance` null → etkisiz.

### C. Adım listesi (Tutorial.unity `tutorialSteps` + `Assets/Editor/TutorialSceneSetup.cs:337-358` StepDef) — gameplay
Index 4 (PackItem) sonrasına ekle:
- 5 **TakeTape**: `PickupItem`, `requiresItemPickup=1`, `requiredItemName="DuctTape"`, highlight=TapeStation.
- 6 **SealBox**: `SealBox`, highlight=TutorialPackingTable.
- Eski 5-9 → 7-11.
- **İndeks bağımlılıkları kayar:** `TutorialGarageDoorController` `openOnStepIndex 6→8`, `closeOnStepIndex 9→11` (`Tutorial.unity:20907-20908`). Diğer `*StepIndex` alanları grep'lenecek (TutorialTruckSpawner, TutorialCustomerManager `spawnOnStepIndex:1` etkilenmez).
- StepDef dizisi de güncellenir ki setup yeniden koşulunca adımlar silinmesin.

### D. Metinler (17 dil) — gameplay/assistant
- Yeni anahtarlar `TutorialTakeTape`, `TutorialSealBox` (+ varsa başlık anahtarları, mevcut adımların anahtar desenine uy) → `Assets/Editor/TutorialLocalizationSetup.cs` + `StringTable Shared Data.asset` + 17 `StringTable_*.asset`.
- `TutorialPackItem` düzelt: "otomatik paketlenecek" → kutu açık gelir, bantla kapatılır.
- `TutorialTakePackedBox` gerekirse "bantlanmış kutuyu al" diye güncelle.
- Font: TR dışı Latin diller SpaceGrotesk TR kapsamında (U+0000-017F); Kiril/CJK dil yok.
- Unity'nin StringTable satır-kaydırma yeniden serileştirmesi commit'e karışmasın.

### E. Kapı
Ekonomik değer yok → economist yok. gameplay (B+C+D) → qa → kontrol ONAY. A sahne işi, müdür/graphics-ui.

## Doğrulama
- Unity derleme 0 hata (MCP refresh + read_console).
- Playtest: tutorial baştan sona — paketle → bant al (adım ilerler) → bantla (adım ilerler) → kutuyu al → rafa koy → tır; garaj kapısı doğru adımda açılıp kapanıyor; bant alındıktan 1 sn sonra yenisi geliyor; TR + EN + bir 3. dil metinleri.
- Main Office'te bantlama regresyonu yok (TutorialManager yok → null-safe).
