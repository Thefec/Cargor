# 🎮 PLAYTEST CHECKLIST — biriken tüm açık gözlem maddeleri

> **Neden bu dosya var:** birçok iş kolu "kod bitti, kontrol ONAY, tek kalan Unity playtest"
> durumunda kapandı ama playtest maddeleri 5 ayrı yere dağıldı (ekonomi planı Adım 5, oda
> görünürlüğü S7, kira-sonrası özellikler, telefon collider'ı, telsiz). Burada tek oturumda
> koşulabilecek hâle getirildi.
> **Derlendi:** 2026-08-31, `main` (37 commit push'suz).
> **Kullanım:** her maddenin başına ✅/❌/⏭️ koy, ❌ olanları not düş, sonra oturumda bana ver.

---

## 0. Başlamadan (2 dk)

- [ ] Unity Console'da **Clear on Play KAPALI**, **Collapse KAPALI**, Error+Warning+Log açık.
- [ ] Bir CS hatası var mı — Console kırmızı temiz mi?
- [ ] Log dosyası yeri (build'de test edilecekse):
      `%USERPROFILE%\AppData\LocalLow\Eclion Software\Cargor\Player.log`

---

## A. TEK OYUNCU TURU (host, 1 kişi) — ekonomi + gün döngüsü + telefon

En az **gün 13'e kadar** oynanmalı (gün 5 / 9 / 13 üç ayrı özellik açıyor).

### A1 — Ekonomi hissi (ekonomi planı Adım 5)
- [ ] **Normal/strict bant fazla kolaylaştı mı?** Yeni kira `{290,650,1140,1630}` P1'de final kasayı
      +%115 şişiriyor (ölçüldü). Oyun "para sıkıntısı hiç yaşamadım"a dönüyorsa not düş —
      ikinci tur kira ayarı gerekir.
- [ ] **Telefon spam'i baskın mı?** Sim, yavaş oyunda optimal stratejinin %100 telefon çevirmek
      olduğunu söylüyor. Sen de kendini "sürekli telefona koşarken" buluyor musun? Öyleyse
      yapısal düzeltme hazır bekliyor (çağrı parası yalnız müşteri SERVİS edilirse verilsin).
- [ ] **`patient_customers` perki** alındığında işe yarıyor mu, yoksa fark edilmiyor mu?
- [ ] Kira günü (4/8/12/16) geldiğinde takvimde **"Kira Günü"** etiketi görünüyor mu?
- [ ] Event sıklığı (16 günde ort. 6 event) fazla/az geliyor mu?

### A2 — Gün döngüsü (PlateUp modeli)
- [ ] Günlük müşteri kotası bitince gün **erken bitiyor** mu? Öncesinde ~30 sn kapanış payı
      (son tırı yükleme fırsatı) veriliyor mu?
- [ ] Kapanış payı sırasında **başka bir panel açıp kapatınca** özellik ölüyor mu?
      (Bu daha önce coroutine yüzünden sessizce ölmüştü, `Update`+sayaca çevrildi — regresyon testi.)
- [ ] Servis edilmeden çıkan müşteri prestij cezası (-0.4) HUD'da hissediliyor mu?

### A3 — Telefon V4 (dışarı arama)
- [ ] `E`'ye **basılı tutunca** bar doluyor ve müşteri geliyor mu (tek basış değil)?
- [ ] **🔴 Telefonun yanına gidince Console'da `[PhoneCall] Player entered phone area` düşüyor mu?**
      DÜŞMÜYORSA: telefonun BoxCollider'ı çok küçük (`&424799585`, local size ~0.0106),
      Editor'de elle büyütülmesi gerekiyor. Bu bilinen tek açık madde.
- [ ] **Başarılı arama sesi duyuluyor mu?** (2026-08-31'de geri bağlandı — `e669e33` yanlışlıkla
      silmişti, aylardır sessizdi.) ⚠️ Bağlı klip `male_hmhmhm_grunting_#2` — **yer tutucu gibi
      duruyor**, kastedilen ses bu mu, kulağınla doğrula. Değilse klip değiştirilmeli.
- [ ] Bekleme barı çağrı yokken **gizleniyor** mu (sürekli açık kalmıyor mu)?
- [ ] Gün sonuna yakın (17:30 civarı) telefon çevirmeyi **reddediyor** mu? (Az önce çağırdığı
      müşteriyi cezalandırma bug'ının guard'ı.)

### A4 — Quest / görev kartları
- [ ] Her gün **3 görev** görünüyor mu (tier yükselince de 3 kalmalı — 5'e çıkmamalı)?
- [ ] "Görev Kademesi" upgrade'ini aldıktan sonra görevler **gözle görülür şekilde daha iyi**
      geliyor mu? (Round 11'in K-aday çekilişi bunu sağlamalı; hiç fark yoksa not düş.)
- [ ] Ödül gün sonunda mı veriliyor?
- [ ] Console'da `Quota calc: ... eventMult=` logunu ara — event günlerinde çarpan **1'den
      farklı** mı? Hep 1 ise abone sırası bug'ı var demektir.

### A5 — Kira sonrası 3 özellik
- [ ] **Gün 5 — İade:** müşterinin üstünde renk gösteren küre görünüyor mu, ne istediği anlaşılıyor mu?
- [ ] **Gün 9 — 2 item:** BoxRequest-dual iki ayrı E etkileşimi (~23 sn toplam) — sıkıcı/uzun geliyor mu?
- [ ] **Gün 13 — Karışık tır:** iki renkli talep tırın üstünde/garaj kapısında doğru yazıyor mu?
      Karışım rengi (örn. kırmızı+sarı → turuncu) **ACES tonemapping altında** doğru görünüyor mu?
      (Geçmişte mavi→mor kayması emsali var.)
- [ ] Gün 5 ve 9'da aynı anda hem yeni perk tier'ı hem yeni müşteri mekaniği açılıyor —
      üst üste binme bunaltıcı mı?

### A6 — Garaj kapısı UI (Editor wiring bekliyordu)
- [ ] İstenen kutu sayısı **garaj kapılarının üstünde** yazıyor mu (tırın üstünde değil)?
- [ ] Her hangarda doğru kapıya yazıyor mu (indeks karışması yok mu)?
- [ ] Tır çıkınca kapı metni **temizleniyor** mu?
- [ ] Eski tır-üstü TMP objesi hâlâ görünüyor mu (artık hiçbir script yazmıyor, elle kapatılmalı)?

### A7 — Not defteri + lokalizasyon
- [ ] Not defteri 14 sayfa açılıyor mu, uzun paragraflar TMP kutusuna **sığıyor** mu?
- [ ] Dil değiştir (TR→EN→TR): tuş atama ekranındaki "Sol Tık" vb. **anında** güncelleniyor mu?
- [ ] Not defteri açıkken hareket kilitleniyor, kapanınca **geri açılıyor** mu?

---

## B. İKİ İSTEMCİ TURU (host + 1 client) — netcode, oda görünürlüğü, telsiz

> Bu turun **en kritik maddesi B1**. Diğerlerini atlasan bile onu koş.

### B1 — 🔴 CLIENT'TA WASD ÖLMESİ (aylardır açık, kök neden doğrulanmadı)

> **Neden bugüne kadar veri çıkmadı:** teşhis kodu `#if UNITY_EDITOR` altındaydı — ikinci
> makinede standalone client koşuyorsan log **hiç derlenmiyordu**. 2026-08-31'de guard
> `#if UNITY_EDITOR || DEVELOPMENT_BUILD` yapıldı ve `LockMovement`'a **çağıran yığın izi**
> eklendi. Artık build alırken **Development Build kutusunu işaretle**, yoksa yine veri yok.

- [ ] Client'ı Editor'de VEYA **Development Build** ile çalıştır (ikisinden biri şart).
- [ ] Hareket ölene kadar normal oyna. Öldüğü ANI not et: hangi panel açıktı, telsiz/telefon/
      müşteri etkileşimi var mıydı, gün sonu muydu?
- [ ] Öldüğü andaki **3-4 `[TESHIS]` satırını** kopyala + varsa **`LockMovement(True) cagiran:`**
      yığın izini kopyala. Bu ikincisi kimin kilitlediğini doğrudan söyler.

**Karar tablosu — bug anındaki `[TESHIS]` satırına bak:**

| Gözlem | Sonuç |
|---|---|
| Hiç `[TESHIS]` satırı yok | `Update` hiç koşmuyor: obje deaktif / component disabled / `isLocalPlayer=False`. Netcode spawn yoluna bakılır. |
| `locked=True` + `interactionsLocked=True` | Müşteri etkileşimi kilidi (`CustomerAI` unlock RPC'si düşmüş) |
| `locked=True` + ekranda açık panel VAR | Panel `_isAnimating` takılması (kapat butonu iş görmez) |
| `locked=True` + panel YOK + gün sonu civarı | **En olası:** break-room kilidi. Kilidi ClientRpc koyuyor, açması ise yalnız panelin kapanma kenarında — panel o peer'de açılmadıysa kilit sonsuza kadar kalır. |
| `locked=False`, `rawWASD=FalseFalseFalseFalse` tuş basılıyken | Tuş ataması / pencere odağı (`focus=False`) |
| `locked=False`, girdi var, `speed≈0` | Hız çarpanı üst üste binmiş (event/perk/buff) |
| `locked=False`, girdi ve hız normal, `delta1s≈0` | Fiziksel sıkışma / `CharacterController`+`Rigidbody` çakışması |
| `timeScale=0` | ESC menüsü ağ üzerinden herkesi donduruyor (ayrı bug, aşağı bak) |

- [ ] **En olası hipotezi doğrudan tetikle:** günü bitir → herkes break room'a → Next Day paneli
      açılsın → **client kendi panelini kapatsın** → sonra **herhangi bir oyuncu break room
      trigger'ından çıkıp tekrar girsin**. Hipotez doğruysa client tam burada kilitlenir
      (`locked=True`, ekranda panel yok) ve bir daha açılmaz.
- [ ] Hareket ölünce: fare bakışı da ölüyor mu, yoksa sadece WASD mi?
- [ ] ESC menüsü açıp kapatınca **geri geliyor** mu?

### B1b — Analizin yan bulguları (ayrı bug'lar, doğrulanacak)
- [ ] **Bir oyuncunun ESC'si herkesi donduruyor mu?** `EscapeMenuManager` menüyü ClientRpc ile
      tüm peer'lere açıp `Time.timeScale = 0` yapıyor. Co-op'ta olmaması gereken davranış.
- [ ] Not defteri tetik alanına girince panel açılıyor mu? (`UITriggerZone` `"Player"` tag'i
      arıyor, oyuncu prefab'ının tag'i `"Character"` — hiç tetiklenmiyor olabilir.)

### B2 — Telsiz (voice chat)
- [ ] Host→client ses **kesik kesik** mi geliyor?
- [ ] Push-to-talk ikonu: basılıyken renkli, bırakınca gri oluyor mu? (Build'de de test et —
      shader stripping yüzünden daha önce hep renkli takılı kalmıştı.)

### B3 — Oda görünürlüğü / karartma (S7, hiç 2-istemci test edilmedi)
- [ ] Odalar arası geçişte **çapraz fade** pürüzsüz mü, pop-in var mı?
- [ ] Diğer oyuncu başka odadayken **gizleniyor**, aynı odaya girince **görünüyor** mu?
- [ ] **X** basılıyken eşyalar görünüyor mu ("stok kontrolü")?
- [ ] Tır avlusu / dış mekan / yeşillik **kendi renginde** kalıyor mu (kararmıyor mu)?
- [ ] Sarı kutunun y-tavanı (çoğu 11) üstündeki çatı/duvar aydınlık kalıyor mu — rahatsız edici mi?
- [ ] **Frame Debugger**: SRP Batcher bozulmuş mu? (Shader dokunuşunun tek gerçek riski;
      yalnız gerçek Unity'de ölçülebilir.)
- [ ] URP/Lit materyalli 5 obje parlak leke bırakıyor mu?

### B4 — Netcode genel
- [ ] **Late join**: client oyun ortasında katılınca gün/para/prestij/quest doğru mu?
- [ ] Client'ta müşteri iade modu (gün 5+) doğru görünüyor mu? (NGO'nun "ilk NetworkVariable
      senkronu `OnNetworkSpawn`'dan önce" tuzağı burada düzeltilmişti — regresyon testi.)
- [ ] Client telefonu kullanabiliyor mu, ödül host'ta doğru işleniyor mu?

---

## C. Konsol log avı (oyun sonunda tek seferde)

Console'u filtrele, şunları ara:

| Ara | Anlamı |
|---|---|
| `[TESHIS]` | B1 (WASD bug'ı) için tek kanıt kaynağı — **mutlaka kopyala** |
| `[PhoneCall] Player entered phone area` | A3, düşmüyorsa collider küçük |
| `Quota calc:` … `eventMult=` | A4, event günlerinde 1'den farklı olmalı |
| `NullReference` | her biri bir bug |
| `Shader error` / pembe materyal | B3 |
| `Missing` / `not assigned` | bağlanmamış SerializeField (bu projede tekrar eden tuzak) |

---

## T. TUTORIAL TURU (host, 1 kişi) — yeniden yazım sonrası (`feature/tutorial-rewrite`)

> Ön koşul: Adım 6 (kullanıcı Editor işleri, `plans/tutorial-rewrite.md`) tamamlanmış olmalı —
> adım listesi Inspector'da dolu, highlight/trigger referansları bağlı.

- [ ] T1 — Sahne açılışında Console'da Missing Script / broken reference YOK mu? (guid taşıması doğrulaması)
- [ ] T2 — Adım 1 (hareket) mesajı doğru dilde (TR/EN) görünüyor mu?
- [ ] T3 — Kutu alma adımı NewPickup v2 ile uyumlu tetikleniyor mu (PickupItem koşulu doğru objeyi tanıyor mu)?
- [ ] T4 — Rafa koy/raftan al: NetworkedShelf.BoxType karşılaştırması hatalı renk kabul/reddi yapıyor mu?
- [ ] T5 — Masaya koy/masadan al akışı donmadan ilerliyor mu?
- [ ] T6 — Tıra teslim: DeliverToTruck sayaç doğru artıyor mu, BoxInfo.BoxType ile karışıklık var mı?
- [ ] T7 — Garaj kapısı adım geçişlerine göre doğru açılıp kapanıyor mu (TutorialDoor/GarageDoorController)?
- [ ] T8 — Tutorial biter bitmez Menu.cs akışına (MapSelection) sorunsuz dönüyor mu? Bittikten sonra
      SPACE'e (skip) basınca ya da dil değiştirince tamamlanma mesajı/akışı tekrar tetikleniyor mu?
      (Bu turda `_currentStep=null` fix'i ile kapatıldı — regresyon testi.)
- [ ] T9 — 🔴 Bilinen açık WASD bug'ı (bkz B1) tutorial'ı da etkiliyor mu? Development Build ile test et,
      [TESHIS] logu kopyala. **Bu bug'ı çözmeye çalışma, yalnız gözlemle** — bu işin kapsamı dışı.

---

## D. Bulguları bana nasıl ver

Her ❌ için tek satır yeter: **ne yaptın → ne bekledin → ne oldu**, varsa log satırı.
Ekran görüntüsü gerekmiyor (görsel maddeler hariç: A5 karışık tır rengi, B3 karartma).

**Bu checklist bitince kapanacak işler:** 11-round ekonomi dengeleme · PlateUp gün döngüsü ·
oda görünürlüğü S7 · kira-sonrası 3 özellik · telefon trigger collider'ı.
