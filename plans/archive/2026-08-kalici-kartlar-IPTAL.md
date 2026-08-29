# Kalıcı Kart Sistemi (Kira Sonrası Draft) — ❌ İPTAL

> **🚫 DURUM: 2026-08-13'te kullanıcı bu sistemden VAZGEÇTİ. Hiç kod yazılmadı, yazılmayacak.**
> Dosya referans olarak arşivde tutuluyor (14 kartın onaylı değerleri + economist gerekçeleri + §4.3'teki mutlak-atama mimari bulgusu ileride başka bir sistemde işe yarayabilir). Aktif iş listesinde DEĞİL.
>
> **Not:** §4.3'teki `PerkEffect.cs` mutlak-atama bulgusu kartlardan bağımsız olarak geçerlidir — perk sistemine dokunan her yeni iş için hâlâ okunmaya değer.

> **Eski durum (iptalden önce):** içerik/mekanik tasarımı kullanıcı tarafından 14 karta indirilip onaylandı, **economist değer turu BİTTİ** (4 kart düzeltildi, 9 kart baseline'a uygun bulundu, 1 kritik mimari uyarı çıktı — bkz. §4). Kod yazılmadı.
> Bu dosya tek başına yeterlidir — kart listesi + mekanik kurallar burada, GDD.md referansları aşağıda.

---

## 1. Mekanik (kullanıcı tarafından belirlendi, sabit)

- **Tetiklenme**: Kira günü (4/8/12) biter, para düşülür → **ertesi gün başında** (5/9/13) 2 kart sunulur. Gün 16'da kira olsa da oyun bittiği için teklif yok — 4 kira gününün 3'ünde çalışır.
- **Duraklama**: ESC menüsü gibi tam ekran modal, dünya donar (`timeScale=0` — `EscapeMenuManager` deseni).
- **Oylama**: Tüm bağlı oyuncular **oybirliği** ile aynı kartı seçmeli. **30 sn sayaç.** Süre dolar ve oybirliği yoksa → 2 karttan biri **%50/50 rastgele** seçilir.
- **Tekrar yok**: Gösterilen kart (seçilse de seçilmese de) o run'da bir daha çıkmaz.
- **Kalıcılık**: Seçilen kartın etkisi anında uygulanır ve **oyunun sonuna kadar kalır** (event sisteminin aksine gün başında geri alınmaz — event'ler ayrı, tek günlük buff/nerf sistemi, karıştırılmayacak).
- **Havuz yapısı**: Kartlar 3 zorluk kademesine ayrılır, her kademe kendi gün sunumunda kullanılır. Kademe = kötü yönün şiddeti (erken oyunda düşük risk, geç oyunda yüksek risk/ödül).
- **Ayrı sistem**: `UpgradePanel`/`DraftPool` (perk draft) sisteminden **tamamen bağımsız** bir mekanik — aynı UI/tetikleyici değil, farklı amaç (perk = satın alınan yükseltme, kart = kira sonrası bedava/zorunlu seçim).

> ⚠️ **İmplementasyon uyarısı (ileride, kod yazılırken hatırlanmalı):** `PerkEffect`'in `GameEconomySettings`/prefab alanlarına runtime'da yazıp geri almama hatası (`plans/devam.md` 2026-08-07, snapshot+restore ile kapatıldı) bu sistemde TEKRARLANMAMALI — kart efektleri de kalıcı SO/prefab alanlarına yazacaksa aynı snapshot+restore veya güvenli bir uygulama deseni şart.
> 🔴 **KRİTİK — mutlak atama çakışması (economist turu bulgusu, §4.3):** `PerkEffect.cs` tüm perkleri idempotent **mutlak atama** ile yazıyor (`+=`/`*=` YASAK — `NetworkList.OnListChanged` her upgrade değişiminde tüm client'larda yeniden tetiklendiği için). Yani kartlar da aynı alanlara mutlak atama yaparsa **perk + kart aynı alanı hedeflediğinde birikmiyor, hangisi SON çalışırsa o kazanıyor, diğeri sessizce silinir.** Çakışan çiftler: #2/#19 ↔ `agile_crew`/`energetic_crew` (moveSpeed/staminaRegenRate) · #5/#9 ↔ `prestige_master` · #1/#6/#11/#15/#20 ↔ `gambler_case`/`all_in` · #13 ↔ `phone_line` · #11 ↔ gün-süresi perki. Kod yazılırken kartlar perk alanlarına DOĞRUDAN mutlak atama yapmamalı — ayrı bir katmanda tutulup (kart etkisi + perk etkisi) birleştirilerek uygulanmalı. Gameplay departmanına build aşamasında aktarılacak.

---

## 2. Onaylı 14 Kart — FİNAL DEĞERLER (economist turu sonrası)

Kullanıcı 20 kartlık ilk taslaktan kira-azaltan 2 kartı (Nakit Akışı Danışmanlığı, Gece Nöbeti) sildirdi, kalan havuzdan 14'ünü onayladı. Economist `GameEconomySettings` baseline'ına ve `tools/economy-sim/sim.js`'e karşı doğruladı; **4 kartın değeri düzeltildi** (aşağıda **kalın** işaretli), 9 kart olduğu gibi onaylandı. Detaylı gerekçe §4'te.

### 🟢 Hafif — Gün 5 havuzu (6 kart)

| # | Kart | İyi Yön | Kötü Yön |
|---|------|---------|----------|
| 1 | Toptan Anlaşma | Doğru teslimat ödülü +%10 | Yanlış teslimat cezası +%10 |
| 2 | Sadık Ekip | Stamina yenilenme hızı +%15 | Hareket hızı -%5 |
| 4 | Erken Kalkanlar | Günün ilk yarısı müşteri sabrı +%20 | İkinci yarısı sabır -%10 |
| 5 | Kişisel Dokunuş | Telefon arama ödülü +%50 (20→30 TL) | Servis başı prestij kazancı -%10 |
| 6 | Basit Muhasebe | Kutu düşürme cezası kaldırılır (5→0 TL) | **Doğru teslimat ödülü -%2** *(economist: taslak -%5 lopsided'dı, 11 gün üzerinden 229-762 TL'ye mal oluyordu, kaldırılan cezanın 50-150 TL'lik kazancıyla orantısız)* |
| 7 | İkinci El Ekipman | Upgrade satın alma maliyeti -%10 | Yanlış ürün gösterme prestij cezası +%20 |

### 🟡 Orta — Gün 9 havuzu (4 kart)

| # | Kart | İyi Yön | Kötü Yön |
|---|------|---------|----------|
| 9 | Sıkı Sözleşme | Servis prestij kazancı +%25 | Müşteri kaybı/hatalı teslimat prestij cezası +%25 |
| 11 | Fazla Mesai | Gün süresi +%10 uzar | **Doğru teslimat ödülü -%6** *(economist: gün+%10 kapasite-sınırsız olduğu için kutu/gün TAM +%10 veriyor — taslaktaki -%15 ile net çarpan 1.10×0.85=0.935, yani "iyi yön" aslında garanti %6.5 kayıptı. -%6'ya çekilince net 1.034, vaatle tutarlı hafif pozitif)* |
| 13 | Meşgul Hat | Telefon çalma ihtimali +%10 | Kaçan müşteri prestij cezası +%20 |
| 14 | Hızlı Sevkiyat Sözleşmesi | Tır kargo kapasitesi (max) +1 | Hangar bekleme süresi +%15 |

### 🔴 Ağır — Gün 13 havuzu (4 kart)

| # | Kart | İyi Yön | Kötü Yön |
|---|------|---------|----------|
| 15 | Riskli Yatırım | Kutu başı ödül kalıcı +%30 | Kalan son kira dönemi (gün 16) %25 pahalı — **economist onayladı, aşırı değil** (bkz §4.2) |
| 16 | Tükenmişlik Eşiği | **Maksimum stamina +%40** *(economist: taslak +%25, 3sn tabanda yalnız +0.75sn — zar zor hissedilir; kötü yön güçlü kaldığı için asimetrikti)* | Stamina bitince hareket cezası %20 daha sert |
| 19 | Usta İşçilik | Hareket hızı kalıcı +%15 | Stamina yenilenme hızı -%20 |
| 20 | Şöhretin Bedeli | **Kutu başı ödül +%15** *(günlük talep+%20 kaldırıldı — economist: gün13-16'da servis kapasitesi zaten talebin altında bağlayıcı kısıt, talep artışı `served` sayısını hiç değiştirmiyor, kartın kendi cezasını karşılıksız besliyordu)* | **Müşteri kaybı prestij cezası +%25** *(50'den düşürüldü — telafi eden talep artışı gerçek etkisiz olduğu için ceza da orantılı küçüldü)* |

### Reddedilenler (referans için, tekrar gündeme gelebilir)

3 Uzman Kurye · 8 Ekspres Hat · 10 Kumarbaz Sözleşmesi · 12 Sadakat Programı · 17 Yoğun Trafik · 18 Sınırsız Kredi · ~~Nakit Akışı Danışmanlığı~~ (kira azaltıyordu) · ~~Gece Nöbeti~~ (kira azaltıyordu)

---

## 3. Bağlı sistemler / bilinen alanlar (economist ve ileride engineering için)

GDD.md §4 (Ekonomi), §5 (Kira), §6 (Prestij) — `GameEconomySettings`: `rewardPerBox`(50) · `penaltyPerBox`(40) · `boxDropMoneyPenalty`(5) · `customerServedPrestigeBonus`(0.4) · `wrongProductPrestigePenalty`(-0.08) · `wrongDeliveryPrestigePenalty`(-0.16) · `customerLostPrestigePenalty`(-0.4) · `callMoneyReward`(20) · `phoneRingChanceByPlayerCount`([0.2,0.25,0.3,0.35]) · `prestigePerBonus`(8) · `hangarStayDurationByPlayerCount`([120,60,40,30]) · `truckCargoMaxExclusiveByPlayerCount`([3,4,5,6]) · `rentGrowthMultiplier`(1.35, **bilerek dik, geç oyun parayı eritsin diye** — #15'in +%25 kira sürşarjı bunun üstüne biniyor, kontrol edilmeli). `DayCycleManager` gün süresi (160s +10s/gün). `PlayerMovement.moveSpeed`/`staminaRegenRate`.

#14'teki "tır kargo kapasitesi" gibi bazı efektler için birebir isimli bir alan GDD'de yok — TruckSpawner'ın ilgili mantığına bağlanacak, kesin alan adı engineering aşamasında netleşir. (#20'deki talep artışı economist turunda kaldırıldı, bkz §4.4.)

---

## 4. Economist Değer Turu — Sonuç (2026-08-12)

Kaynak: `GameEconomySettings.cs` (kod-doğrulanmış baseline) + `tools/economy-sim/sim.js` (P1-P4, optimistic+strict senaryolar). Tam hesap detayları `.claude/agent-memory/economist/permanent_cards_value_review_2026-08-12.md` ve `.claude/agent-memory/economist/perk_card_absolute_assignment_conflict.md`.

### 4.1 Değeri OLDUĞU GİBİ onaylanan 9 kart
#1, #2, #4, #5, #7, #9, #14, #15, #19 — baseline'a göre makul ölçekte. #7'nin -%10 upgrade indirimi özellikle doğrulandı: `UpgradePanel.GetCostMultiplier()` zaten çarpımsal (event×P), 3. katman olarak eklenmesi güvenli, perk çakışması yok.

### 4.2 #15 Riskli Yatırım — kullanıcının özellikle sorduğu nokta, AŞIRI DEĞİL
Gün 13'te seçilirse kalan TEK kira ödemesi gün 16'dır (rentIntervalDays=4, oyun gün 16'da bitiyor) — %25 sürşarj `rentGrowthMultiplier`in compounding'iyle çarpışmıyor, tek seferlik yük. sim.js P1-P4/optimistic+strict test edildi: en dar marjda bile (P2-strict) sürşarjlı kira ödendikten sonra kasada 1711 TL kalıyor. **Kart olduğu gibi kalıyor.**

### 4.3 Mimari uyarı — mutlak atama çakışması (kritik, §1'e taşındı)
`PerkEffect.cs` tüm perkleri idempotent mutlak atama ile yazıyor (`+=`/`*=` yasak). Kartlar aynı deseni izlerse perk+kart aynı alanı hedeflediğinde **birikmez, son çalışan kazanır, diğeri sessizce silinir**. Etkilenen alan/kart/perk çiftleri §1'de listeli. Bu bir DEĞER sorunu değil, build aşamasında gameplay departmanının çözmesi gereken bir uygulama deseni sorunu.

### 4.4 Tier atamaları
Hafif/Orta/Ağır sıralaması doğru bulundu — #11'deki tek sorun matematikti (yukarıda düzeltildi), kademe değişmedi. **Not:** Hafif kartlar (gün 5, ~11 gün kalan pencere) en uzun etki süresine sahip — küçük yüzdeler bile büyük mutlak TL'ye dönüşüyor, ileride yeni Hafif kart eklenirken bu unutulmamalı.
