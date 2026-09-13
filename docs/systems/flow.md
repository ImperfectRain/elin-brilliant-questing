# Data and control flow

[Router](../architecture.md). This is a set of implemented paths, **not one universal scheduler**.
“Wired” means current call sites exist; it is not a runtime evidence grade. Use
[capabilities](../elin/capabilities.md) for native proof and [extension seams](extension-seams.md) for gaps.

## Live host joins

The host is [BrilliantQuestingPlugin](../../src/BrilliantQuesting.Plugin/BrilliantQuestingPlugin.cs).
Search the method named in the table before changing order or adding another tick.

| Trigger / input | Implemented join | Output / constraint |
|---|---|---|
| GameIO post-load/new game | `LoadWithDiagnostics` → binding restoration → service/handler creation → `ConsequenceEngine.Attach` | New listeners see only new events; reattachment is not historical replay |
| Attach/zone change | `RegisterLocalVanillaActors` → `ElinBindings.CanonicalIdFor` → identity intake/authority refresh | Stable IDs and re-read native facets; no personality inferred from job |
| `EVENT.ActPerformed` | `OnActPerformed` → `ElinActionObserver.Observe` → `VanillaActionRecorder` → `NarrativeWorldState.Record` → `LiveWorldCycle.Observed` → `ProductionCycle.Close` | Recognized acts become history; unknown payloads ignored, native outcomes not rolled again, and the indivisible opening one took is closed rather than recorded twice |
| New event | `EventLedger.Append` → attached `ConsequenceEngine.Handle` | Knowledge/tension then profile-based social/memory/standing reactions; no blanket claim that every fact comes from this listener |
| Day changes after acts | `AdvanceThreadsIfTheDayTurned` → `AdvanceThreads` → `ThreadLifecycle.Review`, `ThreadEngine.Advance`, `LiveWorldCycle.Advance`, autonomy, schemes, adventurers, travel; then rumor circulation | Host order is explicit; each owner retains its own one-time state/gates. The cycle runs ahead of the other autonomy owners so an opening it committed is already closed when they look at the same day. Its pass carries the organization half (BQa-020); the host adds no second organization tick |
| Completed zone visit | `NativeZoneVisit` postfix → `ReconcileIfTheZoneChanged(completedVisit)` → `AdvanceThreadsIfTheDayTurned` | Reconciliation before elapsed work is consumed, after vanilla's own catch-up; a journey or rest that crossed a day with no act of its own still gets the interval it owes |
| Generic conversation opened | `DramaChoiceProjector` → `AdvanceFromDialogue` → `ReconcileIfTheZoneChanged` → `AdvanceThreadsIfTheDayTurned` | Expression may catch up a day that turned and may do nothing else; it can neither advance the world nor be required to |
| Attach/zone changes | `MaybeGenerateLocalSituation` → `SettlementSituationGenerator.Evaluate` → `SituationProposalSelection.Rank` → `TryGenerateSelected` through `TryGenerate`; separate Home generation | Admission before native theft mutation; established facts/threads remain unknown to player unless learned |
| Ignored known matter | `AutonomousInterventions.Advance` → actor context/registry offers → `ActionIntent` → `ActionAttempt.Run` | Same verb/check path as player; deed/ending/claim, no free player learning |
| Generic `_chara/main` conversation | `DramaChoiceProjector` → `ActionRegistry.Discover` → `ContextualActionProjection` → click revalidation → verb | Native choices and action outcomes; does not host the routed storylet engine |
| Shared verb attempt | `GetAvailability` → `Perform` → optional `ICheckResolver.Resolve`, native/owned writes, `Record` | Systemic native actions need no second roll; semantic binding survives into event meaning |
| Player proximity | `AmbientTalk.Next` → `ElinBark.Speak` acknowledgment → `AmbientTalk.Deliver` | Only delivery teaches player and spends cooldown; `TownNews` is the requested route |
| Journal open | `NativeJournalSurface` → `JournalPageRegistry` projections → `NativeJournalRenderer` | Derived UI or fallback; no new knowledge from rendering |
| Pre-save | `Persist` → `WorldStateSerializer.Save` → GameIO chunk | Owned records, not native instances, dialogue text or content bundle |

Join sources beyond the host:
[ElinActionObserver](../../src/BrilliantQuesting.Plugin/ElinActionObserver.cs),
[VanillaActionRecorder](../../src/BrilliantQuesting.Core/Integration/VanillaActionRecorder.cs),
[DramaChoiceProjector](../../src/BrilliantQuesting.Plugin/DramaChoiceProjector.cs),
[ActionAttempt](../../src/BrilliantQuesting.Core/Actions/ActionAttempt.cs),
[ConsequenceEngine](../../src/BrilliantQuesting.Core/Consequences/ConsequenceEngine.cs).
Proof routes: [Actions](../agent/validation.md#actions), [World](../agent/validation.md#world),
[Native](../agent/validation.md#native). A sandbox test of the join does not prove hook timing in Elin.

## Core and Lab expression joins

```text
saved facts/beliefs/obligations/history
  -> DevelopmentDetector.Detect -> Development (derived; no required scene)
  -> DevelopmentExpression.Opportunities -> StoryletCastingContext
  -> StoryletEngine.Find/Evaluate (also callable directly by a host)
  -> qualified groups (StoryletCasting) -> StoryletChemistry -> StoryletOpportunity
  -> host presentation admission (FindForPresentation / TryPresent when used)
  -> StoryletRouter.Play -> PlayBeat
       -> Disclosure.Decide/Compose for an offered answer to a prior Ask
          otherwise ActorIntent.Choose -> SpeechAct
       -> optional CheckRequest -> ICheckResolver
       -> callback permission + RealizationRequest -> DialogueRealizer -> RealizedLine
       -> Apply semantic consequences -> events/owned state
       -> BeatRoute -> next beat or ThreadResolution
  -> StoryletFiring on thread + transient StoryletPlay/PlayedBeat report
```

The host must explicitly supply the world, definitions, resolver and optional realizer. `Fire` is
the simple recording path; `Play` records its own routed firing. The router can apply semantics
without a rendered line; it is not a native delivery acknowledgment. In particular, do not wire
this wholesale to player learning without a delivery policy. Beat `PlayerIntersections` describe
intersection IDs in the play result; they are not native buttons by themselves.

Sources: [DevelopmentDetector](../../src/BrilliantQuesting.Core/Developments/DevelopmentDetector.cs),
[DevelopmentExpression](../../src/BrilliantQuesting.Core/Developments/DevelopmentExpression.cs),
[StoryletEngine](../../src/BrilliantQuesting.Core/Storylets/StoryletEngine.cs),
[StoryletRouter](../../src/BrilliantQuesting.Core/Storylets/StoryletRouter.cs),
[SceneFixture](../../tools/BrilliantQuesting.Lab/Scenes/SceneFixture.cs).
Proof: [DevelopmentLayerTests](../../tests/BrilliantQuesting.Core.Tests/DevelopmentLayerTests.cs),
[StoryletRoutingTests](../../tests/BrilliantQuesting.Core.Tests/StoryletRoutingTests.cs),
[SemanticConversationIntegrationTests](../../tests/BrilliantQuesting.Core.Tests/SemanticConversationIntegrationTests.cs).

## Core production cycle

```text
supplied observations (VanillaActionRecorder)
  -> PressureFeedback.Inspect/Take -> PressurePass (bounded work set + woken people)
  -> DevelopmentDetector.Detect(scope) -> Development
  -> ActorPressureView.Of -> ActorLocalPressure
  -> ActorGoalEvolution.Advance -> GoalChange / NpcGoal
  -> GoalRoutes.Discover -> GoalRoute -> ActionIntent -> ActionCandidate
  -> ArbitrationBatch.Gather/Resolve -> ActionAttempt.Run
  -> attached ConsequenceEngine and the verbs' own owners
  then, in the same pass, for the bodies:
  -> OrganizationEnrollment.Advance -> raised/admitted roster
  -> OrganizationPressureView.Of -> OrganizationGoalEvolution.Advance
  -> OrganizationActivity.Advance(roster) -> bookkeeping or a member's ActionAttempt
  -> changed state -> back into PressureFeedback for the next interval
```

One interval per pass, recorded in `ProductionCycleLedger`; replaying a consumed interval does
nothing, and a re-entrant call from an immediate listener is refused rather than run. Attempts
happen inside the batch and nowhere else on this path. The institutional half runs after the
people's, so an opening a person committed is already closed when their body looks at the same day
(BQa-020); which bodies it reaches is [enrollment](world.md#organizations)'s answer, not a sweep of
the registry. The same runner is called by the Lab's
[production registry](../../tools/BrilliantQuesting.Lab/ProductionSystemRegistry.cs) and by the host
through [LiveWorldCycle](../../src/BrilliantQuesting.Plugin/LiveWorldCycle.cs) (BQa-017), which adds
hooks and failure absorption rather than scheduling: the interval gate is the persisted marker, not a
Plugin cursor, and a pass that throws closes its interval instead of replaying its finished half.
Live hook timing and vanilla catch-up ordering remain a real save's to prove.

Source: [ProductionCycle](../../src/BrilliantQuesting.Core/Autonomy/ProductionCycle.cs),
[OrganizationEnrollment](../../src/BrilliantQuesting.Core/World/OrganizationEnrollment.cs),
[LiveWorldCycle](../../src/BrilliantQuesting.Plugin/LiveWorldCycle.cs).
Contract: [autonomy](world.md#autonomy), [organizations](world.md#organizations),
[persistence](integration.md#persistence).
Proof: [ProductionCycleTests](../../tests/BrilliantQuesting.Core.Tests/ProductionCycleTests.cs),
[LiveOrganizationAgencyTests](../../tests/BrilliantQuesting.Core.Tests/LiveOrganizationAgencyTests.cs),
[LiveWorldCycleTests](../../tests/BrilliantQuesting.Core.Tests/LiveWorldCycleTests.cs),
[IntegrationHarnessTests](../../tests/BrilliantQuesting.Lab.Tests/IntegrationHarnessTests.cs).

## Partial joins and extension points

The [generation proposal seam](world.md#generation) also accepts hypothetical actor requirements
for headless comparison. Selection only returns a candidate to its owner; it does not satisfy those
requirements. The live settlement owner proposes existing actors only and refuses hypothetical
creation at handoff. BQ-152 supplies pre-creation comparison; BQ-103 subtracts explicit actor/premise
creation costs in that selection seam. Neither adds a director-to-spawner join or charges existing news.

| Join | Current limit / reusable seam |
|---|---|
| All pressures → goals | Detector implements unproven knowledge/unmet obligations; no universal development-to-goal pass. Reuse `NpcGoal`, `GoalFormationTrace` and `ActionIntent`; retain null for unattemptable choices |
| Director → all world/scene selection | `DevelopmentScoring` ranks eligible news; attention also gates generation, and sincerity gates host presentations. No universal director owns every decision |
| Routed storylets → live Drama | Plugin has no `StoryletRouter`/`StoryletEngine` host. Preserve semantic/wording/delivery boundaries when adding one |
| Stable voice → save | `VoiceProfile` is caller-supplied; `NarrativeNpc`/serializer do not store an assigned voice. Reuse existing tone/idiolect vocabulary if persistent assignment is introduced |
| Generated organization activity → live tick | Joined inside `ProductionCycle` (BQa-020), so the Lab and the host run one pass rather than two schedules. Headless/source only: no live game has been observed raising a body from an observed office or ticking one |
| Core production cycle → live tick | Joined by `LiveWorldCycle` on the day-turn, zone-visit and dialogue paths (BQa-017), reconstructed from the save's own markers. Headless/source evidence only: no Elin hook, callback timing, travel/rest cadence or vanilla catch-up ordering has been observed in a running game |
| Site plan → native structure → future spatial pressure | Core plan/realization/addition exists; live structure/addition capabilities refuse. Do not promote plan geometry to native fact |
| Native catch-up → BQ tiers | Plugin attach/zone change reads Home state through `OffScreenSchemes.ReconcileZone`; observed Active residents consume elapsed scheme windows without replaying physical work. [Tier contract](world.md#autonomy); actual Home revisit timing/readback still needs live evidence |

The [extension register](extension-seams.md) links these limits to proofs and committed planning.
The [adopted BQa roadmap](../living-world-roadmap.md) owns the post-BQ work on these joins;
its planned cycle is not implemented by the current call sites described here.
