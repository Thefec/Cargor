# ⚡ Optimizasyon Teftişi — 2026-09-18

**Kapsam:** `Assets/NewCss/` + `Assets/Scripts/` (Discord SDK ve Editor klasörleri hariç), `ProjectSettings/`, `Assets/Settings/` (URP).
**Yöntem:** Tüm Update/LateUpdate/FixedUpdate/ServerUpdate/OnGUI gövdeleri (42 adet) bir seviye helper-inlining ile tarandı; FindObject/GetComponent/Camera.main/Debug.Log/string/LINQ/Physics-alloc/NetworkVariable yazımı kalıpları sayıldı; proje ayarları okundu. Alt ajan kullanılmadı.
**Durum (2026-09-18, onay sonrası):** UYGULANDI — madde 2, 6, 4, 1. Derleme + konsol temiz. COMMIT EDİLMEDİ (dal `fix/difficulty-scaling-and-dead-code`).
- Madde 2: `ProjectSettings.asset` `m_StackTraceTypes` 4. giriş (LogType.Log) 1→0. Warning/Error/Exception ScriptOnly kaldı.
- Madde 6: `StaminaBar.cs` Image Awake'de önbellekli. `NextDayUIManager` tarafı YANLIŞ ALARM: GetComponent'ler yalnız Lock/Unlock geçişinde çalışıyor, per-frame değil — dokunulmadı.
- Madde 4: `CustomerAI.UpdateAnimator` NetworkVariable yalnız |Δ|>0.02 veya 0'a iniş anında yazılıyor.
- Madde 1: Standalone varsayılan kalite index 4→3 (`PC_RPAssetHIGHT`), `m_CurrentQuality` 4→3, `GraphicsSettings.m_CustomRenderPipeline` ULTRA→HIGHT. ULTRA asset'in kendisine DOKUNULMADI (kullanıcı seçmedi). NOT: PlayerPrefs `QualityLevel` kayıtlı makinelerde eski seviye kalır; Ayarlar menüsünden değiştirmek gerekir.
- Madde 3 İPTAL: `Outline.registeredMeshes` zaten `static HashSet<Mesh>` — SmoothNormals mesh başına oturumda bir kez çalışıyor, teftiş bulgusu YANLIŞTI. Kalan per-spawn maliyet 2 materyal Instantiate + renderer.materials kopyası (QuickOutline tasarımı), MaterialPropertyBlock refactoru bu tur dışı.
- Madde 5 UYGULANMADI (5 madde tavanı + Editor teşhis alışkanlığı riski).

## Olumlu bulgular (dokunma)
- Update yolları büyük ölçüde throttle'lı/guard'lı: `DayCycleManager` UI 10 Hz + değişim kontrolü, `RoomViewController` arama 
  aralıklı, `LobbyDifficultyDisplay` aralıklı, `CustomerManager.Update` server-only.
- `Physics.OverlapSphere` (alloc'lu) 9 çağrı yeri var ama hepsi tuş basımında çalışıyor, per-frame değil; per-frame algılama zaten `NonAlloc`.
- `DynamicsManager`: AutoSyncTransforms kapalı, ReuseCollisionCallbacks açık. Fixed timestep 0.02.
- `LocalCoopTestBootstrap.OnGUI` ve `RadioVoiceDevTools` editör/dev-only.
- Outline materyalleri OnDestroy'da temizleniyor (sızıntı yok).

## Öncelikli bulgular (etki sırası)

### 1. Varsayılan kalite seviyesi = ULTRA URP asset — en büyük GPU kaldıracı  · risk: DÜŞÜK ama GÖRSEL DEĞİŞİKLİK → ONAY GEREKİR
- **Sorun:** `ProjectSettings/QualitySettings.asset` `m_PerPlatformDefaultQuality: Standalone: 4` ve `GraphicsSettings.m_CustomRenderPipeline`
  ikisi de `Assets/Settings/PC_RPAssetULTRA.asset`'e gidiyor: `m_RenderScale: 2` (4× piksel), `m_MSAA: 8`, ana ışık gölge haritası 8192,
  HDR + depth + opaque texture açık, `PC_Renderer`'da SSAO feature aktif. `UnifiedSettingsManager.cs:936` PlayerPrefs'te kayıt yoksa
  mevcut seviyeyi baz alıyor → her oyuncu ilk açılışta ULTRA'da başlıyor.
- **Neden önemli:** Orta seviye PC'de kare süresi doğrudan piksel sayısıyla ölçeklenir; 2× render scale + 8× MSAA tek başına 4-8× GPU yükü.
- **Çözüm:** (a) Standalone varsayılanını index 3 (`PC_RPAssetHIGHT`: scale 1.5, MSAA 4, 2048) ya da index 2 (`MEDIUM`: scale 1, MSAA 2) yap;
  (b) ULTRA asset'ini makul sınıra çek: render scale 2→1.25, gölge 8192→4096 (ULTRA "seçilebilir" kalır ama saçma olmaz).
- **Risk:** Görsel netlik/kenar yumuşatma ilk açılışta farklı görünür. Kod değişmez. Kullanıcı hangi seviyeyi varsayılan istediğini söylemeli.

### 2. Build'de her `Debug.Log` yığın izi (stack trace) topluyor  · risk: ÇOK DÜŞÜK
- **Sorun:** `ProjectSettings.asset` `m_StackTraceTypes` 6 log tipinin hepsi için `1` (ScriptOnly). Wrapper dışı 672 ham `Debug.Log(` var
  (müşteri durum geçişleri, pickup, ağ olayları — sık tetiklenen yollar). Her çağrı managed stack yürüyüşü + string.
- **Neden önemli:** Log tek başına ucuz değil, stack trace ile 10-50× pahalı; ana thread'de hitch üretir.
- **Çözüm:** `Log` tipi için stack trace → None (Warning/Error/Exception ScriptOnly kalır). Mesajlar `Player.log`'a aynen yazılmaya devam eder
  (multiplayer teşhis alışkanlığı bozulmaz), sadece Info satırlarının altındaki stack kalkar.
- **Opsiyonel (ONAY GEREKİR):** non-development build'de `Debug.unityLogger.filterLogType = LogType.Warning` → Info logları tamamen kesilir.
  Player.log içeriğini değiştirir; [[cargor-steam-deploy]] teşhis akışıyla çelişir, o yüzden ayrı karar.

### 3. `Outline` her spawn'da tüm vertex'leri LINQ ile grupluyor  · risk: DÜŞÜK
- **Sorun:** `Assets/NewCss/TableScripts/Outline.cs:88-105` Awake: 2 materyal `Instantiate` + `LoadSmoothNormals()` → `SmoothNormals()`
  (`:237`) `mesh.vertices.Select(...).GroupBy(...)` + `group.Count()`; `precomputeOutline` 36 prefab'ın HİÇBİRİNDE açık değil (`precomputeOutline: 1` = 0).
  4 çalışma-zamanı `AddComponent<Outline>` noktası daha var (CustomerAI, PlayerInventory.Detection, Table, TutorialManager).
- **Neden önemli:** Her kutu/eşya/müşteri spawn'ında mesh boyutuyla orantılı CPU + GC; PlateUp tarzı sık spawn döngüsünde hitch kaynağı.
- **Çözüm:** `Outline.cs`'e `static Dictionary<Mesh, List<Vector3>>` önbelleği: aynı sharedMesh için normaller oturumda bir kez hesaplanır.
  Görsel çıktı birebir aynı. Materyal instantiate'e dokunulmaz (per-instance renk gerekiyor).
- **Risk:** Düşük; tek dosya, saf hesaplama önbelleği.

### 4. `CustomerAI` her server frame'inde NetworkVariable yazıyor  · risk: DÜŞÜK
- **Sorun:** `CustomerAI.cs:684` `UpdateAnimator()` → `_networkAnimatorSpeed.Value = normalizedSpeed` koşulsuz, her frame. NavMesh hız
  büyüklüğü sürekli dalgalandığı için değişken her tick dirty → müşteri başına her network tick'te delta gönderimi.
- **Neden önemli:** 3-4 oyuncuda 10+ müşteri × tick başına serileştirme; bant genişliği küçük ama CPU serileştirme + client OnValueChanged spam.
- **Çözüm:** Yalnız `Mathf.Abs(yeni - eski) > 0.02f` ise yaz (ve durunca 0'a kesin yaz). Animasyon blend görsel olarak aynı.
- **Risk:** Düşük; 3 satır.

### 5. `LogDebug($"...")` bayrak kontrolünden ÖNCE string üretiyor  · risk: DÜŞÜK
- **Sorun:** 20 dosyada `LogDebug(string)` wrapper'ı `if (showDebugLogs)`'u içeride kontrol ediyor; çağıran taraf interpolasyonu zaten yapmış
  oluyor. Per-frame yollarda örnekler: `CustomerManager.cs` `EnsureDayInitialized`/`CheckEndOfDayCustomerExit`, `PhoneCallManager.Update` (2).
- **Çözüm:** 20 wrapper'a `[System.Diagnostics.Conditional("CARGOR_VERBOSE_LOGS")]` → define yoksa çağrı VE argüman ifadesi derleyici
  tarafından silinir (sıfır maliyet). Editor'de görmek isteyen `Scripting Define Symbols`'a `CARGOR_VERBOSE_LOGS` ekler.
- **Risk:** Düşük-orta: Editor'de `showDebugLogs` inspector bayrağı define olmadan etkisiz kalır → teşhis alışkanlığı değişir. Alternatif (daha
  dar): yalnız per-frame çağrı yerlerini `if (showDebugLogs)` ile sarmak.

### 6. Per-frame `GetComponent`  · risk: SIFIR
- `StaminaBar.cs:26` `loadingBarImage.GetComponent<Image>()` her frame; `NextDayUIManager.cs:184` Update → helper'da 2 `GetComponent`.
- **Çözüm:** Awake'de önbellekle. Küçük ama bedava.

### 7. Kod kalitesi (perf değil)
- `Resources.Load<GameEconomySettings>("EkonomiAyarlari")` fallback'i **12 dosyada** kopyalanmış; tek statik `GameEconomySettings.LoadDefault()`
  erişimcisi olmalı. `Table.cs:1129` `$"Items/{itemName}"` ile her çağrıda string + Resources.Load (Unity içi cache var, yine de gereksiz).
- 2000+ satırlık sınıflar: `UpgradePanel.cs` 2172, `CustomerAI.cs` 2108, `SteamManager.cs` 1915, `CustomerManager.cs` 1625 — partial split adayı.
  Büyük diff, davranış riski; bu turda önerilmez.
- `LobbyDifficultyDisplay` polling yerine artık var olan `DifficultyManager.OnDifficultyChanged` event'ine abone olabilir.

## Önerilen uygulama seti (5 madde)
1. Madde 2 (stack trace kapatma) — ayar, güvenli.
2. Madde 3 (Outline normal önbelleği) — kod, güvenli.
3. Madde 4 (NetworkVariable eşik) — kod, güvenli.
4. Madde 6 (GetComponent önbelleği) — kod, güvenli.
5. Madde 1 (varsayılan kalite) — **kullanıcı seviyeyi seçtikten sonra**.

Her madde sonrası: `refresh_unity` + derleme + console CS taraması; madde 1 için ek olarak Play'de render scale/MSAA teyidi.
