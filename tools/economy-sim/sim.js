// ============================================================================
// Cargor 16-gun ekonomi simulasyonu -- v3.1
// ============================================================================
// Tarih: 2026-07-30. Selef: v2 (2026-07-18/20), quest modeli + gun suresi bayatti.
// Calistirma: node tools/economy-sim/sim.js
//
// ---------------------------------------------------------------------------
// v3.1 (FAZ 4 uzlastirma) -- FAZ 3'un buldugu IKI MODELLEME HATASI duzeltildi.
// Ikisi de bagimsiz olarak sahne+kod ile YENIDEN dogrulandi (FAZ3 iddiasi olarak
// kabul edilmedi):
//   D1. `startingActiveInteractables` 3 -> 5.
//       KANIT: sahnede ShelfState = 13 ornek (guid d02b1bd2...), bunlarin 10'u
//       "Genis Ambar" levelObjects'i (unity:21193-21202); kalan 3 bagimsiz raf
//       (fileID 1031163660/1630707798/749946192) m_IsActive:1. DisplayTable = 1.
//       `UpgradePanel.UpdateLevelObjects` (cs:683-695) `SetActive(i <= level)`
//       yaziyor ve `InitializeLevelObjects` (cs:458-464) level 0 ile cagiriyor
//       -> seviye 0'da levelObjects'ten YALNIZ [0] aktif.
//       => 3 + 1 + 1 = 5. `CountActiveInteractables` FindObjectsOfType kullaniyor,
//       inaktifleri SAYMAZ (CustomerManager.cs:423-436).
//       ETKI: talep 1P 9->12, 2P 11->16, 3P 14->20, 4P 16->24. Artik HER oyuncu
//       sayisi seri servis tavaninin (11.4) USTUNDE -> 1P bile musteri kaybediyor.
//   D2. MASA CEKISMESI modele eklendi (v3.0'da HIC YOKTU).
//       KANIT: sahnede `Table` = 2 ornek (guid 8656889b...), IKISI DE "Paketleme
//       Istasyonu" levelObjects'i (unity:21223-21224). Seviye 0'da 1 masa aktif.
//       `Table` TEK item tasiyor (Table.cs:57) ve paketleme YALNIZ masada
//       (Table.cs:763-781) -> takimin tum uretimi 1 masadan SERI geciyor.
//       ETKI: 2P-4P geliri v3.0'da %4-26 FAZLA IYIMSERDI.
//       Yeni parametre: ASSUMED.tableBusySeconds (S) -- playtest'te olculmesi
//       gereken 2. en duyarli sayi (S=4 <-> 8 arasi Paketleme upgrade'inin
//       degerini 4x degistiriyor).
//
// AMAC: sim <-> kod ayrisma riskini azaltmak icin TEK kontrol noktasi.
// Asagidaki her deger, 2026-07-30'da CANLI kod/asset'ten satir satir OKUNARAK
// dogrulandi. Bir sonraki denetimde ONCE bu blogu yeniden dogrula.
//
// ----------------------------------------------------------------------------
// v2'YE GORE METODOLOJI FARKLARI (hepsi kod-kanitli)
// ----------------------------------------------------------------------------
//  1. GUN SURESI DUZELTILDI: v2 realDurationInSeconds=160 (cs default) kullaniyordu.
//     SAHNE DEGERI 200 ("The Main Office.unity":15995). Sahne kazanir.
//  2. MUSTERI SERVISI ARTIK SERI (buyuk duzeltme): CustomerAI yalnizca
//     manager.IsFirstInQueue(this) iken BeginService yapiyor (CustomerAI.cs:582).
//     Yani ayni anda TEK musteri servis edilir. v2 tum "demandAdjusted" icin
//     prestij veriyordu -> prestij gelirini ciddi sekilde FAZLA tahmin ediyordu.
//  3. KUYRUK DOLU = SESSIZ ATLAMA: CustomerManager.cs:516 (IsQueueFull -> spawn
//     yok). Spawn olmayan musteri CEZA URETMEZ. Yani "talep" bir tavan degil,
//     bir HAVUZ; gercek prestij gelirini SERI SERVIS KAPASITESI belirler.
//     maxQueueSize sahnede 2 (unity:68600), .cs default 3 (DEFAULT_QUEUE_SIZE).
//  4. QUEST MODELI SIFIRDAN: havuz+MAX_SELECTED=2 modeli ARTIK YOK. QuestData
//     4 sabit alan tasiyor (moneyReward/prestigeReward/moneyPenalty/
//     prestigePenalty, QuestData.cs:48-58). 30 asset Assets/Resources/Quests/
//     altinda; degerleri bu dosyada birebir gomulu (asagida QUEST_ASSETS).
//  5. QUEST ODULU KIRA'DAN SONRA YATAR: settlement DayCycleManager.OnNewDay ->
//     QuestManager.HandleNewDay -> SettleAcceptedQuestsForDayEnd (QuestManager.cs:
//     356-365). OnNewDay bir SONRAKI gunun basinda tetiklenir; kira kontrolu ise
//     ayni gunun sonunda TryProcessMoneyCheck ile yapilir (DayCycleManager.cs:
//     483-489). Yani gun 4'te kabul edilen quest'in parasi gun 4 kirasini
//     ODEYEMEZ. Sim bunu "questSettlePending" ile modelliyor.
//  6. TELEFON GELIRI MODELLENDI (v2'de yoktu): saat 8..17 arasi 10 zar,
//     her biri %30 -> gunde ~3 calma; her yanit 20 TL + 0.2 prestij.
//  7. HANGAR SURESI HER IKI BANTTA ETKILI: v2'de OPTIMISTIC bant
//     hangarStayDuration'i hic kullanmiyordu. v3'te optimistic bant "on-stok
//     var ama kutuyu tira TASIMA suresi var" olarak modellendi (HANDOVER_SPEEDUP),
//     boylece bekleme sureleri iki bantta da olculebiliyor.
//  8. PARA 0'IN ALTINA INMEZ: MoneySystem.ModifyMoney -> Mathf.Max(0, ...)
//     (MoneySystem.cs:91). Cezalar 0'da yutulur; iflas YALNIZ kira kapisinda olur.
//
// ============================================================================
// KAYNAK DEGERLER (dosya:satir -- 2026-07-30'da okunan CANLI deger)
// ============================================================================
// NOT: .asset ile .cs default'u catistiginda ASSET/SAHNE kazanir (Unity
// serialize edilmis degeri yukler). Catismalar raporun "AYRISMA" bolumunde.
const SRC = {
  // ---- Assets/Resources/EkonomiAyarlari.asset (ScriptableObject, CANLI) -----
  // v3.2 RESYNC (2026-08-19): FAZ4 (2026-07-30) sonrasi asset/scene guncellemeleri
  // hic yansitilmamisti. Asagidaki 11 deger bu turda duzeltildi (bkz. rapor).
  baseRentByPlayerCount:      [500, 1000, 1450, 1800], // asset:15 hex f4010000/e8030000/aa050000/08070000 (= GameEconomySettings.cs:21)
  rentGrowthMultiplier:       1.20,   // asset:16   (= GameEconomySettings.cs:24)
  rentIntervalDays:           4,      // asset:17   (= cs:27)
  gracePaymentPercent:        0.8,    // asset:18   (= cs:30)
  rentScaledMultiplier:       1.0,    // asset:19   (perk yoksa 1, cs:33)
  rewardPerBox:               50,     // asset:20   (= cs:42)
  penaltyPerBox:              40,     // asset:21   (= cs:45)  YANLIS RENK teslimat
  hangarStayDurationLegacy:   30,     // asset:22 "hangarStayDuration" (= cs:48) YALNIZ dizi bos ise
  hangarStayByPlayerCount:    [120, 60, 40, 30], // asset:23 hex 78/3c/28/1e (= cs:51) v3.2: 1P 90->120
  prestigePerBonus:           8,      // asset:24   (= cs:54)
  bonusPerTier:               5,      // asset:25   (= cs:57)
  rewardVolatility:           0,      // asset:26   (perk kapali, cs:60)
  boxDropMoneyPenalty:        5,      // asset:30   (= cs:78) FIZIKSEL dusme
  phoneRingChancePerHour:     0.20,   // asset:31   (= cs:87) LEGACY skaler; canli davranis artik P-bazli, asagidaki MODEL NOTU'na bak
  phoneRingEventMultiplier:   2.0,    // asset:32   (= cs:93)
  callMoneyReward:            20,     // asset:34   (= cs:99)
  callPrestigeReward:         0.4,    // asset:35   (= cs:102)
  customerLostPrestigePenalty:  -0.4, // asset:36   (= cs:111)
  customerServedPrestigeBonus:   0.4, // asset:37   (= cs:114)
  wrongProductPrestigePenalty: -0.08, // asset:38   (= cs:117)
  boxDropPrestigePenalty:     -0.04,  // asset:39   (= cs:120)
  wrongDeliveryPrestigePenalty: -0.16,// asset:40   (= cs:123)
  festivalBonusMin:           100,    // asset:41   (= cs:132) FALLBACK -- artik kullanilmiyor
  festivalBonusMax:           300,    // asset:42   (= cs:135) FALLBACK
  // FESTIVAL DAY canli davranis: EventEffectManager.cs:407 -> kiranin %10-%20'si.
  festivalRentSharePct:       [0.10, 0.20],
  // v3.2: EkonomiAyarlari.asset'te FAZ4'ten sonra eklenen P-bazli/yardimci alanlar.
  // SIM'DE HENUZ KULLANILMIYOR (bkz. asagidaki MODEL NOTU + rapor). Referans icin tutuluyor.
  rewardVolatilityMean:       1,      // asset:27 (= cs:63) perk kapaliyken etkisiz
  truckCargoMinByPlayerCount: [1, 2, 2, 2],   // asset:28 hex (= cs:66) MODELLENMIYOR
  truckCargoMaxExclusiveByPlayerCount: [3, 4, 5, 6], // asset:29 hex (= cs:69) MODELLENMIYOR
  phoneRingChanceByPlayerCount: [0.20, 0.25, 0.30, 0.35], // asset'te YOK -> cs:90 default CANLI (MODELLENMIYOR)
  phoneRingPerkBonus:         0,      // asset:33 (= cs:96) perk kapali

  // ---- Assets/NewCss/UIScripts/DayCycleManager.cs + SAHNE -------------------
  maxDays:                    16,   // cs:36 MAX_DAYS
  dynamicDurationStartDay:    3,    // cs:37 DYNAMIC_DURATION_START_DAY
  realDurationInSeconds:      200,  // SAHNE unity:16797 (= cs:52 default, artik AYRISMA yok)
  dailyDurationIncrease:      10,   // SAHNE unity:16798 (= cs:55)
  dayStartHour:               7,    // SAHNE unity:16799 "startHour" (alan yeniden adlandirildi, = cs:58)
  dayEndHour:                 18,   // SAHNE unity:16800 "endHour" (alan yeniden adlandirildi, = cs:61)

  // ---- Assets/NewCss/TruckScripts/TruckSpawner.cs + SAHNE ------------------
  truckStartHour:             8,    // SAHNE unity:39722 (= cs:68)
  truckEndHour:               17,   // SAHNE unity:39723 (= cs:71)
  minCargo:                   2,    // cs:38 MIN_CARGO_AMOUNT (legacy fallback -- FAZ4'ten sonra CANLI kargo
                                     // P-bazli, bkz. asagidaki MODEL NOTU + truckCargoMinByPlayerCount)
  maxCargoExclusive:          6,    // cs:39 MAX_CARGO_AMOUNT (Random.Range int -> UST SINIR HARIC, ayni not)
  respawnDelayRange:          [3, 5], // SAHNE unity:39720 (= cs:61) -> ort 4s
  hangarCount:                3,    // SAHNE unity 39705-39718: requiredUpgradeLevel 0/1/2 (dogrulandi)
  hangarsAtLevel0:            1,    // level 0 -> yalniz ilk hangar aktif

  // ---- Assets/NewCss/TruckScripts/Truck.cs + Truck.prefab ------------------
  exitDelay:                  5,    // prefab Truck.prefab:196 (= cs:79)
  // NOT: prefab'daki rewardPerBox=10 / penaltyPerBox=2 (Truck.prefab:197-198)
  // OLU DEGER -- OnNetworkSpawn (Truck.cs:206-218) Resources'tan EkonomiAyarlari
  // yukleyip 50/40 ile EZIYOR. [HideInInspector] alanlar.

  // ---- Assets/NewCss/CustomerSripts/PrestigeManager.cs + SAHNE ------------
  startingPrestige:           12,   // SAHNE unity:26357 (= cs:16) v3.2: 6->12 (cs default de 12'ye cikmis)
  maxPrestige:                100,  // SAHNE unity:26358 (= cs:19)
  prestigePerCustomer:        4,    // SAHNE unity:26360 (= cs:26)
  baseCustomerCapacity:       1,    // SAHNE unity:26361 (= cs:29)
  maxCustomerCapacity:        20,   // SAHNE unity:26362 (= cs:32)

  // ---- Assets/NewCss/CustomerSripts/CustomerManager.cs + SAHNE ------------
  maxQueueSize:               2,    // SAHNE unity:73027 (cs:20 DEFAULT_QUEUE_SIZE artik 2 -- AYRISMA KAPANDI)
  shelfMultiplier:            2,    // SAHNE unity:73029 "_shelfMultiplier" (= cs:71)
  levelMultiplier:            2,    // SAHNE unity:73030 "_levelMultiplier" (= cs:74)
  storeLevelStart:            1,    // SAHNE unity:73031 "_storeLevel" (= cs:77)
  minVariance:               -2,    // SAHNE unity:73032 "_minVariance" (= cs:80)
  maxVariance:                3,    // SAHNE unity:73033 "_maxVariance" (= cs:83)
  minCustomersPerDay:         1,    // SAHNE unity:73034 "_minCustomersPerDay" (= cs:86)
  maxCustomersPerDay:         50,   // SAHNE unity:73035 "_maxCustomersPerDay" (= cs:89)
  spawnStartHour:             8,    // SAHNE unity:73046 (= cs DEFAULT_SPAWN_START_HOUR:21)
  spawnEndHour:               17,   // SAHNE unity:73047 (= cs DEFAULT_SPAWN_END_HOUR:22)

  // ---- Assets/ithappy/.../Customer.prefab (CANLI musteri sabri) -----------
  // DIKKAT: DifficultyManager'in P-bazli sabir olceklemesi bu prefab'a ULASMIYOR
  // (FindObjectsOfType<CustomerAI>() sahnede ornek bulamiyor) -> OLU KOD.
  customerMinWaitTime:        15,   // Customer.prefab:2305 (CustomerAI.cs:81 default 10)
  customerMaxWaitTime:        20,   // Customer.prefab:2306 (CustomerAI.cs:84 default 20)
  customerInteractionTime:    2,    // Customer.prefab:2307 (CustomerAI.cs:87 default 5)

  // ---- SAHNE TOPOLOJISI (v3.1'de KOD-DOGRULANDI, artik VARSAYIM DEGIL) ----
  // `UpgradePanel.UpdateLevelObjects` (cs:683-695): `levelObjects[i].SetActive(i <= currentLevel)`
  // ve `InitializeLevelObjects` (cs:458-464) her upgrade icin level 0 ile cagiriyor
  // => seviye 0'da levelObjects dizisinin YALNIZ [0] indeksi aktif.
  shelfStateTotal:            13,   // sahnede ShelfState guid d02b1bd2... = 13 ornek
  shelfStateInStorageUpgrade: 10,   // "Genis Ambar" levelObjects (unity:21193-21202)
  shelfStateStandalone:       3,    // 13-10; fileID 1031163660/1630707798/749946192, hepsi m_IsActive:1
  displayTableTotal:          1,    // DisplayTable guid c22e4241... = 1 ornek
  // Seviye 0'da aktif interactable = 3 bagimsiz raf + levelObjects[0] + 1 masa = 5
  // (`CustomerManager.CountActiveInteractables` cs:423-436, FindObjectsOfType INAKTIFLERI SAYMAZ)
  activeInteractablesAtLevel0: 5,   // <-- v3.0'da ASSUMED=3 idi, YANLISTI (FAZ3 B?, dogrulandi)

  // Paketleme masasi: sahnede `Table` guid 8656889b... = 2 ornek, IKISI DE
  // "Paketleme Istasyonu" levelObjects'i (unity:21223-21224 -> fileID 729050603, 457085722)
  // => seviye 0'da YALNIZ 1 masa aktif. `Table` TEK item tasiyor
  // (`Table.cs:57` TableState{isEmpty,itemNetworkId,isItemBoxed}) ve paketleme
  // YALNIZ masada yapiliyor (`Table.cs:763-781`) => tum uretim SERI bu masadan geciyor.
  packingTableTotal:          2,    // sahnede fiziksel masa sayisi
  packingTablesAtLevel0:      1,    // seviye 0'da aktif masa sayisi

  // Paralel MUSTERI servis istasyonu sayisi. CANLI = 1: CustomerAI.cs:582 yalniz
  // manager.IsFirstInQueue(this) iken BeginService cagiriyor -> ayni anda TEK
  // musteri. (CustomerManager.cs serviceTables[] KABLOLANMAMIS -- sifir cagiran;
  // sahnede dizi 2 elemanli ama yalniz [0] dolu/[1] fileID:0 bos, unity:73040-73042.)
  serviceStations:            1,

  // ---- Assets/NewCss/GameState/DifficultyManager.cs + DifficultyManager.prefab
  baseStartingMoney:          500,  // prefab:75 (= cs:36)
  moneyMultiplierPerPlayer:   1.2,  // prefab:80 (= cs:60) v3.2: 1.0->1.2 (FAZ4 karari, artik P'ye BAGIMLI)
  playerCountMultCoeff:       0.3,  // DifficultyManager.cs:429 ApplyCustomerSettings() inline literal
                                     // (eski "playerCountMultCoeff" serialize alani KALDIRILDI, deger ayni: 1+(P-1)*0.3)
  upgradeCostMultiplierPerPlayer: [1.00, 2.00, 2.95, 3.70], // cs:72 upgradeCostMultiplierByPlayerCount[]
                                     // v3.2: eski tek-skaler 1.15 (compounding) alani KALDIRILDI; artik P-bazli
                                     // DIZI (prefab override YOK, cs default canli). SIM'DE KULLANILMIYOR
                                     // (upgradeSpendRatio=0 taban kosuda tuketilmiyor) -- dokumantasyon amacli.
  // OLU/KULLANILMAYAN (bkz AYRISMA): baseCustomerCount 10 / customerCountPerPlayer 2
  // (prefab:74,79) -> ScaledCustomerCount hicbir yere yazilmiyor, yalniz log/UI.
  // basePhoneCallChance / phoneChancePerPlayer KALDIRILDI (FAZ4 SS.6) -- artik prefab'ta da YOK,
  // telefonun P-olceklemesi GameEconomySettings.phoneRingChanceByPlayerCount'a tasindi.
  // baseMinPatience 8 / baseMaxPatience 14 / patienceReductionPerPlayer 2
  // (prefab:76,77,81) -> yalniz sahnedeki CustomerAI ornegine yazilir, YOK.

  // ---- Assets/NewCss/UIScripts/MoneySystem.cs + SAHNE --------------------
  moneySystemSceneStartingMoney: 500, // SAHNE unity:4913 -- v3.2: eski DEBUG degeri 50000 DUZELTILMIS (artik AYRISMA yok)
  moneyFloorZero:             true,  // cs:91 Mathf.Max(0, ...)

  // ---- Assets/NewCss/Phone/PhoneCallManager.cs + SAHNE -------------------
  phoneStartHour:             8,    // SAHNE unity:14820 (= cs:33)
  phoneEndHour:               18,   // SAHNE unity:14821 (= cs:36)
  phoneRingDuration:          15,   // SAHNE unity:14822 "ringDuration" (= cs:40) v3.2: 25->15
  phoneRingChanceCap:         0.65, // cs:281 GetEffectiveRingChance() Mathf.Clamp(baseChance*mult + perkBonus, 0, 0.65)

  // ---- Assets/NewCss/Events/EventCalendarUI.cs --------------------------
  eventFreeDays:              3,    // cs:25 INITIAL_EVENT_FREE_DAYS
  eventIntervalMin:           1,    // cs:23 EVENT_INTERVAL_MIN
  eventIntervalMax:           2,    // cs:24 EVENT_INTERVAL_MAX (2026-08-25: 3->2 — NOT: bu sim'in nakit-akışı döngüsünde kullanılmıyor, salt dokümantasyon)
  eventSkipRentDays:          true, // cs: IsRentDay(currentDay) -> continue (rentIntervalDays'e göre, varsayılan 4)
  eventPoolSize:              16,   // cs:160-177 _allEvents

  // ---- Assets/Scripts/Quest/Manager/QuestManager.cs ---------------------
  dailyQuestOffered:          3,    // cs:17 DAILY_QUEST_COUNT
  dailyQuestAcceptLimit:      1,    // cs:691 HasAcceptedQuestToday() -> gunde 1
  questTierStart:             0,    // cs:70 NetworkVariable<int>(0) -> yalniz Easy
};

// ============================================================================
// VARSAYIMLAR (KOD'DA YOK -- oyuncu verimi / insan hizi). Hepsi ETIKETLI.
// ============================================================================
const ASSUMED = {
  // Kutu URETIM hizi: bir oyuncunun dakikada kac SATILABILIR kutu hazirladigi
  // (urun al -> paketle -> tasi). Kodda zamanli bir kapi YOK (Table.cs'de yalniz
  // ITEM_SPAWN_DELAY=0.1s), yani tamamen oyuncu becerisi. PlayerMovement.moveSpeed=5,
  // sprintSpeed=7 (PlayerMovement.cs:32,35) referans.
  boxesPerMinPerPlayer: { Normal: 2.0, Slow: 1.2, Fast: 3.0 },

  // On-stoklanmis kutuyu TIRA TASIMA hizi, uretime gore kac kat hizli.
  handoverSpeedup: 3.0,

  // Tir devir-arasi olu sure: exitDelay(5, KOD) + ort. respawnDelay(4, KOD) = 9s
  // KOD-DOGRULANMIS. Giris/cikis animator klip suresi kodda sayisallastirilmiyor.
  animBufferSeconds: 6,

  // Bir musteri servisinin SERI kuyrukta tuttugu sure (musterinin tezgaha
  // yurumesi + urunun masaya konmasi + ayrilmasi). Musteri sabri 15-20s
  // (ort 17.5) bu araligin ust sinirini veriyor.
  serviceCycleSeconds: { Normal: 18, Slow: 24, Fast: 14 },

  // Bir servisin oyuncudan CEKTIGI emek suresi (rafa/masaya kosma dahil).
  serviceLaborSeconds: { Normal: 15, Slow: 25, Fast: 11 },

  // STRICT bantta emegin tira ayrilan payi (kalani musteri servisine).
  laborShareTruck: 0.6,

  // Telefonu cevaplama orani (calan telefonun kacinin acildigi).
  phoneAnswerRate: { strict: 0.50, optimistic: 0.85 },

  // Hata oranlari.
  wrongDeliveryRate: { Normal: 0.12, Slow: 0.22, Fast: 0.07 }, // tira yanlis RENK kutu
  physicalDropRate:  { Normal: 0.05, Slow: 0.09, Fast: 0.03 }, // kutu yere dusme

  // Quest tamamlama: hedefe/kapasiteye orana gore turetilir (asagida), ama
  // "hedefi karsilayabiliyor olsa bile oyuncu unutur/vazgecer" surtunmesi:
  questExecutionFriction: { strict: 0.75, optimistic: 0.92 },

  // Magaza buyumesi (upgrade ile artan etkilesim noktasi / seviye). FAZ3
  // fiyatlandirmasi yapilmadan once TABAN kosuda buyume KAPALI (0).
  // v3.1: ARTIK VARSAYIM DEGIL -- SRC.activeInteractablesAtLevel0'dan (=5) geliyor.
  startingActiveInteractables: SRC.activeInteractablesAtLevel0,

  // ── MASA CEKISMESI (v3.1 YENI) ────────────────────────────────────────────
  // `S` = bir kutunun PAKETLEME MASASINI mesgul ettigi sure (urunu masaya koy ->
  // kutula -> paketlenmisi al). Kodda zamanli kapi YOK -> VARSAYIM.
  // Cevrim: Z + S = 60/boxesPerMin (Normal 30s). S masaya bagli mekanik kisim,
  // Z (yurume/karar) senaryoyla degisen kisim -> S senaryodan BAGIMSIZ tutuldu.
  // ⚠️ FAZ3 duyarlilik: S=4 ↔ 8 arasi "Paketleme Istasyonu" upgrade'inin degerini 4x
  // degistiriyor. Playtest'te olculmesi gereken 2. en onemli sayi.
  tableBusySeconds: 6,
};

// ============================================================================
// TUREVLER
// ============================================================================
const CARGO_VALUES = [];
for (let c = SRC.minCargo; c < SRC.maxCargoExclusive; c++) CARGO_VALUES.push(c); // {2,3,4,5}
const CARGO_AVG = CARGO_VALUES.reduce((a, b) => a + b, 0) / CARGO_VALUES.length;  // 3.5

const OVERHEAD_CODE = SRC.exitDelay + (SRC.respawnDelayRange[0] + SRC.respawnDelayRange[1]) / 2; // 9
const OVERHEAD_TOTAL = OVERHEAD_CODE + ASSUMED.animBufferSeconds;                                 // 15

const GAME_HOURS_PER_DAY   = SRC.dayEndHour - SRC.dayStartHour;                 // 11
const TRUCK_GAME_HOURS     = SRC.truckEndHour - SRC.truckStartHour;             // 9
const CUSTOMER_GAME_HOURS  = SRC.spawnEndHour - SRC.spawnStartHour;             // 9
// Telefon: saat DEGISIMINDE zar atilir; ziyaret edilen saatler 7..18, gecerli
// pencere [8,18) -> 8,9,...,17 = 10 zar.
const PHONE_ROLLS_PER_DAY  = SRC.phoneEndHour - SRC.phoneStartHour;             // 10

function dayDurationSec(day) {
  return day <= SRC.dynamicDurationStartDay
    ? SRC.realDurationInSeconds
    : SRC.realDurationInSeconds + (day - SRC.dynamicDurationStartDay) * SRC.dailyDurationIncrease;
}
function secPerGameHour(day) { return dayDurationSec(day) / GAME_HOURS_PER_DAY; }
function truckWindowSec(day) { return secPerGameHour(day) * TRUCK_GAME_HOURS; }
function customerWindowSec(day) { return secPerGameHour(day) * CUSTOMER_GAME_HOURS; }

function hangarStayFor(playerCount) {
  const a = SRC.hangarStayByPlayerCount;
  if (!a || a.length === 0) return SRC.hangarStayDurationLegacy;
  return a[Math.min(Math.max(playerCount - 1, 0), a.length - 1)];
}

// ============================================================================
// MASA CEKISMESI (v3.1 YENI) -- sonlu-kaynak kuyruk / machine-repairman
// ============================================================================
/**
 * Oyuncular paketleme masalarini PAYLASIYOR. Sahnede seviye 0'da YALNIZ 1 masa
 * aktif (SRC.packingTablesAtLevel0) ve `Table` TEK item tasiyor (Table.cs:57)
 * => 2+ oyuncu ayni masa icin siraya giriyor.
 *
 * Model: M/M/c//P sonlu-kaynak kuyrugu.
 *   P kaynak (oyuncu), c sunucu (masa), her oyuncu:
 *     Z sn "masa disi" is (urun getir, kutuyu tira/rafa tasi) + S sn masada.
 *   Durum n = masada+kuyrukta bekleyen oyuncu sayisi.
 *     dogum orani  (n -> n+1): (P-n)/Z
 *     olum  orani  (n -> n-1): min(n,c)/S
 *   p_n = p_0 * PROD_{k=1..n} [ (P-k+1) * S / (Z * min(k,c)) ]
 *   Verim X = SUM_n p_n * min(n,c)/S            [kutu/sn]
 *
 * Dondurulen deger CEKISME VERIMLILIGI: eta = X / (P/(Z+S)) in (0,1].
 * c >= P veya P == 1 iken eta = 1 (cekisme yok) -> v3.0 davranisina birebir doner.
 * Bu carpimsal duzeltme FAZ 1 yapisini bozmadan denetlenebilir kaliyor.
 */
function tableContentionEfficiency(playerCount, tables, tableBusySec, cycleSec) {
  const P = Math.max(1, Math.round(playerCount));
  const c = Math.max(1, Math.round(tables));
  const S = Math.max(0, tableBusySec);
  if (S === 0 || c >= P || P === 1) return 1;
  const Z = Math.max(1, cycleSec - S);   // masa disi sure; S >= cevrim ise tabana kirp

  const ratios = [1];                    // p_n / p_0
  for (let n = 1; n <= P; n++) {
    ratios[n] = ratios[n - 1] * ((P - n + 1) * S) / (Z * Math.min(n, c));
  }
  const norm = ratios.reduce((a, b) => a + b, 0);
  let X = 0;                             // tamamlama orani (kutu/sn)
  for (let n = 1; n <= P; n++) X += (ratios[n] / norm) * Math.min(n, c) / S;

  const uncontended = P / (Z + S);
  return X / uncontended;
}

/** Kac masa aktif: seviye 0'da 1, "Paketleme Istasyonu" L1 alinirsa 2 (tavan 2). */
function packingTablesForLevel(level) {
  return Math.min(SRC.packingTableTotal, SRC.packingTablesAtLevel0 + Math.max(0, level));
}

// ============================================================================
// TIR / HANGAR VERIM MODELI
// ============================================================================
/**
 * Tek hangarin bir gunde kac devir yaptigini ve devir basina kac kutu
 * teslim edilebildigini hesaplar.
 *
 * mode='strict'      : on-stok YOK. Kutular tirin hangarda oldugu pencerede
 *                      URETILIR. Tir "dolunca VEYA sure bitince" kalkar
 *                      (Truck.cs HangarTimerCoroutine + IsComplete).
 * mode='optimistic'  : kutular gun boyunca on-stoklanmis; tira TASIMA suresi
 *                      var ama uretim suresi yok -> handoverSpeedup kat hizli.
 *                      Toplam gunluk uretim ayrica ust sinir.
 */
function truckThroughput(playerCount, boxesPerMin, day, numHangars, mode, laborShare, packingTables, cargoValues) {
  const playersOnTrucks = playerCount * laborShare;
  // v3.1: kargo dizisi opsiyonel parametre (FAZ2 "P-bazli kargo" onerisini
  // olcebilmek icin). Verilmezse CANLI deger: Random.Range(2,6) -> {2,3,4,5}.
  const CARGO = (cargoValues && cargoValues.length) ? cargoValues : CARGO_VALUES;
  // v3.1: MASA CEKISMESI. Uretim, paylasilan paketleme masasindan seri geciyor.
  const tables   = packingTables === undefined ? SRC.packingTablesAtLevel0 : packingTables;
  const cycleSec = 60 / boxesPerMin;                                            // Z + S
  const eta      = tableContentionEfficiency(playerCount, tables, ASSUMED.tableBusySeconds, cycleSec);
  const prodRate = (boxesPerMin * playersOnTrucks) / 60 * eta;                  // kutu/sn (uretim)
  const fillRate = mode === 'optimistic' ? prodRate * ASSUMED.handoverSpeedup : prodRate;
  const tws      = truckWindowSec(day);
  const stay     = hangarStayFor(playerCount);

  let sumDeliverable = 0, sumCycle = 0, fullCount = 0;
  for (const cargo of CARGO) {
    const fillTime  = fillRate > 0 ? cargo / fillRate : Infinity;
    const dwell     = Math.min(stay, fillTime);       // "dolunca VEYA sure bitince"
    const delivered = Math.min(cargo, fillRate * stay);
    sumDeliverable += delivered;
    sumCycle       += dwell + OVERHEAD_TOTAL;
    if (fillTime <= stay) fullCount++;
  }
  const avgDeliverable = sumDeliverable / CARGO.length;
  const avgCycle       = sumCycle / CARGO.length;
  const trucksPerDay   = (tws / avgCycle) * numHangars;
  let boxesPerDay      = trucksPerDay * avgDeliverable;

  // URETIM TAVANI her iki bantta uygulanir. Aksi halde numHangars>1 ayni oyuncu
  // havuzunu birden fazla kez sayardi (paralel hangarlar ayni emegi paylasir).
  //  - optimistic: on-stok yapilabilir -> uretim penceresi TUM gun (7-18).
  //  - strict    : on-stok yok -> uretim yalniz tir penceresinde (8-17).
  const productionCapPerDay = prodRate * (mode === 'optimistic' ? dayDurationSec(day) : tws);
  boxesPerDay = Math.min(boxesPerDay, productionCapPerDay);
  // Tir sayisi da uretimle tutarli olmali (tavan bagladiysa daha az tir dolar).
  const trucksPerDayEffective = avgDeliverable > 0
    ? Math.min(trucksPerDay, boxesPerDay / avgDeliverable)
    : 0;

  return {
    trucksPerDay: trucksPerDayEffective,
    trucksPerDayCycleBound: trucksPerDay,
    boxesPerDay,
    avgCycleSec: avgCycle,
    avgDeliverablePerTruck: avgDeliverable,
    fullTruckFraction: fullCount / CARGO.length,
    fullTrucksPerDay: trucksPerDayEffective * (fullCount / CARGO.length),
    productionCapPerDay,
    hangarStaySec: stay,
    // Mekanik ust sinir: doldurma ANINDA olsaydi gunde kac tir islenebilirdi
    absoluteTruckCeiling: (tws / OVERHEAD_TOTAL) * numHangars,
    // v3.1 masa cekismesi teshisi
    tableContentionEta: eta,
    packingTables: tables,
  };
}

// ============================================================================
// MUSTERI / PRESTIJ VERIM MODELI  (SERI KUYRUK)
// ============================================================================
function customerDemand(playerCount, activeInteractables, storeLevel, eventCustomerMult = 1) {
  const capacityBase = activeInteractables * SRC.shelfMultiplier + storeLevel * SRC.levelMultiplier;
  const avgVariance  = (SRC.minVariance + SRC.maxVariance) / 2; // -2..3 -> 0.5
  const pMult        = 1 + (playerCount - 1) * SRC.playerCountMultCoeff;
  const raw = (capacityBase + avgVariance) * eventCustomerMult * pMult;
  return Math.min(SRC.maxCustomersPerDay, Math.max(SRC.minCustomersPerDay, Math.round(raw)));
}

/**
 * Seri kuyrukta gunde kac musteri fiilen servis edilebilir?
 *  - serviceSlots  : mekanik tavan (kuyruk serisi, CustomerAI.cs:582)
 *  - laborCapacity : takimin fiilen yapabildigi servis sayisi
 *  - spawned       : kuyruk (maxQueueSize) sayesinde servis edilebilenden
 *                    birkac fazla musteri sahneye girer; girip servis
 *                    edilemeyen sabri dolar -> customerLostPrestigePenalty.
 *  - Kuyruk DOLUYKEN scheduled spawn ATLANIR (CustomerManager.cs:516) ->
 *    o musteriler hic gelmez, CEZA URETMEZ.
 */
function customerThroughput(playerCount, day, demand, scenario, laborShare, mode, serviceStations) {
  const cws = customerWindowSec(day);
  // v3.1: PARALEL SERVIS ISTASYONU sayisi. CANLI deger 1 -- CustomerAI.cs:582
  // yalnizca IsFirstInQueue iken BeginService yapiyor (seri). FAZ2 "2 paralel
  // istasyon" onerisini olcebilmek icin parametre; verilmezse canli deger.
  const stations = Math.max(1, serviceStations || SRC.serviceStations);
  const serviceSlots = (cws / ASSUMED.serviceCycleSeconds[scenario]) * stations;
  const playersOnCustomers = mode === 'optimistic'
    ? playerCount                      // emek cakismasi yok varsayimi
    : playerCount * (1 - laborShare);
  const laborCapacity = (playersOnCustomers * cws) / ASSUMED.serviceLaborSeconds[scenario];

  const served  = Math.min(demand, serviceSlots, laborCapacity);
  const spawned = Math.min(demand, serviceSlots + SRC.maxQueueSize);
  const lost    = Math.max(0, spawned - served);
  const skipped = Math.max(0, demand - spawned); // hic spawn olmayan (cezasiz)

  return { served, spawned, lost, skipped, serviceSlots, laborCapacity };
}

// ============================================================================
// TELEFON
// ============================================================================
function phoneIncome(mode, eventSupportActive = false) {
  const chance = Math.min(
    SRC.phoneRingChanceCap,
    SRC.phoneRingChancePerHour * (eventSupportActive ? SRC.phoneRingEventMultiplier : 1)
  );
  const rings   = PHONE_ROLLS_PER_DAY * chance;
  const answers = rings * ASSUMED.phoneAnswerRate[mode];
  return {
    rings,
    answers,
    money: answers * SRC.callMoneyReward,
    prestige: answers * SRC.callPrestigeReward,
    ringOccupancySec: rings * SRC.phoneRingDuration,
  };
}

// ============================================================================
// QUEST -- 30 CANLI ASSET (Assets/Resources/Quests/)
// ============================================================================
// !! ODUL KOLONU 2026-08-30 (ROUND 10) RESENKRONLANDI. Onceki tablo `975f011`
//    (2026-08-06, FAZ4 SD#6) oncesi grup-bazli (base/premium/phone) odulleri
//    tasiyordu -> BAYATTI. Canli asset'ler TIER-DUZ:
//       Easy   mR=28  pR=1.4  mP=15  pP=0.8
//       Medium mR=60  pR=3.0  mP=27  pP=1.36
//       Hard   mR=150 pR=7.5  mP=53  pP=2.66
//    (dogrulama: Assets/Resources/Quests/*.asset, 30/30 dosya grep'lendi)
//    targetCount / type / colorLocked kolonlari DEGISMEDI (zaten dogruydu).
// Alanlar: tier(0=Easy,1=Medium,2=Hard), type (QuestEnums.cs:28-37):
//   1=PlaceBoxOnShelf, 2=CompleteTruck, 3=PackToy, 4=AnswerPhone
// colorLocked = requirement.requireSpecificBoxType==1 (renk/kategori filtresi)
// ROUND 11 RESYNC (2026-08-30): Round 10 U7 (prestij odul+ceza x0.4) CANLI
// asset'lere UYGULANDI -> 30/30 asset grep'lendi, tablo asagida canli hali.
const QR = {
  easy:   { mR: 28,  pR: 0.6, mP: 15, pP: 0.32 },
  medium: { mR: 60,  pR: 1.2, mP: 27, pP: 0.55 },
  hard:   { mR: 150, pR: 3.0, mP: 53, pP: 1.05 },
};
const QUEST_ASSETS = [
  // --- EASY (11) ---
  { id: 'easy_truck_1',      file: 'Q_Easy_1_Truck',       tier: 0, type: 2, target: 1,  colorLocked: false, ...QR.easy },
  { id: 'easy_shelf_4',      file: 'Q_Easy_2_Shelf',       tier: 0, type: 1, target: 4,  colorLocked: false, ...QR.easy },
  { id: 'easy_shelf_red',    file: 'Q_Easy_3_ShelfRed',    tier: 0, type: 1, target: 2,  colorLocked: true,  ...QR.easy },
  { id: 'easy_pack_4',       file: 'Q_Easy_4_Pack',        tier: 0, type: 3, target: 4,  colorLocked: false, ...QR.easy },
  { id: 'easy_pack_toy',     file: 'Q_Easy_5_PackToy',     tier: 0, type: 3, target: 2,  colorLocked: true,  ...QR.easy },
  { id: 'easy_phone_2',      file: 'Q_Easy_6_Phone',       tier: 0, type: 4, target: 1,  colorLocked: false, ...QR.easy }, // U8: 2->1 UYGULANDI
  { id: 'easy_shelf_yellow', file: 'Q_Easy_7_ShelfYellow', tier: 0, type: 1, target: 2,  colorLocked: true,  ...QR.easy },
  { id: 'easy_shelf_blue',   file: 'Q_Easy_8_ShelfBlue',   tier: 0, type: 1, target: 2,  colorLocked: true,  ...QR.easy },
  { id: 'easy_pack_cloth',   file: 'Q_Easy_9_PackCloth',   tier: 0, type: 3, target: 2,  colorLocked: true,  ...QR.easy },
  { id: 'easy_pack_glass',   file: 'Q_Easy_10_PackGlass',  tier: 0, type: 3, target: 2,  colorLocked: true,  ...QR.easy },
  { id: 'easy_shelf_6',      file: 'Q_Easy_11_ShelfBig',   tier: 0, type: 1, target: 6,  colorLocked: false, ...QR.easy },
  // --- MEDIUM (10) ---
  { id: 'med_truck_2',       file: 'Q_Medium_1_Truck',     tier: 1, type: 2, target: 2,  colorLocked: false, ...QR.medium },
  { id: 'med_shelf_7',       file: 'Q_Medium_2_Shelf',     tier: 1, type: 1, target: 7,  colorLocked: false, ...QR.medium },
  { id: 'med_shelf_blue',    file: 'Q_Medium_3_ShelfBlue', tier: 1, type: 1, target: 3,  colorLocked: true,  ...QR.medium },
  { id: 'med_pack_7',        file: 'Q_Medium_4_Pack',      tier: 1, type: 3, target: 7,  colorLocked: false, ...QR.medium },
  { id: 'med_pack_cloth',    file: 'Q_Medium_5_PackCloth', tier: 1, type: 3, target: 3,  colorLocked: true,  ...QR.medium },
  { id: 'med_phone_3',       file: 'Q_Medium_6_Phone',     tier: 1, type: 4, target: 2,  colorLocked: false, ...QR.medium }, // U8: 3->2 UYGULANDI
  { id: 'med_shelf_yellow',  file: 'Q_Medium_7_ShelfYellow',tier: 1, type: 1, target: 3, colorLocked: true,  ...QR.medium },
  { id: 'med_shelf_red',     file: 'Q_Medium_8_ShelfRed',  tier: 1, type: 1, target: 3,  colorLocked: true,  ...QR.medium },
  { id: 'med_pack_toy',      file: 'Q_Medium_9_PackToy',   tier: 1, type: 3, target: 3,  colorLocked: true,  ...QR.medium },
  { id: 'med_pack_glass',    file: 'Q_Medium_10_PackGlass',tier: 1, type: 3, target: 3,  colorLocked: true,  ...QR.medium },
  // --- HARD (9) ---
  { id: 'hard_truck_3',      file: 'Q_Hard_1_Truck',       tier: 2, type: 2, target: 3,  colorLocked: false, ...QR.hard },
  { id: 'hard_shelf_10',     file: 'Q_Hard_2_Shelf',       tier: 2, type: 1, target: 12, colorLocked: false, ...QR.hard },
  { id: 'hard_shelf_yellow', file: 'Q_Hard_3_ShelfYellow', tier: 2, type: 1, target: 5,  colorLocked: true,  ...QR.hard },
  { id: 'hard_pack_10',      file: 'Q_Hard_4_Pack',        tier: 2, type: 3, target: 12, colorLocked: false, ...QR.hard },
  { id: 'hard_pack_glass',   file: 'Q_Hard_5_PackGlass',   tier: 2, type: 3, target: 5,  colorLocked: true,  ...QR.hard },
  { id: 'hard_shelf_blue',   file: 'Q_Hard_6_ShelfBlue',   tier: 2, type: 1, target: 5,  colorLocked: true,  ...QR.hard },
  { id: 'hard_shelf_red',    file: 'Q_Hard_7_ShelfRed',    tier: 2, type: 1, target: 5,  colorLocked: true,  ...QR.hard },
  { id: 'hard_pack_toy',     file: 'Q_Hard_8_PackToy',     tier: 2, type: 3, target: 5,  colorLocked: true,  ...QR.hard },
  { id: 'hard_pack_cloth',   file: 'Q_Hard_9_PackCloth',   tier: 2, type: 3, target: 5,  colorLocked: true,  ...QR.hard },
];

// Renk-kilitli quest'in ilerleme orani: tir renk torbasi (TruckSpawner
// DrawNextBagColor, 3 renk) ve musteri renk torbasi TAM 1/3.
const COLOR_LOCK_FACTOR = 1 / 3;

/**
 * Bir quest'in gunluk kapasite ile tamamlanma olasiligi.
 * capacity: { trucks, shelfPlacements, packedBoxes, phoneAnswers }
 */
function questCompletionProb(q, capacity, mode) {
  let supply;
  switch (q.type) {
    case 1: supply = capacity.shelfPlacements; break;
    case 2: supply = capacity.trucks; break;
    case 3: supply = capacity.packedBoxes; break;
    case 4: supply = capacity.phoneAnswers; break;
    default: supply = 0;
  }
  if (q.colorLocked) supply *= COLOR_LOCK_FACTOR;
  if (supply <= 0) return 0;

  const ratio = supply / q.target;
  // Doygunluk egrisi: ratio<1 -> hizla duser, ratio>=1.5 -> plato.
  let base;
  if (ratio >= 1.5)      base = 0.95;
  else if (ratio >= 1.0) base = 0.60 + 0.70 * (ratio - 1.0);   // 1.0->0.60, 1.5->0.95
  else                   base = 0.60 * Math.pow(ratio, 1.8);    // 0.5->0.17, 0.8->0.41
  return Math.min(1, base * ASSUMED.questExecutionFriction[mode]);
}

/**
 * Gunun quest kararini modeller (v5, ROUND 10 -- YENIDEN YAZILDI).
 *
 * ESKI (v4) MODEL YANLISTI (Round 8 S10b): tum havuzu EV'ye siralayip ilk 3'un
 * ORTALAMASINI aliyordu -> havuz seyrelmesine KOR. Canli kod
 * (`QuestManager.SelectDailyQuestsStratified` cs:496-544) her gun RASTGELE
 * teklif uretiyor; oyuncu gunde EN FAZLA 1 kabul ediyor (`HasAcceptedQuestToday`).
 * Yani dogru metrik "ilk 3'un ortalamasi" degil, RASTGELE cekilen N teklifin
 * MAKSIMUMU (ve maksimum bile negatifse KABUL ETMEME).
 *
 * Teklif uretimi (cs:503-541):
 *   - t = 0..min(questTier,2): her acik tier'dan 1 tane UNIFORM cekilir (D1)
 *   - kalan slotlar (dailyQuestCount - acikTierSayisi) tier<=questTier havuzundan
 *     tekrarsiz UNIFORM doldurulur
 * Bu fonksiyon slotlari BAGIMSIZ (tekrarli) varsayar -> tekrarsizliktan gelen
 * kucuk cesitlilik kaybi ihmal edilir (E[max] hafif ALT SINIR, |sapma| < %2).
 *
 * E[max] siralama-istatistigi ile TAM hesaplanir:
 *   P(en iyi teklif = rank r) = PROD_i C_i(r) - PROD_i C_i(r-1)
 * Boylece "tier acmak havuzu seyreltir" etkisi dogal olarak cikar.
 *
 * opts:
 *   dailyQuestCount   -- QuestManager.DAILY_QUEST_COUNT (cs:17), varsayilan 3
 *   assets            -- quest asset listesi (override edilebilir)
 *   prestigeScale     -- prestij odul+cezasini olcekler (Round 8 onerisi x0.4)
 *   noPenalty         -- gun-16 settlement'i: ceza sonucsuz (Round 8 S8)
 */
/**
 * ROUND 11 (2026-08-30) -- TEKLIF SLOTU URETICISI (varyant motoru).
 *
 * Her slot = { entries: [{q, p}], k } biciminde bir DAGILIM. `k` slot icinde
 * yapilan bagimsiz cekilis sayisi (best-of-K); k=1 canli davranis.
 *
 * !! UI SLOT TAVANI (qa/kontrol 2026-08-30 bulgusu): sahnede
 * (`The Main Office.unity:90392-90395`) TAM 3 adet QuestSlotUI var ve
 * `QuestUIController.RefreshSlots` `Mathf.Min(questSlots.Count, DailyQuestCount)`
 * ile KIRPIYOR. `SelectDailyQuestsStratified` garanti tier picklerini HER ZAMAN
 * listenin BASINA koydugu icin, dailyQuestCount 3'un uzerine cikarilsa bile
 * oyuncunun GORDUGU ilk 3 teklif AYNI kaliyor -> Round 10 U6 (3 -> 3+tier)
 * OYUNDA NO-OP. `uiSlots` bunu modelliyor.
 *
 * Varyantlar:
 *   'live'          canli kod: t=0..maxTier her tier'dan 1 garanti + kalanlar
 *                   tum acik havuzdan uniform dolgu.
 *   'showcase'      1 slot EN UST tier'dan (vitrin) + kalan slotlar
 *                   tier<=maxTier-1 havuzundan uniform.
 *   'showcaseEasy'  1 slot en ust tier'dan + kalanlar YALNIZ tier 0'dan.
 *   'tierWeighted'  1 slot en ust tier'dan + kalanlar SABIT tier agirliklariyla
 *                   (selectionOpts.fillTierWeights, acik tierlere normalize).
 *   'bestOfTier'    'live' + her slot icin k = 1 + (maxTier - slotTier)
 *                   (alt tier slotlari, kaybettikleri cekilisi geri kazanir).
 *   'liveBestOfK'   'live' + her slota sabit k = selectionOpts.bestOfK.
 */
/**
 * ROUND 11: "K aday cek, EN IYISINI teklif et" donusumu -- ama secim olcutu
 * SABIT (oyun-ici hesaplanabilir) bir skor. score DUSUK = daha iyi (kolay).
 * P(secilen = j) = Q_j^K - Q_{j+1}^K,  Q_j = j'inci ve daha kotu olanlarin toplam olasiligi.
 * (EV-tabanli best-of-K icin bu gerekmez: CDF^K yeterli -- bkz. slot.k.)
 */
function staticBestOfKPmf(entries, k, scoreOf) {
  if (!k || k <= 1) return entries;
  const sorted = entries.slice().sort((a, b) => scoreOf(a.q) - scoreOf(b.q));
  const m = sorted.length;
  const Q = new Array(m + 1).fill(0);
  for (let j = m - 1; j >= 0; j--) Q[j] = Q[j + 1] + sorted[j].p;
  return sorted.map((e, j) => ({ q: e.q, p: Math.pow(Q[j], k) - Math.pow(Q[j + 1], k) }));
}

/**
 * SABIT ZORLUK SKORU (oyun-ici hesaplanabilir proxy).
 * Tier icinde odul/ceza DUZ (28/15, 60/27, 150/53) oldugu icin tier ici EV
 * siralamasi = tamamlanma olasiligi siralamasi = "zorluk" siralamasi.
 * zorluk = effectiveTarget * (renk-kilitli ? 3 : 1) / tipArzAgirligi
 * Tip arz agirliklari 16 hucre x 16 gun kapasite ortalamalarindan kalibre edildi
 * (shelf/pack ayni havuz, tir cok daha kit, telefon kullanim-oranina bagli).
 */
// 16 hucre x 16 gun ortalamalarinin MEDYANI (raf/paket = 1.0 normalize):
//   raf 8.34/gun, tir 2.97/gun -> 0.356, telefon 2.0/gun -> 0.24
const QUEST_TYPE_SUPPLY_WEIGHT = { 1: 1.0, 2: 0.356, 3: 1.0, 4: 0.24 };
function questStaticDifficulty(q) {
  return q.target * (q.colorLocked ? 3 : 1) / (QUEST_TYPE_SUPPLY_WEIGHT[q.type] || 1);
}

function buildQuestSlots(pool, maxTier, dailyQuestCount, selection, selOpts, uiSlots) {
  uiSlots = uiSlots || 3;
  const uni = (arr) => arr.map(q => ({ q, p: 1 / arr.length }));
  const tierPool = (t) => pool.filter(q => q.tier === t);
  const upTo = (t) => pool.filter(q => q.tier <= t);
  const n = Math.max(1, Math.min(dailyQuestCount, uiSlots));
  const slots = [];

  const LIVE_LIKE = ['live', 'bestOfTier', 'liveBestOfK', 'bestOfTierStatic', 'easyBestOnly',
                     'easyBestOnlyStatic', 'allBestOfTierStatic', 'ladderK'];
  if (LIVE_LIKE.includes(selection)) {
    const statik = selection.endsWith('Static');
    const scoreFn = selOpts.scoreFn || questStaticDifficulty;
    for (let t = 0; t <= maxTier; t++) {
      const tp = tierPool(t);
      if (!tp.length) continue;
      let k = 1;
      if (selection === 'bestOfTier' || selection === 'bestOfTierStatic') k = 1 + (maxTier - t);
      if (selection === 'easyBestOnly' || selection === 'easyBestOnlyStatic') k = (t === 0) ? 1 + maxTier : 1;
      if (selection === 'allBestOfTierStatic') k = 1 + maxTier;
      // ROUND 11 'ladderK': K(t) = (maxTier==0) ? 1 : (uiSlots - t)  -> 3/2/1
      if (selection === 'ladderK') k = (maxTier === 0) ? 1 : Math.max(1, uiSlots - t);
      if (selection === 'liveBestOfK') k = selOpts.bestOfK || 1;
      if (selOpts.kCap) k = Math.min(k, selOpts.kCap);
      // ROUND 11: tier-0 slotunun k'sini ayrica zorla (dolgu slotunun kaybini telafi).
      if (selOpts.easyK && t === 0 && maxTier >= 1) k = Math.max(k, selOpts.easyK);
      slots.push(statik
        ? { entries: staticBestOfKPmf(uni(tp), k, scoreFn), k: 1, tier: t }
        : { entries: uni(tp), k, tier: t });
    }
    const fill = Math.max(0, dailyQuestCount - slots.length);
    for (let i = 0; i < fill; i++) {
      slots.push({ entries: uni(pool), k: selection === 'liveBestOfK' ? (selOpts.bestOfK || 1) : 1, tier: -1 });
    }
    return slots.slice(0, n);
  }

  // --- vitrin tabanli varyantlar: 1 slot en ust tier, kalan n-1 slot "taban" havuz
  const top = tierPool(maxTier);
  slots.push({ entries: uni(top.length ? top : pool), k: 1, tier: maxTier });

  let baseEntries;
  if (selection === 'showcaseEasy') {
    baseEntries = uni(tierPool(0));
  } else if (selection === 'tierWeighted') {
    const w = selOpts.fillTierWeights || [0.60, 0.25, 0.15];
    let tot = 0;
    for (let t = 0; t <= maxTier; t++) if (tierPool(t).length) tot += w[t];
    baseEntries = [];
    for (let t = 0; t <= maxTier; t++) {
      const tp = tierPool(t);
      if (!tp.length) continue;
      for (const q of tp) baseEntries.push({ q, p: (w[t] / tot) / tp.length });
    }
  } else { // 'showcase'
    const lower = upTo(Math.max(0, maxTier - 1));
    baseEntries = uni(lower.length ? lower : pool);
  }
  const kBase = selOpts.baseBestOfK || 1;
  for (let i = 1; i < n; i++) slots.push({ entries: baseEntries, k: kBase, tier: -1 });
  return slots;
}

function questDailyDecision(questTier, capacity, mode, opts = {}) {
  const {
    dailyQuestCount = SRC4.dailyQuestCount,
    assets = QUEST_ASSETS,
    prestigeScale = 1,
    noPenalty = false,
    // ROUND 11
    selection = 'live',
    selectionOpts = {},
    uiSlots = SRC4.questUiSlots,
  } = (opts && typeof opts === 'object') ? opts : {};

  const maxTier = Math.min(questTier, 2);
  const pool = assets.filter(q => q.tier <= maxTier);
  if (pool.length === 0) return { accepted: false, acceptProb: 0, money: 0, prestige: 0, pick: null };

  // 1) Her quest'in EV'si
  const evs = pool.map((q, i) => {
    const c = questCompletionProb(q, capacity, mode);
    return {
      q, c, idx: i,
      money:    noPenalty ? c * q.mR : c * q.mR - (1 - c) * q.mP,
      prestige: prestigeScale * (noPenalty ? c * q.pR : c * q.pR - (1 - c) * q.pP),
    };
  });
  // Deterministik siralama (esitlikler idx ile kirilir) -> rank benzersiz
  const ranked = evs.slice().sort((a, b) => (a.money - b.money) || (a.idx - b.idx));
  const rankOf = new Map();
  ranked.forEach((e, r) => rankOf.set(e.q, r));

  // 2) Slot dagilimlari (ROUND 11: varyant destegi + UI SLOT TAVANI)
  const slots = buildQuestSlots(pool, maxTier, dailyQuestCount, selection, selectionOpts, uiSlots);

  // 3) C_i(r) = P(slot i, rank <= r).  bestOfK: slot icinde K bagimsiz cekilisin
  //    en iyisi alinir -> CDF^K (siralama istatistigi).
  const N = ranked.length;
  const C = slots.map(sl => {
    const cnt = new Array(N + 1).fill(0);
    for (const e of sl.entries) cnt[rankOf.get(e.q) + 1] += e.p;
    for (let r = 1; r <= N; r++) cnt[r] += cnt[r - 1];
    if (sl.k && sl.k > 1) for (let r = 0; r <= N; r++) cnt[r] = Math.pow(cnt[r], sl.k);
    return cnt;             // C[r+1] = P(rank <= r)
  });
  const prod = (r) => {     // r = -1 -> 0
    if (r < 0) return 0;
    let p = 1;
    for (const c of C) p *= c[r + 1];
    return p;
  };

  // 4) E[secilen] -- yalniz EV>0 olan kazananlar kabul edilir
  let money = 0, prestige = 0, acceptProb = 0, bestId = null, bestP = -1;
  let prev = 0;
  for (let r = 0; r < N; r++) {
    const cur = prod(r);
    const pWin = cur - prev;
    prev = cur;
    if (pWin <= 1e-12) continue;
    const e = ranked[r];
    if (pWin > bestP) { bestP = pWin; bestId = e.q.id; }
    if (e.money > 0) { money += pWin * e.money; prestige += pWin * e.prestige; acceptProb += pWin; }
  }

  return {
    accepted: acceptProb > 0, acceptProb,
    money, prestige, pick: bestId,
    bestEV: ranked[N - 1], poolSize: pool.length, slots: slots.length,
  };
}

// ============================================================================
// ANA SIMULASYON
// ============================================================================
function runSim(playerCount, opts = {}) {
  const {
    scenario = 'Normal',            // 'Normal' | 'Slow' | 'Fast'
    mode = 'optimistic',            // 'optimistic' | 'strict'
    numHangars = SRC.hangarsAtLevel0,
    questsEnabled = true,
    questTier = SRC.questTierStart,
    phoneEnabled = true,
    upgradeSpendRatio = 0,          // 0 = TABAN kosu (FAZ3 oncesi)
    packingTables = SRC.packingTablesAtLevel0,  // v3.1: 1 = seviye 0, 2 = Paketleme L1
    serviceStations = SRC.serviceStations,      // v3.1: 1 = canli (seri), 2 = FAZ2 onerisi
    cargoValues = null,                         // v3.1: null = canli {2,3,4,5}
    label = '',
  } = opts;

  const boxesPerMin = ASSUMED.boxesPerMinPerPlayer[scenario];
  const laborShare  = ASSUMED.laborShareTruck;
  const baseRent    = SRC.baseRentByPlayerCount[playerCount - 1];

  let cash = Math.round(SRC.baseStartingMoney * Math.pow(SRC.moneyMultiplierPerPlayer, playerCount - 1));
  let prestige = SRC.startingPrestige;
  let rentCycle = 0, graceUsed = false;
  let activeInteractables = ASSUMED.startingActiveInteractables;
  let storeLevel = SRC.storeLevelStart;
  let totalUpgradeValue = 0;
  let bankrupt = false, bankruptDay = null;
  let prestigeCapDay = null;
  let questSettlePending = null;   // {money, prestige} -- BIR SONRAKI gun yatar
  const rows = [];

  for (let day = 1; day <= SRC.maxDays; day++) {
    // --- 0) Gecen gunun quest'i simdi yatar (kira'dan SONRA gelmis olur) ---
    let questSettledMoney = 0, questSettledPrestige = 0;
    if (questSettlePending) {
      questSettledMoney = questSettlePending.money;
      questSettledPrestige = questSettlePending.prestige;
      questSettlePending = null;
    }

    // --- 1) Verim ---
    const tt = truckThroughput(playerCount, boxesPerMin, day, numHangars, mode, laborShare, packingTables, cargoValues);
    const demand = customerDemand(playerCount, activeInteractables, storeLevel);
    const ct = customerThroughput(playerCount, day, demand, scenario, laborShare, mode, serviceStations);
    const ph = phoneEnabled ? phoneIncome(mode) : { rings: 0, answers: 0, money: 0, prestige: 0 };

    // --- 2) Tir geliri (TEK para kaynagi) ---
    const boxesToTruck   = tt.boxesPerDay;
    const wrongRate      = ASSUMED.wrongDeliveryRate[scenario];
    const dropRate       = ASSUMED.physicalDropRate[scenario];
    const wrongBoxes     = boxesToTruck * wrongRate;
    const correctBoxes   = boxesToTruck - wrongBoxes;
    const droppedBoxes   = boxesToTruck * dropRate;

    const prestigeTier = Math.floor(prestige / SRC.prestigePerBonus);
    const rewardActual = SRC.rewardPerBox + prestigeTier * SRC.bonusPerTier;

    const truckRevenue = correctBoxes * rewardActual;
    const wrongCost    = wrongBoxes * SRC.penaltyPerBox;
    const dropCost     = droppedBoxes * SRC.boxDropMoneyPenalty;

    // --- 3) Quest karari (bugun kabul, YARIN yatar) ---
    let questDecision = { accepted: false, money: 0, prestige: 0, pick: null };
    if (questsEnabled) {
      const capacity = {
        trucks: tt.fullTrucksPerDay,
        // Rafa koyma ve paketleme, tira giden kutularin ONCESINDEKI adimlar;
        // gunluk uretim kapasitesi ile sinirli.
        shelfPlacements: tt.productionCapPerDay,
        packedBoxes: tt.productionCapPerDay,
        phoneAnswers: ph.answers,
      };
      questDecision = questDailyDecision(questTier, capacity, mode);
      if (questDecision.accepted) {
        questSettlePending = { money: questDecision.money, prestige: questDecision.prestige };
      }
    }

    const grossIncome = truckRevenue + ph.money + questSettledMoney;
    const grossCost   = wrongCost + dropCost;
    const netEarnings = grossIncome - grossCost;

    let cashBeforeRent = Math.max(0, cash + netEarnings);

    // --- 4) Prestij ---
    prestige += ct.served * SRC.customerServedPrestigeBonus;
    prestige += ct.lost   * SRC.customerLostPrestigePenalty;
    prestige += wrongBoxes   * SRC.wrongDeliveryPrestigePenalty;
    prestige += droppedBoxes * SRC.boxDropPrestigePenalty;
    prestige += ph.prestige;
    prestige += questSettledPrestige;
    if (prestige >= SRC.maxPrestige && prestigeCapDay === null) prestigeCapDay = day;
    prestige = Math.max(0, Math.min(SRC.maxPrestige, prestige));

    // --- 5) Kira (gun sonu, quest ODULUNDEN ONCE) ---
    let rentAmount = 0, rentPaid = 0, event = '';
    const isRentDay = day % SRC.rentIntervalDays === 0;
    if (isRentDay) {
      rentAmount = Math.round(baseRent * Math.pow(SRC.rentGrowthMultiplier, rentCycle) * SRC.rentScaledMultiplier);
      if (cashBeforeRent >= rentAmount) {
        rentPaid = rentAmount; cash = cashBeforeRent - rentPaid; rentCycle++;
      } else if (!graceUsed) {
        rentPaid = Math.round(cashBeforeRent * SRC.gracePaymentPercent);
        cash = cashBeforeRent - rentPaid; graceUsed = true; rentCycle++; event = 'GRACE';
      } else {
        bankrupt = true; bankruptDay = day; event = 'IFLAS'; cash = cashBeforeRent;
      }
    } else {
      cash = cashBeforeRent;
    }

    // --- 6) Yeniden yatirim (TABAN kosuda kapali) ---
    if (!bankrupt && upgradeSpendRatio > 0 && !isRentDay && cash > 200) {
      const spend = (cash - 200) * upgradeSpendRatio;
      cash -= spend; totalUpgradeValue += spend;
      activeInteractables = ASSUMED.startingActiveInteractables + Math.floor(totalUpgradeValue / 300);
      storeLevel = SRC.storeLevelStart + Math.floor(totalUpgradeValue / 600);
    }

    rows.push({
      gun: day,
      sureSn: dayDurationSec(day),
      tirGun: +tt.trucksPerDay.toFixed(2),
      kutuGun: +boxesToTruck.toFixed(2),
      odulKutu: rewardActual,
      tirGeliri: Math.round(truckRevenue),
      telefon: Math.round(ph.money),
      questYatan: Math.round(questSettledMoney),
      cezalar: -Math.round(grossCost),
      netGelir: Math.round(netEarnings),
      talep: demand,
      servisEdilen: +ct.served.toFixed(1),
      kacan: +ct.lost.toFixed(1),
      hicGelmeyen: +ct.skipped.toFixed(1),
      prestij: +prestige.toFixed(2),
      kira: rentAmount,
      kasa: Math.round(cash),
      olay: event,
    });

    if (bankrupt) break;
    if (prestige <= 0) { bankrupt = true; bankruptDay = day; break; }
  }

  const cumNet = rows.reduce((a, r) => a + r.netGelir, 0);
  return {
    playerCount, label, scenario, mode, numHangars,
    bankrupt, bankruptDay, prestigeCapDay, rows,
    finalCash: rows[rows.length - 1]?.kasa,
    finalPrestige: rows[rows.length - 1]?.prestij,
    cumulativeNet: cumNet,
    avgDailyNet: +(cumNet / rows.length).toFixed(1),
  };
}

// ============================================================================
// PLATEUP MUSTERI/TELEFON MODELI (PROPOSED, 2026-08-29)
// ============================================================================
// plans/plateup-musteri-telefon.md -- economist turu (kod yazmadan onceki
// on kosul, "Is 0"). ASAGIDAKI DEGERLER HENUZ KODA/ASSET'E YAZILMADI; bu
// turun teslim edilecek onerisidir. Gameplay departmanina devredilmeden once
// burada TEK NOKTADAN hesaplanip dogrulanir.
//
// TUReTIM MANTIGI:
//  - Gun penceresi degismiyor: customerWindowSec(day) (spawnStartHour..spawnEndHour,
//    canli 8-17, 9 oyun-saati) hala "musteri isinin sigmasi gereken butce".
//  - SERI SERVIS TAVANI = min(istasyon-tavani, emek-tavani), FAZ2/FAZ1'de
//    kod-dogrulanan ayni formul (bkz. customerThroughput). STRICT senaryo
//    (emegin %60'i tira gidiyor) "kotumser ama gercekci" taban olarak alindi.
//  - Kota = SAFETY(0.85) x STRICT tavan -- yani STRICT oyunda bile %85
//    dolduruluyor, %15 tampon kaliyor (17:30 baskisi ancak STRICT'ten DAHA
//    KOTU -- ör. Slow senaryo, event, dikkat dagatan playtest anlarinda -
//    gercek hale gelir; "her gun kesin ceza" riski yaratmaz).
//
// DUZELTME (2026-08-29, koordinator geri bildirimi -- KANITLA DOGRULANDI):
// "kota-para baglantisi yok" onceki notu HATALIYDI. Kod denetimi:
//   - `CustomerAI.cs:1442-1444 PlaceProductOnDropOffTable` musterinin KENDISI
//     `Instantiate(productPrefabs[...])` yapiyor -- ProductSupply modu (cs:77,83).
//   - `PickUpScripts/ShelfState.cs`: SIFIR Instantiate -- raf sadece DEPOLUYOR,
//     URETMIYOR.
//   - `TableScripts/Shelf.cs` (NetworkedShelf) auto-respawn eden BOS kutular
//     saglıyor (1sn respawn) ama bunlar `boxDropMoneyPenalty` notunun da
//     dogruladigi gibi ICI BOS/degersiz -- degeri veren PRODUCT, tek kaynagi
//     musteri. `productPrefabs`/`ProductSupply` grep'i TUM projede sadece 3
//     dosyada geciyor (CustomerAI, CustomerManager, PostRentFeatureUnlocks) --
//     bagimsiz bir "restock/warehouse" spawner YOK.
//   - Zincir: musteri -> urun (Instantiate) -> oyuncu paketler -> tir ->
//     `Truck.cs:643 AddMoney`. **SONUC: gunluk kota, gunluk kutu arzinin (=
//     gelirin) GERCEK ust siniri -- truckThroughput() sadece bu arzi ISLEME
//     HIZINI (emek/masa/hangar) modelliyor, arzin KENDISINI degil.**
// Asagidaki tum model bu duzeltmeyle YENIDEN kuruldu: `plateUpBoxSupply` =
// min(mekanik islem tavani, kota) ve `runSimPlateUp` bunu PARAYA baglıyor.
//
// KRITIK ON KOSUL DEGISTI: koordinator sahnedeki 2. istasyon bosluğunu
// (`serviceTables[1]={fileID:0}`) DOGRULADI ve "2. masa eklenmesi garanti
// degil" dedi -- bu yuzden 1-ISTASYON artik FALLBACK degil, ANA SENARYO.
// `stations` parametresi asagida SRC.serviceStations (=1, canli) default'u
// kullanir. P3/P4 icin bu, "istasyon-slotu" P-BAGIMSIZ SABIT bir urun-arzi
// tavanina carpar (~8-13/gun, P2 ile hemen hemen AYNI) -- yani P3/P4 REVENUE
// artik P2'den (hemen hemen) FAZLA BUYUYEMEZ sadece musteri sayisiyla. Rent
// ise P ile 3.6x'e kadar buyudugunden (baseRent 500->1800), bu YAPISAL bir
// acik yaratir (bkz asagida REWARD_PER_BOX_BY_PLAYER lever'i).
const PLATEUP = {
  SAFETY: 0.85,
  // Davranissal tampon: SAFETY'nin tersi (1/0.85=1.176) -- kota zaten STRICT
  // tavanin %85'i, yani "tam SAFETY'de oyna" senaryosunda bile gelir ihtiyaci
  // bunun uzerine +%17.6 pay ister (STRICT'ten DAHA KOTU performans icin).
  behavioralMargin: 1 / 0.85,
  callCooldownRealSeconds: 20,     // P-bagimsiz; anti spam-click, ana kilit
                                    // maxQueueSize=2 + istasyon dolulugu zaten
  callMoneyReward: 20,             // DEGISMEDI (asset:34) -- artik "bedava"
                                    // degil: zaman atlama + kuyruk doldurma
                                    // maliyeti var (bkz rapor §6).
  callPrestigeReward: 0.4,         // DEGISMEDI (asset:35)
  dayEndGraceSeconds: 30,          // plan onerisi -- ekonomik etkisi asagida olculdu
  missedQuotaPrestigePenalty: -0.2, // YENI SABIT. customerLostPrestigePenalty'nin
                                     // (-0.4) YARISI; SADECE "hic spawn olmadan
                                     // gun sonunda kalan kota" icin. Sabri
                                     // dolan/kuyrukta kaybedilen musteri ESKI
                                     // -0.4'u alir (degismedi, cifte ceza yok).
  // YENI LEVER (bu turun secimi -- bkz asagidaki gerekce): rewardPerBox artik
  // P-bazli. baseRentByPlayerCount deseniyle AYNI (int[4] dizi, index=P-1).
  // Secim gerekcesi: (a) rentGrowthMultiplier 2026-08-20'de 1.35->1.20'ye
  // KONTROL-onayli dusuruldu, P1/P2 zaten dengede -- global buyume oranini
  // tekrar oynatmak o turu bozar VE P3/4'un asil sorununu (rent P ile 3.6x
  // buyuyor ama urun-arzi P3'ten sonra DUZLESIYOR) cozmez. (b) kotayi P3/4
  // icin daha da buyutmek MEKANIK OLARAK IMKANSIZ -- 1-istasyon tavanina
  // zaten carpiyorlar. (c) prestij-tier bonusu (`prestigePerBonus`/
  // `bonusPerTier`) organik yardimci ama ERKEN GUNLERDE (dusuk prestij)
  // yetersiz kaliyor (asagida runSimPlateUp ile olculdu) ve P'ye gore
  // FARKLILASTIRILAMIYOR (tek global deger). => rewardPerBoxByPlayerCount
  // rent'in P ile buyudugu ORANI DOGRUDAN telafi eden TEK surgical lever.
  rewardPerBoxByPlayerCount: [50, 55, 70, 88],
};

/** Seri servis tavani (musteri/gun) -- customerThroughput'un ceza/spawn
 *  ayrimi olmadan salt "mekanik ustsinir" hali. stations verilmezse CANLI
 *  sahne degeri (SRC.serviceStations=1) kullanilir -- artik ANA senaryo. */
function plateUpCeiling(day, playerCount, scenario, mode, stations = SRC.serviceStations) {
  const w = customerWindowSec(day);
  const slots = (w / ASSUMED.serviceCycleSeconds[scenario]) * stations;
  const playersOnCust = mode === 'optimistic'
    ? playerCount
    : playerCount * (1 - ASSUMED.laborShareTruck);
  const laborCap = (playersOnCust * w) / ASSUMED.serviceLaborSeconds[scenario];
  return Math.min(slots, laborCap);
}

/** Onerilen gunluk kota (dailyCustomerCountByDay'in P-bazli hali). Normal
 *  senaryo + STRICT emek varsayimi + CANLI istasyon sayisi (1) ile SAFETY
 *  carpani. P3/P4 bu yuzden P2'ye COK YAKIN cikiyor (istasyon-slotu P3'ten
 *  itibaren baglayici, labor degil) -- bu bir yuvarlama hatasi DEGIL, gercek
 *  mekanik doygunluk. Diger senaryolarda (Slow, event, dusuk performans) bu
 *  SABIT kotanin altinda kalinmasi BEKLENIR VE ISTENIR (bkz plateUpDayOutcome). */
function plateUpQuota(day, playerCount, stations = SRC.serviceStations) {
  const c = plateUpCeiling(day, playerCount, 'Normal', 'strict', stations);
  return Math.max(3, Math.round(PLATEUP.SAFETY * c));
}

/** P-bazli varis araligi (customerArrivalInterval, saniye). 1-istasyon
 *  gercekliginde P3/4 icin "labor her zaman baglayici" kapali-formu ARTIK
 *  GECERSIZ (istasyon-slotu daha erken baglar) -- bu yuzden ampirik olarak
 *  pencere(day)/kota(day,P) oranindan, GUNLER ARASI ORTALAMA alinarak
 *  turetildi (oran gunden gune ~±10% oynuyor, tek sabit deger icin yeterince
 *  stabil -- dogrulama: sim.js CLI 13b). */
function plateUpArrivalInterval(playerCount, stations = SRC.serviceStations) {
  let sum = 0;
  for (let d = 1; d <= 16; d++) sum += customerWindowSec(d) / plateUpQuota(d, playerCount, stations);
  return sum / 16;
}

/** Telefonun tek basina atladigi oyun-DAKIKASI miktari (timeSkipAmount).
 *  Tasarim kurali: telefon dogal "bir sonraki musteri" bekleyisini YERINE
 *  GECIRIR, ONUNE GECMEZ -- yani atlanan sure DOGAL ARALIGIN oyun-dakikasi
 *  karsiligina esitlenir (ne bedava sure yaratir ne de cezalandirir).
 *  referenceDay=8 (orta-oyun) donusum orani kullanilir. */
function plateUpTimeSkipMinutes(playerCount, referenceDay = 8, stations = SRC.serviceStations) {
  const intervalSec = plateUpArrivalInterval(playerCount, stations);
  const secPerGameMinute = secPerGameHour(referenceDay) / 60;
  return intervalSec / secPerGameMinute;
}

/** Bir gunun musteri akisi: SABIT kota (Normal+strict+CANLI istasyon sayisiyla
 *  belirlenmis) verilen scenario/mode altinda ne kadari servis edilebiliyor?
 *  missedQuota = ne sabri dolan (queue'da) ne de spawn'a hic sira gelmeyen --
 *  ikisi de gun sonunda TEK ceza kalemi (missedQuotaPrestigePenalty) alir;
 *  gercek oyunda "sabri dolan" ayrimi CustomerAI tarafinda ayrica -0.4 ile
 *  ele alinacagi icin bu fonksiyon UST SINIR/OZET amaclidir, cifte saymaz. */
function plateUpDayOutcome(day, playerCount, scenario, mode, stations = SRC.serviceStations) {
  const quota = plateUpQuota(day, playerCount, stations);
  const ceilingActual = plateUpCeiling(day, playerCount, scenario, mode, stations);
  const served = Math.min(quota, ceilingActual);
  const missedQuota = Math.max(0, quota - served);
  const prestigeDelta = served * SRC.customerServedPrestigeBonus
                       + missedQuota * PLATEUP.missedQuotaPrestigePenalty;
  return { quota, ceilingActual: +ceilingActual.toFixed(2), served: +served.toFixed(2), missedQuota: +missedQuota.toFixed(2), prestigeDelta: +prestigeDelta.toFixed(2) };
}

/** GUNLUK KUTU ARZI = min(mekanik islem tavani (truckThroughput -- emek/masa/
 *  hangar), SERVIS EDILEN musteri sayisi (=urun kaynagi)). Ikinci terim bu
 *  turun eklentisi -- musteri artik gercek bir ARZ TAVANI (bkz dosya basi not). */
function plateUpBoxSupply(day, playerCount, boxesPerMin, scenario, mode, numHangars, laborShare, packingTables, stations = SRC.serviceStations) {
  const tt = truckThroughput(playerCount, boxesPerMin, day, numHangars, mode, laborShare, packingTables);
  const o = plateUpDayOutcome(day, playerCount, scenario, mode, stations);
  return { boxSupply: Math.min(tt.boxesPerDay, o.served), tt, outcome: o };
}

/** runSim'in PlateUp-baglantili varyanti: PARA artik SADECE truckThroughput
 *  DEGIL, min(truckThroughput, servis-edilen-musteri) ile sinirli. Prestij de
 *  eski customerDemand/customerThroughput yerine plateUpDayOutcome kullanir.
 *  Geri kalan HER SEY (kira, grace, quest, telefon, hata oranlari) runSim ile
 *  BIREBIR AYNI -- karsilastirilabilir olsun diye. */
function runSimPlateUp(playerCount, opts = {}) {
  const {
    scenario = 'Normal', mode = 'optimistic', numHangars = SRC.hangarsAtLevel0,
    questsEnabled = true, questTier = SRC.questTierStart, phoneEnabled = true,
    packingTables = SRC.packingTablesAtLevel0, stations = SRC.serviceStations,
    rewardPerBoxByPlayerCount = PLATEUP.rewardPerBoxByPlayerCount,
    label = '',
  } = opts;

  const boxesPerMin = ASSUMED.boxesPerMinPerPlayer[scenario];
  const laborShare = ASSUMED.laborShareTruck;
  const baseRent = SRC.baseRentByPlayerCount[playerCount - 1];
  const rewardPerBoxBase = rewardPerBoxByPlayerCount[playerCount - 1];

  let cash = Math.round(SRC.baseStartingMoney * Math.pow(SRC.moneyMultiplierPerPlayer, playerCount - 1));
  let prestige = SRC.startingPrestige;
  let rentCycle = 0, graceUsed = false;
  let bankrupt = false, bankruptDay = null;
  let prestigeCapDay = null;
  let questSettlePending = null;
  const rows = [];

  for (let day = 1; day <= SRC.maxDays; day++) {
    let questSettledMoney = 0, questSettledPrestige = 0;
    if (questSettlePending) {
      questSettledMoney = questSettlePending.money;
      questSettledPrestige = questSettlePending.prestige;
      questSettlePending = null;
    }

    const bs = plateUpBoxSupply(day, playerCount, boxesPerMin, scenario, mode, numHangars, laborShare, packingTables, stations);
    const ph = phoneEnabled ? phoneIncome(mode) : { rings: 0, answers: 0, money: 0, prestige: 0 };

    const boxesToTruck = bs.boxSupply;
    const wrongRate = ASSUMED.wrongDeliveryRate[scenario];
    const dropRate = ASSUMED.physicalDropRate[scenario];
    const wrongBoxes = boxesToTruck * wrongRate;
    const correctBoxes = boxesToTruck - wrongBoxes;
    const droppedBoxes = boxesToTruck * dropRate;

    const prestigeTier = Math.floor(prestige / SRC.prestigePerBonus);
    const rewardActual = rewardPerBoxBase + prestigeTier * SRC.bonusPerTier;

    const truckRevenue = correctBoxes * rewardActual;
    const wrongCost = wrongBoxes * SRC.penaltyPerBox;
    const dropCost = droppedBoxes * SRC.boxDropMoneyPenalty;

    let questDecision = { accepted: false, money: 0, prestige: 0, pick: null };
    if (questsEnabled) {
      const capacity = {
        trucks: bs.tt.fullTrucksPerDay,
        shelfPlacements: bs.tt.productionCapPerDay,
        packedBoxes: bs.tt.productionCapPerDay,
        phoneAnswers: ph.answers,
      };
      questDecision = questDailyDecision(questTier, capacity, mode);
      if (questDecision.accepted) questSettlePending = { money: questDecision.money, prestige: questDecision.prestige };
    }

    const grossIncome = truckRevenue + ph.money + questSettledMoney;
    const grossCost = wrongCost + dropCost;
    const netEarnings = grossIncome - grossCost;
    let cashBeforeRent = Math.max(0, cash + netEarnings);

    prestige += bs.outcome.served * SRC.customerServedPrestigeBonus;
    prestige += bs.outcome.missedQuota * PLATEUP.missedQuotaPrestigePenalty;
    prestige += wrongBoxes * SRC.wrongDeliveryPrestigePenalty;
    prestige += droppedBoxes * SRC.boxDropPrestigePenalty;
    prestige += ph.prestige;
    prestige += questSettledPrestige;
    if (prestige >= SRC.maxPrestige && prestigeCapDay === null) prestigeCapDay = day;
    prestige = Math.max(0, Math.min(SRC.maxPrestige, prestige));

    let rentAmount = 0, rentPaid = 0, event = '';
    const isRentDay = day % SRC.rentIntervalDays === 0;
    if (isRentDay) {
      rentAmount = Math.round(baseRent * Math.pow(SRC.rentGrowthMultiplier, rentCycle) * SRC.rentScaledMultiplier);
      if (cashBeforeRent >= rentAmount) {
        rentPaid = rentAmount; cash = cashBeforeRent - rentPaid; rentCycle++;
      } else if (!graceUsed) {
        rentPaid = Math.round(cashBeforeRent * SRC.gracePaymentPercent);
        cash = cashBeforeRent - rentPaid; graceUsed = true; rentCycle++; event = 'GRACE';
      } else {
        bankrupt = true; bankruptDay = day; event = 'IFLAS'; cash = cashBeforeRent;
      }
    } else {
      cash = cashBeforeRent;
    }

    rows.push({
      gun: day, kota: bs.outcome.quota, servisEdilen: +bs.outcome.served.toFixed(1),
      kutuArzi: +boxesToTruck.toFixed(1), mekanikTavan: +bs.tt.boxesPerDay.toFixed(1),
      odulKutu: rewardActual, tirGeliri: Math.round(truckRevenue), telefon: Math.round(ph.money),
      questYatan: Math.round(questSettledMoney), cezalar: -Math.round(grossCost),
      netGelir: Math.round(netEarnings), prestij: +prestige.toFixed(2),
      kira: rentAmount, kasa: Math.round(cash), olay: event,
    });

    if (bankrupt) break;
    if (prestige <= 0) { bankrupt = true; bankruptDay = day; break; }
  }

  const cumNet = rows.reduce((a, r) => a + r.netGelir, 0);
  return {
    playerCount, label, scenario, mode, bankrupt, bankruptDay, prestigeCapDay, rows,
    finalCash: rows[rows.length - 1]?.kasa, finalPrestige: rows[rows.length - 1]?.prestij,
    cumulativeNet: cumNet, avgDailyNet: +(cumNet / rows.length).toFixed(1),
  };
}

// ############################################################################
// ############################################################################
// ##                                                                        ##
// ##   v4.0  KANONIK BIRLESIK MODEL  --  runFullSim()                       ##
// ##   Round 1 / "Tam Kapsamli Ekonomi Dengeleme" (2026-08-30)              ##
// ##   plans/economy-full-balance-2026-08-30.md                             ##
// ##                                                                        ##
// ############################################################################
// ############################################################################
//
// NEDEN YENI BIR FONKSIYON?
//   `runSim` (v3.1) ve `runSimPlateUp` (2026-08-29) parcali ve ARTIK BAYAT:
//     - runSim musteri talebini HALA kapasite formulunden (raf x2 + level x2 +
//       varyans) turetiyor. CANLI kod bunu 2026-08-29'da SILDI:
//       CustomerManager.CalculateTodaysCustomerCount (cs:403-419) artik YALNIZCA
//       GameEconomySettings.GetDailyCustomerCount(day, P) okuyor.
//     - Iki fonksiyon da `phoneIncome()` kullaniyor; o da SILINMIS alanlara
//       (phoneRingChancePerHour / phoneRingEventMultiplier / phoneRingPerkBonus)
//       dayaniyor. Bu alanlar GameEconomySettings.cs'te ARTIK YOK (asset'te olu
//       anahtar olarak duruyorlar). Telefon artik "calan telefonu cevapla"
//       degil "DISARI ARA" (PhoneCallManager V4).
//     - Ikisi de gunu SABIT `dayDurationSec(day)` uzunlugunda variyor. CANLI kod
//       kota tukenince gunu ERKEN BITIRIYOR (CustomerManager.CheckEarlyDayCompletion
//       cs:896-910 -> DayCycleManager.FastForwardToEndOfDay cs:461).
//     - Ikisi de "1 musteri = 1 urun" variyor. CANLI kod: gun 5+ musterilerin
//       %25'i IADE modunda (SIFIR urun), gun 9+ TUM tedarik musterileri IKI urun
//       birakiyor (PostRentFeatureUnlocks + CustomerAI.PlaceProductCoroutine:1339).
//     - Ikisi de kargo araligini sabit {2,3,4,5} variyor. CANLI kod P-bazli
//       (TruckSpawner.cs:613,624 -> GetTruckCargoRange).
//
// KAYNAK DENETIMI 2026-08-30 (her sabit dosya:satir ile):
//   ! ONEMLI UNITY NOTU: Assets/Resources/EkonomiAyarlari.asset SU ANAHTARLARI
//     HIC ICERMIYOR -> C# field-initializer degeri CANLIDIR (Unity eksik anahtari
//     yazmaz, alan initializer'da kalir; bkz. hafiza notu "Unity YAML float[]
//     tuzagi" -- anahtari hic yazmamak DOGRU yontem):
//       dailyCustomerCountP1..P4, customerArrivalIntervalByPlayerCount,
//       rewardPerBoxByPlayerCount, timeSkipAmountByPlayerCount,
//       phoneCooldownSeconds, phoneCooldownPerkBonusSeconds, phoneDialHoldSeconds,
//       customerMissedQuotaPrestigePenalty
//   ! OLU ASSET ANAHTARLARI (asset'te var, C# sinifinda YOK -> hicbir sey yapmiyor):
//       phoneRingChancePerHour: 0.2 / phoneRingEventMultiplier: 2 / phoneRingPerkBonus: 0
const SRC4 = {
  // ---- KIRA (GameEconomySettings.cs:21,24,27,30,33 + asset:15-19) ----------
  // ROUND 11 RESYNC (2026-08-30): Round 10 U1 UYGULANDI -> canli deger.
  baseRentByPlayerCount: [290, 650, 1140, 1630], // cs:21 (U1 uygulandi)
  rentGrowthMultiplier:  1.20,  // cs:24 == asset:16
  rentIntervalDays:      4,     // cs:27 == asset:17
  gracePaymentPercent:   0.8,   // cs:30 == asset:18
  rentScaledMultiplier:  1.0,   // cs:33 == asset:19 (perk yok -> 1)

  // ---- MUSTERI KOTASI (PlateUp) -- GameEconomySettings.cs:44,47,50,53 ------
  // ASSET'TE YOK -> C# initializer CANLI. index = gun-1.
  dailyCustomerCountP1: [4, 4, 4, 4, 4, 4, 4, 5, 5, 5, 5, 5, 6, 6, 6, 6],
  dailyCustomerCountP2: [7, 7, 7, 8, 8, 9, 9, 9, 10, 10, 10, 11, 11, 11, 12, 12],
  dailyCustomerCountP3: [8, 8, 8, 8, 9, 9, 9, 10, 10, 10, 11, 11, 12, 12, 12, 13],
  dailyCustomerCountP4: [8, 8, 8, 8, 9, 9, 9, 10, 10, 10, 11, 11, 12, 12, 12, 13],
  // GameEconomySettings.cs:56 (ASSET'TE YOK). GERCEK saniye (elapsedTime domeni).
  customerArrivalIntervalByPlayerCount: [44, 22, 21, 21],
  dayEndGraceSeconds: 30,       // cs:59 == asset:19 "dayEndGraceSeconds: 30"
  minCustomersPerDay: 1,        // sahne unity:87604 "_minCustomersPerDay"
  maxCustomersPerDay: 50,       // sahne unity:87605
  spawnTimeRandomness: 0.20,    // sahne unity:87606 (simetrik jitter -> ORTALAMA NOTR)

  // ---- ODUL / TIR (GameEconomySettings.cs:68,71,74,80,83,86 + asset) -------
  rewardPerBox: 50,                          // cs:68 == asset:20 (LEGACY fallback)
  rewardPerBoxByPlayerCount: [50, 55, 70, 88],// cs:71 ASSET'TE YOK -> CANLI
  penaltyPerBox: 40,                          // cs:74 == asset:21
  hangarStayByPlayerCount: [120, 60, 40, 30], // cs:80 == asset hex 78/3c/28/1e
  prestigePerBonus: 8,                        // cs:83 == asset:24
  bonusPerTier: 5,                            // cs:86 == asset:25
  rewardVolatility: 0,                        // cs:89 == asset:26 (perk kapali)
  // P-BAZLI KARGO -- CANLI (asset hex mevcut). TruckSpawner.cs:613,624 kullaniyor.
  truckCargoMinByPlayerCount:          [1, 2, 2, 2], // cs:95 == asset:28
  truckCargoMaxExclusiveByPlayerCount: [3, 4, 5, 6], // cs:98 == asset:29
  boxDropMoneyPenalty: 5,                     // cs:107 == asset:30

  // ---- TELEFON V4 (DISARI ARAMA) -- GameEconomySettings.cs:117-132 ---------
  // ROUND 11 RESYNC: Round 10 U2 UYGULANDI.
  timeSkipAmountByPlayerCount: [115, 49, 47, 47], // cs:117 ASSET'TE YOK -> CANLI (oyun-DAKIKASI)
  phoneCooldownSeconds: 3,        // cs:120 ASSET'TE YOK -> CANLI (2026-08-30 kullanici istegi 20->3)
  phoneCooldownPerkBonusSeconds: 0, // cs:123 ASSET'TE YOK -> CANLI.
                                    // !! TUTARSIZLIK: ayni satirdaki tooltip ve
                                    // PhoneCallManager.cs:292 yorumu "10f economist onayli"
                                    // diyor, plan dosyasi da 10f varsayiyor. GERCEK DEGER 0f.
                                    // Round 7'nin cikis noktasi bu olmali.
  callMoneyReward: 20,            // cs:126 == asset:34
  callPrestigeReward: 0.4,        // cs:129 == asset:35
  phoneDialHoldSeconds: 1,        // cs:132 ASSET'TE YOK -> CANLI (ekonomik deger DEGIL)
  phoneStartHour: 8,              // sahne unity:14820
  phoneEndHour: 18,               // sahne unity:14821

  // ---- QUEST (QuestManager.cs) --------------------------------------------
  dailyQuestCount: 3,             // cs:19 BASE_DAILY_QUEST_COUNT (gunluk TEKLIF sayisi;
                                  // kabul gunde EN FAZLA 1 -- HasAcceptedQuestToday).
                                  // CANLI KOD su an cs:117 DailyQuestTargetCount =
                                  // 3 + CurrentQuestTier (Round 10 U6) AMA UI 3 slotta
                                  // kirpiyor -> etkin deger yine 3 (bkz. questUiSlots).
  questUiSlots: 3,                // sahne "The Main Office.unity":90392-90395 -> TAM 3
                                  // QuestSlotUI; QuestUIController.cs:407 Mathf.Min ile kirpar.

  // ---- PRESTIJ CEZA/ODUL (GameEconomySettings.cs:141-156) ------------------
  customerLostPrestigePenalty:        -0.4,  // cs:141 == asset:36
  customerMissedQuotaPrestigePenalty: -0.2,  // cs:144 ASSET'TE YOK -> CANLI
  customerServedPrestigeBonus:         0.4,  // cs:147 == asset:37
  wrongProductPrestigePenalty:        -0.20, // cs:150 == asset:38 (ROUND 11 RESYNC: U12 uygulandi)
  boxDropPrestigePenalty:             -0.04, // cs:153 == asset:39
  wrongDeliveryPrestigePenalty:       -0.16, // cs:156 == asset:40

  // ---- GUN DONGUSU (DayCycleManager.cs + sahne) ----------------------------
  maxDays: 16,                    // cs:36 MAX_DAYS
  dynamicDurationStartDay: 3,     // cs:37 DYNAMIC_DURATION_START_DAY
  realDurationInSeconds: 200,     // sahne unity:19726 (= cs:52)
  dailyDurationIncrease: 10,      // sahne unity:19727 (= cs:55)
  dayStartHour: 7,                // sahne unity:19728 "startHour"
  dayEndHour: 18,                 // sahne unity:19729 "endHour"

  // ---- MUSTERI PENCERESI / KUYRUK (CustomerManager + sahne) ----------------
  spawnStartHour: 8,              // sahne unity:87615
  spawnEndHour: 17,               // sahne unity:87616
  customerExitHour: 17.5,         // CustomerManager.cs:27 CUSTOMER_EXIT_HOUR
  maxQueueSize: 2,                // sahne unity:87596
  // sahne unity:87612-87614 -> serviceTables[0] dolu, serviceTables[1] {fileID: 0}
  // => TEK ISTASYON. Bu BILINCLI tasarim (koordinator dogrulamasi 2026-08-29).
  serviceStations: 1,

  // ---- WAVE SISTEMI (sahne unity:87618 enableWaveSystem: 1) ---------------
  // Assets/Scenes/WaveSettings.asset. AdvanceNextSpawnThreshold (CustomerManager.cs:
  // 456-463) araligi `interval /= spawnRateMultiplier` ile OLCEKLIYOR.
  waveTimePeriods: [
    { startHour: 8,  endHour: 12, maxCustomers: 4, rate: 1.0 },
    { startHour: 12, endHour: 14, maxCustomers: 6, rate: 1.5 },
    { startHour: 14, endHour: 15, maxCustomers: 2, rate: 0.5 },
    { startHour: 15, endHour: 16, maxCustomers: 3, rate: 0.8 },
    { startHour: 16, endHour: 17, maxCustomers: 4, rate: 1.3 },
    { startHour: 17, endHour: 18, maxCustomers: 2, rate: 0.6 },
  ],

  // ---- KIRA-SONRASI OZELLIKLER (PostRentFeatureUnlocks.cs) ----------------
  returnUnlockDay: 5,             // cs:16 RETURN_UNLOCK_DAY
  returnModeChance: 0.25,         // cs:22 RETURN_MODE_CHANCE
  dualItemUnlockDay: 9,           // cs:25 DUAL_ITEM_UNLOCK_DAY
  mixedTruckUnlockDay: 13,        // cs:28 MIXED_TRUCK_UNLOCK_DAY
  dualItemInteractionMult: 1.30,  // cs:60 DUAL_ITEM_INTERACTION_TIME_MULTIPLIER
  dualItemBoxRequestMult: 1.15,   // cs:74 DUAL_ITEM_BOXREQUEST_INTERACTION_TIME_MULTIPLIER

  // ---- BASLANGIC DEGERLERI ------------------------------------------------
  baseStartingMoney: 500,         // DifficultyManager.cs:36 == prefab:75
  moneyMultiplierPerPlayer: 1.2,  // DifficultyManager.cs:61 == prefab:80
                                  // ScaledStartingMoney = 500 * 1.2^(P-1) (cs:307-309),
                                  // MoneySystem.startingMoney'e yaziliyor (cs:455)
  startingPrestige: 12,           // sahne unity:31816
  maxPrestige: 100,               // sahne unity:31817

  // ---- TIR ZAMANLAMASI ----------------------------------------------------
  truckStartHour: 8,              // TruckSpawner sahne (= cs:68)
  truckEndHour: 17,               // TruckSpawner sahne (= cs:71)
  exitDelay: 5,                   // Truck.prefab:196
  respawnDelayRange: [3, 5],      // sahne (ort 4)
  hangarsAtLevel0: 1,
  packingTablesAtLevel0: 1,       // sahne: "Paketleme Istasyonu" levelObjects[0]
};

// ---------------------------------------------------------------------------
// v4 VARSAYIMLARI (kodda YOK -- oyuncu davranisi). ASSUMED'in uzantisi.
// ---------------------------------------------------------------------------
const ASSUMED4 = {
  // Kota musterilerinin ne kadari TELEFONLA one cekiliyor.
  // ESKI `ASSUMED.phoneAnswerRate` BURADA KULLANILAMAZ: o "calan telefonu acma
  // orani" idi (V3 pasif telefon). V4'te telefon AKTIF bir tercih ve gunun
  // GERCEK saniyelerini yakiyor (SkipTime).
  //
  // !! ROUND 10 (2026-08-30) GUNCELLEMESI: eski {strict:0.60, optimistic:0.10}
  //    varsayimi Round 7'nin duzeltilmis modeliyle CURUDU. Duzeltilmis modelde
  //    optimal kullanim HER IKI bantta da %10-25 araliginda ve plato genis.
  //    Ikisini de 0.20'ye esitliyoruz -> "kabaca her 4-5 musteriden birini
  //    telefonla cagir" (Round 7 S7B'nin ogretilebilir tek kurali).
  phoneUseRate: { strict: 0.20, optimistic: 0.20 },
};

// --- Wave sisteminin ORTALAMA aralik carpani ---------------------------------
// interval /= rate oldugu icin ZAMAN-AGIRLIKLI ORTALAMA 1/rate alinir.
// Yalniz spawn penceresi (spawnStartHour..spawnEndHour) sayilir.
function waveIntervalFactor() {
  let hours = 0, weighted = 0;
  for (const p of SRC4.waveTimePeriods) {
    const a = Math.max(p.startHour, SRC4.spawnStartHour);
    const b = Math.min(p.endHour, SRC4.spawnEndHour);
    if (b <= a) continue;
    hours += (b - a);
    weighted += (b - a) / p.rate;
  }
  return hours > 0 ? weighted / hours : 1;
}
const WAVE_INTERVAL_FACTOR = waveIntervalFactor();

function quotaFor(day, playerCount) {
  const p = Math.min(Math.max(playerCount, 1), 4);
  const curve = [SRC4.dailyCustomerCountP1, SRC4.dailyCustomerCountP2,
                 SRC4.dailyCustomerCountP3, SRC4.dailyCustomerCountP4][p - 1];
  const i = Math.min(Math.max(day - 1, 0), curve.length - 1);
  return curve[i];
}
function arrivalIntervalFor(playerCount) {
  const a = SRC4.customerArrivalIntervalByPlayerCount;
  return a[Math.min(Math.max(playerCount - 1, 0), a.length - 1)];
}
function rewardPerBoxFor(playerCount) {
  const a = SRC4.rewardPerBoxByPlayerCount;
  if (!a || !a.length) return SRC4.rewardPerBox;
  return a[Math.min(Math.max(playerCount - 1, 0), a.length - 1)];
}
function cargoValuesFor(playerCount) {
  const i = Math.min(Math.max(playerCount - 1, 0), 3);
  const lo = SRC4.truckCargoMinByPlayerCount[i];
  const hi = SRC4.truckCargoMaxExclusiveByPlayerCount[i];
  const out = [];
  for (let c = lo; c < hi; c++) out.push(c);
  return out.length ? out : [2, 3, 4, 5];
}
function timeSkipMinutesFor(playerCount) {
  const a = SRC4.timeSkipAmountByPlayerCount;
  return a[Math.min(Math.max(playerCount - 1, 0), a.length - 1)];
}

/**
 * Tir verimi -- runSim'in truckThroughput'unun PENCERE-PARAMETRELI hali.
 * Fark: `truckWinSec` ve `prodWinSec` disaridan verilir (erken gun bitisi ve
 * telefon zaman-atlamasi gercek gun uzunlugunu KISALTIYOR), kargo dizisi
 * P-bazli gelir.
 */
function truckThroughputWindowed(playerCount, boxesPerMin, truckWinSec, prodWinSec,
                                 numHangars, mode, laborShare, packingTables, cargoValues) {
  const playersOnTrucks = playerCount * (mode === 'optimistic' ? 1 : laborShare);
  const cycleSec = 60 / boxesPerMin;
  const eta = tableContentionEfficiency(playerCount, packingTables, ASSUMED.tableBusySeconds, cycleSec);
  const prodRate = (boxesPerMin * playersOnTrucks) / 60 * eta;              // kutu/sn
  const fillRate = mode === 'optimistic' ? prodRate * ASSUMED.handoverSpeedup : prodRate;
  const stay = SRC4.hangarStayByPlayerCount[Math.min(Math.max(playerCount - 1, 0), 3)];
  const CARGO = cargoValues && cargoValues.length ? cargoValues : [2, 3, 4, 5];

  let sumDeliverable = 0, sumCycle = 0, fullCount = 0;
  for (const cargo of CARGO) {
    const fillTime = fillRate > 0 ? cargo / fillRate : Infinity;
    sumDeliverable += Math.min(cargo, fillRate * stay);
    sumCycle += Math.min(stay, fillTime) + OVERHEAD_TOTAL;
    if (fillTime <= stay) fullCount++;
  }
  const avgDeliverable = sumDeliverable / CARGO.length;
  const avgCycle = sumCycle / CARGO.length;
  const trucksPerDay = (truckWinSec / avgCycle) * numHangars;
  const productionCap = prodRate * prodWinSec;
  let boxesPerDay = Math.min(trucksPerDay * avgDeliverable, productionCap);
  const trucksEff = avgDeliverable > 0 ? Math.min(trucksPerDay, boxesPerDay / avgDeliverable) : 0;

  return {
    trucksPerDay: trucksEff, boxesPerDay, productionCapPerDay: productionCap,
    avgDeliverablePerTruck: avgDeliverable, avgCycleSec: avgCycle,
    fullTrucksPerDay: trucksEff * (fullCount / CARGO.length),
    tableContentionEta: eta, hangarStaySec: stay,
  };
}

/**
 * Bir gunun MUSTERI akisi (v4, CANLI kota modeli).
 *
 * Zincir (hepsi kod-kanitli):
 *  1. Kota  Q = GetDailyCustomerCount(day, P)          (cs:256-262)
 *  2. Varis araligi I = GetCustomerArrivalIntervalSeconds(P) x waveFactor x
 *     jitter(ORTALAMA NOTR)                             (CustomerManager.cs:448-466)
 *  3. Spawn kapisi: HasUnspawnedCustomers && !IsQueueFull (cs:484-503).
 *     Kuyruk dolu -> spawn ATLANIR (musteri gecikir, KAYBOLMAZ).
 *  4. Servis: TEK istasyon (serviceTables[1] bos) -> SERI.
 *  5. 17:30 (CUSTOMER_EXIT_HOUR) -> ForceAllCustomersToExit (servis edilmemis her
 *     musteri OnCustomerLost, -0.4) + ApplyMissedQuotaPenalty (hic spawn olmamis
 *     her kota musterisi OnCustomerQuotaMissed, -0.2)   (cs:559-627)
 *  6. Kota tukenip kuyruk bosalinca gun ERKEN biter: +dayEndGraceSeconds sonra
 *     FastForwardToEndOfDay                              (cs:896-930)
 */
function fullCustomerDay(day, playerCount, scenario, mode, opts) {
  const {
    laborShare = ASSUMED.laborShareTruck,
    stations = SRC4.serviceStations,
    phoneUseRate = 0,
    // ROUND 10: telefon sabitleri artik PARAMETRIK (Round 7 onerisi test edilebilsin)
    timeSkipMinutes = timeSkipMinutesFor(playerCount),
    phoneTimeSkipPerkMultiplier = 1,   // Round 7 S6 onerisi: phone_line perki 0.80
  } = opts || {};

  const Q = quotaFor(day, playerCount);
  const secHour = dayDurationSec4(day) / (SRC4.dayEndHour - SRC4.dayStartHour);
  const spawnWinSec = secHour * (SRC4.spawnEndHour - SRC4.spawnStartHour);   // 8->17
  const serveWinSec = secHour * (SRC4.customerExitHour - SRC4.spawnStartHour); // 8->17.5

  // --- servis tavanlari ---
  // Gun 9+ dual-item: etkilesim suresi x1.30 (tedarik) / x1.15 (iade).
  const dual = day >= SRC4.dualItemUnlockDay;
  const returnFrac = day >= SRC4.returnUnlockDay ? SRC4.returnModeChance : 0;
  const interactionMult = dual
    ? (1 - returnFrac) * SRC4.dualItemInteractionMult + returnFrac * SRC4.dualItemBoxRequestMult
    : 1;

  const svcCycle = ASSUMED.serviceCycleSeconds[scenario] * interactionMult;
  const svcLabor = ASSUMED.serviceLaborSeconds[scenario] * interactionMult;
  const playersOnCust = mode === 'optimistic' ? playerCount : playerCount * (1 - laborShare);

  // --- TELEFON CAGRISININ GERCEK-SANIYE MALIYETI (ROUND 10 / Round 7 S1a) ----
  // !! KRITIK, KORUNMASI GEREKEN DAVRANIS:
  //    DayCycleManager.SkipTime (cs:425) ve PredictTimeAfterSkip (cs:446)
  //    saniye/oyun-dakikasi oranini TABAN `realDurationInSeconds` (200s) ile
  //    hesapliyor, gunun GERCEK uzunlugu CurrentDayDuration ile DEGIL:
  //        secondsPerGameHour = realDurationInSeconds / totalGameHours
  //    => cagri basina gercek-saniye maliyeti GUNDEN BAGIMSIZ ve SABIT:
  //        cost = T[P] * (200 / 660) = T[P] * 0.30303 sn
  //    Bu, dogal varis araliginin yalnizca %76-78'i. ESKI v4 MODELI buraya
  //    `phoneCalls * I` (tam bir aralik) yaziyordu -> telefonu ~%31 FAZLA
  //    faturalandiriyordu ve Round 1/2'nin "beceri-ters TRAP" bulgusunu
  //    URETEN sey buydu (model artifakti).
  //    ⚠️ Bunu `CurrentDayDuration`'a cevirmeyin: gec-oyun maliyeti +%65 artar.
  //    (phoneDialHoldSeconds=1sn BILEREK haric -- Round 7 kalibrasyonu ile
  //     ayni kalsin diye; dahil edilirse maliyet P1'de +%3, P2-4'te +%6 artar.)
  const SEC_PER_GAME_MIN_BASE =
    SRC4.realDurationInSeconds / ((SRC4.dayEndHour - SRC4.dayStartHour) * 60);
  const callCostSec = timeSkipMinutes * phoneTimeSkipPerkMultiplier * SEC_PER_GAME_MIN_BASE;

  // --- varis kapasitesi ---
  const I = arrivalIntervalFor(playerCount) * WAVE_INTERVAL_FACTOR;
  const phoneCalls = Math.max(0, Math.min(Q, Q * phoneUseRate));
  const skipSec = phoneCalls * callCostSec;

  // --- servis tavanlari ---
  // ROUND 10: SkipTime GERCEK saniyeleri yakar; servis de gercek-zamanli bir is
  // oldugu icin servis penceresi de KISALIR. (v4 bunu yalniz TIR penceresine
  // uyguluyordu -> optimistic bantta telefon "bedava para" gorunuyordu.)
  const serveWinEff = Math.max(svcCycle, serveWinSec - skipSec);
  const stationCap = (serveWinEff / svcCycle) * stations;
  const laborCap = (playersOnCust * serveWinEff) / svcLabor;
  const serviceCap = Math.min(stationCap, laborCap);
  // ROUND 10 (Round 7 S1c): `ForceSpawnNextCustomer` DOGAL varislara EKLENIR --
  // eski model arrivalCap'i telefondan bagimsiz sabit tutuyordu.
  const naturalArrivalCap = 1 + spawnWinSec / I;
  const arrivalCap = naturalArrivalCap + phoneCalls;

  const spawned = Math.min(Q, arrivalCap, serviceCap + SRC4.maxQueueSize);
  const served = Math.min(spawned, serviceCap);
  const lost = Math.max(0, spawned - served);          // -0.4 (sabri dolan / 17:30 kesimi)
  const missedQuota = Math.max(0, Q - spawned);        // -0.2 (hic spawn olmayan)

  // --- gunun GERCEK aktif suresi (erken bitis + telefon zaman-atlamasi) -----
  const realTimeSavedSec = skipSec;

  // ROUND 10 (Round 7 S1d): DRAIN SIKISMASI. Telefonla cagrilan musteri dogal
  // araligi (I) beklemez; sirasi ~callCostSec sonra gelir (servis dongusunun
  // altina inemez). Kuyrugun bosalma suresi bu yuzden KISALIR.
  const perStationCycle = svcCycle / Math.max(stations, 1);
  const gapNatural = Math.max(I, perStationCycle);
  const gapPhone   = Math.max(callCostSec, perStationCycle);
  const phoneShare = spawned > 0 ? Math.min(1, phoneCalls / spawned) : 0;
  const avgGap = phoneShare * gapPhone + (1 - phoneShare) * gapNatural;
  const drainSec = Math.max(spawned - 1, 0) * avgGap + svcCycle;

  // ROUND 10 (Round 7 S1b): CIFT SAYIM DUZELTMESI.
  // Eski: min(dayDur, naturalEnd) - atlanan  -> naturalEnd bagliyorken atlama
  // IKINCI kez dusuluyordu (naturalEnd zaten sikismis drain'i iceriyor).
  // Dogrusu IKI YOLUN MIN'i. Ayrica erken gun bitisi ancak kota TAMAMEN spawn
  // olduysa acilir (CustomerManager.CheckEarlyDayCompletion cs:896-930).
  const quotaFullySpawned = spawned >= Q - 1e-9;
  const naturalEndSec = secHour * (SRC4.spawnStartHour - SRC4.dayStartHour) + drainSec + SRC4.dayEndGraceSeconds;
  const budgetSec = dayDurationSec4(day) - realTimeSavedSec;
  const dayActiveSec = Math.max(
    secHour,                                  // en az 1 oyun-saati
    quotaFullySpawned ? Math.min(budgetSec, naturalEndSec) : budgetSec
  );

  // --- URUN ARZI (para zincirinin GERCEK tavani) ---
  // gun 5+  : servis edilenlerin %25'i IADE modu -> SIFIR urun uretir
  // gun 9+  : tedarik musterisi IKI urun birakir (PlaceProductCoroutine:1339)
  const supplyCustomers = served * (1 - returnFrac);
  const productsPerCustomer = dual ? 2 : 1;
  const productSupply = supplyCustomers * productsPerCustomer;

  return {
    quota: Q, spawned, served, lost, missedQuota,
    stationCap, laborCap, serviceCap, arrivalCap,
    productSupply, returnFrac, productsPerCustomer,
    dayActiveSec, realTimeSavedSec, phoneCalls,
    intervalSec: I, svcCycle,
    callCostSec, callCostRatio: I > 0 ? callCostSec / I : 0,
    naturalArrivalCap, drainSec, naturalEndSec, quotaFullySpawned,
  };
}

function dayDurationSec4(day) {
  return day <= SRC4.dynamicDurationStartDay
    ? SRC4.realDurationInSeconds
    : SRC4.realDurationInSeconds + (day - SRC4.dynamicDurationStartDay) * SRC4.dailyDurationIncrease;
}

/**
 * ===========================================================================
 * runFullSim -- KANONIK 16-GUN SIMULASYONU (v5.0, ROUND 10 2026-08-30)
 * ===========================================================================
 * Perk YOK (level 0, hicbir perk alinmamis) -- perk etkisi Round 4'un konusu.
 * Upgrade YOK (taban kosu).
 *
 * --- v4.0 -> v5.0 DEGISIKLIKLERI (Round 10, 5 MODEL HATASI DUZELTILDI) -----
 * 1. TELEFON ZAMAN MALIYETI (Round 7 S1a): `phoneCalls * I` -> `phoneCalls *
 *    T[P]*0.30303`. Canli kod TABAN 200s ile donusum yapiyor -> maliyet gunden
 *    BAGIMSIZ. v4 telefonu ~%31 fazla faturaliyordu.
 * 2. CIFT SAYIM (Round 7 S1b): `min(dayDur, naturalEnd) - atlanan` ->
 *    `min(dayDur - atlanan, naturalEnd)`; erken bitis yalniz kota tam spawn
 *    olduysa acilir.
 * 3. FORCED-SPAWN KREDISI (Round 7 S1c): `arrivalCap += phoneCalls`.
 * 4. DRAIN SIKISMASI (Round 7 S1d) + SERVIS PENCERESI KISALMASI: atlanan
 *    saniyeler yalniz TIR penceresini degil SERVIS penceresini de kisaltiyor
 *    (v4 bunu kaciriyordu -> optimistic bantta telefon "bedava para"ydi).
 * 5. QUEST KARAR MODELI (Round 8 S10b): "havuzun en iyi 3'unun ORTALAMASI" ->
 *    N rastgele teklifin MAKSIMUMU (siralama istatistigi ile tam hesap) +
 *    canli TIER-DUZ odul tablosu + gun-16 cezasiz settlement.
 * Ayrica `ASSUMED4.phoneUseRate` 0.60/0.10 -> 0.20/0.20 (Round 7 S8).
 *
 * v5 opts ile Round 3/6/7/8 onerileri parametrik test edilebilir:
 *   baseRentByPlayerCount, timeSkipAmountByPlayerCount, callMoneyReward,
 *   callPrestigeReward, phoneTimeSkipPerkMultiplier, dailyQuestCount,
 *   questAssets, questPrestigeScale, prestigePerBonus, wrongProductRate.
 *
 * Kullanim:
 *   const { runFullSim } = require('./sim.js');
 *   runFullSim(2, { scenario: 'Normal', mode: 'strict' });
 * CLI:
 *   node tools/economy-sim/sim.js            (tum bloklar; v4 bloklari 18-22)
 */
function runFullSim(playerCount, opts = {}) {
  const {
    scenario = 'Normal',        // 'Normal' | 'Slow' | 'Fast'
    mode = 'optimistic',        // 'optimistic' | 'strict'
    numHangars = SRC4.hangarsAtLevel0,
    packingTables = SRC4.packingTablesAtLevel0,
    stations = SRC4.serviceStations,
    questsEnabled = true,
    questTier = 0,
    phoneEnabled = true,
    // Oyuncunun kota musterilerinin ne kadarini TELEFONLA one cektigi (ASSUMED4).
    phoneUseRate = null,
    rewardPerBoxByPlayerCount = SRC4.rewardPerBoxByPlayerCount,
    rentGrowthMultiplier = SRC4.rentGrowthMultiplier,
    baseRentByPlayerCount = SRC4.baseRentByPlayerCount,
    // ---- ROUND 10: parametrik oneri anahtarlari --------------------------
    timeSkipAmountByPlayerCount = SRC4.timeSkipAmountByPlayerCount, // Round 7
    phoneTimeSkipPerkMultiplier = 1,                                // Round 7 S6
    callMoneyReward = SRC4.callMoneyReward,
    callPrestigeReward = SRC4.callPrestigeReward,
    dailyQuestCount = SRC4.dailyQuestCount,      // Round 8: 3 -> 3+questTier onerisi
    questSelection = 'live',                     // ROUND 11: teklif uretim varyanti
    questSelectionOpts = {},                     // ROUND 11
    questUiSlots = SRC4.questUiSlots,            // ROUND 11: sahnedeki QuestSlotUI sayisi
    questAssets = QUEST_ASSETS,                  // Round 8: telefon hedefi override'i
    questPrestigeScale = 1,                      // Round 8: prestij odul/ceza x0.4
    day16Settlement = true,                      // Round 8 S8: gun-16 cezasiz settlement
    prestigePerBonus = SRC4.prestigePerBonus,    // Round 6: 8 -> 10 onerisi
    wrongProductPrestigePenalty = SRC4.wrongProductPrestigePenalty, // Round 6 S4
    wrongProductRate = 0,                        // 0 = kanal KAPALI (taban kosum)
    // Round 3 (2026-08-30): kira affi (grace) tamponunu KAPATMA anahtari.
    // false => oyuncu grace'i daha once yakmis / leveraged_rent|all_in perki
    // gracePaymentPercent=0 yapmis gibi davranir (PerkEffect.cs:318,336).
    graceAvailable = true,
    // Round 3 (2026-08-30): TANI modu. Grace + 0'a-kirpma + iflas kesmesi devre
    // disi; 16 gun sonuna kadar SAF nakit defteri tutulur (kasa negatife duser).
    // Grace'in yarattigi monotonluk kirilmalarindan aridir -> egri tasariminda
    // KULLANILACAK metrik budur. Oyun sonucu icin ledgerMode=false koslulmali.
    ledgerMode = false,
    label = '',
  } = opts;

  const boxesPerMin = ASSUMED.boxesPerMinPerPlayer[scenario];
  const laborShare = ASSUMED.laborShareTruck;
  const baseRent = baseRentByPlayerCount[playerCount - 1];
  const rewardBase = rewardPerBoxByPlayerCount[playerCount - 1];
  const useRate = phoneUseRate === null ? ASSUMED4.phoneUseRate[mode] : phoneUseRate;
  const cargo = cargoValuesFor(playerCount);

  let cash = Math.round(SRC4.baseStartingMoney * Math.pow(SRC4.moneyMultiplierPerPlayer, playerCount - 1));
  let prestige = SRC4.startingPrestige;
  let rentCycle = 0, graceUsed = !graceAvailable;
  let bankrupt = false, bankruptDay = null, prestigeCapDay = null;
  let questSettlePending = null;
  const rows = [];

  for (let day = 1; day <= SRC4.maxDays; day++) {
    // 0) Dunku quest odulu simdi yatar (kira'dan SONRA gelmis sayilir)
    let qMoney = 0, qPrestige = 0;
    if (questSettlePending) { qMoney = questSettlePending.money; qPrestige = questSettlePending.prestige; questSettlePending = null; }

    // 1) Musteri gunu (kota + arz + gunun gercek uzunlugu)
    const cd = fullCustomerDay(day, playerCount, scenario, mode, {
      laborShare, stations, phoneUseRate: phoneEnabled ? useRate : 0,
      timeSkipMinutes: timeSkipAmountByPlayerCount[
        Math.min(Math.max(playerCount - 1, 0), timeSkipAmountByPlayerCount.length - 1)],
      phoneTimeSkipPerkMultiplier,
    });

    // 2) Tir penceresi -- GUNUN GERCEK UZUNLUGUNA gore kirpilir
    const secHour = dayDurationSec4(day) / (SRC4.dayEndHour - SRC4.dayStartHour);
    const truckWinFull = secHour * (SRC4.truckEndHour - SRC4.truckStartHour);
    const truckOffset = secHour * (SRC4.truckStartHour - SRC4.dayStartHour);
    const truckWinSec = Math.max(0, Math.min(truckWinFull, cd.dayActiveSec - truckOffset));
    const prodWinSec = mode === 'optimistic' ? cd.dayActiveSec : truckWinSec;
    const tt = truckThroughputWindowed(playerCount, boxesPerMin, truckWinSec, prodWinSec,
                                       numHangars, mode, laborShare, packingTables, cargo);

    // 3) KUTU ARZI = min(mekanik isleme tavani, musteriden gelen URUN arzi)
    const boxes = Math.min(tt.boxesPerDay, cd.productSupply);

    const wrongRate = ASSUMED.wrongDeliveryRate[scenario];
    const dropRate = ASSUMED.physicalDropRate[scenario];
    const wrongBoxes = boxes * wrongRate;
    const correctBoxes = boxes - wrongBoxes;
    const droppedBoxes = boxes * dropRate;

    const prestigeTier = Math.floor(prestige / prestigePerBonus);
    const rewardActual = rewardBase + prestigeTier * SRC4.bonusPerTier;

    const truckRevenue = correctBoxes * rewardActual;
    const wrongCost = wrongBoxes * SRC4.penaltyPerBox;
    const dropCost = droppedBoxes * SRC4.boxDropMoneyPenalty;

    // 4) Telefon V4 -- her cagri +20 TL, +0.4 prestij (PhoneCallManager.ExecuteCall:452-463)
    const phoneMoney = cd.phoneCalls * callMoneyReward;
    const phonePrestige = cd.phoneCalls * callPrestigeReward;

    // 5) Quest (bugun kabul, YARIN yatar -- kira'yi odeyemez)
    //    ROUND 10: gun 16'da kabul edilen quest AYNI GUN, zafer ILANINDAN SONRA
    //    kapaniyor (QuestManager.SettleAcceptedQuestsOnGameEnd cs:839 <-
    //    DayCycleManager.cs:779-781) -> ceza sonucsuz, odul kasaya yaziliyor.
    let questDecision = { accepted: false, money: 0, prestige: 0 };
    let q16Money = 0, q16Prestige = 0;
    if (questsEnabled) {
      const capacity = {
        trucks: tt.fullTrucksPerDay,
        shelfPlacements: Math.min(tt.productionCapPerDay, cd.productSupply),
        packedBoxes: Math.min(tt.productionCapPerDay, cd.productSupply),
        phoneAnswers: cd.phoneCalls,
      };
      const lastDay = day === SRC4.maxDays;
      questDecision = questDailyDecision(questTier, capacity, mode, {
        dailyQuestCount, assets: questAssets, prestigeScale: questPrestigeScale,
        noPenalty: lastDay && day16Settlement,
        selection: questSelection, selectionOpts: questSelectionOpts, uiSlots: questUiSlots,
      });
      if (lastDay) {
        if (day16Settlement) { q16Money = questDecision.money; q16Prestige = questDecision.prestige; }
      } else if (questDecision.accepted) {
        questSettlePending = { money: questDecision.money, prestige: questDecision.prestige };
      }
    }

    const grossIncome = truckRevenue + phoneMoney + qMoney;
    const grossCost = wrongCost + dropCost;
    const net = grossIncome - grossCost;
    // ledgerMode: MoneySystem'in 0'a kirpmasini ve grace'i BYPASS eder (tani amacli).
    let cashBeforeRent = ledgerMode ? (cash + net) : Math.max(0, cash + net); // MoneySystem.cs:91 Mathf.Max(0,...)

    // 6) Prestij
    prestige += cd.served * SRC4.customerServedPrestigeBonus;
    prestige += cd.lost * SRC4.customerLostPrestigePenalty;
    prestige += cd.missedQuota * SRC4.customerMissedQuotaPrestigePenalty;
    prestige += wrongBoxes * SRC4.wrongDeliveryPrestigePenalty;
    // ROUND 10: Round 6 S4'un "yanlis urun ver" kanali. VARSAYILAN KAPALI
    // (wrongProductRate=0) -- acilirsa taban kosum degisir, karsilastirmalarda
    // ayni deger kullanilmali.
    if (wrongProductRate > 0) {
      prestige += cd.served * cd.returnFrac * wrongProductRate * wrongProductPrestigePenalty;
    }
    prestige += droppedBoxes * SRC4.boxDropPrestigePenalty;
    prestige += phonePrestige;
    prestige += qPrestige;
    if (prestige >= SRC4.maxPrestige && prestigeCapDay === null) prestigeCapDay = day;
    prestige = Math.max(0, Math.min(SRC4.maxPrestige, prestige));

    // 7) Kira (gun sonu, DayCycleManager.TryProcessMoneyCheck cs:587-654)
    let rentAmount = 0, event = '';
    const isRentDay = day % SRC4.rentIntervalDays === 0;
    if (isRentDay) {
      rentAmount = Math.round(baseRent * Math.pow(rentGrowthMultiplier, rentCycle) * SRC4.rentScaledMultiplier);
      if (ledgerMode) { cash = cashBeforeRent - rentAmount; rentCycle++; if (cash < 0 && event !== 'ACIK') event = 'ACIK'; }
      else if (cashBeforeRent >= rentAmount) { cash = cashBeforeRent - rentAmount; rentCycle++; }
      else if (!graceUsed) {
        const g = Math.round(cashBeforeRent * SRC4.gracePaymentPercent);
        cash = cashBeforeRent - g; graceUsed = true; rentCycle++; event = 'GRACE';
      } else { bankrupt = true; bankruptDay = day; event = 'IFLAS'; cash = cashBeforeRent; }
    } else cash = cashBeforeRent;

    // ROUND 10: gun-16 quest settlement'i KIRADAN SONRA yatar (zafer ilan
    // edilmis olur) -> son gun kirasini odemeye YARDIM ETMEZ. Round 8 S8:
    // olculen buyukluk kasanin %0.1-8.2'si, denge riski YOK.
    if (q16Money || q16Prestige) {
      if (!bankrupt) {
        cash += q16Money;
        prestige = Math.max(0, Math.min(SRC4.maxPrestige, prestige + q16Prestige));
      }
    }

    rows.push({
      gun: day,
      gunSn: Math.round(dayDurationSec4(day)),
      aktifSn: Math.round(cd.dayActiveSec),
      kota: cd.quota,
      spawn: +cd.spawned.toFixed(1),
      servis: +cd.served.toFixed(1),
      kacan: +cd.lost.toFixed(1),
      kotaKacan: +cd.missedQuota.toFixed(1),
      urunArzi: +cd.productSupply.toFixed(1),
      mekTavan: +tt.boxesPerDay.toFixed(1),
      kutu: +boxes.toFixed(1),
      odulKutu: rewardActual,
      tirGeliri: Math.round(truckRevenue),
      telefon: Math.round(phoneMoney),
      questYatan: Math.round(qMoney + q16Money),
      cezalar: -Math.round(grossCost),
      netGelir: Math.round(net),
      prestij: +prestige.toFixed(2),
      kira: rentAmount,
      kasa: Math.round(cash),
      olay: event,
    });

    if (bankrupt) break;
    if (!ledgerMode && prestige <= 0) { bankrupt = true; bankruptDay = day; event = 'PRESTIJ-0'; break; }
  }

  const cumNet = rows.reduce((a, r) => a + r.netGelir, 0);
  const last = rows[rows.length - 1];
  return {
    version: 'v5.1', playerCount, label, scenario, mode,
    bankrupt, bankruptDay, prestigeCapDay, rows,
    finalCash: last?.kasa, finalPrestige: last?.prestij,
    // GameStateManager.CheckWinCondition (cs:696-712): gun 16 + prestij > 0
    win: !bankrupt && rows.length === SRC4.maxDays && (last?.prestij ?? 0) > 0,
    cumulativeNet: cumNet,
    avgDailyNet: +(cumNet / rows.length).toFixed(1),
  };
}

module.exports = {
  SRC, ASSUMED, QUEST_ASSETS, CARGO_VALUES, CARGO_AVG,
  OVERHEAD_CODE, OVERHEAD_TOTAL,
  dayDurationSec, secPerGameHour, truckWindowSec, customerWindowSec,
  hangarStayFor, truckThroughput, customerDemand, customerThroughput,
  phoneIncome, questCompletionProb, questDailyDecision, runSim,
  PHONE_ROLLS_PER_DAY,
  // v3.1 masa cekismesi
  tableContentionEfficiency, packingTablesForLevel,
  // PlateUp modeli (PROPOSED, 2026-08-29, kota-para BAGLANTILI v2)
  PLATEUP, plateUpCeiling, plateUpQuota, plateUpArrivalInterval,
  plateUpTimeSkipMinutes, plateUpDayOutcome, plateUpBoxSupply, runSimPlateUp,
  // v4.0 KANONIK BIRLESIK MODEL (Round 1, 2026-08-30)
  SRC4, ASSUMED4, WAVE_INTERVAL_FACTOR, waveIntervalFactor, quotaFor, arrivalIntervalFor,
  rewardPerBoxFor, cargoValuesFor, timeSkipMinutesFor, dayDurationSec4,
  truckThroughputWindowed, fullCustomerDay, runFullSim,
};

// ============================================================================
// CLI
// ============================================================================
if (require.main === module) {
  const B = (s) => `\n${'='.repeat(78)}\n ${s}\n${'='.repeat(78)}`;

  console.log(B('0) GUN TAKVIMI: gun suresi ve pencereler (SAHNE realDuration=200s)'));
  const calRows = [];
  for (const d of [1, 2, 3, 4, 8, 12, 16]) {
    calRows.push({
      gun: d,
      gunSn: dayDurationSec(d),
      snPerOyunSaati: +secPerGameHour(d).toFixed(1),
      tirPenceresiSn: +truckWindowSec(d).toFixed(1),
      musteriPenceresiSn: +customerWindowSec(d).toFixed(1),
    });
  }
  console.table(calRows);
  console.log(`Gun 1-16 toplam gercek sure: ${(() => { let s = 0; for (let d = 1; d <= 16; d++) s += dayDurationSec(d); return s; })()}s ` +
              `(~${((() => { let s = 0; for (let d = 1; d <= 16; d++) s += dayDurationSec(d); return s; })() / 60).toFixed(1)} dk oynanis)`);

  console.log(B('1) GUNDE KAC TIR? (numHangars=1, oyuncu-bazli hangar suresi)'));
  const truckRows = [];
  for (const p of [1, 2, 3, 4]) {
    for (const mode of ['strict', 'optimistic']) {
      for (const d of [1, 8, 16]) {
        const t = truckThroughput(p, ASSUMED.boxesPerMinPerPlayer.Normal, d, 1, mode, ASSUMED.laborShareTruck);
        truckRows.push({
          P: p, bant: mode, gun: d,
          hangarSn: t.hangarStaySec,
          devirSn: +t.avgCycleSec.toFixed(1),
          tirGun: +t.trucksPerDay.toFixed(2),
          kutuTir: +t.avgDeliverablePerTruck.toFixed(2),
          kutuGun: +t.boxesPerDay.toFixed(2),
          tamDolanTirGun: +t.fullTrucksPerDay.toFixed(2),
          mekanikTavanTir: +t.absoluteTruckCeiling.toFixed(2),
        });
      }
    }
  }
  console.table(truckRows);
  console.log(`Devir-arasi olu sure: KOD-DOGRULANMIS ${OVERHEAD_CODE}s (exitDelay 5 + ort respawn 4) ` +
              `+ VARSAYILAN anim ${ASSUMED.animBufferSeconds}s = ${OVERHEAD_TOTAL}s`);

  console.log(B('1b) MASA CEKISMESI (v3.1) -- cekisme verimliligi eta ve S duyarliligi'));
  {
    const rows = [];
    for (const S of [4, 6, 8]) {
      for (const p of [1, 2, 3, 4]) {
        const cyc = 60 / ASSUMED.boxesPerMinPerPlayer.Normal; // 30 sn
        const e1 = tableContentionEfficiency(p, 1, S, cyc);
        const e2 = tableContentionEfficiency(p, 2, S, cyc);
        rows.push({
          S_sn: S, P: p,
          'eta 1 masa': +e1.toFixed(4), 'eta 2 masa': +e2.toFixed(4),
          'v3.0 (cekisme yok)': 1,
          'v3.0 -> v3.1 kayip': (100 * (e1 - 1)).toFixed(1) + '%',
          '2. masa kazanci': (100 * (e2 / e1 - 1)).toFixed(1) + '%',
        });
      }
    }
    console.table(rows);
    console.log(`Canli: ${SRC.packingTablesAtLevel0} masa aktif (sahnede toplam ${SRC.packingTableTotal}), ` +
                `S = ${ASSUMED.tableBusySeconds} sn (VARSAYIM).`);
  }

  console.log(B('2) HANGAR SAYISI DUYARLILIGI (gun 8, Normal)'));
  const hangRows = [];
  for (const p of [1, 2, 3, 4]) {
    const r = { P: p };
    for (const nh of [1, 2, 3]) {
      r[`strict_${nh}h`] = +truckThroughput(p, 2.0, 8, nh, 'strict', ASSUMED.laborShareTruck).boxesPerDay.toFixed(1);
      r[`optim_${nh}h`] = +truckThroughput(p, 2.0, 8, nh, 'optimistic', ASSUMED.laborShareTruck).boxesPerDay.toFixed(1);
    }
    r.uretimTavani = +truckThroughput(p, 2.0, 8, 1, 'optimistic', ASSUMED.laborShareTruck).productionCapPerDay.toFixed(1);
    hangRows.push(r);
  }
  console.table(hangRows);

  console.log(B('3) MUSTERI / PRESTIJ VERIMI (SERI kuyruk, maxQueueSize=2)'));
  const custRows = [];
  for (const p of [1, 2, 3, 4]) {
    for (const mode of ['strict', 'optimistic']) {
      const d = 8;
      const dem = customerDemand(p, ASSUMED.startingActiveInteractables, 1);
      const c = customerThroughput(p, d, dem, 'Normal', ASSUMED.laborShareTruck, mode);
      custRows.push({
        P: p, bant: mode, gun: d, talep: dem,
        seriTavan: +c.serviceSlots.toFixed(1),
        emekTavani: +c.laborCapacity.toFixed(1),
        servisEdilen: +c.served.toFixed(1),
        kacan: +c.lost.toFixed(1),
        hicGelmeyen: +c.skipped.toFixed(1),
        prestijNetGun: +(c.served * SRC.customerServedPrestigeBonus + c.lost * SRC.customerLostPrestigePenalty).toFixed(2),
      });
    }
  }
  console.table(custRows);

  console.log(B('4) TELEFON GELIRI (P-BAGIMSIZ -- SetCallChance govdesi bos)'));
  for (const mode of ['strict', 'optimistic']) {
    const ph = phoneIncome(mode);
    console.log(`  ${mode.padEnd(11)}: calma/gun=${ph.rings.toFixed(2)}  yanit/gun=${ph.answers.toFixed(2)}  ` +
                `para=${ph.money.toFixed(1)} TL  prestij=${ph.prestige.toFixed(2)}  ` +
                `ekranda calma suresi=${ph.ringOccupancySec.toFixed(0)}s/gun`);
  }
  const supportPh = phoneIncome('optimistic', true);
  console.log(`  CUSTOMER SUPPORT gunu (x1.5, cap 0.65): calma/gun=${supportPh.rings.toFixed(2)} para=${supportPh.money.toFixed(1)} TL`);

  console.log(B('5) QUEST TAMAMLANMA + EV (tier gate=Easy, gun 8, Normal, optimistic)'));
  const capDemo = (() => {
    const t = truckThroughput(2, 2.0, 8, 1, 'optimistic', ASSUMED.laborShareTruck);
    return { trucks: t.fullTrucksPerDay, shelfPlacements: t.productionCapPerDay, packedBoxes: t.productionCapPerDay, phoneAnswers: phoneIncome('optimistic').answers };
  })();
  console.log(`  (2P kapasitesi: tamDolanTir=${capDemo.trucks.toFixed(2)}/gun, uretim=${capDemo.shelfPlacements.toFixed(2)} kutu/gun, telefonYanit=${capDemo.phoneAnswers.toFixed(2)}/gun)`);
  console.table(QUEST_ASSETS.map(q => {
    const c = questCompletionProb(q, capDemo, 'optimistic');
    return {
      id: q.id, tier: ['Easy', 'Med', 'Hard'][q.tier], tip: q.type, hedef: q.target, renkKilit: q.colorLocked ? 'E' : '-',
      odul: q.mR, ceza: q.mP,
      tamamlanma: +(c * 100).toFixed(0) + '%',
      paraEV: +(c * q.mR - (1 - c) * q.mP).toFixed(1),
      prestijEV: +(c * q.pR - (1 - c) * q.pP).toFixed(2),
    };
  }));

  console.log(B('6) 16-GUN TABAN KOSU -- OPTIMISTIC bant (Normal, 1 hangar, quest+telefon acik)'));
  const optRuns = [1, 2, 3, 4].map(p => runSim(p, { scenario: 'Normal', mode: 'optimistic', label: `${p}P-Normal-OPT` }));
  console.table(optRuns.map(s => ({
    P: s.playerCount, iflas: s.bankrupt ? `GUN ${s.bankruptDay}` : 'yok',
    ortGunlukNet: s.avgDailyNet, kumulatifNet16: Math.round(s.cumulativeNet),
    sonKasa: s.finalCash, sonPrestij: s.finalPrestige,
    prestijTavanGunu: s.prestigeCapDay ?? '-',
  })));

  console.log(B('7) 16-GUN TABAN KOSU -- STRICT bant (Normal, 1 hangar, quest+telefon acik)'));
  const strRuns = [1, 2, 3, 4].map(p => runSim(p, { scenario: 'Normal', mode: 'strict', label: `${p}P-Normal-STR` }));
  console.table(strRuns.map(s => ({
    P: s.playerCount, iflas: s.bankrupt ? `GUN ${s.bankruptDay}` : 'yok',
    ortGunlukNet: s.avgDailyNet, kumulatifNet16: Math.round(s.cumulativeNet),
    sonKasa: s.finalCash, sonPrestij: s.finalPrestige,
    prestijTavanGunu: s.prestigeCapDay ?? '-',
  })));

  console.log(B('8) YAVAS SENARYO (Slow) -- iki bant'));
  const slowRuns = [];
  for (const mode of ['optimistic', 'strict']) {
    for (const p of [1, 2, 3, 4]) slowRuns.push(runSim(p, { scenario: 'Slow', mode, label: `${p}P-Slow-${mode}` }));
  }
  console.table(slowRuns.map(s => ({
    P: s.playerCount, bant: s.mode, iflas: s.bankrupt ? `GUN ${s.bankruptDay}` : 'yok',
    ortGunlukNet: s.avgDailyNet, kumulatifNet16: Math.round(s.cumulativeNet),
    sonKasa: s.finalCash, sonPrestij: s.finalPrestige,
  })));

  console.log(B('9) GELIR TABANI TABLOSU (FAZ2/FAZ3 GIRDISI) -- gunluk net, gun 1/4/8/12/16'));
  const baseRows = [];
  for (const mode of ['optimistic', 'strict']) {
    for (const p of [1, 2, 3, 4]) {
      const s = runSim(p, { scenario: 'Normal', mode });
      const g = (d) => s.rows.find(r => r.gun === d);
      const v = (d) => g(d) ? g(d).netGelir : 'IFLAS';
      // Kira serisi iflastan bagimsiz: baseRent * 1.15^cycle (dongu 0..3)
      const rentAt = (cycle) => Math.round(SRC.baseRentByPlayerCount[p - 1] * Math.pow(SRC.rentGrowthMultiplier, cycle));
      baseRows.push({
        P: p, bant: mode,
        gun1: v(1), gun4: v(4), gun8: v(8), gun12: v(12), gun16: v(16),
        kumulatif16: s.bankrupt ? `${Math.round(s.cumulativeNet)} (gun ${s.bankruptDay})` : Math.round(s.cumulativeNet),
        kira4: rentAt(0), kira8: rentAt(1), kira12: rentAt(2), kira16: rentAt(3),
        kira16Toplam: [0, 1, 2, 3].reduce((a, c) => a + rentAt(c), 0),
      });
    }
  }
  console.table(baseRows);

  console.log(B('10) 2P OPTIMISTIC -- gun gun detay (ornek okuma)'));
  console.table(runSim(2, { scenario: 'Normal', mode: 'optimistic' }).rows);

  console.log(B('11) KIRA BASKISI: kira / gunluk net orani (kac gunluk gelir = 1 kira?)'));
  const pressRows = [];
  for (const mode of ['optimistic', 'strict']) {
    for (const p of [1, 2, 3, 4]) {
      const s = runSim(p, { scenario: 'Normal', mode });
      const g = (d) => s.rows.find(r => r.gun === d);
      const row = { P: p, bant: mode };
      for (const d of [4, 8, 12, 16]) {
        const r = g(d);
        row[`gun${d}`] = r && r.netGelir > 0 ? +(r.kira / r.netGelir).toFixed(2) : 'N/A';
      }
      pressRows.push(row);
    }
  }
  console.table(pressRows);
  console.log('Yorum: 4.0 = o gunun kirasi 4 gunluk gelire esit (kira araligi 4 gun -> 1.0 tam denge).');

  console.log(B('12) BEKLEME SURELERI OZETI (canli degerler)'));
  console.table([
    { deger: 'hangarStayDuration 1P/2P/3P/4P', canli: SRC.hangarStayByPlayerCount.join(' / ') + ' sn', kaynak: 'EkonomiAyarlari.asset:24' },
    { deger: 'tir exitDelay', canli: SRC.exitDelay + ' sn', kaynak: 'Truck.prefab:196' },
    { deger: 'tir respawnDelay', canli: SRC.respawnDelayRange.join('-') + ' sn (ort 4)', kaynak: 'unity:36776' },
    { deger: 'musteri sabri (min-max)', canli: `${SRC.customerMinWaitTime}-${SRC.customerMaxWaitTime} sn (ort ${(SRC.customerMinWaitTime + SRC.customerMaxWaitTime) / 2})`, kaynak: 'Customer.prefab:2305-2306' },
    { deger: 'musteri interactionTime', canli: SRC.customerInteractionTime + ' sn', kaynak: 'Customer.prefab:2307' },
    { deger: 'telefon ringDuration', canli: SRC.phoneRingDuration + ' sn', kaynak: 'unity:14158' },
    { deger: 'gun uzunlugu (gun1 / gun16)', canli: `${dayDurationSec(1)} / ${dayDurationSec(16)} sn`, kaynak: 'unity:15995-15996' },
    { deger: 'oyun-saati basina gercek sure (gun1/16)', canli: `${secPerGameHour(1).toFixed(1)} / ${secPerGameHour(16).toFixed(1)} sn`, kaynak: 'turev' },
  ]);

  console.log(B('13) PLATEUP MODELI v2 (KOTA-PARA BAGLANTILI) -- dailyCustomerCountByDay + aralik + telefon + gelir'));
  console.log('DUZELTME (koordinator geri bildirimi, dogrulandi): musteri tek urun kaynagi ' +
              '(CustomerAI.cs:1442-1444 Instantiate, ShelfState.cs 0 Instantiate) -- kota = gunluk kutu/gelir tavani.');
  console.log('ANA SENARYO ARTIK 1 ISTASYON (SRC.serviceStations=1, sahne dogrulandi -- 2. masa GARANTI DEGIL).');

  console.log('13a) P-bazli KOTA egrisi (dailyCustomerCountByDay), CANLI 1-istasyon ile:');
  const quotaRows = [];
  for (let d = 1; d <= 16; d++) {
    const row = { gun: d, pencereSn: +customerWindowSec(d).toFixed(1) };
    for (const p of [1, 2, 3, 4]) row[`P${p}`] = plateUpQuota(d, p);
    quotaRows.push(row);
  }
  console.table(quotaRows);
  console.log('Not: P3/P4 P2ye COK YAKIN -- 1 istasyonda urun-arzi P3ten itibaren istasyon-slotuyla ' +
              'doyuyor (labor degil). Bu YUVARLAMA HATASI DEGIL, gercek mekanik doygunluk.');

  console.log('13b) Aralik / timeSkip (1-istasyon, ana senaryo):');
  console.table([1, 2, 3, 4].map(p => ({
    P: p,
    customerArrivalInterval_sn: +plateUpArrivalInterval(p).toFixed(1),
    timeSkipAmount_dk: +plateUpTimeSkipMinutes(p).toFixed(1),
    gun16_kota: plateUpQuota(16, p),
    gun16_tavan_strict: +plateUpCeiling(16, p, 'Normal', 'strict').toFixed(1),
  })));

  console.log('13c) GEREKLI rewardPerBox (P-bazli) -- kira/kota oranindan geriye turetildi ' +
              '(behavioralMargin=1.176, hata orani Normal wrongDelivery+drop=%17 dusulmus):');
  {
    const windows = { 0: [1, 4], 1: [5, 8], 2: [9, 12], 3: [13, 16] };
    const errorFactor = 1 - ASSUMED.wrongDeliveryRate.Normal - ASSUMED.physicalDropRate.Normal; // 0.83
    const reqRows = [];
    for (const p of [1, 2, 3, 4]) {
      for (const [cyc, [d0, d1]] of Object.entries(windows)) {
        let sumQ = 0, n = 0;
        for (let d = d0; d <= d1; d++) { sumQ += plateUpQuota(d, p); n++; }
        const avgQ = sumQ / n;
        const rent = Math.round(SRC.baseRentByPlayerCount[p - 1] * Math.pow(SRC.rentGrowthMultiplier, +cyc));
        const dailyRentReq = rent / SRC.rentIntervalDays;
        const reqDailyRevenue = dailyRentReq * PLATEUP.behavioralMargin;
        const reqRewardPerBox = reqDailyRevenue / (avgQ * errorFactor);
        reqRows.push({ P: p, dongu: +cyc, gunler: `${d0}-${d1}`, avgKota: +avgQ.toFixed(1), kira: rent, gerekliRewardPerBox: +reqRewardPerBox.toFixed(1) });
      }
    }
    console.table(reqRows);
    console.log('SECILEN rewardPerBoxByPlayerCount:', PLATEUP.rewardPerBoxByPlayerCount, '(en kotu-dongu ihtiyacini karsilayacak sekilde yuvarlandi)');
  }

  console.log(B('14) runSimPlateUp -- TAM PARA+PRESTIJ DOGRULAMASI (kota gelire baglı, 1-istasyon, flat rewardPerBox=50 KIYASLAMA)'));
  {
    const rows = [];
    for (const scenario of ['Normal', 'Slow']) {
      for (const mode of ['strict', 'optimistic']) {
        for (const p of [1, 2, 3, 4]) {
          const flat = runSimPlateUp(p, { scenario, mode, rewardPerBoxByPlayerCount: [50, 50, 50, 50] });
          rows.push({
            senaryo: scenario, bant: mode, P: p, rewardModel: 'flat50',
            iflas: flat.bankrupt ? `GUN ${flat.bankruptDay}` : 'yok',
            sonKasa: flat.finalCash, sonPrestij: flat.finalPrestige,
            kumulatifNet16: Math.round(flat.cumulativeNet),
          });
        }
      }
    }
    console.table(rows);
    console.log('^ flat rewardPerBox=50 (canli deger) ile P3/P4 iflas riskini gosterir -- rent P ile 3.6x buyurken ' +
                'urun-arzi (kota) P3ten sonra DUZLESIYOR (13a notu).');
  }

  console.log(B('15) runSimPlateUp -- rewardPerBoxByPlayerCount [50,55,70,88] ile DUZELTILMIS'));
  {
    const rows = [];
    for (const scenario of ['Normal', 'Slow']) {
      for (const mode of ['strict', 'optimistic']) {
        for (const p of [1, 2, 3, 4]) {
          const s = runSimPlateUp(p, { scenario, mode });
          rows.push({
            senaryo: scenario, bant: mode, P: p,
            iflas: s.bankrupt ? `GUN ${s.bankruptDay}` : 'yok',
            sonKasa: s.finalCash, sonPrestij: s.finalPrestige,
            kumulatifNet16: Math.round(s.cumulativeNet),
            prestijTavanGunu: s.prestigeCapDay ?? '-',
          });
        }
      }
    }
    console.table(rows);
  }

  console.log(B('16) runSimPlateUp -- 4P/Slow/strict GUN GUN DETAY (en kotu senaryo, duzeltilmis reward ile)'));
  console.table(runSimPlateUp(4, { scenario: 'Slow', mode: 'strict' }).rows);

  console.log(B('17) rentGrowthMultiplier=1.20 REGRESYON KONTROLU (2026-08-20 karari BOZULMADI mi?)'));
  {
    const rows = [];
    for (const p of [1, 2, 3, 4]) {
      const s = runSimPlateUp(p, { scenario: 'Normal', mode: 'strict' });
      rows.push({ P: p, senaryo: 'Normal-strict', iflas: s.bankrupt ? `GUN ${s.bankruptDay}` : 'yok', sonKasa: s.finalCash });
    }
    console.table(rows);
    console.log('Not: rentGrowthMultiplier bu turda DEGISTIRILMEDI (hala 1.20) -- P1/P2in 2026-08-20da ' +
                'onaylanan sagliginin YENI kota-para baglantisi ALTINDA da korundugunu dogrular.');
  }

  // ==========================================================================
  // v4.0 BLOKLARI (Round 1, 2026-08-30) -- KANONIK runFullSim
  // ==========================================================================
  console.log(B('18) v4.0 CANLI SABIT DENETIMI -- turetilmis degerler'));
  console.log(`  Wave aralik carpani (zaman-agirlikli 1/rate, 8-17): ${WAVE_INTERVAL_FACTOR.toFixed(4)}`);
  console.table([1, 2, 3, 4].map(p => ({
    P: p,
    kota_g1: quotaFor(1, p), kota_g8: quotaFor(8, p), kota_g16: quotaFor(16, p),
    kota16Toplam: Array.from({ length: 16 }, (_, i) => quotaFor(i + 1, p)).reduce((a, b) => a + b, 0),
    aralikSn: arrivalIntervalFor(p),
    aralikWaveli: +(arrivalIntervalFor(p) * WAVE_INTERVAL_FACTOR).toFixed(1),
    odulKutu: rewardPerBoxFor(p),
    kargo: '{' + cargoValuesFor(p).join(',') + '}',
    hangarSn: SRC4.hangarStayByPlayerCount[p - 1],
    timeSkipDk: timeSkipMinutesFor(p),
    baslangicPara: Math.round(SRC4.baseStartingMoney * Math.pow(SRC4.moneyMultiplierPerPlayer, p - 1)),
    kira_d0: SRC4.baseRentByPlayerCount[p - 1],
  })));

  console.log(B('19) v4.0 GUN AKISI -- 2P Normal/strict gun gun (erken bitis + urun arzi gorunur)'));
  console.table(runFullSim(2, { scenario: 'Normal', mode: 'strict' }).rows);

  console.log(B('20) v4.0 TAM MATRIS -- 4P x Normal/Slow x strict/optimistic (16 hucre)'));
  {
    const rows = [];
    for (const scenario of ['Normal', 'Slow']) {
      for (const mode of ['strict', 'optimistic']) {
        for (const p of [1, 2, 3, 4]) {
          const s = runFullSim(p, { scenario, mode });
          rows.push({
            senaryo: scenario, bant: mode, P: p,
            iflas: s.bankrupt ? `GUN ${s.bankruptDay}` : 'yok',
            kazandi: s.win ? 'EVET' : 'hayir',
            sonKasa: s.finalCash, sonPrestij: s.finalPrestige,
            kumulatifNet16: Math.round(s.cumulativeNet),
            ortGunlukNet: s.avgDailyNet,
            prestijTavanGunu: s.prestigeCapDay ?? '-',
          });
        }
      }
    }
    console.table(rows);
  }

  console.log(B('21) v4.0 vs ESKI MODELLER -- ayni senaryoda sonuc karsilastirmasi'));
  {
    const rows = [];
    for (const scenario of ['Normal', 'Slow']) {
      for (const mode of ['strict', 'optimistic']) {
        for (const p of [1, 2, 3, 4]) {
          const a = runSim(p, { scenario, mode });
          const b = runSimPlateUp(p, { scenario, mode });
          const c = runFullSim(p, { scenario, mode });
          rows.push({
            senaryo: scenario, bant: mode, P: p,
            'runSim(v3.1)': a.bankrupt ? `IFLAS g${a.bankruptDay}` : a.finalCash,
            'runSimPlateUp': b.bankrupt ? `IFLAS g${b.bankruptDay}` : b.finalCash,
            'runFullSim(v4)': c.bankrupt ? `IFLAS g${c.bankruptDay}` : c.finalCash,
            'v3.1 vs v4 fark': (a.bankrupt !== c.bankrupt || a.bankruptDay !== c.bankruptDay) ? 'IFLAS AYRISMASI'
              : (a.finalCash === c.finalCash ? 'ayni' : `${Math.round(100 * (c.finalCash / Math.max(a.finalCash, 1) - 1))}%`),
          });
        }
      }
    }
    console.table(rows);
    console.log('Beklenti: ORTUSMEMELERI NORMAL. runSim(v3.1) musteri talebini SILINMIS kapasite');
    console.log('formulunden turetiyor + telefonu SILINMIS phoneRingChancePerHour ile modelliyor +');
    console.log('gunu hep tam uzunlukta variyor + 1 musteri=1 urun variyor. v4 canli kodu yansitir.');
  }

  console.log(B('22) v4.0 DUYARLILIK -- telefon kullanim orani (zaman-atlamasi gunu KISALTIYOR)'));
  {
    const rows = [];
    for (const p of [1, 2, 3, 4]) {
      const r = { P: p };
      for (const u of [0, 0.25, 0.5, 0.85, 1.0]) {
        const s = runFullSim(p, { scenario: 'Normal', mode: 'strict', phoneUseRate: u });
        r[`kullanim${Math.round(u * 100)}%`] = s.bankrupt ? `IFLAS g${s.bankruptDay}` : s.finalCash;
      }
      rows.push(r);
    }
    console.table(rows);
    console.log('Telefon +20 TL/cagri veriyor AMA SkipTime gunun GERCEK saniyelerini yakiyor');
    console.log('(cagri basina T[P]x0.30303 sn, GUNDEN BAGIMSIZ) -> hem TIR uretim penceresi');
    console.log('hem SERVIS penceresi kisaliyor. v5 (Round 10): optimum %10-25 bandinda.');
  }

  console.log(B('23) v5.0 TELEFON ACIK/KAPALI -- olculu kullanim (%20) vs hic kullanmama'));
  {
    const rows = [];
    for (const scenario of ['Normal', 'Slow']) {
      for (const mode of ['strict', 'optimistic']) {
        for (const p of [1, 2, 3, 4]) {
          const off = runFullSim(p, { scenario, mode, phoneUseRate: 0 });
          const def = runFullSim(p, { scenario, mode });
          const d = (!off.bankrupt && !def.bankrupt)
            ? `${Math.round(100 * (def.finalCash / Math.max(off.finalCash, 1) - 1))}%` : 'iflas';
          rows.push({
            senaryo: scenario, bant: mode, P: p,
            telefonKAPALI: off.bankrupt ? `IFLAS g${off.bankruptDay}` : off.finalCash,
            [`telefon${Math.round(ASSUMED4.phoneUseRate[mode] * 100)}%`]: def.bankrupt ? `IFLAS g${def.bankruptDay}` : def.finalCash,
            telefonunNetEtkisi: d,
          });
        }
      }
    }
    console.table(rows);
    console.log('v5 (ROUND 10): Round 1/2\'nin "beceri-ters TRAP"i BUYUK OLCUDE MODEL ARTIFAKTIYDI.');
    console.log('Duzeltilmis modelde STRICT bantta olculu kullanim (%15-25) POZITIF; telefon yalniz');
    console.log('VARIS darbogaziysa (kuyruk bos + kota kaldi) kar ediyor. Arz-bagli optimistic');
    console.log('P3/P4 hucrelerinde optimum %0 -- ogretilebilir kural: "bosta beklerken cevir".');
  }
}
