---
name: economy-full-balance-round9-2026-08-30
description: Round 9 — GDD.md ekonomi bölümlerinin canlı kodla senkronu; 14 bölümde 26 düzeltme, en ağırı gün süresi 160→200s, §14 telefon V3→V4, §7 kota "silindi"→"yeni sistem", §9.4 sabır 35-55s ölü kablo
metadata:
  type: project
---

# Round 9 — GDD.md ↔ CANLI KOD senkronu (2026-08-30, Opus)

**Kod DEĞİŞMEDİ** (`.cs` / `.asset` / sahne / `sim.js` hiçbiri okundu, hiçbiri yazılmadı).
Yalnız `GDD.md` düzenlendi. Round 1-8'in **önerdiği ama uygulanmamış** değerler (kira
`{290,650,1140,1630}`, `timeSkipAmountByPlayerCount={115,49,47,47}`, quest prestij ×0.4,
`DAILY_QUEST_COUNT=3+tier`) **bilinçli olarak GDD'ye YAZILMADI** — yalnız "önerildi, henüz
uygulanmadı" notu olarak işaretlendi (müdür çerçeve kararı).

**Why:** GDD, oyuncu/geliştirici için tek tasarım referansı; 2026-08-29'daki PlateUp kota +
Telefon V4 geçişi ve 2026-08-30 ölçümleri hiç yansımamıştı. Doküman okuyanı yanlış modele
sokuyordu (özellikle "telefon çalar", "kota silindi", "müşteri kapasiteye bağlı").

**How to apply:** Round 10 (toplu uygulama turu) bittikten SONRA GDD tekrar senkronlanmalı —
her "önerildi ama uygulanmadı" bloğu ya güncel değere çevrilecek ya kaldırılacak. İlgili
uyarı kutuları `[!NOTE]`/`[!CAUTION]` ile işaretli, grep'lenebilir: "HENÜZ UYGULANMADI",
"henüz uygulanmadı", "Round 10".

---

## 1. En ağır 5 sapma (hepsi dosya:satır ile doğrulandı)

| # | Bölüm | GDD diyordu | CANLI kod | Kanıt |
|---|-------|-------------|-----------|-------|
| 1 | §3.1/3.2, §2.2 | Baz gün süresi **160s**, gün 16 = 290s | **200s**, gün 16 = **330s** | `DayCycleManager.cs:52` + sahne `The Main Office.unity:19524` (`realDurationInSeconds: 200`), `dailyDurationIncrease: 10`, `DYNAMIC_DURATION_START_DAY=3` (cs:37) |
| 2 | §14 (tamamı) | Telefon **V3 REAKTİF**: sunucu zar atar, telefon çalar (P bazlı %20-35), oyuncu açar, 15sn zil | **V4 DIŞARI ARAMA**: telefon çalmıyor; oyuncu E'yi 1sn basılı tutup müşteri çağırıyor, bedel `SkipTime` | `PhoneCallManager.cs` tamamı (V3 alanları sınıftan silinmiş); `GameEconomySettings.cs:114-132` |
| 3 | §7 | "Kota sistemi TAMAMEN silindi, kodda yok" | Eski **kutu** kotası silindi ✅ ama **yeni müşteri kotası** var (PlateUp, gün-numarası tablosu) | `GameEconomySettings.cs:44-53` + `CustomerManager.CalculateTodaysCustomerCount` (cs:403-419) |
| 4 | §9.4, §19.1 | Müşteri sabrı **35-55s**, oyuncu başına −2s | **15-20s**, P'ye göre HİÇ ölçeklenmiyor | `Customer.prefab:2323-2324` (`minWaitTime:15`, `maxWaitTime:20`); `ScaledMinPatience`/`ScaledMaxPatience`'in `DifficultyManager.cs` dışında **0 tüketicisi** (grep) |
| 5 | §9.6 | Müşteri sayısı = `(raf×3)+(seviye×2)+Random(-2,+3)`, clamp 1-50 | Formül **koddan silindi**; kapasite etkisi sıfır | `CustomerManager.cs:398-419` docstring + gövde |

## 2. Tam düzeltme listesi (26 kalem, 14 bölüm)

**Başlık/meta**
1. "Son Güncelleme 7 Ağustos" → 30 Ağustos; "165 kontrol" → **77 `Expect*` iddiası** (gerçek sayım: `grep -c "r\.Expect"` = 77).
2. Yeni `[!CAUTION]` kutusu: "Bu belge CANLI kodu anlatır, Round 1-8'in önerileri buraya yazılmadı."
3. TOC madde 7 başlığı düzeltildi.

**§1.4 USP** — "Kapasite-bazlı dinamik müşteri sistemi" → "PlateUp tarzı günlük müşteri kotası" (eski ifade **tam tersini** söylüyordu).

**§2.2 Mikro döngü** — tek 160s kolonu → gün 1-3 (200s) / gün 16 (330s) çift kolon; **17:30 `CUSTOMER_EXIT_HOUR` satırı eklendi** (iki ayrı ceza kanalıyla); erken-gün-bitişi (`dayEndGraceSeconds=30`) notu eklendi.

**§2.3** — "Telefon Aç" → "Telefonla Çağır (E'yi basılı tut)".

**§3** — süre tablosu; `realDurationInSeconds`'a doğrudan yazma yasağı (`RecomputeDayDuration` tek yazıcı); **`SkipTime`'ın TABAN 200s kullandığı** uyarısı (§14.4'e link); dosya yolu düzeltildi (`GameState/` → `UIScripts/`).

**§3.4 OnNewDay hub** — `PhoneCallManager.HandleNewDay` (cs:313) eklendi; `EventEffectManager` ↔ `CustomerManager` aynı statik event'e abone / sıra deterministik değil uyarısı (Round 5 §7).

**§4.1** — kutu ödülü `+50` → **P-bazlı {50,55,70,88}**; FESTIVAL DAY geliri (kira×%10-20) eklendi; "para YALNIZ tırdan gelir / müşteri 0 para verir" kutusu; kutu düşme eşiği 1 m/s → **3 m/s**; `penaltyPerBox`'ın P-bağımsız olduğu vurgulandı.

**§4.2 SO ağacı (en büyük tek düzenleme)**
- **MÜŞTERİ KOTASI bloğu eklendi** (4 eğri + 16-gün toplamları 77/151/160/160 + `customerArrivalIntervalByPlayerCount` + `dayEndGraceSeconds`).
- `rewardPerBoxByPlayerCount` eklendi, `rewardPerBox`'ın legacy fallback olduğu belirtildi.
- **TELEFON bloğu V3 → V4** (4 ölü alan çıkarıldı, 5 canlı alan eklendi).
- `customerMissedQuotaPrestigePenalty: -0.2` eklendi.
- `festivalBonusMin/Max`'ın artık yalnız **fallback** olduğu işaretlendi.
- **YENİ uyarı kutusu**: `EkonomiAyarlari.asset` bu alanların çoğunu içermiyor → `.cs` default'ları canlı; asset'te ölü V3 anahtarları duruyor; `float[]`'a hex yazma tuzağı.
- PerkEffect'in yazdığı 7 alan listesi düzeltildi: `phoneRingPerkBonus` (ölü) → **`phoneCooldownPerkBonusSeconds`** (`PerkEffect.cs:301`), her alana satır no eklendi, "mutlak atama" notu.

**§4.3** — tier tablosu P-bazlı 4 kolona genişletildi (tavan tier 12: 110/115/130/148 TL); "prestij = gizli gelir çarpanı" (tır gelirinin %13-35'i, marjinal değer gün 1'de 34.7-91.7 TL) kutusu; "maxPrestige hiç ulaşılmıyor" → **%75-92 doluyor, quest'le 6/12 hücrede tavan çarpılıyor**.

**§5** — Slow/strict'in 16/16 iflası + "%25-30 SEVİYE açığı" + önerilen (uygulanmamış) kira kutusu; grace'in **açığa değil nakde** oranlı olduğu + "fakir kal" exploiti (+0.20 × kira).

**§6.2/6.4** — `customerMissedQuotaPrestigePenalty` satırı; "servis para vermez" notu; SURPRISE AUDIT'in dekoratifliği (%0.1-4.6); **"dump the customer" exploiti** (`CustomerAI.cs:1228-1259`, iade modu, −0.08 vs −0.4); prestij fail-state'inin 16/16 hücrede ölü olduğu + P-asimetrisi (4P gün 7 / 1P gün 13).

**§7 (yeniden yazıldı)** — 3 alt bölüm: 7.1 kota tablosu, 7.2 ekonomik anlam + "kota çoğu bantta bağlayıcı değil / yukarı yönlü çarpan ölü", 7.3 gün sonu muhasebesi (−0.4 vs −0.2 iki kanal + erken bitişte missed-quota hiç tetiklenmez).

**§8.2/8.3** — mermaid'de "120s" → P-bazlı, "+50 TL" → `rewardPerBox[P]`; **prestij örneği `floor(24/4)` → `floor(24/8)`** (80 TL yanlıştı, doğrusu 65 TL); ceza/ödül oranının P ile eridiği notu (1P 0.62 → 4P 0.39).

**§9.3/9.4/9.5/9.6** — mermaid +0.5/−1.5 → **+0.4/−0.4**; sabır 15-20s + DifficultyManager ölü kablo + sabır sayacının kuyruğa varışta başlaması + RELAXED DAY'in Normal bantta sıfır olması; kuyruk boyutu **2** (`DEFAULT_QUEUE_SIZE`) ve Uzun Kuyruk perkinin 4 yaptığı; §9.6 kapasite formülü → **kota + varış aralığı iki kolu** (jitter, dalga çarpanı, formül).

**§10.2** — −10 TL/−0.05/1 m/s → **−5 TL / −0.04 / ≥3 m/s** + kırılma eşiğiyle hizalı olduğu notu.

**§13.2** — Round 4 ölçüm notu eklendi: 19 aktif / 6 `disabledInDraft`; Ek Hangar 9.21×–0× bant bağımlılığı; **`cheap_rent` STALE-BASELINE bug'ı** (`PerkEffect.cs:194`, 1.15 hardcode vs canlı 1.20 → ~2.7× güçlü); `leveraged_rent`'in Slow/strict'te P1/P2'yi kurtarması.

**§14 (tamamen yeniden yazıldı)** — 14.1 V4 parametre tablosu; cooldown/perk/event'in **fiilen etkisiz** olduğu (gerçek kapı `HasUnspawnedCustomers`/`IsQueueFull`) + CUSTOMER SUPPORT'un "pozitif tabelalı ama zararlı" olduğu; 14.2 yeni akış diyagramı (5 guard + **17:30 guard'ının gerekçesi** + server-authoritative dial); 14.3 bar'ın iki işlevi + bağlanmamış inspector alanı tuzağı; **14.4 YENİ**: `T[P]×0.30303` formülü, maliyet/aralık %79-81 tablosu, gün 1 vs gün 16 oyun-dakikası tablosu, "%10-25 kullan / %100 spam yapma" rehberi.

**§15.2 (yeniden yazıldı)** — "17 Etkinlik" → **16**, 8 pozitif + 8 negatif; nesir açıklamalar → `EventEffectManager.cs:130-360` ile birebir çarpan matrisi. Düzeltilen değerler: BUSY DAY +%50→**×1.35 (+ sabır ×0.85)**; ANGRY CUSTOMERS −%30→**×0.60 (+ kota ×1.10)**; RELAXED DAY'den **−%30 müşteri kaldırıldı** (FAZ4'te silinmişti, tablo hâlâ gösteriyordu); GOLDEN BOX DAY +%30/+%20/+%20 → **×1.15/×1.15/×0.80 (+ stamina ×0.80)**; VIP SERVICE "%10 mükemmel kutu şansı" → **×1.12**; HEAVY BOXES −%20 → **×0.85/×0.80**; FATIGUE PROBLEM'e **kota ×0.85** eklendi; SLOW LOGISTICS'e **ödül ×0.92**, EXPRESS CARGO'ya **×1.08** eklendi; **CUSTOMER SUPPORT "+%30 telefon çalması" → "cooldown ×0.5"** (V4'te çalma yok); FESTIVAL DAY "rastgele bonus" → **kira × %10-20**. Ayrıca Round 5'in 4 yapısal bulgusu + gün-15 yerleşim riski + `IsGoldenBoxDay`/`IsVIPServiceDay` ölü kod notu.

**§15.3** — mekanizma listesi 5 → 10 satır (kota çarpanı, sprint, telefon, ceza çarpanı eklendi) + perk/snapshot çakışma uyarısı.

**§16.2/16.4/16.5** — ödül tablosunun **tier-düz** olduğu + asset sayıları; quest prestijinin para ödülünün 1.7-1.9 katı **görünmez** değer olduğu; **"Görev Kademesi ödenmiş kötüleştirme"** kutusu; `AnswerPhone`'un tetikleyicisi (`PhoneCallManager.cs:469`) ve **§14.4'ün tersini öğrettiği**; `CompleteSpecificColorTruck`'ın D2 muafiyetinde olmadığı uyarısı.

**§19.1 (tablo yeniden yazıldı)** — ölü satırlar (müşteri 10/12/14/16, sabır 8-14s) kaldırıldı; canlı satırlar eklendi (kota, varış aralığı, kutu ödülü, telefon zaman bedeli); **`DifficultyManager` ölü kablo kutusu** (`ScaledCustomerCount`/`ScaledMinPatience`/`ScaledMaxPatience`/`ScaledStaminaRegenRate` = 0 tüketici; `ScaledStartingMoney` ise CANLI, `cs:455`).

**§21.1/21.2** — `CheckWinCondition` docstring'inin kodla çeliştiği (cs:691,706 vs 696-712) + `SetPrestige`'in `TriggerLose` kapısından geçmediği; kota'nın kaybetme koşulu olmadığı; prestij kaybının pratikte ölü olduğu.

**§31** — "sim v3.1" → **yalnız `runFullSim` v4.0 kullanın** + eski `runSim`/`runSimPlateUp`'ın sapması (−%76…+%540); `runFullSim`'in bilinen **3 model açığı** + modellenmeyenler; §31.2'de müşteri sayısı kota tablosuna bağlandı; §31.1'de telefon maliyeti "ölçülmedi" → ölçüldü; **§31.3 kapasite tablosu silindi**, yerine 16-hücre bant sağlığı + Slow/strict anatomisi + kota→para dönüşüm oranları.

## 3. GDD'ye BİLEREK yazılmayanlar

- Round 3 kirası, Round 7 `timeSkipAmountByPlayerCount`, Round 8 quest prestij ×0.4 /
  `DAILY_QUEST_COUNT` / fiyat muafiyeti → yalnız "önerildi, uygulanmadı" notu.
- Tasarım niyeti / gelecek plan bölümleri (§1.1-1.3, §1.5, §17, §18, §20, §22-§30) —
  ekonomiyle ilgisi olmayan yerlere dokunulmadı.
- `phone_line` perkinin sahne `contentText`'i bayat (V3 metni) — **sahne dosyası, kod kısıtı
  gereği değiştirilmedi**, Round 10'un listesinde zaten var.

## 4. Round 9'da ORTAYA ÇIKAN, önceki roundlarda olmayan bulgular

1. **§8.3'ün prestij örneği aritmetik olarak yanlıştı** — `floor(24/4)` kullanıyordu ama
   `prestigePerBonus = 8`. GDD 80 TL/kutu diyordu, doğrusu 65. FAZ4 senkronunda gözden kaçmış.
2. **Müşteri sabri hiçbir zaman 35-55s olmadı.** `DifficultyManager`'ın sabır alanları
   yalnız `GetDailyCustomerCount` benzeri bir log satırında görünüyor; gerçek değer
   `Customer.prefab`'ta 15-20s. Round 5 §5'in "RELAXED DAY Normal bantta sıfır" bulgusunun
   asıl sebebi de bu (sim'in 35-55s varsayımı değil, prefab'ın 15-20s'i).
3. **`EkonomiAyarlari.asset` yeni alanların HİÇBİRİNİ içermiyor** — kota dizileri,
   `rewardPerBoxByPlayerCount`, `customerArrivalIntervalByPlayerCount`, tüm V4 telefon
   alanları, `customerMissedQuotaPrestigePenalty` asset'te YOK. Canlı olan `.cs` default'ları.
   Round 10 bir değeri değiştirirken **`.cs` initializer'ı** düzenlemeli; asset'e anahtar
   eklemek `float[]` hex tuzağını açar. Asset'te ayrıca 3 ölü V3 anahtarı duruyor.
4. **`EconomyInvariantCheck.cs` "165 kontrol" iddiası yanlış** — gerçek sayı 77 `Expect*`
   çağrısı. (Dosya şu an uncommitted olarak PlateUp alanlarıyla güncellenmiş durumda.)
5. **`penaltyPerBox` P-bağımsız kalmış** — ödül dizisi P ile 50→88 büyürken ceza 40'ta sabit,
   yani yanlış teslimatın caydırıcılığı 4P'de 1P'nin %63'ü. Bugün risk değil (yanlış teslimat
   nadir) ama ödül dizisi tekrar büyütülürse denge kalemi.

## İlgili

[[economy_full_balance_round7_2026-08-30]] · [[economy_full_balance_round8_2026-08-30]] ·
[[economy_full_balance_round5_2026-08-30]] · [[economy_full_balance_round6_2026-08-30]] ·
[[dead_wiring_p_scaling]] · [[perk_card_absolute_assignment_conflict]] ·
[[unity-yaml-float-array-trap]]
