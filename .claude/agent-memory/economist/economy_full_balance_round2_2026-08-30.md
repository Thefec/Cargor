---
name: economy-full-balance-round2-2026-08-30
description: Round 2 - zayif bant haritasi; telefon optimal kullanim orani STRICT'te %0-25 (tasarim kusuru, u=100'de 15/16 hucre iflas); P1 146 TL marji SAHTE (kullanilmamis grace tamponu gercek marj); Slow-strict gun-12 duvari SEVIYE sorunu (kira egrisi degil); grace "fakir kal" exploiti = 0.2 x son kira
metadata:
  type: project
---

# Round 2 — Zayıf bant haritası (2026-08-30)

Takip: `plans/economy-full-balance-2026-08-30.md`. Yalnız `runFullSim` (sim.js v4.0) kullanıldı.
Kod DEĞİŞTİRİLMEDİ; analiz scriptleri scratchpad'de.

## 0. Round 1'in §3 bulgusu HATALIYDI — düzeltildi
`phoneCooldownPerkBonusSeconds` **statik varsayılanı** 0f (`GameEconomySettings.cs:123`), ama
`PerkEffect.cs:301` perk satın alınınca `= 10f` mutlak ataması yapıyor (bu turda tekrar
doğrulandı). Yani perk BAĞLI ve ÇALIŞIYOR; sorun `Mathf.Max(1f, 3-10)=1f` ile **tabana
çakılması**. Round 7 çerçevesi: "taban altı, yeni değer öner". Bkz.
[[phone_cooldown_perk_event_stacking_2026-08-29]].

## 1. 16 hücre × telefon kullanım oranı (finalCash; `*`= grace tüketildi)

| senaryo | bant | P | u=0 | u=25% | u=50% | u=75% | u=100% | OPTİMAL | varsayılan (0.6/0.1) |
|---|---|---|---|---|---|---|---|---|---|
| Normal | strict | 1 | **768** | 654 | 299 | 145* | IFLAS g16 | **0%** | 146 |
| Normal | strict | 2 | 1844 | **1983** | 1199 | 100 | IFLAS g12 | **25%** | 781 |
| Normal | strict | 3 | **3308** | 3174 | 1482 | IFLAS g16 | IFLAS g12 | **0%** | 692 |
| Normal | strict | 4 | **5087** | 4776 | 2275 | IFLAS g16 | IFLAS g8 | **0%** | 1068 |
| Normal | optimistic | 1 | 3071 | **3252** | 2714 | 1706 | IFLAS g16 | **25%** | 3297 |
| Normal | optimistic | 2 | 7764 | **8474** | 7435 | 5143 | 246 | **25%** | 8379 |
| Normal | optimistic | 3 | 8770 | 10417 | **11226** | 7107 | IFLAS g16 | **50%** | 9385 |
| Normal | optimistic | 4 | 10102 | 11750 | **13415** | 9649 | IFLAS g12 | **50%** | 10718 |
| Slow | strict | 1 | IFLAS g12 | IFLAS g12 | IFLAS g16 | IFLAS g16 | IFLAS g16 | (hepsi iflas) | IFLAS g16 |
| Slow | strict | 2 | IFLAS g12 | IFLAS g12 | IFLAS g12 | IFLAS g12 | IFLAS g12 | (hepsi iflas) | IFLAS g12 |
| Slow | strict | 3 | IFLAS g12 | IFLAS g12 | IFLAS g12 | IFLAS g12 | IFLAS g8 | (hepsi iflas) | IFLAS g12 |
| Slow | strict | 4 | IFLAS g12 | IFLAS g12 | IFLAS g12 | IFLAS g8 | IFLAS g8 | (hepsi iflas) | IFLAS g12 |
| Slow | optimistic | 1 | **1097** | 976 | 769 | 344 | IFLAS g16 | **0%** | 1052 |
| Slow | optimistic | 2 | 2804 | **2827** | 2301 | 1301 | IFLAS g16 | **25%** | 2879 |
| Slow | optimistic | 3 | 2673 | **4072** | 3817 | 1838 | IFLAS g12 | **25%** | 3242 |
| Slow | optimistic | 4 | 3121 | 4520 | **5965** | 3591 | IFLAS g12 | **50%** | 3690 |

**Sonuç — TASARIM KUSURU (işaretlendi).** Telefon PlateUp planı §D'de "tempo hızlandırma kolu"
olarak onaylanmıştı; ölçüm bunun tersini söylüyor:
- **u=100% ("her fırsatta çevir") 16 hücrenin 15'inde İFLAS** (tek istisna Normal/opt P2: 246 TL,
  yani −%97). Mekaniği "amacına uygun" kullanan oyuncu oyunu kaybediyor.
- **STRICT bantta optimal oran %0 (3/4 hücre) veya %25 (1/4).** Yani zorlanan oyuncu için doğru
  strateji "telefonu hiç kullanma".
- OPTIMISTIC bantta optimum %25-50 ve gerçek fayda **20 TL değil prestij**: çağrı başına net
  değer Normal/opt P3-P4'te **+41 TL** (nominal ödülün 2 katı) — çünkü `callPrestigeReward=0.4`
  prestij tier'ını (`prestigePerBonus=8` → +5 TL/kutu) yukarı itiyor. Strict'te aynı ölçüm
  **−5.9 / +3.7 / −3.4 / −7.8 TL** (P1-P4).
- Fonksiyon tepe noktalı (0-50 arası artıp 75-100'de uçurumdan düşen) ve tepe yeri banda göre
  kayıyor → oyuncuya öğretilemez. Kök neden Round 1'de teşhis edildi: `SkipTime` GERÇEK saniye
  yakıyor, üretim penceresini kısaltıyor.
- `phoneCooldownSeconds` 20→3 düşüşü sınırlayıcıyı kaldırdı; artık u=100 gerçekten erişilebilir.

## 2. Normal/strict P1'in "146 TL marjı" — SAHTE bir ince marj

| kira günü | kira | kira öncesi kasa | ödendi | kalan |
|---|---|---|---|---|
| 4 | 500 | 914 | 500 | 414 |
| 8 | 600 | 887 | 600 | 287 |
| 12 | 720 | 921 | 720 | 201 |
| 16 | 864 | 1010 | 864 | **146** |

146 TL = tam olarak 1.00 günlük net gelir (ort. 145.6). AMA **grace (kira affı) hiç
kullanılmamış** — `DayCycleManager.cs:616-624` grace'i "eldeki nakdin %80'i, bir kez" olarak
uyguluyor, açığın büyüklüğünden BAĞIMSIZ. Bu yüzden kullanılmamış grace, **sınırsız büyüklükte
tek bir açığı** emen bir tampon:

| sarsıntı | sonKasa | kazandı | grace |
|---|---|---|---|
| taban | 146 | EVET | kullanılmadı |
| üretim −%2 / −%5 / −%10 | 121 / 84 / 22 | EVET | kullanılmadı |
| üretim −%12 / −%20 / −%30 | 172 / 145 / 151 | EVET | g16'da yandı |
| üretim −%40 | IFLAS g16 | HAYIR | g16 yetmedi |
| gün 8 gelirinin TAMAMI kayıp | 8 | EVET | kullanılmadı |
| gün 7+8 gelirinin tamamı kayıp | 79 | EVET | g12'de yandı |
| gün 7+8 kayıp + üretim −%10 | 19 | EVET | g12'de yandı |
| quest geliri hiç yok (16 gün) | 92 | EVET | kullanılmadı |
| ödül 50→48 TL/kutu | 86 | EVET | kullanılmadı |
| kira tabanı +%10 | 162 | EVET | g16'da yandı |

**Verdict: marj ince GÖRÜNÜYOR ama kırılgan DEĞİL.** Gerçek kırılma eşiği üretimde **−%40**
civarı; tek günlük tam gelir kaybını, quest'siz oyunu, +%10 kira şokunu kaldırıyor.
Duyarlılık eğimi: üretimde her −%1 ≈ **−12.4 TL** son kasa (grace yanmadan önce).

İki uyarı:
1. **146 rakamının kendisi telefon varsayımının ürünü.** `ASSUMED4.phoneUseRate.strict=0.60`
   ile 146; u=0 ile **768**. Yani P1 Normal/strict aslında rahat; darlığı üreten şey telefon
   tuzağı (bkz. §1), kira değil. Round 3 bu hücreye bakıp kira indirmemeli.
2. **Grace tampon olarak sayılmasının bedeli sıfır değil:** `leveraged_rent` / `all_in` /
   `Kelle Koltukta` perkleri `gracePaymentPercent=0` yapıyor (`PerkEffect.cs:318,336`). Bu
   perkleri alan P1 oyuncusunun tamponu tamamen kalkar — o zaman 146 TL gerçekten 1 günlük
   marj olur. Round 4'te bu perklerin fiyatı bu bilgiyle okunmalı.

## 3. Slow/strict gün-12 toplu ölümü — TAM NAKİT AKIŞI

Kira serisi (`base × 1.20^ödeme`): **3. ödeme ×1.44** (1.728 4. ödemedir, gün 16).

| P | g4 (×1.00) | g8 (×1.20) | g12 (×1.44) | g12 açığı |
|---|---|---|---|---|
| 1 | 800 → öde 500 → 300 | 635 → öde 600 → **35** | 455 < 720 → **GRACE** (364 öde) → 91 | −265 |
| 2 | 1201 → öde 1000 → 201 | 960 < 1200 → **GRACE** (768) → 192 | 1137 < 1440 → **İFLAS** | −303 |
| 3 | 1537 → öde 1450 → **87** | 1130 < 1740 → **GRACE** (904) → 226 | 1569 < 2088 → **İFLAS** | −519 |
| 4 | 1909 → öde 1800 → 109 | 1435 < 2160 → **GRACE** (1148) → 287 | 1997 < 2592 → **İFLAS** | −595 |

(P1 gün 12'de grace'i yakıyor, gün 16'da 567 < 864 ile ölüyor.)

**Teşhis: ölüm gün 12'de görünüyor ama gün 4'te başlıyor.** P2/P3/P4'te ilk kira zaten kasayı
sıfıra yakın süpürüyor (87-201 TL kalıyor), gün 8'de grace yanıyor, gün 12'de kurtaracak bir
şey kalmıyor. 4 günlük net gelir vs o dönemin kirası:

| P | net g9-12 | kira g12 | kira/gelir |
|---|---|---|---|
| 1 | 420 | 720 | 1.71 |
| 2 | 946 | 1440 | 1.52 |
| 3 | 1343 | 2088 | 1.55 |
| 4 | 1710 | 2592 | 1.52 |

Karşılaştırma — Normal/strict'te aynı oran 0.88-1.27 arasında ve **düşerek** gidiyor
(g1-4: 1.09-1.21 → g13-16: 0.88-1.09). Yani eğri Normal bantta sağlıklı.

**Bu bir EĞRİ değil SEVİYE sorunu (Round 3 için kritik girdi):**
- `rentGrowthMultiplier` taraması (Slow/strict): 1.20 / 1.15 / **1.10 → hâlâ P2-P4 İFLAS g12**.
  Ancak 1.05'te kısmen, 1.00'de tamamen kurtuluyor (kasa 31-488 TL, yine de kıl payı).
- Taban kira çarpanı taraması: ×0.9 kurtarmıyor, **×0.8** sınırda (P1 115, P2 2, P3/P4 hâlâ
  g16'da ölüyor), **×0.7** hepsini kurtarıyor.
- Ödül çarpanı taraması: ×1.6 kısmi, **×1.8** hepsini kurtarıyor.
→ Slow/strict açığı ≈ **%25-30'luk bir gelir/gider seviye açığı**. Tek başına
`rentGrowthMultiplier` ile kapanmaz; Round 3 bunu "eğriyi yatırma" olarak çözmeye çalışmamalı.

## 4. STRICT bantta biriken (teslim edilemeyen) ürün ve kayıp TL

`birikenÜrün = ürünArzı − kutu` (mekanik işleme tavanı bağlayıcı olduğu için).
**Her iki senaryoda da 16/16 gün mekanik-bağlı** (kota HİÇBİR gün bağlayıcı değil).

| senaryo | bant | P | gün | toplam biriken ürün | gün başı ort. | toplam kayıp TL | gerçekleşen küm. net | kayıp/gerçekleşen |
|---|---|---|---|---|---|---|---|---|
| Normal | strict | 1 | 16 | 63.4 | 3.96 | 4 463 | 2 313 | 1.93 |
| Normal | strict | 2 | 16 | 122.1 | 7.63 | 11 150 | 5 516 | 2.02 |
| Normal | strict | 3 | 16 | 128.7 | 8.04 | 14 162 | 7 754 | 1.83 |
| Normal | strict | 4 | 16 | 118.8 | 7.42 | 15 250 | 9 866 | 1.55 |
| Slow | strict | 1 | 16 | 44.6 | 2.79 | 2 880 | 1 530 | 1.88 |
| Slow | strict | 2 | 12 | 56.4 | 4.70 | 4 177 | 2 303 | 1.81 |
| Slow | strict | 3 | 12 | 72.6 | 6.05 | 6 844 | 3 198 | 2.14 |
| Slow | strict | 4 | 12 | 67.1 | 5.59 | 7 534 | 4 079 | 1.85 |

Kontrol (OPTIMISTIC): Normal P1/P4 ve Slow P4'te birikme **0**; yalnız Slow/opt P1'de 25.2
ürün / 1 736 TL. Yani birikme strict banda özgü.

**Dönüşüm oranı (kota tasarımının gizli varsayımı):**

| bant | kutu/kota (P1→P4) |
|---|---|
| Normal/strict | 0.31 / 0.33 / 0.41 / 0.47 (ort **0.38**) |
| Slow/strict | 0.20 / 0.21 / 0.26 / 0.31 (ort **0.245**) |
| Normal/optimistic | 1.23 / 1.23 / 1.21 / 1.21 |
| Slow/optimistic | 0.90 / 0.92 / 0.97 / 0.97 |

`rewardPerBoxByPlayerCount={50,55,70,88}` 2026-08-29'da "kota müşterisi ≈ 1 kutu" premisiyle
türetilmişti (bkz. [[plateup_customer_quota_2026-08-29]]). Ölçüm: bu premis yalnızca OPTIMISTIC
bantta doğru. STRICT bantta kota müşterisinin yalnız **%25-47'si** paraya dönüşüyor →
kota-tabanlı ödül türetmesi strict bandı sistematik olarak **~2.5-4 kat** eksik fonluyor.
Kayıp TL rakamları "kota tasarımının vaat ettiği ama mekaniğin teslim edemediği gelir"in
üst sınırıdır (tırlar da doluysa gerçekte teslim edilemezdi) — mutlak kayıp değil, **tasarım
niyeti ile mekanik gerçeklik arasındaki açık**.

Ek: Round 1'in not düştüğü DisplayTable dolma → `HandleFailedInteraction` kanalı hâlâ
modellenmiyor; günde 3-8 ürün birikiyorsa bu kanal gerçek oyunda AKTİF → v4 hâlâ iyimser.

## 5. YENİ BULGU — grace "fakir kal" exploiti

`DayCycleManager.cs:616-624`: grace, açığa değil **eldeki nakde** oranlı (%80) ve ödenmiş
sayılıyor (`_rentPaymentCount++`, yani kira çarpanı da ilerliyor). Dolayısıyla kira gününde
nakdi kiradan AZ tutmak (hemen öncesinde upgrade/perk almak) tam ödemeden ucuz.

Kazanç formülü: `≈ 0.20 × o günkü kira` (grace daha önce yanmamışsa, tek kullanım).

| P | gün 16 kirası | grace'i 16'ya saklamanın değeri |
|---|---|---|
| 1 | 864 | +173 TL |
| 2 | 1 728 | +346 TL |
| 3 | 2 506 | +501 TL |
| 4 | 3 110 | +622 TL |

Yan etki: analizde **monotonluk kırılmaları** buradan geliyor — P1'de "yanlış teslim ×1.5"
son kasayı 146→170'e, "üretim −%12" 22→172'ye **yükseltiyor**, çünkü kötüleşme grace'i
tetikliyor. Dengeleme turlarında "daha kötü girdi = daha iyi sonuç" görülürse önce grace
zamanlamasına bakılmalı. (Round 10 için not: exploit'in ucuz düzeltmesi grace'i
`min(%80×nakit, kira)` yerine **`kira − sabit indirim`** ya da "yalnız ilk 2 kira" gibi
kısıtlamak olabilir; karar Round 10'da.)

## 6. Zayıf bant haritası — özet

| bant | durum |
|---|---|
| Normal/optimistic (4 hücre) | SAĞLIKLI, geniş marj (3.1k-13.4k) |
| Normal/strict (4 hücre) | u≤%25 oynanırsa SAĞLIKLI (768-5087); varsayılan %60 telefonla P1 146'ya iniyor — **telefon kaynaklı, kira kaynaklı değil** |
| Slow/optimistic (4 hücre) | SAĞLIKLI ama dar (1.1k-6.0k); P1 en zayıf üye |
| Slow/strict (4 hücre) | **KIRIK** — telefon oranından BAĞIMSIZ, denenen 5 oranın 5'inde de iflas; seviye açığı ~%25-30 |

Yeni riskli hücre yok; bugünkü reward-lever + kota değişikliği Normal bandı bozmamış.

İlgili: [[economy_full_balance_round1_2026-08-30]], [[plateup_customer_quota_2026-08-29]],
[[rent_growth_1_35_deficit_2026-08-20]], [[phone_cooldown_perk_event_stacking_2026-08-29]],
[[money_comes_only_from_trucks]]
