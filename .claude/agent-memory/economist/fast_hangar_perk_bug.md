---
name: fast_hangar_perk_bug
description: PerkEffect.ApplyFastHangar hardcoded eski 120s tabanini kullaniyor (canli taban 30s) - aciklama +%30 vaat ediyor ama kod +%420 veriyor (156s absolut atama)
metadata:
  type: project
---

2026-07-18 bulgusu (`plans/economy-balance-round.md`, birleşik ekonomi turu). `Assets/NewCss/UpgradeScripts/PerkEffect.cs:96-100`:

```csharp
private static void ApplyFastHangar(int level, PerkContext ctx)
{
    ctx.Truck.hangarStayDuration = 120f * 1.30f;   // = 156, ESKİ taban (120) hardcode
}
```

Sahne metni (`The Main Office.unity:21835`) "Tırın hangarda kalma süresi %30 uzar" diyor, fiyat
280 TL (T1, `UPGRADE_PRICING_REPORT.md` §3-A ile eşleşiyor — fiyatlama doğru, uygulama yanlış).
Canlı taban `GameEconomySettings.hangarStayDuration=30` (bkz [[truck_hangar_window_cap]], 120→30
değişimi bu perk kodundan SONRA yapılmış, perk kodu güncellenmemiş — klasik "bir parametre
değişince bağımlı kodun senkron kalmaması" deseni, [[rent_death_spiral]]'daki tekil-parametre
etkisiyle aynı kategori).

**Gerçek/vaat edilen fark:** vaat 30→39 (+%30), kod 30→156 (+%420).

**Etki modele göre değişiyor (node, `truckCapStrict`/`truckCapOptimistic`):**
- OPTIMISTIC model (sim'in birincil modeli, `truck_hangar_window_cap`'te tanımlı): `hangarStayDuration`'a
  hiç referans vermiyor → bug'ın etkisi **SIFIR**.
- STRICT model (kötümser alt sınır): bug doğru-düzeltmenin üstünde fazladan kapasite veriyor —
  1P'de doğru fix +%7-9 kazandırırken bug +%31-36 kazandırıyor; 4P'de fark küçülüyor (kargo tavanına
  zaten çarpılıyor, ~+%3 her ikisinde de aynı).

**Öneri (henüz uygulanmadı, gameplay'e devredilecek):** `ctx.Truck.hangarStayDuration =
ctx.Economy.hangarStayDuration * 1.30f;` — diğer tüm `PerkEffect` metodlarının (`ApplyGamblerCase`,
`ApplyLeveragedRent` vb.) zaten kullandığı "canlı Economy tabanından relatif hesapla" deseni,
yalnız bu metod istisna/eski kalmış.

**Why:** Kullanıcı "genel ekonomi turu" istedi, roguelite perk kodu (v3.2 raporundan sonra yazılmış)
ile canlı ekonomi sabitleri arasında drift kontrolü yaparken bulundu.

**How to apply:** Perk/upgrade koduna her dokunulduğunda (Truck/hangar ile ilgili herhangi bir
değişiklik) bu metodun düzeltilip düzeltilmediğini kontrol et — düzeltilmeden bırakılırsa STRICT
model gerçek oyun davranışına yakın çıkarsa (playtest) bu perk mütevazı bir "hafif Truck omurgası
versiyonu" olmaktan çıkıp anlamlı bir kapasite sıçraması olur.

İlişkili: [[truck_hangar_window_cap]], [[roguelite_perk_pricing]], [[rent_death_spiral]]
