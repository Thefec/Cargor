# 📋 CARGOR — Yol Haritası & Sprint Planı

> **Sahibi**: Müdür (planlama + delegasyon)
> **Oluşturulma**: 6 Temmuz 2026
> **Kaynak**: `Assets/NewCss` + `Assets/Scripts` kod taraması ↔ `GDD.md` karşılaştırması
> **Durum**: Sprint 0 onay bekliyor

Bu dosya şirketin ortak hafızasıdır. Her oturum başında oku, her tamamlanan işte güncelle.

---

## 1. 🔍 Mevcut Durum Değerlendirmesi (Kod Taraması)

Genel tablo beklenenden **sağlıklı**. GDD'de "eksik" görünen birçok sistem aslında yazılmış; sorun daha çok **dosya yolu tutarsızlığı, commit'lenmemiş yeniden yapılanma ve doğrulanmamış (test edilmemiş) tamamlık**.

### Var ve çalışır görünen sistemler (kodu mevcut)
- Gün döngüsü (`UIScripts/DayCycleManager.cs`), aydınlatma (`DayLightController.cs`)
- Ekonomi ayarları (`GameEconomySettings.cs` + yeni `Resources/EkonomiAyarlari.asset`)
- Para (`UIScripts/MoneySystem.cs`), prestij (`CustomerSripts/PrestigeManager.cs`), kota (`QuotaManager.cs`)
- Tır sistemi (`TruckScripts/Truck.cs`, `TruckSpawner.cs`, `GarageDoorController.cs`)
- Müşteri sistemi + **kapasite-bazlı spawn zaten kodda** (`CustomerManager.cs`: `CountActiveInteractables`, `_storeLevel`, `_shelfMultiplier`, `_levelMultiplier`)
- Pickup/envanter v2 (6 parçalı `NewPickup/PlayerInventory.*`), raf/masa (`TableScripts/`)
- Upgrade (`UpgradeScripts/`), telefon (`Phone/`), event (`Events/`)
- **Quest sistemi VAR** — `Assets/Scripts/Quest/` (Data, Manager, UI, Buff) + `BuffManager`
- Zorluk (`GameState/DifficultyManager.cs`), oyun durumu (`GameState/GameStateManager.cs`)
- Steam (`Steam/`), Discord (`Assets/Scripts/Discord/`), lokalizasyon (`Localization/`)
- Tutorial (`Assets/Tutorialassets/TutorialManager.cs` + `NewCss/Tutorial/` spawner'ları)

### Tespit edilen boşluklar / riskler
| # | Bulgu | Şiddet | Not |
|---|-------|--------|-----|
| R1 | **Commit'lenmemiş büyük yeniden yapılanma** — onlarca dosya NewCss kökünden alt klasörlere taşınmış, yeni asset'ler eklenmiş, hepsi working tree'de | 🔴 Yüksek | Kaybolma/çakışma riski. Önce stabilize + commit. |
| R2 | **GDD ↔ kod yol tutarsızlığı** — GDD `MoneySystem`, `DayCycleManager`'ı `GameState/` gösteriyor (gerçek: `UIScripts/`); Quest'i `NewCss/Quest` gösteriyor (gerçek: `Scripts/Quest`); `TutorialManager` yolu yanlış | 🟡 Orta | GDD canlı referans; yanıltıyor. Güncellenmeli. |
| R3 | **Doğrulanmamış tamamlık** — sistemlerin kodu var ama runtime'da GDD'deki spesifik davranışları (event zincirleri, ekonomi formülleri, multiplayer senkron) test edilmedi | 🟡 Orta | Unity içi test + qa denetimi gerekli. |
| R4 | **Ekonomi değerleri doğrulanmadı** — yeni `EkonomiAyarlari.asset` + kapasite spawn sistemi, GDD bölüm 31 simülasyon tablolarıyla eşleşiyor mu bilinmiyor | 🟡 Orta | economist doğrulaması şart. |
| R5 | **Otomatik test yok** — server-authoritative multiplayer ekonomi test kapsamı görünmüyor | 🟢 Düşük-Orta | Regresyon riski. |
| R6 | **Kod organizasyonu ikiliği** — `Assets/NewCss` (namespace `NewCss.*`) ile `Assets/Scripts` (Quest/Discord) ayrı; PickUpScripts (v1) + NewPickup (v2) birlikte duruyor | 🟢 Düşük | Teknik borç. |

> ⚠️ Not: "Var" işaretli sistemlerin kodu mevcut ama satır satır tamamlık/derleme denetimi yapılmadı. Sprint 1'in ilk işi bu doğrulama.

---

## 2. 🎯 Öncelik Sırası (Neden bu sıra?)

1. **Stabilizasyon** (R1) — commit'lenmemiş taşımalar üstüne iş yapmak riskli; önce zemin sağlamlaştırılır.
2. **Doğrulama & denetim** (R3, R4) — "var" dediklerimizin gerçekten çalıştığını kanıtla; ekonomiyi kilitle.
3. **Belge senkronu** (R2) — GDD'yi gerçeğe hizala; sonraki tüm işler doğru referansa dayansın.
4. **Cilalama & borç** (R5, R6) — test kapsamı ve organizasyon temizliği.

---

## 3. 🏃 Sprint Planı

### Sprint 0 — Stabilizasyon (kısa, blokaj kaldırma) → **devops**
- [ ] Working tree'yi incele: taşınan dosyaların meta/GUID bütünlüğünü doğrula (kırık referans var mı?)
- [ ] Unity'de projenin **derlendiğini** doğrula (özellikle `Quest.QuestManager`/`QuestTracker` çağrıları `NewCss.Quest`'e çözülüyor mu)
- [ ] Anlamlı commit(ler) oluştur: "reorg: NewCss klasör yapısı + EkonomiAyarlari asset"
- [ ] `.claude/`, scratchpad gibi geçici şeylerin `.gitignore` durumunu netleştir
- **Çıktı**: Temiz, derlenen, commit'lenmiş bir baz.

### Sprint 1 — Doğrulama & Denetim (paralel) → **qa** + **economist**
- [ ] **qa**: Çekirdek döngü denetimi — `OnNewDay` event hub'ına abone tüm sistemler (GDD 3.4) gerçekten bağlı mı? Kota→GameOver, prestij→GameOver, kira→grace zincirleri (GDD 21.2) kodda doğru mu? Salt-okunur bulgu raporu.
- [ ] **economist**: `EkonomiAyarlari.asset` + `GameEconomySettings.cs` değerlerini GDD bölüm 4/5/6/31 ile karşılaştır. Kapasite spawn formülü (GDD 9.6) ve simülasyon tablosu (GDD 31.3) tutuyor mu? Sapma raporu.
- [ ] **qa**: PickUpScripts (v1) vs NewPickup (v2) — hangisi canlı? Ölü kod var mı?
- **Çıktı**: Doğrulanmış sistem listesi + ekonomi sapma raporu.

### Sprint 2 — Belge Senkronu → **assistant** (+ müdür onayı)
- [ ] GDD'deki tüm dosya yollarını gerçek yollarla güncelle (R2: MoneySystem, DayCycleManager, Quest, TutorialManager…)
- [ ] Sprint 1 bulgularını GDD "Bilinen Riskler" bölümüne işle
- [ ] PLAN.md'yi Sprint 1 sonuçlarıyla güncelle
- **Çıktı**: GDD gerçekle %100 hizalı.

### Sprint 3 — Cilalama & Teknik Borç (Sprint 1 bulgularına göre önceliklenir)
- **gameplay**: qa'nın bulduğu kopuk event zincirleri / eksik davranışları tamamla
- **economist**: sapmalar için düzeltilmiş ekonomi değerleri öner (gameplay uygular)
- **graphics-ui**: doğrulama sırasında çıkan UI/HUD eksikleri (GDD 26.2)
- **devops**: NewCss vs Scripts organizasyon birleştirme kararı; temel EditMode testleri
- **Çıktı**: Oynanabilir, dengeli, dokümante dikey dilim.

---

## 4. 🏢 Departman Dağılım Tablosu

| İş | Departman | Sprint |
|----|-----------|--------|
| Stabilizasyon, commit, derleme, git, organizasyon | **devops** | 0, 3 |
| Kod denetimi, event zinciri & GameOver doğrulama, ölü kod | **qa** | 1 |
| Ekonomi değeri doğrulama, denge, simülasyon karşılaştırma | **economist** | 1, 3 |
| GDD güncelleme, rapor derleme, PLAN.md senkron | **assistant** | 2 |
| Kopuk mekanik tamamlama, gameplay düzeltmeleri | **gameplay** | 3 |
| UI/HUD eksikleri, görsel cila | **graphics-ui** | 3 |

**Kural hatırlatması**: Ekonomik değer (fiyat/süre/ödül/çarpan) gereken her işte önce economist'e danış. Önemli kod değişikliğinden sonra qa denetimi.

---

## 5. 📌 Açık Kararlar (Kullanıcı Onayı Gereken)
- [x] **Q1**: Öncelik sırası onaylandı → stabilizasyon → doğrulama → belge → cila. *(2026-07-06)*
- [x] **Q2**: Kod organizasyon ikiliğine **şimdilik dokunulmayacak**; NewCss vs Scripts birleştirme ertelendi. *(2026-07-06)*
- [ ] **Q3**: Otomatik test (Unity Test Framework) yatırımı — Sprint 1 sonrası tekrar değerlendirilecek.

---

## 5.5 💰 Ekonomi Denge Sprint'i (devam ediyor)

Kullanıcı isteğiyle ekonomi dengesi önceliklendirildi. economist + qa + gameplay ile yürütülüyor.

### Faz 1 — Temel değerler ⚠️ KOD YAPILDI, SAHNE DÜZELTİLİYOR (gameplay, 2026-07-07)
Raporlar: `ECONOMY_BALANCE_REPORT.md`. Kod default'ları + `EkonomiAyarlari.asset` güncellendi.
**qa uyarısı:** Sahne/prefab override'ları eski değerleri tutuyordu (runtime'da 50 TL / 1 prestij / prefab 100 & ×0.85), bu yüzden kod değişikliği tek başına ETKİSİZDİ. gameplay şu an bu override'ları düzeltiyor:
- `DifficultyManager.prefab` baseStartingMoney 100→500, moneyMultiplierPerPlayer 0.85→1.0
- `The Main Office.unity` MoneySystem startingMoney 50→500, PrestigeManager startingPrestige 1→15

Hedeflenen 7 değer:
| Değer | Eski (runtime gerçek) | Yeni |
|---|---|---|
| startingMoney (MoneySystem + DifficultyManager) | 100/61 | **500** |
| moneyMultiplierPerPlayer | 0.85 | **1.0** |
| rentGrowthMultiplier | 1.3 | **1.15** (iflas sarmalını çözen kaldıraç) |
| startingPrestige | 5.0 | **15.0** |
| customerLostPrestigePenalty | -2.0 | **-1.5** |
| penaltyPerBox | 60 | **40** |

economist testinde 1P/2P/4P üçü de 16 günü sağlıklı kasayla bitiriyor. **Henüz Unity'de test edilmedi.**

### Faz 2 — Upgrade fiyatlandırması 📋 RAPOR VAR ama SEVİYE SAYILARI YANLIŞ — economist yeniden fiyatlamalı
Rapor: `UPGRADE_PRICING_REPORT.md`. Prensip: fiyat = kattığı TL/gün değeri (düz 100×seviye değil), ~1.8-2.3 gün payback.
Eski rapor önerisi (tahmini seviye sayılarıyla): Raf 200→300, Masa 140→320, Ek Hangar 300→700→1250, Kuyruk 70→140, Stamina 30→80, Para 300→850, Görev Tier 50→550.

**⚠️ Gerçek upgrade envanteri (kullanıcı Unity'den getirdi, 2026-07-08) — rapor bunun üzerine yeniden yapılmalı:**

| Upgrade | Ne yapar | Gerçek satın alma sayısı | Rapor varsaymıştı | Not |
|---|---|---|---|---|
| **Storage** (raf) | Oyuna raf ekler (kapasite formülü) | **10** | 10 | ✅ uyuyor |
| **Table** (masa) | Ek paketleme masası ekler | **2** | 4 (öneri) | ❌ yeniden fiyatla |
| **Queue** (kuyruk) | Müşteri sırasını uzatır | **4** | 3 | ❌ yeniden fiyatla |
| **Money** (gelir) | Gelen parayı artırır | **3** | 5 | ❌ yeniden fiyatla |
| **Stamina** | Stamina dolma hızını artırır | **3** | 5 | ❌ yeniden fiyatla |
| **Truck** (hangar) | 2. ve 3. hangar kapılarını açar | **2** | 3 | ❌ yeniden fiyatla |
| **Quest Tier** | Görev zorluğu+ödülü artırır (görev sistemi şu an PASİF) | **2** | 3 | ❌ yeniden fiyatla |
| **Water** | Sadece başarım (achievement) tetikler — ekonomik değer yok | **1** | — | 🆕 raporda yok; nominal/sembolik fiyat |
| **Customer** | Müşteri bekleme süresini artırır (patience) | **2** | — | 🆕 raporda yok; economist değer biçmeli |

**economist görevi:** Yukarıdaki gerçek seviye sayılarına göre §3-9 tablolarını yeniden üret. Yeni iki upgrade (Water = ekonomik değersiz/sembolik fiyat; Customer patience = talebi yakalayan tip, kuyruk/masa mantığına benzer) için değer biç. Quest Tier'ın görev sistemi pasif olduğunu hesaba kat (şu an EV≈0).

### Bug'lar (gameplay düzeltiyor, 2026-07-07 — kullanıcı onayı: "önce bug'ları düzelt")
- 🔴 CANLI: `BoxFallPenalty.cs:17` çift eksi → kutu düşürünce prestij ceza yerine +0.05 ARTIYOR (ters mantık) — düzeltiliyor
- 🔴 `UpgradeAssets.GetCost()` → `MoreCapacity_4..15` bedava (0 TL) — şu an UI'a bağlı değil ama saatli bomba; fiyatlar dolduruluyor
- 🟡 Para-sıfırlanma race condition (`DifficultyManager.ApplyMoneySettings` guard'sız) — guard ekleniyor. Şu an otomatik tetikleyicisi yok (qa)
- ⏸️ Çift-kuyruk: `UpgradePanel` "Kuyruk" = canonical/canlı; `ItemType.QueueCapacity_1..3` = orphan ölü kod (hiç `.Buy()` çağrısı yok). Riskli olduğundan ŞİMDİLİK dokunulmuyor, sonra temizlenecek
- ⏸️ `PrestigeManager.GetCustomerCapacity()` dead-code — sonra karar

### gameplay bug düzeltmeleri ✅ TAMAMLANDI (2026-07-07)
- Sahne/prefab override'ları düzeltildi → **Faz 1 artık etkin** (runtime 500 TL / 15 prestij; prefab 500 & ×1.0)
- `BoxFallPenalty.cs:17` ters ceza düzeltildi (kutu düşünce artık doğru ceza)
- `UpgradeAssets.cs` MoreCapacity_4..15 fiyatları dolduruldu (bedava açık kapandı — ölü kod tarafı)
- `DifficultyManager.ApplyMoneySettings()` para-sıfırlama guard'ı eklendi (`GameStateManager.HasGameEverStarted`)
- ⚠️ Unity kapalıyken yapıldı; sonraki açılışta Console derleme kontrolü yapılmalı.

### ⏭️ SIRADAKİ OTURUM — buradan devam
1. ✅ **Kullanıcı Unity'den upgrade listesini getirdi (2026-07-08)** — gerçek envanter yukarıdaki Faz 2 tablosuna işlendi. 9 upgrade, gerçek seviye sayılarıyla.
   - Mimari not (hâlâ geçerli): gerçek upgrade'ler **Yol A (UpgradePanel, Inspector-driven)**. `UpgradeAssets.GetCost()` (Yol B) ölü kod. Faz 2 fiyatları Inspector/sahne YAML'ına uygulanmalı, koda değil.
2. **economist: raporu gerçek seviye sayılarına göre yeniden fiyatla** (Queue 4, Money 3, Stamina 3, Hangar 2, Quest 2, Table 2, Storage 10) + Water (sembolik) ve Customer (patience) için değer biç. → çıktı **kontrol**'den geçer.
3. Fiyatlar netleşince gameplay: Inspector/sahne YAML'ına uygula → **kontrol**'den geçer.
4. qa: 4 bug düzeltmesini + Faz 1'i doğrula (özellikle P4 guard'ının host-client'ta `GameStateManager.Instance` null senaryosu). → **kontrol**'den geçer.
5. Unity'de gerçek 1/2/4 kişi test.

> ⚙️ **Kurulum notu (2026-07-08):** Fable 5 **kontrol** kalite kapısı eklendi (`.claude/agents/kontrol.md` + CLAUDE.md iş akışı kuralı 4). Artık her departman çıktısı kontrol'den ONAY almadan kullanıcıya sunulmaz. **DİKKAT:** Departman agent'ları + kontrol yalnızca Claude Code **Cargor klasöründen** açıldığında yüklenir; üst `GitHub/` klasöründen açılan oturumda çözülmez.

---

## 5.6 🎲 Roguelite Upgrade Draft Sistemi (YENİ — brainstorming yarım, 2026-07-08)

Kullanıcı isteği: mağazayı "tüm upgrade'leri listele" düzeninden **gün sonu 3 rastgele kart** draft'ına çevir + yeni perk'ler ekleyerek çeşitlilik/kaos kat.

**Tasarım dosyası:** `docs/superpowers/specs/2026-07-08-roguelite-upgrade-draft-design.md`
**Durum:** ✅ **TASARIM TAMAM (2026-07-08).** Bölüm 1 (mekanik) + Bölüm 2 (16-perk roster & tier) + Bölüm 3 (birleşik ekonomi) + Bölüm 4 (sabit fiyat) + Bölüm 5 (veri-güdümlü mimari) onaylandı.

Onaylanan tasarım özeti:
- **Draft:** gün sonu masa trigger'ında panel açılır; içerik 3 kart; server-authoritative senkron; parası yeten hepsini alır; ertesi gün aktif; reroll (artan fiyat); RNG **kilidi açık tier içinde**.
- **Birleşik yapı:** fiziksel omurga KALIR (Raf 10, Masa 2, Hangar 2, Görev Tier 2 — kullanıcı Quest'i korumak istedi ama sistem pasif); soyut statlar (Stamina, rewardPerBox, Kuyruk, Sabır, Su) KALDIRILDI → yerine **16 yeni perk** (9 güvenli / 5 risk-trade-off / 2 sinerji). Su tamamen silindi.
- **Tier + kilit:** perk'ler T1/T2/T3; T2/T3 gün/mağaza eşiğiyle açılır (Ucuz Kira gibi OP'ler geç kilit).
- **Fiyat:** sabit (yüzde değil), tier bandına göre. **Mimari:** veri-güdümlü, kolay genişleyen (v1=20 kart havuzu, hedef 25-30).

**⚠️ Thread A (Faz 2 fiyat raporu) buraya SOĞURULDU:** Stamina/Money/Queue kaldırıldığı için ayrıca düzeltilmiyor. economist 4 omurga + 16 perk + reroll'u **tek seferde sıfırdan** fiyatlayacak; kontrol'ün 2 yapısal bulgusu (bütçe fizibilitesi + doğrusal fiyat modeli) yeni hesaba baştan konacak (bkz. spec Bölüm 4).

**✅ EKONOMİ KİLİTLENDİ (2026-07-08):** economist v3.2 → kontrol **ONAY** (3 turda: tur1 7 bulgu, tur2 Prestij Simsarı, tur3 ONAY). Tam fiyat tablosu `UPGRADE_PRICING_REPORT.md` v3.2. Özet:
- **Omurga:** Raf 200→290 (doğrusal, 10sv, top 2450), Masa 360/470, Hangar 300/700. Görev Tier feature-flag ile havuz dışı (sistem pasif).
- **16 perk:** T1 (150-240), T2 (220-380), T3 (Ucuz Kira 130/160/190, Kaldıraçlı Kira 350, Kelle Koltukta 800, Prestij Simsarı 510/505). Genel toplam 9945 TL.
- **Tier kilidi:** T1 hep açık; **T2 gün≥5; T3 gün≥9** (sadece gün-bazlı — storeLevel/prestij OR koşulları kaldırıldı).
- **Reroll:** 50/90/160/290/525 (×1.8, günlük sıfırlanır).
- **Bütçe fizibilitesi:** 1P düşük gelir "hepsini al" kasıtlı imkansız (seçim zorunlu); 2P sıkı; orta-yüksek mümkün.

**⚠️ gameplay'e taşınan 2 uygulama kalemi (kontrol notu, rapor revizyonu değil):**
1. `bonusPerTier` kodda `int` (`Truck.cs:105`, `GameEconomySettings.cs:54`) — Prestij Simsarı 5.5/6 için int→float + yuvarlama kuralı gerekli (`Truck.cs:596` `prestigeTiers*bonusPerTier` → `rewardPerBox` int'e ekleniyor; `EventEffectManager.cs:356/446` (int) cast).
2. `Truck_Anim (2).prefab:972` rewardPerBox:20 override — QA prefab/asset override zincirini teyit etmeli (projenin bilinen tuzağı).

**✅ UYGULAMA PLANI HAZIR (2026-07-08):** `docs/superpowers/plans/2026-07-08-roguelite-upgrade-draft.md`. 10 task (Task 0-9): bonusPerTier int→float, perk veri modeli, DraftPool+RerollCurve saf-mantık (EditMode testli), server-authoritative günlük teklif, 3-kart panel, reroll, 16-perk effect registry, veri girişi, qa+ölü kod. Mevcut UpgradePanel mimarisi (NetworkList seviyeler, _pendingUpgrades ertesi-gün-aktif, CalculateFinalCost) korunuyor; üstüne draft+tier+reroll katmanı biniyor.

### 🚧 UYGULAMA DEVAM EDİYOR — subagent-driven (2026-07-08, 2. oturum sonu)

**Branch:** `feature/roguelite-upgrade-draft` (base commit `1f40742` = baseline: ekonomi Faz1 + tüm planlama dokümanları).
**Yürütme modeli:** subagent-driven. Her task'ı **gameplay** uygular, müdür (controller) diff'i doğrular; **son kontrol whole-branch ONAY kapısı** en sonda (kontrol'ün "final kalite kapısı" rolü). Ledger: `.superpowers/sdd/progress.md`.

**✅ Tamamlanan task'lar (hepsi commit'li, controller diff-doğrulamalı):**
| Task | Commit | İş |
|---|---|---|
| 0 | `d5d010f` | `bonusPerTier` int→float + Mathf.RoundToInt (Truck.cs, GameEconomySettings.cs) |
| 1 | `847908e` | `PerkTier`/`PerkKind` enum'ları + `UpgradeDefinition`'a kind/tier/effectId/requiresQuestSystem |
| 2 | `60db470` | `DraftPool.cs` (tier+max filtresi, 3-kart seçim) + izole `NewCss.Roguelite.asmdef` + EditMode testleri; PerkTier.cs asmdef'e taşındı |

**Mimari karar (yeni oturum bilmeli):** Saf-mantık dosyaları (`PerkTier`, `DraftPool`, + Task 3'te `RerollCurve`) `Assets/NewCss/Roguelite/` altında **izole `NewCss.Roguelite` asmdef**'inde (autoReferenced=true → Assembly-CSharp/UpgradePanel otomatik görür). Test asmdef'i (`Assets/Tests/EditMode/Cargor.Tests.EditMode.asmdef`) bunu referanslar. `PerkEffect.cs` (Task 7) Assembly-CSharp'ta kalır (Truck/CustomerManager'a bağımlı).

**🔴 KRİTİK RİSK — Unity teyidi bekliyor:** Bu oturumda Unity hiç açılmadı. Tüm `.meta` dosyaları subagent'lar tarafından ELLE üretildi (PerkTier.cs'in hiç Unity-GUID'i olmamıştı). Ayrıca hiçbir C# CLI'dan derlenmedi, hiçbir EditMode testi koşulmadı. **Yeni oturumda İLK İŞ:** kullanıcı Unity Editor'ı açsın → reimport/meta regen → Console'da derleme hatası + GUID çakışması yok mu → EditMode Test Runner'da DraftPoolTests geçiyor mu. Sorun çıkarsa Task 0-2 düzeltilir.

**⏭️ Sıradaki (yeni oturum buradan devam):**
1. **Önce Unity teyidi** (yukarıdaki kritik risk) — kullanıcı Unity'de derleme+test doğrulasın.
2. **Task 3:** `RerollCurve.cs` (50/90/160/290/525) → `NewCss.Roguelite` asmdef'ine ekle + EditMode testleri. Plan Task 3.
3. **Task 4:** server-authoritative `_dailyOffer` NetworkList + tier-filtreli teklif üretimi (UpgradePanel.cs). Play doğrulaması.
4. **Task 5:** panel 3-kart draft. **Task 6:** reroll butonu. **Task 7:** 16-perk effect registry (`PerkEffect.cs`, Assembly-CSharp). **Task 8:** Inspector/sahne veri girişi (fiyatlar UPGRADE_PRICING_REPORT.md v3.2'den). **Task 9:** qa + ölü kod + prefab override.
5. **Son:** kontrol whole-branch ONAY → Unity 1/2/4 kişi test.

**Değer kaynağı:** her fiyat/tier/etki `UPGRADE_PRICING_REPORT.md` v3.2'den (kontrol ONAY'lı, genel toplam 9945 TL). Plan: `docs/superpowers/plans/2026-07-08-roguelite-upgrade-draft.md`. Task brief'leri önceki oturumun scratchpad'indeydi (devretmez) — plandan yeniden üretilir.

## 6. 📝 Değişiklik Günlüğü
- **2026-07-06**: İlk yol haritası oluşturuldu (kod taraması + GDD karşılaştırması).
- **2026-07-06**: Q1 (sprint sırası) ve Q2 (organizasyona dokunma) onaylandı. Sprint 0 başlamaya hazır.
- **2026-07-07**: Ekonomi denge sprint'i başladı. Faz 1 (7 değer) uygulandı. Faz 2 fiyat raporu hazır. qa doğrulama + bug analizi çalışıyor.
- **2026-07-08**: Fable 5 **kontrol** kalite kapısı eklendi (zorunlu final review, 3 tur, ONAY şartı). Kullanıcı gerçek upgrade envanterini getirdi (9 upgrade); Faz 2 raporunun seviye sayıları gerçekle uyuşmuyor → economist yeniden fiyatlayacak (+ Water/Customer yeni).
- **2026-07-08 (2. oturum)**: economist Faz 2 v2 raporu üretti → kontrol DÜZELTME GEREKLİ (bütçe fizibilitesi + doğrusal fiyat modeli bulguları). Ardından kullanıcı **pivot**: fiziksel upgrade'ler kalsın, soyut statlar kaldırılıp sıfırdan dengeli **roguelite perk havuzuna** dönüşsün. Roguelite spec tamamlandı (16 perk, T1/T2/T3 tier+kilit, sabit fiyat, veri-güdümlü mimari). Thread A fiyatlaması bu birleşik yapıya soğuruldu. Sıradaki: kullanıcı spec review → economist sıfırdan fiyatlama.
