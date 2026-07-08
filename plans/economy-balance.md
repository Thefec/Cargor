# 💰 Ekonomi Denge Sprint'i

> **Durum**: Faz 1 kod+sahne ✅ (Unity teyidi bekliyor) · Faz 2 → roguelite'a soğuruldu · Bug'lar ✅
> **İlgili**: [../PLAN.md](../PLAN.md) · [roguelite-draft.md](roguelite-draft.md)
> **Raporlar**: `ECONOMY_BALANCE_REPORT.md`, `UPGRADE_PRICING_REPORT.md`

Kullanıcı isteğiyle ekonomi dengesi önceliklendirildi. economist + qa + gameplay ile yürütüldü.

---

## Faz 1 — Temel değerler ✅ (kod + sahne yapıldı, Unity teyidi bekliyor)

Kod default'ları + `EkonomiAyarlari.asset` güncellendi. **qa uyarısı:** Sahne/prefab override'ları eski değerleri tutuyordu (runtime'da 50 TL / 1 prestij / prefab 100 & ×0.85), bu yüzden kod değişikliği tek başına ETKİSİZDİ — gameplay override'ları da düzeltti.

Hedeflenen değerler:
| Değer | Eski (runtime gerçek) | Yeni |
|---|---|---|
| startingMoney (MoneySystem + DifficultyManager) | 100/61 | **500** |
| moneyMultiplierPerPlayer | 0.85 | **1.0** |
| rentGrowthMultiplier | 1.3 | **1.15** (iflas sarmalını çözen kaldıraç) |
| startingPrestige | 5.0 | **15.0** |
| customerLostPrestigePenalty | -2.0 | **-1.5** |
| penaltyPerBox | 60 | **40** |

economist testinde 1P/2P/4P üçü de 16 günü sağlıklı kasayla bitiriyor. **Henüz Unity'de test edilmedi.**

## Faz 2 — Upgrade fiyatlandırması → 🔀 ROGUELITE'A SOĞURULDU

Faz 2 fiyat raporu (`UPGRADE_PRICING_REPORT.md` eski sürüm) hazırdı ama kullanıcı pivot yaptı: soyut statlar (Stamina/Money/Queue/Sabır/Su) kaldırıldı → roguelite perk havuzuna dönüştü. Bu yüzden Faz 2 ayrıca fiyatlanmadı; economist 4 omurga + 16 perk + reroll'u sıfırdan fiyatladı (v3.2). Detay: [roguelite-draft.md](roguelite-draft.md).

**Referans — kullanıcının getirdiği gerçek upgrade envanteri (2026-07-08):**
| Upgrade | Ne yapar | Gerçek satın alma sayısı | Roguelite'taki akıbeti |
|---|---|---|---|
| **Storage** (raf) | Kapasite formülü | 10 | Omurga olarak KALDI (200→290 doğrusal) |
| **Table** (masa) | Ek paketleme masası | 2 | Omurga KALDI (360/470) |
| **Truck** (hangar) | 2./3. hangar kapısı | 2 | Omurga KALDI (300/700) |
| **Quest Tier** | Görev zorluğu+ödülü (sistem PASİF) | 2 | Feature-flag ile havuz dışı |
| **Queue** (kuyruk) | Müşteri sırasını uzatır | 4 | KALDIRILDI → perk havuzu |
| **Money** (gelir) | Gelen parayı artırır | 3 | KALDIRILDI → perk havuzu |
| **Stamina** | Stamina dolma hızı | 3 | KALDIRILDI → perk havuzu |
| **Customer** | Bekleme süresi (patience) | 2 | KALDIRILDI → perk havuzu |
| **Water** | Sadece başarım tetikler | 1 | Tamamen SİLİNDİ |

---

## Bug'lar — ✅ TAMAMLANDI (gameplay, 2026-07-07)

Kullanıcı onayı: "önce bug'ları düzelt".

- ✅ `BoxFallPenalty.cs:17` ters ceza düzeltildi (kutu düşünce çift-eksi → +0.05 artıyordu; artık doğru ceza).
- ✅ `UpgradeAssets.cs` MoreCapacity_4..15 fiyatları dolduruldu (bedava/0 TL açığı kapandı — ölü kod tarafı).
- ✅ Sahne/prefab override'ları düzeltildi → Faz 1 artık etkin (runtime 500 TL / 15 prestij; prefab 500 & ×1.0).
- ✅ `DifficultyManager.ApplyMoneySettings()` para-sıfırlama guard'ı eklendi (`GameStateManager.HasGameEverStarted`).
- ⚠️ Unity kapalıyken yapıldı; sonraki açılışta Console derleme kontrolü yapılmalı.

**Ertelenen (şimdilik dokunulmuyor):**
- ⏸️ Çift-kuyruk: `UpgradePanel` "Kuyruk" = canonical; `ItemType.QueueCapacity_1..3` = orphan ölü kod (hiç `.Buy()` yok). Riskli, sonra temizlenecek.
- ⏸️ `PrestigeManager.GetCustomerCapacity()` dead-code — sonra karar.

**Mimari not (hâlâ geçerli):** Gerçek upgrade'ler **Yol A (UpgradePanel, Inspector-driven)**. `UpgradeAssets.GetCost()` (Yol B) ölü kod. Fiyatlar Inspector/sahne YAML'ına uygulanır, koda değil.
