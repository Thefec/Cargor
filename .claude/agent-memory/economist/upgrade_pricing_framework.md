---
name: upgrade-pricing-framework
description: Cargor upgrade fiyatlandırma çerçevesi — payback-süresi bazlı, upgrade karakterine göre farklı hedef payback bandı (1.4-3.2 gün); v2'de gerçek maxLevel sayılarına göre revize edildi
metadata:
  type: project
---

**v2 güncellemesi (2026-07-08):** v1 (2026-07-07) raporu TAHMİNİ maxLevel sayılarıyla yazılmıştı. Kullanıcı Unity'den gerçek envanteri verdi, sayılar farklı çıktı: Storage=10(aynı), Table=**2**(v1:4), Queue=**4**(v1:3), Money=**3**(v1:5), Stamina=**3**(v1:5), Truck=**2**(v1:3), Quest Tier=**2**(v1:3, ayrıca **sistem artık PASİF, EV≈0**), + iki yeni tip: **Water**(maxLevel 1, salt achievement, ekonomik değeri yok, sembolik fiyat) ve **Customer/patience**(maxLevel 2, "talebi yakalayan" kategori — kuyruk/masa gibi). Rapor `UPGRADE_PRICING_REPORT.md` v2 olarak güncellendi. **Önemli ders:** Fiyat/değer tabloları oluştururken Unity'deki GERÇEK maxLevel'i önceden doğrulamadan varsayımla ilerleme — seviye sayısı yanlışsa tüm eğri (Masa örneğinde fiyat +150%'ye kadar) yeniden hesaplanmak zorunda kalıyor.

`UPGRADE_PRICING_REPORT.md` için kurulan fiyatlandırma çerçevesi (2026-07-07, v2: 2026-07-08), Faz 1 sabitleri üzerine (startingMoney=500, rentGrowthMultiplier=1.15, wealthTaxRate=0.10, rewardPerBox=50→75, penaltyPerBox=40):

Upgrade'ler 6 karaktere ayrıldı, her birine farklı hedef payback süresi (gün) atandı:
1. Talep yaratan (Raf/MoreCapacity) — payback 1.8 gün, değer = +3 ham müşteri/gün × %75 capture rate × rewardAt(prestij).
2. Talebi yakalayan (Masa/TableSlots) — payback 2.0 gün, değer = talep × capture rate delta (+%8/seviye) × reward.
3. Darboğaz kaldıran (Ek Hangar Kapısı) — payback 2.3 gün, değer sadece geç oyunda yüksek (erken oyunda israf), fiyat sıçramalı (300→700→1250).
4. Doğrudan çarpan (Para/rewardPerBox +10/seviye) — payback kasıtlı en uzunlardan (2.0-2.3 gün) çünkü hacimle otomatik büyüyen bir değer, enflasyon riski taşıyor; maxLevel 5 ile sınırlı önerildi.
5. Dolaylı küçük çarpan (Stamina) — payback en kısa (1.4-1.7 gün), ucuz "ilk yatırım" hissi için kasıtlı.
6. Opsiyonel/EV-bazlı (Görev Tier) — payback en uzun tolere edilen (2.4-3.2 gün), günlük değil oyun-bazlı EV.

**Gerçek maliyet formülü:** `gerçekMaliyet = fiyat × (1 + wealthTaxRate × kalanKiraDönemSayısı)`. Kalan dönem sayısı satın alma gününe göre 1-3 arası; varsayılan planlama için "kalan 2 dönem" kullanıldı.

**Why:** Kullanıcı "oyuncu neden hep aynı fiyatı ödememeli" prensibini istedi — düz `100×seviye` gibi tek formül yerine, her upgrade'in ekonomiye kattığı değerin karakteri (talep yaratma vs yakalama vs darboğaz vs doğrudan çarpan) farklı olduğu için farklı payback hedefleri ve fiyat eğrisi şekli (lineer vs sıçramalı) kullanıldı.

**How to apply:** Gelecekte yeni bir upgrade tipi eklenirse önce bu 6 kategoriden hangisine girdiği belirlenmeli, sonra o kategorinin payback bandı içinde fiyat türetilmeli. Sabit "her upgrade N×seviye" formülüne geri dönülmemeli — bu, GDD 13.2'deki eski formülün (Raf çok ucuz, Kuyruk çok pahalı çıktığı) tekrarına yol açar (bkz. rapor §11).

**Yeni kategori 7 (v2, "sembolik/achievement-only"):** Ekonomik değeri sıfır olan upgrade'ler (Water) payback modeline hiç sokulmamalı — sabit, düşük, nominal fiyat (mevcut en ucuz kategoriyle aynı bant, ör. 60 TL) yeterli. Zorla ROI hesaplamaya çalışmak anlamsız sayılar üretir.

**EV≈0 durumunda karar kuralı (v2, Quest Tier vakası):** Bir upgrade'in bağlı olduğu sistem (görev sistemi gibi) oyunda pasifse, fiyatı da EV'ye göre ≈0'a çekilmeli veya satın alma tamamen askıya alınmalı (UI'da gizlenmeli). Gerçek değer taşımayan bir şeye normal fiyat koymak oyuncu güvenini zedeler — bu ekonomik değil bir güven/UX riski, ama tespiti economist'in işi.

İlişkili: [[upgrade-dual-system]], [[rent_death_spiral]]
