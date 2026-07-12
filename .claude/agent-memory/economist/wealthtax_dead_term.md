---
name: wealthtax-dead-term
description: Kira formulundeki wealthTax terimi hep 0 - UpgradeManager.Buy() hicbir yerden cagrilmiyor, gercek satin alma UpgradePanel uzerinden yapiliyor
metadata:
  type: project
---

2026-07-12 analizi: `GameEconomySettings.CalculateRent()` (satır 118-124) formülü `scaledRent + totalUpgradeValue*wealthTaxRate(0.1)`. `totalUpgradeValue`, `DayCycleManager.GetTotalUpgradeValue()` üzerinden `UpgradeManager.Instance.IsPurchased(t)`'e bakıyor. `UpgradeManager.Buy()` (`Assets/NewCss/UpgradeScripts/UpgradeManager.cs:34`) kod tabanında hiçbir yerden çağrılmıyor (grep doğrulandı) — canlı satın alma akışı `UpgradePanel.cs`, doğrudan `MoneySystem.SpendMoney()` çağırıyor, `UpgradeManager`'a hiç dokunmuyor. Sonuç: `wealthTax` her zaman 0, kira tasarımdan sistematik düşük. Bu [[upgrade_dual_system]] kaydındaki "iki paralel upgrade sistemi" tespitinin kira tarafındaki somut sonucu.

Kaba etki hesabı (roguelite toplam olası harcama ≈9945 TL, bkz. [[roguelite_perk_pricing]]; `rentGrowthMultiplier` zaten 1.15'e düzeltilmiş, bkz. [[rent_death_spiral]]): wealthTax tam aktif olsaydı 16 gün toplam kira 1P için 2497→5779 TL (+131%), 2P 4494→7776 TL (+73%), 4P 7490→10772 TL (+44%). 1P'deki iki kata katlanma, rent_death_spiral'ın önceden düzelttiği dengeyi yeniden bozma riski taşıyor.

**Why:** Kullanıcıya sunulan karar memosunda net öneri: wealthTax terimini kira formülünden tamamen kaldır (zaten hiç çalışmıyor, kimse bu davranışa alışık değil, kaldırmak sıfır regresyon riski). `Buy()`'ı gerçek akışa bağlamak istenirse `wealthTaxRate` 0.1'den ~0.02-0.03'e düşürülmeli ve tam 16-gün simülasyonla (özellikle 1P) doğrulanmalı — bu ayrı bir iş.

**How to apply:** Kira formülüyle ilgili gelecekteki her analizde wealthTax teriminin o an gerçekten aktif olup olmadığını (yani `UpgradeManager.Buy()`'ın çağrılıp çağrılmadığını) tekrar doğrula — durum değişmiş olabilir. Karar kullanıcıdan onay bekliyor, henüz uygulanmadı.
