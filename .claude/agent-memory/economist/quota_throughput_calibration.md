---
name: quota_throughput_calibration
description: ARTIK GECERSIZ - QuotaManager.cs dosyasi tamamen silindi (0c026ef, 2026-07-14), C1 kota-olumu aktivasyonu konusu kapandi. Tarihsel kayit.
metadata:
  type: project
---

> ⚠️ **GÜNCEL DEĞİL (2026-07-18 doğrulandı):** `QuotaManager.cs` dosyası **tamamen silindi**
> (commit `0c026ef`, 2026-07-14, "kota sistemini kaldir (kod) + QUOTA DAY event cikar"). Bu bilinçli
> bir temizlikti (C1 aktivasyonu/kalibrasyonu DEĞİL) — commit mesajı: "Kota sistemi tamamen
> izole/oluydu: RegisterShippedBox() hicbir yerden cagirilmiyordu, CheckEndOfDayQuota() cagirilmiyor,
> OnQuotaFailed/OnQuotaCompleted dinleyicisiz, GameOver'a hic bagli degildi." QUOTA DAY event de
> takvimden çıkarıldı. Aşağısı artık geçmiş/tarihsel kayıt — **C1 aktivasyonu konusu tamamen
> kapandı, gelecekte tekrar gündeme gelmemeli** (kota mekaniği kod tabanında yok).
>
> Sahne borcu: `The Main Office.unity` içinde hâlâ "QuotaManager" adlı GameObject var (script silinmiş,
> missing-script durumu) — commit mesajında zaten not edilmiş, kullanıcının Unity'de manuel silmesi
> bekleniyor.

---

(Tarihsel — 2026-07-13 denetimi, artık geçerli değil)

QuotaManager._difficultyRatio=0.8 ile CustomerManager'ın müşteri-sayısı büyüme formülü (dükkan büyüdükçe activeInteractables/storeLevel artıyor, ama gün süresi çok yavaş büyüyor: gün≤3=160s, sonra +10s/gün) arasında, oyuncunun GERÇEK kutu/dakika verimine bağlı kritik bir uyumsuzluk riski var.

Node.js sim (16 gün, [[env_no_python]]): 1P'de kota kaçırmadan bitirmek için ~6 kutu/dk/oyuncu (10 saniyede 1 teslimat) gerekiyor; 2P'de ~4/dk, 4P'de ~3/dk. Bu gerçek oyuncu verimi kodda yok, sadece playtest ile ölçülebilir. Verim ölçülen değerin altındaysa, kota HER GÜN kaçırılır (skill'den bağımsız) — hard game-over ile açılırsa oyunu anlamsız şekilde bitirir.

**Why (tarihsel):** 2026-07-13 FAZ 2 denetiminde C1 (kota-ölümü, o zaman dead code) aktivasyon kararı için yapıldı. Sonuç: aktivasyon yerine dosya tamamen silindi.

**How to apply:** Kota/quota ile ilgili gelecek bir konuşmada artık "aktivasyon kalibrasyonu" önerme — mekanik kod tabanında yok. Eğer kullanıcı kota mekaniğini GERİ getirmek isterse bu, yeni bir tasarım kararı (mevcut kodun restore edilmesi + [[missing_events_g9]]'daki QUOTA DAY notlarıyla birlikte) olur, eski `_difficultyRatio=0.8` kalibrasyon riski hâlâ geçerli bir başlangıç noktası olabilir.

İlişkili: [[env_no_python]], [[missing_events_g9]]
