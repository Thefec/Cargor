# Quest Sistemi Onarımı — Implementasyon Planı

> **For agentic workers:** Bu plan CLAUDE.md müdür-modeliyle yürütülür: müdür KÜÇÜK/net task'ları
> inline yapar, R2'yi gameplay+graphics-ui'ya delege eder, R4'te kullanıcıyı Unity'de yönlendirir,
> dal-sonu **tek toplu kontrol kapısı** çalıştırır. Steps checkbox (`- [ ]`) ile izlenir.

**Goal:** Pasif quest sistemini co-op'ta çalışır + mimari-güvenli hale getir (butonlar iş görsün,
ödüller uygulansın, buff'lar stack'lensin, kritik sistemler prefab riskinden çıksın).

**Architecture:** Dört bağımsız onarım (R1 veri, R2 UX, R3 kod bug, R4 sahne). R1/R3 nokta-atışı,
R2 iki-parçalı (server durum expose + UI), R4 Unity-elle. Netcode server-authoritative korunur.

**Tech Stack:** Unity 6000.4.3f1, URP, Netcode for GameObjects (NetworkList/NetworkVariable/RPC),
C#, ScriptableObject quest verisi (`Assets/Resources/Quests/`).

## Global Constraints

- Dal: `feature/quest-system-repair` (main'den). Ekonomi/netcode dallarına dokunma.
- Server-authoritative korunur: durum yazımı yalnız `IsServer`, client `ServerRpc` ile ister.
- Çalışma ağacındaki font/materyal churn **commit DIŞI** — her commit seçici (`git add <dosya>`), asla
  `git add -A`. Bkz `[[unity-batchmode-artifacts]]`.
- Unity **kapalı** → kod değişiklikleri (R2/R3) sonrası headless EditMode ile 0 derleme hatası teyidi.
  R1 asset + R4 sahne Unity gerektirir (kullanıcı Editor'de).
- Ekonomik değer bu turda **değişmez** — `easy3` orantısızlığı economist'e flag, karar yenilik turunda.
- BoxType eşleşmesi (doğrulandı): `BoxInfo.BoxType { Yellow=0, Blue=1, Red=2 }`.

---

## Task R1: easy1 quest verisi düzeltme (müdür, KÜÇÜK)

**Files:**
- Modify: `Assets/Resources/Quests/easy1.asset:19`

**Bağlam (doğrulandı):** easy1 `questType: 0` (CompleteMinigame) ama başlığı "5 kırmızı kutu paketle".
`NotifyMinigameCompleted()` hiç çağrılmıyor → quest asla ilerlemez. `requirement` bloğu zaten doğru
(`targetCount: 5`, `requireSpecificBoxType: 1`, `requiredBoxType: 2`=Red). Handler zinciri hazır:
`Table.cs:777 NotifyToyPacked(boxType)` → `QuestManager.HandleToyPacked` (satır 304) →
`UpdateQuestProgress(QuestType.PackToy, boxType, 1)`, satır 427 box-type eşleşmesi yapıyor. Tek eksik
questType değeri.

- [ ] **Step 1: questType'ı PackToy yap**

`easy1.asset` satır 19: `  questType: 0` → `  questType: 3`

(Başka hiçbir alan değişmez — requirement/rewardPool/penaltyPool doğru.)

- [ ] **Step 2: Değişikliği doğrula**

Run: `grep -n "questType:" Assets/Resources/Quests/easy1.asset`
Expected: `19:  questType: 3`

- [ ] **Step 3: Commit**

```bash
git add "Assets/Resources/Quests/easy1.asset"
git commit -m "fix(quest): easy1 questType Minigame->PackToy (olu tetikleyici -> canli PackToy hook)"
```

**Play-test doğrulama borcu (R4 sonrası, kullanıcıda):** kırmızı kutu paketle → easy1 progress 1/5 artar.

---

## Task R3: BuffManager stacking bug (gameplay veya müdür inline)

**Files:**
- Modify: `Assets/Scripts/Quest/Buff/BuffManager.cs:180-183`

**Interfaces:**
- Consumes: `NetworkListEvent<BuffData>` (`changeEvent.Value`, `changeEvent.PreviousValue`),
  `BuffData(BuffType, float amount, int remainingDays)` ctor (bkz satır 209), `ApplyBuffEffect(BuffData)`.
- Produces: doğru stat-stacking (dış imza değişmez).

**Bağlam (doğrulandı):** `ApplyBuffEffect` **additive** (`player.moveSpeed += amount`,
`sprintDuration += amount`, `staminaRegenRate += amount`, `realDurationInSeconds += amount`,
`maxQueueSize += amount`, `minWaitTime/maxWaitTime += amount` — satır 494-547). `AddBuffInternal`
(342-366) aynı tür buff varsa `existing.amount += buff.amount` yapıp `_activeBuffs[i] = existing` yazar
→ `NetworkListEvent.Value` → mevcut `Value` case (180-183) yalnız `OnBuffUpdated` fırlatır,
`ApplyBuffEffect` çağırmaz → ikinci buff'ın stat etkisi hiç uygulanmaz. `Add` case (172) ilk eklemede
zaten tam effect uyguladığından, `Value`'da **yalnız artan delta** uygulanmalı (çift-uygulama olmaz).

- [ ] **Step 1: Value case'ine delta-effect uygula**

`BuffManager.cs` satır 180-182'yi değiştir:

```csharp
                case NetworkListEvent<BuffData>.EventType.Value:
                    OnBuffUpdated?.Invoke(changeEvent.Value);
                    // Stacking: Add ilk effect'i uyguladi; burada yalniz artan delta kadar ek uygula.
                    var stackDelta = new BuffData(
                        changeEvent.Value.buffType,
                        changeEvent.Value.amount - changeEvent.PreviousValue.amount,
                        changeEvent.Value.remainingDays);
                    ApplyBuffEffect(stackDelta);
                    break;
```

- [ ] **Step 2: Derleme teyidi (Unity kapalı)**

Run: headless EditMode derleme (bkz `[[unity-headless-verify]]`).
Expected: 0 compile error. `changeEvent.PreviousValue`'nun `NetworkListEvent<BuffData>`'te mevcut
olduğu doğrulanır (NGO Value type'lar için sağlar); derlenmezse `PreviousValue` yerine mevcut
`_activeBuffs`'tan eski değeri türetmeye düş (fallback: delta hesaplamak için Add/Value ayrımını
`AddBuffInternal`'a taşı).

- [ ] **Step 3: Commit**

```bash
git add "Assets/Scripts/Quest/Buff/BuffManager.cs"
git commit -m "fix(buff): stacking'de artan delta kadar stat effect uygula (Value case olu idi)"
```

**Play-test doğrulama borcu:** aynı buff'ı iki kez ver → stat etkisi iki kat (örn. moveSpeed +5 sonra +5 = +10).

---

## Task R2: Günlük-limit UX geri bildirimi (gameplay + graphics-ui, DELEGE)

Bu task iki parça: **R2a** server durum expose (gameplay), **R2b** UI tepki (graphics-ui). Sıralı —
R2b, R2a'nın ürettiği durumu tüketir.

**Files:**
- Modify: `Assets/Scripts/Quest/Manager/QuestManager.cs` (R2a — `HasAcceptedQuestToday` satır 572,
  `AcceptQuestInternal` satır 548-567; client-görünür durum + red bildirimi)
- Modify: `Assets/Scripts/Quest/UI/QuestSlotUI.cs` (R2b — `UpdateButtonStates` satır 292-308)
- Modify: `Assets/Scripts/Quest/UI/QuestUIController.cs` (R2b — refresh akışı, satır ~160-172/286-305)

**Interfaces:**
- Produces (R2a → R2b tüketir): QuestManager üzerinde client-okunur bir "bugün quest alındı mı"
  sinyali. Öneri: `NetworkVariable<bool> _hasAcceptedToday` (server yazar, `AcceptQuestInternal`
  başarısında true, gün-değişiminde false) + public getter `bool HasAcceptedQuestTodayClient =>
  _hasAcceptedToday.Value`. Mevcut server-side `HasAcceptedQuestToday()` (satır 572) korunur; NV onun
  client-projeksiyonudur. Ayrıca red durumunda client'a bildirim: reddedilen requester'a
  `ClientRpc`/`OnAcceptRejected` event (mesaj gösterimi için).
- QuestSlotUI, `QuestManager.Instance.HasAcceptedQuestTodayClient` okuyup accept butonunu
  `interactable=false` + görsel gri yapar; reddedilirse `LocalizationHelper.GetLocalizedString`
  ("Quest_AlreadyAcceptedToday" yeni anahtar) ile kısa mesaj.

**R2a — gameplay departman brief:**
- Server-side davranış (günde-1-limit) **korunur**, sadece client'a görünür kılınır.
- `_hasAcceptedToday` NetworkVariable ekle (writePerm Server), `AcceptQuestInternal` başarı yolunda
  (satır 563'ten sonra) `_hasAcceptedToday.Value = true`; gün-değişimi/atama noktasında (`OnNewDay`
  → `AssignDailyQuests`, satır ~262) `false`'a resetle. `HasAcceptedQuestToday()`'in mevcut kaynağını
  (hangi state'e bakıyorsa) NV ile senkron tut — tek-kaynak tutarsızlığı olmasın.
- Red bildirimi: `AcceptQuestInternal` limit dalında (satır 557-561) sessiz `return` yerine
  reddedilen client'a `ClientRpc` ile "bugün zaten aldın" sinyali (sadece o requester'a — `RpcParams`
  ile hedefli, tüm client'lara broadcast etme).
- Kabul kriteri: client `HasAcceptedQuestTodayClient` doğru okur; ikinci accept denemesi client'a
  görünür şekilde reddedilir.

**R2b — graphics-ui departman brief:**
- `QuestSlotUI.UpdateButtonStates`: accept butonu `canAccept && !HasAcceptedQuestTodayClient` iken
  interactable; aksi halde `interactable=false` + gri (mevcut renk alanları veya CanvasGroup alpha).
  Buton **görünür kalabilir** ama tıklanamaz/gri (kullanıcı neden tıklayamadığını anlasın).
- Red mesajı: R2a'nın red-sinyalini dinle → kısa toast/label (`LocalizationHelper` deseni, yeni anahtar
  `Quest_AlreadyAcceptedToday`). Mevcut lokalizasyon dosyasına anahtarı ekle.
- `_hasAcceptedToday` değişiminde tüm slotlar refresh olmalı (NV OnValueChanged → RefreshAllSlots).
- Kabul kriteri: quest alındıktan sonra diğer slotların accept butonu gri/pasif; tıklama denemesi
  mesaj gösterir.

- [ ] **Step 1: R2a'yı gameplay'e delege et** (NetworkVariable + reset + hedefli red ClientRpc). qa
  desenini takip et (G-serisi auth: RPC `RequireOwnership=false` ama server body). Subagent dosyayı
  kendi okur — yol+satır verildi, tüm dosya prompt'a yapıştırılmaz.
- [ ] **Step 2: R2a headless derleme teyidi** (0 CS).
- [ ] **Step 3: R2b'yi graphics-ui'ya delege et** (buton gri/pasif + red mesajı + lokalizasyon anahtarı).
- [ ] **Step 4: R2b headless derleme teyidi** (0 CS).
- [ ] **Step 5: Commit** (R2a+R2b tek mantıksal birim, seçici add)

```bash
git add "Assets/Scripts/Quest/Manager/QuestManager.cs" "Assets/Scripts/Quest/UI/QuestSlotUI.cs" "Assets/Scripts/Quest/UI/QuestUIController.cs" <lokalizasyon-dosyasi>
git commit -m "fix(quest): gunluk-limit UX - buton gri/pasif + gorunur red mesaji (sessiz return kalkti)"
```

**Play-test doğrulama borcu:** bir quest kabul et → diğer slot butonları gri; birine tıkla → "bugün
zaten aldın" mesajı; ertesi gün butonlar tekrar aktif.

---

## Task R4: Kritik sistemleri prefab riskinden çıkar (kullanıcı Unity'de + müdür yönlendirme)

**Files:**
- Modify: `Assets/Scenes/The Main Office.unity` (Unity Editor'de, YAML elle değil)

**Bağlam:** `QuestManager`/`BuffManager`/`NetworkObject`/`QuestTriggerZone`, `Office_Computer (1)`
prefab instance'ına override-component olarak yapışık (`The Main Office.unity:69520-69598`). Prefab'da
Revert/Apply → dört bileşen sessizce silinir.

**Yöntem (müdür adım-adım yönlendirir, kullanıcı yapar — YAML editi riskli, Unity Editor kullanılır):**

- [ ] **Step 1:** Sahnede boş yeni GameObject: `_QuestSystem` (kök seviye, transform sıfır).
- [ ] **Step 2:** `Office_Computer (1)`'deki 4 bileşeni (`NetworkObject`, `QuestManager`, `BuffManager`,
  `QuestTriggerZone`) `_QuestSystem`'e taşı. Not: Unity'de component "taşıma" yok → `_QuestSystem`'e
  aynı 4 bileşeni **ekle**, Inspector referanslarını yeniden bağla, sonra `Office_Computer (1)`'den
  override component'leri kaldır. GUID'ler script asset'ine ait (değişmez); yeniden bağlanacak olan
  **saha referansları**: `QuestManager.allQuests` (easy1/2/3), UI bağları (QuestUIController/SlotUI),
  QuestTriggerZone tetik alanı.
- [ ] **Step 3:** `NetworkObject`'in sahne-içi (in-scene placed) olarak spawn olduğunu teyit et —
  NetworkManager sahne objelerini otomatik spawn eder; `_QuestSystem` aktif ve sahnede olmalı.
- [ ] **Step 4:** `Office_Computer (1)` prefab instance'ı artık sadece dekor — 4 bileşen kalkmış olmalı.
- [ ] **Step 5:** Kullanıcı Play (host) ile teyit: quest paneli açılır, `QuestManager.Instance` null
  değil, quest'ler atanır.
- [ ] **Step 6: Commit** (sahne dosyası — Unity kapatıldıktan sonra, seçici)

```bash
git add "Assets/Scenes/The Main Office.unity"
git commit -m "refactor(scene): quest/buff/network sistemlerini dekor prefabindan _QuestSystem objesine tasi"
```

**Play-test doğrulama borcu:** `Office_Computer` prefab'ında Revert/Apply → quest sistemi etkilenmez.

---

## Dal-sonu kalite kapısı

- [ ] **kontrol subagent** — tek toplu ONAY kapısı (R1+R2+R3+R4, her task ayrı değil). Brief: bu plan
  yolu + spec yolu + değişen dosyalar + "kısa kanıta dayalı verdict". En fazla 3 tur.
- [ ] **economist flag** — `easy3` ödül orantısızlığı (100-200 TL vs 8-30 TL) not; değer önerisi
  yenilik/ekonomi turuna. Bu turda değer DEĞİŞMEZ.
- [ ] **Kullanıcı play-test** — yukarıdaki 4 doğrulama borcu (host/co-op).
- [ ] **devam.md + PLAN.md** güncelle; merge kararı kullanıcıya.

---

## Self-Review — spec kapsama

- Spec §3 R1 → Task R1 ✓ · R2 → Task R2 ✓ · R3 → Task R3 ✓ · R4 → Task R4 ✓
- Spec §3 "dışarıda" (ölü tetikleyici/easy4-5/late-join buff/ekonomik denge) → plana alınmadı, dal-sonu
  economist flag + devam.md'ye backlog notu ✓
- Spec §2.4 "önce doğrula" (buff additive mi) → R3'te doğrulandı (additive), delta-fix güvenli ✓
- Spec §5 başarı kriterleri → her task'ın "play-test doğrulama borcu" satırı ✓
- Placeholder taraması: R3 tam kod var; R1 tam değişiklik var; R2 delege (subagent kendi okur, kabul
  kriteri net); R4 Unity-elle (adım net). Kod-adımı olan tek yer R3 → tam kod verildi ✓
- Tip tutarlılığı: `BuffData(BuffType, float, int)` ctor R3'te satır 209 imzasıyla tutarlı;
  `HasAcceptedQuestTodayClient` R2a-produces/R2b-consumes tutarlı ✓
