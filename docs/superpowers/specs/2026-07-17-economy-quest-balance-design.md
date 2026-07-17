# Ekonomi Doğrulama + Quest Ödül Dengesi (Birleşik) — Tasarım (2026-07-17)

> Durum: kapsam onaylandı (kullanıcı "birleştir" + "denge odaklı"), spec onayı bekliyor.
> Dal: `feature/economy-quest-balance` (main'den, quest onarımı `3c82189` dahil).
> Selefler: ekonomi-doğrulama spec `a522ed7` (dal `feature/economy-verification` — TERK, içeriği buraya taşındı) +
> quest onarımı `docs/.../2026-07-17-quest-system-repair-design.md` (backlog: easy3 orantısız).

## 1. Amaç ve birleştirme gerekçesi

İki iş kolu tek turda: (a) **ekonomi doğrulama** — 2026-07-13 denetiminden bu yana değişen değerlerle
16-gün nakit akışı sim'ini yeniden koştur, dengeyi teyit et; (b) **quest ödül dengesi** — aktif
quest'lerin (easy1-3) ödül/ceza değerlerini çekirdek akışa göre hizala, özellikle easy3 orantısızlığı
(100-200 TL vs diğerleri 8-30 TL).

**Neden birlikte:** Quest ödülü de para/prestij girişidir → çekirdek nakit akışının parçası. Ayrı
sim'lerde bakmak yanıltıcı olur; quest geliri çekirdek dengeyi kaydırabilir (easy3 tek başına bir
günün gelirini katlayabilir). Tek sim ikisini birlikte görür.

**Tetikleyici:** Somut oyun-hissi şikâyeti değil — regresyon/tutarlılık doğrulaması. Değerler ihlal
göstermezse değişmez; easy3 zaten bilinen bir ihlal.

## 2. Kapsam

### İÇERİDE
- Çekirdek nakit akışı: kira / gelir / ceza / prestij, 16-gün 1P-4P (Normal + Yavaş)
- **Aktif quest'lerin (easy1, easy2, easy3) ödül/ceza dengesi** — sim'e günlük quest geliri olarak kat
- easy3 orantısızlığını düzelt (economist değeri), gerekirse easy1/easy2'yi de hizala

### DIŞARIDA (bilinçli)
- **Quest yeniliği**: 4 ölü tetikleyiciyi canlandırma, easy4/5 düzeltme, late-join buff — ayrı geliştirme
  turu ([[cargor-quest-status]] backlog). Bu tur denge, geliştirme değil.
- Upgrade fiyatlandırması (`UPGRADE_PRICING_REPORT` v3.2), roguelite perk fiyat/güç dengesi
- C1 kota kalibrasyonu (play-test blocker)
- Perkler sim'de **nötr sabit** (`rentScaledMultiplier=1`, `rewardVolatility=0`, `phoneRingPerkBonus=0`)

## 3. Sim girdisi — varsayımsal

Gerçek kutu/dk verimi kodda yok → denetimin varsayımları **aynen** korunur (karşılaştırılabilirlik):
- **Normal**: 2.0 kutu/dk/oyuncu, %20 kutu hatası, %3 müşteri kaybı
- **Yavaş/Kötü**: 1.2 kutu/dk/oyuncu, %30 hata, %8 müşteri kaybı
- **YENİ — quest varsayımı**: günde 1 quest kabul edilir (kod limiti), ortalama ~%X tamamlanır
  (economist gün-süresi/hedef-zorluğa göre belirler; easy1=5 kutu paketle zaten yapılan iş → yüksek
  tamamlanma; easy2=2 tır → orta). Tamamlanınca havuzdan rastgele 2 ödül (para+prestij) uygulanır.

Bu turun cevapladığı: "değişen değerler + quest geliri dengeyi bozmuş mu?" (aynı taban → geçerli
karşılaştırma). Cevaplamadığı: mutlak zorluk (play-test ölçümü gerekir; sim repoda kalacağı için o
veri gelince ucuz tekrar koşum).

## 4. Denetimden bu yana değişenler (sim'e işlenecek)

| Etki | Değişiklik | Kaynak | Not |
|---|---|---|---|
| **Büyük** | `maxPrestige` 100 → **150** | `PrestigeManager.cs:19` | Gelir çarpanı: ödül `rewardPerBox + floor(prestij/10)×5`. Tavan 100→tier10→100TL/kutu; 150→tier15→125TL/kutu. Son üçte bir ~%25 daha fazla gelir. Denetim tablosu (4P 1661TL) muhtemelen düşük. |
| **Orta** | `requiredCargo` 3-7 → **2-6** | TruckSpawner `09197b9` | Tır başına ort. 5→4 kargo |
| **Orta** | `hangarStayDuration` 120 → **30sn** | `GameEconomySettings.cs:48` | 30sn artık üretim tavanı — yavaş takım tır kalkmadan yetişemezse gelir sıfırlanır. Sim tır penceresini modellemeli (denetim sim'i modellemiyordu). |
| Küçük | `boxDropMoneyPenalty` = **5** | `GameEconomySettings.cs:69` | Merkezileşti (eskiden 1/5/10 tutarsız) |
| Küçük | `wrongDeliveryPrestigePenalty` = **-0.2** | `GameEconomySettings.cs:111` | Yeni |
| Küçük | wealthTax **kaldırıldı** | `9d2c3b0` | Denetim sim'inde hâlâ 0.1 var → sök |

## 5. Quest ödül boyutu (YENİ analiz)

Aktif quest verileri (`Assets/Resources/Quests/`, qa dökümü — economist asset'ten doğrulayacak):

| Quest | Hedef | Ödül havuzu (2 seçilir) | Ceza havuzu (2 seçilir) |
|---|---|---|---|
| easy1 | 5 kırmızı kutu paketle (PackToy) | Money +10/+20/+15, Prestige +1/+2 | Money -10/-15/-5, Prestige -2/-1 |
| easy2 | 2 tır tamamla | Money +20/+16/+8, Prestige +1/+2 | Money -6/-12/-18, Prestige -1/-2 |
| easy3 | rafa 5 kutu koy | **Money +100/+150/+200**, Prestige +1/+2 | Money -20/-10/-5, Prestige -1 |

**Sorun:** easy3'ün para ödülü easy1/easy2'nin ~10 katı, aynı zorluk tier'ında (hepsi "easy").
Rafa 5 kutu koymak, 5 kutu paketlemekten ya da 2 tır tamamlamaktan daha değerli değil. Bir günün
çekirdek geliri (Normal 1P ~birkaç yüz TL) düşünülünce easy3 tek başına günü domine eder → oyuncu hep
easy3'ü seçer, denge bozulur.

**economist görevi:** easy1-3 ödüllerini (a) birbirine göre zorlukla orantılı, (b) çekirdek günlük
gelire göre "anlamlı ama domine etmeyen" (~yarım-bir günlük ekstra gibi) hedefe hizala. Ceza havuzları
da gözden geçir (easy1 -15 prestij yok ama -2 prestij ceza var; tutarlılık). Değerler sim'de doğrulanır.

## 6. Başarı kriterleri

Kullanıcı hedefi (ekonomi): **"kıl payı başarmış"** — son kira ısırmalı.

| Eksen | Ölçüt |
|---|---|
| İflas (Normal) | 1P-4P hiçbiri iflas etmemeli |
| İflas (Yavaş) | Erken iflas olmalı (~gün 8) |
| Prestij pacing | Tavana (150) gün ~14'ten önce çarpmamalı |
| Kira baskısı | Gün 16 kirası sonrası kasa, o kiranın 3 katını aşmamalı |
| Oyuncu ölçeği | 4P final kasası 1P'nin 4 katını aşmamalı |
| **Quest denge (YENİ)** | Hiçbir aktif quest tek başına bir günlük çekirdek geliri aşan ödül vermemeli; easy1-3 ödülleri birbirine ±2x içinde (aynı tier) |

## 7. Çıktılar

1. `tools/economy-sim/sim.js` — repoda kalıcı, kaynak-değer başlıklı, **quest gelir boyutu dahil**
2. Sim koşusu: 1P-4P × Normal/Yavaş = 8 senaryo + quest-geliri açık/kapalı karşılaştırması
3. `plans/economy-audit-2026-07-17.md` — yeni rapor (denetim→bugün delta + quest boyutu). Eski rapor
   **silinmez** (baseline)
4. Değer düzeltmeleri: `easy3.asset` (+ gerekirse easy1/2) ödül havuzu; çekirdek ihlal çıkarsa
   `EkonomiAyarlari.asset`/`GameEconomySettings.cs`
5. `GameEconomySettings.cs:152-297` bayat C# ContextMenu sim'i **sil** (15 gün/prestij 5/clamp 100/
   playerCountMultiplier yok = yanlış cevap veriyor)
6. `plans/devam.md` + `PLAN.md` güncelle

## 8. İş akışı

CLAUDE.md BÜYÜK/RİSKLİ (ekonomik değer):
- **economist** yazar (sim + rapor + easy3 vd. değer önerileri + çekirdek doğrulama)
- **kontrol** dal-sonu tek toplu ONAY kapısı (en fazla 3 tur)
- Müdür sim çıktısını + easy3 değişikliğini kendi doğrular
- Quest asset düzeltmesi (easy3) = veri (YAML), C# sim silme = kod → **Unity kapalıysa** headless
  EditMode ile 0 CS teyidi (şu an Unity AÇIK — kod silme öncesi kapatılmalı ya da Console teyidi)

## 9. Riskler

| Risk | Azaltma |
|---|---|
| Sim ↔ kod ayrışması (JS vs C#) | `sim.js` başlığı kaynak dosya:satır + tarih belgeler |
| Varsayımsal kutu/dk → mutlak zorluk bilinmiyor | Rapor açıkça işaretler; play-test ölçümü gelince tekrar koşum |
| Quest tamamlanma oranı varsayımı belirsiz | economist muhafazakâr aralık kullanır + duyarlılık (quest-geliri açık/kapalı iki koşum) |
| easy3 düşürünce quest cazibesi kaybolur | Hedef "domine etmeyen ama anlamlı" — sıfırlama değil hizalama |
| Unity açık → C# sim silme derleme teyidi | Silme öncesi Unity kapatma ya da Console teyidi |

## 10. Açık kalan (bu tur DIŞI)
- Quest yeniliği (ölü tetikleyici/easy4-5/late-join buff) — [[cargor-quest-status]]
- Netcode auth-hardening dalı (3 commit, play-test + merge bekliyor)
- Upgrade fiyat + roguelite perk dengesi + C1 kota
