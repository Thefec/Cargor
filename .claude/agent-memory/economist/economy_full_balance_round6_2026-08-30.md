---
name: economy-full-balance-round6-2026-08-30
description: Round 6 prestij ekonomisi — prestij bir FAİL-STATE değil GELİR ÇARPANI (tır gelirinin %13-35'i); kazanma eşiği 16/16 hücrede ayırt edici DEĞİL; customerMissedQuotaPrestigePenalty yapısal ceza DEĞİL; yanlış-ürün cezası 5x TERS (dump-the-customer exploiti); CheckWinCondition docstring'i yalan
metadata:
  type: project
---

# Round 6 — Prestij Ekonomisi (2026-08-30, Opus)

Kod DEĞİŞMEDİ (`.cs`/`.asset`/sahne/`sim.js` hiçbiri). Ölçüm `runFullSim` (v4.0) +
`SRC4` geçici mutasyonu (geri alındı) ile. **Kullanılan kira: CANLI
`{500,1000,1450,1800}` / g=1.20** — Round 3'ün `{290,650,1140,1630}` önerisi HÂLÂ
UYGULANMADI. Telefon oranı Round 2'nin optimal tablosu (strict: P2=%25, diğerleri %0;
optimistic: %25).

## 1. Prestij sabitleri envanteri (canlı, `GameEconomySettings.cs:141-156` + asset:36-40)

| Sabit | Değer | \|lost\| oranı | Tetikleyici | Para cezası eşlik ediyor mu |
|---|---|---|---|---|
| `customerServedPrestigeBonus` | **+0.4** | 1.00x | servis edilen her müşteri | — (para YOK, `CustomerAI.cs:1378`) |
| `callPrestigeReward` | **+0.4** | 1.00x | her telefon araması | +20 TL |
| `customerLostPrestigePenalty` | **−0.4** | 1.00x (referans) | sabrı dolan / 17:30 kesilen | yok |
| `customerMissedQuotaPrestigePenalty` | **−0.2** | 0.50x | hiç spawn olmamış kota müşterisi | yok. ASSET'TE YOK → C# initializer canlı |
| `wrongDeliveryPrestigePenalty` | **−0.16** | 0.40x | tıra yanlış renk kutu | **+40 TL** (`penaltyPerBox`) |
| `wrongProductPrestigePenalty` | **−0.08** | 0.20x | müşteriye yanlış ürün | **yok** |
| `boxDropPrestigePenalty` | **−0.04** | 0.10x | kutu yere düşme | +5 TL |
| `prestigePerBonus` / `bonusPerTier` | 8 / 5 | — | her 8 prestij = +5 TL/kutu | — |
| `startingPrestige` / `maxPrestige` | 12 / 100 | — | sahne `The Main Office.unity:31614,31615` | — |
| SURPRISE AUDIT `GetPenaltyMultiplier()` | ×2 | — | 5 çağrı yeri (`EventEffectManager.cs:455`) | ödülleri ETKİLEMEZ |

## 2. ⭐ EN BÜYÜK BULGU: prestij bir FAİL-STATE değil, GİZLİ GELİR ÇARPANI

Prestij tier bonusu (`floor(prestij/8)*5` TL/kutu, `Truck.cs:709-714`) 16 günde kutu
ödülünü **+%20…+%91** şişiriyor ve **toplam tır gelirinin %13-35'ini** oluşturuyor:

| hücre | taban ödül | gün-16 ödül | artış | tier geliri / toplam tır geliri |
|---|---|---|---|---|
| P1 Normal/strict | 50 | 75 | +50% | 690 / 3 136 = **%22** |
| P2 Normal/strict | 55 | 105 | **+91%** | 2 072 / 6 290 = **%33** |
| P3 Normal/strict | 70 | 110 | +57% | 2 819 / 10 858 = %26 |
| P4 Normal/strict | 88 | 128 | +45% | 3 106 / 14 462 = %21 |
| P2 Normal/optimistic | 55 | 105 | +91% | 4 638 / 13 091 = **%35** |
| P1 Slow/strict | 50 | 60 | +20% | 140 / 1 087 = %13 |

**1 prestij puanının marjinal TL değeri** (o günden sonra işlenecek kutu × 5/8):
P1 gün1 **34.7 TL**, P4 gün1 **91.7 TL**, gün 12'de 13-36 TL, gün 16'da 2.8-7.8 TL.
→ Prestij erken oyunda ÇOK değerli, geç oyunda neredeyse değersiz (kalan gün sayısıyla
lineer sönüyor). Bu kabul edilebilir bir eğri ama **oyuncuya hiç anlatılmıyor** —
prestij UI'da "0-100 bar" olarak duruyor, para çarpanı olduğu görünmüyor.

Sonuç: `customerLostPrestigePenalty=-0.4` "kalite standardı" değil, **32.7 TL'lik
(P3 gün1) gizli para cezası**. Her ceza sabitinin TL karşılığı (P3, gün 1, 130.7 kutu kalan):
lost −32.7 TL · mq −16.3 TL · wrongDel −13.1 TL (+40 TL nakit = toplam **−53 TL**) ·
wrongProd −6.5 TL · drop −3.3 TL (+5 TL = −8.3 TL).

## 3. `customerMissedQuotaPrestigePenalty` YAPISAL CEZA DEĞİL (hipotez ÇÜRÜTÜLDÜ)

Round 2 §4'ün "kutu/kota oranı 0.20-0.47" bulgusu **farklı bir kanal** — o KUTU/para
zinciri (mekanik tavan bağlıyor), bu ise MÜŞTERİ/spawn zinciri. Karıştırılmamalı.

Ölçülen 16 günlük toplam `missedQuota` prestij maliyeti (canlı hız, ×1.0):

| bant | ceza tetikleyen gün | toplam kota-kaçan | prestij maliyeti | aynı hücrede `lost` maliyeti |
|---|---|---|---|---|
| Normal/strict (4 hücre) | **0/16** | 0.0 | **0.00** | −0.56 … −0.72 |
| Normal/optimistic (4) | **0/16** | 0.0 | **0.00** | 0 … −0.72 |
| Slow/strict (4, g12 iflas) | 1-8/12 | 0.2 – 10.3 | −0.04 … **−2.06** | −5.6 … −8.96 |
| Slow/optimistic (4) | 0-8/16 | 0.0 – 7.7 | 0.00 … **−1.54** | 0 … −8.8 |

Kısıt analizi (`P3`): **Normal/strict'te 16/16 gün bağlayan kol KOTA'nın kendisi**
(spawn == kota, varış tavanı 8.5→13.4 ve servis tavanı+kuyruk 11.6→14.5 hep kotanın
üstünde). Slow/strict'te gün 1-8 KOTA, gün **9-16 SERVİS** bağlıyor — kırılma noktası
`dualItemUnlockDay=9` (etkileşim süresi ×1.30). Yani ceza "her gün" değil,
**gün-9 unlock'una bağlı, yalnız yavaş bantta** tetikleniyor.

Servis yavaşlığı duyarlılığı (oyuncu tepki gecikmesi modellenmediği için üst sınır testi):

| servis ×1.0 | ×1.2 | ×1.5 |
|---|---|---|
| max −2.06 prestij | max −4.14 | max **−8.24** (P3/P4 Slow/optimistic, 16/16 gün) |

**Karar: −0.2 sabiti "yapısal ceza" DEĞİL.** En kötü senaryoda bile 16 günlük toplam
maliyet −8.24, aynı koşumda `customerLostPrestigePenalty`'nin maliyeti −12.8 ve
`customerServedPrestigeBonus`'un kazancı +40…+52. Sistem net POZİTİF kalıyor.
**Ama işaretlenmesi gereken 2 şey var:**
- Ceza tamamen **oyuncunun kontrol EDEMEDİĞİ** bir olaya bağlı: kota spawn'ı arz
  tarafı, oyuncu yalnız servis hızıyla dolaylı etkiliyor. Slow bantta gün 9'dan sonra
  her gün 0.3-1.6 müşteri "hiç gelmeden" cezalanıyor → **oyuncu geri bildirimi kör**.
- `dailyCustomerMultiplier` (BUSY DAY +%35) bu cezanın YAKITI (Round 5 §3'ün tespiti):
  kota artıyor, varış aralığı değişmiyor → fazla kota doğrudan −0.2'lere dönüşüyor.
  Round 5'in "doğru kol spawn hızı" önerisi bu cezayı da düzeltir.

## 4. ⭐ KAZANMA EŞİĞİ AYIRT EDİCİ DEĞİL (16/16)

`GameStateManager.CheckWinCondition` (cs:696-712) — **docstring YALAN**. Yorum
"prestige > 0 and rent paid" diyor (cs:691, cs:706), kod **yalnız
`currentDay >= DayCycleManager.MAX_DAYS`** kontrol ediyor. Ne prestij ne kira kontrolü
var. Prestij kapısı fiilen `PrestigeManager.ModifyPrestige` (cs:154-157) içinde:
`newPrestige <= 0` → `TriggerLose()`. Pratikte eşdeğer (prestij 0'a düşen oyun zaten
bitmiş olur) ama **`SetPrestige` bu kapıdan geçmiyor** (clamp var, TriggerLose yok) —
şu an dış çağıranı yok, ileride bağlanırsa sessiz bir kaçak olur.

16 hücrede finalPrestige dağılımı (Round 2 optimal telefon oranı):

| hücre | kazandı | sebep | final prestij | başlangıcın katı |
|---|---|---|---|---|
| P1/P2/P3/P4 Normal/strict | ✅ | — | 43.0 / 88.0 / 76.0 / 75.4 | 3.6-7.3x |
| P1/P2/P3/P4 Normal/optimistic | ✅ | — | 53.5 / 90.4 / 93.4 / 93.4 | 4.5-7.8x |
| P1/P2/P3/P4 Slow/strict | ❌ | **NAKİT iflas g12** | 18.9 / 39.8 / 43.4 / 43.4 | 1.6-3.6x |
| P1/P2/P3/P4 Slow/optimistic | ✅ | — | 51.8 / 72.9 / 70.4 / 70.4 | 4.3-6.1x |

**16/16 hücrede prestij ASLA 0'a inmiyor.** Kaybeden 4 hücre NAKİT'ten kaybediyor ve
o anda bile prestijleri 18.9-43.4 (eşiğin 19-43 puan üstünde). **En düşük gözlenen
marj 18.87** — cliff'e yakın hiçbir hücre yok. Prestij fail-state'i tamamen ÖLÜ bir kol.

Ne kadar kötü oynamak gerekiyor? (spawn olan müşterilerin f oranı kaçıyor)

| f (kaçan oranı) | P1 ölüm günü | P2 | P3 | P4 |
|---|---|---|---|---|
| ≤50% | — (asla) | — | — | — |
| 60% | — (son prestij 5.0) | g15 | g14 | g14 |
| 75% | g13 | g8 | g7 | g7 |
| 100% (hiç servis yok) | g8 | g5 | g4 | g4 |

Başabaş oran tam **%50** — `served=+0.4` ile `lost=−0.4` birebir simetrik olduğu için.
Yani "müşterilerinin yarısını kaçıran" bir oyuncu sonsuza kadar hayatta.
**Yan bulgu: prestij ölümü P ile ASİMETRİK.** Ceza müşteri BAŞINA ama `startingPrestige=12`
ve eşik 0 P'den BAĞIMSIZ → aynı beceriksizlik oranında P4 takımı P1'den **2 kat hızlı**
ölüyor (f=%75'te g7 vs g13). P4'ün kotası 2x, tamponu aynı.

## 5. ⭐ CEZA ORANI TERSLİĞİ: "dump the customer" exploiti

`CustomerAI.cs:1360-1366`: yanlış ürün gösterilirse `HandleFailedInteraction()`
(**−0.08**) → `TransitionToExit()`. Müşteri ANINDA çıkıyor, kuyruktan siliniyor,
`_hasTimedOut=false` kaldığı için 17:30 kesiminde tekrar cezalanmıyor.
Sabrı dolarsa `HandleTimeUp()` → `OnCustomerLost()` (**−0.4**) + istasyon sabır
süresi boyunca BLOKE.

→ Doğru ürünü elinde olmayan oyuncu için **bilerek yanlış ürün göstermek kesinlikle
baskın strateji**: prestij cezası **5 kat ucuz** VE istasyonu anında boşaltıyor
(çift kazanç). Tasarım niyetinin tersi — aktif hata pasif hatadan ucuz.
Round 2'nin grace exploiti gibi "daha kötü oyna, daha iyi sonuç al" kalıbı.

Diğer oran tutarsızlıkları:
- `wrongDelivery` (−0.16) **+40 TL** nakit cezasıyla birlikte geliyor (toplam ≈ −53 TL
  P3 gün1'de); `wrongProduct` (−0.08) **hiç nakit cezası yok** (−6.5 TL). Aynı sınıf
  hatanın maliyeti 8 kat farklı. `wrongProduct`'ın nakit karşılığı olmayışı doğru
  (para yalnız tırdan gelir, `money_comes_only_from_trucks`) ama prestij tarafı bunu
  telafi etmiyor.
- `boxDrop` (−0.04, 0.10x) doğru kalibre — en sık, en ucuz, düzeltilebilir hata.
- `callPrestigeReward` (+0.4) = `customerServedPrestigeBonus` (+0.4). Bir telefon
  araması (buton, ~1sn basılı tut, 3sn cooldown) tam bir müşteri servisi kadar prestij
  veriyor. **Teorik tavan: 16 günde tüm kotayı telefonla çekmek P1'de +30.8, P3/P4'te
  +64.0 prestij** = başlangıcın 2.6-5.3 katı. Şu an bağlayıcı olmuyor çünkü telefon
  gerçek saniye yakıyor (Round 1 §3 trap) — yani telefonu **ekonomik olarak** frenleyen
  şey prestij tasarımı değil, bir yan etki. Round 7 telefonun para tarafını düzeltirse
  bu prestij kolu enflasyon kaynağına dönebilir. **Round 7'ye zorunlu girdi.**

## 6. SURPRISE AUDIT (×2 ceza) etkisiz

En kötü tek gün prestij maliyeti normalde −0.07…−1.40; AUDIT günü −0.15…−2.79.
**Ek maliyet final prestijin %0.1-4.6'sı.** Hiçbir hücrede karar değiştirmiyor.
Sebep: ceza kanalları zaten küçük. Event ancak ceza sabitleri 3-5 kat büyütülürse
anlamlı olur — yani `GetPenaltyMultiplier()` bugün dekoratif.

## 7. Perk etkileri (yalnız prestij tarafı; fiyat Round 4'ün konusu)

- **`prestige_master`** (`PerkEffect.cs:209`, `0.4 + 0.12*level`): kodda `= 0.4f` tabanı
  var, canlı taban da 0.4 → **stale-baseline bug YOK** (yorumdaki "FAZ4: taban ×2"
  tarihsel bir açıklama, formül doğru). AMA **tavanı patlatıyor**: L1'de 6/16 hücre,
  L2'de **9/16 hücre gün 13-16'da `maxPrestige=100` tavanına çarpıyor**. Tavana çarpan
  hücrede perkin marjinal değeri SIFIR. L2 (0.64) fiilen L1'den yalnız "tavana 1-2 gün
  daha erken varmak" satın alıyor. Round 10 notu: prestij tavanı 100, `prestigePerBonus=8`
  ile en fazla 12 tier veriyor; taban koşumda 9-11 tier'a zaten ulaşılıyor
  (**tavanın %75-92'si kullanılıyor**) — perk için baş boşluk kalmamış.
- **`prestige_broker`** (`PerkEffect.cs:199`, `bonusPerTier = 5 + 0.5*level`): canlı
  tıra yazılıyor, çalışıyor. L2 değeri **+%3.7…+%20.9 final kasa** (17-1 089 TL) —
  bant-bağımlılığı Ek Hangar kadar aşırı değil, sağlıklı. Slow/strict'te en zayıf
  (%3.7-9.1) çünkü orada prestij zaten düşük — doğru yönde ölçekleniyor.

## 8. Model açıkları (dürüstlük notu)

- `wrongProductPrestigePenalty` `runFullSim`'de HİÇ modellenmiyor (§5 analizi kod
  okumasına dayanıyor, sim'e değil). Büyüklüğü küçük (−0.08 × birkaç olay/gün).
- Oyuncu tepki gecikmesi modellenmiyor → `lost`/`missedQuota` sayıları **alt sınır**.
  §3'ün ×1.2/×1.5 taraması bu belirsizliği kapsamak için yapıldı.
- Event'ler `runFullSim`'de yok (Round 5'in ayrı harness'ı vardı); §6 AUDIT hesabı
  taban koşumun günlük ceza kanallarının ×2'siyle analitik yapıldı.

## 9. Round 10 için biriken öneriler (DEĞER DEĞİŞİKLİĞİ ÖNERİLMEDİ, tespit)

1. **TASARIM**: `CheckWinCondition` docstring'i (cs:691, 706) kodla çelişiyor —
   ya yorum düzeltilsin ya prestij/kira kontrolü gerçekten eklensin.
2. **DENGE (yüksek)**: `wrongProductPrestigePenalty=-0.08` → müşteri kaybının en az
   yarısına (≥−0.2) çıkarılmalı; şu an "bilerek yanlış ürün ver" 5x ucuz baskın strateji.
   Alternatif: yanlış ürün de `OnCustomerLost` yolundan geçsin (o zaman sabit gereksiz).
3. **DENGE (orta)**: prestij fail-state'i 16/16 hücrede ölü. Ya eşik anlamlı hale
   getirilsin (örn. gün-16'da `prestij ≥ 30` kazanma şartı — taban koşumda 12/16 hücre
   geçer, Slow/strict'in 18.9'u geçmez) ya da `startingPrestige`/ceza büyüklükleri
   P-bazlı yapılıp ölüm eğrisi P'ye göre eşitlensin.
4. **DENGE (orta)**: `maxPrestige=100` taban koşumda %75-92 doluyor → `prestige_master`
   L2'nin marjinal değeri sıfırlanıyor. Tavan yükseltmek yerine `prestigePerBonus`'u
   büyütmek (8→10) hem tier enflasyonunu yavaşlatır hem perke baş boşluğu açar
   — ama tır gelirinin %13-35'ini keser, Round 3 kirasıyla BİRLİKTE değerlendirilmeli.
5. **İZLE**: `callPrestigeReward=0.4` (= tam müşteri servisi). Round 7 telefonun
   zaman maliyetini düzeltirse teorik +30.8…+64.0 prestij enflasyon kanalı açılır.
6. **TEMİZLİK**: `GetPenaltyMultiplier()` (SURPRISE AUDIT ×2) etkisi %0.1-4.6 —
   ceza sabitleri büyümedikçe dekoratif.

İlgili: [[economy_full_balance_round2_2026-08-30]] (kutu/kota kanalı ayrımı),
[[economy_full_balance_round5_2026-08-30]] (kota çarpanı ölü kol, AUDIT),
[[prestige_100_rescale_2026-07-20]] (0-100 skalası, k=0.4),
[[prestige_function_surface]] (prestijin tek işlevi ödül tier'ı — DOĞRULANDI),
[[money_comes_only_from_trucks]].
