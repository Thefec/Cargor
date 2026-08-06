---
name: prestige-function-surface
description: Prestijin oyundaki TEK ekonomik islevi kutu-basi odul tier'i (+ 0'a dusunce olum). Musteri kapasitesi zinciri ve iki event OLU/dekoratif; kazanma kosulu prestije BAKMIYOR.
metadata:
  type: project
---

**2026-07-30 grep ile tam tarandi (FAZ2).** Prestij dengelemeden ONCE bunu bil:

## GERCEK islevler (yalniz 2 tane)
1. **Kutu basi odul tier'i** — `Truck.cs:610-645`:
   `odul = rewardPerBox + floor(prestij / prestigePerBonus) * bonusPerTier`.
   **Tek ekonomik islev.** => Prestij birikimini artirmak DOGRUDAN gelir enflasyonu demek;
   kira egimiyle catisir. Bu yuzden birikim ve tier esigi BIRLIKTE ele alinmali.
2. **Olum kapisi** — `PrestigeManager.cs:154-157`: clamp ONCESI `prestij <= 0` -> `TriggerLose()`.

## OLU / dekoratif olanlar
- `GetCustomerCapacity()` / `currentCustomerCapacity` (`PrestigeManager.cs:108-124, 199-202`):
  **sifir dis tuketici**. `OnCustomerCapacityChanged` event'inin **sifir abonesi** (`cs:12, 103`).
  Tek kullanim bir UI metni `"Capacity: N"` (`cs:181-187`).
  => `prestigePerCustomer=4`, `baseCustomerCapacity=1`, `maxCustomerCapacity=20` DEKORATIF.
  Bir analizde "prestij musteri kapasitesini aciyor" DEME, acmiyor.
- `OnPrestigeChanged` (`cs:11, 91`): sifir abone.
- **Kazanma kosulu prestije BAKMIYOR**: `GameStateManager.cs:645-659 CheckWinCondition` yalniz
  `currentDay >= MAX_DAYS` kontrol ediyor. Doc yorumu "with prestige > 0" diyor ama KOD BAKMIYOR.
  Prestij yalniz kosu ORTASINDA 0'a duserse olduruyor.

## Prestiji yazan 7 mesru cagiran
`CustomerAI.cs:867` (servis +) / `:882` (yanlis urun -), `GameStateManager.cs:627` (kacan musteri -),
`Truck.cs:603` (yanlis renk teslim -), `BoxFallPenalty.cs:167` (kutu dusme -),
`PhoneCallManager.cs:336` (telefon +), `QuestManager.cs:835` (quest +/-),
`DayCycleManager.cs:554` (Acil Fren perki -).

## Perklerde HARDCODED prestij tabanlari (taban degisirse senkron sart)
- `PerkEffect.cs:92` Prestij Ustasi: `customerServedPrestigeBonus = 0.2f + 0.06f*level`
- `PerkEffect.cs:165` Kaldiracli Kira: `customerLostPrestigePenalty = -1.2f`
- `PerkEffect.cs:84` Prestij Simsari: `Truck.bonusPerTier = 5f + 0.5f*level`

**How to apply:** "Prestij birikimini yukseltelim" onerisi yapmadan once bunun kutu-basi odulu
(dolayisiyla geliri) sisirdigini hesapla. Gelir-notr kalmak icin `prestigePerBonus`'u ayni
carpanla buyut (bkz. [[faz2_prestige_rent_event_2026-07-30]] cift-carpan cozumu).

Iliskili: [[faz2_prestige_rent_event_2026-07-30]], [[serial_customer_service_ceiling]],
[[money_comes_only_from_trucks]], [[prestige_100_rescale_2026-07-20]]
