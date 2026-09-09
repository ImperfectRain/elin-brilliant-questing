# Recorded state and character ownership

Retrieve one heading. [Router](../architecture.md) · [State validation](../agent/validation.md#state)
· [maintenance](../agent/documentation.md). Unless noted, contracts are headless with no native
dependency. “Persisted” means explicitly serialized; derivation from history does not imply replay.

## History

**Owns:** ordered events and queued dispatch. `NarrativeWorldState.Record` mints an event and appends
it to `EventLedger`; subscribers may append reactions without recursive dispatch. Inputs: deeds,
observations, lifecycle changes. Outputs: history/listener calls. Events are persisted; listeners and
queues are transient. **Does not own:** native current state, truth of every spoken claim, or universal
event-sourced reconstruction. Never rewrite events to reconcile identity or replay reactions on load.
Consumers: consequences, threads, continuity, diagnostics.

For live native violence, `VanillaActionRecorder.ShouldObserveViolence` admits player actions or
combat between actors at `NarrativeImportance.Known` or above (the player is known).
Passive registration alone is not significance. The Plugin checks before witness scans/intake;
explicit recorder callers retain their observation authority. Only admission of new events changes,
never existing history. `VanillaActionRecorderTests` covers passive intake/reload and player/known
actors; [native hook evidence](../elin/bq-integration/event-hooks.md) records the live defect.

Source: [EventLedger](../../src/BrilliantQuesting.Core/Events/EventLedger.cs),
[NarrativeWorldState](../../src/BrilliantQuesting.Core/World/NarrativeWorldState.cs).
Proof: [FoundationTests](../../tests/BrilliantQuesting.Core.Tests/FoundationTests.cs),
[PersistenceTests](../../tests/BrilliantQuesting.Core.Tests/PersistenceTests.cs).
Lab: [theft](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/TheftLaboratoryScenario.cs).

## Truth

**Owns:** BQ propositions in `KnowledgeGraph.Facts`; `Fact.Truth` distinguishes true, false and
uncertain or superseded. Inputs: generators, action interpretation, inference. Outputs: stable fact IDs for beliefs,
actions, developments, threads. Facts/evidence references are saved. **Does not own:** native physical
state or belief that a proposition is true. `AddFact` is explicit; not every fact is reconstructed
from an event. Actor-local inferences remain uncertain and name provenance without overwriting sources.

Source: [Fact](../../src/BrilliantQuesting.Core/Knowledge/Fact.cs),
[KnowledgeGraph](../../src/BrilliantQuesting.Core/Knowledge/KnowledgeGraph.cs),
[ActorLocalInterpreter](../../src/BrilliantQuesting.Core/World/ActorLocalInterpreter.cs).
Proof: [KnowledgeTests](../../tests/BrilliantQuesting.Core.Tests/KnowledgeTests.cs),
[ActorLocalInterpretationTests](../../tests/BrilliantQuesting.Core.Tests/ActorLocalInterpretationTests.cs).
Lab: [theft](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/TheftLaboratoryScenario.cs).

## Belief and proof

**Owns:** per-knower confidence/source, teaching and proof links. `Teach` requires an existing fact;
weaker hearsay does not downgrade existing confidence. Beliefs/proof links/provenance are saved.
Inputs: witnessing, testimony, rumor, investigation, inference. Outputs: `Knows`, `CanProve`,
disclosure/legal eligibility, journal claims. **Does not own:** truth or institutional judgment.
A proof flag/handle is not a live object; reconcile stale physical backing. NPC circulation does
not teach the player off-screen.

Source: [KnowledgeGraph](../../src/BrilliantQuesting.Core/Knowledge/KnowledgeGraph.cs),
[ProofLink](../../src/BrilliantQuesting.Core/Knowledge/ProofLink.cs),
[EvidenceTraceAuditor](../../src/BrilliantQuesting.Core/Integration/EvidenceTraceAuditor.cs).
Proof: [KnowledgeTests](../../tests/BrilliantQuesting.Core.Tests/KnowledgeTests.cs),
[EvidenceTraceTests](../../tests/BrilliantQuesting.Core.Tests/EvidenceTraceTests.cs).
Lab: [guilds](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/GuildsScenario.cs),
[authority](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/AuthorityScenario.cs).
Native dependency: [inventory/evidence](../elin/capabilities.md#capability-routing).

## Memory and continuity

**Owns:** `MemoryLedger` consolidates per-owner memories, including accounted affinity; routine
repetition folds, defining memories stay distinct. Memory records are saved. `CallbackHooks`,
`CallbackRecurrence`, `ItemProvenance` and `LocationHistory` derive history views with no parallel
store. Inputs: remembered events or ledger/source-belief routes. Outputs: recall material and
item/place explanations for dialogue, callback disclosure, sites and inspector. **Does not own:**
facts, invented incidents/nicknames or permission to disclose. Uninvolved recallers get no shared past.

Source: [MemoryLedger](../../src/BrilliantQuesting.Core/Memory/MemoryLedger.cs),
[CallbackHook](../../src/BrilliantQuesting.Core/Continuity/CallbackHook.cs),
[ItemProvenance](../../src/BrilliantQuesting.Core/Continuity/ItemProvenance.cs),
[LocationHistory](../../src/BrilliantQuesting.Core/Continuity/LocationHistory.cs).
Proof: [MemoryTests](../../tests/BrilliantQuesting.Core.Tests/MemoryTests.cs),
[CallbackHookTests](../../tests/BrilliantQuesting.Core.Tests/CallbackHookTests.cs).
Lab: [scene](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/SceneScenario.cs),
[questline](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/QuestlineScenario.cs).

## Social state

**Owns:** directed `RelationshipGraph` ties/sentiment and scoped `SocialObligationLedger` records
(debtor, creditor, subject, source event, status). Both are saved. Inputs: authored ties/deeds.
Outputs: harm propagation, intent/disclosure weights, redeemable favors, pressures. **Does not own:**
vanilla affinity, karma, fame, guild rank or currency. `StandingSheet` is a derived report, not a
reputation meter. Favors are spent by explicit action, never discovery. Generated organizations
are distinct from native guild standing.

Source: [RelationshipGraph](../../src/BrilliantQuesting.Core/Relationships/RelationshipGraph.cs),
[SocialObligation](../../src/BrilliantQuesting.Core/Obligations/SocialObligation.cs),
[StandingSheet](../../src/BrilliantQuesting.Core/Diagnostics/StandingSheet.cs).
Proof: [RelationshipHarmTests](../../tests/BrilliantQuesting.Core.Tests/RelationshipHarmTests.cs),
[SocialObligationTests](../../tests/BrilliantQuesting.Core.Tests/SocialObligationTests.cs).
Lab: [authority](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/AuthorityScenario.cs).
Native: [standing evidence](../elin/capabilities.md#capability-routing).

## Character state

**Owns:** `NarrativeNpc` behavioral dimensions, values, narrative needs, sensitivities, contradictions,
quirks, negative-space limits, emotions and `NpcGoal`s. These are saved; goal-choice explanations
are transient. Inputs: profiles, interpreted stakes, time. Outputs: candidate goals, responses and
disclosure/intent/check/wording inputs. **Does not own:** bodily needs, schedules or physical execution.
Emotion decays with time; personality grants no institutional role. `MissingGoatProblemSolver` is
a concrete goal-formation probe, not a universal planner. An unattemptable chosen goal is not silently
substituted with an attemptable alternative.

Source: [NarrativeNpc](../../src/BrilliantQuesting.Core/World/NarrativeNpc.cs),
[MissingGoatProblemSolver](../../src/BrilliantQuesting.Core/World/MissingGoatProblemSolver.cs),
[ActionIntent.FromGoalChoice](../../src/BrilliantQuesting.Core/Actions/ActionAttempt.cs).
Proof: [ProblemSolvingStyleTests](../../tests/BrilliantQuesting.Core.Tests/ProblemSolvingStyleTests.cs),
[EmotionalStateTests](../../tests/BrilliantQuesting.Core.Tests/EmotionalStateTests.cs).
Lab: [actor-action](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/ActorActionScenario.cs),
[playground](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/PlaygroundScenario.cs).
Rationale: [character design](../design/character-dialogue-system.md).

## Identity

**Owns:** stable IDs/alias retirement (`ActorIdentityIntake`); `CharacterIdentity` is a fresh native
observation; `IdentityAffordances` derives plausible domains, stakes and role eligibility once.
BQ IDs/authored occupation/retirement are saved; native facets and affordances are not. Inputs:
observed facets or BQ authorship. Outputs: provenance-bearing inputs for casting, interpretation,
goals and services. **Does not own:** temperament, actual knowledge, mutation permission or another
body. `SourceChara.job` can be a combat template, not livelihood evidence. Retire duplicates without
rewriting history; unread institutional facets do not establish role revocation.

Source: [IdentityAffordances](../../src/BrilliantQuesting.Core/World/IdentityAffordances.cs),
[ActorIdentityIntake](../../src/BrilliantQuesting.Core/World/ActorIdentityIntake.cs),
[ElinBindings](../../src/BrilliantQuesting.Plugin/ElinBindings.cs).
Proof: [IdentityAffordanceTests](../../tests/BrilliantQuesting.Core.Tests/IdentityAffordanceTests.cs),
[ActorIdentityIntakeTests](../../tests/BrilliantQuesting.Core.Tests/ActorIdentityIntakeTests.cs).
Lab: [playground](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/PlaygroundScenario.cs) for derivations;
Plugin attach diagnostics for live facets. [Evidence](../elin/capabilities.md).
