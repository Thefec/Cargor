# A2 — Day 3 vs Day 11: what actually changes across 16 days

Scope: day cycle, events, quests, perk draft. Netcode/late-join excluded (covered by a parallel audit).

## Day-by-day table

| Day | Duration | Rent due | Quota P1/P2/P3/P4 | New mechanic unlock | Event possible | Quest tier ceiling |
|---|---|---|---|---|---|---|
| 1 | 200s | no | 4/7/8/8 | none (baseline) | no (event-free) | T0 (Easy only, K=1) |
| 2 | 200s | no | 4/7/8/8 | none | no | T0 |
| 3 | 200s | no | 4/7/8/8 | none | no | T0 |
| 4 | 210s | **yes** | 4/8/8/8 | none | no (rent days excluded) | T0 |
| 5 | 220s | no | 4/8/9/9 | **Return mode** (25% of customers) + draft Tier2 unlocked | yes | T1 possible |
| 6 | 230s | no | 4/9/9/9 | none | yes | T1 |
| 7 | 240s | no | 4/9/9/9 | none | yes | T1 |
| 8 | 250s | **yes** | 5/9/10/10 | none | no | T1 |
| 9 | 260s | no | 5/10/10/10 | **Dual-item orders** (100%) + draft Tier3 unlocked | yes | T2 possible |
| 10 | 270s | no | 5/10/10/10 | none | yes | T2 |
| 11 | 280s | no | 5/10/11/11 | none | yes | T2 |
| 12 | 290s | **yes** | 5/11/11/11 | none | no | T2 |
| 13 | 300s | no | 6/11/12/12 | **Mixed-color trucks** (100%) | yes | T2 (Hard slot open if upgrade bought) |
| 14 | 310s | no | 6/11/12/12 | none | yes | T2 |
| 15 | 320s | no | 6/12/12/12 | none | yes (unprotected — see below) | T2 |
| 16 | 330s | **yes** | 6/12/13/13 | none, but win check + `SettleAcceptedQuestsOnGameEnd` bypasses normal `OnNewDay` settlement | no | T2 |

Sources: `DayCycleManager.cs:37-56,200-206` (duration/MAX_DAYS); `GameEconomySettings.cs:44,47,50,53` (per-day quota curves, index=day-1); `EventCalendarUI.cs:23-26,203,747-786` (calendar generation); `PostRentFeatureUnlocks.cs` via GDD §37 (day 5/9/13 unlocks); `DraftPool.cs:11-19` (T2/T3 gates); GDD §3.1-3.2, §7.1, §15.1, §16.1.

## Event system: verifying the "3-word taxonomy" lead

`EventType` does have only 3 values (`Positive/Negative/Neutral`, `EventCalendarUI.cs:40-45`), but `Neutral` is unused — `_allEvents` (cs:162-180) contains exactly 16 hand-written entries, 8 Positive / 8 Negative, 0 Neutral. So the taxonomy isn't the bottleneck; the catalog itself is the ceiling: **16 distinct events total**.

Per run: first 3 days event-free, events land every 1-2 days (`EVENT_INTERVAL_MIN/MAX`) skipping rent days (4/8/12/16), first 2 guaranteed positive, 3rd guaranteed negative, rest uniform-random over all 16 with **no duplicate-avoidance** (`SelectEventByCount`, cs:774-787, picks from the full list every time — a run can see the same event twice). GDD §15.2 cites a 40k-run Monte Carlo average of **5.86 events per 16-day run** (3.43 positive / 2.43 negative). That means roughly 10 of 16 days in a typical run see *no* event at all, and of the ~6 that do, several can repeat a prior event's exact effect. Variety is real but thin and not guaranteed distinct.

Two of 16 events are confirmed dead-effect at code level regardless of RNG: `IsGoldenBoxDay()`/`IsVIPServiceDay()` have no readers (GDD §15.2 note), and the quota-raising events (BUSY DAY, MARKETING DAY, ANGRY CUSTOMERS, GOLDEN BOX DAY) are mechanically inert because arrival interval isn't divided (GDD §7.2) — so up to 4 of the 8 negative/positive entries can fire and produce no felt change beyond a number or a missed-quota prestige ding.

## Verdict

- **Near-identical day clusters**: Days 1-3 are exactly identical (same quota, no events, T0 quest ceiling, flat 200s). Days 6-7, 10-11, and 14-15 are each a flat pair after their preceding unlock day (5, 9, 13) — same mechanic set, only duration (+10s/day) and quota (+0-1 customer) tick up, and whether an event happens to land is the only source of day-to-day difference.
- **Flat stretches with no new mechanic, no new decision**: days 1-3 (triplet), 6-7, 10-11, 14-15, and 16 itself (no new mechanic — win check + one-off settlement code path only). That is 11 of 16 days with zero structural change.
- **Variety is front-loaded, then thin and decaying**: the three real mechanic unlocks (return mode, dual-item, mixed truck) are hard-scripted to days 5/9/13 — evenly spread, not front- or back-loaded by design. But they're irreversible one-shot gates, not repeating decisions; once unlocked, days 6-8, 10-12, 14-16 offer nothing further except quota creep and coin-flip events. The event system is the only remaining variance source late-game, and GDD §15.2 flags day 15 (pre-rent) as the single least-protected, highest-single-event-damage day (−255 TL BUSY DAY, 4P) — a risk spike sitting right before the flattest, latest stretch of the run.
- Day 3 vs day 11 concretely: day 3 has quota 4/7/8/8, no events, T0 quests, 200s days; day 11 has quota 5/10/11/11 (dual-item + return mode both live, mixed trucks one day away), T2 quests, 280s days, possible events. The delta is real but almost entirely additive/numeric (bigger quota, longer day, wider quest/perk tier) rather than a new type of decision — the three unlock days are the only qualitative breaks in an otherwise scaling loop.
