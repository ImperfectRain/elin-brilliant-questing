# Elin update smoke test (BQ-110)

Run this checklist after every Elin update. It checks aliases, native patches, an existing save
chunk and one playable situation. It is a bounded compatibility check; it does not certify every
capability or replace the [capability degradation drill](runtime-probes.md#drill-implementation-and-procedure).

## Prepare and identify the update

- [ ] Record the previous and updated displayed Elin versions and, where available, Steam build
  IDs. Preserve the update source or local build record. An Elin assembly version of `0.0.0.0`,
  a new BQ commit, or a DLL timestamp alone does not establish a real game update.
- [ ] Record date/time, BQ commit and installed DLL hash, BepInEx version, enabled mods and relevant
  configuration. Keep the mod set/configuration fixed when comparing before and after.
- [ ] Preserve a backup of the pre-update save and its log. Test a disposable copy containing an
  existing BQ chunk and a known situation. Record its people/events/threads and the situation's
  participants, known facts, current outcome and relevant inventory/standing before loading it.
- [ ] Clear `[Debug] DisabledCapability` and disable optional mutation/scenario flags for the
  existing-save pass. Restart Elin. Keep logs from each launch before they are overwritten.
- [ ] Build the Plugin against the updated installed game libraries using the
  [build procedure](../../plugin-build.md#local-references); record command and result. Stale `lib/`
  copies cannot establish compatibility with the update. Build success proves compilation only.

## Run in the updated game

1. **Aliases resolve.** Load the test copy and retain `Resolved all ... element aliases.` plus the
   capability report. Investigate any `Unresolved element aliases` or empty source-sheet diagnostic.
   Compare logged player attributes/skills with the character sheet, so a resolved name is not
   mistaken for a correct read. Record any newly unavailable capability and its baseline reason;
   already unsupported capabilities are not new regressions.
2. **Patches apply and execute.** Retain `Drama choice projector installed.`,
   `Native Brilliant Questing journal patch installed with LayerJournal tab-memory guard.` and
   `Zone.OnVisit completion observer installed; reconciliation follows vanilla catch-up.`
   Exercise generic conversation, open/close/reopen the BQ journal, open authored/quest/shop
   dialogue, and change zones. Check visible BQ content, normal vanilla dialogue and journal tabs,
   and errors during these actions. Installation proves installation only; record actual UI and
   transition behavior separately. A disabled/skipped patch or fallback is a degraded result,
   even if ordinary play survives. Journal visual details have their own
   [acceptance checklist](../api/journal-ui.md#live-verification--all-items-unverified-for-this-corrective-build).
3. **Existing chunk loads.** Check restored bindings and `Simulation attached:` against the saved
   baseline, then inspect the known situation. Confirm its history and participants survive.
   Investigate `Saved world warning`, `Saved world could not be read`, missing bindings or an
   unexpected empty world. A playable empty fallback does not pass chunk compatibility. Explain
   legitimate new observations during attach rather than requiring all world counts to stay fixed.
4. **One situation plays.** Talk to a participant in the known situation through ordinary generic
   dialogue. Record the offered BQ action and its prerequisites, choose it once, and verify the
   resulting visible outcome and corresponding history/state. Continue to a recognizable situation
   outcome and record it. A failed check can be a valid outcome; merely listing options is not a
   played situation. Verify unavailable options reflect feasibility, not low skill alone. Record
   actual inventory/standing readback if the chosen route changes them; wording alone proves no write.
   If no suitable situation exists, use a separate throwaway save and the existing
   [three-NPC staging procedure](../../plugin-build.md#running-the-in-game-scenario-test).
   `StageScenarioOnLoad` writes actors/items and requires a world with no threads; it does not
   replace the existing-chunk test. Do not enable it on the original save.
5. **Save and reload the outcome.** Save the test copy, retain `Saved ... events into chunk
   'brilliantQuesting'.`, quit fully and reload. Verify the outcome, history, known facts and relevant
   inventory/standing remain consistent and that the chosen action/consequences are not applied
   again. Resume ordinary movement/dialogue. Record unexpected duplicates, lost state, exceptions
   or serialization errors. Preserve both logs and the before/after observations.

## Decide and record

Use **PASS**, **FAIL**, **DEGRADED** or **NOT RUN** for each check. PASS requires observed expected
behavior. DEGRADED means a diagnosed fallback or lost feature; it is not a clean smoke pass.
Missing update identity or an unexercised check leaves acceptance open. Do not upgrade capability
evidence from compilation, headless tests, patch-install messages or another build's observations.
For a regression, preserve the failing test copy/log, record the smallest reproduction and route
the affected operation through [capability evidence](../capabilities.md). Do not overwrite the
original backup with the failed test save.

Copy this record into a dated section of [runtime probes](runtime-probes.md) after a run. Link
supporting logs/screenshots with line ranges or timestamps and hashes; retain artifacts outside
Git if they contain private save data. Update an owning API page only for the operation proved.

```text
Date/time and tester:
Previous -> updated Elin release / Steam build IDs; update evidence:
BQ commit / installed DLL hash; BepInEx; mods/configuration:
Updated library build command/result:
Pre-update save baseline and backup; test copy:
Aliases (status, sheet comparison, log evidence):
Patches (status, installation and actual UI/zone behavior separately):
Existing chunk (status, baseline/restored state, diagnostics):
Situation (status, participants, action, observed outcome/readback):
Save/reload (status, preserved outcome and no duplicate consequences):
Logs/artifacts (paths, hashes, relevant timestamps/lines):
Overall result; regressions, missing checks and follow-up:
```

## Acceptance evidence

No run against an identified real game update is recorded yet. The September 10 BQ-109 capture
proves three disable diagnostics and bounded attach/save survival, but identifies no before/after
Elin update and no played situation. It cannot satisfy BQ-110's live done-when condition.
The user confirmed on September 10, 2026 that no update is available to test. The first live run
therefore remains blocked until a real update is available; the checklist is not a completed run.
