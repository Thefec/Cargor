---
name: event-kart-tasarim-2026-09-25
description: Her-gün event (22'lik katalog, bant kuralı) + 6 kapalı kartın yeni tasarımı (Bahşiş/Taksit/Serinlik/Sabah Vardiyası/Sıra Numaratörü/Hava Raporu) — değerler, ölçüm, açık sorular; kullanıcı onayı BEKLENİYOR
metadata:
  type: project
---

Doküman: docs/economy/her-gun-event-ve-kart-tasarimi-2026-09-24.md · harness tools/economy_sim/event_kart_2026_09_24.py (selftest: özellikler kapalıyken sim.Run ile bit-birebir) · sonuçlar results_event_kart_2026-09-24/.
Durum 2026-09-25: TASARIM teslim edildi, kullanıcı onayı yok, kod yazılmadı.

Karar noktaları:
- Event denetimi: 16'nın hiçbiri tam ölü değil; müşteri artışı event'leri hedefin %15-50'si (varış aralığı ölçeklenmiyor), RELAXED/CUSTOMER SUPPORT ~etkisiz, FATIGUE gizli müşteri×0.85, Golden/VIP bayrakları okuyucusuz.
- Gün 1'de OnNewDay tetiklenmez → gün 1-3 event'i için EventEffectManager koşu başında da aktive etmeli.
- Katalog 22 (12+/10−), bant: 1-3 PPP (hafif), 5-7 P N X, 9-11 P N X, 13-15 P N N (orta/sert); ~7P/5N; tekrar yok.
- Kartlar: Güler Yüz=Bahşiş (%10/%20 kutu ödülü/müşteri, 80/80), Sağlam Kasa=Taksit (+1 grace, %70, 60, KİLİT-MUAF, Acil Fren ile özel grup), Su Sebili=Serinlik (neg event %75 hafif, 40), Dinç Ekip=Sabah Vardiyası (1/2 kutu/gün, 80/80), Geniş Kuyruk=Sıra Numaratörü (sabır masada başlar, 70), Uzun Kuyruk=Hava Raporu (dönem başı 1 neg→pos, 60).
- Sonuç (rastgele teklif): zayıf kayıp %25.4→%19.4; Acil Fren'siz zayıf P3/P4 %42/%35→%84/%80; orta/iyi ≥%99.9 (kaybedilemezlik değişmedi).
- Karışık Sevkiyat sim'de POZİTİF (+%10-19) çıktı → negatif event olarak alınmadı.

**Why:** Plan İş 1+2'nin ekonomi tasarım adımı; gameplay bu değerlerle uygulayacak.
**How to apply:** Uygulama/revizyon turunda buradan başla; kullanıcının S1-S5 cevaplarını (Taksit özel grup, isimler, teklif tohumu, sert final bandı, Karışık Sevkiyat) kontrol et. Bkz. [[draft-offer-day-seeded-2026-09-25]], [[mc40k-2026-09-24-findings]], [[sim-sync-gotchas]].
