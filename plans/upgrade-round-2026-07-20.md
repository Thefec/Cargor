# Upgrade Fiyat/ROI Turu — 2026-07-20

> Kaynak: Opus economist. Sim `tools/economy-sim/sim.js` helper'larıyla; gerçek fiyatlar `The Main Office.unity` `UpgradePanel.upgrades` (satır ~21593-22061), etkiler `PerkEffect.cs` + `UpgradePanel.ApplyUpgradeEffect`. Cost formülü: `cost(L→L+1) = (baseCost + L·costStep) × 1.15^(P-1)`. Oyun koduna DOKUNULMADI — denetim/spec.

## KRİTİK ÇERÇEVE — gelir ÜRETİM-bound
Para = tır throughput ([[money_comes_only_from_trucks]]), bir katman daha: OPTIMISTIC modelde `truckCapOptimistic` her senaryoda `productionCap = rate × truckWindow` ile bağlanıyor (demand asla bağlamıyor: 15-49 >> cap 4.6-31.6). Sonuç:
- **Truck (+hangar) ve Storage (+demand) upgrade'leri OPTIMISTIC modelde SIFIR gelir üretir.** Truck STRICT'te ROI 27-73x, OPTIMISTIC'te 0 — değer tamamen playtest-belirsiz model seçimine bağlı.
- Gerçek gelir kaldıraçları: (a) üretim hızı (boxes/min), (b) gün süresi (overtime), (c) kutu-başı ödül çarpanları (gambler/volatilite/all_in/prestij).

## P0 — Draft havuzu kirliliği: 9 eski omurga hâlâ teklif ediliyor
Roguelite tasarımı (yalnız Storage/Table/Truck omurga kalsın) uygulanmamış; scene 9 omurgayı `kind:0` tutuyor, `DraftPool.IsEligible` (`DraftPool.cs:27`) kind:0'ı koşulsuz eligible sayıyor. 3-kartlık günlük draft'ı seyreltiyorlar.

| Upgrade | Dosya:satır | Sorun | Öneri |
|---|---|---|---|
| **Money** | `UpgradePanel.cs:714` (`ApplyMoneyUpgrade`) + `:427` | **AKTİF ZARARLI**: `rewardPerBox = 15 + level·10` = 25/35/45, taban 50'nin ALTINDA. Kart teklifteyse spawn'da rewardPerBox'ı 15'e çekebilir. Satın alan geliri DÜŞÜRÜR. | Havuzdan çıkar; reward-artışı istenirse effectId'li perke taşı (base'e ADDITIVE) |
| **Customer** | scene 21743; switch'te case YOK | **ÖLÜ**: mekanik etki yok, 1500 TL sadece görsel. `patient_customers` perki gerçek işi yapıyor | Havuzdan çıkar |
| **Water** | scene 21724, maxLevel 1, 500 TL | Kozmetik, değer 0 | Havuzdan çıkar (veya sembolik ≤60 TL) |
| **Quest Tier** | scene 21763; `SetQuestTier` | Quest sistemi pasif, EV≈0 | `requiresQuestSystem=1` yapıp gizle |
| **Stamina** ↔ energetic_crew | `:706` vs `PerkEffect.cs:109` | DUPLİKE (ikisi staminaRegenRate=2.5) | Birini kaldır (perki tut) |
| **Queue** ↔ long_queue | `:698` vs `PerkEffect.cs:131` | DUPLİKE/ÇAKIŞAN (maxQueueSize) | Birini kaldır |

Not: kablolama/temizlik işi (gameplay), ekonomik değer kararı değil — ama upgrade ekonomisinin doğrulanamamasının KÖK nedeni bu. Detay: [[upgrade_legacy_backbones]].

## P1 — Reward-çarpan perkleri underpriced (no-brainer)
buyDay=5, gün 5-16 kümülatif ROI:

| Perk | dosya:satır | Fiyat(1P) | ROI 1P | ROI 4P | Değerlendirme |
|---|---|---|---|---|---|
| Kumarbaz Kasası | `PerkEffect.cs:137` / scene 21919 | 220 | 7.7x | 29.2x | Aşırı ucuz |
| Yüksek Volatilite | `PerkEffect.cs:170` / scene 21991 | 300 | 3.4x | 12.2x | Salt yukarı-yön |
| Mesai Saati | `PerkEffect.cs:196` / scene 21955 | 200 | 2.7x | 9.9x | Üretim/gün-süresi kaldıracı |
| Kelle Koltukta | `PerkEffect.cs:178` / scene 22027 | 800 | 2.2x | 7.6x | En dengeli (grace-iptal riski) |

Kök: throughput ile ölçekleniyorlar; 4P 16-gün geliri 1P'nin ~5x'i ama per-player çarpanı yalnız 1.52x → yüksek P'de relatif 3x ucuz. Önerilen (magnitüd sabit):

| Perk | alan | scene satır | eski | yeni | gerekçe |
|---|---|---|---|---|---|
| Kumarbaz Kasası | baseCost | 21929 | 220 | **400** | 1P ROI ~4x, 4P ~16x |
| Yüksek Volatilite | baseCost | 22001 | 300 | **450** | 1P ~2.3x, salt-upside primi |
| Mesai Saati | baseCost | 21965 | 200 | **300** | 1P ~1.8x |
| Kelle Koltukta | baseCost | 22037 | 800 | **800 (değişmez)** | Zaten pahalı + gerçek downside |

Playtest-bağımlı: Roguelite "heyecanlı çekiliş" ister; aşırı sıkma da yanlış. En sivri uçları törpüleme. Tek fiyatla 1P-4P eşitliği matematiksel imkânsız (% etki yüksek-P kayırır).

## P2 — Sağlıklı / değişme (doğrulama)
- **Prestij Ustası** (`PerkEffect.cs:92`, served 0.2→0.32, 660 TL): L2 ROI 3.0-5.1x; 0.2 tabanı HÂLÂ hissediliyor (L1 delta 1126-3276 TL). Magnitüd doğru. **v3.2 raporunun "0.5→0.65" değerleri BAYAT; kod güncel, etki değerli.**
- **Prestij Simsarı** (`PerkEffect.cs:85`, bonusPerTier 5→6, 1015 TL): ROI 0.7x(1P kasıtlı zararına)→3.2x(4P). Banda uygun. Değişme.
- **Rent perkleri**: Ucuz Kira (480, 0.55-1.6x), Kaldıraçlı Kira (350, 1.1-2.3x) — kasıtlı [[rent_death_spiral]] tutarlı. OK.

## Per-player (upgradeCostMultiplierPerPlayer = 1.15)
Fixed-etki upgrade'ler için adil. Throughput-scaling perkler için yetersiz ama blunt araç — globalde 1.15 kalsın, dengesizliği perk fiyat/magnitüd seviyesinde çöz.

## Source/sink
16-gün gelir 1P 7043 / 4P 36184; kira-sonrası net 4547 / 28694. Kasa şişmesi/deflasyon runaway YOK (prestij clamp + kira büyümesi). Sink ~%51 (1P). 1P Yavaş gün12 iflası upgrade'den bağımsız.

## Öncelik özeti
1. **P0**: Money(zararlı)/Customer(ölü)/Water/Quest Tier draft havuzundan çıkar; Stamina/Queue duplikasyonu tekle. (gameplay — kablolama)
2. **P1**: Money upgrade sil veya base'e-additive doğru effectId'ye taşı (rewardPerBox<50 bug).
3. **P1 (playtest)**: 4 reward-çarpan perkinin fiyatını ~×1.5.
4. **P2 (playtest)**: Truck upgrade değeri STRICT/OPTIMISTIC belirsizliğine takılı — model netleşene kadar fiyat kararı yok; şimdilik ucuz/zararsız.

## Memory
[[upgrade_legacy_backbones]], `upgrade_roi_2026-07-20.md` (yeni), [[upgrade_pricing_framework]], [[roguelite_perk_pricing]], [[money_comes_only_from_trucks]].
