# Economist Agent Memory Index

- [Python yok, Node.js kullan](env_no_python.md) — bu makinede python3/python/py PATH'te yok, hesaplamalar için node kullan
- [Kira ölüm sarmalı kök nedeni](rent_death_spiral.md) — ✅ÇÖZÜLDÜ: kök neden rentGrowthMultiplier (startingMoney değil), 1.3→1.15 uygulandı; wealthTax aslında inert'ti (asıl kaldıraç yalnız rentGrowth)
- [Başlangıç parası çelişkisi](money_config_conflict.md) — ÇÖZÜLDÜ (2026-07-12 doğrulandı): startingMoney=500, moneyMultiplierPerPlayer=1.0, rentGrowthMultiplier=1.15 artık senkron
- [Eksik event'ler G9 kalibrasyonu](missing_events_g9.md) — 7 takvim event'i mekanik etkisiz; BUSY/RAINY/MARKETING/QUOTA/AUDIT/SUPPORT/FESTIVAL için değer önerileri
- [Prestij kırılganlığı eşikleri](prestige_fragility.md) — startingPrestige=5 ile tek rush dalgasında 3 kayıp = game over; 15+(-1.5 ceza) öneriliyor
- [Upgrade çift sistem çakışması](upgrade_dual_system.md) — ✅ÇÖZÜLDÜ (479dbf0): Yol B (ItemType/UpgradeAssets/UpgradeManager) silindi, tek sistem UpgradePanel kaldı (tarihsel kayıt)
- [Upgrade fiyatlandırma çerçevesi](upgrade_pricing_framework.md) — 7 upgrade karakterine göre farklı payback hedefi (1.4-3.2 gün); v2'de gerçek maxLevel'e göre revize, bkz. UPGRADE_PRICING_REPORT.md v2
- [Roguelite perk fiyatlandırma v3.2 ONAY](roguelite_perk_pricing.md) — T3 gate gün≥9, Ucuz Kira tablo, Raf doğrusal, Prestij Simsarı fiyat+etki düzeltildi (18x→2.5x), toplam 9945 TL
- [FAZ2 kota-verim kalibrasyonu](quota_throughput_calibration.md) — C1 aktivasyonu playtest verisi olmadan ÖNERİLMEZ; ~4-6 kutu/dk eşiği, detay plans/economy-audit-2026-07-13.md
- [FAZ2 wealthTax kırık kablolama](wealthtax_broken_wiring.md) — ✅ÇÖZÜLDÜ (Seçenek A, 9d2c3b0): wealthTax terimi kira formülünden tamamen kaldırıldı + ölü Yol B silindi (479dbf0)
