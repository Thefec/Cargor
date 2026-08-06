---
name: env-no-python
description: GUNCELLENDI 2026-07-25 - python3/python ARTIK PATH'te (kurulmus). Yine de bu projede Node.js tercih et, cunku tools/economy-sim/sim.js Node modulu ve cogu ekonomi hesabi ona require() ile entegre olmali.
metadata:
  type: project
---

**2026-07-25 güncelleme**: `python3`/`python` artık PATH'te bulundu (`/c/Users/cicek/AppData/Local/Python/bin/`)
— 2026-07-07'deki "yok" tespiti artık GEÇERSİZ, makineye Python kurulmuş. Yine de bu projede
**Node.js'i tercih etmeye devam et**: `tools/economy-sim/sim.js` (kaynak-doğrulanmış ekonomi
sabitleri + `runSim`/`truckCapOptimistic`/`questDailyEV` vb. fonksiyonlar) bir Node modülü —
`require('.../sim.js')` ile doğrudan import edip gerçek sim fonksiyonlarını kullanmak, aynı
mantığı Python'da yeniden yazmaktan çok daha az hataya açık ve sim ile senkron kalmayı garantiler.
Sim'e ihtiyaç duymayan yalın/bağımsız hesaplarda (örn. saf formül/oran hesabı) Python da kullanılabilir
— CLAUDE.md'nin genel talimatı artık bu ortamda da uygulanabilir, sadece sim-entegreli hesaplarda
Node zorunlu tercih.

**Eski not (2026-07-07, artık geçersiz kısım):** O tarihte `python3`/`python`/`py` PATH'te
bulunamamıştı, sadece `node` (v24.15.0) çalışıyordu — bu yüzden Node'a geçilmişti. Kök neden
(python eksikliği) artık yok ama Node'da kalma kararı sim.js entegrasyonu nedeniyle geçerliliğini
koruyor.
