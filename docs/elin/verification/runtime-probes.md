# Runtime Probes

Static Phase 2 reduced the probe list. Remaining probes validate live UI behavior, actor populations, and nonzero save-affecting mutations. Do not upgrade any item to `VERIFIED-RUNTIME` unless the installed game log shows the probe ran successfully.

## BQ-109 capability degradation observation

### Drill implementation and procedure

`[Debug] DisabledCapability` in `BepInEx/config/elin.brilliant.questing.cfg` accepts one
`VanillaCapability` name, case-insensitively. Empty (the default) uses ordinary detection.
Restart Elin after each change. Numeric values, lists and unknown names are rejected with an
`INVALID` diagnostic and leave normal detection active; such a run is not a degradation test.
The selected native probe is skipped, support is reported unavailable, direct reads use their
existing unavailable fallback, and writes retain their existing refusal gates. Witness collection
also honors its capability. Home's normal fresh-read behavior is retained when the drill is off.
No drill selection or probe result is written into BQ saves.

The actual Plugin probe/report component is exercised by
[CapabilityDegradationTests](../../../tests/BrilliantQuesting.Core.Tests/CapabilityDegradationTests.cs)
for every enum member, repeated detection, skipped delegates, invalid settings, failed probes and
already-unsupported capabilities. Core action discovery is exercised using the existing theft
sandbox. These are **HEADLESS-ONLY** checks, not observations of feature isolation in Elin.

1. Build/install using the [Plugin procedure](../../plugin-build.md). Use a disposable copy of a
   save with relevant BQ situations. Keep other mod/settings unchanged throughout the drill.
2. With `DisabledCapability =` empty, restart, load the copy and preserve the full capability
   report from `BepInEx/LogOutput.log`. Record game version, Plugin build/commit and enabled mods.
3. For each row below, restore the same baseline save copy, set `DisabledCapability` to that row's
   name, restart and load. Confirm the `disabled by BQ-109 drill` report, and compare all other
   capability lines against baseline. A log line alone does not prove the affected feature behaved.
4. Exercise the affected route and an unrelated BQ route. Walk/perform ordinary actions, open
   generic and authored dialogue and the journal, change zones, then save/reload the test copy.
   Record actual behavior and errors, including unavailable choices or refused mutations.
   Capability-dependent routes may share a capability; do not require exactly one UI option to vanish.
5. Preserve each run's log before the next launch. Record baseline support, disabled name,
   diagnostic, affected route, unrelated route, transition/reload result and exceptions.
   Mark an unexercised route unresolved. For already-unavailable capabilities record the baseline
   reason and unchanged fallback; do not claim a demonstrated loss of a working feature.
6. Clear `DisabledCapability`, restart and confirm normal detection/behavior on the baseline copy.

Expected scope below is a test prescription, not a result. The three-pass capture below verifies
selected disable diagnostics and attach/save behavior; full gameplay isolation remains unverified.

| Disabled capability | Expected affected scope to exercise |
|---|---|
| `ReadAttributes` | Attribute-backed checks use the adapter's unavailable read fallback |
| `ReadSkills` | Skill-backed checks use the adapter's unavailable read fallback |
| `ReadWriteAffinity` | Affinity reads fall back; BQ affinity writes are refused |
| `ReadWriteKarma` | Karma reads fall back; BQ karma writes are refused |
| `ReadWriteFame` | Fame reads fall back; BQ fame writes are refused |
| `ReadWriteInfluence` | Influence reads fall back; BQ influence writes are refused |
| `ReadGuildRank` | Guild membership/rank/contribution reads fall back |
| `ReadFaith` | Deity/piety reads fall back |
| `ReadInventory` | Inventory snapshots are unavailable to BQ; vanilla inventory remains usable |
| `ReadPlaceContents` | Place-content-dependent routes remain unavailable on the current adapter |
| `TransferItems` | BQ item transfer is refused without moving the item |
| `DestroyItems` | BQ item destruction is refused without removing the item |
| `SpendMoney` | BQ payment is refused without debiting/crediting either party |
| `ReadHomeState` | Home snapshot is unavailable; no inferred empty Home or resident removal |
| `ReadCharacterIdentity` | Identity facets are unknown; presence and conversation remain possible |
| `ReadActorActivity` | Activity facets are unknown; no inferred idle/travel state |
| `WriteHomeResidents` | BQ resident admission is refused; Home reads remain available |
| `MoveCharaBetweenZones` | BQ relocation is refused; ordinary vanilla travel remains usable |
| `ObserveCrimeWitnesses` | Observed actions collect no BQ witnesses; vanilla crime resolution remains owned by vanilla |
| `ReadPlayerCompanions` | Companion enumeration is unavailable; Home enumeration remains separate |
| `BuildPlaceStructure` | Structured-site creation remains unavailable on the current adapter |
| `AddPlaceFixture` | Additive-site mutation remains unavailable on the current adapter |

### Reported observation

User report received September 10, 2026: losing capabilities does not seem to cause instability.
This records the user's qualitative observation; no capability list, disabling procedure, game/plugin
build, observation duration or supporting log was supplied with the report. It does not establish
that every capability was disabled individually, that each loss affected only its intended feature,
or that the expected diagnostic appeared. No capability evidence grade is upgraded by this report.

BQ-109's live acceptance remains open. For each capability, record the baseline support state,
game/plugin build, how it was disabled, diagnostic, affected feature, behavior of unrelated features,
and any errors during the exercise. Capabilities already unavailable at baseline must be identified
as such rather than presented as demonstrated losses of working features.

### Three-pass live capture — September 10, 2026

Source: user-supplied `E:/SteamLibrary/steamapps/common/Elin/BepInEx/LogOutput.log`,
228,449 bytes, SHA-256 `3941d870d0212c15aee61277d515fa54abf265083ec0441fd68f0e3c38025ae5`.
The captured interval is September 11, 02:43:34–02:44:54 UTC (September 10 local time).
Plugin module ID `8a43a607-02c8-491a-9dd5-e2f838e7f9b6`, advertised BQ version 0.1.0,
Unity 2021.3.45f2. The logged Elin assembly version is `0.0.0.0`; this does not identify the
displayed game release. The log contains multiple enabled mods and three attach cycles within
one log; separate process restarts and a pristine restored baseline are not established.

| Selected disable | Log lines | Observed result |
|---|---|---|
| `ReadHomeState` | 356, 374, 383–385, 811–815 | Disable diagnostic; attach succeeds; Home is `unreadable` at attach and pre-save; two saves of 157 events |
| `ReadCharacterIdentity` | 884, 902, 910–912, 1357 | Disable diagnostic; attach succeeds; Home reads return with 22 residents; save of 157 events |
| `ObserveCrimeWitnesses` | 1428, 1449, 1453–1455, 1900 | Disable diagnostic; attach succeeds; Home and identity are available; save of 157 events, followed by quit/detach |

Each report contains all 22 capabilities. Apart from the selected disable, reported support is
consistent across passes: `ReadPlaceContents`, `MoveCharaBetweenZones`, `BuildPlaceStructure` and
`AddPlaceFixture` remain unavailable for their existing reasons. The other capabilities report
available. There is no all-enabled baseline capture. Each attach reports 313 people, 157 events
and one thread; the subsequent saves retain 157 events. No Error/Fatal entries or exception text
were found in this captured log. This is **VERIFIED-RUNTIME** evidence for the three disable
diagnostics and bounded attach/save survival, including the Home read fallback/restoration.

Every captured `Act`, `Dialogue`, `Witnesses` and `ZoneVisit` callback count is zero. Accordingly,
the log does not verify crime witness suppression during an action, dialogue behavior, zone
transition behavior, or full feature isolation. There is no post-final-save reload or final
all-enabled restoration in this capture. Long frame intervals occur around attach windows;
without a controlled baseline these do not establish either a regression or performance safety.
The remaining 19 disables and the unexercised gameplay checks keep BQ-109's full acceptance open.

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
   at least seven in-game days have elapsed, and return. Capture the same displays immediately, before an ordinary action or reload, and note
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
Additional scopes split Observe, DescribeAct, Witnesses, Record, Heartbeat, Ambient, ZoneIntake
and completed ZoneVisit. The latter should appear even on an action-free return.
They are inclusive, can nest, and must not be added together; other BQ hooks are outside this
attribution. A controlled baseline would still be needed to establish whole-mod frame impact.

`BQ-HOME` brackets the existing reconciliation call on attach/observed zone change and samples
before save. It reports fresh Home readback, game minute, zone IDs, event count and up to 64
resident IDs/presence/BQ clocks with an explicit sample count. Missing reads remain unknown.
Both sides are **after native zone entry**, not before/after `Zone.Simulate`; the corrected host runs intake/reconciliation before daily advance; the first capture preceded
that ordering correction. The logger never calls
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

## September 9 performance sample

The first log (hash/module in [Home evidence](../api/home-and-settlements.md#september-2026-identity-and-membership-correction))
contains 41,604 frame intervals, approximate weighted mean 6.542 ms, 28 intervals above 50 ms,
and max 1,681.614 ms. The maximum occurred in a report with no instrumented BQ callbacks and cannot
be attributed to BQ. Percentiles remain per-window, not averaged into global percentiles.
BQ Act: 666 calls, 882.660 ms inclusive total, max 22.769 ms. Two post-reload combat-heavy reports
average 6.605/7.224 ms per Act call. Attach max 261.620 ms, save max 34.584 ms, dialogue projection
max 1.027 ms. These include diagnostic overhead and are not incremental whole-mod frame cost.

BQ counts were 205 to 294 actors, 2 to 99 events, 2 facts, 1 thread. Large BQ history and a controlled
baseline remain untested. Phase notes and CPU/GPU/RAM models were not supplied. Graphics show
VSync/fullscreen/Unlimited and no post-effect profile, but 1280x768 fields versus 2560x1440 Steam
screenshots do not establish render resolution. Elin assembly version 0.0.0.0 is not its displayed
release version. BQ-108 remains open.

After correction, a focused follow-up should show `home_zone=zone_7` matching the player zone,
a Home name and registered active-member clocks advanced to the sampled minute on return,
then preserved on reload. Keep the full 22-member roll if membership is unchanged. Repeat a
combat-heavy segment; new nested timing scopes identify remaining costs. New observed combat
records alone are not evidence of save replay.

Use a pre-capture save copy, if available, to test background combat admission: old combat can
already have promoted participants, and the correction deliberately preserves those saved promotions.

## September 10 follow-up performance sample

The [follow-up Home evidence](../api/home-and-settlements.md#september-10-follow-up-attach-verified-return-hook-incomplete)
identifies the log/module. 23,096 frame intervals have approximate weighted mean 6.871 ms,
38 intervals above 50 ms and max 1,928.052 ms. Workload/phase differences and lack of baseline
prevent a performance-improvement claim against September 9.

Act: 145 callbacks, 564.440 ms inclusive total, max 28.291 ms. Record: 58 scopes, 538.863 ms total,
mean 9.291 ms, max 26.247 ms (about 95.5% of Act total). Witnesses: 58 scopes, 3.663 ms total,
mean 0.063 ms, max 0.479 ms. Do not sum nested timings. The expensive region is recording plus
synchronous event dispatch/consequences; the individual subscriber/algorithm is not yet identified.
The run starts with the prior 99 events and reaches 157, so previous combat promotions remain a
confound for admission comparisons. Actor count reaches 310; large-history and baseline gates remain
open. Investigate the missing Home return trigger and Record cost before repeating the same capture.

## BQ-108 scoped correction and synthetic measurement

The reaction path now skips social-practice detectors whose existing bearing table cannot affect
the event. An attack still reads mourning, but no longer performs commerce identity or household
reads. Tests compare all event types across ordinary, mourning, contest, shop, assembly, household
and stacked contexts; norm values and explanatory terms are identical. Native-call tests confirm
unused reads are absent. This is a demonstrated waste reduction, not proof that it accounts for
all of the live Record cost above. The completed-visit correction is documented in
[Home evidence](../api/home-and-settlements.md#post-visit-reconciliation-correction).

On September 10, the Release `performance` Lab run restored 20,022 actors and 100,000 recent
unrelated events with 22 local people. Each measurement excludes setup/restore and ten warm-ups,
then samples 100 calls using Stopwatch and current-thread allocation counts on .NET 8/Windows.
Witnessed dispatch appends new attacks through the production consequence engine; it does not
redispatch restored history. This fixture stresses recent-history scans, not every save topology.

| Measured operation | Mean ms | p95 ms | Max ms | Bytes/call |
|---|---:|---:|---:|---:|
| Full practices plus attack norm | 0.842 | 1.158 | 1.520 | 19,152 |
| Event-scoped attack norm | 0.414 | 0.574 | 0.699 | 1,312 |
| Witnessed attack dispatch | 0.454 | 0.646 | 0.902 | 19,177 |

These are one-machine observations, not portable thresholds or game frame measurements. Sandbox
identity queries omit native reflection costs. Existing recent-history scans remain linear inside
the time window; the change does not claim constant-time history access. No new provenance store,
rumour authority, director or dialogue realization policy was introduced.

**Acceptance still pending:** a post-change large-save live capture with phase notes and a comparable
baseline, plus Home revisit after at least seven in-game days and reload. For comparing this change,
use copies of the same pre-run save, identical settings and workload with both diagnostic builds;
whole-mod impact additionally needs externally measured BQ-disabled frame cadence. Do not resave
the original with BQ disabled. Existing frame intervals alone cannot establish incremental impact.
