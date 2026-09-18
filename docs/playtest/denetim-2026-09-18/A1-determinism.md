# A1 — How early is a Cargor run's outcome decided?

Source: `tools/economy-sim/sim.js` v5.1 (`runFullSim`, read-only). Driver scripts (throwaway,
not committed elsewhere): `mc_driver.js` / `mc_sweep.js` in this session's scratchpad.

## 0. Critical precondition — sim.js has ZERO randomness

`grep -c "Math.random" tools/economy-sim/sim.js` → **0 matches**. `runFullSim` is a pure
expected-value (EV) model: identical `opts` always produce the byte-identical 16-day
trajectory (confirmed in code: quest outcomes use EV blending via `questDailyDecision`
`sim.js:395-463`, not a resolved win/loss; events are explicitly "not modeled in runFullSim"
per `GDD.md:2424-2427`). **Monte Carlo variance as literally requested in the brief does not
exist in the current simulator** — running `runFullSim` twice with the same args is not a
"trial", it's the same number.

To produce any run-to-run variance at all, I wrote a driver (`mc_driver.js`) that replays
sim.js's own exported day-loop building blocks (`fullCustomerDay`, `truckThroughputWindowed`,
`questDailyDecision`, `SRC4`, `ASSUMED`) but adds two noise layers **not present in sim.js**,
clearly labeled as my additions, not code-derived:
1. Daily ±15% Gaussian noise on net truck+phone income (proxy for arrival timing / table
   contention / delivery-accuracy luck the EV model smooths away).
2. Quest outcomes resolved as genuine Bernoulli draws (`questCompletionProb` as success
   probability) instead of the EV blend — the single largest real swing already in the
   design (+150/−53 TL on a Hard quest).
Everything else (rent, grace, prestige floor, bankruptcy gate) is copied verbatim from
`runFullSim` (`sim.js:1079-1100`). N=4000 trials/cell unless noted.

## 1. Headline numbers

| Metric | Value | Note |
|---|---|---|
| Win rate, all 10 tested cells (P1-4 × Normal-strict/optimistic + P1-2 Slow-strict), ±15% noise | **100%** | 0 bankruptcies in 40,000 trials |
| corr(cash@day5, finalCash), P2 Normal/strict | **0.386** | weak — day 5 explains ~15% of final-wealth variance (r²) |
| corr(cash@day5, finalCash), across all 10 cells | **0.27 – 0.48** | consistently weak-to-moderate, never strong |
| corr(prestige@day5, finalCash) | **0 – 0.47** | weaker than cash; P1 Slow/strict = 0 exactly |
| corr(quota-progress@day5, finalCash) | **0 (all cells)** | **not measurable** — see §3 |
| Day at which corr(cash_day, finalCash) ≥ 0.90 | **day 15 of 16** | both strict and optimistic bands (P2 Normal) |
| Comeback rate (bottom half @ day5 → still win) | **100%** | degenerate: nobody loses at realistic noise |
| Bankruptcy-day distribution, realistic noise (σ=0.15) | **undefined — 0 bankruptcies** | |
| Bankruptcy-day distribution, stress test (σ up to 0.90, P2 Slow/strict) | **only days 12 and 16** | day 4, day 8 = 0 occurrences in every sweep |
| Win rate under stress test, σ=0.90 (P2 Slow/strict, the most fragile live cell) | **98.3%** | |

## 2. Stress-test sweep (finding the bankruptcy onset)

At the realistic noise level (σ=0.15) none of the 10 cells ever lose — consistent with prior
balance rounds' finding "16/16 hücre hayatta" (`GDD.md:2395`). To get *any* bankruptcy signal
at all I swept σ up to 0.90 (±90% daily swing, well beyond plausible play variance) on the two
historically most fragile cells (P1/P2 Slow-strict):

| P | σ | winRate | bankruptRate | bankruptDayHist | comebackRate |
|---|---|---|---|---|---|
| 1 | 0.60 | 100.0% | 0.0% | {} | 1.00 |
| 1 | 0.75 | 99.8% | 0.2% | {16: 6} | 0.996 |
| 1 | 0.90 | 99.9% | 0.1% | {16: 2} | 0.999 |
| 2 | 0.45 | 99.7% | 0.3% | {12: 2, 16: 6} | 0.995 |
| 2 | 0.60 | 99.2% | 0.8% | {12: 6, 16: 17} | 0.985 |
| 2 | 0.75 | 98.8% | 1.2% | {12: 15, 16: 22} | 0.976 |
| 2 | 0.90 | 98.3% | 1.7% | {12: 11, 16: 39} | 0.967 |

Every single bankruptcy across all sweeps (73 total) fell on **day 12 or day 16** — the last
two rent gates, where `rentGrowthMultiplier=1.20` compounding (`sim.js:507`) finally
outstrips a bad streak. Day 4 and day 8 never produced a bankruptcy in any run, at any noise
level tested.

## 3. What is not measurable with the current simulator

- **True Monte Carlo variance**: sim.js has no RNG (§0). All variance numbers above come from
  a noise layer I added in the driver, not from the game's own simulated stochasticity.
- **corr(quota-progress@day5, finalCash) = 0 is a modeling artifact, not a game signal**:
  `fullCustomerDay` (`sim.js:779-890`) computes customer arrivals/quota as closed-form
  capacity math with zero day-to-day randomness — quota progress at day 5 is bit-identical
  across all 4000 trials for a given (P, scenario, mode), so its variance is exactly 0 and
  Pearson correlation against it is undefined (I return 0 by convention). Answering this
  sub-question for real would require reimplementing per-customer arrival as a stochastic
  process, which is out of scope for a read-only driver.
- **Event system impact**: `runFullSim` does not model the 16-entry event pool at all
  (`GDD.md:2426`); any early/late variance events (Festival Day, Customer Support, etc.)
  contribute is untested here.

## Verdict

Reject the "day-5 already decides the run" hypothesis **as far as sim.js's economy math
can show**: correlation between day-5 state and final cash is weak (r=0.27-0.48, r²≈10-20%)
and only crosses 0.90 on day 15 of 16 — one day before the end. The much bigger finding is
that **the current economy essentially cannot be lost**: win rate stayed ≥98.3% even under a
±90%/day noise injection that is far beyond realistic play variance, and the handful of
losses that did occur only ever happened on the day-12 or day-16 rent gate, never earlier.
If the "static feel" complaint is real, this data points away from *early determinism* and
toward **absence of jeopardy across the entire 16 days** — the player is never plausibly at
risk of losing at any point, not just after day 5, so no part of the run carries tension. That
is a distinct root cause from the one hypothesized and should redirect the follow-up
investigation (rent-curve steepness, bankruptcy-gate frequency, or a deliberate stochastic
event/customer-arrival layer) rather than an early-game pacing fix.
