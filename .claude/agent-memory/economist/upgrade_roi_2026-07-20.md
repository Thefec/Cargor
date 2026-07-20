---
name: upgrade-roi-2026-07-20
description: Upgrade ROI turu bulguları — gelir ÜRETİM-bound (Truck/Storage gelir vermez), reward-çarpan perkleri underpriced no-brainer, per-player 1.15 throughput-scaling'i telafi etmiyor
metadata:
  type: project
---

**2026-07-20 adanmış upgrade fiyat/ROI turu (Opus). Sim `tools/economy-sim/sim.js` + gerçek
scene fiyatları.** Kod DEĞİŞMEDİ; spec/bulgu üretildi.

**TEMEL BULGU — gelir OPTIMISTIC modelde ÜRETİM-bound:** `truckCapOptimistic` her senaryoda
`productionCap = rate×truckWindow` ile bağlanıyor (truckAcceptCap >> productionCap). Demand da
asla bağlamıyor (demand 15-49 >> cap 4.6-31.6). Sonuç:
- **Truck (+hangar) ve Storage (+demand) OPTIMISTIC'te SIFIR gelir** verir. Truck STRICT'te ROI 27-73x, OPTIMISTIC'te 0 → değeri tamamen model-bağımlı (playtest).
- Gerçek gelir kaldıraçları: (a) üretim hızı (boxes/min — agile_crew/energetic_crew/Table dolaylı, +%15 hız 1P +860 / 4P +5368 TL kazanç), (b) gün süresi (overtime +20s ROI 2.7-9.9x), (c) kutu-başı ödül çarpanları.

**Reward-çarpan perkleri UNDERPRICED (no-brainer), buyDay5 ROI (1P→4P):**
- Kumarbaz Kasası (220 TL): 7.7x → 29x. Aşırı ucuz.
- Yüksek Volatilite (300 TL): 3.4x → 12.2x. Salt yukarı-yön (mean+15%).
- Mesai Saati (200 TL): 2.7x → 9.9x.
- Kelle Koltukta (800 TL): 2.2x → 7.6x (grace-iptal riski gerçek, en dengeli).
Sorun: bu perkler throughput ile ölçekleniyor; 4P throughput 1P'nin ~5x'i (16gün gelir 7043→36184)
ama per-player fiyat çarpanı yalnız 1.15^3=1.52x → yüksek P'de RELATİF 3x daha ucuz. Öneri: base
fiyatları ~×1.5-2 artır (Kumarbaz 220→400, Volatilite 300→450, Mesai 200→300) VEYA magnitüdü küçült;
tam eşitlik tek fiyatla imkânsız (% etki doğası gereği yüksek-P kayırıyor). PLAYTEST-bağımlı: roguelite
"heyecanlı çekiliş" ister, aşırı sıkılaştırma da yanlış.

**Prestij perkleri rescale-sonrası SAĞLIKLI (değişiklik gerekmez):**
- Prestij Ustası (0.2→0.32, 660 TL): L2 ROI 3.0-5.1x, prestij-tavan gününü 1P asla→14, 4P 13→9'a çeker. 0.2 tabanı HÂLÂ hissediliyor (L1 delta 1126-3276 TL). Magnitüd iyi.
- Prestij Simsarı (bonusPerTier 5→6, 1015 TL): ROI 0.7x(1P zararına, kasıtlı)→3.2x(4P). Fiyat/magnitüd banda uygun. Değişme.

**Rent perkleri:** Ucuz Kira (480) ROI 0.55-1.6x, Kaldıraçlı Kira (350) 1.1-2.3x — 1P marjinal/zararına
(kasıtlı, [[rent_death_spiral]] tutarlı). OK.

**Source/sink:** 16-gün toplam gelir 1P 7043 / 4P 36184; kira-sonrası net 4547 / 28694. Realistik
upgrade bütçesi bu; kasa şişmesi/deflasyon runaway YOK (prestij clamp + kira geliri sınırlıyor).
1P Yavaş g12 iflas — upgrade'den bağımsız önceden var olan kırılganlık.

İlişkili: [[upgrade_legacy_backbones]], [[upgrade_pricing_framework]], [[roguelite_perk_pricing]],
[[hangar_stay_duration_per_player]], [[money_comes_only_from_trucks]], [[truck_hangar_window_cap]]
