# Elin Brilliant Questing

A persistent, simulation-driven questing layer for [Elin](https://store.steampowered.com/app/2135150/Elin/).

The premise, from the [master design document](docs/design/master-design.md): **generate persistent
situations, not disposable quests.** The simulation decides what is happening; vanilla Elin decides
what the player can do about it. Attributes, skills, checks, affinity, Karma, fame, town Influence,
guild rank, faith, inventory, crime witnesses, combat and the player's own land are the interface -
not a parallel roleplaying ruleset bolted on beside them.

> Never create a procedural solution when an existing Elin mechanic can constitute that solution.

## Current status

Read it from the repository rather than from here: `git log --oneline` names the last step landed,
`dotnet test` says what is proven, and §3 of [the roadmap](docs/implementation-roadmap.md) carries
the audited picture. The table below is the original Phase 0/Phase 1 milestone and is kept for
shape, not as a current count — a status block in a README goes stale faster than anyone updates it.

| | |
|---|---|
| Simulation core | implemented, 46 passing tests |
| Three-NPC laboratory (Gate B) | implemented, runs headless |
| Save / load / schema migration | implemented |
| Elin runtime adapter (BepInEx plugin) | written and compiling against the real assemblies; **not yet run in game** |

Everything the simulation needs from the game is expressed as the
[`IVanillaState`](src/BrilliantQuesting.Core/Integration/IVanillaState.cs) interface, with a headless
[`SandboxVanillaState`](src/BrilliantQuesting.Core/Integration/SandboxVanillaState.cs) implementing
the same contract. That is what lets the simulation be built and tested with no game process and no
game assets, and it means the live adapter is one file to repair when Early Access moves under us.

`src/BrilliantQuesting.Plugin` is the live half. It is not in the solution, so the root build and
tests stay green without the game installed; see [docs/plugin-build.md](docs/plugin-build.md) for
how to populate `lib/` from your own install and build it. No game assemblies are committed.

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
- **A library of reusable verbs** spanning six solution families, each resolving through a
  vanilla-shaped check with four outcomes. Options are hidden only for genuine impossibility - a
  hopeless liar can still lie, and the critical failure is the interesting part. A case can be
  closed on physical evidence alone, without a single character being willing to talk.
- **Escalation over time.** Situations deteriorate on milestones, not countdowns. Ignoring one
  changes the world rather than deleting the content.
- **Save, migrate, reload.** Restored events are not re-dispatched, so loading a save never
  re-applies fifty hours of consequences.

## What is deliberately absent

No LLM anywhere in the authoritative path. No second persuasion skill, reputation meter or morality
axis beside Elin's own. No thousand quest templates. No combat resolution - Elin does that better
than a narrative layer could, so `attack` records intent and reads the result back.

## Next

The [roadmap](docs/roadmap.md) has the detail, and [docs/elin-api-notes.md](docs/elin-api-notes.md)
records what reading the shipped assemblies established. The adapter compiles against the real
game, which proves its calls exist - not that they behave. Running it inside Elin is the next step,
and the first thing it will settle is whether the element aliases are right.

## Layout

```
src/BrilliantQuesting.Core     the simulation - no Elin, BepInEx or Unity references
src/BrilliantQuesting.Plugin   the live adapter - the only project that touches the game
tests/                         46 tests, including the Gate B scenario assertions
tools/BrilliantQuesting.Lab    headless runner that prints the world and its reasoning
docs/design/                   the master design document
docs/architecture.md           how the pieces fit, and why the seams are where they are
docs/roadmap.md                phases, gates, and what is done
docs/elin-api-notes.md         what the shipped assemblies actually expose
docs/plugin-build.md           populating lib/, building and installing the plugin
tools/ApiDump                  prints the game's API surface without executing it
```
