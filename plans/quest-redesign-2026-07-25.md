# 🏆 Quest Sistemi — Derin Analiz + Yeniden Tasarım (2026-07-25)

> **Bağlam:** Kullanıcı bir sonraki Unity oturumunda MEVCUT tüm quest'leri (easy1-5) silecek ve bu dokümanda
> önerilen yenileri ekleyecek. Quest sayısı / tipler / ödül / ceza / tier / limit kararları müdüre (bana)
> bırakıldı. Ekonomik değerler economist tarafından sim-doğrulanacak (§7).
>
> **Kaynak dosyalar:** `Assets/Scripts/Quest/` (Data/Manager/UI) — NewCss DIŞINDA, legacy klasör.
> Asset'ler `Assets/Resources/Quests/*.asset`. GDD §16.

---

## 1. Sistem Mimarisi (kod-doğrulandı)

**Veri modeli — `QuestData` (ScriptableObject, `Cargor/Quest Data` menüsü):**
| Alan | Tip | Not |
|---|---|---|
| `questId` | string | **BENZERSİZ olmalı** — çakışma sessizce üzerine yazar (easy4/5 bug'ının kökü) |
| `questTitle` / `questDescription` | string | Açıklama runtime'da `requirement`'tan üretilir; el-yazımı metin yalnız fallback |
| `tier` | QuestTier | Easy=0 / Medium=1 / Hard=2 |
| `questType` | QuestType | Aşağıda 7 tip (yalnız 3'ü canlı) |
| `requirement` | QuestRequirement | `targetCount` + opsiyonel kutu/tır rengi |
| `rewardPool` | List\<QuestReward\> | **Runtime'da max 2 rastgele seçilir** (seed'li, netcode-güvenli) |
| `penaltyPool` | List\<QuestReward\> | **Runtime'da max 2 rastgele seçilir** |

**Akış (server-authoritative, `QuestManager`):**
1. Her yeni gün → `AssignDailyQuests`: tier-uygun havuzdan **3 quest teklif** (`DAILY_QUEST_COUNT=3`), her birine deterministik `rewardSeed`.
2. Oyuncu bir slotu **kabul eder** → `Active`. **Takım günde YALNIZ 1 quest kabul edebilir** (`_hasAcceptedToday`, tek global NetworkVariable).
3. İlerleme `QuestTracker` event'leriyle (aşağıda) takip edilir → hedef dolunca `Completed`.
4. Oyuncu **toplar** → `ApplyRewards(SelectedRewards)` (havuzdan seçilen max 2).
5. Kabul edilip **tamamlanmayan** quest → gün sonu `ApplyPenalties(SelectedPenalties)` + `Failed`. **Kabul edilmeyen quest cezalandırılmaz** (ceza yalnız kabul+başarısızlık).
6. Tier kilidi: `_currentQuestTier` başlangıç 0 (Easy). Medium/Hard yalnız **"Görev Tier" upgrade**'i (`SetQuestTier`) ile açılır.

**Havuz seçim modeli önemli:** Bir quest'in SABİT ödülü yoktur. `rewardPool`'dan Fisher-Yates ile max 2 seçilir. Yani EV, havuz kompozisyonuna bağlıdır. Örn. 3 Money + 2 Prestige havuzdan 2 seçilince beklenen ≈ 1.2 Money öğesi + 0.8 Prestige öğesi.

---

## 2. Canlı vs Ölü Tetikleyiciler ⚠️ (EN KRİTİK KISIT)

`QuestTracker` 7 event tanımlar ama yalnız **3'ü** oyun kodundan gerçekten ateşleniyor (grep ile tüm `Assets` tarandı):

| # | QuestType | Tetikleyici çağrısı | Durum |
|---|---|---|---|
| 1 | **PlaceBoxOnShelf** | `ShelfState.cs:608` | ✅ **CANLI** |
| 2 | **CompleteTruck** | `Truck.cs:584` | ✅ **CANLI** |
| 3 | **PackToy** | `Table.cs:777` | ✅ **CANLI** |
| 0 | CompleteMinigame | — | ☠️ ölü |
| 4 | AnswerPhone | — | ☠️ ölü (ama `PhoneCallManager` VAR → kolay bağlanır) |
| 5 | MakePackagingMistake | — | ☠️ ölü |
| 6 | CompleteSpecificColorTruck | — | ☠️ ölü (tır renkleri VAR → kolay bağlanır) |

**Sonuç:** Kod DEĞİŞMEDEN quest'ler yalnız **PlaceBoxOnShelf / CompleteTruck / PackToy** kullanabilir. Bu 3 tip üzerine tasarım = kullanıcı sadece asset ekler, sıfır kod.
(`CustomerAI.cs:871`'deki `// NotifyCustomerServed()` yorumu ölü — o metod QuestTracker'da hiç yok.)

**Fırsat (opsiyonel, küçük gameplay task):** `AnswerPhone` + `CompleteSpecificColorTruck` tetikleyicilerini bağlamak (PhoneCallManager event'i + Truck.cs'te renk-özel event'i mevcut `NotifyTruckCompleted` yanına eklemek) → kullanılabilir tip 3'ten **5'e** çıkar, çok daha zengin quest çeşitliliği. Minigame/Mistake düşük değerli, atlanır.

---

## 3. Ödül/Ceza Uygulaması (12 RewardType, hepsi bağlı)

`ApplyRewardOrPenalty` switch'i (kod-doğrulandı):
- **Money** → `MoneySystem.ModifyMoney((int)amount)` ✅ çalışır
- **Prestige** → `PrestigeManager.ModifyPrestige(amount)` ✅ çalışır (0–100 skala, tier eşiği 4)
- **MaxStamina / MoveSpeed / CustomerWaitTime / WalkSpeed / StaminaRegenRate / DayDuration / MaxQueueSize / PenaltyReduction** → `BuffManager.AddPermanentBuff` ⚠️ (BuffManager tüketimini quest başına doğrula)
- **TempMoneyBoost / TempSpeedBoost** → `BuffManager.AddTemporaryBuff(..., durationDays)` ⚠️ (`TempMoneyPerBox` hafızada ÖLÜ işaretli)

**Tasarım kararı:** Çekirdek ödül/ceza = **Money + Prestige** (ikisi de kesin çalışır ve dengelemek istediğin ekonominin ta kendisi). Stat-buff'lar yalnız BuffManager tüketimi doğrulandıktan sonra "baharat" olarak. Ceza çarpanı: `PenaltyReduction` buff'ı cezayı düşürür (`penaltyMultiplier`).

---

## 4. Limitler (mevcut)

| Limit | Değer | Yorum |
|---|---|---|
| Günlük teklif | 3 | Tier-uygun havuzdan rastgele 3 (tekrarsız) |
| **Günlük kabul** | **1 / takım** | Global `_hasAcceptedToday` → 16 günde en fazla ~16 quest tamamlanır |
| Tier kilidi | Easy açık | Medium/Hard "Görev Tier" upgrade'iyle |
| Havuz seçim | max 2 ödül + max 2 ceza | Quest başına |

**1-kabul/gün** ekonomik olarak koruyucu: quest gelirini üst-sınırlar (audit: toplam ekonominin ~%1-5'i, bozmuyor) ve "günün odağı" kararını anlamlı kılar. Co-op'ta tek paylaşılan günlük hedef temiz.

---

## 5. Mevcut Kalibrasyon Baseline (silinmeden önce — economist EV referansı)

Prestij-rescale (2026-07-20) sonrası easy1 & easy3 örnek:
- **rewardPool:** 3× Money (10–35) + 2× Prestige (0.4 & 0.8) → 2 seçilir
- **penaltyPool:** 3× Money (−5…−20) + 2× Prestige (−0.4 & −0.8) → 2 seçilir
- **targetCount:** 5 (kutu/paket)

Bağlam: `rewardPerBox=50` (tır'a kutu başı para), prestij tier eşiği=4, startingPrestige=6, maxPrestige=100. Yani bir quest'in ~25-50 parası ≈ 0.5-1 kutu; 0.4-0.8 prestij ≈ tier'ın %10-20'si. Küçük ama hissedilir.

---

## 6. Kritik Kısıtlar & Fırsatlar (tasarım özeti)

1. **Sıfır-kod = 3 canlı tip** (rafa kutu / tır tamamla / oyuncak paketle). Ana tasarım bunun üzerine.
2. **1 kabul/gün** → quest ekonomisi doğası gereği sınırlı; ödüller cömert olabilir ama tek/gün olduğu için toplamı bozmaz.
3. **Havuz modeli** çeşitlilik + netcode-güvenli reroll verir → koru.
4. **Money+Prestige** güvenli çekirdek; stat-buff'lar doğrulama-sonrası.
5. **Tier progression** var ama Hard'ın "Görev Tier" upgrade fiyatına değmesi için ödül sıçraması belirgin olmalı.
6. **Renk gereksinimi** (`requireSpecificBoxType`) PlaceBoxOnShelf/PackToy'da çalışır → Red/Blue/Yellow varyasyonu bedava çeşitlilik.

---

## 7. NİHAİ TASARIM (economist sim-doğrulamalı + müdür kod-doğrulamalı) ✅

### 7.0 UYGULAMADAN ÖNCE 3 ZORUNLU KISIT (kod-doğrulandı)

1. **CompleteTruck ASLA renk-kilitlenemez.** `HandleTruckCompleted` (`QuestManager.cs:351`) her zaman hardcoded `Red` gönderir, `UpdateQuestProgress:488` renk kontrolünü tipe bakmadan uygular → renkli CompleteTruck = **sessiz soft-lock** (oyuncu kabul eder, çalışır, gün sonu yine ceza yer). **KURAL: tüm CompleteTruck asset'lerinde `requireSpecificBoxType = FALSE`.** (PlaceBoxOnShelf/PackToy gerçek rengi geçirir → onlarda güvenli.)
2. **PackToy'da "renk" = ÜRÜN KATEGORİSİ.** Sabit eşleme (`Table.cs:828-832`): **Toy→Red, Clothing→Yellow, Glass→Blue.** Renkli PackToy quest'inde `requiredBoxType=Red` demek "oyuncak paketle" demek — açıklama metni kategoriye uymalı.
3. **Medium/Hard şu an OYUNDA AÇILAMAZ.** "Görev Tier" upgrade'i `requiresQuestSystem=true` ile `_questSystemActive` flag'ine bağlı; flag default `false` ve **kodda hiçbir yer `true` yapmıyor** (`UpgradePanel.cs:231`). Yani upgrade draft'a hiç girmez → `_currentQuestTier` hep 0 → yalnız **Easy** teklif edilir. Medium/Hard yazarsan dormant kalır (aşağıda Seçenek A/B).

### 7.1 Yapı: 3 tier × 5 quest = 15 asset

Tier başına 5 asset kompozisyonu: **1× CompleteTruck (renksiz) + 2× PlaceBoxOnShelf (1 renksiz + 1 renkli) + 2× PackToy (1 renksiz + 1 kategori).**
`5/5/5` bilinçli: Easy-tek dönemde `C(5,3)=10` farklı günlük kombinasyon (3/3/3 tekdüze olurdu). Kümülatif havuz (alt tier'lar havuzda kalır) → Hard açıkken P(≥1 Hard teklif)=%73.6.

### 7.2 Ödül / Ceza havuzları (tier başına TEK şablon — o tier'ın 5 quest'i de aynı havuzu paylaşır)

Mevcut şekil korunur: **3 Money + 2 Prestige = 5 öğe havuz, runtime max-2 seçer** (`durationDays=0`).

| Tier | rewardPool Money | rewardPool Prestige | penaltyPool Money | penaltyPool Prestige |
|---|---|---|---|---|
| **Easy** | 10 / 15 / 25 | 0.4 / 0.8 | −5 / −10 / −15 | −0.2 / −0.4 |
| **Medium** | 20 / 30 / 40 | 0.8 / 1.4 | −14 / −18 / −22 | −0.4 / −0.8 |
| **Hard** | 35 / 50 / 65 | 1.6 / 2.4 | −25 / −30 / −35 | −0.8 / −1.2 |

**EV (pick-2-of-5, sim-hesaplı):**
| Tier | Ödül EV | Ceza EV | ceza/ödül |
|---|---|---|---|
| Easy | +20.0 para, +0.48 prestij | −12.0 para, −0.24 prestij | %60 / %50 |
| Medium | +36.0 para (1.8×), +0.88 prestij | −21.6 para, −0.48 prestij | %60 / %55 |
| Hard | +60.0 para (3.0×), +1.6 prestij | −36.0 para, −0.80 prestij | %60 / %50 |

Easy EV=20 ≈ eski easy1/2/3 ortalaması (~21.9) → sürpriz yok. Ceza her iki kaynakta da ödülün ALTINDA (eski sistem prestij cezası ödülle tam simetrikti — bu tur düzeltti: kabul kararı riskli ama adil).

### 7.3 targetCount (5 asset'in hedefleri, tier bazlı)

| Asset (tier başına) | Easy | Medium | Hard |
|---|---|---|---|
| CompleteTruck (renksiz) | 1 tır | 2 tır | 3 tır |
| PlaceBoxOnShelf renksiz | 4 kutu | 7 kutu | 10 kutu |
| PlaceBoxOnShelf 1-renk | 2 kutu | 3 kutu | 4 kutu |
| PackToy renksiz | 4 paket | 7 paket | 10 paket |
| PackToy 1-kategori | 2 paket | 3 paket | 4 paket |

Tamamlanma (sim step-fonksiyonu, tır throughput'una karşı): Easy tüm P'de rahat (%65-85); Medium 2P+ rahat / 1P zorlu (%40); Hard **fiilen 2P+ içerik** (3P+ rahat, 2P orta, 1P nadir) — bilinçli 1P-asimetri (Ucuz Kira/Prestij Simsarı emsali). Renkli hedefler ≈ renksizin %40-50'si (renk dağılımı ~1/3 varsayımı, **playtest-doğrulanmamış**).

### 7.4 Limitler: 3 teklif + 1 kabul/gün **KORUNDU** (kod sabiti değişmez)

Sim: quest günlük EV / çekirdek gelir oranı tüm tier×gün×P'de **%0.3–3.9** (mevcut ~%1-5 baseline korunur, enflasyon yok). En kötü tek-gün ceza en kırılgan senaryoda (1P Yavaş gün1) bile günlük gelirin %45'ini geçmez → hiçbir quest bir günü sıfırlamaz. `DAILY_QUEST_COUNT` ve günde-1-kabul'e dokunma.

### 7.5 İki uygulama seçeneği → **SEÇENEK B UYGULANDI (2026-07-25, kontrol ONAY)**

> **DURUM: B tamamlandı, 3 tier de canlı.** Yapılanlar:
> - `UpgradePanel.RefreshQuestSystemFlag()` — QuestManager varsa + havuzda quest varsa `_questSystemActive=true` (server-only, `BuildEligibility` başında).
> - **Blocker fix:** omurgalar artık `displayName` yerine `ResolveUpgradeKey()` ile eşleşiyor (`UpgradeKeyAliases`: `bb_*` effectId + İngilizce + Türkçe adlar). Türkçe yeniden adlandırma `Görev Kademesi`/`Ek Hangar`/`Geniş Kuyruk` tüm eşleşmeleri sessizce koparmıştı. `WarnMissingBackbones()` tekrarını engelliyor.
> - Sahnede Görev Kademesi fiyatı `baseCost 80 / costStep 20` → L1=80, L2=100 (aşağıdaki economist rakamı).
> - 15 asset üretildi: `Assets/Resources/Quests/Q_{Easy,Medium,Hard}_*.asset`.
> - `QuestManager.CollectQuestAssets()` — `Resources/Quests` otomatik yükleniyor, elle inspector wiring'i **gerekmiyor**.
> - **Kullanıcıya kalan tek adım:** eski `easy1-5.asset`'i Unity'de sil.


- **Seçenek A — SIFIR KOD (hemen çalışır):** Yalnız **5 Easy** quest'i yaz + `allQuests`'e ekle. Medium/Hard'ı da yazabilirsin ama dormant kalır. Kullanıcı tarafında hiç kod yok.
- **Seçenek B — Medium/Hard'ı aç (küçük gameplay+economist task):** `_questSystemActive` flag'ini `true` yap (feature'ı aç) + "Görev Tier" upgrade'ini draft'a geri koy (`requiresQuestSystem`/fiyat). economist reaktivasyon fiyatı: **Level1 (Medium aç) ≈ 80 TL, Level2 (Hard aç) ≈ 100 TL** (T1-bandı; eski "tier×250" sezgisi günde-1-kabul'de asla payback almaz, geçersiz). Bu bir sonraki tur işi.

> **Müdür önerisi:** 15 asset'in TÜMÜNÜ yaz (tek seferde). Easy hemen canlanır (Seçenek A); Medium/Hard hazır bekler, Seçenek B tek küçük turda flag+upgrade ile açılır. Böylece asset işini iki kez yapmazsın.

### 7.7 TETİKLEYİCİ GENİŞLETME TURU (2026-07-25, ikinci tur) ✅

**Kod (3 değişiklik, headless 0 CS hata / 8-8 test):**
1. `QuestManager.UpdateQuestProgress` — renk filtreleri artık **tipe göre** uygulanıyor (`boxTypeApplies` / `truckColorApplies`). §7.0-1'deki **CompleteTruck soft-lock kısıtı KALKTI** — bu artık kök nedeninden kapalı, kural olarak taşımaya gerek yok.
2. `Truck.cs` (~584) — tır tamamlanınca `NotifySpecificColorTruckCompleted(_networkRequestedBoxType.Value)` de atılıyor (server-only blok, tam bir kez).
3. `PhoneCallManager.AnswerServerRpc` — `NotifyPhoneAnswered()`. `!_isRinging` guard'ı çift saymayı engelliyor.

→ Kullanılabilir quest tipi **3 → 5**. Abonelikler zaten vardı, yalnız *emit* tarafı eksikti.

**economist bulguları** (`.claude/agent-memory/economist/quest_answerphone_colortruck_2026-07-25.md`):

| Tip | Easy | Medium | Hard |
|---|---|---|---|
| AnswerPhone (target / tamamlanma) | 2 / %85.1 | 3 / %61.7 | 4 / %35.0 → **EV negatif** |
| CompleteSpecificColorTruck (target / en iyi P) | 1 / %40 | 2 / %40 | 3 / %20 |

- Tır rengi **tam 1/3 garanti** (`TruckSpawner.DrawNextBagColor` dengeli torba) — eski "~1/3 varsayımı, playtest-doğrulanmamış" notu artık kesin gerçek.
- AnswerPhone **oyuncu-sayısından bağımsız** (10 zar/gün × %30) → 1P için equalizer, "Medium 2P+/Hard 3P+" bandı bu tipe mekanik olarak uygulanamaz.
- Havuz 5→7/tier limitleri bozmuyor (EV/gelir %0.3–4.58, tek hücrede +2.68 TL) → `DAILY_QUEST_COUNT`/1-kabul sabitlerine dokunulmadı.

**MÜDÜR KARARI — 2 asset basıldı, 4'ü basılmadı:**
- ✅ `Q_Easy_6_Phone` (target 2), `Q_Medium_6_Phone` (target 3) → sağlıklı tamamlanma, çeşitlilik + 1P desteği.
- ❌ **Hard AnswerPhone basılmadı** — baseline EV **negatif**, rasyonel takım hiç kabul etmez = ölü kart, günde-1-kabul modelinde teklif slotu israfı.
- ❌ **3 renk-özel tır quest'i basılmadı** — en iyi hücrede bile %40, 1P'de %8 → EV negatif ("tuzak kart"). **Tetikleyici canlı, içerik bekliyor:** bu tip tır throughput'u artınca viable olur → doğrudan **tır penceresi cap** işine bağlı (%18 sıfır kapasite). O iş bitince 3 asset tek seferde basılır.

**Havuz artık 17 asset:** Easy 6 · Medium 6 · Hard 5.

### 7.6 Opsiyonel çeşitlilik enhancement'ı (ayrı küçük task) — ✅ §7.7'de yapıldı

`AnswerPhone` + `CompleteSpecificColorTruck` tetikleyicilerini bağla (PhoneCallManager event'i + `Truck.cs`'te renk-özel event) → kullanılabilir tip 3'ten 5'e çıkar, renkli tır quest'leri mümkün olur (`HandleSpecificColorTruckCompleted` zaten gerçek rengi geçiriyor, sadece çağrı eksik). Bu turun kapsamı dışı.

---

## 8. Kullanıcı Workflow (Unity'de nasıl uygulanır)

1. **Sil:** `Assets/Resources/Quests/easy1-5.asset` sil VEYA QuestManager `allQuests` listesini temizle.
2. **Oluştur:** her yeni quest için `Create → Cargor → Quest Data`. `questId` **benzersiz** ver.
3. Alanları §7 tablosuna göre doldur (tier, questType, requirement.targetCount + renk, rewardPool, penaltyPool).
4. **Bağla:** tümünü sahnedeki `QuestManager.allQuests` listesine sürükle.
5. Hard quest'ler için "Görev Tier" upgrade'inin `SetQuestTier`'ı çağırdığını doğrula (zaten bağlı).

---

*Analiz: müdür (statik, kod-doğrulandı). §7 sayıları: economist sim-doğrulaması bekliyor.*
