---
name: quest-fixed-reward-table-2026-07-28
description: 30 quest icin SABIT moneyReward/prestigeReward/moneyPenalty/prestigePenalty tablosu (havuz-pick-2 modeli terk edildi, QuestData artik 4 sabit alan tasiyor) - grup-carpani metodolojisi (base/premium/phone) + tier EV dogrulamasi
metadata:
  type: project
---

**Bağlam**: 2026-07-28, `QuestData.cs` modeli havuzdan-rastgele-2-seçimden 4 sabit alana geçti
(`moneyReward`/`prestigeReward`/`moneyPenalty`/`prestigePenalty`, ceza pozitif yazılır kodda
`-Mathf.Abs()` uygulanır). [[quest_tier_redesign_2026-07-25]]'teki 5-öğe havuz artık GEÇERSİZ
(o dosyanın EV'leri bu turun hedef-bandı REFERANSI olarak kullanıldı, kendisi tarihsel). Kaynak
katalog: `plans/quest-listesi.md` (30 görev: Easy 11 · Medium 10 · Hard 9, targetCount/tip/renk
zaten kod-doğrulanmış, DEĞİŞTİRİLMEDİ — bu tur sadece ödül/ceza SAYILARINI sabitledi).

## Metodoloji: 3 grup × tier (base / premium / phone)

Her tier'daki quest'ler 3 gruba ayrıldı, HER GRUP içinde AYNI sabit değer:

- **base**: standart colorless Shelf/Pack (target 4/7/10) VE renk-kilitli varyantlar (target 2/3/4)
  AYNI değeri alır. Gerekçe: renk-kilitli hedef sayısı zaten düşük tutulmuş (colorless'ın ~%40-50'si,
  bkz [[quest_tier_redesign_2026-07-25]] targetCount türetmesi) — bu düşük hedef renk kıtlığının
  telafisidir, ayrıca ödül primi eklemek ÇİFTE telafi olurdu. Kasıtlı basitleştirme.
- **premium** (base × 1.5): CompleteTruck (tüm tier'lar) + Easy'nin ekstra "Vardiya Sonu Düzeni"
  (6 kutu, aynı tip içinde bir üst hedef). Truck'a prim gerekçesi: tam tır tamamlama hem çok
  kutu gerektiriyor hem hangar-penceresi zamanlama riski taşıyor ([[truck_hangar_window_cap]],
  [[hangar_stay_duration_per_player]]) — tek "target count" metriğiyle ölçülemeyen ekstra risk.
- **phone** (base × ~1.15-1.18): AnswerPhone (Easy/Medium'da var, Hard'da YOK — mevcut karar
  korundu, [[quest_tier_redesign_2026-07-25]] Hard-telefon negatif-EV nedeniyle zaten basılmamıştı).
  Prim gerekçesi: oyuncu kontrolü düşük (RNG'ye bağlı, [[quest_answerphone_colortruck_2026-07-25]]
  P-bağımsız zar mekaniği) — ama düşük emek nedeniyle prim KÜÇÜK tutuldu (premium kadar değil).

Grup ağırlıkları tier ortalaması eski EV bandına (20/36/60 TL, 0.48/0.88/1.6 prestij,
[[quest_tier_redesign_2026-07-25]] referansı) eşitlenecek şekilde çözüldü, sonra "temiz" sayılara
yuvarlandı (node/python, `round_nice` iterasyonu — bkz final tablo).

## Final sabit değerler (canlı, uygulamaya hazır)

| Tier | base money/prestij | premium money/prestij | phone money/prestij |
|---|---|---|---|
| Easy | 18 / 0.4 | 28 / 0.7 | 22 / 0.5 |
| Medium | 34 / 0.8 | 52 / 1.2 | 40 / 1.0 |
| Hard | 57 / 1.5 | 86 / 2.3 | — (yok) |

Ceza = money×~0.55, prestij×~0.50-0.55 (mevcut "ceza < ödül" kuralı [[quest_tier_redesign_2026-07-25]]
ile tutarlı devam):

| Tier | base ceza (M/P) | premium ceza (M/P) | phone ceza (M/P) |
|---|---|---|---|
| Easy | 10 / 0.2 | 15 / 0.4 | 12 / 0.2 |
| Medium | 19 / 0.4 | 29 / 0.6 | 22 / 0.5 |
| Hard | 31 / 0.8 | 47 / 1.2 | — |

**Doğrulanmış tier ortalama EV** (30 quest'in basit ortalaması, node): Easy money=20.18 (hedef 20,
sapma +0.9%), Medium=36.4 (+1.1%), Hard=60.22 (+0.4%); prestij Easy=0.464 (-3.4%), Medium=0.86
(-2.3%), Hard=1.589 (-0.7%) — hepsi ±4% içinde, eski EV bandı KORUNDU.

## Denge riskleri / notlar

1. **base grup içi target-farklılaştırma YOK** (Shelf-4 = ShelfColor-2 = aynı değer) — bilinçli
   basitleştirme (yukarıda), sadece görev BAŞLIĞI/hedefi farklı görünür ama ödül aynıdır. Playtest'te
   "neden 10 kutu ile 4 kutu aynı ödülü veriyor" hissi gelirse (Hard'da en belirgin: target=10 vs
   target=4 hepsi 57 TL) ince ayar gerekebilir — şu an kasıtlı, kanıt yok henüz.
2. **Tek-gün ceza riski YOK**: takım geneli günde 1 kabul limiti korunduğu için (kod sabitine
   dokunulmadı) aynı gün birden fazla ceza stacklenemiyor. En büyük tek ceza Hard/premium=47 TL,
   önceki turun "günlük çekirdek geliri aşmasın" kriterinin (~144 TL en kırılgan gün) çok altında.
3. **Enflasyon riski YOK**: tier ortalama EV'ler eski havuz-bazlı EV bandını (±4%) koruyor, quest
   artık VARYANSSIZ ama TOPLAM ekonomik ağırlık değişmedi.
4. **Quest hâlâ "bonus" kalemi**: [[quest_tier_redesign_2026-07-25]]'te ölçülen quest-EV/günlük-
   çekirdek-gelir oranı (%0.3-4.6 bandı) bu turda yeniden simüle EDİLMEDİ ama sabit değerler eski
   EV'lerle aynı büyüklükte olduğu için sonuç değişmemesi beklenir — bir sonraki sim turunda
   `sim.js`'e sabit-değer modeliyle re-check önerilir (varyans sıfırlandığı için aslında hesap
   DAHA BASİT hale geldi, best-of-3 EV yerine direkt 3 sabit değerin ortalaması yeterli).
5. **AnswerPhone Hard'da hâlâ yok** — mevcut karar (negatif EV) bu tur DEĞİŞTİRİLMEDİ, sadece
   Easy/Medium'daki phone quest'lere sabit prim uygulandı.

İlişkili: [[quest_tier_redesign_2026-07-25]] (EV referans bandı, artık tarihsel havuz),
[[quest_completetruck_color_constraint]] (CompleteTruck renk kilidi kısıtı, bu turda hedef
sayıları değişmedi), [[quest_answerphone_colortruck_2026-07-25]] (telefon P-bağımsızlığı, prim
gerekçesi), [[money_comes_only_from_trucks]], [[truck_hangar_window_cap]] (Truck premium gerekçesi).
