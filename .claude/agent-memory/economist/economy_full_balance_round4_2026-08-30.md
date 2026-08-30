---
name: economy-full-balance-round4-2026-08-30
description: Round 4 (Sonnet) - perk/upgrade fiyat-guc tutarliligi; Ek Hangar (200 TL) 9.21x deger/maliyet oraniyla asiri ucuz VE optimistic bantta 0 deger (bant-bagimli asirilik); Gorev Kademesi L2 (100 TL) olculebilir deger SIFIR (Hard quest EV dusuk kaliyor); patient_customers STRICT bantta NEGATIF deger (gun-kisaltma yan etkisi); 6 backbone/relic draft'tan TAMAMEN disabledInDraft=1 (satin alinamaz); cheap_rent + long_queue STALE-BASELINE kod hatasi (formul artik gecersiz taban varsayiyor)
metadata:
  type: project
---

# Round 4 — Perk/upgrade fiyat-güç tutarlılığı (2026-08-30, Sonnet)

Takip: `plans/economy-full-balance-2026-08-30.md`. Kod DEĞİŞTİRİLMEDİ. Ölçüm `runFullSim`
(sim.js v4.0) ile yapıldı; `opts`'ta olmayan alanlar (bonusPerTier, customerServedPrestigeBonus,
hangarStayByPlayerCount, rentScaledMultiplier, penaltyPerBox, maxQueueSize, serviceCycleSeconds,
realDurationInSeconds) için modülün **export ettiği `SRC4`/`ASSUMED` nesneleri doğrudan mutate
edilip geri alındı** (aynı referans, iç fonksiyonlar da görüyor — doğrulanmış çalışma yöntemi,
kalıcı değişiklik yok, script scratchpad'de). Analiz scripti:
`.../scratchpad/round4_perk_value.js` (kalıcı değil, tekrarlanabilir).

## 0. KRİTİK METODOLOJİ NOTU — hangi rakamlar kullanıldı

**Bu round CANLI KOD değerlerini kullandı, Round 3'ün önerisini DEĞİL:**
`baseRentByPlayerCount = {500, 1000, 1450, 1800}`, `rentGrowthMultiplier = 1.20`,
`rewardPerBoxByPlayerCount = {50, 55, 70, 88}` (Round 3'ün `{290,650,1140,1630}` önerisi
henüz koda uygulanmadı — bkz. plan dosyası). Tüm perk değerleri bu CANLI tabanla ölçüldü.
Round 3'ün önerisi uygulanırsa mutlak TL değerleri değişir ama **göreli sıralama** büyük
ölçüde korunur çünkü çoğu perk kira tabanına değil kutu/prestij mekaniğine dokunuyor
(istisna: `cheap_rent`/`leveraged_rent`, ayrıca test edildi, §4).

Birincil ölçüm bandı: **Normal/strict** (mainstream, sağlıklı bant — Round 1-3 notlarına göre).
Değer = `finalCash(perk MAX seviye)` − `finalCash(perk YOK)`, P1-P4 ortalaması. Maliyet =
tüm seviyelerin toplam TL'si (asset `baseCost`+`costStep×(seviye-1)` toplamı).

## 1. Envanter — TAM tablo (26 giriş, sahne `The Main Office.unity:27015-27530`)

### 1a. Draft'ta AKTİF (satın alınabilir, `disabledInDraft: 0`) — 19 öğe

| Ad | effectId/tip | maxLv | toplam maliyet | Etki |
|---|---|---|---|---|
| Geniş Ambar | backbone (kind0) | 2 | 150 | +1 raf (levelObjects, sim ile ÖLÇÜLEMEZ) |
| Paketleme İstasyonu | backbone | 1 | 150 | +1 paketleme masası (`packingTables` 1→2) |
| Ek Hangar | backbone | 1 | 200 | +1 hangar (`numHangars` 1→2) |
| Görev Kademesi | backbone | 2 | 180 | quest tier 0→1→2 |
| Ucuz Kira | cheap_rent | 3 | 480 | `rentGrowthMultiplier` 1.15→1.06 (MUTLAK, taban 1.15 varsayıyor) |
| Prestij Simsarı | prestige_broker | 2 | 275 | `bonusPerTier` 5→6 |
| Prestij Ustası | prestige_master | 2 | 375 | `customerServedPrestigeBonus` 0.4→0.64 |
| Hızlı Hangar | fast_hangar | 1 | 120 | `hangarStayDuration` ×1.30 |
| Enerjik Ekip | energetic_crew | 1 | 100 | stamina regen →2.5 (NON-EKONOMIK) |
| Çevik Ekip | agile_crew | 1 | 180 | hareket hızı +%15 (NON-EKONOMIK) |
| Sabırlı Müşteriler | patient_customers | 1 | 120 | interaction time ×0.6 |
| Kumarbaz Kasası | gambler_case | 1 | 350 | ödül +%30, ceza +%55 |
| Telefon Hattı | phone_line | 1 | 160 | cooldown −10sn (sim'de ÖLÇÜLEMEZ, bkz §5) |
| Mesai Saati | overtime | 1 | 300 | gün uzunluğu ×1.125 |
| Kaldıraçlı Kira | leveraged_rent | 1 | 300 | kira ×0.75, grace=0 |
| Yüksek Volatilite | high_volatility | 1 | 320 | ödül ±%35 RNG, ort ×1.15 |
| Acil Fren | emergency_brake | 1 | 250 | tek-kullanımlık iflas sigortası (ÖLÇÜLEMEZ, bkz §5) |
| Kelle Koltukta | all_in | 1 | 320 | ödül +%25, grace=0 |
| Toplu Alım | bulk_buy | 1 | 80 | sonraki 1 kart −%50 |

### 1b. Draft'tan TAMAMEN ÇIKARILMIŞ (`disabledInDraft: 1`) — satın ALINAMIYOR, fiyat/güç tartışması ANLAMSIZ

| Ad | effectId/tip | Not |
|---|---|---|
| Geniş Kuyruk | backbone | `starterValue=3` — level 0'da bile `InitializeQueueBaseValue` ile `maxQueueSize=3` yazıyor (§6) |
| Sağlam Kasa | backbone | AYRICA kod-ölü: `ApplyMoneyUpgrade` NO-OP (2026-07-20 kararı, satılsa bile hiçbir şey yapmaz) |
| Dinç Ekip | backbone | stamina regen (Enerjik Ekip ile aynı hedef, ama alınamaz — eski "duplike" endişesi artık MOOT) |
| Su Sebili | backbone | contentText "Water Dispenser added" |
| Güler Yüz | backbone | müşteri bekleme süresi artışı |
| Uzun Kuyruk | long_queue | `maxQueueSize = DEFAULT_QUEUE_SIZE(2)+2=4` (STALE, §6) |

**Not:** `UpgradeDefinition.disabledInDraft` alanı `DraftPool.IsEligible`'ı kapatıyor (bkz.
`UpgradePanel.cs`), yani bu 6 öğe oyuncuya hiç TEKLİF EDİLMİYOR. Önceki turların hafıza notu
("Stamina↔energetic + Queue↔long_queue DUPLİKE", `upgrade_legacy_backbones.md`) bu round'da
**KISMEN ÇÖZÜLMÜŞ bulundu** — duplike olan taraf artık draft dışı, çakışma fiilen yok. Ama
kod hâlâ oradaki formüller STALE (aşağıda), yeniden aktive edilirse hemen bozuk olur.

## 2. Değer/maliyet oranı sıralaması (Normal/strict, P1-4 ort., CANLI taban)

| Perk | Maliyet | Değer (ort.) | Oran | Not |
|---|---|---|---|---|
| **Ek Hangar** | 200 | **1842** | **9.21** | ⚠️ AŞIRI — ama bant-bağımlı: optimistic'te **0** (§3) |
| Paketleme İstasyonu | 150 | 416 | 2.78 | |
| Kelle Koltukta (all_in) | 320 | 847 | 2.65 | Slow/strict'te riskli, bkz Round 3 §7 |
| Ucuz Kira (cheap_rent, L3) | 480 | 1180 | 2.46 | Slow/strict'i KURTARMIYOR (§4) |
| Kumarbaz Kasası (gambler_case) | 350 | 858 | 2.45 | high_volatility ile süperlineer yığılıyor (§7) |
| Mesai Saati (overtime) | 300 | 682 | 2.27 | |
| Hızlı Hangar (fast_hangar) | 120 | 238 | 1.98 | |
| Yüksek Volatilite (high_volatility) | 320 | 499 | 1.56 | EV-proxy (gerçek RNG varyansı sim'de yok) |
| Toplu Alım (bulk_buy) | 80 | ~93 | 1.16 | ortalama kart fiyatı 185.5 TL varsayımıyla |
| Prestij Simsarı (prestige_broker) | 275 | 312 | 1.14 | |
| Prestij Ustası (prestige_master) | 375 | 329 | 0.88 | en zayıf POZİTİF |
| **Görev Kademesi (L1 TEK)** | 80 | 93 | **1.16** | L1 tüm değeri taşıyor |
| **Görev Kademesi (L1+L2 max)** | 180 | 93 | **0.52** | **L2 (100 TL) SIFIR ek değer (§8)** |
| **Sabırlı Müşteriler (patient_customers)** | 120 | **−86** | **−0.72** | **NEGATİF — STRICT bantta zarar veriyor (§9)** |
| Telefon Hattı (phone_line) | 160 | ÖLÇÜLEMEZ | — | sim `useRate` telefonu cooldown'dan bağımsız modelliyor (§5) |
| Acil Fren (emergency_brake) | 250 | ÖLÇÜLEMEZ | — | tek-kullanımlık sigorta, EV oyuncunun iflasa yakınlığına bağlı (§5) |
| Geniş Ambar | 150 | ÖLÇÜLEMEZ | — | raf/DisplayTable taşması sim'de yok (bilinen model açığı) |
| Enerjik Ekip / Çevik Ekip | 100 / 180 | N/A | N/A | tasarım gereği NON-EKONOMİK (PerkEffect.cs yorumu) |

## 3. Ek Hangar — bant-bağımlı aşırılık (en kritik bulgu)

`numHangars`: 1→2, `TruckSpawner.UpdateActiveHangars` ile doğrulandı (gerçek 2. hangar spawn
noktası aktifleşiyor, kozmetik değil). STRICT'te mekanik hangar-döngü tavanı bağlayıcı olduğu
için (Round 1 §6) hangar sayısını ikiye katlamak trucksPerDay'i neredeyse lineer ikiye katlıyor:

| P | Normal/strict base | +1 hangar | fark | Normal/optimistic base | +1 hangar | fark |
|---|---|---|---|---|---|---|
| 1 | 146 | 439 | +293 | 3297 | 3297 | **0** |
| 2 | 781 | 1743 | +962 | 8379 | 8379 | **0** |
| 3 | 692 | 2895 | +2203 | 9385 | 9385 | **0** |
| 4 | 1068 | 4978 | +3910 | 10718 | 10718 | **0** |

**OPTIMISTIC bantta değer TAM SIFIR** (üretim zaten hangar-tavanı değil işçilik-tavanı ile
sınırlı — Round 1'in "kota=tavan STRICT'te yanlış" bulgusunun aynası: burada OPTIMISTIC'te
hangar hiç bağlayıcı değil). Yani bu perkin gerçek değeri **tamamen oyuncunun beceri bandına
bağlı** — beceriksiz/zorlanan takım için oyunu kurtaran en güçlü satın alma, usta takım için
tamamen israf. 200 TL fiyat, STRICT bandındaki 9.21x oranla **ekonominin en dengesiz kalemi**.
Fiyat artırımı ÖNERİLMİYOR burada (Round 10'a bırakıldı) ama bu asimetri kayda geçirildi.

## 4. Kira perkleri — Round 3'ün yeni önerisiyle etkileşim

`graceAvailable=false` (leveraged_rent/all_in'in gracePaymentPercent=0 karşılığı) + CANLI kira
ile Slow/strict testi:

| P | base (Slow/strict) | cheap_rent L3 (1.06) | leveraged_rent (×0.75 + grace=0) |
|---|---|---|---|
| 1 | IFLAS g16 | IFLAS g16 (değişmedi) | **18 (KAZANDI, cliff kenarı)** |
| 2 | IFLAS g12 | IFLAS g16 (ertelendi, hâlâ ölüyor) | **32 (KAZANDI, cliff kenarı)** |
| 3 | IFLAS g12 | IFLAS g12 (değişmedi) | IFLAS g12 |
| 4 | IFLAS g12 | IFLAS g12 (değişmedi) | IFLAS g16 (ertelendi) |

**Şaşırtıcı bulgu: `leveraged_rent` (grace'i SİLEN perk) Slow/strict'in EN KIRIK persona'sında
P1/P2'yi kazandırıyor** — grace zaten bu persona'da yalnız BİR kez ve yetersiz miktarda işe
yarıyor (Round 2 §3), oysa `rentScaledMultiplier=0.75` her 4 kira ödemesinde de %25 kalıcı
tasarruf sağlıyor; kümülatif etki grace'in tek seferlik faydasını aşıyor. `cheap_rent` ise
(yalnız büyüme ORANINI düşürüyor, taban rakamı değil) Round 2'nin "seviye açığı" tespitini
DOĞRULUYOR — Slow/strict'i genelde KURTARMIYOR (P3/P4 hiç etkilenmiyor bile). **Round 3'ün
girdisi güncellendi: `leveraged_rent`in Slow/strict'teki riski önceki turun düşündüğünden DAHA
KARIŞIK — P1/P2 için aslında NET FAYDALI (marjinal de olsa), yalnız P3/P4 için hâlâ zararsız-
nötr (zaten kaybediyorlardı, perk almasalar da kaybediyorlar).**

## 5. Sim ile ÖLÇÜLEMEYEN 4 perk (yapısal model açığı, HATA değil)

- **`phone_line`**: `fullCustomerDay`'de `phoneCalls = Q × phoneUseRate` — cooldown saniyesi
  formüle hiç girmiyor (üretim-tavanlı bir "kaç çağrı/saat" limiti sim'de yok). Perk gerçek
  oyunda "saatte kaç kez arayabilirsin" limitini gevşetiyor ama sim bunu zaten dışsal bir
  oran (`ASSUMED4.phoneUseRate`) olarak model dışı tutuyor. **Round 7'nin konusu, burada
  sadece "ölçülemez" diye işaretlendi, çözüm önerilmedi (görev tanımı gereği).**
- **`emergency_brake`**: tek-kullanımlık iflas sigortası, EV'si "oyuncunun kaç kez neredeyse
  iflas ettiğine" bağlı — `runFullSim`'deikinci bir grace mekanizması yok, sahte bir sayı
  üretmek yanıltıcı olur. Nitel değerlendirme: Round 2'nin "−%40 üretim = P1 Normal/strict
  kırılma eşiği" bulgusuyla okunmalı — bu eşiğe yakın oynayan bir takım için 250 TL makul bir
  sigorta fiyatı, hiç zorlanmayan takım için tamamen israf (Ek Hangar ile aynı bant-bağımlılık
  deseni, ama tersine: garanti değil olasılıksal).
- **`Geniş Ambar`** (+1 raf): raf kapasitesi/`DisplayTable` taşması sim'de hiç modellenmiyor
  (Round 1 §7'nin bildiği açık — "v4 bile İYİMSER" notu). STRICT bantta biriken ürün Round 2
  §4'te 2.8-8.0/gün ölçülmüştü; bu perkin gerçek değeri o taşmayı ne kadar geciktirdiğine bağlı,
  ama sim taşma cezasını hiç saymıyor → ölçüm mümkün değil.
- **Enerjik Ekip / Çevik Ekip**: `PerkEffect.cs` yorumunda zaten "ekonomik değer değil" diye
  işaretli (stamina/hareket hızı, mevcut mekaniğe bağlanış). Value/cost oranına DAHİL EDİLMEDİ.

## 6. STALE-BASELINE kod bug'ları (2 adet, aynı kalıp)

**a) `cheap_rent`** (`PerkEffect.cs:194`): `rentGrowthMultiplier = 1.15f - 0.03f*level`.
Formül **1.15 sabit taban** varsayıyor (muhtemelen FAZ2 döneminden kalma — o zamanki taban
1.15'ti). Canlı taban artık **1.20** (`GameEconomySettings.cs:24`, 2026-08-20 kararı).
Sonuç: L1 alan oyuncu `1.20→1.12` görüyor (−0.08), tasarım niyeti yalnızca **−0.03**'tü.
Perk niyet edilenden **~2.7x daha güçlü** çalışıyor (bu round'un ölçtüğü 2.46x değer/maliyet
oranının BİR KISMI bu kazadan geliyor — "doğru" taban (1.20) ile yazılsaydı perk zayıf kalırdı).

**b) `long_queue`** (`PerkEffect.cs:261`): `maxQueueSize = DEFAULT_QUEUE_SIZE(2) + 2 = 4`.
Kod sabiti `DEFAULT_QUEUE_SIZE=2`'yi taban alıyor, ama gerçek oyun-başı taban (disabled olsa
bile hâlâ çalışan `InitializeQueueBaseValue` → "Geniş Kuyruk" backbone'unun `starterValue=3`'ü
yazması yüzünden) **3**. Perk aktive edilseydi net kazanç reklamdaki "+2" değil **+1** (3→4)
olurdu. **Şu an `disabledInDraft=1` olduğu için ZARARSIZ (satın alınamıyor) ama gameplay
departmanı bu perki yeniden aktive ederse formül YANLIŞ sonuç verir — Round 10'a not düşüldü.**

Desen: her iki bug da **"formül yazıldığı andaki tabanı MUTLAK sabit olarak gömmüş, taban
sonradan değiştiğinde formül güncellenmemiş"** — aynı sınıf hata. Gelecekte yeni bir taban
değişikliği (örn. Round 3'ün kira önerisi uygulanırsa) benzer bir perk formülünü bozabilir;
uygulama turunda (Round 10) TÜM mutlak-atama perk formülleri taban değişikliğine karşı
yeniden kontrol edilmeli.

## 7. `gambler_case` + `high_volatility` stacking — doğrulandı (Round 2'nin notu KAPANDI)

Reward çarpanları çarpımsal: 1.30 × 1.15 = **1.495** (+%49.5, Round 2'nin tahmin ettiği
"+%49"le birebir eşleşiyor). Normal/strict'te ayrı ayrı toplam değer (407+977+1659+2384,
P1-4) ile YIĞILMIŞ ölçüm (471+1066+1831+2714) arasında **~+10-16% süperlineer sinerji**
(penaltyPerBox artışının payı da var). Kombine maliyet 670 TL, ortalama oran **2.27x** —
bireysel oranlarla (2.45/1.56) aynı büyüklük mertebesinde, **alarm verici değil** (çarpımsal
iki ödül-perki beklenen şekilde davranıyor). Playtest'te izlenmeli notu KORUNUYOR ama bu
round'da "dengesiz" bulunmadı.

## 8. Görev Kademesi L2 — ölçülebilir değer SIFIR

`questTier=1` ve `questTier=2` ile `runFullSim` **BİREBİR AYNI** `finalCash` üretiyor (P1-4,
Normal/strict): 190/927/785/1156 (her ikisi de). Kanıt: `questDailyDecision`, tier'a göre
filtrelenmiş havuzdan **EV'ye göre en iyi 3'ün ortalamasını** alıyor — Hard-tier quest'lerin
EV'si (düşük tamamlanma olasılığı yüzünden) her zaman mevcut Easy/Medium'un altında kalıyor,
yani üst tier açılınca havuza EKLENİYOR ama hiçbir gün "top-3" seçimine giremiyor. **Sonuç:
Görev Kademesi'nin 2. seviyesi (100 TL'lik dilim, toplam 180 TL'nin) bu modelde HİÇBİR ek
para/prestij üretmiyor** — 1. seviye (80 TL) tüm ölçülebilir değeri tek başına taşıyor.
Bu, Round 8'in bilinen `quest_d2_double_scaling_bug_2026-08-06` bulgusuyla (Hard tier
tamamlanma oranının D2 çifte ölçeklemesiyle çökmesi) TUTARLI — kök neden muhtemelen aynı.
**Round 8'e girdi: Hard-tier EV'sini yükseltmeden Görev Kademesi L2 fiyatı anlamsız kalır.**

## 9. `patient_customers` — STRICT bantta NEGATİF değer (yaklaşık ölçüm, dikkatli okunmalı)

**Yöntem sınırlaması:** `PerkEffect.cs`'in gerçek hedefi `CustomerManager.interactionTimeMultiplier`
alanı — sim.js bu alanı hiç okumuyor (yalnız gün9+ dual-item çarpanını modelliyor). Yaklaşık
ölçüm için `ASSUMED.serviceCycleSeconds['Normal']` ve `serviceLaborSeconds['Normal']` ×0.6
skalalandı (perkin kendi çarpanıyla). **Bu YAKLAŞIK bir vekil, gerçek büyüklük garantisi yok**
ama ortaya çıkan MEKANİZMA gerçek ve yapısal:

| P | base | patient_customers (approx) | fark |
|---|---|---|---|
| 1 | 146 | 38 | −108 |
| 2 | 781 | 701 | −80 |
| 3 | 692 | 626 | −66 |
| 4 | 1068 | 979 | −89 |

Kök neden: STRICT bantta gün kota bitince (kuyruk boşalınca) ERKEN kapanıyor
(`fullCustomerDay`'in `drainSec`/`naturalEndSec` zinciri). Servis hızı arttırılınca kuyruk
daha ÇABUK boşalıyor → gün daha ERKEN bitiyor → tır yükleme penceresi (`truckWinSec`,
`dayActiveSec`'e bağlı) KISALIYOR → para SADECE tırdan geldiği için (`money_comes_only_from_trucks`)
net gelir düşüyor. Yani "müşteriye daha hızlı hizmet ver" mekaniği, STRICT bantta parayı
üreten asıl darboğaza (tır penceresi) hiç dokunmuyor ama günü kısaltarak o pencereyi
KÜÇÜLTÜYOR — **klasik "yanlış darboğazı optimize etme" tuzağı.** 120 TL fiyat, eğer bu
mekanizma gerçek oyunda da geçerliyse, POZİTİF bir perk için ödenen bir bedel değil,
**negatif bir perk için ödenen bir bedel** olabilir. **Bu round'un görevi tespit, çözüm
önerisi değil** — ama bulgu ciddi, gameplay/QA'in gerçek oyunda (Unity playtest, `interactionTimeMultiplier`
gerçekten günü kısaltıyor mu) doğrulaması ÖNERİLİR. Sim yaklaşıklığı yüzünden kesin TL rakamı
verilmiyor, yalnızca YÖN (negatif) ve MEKANİZMA (gün kısalması → tır penceresi küçülmesi)
raporlanıyor.

## 10. Aşırı uçlar özeti (somut sayı önerileri — UYGULANMADI, Round 10'a not)

- **Ek Hangar (200→?)**: STRICT'te 9.21x, OPTIMISTIC'te 0x. Fiyatı STRICT'e göre ayarlamak
  OPTIMISTIC'i anlamsız pahalandırır (zaten sıfır değer). Öneri düşünce tarzı: fiyat
  ARTIRILMAZ, bunun yerine STRICT bandının kendisinin (mekanik hangar tavanı) gevşetilmesi
  düşünülebilir (ayrı bir round konusu, ekonomi değil kapasite tasarımı).
- **Görev Kademesi L2 (100 TL dilim)**: mevcut haliyle **fiyat 0 TL olsa bile** ekonomik fark
  yaratmıyor (quest EV yapısı L2'yi hiç seçmiyor) — asıl düzeltme fiyat değil Round 8'in
  quest EV dengesi.
- **patient_customers (120 TL)**: eğer Unity playtest mekanizmayı doğrularsa fiyat 120→0 veya
  daha düşük bir taban gerekir; şu an POZİTİF bir hizmet önerisi gibi konumlanmış ama STRICT'te
  muhtemelen NEGATİF.
- **prestige_master (375 TL, oran 0.88)**: en zayıf pozitif relic; hafif ucuzlatma (375→300
  civarı) diğer prestij perkiyle (prestige_broker, 275 TL/1.14 oran) daha tutarlı olurdu.

İlgili: [[economy_full_balance_round3_2026-08-30]], [[economy_full_balance_round2_2026-08-30]],
[[economy_full_balance_round1_2026-08-30]], [[quest_d2_double_scaling_bug_2026-08-06]],
[[perk_card_absolute_assignment_conflict]], [[upgrade_legacy_backbones]], [[money_comes_only_from_trucks]]
