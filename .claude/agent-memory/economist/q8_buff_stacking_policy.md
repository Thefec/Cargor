---
name: q8-buff-stacking-policy
description: BuffManager aynı-tip stack kuralı (amount+=, duration=Max) — şu an sıfır canlı etki, ama gelecekteki temp buff quest'leri için enflasyon riski + cap önerisi
metadata:
  type: project
---

`Assets/Scripts/Quest/Buff/BuffManager.cs` `AddBuffInternal()` (satır 348-366): aynı `BuffType` tekrar eklenince `existing.amount += buff.amount` (additive) ve geçici buff'larda `existing.remainingDays = Mathf.Max(existing.remainingDays, buff.remainingDays)` (süre uzamıyor, sadece en uzun olana resetleniyor).

**Şu anki canlı etki = SIFIR.** `Assets/Resources/Quests/easy1-5.asset` içindeki TÜM ödül/ceza girdileri sadece `rewardType 0` (Money) veya `1` (Prestige) — ikisi de `BuffManager`'a hiç uğramıyor (`ApplyRewardOrPenalty` içinde doğrudan `MoneySystem`/`PrestigeManager`'a gidiyor, bkz `QuestManager.cs:769-781`). Stack kodu şu an sadece gelecekteki (medium/hard tier gibi) buff-tipi ödünç için pasif duruyor. Ayrıca günde takım-çapında TEK quest kabul limiti var (`_hasAcceptedToday`, tek `NetworkVariable<bool>`, oyuncu başına değil) — bu da olası stack sıklığını zaten sınırlıyor.

**KARAR: Kuralı KORU (permanent buff'lar için), ama TEMPORARY buff'lara CAP ekle (ileride buff-tipi ödül eklenirse).**

Gerekçe / risk analizi:
- **Permanent buff'lar** (MaxStamina/MoveSpeed/WalkSpeed/StaminaRegenRate/DayDuration/MaxQueueSize/CustomerWaitTime/PenaltyReduction): `remainingDays<=0` olduğu için Max-duration dalı hiç çalışmıyor, sadece additive amount kalıyor. Günde-1-quest tavanı zaten büyümeyi ~+0.x/gün'e sınırlıyor — enflasyon riski yok, additive doğru davranış (kazanılmış kalıcı ilerleme hissi). **Değiştirme.**
- **Temporary buff'lar** (TempMoneyPerBox, TempSpeedBoost): additive-amount + duration-resetten-uzatma-yok kombinasyonu, eğer AYNI buff tipi ardışık günlerde (süre dolmadan) tekrar verilirse SINIRSIZ BÜYÜR — her yeni instance eskiyi süpürmez, üstüne ekler, süre her seferinde taze başa döner (asla decay olmadan). 16 günlük bir koşuda aynı temp-buff tipini veren quest'ler tekrar tekrar çıkarsa (örn. günde-1 kabul limitiyle bile, 3-4 günlük penceresi olan bir buff üst üste 3-4 kez yenilenirse) kutu-başı bonus teorik olarak sınırsız katlanır — bu klasik "duvar hissi yerine ters yönde patlayan enflasyon" riski.
- Öneri: temp buff stacking'e üst sınır koy — `existing.amount = Mathf.Min(existing.amount + buff.amount, buff.amount * MAX_STACK)` biçiminde, `MAX_STACK = 2` (yani aynı anda en fazla "2 instance değerinde" kutu-başı bonus birikebilsin, sonrası yeni instance'lar sadece süreyi tazeler amount'u değil). Alternatif daha basit kural: stack yerine REPLACE (yeni instance eskisinin üstüne yazar, toplanmaz) — eğer tasarım "quest çeşitliliği ödülü küçük bir double-dip hissi versin" istemiyorsa bu daha güvenli/basit.

Bu, [[q3_tempmoneyperbox_dead]] wiring önerisindeki kısa süre (2 gün) tavsiyesiyle birlikte okunmalı — kısa süre zaten stack pencere riskini daraltıyor.

Öncelik: DÜŞÜK — bugün hiçbir canlı quest bu path'e girmiyor, aciliyet yok. Medium/hard tier quest'lerine buff-tipi ödül eklenmeden önce bu cap uygulanmalı.

Kaynak: `Assets/Scripts/Quest/Buff/BuffManager.cs:348-366` (AddBuffInternal), `Assets/Scripts/Quest/Manager/QuestManager.cs:74,643` (_hasAcceptedToday tek NetworkVariable, takım-çapında).
