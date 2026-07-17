---
name: truck_hangar_window_cap
description: hangarStayDuration=30s + tir sadece 8-17 calisiyor -> ekonominin en yuklu YENI kaldiraci; hangar sayisi (1 vs 2) OPTIMISTIC modelde onemsiz cikti, asil darbogaz tir-calisma-saati/gun-uzunlugu orani
metadata:
  type: project
---

2026-07-18 bulgusu (`plans/economy-audit-2026-07-17.md` §3, `tools/economy-sim/sim.js`
`truckCapStrict`/`truckCapOptimistic`). `hangarStayDuration` 120s→30s değişti (bkz
[[rent_death_spiral]] tarzı bir "tek parametre değişikliği köklü etki yaratır" örneği). İlk kez
tır/hangar döngüleme mekaniği modellendi (önceki denetimler bunu hiç hesaba katmıyordu, sadece
`durationMin × kutu/dk × P` düz oranını müşteri talebiyle kıyaslıyordu).

**İki model kuruldu:**
- STRICT (kötümser): her tır SADECE kendi 30sn penceresinde üretilebileni alır, stoklama yok.
- OPTIMISTIC (birincil, sim'de kullanılan): takım tır-çalışma-saatleri (8:00-17:00, 9 oyun-saati)
  boyunca sürekli üretip ön-stok yapabilir (rafa/istasyona), tır sadece devir-başına en fazla
  `requiredCargo` kabul eder. `easy3` görevinin "rafa kutu koy" ayrı bir eylem olması ve
  `ShelfState`'in bağımsız bir `activeInteractable` olması bu varsayımı destekliyor ama **KOD İLE
  DOĞRULANMADI** — gerçek davranış (oyuncular önceden kutu biriktirebiliyor mu, tır rengi
  eşleşmesi ne kadar sürtünme yaratıyor) playtest gerektirir.

**Kritik matematik bulgusu**: OPTIMISTIC modelde `cap = min(takımÜretimHızı×tırPenceresi,
tırKabulKapasitesi)`. Test edilen TÜM senaryolarda (1P-4P, Normal/Yavaş, max 8 kutu/dk takım hızı)
birinci terim (üretim) ikinciyi (tır kabul kapasitesi, hangar sayısıyla ölçekleniyor) her zaman alt
kırpıyor. Yani **hangar sayısı (1 vs 2) sonucu bu aralıkta HİÇ DEĞİŞTİRMİYOR** — asıl darboğaz
"kaç hangar var" değil, "**tır sadece 9 oyun-saati çalışıyor ama talep tam güne (11 saat) +
mağaza-büyüme yeniden-yatırımına göre ölçekleniyor**". İkinci terim (kabul kapasitesi) ancak takım
hızı ~14 kutu/dk'yı geçerse bağlayıcı olur (test edilen max 8'in çok üstünde).

**Sonuç**: 3P/4P'de mağaza yeniden-yatırımla hızla büyüdükçe (gün4-8 arası talep 16→49'a çıkabiliyor)
tır kapasitesi (gün8'de 4P optimistic: 22.9 kutu/gün) talebin (49) yarısından azını karşılıyor —
İFLAS RİSKİ DEĞİL (Normal senaryolar sağlıklı bitiyor) ama görünür talebin büyük kısmı fiilen
gelire dönüşmüyor. Bu, oyunun geç-oyun büyüme eğrisini görünmez şekilde düzleştiren bir mekanik.

**Sağlamlık**: Bulgu, kod-onaylı-olmayan "6sn animasyon tamponu" varsayımına duyarlı DEĞİL — sadece
kod-onaylı overhead (exitDelay 5s + ort. respawn 4s = 9s) ile de aynı nitel sonuç çıkıyor.

**Why:** Kullanıcı bu turda hangarStayDuration'ı (120→30s) ilk kez modellenmesini istedi (spec
`2026-07-17-economy-quest-balance-design.md` §4). Sonuç beklenenden büyük çıktı — 2026-07-13
denetiminin hiç görmediği bir kaldıraç ortaya çıkardı.

**How to apply:** Truck/hangar/upgrade fiyatlandırma ile ilgili gelecek her analizde bu tavanı
hesaba kat — "Truck upgrade"in hangar 2/3'ü ne zaman açtığı artık salt bir "daha fazla tır" değil,
"geç-oyun gelir tavanını gerçekten yükselten" bir karar. STRICT/OPTIMISTIC ayrımı netleşene kadar
(playtest) iki sınırı da rapor et, tek sayı verme. Detay ve tam formüller: `tools/economy-sim/sim.js`
(`truckCapStrict`, `truckCapOptimistic`, `OVERHEAD_TOTAL`).

İlişkili: [[quota_throughput_calibration]] (benzer "playtest olmadan aktive/kalibre etme" deseni),
[[prestige_cap_bug_and_fix]] (aynı turda prestij pacing'i de metodoloji nedeniyle değişti)
