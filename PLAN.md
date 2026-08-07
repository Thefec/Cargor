# 📋 CARGOR — Canlı Plan (Dashboard)

> **Sahibi**: Müdür (planlama + delegasyon). Bu dosya = hızlı okunan gösterge paneli.
> Her oturum başında **[plans/devam.md](plans/devam.md)**'i oku; detay gerektikçe buraya ve `plans/` altına in.
> Bir iş bitince buradan çıkar, `plans/archive/` altına taşı.

---

## 🎯 Şu an aktif iş

**YOK — kod yazılmıyor.** İki iş kuyrukta: **play-test** (kullanıcıda) ve **telsiz** (plan onaylı, uygulama komutu bekliyor).

> 🎙️ **TELSİZ (oyun içi bas-konuş sesli iletişim) — PLAN ONAYLI, UYGULAMA BEKLİYOR.**
> Tam plan: **[plans/telsiz-voice-chat.md](plans/telsiz-voice-chat.md)** (10 adımlık commit sırası, doğrulama katmanları, riskler).
> Kullanıcı kararı: *"detaylıca planlama yapalım, uygulama komutunu ben vereceğim."* **Komut gelmeden kod yazma.**
> Ardından gelecek 2. aşama: **oda-bazlı görünürlük** (oda sayısı SABİT DEĞİL, haritaya göre değişecek → veri-güdümlü olmalı). Telsiz tasarımı bunu engellemiyor; gerekçe planın "Sonraki iş" bölümünde.

`main` = `6e945bb`, **origin/main'in 1 önünde (PUSH YOK)**. Ekonomi denge turu 2026-08-06'da merge edildi (`238fd92`, `--no-ff`, 21 commit) — sorun çıkarsa tek noktadan revert edilebilir.

- ✅ **Roguelite draft + RELEASE PUSH FAZ 0/1** — arşiv: [plans/roguelite-draft.md](plans/roguelite-draft.md), [plans/release-push.md](plans/release-push.md)
- ✅ **Ekonomi sıfırdan yeniden hesaplandı + uygulandı** — 4 faz analiz ([plans/economy-rebuild-2026-07-30*.md](plans/economy-rebuild-2026-07-30-faz4-final.md)), §D#1–#8 tamamı kodda; kalite kapısı 3 turda ONAY
- ✅ **Quest sistemi canlı** — 30 asset, tier ödül tablosu, kart UI, gün-16 settlement, raf exploit'i kapalı
- ✅ **Unity 6000.5.6f1 geçişi**
- ✅ **Doğrulama**: 0 CS hatası · EditMode 9/9 · 30 asset + tüm P dizileri Unity'ye okutularak teyitli

### ⏭️ Sıradaki adım

0. 📏 **Ölçüm protokolü hazır: [plans/playtest-olcum-protokolu.md](plans/playtest-olcum-protokolu.md)** — oynamadan önce bunu aç. Tek zorunlu çıktı **kutu/dakika/oyuncu**. Oturum öncesi VE sonrası `Cargor / Ekonomi Değerlerini Doğrula` çalıştır (perk asset bozulmasını yakalar).
1. 🔴 **PLAY-TEST — tek gerçek kapı.** Bu turda kira, prestij, upgrade fiyatları, quest ödülleri, event çarpanları ve telefon ekonomisi değişti; **hiçbiri oyun içinde çalışırken görülmedi.** Makine doğrulaması "derleniyor ve sayılar doğru yerde" der, "oyun iyi hissettiriyor" demez. Checklist tabanı: [plans/playtest-2026-07-19.md](plans/playtest-2026-07-19.md) (bayat, ekonomi kısmı yeniden yazılmalı).
   **Ölçülecekler duyarlılık sırasına göre:** `kutu/dk/oyuncu` (1.2→2.0 ile 1P kümülatifi %117 değişiyor) · masa meşgul süresi S · `agile_crew`'in üretime yansıması · telefon yanıtlamanın oyuncu-saniyesi maliyeti. Bir oyun günü yalnız 200–330 gerçek saniye → **mutlak TL değil oranlarla konuş.**
2. 🙋 **Kullanıcıda bekleyen 3 iş:** (a) 2. servis masasının mesh/collider yerleşimi — headless doğrulayamıyor; (b) sahnede `endIntensity 0.03→0` plansız değişiklik, istenmiyorsa geri al; (c) `StringTable Shared Data`'daki event açıklamaları eski yüzdelerde kalmış olabilir.
3. **PERK MİMARİSİ — (a) ✅ KAPANDI / (b) 🟡 AÇIK.**
   - ✅ **Asset bozulması durdu** — seçenek **B (snapshot + restore)** uygulandı (2026-08-07, `6e945bb`, kontrol ONAY). 13 alan (SO×7 + `Truck` prefab×4 + `PlayerMovement` prefab×2) `UpgradePanel`'de static snapshot'a alınıp `OnNetworkDespawn`+`OnDestroy`'da geri yazılıyor. Denge değişmedi; denetçi 179/179.
   - 🟡 **AÇIK — seçenek C: 5 ölü perk.** `gambler_case` · `all_in` (ödül kısmı) · `prestige_broker` · `fast_hangar` · `agile_crew` hâlâ etkisiz: perk prefab'a yazıyor ama `Truck.Awake` her spawn'da değerleri SO'dan yeniden okuyor (`Truck.cs:211-218`); `PlayerMovement` de prefab olduğu için mevcut oyunculara ulaşmıyor. Fix = `TruckSpawner`/spawn yolunun aktif perkleri canlı instance'a uygulaması. **⚠️ 5 perk canlanınca ekonomi kayar → economist turu ŞART, ayrı denge turu.** Play-test'ten SONRA yapılmalı (ölçüm tabanı kirlenmesin).
4. 📖 ~~GDD senkronu~~ ✅ **BİTTİ** (2026-08-07, `bc43773`) — §4/§5/§6/§7/§8/§13/§14/§16/§19/§21/§31 koda hizalandı; §7 Kota Sistemi tamamen kaldırıldı (kodda yok).
5. 🧹 **Temizlik onayı bekliyor:** kök `herhangi` (0 bayt) + `Assets/_Recovery/0 (1..13).unity` (13 dosya).

### 🟢 Latent / düşük öncelik
- **Ölü quest tetikleyicileri**: `CompleteMinigame` + `MakePackagingMistake` — `QuestTracker.Notify*` metodlarının gerçek çağıranı yok. 30 asset'in hiçbiri bu tipleri kullanmıyor (hepsi tip 1/2/3/4), o yüzden canlı bug değil; yeni görev tipi eklenirse önce bunlar bağlanmalı.
- **`CompleteSpecificColorTruck` (tip 6) D2 muafiyet listesinde yok** — aynı "tır arzı P ile küçülüyor" gerekçesi geçerli. Hiçbir canlı asset kullanmıyor; tip 6 kullanılacaksa önce economist.
- **`economySettings` bağlanmamış 2 component** (`UpgradePanel`, `GameStateManager`) — ikisinde de `Resources.Load` self-heal fallback'i var, runtime'da ölü değil, yalnız Inspector kirliliği.

> ⚠️ Batchmode EditMode **yalnız derlemeyi** doğrular; netcode senkron / UI / Play davranışı için gerçek Unity gerekir.
> ⚠️ **Unity YAML'a elle `float[]` yazma** — hex-blob `int[]`'te çalışır, `float[]`'te sessizce boş dizi üretir. Anahtarı hiç yazma, C# field initializer'a bırak.
> ℹ️ Kalıcı çalışma ağacı gürültüsü: font atlası + URP global settings yeniden-serileştirmesi — commit'lerde dışarıda bırakılıyor.

---

## 📌 Açık Kararlar (kullanıcı onayı gereken)
- [x] **Q1** Öncelik sırası onaylandı *(2026-07-06)*
- [x] **Q2** Kod organizasyon ikiliğine şimdilik dokunulmuyor *(2026-07-06)*
- [ ] **Q3** Otomatik test yatırımı — EditMode 9 test (roguelite/draft). Ekonomi formülleri için **gerçek unit test yazılamıyor**: `Assets/Tests/EditMode` yalnız `NewCss.Roguelite` assembly'sini görüyor, `GameEconomySettings` ise Assembly-CSharp'ta → asmdef refaktörü gerekir. Pratik ihtiyaç şimdilik `EconomyInvariantCheck.cs` (165 kontrol) ile karşılandı. **Karar: asmdef refaktörü yapılsın mı, yoksa denetçi yeterli mi?**

---

## 🗂️ Plan dosyaları
| Dosya | İçerik | Durum |
|---|---|---|
| **[plans/devam.md](plans/devam.md)** | Oturum logu — oturum başında ÖNCE bunu oku | 🚧 **canlı — gerçek kaynak** |
| **[plans/economy-rebuild-2026-07-30-faz4-final.md](plans/economy-rebuild-2026-07-30-faz4-final.md)** | Uygulanan nihai değer seti (§A gelir · §B değerler · §D sıra · §E ölçülecekler) | ✅ **uygulandı — play-test referansı** |
| [plans/economy-rebuild-2026-07-30.md](plans/economy-rebuild-2026-07-30.md) · [-faz2](plans/economy-rebuild-2026-07-30-faz2.md) · [-faz3](plans/economy-rebuild-2026-07-30-faz3.md) | 4 fazlık analiz (envanter, verim modeli, kira/prestij/event, upgrade/quest) | 📖 gerekçe kaynağı |
| **[plans/playtest-olcum-protokolu.md](plans/playtest-olcum-protokolu.md)** | 🙋 **Oynamadan önce aç** — ölçüm protokolü; tek zorunlu çıktı kutu/dk/oyuncu | 🔴 **bekliyor** |
| **[plans/telsiz-voice-chat.md](plans/telsiz-voice-chat.md)** | 🎙️ Telsiz (bas-konuş) tam implementasyon planı — mimari, 5 kritik karar, 10 adım, doğrulama A-D, riskler | 🟢 **onaylı — uygulama komutu bekliyor** |
| [plans/playtest-2026-07-19.md](plans/playtest-2026-07-19.md) | Regresyon checklist'i (27 madde) | 🟡 bayat — ekonomi kısmı FAZ4 öncesi (`maxPrestige 240` yazıyor) |
| [plans/quest-ekleme-rehberi.md](plans/quest-ekleme-rehberi.md) | 30 görevin Inspector doldurma rehberi + değer tablosu | 📖 referans |
| [plans/economy-audit-2026-07-20.md](plans/economy-audit-2026-07-20.md) | Holistik ekonomi denetimi (7 sistem) | 📖 tarihsel *(tır penceresi cap bulgusu FAZ4'te ÇÜRÜDÜ — darboğaz tır değil insan üretim hızı)* |
| [plans/roadmap.md](plans/roadmap.md) | Orijinal yol haritası + Sprint 0-3 + departman tablosu | 📖 referans *(Sprint 0-1 kapandı, Sprint 2 = GDD senkronu açık)* |
| [plans/manuel-gorevler.md](plans/manuel-gorevler.md) | 🙋 Unity Play/UI/multiplayer teyitleri | 🟡 bayat |
| [plans/quest-listesi.md](plans/quest-listesi.md) · [quest-redesign-2026-07-25.md](plans/quest-redesign-2026-07-25.md) · [economy-balance-round.md](plans/economy-balance-round.md) · [upgrade-round-2026-07-20.md](plans/upgrade-round-2026-07-20.md) · [upgrade-isim-listesi.md](plans/upgrade-isim-listesi.md) · [release-push.md](plans/release-push.md) · [roguelite-draft.md](plans/roguelite-draft.md) · [economy-audit-2026-07-13.md](plans/economy-audit-2026-07-13.md) · [-17](plans/economy-audit-2026-07-17.md) · [economy-balance.md](plans/economy-balance.md) | Bitmiş turlar ve tarihsel referanslar | 🗄️ arşiv niteliğinde |

**Referans raporlar (kök):** `GDD.md` (tasarım — ✅ **2026-08-07'de koda senkronlandı**), `UPGRADE_PRICING_REPORT.md`, `ECONOMY_BALANCE_REPORT.md`.
**Denetçi:** `Assets/Editor/EconomyInvariantCheck.cs` — 165 kontrol, menü `Cargor / Ekonomi Değerlerini Doğrula`. Ekonomi değeri değiştiren HER işten sonra ve **her play-test sonrası** çalıştır.
**Sim:** `tools/economy-sim/sim.js` v3.1 — `node tools/economy-sim/sim.js`. Başlığındaki her değer `dosya:satır` ile belgeli; denetimden önce gerçek koda karşı doğrula.

---

## 🏢 Departman kısa hatırlatma
Ekonomik değer (fiyat/süre/ödül/çarpan) → **economist** (gameplay uydurmasın). Kod değişikliği sonrası → **qa**. Her BÜYÜK iş çıktısı → **kontrol** ONAY kapısı, en fazla 3 tur. Tam tablo: [plans/roadmap.md §4](plans/roadmap.md).
