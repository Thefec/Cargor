# Açık Kutu Modelleri + Bantlı Paketleme Sistemi

## Context

Şu anda paketleme "anında" oluyor: `Table.PerformInstantBoxing()` (Assets/NewCss/PickUpScripts/Table.cs:781-833) oyuncunun elindeki boş kutuyu masadaki ürünle (renk eşleşmesi ile) doğrudan birleştirip **tek adımda kapalı ("...Full") kutu** üretiyor — ara görsel yok, minigame yok (kod yorumunda da böyle belirtilmiş).

Kullanıcı, oyuna görsel/dokunsal bir ara adım eklemek istiyor: ürün+kutu eşleştikten sonra masada **açık kutu** görünsün (kapalı kutu değil), oyuncu ayrı bir masadan **bant** alıp getirsin, bantlayınca kutu kapansın. Bu, mevcut ürün-kutu eşleştirme mantığını DEĞİŞTİRMİYOR — sadece sonucu iki aşamaya bölüyor: **açık+dolu** (ara, masada bekleyen) → **kapalı+dolu** (mevcut "...Full", bantlanınca).

Kullanıcının netleştirdiği noktalar (AskUserQuestion ile teyit edildi):
- Ürün eşleştirme mantığı **aynen kalıyor** ("...buraya kadar şuanki sistemle aynı" — kullanıcının kendi ifadesi). Sadece PerformInstantBoxing'in ÇIKTISI değişiyor.
- Eski sistem **genişletilecek**, ayrı/paralel bir sistem olmayacak.
- Bant **tükeniyor**, masada stok var, süreyle otomatik yeniden doluyor (NetworkedShelf'teki respawn deseniyle tutarlı).

## Durum Modeli (netleştirilen mimari)

Renk başına sadece **2 görsel state** var (kullanıcının kendi eşleştirmesiyle birebir): **açık** ve **kapalı**. Üçüncü bir "açık+dolu" prefabına GEREK YOK — çünkü içindeki ürün zaten görünmüyor ("içindeki item görünmez" — kullanıcının kendi ifadesi), yani açık kutunun görseli boşken de doluyken de identik. "Doldu mu, bantlanmayı mı bekliyor" bilgisi **masanın kendi `_tableState.isItemBoxed` NetworkVariable'ında** tutuluyor (bu alan zaten var, `Table.cs:56-57,99,1140-1150`) — item'ın kendi `BoxInfo.isFull` alanı sadece gerçek kapalı/mühürlü state için ayrılmış kalıyor (mevcut kullanımıyla tutarlı).

Bu sayede:
- Rafta duran boş açık kutu ile masada ürünle eşleşip bekleyen "dolu ama açık" kutu **aynı ItemData/prefab** — tek yeni item seti (renk başına 1) yeterli. Rafta/taşınırken (henüz masaya konup ürünle eşleşmeden önce) hâlâ kırılabilir (mevcut boş kutu davranışıyla birebir, `BoxDestroyOnCollisionNetcode`/`BoxFallPenalty` dahil). **Masaya konup `isItemBoxed=true` olduktan sonra ise artık masadan hiçbir yoldan alınamıyor** (bkz. bölüm 4, `CanTakeItem`/pickup guard'ları), dolayısıyla "bantlanmadan taşırken düşürüp kırma" senaryosu mümkün değil — kutu bantlanana kadar masada sabit kalır.
- `PerformInstantBoxing` artık "...Full" yerine bu yeni **açık kutu** ItemData'sını masaya spawn edecek (tek satırlık kaynak değişimi + `isItemBoxed=true`).
- Yeni bir `PerformSealing` metodu (tape ile tetiklenen) mevcut `GetBoxedProductData`/`SpawnBoxedProductCoroutine` desenini AYNEN kullanarak açık kutuyu despawn edip mevcut "...Full" kapalı kutuyu spawn edecek.

## 1. Yeni item'lar

**Açık kutular (Y/B/R)** — `Assets/Models/Y_Open_Box.fbx`, `B_Open_Box.fbx`, `R_Open_Box.fbx` (daha önce içeri aktarıldı).
- Yeni ItemData: `RedBoxOpen`, `BlueBoxOpen`, `YellowBoxOpen` (`Resources/Items/`), mevcut `RedBox`/`BlueBox`/`YellowBox` ile aynı alan yapısı (`itemCategory=Box`, `throwForce=10`), yeni benzersiz `itemID` (mevcut en yüksek id 15'in üstünden devam).
- Yeni prefab çiftleri: `Normal/RedOpen.prefab`+`NGO/RedOpenNGO.prefab` (ve Blue/Yellow eşleniği), **`NGO/RedNGO.prefab`'ın birebir kopyası** (BoxInfo isFull=false, Outline, NetworkObject, NetworkTransform, NetworkWorldItem, BoxDestroyOnCollisionNetcode, ItemFreezeSystem, AudioSource, BoxFallPenalty, RoomItemVisibility) — sadece MeshFilter/MeshRenderer/MeshCollider yeni FBX mesh'ine işaret edecek.
- Referans prefab: `Assets/ithappy/Creative_Characters_FREE/Saved_Characters/NGO/RedNGO.prefab` (component listesi ve ayarları burada — `unity-mcp-skill` ile birebir kopyalanacak, satır satır elle yazılmayacak).
- **MeshCollider doğrulaması**: yeni FBX'lerden gelen mesh'ler kopyalanan `MeshCollider`'a atanınca `convex` ayarının açık kaldığı (mevcut RedNGO ile aynı) tek tek doğrulanacak — Unity import'ta bazen bu ayar mesh değişince sıfırlanabiliyor.
- **`BoxInfo.boxType` doğrulaması (kullanıcı talebi)**: `RedNGO.prefab` kopyalanarak `BlueOpenNGO`/`YellowOpenNGO` üretilirken **`BoxInfo.boxType` alanı Red'de kalabilir** (kopyala-yapıştır hatası) — her üç prefab (`RedOpenNGO`, `BlueOpenNGO`, `YellowOpenNGO`) oluşturulduktan SONRA `BoxInfo.boxType` değerinin sırasıyla Red/Blue/Yellow olduğu tek tek okunup teyit edilecek. Aynı kontrol **Normal (visualPrefab) varyantları için de** yapılacak (`RedOpen`, `BlueOpen`, `YellowOpen`) — `BoxInfo` her ikisinde de (visual+world) olduğu için (bkz. yukarıdaki "Ortak" listesi) iki kopyası da ayrı ayrı doğrulanmalı.
- Eski `RedBox`/`BlueBox`/`YellowBox` ItemData/prefab'ları **silinmeyecek**, sadece artık raf tarafından referans alınmayacaklar (temizlik ayrı, isteğe bağlı bir iş).

**Bant (`DuctTape`)** — `Assets/Models/Duct_Tape.fbx` (daha önce içeri aktarıldı).

- **`ItemCategory` enum'u** (`Assets/NewCss/NewPickup/ItemCategory.cs`, şu an `{ Box, Product }`): yeni bir `Tool` değeri **enum'un EN SONUNA** eklenecek (`{ Box, Product, Tool }`) — mevcut asset'lerdeki serialize edilmiş `Box=0`/`Product=1` değerleri kaymasın diye araya eklenmeyecek. Bu enum'un switch/if-else ile dallandığı her yer (ör. `ShelfState.ValidateItemIsBox`) grep'lenip yeni `Tool` değerinin sessizce yanlış dala düşmediği QA aşamasında tek tek doğrulanacak.
- **`TapeInfo` marker component** (yeni dosya: `Assets/NewCss/InfoScripts/TapeInfo.cs`) — `BoxInfo`/`ProductInfo` ile birebir aynı minimal kalıp (sadece `MonoBehaviour`, alan gerekmiyor, tek amacı `GetComponent<TapeInfo>()` ile "bu item bant" tespiti):
  ```csharp
  namespace NewCss
  {
      public class TapeInfo : MonoBehaviour { }
  }
  ```
  Table.cs bunu `playerItemData.visualPrefab.GetComponent<TapeInfo>() != null` şeklinde kontrol edecek (`BoxInfo` kontrolüyle birebir aynı çağrı deseni, `ValidateBoxingRequest` satır 856) — yani **`visualPrefab`'ın kendisinde** bulunması zorunlu (kontrol bu alan üzerinden yapılıyor). Kanıt: mevcut `BoxInfo`, hem `Normal/Red.prefab` (visualPrefab) hem `NGO/RedNGO.prefab` (worldPrefab) üzerinde **ikisinde de** duruyor (Explore raporu: "Ortak (tüm varyantlarda): ... BoxInfo ... Outline"). Aynı desen `TapeInfo` için de uygulanacak: **hem `Normal/DuctTape.prefab` hem `NGO/DuctTapeNGO.prefab`'a eklenecek** — visualPrefab'da olmazsa Table.cs'in kontrolü asla true dönmez, worldPrefab'da olmazsa gelecekte worldItem tarafı (ör. bir grep/kontrol) tutarsız kalır.
- **Yeni ItemData `DuctTape`** (`Resources/Items/DuctTape.asset`) — `itemCategory = Tool`, `itemID` = yeni benzersiz değer, `throwForce` mevcut item'larla tutarlı (economist önerisiyle teyit edilecek).
- **Yeni prefab çifti**: `Normal/DuctTape.prefab` (elde tutulan görsel — NetworkObject YOK, sadece mesh/render/collider, `Normal/Red.prefab` deseni) + `NGO/DuctTapeNGO.prefab` (dünya objesi). `NGO/DuctTapeNGO.prefab` component listesi **`RedNGOFull.prefab`'ın alt kümesine** dayanır (kutu değil, kırılma/ekonomi cezası uygulanmaz — `BoxDestroyOnCollisionNetcode` ve `BoxFallPenalty` **dahil edilmeyecek**):
  1. `TapeInfo` (BoxInfo yerine)
  2. `Outline`
  3. `NetworkObject` (Ownership, SynchronizeTransform, SpawnWithObservers — RedNGO ile birebir aynı ayarlar)
  4. `NetworkTransform` (RedNGO ile birebir aynı ayarlar — scale sync kapalı, world space, aynı threshold'lar)
  5. `NetworkWorldItem` (`itemData` → `DuctTape.asset`, `rb`, `itemCollider`, `impactSpeedThreshold` mevcut değerle aynı)
  6. `ItemFreezeSystem`
  7. `AudioSource`
  8. `RoomItemVisibility` (linker script ile otomatik eklenebilir de, elle de eklenebilir — bkz. bölüm 6)
  9. `Rigidbody` (mevcut örneklerle aynı `useGravity`/`isKinematic` başlangıç değerleri)
  - Mesh kaynağı: `Assets/Models/Duct_Tape.fbx`; MeshCollider convex olacak (yukarıdaki doğrulama kuralı burada da geçerli).

## 2. Raf değişikliği

`Assets/NewCss/TableScripts/Shelf.cs` (`NetworkedShelf`) — `redBoxItemData`/`blueBoxItemData`/`yellowBoxItemData` Inspector alanları sahnede yeni `RedBoxOpen`/`BlueBoxOpen`/`YellowBoxOpen` ItemData asset'lerine yeniden bağlanacak (kod değişikliği YOK, sadece sahnedeki component referansı — `unity-mcp-skill` ile `manage_asset`/`manage_gameobject`). Spawn/respawn mantığı (`SpawnBoxAtSlot`, satır 387-445) hiç değişmiyor — sadece hangi `ItemData`'yı spawn ettiği değişiyor.

## 3. Bant dispenser'ı (yeni, küçük script)

`NetworkedShelf` 3 renge sabitlenmiş (Red/Blue/Yellow slot alanları donanımsal) — genelleştirmek mevcut, oyunun çekirdek akışındaki riskli bir refactor olur. Bunun yerine `NetworkedShelf`'in **tek-slotlu, jenerik bir versiyonu** yazılacak: `Assets/NewCss/TableScripts/ItemDispenser.cs` — tek `ItemData` (`DuctTape`), tek `Transform` spawn noktası, `respawnDelay` (economist'e danışılacak — NetworkedShelf'in `respawnDelay=1f` değeriyle tutarlı mı yoksa farklı mı olmalı), `NetworkVariable<ulong>` ile spawn edilen objeyi takip.

**Stok modeli (kullanıcı onayladı)**: her an masada **tek** bant bulunur. Oyuncu alınca slot boşalır, `respawnDelay` sonra otomatik yeniden spawn olur — `NetworkedShelf.SpawnBoxAtSlot`/`ConfigureSpawnedBox` (Shelf.cs:387-445) ile birebir aynı desen, sadece 3 renk yerine 1 jenerik slot. Stok sayacı/kısmi doluluk YOK.

**Tüketim tespiti**: Bant alındığında `PlayerInventory.RequestPickupServerRpc` zaten dünya NetworkObject'ini `DelayedDespawnAndUnlock` ile despawn ediyor (`PlayerInventory.Interaction.cs:511,523` — mevcut kutu alma akışıyla birebir aynı, NetworkedShelf bu despawn'ı bugün de doğru tespit edip kutuları respawn ediyor). Yani `ItemDispenser` NetworkedShelf'in kullandığı **aynı tespit deseninin** (spawn edilen objenin `SpawnedObjects`'ten düşmesini `NetworkVariable<ulong>` referansı üzerinden izleme) birebir kopyasını kullanırsa, banda özel ekstra bir despawn/tüketim koduna gerek YOK — implementasyon sırasında QA bunu NetworkedShelf ile karşılaştırarak doğrulayacak.

Kullanıcının kendi yerleştireceği bant masasına bu component eklenip `DuctTape` ItemData'sı ve spawn noktası atanacak.

## 4. Table.cs genişletmesi

- **`GetBoxedProductData` genişletilecek, kopyalanmayacak** (satır 998-1014): metoda `string suffix = "Full"` parametresi eklenir, `itemName` üretimi `$"{RenkAdı}Box{suffix}"` şeklinde değişir (switch aynı kalır, sadece sabit `"Full"` yerine parametre kullanılır). Mevcut tüm çağrı yerleri (`SpawnBoxedProductCoroutine` içindeki çağrı) parametresiz kullanıldığı için davranışları değişmez.
- **`SpawnBoxedProductCoroutine`** (satır 962-992) da aynı şekilde `string suffix = "Full"` parametresi alacak ve `GetBoxedProductData(boxType, suffix)` çağıracak — yeni bir coroutine kopyalanmayacak, tek metot iki amaca hizmet edecek.
- `PerformInstantBoxing` (satır 781-833): `StartCoroutine(SpawnBoxedProductCoroutine(playerBox.boxType))` çağrısı `StartCoroutine(SpawnBoxedProductCoroutine(playerBox.boxType, "Open"))` olarak değişir — yani artık `"{Renk}BoxFull"` yerine `"{Renk}BoxOpen"` (bölüm 1'de oluşturulan yeni ItemData'lar) yüklenip masaya spawn edilir. `SetTableState(false, netObj.NetworkObjectId, true)` aynen kalıyor (`isItemBoxed=true` artık "açık ama dolu, bantlanmayı bekliyor" anlamına geliyor).
- `ValidateBoxingRequest` (satır 835-878): `if (state.isItemBoxed) return false;` guard'ı aynen kalıyor (spam-click / zaten dolu koruması) — davranış değişmiyor.
- **Yeni helper `IsAwaitingSeal()`**: masadaki objenin `isItemBoxed==true` VE üzerindeki `BoxInfo.isFull==false` olup olmadığını kontrol eder (mevcut `TryGetTableItem`, satır 752-771, aynen kullanılır):
  ```csharp
  private bool IsAwaitingSeal()
  {
      var state = _tableState.Value;
      if (!state.isItemBoxed) return false;
      if (!TryGetTableItem(state.itemNetworkId, out _, out NetworkWorldItem worldItem)) return false;
      var boxInfo = worldItem.GetComponent<BoxInfo>();
      return boxInfo != null && !boxInfo.isFull;
  }
  ```
- **Yeni server-only re-entrancy kilidi**: `private bool _sealingInProgress;` (satır 72 civarına, `_placementInProgress` ile yan yana).
- **Kalıcı kilitlenmeye karşı ek sıfırlama noktaları (kullanıcı talebi)** — `_sealingInProgress` yalnızca `PerformSealing`'in kendi erken-dönüşlerinde değil, masa objesinin/state'inin YOK OLDUĞU/sıfırlandığı her yerde de `false`'a çekilecek, aksi halde masa despawn olup yeniden spawn olursa (veya sahne geçişinde) kilit kalıcı takılı kalabilir:
  - `OnNetworkDespawn()` (satır 161-167): `UnsubscribeFromNetworkEvents(); UnregisterTable();` satırlarının yanına `_sealingInProgress = false;` eklenecek.
  - `InitializeTableState()` (satır 190-198, sunucu `OnNetworkSpawn`'da çağrılıyor): `_tableState.Value = new TableState {...}` atamasının yanına `_sealingInProgress = false;` eklenecek.
  - `SetTableState(...)` (satır 478-486): metodun içine koşulsuz `_sealingInProgress = false;` eklenmesi düşünülebilir ANCAK bu, `PerformSealing`'in kendi `SetTableState(false, netObj.NetworkObjectId, true)` çağrısıyla (coroutine içinde, satır 986 karşılığı) çakışıp kilidi ERKEN açabilir (sarma coroutine'i daha bitmeden). Bu yüzden `SetTableState`'e DOKUNULMAYACAK — sadece yukarıdaki iki nokta (despawn + initialize) yeterli, çünkü bunlar "masa sıfırdan başlıyor" anlamına geliyor, `SetTableState` ise normal akışın bir parçası.
- **`public bool CanTakeItem` güncellenecek** (satır 114): `CanTakeItem => HasItem && !IsAwaitingSeal() && !_sealingInProgress;` — açık+dolu (henüz bantlanmamış) kutu masadayken VEYA sarma işlemi tam o an sürerken (despawn→spawn arasındaki kısa boşluk, `IsAwaitingSeal()` bu sırada objenin despawn edilmiş olması nedeniyle yanlışlıkla `false` dönebilir — `_sealingInProgress` bu boşluğu da kapatıyor) oyuncu masadan item alamaz.
- **İkinci alım yolu doğrulandı**: masadaki item'lar Table.cs dışında, genel "yakındaki item'ı doğrudan al" sistemiyle de (`PlayerInventory.RequestPickupServerRpc`) alınabiliyor mu diye kontrol edildi. **Zaten güvenli**, kanıt:
  - `RequestPickupServerRpc` sunucu tarafında `worldItem.CanBePickedUp` NetworkVariable'ını kontrol ediyor, `false` ise pickup'ı reddediyor (`PlayerInventory.Interaction.cs:473` — server-authoritative, sadece client-side gizleme değil).
  - `Table.ConfigureWorldItem` (satır 693-711), masaya yerleştirilen HER item için `worldItemComponent.DisablePickup()` çağırıyor (satır 699) — bu, `SpawnBoxedProductCoroutine` içinde (satır 985) hem bugünkü Full-kutu spawnunda hem YENİ açık-kutu spawnunda **aynen** çalışıyor, değişiklik gerekmiyor.
  - Sonuç: masadaki açık+dolu kutu zaten `canBePickedUp=false` durumda olduğu için genel pickup yolundan alınamıyor; `CanTakeItem` guard'ı sadece Table'ın KENDİ E-interact yolunu kapatıyor, ikisi birlikte tam kapsıyor. QA bu iki yolu da (Table E-interact + doğrudan hedefleyip pickup tuşuna basma) elle test edip doğrulayacak.
- **`ValidateBoxingRequest` güncellenecek** (satır 835-878, madde 2): guard `if (state.isItemBoxed) return false;` → `if (state.isItemBoxed || _sealingInProgress) return false;` — sarma sürerken (teorik olarak zaten `isItemBoxed` bunu kapsıyor ama `_sealingInProgress` savunma-derinliği olarak eklendi) başka bir oyuncu kutu+ürün akışını tetikleyemez.
- **`ProcessPlayerHasItem` güncellenecek** (satır 594-607, madde 2 + dallanma + madde 4 re-entrancy + madde 6 uyarı) — **düzeltilmiş, tam if/else-if zinciri** (her koşul karşılıklı dışlayıcı ve açık, önceki taslaktaki "sarmaya hazır" dalının uyarı dalıyla karışabilecek belirsiz sırası giderildi):
  ```csharp
  private void ProcessPlayerHasItem(PlayerInventory player, ulong requesterClientId)
  {
      bool holdingTape = player.CurrentItemData?.visualPrefab?.GetComponent<TapeInfo>() != null;

      if (CanPlaceItem && holdingTape)
      {
          // Bant boş masaya doğrudan konulamaz
          LogDebug("⚠️ Bant boş masaya doğrudan konulamaz");
          NotifySealingNotReadyClientRpc(requesterClientId);
      }
      else if (CanPlaceItem && !_placementInProgress && !_sealingInProgress && !holdingTape)
      {
          LogDebug($"✅ Placing item from player {requesterClientId}");
          _placementInProgress = true;
          PlaceItemOnTable(player);
      }
      else if (holdingTape && IsAwaitingSeal() && !_sealingInProgress)
      {
          // Masa bantlamaya HAZIR: açık+dolu kutu bekliyor -> sarma işlemini başlat
          _sealingInProgress = true;
          PerformSealing(player, requesterClientId);
      }
      else if (holdingTape)
      {
          // Bant tutuluyor ama masa hazır değil (ham ürünle dolu, zaten sarılıyor,
          // veya zaten kapalı) - sessizce yutmak yerine kısa uyarı ver
          LogDebug("⚠️ Masa şu an bantlanmaya hazır değil");
          NotifySealingNotReadyClientRpc(requesterClientId);
      }
      else
      {
          // Bu dala SADECE !holdingTape iken düşülür (yukarıdaki üç dal da holdingTape
          // gerektiriyor) - PerformInstantBoxing bant tutan bir oyuncu için ASLA çağrılmaz.
          LogDebug($"📦 Attempting instant boxing for player {requesterClientId}");
          PerformInstantBoxing(player, requesterClientId);
      }
  }
  ```
  Yeni `NotifySealingNotReadyClientRpc(ulong targetClientId)` — mevcut `NotifyBoxingFailedClientRpc` (satır 894-901) ile birebir aynı minimal kalıp, sadece hedef client'a kısa bir log/UI uyarısı ("bant burada kullanılamaz") tetikler.
- **Madde 4 — çifte bantlama koruması + madde 1 — TÜM çıkış yollarında sıfırlama**: `_sealingInProgress`, `PerformSealing`'in HER erken-dönüş noktasında `false`'a çekilecek (aşağıya bakın), coroutine tarafında ise coroutine'i saran ince bir wrapper ile kapatılacak — böylece `SpawnBoxedProductCoroutine` içindeki mevcut erken-çıkışlar (ItemData bulunamadı, NetworkObject yok) dahil TÜM yollarda kilit açılır, masa kalıcı kilitlenmez.
- **`PerformSealing`** (yeni metod) — madde 3 sırasına göre **önce doğrula, sonra yık**:
  ```csharp
  private void PerformSealing(PlayerInventory player, ulong requesterClientId)
  {
      var state = _tableState.Value;

      if (!TryGetTableItem(state.itemNetworkId, out _, out NetworkWorldItem tableWorldItem))
      {
          _sealingInProgress = false;
          return;
      }

      var boxInfo = tableWorldItem.GetComponent<BoxInfo>();
      if (boxInfo == null || boxInfo.isFull)
      {
          _sealingInProgress = false;
          return;
      }

      // Madde 3: önce hedef "...Full" ItemData'sının gerçekten yüklendiğini doğrula,
      // ANCAK ondan sonra masadaki açık kutuyu yık ve oyuncunun bandını tüket.
      ItemData sealedBoxData = GetBoxedProductData(boxInfo.boxType, "Full");
      if (sealedBoxData == null || sealedBoxData.worldPrefab == null)
      {
          LogError($"Sealed box data not found for {boxInfo.boxType} - aborting seal");
          _sealingInProgress = false;
          return;
      }

      // Doğrulama geçti - artık tüketime/yıkıma geçilebilir
      player.SetInventoryStateServer(false, -1);
      player.TriggerDropAnimationServerRpc();

      DespawnCurrentTableItem();

      StartCoroutine(SealAndReleaseLockCoroutine(boxInfo.boxType));

      NotifyBoxPackedClientRpc(requesterClientId, (int)boxInfo.boxType);
  }

  private IEnumerator SealAndReleaseLockCoroutine(BoxInfo.BoxType boxType)
  {
      // SpawnBoxedProductCoroutine'in KENDİ içindeki tüm erken-çıkışları
      // (yield break dahil) bekler - coroutine hangi yoldan biterse bitsin
      // buradan sonrasına düşülür, kilit her durumda açılır.
      yield return StartCoroutine(SpawnBoxedProductCoroutine(boxType, "Full"));
      _sealingInProgress = false;
  }
  ```
  Not: bandın kendi NetworkObject'ini ayrıca despawn etmeye gerek yok — pickup anında zaten despawn edilmiş durumda (bkz. bölüm 3, "Tüketim tespiti").

## 5. Bant düşürme/fırlatma davranışı (madde 7 + madde 3 — kullanıcı talebi)

**Tek çıkış noktası bulundu ve doğrulandı**: `RequestDropServerRpc` (`PlayerInventory.Interaction.cs:535-549`), `RequestThrowServerRpc` (aynı dosya, satır 551-569) VE disconnect/forced-cleanup yolu (`PlayerInventory.cs:264-291`, `OnNetworkDespawn → TryReturnHeldItemToWorldOnDespawn`) **hepsi TEK bir paylaşılan metodu çağırıyor**: `SpawnWorldItemAtPosition(Vector3 position, Vector3 force)` (`PlayerInventory.Visual.cs:267-305`). `PlayerInventory.cs:259-263`'teki kod yorumu bunu doğruluyor: "OnClientDisconnected/CleanupExistingPlayers/CleanupAndDestroy gibi TÜM despawn çağrı noktalarını tek yerden kapsar" — yani bu metot zaten projedeki TEK "elden zorla çıkan item'ı dünyaya koy" noktası.

**Sonuç (madde 3'ü de otomatik çözer)**: değişiklik `RequestDropServerRpc`/`RequestThrowServerRpc`'ye değil, doğrudan `SpawnWorldItemAtPosition`'ın BAŞINA eklenecek — bu tek patch drop, throw VE disconnect/forced-despawn'ı aynı anda kapsar, üç ayrı yere dokunmaya gerek yok:
```csharp
private void SpawnWorldItemAtPosition(Vector3 position, Vector3 force)
{
    if (_currentItemData == null) return;

    // Bant hiçbir yoldan (drop/throw/disconnect) dünyaya geri konmaz - kaybolur.
    // Bu metot projedeki TEK "elden zorla çıkan item" noktası (bkz. plan bölüm 5),
    // dispenser zaten pickup anında kendi respawn sayacını başlatmıştı (bölüm 3).
    if (_currentItemData.visualPrefab != null &&
        _currentItemData.visualPrefab.GetComponent<TapeInfo>() != null)
    {
        return;
    }

    var worldItemPrefab = GetWorldItemPrefab(_currentItemData);
    // ... (metodun geri kalanı DEĞİŞMEDEN kalır)
}
```
Çağıranların (`RequestDropServerRpc` vb.) kendisi değişmiyor — onlar zaten `SpawnWorldItemAtPosition(...)` çağrısından SONRA `ClearInventoryState()`/animasyon RPC'lerini çalıştırıyor; `SpawnWorldItemAtPosition` sessizce hiçbir şey spawn etmeden dönünce envanter yine de doğru şekilde temizleniyor, sadece dünyada bant kopyası oluşmuyor. `TryReturnHeldItemToWorldOnDespawn` tarafında da ek bir işlem gerekmiyor (obje zaten despawn sürecinde).

**Kapsam dışı bırakılan yollar (QA doğrulayacak)**: `PlayerInventory.Shelf.cs` (satır ~517-544, ~660-683) ve tutorial-shelf yerleştirme (`PlayerInventory.Interaction.cs` satır ~330-374) kendi ayrı `Instantiate`+`Spawn` kodunu kullanıyor, `SpawnWorldItemAtPosition`'ı ÇAĞIRMIYOR — ama bunlar rafa YERLEŞTİRME akışları (kasıtlı "place" eylemi, "elden zorla çıkma" değil) ve `ShelfState`/`TutorialShelfState` yalnızca `ItemCategory.Box` kabul ediyor (`ValidateItemIsBox`). Bandın kategorisi `Tool` olacağı için bu yollara teorik olarak hiç giremiyor olması gerekiyor — QA bunu doğrulayıp (bant rafa konmaya çalışılınca reddedildiğini/hiç tetiklenmediğini teyit ederek) kapatacak.

## 6. Room visibility linker

Yeni prefabler (`RedOpenNGO`, `BlueOpenNGO`, `YellowOpenNGO`, `DuctTapeNGO`) oluşturulduktan sonra `Tools/Cargor/Rooms/Eşya Prefab'larına Oda Görünürlüğü Ekle` editor tool'u (`Assets/NewCss/Rooms/Editor/RoomItemVisibilityPrefabLinker.cs`) tekrar çalıştırılacak — `NetworkWorldItem` taşıyan her prefab'ı otomatik tarayıp `RoomItemVisibility` ekliyor, elle eklemeye gerek yok (yine de prefab RedNGO kopyalanırken zaten bu component dahil edilecek, linker sadece güvence/doğrulama amaçlı).

## Departman dağılımı

1. **economist**: bant respawn süresi (ve varsa yeni bant stoku/ekonomi etkisi) için kısa bir denge kontrolü — GDD.md §10 ("Kutu ve Eşya Sistemi") baz alınarak.
2. **gameplay**: ItemCategory enum + TapeInfo + ItemDispenser.cs + Table.cs genişletmesi (`PerformSealing`, `SealAndReleaseLockCoroutine`, `_sealingInProgress` + sıfırlama noktaları, `CanTakeItem`/`IsAwaitingSeal`, `GetBoxedProductData`/`SpawnBoxedProductCoroutine`'e suffix parametresi, `ProcessPlayerHasItem` dallanması, `NotifySealingNotReadyClientRpc`) + `PlayerInventory.Visual.cs`'deki `SpawnWorldItemAtPosition`'a bant istisnası (bölüm 5, drop+throw+disconnect'i tek patch'le kapsıyor) — dosya yolları yukarıda.
3. **graphics-ui/devops** (UnityMCP ile prefab/asset işleri): yeni ItemData asset'leri, yeni Normal/NGO prefab çiftleri (RedNGO.prefab deseni kopyalanarak), Shelf.cs'in sahne referanslarının güncellenmesi, RoomItemVisibilityPrefabLinker'ın çalıştırılması. (Bu iş büyük ölçüde script değil, prefab/asset kurulumu — devops ya da gameplay üstlenebilir, ilk uygun olan devam eder.)
4. **qa**: 
   - `ItemCategory` enum'una yeni değer eklenmesinin var olan switch/if-else zincirlerini sessizce bozup bozmadığını grep ile doğrular.
   - Table.cs'teki yeni dallanmanın eski `PerformInstantBoxing` davranışını (kutu+ürün, tape olmadan) regressiona uğratmadığını kontrol eder.
   - **`isItemBoxed` alanını okuyan TÜM yerleri grep'ler** (madde 4 — kullanıcı talebi) — bu alan artık sadece "kapalı/mühürlü kutu hazır" değil, "açık+dolu, bantlanmayı bekliyor" durumunu da kapsıyor; bu varsayımla (`isItemBoxed==true` ⇒ "kutu kapalı, teslim edilebilir") yazılmış başka bir sistem (quest/economy/tutorial) varsa tespit edip raporlar.
   - **`RedNGOFull.prefab`/`BlueNGOFull.prefab`/`YellowNGOFull.prefab`'da `BoxInfo.isFull=true` olduğunu doğrudan prefab YAML'ından teyit eder** (madde 4) — Explore raporundaki bulgu varsayım olarak değil, kanıtlanmış olarak kalsın.
   - **Yeni `RedOpenNGO`/`BlueOpenNGO`/`YellowOpenNGO` (+ Normal varyantları) prefab'larında `BoxInfo.boxType`'ın kopyalama sırasında yanlış renkte kalmadığını doğrudan prefab YAML'ından teyit eder** (kullanıcı talebi).
   - **Bandın `ShelfState`/`TutorialShelfState`'e hiç giremediğini** (kategori `Tool` olduğu için reddedildiğini) doğrular — bölüm 5'teki "kapsam dışı" varsayımının gerçekten güvenli olduğunu kanıtlar.
5. **kontrol**: tüm iş bitince tek toplu ONAY kapısı (birden fazla küçük adım var, dal-sonu tek kapı kuralı uygulanır).

## Açık Kalan / Kullanıcının Kendi Yapacağı İşler

- Paketleme odasındaki yeni **fiziksel masa** (mesh, sahne yerleşimi) ve bant masasının sahnedeki konumu kullanıcı tarafından yerleştirilecek gibi görünüyor ("koyacağım" — kullanıcının kendi ifadesi). Biz gerekli script/prefab'ları hazır edeceğiz; kullanıcı masayı sahneye koyunca `ItemDispenser` component'ini o objeye ekleyip spawn noktasını atamak bizim (devops/gameplay) işimiz olacak — bu adımda kullanıcıdan masanın sahnedeki GameObject'ini işaret etmesi istenecek.
- Eski `RedBox`/`BlueBox`/`YellowBox` (artık kullanılmayan) ItemData/prefab'larının silinip silinmeyeceği — plan bunları SİLMİYOR, sadece kullanımdan kaldırıyor. İstenirse ayrı bir temizlik işi olarak ele alınır.

## Doğrulama

- Unity headless EditMode derleme kontrolü (`unity-headless-verify` hafıza notu: `-runTests` ve `-quit` birlikte kullanılmaz, XML'den oku).
- `mcp__UnityMCP__read_console` ile yeni script'lerin derleme hatasız geçtiği doğrulanır.
- Manuel playtest (host + en az 1 client, memory'de belirtilen build-parity pratiğine uygun): raftan açık kutu al → masaya ürün koy → kutuyla eşleştir → masada AÇIK kutu görünmeli → **masadan elle almayı dene, engellenmeli** (`CanTakeItem`/`IsAwaitingSeal` guard) → bant masasından bant al → **boş bir masaya bandı koymayı dene, engellenmeli** → bantla dolu-açık kutuya götür, etkileşime gir → KAPALI (mevcut Full) kutu görünmeli ve şimdi alınabilir olmalı. Hem host hem client ekranında aynı görsel state senkron olmalı (NetworkTransform/NetworkObject zaten var olan deseni kullanıyor, ek senkron kodu gerekmiyor).
- Rafta boş açık kutu (bantlama sürecine hiç girmemiş, `isItemBoxed=false`) hâlâ eskisi gibi kırılabilir olmalı (`BoxDestroyOnCollisionNetcode`/`BoxFallPenalty` çalışmaya devam etmeli) — sadece masaya konup ürünle eşleştikten SONRA (`isItemBoxed=true`) pickup guard'ı devreye girer, kırılma davranışının kendisi değişmez.
- Bant dispenser: bandı al → slot boşalsın → `respawnDelay` sonra otomatik yeni bant spawn olsun (host ve client'ta aynı anda görünsün).
- **Race condition testi (madde 4/6 — kullanıcı talebi)**: 2 client, ikisi de elinde bant, aynı açık+dolu kutuya (neredeyse) aynı anda etkileşime girsin → sonuçta **tek** kapalı kutu oluşmalı, **tek** bant tüketilmeli (dispenser'da sadece 1 yeni bant respawn olmalı, 2 değil), `_sealingInProgress` kilidinin ikinci isteği reddettiği log'lardan doğrulanmalı.
- **Hazır olmayan masada bant testi (madde 6)**: elinde bant, boş masaya / ham ürünle dolu (henüz kutulanmamış) masaya / zaten kapalı kutu duran masaya etkileşime girilince `NotifySealingNotReadyClientRpc` tetiklenip kısa bir uyarı göründüğü, hiçbir state'in bozulmadığı doğrulanır.
- **Bant düşürme testi (madde 7)**: elde bantla drop/throw tuşuna basılınca yerde/havada bant objesi belirmemeli, envanter boşalmalı; dispenser'ın zaten pickup anında başlattığı respawn sayacı normal işlemeye devam etmeli.
- **Bant + disconnect testi (madde 3)**: bir client elinde bantla oyundan ayrılsın (disconnect) → sunucuda/diğer client'larda ortada bant objesi belirmemeli.
- **Kilit sızıntısı testi (madde 4)**: sarma coroutine'i sürerken masa objesi (test amaçlı) despawn edilirse `_sealingInProgress`'in `OnNetworkDespawn`'da sıfırlandığı, masa yeniden spawn olduğunda kalıcı kilitlenme olmadığı doğrulanır.
