# 🙋 Task 7 — Senin Yapacakların (qa 3 bulgusu)

> qa incelemesi Task 7 kodunu sağlam buldu **ama** 3 iş senin Unity Editor'de elle yapmanı gerektiriyor. Ben C# tarafını hallettim; bunlar sahne/prefab authoring + tasarım kararı — headless yapamam.
> İlgili: [task7-prep.md](task7-prep.md) · [manuel-gorevler.md](manuel-gorevler.md)
> Bitince `[ ]` → `[x]` yap, bana söyle.

---

## 🔴 BULGU 1 (BLOCKER) — 16 perk'i sahneye gir

> **ÖNCE OKU — iki ayrı upgrade türü var, karıştırma:**
> - **Omurga (backbone):** `Storage` (raf/shelf alma), `Table` (masa alma), `Queue`, `Money`, `Stamina`, `Truck`, `Water`, `Customer`, `Quest Tier` → **sahnede zaten var** (9 girdi, `kind = LeveledBackbone`), eski `switch` ile çalışıyor. **Task 7 bunlara DOKUNMAZ, tekrar eklemene gerek yok.** "Ek shelf/table alma" bunlar; perk değil, o yüzden aşağıdaki tabloda yoklar.
> - **Perk (roguelite):** Aşağıdaki 16 yeni girdi (`kind = Perk`). Omurganın *üstüne* ekleniyor.
> - **Sonuç:** liste = 9 mevcut omurga **+** 16 yeni perk = **25 girdi**. Sen sadece 16 yeni perki ekliyorsun.

**Sorun:** `PerkEffect.cs` kodu doğru ama sahnedeki `UpgradePanel.upgrades` listesi hâlâ sadece eski 9 omurga girdisini içeriyor. `effectId` boş olduğu için 16 perk **hiç tetiklenmiyor** (ölü kod). Bu kapanmadan hiçbir perk Play-mode'da test edilemez.

**Yapılacak:** Unity'de `The Main Office.unity` sahnesinde `UpgradePanel` objesini seç → Inspector'da `Upgrades` listesine (mevcut 9 girdinin altına) aşağıdaki 16 girdiyi ekle.

> ⚠️ Kritik alan: **`effectId`** birebir aşağıdaki gibi yazılmalı (kod bu string'e bakıyor, yanlış yazım = perk çalışmaz).

**16 perkin HEPSİ için sabit olan alanlar (tek tek yazmana gerek yok, hepsinde aynı):**
- **`kind = Perk`** (16'sının hepsi)
- **`requiresQuestSystem` = işaretsiz (false)** — hiçbiri görev sistemine bağlı değil
- **`levelObjects` = boş** — hepsi saf mantık, sahnede fiziksel obje belirmez
- **`garageDoorControllers` = boş** — sadece "Truck" omurgasına özel

| # | Display Name | effectId | tier | maxLevel | baseCost | costStep |
|---|---|---|---|---|---|---|
| 1 | Ucuz Kira | `cheap_rent` | T3 | 3 | 130 | 30 |
| 2 | Prestij Simsarı | `prestige_broker` | T3 | 2 | 510 | -5 |
| 3 | Prestij Ustası | `prestige_master` | T2 | 2 | 280 | 100 |
| 4 | Hızlı Hangar | `fast_hangar` | T2 | 1 | 280 | 0 |
| 5 | Enerjik Ekip | `energetic_crew` | T1 | 1 | 160 | 0 |
| 6 | Çevik Ekip | `agile_crew` | T1 | 1 | 180 | 0 |
| 7 | Sabırlı Müşteriler | `patient_customers` | T1 | 1 | 220 | 0 |
| 8 | Uzun Kuyruk | `long_queue` | T1 | 1 | 240 | 0 |
| 9 | Kumarbaz Kasası | `gambler_case` | T2 | 1 | 220 | 0 |
| 10 | Telefon Hattı | `phone_line` | T1 | 1 | 160 | 0 |
| 11 | Mesai Saati | `overtime` | T1 | 1 | 200 | 0 |
| 12 | Kaldıraçlı Kira | `leveraged_rent` | T3 | 1 | 350 | 0 |
| 13 | Yüksek Volatilite | `high_volatility` | T2 | 1 | 300 | 0 |
| 14 | Acil Fren | `emergency_brake` | T2 | 1 | 250 | 0 |
| 15 | Kelle Koltukta | `all_in` | T3 | 1 | 800 | 0 |
| 16 | Toplu Alım | `bulk_buy` | T1 | 1 | 150 | 0 |

> Notlar: `maxLevel=1` olanlar "relic" (tek-seferlik), `costStep` yok sayılır → 0 bırak. `prestige_broker` iki seviyeli, 2. seviye ucuzluyor (510→505), o yüzden `costStep = -5`.

### Açıklama metinleri (`contentText`) — kopyala-yapıştır

| # | Perk | contentText |
|---|---|---|
| 1 | Ucuz Kira | Kira artış oranını her seviyede düşürür (kira daha yavaş büyür). |
| 2 | Prestij Simsarı | Her prestij kademesinden kazandığın bonusu artırır. |
| 3 | Prestij Ustası | Hizmet verdiğin her müşteriden kazanılan prestiji artırır. |
| 4 | Hızlı Hangar | Tırın hangarda kalma süresi %30 uzar — yüklemeye daha çok zaman. |
| 5 | Enerjik Ekip | Stamina yenilenme hızın belirgin şekilde artar. |
| 6 | Çevik Ekip | Hareket hızın %15 artar. |
| 7 | Sabırlı Müşteriler | Müşteriler %25 daha uzun süre sabırla bekler. |
| 8 | Uzun Kuyruk | Müşteri kuyruğu kapasitesi +2 artar. |
| 9 | Kumarbaz Kasası | Kutu ödülü %30 artar — ama hata cezası da %55 artar. |
| 10 | Telefon Hattı | Saatte yapabileceğin telefon siparişi sayısı +1 artar. |
| 11 | Mesai Saati | Çalışma günü biraz daha uzun sürer — daha çok teslimat şansı. |
| 12 | Kaldıraçlı Kira | Kira %20 düşer — bedeli: müşteri kaybının prestij cezası 2 katına çıkar. |
| 13 | Yüksek Volatilite | Her teslimat ödülü ±%35 rastgele oynar; uzun vadede ortalama +%15 kâr. |
| 14 | Acil Fren | İflası bir kez önler: o günkü gelir 0'a düşer, −5 prestij, hak biter. |
| 15 | Kelle Koltukta | Gelir %25 artar — bedeli: ödeme erteleme (grace) hakkın iptal olur. |
| 16 | Toplu Alım | Bir sonraki teklifteki rastgele 1 kartın fiyatı %50 iner. |

### Referans — mevcut 9 omurga (SEN EKLEMİYORSUN, sahnede zaten var)

Bunları tekrar eklemene gerek yok; sadece tier/kind düzenlemek istersen diye mevcut değerleri burada. Şu an hepsi `kind = LeveledBackbone`, `tier = T1` (default, serialize edilmemiş), `effectId = ` **boş** → eski `switch` ile çalışıyorlar.

> ⚠️ **effectId'lerini DOLDURMA.** Bu 9 girdinin `effectId`'si boş KALMALI — dolu olursa kod eski `switch` yerine `PerkEffect`'e gider ve o effectId'ler `PerkEffect`'te tanımlı olmadığı için `[PerkEffect] Bilinmeyen effectId` uyarısıyla omurga **çalışmaz**. Omurga = boş effectId, kural bu.
>
> ⚠️ **tier omurga için yok sayılıyor** (`DraftPool.cs:27` — `LeveledBackbone` her gün uygun). Tier'ı değiştirmen draft davranışını değiştirmez; kozmetik.

| Display Name | Ne yapıyor | kind | tier | maxLevel | baseCost | costStep | effectId |
|---|---|---|---|---|---|---|---|
| Storage | +1 raf (shelf) | LeveledBackbone | T1 | 9 | 50 | 10 | *(boş)* |
| Table | +1 masa | LeveledBackbone | T1 | 3 | 100 | 150 | *(boş)* |
| Queue | +1 müşteri kuyruğu | LeveledBackbone | T1 | 3 | 250 | 100 | *(boş)* |
| Money | Kutu ödülü +10 | LeveledBackbone | T1 | 3 | 300 | 100 | *(boş)* |
| Stamina | Stamina şarjı +0.5 | LeveledBackbone | T1 | 3 | 100 | 75 | *(boş)* |
| Truck | Depo kapısı açar | LeveledBackbone | T1 | 2 | 200 | 100 | *(boş)* |
| Water | Su sebili | LeveledBackbone | T1 | 1 | 500 | 200 | *(boş)* |
| Customer | Müşteri bekleme süresi ↑ | LeveledBackbone | T1 | 3 | 300 | 200 | *(boş)* |
| Quest Tier | Zor görev + iyi ödül | LeveledBackbone | T1 | 2 | 200 | 150 | *(boş)* |

> **`requiresQuestSystem` hakkında (2. sorunun cevabı):** Bu kutu **yalnızca `Quest Tier`** için anlamlı — ve sadece görev sistemi sonradan açılan bir mekanikse işaretlemelisin. Şu an sahnede hepsi işaretsiz (false) ve oyun çalışıyor; oyunda görev sistemi baştan aktifse **`Quest Tier` dahil hiçbirini işaretleme.** 16 perkin hiçbirinde işaretlenmez. Emin değilsen: **hepsini işaretsiz bırak** (mevcut davranış).

> Not: Eğer bu omurgalardan birini "roguelite perk" gibi draft/tier davranışına sokmak istiyorsan (örn. Storage'ı T2'de kilitlemek) bu **tasarım değişikliği** — bana söyle, kod tarafını (backbone'u draft'tan çıkarma kuralı) buna göre ayarlamak gerekir.

- [ ] 16 girdi eklendi, her birinde `kind = Perk` + doğru `effectId`
- [ ] Play'e al, bir perk satın al → Console'da `[PerkEffect] Bilinmeyen effectId` **uyarısı çıkmamalı** (çıkarsa o satırda effectId yazımı hatalı)

---

## 🟡 BULGU 2 (TASARIM KARARI) — `gambler_case` + `all_in` çakışması

**Sorun:** İkisi de `Truck.rewardPerBox`'ı baz değer üzerinden **mutlak** yazıyor. Oyuncu ikisini birden alırsa son alınan kazanır, diğerinin etkisi kaybolur (stacking yok). Aralarında dışlama (mutual-exclusion) mekanizması da yok.

**Senden istediğim:** Hangi davranışı istiyorsun? Kod tarafını ben ona göre düzelteceğim:
- [ ] **(a) Dışlama** — biri alınınca diğeri draft'ta çıkmasın (temiz, önerilen).
- [ ] **(b) Compound** — ikisi de alınırsa çarpanlar çarpılsın (1.30 × 1.25 gibi). Ekonomik denge etkisi var → economist'e danışırım.
- [ ] **(c) Şimdilik böyle kalsın** — nadir kcombo, sonra bakarız.

*(Karar ver → bana söyle, gerisini ben yaparım. Editor işi yok.)*

---

## 🟡 BULGU 3 (DOĞRULAMA) — 3 `economySettings` referansı aynı asset mi?

**Sorun:** `Truck`, `UpgradePanel`, `DayCycleManager` üçü de ayrı `economySettings` (SerializeField) tutuyor. Biri Inspector'da farklı/eski bir SO kopyasına bağlıysa, perk o objeyi değiştirir ama oyun başka instance'tan okur → **perk parayı düşürür, etki görünmez.**

**Yapılacak:** Unity'de üç objeyi de seç, Inspector'daki `Economy Settings` alanının **aynı** asset'e (`Assets/Resources/EkonomiAyarlari.asset`) işaret ettiğini doğrula. (Ya da üçünü de boş bırak → kod aynı `Resources.Load`'a düşer.)

- [ ] `Truck` (prefab) → `economySettings` = `EkonomiAyarlari`
- [ ] `UpgradePanel` → `economySettings` = `EkonomiAyarlari`
- [ ] `DayCycleManager` → `economySettings` = `EkonomiAyarlari`
- [ ] Üçü de aynı asset (veya üçü de boş)

---

## Özet — sende ne var
1. **Editor authoring:** 16 perk girdisi (Bulgu 1) — asıl iş, bu bitmeden test yok.
2. **Tek karar:** gambler/all-in davranışı (Bulgu 2) — bana a/b/c söyle.
3. **Göz teyidi:** 3 economySettings referansı aynı mı (Bulgu 3).

Bunlar bitince: perk Play-mode teyidi → qa re-run → kontrol kapısı → commit.
