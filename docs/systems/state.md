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

Also owns **typed causal provenance** (`D077`). `WorldEvent.Provenance` carries what prompted a
transition, what it is about, what the actor acted on and what it produced; `Record` takes it and
`CausalHistory` resolves it against the world. `Related`/`Evidence`/`Witnesses` keep their meaning -
what the event touched - and are not where a cause belongs. Nothing infers a cause from adjacency,
timestamp or tag: an event with no links reads as unknown, and old saves keep that after migration.
Motive is the actor's, never the world's, so an accusation names the occurrence rather than the
object. `ReserveEvent` hands out an identity for a record that must name its origin before that
event exists; read-only inspection reserves nothing. A decision record holds bounded reason codes,
never a snapshot. The ledger owns identity as well as order: `EventLedger.Find` answers one
occasion by id without walking history, and `CausalHistory.FindEvent` resolves through it rather
than keeping a second scan.

Source: [EventLedger](../../src/BrilliantQuesting.Core/Events/EventLedger.cs),
[NarrativeWorldState](../../src/BrilliantQuesting.Core/World/NarrativeWorldState.cs),
[EventProvenance](../../src/BrilliantQuesting.Core/Events/EventProvenance.cs),
[CausalHistory](../../src/BrilliantQuesting.Core/World/CausalHistory.cs).
Proof: [FoundationTests](../../tests/BrilliantQuesting.Core.Tests/FoundationTests.cs),
[CausalProvenanceTests](../../tests/BrilliantQuesting.Core.Tests/CausalProvenanceTests.cs),
[PersistenceTests](../../tests/BrilliantQuesting.Core.Tests/PersistenceTests.cs).
Lab: [theft](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/TheftLaboratoryScenario.cs).

## Determinism and streams

**Owns:** which stream decided what, and what a repeat of the same attempt draws from (`D078`).
`RngStreams` holds every routed label in one file, split into the family the world is held to -
checks, actor intent, tie-breaks - and the family that only decides wording. Expression is always
handed a `Fork`, never the stream a decision draws from, so rendering a scene, rendering it out of
different content, rendering it not at all, or allocating delivery identities around it decides the
same things. **Does not own:** the RNG algorithm, check arithmetic, or a second saved stream.

A scene is keyed on its occurrence - how many times that storylet has already fired on that thread
for that focus - because a fork derives from its parent's seed and would otherwise replay the first
firing exactly. The count is thread history the save already carries; nothing is persisted for it,
and a thread restored without firings plays its first occurrence. An action attempt needs no key: it
draws from the world stream, whose state is saved, so each attempt continues and a reload continues
with it. A play that applies no consequences records no firing, mints no identity and draws nothing,
so reopening a surface gains no roll; discovery and availability are read-only on the same terms.

Source: [RngStreams](../../src/BrilliantQuesting.Core/Foundation/RngStreams.cs),
[DeterministicRng](../../src/BrilliantQuesting.Core/Foundation/DeterministicRng.cs),
[StoryletRouter](../../src/BrilliantQuesting.Core/Storylets/StoryletRouter.cs).
Proof: [RngIsolationTests](../../tests/BrilliantQuesting.Core.Tests/RngIsolationTests.cs),
[FoundationTests](../../tests/BrilliantQuesting.Core.Tests/FoundationTests.cs),
[PersistenceTests](../../tests/BrilliantQuesting.Core.Tests/PersistenceTests.cs).
Lab: [scene](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/SceneScenario.cs).

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

`ActorPressureView.Of` is this card's projection of a `Development` onto one person: route before
stake, and no route means no reading. A route is holding the claim, being party to the record the
pressure is read from, or an openly visible condition where the actor lives or works; kinship,
values, sensitivities, office and membership only change how hard it presses, and who the actor can
place in the matter is gated on proof. Actor-held claims are enumerated as well as projected, so a
sincerely false belief is a real local pressure with no development behind it, and settlement is
read off what the actor believes rather than off the matter that ended. Derived, unsaved and side
effect free — it teaches nothing, which is what separates it from `ActorLocalInterpreter` above.
The durable rule is [`D083`](../agent/decisions.md#d083--an-actor-reaches-a-pressure-by-route-and-a-stake-only-changes-what-it-costs-them).

Source: [KnowledgeGraph](../../src/BrilliantQuesting.Core/Knowledge/KnowledgeGraph.cs),
[ProofLink](../../src/BrilliantQuesting.Core/Knowledge/ProofLink.cs),
[ActorPressureView](../../src/BrilliantQuesting.Core/Developments/ActorPressureView.cs),
[EvidenceTraceAuditor](../../src/BrilliantQuesting.Core/Integration/EvidenceTraceAuditor.cs).
Proof: [KnowledgeTests](../../tests/BrilliantQuesting.Core.Tests/KnowledgeTests.cs),
[ActorPressureViewTests](../../tests/BrilliantQuesting.Core.Tests/ActorPressureViewTests.cs),
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

A goal also owns its machine-readable contract (BQa-008, [`D084`](../agent/decisions.md)). `Identity`
is derived from kind, subject and condition, so `NpcGoalCollection.Adopt` is how automatic formation
takes on a want without accumulating duplicates across passes; plain `Add` stays the raw path a
hand-established scenario uses. `GoalCondition` is a term from `GoalConditionRegistry` plus concrete
`EntityId` bindings, and evaluation is a pure read of authoritative state that answers `Unsupported`
rather than guessing — including for a legacy goal with no condition and for a term this build does
not know. `GoalOrigin` records which reading caused the goal by reference, never a copy of it, and
unknown provenance stays unknown. `GoalLifecycle` distinguishes active, satisfied, abandoned and
superseded; retirement keeps the record and both the active set and the retained history are bounded.
`ActorAssessment` is what the owner believes and is never set by objective evaluation. `Reason` stays
an explanation for people, and no production decision parses it.

Source: [NarrativeNpc](../../src/BrilliantQuesting.Core/World/NarrativeNpc.cs),
[MissingGoatProblemSolver](../../src/BrilliantQuesting.Core/World/MissingGoatProblemSolver.cs),
[ActionIntent.FromGoalChoice](../../src/BrilliantQuesting.Core/Actions/ActionAttempt.cs).
Source: [GoalContract](../../src/BrilliantQuesting.Core/World/GoalContract.cs).
Proof: [ProblemSolvingStyleTests](../../tests/BrilliantQuesting.Core.Tests/ProblemSolvingStyleTests.cs),
[NpcGoalContractTests](../../tests/BrilliantQuesting.Core.Tests/NpcGoalContractTests.cs),
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
