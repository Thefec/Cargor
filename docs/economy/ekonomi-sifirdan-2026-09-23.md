# Ekonomi — sıfırdan bağımsız hesap (2026-09-23)

Tetikleyici: 3 oyunculu Steam playtest. Gün 1 sonunda kasa 500, 3 kart ≈200 → kalan 300. Kullanıcı: "upgrade'ler çok ucuz sanırım."
Yöntem: her sabit canlı kaynaktan (sahne/prefab/asset YAML + kod) yeniden okundu, sim config'iyle tek tek karşılaştırıldı, sonra yeni koşular yapıldı (300 koşu/hücre, event'ler açık).
Çıktılar: `tools/economy_sim/results_2026-09-23/` · Betik: `tools/economy_sim/sifirdan_2026_09_23.py` (sim.py'yi import eder; `results/` klasörüne dokunmaz).

## 0. Canlı değer doğrulaması (sim ↔ oyun)

| Değer | Canlı kaynak | Değer | Sim |
|---|---|---|---|
| Başlangıç parası | `DifficultyManager.prefab` 500 × 1.2^(P−1) → `MoneySystem.startingMoney` (cs:510) | 500/600/720/864 | = |
| Kira | `EkonomiAyarlari.asset` taban {290,650,1140,1630}, ×1.20/dönem, gün 4/8/12/16, grace %80 ×1 | 3P: 1140/1368/1642/1970 | = |
| Kutu ödülü | asset {50,55,70,88} + ⌊prestij/8⌋×5 (başlangıç prestiji 12 → +5) | 3P: 75 TL/kutu | = |
| Kota | cs initializer (asset'te alan yok) P3=P4 {8…13} | | = |
| Sabır | prefab 15/20 s, oyuncu başına −2 s | | = |
| Kart fiyat P-çarpanı | prefab {1.0,1.6,2.1,2.5}; Görev Kademesi muaf; OPPORTUNITY ×0.8 | | = |
| Kart fiyatları | sahne `The Main Office.unity` 27164-27678 | aşağıdaki tablo | = |
| Reroll | `RerollCurve` {50,90,160,290,525} × P-çarpanı | | = |
| Görev ödülleri | 30 asset: 28/60/150 TL, ceza 15/20/30; 4 tipin hepsi D2 ölçeğinden muaf | | = |
| Event tablosu | `EventEffectManager.cs` 134-266 | | = |

**Sonuç: sim bayat değil.** Canlı config ile yeni baseline matrisim `results/matrix_post_uygulama.csv` ile bit düzeyinde aynı çıktı.
**Tek model sapması (YENİ BUG):** `all_in` / `leveraged_rent` grace hakkını SİLMİYOR, `gracePaymentPercent=0` yazıyor (`PerkEffect.cs:332,350`). `DayCycleManager.TryProcessMoneyCheck` (cs:619-627) grace dalına yine giriyor, %0 alıyor ve kirayı ödenmiş sayıyor. Yani perkin "bedeli" aslında **bir kira bedava**. Sim'e `graceZeroPctLive` bayrağı eklendi (varsayılan kapalı). Toplu etkisi ölçülemeyecek kadar küçük, çünkü grace çoğunlukla gün 4/8'de, T3 kilidi açılmadan önce harcanıyor. Yine de mantık hatası; gameplay'e gitmeli.

## 1. Gelir modeli (kart almayan takım, ortalama)

Günlük gelir, TL (gün 1 / 2-4 ort. / 8 / 12 / 16) · Gün-16 kasa · Kayıp:

| P | Profil | g1 | g2-4 | g8 | g12 | g16 | Kasa g16 | Kayıp |
|---|---|---|---|---|---|---|---|---|
| 1 | zayıf / orta / iyi | 20 / 34 / 53 | 57 / 103 / 140 | 64 / 108 / 140 | 98 / 171 / 272 | 128 / 223 / 368 | 218 / 1131 / 2474 | %20 / 0 / 0 |
| 2 | zayıf / orta / iyi | 33 / 95 / 150 | 144 / 244 / 350 | 164 / 276 / 381 | 242 / 532 / 782 | 358 / 725 / 1085 | 465 / 3392 / 6671 | %21 / 0 / 0 |
| 3 | zayıf / orta / iyi | 86 / 150 / 210 | 253 / 421 / 547 | 257 / 451 / 549 | 403 / 814 / 1090 | 584 / 1134 / 1443 | 901 / 4475 / 7747 | %45 / 0 / 0 |
| 4 | zayıf / orta / iyi | 83 / 243 / 230 | 355 / 551 / 686 | 332 / 508 / 640 | 609 / 986 / 1304 | 862 / 1411 / 1809 | 1420 / 4575 / 8558 | %61 / 0 / 0 |

- Gün 1 zayıf, çünkü stok sıfırdan başlıyor (müşteri → ürün → paket → tır zinciri). Gün 1 geliri gün 2'nin yaklaşık 1/3'ü.
- **3P gün-1 kasası modelde 796 / 862 / 926 (zayıf/orta/iyi).** Playtest'teki 500, en zayıf profilin bile ~300 TL altında. İki olası açıklama var: (a) ilk oyunda çok sayıda yanlış renk teslimi (−40/kutu) ve reroll (3P'de 105); (b) oyuncu sayısı 1 algılandı, yani start 500 oldu (`ApplyMoneySettings` oyun başladıktan sonra `SetMoney` atlıyor). Bu durumda fiyatlar da ×1.0 olmuş olabilir. **Doğrulama:** Player.log'da `[DifficultyManager] Initialized with N players` ve `Starting money set to` satırları.
- 3P'de gün 1-3 kasası büyük ölçüde **başlangıç parası**, ve bu para gün-4 kirasına (1140) ayrılmış durumda. Kasa rakamı "harcanabilir para" gibi görünüyor ama değil.

## 2. Kart tablosu (sahne değerleri) ve net değer

"Net Δkasa" = kart tek başına, kilidinin açıldığı gün alınırsa gün-16 kasasındaki fark (fiyat dahil). Kart almayan takıma göre, 200 koşu/hücre. Gürültü ±~100.

| Kart | Tür | base/step/max | 1P | 3P (tüm seviyeler) | 3P fiyat ÷ orta günlük gelir | 3P net z/o/i | 4P net z/o/i | 3P zayıf Δkayıp |
|---|---|---|---|---|---|---|---|---|
| Geniş Ambar | omurga | 25/10/2 | 25 | 52 (126) | 0.12 | 52/−135/−166 | −10/−182/−70 | +10 |
| Paketleme İst. | omurga | 150/150/1 | 150 | 315 | 0.75 | 117/31/426 | 41/−34/270 | +27 |
| **Ek Hangar** | omurga | 200/100/1 | 200 | 420 | 1.00 | 106/**1009/1797** | 61/**1302/2006** | +2.5 |
| Görev Kademesi | omurga | 40/10/2 (P-muaf) | 40 | 40 (90) | 0.09 | 44/−41/−145 | 61/−145/61 | +6 |
| Ucuz Kira | T3 | 70/15/3 | 70 | 147 (535) | 0.24 | −120/63/180 | −78/201/200 | +6 |
| Prestij Simsarı | T3 | 80/10/2 | 80 | 168 (357) | 0.27 | 25/14/341 | 43/−9/191 | +12.5 |
| Prestij Ustası | T2 | 175/25/2 | 175 | 368 (788) | 0.85 | 108/**−233**/−107 | −34/**−237/−357** | +16.5 |
| **Hızlı Hangar** | T2 | 120/0/1 | 120 | 252 | 0.58 | 236/**−638**/25 | −53/**−1145**/−284 | +29 |
| Enerjik Ekip | T1 | 55/0/1 | 55 | 116 | 0.28 | ~0 | ~0 | −4 |
| Çevik Ekip | T1 | 110/0/1 | 110 | 231 | 0.55 | −5/−148/31 | 87/−121/−227 | −5.5 |
| Sabırlı Müşteriler | T1 | 60/0/1 | 60 | 126 | 0.30 | 88/−48/−186 | −19/−326/−363 | +13.5 |
| **Kumarbaz Kasası** | T2 | 350/0/1 | 350 | 735 | 1.70 | 57/**673/1190** | 28/**1014/1603** | −4.5 |
| Telefon Hattı | T1 | 60/0/1 | 60 | 126 | 0.30 | 97/29/61 | 28/102/−63 | +12.5 |
| **Mesai Saati** | T1 | 120/0/1 | 120 | 252 | 0.60 | 43/**−275/−358** | 50/**−348/−242** | +14 |
| Kaldıraçlı Kira | T3 | 300/0/1 | 300 | 630 | 1.01 | 13/208/341 | −100/438/452 | +2.5 |
| **Yüksek Volatilite** | T2 | 320/0/1 | 320 | 672 | 1.56 | 87/**501/913** | −19/**644/1059** | +7 |
| **Acil Fren** | T1 | 90/0/1 | 90 | 189 | 0.45 | 90/−198/−229 | 61/−256/−143 | **−52** |
| Kelle Koltukta | T3 | 320/0/1 | 320 | 672 | 1.08 | 107/349/689 | 130/477/787 | +11 |
| Toplu Alım | T1 | 30/0/1 | 30 | 63 | 0.15 | −6/−72/−103 | −20/−109/5 | +2 |

- **Dominant (orta/iyi için neredeyse bedava):** Ek Hangar, Kumarbaz Kasası, Yüksek Volatilite, Kelle Koltukta, Kaldıraçlı Kira (3P/4P). Hepsi gelir çarpanı: değerleri gelirle (1P'ye göre 3P ×4.1) büyüyor, fiyatları ise P-çarpanıyla (×2.1).
- **Zararlı (fiyat ne olursa olsun alınmamalı):** Hızlı Hangar (tır dolmadan kalkıyor), Mesai Saati, Prestij Ustası. 1P/2P'de Paketleme İstasyonu da bu gruba giriyor.
- **Ölü (net ≈0 veya negatif):** Geniş Ambar, Görev Kademesi, Toplu Alım, Sabırlı Müşteriler, Çevik Ekip, Enerjik Ekip, Prestij Simsarı.
- **Acil Fren iki yüzlü:** orta/iyi için değeri sıfır, çünkü hiç tetiklenmiyor. **Zayıf için oyundaki en güçlü kart:** 3P'de −52 pp, 4P'de −62 pp kayıp. Bir kirayı (1140-2817 TL) 189-225 TL'ye siliyor.

## 3. Karar: upgrade'ler ucuz mu?

**"Ucuz" tanımı:** kart fiyatı, o takımın o günkü günlük gelirinin < 0.5'i (fiyat hissedilmiyor) VE/VEYA orta/iyi takım havuzun tamamını oyun bitmeden alabiliyor (fiyat hiçbir tercihi zorlamıyor).

- **Nominal olarak evet, özellikle 3P/4P'de.** 1P'de T1 kartlar 0.5-1.2 günlük gelire mal oluyor. 3P'de 0.1-0.6. Gelire göre göreli fiyat 1P'ye kıyasla 2P ×0.68, 3P ×0.51, 4P ×0.47, yani 3P'de kartlar 1P'nin yarı fiyatında. İyi takım 25 seviyenin ~24'ünü gün 12-13'te bitiriyor, katalog doyuyor (3P toplamı 6694 TL).
- **Ama fiyatı artırmak işe yaramıyor, test edildi:**
  - Eski P-dizisi {1,2,2.95,3.7}, 3P açgözlü orta: kayıp %13 → **%38**. 4P: %17 → **%64**.
  - Orta dizi {1,1.8,2.5,3.0}: 4P orta açgözlü kayıp %17 → %39.
  - Gün-ölçekli fiyat (+%5/gün): 3P orta açgözlü kayıp %13 → %30.
  - Sadece 5 dominant karta hedefli zam: 4P orta açgözlü kayıp %17 → %33.
  - Neden: ayrım gözetmeden alan takım fiyat ne olursa olsun yine alıyor, sadece daha çok para kaybediyor. Seçici (akıllı) oyuncu için de zam, kart almayı "hiç almamaktan" kötü hâle getiriyor: kira kilidi + orta dizi ile 3P orta seçici 4071 < hiç 4475.
- **Playtest algısı sayılarla kısmen çelişiyor.** "Ucuz" hissi, kasanın gün 1-3'te çoğunlukla gün-4 kirasına ayrılmış başlangıç parası olmasından geliyor. 200 TL, 3P'de ilk kiranın %18'i. Gün-1 kasası modelin altında olan bir takım için gün 2'de 3 kart almak tam olarak **zayıf-tier tuzağı**. Sim, ayrım gözetmeden kart alan zayıf 3P takım için %95-100 iflas öngörüyor.
- **Asıl sorun fiyat değil, iki şey:** (1) kasa "harcanabilir" ile "kiraya ayrılmış" parayı ayırmıyor; (2) 19 kartın ~10'u ölü veya zararlı. Ayrım gözetmeden kart alan takım bu yüzden cezalanıyor. Seçici alıcı bugün de kazançlı: canlıda 3P orta 5247, iyi 11046; hiç almayan 4475 / 7747.

## 4. Öneri ve sim kanıtı

**Ö-A (ana öneri, yeni ekonomi değeri + oyun kuralı):**
- **Kira fonu kilidi `upgradeRentReserveFraction = 1.0`.** Kart alımı ve reroll, kasayı **sıradaki kiranın altına** düşüremez.
- **Acil Fren muaf.** Tek kurtarıcı kart olduğu için kilitten muaf tutuldu; muafiyet olmadan zayıf 2P/3P'de kayıp %30-55'e çıkıyor.
- UI: panelde "Kira fonu: X TL (gün N)" gösterilsin, kilitli kartta sebep yazsın.
- Fiyatlar, P-dizisi, reroll: **DEĞİŞMESİN.**

Kayıp % / gün-16 kasa ortalaması. Strateji sırası: hiç / açgözlü / mantıklı / seçici. 300 koşu, event'ler açık.

| Hücre | CANLI | ÖNERİ (Ö-A) |
|---|---|---|
| 1P zayıf | 20:218 / 100:220 / 92:314 / 17:203 | 20:218 / **11**:190 / **10**:183 / **3**:205 |
| 1P orta | 0:1131 / 54:272 / 14:289 / 0:687 | 0:1131 / **0**:306 / **0**:290 / 0:697 |
| 1P iyi | 0:2474 / 9:1687 / 1:1504 / 0:2565 | 0:2474 / 0:999 / 0:1244 / 0:2572 |
| 2P zayıf | 21:465 / 100:537 / 92:629 / 17:539 | 21:465 / **1**:342 / **3**:350 / **1**:438 |
| 2P orta | 0:3392 / 16:2781 / 6:2247 / 0:3579 | 0:3392 / **0**:1555 / **0**:1817 / 0:3253 |
| 2P iyi | 0:6671 / 0:8905 / 0:8294 / 0:8809 | 0:6671 / 0:7427 / 0:7328 / 0:8718 |
| 3P zayıf | 45:901 / 100:886 / 95:1087 / 44:1063 | 45:901 / **2**:649 / **56**:937 / 54:943 |
| 3P orta | 0:4475 / 13:5677 / 7:3425 / 0:5247 | 0:4475 / **0**:2581 / **0**:3104 / 0:4654 |
| 3P iyi | 0:7747 / 0:12304 / 0:10717 / 0:11046 | 0:7747 / 0:8658 / 0:9448 / 0:10672 |
| 4P zayıf | 61:1420 / 99:1341 / 96:1493 / 67:1550 | 61:1420 / **3**:961 / **63**:1383 / 62:1389 |
| 4P orta | 0:4575 / 17:7150 / 9:4097 / 0:5865 | 0:4575 / **0**:2539 / **0**:2992 / 0:4538 |
| 4P iyi | 0:8558 / 0:14506 / 0:11682 / 0:12443 | 0:8558 / 0:8163 / 0:9283 / 0:11135 |

- **Zayıf-tier tuzağı:** kart alan zayıf takımın kaybı %92-100 → %1-63. Hiç almayanın (%20-61) altına veya seviyesine iniyor. Açgözlü zayıf 2P-4P'de %1-3'e düşüyor, çünkü Acil Fren'i alıyor. KÖTÜLEŞMİYOR, belirgin düzeliyor.
- **Orta/iyi:** kartlardan kaynaklanan iflas her hücrede %0.
- **Bedeli:** kart alan orta takımın gün-16 kasası azalıyor: seçicide %9-23, açgözlüde %44-64 (1P hariç). Sebep: alımlar kira sonrasına kayıyor. Seçici orta 3P'de hâlâ kart almayanın üstünde, 4P'de eşit (4538 / 4575), 1P/2P'de altında.
- **"Kart almak hiç almamaktan iyi" hedefi:** kayıp ölçütünde, en iyi strateji ile 12/12 hücrede sağlanıyor (≤ kart almayan; canlıda 11/12). Kasa ölçütünde iyi 4/4 ve orta 1/4. Canlıda kasa ölçütü orta 3/4 idi.
- Kilit oranı taraması (Acil Fren muaf), açgözlü zayıf 3P kayıp: 0.5 → %97, 0.75 → %38, **1.0 → %2**. Yumuşak kilit tuzağı çözmüyor.

**Ö-B (fiyat değil, efekt; gameplay'e):**
- Hızlı Hangar, Mesai Saati ve Prestij Ustası zararlı. Ya efektleri düzeltilmeli ya da draft'tan çıkarılmalı (`disabledInDraft`). Zayıf takımda kaybı +14 ile +29 pp artırıyorlar.
- Fiyat düşürmek bu kartları düzeltmez.

**Ö-C (bug):** `all_in` / `leveraged_rent` grace'i silmeli (bayrak), `%0`'a çekmemeli.

## 5. Riskler
- Ö-A ile 3P/4P'de gün 1-3 arası neredeyse hiç kart alınamaz (başlangıç parası < ilk kira). Draft ilk günlerde "vitrin" olur. Playtest'te sıkıcı hissettirebilir. Kilidin görünür olması (UI) şart.
- Açgözlü zayıf takımın düşük kaybı Acil Fren'in teklif edilmesine bağlı. Mantıklı zayıf 3P/4P hâlâ %56-63.
- Profil süreleri (zayıf/orta/iyi) varsayım. Playtest ekibi gün 1'de zayıf profilin de altında kaldı, yani gerçek dünyada zayıf profil iyimser olabilir.
- Oyuncu sayısı yanlış algılanıyorsa (§1b), fiyat ve start parası 1P değerinde kalır. Bu durumda "ucuz" hissinin kaynağı sayı değil bug olur.

## 6. Önceki raporlarla çelişkiler
1. GDD §13.2 bayat: P-dizisi {1,2,2.95,3.7} yazıyor, canlı {1,1.6,2.1,2.5}. Görev Kademesi GDD'de 80/20, sahnede **40/10**. Geniş Ambar GDD'de 60/30, sahnede **25/10**. GDD §2.3/§4.1 telefon "+20 TL" diyor, canlı **0**.
2. GDD §13 ve PerkEffect yorumları "all_in / leveraged_rent grace'i iptal eder" diyor. Kodda grace **bedava** (bkz. §0).
3. "Ö1-Ö3 ile kart almak kazançlı" iddiası yalnız seçici alıcı ve iyi profil için doğru. Ayrım gözetmeden alan orta takım canlıda hâlâ kart almayandan riskli (%6-54 vs %0). Sayılar önceki matrisle birebir aynı; fark yorumdan geliyor.
4. Önceki raporlarda Acil Fren "zayıf/ölü" sınıfında. Zayıf takım için en değerli kart bu (−52/−62 pp).
5. Ajan hafızasında "sabır prefab 8/14/2" yazıyordu. Prefab şu an 15/20/2 (hafıza bayat, düzeltildi).
