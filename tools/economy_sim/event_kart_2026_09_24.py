#!/usr/bin/env python3
"""
2026-09-24 — Her-gün event + 6 kapalı kartın yeniden tasarımı: TASARIM ölçümü.
Plan: plans/her-gun-event-ve-kart-yenileme.md · Rapor: docs/economy/her-gun-event-ve-kart-tasarimi-2026-09-24.md

sim.py'yi import eder, DEĞİŞTİRMEZ; config.json'a YAZMAZ (tüm varyantlar bellekte).
EKRun, sim.Run.simulate_day/shop'un birebir kopyası + kancalar. Tüm yeni özellikler kapalıyken
sim.Run ile bit-birebir aynı sonucu verdiği `selftest` alt komutuyla doğrulanır.

Varyantlar:
  base      canlı eşdeğeri: config.json + events.freeDays=4 (canlı EventCalendarUI takvimi, bkz. sim-sync-gotchas)
  ev        YENİ KATALOG + bant kuralı + kira-dışı her gün (12 gün) + tekrar yok; kartlar ESKİ (6 kapalı)
  kart      ESKİ takvim (base) + 6 yeni kart açık
  paket     ev + kart (önerilen paket)
  paket_hard / paket_soft  hassasiyet: negatif event'ler ×1.5 / ×0.5 şiddet
  paket_noexcl  paket ama Taksit (grace_plus) Acil Fren ile ÖZEL GRUPTA DEĞİL (ikisi birden alınabilir)

Stratejiler: hic / acgozlu / mantikli (config) + acgozlu_noeb (acgozlu ama Acil Fren'i ASLA almaz — Acil Fren
bağımlılığını kart tasarımından bağımsız ölçmek için).

Kullanım:
  python event_kart_2026_09_24.py selftest
  python event_kart_2026_09_24.py iso [--runs N]            # her event'in izole etkisi (tek event her gün)
  python event_kart_2026_09_24.py matrix <variant> [--runs N]
  python event_kart_2026_09_24.py cards [--runs N]           # yeni kartların amortismanı (zorlanmış alım)
  python event_kart_2026_09_24.py report                     # matrix_*.csv -> markdown tablolar
"""
import argparse
import csv
import json
import os
import random
import sys
import time
from multiprocessing import Pool

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import sim  # noqa: E402

COLORS = sim.COLORS
pidx = sim.pidx
OUT = os.path.join(HERE, "results_event_kart_2026-09-24")

# ---------------------------------------------------------------------------
# YENİ EVENT KATALOĞU (öneri). sev: hafif/orta/sert. minDay/maxDay: geçerli gün aralığı.
# Anahtarlar: customers, wait, reward, exit, move, upgradeCost, penalty, phoneSkip, festival (sim.py yerli)
#   + hangar (tır hangar bekleme ×), returnChance (iade oranı), dual (2-kalem müşteri), mixed (karışık tır),
#     gold (günün rengi kutu ödülü ×), speedBonus (hangar penceresinin ilk yarısında teslim ×),
#     mono (tek renk günü), questMult (görev ödülü ×), arrivalFix (müşteri artışında varış aralığı ÷ çarpan)
# ---------------------------------------------------------------------------
NEW_POOL = [
    # ---- POZİTİF (sev "hafif" = gün 1-3 öğretici bandına uygun; "orta" = gün 5+) ----
    dict(name="DELIVERY BONUS", type="pos", sev="hafif", reward=1.2),
    dict(name="EXPRESS CARGO", type="pos", sev="hafif", reward=1.1, exit=0.5, hangar=1.2),
    dict(name="RELAXED DAY", type="pos", sev="hafif", wait=1.5, penalty=0.5),
    dict(name="OPPORTUNITY DAY", type="pos", sev="hafif", upgradeCost=0.7),
    dict(name="CUSTOMER SUPPORT", type="pos", sev="hafif", phoneSkip=0.0),
    dict(name="MONOCHROME DAY", type="pos", sev="hafif", mono=True),
    dict(name="QUEST DAY", type="pos", sev="hafif", questMult=3.0),
    dict(name="GOLDEN BOX DAY", type="pos", sev="hafif", gold=1.6),
    dict(name="VIP SERVICE", type="pos", sev="orta", dual=True, maxDay=8),
    dict(name="FESTIVAL DAY", type="pos", sev="orta", festival=True),
    dict(name="RUSH BONUS", type="pos", sev="orta", speedBonus=1.4),
    dict(name="BUSY DAY", type="pos", sev="orta", customers=1.35, wait=0.85, arrivalFix=True),  # UI: Nötr/takas
    # ---- NEGATİF (şiddet = zayıf P3 günlük net gelir etkisi: hafif <=%15, orta %15-30, sert >%30) ----
    dict(name="ANGRY CUSTOMERS", type="neg", sev="hafif", wait=0.6, customers=1.1, arrivalFix=True),
    dict(name="FATIGUE PROBLEM", type="neg", sev="hafif", move=0.9),
    dict(name="SLOW LOGISTICS", type="neg", sev="hafif", reward=0.9, exit=2.0),
    dict(name="HEAVY BOXES", type="neg", sev="orta", move=0.85),
    dict(name="SURPRISE AUDIT", type="neg", sev="orta", penalty=2.0),
    dict(name="RAINY DAY", type="neg", sev="orta", customers=0.8),
    dict(name="IMPATIENT DRIVERS", type="neg", sev="orta", hangar=0.6),
    dict(name="MARKETING DAY", type="neg", sev="sert", reward=0.7, customers=1.2, arrivalFix=True),
    dict(name="RETURN WAVE", type="neg", sev="sert", returnChance=0.45, minDay=5),
    dict(name="SUPPLY STRIKE", type="neg", sev="sert", customers=0.7, reward=0.9),
]

# Bant kuralı: her bant 3 slot. "P" pozitif, "N" negatif, "X" %50 P/N. Negatif şiddet tavanı banda göre.
BANDS = [
    dict(days=(1, 2, 3), slots=("P", "P", "P"), posSev=("hafif",), negSev=()),
    dict(days=(5, 6, 7), slots=("P", "N", "X"), posSev=("hafif", "orta"), negSev=("hafif",)),
    dict(days=(9, 10, 11), slots=("P", "N", "X"), posSev=("hafif", "orta"), negSev=("hafif", "orta")),
    dict(days=(13, 14, 15), slots=("P", "N", "N"), posSev=("hafif", "orta"), negSev=("orta", "sert")),
]

NUM_KEYS = ("customers", "wait", "reward", "exit", "move", "upgradeCost", "penalty", "phoneSkip",
            "hangar", "gold", "speedBonus", "questMult")

# ---------------------------------------------------------------------------
# 6 YENİ KART (öneri) — id: (sahne adı, tür, tier, maxLevel, baseCost, costStep)
# ---------------------------------------------------------------------------
NEW_CARDS = [
    dict(id="tip_jar", name="Güler Yüz (Bahşiş)", kind="backbone", tier=0, maxLevel=2, baseCost=80, costStep=80),
    dict(id="grace_plus", name="Sağlam Kasa (Taksit)", kind="backbone", tier=0, maxLevel=1, baseCost=60, costStep=0),
    dict(id="cooler", name="Su Sebili (Serinlik)", kind="backbone", tier=0, maxLevel=1, baseCost=40, costStep=0),
    dict(id="morning_shift", name="Dinç Ekip (Sabah Vardiyası)", kind="backbone", tier=0, maxLevel=2, baseCost=80, costStep=80),
    dict(id="ticket_queue", name="Geniş Kuyruk (Sıra Numaratörü)", kind="backbone", tier=0, maxLevel=1, baseCost=70, costStep=0),
    dict(id="forecast", name="Uzun Kuyruk -> Hava Raporu", kind="perk", tier=1, maxLevel=1, baseCost=60, costStep=0),
]
NEW_PERK_EFFECTS = {
    "tip_frac": [0.0, 0.10, 0.20],       # servis edilen müşteri başı bahşiş = frac × P-bazlı kutu ödülü
    "grace_plus_uses": 1,                # +1 taksit hakkı
    "grace_plus_pct": 0.7,               # kartla taksitte kasanın %70'i alınır (taban %80)
    "cooler_damp": 0.25,                 # negatif event sapmasının %25'i kalır (%75 hafifler)
    "morning_boxes": [0, 1, 2],          # gün başı rafa hazır kutu
    "morning_cap": 8,                    # raf toplamı bu sayının altındaysa eklenir
    "forecast_per_cycle": 1,             # kira dönemi başına 1 kez yarının negatif event'ini değiştir
}
NEW_EXEMPT = ["grace_plus"]              # kira fonu kilidinden muaf (Acil Fren gibi) — yalnız can simidi
NEW_GROUPS = [["grace_plus", "emergency_brake"], ["grace_plus", "leveraged_rent"], ["grace_plus", "all_in"]]
MANTIKLI_INSERT = {  # öncelik listesine yerleştirme (index = önüne girilecek kart)
    "tip_jar": "fast_hangar", "morning_shift": "all_in", "grace_plus": "cheap_rent",
    "ticket_queue": "patient_customers", "cooler": "overtime", "forecast": "phone_line",
}


def load_cfg(variant, offer_rand=False):
    cfg = sim.load_config(sim.DEFAULT_CONFIG)
    ev = cfg["events"]
    ev["freeDays"] = 4  # canlı takvim eşdeğeri (base için)
    new_events = variant.startswith("ev") or variant.startswith("paket")
    new_cards = variant.startswith("kart") or variant.startswith("paket")
    cfg["_x"] = dict(newCalendar=new_events, newCards=new_cards, negScale=1.0, arrivalFix=new_events,
                     offerRand=offer_rand)
    if variant.endswith("_hard"):
        cfg["_x"]["negScale"] = 1.5
    elif variant.endswith("_soft"):
        cfg["_x"]["negScale"] = 0.5
    if new_events:
        ev["pool"] = [dict(x) for x in NEW_POOL]
    if new_cards:
        for c in NEW_CARDS:
            cfg["upgrades"].append(dict(c))
        cfg["perkEffects"].update(NEW_PERK_EFFECTS)
        cfg["economy"]["rentReserveExempt"] = list(cfg["economy"]["rentReserveExempt"]) + NEW_EXEMPT
        groups = NEW_GROUPS if not variant.endswith("_noexcl") else [g for g in NEW_GROUPS if "emergency_brake" not in g]
        cfg["economy"]["exclusiveGroups"] = cfg["economy"]["exclusiveGroups"] + groups
        pr = cfg["strategies"]["mantikli"]["priority"]
        for uid, before in MANTIKLI_INSERT.items():
            pr.insert(pr.index(before), uid)
    s = dict(cfg["strategies"]["acgozlu"])
    s["neverBuy"] = ["emergency_brake"]
    cfg["strategies"]["acgozlu_noeb"] = s
    return cfg


def scale_neg(ev, k):
    """Negatif event'in sayısal sapmalarını k ile ölçekle (hassasiyet)."""
    if k == 1.0 or ev.get("type") != "neg":
        return ev
    out = dict(ev)
    for key in NUM_KEYS:
        if key in out:
            out[key] = max(0.0, 1 + (out[key] - 1) * k)
    if "returnChance" in out:
        out["returnChance"] = 0.25 + (out["returnChance"] - 0.25) * k
    return out


class EKRun(sim.Run):
    def __init__(self, *a, **kw):
        super().__init__(*a, **kw)
        x = self.cfg.get("_x", {})
        self.x = x
        seed = a[5] if len(a) > 5 else kw.get("seed", 0)
        # Koşu-bazlı teklif RNG'si (self.rng'yi TÜKETMEZ). Canlı kod ve sim.py günlük teklifi YALNIZ gün
        # ile tohumluyor (UpgradePanel.cs:1343) → her koşuda aynı gün aynı teklif. offerRand=True bunu kırar.
        self._offer_rng = random.Random((seed * 2654435761) ^ 0x5BD1E995)
        self.forecast_used = set()
        self.cards_on = bool(x.get("newCards"))
        if "tip_jar" not in self.levels:
            for c in ("tip_jar", "grace_plus", "cooler", "morning_shift", "ticket_queue", "forecast"):
                self.levels.setdefault(c, 0)
        self.tip_total = 0
        self.morning_total = 0
        self.grace_plus_used = 0
        self.forecast_swaps = 0
        self.eb_used = False

    # ---- takvim -----------------------------------------------------------
    def _gen_calendar(self):
        if not self.cfg.get("_x", {}).get("newCalendar"):
            return super()._gen_calendar()
        rng = random.Random(self.rng.randrange(1, 2 ** 31))
        pool = [scale_neg(e, self.cfg["_x"]["negScale"]) for e in self.cfg["events"]["pool"]]
        used = set()
        cal = {}
        for band in BANDS:
            slots = list(band["slots"])
            rng.shuffle(slots)
            for day, s in zip(band["days"], slots):
                if s == "X":
                    s = "P" if rng.random() < 0.5 else "N"
                cand = self._cands(pool, used, day, s, band)
                if not cand:  # emniyet: şiddet kısıtını gevşet
                    cand = [e for e in pool if e["name"] not in used and self._day_ok(e, day)
                            and e["type"] == ("pos" if s == "P" else "neg")]
                if not cand:
                    continue
                ev = cand[rng.randrange(len(cand))]
                used.add(ev["name"])
                cal[day] = ev
        self._pool_scaled = pool
        return cal

    @staticmethod
    def _day_ok(e, day):
        return e.get("minDay", 1) <= day <= e.get("maxDay", 99)

    def _cands(self, pool, used, day, s, band):
        typ = "pos" if s == "P" else "neg"
        sevs = band["posSev"] if s == "P" else band["negSev"]
        return [e for e in pool if e["type"] == typ and e["sev"] in sevs and e["name"] not in used and self._day_ok(e, day)]

    # ---- event çarpanları -------------------------------------------------
    def event_mults(self, day):
        ev = self.calendar.get(day)
        m = {"customers": 1.0, "wait": 1.0, "reward": 1.0, "exit": 1.0, "move": 1.0,
             "upgradeCost": 1.0, "penalty": 1.0, "phoneSkip": 1.0, "festival": False, "name": None,
             "hangar": 1.0, "returnChance": None, "dual": False, "mixed": 0.0, "gold": 1.0,
             "speedBonus": 1.0, "mono": False, "questMult": 1.0, "arrivalFix": False, "type": None}
        if ev:
            m["name"] = ev["name"]
            m["type"] = ev.get("type")
            for k in list(m.keys()):
                if k in ev and k not in ("name", "type"):
                    m[k] = ev[k]
            if ev.get("mixed") is True:
                m["mixed"] = 1.0
            if self.levels.get("cooler") and ev.get("type") == "neg":
                d = self.pe["cooler_damp"]
                for k in NUM_KEYS:
                    m[k] = 1 + (m[k] - 1) * d
                if m["returnChance"] is not None:
                    m["returnChance"] = 0.25 + (m["returnChance"] - 0.25) * d
                m["mixed"] = m["mixed"] * d
        return m

    def forecast_check(self, day):
        """Hava Raporu: kira dönemi başına 1 kez, bugünün (dün görülen) negatif event'i pozitifle değişir."""
        if not self.levels.get("forecast"):
            return
        ev = self.calendar.get(day)
        cyc = (day - 1) // self.eco["rentIntervalDays"]
        if not ev or ev.get("type") != "neg" or cyc in self.forecast_used:
            return
        pool = getattr(self, "_pool_scaled", self.cfg["events"]["pool"])
        used = {e["name"] for e in self.calendar.values()}
        cand = [e for e in pool if e["type"] == "pos" and e["name"] not in used and self._day_ok(e, day)]
        if not cand:
            return
        self.calendar[day] = cand[self.rng.randrange(len(cand))]
        self.forecast_used.add(cyc)
        self.forecast_swaps += 1

    # ---- ana döngü --------------------------------------------------------
    def run(self):
        for day in range(1, self.days + 1):
            self.day = day
            self.activate_pending(day)
            self.forecast_check(day)
            self.simulate_day(day)
            if self.dead:
                break
        return self.summary()

    def activate_pending(self, day):
        before = self.levels.get("grace_plus", 0)
        super().activate_pending(day)
        if self.levels.get("grace_plus", 0) > before:
            self.grace_left += self.pe["grace_plus_uses"]

    def summary(self):
        s = super().summary()
        s.update(tip=self.tip_total, morning=self.morning_total, gracePlusUsed=self.grace_plus_used,
                 forecastSwaps=self.forecast_swaps, ebUsed=self.eb_used)
        return s

    # ---- gün (sim.Run.simulate_day kopyası + kancalar; KANCA etiketli satırlar yeni) ----------
    def simulate_day(self, day):
        e, pf, asm, pe = self.eco, self.prof, self.asm, self.pe
        P = self.P
        rng = self.rng
        T = self.day_duration(day)
        m = self.event_mults(day)
        pen_mult = m["penalty"]
        X = self.x

        quest_money = 0
        if self.quest_settle:
            qm, qp, _ = self.quest_settle
            self.add_money(qm)
            self.add_prestige(qp)
            quest_money = qm
            self.quest_settle = None
        festival = 0
        if m["festival"]:
            lo, hi = e["festivalRentShare"]
            festival = int(round(rng.uniform(lo, hi) * self.rent()))
            self.add_money(festival)
        self.quest_offer_and_accept(day)

        # KANCA: Sabah Vardiyası — gün başı rafa hazır kutu
        mb = self.pe.get("morning_boxes", [0])[self.levels.get("morning_shift", 0)] if self.cards_on else 0
        for _ in range(mb):
            if sum(self.stock.values()) < self.pe["morning_cap"]:
                self.stock[rng.choice(COLORS)] += 1
                self.morning_total += 1
        # KANCA: tek renk / günün rengi
        mono_col = rng.choice(COLORS) if m["mono"] else None
        gold_col = rng.choice(COLORS) if m["gold"] != 1.0 else None

        quota = max(e["minCustomersPerDay"], min(e["maxCustomersPerDay"], int(round(e["dailyCustomerCount"][pidx(P)][min(day, 16) - 1] * m["customers"]))))
        pmin = max(e["patienceFloorMin"], e["baseMinPatience"] - (P - 1) * e["patienceReductionPerPlayer"]) * m["wait"]
        pmax = max(e["patienceFloorMax"], e["baseMaxPatience"] - (P - 1) * e["patienceReductionPerPlayer"]) * m["wait"]
        arrival = e["customerArrivalIntervalByPlayerCount"][pidx(P)]
        if m["arrivalFix"] and X.get("arrivalFix") and m["customers"] > 1.0:  # KANCA
            arrival = arrival / m["customers"]
        R = e["rewardPerBoxByPlayerCount"][pidx(P)]
        R_base = R
        if self.levels["gambler_case"]:
            R = int(round(R * pe["gambler_reward_mult"]))
        elif self.levels["all_in"]:
            R = int(round(R * pe["all_in_reward_mult"]))
        R = int(R * m["reward"])
        penalty_box = e["penaltyPerBox"]
        if self.levels["gambler_case"]:
            penalty_box = int(round(penalty_box * pe["gambler_penalty_mult"]))
        bonus_per_tier = e["bonusPerTier"] + pe["prestige_broker_bonus_step"] * self.levels["prestige_broker"]
        served_bonus = e["customerServedPrestigeBonus"] + pe["prestige_master_served_step"] * self.levels["prestige_master"]
        hangar_stay = e["hangarStayDurationByPlayerCount"][pidx(P)] * (pe["fast_hangar_mult"] if self.levels["fast_hangar"] else 1.0)
        hangar_stay *= m["hangar"]  # KANCA
        exit_delay = e["exitDelay"] * m["exit"]
        respawn_mult = 1.0
        if self.levels["fast_hangar"]:
            exit_delay = max(pe.get("fast_hangar_exit_min", 0.0), exit_delay * pe.get("fast_hangar_exit_mult", 1.0))
            respawn_mult = pe.get("fast_hangar_respawn_mult", 1.0)
        day_end_grace = e["dayEndGraceSeconds"] + (pe.get("overtime_grace_add", 0.0) if self.levels["overtime"] else 0.0)
        n_hangars = e["hangarsAtStart"] + self.levels["extra_hangar"]
        grants_station = bool(pe.get("packing_station_grants_station", 0))
        n_tables = e["packingTablesAtStart"] + (0 if grants_station else self.levels["packing_station"])
        extra_station = self.levels["packing_station"] if grants_station else 0
        cmin = e["truckCargoMinByPlayerCount"][pidx(P)]
        cmax = e["truckCargoMaxExclusiveByPlayerCount"][pidx(P)]
        speed = m["move"] * (pe["agile_speed_mult"] if self.levels["agile_crew"] else 1.0)
        labor_mult = (1 - asm["walkShare"]) + asm["walkShare"] / speed
        if self.levels["energetic_crew"]:
            labor_mult *= pe["energetic_labor_mult"]
        inter = e["interactionTime"] * (pe["patient_interaction_mult"] if self.levels["patient_customers"] else 1.0)
        base_real = e["realDurationInSeconds"] * (pe["overtime_mult"] if self.levels["overtime"] else 1.0)
        skip_sec = e["timeSkipAmountByPlayerCount"][pidx(P)] * (pe["phone_line_skip_mult"] if self.levels["phone_line"] else 1.0) \
            * m["phoneSkip"] * (base_real / (e["endHour"] - e["startHour"])) / 60.0
        vol = self.levels["high_volatility"] > 0
        dual_day = day >= e["dualItemUnlockDay"] or m["dual"]  # KANCA
        ret_chance = e["returnModeChance"] if m["returnChance"] is None else m["returnChance"]  # KANCA
        return_day = day >= e["returnUnlockDay"] or m["returnChance"] is not None
        mixed_day = day >= e["mixedTruckUnlockDay"]
        mixed_p = m["mixed"]
        p_wrong_del = pf["p_wrongDelivery"] * (0.3 if mono_col else 1.0)  # KANCA (varsayım)
        p_wrong_prod = pf["p_wrongProduct"] * (0.5 if mono_col else 1.0)
        tip = int(round(pe["tip_frac"][self.levels["tip_jar"]] * R_base)) if self.cards_on and self.levels.get("tip_jar") else 0
        ticket = self.cards_on and self.levels.get("ticket_queue", 0) > 0

        def t_serve(dual, is_return):
            base = pf["t_serve"] - e["interactionTime"]
            im = inter
            if dual:
                im *= (e["dualItemBoxRequestMult"] * 2 if is_return else e["dualItemInteractionMult"])
            return (base + im) * labor_mult + pf["reaction"]

        t_pack = pf["t_pack"] * labor_mult + pf["reaction"]
        t_load = pf["t_load"] * labor_mult + pf["reaction"]
        t_phone = pf["t_phone"] * labor_mult + pf["reaction"]

        st = {"quota": quota, "spawned": 0, "served": 0, "lost": 0, "missed": 0, "wrongProduct": 0,
              "packed": 0, "delivered": 0, "trucksSpawned": 0, "trucksFull": 0, "phone": 0,
              "wrongDelivery": 0, "drops": 0, "income": 0, "penaltyMoney": 0, "returnServed": 0,
              "productsSupplied": 0, "earlyEnd": False, "idleSec": 0.0, "dayLen": T, "event": m["name"],
              "shopSpent": 0, "rerollSpent": 0, "bought": [], "tip": 0}
        supply_counts = {"shelf": 0, "truck": 0, "pack": 0, "phone": 0}

        customers = []
        queue = []
        n_stations = max(1, int(e.get("serviceStations", 1)) + extra_station)
        stations = [None] * n_stations
        players = [{"busy": 0.0, "task": None} for _ in range(P)]
        tables_free_at = [0.0] * n_tables
        trucks = [{"state": "none", "until": 0.0, "need": {}, "reserved": {}, "delivered": 0, "cargo": 0, "timer_end": 0.0, "color": None} for _ in range(n_hangars)]
        phone_cd = 0.0
        next_spawn_e = 0.0
        t = 0.0
        el = 0.0
        early_end_t = None
        exited_1730 = False
        shop_done = False
        idle_next = [0.0] * P

        def hour_of(x):
            return e["startHour"] + (e["endHour"] - e["startHour"]) * min(1.0, x / T)

        def wave(hour):
            for w in e["wavePeriods"]:
                if w["start"] <= hour < w["end"]:
                    return w
            return {"maxCustomers": 3, "rate": 1.0}

        def draw_truck_color():
            if mono_col:  # KANCA
                return mono_col
            if not self.color_bag:
                self.color_bag = list(COLORS)
                rng.shuffle(self.color_bag)
            return self.color_bag.pop()

        def spawn_truck(tr):
            cargo = rng.randrange(cmin, cmax)
            need = {}
            is_mixed = mixed_day or (mixed_p > 0 and rng.random() < mixed_p)  # KANCA
            if is_mixed and cargo >= 2 and not mono_col:
                slots = 3 if (cargo >= 3 and rng.random() < 0.5) else 2
                cols = []
                guard = 0
                while len(cols) < slots and guard < 50:
                    guard += 1
                    c = draw_truck_color()
                    if c not in cols:
                        cols.append(c)
                counts = [1] * len(cols)
                for _ in range(cargo - len(cols)):
                    counts[rng.randrange(len(cols))] += 1
                for c, n in zip(cols, counts):
                    need[c] = need.get(c, 0) + n
            else:
                need[draw_truck_color()] = cargo
            tr.update({"state": "entering", "until": t + asm["truckEnterAnimSeconds"], "need": need,
                       "reserved": {c: 0 for c in COLORS}, "delivered": 0, "cargo": cargo, "timer_end": 0.0})
            st["trucksSpawned"] += 1

        def truck_leave(tr, full):
            tr["state"] = "exiting"
            tr["until"] = t + exit_delay + asm["truckExitAnimSeconds"]
            if full:
                st["trucksFull"] += 1
                self.quest_progress("truck", max(tr["need_orig"], key=tr["need_orig"].get) if tr.get("need_orig") else None)
                supply_counts["truck"] += 1

        def spawn_customer(force=False):
            nonlocal next_spawn_e
            mode = "return" if (return_day and rng.random() < ret_chance) else "supply"
            pat = rng.uniform(pmin, pmax)  # sim.py dict-literal sırası: önce sabır, sonra renkler
            if mono_col:
                cols = [mono_col, mono_col]
            else:
                cols = [rng.choice(COLORS), rng.choice(COLORS)]
            c = {"arrive": t + asm["customerWalkInSeconds"], "patience": pat, "queued_at": None,
                 "mode": mode, "dual": dual_day, "colors": cols,
                 "state": "walking", "served_by": None}
            customers.append(c)
            st["spawned"] += 1
            hour = hour_of(el)
            jitter = rng.uniform(-arrival * e["spawnTimeRandomness"], arrival * e["spawnTimeRandomness"])
            interval = max(1.0, arrival + jitter) / wave(hour)["rate"]
            next_spawn_e = el + interval

        def reward_now():
            tiers = int(self.prestige // e["prestigePerBonus"])
            base = R + int(round(tiers * bonus_per_tier))
            if vol:
                base = int(round(base * (pe["volatility_mean"] + rng.uniform(-pe["volatility_range"], pe["volatility_range"]))))
            return max(0, base)

        def customer_leave(c, lost):
            c["state"] = "gone"
            if c in queue:
                queue.remove(c)
            for si in range(n_stations):
                if stations[si] is c:
                    stations[si] = None
            if lost:
                st["lost"] += 1
                self.add_prestige(e["customerLostPrestigePenalty"] * pen_mult)

        dt = self.dt
        while el < T and self.dead is None:
            hour = hour_of(el)
            if not exited_1730 and hour >= e["customerExitHour"]:
                exited_1730 = True
                for c in list(customers):
                    if c["state"] in ("walking", "queued"):
                        being_served = c["served_by"] is not None
                        customer_leave(c, lost=not being_served)
                missed = quota - st["spawned"]
                if missed > 0:
                    st["missed"] = missed
                    for _ in range(missed):
                        self.add_prestige(e["customerMissedQuotaPrestigePenalty"] * pen_mult)

            if (e["spawnStartHour"] <= hour <= e["spawnEndHour"] and st["spawned"] < quota
                    and len(queue) + sum(1 for c in customers if c["state"] == "walking") < e["maxQueueSize"]
                    and len(queue) < wave(hour)["maxCustomers"] and el >= next_spawn_e):
                spawn_customer()

            for c in customers:
                if c["state"] == "walking" and t >= c["arrive"]:
                    c["state"] = "queued"
                    c["queued_at"] = t
                    queue.append(c)
                elif c["state"] == "queued" and c["served_by"] is None:
                    if ticket:  # KANCA: sabır saati yalnız istasyonda (sıra başında) işler
                        if c.get("st_at") is not None and t - c["st_at"] >= c["patience"]:
                            customer_leave(c, lost=True)
                    elif t - c["queued_at"] >= c["patience"]:
                        customer_leave(c, lost=True)
            for c in queue:
                if c["state"] != "queued" or c in stations:
                    continue
                for si in range(n_stations):
                    if stations[si] is None:
                        stations[si] = c
                        if c.get("st_at") is None:
                            c["st_at"] = t
                        break
                else:
                    break

            trucks_open = e["truckStartHour"] <= hour < e["truckEndHour"]
            for tr in trucks:
                s = tr["state"]
                if s == "none":
                    if trucks_open:
                        spawn_truck(tr)
                        tr["need_orig"] = dict(tr["need"])
                elif s == "entering":
                    if t >= tr["until"]:
                        tr["state"] = "waiting"
                        tr["timer_end"] = t + hangar_stay
                        tr["wait_start"] = t
                elif s == "waiting":
                    if hour >= e["truckEndHour"] or t >= tr["timer_end"]:
                        truck_leave(tr, full=False)
                elif s == "exiting":
                    if t >= tr["until"]:
                        tr["state"] = "respawn"
                        tr["until"] = t + rng.uniform(*e["respawnDelayRange"]) * respawn_mult
                        for c in COLORS:
                            self.stock[c] += tr["reserved"].get(c, 0)
                            tr["reserved"][c] = 0
                elif s == "respawn":
                    if t >= tr["until"]:
                        tr["state"] = "none"
                        if not trucks_open:
                            tr["state"] = "closed"

            if not shop_done and hour >= e["panelOpenHour"]:
                shop_done = True
                if self.strat.get("buy") or self.force_cards:
                    spent = self.shop(day, m["upgradeCost"], st)
                    if spent > 0:
                        shopper = min(players, key=lambda p: p["busy"])
                        shopper["busy"] = max(shopper["busy"], t) + asm["shopTripSeconds"] * labor_mult

            for pi, pl in enumerate(players):
                if pl["busy"] > t:
                    continue
                task = pl["task"]
                pl["task"] = None
                if task:
                    kind = task[0]
                    if kind == "serve":
                        c = task[1]
                        if c["state"] == "queued":
                            c["served_by"] = None
                            if c["mode"] == "supply":
                                if rng.random() < p_wrong_prod:
                                    st["wrongProduct"] += 1
                                    self.add_prestige(e["wrongProductPrestigePenalty"] * pen_mult)
                                    customer_leave(c, lost=False)
                                else:
                                    n = 2 if c["dual"] else 1
                                    for k in range(n):
                                        self.reception.append(c["colors"][k])
                                    st["productsSupplied"] += n
                                    st["served"] += 1
                                    self.add_prestige(served_bonus)
                                    customer_leave(c, lost=False)
                                    if tip:  # KANCA
                                        self.add_money(tip); st["tip"] += tip; self.tip_total += tip
                            else:
                                st["served"] += 1
                                st["returnServed"] += 1
                                self.add_prestige(served_bonus)
                                customer_leave(c, lost=False)
                                if tip:  # KANCA
                                    self.add_money(tip); st["tip"] += tip; self.tip_total += tip
                    elif kind == "load":
                        tr, col = task[1], task[2]
                        tr["reserved"][col] -= 1
                        if tr["state"] == "waiting" and tr["need"].get(col, 0) > 0:
                            r = rng.random()
                            if r < p_wrong_del:
                                st["wrongDelivery"] += 1
                                pen = int(round(penalty_box * pen_mult))
                                self.add_money(-pen)
                                st["penaltyMoney"] += pen
                                self.add_prestige(e["wrongDeliveryPrestigePenalty"] * pen_mult)
                            elif r < p_wrong_del + pf["p_drop"]:
                                st["drops"] += 1
                                pen = int(round(e["boxDropMoneyPenalty"] * pen_mult))
                                self.add_money(-pen)
                                st["penaltyMoney"] += pen
                                self.add_prestige(e["boxDropPrestigePenalty"] * pen_mult)
                            else:
                                rw = reward_now()
                                if gold_col is not None and col == gold_col:  # KANCA
                                    rw = int(round(rw * m["gold"]))
                                if m["speedBonus"] != 1.0 and t < tr.get("wait_start", 0) + hangar_stay * 0.5:  # KANCA
                                    rw = int(round(rw * m["speedBonus"]))
                                self.add_money(rw)
                                st["income"] += rw
                                st["delivered"] += 1
                                tr["need"][col] -= 1
                                tr["delivered"] += 1
                                if sum(tr["need"].values()) <= 0:
                                    truck_leave(tr, full=True)
                        else:
                            self.stock[col] += 1
                    elif kind == "pack":
                        col = task[1]
                        if rng.random() < pf["p_drop"]:
                            st["drops"] += 1
                            pen = int(round(e["boxDropMoneyPenalty"] * pen_mult))
                            self.add_money(-pen)
                            st["penaltyMoney"] += pen
                            self.add_prestige(e["boxDropPrestigePenalty"] * pen_mult)
                        else:
                            self.stock[col] += 1
                            st["packed"] += 1
                            supply_counts["pack"] += 1
                            supply_counts["shelf"] += 1
                            self.quest_progress("pack", col)
                            self.quest_progress("shelf", col)
                    elif kind == "phone":
                        hour2 = hour_of(el)
                        ok = (e["phoneStartHour"] <= hour2 < e["phoneEndHour"] and st["spawned"] < quota
                              and len(queue) + sum(1 for c in customers if c["state"] == "walking") < e["maxQueueSize"]
                              and t >= phone_cd and hour_of(el + skip_sec) < e["customerExitHour"])
                        if ok:
                            spawn_customer(force=True)
                            st["phone"] += 1
                            supply_counts["phone"] += 1
                            self.quest_progress("phone")
                            phone_cd = t + e["phoneCooldownSeconds"]
                            el += skip_sec
                            self.add_money(e["callMoneyReward"])
                            self.add_prestige(e["callPrestigeReward"])
                    if self.dead:
                        break

                if self.dead:
                    break
                took_serve = False
                for c in stations:
                    if c is None or c["state"] != "queued" or c["served_by"] is not None:
                        continue
                    can = True
                    if c["mode"] == "return":
                        need = c["colors"][:2] if c["dual"] else c["colors"][:1]
                        tmp = dict(self.stock)
                        for col in need:
                            if tmp[col] > 0:
                                tmp[col] -= 1
                            else:
                                can = False
                        if can:
                            for col in need:
                                self.stock[col] -= 1
                    if can:
                        c["served_by"] = pi
                        pl["task"] = ("serve", c)
                        pl["busy"] = t + t_serve(c["dual"], c["mode"] == "return")
                        took_serve = True
                        break
                if took_serve:
                    continue
                loaded = False
                for tr in trucks:
                    if tr["state"] != "waiting":
                        continue
                    items = tr["need"].items()
                    if gold_col is not None:  # KANCA: günün rengine öncelik
                        items = sorted(items, key=lambda kv: kv[0] != gold_col)
                    for col, n in items:
                        if n - tr["reserved"].get(col, 0) > 0 and self.stock[col] > 0:
                            self.stock[col] -= 1
                            tr["reserved"][col] = tr["reserved"].get(col, 0) + 1
                            pl["task"] = ("load", tr, col)
                            pl["busy"] = t + t_load
                            loaded = True
                            break
                    if loaded:
                        break
                if loaded:
                    continue
                if self.reception:
                    ti = None
                    for k, fa in enumerate(tables_free_at):
                        if fa <= t:
                            ti = k
                            break
                    if ti is not None:
                        col = self.reception.pop(0)
                        tables_free_at[ti] = t + pf["S_pack"]
                        pl["task"] = ("pack", col)
                        pl["busy"] = t + t_pack
                        continue
                if t >= idle_next[pi]:
                    idle_next[pi] = t + asm["idleRecheckSeconds"]
                    if (e["phoneStartHour"] <= hour < e["phoneEndHour"] and st["spawned"] < quota
                            and len(queue) + sum(1 for c in customers if c["state"] == "walking") < e["maxQueueSize"]
                            and t >= phone_cd and hour_of(el + skip_sec) < e["customerExitHour"]
                            and rng.random() < pf["phone_use"]):
                        pl["task"] = ("phone",)
                        pl["busy"] = t + t_phone
                        continue
                st["idleSec"] += dt / P

            if early_end_t is None and st["spawned"] >= quota and not queue and not any(c["state"] == "walking" for c in customers):
                early_end_t = t + day_end_grace
            if early_end_t is not None and t >= early_end_t:
                st["earlyEnd"] = True
                el = T
                break

            t += dt
            el += dt

        for tr in trucks:
            for c in COLORS:
                self.stock[c] += tr["reserved"].get(c, 0)
        self.yesterday_supply = dict(supply_counts)
        qres = self.quest_settle_day(day)
        if qres and qres[2] and m["questMult"] != 1.0:  # KANCA: Görev Günü
            qm_, qp_, ok_ = self.quest_settle
            self.quest_settle = (int(round(qm_ * m["questMult"])), qp_ * m["questMult"], ok_)

        rent_paid = 0
        gate = None
        if self.dead is None and day % e["rentIntervalDays"] == 0:
            r = self.rent()
            if self.money >= r:
                self.money -= r
                rent_paid = r
                self.rent_cycle += 1
                gate = "paid"
            elif self.grace_left > 0 and (e.get("graceZeroPctLive", False)
                                          or not (self.levels["leveraged_rent"] or self.levels["all_in"])):
                gp = 0.0 if (self.levels["leveraged_rent"] or self.levels["all_in"]) else e["gracePaymentPercent"]
                if self.levels.get("grace_plus"):  # KANCA: Taksit kartı oranı
                    gp = min(gp, self.pe["grace_plus_pct"])
                    self.grace_plus_used += 1
                took = int(round(self.money * gp))
                self.money -= took
                rent_paid = took
                self.grace_left -= 1
                self.rent_cycle += 1
                gate = "grace"
            elif self.insurance:
                self.insurance = False
                self.insurance_consumed = True
                self.add_prestige(e["emergencyBrakePrestigePenalty"])
                self.rent_cycle += 1
                gate = "insurance"
                self.eb_used = True
            else:
                gate = "bankrupt"
                self.dead = ("bankrupt", day)
        self.total_income += st["income"]
        self.total_rent += rent_paid
        self.total_penalty_money += st["penaltyMoney"]
        st.update({"day": day, "moneyEnd": self.money, "prestigeEnd": round(self.prestige, 2), "rentPaid": rent_paid,
                   "gate": gate, "quest": (qres[2] if qres else None), "questMult": m["questMult"], "questMoney": quest_money, "festival": festival,
                   "stockEnd": sum(self.stock.values()), "receptionEnd": len(self.reception),
                   "levels": {k: v for k, v in self.levels.items() if v}})
        self.records.append(st)

    def select_offer(self, elig, rng):
        if self.x.get("offerRand") and rng is not self.rng:
            rng = self._offer_rng
        return super().select_offer(elig, rng)

    # ---- neverBuy: kart draft havuzundan tamamen çıkar (Acil Fren'siz oyun) --------------
    def eligibility(self, day):
        el = super().eligibility(day)
        nb = self.strat.get("neverBuy")
        if nb:
            for i, uid in enumerate(self.up_order):
                if uid in nb:
                    el[i] = False
        return el


# ---------------------------------------------------------------------------
# Koşucular
# ---------------------------------------------------------------------------
SEED0 = 20260918
PROFILES = ("zayif", "orta", "iyi")
STRATS = ("hic", "acgozlu", "mantikli", "acgozlu_noeb")
NEWC = [c["id"] for c in NEW_CARDS]


def pct(v, q):
    v = sorted(v)
    if not v:
        return 0
    k = (len(v) - 1) * q
    f = int(k)
    c = min(f + 1, len(v) - 1)
    return v[f] + (v[c] - v[f]) * (k - f)


def run_cell(args):
    variant, P, prof, strat, n, orand = args
    cfg = load_cfg(variant, orand)
    lost = 0
    bk = {4: 0, 8: 0, 12: 0, 16: 0}
    finals, incomes = [], []
    eb_buy = eb_used = 0
    own = {c: 0 for c in NEWC}
    tip = morning = gp_used = fc = 0
    noeb_n = noeb_lost = 0
    ev_days = pos_days = 0
    for i in range(n):
        r = EKRun(cfg, P, prof, strat, True, SEED0 + i * 7919)
        o = r.run()
        dead = bool(o["dead"])
        lost += dead
        if dead and o["dead"][0] == "bankrupt":
            bk[o["dead"][1]] = bk.get(o["dead"][1], 0) + 1
        finals.append(o["finalMoney"] if not dead else 0)
        incomes.append(o["income"])
        bought = set()
        for rec in o["records"]:
            bought.update(rec["bought"])
            if rec["event"]:
                ev_days += 1
        eb_buy += "emergency_brake" in bought
        eb_used += o["ebUsed"]
        if not o["ebUsed"]:
            noeb_n += 1
            noeb_lost += dead
        for c in NEWC:
            own[c] += c in bought
        tip += o["tip"]
        morning += o["morning"]
        gp_used += o["gracePlusUsed"] > 0
        fc += o["forecastSwaps"]
        pos_days += sum(1 for d, ev in r.calendar.items() if d <= 16 and ev.get("type") == "pos")
    return dict(variant=variant, P=P, profile=prof, strategy=strat, runs=n, win=round(1 - lost / n, 4),
                lose=round(lost / n, 4), bk_d4=bk[4], bk_d8=bk[8], bk_d12=bk[12], bk_d16=bk[16],
                final_p10=int(pct(finals, .1)), final_med=int(pct(finals, .5)), final_p90=int(pct(finals, .9)),
                income_mean=int(sum(incomes) / n), eb_buy=round(eb_buy / n, 3), eb_used=round(eb_used / n, 3),
                lose_without_eb=round(noeb_lost / noeb_n, 4) if noeb_n else "", n_without_eb=noeb_n,
                **{f"own_{c}": round(own[c] / n, 3) for c in NEWC},
                tip_mean=int(tip / n), morning_mean=round(morning / n, 1), grace_plus_used=round(gp_used / n, 3),
                forecast_swaps=round(fc / n, 2), event_days_mean=round(ev_days / n, 2), pos_cal_mean=round(pos_days / n, 2))


def write_csv(rows, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    keys = []
    for r in rows:
        for k in r:
            if k not in keys:
                keys.append(k)
    with open(path, "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=keys)
        w.writeheader()
        w.writerows(rows)


def cmd_matrix(variant, n, procs, strats=STRATS, orand=True):
    jobs = [(variant, P, pr, st, n, orand) for P in (1, 2, 3, 4) for pr in PROFILES for st in strats]
    t0 = time.time()
    with Pool(procs) as p:
        rows = p.map(run_cell, jobs, chunksize=1)
    for r in rows:
        r["offers"] = "random" if orand else "fixed"
    write_csv(rows, os.path.join(OUT, f"matrix_{variant}{'' if orand else '_fixed'}.csv"))
    print(f"{variant}: {len(jobs)} hücre x {n} = {len(jobs) * n} koşu, {time.time() - t0:.0f}s")
    return rows


# ---- iso: tek event her geçerli kira-dışı günde, strateji hic; null ikiz (event'siz) AYNI GÜNLERLE kıyaslanır ----
ISO_KEYS = ("income", "lost", "delivered", "served", "spawned", "quota", "penalty")


def iso_cell(args):
    evdef, P, prof, n, which = args
    cfg = load_cfg("paket" if which == "new" else "base")
    per_day = {d: dict.fromkeys(ISO_KEYS + ("n",), 0) for d in range(1, 17)}
    lost_runs = 0
    for i in range(n):
        r = EKRun(cfg, P, prof, "hic", False, SEED0 + i * 7919)
        if evdef is not None:
            ev = dict(evdef)
            r.calendar = {d: ev for d in range(1, 17) if d % 4 != 0 and EKRun._day_ok(ev, d)}
        o = r.run()
        lost_runs += bool(o["dead"])
        for rec in o["records"]:
            a = per_day[rec["day"]]
            a["income"] += rec["income"] + rec.get("festival", 0) + rec.get("tip", 0) - rec["penaltyMoney"]
            a["lost"] += rec["lost"]
            a["delivered"] += rec["delivered"]
            a["served"] += rec["served"]
            a["spawned"] += rec["spawned"]
            a["quota"] += rec["quota"]
            a["penalty"] += rec["penaltyMoney"]
            a["n"] += 1
    return (evdef["name"] if evdef else "(yok)", which, P, prof, per_day, lost_runs / n, evdef)


def _sum_days(per_day, days):
    t = dict.fromkeys(ISO_KEYS + ("n",), 0)
    for d in days:
        for k in t:
            t[k] += per_day[d][k]
    nn = max(1, t["n"])
    return {k: t[k] / nn for k in ISO_KEYS}, t["n"]


def cmd_iso(n, procs):
    old = sim.load_config(sim.DEFAULT_CONFIG)["events"]["pool"]
    jobs = []
    for P in (2, 3):
        for prof in ("zayif", "orta"):
            jobs.append((None, P, prof, n, "old"))
            for ev in old:
                jobs.append((ev, P, prof, n, "old"))
            for ev in NEW_POOL:
                jobs.append((ev, P, prof, n, "new"))
    with Pool(procs) as p:
        res = p.map(iso_cell, jobs, chunksize=1)
    base = {(P, prof): (pdays, lr) for (nm, w, P, prof, pdays, lr, ev) in res if nm == "(yok)"}
    rows = []
    for nm, w, P, prof, pdays, lr, ev in res:
        bdays, blr = base[(P, prof)]
        evd = ev or {}
        days = [d for d in range(1, 17) if d % 4 != 0 and evd.get("minDay", 1) <= d <= evd.get("maxDay", 99)]
        m, cnt = _sum_days(pdays, days)
        b, _ = _sum_days(bdays, days)
        rows.append(dict(event=nm, catalog=w, P=P, profile=prof, dayRange=f"{days[0]}-{days[-1]}", eventDays=cnt,
                         net_income_day=round(m["income"], 1), base_income_day=round(b["income"], 1),
                         d_income_pct=round(100 * (m["income"] / b["income"] - 1), 1) if b["income"] else "",
                         lost_day=round(m["lost"], 2), d_lost=round(m["lost"] - b["lost"], 2),
                         delivered_day=round(m["delivered"], 2), served_day=round(m["served"], 2),
                         spawn_fill=round(m["spawned"] / m["quota"], 3) if m["quota"] else "",
                         penalty_day=round(m["penalty"], 1), run_lose=round(lr, 4), run_lose_base=round(blr, 4)))
    write_csv(rows, os.path.join(OUT, "event_iso.csv"))
    for r in rows:
        print(r["event"], r["catalog"], r["P"], r["profile"], r["dayRange"], r["d_income_pct"], r["d_lost"], r["spawn_fill"], r["run_lose"])


# ---- kart amortismanı: gün 1'den itibaren zorla al (hic stratejisi), null ikiz aynı tohumla ----
def card_cell(args):
    card, P, prof, n = args
    cfg = load_cfg("paket", True)
    d_money = d_income = 0
    buy_day = []
    lose_with = lose_without = 0
    cost_paid = 0
    for i in range(n):
        seed = SEED0 + i * 7919
        a = EKRun(cfg, P, prof, "hic", True, seed, force_cards=[card])
        oa = a.run()
        b = EKRun(cfg, P, prof, "hic", True, seed)
        ob = b.run()
        bd = next((rec["day"] for rec in oa["records"] if card in rec["bought"]), None)
        if bd:
            buy_day.append(bd)
            cost_paid += oa["upgrades"]
        d_money += (oa["finalMoney"] if not oa["dead"] else 0) - (ob["finalMoney"] if not ob["dead"] else 0)
        d_income += (oa["income"] + oa["tip"]) - (ob["income"] + ob["tip"])
        lose_with += bool(oa["dead"])
        lose_without += bool(ob["dead"])
    nb = max(1, len(buy_day))
    mean_bd = sum(buy_day) / nb if buy_day else None
    cost = cost_paid / nb
    active_days = (16 - mean_bd) if mean_bd else 0
    gain_day = (d_income / n) / active_days if active_days else 0
    return dict(card=card, P=P, profile=prof, runs=n, bought_frac=round(len(buy_day) / n, 3),
                buy_day_mean=round(mean_bd, 2) if mean_bd else "", cost_mean=int(cost),
                d_final_money=int(d_money / n), d_income_gross=int(d_income / n),
                gain_per_active_day=round(gain_day, 1),
                payback_days=round(cost / gain_day, 2) if gain_day > 0 else "",
                lose_with=round(lose_with / n, 4), lose_without=round(lose_without / n, 4))


def cmd_cards(n, procs, only=None):
    cards = only or (NEWC + ["emergency_brake"])
    jobs = [(c, P, prof, n) for c in cards for P in (1, 2, 3, 4) for prof in ("zayif", "orta")]
    with Pool(procs) as p:
        rows = p.map(card_cell, jobs, chunksize=1)
    write_csv(rows, os.path.join(OUT, "cards_payback.csv"))
    for r in rows:
        print(r)


def cmd_selftest(n=300):
    cfg = load_cfg("base")
    base_cfg = sim.load_config(sim.DEFAULT_CONFIG)
    base_cfg["events"]["freeDays"] = 4
    bad = tot = 0
    for P in (1, 3):
        for prof in ("zayif", "orta"):
            for strat in ("hic", "acgozlu", "mantikli"):
                for i in range(n // 12 + 1):
                    s = SEED0 + i * 7919
                    a = EKRun(cfg, P, prof, strat, True, s).run()
                    b = sim.Run(base_cfg, P, prof, strat, True, s).run()
                    tot += 1
                    if (a["finalMoney"], a["dead"], a["income"], a["finalPrestige"]) != (b["finalMoney"], b["dead"], b["income"], b["finalPrestige"]):
                        bad += 1
    print(f"selftest: {tot} koşu, uyuşmazlık {bad}")
    return bad


def _load(variant):
    path = os.path.join(OUT, f"matrix_{variant}.csv")
    if not os.path.exists(path):
        return None
    return {(int(r["P"]), r["profile"], r["strategy"]): r for r in csv.DictReader(open(path, encoding="utf-8"))}


def cmd_report():
    """Rapor tabloları (markdown) — matrix_*.csv'lerden."""
    V = ["base_fixed", "base", "ev", "kart", "paket", "paket_noexcl", "paket_hard", "paket_soft", "paket_fixed"]
    data = {v: _load(v) for v in V}
    data = {v: d for v, d in data.items() if d}
    NL = chr(10)
    print("### Kazanma % (P x profil x strateji)" + NL)
    print("| P | profil | strateji | " + " | ".join(data) + " |")
    print("|---|---|---|" + "---|" * len(data))
    for P in (1, 2, 3, 4):
        for pr in PROFILES:
            for st in STRATS:
                cells = []
                for v, d in data.items():
                    r = d.get((P, pr, st))
                    cells.append(f"{100 * float(r['win']):.1f}" if r else "-")
                print(f"| {P} | {pr} | {st} | " + " | ".join(cells) + " |")
    print(NL + "### Zayıf: Acil Fren bağımlılığı (acgozlu - mantikli kazanma farkı, puan) ve EB'siz hayatta kalma" + NL)
    print("| P | varyant | acgozlu | mantikli | fark | acgozlu_noeb | hic | EB alım (acg) | Taksit alım (acg/man) |")
    print("|---|---|---|---|---|---|---|---|---|")
    for P in (1, 2, 3, 4):
        for v, d in data.items():
            a, m, n, h = (d[(P, "zayif", s)] for s in ("acgozlu", "mantikli", "acgozlu_noeb", "hic"))
            wa, wm, wn, wh = (100 * float(x["win"]) for x in (a, m, n, h))
            print(f"| {P} | {v} | {wa:.1f} | {wm:.1f} | {wa - wm:+.1f} | {wn:.1f} | {wh:.1f} | {float(a['eb_buy']):.2f} | "
                  f"{a.get('own_grace_plus', '-')}/{m.get('own_grace_plus', '-')} |")
    print(NL + "### İflas günü dağılımı (zayıf, tüm stratejiler toplam)" + NL)
    print("| varyant | d4 | d8 | d12 | d16 | toplam kayıp % |")
    print("|---|---|---|---|---|---|")
    for v, d in data.items():
        tot = {k: 0 for k in ("bk_d4", "bk_d8", "bk_d12", "bk_d16")}
        runs = lost = 0
        for (P, pr, st), r in d.items():
            if pr != "zayif":
                continue
            for k in tot:
                tot[k] += int(r[k])
            runs += int(r["runs"])
            lost += float(r["lose"]) * int(r["runs"])
        print(f"| {v} | " + " | ".join(str(tot[k]) for k in tot) + f" | {100 * lost / runs:.1f} |")
    for v in ("paket", "paket_noexcl"):
        if v not in data:
            continue
        print(NL + f"### Yeni kart sahipliği ({v}; koşuların % kaçında alındı)" + NL)
        print("| P | profil | strateji | " + " | ".join(NEWC) + " | bahşiş TL | sabah kutu |")
        print("|---|---|---|" + "---|" * (len(NEWC) + 2))
        for P in (1, 2, 3, 4):
            for pr in PROFILES:
                for st in ("acgozlu", "mantikli"):
                    r = data[v][(P, pr, st)]
                    print(f"| {P} | {pr} | {st} | " + " | ".join(f"{100 * float(r['own_' + c]):.0f}" for c in NEWC)
                          + f" | {r['tip_mean']} | {r['morning_mean']} |")
    print(NL + "### Final kasa medyanı (orta/iyi, mantikli)" + NL)
    print("| P | profil | " + " | ".join(data) + " |")
    print("|---|---|" + "---|" * len(data))
    for P in (1, 2, 3, 4):
        for pr in ("orta", "iyi"):
            print(f"| {P} | {pr} | " + " | ".join(d[(P, pr, 'mantikli')]["final_med"] for d in data.values()) + " |")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("cmd")
    ap.add_argument("variant", nargs="?")
    ap.add_argument("--runs", type=int, default=5000)
    ap.add_argument("--procs", type=int, default=11)
    ap.add_argument("--fixed-offers", action="store_true", help="canlı/sim.py gibi gün-tohumlu sabit teklif")
    a = ap.parse_args()
    if a.cmd == "selftest":
        sys.exit(1 if cmd_selftest() else 0)
    elif a.cmd == "iso":
        cmd_iso(a.runs, a.procs)
    elif a.cmd == "matrix":
        cmd_matrix(a.variant, a.runs, a.procs, orand=not a.fixed_offers)
    elif a.cmd == "cards":
        cmd_cards(a.runs, a.procs, a.variant.split(",") if a.variant else None)
    elif a.cmd == "report":
        cmd_report()
    else:
        raise SystemExit("komut: selftest|iso|matrix|cards|report")


if __name__ == "__main__":
    main()
