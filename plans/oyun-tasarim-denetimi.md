# 🔍 Oyun Tasarım Denetimi + Salt-Okunur Bug Avı

**Tarih:** 2026-09-18
**Bütçe:** ~$29.5 Claude kredisi (yarın doluyor)
**Dil kuralı:** Ajan promptları **İngilizce**. Kullanıcıya çıkan sentez raporu **Türkçe**.
**Kod kuralı:** Bu iş kapsamında **HİÇBİR KOD DEĞİŞTİRİLMEZ.** Tüm ajanlar salt okunur. Çıktı = kanıtlı bulgu.

## Neden bu iş
Kullanıcının sorusu: "eğlenceli mi, ne gereksiz, oyun sabit döngüde mi, sesler, ekonomi".
Bu bir **tasarım** sorusu. Model oyunu oynayamaz — "eğlenceli mi"nin doğrudan cevabı playtest'ten çıkar.
Ama eğlencesizliğin *sebepleri* ölçülebilir: karar yoğunluğu, tekrar eğrisi, sonuç determinizmi, geri bildirim boşlukları.

**Toplu otomatik bug DÜZELTME bilerek kapsam dışı.** Gerekçe: `626a1df` fix'i `508d7bd` regresyonunu doğurdu;
projede sıfır PlayMode testi var (132 EditMode testinin hepsi saf matematik yardımcısı), yani otomatik
düzeltmelerin arkasında güvenlik ağı yok. Düzeltme kararı bulgular okunduktan sonra kullanıcıda.

## Kaynaklar
- `GDD.md` — 2967 satır, 38 bölüm (ajanlara sadece ilgili bölüm numarası verilir, tamamı değil)
- `tools/economy-sim/sim.js` — 1257 satır, çalışan ekonomi simülatörü (v5.1)
- `Assets/NewCss/` + `Assets/Scripts/` — 180 dosya / 63K satır
- `Assets/LocalSettings/Tables/` — 17 dil × 336 girdi

---

## TRACK A — Tasarım Denetimi (~$22)

### A1 — Sonuç determinizmi / pacing  (economist, fable)
**Soru:** Oyun kaçıncı günde belli oluyor?
Monte Carlo koşuları üzerinden gün-5 durumu (para/prestij/kota) ile nihai sonuç arasındaki korelasyon.
Yüksek korelasyon = kalan 11 gün gerilimsiz = "sabit döngü" hissinin matematiksel kaynağı.
Ayrıca: sonuç varyansı, iflas günlerinin dağılımı, comeback mümkün mü.
**Kaynak:** `tools/economy-sim/sim.js`, GDD §4, §5, §7, §31.

### A2 — Tekrar eğrisi / gün-gün çeşitlilik  (assistant, fable)
**Soru:** Gün 3 ile gün 11 arasında mekanik olarak ne değişiyor?
16 günü tek tabloya çıkar: o gün ne açılıyor, hangi event, hangi quest, hangi perk draft'ı, kota/kira nerede.
Çıktı: mekanik olarak birbirinin kopyası olan günler.
**Kaynak:** GDD §3, §15, §16, §37; `Assets/NewCss/UIScripts/DayCycleManager.cs`,
`Assets/NewCss/Events/EventCalendarUI.cs`, `Assets/Scripts/Quest/` (30 asset), `Assets/NewCss/Roguelite/`.
**Bilinen işaret:** `EventType` enum'ı yalnızca `{Positive, Negative, Neutral}` — çeşitlilik motoru olması
gereken sistemin taksonomisi üç kelime.

### A3 — Mekanik envanteri + karar yoğunluğu  (gameplay, fable) — en pahalı eksen
**Soru:** Hangi sistem oyuncuya gerçekten karar verdiriyor, hangisi ölü?
Her sistem için: oyuncu günde kaç kez etkileşiyor, hangi kararı doğuruyor, tetiklenme koşulu ne kadar dar,
kodda var ama pratikte görünmeyen ne var.
**Kaynak:** `Assets/NewCss/` tamamı + `Assets/Scripts/Quest/`. GDD §2, §10-14, §18, §19.

### A4 — Duyusal geri bildirim kapsamı  (graphics-ui, sonnet)
**Soru:** Hangi eylemin ses/görsel karşılığı yok?
**Bilinen işaret:** `SfxLibrary` 10 giriş, `MoneyEarned` ve `CorrectItem` **boş** — oyunun en ödüllendirici
iki anı sessiz. Ayrıca 53 pre-existing boş-slot bulgusu (2026-09-14 taraması) hiç çözülmedi.
**Kaynak:** `Assets/NewCss/Audio/`, `SfxLibrary.asset`, GDD §27, §26.

### A5 — Tür karşılaştırması  (general-purpose, sonnet)
PlateUp / Overcooked / Supermarket Simulator eşdeğer noktada ne yapıyor.
Cargor nerede ayrışıyor (iyi), nerede sadece eksik (kötü). Web araması serbest.
**Kaynak:** GDD §1, §2 + web.

### A6 — Sentez  (fable, effort max) → **TÜRKÇE RAPOR**
A1-A5 + B çıktılarını tek rapora indirger. Önceliklendirilmiş, kanıtlı, kesme önerileri dahil.
Övgü raporu değil — "şunu kes, şunu ekle, şu zaten iyi".
Çıktı: `docs/playtest/tasarim-denetimi-2026-09-18.md`

---

## TRACK B — Salt-Okunur Bug Avı (~$7)

### B1 — Netcode / late-join  (qa, fable)
En riskli alt sistem: FAZ 1'de 21 bug'ın çıktığı yer.
IsServer guard'ı eksik RPC, late-join deserialize, NetworkVariable yarışları, host-migration boşlukları.
**KOD DEĞİŞTİRME.** Çıktı: dosya:satır + kanıt + önem sırası.
**Kaynak:** `Assets/NewCss/` NetworkBehaviour türevleri, `SteamManager.cs` (~1800 satır — grep + hedefli
satır oku, tüm dosyayı Read'leme), `LobbyManager`, GDD §23.

---

## Ajan brief disiplini (CLAUDE.md §7)
- Dosya yolu + fonksiyon/satır aralığı ver, içerik yapıştırma.
- GDD'nin tamamını değil, bölüm numarasını söyle.
- "Kısa, kanıta dayalı verdict ver; uzun rapor yazma."
- Büyük dosyalarda grep + hedefli okuma iste.
- **Promptlar İngilizce.**

## Faturalandırma notu (çözüldü)
Kredi **claude.ai usage credits** bakiyesi — Console API kredisi DEĞİL.
`ANTHROPIC_API_KEY` gerekmiyor; oturum zaten doğru hesapla kimliklendirilmiş.
Abonelik limiti önce, usage credits sonra devreye girer → kredinin erimesi için ağır kullanım gerekir.
Bakiye: claude.ai → Settings → Usage.

## Çıktı yeri
Her ajan bulgusunu `docs/playtest/denetim-2026-09-18/<id>.md` dosyasına yazar.
A6 sentezi bu dosyaları okur → `docs/playtest/tasarim-denetimi-2026-09-18.md` (TÜRKÇE).
