#!/usr/bin/env python3
"""
2026-09-24 Ö-B kart düzeltmeleri (Hızlı Hangar / Mesai Saati / Prestij Ustası).
sim.py'yi ve sifirdan_2026_09_23.py'yi import eder, ikisinin davranışını DEĞİŞTİRMEZ.
Çıktılar: tools/economy_sim/results_2026-09-24/

Taban kural seti (hepsi AÇIK):
  Ö-A  kira fonu kilidi 1.0 (Acil Fren muaf) — zorlanmış (force) alımlara da uygulanır
  Ö-C  all_in/leveraged_rent grace'i İPTAL eder (graceZeroPctLive=False = sim varsayılanı)
Oyuncu sayısı koşu boyunca sabit (sim her zaman böyle varsaydı; 5b55926 ile oyun da böyle).

Kart varyant parametreleri (perkEffects içinde, yoksa canlı davranış):
  fast_hangar_mult            hangarStay çarpanı (canlı 0.75)
  fast_hangar_respawn_mult    tır respawn gecikmesi çarpanı (canlı yok = 1.0)
  fast_hangar_exit_mult       exitDelay çarpanı (canlı yok = 1.0)
  fast_hangar_anim_mult       giriş/çıkış animasyonu çarpanı (verilmezse exit_mult ile aynı)
  overtime_mult               gün süresi çarpanı (canlı 1.125)
  overtime_quota_bonus        [P1,P2,P3,P4] günlük kota +N müşteri (canlı yok = 0)
  overtime_grace_add          kota bitince erken-bitiş kapanış payına +sn (canlı yok = 0; taban 30)
  prestige_master_served_step müşteri başı prestij artışı/seviye (canlı 0.12)
  prestige_master_instant     satın alma anında tek seferlik +prestij/seviye (canlı yok = 0)

Kullanım:
  python ob_2026_09_24.py diag  [--runs N]
  python ob_2026_09_24.py sweep [--runs N]
  python ob_2026_09_24.py final [--runs N]
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
import sifirdan_2026_09_23 as sf  # noqa: E402

OUT = os.path.join(HERE, "results_2026-09-24")
os.makedirs(OUT, exist_ok=True)
PROFILES = ["zayif", "orta", "iyi"]
UNLOCK = {0: 1, 1: 5, 2: 9}
GUARD = 1.0
EXEMPT = ["emergency_brake"]


# 2026-09-24 sonrası config.json = Ö-B canlı değerleri + Ö-A kilidi (sim.py yerli). Ö-B ÖNCESİ canlı
# kart davranışını yeniden üretmek için bu pe'yi variant(...)'a verin ve Ö-A'yı kapatmak için
# economy.upgradeRentReserveFraction=0 yapın.
PRE_OB_LIVE_PE = {"fast_hangar_mult": 0.75, "fast_hangar_respawn_mult": 1.0, "fast_hangar_exit_mult": 1.0,
                  "fast_hangar_exit_min": 0.0, "overtime_grace_add": 0.0, "prestige_master_served_step": 0.12}


def base_cfg():
    cfg = sim.load_config(sim.DEFAULT_CONFIG)
    cfg["economy"]["graceZeroPctLive"] = False  # Ö-C: grace iptal
    return cfg


def with_guard(cfg):
    """Ö-A: strateji rezervi en az GUARD × sıradaki kira, Acil Fren muaf."""
    c = copy.deepcopy(cfg)
    for st in c["strategies"].values():
        if st.get("buy"):
            st["origReserveFrac"] = st.get("reserveFrac", 0)
            st["reserveFrac"] = max(st.get("reserveFrac", 0), GUARD)
    c["economy"]["guardExempt"] = list(EXEMPT)
    c["economy"]["forceGuard"] = GUARD
    return c


class OBRun(sf.DelayedForceRun):
    def activate_pending(self, day):
        before = dict(self.levels)
        super().activate_pending(day)
        inst = self.pe.get("prestige_master_instant", 0.0)
        if inst and self.levels["prestige_master"] > before["prestige_master"]:
            self.add_prestige(inst * (self.levels["prestige_master"] - before["prestige_master"]))

    def simulate_day(self, day):
        oe, oa = self.eco, self.asm
        e2, a2 = dict(oe), dict(oa)
        pe = self.pe
        native = getattr(sim, "NATIVE_OB", False)
        if self.levels["fast_hangar"] and not native:
            rm = pe.get("fast_hangar_respawn_mult", 1.0)
            xm = pe.get("fast_hangar_exit_mult", 1.0)
            e2["respawnDelayRange"] = [x * rm for x in oe["respawnDelayRange"]]
            am = pe.get("fast_hangar_anim_mult", xm)
            e2["exitDelay"] = oe["exitDelay"] * xm
            a2["truckEnterAnimSeconds"] = oa["truckEnterAnimSeconds"] * am
            a2["truckExitAnimSeconds"] = oa["truckExitAnimSeconds"] * am
        ga = pe.get("overtime_grace_add", 0.0)
        if self.levels["overtime"] and ga and not native:
            e2["dayEndGraceSeconds"] = oe["dayEndGraceSeconds"] + ga
        qb = pe.get("overtime_quota_bonus")
        if self.levels["overtime"] and qb:
            k = qb[sim.pidx(self.P)]
            e2["dailyCustomerCount"] = [list(r) for r in oe["dailyCustomerCount"]]
            e2["dailyCustomerCount"][sim.pidx(self.P)] = [v + k for v in oe["dailyCustomerCount"][sim.pidx(self.P)]]
        self.eco, self.asm = e2, a2
        try:
            super().simulate_day(day)
        finally:
            self.eco, self.asm = oe, oa

    def shop(self, day, event_cost_mult, st):
        g = self.eco.get("forceGuard")
        if g is None or day < self.force_day or not self.force_cards:
            return super().shop(day, event_cost_mult, st)
        # Ö-A kilidini zorlanmış alımlara da uygula (Acil Fren muaf)
        bought = 0
        for uid in list(self.force_cards):
            lv = self.visual_level(uid)
            if lv >= self.ups[uid]["maxLevel"]:
                self.force_cards.remove(uid)
                continue
            cost = self.cost_of(uid, lv, event_cost_mult)
            reserve = 0 if uid in (self.eco.get("guardExempt") or []) else int(g * self.rent())
            if self.money - cost < reserve:
                break  # sıradakileri de bekle (seviye sırası korunur)
            self.money -= cost
            bought += cost
            self.total_spent_upgrades += cost
            st["shopSpent"] += cost
            self.pending.append((uid, lv + 1, day))
            st["bought"].append(uid)
            self.force_cards.remove(uid)
        saved, self.force_cards = self.force_cards, []
        r = super().shop(day, event_cost_mult, st)
        self.force_cards = saved
        return r + bought


def _cell(args):
    cfg, P, prof, strat, ev, runs, seed, force, fday = args
    outs = []
    for i in range(runs):
        r = OBRun(cfg, P, prof, strat, ev, seed + i * 7919, force, None)
        r.force_day = fday
        outs.append(r.run())
    agg = sim.aggregate(outs, cfg["sim"]["days"])
    # teşhis metrikleri (tüm günler toplamı, ortalama koşu)
    def tot(key):
        return statistics.mean(sum(rec.get(key, 0) for rec in o["records"]) for o in outs)
    agg["diag"] = {k: round(tot(k), 1) for k in ("trucksSpawned", "trucksFull", "delivered", "served", "spawned",
                                                  "quota", "lost", "missed", "packed", "phone", "earlyEnd",
                                                  "wrongDelivery", "drops")}
    buy_days = [next((rec["day"] for rec in o["records"] if rec["bought"]), None) for o in outs]
    bd = [b for b in buy_days if b is not None]
    agg["buy_day_mean"] = round(statistics.mean(bd), 1) if bd else None
    agg["buy_frac"] = len(bd) / len(outs)
    agg.pop("byDay", None)
    return agg


def run_jobs(jobs):
    with Pool(min(12, len(jobs))) as pool:
        return pool.map(_cell, jobs)


def add_null_twins(cfg, cards):
    """Her kart için 'null_<id>': aynı kind/tier/fiyat/maxLevel, efekti YOK. Kartın saf fiyat
    etkisini (para harcamanın kendisi) efekt etkisinden ayırmak için kontrol grubu."""
    c = copy.deepcopy(cfg)
    for u in list(c["upgrades"]):
        if u["id"] in cards:
            t = dict(u); t["id"] = "null_" + u["id"]; t["name"] = "NULL " + u["name"]
            c["upgrades"].append(t)
    return c


def card_jobs(cfg, cards, runs, events=False, Ps=(1, 2, 3, 4)):
    """Her (P, profil) için: baseline (kart yok) + her kart tek başına, kilit gününde zorla (Ö-A kilitli)."""
    ups = {u["id"]: u for u in cfg["upgrades"]}
    jobs, keys = [], []
    seed = cfg["sim"]["seed"]
    for P in Ps:
        for prof in PROFILES:
            jobs.append((cfg, P, prof, "hic", events, runs, seed, [], 1)); keys.append((P, prof, None))
            for c in cards:
                u = ups[c]
                fday = 1 if u["kind"] == "backbone" else UNLOCK[u["tier"]]
                jobs.append((cfg, P, prof, "hic", events, runs, seed, [c] * u["maxLevel"], fday)); keys.append((P, prof, c))
    return jobs, keys


def card_table(cfg, cards, runs, events=False, Ps=(1, 2, 3, 4)):
    jobs, keys = card_jobs(cfg, cards, runs, events, Ps)
    res = run_jobs(jobs)
    base = {(k[0], k[1]): r for k, r in zip(keys, res) if k[2] is None}
    rows = []
    for k, r in zip(keys, res):
        if k[2] is None:
            continue
        b = base[(k[0], k[1])]
        rows.append({"P": k[0], "profile": k[1], "card": k[2],
                     "d_final": round(r["finalMoney_mean"] - b["finalMoney_mean"]),
                     "d_loss_pp": round(100 * (r["deathRate"] - b["deathRate"]), 1),
                     "loss%": round(100 * r["deathRate"], 1), "base_loss%": round(100 * b["deathRate"], 1),
                     "cost": round(r["upgrades_total_mean"]), "buy_day": r["buy_day_mean"], "buy_frac": round(r["buy_frac"], 2),
                     "d_income": round(r["income_total_mean"] - b["income_total_mean"]),
                     "d_prestige_end": round(r["finalPrestige_mean"] - b["finalPrestige_mean"], 1),
                     **{"d_" + dk: round(r["diag"][dk] - b["diag"][dk], 1) for dk in r["diag"]},
                     **{"b_" + dk: b["diag"][dk] for dk in ("trucksSpawned", "trucksFull", "delivered", "served", "quota", "earlyEnd")}})
    return rows


def write(rows, name):
    path = os.path.join(OUT, name)
    with open(path, "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(rows[0].keys()))
        w.writeheader()
        w.writerows(rows)
    return path


def variant(cfg, pe=None, ups=None):
    c = copy.deepcopy(cfg)
    for k, v in (pe or {}).items():
        c["perkEffects"][k] = v
    for uid, (b, s) in (ups or {}).items():
        for u in c["upgrades"]:
            if u["id"] == uid:
                u["baseCost"], u["costStep"] = b, s
    return c


def strat_matrix(cfg, runs, tag):
    seed = cfg["sim"]["seed"]
    cells = [(P, pr, st, True) for P in (1, 2, 3, 4) for pr in PROFILES for st in ("hic", "acgozlu", "mantikli")]
    jobs = [(cfg, P, pr, st, ev, runs, seed, [], 1) for (P, pr, st, ev) in cells]
    res = run_jobs(jobs)
    rows = []
    for (P, pr, st, ev), r in zip(cells, res):
        rows.append({"variant": tag, "cell": f"P{P}_{pr}_{st}", "loss%": round(100 * r["deathRate"], 1),
                     "final_mean": round(r["finalMoney_mean"]),
                     "own_fast_hangar": round(r["ownership"].get("fast_hangar", 0), 2),
                     "own_overtime": round(r["ownership"].get("overtime", 0), 2),
                     "own_prestige_master": round(r["ownership"].get("prestige_master", 0), 2)})
    return rows


if __name__ == "__main__":
    mode = sys.argv[1]
    runs = int(sys.argv[sys.argv.index("--runs") + 1]) if "--runs" in sys.argv else 300
    CARDS3 = ["fast_hangar", "overtime", "prestige_master"]
    if mode == "diag":
        cfg = with_guard(base_cfg())
        rows = card_table(cfg, CARDS3 + ["extra_hangar", "gambler_case"], runs)
        print(write(rows, "diag_live_card_table.csv"))
    elif mode == "variants":
        # argv[2] = json dosyası {name: {"pe": {...}, "cards": [...]}}
        with open(sys.argv[2], encoding="utf-8") as f:
            spec = json.load(f)
        ps = tuple(json.loads(sys.argv[sys.argv.index("--P") + 1])) if "--P" in sys.argv else (1, 2, 3, 4)
        allrows = []
        ev = "--events" in sys.argv
        for name, v in spec.items():
            cfg = add_null_twins(variant(with_guard(base_cfg()), v.get("pe"), v.get("ups")), CARDS3)
            for r in card_table(cfg, v["cards"], runs, events=ev, Ps=ps):
                allrows.append({"variant": name, **r})
            print("tamam", name, file=sys.stderr)
        print(write(allrows, sys.argv[3]))
    elif mode == "matrix":
        with open(sys.argv[2], encoding="utf-8") as f:
            spec = json.load(f)
        allrows = []
        for name, v in spec.items():
            cfg = base_cfg()
            if v.get("guard", True):
                cfg = with_guard(cfg)
            if v.get("graceZeroPctLive"):
                cfg["economy"]["graceZeroPctLive"] = True
            cfg = variant(cfg, v.get("pe"), v.get("ups"))
            allrows += strat_matrix(cfg, runs, name)
            print("tamam", name, file=sys.stderr)
        print(write(allrows, sys.argv[3]))
