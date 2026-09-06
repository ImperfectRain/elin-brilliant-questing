# Runtime Probes

Static Phase 2 reduced the probe list. Remaining probes validate live UI behavior, actor populations, and nonzero save-affecting mutations. Do not upgrade any item to `VERIFIED-RUNTIME` unless the installed game log shows the probe ran successfully.

## Session A: Safe Read-Only Shape And UI Probe

Questions answered: `ELIN-Q-0013`, `ELIN-Q-0014`, `ELIN-Q-0015`, `ELIN-Q-0017`, `ELIN-Q-0018`, `ELIN-Q-0020`, `ELIN-Q-0021`, `ELIN-Q-0023`, `ELIN-Q-0024`.

Setup: ordinary save, debug logging enabled, no vanilla mutation.

Player actions: load into a town/Home, talk to at least one ordinary NPC and one service/story/guild NPC, open journal, advance one hour or revisit a zone if convenient.

Log values:

- Actor sample: uid, source id/name, trait type, source tags, `IsUnique`, `IsImportant`, `c_uniqueData != null`, `c_isImportant`, `quest != null`, `IsGlobal`, `IsHomeMember`, `IsBranchMember`, BQ classifier result.
- Activity sample: `idTimeTable`, current span/goal type, work/hobby goal type, `TraitChara.UseGlobalGoal`, `global.goal` type, `global.transition` state/coordinates/last-zone uid, current zone uid, active-zone match.
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
- `ActPerformed` remains observation-only for act payloads; production still needs separate hook evidence if no production act publishes.

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
