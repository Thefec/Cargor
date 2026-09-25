# Her gün event + 6 kapalı kartın yeniden tasarımı: tasarım ve sim ölçümü (2026-09-24/25)

Plan: `plans/her-gun-event-ve-kart-yenileme.md` (İş 1 adım 1-2 + İş 2 adım 1). Bu doküman **yalnızca tasarım**. Oyun koduna, `config.json`'a ve sahneye dokunulmadı.
Harness: `tools/economy_sim/event_kart_2026_09_24.py`. `sim.py`'yi import ediyor ama değiştirmiyor. Yeni özellikler kapalıyken `sim.Run` ile bit-birebir aynı sonucu veriyor (`selftest`: 312 koşu, 0 uyuşmazlık).
Sonuçlar `tools/economy_sim/results_event_kart_2026-09-24/` altında: `matrix_*.csv`, `event_iso.csv`, `cards_payback*.csv`, `report.md`.

---

## Karar özeti

1. **16 event'in hepsi kodda bir şeyi değiştiriyor, tamamen ölü olan yok.** Ama sorunlu olanlar var:
   - **4'ü kısmen çalışıyor:** BUSY, ANGRY, GOLDEN ve MARKETING'de müşteri artışının yalnızca %15-50'si gerçekleşiyor. Sebep: varış aralığı ölçeklenmiyor.
   - **3'ünün etkisi çok zayıf:** RELAXED +%0,5-4, CUSTOMER SUPPORT +%1,5-5. SLOW/EXPRESS'teki "kalkış süresi" parçası da kozmetik kalıyor.
   - **FATIGUE'da açıklama ile kod uyuşmuyor:** Koddaki gizli "müşteri ×0,85" gelir kaybının asıl kaynağı, ama açıklamada yok.
   - **`IsGoldenBoxDay`/`IsVIPServiceDay` bayrakları hiçbir yerden okunmuyor.** Yine de bu iki event'in çarpanları genel kanallardan çalışıyor (§A).
2. **Gameplay'i bloke eden iki yapısal engel var:**
   - `DayCycleManager.OnNewDay` 1. günde tetiklenmiyor (`DayCycleManager.cs:1002` yalnızca `NextDay`'de çağırıyor). Gün 1-3'e event koymak için EventEffectManager'ın koşu başında da event aktif etmesi gerekiyor.
   - Event isimleri iki ayrı listede duruyor: `EventCalendarUI._allEvents` :162 ve `EventEffectManager.eventNames` :35. Aktif event ağda index olarak taşınıyor (`NetworkVariable<int>`). Yeni event'ler **iki listenin de sonuna** eklenmeli.
3. **Katalog 22 event'e çıkıyor** (12 pozitif/takas, 10 negatif):
   - **6 yeni:** Tek Renk Günü, Görev Günü, Acele Primi, Sabırsız Şoförler, İade Dalgası, Tedarik Grevi.
   - **4 yeniden tanımlı:** Altın Kutu artık "günün rengi ×1,6", VIP artık "2 kalem müşteri", Sakin Gün güçlendirildi, Müşteri Hattı'nda arama zaman atlatmıyor.
   - **Düzeltmeler:** Müşteri artışı olan event'lerde varış aralığı ÷ çarpan yapılıyor. FATIGUE'daki gizli müşteri cezası kaldırılıyor.
   - Hiçbiri yeni bir sistem gerektirmiyor, hepsi mevcut sistemlere küçük kodla bağlanıyor (§B).
4. **Dağılım kuralı:**
   - Gün 1-3: 3 pozitif ve öğretici event.
   - Gün 5-7 ve 9-11: pozitif + negatif + yazı-tura.
   - Gün 13-15: 1 pozitif + 2 negatif (orta/sert).
   - Ortalama 7 pozitif / 5 negatif. Koşu içinde tekrar yok (§C).
5. **6 kart (§D):**
   - **Güler Yüz = Bahşiş.** Gelir kartı.
   - **Sağlam Kasa = Taksit.** +1 kira taksiti hakkı. Acil Fren'in alternatifi: o da kilitten muaf, ama Acil Fren ile özel grupta (ikisinden yalnızca biri alınabilir).
   - **Su Sebili = Serinlik.** Negatif event'leri %75 hafifletir.
   - **Dinç Ekip = Sabah Vardiyası.** Her sabah rafa hazır kutu koyar.
   - **Geniş Kuyruk = Sıra Numaratörü.** Müşteri sabrı yalnızca masaya gelince işlemeye başlar.
   - **Uzun Kuyruk = Hava Raporu.** Kira dönemi başına 1 kez yarının negatif event'ini değiştirir. Aktif bir karar.
6. **Sim sonucu** (paket = yeni katalog + her gün event + tekrarsız + 6 kart, rastgele teklif, hücre başına 5000 koşu):
   - Zayıf kayıp (hic/acgozlu/mantikli ortalaması) **%25,4 → %19,4**.
   - Orta/iyi ≥%99,9 kazanıyor. Kaybedilemezlik değişmedi.
   - **Acil Fren'i hiç görmeyen** zayıf P3/P4 kazanma oranı **%42 / %35 → %84 / %80**. Tek kart bağımlılığı fiilen kırıldı.
   - acgozlu−mantikli farkı P3'te 18 → 10 puan, P4'te 25 → 16 puan (§E).
7. **Yan bulgu (önemli): kart teklifi yalnızca gün ile tohumlanıyor** (`UpgradePanel.cs:1343`, reroll `:1533`). Her koşuda aynı gün aynı 3 kart geliyor.
   - 2026-09-24 mc40k'daki "Acil Fren farkı 54-63 puan" sonucunun çoğu bu artefakttan geliyordu. Rastgele teklifle fark 18-25 puan.
   - Öneri: teklif tohumuna koşu seed'i de eklensin. Takvim zaten seed'li.
8. **Her gün event, sonucu ekonomik olarak değiştirmiyor** (yalnızca event: zayıf kaybı −3,3 puan). Katkısı davranış çeşitliliği: renk önceliği, hızlı yükleme, stok tutma, görev seçimi. "Sonuç önemsiz" hissi için kaybedilemezlik kararı (Karar 3) ayrı ve hâlâ açık.

### Kullanıcıya sorular
- **S1:** Taksit, Acil Fren ile özel grupta mı olsun (önerilen), yoksa ikisi birlikte alınabilsin mi?
  - Birlikte alınabilirse (`paket_noexcl`) zayıf takımın 3 can simidi olur: zayıf acgozlu kaybı P3'te %7,7 → %3,5 iner. Ama oyun daha da kaybedilmez hale gelir.
- **S2:** Kart adları. Backbone'larda eski adı koruyup yalnızca açıklamayı değiştirmeyi öneriyorum (Güler Yüz, Sağlam Kasa, Su Sebili, Dinç Ekip temaya uyuyor). "Geniş Kuyruk → Sıra Numaratörü" ve "Uzun Kuyruk → Hava Raporu" için yeni ad gerekiyor. Onay?
- **S3:** Teklif tohumu düzeltmesi (koşu seed'i) bu dala mı alınsın, ayrı bir iş mi olsun? Etkisi büyük: her koşuda farklı kart teklifi gelir.
- **S4:** Gün 13-15 için sert negatif havuzu (Tedarik Grevi −%37-45, Pazarlama −%14-34, İade Dalgası −%27-28) kabul mü? Hassasiyet (`paket_hard`, negatifler ×1,5) zayıf kaybını +4 puan artırıyor.
- **S5:** "Karışık Sevkiyat" kataloğa alınmadı. Sim'de gelir **+%10-19** çıkıyor (tırın ihtiyacı birden çok renkle eşleşebiliyor). Model karışık tırın zihinsel yükünü taşımıyor. Playtest'te zor hissettiriyorsa negatif olarak geri eklenebilir.

---

## A) Event denetimi (16 event, güncel kod)

Tanımlar `Assets/NewCss/Events/EventEffectManager.cs:130-361` (`InitializeEventMultipliers`) içinde. Uygulama `:484-540` (`SaveCurrentValuesAndApplyMultipliers`), sonradan doğan nesneler için `:589-623`.
Etki sütunu: sim'de o event'i geçerli her kira-dışı güne zorlayıp, event'siz aynı tohumlu koşularla **aynı günleri** kıyasladım (strateji `hic`, 2000 koşu). Günlük net gelir farkı = kutu geliri + festival − para cezası. Kaynak: `event_iso.csv` (catalog=old).

| # | Event | Kod tanımı | Efekti okuyan kod | Durum | Günlük net gelir Δ, P3 zayıf / P3 orta |
|---|---|---|---|---|---|
| 1 | BUSY DAY | müşteri ×1,35, sabır ×0,85 (:132) | sayı `CustomerManager.cs:412`; sabır `CustomerAI.cs:323`→`:547` | **KISMEN**: varış aralığı (`CustomerManager.cs:436-470`) event'ten habersiz. +%35 hedefin +%6 (zayıf) / +%18'i (orta) gerçekleşiyor | −1,3 / +3,3 |
| 2 | DELIVERY BONUS | ödül ×1,2 (:146) | `Truck.cs:315` → `:783` | ÇALIŞIYOR | +24,5 / +15,2 |
| 3 | ANGRY CUSTOMERS | sabır ×0,6, müşteri ×1,1 (:160) | aynı (1) | ÇALIŞIYOR (sabır); müşteri artışı kısmen (+%6-10) | −0,1 / −3,1 (kaçan müşteri +1,1-1,3/gün) |
| 4 | RELAXED DAY | sabır ×1,3 (:174) | `CustomerAI.cs:547` | Çalışıyor ama **neredeyse etkisiz** | +0,5 / +3,6 |
| 5 | SLOW LOGISTICS | ödül ×0,92, kalkış ×1,5 (:188) | ödül `Truck.cs:783`; kalkış `Truck.cs:899` | **KISMEN**: kalkış bekleme tabanı birkaç saniye, fark kozmetik | −14,0 / −7,7 |
| 6 | EXPRESS CARGO | ödül ×1,08, kalkış ×0,7 (:202) | aynı (5) | KISMEN (5 ile aynı sebep) | +9,0 / +5,4 |
| 7 | HEAVY BOXES | hareket ×0,85, sprint ×0,8 (:216) | `PlayerMovement.cs:491/499` | ÇALIŞIYOR. Yalnızca o anki kendi oyuncusu etkileniyor; geç katılan yakalanmıyor | −18,4 / −8,9 |
| 8 | GOLDEN BOX DAY | ödül 1,15, kalkış 0,8, hareket 1,08, sprint 1,2, stamina 0,8, müşteri 1,15 (:230) | genel kanallar | ÇALIŞIYOR, ama `IsGoldenBoxDay()` (:702) **okuyucusuz** (grep 0). Müşteri artışı kısmen | +22,4 / +18,4 |
| 9 | OPPORTUNITY DAY | kart fiyatı ×0,8 (:244) | `UpgradePanel.cs:1771` | ÇALIŞIYOR (hic stratejide doğal olarak 0) | 0 / 0 |
| 10 | FATIGUE PROBLEM | stamina 0,6, sprint 0,7 **+ hareket 0,9 + müşteri 0,85** (:258) | (7) + `CustomerManager.cs:412` | ÇALIŞIYOR, **açıklama-kod uyuşmazlığı**. Açıklama yalnızca stamina/sprint diyor. Kaybın çoğu gizli −%15 müşteriden | −28,3 / −20,4 |
| 11 | VIP SERVICE | ödül ×1,12 (:272) | `Truck.cs:783` | ÇALIŞIYOR. `IsVIPServiceDay()` (:709) okuyucusuz. Delivery Bonus'un zayıf kopyası | +14,3 / +8,7 |
| 12 | SURPRISE AUDIT | isim-bazlı ×2 (`GetPenaltyMultiplier` :455) | `Truck.cs:747` (yanlış teslim TL), `BoxFallPenalty.cs:146` (düşürme TL), `CustomerAI.cs:1412`, `GameStateManager.cs:653/687` (prestij) | ÇALIŞIYOR | −22,4 / −7,9 |
| 13 | RAINY DAY | müşteri ×0,8 (:286) | `CustomerManager.cs:412` | ÇALIŞIYOR (azalış tavana takılmıyor) | −29,7 / −21,4 |
| 14 | MARKETING DAY | ödül 0,7, müşteri 1,2 (:300) | (2) + (1) | KISMEN (müşteri artışı +%5-15) | −36,0 / −17,6 |
| 15 | CUSTOMER SUPPORT | isim-bazlı, zaman atlama ×0,5 | `PhoneCallManager.cs:316` | ÇALIŞIYOR, küçük. Zayıf takım telefonu az kullanıyor | +3,8 / +5,3 |
| 16 | FESTIVAL DAY | gün başında kiranın %10-20'si nakit | `EventEffectManager.cs:383-421` | ÇALIŞIYOR. Host'ta çift tetik korumalı | +94,1 / +41,1 (tek gün) |

**Müşteri artışının gerçekleşen kısmı** (spawn/kota × çarpan; `event_iso.csv`):

| Event | Hedef | Zayıf P2/P3 | Orta P2/P3 |
|---|---|---|---|
| BUSY | +35% | +6,0 / +6,4 | +13,0 / +17,6 |
| ANGRY | +10% | +5,0 / +5,7 | +8,8 / +9,7 |
| GOLDEN | +15% | +4,3 / +5,8 | +8,8 / +12,7 |
| MARKETING | +20% | +6,3 / +5,1 | +12,0 / +15,2 |

**Yapısal notlar (uygulamadan önce gameplay'in bilmesi gerekenler):**
- **S-1: Gün 1'de event aktif olmuyor.** `EventEffectManager.OnNewDayHandler` (:363) yalnızca `OnNewDay`'de çalışıyor. `OnNewDay` ise yalnızca `NextDay`'den çağrılıyor (`DayCycleManager.cs:1002`, ClientRpc `:1186`). Gün 1-3 event planı için koşu başında da (server) `currentActiveEvent` set edilmeli.
- **S-2: Sıra bağımlılığı riski.** `CustomerManager.HandleNewDay` (abonelik `Start` :299) ile `EventEffectManager.OnNewDayHandler` (abonelik `OnNetworkSpawn` :109) aynı static event'i dinliyor. Kota hesabı çarpan set edilmeden önce çalışırsa bir önceki günün çarpanını kullanır. Muhtemelen doğru sırada ama doğrulanmadı. Öneri: CustomerManager çarpanı hesap anında `EventEffectManager`'dan çeksin.
- **S-3: İsim listesi iki yerde + index-bazlı ağ değişkeni.** Yeni event'ler iki listenin de **sonuna** eklenmeli, yerlerine lokalizasyon anahtarı eklenmeli.
- **S-4: Stamina/sprint bileşenleri sim'de modellenmiyor.** Sim'de stamina yok. HEAVY/FATIGUE/GOLDEN'ın o kısmı ölçülmedi.

---

## B) Önerilen event kataloğu (22 event)

Uygulama kısaltmaları:
- **M** = mevcut `EventMultipliers` alanıyla yapılır.
- **K** = küçük kod: mevcut bir sisteme tek kanca, yeni sistem yok.

Hiçbir event yeni sistem gerektirmiyor. Etki sütunu yeni tanımın iso ölçümü (`event_iso.csv` catalog=new). İade Dalgası'nın değeri 0,45 ile yeniden ölçüldü.

| # | Ad (TR / EN) | Tip | Şiddet / bant | Efekt ve değer | Δ gelir P3 zayıf / orta | Uygulama |
|---|---|---|---|---|---|---|
| 1 | Teslimat Primi / DELIVERY BONUS | + | hafif (gün 1+) | kutu ödülü ×1,20 | +24,5 / +15,2 | M |
| 2 | Ekspres Kargo / EXPRESS CARGO | + | hafif | ödül ×1,10, kalkış ×0,5, **hangar süresi ×1,2** | +21,8 / +6,4 | M + K (hangar alanı, bkz. 19) |
| 3 | Sakin Gün / RELAXED DAY | + | hafif | sabır ×1,5, **tüm cezalar ×0,5** | +11,7 / +8,3 | M + K: `GetPenaltyMultiplier` isim yerine struct alanı okusun |
| 4 | Fırsat Günü / OPPORTUNITY DAY | + | hafif | kart fiyatı ×0,7 | (alışverişe bağlı) | M |
| 5 | Müşteri Hattı / CUSTOMER SUPPORT | + | hafif | telefon araması **zaman atlatmaz** (×0) | +9,3 / +12,2 | K: `PhoneCallManager.cs:316` sabit 0,5 yerine struct alanı |
| 6 | **Tek Renk Günü / MONOCHROME DAY** (yeni) | + | hafif (öğretici) | bugün tüm tırlar ve müşteriler tek renk (renk takvim seed'inden) | +33,5 / +16,5 (varsayım: yanlış teslim ×0,3) | K: TruckSpawner + CustomerManager renk torbası override (server) |
| 7 | **Görev Günü / QUEST DAY** (yeni) | + | hafif | başarılı görevin ödülü (TL + prestij) ×3 | +3,6 / +3,4 | K: Quest ödül ödeme noktası (`Assets/Scripts/Quest`) |
| 8 | Altın Kutu Günü / GOLDEN BOX DAY (**yeniden**) | + | hafif | **günün rengi** (seed) kutuları ×1,6 ödül. Eski karışık çarpanlar kalkıyor | +33,1 / +21,3 | K: Truck ödül hesabında renk kontrolü. `IsGoldenBoxDay` okuyucu kazanıyor |
| 9 | VIP Hizmet / VIP SERVICE (**yeniden**) | + | orta, gün ≤8 | tüm müşteriler **2 kalem** getirir (gün-9 kilidi o gün için açılır) | +16,0 / +37,2 | K: `PostRentFeatureUnlocks.IsDualItemUnlocked(day) \|\| IsVIPServiceDay()` |
| 10 | Festival / FESTIVAL DAY | + | orta (gün 5+) | gün başı nakit = kiranın %10-20'si | +94 / +41 | M (değişmedi) |
| 11 | **Acele Primi / RUSH BONUS** (yeni) | + | orta | hangar süresinin **ilk yarısında** yüklenen kutu ×1,4 | +27,2 / +22,9 | K: Truck ödülünde kalan hangar süresi/toplam ≥ 0,5 kontrolü |
| 12 | Yoğun Gün / BUSY DAY (**takas**, UI "Nötr") | ± | orta | müşteri ×1,35 **+ varış aralığı ÷1,35**, sabır ×0,85 | +1,7 / +14,6 (kaçan +1,1-1,9/gün) | K: `AdvanceNextSpawnThreshold` içinde `interval /= max(1, eventCustomerMultiplier)` |
| 13 | Sinirli Müşteriler / ANGRY CUSTOMERS | − | hafif | sabır ×0,6, müşteri ×1,1 (+ varış düzeltmesi) | +1,7 / −4,0 (kaçan +1,4 → prestij) | M + K (12 ile aynı) |
| 14 | Yorgunluk / FATIGUE PROBLEM (**düzeltme**) | − | hafif | hareket ×0,9, sprint ×0,7, stamina ×0,6. **Gizli müşteri ×0,85 kaldırıldı** | −11,0 / −4,9 (stamina hariç) | M |
| 15 | Yavaş Lojistik / SLOW LOGISTICS | − | hafif | ödül ×0,9, kalkış ×2 | −16,6 / −9,6 | M |
| 16 | Ağır Kutular / HEAVY BOXES | − | orta | hareket ×0,85, sprint ×0,8 | −18,4 / −8,9 | M |
| 17 | Sürpriz Denetim / SURPRISE AUDIT | − | orta | tüm cezalar ×2 | −22,4 / −7,9 | M |
| 18 | Yağmurlu Gün / RAINY DAY | − | orta | müşteri ×0,8 | −29,7 / −21,4 | M |
| 19 | **Sabırsız Şoförler / IMPATIENT DRIVERS** (yeni) | − | orta | tır hangar süresi ×0,6 | −5,6 / −19,0 (P2: −21,6 / +1,3; P'ye göre değişken) | K: `EventTruckValues`'a `hangarStayDuration` + `fast_hangar` için rebase (şu an "event bu alana dokunmuyor" varsayımı var, :651-655) |
| 20 | Pazarlama Günü / MARKETING DAY | − | sert | ödül ×0,7, müşteri ×1,2 (+ varış düzeltmesi) | −34,1 / −13,5 | M + K |
| 21 | **İade Dalgası / RETURN WAVE** (yeni) | − | sert, gün ≥5 | iade (kutu isteyen) müşteri oranı %25 → %45 | −28,4 / −26,9 (kaçan +1,1-1,5) | K: `ShouldEnterBoxRequestMode` oran parametresi |
| 22 | **Tedarik Grevi / SUPPLY STRIKE** (yeni) | − | sert | müşteri ×0,7, ödül ×0,9 | −44,7 / −37,1 | M |

- **Neden bunlar:** Yeni ve yeniden tanımlı event'lerin yarısı oyuncu davranışını değiştiriyor:
  - Altın Kutu → o rengi önceliklendir
  - Acele Primi / Sabırsız Şoförler → yüklemeye koş
  - İade Dalgası → rafı dolu tut
  - Görev Günü → zor görevi seç
  - VIP → 2 kalemli müşteriyi karşıla
  - Tek Renk → öğretici kolay gün
- **Şiddet ölçütü** (zayıf P3 günlük net gelir etkisi): hafif ≤%15, orta %15-30, sert >%30.
  - İade Dalgası %28 ile sınırda. Kaçan müşteri etkisi yüksek olduğu için sert sayıldı.
- **Kaldırılan / kataloğa alınmayan:** Karışık Sevkiyat (S5).
- **Netcode:** Günün rengi ve Tek Renk rengi takvim üretimindeki aynı `System.Random`'dan çekilip event kaydında saklanmalı. Böylece client UI aynı rengi gösterir. Ödül hesabı zaten server'da.

---

## C) Dağılım kuralı

| Bant | Günler | Slotlar (bant içinde karıştırılır) | Pozitif havuz | Negatif havuz |
|---|---|---|---|---|
| Öğretici | 1, 2, 3 | P, P, P | yalnızca "hafif" pozitifler (1-8) | — |
| Kira-1 sonrası | 5, 6, 7 | P, N, X | tüm pozitifler (gün kısıtıyla) | yalnızca hafif (13-15) |
| Kira-2 sonrası | 9, 10, 11 | P, N, X | tüm pozitifler | hafif + orta (13-19) |
| Final | 13, 14, 15 | P, N, N | tüm pozitifler | orta + sert (16-22) |

- **X** = %50 pozitif / %50 negatif. Beklenen dağılım koşu başına **7 pozitif / 5 negatif** (ölçülen: 6,9-7,1 pozitif).
- Kira günleri (4/8/12/16) event'siz.
- **Tekrar yok:** Seçilen event koşu havuzundan düşer.
  - Havuz 12 pozitif / 10 negatif, kısıtlar gün bazında sağlanıyor.
  - Emniyet: aday kalmazsa şiddet kısıtı gevşetilir, tip kısıtı korunur.
- Gün kısıtları: VIP ≤8, İade Dalgası ≥5.
- **Algoritma (gameplay için):** `GenerateInitialEvents(seed, baseDay)` içinde tek `System.Random(seed)`. Her bant için:
  1. Slot dizisini karıştır.
  2. X'i yazı-turayla çöz.
  3. Filtrelenmiş havuzdan (tip, şiddet, gün, kullanılmamış) çek.
  4. Günün rengi gibi parametreleri aynı rng'den çek.
  - Aynı seed → tüm peer'lerde aynı takvim (mevcut garanti korunuyor).
  - Harness'taki referans uygulama: `EKRun._gen_calendar`.
- **Hava Raporu kartı** takvimi değiştirebildiği için değişiklik server'da yapılıp `_eventsByDay` ile senkronlanmalı. Seed'den yeniden üretim artık yetmez.

---

## D) 6 kapalı kartın yeni tasarımı

Kurallar:
- Liste sırası ve sayısı değişmiyor (NetworkList index).
- Backbone'lar (kind 0, effectId boş) için **yeni `effectId` verilmesi** öneriliyor. displayName eşleşmesi tuzağından kaçınmak için.
- Fiyatlar P ile ölçekleniyor (×1 / 1,6 / 2,1 / 2,5).

| Sahne kaydı (sıra) | Önerilen ad | effectId | Efekt | Tür / tier | maxLevel | base / step | P1-P4 fiyat | Kira fonu kilidi |
|---|---|---|---|---|---|---|---|---|
| Güler Yüz (#8) | Güler Yüz (açıklama: Bahşiş) | `tip_jar` | Servis edilen her müşteri, P-bazlı kutu ödülünün L1 %10 / L2 %20'si kadar bahşiş bırakır. L1: 5/6/7/9 TL, L2: 10/11/14/18 TL | omurga | 2 (eskisi 3) | 80 / 80 | L1 80/128/168/200 · L2 160/256/336/400 | tabi |
| Sağlam Kasa (#4) | Sağlam Kasa (açıklama: Taksit) | `grace_plus` | +1 kira taksiti (grace) hakkı. Kartla tüm taksitlerde kasanın %70'i alınır (taban %80). **Özel grup:** Acil Fren, Kaldıraçlı Kira, Kelle Koltukta | omurga | 1 (eskisi 3) | 60 / 0 | 60/96/126/150 | **MUAF** (Acil Fren gibi) |
| Su Sebili (#7) | Su Sebili (açıklama: Serinlik) | `cooler` | Negatif event'lerin sapması %75 hafifler (örnek: müşteri ×0,8 → ×0,95; ceza ×2 → ×1,25; hangar ×0,6 → ×0,9). **Event etkileşimi 1** | omurga | 1 | 40 / 0 (eskisi 500) | 40/64/84/100 | tabi |
| Dinç Ekip (#5) | Dinç Ekip (açıklama: Sabah Vardiyası) | `morning_shift` | Her gün başında rafa L1 1 / L2 2 hazır (paketlenmiş) kutu gelir, rastgele renk. Raftaki toplam <8 ise eklenir. `energetic_crew` kopyası olmaktan çıkıyor | omurga | 2 (eskisi 3) | 80 / 80 | L1 80/128/168/200 · L2 160/256/336/400 | tabi |
| Geniş Kuyruk (#3) | **Sıra Numaratörü** | `ticket_queue` | Müşterinin sabır sayacı yalnızca servis masasına geçince başlar, kuyrukta beklerken işlemez | omurga | 1 (eskisi 3) | 70 / 0 | 70/112/147/175 | tabi |
| Uzun Kuyruk (#17, perk) | **Hava Raporu** | `forecast` (eski `long_queue` yerine) | Kira dönemi (4 gün) başına 1 kez, **yarının negatif event'ini** koşuda henüz çekilmemiş, güne uygun bir pozitif event'le değiştir. Takvimde buton. **Event etkileşimi 2, aktif karar** | perk T1 | 1 | 60 / 0 (eskisi 240) | 60/96/126/150 | tabi |

Ölçüm (`cards_payback.csv`, Serinlik/Hava Raporu için `cards_payback_v2_cooler_forecast.csv`):
- Paket (her-gün event), strateji hic + kart zorla alındı.
- Kilit izin verdiği ilk gün alındı. Orta profilde gün 1-3.5, zayıf P3/P4'te kilit yüzünden gün 4-6.
- Aynı tohumlu kartsız ikizle kıyaslandı, 1500 koşu.
- Amortisman = maliyet ÷ (brüt gelir farkı / aktif gün).

| Kart | Orta amortisman P1/P2/P3/P4 (gün) | Zayıf kayıp: kartla / kartsız (P1, P2, P3, P4) | Yorum |
|---|---|---|---|
| Bahşiş | 5,0 / 3,0 / 2,9 / 2,6 | 9,4/17,5 · 8,8/18,9 · 31,2/42,7 · 48,3/54,3 | Gelir kartı. Zayıf P3/P4 kilit yüzünden geç alabiliyor |
| **Taksit** | (sigorta, gelir yok: orta −59…−167 TL) | **2,7/17,5 · 3,3/18,9 · 8,2/42,7 · 9,4/54,3** | Acil Fren alternatifi. Acil Fren referansı: 1,3/0,4/1,5/1,3. **Bilinçli olarak daha zayıf** (kasanın %70'ini alıyor) ama prestij cezası yok |
| Serinlik | 7,2 / 4,3 / 3,1 / 2,9 | nötr (±3 puan) | Negatif gün sayısı az (~5) olduğu için değeri P1'de düşük |
| Sabah Vardiyası | 2,3 / 3,7 / 4,1 / 4,1 | 4,9/17,5 · 12,3/18,9 · 39,3/42,7 · 52,0/54,3 | P1 zayıfa büyük katkı (tek kişilik paketleme darboğazı) |
| Sıra Numaratörü | 17,5 / 4,4 / 3,0 / 2,5 | nötr / hafif negatif | P1'de değersiz (tek oyuncu, kuyruk sorunu değil). P2+ iyi |
| Hava Raporu | 6,0 / 4,3 / 3,2 / 2,8 | 17,7/17,5 · 14,4/18,9 · 36,1/42,7 · 50,2/54,3 | Pozitif ama küçük |

Çakışma kontrolü (aktif 19 kartla):
- Bahşiş'e benzer bir kart yok (müşteriden para gelen tek kanal bu olur).
- Taksit'e en yakın kart Acil Fren, bu yüzden özel gruba alındı.
- Serinlik ve Hava Raporu event sistemine yazan ilk kartlar.
- Sabah Vardiyası stoğa, Sıra Numaratörü sabır saatine yazan tek kart. Sabırlı Müşteriler etkileşim süresine yazıyor, farklı alan.

**Uygulama uyarıları (gameplay):**
- **Taksit:**
  - `DayCycleManager` `_graceUsed` bool'unu sayaca çevirmeli (:134, :740-750).
  - `gracePaymentPercent` SO'da duruyor. Perk yazarsa "perk kalıcı asset'i bozar" tuzağına düşer: snapshot/restore'a eklenmeli ya da runtime alanına taşınmalı.
  - Kilit muafiyeti şu an `effectId == "emergency_brake"` sabit kontrolünde (`UpgradePanel.cs:1607`). Liste haline getirilmeli.
- **Sabah Vardiyası:** Rafa kutu spawn kodu gerekiyor (küçük-orta iş). İade/raf sistemleri tanıdık bir yol sağlıyor olabilir, gameplay baksın.
- **Sıra Numaratörü:** `CustomerAI` sabır zamanlayıcısını istasyon atamasına bağlamak gerekiyor.
- **Hava Raporu:** UI butonu + server RPC + takvim senkronu (§C).
- `EconomyInvariantCheck.cs:245`'teki `long_queue expectDisabled: true` beklentisi güncellenmeli. 6 kart için fiyat satırları eklenmeli.
- Sim modeli notları:
  - Taksit, Acil Fren ile özel grupta.
  - Bahşiş için baz ödül kullanılıyor (event/perk çarpanı uygulanmıyor).
  - Serinlik yalnızca `neg` tipli event'lere uygulanıyor. Yoğun Gün "takas" olduğu için hafifletilmiyor.

---

## E) Sim ölçümü

**Varyantlar** (hepsi bellekte, `config.json` değişmedi):

| Varyant | Tanım |
|---|---|
| base | canlı: `freeDays=4` (canlı takvim eşdeğeri), eski 16 event, 6 kart kapalı |
| ev | yeni katalog + bant kuralı + 12 gün + tekrarsız, kartlar eski |
| kart | eski takvim + 6 yeni kart |
| paket | ev + kart = **önerilen paket** |
| paket_noexcl | paket, ama Taksit ile Acil Fren birlikte alınabilir |
| paket_hard / paket_soft | negatif event sapmaları ×1,5 / ×0,5 |
| *_fixed | canlı gibi gün-tohumlu sabit teklif. Diğer tüm varyantlarda teklif koşu-tohumlu rastgele |

- Hücre başına N: base/ev/kart/paket(+fixed) 5000, noexcl/hard/soft 2000. Toplam ≈2,2M koşu.
- Stratejiler: hic, acgozlu, mantikli, **acgozlu_noeb** (acgozlu ama Acil Fren draft'a hiç gelmiyor).
- Kira fonu kilidi (Ö-A, 1,0) + Acil Fren muafiyeti dahil.

### E1. Zayıf profil kazanma % (orta/iyi tüm varyantlarda ≥%99,9; tam tablo `report.md`)

| P | strateji | base_fixed | base | ev | kart | **paket** | paket_noexcl | paket_hard | paket_soft |
|---|---|---|---|---|---|---|---|---|---|
| 1 | hic | 79,6 | 79,6 | 81,8 | 79,6 | **81,8** | 82,0 | 79,0 | 83,2 |
| 1 | acgozlu | 91,1 | 81,0 | 82,6 | 85,4 | **88,2** | 94,3 | 87,9 | 89,5 |
| 1 | mantikli | 89,7 | 74,3 | 77,1 | 81,7 | **85,2** | 88,6 | 84,6 | 86,2 |
| 1 | acgozlu_noeb | 45,1 | 35,3 | 38,3 | 77,4 | **82,0** | 82,0 | 79,5 | 83,0 |
| 2 | hic | 78,6 | 78,6 | 80,8 | 78,6 | **80,8** | 80,5 | 76,5 | 84,8 |
| 2 | acgozlu | 98,8 | 93,8 | 94,7 | 95,4 | **95,9** | 97,9 | 95,4 | 97,0 |
| 2 | mantikli | 98,6 | 89,2 | 90,3 | 93,2 | **93,8** | 95,6 | 93,0 | 95,7 |
| 2 | acgozlu_noeb | 67,8 | 64,0 | 65,5 | 89,5 | **91,1** | 91,5 | 87,4 | 92,5 |
| 3 | hic | 50,3 | 50,3 | 55,3 | 50,3 | **55,3** | 55,9 | 43,4 | 61,0 |
| 3 | acgozlu | 97,6 | 90,0 | 91,2 | 90,3 | **92,3** | 96,5 | 88,0 | 93,8 |
| 3 | mantikli | 43,2 | 71,8 | 75,8 | 78,1 | **82,3** | 83,4 | 76,5 | 85,4 |
| 3 | acgozlu_noeb | 44,5 | 42,4 | 47,2 | 80,3 | **84,3** | 84,3 | 77,4 | 87,0 |
| 4 | hic | 36,6 | 36,6 | 45,7 | 36,6 | **45,7** | 45,5 | 33,9 | 47,2 |
| 4 | acgozlu | 97,1 | 87,7 | 90,0 | 87,9 | **91,0** | 94,8 | 87,7 | 92,0 |
| 4 | mantikli | 34,4 | 62,3 | 69,6 | 67,3 | **75,3** | 76,1 | 71,5 | 77,6 |
| 4 | acgozlu_noeb | 34,8 | 34,6 | 41,8 | 73,4 | **79,9** | 81,0 | 73,4 | 82,5 |

- Zayıf kayıp ortalaması (hic/acgozlu/mantikli): **base %25,4 → paket %19,4**.
- Orta/iyi: base'de 160k koşuda 95 kayıp (91'i P1 orta, 64'ü acgozlu_noeb), paket'te 13. Paket'te en düşük kazanma %99,9.

### E2. Acil Fren bağımlılığı azaldı mı?

| P | Varyant | acgozlu − mantikli (puan) | Acil Fren'siz (acgozlu_noeb) | Acil Fren alımı (acgozlu) | Taksit alımı (acg/man) |
|---|---|---|---|---|---|
| 3 | base_fixed (canlı bugün) | **+54,4** | 44,5 | 1,00 | – |
| 3 | base | +18,2 | 42,4 | 0,91 | – |
| 3 | **paket** | **+10,0** | **84,3** | 0,49 | 0,48 / 0,44 |
| 4 | base_fixed (canlı bugün) | **+62,7** | 34,8 | 1,00 | – |
| 4 | base | +25,4 | 34,6 | 0,91 | – |
| 4 | **paket** | **+15,6** | **79,9** | 0,48 | 0,49 / 0,41 |

- **Evet, azaldı.** Acil Fren hiç teklif edilmese bile zayıf P3/P4 artık %80-84 kazanıyor (base'de %35-42). Artışın kaynağı Taksit.
  - Kart varyantında da aynı etki görülüyor (80,3 / 73,4), yani katkı event'lerden değil karttan geliyor.
- Kalan acgozlu−mantikli farkı (10-16 puan) bot rezervinden geliyor. mantikli bot kiranın %60'ını ayırıyor ve can simidini geç alıyor. Bu bir kart tasarımı sorunu değil.
- **Bedeli:** acgozlu artık Acil Fren'in yerine yarı yarıya Taksit alıyor. Taksit daha zayıf bir can simidi olduğu için zayıf acgozlu P3 kazanma oranı 90,0 → 92,3'te kalıyor (base_fixed'deki 97,6 artefakt).
- `paket_noexcl` (ikisi birlikte): zayıf acgozlu P3 %96,5, P4 %94,8. Oyun kolaylaşıyor (S1).

### E3. İflas günü (zayıf, 3 strateji + noeb toplamı)

| Varyant | Gün 8 | Gün 12 | Gün 16 | Toplam kayıp |
|---|---|---|---|---|
| base | 2989 | 17622 | 5812 | %33,0 |
| ev | 1888 | 14947 | 6779 | %29,5 |
| kart | 1798 | 10731 | 5224 | %22,2 |
| **paket** | 1015 | 8384 | 5350 | **%18,4** |
| paket_hard (N=2000) | 435 | 3779 | 3082 | %22,8 |
| paket_soft (N=2000) | 427 | 3124 | 1682 | %16,4 |

- Kayıp gün 12 kapısından gün 16'ya kayıyor (gün 16'nın iflaslar içindeki payı: base %22, ev %29, paket %36). Sert final bandı (gün 13-15) bunun kaynağı. "Geç günler sert" hedefiyle tutarlı.
- Gün 4'te iflas yok, grace koruyor (değişmedi).

### E4. Orta/iyi final kasa medyanı (mantikli)

| P | Profil | base | ev | kart | paket |
|---|---|---|---|---|---|
| 1 | orta | 301 | 306 | 414 | 466 |
| 2 | orta | 1688 | 1792 | 2298 | 2566 |
| 3 | orta | 2854 | 2969 | 3536 | 3966 |
| 4 | orta | 2863 | 2941 | 3574 | 3983 |
| 3 | iyi | 10451 | 10890 | 10384 | 11711 |

- Paket orta/iyi kasasını +%10-55 artırıyor. Artışın çoğu yeni gelir kartlarından (Bahşiş, Sabah Vardiyası, Sıra Numaratörü). Event'lerden gelen ~+%4.
- Enflasyon riski düşük: kasa zaten hiçbir kararı bağlamıyor (kaybedilemezlik). Ama "para anlamsız" hissini artırıyor. Karar 3 ile birlikte düşünülmeli.

### E5. Yeni kartların alım oranı (paket, koşuların %'si; `report.md` tam tablo)

| Profil, P3 | Bahşiş | Taksit | Serinlik | Sabah V. | Sıra N. | Hava R. |
|---|---|---|---|---|---|---|
| zayıf acgozlu / mantikli | 11 / 16 | 48 / 44 | 14 / 12 | 11 / 14 | 14 / 14 | 8 / 6 |
| orta acgozlu / mantikli | 77 / 80 | 50 / 50 | 79 / 77 | 78 / 79 | 79 / 78 | 71 / 69 |

- Zayıf P2+ takım kilit yüzünden (kasa − fiyat ≥ sıradaki kira) kilitten muaf olmayan kartlara nadiren erişiyor. Bu Ö-A'nın tasarım amacı. Zayıf takımın gerçek seçeneği can simitleri.
- Taksit'in %50'de takılması özel gruptan geliyor: hangisi önce teklif edilirse o alınıyor.

### E6. Her-gün event'in tek başına etkisi

- ev − base: zayıf ortalama kayıp (3 strateji) %25,4 → %22,1, yani −3,3 puan. hic P4'te +9 puan kazanma (pozitif event'ler ağırlıkta, 7P/5N).
- Hassasiyet (hard/soft ±%50 negatif şiddet) → zayıf kayıp %16,4-22,8. **Katalog değerleri ekonomiyi kırmıyor.**

### Varsayımlar ve sınırlar
- **Profil varsayımı:** Mc40k'da zayıf profil süreleri ±%20 oynayınca kayıp ±40 puan değişiyordu. Buradaki mutlak yüzdeler playtest ile kalibre edilmeden kesin değil, **farklar** daha güvenilir.
- **Bot sadeleştirmeleri:**
  - Bahşiş'i baz alan ve Hava Raporu'nu otomatik kullanan bot sadeleştirilmiş.
  - Günün rengine öncelik veriyor.
  - Acele Primi için davranış değiştirmiyor. Gerçek oyuncu primi daha çok alır.
- **Tek Renk Günü:** yanlış teslim ×0,3, yanlış ürün ×0,5 varsayımı.
- **Modellenmeyenler:**
  - Stamina/sprint modellenmiyor.
  - Hava Raporu UI maliyeti modellenmiyor.
  - Sabah Vardiyası raf kapasitesi sabit 8 alındı. Sim rafı modellemiyor.
- **Teklif artefaktı:** Canlı teklif sabit (gün-tohumlu). `paket_fixed` rastgele teklifle ±1-9 puan içinde, ama hangi kartın alındığı teklif takvimine bağlı (sabit teklifle Taksit P2-P4'te %100, P1'de %3). Karar için rastgele teklif sonuçları kullanıldı.
