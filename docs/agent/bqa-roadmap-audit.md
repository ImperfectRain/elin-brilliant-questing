# BQa roadmap adoption audit

This is the fixed rationale for adopting the [living-world roadmap](../living-world-roadmap.md),
not a maintained implementation-status file. The supplied v4 document was treated as proposed
planning, not as instructions or evidence. No production code, content, schemas or BQa feature tests
were changed by this audit. Future implementation status must be established from Git/source/tests
and operation-specific runtime evidence.

## Repository-grounded assessment

The foundation is substantial: deterministic IDs/RNG, queued event dispatch, distinct facts and
beliefs/proof, persisted individual and organization state, shared actions, lifecycle, bounded
off-screen schemes, native action observation, player-filtered discovery and a compiled expression
pipeline. It supports extending owners rather than rebuilding a simulation framework. It does not
yet constitute the complete causal loop promised by the beta.

Source and representative test observations used in the audit:

| Finding | Source and representative proof | Implication for the plan |
|---|---|---|
| Events have stable IDs and queued dispatch, but no general typed parent/decision provenance | [WorldEvent](../../src/BrilliantQuesting.Core/Events/WorldEvent.cs), [Record](../../src/BrilliantQuesting.Core/World/NarrativeWorldState.cs), [EventLedger](../../src/BrilliantQuesting.Core/Events/EventLedger.cs), [FoundationTests](../../tests/BrilliantQuesting.Core.Tests/FoundationTests.cs) | Extend these in BQa-001; do not invent another ledger or assert current IDs are absent |
| Detector has two rules and filters unproven claims to true facts | [DevelopmentDetector](../../src/BrilliantQuesting.Core/Developments/DevelopmentDetector.cs), [DevelopmentLayerTests](../../tests/BrilliantQuesting.Core.Tests/DevelopmentLayerTests.cs) | BQa-006 broadens pure readings; BQa-007 must enumerate belief-only concern independently |
| Goals persist kind/subject/weight/reason/satisfied, with no general desired-condition/provenance lifecycle | [NarrativeNpc](../../src/BrilliantQuesting.Core/World/NarrativeNpc.cs), [serializer](../../src/BrilliantQuesting.Core/Persistence/WorldStateSerializer.cs), [ProblemSolvingStyleTests](../../tests/BrilliantQuesting.Core.Tests/ProblemSolvingStyleTests.cs) | BQa-008–BQa-011 extend goal/action semantics and preserve character differences |
| Off-screen selection matches substrings; `GoalSatisfiedBy` accepts any successful event-bearing attempt | [OffScreenSchemes](../../src/BrilliantQuesting.Core/Autonomy/OffScreenSchemes.cs), [OffScreenSchemeTests](../../tests/BrilliantQuesting.Core.Tests/OffScreenSchemeTests.cs) | BQa-011 tests actual desired state and legitimate actor assessment; current month fixture stages goals and performs one catch-up pass |
| Shared registry/attempt already rechecks availability and preserves null for unattemptable chosen intent | [ActionAttempt](../../src/BrilliantQuesting.Core/Actions/ActionAttempt.cs), [NarrativeAction](../../src/BrilliantQuesting.Core/Actions/NarrativeAction.cs), [ActionBindingTests](../../tests/BrilliantQuesting.Core.Tests/ActionBindingTests.cs) | Preserve these; effect declarations and matching are missing joins, not another action library |
| Portable resolver linearly adds opposed terms and uses declared dice/critical windows | [resolver](../../src/BrilliantQuesting.Core/Checks/VanillaStyleCheckResolver.cs), [CheckTests](../../tests/BrilliantQuesting.Core.Tests/CheckTests.cs), [native checks evidence](../elin/api/checks-elements-skills.md) | Classify/refine arithmetic without assuming native RNG is authoritative or removing fumbles by accident |
| Business continuity is durable, but a test explicitly changes it; sleeping/shift absence stays temporary | [BusinessContinuity](../../src/BrilliantQuesting.Core/World/BusinessContinuity.cs), [BusinessContinuityTests](../../tests/BrilliantQuesting.Core.Tests/BusinessContinuityTests.cs) | BQa-012 owns real intake/feedback, not just another test which calls `TryChangeState` as a scripted sequel |
| Plugin advances lifecycle, escalation, autonomy, schemes, adventurers and travel; generation remains gated by any existing thread | [Plugin](../../src/BrilliantQuesting.Plugin/BrilliantQuestingPlugin.cs), [flow](../systems/flow.md#live-host-joins) | BQa-016 defines bounded cycle ownership and BQa-017 integrates it live; BQa-024 removes bootstrap-only recurrence and preserves event-independent wakeup |
| Core organization activity exists, but unknown goals and failed operations can fall back to wealth; recruitment searches registry order | [OrganizationActivity](../../src/BrilliantQuesting.Core/World/OrganizationActivity.cs), [OrganizationActivityTests](../../tests/BrilliantQuesting.Core.Tests/OrganizationActivityTests.cs) | Separate local institutional goals, semantic execution, then live enrollment in BQa-018–BQa-020 |
| Lab registers organization activity; Plugin does not | [ProductionSystemRegistry](../../tools/BrilliantQuesting.Lab/ProductionSystemRegistry.cs), [Plugin](../../src/BrilliantQuesting.Plugin/BrilliantQuestingPlugin.cs) | Headless integration is not live population, tick or information evidence |
| Proposals rank without allocation; current selected settlement owner performs the founding theft transfer | [SettlementSituationGenerator](../../src/BrilliantQuesting.Core/Situations/SettlementSituationGenerator.cs), [SituationProposalTests](../../tests/BrilliantQuesting.Core.Tests/SituationProposalTests.cs), [SettlementSituationGeneratorTests](../../tests/BrilliantQuesting.Core.Tests/SettlementSituationGeneratorTests.cs) | BQa-021–BQa-024 must separate autonomous incident creation from situation recognition; existing staging constructors cannot simply become recurring producers |
| Routed play selects/realizes/applies the entire scene, exposes intersection IDs and records generic beat events | [StoryletRouter](../../src/BrilliantQuesting.Core/Storylets/StoryletRouter.cs), [StoryletRoutingTests](../../tests/BrilliantQuesting.Core.Tests/StoryletRoutingTests.cs), [semantic integration tests](../../tests/BrilliantQuesting.Core.Tests/SemanticConversationIntegrationTests.cs) | BQa-029 introduces one incremental execution authority; BQa-030 adds Core choice execution before BQa-031 live hosting |
| `RealizedLine.Meaning` preserves the input signature, not a proof that words convey every required detail; voice is caller-supplied | [DialogueRealizer](../../src/BrilliantQuesting.Core/Dialogue/DialogueRealizer.cs), [VoiceProfile](../../src/BrilliantQuesting.Core/Dialogue/VoiceProfile.cs), [FragmentSemanticHonestyTests](../../tests/BrilliantQuesting.Core.Tests/FragmentSemanticHonestyTests.cs), [VoiceIdiolectTests](../../tests/BrilliantQuesting.Core.Tests/VoiceIdiolectTests.cs) | BQa-026 tests payload conveyance without asserting every current line is defective; BQa-028 assigns stable voice before live use |
| Save shape is explicit; migrations/defaults and frozen fixtures already exist | [SaveMigrations](../../src/BrilliantQuesting.Core/Persistence/SaveMigrations.cs), [MigrationFixtureTests](../../tests/BrilliantQuesting.Core.Tests/MigrationFixtureTests.cs) | Every introducing step owns its field's migration/defaults; no late generic schema project |
| Structured sites, additions and loose-place reads are refused or unresolved; corrected journal pixels remain unverified | [capability router](../elin/capabilities.md), [site questions](../elin/verification/unresolved.md), [journal checklist](../elin/api/journal-ui.md) | Evidence-gated physical exits; journal baseline acceptance stays BQ-owned, then BQa rechecks new projections |

These are source/test inspections, not newly executed gameplay tests. No live save was modified and
no native evidence was promoted. Source-level risks above are future roadmap work, not fixes in this
documentation commit.

## Canonical BQ work and acceptance still owned by BQ

The audit inspected the canonical route, launch definition, standing rules, current implementation
notes, current host and capability register. Commit subjects were only a discovery aid: missing
dedicated subjects for BQ-107, BQ-132, BQ-133 and BQ-146 do not mean those systems are absent.
Their tier, content/compiler and routing source/tests exist. Conversely a matching commit subject
does not prove a live Done-when.

| BQ scope | Remaining requirement / disposition |
|---|---|
| BQ-111 | Full player configuration contract is not present in the current small Plugin config set. Complete the settings/weighting acceptance under BQ. Existing policy properties are not shipped controls. |
| BQ-119 | Inspector diagnostics and scene telemetry are not the requested debug engagement lifecycle profile. Complete generated/surfaced/engaged/ignored/resolved-by-others accounting under BQ. |
| BQ-120 | Intensity presets depend on the preceding settings/telemetry work; no production preset implementation was found. Remains BQ-owned. |
| BQ-107 | Tier/index and reconciliation implementations exist. Actual completed Home revisit/catch-up ordering and controlled resource delta acceptance remain unverified. |
| BQ-108 | Corrected scoped measurements and synthetic large-history tests do not establish large-save live incremental frame impact. Comparable baseline and post-change Home revisit/reload capture remain open. |
| BQ-109 | Existing owner-approved limited deferral: three live disable captures, remaining 19 capability disables and gameplay-isolation checks deferred. Do not broaden this into full acceptance or a waiver of BQ launch. |
| BQ-110 | Checklist exists; first run against an identified real Elin update is unverified. A disable drill is not that run. |
| BQ-138 (corrective BQ-138h build) | Owned pages/single mount exist. Corrective visual, navigation, scrolling, reopen/reload and vanilla isolation acceptance remains with the canonical journal checklist. BQa-036 must not absorb unfinished BQ baseline acceptance. |
| BQ-003, BQ-009, BQ-011, BQ-014–BQ-018, BQ-023–BQ-032 | Preserve operation-specific open native read/write, witness, physical consequence and absence questions. Zero-delta probes and headless safety tests do not prove nonzero live mutations or hostile Grade-B persistence. Use the capability router and exact API checklist per affected operation. |
| BQ-030, BQ-037, BQ-038, BQ-048, BQ-049, BQ-051, BQ-123, BQ-135, BQ-144, BQ-145 | Home identity/attach observations are stronger than general Home catch-up, service-state, companion enumeration, activity and NPC guild evidence. Unknown facets must remain unknown. Do not claim all household/service/institutional paths are live. |
| BQ-039–BQ-047, BQ-053, BQ-054, BQ-093–BQ-098 | Core archetypes/organization/travel/autonomy are stronger than generic live generation and physical execution coverage. Existing loops are valid foundations, not proof of the proposed full ecology. Canonical baseline acceptance or an explicit scoped disposition must precede the follow-on phase. |
| BQ-087–BQ-092, BQ-139, BQ-140, BQ-143 | Site topology/content/reuse/addition proofs are largely headless; structured native realization, contents and safe ground mutation remain unavailable/unverified. Preserve fail-closed paths; any BQ acceptance disposition belongs on those BQ requirements, not an implicit waiver here. BQ-141 is a headless measurement tool, not physical proof. |
| BQ-099–BQ-103, BQ-117, BQ-127, BQ-128 | Implemented scoring, history, tone and harness rules do not by themselves prove live pacing, comprehension or corrective visual parity. Retain the runtime/human acceptance in the original criteria. |
| BQ-005–BQ-008, BQ-010, BQ-012, BQ-013, BQ-033–BQ-036 and cross-cutting launch checkpoints | Existing live baseline and guarded projection are useful evidence, not automatic full revalidation of all interruptions, milestones, disclosure and restraint on the current build. Reconcile the original human/live checklists at BQ phase closure; do not mark unknown acceptance complete from this audit. |

This inventory distinguishes clearly missing implementation, explicitly partial acceptance and
capability-dependent proof limits. It does not declare every step in a grouped row wholly absent,
nor presume that every other step passed a human criterion. Source evidence is routed through
[architecture](../architecture.md), runtime gaps through [capabilities](../elin/capabilities.md),
[runtime probes](../elin/verification/runtime-probes.md) and the
[update checklist](../elin/verification/update-smoke-test.md). Final BQ closure must reconcile every
original Done-when against evidence or an explicit owner disposition. BQa adoption itself closes none.

## Every proposed step audited

The first column below uses **attachment IDs only** for traceability. They are not active dependency
references. The final column uses the current adopted numbering. The complete v4 file was
rechecked during follow-up review: attachment steps 011–014 are present, including causal event
identity, consequence feedback and failure semantics. Earlier tool output appeared incomplete,
but the reason for that discrepancy is not established. The original audit's claim that v4 itself
was structurally damaged is withdrawn. The changes below revise existing contracts; they do not
reconstruct sections absent from v4.

| Attachment step | Assessment and substantive correction | Adopted owner |
|---|---|---|
| 001 checks audit | Need exists; preserve deterministic composite authority; classify beats too; separate source/native samples | BQa-003 |
| 002 opposed scaling | Need exists; retain ratio design; add equal-scaling invariance, invalid inputs, rounding/overflow and explicit fumble preservation | BQa-004 |
| 003 feasibility | Mostly existing seam; extend only semantic certainty with impossible/uncertain regressions, no second availability authority | BQa-005 |
| 004 semantic payload | Existing signature integrity is insufficient proof of wording; add payload/omission checks plus human sample review; move after causal loop | BQa-026 |
| 005 discourse grammar | Reuse fragment/compiler/voice machinery, preserve core under repetition, avoid a second speech ontology; move with expression | BQa-027 |
| 006 broad pressure | Add required minimal families, positive/recovery controls, threadless state and bounded work-set intake; not every illustrative pressure requires a new system | BQa-006 |
| 007 local pressure | Add an independent belief-only enumeration route, correction path and no hidden objective completion leak | BQa-007 |
| 008 goal contract | Move behind provenance; require typed evaluator/binding vocabulary, legacy unsupported handling, bounded lifecycle and actor assessment | BQa-008 |
| 009 goal evolution | Earlier month proof depended on a future scheduler; prove owner over time inputs here, integrated autonomous month later; pin character divergence and waiting | BQa-009 |
| 010 action effects | Require descriptors from actual verbs for core chains and bounded accessible bindings; not another action table | BQa-010 |
| 011 semantic bridge | Strengthen the existing desired-state/effect contract; explicitly replace success/event shortcut and substring matching; objective satisfaction cannot inform an ignorant actor | BQa-011 |
| 012 causal identity | Strengthen existing event-instance identity/provenance; retain repeated-transfer/accusation proof and place it first so goal provenance does not need rewriting later | BQa-001 |
| 013 feedback | Extend existing feedback contract to own real native/BQ intake, knowledge notice routes, non-event changes, business spillover and threadless wakeup | BQa-012 |
| 014 failure semantics | Refine the existing failure-semantics contract; scope to beta action families; preserve null failures and distinguish native refusal | BQa-013 |
| 015 running host | Split Core batching/fairness/replay/deduplication and the 30-day proof from Plugin hook integration and live reconciliation; preserve one production runner and no permanent player-engaged freeze | BQa-016, BQa-017 |
| 016 opportunity | Move before host; remove host dependency; allow legitimate coarse channels without inventing exact meetings or witnesses | BQa-014 |
| 017 competition | Move before host; define snapshot selection, stable keyed ties, execution revalidation, expiry and no persisted reservations | BQa-015 |
| 018 organization goals | Require actual institutional receipt/accounting, not ownership or union of private member knowledge; false reports and receipt migration | BQa-018 |
| 019 live organizations | Split risky semantic repair from enrollment/live wiring. Remove wealth fallthrough, prove costs/eligible members/claims, then populate and host legitimate groups | BQa-019, BQa-020 |
| 020 proposal ecology | Explicitly forbid founding theft/shortage manufacture; all producers bind existing causes; add stable identity and minimum family coverage | BQa-021 |
| 021 fulfillment | Preflight is not native atomicity. Restrict to safe commit/verified recovery; no minter rewind; one justified BQ-owned requirement suffices, new physical spawning not mandatory | BQa-022 |
| 022 cross-matter | Shared IDs alone are insufficient; require one real change to affect independent matters without duplicate effects or global resolution | BQa-023 |
| 023 recurrence | Separate exposure from simulation capacity; pressure remains actionable on refusal; bounded retries and materially new episode identity prevent floods and permanent suppression | BQa-024 |
| 024 escalation | Audit existing handlers, not just a new policy; checkpoints revalidate; timers may make overdue conditions but cannot author missing harm | BQa-025 |
| 025 incremental routing | Keep one engine; split communication versus world-action authority; prevent fabricated beat events/endings, double checks and stale acknowledgments | BQa-029 |
| 026 live host | Core intersections and stable voice now precede it; explicitly wire development search, native controls, interruption and delivery failures | BQa-031 |
| 027 intersections | Move Core executable contract before native host, preserving shared verbs/click revalidation | BQa-030 |
| 028 continuation/voice | Split independent persistent voice assignment earlier; continuation owns semantic resume only, with mid-ack reload/recast/content-change coverage | BQa-028, BQa-032 |
| 029 native site | Retain supported/unsupported branch; unsupported adapter evidence need not assert Elin incapability or demand an unsafe experiment | BQa-033 |
| 030 native additions | Scope implementation to one representative supported operation; explicit all-unsupported exit, readback and player-ground safety | BQa-034 |
| 031 embodiment | Coverage matrix over existing owners, not seven new movement features; require visible mechanical consequence but no mandatory new map/teleportation | BQa-035 |
| 032 journal | Keep existing shell; do not transfer BQ-138 acceptance into BQa; knowledge-filtered projections plus regression of native shell | BQa-036 |
| 033 discovery | Existing routes reused; require two materially different verified deliveries, not eleven new adapters; unknown/duplicate/render controls | BQa-037 |
| 034 formal quest | Reuse commission/obligation semantics; reconcile NPC resolution and rewards; native quest-engine work optional, actual contract required | BQa-038 |
| 035 attention feedback | Preserve telemetry independence; define comparison inputs and durable ownership only if existing evidence insufficient | BQa-039 |
| 036 sweep | Late breadth measurement retained; earliest runner belongs to the Core-cycle step before Plugin integration; add real operator/check use, no-cause controls and structural assertions | BQa-040 |
| 037 ecology report | Distinguish declared/attempted/achieved coverage; missing cells inform bounded owner fixes rather than content quotas | BQa-041 |
| 038 stress | Define measurable budgets and event growth rate, not impossible constant history; correction required before passing | BQa-042 |
| 039 causal audit | Reconstruct past decisions from retained provenance, not current recomputed motives; unknown legacy allowed, missing new-chain cause fails | BQa-043 |
| 040 counterfactual/independence | Pin equal-input world projection and RNG isolation; actual delivery/player decisions may differ legitimately; restore expression without retrocausality | BQa-044 |
| 041 beta acceptance | Mandatory entire range and seven traces, not “chosen scope”; require contrasting solutions, quiet play, real embodiment, organizations and ignored-player recurrence | BQa-045 |

No original subject was discarded as redundant. Existing foundations narrowed the new work instead.
The original forty-one subjects remain represented. Organization execution was split from live
enrollment and stable voice from continuation. Follow-up review also separated RNG isolation from
causal provenance and the Core production-cycle runner from Plugin integration. These four splits
make 45 steps without expanding feature scope. Numerical predecessor/successor fields and explicit
dependencies use only the current final IDs.

## Follow-up scope and provenance corrections

The first adopted plan combined causal provenance with RNG isolation. Current
[RNG forks](../../src/BrilliantQuesting.Core/Foundation/DeterministicRng.cs) and separate check/line
labels in [StoryletRouter](../../src/BrilliantQuesting.Core/Storylets/StoryletRouter.cs) already
provide partial separation. BQa-002 now audits and enforces that existing boundary, including
delivery-event allocation and decision keys; it does not mandate another RNG system or new saved
streams. BQa-001 retains causal identity/provenance and the original v4 repeated-transfer and
repeated-accusation acceptance case.

The first adopted host step also combined independently breakable Core and native work. BQa-016
now defines one production runner, including ownership/deduplication, fairness, consumed openings,
migrations and the 30-day headless proof. BQa-017 connects that exact runner to Plugin hooks,
removes overlapping calls and proves live time/zone/reload reconciliation. Deduplication semantics
are established before live integration, not invented by the Plugin.

For mapping from the first adopted plan: its step 001 is now 001–002; its steps 002–014 shift by
one; its step 015 is now 016–017; its steps 016–043 shift by two. The attachment mapping above uses
v4's original subjects, a different numbering baseline. The complete v4 sections were rechecked;
the earlier claim of missing sections is corrected rather than used as a reason for new scope.

## Adversarial and end-product review

The roadmap's [seven-chain acceptance matrix](../living-world-roadmap.md#end-to-end-acceptance-matrix)
is normative. Each trace was walked from authoritative input through local motive, binding,
opportunity/competition, action, consequences and later recognition, then through actual delivery and
intervention. The uncovered bridges are owned in the matrix and step dispositions above.

The second pass specifically checked these failure classes:

| Failure class | Final protection / reason no further standalone step is needed |
|---|---|
| Pressure/Director/goals/consequences/UI become duplicate authorities | BQa-006–BQa-012 retain source owners; BQa-016 coordinates only, with BQa-017 supplying native hooks; BQa-021–BQa-025 separate recognition/establishment/capacity/exposure; BQa-036–BQa-039 project known state |
| Omniscience, false-belief collapse, fabricated off-screen witnesses | BQa-007/BQa-011 distinguish local belief and objective state; BQa-014 grades opportunity; BQa-018 owns receipt; BQa-037 acknowledges learning |
| String matching, unrelated fallback, success equals satisfaction | BQa-008–BQa-011 establish typed registered matching; BQa-019 removes organization fallthrough; unsupported remains explicit |
| Recursive feedback, iteration-order winners, duplicate spending and actor starvation | BQa-015 batch arbitration and expiry; BQa-016 boundaries/fairness/idempotence and BQa-017 hook deduplication; BQa-042 adversarial runs |
| Unlimited or permanently suppressed matters, reload rerolls | BQa-024 stable cause/episode identity, bounded retries/capacity and actionable source state on refusal |
| Cold simulation duplicates native work, player engagement freezes the world | BQa-014/BQa-017/BQa-035 preserve D021, native reconciliation and bounded interaction deferral |
| Dialogue says less/different meaning, cursor becomes authority, arbitrary beat events invent harm | BQa-026 payload proof, BQa-029 acknowledgment/shared action handoff, BQa-032 semantic rediscovery |
| New goals/voice/references lack migration, claims leak into history | Introducing-owner persistence table; BQa-001/BQa-002/BQa-008/BQa-016/BQa-018/BQa-028/BQa-029 contracts and transient reservation prohibition |
| Native refusal blocks the whole beta or plans are called physical | BQa-033–BQa-035 explicit operation-level evidence exits plus mandatory visible supported consequences |
| Repetitive crises, indistinguishable actors, exposition, noise without value | Positive/quiet controls in BQa-006/BQa-009, actual cross-matter changes in BQa-023, measured diversity/retention in BQa-040–BQa-044, human restraint/comprehension gate in BQa-045 |

Two contradictions were removed during review: admission may not gate the *causes* needed to keep
the world evolving, and failed native execution cannot be called atomic merely because its inputs
were validated. Another important distinction is that actual communication can be causal without
optional storylet rendering being the scheduler of NPC communication.

The smallest convincing beta needs no broad settlements, commodity economy, extra mythology or
universal UI. It does need economic spillover, honest institutional agency, false-belief action,
threadless autonomy, shared player mechanics, visible consequences and understandable long-term
history. Those are mandatory contracts, not optional examples. The final roadmap is sufficient as a
specification if every acceptance condition is met. Native feasibility, performance, tuning and
human comprehension remain empirical risks with explicit gates, not missing planned subsystems.

## Adoption and validation contract

The BQ roadmap keeps launch/hardening authority; the new roadmap owns only the post-BQ order.
AGENTS, documentation index, architecture, flow, extension routing and maintenance ownership point
to that distinction. The doc checker's default entry list includes the roadmap and this audit;
its existing parsing behavior is unchanged. Structural verification checks unique contiguous 001–045
headings, one predecessor/successor, valid backward dependencies, phase ranges and final acceptance.

The roadmap is ready as an implementation specification, but **BQa-001 is not authorized to start by
this audit**. First close or explicitly disposition the unfinished canonical BQ phase. Once that gate
and implementation authorization are satisfied, begin BQa-001's causal identity/provenance foundation,
not the attachment's formerly first check-scaling audit.
