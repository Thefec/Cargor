---
name: prestige-cap-bug-and-fix
description: PrestigeManager'da gercek maxPrestige clamp'i hic yok (kod bug'i, GDD 100 diyor) - 150'ye cikan yeni clamp eklenmesi karari + 16-gun etkisi
metadata:
  type: project
---

> ✅ **UYGULANDI + DOĞRULANDI (2026-07-18):** `PrestigeManager.cs:19` `maxPrestige=150f` ve satır 167
> `Mathf.Clamp(newPrestige, 0f, maxPrestige)` artık GERÇEK/CANLI kodda var (bu kayıttaki karar
> uygulanmış). AMA 2026-07-18 turunda (`plans/economy-audit-2026-07-17.md` §2) tavan-dolma günleri
> BURADAKİ tablodan FARKLI çıktı: **4P gün8, 3P gün9, 2P gün11, 1P gün14** (aşağıdaki tablo: 4P gün15,
> 3P/2P hiç dolmuyor). Fark bug değil, **metodoloji düzeltmesi**: bu kayıttaki sim (ve 2026-07-13
> denetimi) `customerServedPrestigeBonus`'u `correctDeliveries` (tır-teslim sayısı, tır-kapasitesine
> tabi) üzerinden veriyordu. 2026-07-18'de gerçek kod okunarak (`Truck.cs`'de bu bonusa hiç referans
> yok — CustomerAI akışından geliyor) prestij artık tır tavanından BAĞIMSIZ `demandAdjusted`
> (müşteri-servis) üzerinden veriliyor → daha hızlı büyüyor → tavana daha erken çarpıyor. Yeni
> analiz "2P/3P/4P hedeften (gün~14) erken tavana çarpıyor" sonucuna vardı — 2026-07-18 turunda
> DEĞİŞTİRİLMEDİ (yalnız modellendi/raporlandı), ayrı bir takip turu önerildi. Detay:
> [[truck_hangar_window_cap]], `plans/economy-audit-2026-07-17.md` §2.

2026-07-14 bulgusu + karar. `economy-audit-2026-07-13.md` §1'in "3-4P'de prestij gün 9-13'te 100 tavanına donuyor" bulgusu **yanlış kod yoluna dayanıyordu**: o rapor Node.js sim'i `GameEconomySettings.RunSimulation()`'ı (editör-only debug context-menu aracı, `GameEconomySettings.cs:216` `prestige = Mathf.Clamp(prestige, 0f, 100f)`) birebir taklit etmişti. **Gerçek networked yol** (`PrestigeManager.ModifyPrestigeServerRpc`, satır 149-161) prestiji **hiç kırpmıyor** — sadece `<=0` kontrolüyle oyunu bitiriyor, üst sınır YOK. Bu daha önce [[roguelite_perk_pricing]] hafızasında da not edilmişti (Prestij Simsarı fiyatlaması bağlamında) ama bugüne kadar "prestij tavanı" pacing kararına bu çelişki taşınmamıştı.

Yani şu an CANLI OYUNDA: `GDD.md:338`'in belgelediği "Maksimum prestij: 100" **hiçbir zaman uygulanmamış bir tasarım niyeti**, gerçek kod bunu hiç zorlamıyor → 3-4P grupları teorik olarak prestiji sınırsız büyütüp `Truck.cs:626-627`'deki kutu-başı ödül tier'ını (`prestigePerBonus=10, bonusPerTier=5`) sınırsız şişirebilir. Bu, "donma" değil **sınırsız geç-oyun enflasyonu** riski — önceki teşhisten daha ciddi bir bug.

**Karar: Seçenek (a) — gerçek clamp EKLE, tavanı 150 yap.**
- `PrestigeManager.cs`'e YENİ alan: `public float maxPrestige = 150f;`
- `ModifyPrestigeServerRpc` içine clamp ekle: `currentPrestige.Value = Mathf.Clamp(currentPrestige.Value + amount, 0f, maxPrestige);`
- `prestigePerBonus=10`, `bonusPerTier=5` DEĞİŞMEDİ (tier granülaritesi aynı kalıyor, sadece tavan yükseliyor → max tier 10→15, max bonus +50→+75 TL/kutu)

**Neden (b) değil (tier eşiği 10→15)**: node simülasyonu (aynı model, `GameEconomySettings.RunSimulation()` mantığı) gösterdi ki eşiği 15 yapmak donma gününü HİÇ ertelemiyor (3P/4P yine gün 12/11'de donuyor, çünkü ham prestij-puanı büyümesi eşikten etkilenmiyor, sadece o puanın kaç tier'e böldüğü değişiyor) — üstelik toplam ödül tavanını düşürerek (3P son kasa 2553→1608 TL, -%37) sorunu çözmeden ekonomiyi kötüleştiriyor. Kesin reddedildi.

**Neden (c) şimdi değil (harcanabilir itibar/VIP sipariş)**: iyi bir uzun-vadeli fikir ama "sayı ayarı" değil, YENİ bir mekanik (harcama etkileşimi + UI + yeni ödül yolu) — kapsamı "dar ekonomik değer kararı" isteğini aşıyor. Roguelite T3 perk adayı olarak parkedildi (örn. "VIP Sipariş: prestij harca → büyük tek seferlik ödül"), gelecekte [[roguelite_perk_pricing]] tarzı bir EV hesabıyla fiyatlanabilir.

**16-gün node simülasyonu sonuçları** (Normal senaryo, 2.0 kutu/dk/oyuncu, aynı model economy-audit'in kullandığıyla):

| P | cap=100 (mevcut/bug'lı davranışın sim eşleniği) donma günü | cap=150 donma günü | cap=150 son prestij | cap=150 son kasa (cap=100'e göre) |
|---|---|---|---|---|
| 1P | hiç (64.0) | hiç (64.0) | 64.0 | değişim yok |
| 2P | gün 15 | hiç dolmuyor | 108.1 | değişim yok (108<150) |
| 3P | gün 12 | hiç dolmuyor | 146.5 (tavana çok yakın, gün16'da doğal doluyor) | 2553→3056 TL (+%19.7) |
| 4P | gün 11 | gün 15 (son güne çok yakın) | 150.0 | 3474→4714 TL (+%35.7) |

1P/2P tamamen etkilenmiyor (zaten eski tavanın altındaydı) — [[prestige_fragility]]'deki başlangıç=15/ceza=-1.5/lossToZero=10 alt-sınır mekaniği **hiç dokunulmadı**, sadece tavan değişti. 3P kasa artışı sağlıklı bir "iyi oynayan takıma ödül", 4P zaten pozitif bitişi güçlendiriyor — iflas riski yaratmıyor.

**Why:** Kullanıcı 3-4P'nin ikinci yarısında prestij ekseninin "ölmesi" sorununu 1P kırılganlığını bozmadan çözmek istedi; kod okurken asıl kök nedenin (eksik clamp = tasarım-kod uyumsuzluğu) audit'teki teşhisten daha derin olduğu ortaya çıktı.

**How to apply:** Bu clamp gameplay tarafından eklenirken UI tarafında prestij barının/göstergesinin 0-100 sabit varsayımı olup olmadığı kontrol edilmeli (ilk taramada böyle bir hardcode bulunamadı, ama gameplay implementasyon sırasında teyit etmeli).

İlişkili: [[prestige_fragility]], [[roguelite_perk_pricing]]
