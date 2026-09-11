# Extension seams and planning handoff

[Router](../architecture.md) · [flow](flow.md). This is a boundary/gap register, not progress tracking.
Each row preserves an existing authority. The [BQ roadmap](../implementation-roadmap.md) owns
unfinished launch/hardening, followed by the authoritative
[BQa living-world roadmap](../living-world-roadmap.md). Search the exact token in its owning plan
for dependencies and done-when; rows below do not declare a step complete or incomplete.

The adopted BQa plan extends these sources after its BQ start gate. Its
[adoption audit](../agent/bqa-roadmap-audit.md) records repository evidence and renumbering;
unadopted proposals remain provisional. No planning link upgrades native evidence or makes a
Core/Lab join live.

## Post-BQ contract routing

These are future implementation owners; the current source/test cards below retain authority.

| Join | Adopted BQa owner |
|---|---|
| Durable causal identity and decision provenance | BQa-001 |
| Simulation/expression RNG and decision-key isolation | BQa-002 |
| Classified uncertainty and feasibility | BQa-003–BQa-005 |
| Pressure → local interpretation → desired conditions → registered actions → feedback | BQa-006–BQa-013 |
| Evidence-graded opportunity → competition | BQa-014–BQa-015 |
| Shared Core cycle → Plugin recurring-cycle integration | BQa-016–BQa-017 |
| Institutional information → goals → supported execution → live enrollment | BQa-018–BQa-020 |
| Existing causes → proposals → safe establishment → cross-matter recurrence | BQa-021–BQa-025 |
| Payload/voice → incremental interaction → player controls → live continuation | BQa-026–BQa-032 |
| Verified physical consequences and honest unsupported exits | BQa-033–BQa-035 |
| Player-known journal/discovery/commissions and exposure feedback | BQa-036–BQa-039 |
| Production range, stress, causal audit and real-save beta acceptance | BQa-040–BQa-045 |

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
| Intent/disclosure/speech/conversation | BQ-070–073/083/146; meaning before words, no free disclosure | Further host integration must reuse `SpeechAct`, `ActionBinding`, `Disclosure` and commitment events. BQa-029–BQa-032 own subsequent integration. [Flow](flow.md#partial-joins-and-extension-points) |
| Realization/voice/repetition/weirdness | BQ-074–079/127/128/142/147–151; eligible wording preserves meaning | BQ-104/133 coverage; BQ-149–151 address contextual trait gates, diversity metrics and ordinary high-frequency wording. BQa-028 owns durable voice assignment and its save decision. [Controls](expression.md#expression-controls) |
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
