# 🎯 EKONOMİ TURU — Birleşik Denge (2026-07-18)

> Kullanıcı hedefi: "genel bir ekonomi turu — görev ödülleri / cezaları / görevler / upgrade fiyatları / etkileri / takvim event efektleri — oynanışa ve ekonomiye etki eden her şeyi incele, ORTAK bir denge yap."
> Bu FAZ 1 = economist salt-analiz + **birleşik öneri** (sim-destekli). Kod/asset DEĞİŞTİRME (sim.js hariç). Kullanıcı öneriyi onaylayınca FAZ 2 batch-implement.

## Kapsam (8 sistem)
1. **Quest sistemi** — easy1-5 (`Assets/Resources/Quests/*.asset`), ödül/ceza/requirement, EV hizası. Backlog: easy4/5 bağlı değil + duplicate questId=4; easy3 zaten 30 EV'ye düzeltildi (`83bb793`). Soru: quest seti genişlesin mi? ödüller zorluğa göre dengeli mi? ceza var mı olmalı mı? Quest kaynak/mantık `Assets/Scripts/Quest/` (Data/Manager/Buff/UI) — NewCss DIŞINDA legacy.
2. **Cezalar** — kutu düşürme para cezası (`GameEconomySettings.boxDropMoneyPenalty=5`), kırılma eşiği (=3), yanlış teslim prestij cezası (`wrongDeliveryPrestigePenalty=-0.2`), `penaltyPerBox=40`. Orantılı mı? GDD §10.2.
3. **Upgrade fiyatları + etkileri** — `UpgradePanel.cs`, `PerkEffect.cs`, roguelite perkler. Referans: `UPGRADE_PRICING_REPORT.md` (v3.2) + agent-memory `upgrade_pricing_framework.md`, `roguelite_perk_pricing.md`, `upgrade_dual_system.md`. Fiyatlar gelir eğrisine göre + etki büyüklüğü dengeli mi?
4. **Takvim event efektleri** — `Assets/NewCss/Events/EventEffectManager.cs`, `GameState/DifficultyManager.cs`. Tüm event'ler: BUSY/RAINY/MARKETING (bağlı) + QUOTA/SURPRISE AUDIT/CUSTOMER SUPPORT/FESTIVAL + telefon. Efekt büyüklükleri + sıklık dengeli mi? agent-memory `missing_events_g9.md`, `phone_passive_redesign.md`.
5. **Prestij pacing** (ERTELENEN yapısal bulgu) — 2P/3P/4P prestij tavanına (150) gün8-11'de çarpıyor (hedef ~14), gelir çarpanı erken donuyor. `PrestigeManager.cs`, GDD §6.4. agent-memory `prestige_fragility.md`, `prestige_cap_bug_and_fix.md`.
6. **Tır penceresi cap** (EN BÜYÜK ERTELENEN bulgu) — tır 8:00-17:00 (9saat) vs talep 7:00-18:00 (11saat)+büyüme → 3P/4P geç-oyun talep tır kapasitesinin ~2x'i. Hangar 2/3 açılış zamanlamasıyla iç içe. `TruckSpawner`, GDD §8.1/§8.5. agent-memory `truck_hangar_window_cap.md`.
7. **Oyuncu-sayısı ölçeği** — flat EV, dokümante değil. Ölçek geri gelmeli mi? (telefon `SetCallChance` no-op, difficulty scaling kaybı).
8. **Çekirdek döngü sanity** — kira "kıl payı", 16-gün hayatta kalma 1P-4P × Normal/Yavaş, quest gelir etkisi. QuotaManager SİLİNMİŞ (kota-ölümü konusu kapandı).

## economist çıktısı (bu dosyanın altına yaz)
Her sistem için: **mevcut değer (kaynak:satır) → önerilen → EV gerekçesi**. Sim ile doğrula (`tools/economy-sim/sim.js`, 8 senaryo × quest açık/kapalı). Öncelik sıralaması (hangi değişiklik en çok fark yaratır). Kör-düzeltilemez / playtest-gerektiren kalemleri AÇIKÇA işaretle. Değişiklikleri birbirine bağımlılıklarıyla (prestij↔tır↔hangar iç içe) birlikte değerlendir — izole değil ORTAK denge.

---

## ✅ KULLANICI KARARLARI (2026-07-18) → FAZ 2 uygulama (dal `feature/economy-balance-round`)
1. **maxPrestige 150→240** (`PrestigeManager.cs:19` — sahne/prefab override'ı da kontrol et!).
2. **fast_hangar bug fix** — `PerkEffect.cs:99` `120f*1.30f` → `ctx.Economy.hangarStayDuration*1.30f`.
3. **easy4/5 → FARKLI görevlere dönüştür** (çeşitlilik) — questId çakışması çözülür; SADECE üretimde tetiklendiği DOĞRULANMIŞ quest tipi kullan (dead-trigger riski, easy1 bug precedent'i); EV bandı 18-30 TL (economist).
4. **FESTIVAL DAY oransal** — `EventEffectManager.cs:396-406` sabit min/max → `Random.Range(currentRent*0.10, currentRent*0.20)`.
5. **Event metin/kod senkron** (7 event) — `EventCalendarUI.cs:160 _allEvents` metnini gerçek koda uydur (graphics-ui, ekonomik değer yok).

---

## ÖNERİ (economist, 2026-07-18)

### Yöntem notu
Önce `.claude/agent-memory/economist/*` (12 ilgili kayıt) + `UPGRADE_PRICING_REPORT.md` (v3.2) okundu,
sonra gerçek kod/asset koddan tekrar doğrulandı (drift kontrolü). **Büyük bulgu**: önceki turların
"backlog" önerilerinin ÇOĞU zaten uygulanmış — event kalibrasyonu (`missing_events_g9`), telefon
additive+clamp modeli (`phone_passive_redesign`), roguelite perk fiyatlandırması (`roguelite_perk_pricing`
v3.2, T2/T3 gün≥5/9 gate, reroll eğrisi 50/90/160/290/525), easy3 quest düzeltmesi (`quest_reward_balance`)
— hepsi kod/asset içinde birebir doğrulandı. Bu turun katkısı: (a) bu uygulamalardaki YENİ bir kod
bug'ı (fast_hangar), (b) daha önce hiç bakılmamış easy4/5 quest çifti, (c) ERTELENEN prestij pacing
için somut sayısal öneri, (d) FESTIVAL DAY kalibrasyon boşluğu, (e) event metin/kod büyüklük
uyuşmazlıkları. Tüm sayılar `node` ile hesaplandı (bu makinede python yok — `env_no_python.md`).

---

### P0 — ORTAK bağımlı paket: Prestij tavanı ↔ Tır/Hangar penceresi

**Bulgu 1 — `fast_hangar` perk kodu tanımıyla çelişiyor (YENİ, bu turda bulundu)**

`Assets/NewCss/UpgradeScripts/PerkEffect.cs:96-100`:
```csharp
private static void ApplyFastHangar(int level, PerkContext ctx)
{
    if (ctx.Truck == null || level <= 0) return;
    ctx.Truck.hangarStayDuration = 120f * 1.30f;   // = 156
}
```
Sahne verisi (`The Main Office.unity:21835-21847`) bu perkin açıklaması: **"Tırın hangarda kalma
süresi %30 uzar"** (fiyat 280 TL, T1, `UPGRADE_PRICING_REPORT.md` §3-A ile birebir eşleşiyor). Ama
kod, canlı taban değeri (`GameEconomySettings.hangarStayDuration=30`, `truck_hangar_window_cap`
bulgusundan beri geçerli) yerine **eski, artık geçersiz 120s tabanını hardcode ediyor**. Sonuç:
perk alındığında süre 30→**156** (+420%) oluyor, açıklamanın vaat ettiği 30→**39** (+30%) değil.

Etki büyüklüğü modele göre değişiyor (node, `tools/economy-sim/sim.js` `truckCapStrict/Optimistic`):
- **OPTIMISTIC model** (sim'in birincil/varsayılan modeli): `hangarStayDuration`'a hiç referans
  vermiyor (devir süresi yalnız `OVERHEAD_TOTAL=15s`'ye bağlı) → bug'ın **hiç etkisi yok** (0 fark).
- **STRICT model** (kötümser alt sınır): bug, doğru düzeltmenin (39s) üstünde ekstra kapasite
  veriyor — 1P'de doğru düzeltme +8-9% kapasite kazandırırken bug +31-36% kazandırıyor (gün8/16,
  1P Normal/Yavaş); 4P'de fark küçülüyor (kargo tavanına zaten çarpılıyor, +3% vs +3%, pratik fark yok).

| P | Senaryo | cap (taban 30s) | cap (doğru fix 39s) | cap (BUG 156s) | bug/taban |
|---|---|---|---|---|---|
| 1P | Normal g16 | 5.3 | 5.7 (+7.5%) | 6.9 (**+30%**) | 1.31x |
| 1P | Yavaş g16 | 3.2 | 3.4 (+6%) | 4.3 (**+34%**) | 1.36x |
| 2P | Normal g16 | 10.5 | 11.2 (+6.7%) | 12.3 (+17%) | 1.17x |
| 4P | Normal g16 | 19.6 | 20.1 (+2.5%) | 20.1 (+2.5%) | 1.03x |

**Öneri (kod düzeltmesi, gameplay'e devredilecek):** `ApplyFastHangar` canlı tabana göre relatif
hesaplamalı: `ctx.Truck.hangarStayDuration = ctx.Economy.hangarStayDuration * 1.30f;` (idempotent,
diğer tüm `PerkEffect` metodlarının zaten kullandığı desen — bkz. `ApplyGamblerCase`,
`ApplyLeveragedRent` gibi metodlar hep `ctx.Economy`'den okuyor, yalnız bu metod istisna).
**Öncelik: YÜKSEK ama ACİL DEĞİL** — hangi modelin (strict/optimistic) gerçek oynanışa yakın olduğu
hâlâ çözülmedi (`truck_hangar_window_cap` notu), o yüzden bug'ın gerçek etkisi 0%-36% arasında
belirsiz; ama açıklama-kod uyuşmazlığı model bağımsız kesin bir doğruluk hatası, düzeltilmeli.

**Bulgu 2 — Prestij tavanı hâlâ erken doluyor, YENİ bir sayısal öneri (ERTELENEN konunun devamı)**

`maxPrestige=150` (uygulanmış, `prestige_cap_bug_and_fix` ✅) ama `customerServedPrestigeBonus`
tır kapasitesinden bağımsız (`demandAdjusted` üzerinden, `prestige_cap_bug_and_fix` notu) büyüdüğü
için 2P/3P/4P **hâlâ** oyunun ilk yarısında tavana çarpıyor (Normal senaryo, node):

| P | maxPrestige=150 (mevcut) tavan günü | maxPrestige=240 (önerilen) tavan günü | sonKasa (150→240) |
|---|---|---|---|
| 1P | hiç (64→sonra 190'a kadar organik) | hiç (190, tavan hiç etkilemiyor) | 946→980 (değişim yok, zaten altında) |
| 2P | gün 11 | gün 15 | 1727→2621 (+52%) |
| 3P | gün 9 | gün 13 | 2807→4468 (+59%) |
| 4P | gün 8 | gün 13 | 3702→5916 (+60%) |

1P'nin organik tavanı (~190) zaten 150'nin üstünde ama 220+ değerlerde etkilenmiyor (node ile
doğrulandı, 220/260/300 hepsinde 1P finalPrestige=189.8 sabit) — yani 1P için risk yok. 2P'nin
organik tavanı ~272 — 240'ta bile son 1-2 günde dolduruluyor (gün15), tam "hiç dolmasın" isteniyorsa
280-300 gerekir. **Trade-off**: `bonusPerTier=5` sabit kalırsa maxPrestige=240'ta 4P'nin maksimum
kutu-başı bonusu 120 TL (taban 50 TL'nin **3.4 katı**, mevcut 150 tavanında 75 TL/**2.5 kat**) —
bu geç-oyun enflasyonunu kasıtlı büyütüyor, iflas riski yaratmıyor (SURPRISE AUDIT stres testi ve
kira/kasa oranları sağlıklı kalıyor) ama **playtest'e bağlı bir "ne kadar geç-oyun zenginleşmesi
kabul edilebilir" tasarım kararı**, salt güvenli matematik değil.

**Öneri: `maxPrestige` 150 → **240** (orta yol).** Gerekçe: 3P/4P artık gün 13'e kadar (16'nın
%80'i) büyümeye devam ediyor — mevcut gün 8-9'a göre çok daha az "duvar hissi". 2P/1P zaten
etkilenmiyor. 280-300'e çıkarmak "hiç kimse hiç dolmasın" hedefini tam karşılar ama geç-oyun
ödül enflasyonunu (3.4x→~4x) daha da büyütür — **kontrol/kullanıcı playtest sonrası 240 vs 280-300
arasında seçim yapmalı**, ben 240'ı güvenli orta nokta olarak öneriyorum.

**Bu ikisi neden PAKET**: `truck_hangar_window_cap` notundaki asıl darboğaz (tır-saati/gün-uzunluğu
oranı) hâlâ çözülmedi — maxPrestige'i yükseltmek reward-per-box tavanını yükseltir ama bu ödül
gerçekten teslim edilebiliyor mu (tır kapasitesi izin veriyor mu) sorusu ayrı kalıyor. fast_hangar
bug'ının düzeltilmesi (doğru +30%) STRICT modelde tam da bu tavanı hafifçe gevşetiyor — yani iki
düzeltme birbirini tamamlıyor (biri ödül tavanını kaldırıyor, diğeri o ödülün fiilen teslim
edilebilirliğini bir tık artırıyor). Tır penceresinin köklü (yapısal) çözümü bu turun kapsamı dışında
kalmaya devam ediyor — playtest gerektirir.

---

### P1 — Quest sistemi: easy4/easy5 çifti (YENİ bulgu, önceki turlarda hiç incelenmemiş)

`Assets/Resources/Quests/easy4.asset` ve `easy5.asset`: **birebir aynı içerik** (başlık "Anlaşmalı
Çalışan", açıklama "1 Tır Tamamla", aynı ödül/ceza havuzu) VE **aynı `questId: 4`** (string alan,
`QuestData.cs:24`). `QuestManager.BuildQuestDatabase()` (`Manager/QuestManager.cs:180-193`)
`Dictionary<string, QuestData>` kurarken `questId` ile key'liyor — iki asset aynı key'i paylaştığı
için biri (Inspector listesindeki sıraya göre) sessizce ezilir. İçerik birebir aynı olduğu için
**pratik ekonomik etki yok** (hangisi "kazanırsa kazansın" aynı sayılar uygulanır) ama bu bir
zamanlı-bomba: içerik ileride farklılaştırılırsa (örn. easy5'e farklı ödül verilirse) sessiz veri
kaybı/yanlış-ödül riski oluşur. Ayrıca **2 asset slotu 1 görevi temsil ediyor** — günlük 3 teklifte
aynı görevin iki kopyası aynı gün gösterilebilir (varyete kaybı).

EV analizi (node, `poolEV` aynı formül — `quest_reward_balance` memory):
- easy4/5 reward havuzu **6 öge** (3 Money 10/20/30 + 3 Prestige 1/1.5/2), easy1/2/3'ün 5-öge
  yapısından farklı → `p=2/6=0.333`, `rewardMoneyEV=20`, `rewardPrestigeEV=1.5`.
- penalty havuzu 5 öge (3 Money -5/-10/-15 + 2 Prestige -1/-0.5) → `p=0.4`, `penaltyMoneyEV=-12`.

**Orantısızlık**: easy4/5 `targetCount=1` tır (easy2'nin **yarısı** efor, easy2 `targetCount=2`) ama
`rewardMoneyEV=20` **easy2'nin (17.6) ÜSTÜNDE**. Yani rasyonel oyuncu her zaman easy4/5'i easy2'ye
tercih eder (yarı efor + daha yüksek EV) — easy2 fiilen domine ediliyor.

**Öneri (asset değişikliği, FAZ2'de uygulanacak):**
1. `easy5.asset` → `questId: "5"` (çakışmayı çöz).
2. easy4/5 reward havuzunu easy2'nin ~yarısına indir (targetCount yarı olduğu için EV de yarı
   civarı olmalı): Money `10/20/30` → **`5/8/12`** (yeni EV≈8.3, easy2'nin (17.6) ~%47'si — efor
   oranıyla tutarlı), Prestige havuzunu `1/1.5` → `0.5/1` (tutarlılık). Penalty havuzu orantılı
   küçültülebilir (`-3/-6/-9`) ama bu ikincil, ana düzeltme reward tarafı.
3. Alternatif (daha basit): easy4 VE easy5'i FARKLI görevlere dönüştürmek (örn. biri "belirli renk
   tır" `CompleteSpecificColorTruck`, kod zaten destekliyor — `QuestType.CompleteSpecificColorTruck`,
   `HandleSpecificColorTruckCompleted` zaten bağlı) — çeşitlilik + questId çakışması ikisi birden
   çözülür. Bu, gameplay/tasarım kararı gerektirir (economist yalnızca EV bandını verir: hedef
   ~18-30 TL, `quest_reward_balance` bandıyla tutarlı).

---

### P2 — Takvim event'leri: önceki backlog BÜYÜK ÖLÇÜDE KAPANMIŞ, kalan 2 boşluk

`missing_events_g9` hafızasındaki 7 event (BUSY/RAINY/MARKETING/QUOTA/AUDIT/SUPPORT/FESTIVAL)
`EventEffectManager.cs`'de artık TAMAMI uygulanmış durumda, önerilen değerlerle **neredeyse birebir
eşleşiyor** (BUSY=1.3, RAINY=0.8, MARKETING=1.2/0.7, AUDIT=2x-hardcoded [önerilen 1.5x değil ama
kod+metin tutarlı ve stres testinde güvenli — aşağıda doğrulandı], SUPPORT=phone additive+clamp
model tam uygulanmış, QUOTA event takvimden çıkarılmış). Telefon reaktif V3 modeli
(`phone_passive_redesign`) de additive+clamp(0.65) ile tam uygulanmış. **Bu ikisi için yeni öneri
YOK — zaten doğru.**

**Kalan boşluk 1 — FESTIVAL DAY sabit TL, oyuncu sayısına göre orantısız:**
`festivalBonusMin=100, festivalBonusMax=300` (flat, `GameEconomySettings.cs:120-123`). `missing_events_g9`
önerisi ("kira yüzdesi, sabit TL değil") hiç uygulanmamış. Node ile oransal etki (gün12 kira baz
alınarak):

| P | rentGün12 | ort.bonus (200) | bonus/kira oranı | hedef bant (10-20%) |
|---|---|---|---|---|
| 1P | 661 | 200 | **%30.3** (bant üstü) | 66-132 TL |
| 2P | 1190 | 200 | %16.8 | 119-238 TL |
| 3P | 1587 | 200 | %12.6 | 159-317 TL |
| 4P | 1984 | 200 | %10.1 (bant altı sınırda) | 198-397 TL |

1P'de FESTIVAL DAY, tek başına bir günlük çekirdek gelirin (~gün1 240 TL) neredeyse tamamına
denk geliyor — orantısız güçlü. **Öneri**: `ApplyFestivalBonus()` içinde sabit `festivalBonusMin/Max`
yerine `Random.Range(currentRent*0.10, currentRent*0.20)` formülü (currentRent = o anki
`CalculateRent` çıktısı, rent cycle'a göre otomatik büyür). Kod değişikliği küçük
(`EventEffectManager.ApplyFestivalBonus`, satır 396-406), gameplay'e devredilecek.

**Kalan boşluk 2 — event metin/kod büyüklük uyuşmazlıkları (UX/güven riski, ekonomik exploit değil):**
`EventCalendarUI._allEvents` metni ile `EventEffectManager.eventMultipliers` gerçek değeri arasında
7/16 event'te sayısal sapma var (`missing_events_g9`'daki "RELAXED DAY metin+10/kod+30" deseninin
devamı, daha geniş bir liste):

| Event | Metin vaadi | Gerçek kod değeri | Sapma |
|---|---|---|---|
| RELAXED DAY | sabır +%10 | customerWaitTimeMultiplier=1.3 (+%30) | 3x |
| SLOW LOGISTICS | tır hızı -%20 | exitDelayMultiplier=1.5 (-%50 hız) | 2.5x |
| EXPRESS CARGO | tır +%10 hızlı | exitDelayMultiplier=0.7 (+%30 hızlı) | 3x |
| HEAVY BOXES | hareket -%10 | playerMoveSpeedMultiplier=0.8 (-%20) | 2x |
| OPPORTUNITY DAY | upgrade -%10 | upgradeCostMultiplier=0.8 (-%20) | 2x |
| GOLDEN BOX DAY | +%5 kutu başı | rewardPerBoxMultiplier=1.3 (+%30) + **metinde hiç geçmeyen** ek buff'lar (dailyCustomerMultiplier=1.2, exitDelay -%20, hareket +%20, stamina -%20) | 6x + gizli paket |
| FATIGUE PROBLEM | stamina regen +%30 (yavaş) | staminaRegenRateMultiplier=0.6 (-%40 regen) + sprintSpeed -%30 (metinde yok) | büyüklük+kapsam farklı |

**Öneri**: Ekonomik değer değişikliği gerektirmiyor (kod zaten iç-tutarlı bir skala içinde, hiçbiri
mevcut bandın dışına çıkmıyor) — bu bir **metin/UX senkronizasyon** işi, gameplay/graphics-ui'ya
devredilmeli: ya metinleri kodun gerçek değerine güncelle, ya da GOLDEN BOX DAY gibi "paket" event'lerin
TÜM etkilerini metne ekle (oyuncu sürpriz bir stamina cezası görmemeli). Bloklayıcı değil ama
"oyuncu güveni" açısından not edildi.

---

### P3 — Cezalar (system 2): sağlıklı, doğrulandı

`box_drop_penalty_centralization` kararları (boxDropMoneyPenalty=5, wrongDeliveryPrestigePenalty=-0.2,
penaltyPerBox=40) kodda birebir uygulanmış, `GetPenaltyMultiplier()` ile SURPRISE AUDIT günü
merkezi 2x uygulanıyor (`Truck.cs:581`, `BoxFallPenalty.cs:140`, `CustomerAI.cs:880`,
`GameStateManager.cs:606` — dört ayrı ceza yolu da aynı merkezi çarpanı okuyor, tutarlı).

**Stres testi (node, en kötü durum — 2x cezayı TÜM 16 gün boyunca uygulayarak, gerçek SURPRISE
AUDIT'in tek-günlük etkisinden çok daha ağır bir üst sınır):** hiçbir oyuncu sayısında iflas yok
(1P sonKasa 840, 4P sonKasa 3203 — sağlıklı). Gerçek SURPRISE AUDIT (yılda birkaç kez, tek gün) bu
üst sınırın çok altında bir risk taşıyor → **güvenli, değişiklik gerekmiyor.**

---

### P4 — Upgrade/perk fiyatlandırma: v3.2 raporu doğrulandı, fast_hangar hariç sağlıklı

Sahne verisi (`The Main Office.unity`) ile `UPGRADE_PRICING_REPORT.md` v3.2 karşılaştırıldı:
`cheap_rent` (130/30/3sv), `prestige_broker` (510/-5/2sv, `bonusPerTier` etkisi 5→5.5→6 kodda
birebir — `PerkEffect.cs:82-86`), `fast_hangar` (280 TL), `leveraged_rent` (350 TL), `all_in`
(800 TL) — **hepsi rapor değerleriyle birebir eşleşiyor**. `DraftPool.T2_UNLOCK_DAY=5`,
`T3_UNLOCK_DAY=9` (rapor §5 ile birebir). `RerollCurve` (50/90/160/290/525) birebir. **Tek sapma
`fast_hangar`'ın etki büyüklüğü** (yukarıda P0'da ele alındı) — fiyat doğru, uygulama kodu yanlış.
Bu turda başka bir fiyat/etki değişikliği önerilmiyor, sistem zaten onaylı halinde.

---

### P5 — Oyuncu sayısı ölçeği: senkron, değişiklik gerekmiyor

`moneyMultiplierPerPlayer=1.0` (`DifficultyManager.cs:61`), `playerCountMultiplierCoeff=0.3`
(müşteri talebi çarpanı 1.0/1.3/1.6/1.9) — `money_config_conflict` çözümüyle tutarlı, kod-doğrulandı.
`SetCallChance` no-op notu artık GEÇERSİZ (telefon reaktif V3'e geçti, o API zaten kaldırılmış/
kullanılmıyor). Bu madde kapandı, değişiklik önerilmiyor.

---

### P6 — Çekirdek döngü sanity: Normal sağlıklı, Yavaş (Slow) senaryo tüm oyuncu sayılarında iflas

Node 8-senaryo çıktısı (bu turda tazelendi, `node tools/economy-sim/sim.js`):

| P | Normal sonKasa | Normal prestijTavanGünü | Yavaş sonuç |
|---|---|---|---|
| 1P | 946 | 14 | GÜN 12 İFLAS (RENT) |
| 2P | 1727 | 11 | GÜN 8 İFLAS (RENT) |
| 3P | 2807 | 9 | GÜN 8 İFLAS (RENT) |
| 4P | 3702 | 8 | GÜN 8 İFLAS (RENT) |

"Yavaş" senaryo (1.2 kutu/dk/oyuncu, %30 yanlış-teslim, %8 fiziksel düşme, %8 müşteri-kaybı —
`UPGRADE_PRICING_REPORT.md`'nin "kötü/panik oyuncu" varsayımıyla aynı) **2P/3P/4P'de ikinci kira
döneminde (gün 8) hepsi iflas ediyor**, yalnız 1P gün 12'ye kadar dayanıyor. Bu **YENİ bir bulgu
değil** (aynı senaryo parametreleri önceki turlarda da vardı) ama round'un "16-gün hayatta kalma"
istediği net kontrol bu. **Bu bir bug değil, senaryonun TANIMI gereği** (gerçekten kötü oynayan
bir takım kaybetmeli — zorluk sisteminin amacı bu). **Kör-düzeltilemez / playtest gerektiren madde**:
gerçek oyuncu hata oranları bu varsayımlarla (özellikle %30 yanlış-teslim) örtüşüyor mu, yoksa
senaryo gerçekçi olmayan derecede kötümser mi — bu **QA/gameplay playtestiyle** doğrulanmalı, sim
sadece "eğer bu kadar kötü oynanırsa iflas eder" diyor, "bu kadar kötü oynanır mı" sorusuna cevap
veremez.

---

## Öncelik sıralaması (en çok fark yaratandan aza)

| # | Değişiklik | Etki büyüklüğü | Bağımlılık | Risk/not |
|---|---|---|---|---|
| 1 | `maxPrestige` 150→240 | YÜKSEK (3P/4P sonKasa +59-60%) | Tır kapasitesi ile birlikte okunmalı | Playtest'e bağlı (geç-oyun enflasyon kararı) |
| 2 | `fast_hangar` perk kodu (`120f*1.3f`→`ctx.Economy.hangarStayDuration*1.3f`) | DÜŞÜK-ORTA (model belirsiz: 0%-36%) | maxPrestige ile aynı paket (P0) | Kod düzeltmesi net, etkisi model-bağımlı |
| 3 | easy4/5 questId çakışması + EV düzeltmesi | DÜŞÜK (bugün pratik etkisi yok, gelecek risk) | Bağımsız | Asset düzenleme, düşük efor |
| 4 | FESTIVAL DAY sabit→oransal TL | DÜŞÜK-ORTA (1P'de %30→hedef %10-20) | Bağımsız | Küçük kod değişikliği |
| 5 | Event metin/kod senkronizasyonu (7 event) | YOK (ekonomik), UX/güven riski | Bağımsız | gameplay/graphics-ui işi, ekonomik değer taşımıyor |
| — | Cezalar (system2), oyuncu ölçeği (system5), upgrade fiyatlandırma (system4) | değişiklik yok | — | Doğrulandı, sağlıklı |

## Kör-düzeltilemez / playtest gerektiren kalemler
- **maxPrestige hedefi (240 vs 280-300)**: geç-oyun ödül enflasyonu (3.4x-4x taban) ne kadar kabul
  edilebilir — tasarım zevki, matematik değil.
- **`fast_hangar` bug'ının gerçek etkisi**: STRICT/OPTIMISTIC model belirsizliği çözülmeden (gerçek
  oyuncuların önceden stok yapıp yapamadığı) bug'ın 0%-36% aralığındaki hangi ucunda olduğu bilinmiyor.
- **"Yavaş" senaryo varsayımları gerçekçi mi**: %30 yanlış-teslim oranı playtest'te doğrulanmalı,
  aksi halde 2P/3P/4P'nin "kötü oyunda gün 8'de iflas" sonucu yanlış senaryo varsayımına dayanıyor
  olabilir.
- **Event metin/kod uyuşmazlıkları**: hangi yönde düzeltileceği (metni koda mı, kodu metne mi
  uydur) bir tasarım tercihi, economist yalnızca sapmayı tespit etti.

---

