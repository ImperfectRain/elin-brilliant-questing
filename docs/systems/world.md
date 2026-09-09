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
`SituationProposalSelection.Rank` orders by existing quality then unique ordinal proposal key,
rejecting duplicate keys. Construction, ranking and inspector-only `Explain` have no world access,
commit callbacks, RNG or saved state. BQ-103 conservation costs are not implemented here.

Settlement plans expose proposals over their admitted candidates. Their evaluation-local keys
preserve the owner's existing deterministic tie order; they are not persistent identities.
Selection hands back the original proposal to `TryGenerateSelected`, which accepts only a proposal
from that plan, refuses hypothetical actor creation, rechecks attention and performs the existing
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
