# 04 — Analiz (8 soru, sayılarla)

Kanıt kaynağı: `03-simulasyon-sonuclari.md` ve `tools/economy_sim/results/{matrix,daily}.csv`, `summary*.json`. Hücre adı biçimi `P<n>_<beceri>_<strateji>_<ev|noev>`.

---

## Soru 1 — Her oyuncu sayısı ve beceri profili için gün gün para, prestij, kota durumu

**Gün 16 sonuçları (strateji "hiç", event açık, 300 koşu — en temiz taban):**

| Beceri | 1P | 2P | 3P | 4P |
|---|---|---|---|---|
| kasa16 (zayıf) | 218 · kayıp %19.7 | 465 · %21.3 | 901 · %45.3 | 1.420 · **%61.0** |
| kasa16 (orta) | 1.131 · %0 | 3.392 · %0 | 4.475 · %0 | 4.575 · %0 |
| kasa16 (iyi) | 2.474 · %0 | 6.671 · %0 | 7.747 · %0 | 8.558 · %0 |
| prestij16 (zayıf/orta/iyi) | 12.9 / 25.0 / 37.5 | 24.9 / 54.2 / 77.0 | 41.3 / 71.4 / 91.7 | 44.0 / 78.1 / **96.3** |
| teslim kutu/gün (gün 16) | 2.3 / 3.5 / 5.2 | 5.2 / 8.5 / 10.9 | 6.2 / 10.2 / 11.7 | 7.4 / 10.6 / 12.5 |
| kota karşılama (servis/kota) | %46 / 57 / 78 | %54 / 73 / 84 | %70 / 75 / 83 | %65 / 76 / 83 |

Gün gün seyir tabloları `03` §2'de. Özet desen: kasa 4 günlük testere dişi çiziyor (kira günleri %65-85 süpürüyor), prestij monoton artıyor, kota **hiçbir hücrede tam karşılanmıyor** (en iyi %84).

**Kritik asimetri:** kota karşılama oranı beceriyle belirleniyor, oyuncu sayısıyla değil (2P→4P arasında %54→%65 gibi küçük fark). Prestij ise oyuncu sayısıyla 2 kata kadar açılıyor (1P orta 25.0 vs 4P orta 78.1) — çünkü prestij servis **sayısına** bağlı ve 4P günde 9-10 müşteri servis ederken 1P 3.4 ediyor.

## Soru 2 — Batma riski hangi günlerde, hangi senaryolarda?

**5.016 ölümün tamamı kira kapılarında; dağılım:**

| Gün 4 | Gün 8 | Gün 12 | Gün 16 | Prestij ölümü |
|---|---|---|---|---|
| **0** | **3.608 (%71.9)** | 1.132 (%22.6) | 276 (%5.5) | **0** |

- **Gün 4 hiç öldürmüyor.** Başlangıç parası (500/600/720/864) + 3 günlük gelir, ilk kirayı (290/650/1.140/1.630) + grace ile her koşuda karşılıyor. İlk kira kapısı fiilen bir formalite.
- **Gün 8 oyunun tek gerçek katili.** Mekanizma: gün 4'te kasa dibe iner (2P p10 = 33 TL), grace çoğu koşuda gün 4'te tükenir, gün 8'in ×1.20 kirası (2P 780 TL) karşılıksız kalır. Ölüm gün 8'de görünür ama **kararı gün 3-4'teki harcama verir**.
- **Risk yoğunlaşması:** ölümler ezici biçimde upgrade satın alan stratejilerde. "hiç" stratejisinde orta/iyi beceride kayıp **%0**; "mantıklı"da %1.7-64; "açgözlü"de %17-100. Zayıf beceri + herhangi bir alım = %95-100 kayıp.
- **Kira günleri kritik, evet** — ama tek kritik şey onlar. Aradaki 3 gün tamamen risksiz: kasa 0'a inse bile ceza yutulur (`MoneySystem` 0'ın altına inmez), oyun devam eder.

## Soru 3 — Zorluk eğrisi mantıklı mı? Oyun çözülüyor mu, imkânsızlaşıyor mu?

**İkisi birden oluyor — stratejiye göre ayrışıyor:**

1. **"Hiç upgrade alma" yolunda oyun gün 5'ten sonra çözülüyor.** 2P orta: kasa gün 5'te 1.056 → gün 16'da 3.392 TL; hiçbir kira karşılıksız kalmıyor, kayıp %0. Para birikiyor ve **harcanacak yer kalmıyor** (upgrade almak istatistiksel olarak zarar, bkz. Soru 5). 4P iyi'de son kasa 8.558 TL — 16 günlük tüm kiraların (8.750) neredeyse tamamı kadar atıl para.
2. **Upgrade alan yolda oyun gün 8'de imkânsızlaşabiliyor** (%24-64 kayıp orta beceride). Yani zorluk eğrisi değil, **strateji uçurumu** var: aynı beceride iki yol %0 ve %64 kayıp veriyor.
3. **Gerçek zorluk artışı gün 9'da geliyor ama ekonomik değil mekanik:** dual-item modu (her müşteri 2 ürün, etkileşim ×1.3) tek servis istasyonunu tıkıyor, kaçan müşteri 1.8 → 3.2'ye zıplıyor (2P). Bu prestij kaybı, kutu-başı ödülü düşürerek dolaylı gelir kaybı yaratıyor.
4. **Gün uzunluğu artışı (200→330 s) zorluk DÜŞÜRÜYOR**: kota 7→12 (%71) artarken gün süresi %65 artıyor ve kutu-başı ödül prestijle 55→80 TL'ye çıkıyor. Sonuç: gelir/gün 95 → 1.022 TL (10 kat), kira 650 → 1.123 TL (1.7 kat). **Geç oyun erken oyundan belirgin biçimde kolay.**

Zorluk eğrisi özeti: gün 1-3 kolay (risk yok) → gün 4 formalite → **gün 8 duvar** → gün 9 mekanik sıçrama → gün 10-16 giderek kolaylaşan bolluk. Tek tepe noktası gün 8.

## Soru 4 — Prestij formülü anlamlı bir karar yaratıyor mu?

**Hayır — prestij fiilen otomatik artan bir sayaç.**

- 21.600 koşunun **hiçbirinde** prestij 0'a inmedi (kaybetme yolu ölü). En düşük gözlenen gün-sonu değeri 12.6 (başlangıç 12).
- Prestij bilançosu her hücrede pozitif: 2P orta günde +2.4 net (servis 6.7×0.4 + telefon 1.5×0.4 − kayıp 3.2×0.4 − küçük cezalar). Kaybın prestiji düşürmesi için günde **~7 müşteri** kaçması gerekir; kota 2P'de 7-12, yani ancak tam çöküşte.
- Tek ekonomik etkisi kutu-başı ödül: `+5 TL / 8 prestij`. 2P orta'da gün 16'da prestij 54 → +30 TL/kutu (taban 55'in %55'i). Bu **büyük** bir etki ama oyuncu kararı değil, zamanın fonksiyonu.
- `PrestigeManager.GetCustomerCapacity()` (1+prestij/4) hiçbir yerde tüketilmiyor → prestijin ikinci kapısı yok (`01-parametreler.md` §K).
- Tavan sorunu: 4P iyi gün 16'da **96.3** (tavan 100) — tavana dayanıyor, yani en güçlü takımda prestij kazancı son günlerde boşa gidiyor. 1P'de ise 25-37, yani aynı sistem 1P'de neredeyse hiç ödül vermiyor: **prestij ödülü oyuncu sayısına göre 2.6 kat asimetrik.**

Sonuç: prestij ne bir risk (ölüm imkânsız) ne bir karar (otomatik artıyor); yalnız bir gelir çarpanı ve o çarpan P ile adaletsiz dağılıyor.

## Soru 5 — İşe yaramayan veya zorunlu upgrade var mı?

**19 aktif karttan yalnız 6'sı 16 günlük oyunda kendini amorti ediyor** (`03` §5). Tam liste:

- **Amorti eden (≤15 gün):** Ek Hangar 7.0 · Kumarbaz Kasası 9.5 · Kaldıraçlı Kira 10.8 · Yüksek Volatilite 11.0 · Kelle Koltukta 13.7 · Prestij Ustası 15.2
- **Amorti etmeyen (>16 gün, yani ölü yatırım):** Çevik Ekip 20 · Prestij Simsarı 21.5 · Enerjik Ekip 26.8 · Ucuz Kira 27 · Sabırlı Müşteriler 50 · Telefon Hattı 55 · Mesai Saati 131 · Toplu Alım 218 · Acil Fren 255 · Geniş Ambar 341 · Görev Kademesi 1.028
- **Negatif (zararlı):** **Hızlı Hangar −5.3 TL/gün** · **Paketleme İstasyonu −12.3 TL/gün**

İki zararlı kartın mekanizması:
- `fast_hangar` hangar bekleme süresini ×1.3 **uzatıyor** (`PerkEffect.cs:223`). Tır daha uzun bekleyince doldurma şansı artar ama tır devir sayısı düşer; 2P'de (kargo 2-3) net etki negatif. İsim ("Hızlı Hangar") yaptığının tersini ima ediyor.
- `packing_station` 2. masayı açıyor, ama darboğaz masa değil **oyuncu emeği**. Fazladan paketleme fırsatı oyuncuyu 26 s'lik görevlere bağlıyor, servis/tır yükleme gecikiyor: prestij 54.9 → 49.4, gelir düşüyor.

**Zorunlu kart yok** — hiçbir kart "almamak mümkün değil" seviyesinde. Tersi doğru: **hiçbirini almamak en iyi strateji.** Bu, roguelite draft sisteminin amacının tam tersi.

Yan bulgu (dolaylı maliyet): kart fiyatları `upgradeCostMultiplierByPlayerCount = {1, 2, 2.95, 3.70}` ile ölçekleniyor ama kartların faydası oyuncu sayısıyla bu oranda büyümüyor. 4P'de Ek Hangar 740 TL, Kumarbaz Kasası 1.295 TL. `mantikli` stratejide 4P orta yalnız kartların %25-63'ünü alabiliyor (sahiplik oranları, `03` verisi) ve buna rağmen %49 kayıp veriyor.

## Soru 6 — Dominant strateji veya exploit var mı?

**Dominant strateji: hiçbir şey satın almamak.** 12/12 hücrede hem en yüksek kasa hem en düşük kayıp. Bu bir exploit değil, denge hatası — oyunun ana karar mekanizmasını (günlük 3 kart draftı) ekonomik olarak reddetmek optimal.

Test edilen ve **sömürülemez** çıkan yollar:
- **Telefon spam:** `phone_use = 1` her hücrede sonucu kötüleştiriyor (2P kayıp %23 → %31). 2026-09-18'deki `callMoneyReward: 20 → 0` düzeltmesi sömürüyü kapatmış. ✅
- **Reroll:** `mantikli` stratejide reroll harcaması ortalama 0 TL çıktı (rezerv kuralı tetiklenmesine izin vermiyor) — reroll fiyatı (50-525 × P çarpanı) pratikte kullanılmaz durumda.
- **Kota/talep artırma:** varış aralığı ×0.7 → kayıp %23 → %48. Daha fazla müşteri kötü, çünkü servis kapasitesi sabit.
- **İade modu istismarı:** iade müşterisi stoktan kutu alıyor (para yok, +0.4 prestij). Modelde ayrı bir avantaj üretmiyor; gün 5 sonrası kutu arzının %25'i paraya çevrilemiyor, yani iade modu net **gelir kaybı** (gün 5-8'de gelir düşüşü: 2P 306 → 299 → 284 → 272 TL, kota artmasına rağmen).
- **Yanlış ürün verip müşteriyi hızlı yollama:** −0.20 prestij vs +0.4 ödül farkı bu simde kârlı çıkmıyor (2:1 asimetri yeterli).

## Soru 7 — Co-op'ta boşta kalma / iş yükü dengesi

**Boşta geçen oyuncu-zaman oranı ("hiç" stratejisi, gün ortalaması):**

| Beceri | 1P | 2P | 3P | 4P |
|---|---|---|---|---|
| zayıf | 12.7% | 12.8% | 19.4% | **26.4%** |
| orta | 12.7% | 11.9% | 16.6% | **20.8%** |
| iyi | 12.9% | 12.5% | 16.7% | **20.2%** |

**Gün erken bitme oranı** (kota tükendi → gün sarıldı):

| Beceri | 1P | 2P | 3P | 4P |
|---|---|---|---|---|
| zayıf | 85.4% | 48.4% | 46.3% | 64.4% |
| orta | 89.4% | 74.5% | 88.1% | 93.8% |
| iyi | 91.5% | 89.0% | 96.0% | **97.5%** |

İki bulgu:
1. **3P/4P'de her 5 oyuncu-saatinin 1'i boşa gidiyor** (2P'nin ~1.7 katı). Kök neden `01-parametreler.md` §C: fiilen **tek servis istasyonu** (sahnede `serviceTables[1] = {fileID: 0}`) + `maxQueueSize = 2` + P3=P4 kota eğrisi. 4. oyuncunun yapabileceği iş yok.
2. **İyi oynayan takımlar günün %89-97'sini atlıyor.** Kota erken tükeniyor, kuyruk boşalıyor, gün sarılıyor. Yani "daha iyi oyna" ödülü **daha kısa gün**; tır penceresinin kalanı ve onunla gelebilecek gelir kayboluyor. Bu, tasarım denetimindeki "statik/olaysız his" şikâyetinin en güçlü mekanik karşılığı: oyun, iyi oynandığında kendini kapatıyor.

## Soru 8 — Faucet / sink dengesi

**Tek faucet: tır teslimi** (+ ihmal edilebilir festival/quest). Tek gerçek sink: **kira**.

| Hücre | Gelir (16 gün) | Kira | Upgrade | Ceza | Kasa16 | Kira/gelir | Upgrade/gelir |
|---|---|---|---|---|---|---|---|
| P1 orta hiç | 2.268 | 1.556 | 0 | 160 | 1.131 | **69%** | 0% |
| P2 orta hiç | 6.370 | 3.489 | 0 | 367 | 3.392 | 55% | 0% |
| P4 orta hiç | 12.533 | 8.742 | 0 | 466 | 4.575 | **70%** | 0% |
| P2 iyi hiç | 9.304 | 3.489 | 0 | 169 | 6.671 | **38%** | 0% |
| P4 iyi hiç | 16.136 | 8.750 | 0 | 203 | 8.558 | 54% | 0% |
| P2 orta mantıklı | 6.646 | 2.665 | 3.639 | 381 | 812 | 40% | **55%** |
| P4 iyi mantıklı | 20.058 | 7.605 | 9.974 | 236 | 3.617 | 38% | 50% |

- **Kira tek başına gelirin %38-70'ini yutuyor.** Upgrade almayan oyuncuda gelirin %30-62'si kasada birikiyor ve hiçbir işe yaramıyor (oyun gün 16'da bitiyor, prestij/skor karşılığı yok).
- Cezalar (yanlış teslim + kutu düşme) gelirin yalnız **%1.3-6'sı** — caydırıcılık olarak zayıf, ama duyarlılık analizinde yanlış teslim oranı ölümcül çıkıyor (dar kasada zamanlama etkisi, toplam büyüklük değil).
- **Sink eksikliği yapısal:** kira dışında para harcanacak tek yer upgrade, o da zarar. Yani ekonomi "biriken para → kullanılmayan para" ile bitiyor. Oyun sonunda kasada kalan para hiçbir şeye dönüşmüyor (skor/prestij/unlock yok).

---

## Analiz özeti — 6 yapısal sorun

| # | Sorun | En güçlü kanıt |
|---|---|---|
| 1 | **Upgrade sistemi ekonomik olarak reddediliyor**: 19 kartın 13'ü amorti etmiyor, 2'si zararlı, "hiç alma" 12/12 hücrede optimal | `03` §1, §5 |
| 2 | **Tek iflas noktası gün 8**; gün 4 formalite, gün 12/16 marjinal; prestij ölümü imkânsız | `03` §3 |
| 3 | **İyi oynamak günü kısaltıyor** (%89-97 erken bitiş) → geç oyunda yapacak iş ve olay kalmıyor | Soru 7 |
| 4 | **3P/4P'de %20-26 boşta oyuncu**: tek servis istasyonu + kuyruk 2 + P3=P4 kota | Soru 7 |
| 5 | **Event'ler ekonomik olarak görünmez** (±150 TL / 16 gün, kasanın %4'ü); BUSY/MARKETING gibi kota event'leri ölü kol | `03` §4 |
| 6 | **Kira gelirin %38-70'ini yutup kalanı atıl bırakıyor**; ikinci sink yok | Soru 8 |
