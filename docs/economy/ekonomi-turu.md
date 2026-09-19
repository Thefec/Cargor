# Cargor — Ekonomi Denetimi (Economy Pass)

**Tarih:** 2026-09-18 · **Dal:** `fix/difficulty-scaling-and-dead-code` · **Kapsam:** mevcut ekonominin tam denetimi — parametre çıkarımı, matematiksel model, bağımsız simülasyon, analiz, sim ile doğrulanmış öneriler.
**Kural:** bu denetim sırasında oyun koduna / prefab'a / sahneye / ScriptableObject'e **hiçbir değişiklik yapılmadı**. Değişiklikler ayrı bir adımda, onay sonrası uygulanacak.

> **Güncelleme 2026-09-19:** Denetimin ardından kullanıcı onayıyla **Ö1+Ö2+Ö3 uygulandı**
> (kart fiyatları, upgrade P-maliyet çarpanı, `fast_hangar` yönü). Doğrulama: Unity invariant
> 223/223, EditMode 132/132 → [verification/2026-09-19-unity-dogrulama.md](verification/2026-09-19-unity-dogrulama.md).
> Ölçülen etki: orta beceride kayıp 1P %64→%14.3, 2P %24→%5.7, 3P %27.3→%7.0, 4P %49.3→%9.3.
> **Aşağıdaki tüm analiz sayıları uygulama ÖNCESİ durumu anlatır** (baseline config donmuş kanıt
> olarak `tools/economy_sim/config_baseline_pre.json`). Kalan öneriler Ö4-Ö10 bekliyor; durum
> tablosu: [PLAN.md](PLAN.md).

**Bölüm dosyaları:** [01 Parametreler](01-parametreler.md) · [02 Model](02-model.md) · [03 Simülasyon](03-simulasyon-sonuclari.md) · [04 Analiz](04-analiz.md) · [05 Öneriler](05-oneriler.md) · [Çalışma planı/durum](PLAN.md)
**Araç:** `tools/economy_sim/sim.py` + `config.json` (+ `proposals.py`), ham çıktı `tools/economy_sim/results/`
**Grafikler:** `fig-money-by-day.png` · `fig-prestige-by-day.png` · `fig-playercount-compare.png` · `fig-bankruptcy-heatmap.png` · `fig-upgrade-payback.png` · `fig-proposals-compare.png`

---

## Yönetici özeti — en kritik 5 bulgu

1. **Upgrade sistemi ekonomik olarak reddediliyor.** 19 aktif karttan yalnız 6'sı 16 günlük oyunda kendini amorti ediyor; 2'si (Hızlı Hangar −5.3 TL/gün, Paketleme İstasyonu −12.3 TL/gün) aktif olarak zararlı. Sonuç: **"hiçbir kart almamak" 12 hücrenin 12'sinde hem en güvenli hem en zengin strateji** (ortalama kayıp %12.3 / kasa 3.494 TL; kart alan: %49.2 / 1.568 TL). Oyunun ana karar mekanizması (günlük 3 kart draftı) matematiksel olarak "pas geç" diyor.
2. **Oyunun tek gerçek iflas noktası gün 8.** 5.016 ölümün %71.9'u gün 8, %22.6'sı gün 12, %5.5'i gün 16; **gün 4 hiç öldürmüyor** ve **prestij ölümü 21.600 koşuda hiç gerçekleşmedi** (prestij bir kaybetme yolu olarak ölü).
3. **İyi oynamak günü kısaltıyor.** Günlerin %75-97'si (iyi takımlarda %89-97.5) kota tükenip kuyruk boşalınca erken sarılıyor; kalan tır penceresi ve geliri kayboluyor — "statik/olaysız his" şikâyetinin en güçlü mekanik karşılığı. Küçük ayarlar (2. istasyon, kuyruk) etkisiz; **tek işe yarayan kaldıraç kota**: 4P kotasını %20 artırmak erken bitişi %93.7 → %22.2 düşürüyor (Ö7/Ö9).
4. **3P/4P'de oyuncu-zamanının %17-26'sı boşa gidiyor.** Kök neden: sahnede 2. servis istasyonu bağlı değil (`serviceTables[1] = {fileID: 0}`, kod çoklu istasyonu destekliyor), kuyruk 2, ve `dailyCustomerCountP4` ile `P3` birebir aynı. Aynı anda 4P kirası 2P'nin 2.5 katı → 4P en riskli oyuncu sayısı (%50 kayıp, 2P %25.6).
5. **Event katmanı ekonomik olarak görünmez.** 16 eventin izole etkisi −31.5 … +36.9 TL/gün; 16 günde toplam ±150 TL = gün-16 kasasının **%4'ü**. Event'li/event'siz fark ortalama %4.4 ve yönü bile tutarsız. BUSY DAY (+%35 kota) neredeyse etkisiz (+6 TL/gün) çünkü varış aralığı değişmiyor.

**Önerilen paket (REC = Ö1-Ö5) ile ölçülen sonuç:** ortalama kayıp %49.2 → **%30.4**, ortalama kasa 1.568 → **4.994 TL**, amorti eden kart sayısı **6 → 11**, ve **ilk kez kart almak kazançlı** hale geliyor. Ardından taban kira +%15 (Ö6) ile risk geri konuyor: orta beceride %12.3, iyi beceride %0.3 — sağlıklı beceri gradyanı. Ayrıca 4P kotasını ayırmak (Ö7) hem 4P riskini (%18.8 → %7.6) hem **gün erken bitişini (%93.7 → %22.2)** düzeltiyor. Detay: [05-oneriler.md](05-oneriler.md).

---

## 1. Ne yapıldı

| Aşama | Çıktı | Özet |
|---|---|---|
| Keşif | `01-parametreler.md` | ~120 ekonomi parametresi dosya:satır kanıtıyla; asset hex dizileri çözüldü; 2 prefab override (`interactionTime` 2, `exitDelay` 2 — kod 5), 2 ölü kablo (`ScaledCustomerCount`, `GetCustomerCapacity`), 2 bayat `sim.js` değeri bulundu |
| Model | `02-model.md` | Zaman/para/prestij/upgrade/event formülleri + 13 satırlık **insan varsayımı** tablosu (zayıf/orta/iyi profiller, parametrik) |
| Simülasyon | `03-simulasyon-sonuclari.md` | Saniye çözünürlüklü, stokastik, olay tabanlı 16 günlük motor; 72 hücre × 300 koşu (43 s); determinizm doğrulandı; `sim.js` ile çapraz kontrol |
| Analiz | `04-analiz.md` | 8 sorunun tamamı sayıyla cevaplandı; 6 yapısal sorun çıkarıldı |
| Öneriler | `05-oneriler.md` | 10 öneri (her biri sim ile ölçülmüş) + **7 ölçülüp reddedilen** öneri |

**Model doğrulaması:** ilk koşuda anormal bir hücre çıktı, `--trace-day` ile saniye saniye izlendi ve **simülasyonun kendi bug'ı** bulundu (alışverişe giden oyuncunun görevi eziliyordu → kuyruk kilitleniyordu). Düzeltildi; rapordaki tüm sayılar düzeltme sonrası. `sim.js` (v5.1) ile karşılaştırma: gün-16 geliri **%2.7**, kira **%0** fark; prestij −%31 ve gün-1 geliri −%63 fark, ikisi de açıklanabilir (bu sim müşteri sabrını/kaçışını ve üretim zincirinin ilk dolma gecikmesini modelliyor, `sim.js` modellemiyor).

## 2. Ekonominin bugünkü hali (özet sayılar)

**Para akışı:** tek faucet tır teslimi (`rewardPerBox(P) + 5·floor(prestij/8)`), tek gerçek sink kira. Müşteriler para vermez — **ürün** verir (kutunun hammaddesi), yani kota gelirin üst sınırı.

| Ölçüt | 1P | 2P | 3P | 4P |
|---|---|---|---|---|
| Başlangıç parası | 500 | 600 | 720 | 864 |
| Kira (dönem 0→3) | 290→501 | 650→1.123 | 1.140→1.970 | 1.630→2.817 |
| 16 gün kira toplamı | 1.557 | 3.489 | 6.120 | 8.750 |
| Kota (gün 1→16) | 4→6 | 7→12 | 8→13 | 8→13 |
| Kutu ödülü (taban) | 50 | 55 | 70 | 88 |
| Hangar bekleme | 120 s | 60 s | 40 s | 30 s |
| Upgrade maliyet çarpanı | ×1.00 | ×2.00 | ×2.95 | ×3.70 |
| **Kira / gelir oranı (orta beceri)** | **%69** | %55 | %61 | **%70** |
| Gün-16 prestij (orta / iyi) | 25 / 38 | 54 / 77 | 71 / 92 | 78 / **96** |
| Boşta oyuncu oranı | %12.7 | %11.9 | %16.6 | **%20.8** |
| Gün erken bitme oranı (orta) | %89 | %75 | %88 | %94 |

Gün-gün seyir: `fig-money-by-day.png` — kasa 4 günlük testere dişi çiziyor, kira günleri kasayı %65-85 süpürüyor (2P alt %10 dilimi gün 4'te 33 TL, gün 8'de 44 TL).

## 3. Sekiz sorunun kısa cevabı

1. **Gün gün para/prestij/kota** → `04` Soru 1 + `03` §2 tabloları. Kota hiçbir hücrede tam karşılanmıyor (en iyi %84).
2. **Batma riski** → Yalnız kira kapılarında; gün 8 %72, gün 4 sıfır. Kira günleri kritik, aradaki günler tamamen risksiz (kasa 0'a inse bile ceza yutulur).
3. **Zorluk eğrisi** → Tek tepe gün 8; sonrası giderek kolaylaşıyor (gün süresi + kota + prestij bonusu birlikte büyürken kira 1.7 kat artıyor). "Hiç kart alma" yolunda oyun gün 5'ten sonra çözülüyor ve para atıl birikiyor.
4. **Prestij anlamlı karar mı?** → Hayır. Ölüm imkânsız, otomatik artıyor, tek etkisi kutu ödülü; kapasite formülü ölü; 1P'de 25-37'de kalırken 4P iyi'de tavana (96) dayanıyor — **2.6 kat asimetrik**.
5. **Ölü/zorunlu upgrade** → 13 ölü, 2 zararlı, zorunlu yok; tersine "hiç almamak" optimal (`03` §5 tam amortisman tablosu).
6. **Dominant strateji/exploit** → Dominant: hiçbir şey almamak. Telefon spam artık zararlı (2026-09-18 düzeltmesi tuttu), reroll pratikte kullanılamaz, kota artırma ters tepiyor.
7. **Co-op iş yükü** → 3P/4P'de %17-26 boşta; iyi takımlar günün %89-97'sini atlıyor.
8. **Faucet/sink** → Kira gelirin %38-70'ini yutuyor, kalanı atıl (4P iyi'de 8.558 TL kullanılmayan para). İkinci sink yok.

## 4. Öneri özeti

| # | Öneri | Dosya | Ölçülen etki | Etiket |
|---|---|---|---|---|
| Ö1 | 11 ölü kartın fiyatını indir | `The Main Office.unity` (baseCost/costStep) | ort. kayıp %49.2 → **%38.6**; 1P %62 → %16 | **KRİTİK** |
| Ö2 | Upgrade P-çarpanı → {1, 1.6, 2.1, 2.5} | `DifficultyManager.cs:73` + prefab | 4P %50 → %41.6 | **KRİTİK** |
| Ö3 | `fast_hangar` ×1.30 → ×0.75 | `PerkEffect.cs:223` | kart −5.3 → +3.2 TL/gün | **KRİTİK** |
| Ö4 | Paketleme İstasyonu → 2. **servis** istasyonu, fiyat 150→90 | sahne `serviceTables[1]` + `UpgradePanel` | kart −12.3 → **+10.7** TL/gün; 4P boşta %20.9→%17.1; kaçan müşteri −%43 | **KRİTİK** |
| Ö5 | `prestigePerBonus` 8 → 6 | `GameEconomySettings.cs:83` + asset | ort. kayıp → %44.5; 1P geliri ↑ | ÖNEMLİ |
| Ö6 | Taban kira +%15 → {335, 750, 1310, 1875} (**Ö1-Ö5'ten sonra**) | `GameEconomySettings.cs:21` + asset | orta beceride risk %3.2 → **%12.3** | ÖNEMLİ |
| Ö7 | `dailyCustomerCountP4`'ü P3'ten ayır (+%20) | `GameEconomySettings.cs:53` | 4P kasa **+%31**, kayıp %18.8→%7.6, **erken bitiş %93.7→%22.2** | ÖNEMLİ |
| Ö8 | Etkisi ölçülemeyen 8 kartı yeniden tanımla (Görev Kademesi negatif, Mesai Saati yapısal olarak değersiz) | sahne / tasarım | REC sonrası bile amorti etmiyorlar | ÖNEMLİ |
| Ö9 | Gün erken bitişi — Ö7'nin kota kaldıracıyla çözülüyor, her P için ölçülmeli | `GameEconomySettings.cs:44-53` | 4P'de kanıtlandı | ÖNEMLİ |
| Ö10 | İkinci para yutağı ekle | — | 1.100-8.500 TL atıl kapasite | İNCE AYAR |

**Ölçülüp reddedilenler** (bir sonraki tur tekrar getirmesin): kira eğrisini düzleştirme (**kötüleştirdi**: %49.2 → %53.2) · grace 2 kullanım (oyunu kaybedilemez yapıyor) · grace yüzdesi artırma (etkisiz) · `maxPrestige` 140 (sıfır etki) · event etkilerini ×2 (sıfır etki) · kuyruk 2→3 (etkisiz) · varış aralığını tüm P için kısaltma (2P kayıp %23 → %48). Gerekçeler `05` sonunda.

Karşılaştırma grafiği: `fig-proposals-compare.png`.

## 5. Varsayımlar (kodda olmayan, sonucu etkileyen)

Tam tablo `02-model.md` §6. Kritik olanlar ve duyarlılıkları (`03` §6):

| Varsayım | Değer (zayıf/orta/iyi) | Duyarlılık |
|---|---|---|
| `t_pack` — ürün → paketlenmiş kutu emek süresi | 40 / 26 / 18 s | **En duyarlı girdi**: ±%30 → 2P kayıp %11 ↔ %53.5. Playtestte ölçülmesi gereken 1 numaralı sayı |
| `p_wrongDelivery` — yanlış renk tıra teslim | 0.15 / 0.08 / 0.03 | 2. en duyarlı: 0 → %9.5, 0.20 → %58.5 |
| `t_serve`, `t_load`, `t_phone` | 10/7/5, 14/10/7, 6/5/4 s | orta |
| `S_pack` — masanın kutu başına meşguliyeti | 8 / 6 / 5 s | düşük (mevcut kurulumda masa darboğaz değil) |
| `phone_use` — telefon kullanma eğilimi | 0.05 / 0.30 / 0.60 | düşük; spam artık zararlı |
| Tır giriş/çıkış animasyon süresi | 3 + 3 s (**BULUNAMADI**, kodda sayısallaşmamış) | düşük |
| Raf kapasitesi | 8 slot (+4/seviye) | modelde bağlayıcı değil → Geniş Ambar kartı değersiz çıkıyor |

## 6. Açık sorular / bu turun kapsamı dışında kalanlar

1. **Gün erken bitişi** bir tasarım kararı bekliyor (Ö9'un 3 seçeneği); seçilen yol sim ile yeniden ölçülmeli.
2. **Ö4'ün sahne tarafı** (2. `DisplayTable`'ın `serviceTables[1]`'e bağlanması) bu denetimde yapılmadı — kod desteği var, sahne slotu boş.
3. **Quest sistemi** ekonomik olarak negatif (Görev Kademesi −1.9 TL/gün); hedef/ceza dengesi ayrı bir tur gerektirir.
4. **İade modu** (gün 5+, %25 müşteri) net gelir kaybı: kutu arzının dörtte biri paraya çevrilemiyor (2P'de gün 5-8 arası gelir kota artmasına rağmen 306 → 272 TL'ye iniyor). Tasarım amacı (çeşitlilik) ile ekonomik maliyeti ayrıca değerlendirilmeli.
5. **Gün 9 dual-item sıçraması**: kaçan müşteri 1.8 → 3.2 (2P). Tek istasyonla birleşince sert; Ö4 bunu kısmen çözer, playtest teyidi gerekir.
6. `sim.js` (v5.1) iki bayat değer taşıyor (`exitDelay` 5 → canlı 2; quest Medium/Hard cezaları) — o araç bir sonraki kullanımda tazelenmeli.
7. Bu sim oyuncu iş seçimini sabit öncelikle modelliyor (servis → yükleme → paketleme → telefon); gerçek takımlar rol paylaşımı yapabilir, bu boşta-kalma sayılarını bir miktar abartabilir.
