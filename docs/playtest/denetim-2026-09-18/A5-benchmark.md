# A5 — Genre Benchmark: Cargor vs. PlateUp! / Overcooked 2 / Supermarket Simulator / Papers, Please

Scope: GDD.md §1 (vision) and §2 (core loop) only, plus §15 (event calendar) and §37 (rent-gated unlocks) checked directly against code comments to verify claims made in §1.4's USP list. Read-only.

## 1. What Cargor does that the comparators don't

**Deterministic daily quota instead of run-to-run luck.** GDD §1.4 states the customer count per day is fixed by day number, not randomized. This is a real, verifiable design choice (not just a claim — it's load-bearing enough to be USP #1). It's a direct answer to PlateUp!'s most-cited weakness: reviewers say PlateUp's difficulty is "not all up to how well you do" because blueprint draws are luck-gated (TheGamer/geekyhobbies-style commentary aggregated via OpenCritic). Cargor removing that luck axis while keeping PlateUp's day-based roguelite shape is a genuine, defensible differentiation — **it's good**, assuming the fixed quota curve is itself well-tuned (unverified here; that's economist's job, not this audit's).

**Room-based visibility + walkie-talkie (§34/§35, referenced in §1.4.6).** No comparator hides teammates from each other. Overcooked's entire appeal, per reviews (Push Square, GameCritics), is *watching* the chaos together and shouting callouts in the same frame. Cargor inverts that: you coordinate blind, by voice, across rooms. This is genuinely unusual — no genre precedent found in research. Whether it's **good** is unproven by this audit: it trades Overcooked's proven "shared-screen slapstick" appeal for an unproven "blind coordination" tension. Flag as differentiator, not yet validated as quality (matches existing memory note: WASD/voice issues already had shaky verification history).

**Rent-payment cadence as the unlock gate (§37, day 5/9/13).** This looks unique at first glance but isn't — Papers, Please already ties escalating mandatory rules to specific day numbers as its core pacing device, and reviewers cite that escalation as the game's spine (GameSpot, jkgeekly). Cargor doing the same with rent days is convergent design, not divergent. Correctly borrowed, not a genuine USP — the GDD's framing of it as differentiation is overstated.

## 2. What all four comparators do that Cargor doesn't (ranked by how load-bearing reviewers say it is)

1. **Player-chosen, build-defining upgrades with synergies, every single day.** PlateUp's blueprint-pool system (wiki.plateupgame.com) gives ~15 meaningful build decisions per run, each shifting strategy. Cargor's GDD §2.3.7 does have a 3-card upgrade draft post-10:00 — structurally similar — but it's gated to once/day at best and nothing in §1-2 suggests card synergies or build archetypes. This is the single most-cited "why runs stay fresh" mechanism across PlateUp reviews. Highest-ranked gap.
2. **Procedural/randomized physical layout per run.** PlateUp explicitly randomizes kitchen layout every restaurant (plateuptools.com, PlateUp wiki). Nothing in Cargor's GDD §1-2 indicates the store/rooms are regenerated between runs — they read as fixed. Reviewers treat layout randomization as a primary reason PlateUp doesn't go stale run over run.
3. **Escalating environmental/mechanical gimmicks at a near-per-level cadence.** Overcooked adds a new hazard or mechanic almost every level (fire, conveyors, moving platforms — cited by Nintendolife/Trusted Reviews as core to why it doesn't get boring early). Cargor's equivalent cadence is 3 forced unlocks across 16 days (day 5/9/13) — an order of magnitude sparser.
4. **Failure that reads as funny, not just punitive.** Overcooked's "failing is just as fun as success" (Gideon's Gaming) is repeatedly cited as why co-op friction lands as comedy, not frustration. Cargor's only comparable friction point in §2.3 is "Fırlat → düşerse ceza" (throw → penalty if dropped) — a punishment, not a slapstick moment. Unverified beyond GDD text; worth a targeted qa/playtest check, not asserted as fact here.

## 3. Variety at the equivalent point in a run

At "day N of 16," PlateUp gives the player ~N cumulative blueprint choices plus a freshly randomized kitchen; Supermarket Simulator gives incremental license/customer-variety growth (grindy per Steam reviews, but still growing); Papers, Please gives ~N cumulative rule-stacking decisions that compound difficulty every day. Cargor, per GDD §15.1, gives one randomized event roughly every 1-2 days from day 3 onward (passive multiplier, no player agency) plus, at most, N/1 upgrade-card choices — comparable in *frequency* to Papers, Please's rule cadence but shallower in *player agency*, since PlateUp/Papers both make the day-to-day change something the player picked or must actively adapt to, while Cargor's event calendar is something that happens *to* the player. Net: Cargor's per-day variance mechanism exists and is structurally sound, but it is thinner in player agency than every comparator's equivalent system.

## Sources
- [PlateUp! Reviews — OpenCritic](https://opencritic.com/game/13554/plateup-/reviews)
- [Blueprints — PlateUp! Wiki](https://wiki.plateupgame.com/gameplay/Blueprints)
- [Seeded runs FAQ — PlateUp! Tools](https://plateuptools.com/seeds-faq)
- [Overcooked 2 Review — Push Square](https://www.pushsquare.com/reviews/ps4/overcooked_2)
- [Overcooked! 2 Review — GameCritics](https://gamecritics.com/aj-small/overcooked-2-review/)
- [Overcooked 2 Review: Chaos Incarnate — Gideon's Gaming](https://gideonsgaming.com/overcooked-2-review-chaos-incarnate/)
- [Overcooked 2 Review — Nintendo Life](https://www.nintendolife.com/reviews/nintendo-switch/overcooked_2)
- [Supermarket Simulator Reviews — OpenCritic](https://opencritic.com/game/18883/supermarket-simulator/reviews)
- [Supermarket Simulator review — GameSpew](https://www.gamespew.com/2025/11/supermarket-simulator-review/)
- [Papers, Please Review — GameSpot](https://www.gamespot.com/reviews/papers-please-review/1900-6412914/)
- [Quirky Video Game Review: Papers, Please](https://jkgeekly.com/2025/04/05/quirky-video-game-review-papers-please/)
