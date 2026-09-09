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
| `EVENT.ActPerformed` | `OnActPerformed` → `ElinActionObserver.Observe` → `VanillaActionRecorder` → `NarrativeWorldState.Record` | Recognized acts become history; unknown payloads ignored, native outcomes not rolled again |
| New event | `EventLedger.Append` → attached `ConsequenceEngine.Handle` | Knowledge/tension then profile-based social/memory/standing reactions; no blanket claim that every fact comes from this listener |
| Day changes after acts | `AdvanceThreadsIfTheDayTurned` → `AdvanceThreads` → `ThreadLifecycle.Review`, `ThreadEngine.Advance`, autonomy, schemes, adventurers, travel; then rumor circulation | Host order is explicit; each owner retains its own one-time state/gates |
| Attach/zone changes | `MaybeGenerateLocalSituation` → `SettlementSituationGenerator.Evaluate/TryGenerate`; separate Home generation | Admission before native theft mutation; established facts/threads remain unknown to player unless learned |
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

## Partial joins and extension points

| Join | Current limit / reusable seam |
|---|---|
| All pressures → goals | Detector implements unproven knowledge/unmet obligations; no universal development-to-goal pass. Reuse `NpcGoal`, `GoalFormationTrace` and `ActionIntent`; retain null for unattemptable choices |
| Director → all world/scene selection | `DevelopmentScoring` ranks eligible news; attention also gates generation, and sincerity gates host presentations. No universal director owns every decision |
| Routed storylets → live Drama | Plugin has no `StoryletRouter`/`StoryletEngine` host. Preserve semantic/wording/delivery boundaries when adding one |
| Stable voice → save | `VoiceProfile` is caller-supplied; `NarrativeNpc`/serializer do not store an assigned voice. Reuse existing tone/idiolect vocabulary if persistent assignment is introduced |
| Generated organization activity → live tick | Called by Lab `ProductionSystemRegistry`, not instantiated in Plugin. No claim of live organization simulation |
| Site plan → native structure → future spatial pressure | Core plan/realization/addition exists; live structure/addition capabilities refuse. Do not promote plan geometry to native fact |
| Native catch-up → BQ tiers | Source-observed vanilla behavior; no completed general reconciliation/tiering join. D021 applies before introducing scheduling |

The [extension register](extension-seams.md) links these limits to proofs and committed planning.
No provisional future roadmap is an implementation requirement.
