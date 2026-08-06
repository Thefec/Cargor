---
name: quest_reward_balance
description: easy1/2/3 quest EV mekanigi (pool pick-2-of-n) + easy3 orantisizlik duzeltmesi (180 TL EV -> 30 TL EV) karari, 2026-07-18 ONAYLANDI ve UYGULANDI. 2026-07-25 itibariyle TARIHSEL - easy1-5 tamamen silinip [[quest_tier_redesign_2026-07-25]] ile degistiriliyor, EV FORMULU (pick-2-of-n) hala gecerli/tekrar kullanildi.
metadata:
  type: project
---

**2026-07-25 güncelleme**: easy1-5 assetleri kullanıcı tarafından silinip 3-tier (Easy/Medium/Hard,
15 quest) yeni tasarımla değiştiriliyor — bkz [[quest_tier_redesign_2026-07-25]] (güncel/canlı tablo).
Bu dosyadaki EV FORMÜLÜ (pick-2-of-5, `p=0.4`) ve "rasyonel oyuncu en iyi EV'liyi seçer" modelleme
prensibi AYNEN yeni tasarımda tekrar kullanıldı — sadece aşağıdaki SAYILAR (easy1/2/3'ün spesifik
TL değerleri) artık tarihsel referans, canlı değil.

2026-07-18 kararı (`plans/economy-audit-2026-07-17.md` §4-5, dal `feature/economy-quest-balance`).
`Assets/Resources/Quests/easy3.asset` DÜZENLENDİ (bu turda bizzat uygulandı, gameplay'e devredilmedi
— saf veri/YAML değişikliği, kod değişikliği yok).

## Mekanik (gelecek quest analizlerinde tekrar kullan)
`QuestData.cs`: her quest'in `rewardPool`/`penaltyPool`'undan **rastgele 2** seçilir
(`MAX_SELECTED_REWARDS/PENALTIES=2`, Fisher-Yates). Havuz `n` ögeliyse her ögenin seçilme olasılığı
`min(2,n)/n` (uniform). **EV formülü**: `EV(tür) = P(dahil) × Σ(o türden ögelerin toplamı)`. Örnek:
5 ögeli havuz (3 Money + 2 Prestige) → `P=0.4` → `EV(Money)=0.4×ΣMoney`, `EV(Prestige)=0.4×ΣPrestige`.

`QuestManager.cs`: günde 3 gösterilir (`DAILY_QUEST_COUNT`) ama **sadece 1 kabul edilebilir**
(satır 591-597). Modelleme için: oyuncu rasyonel varsayılmalı — sunulan seçenekler arasından EN
İYİ EV'liyi seçer, hepsi negatifse hiçbirini kabul etmez (ortalama almak veya "zorla kabul"
varsaymak struggling takımlar için sahte-negatif quest geliri üretir — bkz `tools/economy-sim/sim.js`
`questDailyEV()` yorumu, ilk taslakta bu hata yapılmış ve düzeltilmişti).

## easy1/easy2/easy3 EV tablosu (node ile hesaplandı, kafadan değil)
| Quest | rewardMoneyEV | rewardPrestigeEV | penaltyMoneyEV | penaltyPrestigeEV |
|---|---|---|---|---|
| easy1 (5 kırmızı paketle) | 18 | 1.2 | -12 | -1.2 |
| easy2 (2 tır tamamla) | 17.6 | 1.2 | -14.4 | -1.2 |
| easy3 ESKİ (rafa 5 kutu) | **180** | 1.2 | -17.5 | -0.5 |
| easy3 YENİ (uygulandı) | **30** | 1.2 | -18 | -1.2 |

## easy3 karar detayı
**Sorun**: eski para havuzu (100/150/200) EV=180 TL, easy1(18)/easy2(17.6)'nin ~10 katı — aynı
"Easy" tier'da orantısız. Nicel kanıt: en kırılgan senaryonun (1P Yavaş) gün-1 çekirdek geliri
144 TL — eski easy3 EV'si (180) bunu bile aşıyor; max tek-çekim (150+200=350) günlük gelirin 2.4x'i.

**Uygulanan yeni değerler**:
- rewardPool Money: 100/150/200 → **15/25/35** (EV=30, easy1/2'ye göre 1.70x — ±2x bandı içinde)
- rewardPool Prestige: 1/2 → değişmedi (easy1/2 ile zaten aynıydı)
- penaltyPool Money: -20/-10/-5 (3 öge) → **-15/-20/-10** (EV=-18, reward'ın ~%60'ı)
- penaltyPool Prestige: **sadece -1 (1 öge, asimetrik)** → **-1/-2 (2 öge)** — artık easy1/easy2 ile
  AYNI YAPIDA (3 Money + 2 Prestige = 5 öge), EV=-1.2, easy1/easy2 ile TAM AYNI

**easy1/easy2 değiştirilmedi** — zaten birbirine yakın (18 vs 17.6, 1.02x), kritik ihlal yok.

**Neden 30 TL (0 ya da 18 değil)**: "domine etmeyen AMA anlamlı" hedefi — sıfırlama değil hizalama.
30, easy1/2'nin biraz üstünde kalarak görevin "en değerlisi" temasını korur (rafa kutu koymanın
diğer ikisinden daha az emek/renk-kısıtı gerektirdiği düşünülürse bu fazlalık tartışmalı olabilir,
ama spec'in kendi çerçevesi easy3'ü "orta-yüksek" tamamlanma ile easy1'den [yüksek] hemen sonraya
koyuyordu — EV farkının küçük olması bununla tutarlı).

**Why:** Kullanıcı (spec `2026-07-17-economy-quest-balance-design.md` §5) easy3'ün bilinen
orantısızlığını bu turda düzeltilmesini istedi; "hiçbir aktif quest tek başına bir günlük çekirdek
geliri aşmamalı" ve "easy1-3 ±2x içinde" kriterleri açıkça verildi.

**How to apply:** Gelecekte easy4/easy5 (bu turun kapsamı dışı, muhtemelen bozuk/inaktif — spec
"DIŞARIDA" listesinde) düzeltilirse AYNI EV çerçevesini (pick-2-of-5, ~18-30 TL bandı, prestij
havuzu 1/2 sabit) uygula, tutarlılık için. Quest tier sistemi (`_currentQuestTier`, Medium/Hard)
açılırsa bu bandın üst tier'lar için nasıl ölçekleneceği (muhtemelen ×1.5-2 per tier) ayrı bir karar
gerektirir — bu turda ele alınmadı.

İlişkili: [[roguelite_perk_pricing]] (benzer "havuzdan EV hesapla, kafadan yapma" disiplini),
[[truck_hangar_window_cap]] (easy2 tamamlanma oranı bu modelden dinamik türetildi)
