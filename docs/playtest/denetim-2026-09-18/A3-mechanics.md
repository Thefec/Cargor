# A3 — Mechanic Inventory: Decisions vs. Dead Weight

Scope: `Assets/NewCss/`, `Assets/Scripts/Quest`. Read-only audit, no code changed.

## 1. Ranked by decision density

**1. Truck/hangar routing (`TruckSpawner.cs`, `Truck.cs`, `Table.cs`)** — every trip. Multiple
hangars spawn trucks needing different colors/quantities (`HangarSpawnPoint`,
`TruckSpawner.cs:336 ProcessHangar`); player picks which color to carry to which bay under a
countdown (`hangarStayDurationByPlayerCount`). This is the core decision loop — which truck is
closest to timing out, which color is scarce.

**2. Upgrade draft (`UpgradePanel.cs`, `DraftPool.cs`)** — daily after 10:00. Pick 1 of 3
randomized cards, tier-gated (T2 day5, T3 day9), with paid reroll that compounds
(`RerollCurve`: 50/90/160/290/525). Real opportunity cost, deferred to next day
(`UpgradePanel.cs` "pending" activation) — forces planning ahead of price knowledge.

**3. Quest accept/skip (`QuestManager.cs:816 AcceptQuest`)** — once/day, one active slot
(`_hasAcceptedToday`). Accepting commits to a penalty if unfinished by day-end
(`SettleAcceptedQuestsForDayEnd`, GDD §16.2: 15/27/53 TL). Real risk/reward.

**4. Throw vs. place (`PlayerInventory.Interaction.cs:414 HandleThrowInteraction`)** — frequent,
every box handoff. Throw is faster / enables co-op catches but risks the ≥3 m/s drop penalty
(`BoxFallPenalty.cs:41`, −5 TL/−0.04 prestige). Wired and reachable.

**5. Shelf item scroll-select (`PlayerInventory.Shelf.cs:121`)** — picks among available shelf
variants via mouse-wheel. Narrow (single-player-local, no controller-equivalent found), but a
real choice when it fires.

**6. Phone call (`PhoneCallManager.cs`)** — E-hold, unconditional +20 TL, +0.4 prestige, force-
spawns next customer, costs real time (14–35s depending on P). GDD's own economist notes (§14.4)
that in some bands optimal play is "call at 100%," which **collapses this into a no-decision
button-mash** rather than the intended "call only when idle" tradeoff. Decision exists but is
poorly bounded — see §3.

**7. Mixed-truck color priority (`TruckColorMixing.cs`)** — day 13+ only
(`PostRentFeatureUnlocks.MIXED_TRUCK_UNLOCK_DAY`). Real dominant-color/weighted-delivery choice,
but gated to the last 4 of 16 days — most players see it in ≤25% of a run.

**8. Stock-check / map view (`RoomViewController.cs`, X key)** — informational only
(desaturation + `RoomItemVisibility`), doesn't itself decide anything; it *feeds* decision #1 by
showing where items/players are in other rooms. Reachable, confirmed wired (matches memory:
merged/verified 2026-09-15).

## 2. Dead or near-dead mechanics

- **`DifficultyManager` patience/regen scaling — dead in practice.**
  `ScaledCustomerCount`, `ScaledMinPatience`, `ScaledMaxPatience`, `ScaledStaminaRegenRate`
  (`DifficultyManager.cs:122-146`) have no consumer outside the file itself (grep-confirmed). The
  one place that *looks* live — `ApplyCustomerSettings` (`DifficultyManager.cs:425-441`) calling
  `FindObjectsOfType<CustomerAI>()` and writing `minWaitTime`/`maxWaitTime` — only fires once, on
  player-count-change/start, before `CustomerManager` has spawned any customers
  (`CustomerManager.cs:706 Instantiate(customerPrefab, ...)` happens continuously through the
  day). Every actual customer therefore keeps the prefab's static 15/20s patience regardless of
  player count. Same story for `ApplyStaminaSettings` (`DifficultyManager.cs:470-478`). Net
  effect: an entire "P-count changes difficulty feel" system that fires, writes values, and is
  then silently overwritten by the next spawn. Confirms GDD §19.1's own caution note.

- **6 of 26 upgrades are permanently `disabledInDraft`** (`UpgradePanel.cs:76`): Geniş Kuyruk,
  Sağlam Kasa, Dinç Ekip, Su Sebili, Güler Yüz, Uzun Kuyruk. They exist as full `UpgradeDefinition`
  entries (art, cost fields, descriptions) but `DraftPool.IsEligible` (`DraftPool.cs:27`) filters
  them out of every offer — unreachable by any player, ever. `PlayerMovement.cs:228-235`
  literally references the "Dinç Ekip" stamina buff in a comment while the upgrade that grants it
  can't be bought.

- **`rewardPerBox` scalar field** (`GameEconomySettings`) — legacy fallback only used if the
  per-player-count array is empty/null; the array is always populated, so this field is dead
  weight in the inspector (confirmed by GDD §4.1 note, not independently re-verified further).

- **Quota not shown as an actionable number to the player.** `CustomerManager.remainingCustomersText`
  (`CustomerManager.cs:149,1062-1075`) is the only UI surface for "customers left today," and it's
  a bare `TextMeshProUGUI` field with the known silent-null failure mode (per project memory —
  not independently re-verified against the scene YAML in this pass, flagged for a scene check).
  If unbound, the phone-vs-wait decision (#6) has no information to be made *on*.

## 3. Redundant systems (same decision, two levers)

- **Phone call vs. natural customer service both pay identical prestige (+0.4)**, but phone also
  pays unconditional +20 TL *and* skips time, regardless of whether the pulled-forward customer
  is ever actually served (`PhoneCallManager.cs` `ExecuteCall`, GDD §14.4 warning: "second,
  unconditional money tap"). This makes the phone strictly dominant in several economy bands
  (GDD's own simulation: optimal usage hits 100% in Slow/strict P1/P2), collapsing what should be
  a scarce-time-vs-money tradeoff into a spam button.

## 4. Complexity without a choice

- **Event calendar** (`EventCalendarUI.cs`, `EventEffectManager.cs`) is read-only foresight — it
  doesn't ask the player to do anything, it just multiplies other systems (upgrade cost ×0.8 on
  OPPORTUNITY DAY, phone time-cost ×0.5 on CUSTOMER SUPPORT). Fine as a modifier layer, but it is
  not itself a decision point — its value depends entirely on whether the systems it touches (#2,
  #6) are legible enough for the player to act on the forecast.
- **Break room sync** (`BreakRoomManager.cs`) — pure rendezvous gate, zero decisions, by design
  (documented as such in GDD §18).
- **`WaveSettings`** is wired (`CustomerManager.cs:138`) and does scale spawn-rate during rush
  windows, but this is environmental pacing, not a player decision — it shapes urgency, doesn't
  create a choice.

## Bottom line

The loop's real decisions cluster in truck/hangar routing and the daily upgrade draft; quest
accept and throw-vs-place add real but lower-frequency risk choices. The clearest deletion or
rework candidates are the `DifficultyManager` scaling properties (dead code, ~150 lines, actively
misleading since GDD had to document why they don't work), the 6 permanently-disabled upgrade
cards (unreachable content, wasted art/design budget), and the phone's unconditional money reward
(turns a scarcity decision into a dominant-strategy button in multiple economy bands).
