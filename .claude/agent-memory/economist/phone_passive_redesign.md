---
name: phone-passive-redesign
description: Telefon sistemi proaktiften (E-bas-ara) pasif/reaktif (saatte %30 calar, ac->odul, acma->cezasiz) modele gecti - onaylanan deger seti
metadata:
  type: project
---

2026-07-14 kararı. Eski `PhoneCallManager` modeli (oyuncu E'ye basılı tutup arama başlatıyor, `callReward=10`, `maxCallsPerHour=2`, `timeSkipAmount=20`, `postCallCooldown=30`) **oyuncu-inisiyatifli**ydi ve arama başarılı olursa müşteri spawn + zaman atlatma + para veriyordu. Yeni tasarım **sunucu-inisiyatifli/pasif**: her oyun-saati başında `phoneRingChancePerHour` ihtimaliyle telefon çalıyor, oyuncu giderse ödül alıyor, gitmezse **hiçbir ceza yok**.

**Onaylanan değerler** (`Assets/NewCss/GameEconomySettings.cs` satır 75-87 bölgesi, `PhoneCallManager.cs` ile birlikte):
- `phoneRingChancePerHour = 0.30f` (YENİ alan)
- `phoneRingEventMultiplier = 1.5f` (YENİ alan, CUSTOMER_SUPPORT event günü çarpanı → efektif %45/saat). Bağıl (×1.5) seçildi, mutlak (%30→%50) değil — diğer takvim event'leri (RAINY DAY -%20, MARKETING +%20/-%30) zaten bağıl-yüzde dilinde tanımlı, tutarlılık için.
- `callMoneyReward = 20` (eski `callReward=10`'dan yükseltildi; `rewardPerBox=50`'nin ~%40'ı — bir kutu teslimatından bilinçli olarak daha düşük tutuldu ki oyuncu telefonu "daha iyi bir kutu" olarak kamp etmesin, ama koşup açmaya değecek kadar cazip)
- `callPrestigeReward = 0.5f` (YENİ alan, `customerServedPrestigeBonus=0.5` ile birebir aynı — tema: "telefonu açmak bir müşteriye iyi hizmet etmek kadar değerli")

**Kod-gerçek pencere**: `PhoneCallManager.phoneStartHour=8, phoneEndHour=18` → gerçek pencere **10 saat**, kullanıcının belirttiği "~11 saat (07-18)" DayCycleManager'ın genel gün penceresiyle (startHour=7,endHour=18) karışıyor olabilir — telefon kendi içinde 1 saat daha dar. Bu bilinçli bir tampon (gün başı 1 saat rahatsız edilmeme) olabilir, DEĞİŞTİRİLMEDİ, sadece not edildi.

**Beklenen değerler (10 saatlik pencere, node ile hesaplandı)**:
- Baseline: ~3.0 çalma/gün → ~60 TL + 1.5 prestij/gün
- Event günü (×1.5): ~4.5 çalma/gün → ~90 TL + 2.25 prestij/gün
- 1P gün1 box gelirine oranı ~%33 (cazip, erken oyunda anlamlı), gün16'da ~%10 (box geliri prestij-tier ile büyüdükçe telefonun payı doğal olarak küçülüyor — telefon ödülü sabit TL, ekstra ölçekleme gerekmiyor)
- 16 gün toplam (event günsüz varsayımla) ~960-1000 TL + ~24-26 prestij — 1P başlangıç kirasının (500 TL) ~2 katı kadar bir "cushion", rent_death_spiral riskini azaltıcı yönde ama domine etmiyor.

**Kaldırılması önerilen eski alanlar** (yeni tasarımda anlamsız/gereksiz hale geliyor):
- `timeSkipAmount` — KALDIR. Yeni tasarımda customer-spawn+zaman-atlatma yok, sadece direkt para+prestij ödülü.
- `maxCallsPerHour` — KALDIR. Saatte tek Bernoulli roll yapısı zaten ≤1 çalma/saat garantiliyor, ayrı bir limit alanı gereksiz.
- `postCallCooldown` — KALDIR. Oyuncu artık arama başlatmıyor (spam vektörü yok), sunucu tetikliyor; doğal saatlik kapı zaten cooldown görevi görüyor.

**Tasarım tutarsızlığı bulgusu**: `EventCalendarUI.cs:176`'da CUSTOMER_SUPPORT event'i `EventType.Negative` olarak etiketli ("RECEPTION PHONE RINGS 30% MORE OFTEN"). Eski (cezalı/proaktif) modelde bu mantıklıydı (daha sık çalma = daha çok dikkat dağınıklığı riski). Yeni modelde çalmanın **hiç dezavantajı yok** (açmazsan ceza yok, açarsan saf kazanç) — yani "daha sık çalma" objektif olarak artık bir POSITIVE event. Gameplay/design departmanına iletilmeli: event tipini `Positive`'e çevirmek veya en azından metnini/temasını gözden geçirmek gerekir, yoksa oyuncuya "negatif" diye sunulan bir gün aslında gizli bir bonus gün olur.

**Why:** Kullanıcı proaktif E-bas modelini pasif/reaktif modele geçirme kararı verdi, cezasızlık açıkça istendi (misilleme riski olmayan bir "quality of life" ödül sistemi hedefleniyor).

**How to apply:** Gameplay departmanı bu değerleri uygularken CUSTOMER_SUPPORT event tipini de gözden geçirsin; yeni `callMoneyReward`/`callPrestigeReward`/`phoneRingChancePerHour`/`phoneRingEventMultiplier` alanlarını `GameEconomySettings.cs`'e ekleyip eski 3 alanı silsin veya `[Obsolete]` işaretlesin.

**Telefon Hattı perk eşleme çözümü (2026-07-15, onaylandı)**: `PerkEffect.ApplyPhoneLine` (`Assets/NewCss/UpgradeScripts/PerkEffect.cs:141-150`) geçici olarak `ctx.Economy.phoneRingChancePerHour = 0.30f * 1.5f` (ALAN-SET, mutlak 0.45) yapıyordu. Sorun: `PhoneCallManager.GetEffectiveRingChance()` (`Assets/NewCss/Phone/PhoneCallManager.cs:263-270`) her saat zar atarken AYNI `phoneRingChancePerHour` alanını "taban" olarak okuyup CUSTOMER SUPPORT event'iyle (`×1.5`) çarpıyor — perk alanı kalıcı ezdiği için event günü perk+event çarpanları BİRLEŞİK-ÇARPIMSAL katlanıyor (0.45×1.5=0.675/saat, ~6.75 çağrı/gün, taban×2.25).
- **(1) Hedef değer**: İzole (event'siz) durumda **0.45 DOĞRU** — eski "kontenjan 2→3" (+%50) hedefiyle EV-tutarlı (3.0→4.5 çağrı/gün, +30 TL +0.75 prestij/gün). Sayı onaylandı, değiştirilmesin.
- **(2) Mekanizma**: ALAN-SET yanlış — `phoneRingChancePerHour` hem "taban" hem "event çarpanının uygulandığı değer" olarak çift görev görüyor, perk onu kalıcı ezince event çarpanı perk'in ÜSTÜNE binip katlanıyor. Önerilen düzeltme: perk KENDİ ayrı alanına yazsın (örn. `phoneRingPerkBonus`, additive/flat, level>0 → 0.15f) ve `GetEffectiveRingChance()` şu sıralamayla hesaplasın: `chance = Clamp(baseChance * (eventActive ? eventMultiplier : 1f) + perkBonus, 0, cap)` — yani event çarpanı SADECE ham tabana uygulanır, perk bonusu ayrıca ve additif eklenir (çarpımsal katlanma yok). Bu additive/relative eşdeğerliği izole durumda korur (0.30+0.15=0.30×1.5=0.45) ama event+perk günü 0.675 yerine 0.60'a (6 çağrı/gün, taban×2.0) sabitler. `Economy.phoneRingChancePerHour` alanı asla perk tarafından mutasyona uğramamalı — canonical baseline olarak kalmalı (UI/tooltip/save gibi başka kod yolları bu alanı 0.30 baz değer sanarak okuyabilir).
- **(3) Üst clamp**: EVET gerekli — mevcut kod `Mathf.Clamp01` kullanıyor (yalnız 100% tavanı), bu tasarımsal olarak anlamsız (saat başı garanti çalma = pasif "bonus" hissini bozup birincil gelir döngüsüne dönüşür). Önerilen: `Mathf.Clamp(chance, 0f, 0.65f)`. Gerekçe: additive model altında hesaplanan gerçekçi maksimum zaten 0.60 (perk+event); 0.65 bu değerin üstünde tampon bırakıp gelecekte ikinci bir telefon-odaklı relic eklenirse (ör. iki relic üst üste +0.15+0.15) oluşacak 0.30×1.5+0.15+0.15=0.75 gibi bir katlanmayı da güvenli şekilde kesiyor.
- **Sonuç günlük EV karşılaştırma (10 saatlik pencere, node ile hesaplandı)**: taban 3.0 çağrı/gün (60 TL); yalnız perk 4.5 (90 TL); yalnız event 4.5 (90 TL); perk+event mevcut-kod(ALAN-SET) 6.75 (135 TL); perk+event önerilen(additive+clamp0.65) 6.0 (120 TL, clamp'e takılmıyor çünkü 0.60<0.65). Perk+event kombinasyonu nadir (CUSTOMER SUPPORT 14 event'ten 1'i, aynı gün relic sahibi olmak gerekiyor) — mevcut ALAN-SET'in 0.675'i tek başına "felaket" değil ama mimari olarak kırılgan (alan çift-görevli), additive+clamp düzeltmesi hem daha güvenli hem daha öngörülebilir.
- **Uygulama notu (gameplay departmanına)**: `GameEconomySettings.cs`'e yeni `phoneRingPerkBonus` alanı eklenmeli (default 0), `PerkEffect.ApplyPhoneLine` bu alana `0.15f` yazmalı (Economy.phoneRingChancePerHour'a DOKUNMAMALI), `PhoneCallManager.GetEffectiveRingChance()` yukarıdaki additive formülle + `Clamp(...,0,0.65f)` güncellenmeli.

İlişkili: [[rent_death_spiral]], [[missing_events_g9]]
