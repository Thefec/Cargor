---
name: post-rent-features-pricing-2026-08-15
description: Kira-sonrası 3 özellik (gün5 iade/gün9 2-item/gün13 karışık tır) ekonomi kararları — dal feature/post-rent-mechanics
metadata:
  type: project
---

Plan: `plans/ilk-kiradan-sonra-m-steriler-serialized-cosmos.md` (dal `feature/post-rent-mechanics`,
main'den temiz). Kod henüz yazılmadı — bu değerler gameplay departmanına devredilecek.

## Kararlar
1. **İade oranı %25** (aralık %20-30). Gerekçe: normal akış kapasitesinin %75'i kalıyor + iade
   modu paketleme masasını (S=6sn, [[sim_v31_table_contention]] 2. en duyarlı sayı) hiç kullanmıyor
   → masa çekişmesini rahatlatıyor, %25 güvenli.
2. **İade başarı/hata = mevcut alanlarla birebir aynı büyüklük**: `returnServedPrestigeBonus=0.4`
   (=`customerServedPrestigeBonus`), `returnFailedPrestigePenalty=-0.08`
   (=`wrongProductPrestigePenalty`). Ayna-simetrik mekanik, farklı eğri icat etme.
3. **İade'de PARA YOK** (ne ödül ne ceza). [[money_comes_only_from_trucks]] invariant'ı — paranın
   TEK kaynağı tır; iadeye para eklemek yeni musluk açar, FAZ4 kalibrasyonunu bozar.
4. **2-item interactionTime ×1.3** (5s→6.5s), TAM ×2 DEĞİL. Python doğrulama: ×1.3'te
   müşteri/saat throughput -%23 ama ürün/saniye throughput (asıl para kaynağı, kutu sayısı)
   +%54 kazanç; ×2.0 seçilirse ürün throughput'u TAM NÖTR (±0%, özelliğin ekonomik faydası kalmaz).
   **Risk:** prestij müşteri-başına ödeniyor (kalem-başına değil) → müşteri/saat -%23 düşüşü
   prestij/saat'i de düşürür, tam gün 9'un kira döngüsü +35% arttığı ana denk geliyor
   ([[faz2_prestige_rent_event_2026-07-30]]'daki 2-istasyon "monoton artan" prestij eğrisi geçici
   düzleşebilir). Başlangıçta dokunma, playtest'te düşüş görülürse +0.1 (0.4→0.5, yalnız 2-item
   modunda) telafi opsiyonu hazır tut.
5. **Karışık tır = düz toplam, PRİM EKLEME**. `Truck.cs:610-620 CalculateRewardWithPrestige()`
   zaten renk-bağımsız (`baseReward=rewardPerBox` sabit) → kod değişikliği gerekmeden hazır.
   Prim gereksiz çünkü karışık teslimat zaten tırın ZORUNLU koşulu (kaçınma seçeneği yok, teşvike
   gerek yok), üstelik gün 13 en yüksek kira baskısı döngüsünde (+82%) yeni musluk riskli.
6. **Gün eşiği ÇAKIŞMASI — risk, eskale edildi**: `DraftPool.T2_UNLOCK_DAY=5` /
   `T3_UNLOCK_DAY=9` (`Assets/NewCss/Roguelite/DraftPool.cs:11-12`) planın
   `RETURN_UNLOCK_DAY=5`/`DUAL_ITEM_UNLOCK_DAY=9` ile TAM çakışıyor. Gün 9'da ayrıca madde 4'ün
   prestij-düşüş riskiyle üst üste biniyor (üçlü yük: yeni perk tier + yeni müşteri modu + kira
   döngü artışı). Gün 13 bağımsız, düşük risk. Karar müdürün — 1 gün kaydırma
   (`RETURN=6`,`DUAL_ITEM=10`) düşük maliyetli bir yumuşatma seçeneği.

## Kalibrasyon tabanı (bu turda kullanılan)
Kira dizisi `{500,1000,1450,1800}` × `rentGrowthMultiplier=1.35`, döngü göre:
gün5=döngü0 (500/1000/1450/1800), gün9=döngü1 (675/1350/1958/2430, +35%),
gün13=döngü2 (911/1823/2643/3281, +82%). `interactionTime` baz=5s (`CustomerAI.cs:87`),
2 paralel istasyon canlı ve `AssignDropOffTable` gerçekten çağrılıyor
(`CustomerManager.cs:905-953` — [[serial_customer_service_ceiling]]'deki "sıfır çağrıcı" notu
ARTIK BAYAT, kod ilerlemiş). `DisplayTable.SlotCount` dinamik (`slotPoints.Length`) — 2-item için
masa tarafında ek mühendislik gerekmiyor.

İlgili: [[faz4_final_value_set_2026-07-30]] [[money_comes_only_from_trucks]]
[[sim_v31_table_contention]] [[serial_customer_service_ceiling]] [[box_drop_penalty_centralization]]
