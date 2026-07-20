# CARGOR HOLİSTİK EKONOMİ DENETİMİ — 2026-07-20

> Kaynak: kapsamlı Opus economist denetimi. Sim (`tools/economy-sim/sim.js`) bugünkü iki değişiklikle (prestij 100-skala `1496949` + hangar P-bazlı `6ef9ad6`) senkronlandı ve koda karşı doğrulandı. Oyun koduna/asset'lerine DOKUNULMADI — bu bir denetim raporu.

## ADIM 0 — SİM SENKRONU (koda karşı doğrulandı)

| Alan | Eski sim | Yeni (canlı kod/asset) | Kaynak |
|---|---|---|---|
| prestigePerBonus | 10 | **4** | asset:25 |
| startingPrestige | 15 | **6** | PrestigeManager:16 |
| maxPrestige | 150 | **100** | PrestigeManager:19 |
| customerServedPrestigeBonus | 0.5 | **0.2** | asset:36 |
| customerLostPrestigePenalty | -1.5 | **-0.6** | asset:35 |
| wrongDeliveryPrestigePenalty | -0.2 | **-0.08** | asset:39 |
| boxDropPrestigePenalty | -0.05 | **-0.02** | asset:38 |
| hangarStayDuration | tek 30s | **P-bazlı {90,60,40,30}** | asset:24 hex, `GetHangarStayDuration()` |
| quest prestij havuzları | {1,2} | **{0.4,0.8}** (×0.4) | easy1-5.asset |

Para alanları (rewardPerBox 50, bonusPerTier 5, penaltyPerBox 40, baseRent, rentGrowth 1.15) değişmedi — rescale gereği para ekonomisi sabit (doğrulandı). `truckCapOptimistic` hangar süresine referans vermiyor → "hangar süresi OPTIMISTIC modelde inert" tekrar doğrulandı.

**Bonus doğrulamalar (memory drift):** fast_hangar perk bug ÇÖZÜLMÜŞ (`PerkEffect.cs:102` P-bazlı taban ×1.30); easy4/5 questId çakışması ÇÖZÜLMÜŞ (id4/id5 ayrı); FESTIVAL oransal + SURPRISE AUDIT 2× kodda doğrulandı.

## ADIM 1 — SİSTEM SİSTEM DENETİM

### 1. Kira / çekirdek döngü — ✅ SAĞLIKLI (bir P1 uyarı)
- Normal 1-4P hepsi 16. günü iflasetmeden bitiriyor. gün16 kasa/kira: 1P **1.29×**, 2P 1.91×, 3P 2.53×, 4P 2.69× — hepsi 3× altında, kira son güne kadar baskı koruyor, kasa şişmiyor. Death-spiral çözülü.
- **BULGU (P1):** Yavaş/zorlanan takımda 2. kira duvarı. Yavaş 2P/3P/4P **gün8** (2. kira), 1P gün12 (3. kira) RENT iflası. Çok-oyunculu kira (900/1200/1500) zorlanan takımın throughput'undan hızlı büyüyor → oyuncu eklemek zorlanan takımı kurtarmıyor, kira baskısını erkene çekiyor. Bilinçli zorluk mu, adaletsiz duvar mı → playtest.

### 2. Prestij (yeni 100-skala) — ✅ SAĞLIKLI
- Pacing korundu: 2P/3P/4P tavan günü 16/14/13, 1P tavana ulaşmıyor (final 75.9). 240-skala pacing'i birebir korundu.
- Ödül tier eğrisi artık TAVANLI (iyileşme): prestij 6→100 ile kutu-ödülü 55→175 TL (1.1×→3.5×). maxPrestige=100 clamp'i eski "sınırsız ödül" riskini kapatıyor.
- Kırılganlık oranı değişmedi: startingPrestige 6 + ceza -0.6 → ~10 ardışık kayıp = game over (rescale öncesiyle aynı oran). Sim'de prestij hiç iflasnedeni olmuyor (hep kira).

### 3. Upgrade'ler — ⚠️ EN BÜYÜK AÇIK (P1)
- Sim gerçek upgrade fiyatlarını modellemiyor (soyut "%50 fazla-kasa harca"). ROI eğrisi doğrulanmamış.
- `UPGRADE_PRICING_REPORT.md` v3.2 **2026-07-08, rescale ÖNCESİ**. Para değişmediği için TL fiyatları kabaca geçerli ama **prestij-tabanlı perkler bayat**: "Prestij Ustası" 0.5→0.65→0.8 anlatıyor ama canlı taban artık **0.2**; "Prestij Simsarı" bonusPerTier büyüklüğü de gözden geçirilmeli.
- Source/sink: upgrade'ler gelirin **%58-64'ünü** emiyor (ana sink), kira %21-35. Para dengesi büyük ölçüde upgrade sink'ine yaslı → underpriced/tükenirse kasa şişer.
- upgradeCostMultiplierPerPlayer=1.15 (4P upgrade 1.52× pahalı).

### 4. Görevler (quest) — ✅ SAĞLIKLI (bir P2)
- EV: easy1=18, easy2=17.6, easy3=**30** (eski 180 exploit düzeltilmiş). Dengeli.
- Gelir katkısı: 1P +131 TL/16gün (~8/gün), 4P +58 (ihmal). Doğru tasarım (küçük-takım yardımcısı).
- **BULGU (P2):** easy4/5 EV (21.6/24) > easy2 (17.6); efor farklı olduğundan tam dominasyon değil ama rasyonel oyuncu yüksek-EV seçer.
- 4 ölü tetikleyici ekonomik pasif; aktive edilirse her biri EV kalibrasyonu ister. Düşük öncelik.

### 5. Event'ler — ✅ SAĞLIKLI
- 16 event doğrulandı. Etkiler dar/dengeli bantta (ödül/müşteri/sabır ×0.7-1.3, exitDelay ×0.7-1.5).
- FESTIVAL oransal (kira %10-20) iyi ölçekleniyor; SURPRISE AUDIT 2× ceza caydırıcı ama adil; CUSTOMER SUPPORT (telefon ×1.5) zayıf ama zararsız.

### 6. Süreler / throughput — ⭐ EN BÜYÜK YAPISAL BULGU
- **Para modeli (kod-doğrulandı): para YALNIZ tır kutu-tesliminden gelir; müşteriler SIFIR para, yalnız prestij.** (`CustomerAI.cs` money grep=0; `Truck.cs:576` tek AddMoney). → **tır throughput = para tavanı**; müşteri talebi parayı gate'lemez, prestiji (→ ödül tier) besler.
- Gün-bazlı tır cap (optimistic, kutu/gün): 1P 5.7→7.9, 2P 11.5→15.8, 3P 17.2→23.7, 4P 22.9→31.6 (gün8→16). Talep (46-49) hep üstünde — ama "kayıp para" değil, çünkü para talebe değil tıra bağlı.
- P-bazlı hangarın etkisi (STRICT, gün8): P1 +27%, P2 +15%, P3 +6%, P4 0%. OPTIMISTIC'te sıfır. Düşük-P için mütevazı, zararsız.
- **Gerçek risk (P2, playtest):** Sim müşteri servisini tır kapasitesinden bağımsız modelliyor. Gerçekte raf-stoklama emeği ile tır-yükleme emeği aynı oyunculardan çekiliyorsa, yüksek talepte az oyuncuyla müşteri servissiz kaçar → `customerLostPrestigePenalty` birikimi sim'in göstermediği prestij kanaması. **2. belirsizlik:** `CustomerManager` talep (max 50) vs `PrestigeManager.GetCustomerCapacity()` (max 20 eşzamanlı) — hangisi gerçek spawn'ı yönetiyor? Talep fazla-tahmin edilmiş olabilir.

### 7. Para kaynak/sink 16 gün — ✅ SAĞLIKLI (Normal), deflasyon riski (Yavaş)
- Enflasyon yok (kasa 3× kira altında, ödül tier 100'de tavanlı). Upgrade ana sink. Deflasyon/iflas yalnız zorlanan takımda (bilinçli sınır).

## ADIM 2 — ÖNCELİK-SIRALI BULGULAR

| # | Öncelik | Bulgu | Önerilen yön |
|---|---|---|---|
| 1 | **P1** | Upgrade fiyat/ROI sim'de doğrulanmamış; v3.2 raporu rescale öncesi; prestij-perkleri (Ustası/Simsarı) bayat büyüklük (taban artık 0.2) | Adanmış upgrade-ROI turu: fiyatları tır-throughput gelir eğrisine karşı modelle; prestij-perkleri 0.2 tabanına göre yeniden hesapla |
| 2 | **P1** | Yavaş takımda 2. kira duvarı (2P-4P gün8 iflas) | Playtest ÖNCE: gerçek zorluk mu? Değilse 2. kira yumuşatma / grace genişletme |
| 3 | **P2** | Müşteri-servis ↔ tır-yükleme emek rekabeti sim'de yok; prestij kanaması riski + 2 müşteri-sistemi belirsizliği | Kör düzeltme YOK — playtest'te servis tamamlanma oranını enstrümante et; fix müşteri kapasite/sabır tarafında, tır cap DEĞİL |
| 4 | P2 | easy4/5 EV (21.6/24) > easy2 (17.6) | easy4/5 para havuzunu ~%20 kıs (opsiyonel) |
| 5 | P3 | 4 ölü quest tetikleyici; CUSTOMER SUPPORT zayıf | İleride varyete; her biri EV kalibrasyonu ister |

## En önemli yapısal karar önerileri
1. **Upgrade turu (P1) = sıradaki iş.** Tek doğrulanmamış büyük sistem. Sim'e gerçek upgrade fiyat/etkilerini ekle, ROI/payback hesapla, prestij-perkleri 0.2 tabanına göre yeniden büyüklüklendir. Source/sink (%58-64 upgrade) doğrulaması için de gerekli.
2. **Tır cap'i KÖR YÜKSELTME.** Tır throughput'u gelirin doğal governor'ı — kirayı anlamlı tutan şey bu. Açmak kirayı önemsizleştirir. Asıl soru "müşteri servissiz kaçıp prestij kanatıyor mu" → playtest.
3. **Zorlanan takım kira duvarı (P1).** 2P-4P Yavaş gün8 iflası; bilinçli mi playtest'e bağlı, değilse 2. kira yumuşatma / grace genişletme.

## Memory güncellemeleri (economist tarafından yapıldı)
- `prestige_100_rescale_2026-07-20.md`, `hangar_stay_duration_per_player.md` → UYGULANDI
- `fast_hangar_perk_bug.md` → ÇÖZÜLDÜ; `quest_easy4_5_duplicate.md` → çakışma çözüldü, P2 EV notu
- YENİ `money_comes_only_from_trucks.md` → temel para modeli
- `MEMORY.md` indeksi + `sim.js` başlığı senkronlandı
