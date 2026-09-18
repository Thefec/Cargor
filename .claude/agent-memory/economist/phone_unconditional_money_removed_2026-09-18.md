---
name: phone_unconditional_money_removed_2026-09-18
description: PhoneCallManager ExecuteCall'daki koşulsuz +20 TL kaldırıldı (callMoneyReward 20->0); prestij+forced-spawn kaldı
metadata:
  type: project
---

Karar (2026-09-18, denetim `docs/playtest/tasarim-denetimi-2026-09-18.md` #2 / `denetim-2026-09-18/A3-mechanics.md` §3):
`PhoneCallManager.ExecuteCall` müşteri servis edilsin edilmesin `AddMoney(20)` yapıyordu — "para
yalnız tırdan gelir" invariant'ını (bkz. [[cargor-economy-status]]) bozan ikinci koşulsuz musluk.
Round 10'da 3 farklı küçültülmüş değer (10/12/15) denenmiş ama Slow/strict P1/P2'de optimal
telefon oranını %100'den kurtaramamıştı.

**Seçilen çözüm = seçenek (a)**: `callMoneyReward: 20 → 0`. Prestij ödülü (+0.4) ve zorunlu
müşteri spawn'ı DOKUNULMADI. Gerekçe: (b) yeni bir "servis edildiyse öde" gameplay hook'u
gerektiriyordu (kapsam dışı bırakıldı); (c) daha önce üç değerde denenmiş ve başarısız olmuştu.
(a) hem invariant'ı geri getiriyor hem sıfır yeni mekanik gerektiriyor.

**Sim sonucu (`runFullSim`, argmax phoneUseRate 0-100% adım 5%, 20 hücre × 2 callMoneyReward)**:

| Bant | P | opt%(20 TL) → kasa | opt%(0 TL) → kasa |
|---|---|---|---|
| Normal/strict | 1 | 15%→2127 | 10%→1924 |
| Normal/strict | 2 | 25%→4484 | 15%→3944 |
| Normal/strict | 3 | 20%→5710 | 15%→5203 |
| Normal/strict | 4 | 15%→6832 | 15%→6352 |
| Normal/optimistic | 1-4 | 0-30%→4773-11074 | aynı bant, 0-25%→4328-11074 (çoğu değişmedi) |
| **Slow/strict** | **1** | **100%→957** | **0%→228** |
| **Slow/strict** | **2** | **100%→1557** | **20%→240** |
| Slow/strict | 3-4 | 20-35%→433-578 | 0-15%→394-560 |
| Slow/optimistic | 1-4 | 0-35%→2205-4737 | çoğu 0%, küçük düşüş |

Kilit sonuç: eski "spam" optimumu (Slow/strict P1/P2, %100) tamamen kırıldı → %0/%20. Sağlıklı
bantlarda (Normal/strict) optimum %10-15'e indi (önceden %15-25), hâlâ gerçek bir karar — spam
değil. Hiçbir hücrede yeni iflas çıkmadı (varsayılan ASSUMED4 kullanım oranında da doğrulandı).

**Açık kalan (bilerek, ayrı iş)**: Slow/strict P1/P2'nin düşük mutlak kasa değeri (176-240 TL gün
16) telefon fix'inin yan etkisi DEĞİL — önceden telefon parası bu zayıf bandı gizliyordu
([[economy_full_balance_round2]]: "Slow/strict duvarı EĞRİ değil SEVİYE"). Bu konu bu işin
kapsamı dışında, dokunulmadı.

Uygulanan dosyalar: `Assets/NewCss/GameEconomySettings.cs:129` (default 20→0, tooltip güncellendi),
`Assets/Resources/EkonomiAyarlari.asset:32` (callMoneyReward: 20→0), `Assets/NewCss/Phone/
PhoneCallManager.cs` (fallback default 20→0), `tools/economy-sim/sim.js:547` (SRC4.callMoneyReward
20→0), `GDD.md §14.4` (3 blok güncellendi) ve §16.1 civarındaki eski "kabul edilmiş açık" uyarı
kutusu KAPANDI olarak işaretlendi.

İlgili: [[cargor-economy-status]], [[money_comes_only_from_trucks]] (bu memory artık kısmen
DÜZELDİ — telefon artık koşulsuz para vermiyor, ama hâlâ prestij+forced-spawn veriyor).
