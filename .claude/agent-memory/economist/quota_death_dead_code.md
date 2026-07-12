---
name: quota-death-dead-code
description: QuotaManager.CheckEndOfDayQuota/OnQuotaFailed hicbir yerden cagrilmiyor - GDD'de tanimli kota-olumu kodda dead code, karar bekliyor
metadata:
  type: project
---

2026-07-12 analizi: `Assets/NewCss/QuotaManager.cs` içindeki `CheckEndOfDayQuota()` ve `OnQuotaFailed` event'i hiçbir yerden çağrılmıyor/dinlenmiyor (doğrulandı — `DayCycleManager.ProcessDayEnd()` sadece `TryProcessMoneyCheck()` çağırıyor, kota kontrolü yok). GDD.md bölüm 7.2 "gün sonunda kota tutturulamazsa GAME OVER" diyor ama kod bunu tetiklemiyor.

Kota formülü: `ceil(toplamMüşteri × 0.8)`, min 1. Örnek: gün 1 kota ~5-8, gün 16 kota ~16-20 (müşteri sayısı upgrade ilerlemesiyle büyüdükçe).

**Why:** Kullanıcıya sunulan karar memosunda net öneri: kota-ölümünü aktive et AMA kira sistemiyle simetrik iki kademeli tampon ekle (1. kaçırma=uyarı+küçük prestij cezası, 2. kez üst üste=game over) — çünkü kira sisteminde zaten grace/2.kez-game-over yapısı var (`TryProcessMoneyCheck`), kota günlük kontrol edilirse (16 fırsat) tek kademeli hard-fail kira sisteminin 4 katı ölüm riski yaratır. Bu, [[prestige_fragility]]'de doğrulanmış "tek olay zinciriyle oyun bitmemeli" ilkesiyle tutarlı.

**How to apply:** Bu konu implementasyona geçerse: `DayCycleManager.ProcessDayEnd()` içine `QuotaManager.Instance.CheckEndOfDayQuota()` çağrısı eklenmeli, `OnQuotaFailed` dinlenip ardışık kaçırma sayacı tutulmalı (kira grace mantığına benzer). Karar kullanıcıdan onay bekliyor — henüz uygulanmadı.
