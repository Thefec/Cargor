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

## 5. Sonraki dalgalarda buraya eklenecek bölümler (henüz boş)

- **Dalga 3 sonrası:** `RadioHUD.prefab`, `RadioSpeakerRow.prefab` authoring adımları (Canvas ayarları, ekran-uzayı liste yerleşimi).
- **Dalga 4:** Ayarlar prefabına `keyBindingRows`'a Bas-Konuş satırı ekleme + slider/toggle wiring (`UnifiedSettingsManager.cs:559-580`).
