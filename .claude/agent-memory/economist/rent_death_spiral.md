---
name: rent-death-spiral
description: 1P/2P bankruptcy kök nedeni rentGrowthMultiplier (startingMoney değil); ÇÖZÜLDÜ 1.3→1.15 uygulandı. NOT: wealthTax aslında inert'ti (kırık kablolama), asıl kaldıraç yalnız rentGrowth.
metadata:
  type: project
---

> ⚠️ **Düzeltme (2026-07-13 denetimi):** Bu kayıt ölüm-sarmalını "rentGrowth + wealthTax bileşimi" diye anlatıyor, ama sonraki analiz wealthTax'in **fiilen hep 0 / etkisiz** olduğunu (kırık kablolama, [[wealthtax_broken_wiring]]) buldu — yani asıl (ve tek) kaldıraç `rentGrowthMultiplier`'dı. ✅ **ÇÖZÜLDÜ:** `rentGrowthMultiplier` 1.3→1.15 uygulandı (Faz-1), 16-gün sim 1P/2P/4P sağlıklı ([[money_config_conflict]], plans/economy-audit-2026-07-13.md). wealthTax terimi de sonradan tamamen kaldırıldı.

2026-07-07 tarihli tam analiz (bkz. proje kökünde `ECONOMY_BALANCE_REPORT.md`, artık silinmiş/güncellenmiş olabilir — güncel değerler için `Assets/Resources/EkonomiAyarlari.asset` ve `Assets/NewCss/GameEconomySettings.cs`'i kontrol et):

`GameEconomySettings.RunSimulation()` portlanarak Node.js'te 16 günlük 1P/2P/4P simülasyonu koşuldu. Bulgu: `startingMoney` değerini 100'den 1500'e kadar hiçbir seviyede değiştirmek 1P ve 2P'nin gün 8-12'de iflasını önlemiyor. Kök neden: `rentGrowthMultiplier=1.3` (dönem başına %30 bileşik kira artışı) + `wealthTaxRate=0.10` (harcanan her upgrade'in kalıcı ek kira yükü olması) birlikte, gelir tavanını (expectedCustomers formülü, max 50) aşan bir büyüme oranı yaratıyor.

**Why:** Bu proje için ekonomi dengesi önerileri verirken "startingMoney'i artır" gibi yüzeysel çözümler yeterli değil — testte kanıtlandı ki bu parametre izole değiştirilirse sorunu çözmüyor.

**How to apply:** Kira/iflas dengesizliği görülen her analizde önce `rentGrowthMultiplier` ve `wealthTaxRate`'in bileşik etkisini 16 günlük tam simülasyonla test et, sadece başlangıç parasına bakma. Önerilen düzeltme (test edilip doğrulandı): `rentGrowthMultiplier` 1.3→1.15 tüm oyuncu sayılarını (1P/2P/4P) 16 gün boyunca sağlıklı kasa bakiyesiyle hayatta tutuyor, startingMoney=500 ile bile. Ayrıca `DifficultyManager.cs`'deki `moneyMultiplierPerPlayer=0.85` (oyuncu başına başlangıç parasını AZALTAN çarpan) ile `baseRentByPlayerCount` (oyuncu arttıkça kirayı ARTTIRAN dizi) aynı anda var — bu çifte ceza, co-op gruplarını solo oyunculardan orantısız zorluyor. İlgili: [[env_no_python]]
