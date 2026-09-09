# Runtime Probes

Static Phase 2 reduced the probe list. Remaining probes validate live UI behavior, actor populations, and nonzero save-affecting mutations. Do not upgrade any item to `VERIFIED-RUNTIME` unless the installed game log shows the probe ran successfully.

## Performance and Home capture

This opt-in instrumentation gathers evidence for BQ-107/BQ-108; it does not complete either live
acceptance criterion by itself. It adds diagnostic reads and timings, with no new simulation,
native mutations or persisted fields. Ordinary gameplay and existing BQ features still run.

1. Close Elin. Build/install the diagnostic plugin using the [build procedure](../../plugin-build.md).
   The build output is `src/BrilliantQuesting.Plugin/bin/Debug/netstandard2.0/`; copy
   `BrilliantQuesting.Plugin.dll`, `package.xml` and `content.bqc` into the existing BQ Package folder.
   Keep the mod enabled in Elin's Mods menu. Use a backup/copy of the large save for this exercise.
2. Launch once and exit if the config entry has not been generated. In
   `BepInEx/config/elin.brilliant.questing.cfg`, under `[Debug]`, set
   `CaptureRuntimeEvidence = true`. Restart Elin. Leave other settings unchanged; this capture
   does not require enabling any Testing flag.
3. Load your largest representative save. Keep the game focused, with unchanged resolution,
   FPS cap/VSync and other mods throughout the capture. Wait 30 seconds after loading, then
   spend about two minutes each standing idle, walking/performing ordinary actions in a busy
   area, and opening/closing ordinary NPC conversations (including BQ options when available).
   Note the approximate wall-clock start of each phase and any visible stutters.
4. Visit Home and take screenshots of its resident/resource displays. Leave, play or wait until
   at least seven in-game days have elapsed, and return. Take one ordinary step so the existing
   action-driven reconciliation can observe the zone change. Capture the same displays and note
   the in-game dates. Save, exit, reload that copy, take a step and capture those displays again.
   If seven days is impractical, report the actual elapsed time; do not claim that a shorter run
   exercised the seven-day scheduling window.
5. Exit Elin and copy `BepInEx/LogOutput.log` before launching again (it may be overwritten).
   Return the complete log, the phase/time notes and Home screenshots. Include the displayed
   Elin version, CPU/GPU/RAM, resolution, FPS cap/VSync, other enabled mods, approximate save age,
   and whether this save already had substantial BQ history. The log reports BQ population counts;
   a large vanilla save with few BQ records does not demonstrate large-history BQ performance.
6. Set `CaptureRuntimeEvidence = false` and restart to turn off capture.

`BQ-PERF` reports windows every 30 seconds or 8,192 intervals, plus partial windows at save,
detach and quit. Frame mean/p95/p99/max and the number above 50 ms use focused Unity `Update`
intervals measured with a monotonic stopwatch. Unfocused gaps are excluded; focused menus,
loading, pauses, VSync and diagnostic/logging overhead can be included. These are main-thread
frame cadence measurements, not GPU timings or the incremental cost of the entire mod.
The build module ID identifies the diagnostic binary. Callback count/total/mean/max cover BQ's
ActPerformed handler, attach, save, dialogue choice projection and scheme zone reconciliation.
They are inclusive, can nest, and must not be added together; other BQ hooks are outside this
attribution. A controlled baseline would still be needed to establish whole-mod frame impact.

`BQ-HOME` brackets the existing reconciliation call on attach/observed zone change and samples
before save. It reports fresh Home readback, game minute, zone IDs, event count and up to 64
resident IDs/presence/BQ clocks with an explicit sample count. Missing reads remain unknown.
Both sides are **after native zone entry**, not before/after `Zone.Simulate`; the existing daily
advance can already have run before the zone-change observation. The logger never calls
`Zone.Simulate` or replays production. `Food` is the Home **capacity skill**, not food stock;
unchanged values alone do not prove absence of duplicate production. Screenshots and the actual
revisit/reload sequence supply context; ambiguous deltas need a controlled follow-up observation.

Implementation: [RuntimeEvidence](../../../src/BrilliantQuesting.Plugin/RuntimeEvidence.cs).
Headless proof: [RuntimeEvidenceTests](../../../tests/BrilliantQuesting.Core.Tests/RuntimeEvidenceTests.cs)
covers window statistics/bounds, excluded focus gaps, exception-safe timing/logging and read-only
Home sampling. Native callbacks, rendered frame behavior and actual Home catch-up remain unverified.

## Session A: Safe Read-Only Shape And UI Probe

Questions answered: `ELIN-Q-0013`, `ELIN-Q-0014`, `ELIN-Q-0015`, `ELIN-Q-0017`, `ELIN-Q-0018`, `ELIN-Q-0020`, `ELIN-Q-0021`, `ELIN-Q-0023`, `ELIN-Q-0024`.

Setup: ordinary save, debug logging enabled, no vanilla mutation.

Player actions: load into a town/Home, talk to at least one ordinary NPC and one service/story/guild NPC, open journal, advance one hour or revisit a zone if convenient.

Log values:

- Actor sample: uid, source id/name, trait type, source tags, `IsUnique`, `IsImportant`, `c_uniqueData != null`, `c_isImportant`, `quest != null`, `IsGlobal`, `IsHomeMember`, `IsBranchMember`, BQ classifier result.
- Activity sample (BQ-135, now the adapter's own log lines rather than a bespoke probe): the `BQ actor activity:` shape line, the per-actor `activity <name> [<id>]:` lines, the `Actor activity read for N loaded actor(s)` tally naming every unanswered facet, and the `Actor activity: N global actor(s) listed, E eligible ...` line. Also record the `capability ReadActorActivity:` line, since a build that answers no facet reports it unavailable and every downstream reading is then `Unknown` by design.
- Dialogue/bark: chosen route (`Card.SayRaw`, `Card.TalkRaw`, or `Msg.SayRaw`), whether speaker is synced, whether raw line is visible before/after `DramaManager.sequence.Exit()`.
- Choice layout: total vanilla+BQ choice count and whether choices are visible/scrollable/clickable.
- Journal shape: `LayerJournal` window count, `Window.setting.tabs` count/names before build, selected `idTab`, content component names, switch callback sequence.
- Act/witness sample: act type full name, static `Act.CC`, `Act.TC`, `Act.TOOL`, `Act.TP`, instance `AIAct.owner`, `AI_TargetCard.target`, BQ witness candidates, `Card.Dist`, `Card.GetSightRadius`, `Chara.CanSeeLos`, perception/spotting/stealth totals, and whether observed action is theft, pickup, combat, chat, craft, harvest, or production.
- Affordance sample: active zone id/name/source row id, branch/faction ids if present, loaded chara source job/hobby/trait/faith counts, loose `Map.things` category/source ids.

Expected interpretations:

- Classifier thresholds can be tightened only if ordinary/service/story samples support them.
- Global-goal availability can be exposed read-only if samples match `GameDate.AdvanceHour` predicates.
- Bark/open-Drama display remains `UNRESOLVED` unless the line is visually confirmed.
- Journal tabs remain implementation-risky until a BQ content object can be switched without layout/lifecycle issues.
- `ActPerformed` remains observation-only for act payloads; production still needs separate hook evidence if no production act publishes. BQ-135 did not change this: activity is read by asking a `Chara`, never by turning the act event into an activity bus.
- BQ-135 activity facets stay `SOURCE-OBSERVED`/`VERIFIED-METADATA` until this session runs. Promote a facet to `VERIFIED-RUNTIME` only on the evidence named beside it:
  - **timetable** — an ordinary resident's line shows `timetable <id>` rather than `timetable ?`. A town where every line reads `?` answers `ELIN-Q-0014`'s first half negatively and is worth recording as such.
  - **routine span** — a line shows a span other than `?`. If every span reads `?` while timetables read, the span member has been renamed or reshaped: record the real member name and enum members, because the reader matches on the value's own name.
  - **current activity** — record which concrete `AIAct` type names actually appear, and specifically how many lines read `activity Other`. A high `Other` count is the mapping table being wrong, not the game being unfamiliar.
  - **`UseGlobalGoal`** — the eligible count on the global line. Record whether ordinary town citizens appear among the eligible or only adventurer/traveller kinds; that is `ELIN-Q-0014` proper.
  - **`global.goal` / `global.transition`** — a global sample line showing `global-activity` and `transition` as something other than `?`, and at least one actor reading `vanilla-moving Moving`. Record whether a transition is visible at attach at all.
  - **still not probed, and deliberately** — whether `GetGoalFromTimeTable`, `GetGoalWork` and `GetGoalHobby` are side-effect-free. BQ-135 does not call them, so this session does not answer it. Answering it needs its own deliberate step (call one on a disposable save and compare the actor's `ai`, needs and position before and after), and until it is answered the routine's projected goal stays out of the snapshot.

Disposable save required: no.

Estimated human interaction time: 10-15 minutes.

## Session B: Disposable Item Mutation Probe

Questions answered: `ELIN-Q-0004`, `ELIN-Q-0008`, `API-020`, `API-021`.

Setup: `DISPOSABLE SAVE REQUIRED`. Create or select mundane disposable items and, for transfer, two safe holders or a generated holder.

Player/tool actions:

1. Record source holder inventory and active `EClass._map.things`.
2. Transfer one item via `Chara.Pick(Thing,bool,bool)`.
3. Record source/destination inventories, returned `Thing.uid`, stack counts, and whether the original uid survived.
4. Destroy one mundane item with `Thing.Destroy()`/`Card.Destroy()`.
5. Save/reload if persistence validation is required for enabling gameplay.

Expected interpretations:

- If `Pick` returns a different destination stack, BQ must update bindings or validate by count/category delta rather than original uid.
- `Destroy` can be considered live nonzero mutation only if the item disappears from holder/zone and remains gone after reload.

Estimated human interaction time: 5-10 minutes.

## Session C: Disposable Home And Absence Mutation Probe

Questions answered: `ELIN-Q-0009`, `ELIN-Q-0010`, `ELIN-Q-0011`, `ELIN-Q-0012`, `API-026`, `API-029`.

Setup: `DISPOSABLE SAVE REQUIRED`. Use a save with a Home and a generated/test Chara, not an important/story/party actor.

Player/tool actions:

1. Log `FactionBranch.members`, `CountMembers(Default,false)`, `MaxPopulation`, `elements`, `owner.uid`, and test Chara global/faction/home state.
2. Call `FactionBranch.AddMemeber(Chara)` once.
3. Re-read members, counts, capacity, efficiency, work elements, faction, home zone, and global status.
4. For absence, try `MoveZone(Zone, EnterState.RandomVisit)` only on an already-global generated/test Chara.
5. Re-read `currentZone`, active map membership, `global.transition`, global registry presence, then save/reload and re-read same uid.

Expected interpretations:

- `AddMemeber` can be enabled only if live nonzero admission persists and metrics/jobs refresh as source analysis predicts.
- Grade-B absence should remain limited to already-global actors unless the probe proves a safe path for ordinary non-global citizens.

Estimated human interaction time: 10-15 minutes.

## Session D: Disposable Additive Site Mutation Probe (BQ-143)

Questions answered: `ELIN-Q-0032`, `ELIN-Q-0033`, and the live half of BQ-143's done-when.

Setup: `DISPOSABLE SAVE REQUIRED`. This probe writes terrain into a map the game has already
generated. Do not run it on a save anybody cares about, and do not run it in a vanilla town: a
BQ-owned place is the only legitimate target.

Prerequisite: `ELIN-Q-0032` must be answered first. There is nothing to add to until a zone this mod
owns can be created and authored pieces applied into it, so Session D begins where that probe ends,
on the place it made.

Player/tool actions:

1. Log the created zone's uid, bounds, and the coordinates BQ's own site grid is lined up against.
2. Read the ground for one candidate footprint before writing: what `EClass._map.things`, the tile
   and any block/floor/obstacle read report for every tile in it.
3. Have the player change one adjacent patch by hand - build, dig, or drop something on it - and
   read that footprint again. The read must come back different from the untouched one; if it does
   not, `InspectGround` cannot be advertised whatever the write does.
4. Apply one authored piece into the free footprint.
5. Re-read the zone: the piece is there, and every preexisting actor, item, and piece of evidence in
   the place is still there and still where it was.
6. Save, quit, reload. Re-read. Leave the zone, re-enter, re-read.
7. Advance several in-game days, save, quit, reload, and re-read a second time.
8. Watch an NPC path through and around the addition, and any service or work behaviour near it.
9. Disable the BQ plugin, load the same save, and record what the zone looks like without it.

Log values:

- Zone uid, map bounds, site-grid origin used, footprint coordinates.
- Ground read per tile before and after the player's own change.
- Piece application call, its return, and any exception.
- Actor/item/evidence inventory of the zone before the addition and after each reload.
- Count of the added piece after each reload - the number that must be exactly one.
- NPC path and goal behaviour near the addition; any pathing failure or stuck actor.
- What the save does with the addition when BQ is not loaded.

Expected interpretations:

- `AddPlaceFixture` may be advertised only if the ground read distinguishes player-changed ground
  from free ground, the write lands, and the addition is present exactly once after both reloads.
- A single reload is not enough: BQ-143's done-when asks for save/reload, leave/re-enter, elapsed
  days, and a second save/reload, because an addition that is reapplied on load looks correct after
  the first one.
- Anything that cannot be read stays `Unknown`. An adapter that reports free ground it has not
  looked at is worse than one that refuses.

Disposable save required: yes.

Estimated human interaction time: 20-30 minutes.
