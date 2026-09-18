---
name: a1-determinism-audit-2026-09-18
description: sim.js has zero RNG (pure EV model); MC-with-noise driver shows economy is essentially unloseable (winRate>=98% even at +-90%/day noise), bankruptcies only ever hit day12/16 rent gate, day-5 state weakly predicts outcome (r=0.27-0.48)
metadata:
  type: project
---

# A1 determinism audit — 2026-09-18 (full report: docs/playtest/denetim-2026-09-18/A1-determinism.md)

Task: investigate whether Cargor's 16-day outcome is already decided by day 5 (suspected
root cause of "loop feels static" complaint).

## Key structural fact (read this before any future MC/variance request)
`tools/economy-sim/sim.js` `runFullSim` has **zero `Math.random` calls** — it is a closed-form
expected-value model. Same `opts` -> byte-identical output every time. Quest outcomes are EV-
blended (`questDailyDecision`, sim.js:395-463), not resolved win/loss. Events (16-entry pool)
are **not modeled in `runFullSim` at all** (GDD.md:2426). "Monte Carlo" against sim.js as-is is
meaningless — there is no variance to sample. Any future variance/distribution question needs a
driver that reimplements the day-loop using sim.js's exported low-level functions
(`fullCustomerDay`, `truckThroughputWindowed`, `questDailyDecision`, `SRC4`, `ASSUMED` — all
exported, sim.js:1151-1163) plus an explicitly-labeled injected noise layer. Never present
sim.js's own determinism as if it were measured game variance.

## Findings (noise layer: +-15% daily Gaussian on truck+phone net income + quest resolved as
Bernoulli via questCompletionProb, both my own addition — see driver for exact method)
- **Win rate = 100%** across all 10 tested cells (P1-4 x Normal-strict/optimistic, P1-2
  Slow-strict) at realistic noise (sigma=0.15, N=4000/cell). Matches prior balance-round
  finding "16/16 hucre hayatta" ([[economy_full_balance_round12_gdd_resync_2026-08-30]]).
- Stress test up to sigma=0.90 (~6x realistic) on the two historically fragile cells (P1/P2
  Slow-strict): win rate never dropped below **98.3%**. Every bankruptcy (73 total across the
  sweep) landed on **day 12 or day 16 only** — the last two rent gates where
  `rentGrowthMultiplier=1.20` compounding finally outstrips a bad streak. Day 4 and day 8 = 0
  bankruptcies in any run at any tested noise level.
- corr(cash@day5, finalCash) = 0.27-0.48 across cells (weak; r^2~10-20%). Continuous-outcome
  "lock day" (corr >= 0.90) lands at **day 15 of 16**, not early.
- corr(quotaProgress@day5, finalCash) = 0 in every cell — **not a game signal, a modeling
  artifact**: `fullCustomerDay` computes quota/arrivals as closed-form capacity math with zero
  day-to-day randomness, so quota progress at day 5 is bit-identical across all MC trials for a
  given (P,scenario,mode). Don't reuse this number as "quota progress doesn't matter" — it
  literally cannot vary in the current model.

## Verdict / redirect for design work
"Day 5 decides the run" hypothesis is **not supported**. The more load-bearing finding: the
current economy is essentially **unloseable** at any point across all 16 days (not just after
day 5), so the "static feel" complaint more likely traces to *absence of jeopardy throughout the
run* than to *early lock-in*. Future tension-tuning work should look at rent-curve steepness /
bankruptcy-gate frequency (only 4 gates total, days 4/8/12/16) or a deliberate stochastic event/
customer-arrival layer — not early-game pacing.

Related: [[economy_full_balance_round12_gdd_resync_2026-08-30]] (16/16 hucre hayatta baseline),
[[gdd_section13_16_verified_current_2026-09-13]] (confirms GDD-code sync at time of this audit).
