# 🎙️ Telsiz — Dissonance vs. Kendi Sistemimiz (KARAR DOSYASI)

> Tarih: 2026-08-08 · Dal: `feature/voice-chat` (15 commit, merge/push yok)
> Kullanıcı isteği: "karar için daha fazla bilgi ver". Bu dosya karar vermek için; uygulama komutu ayrı.
> **Bir önceki oturumda söylediğim iki şey DÜZELTİLDİ** — aşağıda ⚠️ ile işaretli.

---

## 0. Tek paragraf özet

Stack'imiz (NGO + FacepunchTransport + Facepunch.Steamworks) üstünde Dissonance'ın çalıştığı
**kanıtlandı** — Lethal Company birebir bu üçlüyü shipliyor. Buna karşılık kendi sistemimizde
**gerçek PLC teknik olarak YAZILAMAZ**: Steam'in `DecompressVoice`'ı Opus decoder'ını bize
açmıyor, yani kayıp paketi decoder'a bildirme yolu yok. Kendi yolumuzda PLC ancak PCM taklidi
(son frame tekrar + fade) olabilir. Bu, iki seçenek arasındaki **kalite tavanı farkı**dır ve
ayar turlarıyla kapanmaz.

---

## 1. Fiyat — ⚠️ DÜZELTME

| Kalem | Gerçek |
|---|---|
| Dissonance Voice Chat (çekirdek) | **$120** (per seat), v9.0.9, son güncelleme 2026-04-27 |
| Dissonance For Netcode For GameObjects | **ÜCRETSİZ** — ama çekirdeği zorunlu bağımlılık olarak listeliyor |

⚠️ Önceki oturumda **"$100"** demiştim → **$120**. NGO entegrasyonunun ücretsiz olduğu bilgisi doğruydu.
Tek geliştirici için tek koltuk yeterli (Standard Unity Asset Store EULA).

---

## 2. Stack uyumu — ⚠️ artık ÇIKARIM değil, KANITLI

Önceki oturumda "Dissonance NGO'nun mesajlaşmasını kullandığı için `FacepunchTransport` ile
şeffaf çalışır" dedim. ⚠️ **O bir çıkarımdı, doğrulanmamıştı.** Dissonance dokümanları
transport-agnostikliği açıkça yazmıyor ve NFGO entegrasyonunun kaynağı kapalı
(GitHub reposu yalnızca doküman + issue tracker, kod yok).

**Doğrulama başka yerden geldi — Lethal Company'nin modding template'i, gereken plugin listesi:**

```
DissonanceVoip.dll
Facepunch Transport for Netcode for GameObjects.dll
Facepunch.Steamworks.Win64.dll
ClientNetworkTransform.dll · Newtonsoft.Json.dll · ...
```

Yani **Dissonance + NGO + FacepunchTransport + Facepunch.Steamworks** aynı build'de,
satılan bir oyunda birlikte çalışıyor. Bizim stack'imizle birebir aynı. Bu risk kapandı.

(LC Unity 2022.3.9f1; biz 6000.5.6f1 — Dissonance'ın asset store sayfası "Original Unity
version 6000.0.23" diyor, yani Unity 6 hattı destekli.)

---

## 3. Karar verici teknik gerçek: PLC'yi kendi yolumuzda YAZAMIYORUZ

Vendor'lanmış API yüzeyimizin tamamı
(`Assets/SteamWorks/Runtime/Facepunch/Facepunch.Steamworks.Win64.xml:2446-2467`):

- `ReadVoiceData(Stream)` → sıkıştırılmış veri
- `DecompressVoice(Stream compressed, int size, Stream output)` → 16-bit PCM

İki sonuç:

1. **Kayıp paketi decoder'a bildiremiyoruz.** Opus PLC, decoder'ı "veri yok" ile çağırınca
   devreye girer ve interpolasyon frame'i üretir. `DecompressVoice` yalnızca elimizdeki
   sıkıştırılmış baytları alıyor — kayıp frame'i haber verecek parametre YOK.
2. **Kendi Opus decoder'ımızı da takamıyoruz.** Steam'in kendi dokümantasyonu sıkıştırılmış
   formatı *"arbitrary format and is not meant to be played directly"* diye tanımlıyor —
   Opus olduğu garanti bile değil, sürüm sürüm değişebilir.

→ Kendi yolumuzda PLC = **PCM-domain taklidi**: son frame'i tekrarla + fade-out, ya da sessizlik.
Tek paket kaybında Opus PLC'nin "neredeyse duyulmaz" sonucuna karşı bu duyulur bir artefakt.

**Üçüncü bir yol var ve dürüstçe söylemek gerek:** gerçek PLC istiyorsak Steam Voice'tan
tamamen çıkıp Unity `Microphone` + kendi Opus'umuza (ör. Concentus) geçmemiz gerekir —
bu tam olarak Dissonance'ın yaptığı iştir, sadece elle. Kapsam olarak şu ana kadar
yazdığımız her şeyden büyük.

---

## 4. Dissonance ne veriyor (doğrulanmış)

| Yetenek | Durum |
|---|---|
| **Adaptif jitter buffer** | Var — 100 ms muhafazakâr başlıyor, ölçülen jitter'a göre *küçülüyor* (playback'i bir tık hızlandırarak). Bizim 260 ms **sabit** hedefimizin yerine geçer. |
| **PLC** | Var — Opus PLC'yi doğrudan çağırıyor (yazarın kendi pipeline yazısı) |
| **Opus, 20 ms frame** | Var — PLC'nin iyi çalıştığı en küçük birim |
| **VAD** | Var |
| **Oda / kanal sistemi** | Var ve **runtime string tabanlı**: `comms.RoomChannels.Open(roomId, positional, priority)`. Dinleme tarafı `Voice Receipt Trigger`. |
| **Positional (proximity) ses** | Var (`Voice Proximity Broadcast Trigger`) — bizim kapsamda değil ama bedava geliyor |
| **Kurulum** | `DissonanceSetup` prefab'ı sahneye at → `DissonanceComms` + `NfgoCommsNetwork`. "Scripting gerekmiyor." |

**2. aşama (oda-bazlı ses/görüş) için önemli:** oda adı runtime string olduğu için
"oda sayısı sabit değil, yeni haritalarda değişir" gereksinimimiz doğrudan karşılanıyor.
Kendi sistemimizde bu, sıfırdan yazılacak bir katman.

**Bir sadeleşme daha:** Dissonance sahne objesi olarak durduğu için mikrofonun yalnızca oyun
haritalarında açık olması *kendiliğinden* oluyor — bizim `ad3fa49`'daki sahne kapısı
(`GameStateManager` var mı işaretçisi) gereksizleşir. `SteamUser.VoiceRecord`'ın
süreç-global olması ve "ana menüde mikrofon açık kalması" riski de tamamen ortadan kalkar
(planın "en tehlikeli noktası" olarak işaretlediğim şey buydu).

**Steam bağımlılığı kalkar:** Dissonance kendi mikrofon yakalamasını yapıyor → Tutorial
sahnesinde de çalışır, Steam çalışmadan test edilebilir.

---

## 5. Kod envanteri — geçilirse ne atılır, ne taşınır

Ölçülen gerçek satır sayıları (`Assets/NewCss/Voice` + `Assets/Tests/EditMode`):

### ATILIR (~3 700 satır kod + test)
| Dosya | Satır |
|---|---|
| `Core/VoiceRingBuffer.cs` | 350 |
| `Core/PooledByteStream.cs` | 157 |
| `Core/VoiceSequenceTracker.cs` | 128 |
| `Core/VoiceBufferPolicy.cs` | 125 |
| `Core/VoicePacket.cs` | 110 |
| `Core/VoiceDriftTracker.cs` | 109 |
| `RadioVoiceTransport.cs` | 456 |
| `RadioVoiceSpeakerSlot.cs` | 390 |
| `RadioVoiceCapture.cs` | 282 |
| `RadioVoicePlayback.cs` | 264 |
| 5 test dosyası (ring/drift/sequence/policy/packet) | 548 |
| `DevTools/RadioVoiceDevTools.cs` — ağ simülatörü + istatistik overlay kısmı | 809'un çoğu |

Bu satırların **tamamı** son 3 turda uğraştığımız problem alanı (jitter/drift/sequence/buffer).
Dissonance'a geçmek, bu problemi bize satın almak demek.

### TAŞINIR (~870 satır)
| Dosya | Satır | Not |
|---|---|---|
| `UI/RadioHudController.cs` | 399 | konuşan listesi + mute; Dissonance'ın `VoicePlayerState` API'sine bağlanır |
| `UI/RadioSpeakerRow.cs` | 158 | değişmez |
| `RadioVoicePrefs.cs` | 138 | ses/enabled/self-monitor tercihleri |
| `Core/VoiceHudRowTimer.cs` + testi | 67 + 107 | saf mantık, değişmez |
| `RadioVoiceRuntime.cs`'in PTT kısmı | 414'ün bir dilimi | `V` binding + `GameAction.PushToTalk` korunur |

Ayrıca **korunan editör işleri**: 10 lokalizasyon anahtarı, ayarlar paneli 4 kontrol,
PTT rebinding satırı — `plans/telsiz-editor-isleri.md` aynen geçerli kalır.

---

## 6. İki yolun dürüst maliyeti

### A) Kendi sistemimizle devam
- **Para:** 0
- **İş:** adaptif buffer (ölçüme göre hedef boyutu ayarlama + resample/hızlandırma) +
  PLC *taklidi*. Sonra **hâlâ ölçülmemiş olan** gerçek ağ koşullarında ayar turları.
- **Bilinen tavan:** gerçek PLC yok (§3). Steam'in ses-aktivitesi tespitinden gelen
  burst yapısı bizim kontrolümüzde değil (asimetrik kapı turu bunun yüzünden çıktı).
- **Bugüne kadarki 3 tur ölçülen varyansın TAMAMI YEREL.** Steam relay jitter'ı ve
  4 oyuncu eşzamanlı konuşma henüz hiç görülmedi → belirsizlik büyük.
- **Doğrulama darboğazı devam eder:** 2 makine + 2 Steam hesabı zorunlu.

### B) Dissonance
- **Para:** $120 (tek seferlik)
- **İş:** prefab kurulumu + HUD/prefs/PTT wiring (~870 satır taşıma) + ~3 700 satır silme.
  Sahne kapısı ve mikrofon-teardown riski ortadan kalkar.
- **Kazanç:** adaptif buffer + gerçek PLC + oda sistemi (2. aşama) + Steam'siz test edilebilirlik.
- **Riskler:** üçüncü parti bağımlılığı (kaynak kapalı, bug'da satıcıya bağımlıyız) ·
  27 MB asset (runtime ayak izi daha küçük) · Dissonance'ın kendi mikrofon/VAD davranışını
  öğrenmek · mevcut 15 commit'in yarısından çoğu çöpe gider.
- **Riskleri azaltan:** LC gibi büyük bir oyunun aynı stack'te shiplemesi (§2), aktif bakım
  (2026-04 güncellemesi).

---

## 7. Müdür önerisi

**Dissonance (B).** Gerekçe sırasıyla:

1. §3 bir *tavan*, bütçe sorunu değil: kendi yolumuzda gerçek PLC'ye ulaşmanın yolu
   Steam Voice'u terk edip Dissonance'ın işini elle yapmaktan geçiyor.
2. Son 3 tur, atılacak listedeki dosyalarda geçti ve belirti hâlâ kapanmadı. Bu, "birkaç tur
   daha" tahmininin güvenilir olmadığının kanıtı — hem de **henüz yalnızca yerel** koşullarda.
3. 2. aşama (oda-bazlı ses) zaten planda var; Dissonance onu bedava veriyor, kendi
   sistemimizde ayrı bir katman demek.
4. $120, bu üç turun sürdüğü zamanın yanında küçük.

**Ama önce şunu yap (ücretsiz ve 15 dakika):** son turu (`248a93e` drift + `63bf30b` buffer)
Unity'de test et. Sebebi para değil bilgi: eğer ses **şu an kabul edilebilir** hâle geldiyse,
telsizi olduğu gibi bırakıp FAZ 1/2/3'e dönmek ve Dissonance kararını 2. aşamaya (oda sistemi
gerçekten gerekince) ertelemek en ucuz yol olur. Hâlâ bozuksa karar zaten netleşir.

---

## Kaynaklar
- https://assetstore.unity.com/packages/tools/audio/dissonance-voice-chat-70078 ($120, v9.0.9)
- https://assetstore.unity.com/packages/tools/integration/dissonance-for-netcode-for-gameobjects-206514 (FREE, çekirdek bağımlı)
- https://github.com/EvaisaDev/LethalCompanyUnityTemplate (LC plugin listesi = stack kanıtı)
- https://martindevans.me/voip/2017/02/19/Dissonance-Voip-Pipeline/ (adaptif buffer + Opus PLC, yazarın kendisi)
- https://placeholder-software.co.uk/dissonance/docs/Reference/Other/RoomChannel.html (oda API)
- https://placeholder-software.co.uk/dissonance/docs/Basics/Quick-Start-Unity-NFGO.html (NGO kurulumu)
- https://github.com/Placeholder-Software/Dissonance (kaynak YOK, yalnız doküman/issue)
