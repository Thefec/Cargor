# 🔊 SES TASARIMI — Cargor

> **Durum (2026-09-14):** tasarım yönü ONAYLANDI, SFX seçimi kısmen bitti, müzik üretimi kullanıcıda.
> Kod henüz yazılmadı. İlgili GDD bölümü: **§27 Ses Tasarımı**.

---

## 1. Onaylanan yön: PlateUp modeli

Kullanıcı kararı (2026-09-14): **"atmosfer sesi olmayacak, PlateUp'taki gibi sadece
etkileşimler ve müzik."**

Bu kararla **iptal edilenler** (ilk tasarımda vardı, artık YOK):
- Oda bazlı ambiyans katmanı (5 oda için yatak sesi + serpiştirme)
- Mixer reverb snapshot'ları (odaya göre yankı)
- Diegetic radyo (ofisteki radyodan çalan, duvar arkasından boğuklaşan müzik)

**Kalan yapı:** sürekli arka plan müziği + etkileşim/olay sesleri. Bu sadeleşme planı
ciddi ölçüde küçülttü.

Müzik karakteri: **sıcak, midtempo caz, sürdinli trompet ağırlıklı.** Haritanın pastel
low-poly karikatür temasına ve mevcut `Assets/Music/` caz setine uyumlu.

---

## 2. Tespit edilen eksikler (kanıtlı)

| # | Eksik | Kanıt |
|---|---|---|
| 1 | **AudioMixer hiç yok** | Projede tek `.mixer` dosyası yok. `UnifiedSettingsManager.cs:1163` sadece `AudioListener.volume = master` yapıyor; Müzik/SFX slider'ları yalnızca inspector'a elle bağlı 2 AudioSource'u etkiliyor → **oyun içi SFX slider'ı çalışmıyor. Bu bir BUG.** |
| 2 | **Oyun içinde müzik çalmıyor** | `Assets/Music/MusicPlayer.cs` → `allowedScenes = { "MainMenu" }`. 8 caz parçası duruyor, kimse çalmıyor. |
| 3 | **Olay seslerinin hiçbiri yok** | Para, sayaç, gün sonu, kira, prestij, upgrade, quest, iflas, yanlış eşya — hepsi sessiz. |
| 4 | **Müzik süresi çok kısa** | 8 parça × **tam 30 sn** = 4 dakika. Oyun süresi (GDD §3): gün 1-3 = 200sn, sonra +10sn/gün → 16 gün ≈ **69 dakika**. 4 dk müzik ≈ **17 tekrar**. Atmosfer sesi olmadığı için tüm ses dünyasını müzik taşıyacak → yetersiz. |

Mevcut ve ÇALIŞAN sesler (dokunma): adım sesleri, alma/bırakma/rafa koyma, kutu düşme,
garaj kapısı, tır motoru (geliş/bekleme/çıkış), telefon, müşteri sesleri, menü butonları.

---

## 3. SFX seçimi — Kenney CC0

Paketler CC0 (`License.txt`: *"You may use these assets in personal and commercial
projects"*), **atıf gerekmiyor, Steam'e uygun.** Toplam 423 dosya.

İndirme adresleri (scratchpad geçiciydi; gerekirse yeniden indir):

```
https://kenney.nl/media/pages/assets/interface-sounds/fa43c1dd4d-1677589452/kenney_interface-sounds.zip
https://kenney.nl/media/pages/assets/impact-sounds/87b4ddecda-1677589768/kenney_impact-sounds.zip
https://kenney.nl/media/pages/assets/casino-audio/2472606a04-1721639069/kenney_casino-audio.zip
https://kenney.nl/media/pages/assets/music-jingles/f37e530b9e-1677590399/kenney_music-jingles.zip
https://kenney.nl/media/pages/assets/rpg-audio/8e99002d76-1677590336/kenney_rpg-audio.zip
```

### 3.1 ONAYLANAN seçimler → `Assets/Audio/SFX/` altına kopyalandı

| Olay | Dosya | Kenney kaynağı |
|------|-------|----------------|
| Sayaç tıkırtısı (NumberRollDisplay) | `sfx_counter_tick.ogg` | interface `click_001` |
| Gün sonu | `sfx_day_end.ogg` | music-jingles `Steel/STEEL05` |
| Quest tamamlandı | `sfx_quest_complete.ogg` | music-jingles `Pizzicato/PIZZI08` |
| Yanlış eşya | `sfx_wrong_item.ogg` | interface `error_004` |
| Kira günü uyarısı | `sfx_rent_warning.ogg` | interface `bong_001` |
| Upgrade satın alma | `sfx_upgrade_bought.ogg` | interface `switch_001` |

### 3.2 AÇIK — kullanıcı henüz seçmedi

- **Para kazanma** — adaylar: rpg `handleCoins`, `handleCoins2`, casino `chips-stack-1`
- **Doğru eşya / müşteri memnun** — adaylar: interface `confirmation_001` / `_002` / `_004`
- **İflas** — ilk 3 aday BEĞENİLMEDİ ("bir tık daha ağır olabilirdi"). Yeni adaylar
  hazırlandı (SAX07, SAX03, STEEL02, STEEL07, HIT15, HIT03) ama dinlenmedi.
  **Muhtemelen gereksiz:** aşağıdaki müzik prompt'u **F** iflas cue'sunu zaten üretecek,
  Kenney cingılından daha iyi oturur. Önce onu bekle.

### 3.3 İPTAL — kota sesi

GDD §7 + kod doğrulandı: eski kutu kotası (`QuotaManager`, GAME OVER koşulu) commit
`0c026ef` ile **tamamen silinmiş**. Yeni müşteri kotası sadece o günün müşteri sayısını
belirliyor, kaybetme koşulu değil (−0.2 prestij cezası). **Kutlanacak bir "kota doldu"
anı yok** → ses gereksiz.

---

## 4. Müzik üretimi — flowmusic.app

Araç: <https://www.flowmusic.app/> — enstrümantal seçeneği var, royalty-free ticari lisans.

**Kullanım kuralları:**
- **Aile başına bir session** (A, B, C, D, E, F, G = 7 session). Aynı ailede tutarlılık
  istiyoruz, aileler arası bulaşma istemiyoruz.
- A1 ve A2'yi aynı session'da test et — A2, A1'in kopyası gibi çıkıyorsa her parça için
  yeni session aç. Prompt'lar kendi kendine yeter (BPM + akort içlerinde yazılı).
- **Varyasyonlar ÜST ÜSTE EKLENMEZ, DEĞİŞTİRİLİR.** Aşağıdaki her prompt tam hâlindedir.
- Hepsi F majör (gerilim ve iflas F minör — kasıtlı), uyumlu tempolarda → çapraz geçişte
  akort çakışması olmaz.
- **Kusursuz loop gerekmiyor** — müzik sistemi 2-3 sn çapraz geçişli yazılacak.

### İLERLEME

- [x] **A ailesi (6 parça)** — 2026-09-14, projede
- [x] **B ailesi (3 parça)** — 2026-09-14, projede
- [x] **C ailesi — 1 parça, KAPANDI.** Plan 2 parça öngörüyordu; kullanıcı 2026-09-14'te
      "C ailesi tek kalsın, 2.'ye gerek yok" dedi. Gün sonu müziği her gün aynı çalacak —
      bilinçli kabul edilen bir tekrar, eksik değil.
- [x] D ailesi (2 parça) — projede
- [x] E — ana menü teması (1) — projede
- [x] F — iflas cue (1) — projede
- [x] G — kazanma finali (1) — projede

**MÜZİK ÜRETİMİ TAMAMLANDI: 15 parça, 47.5 dakika.** 69 dakikalık oyunda ~1.5 tekrar —
tekrar sorunu çözüldü (eski durum: 4 dakika / 17 tekrar). Yeni parça gerekmiyor.

F ailesi geldiği için **`06_iflas` SFX seçimi artık gereksiz** (§3.2'deki açık madde kapandı);
`Stinger/Bankrupt ... F.ogg` kullanılacak.

### 4.1 A — Ana döngü (günün %70'i), 100 BPM F majör

**A1**

```
Warm, cozy midtempo jazz for a lighthearted cartoon warehouse management game. 100 BPM, key of F major. Brushed drums, walking upright bass, soft Rhodes electric piano comping, and a gentle muted trumpet playing a simple relaxed melody with plenty of space between phrases. Friendly and unhurried — background music that never demands attention. Instrumental, no vocals.
```

**A2**

```
Warm, cozy midtempo jazz for a lighthearted cartoon warehouse management game. 100 BPM, key of F major. Light bossa nova groove with soft rim-click drums, walking upright bass, warm vibraphone comping, and a gentle muted trumpet playing a simple relaxed melody with plenty of space between phrases. Cool and floating, unhurried — background music that never demands attention. Instrumental, no vocals.
```

**A3**

```
Warm, cozy midtempo jazz for a lighthearted cartoon warehouse management game. 100 BPM, key of F major. Gentle New Orleans swing with brushed drums, walking upright bass, soft piano comping, and a clarinet playing a simple cheerful melody with plenty of space between phrases. Old-fashioned corner-shop charm, friendly and unhurried — background music that never demands attention. Instrumental, no vocals.
```

**A4**

```
Warm, cozy midtempo cool jazz for a lighthearted cartoon warehouse management game. 100 BPM, key of F major. Soft brushed drums, walking upright bass, mellow acoustic guitar comping, and a breathy flute playing a simple relaxed melody with plenty of space between phrases. The calmest and softest of the set — background music that never demands attention. Instrumental, no vocals.
```

**A5**

```
Warm, cozy midtempo jazz for a lighthearted cartoon warehouse management game. 100 BPM, key of F major. Hammond organ trio feel — rounded Hammond organ carrying both the comping and a simple relaxed melody, brushed drums and walking upright bass underneath. Full and warm, a 1960s lounge feel, unhurried — background music that never demands attention. Instrumental, no vocals.
```

**A6**

```
Warm, cozy midtempo jazz for a lighthearted cartoon warehouse management game. 100 BPM, key of F major. Piano trio only — acoustic piano, walking upright bass and brushed drums, with no horns or wind instruments at all. Sparse and airy, lots of space between phrases, unhurried — background music that never demands attention. Instrumental, no vocals.
```

### 4.2 B — Yoğun saat (müşteri kalabalığı), 126 BPM F majör

**B1**

```
Upbeat hard bop jazz for the busy rush hour of a cooperative cargo game. 126 BPM, key of F major. Driving ride cymbal, energetic walking bass, punchy piano stabs, and trumpet and saxophone trading short lively riffs. Bustling and fun, never stressful or chaotic. Instrumental, no vocals.
```

**B2**

```
Upbeat shuffle jazz for the busy rush hour of a cooperative cargo game. 126 BPM, key of F major. Swinging shuffle groove, driving ride cymbal, energetic walking bass, punchy piano stabs, and a honking baritone saxophone carrying short lively riffs. Bustling and fun, never stressful or chaotic. Instrumental, no vocals.
```

**B3**

```
Upbeat latin jazz mambo for the busy rush hour of a cooperative cargo game. 126 BPM, key of F major. Congas and bongos with a driving montuno piano figure, energetic bass line, and bright trumpets playing short lively riffs. Bustling and fun, never stressful or chaotic. Instrumental, no vocals.
```

### 4.3 C — Kapanış / gün sonu, 76 BPM F majör → 2 parça (aynı prompt iki kez)

```
Slow, warm closing-time jazz. 76 BPM, key of F major. Solo muted trumpet over soft brushed drums, upright bass and sparse piano chords. The nostalgic feeling of an honest day's work finished. Calm and satisfied. Instrumental, no vocals.
```

### 4.4 D — Son 30 saniye gerilimi, 132 BPM F minör → 2 parça (aynı prompt iki kez)

```
Tense jazz countdown loop for the final seconds of a work shift. 132 BPM, key of F minor. Fast walking upright bass, insistent ride cymbal and rim shots, staccato piano stabs, no melody line. Building pressure and urgency while staying jazzy and playful — never horror or dread. Instrumental, no vocals.
```

### 4.5 E — Ana menü teması (oyunun kimliği), 104 BPM F majör → 1 parça

```
Signature main theme for a cozy cooperative cargo and warehouse management game. Midtempo swing jazz, 104 BPM, key of F major. A confident, memorable, hummable muted trumpet melody, brushed drums, walking bass, warm piano. Inviting and optimistic with a slightly retro 1960s office feel. Instrumental, no vocals.
```

### 4.6 F — İflas cue, 60 BPM F minör, ~10 sn → 1 parça

```
Short somber jazz cue for going bankrupt. 60 BPM, key of F minor. A lone muted trumpet playing a slow descending phrase, soft piano, brushed drums fading out. Melancholy and defeated but dignified, with a touch of dark humour. Instrumental, no vocals, with a clear ending.
```

### 4.7 G — Kazanma finali, 120 BPM F majör, ~15 sn → 1 parça

```
Triumphant big band jazz finale celebrating a successful sixteen day run. 120 BPM, key of F major. Full horn section with trumpets and saxophones, swinging drums, walking bass. Joyful, warm and generous. Instrumental, no vocals, with a clear ending.
```

### 4.8 Dosyalar nerede (2026-09-14 itibarıyla YERLEŞTİ)

Müzik sistemi bu klasör yapısını **doğrudan okuyacak** — yeni parça eklemek için klasöre
atmak yeterli olacak, kod değişmeyecek. Dosya adı serbest; harf soneki (A1, B2, C...)
kullanıcının etiketi, kod buna bakmıyor, **klasör belirleyici**.

```
Assets/Music/Gameplay/Main/      <- A1..A6   (6 dosya)
Assets/Music/Gameplay/Busy/      <- B1..B3   (3)
Assets/Music/Gameplay/Closing/   <- C        (1 — nihai, kullanıcı kararı)
Assets/Music/Gameplay/Tension/   <- D1, D2   (2)
Assets/Music/Menu/               <- E        (1)
Assets/Music/Stinger/            <- F, G     (2)
```

> **MainMenu.unity'deki 5 eksik GameObject ses işiyle ilgisiz — KAPANDI.** QA bu turda
> fark etti (333→328, 3 `Rectangle` + `FigmaImage`'ler). Kullanıcı 2026-09-14'te
> doğruladı: tutorial'a giriş ile geri/devam butonlarının arayüzünü kendisi değiştirmiş.
> Kasıtlı, geri alınmayacak. (Kullanıcı "büyük ihtimal" dedi — ileride ana menüde eksik
> bir görsel fark edilirse bakılacak ilk yer burasıdır.)

Mevcut 8 caz parçası (`Assets/Music/Music_fx_*.wav`, kök klasörde) atılmadı; tempoları
tutarsa A ailesine katılabilir. **Hâlâ WAV (47 MB)** — istenirse aynı yöntemle OGG'ye
çevrilebilir, ama zaten git geçmişinde olduğu için kazanç sınırlı.

#### ⚠️ Format: WAV değil OGG — neden ve nasıl

Kaynak dosyalar 48 kHz 16-bit stereo WAV olarak geldi, **toplam 431 MB**. Repo `.git`
zaten 495 MB ve projede **git LFS yok** → olduğu gibi commit'lenirse repo ~1 GB'a çıkardı
ve WAV'lar git geçmişinde kalıcı olurdu.

**Yapılan:** hepsi OGG Vorbis `-q:a 6`'ya çevrildi → **431 MB → 48 MB (8.9× küçülme).**
15/15 dosyada süre birebir korundu (0.00 sn sapma), örnekleme hızı ve kanal sayısı aynı.
Proje içindeki WAV'lar silindi; **orijinaller `C:\Users\cicek\Downloads` içinde duruyor.**

**Denenip başarısız olan yol:** `soundfile` (libsndfile 1.2.2) paketinin Vorbis encoder'ı
bu kurulumda **süreci hard-crash ettiriyor** (exit 127; float/int16/mono/stereo, tüm
varyantlar). Aynı paketin MP3 encoder'ı sorunsuz çalışıyor — sorun yalnızca Vorbis'te.
Çözüm: taşınabilir **ffmpeg** indirildi (kurulum yok, PATH'e dokunulmadı):
`C:\Users\cicek\AppData\Local\Temp\ffmpeg-tmp\ffmpeg-9.0.1-essentials_build\bin\ffmpeg.exe`
Geçici klasörde olduğu için **silinebilir**; tekrar gerekirse
<https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip> adresinden inip açılır.

**Zip denendi mi?** Evet, ölçüldü: WAV'ı zip'lemek yalnızca **%6.7** kazandırıyor
(33 MB → 30 MB). Müzik PCM'i bayt seviyesinde rastgeleye yakın, DEFLATE sıkıştıracak
tekrar bulamıyor. Ses için doğru sıkıştırıcı OGG/Vorbis'tir. Unity `.ogg`'yi `.wav` ile
birebir aynı şekilde import eder, iş akışı değişmez.

> Not: Unity build sırasında bu OGG'leri kendi Vorbis'ine yeniden kodlar (çift kayıplı
> kodlama). Arka plan cazında duyulması beklenmez; kritik bulunursa kaynak WAV'lar
> Downloads'ta duruyor, LFS'e geçilerek geri dönülebilir.

#### Import ayarları — DÜZELTİLDİ (2026-09-14)

Unity dosyaları varsayılan **Decompress On Load** ile import etmişti. Bu modda klip
yüklenirken tamamı RAM'e açılır — 3 dakikalık stereo parça ≈ **32 MB**; 15 parçayla
yüzlerce MB. Uzun müzikte doğru ayar **Streaming**'dir.

Araç: **`Assets/Editor/AudioImportSettingsSetup.cs`**
Menü: `Tools ▸ Cargor ▸ Audio ▸ Ses Import Ayarlarini Duzelt` (+ `(kuru gosterim)` varyantı,
hiçbir şey yazmadan ne değişeceğini raporlar).

Süreye göre kural:

| Süre | loadType | Neden |
|------|----------|-------|
| ≥ 30 sn | `Streaming` | Müzik parçaları — RAM'de yer kaplamaz |
| 5–30 sn | `CompressedInMemory` | Cingıllar — anında başlamalı, RAM maliyeti düşük |
| < 5 sn | `DecompressOnLoad` | Kısa SFX — en düşük gecikme |

Hepsinde `compressionFormat = Vorbis`, `quality = 0.7`, `preloadAudioData = false`;
Streaming olanlarda ayrıca `loadInBackground = true`.

**Sonuç:** 21 dosya (15 müzik + 6 SFX) düzeltildi, 0 hata. 14 parça Streaming, F cingılı
(10 sn) CompressedInMemory, 6 SFX DecompressOnLoad. Tekrar çalıştırıldığında
"Değişen: 0 · Zaten doğru: 21" veriyor — **idempotent**, yeni parça ekleyince tekrar
çalıştırılabilir.

> `Assets/Music` **kökündeki 8 eski caz WAV'ı bilinçli olarak taranmıyor** (araçtaki
> `TaranacakKlasorler` listesi). Gereksiz diff üretmesin diye; A ailesine katılmalarına
> karar verilirse klasöre taşınıp araç tekrar çalıştırılır.

---

## 5. Uygulama planı (KOD HENÜZ YAZILMADI)

| Faz | İş | Departman | Ses bağımlılığı |
|-----|----|-----------|-----------------|
| **A** | `CargorMixer.mixer` (Master ▸ Music / SFX) + tüm AudioSource'ların gruba yönlendirilmesi + `UnifiedSettingsManager.ApplyAudioSettings()` bağlantısı | gameplay | **YOK — hemen başlanabilir** |
| **B** | `SfxBus` (static, MonoBehaviour değil) + `SfxLibrary` ScriptableObject + havuzlanmış AudioSource + olay bağlantıları | gameplay | Seçilen 6 ses hazır |
| **C** | Müzik sistemi: klasör tabanlı playlist, çapraz geçiş, gün fazına göre aile seçimi (Main/Busy/Closing/Tension) | graphics-ui | Müzik üretimi bitmeli |

### Mimari kararlar

- `AudioListener.volume = master` **KALIR**; mixer grupları yalnızca kategori seviyesi
  taşır → çifte ölçekleme tuzağına düşülmez (aynı tuzak ekonomi dengelemesinde yaşandı).
- **Telsize DOKUNULMAZ.** `RadioVoicePrefs` / `RadioVoiceSpeakerSlot` kasten listener
  zincirinin dışında; mixer'a sokmak regresyon olur.
- `AudioSource.PlayClipAtPoint` **KULLANILMAZ** — mixer'a uğramıyor, havuz kullan.
- Test: `SfxLibrary` çözümlemesi + çapraz geçiş matematiği EditMode testi (MonoBehaviour
  olmayan çekirdek + kendi asmdef'i; `NumberRoller` emsali).

### ⚠️ Sessiz ölüm bekçisi (pazarlık konusu değil)

GDD §27 bu projede sesin **iki kez sessizce öldüğünü** kaydediyor (2026-08-13 bağlanmamış
AudioSource; 2026-08-31 Unity yeniden-serileştirmesi `successCallSound`'u sildi).
Sahne/prefab'ları tarayıp bizim bileşenlerdeki **boş clip slotlarını hata olarak basan**
bir editör doğrulayıcı yazılacak.

---

## 6. Denenmiş ve ELENMİŞ yollar (tekrar önerme)

| Yol | Neden elendi |
|-----|--------------|
| Unity MCP `generate_audio` (fal.ai) | fal `configured: false`, ön ödemeli. Kullanıcı: "yüklenecek para yok". Fiyat: stable-audio $0.20/üretim, cassette SFX $0.01/üretim. |
| **Prosedürel sentez (Python ile WAV üretimi)** | Denendi, 5 demo üretildi. Kullanıcı dinledi: **"hiç uygun değiller çok kötüler".** Ajan sesi duyamadığı için kör iterasyon anlamsız → yol kapandı. |
| ElevenLabs ücretsiz plan | Ücretsiz plan **kişisel kullanım**, atıf zorunlu. Ticari lisans ücretli planda (~$5/ay). Steam çıkışı için uygun değil. |
| Mixkit atmosfer sesleri | 9 dosya indirildi ama lisans metni JS modal içinde, **doğrulanamadı**. PlateUp kararıyla atmosfer zaten iptal → konu kapandı. |
