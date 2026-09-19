# Cargor Ekonomi Denetimi — Çalışma Planı ve Durum Dosyası

**Başlatan:** Fable 5.1 (müdür, subagent KULLANMADAN — kullanıcı talimatı)
**Başlangıç:** 2026-09-18
**Dal:** `fix/difficulty-scaling-and-dead-code` (mevcut checkout)
**Onaylı üst plan:** `C:\Users\cicek\.claude\plans\modeli-fable-a-ald-m-elegant-donut.md` (erişilemezse bu dosya yeter)

---

## 1. Görev özeti ve kurallar

**Görev:** Cargor'un mevcut ekonomisinin tam denetimi: parametreleri koddan çıkar → matematiksel model → bağımsız Python simülasyonu (1-4 oyuncu × beceri × upgrade stratejisi × event) → analiz (8 soru) → somut, sim ile doğrulanmış öneriler → tek birleşik rapor + PNG grafikler.

**Bağlayıcı kurallar (devralan model de uyar):**
1. Oyun koduna, prefab'a, sahneye, ScriptableObject'e **HİÇBİR değişiklik yapılmaz.** Sadece okuma/hesap/simülasyon/rapor. Dokunulabilen yerler: `docs/economy/`, `tools/economy_sim/`.
2. **Subagent kullanılmaz** (economist dahil).
3. Her alt adım biter bitmez BU dosya güncellenir (kutucuk + DURUM + günlük). Ara sonuç hafızada tutulmaz, ilgili `0X-*.md` dosyasına yazılır.
4. Belirsizlikte kullanıcıya sorulmaz; en makul yorum seçilir, **"VARSAYIM"** etiketiyle yazılır.
5. Sayı uydurulmaz. Bulunamayan değer **"BULUNAMADI"** yazılır.
6. Eski raporlar (`ECONOMY_BALANCE_REPORT.md` 2026-07-07, `UPGRADE_PRICING_REPORT.md` 2026-07-08, `plans/economy-*.md`, `.claude/agent-memory/economist/*.md` round 1-12 2026-08-30) **ESKİ dengeleri** anlatır; yalnız karşılaştırma için okunur, **güncel kod esas**.
7. Grafik için `pip install matplotlib` denenir (onaylı); olmazsa stdlib PNG fallback.

**Çıktı yolları:**
```
docs/economy/PLAN.md                    (bu dosya)
docs/economy/01-parametreler.md
docs/economy/02-model.md
docs/economy/03-simulasyon-sonuclari.md
docs/economy/04-analiz.md
docs/economy/05-oneriler.md
docs/economy/ekonomi-turu.md            (birleşik final)
docs/economy/fig-*.png
tools/economy_sim/config.json, sim.py, results/
```

---

## 2. Aşamalar

### AŞAMA 1 — Keşif → `01-parametreler.md`
**Hedef:** Her ekonomik sayı için `parametre | değer | birim | dosya:satır (veya asset yolu) | ne işe yarar`.
**Alt adımlar:**
- [x] 1.1 Tam okuma: `Assets/NewCss/GameEconomySettings.cs`, `Assets/Resources/EkonomiAyarlari.asset` (hex dizileri çöz, eksik anahtar = kod default), `Assets/NewCss/CustomerSripts/PrestigeManager.cs`, `Assets/NewCss/PostRentFeatureUnlocks.cs`, `Assets/NewCss/GameState/WaveSettings.cs` (+ `.asset` override varsa), `Assets/NewCss/BoxScripts/BoxFallPenalty.cs`, `Assets/NewCss/Roguelite/DraftPool.cs`, `Assets/NewCss/UpgradeScripts/PerkEffect.cs`, `Assets/NewCss/UIScripts/MoneySystem.cs`, `Assets/NewCss/TruckScripts/TruckSpawner.cs`
- [x] 1.2 Hedefli okuma (grep + satır aralığı): `DifficultyManager.cs`, `DayCycleManager.cs`, `Truck.cs`, `CustomerManager.cs`, `CustomerAI.cs`, `PhoneCallManager.cs`, `GameStateManager.cs`, `UpgradePanel.cs`, `EventEffectManager.cs`, `EventCalendarUI.cs`, `Assets/Scripts/Quest/Manager/QuestManager.cs`, `Assets/Editor/EconomyInvariantCheck.cs`
- [x] 1.3 Sahne/prefab override taraması (`Assets/Scenes/*.unity`, prefab'lar): startingPrestige, startingMoney, moneyMultiplierPerPlayer, sabır, perk fiyatları. Kod ≠ sahne ise sahne esas, "SAHNE OVERRIDE" notu.
- [x] 1.4 `tools/economy-sim/sim.js` `SRC4` bloğu ile çapraz kontrol
- [x] 1.5 Eski raporlarla "ne değişmiş" özeti + açık sorular/varsayımlar listesi
**Bitti kriteri:** Listedeki her dosya işlendi, tablo yazıldı, açık sorular listelendi.

### AŞAMA 2 — Model → `02-model.md`
- [x] 2.1 Zaman modeli (gün süresi, saat dönüşümü, tır/hangar, varış aralığı × wave, sabır, etkileşim, telefon SkipTime)
- [x] 2.2 Para modeli (gelir formülü, giderler, kira/grace, cezalar, festival)
- [x] 2.3 Prestij modeli (kazanç/kayıp kaynakları, tavan, tier→ödül, kapasite bağlı mı)
- [x] 2.4 Upgrade/perk tablosu (fiyat, etki, kilit, reroll)
- [x] 2.5 Event tablosu (tetikleme, süre, çarpanlar)
- [x] 2.6 İnsan varsayımları tablosu (zayıf/orta/iyi profil; parametrik, gerekçeli)
**Bitti kriteri:** Her alt sistem için formül yazılı, varsayımlar ayrı tabloda.

### AŞAMA 3 — Simülasyon → `tools/economy_sim/` + `03-simulasyon-sonuclari.md` + PNG
- [x] 3.1 `config.json` (tüm parametreler + profil varsayımları)
- [x] 3.2 `sim.py` motoru (saniye çözünürlüklü gün döngüsü; müşteri/sabır/servis/tır/ödül/ceza/prestij/telefon/kira/grace/draft/event) — trace ile 1 model bug'ı bulundu ve düzeltildi (alışveriş, oyuncunun görevini eziyordu)
- [x] 3.3 Senaryo matrisi koşusu: P{1,2,3,4} × beceri{zayıf,orta,iyi} × strateji{hiç,açgözlü,mantıklı} × event{kapalı,açık}, 16 gün, N=300 seed'li → ort + P10 + iflas günü dağılımı (72 hücre, 43 s)
- [x] 3.4 Ek koşular: upgrade amortisman (19 kart tek tek), event izolasyonu (16 event), duyarlılık (13 varyant)
- [x] 3.5 `sim.js` ile çapraz doğrulama — gün-16 geliri %2.7, kira %0 fark; prestij/gün-1 farkı açıklandı
- [x] 3.6 Grafikler (6 PNG: money/prestige-by-day, playercount-compare, bankruptcy-heatmap, upgrade-payback, proposals-compare)
- [x] 3.7 `03-simulasyon-sonuclari.md` yazımı
**Bitti kriteri:** `python tools/economy_sim/sim.py` tek komutla tekrar üretilebilir; matris tamam.

### AŞAMA 4 — Analiz → `04-analiz.md`
- [x] 4.1-4.8 Sekiz soru (gün-gün tablo; iflas günleri; zorluk eğrisi; prestij kararı; ölü/zorunlu upgrade; dominant strateji/exploit; co-op iş yükü; faucet/sink) — her cevap senaryo id/tablo referanslı
**Bitti kriteri:** 8 cevap + kanıt. ✔

### AŞAMA 5 — Öneriler → `05-oneriler.md`
- [x] 5.1 Her sorun: ne / kanıt / önerilen değişiklik (sayı + dosya:satır) / **değişiklik sonrası sim sonucu** / etiket — 10 öneri + 7 ölçülüp reddedilen
- [x] 5.2 Oyuncu sayısı ölçekleme formülü önerisi
**Bitti kriteri:** Etki/zahmet sırasına göre listelendi. ✔

### BİRLEŞTİRME
- [x] 6.1 `ekonomi-turu.md` (01-05 + grafik referansları + varsayımlar + açık sorular)
- [x] 6.2 Doğrulama: `git status` → Assets/Packages/ProjectSettings'teki değişiklik listesi oturum başındakiyle **birebir aynı** (bu iş hiçbir oyun dosyasına dokunmadı); sim aynı seed ile bit-bit aynı sonucu veriyor (md5 teyitli)
- [x] 6.3 Terminalde kısa özet: en kritik 5 bulgu + rapor yolu

---

## 3. Onay kutuları (aşama düzeyi)

- [x] AŞAMA 0 — Plan
- [x] AŞAMA 1 — Keşif
- [x] AŞAMA 2 — Model
- [x] AŞAMA 3 — Simülasyon
- [x] AŞAMA 4 — Analiz
- [x] AŞAMA 5 — Öneriler
- [x] Birleştirme

---

## 4. DURUM

**Şu anki aşama:** Denetim TAMAMLANDI (Aşama 1-5 + birleştirme) · **Ö1+Ö2+Ö3 UYGULANDI ve doğrulandı** · kalan öneriler bekliyor.

**Son tamamlanan adım (2026-09-19):** Kullanıcı onayıyla Ö3+Ö1+Ö2 canlıya alındı, qa + kontrol kapısından geçirildi, kontrol'ün DÜZELTME GEREKLİ bulguları kapatıldı.

### Kod/sahne durumu — NE UYGULANDI, NE BEKLİYOR

| Öneri | Durum | Dosya |
|---|---|---|
| **Ö1** kart fiyatları (11 kart) | ✅ UYGULANDI | `Assets/Scenes/The Main Office.unity` (15 satır) |
| **Ö2** upgrade P-maliyet çarpanı → {1, 1.6, 2.1, 2.5} | ✅ UYGULANDI | `DifficultyManager.cs:73` **+ `Assets/DifficultyManager.prefab`** (prefab'a açıkça yazıldı — yalnız .cs yetmedi, bkz. aşağıdaki tuzak) |
| **Ö3** `fast_hangar` ×1.30 → ×0.75 | ✅ UYGULANDI | `PerkEffect.cs` `ApplyFastHangarToTruck` (+ kartın sahnedeki açıklama metni de düzeltildi) |
| Ö4 (2. servis istasyonu), Ö5-Ö10 | ⏳ BEKLİYOR | `05-oneriler.md` |

Yan güncellemeler: `Assets/Editor/EconomyInvariantCheck.cs` (yeni beklenen değerler + effectId-tabanlı fiyat nöbetçisi `CheckUpgradeByEffect`), `tools/economy_sim/config.json` (canlı değerlere senkron).

**⚠️ Uygulamada yakalanan tuzak (tekrarlanmasın):** `upgradeCostMultiplierByPlayerCount` alanı prefab'ta serialize EDİLMEMİŞTİ, ama Unity prefab cache'inden ESKİ değeri okuyordu → `DifficultyManager.cs` field initializer'ını değiştirmek **tek başına hiçbir şey yapmadı**. Invariant denetimi yakaladı. Değer prefab YAML'ına açıkça (liste formatı, hex DEĞİL) yazılınca düzeldi.

**Sıradaki adım (kullanıcı onayı bekler):** Ö4 (Paketleme İstasyonu → 2. servis istasyonu; sahnede boş `serviceTables[1]` slotu doldurulmalı) → Ö5 (`prestigePerBonus` 8→6) → Ö6 (taban kira +%15, **yalnız Ö1-Ö5'ten sonra**) → Ö7/Ö8/Ö9/Ö10.

**Aşama 1'in kritik bulguları (devralan bunları bilmeli):**
- Tek para kaynağı tır teslimi; müşteri para vermez, ürün (hammadde) verir. Kota = ürün arzı tavanı.
- Fiilen **tek servis istasyonu** (sahnede 2. slot boş) → aynı anda 1 müşteri; kuyruk 2.
- Ekonomi P-ölçeklemesi: kira ×1/2.24/3.93/5.62, ödül 50/55/70/88, kota 4-6 / 7-12 / 8-13 / 8-13 (P3=P4), upgrade maliyeti ×1/2/2.95/3.7, hangar 120/60/40/30 s, kargo 1-2/2-3/2-4/2-5.
- Prestijin tek ekonomik etkisi: kutu ödülü +5 TL / 8 prestij. Kapasite formülü ölü.
- Telefon: para 0, +0.4 prestij, sıradaki müşteriyi öne çeker, günün gerçek saniyesini yakar (34.8/14.8/14.2/14.2 s).
- Prefab override'ları: `interactionTime` 2 (kod 5), `exitDelay` 2 (kod 5). sim.js `exitDelay 5` ve quest cezaları BAYAT.
- Economist A1 denetimi (2026-09-18): mevcut sim.js modelinde ekonomi neredeyse kaybedilemez; sim.js RNG içermiyor, event modellemiyor.

---

## 5. İLERLEME GÜNLÜĞÜ

- 2026-09-18 — Plan onaylandı (Fable 5.1, plan modu). Önceki Sonnet oturumu taslağı bu dosyayla değiştirildi. Araç durumu: `python` 3.14 var, matplotlib/numpy yok, `node` var.
- 2026-09-18 — AŞAMA 1 bitti: 01-parametreler.md yazıldı (~120 parametre, dosya:satır'lı). 2 prefab override + 2 sim.js bayatlığı + 2 ölü kablo bulundu.
- 2026-09-18 — AŞAMA 2 bitti: 02-model.md. Analitik ön-bulgu: gün 1'de spawn aralığı yüzünden kota P3/P4'te yetişmiyor (7.5 spawn kapasitesi vs 8 kota), telefon bu açığı kapatıyor; 1P tek hangar ≈ 1.2-2 tır/gün.
- 2026-09-18 — AŞAMA 3 bitti: `tools/economy_sim/{config.json,sim.py}` + 72 hücre × 300 koşu matris, 19 kart amortismanı, 16 event izolasyonu, 13 duyarlılık varyantı, 6 grafik. **Simülasyonun kendi bug'ı** trace ile bulundu ve düzeltildi (alışveriş oyuncunun görevini eziyordu). `sim.js` ile çapraz kontrol: gün-16 geliri %2.7 fark.
- 2026-09-18 — AŞAMA 4 bitti: 04-analiz.md, 8 soru + 6 yapısal sorun. Ana bulgu: **"hiç upgrade almamak" 12/12 hücrede optimal**; ölümlerin %72'si gün 8; prestij ölümü imkânsız; event'ler kasanın %4'ü.
- 2026-09-19 — **Ö3+Ö1+Ö2 UYGULANDI** (kullanıcı onayı). Doğrulama: Unity invariant 223/223 (exit 0), EditMode 132/132 — çıktılar `docs/economy/verification/2026-09-19-unity-dogrulama.md`. Ö2 önce sessizce etkisiz kaldı (prefab cache tuzağı), prefab'a açık yazımla çözüldü. qa: kod temiz, tek bulgu `fast_hangar` kartının açıklama metni hâlâ "%30 uzar" diyordu → düzeltildi. kontrol: DÜZELTME GEREKLİ (aşağıdaki 3 madde), hepsi kapatıldı.
- 2026-09-19 — **kontrol düzeltme turu:** (1) uygulama sonrası sim koşusu raporun baseline kanıtını (`matrix.csv`/`summary.json`/`daily.csv` + grafikler) ezmişti → denetim anındaki config `tools/economy_sim/config_baseline_pre.json` olarak donduruldu, baseline birebir yeniden üretildi (P1 %64.0 / P2 %24.0 / P3 %27.3 / P4 %49.3 — rapor metniyle aynı), uygulama sonrası koşu ayrı ada (`*_post_uygulama`) alındı; `proposals.py` artık varsayılan olarak donmuş baseline'ı taban alıyor. (2) PLAN.md implementasyonu loglamıyordu → DURUM bölümü yeniden yazıldı. (3) `EconomyInvariantCheck.cs` yorumu prefab'a yazımdan sonra bayatlamıştı → düzeltildi.
- 2026-09-18 — AŞAMA 5 + birleştirme bitti: `tools/economy_sim/proposals.py` ile 17 paket ölçüldü. Önerilen paket (REC) kaybı %49.2 → %30.4'e indiriyor, amorti eden kart 6 → 11, kart almak ilk kez kazançlı. 7 öneri ölçülüp REDDEDİLDİ (kira düzleştirme kötüleştirdi, grace ×2 oyunu kaybedilemez yapıyor, maxPrestige/event güçlendirme sıfır etki).

---

## 6. DEVRALMA TALİMATI

Sohbet geçmişine erişimin yoksa:
1. Bu dosyanın **DURUM** bölümünü oku — hangi aşama/adımdayız.
2. Var olan `docs/economy/0X-*.md` dosyalarını sırayla oku (sadece var olanları). Bunlar tamamlanmış aşamaların çıktısıdır; **tekrar yapma**.
3. `tools/economy_sim/` varsa `python tools/economy_sim/sim.py --help` ile çalışır mı bak.
4. Bölüm 1'deki kurallara uy (kod değişikliği yok, subagent yok, VARSAYIM etiketi).
5. DURUM'daki "Sıradaki adım"dan devam et; her alt adımda bu dosyayı güncelle.
6. Kaynak dosyalar `01-parametreler.md`'de dosya:satır ile listelidir; bir değerden şüphelenirsen kodu tekrar grep'le, tabloyu düzelt ve günlüğe not düş.
