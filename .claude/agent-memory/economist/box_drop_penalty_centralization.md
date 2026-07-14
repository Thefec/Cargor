---
name: box_drop_penalty_centralization
description: Ana harita kutu/ürün düşme para cezası merkezileştirme kararı (tek boxDropMoneyPenalty=5) + wrongDeliveryPrestigePenalty=-0.2 onayı + Truck default hizalama
metadata:
  type: project
---

2026-07-14, `plans/iterative-stargazing-yeti.md` Aşama 1+3 için verilen karar (kod değiştirilmedi, gameplay departmanına devredildi).

## Karar 1 — boxDropMoneyPenalty (TEK değer, kutu/ürün ayrımı YOK)
`GameEconomySettings.boxDropMoneyPenalty = 5` (TL). `productDropMoneyPenalty` diye ayrı alan **açma**.

Gerekçe: cezanın tek işlevi "dikkatsizliği caydırmak" (boş kutu ücretsiz spawn olur, dolu kutu hiç kırılmaz → gerçek materyal kaybı yok, ne kutu ne ürün için). Mevcut gömülü değerler zaten çoğunlukla 5'te toplanmış (RedNGO/YellowNGO/tüm ürün NGO'ları=5); tek dışlayıcı BlueNGO=1 ve ölü/legacy "Normal/" non-NGO kopyalar=10 (economySettings:{fileID:0}, null → hardcoded -0.05 prestij fallback kullanıyorlar, muhtemelen tutorial/dead code — ayrı hijyen kararı, plan Aşama 4 kapsamında zaten işaretli, bu karara dahil değil). 5 TL zaten baskın değer olduğu için minimum sürtünmeli, tutarlılık-odaklı bir düzeltme.

Node sim (16 gün, NORMAL senaryo baseline kasa: 1P=237, 2P=589, 3P=1277, 4P=1661 TL — bkz [[economy-audit-2026-07-13]]) chaotic-coop drop rate'i (1.5 drop/gün/oyuncu, kasıtlı kötümser) ile test edildi:
- penalty=5: en kırılgan 1P bile 51% kasa etkisiyle 117 TL'de bitiyor (iflas yok).
- penalty=8: 1P 81% etki, 45 TL'de bitiyor (çok ince marj, risk).
- penalty=10: 1P NEGATİF bitiyor (iflas riski) — reddedildi.
→ 5 TL güvenli üst sınır; 8+ TL 1P (en kırılgan oyuncu sayısı) için marjı tehlikeli inceltiyor.

Not: Bu değer, plan Aşama 2'nin (throwVelocityThreshold 1f→3f hizalaması, kırılma eşiğiyle senkron) tetikleme sıklığını **azaltacağı** varsayımıyla kalibre edildi — ama Aşama 2 uygulanmasa (mevcut düşük eşik kalsa) bile 5 TL chaotic-case'de dahi iflas yaratmıyor, güvenli.

## Karar 2 — wrongDeliveryPrestigePenalty = -0.2 (müdür önerisi ONAYLANDI)
Truck.ProcessWrongDelivery() içine `PrestigeManager.ModifyPrestige(-0.2)` eklenecek (mevcut -40 TL cezasının yanına). Konum: wrongProductPrestigePenalty(-0.1) ile customerLostPrestigePenalty(-1.5) arası, -0.1'e daha yakın ama 2x ağır (para cezası zaten var + kutu/kaynak israfı temayı haklı çıkarıyor).

Risk kontrolü: yanlış teslim zaten -40 TL caydırıcı içerdiğinden nadir bir olay. 1P prestij tamponu gün16'da ~62.5 (audit). 20 yanlış teslim = -4 prestij, 50 yanlış teslim (aşırı senaryo) = -10 — tampona kıyasla ihmal edilebilir. Onaylandı, sim re-check gerekmedi (etki < %5 tampon, eşik altı).

## Karar 3 — Truck.cs default hizalama
Yalnızca `penaltyPerBox` default'u **60 → 40** değişecek (SO=40 ile hizalı, SO referansı kaybolursa stale 60 fallback riski). Diğerleri zaten hizalı, değişiklik gerekmiyor:
- `rewardPerBox` default=50 ✓ (SO=50)
- `prestigePerBonus` default=10 ✓ (SO=10)
- `bonusPerTier` default=5 ✓ (SO=5)

## İlişkili
[[rent_death_spiral]] (1P en kırılgan oyuncu sayısı deseni burada da doğrulandı), [[quota_throughput_calibration]] (aynı plan ailesi, playtest-bağımlı kalibrasyon deseni tekrarlanıyor).
