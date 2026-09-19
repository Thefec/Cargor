# 05 — Öneriler (her biri sim ile ölçüldü)

> ## ✅ UYGULAMA DURUMU (2026-09-19)
> **Ö1, Ö2, Ö3 canlıya alındı** (kullanıcı onayı). Ö4-Ö10 bekliyor.
> Doğrulama: Unity invariant **223/223**, EditMode **132/132** → `verification/2026-09-19-unity-dogrulama.md`.
> Uygulama sonrası ölçüm (canlı config): 1P orta/mantıklı kayıp %64.0 → **%14.3** · 2P %24.0 → **%5.7** ·
> 3P %27.3 → **%7.0** · 4P %49.3 → **%9.3**; iyi beceride kart alan takım 11.682 TL vs almayan 8.558 TL.
> Ham veri: `results/matrix_post_uygulama.csv`.
>
> **Ö2'de yakalanan tuzak:** `DifficultyManager.cs`'teki alanı değiştirmek tek başına ETKİSİZDİ —
> Unity prefab cache'inden eski diziyi okuyordu. Değer `Assets/DifficultyManager.prefab`'a açıkça
> yazılınca düzeldi. **Ö5'i (`prestigePerBonus`) uygularken aynı sınıf hataya dikkat:** o değer
> `EkonomiAyarlari.asset`'te serialize edilmiş durumda, yani asset'i güncellemek şart.
>
> **Ö3'ün yan işi:** kartın sahnedeki açıklaması hâlâ "%30 uzar" diyordu (qa bulgusu);
> "%25 kısalır — hangar daha hızlı döner." olarak düzeltildi. Perk etkisi değişirse metni de değiştir.

Her öneri için: sorun → kanıt → somut değişiklik (dosya:satır + sayı) → **değişiklik sonrası simülasyon sonucu**. Sıralama etki/zahmet oranına göre.

Kanıt üreticisi: `tools/economy_sim/proposals.py` (paket tanımları kodda, çıktı `tools/economy_sim/results/proposals.json`). Tekrar üretmek:
```bash
python tools/economy_sim/proposals.py --runs 250              # tüm paketler
python tools/economy_sim/proposals.py --only A3 A4 --runs 250
```

**Referans değerler (mevcut durum, 250-300 koşu, event açık):**

| Ölçüt | "hiç upgrade" | "mantıklı upgrade" |
|---|---|---|
| Ortalama kayıp (12 hücre) | %12.3 | **%49.2** |
| Ortalama gün-16 kasa | 3.494 TL | **1.568 TL** |
| Upgrade almak kazandırıyor mu? | — | **HAYIR** (−1.926 TL) |

---

## Önerilen paket (REC) — tek cümlede

**Kart ekonomisini düzelt (Ö1-Ö4), prestij eşiğini düşür (Ö5), sonra kaybolan riski taban kirayla geri koy (Ö6).** Ölçülen sonuç:

| Paket | Ortalama kayıp (mantıklı) | Ortalama kasa (mantıklı) | Upgrade kazandırıyor mu | orta/mantıklı kayıp | iyi/mantıklı kayıp |
|---|---|---|---|---|---|
| **BAZ (mevcut)** | %49.2 | 1.568 | **HAYIR** | %41.2 | %7.9 |
| Ö1 tek başına (A3) | %38.6 | 2.370 | HAYIR | %15.6 | %1.0 |
| Ö1+Ö2+Ö3 (A4) | %34.2 | 3.799 | **EVET** | %9.1 | %0.1 |
| **REC = Ö1-Ö5** | **%30.4** | **4.994** | **EVET** | %3.2 | %0.1 |
| **REC + Ö6 (kira +%15)** | %36.7 | 4.403 | **EVET** | **%12.3** | %0.3 |
| REC + Ö6 + Ö7 (REC7) | %35.6 | — | **EVET** | %10.3 | %0.3 |

**REC sonrası kart amortismanı (2P orta): amorti eden kart sayısı 6 → 11.**
`Ek Hangar 5.4 · Kumarbaz Kasası 6.7 · Kelle Koltukta 6.8 · Kaldıraçlı Kira 8.2 · Prestij Simsarı 8.4 · Yüksek Volatilite 8.8 · Prestij Ustası 8.9 · Çevik Ekip 9.2 · Paketleme İstasyonu 9.8 · Enerjik Ekip 12.9 · Ucuz Kira 14.1` (gün)
Hâlâ amorti etmeyen 8 kart: Telefon Hattı 21.4 · Sabırlı Müşteriler 29.8 · Hızlı Hangar 60.5 · Mesai Saati 160 · Toplu Alım 201 · Geniş Ambar 402 · Acil Fren 603 · **Görev Kademesi negatif** → bunlar fiyatla değil **etkiyle** düzelir (Ö8).

REC + Ö6'nın anlamı: zayıf oyuncu kart alırsa batar (%94), orta oyuncu gerçek risk taşır (%12), iyi oyuncu güvenle yatırım yapar (%0.3) — **ilk kez sağlıklı bir beceri gradyanı**. Mevcut durumda üç beceri seviyesinin de doğru cevabı "hiçbir şey almamak".

---

## Ö1 — KRİTİK: Ölü kartların fiyatlarını düşür

**Sorun.** 19 aktif karttan 13'ü 16 günlük oyunda kendini amorti etmiyor (`04` Soru 5). Fiyatlar, kartların ölçülen getirisiyle ilgisiz.

**Kanıt.** Amortisman (2P orta): Mesai Saati 131 gün, Acil Fren 255, Geniş Ambar 341, Görev Kademesi 1.028, Toplu Alım 218, Sabırlı Müşteriler 50, Telefon Hattı 55, Ucuz Kira 27, Prestij Simsarı 21.5, Enerjik Ekip 26.8, Çevik Ekip 20 (`03` §5).

**Değişiklik** — `Assets/Scenes/The Main Office.unity`, ilgili `baseCost`/`costStep` alanları (satırlar `01-parametreler.md` §G tablosundaki kart sırasına göre, blok 27162-27676):

| Kart | Sahne alanı | Mevcut | Önerilen | Yeni amortisman (2P) |
|---|---|---|---|---|
| Mesai Saati | baseCost | 300 | **120** | 140 gün (hâlâ zayıf → bkz. Ö8) |
| Acil Fren | baseCost | 250 | **90** | 268 gün (sigorta, bkz. Ö8) |
| Geniş Ambar | baseCost / costStep | 60 / 30 | **25 / 10** | 179 gün (bkz. Ö8) |
| Görev Kademesi | baseCost / costStep | 80 / 20 | **40 / 10** | negatif (bkz. Ö8) |
| Toplu Alım | baseCost | 80 | **30** | 89 gün |
| Sabırlı Müşteriler | baseCost | 120 | **60** | 40 gün |
| Telefon Hattı | baseCost | 160 | **60** | 30 gün |
| Prestij Simsarı | baseCost / costStep | 130 / 15 | **80 / 10** | **13.9 gün** ✅ |
| Ucuz Kira | baseCost / costStep | 130 / 30 | **70 / 15** | **17.0 gün** ✅ |
| Enerjik Ekip | baseCost | 100 | **55** | 18.8 gün |
| Çevik Ekip | baseCost | 180 | **110** | **13.4 gün** ✅ |

**Ölçülen sonuç (paket A3):** ortalama kayıp %49.2 → **%38.6**; 1P orta/mantıklı kayıp %62.4 → **%16.4**; amorti eden kart sayısı 6 → 7. Tek başına en yüksek etki/zahmet oranlı öneri (yalnız sahne YAML sayıları).

## Ö2 — KRİTİK: Upgrade oyuncu-sayısı maliyet çarpanını yumuşat

**Sorun.** `{1.00, 2.00, 2.95, 3.70}` çarpanı kart fiyatlarını P ile 3.7 katına çıkarıyor, ama kartların getirisi bu oranda büyümüyor (kota P3=P4, servis istasyonu 1, hangar süresi kısalsa da tek hangar).

**Kanıt.** 4P orta/mantıklı kayıp %50.0 (2P'de %25.6) — en kalabalık takım en yüksek riskte. 4P'de `mantikli` strateji kartların yalnız %25-63'ünü alabiliyor (`03` sahiplik oranları).

**Değişiklik.** `Assets/NewCss/GameState/DifficultyManager.cs:73` **ve** `Assets/DifficultyManager.prefab`:
```
upgradeCostMultiplierByPlayerCount = { 1.00f, 1.60f, 2.10f, 2.50f }   // eski {1.00, 2.00, 2.95, 3.70}
```
> **Uygulama notu (2026-09-19, ölçülerek öğrenildi):** Bu alan prefab YAML'ında serialize
> edilmemişti, ama Unity prefab'ın cache'inden **eski** diziyi döndürüyordu — yalnız `.cs`
> initializer'ını değiştirmek canlı değeri DEĞİŞTİRMEDİ (invariant denetimi yakaladı). Değer
> prefab'a açıkça eklendi:
> ```yaml
>   upgradeCostMultiplierByPlayerCount:
>   - 1
>   - 1.6
>   - 2.1
>   - 2.5
> ```
> `float[]` için **liste formatı** kullanılmalı; elle hex yazmak sessizce boş dizi üretir ve kod
> o durumda legacy ×1.15 bileşik davranışına düşer.
**Ölçülen sonuç (A2):** 4P orta/mantıklı %50.0 → **%41.6**, 3P %26.8 → %22.8, ortalama kasa 1.568 → 2.390. Ö1 ile birlikte 4P %9.6.

## Ö3 — KRİTİK: `fast_hangar` hangar süresini kısaltsın (şu an uzatıyor)

**Sorun.** Kart, hangar bekleme süresini **×1.30 uzatıyor** (`PerkEffect.cs:223`). Tır daha uzun bekleyince dolma şansı artıyor ama günlük tır devri düşüyor; 2P'de net **−5.3 TL/gün** (ölçülen tek negatif perk, `03` §5). Kartın adı ("Hızlı Hangar") ve sahne açıklaması yaptığının tersini ima ediyor.

**Değişiklik.** `Assets/NewCss/UpgradeScripts/PerkEffect.cs:223`:
```csharp
truck.hangarStayDuration = ctx.Economy.GetHangarStayDuration(pc) * 0.75f;  // eski 1.30f
```
**Ölçülen sonuç (A1):** kart −5.3 → **+3.2 TL/gün** (amortisman 75 gün — yani zarar duruyor ama kart hâlâ zayıf; Ö1'in fiyat indirimiyle birlikte A4'te 61 gün). Zararlı olmaktan çıkıyor.
**Alternatif (aynı sonucu veren, kod değişikliği olmayan yol):** kartı draft'tan çıkar (`disabledInDraft: 1`) ve etkisini Ek Hangar'a bırak.

## Ö4 — KRİTİK: Paketleme İstasyonu kartı 2. **servis istasyonunu** açsın

**Sorun.** Kart 2. paketleme masasını açıyor ama darboğaz masa değil **oyuncu emeği**: fazladan paketleme fırsatı oyuncuyu 26 s'lik görevlere bağlıyor, servis ve tır yükleme gecikiyor. Ölçülen etki **−12.3 TL/gün** ve prestij 54.9 → 49.4 (`03` §5). Aynı zamanda 3P/4P'de oyuncu-zamanının %17-26'sı boşa gidiyor, kök neden sahnede **2. servis istasyonunun bağlanmamış olması** (`serviceTables[1] = {fileID: 0}`, `The Main Office.unity:87883`) — kod tarafı (`CustomerManager.AssignFreeServiceStations`, `cs:1006-1049`) çoklu istasyonu zaten destekliyor.

**Değişiklik (iki parça).**
1. Sahne: `The Main Office.unity:87881-87883` → `serviceTables[1]`'e ikinci bir `DisplayTable` bağla (paketleme odasındaki ikinci masa objesi; `01-parametreler.md` §D'ye göre sahnede `Table` taşıyan 2 obje var).
2. Kartı bu slota bağla: `UpgradePanel` "Paketleme İstasyonu" omurgasının `levelObjects`'i 2. masa yerine 2. **servis** istasyonunu etkinleştirsin; fiyat `baseCost: 150 → 90`.

**Ölçülen sonuç.**
- Kart: **−12.3 → +10.7 TL/gün** (paket F1), prestij 54.9 → **61.5** (müşteri kaybı azalıyor), kayıp %0. REC paketi içinde (Ö2'nin maliyet indirimiyle) amortisman **9.8 gün** ✅.
- **İstasyon koşulsuz açık olsaydı** (paket C1, üst sınır ölçümü): boşta oyuncu 4P %20.9 → **%17.1**, 3P %16.7 → %13.6; **kaçan müşteri/gün 4P 1.62 → 0.92 (−%43)**, 2P 2.07 → 1.48; kasa16 +%6-7.
- **Kartla açıldığında** (F1, gerçek yol — faydayı yalnız kartı alan görür): kart alan takımda boşta oyuncu 4P %20.9 → **%19.1**, 3P %17.3 → %15.3; kaçan müşteri/gün 4P 1.87 → **1.48**, 2P 2.38 → 1.65. REC paketinde 4P %17.9.
  Not: "hiç kart almayan" hücrelerde bu öneri hiçbir şey değiştirmez (istasyon kart ile açılıyor) — C1 ile F1 arasındaki fark tam olarak bu.

## Ö5 — ÖNEMLİ: `prestigePerBonus` 8 → 6

**Sorun.** Prestijin tek ekonomik kapısı kutu-başı ödül (`+5 TL / 8 prestij`). 1P gün 16'da yalnız 25-37 prestije ulaşıyor (+15-20 TL/kutu), 4P iyi ise 96.3 ile tavana dayanıyor (`04` Soru 4) — aynı sistem 1P'ye neredeyse hiç ödül vermiyor, 4P'de ise son günlerde israf oluyor.

**Değişiklik.** `Assets/NewCss/GameEconomySettings.cs:83` ve `Assets/Resources/EkonomiAyarlari.asset:25`:
```
prestigePerBonus: 6      // eski 8  → tier eşikleri 6/12/18/… (eski 8/16/24/…)
```
**Ölçülen sonuç (D1):** ortalama kayıp %49.2 → **%44.5**, 2P/3P orta %14, ortalama kasa 1.568 → 2.134. REC paketinin parçası.
**Not:** `maxPrestige` 100 → 140 önerisi **ölçüldü ve reddedildi** (paket D2): sıfır etki, çünkü tavana yalnız "iyi + hiç upgrade" hücresi dayanıyor.

## Ö6 — ÖNEMLİ: Kart ekonomisi düzeldikten sonra taban kirayı +%15 çek

**Sorun.** Ö1-Ö5 uygulanınca risk neredeyse kayboluyor: orta/mantıklı kayıp %41.2 → **%3.2**, iyi %0.1. Bu, economist A1 denetimindeki "ekonomi neredeyse kaybedilemez" bulgusunu derinleştirir.

**Değişiklik.** `GameEconomySettings.cs:21` + `EkonomiAyarlari.asset:15` (**dikkat:** asset'te `int[]` hex olarak yazılı — `220100008a020000740400005e060000`; elle hex yazmak yerine Inspector'dan girilmeli, bkz. hafıza notu "Unity YAML float[] tuzağı"):
```
baseRentByPlayerCount = { 335, 750, 1310, 1875 }    // eski {290, 650, 1140, 1630}, +%15
```
**Ölçülen sonuç:** orta/mantıklı kayıp %3.2 → **%12.3**, iyi %0.3, upgrade almak hâlâ kazandırıyor (4.403 vs 3.693). Alternatifler ölçüldü: +%30 çok sert (orta %27.1), `rentGrowthMultiplier` 1.20 → 1.26 yetersiz (%4.5).
**Sıra önemli:** Ö6'yı Ö1-Ö4 olmadan uygulamak mevcut %49 kaybı daha da yukarı çeker.

## Ö7 — ÖNEMLİ: 4P kotasını 3P'den ayır

**Sorun.** `dailyCustomerCountP4` ile `P3` **birebir aynı** (`GameEconomySettings.cs:50,53`); 4. oyuncunun işleyeceği ek müşteri yok. Sonuç: 4P'de boşta oyuncu %20.8 (2P'de %11.9) ve 4P kirası 2P'nin 2.5 katı.

**Değişiklik.** `GameEconomySettings.cs:53`:
```
dailyCustomerCountP4 = { 10,10,10,10, 11,11,11,12, 12,12,13,13, 14,14,14,16 }   // ≈ P3 +%20
```
**Ölçülen sonuç (C3, Ö4 ile birlikte):** 4P kasa16 4.576 → **6.008 (+%31)**, boşta %20.9 → **%13.8**, kayıp %50.0 → %29.2. REC6 üstüne eklenince (paket REC7) 4P orta/mantıklı kayıp %18.8 → **%7.6**.

**⭐ Yan etki — Ö9'u da çözüyor:** 4P'de gün erken bitme oranı **%93.7 → %22.2** (kart almayan), mantıklı stratejide %94.6 → %55.2. Yani "gün erken bitiyor" sorununun (Ö9) gerçek kaldıracı kota; kotayı kapasiteye göre artırmak günü fiilen dolduruyor. Bedeli küçük: kaçan müşteri/gün 1.62 → 1.83. Bu, Ö9'un 1. seçeneğinin (kotayı gün süresine bağla) diğer P değerleri için de çalışacağının ilk kanıtı — uygulanırsa her P için ayrı ölçülmeli.
**Uyarı (ölçüldü):** varış aralığını 3P için de kısaltmak (21 → 19 s) **kötüleştirdi** (3P kasa 4.456 → 4.039) — kota artmadan aralık kısaltmak yalnız müşteri kaybı üretiyor. Aralık değişikliği yalnız kota artan P için yapılmalı.

## Ö8 — ÖNEMLİ: Etkisi ölçülemeyen 5 kartı yeniden tanımla veya draft'tan çıkar

Ö1'in fiyat indirimi bu kartları kurtarmıyor; sorun **etkinin kendisi** (A4 sonrası amortisman, 2P):

| Kart | A4 amortisman | Kök neden | Öneri |
|---|---|---|---|
| **Görev Kademesi** | **negatif (−1.9 TL/gün)** | Tier açmak Medium/Hard görev getiriyor; tamamlanma olasılığı düşük, ceza (−20/−30 TL, −0.4/−0.6 prestij) ödülü yiyor | Hedefleri düşür (Hard raf 12 → 7, paket 12 → 7) **veya** `disabledInDraft: 1` |
| **Mesai Saati** | 119 gün | Gün süresini +25 s uzatıyor ama günlerin **%75-97'si erken bitiyor** (`04` Soru 7) — uzatılan süre hiç kullanılmıyor | Etkiyi "günlük kota +1" veya "kota tükendikten sonra tır penceresi açık kalsın"a çevir |
| **Geniş Ambar** | 143 gün | Raf kapasitesi hiçbir yerde bağlayıcı değil; kodda taşma cezası yok | Ya gerçek bir stok tavanı getir ya kartı çıkar |
| **Acil Fren** | 214 gün | Sigorta; REC sonrası iflas oranı düştüğü için beklenen değeri daha da azalıyor | 90 TL'de bırak, "sigorta" olarak kabul et (kayıp oranını düşürmüyor ama kuyruk riskini kesiyor) |
| **Toplu Alım** | 71 gün | Diğer kartlar ucuzlayınca %50 indirimin mutlak değeri küçülüyor | 30 TL'de bırak veya indirimi %50 → %100 (bir kart bedava) yap |

## Ö9 — ÖNEMLİ (tasarım kararı): Gün erken bitiyor

**Sorun.** Günlerin **%75-97'si** kota tükenip kuyruk boşalınca sarılıyor (iyi takımlarda %89-97.5). İyi oynamanın ödülü "daha kısa gün": kalan tır penceresi ve onunla gelebilecek gelir kayboluyor. Bu, tasarım denetimindeki "statik/olaysız" şikâyetinin en güçlü mekanik karşılığı.

**Ölçülen:** küçük ayarlar çözmüyor — 2. istasyon (%89 → %89), kuyruk 3 (%75 → %76). **Tek işe yarayan kaldıraç kota:** Ö7'nin 4P kota artışı (+%20, ~×1.23) erken bitişi **%93.7 → %22.2** düşürdü (bkz. Ö7). Kök neden kotanın gün süresinden bağımsız olması: gün 200 → 330 s uzarken kota yalnız 8 → 13 artıyor.

**Seçenekler (hangisi seçilirse sim ile yeniden ölçülmeli):**
1. **Kotayı gün süresine bağla** (gün uzarken kota da uzasın) + servis kapasitesini Ö4 ile artır. → Ö7'de 4P için ölçüldü: erken bitiş %93.7 → %22.2, bedeli +0.2 kaçan müşteri/gün. **En umut verici yol.**
2. Erken bitişi kaldır, günün kalanını "serbest üretim zamanı" yap (stok biriktirip sonraki günü hızlandırma).
3. Günü kısalt (200 s → 150 s) ve kotayı koru — gün gerçekten dolsun.

Not: Mesai Saati kartının (gün +25 s) değersiz olmasının nedeni de bu (Ö8) — uzatılan süre zaten kullanılmıyor. Ö9 çözülürse o kart da kendiliğinden anlam kazanır.

## Ö10 — İNCE AYAR: İkinci para yutağı yok

Gelirin %30-62'si kasada birikip oyun sonunda hiçbir şeye dönüşmüyor (`04` Soru 8; 4P iyi'de 8.558 TL atıl). Kira dışında sink yok. Öneri: gün 16 sonunda kalan paranın bir karşılığı olsun (skor/derece/sonraki run'a taşınan bonus) **veya** gün içinde tüketilebilir bir harcama kalemi (ör. günlük "ekstra tır çağırma" ücreti) eklensin. Ekonomik büyüklük: 16 günde 1.100-8.500 TL'lik bir yutak kapasitesi var.

---

## Ölçüldü ve REDDEDİLDİ (bir sonraki tur aynı öneriyi getirmesin)

| Öneri | Ölçülen sonuç | Karar |
|---|---|---|
| **Kira eğrisini düzleştir** (g 1.20 → 1.12, taban {330,730,1280,1830}, 16-gün toplamı sabit) | Ortalama kayıp %49.2 → **%53.2 (KÖTÜLEŞTİ)** — gün 4'ü zorlaştırmak erken oyunu bozuyor, çünkü kasa zaten gün 4'te dibe iniyor | RED |
| **Grace 2 kullanım** | Kayıp %49.2 → %33.8, ama "hiç upgrade" hücrelerinde %12.3 → **%2.0** — oyunu fiilen kaybedilemez yapıyor | RED (gün 8 duvarı Ö1-Ö4 ile zaten çözülüyor) |
| Grace yüzdesini artır (%80 → %90/%100) | Kayıp farkı %6.5 → %6.0 → %5.8 (gürültü) — grace anında kasa neredeyse boş olduğu için yüzde önemsiz | RED |
| **maxPrestige 100 → 140** | Sıfır etki (hiçbir hücrede tavan bağlayıcı değil) | RED |
| **Event ekonomik etkilerini ×2 güçlendir** | Ortalama kayıp %49.2 → %49.8, kasa 1.568 → 1.553 (etkisiz) — event günleri az (16 günde ~7) ve etkileri gelirin küçük kısmı | RED (event'ler ekonomik değil, deneyimsel katman olarak kalmalı) |
| **Kuyruk 2 → 3** | Kayıp %49.0 → %49.8, kasa 1.727 → 1.708 | RED (kuyruk büyütmek servis kapasitesi artmadan işe yaramıyor) |
| **Varış aralığını kısalt** (tüm P için ×0.7) | 2P kayıp %23 → **%48** | RED (yalnız kotası artan P için, Ö7 ile birlikte) |

---

## Oyuncu sayısına göre ölçekleme — önerilen formül

Mevcut ölçekleme tutarsız: kira 1 : 2.24 : 3.93 : 5.62, upgrade 1 : 2.00 : 2.95 : 3.70, kota 1 : 1.75 : 2.00 : 2.00, ödül 1 : 1.10 : 1.40 : 1.76. Yani **gider P ile 5.6 kata, gelir kapasitesi 2 kata çıkıyor**.

Önerilen hizalama (Ö2 + Ö6 + Ö7 sonrası):

| Eksen | 1P | 2P | 3P | 4P | Oran |
|---|---|---|---|---|---|
| Kota (gün 16) | 6 | 12 | 13 | **16** | 1 : 2.00 : 2.17 : 2.67 |
| Kutu ödülü | 50 | 55 | 70 | 88 | 1 : 1.10 : 1.40 : 1.76 |
| → Gelir kapasitesi | 1 | 2.20 | 3.03 | **4.69** | |
| Kira (öneri) | 335 | 750 | 1.310 | 1.875 | 1 : 2.24 : 3.91 : **5.60** |
| Upgrade maliyeti (öneri) | 1.00 | 1.60 | 2.10 | 2.50 | |

Kira/gelir oranı bu haliyle 1P'de en ağır, 4P'de en hafif kalıyor. Tam hizalama isteniyorsa kira **1 : 2.2 : 3.0 : 4.3** → `{335, 737, 1005, 1440}` olurdu; bu tasarım kararı (GDD §19.3 "kalabalık takım koordinasyon avantajını kirayla geri öder" ilkesi bilinçli olarak kirayı dik tutuyor), bu yüzden öneri olarak **değil**, seçenek olarak not edildi — seçilirse sim ile yeniden ölçülmeli.

---

## Uygulama sırası (önerilen)

1. **Ö3** (tek satır, zararlı kartı durdurur) → **Ö1** (sahne sayıları) → **Ö2** (tek dizi) — üçü birlikte ölçülen A4: kayıp %34.2, upgrade ilk kez kazançlı.
2. **Ö5** (tek sayı) + **Ö4** (sahne slotu + kart bağlama, en çok iş) → REC: kayıp %30.4, orta beceride %3.2.
3. **Ö6** (risk geri gelsin) → orta beceride %12.3.
4. **Ö7** (4P dengesi), **Ö8** (5 kart), **Ö9/Ö10** (tasarım kararları) — ayrı turlar.

Her adımdan sonra doğrulama: `python tools/economy_sim/sim.py --runs 300 --figs` + `Assets/Editor/EconomyInvariantCheck.cs` (menü: *Cargor / Ekonomi Değerlerini Doğrula*) — invariant testi bu önerilerin **beklenen değerlerini** içerdiği için değişiklikle birlikte güncellenmeli (`EconomyInvariantCheck.cs:288-382`).
