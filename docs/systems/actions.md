# Actions and causal progression

[Router](../architecture.md) · [Action validation](../agent/validation.md#actions)
· [maintenance](../agent/documentation.md). These are headless contracts; native dependencies are
per verb, not a blanket guarantee that every registered action works in Elin.

## Actions

**Owns:** the shared verb vocabulary (`NarrativeAction`, `ActionRegistry`) and execution handoff
(`ActionIntent`, `ActionAttempt`, `ActionContext`, `ActionBinding`). Inputs: actor/target, semantic
purpose, fact/item/thread and adapter/resolver. Outputs: availability or `ActionOutcome`, checks,
recorded events and native writes. Registry/context/outcomes are transient; resulting history and
owned state are saved. **Does not own:** NPC motive selection, native combat/crafting resolution or
a separate NPC verb library. `ActorScope`, `Embodiment` and `SettlesMatters` are declarations on the
verb; consumers must not keep parallel lists. Player projection and NPC attempts ask the same verbs.

Source: [NarrativeAction](../../src/BrilliantQuesting.Core/Actions/NarrativeAction.cs),
[ActionRegistry](../../src/BrilliantQuesting.Core/Actions/ActionRegistry.cs),
[ActionAttempt](../../src/BrilliantQuesting.Core/Actions/ActionAttempt.cs).
Proof: [ActionBindingTests](../../tests/BrilliantQuesting.Core.Tests/ActionBindingTests.cs),
[PlayerNpcActionSymmetryTests](../../tests/BrilliantQuesting.Core.Tests/PlayerNpcActionSymmetryTests.cs).
Lab: [actor-action](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/ActorActionScenario.cs).
Native: [capability routing](../elin/capabilities.md#capability-routing).

## Availability

**Owns:** side-effect-free feasibility classification and rejection reasons. Inputs: actor scope,
purpose binding, knowledge, resources, presence and capabilities. Outputs: offered/rejected attempts
for registry, contextual projection and autonomy. No saved availability cache. **Does not own:**
success odds, consequence resolution or presentation pacing. Low skill changes the check, not the
menu. Native capability refusal and hard impossibility differ from a contested attempt or a systemic
action with no roll. `ActionAttempt.Run` rechecks availability; the base `Perform` structurally gates
actor scope, not every verb-specific precondition. Native mutation gates remain necessary underneath.

Source: [Availability](../../src/BrilliantQuesting.Core/Actions/Availability.cs),
[NarrativeAction](../../src/BrilliantQuesting.Core/Actions/NarrativeAction.cs),
[ContextualActionProjection](../../src/BrilliantQuesting.Core/Actions/ContextualActionProjection.cs).
Proof: [ActionAvailabilityTests](../../tests/BrilliantQuesting.Core.Tests/ActionAvailabilityTests.cs),
[ActionBindingTests](../../tests/BrilliantQuesting.Core.Tests/ActionBindingTests.cs).
Lab: [theft](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/TheftLaboratoryScenario.cs).

## Checks

**Owns:** declared uncertainty and explainable arithmetic. `CheckRequest` supplies a profile,
actor/target and labeled modifiers; `ICheckResolver` returns `CheckResult`. Portable resolution uses
profile dice/critical windows and deterministic RNG. Requests/results are transient; world RNG state
and semantic outcomes are saved. **Does not own:** feasibility or vanilla outcomes already resolved
by Elin. Plugin `ElinCheckResolver` defaults to portable resolution; its native path is diagnostic,
and native rows supply difficulty text. Do not swap to Elin RNG merely because `Check.Perform` exists.
Consumers: verbs and routed storylet beats.

Source: [VanillaStyleCheckResolver](../../src/BrilliantQuesting.Core/Checks/VanillaStyleCheckResolver.cs),
[ElinCheckResolver](../../src/BrilliantQuesting.Plugin/ElinCheckResolver.cs).
Proof: [CheckTests](../../tests/BrilliantQuesting.Core.Tests/CheckTests.cs).
Lab: [actor-action](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/ActorActionScenario.cs).
Evidence: [checks API](../elin/api/checks-elements-skills.md),
[portable rationale](../elin-api-notes.md#why-procedural-checks-resolve-portably).

## Reactions

**Owns:** `ConsequenceEngine` subscribes to history and propagates knowledge, thread tension,
memories, social reactions, favors and player standing through consequence profiles and contextual
policies. Inputs: events, witnesses, practices, ties, observed/judged status. Outputs: mutations of
the owning stores and gated native standing writes. Engine/trace are transient; outputs are saved by
their owners. **Does not own:** all state mutation, item transfer, truth creation in every action,
or universal institutional judgment. Unnoticed deeds must not leak through affinity; observed
violence is not automatically murder. Consumers: social state, journal, developments, future acts.

Source: [ConsequenceEngine](../../src/BrilliantQuesting.Core/Consequences/ConsequenceEngine.cs),
[SocialPractices](../../src/BrilliantQuesting.Core/World/SocialPractices.cs).
Proof: [ConsequenceTests](../../tests/BrilliantQuesting.Core.Tests/ConsequenceTests.cs),
[RecognizedViolenceTests](../../tests/BrilliantQuesting.Core.Tests/RecognizedViolenceTests.cs).
Lab: [theft](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/TheftLaboratoryScenario.cs).
Native: [standing evidence](../elin/capabilities.md#capability-routing).

## Threads

**Owns:** durable matters, linked entities/facts/sites, escalation schedule/completed steps, lifecycle,
resolution and storylet firings. These are saved. `ThreadEngine.Advance` applies due steps through
registered archetype handlers; `ThreadLifecycle` reviews viability; `ThreadResolution.Resolve`
records an ending once. Inputs: time, events and participating state. Outputs: escalation, ending,
inheritance/quarantine and history for autonomy, discovery, storylets and chronicle.
**Does not own:** a quest objective UI, all developments or native life state. Unresolved bindings
are not dead actors. Handler registrations are transient and must be restored by the host.

Source: [ThreadEngine](../../src/BrilliantQuesting.Core/Threads/ThreadEngine.cs),
[ThreadLifecycle](../../src/BrilliantQuesting.Core/Threads/ThreadLifecycle.cs),
[ThreadResolution](../../src/BrilliantQuesting.Core/Threads/ThreadResolution.cs).
Proof: [ThreadLifecycleTests](../../tests/BrilliantQuesting.Core.Tests/ThreadLifecycleTests.cs),
[PersistenceTests](../../tests/BrilliantQuesting.Core.Tests/PersistenceTests.cs).
Lab: [questline](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/QuestlineScenario.cs).
Native: life/location in [capabilities](../elin/capabilities.md).

## Developments

**Owns:** `DevelopmentDetector.Detect` reads unresolved unproven-knowledge and unmet-obligation
pressure into ID-linked `Development`s. Inputs: saved facts/beliefs/obligations/history/threads.
Outputs: stable sorted pressure readings for storylet eligibility and inspector. **All derived;
never saved.** **Does not own:** a lifecycle, new facts/threads, a universal detector for every world
pressure or a promise of a scene. Resolution belongs to source state: pressure disappears when no
longer derivable. `DevelopmentScoring` is a separate attention reader over eligible news, not a
second `Development` store or general goal planner.

Source: [DevelopmentDetector](../../src/BrilliantQuesting.Core/Developments/DevelopmentDetector.cs),
[DevelopmentExpression.Opportunities](../../src/BrilliantQuesting.Core/Developments/DevelopmentExpression.cs),
[Development](../../src/BrilliantQuesting.Core/Developments/Development.cs).
Proof: [DevelopmentLayerTests](../../tests/BrilliantQuesting.Core.Tests/DevelopmentLayerTests.cs).
Lab: [playground](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/PlaygroundScenario.cs).
Rationale: [character design](../design/character-dialogue-system.md#365-development-layer).
