---
name: economy-full-balance-round12-gdd-resync-2026-08-30
description: Round 12 - Round 10/11 koda islendikten (commit bb98ad1) sonra GDD.md'nin uygulama-sonrasi 2. senkronu; 14 bolumde ~30 duzeltme, quest best-of-K bolumu EKLENDI, 11 REDDEDILEN oneri "gelecek plan" gibi degil "olculdu ve reddedildi" olarak yazildi; 2 yeni senkron acigi bulundu (DayCycleManager fallback kirasi, invariant sayisi)
metadata:
  type: project
---

# Round 12 — GDD.md uygulama-sonrası senkron (2026-08-30, Opus)

Takip: `plans/economy-full-balance-2026-08-30.md` → "SIRADAKİ / Adım 4".
**Değişen tek dosya: `GDD.md`** (392 ekleme / 175 silme). Oyun kodu/asset/sahne yalnız OKUNDU.

**Taban:** commit `bb98ad1` — Round 10'un 12 UYGULA maddesi + Round 11 (U6 geri alma, best-of-K,
ceza dengelemesi) koda işlendi, `kontrol` 2. turda ONAY verdi.

---

## 1. Koda karşı DOĞRULANAN canlı değerler (GDD'ye bunlar yazıldı)

| ne | canlı değer | dosya:satır |
|---|---|---|
| `baseRentByPlayerCount` | `{290,650,1140,1630}` | `GameEconomySettings.cs:21` + asset hex `220100008a020000740400005e060000` |
| `timeSkipAmountByPlayerCount` | `{115,49,47,47}` | `GameEconomySettings.cs:117` |
| `phoneTimeSkipPerkMultiplier` | alan var, default `1f`; perk `0.80f` | `:120` / `PerkEffect.cs:308` |
| `phoneCooldownPerkBonusSeconds` | perk `1f` (eski 10f) | `PerkEffect.cs:309` |
| `wrongProductPrestigePenalty` | `-0.20f` (asset `-0.2`) | `:153` + `EkonomiAyarlari.asset:35` |
| `cheap_rent` | `1.20f - 0.03f*level` | `PerkEffect.cs:198` |
| CUSTOMER SUPPORT | `GetEffectiveTimeSkipMinutes` ×0.5 (cooldown dalı KALDIRILDI) | `PhoneCallManager.cs:311-323` |
| `DailyQuestTargetCount` | SABİT 3 (U6 geri alındı) | `QuestManager.cs:150` |
| best-of-K | `SelectDailyQuestsStratified` cs:583-649 · `CalculateCandidateCount` cs:662 · `CalculateFeasibilityScore` cs:675 | — |
| quest ödül/ceza | Easy 28/15/0.6/0.32 · Med 60/**20**/1.2/**0.4** · Hard 150/**30**/3.0/**0.6** | 30 asset (grep ile 11/10/9 dağılımı doğrulandı) |
| telefon quest hedefleri | `Q_Easy_6_Phone=1`, `Q_Medium_6_Phone=2` | asset:22 |
| Görev Kademesi muafiyeti | `GetCostMultiplier(upgrade)`, `isQuestTierUpgrade` | `UpgradePanel.cs:1638-1644` |
| Görev Kademesi fiyatı | `baseCost=80 costStep=20 maxLevel=2` → L1 80, L2 100 (P-DÜZ) | sahne `The Main Office.unity:27210-27211` |
| tip-6 D2 muafiyeti | `CompleteSpecificColorTruck` listede | `QuestManager.cs:735` |
| `phone_line` sahne metni | "Dışarı arama zaman maliyetini %20 azaltır." (V3 metni GİTTİ) | sahne:27397 |
| yeni invariant | sahne `questSlots.Count == 3` | `EconomyInvariantCheck.cs:193-195` |

**DEĞİŞMEYENLER (Round 10 §4 ile reddedildikleri için):** `prestigePerBonus=8f` (`:83`),
`callMoneyReward=20` (`:129`), `callPrestigeReward=0.4f` (`:132`), `phoneCooldownSeconds=3f` (`:123`).

---

## 2. GDD'de güncellenen yerler (14 bölüm, ~30 nokta)

- **Başlık + IMPORTANT/CAUTION bloğu** — "henüz uygulanmadı" uyarısı → "uygulandı (bb98ad1)" +
  **11 REDDEDİLEN önerinin açık listesi** (gelecek-plan sanılmasınlar diye).
- **§4.2** parametre ağacı: kira, telefon dizisi, yeni `phoneTimeSkipPerkMultiplier` satırı,
  perk cooldown 10→1, `wrongProduct` −0.20, `prestigePerBonus`'a "10 reddedildi" notu;
  ölü V3 anahtarları artık SİLİNDİ; PerkEffect **7 alan → 8 alan** + satır numaraları tazelendi.
- **§5.2/§5.3** kira tabloları (yeni taban + 16 günlük toplamlar + kesinti kolonu), Slow/strict
  kurtarma + Normal bandın **+%16-115 şişmesi** açıkça yazıldı, yedek eğri `{350,730,1190,1650}`.
- **§6.2** prestij kaynakları (wrongProduct −0.20, quest prestij ×0.4), "dump the customer"
  CAUTION → NOTE (5 kat → 2 kat).
- **§6.4** "Hard görev kaçırmak ~7 müşteri" → **1.5 müşteri**.
- **§13.2** Görev Kademesi P-muafiyeti bloğu EKLENDİ (fiyat 80/100 P-düz, X6 fiyat-indirimi RED),
  `cheap_rent` bug'ı "düzeltildi", `long_queue` iddiası "çürüdü".
- **§14.1/§14.4** telefon: yeni T dizisi, çarpan tablosu (perk 0.80 / event 0.50), maliyet/aralık
  %67-79, gün-16 dönüşümü, perk+event indirim tablosu EKLENDİ, "SkipTime'a dokunmayın" (R5),
  "ikinci koşulsuz para musluğu" WARNING'i EKLENDİ.
- **§15.2/§15.3** CUSTOMER SUPPORT satırı + mekanizma haritası; FESTIVAL outlier'ı "çözüldü,
  event'e dokunmadan"; event asimetrisi "düzeltilmedi, sönümlendi".
- **§16.1** ⭐ **YENİ ALT BÖLÜM: best-of-K teklif seçimi** (K tablosu, fizibilite skoru formülü,
  ölçülen 4/16→0/16, çeşitlilik %24.9, adaptif vs statik uyarısı) + `DailyQuestTargetCount` UI
  slot CAUTION'ı (U6 no-op mekanizması).
- **§16.2** ödül/ceza tablosu + ödül/ceza oranı kolonu; "prestij ×0.4 neden"; "naif oyuncu Hard
  16/16 negatifti" gerekçesi; "ödenmiş kötüleştirme ÇÖZÜLDÜ".
- **§16.3** gün-16 settlement %0.1-8.2 → düzeltme önerilmiyor.
- **§16.4** AnswerPhone hedefleri 1/2 + adaptif skorun ikinci güvenlik ağı.
- **§16.5** tip-6 muafiyeti eklendi.
- **§19.1/§19.2** kira + telefon satırları, Görev Kademesi satırı EKLENDİ, gelir/kira ölçek
  karşılaştırması (1 : 2.24 : 3.93 : 5.62).
- **§21.1** docstring çelişkisi kapandı + "prestij ≥ 30 kapısı ters tepiyor" (R10).
- **§31 (giriş)** sim v4.0 → **v5.1**, "3 model açığı" bloğu → "6 hata kapandı" bloğu.
- **§31.3** bant sağlığı tablosu tamamen yeniden yazıldı (16/16 hayatta, önce/sonra kolonlu) +
  "Slow/strict P1/P2'de telefon spam'i kabul edilmiş açık" WARNING'i.
- **§6.3 civarı** prestij tavanı: quest'li 6/16 → **0/16**.
- Doğrulama sayısı **"77 Expect*" / "165 kontrol" → 79 çağrı yeri, çalışma anında ~196**
  (30 quest asset'i 4'er `ExpectFloat` ile döngüde denetleniyor: 75 + 4×30 + 1).

---

## 3. ⭐ Bu turda BULUNAN yeni senkron açıkları (kod değişikliği önerisi, ucuz)

1. **`DayCycleManager.CalculateRent` fallback dalı hâlâ ESKİ kirayı taşıyor**
   (`DayCycleManager.cs:672-679`: `playerCount == 1 ? 500 : ... 1800`). Yalnız `economySettings`
   atanmamışsa çalışır (o durumda `LogWarning` da basar), yani canlı risk düşük — ama sessizce
   eski ekonomiye düşme yolu. GDD §4.2'ye uyarı olarak yazıldı, **kod düzeltilmedi**.
2. **`EconomyInvariantCheck` kontrol sayısı belirsiz raporlanıyordu** — GDD iki farklı yerde
   77 ve 165 diyordu, commit mesajı 200. Gerçek: **79 statik `r.Expect*` çağrı yeri**, bunlardan
   4'ü 30-elemanlı quest döngüsünün içinde → çalışma anında **~196**. GDD'de ikisi de yazıldı.

---

## 4. Metodolojik not (gelecek turlar)

GDD'ye "önerildi ama uygulanmadı" notu düşmek Round 9'da doğru karardı, AMA bu notlar
**uygulama turundan sonra kendiliğinden bayatlıyor** ve okuyucu için "yapılacak iş" gibi
görünüyor. Round 12'de bunlar iki sınıfa ayrıldı ve GDD'de AÇIKÇA etiketlendi:
- **UYGULANDI** → canlı değer + kısa "neden" + ölçüm.
- **ÖLÇÜLDÜ VE REDDEDİLDİ** → değerin neden DEĞİŞMEDİĞİ yazıldı (yoksa bir sonraki tur aynı
  öneriyi tekrar getirir; Round 10 §4'ün 11 maddesi tam olarak bu riski taşıyordu).

İlgili: [[economy_full_balance_round9_2026-08-30]] (1. senkron),
[[economy_full_balance_round10_2026-08-30]] (12 UYGULA / 11 UYGULAMA),
[[economy_full_balance_round11_2026-08-30]] (U6 geri alma + best-of-K),
[[economy_full_balance_round3_2026-08-30]], [[economy_full_balance_round7_2026-08-30]],
[[money_comes_only_from_trucks]], [[serial_customer_service_ceiling]].
