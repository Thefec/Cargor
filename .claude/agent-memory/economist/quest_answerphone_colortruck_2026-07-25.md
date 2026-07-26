---
name: quest-answerphone-colortruck-2026-07-25
description: AnswerPhone + CompleteSpecificColorTruck tetikleyicileri baglandi (5->7 quest/tier, 15->21 toplam) - targetCount, odul sablonu karari, limit/EV dogrulamasi, renk-bag KOD-DOGRULANMIS tam 1/3
metadata:
  type: project
---

**Bağlam**: 2026-07-25, [[quest_tier_redesign_2026-07-25]]'in devamı. Müdür iki ölü tetikleyiciyi bağladı
(`PhoneCallManager.AnswerServerRpc` → `QuestTracker.NotifyPhoneAnswered`; `Truck.cs:588` →
`NotifySpecificColorTruckCompleted(_networkRequestedBoxType.Value)` gerçek renkle) ve
`QuestManager.UpdateQuestProgress` renk filtresini artık TİPE GÖRE ayırıyor (CompleteTruck asla
renk kontrolü yapmıyor — soft-lock riski kapandı; sadece CompleteSpecificColorTruck yapıyor). Bu
turda bu 2 yeni tipin targetCount/ödül kararları verildi, her tier 5→7 quest'e çıkıyor (toplam 21).

## AnswerPhone: P-BAĞIMSIZ bir quest tipi (kritik yapısal fark, önceden bilinmiyordu)

Diğer 3 canlı tipin (CompleteTruck/PlaceBoxOnShelf/PackToy) hepsi TAKIM THROUGHPUT'una bağlı
(oyuncu sayısı arttıkça tamamlanma kolaylaşır). AnswerPhone'un fırsat sayısı **oyuncu sayısından
tamamen bağımsız**: `PhoneCallManager` sunucu tarafında saatte 1 zar atıyor (`TryRollRing`, iş
saatleri phoneStartHour=8/phoneEndHour=18 → n=10 zar/gün, GÜN UZUNLUĞUNDAN BAĞIMSIZ çünkü saat
geçişleri gün süresine göre ölçekleniyor ama HER saat mutlaka ziyaret ediliyor, 16 günün hepsinde
sabit n=10). `DifficultyManager`'ın eski oyuncu-sayısı-ölçekli çağrı şansı V3'te no-op
(`SetCallChance` boş stub — legacy compat). **"Medium 2P+" / "Hard 3P+ içerik" bandı AnswerPhone'a
mekanik olarak UYGULANAMAZ** — tier farkı yalnız targetCount ile üretilebilir, P-gradyanı yok.

**Zar parametreleri** (kod+asset doğrulandı, `EkonomiAyarlari.asset` + `PhoneCallManager.cs`):
`phoneRingChancePerHour=0.30`, CUSTOMER SUPPORT günü ×`phoneRingEventMultiplier`(1.5)=0.45,
"Telefon Hattı" perk +`phoneRingPerkBonus`(0.15) additive, ikisi birden=0.60, tavan
`clamp(...,0,0.65)` (`GetEffectiveRingChance`).

**targetCount kararı** (Binomial(n=10,p), node hesaplı, baseline p=0.30 = event/perk YOK gün):

| Tier | target | P(X≥target) baseline | P(X≥target) event/perk (p=0.45) | Not |
|---|---|---|---|---|
| Easy | 2 | %85.1 | %97.7 | Easy bandının (65-85%) tam üst ucu |
| Medium | 3 | %61.7 | %90.0 | Easy-Hard arası, tek sayı olarak en makul orta nokta |
| Hard | 4 | %35.0 | %73.4 | Baseline'da "1P nadir" (20-40%) bandında — P-BAĞIMSIZ olduğu için 4P takım da aynı %35'te kalıyor (diğer Hard quest'lerin "3P+ rahat" vaadini KARŞILAMIYOR, bilinçli kabul edilmeli) |

Gerçekçi günlük tavan: mutlak teorik maksimum=10 (P(X=10) baseline ≈ 0.0006%, imkansıza yakın),
EV=3.0 çağrı/gün baseline — 6.0 üst üste event+perk. Seçilen hedefler (2/3/4) bu aralığın güvenle
altında, hiçbiri "imkansız quest" riski taşımıyor.

**Ödül şablonu kararı: TAM (indirimsiz) tier şablonu KULLANILABİLİR, ayrı/indirimli şablon
GEREKMİYOR.** Gerekçe (EV kanıtı, node, tier reward/penalty EV = [[quest_tier_redesign_2026-07-25]]
§7.2 tablosu): AnswerPhone P-bağımsız olduğu için zayıf takımlar (1P) için THROUGHPUT quest'lerine
göre GÖRECELİ OLARAK GÜÇLÜ (Easy 1P: AnswerPhone EV=15.2 vs kutu-quest 1P EV≈8.8; Medium 1P:
AnswerPhone EV=13.9 vs kutu-quest 1P EV≈1.4) — bir "equalizer" (zayıf takım dengeleyici),
[[hangar_stay_duration_per_player]]'daki P-bazlı güçlendirme felsefesiyle aynı yönde ama farklı
mekanizma. Güçlü takımlar (4P) için AnswerPhone EN İYİ kutu-quest seçeneğiyle Easy'de NEREDEYSE TAM
EŞİT (15.23 vs 15.2 — hedef seçimi bilinçli %85 tavanını hedeflediği için beklenen sonuç), Medium'da
BELİRGİN ŞEKİLDE DÜŞÜK (13.9 vs ~27.4) — yani üst düzey takımlar için "no-brainer" baskınlık YOK
(özellikle Medium/Hard'da). Hard'da baseline EV NEGATİF (-2.4, ekip günlük kabul etmez) → yalnız
event/perk günü (+34.5) pozitife dönüyor, "event-senkron ödül" niteliği taşıyor (CUSTOMER SUPPORT'un
pozitif event teması ile tutarlı, flavor text: "RECEPTION PHONE RINGS 50% MORE OFTEN"). **Tek zayıf
nokta: Easy/4P'de tam eşitlik** (bkz Denge Riskleri) — düşük tutar (~12-25 TL) nedeniyle ayrı şablon
açmaya değmez, playtest sinyaline göre gelecekte hafif indirim (×0.8 Money) opsiyonu açık bırakıldı.

## CompleteSpecificColorTruck: renk-kilit ~1/3 seyreltme + KOD-DOĞRULANMIŞ tam eşit dağılım

`TruckSpawner.cs:537-551 DrawNextBagColor()` — dengeli TORBA (bag) yöntemi: torba boşalınca
`[Yellow,Blue,Red]` eklenip Fisher-Yates karıştırılıyor, `RefillPlannedTruckColors` ile önceden
doldurma. **Sonuç: renk dağılımı YAKLAŞIK değil, HER 3 ARDIŞIK TIRDA GARANTİ tam 1/3-1/3-1/3**
(önceki turun "playtest-doğrulanmamış ~1/3 varsayımı" notu artık KOD-DOĞRULANMIŞ KESİN GERÇEK —
ama bu SADECE tır rengi için geçerli, PlaceBoxOnShelf/PackToy'daki ürün-spawn renk dağılımı hâlâ
ayrı/doğrulanmamış bir varsayım, bkz [[quest_tier_redesign_2026-07-25]]).

**targetCount kararı: Easy=1, Medium=2, Hard=3** (colorless CompleteTruck'la AYNI escalating
pattern — düz-1 yerine bu tercih edildi çünkü düz-1 modelde gün-uzunluğu büyümesi Medium/Hard'ı
Easy'den DAHA KOLAY yapıyordu [tier-sıra ihlali, örn. Hard/4P %65 > Easy/4P %40], escalating 1/2/3
monoton-azalan tamamlanma sağlıyor):

| Tier | day (temsili) | target | P1 | P2 | P3 | P4 |
|---|---|---|---|---|---|---|
| Easy | 1 | 1 | %8 | %40 | %40 | %40 |
| Medium | 8 | 2 | %8 | %20 | %20 | %40 |
| Hard | 12 | 3 | %8 | %20 | %20 | %20 |

**Önemli/dürüst bulgu**: colorless CompleteTruck'ın ulaştığı "Easy rahat %65-85" bandına HİÇBİR
tier'da ulaşılamıyor (en iyisi Easy/4P %40) — renk kilidi + tam-tır-doldurma ikisi birden çarpınca
(throughput zaten seyrek + 1/3 seyreltme) opportunity çok düşük kalıyor. **1P HER ÜÇ TIER'DA DA
SADECE %8** — mevcut "1P hiçbir tier'da sıfırlanmıyor (en kötü %20-40)" ilkesini sayısal olarak
ihlal ediyor. Ancak TEHLİKELİ değil: quest kabulü opsiyonel, düşük tamamlanma → negatif EV →
rasyonel takım zaten kabul etmiyor (Hard/1P best-of-3 havuz EV=0, hiç kabul edilmiyor — "duvar
hissi" yaratmıyor çünkü ceza riski hiç alınmıyor). Ödül şablonu TAM (indirimsiz) KALMALI
(AnswerPhone'un aksine burada gerçek emek + tır-bekleme sürtünmesi var, "no-brainer" riski yok).

## Limit kontrolü: 5→7/tier (15→21 toplam) — DAILY_QUEST_COUNT=3 ve 1-kabul/gün BOZULMUYOR

Node ile best-of-3-rastgele-çekiliş beklenen-EV modeli (7 tipin EV'si hesaplanıp C(7,3)=35
kombinasyon üzerinden ortalama alındı, ESKİ 5'li havuzla AYNI yöntemle karşılaştırıldı — box-quest
tamamlanma oranları bu turda §7.3 bant-tanımından nokta-tahmini/yaklaşık türetildi [önceki turun tam
formülü değil], ama HER İKİ havuzda aynı yaklaşım kullanıldığı için DELTA/ORAN karşılaştırması
sağlam kalıyor, mutlak sayılar yaklaşık):

- Ratio (quest best-of-3 EV / o günün çekirdek geliri `netEarnings`) aralığı: ESKİ %0.00-3.62 →
  YENİ %0.00-4.58. Üst sınır hafifçe (%3.9→%4.58) aşıyor ama TEK hücrede (Easy/1P/gün1) ve mutlak
  TL küçük (+2.68 TL/gün — o günün çekirdek geliri de düşük [240 TL] olduğu için ORAN büyüyor, TL
  küçük kalıyor). Enflasyon riski değil.
- En büyük etkiler HER ZAMAN 1P zayıf hücrelerde POZİTİF (Easy 1P +2.68, Medium 1P +5.34 TL/gün) —
  AnswerPhone'un "equalizer" etkisi best-of-3 seçimine yansıyor. 2P-4P hücrelerde etki KÜÇÜK NEGATİF
  (-0.1..-2.3 TL/gün, havuz büyüyünce güçlü tiplerin çekiliş olasılığı istatistiksel olarak hafif
  seyreliyor — beklenen etki, önemsiz büyüklük).
- Bonus: P(gün içinde ≥1 Hard teklif edilir) pratik olarak DEĞİŞMEDİ (%73.6→%72.6, 5/5/5→7/7/7 pool
  büyürken oran korundu). Tek bir spesifik tipin (örn. AnswerPhone) o gün teklif edilme olasılığı
  DÜŞTÜ (Easy-tek dönem %60→%42.9, Hard açıkken kümülatif havuzda sadece %14.3) — çeşitlilik
  arttıkça herhangi bir TEK tipin görünme sıklığı doğal olarak azalıyor, sorun değil.

**Sonuç: `DAILY_QUEST_COUNT=3` ve günde-1-kabul kod sabitlerine DOKUNMA gerekmiyor.**

## Denge riskleri (bu turun eklediği)

1. **AnswerPhone Easy/4P tam eşitlik** — en iyi kutu-quest'le EV başa baş (15.23 vs 15.2) ama emek
   maliyeti neredeyse sıfır. Düşük tutar (~15-25 TL) nedeniyle düşük öncelik, playtest'te "herkes
   hep telefonu mu seçiyor" sinyali gelirse Easy Money havuzunu hafif indirime (örn ×0.8) almak
   yeterli olur.
2. **AnswerPhone Hard, P-gradyanı YOK** — diğer Hard quest'lerin "3P+ rahat" karakterini
   karşılamıyor, herkes için %35 (event günü hariç). Playtest/UI bunu iletmeli veya kabul edilmeli
   (bilinçli asimetri, [[roguelite_perk_pricing]] emsaliyle aynı kategori — ama YÖNÜ TERS: orada
   1P dezavantajlıydı, burada TÜM P'ler aynı, sadece "diğer Hard'lara göre 4P dezavantajlı").
3. **CompleteSpecificColorTruck 1P = %8 (her tier)** — mevcut "1P hiçbir tier'da sıfırlanmıyor"
   ilkesini sayısal olarak ihlal ediyor, ama zararsız (opsiyonel kabul + negatif EV kendi kendini
   regüle ediyor). Playtest'e not.
4. **Box-quest tamamlanma oranları bu turda yaklaşık/nokta-tahmini** (önceki turun tam formülü
   değil, sadece §7.3 bant-tanımından türetildi) — limit/EV kontrolü DELTA bazında sağlam ama mutlak
   sayılar yaklaşık, ileri bir turda gerçek formülle re-check önerilir.
5. **sim.js `realDurationInSeconds=160` sahne değerinden (200) ESKİ/FARKLI** (`DayCycleManager.cs:50`
   C# alan-default'u=160 ile eşleşiyor ama `The Main Office.unity` sahne override=200 — Inspector'da
   ayarlanmış). Tüm gün-uzunluğu bağımlı hesaplar (`truckWindowSeconds`, `fullTrucksPerDayEstimate`)
   bu yüzden hafif KÖTÜMSER (gerçek throughput muhtemelen modellenen değerden ~%25 daha yüksek). Bu
   turun sonuçlarını YIKMIYOR (metodoloji tutarlı, mevcut CompleteTruck target'larıyla [1/2/3] aynı
   temelde karşılaştırıldı, ikisi de aynı "kötümser" sim.js'i kullanıyor) ama sim.js↔sahne
   senkronizasyonu ayrı bir bakım turu gerektiriyor (bir sonraki denetimde `ECONOMY.realDurationInSeconds`
   160→200 güncellenmeli).

İlişkili: [[quest_tier_redesign_2026-07-25]] (ana tasarım turu, bu dosya onun DEVAMI),
[[quest_completetruck_color_constraint]] (renk kısıtı kökeni, artık bag-mekaniği ile güçlendirildi),
[[money_comes_only_from_trucks]], [[hangar_stay_duration_per_player]] (P-bazlı güçlendirme emsali),
[[upgrade_roi_2026-07-20]] (no-brainer/underpriced-perk emsali, AnswerPhone analizinde referans alındı).
