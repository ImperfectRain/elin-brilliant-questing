# Actions and causal progression

[Router](../architecture.md) · [Action validation](../agent/validation.md#actions)
· [maintenance](../agent/documentation.md). These are headless contracts; native dependencies are
per verb, not a blanket guarantee that every registered action works in Elin.

## Actions

**Owns:** the shared verb vocabulary (`NarrativeAction`, `ActionRegistry`) and execution handoff
(`ActionIntent`, `ActionAttempt`, `ActionContext`, `ActionBinding`). Inputs: actor/target, semantic
purpose, fact/item/thread and adapter/resolver. Outputs: availability or `ActionOutcome`, checks,
recorded events and native writes. Registry/context/outcomes are transient; resulting history and
owned state are saved. **Does not own:** NPC motive selection, native combat/crafting resolution or
a separate NPC verb library. `ActorScope`, `Embodiment`, `SettlesMatters`, `Effects`, `Postconditions`
and `Reach` are declarations on the verb; consumers must not keep parallel lists. Player projection and NPC attempts ask the same verbs.

**Semantic effects (BQa-010).** `NarrativeAction.Effects` is the fourth declaration: which kinds of
state change this verb could potentially advance, from the registered `SemanticEffects` vocabulary,
and which `SemanticSlots` any one of which would point it at something. `ActionRegistry.Advancing`
answers "which verbs could do this kind of thing" with no context at all, and its build-gated
overload drops an effect vanilla would have to carry on a build that cannot; `EffectCoverage` names
the verbs that have declared nothing and the vocabulary keys no verb answers, because to a consumer
that only asks what matches, those read the same as "nothing can be done". `GoalConditionRegistry`
terms name effect kinds rather than verbs, so registering a verb with existing vocabulary joins every
want that vocabulary answers with no central edit. It is potential capability, not outcome
prediction: inspection mutates nothing, `GetAvailability` still decides whether the attempt makes
sense here, the check and the seam still decide what happens, and failure may produce a different
effect than success. The binding requirement `ContextualActionProjection` and the verbs themselves
read now comes from this declaration rather than from a switch on verb ids. See
[D086](../agent/decisions.md#d086--a-verb-declares-what-kind-of-change-it-could-make-nothing-keeps-a-list-of-which-verbs-answer-which-wants).

**Goal/action bridge (BQa-011).** `GoalRoutes.Discover` answers "what could this person do about what
they want": the want's `GoalCondition` term names the effect kinds that would move it, `ActionRegistry`
names the verbs that could make those changes on this build, and the term's own `GoalConditionSlot`
roles say which of its bindings is an object, a claim, an undertaking, a place or a person, so a verb is
pointed at something without anything switching on the term's name. Nothing holds a list of which verbs
answer which wants. The parties a route may be aimed at come from the condition's bindings, from the
records those bindings name and from claims the actor themself holds - never from a sweep of the world,
so an actor with no route to who took their property is offered none. The search is a read: it rolls,
records and mutates nothing, ranking stays with the caller's existing goal weight, `InterventionStyles`
and opportunity authorities, and `GetAvailability` plus `ActionAttempt.Run` still decide everything they
decided before. `GoalRouteSearch.Unsupported` and its notes separate "this want cannot be read" from
"this want has nothing available", because to a consumer that only asks what matched, those read alike.
Satisfaction is two answers: whether the condition holds is authoritative state's, and whether its owner
may believe it is answered only by their own successful attempt, so another actor's deed can make a want
objectively true without closing it. `lie` declares `information.denied` rather than
`information.disclosed`: it moves one belief downward, and a want that somebody be told something must
not be offered it. `GoalRoutes.DiscoverFor` is the same search for a desired condition that is not this
person's own want - an institutional end, where a body directs one of its members (BQa-019) - and it is
the same search deliberately: the routes offered are the ones *that member* could take, read through
what that member knows, so directing somebody is not a way of handing them the save's knowledge. See
[D087](../agent/decisions.md#d087--what-the-world-holds-and-what-a-wants-owner-may-believe-are-two-answers-and-a-route-is-found-through-the-vocabulary-rather-than-through-a-name).

**Postconditions (BQa-013).** `NarrativeAction.Postconditions` is the fifth declaration, and it
refines the fourth rather than repeating it: `Effects` is capability, read before anybody attempts
anything; this is what each of the three endings actually leaves behind, read afterwards. Three,
because a failure and a refusal both end with the actor holding nothing and are not the same event -
a failure was performed and has fallout, a refusal never took place. `ActionOutcome.Resolution`
carries the answer, `ActionOutcome.Changed` records what the attempt actually moved at the line
after the write, and `Perform` demotes a success claim with none of its declared changes behind it
to a refusal, so an unsupported native write cannot be narrated as a deed. A refusal records no
change and no event; `ActionPostconditionAudit` reports anything an outcome says that its verb's
declaration does not support, including witnesses named where nobody's presence was read.
Failure is classified from `FailureOutcomes` - nothing changed, information revealed, cost paid,
harm done, later options transformed - and never required to cost something: a lie nobody believed
leaves the world byte-identical. `ActionRegistry.PostconditionCoverage` reports the verbs that have
said nothing, and separately the verbs whose effects are undeclared, whose success is still BQa-010's
open question. See
[D089](../agent/decisions.md#d089--a-verb-says-what-each-of-its-endings-leaves-behind-and-the-library-holds-every-attempt-to-it).

Source: [NarrativeAction](../../src/BrilliantQuesting.Core/Actions/NarrativeAction.cs),
[ActionRegistry](../../src/BrilliantQuesting.Core/Actions/ActionRegistry.cs),
[ActionEffects](../../src/BrilliantQuesting.Core/Actions/ActionEffects.cs),
[ActionPostconditions](../../src/BrilliantQuesting.Core/Actions/ActionPostconditions.cs),
[ActionOutcome](../../src/BrilliantQuesting.Core/Actions/ActionOutcome.cs),
[GoalRoutes](../../src/BrilliantQuesting.Core/Actions/GoalRoutes.cs),
[ActionAttempt](../../src/BrilliantQuesting.Core/Actions/ActionAttempt.cs).
Proof: [ActionBindingTests](../../tests/BrilliantQuesting.Core.Tests/ActionBindingTests.cs),
[ActionEffectContractTests](../../tests/BrilliantQuesting.Core.Tests/ActionEffectContractTests.cs),
[ActionPostconditionContractTests](../../tests/BrilliantQuesting.Core.Tests/ActionPostconditionContractTests.cs),
[GoalActionBridgeTests](../../tests/BrilliantQuesting.Core.Tests/GoalActionBridgeTests.cs),
[PlayerNpcActionSymmetryTests](../../tests/BrilliantQuesting.Core.Tests/PlayerNpcActionSymmetryTests.cs).
Lab: [actor-action](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/ActorActionScenario.cs).
Native: [capability routing](../elin/capabilities.md#capability-routing).

## Opportunity

**Owns:** the side-effect-free reading of what the world around one attempt allowed and how
plausible it made it, and the verb's declaration of how it reaches whoever it is aimed at. Inputs:
the BQ-135 activity snapshot, the context's own evidence mode, zone, witness list, named object and
matter clock. Outputs: a refusal quoting the observation behind it, a 0..1 plausibility, and one
named term per facet. Nothing is saved and nothing is rolled. **Does not own:** the verb's own
rules, the check, the consequence, or whether the object is one this verb can use - that stays with
[availability](#availability). `ActorContexts` still builds the context; this reads it.

**Two evidence modes, kept apart (BQa-014).** `ActionOpportunity.Read` upgrades the loose activity
weighting `InterventionOpportunity` did into a reading about an actual attempt, and the thing it
adds is the line between what was seen and what is merely recorded. An **observed local** reading
is taken in a zone the game is running: co-location is verified, the room's contents are the
witness list, privacy is an observation. A **coarse off-screen** reading has only the save's own
state - which zone the game keeps somebody in, what vanilla has them doing, what is in whose pack -
and none of it becomes a meeting, a witness, a position or a moment. Two people the save keeps in
one town is a chance to have crossed paths and never proof that they did, so
`ActionOpportunity.VerifiedCoLocation` is false for every coarse reading and the witness facet
reads unread rather than zero. An unread facet and a harmless one are both worth 1.0 and only
`OpportunityTerm.Known` separates them (`D017`).

The ten facets an attempt can care about are the `OpportunityFacet` vocabulary. Four of them can
refuse, and each refusal quotes the observation that produced it: somebody Elin is already carrying
between zones, either party (`VS 3.3`, `D021`); a coarse attempt whose parties the save keeps apart
or cannot place at all; a party the game does not answer for. The rest are named weights, including
the named zeros. Object accessibility is deliberately a weight: whether a verb can proceed without
its object is the verb's own question and it already answers it.

**Reach (BQa-014).** `NarrativeAction.Reach` is the sixth declaration and the smallest: one axis,
because it answers the one question the other five cannot. `ActionReach.Present` is the default and
is a claim rather than a gap - almost the whole library is hands, faces and objects - so unknown
exact co-location refuses it off screen. `ActionReach.ThroughChannel` names an established contact
or report channel and is the exception: `report` reaches whoever holds authority without anybody
standing anywhere, which is the standing rule that organizations receive information through
identifiable channels. The channel buys eligibility and nothing else - no meeting, no place, no
hour, and `ActionSupport.Bystanders` still has no room to draw anybody from. See
[D090](../agent/decisions.md#d090--what-was-seen-and-what-is-merely-recorded-are-different-evidence-and-a-verb-says-which-one-it-needs).

Source: [ActionOpportunity](../../src/BrilliantQuesting.Core/Actions/ActionOpportunity.cs),
[NarrativeAction](../../src/BrilliantQuesting.Core/Actions/NarrativeAction.cs),
[AttemptFeasibility](../../src/BrilliantQuesting.Core/Actions/AttemptFeasibility.cs).
Proof: [ActionOpportunityTests](../../tests/BrilliantQuesting.Core.Tests/ActionOpportunityTests.cs).
Lab: [off-screen-schemes](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/OffScreenSchemesScenario.cs),
[actor-action](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/ActorActionScenario.cs).

## Availability

**Owns:** side-effect-free feasibility classification and rejection reasons. Inputs: actor scope,
purpose binding, knowledge, resources, presence and capabilities. Outputs: offered/rejected attempts
for registry, contextual projection and autonomy. No saved availability cache. **Does not own:**
success odds, consequence resolution or presentation pacing. Low skill changes the check, not the
menu. Native capability refusal and hard impossibility differ from a contested attempt or a systemic
action with no roll. `ActionAttempt.Run` rechecks availability; the base `Perform` structurally gates
actor scope, not every verb-specific precondition. Native mutation gates remain necessary underneath.

**Opportunity before feasibility before difficulty (BQa-005, BQa-014).** `AttemptFeasibility.Classify`
is the one place the questions are asked in order, and there are three of them. [Opportunity](#opportunity)
comes first because it is about the place and the hour rather than about the verb: somebody Elin is
carrying between zones, or two people the save keeps a valley apart, are not a hard attempt but no
attempt at all, and asking the verb first would spend its reasoning - and let a caller read a
considered yes - on a place the act could not occur in. The refusal is `NotRelevant` and carries the
observation behind it verbatim; `AttemptFeasibility.Opportunity` carries the reading either way, so
a caller ranking options has the plausibility of the ones that were allowed as well as the reason
for the ones that were not. Then availability, and only for a possible attempt the certainty
question. It is side-effect free like the availability call underneath it, and `ActionAttempt.Run`
and `TheftLaboratory.Perform` both go through it rather than each remembering the order. A refused
attempt returns at that gate, so no resolver is asked and the actor's RNG stream does not move;
`Uncertainty` is null for it rather than `Certain`, because "no uncertainty here" reads as
permission to resolve without a roll and a refused attempt must not resolve at all.

Certainty comes from the verb's contract through `ProceduralCheckProfiles.FamilyForAction`, never
from the arithmetic a resolver would produce: a very favourable DC is mastery, not the disappearance
of uncertainty, and a profile's dice and critical windows are untouched by it (BQa-004). An uncertain
attempt keeps its classified family and profile at either extreme of advantage. High statistics still
invent no facts, resources, access or authority — those are availability's refusals, and a check
success does not overturn them. See
[D081](../agent/decisions.md#d081--feasibility-is-settled-before-uncertainty-and-certainty-is-read-off-the-verb-rather-than-off-the-difficulty).

Source: [Availability](../../src/BrilliantQuesting.Core/Actions/Availability.cs),
[AttemptFeasibility](../../src/BrilliantQuesting.Core/Actions/AttemptFeasibility.cs),
[ActionOpportunity](../../src/BrilliantQuesting.Core/Actions/ActionOpportunity.cs),
[NarrativeAction](../../src/BrilliantQuesting.Core/Actions/NarrativeAction.cs),
[ContextualActionProjection](../../src/BrilliantQuesting.Core/Actions/ContextualActionProjection.cs).
Proof: [ActionAvailabilityTests](../../tests/BrilliantQuesting.Core.Tests/ActionAvailabilityTests.cs),
[ActionBindingTests](../../tests/BrilliantQuesting.Core.Tests/ActionBindingTests.cs).
Lab: [theft](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/TheftLaboratoryScenario.cs),
[performance](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/PerformanceScenario.cs) for restored-history
CPU/allocation inspection (not native frame time).

## Competition

**Owns:** which of several people reaching for one indivisible thing gets to try for it, and the
transient hold that stops a coarse scheduler letting two of them finish it at once. Inputs: one
gathered batch of `ActionCandidate` intentions, the world stream, a registry and an
`IAttemptEnvironment`. Outputs: an `ArbitrationResult` - a ranked decision per contender, each
attempt that was actually run, and every claim taken with the reason it was given back. Nothing
here is saved. **Does not own:** goal ranking (the caller's `Motive` is carried, never recomputed),
the verb's own rules, the roll, or any schedule - `ArbitrationBatch` has no clock and runs when a
host resolves it.

**Contest, claim and the draw (BQa-015).** `ActionContest` keeps two questions apart. *What* is
contended for is read off the intent, because the intent already said which object, matter or
person the attempt is about. *Whether it is indivisible* is read off the verb's BQa-010 effects, so
there is no second list of exclusive verbs to drift (`D086`); the five
`ActionContest.IndivisibleEffects` are the changes a second completion would have to invent a
second subject for. Most contests are not exclusive and nothing pretends otherwise - two people can
both tell the reeve, both learn the same fact - and an undeclared verb yields a shared contest,
because BQa-010's reported coverage gap must not read as scarcity.

`ArbitrationBatch.Gather` copies its input, so a caller still filling its own list cannot change a
decision already being made. Contenders are grouped by `ActionContest.Key`, the groups are walked
in ordinal key order, and inside a group the order is standing (the caller's motive scaled by what
`ActionOpportunity` allowed), then who is ready sooner, then a keyed draw, then the contender's own
id. The draw is `RngStreams.TieBreak`, forked by batch, contest and contender: ties move with the
seed and with the batch - which is to say with time - and never with the order anything was
enumerated in. It is a fork rather than a draw, so ranking a crowded contest cannot move the check
its winner is about to roll.

`TransientClaim` is taken only for an exclusive contest and buys one thing: nobody else executes
against that contest while it is held. Each winner is revalidated through `IAttemptEnvironment`
immediately before execution rather than against the reading it was ranked on, because the world
the loser was ranked in is precisely the one where nobody had taken the purse yet. The claim is
released on refusal, on a fault, on cancellation, on commit and at batch end, and only a commit
also closes the contest: an attempt that was made and changed nothing leaves the object where it
was, so the next contender is owed their turn. No reservation is ever written to a save - history
gets the committed outcome and nothing else. There is no player branch: the player enters the same
batch and loses to an NPC who wants it more, is readier, or wins the draw. See
[D091](../agent/decisions.md#d091--one-indivisible-thing-is-settled-by-a-ranked-batch-and-a-claim-that-outlives-nothing).

Source: [ActionContest](../../src/BrilliantQuesting.Core/Actions/ActionContest.cs),
[ActionArbitration](../../src/BrilliantQuesting.Core/Actions/ActionArbitration.cs),
[RngStreams](../../src/BrilliantQuesting.Core/Foundation/RngStreams.cs).
Proof: [ActionArbitrationTests](../../tests/BrilliantQuesting.Core.Tests/ActionArbitrationTests.cs).

## Checks

**Owns:** declared uncertainty and explainable arithmetic. `CheckRequest` supplies a profile,
actor/target and labeled modifiers; `ICheckResolver` returns `CheckResult`. Portable resolution uses
profile dice/critical windows and deterministic RNG. Requests/results are transient; world RNG state
and semantic outcomes are saved. **Does not own:** feasibility or vanilla outcomes already resolved
by Elin. Plugin `ElinCheckResolver` defaults to portable resolution; its native path is diagnostic,
and native rows supply difficulty text. Do not swap to Elin RNG merely because `Check.Perform` exists.
Consumers: verbs and routed storylet beats.

**Check family.** Every profile declares a `CheckFamily`: `Opposed` (capability against a resisting
actor or state) or `Absolute` (capability against a fixed challenge). `Certain` is the third kind and
never sits on a profile - an attempt with no uncertainty has no profile, and `FamilyForAction` returns
it for the verbs that roll nothing. The family is declared, not inferred from the terms present: an
absolute profile may not take a target attribute or target level at all (`InvalidOperationException`),
and an opposed profile that declares no opposition fails classification. `TargetLevelIsOpposition`
is the only sanctioned reading of a level term; nothing may infer universal level scaling from its
absence. Family now selects the arithmetic as well (BQa-004). See
[D079](../agent/decisions.md#d079--a-check-declares-which-kind-of-uncertainty-it-is-and-the-portable-resolver-is-the-authority-rather-than-a-stand-in)
and [D080](../agent/decisions.md#d080--an-opposed-check-scales-on-the-ratio-between-the-two-sides-and-a-partial-band-is-paid-to-neither).

**Opposed arithmetic.** An `Absolute` DC stays the flat sum it was: base minus each declared actor
term, plus situational modifiers. An `Opposed` DC is a ratio instead. Both sides compose in double
precision - actor skills and attributes against target attributes, plus target level only where
`TargetLevelIsOpposition` - and the DC moves by
`wholeNumber(log2(stabilize(actorPower) / stabilize(targetPower)) x 3)`, subtracted so an advantage
lowers it. One doubling of relative power is one band worth 3 DC at any scale, so 2000 against 1000
is the advantage 2 against 1 is. `BaseDifficulty` and situational modifiers stay outside both
composites; folding them in would make a fact about the occasion scale with the contest. Mastery is
uncapped and the profile's dice/critical windows are untouched, so a low DC never abolishes a
retained fumble. The stabilizer is one floor of 1.0 applied identically to both sides, which also
absorbs negative and non-finite composites; the whole-number rule is toward zero in both directions,
the only rule under which swapping the two sides negates the adjustment exactly.

`CheckResult.Opposition` carries that working - both composites, the band count and the applied
adjustment - because a list of signed deltas cannot show a ratio. It is non-null on exactly the
opposed resolutions of this resolver and null elsewhere, including the native diagnostic path.
`CheckResult.Terms` keeps its invariant: every entry is a real signed contribution to the DC, and
the composite arrives there as the single `opposed power` term.

Deliberate rebalance: a single-element opposed row no longer reproduces vanilla's flat `SourceCheck`
sum, and the recorded baseline distribution test now covers the absolute family, which still does.
Near parity the ratio is more conservative than the flat sum was - a gap of 1.5 that used to read as
a point of advantage is 0.26 of a band and buys nothing - while a large relative advantage is worth
far more than a flat difference could express.

Limits the classification records rather than assumes away. An opposed profile resolved with no
target keeps its actor terms and silently loses its opposing ones. A stat that cannot be read -
unknown alias, unbound `Chara`, disabled read capability - returns 0 from both vanilla states and is
dropped from the trace exactly like a genuine 0; only `CheckRequest.WithUnreadTerm` distinguishes
them. `proc_fencing` and `proc_festival_competition` are classified absolute because neither row
reads a rival, which is a limit of those rows, not proof that nobody is on the other side.

Source: [VanillaStyleCheckResolver](../../src/BrilliantQuesting.Core/Checks/VanillaStyleCheckResolver.cs),
[ElinCheckResolver](../../src/BrilliantQuesting.Plugin/ElinCheckResolver.cs).
Proof: [CheckTests](../../tests/BrilliantQuesting.Core.Tests/CheckTests.cs).
Lab: [actor-action](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/ActorActionScenario.cs).
Evidence: [checks API](../elin/api/checks-elements-skills.md),
[portable rationale](../elin-api-notes.md#why-procedural-checks-resolve-portably).

## Reactions

**Owns:** `ConsequenceEngine` subscribes to history and propagates knowledge, thread tension,
memories, social reactions, favors and player standing through consequence profiles and contextual
policies. Inputs: events, witnesses, practices, ties, observed/judged status. Outputs: mutations of
the owning stores and gated native standing writes. Engine/trace are transient; outputs are saved by
their owners. **Does not own:** all state mutation, item transfer, truth creation in every action,
or universal institutional judgment. Unnoticed deeds must not leak through affinity; observed
violence is not automatically murder. Witness reactions use `SocialPractices.NormFor` to read only
practices bearing on the current event, using the same table as a full reading; nothing is cached
across events. Consumers: social state, journal, developments, future acts.

An act resolved where nobody's presence was read produces no witnesses, whatever the caller's
witness list holds (BQa-013): `ActionSupport.Bystanders` refuses off screen rather than relying on
each verb to remember, which is what keeps a hidden failure from costing somebody trust.

Observed possession change (BQa-012) is the recorder's one actorless intake: a readback saying a
thing is no longer whoever's the record says, which supersedes the standing claim and names no
culprit, no crime and no witness. It is idempotent against the record rather than against a session,
so reconciling a world that already agrees does nothing. A theft somebody watched stays `Theft`.

Source: [ConsequenceEngine](../../src/BrilliantQuesting.Core/Consequences/ConsequenceEngine.cs),
[SocialPractices](../../src/BrilliantQuesting.Core/World/SocialPractices.cs),
[VanillaActionRecorder](../../src/BrilliantQuesting.Core/Integration/VanillaActionRecorder.cs).
Proof: [ConsequenceTests](../../tests/BrilliantQuesting.Core.Tests/ConsequenceTests.cs),
[RecognizedViolenceTests](../../tests/BrilliantQuesting.Core.Tests/RecognizedViolenceTests.cs).
Lab: [theft](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/TheftLaboratoryScenario.cs),
[performance](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/PerformanceScenario.cs) for restored-history
CPU/allocation inspection (not native frame time).
Native: [standing evidence](../elin/capabilities.md#capability-routing).

## Threads

**Owns:** durable matters, linked entities/facts/sites, escalation schedule/completed steps, lifecycle,
resolution and storylet firings. These are saved. `ThreadEngine.Advance` applies due steps through
registered archetype handlers; `ThreadLifecycle` reviews viability; `ThreadResolution.Resolve`
records an ending once. Inputs: time, events and participating state. Outputs: escalation, ending,
inheritance/quarantine and history for autonomy, discovery, storylets and chronicle.
**Does not own:** a quest objective UI, all developments or native life state. Unresolved bindings
are not dead actors. Handler registrations are transient and must be restored by the host.

Source: [ThreadEngine](../../src/BrilliantQuesting.Core/Threads/ThreadEngine.cs),
[ThreadLifecycle](../../src/BrilliantQuesting.Core/Threads/ThreadLifecycle.cs),
[ThreadResolution](../../src/BrilliantQuesting.Core/Threads/ThreadResolution.cs).
Proof: [ThreadLifecycleTests](../../tests/BrilliantQuesting.Core.Tests/ThreadLifecycleTests.cs),
[PersistenceTests](../../tests/BrilliantQuesting.Core.Tests/PersistenceTests.cs).
Lab: [questline](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/QuestlineScenario.cs).
Native: life/location in [capabilities](../elin/capabilities.md).

## Developments

**Owns:** `DevelopmentDetector.Detect` reads standing conditions into ID-linked `Development`s over
several authoritative families: unproven knowledge, belief conflict, unresolved crime, damaged
property, shortage, service continuity, unmet obligation and organization stake. Inputs: saved
facts/beliefs/obligations/demands/businesses/organizations/history/threads. Outputs: stable sorted
pressure readings for storylet eligibility and inspector. **All derived; never saved.**

Two contracts hold the rule set together. **Aggregation:** rules contribute readings, and readings
naming the same standing condition merge onto one id, so a demand-ledger entry and the `Needs` claim
it cites are one pressure with two sources; distinct causes keep distinct ids. Urgency is the
highest contributing reading, never a sum. **Bounded intake:** `Detect(world, DevelopmentScope)`
enumerates an affected work set of facts/obligations/sites/businesses/organizations, so a live
consumer does not scan every store; `DevelopmentScope.EntireWorld` is the unbounded diagnostic and
fixture reading. Every rule applying to an entity in the work set runs on it exactly as in a
whole-world pass, with the same identity, tags and urgency; a work set answers only for what it
names, so a condition recorded in two stores is complete when both are named and a later pass
naming the other source lands on the same development.

Each rule declares source, default and refusal. Refusals are the contract: no live vanilla stock,
operator availability or native crime state is read, and unsupported native facts stay unknown.
Positive (`opportunity`) and easing (`recovering`) readings exist so the cycle is not only crises.

**Does not own:** a lifecycle, new facts/threads, actor-local interpretation of who cares — that is
`ActorPressureView`'s, under [belief and proof](state.md#belief-and-proof) — or a
promise of a scene. Resolution belongs to source state: pressure disappears when no longer derivable.
`DevelopmentScoring` is a separate attention reader over eligible news, not a second `Development`
store or general goal planner.

**Feedback intake (BQa-012).** `PressureFeedback` is what supplies that work set: it collects real
changes into a `PressurePass` carrying the scope to read next and the people a changed record names.
Three intakes, because changes arrive three ways — `Attach` listens to the ledger, `Changed` is the
route for a store mutation no event announces (demand relieved, claim superseded, debt fulfilled),
and `Inspect` is a bounded rotation over the records the detector reads, never over actors or
history, covering elapsed-time conditions and missed invalidations. Attach after load, as
`ConsequenceEngine` does: restored events are not dispatched, so reattachment is not replay. Woken
actors are the parties the changed record itself records, its claim's knowers included, so somebody
holding no goal is still considered; waking is not informing, and what any of them may legitimately
make of it stays `ActorPressureView`'s answer. **Derived and never saved**, like the pressures: it
writes no fact, records no event and chooses no goal.

Source: [PressureFeedback](../../src/BrilliantQuesting.Core/Developments/PressureFeedback.cs).
Proof: [PressureFeedbackTests](../../tests/BrilliantQuesting.Core.Tests/PressureFeedbackTests.cs).
Rationale: [`D088`](../agent/decisions.md#d088--a-change-reaches-the-next-reading-through-the-owner-that-holds-it-and-wakes-only-the-people-that-owner-names).

Source: [DevelopmentDetector](../../src/BrilliantQuesting.Core/Developments/DevelopmentDetector.cs),
[DevelopmentScope](../../src/BrilliantQuesting.Core/Developments/DevelopmentScope.cs),
[DevelopmentPressures](../../src/BrilliantQuesting.Core/Developments/DevelopmentPressures.cs),
[DevelopmentExpression.Opportunities](../../src/BrilliantQuesting.Core/Developments/DevelopmentExpression.cs),
[Development](../../src/BrilliantQuesting.Core/Developments/Development.cs).
Proof: [DevelopmentLayerTests](../../tests/BrilliantQuesting.Core.Tests/DevelopmentLayerTests.cs),
[PressureSynthesisTests](../../tests/BrilliantQuesting.Core.Tests/PressureSynthesisTests.cs).
Lab: [playground](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/PlaygroundScenario.cs).
Rationale: [character design](../design/character-dialogue-system.md#365-development-layer),
[`D082`](../agent/decisions.md#d082--pressure-is-aggregated-by-the-condition-and-read-from-a-bounded-work-set).
