# Procedural Places And Spatial History

**Status:** Canonical implementation doctrine for procedural sites, scenario dungeons, generated settlements, and later spatial evolution.

**Scope:** This document distills the 2026-08-30/31 procedural-places research into guidance an implementation agent can use. It is not the full research archive. Runtime facts remain in [`../elin-api-notes.md`](../elin-api-notes.md) and [`../elin/`](../elin/); where this document conflicts with those, the evidence docs win.

## 1. Product Goal

Brilliant Questing should eventually create and evolve persistent places whose form is readable as the result of geography, function, society, and history.

The target is not random towns or random dungeons. A place should answer why it exists, who uses it, what happened there, why its important routes and occupants are arranged as they are, and how later events changed it without erasing what the player already saw.

```text
world/history state
-> spatial requirements
-> candidate places
-> authored physical realization
-> validation/scoring
-> persistent Elin zone
-> later additive development
-> projected spatial affordances
-> new situation generation
```

Geometry matters because it makes history visible. History matters because it gives geometry meaning.
Verified spatial facts also become inputs to later narrative generation; a dock, isolated warehouse,
guarded entrance or abandoned yard is not only scenery once BQ can safely observe it.

## 2. Doctrine

**Semantic structure before geometry.** Generate purpose, relationships, pressures, history, routes and constraints first. Tiles, rooms, roads and objects are a later realization of that plan.

**Authored atoms, procedural composition.** Prefer authored Elin map pieces, room/building patterns, scenario anchors and transformations composed by deterministic planners. Do not synthesize all aesthetics tile-by-tile.

**State causes contents.** Cargo, enemies, clues, loot, damage, repairs, residents and services derive from actual situation, organization, economy, travel and location-history state. No filler chest because a template expects one.

**Generate, score, validate.** Produce multiple candidates and select a valid one. Reject unreachable objectives, unsupported route promises, useless loops, trivial shortcuts, excessive linearity, inaccessible evidence, and mechanics not proven in Elin. Expose rejection reasons in debug tools.

**Procedural problems before custom puzzle mechanics.** Near-term sites should use verified Elin verbs: locks, guarded thresholds, digging/breaking where permitted, traps, hidden routes, search, evidence, stairs and zone transitions. Puzzle systems are future BQ-owned mechanics only after their state and runtime behavior are verified.

**Genesis and Development are separate.** Genesis runs once to create a place. Development later mutates it additively. A visited place is never destructively regenerated.

**Spatial state feeds later situations.** Physical realization must eventually project semantic affordances back into world-state generation. The loop is spatial fact -> affordance -> situation -> history -> later spatial consequence.

**Vanilla owns physical substrate where possible.** BQ owns narrative meaning, history, planning, scoring and semantic identity. Do not duplicate economy, organizations, pathfinding, routines, needs, combat, Home arithmetic, or vanilla zone simulation.

## 3. Architecture

Use at least two representations:

```text
PlacePlan / SettlementPlan / ScenarioPlan
  purpose
  causal history
  functional nodes
  route and separation constraints
  required affordances
  semantic sockets/anchors
  validation requirements

Physical realization
  Elin zone/site identity
  map pieces or native generation profile
  placed cards, things, NPC materialization points
  doors, barriers, traps, stairs, route objects
```

The plan is authoritative for meaning. The physical map is the persistent embodiment that the player sees and may affect.

For larger settlements, extend the plan hierarchically:

```text
settlement -> district -> lot/site -> building -> room -> object
```

Districts should emerge from function and history, not flavor tables.

## 4. Scenario Dungeons And Adventure Sites

Procedural scenario dungeons are a first-class product system, not merely a prototype for towns. Elin already supplies dungeon substrate and systemic verbs; BQ's value is making a site exist because something happened there.

Recommended early grammars: failed caravan camp or wreck, occupied/abandoned mine, bandit camp, ruined shrine, smuggler cache, logging camp, abandoned farm, refugee camp, excavation, and small prison or holding site.

Use abstract navigation graphs before physical layout:

```text
entry
|-- clue / information route
|-- hazardous or guarded route
`-- alternate systemic route
   -> objective
   -> return or deeper transition
```

Tier the mechanics:

- **Tier 1:** compose existing or vanilla-like mechanics: lock/key or access cycles, blocked route versus costly detour, traps, guarded thresholds, diggable/breakable bypasses, hidden routes, evidence caches, locked descent/exit, causal populations and loot.
- **Tier 2:** small reusable BQ scenario state machine after a spike and only when Tier 1 exposes a real need: triggers, conditions and effects over switches, gates, revealed evidence, encounter activation, hostility changes and scenario flags.
- **Tier 3:** new gameplay mechanics such as water levels, movable blocks, beam routing, power networks or physics puzzles. These are non-goals for the core dungeon path.

A useful later primitive is a declarative Trigger -> Condition -> Effect language connected to authored sockets:

```text
Trigger: Interact, StepOn, EnterArea, Kill, Destroy, PossessItem, Read, Talk, CounterReached
Condition: scenario flag/counter, actor alive/dead, item present, object state, faction/relationship state
Effect: unlock exit, open/close object, reveal evidence, activate encounter, alter scenario state, show message
```

Do not add bespoke C# scripting per dungeon before testing whether reusable primitives cover the cases.

## 5. Settlement Strategy

Generated settlements are post-launch unless runtime evidence and implementation cost change materially. They require S6 economy/business/organization work, BQ-085/BQ-086 provenance and location history, S8 site foundations, autonomy/travel, and director diversity.

Settlement genesis should follow this order:

1. Purpose: river crossing, mine, lumber, farming, pilgrimage, trade junction, defensive outpost, fishing, excavation, refugee settlement, organization stronghold.
2. Geography: read actual surrounding world context; do not invent physical support the region cannot provide.
3. Founding/history: sparse events that can leave readable traces.
4. Population/institutions: households, services, authority, religious presence, organizations, relationships and coarse economic role from existing BQ systems.
5. Functional graph: nodes such as Entrance, Market, Residence, Industry, Worship, Authority, Storage, Inn, Dock, Mine, Landmark, Abandoned.
6. District graph where scale requires it.
7. Roads/lots and authored physical pieces.
8. Interiors from function, not generic rooms with labels.
9. Historical transformations such as fire scars, prosperity expansion, defensive growth, decline, reoccupation.
10. Decoration after the structure and history validate.

Prefer satellite settlements before mutating vanilla towns: farmstead, lumber camp, mine camp, shrine, merchant outpost, refugee camp. BQ owns them from genesis, they are easier to persist, and they can develop into hamlets without rewriting a vanilla map.

## 6. Persistence And Development

Genesis creates initial purpose, history, site or settlement graph, layout, residents, institutions, contents, evidence and validation record.

Development adds occupancy changes, business opening/closing, ownership changes, memorials, signs, furniture, stalls, fences, small structures, repairs, abandonment, reoccupation and satellite sites.

Development must not reconstruct the original map. If a place has been visited, preserve the save-owned map and apply bounded additive changes only after verifying free/compatible space. Fail closed when player construction, vanilla ownership, NPC binding, service behavior or persistence cannot be proven.

## 7. Elin Evidence Status

Do not treat design intent as runtime proof. Current evidence supports investigation of these primitives, but exact-build behavior must be recorded before product steps depend on them:

- random zone profiles and source-sheet zone support;
- `addMap` existing-save behavior for predeclared mod zones;
- `Region.CreateRandomSite(...)` and native registered random site concepts;
- `.mp` map pieces and vanilla runtime application paths such as `GenBounds.TryAddMapPiece` and `PartialMap.Apply(...)`;
- visited-zone persistence, where a zone keeps a save-owned map copy after entry;
- doors, locks, destructible/diggable terrain, traps, detector/search behavior, stairs and locked exits;
- custom `Trait`, `Act`, `Zone`, Drama flags/counters and bounded Harmony hooks.

Use [`../elin/api/world-and-zones.md`](../elin/api/world-and-zones.md), [`../elin/verification/matrix.md`](../elin/verification/matrix.md), [`../elin/verification/unresolved.md`](../elin/verification/unresolved.md), and [`../elin/verification/runtime-probes.md`](../elin/verification/runtime-probes.md) before converting any item above into a runtime commitment.

Before physical settlement development, run a disposable-save probe: create or enter a BQ-owned persistent site, apply one intended authored addition, save/quit/reload, leave/re-enter, advance several days, save/quit/reload again, verify NPC/path/service behavior, and where feasible disable BQ to confirm save health.

## 8. Testing Requirements

Headless tests should cover deterministic plan generation and seed replay; graph validity and explainability; reachability, route diversity, objective separation and evidence access; candidate rejection reasons; scenario-state transitions where used; and expressive-range metrics over many generated plans.

Runtime tests should cover native site creation or reuse; unload/reload/return persistence; save/quit/reload persistence; no redispatch of historical events; no duplicate actors/items after lifecycle boundaries; compatibility when an Elin primitive is absent or reclassified; and additive mutation safety on BQ-owned sites before any vanilla-town mutation.

Spatial anti-template metrics should extend BQ-104-style testing: graph shape, cycle count, centralization, dead-end ratio, service distribution, historical scars, abandonment, landmark placement, route diversity and mechanical vocabulary. Different names and roofs do not count as different experiences.

## 9. Non-Goals

- Full procedural towns in the 1.0 path.
- A general replacement Nefia generator.
- Tile-by-tile city generation as the source of meaning.
- A parallel settlement economy, organization model, routine system, pathfinder, combat resolver or needs simulator.
- Destructive regeneration of visited places.
- Physical vanilla-town mutation before BQ-owned site mutation is runtime-proven.
- Assuming editor/decompile evidence proves in-game save semantics.
- Custom puzzle mechanics on the critical path before reusable BQ-owned scenario state passes runtime verification.

## 10. Source Register

External research sources used conceptually:

- Joris Dormans, ["Unexplored's Secret: Cyclic Dungeon Generation"](https://www.gamedeveloper.com/design/unexplored-s-secret-cyclic-dungeon-generation-).
- Joris Dormans, ["The Theory of The Place: A level design philosophy for Unexplored 2"](https://www.gamedeveloper.com/game-platforms/the-theory-of-the-place-a-level-design-philosophy-for-unexplored-2).
- Brian Bucklew and Jason Grinblat, ["Math for Game Developers: End-to-End Procedural Generation in Caves of Qud"](https://www.gdcvault.com/play/1026313/Math-for-Game-Developers-End).
- Game Developer, ["The 10-Year Journey of Ultima Ratio Regum: The Culture-Generating Roguelike"](https://www.gamedeveloper.com/design/the-10-year-journey-of-ultima-ratio-regum-the-culture-generating-roguelike).
- ColePowered Games, ["Shadows of Doubt DevBlog 13: Creating Procedural Interiors"](https://colepowered.com/shadows-of-doubt-devblog-13-creating-procedural-interiors/).
- Microsoft Learn, ["Introduction to Jigsaw Structures"](https://learn.microsoft.com/en-us/minecraft/creator/documents/structures/introductiontojigsawstructures?view=minecraft-bedrock-stable) and ["Bedrock Editor Jigsaws"](https://learn.microsoft.com/en-us/minecraft/creator/documents/bedrockeditor/editorjigsaws?view=minecraft-bedrock-stable).
- Oskar Stalberg / Game Developer, ["How Townscaper Works: A Story Four Games in the Making"](https://www.gamedeveloper.com/game-platforms/how-townscaper-works-a-story-four-games-in-the-making).
- Elin Modding Wiki, ["Zone"](https://elin-modding-resources.github.io/Elin.Docs/articles/10_Source%20Sheets/zone), and Elin decompiled references, mediated through the repo evidence docs.

These sources are design input only. Implementation agents should not promote a technique into BQ without checking current code, current roadmap dependencies, and exact-build Elin evidence.
