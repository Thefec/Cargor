---
name: quota_throughput_calibration
description: C1 kota-ölümü aktivasyonu neden gerçek oyuncu kutu/dk verimi olmadan yapılamaz — sim kanıtı ve eşik sayıları
metadata:
  type: project
---

QuotaManager._difficultyRatio=0.8 ile CustomerManager'ın müşteri-sayısı büyüme formülü (dükkan büyüdükçe activeInteractables/storeLevel artıyor, ama gün süresi çok yavaş büyüyor: gün≤3=160s, sonra +10s/gün) arasında, oyuncunun GERÇEK kutu/dakika verimine bağlı kritik bir uyumsuzluk riski var.

Node.js sim (16 gün, [[env_no_python]]): 1P'de kota kaçırmadan bitirmek için ~6 kutu/dk/oyuncu (10 saniyede 1 teslimat) gerekiyor; 2P'de ~4/dk, 4P'de ~3/dk. Bu gerçek oyuncu verimi kodda yok, sadece playtest ile ölçülebilir. Verim ölçülen değerin altındaysa, kota HER GÜN kaçırılır (skill'den bağımsız) — hard game-over ile açılırsa oyunu anlamsız şekilde bitirir.

**Why:** 2026-07-13 FAZ 2 denetiminde C1 (kota-ölümü, halen dead code — DayCycleManager.ProcessDayEnd() QuotaManager.CheckEndOfDayQuota()'yu hiç çağırmıyor) aktivasyon kararı için yapıldı. Aktivasyon önerisi (bkz [[roguelite_perk_pricing]] tarzı iki-kademeli tampon: 1. kaçırma uyarı+prestij cezası -1.5, ardışık 3 kaçırma=game over) VERİLDİ ama ön koşul olarak playtest ile gerçek kutu/dk ölçümü ve `_difficultyRatio` kalibrasyonu (muhtemelen 0.8→0.4-0.6) şart koşuldu.

**How to apply:** C1 ile ilgili herhangi bir gelecek konuşmada, önce playtest verisi var mı diye sor. Yoksa hard game-over ÖNERME — önce kalibrasyon iste. Detay ve tam sim tablosu: `plans/economy-audit-2026-07-13.md` §2.
