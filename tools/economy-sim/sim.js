// ============================================================================
// Cargor 16-gun ekonomi simulasyonu -- v5.1  (TEK MODEL)
// ============================================================================
// Calistirma: node tools/economy-sim/sim.js
//
// !! BU DOSYADA TEK BIR OYUN GERCEKLIGI VARDIR: `SRC4` + `runFullSim`.
//
// 2026-08-31 TEMIZLIGI -- NEDEN: dosya bu tarihe kadar IKI ayri oyun modeli
// tasiyordu ve ikisi de CANLI KODDA ARTIK VAR OLMAYAN mekanikleri simule
// ediyordu. Yanlis blogun okunmasi sessizce hatali analiz uretiyordu:
//   * `const SRC` + `runSim`          = v3.1, PlateUp ONCESI model
//   * `const PLATEUP` + `runSimPlateUp` = 2026-08-29 oneri modeli
// Somut bayatliklar (2026-08-31'de kaynaktan dogrulandi):
//   - `SRC.baseRentByPlayerCount = [500,1000,1450,1800]` -> CANLI deger
//     [290,650,1140,1630] (GameEconomySettings.cs:21; asset:15 hex
//     22010000/8a020000/74040000/5e060000 = 290/650/1140/1630).
//   - `phoneRingChancePerHour` / `phoneRingEventMultiplier` / `phoneRingPerkBonus`
//     (V3 "calan telefonu ac" modeli) -> GameEconomySettings.cs'te DE
//     Assets/Resources/EkonomiAyarlari.asset'te DE ARTIK YOK; PhoneCallManager'da
//     `ringDuration` alani da yok. Telefon V4: oyuncu DISARI arar ve SkipTime
//     gunun GERCEK saniyelerini yakar.
//   - `shelfMultiplier` / `levelMultiplier` / `playerCountMultCoeff` (kapasite
//     tabanli musteri talebi) -> `CustomerManager.CountActiveInteractables`
//     SILINDI; kota artik gun egrisinden geliyor
//     (`GameEconomySettings.GetDailyCustomerCount(day, P)`, CustomerManager.cs:398).
// Bu iki blok ve YALNIZ onlara dayanan her sey SILINDI. Silinen semboller:
//   SRC, PLATEUP, runSim, runSimPlateUp, plateUpCeiling/Quota/ArrivalInterval/
//   TimeSkipMinutes/DayOutcome/BoxSupply, truckThroughput, customerDemand,
//   customerThroughput, phoneIncome, packingTablesForLevel, hangarStayFor,
//   dayDurationSec/secPerGameHour/truckWindowSec/customerWindowSec,
//   CARGO_VALUES/CARGO_AVG, PHONE_ROLLS_PER_DAY, GAME/TRUCK/CUSTOMER_GAME_HOURS,
//   ASSUMED.phoneAnswerRate, ASSUMED.startingActiveInteractables.
// Gecmis analizler icin: .claude/agent-memory/economist/ (Round 1-12 raporlari).
//
// ---------------------------------------------------------------------------
// HARITA -- hangi sembol neye ait (yeni analizde ONCE buraya bak)
// ---------------------------------------------------------------------------
//   SABITLER
//     SRC4            -- CANLI kod/asset/sahne degerleri, her satirda dosya:satir
//                        kaniti. Bir sonraki denetimde ONCE bu blogu tazele.
//     SRC4.reference  -- sim'in HIC OKUMADIGI, yalniz belge amacli canli degerler.
//     ASSUMED         -- oyuncu becerisi/hizi (kodda YOK). Model girdisi, kanit degil.
//     ASSUMED4        -- V4 telefon kullanim varsayimi (ASSUMED'in uzantisi).
//     QR / QUEST_ASSETS -- 30 canli quest asset'i (Assets/Resources/Quests/).
//   TUREVLER
//     OVERHEAD_CODE / OVERHEAD_TOTAL, WAVE_INTERVAL_FACTOR, quotaFor,
//     arrivalIntervalFor, rewardPerBoxFor, cargoValuesFor, timeSkipMinutesFor,
//     dayDurationSec4
//   MODEL PARCALARI
//     tableContentionEfficiency -- paketleme masasi cekismesi (M/M/c//P)
//     truckThroughputWindowed   -- tir/hangar devri + uretim tavani (pencere-bazli)
//     fullCustomerDay           -- bir gunun musteri akisi (kota/varis/servis/erken bitis)
//     questCompletionProb / buildQuestSlots / questDailyDecision -- gunluk quest karari
//   GIRIS NOKTASI
//     runFullSim(playerCount, opts)  <-- TEK kanonik simulasyon. BASKASI YOK.
//   CLI (node sim.js)
//     Blok NUMARALARI KORUNDU (18-23). 0-17 ve 21 silindi ama kalanlar yeniden
//     NUMARALANDIRILMADI: gecmis raporlar "blok 18-23" diye atif yapiyor.
//
// ---------------------------------------------------------------------------
// TEMEL DAVRANIS NOTLARI (kod-kanitli -- model kurulumunu aciklar)
// ---------------------------------------------------------------------------
//  1. PARA 0'IN ALTINA INMEZ: MoneySystem.ModifyMoney -> Mathf.Max(0, ...)
//     (MoneySystem.cs:91). Cezalar 0'da yutulur; IFLAS YALNIZ kira kapisinda olur.
//  2. MUSTERI SERVISI SERI: CustomerAI yalniz `manager.IsFirstInQueue(this)` iken
//     BeginService yapiyor -> ayni anda TEK musteri (SRC4.serviceStations = 1).
//  3. KUYRUK DOLUYKEN SPAWN ATLANIR ama musteri KAYBOLMAZ (ertelenir); gun sonunda
//     hic spawn olmamis kota musterisi ApplyMissedQuotaPenalty yer.
//  4. QUEST ODULU KIRADAN SONRA YATAR: SettleAcceptedQuestsForDayEnd bir SONRAKI
//     gunun basinda kosuyor -> gun 4'te kabul edilen quest gun 4 kirasini ODEYEMEZ.
//     Sim bunu `questSettlePending` ile modelliyor.
//  5. .asset ile .cs default'u catistiginda ASSET/SAHNE kazanir. ANAHTAR asset'te
//     HIC YOKSA C# field-initializer degeri CANLIDIR (bkz. "Unity YAML float[]
//     tuzagi" notu -- anahtari hic yazmamak DOGRU yontem).
// ============================================================================

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

  // Hata oranlari.
  wrongDeliveryRate: { Normal: 0.12, Slow: 0.22, Fast: 0.07 }, // tira yanlis RENK kutu
  physicalDropRate:  { Normal: 0.05, Slow: 0.09, Fast: 0.03 }, // kutu yere dusme

  // Quest tamamlama: hedefe/kapasiteye orana gore turetilir (asagida), ama
  // "hedefi karsilayabiliyor olsa bile oyuncu unutur/vazgecer" surtunmesi:
  questExecutionFriction: { strict: 0.75, optimistic: 0.92 },

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
// MASA CEKISMESI (v3.1 YENI) -- sonlu-kaynak kuyruk / machine-repairman
// ============================================================================
/**
 * Oyuncular paketleme masalarini PAYLASIYOR. Sahnede seviye 0'da YALNIZ 1 masa
 * aktif (SRC4.packingTablesAtLevel0) ve `Table` TEK item tasiyor (Table.cs:57)
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
// BU MODELIN KARSILADIGI 5 GERCEK. Selef modeller (2026-08-31'de SILINEN runSim
// ve runSimPlateUp) bunlarin HICBIRINI karsilamiyordu:
//   1. MUSTERI SAYISI kapasite formulunden DEGIL gun egrisinden geliyor:
//      CustomerManager.CalculateTodaysCustomerCount artik YALNIZCA
//      GameEconomySettings.GetDailyCustomerCount(day, P) okuyor (cs:398).
//   2. TELEFON V4: "calan telefonu cevapla" degil "DISARI ARA". Para verirken
//      SkipTime ile gunun GERCEK saniyelerini YAKIYOR (PhoneCallManager V4).
//   3. GUN ERKEN BITIYOR: kota tukenip kuyruk bosalinca
//      CustomerManager.CheckEarlyDayCompletion -> DayCycleManager.FastForwardToEndOfDay.
//   4. "1 musteri = 1 urun" YANLIS: gun 5+ musterilerin %25'i IADE modunda
//      (SIFIR urun), gun 9+ tedarik musterileri IKI urun birakiyor
//      (PostRentFeatureUnlocks + CustomerAI.PlaceProductCoroutine).
//   5. KARGO ARALIGI P-bazli (TruckSpawner.cs:613,624 -> GetTruckCargoRange),
//      sabit {2,3,4,5} degil.
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
//   ! 2026-08-31 DUZELTME: bu satirlar eskiden "olu asset anahtarlari" olarak
//     phoneRingChancePerHour / phoneRingEventMultiplier / phoneRingPerkBonus'u
//     listeliyordu. O anahtarlar EkonomiAyarlari.asset'ten DE silinmis durumda
//     (grep: sifir eslesme). V3 telefon modelinden geriye HICBIR SEY kalmadi.
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
  dailyQuestCount: 3,             // QuestManager.cs:25 BASE_DAILY_QUEST_COUNT (gunluk
                                  // TEKLIF sayisi; kabul gunde EN FAZLA 1 --
                                  // HasAcceptedQuestToday cs:971).
                                  // 2026-08-31 RESYNC: cs:150 DailyQuestTargetCount
                                  // artik DUZ 3; Round 10 U6'nin "3 + CurrentQuestTier"
                                  // hali kodda YOK. Deger ayni, gerekce degisti.
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

  // ---- REFERANS-ONLY -- SIM BU ALANLARI OKUMAZ ----------------------------
  // Silinen `SRC` blogundan tasinan, HALA CANLI olan belge degerleri. Modelde
  // kullanilmazlar; amac gelecek turun bunlari sifirdan aramak zorunda
  // kalmamasi. Analize TEMEL yapmadan once kaynagi tazele.
  reference: {
    // Musteri sabri -- ithappy/Creative_Characters_FREE/Saved_Characters/
    // Customer.prefab:2323-2325 (CustomerAI default'larini EZIYOR). 2026-08-31 OK.
    customerMinWaitTime: 15, customerMaxWaitTime: 20, customerInteractionTime: 2,
    // Olay takvimi -- Events/EventCalendarUI.cs:23,24,25. 2026-08-31 OK.
    eventFreeDays: 3, eventIntervalMin: 1, eventIntervalMax: 2,
    eventPoolSize: 16,            // cs:160-177 _allEvents (2026-07-30 sayimi)
    // Upgrade maliyet olcegi -- GameState/DifficultyManager.cs:73 (P-bazli DIZI,
    // prefab override YOK). Taban kosuda upgrade harcamasi yok. 2026-08-31 OK.
    upgradeCostMultiplierByPlayerCount: [1.00, 2.00, 2.95, 3.70],
    // FESTIVAL DAY -- Events/EventEffectManager.cs:409 CANLI davranis kiranin
    // %10-20'si; min/max YALNIZ DayCycleManager yoksa fallback (cs:414-415). 2026-08-31 OK.
    festivalRentSharePct: [0.10, 0.20], festivalBonusMin: 100, festivalBonusMax: 300,
    // Quest kapilari -- QuestManager.cs:971 gunde EN FAZLA 1 kabul; cs:78 tier
    // NetworkVariable<int>(0) -> oyun Easy ile basliyor. 2026-08-31 OK.
    dailyQuestAcceptLimit: 1, questTierStart: 0,
    // Para tabani -- UIScripts/MoneySystem.cs Mathf.Max(0, ...) -> kasa eksiye inmez.
    moneyFloorZero: true,
    // Sahne topolojisi (2026-07-30 sayimi -- 2026-08-31'de YENIDEN DOGRULANMADI).
    hangarCount: 3,               // TruckSpawner requiredUpgradeLevel 0/1/2
    packingTableTotal: 2,         // "Paketleme Istasyonu" levelObjects (tavan 2 masa)
  },
};

// ---------------------------------------------------------------------------
// v4 VARSAYIMLARI (kodda YOK -- oyuncu davranisi). ASSUMED'in uzantisi.
// ---------------------------------------------------------------------------
const ASSUMED4 = {
  // Kota musterilerinin ne kadari TELEFONLA one cekiliyor.
  // SILINEN `ASSUMED.phoneAnswerRate` BURADA KULLANILAMAZDI: o "calan telefonu acma
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

// ============================================================================
// TUREVLER (SRC4 + ASSUMED)
// ============================================================================
// Tir devir-arasi olu sure. KOD kismi: exitDelay (Truck.prefab:196) + ortalama
// respawnDelay (sahne [3,5] -> 4) = 9 sn. Animator giris/cikis klibi kodda
// sayisallastirilmadigi icin ustune VARSAYIM tampon eklenir -> 15 sn.
// NOT (2026-08-31): eskiden silinen `SRC` blogundan turetiliyordu; SRC4'teki
// exitDelay/respawnDelayRange AYNI degerleri tasidigi icin sonuc DEGISMEDI.
const OVERHEAD_CODE  = SRC4.exitDelay + (SRC4.respawnDelayRange[0] + SRC4.respawnDelayRange[1]) / 2; // 9
const OVERHEAD_TOTAL = OVERHEAD_CODE + ASSUMED.animBufferSeconds;                                    // 15

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

// ============================================================================
// EXPORTS -- TEK MODEL (v5.1). Buradaki her sembol SRC4/runFullSim dunyasina ait.
// 2026-08-31'de KALDIRILAN exportlar (disaridan cagiran script varsa BILINCLI):
//   SRC, PLATEUP, runSim, runSimPlateUp, plateUp*, truckThroughput,
//   customerDemand, customerThroughput, phoneIncome, packingTablesForLevel,
//   hangarStayFor, dayDurationSec, secPerGameHour, truckWindowSec,
//   customerWindowSec, CARGO_VALUES, CARGO_AVG, PHONE_ROLLS_PER_DAY.
// ============================================================================
module.exports = {
  // --- sabitler
  SRC4, ASSUMED, ASSUMED4, QR, QUEST_ASSETS,
  // --- turevler
  OVERHEAD_CODE, OVERHEAD_TOTAL, WAVE_INTERVAL_FACTOR, waveIntervalFactor,
  quotaFor, arrivalIntervalFor, rewardPerBoxFor, cargoValuesFor,
  timeSkipMinutesFor, dayDurationSec4,
  // --- model parcalari
  tableContentionEfficiency, questCompletionProb, buildQuestSlots,
  questDailyDecision, truckThroughputWindowed, fullCustomerDay,
  // --- TEK kanonik giris noktasi
  runFullSim,
};

// ============================================================================
// CLI
// ============================================================================
if (require.main === module) {
  const B = (s) => `\n${'='.repeat(78)}\n ${s}\n${'='.repeat(78)}`;

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
