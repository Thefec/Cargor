# 📏 Play-Test Ölçüm Protokolü

> **Amaç**: oyunu "iyi hissettirdi / hissettirmedi" ile değil, **sim'i kalibre edecek sayılarla**
> bırakmak. Bu oturumun tek zorunlu çıktısı **kutu/dakika/oyuncu** değeri.
>
> Bu dosya `plans/playtest-2026-07-19.md`'nin yerini almaz — o bir **regresyon** listesi
> (ve ekonomi kısmı bayat: `maxPrestige 240` yazıyor, o 100'e çekileli çok oldu).
> Bu dosya bir **ölçüm** protokolü.

---

## 0. Oturum öncesi (2 dakika)

- [ ] Unity'de menü → **`Cargor / Ekonomi Değerlerini Doğrula`** → yeşil olmalı (179 kontrol)
- [ ] `git status` temiz olmalı — kirliyse önce commit'le, yoksa oyunun asset'leri bozup bozmadığını ayırt edemeyiz
- [ ] Kâğıt/not defteri hazır. **Ekran kaydı alabiliyorsan al** — sonradan saymak çok daha kolay

> Neden önemli: `PerkEffect` perk satın alınınca ekonomi asset'lerini kalıcı olarak değiştiriyor
> (bkz. `plans/devam.md` 2026-08-07). Oturum öncesi/sonrası karşılaştırma yapamazsak hangi değerin
> senin ayarın hangisinin bug olduğunu ayıramayız.

---

## 1. ZORUNLU ÖLÇÜM — kutu/dakika/oyuncu

**Bu tek sayı tüm ekonomi modelinin en duyarlı girdisi.** 1.2 ile 2.0 arasındaki fark, 1 oyunculu
kümülatif geliri **%117** değiştiriyor. Şu an elimizde ölçüm değil **tahmin** var.

### Nasıl ölçülür

**Gün 2'yi seç** (gün 1 öğretici sayılır, gün 3+ upgrade'ler devreye girer).

1. Gün başladığında saati not et
2. Normal oyna — **acele etme, ama boş da durma.** "Ciddi oynayan bir oyuncu" temposu
3. Gün bitince not et: **kaç kutu tıra girdi** ve **gün kaç saniye sürdü**

```
Gün 2 · oyuncu sayısı: ___
Tıra giren kutu     : ___
Gün süresi (sn)     : ___   (varsayılan 200)

kutu/dk/oyuncu = kutu ÷ (süre ÷ 60) ÷ oyuncuSayısı = ______
```

> Sayamazsan yaklaşık ver — **200 saniyede kaç tır tam doldu** da yeterli bir tahmin verir
> (1P'de tır 1–2 kutu, 4P'de 2–5 kutu alıyor).

### Aynı anda not et

| Ölçüm | Nasıl | Değer |
|---|---|---|
| **Masa meşguliyeti** | Paketleme masası önünde beklediğin oldu mu? Kaç kez? | ___ |
| **Boşta kalma** | "Yapacak iş yok" hissettiğin oldu mu? | Evet / Hayır |
| **Tır bekleme** | Kutu hazır ama tır yok — oldu mu? | Evet / Hayır |

Bunlar 2. ve 3. en duyarlı girdiler. Masa darboğazsa `Paketleme İstasyonu` upgrade'inin değeri
4 katına çıkıyor; tır darboğaz değilse (ki ölçüm öyle diyor) `Ek Hangar` gerçekten değersiz demektir.

---

## 2. Ekonominin şekli — hangi gün ne hissettin

Her kira gününde (4, 8, 12, 16) **kirayı ödedikten sonraki** paranı not et:

| Gün | Kira (1P beklenen) | Ödedikten sonra kalan | Hissiyat |
|-----|---------------------|------------------------|----------|
| 4   | 500                 | ___                    | rahat / gergin / ödeyemedim |
| 8   | 675                 | ___                    | |
| 12  | 911                 | ___                    | |
| 16  | 1.230               | ___                    | |

> 2P için 1.000 / 1.350 / 1.823 / 2.460 · 3P için 1.450 / 1.958 / 2.643 / 3.568 ·
> 4P için 1.800 / 2.430 / 3.281 / 4.429

**Aradığımız cevap**: kira eğimi (`1.35`) doğru mu? Son kira gerçek bir tehdit mi, yoksa
birikmiş parayla kolayca mı ödeniyor? Ya da tam tersi — 8. günde duvara mı çarpıyorsun?

---

## 3. Bu turda değişen şeyler — gerçekten çalışıyor mu

FAZ 4'te değişip **hiç oynanmamış** olanlar. Sadece "çalışıyor mu" bak, denge yorumu yapma:

- [ ] **Telefon 15 saniye çalıyor** (25 değil) ve açınca **+20 TL** geliyor
- [ ] **İki servis masası** aynı anda müşteri alıyor — ikisi de kullanılıyor mu, yoksa biri ölü mü?
- [ ] **Tır kargosu** oyuncu sayına göre değişiyor (1P'de 1-2 kutu, 4P'de 2-5)
- [ ] **Görev ödülü gün sonunda otomatik** yatıyor — "Topla" butonu aramıyorsun
- [ ] **Upgrade fiyatları** çok oyunculuda belirgin pahalı (2P'de tam 2×)
- [ ] **Event açıklamaları** ekranda gördüğün etkiyle uyuşuyor

### Özellikle bak: gün 16 zaferi

- [ ] 16. günü bitirince **zafer ekranı** geliyor ve **kayıp ekranına dönüşmüyor**

> Bu bir bug'dı ve düzeltildi (`f013f5d`) ama hiç oyun içinde test edilmedi. Zorlamak istersen:
> son güne **düşük prestijle** gir ve **bir Hard görev kabul edip tamamlama.**
> Ceza 2.66 prestij — eskiden bu, kazandığın oyunu kaybettiriyordu.

---

## 4. Oturum sonrası (ZORUNLU — 1 dakika)

- [ ] Unity'de menü → **`Cargor / Ekonomi Değerlerini Doğrula`**

**🔴 Kırmızı yanarsa panik yok, beklenen olabilir.** Perk satın aldıysan `PerkEffect` ekonomi
asset'lerini kalıcı değiştirmiştir. Rapor hangi alanın bozulduğunu ve hangi perkin yaptığını yazar.

Temizlemek için:
```bash
git checkout -- Assets/Resources/EkonomiAyarlari.asset
git checkout -- "Assets/NewCss/TruckScripts/Truck_Anim (2).prefab"
```

- [ ] `git status` — `Assets/` altında beklemediğin bir değişiklik var mı? Varsa **bana söyle**,
      hangi perkin neyi bozduğunu ayıklayalım

---

## 5. Bana ne getirmen yeterli

Her şeyi doldurman gerekmiyor. Şu üçü elimde olursa sim'i kalibre edip tüm tabloyu tek katsayıyla
kaydırabilirim:

1. **kutu/dakika/oyuncu** (§1) — tek zorunlu sayı
2. **Hangi kira gününde zorlandın** (§2)
3. **Masa mı tır mı seni bekletti** (§1 alt tablo)

Geri kalanı serbest yorum: neyi sıkıcı buldun, ne eğlenceliydi, hangi upgrade'i almaya değer
buldun. Bunlar `plans/2026-08-06-paketleme-derinlik.html`'deki 63 fikri önceliklendirmek için.

---

## Ek: bilinen sorunlar (bunları bug sanma)

| Gözlem | Durum |
|---|---|
| `gambler_case`, `all_in`, `prestige_broker`, `fast_hangar` alınca hiçbir şey değişmiyor gibi | **Bilinen** — bu perkler muhtemelen hiç çalışmıyor (`Truck.Awake` değeri eziyor). Karar bekliyor. |
| `agile_crew` hız vermiyor gibi | **Bilinen** — aynı sebep, `PlayerMovement` prefab referansı |
| Görev kartında ilerleme göstergesi yok | **Bilinen** — `progressText` senin talebinle kaldırıldı (2026-07-29) |
| Takvimde 1. gün dışında event yazısı çıkmıyor | **Bilinen** — `eventTexts` dizisinde yalnız index 0 atanmış, 15 slot sende |
| Kira günü görev ödülünden ÖNCE kesiliyor | **Bilinen tasarım yan etkisi** — collect kaldırılınca oluştu |
