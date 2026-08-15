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

## DÜZELTME (2026-08-15, QA bulgusu üzerine) — BoxRequest-dual (iade, 2 renk) ayrı sabit
QA `CustomerAI.cs`'de madde 4'ün tek bir E-etkileşim varsayımının yalnız **ProductSupply-dual**
(normal müşteri 2 ürün) için geçerli olduğunu, **BoxRequest-dual**'ın (iade müşterisi 2 farklı
renk ister) fiilen İKİ AYRI E-etkileşimi olduğunu buldu (oyuncu tek seferde tek kutu taşıyor).
Python doğrulama (`base=5s`, `baseline_rate=1 ürün/5s`):
- ProductSupply-dual referans: ×1.3 tek etkileşim (6.5s) → 2 ürün/6.5s = **+53.8%** throughput
  (madde 4'teki mevcut karar, DEĞİŞMEDİ).
- BoxRequest-dual, ×1.3'ü AYNEN KORUsaydı (mevcut kod, iki bacağa da uygulanmış): 2×6.5s=13s
  toplam → 2 ürün/13s = **-23.1%** throughput. Soru metnindeki "throughput'a zarar vermez"
  varsayımı YANLIŞ — kompresyon avantajı (1 etkileşimde 2 ürün) burada yok, ×1.3 her bacağa binmesi
  saf bir çifte ceza (1.3² = 1.69× toplam ağırlık, tasarım amacının çok üstünde).
- ×1.00 (çarpansız, doğal 2×): 10s toplam → nötr (%0), ama "iade lojistiği biraz daha zahmetli"
  hissi tamamen kayboluyor.
- ×1.15/bacak: 2×5.75s=11.5s toplam → **-13.0%** throughput. Bileşik ×1.3225 ≈ orijinal tek-tur
  ×1.3'e yakın büyüklükte (tesadüf değil, kullanıcının önerisiyle örtüşüyor), ama throughput etkisi
  hâlâ NÖTR DEĞİL, sadece -23%'ün ~yarısı kadar yumuşak bir ceza.

**KARAR: DÜŞÜR.** Yeni ayrı sabit `DUAL_ITEM_BOXREQUEST_INTERACTION_TIME_MULTIPLIER = 1.15`
(bacak başına, `PostRentFeatureUnlocks` içinde `DUAL_ITEM_INTERACTION_TIME_MULTIPLIER`dan BAĞIMSIZ
alan). Gerekçe:
1. İki mekanik yapısal olarak farklı: ProductSupply-dual'da ×1.3 bir KOMPRESYON ödülünü modelliyor
   (2 ürünü 1 etkileşimde toplamanın getirisi); BoxRequest-dual'da bu ödül fiziksel olarak mümkün
   değil (taşıma kapasitesi=1 kutu). Aynı sabiti iki yere uygulamak kategori hatası — biri
   diğerinin kalibrasyonunu miras alıyor ama farklı bir ekonomik anlam taşıyor.
2. Mevcut kod (×1.3 her bacağa) -23.1% ile madde 4'ün ProductSupply-dual için zaten kabul edilen
   -23% müşteri/saat riskiyle (madde 4, risk notu) AYNI BÜYÜKLÜKTE bir ikinci düşüşü BoxRequest-dual
   üzerinden de gün 9'a yığıyor — madde 6'daki gün 9 çakışma riskini (kira +35% + DraftPool tier)
   üçüncü bir katmanla ağırlaştırıyor.
3. ×1.15 (-13.0%) hem "iade lojistiği zahmetli" hissini koruyor hem de gün 9 risk yığınını
   ×1.3'e göre yaklaşık yarıya indiriyor. Nötr (×1.00) tercih edilmedi çünkü BoxRequest-dual'ın
   ProductSupply-dual'a göre zaten dezavantajlı kalması (kompresyon ödülü yok) tasarım açısından
   makul — sıfır ceza fazla cömert olur, oyuncu iade müşterisini "bedavaya zor" bulur.
4. Ayrı isimlendirme zorunlu: aynı alan paylaşılırsa gelecekte biri tune edilirken diğeri sessizce
   kayar (madde 4'ün playtest telafi opsiyonu — 0.4→0.5 — BoxRequest-dual'ı da habersiz etkiler).

**Ek risk notu:** ×1.15'te bile BoxRequest-dual, ProductSupply-dual'ın +53.8%'ine karşı -13.0%'da
kalıyor — aynı "2-item" tier'i içinde iki müşteri tipi arasında ~67 puanlık throughput farkı var.
Playtest'te oyuncular BoxRequest (iade) müşterisini fark edip önceliksizleştirirse (serve etmeyi
erteleme davranışı), bu prestij cezasına (`returnFailedPrestigePenalty=-0.08`) çarpar — izlenmesi
gereken bir davranışsal risk, ceza büyüklüğü şimdilik değiştirilmiyor.

İlgili: [[faz4_final_value_set_2026-07-30]] [[money_comes_only_from_trucks]]
[[sim_v31_table_contention]] [[serial_customer_service_ceiling]] [[box_drop_penalty_centralization]]
