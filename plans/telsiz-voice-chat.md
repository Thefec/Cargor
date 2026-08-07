# Cargor — Oyun İçi Telsiz (Bas-Konuş Sesli İletişim)

## Context

Cargor 1-4 oyunculu co-op depo simülasyonu. Şu an 4 oyuncu **aynı açık depoda** ve herkes her şeyi görüyor — yani **konuşmak için oyunsal bir sebep yok**. Kullanıcının hedefi iki aşamalı: (1) telsizle sesli iletişim, (2) sonrasında oda-bazlı görünürlük (aynı odada olmayan oyuncular birbirini görmez). İkinci aşama birinciye *sebep* yaratıyor: birbirini görmüyorsan tarif etmek zorundasın.

**Bu plan yalnızca 1. aşamayı (telsiz) kapsar.** Oda görünürlüğü ayrı bir iş; oda sayısı sabit değil (haritaya göre değişecek), o yüzden bu tasarım oda sistemine hiçbir varsayım dayatmıyor — aşağıdaki iki karar bunu garantiliyor: ses yolu **player objesine hiç dokunmuyor**, "kim konuşuyor" göstergesi **ekran-uzayı** (dünya-uzayı etiket, konuşmacı gizlendiğinde tam da telsizin gerektiği anda kaybolurdu).

**Neden bu iş kolay:** proje `FacepunchTransport` kullanıyor, yani Steam zaten çalışır durumda olmak zorunda. Steam'in ses API'sı vendor'lanmış DLL'de mevcut — **mikrofon yakalama, Opus sıkıştırma ve açma bize hazır geliyor.** Telsiz ayrıca proximity voice'tan kolay: ses 2D, mesafe/spatial hesabı yok.

**Ekonomiye etkisi yok.** `EconomyInvariantCheck` 179/179 kalmalı; oradaki herhangi bir değişim kırmızı bayrak.

---

## Tasarımı belirleyen doğrulanmış gerçekler

| Gerçek | Kanıt | Sonucu |
|---|---|---|
| NGO parça-bölünmemiş mesaj tavanı **1296 B** (`1300 & ~7`) | `NetworkMessageManager.cs:113` (müdür teyitli) | Ses yükü ≤800 B kapaklanır |
| `FacepunchTransport` **`UnreliableSequenced`'i de düz `Unreliable`'a çeviriyor** | `Assets/SteamWorks/Runtime/FacepunchTransport.cs:110-121` (müdür teyitli) | **Kendi sequence'ımız zorunlu** |
| RPC `byte[]` parametresi her alışta yeni dizi ayırıyor | `FastBufferReader.cs:857`, `:869` | `[Rpc]` reddedildi → `CustomMessagingManager` |
| Client yalnızca server'a custom mesaj gönderebilir | `CustomMessageManager.cs:71-82` | Relay server-authoritative, spoofing kapalı |
| Host, alıcı listesinde kendi ID'si varsa handler'ı **yerelde** çağırır | `CustomMessageManager.cs:100-113`, `:137-148` | Host kendini duyar — iki yerde dışlama şart |
| `ReadVoiceDataBytes` her çağrıda dizi ayırıyor; Facepunch `ReadVoiceData(Stream)` öneriyor | `Facepunch.Steamworks.Win64.xml:2446-2460` | Stream yolu seçildi |
| `ReadVoiceData` "frame başına bir kez, saniyede 4'ten az OLMASIN" | aynı XML `:2451-2452` | Yoklama 30 Hz |
| `DecompressVoice` çıktısı tek kanal 16-bit PCM, decoder 11025-48000 | aynı XML `:2462-2466` | Örnekleme hızı eşlemesi §4 |
| **Projede AudioMixer YOK** (0 `.mixer`) | müdür teyitli | Filtre komponentleri, mixer getirilmeyecek |
| Test asmdef `overrideReferences: true`, yalnız `NewCss.Roguelite` görüyor | `Assets/Tests/EditMode/Cargor.Tests.EditMode.asmdef` (müdür teyitli) | Kırılgan mantık ayrı asmdef'e |
| `V` tuşu boş (bağlı: mouse0/1/2, E, LShift, Z, X, C, WASD) | `InputBindingManager.cs:59-70` (müdür teyitli) | PTT default = `V` |
| Binding kalıcılığı `Defaults` üzerinden otomatik dönüyor | `InputBindingManager.cs:90-114`, `:313-326` | Yeni enum için persistence kodu **gerekmiyor** |
| Rebinding satırları prefab'da elle authoring (`keyBindingRows`) | `UnifiedSettingsManager.cs:559-580` | Yeni satır otomatik görünmez, elle eklenecek |
| Tutorial sahnesinde **NetworkManager/GameStateManager YOK** | `Assets/Scenes/Tutorial.unity` grep | Sahne-yerleşimli obje olmaz → bootstrap singleton |
| DSP buffer 1024 örnek (~21.3 ms), çıkış hızı sistem varsayılanı (`m_SampleRate: 0`) | `ProjectSettings/AudioManager.asset` | 48000 **hardcode edilmez** |
| `Discord.VoiceManager` tipi zaten var (kullanılmıyor) | `Assets/Scripts/Discord/Core.cs:4035` | Hepsi `RadioVoice*` öneki alır |

---

## Mimari

```
Assets/NewCss/Voice/
  Core/                          ← YENİ asmdef: NewCss.Voice.Core
    NewCss.Voice.Core.asmdef       autoReferenced:true, noEngineReferences:true
    PooledByteStream.cs            önceden ayrılmış byte[] üzerinde alloc'suz Stream
    VoicePacket.cs                 4 byte başlık encode/decode (saf)
    VoiceSequenceTracker.cs        dup / reorder / gap (saf)
    VoiceRingBuffer.cs             jitter buffer + underrun/overrun/drift (saf)
    VoiceBufferPolicy.cs           eşikler (saf)
  RadioVoiceRuntime.cs           bootstrap singleton, yaşam döngüsü, alt sistem sahibi
  RadioVoiceCapture.cs           VoiceRecord + okuma + paketleme
  RadioVoiceTransport.cs         named message kayıt/gönder/al + host relay
  RadioVoicePlayback.cs          konuşmacı slot havuzu, roster yönlendirme
  RadioVoiceSpeakerSlot.cs       AudioSource + streaming AudioClip + filtreler
  RadioVoicePrefs.cs             PlayerPrefs (static)
  UI/  RadioHudController.cs · RadioSpeakerRow.cs · RadioHUD.prefab · RadioSpeakerSlot.prefab
  DevTools/ RadioVoiceDevTools.cs  #if UNITY_EDITOR — simülatör + kayıt/replay + istatistik
```

**`RadioVoiceRuntime` = kendi kendini kuran `DontDestroyOnLoad` singleton, düz `MonoBehaviour`** (`NetworkBehaviour` DEĞİL). Mevcut deseni yeniden kullan: `Assets/NewCss/UIScripts/WorldSpaceCanvasCameraBinder.cs:26-32` (`[RuntimeInitializeOnLoadMethod]` + `HideFlags.HideAndDontSave`).

Gerekçe: (a) Tutorial'da NetworkManager yok — sahne-yerleşimli obje iki sahnede iki wiring demek; (b) `PlayerSpawner.cs:301/319` spawn asenkron, PTT'ye spawn'dan önce basılabilir; (c) `CustomMessagingManager`'a düz MonoBehaviour'dan erişilir → NetworkObject/ownership/`OnNetworkSpawn` timing'i/late-join replay'i **hiçbiri gerekmiyor**. Bu, bu projede `UpgradePanel.cs:361-378`'de belgelenmiş bug sınıfını (late-join'de `OnListChanged` replay etmiyor) baştan devre dışı bırakıyor — ses akışında senkronize edilecek durum yok, sadece geçici veri var.

**`NewCss.Voice.Core` neden ayrı asmdef:** test asmdef `overrideReferences: true` ile Assembly-CSharp'ı göremiyor. Kırılgan mantığın **tamamı** (ring buffer, sequence, header) motor referansı olmayan bu assembly'ye konur ve %100 headless test edilir.

### Mevcut dosyalara dokunuşlar (hepsi eklemeli, davranış değiştirmeyen)

| Dosya | Dokunuş |
|---|---|
| `Assets/NewCss/InputBindingManager.cs` | `GameAction.PushToTalk` (`:12-26`) · `Defaults` → `new Binding(KeyCode.V)` (`:57-71`) · `GetActionDisplayName` case (`:267-285`) |
| `Assets/MENUUI/UnifiedSettingsManager.cs` | `PREF_VOICE_*` · `SettingsData` alanları + `Clone()`/`Equals()` · slider/toggle · `SetupAudioControls` · `ApplyAudioSettings` (`:1065`) · `LoadSettings` · `SaveToPlayerPrefs` (`:1297`) |
| `Assets/NewCss/GameState/GameStateManager.cs` | **tek yeni metod** `TryGetRosterName(ulong, out string)` (~10 satır) — `_playerRoster` private, mevcut `GetRosterPlayerNames()` (`:236-244`) ClientId'yi düşürüyor |
| `Assets/Tests/EditMode/Cargor.Tests.EditMode.asmdef` | `references`'a `"NewCss.Voice.Core"` |
| 3 lokalizasyon asset'i | 10 anahtar (aşağıda) |
| Ayarlar prefab'ı | 1 slider + 2 toggle + 1 keybinding satırı authoring |

**Karakter prefab'ına, `PlayerSpawner`'a, `PlayerMovement`'a HİÇ dokunulmuyor.**

---

## Beş kritik karar

**1. Yakalama: sabit 30 Hz, `Time.unscaledTimeAsDouble` ile.**
Her frame olmaz (200 fps'te 10 B yük / ~18 B overhead = %180 israf + saniyede 400 Steam çağrısı). 4 Hz de olmaz (Facepunch "gaps in the stream" uyarısı + 250 ms ≈ 2000 B, 1296 sınırını aşar). 30 Hz'de yük ~70-270 B, overhead %7-25.
**`Time.time`/`deltaTime` YASAK** — hem duraklatma (`EscapeMenuManager.cs:404`) hem oyun sonu (`GameStateManager.cs:109/115`) `timeScale = 0` yapıyor; ölçekli zamanla menüde konuşma donar ve Steam iç tamponu taşar. Sapma birikmesin diye `_next += kInterval` (asla `= now + kInterval`).
**Tail zorunlu:** PTT bırakılınca hemen `VoiceRecord = false` yapılırsa **son hece kesilir** (PTT sistemlerinin klasik hatası). 200 ms okumaya devam et, sonra kapat.
Her Steam çağrısından hemen önce `SteamClient.IsValid` (deseni `SteamManager.cs:289`) — `FacepunchTransport.OnDestroy()` (`:65-68`) `SteamClient.Shutdown()` çağırıyor ve yıkım sırası garanti değil.

**2. Taşıma: `CustomMessagingManager.SendNamedMessage("CargorVoice")`, `NetworkDelivery.Unreliable`.**
`[Rpc]` reddedildi: `byte[]` parametresi her alışta dizi ayırıyor (`FastBufferReader.cs:857/869`) → 30 Hz × 3 konuşmacı = saniyede 90 çöp dizi. `FastBufferWriter`/`ReadBytesSafe` ile **sıfır managed alloc**.
`Reliable` reddedildi: kaybolan ses paketinin retransmit'i arkasındaki **oyun durumu** mesajlarını bekletir (head-of-line); 300 ms gecikmiş ses zaten işe yaramaz.
Akış: konuşan client → server (4 B başlık) → server 8 B `senderClientId` **ekler** → `ConnectedClientsIds \ {gönderen, host_konuşuyorsa_kendisi}`. **İki dışlama da şart**, yoksa host gecikmeli kendi sesini duyar (sessiz, tanısı zor bug).
Paket başlığı 4 B: `Version`(1) + `Flags`(1: BurstStart/End/Continuation) + `Sequence`(2, uint16 sarmalı). Gönderen kimliği yükte **yok** → spoofing kapalı.
Yük **≤800 B** kapaklanır (aşan okuma çoklu pakete bölünür): NGO sınırı 1296 muhtemelen Steam'in tek-datagram eşiğinin üstünde, ve **unreliable mesajda tek fragment kaybı tüm mesajı düşürür** (`Facepunch...Win64.xml:162-164`).
Bant genişliği (R=4 KB/s tahmin): en kötü (4'ü birden) host **~36 KB/s upload**; gerçekçi (1-2 konuşan) 8-16 KB/s.

**3. Çalma: streaming `AudioClip` + `PCMReaderCallback`, kalıcı çalan `AudioSource`.**
`PlayOneShot` zinciri reddedildi: paket başına klip alloc + 33 ms'lik her sınırda click + `m_RealVoiceCount: 32` bütçesini yer.
Belirleyici avantaj: AudioSource sürekli `isPlaying` kalır → filtre DSP zinciri hiç kurulup yıkılmaz, konuşma başında "ısınma" artefaktı olmaz.
**Örnekleme hızı — hiçbir yerde yeniden örnekleme olmasın.** `AudioSettings.outputSampleRate` oku (48000 hardcode YASAK, `m_SampleRate: 0`), `SteamUser.SampleRate`'i ona ayarla, `AudioClip.Create(frequency: fiilen ayarlanan hız)`. **Clip frequency'sini fiili üretim hızından farklı bırakmak perdeyi kaydırır — "robot ses"in ikinci en yaygın sebebi.** `AudioSettings.OnAudioConfigurationChanged` aboneliği zorunlu (kullanıcı kulaklık takarsa herkes yanlış perdeden çalar).

**4. Jitter buffer — sistemin en kırılgan parçası.**
Slot başına SPSC kilitsiz float ring buffer (`NewCss.Voice.Core.VoiceRingBuffer`). Ana thread yazar, audio thread okur; iki `int` indeks + `Volatile.Write/Read` yayınlama bariyeri.
**`PCMReaderCallback` içinde KESİNLİKLE YASAK:** `lock`, allocation, `Debug.Log`, herhangi bir Unity API, LINQ. (`lock` alınırken ana thread GC pause'a girerse audio thread bloke olur → duyulabilir dropout.) Bu kural koda yorum olarak yazılacak. Sayaçlar `Interlocked.Increment`.

| Parametre | Değer | Gerekçe |
|---|---|---|
| Kapasite | 1.0 s (`outRate` float) × 3 slot | Bir kez ayrılır; taşmayı nadir kılar |
| **Hedef gecikme (prebuffer)** | **120 ms** | DSP bloğu 21.3 ms × ≥3 pay + 1 yakalama aralığı (33 ms) + Steam relay jitter'ı. Telsiz gecikmeyi diegetik olarak mazur gösteriyor. **Dev-tunable, kullanıcı ayarı değil.** |
| Overrun tavanı | hedef + 250 ms | Üstünde **en eskiyi** at, `_read`'i `_write − hedef`'e zıplat |
| Drift eşikleri | hedef +60 ms / −40 ms, 1 s sürerse | 10 ms parça düşür / 10 ms sessizlik ekle |

**Playout kapısı (en önemli tek kural):** slot hedef gecikme kadar örnek biriktirmeden **çalmaya başlamaz**. "Cızırtı/robot ses" şikayetlerinin çoğu bu kapı yokken ilk paketin hemen çalınıp arkasının yetişmemesinden doğar.
**Underrun:** sıfır yaz, `_read`'i `_write`'ın ötesine asla geçirme, girişte/çıkışta **~2 ms kosinüs fade** (ani sıfıra atlama = duyulan click; kullanıcının "cızırtı" dediği şeyin doğrudan çözümü), kapıyı **yeniden kur**. Pitch/gerdirme yapılmaz.
**Overrun:** en eskiyi at (en yeniyi atmak gecikmeyi sonsuza büyütür — klasik hata), zıplama noktasında ~2 ms crossfade.
**Burst sonu:** `BurstEnd` flag'i **veya** 400 ms sessizlik zaman aşımı — flag unreliable, **asla yalnızca flag'e güvenme**.
Slot havuzu init'te 3 adet; runtime'da **hiç** `Instantiate`/`AddComponent` yok. `spatialBlend = 0`, `dopplerLevel = 0`, `priority = 0`.

**5. Radyo efekti: yerleşik filtre komponentleri, mixer getirilmeyecek.**
`RadioSpeakerSlot.prefab` üzerinde DSP sırasıyla `AudioHighPassFilter` 300 Hz → `AudioLowPassFilter` 3400 Hz (bonus: codec artefaktlarını maskeler) → `AudioDistortionFilter` 0.18. Hepsi `[SerializeField]`, hardcode yok.
Mixer eklemek projedeki *her* AudioSource'u gruplara yönlendirmek demek (PlayerInventory, PhoneCallManager, UI, müzik) → kapsam dışı, takip maddesi.
Klikler: slot prefab'ının **`Clicks` child'ında** ayrı filtresiz AudioSource (aynı GameObject'te iki AudioSource filtre atamasını belirsizleştirir); klipler pre-baked asset. **Yerel geri bildirim:** PTT'de aynı klikler yerelde kısık çalınır — "mikrofonum açık mı" sorusunun tek cevabı.

---

## UI ve ayarlar

- **HUD:** `RadioHUD.prefab`, kendi Screen-Space-Overlay Canvas'ı, bootstrap'ta instantiate + `DontDestroyOnLoad`. **Ekran-uzayı liste** (dünya-uzayı etiket açıkça reddedildi — bkz. Context). Burst bitince satır ~300 ms tutulur, sonra fade (tıkırdayan akış listeyi yanıp söndürmesin).
- **İsim kaynağı:** `GameStateManager`'ın replike `NetworkList<PlayerRosterEntry>`'si (`:47`, `PlayerRosterEntry.cs:14-15`) + `OnRosterChanged` (`:49`). **`SteamIdHolder` reddedildi** — `SteamId` düz property + ServerRpc (`SteamIdHolder.cs:6`, `:18-22`), replike DEĞİL, uzak client'larda `0`. Steam lobi üyeleri de reddedildi (`GameStateManager.cs:41-46` yorumu tutarsız olduğunu belgeliyor).
- **Mute:** HUD satırına tıkla, **v1'de oturum-içi `clientId` bazlı.** Kalıcı (SteamId) mute `clientId ↔ SteamId` replikasyonu ister, `SteamIdHolder` bozuk → bilinçli kapsam kesintisi, takip maddesi.
- **Ses seviyesi:** `RadioVoicePrefs` **doğrudan PlayerPrefs'ten** okur (`UnifiedSettingsManager` singleton değil, `FindObjectOfType` ile bulunuyor ve her sahnede yok). Master ile **ÇARPILMAZ** — `ApplyAudioSettings()` zaten `AudioListener.volume = master` yapıyor (`:1071`). ⚠️ `PlayerInventory.Audio.cs:60` bunun üstüne bir kez daha çarpıyor = master iki kez uygulanıyor; **mevcut bir hata, kopyalanmayacak.**
- **PTT rebinding:** persistence otomatik; ama satır `keyBindingRows`'a **elle authoring** edilmeli (`UnifiedSettingsManager.cs:559-580`). `GetActionDisplayName` hardcoded Türkçe (`:267-285`) → yeni giriş `LocalizationHelper.GetLocalizedString("ControlPushToTalk")` ile verilir (diğer 12'si takip maddesi). Bastırma: rebinding yakalaması (`_isWaitingForKey`) sırasında evet; menü/harita açıkken **hayır** (o sırada konuşmak istenir).

**10 lokalizasyon anahtarı** (`SettingsVoiceVolume`, `SettingsVoiceEnabled`, `SettingsVoiceSelfMonitor`, `ControlPushToTalk`, `VoiceHudTransmitting`, `VoiceHudMuted`, `VoiceHudUnknownPlayer`, `VoiceErrorNoSteam`, `VoiceErrorNoMic`, `VoiceHintPushToTalk`) — üç asset'e de (Shared Data + tr + en).
⚠️ **Window ▸ Asset Management ▸ Localization Tables üzerinden ekle, YAML'ı elle düzenleme.** Elle yazılan `m_Id` çakışırsa girdi sessizce düşer ve `LocalizationHelper` anahtarın kendisini döndürür — bu projede daha önce "İngilizce build'de Türkçe metin" bugu olarak yaşandı.

---

## Yaşam döngüsü — en tehlikeli nokta

`SteamUser.VoiceRecord` **süreç-global ve mikrofonu açık tutar.** Bir ScriptableObject sızıntısından kötü: tek bir kaçan temizlik yolu = ana menüye döndükten sonra mikrofon açık kalması.

**Tek, idempotent `TeardownVoice()`** — `VoiceRecord = false` + handler iptali + event unsubscribe + slot temizliği. Çağrıldığı yerler: `OnDisable`, `OnDestroy`, `OnApplicationQuit`, `OnClientStopped`, `OnServerStopped`, `activeSceneChanged`. `_teardownDone` flag'i korur. **İçinde bile her Steam çağrısı öncesi `IsValid`** (`FacepunchTransport.OnDestroy()` bizden önce `Shutdown()` çağırmış olabilir).
Bu, projede zaten kurulmuş ikili-temizlik disiplininin aynısı: `UpgradePanel.cs:346-353`, `:438-451` (perk snapshot restore).

Diğer durumlar: Steam geçersiz → `Disabled(NoSteam)`, mesaj **oturum başına bir kez**, 5 s'lik yavaş retry · mikrofon yok (PTT 1.5 s basılı, 0 byte) → `Degraded(NoMicData)`, bir kez, log spam yasak · tek oyuncu → **kayıt YAPILIR** (HUD + Steam tamponu boşalsın) ama paket **düşürülür** · handler kaydı `Awake`'te DEĞİL, `OnClientStarted`/`OnServerStarted`'da (`CustomMessagingManager` öncesinde `null`) · konuşan disconnect → slot serbest + **~15 ms fade-out** (hard stop = click).

---

## Uygulama sırası

Kritik yol **2 → 3 → 4 → 6**. Adım 1, 5, 7, 8, 9 paralelleştirilebilir.

| # | Adım | Bitince doğrulanan |
|---|---|---|
| 1 | Input + prefs + 10 lokalizasyon anahtarı (ses yok) | Rebinding satırı tuşu gösteriyor, prefs round-trip, iki dil doğru |
| **2** | **`NewCss.Voice.Core` asmdef + saf mantık + ~25 EditMode testi** | EditMode yeşil, mevcut 9 hâlâ geçiyor. **Kırılgan mantığın tamamı burada ve %100 headless** |
| 3 | Sadece yakalama, ağ yok — paket üretilir ve loglanır | **Gerçek bitrate ÖLÇÜMÜ** · PTT basılıyken Profiler'da **0 B/frame alloc** (kabul kriteri) · her çıkışta `VoiceRecord` false |
| **4** | **Loopback çalma: playback + slot + streaming clip + ring buffer. En büyük ve en riskli adım.** | Kendini ~120 ms gecikmeyle duyuyorsun, cızırtı yok, 5 dk'da drift yok |
| 4b | Paket kaydet/oynat (dev) — adım 6'nın doğrulaması buna dayanıyor | Kaydedilen akış aynı sesi veriyor |
| 5 | Radyo filtreleri + klikler | Loopback'te kulakla |
| 6 | Ağ taşıması: named message, relay, dışlamalar, 800 B kapak, sequence | 2 instance (`LocalCoopTestBootstrap`), konuşan taraf 4b'nin canned akışını kullanır |
| 7 | HUD + `TryGetRosterName` + mute | 2 instance: doğru isim/satır, mute çalışıyor |
| 8 | Ayarlar UI wiring (prefab authoring dahil) | Değiştir → restart → kalıcı; seviye yalnız telsizi etkiliyor |
| 9 | Dev araçları: ağ simülatörü (gecikme/jitter/kayıp/dup/reorder) + istatistik overlay | **%10 kayıp + 80 ms jitter altında ses anlaşılır** |
| 10 | Hata durumları + teardown denetimi | Kasten boz: Steam'i öldür, mikrofonu kapat, yayın ortasında Alt-F4 / sahne değiştir |

---

## Doğrulama

**A — Headless EditMode** (`-runTests -testPlatform EditMode`). Yalnız adım 2 test edilebilir **ama cızırtı tam orada yaşıyor.**
`VoiceRingBuffer`: sarmalama · underrun sessizlik döndürüyor **ve** prebuffer kapısını yeniden kuruyor · overrun **en eskiyi** düşürüp gecikmeyi tam hedefe getiriyor · fade örnek sayıları · drift kararları eşiklerde.
`VoiceSequenceTracker`: uint16 sarmalaması (65535→0) · dup yok sayılıyor · pencere içi reorder kabul, dışı düşük · `BurstEnd` kaybolunca zaman aşımı.
`VoicePacket`: round-trip · bilinmeyen `Version` düşüyor · bozuk buffer'da exception atmadan `false`.
`PooledByteStream`: kapasite aşımında `Truncated` işaretliyor, atmıyor.
Regresyon kapısı: **`EconomyInvariantCheck` 179/179.**

**B — Tek makine, tek instance (loopback).** **Özelliğin ~%70'i burada.** Yakalanan paket ağa çıkmadan yerel slot'a beslenir; dev tool'la yapay gecikme/jitter/kayıp enjekte edilir. Doğrulanan: `VoiceRecord` yaşam döngüsü + tail · `DecompressVoice` · **perde doğru mu** · filtreler/klikler · PTT + rebinding + bastırma · ayar kalıcılığı · iki dil · 0 GC alloc · **tüm teardown yolları (mikrofon kapanıyor mu)**.

**C — Tek makine, 2 instance.** `LocalCoopTestBootstrap` (127.0.0.1) + Multiplayer Play Mode. Doğrulanan: handler zamanlaması · yönlendirme · **gönderen dışlaması (host kendini duymuyor)** · slot tahsisi · HUD isimleri · mute · burst ortasında disconnect · late join.
Mikrofon paylaşımını aşmak için **konuşan instance canlı mikrofon yerine 4b'nin disk replay'ini kullanır** (çakışma + tekrar-üretilebilirlik birlikte çözülür).
⚠️ Bilinen sınır: bootstrap `UnityTransport`'a geçiyor (`:97-105`) → **Steam relay baypas edilir**, gerçek gecikme karakteri test EDİLMEZ.

**D — 2 makine, 2 Steam hesabı (ZORUNLU, alternatifi yok).** Steam Voice tek Steam client'ına bağlı. Yalnızca burada: gerçek relay gecikmesi/jitter/kayıp · uçtan uca kalite · **20+ dk oturumda saat drift'i (drift eşiklerinin ayarı)** · 3-4 eşzamanlı konuşmacı · farklı mikrofon donanımı · farklı `outputSampleRate`'li iki makine.

**Headless test EDİLEMEYENLER:** `AudioSettings`, `AudioClip.Create`, `PCMReaderCallback`, filtreler, audio thread. **DSP yolu için PlayMode testi yazılmayacak** (flaky olur) — ring buffer'ın dışa verdiği sayaçlar (underrun/overrun/drop/doluluk) üzerinden assert edilir, o mantık Katman A'da zaten test edilmiş.

---

## Riskler ve doğrulanmamış varsayımlar

1. **Gerçek Steam bitrate'i (EN ÖNEMLİ).** 2-8 KB/s tahmini Steam dokümanından, **bu projede ölçülmedi.** Aşağı akıştaki her şey (paket boyutu, MTU payı, host relay maliyeti) buna bağlı. → **Adım 3'ün log çıktısı ölçümdür**; 800 B kapağı ve 30 Hz revize edilebilir.
2. **Facepunch C# imzaları.** XML'den doğrulandı: `VoiceRecord`, `HasVoiceData`, `ReadVoiceData(Stream)`, `ReadVoiceDataBytes`, `DecompressVoice(Stream,int,Stream)`. Yalnız **DLL metadata dizesi** olarak görüldü (imza DEĞİL): `set_SampleRate`, `get_OptimalSampleRate`, `nDesiredSampleRate`. → **Adım 3, 5 satırlık derleme probuyla başlar.** Plandaki hiçbir karar değişmiyor, sadece kod şekli.
3. **Aynı makinede/hesapta iki Steam-voice süreci — DOĞRULANMADI.** Adım 6'nın ne kadarının yerelde doğrulanabileceğini belirliyor. Olmazsa adım 6 doğrulaması Katman D'ye kayar.
4. **`PCMReaderCallback` + `stream: true` Unity 6000.5.6f1'de — DOĞRULANMADI** (tarihsel olarak sağlam ama 6000.x yeni). Fallback: sessiz döngüsel klip üstünde `OnAudioFilterRead`.
5. **Steam'in ~1200 B tek-datagram eşiği** SDK dokümanından, repoda doğrulanamadı. Yanlışsa 800 B kapağı sadece gereksiz muhafazakâr — güvenli yönde hata. (NGO'nun 1296'sı **kesin**.)
6. **Saat drift'inin büyüklüğü** yerelde ölçülemez; eşikler 20 dk'lık iki-makine oturumunda ayarlanacak.
7. **`FacepunchTransport` `NoNagle` kullanmıyor** (`:110-121`) → her unreliable gönderim Steam Nagle timer'ını bekliyor. 120 ms buffer yanında önemsiz ama **ayarlanamaz gecikme tabanı.** Vendor'lanmış transport'a yama = yükseltme borcu, kapsam dışı.
8. **`m_RealVoiceCount: 32`** — kalabalık sahne bütçeyi doyurursa ses AudioSource'ları sanallaştırılabilir. `priority = 0` ile hafifletildi, **yük altında doğrulanmadı.**
9. **Ayar panelinin hangi prefab/sahnede olduğu** netleşmemiş (`EscapeMenuManager.cs:82` referans tutuyor). Ses seviyesi doğrudan PlayerPrefs'ten okunduğu için sistem etkilenmiyor; adım 8'de netleşecek.

**Takip maddeleri (bu işin kapsamı dışı):** AudioMixer mimarisi · kalıcı SteamId-bazlı mute · `GetActionDisplayName`'in kalan 12 aksiyonunun lokalizasyonu · `PlayerInventory.Audio.cs:60` çifte master çarpımı · `FacepunchTransport` `NoNagle`.

---

## Sonraki iş (bu planın kapsamı dışı)

**Oda-bazlı görünürlük.** Kullanıcı kararı: harita görünümünde (X) **kendi odası tam renk, diğer odalar desatüre + karartılmış, oyuncular hiçbir odada görünmez, kutular/işler soluk ama görünür.** Oda sayısı **sabit değil** — veri-güdümlü olmalı.
Bu telsiz tasarımı o işi engellemiyor: ses yolu player objesine dokunmuyor, HUD ekran-uzayı, isim `ConnectedClientsIds` + roster'dan geliyor. `NetworkHide` ile gelse bile ayakta kalır.
O iş başlarken bilinmesi gerekenler (keşifte çıktı): sahnede **oda kavramı yok** ve 13 rafın tamamı tek ızgarada (x -40.5…-57.6), 2 paketleme masası en yakın rafa ~2 birim → **level yeniden bölünmesi gerekiyor**; post-process ekran geneli olduğu için karartma **custom `ScriptableRendererFeature` + shader'a `_Desaturate`** ister (`FlatLitEnvironment.shader`'da `Luminance3()` ve `shader_feature_local` deseni hazır, 19 boş layer var); ve **`kutu/dk/oyuncu` düştüğü için economist turu ZORUNLU.**
