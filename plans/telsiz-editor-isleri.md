# Telsiz — Unity Editor'de Elle Yapılacak İşler

> **NOT:** Bu dosya `plans/telsiz-voice-chat.md` planının kod-dışı (Unity Editor authoring) adımlarını topluyor. Kod tarafı subagent'larla ilerlerken bazı işler (lokalizasyon tabloları, prefab authoring, StringTable elle düzenlenemez) yalnızca Editor'de elle yapılabiliyor. **Sonraki dalgalar (prefab/SFX/ayarlar UI wiring) bu dosyaya yeni bölüm olarak ekleme yapacak** — şu an yalnızca Dalga 1 kapsamı olan **lokalizasyon** bölümü dolu.

---

## 1. Lokalizasyon — 10 yeni anahtar (Dalga 1)

### Neden elle YAML düzenlenmiyor
`StringTable*.asset` dosyaları `.yaml` formatında ama içeriği Unity Localization paketinin kendi iç ID sistemine (`m_Id`, `SharedTableData` GUID eşlemesi) bağlı. Elle bir satır eklenirse ve `m_Id` çakışırsa:
- Girdi **sessizce düşer** (hata vermez, derleme de bozulmaz).
- `LocalizationHelper.GetLocalizedString(key)` (`Assets/NewCss/Localization/LocalizationHelper.cs:79-105`) o anahtarı bulamaz ve **anahtarın kendisini** (örn. `"ControlPushToTalk"`) ekranda gösterir.
- Bu, bu projede daha önce **"İngilizce build'de Türkçe metin görünmesi"** bug sınıfının aynısı — sessiz ve fark edilmesi zor.

Bu yüzden 10 anahtar da **Window ▸ Asset Management ▸ Localization Tables** editör penceresi üzerinden eklenecek.

### Etkilenen 3 asset
- `Assets/LocalSettings/Tables/StringTable Shared Data.asset` — anahtar ID eşlemesi (tüm dillerin paylaştığı ortak tablo)
- `Assets/LocalSettings/Tables/StringTable_tr.asset` — Türkçe metinler
- `Assets/LocalSettings/Tables/StringTable_en.asset` — İngilizce metinler

(`Assets/LocalSettings/Tables/StringTable.asset` bu ikisini + shared data'yı işaret eden **koleksiyon** asset'i; doğrudan elle bir şey eklenmeyecek, pencere üzerinden otomatik güncellenir.)

### Adımlar
1. Unity'de üst menüden **Window ▸ Asset Management ▸ Localization Tables** aç.
2. **String Tables** sekmesinde, koleksiyon listesinde **`StringTable`** satırını bul (Shared Data: `StringTable Shared Data`).
3. Tablo görünümünde sağ üstteki **"+"** (Add New Entry) ile aşağıdaki 10 anahtarı **tek tek, aşağıdaki isimlerle harfiyen** ekle (büyük/küçük harf duyarlı, boşluksuz):
   - `SettingsVoiceVolume`
   - `SettingsVoiceEnabled`
   - `SettingsVoiceSelfMonitor`
   - `ControlPushToTalk`
   - `VoiceHudTransmitting`
   - `VoiceHudMuted`
   - `VoiceHudUnknownPlayer`
   - `VoiceErrorNoSteam`
   - `VoiceErrorNoMic`
   - `VoiceHintPushToTalk`
4. Her anahtar eklendiğinde tablo görünümünde **tr** ve **en** kolonları belirir — aşağıdaki tabloyu satır satır doldur (kopyala-yapıştır).
5. Kaydet (pencere genelde otomatik kaydeder; emin olmak için `Ctrl+S` / **File ▸ Save Project**).
6. Doğrulama: Oyunu Türkçe ve İngilizce locale ile aç, ayarlar/telsiz ekranlarında hiçbir yerde anahtar adının kendisinin (örn. çıplak `"ControlPushToTalk"` yazısı) görünmediğini teyit et — göründüğü yer varsa o anahtar tabloya yanlış yazılmış demektir.

### Metin tablosu (tr / en)

| Anahtar | Türkçe (tr) | İngilizce (en) | Not |
|---|---|---|---|
| `SettingsVoiceVolume` | Telsiz Sesi | Radio Volume | Ayarlar panelinde slider etiketi |
| `SettingsVoiceEnabled` | Telsiz | Radio Voice | Ayarlar panelinde toggle etiketi (telsizi tamamen aç/kapat) |
| `SettingsVoiceSelfMonitor` | Kendini Dinle | Self Monitor | Ayarlar panelinde toggle etiketi (loopback) |
| `ControlPushToTalk` | Bas-Konuş | Push to Talk | Rebinding satırı etiketi — `InputBindingManager.GetActionDisplayName` bunu `LocalizationHelper` üzerinden çözüyor, tablo boşsa kod otomatik "Bas-Konuş"a düşer |
| `VoiceHudTransmitting` | {0} konuşuyor | {0} speaking | HUD satırı, `{0}` = oyuncu adı (format parametresi, `GetLocalizedStringFormat` ile kullanılacak) |
| `VoiceHudMuted` | {0} (Sessize Alındı) | {0} (Muted) | HUD satırı, mute edilmiş konuşmacı için |
| `VoiceHudUnknownPlayer` | Bilinmeyen Oyuncu | Unknown Player | Roster'da isim bulunamazsa HUD fallback'i |
| `VoiceErrorNoSteam` | Steam bağlantısı yok — telsiz kullanılamıyor | No Steam connection — radio unavailable | Steam geçersizken oturum başına bir kez gösterilecek uyarı |
| `VoiceErrorNoMic` | Mikrofon bulunamadı | No microphone detected | PTT basılı ama 1.5s veri gelmezse gösterilecek uyarı |
| `VoiceHintPushToTalk` | Konuşmak için {0}'a basılı tutun | Hold {0} to talk | İpucu metni, `{0}` = güncel PTT tuşu (`GetBindingDisplayName`) |

> Bu metinler taslak — oyunun genel ton/üslup rehberine göre kullanıcı serbestçe değiştirebilir. Değer/ekonomi içermiyorlar (economist onayı gerekmez), sadece UI stringleri.

---

## 3. Klik SFX asset'leri (Dalga 2 — kod null-guard'lı bekliyor)

Kod (`RadioVoiceSpeakerSlot.cs`, `RadioVoiceRuntime.cs`) bu klipleri `Resources.Load<AudioClip>(...)` ile arıyor; **asset yoksa sessizce atlar** (oturum başına bir kez uyarı loglar, log spam yapmaz) — yani sistem SFX'siz de tam çalışır, aşağıdakiler kozmetik bir sonraki-adım.

Beklenen 4 dosya, **tam bu isim ve yol** ile `Assets/Resources/Voice/Clicks/` altına:

| Dosya | Kullanım yeri | Önerilen süre | Karakter |
|---|---|---|---|
| `PttOn.wav` | Yerel: PTT'ye basınca (kendi mikrofonun) | ~60-80 ms | Kısa, net "tık" — telsiz açılış sesi |
| `PttOff.wav` | Yerel: PTT bırakınca (200ms tail sonrası) | ~60-80 ms | `PttOn`'dan hafif farklı (ör. biraz daha pes) — kulakla ayırt edilsin |
| `BurstStart.wav` | Uzak: bir konuşmacı slot'u ilk paketini alınca | ~60-80 ms | `PttOn` ile aynı aile, ama telsiz filtresinden geçmemiş (Clicks child filtresiz) |
| `BurstEnd.wav` | Uzak: bir konuşmacının burst'ü bitince (BurstEnd bayrağı VEYA 400ms zaman aşımı) | ~60-80 ms | `PttOff` ile aynı aile |

Not: `PttOn/PttOff` yerel geri bildirim için `RadioVoiceRuntime`'ın kendi filtresiz `AudioSource`'undan çalar; `BurstStart/BurstEnd` her slot'un `Clicks` child'ındaki filtresiz `AudioSource`'undan çalar — dördü de aynı ses karakterinde olabilir, kod tarafında zaten ayrı kaynaklardan çalınıyor.

Import ayarı: Force To Mono açık, Load Type = Decompress On Load (çok kısa klip), Compression = PCM veya ADPCM.

## 4. Opsiyonel `RadioSpeakerSlot.prefab` authoring (Dalga 2 — şu an kodda kurulu, prefab yok)

**Şu an gerekmiyor** — `RadioVoiceSpeakerSlot.CreateSlot()` (`Assets/NewCss/Voice/RadioVoiceSpeakerSlot.cs`) prefab yoksa AudioSource + AudioHighPassFilter(300Hz) + AudioLowPassFilter(3400Hz) + AudioDistortionFilter(0.18) + "Clicks" child'ı **koddan** kurup çalışır durumda bırakıyor. Bu bölüm, sanatçı/tasarımcı filtre değerlerini **sanatsal olarak** ayarlamak isterse:

1. Boş bir GameObject oluştur, `RadioVoiceSpeakerSlot` komponentini ekle (script Inspector'da `burstStartClip`/`burstEndClip` alanlarını gösterir — istersen buradan da atayabilirsin, `Resources/Voice/Clicks/*` yerine).
2. Aynı GameObject'e sırasıyla (SIRA ÖNEMLİ — DSP zinciri component-ekleme sırasına göre kuruluyor): `AudioSource`, `AudioHighPassFilter`, `AudioLowPassFilter`, `AudioDistortionFilter`.
3. Filtre değerlerini `RadioVoiceSpeakerSlot.cs` içindeki sabitlerle aynı başlat (300 / 3400 / 0.18) — kod, prefab'tan gelen bir instance'ta bu değerleri **override etmiyor** (kod sadece component yoksa ekliyor), yani prefab'a yazdığın değer kalıcı kalır.
4. Child olarak "Clicks" adında boş bir GameObject + üzerine **filtresiz** `AudioSource` ekle (isim harfiyen `Clicks` olmalı, kod `transform.Find("Clicks")` ile arıyor).
5. Prefab'ı `Assets/Resources/Voice/RadioSpeakerSlot.prefab` yoluna kaydet (isim ve yol harfiyen böyle — kod `Resources.Load<GameObject>("Voice/RadioSpeakerSlot")` ile arıyor).
6. Play'e bas, PTT ile test et — kod artık bu prefab'tan 3 kopya Instantiate edecek (koddan kurulum devre dışı kalır).

## 5. Sonraki dalgalarda buraya eklenecek bölümler

### 5.1 Opsiyonel `RadioHUD.prefab` / satır prefab'ı authoring'i (Dalga 3 — şu an kodda kurulu, prefab yok)

**Şu an gerekmiyor** — `RadioHudController.Bootstrap()` (`Assets/NewCss/Voice/UI/RadioHudController.cs`) `Resources/Voice/RadioHUD` prefab'ı yoksa Canvas (Screen Space Overlay, sortingOrder 500) + `VerticalLayoutGroup`'lu bir satır container'ı + 4 satır (1 kendi mikrofon + 3 uzak konuşmacı havuzu) **koddan** kurup çalışır durumda bırakıyor. `RadioSpeakerRow.EnsureBuilt()` de aynı şekilde: bulamadığı her child'ı (`Label`, `LevelBar/LevelFill`) koddan ekliyor. Bu bölüm, sanatçı/tasarımcı görünümü **sanatsal olarak** ayarlamak isterse:

1. Boş bir GameObject oluştur (`Canvas` + `CanvasScaler` + `GraphicRaycaster` ekle), `RadioHudController` komponentini ekle.
2. Inspector'da `rowContainer` alanına, `VerticalLayoutGroup` + `ContentSizeFitter` (Vertical: Preferred Size) taşıyan bir child RectTransform ata — kod bu alan doluysa kendi container'ını KURMAZ.
3. `rowPrefab` alanına, `RadioSpeakerRow` komponentli bir prefab ata (adım 4'e bak). Doluysa kod `CreateRow()` içinde bu prefab'tan Instantiate eder (koddan satır kurulumu devre dışı kalır) — hem kendi mikrofon satırı hem 3 havuz satırı AYNI prefab'tan çoğaltılır.
4. `RadioSpeakerRow` prefab'ı üzerinde SIRA/İSİM ÖNEMLİ (kod `transform.Find` ile arıyor):
   - Kök objede `Image` (arka plan), `CanvasGroup` (fade için), `Button` (mute tıklaması için — kendi mikrofon satırında kod bunu `interactable=false` yapıyor, prefab'ta ayrıca bir şey yapmana gerek yok) bulunmalı/eklenmeli.
   - Child adı harfiyen `Label` → üzerinde `TextMeshProUGUI` (durum metni: "{isim} konuşuyor" / "{isim} (Sessize Alındı)" / çıplak isim).
   - Child adı harfiyen `LevelBar`, onun child'ı harfiyen `LevelFill` → üzerinde `Image` (`Image Type = Filled`, `Fill Method = Horizontal`) — nabız atan seviye çubuğu.
5. Prefab'ı `Assets/Resources/Voice/RadioHUD.prefab` yoluna kaydet (isim ve yol harfiyen böyle — kod `Resources.Load<GameObject>("Voice/RadioHUD")` ile arıyor).
6. Play'e bas, PTT ile test et (2 instance ile mute'u da dene — bir satıra tıkla).

### 5.2 HUD ikonları / SFX (henüz istek yok)

Şu an HUD SADECE metin + doluluk çubuğu kullanıyor (bilerek — emoji/ikon glyph'i TMP fallback fontunda kutu gösterme riski taşıyor, bkz. RadioSpeakerRow sınıf yorumu). İkon eklemek istenirse (örn. mikrofon/mute simgesi) `RadioSpeakerRow`'a `Image` tabanlı bir ikon child'ı eklenip kod küçük bir değişiklikle buna bağlanabilir — şu an kapsam dışı, istek gelirse buraya yeni bir alt bölüm eklenecek.

### 5.3 Dalga 4: Ayarlar UI wiring (kod tarafı bitti — aşağıdaki authoring kaldı)

Kod tarafı (`Assets/MENUUI/UnifiedSettingsManager.cs`) hazır: yeni 4 serialize alan (`voiceVolumeSlider`, `voiceVolumeText`, `voiceEnabledToggle`, `voiceSelfMonitorToggle`) ve `keyBindingRows` dizisine eklenecek bir Bas-Konuş satırı **null-guard'lı** — hiçbiri Inspector'da atanmadan da oyun hatasız çalışır (slider/toggle'lar sadece görünmez, telsiz kendi varsayılan değerleriyle `RadioVoicePrefs`'ten çalışmaya devam eder). Aşağıdakiler tamamen **kozmetik/UX** authoring — kod değişikliği gerektirmiyor.

**Nerede:** Ayarlar panelinin **Audio (Ses) sekmesi** — Master/Music/SFX slider'larının bulunduğu AYNI sekme (plan: "Mevcut Audio sekmesi 3 slider ve `SaveAudioSettings()` desenine UY"). Bas-Konuş rebinding satırı ise **Controls (Kontroller) sekmesinde**, diğer tuş atama satırlarının yanına.

**A) Audio sekmesi — 1 slider + 2 toggle**

`UnifiedSettingsManager` component'inin Inspector'ında **"VOICE (TELSİZ) SETTINGS"** header'ı altında 4 alan var:

1. `voiceVolumeSlider` → Audio sekmesinde, SFX slider'ının altına/yanına **Master/Music/SFX ile aynı görünümde** bir `Slider` (Min=0, Max=1 — kod `SetupVolumeSlider` içinde zaten zorluyor, Inspector'da 0-1 dışı bir değer verilse de ezilir) ekle, bu alana sürükle.
2. `voiceVolumeText` → yanına yüzde göstergesi için bir `TextMeshProUGUI` (diğer 3'ünün text'leriyle aynı stil — `"%"` işareti kod tarafında eklenmiyor, sadece sayı; mevcut Master/Music/SFX text'leri de aynı şekilde çıplak sayı basıyor, tutarlılık için aynı yolu izle).
3. `voiceEnabledToggle` → "Telsiz Aç/Kapat" etiketli bir `Toggle`.
4. `voiceSelfMonitorToggle` → "Kendini Dinle" etiketli bir `Toggle`.

Etiket metinleri (görünür Text/TMP child'lar üzerinde) için `LocalizeStringEvent` komponenti ekleyip `String Reference`'ı sırasıyla `SettingsVoiceVolume` / `SettingsVoiceEnabled` / `SettingsVoiceSelfMonitor` anahtarına bağla (bkz. bu dosyanın 1. bölümü — anahtarlar zaten StringTable'a eklendi). Mevcut Vsync/Music/Effect etiketlerinin NASIL bağlandığına bak (aynı prefab içinde bir `LocalizeStringEvent` örneği bulup onu kopyala) — kod tarafı bu etiketlere HİÇ dokunmuyor, `RefreshAllLocalizedUI()` (`UnifiedSettingsManager.cs`) sahnedeki tüm `LocalizeStringEvent`'leri otomatik tarayıp yeniliyor.

**Not — anında etkili + master ile çarpılmama:** Bu 3 kontrol diğer 3 audio slider'ından FARKLI bir kalıcılık deseni kullanıyor (bilerek — kod tarafındaki `HandleVoiceVolumeChanged`/`HandleVoiceEnabledChanged`/`HandleVoiceSelfMonitorChanged` yorumlarına bak): değiştirildiği anda `RadioVoicePrefs` üzerinden PlayerPrefs'e hemen yazılıyor VE aktif konuşan slot'lara anında uygulanıyor ("Kaydet" tuşuna basmayı beklemiyor), Geri/İptal ise bu anlık yazımı son kaydedilen değere geri sarıyor. Authoring tarafında ekstra bir şey yapmana gerek yok, sadece bu davranışın BİLİNÇLİ olduğunu bil — "neden Master/Music/SFX gibi davranmıyor" diye şüphelenme.

**B) Controls sekmesi — Bas-Konuş rebinding satırı**

`keyBindingRows` (`UnifiedSettingsManager.KeyBindingRow[]`) dizisi mevcut satırların (WASD, E, Shift, vb.) OTOMATİK YANINA yeni bir `GameAction.PushToTalk` girişi EKLEMEZ — enum'a yeni değer gelmesi diziyi büyütmüyor, elle authoring gerekiyor (plan: "yeni `GameAction` otomatik GÖRÜNMEZ"):

1. Controls sekmesindeki mevcut tuş atama satırlarından birini (örn. `E` satırı) **kopyala/çoğalt**, "Bas-Konuş" / "Push to Talk" için yeni bir satır oluştur. Etiket metnine `LocalizeStringEvent` ile `ControlPushToTalk` anahtarını bağla (StringTable'da zaten var — bölüm 1).
2. `UnifiedSettingsManager` Inspector'ında `keyBindingRows` dizisinin boyutunu 1 artır, yeni elemanda:
   - `action` = **`PushToTalk`** (dropdown'da `InputBindingManager.GameAction` enum'ından seçilir — Adım 1'de eklendi, listede görünür).
   - `button` = yeni satırdaki tıklanabilir `Button` (rebinding yakalamasını başlatan).
   - `keyText` = yeni satırdaki tuş adını gösteren `TextMeshProUGUI` (örn. "V").
3. Ekstra kod GEREKMİYOR: `RefreshAllKeyBindingTexts()` ve `StartRebinding()`/`HandleKeyRebinding()` zaten `keyBindingRows`'u generic foreach ile geziyor, `action` alanına göre `InputBindingManager` API'lerini çağırıyor — yeni satır otomatik çalışır.
4. Doğrulama: Play'e bas, Controls sekmesinde "Bas-Konuş" satırına tıkla, `V`'den başka bir tuşa bas, satırın yeni tuşu gösterdiğini ve `Escape`'in rebinding'i iptal ettiğini doğrula; ayrıca menü açıkken PTT'nin YANLIŞLIKLA tetiklenmediğini (bkz. plan §UI ve ayarlar "bastırma" notu — Adım 1 kapsamı, burada sadece gözle teyit).

---

## 6. Dalga 4 — Adım 9: Ağ Simülatörü + İstatistik Overlay (kod hazır, kullanım kılavuzu)

Kod: `Assets/NewCss/Voice/DevTools/RadioVoiceDevTools.cs` (tamamı `#if UNITY_EDITOR`). Aynı dosyada üç BAĞIMSIZ araç var, üçü de varsayılan KAPALI:

| Menü | Ne yapar |
|---|---|
| `Tools ▸ Cargor ▸ Voice ▸ Kayıt-Oynatma Aracı` | Bölüm 1 (mevcut, değişmedi) |
| `Tools ▸ Cargor ▸ Voice ▸ Ağ Simülatörü (Alım - Gecikme-Jitter-Kayıp)` | **YENİ** — bu bölüm |
| `Tools ▸ Cargor ▸ Voice ▸ İstatistik Overlay` | **YENİ** — bu bölüm |

⚠️ **Toggle'ları Play'e BASMADAN ÖNCE aç.** Dev araç GameObject'i sadece sahne yüklenirken bir kez kurulur (`[RuntimeInitializeOnLoadMethod]`) — Play ortasında menüyü ilk kez açmak geriye dönük GameObject oluşturmaz (kayıt/oynatma aracıyla aynı sınırlama).

### 6.1 Ağ simülatörü nasıl çalışır

`RadioVoicePlayback.HandleIncomingPacket` — hangi kaynaktan gelirse gelsin (kendi mikrofonun/Kendini Dinle loopback'i, disk-replay, gerçek ağ) — TEK giriş noktası. Simülatör bu noktaya **ALIM tarafında** girer: paketi gerçekten işlemeden önce yakalar, gecikme/jitter/kayıp/dup/sıra-bozma kararını verir, sonra (varsa gecikmeyle) gerçek işleme fonksiyonuna besler. Simülatör KAPALIYKEN bu kanca `null`'dır ve player build'de kod tamamen derleme dışıdır — üretim davranışı hiç değişmez.

**Bunun sonucu**: simülatörü test etmek için 2. bir makineye/Steam hesabına gerek YOK. Tek başına:
1. `Tools ▸ Cargor ▸ Voice ▸ Ağ Simülatörü...` aç.
2. Ayarlar panelinden (veya `RadioVoicePrefs`) **Kendini Dinle**'yi aç.
3. Play'e bas, PTT'ye bas-konuş.
4. Ekranda beliren "AĞ SİMÜLATÖRÜ — ALIM YOLU" panelinden Gecikme/Jitter/Kayıp/Duplike/Sıra-bozma slider'larını ayarla — kendi sesini artık simüle edilmiş kötü ağ üzerinden dinliyorsun.

### 6.2 Kabul kriteri testi: %10 kayıp + 80 ms jitter

Plan riski/kabul kriteri: **"%10 kayıp + 80 ms jitter altında ses ANLAŞILIR kalmalı."** Panelde bunun için hazır bir buton var: **"Kabul Testi Ön Ayarı (%10 kayıp + 80ms jitter)"** — basınca Gecikme=0, Jitter=80ms, Kayıp=%10, Duplike=%0, Sıra bozma=%0 ayarlanır (planın metnindeki değerlerin harfiyen aynısı, fazlası eklenmedi).

Doğrulama: butona bas → konuş → kulakla dinle (anlaşılır mı?) + aynı anda İstatistik Overlay'de:
- İlgili slotun ring buffer doluluğunun 120ms hedefinin etrafında salınıp salınmadığına,
- `under=`/`over=` sayaçlarının konuşma boyunca makul kalıp kalmadığına (sürekli artan underrun = jitter buffer yetersiz, eşiklerin/`VoiceBufferPolicy` sabitlerinin gözden geçirilmesi gerekir — bu, Core'da bir değişiklik ve yeni EditMode testi ister, bu dalganın kapsamı dışında, bulgu olarak not düşülür).

Sürüm/parametre notu: **Duplike** ve **Sıra bozma** planın kabul kriterinde YOK — panelde ayrı slider olarak var, ama bu spesifik testte %0'da bırakılıyor (buton bunu böyle ayarlıyor). İstenirse elle açılabilir, ek bir stres testi olarak.

### 6.3 Kayıt/Oynatma + Ağ Simülatörü birlikte (2-instance testi için)

`RadioVoicePlayback.HandleIncomingPacket` tek giriş noktası olduğu için disk-replay akışı da simülatörden geçer: Bölüm 1'deki "▶ Oynatmayı Başlat" ile beslenen kayıtlı ses akışı, Ağ Simülatörü AÇIKSA aynı gecikme/jitter/kayıp'tan geçer. Bu, **aynı canned ses + kontrollü/tekrarlanabilir ağ bozulması** anlamına gelir — iki instance testinde (plan Katman C) "konuşan taraf" replay kullanıyor; o replay akışının üstüne simülatörü de açarsan tek makinede gerçek 2-makine senaryosuna (Katman D) en yakın simülasyonu (replay + jitter) elde edersin. Adımlar:
1. Instance A: Kayıt aracını aç, PTT ile birkaç saniye konuş, kaydet.
2. Kaydı Instance B'ye kopyala (`Application.persistentDataPath` altındaki yol ekranda yazılı).
3. Instance B: hem Kayıt-Oynatma hem Ağ Simülatörü toggle'larını aç, Play'e bas, "▶ Oynatmayı Başlat"a bas.
4. Instance A: normal ağ üzerinden dinler — B'nin gönderdiği replay akışı B'nin simülatöründen geçmiş olarak A'ya ulaşır (simülatör B'nin ALIM yolunda değil, B'nin kendi Playback'ine replay beslerken devreye giriyor — yani bu senaryoda simülatör B'nin KENDİ loopback'ini bozuyor, A'nın aldığı ağ paketini DEĞİL; gerçek ağ üzerinden A'nın alım yolunu bozmak istiyorsan simülatörü A'da aç).

### 6.4 İstatistik overlay ne gösteriyor

Ekranın sağında (`Rect(650,10,380,460)`) beliren panel, ~6 Hz'de bir güncellenir (her frame DEĞİL — GC çöpü yapmasın diye, bkz. kod yorumu):
- **Gönderilen/Alınan KB/s** + bu pencerede ortalama paket boyutu + **oturum boyunca gözlenen tepe paket boyutu**. Tepe 700B'yi geçerse ekranda uyarı çıkar — plan riski #1'in ("gerçek Steam bitrate'i ölçülmedi, 800B kapağı buna bağlı") somut ölçüm noktası burası.
- Her aktif slot için **ring buffer doluluğu (ms) / 120ms hedef** + underrun/overrun/dropped-örnek sayaçları (mevcut `VoiceRingBuffer` public sayaçlarından okunuyor, yeni API eklenmedi).
- Aktif konuşmacı sayısı, yakalama durumu (Idle/Transmitting/Tail/Degraded).
- Ağ simülatörü açıksa: kuyruk derinliği + toplam kayıp/dup/reorder sayaçları.

Overlay SADECE İstatistik Overlay toggle'ı açıkken hook'larını takar (Capture.PacketProduced + Playback.DevPacketReceived) — kapalıyken hiçbir ek abonelik/ölçüm maliyeti yoktur.
