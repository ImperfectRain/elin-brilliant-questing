> **Superseded for planning.** [`implementation-roadmap.md`](implementation-roadmap.md) is the
> single ordered plan. This file is kept as the historical record of the original phase structure
> and the evidence for Gates A and B.

# Roadmap

Phases and gates follow the master design document. This file tracks what is actually done.

## Phase 0 - reverse-engineering spike

Assemblies from a live install are now readable locally, and `tools/ApiDump` reads their metadata.
Findings are written up in [elin-api-notes.md](elin-api-notes.md). Metadata proves a member exists;
it does not prove it behaves, so "found" below is not the same as "works".

| Item | Status |
|---|---|
| Confirm runtime access to elements, skills, affinity, Karma, prestige, Influence, inventory | **verified in game** - real values read back through the adapter |
| Confirm access to guild, faith, home state | partly - guild and faith verified in game; the Home is read through `EClass.Branch`, and residents are added through it, but every member below it is resolved by name and unconfirmed in play |
| Confirm creation and persistence of generated Chara and zones | partly - `Zone.AddChara` located, persistence untested |
| Decide how mod save data attaches, with a migration version | **working in game** - chunk written on `PreSave`, read on `PostLoad` |
| Prototype custom `Check` rows and `Check.Perform` from runtime | **found, untested** - `Check.Get/GetFinalDC/Perform` and the four-result enum exist; `SourceCheck.Row` schema captured |
| Prototype Drama choice injection | **working in game** - the current Elin build exposes `EVENT.DramaParseAction` but does not publish it, so the prototype injects into default Drama talk and labels procedural choices with `BQ:` |
| Confirm crime/witness hooks | hook located (`EVENT.ActPerformed`), not yet used |

`src/BrilliantQuesting.Plugin` implements `IVanillaState`, `ICheckResolver` and `ISituationStager`
against the real assemblies. **It now loads and runs inside Elin.** Observed on 28 Aug 2026:

```
Resolved all 23 element aliases.
Vanilla capabilities: 11 of 13
player: Solid Fairy 「Gandadaora」  level 32  karma 100  fame 1197  207664 orens  29 items
attributes: STR 52  END 33  DEX 42  PER 31  LER 62  WIL 31  MAG 15  CHA 24
skills: negotiation 58  stealth 10  pickpocket 0  spotHidden 0  literacy 30  appraising 15
standing: deity 'harvest'  piety 54  guilds F/M/T/Me True/True/False/True
Saved 0 events into chunk 'brilliantQuesting'.
```

Every number matches the character sheet, which verifies the whole chain - alias, element id,
`ElementContainer`, `IVanillaState` - rather than merely proving the calls did not throw. The event
bus delivers `PostLoad` and `PreSave`, and `GameIOContext` writes the mod's chunk into the save.

The one wrong number was Influence, reading zero on a character with fame 1197. `expInfluence` is
not town Influence; the spendable resource is a currency. Fixed, and pending re-verification.

**Gate A** - a staged scenario reads vanilla stats, offers actions through dialogue, performs a
native-style check, updates affinity and Karma, saves, reloads and continues correctly.
*Passed headless; partly passed in game.* The plugin loads, attaches, persists and detaches against
a real save. The dialogue prototype routes choices through the existing action library, records
varied outcomes, and now announces the case in game. The remaining Gate A risk is journal
integration: `LayerJournal`, `ContentQuest`, `QuestManager`, and `Quest` exist, but a first-class
Brilliant Questing tab still needs a focused UI spike.

Live playtest on 28 Aug 2026 confirmed a janky but complete resolution path after the perpetrator
was killed: the compatibility repair spawned missing evidence near the player, search recovered it
as provable, return-item handed it to the victim, and the thread resolved as `property_returned`.
The log saved 13 events into the `brilliantQuesting` chunk. Remaining rough edges are item naming
(`a bronze silver ring`), dead-NPC binding, procedural dialogue flavor, and journal tracking.

## Phase 1 - three-NPC simulation laboratory

Done. `PettyTheftSituation` generates three persistent, motivated characters, an object, a fact
graph and an escalation schedule. Thirteen verbs act on it: build rapport, question, persuade,
lie, intimidate, bribe, search, expose, pickpocket, frame, return, keep, attack.

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

Fifty-seven verbs against a target thirty, and all eight solution families now have at least one
real route. Each verb needs testable preconditions, real vanilla mechanics, four-outcome behaviour
where checked, exposure rules and consequence outputs; the count is not the measure - a verb counts
only once some situation can be solved by it alone.

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
