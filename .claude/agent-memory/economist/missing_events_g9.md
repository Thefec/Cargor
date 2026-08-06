---
name: missing-events-g9
description: 7 takvim event'i (BUSY/RAINY/MARKETING/QUOTA/SURPRISE AUDIT/CUSTOMER SUPPORT/FESTIVAL DAY) hic mekanik etki uretmiyor - G9 kalibrasyon degerleri
metadata:
  type: project
---

## ✅ TAM KAPANDI (2026-07-30 dogrulandi) -- FESTIVAL DAY dahil
Kalan tek bosluk olan **FESTIVAL DAY artik sabit TL DEGIL**: `EventEffectManager.cs:404-408`
`Random.Range(currentRent * 0.10f, currentRent * 0.20f)` kullaniyor (kirayla olcekleniyor).
`festivalBonusMin/Max` (100/300) yalnizca `DayCycleManager.Instance == null` fallback'i
(`cs:411-415`). Onceki "P2 acik" notu GECERSIZ.

Canli event havuzu **16** (`EventCalendarUI.cs:160-177`) ve `EventEffectManager.eventMultipliers`
ile TAM eslesiyor -- isim uyusmazligi kalmadi.

**YENI 2026-07-30 tutarsizliklari (FAZ 2 isi):**
- `RAINY DAY` -> `EventType.Positive` (`EventCalendarUI.cs:174`) ama etkisi musteri x0.8
  (`EventEffectManager.cs:294`) = prestij geliri -%20. Pozitif DEGIL.
- `RELAXED DAY` aciklamasi yalniz "sabir +%30" diyor ama kodda ayrica
  `dailyCustomerMultiplier = 0.7` var (`EventEffectManager.cs:182`) = oyuncuya SOYLENMEYEN
  %30 musteri kesintisi.
- Event sikligi: gun 4'ten baslar, aralik `rng.Next(1,4)` (1-3 gun), kira gunleri atlanir
  (`cs:688`) -> **~5 event / 16 gun**. Ilk 2 garanti pozitif, 3. garanti negatif (`cs:701-714`).

---
TARIHSEL: 2026-07-12 analizi. `EventCalendarUI._allEvents` (17 event) ile `EventEffectManager.eventNames`/`eventMultipliers` (11 event) arasında isim eşleşmesi yok, 7 event takvimde vaat ediliyor ama uygulanmıyor. Ayrıca `INTENSIVE DAY` (eventMultipliers'da var, dailyCustomerMultiplier=1.5) takvimde hiç yok — ölü kod, gerçek karşılığı `BUSY DAY`.

Ekonomi taban değerleri (doğrulandı): `rewardPerBox=50`, `penaltyPerBox=40`, `baseRentByPlayerCount={500,900,1200,1500}`, `rentGrowthMultiplier=1.15` (zaten [[rent_death_spiral]] önerisiyle düzeltilmiş durumda, uygulanmış). Mevcut 11 event'in çarpan skalası: müşteri 0.7-1.5, ödül 1.0-1.3, hız/bekleme 0.6-1.3 — hiçbiri rewardPerBoxMultiplier'ı 1.0 altına düşürmüyor.

Verilen kalibrasyon önerileri:
- BUSY DAY: dailyCustomerMultiplier=1.3 (INTENSIVE DAY kaydını 1.5→1.3 yeniden adlandır)
- RAINY DAY: dailyCustomerMultiplier=0.8 (FATIGUE PROBLEM ile aynı skala)
- MARKETING DAY: dailyCustomerMultiplier=1.2 + rewardPerBoxMultiplier=0.7 → net günlük gelir -%16, ilk kez reward çarpanı 1.0 altına iniyor
- QUOTA DAY: Neutral kalmalı, çarpan verilmemeli — sadece kutu renk paterni; QA'ya "ortalama teslim süresi normal günle aynı mı" testi önerildi (gizli buff riski)
- SURPRISE AUDIT: penaltyPerBoxMultiplier=1.5 önerildi (metindeki "2x" yerine) — 2x, reward/penalty oranını 1.25'ten 0.63'e düşürüyor, mevcut skalanın (%30-50 sapma) çok üstünde bir sıçrama. Metin ile kod arasında zaten emsal var (RELAXED DAY: metin +%10, kod +%30) — "double" abartı olarak görülüp 1.5x uygulanabilir.
- CUSTOMER SUPPORT: postCallCooldown x0.7 (30s→21s), maxCallsPerHour sabit kalsın. `PhoneCallManager.SetCallChance(float)` satır 543 tam bu iş için boş bırakılmış stub — dolu değil, doldurulabilir.
- FESTIVAL DAY: gün başı tek seferlik RNG bonus = cari kira döngüsünün %10-20'si (sabit TL değil, kira yüzdesi — enflasyona otomatik ayak uydursun). 1P≈50-152 TL, 2P≈90-274 TL, 4P≈150-456 TL (gün bandına göre artan).

**Why:** Yeni event değerleri mevcut 11 event'in skalasından absürt sapmamalı; sabit TL yerine kira/reward oranına bağlı formüller gelecekteki rebalance'larda otomatik tutarlı kalır.

**How to apply:** Event ekonomi kalibrasyonu istenirse önce mevcut eventMultipliers skalasının min-max aralığını çıkar, yeni değeri o aralığa göre konumlandır. FESTIVAL DAY tipi "gün başı bonus" event'lerinde sabit sayı yerine `baseRentByPlayerCount × rentGrowthMultiplier^cycle × %` formülü kullan. İlgili: [[rent_death_spiral]], [[prestige_fragility]]
