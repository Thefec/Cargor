---
name: permanent-cards-value-review-2026-08-12
description: Kalıcı Kart Sistemi (kira-sonrası draft, plans/kalici-kartlar.md) 14 kartın değer onayı — sim.js doğrulamalı, 3 kart değer değişikliği önerisi (6, 11, 20), 1 kart güçlendirme önerisi (16)
metadata:
  type: project
---

**Kaynak:** `plans/kalici-kartlar.md` (14 onaylı kart, mekanik: kira günü 4/8/12 sonrası ertesi gün 2 kart,
oybirliği, kalıcı — event'lerin aksine gün başında geri alınmaz). Bu tur sadece DEĞER onayı, kod yazılmadı.

## Değişiklik önerilen kartlar

**#6 Basit Muhasebe (Hafif)** — reward penalty **-%5 → -%2**. `boxDropMoneyPenalty`=5 TL/düşürme (zaten
trivial bir sürtünme). -%5 kalıcı ödül vergisi gün5-16 boyunca toplamda 229-762 TL'ye mal oluyor
(sim, `tirGeliri` toplamı × 0.05, P1-P4 optimistic); bunu karşılamak için 46-152 kutu düşürmek gerekir
(11 gün boyunca günde 4-14 düşürme — gerçekçi değil). -%2'ye çekilince maliyet 92-305 TL'ye iner, 10-30
düşürme/11gün (günde ~1-3) ile daha gerçekçi dengeleniyor.

**#11 Fazla Mesai (Orta)** — reward penalty **-%15 → -%6**. sim.js `truckThroughput` ile doğrulandı:
gün süresi +%10 → boxesPerDay TAM +%10 lineer artıyor (trüc üretimi bu rejimde kapasite-sınırlı DEĞİL,
tüm P/mode kombinasyonlarında birebir +10.0%). Yani net gelir çarpanı = 1.10 × (1-ceza). Mevcut -%15 ile
net çarpan **0.935 — yani kart SEÇİLDİĞİNDE gelir GARANTİLİ %6.5 DÜŞÜYOR**, "daha uzun gün = daha fazla
üretim" vaadi yanıltıcı. -%6'ya çekilince net çarpan 1.034 (hafif pozitif) — kartın "iyi yön" vaadiyle
tutarlı hale geliyor.

**#20 Şöhretin Bedeli (Ağır)** — "+%20 günlük müşteri talebi" **kaldırılmalı veya küçültülmeli**,
prestij cezası **+%50 → +%25-30**. sim.js `customerThroughput` doğrulaması: gün13-16'da `serviceSlots`
(servis kapasitesi) zaten talebin altında bağlayıcı kısıt (2P gün16: serviceSlots=15, talep=16) —
talep +%20 artınca `served` (gerçekten servis edilen, = tek prestij/gelir kaynağı) **HİÇ DEĞİŞMİYOR**
(15→15, hem optimistic hem strict), yalnızca `lost`/`skipped` artıyor. Yani "+%20 talep" hiçbir
prestij/gelir faydası getirmiyor (müşteri zaten SIFIR TL veriyor — bkz [[money_comes_only_from_trucks]]),
sadece bu kartın KENDİ +%50 cezalandırdığı "kaçan müşteri" havuzunu büyütüyor — kart kendi kendini
yiyor. 2P günlük ekstra prestij kaybı: sim-modeli okumada +0.3/-0.5, "kötü senaryo" okumada +1.6/-2.4
(bkz hesap detayları altta). Önerilen düzeltme: talep+%20'yi sil, kutu başı ödül +%15'i tek iyi yön
yap, ceza +%50→+%25-30'a çek (artık tek gerçek kötü yön olduğu için orantılı küçült).

## Değer OLDUĞU GİBİ KALSIN + not düşülen kartlar

- **#1 Toptan Anlaşma, #2 Sadık Ekip, #4 Erken Kalkanlar, #5 Kişisel Dokunuş, #9 Sıkı Sözleşme, #14
  Hızlı Sevkiyat Sözleşmesi, #15 Riskli Yatırım, #19 Usta İşçilik** — değerler OLDUĞU GİBİ kalsın.
- **#7 İkinci El Ekipman** — -%10 upgrade maliyeti KALSIN (kod-doğrulandı: `UpgradePanel.GetCostMultiplier()`
  zaten event×P çarpanlarını çarpımsal birleştiriyor, kartın 3. çarpan olarak eklenmesi güvenli, perk
  çakışması yok). Kötü yön (`wrongProductPrestigePenalty` +%20, -0.08→-0.096) neredeyse kozmetik kaldı;
  isteğe bağlı +%40'a çekilebilir (-0.08→-0.112, yine de küçük) ama zorunlu değil — Hafif tier için kabul
  edilebilir hafif pozitif skew.
- **#15 Riskli Yatırım** — +%25 sürşarj AŞIRI DEĞİL. `DayCycleManager._rentPaymentCount` kod-doğrulaması:
  gün13 (Ağır havuz) seçildiğinde kalan TEK kira ödemesi gün16'dır (rentIntervalDays=4, oyun gün16'da
  bitiyor) — yani sürşarj `rentGrowthMultiplier`in compounding'iyle ÇARPIŞMIYOR, tek seferlik ek yük.
  sim.js `runSim` ile test edildi: P1-P4, optimistic+strict, tüm senaryolarda gün16 kirası sürşarjlı
  haliyle bile ÖDENEBİLİYOR (cashBeforeRent >= rentAmount×1.25), en dar marj P2-strict'te sürşarj sonrası
  kasada 1711 TL kalıyor (iflas yok). P1-strict zaten kartsız da gün3'te iflas ediyor (baseline sorunu,
  karttan bağımsız).
- **#16 Tükenmişlik Eşiği** — GÜÇLENDİRME öner: max stamina (`sprintDuration`) +%25 = 3sn tabanda yalnız
  +0.75sn (neredeyse hissedilmiyor), buna karşı kötü yön (`exhaustedSpeed` %20 daha sert kesinti, 3→2.4,
  yani normale oranla -%40'tan -%52'ye) gerçek ve hissedilir. Ağır tier'de bu asimetri ters yönde: zayıf
  ödül + belirgin ceza. Karşılaştırma: #19 Usta İşçilik +%15 hareket hızı (5→5.75) AYNI MAGNITUDE'da
  var olan `agile_crew` relic perkiyle BİREBİR eşleşiyor (180 TL'lik bedelsiz eşdeğeri) — #16 böyle güçlü
  bir referans noktasına sahip değil. Öneri: max stamina +%25 → +%40-50 (3→4.2-4.5sn) ki Ağır tier'e
  layık hissettirsin, ya da ceza şiddeti hafifletilsin.

## Genel değer prensibi (kalıcılık indirimi)

Tek-günlük event çarpanları ±%20-50 aralığında (örn. RUSH DAY dailyCustomerMultiplier=1.35,
FESTIVAL DAY kira×%10-20). Kalıcı kartlar bu event büyüklüğünü AŞMAMALI — 14 karttan hiçbiri tek
başına %50'yi aşmıyor (en yükseği #20'nin cezası, o da tek gerçek downside kalınca zaten küçülüyor).
Süre = risk çarpanı: Hafif (gün5, ~11 gün kalıyor) > Orta (gün9, ~7 gün) > Ağır (gün13, ~4 gün) —
bu yüzden #7 gibi Hafif kalıcı-yüzde kartları en uzun etki penceresine sahip, küçük yüzdeler bile
büyük mutlak TL'ye dönüşebiliyor; değerlendirirken süreyi her zaman hesaba kat.

İlişkili: [[perk_card_absolute_assignment_conflict]], [[money_comes_only_from_trucks]],
[[serial_customer_service_ceiling]], [[faz4_final_value_set_2026-07-30]]
