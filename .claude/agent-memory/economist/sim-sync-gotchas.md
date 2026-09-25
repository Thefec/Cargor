---
name: sim-sync-gotchas
description: sim.py ile canlı Cargor kodu arasındaki bilinen senkron farkları ve doğrulama noktaları (event takvimi kayması, event etkilerinin canlıda bağlı olmaması, Acil Fren davranışı)
metadata:
  type: project
---

sim.py (tools/economy_sim) canlıyla büyük ölçüde senkron (2026-09-24 doğrulandı: kira 290/650/1140/1630 ×1.2^n, başlangıç 500×1.2^(P-1), grace %80 tek sefer, Ö-A kilit 1.0 + Acil Fren muaf, Ö-C grace iptali, kutu ödülü [50,55,70,88]). Bilinen farklar:

- **Event takvimi 1 gün kayık**: canlı `EventCalendarUI.GenerateInitialEvents` currentDay = baseDay(1)+3 = 4'ten başlar, +1..2 → ilk event gün 5/6. sim `events.freeDays=3` → ilk aday 4 (kira, atla)/5. Canlı eşdeğeri `events.freeDays=4`. Etkisi 2026-09-24 ölçümünde küçük (bkz. [[mc40k-2026-09-24-findings]]).
- **Event etkileri sim'de hepsi çalışıyor varsayılıyor**; canlıda `IsGoldenBoxDay`/`IsVIPServiceDay` okuyucusuz (A2 denetimi). Sim event etkisini üst sınır olarak ölçer.
- **Acil Fren kirayı tamamen affeder** (sim + canlı DayCycleManager aynı): tek kullanımlık, kira ödenmez, rent_cycle ilerler, -2 prestij. Zayıf takımın hayatta kalması büyük ölçüde bunu almasına bağlı.
- dt=1 yuvarlama tuzağı: ince kart varyantlarında dt=0.2 ile teyit et.
- **Teklif gün-tohumlu** (canlı UpgradePanel.cs:1343 ve sim.py shop aynı): her koşu aynı gün aynı teklif → kart alım oranları teklif takvimini ölçer. Kart ölçümünde event_kart harness'ının `offerRand`'ını kullan.
- **Gün 1'de OnNewDay yok** (DayCycleManager.cs:1002): canlıda gün-1 event'i aktive olmaz; sim ise gün 1 event'ini uygular.
- sim.py spawn_customer dict-literal RNG sırası (sabır → renkler): kopya/override yazarken sırayı koru, yoksa bit-birebir bozulur.

**Why:** Sonuçları canlıya genellerken bu farklar yanlış yorum doğurur.
**How to apply:** Yeni bir ölçümde önce bu 3 noktayı grep ile yeniden doğrula (dosyalar değişmiş olabilir).
