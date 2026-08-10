# Karartma / Oda-Bazlı Görünürlük

> **Durum:** plan ONAYLI (2026-08-10), **kod yazılmadı**, uygulama komutu bekleniyor.
> Bu dosya tek başına yeterlidir — yeni oturumda keşfi tekrarlama, §2'deki ölçümler ve §2.4'teki çürütülmüş varsayımlar hazır.

---

## 0. Zihinsel model (önce bunu oku — 1 dakika)

Deponun üstten görünüşü, gerçek koordinatlarla:

```
x:  -92        -68.3        -43.5   -37.9   -28.9
     |  AVLU    |  ANA HOL   | PAKET | TEZGAH |
        tır        raflar      masa    müşteri
```

Sistem üç cümle:

1. **Unity'de görünmez kutular çiziyorsun.** Her kutu bir oda. Kodda hiçbir oda adı/sayısı yazmıyor — kod sadece "bir nokta hangi kutunun içinde" sorusunu cevaplıyor. Yeni haritada 7 oda çizersin, kod değişmez.
2. **Farklı kutudaki oyuncu çizilmiyor.** Arkadaşın hâlâ oyunda, hareket ediyor, kutu taşıyor — sadece senin ekranında yok. Kapıdan senin odana girince belirir. *Telsizin sebebi bu: parmakla gösteremediğin için tarif etmek zorundasın.*
3. **X'e basınca senin odan normal, gerisi gri ve karanlık.** Ekran kartına "4 oda var" demiyoruz — sadece **senin odanın kutusu** gönderiliyor, ekran kartı her piksel için "bu kutunun içinde mi?" diye soruyor. Ekran kartı "oda" kavramını hiç bilmiyor, tek bir kutu biliyor. **Oda sayısının önemsiz olmasının sebebi tam olarak bu.**

Bir de **yapışkanlık**: tam kapı ağzında dururken ne tam bu odada ne tam şu odadasın. Naif yazılsa arkadaşın her karede görünür/kaybolur diye titrerdi. Çözüm: kutuları sorguda 35 cm büyüt (kenarlar üst üste binsin) ve **kararsız kalınca son odayı koru**. Sistem fikrini değiştirmekte tembel.

---

## 1. Bağlam

Cargor'da 4 oyuncu aynı açık depoda ve herkes her şeyi görüyor — yani **konuşmak için oyunsal bir sebep yok**. Telsiz kodu bitti ama sebebi eksik. Bu iş o sebebi yaratıyor.

**Kilit keşif:** odalar sahnede **zaten var** ama işe yaramıyorlar — iç duvarlar yarım (1.84 m), çatı yok, kamera 40° eğik → herkes duvarların üstünden birbirini görüyor. Eksik olan level değil **görünürlük katmanı**. Yeni geometri / duvar / level yeniden bölünmesi **GEREKMİYOR**.

İki teslimat:
1. **Görünürlük** — başka odadaki oyuncu hiç çizilmez (normal oyunda).
2. **Karartma** — X basılıyken kendi odan tam renk, diğer odalar desatüre + karartılmış.

Oda sayısı **sabit değil** (kullanıcı: *"her haritada oda sayısı değişebilir, modüler olsun"*).

---

## 2. Ölçülen sahne gerçekleri — REFERANS, yeniden keşfetme

Sahne: `Assets/Scenes/The Main Office.unity` (165.409 satır, 6.5 MB, 771 GameObject).

### 2.1 Duvar hatları (FBX mesh vertex AABB'lerinden, dünya uzayı)

Zemin **y ≈ 3.88**. Yarım duvar = y[3.88 … 5.72] → **1.84 m**. Tam duvar alt katman = y[3.88 … 7.37].

| x hattı | tip | z aralığı |
|---|---|---|
| **-68.3** | TAM (çift: Main_Map + Front_Walls) + üst katman 7.36–13.56 | -17.2 … +7.96 |
| **-58.5** | YARIM | +2.76 … +7.93 |
| **-43.5** | YARIM + üst katman → efektif TAM | -17.23 … +7.97 |
| **-37.86** | YARIM | -17.04 … +2.83 |
| **-28.86** | TAM (y 3.91–10.51) | -17.04 … +2.84 |

| z hattı | tip | x aralığı |
|---|---|---|
| **-17.1** | TAM, kesintisiz | -68.44 … -28.78 |
| **-7.63** | görünmez collider bariyeri (y 3.92–10.18, mesh YOK) | -37.87 … -28.48 |
| **+2.9** | YARIM + üst katman | -68.46 … -28.80 |
| **+7.9** | YARIM | -68.47 … -43.44 |

**Uzatılmış collider kanıtı:** GameObject satır 64619 / BoxCollider 64634 → dünya y[3.92 … 10.18], dy = 6.26 m = yarım duvarın 3.4 katı, görünür mesh'i yok. Kullanıcının "collider'ları tepeye çektim" dediği şey bu.
**Görünmez tavan collider'ı:** GameObject 51449 / BoxCollider 51467 → x[-74.21…-28.08] y[9.25…9.70] z[-18.23…+10.71]. "Üstünden item atma" işini yapan asıl parça.

### 2.2 İşlevsel bölgeler (dünya x/z)

| Bölge | x | z | kanıt |
|---|---|---|---|
| Raflar (13 adet, tag `Shelf`) | -57.6 … -46.1 | -6.3 … +6.6 | `StorageRack_2_mesh.001`, y=3.93 |
| Raf slotları (39) | -58.6 … -45.1 | -6.3 … +6.6 | y=5.40 |
| Paketleme masaları (2, tag `Table`) | -42.8 … -38.6 | -5.2 … +0.2 | `Cube.012 (3)`/`(4)` + 4 `PlacePoint` |
| Tır hangarı | -92.1 … -66.2 | -9.9 … +0.6 | `TruckSpawner1/3/4` x=-78.09 · `HangarSpawnPoint` x=-66.17 · `HangarExit` x=-92.10 |
| Garaj kapıları (3) | -68.37 | -9.99 / -4.71 / +0.55 | `Static_Garage_Door` |
| Müşteri kuyruğu (12 waypoint) | -32.4 … -24.8 | -6.6 … +8.5 | `Sira1…Sira6 (6)` |
| Müşteri spawn/exit | -33.44 / -31.22 | +17.83 | `CustomerSpawner` (tag `ExitPoint`) |
| Break room | -68.2 … -58.9 | -17.1 … -12.9 | `BreakRoom` trigger @ (-63.57, 4.9, -15.01) |
| Oyuncu spawn (tek) | -62.44 | -14.81 | `PlayerSpawner`, y=3.85 |

**Toplam bina/oynanabilir alan:** x[-92.1 … -24.8] (67 birim), z[-17.2 … +17.8] (35 birim), merkez ≈ (-58, -4).
**İç zemin mesh'leri:** `Place` x[-68.42…-43.51] z[-17.16…+7.90] · `Place.001` x[-43.57…-28.80] z[-17.14…+2.96].

### 2.3 Kapı boşlukları (collider kesintisi)

Yalnız **x = -68.3** hattında net boşluk var — 3 adet, `Static_Garage_Door`larla birebir örtüşüyor, **collider YOK** (oyuncu düz geçiyor):

| # | boşluk (z) | genişlik | eşleşen kapı |
|---|---|---|---|
| 1 | -11.90 … -7.78 | 4.12 m | z=-9.99 (satır 704) |
| 2 | -6.78 … -2.53 | 4.25 m | z=-4.71 (satır 46689) |
| 3 | -1.53 … +2.81 | 4.34 m | z=+0.55 (satır 71425) |

**İç kapılar — `Door.fbx`, 4 instance, collider'lı** (3-4 BoxCollider her birinde):

| instance | menteşe dünya (x, z) | hangi hat |
|---|---|---|
| Door (2) | (-43.58, -10.25) | x = -43.5 ✓ |
| Door (3) | (-37.94, -10.23) | x = -37.86 ✓ |
| Door | (-56.00, -12.39) | z ≈ -12.4 |
| Door (1) | (-59.60, -12.39) | z ≈ -12.4 (break room güney kenarı) |

**Diğer hatlarda boşluk tespit edilemedi — DOĞRULANMADI.** Sebep: o hatlar tek parça mesh, kapı boşluğu mesh'in içine modellenmiş, AABB'den görünmüyor.

### 2.4 Çürütülmüş varsayımlar — tekrar araştırma

| İddia | Gerçek |
|---|---|
| "Sahnede oda kavramı yok, **level yeniden bölünmeli**" (`telsiz-voice-chat.md:210`) | **YANLIŞ.** Odalar var, duvarlar tek FBX mesh içinde olduğu için isim/tag aramasında görünmüyor. Level işi kapsamdan DÜŞTÜ. |
| "Harita görünümü (0,50,0)'a çıkıyor, sahne kadraja sığmıyor, %8 görünür" | **YANLIŞ** — koddaki default'a bakılmış. Sahnede override: `mapViewPosition (-52.5, 26, -32)`, `mapViewRotationX **40**` (`The Main Office.unity:31830`). Top-down harita DEĞİL, 40° eğik geriye çekilmiş kamera. Harita görünümü sağlam. |
| Layer 10 / tag `Wall` ile duvarlar bulunur | **HAYIR.** Ham alan olarak 0 sonuç; yalnız 2 PrefabInstance override'ı: `Front_Walls` (satır 45326) ve `Main_Map 1` (satır 75734). |
| `Officewall` bir iç duvardır | **HAYIR** — eğik TAVAN paneli. Dünya (-55.90, 8.75, -14.72), y[7.11…10.39], break room'un ~3 m üstünde. (Parent'ın -90° Y dönüşü hesaba katılmazsa yanlış koordinat çıkar.) |
| `Bolme` bir bölmedir | **HAYIR** — yalnız Transform + NetworkObject. Collider yok, MeshRenderer yok, MeshFilter yok. |
| QuickOutline gizlenen oyuncuda outline bırakır | **HAYIR** — `Outline` yalnız `PlayerInventory.Detection.cs:265-282`'de `NetworkWorldItem`'a ekleniyor. `Character.prefab`'te 0, sahnede 0. |
| `WorldSpaceCanvasCameraBinder` çakışır | **HAYIR** — `renderMode != RenderMode.WorldSpace` filtresi var, karakterin canvas'ı Screen-Space-Overlay. |
| Telsiz etkilenir | **HAYIR** — `RadioVoiceSpeakerSlot.cs:161` ve `RadioVoiceRuntime.cs:95` `spatialBlend = 0f`; HUD ekran-uzayı (`RadioHudController.cs:348`), oyuncu objesinden bağımsız. |
| Kamera layer culling ile gizlenebilir | **HAYIR** — eldeki kutu `PlayerInventory.Visual.cs:174`'te Layer 0 (Default)'a çekiliyor, oyuncu Layer 6 (Character). Character layer'ı cull edilirse **havada uçan kutu** kalır. |
| `FlatLitProps.shader` yamanmalı | **HAYIR** — ölü, guid `c121836e…` hiçbir `.mat`'te yok. |

### 2.5 Çözülemeyen (dürüstlük notu)

`Main_Map 1` prefab instance'ında **24 BoxCollider + 1 MeshCollider** elle eklenmiş; `m_Size`/`m_Center` sahnede okunabiliyor ama bağlı oldukları node'un transform'u **binary FBX içinde** ve `.fbx.meta`'da `internalIDToNameTable: []` boş → fileID→FBX node eşlemesi çıkarılamıyor. **Bu 24 collider'ın dünya konumu doğrulanmadı.** Break room'un güney (z≈-12.4) ve doğu (x≈-58.5) sınırı büyük ihtimalle bunlardan geliyor. → S2'de kutuları Gizmo'ya bakarak elle oturtmak zaten bunu çözüyor.

### 2.6 Oda adayları (S2 için başlangıç noktası)

```
AVLU        | x[-99 .. -68.3]              | tır, kamyonlar, hangar    | 3 garaj kapısı boşluğu
ANA_HOL     | x[-68.3 .. -43.5] z[-17.1 .. +7.9] | raflar, break room, spawn | Door(2) @ (-43.58,-10.25)
PAKET       | x[-43.5 .. -37.86]           | paketleme masaları        | Door(3) @ (-37.94,-10.23)
TEZGAH      | x[-37.86 .. -28.86]          | müşteri kuyruğu, ofis     | z=-7.63'te görünmez bariyer
```

Ayrıştırılabilir ama muhtemelen gereksiz: BREAK_ROOM (x[-68.3…-58.5] z[-17.1…-12.4], kimse iş yapmıyor) · KUZEY_ŞERİT (z > +2.9, `x=-58.5` yarım duvarıyla ikiye bölük).

---

## 3. Üç sütun (tasarım)

### 3.1 Oda verisi — modüler, sahneden gelir
`RoomVolume` bileşeni sahnede elle yerleştirilir (Gizmo'lu), `RoomRegistry` toplar, `RoomResolver` "bu nokta hangi odada" sorusunu cevaplar. **Kodda hiçbir oda sabiti yok.** Birden çok `RoomVolume` aynı `roomId`'yi paylaşabilir → L şekilli oda tek kutuya sıkışmak zorunda değil. Oda yoksa (Tutorial) sistem kendini kapatır, herkes görünür.

**Histerezis — zamanlayıcısız yapışkan çözücü.** Asıl tehlike oda↔oda değil, **oda↔hiçbiri** (kapı boşluğunda hiçbir hacme girmeyen nokta → her karede gizlen/görün):
- kutular sorguda `margin` (~0.35 m) ile genişletilir → komşular çakışır;
- hiçbir kutuda değilse **mevcut oda korunur** (`-1` DÖNMEZ);
- ≥2 genişletilmiş kutudaysa **mevcut olan tercih edilir**.

Çakışma + prefer-current histerezisin kendisidir; ayrı dwell-timer gerekmez.

### 3.2 Görünürlük — İSTEMCİ TARAFLI (NetworkHide DEĞİL)
Oyuncu ağda hep var, sadece **çizilmiyor**.

**Neden NetworkHide değil:** oyun akışı raf→paketleme→tezgah, oyuncular sürekli oda değiştiriyor; her geçişte despawn/respawn = `ClientNetworkTransform` re-sync + görünür pop-in + istemcide `NetworkObject` yok olduğu için `_itemsInRange` / `NetworkWorldItem` referanslarının düşmesi. Co-op'ta gizlemenin koruyacağı hile riski zaten yok.

**Sahiplik:** merkezi manager YOK. Her oyuncu prefabinde `PlayerRoomVisibility` kendi odasını hesaplar, statik `LocalRoomId` ile karşılaştırır → late-join, despawn, `ConnectedClientsList`'in istemcide boş olması dertleri tek seferde gider.

**Sabit renderer listesi TUTMA** — her uygulamada `GetComponentsInChildren<Renderer>(true)`. Kapsam: **15** SkinnedMeshRenderer + 2 ParticleSystemRenderer (`Dust`, `Dust (1)`) + **oyuncunun elindeki kutu** (`HoldPosition` karakterin child'ı, `Character.prefab:7579`). Uygulama **kenar-tetiklemeli** (oda değişince), her karede değil — Canvas/SetActive UI rebuild tetikler.

### 3.3 Karartma — GLOBAL SHADER AABB
Materyal duplikasyonu ve `MaterialPropertyBlock` **yok**. Aktif odanın AABB'si `Shader.SetGlobalVector` ile gönderilir, fragment `positionWS`'e bakar:

```hlsl
// UnityPerMaterial CBUFFER'ının DIŞINDA (TEXTURE2D bildirimleri gibi):
float4 _CargoRoomMin;
float4 _CargoRoomMax;
float  _CargoRoomFade;   // 0 = kapalı, 1 = tam karartma
float  _CargoRoomDesat;
float  _CargoRoomDim;

// frag(), return'den hemen önce:
float inside = (all(IN.positionWS >= _CargoRoomMin.xyz) &&
                all(IN.positionWS <= _CargoRoomMax.xyz)) ? 1.0 : 0.0;
float k = _CargoRoomFade * (1.0 - inside);
finalColor = lerp(finalColor, Luminance3(finalColor).xxx, k * _CargoRoomDesat);
finalColor *= lerp(1.0, _CargoRoomDim, k);
```

`positionWS` (`FlatLitEnvironment.shader:85, :101`) ve `Luminance3()` (`:91`) **zaten var**.

**Kritik detaylar:**
- Globaller `UnityPerMaterial` CBUFFER'ının **DIŞINDA** → SRP Batcher korunur. (Doğrulama: Frame Debugger'da önce/sonra "SRP Batch" düğüm sayısı — shader'a dokunmanın **tek gerçek riski** bu.)
- **`k` çarpımda en dışta** olmalı ki `min=max=0` durumu ("her şey dışarıda", sistemsiz sahne) etkisiz kalsın. Tanımsız global varsayılanı 0 → shader kimliktir.
- Harita kapalıyken fade **bit-tam 0** → `Mathf.MoveTowards`, **`Lerp` DEĞİL** (asimptotik yaklaşma 0.002'lik kalıcı gri bırakır).

---

## 4. Ne DEĞİŞMEZ (koruma bandı)

- `CameraFollow.cs` — `IsMapViewActive` (`:153`) zaten public, **yalnız okunur**.
- `PlayerMovement.cs`, `PlayerInventory.Detection.cs` — hiç dokunulmaz.
- Etkileşim/raycast — layer-agnostik (`itemLayerMask = -1`, `Character.prefab:14175`; layer yalnız öncelik sıralaması için, `Detection.cs:150`) → gizleme onu bozmaz.
- Telsiz — bkz. §2.4.
- `UsePass ShadowCaster` / `DepthOnly` — asla.
- Hiçbir ekonomik değere (fiyat, süre, ödül, çarpan, kota) dokunulmaz.

---

## 5. Yeni dosyalar

| Dosya | Sorumluluk |
|---|---|
| `Assets/NewCss/Rooms/Core/NewCss.Rooms.Core.asmdef` | `noEngineReferences: true`. Şablon: `Assets/NewCss/Voice/Core/NewCss.Voice.Core.asmdef` |
| `Assets/NewCss/Rooms/Core/RoomBox.cs` | Saf struct: 6 float + `Contains(x,y,z)` + `Expanded(m)`. **`Bounds`/`Vector3` KULLANILAMAZ** (motor referanssız assembly) |
| `Assets/NewCss/Rooms/Core/RoomResolver.cs` | Yapışkan/histerezis durum makinesi: `int Resolve(int current, float x, float y, float z)`. **Tek gerçek mantık, testlerin hedefi** |
| `Assets/NewCss/Rooms/Core/RoomFade.cs` | `float Step(float cur, bool want, float dt, float speed)` — tam 0/1 clamp |
| `Assets/NewCss/Rooms/RoomVolume.cs` | MonoBehaviour: `roomId`/`roomName` + `Bounds`. `OnEnable`/`OnDisable`'da Registry'ye kayıt (**`FindObjectsByType` YOK** → additive sahne uyumlu). `OnDrawGizmos` + `OnDrawGizmosSelected` — **authoring için şart, S2 bunsuz yapılamaz** |
| `Assets/NewCss/Rooms/RoomRegistry.cs` | Statik liste + `RoomBox[]` cache'i + `Resolve` sarmalayıcı |
| `Assets/NewCss/Rooms/RoomViewController.cs` | Bootstrap + global shader değişkenleri + `public static int LocalRoomId` (§6.1) |
| `Assets/NewCss/Rooms/PlayerRoomVisibility.cs` | `Character.prefab` üzerinde. Her Apply'da `GetComponentsInChildren<Renderer>(true)`; **Canvas HARİÇ**. Kenar-tetiklemeli. `public void Refresh()` |
| `Assets/Tests/EditMode/RoomResolverTests.cs` | §11 |

### 5.1 `RoomViewController` akışı

Bootstrap deseni **`WorldSpaceCanvasCameraBinder.cs:26-32` birebir**: `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` + `HideFlags.HideAndDontSave` + `DontDestroyOnLoad`. Sahne authoring'i gerektirmez, Tutorial'da güvenle boş çalışır.

`Update()`:
1. yerel oyuncu transform'u (cache, 0.5 s aralıkla tazele — desen `CameraFollow.cs:17` `TARGET_SEARCH_INTERVAL`);
2. `RoomRegistry.Resolve(_currentRoomId, pos)` → yapışkan oda;
3. `bool want = _cameraFollow != null && _cameraFollow.IsMapViewActive;`
4. `_fade = Mathf.MoveTowards(_fade, (want && haveRoom) ? 1f : 0f, dt * fadeSpeed);`
5. değiştiyse `SetGlobalFloat`; oda değiştiyse `SetGlobalVector(min/max)`. `Shader.PropertyToID` `static readonly int`'te cache'li;
6. `OnDisable` / `OnDestroy` + startup hook → fade **tam 0** (bkz. delik H6).

**Fade hızı kamerayla uyumlu olmalı:** `_isMapViewActive` anında dönüyor (`CameraFollow.cs:233`) ama kamera lerp'liyor (`mapViewTransitionSpeed = 5`) — yoksa kamera hareket etmeden dünya griye döner. Escape menüsü `timeScale = 0` yapıyorsa `unscaledDeltaTime` gerekir (**menü davranışı doğrulanmadı**).

---

## 6. Değişecek mevcut dosyalar (7 dosya, çoğu tek satır)

| Dosya | Yer | Değişiklik |
|---|---|---|
| `Assets/Tests/EditMode/Cargor.Tests.EditMode.asmdef` | `references` | `"NewCss.Rooms.Core"` ekle. ⚠️ `overrideReferences: true` + `autoReferenced: false` → unutulursa yalnız test assembly'sinde "type not found", kafa karıştırıcı |
| `Assets/NewCss/NewPickup/PlayerInventory.Visual.cs` | `:174` sonrası (`SpawnHeldItemVisual` sonu) | tek satır `Refresh()` çağrısı (delik H2). `DestroyHeldItemVisual` için hook GEREKMEZ |
| `Assets/NewCss/CharacterScript/NetworkStaminaBarUI.cs` | `OnNetworkSpawn` `:59-62` | `if (!IsOwner)` canvas kapat — **mevcut bug, oda sisteminden bağımsız** |
| `Assets/Shaders/FlatLitEnvironment.shader` | CBUFFER `:71` altı + `return :218` öncesi | globaller + 4 satır |
| `Assets/Shaders/FlatLitMetal.shader` | CBUFFER `:62` altı + `return :181` öncesi | globaller + **`Luminance3` helper** + 4 satır |
| `Assets/Shaders/FlatLit.shader` | CBUFFER `:143` altı + `return :288` öncesi | globaller + 4 satır, **yalnız Forward pass** |
| `Assets/ithappy/Creative_Characters_FREE/Saved_Characters/Character.prefab` | — | `PlayerRoomVisibility` bileşeni ekle |

### 6.1 Shader yaması kapsamı

| Dosya | Pass | Materyal | Not |
|---|---|---|---|
| `FlatLitEnvironment.shader` | `EnvironmentForward` (`:36-221`) | **40 sahne materyali** | işin **%85'i** |
| `FlatLitMetal.shader` | `MetalForward` (`:31-185`) | 7 | `positionWS` `:79/:92` var, **`Luminance3` YOK** |
| `FlatLit.shader` | Forward (`:106-292`) | 5 (`M_Character_Toon`, `M_NPC1..4`) | **Müşteriler için** — odalar arası yürüyorlar, karartılmazsa göze batar. Uzak oyuncular zaten gizli, yerel oyuncu daima kendi odasında → oyuncu için hiç çalışmaz |
| `FlatLit.shader` | **Outline** (`:40-105`) | — | **DOKUNMA.** `Varyings` yalnız `positionCS` (`:77-79`), interpolator eklemek gerekir; `_OutlineColor` zaten koyu → karartmak ~identity |
| `FlatLitProps.shader` | — | **0** | ölü, atla |

---

## 7. 12 delik — tam liste

### Ciddi

**H1 — Karartma kapsamı eksik (authoring işi, kod değil).** Sahnede shader'a göre materyal sayımı: `FlatLitEnvironment 40 · FlatLitMetal 7 · URP/Lit 4 · Glass 1`. Karartılmayan 5: `Assets/Models/M_Bricks_Var_1.mat`, `Assets/UI/UImaterial/Mat.002.mat`, `Mat44.mat`, `Wallcor1.mat`, `Assets/Models/New Folder/Unity-URP-GlassShader-master/Glass.mat` → karartılmış odanın içinde **parlak lekeler**. Çözüm: bu 5 materyali `FlatLitEnvironment`'a çevir. (`PLASTICBOX.mat` zaten FlatLitEnvironment, güvende.)

**H2 — Gizliyken kutu alma.** `PlayerInventory.Visual.cs:158` `SpawnHeldItemVisual()` **3 yerden** çağrılıyor (`:31`, `:146`, pickup coroutine); her çağrıda YENİ GameObject + YENİ renderer, `:174`'te Layer→Default. Sistem haberdar değil → boş odada **havada yüzen kutu** (sızıntıdan daha kötü: hem konumu ele veriyor hem komik). Çözüm §3.2 + §6.

**H3 — Histerezis.** §3.1. Ayrıca Apply kenar-tetiklemeli olmalı.

### Orta

**H4 — Gölge sızıntısı.** `Renderer.enabled=false` gölgeyi de keser → gizli oyuncu sorunsuz. Ama karartma yalnız Forward pass'te; `UsePass ShadowCaster` dokunulmadığı için **başka odanın gölgeleri tam güçte**. Kozmetik.

**H5 — AABB tek kutu, oda L şekliyse kırılır.** Bu haritada duvarlar eksen hizalı → tek AABB yeter. Genel çözüm: veri katmanı çoklu (aynı `roomId`'li 2 `RoomVolume`), **shader tek kutu**. Union oda gerekirse `SetGlobalVectorArray` ile N kutu — v1'de YAPMA.

**H6 — Globaller yapışkan.** `SetGlobalFloat` sahne değişince sıfırlanmaz. Harita görünümü açıkken oyuncu despawn olursa **dünya kalıcı gri kalır** ve sıfırlayacak kimse yoktur. `OnDisable`/`OnDestroy` + `[RuntimeInitializeOnLoadMethod]`'da fade=0 **ZORUNLU**. Editor domain reload'da Scene View'i de etkiler.

**H7 — Fade asla tam 0 olmuyor.** `Lerp` asimptotik → 0.002 karartma kalıcı. `Mathf.MoveTowards` + tam clamp. Fade hızı ↔ kamera lerp uyumu için bkz. §5.1.

**H8 — "Oda yok" semantiği.** Garaj kapısı, bina dışı, spawn öncesi, `RoomRegistry.Count == 0` (Tutorial), `OnNetworkSpawn`'da ilk çözümlemeye kadar → **zorunlu varsayılan: herkes görünür + fade 0**.

**H9 — Pop-in.** Duvarlar 1.84 m, kamera eğik → zaten diğer odanın üstünden görüyorsun. Gizleme doğal occlusion gibi değil, **yok olma** gibi hissedilecek. `Renderer.enabled`'ın fade'i yok; Opaque shader'da alpha fade = blend değişikliği (pahalı). **v1'de sert toggle — bilerek gir**, playtest'teki ilk şikayet muhtemelen bu.

### Kararlar (bug değil)

**H10 — Ayak sesi duvardan geçer.** `PlayerMovement.cs:262` ve `PlayerInventory.cs:328` `spatialBlend = 0.5f`, `maxDistance = 15f` → görünmeyen oyuncunun ayak sesi ve kutu sesi duyulur. Ya telsiz kurgusunu zayıflatır ya hoş bir ipucu. **Playtest-1'de DEĞİŞTİRME, ölç.**

**H11 — Stamina bar oda sistemine BAĞLANMAZ.** Aynı odadaki 3 oyuncunun overlay bar'ı ekranda üst üste binerdi. Doğru düzeltme `IsOwner` gate'i (§6) — oda sisteminden bağımsız. Canvas'ı görünürlük sisteminden **tamamen çıkar**.

**H12 — Müşteriler gizlenmez**, yalnız karartılır — "müşteriye hizmet" döngüsünün farkındalığını kırmamak için.

---

## 8. Adım sırası + delegasyon

```
S0  Rooms.Core + asmdef + test asmdef edit + testler   [gameplay]  bağımsız, tek başına merge edilebilir
S1  RoomVolume + RoomRegistry + Gizmos                 [gameplay]  ← S0 ; S2'yi AÇAR
S2  ★ KULLANICI: odaları Unity'de çiz                  [sen]       ← S1 ; S3/S4/S5'in DOĞRULANMASINI bloke eder
S3  RoomViewController (globaller + LocalRoomId)       [gameplay]  ← S1
S4  PlayerRoomVisibility + Visual.cs 1 satır + prefab  [gameplay]  ← S3  ┐ paralel
S5  3 shader yaması + Frame Debugger doğrulaması       [graphics-ui] ← S3 ┘
S6  NetworkStaminaBarUI Canvas fix                     [gameplay]  bağımsız
S7  2 istemcili playtest                               [sen]       ← S2, S4, S5
S8  Dal-sonu KONTROL kapısı (tek toplu ONAY, max 3 tur)[kontrol]   ← S0–S6
```

**Not:** S0–S6 art arda küçük task'lar → her birini ayrı kontrol'e sokma, **dal-sonu tek toplu ONAY** (CLAUDE.md §4).

---

## 9. Senin Unity'de yapacakların

1. **(S2 — otomatikleştirilemez, tek bloke eden iş)** Sahneye `--- ROOMS ---` boş objesi + altına `RoomVolume`'ler. Gizmo'lara ve §2.1 duvar hatlarına göre kutuları oturt. Başlangıç noktası §2.6.
   ⚠️ **Y'yi cömert ver** (zemin−1 → zemin+8, yani y ≈ 2.9 → 11.9): iç duvarlar yalnız 1.84 m, dar bir Y kutusu tavanı ve zemini karartma dışında bırakır.
   ⚠️ Garaj kapısı boşlukları (z ≈ -9.84 / -4.66 / +0.64) ve iç kapı ağızları: kutuları margin kadar çakıştır ya da boşluk bırak — yapışkanlık (§3.1) halleder.
2. `Character.prefab`'e `PlayerRoomVisibility` ekle.
3. `RoomViewController` inspector'ında dim / desat / fade hızı ayarla.
4. *(opsiyonel, H1)* 5 URP/Lit materyalini `FlatLitEnvironment`'a çevir.

---

## 10. Doğrulama

**Headless EditMode** (`NewCss.Rooms.Core` motor referanssız — `NewCss.Voice.Core` deseni):
- kutu içi/dışı + **sınırda tam nokta** (`>=` vs `>` kararı)
- çakışan iki genişletilmiş kutuda → `current` korunur (histerezis sözleşmesi)
- hiçbir kutuda değil → `current` **değişmez**, `-1` DÖNMEZ (H8)
- 0 oda → sentinel döner, exception atmaz (Tutorial güvenliği)
- **sınırı ±1 cm ile 10 kez geçen yol → tam 1 geçiş** ← *en değerli test, flicker regresyonu*
- `RoomFade.Step`: 0.9'dan `want=false` ile sonlu adımda **tam `0f`** (H7)

⚠️ Editör **`6000.5.6f1` headless** (`6000.4.3f1` KULLANMA → `com.unity.modules.physicscore2d bulunamadı`). Yol: `/c/Program Files/Unity/Hub/Editor/6000.5.6f1/Editor/Unity.exe`. **Unity AÇIKKEN batchmode çalışmaz** ("another Unity instance") — kullanıcıdan kapatmasını iste. Batchmode artefaktlarını (`ProjectSettings.asset` SENTIS define, `UniversalRenderPipelineGlobalSettings.asset`, `ProjectAuditorSettings.asset`, font atlası) `git checkout --` ile geri al, commit'e sokma.

**Yalnız oyun içinde görülebilir:**
AABB'lerin gerçek geometriyle örtüşmesi · **SRP Batcher'ın bozulmaması (Frame Debugger önce/sonra batch sayısı — shader dokunuşunun tek gerçek riski)** · 5 URP/Lit materyalin lekesi (H1) · gölge sızıntısı (H4) · pop-in hissi (H9) · `mapViewRotationX=40`'ta karartmanın "başka oda" olarak okunup okunmadığı · çok-istemcili davranış (2 build; ParrelSync projede var mı **doğrulanmadı**).

---

## 11. Ekonomi

**Playtest ÖNCESİ economist turu ZORUNLU DEĞİL.**
- Değişim **saf bilgi** değişimi: hiçbir spawn oranı / fiyat / süre / kapasite dokunmuyor. Modelin girdileri aynı; değişen tek şey insan koordinasyon verimi — repoda "oyuncu takım arkadaşının yerini biliyor" diye modellenmiş bir değişken **yok**.
- Etkinin **işareti bile belirsiz**: ya iş tekrarı/bekleme yaratıp hızı düşürür, ya telsizle rol uzmanlaşmasına zorlayıp yükseltir. İşaretini bilmediğin değişkeni modellemek tiyatro.
- Playtest öncesi yapılmaya değen şey modelleme değil **enstrümantasyon**: `kutu/dakika/oyuncu` run başına loglanıyor mu? (**doğrulanmadı** — `GameEconomySettings.cs` + gün-sonu özet yolu kontrol edilmeli.) Loglanmıyorsa o ~1 saatlik iş playtest'ten önce yapılmalı, yoksa oyna-test sayı üretmez.
- **Tek istisna — 5 dakikalık kota marjı kontrolü:** mevcut kotalar "herkes herkesi görüyor" varsayımıyla *sıkı* ayarlanmışsa değişim oyunu kazanılamaz yapıp playtest'i yakar.
- Throughput **%20'den fazla düşerse** economist turu playtest **SONRASI** zorunlu — o zaman elde veri olur. Ayar **görünürlük kuralında değil**, gün uzunluğu/kotada yapılır.

---

## 12. Git

Yeni dal **`feature/room-visibility`, `main`'den**.
`feature/voice-chat` üstüne **KURMA**: o dalda 19 merge'siz commit ve açık bir Dissonance kararı var (kabul edilirse ~2500 satır ses kodu silinecek). Oda işi ses kodundan bağımsız doğrulandı (§2.4) → dolaştırmanın anlamı yok.
