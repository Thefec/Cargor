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
  baseRentByPlayerCount:      [500, 900, 1200, 1500], // asset:15 hex f4010000/84030000/b0040000/dc050000
  rentGrowthMultiplier:       1.15,   // asset:16   (= GameEconomySettings.cs:24)
  rentIntervalDays:           4,      // asset:18   (= cs:27)
  gracePaymentPercent:        0.8,    // asset:19   (= cs:30)
  rentScaledMultiplier:       1.0,    // asset:20   (perk yoksa 1)
  rewardPerBox:               50,     // asset:21   (= cs:42)
  penaltyPerBox:              40,     // asset:22   (= cs:45)  YANLIS RENK teslimat
  hangarStayDurationLegacy:   30,     // asset:23   (= cs:48) YALNIZ dizi bos ise
  hangarStayByPlayerCount:    [90, 60, 40, 30], // asset:24 hex 5a/3c/28/1e (= cs:51)
  prestigePerBonus:           4,      // asset:25   (= cs:54)
  bonusPerTier:               5,      // asset:26   (= cs:57)
  rewardVolatility:           0,      // asset:27   (perk kapali)
  boxDropMoneyPenalty:        5,      // asset:29   (= cs:72) FIZIKSEL dusme
  phoneRingChancePerHour:     0.30,   // asset:30   (= cs:81)
  phoneRingEventMultiplier:   1.5,    // asset:31   (= cs:84)
  callMoneyReward:            20,     // asset:33   (= cs:90)
  callPrestigeReward:         0.2,    // asset:34   (= cs:93)
  customerLostPrestigePenalty:  -0.6, // asset:35   (= cs:102)
  customerServedPrestigeBonus:   0.2, // asset:36   (= cs:105)
  wrongProductPrestigePenalty: -0.04, // asset:37   (= cs:108)
  boxDropPrestigePenalty:     -0.02,  // asset:38   (= cs:111)
  wrongDeliveryPrestigePenalty: -0.08,// asset:39   (= cs:114)
  festivalBonusMin:           100,    // asset:40   (= cs:123) FALLBACK -- artik kullanilmiyor
  festivalBonusMax:           300,    // asset:41   (= cs:126) FALLBACK
  // FESTIVAL DAY canli davranis: EventEffectManager.cs:404-408 -> kiranin %10-%20'si.
  festivalRentSharePct:       [0.10, 0.20],

  // ---- Assets/NewCss/UIScripts/DayCycleManager.cs + SAHNE -------------------
  maxDays:                    16,   // cs:35 MAX_DAYS
  dynamicDurationStartDay:    3,    // cs:36 DYNAMIC_DURATION_START_DAY
  realDurationInSeconds:      200,  // SAHNE unity:15995 (cs:50 default 160 -> AYRISMA)
  dailyDurationIncrease:      10,   // SAHNE unity:15996 (= cs:53)
  dayStartHour:               7,    // SAHNE unity:15997 (= cs:56)
  dayEndHour:                 18,   // SAHNE unity:15998 (= cs:59)

  // ---- Assets/NewCss/TruckScripts/TruckSpawner.cs + SAHNE ------------------
  truckStartHour:             8,    // SAHNE unity:36778 (= cs:67)
  truckEndHour:               17,   // SAHNE unity:36779 (= cs:70)
  minCargo:                   2,    // cs:37 MIN_CARGO_AMOUNT
  maxCargoExclusive:          6,    // cs:38 MAX_CARGO_AMOUNT (Random.Range int -> UST SINIR HARIC)
  respawnDelayRange:          [3, 5], // SAHNE unity:36776 (= cs:60) -> ort 4s
  hangarCount:                3,    // SAHNE unity:36763-36775: requiredUpgradeLevel 0/1/2
  hangarsAtLevel0:            1,    // level 0 -> yalniz ilk hangar aktif

  // ---- Assets/NewCss/TruckScripts/Truck.cs + Truck.prefab ------------------
  exitDelay:                  5,    // prefab Truck.prefab:196 (= cs:79)
  // NOT: prefab'daki rewardPerBox=10 / penaltyPerBox=2 (Truck.prefab:197-198)
  // OLU DEGER -- OnNetworkSpawn (Truck.cs:206-218) Resources'tan EkonomiAyarlari
  // yukleyip 50/40 ile EZIYOR. [HideInInspector] alanlar.

  // ---- Assets/NewCss/CustomerSripts/PrestigeManager.cs + SAHNE ------------
  startingPrestige:           6,    // SAHNE unity:25234 (= cs:16)
  maxPrestige:                100,  // SAHNE unity:25235 (= cs:19)
  prestigePerCustomer:        4,    // SAHNE unity:25237 (= cs:26)
  baseCustomerCapacity:       1,    // SAHNE unity:25238 (= cs:29)
  maxCustomerCapacity:        20,   // SAHNE unity:25239 (= cs:32)

  // ---- Assets/NewCss/CustomerSripts/CustomerManager.cs + SAHNE ------------
  maxQueueSize:               2,    // SAHNE unity:68600 (cs:57 default = DEFAULT_QUEUE_SIZE 3 -> AYRISMA)
  shelfMultiplier:            2,    // SAHNE unity:68602 (= cs:68)
  levelMultiplier:            2,    // SAHNE unity:68603 (= cs:71)
  storeLevelStart:            1,    // SAHNE unity:68604 (= cs:74)
  minVariance:               -2,    // SAHNE unity:68605 (= cs:77)
  maxVariance:                3,    // SAHNE unity:68606 (= cs:80)
  minCustomersPerDay:         1,    // SAHNE unity:68607 (= cs:83)
  maxCustomersPerDay:         50,   // SAHNE unity:68608 (= cs:86)
  spawnStartHour:             8,    // SAHNE unity:68617 (= cs DEFAULT_SPAWN_START_HOUR)
  spawnEndHour:               17,   // SAHNE unity:68618 (= cs DEFAULT_SPAWN_END_HOUR)

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
  // musteri. (CustomerManager.cs:124 `serviceTables[]` ve cs:873 AssignDropOffTable
  // KABLOLANMAMIS -- sifir cagiran; sahnede dizi 1 elemanli, unity:68615-68616.)
  serviceStations:            1,

  // ---- Assets/NewCss/GameState/DifficultyManager.cs + DifficultyManager.prefab
  baseStartingMoney:          500,  // prefab:75 (= cs:36)
  moneyMultiplierPerPlayer:   1.0,  // prefab:81 (= cs:61) -> baslangic parasi P'den BAGIMSIZ
  playerCountMultCoeff:       0.3,  // cs:430  -> 1 + (P-1)*0.3 = 1.0/1.3/1.6/1.9
  upgradeCostMultiplierPerPlayer: 1.15, // prefab:85 (= cs:76)  [FAZ3 girdisi]
  // OLU/KULLANILMAYAN (bkz AYRISMA): baseCustomerCount 10 / customerCountPerPlayer 2
  // (prefab:74,80) -> ScaledCustomerCount hicbir yere yazilmiyor, yalniz log/UI.
  // basePhoneCallChance 0.2 / phoneChancePerPlayer 0.1 (prefab:76,82) ->
  // PhoneCallManager.SetCallChance(...) GOVDESI BOS (PhoneCallManager.cs:425).
  // baseMinPatience 8 / baseMaxPatience 14 / patienceReductionPerPlayer 2
  // (prefab:77,78,83) -> yalniz sahnedeki CustomerAI ornegine yazilir, YOK.

  // ---- Assets/NewCss/UIScripts/MoneySystem.cs + SAHNE --------------------
  moneySystemSceneStartingMoney: 50000, // SAHNE unity:4734 -- DEBUG DEGERI (AYRISMA)
  moneyFloorZero:             true,  // cs:91 Mathf.Max(0, ...)

  // ---- Assets/NewCss/Phone/PhoneCallManager.cs + SAHNE -------------------
  phoneStartHour:             8,    // SAHNE unity:14156 (= cs:33)
  phoneEndHour:               18,   // SAHNE unity:14157 (= cs:36)
  phoneRingDuration:          25,   // SAHNE unity:14158 (= cs:40) GERCEK saniye
  phoneRingChanceCap:         0.65, // cs:270 Mathf.Clamp(..., 0f, 0.65f)

  // ---- Assets/NewCss/Events/EventCalendarUI.cs --------------------------
  eventFreeDays:              3,    // cs:25 INITIAL_EVENT_FREE_DAYS
  eventIntervalMin:           1,    // cs:23 EVENT_INTERVAL_MIN
  eventIntervalMax:           3,    // cs:24 EVENT_INTERVAL_MAX
  eventSkipRentDays:          true, // cs:688 (currentDay % 4 == 0) -> continue
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
// QUEST -- 30 CANLI ASSET (Assets/Resources/Quests/, 2026-07-30 okundu)
// ============================================================================
// Alanlar: tier(0=Easy,1=Medium,2=Hard), type (QuestEnums.cs:28-37):
//   1=PlaceBoxOnShelf, 2=CompleteTruck, 3=PackToy, 4=AnswerPhone
// colorLocked = requirement.requireSpecificBoxType==1 (renk/kategori filtresi)
const QUEST_ASSETS = [
  // --- EASY (11) ---
  { id: 'easy_truck_1',      file: 'Q_Easy_1_Truck',       tier: 0, type: 2, target: 1,  colorLocked: false, mR: 28, pR: 0.7, mP: 15, pP: 0.4 },
  { id: 'easy_shelf_4',      file: 'Q_Easy_2_Shelf',       tier: 0, type: 1, target: 4,  colorLocked: false, mR: 18, pR: 0.4, mP: 10, pP: 0.2 },
  { id: 'easy_shelf_red',    file: 'Q_Easy_3_ShelfRed',    tier: 0, type: 1, target: 2,  colorLocked: true,  mR: 18, pR: 0.4, mP: 10, pP: 0.2 },
  { id: 'easy_pack_4',       file: 'Q_Easy_4_Pack',        tier: 0, type: 3, target: 4,  colorLocked: false, mR: 18, pR: 0.4, mP: 10, pP: 0.2 },
  { id: 'easy_pack_toy',     file: 'Q_Easy_5_PackToy',     tier: 0, type: 3, target: 2,  colorLocked: true,  mR: 18, pR: 0.4, mP: 10, pP: 0.2 },
  { id: 'easy_phone_2',      file: 'Q_Easy_6_Phone',       tier: 0, type: 4, target: 2,  colorLocked: false, mR: 22, pR: 0.5, mP: 12, pP: 0.2 },
  { id: 'easy_shelf_yellow', file: 'Q_Easy_7_ShelfYellow', tier: 0, type: 1, target: 2,  colorLocked: true,  mR: 18, pR: 0.4, mP: 10, pP: 0.2 },
  { id: 'easy_shelf_blue',   file: 'Q_Easy_8_ShelfBlue',   tier: 0, type: 1, target: 2,  colorLocked: true,  mR: 18, pR: 0.4, mP: 10, pP: 0.2 },
  { id: 'easy_pack_cloth',   file: 'Q_Easy_9_PackCloth',   tier: 0, type: 3, target: 2,  colorLocked: true,  mR: 18, pR: 0.4, mP: 10, pP: 0.2 },
  { id: 'easy_pack_glass',   file: 'Q_Easy_10_PackGlass',  tier: 0, type: 3, target: 2,  colorLocked: true,  mR: 18, pR: 0.4, mP: 10, pP: 0.2 },
  { id: 'easy_shelf_6',      file: 'Q_Easy_11_ShelfBig',   tier: 0, type: 1, target: 6,  colorLocked: false, mR: 28, pR: 0.7, mP: 15, pP: 0.4 },
  // --- MEDIUM (10) ---
  { id: 'med_truck_2',       file: 'Q_Medium_1_Truck',     tier: 1, type: 2, target: 2,  colorLocked: false, mR: 52, pR: 1.2, mP: 29, pP: 0.6 },
  { id: 'med_shelf_7',       file: 'Q_Medium_2_Shelf',     tier: 1, type: 1, target: 7,  colorLocked: false, mR: 34, pR: 0.8, mP: 19, pP: 0.4 },
  { id: 'med_shelf_blue',    file: 'Q_Medium_3_ShelfBlue', tier: 1, type: 1, target: 3,  colorLocked: true,  mR: 34, pR: 0.8, mP: 19, pP: 0.4 },
  { id: 'med_pack_7',        file: 'Q_Medium_4_Pack',      tier: 1, type: 3, target: 7,  colorLocked: false, mR: 34, pR: 0.8, mP: 19, pP: 0.4 },
  { id: 'med_pack_cloth',    file: 'Q_Medium_5_PackCloth', tier: 1, type: 3, target: 3,  colorLocked: true,  mR: 34, pR: 0.8, mP: 19, pP: 0.4 },
  { id: 'med_phone_3',       file: 'Q_Medium_6_Phone',     tier: 1, type: 4, target: 3,  colorLocked: false, mR: 40, pR: 1.0, mP: 22, pP: 0.5 },
  { id: 'med_shelf_yellow',  file: 'Q_Medium_7_ShelfYellow',tier: 1, type: 1, target: 3, colorLocked: true,  mR: 34, pR: 0.8, mP: 19, pP: 0.4 },
  { id: 'med_shelf_red',     file: 'Q_Medium_8_ShelfRed',  tier: 1, type: 1, target: 3,  colorLocked: true,  mR: 34, pR: 0.8, mP: 19, pP: 0.4 },
  { id: 'med_pack_toy',      file: 'Q_Medium_9_PackToy',   tier: 1, type: 3, target: 3,  colorLocked: true,  mR: 34, pR: 0.8, mP: 19, pP: 0.4 },
  { id: 'med_pack_glass',    file: 'Q_Medium_10_PackGlass',tier: 1, type: 3, target: 3,  colorLocked: true,  mR: 34, pR: 0.8, mP: 19, pP: 0.4 },
  // --- HARD (9) ---
  { id: 'hard_truck_3',      file: 'Q_Hard_1_Truck',       tier: 2, type: 2, target: 3,  colorLocked: false, mR: 86, pR: 2.3, mP: 47, pP: 1.2 },
  { id: 'hard_shelf_10',     file: 'Q_Hard_2_Shelf',       tier: 2, type: 1, target: 12, colorLocked: false, mR: 57, pR: 1.5, mP: 31, pP: 0.8 },
  { id: 'hard_shelf_yellow', file: 'Q_Hard_3_ShelfYellow', tier: 2, type: 1, target: 5,  colorLocked: true,  mR: 57, pR: 1.5, mP: 31, pP: 0.8 },
  { id: 'hard_pack_10',      file: 'Q_Hard_4_Pack',        tier: 2, type: 3, target: 12, colorLocked: false, mR: 57, pR: 1.5, mP: 31, pP: 0.8 },
  { id: 'hard_pack_glass',   file: 'Q_Hard_5_PackGlass',   tier: 2, type: 3, target: 5,  colorLocked: true,  mR: 57, pR: 1.5, mP: 31, pP: 0.8 },
  { id: 'hard_shelf_blue',   file: 'Q_Hard_6_ShelfBlue',   tier: 2, type: 1, target: 5,  colorLocked: true,  mR: 57, pR: 1.5, mP: 31, pP: 0.8 },
  { id: 'hard_shelf_red',    file: 'Q_Hard_7_ShelfRed',    tier: 2, type: 1, target: 5,  colorLocked: true,  mR: 57, pR: 1.5, mP: 31, pP: 0.8 },
  { id: 'hard_pack_toy',     file: 'Q_Hard_8_PackToy',     tier: 2, type: 3, target: 5,  colorLocked: true,  mR: 57, pR: 1.5, mP: 31, pP: 0.8 },
  { id: 'hard_pack_cloth',   file: 'Q_Hard_9_PackCloth',   tier: 2, type: 3, target: 5,  colorLocked: true,  mR: 57, pR: 1.5, mP: 31, pP: 0.8 },
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

/** Gunun quest kararini modeller: 3 teklif -> rasyonel en iyi EV -> 1 kabul. */
function questDailyDecision(questTier, capacity, mode, rngPick) {
  const pool = QUEST_ASSETS.filter(q => q.tier <= questTier);
  if (pool.length === 0) return { accepted: false, money: 0, prestige: 0, pick: null };

  // 3 teklif: analitik olarak havuzun tamaminin EV dagilimindan "en iyi 3'ten
  // en iyisi" beklentisini almak yerine, deterministik ve alintilabilir olsun
  // diye TUM havuzun EV'lerini siralayip ilk 3'un ortalamasini "teklif kalitesi"
  // olarak kullaniyoruz (oyuncu genelde iyi olani goruyor ama her gun en iyiyi gormez).
  const evs = pool.map(q => {
    const c = questCompletionProb(q, capacity, mode);
    return {
      q, c,
      money: c * q.mR - (1 - c) * q.mP,
      prestige: c * q.pR - (1 - c) * q.pP,
    };
  }).sort((a, b) => b.money - a.money);

  const topK = evs.slice(0, Math.min(SRC.dailyQuestOffered, evs.length));
  const avg = {
    money: topK.reduce((a, x) => a + x.money, 0) / topK.length,
    prestige: topK.reduce((a, x) => a + x.prestige, 0) / topK.length,
  };
  if (avg.money <= 0) return { accepted: false, money: 0, prestige: 0, pick: topK[0].q.id, bestEV: evs[0] };
  return { accepted: true, money: avg.money, prestige: avg.prestige, pick: topK[0].q.id, bestEV: evs[0] };
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

module.exports = {
  SRC, ASSUMED, QUEST_ASSETS, CARGO_VALUES, CARGO_AVG,
  OVERHEAD_CODE, OVERHEAD_TOTAL,
  dayDurationSec, secPerGameHour, truckWindowSec, customerWindowSec,
  hangarStayFor, truckThroughput, customerDemand, customerThroughput,
  phoneIncome, questCompletionProb, questDailyDecision, runSim,
  PHONE_ROLLS_PER_DAY,
  // v3.1 masa cekismesi
  tableContentionEfficiency, packingTablesForLevel,
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
}
