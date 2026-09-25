Repository: Thefec/Cargor (branch: main). Unity 6 co-op cargo/shop management game, 16-day roguelite run.

GOAL
Independently answer: "Can a Cargor run actually be lost, and how early is the outcome decided?" using a 40,000-run Monte Carlo on the CURRENT economy. Another team is answering the same question with a different simulator; we will compare the two results, so your model must be INDEPENDENT.

INDEPENDENCE RULE (important)
- Build your own simulation model directly from the C# game code. Do NOT read, import, or copy `tools/economy_sim/` or `tools/economy-sim/` or `docs/economy/` or `docs/playtest/` while building the model.
- Only AFTER your results are final, you may run `python tools/economy_sim/sim.py --quick` and briefly compare, in a separate section titled "Cross-check vs sim.py".

WHERE THE RULES LIVE (read these, not the whole repo)
- Economy constants: `Assets/NewCss/**/GameEconomySettings.cs` and the ScriptableObject asset it loads (search for `EkonomiAyarlari`). Per-player-count arrays are indexed by player count.
- Scene overrides: serialized values in `Assets/Scenes/The Main Office.unity` (YAML) OVERRIDE C# field defaults. When a value differs, the scene/asset value wins. Grep the YAML; the file is huge.
- Day cycle, rent every 4 days (days 4/8/12/16), rent growth, grace, bankruptcy: `DayCycleManager.cs`, `GameStateManager.cs`.
- Rent reserve lock (upgrades cannot spend money reserved for next rent; "Acil Fren"/emergency-brake card is exempt): search `upgradeRentReserveFraction`, `UpgradePanel.cs`.
- Money comes from trucks (boxes delivered), customers give prestige: `Truck.cs`, `CustomerAI.cs`, `CustomerManager.cs`, `PrestigeManager`.
- Day length, customer quota/arrival per day and player count: `DayCycleManager.cs`, `GameEconomySettings.cs`, `DifficultyManager.cs`.
- Events (16 events, calendar generation, which days, effects): `Assets/NewCss/Events/EventCalendarUI.cs` (`_allEvents`, `GenerateInitialEvents`, `SelectEventByCount`) — verify which event effects are actually read by gameplay code; some may be inert.
- Upgrades/perks draft (one card-buy opportunity per day, tiers unlock on day 5/9): `Assets/NewCss/Roguelite/DraftPool.cs`, `PerkEffect.cs`, upgrade list in the scene (`disabledInDraft: 1` = never offered).
- Mechanic unlocks on day 5/9/13 (return mode, dual-item orders, mixed-color trucks): `PostRentFeatureUnlocks.cs`.
- Quests (optional reward/penalty at day end): `Assets/Scripts/Quest/`.

WHAT TO SIMULATE
- Player count P = 1, 2, 3, 4.
- Team skill profiles = the main uncertainty (the game has not been playtested enough). Define at least 3 profiles (weak / average / good) as seconds-per-box-cycle and error rate (wrong color delivery, dropped box), state your numbers explicitly, and justify them from the code (walking distances aren't in code — say so and pick ranges).
- Strategies: at least "never buy upgrades", "buy greedily whenever affordable", "sensible (buy when payback < remaining days)".
- Real randomness: customer arrival timing, which items are ordered, event calendar, card offers, quest outcomes, profile noise per day. Use a fixed seed for reproducibility.
- 40,000 runs total at minimum (e.g. 4 P × 3 profiles × 3 strategies × ≥1,100 runs each). Report 95% confidence intervals.

MEASURE
1. Win % and bankruptcy % per cell; which rent day the bankruptcy happens (4/8/12/16); final money median/p10/p90.
2. Early determination: how well does cash (or cash ÷ next rent) at end of day 3, 5, 8, 12 predict the final outcome? Give correlation and a simple threshold rule ("below X at day 5 → Y% bankruptcy").
3. Sensitivity: change profile speeds ±20% — how does bankruptcy % move?
4. What-if scenario (separate label): events on EVERY non-rent day (days 1-3, 5-7, 9-11, 13-15), no duplicate event within a run. How do win/bankruptcy change vs. current?

DELIVERABLES (open a PR, do NOT modify any game code, .cs, .unity, or .asset files)
- `tools/jules_mc/` — your simulator (Python, standard library + numpy allowed), with a README showing the exact command to reproduce.
- `docs/economy/jules-mc40k-2026-09-24.md` — report: (a) every parameter you extracted from code WITH file:line source, (b) assumptions you had to invent, (c) result tables, (d) 5-bullet conclusion, (e) "Cross-check vs sim.py" section.
- Raw CSV results under `tools/jules_mc/results/`.
- Be explicit when a value could not be found in code; never silently invent game constants.
