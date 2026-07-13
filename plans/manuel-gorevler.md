# 🧪 Cargor — Tam Manuel Test Listesi

> Bu dosya = **benim (müdür/Claude) headless yapamadığım, sadece senin gerçek Unity'de yapabileceğin** testler.
> Batchmode = derleme + saf-mantık; **görsel UI, Play-mode davranışı, multiplayer senkron** senin gözünle.
> Bir maddeyi bitirince `[ ]` → `[x]` yap ve sonucu bana söyle. Terslik varsa Console çıktısını yapıştır.
> Öncelik: **A** (bu oturumun fix'leri) → **B** (merge blocker) → sonrası.
> İlgili: [../PLAN.md](../PLAN.md) · [release-push.md](release-push.md) · [devam.md](devam.md)

---

## 🔴 A. BU OTURUMUN GÜVENLİK FIX'LERİ — regresyon teyidi
> 3 exploit kapatıldı (G2, G1-b, G6/G7). Hepsi **canlı gameplay akışına** dokundu. Amaç: **meşru akış hâlâ çalışıyor mu** (fix bir şeyi kırmadı mı). Tek editör host çoğu için yeter; kimlik-testleri 2 client ister.

### A1. Masa & Raf etkileşimi — G6/G7 kimlik-doğrulama (EN KRİTİK)
> Risk: `Table.ValidateInteractionRequest`'e eklenen `owner==sender` çapraz-kontrolü meşru oyuncuyu YANLIŞLIKLA engelliyor olabilir.

**Tek oyuncu (host):**
- [ ] Elde kutuyla masaya git, etkileş → kutu elden düşer, masada belirir.
- [ ] Boş elle dolu masaya git → üründ/kutu ele gelir.
- [ ] Masada anında kutulama (ürün + doğru kutu tipi) → çalışıyor mu, yanlış tipte ceza/temizlik doğru mu.
- [ ] Raftan item al → ele gelir, raf slotu boşalır.
- [ ] Rafa item koy → rafta belirir (Box kategorisi kısıtı hâlâ geçerli mi).

**2 oyuncu (kimlik-check yanlış-pozitif avı — bu fix'in asıl riski):**
- [ ] Her iki oyuncu da KENDİ envanteriyle masayla etkileşebiliyor mu (client de, host da). Console'da `Kimlik uyusmazligi` uyarısı ÇIKMAMALI.
- [ ] Client oyuncu raftan al / rafa koy yapabiliyor mu.
- [ ] Host (clientId=0) kendi masasıyla etkileşebiliyor mu (kenar durum — owner==0 kontrolü).
- [ ] ❗ Beklenen: hiçbir meşru etkileşim reddedilmemeli. Reddedilirse → `Kimlik uyusmazligi` log satırını bana yapıştır.

### A2. Tır teslimi — G2 (server-only ProcessDelivery)
- [ ] Doğru renk + dolu kutuyu tıra teslim et → **+ödül (50 TL) + prestij geliyor mu**.
- [ ] Yanlış renk kutu → ceza/red yolu doğru çalışıyor mu.
- [ ] `requiredCargo` sayısı kadar teslim → tır tamamlanıp çıkıyor mu.
- [ ] 2 oyuncu: ikisi de teslim edebiliyor mu; ödül **tek** sayılıyor mu (çift-say / hayalet kutu yok).

### A3. Para akışı — G1-b (ModifyMoney server-only)
> Risk: relay kaldırıldı; meşru bir para değişimi client'ta sessizce kaybolmuş olabilir.
- [ ] Teslim ödülü, satın alma (upgrade), kira ödeme, telefon ödülü → **hepsi parayı doğru değiştiriyor mu**.
- [ ] Client (non-host) tarafında para UI'ı host ile **senkron** mu.
- [ ] Console'da `ModifyMoney client'tan cagrildi — yok sayildi` uyarısı normal oyunda ÇIKMAMALI (çıkıyorsa bir çağrı yanlış context'te).

---

## 🟠 B. FAZ 0 — MERGE BLOCKER (combined build, 2+ gerçek Steam client)
> Roguelite dalının main'e merge'i buna bağlı. Combined build = late-join fix + `[NETDBG]` dahil. Steam'e yükle ([[cargor-steam-deploy]]), sonra:

### B1. Lobi (regresyon — EN KRİTİK ilk kontrol)
- [ ] Host lobi kurar, 2. oyuncu **oyun başlamadan** girer → **normal girebilmeli**. Giremezse `ConnectionApproval` config-hash sorunu geri gelmiş demektir (build paritesi bozuk).

### B2. Late-join reddi
- [ ] Host oyunu başlatır (The Main Office). 3. oyuncu Steam davet/kod ile girmeye çalışır → **reddedilmeli + net mesaj** ("Oyun zaten başladı").
- [ ] ❗ Normal çıkışta (oyundan düzgün ayrılan oyuncu) **yanlış red mesajı ÇIKMAMALI** (1. tur regresyonu buydu).
- [ ] Whitelist-dışı biri (oyun başındaki lobide olmayan) reconnect denesin → **reddedilmeli**.

### B3. Reconnect (whitelist içi)
- [ ] Oyun içinde 2. oyuncunun bağlantısını kopar (Alt+F4 / ağ kes) → **aynı** oyuncu tekrar girebilmeli.
- [ ] Reconnect sonrası: satın alınmış perkler / envanter / gün durumu **client-local geri geliyor mu** (roguelite reconnect replay hedefi).

### B4. Oturum-sonu reset
- [ ] Oyun bitince ana menüye dön, **aynı uygulama oturumunda** yeni lobi kur → yeni oyuncular normal girebilmeli; para/gün doğru resetleniyor mu (ResetGuard + ResetGameStartedFlag).

### B5. FAZ 0 kapanış (testler ✅ olunca — ben yaparım)
- [ ] SteamManager+LateJoinGuard'ın **sadece late-join fix hunk'larını** seçici commit (`[NETDBG]` hariç).
- [ ] `[NETDBG]` enstrümantasyonunu kaldır.
- [ ] Font/ProjectSettings artefaktlarını revert.
- [ ] Roguelite dalını main'e merge.

---

## 🟡 C. ROGUELITE — biriken Play-teyidi
### C1. Draft & reroll senkronu (2 client)
- [ ] Panel gün sonu masada açılınca **3 kart** gösteriyor mu (tüm liste değil), doğru upgrade/fiyat.
- [ ] Reroll → yeni 3 kart + fiyat artıyor mu (50→90→160→290→525), günlük sıfırlanıyor mu.
- [ ] Host + client **aynı 3 kartı** görüyor mu; satın alma server-authoritative mi (client'ta da düşüyor mu).

### C2. Perk etkileri & dışlama
- [ ] Her perk satın alınınca gerçekten iddia ettiği etkiyi yapıyor mu.
- [ ] Dışlama (gambler_case ↔ all_in): birini al → diğeri sonraki tekliflerde VE reroll'da çıkmamalı; ikisi aynı teklifte çıkmamalı; biri alındıysa diğerinin satın alınması reddedilmeli.

### C3. Late-join relic re-apply (2+ client, ÖNEMLİ)
- [ ] Host client-lokal etkili relic alsın (`agile_crew`/`energetic_crew` → hız/stamina). SONRA 2. oyuncu katılsın → etki yeni client'ta uygulanıyor mu, yoksa sadece host'ta mı. Bozuksa: `OnNetworkSpawn` catch-up döngüsü gerekebilir (çift-uygulama riskiyle).

### C4. Tier kilidi (veri girildiyse)
- [ ] gün<5 sadece T1, gün≥5 T2, gün≥9 T3 çıkıyor mu; fiyatlar v3.2 raporuyla eşleşiyor mu.

---

## 🟢 D. EKONOMİ / DENGE ÖLÇÜMÜ
### D1. Kutu/dakika verim ölçümü — C1 kota kalibrasyonu
> economist'in kota-ölümü kararı buna bağlı: gerçek oyuncu "kutu/dakika" verimi kodda yok, ölçülmeli.
- [ ] Normal oynarken bir gün boyunca kaç kutu tamamlıyorsun (1P). İdeal: 2 client'la da ölç. Bana kabaca "gün başına ~X kutu / oyuncu" ver → economist `_difficultyRatio`'yu kalibre etsin.

### D2. 16-gün tam döngü (denge doğrulaması)
- [ ] 1P ve 2P ile 16 günü bitir: iflas oluyor mu, kira günleri (4/8/12/16) sorunsuz mu, gün 16 kazanma ekranı geliyor mu. (economist simülasyonu sağlıklı dedi; gerçek oynanışla doğrula.)

---

## 🔵 E. GÖRSEL / INIT
### E1. Shelf/Table görsel init
- [ ] Raf/masa görselleri sahnede doğru başlıyor mu (Yol B ölü-kod silindi; kod-yolu temiz, gözle teyit). Not: raf/masa **yükseltince 3D model değişimi** olmayacak — o sistem bilinçli PARK'ta (bkz devam.md), bu bir bug değil.

---

## ⚫ F. TAM MULTIPLAYER (branch kontrol ONAY sonrası, en son)
- [ ] **1 / 2 / 4 kişi** gerçek co-op oturumu: draft senkronu (herkes aynı teklif, satın alma server-auth, reroll senkron) + 16 günlük tam döngü + A/B/C bölümlerindeki her şeyin bir arada tutarlılığı.

---

## ⚙️ AÇIK KARAR — ProjectSettings SENTIS define
- [ ] Her batchmode çalıştırmada `ProjectSettings.asset`'ten `SENTIS_ANALYTICS_ENABLED` define'ı siliniyor. **Sen karar ver:** Sentis analytics kullanıyorsan kalmalı (neden silindiğine bakarız), kullanmıyorsan gitmeli. (Roadmap artifact'te "Kararın gerekli" listesinde.)

---

## 📌 Not — benim yapabildiklerim (senden istemem gerekmez)
Derleme kontrolü, EditMode/saf-mantık testleri, kod yazımı, git commit, plan/doküman güncelleme → headless hallediyorum.
