#!/usr/bin/env python3
"""
2026-09-24 — "Koşu kaybedilebilir mi / sonuç ne kadar erken belli oluyor" Monte Carlo ölçümü.
sim.py'yi import eder, davranışını DEĞİŞTİRMEZ; config.json'a yazmaz (tüm varyantlar bellekte --set benzeri).
Çıktı: tools/economy_sim/results_mc40k_2026-09-24/

Varyantlar:
  base        config.json aynen (Ö-A kilit 1.0 + Acil Fren muaf, Ö-B, Ö-C grace iptali = sim yerli)
  livecal     events.freeDays=4  (canlı EventCalendarUI: currentDay = baseDay(1)+3, sonra +1..2 → ilk event 5/6;
              sim freeDays=3 ile ilk aday gün 4(kira→atla)/5 → 1 günlük kayma)
  sens_fast   profillerin süre parametreleri ×0.8 (t_serve,t_pack,t_load,t_phone,reaction)
  sens_slow   aynı ×1.2
  wi_daily    WHAT-IF: events.freeDays=0, intervalMin=intervalMax=1 (kira dışı her gün event; tekrar serbest;
              ilk 2 pozitif, 3. negatif = mevcut sıra kuralı)
  wi_norep    WHAT-IF: wi_daily + koşu içinde aynı event tekrar etmez (plans/her-gun-event-ve-kart-yenileme.md)

Kullanım: python mc40k_2026_09_24.py <variant> [--runs N] [--procs K] [--events on|off|both]
"""
import argparse
import copy
import csv
import json
import os
import sys
import time
from multiprocessing import Pool

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import sim  # noqa: E402

OUT = os.path.join(HERE, "results_mc40k_2026-09-24")
CHECK_DAYS = (3, 5, 8, 12)
BINS = [0, 0.25, 0.5, 0.75, 1.0, 1.25, 1.5, 2.0, 3.0, 1e9]
TIME_KEYS = ("t_serve", "t_pack", "t_load", "t_phone", "reaction")


def variant_cfg(name):
    cfg = sim.load_config(sim.DEFAULT_CONFIG)
    ev = cfg["events"]
    if name == "livecal":
        ev["freeDays"] = 4
    elif name in ("sens_fast", "sens_slow"):
        f = 0.8 if name == "sens_fast" else 1.2
        for p in cfg["profiles"].values():
            for k in TIME_KEYS:
                p[k] = p[k] * f
    elif name in ("wi_daily", "wi_norep"):
        ev["freeDays"] = 0
        ev["intervalMin"] = 1
        ev["intervalMax"] = 1
    elif name != "base":
        raise SystemExit(f"bilinmeyen varyant {name}")
    return cfg


class MCRun(sim.Run):
    norep = False

    def _gen_calendar(self):
        if not self.norep:
            return super()._gen_calendar()
        ec = self.cfg["events"]
        rng = sim.random.Random(self.rng.randrange(1, 2 ** 31))
        pool = list(ec["pool"])
        cal, count, day = {}, 0, ec["freeDays"]
        while day < self.days:
            day += rng.randint(ec["intervalMin"], ec["intervalMax"])
            if day in cal or day % self.eco["rentIntervalDays"] == 0:
                continue
            if count < ec["initialPositiveCount"]:
                cand = [x for x in pool if x["type"] == "pos"]
            elif count == ec["guaranteedNegativeIndex"]:
                cand = [x for x in pool if x["type"] == "neg"]
            else:
                cand = pool
            if not cand:
                cand = pool
            evn = cand[rng.randrange(len(cand))]
            pool.remove(evn)
            cal[day] = evn
            count += 1
        return cal

    def run(self):
        self.snap = {}
        for day in range(1, self.days + 1):
            self.day = day
            self.activate_pending(day)
            self.simulate_day(day)
            if day in CHECK_DAYS and not self.dead:
                self.snap[day] = (self.money, self.rent())
            if self.dead:
                break
        return self.summary()


def run_cell(args):
    variant, P, prof, strat, ev, n, seed = args
    cfg = variant_cfg(variant)
    MCRun.norep = variant == "wi_norep"
    final = np.zeros(n, np.int32)
    dead = np.zeros(n, np.int8)      # 0 hayatta, 1 iflas, 2 prestij
    dday = np.zeros(n, np.int8)
    money = np.full((n, len(CHECK_DAYS)), -1, np.int32)
    ratio = np.full((n, len(CHECK_DAYS)), -1.0, np.float32)
    for i in range(n):
        r = MCRun(cfg, P, prof, strat, ev, seed + i * 7919)
        o = r.run()
        final[i] = o["finalMoney"]
        if o["dead"]:
            dead[i] = 1 if o["dead"][0] == "bankrupt" else 2
            dday[i] = o["dead"][1]
        for j, d in enumerate(CHECK_DAYS):
            if d in r.snap:
                m, rt = r.snap[d]
                money[i, j] = m
                ratio[i, j] = m / rt if rt > 0 else 99
    return (variant, P, prof, strat, ev), dict(final=final, dead=dead, dday=dday, money=money, ratio=ratio)


def wilson(k, n, z=1.96):
    if n == 0:
        return (0.0, 0.0)
    p = k / n
    d = 1 + z * z / n
    c = (p + z * z / (2 * n)) / d
    h = z * np.sqrt(p * (1 - p) / n + z * z / (4 * n * n)) / d
    return (max(0.0, c - h), min(1.0, c + h))


def _rankdata(x):
    """Ortalama-sıra (ties) — scipy.stats.rankdata eşdeğeri."""
    x = np.asarray(x)
    order = np.argsort(x, kind="mergesort")
    xs = x[order]
    first = np.r_[True, xs[1:] != xs[:-1]]
    grp = np.cumsum(first) - 1
    starts = np.flatnonzero(first)
    ends = np.r_[starts[1:], len(xs)]
    avg = (starts + ends + 1) / 2.0
    rk = np.empty(len(x))
    rk[order] = avg[grp]
    return rk


def auc(score, label):
    """P(skor(kaybeden) < skor(kazanan)) — düşük oran = kayıp tahmini. Mann-Whitney."""
    pos = label == 1
    npos, nneg = pos.sum(), (~pos).sum()
    if npos == 0 or nneg == 0:
        return float("nan")
    rk = _rankdata(-score)
    return float((rk[pos].sum() - npos * (npos + 1) / 2) / (npos * nneg))


def summarize(results, variant, tag):
    os.makedirs(OUT, exist_ok=True)
    rows = []
    early_rows = []
    bin_acc = {}  # (grp, day, bin) -> [n, lost]
    for key, d in sorted(results.items(), key=lambda kv: (kv[0][1], kv[0][2], kv[0][3], not kv[0][4])):
        _, P, prof, strat, ev = key
        n = len(d["final"])
        lost = d["dead"] > 0
        nb, npd = int((d["dead"] == 1).sum()), int((d["dead"] == 2).sum())
        lo, hi = wilson(int(lost.sum()), n)
        bh = {g: int(((d["dead"] == 1) & (d["dday"] == g)).sum()) for g in (4, 8, 12, 16)}
        surv = d["final"][~lost]
        q = np.percentile(d["final"], [10, 50, 90])
        qs = np.percentile(surv, [10, 50, 90]) if len(surv) else [0, 0, 0]
        rows.append(dict(variant=variant, P=P, profile=prof, strategy=strat, events="on" if ev else "off", runs=n,
                         win=round(1 - lost.mean(), 5), lose=round(lost.mean(), 5), lose_ci95_lo=round(lo, 5),
                         lose_ci95_hi=round(hi, 5), bankrupt=round(nb / n, 5), prestigeDeath=round(npd / n, 5),
                         bk_d4=bh[4], bk_d8=bh[8], bk_d12=bh[12], bk_d16=bh[16],
                         final_p10=int(q[0]), final_med=int(q[1]), final_p90=int(q[2]),
                         surv_final_p10=int(qs[0]), surv_final_med=int(qs[1]), surv_final_p90=int(qs[2])))
        outcome = np.where(lost, 0, d["final"]).astype(np.float64)
        for j, day in enumerate(CHECK_DAYS):
            alive = d["ratio"][:, j] >= 0
            if alive.sum() < 10:
                continue
            m = d["money"][alive, j].astype(np.float64)
            rt = d["ratio"][alive, j].astype(np.float64)
            y = outcome[alive]
            lab = lost[alive].astype(np.int8)
            corr = float(np.corrcoef(m, y)[0, 1]) if m.std() > 0 and y.std() > 0 else float("nan")
            early_rows.append(dict(variant=variant, P=P, profile=prof, strategy=strat, events="on" if ev else "off",
                                   day=day, alive=int(alive.sum()), lostAfter=int(lab.sum()),
                                   corr_money_final=round(corr, 4), auc_ratio_loss=round(auc(rt, lab), 4),
                                   ratio_med=round(float(np.median(rt)), 3)))
            idx = np.digitize(rt, BINS) - 1
            for grp in ("ALL", prof, f"P{P}"):
                for b in range(len(BINS) - 1):
                    sel = idx == b
                    a = bin_acc.setdefault((grp, day, b), [0, 0])
                    a[0] += int(sel.sum())
                    a[1] += int(lab[sel].sum())
    with open(os.path.join(OUT, f"matrix_{tag}.csv"), "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(rows[0].keys()))
        w.writeheader()
        w.writerows(rows)
    if early_rows:
        with open(os.path.join(OUT, f"early_cells_{tag}.csv"), "w", newline="", encoding="utf-8") as f:
            w = csv.DictWriter(f, fieldnames=list(early_rows[0].keys()))
            w.writeheader()
            w.writerows(early_rows)
        with open(os.path.join(OUT, f"early_bins_{tag}.csv"), "w", newline="", encoding="utf-8") as f:
            w = csv.writer(f)
            w.writerow(["group", "day", "ratio_lo", "ratio_hi", "n_alive", "lost_after", "p_lose", "ci95_lo", "ci95_hi"])
            for (grp, day, b), (nn, ll) in sorted(bin_acc.items(), key=lambda kv: (kv[0][0], kv[0][1], kv[0][2])):
                if nn == 0:
                    continue
                lo, hi = wilson(ll, nn)
                w.writerow([grp, day, BINS[b], BINS[b + 1] if BINS[b + 1] < 1e8 else "inf", nn, ll,
                            round(ll / nn, 5), round(lo, 5), round(hi, 5)])
        # havuzlanmış korelasyon/AUC (tüm hücreler + profil bazlı)
        pooled = []
        for grp_name, filt in [("ALL", lambda k: True), ("zayif", lambda k: k[2] == "zayif"),
                               ("orta", lambda k: k[2] == "orta"), ("iyi", lambda k: k[2] == "iyi")]:
            for j, day in enumerate(CHECK_DAYS):
                rs, ls = [], []
                for key, d in results.items():
                    if not filt(key):
                        continue
                    alive = d["ratio"][:, j] >= 0
                    rs.append(d["ratio"][alive, j]); ls.append((d["dead"][alive] > 0).astype(np.int8))
                if not rs:
                    continue
                r_ = np.concatenate(rs); l_ = np.concatenate(ls)
                pooled.append([variant, grp_name, day, len(r_), int(l_.sum()), round(auc(r_.astype(np.float64), l_), 4)])
        with open(os.path.join(OUT, f"early_pooled_{tag}.csv"), "w", newline="", encoding="utf-8") as f:
            w = csv.writer(f)
            w.writerow(["variant", "group", "day", "n_alive", "lost_after", "auc_ratio_loss"])
            w.writerows(pooled)
    return rows


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("variant")
    ap.add_argument("--runs", type=int, default=40000)
    ap.add_argument("--procs", type=int, default=11)
    ap.add_argument("--events", default="both")
    ap.add_argument("--strategies", default="")
    ap.add_argument("--tag", default="")
    a = ap.parse_args()
    cfg = variant_cfg(a.variant)
    evs = {"on": [True], "off": [False], "both": [True, False]}[a.events]
    strats = a.strategies.split("/") if a.strategies else list(cfg["strategies"].keys())
    seed = cfg["sim"]["seed"]
    jobs = [(a.variant, P, pr, s, e, a.runs, seed) for P in (1, 2, 3, 4) for pr in cfg["profiles"]
            for s in strats for e in evs]
    t0 = time.time()
    with Pool(a.procs) as pool:
        res = dict(pool.imap_unordered(run_cell, jobs))
    tag = a.tag or a.variant
    summarize(res, a.variant, tag)
    with open(os.path.join(OUT, f"meta_{tag}.json"), "w", encoding="utf-8") as f:
        json.dump(dict(variant=a.variant, runs_per_cell=a.runs, cells=len(jobs), total_runs=a.runs * len(jobs),
                       seconds=round(time.time() - t0, 1), seed=seed, seed_rule="seed + i*7919 (tüm hücrelerde ortak = CRN)",
                       events=a.events, strategies=strats), f, ensure_ascii=False, indent=1)
    print(f"[{tag}] {len(jobs)} hücre × {a.runs} = {a.runs * len(jobs)} koşu — {time.time() - t0:.0f} s", file=sys.stderr)


if __name__ == "__main__":
    main()
