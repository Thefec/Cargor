# 02 — Matematiksel Model

**Tarih:** 2026-09-18 · Girdi: `01-parametreler.md` (tüm sayılar oradan; burada sadece formül ve akış). Kod-dışı her büyüklük **VARSAYIM** etiketli ve §6 tablosunda parametrik.

---

## 1. Zaman modeli

**Gün süresi (gerçek sn):** `T_d = 200·ovt + 10·max(0, d−3)`, `ovt = 1.125` Mesai Saati perki varsa, yoksa 1. Gün 1-3: 200 s; gün 16: 330 s (perkli 355 s).

**Oyun saati ↔ gerçek saniye:** 11 oyun-saati (07:00-18:00). `sn/saat = T_d/11`; gün 1: 18.2 s/saat, gün 16: 30 s/saat.

**Pencereler (gün payı):**

| Pencere | Oyun saati | Gün payı | Gün 1 (sn) | Gün 16 (sn) |
|---|---|---|---|---|
| Tır + müşteri spawn | 08:00-17:00 | 9/11 = 0.818 | 163.6 | 270 |
| Müşteri çıkış | 17:30 | 10.5/11 | 190.9 | 315 |
| Upgrade paneli | ≥10:00 | son 8/11 | — | — |
| Telefon | 08:00-18:00 (17:30 guard) | | | |

**Tır döngüsü (bir hangar):** `enterAnim(A_in) → hangar sayacı H(P) veya dolunca → exitDelay(2·e_exit) → exitAnim(A_out) → respawn U(3,5)`.
Tır ömrü = `A_in + min(H, t_dolum) + 2·e_exit + A_out + 4`. `A_in`, `A_out` VARSAYIM (3 s + 3 s). Tır kargo `c ~ U{cmin(P) … cmax(P)−1}`. Tek renk (gün ≤12), gün 13+ 2-3 renk.
Bir hangarın günlük tır sayısı üst sınırı ≈ `W_truck / (H + 12)` → 1P (H=120): 163.6/132 ≈ **1.2 tır/gün** (gün 1), 330·0.818/132 ≈ 2.0 (gün 16). 4P (H=30): 163.6/42 ≈ 3.9 → 6.4 tır/gün. **Tır sayısı kargoyla çarpılınca tır-kapasitesi tavanı:** 1P ≈ 1.2-2.0 × 1.5 = 1.8-3 kutu/gün; 4P ≈ 3.9-6.4 × 3 = 12-19 kutu/gün (dolum süresi ihmal edilirse).

**Müşteri akışı:** spawn `k+1` en erken `t_k + I(P)·(1+U(−0.2,0.2)) / rate(saat)`; ayrıca kuyruk < 2 ve kota kalmış olmalı. Engellenen spawn ertelenir (kaybolmaz). Telefon çağrısı bekleme şartını atlar (spawn'ı hemen yapar) ve `elapsed += sk(P)` ekler.

Zaman-ağırlıklı ortalama wave çarpanı (08-17): `(4/1.0 + 2/1.5 + 1/0.5 + 1/0.8 + 1/1.3)/9 = (4 + 1.333 + 2 + 1.25 + 0.769)/9 = 1.039` → etkin aralık ≈ `1.04·I(P)`: 1P 45.7 s, 2P 22.9 s, 3P/4P 21.8 s. Spawn penceresi 163.6 s (gün 1) → **spawn kapasitesi 1P ≈ 3.6, 2P ≈ 7.1, 4P ≈ 7.5 müşteri/gün** (kuyruk hiç dolmazsa). Kota 1P 4 / 2P 7 / 3-4P 8 → **kota, aralık nedeniyle gün 1'de zar zor / P3-P4'te hiç yetişmiyor** (gün uzadıkça rahatlar: gün 16'da 270 s → 5.9 / 11.8 / 12.4 vs kota 6/12/13). Telefon tam da bu açığı kapatmak için var.

**Sabır:** müşteri kuyruk noktasına varınca `W ~ U(min(P), max(P))` (× event) başlar; istasyon boşsa oyuncunun E'si gelene kadar sayar. Yürüme süresi (spawn→kuyruk) VARSAYIM 2 s.

**Telefon atlama (gerçek sn):** `sk(P) = m(P) · (200·ovt/11)/60 · perk(0.8) · cs(0.5)` → 34.8 / 14.8 / 14.2 / 14.2 s (perksiz). Gün 16'da bu, günün %10.6 / %4.5'i.

**Erken gün bitişi:** kota tükenip kuyruk boşalınca 30 s sonra gün biter → kalan tır penceresi kaybolur (yarım tır 30 s içinde doldurulmalı).

## 2. Üretim zinciri (kutu = para)

```
Müşteri (tedarik) --E 2s--> ürün (resepsiyon masası) --oyuncu taşır--> paketleme masası (S sn meşgul)
   --> renkli kutu (kırmızı/sarı/mavi, ürün kategorisine göre ≈ uniform 1/3) --> raf/stok
   --> tır ister renk X, kargo c --oyuncu taşır t_load--> teslim: +R(P)+5·floor(prestij/8)
İade müşterisi (gün 5+, %25): stoktan renk-X kutu ALIR (−1 kutu), +0.4 prestij, 0 TL
```

**Günlük ürün arzı:** `S_d = Σ_{servis edilen tedarik müşterisi} n_ürün`, `n_ürün = 1` (gün ≤8), `2` (gün ≥9). İade müşterileri ürün vermez.
**Kutu üretimi:** `B_d = min(S_d, kap_pack)`, `kap_pack = W_prod / S_pack` (tek masa; 2 masa ile ×2 — ancak oyuncu emeği de sınırlar).
**Teslimat:** `D_d = min(B_d + stok_{d−1}, kap_truck, renk-eşleşme)`. Renk eşleşmesi: tır tek renk istediği, kutu renkleri uniform geldiği için stokta yanlış renk birikir; 3 tırda 3 renk garanti olduğu için uzun vadede stok eritilir ama **kısa günde eşleşme kaybı** var (sim'de explicit stok/renk takibi).
**Emek kısıtı:** P oyuncu, her biri aynı anda tek iş: servis (t_serve), paketleme (t_pack), taşıma (t_load), telefon (1 s + yürüme). Günlük toplam emek `P · T_d` sn; iş süreleri §6.

## 3. Para modeli

**Gelir (gün d):**
`G_d = Σ_kutu [ (int)(R(P)·e_reward) + 5·floor(π/8) ] · vol − 40·e_pen·n_yanlışTeslim − 5·e_pen·n_düşme + festival_d + quest_{d−1}`
- `R(P)` = 50/55/70/88; `e_reward` event ödül çarpanı (tabana, int cast); `π` teslim anındaki prestij; `vol` = 1 (perk: U(0.80,1.50)); `e_pen` = 2 AUDIT günü.
- Festival: `U(0.10,0.20)·kira_şuanki` (yalnız o gün).
- Quest: kabul edilen tek quest'in ödülü ertesi gün başında (+28/60/150 veya −15/20/30).

**Gider:** kira (gün 4/8/12/16, gün sonu), upgrade/perk `round((base+lv·step)·0.5^{toplu}·e_cost·Pmult)`, reroll `{50,90,160,290,525}·Pmult`.

**Kira kapısı (gün k∈{4,8,12,16}):** `kasa ≥ kira → öde`; değilse `grace kullanılmadıysa → kasa·0.2 kalır, grace tükenir`; değilse `Acil Fren varsa → kira silinir, π −= 2`; değilse **İFLAS**. Kasa hiç eksiye inmez.

**16 gün kira toplamı (perksiz):** 1P 1 557 · 2P 3 489 · 3P 6 120 · 4P 8 750 TL. Ucuz Kira Lv3 (g=1.11): 1P 290+322+357+397=1 366 (−191) · 4P 1630+1809+2008+2229=7 676 (−1 074). Kaldıraçlı Kira (×0.75 tüm dönemler alındığı andan itibaren).

**Gelir tavanı (gün 1, tek hangar, kota bağlayıcı):** `kota × R(P)` = 1P 4×50=200 · 2P 7×55=385 · 3P 8×70=560 · 4P 8×88=704 TL/gün (prestij bonusu hariç). 4 günlük tavan vs ilk kira: 1P 800 vs 290 · 2P 1 540 vs 650 · 3P 2 240 vs 1 140 · 4P 2 816 vs 1 630 → **kota tavanına ulaşan takım için kira/gelir oranı %36-58**; sorun tavana ulaşılıp ulaşılamadığı (mekanik hız).

## 4. Prestij modeli

`π_{t+1} = clamp(π_t + Δ, 0, 100)`, ham `π_t + Δ ≤ 0 → KAYIP`.

| Olay | Δ | Günlük beklenen adet (orta profil, 2P, gün 8) |
|---|---|---|
| Müşteri servisi (tedarik veya iade tamam) | +0.4 | ≈ kota − kayıp |
| Telefon çağrısı | +0.4 | kullanım oranı × kota |
| Sabır bitti / gün sonu kuyrukta | −0.4 (×2 AUDIT) | |
| Kota spawn edilemedi | −0.2 | |
| Yanlış ürün | −0.20 | hata oranı × servis |
| Yanlış teslim | −0.16 | hata oranı × teslim |
| Kutu düştü | −0.04 | |
| Quest | +0.6/1.2/3.0 veya −0.32/0.4/0.6 | ≤1/gün |
| Acil Fren | −2 | |

**Ekonomik etki:** kutu başına `+5·floor(π/8)` TL. Eşikler 8, 16, 24, … 96 → maksimum +60 TL/kutu (π≥96). Başlangıç 12 → +5. Kapasite formülü ölü; prestijin **başka hiçbir kapısı yok**. Kayıp şartı π≤0: 12 prestij = 30 kaçan müşteri (−0.4) — kota 4-13/gün iken günde en fazla ~2-4 kaçış gerçekçi → **prestij ölümü pratikte yalnız 4+ gün üst üste tam çöküşle** mümkün (sim ölçecek).

## 5. Upgrade / perk / event modeli

Her kartın ekonomik fonksiyonu (sim'de birebir uygulanır):

| Kart | Sim etkisi |
|---|---|
| Geniş Ambar | stok kapasitesi +N slot (VARSAYIM taban 8 slot, +4/seviye) — kutu kaybını önler, gelir etkisi dolaylı |
| Paketleme İstasyonu | paketleme masası 1→2 (`kap_pack ×2`, çekişme azalır) |
| Ek Hangar | hangar 1→2 (tır kapasitesi ×2) |
| Görev Kademesi | quest tier 0→1→2 (teklif havuzu + ödül büyür) |
| Ucuz Kira Lv | g = 1.20−0.03·lv |
| Prestij Simsarı Lv | bonusPerTier 5+0.5·lv |
| Prestij Ustası Lv | servis prestiji 0.4+0.12·lv |
| Hızlı Hangar | H(P)×1.3 |
| Enerjik / Çevik Ekip | emek süreleri ×0.95 / ×0.87 (VARSAYIM: hız +%15 → yürüme payı; stamina → sprint kullanılabilirliği) |
| Sabırlı Müşteriler | E süresi 2→1.2 s (t_serve −0.8 s) |
| Kumarbaz Kasası | R×1.30, ceza 40→62 |
| Telefon Hattı | sk×0.8 |
| Mesai Saati | T_d taban 200→225 |
| Kaldıraçlı Kira | kira×0.75, grace=0 |
| Yüksek Volatilite | ödül×U(0.8,1.5) |
| Acil Fren | 1 iflas önleme, −2 π |
| Kelle Koltukta | R×1.25, grace=0 |
| Toplu Alım | ertesi gün 1 kart −%50 |

**Strateji profilleri (Aşama 3):**
- `hiç`: hiçbir kart alınmaz (para yalnız kira ve cezalara gider).
- `açgözlü`: her gün teklifteki en ucuz alınabilir kartı, kasada `kira_sonraki×0.5` pay bırakarak alır; reroll yok.
- `mantıklı`: öncelik sırası [Ek Hangar, Paketleme İstasyonu, Hızlı Hangar, Kelle Koltukta/Kumarbaz (tekli), Ucuz Kira, Prestij Ustası, Sabırlı Müşteriler, Mesai Saati, Telefon Hattı, Çevik Ekip, Görev Kademesi, Geniş Ambar]; alır ancak `kasa − fiyat ≥ sonraki kira × 0.6`; teklifte yoksa ve kasa boldaysa 1 reroll.

**Event modeli:** takvim algoritması birebir (seed'li), çarpanlar o günün ilgili parametrelerine uygulanır; FESTIVAL kira-bazlı; AUDIT cezalar ×2; CUSTOMER SUPPORT sk×0.5; hız çarpanları emek sürelerinin yürüme payına (VARSAYIM %60) uygulanır.

## 6. İnsan varsayımları (kodda YOK — parametrik, `config.json` `profiles`)

Gerekçe: kodda hiçbir üretim adımının zamanlı kapısı yok (`Table.cs` 0.1 s), bu yüzden hız = oyuncu. `PlayerMovement.moveSpeed=5`, `sprint 7` (m/s); harita ~40 m doğu-batı (GDD §33.2: x −68…−29) → resepsiyon↔paket ≈ 15 m (3 s yürüme), paket↔hangar ≈ 15 m (3 s), tur 6-8 s; kutu alma/koyma animasyonları 0.3 s; karar/yönelme payı insan kaynaklı. Profiller sim.js'in {Slow 1.2, Normal 2.0, Fast 3.0 kutu/dk} bandıyla uyumlu seçildi.

| Parametre | Zayıf | Orta | İyi | Birim | Gerekçe |
|---|---|---|---|---|---|
| `t_serve` — müşteriye E basmak için yürüyüp 2 s etkileşim | 10 | 7 | 5 | s emek | 2 s E + yürüme 3-8 s |
| `t_pack` — ürünü al, masaya taşı, kutula, kutuyu rafa koy | 40 | 26 | 18 | s emek | ≈ 1.5 / 2.3 / 3.3 kutu/dk (sim.js bandı) |
| `S_pack` — masanın kutu başına meşgul süresi | 8 | 6 | 5 | s masa | sim.js `tableBusySeconds=6` |
| `t_load` — raftan kutuyu tıra taşı | 14 | 10 | 7 | s emek | 15 m + geri |
| `t_phone` — telefona gidip 1 s basılı tutma | 6 | 5 | 4 | s emek | |
| `p_wrongProduct` — yanlış ürünle E | 0.08 | 0.04 | 0.01 | oran | öğrenme |
| `p_wrongDelivery` — yanlış renk tıra | 0.15 | 0.08 | 0.03 | oran | sim.js 0.22/0.12/0.07 (biraz iyimser) |
| `p_drop` — kutu düşürme (kutu başına) | 0.09 | 0.05 | 0.02 | oran | sim.js |
| `phone_use` — telefonu kullanma eğilimi (fırsat başına) | 0.05 | 0.30 | 0.60 | oran | zayıf oyuncu telefonu keşfetmez; iyi oyuncu boş anda çağırır |
| `reaction` — boşta oyuncunun yeni işe başlama gecikmesi | 2.0 | 1.0 | 0.5 | s | |
| `A_in`, `A_out` — tır animasyonları | 3 / 3 | 3 / 3 | 3 / 3 | s | BULUNAMADI, sabit |
| `walk_share` — emek sürelerinin yürüme payı (hız event/perk'i buna uygulanır) | 0.6 | 0.6 | 0.6 | oran | |
| `shelf_slots` — başlangıç raf kapasitesi | 8 | 8 | 8 | kutu | VARSAYIM (Geniş Ambar +4/sv) |

**Duyarlılık (Aşama 3.4'te koşulacak):** `t_pack` ±%30, `phone_use` 0-1, `S_pack`, `p_wrongDelivery`.

## 7. Simülasyon algoritması (özet — `sim.py`)

Zaman adımı `dt = 0.5 s`. Gün başında: kota, event, tır spawn, quest teklifi/kabulü. Her adımda:
1. Müşteri spawn kuralı (§1); telefon fırsatı: kuyruk boş ∧ kota kalmış ∧ cooldown yok ∧ tahmin < 17.5 ∧ boş oyuncu → `phone_use` olasılığı ile çağır.
2. Sabır sayaçları; biten müşteri kaçar (−0.4).
3. Boş oyuncu iş seçer (öncelik): (a) istasyondaki müşteriye servis, (b) tır bekliyor ∧ stokta uygun renk → yükle, (c) resepsiyonda ürün var ∧ masa boş → paketle, (d) yoksa bekle. Görev süreleri §6; hata zarları görev sonunda.
4. Tır: sayaç, dolum, çıkış, respawn; 17:00 zorla çıkış.
5. 17:30 kuyruk temizliği + kota cezası; erken bitiş kontrolü.
Gün sonu: kira kapısı, quest settle (ertesi güne), upgrade draft (strateji), prestij/para kaydı. N koşu × seed; çıktı: gün-gün ortalama ve P10.
