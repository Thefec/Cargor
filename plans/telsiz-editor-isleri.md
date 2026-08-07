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

## 2. Sonraki dalgalarda buraya eklenecek bölümler (henüz boş)

- **Dalga 2/3 sonrası:** `RadioSpeakerSlot.prefab`, `RadioHUD.prefab`, `RadioSpeakerRow.prefab` authoring adımları (filtre komponent değerleri, Canvas ayarları).
- **Dalga 4:** Ayarlar prefabına `keyBindingRows`'a Bas-Konuş satırı ekleme + slider/toggle wiring (`UnifiedSettingsManager.cs:559-580`).
