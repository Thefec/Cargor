---
name: economy-full-balance-round3-2026-08-30
description: Round 3 (Sonnet, Opus onayi gerekebilir) - kira egrisi SEVIYE fix'i; baseRentByPlayerCount ASIMETRIK {290,650,1140,1630} onerisi (P-bazli kesinti %42/35/21/9) Slow/strict'in 16/16 hucresini kurtariyor, rentGrowthMultiplier=1.20 DOKUNULMADI; leveraged_rent/all_in perki P2-P4'te hala kirilgan (g8->g16 iyilesme, tam guvenli degil)
metadata:
  type: project
---

# Round 3 — Kira eğrisi SEVİYE fix'i (2026-08-30, Sonnet ile yapıldı — Opus onayı önerilir)

Takip: `plans/economy-full-balance-2026-08-30.md`. Yalnız `runFullSim` (sim.js v4.0,
`tools/economy-sim/sim.js`) kullanıldı. Kod DEĞİŞTİRİLMEDİ; analiz scriptleri scratchpad'de
(kalıcı değil). Bu round **Sonnet** ile yürütüldü (Opus'un düşen denemesinin devamı) — kullanıcı
isterse Opus'la ikinci bir gözden geçirme yapılabilir, ama metodoloji ve sim çıktıları
tekrarlanabilir/doğrulanabilir şekilde bu dosyada.

## 0. Devralınan altyapı — düşen ajanın bıraktığı iş KULLANILDI
Önceki (düşen) ajan `runFullSim`'e şu opsiyonel parametreleri eklemiş, sözdizimi sağlam ve
işe yarar bulundu, DEĞİŞTİRİLMEDEN kullanıldı:
- `graceAvailable` (bool, default true): `false` verilince kira affı hiç tetiklenmez —
  `leveraged_rent`/`all_in` perklerinin `gracePaymentPercent=0` yapmasını simüle etmek için
  tam ihtiyaç duyulan anahtar buydu.
- `ledgerMode` (bool, default false): 0'a-kırpma ve iflas kesmesini bypass eden "saf defter"
  modu; bu round'da kullanılmadı (asıl ihtiyaç `graceAvailable` idi), ama gelecekte
  gün-gün nakit akışı teşhisi için hazır duruyor. **Silinmedi, dosyanın sonuna eklenen
  ayrı bir "628 satırlık ledger aracı" YOK** — bahsedilen 628 satır, `git diff --stat`
  ile ölçülen sim.js'in Round1+2+3 boyunca kümülatif COMMIT'SİZ diff'i (SRC4/ASSUMED4/
  runFullSim/CLI blok 18-23 + bu round'un 2 parametresi), tek bir ayrı blok değil.

## 1. Yöntem — Round 2'nin "0.60 varsayılan" hatasından kaçınma

Round 2'nin kendi kira taramasının (`rent_growth_1_35_deficit_2026-08-20.md`'nin izinden
giden ilk sürümü) **×0.7 ile Slow/strict'in kurtulduğu** iddiası, `ASSUMED4.phoneUseRate.strict
= 0.60` varsayılan oranıyla yapılmıştı. Bu round'un görev tanımı bunun YANLIŞ metodoloji
olduğunu doğru tespit etti: gerçek STRICT-bant optimali **%0** (Round 2 tablo §1). Aynı
arama `phoneUseRate=0` (gerçek optimal) ile tekrarlanınca **×0.7 YETMEDİ** — P2 hâlâ
gün-16'da batıyor. Gerçek eşik **×0.68 uniform** (tüm P'lere aynı oran), sınıra çok yakın
(cliff'e 1 adım). **Düzeltme: Round 2'nin ×0.7 tahmini yanlış metodolojiyle bulunmuştu,
gerçek açık biraz daha büyük (~%32-40, P'ye göre değişken — aşağıya bakın).**

## 2. Asıl bulgu — açık P-BAĞIMSIZ değil, P'ye göre KESKİN ŞEKİLDE DEĞİŞİYOR

Her `baseRentByPlayerCount[P-1]`'i TEK BAŞINA (diğerleri sabit) değiştirip Slow/strict'in
o P için hayatta kalması için gereken minimum kesinti tarandı (diğer P'lerle etkileşim YOK —
her P kendi rent index'ini okuyor, sim'de çapraz etkileşim yok, doğrulandı):

| P | taban kira | hayatta-kalma-başlangıcı | seçilen (güvenli marj) | kesinti |
|---|---|---|---|---|
| 1 | 500 | ~370 (marj 84, cliff'e yakın) | **290** | **%42** |
| 2 | 1000 | ~680 (marj 189) | **650** | **%35** |
| 3 | 1450 | ~1150 (marj 139, cliff'e yakın) | **1140** | **%21.4** |
| 4 | 1800 | ~1740 (marj 76, cliff'e yakın) | **1630** | **%9.4** |

**Kesinti ihtiyacı P arttıkça DÜŞÜYOR** (P1 %42 → P4 %9.4). Bu, Round 2'nin "kutu/kota
dönüşüm oranı" bulgusuyla (`economy_full_balance_round2_2026-08-30.md` §4: Slow/strict
kutu/kota P1'de 0.20, P4'te 0.31) DOĞRUDAN TUTARLI — yüksek P'de mekanik-tavan verimliliği
zaten daha iyi, bu yüzden mevcut `rewardPerBoxByPlayerCount={50,55,70,88}` yüksek P'yi
zaten nispeten daha iyi besliyor, kira tarafında daha az düzeltmeye ihtiyaç var.

**Fonksiyon monotonik DEĞİL — yerel çukurlar (cliff) var** (grace/ödeme-çevrimi eşik
etkileri): örn. P1'de kira=320 marj=52 iken kira=330 marj=114 (kesinti AZALTINCA marj
ARTIYOR, ters yönlü lokal anomali). Round 2'nin §5 "grace fakir-kal exploiti" ile aynı
kök neden — kira tam nakit sınırına yakın oturunca hangi ödeme turunda grace'in yanıp
yanmadığı değişiyor. **Seçilen 4 değer, en yakın cliff'ten en az 2 basamak (20-30 TL) uzakta**
(bkz. ham tarama — grid taraması bu dosyanın hazırlığında yapıldı, tekrarlanabilir:
`runFullSim(P, {scenario:'Slow', mode:'strict', phoneUseRate:0, baseRentByPlayerCount:[...]})`
ile `baseRentByPlayerCount[P-1]`'i 10'ar 10'ar tarayın).

## 3. Neden UNIFORM ORANLI KESİNTİ değil ASİMETRİK seçildi

Uniform ×0.60 kesinti de Slow/strict'i kurtarıyordu (marj 160/198/1310/2791) ama Normal/strict
ve Normal/optimistic'i GEREKSİZ YERE şişiriyordu (P4 Normal/strict 5087→8844, +%74).
Asimetrik seçim aynı Slow/strict kurtarmayı çok daha ucuza (P3/P4'te Normal bandı neredeyse
dokunulmamış gibi) yapıyor:

| bant | P | ESKİ | UNIFORM ×0.60 (terk edildi) | ASİMETRİK (öneri) |
|---|---|---|---|---|
| Normal/strict | 4 | 5087 | 8844 (+74%) | **5999 (+18%)** |
| Normal/optimistic | 4 | 13415 | 17172 (+28%) | **14327 (+7%)** |

P1/P2'de asimetrik de uniform kadar (hatta biraz fazla) gevşetiyor çünkü P1/P2'nin gerçek
açığı zaten büyük — bu KAÇINILMAZ, çünkü Normal/Slow/strict/optimistic aynı canlı
`baseRentByPlayerCount[P-1]`'i okuyor (simülasyon kategorisi değil, gerçek oyun ayarı);
tek bir P için farklı "zorluk moduna göre kira" gibi bir mekanik YOK.

## 4. NİHAİ ÖNERİ

```
baseRentByPlayerCount = { 290, 650, 1140, 1630 }   // eski: {500, 1000, 1450, 1800}
rentGrowthMultiplier  = 1.20                        // DEĞİŞMEDİ (2026-08-20 kararı)
rewardPerBoxByPlayerCount = { 50, 55, 70, 88 }      // DEĞİŞMEDİ
```
Dosya: `Assets/NewCss/GameEconomySettings.cs:21` (+ `Assets/Resources/EkonomiAyarlari.asset`
karşılığı varsa orada da).

## 5. 16 hücre — ESKİ vs YENİ (Round 2'nin optimal telefon oranlarıyla)

Kullanılan oranlar (Round 2 §1 tablosundan, SABİT — rent değişikliğiyle "yeniden optimize
edilmiş" oranlar KULLANILMADI, kirletici etkileşimi izole etmek için): Normal/strict
{0,25,0,0}%, Normal/optimistic {25,25,50,50}%, Slow/strict {0,0,0,0}%, Slow/optimistic
{0,25,25,50}%.

| senaryo/bant | P | ESKİ (500/1000/1450/1800) | YENİ (290/650/1140/1630) |
|---|---|---|---|
| Normal/strict | 1 | 768 | 1895 |
| Normal/strict | 2 | 1983 | 3862 |
| Normal/strict | 3 | 3308 | 4972 |
| Normal/strict | 4 | 5087 | 5999 |
| Normal/optimistic | 1 | 3252 | 4379 |
| Normal/optimistic | 2 | 8474 | 10353 |
| Normal/optimistic | 3 | 11226 | 12890 |
| Normal/optimistic | 4 | 13415 | 14327 |
| Slow/strict | 1 | **IFLAS g12** | **213** |
| Slow/strict | 2 | **IFLAS g12** | **211** |
| Slow/strict | 3 | **IFLAS g12** | **366** |
| Slow/strict | 4 | **IFLAS g12** | **531** |
| Slow/optimistic | 1 | 1097 | 2224 |
| Slow/optimistic | 2 | 2827 | 4706 |
| Slow/optimistic | 3 | 4072 | 5736 |
| Slow/optimistic | 4 | 5965 | 6877 |

**Sonuç: Slow/strict'in 4 hücresi de kurtarıldı** (marj 211-531 TL, hiçbiri cliff kenarında
değil — bkz. §2 metodoloji). **Normal/strict, Normal/optimistic, Slow/optimistic hiçbiri
kırılmadı** — hepsi zaten sağlıklıydı, şimdi daha da sağlıklı (P1/P2'de belirgin, P3/P4'te
ılımlı artış).

## 6. 2026-08-20 kararıyla çelişki var mı?

**Kısmen evet, gerekçeli.** `rent_growth_1_35_deficit_2026-08-20.md`: "`baseRentByPlayerCount`
DEĞİŞMEDİ... SLOW-STRICT'i düzeltmeye çalışırken diğer bütün bantları (özellikle optimistic)
anlamsızca kolaylaştırır... bu persona kasıtlı 'en kötü durum' olarak bırakılması önerilir."

Bu round tam olarak o zaman YAPILMAMASI önerilen şeyi yapıyor (`baseRentByPlayerCount`'a
dokunuyor). Nedenleri:
1. **O zaman `rewardPerBoxByPlayerCount` P-bazlı lever YOKTU** (2026-08-29'da eklendi) —
   2026-08-20'de kira dışında hiçbir P-hassas düzeltme aracı olmadığından "tek sabitle
   çözülmez" tespiti o an için doğruydu.
2. **O zamanki ölçüm metodolojisi farklıydı** (eski `runSim`/`runSimPlateUp`, Round 1'de
   "canlı kodu yansıtmıyor" bulunup terk edildi) — açığın büyüklüğü hiç bugünkü kadar net
   ölçülmemişti.
3. **Bu round asimetrik (P-bazlı farklı oranlı) kesinti kullanarak** "diğer bantları anlamsızca
   kolaylaştırma" riskini büyük ölçüde SÖNDÜRDÜ (§3) — P3/P4'te Normal bandı sadece %7-18
   şişiyor, 2026-08-20'nin öngördüğü kadar dramatik değil.
4. `rentGrowthMultiplier=1.20` kararının KENDİSİ bozulmadı — bu round yalnız `baseRent`'e
   dokundu, 2026-08-20'nin ana lever'ı (`rentGrowthMultiplier`) aynı kaldı.

**Kalan gerçek risk:** Slow/strict artık "kazanılamaz" değil ama hâlâ en dar bant (marj
211-531, diğer bantların ~%5-10'u) — 2026-08-20'nin "bu bilinçli en kötü durum olarak
bırakılsın" felsefesi kısmen KORUNDU: Slow/strict hâlâ AÇIKÇA en zor bant, sadece artık
matematiksel olarak imkânsız değil.

## 7. `leveraged_rent` / `all_in` perki değerlendirmesi

`PerkEffect.cs:317-318` (`ApplyLeveragedRent`: `rentScaledMultiplier=0.75` + `gracePaymentPercent=0`)
ve `:336` (`ApplyAllIn`: yalnız `gracePaymentPercent=0`) — ikisi de kira affı tamponunu
KALICI OLARAK KAPATIYOR (`EXCLUSIVE_EFFECT_GROUPS`'ta aynı grupta, ikisi birden alınamaz).

`graceAvailable=false` ile test edildi (Slow/strict, YENİ rent tabanı, phoneUseRate=0):

| P | grace AÇIK (yeni rent) | grace KAPALI (perk senaryosu, yeni rent) | ESKİ rent + grace KAPALI (referans) |
|---|---|---|---|
| 1 | 213 | **213 (değişmedi — grace zaten hiç yanmamış)** | IFLAS g8 |
| 2 | 211 | **IFLAS g16** | IFLAS g8 |
| 3 | 366 | **IFLAS g16** | IFLAS g8 |
| 4 | 531 | **IFLAS g16** | IFLAS g8 |

**Bulgu: perk hâlâ riskli ama önemli ölçüde iyileşti.** ESKİ rentte perk P2-P4'ü gün 8'de
(oyunun yarısında) batırıyordu; YENİ rentte aynı persona gün 16'ya (SON kira ödemesi) kadar
hayatta kalıyor, tam son ödemede kısa düşüyor. P1 için perk artık TAM GÜVENLİ (grace zaten
hiç kullanılmıyor, kaybedecek bir şey yok). P2-P4 için "son gün açığını" da kapatmak
denendi — mümkün ama marjlar 22-52 TL'ye düşüyor (cliff kenarı, güvenilmez) — **bu round
bunu YAPMADI**, çünkü grace tam olarak bu tür kenar durumlar için var (normal oyuncu
grace'i kullanır, sorun değil); yalnız `leveraged_rent`/`all_in` alan oyuncu bu tamponu
BİLEREK feda ediyor. **Öneri: Round 4 (perk fiyat-güç turu) bu perklerin fiyatını "Slow/strict
persona'sında P2-P4 için son-gün riski taşır" notuyla değerlendirsin** — fiyat düşürülmesi
gerekmez ama tooltip/uyarı ("kira affı tamamen iptal olur") netleştirilebilir.

## 8. Metodolojik not (gelecek round'lara)

`runFullSim`'in `graceAvailable=false` parametresi artık STANDART bir "stres testi" aracı:
herhangi bir yeni kira/ödül önerisi hem `graceAvailable=true` (normal oyuncu) hem `=false`
(perk/şanssız-grace-daha-önce-yanmış oyuncu) ile test edilmeli. Bu round'dan önce bu ayrım
yoktu, Round 2'nin "146 TL marjı sahte" bulgusu bunu MANUEL (gün-gün tablo okuyarak) yapmıştı
— artık parametrik olarak tekrarlanabilir.

İlgili: [[economy_full_balance_round2_2026-08-30]], [[economy_full_balance_round1_2026-08-30]],
[[rent_growth_1_35_deficit_2026-08-20]], [[plateup_customer_quota_2026-08-29]]
