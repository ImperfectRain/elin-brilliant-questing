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
claim, nor what the snapshot means for one attempt - that is
[opportunity](actions.md#opportunity)'s, and the activity and routine weights it and
`InterventionOpportunity` share are read through `ActionOpportunity`'s own facet readers so that
"they are asleep" is one answer rather than two (BQa-014). Unknown is neither idle nor moving.
Native behavior is source-observed; facet populations remain runtime questions. Do not call
goal-constructing APIs just to observe activity.

Source: [ActorActivity](../../src/BrilliantQuesting.Core/Integration/ActorActivity.cs),
[ElinActorActivity](../../src/BrilliantQuesting.Plugin/ElinActorActivity.cs).
Proof: [ActorActivityTests](../../tests/BrilliantQuesting.Core.Tests/ActorActivityTests.cs),
[ActionOpportunityTests](../../tests/BrilliantQuesting.Core.Tests/ActionOpportunityTests.cs).
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

**Goal/action bridge (BQa-011).** A want that names a machine-readable condition is answered by
[`GoalRoutes`](actions.md#actions) and by nothing else: `OffScreenSchemes` no longer matches substrings
of `NpcGoal.Kind` for such a want, and does not fall through to that matching when the bridge offers
nothing, because a want that said no verb here could serve it must not get an unrelated one. The
name-matching remains only for wants with no condition - old saves and hand-established fixtures - and
so does the pre-BQa-011 close on a successful attempt, since there is nothing better to ask about them.
For covered wants the close is the condition read against authoritative state, and it retires the goal
only when the actor's own successful attempt is how they could know; a condition another actor made true
leaves the want open for `ActorGoalEvolution` to retire. `OffScreenSchemeTrace` carries each search, the
condition reading and which of those two closes applied. The pass selects active wants rather than
merely unsatisfied ones, so a want BQa-009 abandoned or superseded is no longer pursued.

**Production cycle (BQa-016).** `ProductionCycle` is the one bounded causal pass and the only
authority over where a pass starts and ends, how much work it may do, whose turn it is and which
indivisible openings are already gone. It decides nothing else: supplied observations go to
`VanillaActionRecorder`, the work set to `PressureFeedback`, conditions to `DevelopmentDetector`,
actor-local readings to `ActorPressureView`, wants to `ActorGoalEvolution`, routes to `GoalRoutes`,
and every attempt runs inside one `ArbitrationBatch` and nowhere else, so a contested opening is
settled once against the world as it is at that moment.

The batch boundary is one day, recorded in `ProductionCycleLedger.LastConsumedDay`, so a host that
fires the same interval twice gets one pass and a reload onto the same morning does not re-run it.
Re-entry is refused outright: an immediate listener reacting to what a pass did cannot start
another pass on the same stack, and its changes wait for the next interval. Per-pass work is bounded
by `ProductionCycleBudget` counts rather than by wall clock, and arbitration runs each gathered
intention at most once, so bounding the batch's input bounds the pass. Whose turn it is comes off the
persisted `NarrativeNpc.LastSimulatedAt` rather than off the rotation's position, which a load
rebuilds from the front; the index supplies a bounded sample and the turn marker chooses inside it.
Actors a change named are read first, everyone else in least-recently-considered order, and an actor
who was read has had their turn whether or not anything came of it.

An indivisible opening a committed attempt or a supplied native outcome closed is recorded in the
same ledger, which `OffScreenSchemes` and `AutonomousInterventions` also consult, so one purse
cannot be lifted once by each pass. Those markers are closures rather than reservations - a thing
nobody has taken yet is never written - and remembering them is capped, with oldest-first eviction
as the documented cost of a bounded save.

The live host is [LiveWorldCycle](../../src/BrilliantQuesting.Plugin/LiveWorldCycle.cs) (BQa-017),
which supplies hooks and absorbs failures and owns nothing the runner owns: no batching, fairness,
turn order, goal selection or interval cursor of its own. Reconciliation precedes the pass on every
path that reaches one; an act the observer already recorded closes its opening through
`ProductionCycle.Close` rather than being handed back to `Run` and recorded twice; and a pass that
throws is absorbed with its interval consumed, because nothing a pass committed unwinds and a retry
would repeat the finished half. Evidence remains headless/source: no Elin hook, callback cadence or
vanilla catch-up ordering has been observed in a running game.

**Owns:** bounded off-screen selection and passes: `ProductionCycle` runs the shared causal pass;
`AutonomousInterventions` handles ignored matters; `OffScreenSchemes` pursues staged schemes;
`AdventurerEcology` handles rival involvement. Inputs:
known matters, stakes/goals, personal limits, activity and shared action offers. Outputs: `ActionIntent`
through `ActionAttempt.Run`, deeds/endings and discoverable claims. Choices/traces are transient;
meaningful outcomes and relevant scheduling state survive through owned world records/history.
**Does not own:** another check resolver, embodiment, or free player knowledge. An active, bounded
player interaction temporarily defers a conflicting attempt (`AutonomousInterventions.PlayerInteractionDays`);
having once acted in a matter does not, and a matter the player walked away from returns to the world.
Host calls advance; these classes are not independent clocks.

Source: [ProductionCycle](../../src/BrilliantQuesting.Core/Autonomy/ProductionCycle.cs),
[AutonomousInterventions](../../src/BrilliantQuesting.Core/Autonomy/AutonomousInterventions.cs),
[OffScreenSchemes](../../src/BrilliantQuesting.Core/Autonomy/OffScreenSchemes.cs),
[AdventurerEcology](../../src/BrilliantQuesting.Core/Autonomy/AdventurerEcology.cs),
[cycle markers](../../src/BrilliantQuesting.Core/World/ProductionCycleLedger.cs).
Proof: [ProductionCycleTests](../../tests/BrilliantQuesting.Core.Tests/ProductionCycleTests.cs),
[LiveWorldCycleTests](../../tests/BrilliantQuesting.Core.Tests/LiveWorldCycleTests.cs),
[AutonomousInterventionTests](../../tests/BrilliantQuesting.Core.Tests/AutonomousInterventionTests.cs).
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
time; `OrganizationActivity.Advance` runs one bounded pass over them and records deeds. Inputs:
generated organization state/goals, the action library and the build. Outputs: membership,
reserves, protection/raids, delegated attempts and events for ties, sites and situations. Records
are persisted; pass machinery is transient.
**Does not own:** vanilla guild membership/rank/economy. `GuildNetworks`/authority actions consume
native or explicitly authored standing through their own routes. The Lab integration harness calls
organization activity; the Plugin does not currently instantiate that pass.

**What a body knows, and what follows from it.** `InstitutionalReceiptLedger` on each organization is
the whole of what the institution has been told: one saved receipt per filing, carrying the channel
(`Accounting`, `MemberReport`, `HoldingObservation`, `ExternalReport`), whoever filed it, the claim,
and a standing that can be corrected or retracted without the filing leaving the record.
`InstitutionalReports` is the only way in, and where a person files it reads their confidence and
provability off their own knowledge record rather than minting a second belief. Members' private
beliefs are theirs: the union of them is never the body's knowledge.
`OrganizationPressureView.Of` is the derived reading of that - a standing receipt, an open condition
of a site the body keeps, or an undertaking it is party to on the ledger, and nothing else; a body's
own stated aim is deliberately not a route. `OrganizationGoalEvolution.Advance` is the goal owner:
it forms, reweights, supersedes and retires `OrganizationGoal`s from those readings alone, choosing
among the ends a reading admits by `OrganizationPolicy`'s per-type charter, and asks the world
whether a want came about only once its cause has left the body's own view. `OrganizationGoal`
carries the same condition/provenance/lifecycle contract an `NpcGoal` does; `Progress` remains the
body's effort counter and is not evidence about the condition. Reading is side effect free; only the
evolution pass records a change. Nothing there schedules or acts.

**What a body does about it, and what it does when it can do nothing.**
`OrganizationOperations.Plan` is the reading that turns one end into an operation, and every
operation declares what kind of change it could make - in `SemanticEffects`' own vocabulary, never
a second one - what it needs and what it costs. Two means, and the line between them is whether the
change needs hands. *Bookkeeping* writes the body's own records - the roll, the reserve band, the
watch on a yard it keeps, a blow at a rival - has no NPC performing it and names the body on the
event. *Delegated* is a deed, carried by one eligible real member through the shared `ActionAttempt`
that anybody else would use, routed by `GoalRoutes.DiscoverFor` from the end's own BQa-008 condition
and settled in `ArbitrationBatch` beside every other intention. The body sends whoever filed the
receipt the end came off, because that is the person who has seen the thing, and the member is then
routed through their own knowledge rather than the body's - being directed is not being told. Every
way of going about one end is offered together: they share a contest, so BQa-015 ranks them,
revalidates the best against the world as it is, and the rest yield once one of them finishes it.
A holding is a site the body keeps whose own record does not name another controller, and reserves
come from one or not at all. There are no organization verbs, no organization checks and no
synthetic member: an end nothing supports is a refusal on the pass and the body waits. What closes
an end is the world's answer to the body's own committed operation; `Progress` is effort and is not
evidence, and an end whose condition quietly came true stays open for `OrganizationGoalEvolution`.
One person is spent once per pass whichever body asks for them, reserves are read as the last
operation left them, and BQa-016's spent openings stop a reload repeating a deed.

Source: [OrganizationActivity](../../src/BrilliantQuesting.Core/World/OrganizationActivity.cs),
[OrganizationOperations](../../src/BrilliantQuesting.Core/World/OrganizationOperations.cs),
[Organization](../../src/BrilliantQuesting.Core/World/Organization.cs),
[InstitutionalReceipt](../../src/BrilliantQuesting.Core/World/InstitutionalReceipt.cs),
[OrganizationPressureView](../../src/BrilliantQuesting.Core/Developments/OrganizationPressureView.cs),
[OrganizationGoalEvolution](../../src/BrilliantQuesting.Core/World/OrganizationGoalEvolution.cs).
Proof: [OrganizationActivityTests](../../tests/BrilliantQuesting.Core.Tests/OrganizationActivityTests.cs),
[OrganizationPressureInterpretationTests](../../tests/BrilliantQuesting.Core.Tests/OrganizationPressureInterpretationTests.cs),
[OrganizationOperationTests](../../tests/BrilliantQuesting.Core.Tests/OrganizationOperationTests.cs).
Lab: [integration](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/IntegrationScenario.cs).
Native guild limits: [capabilities](../elin/capabilities.md).
The durable rules are [`D094`](../agent/decisions.md#d094--a-body-knows-what-it-was-told-through-a-named-channel-and-never-the-union-of-what-its-people-believe)
and [`D095`](../agent/decisions.md#d095--a-body-acts-through-its-own-people-or-on-its-own-records-and-never-gets-paid-for-failing).

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
