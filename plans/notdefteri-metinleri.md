# 📓 Cargor — Oyun İçi Not Defteri Metinleri

> **Not (geliştirici için, oyun içinde görünmez):** Aşağıdaki her `## Sayfa N` başlığı, oyun içi not defterinde tek bir sayfaya karşılık gelir. İçerik gerçek oyun mekaniklerine göre yazıldı (kutu renkleri, paketleme masası, raf sistemi, tır teslimatı, müşteri sabır sistemi, iade/2-item/karışık-tır özellikleri, kira/dinlenme odası akışı — kaynak: `GDD.md` + ilgili C# scriptleri). Sayfa sayısı örnekti, istersen birleştir/böl. Doğrudan string table'a veya notebook UI'ına kopyalanabilir.

---

## Sayfa 1 — Kapak / İçindekiler

**CARGOR**
*Depo El Kitabı*

| Sayfa | Başlık |
|---|---|
| 2 | Cargor'a Hoş Geldin! |
| 3 | Temel Hareketler |
| 4 | Ürünler Nasıl Paketlenir |
| 5 | Rafa Yerleştirme |
| 6 | Tırlar ve Teslimat |
| 7 | Müşteriler |
| 8 | Özel Talepler: İadeler, Çift Sipariş, Karışık Tırlar |
| 9 | Kira ve Paran |
| 10 | Yükseltmeler |
| 11 | Görev Panosu |
| 12 | Telefon Hattı |
| 13 | Dinlenme Odası ve Gün Sonu |
| 14 | Ekip Çalışması İpuçları |

---

## Sayfa 2 — Cargor'a Hoş Geldin!

Hoş geldin! Artık Cargor ailesinin bir parçasısın.

Burası bir depo, bir mağaza ve biraz da kaos — hepsi bir arada. Müşteriler sana ne istediklerini söyler, sen onu paketler, doğru tıra yüklersin. Basit görünüyor, değil mi? On altı gün sonra hâlâ ayaktaysan, gerçekten iyi iş çıkarmışsın demektir.

Elindeki bu defter, işin inceliklerini unuttuğunda başvuracağın yer. Her sayfa farklı bir konuyu anlatıyor. Aceleye gerek yok — ilk günlerde yavaş çalış, sistemi öğren. Hızın zamanla gelecek.

**Unutma:** Burada yalnız değilsin. Cargor bir takım işi — yükünü paylaş, sesini duyur.

---

## Sayfa 3 — Temel Hareketler

- **Yürü / Koş** — depoda dolaşmanı sağlar.
- **Al / Bırak** — yerdeki, masadaki veya raftaki bir eşyayı elinle al ya da bıraktığın yere koy.
- **Fırlat** — elindekini uzağa at. Hızlı ama riskli: sert çarparsa kırılır, para ve itibar kaybedersin. Bir takım arkadaşına atıp yakalatırsan ceza yok — üstelik zaman kazandırır.
- **Etkileşim** — müşterilerle konuşmak, telefonu açmak, raftan/masadan eşya almak hep bu tuşla olur.
- **Harita / Stok Kontrolü** — deponun genel görünümünü açar; hangi eşya nerede duruyor görebilirsin. Takım arkadaşlarının konumunu göstermez — onu telsizden sorman gerekecek.

> 💡 **İpucu:** Fırlatıp yakalama, iki kişilik bir istasyonda zaman kazandırır ama alışkanlık hâline getirme — her düşürme cebinden gider.

---

## Sayfa 4 — Ürünler Nasıl Paketlenir

Bir müşteriyle konuştuğunda, senden bir ürün ister ve onu kendi masasına bırakır. İşin orada bitmiyor — o ürün henüz **paketlenmemiş**.

Paketleme adımları:

1. Müşterinin bıraktığı **çıplak ürünü** al.
2. En yakın **paketleme masasına** git.
3. Rafdan ürünle **aynı renkte boş bir kutu** al.
4. Masada ürünü ve kutuyu bir araya getir — paketleme anında gerçekleşir, bekleme yok.
5. Elinde artık **paketlenmiş bir kutu** var. Şimdi sırada teslimat var (bkz. Sayfa 6).

> ⚠️ **Dikkat:** Kutu rengi ürünle uyuşmuyorsa paketleme başarısız olur ve kutu elinde kırılır. Renk kontrolünü paketlemeden önce yap, kutunu boşa harcama.

---

## Sayfa 5 — Rafa Yerleştirme

Raflar Cargor'un can damarı. Kırmızı, sarı ve mavi olmak üzere üç boş kutu türü hep orada bekler — birini aldığında raf kendini kısa süre içinde tazeler, yani asla tamamen boş kalmaz.

Elinde fazladan bir kutu ya da paketlenmiş bir ürün varsa ve o an taşımak istemiyorsan, rafa geri koyabilirsin. Düzenli bir raf, hem senin hem takımının işini kolaylaştırır — herkes ne nerede bulacağını bilir.

> 💡 **İpucu:** Yoğun saatlerde (öğle ve akşam koşuşturması) raflara uğrayıp stok kontrolü yapmak, sırada bekleyen bir sonraki müşteri için saniyeler kazandırır.

---

## Sayfa 6 — Tırlar ve Teslimat

Hangarda bekleyen tırlar, gövde rengiyle sana ne istediklerini gösterir: kırmızı tır kırmızı kutu ister, sarı tır sarı, mavi tır mavi.

- **Doğru renk teslim et** → para kazanırsın, itibarın artar.
- **Yanlış renk teslim et** → para kaybedersin. Renklere iki kere bak.
- Her tırın sınırlı bir bekleme süresi ve kapasitesi var. Dolduğunda ya da süresi bittiğinde tır ayrılır, yenisi gelir.
- Depo büyüdükçe (yükseltmelerle) birden fazla hangar açılabilir — aynı anda birkaç tırı besleyebilirsin.

> 💡 **İpucu:** Tır rengini uzaktan görebiliyorsan, elindeki kutuyla eşleşen tıra öncelik ver — boşuna yürüme.

---

## Sayfa 7 — Müşteriler

Müşteriler kapıdan girer, sıraya girer ve senden bir şey ister. Üzerlerindeki bar, ne kadar sabırlı olduklarını gösterir — bar biterse müşteri öfkeyle ayrılır, sen de para ve itibar kaybedersin.

- Gün içinde yoğunluk değişir: öğle ve akşam saatleri en kalabalık zamanlar, öğleden sonra biraz nefes alırsın.
- Bir müşteriyle konuştuğunda (etkileşim tuşu) istediği ürünü masasına bırakır — bundan sonrası paketleme ve teslimattır (Sayfa 4-6).
- Zamanında hizmet ettiğin her müşteri hem para hem itibar kazandırır. İtibarın yükseldikçe teslimat başına kazancın da artar.

> ⚠️ **Dikkat:** Sıra doluyken yeni müşteri gelmez ama sıradakiler sabırsızlanmaya devam eder. Kuyruğu eritmeden yeni işe başlama.

---

## Sayfa 8 — Özel Talepler: İadeler, Çift Sipariş, Karışık Tırlar

Günler ilerledikçe iş biraz karışıyor. Üç yeni durumla karşılaşacaksın:

- **İadeler:** Bazı müşteriler ürün istemez, tam tersini yapar — sana bir kutu **geri getirmeni** ister. Üzerlerinde beliren renkli küre, hangi renk kutu istediklerini gösterir. Rafdan doğru rengi al, müşteriye götür.
- **Çift sipariş:** Bazı müşteriler tek seferde iki farklı renk ister. İkisini de hazırlamadan iş bitmez.
- **Karışık tır:** Bazı tırlar tek renk değil, iki farklı renk kutu birden ister. Gövdesindeki karışık renk (örneğin kırmızı + sarı = turuncu) sana ipucu verir — hangi iki rengi yüklemen gerektiğini gösterir.

> 💡 **İpucu:** Karışık tır ya da çift sipariş gördüğünde panik yapma — sadece iki ayrı teslimat gibi düşün, art arda hallet.

---

## Sayfa 9 — Kira ve Paran

Cargor'u işletmek ücretsiz değil. Belirli günlerde kira ödemesi gelir — bu güne "kira günü" diyoruz.

- Kirayı ödeyecek paran varsa, ödenir ve devam edersin.
- Ödeyemezsen bir kerelik bir şans var: elindeki paranın büyük bir kısmı alınır ama işletme kapanmaz.
- Bu şansı bir kere kullandıktan sonra tekrar ödeyemezsen, iş burada biter.

Para; doğru teslimatlardan, telefon aramalarından ve tamamlanan görevlerden gelir. Yanlış teslimat, düşürülen kutular ve yarım bırakılan görevler paranı azaltır.

> ⚠️ **Dikkat:** Kira günleri öncesinde elindeki nakdi kontrol et. Son ana bırakma.

---

## Sayfa 10 — Yükseltmeler

Belirli günlerde önüne birkaç yükseltme seçeneği gelir — hepsini alamazsın, aralarından seçim yaparsın. Ek raf, ek hangar, daha hızlı hareket, daha uzun sabır süresi gibi kalıcı iyileştirmeler kazanabilirsin.

Seçimini iyi düşün: bazı yükseltmeler birbirini dışlar, bazıları belli bir güne kadar açılmaz. Erken günlerde temel işleyişi hızlandıran seçimler, ileride işine çok yarar.

> 💡 **İpucu:** Takımınla konuşmadan seçim yapma — bir kişinin aldığı yükseltme, herkesin işleyişini değiştirir.

---

## Sayfa 11 — Görev Panosu

Her gün panoda birkaç görev teklifi görürsün — rafa kutu koymak, ürün paketlemek, tır tamamlamak, telefona cevap vermek gibi. Bir günde yalnız **bir tanesini** kabul edebilirsin.

- Kabul ettiğin görevi gün bitmeden tamamlarsan, gün sonunda ödülün otomatik hesabına yatar.
- Kabul edip yarım bırakırsan, gün sonunda ceza uygulanır.

Kabul etmezsen hiçbir şey olmaz — ama fırsatı da kaçırırsın. Elindeki işe göre gerçekçi bir görev seç.

> 💡 **İpucu:** Zor bir görevi son güne kadar erteleme. Gün sonu her zaman beklediğinden çabuk gelir.

---

## Sayfa 12 — Telefon Hattı

Depodaki telefon zaman zaman çalar. Yanına gidip açarsan küçük bir para ve itibar ödülü kazanırsın. Açmazsan da bir şey kaybetmezsin — telefon kendi kendine susar.

Çalması tamamen şansa bağlı, ama bazı günler (örneğin özel etkinlik günlerinde) çalma ihtimali artar. Yanından geçerken bir bakış atmak zarar vermez.

> 💡 **İpucu:** Telefon çalarken elin doluysa bile yoluna çıkıyorsa uğra — kısa bir mola, küçük bir kazanç.

---

## Sayfa 13 — Dinlenme Odası ve Gün Sonu

Gün sona erdiğinde herkesin **dinlenme odasına** gitmesi gerekir. Tek bir kişi bile dışarıda kalsa, gün kapanmaz — takım tam olarak toplanana kadar beklenir.

Herkes odaya girdiğinde gün özeti gösterilir ve yeni gün başlar. On altıncı günün sonuna kadar iflas etmeden gelebilirsen, kutlamayı hak ettin demektir.

> 💡 **İpucu:** Gün sonuna yaklaşırken elindeki işi bitirip erkenden dinlenme odasına yönel — takımını bekletme.

---

## Sayfa 14 — Ekip Çalışması İpuçları

Cargor tek başına da oynanır ama gerçek eğlence takımla başlar. Birkaç pratik öneri:

- **İşi bölüşün:** Biri paketleme masasında dursun, biri tırlara koşsun, biri müşterilerle ilgilensin.
- **Konuşun:** Aynı odada değilseniz birbirinizi göremeyebilirsiniz — nerede ne olduğunu haritadan kontrol edip sesli olarak paylaşın.
- **Fırlatıp yakalayın:** İki istasyon arasında mekik dokumak yerine, kutuyu doğrudan takım arkadaşına fırlatın.
- **Yükseltmelerde uzlaşın:** Kalıcı seçimler herkesi etkiler, tek başına karar vermeyin.

İyi çalışmalar — Cargor'a hoş geldin!
