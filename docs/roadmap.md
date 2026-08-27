# Roadmap

Phases and gates follow the master design document. This file tracks what is actually done.

## Phase 0 - reverse-engineering spike

| Item | Status |
|---|---|
| Confirm runtime access to elements, skills, affinity, Karma, prestige, Influence, guild, faith, home, inventory | **not started** - needs a current Elin build |
| Confirm creation and persistence of generated Chara and zones | **not started** |
| Decide how mod save data attaches, with a migration version | design done, `WorldStateSerializer` + `SaveMigrations`; attachment point not chosen |
| Prototype custom `Check` rows and `Check.Perform` from runtime | modelled by `VanillaStyleCheckResolver`; the native path is unproven |
| Prototype Drama choice injection | **not started** |
| Confirm crime/witness hooks | modelled behind `VanillaCapability.ObserveCrimeWitnesses`; the real hook is unproven |

**Gate A** - a hard-coded scenario reads vanilla stats, performs a native-style check, updates
affinity and Karma, saves, reloads and continues correctly.
*Passed headless.* The same scenario against a running game is what the spike above is for.

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
