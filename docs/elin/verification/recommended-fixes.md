# Recommended Elin Integration Fixes

These fixes are research outputs only. Do not implement them as part of the API audit unless a later task explicitly takes the fix.

## FIX-ELIN-001

Title: Resolve Grade-B absence through the real `MoveZone` overload.

Status: IMPLEMENTED in adapter; STATICALLY VALIDATED by plugin build and headless reflection-shape tests; RUNTIME VALIDATION STILL REQUIRED for nonzero movement/save-load behavior.

Affected BQ code: `src/BrilliantQuesting.Plugin/ElinPresence.cs`, `ResolveMove`, `TryMove`, `ResolvedMembers`.

Problem fixed: BQ previously searched for `Chara.MoveZone(Zone)`, `SetZone(Zone)`, or `ChangeZone(Zone)`. EA 23.338 Patch 2 has none of those.

Evidence: Installed `Chara` exposes `MoveZone(string)`, `MoveZone(Zone, ZoneTransition.EnterState)`, and `MoveZone(Zone, ZoneTransition)` (`VERIFIED-METADATA`). `MoveZone(Zone, EnterState)` delegates to the `ZoneTransition` overload. The non-PC path returns without moving when `chara.global == null`; inactive-zone movement stores `global.transition`, while active-zone movement clears transition and calls `Zone.AddCard(chara, pos)` (`SOURCE-OBSERVED`). `SpatialManager.Find(int)` exists (`VERIFIED-METADATA`).

Recommended fix: Resolve `MoveZone(Zone, ZoneTransition.EnterState)` and invoke with a conservative state such as `RandomVisit` or `Auto` after confirming the actor is global. Refuse non-global ordinary NPCs until a disposable-save probe proves a safe mechanism. Keep post-call verification by reading `chara.currentZone.uid`.

Safety: Potentially save-affecting relocation. Requires mutation policy `TemporaryAbsence` and disposable-save validation before nonzero use.

Validation: Unit test resolver against stubs with two-argument overload; runtime Session C for nonzero movement and save/load.

Blocks: BQ-032 and any future physical absence workflow.

Priority: Critical.

## FIX-ELIN-002

Title: Use vanilla Home `AddMemeber` and `MaxPopulation`.

Status: IMPLEMENTED in adapter; STATICALLY VALIDATED by plugin build and headless reflection-shape tests; RUNTIME VALIDATION STILL REQUIRED for nonzero Home admission.

Affected BQ code: `src/BrilliantQuesting.Plugin/ElinHomeState.cs`, `CapacityNames`, `AdmitNames`, `TryAdmitResident`.

Problem fixed: BQ previously searched for `AddMember`, `AddResident`, `AddChara`, and capacity names like `maxResident`/`capacity`. Installed vanilla uses misspelled `AddMemeber(Chara)` and `MaxPopulation`.

Evidence: `FactionBranch.MaxPopulation` returns `5 + Evalue(2204)`, where SourceElement `2204` is `fFood` (`SOURCE-OBSERVED`, `SOURCE-DATA`). `FactionBranch.AddMemeber(Chara)` removes prior Home branch membership, removes reserve state, calls `SetGlobal`, sets Home faction/zone, normalizes hostility/member type, adds to `members`, calls `OnAddMemeber`, `RefreshEfficiency`, and `RefreshWorkElements` (`SOURCE-OBSERVED`). Vanilla `Quest.AddResident`, `Game.OnLoad`, `Zone.ClaimZone`, and `FactionBranch.Recruit` call it.

Recommended fix: Add `MaxPopulation` as the capacity member. Add `AddMemeber` as the preferred admission method. Use `Recruit(Chara)` only for hire/recruit presentation flows, not generic BQ shelter admission. Re-read `members`, `CountMembers(Default,false)`, `MaxPopulation`, efficiency, and work elements after mutation.

Safety: Potentially save-affecting Home mutation.

Validation: Stub resolver tests plus runtime Session C on a disposable Home save.

Blocks: BQ-030 follow-ups, BQ-048/BQ-049 Home actions, settlement-affordance generation that depends on residents.

Priority: Critical.

## FIX-ELIN-003

Title: Tighten actor classification with confirmed card and trait semantics.

Status: IMPLEMENTED in adapter; STATICALLY VALIDATED by plugin build and headless reflection-shape tests; RUNTIME VALIDATION STILL REQUIRED for live population threshold sampling.

Affected BQ code: `src/BrilliantQuesting.Plugin/ElinActorClasses.cs`.

Problem fixed: The classifier omitted confirmed important/unique signals and could not distinguish safe ordinary actors from story/unique actors with enough confidence for relocation.

Evidence: `Card.IsUnique` is `rarity == Rarity.Artifact`; `Card.IsImportant` is `sourceCard.HasTag(CTAG.important)`; `c_isImportant` and `c_uniqueData` are persisted card data; `TraitUniqueChara`, adventurer lists, quest state, and Home/party/faction state are used by vanilla removal/banishment code (`VERIFIED-METADATA`, `SOURCE-OBSERVED`).

Recommended fix: Classify PC/party as never relocatable. Mark `TraitUniqueChara`, `IsUnique`, `c_uniqueData != null`, adventurers, `IsImportant`, `c_isImportant`, active quest holders, and unknown story/service rows as unsafe. Only classify BQ-generated actors as generated/safe when BQ owns their creation. Treat ordinary non-global town citizens as unsafe for Grade-B absence until movement runtime evidence exists.

Safety: Read-only classification; affects later mutation gates.

Validation: Stub tests for each signal plus runtime Session A actor samples.

Blocks: BQ-031, BQ-032, death/removal/relocation policies.

Priority: High.

## FIX-ELIN-004

Title: Stop using `rarity` as production quality.

Status: IMPLEMENTED in adapter; STATICALLY VALIDATED by plugin build and headless reflection-shape tests; optional live comparison still useful for crafted item samples.

Affected BQ code: `src/BrilliantQuesting.Plugin/ElinVanillaState.cs`, item descriptor/quality reads; future production observation.

Problem fixed: Runtime previously selected `Thing.rarity` because it was a readable candidate, but vanilla made-quality semantics are not rarity.

Evidence: `Card.GetTotalQuality(true)` combines level, material quality, and `Card.Quality`; `ChangeRarity` only changes rarity. Production/crafting quality should not be inferred from rarity alone (`SOURCE-OBSERVED`).

Recommended fix: For "well-made" or property-constrained produced items, read `Card.GetTotalQuality(true)` or explicitly model `Card.Quality`, `LV`, and material quality. Reserve rarity for rare/artifact semantics.

Safety: Read-only.

Validation: Unit tests around quality adapter shape; optional live comparison of crafted items in Session B or a later production probe.

Blocks: BQ-050/BQ-051 production quality, item-demand routes.

Priority: High.

## FIX-ELIN-005

Title: Handle transfer stack merging and returned item identity.

Status: IMPLEMENTED in adapter; STATICALLY VALIDATED by plugin build and headless reflection-shape tests; RUNTIME VALIDATION STILL REQUIRED for nonzero transfer, stack-merge behavior, and save/load persistence.

Affected BQ code: `src/BrilliantQuesting.Plugin/ElinVanillaState.cs`, `TryTransferItem`; inventory binding code.

Problem: BQ verifies transfer by re-reading the original item id. Vanilla `Chara.Pick` may merge into an existing destination stack through `Thing.TryStackTo`, destroy the source stack, and return the destination stack.

Evidence: `Chara.Pick(Thing,bool,bool)` finds destination containers/stacks, may call `TryStackTo(destination)`, and returns the destination `Thing` after stacking; `TryStackTo` destroys the source stack after merging counts and flags (`SOURCE-OBSERVED`).

Implemented fix: Capture the returned `Thing`. If its uid differs from the original, update BQ binding and verify success by re-reading the current binding at the destination rather than requiring the original uid to survive.

Safety: Potentially save-affecting inventory mutation.

Validation: Plugin build plus runtime Session B. A focused returned-different-uid adapter test still requires plugin-test infrastructure or an extracted transfer verifier.

Blocks: item return/steal/plant/fence/evidence workflows.

Priority: High.

## FIX-ELIN-006

Title: Do not rely on `ActPerformed` for chat or production output.

Status: IMPLEMENTED in adapter; STATICALLY VALIDATED by plugin build and exact-field reflection tests; RUNTIME VALIDATION STILL REQUIRED for representative live `ActPerformed` payload samples.

Affected BQ code: `src/BrilliantQuesting.Plugin/ElinActionObserver.cs`, production-name matching and action classification.

Problem fixed: BQ tried to infer production/crafting from `ActPerformed` act names and reflective payloads. Installed vanilla publishes `ActPerformed` only when `Act.Perform()` returns true, and representative production creation paths do not publish through that event.

Evidence: `Act.Perform(Chara,Card,Point)` publishes `EVENT.ActPerformed` with `this` after successful perform (`SOURCE-OBSERVED`). `ActChat.Perform()` opens dialogue and returns false. `Recipe.Craft`, `RecipeCard.Craft/Build`, `TraitCrafter.Craft`, `AI_UseCrafter.OnEnd`, harvest, grow, and mining creation paths create things without the `ActPerformed` publish wrapper (`SOURCE-OBSERVED`).

Implemented fix: Keep `ActPerformed` for theft-like and hostile true `Act` completions. Production inference from `ActPerformed` is removed; production observation remains unsupported until a real hook is identified.

Safety: Read-only observer change, but affects provenance quality.

Validation: Runtime Session A for act payload samples; later production-specific probe when implementing BQ-050.

Blocks: BQ-050/BQ-051 production provenance, action observation confidence.

Priority: High.

## FIX-ELIN-007

Title: Read real guild rank/progression instead of binary membership.

Status: IMPLEMENTED in adapter; STATICALLY VALIDATED by plugin build and headless reflection-shape tests; live guild-member sample still useful.

Affected BQ code: `src/BrilliantQuesting.Plugin/ElinVanillaState.cs`, `GetGuildRank`.

Problem fixed: BQ previously returned `1` for guild member and `0` otherwise. Installed vanilla has numeric rank/progression fields and uses them for UI, salaries, service prices, and benefits.

Evidence: `Guild.IsMember` checks `Faction.relation.type == 2`. `FactionRelation` exposes `rank`, `exp`, `ExpToNext`, `GetSalary`, `TextTitle`, and `Promote`. `QuestGuild.GetDetailText` displays rank/contribution/salary and benefit thresholds; guild service methods read `relation.rank` (`VERIFIED-METADATA`, `SOURCE-OBSERVED`).

Recommended fix: When `Guild.IsMember` is true, read `guild.relation.rank`. Optionally expose contribution/exp-to-next separately rather than overloading rank. Preserve binary `IsGuildMember` for simple gates.

Safety: Read-only.

Validation: Stub tests plus one runtime read on a guild-member save.

Blocks: BQ-037/BQ-038, guild-gated route difficulty, future faction affordances.

Priority: High.

Taken: `BQ-038`. `GetGuildRank` now reads `guild.relation.rank` and a separate `GetGuildContribution` reads `guild.relation.exp`; `IsGuildMember` is unchanged. Still `SOURCE-OBSERVED` - the member names have not been exercised on a running game, and the plugin could not be compiled where the change was made.

## FIX-ELIN-008

Title: Add active-zone loose item inventory surface.

Affected BQ code: `src/BrilliantQuesting.Plugin/ElinVanillaState.cs`, `GetInventory(EntityId holder)`.

Problem: BQ can enumerate character inventories but not loose zone items, so evidence/repair/search flows miss items on the ground.

Evidence: Installed metadata confirms `EClass._map.things` and Phase 2 source analysis identifies it as the active loaded map's loose item collection (`VERIFIED-METADATA`, `SOURCE-OBSERVED`). Current `GetInventory(zone)` path only resolves `Chara` holders.

Recommended fix: When holder id matches the active `zone_<EClass._zone.uid>`, enumerate `EClass._map.things` and project the same item descriptor fields used for character inventories. Do not claim arbitrary unloaded-zone item inventory until a separate zone persistence adapter exists.

Safety: Read-only.

Validation: Runtime Session A or B logs active map item count/category ids.

Blocks: BQ-017/BQ-026 evidence search/repair, BQ-039 local item availability.

Priority: Medium.
