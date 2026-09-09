# Expression and attention

[Router](../architecture.md) · [Expression validation](../agent/validation.md#expression)
· [maintenance](../agent/documentation.md). These contracts are headless; routed scenes have a
Core/Lab host, not an implicit live Drama host. See [flow](flow.md). Semantic design lives in
[character dialogue](../design/character-dialogue-system.md); prose tasks alone use the writing reference.

## Storylets

**Owns:** reusable authored patterns, eligibility and routed beats. `StoryletEngine.Find` returns
opportunities; `Fire` records a firing, whereas `StoryletRouter.Play` executes routed beats and
records the played firing. Inputs: definitions, thread/fact/cast context. Outputs: opportunities,
played beats, semantic consequences and routes. Definitions are bundle content; opportunities/play
traces are transient; firings on threads are saved by IDs/bindings. **Does not own:** generating truth
to satisfy a pattern, automatically delivering a scene, or a duplicate action resolver. Do not call
both firing paths for one presentation. Consumers: Lab scene/playground hosts and diagnostics.

Source: [StoryletEngine](../../src/BrilliantQuesting.Core/Storylets/StoryletEngine.cs),
[StoryletRouter](../../src/BrilliantQuesting.Core/Storylets/StoryletRouter.cs).
Proof: [StoryletEngineTests](../../tests/BrilliantQuesting.Core.Tests/StoryletEngineTests.cs),
[StoryletRoutingTests](../../tests/BrilliantQuesting.Core.Tests/StoryletRoutingTests.cs).
Lab: [scene](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/SceneScenario.cs).

## Casting

**Owns:** role qualification and whole-group selection. Inputs: role requirements, claim, place,
life/presence, household, identity affordances, knowledge, goals and ties. Outputs: distinct actor
bindings and explainable chemistry terms (goal conflict, shared history, knowledge/power asymmetry).
Search/context/scores are transient; played bindings are saved with firings. **Does not own:**
identity stereotypes, personality generation, moving actors into a room or granting knowledge.
Chemistry ranks qualified groups; it cannot excuse an invalid role. Consumer: storylet evaluation.

Source: [StoryletCasting](../../src/BrilliantQuesting.Core/Storylets/StoryletCasting.cs),
[StoryletChemistry](../../src/BrilliantQuesting.Core/Storylets/StoryletChemistry.cs).
Proof: [StoryletCastingTests](../../tests/BrilliantQuesting.Core.Tests/StoryletCastingTests.cs),
[StoryletChemistryTests](../../tests/BrilliantQuesting.Core.Tests/StoryletChemistryTests.cs).
Lab: [scene](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/SceneScenario.cs).
Native dependencies: [identity, life, Home/companions](../elin/capabilities.md).

## Intent and disclosure

**Owns:** choice of communicative meaning. `ActorIntent.Choose` scores offered intentions against
actor state; `Disclosure.Decide` answers willingness/depth/tactic when asked about a known claim.
Inputs: speaker/listener, proposition, ties, privacy, risk, leverage and character state. Outputs:
explainable decisions and composed `SpeechAct`s. Decisions are transient. **Does not own:** changing
belief just because willingness changed, wording, or general NPC action selection. The router asks
Disclosure for answer beats and uses ActorIntent for other offered moves. Consumers: routed scenes,
inquiry actions, realization request builders.

Source: [ActorIntent](../../src/BrilliantQuesting.Core/Storylets/ActorIntent.cs),
[Disclosure](../../src/BrilliantQuesting.Core/Dialogue/Disclosure.cs).
Proof: [DisclosureDecisionTests](../../tests/BrilliantQuesting.Core.Tests/DisclosureDecisionTests.cs),
[StoryletRoutingTests](../../tests/BrilliantQuesting.Core.Tests/StoryletRoutingTests.cs).
Lab: [playground](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/PlaygroundScenario.cs).

## Semantic communication

**Owns:** wording-free `SpeechAct` meaning, profile/stance, participants, `ActionBinding` content
and reply linkage. `ConversationState` tracks questions, claims, contradictions and commitments
within an exchange. Inputs: chosen meaning/recorded speech. Outputs: semantic signatures, reply
context and selected durable commitment events. Acts/conversation state are transient; meaningful
commitments/deceptions survive through existing events/obligations. **Does not own:** a second RPG
verb library, new truth inferred from wording, or saving entire conversations. A lie is stance
against belief, not a separate speech-act type. Consumers: disclosure, router, realizer, diagnostics.

Source: [SpeechAct](../../src/BrilliantQuesting.Core/Dialogue/SpeechAct.cs),
[ConversationState](../../src/BrilliantQuesting.Core/Dialogue/ConversationState.cs).
Proof: [SemanticSpeechActTests](../../tests/BrilliantQuesting.Core.Tests/SemanticSpeechActTests.cs),
[ConversationStateTests](../../tests/BrilliantQuesting.Core.Tests/ConversationStateTests.cs).
Lab: [playground](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/PlaygroundScenario.cs).

## Realization

**Owns:** wording already-decided meaning from eligible compiled fragments. Inputs: a
`RealizationRequest` containing semantic act, explicit wording data, voice/context and RNG.
Outputs: `RealizedLine` with unchanged meaning/signature or an explained refusal. No world reference,
no authoritative writes, no saved generated lines. **Does not own:** outcomes, facts or semantic
substitution when no fragment fits. Fragment eligibility must justify every asserted world detail.
Consumers: routed scenes and Lab presentations; it does not replace all current native action prose.

Source: [DialogueRealizer](../../src/BrilliantQuesting.Core/Dialogue/DialogueRealizer.cs),
[Realization](../../src/BrilliantQuesting.Core/Dialogue/Realization.cs).
Proof: [FragmentRealizationTests](../../tests/BrilliantQuesting.Core.Tests/FragmentRealizationTests.cs),
[FragmentSemanticHonestyTests](../../tests/BrilliantQuesting.Core.Tests/FragmentSemanticHonestyTests.cs).
Lab: [playground-sweep](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/PlaygroundSweepScenario.cs).

## Expression controls

**Owns:** stable `VoiceProfile`/idiolect inputs, contextual tone/vocabulary selection,
`DialogueExpressionHistory` repetition counts and `WeirdnessBudget` premise limits. Inputs: character
and conversation context plus authored metadata. Outputs: a narrower eligible fragment pool, not
new meaning. Voice profiles are supplied by the host, not fields on saved `NarrativeNpc`; expression
history and weirdness budgets are also transient. Durable voice assignment is an open host seam.
**Does not own:** memory/belief, a second register system, or relaxing semantic honesty to avoid
repetition. Optional stale wording can disappear; the last valid core remains available under
repetition pressure. `SincerityBudget` is separate host-session presentation pacing, not emotion.

Source: [VoiceProfile](../../src/BrilliantQuesting.Core/Dialogue/VoiceProfile.cs),
[DialogueExpressionHistory](../../src/BrilliantQuesting.Core/Dialogue/DialogueExpressionHistory.cs),
[WeirdnessBudget](../../src/BrilliantQuesting.Core/Dialogue/WeirdnessBudget.cs),
[SincerityBudget](../../src/BrilliantQuesting.Core/Storylets/SincerityBudget.cs).
Proof: [VoiceIdiolectTests](../../tests/BrilliantQuesting.Core.Tests/VoiceIdiolectTests.cs),
[RepetitionControlTests](../../tests/BrilliantQuesting.Core.Tests/RepetitionControlTests.cs),
[WeirdnessBudgetTests](../../tests/BrilliantQuesting.Core.Tests/WeirdnessBudgetTests.cs).
Lab: [playground-sweep](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/PlaygroundSweepScenario.cs).

## Attention

**Owns:** admission of new generated matters and unsolicited exposure (`NarrativeAttentionBudget`),
ranking eligible news (`DevelopmentScoring`), repeated shape penalties and recent semantic niche
occupancy (`SituationFingerprint` / `SituationRepetition`).
Inputs: threads, known claims, actual delivery history, location and declared recovery routes.
Outputs: admission/refusal and scored candidates for generator/ambient news. Policy/scores are
transient; delivery events and cooldown stamps belong to saved world state. **Does not own:** truth,
resolution, player knowledge or suppressing requested actions/news. Scoring reads every eligible
candidate before selection; unknown evidence contributes no invented score. Sincerity admission
is acknowledged by a successful host presentation, not by finding a storylet.

Quality-diversity selection adds at most 0.75 to the existing quality score: `0.75 * (1 -
occupancy share)`, using known semantic domain sets as niches. It reuses the fingerprint policy's
three most recent distinct encountered matters within seven game days, including resolved matters,
excluding quarantined/unseen matters and deduplicating shared encountered claims. The denominator
contains only classified encounters; unknown candidates or no classified history earn nothing.
Multiple candidate carriers take the smallest bonus. Eligibility and attention refusal still apply
before ranking. Inspector scores explain the niche, occupancy and bonus beside quality and penalties.
This ranks ambient proposals only; it neither generates situations nor stores a novelty archive.
History is reconstructed from current player-held claims, not a snapshot of past experience; actual
delivery changes subsequent occupancy. Native pacing quality still requires live observation.

Source: [NarrativeAttentionBudget](../../src/BrilliantQuesting.Core/Threads/NarrativeAttentionBudget.cs),
[DevelopmentScore](../../src/BrilliantQuesting.Core/Threads/DevelopmentScore.cs),
[SituationFingerprint](../../src/BrilliantQuesting.Core/Threads/SituationFingerprint.cs).
Proof: [NarrativeAttentionBudgetTests](../../tests/BrilliantQuesting.Core.Tests/NarrativeAttentionBudgetTests.cs),
[DevelopmentScoringTests](../../tests/BrilliantQuesting.Core.Tests/DevelopmentScoringTests.cs),
[SituationFingerprintTests](../../tests/BrilliantQuesting.Core.Tests/SituationFingerprintTests.cs).
Lab: [ambient](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/AmbientScenario.cs).
Native dependency: current location/bark delivery in [capabilities](../elin/capabilities.md).

## Content

**Owns:** build-time YAML compilation and runtime bundle validation; authored storylet, fragment,
grammar and piece definitions. Inputs: content source and known vocabulary. Output: `content.bqc`,
loaded without a runtime YAML parser. Content is shipped separately, never copied into the save;
history keeps stable IDs/bindings. **Does not own:** simulation rules or arbitrary executable scripts.
Consumers: storylets, realizer, site planners. Changing a semantic vocabulary requires validating the
compiler/loader and actual bundle together.

Source: [ContentCompiler](../../tools/ContentCompiler/Program.cs),
[ContentBundleLoader](../../src/BrilliantQuesting.Core/Content/ContentBundleLoader.cs).
Proof: [ContentBundleLoaderTests](../../tests/BrilliantQuesting.Core.Tests/ContentBundleLoaderTests.cs),
[StoryletContentTests](../../tests/BrilliantQuesting.Core.Tests/StoryletContentTests.cs).
Lab: [scene](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/SceneScenario.cs),
[dungeon](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/DungeonScenario.cs).
Detailed contract: [content pipeline](../design/content-pipeline.md).
