# Extension seams and planning handoff

[Router](../architecture.md) · [flow](flow.md). This is a boundary/gap register, not progress tracking.
Each row preserves an existing authority and routes planning to the committed
[implementation roadmap](../implementation-roadmap.md). Search the exact BQ token there for order,
dependencies and done-when; rows below do not declare a step complete or incomplete.

No finalized, explicitly authoritative BQa roadmap was found in this audit. Any separately supplied
BQa proposal remains provisional design input. Do not assign numbers/dependencies here. If a future
roadmap is formally adopted, add its canonical link and affected contract links; retain the subsystem
headings and source/test ownership. Provisional ideas must not outrank code, tests, evidence or BQ.

## Foundations and extension routing

| Affected subsystem | BQ foundation to preserve | Committed planning to consult / safe seam |
|---|---|---|
| History, truth, beliefs/proof | BQ-001/010/014–020; IDs, queued history, truth/belief/proof separation | BQ-105/106 integrity/migration work; reuse existing stores, never reconstruct by rewriting events. [State](state.md) |
| Memory, callbacks, provenance | BQ-021/081–086; saved memory versus derived history views | BQ-103 conservation, BQ-107/108 scale; extend record-backed recall, not a parallel memory ledger. [Memory](state.md#memory-and-continuity) |
| Relationships/obligations/standing | BQ-022/055/113/118/136; ties and favors distinct from native standing | BQ-125 family weighting, BQ-103 reuse; retain native guild uncertainty. [Social](state.md#social-state) |
| Character goals/personality/emotion | BQ-056–064/077/080; independent character dimensions and inspectable choices | BQ-107 fidelity; extend choice-to-action bindings without substituting unattemptable goals. [Character](state.md#character-state) |
| Identity and activity | BQ-144/145/135; unknown facets, stable identity, single affordance derivation | BQ-107/109/110 require runtime coupling/evidence, not invented activity. [Identity](state.md#identity), [activity](world.md#activity) |
| Actions/availability/checks/reactions | BQ-004/023–029/093/134/137; one verb library, semantic binding, deterministic uncertainty | BQ-109 degradation and BQ-110 update smoke; native outcomes stay native. [Actions](actions.md) |
| Threads/developments | BQ-052/069; durable matters distinct from derived pressures | BQ-102/103 selection/conservation and BQ-105 integrity; extend detector rules only over owned state. [Developments](actions.md#developments) |
| Generation/archetypes | BQ-039–051/103/114/115/124/152; pressure-led establishment, recoverable outcomes, read-only proposals with actor/premise creation costs | Hypothetical fulfillment remains owner responsibility; ranking does not establish state. BQ-125/126 family weighting, setting seeds; reuse registry/facts/sites. [Generation](world.md#generation) |
| Storylets/casting/chemistry | BQ-065–068/146; qualification before chemistry, actor-selected beat meaning | BQ-102/104 diversity/harness; reuse casting and routed beats rather than another scene database. [Expression](expression.md) |
| Intent/disclosure/speech/conversation | BQ-070–073/083/146; meaning before words, no free disclosure | Further host integration must reuse `SpeechAct`, `ActionBinding`, `Disclosure` and commitment events. No new committed step inferred from this gap. [Flow](flow.md#partial-joins-and-extension-points) |
| Realization/voice/repetition/weirdness | BQ-074–079/127/128/142/147–151; eligible wording preserves meaning | BQ-104/133 coverage; BQ-149–151 address contextual trait gates, diversity metrics and ordinary high-frequency wording. Durable voice assignment needs an explicit owner/save decision, not inferred BQa work. [Controls](expression.md#expression-controls) |
| Director/attention | BQ-098–101; selection doesn't deliver or alter truth | BQ-102/103/104/119/120 selection, conservation, telemetry/intensity; reuse delivery history. [Attention](expression.md#attention) |
| Autonomy/travel | BQ-093–098; shared attempts, semantic milestones, no duplicate embodiment | BQ-107/108 scale/catch-up; respect absence/global movement ownership. [World](world.md) |
| Organizations | BQ-053/054; generated organization state distinct from vanilla guilds | BQ-116 supply coupling and BQ-107 scheduling; do not invent a second native economy. [Organizations](world.md#organizations) |
| Sites | BQ-087–092/139/140/143; reuse, semantic planning, causal contents, once-only realization | BQ-141 spatial expressive range; larger settlements/spatial feedback in [places design](../design/procedural-places-and-spatial-history.md) are intent, not proof of a live join |
| Player discovery/presentation | BQ-033–036/117/121/138; player knowledge gates and owned native pages | BQ-109/110 failure/update smoke; corrective visual acceptance remains required. [Integration](integration.md) |
| Persistence/native adapter | BQ-003/010/011/030–032; fail-closed mutation, reattach without replay | BQ-105–110 hardening, fixtures, tiers and runtime gates; evidence before capability promotion. [Persistence](integration.md#persistence) |
| Content | BQ-129–133; behavior/content/history separation | BQ-104/141 range harnesses; extend vocabulary with a real consumer and compiler validation. [Content](expression.md#content) |

## Demonstrated gaps to consult before extending

| Gap / consequence | Evidence and authority to reuse |
|---|---|
| No general development → goal → action scheduler | [DevelopmentDetector](../../src/BrilliantQuesting.Core/Developments/DevelopmentDetector.cs) has two pressure rules; [ActionIntent.FromGoalChoice](../../src/BrilliantQuesting.Core/Actions/ActionAttempt.cs) returns null for unattemptable choices. [DevelopmentLayerTests](../../tests/BrilliantQuesting.Core.Tests/DevelopmentLayerTests.cs), [actor-action Lab](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/ActorActionScenario.cs). Preserve pressure without forced scene/quest |
| Routed scenes have no live Plugin host; player intersection IDs are not native intervention controls | [StoryletRouter](../../src/BrilliantQuesting.Core/Storylets/StoryletRouter.cs) and [StoryletRoutingTests](../../tests/BrilliantQuesting.Core.Tests/StoryletRoutingTests.cs) prove Core routing; [DramaChoiceProjector](../../src/BrilliantQuesting.Plugin/DramaChoiceProjector.cs) projects actions/news. Reuse delivery acknowledgment before exposing semantic learning |
| Voice vocabulary has no persisted actor assignment | [VoiceProfile](../../src/BrilliantQuesting.Core/Dialogue/VoiceProfile.cs) is host input; [NarrativeNpc](../../src/BrilliantQuesting.Core/World/NarrativeNpc.cs) and [serializer](../../src/BrilliantQuesting.Core/Persistence/WorldStateSerializer.cs) contain no voice field. [VoiceIdiolectTests](../../tests/BrilliantQuesting.Core.Tests/VoiceIdiolectTests.cs) prove wording constraints, not saved assignment |
| Lab and Plugin are different orchestration hosts | [ProductionSystemRegistry](../../tools/BrilliantQuesting.Lab/ProductionSystemRegistry.cs) invokes `OrganizationActivity`; [Plugin](../../src/BrilliantQuesting.Plugin/BrilliantQuestingPlugin.cs) does not. Broad Lab coverage is not proof that all systems tick in Elin |
| Automatic settlement selection is narrower than archetype library | [SettlementSituationGenerator](../../src/BrilliantQuesting.Core/Situations/SettlementSituationGenerator.cs) returns theft; separate establishment paths exist. [SettlementSituationGeneratorTests](../../tests/BrilliantQuesting.Core.Tests/SettlementSituationGeneratorTests.cs). New selector must preserve pre-mutation admission |
| Settlement membership remains a narrower unresolved question than social eligibility | [ELIN-Q-0027 correction](../elin/verification/unresolved.md#supersession-corrections): theft already requires `SocialAgency.Full`, with animal/unknown exclusions in [SettlementSituationGeneratorTests](../../tests/BrilliantQuesting.Core.Tests/SettlementSituationGeneratorTests.cs). A general citizenship read is still unverified; do not mistake mutation class or social capacity for residence |
| Physical evidence, structured sites and additive ground mutation remain incomplete live joins | [ELIN-Q-0008/0032/0033](../elin/verification/unresolved.md), [SiteMutationTests](../../tests/BrilliantQuesting.Core.Tests/SiteMutationTests.cs), [dungeon Lab](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/DungeonScenario.cs). Reuse capability/refusal seams; do not claim a plan physically exists |
| Identity job-token inference and duplicate UID recurrence need live samples | [ELIN-Q-0028/0030/0031](../elin/verification/unresolved.md); preserve identity/affordance authorities, not a second occupation classifier |
| Corrective journal visual acceptance still open | [Journal evidence/checklist](../elin/api/journal-ui.md), [NativeJournalSurfaceTests](../../tests/BrilliantQuesting.Core.Tests/NativeJournalSurfaceTests.cs). Headless mounting proves isolation/fallback, not layout |

These gaps are intentionally not feature changes in this pass. New code defects belong beside the
owning gap/evidence, with an exact reproduction or test hypothesis; do not “fix” code to match stale prose.
