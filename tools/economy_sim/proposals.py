#!/usr/bin/env python3
"""
Öneri paketlerini baz duruma karşı koşar (docs/economy/05-oneriler.md'nin kanıt üreticisi).

Kullanım:
  python tools/economy_sim/proposals.py                 # tüm paketler
  python tools/economy_sim/proposals.py --only A1 B1     # seçili paketler
  python tools/economy_sim/proposals.py --runs 300

Her paket = `sim.py --set` override listesi. Çıktı: baz ile yan yana kasa16/kayıp/prestij
tablosu (ve --payback ile kart amortismanı, paket kart ekonomisine dokunuyorsa).
"""
import argparse
import copy
import json
import os
import sys
from multiprocessing import Pool, cpu_count

HERE_DIR = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE_DIR)
from sim import (DEFAULT_CONFIG, RESULTS_DIR, cell_name, deep_set, load_config,  # noqa: E402
                 parse_value, payback_runs, run_matrix)

# Kart id -> config listesindeki index (config.json "upgrades" sırası)
CARD_INDEX = {"wide_storage": 0, "packing_station": 1, "extra_hangar": 2, "quest_tier": 3,
              "cheap_rent": 4, "prestige_broker": 5, "prestige_master": 6, "fast_hangar": 7,
              "energetic_crew": 8, "agile_crew": 9, "patient_customers": 10, "gambler_case": 11,
              "phone_line": 12, "overtime": 13, "leveraged_rent": 14, "high_volatility": 15,
              "emergency_brake": 16, "all_in": 17, "bulk_buy": 18}


def card(uid, field, value):
    return f"upgrades.{CARD_INDEX[uid]}.{field}={value}"


PACKAGES = {
    # ── KART EKONOMİSİ ────────────────────────────────────────────────────────
    "A1": ("fast_hangar etkisi tersine (hangar süresi ×1.3 → ×0.75)",
           ["perkEffects.fast_hangar_mult=0.75"]),
    "A2": ("upgrade P-maliyet çarpanı {1,2,2.95,3.7} → {1,1.6,2.1,2.5}",
           ["economy.upgradeCostMultiplierByPlayerCount=[1.0,1.6,2.1,2.5]"]),
    "A3": ("ölü kartların fiyatı amortisman hedefine çekildi (≤12 gün)",
           [card("overtime", "baseCost", 120), card("emergency_brake", "baseCost", 90),
            card("wide_storage", "baseCost", 25), card("wide_storage", "costStep", 10),
            card("quest_tier", "baseCost", 40), card("quest_tier", "costStep", 10),
            card("bulk_buy", "baseCost", 30), card("patient_customers", "baseCost", 60),
            card("phone_line", "baseCost", 60), card("prestige_broker", "baseCost", 80),
            card("prestige_broker", "costStep", 10), card("cheap_rent", "baseCost", 70),
            card("cheap_rent", "costStep", 15), card("energetic_crew", "baseCost", 55),
            card("agile_crew", "baseCost", 110)]),
    # A4 ve ALL aşağıda programatik olarak doldurulur (A1+A2+A3 birleşimi).
    # ── KİRA / RİSK ───────────────────────────────────────────────────────────
    "B1": ("kira eğrisi düzleştirme: g 1.20→1.12, taban {330,730,1280,1830} (16-gün toplamı sabit)",
           ["economy.rentGrowthMultiplier=1.12",
            "economy.baseRentByPlayerCount=[330,730,1280,1830]"]),
    "B2": ("grace 2 kullanım (gün 8 duvarını yumuşat)", ["economy.graceUses=2"]),
    "B3": ("B1 + B2", ["economy.rentGrowthMultiplier=1.12",
                       "economy.baseRentByPlayerCount=[330,730,1280,1830]",
                       "economy.graceUses=2"]),
    # ── GÜN DOLULUĞU / CO-OP ──────────────────────────────────────────────────
    "C1": ("2. servis istasyonu bağlanır (sahnedeki boş slot doldurulur)",
           ["economy.serviceStations=2"]),
    "C2": ("C1 + kuyruk 2→3", ["economy.serviceStations=2", "economy.maxQueueSize=3"]),
    "C3": ("C2 + kota P3/P4 ayrıştırılır (P4 +%20) + varış aralığı P3/P4 ×0.8",
           ["economy.serviceStations=2", "economy.maxQueueSize=3",
            "economy.dailyCustomerCount.3=[10,10,10,10,11,11,11,12,12,12,13,13,14,14,14,16]",
            "economy.customerArrivalIntervalByPlayerCount=[44,22,19,17]"]),
    # ── PRESTİJ ───────────────────────────────────────────────────────────────
    "D1": ("prestigePerBonus 8 → 6 (tier eşiği düşer, 1P'ye daha çok kazanç)",
           ["economy.prestigePerBonus=6"]),
    "D2": ("maxPrestige 100 → 140 (4P iyi tavana dayanmasın)",
           ["economy.maxPrestige=140"]),
    # ── EVENT GÜÇLENDİRME ─────────────────────────────────────────────────────
    "E1": ("event ekonomik etkileri ×2 güçlendirildi (ödül çarpanları tabandan uzaklaştırıldı)",
           ["events.pool.1.reward=1.4", "events.pool.4.reward=0.84", "events.pool.5.reward=1.16",
            "events.pool.7.reward=1.30", "events.pool.10.reward=1.24", "events.pool.13.reward=0.4",
            "events.pool.12.customers=0.6", "events.pool.9.customers=0.7"]),
    # ── PAKETLEME İSTASYONU'NUN YENİDEN TANIMI (Ö4) ──────────────────────────
    "F1": ("Paketleme İstasyonu 2. paketleme masası yerine 2. SERVİS istasyonunu açar + fiyat 150→90",
           ["perkEffects.packing_station_grants_station=1", card("packing_station", "baseCost", 90)]),
}
# A4, REC ve varyantlarını programatik doldur
PACKAGES["A4"] = ("A1 + A2 + A3 birlikte (kart ekonomisi paketi)",
                  PACKAGES["A1"][1] + PACKAGES["A2"][1] + PACKAGES["A3"][1])
PACKAGES["ALL"] = ("A1+A2+A3+B1+C1+D1 (B1 dahil — ölçüldü, B1 katkısı YOK)",
                   PACKAGES["A1"][1] + PACKAGES["A2"][1] + PACKAGES["A3"][1]
                   + PACKAGES["B1"][1] + PACKAGES["C1"][1] + PACKAGES["D1"][1])
# ÖNERİLEN PAKET: Ö1(A3) + Ö2(A2) + Ö3(A1) + Ö5(D1) + Ö4(F1). Kira düzleştirme (B1) BİLEREK YOK.
PACKAGES["REC"] = ("ÖNERİLEN: Ö1+Ö2+Ö3+Ö4+Ö5 (docs/economy/05-oneriler.md)",
                   PACKAGES["A1"][1] + PACKAGES["A2"][1] + PACKAGES["A3"][1]
                   + PACKAGES["D1"][1] + PACKAGES["F1"][1])
# Ö6: REC üstüne taban kira +%15 — kart ekonomisi düzelince kaybolan riski geri koyar.
PACKAGES["REC6"] = ("ÖNERİLEN + Ö6 (taban kira +%15 → {335,750,1310,1875})",
                    PACKAGES["REC"][1] + ["economy.baseRentByPlayerCount=[335,750,1310,1875]"])
PACKAGES["REC6b"] = ("REC + taban kira +%30 (ölçüldü: çok sert)",
                     PACKAGES["REC"][1] + ["economy.baseRentByPlayerCount=[377,845,1482,2119]"])
PACKAGES["REC7"] = ("ÖNERİLEN + Ö6 + Ö7 (4P kotası P3'ten ayrılır)",
                    PACKAGES["REC6"][1]
                    + ["economy.dailyCustomerCount.3=[10,10,10,10,11,11,11,12,12,12,13,13,14,14,14,16]"])

CELLS = [(P, pr, st, True) for P in (1, 2, 3, 4) for pr in ("zayif", "orta", "iyi")
         for st in ("hic", "mantikli")]


def apply_overrides(cfg, ovs):
    c = copy.deepcopy(cfg)
    for ov in ovs:
        k, v = ov.split("=", 1)
        deep_set(c, k, parse_value(v))
    return c


def summarize(res):
    out = {}
    for n, r in res.items():
        days = [d for d in r["byDay"] if d]
        out[n] = {"kasa": round(r["finalMoney_mean"]), "p10": round(r["finalMoney_p10"]),
                  "kayip": round(100 * r["deathRate"], 1), "prestij": round(r["finalPrestige_mean"], 1),
                  "gelir": round(r["income_total_mean"]), "upg": round(r["upgrades_total_mean"]),
                  # iş yükü / gün doluluğu ölçütleri (Ö4/Ö7/Ö9'un kanıtı)
                  "idle%": round(100 * sum(d["idle_frac"] for d in days) / len(days), 1),
                  "erken%": round(100 * sum(d["early_frac"] for d in days) / len(days), 1),
                  "kayipMusteri/gun": round(sum(d["lost_mean"] for d in days) / len(days), 2),
                  "deaths": r["deaths"]}
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--runs", type=int, default=300)
    ap.add_argument("--only", nargs="*", default=None)
    ap.add_argument("--procs", type=int, default=max(1, cpu_count() - 1))
    ap.add_argument("--payback-for", nargs="*", default=["A1", "A3", "A4", "ALL"])
    # Öneri paketleri DENETİM ANINDAKİ (Ö1/Ö2/Ö3 öncesi) duruma karşı ölçülür. config.json artık
    # canlı (uygulama sonrası) değerleri taşıdığı için varsayılan taban donmuş kanıt dosyasıdır;
    # aksi halde "BAZ" ile "A4" aynı çıkar ve paketlerin etkisi görünmez.
    ap.add_argument("--config", default=os.path.join(HERE_DIR, "config_baseline_pre.json"),
                    help="taban config (varsayılan: config_baseline_pre.json = denetim anı)")
    ap.add_argument("--out", default=None, help="çıktı json adı (varsayılan: proposals.json)")
    args = ap.parse_args()

    base_cfg = load_config(args.config)
    names = args.only or list(PACKAGES.keys())

    out = {}
    base = run_matrix(base_cfg, CELLS, args.runs, args.procs, label="baz")
    out["BAZ"] = {"desc": "denetim anındaki değerler (Ö1/Ö2/Ö3 öncesi) — taban: %s" % os.path.basename(args.config),
                  "overrides": [], "cells": summarize(base)}

    for name in names:
        desc, ovs = PACKAGES[name]
        cfg = apply_overrides(base_cfg, ovs)
        res = run_matrix(cfg, CELLS, args.runs, args.procs, label=name)
        entry = {"desc": desc, "overrides": ovs, "cells": summarize(res)}
        if name in args.payback_for:
            entry["payback"] = payback_runs(cfg, args.runs, args.procs)
        out[name] = entry

    path = os.path.join(RESULTS_DIR, args.out or "proposals.json")
    with open(path, "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False, indent=1)
    print(f"\nyazıldı: {path}\n", file=sys.stderr)

    # terminal tablosu: ortalama kayıp ve kasa (orta/mantıklı ve orta/hiç)
    hdr = f"{'paket':6s} {'aciklama':58s}"
    for P in (1, 2, 3, 4):
        hdr += f"  P{P}m"
    print(hdr + "   ort.kayip%")
    for name, e in out.items():
        row = f"{name:6s} {e['desc'][:58]:58s}"
        for P in (1, 2, 3, 4):
            c = e["cells"][cell_name(P, "orta", "mantikli", True)]
            row += f" {c['kayip']:5.1f}"
        avg = sum(v["kayip"] for k, v in e["cells"].items() if "mantikli" in k) / 12
        print(row + f"   {avg:6.1f}")


if __name__ == "__main__":
    main()
