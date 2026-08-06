---
name: faz4-final-value-set-2026-07-30
description: FAZ 4 NİHAİ EKONOMİ SETİ — FAZ 1/2/3'ün üstüne yazar; kira {500,1000,1450,1800}+g1.35, lost -0.4, moneyMult 1.2, maliyet DİZİSİ {1,2,2.95,3.7}
metadata:
  type: project
---

**`plans/economy-rebuild-2026-07-30-faz4-final.md` UYGULANACAK TEK KAYNAKTIR.**
FAZ 1/2/3 raporlarıyla çeliştiği her yerde FAZ 4 kazanır.

**Why:** FAZ 3 sim'de iki modelleme hatası buldu (D1 `startingActiveInteractables` 3→**5**,
D2 **masa çekişmesi** hiç modellenmemiş). Düzeltince gelir tabanı %7-13 düştü ve gelir ölçeği
`1:1.81:2.44:3.15` → **`1:1.73:2.40:2.95`** oldu. FAZ 2 ve FAZ 3'ün gelir-ölçeğine kalibre edilmiş
tüm sayıları bu yüzden kaydı.

**How to apply:** ekonomi değeri sorulduğunda önce FAZ 4 §B tablosuna bak; FAZ 2/3'e yalnız
*gerekçe* için dön.

## FAZ 2/3'ten REVİZE olanlar (yalnız bunlar değişti)
| Kalem | FAZ 2/3 | **FAZ 4** | Neden |
|---|---|---|---|
| `baseRentByPlayerCount` | {500,1000,1550,2150} | **{500,1000,1450,1800}** | Paket-sonrası gerçek gelir ölçeği `1:2.04:2.94:3.67` (FAZ2 `1:1.99:3.12:4.28` sanıyordu); yayılım 0.32 → **0.04** |
| `customerLostPrestigePenalty` | −0.5 | **−0.4** | Düzeltilmiş talep (13/16/20/24) ile −0.5 → 1P STRICT **gün 7'de prestij ölümü** |
| `moneyMultiplierPerPlayer` | 1.35 | **1.2** | Ölçüt: gün-4 sonrası kasa P'den bağımsız (572/599/622/724) |
| `upgradeCostMultiplierPerPlayer` | F2 skaler 1.62 / F3 dizi {1,2,3.1,4.25} | **DİZİ {1.00,2.00,2.95,3.70}** | En iyi skaler bile 2P/3P'de %19-25 sapıyor; kod değişikliği bu yüzden haklı. Skaler zorunluysa **1.55** |
| `Geniş Ambar` | F3-C3: maxLevel 3 / 450 TL "değer kazanıyor" | **maxLevel 2 / 60+30 = 150 TL** | Ölçüm: 1P −176 · 2P +278 · 3P −63 · 4P −79 TL → C3 GERİ ALINDI |
| `long_queue` | "aktif zararlı −449…−5392, P0" | **P2, ölü kart** (1P/2P/3P **0**, 4P −519) | 2 istasyondan sonra kuyruk zaten bağlayıcı değil |
| `leveraged_rent` perk senkronu | −1.0f | **−0.8f** (etki değiştirilmezse) | Yeni taban −0.4'ün 2 katı |
| quest `targetCount` ölçek vektörü | {1,1.8,2.45,3.15} | **{1,2,2.95,3.70}** (tır+telefon hariç) | Tek `ECONOMY_SCALE`; üretim ölçeği `1:1.92:2.75:3.45` olduğu için 3P/4P ~%5-7 zorlaşır (kabul) |

## DEĞİŞMEYENLER (FAZ 2/3'ten aynen)
`rentGrowthMultiplier` **1.35** · prestij ×2 paketi (served 0.4, perBonus 8, startingPrestige 12,
maxPrestige **100**) · **2 paralel servis istasyonu** (FAZ 4'te doğrulandı: prestij/gün 1 istasyonla
3.73/3.55/3.55/3.55 → 2 istasyonla **5.20/6.40/8.00/8.45** monoton) · `maxQueueSize` 2 ·
`hangarStay {120,60,40,30}` · P-bazlı kargo (**gelir-nötr ≤%1.5 doğrulandı**) · sabır 24-32 ·
FAZ 3 §6 upgrade/perk fiyatları (bütçe yalnız %5 kaydı, kapsama **%141**) · quest D1/D2/D3 +
prestij ×2 (Easy 28/−15, Med 60/−27, Hard 150/−53) · FAZ 2 §3.2 event tablosu.

## Nihai beklenen tablo (Normal, 1 hangar, S=6)
OPT kümülatif **6 299 / 12 852 / 18 541 / 23 135**, oran **1.90/1.94/1.93/1.94**, kira baskısı düz
(1.71-2.06). STRICT **2 658 / 6 059 / 9 424 / 12 228**, oran 0.80/0.91/0.98/1.02, **hiç iflas yok**.
Yavaş+OPT 0.80-1.04 (kazanıyor), Yavaş+STRICT hepsi kaybediyor (bilinçli).
Upgrade bütçesi (kira sonrası fazla): **2 983 / 6 219 / 8 922 / 11 195**.

İlgili: [[faz2_prestige_rent_event_2026-07-30]] [[faz3_upgrade_quest_2026-07-30]]
[[economy_rebuild_faz1_2026-07-30]] [[sim_v31_table_contention]]
