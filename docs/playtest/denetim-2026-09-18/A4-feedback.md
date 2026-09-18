# A4 — Sensory Feedback Gap Audit (2026-09-18)

| Action | Audio | Visual | Severity |
|---|---|---|---|
| Correct delivery (truck) | **NONE** — `SfxId.CorrectItem` empty slot (`SfxLibrary.asset` id 9, `fileID: 0`); call site `Truck.cs:650` | None dedicated (no VFX/checkmark); only incidental money-counter roll if reward is nonzero | **CRITICAL** |
| Money earned | **NONE** — `SfxId.MoneyEarned` empty slot (id 8); call site `MoneyUI.cs:70` | Yes — count-up roll, color flash, punch scale (`NumberRollDisplay.cs:131-238`) | **CRITICAL** |
| Wrong delivery | Yes — `SfxId.WrongItem`, `Truck.cs:673`, clip assigned (id 3) | None dedicated (no shake/red flash on truck); prestige drop has no visible readout | Medium |
| Customer leaves angry (patience timeout) | **NONE** — `CustomerAI.cs:965-985 HandleTimeUp()` has zero `SfxBus` call | Only implicit: wait bar hidden (`HideWaitUI()`), customer walks to exit; no distinct "angry" cue (no red flash, no animation trigger, no VFX) | **CRITICAL** |
| Quest completed | Yes — `SfxId.QuestComplete`, `QuestManager.cs:387`, clip assigned | Card UI exists per user memory (quest card system merged) — not re-verified line-by-line here | Low |
| Gaining prestige | **NONE** — `PrestigeManager.cs` has zero AudioClip/AudioSource/Animator/Particle fields | **NONE found** in UI scripts grep — no dedicated prestige gain readout located | High |
| Day start | **NONE** — no `SfxId` call found near day-start path in `DayCycleManager.cs` | Not verified (day-start screen exists per gameplay flow but no audio pairing) | Medium |
| Day end | Yes — `SfxId.DayEnd`, `DayCycleManager.cs:965` | Day-end screen (`ShowDayEndScreenClientRpc`, `DayCycleManager.cs:960`) | OK |
| Rent warning | Yes — `SfxId.RentWarning`, `DayCycleManager.cs:1000` | Not verified | OK (audio) |
| Perk drafted / upgrade bought | Yes — `SfxId.UpgradeBought`, `UpgradePanel.cs:852` | Panel UI (not re-verified for animation) | OK |
| Truck enter/exit (arrival/departure) | Fields present (`enterAudioSource/exitAudioSource`, `Truck.cs:135-140`) but flagged in the 2026-09-14 pre-existing 53-slot scan as possibly empty on instances — **not independently reconfirmed here** | `Animator truckAnimator` drives enter/exit anim (`Truck.cs:86,356-378`) — visual is solid | Medium (audio uncertain) |
| Box pickup / place on shelf / packing | No `SfxId` entries exist for these at all — the enum (`SfxId.cs`) only has 10 members total, none named for pickup/place/pack | Not verified (likely animation-only via item/hand system) | Not scoped (out of `SfxLibrary`'s current 10-entry set entirely) |

## Confirmed leads

- **`SfxLibrary.asset` (`Assets/Resources/Audio/SfxLibrary.asset`)**: entries `id: 8` (MoneyEarned) and `id: 9` (CorrectItem) are `clip: {fileID: 0}` — confirmed still empty. Both call sites are live and wired (`MoneyUI.cs:70`, `Truck.cs:650/692`), so the plumbing works — the two most emotionally important positive beats in the whole loop (getting paid, delivering right) are **currently silent by design**, pending the user's clip choice (`plans/ses-tasarimi.md` §3.2, `docs/audio-candidates/para_*`, `dogru_*`).
- **53-slot scan (2026-09-14, `plans/devam.md:39-40`)**: re-confirmed the memory's claim — Truck legacy enter/exit fields, `UnifiedSettingsManager` (`Assets/MENUUI/UnifiedSettingsManager.cs`), Menu, `TutorialManager`/Door (`Assets/NewCss/Tutorial/TutorialManager.cs`), and an ithappy-adjacent `BoxFallPenalty.cs` (`boxDropSound`, `Assets/NewCss/BoxScripts/BoxFallPenalty.cs:20`) were named. Of these, **`BoxFallPenalty.boxDropSound`** is player-facing (mistake feedback — dropping a box) and worth resolving; Truck enter/exit audio is player-facing (truck arrival is a core loop cue) and also worth resolving. `UnifiedSettingsManager`/Menu/Tutorial slots are largely UI/onboarding, lower stakes. Exact per-slot empty/assigned status was not re-run live (would require Unity Editor batchmode) — this is a config-file read confirming the class of finding, not a fresh re-scan.

## Highest-cost gaps (ranked)

1. **Correct delivery is silent** — the core win-action of the entire game loop has no audio and no dedicated visual (no checkmark/VFX), only a money-counter side effect.
2. **Money earned is silent** — visual reward system (roll/flash/punch) was built for this moment but has no matching sound.
3. **Customer anger/loss is silent and visually flat** — `HandleTimeUp()` just hides a bar and despawns the customer; the game's core failure signal (a lost customer) produces no distinct cue at all.
4. **Prestige gain/loss has no feedback path at all** — no audio fields, no visual readout found in the scripts checked.

Both #1 and #2 are intentionally-silent (marked `BİLEREK boş`, awaiting user clip choice). #3 and #4 appear accidentally silent — no comment marks them as deliberate, and no plan reference covers them.
