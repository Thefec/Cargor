// ============================================================================
// Cargor 16-gun ekonomi + quest simulasyonu
// ============================================================================
// Tarih: 2026-07-18, SENKRON 2026-07-20 (holistik denetim: prestij 100-rescale + P-bazli hangar).
//   2026-07-20 degisiklikleri: prestigePerBonus 10->4, startingPrestige 15->6, maxPrestige 150->100,
//   prestij miktarlari x0.4 (customerServed 0.5->0.2, customerLost -1.5->-0.6, wrongDelivery -0.2->-0.08,
//   boxDrop -0.05->-0.02), quest prestij havuzlari x0.4, hangarStayDuration tek-30 -> P-bazli {90,60,40,30}.
// Calistirma: node tools/economy-sim/sim.js
//
// AMAC: sim <-> kod ayrisma riskini azaltmak icin TEK kontrol noktasi. Her
// deger asagida dosya:satir kaynagiyla belgelenir. Bir sonraki denetimde once
// bu dosyanin basindaki degerleri gercek kod/asset'e karsi yeniden dogrula.
//
// Selef: 2026-07-13 denetiminin sim'i (bkz. plans/economy-audit-2026-07-13.md,
// eski script artik repo disinda / scratchpad'de). Bu surumdeki METODOLOJI
// FARKLARI (2026-07-13'e gore):
//   1. YENI: tir/hangar penceresi kapasite tavani (hangarStayDuration=30s +
//      devir-arasi bosluk). Eski sim sadece "durationMin * kutu/dk * P" duz
//      oranini musteri talebiyle kiyasliyordu, hangar donguleme mekanigini
//      hic modellemiyordu.
//   2. DUZELTME: eski sim "kirik kutu" olaylarina hep penaltyPerBox(=40 TL,
//      yanlis-teslimat cezasi) uyguluyordu. Gercek kodda IKI AYRI ceza turu
//      var: (a) tira YANLIS RENK teslimat -> penaltyPerBox(40 TL) +
//      wrongDeliveryPrestigePenalty(-0.2)  [Truck.cs:578-590]; (b) kutu YERE
//      DUSME/CARPMA -> boxDropMoneyPenalty(5 TL) + boxDropPrestigePenalty
//      (-0.05) [BoxFallPenalty ailesi, GameEconomySettings.cs:66-69]. Bu
//      surum ikisini AYRI ayri modelliyor (wrongDeliveryRate + physicalDropRate).
//   3. DUZELTME: eski sim customerServedPrestigeBonus'u "correctDeliveries"
//      (tir'e dogru teslim edilen kutu) uzerinden veriyordu. Gercek kodda bu
//      bonus CustomerAI/musteri-servis akisindan gelir, Truck.cs'den bagimsizdir
//      (Truck.cs'de customerServedPrestigeBonus'a hic referans yok - grep ile
//      dogrulandi). Bu surum musteri-servis prestijini tir kapasitesine
//      BAGLI OLMAKSIZIN (demandAdjusted uzerinden) hesaplar; sadece PARA
//      geliri tir/hangar tavanina tabidir.
//   4. SOKULDU: wealthTaxRate. C# tarafinda alan tamamen kaldirilmis
//      (9d2c3b0, bkz. .claude/agent-memory/economist/wealthtax_broken_wiring.md).
//      EkonomiAyarlari.asset'te hala "wealthTaxRate: 0.1" YAML anahtari var
//      ama GameEconomySettings.cs'de karsilik gelen alan yok -> Unity'nin
//      "kullanilmayan serialized alan" davranisi, zararsiz orphan. Sim'de hic
//      kullanilmiyor.
//   5. SOKULDU: kota-olumu (QuotaManager). Dosya TAMAMEN silinmis
//      (commit 0c026ef, 2026-07-14, "kota sistemini kaldir (kod) + QUOTA DAY
//      event cikar" - bilincli temizlik, C1 aktivasyonu degil). Sim'de kota
//      mekanigi YOK, quota_throughput_calibration.md hafizasi bu bulguyla
//      GUNCEL DEGIL isaretlenmeli.
//
// ============================================================================
// KAYNAK DEGERLER (dosya:satir, 2026-07-18 okunan gercek deger)
// ============================================================================
const ECONOMY = {
  // Assets/Resources/EkonomiAyarlari.asset + Assets/NewCss/GameEconomySettings.cs
  baseRentByPlayerCount: [500, 900, 1200, 1500],   // asset:15 (hex-decoded) = cs:21 default
  rentGrowthMultiplier: 1.15,                       // asset:16 = cs:24 default
  rentIntervalDays: 4,                              // asset:18 = cs:27 default
  gracePaymentPercent: 0.8,                         // asset:19 = cs:30 default
  rentScaledMultiplier: 1.0,                        // asset:20 (perk yoksa 1; leveraged_rent perki 0.8 yapar, bu sim'de perk YOK)
  rewardPerBox: 50,                                 // asset:21 = cs:42 default
  penaltyPerBox: 40,                                // asset:22 = cs:45 default (YANLIS RENK teslimat cezasi)
  hangarStayDuration: 30,                            // asset:23 = cs:48 LEGACY fallback (dizi bos ise)
  // 2026-07-20 (6ef9ad6): P-bazli hangar suresi. asset:24 hex 5a/3c/28/1e = {90,60,40,30}. index=P-1.
  hangarStayDurationByPlayerCount: [90, 60, 40, 30], // asset:24 = cs:51, GetHangarStayDuration(playerCount)
  prestigePerBonus: 4,                              // asset:25 = cs:54 -- 2026-07-20 rescale 10->4
  bonusPerTier: 5,                                  // asset:25 = cs:54 default
  boxDropMoneyPenalty: 5,                            // asset:28 = cs:69 default (FIZIKSEL DUSME/CARPMA cezasi)
  wrongDeliveryPrestigePenalty: -0.08,               // asset:39 = cs:114 -- 2026-07-20 rescale -0.2->-0.08 (x0.4)
  customerLostPrestigePenalty: -0.6,                 // asset:35 = cs:102 -- 2026-07-20 rescale -1.5->-0.6 (x0.4)
  customerServedPrestigeBonus: 0.2,                  // asset:36 = cs:105 -- 2026-07-20 rescale 0.5->0.2 (x0.4)
  boxDropPrestigePenalty: -0.02,                     // asset:38 = cs:111 -- 2026-07-20 rescale -0.05->-0.02 (x0.4)
  // wrongProductPrestigePenalty: -0.04 (asset:37) -- musteriye yanlis urun gosterme; sim modellemedi (kucuk).
  // wealthTaxRate: SOKULDU (bkz. header notu #4) - artik alan yok, kullanilmiyor.

  // Assets/NewCss/CustomerSripts/PrestigeManager.cs
  startingPrestige: 6,                               // satir 16 -- 2026-07-20 rescale 15->6 (x0.4)
  maxPrestige: 100,                                  // satir 19 -- 2026-07-20 rescale 240->100 (100-skala); pratik ~96
  prestigePerCustomer: 4,                            // PrestigeManager satir 26 -- rescale 10->4 (musteri kapasite esigi)

  // Assets/NewCss/GameState/DifficultyManager.cs
  baseStartingMoney: 500,                            // satir 36
  moneyMultiplierPerPlayer: 1.0,                     // satir 61 -- 1.0 = P'den bagimsiz, hep 500 TL baslangic
  playerCountMultiplierCoeff: 0.3,                   // satir 430: 1 + (P-1)*0.3 -> musteri-talebi carpani (1.0/1.3/1.6/1.9)

  // Assets/NewCss/CustomerSripts/CustomerManager.cs
  shelfMultiplier: 2,                                // satir 68 (_shelfMultiplier)
  levelMultiplier: 2,                                // satir 71 (_levelMultiplier)
  minVariance: -2, maxVariance: 3,                   // satir 77-80 (ortalama = 0.5)
  minCustomersPerDay: 1, maxCustomersPerDay: 50,      // satir 83-86

  // Assets/NewCss/UIScripts/DayCycleManager.cs
  maxDays: 16,                                       // satir 35
  realDurationInSeconds: 160,                        // satir 50
  dailyDurationIncrease: 10,                          // satir 53
  dynamicDurationStartDay: 3,                         // satir 36 (DYNAMIC_DURATION_START_DAY)
  dayStartHour: 7, dayEndHour: 18,                    // satir 56-59

  // Assets/NewCss/TruckScripts/TruckSpawner.cs
  truckStartHour: 8, truckEndHour: 17,                // satir 67-70
  minCargoAmount: 2, maxCargoAmountExclusive: 6,      // satir 37-38 (Random.Range int UST SINIR HARIC -> gercek kume {2,3,4,5}, ort=3.5)
  respawnDelayRangeAvg: 4,                            // satir 60: Vector2(3,5) -> ortalama 4s

  // Assets/NewCss/TruckScripts/Truck.cs
  exitDelay: 5,                                      // satir 79 (default alan)

  // GDD.md:464 "Baslangicta 1 hangar aktif"; sahne (The Main Office.unity:37529-37537)
  // 3 hangar slotu var (requiredUpgradeLevel 0/1/2) ama 1/2 Truck upgrade ile acilir.
  // Upgrade fiyatlandirmasi bu turun KAPSAMI DISI -> ne zaman acildigi bilinmiyor.
  // Bu sim VARSAYILAN olarak 16 gun boyunca SABIT 1 hangar kullanir (muhafazakar/
  // dogrulanabilir taban). 2-hangar duyarlilik karsilastirmasi ayrica calistirilir.
  numHangarsDefault: 1,
};

// Hangar devir-arasi bosluk: exitDelay(5) + ortalama respawnDelay(4) = 9s KOD-DOGRULANMIS.
// +6s "animasyon tamponu" (giris/cikis animator klip suresi) KOD'DA YOK, ASSUMED -
// raporda ayrica duyarlilik olarak isaretlenir (bkz ANIM_BUFFER_ASSUMED).
const OVERHEAD_CODE_CONFIRMED = ECONOMY.exitDelay + ECONOMY.respawnDelayRangeAvg; // 9
const ANIM_BUFFER_ASSUMED = 6; // VARSAYIM, kodda yok
const OVERHEAD_TOTAL = OVERHEAD_CODE_CONFIRMED + ANIM_BUFFER_ASSUMED; // 15

const CARGO_VALUES = [2, 3, 4, 5]; // TruckSpawner Random.Range(2,6) exclusive-upper -> uniform bu 4 deger

// ============================================================================
// QUEST VERILERI (Assets/Resources/Quests/easy{1,2,3}.asset, 2026-07-18)
// ============================================================================
// QuestData.cs: MAX_SELECTED_REWARDS = MAX_SELECTED_PENALTIES = 2 (havuzdan
// Fisher-Yates ile rastgele 2 secilir, tekrarsiz). QuestManager.cs:
// DAILY_QUEST_COUNT=3 gosterilir ama AcceptQuestInternal GUNDE SADECE 1 KABUL
// izni verir (satir 591-597, "Gunde sadece 1 gorev kabul edilebilir kontrolu").
const MAX_SELECTED = 2;

function pool(items) { return items; } // [{type:'Money'|'Prestige', amount}]

const QUESTS_RAW = {
  easy1: { // "Secici Paket Canavari" - PackToy, 5 kirmizi kutu paketle (asset easy1, prestij x0.4 rescale)
    rewardPool: pool([{ type: 'Money', amount: 10 }, { type: 'Money', amount: 20 }, { type: 'Money', amount: 15 }, { type: 'Prestige', amount: 0.4 }, { type: 'Prestige', amount: 0.8 }]),
    penaltyPool: pool([{ type: 'Money', amount: -10 }, { type: 'Money', amount: -15 }, { type: 'Money', amount: -5 }, { type: 'Prestige', amount: -0.8 }, { type: 'Prestige', amount: -0.4 }]),
  },
  easy2: { // "Yetistirici" - CompleteTruck, 2 tir tamamla (asset easy2, prestij x0.4 rescale)
    rewardPool: pool([{ type: 'Money', amount: 20 }, { type: 'Money', amount: 16 }, { type: 'Money', amount: 8 }, { type: 'Prestige', amount: 0.4 }, { type: 'Prestige', amount: 0.8 }]),
    penaltyPool: pool([{ type: 'Money', amount: -6 }, { type: 'Money', amount: -12 }, { type: 'Money', amount: -18 }, { type: 'Prestige', amount: -0.4 }, { type: 'Prestige', amount: -0.8 }]),
  },
  easy3_old: { // "Depo Takipcisi" - PlaceBoxOnShelf, rafa 5 kutu koy (MEVCUT/ESKI - orantisiz)
    rewardPool: pool([{ type: 'Money', amount: 100 }, { type: 'Money', amount: 150 }, { type: 'Money', amount: 200 }, { type: 'Prestige', amount: 1 }, { type: 'Prestige', amount: 2 }]),
    penaltyPool: pool([{ type: 'Money', amount: -20 }, { type: 'Money', amount: -10 }, { type: 'Prestige', amount: -1 }, { type: 'Money', amount: -5 }]), // NOT: 4 ogeli (asimetrik), 1 prestij cezasi
  },
  easy3_new: { // UYGULANDI (asset easy3, "Depo Takipcisi" rafa 5 kutu; prestij x0.4 rescale)
    rewardPool: pool([{ type: 'Money', amount: 15 }, { type: 'Money', amount: 25 }, { type: 'Money', amount: 35 }, { type: 'Prestige', amount: 0.4 }, { type: 'Prestige', amount: 0.8 }]),
    penaltyPool: pool([{ type: 'Money', amount: -15 }, { type: 'Money', amount: -20 }, { type: 'Money', amount: -10 }, { type: 'Prestige', amount: -0.4 }, { type: 'Prestige', amount: -0.8 }]),
  },
};

function poolEV(items) {
  const n = items.length;
  const k = Math.min(MAX_SELECTED, n);
  const p = n > 0 ? k / n : 0; // her ogenin secilme olasiligi (uniform k-alt-kume)
  const money = items.filter(x => x.type === 'Money').reduce((a, x) => a + x.amount, 0);
  const prestige = items.filter(x => x.type === 'Prestige').reduce((a, x) => a + x.amount, 0);
  return { moneyEV: p * money, prestigeEV: p * prestige, pickProb: p, n };
}

const QUESTS = {};
for (const key of Object.keys(QUESTS_RAW)) {
  const r = QUESTS_RAW[key].rewardPool.length ? poolEV(QUESTS_RAW[key].rewardPool) : { moneyEV: 0, prestigeEV: 0 };
  const p = QUESTS_RAW[key].penaltyPool.length ? poolEV(QUESTS_RAW[key].penaltyPool) : { moneyEV: 0, prestigeEV: 0 };
  QUESTS[key] = { rewardMoneyEV: r.moneyEV, rewardPrestigeEV: r.prestigeEV, penaltyMoneyEV: p.moneyEV, penaltyPrestigeEV: p.prestigeEV, rewardPickProb: r.pickProb, penaltyPickProb: p.pickProb };
}

// Gun-basi kabul edilen görevin TAMAMLANMA ORANI varsayimlari (MUHAFAZAKAR,
// PLAYTEST-YOK - acikca varsayim). Spec sirasi: easy1 YUKSEK, easy3 ORTA-YUKSEK,
// easy2 ORTA (dinamik hesaplanir, asagida).
const QUEST_COMPLETION = {
  easy1: { Normal: 0.80, Slow: 0.56, rangeLow: 0.65, rangeHigh: 0.90 }, // "zaten yapilan is" ama renk-spesifik surtunme
  easy3: { Normal: 0.65, Slow: 0.46, rangeLow: 0.50, rangeHigh: 0.78 }, // renk-serbest ama easy1'den az yuksek (spec sirasi)
};

function easy2CompletionRate(fullTrucksPerDay) {
  const target = 2; // easy2 targetCount
  if (fullTrucksPerDay >= target * 1.5) return 0.85;
  if (fullTrucksPerDay >= target) return 0.65;
  if (fullTrucksPerDay >= target * 0.5) return 0.40;
  if (fullTrucksPerDay >= target * 0.25) return 0.20;
  return 0.08;
}

/**
 * Gunluk quest geliri: oyuncu her gun ZORUNLU 1 kabul etmez, sunulan (en fazla
 * 3, QuestManager.cs DAILY_QUEST_COUNT) secenekler arasindan RASYONEL olarak
 * EN IYI para-EV'li olani secer (easy1/2/3'un ortalamasini almak, "oyuncu her
 * gun rastgele/zorla en kotu secenegi de kabul eder" gibi gercekci-olmayan bir
 * kotumserlik yaratir - bir onceki taslakta boyleydi, DUZELTILDI). Secilen en
 * iyi secenegin EV'si dahi NEGATIFSE (cok dusuk tamamlanma ihtimali - orn.
 * struggling 1P Yavas takim icin easy2), oyuncu HICBIRINI kabul etmez -> 0/0.
 * Bu, QuestManager.AcceptQuestInternal'in "kabul opsiyonel" dogasini yansitir.
 */
function questDailyEV(scenarioLabel, fullTrucksPerDay, questSetKey) {
  const e1 = QUESTS.easy1, e2 = QUESTS.easy2, e3 = QUESTS[questSetKey === 'old' ? 'easy3_old' : 'easy3_new'];
  const c1 = QUEST_COMPLETION.easy1[scenarioLabel];
  const c3 = QUEST_COMPLETION.easy3[scenarioLabel];
  const c2 = easy2CompletionRate(fullTrucksPerDay);

  function evFor(q, c) {
    return { money: c * q.rewardMoneyEV + (1 - c) * q.penaltyMoneyEV, prestige: c * q.rewardPrestigeEV + (1 - c) * q.penaltyPrestigeEV };
  }
  const candidates = [evFor(e1, c1), evFor(e2, c2), evFor(e3, c3)];
  const best = candidates.reduce((a, b) => (b.money > a.money ? b : a));
  const accept = best.money > 0;
  return {
    money: accept ? best.money : 0,
    prestige: accept ? best.prestige : 0,
    accepted: accept,
    completion: { easy1: c1, easy2: c2, easy3: c3 },
  };
}

// ============================================================================
// GUN SURESI / TIR PENCERESI YARDIMCILARI
// ============================================================================
function dayDuration(day) {
  return day <= ECONOMY.dynamicDurationStartDay
    ? ECONOMY.realDurationInSeconds
    : ECONOMY.realDurationInSeconds + (day - ECONOMY.dynamicDurationStartDay) * ECONOMY.dailyDurationIncrease;
}

function truckWindowSeconds(day) {
  const d = dayDuration(day);
  return d * (ECONOMY.truckEndHour - ECONOMY.truckStartHour) / (ECONOMY.dayEndHour - ECONOMY.dayStartHour);
}

const CARGO_AVG = CARGO_VALUES.reduce((a, b) => a + b, 0) / CARGO_VALUES.length; // 3.5

/**
 * STRICT model: her tir SADECE KENDI 30sn penceresinde uretilebilen kutuyu alir
 * (stockpile/on-uretim yok). Alt sinir (kotumser).
 */
function hangarStayFor(playerCount) {
  const arr = ECONOMY.hangarStayDurationByPlayerCount;
  if (!arr || arr.length === 0) return ECONOMY.hangarStayDuration;
  return arr[Math.min(Math.max(playerCount - 1, 0), arr.length - 1)];
}

function truckCapStrict(playerCount, boxesPerMin, day, numHangars) {
  const rate = (boxesPerMin * playerCount) / 60; // kutu/sn, takim toplami
  const tws = truckWindowSeconds(day);
  const hangarStay = hangarStayFor(playerCount);
  let totalDeliverable = 0, totalCycleTime = 0;
  for (const cargo of CARGO_VALUES) {
    const fillTime = rate > 0 ? cargo / rate : Infinity;
    totalDeliverable += Math.min(cargo, rate * hangarStay);
    totalCycleTime += Math.min(hangarStay, fillTime) + OVERHEAD_TOTAL;
  }
  const avgDeliverable = totalDeliverable / CARGO_VALUES.length;
  const avgCycleTime = totalCycleTime / CARGO_VALUES.length;
  const cyclesPerDay = tws / avgCycleTime;
  return cyclesPerDay * avgDeliverable * numHangars;
}

/**
 * OPTIMISTIC model: takim tir saatleri boyunca (9 oyun-saati) SUREKLI uretip
 * rafa/istasyona ON-STOK yapabilir (easy3'un "rafa kutu koy" gorevi ve
 * ShelfState'in ayri bir "activeInteractable" olmasi bu varsayimi destekler -
 * bkz rapor). Tir sadece devir-basina en fazla requiredCargo KABUL EDER, ama
 * doldurma suresi artik o tirin 30sn'lik penceresine hapsedilmez. PRIMARY
 * model (asagidaki 8-senaryo kosusu bunu kullanir); strict'e gore ust sinir.
 */
function truckCapOptimistic(playerCount, boxesPerMin, day, numHangars) {
  const rate = (boxesPerMin * playerCount) / 60;
  const tws = truckWindowSeconds(day);
  const productionCap = rate * tws;
  const cyclesPerDayMax = tws / OVERHEAD_TOTAL; // devir suresi sadece overhead (stok hazir, dolum aninda)
  const truckAcceptCap = cyclesPerDayMax * CARGO_AVG * numHangars;
  return Math.min(productionCap, truckAcceptCap);
}

/** Gunde kac tir TAM dolduruluyor (easy2 "2 tir tamamla" tamamlanma tahmini icin). */
function fullTrucksPerDayEstimate(playerCount, boxesPerMin, day, numHangars) {
  const rate = (boxesPerMin * playerCount) / 60;
  const tws = truckWindowSeconds(day);
  const hangarStay = hangarStayFor(playerCount);
  let totalCycleTime = 0, fullCount = 0;
  for (const cargo of CARGO_VALUES) {
    const fillTime = rate > 0 ? cargo / rate : Infinity;
    totalCycleTime += Math.min(hangarStay, fillTime) + OVERHEAD_TOTAL;
    if (fillTime <= hangarStay) fullCount++;
  }
  const avgCycleTime = totalCycleTime / CARGO_VALUES.length;
  const cyclesPerDay = tws / avgCycleTime;
  const fracFull = fullCount / CARGO_VALUES.length;
  return cyclesPerDay * fracFull * numHangars;
}

function customerDemandToday(playerCount, activeInteractables, storeLevel) {
  const capacityBase = activeInteractables * ECONOMY.shelfMultiplier + storeLevel * ECONOMY.levelMultiplier;
  const randomVariance = (ECONOMY.minVariance + ECONOMY.maxVariance) / 2; // -2..3 ortalamasi = 0.5
  const playerCountMultiplier = 1 + (playerCount - 1) * ECONOMY.playerCountMultiplierCoeff;
  const raw = (capacityBase + randomVariance) * playerCountMultiplier;
  return Math.min(ECONOMY.maxCustomersPerDay, Math.max(ECONOMY.minCustomersPerDay, Math.round(raw)));
}

// ============================================================================
// ANA SIMULASYON
// ============================================================================
function runSim(playerCount, opts) {
  const {
    boxesPerMinPerPlayer = 2.0,
    wrongDeliveryRate = 0.2,      // yanlis renk teslimat (penaltyPerBox + wrongDeliveryPrestigePenalty)
    physicalDropRate = 0.05,      // fiziksel dusme/carpma (boxDropMoneyPenalty + boxDropPrestigePenalty)
    customerLossRate = 0.03,
    upgradeSpendRatio = 0.5,
    numHangars = ECONOMY.numHangarsDefault,
    truckCapMode = 'optimistic',
    questIncomeEnabled = false,
    questSet = 'new',
    scenarioLabel = 'Normal',
    label = '',
  } = opts;

  const E = ECONOMY;
  const baseRent = E.baseRentByPlayerCount[playerCount - 1];
  let cash = E.baseStartingMoney * Math.pow(E.moneyMultiplierPerPlayer, playerCount - 1); // = 500 (multiplier=1.0)
  let prestige = E.startingPrestige;
  let totalUpgradeValue = 0;
  let rentCycle = 0;
  let graceUsed = false;
  let activeInteractables = 3;
  let storeLevel = 1;
  let bankrupt = false, bankruptDay = null, bankruptReason = null;
  let prestigeCapHitDay = null;
  const rows = [];

  const capFn = truckCapMode === 'strict' ? truckCapStrict : truckCapOptimistic;

  for (let day = 1; day <= E.maxDays; day++) {
    const duration = dayDuration(day);

    const customers = customerDemandToday(playerCount, activeInteractables, storeLevel);
    const customersLost = Math.floor(customers * customerLossRate);
    const demandAdjusted = customers - customersLost;

    const truckCapped = capFn(playerCount, boxesPerMinPerPlayer, day, numHangars);
    const fullTrucksPerDay = fullTrucksPerDayEstimate(playerCount, boxesPerMinPerPlayer, day, numHangars);

    const deliveriesAttempted = Math.max(0, Math.min(demandAdjusted, truckCapped));
    const wrongDeliveries = Math.floor(deliveriesAttempted * wrongDeliveryRate);
    const correctDeliveries = deliveriesAttempted - wrongDeliveries;
    const physicalDrops = Math.floor(deliveriesAttempted * physicalDropRate);

    const prestigeTier = Math.floor(prestige / E.prestigePerBonus);
    const rewardActual = E.rewardPerBox + prestigeTier * E.bonusPerTier;

    const revenue = correctDeliveries * rewardActual;
    const wrongCost = wrongDeliveries * E.penaltyPerBox;
    const dropCost = physicalDrops * E.boxDropMoneyPenalty;
    const netEarnings = revenue - wrongCost - dropCost;

    let questMoney = 0, questPrestige = 0, questInfo = null;
    if (questIncomeEnabled) {
      const q = questDailyEV(scenarioLabel, fullTrucksPerDay, questSet);
      questMoney = q.money;
      questPrestige = q.prestige;
      questInfo = q;
    }

    let cashBeforeRent = cash + netEarnings + questMoney;

    prestige += demandAdjusted * E.customerServedPrestigeBonus;
    prestige += wrongDeliveries * E.wrongDeliveryPrestigePenalty;
    prestige += physicalDrops * E.boxDropPrestigePenalty;
    prestige += customersLost * E.customerLostPrestigePenalty;
    prestige += questPrestige;
    if (prestige >= E.maxPrestige && prestigeCapHitDay === null) prestigeCapHitDay = day;
    prestige = Math.max(0, Math.min(E.maxPrestige, prestige));

    let rentAmount = 0, rentPaid = 0, event = '';
    const isRentDay = day % E.rentIntervalDays === 0;

    if (isRentDay && !bankrupt) {
      rentAmount = Math.round(baseRent * Math.pow(E.rentGrowthMultiplier, rentCycle) * E.rentScaledMultiplier);
      if (cashBeforeRent >= rentAmount) {
        rentPaid = rentAmount;
        cash = cashBeforeRent - rentPaid;
        rentCycle++;
      } else if (!graceUsed) {
        rentPaid = Math.round(cashBeforeRent * E.gracePaymentPercent);
        cash = cashBeforeRent - rentPaid;
        graceUsed = true;
        rentCycle++;
        event = 'GRACE_PERIOD';
      } else {
        bankrupt = true; bankruptDay = day; bankruptReason = 'RENT'; event = 'BANKRUPT_RENT';
        cash = cashBeforeRent;
      }
    } else {
      cash = cashBeforeRent;
    }

    if (!bankrupt && !isRentDay && cash > 200) {
      const surplus = cash - 200;
      const spend = surplus * upgradeSpendRatio;
      cash -= spend;
      totalUpgradeValue += spend;
      activeInteractables = 3 + Math.floor(totalUpgradeValue / 300);
      storeLevel = 1 + Math.floor(totalUpgradeValue / 600);
    }

    rows.push({
      day, customers, demandAdjusted, truckCapped: +truckCapped.toFixed(1), deliveriesAttempted: +deliveriesAttempted.toFixed(1),
      correctDeliveries: +correctDeliveries.toFixed(1), wrongDeliveries, physicalDrops, customersLost,
      questMoney: +questMoney.toFixed(1), questPrestige: +questPrestige.toFixed(2),
      netEarnings: +netEarnings.toFixed(0), rentAmount, cash: +cash.toFixed(0), prestige: +prestige.toFixed(2), event,
    });

    if (!bankrupt && prestige <= 0) { bankrupt = true; bankruptDay = day; bankruptReason = 'PRESTIGE'; }
    if (bankrupt) break;
  }

  return { playerCount, label, bankrupt, bankruptDay, bankruptReason, prestigeCapHitDay, rows, finalCash: rows[rows.length - 1]?.cash, finalPrestige: rows[rows.length - 1]?.prestige };
}

module.exports = {
  ECONOMY, QUESTS, QUEST_COMPLETION, runSim, dayDuration, truckWindowSeconds,
  truckCapStrict, truckCapOptimistic, fullTrucksPerDayEstimate, customerDemandToday,
  questDailyEV, easy2CompletionRate, OVERHEAD_TOTAL, OVERHEAD_CODE_CONFIRMED, CARGO_AVG,
};

// ============================================================================
// CLI CIKTISI (node tools/economy-sim/sim.js dogrudan calistirilirsa)
// ============================================================================
if (require.main === module) {
  console.log('================================================================');
  console.log(' QUEST EV TABLOSU (havuzdan hesaplanan, kafadan degil)');
  console.log('================================================================');
  console.table(Object.fromEntries(Object.entries(QUESTS).map(([k, v]) => [k, {
    rewardMoneyEV: +v.rewardMoneyEV.toFixed(1), rewardPrestigeEV: +v.rewardPrestigeEV.toFixed(2),
    penaltyMoneyEV: +v.penaltyMoneyEV.toFixed(1), penaltyPrestigeEV: +v.penaltyPrestigeEV.toFixed(2),
  }])));

  console.log('\n================================================================');
  console.log(' 8 SENARYO: 1P-4P x Normal/Yavas, QUEST KAPALI');
  console.log('================================================================');
  const scenarios = [];
  for (const p of [1, 2, 3, 4]) {
    scenarios.push(runSim(p, { label: 'Normal', scenarioLabel: 'Normal', boxesPerMinPerPlayer: 2.0, wrongDeliveryRate: 0.2, physicalDropRate: 0.05, customerLossRate: 0.03, questIncomeEnabled: false }));
  }
  for (const p of [1, 2, 3, 4]) {
    scenarios.push(runSim(p, { label: 'Yavas', scenarioLabel: 'Slow', boxesPerMinPerPlayer: 1.2, wrongDeliveryRate: 0.3, physicalDropRate: 0.08, customerLossRate: 0.08, questIncomeEnabled: false }));
  }
  console.table(scenarios.map(s => ({ P: s.playerCount, senaryo: s.label, iflas: s.bankrupt ? `GUN ${s.bankruptDay} (${s.bankruptReason})` : 'yok', sonKasa: s.finalCash, sonPrestij: s.finalPrestige, prestijTavanGunu: s.prestigeCapHitDay ?? '-' })));

  console.log('\n================================================================');
  console.log(' 8 SENARYO: QUEST ACIK (easy3 YENI degerlerle)');
  console.log('================================================================');
  const scenariosQ = [];
  for (const p of [1, 2, 3, 4]) {
    scenariosQ.push(runSim(p, { label: 'Normal+Quest', scenarioLabel: 'Normal', boxesPerMinPerPlayer: 2.0, wrongDeliveryRate: 0.2, physicalDropRate: 0.05, customerLossRate: 0.03, questIncomeEnabled: true, questSet: 'new' }));
  }
  for (const p of [1, 2, 3, 4]) {
    scenariosQ.push(runSim(p, { label: 'Yavas+Quest', scenarioLabel: 'Slow', boxesPerMinPerPlayer: 1.2, wrongDeliveryRate: 0.3, physicalDropRate: 0.08, customerLossRate: 0.08, questIncomeEnabled: true, questSet: 'new' }));
  }
  console.table(scenariosQ.map(s => ({ P: s.playerCount, senaryo: s.label, iflas: s.bankrupt ? `GUN ${s.bankruptDay} (${s.bankruptReason})` : 'yok', sonKasa: s.finalCash, sonPrestij: s.finalPrestige, prestijTavanGunu: s.prestigeCapHitDay ?? '-' })));

  console.log('\n================================================================');
  console.log(' QUEST GELIR DUYARLILIGI: eski easy3 (100/150/200) ile ayni kosu');
  console.log('================================================================');
  const scenariosQOld = [];
  for (const p of [1, 4]) {
    scenariosQOld.push(runSim(p, { label: 'Normal+QuestESKI', scenarioLabel: 'Normal', boxesPerMinPerPlayer: 2.0, wrongDeliveryRate: 0.2, physicalDropRate: 0.05, customerLossRate: 0.03, questIncomeEnabled: true, questSet: 'old' }));
  }
  console.table(scenariosQOld.map(s => ({ P: s.playerCount, senaryo: s.label, iflas: s.bankrupt ? `GUN ${s.bankruptDay}` : 'yok', sonKasa: s.finalCash, sonPrestij: s.finalPrestige })));

  console.log('\n================================================================');
  console.log(' TIR-PENCERESI DUYARLILIGI: strict vs optimistic, 1 vs 2 hangar (gun 8 ornegi)');
  console.log('================================================================');
  const day = 8;
  const sensRows = [];
  for (const p of [1, 2, 3, 4]) {
    for (const [lbl, rate] of [['Normal', 2.0], ['Yavas', 1.2]]) {
      sensRows.push({
        P: p, senaryo: lbl,
        strict_1h: +truckCapStrict(p, rate, day, 1).toFixed(1),
        optimistic_1h: +truckCapOptimistic(p, rate, day, 1).toFixed(1),
        optimistic_2h: +truckCapOptimistic(p, rate, day, 2).toFixed(1),
        demand: customerDemandToday(p, 3, 1),
      });
    }
  }
  console.table(sensRows);

  console.log('\n================================================================');
  console.log(' EASY2 TAMAMLANMA TAHMINI (fullTrucksPerDay -> completion%), gun 8');
  console.log('================================================================');
  const easy2Rows = [];
  for (const p of [1, 2, 3, 4]) {
    for (const [lbl, rate] of [['Normal', 2.0], ['Yavas', 1.2]]) {
      const ftpd = fullTrucksPerDayEstimate(p, rate, day, 1);
      easy2Rows.push({ P: p, senaryo: lbl, fullTrucksPerDay: +ftpd.toFixed(2), completionRate: easy2CompletionRate(ftpd) });
    }
  }
  console.table(easy2Rows);

  console.log('\nOverhead (devir-arasi bosluk): kod-dogrulanmis =', OVERHEAD_CODE_CONFIRMED, 's, +varsayilan anim tamponu =', OVERHEAD_TOTAL - OVERHEAD_CODE_CONFIRMED, 's, toplam =', OVERHEAD_TOTAL, 's');

  console.log('\n================================================================');
  console.log(' KIRA BASKISI: gun 16 kira + kira-sonrasi kasa (Normal, quest KAPALI)');
  console.log('================================================================');
  const rentRows = [];
  for (const p of [1, 2, 3, 4]) {
    const s = runSim(p, { label: 'Normal', scenarioLabel: 'Normal', boxesPerMinPerPlayer: 2.0, wrongDeliveryRate: 0.2, physicalDropRate: 0.05, customerLossRate: 0.03, questIncomeEnabled: false });
    const day16 = s.rows.find(r => r.day === 16);
    rentRows.push({ P: p, gun16Kira: day16?.rentAmount, gun16KasaKiraSonrasi: day16?.cash, oran_kasa_kira: day16 ? +(day16.cash / day16.rentAmount).toFixed(2) : null, kriter_3x_altinda: day16 ? day16.cash <= 3 * day16.rentAmount : null });
  }
  console.table(rentRows);

  console.log('\n================================================================');
  console.log(' GUNLUK CEKIRDEK GELIR (netEarnings, quest HARIC) - quest EV karsilastirmasi icin referans');
  console.log('================================================================');
  const coreIncomeRows = [];
  for (const p of [1, 2, 3, 4]) {
    for (const [lbl, rate, wrongRate, dropRate, lossRate] of [['Normal', 2.0, 0.2, 0.05, 0.03], ['Yavas', 1.2, 0.3, 0.08, 0.08]]) {
      const s = runSim(p, { label: lbl, scenarioLabel: lbl === 'Normal' ? 'Normal' : 'Slow', boxesPerMinPerPlayer: rate, wrongDeliveryRate: wrongRate, physicalDropRate: dropRate, customerLossRate: lossRate, questIncomeEnabled: false });
      const d1 = s.rows.find(r => r.day === 1);
      const d8 = s.rows.find(r => r.day === 8);
      const d16 = s.rows.find(r => r.day === 16);
      coreIncomeRows.push({ P: p, senaryo: lbl, gun1: d1?.netEarnings, gun8: d8?.netEarnings ?? '(iflas)', gun16: d16?.netEarnings ?? '(iflas)' });
    }
  }
  console.table(coreIncomeRows);
  console.log('easy1 EV=18 TL, easy2 EV=17.6 TL, easy3_new EV=30 TL, easy3_new maks tek-cekim=25+35=60 TL (karsilastir).');
}
