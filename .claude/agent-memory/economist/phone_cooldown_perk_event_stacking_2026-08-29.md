---
name: phone-cooldown-perk-event-stacking-2026-08-29
description: phone_line perk (phoneCooldownPerkBonusSeconds) ve CUSTOMER SUPPORT event cooldown carpani onayi, stacking riski kod-kanitiyla cozuldu
metadata:
  type: project
---

**Gorev**: `plans/plateup-musteri-telefon.md` adim 5, dar onay turu. Telefon V4 (disari
arama modeli) icin gameplay iki placeholder birakti, ikisi de ONAYLANDI, degisiklik onerilmedi.

## ONAYLANAN IKI SAYI
1. **`phoneCooldownPerkBonusSeconds = 10f`** (`PerkEffect.ApplyPhoneLine`, PerkEffect.cs:295) —
   taban 20sn'yi 10sn'ye indirir (%50). Fiyat sahnede `The Main Office.unity:27607`
   `baseCost=160, maxLevel=1, tier=0` (en ucuz katman, tek-seferlik) — mutlak deger 10f bu
   ucuz/hafif QoL karakteriyle orantili.
2. **CUSTOMER SUPPORT event `cooldown *= 0.5f`** (`PhoneCallManager.GetEffectiveCooldownSeconds`,
   zaten kod-sabiti, config alani DEGIL) — degisiklik onerilmiyor, aynen kalsin.

**Gerekce (tek satir)**: Perk ve event AYNI degeri uretiyor (20->10sn) ve ikisi de HasUnspawnedCustomers
kota-tavanina carpiyor, yani mutlak sn degeri onemsiz hale geliyor — bkz asagi.

## ⭐ CARPISMA RISKI: YOK (kod-kanitiyla dogrulandi, "para basma" degil)
Kok neden: `CustomerManager.ForceSpawnNextCustomer` (cagiran: telefon call handler) `TryExecuteSpawn`
ile AYNI `_customersSpawnedToday` sayacini artirir ki `HasUnspawnedCustomers => _customersSpawnedToday
< _todaysTotalCustomers` (CustomerManager.cs:261) dogal spawn ile de bu sayaci kullanir. Yani telefon
YENI musteri YARATMIYOR, gunun SABIT kotasindan birini ERKEN cekiyor. Kota tukenince
(`HasUnspawnedCustomers=false`) cagri REDDEDILIYOR (PhoneCallManager.cs:326) — cooldown ne kadar
kisa olursa olsun bu guard'i asamaz.

Python ile dogrulandi (winSec=telefon saati 8-18 penceresinin gercek-saniye karsiligi, gun1=181.8sn,
gun16=300sn; callsFitByCooldown=pencereye sigan cagri sayisi):
```
P1 gun1  cd=20(taban)  callsFit=10  quotaHi=6   -> actual=6   (kota zaten bagliyor)
P3 gun1  cd=20(taban)  callsFit=10  quotaHi=13  -> actual=10  (COOLDOWN bagliyor, kota-ACIGI var)
P3 gun1  cd=10(perk YA DA event, ikisi ayni deger) callsFit=19 quotaHi=13 -> actual=13 (acik KAPANDI)
P3 gun1  cd=5 (perk+event ust uste)  callsFit=37  quotaHi=13 -> actual=13 (PERK-ONLY ile AYNI, FARK YOK)
```
**Sonuc**: perk VEYA event TEK BASINA (ikisi de ayni 20->10sn degerini uretiyor) P3/P4'un gun1
darbogazini zaten TAM kapatiyor; ikisi UST USTE binip 5sn'ye dusse bile EK TEK BIR cagri bile
eklemiyor — kota tavani mutlak. Stacking senaryosu matematiksel olarak NO-OP, ekonomik risk yok.

## Kota-sinirli gunluk ekstra gelir tavani (degismeyen, cooldown'dan bagimsiz)
`rewardPerBoxByPlayerCount=[50,55,70,88]` (bkz [[plateup_customer_quota_2026-08-29]]) ile:
P1: 6 cagri x 20TL = +120TL/gun (o gunku kutu gelirinin ~%40'i)
P2: 12 cagri x 20TL = +240TL/gun (~%36)
P3: 13 cagri x 20TL = +260TL/gun (~%29)
P4: 13 cagri x 20TL = +260TL/gun (~%23)
Bu TAVAN sabit — cooldown 20/10/5 sn fark etmeksizin AYNI (yalnizca oyuncunun bu tavana ne kadar
COLAY ulasacagini degistiriyor, tavanin KENDISINI degil). +2.4/+4.8/+5.2/+5.2 prestij de ayni mantikla
sinirli. Mutlak degerler mevcut ekonomiye (P4 sonKasa~6366-7635, bkz plateup notu) gore kucuk/makul.

## Yan not (kod DEGIL, sadece iceriktir)
`The Main Office.unity:27599` phone_line displayName metni hala eski model diliyle yazili
("telefon siparis sayisi +1 artar") — yeni cooldown-kisaltma etkisini anlatmiyor. Ekonomik
karar bu degil, ama localization/UI departmanina not dusulmeli (gorevin disinda, sadece flag).

Iliskili: [[plateup_customer_quota_2026-08-29]] (kota tablosu, timeSkipAmount, rewardPerBox
kaynagi), [[phone_passive_redesign]] (ESKI additive+clamp modeli, artik V4 ile GECERSIZ —
ring-chance kavrami tamamen kalkti).
