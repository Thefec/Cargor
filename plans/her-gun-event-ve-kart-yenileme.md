# Her Gün Event + 6 Kapalı Kartın Yeniden Tasarımı

> Durum: **UYGULANDI + main'e MERGE (2026-09-25), kontrol ONAY 1. tur, derleme 0 hata, denetçi 249/249. KALAN: kullanıcı playtest (Faz 4).** Tasarımın tek kaynağı: `docs/economy/her-gun-event-ve-kart-tasarimi-2026-09-24.md` (22 event, bant kuralı, 6 kart, değerler). Uygulama adımları en altta "UYGULAMA PLANI".
> Kaynak: tasarım denetimi `docs/playtest/tasarim-denetimi-2026-09-18.md` madde 4 + 5.
> Kullanıcı kararı: Karar 1 → **B** (kira günleri normal, kalan her gün event'li) · Karar 2 → **B** (kapalı kartlara yeni efekt) · Karar 3 → playtest sonrası.

## İŞ 1 — Kira günü hariç her gün event

### Şu an (kod gerçeği)
- `Assets/NewCss/Events/EventCalendarUI.cs` (~1000 satır)
  - `_allEvents` :162-180 → 16 event (8 pozitif / 8 negatif)
  - `GenerateInitialEvents` :739-772 → ilk 3 gün boş, aralık `EVENT_INTERVAL_MIN/MAX` 1-2 gün, kira günleri atlanıyor
  - `SelectEventByCount` :774-787 → ilk 2 pozitif, 3. negatif, kalanı TEKRAR ENGELİ OLMADAN rastgele
- Koşu başına ort. ~5.9 event → ~10 gün olaysız.
- Etkisiz event'ler (A2): `IsGoldenBoxDay()`/`IsVIPServiceDay()` okuyucusuz; BUSY DAY / MARKETING DAY / ANGRY CUSTOMERS / GOLDEN BOX DAY'in kota/müşteri artışı mekanik olarak işlemiyor (varış aralığı bölünmüyor, GDD §7.2). **Uygulamadan önce güncel koda karşı yeniden doğrula** (denetimden sonra zorluk ölçekleme değişti).

### Hedef
- Event'li günler: 1-3, 5-7, 9-11, 13-15 = **12 gün**. Kira günleri (4/8/12/16) event'siz.
- Bir koşuda **aynı event iki kez gelmez**.
- Koşular birbirinden farklı olsun → katalog **≥ 20 çalışan event** (12 slot / 20 havuz).

### Adımlar
1. **gameplay (salt okunur teşhis):** 16 event'in her biri gerçekten bir şey değiştiriyor mu? Tablo: event → okuyan kod satırı → çalışıyor/etkisiz.
2. **economist:** event sistemini sim'e ekle (şu an `runFullSim` event modellemiyor, GDD:2424-2427). Sonra:
   - etkisiz event'leri düzelt/yeniden tanımla,
   - 4-8 yeni event tasarla (değerleriyle),
   - günlere göre dağılım kuralı: gün 1-3 hafif (öğretici), geç günler sert; pozitif/negatif oranı,
   - "her gün event" sonrası iflas/gelir tablosu (zayıf/orta/iyi × 1-4P) — mevcut kira fonu kilidiyle birlikte.
3. **gameplay:** takvim üretimini yeniden yaz (her kira-dışı gün 1 event, tekrar engeli, gün bandına göre havuz) + yeni/düzeltilen event efektleri. Netcode: takvim seed'le üretiliyor — client'larla aynı sonucu verdiğini koru.
4. **graphics-ui + müdür:** takvim UI 12 event'i sığdırıyor mu; yeni event adları/açıklamaları 17 dil (StringTable).
5. qa → kontrol.

## İŞ 2 — 6 kapalı kartı yeniden tasarla

| Kart | Şu an | Not |
|---|---|---|
| Sağlam Kasa (Money) | efekt NO-OP | eski hali kutu ödülünü düşürüyordu |
| Güler Yüz (Customer) | koda bağlı değil | |
| Su Sebili (Water) | efekt yok | |
| Dinç Ekip (Stamina) | `energetic_crew` perkinin kopyası | |
| Geniş Kuyruk (Queue) | `long_queue` perkinin kopyası | tek servis masası kararıyla anlamı zayıf |
| Uzun Kuyruk (`long_queue` perk) | sim'de ~0 / 4P −51 TL | tek masa → kuyruk büyütmek değer katmıyor |

Yer: `UpgradePanel.cs:76` (`disabledInDraft`), `DraftPool.cs:27`, sahne `The Main Office.unity` ~27291-27596, `PerkEffect.cs`.

### Adımlar
1. **economist:** 6 kart için yeni efekt + fiyat + tier. Kural: birbirinin kopyası olmasın, mevcut 19 aktif kartla çakışmasın, zayıf takımı iflasa itmesin (ekonomi raporundaki zayıf-tier tuzağı). Kartlardan biri event sistemiyle etkileşebilir (örn. "negatif event etkisi −%X") — İş 1 ile birlikte tasarla.
2. **gameplay:** efektleri yaz, `disabledInDraft: 0`. **Liste silme/sıralama yok** — NetworkList index'i korunur. Perk snapshot+restore sistemine yeni yazılan alanları ekle (bkz. hafıza: perk-mutates-persistent-assets).
3. `EconomyInvariantCheck.cs:297` beklentilerini güncelle.
4. Adlar/açıklamalar 17 dil.
5. qa → kontrol.

## Sıra
İki iş ekonomiyi birlikte etkiliyor → **economist tek turda ikisini birden tasarlar ve sim'ler** (İş 1 adım 2 + İş 2 adım 1). Kullanıcı tasarımı onaylar → gameplay uygular (İş 1 ve İş 2 paralel, ayrı dosyalar) → qa → kontrol (dal sonu tek kapı).
Dal: `feature/daily-events-and-cards`.

## Açık sorular (varsayılanlar yazıldı)
- Gün 1'de event olsun mu? → **Evet ama yalnız pozitif/hafif** (ilk oyun öğretici kalsın).
- Pozitif/negatif oranı? → economist önersin, varsayılan ~%50/%50, geç günlerde negatif ağırlıklı.
- 6 kartın hepsi mi dönecek, yoksa 2 kopya birleşip 5 mi? → varsayılan **6'sı ayrı yeni efekt**.

---

## KULLANICI KARARLARI (2026-09-25)
- **S1 Taksit ↔ Acil Fren:** aynı teklifte ASLA birlikte çıkmaz; biri alınınca diğeri bir daha hiç çıkmaz. Mevcut dışlama grubu mekanizmasıyla (`UpgradePanel.BuildExclusionGroups` ~:1410-1425, `DraftPool.SelectOffer` :39-61). Reroll dahil.
- **S2 Adlar:** 6 kartın hepsi yeni ad: Sağlam Kasa→**Taksit**, Su Sebili→**Serinlik**, Uzun Kuyruk→**Hava Raporu**, Güler Yüz→**Bahşiş**, Dinç Ekip→**Sabah Vardiyası**, Geniş Kuyruk→**Sıra Numaratörü**. 17 dil.
- **S3 Kart teklifi tohumu:** bu dalda düzeltilecek — koşu seed'i teklif ve reroll RNG'sine katılır (`UpgradePanel.cs:1343`, `:1533`). Host/client aynı teklifi görmeli (teklif zaten server'da üretilip NetworkList ile gidiyor — doğrula).
- **S4 Gün 13-15 sert negatifler:** kabul (Tedarik Grevi dahil).
- **S5 Karışık Sevkiyat:** economist dışarıda bırakmıştı, kullanıcı **KATALOĞA ALINSIN** dedi. Sim'de gelir artırıcı çıktığı için **nötr/takas (±)** olarak etiketlenir, yalnız **gün ≤12** bandında (13'ten sonra zaten kalıcı açık). Katalog 23 event olur. economist değer/bant yerleşimini teyit etsin (kısa iş).

## UYGULAMA PLANI
Dal: `feature/daily-events-and-cards` (main'den). Commit'e GİRMEYECEK: font atlasları, StringTable satır sonu gürültüsü.

**Faz 0 — economist (kısa, ≤20 dk):** S5 Karışık Sevkiyat değeri + bant; S1 kararıyla (birlikte alınamaz) zayıf tablo zaten ölçülmüştü → teyit.

**Faz 1 — paralel:**
- **gameplay-A (event sistemi):** `EventCalendarUI.cs` takvim üretimi (kira-dışı her gün, bant havuzu, tekrarsız, gün 1 dahil) · `EventEffectManager.cs` — gün 1'de OnNewDay tetiklenmiyor (`DayCycleManager.cs:1002`) → koşu başında aktif et · iki isim listesi (`EventCalendarUI.cs:162`, `EventEffectManager.cs:35`) — yeni event'ler ikisinin de sonuna eklenir (NOT, qa 2026-09-25: iki liste main'de de farklı sırada; eşleşme İSİM ile, `EventEffectManager.cs:564` `eventNames.IndexOf(name)` — raw index varsayımıyla kod yazma) · 6 yeni + 4 yeniden tanımlı + Karışık Sevkiyat efektleri (tasarım dokümanı §B'deki "K" kancaları) · günün rengi takvim RNG'sinden, event kaydında sakla (client UI aynı rengi göstersin).
- **gameplay-B (kartlar + teklif):** 6 kartın efektleri (§D), `disabledInDraft: 0` (liste silme/sıralama YOK) · Taksit: `_graceUsed` bool → sayaç; kilit muafiyeti `emergency_brake` sabitinden (`UpgradePanel.cs:1607`) listeye · Taksit+Acil Fren dışlama grubu (S1) · teklif tohumu (S3) · yeni yazılan asset alanlarını perk snapshot+restore'a ekle (`gracePaymentPercent` vb.) · `EconomyInvariantCheck.cs:245/297` beklentileri.
- **müdür:** 23 event + 6 kart adı/açıklaması 17 dil (StringTable).

**Faz 2 — graphics-ui:** takvim UI 12 event'i sığdırıyor mu; nötr (±) event görünümü; günün rengi göstergesi (Altın Kutu / Tek Renk).

**Faz 3 — kapı:** Unity headless derleme · qa (netcode: takvim/teklif host-client aynılığı, index güvenliği, snapshot) → kontrol (dal-sonu tek ONAY, ≤3 tur).

**Faz 4 — kullanıcı playtest:** gün 1-3 event'leri görünüyor mu, takvimde tekrar yok, Taksit/Acil Fren asla yan yana değil, iki koşuda gün 5 teklifi farklı.
