---
name: quest_easy4_5_duplicate
description: easy4/easy5.asset birebir ayni icerik + ayni questId="4" (dictionary collision), ayrica easy2'yi (yari efor + daha yuksek EV) domine ediyor
metadata:
  type: project
---

2026-07-18 bulgusu (`plans/economy-balance-round.md`, birleşik ekonomi turu — önceki turlarda hiç
incelenmemiş yeni sistem). `Assets/Resources/Quests/easy4.asset` ve `easy5.asset` birebir aynı
içerik (başlık "Anlaşmalı Çalışan", "1 Tır Tamamla", aynı ödül/ceza havuzu) VE aynı `questId: "4"`
(string alan, `QuestData.cs:24`).

**Kod etkisi:** `QuestManager.BuildQuestDatabase()` (`Manager/QuestManager.cs:180-193`)
`Dictionary<string,QuestData>` questId ile key'liyor → çakışan key sessizce ezilir. İçerik birebir
aynı olduğu için ŞU AN pratik ekonomik etki YOK, ama içerik ileride farklılaştırılırsa sessiz
veri kaybı riski var. Ayrıca 2 asset slotu 1 görevi temsil ediyor (varyete kaybı, aynı gün 2 kopya
gösterilebilir).

**EV orantısızlığı (node, `poolEV` — [[quest_reward_balance]] formülü):** easy4/5 reward havuzu
6 öge (3 Money 10/20/30 + 3 Prestige 1/1.5/2, easy1/2/3'ün 5-öge yapısından farklı) → `p=2/6=0.333`,
`rewardMoneyEV=20`. `targetCount=1` tır (easy2'nin YARISI efor, easy2 targetCount=2) ama EV=20,
easy2'nin (17.6) ÜSTÜNDE — rasyonel oyuncu her zaman easy4/5'i easy2'ye tercih eder, easy2 fiilen
domine ediliyor.

**Öneri (asset değişikliği, henüz uygulanmadı):**
1. `easy5.asset` → `questId: "5"` (çakışmayı çöz).
2. Reward havuzunu easy2'nin ~yarısına indir: Money `10/20/30`→`5/8/12` (yeni EV≈8.3), Prestige
   `1/1.5`→`0.5/1`.
3. Alternatif: easy4/5'i farklı görev tiplerine dönüştür (`QuestType.CompleteSpecificColorTruck`
   zaten kodda bağlı, `HandleSpecificColorTruckCompleted`) — çeşitlilik + questId çakışması birlikte
   çözülür, ama bu tasarım kararı gerektirir.

**Why:** Kullanıcı "genel ekonomi turu" istedi, quest sistemi taraması sırasında easy4/5'in daha
önce hiç incelenmediği fark edildi (önceki turlar sadece easy1/2/3'e odaklanmıştı).

**How to apply:** Quest sistemine her dokunulduğunda `questId` alanının BENZERSİZ olduğunu kontrol
et (`QuestData.ValidateQuestId()` sadece boşsa GUID atıyor, elle girilen çakışan ID'leri YAKALAMIYOR
— bu genel bir QA riski, yalnız easy4/5'e özgü değil).

İlişkili: [[quest_reward_balance]]
