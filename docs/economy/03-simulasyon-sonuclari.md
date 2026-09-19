# 03 — Simülasyon Sonuçları

**Tarih:** 2026-09-18 · **Araç:** `tools/economy_sim/sim.py` (Python 3.14, stdlib + matplotlib 3.11) · **Parametreler:** `tools/economy_sim/config.json` (değerler `01-parametreler.md`'den, model `02-model.md`)

> ### ⚠️ Bu bölümdeki TÜM sayılar denetim anındaki (Ö1/Ö2/Ö3 **uygulanmadan önceki**) ekonomiye aittir.
> 2026-09-19'da Ö1+Ö2+Ö3 canlıya alındı, yani `tools/economy_sim/config.json` artık **yeni** değerleri taşıyor.
> Buradaki baseline'ı yeniden üretmek için donmuş kanıt dosyasını kullan:
> **`tools/economy_sim/config_baseline_pre.json`** (denetim anının birebir kopyası — değiştirme).

Tekrar üretmek için:
```bash
# BASELINE (bu raporun anlattığı durum) — grafikler de bununla üretildi
python tools/economy_sim/sim.py --config tools/economy_sim/config_baseline_pre.json --runs 300 --figs
python tools/economy_sim/sim.py --config tools/economy_sim/config_baseline_pre.json --payback --no-matrix --figs
python tools/economy_sim/sim.py --config tools/economy_sim/config_baseline_pre.json --event-iso --no-matrix --tag _eviso
python tools/economy_sim/sim.py --config tools/economy_sim/config_baseline_pre.json --sensitivity --no-matrix --tag _sens
python tools/economy_sim/sim.py --config tools/economy_sim/config_baseline_pre.json --single --trace-day 4 \
       --cells P=2,profile=orta,strategy=mantikli,events=on

# CANLI (Ö1+Ö2+Ö3 sonrası) durumu görmek için — varsayılan config
python tools/economy_sim/sim.py --runs 300 --tag _post_uygulama
```

**Kanıt dosyası haritası** (`tools/economy_sim/results/`):

| Dosya | Hangi durum |
|---|---|
| `matrix.csv`, `daily.csv`, `summary.json` | **BASELINE** — bu raporun tüm tabloları |
| `summary_eviso.json`, `summary_sens.json`, `summary_F1.json`, `summary_REC.json` | BASELINE (ek koşular) |
| `proposals.json` | BASELINE taban + 20 öneri paketi (`proposals.py`, varsayılan taban artık donmuş baseline) |
| `matrix_post_uygulama.csv`, `summary_post_uygulama.json` | **Ö1+Ö2+Ö3 SONRASI** doğrulama koşusu |
| `docs/economy/fig-*.png` | BASELINE (rapor metniyle uyumlu) |
**Determinizm doğrulandı:** aynı seed ile iki koşunun çıktısı bit-bit aynı (md5 `d6cb4ad8…`). Varyans RNG'den gelir (spawn jitter, kargo, renk torbası, hata zarları, event takvimi, draft), sim.js'in aksine model **stokastiktir**.

## 0. Model doğrulaması (model bug'ı yakalandı)

İlk koşuda 2P/gün 4'te anormal sonuç çıktı (1 servis / 5 kayıp / %89 boşta). `--trace-day` ile saniye saniye izlendi: alışverişe giden oyuncunun **o an yürüttüğü görev eziliyordu** (`idle["task"] = ("shop",)`), böylece servis edilen müşteri kalıcı olarak "servis ediliyor" sanılıp kuyruğu kilitliyordu. Düzeltildi (`sim.py`, alışveriş artık yalnız meşguliyet ekler). Rapordaki tüm sayılar düzeltme **sonrası**.

## 1. Tam senaryo matrisi (72 hücre × 300 koşu, 43 s)

Oyuncu sayısı 1-4 × beceri (zayıf/orta/iyi) × strateji (hiç/açgözlü/mantıklı) × event (açık/kapalı). Aşağıdaki tablo **event açık** hücreleri gösterir (event kapalı sonuçlar `matrix.csv`'de; fark ±%5 içinde, bkz. §4).

| Beceri | Strateji | 1P kayıp / kasa16 | 2P | 3P | 4P |
|---|---|---|---|---|---|
| zayıf | hiç | **19.7%** / 218 | 21.3% / 465 | 45.3% / 901 | **61.0%** / 1.420 |
| zayıf | açgözlü | 100% / — | 100% / — | 100% / — | 100% / — |
| zayıf | mantıklı | 99.3% / — | 95.7% / — | 99.0% / — | 100% / — |
| orta | hiç | **0%** / 1.131 | **0%** / 3.392 | **0%** / 4.475 | **0%** / 4.575 |
| orta | açgözlü | 98.0% / — | 78.3% / — | 90.7% / — | 97.3% / — |
| orta | mantıklı | 64.0% / 256 | 24.0% / 812 | 27.3% / 1.316 | 49.3% / 1.639 |
| iyi | hiç | **0%** / 2.474 | **0%** / 6.671 | **0%** / 7.747 | **0%** / 8.558 |
| iyi | açgözlü | 56.0% / 345 | 17.0% / 2.912 | 35.3% / 2.950 | 62.3% / 2.273 |
| iyi | mantıklı | 19.7% / 562 | **1.7%** / 3.395 | 2.3% / 3.746 | 7.3% / 3.617 |

(kasa16 = gün 16 sonu ortalama TL, yalnız hayatta kalanlar; kayıp = iflas + prestij ölümü oranı)

**En çarpıcı sonuç: hiç upgrade almamak, 12 hücrenin 12'sinde hem en güvenli hem en zengin strateji.** Orta/iyi beceride "hiç" %0 kayıpla 1.131-8.558 TL bitirirken, aynı hücrede "mantıklı" %1.7-64 kayıpla 256-3.746 TL bitiriyor. Grafik: `fig-bankruptcy-heatmap.png`, `fig-playercount-compare.png`.

## 2. Gün-gün seyir

Grafikler: `fig-money-by-day.png` (kasa, ortalama + alt %10), `fig-prestige-by-day.png`.
> Uyarı: grafikler yalnız **hayatta kalan** koşuların ortalamasıdır (ölen koşu kayıt bırakmaz) → gün 8 sonrası survivor bias vardır. Kayıp oranları §1 ve §3'ten okunmalıdır.

**2P / orta / mantıklı / event açık** (300 koşu):

| gün | 1 | 2 | 3 | **4** | 5 | 6 | 7 | **8** | 9 | 10 | 11 | **12** | 13 | 14 | 15 | **16** |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| kasa (ort) | 686 | 598 | 654 | **119** | 421 | 675 | 783 | **220** | 562 | 982 | 1.082 | **360** | 1.095 | 1.423 | 1.604 | **838** |
| kasa (p10) | 600 | 494 | 525 | **33** | 226 | 465 | 563 | **44** | 288 | 669 | 842 | **82** | 688 | 1.065 | 1.124 | **214** |
| gelir | 95 | 199 | 254 | 306 | 299 | 284 | 272 | 336 | 426 | 538 | 590 | 649 | 811 | 803 | 863 | 1.022 |
| servis | 5.8 | 5.8 | 5.9 | 6.6 | 6.5 | 7.2 | 7.0 | 7.0 | 6.4 | 6.7 | 6.7 | 7.4 | 7.4 | 7.1 | 7.8 | 8.0 |
| kaçan müşteri | 1.0 | 0.9 | 0.9 | 1.1 | 1.4 | 1.6 | 1.8 | 1.8 | **3.2** | 3.0 | 3.2 | 3.3 | 3.4 | 3.6 | 3.9 | 3.7 |
| teslim kutu | 1.6 | 3.2 | 3.9 | 4.7 | 4.3 | 3.9 | 3.8 | 4.5 | 5.7 | 7.1 | 7.7 | 8.1 | 9.4 | 9.1 | 9.3 | 10.5 |
| boşta oyuncu | 18% | 16% | 15% | 15% | 19% | 20% | 21% | 19% | 14% | 8% | 6% | 6% | 6% | 6% | 7% | 6% |
| gün erken bitti | 99% | 98% | 98% | 69% | 92% | 61% | 76% | 96% | 55% | 61% | 75% | 56% | 69% | 86% | 62% | 89% |
| prestij | 14 | 17 | 20 | 22 | 25 | 29 | 32 | 35 | 37 | 39 | 41 | 43 | 46 | 48 | 51 | 54 |

Belirgin desenler:
- **Testere dişi**: kira günleri (4/8/12/16) kasayı %65-85 süpürüyor; p10 satırı gün 4'te 33 TL, gün 8'de 44 TL, gün 12'de 82 TL, gün 16'da 214 TL'ye iniyor.
- **Gün 9 kırılması**: kaçan müşteri 1.8 → 3.2'ye zıplıyor (dual-item modu: her müşteri 2 ürün, etkileşim ×1.3 → tek istasyon tıkanıyor).
- **Gün erken bitiyor** (%55-99): kota tükenip kuyruk boşalınca gün sarılıyor; oyun günün tamamını neredeyse hiç kullanmıyor.
- Boşta kalma erken günlerde %15-21, sonra %6'ya iniyor (üretim zinciri doluyor).

**4P / orta / mantıklı** aynı kesit: boşta kalma **%15-28** (en yüksek), kaçan müşteri gün 5'ten sonra 1.2 → 3.3, kasa gün 4'te 246'ya, gün 12'de 798'e düşüyor, kayıp %49.3.
**4P / iyi / hiç**: prestij gün 16'da **96** (tavan 100'e dayanıyor), kasa 8.558 TL, %0 kayıp, boşta %15-26.
**1P / orta / mantıklı**: gelir 38-291 TL/gün, teslim 0.7-4.3 kutu/gün, prestij gün 16'da yalnız **26**, kayıp %64.

## 3. İflas günü dağılımı (event açık tüm hücreler, 5.016 ölüm)

| Gün 4 | Gün 8 | Gün 12 | Gün 16 | Prestij ölümü |
|---|---|---|---|---|
| **0** (%0) | **3.608** (%71.9) | 1.132 (%22.6) | 276 (%5.5) | **0** (%0) |

- **Gün 4 hiçbir koşuda öldürmüyor** — başlangıç parası (500-864) ilk kirayı (290-1.630) + grace ile her zaman karşılıyor.
- **Gün 8 tek gerçek katil** (ölümlerin %72'si): gün 4'te kasa dibe iniyor (p10 = 33 TL), grace çoğu koşuda gün 4'te yanıyor, gün 8'in ×1.2 kirası karşılıksız kalıyor.
- **Prestij ölümü sıfır.** 300×72 = 21.600 koşuda prestij hiç 0'a inmedi (en düşük gözlenen gün-sonu prestij 12.6). Prestij bir kaybetme yolu olarak **fiilen ölü**.

## 4. Event'lerin etkisi

Event'siz vs event'li aynı hücrede fark: 72 hücre boyunca |Δkasa16| ortalama **%4.4**, |Δkayıp| ortalama **2.1 puan** — yönü bile tutarsız (bazı hücrelerde event'li daha kötü). Yani **event katmanı ekonomiyi kayda değer biçimde değiştirmiyor**.

Her event'in izole etkisi (2P orta mantıklı, event her uygun günde zorlanmış; baz kayıp %24.3):

| Event | TL/gün | gelir/gün | prestij/gün | kayıp% |
|---|---|---|---|---|
| GOLDEN BOX DAY | **+36.9** | +143.9 | +0.25 | 21.3 |
| DELIVERY BONUS | +17.2 | +82.1 | −0.01 | 23.3 |
| FESTIVAL DAY | +16.4 | +45.1 | −0.12 | 22.7 |
| OPPORTUNITY DAY | +8.9 | +8.3 | 0.00 | 24.7 |
| VIP SERVICE | +8.8 | +48.6 | −0.01 | 24.3 |
| RELAXED DAY | +6.8 | +30.2 | +0.32 | 23.3 |
| EXPRESS CARGO | +2.8 | +35.5 | +0.04 | 23.0 |
| CUSTOMER SUPPORT | −1.9 | +7.2 | +0.13 | 25.3 |
| BUSY DAY | −5.4 | +6.1 | −0.34 | 24.3 |
| ANGRY CUSTOMERS | −9.6 | −39.1 | −0.47 | 29.0 |
| SLOW LOGISTICS | −14.8 | −48.6 | +0.02 | 26.3 |
| HEAVY BOXES | −18.6 | −65.3 | −0.34 | 28.0 |
| SURPRISE AUDIT | −21.3 | −55.7 | **−1.03** | 27.7 |
| MARKETING DAY | −23.1 | −109.2 | +0.18 | 25.3 |
| RAINY DAY | −28.7 | −161.6 | −0.56 | **34.0** |
| FATIGUE PROBLEM | −31.5 | −154.2 | −0.60 | 32.3 |

Bant: −31.5 … +36.9 TL/gün. 16 günde ~7 event günü → toplam etki **±150 TL**, gün-16 kasasının %4'ü. En güçlü pozitif (GOLDEN BOX) ile en güçlü negatif (FATIGUE) arasındaki fark bir günün gelirinin ~%10'u kadar.
**BUSY DAY (+%35 kota) neredeyse etkisiz** (+6 TL gelir/gün): varış aralığı değişmediği için ekstra müşteri spawn olamıyor — GDD §7.2'deki "ölü kol" uyarısı sayıyla doğrulandı.

## 5. Kart amortismanı (2P orta, event'siz, kart gün 1'de alınır → gün 2'de aktif, 15 gün çalışır)

Yöntem: baz koşu (hiç kart) ile tek-kart koşusu karşılaştırıldı; kazanç = Δgelir + Δkira tasarrufu + Δceza tasarrufu. Grafik: `fig-upgrade-payback.png`.

| Kart | Maliyet (2P) | Kazanç/gün | **Amortisman** | Kasa16 (baz 3.333) | Prestij (baz 54.9) | Kayıp% |
|---|---|---|---|---|---|---|
| Ek Hangar | 400 | +57.4 | **7.0 gün** ✅ | 3.786 | 49.4 | 0.0 |
| Kumarbaz Kasası | 700 | +73.4 | **9.5 gün** ✅ | 3.728 | 55.2 | 0.7 |
| Kaldıraçlı Kira | 600 | +55.4 | **10.8 gün** ✅ | 3.559 | 54.3 | 2.0 |
| Yüksek Volatilite | 640 | +58.1 | **11.0 gün** ✅ | 3.553 | 55.1 | 0.0 |
| Kelle Koltukta | 640 | +46.7 | **13.7 gün** ⚠️ | 3.366 | 51.5 | **11.7** |
| Prestij Ustası | 750 | +49.3 | **15.2 gün** ⚠️ | 3.317 | **79.6** | 1.0 |
| Prestij Simsarı | 550 | +25.6 | 21.5 gün ❌ | 3.161 | 54.9 | 0.3 |
| Çevik Ekip | 360 | +18.0 | 20.0 gün ❌ | 3.243 | 57.2 | 0.0 |
| Enerjik Ekip | 200 | +7.5 | 26.8 gün ❌ | 3.245 | 56.8 | 0.0 |
| Ucuz Kira (3 sv) | 960 | +35.6 | 27.0 gün ❌ | 2.891 | 53.1 | 6.7 |
| Sabırlı Müşteriler | 240 | +4.8 | 50.4 gün ❌ | 3.156 | 55.4 | 0.0 |
| Telefon Hattı | 320 | +5.8 | 55.3 gün ❌ | 3.091 | 55.7 | 0.0 |
| Mesai Saati | 600 | +4.6 | 131 gün ❌ | 2.796 | 53.0 | 1.7 |
| Toplu Alım | 160 | +0.7 | 218 gün ❌ | 3.178 | 54.9 | 0.0 |
| Acil Fren | 500 | +2.0 | 255 gün ❌ | 2.857 | 54.9 | 0.0 |
| Geniş Ambar (2 sv) | 300 | +0.9 | 341 gün ❌ | 3.040 | 54.9 | 0.0 |
| Görev Kademesi (2 sv) | 180 | +0.2 | 1.028 gün ❌ | 3.149 | 54.7 | 0.0 |
| **Hızlı Hangar** | 240 | **−5.3** | **asla** 🔴 | 3.016 | 55.5 | 0.0 |
| **Paketleme İstasyonu** | 300 | **−12.3** | **asla** 🔴 | 2.846 | 49.4 | 0.0 |

**19 kartın yalnız 6'sı 16 günlük oyunda kendini amorti ediyor.** 11'i ölü yatırım, 2'si aktif olarak zararlı:
- **Hızlı Hangar** hangar süresini ×1.3 *uzatıyor* → tır devir hızı düşüyor. 2P'de (kargo 2-3, üretim yavaş) net zarar; adı yaptığının tersini ima ediyor.
- **Paketleme İstasyonu** 2. masayı açıyor ama darboğaz masa değil **oyuncu emeği**: fazla paketleme kapasitesi oyuncuyu 26 s'lik paketleme görevlerine bağlıyor, servis ve tır yükleme gecikiyor → prestij 54.9 → 49.4 düşüyor, gelir düşüyor.
- Ucuz Kira 3 seviyeye toplam 960 TL (2P) istiyor; T3 kilidi gün 9 olduğu için yalnız 2 kira dönemini etkiliyor → 27 gün amortisman.
- Kelle Koltukta amorti ediyor (13.7 gün) ama grace'i sildiği için **kayıp oranını %0 → %11.7'ye çıkarıyor**.

## 6. Duyarlılık taraması (orta/mantıklı/event açık, 200 koşu; kasa16 / kayıp%)

| Varyant | 1P | 2P | 3P | 4P |
|---|---|---|---|---|
| **baz** | 256 / 62.5% | 810 / 23.0% | 1.318 / 27.5% | 1.606 / 51.0% |
| `t_pack` −%30 (paketleme hızlı) | 286 / 48.0% | 1.390 / **11.0%** | 1.853 / **12.5%** | 1.741 / 28.0% |
| `t_pack` +%30 (yavaş) | 283 / **90.0%** | 576 / 53.5% | 1.024 / 56.0% | 1.625 / 77.5% |
| `phone_use` = 0 (telefon yok) | 246 / 54.5% | 862 / **11.5%** | 1.400 / 20.0% | 1.772 / 38.5% |
| `phone_use` = 1 (telefon spam) | 258 / 64.0% | 685 / 31.0% | 1.151 / 32.5% | 1.573 / 49.5% |
| `S_pack` = 3 (masa hızlı) | 256 / 62.5% | 798 / 24.5% | 1.243 / 23.0% | 1.603 / 42.5% |
| `S_pack` = 10 (masa yavaş) | 256 / 62.5% | 832 / 23.0% | 1.352 / 37.0% | 1.609 / 57.0% |
| `p_wrongDelivery` = 0 | 250 / 49.0% | 1.187 / **9.5%** | 1.961 / 14.5% | 2.007 / 24.0% |
| `p_wrongDelivery` = 0.20 | 281 / 86.5% | 588 / 58.5% | 1.044 / 69.0% | 1.659 / 89.0% |
| `rentGrowth` = 1.10 | 221 / 55.0% | 858 / 15.0% | 1.420 / 18.5% | 1.593 / 40.0% |
| `rentGrowth` = 1.35 | 381 / 86.0% | 712 / 37.5% | 1.220 / 50.0% | 1.995 / 77.5% |
| hangar süresi ×0.75 | 262 / 59.0% | 779 / 20.0% | 1.348 / 34.0% | 1.791 / 54.5% |
| varış aralığı ×0.7 (daha sık müşteri) | 252 / 72.0% | 558 / **48.0%** | 1.024 / 44.0% | 1.499 / 65.5% |

- **En duyarlı girdi paketleme/üretim hızı** (`t_pack`): ±%30 → 2P kayıp %11 ↔ %53.5. Modelin en kritik varsayımı; playtestte ölçülmesi gereken 1 numaralı sayı.
- **İkinci: yanlış teslim oranı.** 0 → %9.5, 0.20 → %58.5. 40 TL ceza + −0.16 prestij, dar kasada ölümcül.
- **Telefon spam artık zararlı** (`phone_use=1` her hücrede daha kötü) → 2026-09-18'deki `callMoneyReward: 20→0` düzeltmesi sömürüyü gerçekten kapatmış.
- **Daha sık müşteri (aralık ×0.7) ekonomiyi KÖTÜLEŞTİRİYOR** (2P kayıp %23 → %48): servis kapasitesi sabit olduğu için fazla müşteri yalnız −0.4 prestij yakıtı oluyor. "Kota/talep artır" tipi kaldıraçların ters teptiğinin ikinci kanıtı.
- `rentGrowth` 1.35'te 1P kasası artıyor gibi görünüyor (381) — bu survivor bias: %86 koşu ölüyor, kalanlar iyi şans serisi yaşamış olanlar.

## 7. `tools/economy-sim/sim.js` (v5.1) ile çapraz kontrol

Karşılaştırılabilir hücre: 2P, upgrade harcaması yok (sim.js hiç upgrade almıyor) = benim `P2_orta_hic`.

| Büyüklük | sim.js (Normal/strict) | bu sim (`P2_orta_hic_ev`) | Fark |
|---|---|---|---|
| Gün 16 geliri | 706 TL | 725 TL | **+2.7%** |
| Gün 16 teslim kutu | 8.0 | 8.5 | +6% |
| Gün 16 kasa | 3.837 TL | 3.392 TL | −11.6% |
| Gün 16 prestij | 77.7 | 54.0 | **−31%** |
| Gün 1 geliri | 258 TL | 95 TL | **−63%** |
| Kira (4/8/12/16) | 650/780/936/1.123 | aynı | 0% |

Yorum: geç-oyun geliri ve kira birebir örtüşüyor (model doğrulandı). İki sistematik fark açıklanabilir:
1. **Prestij (−31%)**: sim.js müşteri sabrını/kaçışını modellemiyor (kendi notu: "kayıplar ALT SINIR"); bu sim sabır sayacını simüle ediyor ve 2P'de günde 1-3.9 müşteri kaybediyor. Bu simin sonucu daha kötümser ve daha gerçekçi.
2. **Gün 1 geliri (−63%)**: sim.js kapalı-form kapasite hesabı yapıyor; bu sim üretim zincirinin ilk dolma gecikmesini (ürün → paketleme → renk eşleşmesi → tır penceresi) ve stok renk uyuşmazlığını simüle ediyor. Erken günlerde sim.js iyimser.
3. sim.js'te `exitDelay` 5 (canlı prefab 2) ve quest Medium/Hard cezaları eski — bu sim canlı değerleri kullanıyor (bkz. `01-parametreler.md` §K/§L).

## 8. Bu simülasyonun bilinen sınırları

- Oyuncu iş seçimi sabit önceliklidir (servis → tır yükleme → paketleme → telefon); gerçek oyuncular rol paylaşımı yapabilir (bir kişi hep tırda). Bu, boşta-kalma sayılarını bir miktar abartabilir.
- Yürüme mesafeleri tek bir "emek süresi" olarak modellenmiştir; oda topolojisi (GDD §33) ayrıntılı simüle edilmez.
- Ürün kategorisi → kutu rengi eşlemesi uniform 1/3 varsayılır.
- Raf kapasitesi (Geniş Ambar) modelde bağlayıcı olmadığı için o kartın değeri ~0 çıkıyor; gerçekte raf taşması olabilir (kodda taşma cezası yok).
- Quest ilerlemesi yalnız raf/paket/tır/telefon sayaçlarıyla ölçülür; renk-kilitli görevlerde renk uyuşması rastgele seçilir.
- Animasyon süreleri (tır giriş/çıkış) BULUNAMADI, 3+3 s varsayıldı.
