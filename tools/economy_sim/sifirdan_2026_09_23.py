#!/usr/bin/env python3
"""
2026-09-23 sıfırdan ekonomi analizi — sim.py'nin Run sınıfını kullanır, sim.py'nin
varsayılan davranışını DEĞİŞTİRMEZ. Tüm çıktı tools/economy_sim/results_2026-09-23/ altına.

Kullanım:
  python tools/economy_sim/sifirdan_2026_09_23.py baseline [--runs N]
  python tools/economy_sim/sifirdan_2026_09_23.py daily
  python tools/economy_sim/sifirdan_2026_09_23.py payback
  python tools/economy_sim/sifirdan_2026_09_23.py proposals [--runs N]
"""
import copy
import csv
import json
import os
import statistics
import sys
from multiprocessing import Pool

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import sim  # noqa: E402

OUT = os.path.join(HERE, "results_2026-09-23")
os.makedirs(OUT, exist_ok=True)

PROFILES = ["zayif", "orta", "iyi"]
STRATS = ["hic", "acgozlu", "mantikli"]
UNLOCK = {0: 1, 1: 5, 2: 9}


def live_cfg():
    cfg = sim.load_config(sim.DEFAULT_CONFIG)
    # canlı kod: all_in/leveraged_rent grace'i %0'a çeker (silmez) — bkz. sim.py graceZeroPctLive
    cfg["economy"]["graceZeroPctLive"] = True
    return cfg


class DelayedForceRun(sim.Run):
    """force_cards'ı yalnız force_day ve sonrasında satın alır (T2/T3 kilidine uyum)."""
    force_day = 1

    def cost_of(self, uid, level, event_cost_mult):
        c = super().cost_of(uid, level, event_cost_mult)
        sp = self.eco.get("cardPMult")
        if sp and uid in sp["ids"]:
            dflt = self.eco["upgradeCostMultiplierByPlayerCount"][sim.pidx(self.P)]
            c = int(round(c * sp["pmult"][sim.pidx(self.P)] / dflt))
        slope = self.eco.get("dayCostSlope", 0.0)
        if slope:
            c = int(round(c * (1.0 + slope * (getattr(self, "day", 1) - 1))))
        return c

    def _guard_exempt_prepass(self, day, event_cost_mult, st):
        """Kira-fonu kilidinden muaf kartlar (örn. emergency_brake) stratejinin ORİJİNAL
        rezerviyle, kilitten önce alınır."""
        ex = self.eco.get("guardExempt") or []
        if not ex or not self.strat.get("buy"):
            return
        import random as _r
        offer = self.select_offer(self.eligibility(day), _r.Random(day * 73856093))
        prio = self.strat.get("priority")
        orig = self.strat.get("origReserveFrac", self.strat.get("reserveFrac", 0))
        for uid in ex:
            if uid not in offer or (prio is not None and uid not in prio):
                continue
            lv = self.visual_level(uid)
            if lv >= self.ups[uid]["maxLevel"] or self.blocked_by_group(uid):
                continue
            cost = self.cost_of(uid, lv, event_cost_mult)
            if self.money - cost < int(orig * self.rent()):
                continue
            self.money -= cost
            self.total_spent_upgrades += cost
            st["shopSpent"] += cost
            self.pending.append((uid, lv + 1, day))
            st["bought"].append(uid)

    def shop(self, day, event_cost_mult, st):
        self._guard_exempt_prepass(day, event_cost_mult, st)
        if day < self.force_day:
            saved, self.force_cards = self.force_cards, []
            r = super().shop(day, event_cost_mult, st)
            self.force_cards = saved
            return r
        return super().shop(day, event_cost_mult, st)


def _cell(args):
    cfg, P, prof, strat, ev, runs, seed, force, fday = args
    outs = []
    for i in range(runs):
        r = DelayedForceRun(cfg, P, prof, strat, ev, seed + i * 7919, force, None)
        r.force_day = fday
        outs.append(r.run())
    agg = sim.aggregate(outs, cfg["sim"]["days"])
    # ek metrikler
    agg["day1_money_mean"] = statistics.mean(o["records"][0]["moneyEnd"] for o in outs)
    agg["day1_income_mean"] = statistics.mean(o["records"][0]["income"] for o in outs)
    buys = [len([b for rec in o["records"] for b in rec["bought"]]) for o in outs]
    agg["cards_bought_mean"] = statistics.mean(buys)
    agg.pop("byDay_full", None)
    return agg


def run_cells(cfg, cells, runs, force=None, fday=1):
    seed = cfg["sim"]["seed"]
    jobs = [(cfg, P, prof, strat, ev, runs, seed, list(force or []), fday) for (P, prof, strat, ev) in cells]
    with Pool(min(12, len(jobs))) as pool:
        res = pool.map(_cell, jobs)
    return {f"P{c[0]}_{c[1]}_{c[2]}": r for c, r in zip(cells, res)}


def row(k, r):
    return {"cell": k, "loss%": round(100 * r["deathRate"], 1), "final_mean": round(r["finalMoney_mean"]),
            "final_p10": round(r["finalMoney_p10"]), "day1_money": round(r["day1_money_mean"]),
            "day1_income": round(r["day1_income_mean"]), "income_total": round(r["income_total_mean"]),
            "rent_total": round(r["rent_total_mean"]), "upg_total": round(r["upgrades_total_mean"]),
            "cards": round(r["cards_bought_mean"], 1), "deaths": r["deaths"]}


def write_rows(rows, name):
    path = os.path.join(OUT, name)
    with open(path, "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(rows[0].keys()))
        w.writeheader()
        for x in rows:
            w.writerow(x)
    return path


def baseline(runs, cfg=None, tag="baseline"):
    cfg = cfg or live_cfg()
    cells = [(P, pr, st, True) for P in (1, 2, 3, 4) for pr in PROFILES for st in STRATS]
    res = run_cells(cfg, cells, runs)
    rows = [row(k, r) for k, r in res.items()]
    write_rows(rows, f"matrix_{tag}.csv")
    # günlük tablo (hic + mantikli)
    drows = []
    for k, r in res.items():
        for d in r["byDay"]:
            if d is None:
                continue
            drows.append({"cell": k, "day": d["day"], "alive": round(d["alive"], 3), "money_mean": round(d["money_mean"]),
                          "money_p10": round(d["money_p10"]), "income_mean": round(d["income_mean"]),
                          "served": round(d["served_mean"], 1), "delivered": round(d["delivered_mean"], 1),
                          "quota": round(d["quota"], 1), "rent_mean": round(d["rent_mean"]),
                          "prestige": round(d["prestige_mean"], 1)})
    write_rows(drows, f"daily_{tag}.csv")
    return rows


def payback(runs):
    """Her kart tek başına, kilidin açıldığı gün alınır (hic stratejisi, event'siz)."""
    cfg = live_cfg()
    out = []
    for P in (1, 2, 3, 4):
        for prof in ("zayif", "orta", "iyi"):
            base = run_cells(cfg, [(P, prof, "hic", False)], runs)[f"P{P}_{prof}_hic"]
            jobs = []
            for u in cfg["upgrades"]:
                fday = 1 if u["kind"] == "backbone" else UNLOCK[u["tier"]]
                jobs.append((cfg, P, prof, "hic", False, runs, cfg["sim"]["seed"], [u["id"]] * u["maxLevel"], fday))
            with Pool(12) as pool:
                res = pool.map(_cell, jobs)
            for u, r in zip(cfg["upgrades"], res):
                fday = 1 if u["kind"] == "backbone" else UNLOCK[u["tier"]]
                cost = r["upgrades_total_mean"]
                gain = (r["income_total_mean"] - base["income_total_mean"]) \
                    - (r["rent_total_mean"] - base["rent_total_mean"]) \
                    - (r["penalties_total_mean"] - base["penalties_total_mean"])
                active = 16 - fday
                per_day = gain / active if active > 0 else 0
                out.append({"P": P, "profile": prof, "card": u["id"], "buyDay": fday, "cost": round(cost),
                            "gain_total": round(gain), "gain_per_day": round(per_day, 1),
                            "payback_days": round(cost / per_day, 1) if per_day > 0 else None,
                            "d_final": round(r["finalMoney_mean"] - base["finalMoney_mean"]),
                            "d_loss_pp": round(100 * (r["deathRate"] - base["deathRate"]), 1),
                            "base_day_income": round(base["income_total_mean"] / 16)})
            print(f"payback P{P} {prof} tamam", file=sys.stderr)
    write_rows(out, "payback.csv")
    return out


def apply_variant(cfg, name):
    c = copy.deepcopy(cfg)
    e = c["economy"]
    ups = {u["id"]: u for u in c["upgrades"]}
    if name == "live":
        pass
    elif name.startswith("pmult:"):
        e["upgradeCostMultiplierByPlayerCount"] = json.loads(name.split(":", 1)[1])
    elif name.startswith("price:"):
        # price:{"id":[base,step],...}
        for uid, (b, s) in json.loads(name.split(":", 1)[1]).items():
            ups[uid]["baseCost"], ups[uid]["costStep"] = b, s
    elif name.startswith("combo:"):
        spec = json.loads(name.split(":", 1)[1])
        if "pmult" in spec:
            e["upgradeCostMultiplierByPlayerCount"] = spec["pmult"]
        for uid, (b, s) in spec.get("price", {}).items():
            ups[uid]["baseCost"], ups[uid]["costStep"] = b, s
        for k, v in spec.get("eco", {}).items():
            e[k] = v
        if "cardPMult" in spec:
            e["cardPMult"] = spec["cardPMult"]
        if "daySlope" in spec:
            e["dayCostSlope"] = spec["daySlope"]
        if "rentGuard" in spec:
            # oyun kuralı varyantı: kart alımı sonrası kasa >= rentGuard × sıradaki kira olmalı
            for st in c["strategies"].values():
                if st.get("buy"):
                    st["origReserveFrac"] = st.get("reserveFrac", 0)
                    st["reserveFrac"] = max(st.get("reserveFrac", 0), spec["rentGuard"])
        if "guardExempt" in spec:
            e["guardExempt"] = spec["guardExempt"]
    return c


def proposals(runs, variants):
    base = live_cfg()
    allrows = []
    for vname, vspec in variants.items():
        cfg = apply_variant(base, vspec)
        cells = [(P, pr, st, True) for P in (1, 2, 3, 4) for pr in PROFILES for st in STRATS]
        res = run_cells(cfg, cells, runs)
        for k, r in res.items():
            x = row(k, r)
            x = {"variant": vname, **x}
            allrows.append(x)
        print(f"variant {vname} tamam", file=sys.stderr)
    write_rows(allrows, "proposals.csv")
    return allrows


if __name__ == "__main__":
    mode = sys.argv[1]
    runs = int(sys.argv[sys.argv.index("--runs") + 1]) if "--runs" in sys.argv else 300
    if mode == "baseline":
        for x in baseline(runs):
            print(x)
    elif mode == "payback":
        payback(runs)
    elif mode == "proposals":
        with open(sys.argv[2], encoding="utf-8") as f:
            variants = json.load(f)
        proposals(runs, variants)
