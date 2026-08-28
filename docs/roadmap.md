# Roadmap

Phases and gates follow the master design document. This file tracks what is actually done.

## Phase 0 - reverse-engineering spike

Assemblies from a live install are now readable locally, and `tools/ApiDump` reads their metadata.
Findings are written up in [elin-api-notes.md](elin-api-notes.md). Metadata proves a member exists;
it does not prove it behaves, so "found" below is not the same as "works".

| Item | Status |
|---|---|
| Confirm runtime access to elements, skills, affinity, Karma, prestige, Influence, inventory | **found**; elements/skills blocked on the alias table, the rest not yet exercised in game |
| Confirm access to guild, faith, home state | partly - `EClass.Home` / `.Branch` located, members not yet mapped |
| Confirm creation and persistence of generated Chara and zones | partly - `Zone.AddChara` located, persistence untested |
| Decide how mod save data attaches, with a migration version | **working in game** - chunk written on `PreSave`, read on `PostLoad` |
| Prototype custom `Check` rows and `Check.Perform` from runtime | **found, untested** - `Check.Get/GetFinalDC/Perform` and the four-result enum exist; `SourceCheck.Row` schema captured |
| Prototype Drama choice injection | hook located (`EVENT.DramaParseAction`), not yet used |
| Confirm crime/witness hooks | hook located (`EVENT.ActPerformed`), not yet used |

`src/BrilliantQuesting.Plugin` implements `IVanillaState`, `ICheckResolver` and `ISituationStager`
against the real assemblies. **It now loads and runs inside Elin.** Observed on 28 Aug 2026:

```
Brilliant Questing 0.1.0 loaded. Waiting for a game.
No saved world; starting a new one from seed 7525.
Vanilla capabilities: 9 of 13
Simulation attached: 0 people, 0 events, 0 threads.
Saved 0 events into chunk 'brilliantQuesting'.
Simulation detached.
```

That confirms three things metadata could only suggest: the event bus delivers `PostLoad` and
`PreSave`, `GameIOContext` writes the mod's chunk into the save, and the capability probe reports
honestly rather than assuming. It does not yet confirm any read or write of vanilla state, because
the element aliases were all unresolved on that run and the stat capabilities correctly switched
themselves off.

**Gate A** - a hard-coded scenario reads vanilla stats, performs a native-style check, updates
affinity and Karma, saves, reloads and continues correctly.
*Passed headless; partly passed in game.* The plugin loads, attaches, persists and detaches against
a real save. Reading and writing vanilla state is next, and is blocked only on the element aliases.

## Phase 1 - three-NPC simulation laboratory

Done. `PettyTheftSituation` generates three persistent, motivated characters, an object, a fact
graph and an escalation schedule. Twelve verbs act on it: question, persuade, lie, intimidate,
bribe, search, expose, pickpocket, frame, return, keep, attack.

**Gate B** - let ten or more in-game days pass through multiple outcomes; the resulting state must
be explainable, persistent, replayable and interesting.
*Passed.* Asserted in `tests/GateBTests.cs` and demonstrated by `tools/BrilliantQuesting.Lab`:

- Solving it early resolves the thread and the feud never happens.
- Ignoring it entirely moves the evidence, spreads an unprovable rumour, produces a false
  accusation and ends in two households that no longer speak.
- The same seed replays the same story; a different seed does not.
- Every memory an NPC holds corresponds to an event that actually happened.

## Phase 2 - persistent thread and generated site

Not started. Needs the Phase 0 spike first: a site is only interesting once it is a real Elin zone.

- Generate a site through native zone infrastructure and decorate it
- Project a situation into the journal and into dialogue
- Verify consequences survive a site unload and reload

## Phase 3 - universal action library

Twelve of a target thirty. Each new verb needs testable preconditions, real vanilla mechanics,
four-outcome behaviour where checked, exposure rules and consequence outputs. Families still thin or
absent: Crafting, MagicFaith, HomeCommunity, and most of Economic beyond bribery.

## Phase 4 - director and multiple archetypes

Not started. One archetype exists (`petty_theft`). The director does not exist at all; nothing
currently decides which situation deserves the player's attention, because with one situation there
is nothing to decide.

## Phase 5 - organizations and off-screen development

`Organization` is modelled and persisted but nothing simulates it yet. Simulation tiers
(active/warm/cold/archived) are designed, not implemented.

## Phase 6 - scale, polish, optional richer text

Not started, and deliberately last.

## Standing constraints

- No LLM in the authoritative path, ever. Optional prose rendering may come later; it may not decide
  state, checks or consequences.
- No duplicate persuasion, reputation, morality, housing or guild systems.
- Every new situation archetype answers the review checklist in the design document, including:
  does it support three distinct solution families, what happens on a critical failure, and what
  happens if the player ignores it entirely.
