import sys, json, csv
sys.path.insert(0, r"C:\Users\cicek\Documents\GitHub\Cargor\tools\economy_sim")
import sifirdan_2026_09_23 as s
from multiprocessing import freeze_support
def main():
    base = s.live_cfg()
    base["strategies"]["secici"] = {"buy": True, "mode": "priority", "reserveFrac": 0.6, "reroll": True, "rerollMaxPerDay": 1,
        "priority": ["extra_hangar", "gambler_case", "all_in", "high_volatility", "leveraged_rent", "emergency_brake", "phone_line", "energetic_crew"]}
    variants = {"G100x": "combo:{\"rentGuard\":1.0,\"guardExempt\":[\"emergency_brake\"]}",
                "G100x+C": "combo:{\"rentGuard\":1.0,\"guardExempt\":[\"emergency_brake\"],\"pmult\":[1.0,1.8,2.5,3.0]}",
                "C_only": "pmult:[1.0,1.8,2.5,3.0]"}
    out = []
    for vn, vs in variants.items():
        cfg = s.apply_variant(base, vs)
        cells = [(P, pr, st, True) for P in (1, 2, 3, 4) for pr in s.PROFILES for st in ("hic", "acgozlu", "mantikli", "secici")]
        res = s.run_cells(cfg, cells, 300)
        for k, r in res.items():
            out.append({"variant": vn, **s.row(k, r)})
    s.write_rows(out, "secici_r4.csv")
    import collections
    d={(x["variant"],x["cell"]):x for x in out}
    V=list(variants)
    for P in (1,2,3,4):
      for pr in ("zayif","orta","iyi"):
        line=f"P{P}_{pr}".ljust(9)
        for v in V:
          line+="  "+"/".join(f"{d[(v,f'P{P}_{pr}_{st}')]['loss%']:.0f}:{d[(v,f'P{P}_{pr}_{st}')]['final_mean']}" for st in ("hic","acgozlu","mantikli","secici"))
        print(line)
if __name__ == "__main__":
    freeze_support(); main()
