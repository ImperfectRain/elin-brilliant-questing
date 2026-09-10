# World integration and simulation

[Router](../architecture.md) · [World validation](../agent/validation.md#world)
· [maintenance](../agent/documentation.md). Native facts remain in the [evidence map](../elin/capabilities.md).
Read [D021](../agent/decisions.md#d021--vanilla-owns-embodiment-bq-owns-narrative-meaning) before movement,
scheduling, needs or occupation work. Core simulations below do not establish live runtime success.

## Activity

**Owns:** `ActorActivity` is a transient eight-facet observation of vanilla presence, zone, timetable,
span, activity, global eligibility/activity and transition. Inputs: existing native state through
`ElinActorActivity`; outputs: typed readings for actor contexts, autonomy, travel and diagnostics.
No snapshot is saved. **Does not own:** routines, work/needs execution, pathfinding or a BQ travel
claim. Unknown is neither idle nor moving. Native behavior is source-observed; facet populations
remain runtime questions. Do not call goal-constructing APIs just to observe activity.

Source: [ActorActivity](../../src/BrilliantQuesting.Core/Integration/ActorActivity.cs),
[ElinActorActivity](../../src/BrilliantQuesting.Plugin/ElinActorActivity.cs).
Proof: [ActorActivityTests](../../tests/BrilliantQuesting.Core.Tests/ActorActivityTests.cs).
Lab: [actor-action](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/ActorActionScenario.cs);
Plugin attach diagnostics for native observations. [Evidence](../elin/api/time-ai-and-global-goals.md).

## Generation

**Owns:** `SettlementSituationGenerator.Evaluate` reads local affordances and scores theft
candidates; `TryGenerate` checks admission/repetition and commits the selected situation. Other
archetypes have their own establishment/recovery paths; their existence does not mean automatic
settlement selection supports them all. Inputs: actors, inventory, stakes, local state and attention.
Outputs: native mutation plus BQ facts/threads/history, with generation causes and fingerprints.
Plans are transient; established state is saved. **Does not own:** spawning trouble because a quest
is needed, inventing native evidence, or filling every archetype from a universal planner.
Consumers: threads, action bindings, discovery, autonomy, sites.

`SituationCandidate.ActorRequirements` describes both existing actor bindings and hypothetical
new actors. New actors use proposal-local keys (shared across roles when appropriate), never
reserved world IDs; these are requirements, not existence or feasibility claims. Actor requirements
are immutable and detached from the builder. `SituationProposal` names a candidate;
`SituationProposalSelection.Rank` orders by quality minus explicit creation costs, then unique ordinal proposal key,
rejecting duplicate keys. Construction, ranking and inspector-only `Explain` have no world access,
commit callbacks, RNG or saved state.

BQ-103 charges 4 per distinct new actor key and 6 per distinct `NewWeirdPremises` key, adopting
CD §33.6's initial weights. These are significant additions explicitly requested by a proposal;
reusing actors/facts/locations has no creation charge, regardless of player familiarity. Actor keys
shared across roles count once. Premises are declared by `RequireNewWeirdPremise`, never inferred
from English, archetype ids, setting references or existing truth. Premise keys are sorted, deduplicated
and detached from the builder. The score uses wide arithmetic and is explained alongside raw quality,
counts, costs and requirements by `SituationProposal.Explain`. Sufficient quality can outweigh cost;
cost does not change eligibility or fabricate a causal reason. Other hypothetical resource types
remain outside this bounded policy; it adds no factories, saved budget or exposure penalty.

Settlement plans expose proposals over their admitted candidates. Their evaluation-local keys
preserve the owner's existing deterministic tie order; they are not persistent identities.
Selection hands back the original proposal to `TryGenerateSelected`, which accepts only a proposal
from that plan, refuses hypothetical actor/premise creation, rechecks attention and performs the existing
native transfer before establishing facts/history. Ranking a hypothetical alternative is supported;
fulfilling its actor requirement is not added to the settlement generator. Explicit scenario staging
remains a separate owner. There is no automatic new-actor fallback or universal creation factory.

Source: [SettlementSituationGenerator](../../src/BrilliantQuesting.Core/Situations/SettlementSituationGenerator.cs),
[SituationProposal](../../src/BrilliantQuesting.Core/Situations/SituationProposal.cs),
[SituationCandidate](../../src/BrilliantQuesting.Core/Situations/SituationCandidate.cs),
[LocalAffordanceProfile](../../src/BrilliantQuesting.Core/Situations/LocalAffordanceProfile.cs),
[FailedCaravanSituation](../../src/BrilliantQuesting.Core/Situations/FailedCaravanSituation.cs).
Proof: [SettlementSituationGeneratorTests](../../tests/BrilliantQuesting.Core.Tests/SettlementSituationGeneratorTests.cs),
[SituationProposalTests](../../tests/BrilliantQuesting.Core.Tests/SituationProposalTests.cs),
[FailedCaravanTests](../../tests/BrilliantQuesting.Core.Tests/FailedCaravanTests.cs).
Lab: [integration](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/IntegrationScenario.cs),
[failed-caravan](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/FailedCaravanScenario.cs).
Native: identity/settlement eligibility and inventory limits in [extension seams](extension-seams.md).

## Autonomy

**Simulation fidelity (BQ-107):** `NarrativeNpc.BackgroundTier` derives Warm from known importance
or an open positive-weight goal, Cold from other living actors, and Archived from death or alias
retirement. `OffScreenSchemes.TierOf` overlays Active only from observed active-zone presence.
Active actors remain available to local narrative systems but do not run off-screen schemes.
Vanilla retains work, hobby, needs and movement ownership in every tier.

`EntityRegistry` maintains derived rotating Warm/Cold work queues when NPC state or goals change.
Each scheme pass takes at most 12 Warm and 2 Cold candidates by default, independently of historical
actor count. Warm openings use seven days; Cold inspections use thirty. Only the bounded selected
set consumes its elapsed window, including selected openings skipped by the attempt cap; unselected
actors retain their clocks. Queue position is transient and rebuilt on load; existing saved
`LastSimulatedAt` prevents replay of consumed openings. This bounds scheme scheduling, not every
other subsystem's work. It does not invent goals for Cold actors or grant player knowledge.

On Plugin attach and completed `Zone.OnVisit` (with action-driven zone change as fallback),
canonical local-actor intake runs before
`OffScreenSchemes.ReconcileZone`, and reconciliation precedes daily scheduling. It reads fresh `HomeState` and
advances the clock of observed active residents monotonically. It never invokes `Zone.Simulate`
or extrapolates Home production. Unknown Home/presence cannot prove a resident was caught up.
The September capture exposed a missing Home zone identity; the correction follows
`FactionBranch.owner` and preserves the full membership roll. See [Home evidence](../elin/api/home-and-settlements.md#september-2026-identity-and-membership-correction).
Corrected attach clocks are live-observed. Completed-visit hook execution and controlled resource
deltas still need live verification; see the [post-visit correction](../elin/api/home-and-settlements.md#post-visit-reconciliation-correction).

Source: [tier index](../../src/BrilliantQuesting.Core/World/SimulationTier.cs).
Proof: [SimulationTierTests](../../tests/BrilliantQuesting.Core.Tests/SimulationTierTests.cs):
20,012 records, 1,000 scheduler ticks within 2 seconds excluding setup, at most 14 inspections per
tick; tier mutations, rotating Cold selection, reload and unchanged Home readback.

**Owns:** bounded off-screen selection and passes: `AutonomousInterventions` handles ignored matters;
`OffScreenSchemes` pursues staged schemes; `AdventurerEcology` handles rival involvement. Inputs:
known matters, stakes/goals, personal limits, activity and shared action offers. Outputs: `ActionIntent`
through `ActionAttempt.Run`, deeds/endings and discoverable claims. Choices/traces are transient;
meaningful outcomes and relevant scheduling state survive through owned world records/history.
**Does not own:** another check resolver, embodiment, or free player knowledge. Interventions protect
matters the player has already acted in. Host calls advance; these classes are not independent clocks.

Source: [AutonomousInterventions](../../src/BrilliantQuesting.Core/Autonomy/AutonomousInterventions.cs),
[OffScreenSchemes](../../src/BrilliantQuesting.Core/Autonomy/OffScreenSchemes.cs),
[AdventurerEcology](../../src/BrilliantQuesting.Core/Autonomy/AdventurerEcology.cs).
Proof: [AutonomousInterventionTests](../../tests/BrilliantQuesting.Core.Tests/AutonomousInterventionTests.cs).
Lab: [autonomy](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/AutonomyScenario.cs),
[off-screen-schemes](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/OffScreenSchemesScenario.cs),
[adventurer-ecology](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/AdventurerEcologyScenario.cs).
Native: [activity and mutation evidence](../elin/capabilities.md).

## Travel

**Owns:** persistent semantic journeys, member/cargo IDs, milestone state and movement ownership.
`TravelingGroup`/`TravelingGroupLedger` do not copy members or items. Inputs: origin/destination,
purpose, time, physical-plan declaration and observed movement. Outputs: departures, arrivals,
interruptions/failure, optionally one gated relocation. **Does not own:** tiles, routes/pathfinding
or a second vanilla global journey. Semantic arrival does not establish physical arrival. Absence
and vanilla movement must not race BQ relocation. Consumers: failed-caravan situations, causal
contents, discovery and inspector. Live relocation remains restricted/unverified beyond source/stubs.

Source: [TravelingGroup](../../src/BrilliantQuesting.Core/World/TravelingGroup.cs),
[AbsenceLifecycle](../../src/BrilliantQuesting.Core/World/AbsenceLifecycle.cs).
Proof: [TravelingGroupTests](../../tests/BrilliantQuesting.Core.Tests/TravelingGroupTests.cs).
Lab: [traveling-groups](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/TravelingGroupsScenario.cs).
Native: [world/zone evidence](../elin/api/world-and-zones.md).

## Organizations

**Owns:** BQ-generated `Organization` records, goals, memberships, holdings, wealth and last-action
time; `OrganizationActivity.Advance` updates them deterministically and records deeds. Inputs:
generated organization state/goals and time. Outputs: membership, reserves, protection/raids and
events for ties, sites and situations. Records are persisted; pass machinery is transient.
**Does not own:** vanilla guild membership/rank/economy. `GuildNetworks`/authority actions consume
native or explicitly authored standing through their own routes. The Lab integration harness calls
organization activity; the Plugin does not currently instantiate that pass.

Source: [OrganizationActivity](../../src/BrilliantQuesting.Core/World/OrganizationActivity.cs),
[Organization](../../src/BrilliantQuesting.Core/World/Organization.cs).
Proof: [OrganizationActivityTests](../../tests/BrilliantQuesting.Core.Tests/OrganizationActivityTests.cs).
Lab: [integration](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/IntegrationScenario.cs).
Native guild limits: [capabilities](../elin/capabilities.md).

## Sites

| Owner | Inputs → outputs; storage | Boundary |
|---|---|---|
| `SiteReuse`, `SiteGenesis` | Existing sites and a semantic plan → reused/established site, actors/cargo/evidence; established manifests saved | Reuse before genesis; no regeneration on return; an empty native handle refuses registration |
| `SiteGrammar`, `ScenarioPlan`, `SiteCandidates`, `SiteRoutes` | Grammar, purpose, action capabilities → scored topology/route claims; candidate work transient | Plans declare meaning before geometry; unsupported/unanswered required routes refuse |
| `SiteContents` | Matter, organization, travel and provenance → causal occupants/items/anchors | No filler independent of state; reuse existing actors instead of cloning them |
| `SiteRealization`, `ScenarioDungeon` | Validated plan and authored pieces → structure and checks; established structure saved | Core geometry is headless proof, not evidence that an Elin zone was created |
| `SiteMutation` | Owned established site, addition ID, piece and ground inspection → one additive footprint/handle saved | Never overwrite unknown or player-changed ground; additions are idempotent and never regenerate the site |

Consumers: spatial action projection, evidence investigation, history, generation and diagnostics.
**Does not own:** Elin map truth, native pathfinding or a general settlement simulator. On the live
adapter, loose place reads, structured creation and additions are explicitly unsupported; unstructured
`StageSite` binds the loaded zone rather than creating one. [Canonical evidence](../elin/verification/unresolved.md)
ELIN-Q-0008/0032/0033. Planning doctrine: [procedural places](../design/procedural-places-and-spatial-history.md).

Source: [SiteGenesis](../../src/BrilliantQuesting.Core/World/SiteGenesis.cs),
[ScenarioPlan](../../src/BrilliantQuesting.Core/World/ScenarioPlan.cs),
[SiteRealization](../../src/BrilliantQuesting.Core/World/SiteRealization.cs),
[SiteMutation](../../src/BrilliantQuesting.Core/World/SiteMutation.cs).
Proof: [SiteGenesisTests](../../tests/BrilliantQuesting.Core.Tests/SiteGenesisTests.cs),
[ScenarioPlanTests](../../tests/BrilliantQuesting.Core.Tests/ScenarioPlanTests.cs),
[ScenarioDungeonTests](../../tests/BrilliantQuesting.Core.Tests/ScenarioDungeonTests.cs),
[SiteMutationTests](../../tests/BrilliantQuesting.Core.Tests/SiteMutationTests.cs).
Lab: [dungeon](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/DungeonScenario.cs),
[site-addition](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/SiteAdditionScenario.cs).

Spatial diagnostics: [SpatialRangeHarness](../../tools/BrilliantQuesting.Lab/SpatialRangeHarness.cs)
reads selected `ScenarioPlan` artifacts across shipped grammars and consecutive seeds, using the
existing theft fixture with an explicitly staged three-person crew and an evidence-cache objective.
It reuses the anti-template histogram/repetition metric; refused plans are reported separately.
Exact directed graph canonicalization ignores nouns, IDs and declaration order while preserving
objective/outside roles and, for experiential topology, requirements, affordances, verbs, admission
and support. Search-budget exhaustion is missing coverage, never a guessed equivalence.
Other axes report directed cycle count, shortest promised objective depth, route mechanics and
alternatives, evidence/occupancy depth distributions (including unplaced people), and causal evidence
linkage/reachability as a limited history-readability proxy. These are selected-plan measurements,
not native geometry, played encounters or human comprehension; no world state is written by measurement.
[SpatialRangeHarnessTests](../../tests/BrilliantQuesting.Lab.Tests/SpatialRangeHarnessTests.cs)
proves renamed/reordered plans repeat, structural/mechanical differences remain distinguishable,
read-only planning/measurement and deterministic batch replay.
Lab: [spatial-range](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/SpatialRangeScenario.cs); CI retains its JSON profile.
