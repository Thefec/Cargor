# Ekonomi Doğrulama Turu — Tasarım (2026-07-17)

> Durum: onaylandı (kullanıcı), implementasyon planı bekliyor.
> Dal: `feature/economy-verification` (main'den açıldı).
> Baseline: [plans/economy-audit-2026-07-13.md](../../../plans/economy-audit-2026-07-13.md)

## 1. Amaç

2026-07-13 ekonomi denetiminden bu yana ekonomiye dokunan birden fazla değişiklik yapıldı. Denetimin
sonuçları (16-gün sim, 1P–4P sağlıklı, iflas sarmalı yok) bu değişiklikleri içermiyor — yani **bayat**.

Bu tur, "değerler hâlâ dengeli mi" sorusunu cevaplar. Somut bir oyun-hissi şikâyetinden doğmadı;
regresyon doğrulamasıdır.

**Kapsam: çekirdek nakit akışı** — kira / gelir / ceza / prestij ve 16-gün 1P–4P simülasyonu.

**Kapsam DIŞI** (bilinçli): upgrade fiyatlandırması (`UPGRADE_PRICING_REPORT.md` v3.2), roguelite perk
fiyat/güç dengesi, C1 kota kalibrasyonu (play-test'e bağlı, ayrı iş).

## 2. Sim girdisi — varsayımsal, ölçülmüş değil

Gerçek oyuncu "kutu/dakika" verimi kodda yok; yalnızca play-test ile ölçülebilir. Bu tur denetimin
varsayımlarını **bilerek aynen** kullanır:

- **Normal**: 2.0 kutu/dk/oyuncu, %20 kutu hatası, %3 müşteri kaybı
- **Yavaş/Kötü**: 1.2 kutu/dk/oyuncu, %30 hata, %8 müşteri kaybı

Bu, turun cevaplayabileceği soruyu sınırlar:

- ✅ Cevaplar: "Değişen değerler dengeyi bozmuş mu?" (karşılaştırma aynı tabanı kullandığı için geçerli)
- ❌ Cevaplamaz: "Oyun mutlak olarak dengeli mi?" — bu play-test ölçümü gerektirir

Perkler nötr default'ta sabit tutulur (`rentScaledMultiplier=1`, `rewardVolatility=0`,
`phoneRingPerkBonus=0`) — denetim de perksiz varsaymıştı, karşılaştırılabilirlik için korunur.

## 3. Denetimden bu yana değişenler (sim'e işlenecek)

| Etki | Değişiklik | Kaynak | Neden önemli |
|---|---|---|---|
| **Büyük** | `maxPrestige` 100 → **150** | `PrestigeManager.cs:19` | Tavan değil, gelir çarpanı. Ödül = `rewardPerBox + floor(prestij/10)×5`. Tavan 100 → tier 10 → **100 TL/kutu**; tavan 150 → tier 15 → **125 TL/kutu**. Denetim 3-4P'de tavanın gün 9-13'te dolduğunu bulmuştu → son üçte bir **%25 daha fazla gelir**. Denetimin son-kasa tablosu muhtemelen olduğundan düşük. |
| **Orta** | `requiredCargo` 3-7 → **2-6** | TruckSpawner (`09197b9`) | Tır başına ortalama kargo 5 → 4. |
| **Orta** | `hangarStayDuration` 120 → **30sn** | `GameEconomySettings.cs:48` | economist "ekonomik nötr" demişti (gelir kutu-başı). Normal hızda doğru; ama 30sn artık **gerçek üretim tavanı** — yavaş takım tır kalkmadan kutu yetiştiremezse gelir sıfırlanır. Denetim sim'i tır penceresini HİÇ modellemiyordu (`min(müşteri, maks_teslimat)`). **Yeni modelleme gerekir.** |
| Küçük | `boxDropMoneyPenalty` = **5** (merkezileşti) | `GameEconomySettings.cs:69` | Eskiden prefab başına tutarsız (1/5/10). |
| Küçük | `wrongDeliveryPrestigePenalty` = **-0.2** (yeni) | `GameEconomySettings.cs:111` | Denetimde yoktu. |
| Küçük | wealthTax **kaldırıldı** | `9d2c3b0` | Denetim sim'inde hâlâ `wealthTaxRate=0.1` var → sökülmeli. |
| Belirsiz | Tır–müşteri renk dengesi | `09197b9`, `4c0a5ca` | Yanlış-teslim oranını düşürmüş olabilir → `penaltyPerBox` yükü azalır. Sim'de hata oranı sabit varsayıldığından ikinci-derece; not düşülür. |

## 4. Yaklaşım: Node sim tek kaynak

Kurtarılan `sim.js` (2026-07-13 denetiminden, eski oturum scratchpad'inde bulundu)
`tools/economy-sim/` altına taşınır ve güncel değerlere göre yenilenir.

**Neden Node, C# değil:** saniyeler içinde koşar, Unity gerekmez, 8 senaryo diff'i kolay, versiyon
kontrollü → bir sonraki ekonomi turu ucuz. Bedeli: ekonomi mantığı iki dilde yaşar (ayrışma riski).
Bu kabul edildi, çünkü sim zaten davranışı **taklit** ediyor, kodu paylaşmıyor — C# sim'i de aynı
riski taşıyordu.

**Ayrışmayı azaltan kural:** `tools/economy-sim/sim.js` başlığı her değeri kaynak dosya:satır ile
belgeler + tarih taşır. Sim ile kod ayrıştığında bu başlık tek kontrol noktasıdır.

### Bayat C# sim siliniyor

`GameEconomySettings.cs:152-297` (`#if UNITY_EDITOR` ContextMenu simülasyonu, ~145 satır) **silinir**.
Doğrulanmış sapmaları:

- 15 gün koşuyor — gerçek `MAX_DAYS = 16` (`DayCycleManager.cs:35`)
- Prestiji `5.0`'dan başlatıyor — gerçek `startingPrestige = 15` (`PrestigeManager.cs:16`)
- `0..100`'e clamp — gerçek `maxPrestige = 150` (`PrestigeManager.cs:19`)
- `playerCountMultiplier` (1.0/1.3/1.6/1.9) **hiç uygulanmıyor** → 2P/3P/4P müşteri sayısı yanlış düşük
- Müşteri kaçma, `boxDropMoneyPenalty`, `wrongDeliveryPrestigePenalty` modellenmemiş

Build etkisi sıfır (`#if UNITY_EDITOR`), ama Unity'de o menüye tıklayan **güvenle yanlış sayı alıyor**.
Silme sonrası `GameEconomySettings.cs` yalnızca ayar + `GetBaseRent`/`CalculateRent` içerir.

## 5. Başarı kriterleri

Denetimin eksiği: sayı üretti ama "iyi mi kötü mü" tanımı yoktu. Kullanıcı hedefi: **"kıl payı başarmış"** —
son günlerde hâlâ kira derdi, kasa ince, kötü bir gün oyunu bitirebilir (roguelite tekrar-oynanış).

| Eksen | Geçme ölçütü |
|---|---|
| İflas (Normal) | 1P–4P hiçbiri iflas etmemeli |
| İflas (Yavaş) | Erken iflas **olmalı** (~gün 8) — denetimdeki davranış korunmalı |
| Prestij pacing | Tavana gün ~14'ten önce çarpmamalı |
| Kira baskısı | Gün 16 kirası ödendikten **sonraki** kasa, o kiranın **3 katını aşmamalı** (ör. kira 875 TL ise final kasa < 2625 TL) |
| Oyuncu ölçeği | 4P'nin final kasası, 1P'nin final kasasının **4 katını aşmamalı** (aynı Normal senaryoda) |

Ölçüt ihlal edilirse economist değer önerir; ihlal yoksa değer değişmez (bu bir doğrulama turu,
bahane bulup ayar yapma turu değil).

## 6. Çıktılar

1. `tools/economy-sim/sim.js` — repoda kalıcı, kaynak-değer başlıklı
2. Sim koşusu: 1P–4P × Normal/Yavaş = 8 senaryo, tek karşılaştırma tablosu
3. `plans/economy-audit-2026-07-17.md` — **yeni** rapor, denetim→bugün delta'sı. Eski rapor
   **silinmez** (baseline'dır)
4. Sapma varsa: `EkonomiAyarlari.asset` / `GameEconomySettings.cs` değer düzeltmesi
5. `GameEconomySettings.cs:152-297` silinir
6. `plans/devam.md` + `PLAN.md` güncellenir

## 7. İş akışı

CLAUDE.md BÜYÜK/RİSKLİ iş (ekonomik değer içeriyor):

- **economist** yazar (sim + rapor + değer önerileri)
- **kontrol** ONAY kapısı — dal-sonu **tek toplu** kapı (her adımda değil, tur sayısını düşürmek için)
- Müdür sim çıktısını kendi doğrular
- Unity **kapalı** → C# sim silindikten sonra headless EditMode ile 0 derleme hatası teyidi

## 8. Riskler

| Risk | Azaltma |
|---|---|
| Sim ↔ kod ayrışması (iki dil) | `sim.js` başlığı kaynak dosya:satır + tarih belgeler |
| Varsayımsal kutu/dk → mutlak zorluk bilinmiyor | Bilinçli kabul; rapor bunu açıkça işaretler. Play-test ölçümü gelirse sim tekrar koşar (ucuz, artık repoda) |
| `DayCycleManager.cs` merge çakışması | `feature/netcode-auth-hardening` bu dosyaya dokundu (N3). Ekonomi `CalculateRent`/`ProcessDayEnd`'e dokunursa çakışma olabilir — iki dal merge edilirken dikkat |
| Çalışma ağacındaki font/materyal churn | Commit **DIŞI** ([[unity-batchmode-artifacts]]) — seçici commit |

## 9. Açık kalan (bu tur DIŞI)

- **C1 kota-ölümü**: kota kodda ölü (sadece UI). Açılması gerçek kutu/dk ölçümüne bağlı → play-test blocker
- **Netcode play-test borcu**: `feature/netcode-auth-hardening` 3 commit merge bekliyor
- Upgrade fiyatlandırması + roguelite perk dengesi
