---
name: roguelite-perk-pricing
description: Cargor roguelite upgrade draft (4 omurga + 16 perk) icin v3.2 fiyatlandirma (kontrol TUR3/3 ONAY) - tek kosullu T3 gate, Ucuz Kira tam tablo, Raf dogrusal dizi, Prestij Simsari duzeltilmis fiyat/etki
metadata:
  type: project
---

2026-07-08: Cargor'un upgrade sistemi "tüm upgrade'leri listele" yapısından **roguelite draft**'a geçiyor (günlük 3 kart, havuzdan tier-filtreli çekiliş). Tam tasarım: `docs/superpowers/specs/2026-07-08-roguelite-upgrade-draft-design.md`. Fiyat raporu: `UPGRADE_PRICING_REPORT.md` (v3, v2'nin üzerine yazıldı, v2 dosya sonunda arşivlendi).

**Yapı:** 4 kalan omurga (Storage max10/Table max2/Truck max2 — [[upgrade_pricing_framework]]'ten aynen taşındı, DEĞİŞMEDİ) + 16 yeni perk (3 güç tier'ı: T1/T2/T3). Money/Stamina/Queue/Water/Customer artık ayrı "upgrade" değil — kaldırıldı veya perk kimliğine dönüştü (Enerjik Ekip=stamina, Sabırlı Müşteriler=patience, Uzun Kuyruk=queue).

**Tier kilit eşikleri (v3.1'de sadeleştirildi, kontrol düzeltmesi sonrası):** T1 her zaman açık; T2 **sadece** gün≥5; T3 **sadece** gün≥9 (VE/OR yok, tek koşul). v3'teki "mağaza sv≥2/3" ve "prestij≥30" OR dalları KALDIRILDI çünkü: (1) `storeLevel` kodda hiç artmıyor (`CustomerManager.cs:71` sabit 1, gerçek bir XP sistemi yok — ölü kod yolu), (2) `prestij≥30` çok erken tetiklenebiliyordu (customerServedPrestigeBonus=0.5, startingPrestige=15 → sadece 30 teslimatla gün 4-6'da ulaşılabilir), bu da T3'ün "gün≥9 garantisi" iddiasıyla çelişiyordu. Tek koşullu gün≥9 kuralı Ucuz Kira'nın (T3, rentGrowthMultiplier düşüren perk) 4 kira döneminden yalnızca son 2'sini (gün12,16) etkileyebilmesini matematiksel olarak garanti ediyor — fiyat/değer teyidi bu kesin kurala göre yapıldı.

**Görev Tier kararı:** Draft havuzundan TAMAMEN ÇIKARILDI (sembolik fiyat değil) — sistem reaktive edilene kadar hiç teklif edilmeyecek, feature-flag ile filtrelendi. Reaktive edilirse v2 raporunun arşivlenmiş EV-bazlı fiyatları geri devreye alınabilir.

**Risk perk trade-off büyüklükleri (node ile hesaplandı):**
- Kumarbaz Kasası: ödül+%30/ceza+%55 — ~%20 hata oranına kadar (oyun içi bot-hata varsayımı) pozitif EV, %40+ hatada negatife dönüyor. Gerçek beceri eşiği.
- Yüksek Volatilite: ort+%15/tek kutu ±%35 — EV her zaman pozitif, sadece nakit akışı varyansı.
- Kaldıraçlı Kira: kira-%20/prestij cezası×2 — lossToZero 10→5 kayba düşüyor (bkz [[prestige_fragility]]).
- Kelle Koltukta: gelir+%25/grace period tam iptal — en sert T3 perk, en pahalı (800 TL).
- Acil Fren: iflası 1 kez önler, o gün geliri 0 + prestij-5.

**Ucuz Kira tam tablo (v3.1'de eklendi, önceden §3'te satırı yoktu):** 3 seviye, `baseCost=130, costStep=30` → Lv1 130 TL (1.15→1.12), Lv2 160 TL (1.12→1.09), Lv3 190 TL (1.09→1.06), toplam 480 TL. Gün≥9 tek-koşullu gate ile "son 2 kira dönemi" (gün12,16) tasarrufu: 1P 264 TL (ratio 0.55x, zararına), 2P 476 TL (ratio 0.99x, tam denk), 3P 635 TL (1.32x), 4P 793 TL (1.65x, kârlı). Artık "otomatik OP" değil — 1P'de gerçekten zayıf bir seçim.

**Prestij Simsarı gerçek (uncapped) değer teyidi (v3.1) — v3.2'de DÜZELTİLDİ, kontrol tur3/3 ONAY:** v3'ün "tavan 75/100" iddiası yanlıştı — gerçek `PrestigeManager.ModifyPrestige()` prestiji hiç kırpmıyor, sadece editör-only `GameEconomySettings.RunSimulation()`'ın yerel değişkeni 0-100 kırpıyor (gerçek oyunu etkilemiyor). Node simülasyonuyla emergent reward/box aralığı: 70 TL (1P yavaş) - 130 TL (4P iyi) gün16'da, perksiz. v3.1'de perkin gerçek TL değeri oyuncu sayısıyla çarpımsal patlıyordu: 1P'de 0.84-1.2x (makul), 2P'de 5.9x, 4P'de 18x (fiyat/değer oranı, fiyat 300/450=750 iken) — teslimat hacmiyle çarpılan sabit-TL bonus tasarımı "zengin daha zengin olur" riski taşıyordu. **Kontrol tur2'de bu KRİTİK bulundu (patlayıcı oran + rapor içi yanlış fiyat bazı çelişkisi) → v3.2'de düzeltildi:** (a) etki küçültüldü, `bonusPerTier` basamağı 5→7→9 (delta+2/+4) yerine **5→5.5→6** (delta+0.5/+1, %75 küçültme) yapıldı; (b) fiyat yükseltildi, Lv1 300→**510 TL**, Lv2 450→**505 TL** (toplam **1015 TL**). Yeni oranlar: 1P 0.11-0.17x (zararına, kabul edilebilir), 2P 0.81-0.82x, 4P (en kötü senaryo) **2.48-2.49x** — hedef banda (~≤2.5x, diğer T3'lerle aynı) girdi. Sadece tek kaldıraç (yalnız etki yarıya ya da yalnız fiyat) yetersizdi — ikisi birlikte gerekti (tam node hesabı `UPGRADE_PRICING_REPORT.md` §4.6'da).

**Bütçe fizibilitesi (gate-aware simülasyon, 20 kart toplam **9945 TL** — v3.2'de Prestij Simsarı +265 TL, v3.1'in 9680'inden):** 1P düşük gelir (600 TL/gün) senaryosunda "hepsini alma" gün 12'de İFLAS ediyor — matematiksel olarak imkansız. 2P düşük gelirde hayatta kalıyor ama son kasa sadece 92 TL (aşırı sıkı). Orta/yüksek gelirde rahat. Sonuç: 1P en kırılgan segment olmaya devam ediyor ([[rent_death_spiral]] ile tutarlı), roguelite yapısı bunu çözmüyor sadece taşıyor.

**Why:** Kullanıcı önceki v2 raporundaki iki kontrol bulgusunu (bütçe fizibilitesi tutarsızlığı + doğrusal kod formülü uyumu) baştan, dürüst şekilde çözülmesini istedi. Naif "kilitsiz eşit-dağılım" simülasyonu yanıltıcı çıkıyordu (gerçekte tier kilidi erken aşırı harcamayı zaten engelliyor) — gate-aware model yazıldı.

**How to apply:** Bu perk sistemine yeni kart eklenirse önce hangi tier'a (T1/T2/T3) girdiği ve niceliksel mi soyut mu olduğu belirlenmeli. Niceliksel etkiler (rent/reward/penalty/prestige formüllerine dokunanlar) mutlaka node ile gerçek TL/gün veya trade-off EV hesabından geçirilmeli — soyut QoL etkiler (hareket hızı, sabır gibi) tier bandı içi göreli sıralamayla fiyatlanabilir (spec bunu izin veriyor).

İlişkili: [[upgrade_pricing_framework]], [[rent_death_spiral]], [[prestige_fragility]], [[upgrade_dual_system]]
