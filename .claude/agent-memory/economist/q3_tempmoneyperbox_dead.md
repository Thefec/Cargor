---
name: q3-tempmoneyperbox-dead
description: TempMoneyPerBox buff tamamen ölü kod (ne quest ne Truck.cs tüketiyor); KARAR = şimdilik bırak, wiring için yedek sayı hazır
metadata:
  type: project
---

2026-07-19 kod turunda doğrulandı: `BuffType.TempMoneyPerBox` (RewardType.TempMoneyBoost=9) **iki yönden de ölü**:
1. Hiçbir canlı quest onu vermiyor — `Assets/Resources/Quests/easy1-5.asset` içindeki TÜM `rewardType`/`penaltyType` alanları sadece `0` (Money) veya `1` (Prestige). Havuzda 9/10/11 (TempMoneyBoost/TempSpeedBoost/PenaltyReduction) hiç yok.
2. Tüketim noktası da yok — `Assets/NewCss/TruckScripts/Truck.cs` `CalculateRewardWithPrestige()` (satır 605-615) sadece `rewardPerBox + prestigeBonus` okuyor, `BuffManager.GetBuffAmount(...)` hiç çağrılmıyor. `BuffManager.ApplyBuffEffect`/`RemoveBuffEffect` switch'lerinde de `TempMoneyPerBox` case'i YOK (satır 434-498) — sadece `BuffData.GetDescription()` string'i var. Tek canlı referans satır 682'deki `#if UNITY_EDITOR` debug context-menu metodu (`DebugAddTestTempBuff`), oyun akışının parçası değil.

**KARAR: (b) şimdilik ölü bırak, kaldırma da gerekmiyor.**

Gerekçe:
- Sıfır aktif risk/exploit — hiçbir canlı kod yolu buna dokunmuyor, silmemek de zarar vermiyor.
- Wiring iki dosyada eşzamanlı değişim ister (Truck.cs tüketim + yeni quest reward girdisi) → CLAUDE.md eşiğine göre bu BÜYÜK/RİSKLİ iş (ekonomik değer + kritik sistem), tam departman+kontrol akışı gerektirir, "küçük iş" değil.
- easy1-5 EV bandı (17.6-30 TL) zaten 2026-07-18'de kapatıldı ([[quest_reward_balance]] bkz. MEMORY.md); yeni ödül tipi eklemek bu turu yeniden açar.
- Diğer tüm canlı ödüller (Money/Prestige) oyuncu sayısından bağımsız SABİT (flat) EV taşıyor; TempMoneyPerBox ise kutu-başı olduğu için doğal olarak takım büyüklüğü/aktiviteyle ölçekleniyor (rewardPerBox'un kendisi de böyle — bu tutarsızlık değil, farklı bir ödül aroması, ama havuza karışınca EV karşılaştırması zorlaşır: 1P'de düşük, 4P'de çok daha yüksek getiri).

**Eğer ileride wiring'e karar verilirse (yedek sayı önerisi):**
Hedef: tek instance EV'si mevcut easy-quest bandına (~15-25 TL) yakın kalsın, süre kısa (Q8 stacking riskini de otomatik sınırlar).
- `amount = 1 TL/kutu`, `durationDays = 2`
- Hesap: 2P orta-oyun (gün 8, optimistic model) ~11.5 kutu/gün → 2 gün × 11.5 × 1 TL ≈ 23 TL EV (bkz [[quest_reward_balance]] bandıyla uyumlu)
- 1P'de ~11.4 TL, 4P'de ~46 TL EV (takım aktivitesiyle doğal ölçekleniyor, kabul edilebilir çünkü zaten üretkenliği ödüllendiren bir mekanik)
- Süreyi 2 günün üstüne çıkarma — bkz [[q8_buff_stacking_policy]] (uzun süreli temp buff + additive stack = enflasyon riski)
- Truck.cs `CalculateRewardWithPrestige()`'e `+ (BuffManager.Instance?.GetBuffAmount(BuffType.TempMoneyPerBox) ?? 0)` eklenmeli VE `ApplyBuffEffect`/`RemoveBuffEffect` switch'lerine case eklenmeli (şu an hiçbiri yok, sadece ekleme değil gerçek implementasyon gerekiyor).

Kaynak: `Assets/Scripts/Quest/Manager/QuestManager.cs:827` (mapping var), `Assets/Scripts/Quest/Buff/BuffManager.cs:434-498` (ApplyBuffEffect switch, case yok), `Assets/NewCss/TruckScripts/Truck.cs:605-615` (tüketim yok), `Assets/NewCss/GameEconomySettings.cs:42` (rewardPerBox=50 taban).
