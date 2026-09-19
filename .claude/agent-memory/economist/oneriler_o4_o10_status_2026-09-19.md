---
name: oneriler-o4-o10-status-2026-09-19
description: Ö4-Ö10 (docs/economy/05-oneriler.md) build-hazirlik denetiminde tek tek dogrulandi — hepsi hala UYGULANMADI; ciddiyet etiketleriyle liste
metadata:
  type: project
---

**Baglam.** 2026-09-19 build-hazirlik salt-okunur denetiminde `docs/economy/05-oneriler.md`
Ö4-Ö10 tek tek okunup kod/asset ile capraz kontrol edildi. Ö1+Ö2+Ö3 uygulanmisti (`3424460`,
main'de); Ö4-Ö10 **hicbiri uygulanmadi** (`PLAN.md` DURUM tablosuyla eslesiyor).

| # | Ne oneriyordu | Uygulanmazsa ne eksik/bozuk kalir | Ciddiyet |
|---|---|---|---|
| Ö4 | Paketleme İstasyonu kartı 2. servis istasyonunu açsın (sahnede `serviceTables[1]` boş slot) | Kart hâlâ −12.3 TL/gün zararlı iş yükü kartı; fiilen tek servis istasyonu → 3P/4P'de oyuncu-zamanının %17-26'sı boşa gidiyor | Oyunu bozmaz ama hissi bozar — co-op'ta 4. oyuncu işe yaramıyor |
| Ö5 | `prestigePerBonus` 8→6 (asset+cs) | Doğrulandı: hâlâ 8 (`GameEconomySettings.cs:83`, `EkonomiAyarlari.asset:25`). 1P prestij ödülü zayıf kalıyor, 4P iyi tavana yakın israf | İnce ayar — oyunu bozmaz |
| Ö6 | Ö1-5 sonrası taban kira +%15 | Uygulanmadığı için risk zaten Ö1-3 ile düşmüş durumda kalıyor (orta/mantıklı kayıp ~%9-14 — REC'in hedeflediği %12.3'e yakın, tesadüfen kabul edilebilir bölgede) | Düşük — sırası Ö1-5'ten sonra geliyor, henüz gerekli değil |
| Ö7 | 4P kotasını 3P'den ayır (`dailyCustomerCountP4` hâlâ P3 ile birebir) | Doğrulanmadı ama PLAN/kod notlarına göre hâlâ eşit; 4P boşta oyuncu %20.9, günün %93.7'si erken bitiyor (iyi takım) | Hissi bozar — "4. oyuncu gereksiz" algısının kök nedeni |
| Ö8 | 5 kart (Görev Kademesi/Mesai Saati/Geniş Ambar/Acil Fren/Toplu Alım) etkisini yeniden tanımla | Görev Kademesi hâlâ **negatif amortisman** (kart almak zarar) — Ö1'in fiyat indirimi kurtarmıyor | Orta — tek bir kart aktif olarak oyuncuyu cezalandırıyor |
| Ö9 | Gün erken bitme tasarım kararı (kota/süre/serbest üretim) | İyi takımlarda günün %89-97.5'i atlanıyor — "statik/olaysız" şikâyetinin mekanik kökü hâlâ duruyor | Deneyim/his — oyunu bozmaz, "neden bir şey olmuyor" hissini besler |
| Ö10 | İkinci para yutağı (gün-16 atıl kasa için) | Gelirin %30-62'si atıl kalıyor, ekonomik olarak zararsız ama motivasyon açısından boşluk | Kozmetik/ince ayar |

**Ayrıca build-hazırlık turunda yeni bulundu (Ö-listesinde YOK):** [[zayif_tier_upgrade_trap_2026-09-19]] —
zayıf beceri + kart alma stratejisi %89-100 kayıp, Ö1-3'ten önce de sonra da değişmedi.

**Verdict'e katkı:** Ö4-Ö10'un hiçbiri "oyun build alınamaz" seviyesinde değil (crash/placeholder
yok); hepsi denge/his ince ayarı. Tek gerçekten "aktif zararlı" kalan kalem Ö8'deki Görev Kademesi
kartı ve yeni bulunan zayıf-tier tuzağı — ikisi de spesifik bir oyuncu profilini cezalandırıyor,
genel oynanabilirliği engellemiyor.

Ilişkili: [[economy_full_balance_round10_2026-08-30]], [[cargor-economy-status]] (kullanıcı hafızası)
