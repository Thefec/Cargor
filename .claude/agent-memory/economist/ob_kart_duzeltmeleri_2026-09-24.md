---
name: ob-kart-duzeltmeleri-2026-09-24
description: Ö-B 2026-09-24 — Hızlı Hangar/Mesai Saati/Prestij Ustası kök nedenleri (null-ikiz kontrolüyle) ve seçilen efekt spec'i; Ö-A+Ö-C açıkken ölçüldü
metadata:
  type: project
---

Kullanıcı kararı (2026-09-24): 3 zararlı kart KALDIRILMAYACAK, efekt düzeltilecek; fiyat sabit. Rapor `docs/economy/ob-kart-duzeltmeleri-2026-09-24.md`, betik `tools/economy_sim/ob_2026_09_24.py`, çıktılar `results_2026-09-24/`.

**Kök nedenler (null ikiz = aynı fiyat efektsiz kart ile ayrıştırıldı):**
- Hızlı Hangar: gelir tırın hangar-saniyesiyle sınırlı (tırların sadece %21-45'i dolu kalkıyor); ölü süre sabit ~12 sn, kalış kısalınca yükleme penceresi oranı düşüyor → saf efekt 3P/4P orta −255/−671. GERÇEKTEN zararlı.
- Mesai Saati: orta/iyi günlerin %77-100'ünde kota erken bitiyor → gün hemen sona sarılıyor, uzayan saat kullanılmıyor → saf efekt ≈0. ÖLÜ, zararlı değil; zayıf Δkayıp tamamen fiyattan.
- Prestij Ustası: efekt pozitif ama fiyatın çok altında (⌊prestij/8⌋×5 dönüşümü çok zayıf, 4P iyi tavana yakın).

**Seçilen spec:** Hızlı Hangar kalış ×0.9 + respawn 0 + exitDelay 0 (yeni çarpan alanları, Truck.exitDelay'e yazma: event snapshot'ı ezer); Mesai 1.125 aynı + erken-bitiş kapanış payı +15 sn; Prestij Ustası adım 0.12→0.35.

**Why:** Grace +30 ve üstü ile kota +1 Ek Hangar düzeyi çıktı; PM 0.4+ 2P'de Kumarbaz'ı geçti. exitDelay'e dokunmayan Hızlı Hangar varyantı yetmedi.

**Sim senkronu (2026-09-24, müdür isteği):** `config.json` + `sim.py` artık Ö-A (upgradeRentReserveFraction 1.0, rentReserveExempt) + Ö-B (exit alt sınırı 0.4 dahil) + Ö-C varsayılan. Ö-B/Ö-A kapatılınca eski sim ile 540/540 koşu bit-birebir. Eski canlıyı üretmek: `ob_2026_09_24.PRE_OB_LIVE_PE` + fraction 0 + graceZeroPctLive True. TUZAK: sim dt=1 sn → 0.4 sn exit 1 sn'ye yuvarlanıyor; alt-saniye mekanikleri dt=0.2 ile ölç.

**How to apply:** Mesai 4P'de güçlü (orta +502) — baskın hissettirirse ilk ayar grace +10. Ö-A kazanımları Ö-B sonrası ±1 pp korundu. İlgili: [[sifirdan-upgrade-ucuz-mu-2026-09-23]]. Yeni kart değerlendirmesinde null-ikiz kontrolü kullan: 1P zayıfta HER alım fiyattan dolayı +9..+28 pp kayıp gösteriyor, bunu efekte yükleme.
