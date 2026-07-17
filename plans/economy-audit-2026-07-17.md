# Ekonomi Doğrulama + Quest Ödül Dengesi — Birleşik Denetim (2026-07-18)

> Spec: `docs/superpowers/specs/2026-07-17-economy-quest-balance-design.md`
> Sim: `tools/economy-sim/sim.js` (repoda kalıcı, `node tools/economy-sim/sim.js` ile koşar)
> Selef: `plans/economy-audit-2026-07-13.md` (baseline, SİLİNMEDİ)
> Rol: economist — değer tablosu + gerekçe. Uygulama (asset dışı kod değişikliği) gameplay departmanına.

## 0. Doğrulanan GERÇEK değerler (2026-07-18) + 2026-07-13'e karşı delta

Kaynak dosyalar okunarak doğrulandı (varsayılmadı): `Assets/Resources/EkonomiAyarlari.asset`,
`Assets/NewCss/GameEconomySettings.cs`, `Assets/NewCss/CustomerSripts/PrestigeManager.cs`,
`Assets/NewCss/GameState/DifficultyManager.cs`, `Assets/NewCss/UIScripts/DayCycleManager.cs`,
`Assets/NewCss/TruckScripts/{Truck,TruckSpawner}.cs`, `Assets/NewCss/CustomerSripts/CustomerManager.cs`,
`Assets/Scripts/Quest/**`, `Assets/Resources/Quests/easy{1,2,3}.asset`.

| Değer | 2026-07-13 | 2026-07-18 (şimdi) | Kaynak | Etki |
|---|---|---|---|---|
| `maxPrestige` | GDD "100" yazıyordu ama koda **hiç clamp yoktu** (sınırsız büyüyordu — bilinen bug) | **150, GERÇEK canlı clamp var** | `PrestigeManager.cs:19,167` | Büyük — tier15 tavanı, +75 TL/kutu max bonus |
| `hangarStayDuration` | 120s (sim hiç modellemiyordu) | **30s**, bu turda ilk kez tır/hangar penceresi kapasite tavanı olarak modellendi | `GameEconomySettings.cs:48`, `Truck.cs:360` | **Büyük — bkz §6, şimdi para geliminin başlıca tavanı** |
| `requiredCargo` | "3-7" belgesiz | `Random.Range(2,6)` → **üst sınır hariç, gerçek küme {2,3,4,5}, ort=3.5** (6 DAHİL DEĞİL) | `TruckSpawner.cs:37-38,517` | Küçük, ama not edilmesi gereken bir kod nüansı |
| `boxDropMoneyPenalty` | tanımlıydı ama sim'de hiç kullanılmıyordu | **5 TL**, artık AYRI "fiziksel düşme" olayı olarak modellendi | `GameEconomySettings.cs:69` | Küçük TL, doğru eşleme önemli |
| `wrongDeliveryPrestigePenalty` | yoktu, sim yanlış-teslimat olayına `boxDropPrestigePenalty(-0.05)` uyguluyordu | **-0.2**, doğru olaya (yanlış renk teslimat) eşlendi | `GameEconomySettings.cs:111`, `Truck.cs:588` | Orta — metodoloji düzeltmesi |
| `wealthTaxRate` | C#'tan sökülmüştü ama sim'de hâlâ formülde vardı | **C# alanı tamamen yok** (asset'te yalnız zararsız orphan YAML anahtarı `wealthTaxRate: 0.1` kalmış — Unity'nin silinen-alan davranışı) | commit `9d2c3b0` | Yok — sim'den de tamamen söküldü |
| `QuotaManager` / kota-ölümü | dead code (dosya vardı, hiç çağrılmıyordu) | **Dosya TAMAMEN silindi** (`QuotaManager.cs` + `.meta`), QUOTA DAY event takvimden çıkarıldı | commit `0c026ef` (2026-07-14, "kota sistemini kaldir") | Yok — kota mekaniği artık kapsam dışı, bkz §6 |
| `startingPrestige` | 15 | 15 (değişmedi) | `PrestigeManager.cs:16` | — |
| `startingMoney` | 500 (P'den bağımsız) | 500 (değişmedi, `moneyMultiplierPerPlayer=1.0`) | `DifficultyManager.cs:36,61` | — |
| `rentGrowthMultiplier` | 1.15 | 1.15 (değişmedi) | `GameEconomySettings.cs:24` | — |
| customerServedPrestigeBonus bağlama | sim'de `correctDeliveries` (tır-teslim) üzerinden veriliyordu | **Metodoloji düzeltmesi**: gerçek kodda bu bonus CustomerAI/müşteri-servis akışından gelir, `Truck.cs`'de bu alana hiç referans yok (grep ile doğrulandı) → sim artık `demandAdjusted` (müşteri-servis, tır tavanından BAĞIMSIZ) üzerinden veriyor | — | Orta — prestij artık tır kapasitesine tıkanmıyor (bkz §2) |

**Ayrıca (kapsam dışı ama gözlemlendi):** `festivalBonusMin/Max` (100/300 TL) artık `GameEconomySettings.cs`'te gerçek/canlı alanlar — 2026-07-12 tarihli `missing_events_g9` hafızası FESTIVAL DAY'i "etkisiz" listelemişti, artık değil (sabit TL aralığıyla, hafızanın önerdiği %-kira formülüyle değil). Bu turun kapsamı dışı, sadece not.

## 1. 8-senaryo çekirdek sonuç (quest KAPALI, `numHangars=1`, `truckCapMode=optimistic`)

```
node tools/economy-sim/sim.js
```

| P | Senaryo | İflas | Son kasa | Son prestij | Prestij-tavan günü |
|---|---|---|---|---|---|
| 1 | Normal | yok | 946 | 150 | 14 |
| 2 | Normal | yok | 1727 | 150 | 11 |
| 3 | Normal | yok | 2807 | 150 | 9 |
| 4 | Normal | yok | 3702 | 150 | 8 |
| 1 | Yavaş | **GÜN 12 (RENT)** | 572 | 81.0 | — |
| 2 | Yavaş | **GÜN 8 (RENT)** | 806 | 69.7 | — |
| 3 | Yavaş | **GÜN 8 (RENT)** | 1067 | 82.6 | — |
| 4 | Yavaş | **GÜN 8 (RENT)** | 1460 | 98.9 | — |

İz-sürme ile doğrulandı (bkz. `node -e` trace, 1P/2P Yavaş): iflas mekaniği gerçek `DayCycleManager.TryProcessMoneyCheck()` iki-vuruşlu kuralını birebir izliyor (1. yetersiz kira→grace %80, 2. yetersiz kira→game over). 1P'nin 2P/3P/4P'den daha geç (gün12 vs gün8) iflas etmesi bug değil: temel kira 500 TL (2P/3P/4P: 900/1200/1500), Yavaş beceri cezası oyuncu-başı sabit olduğundan düşük-kira/oyuncu oranı 1P'ye bir kira-döngüsü fazladan nefes payı veriyor.

**Oyuncu ölçeği kontrolü**: 4P son kasa / 1P son kasa = 3702/946 = **3.91x** (< 4x, kıl payı geçiyor).

## 2. Prestij-tavan (150) etkisi — YENİ bulgu, 2026-07-14 analiziyle ÇELİŞİYOR (metodoloji nedeniyle)

`.claude/agent-memory/economist/prestige_cap_bug_and_fix.md` (2026-07-14) cap=150 için "3P hiç dolmuyor, 4P gün15" bulmuştu.
Bu turun sonucu farklı: **4P gün8, 3P gün9, 2P gün11, 1P gün14** dolduruyor. Fark bug değil,
**bilinçli metodoloji düzeltmesi**: önceki analiz (ve 2026-07-13 audit) prestij-servis bonusunu
`correctDeliveries` (tır-teslim sayısı) üzerinden veriyordu — yani prestij de tır kapasitesine
tıkanıyordu. Bu turda gerçek kod okunarak (`customerServedPrestigeBonus` yalnız CustomerAI akışında,
`Truck.cs`'de referansı yok) prestij artık `demandAdjusted` (müşteri-servis, tır tavanından bağımsız)
üzerinden veriliyor — daha kod-doğru ama daha hızlı tavan dolduruyor.

**Kriter durumu** ("tavana gün~14'ten önce çarpmamalı"): 1P PASS (tam gün14), **2P/3P/4P FAIL**
(gün8-11, hedeften 3-6 gün erken). Bu, kutu-başı ödül tier'ının (rewardPerBox+bonusPerTier×tier)
oyunun ikinci yarısında büyük takımlar için ERKEN düzleştiği anlamına gelir — büyüme motoru sadece
tır-kapasitesi tavanına değil, prestij tavanına da erken çarpıyor.

**Bu turun kapsamında DEĞİŞTİRİLMEDİ** (spec bu değeri "modelle" dedi, "düzelt" demedi — yalnız
easy3 için açık düzeltme talimatı vardı). **Öneri**: ayrı bir takip turunda `prestigePerBonus`/
`bonusPerTier` çok-oyunculu pacing'i (örn. tier eşiğini P'ye göre ölçekleme ya da bonusPerTier'i
hafif küçültüp tavanı geciktirme) değerlendirilmeli — upgrade fiyatlandırma turuyla birlikte ele
alınması mantıklı (ikisi de "büyük takım geç-oyun tavanı" temasında).

## 3. Tır/hangar penceresi — bu turun EN BÜYÜK yapısal bulgusu

`hangarStayDuration=30s` ilk kez modellendi. İki sınır hesaplandı (`tools/economy-sim/sim.js`
`truckCapStrict`/`truckCapOptimistic`):

- **STRICT** (kötümser, stok-yapma yok — her tır SADECE kendi 30sn penceresinde üretileni alır)
- **OPTIMISTIC** (birincil model — takım tır-saatleri boyunca [8:00-17:00, 9 oyun-saati] sürekli
  üretip rafa/istasyona ön-stok yapabilir; `easy3`'ün "rafa kutu koy" görevinin ayrı bir eylem
  olması ve `ShelfState`'in bağımsız bir `activeInteractable` olması bu varsayımı destekliyor —
  ama KOD İLE DOĞRULANMADI, playtest gerektirir, açıkça işaretli varsayım)

Gün 8 örneği (1 hangar):

| P | Senaryo | strict (kutu/gün) | optimistic (kutu/gün, BİRİNCİL) | statik talep referansı |
|---|---|---|---|---|
| 1 | Normal | 3.8 | 5.7 | 9 |
| 1 | Yavaş | 2.3 | 3.4 | 9 |
| 2 | Normal | 7.6 | 11.5 | 11 |
| 2 | Yavaş | 4.6 | 6.9 | 11 |
| 3 | Normal | 11.1 | 17.2 | 14 |
| 3 | Yavaş | 6.9 | 10.3 | 14 |
| 4 | Normal | 14.2 | 22.9 | 16 |
| 4 | Yavaş | 9.0 | 13.7 | 16 |

**Kritik alt-bulgu — hangar sayısı (1 vs 2) OPTIMISTIC modelde SONUCU DEĞİŞTİRMİYOR** (matematiksel
olarak doğrulandı): `optimisticCap = min(takımÜretimHızı×tırPenceresi, tırKabulKapasitesi)`. Test
edilen tüm senaryolarda (1P-4P, Normal/Yavaş, max 8 kutu/dk takım hızı) birinci terim (üretim)
ikinciyi (kabul) her zaman alt kırpıyor — ikinci terim ancak takım hızı ~14 kutu/dk'yı geçerse
bağlayıcı hale gelir. Yani asıl darboğaz **"hangar sayısı" değil, "tır sadece 8:00-17:00 çalışıyor
(9 saat) ama talep TAM GÜNE (7:00-18:00, 11 saat) + biriken yeniden-yatırım büyümesine göre
ölçekleniyor"** — 3P/4P'de mağaza yeniden-yatırımla hızla büyüdükçe (gün4-8 arası talep 16→49'a
çıkıyor, `numHangars` bağımsız) tır kapasitesi (gün8'de 4P: 22.9) talebin (49) çok gerisinde kalıyor.
Bu bir "iflas riski" değil (Normal senaryolar hâlâ sağlıklı bitiyor) ama **görünür talebin büyük bir
kısmının fiilen gelire dönüşmediği** anlamına geliyor — özellikle 3P/4P geç-oyunda.

**Sağlamlık kontrolü**: bu bulgu, kod-onaylı olmayan "6sn animasyon tamponu" varsayımına duyarlı
DEĞİL — yalnız kod-onaylı overhead (exitDelay 5s + ort. respawn 4s = 9s) ile de aynı nitel sonuç
çıkıyor (1P/Yavaş senaryolar oyunun neredeyse tamamında tır-tavanına takılı kalıyor).

**Öneri**: `hangarStayDuration`/hangar-açılış zamanlaması, upgrade fiyatlandırma turunda (Truck
upgrade'in hangar 2/3'ü ne zaman açtığı) birlikte ele alınmalı — bu turun kapsamı dışı ama artık
ekonominin en yüklü kaldıraçlarından biri olduğu netleşti.

## 4. Quest boyutu — açık/kapalı duyarlılık

**Mekanik doğrulandı** (`QuestManager.cs`): günde 3 görev gösterilir (`DAILY_QUEST_COUNT=3`) ama
**sadece 1 kabul edilebilir** (satır 591-597, günlük limit). Ödül/ceza havuzundan rastgele **2**
seçilir (`QuestData.MAX_SELECTED_REWARDS/PENALTIES=2`), her öge eşit olasılıkla seçilir
(`P(dahil) = min(2,havuzBüyüklüğü)/havuzBüyüklüğü`).

Model (sim'de `questDailyEV`): oyuncu **rasyonel** — sunulan 3 seçenek arasından (easy1/2/3'ün
ortalaması değil) **en yüksek para-EV'li olanı** seçer; en iyi seçeneğin EV'si dahi negatifse
**hiçbirini kabul etmez** (0/0). İlk taslak (havuzların ORTALAMASI, "her gün zorla kabul") struggling
takımlar için sistematik NEGATİF quest geliri üretiyordu (örn. 1P Yavaş: -0.32 prestij/gün) — bu,
`QuestManager.AcceptQuestInternal`'in kabulün opsiyonel olduğu gerçeğiyle çelişiyordu, düzeltildi.

Tamamlanma oranları (muhafazakâr, playtest yok — açık varsayım):
- **easy2** (2 tır tamamla): **dinamik hesaplanan** `fullTrucksPerDay` metriğinden (aynı tır-penceresi
  modeli) — P1: %8, P2 Normal: %20, P3 Normal: %65, P4 Normal: %85; tüm Yavaş senaryolar %8-20.
- **easy1** (5 kırmızı kutu paketle): sabit varsayım, Normal %80 (aralık 65-90%), Yavaş %56.
- **easy3** (rafa 5 kutu koy): sabit varsayım, Normal %65 (aralık 50-78%), Yavaş %46.
  (Spec'in kendi sıralaması korundu: easy1 "yüksek" > easy3 "orta-yüksek" > easy2 "orta".)

**Sonuç — quest AÇIK vs KAPALI (easy3 YENİ değerlerle)**:

| P | Senaryo | Kapalı kasa | Açık kasa | Delta | % |
|---|---|---|---|---|---|
| 1 | Normal | 946 | 991 | +45 | +4.8% |
| 2 | Normal | 1727 | 1760 | +33 | +1.9% |
| 3 | Normal | 2807 | 2835 | +28 | +1.0% |
| 4 | Normal | 3702 | 3731 | +29 | +0.8% |
| 1 | Yavaş | 572 | 589 | +17 | +3.0% |
| 2 | Yavaş | 806 | 815 | +9 | +1.1% |
| 3 | Yavaş | 1067 | 1112 | +45 | +4.2% |
| 4 | Yavaş | 1460 | 1469 | +9 | +0.6% |

Quest geliri her senaryoda **pozitif ama küçük** (%0.6-4.8) — çekirdek dengeyi bozmuyor, iflas
sonuçlarını değiştirmiyor (Yavaş senaryolar quest açıkken de aynı günde iflas ediyor). İflas
öncesi son güne kadarki artış prestijde de görülüyor (örn. 1P Yavaş 81.0→86.7).

## 5. easy3 kararı + uygulanan değerler

**Sorun (nicel kanıt)**: eski `easy3` para-ödül EV'si **180 TL** — `easy1`(18) ve `easy2`(17.6)'nin
**~10 katı**, aynı "Easy" tier'da. Somut ihlal: 1P Yavaş gün-1 çekirdek geliri **144 TL** —
eski easy3'ün EV'si (180) BUNU BİLE AŞIYOR (max tek-çekim 150+200=350 TL, günlük gelirin 2.4 katı).
Duyarlılık koşusu bunu doğruladı: quest AÇIK + ESKİ easy3 ile 1P Normal kasası 946→**1230**
(+284 TL, +30%) ve 4P 3702→**3943** (+241 TL, +6.5%) — YENİ değerlerin sağladığı +45/+29 TL'nin
**6.3-8.3 katı** şişirme.

**Uygulanan düzeltme** (`Assets/Resources/Quests/easy3.asset`, Edit ile UYGULANDI):

| Havuz | Eski | Yeni | Gerekçe |
|---|---|---|---|
| rewardPool (Money) | 100 / 150 / 200 | **15 / 25 / 35** | EV=0.4×75=**30 TL** (eski 180'in ~%17'si); easy1(18)/easy2(17.6)'ya göre **1.70x** — ±2x bandı içinde, hâlâ "en değerlisi" (görev teması: pasif/arka-plan iş → en düşük sürtünme, orantılı en yüksek ödül değil ama en tutarlı) |
| rewardPool (Prestige) | 1 / 2 | 1 / 2 (değişmedi) | easy1/easy2 ile birebir aynı, tutarlılık zaten vardı |
| penaltyPool (Money) | -20 / -10 / -5 (3 öge) | **-15 / -20 / -10** | EV=0.4×-45=**-18 TL**, reward'ın ~%60'ı (easy1 ~%67, easy2 ~%82 ile aynı bantta) |
| penaltyPool (Prestige) | **sadece -1 (1 öge, asimetrik)** | **-1 / -2 (2 öge)** | Havuz artık easy1/easy2 ile AYNI YAPIDA (3 para + 2 prestij = 5 öge, pick-2). EV=-1.2, easy1/easy2 ile TAM AYNI |

Doğrulama (kriter: "hiçbir aktif quest tek başına bir günlük çekirdek geliri aşmamalı"): yeni easy3
max tek-çekim = 25+35 = **60 TL** — en kırılgan senaryonun (1P Yavaş, gün1/gün8 ≈ 143-144 TL) bile
YARISINDAN AZ, güvenli marj. EV(30) her senaryoda günlük çekirdek gelirin (144-2960 TL aralığı)
**%1-21**'i — "anlamlı ama domine etmeyen" hedefiyle uyumlu, en hassas kesitte (1P Yavaş) bile tavan
altında.

**easy1/easy2 değiştirilmedi** — zaten birbirine yakın (18 vs 17.6, 1.02x) ve hiçbir kritik ihlali
yok, gereksiz churn'den kaçınıldı.

## 6. Kriter-geçme tablosu (spec §6)

| Eksen | Hedef | Sonuç | Durum |
|---|---|---|---|
| İflas (Normal) | 1P-4P hiçbiri iflas etmemeli | 1P-4P: hiç iflas yok (quest açık/kapalı ikisinde de) | ✅ PASS |
| İflas (Yavaş) | Erken iflas (~gün8) | 2P/3P/4P gün8 (tam hedef), 1P gün12 (biraz geç ama yine de kesin/erken başarısız) | ✅ PASS (1P'de küçük sapma, açıklandı §1) |
| Prestij pacing | Tavana (150) gün~14'ten önce çarpmamalı | 1P gün14 ✅; **2P gün11, 3P gün9, 4P gün8 ❌** | ⚠️ **FAIL (çok-oyunculu)** — §2'de açıklandı, takip önerisi var |
| Kira baskısı | Gün16 kirası sonrası kasa, kiranın 3 katını aşmamalı | 1.24x-1.62x (hepsi rahat marjda, hiçbiri 3x'e yaklaşmıyor) | ✅ PASS |
| Oyuncu ölçeği | 4P final kasa 1P'nin 4 katını aşmamalı | 3.91x (kapalı) / 3.76x (açık) | ✅ PASS (kıl payı) |
| Quest denge | Tek quest 1 günlük geliri aşmamalı; easy1-3 ±2x | Eski easy3: **İHLAL** (180 EV > 144 TL gün1-Yavaş geliri); Yeni easy3: 30 EV, 1.70x, max-tek-çekim 60 < her senaryonun en düşük günü | ❌→✅ **FAIL(eski)→PASS(yeni, bu turda düzeltildi)** |

**Genel değerlendirme**: 6 eksenden 5'i PASS (biri küçük sapmayla). Tek gerçek FAIL — çok-oyunculu
prestij pacing — bu turun editable kapsamı dışında (spec yalnız modellemeyi istedi, değer değişikliği
istemedi); ayrı takip turu önerildi (§2).

## 7. Kalan riskler (öncelik sırasıyla)

1. **[BÜYÜK, YENİ] Tır/hangar penceresi artık ekonominin en yüklü kaldıracı** (§3). Talep büyüdükçe
   (özellikle 3P/4P geç-oyun) fiili gelir tır-kapasitesine tıkanıyor. `numHangars` varsayımı
   (sabit 1, GDD "başlangıçta 1 hangar" baz alındı) upgrade-fiyatlandırma turunda hangar-açılış
   zamanlamasıyla birlikte gözden geçirilmeli. STRICT/OPTIMISTIC modelleri arasındaki fark
   (stoklama var mı yok mu) **playtest ile doğrulanmalı** — şu an kod bunu kanıtlamıyor, sadece
   `ShelfState`'in ayrı bir etkileşim noktası olması OPTIMISTIC'i destekliyor.
2. **[ORTA] Çok-oyunculu prestij tavanı erken doluyor** (§2, §6) — 2P/3P/4P hedeften 3-6 gün erken
   tavana çarpıyor. Ayrı takip turu önerildi.
3. **[KÜÇÜK] `GameEconomySettings.cs:152-297`** bayat C# ContextMenu simülasyonu (prestij başlangıcı
   5, clamp üst sınırı 100, `playerCountMultiplier` yok, 15 günlük döngü) hâlâ kodda duruyor ve
   **yanlış cevap üretiyor** — birisi yanlışlıkla çalıştırırsa güncel değerlerle çelişir. **Silinmeli**
   (bu turda YAPMA talimatı gereği silinmedi — Unity açıkken kod silme riskli, müdür/devops
   koordine etmeli).
4. **[KÜÇÜK] Sahne borcu**: `The Main Office.unity` içinde hâlâ "QuotaManager" adlı GameObject +
   muhtemelen "Kargo" HUD text'i var (script'i silinmiş, missing-script durumu) — `0c026ef` commit
   mesajında zaten not edilmiş, bu turun kapsamı dışı, hatırlatma.
5. **[BELGE]** `GDD.md:248` hâlâ `hangarStayDuration: 120s` yazıyor (kod 30s) ve `GDD.md` bölüm 4.2
   telefon ayarları eski proaktif modeli (`callReward`, `timeSkipAmount`, `maxCallsPerHour`) listeliyor
   (kod çoktan pasif/reaktif modele geçti, bkz `phone_passive_redesign` hafızası). Ekonomi kapsamı
   dışı ama GDD güncellemesi gerekiyor.
6. **Playtest-bağımlı varsayımlar (değişmedi, hatırlatma)**: `boxesPerMinPerPlayer` (2.0/1.2),
   `wrongDeliveryRate` (0.2/0.3), `customerLossRate` (0.03/0.08), quest tamamlanma oranları (easy1/3) —
   hiçbiri ölçülmedi, sim gerçek playtest verisi geldiğinde ucuz şekilde yeniden koşulabilir
   (`tools/economy-sim/sim.js` artık repoda kalıcı).

## 8. Yöntem notları / gelecek denetim için

- Sim artık `tools/economy-sim/sim.js` — kaynak değerler dosya:satır ile başlıkta, `module.exports`
  ile hem `node tools/economy-sim/sim.js` (tam rapor) hem `require(...)` ile nokta-sorgular
  (`node -e "require('./tools/economy-sim/sim.js').runSim(...)"`) destekleniyor.
- Bu turdaki 3 metodoloji düzeltmesi (§0 son satırlar) önceki denetimin sonuçlarıyla BİREBİR
  karşılaştırmayı bozar — 2026-07-13 raporundaki mutlak TL rakamları artık doğrudan kıyaslanabilir
  değil (yöntem değişti, sadece PASS/FAIL kriterleri kıyaslanabilir). Bu beklenen ve kabul edilebilir;
  yöntem düzeltmeleri gerçek koda daha sadık.
