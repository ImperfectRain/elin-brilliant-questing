# Architecture and authority router

Start with the affected row, then its source and representative test. **Do not preload all linked
documents.** This is navigation to implemented contracts, not project status or a replacement for
the [authority order](../AGENTS.md#authority-order). Git/code/tests establish implementation;
the [BQ roadmap](implementation-roadmap.md) owns order and done-when criteria. Use old commits only
when current evidence leaves a historical question unanswered.

## Subsystem index

Cards name ownership, inputs/outputs, storage, exclusions, consumers, proof and native limits.
Use [validation](agent/validation.md) and [documentation maintenance](agent/documentation.md) for
the affected contract. [Extension seams](systems/extension-seams.md) route committed planning and
incomplete joins without speculative step numbers.

| Concept | Read this card |
|---|---|
| Event/history | [History](systems/state.md#history) |
| Facts/truth | [Truth](systems/state.md#truth) |
| Knowledge/belief/proof | [Belief and proof](systems/state.md#belief-and-proof) |
| Memory/callbacks/provenance | [Memory and continuity](systems/state.md#memory-and-continuity) |
| Relationships/obligations/standing | [Social state](systems/state.md#social-state) |
| Personality/values/sensitivities/goals/emotions | [Character state](systems/state.md#character-state) |
| Identity observations/affordances | [Identity](systems/state.md#identity) |
| Vanilla actor activity | [Activity](systems/world.md#activity) |
| Actions/registry | [Actions](systems/actions.md#actions) |
| Feasibility/availability | [Availability](systems/actions.md#availability) |
| Checks/uncertainty | [Checks](systems/actions.md#checks) |
| Consequences | [Reactions](systems/actions.md#reactions) |
| Threads | [Threads](systems/actions.md#threads) |
| Developments/pressures | [Developments](systems/actions.md#developments) |
| Generation/archetypes | [Generation](systems/world.md#generation) |
| Storylets | [Storylets](systems/expression.md#storylets) |
| Casting/chemistry | [Casting](systems/expression.md#casting) |
| Actor intent/disclosure | [Intent and disclosure](systems/expression.md#intent-and-disclosure) |
| Speech acts/conversation | [Semantic communication](systems/expression.md#semantic-communication) |
| Dialogue realization | [Realization](systems/expression.md#realization) |
| Repetition/voice/idiolect/weirdness | [Expression controls](systems/expression.md#expression-controls) |
| Director/attention | [Attention](systems/expression.md#attention) |
| Autonomy | [Autonomy](systems/world.md#autonomy) |
| Traveling groups | [Travel](systems/world.md#travel) |
| Organizations | [Organizations](systems/world.md#organizations) |
| Sites/planning/realization | [Sites](systems/world.md#sites) |
| Player knowledge/discovery | [Discovery](systems/integration.md#discovery) |
| Journal/presentation | [Presentation](systems/integration.md#presentation) |
| Persistence/migrations | [Persistence](systems/integration.md#persistence) |
| Elin adapter/capability layer | [Native boundary](systems/integration.md#native-boundary) |
| Content compiler/bundle | [Content](systems/expression.md#content) |

## Authority matrix

| Question | Owning answer / source navigation |
|---|---|
| What happened? | `EventLedger`, through `NarrativeWorldState.Record`; [history](systems/state.md#history) |
| What is objectively true? | BQ claims: `Fact.Truth`; native inventory/life/location: `IVanillaState`; [truth](systems/state.md#truth) |
| What does X believe? | `KnowledgeGraph` / `KnowledgeRecord`, including false and uncertain claims; [belief](systems/state.md#belief-and-proof) |
| What can X prove? | `ProofLink` and `KnowledgeGraph.CanProve`; physical backing needs reconciliation; [proof](systems/state.md#belief-and-proof) |
| What does X remember? | `MemoryLedger`; callbacks/provenance are derived history views; [memory](systems/state.md#memory-and-continuity) |
| What does X want? | `NarrativeNpc.Goals`, values/needs and goal-formation traces; [character](systems/state.md#character-state) |
| What unresolved pressure exists? | `DevelopmentDetector.Detect`, not a new saved pressure list; [developments](systems/actions.md#developments) |
| Which durable matter is continuing? | `NarrativeThread`, lifecycle and escalation; [threads](systems/actions.md#threads) |
| What actions are available? | `ActionRegistry.Discover` asks each `NarrativeAction`; [actions](systems/actions.md#actions) |
| Is an attempt possible? | `GetAvailability`, actor scope, binding, capability and write policy; [availability](systems/actions.md#availability) |
| Is an attempt uncertain; who resolves it? | Verb/beat declares `CheckRequest`; `ICheckResolver` resolves; [checks](systems/actions.md#checks) |
| What consequence becomes history? | Performing owner records `WorldEvent`; `ConsequenceEngine` applies reactions; [reactions](systems/actions.md#reactions) |
| What does an NPC decide to do? | Autonomy selects `ActionIntent`, then shared registry/attempt; [autonomy](systems/world.md#autonomy) |
| What does a character communicate? | `ActorIntent` or `Disclosure` selects `SpeechAct`; [intent](systems/expression.md#intent-and-disclosure) |
| How is that meaning worded? | `DialogueRealizer` over eligible compiled fragments; [realization](systems/expression.md#realization) |
| What does the player know? | Player's `KnowledgeRecord`s; delivery acknowledges learning; [discovery](systems/integration.md#discovery) |
| What may be surfaced? | Player-filtered projections and attention admission, never raw inspector output; [presentation](systems/integration.md#presentation) |
| What physically exists in Elin? | Resolved native objects/readback, never a handle or BQ plan alone; [native boundary](systems/integration.md#native-boundary) |
| What belongs in the save? | Explicit `WorldStateSerializer` shape and `SaveMigrations`; [persistence](systems/integration.md#persistence) |

## Cross-system navigation

- [Data/control flow](systems/flow.md): live joins versus headless orchestration and missing joins.
- [Native capability/evidence](elin/capabilities.md): grades, limitations and canonical evidence.
- [Extension seams](systems/extension-seams.md): foundations, demonstrated gaps and planning lookup.
- [Decisions](agent/decisions.md): durable why; search D021 before embodiment work.

## Boundary corrections

**[superseded]** The former overview called the integration seam “one file” and consequences a
single table through which all writes flowed. Core interfaces are implemented by several Plugin
readers/writers. Actions, generators, conversation commitments and organization activity also write
owned state; `ConsequenceEngine` centralizes reactions, not every mutation.

**[superseded]** The former universal one-way dramatic diagram conflated a Core/Lab routed scene
with live Drama. The [flow map](systems/flow.md) names hosts at each join. Diagnostics are derived
from several authorities, not exclusively the event ledger.

**[superseded]** Native `Check.Perform` is not a pending automatic replacement for BQ resolution.
Authoritative procedural checks remain deterministic; native rows provide presentation. Dice and
critical windows come from `CheckProfile`, not an unconditional d20 rule. See [checks](systems/actions.md#checks).
