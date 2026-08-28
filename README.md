# Elin Brilliant Questing

A persistent, simulation-driven questing layer for [Elin](https://store.steampowered.com/app/2135150/Elin/).

The premise, from the [master design document](docs/design/master-design.md): **generate persistent
situations, not disposable quests.** The simulation decides what is happening; vanilla Elin decides
what the player can do about it. Attributes, skills, checks, affinity, Karma, fame, town Influence,
guild rank, faith, inventory, crime witnesses, combat and the player's own land are the interface -
not a parallel roleplaying ruleset bolted on beside them.

> Never create a procedural solution when an existing Elin mechanic can constitute that solution.

## Current status

This repository contains the **simulation core and its headless laboratory**. It corresponds to
Phase 0 and Phase 1 of the roadmap: the parts that had to be proven before any game integration was
worth writing.

| | |
|---|---|
| Simulation core | implemented, 46 passing tests |
| Three-NPC laboratory (Gate B) | implemented, runs headless |
| Save / load / schema migration | implemented |
| Elin runtime adapter (BepInEx plugin) | **not written yet** - see below |

There is deliberately no `Elin.dll` reference anywhere in this repository. Everything the simulation
needs from the game is expressed as the [`IVanillaState`](src/BrilliantQuesting.Core/Integration/IVanillaState.cs)
interface, with a headless [`SandboxVanillaState`](src/BrilliantQuesting.Core/Integration/SandboxVanillaState.cs)
implementing the same contract. That is what lets the whole thing be built and tested with no game
process, no game assets and no decompiled code in the tree - and it means the in-game adapter, when
it arrives, is one file to write and one file to repair when Early Access moves under us.

## Try it

```bash
dotnet test                                          # 46 tests, ~150ms
dotnet run --project tools/BrilliantQuesting.Lab     # the laboratory, with its reasoning shown
dotnet run --project tools/BrilliantQuesting.Lab -- --questline 15        # one seeded run, day by day
dotnet run --project tools/BrilliantQuesting.Lab -- --questline-sweep 60  # the same policy over 60 seeds
```

`--questline` plays a generated situation end to end with the real dice, one in-game day at a time.
The player is not a script: each day a policy asks the world what is currently possible and what the
player currently knows, and picks the most sensible thing available - so the player's moves and the
situation's own escalation interleave. On an idle day it prints what the player wanted and what the
world said about it, which is usually where the missing verb is.

The laboratory generates a small situation - A stole something from B, C saw it - and plays it two
ways. Abridged output:

```
fact: Rulf stole silver ring
  Rulf        Participant confidence 1.00  (can prove)
  Nessa       Witnessed   confidence 1.00  (cannot prove)

options against Rulf
  [x] question      Information
  [ ] lie           Social        - you cannot deny something you have never heard of
  [x] bribe         Economic      - costs about 60 orens
  [ ] expose        Social        - you cannot reveal something you do not know
  [x] pickpocket    Crime
  solution families open: 5

> question Nessa
  question: Nessa tells you everything, and offers to back it up.
    check proc_interrogation: base 12 +2 (target Will) -2 (Negotiation) -2 (Charisma) -2 (Will)
      => DC 8; rolled 20 => CriticalPass
> pickpocket Rulf
  pickpocket: You lift the silver ring without anyone noticing.
    - no witnesses: nobody in the world knows this happened
```

And the same situation with the player never turning up at all:

```
  day 2:  victim_asks_around
  day 4:  thief_hides_it        <- the easy route closes because time passed
  day 8:  witness_talks
  day 10: accusation            <- believed, unprovable: a false accusation on the record
  day 14: feud
```

## What is actually implemented

- **Stable identity and deterministic generation.** Every person, place, fact and event gets an
  `EntityId` that outlives the vanilla object it points at. Seeds are forkable, so any situation can
  be replayed exactly.
- **An event ledger.** Every meaningful act is one append. Memory, affinity, knowledge, Karma, fame
  and thread tension are all *derived* from that, in one place, which is what makes a consequence
  traceable to a cause.
- **Facts versus belief.** The world can know something no character does; a character can be
  certain of something false; and believing a thing is not the same as being able to prove it.
  Rumour transmission loses confidence and never carries proof. This is the subsystem that makes
  witnesses, blackmail, alibis, coverups and framing work without a single bespoke quest script.
- **Twelve reusable verbs** spanning six solution families, each of which resolves through a
  vanilla-shaped check with four outcomes. Options are hidden only for genuine impossibility - a
  hopeless liar can still lie, and the critical failure is the interesting part.
- **Escalation over time.** Situations deteriorate on milestones, not countdowns. Ignoring one
  changes the world rather than deleting the content.
- **Save, migrate, reload.** Restored events are not re-dispatched, so loading a save never
  re-applies fifty hours of consequences.

## What is deliberately absent

No LLM anywhere in the authoritative path. No second persuasion skill, reputation meter or morality
axis beside Elin's own. No thousand quest templates. No combat resolution - Elin does that better
than a narrative layer could, so `attack` records intent and reads the result back.

## Next

The [roadmap](docs/roadmap.md) has the detail. The immediate next piece of work is the
reverse-engineering spike against a current Elin build: confirming how to read player elements,
Chara affinity, Karma, prestige, Influence, guild and home state; how to attach mod save data; and
whether custom `Check` rows can be driven from `Check.Perform` directly rather than through the
resolver in this repository.

## Layout

```
src/BrilliantQuesting.Core     the simulation - no Elin, BepInEx or Unity references
tests/                         46 tests, including the Gate B scenario assertions
tools/BrilliantQuesting.Lab    headless runner that prints the world and its reasoning
docs/design/                   the master design document
docs/architecture.md           how the pieces fit, and why the seams are where they are
docs/roadmap.md                phases, gates, and what is done
```
