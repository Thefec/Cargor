#!/usr/bin/env python3
"""
Cargor 16-gün ekonomi simülasyonu (oyun kodundan BAĞIMSIZ, olay/adım tabanlı).

Kullanım:
  python tools/economy_sim/sim.py                      # tam matris (P × profil × strateji × event)
  python tools/economy_sim/sim.py --quick              # küçük N ile hızlı deneme
  python tools/economy_sim/sim.py --cells P=2,profile=orta,strategy=mantikli,events=on
  python tools/economy_sim/sim.py --set economy.rentGrowthMultiplier=1.15 --tag g115
  python tools/economy_sim/sim.py --payback            # her kartın amortisman koşusu
  python tools/economy_sim/sim.py --event-iso          # her event'in izole etkisi
  python tools/economy_sim/sim.py --sensitivity        # duyarlılık taraması
  python tools/economy_sim/sim.py --figs               # grafikleri (yeniden) üret

Tüm sayılar config.json'dan gelir; bu dosyada oyun sabiti YOKTUR.
Model açıklaması: docs/economy/02-model.md
"""
import argparse
import copy
import csv
import json
import math
import os
import random
import statistics
import sys
import time
from multiprocessing import Pool, cpu_count

HERE = os.path.dirname(os.path.abspath(__file__))
DEFAULT_CONFIG = os.path.join(HERE, "config.json")
RESULTS_DIR = os.path.join(HERE, "results")
DOCS_DIR = os.path.normpath(os.path.join(HERE, "..", "..", "docs", "economy"))
COLORS = ("R", "Y", "B")
# 2026-09-24 Ö-B: Hızlı Hangar respawn/exit + Mesai kapanış payı sim.py'de YERLİ. Harici harness'ler
# (ob_2026_09_24.py) bu bayrağa bakıp kendi takaslarını kapatır (çift uygulama olmasın).
NATIVE_OB = True


# ---------------------------------------------------------------------------
# Config yardımcıları
# ---------------------------------------------------------------------------
def load_config(path, overrides=()):
    with open(path, "r", encoding="utf-8") as f:
        cfg = json.load(f)
    for ov in overrides:
        if "=" not in ov:
            raise SystemExit(f"--set biçimi key.path=value olmalı: {ov}")
        key, val = ov.split("=", 1)
        deep_set(cfg, key, parse_value(val))
    return cfg


def parse_value(s):
    try:
        return json.loads(s)
    except json.JSONDecodeError:
        return s


def deep_set(d, dotted, value):
    parts = dotted.split(".")
    cur = d
    for p in parts[:-1]:
        if isinstance(cur, list):
            cur = cur[int(p)]
        else:
            cur = cur.setdefault(p, {})
    last = parts[-1]
    if isinstance(cur, list):
        cur[int(last)] = value
    else:
        cur[last] = value


def pidx(P):
    return max(0, min(3, P - 1))


# ---------------------------------------------------------------------------
# Tek koşu
# ---------------------------------------------------------------------------
class Run:
    def __init__(self, cfg, P, profile, strategy, events_on, seed,
                 force_cards=None, force_event=None, trace_day=None):
        self.cfg = cfg
        self.eco = cfg["economy"]
        self.pe = cfg["perkEffects"]
        self.asm = cfg["assumptions"]
        self.prof = cfg["profiles"][profile]
        self.strat = cfg["strategies"][strategy]
        self.ups = {u["id"]: u for u in cfg["upgrades"]}
        self.up_order = [u["id"] for u in cfg["upgrades"]]
        self.qcfg = cfg["quests"]
        self.P = P
        self.rng = random.Random(seed)
        self.events_on = events_on
        self.force_cards = list(force_cards or [])
        self.force_event = force_event
        self.dt = cfg["sim"]["dt"]
        self.days = cfg["sim"]["days"]

        e = self.eco
        self.money = int(round(e["startingMoney"] * e["moneyMultiplierPerPlayer"] ** (P - 1)))
        self.prestige = float(e["startingPrestige"])
        self.rent_cycle = 0
        self.grace_left = int(e.get("graceUses", 1))
        self.insurance = False
        self.insurance_consumed = False
        self.levels = {uid: 0 for uid in self.ups}
        self.pending = []  # (uid, level, dayPurchased)
        self.stock = {c: 0 for c in COLORS}
        self.reception = []  # ürün renkleri
        self.dead = None  # ("bankrupt"|"prestige", day)
        self.bulk_pending = False
        self.discount_card = None
        self.quest_tier = 0
        self.quest_active = None
        self.quest_settle = None
        self.yesterday_supply = None
        self.color_bag = []
        self.records = []
        self.calendar = self._gen_calendar() if events_on else {}
        if force_event:
            self.calendar = {d: self._event_by_name(force_event) for d in range(1, self.days + 1)
                             if d % e["rentIntervalDays"] != 0 and d > cfg["events"]["freeDays"]}
        self.trace_day = trace_day
        self.total_spent_upgrades = 0
        self.total_spent_reroll = 0
        self.total_income = 0
        self.total_rent = 0
        self.total_penalty_money = 0

    # ---- yardımcılar ------------------------------------------------------
    def _event_by_name(self, name):
        for ev in self.cfg["events"]["pool"]:
            if ev["name"] == name:
                return ev
        raise SystemExit(f"event bulunamadı: {name}")

    def _gen_calendar(self):
        ec = self.cfg["events"]
        e = self.eco
        rng = random.Random(self.rng.randrange(1, 2 ** 31))
        pool = ec["pool"]
        pos = [x for x in pool if x["type"] == "pos"]
        neg = [x for x in pool if x["type"] == "neg"]
        cal = {}
        day = ec["freeDays"]
        count = 0
        max_day = self.days + 40
        while day < max_day:
            day += rng.randint(ec["intervalMin"], ec["intervalMax"])
            if day in cal:
                continue
            if day % e["rentIntervalDays"] == 0:
                continue
            if count < ec["initialPositiveCount"]:
                ev = pos[rng.randrange(len(pos))]
            elif count == ec["guaranteedNegativeIndex"]:
                ev = neg[rng.randrange(len(neg))]
            else:
                ev = pool[rng.randrange(len(pool))]
            cal[day] = ev
            count += 1
        return cal

    def rent(self, cycle=None):
        e = self.eco
        c = self.rent_cycle if cycle is None else cycle
        growth = e["rentGrowthMultiplier"] - self.pe["cheap_rent_growth_step"] * self.levels["cheap_rent"]
        mult = self.pe["leveraged_rent_mult"] if self.levels["leveraged_rent"] > 0 else 1.0
        return int(round(e["baseRentByPlayerCount"][pidx(self.P)] * growth ** c * mult))

    def next_rent_day(self, day):
        k = self.eco["rentIntervalDays"]
        return ((day - 1) // k + 1) * k

    def day_duration(self, day):
        e = self.eco
        base = e["realDurationInSeconds"] * (self.pe["overtime_mult"] if self.levels["overtime"] > 0 else 1.0)
        extra = max(0, day - e["dynamicDurationStartDay"]) * e["dailyDurationIncrease"]
        return base + extra

    def cost_of(self, uid, level, event_cost_mult):
        u = self.ups[uid]
        base = u["baseCost"] + level * u["costStep"]
        if self.discount_card == uid:
            base = int(round(base * self.pe["bulk_buy_discount"]))
        mult = event_cost_mult
        if not u.get("pExempt"):
            mult *= self.eco["upgradeCostMultiplierByPlayerCount"][pidx(self.P)]
        return int(round(base * mult))

    def visual_level(self, uid):
        lv = self.levels[uid]
        for (pid, plv, _) in self.pending:
            if pid == uid:
                lv = max(lv, plv)
        return lv

    def max_tier(self, day):
        e = self.eco
        if day >= e["t3UnlockDay"]:
            return 2
        if day >= e["t2UnlockDay"]:
            return 1
        return 0

    def eligibility(self, day):
        mt = self.max_tier(day)
        elig = []
        for uid in self.up_order:
            u = self.ups[uid]
            ok = self.visual_level(uid) < u["maxLevel"]
            if u["kind"] == "perk" and u["tier"] > mt:
                ok = False
            if uid == "quest_tier" and not self.qcfg["assets"]:
                ok = False
            elig.append(ok)
        return elig

    def owned_ids(self):
        return {uid for uid in self.ups if self.visual_level(uid) > 0}

    def blocked_by_group(self, uid):
        owned = self.owned_ids()
        for grp in self.eco["exclusiveGroups"]:
            if uid in grp and any(o in owned for o in grp if o != uid):
                return True
        return False

    def select_offer(self, elig, rng):
        pool = [i for i, ok in enumerate(elig) if ok]
        groups = self.eco["exclusiveGroups"]
        used = set()
        result = []
        i = 0
        while i < len(pool) and len(result) < self.eco["draftOfferCount"]:
            j = i + rng.randrange(len(pool) - i)
            pool[i], pool[j] = pool[j], pool[i]
            cand = self.up_order[pool[i]]
            gids = [g for g, grp in enumerate(groups) if cand in grp]
            if any(g in used for g in gids):
                i += 1
                continue
            for g in gids:
                used.add(g)
            result.append(cand)
            i += 1
        return result

    # ---- günlük event çarpanları -------------------------------------------
    def event_mults(self, day):
        ev = self.calendar.get(day)
        m = {"customers": 1.0, "wait": 1.0, "reward": 1.0, "exit": 1.0, "move": 1.0,
             "upgradeCost": 1.0, "penalty": 1.0, "phoneSkip": 1.0, "festival": False, "name": None}
        if ev:
            m["name"] = ev["name"]
            for k in list(m.keys()):
                if k in ev:
                    m[k] = ev[k]
        return m

    # ---- quest ---------------------------------------------------------------
    def quest_offer_and_accept(self, day):
        q = self.qcfg
        assets = q["assets"]
        tier = min(self.quest_tier, 2)
        offered = []
        used = set()
        ys = self.yesterday_supply
        for t in range(tier + 1):
            pool = [a for a in assets if a["tier"] == t and a["id"] not in used]
            if not pool:
                continue
            if ys is None:
                k = 1
            elif tier == 0:
                k = 1
            elif t == 0:
                k = 3
            else:
                k = max(1, tier - t + 1)
            best, best_s = None, -1.0
            for _ in range(k):
                cand = pool[self.rng.randrange(len(pool))]
                s = self.feasibility(cand, ys) if ys is not None else 0.0
                if best is None or s > best_s:
                    best, best_s = cand, s
            offered.append(best)
            used.add(best["id"])
        need = q["dailyOfferCount"] - len(offered)
        rest = [a for a in assets if a["tier"] <= tier and a["id"] not in used]
        for _ in range(need):
            if not rest:
                break
            pick = rest.pop(self.rng.randrange(len(rest)))
            offered.append(pick)
            used.add(pick["id"])
        self.quest_active = None
        if offered and self.rng.random() < self.prof["quest_accept"]:
            def score(a):
                fs = self.feasibility(a, ys) if ys is not None else (1.0 / (a["target"] * (3 if a["colorLocked"] else 1)))
                return fs
            best = max(offered, key=score)
            self.quest_active = {"asset": best, "progress": 0, "color": self.rng.choice(COLORS) if best["colorLocked"] else None}

    def feasibility(self, a, ys):
        supply = ys.get(a["type"], 0)
        eff = a["target"] * (3 if a["colorLocked"] else 1)
        return supply / eff if eff > 0 else 0.0

    def quest_progress(self, qtype, color=None):
        qa = self.quest_active
        if not qa:
            return
        a = qa["asset"]
        if a["type"] != qtype:
            return
        if a["colorLocked"] and color != qa["color"]:
            return
        qa["progress"] += 1

    def quest_settle_day(self, day):
        qa = self.quest_active
        if not qa:
            self.quest_settle = None
            return None
        a = qa["asset"]
        tr = self.qcfg["tierRewards"][str(a["tier"])]
        if qa["progress"] >= a["target"]:
            self.quest_settle = (tr["money"], tr["prestige"], True)
        else:
            self.quest_settle = (-tr["moneyPen"], -tr["prestigePen"], False)
        return self.quest_settle

    # ---- prestij / para --------------------------------------------------
    def add_prestige(self, delta):
        raw = self.prestige + delta
        if raw <= 0 and self.dead is None:
            self.dead = ("prestige", self.day)
        self.prestige = max(0.0, min(self.eco["maxPrestige"], raw))

    def add_money(self, delta):
        self.money = max(0, self.money + int(delta))

    # ---- ana döngü -------------------------------------------------------
    def run(self):
        for day in range(1, self.days + 1):
            self.day = day
            self.activate_pending(day)
            self.simulate_day(day)
            if self.dead:
                break
        return self.summary()

    def activate_pending(self, day):
        keep = []
        for (uid, lv, dp) in self.pending:
            if dp < day:
                self.levels[uid] = max(self.levels[uid], lv)
                if uid == "emergency_brake" and not self.insurance_consumed:
                    self.insurance = True
                if uid == "quest_tier":
                    self.quest_tier = lv
                if uid == "bulk_buy":
                    self.bulk_pending = True
            else:
                keep.append((uid, lv, dp))
        self.pending = keep

    def simulate_day(self, day):
        e, pf, asm, pe = self.eco, self.prof, self.asm, self.pe
        P = self.P
        rng = self.rng
        T = self.day_duration(day)
        sec_per_hour = T / (e["endHour"] - e["startHour"])
        m = self.event_mults(day)
        pen_mult = m["penalty"]

        # quest ödeme (dünkü)
        quest_money = 0
        if self.quest_settle:
            qm, qp, _ = self.quest_settle
            self.add_money(qm)
            self.add_prestige(qp)
            quest_money = qm
            self.quest_settle = None
        # festival
        festival = 0
        if m["festival"]:
            lo, hi = e["festivalRentShare"]
            festival = int(round(rng.uniform(lo, hi) * self.rent()))
            self.add_money(festival)
        # quest teklifi
        self.quest_offer_and_accept(day)

        # türevler
        quota = max(e["minCustomersPerDay"], min(e["maxCustomersPerDay"], int(round(e["dailyCustomerCount"][pidx(P)][min(day, 16) - 1] * m["customers"]))))
        pmin = max(e["patienceFloorMin"], e["baseMinPatience"] - (P - 1) * e["patienceReductionPerPlayer"]) * m["wait"]
        pmax = max(e["patienceFloorMax"], e["baseMaxPatience"] - (P - 1) * e["patienceReductionPerPlayer"]) * m["wait"]
        arrival = e["customerArrivalIntervalByPlayerCount"][pidx(P)]
        R = e["rewardPerBoxByPlayerCount"][pidx(P)]
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
        exit_delay = e["exitDelay"] * m["exit"]
        respawn_mult = 1.0
        if self.levels["fast_hangar"]:
            # Ö-B 2026-09-24 (canlı): Truck kalkış beklemesi = max(alt sınır, exitDelay × çarpan),
            # TruckSpawner respawn gecikmesi × çarpan. Event çarpanı önce, perk sonra (oyundaki sıra).
            exit_delay = max(pe.get("fast_hangar_exit_min", 0.0), exit_delay * pe.get("fast_hangar_exit_mult", 1.0))
            respawn_mult = pe.get("fast_hangar_respawn_mult", 1.0)
        day_end_grace = e["dayEndGraceSeconds"] + (pe.get("overtime_grace_add", 0.0) if self.levels["overtime"] else 0.0)
        n_hangars = e["hangarsAtStart"] + self.levels["extra_hangar"]
        # packing_station normalde 2. paketleme masasını açar. Öneri varyantı
        # (perkEffects.packing_station_grants_station=1) bunun yerine 2. SERVİS
        # istasyonunu açar — kartın zararlıdan faydalıya çevrilmesi önerisinin testi.
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
        dual_day = day >= e["dualItemUnlockDay"]
        return_day = day >= e["returnUnlockDay"]
        mixed_day = day >= e["mixedTruckUnlockDay"]

        def t_serve(dual, is_return):
            base = pf["t_serve"] - e["interactionTime"]
            im = inter
            if dual:
                im *= (e["dualItemBoxRequestMult"] * 2 if is_return else e["dualItemInteractionMult"])
            return (base + im) * labor_mult + pf["reaction"]

        t_pack = pf["t_pack"] * labor_mult + pf["reaction"]
        t_load = pf["t_load"] * labor_mult + pf["reaction"]
        t_phone = pf["t_phone"] * labor_mult + pf["reaction"]

        # günlük sayaçlar
        st = {"quota": quota, "spawned": 0, "served": 0, "lost": 0, "missed": 0, "wrongProduct": 0,
              "packed": 0, "delivered": 0, "trucksSpawned": 0, "trucksFull": 0, "phone": 0,
              "wrongDelivery": 0, "drops": 0, "income": 0, "penaltyMoney": 0, "returnServed": 0,
              "productsSupplied": 0, "earlyEnd": False, "idleSec": 0.0, "dayLen": T, "event": m["name"],
              "shopSpent": 0, "rerollSpent": 0, "bought": []}
        supply_counts = {"shelf": 0, "truck": 0, "pack": 0, "phone": 0}

        # varlıklar
        customers = []  # dict
        queue = []
        n_stations = max(1, int(e.get("serviceStations", 1)) + extra_station)
        stations = [None] * n_stations
        players = [{"busy": 0.0, "task": None} for _ in range(P)]
        tables_free_at = [0.0] * n_tables
        trucks = [{"state": "none", "until": 0.0, "need": {}, "reserved": {}, "delivered": 0, "cargo": 0, "timer_end": 0.0, "color": None} for _ in range(n_hangars)]
        phone_cd = 0.0
        next_spawn_e = 0.0
        t = 0.0  # gerçek sn (sayaçlar)
        el = 0.0  # gün saati (skip dahil)
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
            if not self.color_bag:
                self.color_bag = list(COLORS)
                rng.shuffle(self.color_bag)
            return self.color_bag.pop()

        def spawn_truck(tr):
            cargo = rng.randrange(cmin, cmax)
            need = {}
            if mixed_day and cargo >= 2:
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
            mode = "return" if (return_day and rng.random() < e["returnModeChance"]) else "supply"
            c = {"arrive": t + asm["customerWalkInSeconds"], "patience": rng.uniform(pmin, pmax), "queued_at": None,
                 "mode": mode, "dual": dual_day, "colors": [rng.choice(COLORS), rng.choice(COLORS)],
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

        trace = (self.trace_day == day)

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
            if trace:
                print(f"  t={t:6.1f} el={el:6.1f} h={hour_of(el):5.2f} LEAVE lost={lost} mode={c['mode']} served_by={c['served_by']} q={len(queue)} prestige={self.prestige:.2f}")

        # ---- zaman döngüsü ------------------------------------------------
        dt = self.dt
        while el < T and self.dead is None:
            hour = hour_of(el)

            # 17:30 kuyruk boşaltma + kota cezası
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

            # müşteri spawn
            if (e["spawnStartHour"] <= hour <= e["spawnEndHour"] and st["spawned"] < quota
                    and len(queue) + sum(1 for c in customers if c["state"] == "walking") < e["maxQueueSize"]
                    and len(queue) < wave(hour)["maxCustomers"] and el >= next_spawn_e):
                spawn_customer()

            # müşteri durumları
            for c in customers:
                if c["state"] == "walking" and t >= c["arrive"]:
                    c["state"] = "queued"
                    c["queued_at"] = t
                    queue.append(c)
                elif c["state"] == "queued" and c["served_by"] is None and t - c["queued_at"] >= c["patience"]:
                    customer_leave(c, lost=True)
            # Boş istasyonlara kuyruk sırasına göre müşteri ata (canlı kod:
            # CustomerManager.AssignFreeServiceStations; sahnede fiilen 1 istasyon dolu).
            for c in queue:
                if c["state"] != "queued" or c in stations:
                    continue
                for si in range(n_stations):
                    if stations[si] is None:
                        stations[si] = c
                        break
                else:
                    break

            # tır durumları
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
                elif s == "waiting":
                    if hour >= e["truckEndHour"] or t >= tr["timer_end"]:
                        truck_leave(tr, full=False)
                elif s == "exiting":
                    if t >= tr["until"]:
                        tr["state"] = "respawn"
                        tr["until"] = t + rng.uniform(*e["respawnDelayRange"]) * respawn_mult
                        # tırdaki rezerve kutular stoğa döner
                        for c in COLORS:
                            self.stock[c] += tr["reserved"].get(c, 0)
                            tr["reserved"][c] = 0
                elif s == "respawn":
                    if t >= tr["until"]:
                        tr["state"] = "none"
                        if not trucks_open:
                            tr["state"] = "closed"

            # alışveriş (günde 1 kez, panel açılınca)
            if not shop_done and hour >= e["panelOpenHour"]:
                shop_done = True
                if self.strat.get("buy") or self.force_cards:
                    spent = self.shop(day, m["upgradeCost"], st)
                    if spent > 0:
                        # Bir oyuncu ofise gider: YALNIZ meşguliyet eklenir, mevcut görevi
                        # EZİLMEZ (ezilirse o görevin tamamlanma bloğu hiç çalışmaz ve
                        # servis edilen müşteri kalıcı olarak "servis ediliyor" sanılıp
                        # kuyruğu kilitler — 2026-09-18 trace'inde yakalanan model bug'ı).
                        shopper = min(players, key=lambda p: p["busy"])
                        shopper["busy"] = max(shopper["busy"], t) + asm["shopTripSeconds"] * labor_mult

            # oyuncular
            for pi, pl in enumerate(players):
                if pl["busy"] > t:
                    continue
                # görev tamamlama
                task = pl["task"]
                pl["task"] = None
                if task:
                    kind = task[0]
                    if kind == "serve":
                        c = task[1]
                        if c["state"] == "queued":
                            c["served_by"] = None
                            if c["mode"] == "supply":
                                if rng.random() < pf["p_wrongProduct"]:
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
                            else:
                                st["served"] += 1
                                st["returnServed"] += 1
                                self.add_prestige(served_bonus)
                                customer_leave(c, lost=False)
                    elif kind == "load":
                        tr, col = task[1], task[2]
                        tr["reserved"][col] -= 1
                        if tr["state"] == "waiting" and tr["need"].get(col, 0) > 0:
                            r = rng.random()
                            if r < pf["p_wrongDelivery"]:
                                st["wrongDelivery"] += 1
                                pen = int(round(penalty_box * pen_mult))
                                self.add_money(-pen)
                                st["penaltyMoney"] += pen
                                self.add_prestige(e["wrongDeliveryPrestigePenalty"] * pen_mult)
                            elif r < pf["p_wrongDelivery"] + pf["p_drop"]:
                                st["drops"] += 1
                                pen = int(round(e["boxDropMoneyPenalty"] * pen_mult))
                                self.add_money(-pen)
                                st["penaltyMoney"] += pen
                                self.add_prestige(e["boxDropPrestigePenalty"] * pen_mult)
                            else:
                                rw = reward_now()
                                self.add_money(rw)
                                st["income"] += rw
                                st["delivered"] += 1
                                tr["need"][col] -= 1
                                tr["delivered"] += 1
                                if sum(tr["need"].values()) <= 0:
                                    truck_leave(tr, full=True)
                        else:
                            self.stock[col] += 1  # tır gitti, kutu rafa geri
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

                # yeni görev seç
                if self.dead:
                    break
                if trace and t % 10 == 0:
                    print(f"  t={t:6.1f} el={el:6.1f} h={hour:5.2f} p{pi} idle "
                          f"stations={[(s['served_by'] if s else None) for s in stations]} q={len(queue)} "
                          f"recep={len(self.reception)} stock={self.stock} trucks={[(x['state'], x['need']) for x in trucks]}")
                # (a) servis — boş istasyondaki, henüz kimsenin servis etmediği müşteri
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
                # (b) tır yükleme
                loaded = False
                for tr in trucks:
                    if tr["state"] != "waiting":
                        continue
                    for col, n in tr["need"].items():
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
                # (c) paketleme
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
                # (d) telefon
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

            # erken bitiş
            if early_end_t is None and st["spawned"] >= quota and not queue and not any(c["state"] == "walking" for c in customers):
                early_end_t = t + day_end_grace
            if early_end_t is not None and t >= early_end_t:
                st["earlyEnd"] = True
                el = T
                break

            t += dt
            el += dt

        # ---- gün sonu ------------------------------------------------------
        for tr in trucks:
            for c in COLORS:
                self.stock[c] += tr["reserved"].get(c, 0)
        # Kabul edilmiş ama yürüyen görevler kaybolur (tırlar despawn)
        self.reception = self.reception  # ürünler masada kalır (VARSAYIM: sahnede kalır)
        self.yesterday_supply = dict(supply_counts)
        qres = self.quest_settle_day(day)

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
                # graceZeroPctLive (2026-09-23, varsayılan False = eski davranış): canlı kodda
                # all_in/leveraged_rent grace'i SİLMİYOR, gracePaymentPercent=0 yazıyor →
                # DayCycleManager.TryProcessMoneyCheck grace dalı %0 alıp kirayı ödenmiş sayıyor.
                gp = 0.0 if (self.levels["leveraged_rent"] or self.levels["all_in"]) else e["gracePaymentPercent"]
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
            else:
                gate = "bankrupt"
                self.dead = ("bankrupt", day)
        self.total_income += st["income"]
        self.total_rent += rent_paid
        self.total_penalty_money += st["penaltyMoney"]
        st.update({"day": day, "moneyEnd": self.money, "prestigeEnd": round(self.prestige, 2), "rentPaid": rent_paid,
                   "gate": gate, "quest": (qres[2] if qres else None), "questMoney": quest_money, "festival": festival,
                   "stockEnd": sum(self.stock.values()), "receptionEnd": len(self.reception),
                   "levels": {k: v for k, v in self.levels.items() if v}})
        self.records.append(st)

    # ---- alışveriş -----------------------------------------------------------
    def shop(self, day, event_cost_mult, st):
        spent = 0
        e = self.eco
        rng_offer = random.Random(day * 73856093)
        elig = self.eligibility(day)
        offer = self.select_offer(elig, rng_offer)
        # bulk-buy indirimi
        self.discount_card = None
        if self.bulk_pending and offer:
            self.discount_card = self.rng.choice(offer)
            self.bulk_pending = False

        # Ö-A (canlı 2026-09-24): alım sonrası kasa >= upgradeRentReserveFraction × sıradaki kira
        # olmalı; rentReserveExempt (Acil Fren) muaf. Zorlanmış alımlar dahil.
        guard_frac = e.get("upgradeRentReserveFraction", 0.0)
        exempt = set(e.get("rentReserveExempt") or [])
        guard_res = int(guard_frac * self.rent()) if guard_frac > 0 else 0

        def try_buy(uid, reserve):
            nonlocal spent
            lv = self.visual_level(uid)
            if lv >= self.ups[uid]["maxLevel"] or self.blocked_by_group(uid):
                return False
            cost = self.cost_of(uid, lv, event_cost_mult)
            if uid not in exempt:
                reserve = max(reserve, guard_res)
            if self.money - cost < reserve:
                return False
            self.money -= cost
            spent += cost
            self.pending.append((uid, lv + 1, day))
            st["bought"].append(uid)
            if self.discount_card == uid:
                self.discount_card = None
            return True

        # zorlanmış kartlar (amortisman koşuları): teklife bakılmaz
        for uid in list(self.force_cards):
            if try_buy(uid, 0):
                self.force_cards.remove(uid)
        if not self.strat.get("buy"):
            self.total_spent_upgrades += spent
            st["shopSpent"] += spent
            return spent

        next_rent = self.rent()
        reserve = int(self.strat["reserveFrac"] * next_rent)
        mode = self.strat["mode"]
        # Kilitten muaf kartlar (Acil Fren) teklifte varsa ve strateji onu istiyorsa ÖNCE, stratejinin
        # kendi rezerviyle alınır (sifirdan_2026_09_23 guardExempt ön-geçişinin sim.py karşılığı).
        if guard_frac > 0:
            prio_list = self.strat.get("priority")
            for uid in [u for u in offer if u in exempt]:
                if prio_list is not None and uid not in prio_list:
                    continue
                try_buy(uid, reserve)
        if mode == "cheapest_first":
            for uid in sorted(offer, key=lambda u: self.cost_of(u, self.visual_level(u), event_cost_mult)):
                try_buy(uid, reserve)
        elif mode == "priority":
            prio = self.strat["priority"]
            top = prio[:8]
            rerolls = 0
            reroll_max = self.strat.get("rerollMaxPerDay", 0) if self.strat.get("reroll") else 0
            while True:
                bought_any = False
                for uid in prio:
                    if uid in offer and try_buy(uid, reserve):
                        bought_any = True
                if bought_any or rerolls >= reroll_max:
                    break
                # teklifte öncelikli kart yoksa ve para bolsa reroll
                if not any(u in top for u in offer):
                    rc = int(round(e["rerollCosts"][min(rerolls, len(e["rerollCosts"]) - 1)] * e["upgradeCostMultiplierByPlayerCount"][pidx(self.P)]))
                    if self.money - rc >= max(reserve, guard_res) + next_rent * 0.5:
                        self.money -= rc
                        self.total_spent_reroll += rc
                        st["rerollSpent"] += rc
                        rerolls += 1
                        offer = self.select_offer(self.eligibility(day), self.rng)
                        continue
                break
        self.total_spent_upgrades += spent
        st["shopSpent"] += spent
        return spent

    def summary(self):
        return {"records": self.records, "dead": self.dead, "finalMoney": self.money, "finalPrestige": round(self.prestige, 2),
                "income": self.total_income, "rent": self.total_rent, "upgrades": self.total_spent_upgrades,
                "reroll": self.total_spent_reroll, "penalties": self.total_penalty_money,
                "levels": {k: v for k, v in self.levels.items() if v}}


# ---------------------------------------------------------------------------
# Hücre koşucu (multiprocessing)
# ---------------------------------------------------------------------------
def run_cell(args):
    cfg, P, profile, strategy, events_on, runs, seed, force_cards, force_event = args
    outs = []
    for i in range(runs):
        r = Run(cfg, P, profile, strategy, events_on, seed + i * 7919, force_cards, force_event)
        outs.append(r.run())
    return aggregate(outs, cfg["sim"]["days"])


def pct(vals, q):
    if not vals:
        return 0.0
    s = sorted(vals)
    k = (len(s) - 1) * q
    f = math.floor(k)
    c = min(f + 1, len(s) - 1)
    return s[f] + (s[c] - s[f]) * (k - f)


def aggregate(outs, days):
    n = len(outs)
    by_day = []
    for d in range(days):
        money, prest, inc, served, lost, missed, quota, packed, deliv, phone, trucks, idle, early, spawned, rent = ([] for _ in range(15))
        alive = 0
        for o in outs:
            recs = o["records"]
            if d < len(recs):
                r = recs[d]
                alive += 1
                money.append(r["moneyEnd"]); prest.append(r["prestigeEnd"]); inc.append(r["income"])
                served.append(r["served"]); lost.append(r["lost"]); missed.append(r["missed"]); quota.append(r["quota"])
                packed.append(r["packed"]); deliv.append(r["delivered"]); phone.append(r["phone"]); trucks.append(r["trucksFull"])
                idle.append(r["idleSec"] / max(1.0, r["dayLen"])); early.append(1 if r["earlyEnd"] else 0)
                spawned.append(r["spawned"]); rent.append(r["rentPaid"])
        if not money:
            by_day.append(None)
            continue
        by_day.append({
            "day": d + 1, "alive": alive / n,
            "money_mean": statistics.mean(money), "money_p10": pct(money, 0.10), "money_p90": pct(money, 0.90),
            "prestige_mean": statistics.mean(prest), "prestige_p10": pct(prest, 0.10),
            "income_mean": statistics.mean(inc), "served_mean": statistics.mean(served), "lost_mean": statistics.mean(lost),
            "missed_mean": statistics.mean(missed), "quota": statistics.mean(quota), "spawned_mean": statistics.mean(spawned),
            "packed_mean": statistics.mean(packed), "delivered_mean": statistics.mean(deliv), "phone_mean": statistics.mean(phone),
            "trucksFull_mean": statistics.mean(trucks), "idle_frac": statistics.mean(idle), "early_frac": statistics.mean(early),
            "rent_mean": statistics.mean(rent),
        })
    deaths = {}
    for o in outs:
        if o["dead"]:
            key = f"{o['dead'][0]}@{o['dead'][1]}"
            deaths[key] = deaths.get(key, 0) + 1
    finals = [o["finalMoney"] for o in outs]
    fp = [o["finalPrestige"] for o in outs]
    surv = [o for o in outs if not o["dead"]]
    lvl_counts = {}
    for o in outs:
        for k, v in o["levels"].items():
            lvl_counts[k] = lvl_counts.get(k, 0) + 1
    return {
        "runs": n, "byDay": by_day, "deaths": deaths,
        "deathRate": sum(1 for o in outs if o["dead"]) / n,
        "bankruptRate": sum(1 for o in outs if o["dead"] and o["dead"][0] == "bankrupt") / n,
        "prestigeDeathRate": sum(1 for o in outs if o["dead"] and o["dead"][0] == "prestige") / n,
        "finalMoney_mean": statistics.mean(finals), "finalMoney_p10": pct(finals, 0.1), "finalMoney_p90": pct(finals, 0.9),
        "finalPrestige_mean": statistics.mean(fp), "finalPrestige_p10": pct(fp, 0.1),
        "income_total_mean": statistics.mean(o["income"] for o in outs),
        "rent_total_mean": statistics.mean(o["rent"] for o in outs),
        "upgrades_total_mean": statistics.mean(o["upgrades"] for o in outs),
        "reroll_total_mean": statistics.mean(o["reroll"] for o in outs),
        "penalties_total_mean": statistics.mean(o["penalties"] for o in outs),
        "ownership": {k: v / n for k, v in sorted(lvl_counts.items(), key=lambda kv: -kv[1])},
    }


# ---------------------------------------------------------------------------
# Senaryo matrisi
# ---------------------------------------------------------------------------
def cell_name(P, profile, strategy, events_on):
    return f"P{P}_{profile}_{strategy}_{'ev' if events_on else 'noev'}"


def run_matrix(cfg, cells, runs, procs, force_cards=None, force_event=None, label=None):
    seed = cfg["sim"]["seed"]
    jobs = [(cfg, P, prof, strat, ev, runs, seed, force_cards, force_event) for (P, prof, strat, ev) in cells]
    t0 = time.time()
    if procs > 1 and len(jobs) > 1:
        with Pool(procs) as pool:
            res = pool.map(run_cell, jobs)
    else:
        res = [run_cell(j) for j in jobs]
    out = {cell_name(*c): r for c, r in zip(cells, res)}
    print(f"[{label or 'matrix'}] {len(cells)} hücre × {runs} koşu — {time.time() - t0:.1f} s", file=sys.stderr)
    return out


def parse_cells(spec, cfg):
    Ps = [1, 2, 3, 4]
    profs = list(cfg["profiles"].keys())
    strats = list(cfg["strategies"].keys())
    evs = [True, False]
    if spec:
        for kv in spec.split(","):
            k, v = kv.split("=")
            if k == "P":
                Ps = [int(x) for x in v.split("/")]
            elif k == "profile":
                profs = v.split("/")
            elif k == "strategy":
                strats = v.split("/")
            elif k == "events":
                evs = [x == "on" for x in v.split("/")]
    return [(P, pr, s, e) for P in Ps for pr in profs for s in strats for e in evs]


def write_matrix_csv(res, path):
    with open(path, "w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow(["cell", "runs", "deathRate", "bankruptRate", "prestigeDeathRate", "finalMoney_mean", "finalMoney_p10",
                    "finalMoney_p90", "finalPrestige_mean", "income_total", "rent_total", "upgrades_total", "reroll_total",
                    "penalties_total", "deaths"])
        for name, r in res.items():
            w.writerow([name, r["runs"], f"{r['deathRate']:.3f}", f"{r['bankruptRate']:.3f}", f"{r['prestigeDeathRate']:.3f}",
                        f"{r['finalMoney_mean']:.0f}", f"{r['finalMoney_p10']:.0f}", f"{r['finalMoney_p90']:.0f}",
                        f"{r['finalPrestige_mean']:.1f}", f"{r['income_total_mean']:.0f}", f"{r['rent_total_mean']:.0f}",
                        f"{r['upgrades_total_mean']:.0f}", f"{r['reroll_total_mean']:.0f}", f"{r['penalties_total_mean']:.0f}",
                        json.dumps(r["deaths"])])


def write_daily_csv(res, path):
    with open(path, "w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        keys = ["day", "alive", "money_mean", "money_p10", "money_p90", "prestige_mean", "prestige_p10", "income_mean",
                "quota", "spawned_mean", "served_mean", "lost_mean", "missed_mean", "packed_mean", "delivered_mean",
                "phone_mean", "trucksFull_mean", "idle_frac", "early_frac", "rent_mean"]
        w.writerow(["cell"] + keys)
        for name, r in res.items():
            for d in r["byDay"]:
                if d is None:
                    continue
                w.writerow([name] + [f"{d[k]:.2f}" if isinstance(d[k], float) else d[k] for k in keys])


# ---------------------------------------------------------------------------
# Grafikler
# ---------------------------------------------------------------------------
def make_figs(res, out_dir, tag=""):
    try:
        import matplotlib
        matplotlib.use("Agg")
        import matplotlib.pyplot as plt
    except ImportError:
        print("matplotlib yok — grafik atlandı", file=sys.stderr)
        return []
    os.makedirs(out_dir, exist_ok=True)
    figs = []
    colors = {1: "#1f77b4", 2: "#ff7f0e", 3: "#2ca02c", 4: "#d62728"}

    def daily(name, key):
        r = res.get(name)
        if not r:
            return None, None
        xs = [d["day"] for d in r["byDay"] if d]
        ys = [d[key] for d in r["byDay"] if d]
        return xs, ys

    # 1. gün-para (orta / mantıklı / event on)
    for metric, fname, ylabel in (("money_mean", "fig-money-by-day", "Kasa (TL, gün sonu, kira sonrası)"),
                                  ("prestige_mean", "fig-prestige-by-day", "Prestij (gün sonu)")):
        fig, ax = plt.subplots(figsize=(9, 5))
        any_line = False
        for P in (1, 2, 3, 4):
            name = cell_name(P, "orta", "mantikli", True)
            xs, ys = daily(name, metric)
            if xs:
                any_line = True
                ax.plot(xs, ys, marker="o", color=colors[P], label=f"{P}P ortalama")
                xs2, lo = daily(name, metric.replace("mean", "p10"))
                if lo:
                    ax.plot(xs2, lo, linestyle="--", color=colors[P], alpha=0.6, label=f"{P}P alt %10")
        if any_line:
            for rd in (4, 8, 12, 16):
                ax.axvline(rd, color="gray", alpha=0.25, linestyle=":")
            ax.set_xlabel("Gün"); ax.set_ylabel(ylabel); ax.grid(alpha=0.3); ax.legend(fontsize=8, ncol=2)
            ax.set_title(f"{ylabel} — orta beceri, mantıklı strateji, event'li {tag}")
            p = os.path.join(out_dir, f"{fname}{tag}.png"); fig.tight_layout(); fig.savefig(p, dpi=130); figs.append(p)
        plt.close(fig)

    # 2. oyuncu sayısı karşılaştırma: final kasa (profil × P), mantıklı, event on
    fig, ax = plt.subplots(figsize=(9, 5))
    profs = ["zayif", "orta", "iyi"]
    width = 0.25
    any_bar = False
    for i, pr in enumerate(profs):
        vals = []
        for P in (1, 2, 3, 4):
            r = res.get(cell_name(P, pr, "mantikli", True))
            vals.append(r["finalMoney_mean"] if r else 0)
        if any(vals):
            any_bar = True
            ax.bar([P + (i - 1) * width for P in (1, 2, 3, 4)], vals, width, label=pr)
    if any_bar:
        ax.set_xticks([1, 2, 3, 4]); ax.set_xlabel("Oyuncu sayısı"); ax.set_ylabel("Gün 16 kasa (TL, ort.)")
        ax.set_title(f"Oyuncu sayısı × beceri — final kasa (mantıklı, event'li) {tag}"); ax.grid(axis="y", alpha=0.3); ax.legend()
        p = os.path.join(out_dir, f"fig-playercount-compare{tag}.png"); fig.tight_layout(); fig.savefig(p, dpi=130); figs.append(p)
    plt.close(fig)

    # 3. iflas ısı haritası (profil×strateji satır, P sütun), event on
    rows = [(pr, s) for pr in profs for s in ("hic", "acgozlu", "mantikli")]
    mat = []
    for (pr, s) in rows:
        mat.append([res.get(cell_name(P, pr, s, True), {}).get("deathRate", float("nan")) for P in (1, 2, 3, 4)])
    if any(not math.isnan(v) for row in mat for v in row):
        fig, ax = plt.subplots(figsize=(7, 6))
        im = ax.imshow(mat, cmap="Reds", vmin=0, vmax=1, aspect="auto")
        ax.set_xticks(range(4)); ax.set_xticklabels(["1P", "2P", "3P", "4P"])
        ax.set_yticks(range(len(rows))); ax.set_yticklabels([f"{pr}/{s}" for pr, s in rows])
        for i, row in enumerate(mat):
            for j, v in enumerate(row):
                if not math.isnan(v):
                    ax.text(j, i, f"{v*100:.0f}%", ha="center", va="center", color="black" if v < 0.5 else "white", fontsize=9)
        fig.colorbar(im, ax=ax, label="Kayıp oranı (iflas + prestij)")
        ax.set_title(f"Kayıp oranı — event'li {tag}")
        p = os.path.join(out_dir, f"fig-bankruptcy-heatmap{tag}.png"); fig.tight_layout(); fig.savefig(p, dpi=130); figs.append(p)
        plt.close(fig)
    return figs


def make_payback_fig(pb, out_dir, tag=""):
    try:
        import matplotlib
        matplotlib.use("Agg")
        import matplotlib.pyplot as plt
    except ImportError:
        return []
    fig, ax = plt.subplots(figsize=(10, 6))
    names = list(pb.keys())
    vals = [min(60, pb[n]["payback_days"]) if pb[n]["payback_days"] is not None else 60 for n in names]
    cols = ["#2ca02c" if v <= 8 else ("#ff7f0e" if v <= 15 else "#d62728") for v in vals]
    ax.barh(names, vals, color=cols)
    ax.set_xlabel("Amortisman (gün) — 60 = hiç amorti olmuyor / negatif")
    ax.set_title(f"Kart amortisman süresi (2P orta, gün 1 alım, event'siz) {tag}")
    ax.grid(axis="x", alpha=0.3)
    p = os.path.join(out_dir, f"fig-upgrade-payback{tag}.png"); fig.tight_layout(); fig.savefig(p, dpi=130)
    plt.close(fig)
    return [p]


# ---------------------------------------------------------------------------
# Özel koşular
# ---------------------------------------------------------------------------
def payback_runs(cfg, runs, procs, P=2, profile="orta"):
    """Her kart tek başına gün 1'de (aktivasyon gün 2) satın alınır; strateji 'hic'. Event'siz."""
    base = run_matrix(cfg, [(P, profile, "hic", False)], runs, 1, label="payback-base")
    base_r = base[cell_name(P, profile, "hic", False)]
    jobs = []
    ids = [u["id"] for u in cfg["upgrades"]]
    for uid in ids:
        u = cfg["upgrades"][ids.index(uid)]
        cards = [uid] * u["maxLevel"]  # tüm seviyeler
        jobs.append((cfg, P, profile, "hic", False, runs, cfg["sim"]["seed"], cards, None))
    with Pool(procs) as pool:
        res = pool.map(run_cell, jobs)
    out = {}
    for uid, r in zip(ids, res):
        u = cfg["upgrades"][ids.index(uid)]
        cost_total = r["upgrades_total_mean"]
        d_income = (r["income_total_mean"] - base_r["income_total_mean"])
        d_rent = -(r["rent_total_mean"] - base_r["rent_total_mean"])
        d_pen = -(r["penalties_total_mean"] - base_r["penalties_total_mean"])
        gain = d_income + d_rent + d_pen
        days_active = cfg["sim"]["days"] - 1
        per_day = gain / days_active
        pb = (cost_total / per_day) if per_day > 0 else None
        out[u["name"]] = {"id": uid, "cost": round(cost_total), "gain_total": round(gain), "gain_per_day": round(per_day, 1),
                          "payback_days": (round(pb, 1) if pb is not None else None),
                          "finalMoney": round(r["finalMoney_mean"]), "base_finalMoney": round(base_r["finalMoney_mean"]),
                          "finalPrestige": r["finalPrestige_mean"], "deathRate": r["deathRate"]}
    return out


def event_iso_runs(cfg, runs, procs, P=2, profile="orta", strategy="mantikli"):
    base = run_matrix(cfg, [(P, profile, strategy, False)], runs, 1, label="event-base")[cell_name(P, profile, strategy, False)]
    names = [ev["name"] for ev in cfg["events"]["pool"]]
    jobs = [(cfg, P, profile, strategy, False, runs, cfg["sim"]["seed"], None, nm) for nm in names]
    with Pool(procs) as pool:
        res = pool.map(run_cell, jobs)
    out = {}
    n_days = sum(1 for d in range(cfg["events"]["freeDays"] + 1, cfg["sim"]["days"] + 1) if d % cfg["economy"]["rentIntervalDays"] != 0)
    for nm, r in zip(names, res):
        out[nm] = {"event_days": n_days,
                   "d_finalMoney_per_day": round((r["finalMoney_mean"] - base["finalMoney_mean"]) / n_days, 1),
                   "d_income_per_day": round((r["income_total_mean"] - base["income_total_mean"]) / n_days, 1),
                   "d_finalPrestige_per_day": round((r["finalPrestige_mean"] - base["finalPrestige_mean"]) / n_days, 2),
                   "deathRate": r["deathRate"], "base_deathRate": base["deathRate"]}
    return out


def sensitivity_runs(cfg, runs, procs):
    cells = [(P, "orta", "mantikli", True) for P in (1, 2, 3, 4)]
    variants = {
        "base": [],
        "t_pack-30%": ["profiles.orta.t_pack=18"], "t_pack+30%": ["profiles.orta.t_pack=34"],
        "phone_use=0": ["profiles.orta.phone_use=0"], "phone_use=1": ["profiles.orta.phone_use=1"],
        "S_pack=3": ["profiles.orta.S_pack=3"], "S_pack=10": ["profiles.orta.S_pack=10"],
        "wrongDeliv=0": ["profiles.orta.p_wrongDelivery=0"], "wrongDeliv=0.2": ["profiles.orta.p_wrongDelivery=0.2"],
        "rentGrowth=1.10": ["economy.rentGrowthMultiplier=1.10"], "rentGrowth=1.35": ["economy.rentGrowthMultiplier=1.35"],
        "hangarStay×0.75": ["economy.hangarStayDurationByPlayerCount=[90,45,30,22]"],
        "arrival×0.7": ["economy.customerArrivalIntervalByPlayerCount=[31,15.4,14.7,14.7]"],
    }
    out = {}
    for name, ovs in variants.items():
        c2 = copy.deepcopy(cfg)
        for ov in ovs:
            k, v = ov.split("=", 1)
            deep_set(c2, k, parse_value(v))
        r = run_matrix(c2, cells, runs, procs, label=f"sens:{name}")
        out[name] = {cn: {"finalMoney": round(v["finalMoney_mean"]), "deathRate": v["deathRate"],
                          "finalPrestige": v["finalPrestige_mean"]} for cn, v in r.items()}
    return out


# ---------------------------------------------------------------------------
def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--config", default=DEFAULT_CONFIG)
    ap.add_argument("--set", action="append", default=[], help="key.path=value override (tekrarlanabilir)")
    ap.add_argument("--runs", type=int, default=None)
    ap.add_argument("--cells", default=None, help="P=1/2,profile=orta,strategy=mantikli,events=on")
    ap.add_argument("--procs", type=int, default=max(1, cpu_count() - 1))
    ap.add_argument("--quick", action="store_true")
    ap.add_argument("--tag", default="", help="çıktı dosya son eki (örn. _g115)")
    ap.add_argument("--payback", action="store_true")
    ap.add_argument("--event-iso", action="store_true")
    ap.add_argument("--sensitivity", action="store_true")
    ap.add_argument("--figs", action="store_true", help="grafikleri üret")
    ap.add_argument("--no-matrix", action="store_true")
    ap.add_argument("--single", action="store_true", help="tek koşu, gün-gün dök (debug)")
    ap.add_argument("--trace-day", type=int, default=None, help="--single ile: bu günü saniye saniye dök")
    args = ap.parse_args()

    cfg = load_config(args.config, args.set)
    runs = args.runs or (30 if args.quick else cfg["sim"]["runs"])
    os.makedirs(RESULTS_DIR, exist_ok=True)
    tag = args.tag

    if args.single:
        cells = parse_cells(args.cells, cfg)
        P, pr, s, ev = cells[0]
        r = Run(cfg, P, pr, s, ev, cfg["sim"]["seed"], trace_day=args.trace_day).run()
        for rec in r["records"]:
            print(json.dumps({k: v for k, v in rec.items() if k != "levels"}, ensure_ascii=False))
        print(json.dumps({k: v for k, v in r.items() if k != "records"}, ensure_ascii=False, indent=1))
        return

    summary = {"config_overrides": args.set, "runs": runs}
    if not args.no_matrix:
        cells = parse_cells(args.cells, cfg)
        res = run_matrix(cfg, cells, runs, args.procs, label="matrix")
        summary["matrix"] = res
        write_matrix_csv(res, os.path.join(RESULTS_DIR, f"matrix{tag}.csv"))
        write_daily_csv(res, os.path.join(RESULTS_DIR, f"daily{tag}.csv"))
        if args.figs:
            figs = make_figs(res, DOCS_DIR, tag)
            print("grafikler:", *figs, sep="\n  ", file=sys.stderr)
    if args.payback:
        pb = payback_runs(cfg, runs, args.procs)
        summary["payback"] = pb
        if args.figs:
            make_payback_fig(pb, DOCS_DIR, tag)
    if args.event_iso:
        summary["eventIso"] = event_iso_runs(cfg, runs, args.procs)
    if args.sensitivity:
        summary["sensitivity"] = sensitivity_runs(cfg, runs, args.procs)
    with open(os.path.join(RESULTS_DIR, f"summary{tag}.json"), "w", encoding="utf-8") as f:
        json.dump(summary, f, ensure_ascii=False, indent=1)
    # kısa terminal özeti
    if "matrix" in summary:
        print(f"{'hücre':34s} {'kayıp%':>7s} {'kasa16':>8s} {'p10':>7s} {'prestij':>8s} {'gelir':>8s} {'kira':>7s} {'upg':>6s}")
        for name, r in summary["matrix"].items():
            print(f"{name:34s} {r['deathRate']*100:6.1f}% {r['finalMoney_mean']:8.0f} {r['finalMoney_p10']:7.0f} "
                  f"{r['finalPrestige_mean']:8.1f} {r['income_total_mean']:8.0f} {r['rent_total_mean']:7.0f} {r['upgrades_total_mean']:6.0f}")


if __name__ == "__main__":
    main()
