---
name: wealthtax_broken_wiring
description: C5 wealthTax'in gerçek kök nedeni — sadece rate=inert değil, GetTotalUpgradeValue() ölü Yol B (UpgradeManager/ItemType) sistemini okuyor, gerçek satın almalar (UpgradePanel/Yol A) hiç sayılmıyor
metadata:
  type: project
---

`DayCycleManager.GetTotalUpgradeValue()` (satır ~631-644), `UpgradeManager.Instance.IsPurchased(ItemType)` + `UpgradeAssets.GetCost()` okuyor — bu "Yol B", tamamen orphan/dead sistem (bkz [[upgrade_dual_system]]). Gerçek satın alma akışı "Yol A": `UpgradePanel.PurchaseUpgradeServerRpc()` → `MoneySystem.SpendMoney()` + `_visualUpgradeLevels[...]++`. UpgradePanel.cs hiçbir yerde UpgradeManager/ItemType'a dokunmuyor (grep ile teyit, 2026-07-13).

Sonuç: `wealthTaxRate` değerini 0.1→0.03 gibi düşürmek HİÇBİR ŞEY DEĞİŞTİRMEZ — totalUpgradeValue gerçek oyunda her zaman 0, rate ne olursa olsun çarpım 0. Önceki teşhis ("rate=0.1 fakat hiç kullanılmıyor, düşürsek sıfır regresyon") EKSİKTİ — kök neden kırık kablolama, salt sayı ayarı değil.

**Why:** 2026-07-13 FAZ 2 denetimi. Kullanıcı/gameplay wealthTax'i canlandırmak isterse (Seçenek B) `UpgradePanel`'e yeni `TotalSpentTL` alanı eklenip `DayCycleManager.GetTotalUpgradeValue()` onu okuyacak şekilde YENİDEN yazılmalı — bu bir kod-değişikliği (yeni network-senkron alan), salt parametre değişikliği değil.

**How to apply:** wealthTax/kira/upgrade-vergisi ile ilgili gelecek her konuşmada önce hangi purchase yolunun (Yol A/UpgradePanel canonical, Yol B/UpgradeManager dead) kullanıldığını doğrula — varsayımla "rate'i düşür/kaldır" tavsiyesi verme. Sim verisi ve iki seçenek (A=kaldır, B=doğru bağla+0.03) için: `plans/economy-audit-2026-07-13.md` §3.
