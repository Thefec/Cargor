# Ekonomi Ö-A / Ö-B / Ö-C uygulaması (2026-09-24)

Kaynak: `docs/economy/ekonomi-sifirdan-2026-09-23.md` §2, §4, §5 (Opus economist, sıfırdan hesap).
Kullanıcı kararı (2026-09-24): **üçü de yapılacak; Ö-B'de hiçbir kart KALDIRILMAYACAK — efektleri düzeltilecek.**
Dal: `feature/economy-oa-ob-oc`.

## Ö-C — kira affı bug'ı (gameplay, değer yok)
`PerkEffect.cs` `ApplyLeveragedRent` (:~332) / `ApplyAllIn` (:~350) `gracePaymentPercent = 0f` yapıyor; `DayCycleManager.TryProcessMoneyCheck` (:~619-627 eski numara) grace dalında %0 alıp kirayı ÖDENMİŞ sayıyor → perkin "bedeli" bedava kira. Doğru davranış: bu perkler grace'i **iptal eder** — kasa kiraya yetmezse grace dalına girilmez (Acil Fren varsa o, yoksa iflas). Bayrakla (ör. `graceDisabled`), `%0` değil. Perk kaldırılınca/sıfırlanınca bayrak geri alınmalı (perk sistemi snapshot/idempotent deseni — bkz. hafıza perk-mutates-persistent-assets).

## Ö-A — kira fonu kilidi (gameplay + UI, değer economist'ten: 1.0)
- Yeni ekonomi değeri `upgradeRentReserveFraction = 1.0` (`GameEconomySettings` SO + `EkonomiAyarlari.asset`).
- Kart alımı ve reroll, alım SONRASI kasa `< reserve × sıradaki kira` olacaksa REDDEDİLİR (server-authoritative: `UpgradePanel` purchase/reroll ServerRpc'lerinde). **Acil Fren (insurance perk) muaf.**
- "Sıradaki kira" = `DayCycleManager.NextRentAmount` (server'da `CalculateRent(false)` ile aynı değer). Kira günü kesimden sonra (gün sonu → yeni gün) yeni döngünün kirası geçerli.
- UI: panelde "Kira fonu: X TL (gün N)" satırı; kilit yüzünden alınamayan kartta sebep (buton pasif + kısa metin). Metinler loc anahtarlı (müdür 17 dil ekler).
- Fiyatlar, P-dizisi, reroll maliyeti DEĞİŞMEZ.

## Ö-B — 3 zararlı kartın efekti (economist tasarlar → gameplay uygular)
Hızlı Hangar (T2, hangarStayDuration ×0.75 → tır dolmadan kalkıyor), Mesai Saati (T1, gün ×1.125), Prestij Ustası (T2, customerServedPrestigeBonus 0.4+0.12·lvl). Canlıda net Δkasa negatif, zayıf takım kaybını +14..+29 pp artırıyor. Kullanıcı: kaldırma yok, düzelt. Economist yeni efekt/değer + sim kanıtı verir; hedef: her kart en az bir profil/oyuncu sayısında net pozitif, zayıf takımda kaybı artırmıyor, dominant yeni kart yaratmıyor.

## Kapı
economist (Ö-B spec + Ö-A'nın oyuncu-sayısı-kilidi sonrası geçerliliği) → gameplay (A, C, sonra B) → müdür loc → qa → kontrol ONAY → kullanıcı playtest → commit.
