---
name: perk-card-absolute-assignment-conflict
description: PerkEffect.cs TÜM perkleri idempotent MUTLAK atama ile uyguluyor (+= /*= YASAK) — gelecekte aynı alanlara yazan HERHANGİ bir yeni sistem (kalıcı kartlar dahil) perk'i sessizce SİLEBİLİR, biriktiremez
metadata:
  type: project
---

**Kod kanıtı** (`Assets/NewCss/UpgradeScripts/PerkEffect.cs:30-38` yorum, kod-doğrulandı 2026-08-12):
"Her Apply* metodu IDEMPOTENT olmalıdır: level'dan MUTLAK bir hedef değer hesaplar, mevcut alan
değerine += / *= yapmaz." Sebep: `UpgradePanel.HandleUpgradeLevelsChanged` (NetworkList.OnListChanged)
HERHANGİ bir oyuncunun upgrade seviyesi değiştiğinde TÜM client'larda yeniden tetikleniyor — perk her
seferinde "taban + level*artış" formülüyle alanı YENİDEN YAZIYOR, üstüne eklemiyor.

**Somut örnekler:**
- `agile_crew` → `PlayerMovement.moveSpeed = 5f * 1.15f` (mutlak atama, 5.75)
- `energetic_crew` → `PlayerMovement.staminaRegenRate = 1f + 1.5f` (mutlak, 2.5)
- `prestige_master` → `Economy.customerServedPrestigeBonus = 0.4f + 0.12f*level` (mutlak)
- `gambler_case`/`all_in` → `Truck.rewardPerBox = Economy.rewardPerBox * 1.30f` (mutlak, Economy'nin
  SABİT tabanından her seferinde yeniden hesaplanıyor — yorum: "idempotent kalması için her zaman
  Economy'nin (sabit) baz değerinden yeniden hesaplanır")
- `phone_line` → `Economy.phoneRingPerkBonus = 0.15f` (mutlak)
- `Overtime` (Mesai Saati) → `DayCycle.SetOvertimeMultiplier(1.125f)` (idempotent çarpan, ayrı metod)

**Risk:** Herhangi bir GELECEK sistem (örn. Kalıcı Kart Sistemi, `plans/kalici-kartlar.md`) AYNI
alanlara (moveSpeed, staminaRegenRate, customerServedPrestigeBonus, rewardPerBox/penaltyPerBox,
phoneRingPerkBonus, rentGrowthMultiplier/rentScaledMultiplier) benzer mutlak-atama deseniyle yazarsa,
perk ile yeni sistem BİRİKMEZ — hangisi SON çalışırsa o kazanır, diğeri sessizce silinir. Bu, "perk +
kart ikisi de aktifken güç ne kadar" sorusuna kod okumadan cevap verilemeyeceği anlamına gelir.

**Bilinen çakışma noktaları (Kalıcı Kart Sistemi taslağı için, `plans/kalici-kartlar.md` 14 kart
bazında):**
- `#2`/`#19` (moveSpeed, staminaRegenRate) ↔ `agile_crew`/`energetic_crew`
- `#5`/`#9` (customerServedPrestigeBonus) ↔ `prestige_master`
- `#1`/`#6`/`#11`/`#15`/`#20` (rewardPerBox/penaltyPerBox) ↔ `gambler_case`/`all_in`
- `#13` (telefon çalma ihtimali) ↔ `phone_line` (phoneRingPerkBonus benzeri alan)
- `#11` (gün süresi +%10) ↔ "Mesai Saati"/Overtime perk (+%12.5) — AYNI LEVER'a iki farklı isimle
  dokunuyor, muhtemelen tasarım kopyası da (yalnızca ekonomik değer değil, konsept çakışması)

**Why:** Kullanıcı özellikle #16/#19 için "perk + kart aynı anda alınırsa değerler makul kalıyor mu"
diye sordu; kod incelemesi gösterdi ki asıl risk "değerler TOPLANINCA aşırı güçlenir mi" değil,
"biri diğerini SESSİZCE SİLER Mİ" — çok daha ciddi bir mühendislik tuzağı, sadece bu 2 karta değil
tüm alan-paylaşan kart/perk çiftlerine uygulanıyor.

**How to apply:** Kod yazılırken (gameplay departmanı) kartlar `GameEconomySettings`/`PlayerMovement`
alanlarına DOĞRUDAN mutlak atama YAPMAMALI. Ya (a) perk'lerin zaten kullandığı `GetX()` getter deseni
(örn. `GetHangarStayDuration`, `CalculateRent`) genişletilip kart-çarpanı AYRI bir alanda tutulup
perk-çarpanıyla çarpımsal/toplamsal birleştirilmeli, ya da (b) kartlar da perk sistemiyle AYNI
"ctx.Economy sabit tabanından yeniden hesapla" idempotent deseni izlemeli AMA iki sistemin birbirinin
üstüne YAZMADIĞI, ikisinin de OKUYUP BİRLEŞTİRDİĞİ ayrı state alanları olmalı. Bu not ileride kod
yazılırken qa/gameplay'e mutlaka aktarılmalı — economist bu implementasyonu YAPMAZ, sadece riski işaretler.

İlişkili: [[permanent_cards_value_review_2026-08-12]], [[perk-mutates-persistent-assets]] (kullanıcı
hafızasında, farklı ama akraba bir perk-mutasyon bulgusu)
