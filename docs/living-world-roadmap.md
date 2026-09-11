# Living-world implementation roadmap

## Authority and start gate

This is the authoritative **post-BQ** implementation plan for Brilliant Questing. It adopts and
corrects the supplied Living World Beta Roadmap v4; the [adoption audit](agent/bqa-roadmap-audit.md)
records the evidence, every original step's disposition and the dependency changes. It is a plan,
not evidence that any BQa behavior exists. Current source/tests/runtime evidence outrank the plan.

The [BQ implementation roadmap](implementation-roadmap.md) remains authoritative for unfinished
`BQ-xxx` launch/hardening work and its acceptance criteria. Finish that phase before BQa-001:
complete its remaining implementation and required acceptance, or obtain an explicit owner-approved
disposition recorded against the original BQ requirement. Existing limited deferrals retain their
exact scope; permission to proceed to BQ-110 is not blanket permission to skip BQ launch gates.
This adoption does not authorize implementation or declare BQ complete.

Use [AGENTS](../AGENTS.md), then the selected step and its authority/proof route. **One step, one
coherent commit**, with the BQa ID in a future implementation commit subject. Implement in numerical
order. Each step also depends on its immediate numerical predecessor; `Depends` names significant
contract prerequisites, not permission to skip preceding steps. Each step leaves the mod installable;
new paths remain safely gated until their stated evidence exists. If unforeseen work cannot fit one
coherent commit, amend this plan and its dependencies explicitly before that work, not silently inside
an unrelated step. Status lives in Git, code, tests and canonical runtime evidence.

> **BQ does not generate stories. It generates and interprets a causally persistent world in which stories become possible.**

## Required causal loop

```text
Elin / authoritative BQ state
→ objective pressures + independent actor-held belief/need candidates
→ actor-local interpretation through legitimate knowledge, belief and stake
→ machine-readable evolving goals
→ registered potential action effects and concrete actor-accessible bindings
→ feasibility / evidence-graded opportunity / competition
→ shared action/check or delegated native outcome
→ authoritative consequences and legitimate observation/reporting
→ changed state → next bounded causal pass

changed state → read-only situation proposals → admission
→ owner-controlled safe establishment around existing causes
→ development/storylet opportunity → incremental interaction
→ actual communication delivery or player action → consequences → world loop
```

The first loop runs with no player involvement, no running storylet, no presentation and no live
thread. A matter is useful durable organization of state, not permission for actors to care or act.
An actor can sincerely respond to an objectively false premise. Goal evaluation must not disclose
hidden objective satisfaction. Organizations receive information through identifiable channels.

**Creating causes versus interpreting causes:** native activity and registered autonomous actions
can create incidents. Recurring situation recognition cannot perform a theft merely to establish a
theft matter. Owner-controlled establishment may add a current commitment or justified entity/site
record, but must not invent its own qualifying incident, witnesses, history or shortages. Existing
bootstrap/staging constructors are not automatically safe recurring producers.

**Independence:** simulation capacity and conservation bound work/state; attention/engagement bound
player exposure. Rendering and debug telemetry are optional. Actual communication and intervention
can change the world, but optional wording, scene search and UI opening cannot. With identical
external inputs and player actions, expression-only differences must not alter simulation RNG or
NPC outcomes. Disabling the entire mod is a separate native safety test, not expression independence.

## Phase route

| Phase | Steps | Outcome |
|---|---|---|
| BQa-L1 | BQa-001–BQa-013 | Causal references, RNG isolation, checks, pressure, local goals and shared action feedback |
| BQa-L2 | BQa-014–BQa-020 | Opportunity, competition, tested live cycle and legitimate organization agency |
| BQa-L3 | BQa-021–BQa-025 | Read-only recognition, safe establishment, cross-matter recurrence and state-led escalation |
| BQa-L4 | BQa-026–BQa-032 | Semantic wording, stable voice, incremental interactions and live continuation |
| BQa-L5 | BQa-033–BQa-035 | Operation-level native decisions and honestly embodied consequences |
| BQa-L6 | BQa-036–BQa-039 | Knowledge-gated journal, discovery, commissions and exposure weighting |
| BQa-L7 | BQa-040–BQa-045 | Diversity, ecology, long-horizon, causal/counterfactual and live acceptance |

## Evidence and persistence rules for every step

Each step's **Authority / proof route** links the current owning sources and representative tests.
Inspect them before editing; extend those owners, not another subsystem with a similar name.
Every Done-when is mandatory together with its refinements, this document's standing contracts and
the final acceptance matrix. Tests must execute production code. Fixtures may provide initial state,
external inputs and controlled adapter behavior, never a substitute planner, scheduler, action effect
or authored follow-up incident. A headless path is not live proof.

Use [targeted validation](agent/validation.md), including the existing persistence and content gates
when affected. Any live consumer also needs Plugin build and real-save evidence for its changed
join, interruption/fallback and save/reload behavior. Record the build, operation, readback and result
in the owning [Elin evidence](elin/capabilities.md). Source observation, metadata, headless proof,
runtime verification and unsupported operation remain distinct. Unknown does not mean impossible
in Elin forever, and a saved handle does not prove physical existence.

Every new durable field belongs to its introducing feature commit; there is no late migration phase.
Use `WorldStateSerializer`/`SaveMigrations`, old-save defaults or explicit version migration as
appropriate, frozen historical fixtures, round-trip and no-redispatch checks. Preserve ID/RNG
continuity. New unsupported goal/condition vocabulary degrades explicitly, never guesses a behavior.

| Durable contract | Owning step |
|---|---|
| Typed causal links and minimal committed-decision evidence | BQa-001 |
| RNG continuation or stable decision-key changes, only if needed | BQa-002 |
| Individual goal conditions, provenance, lifecycle and actor assessment | BQa-008 |
| New source-state fields needed by feedback, if any | BQa-012, in the existing source owner |
| Consumed cycle/opening markers and deterministic resume | BQa-016; BQa-017 consumes this contract at native hooks |
| Institutional information receipts and organization goals | BQa-018 |
| Organization operation/resource outcomes and enrollment identity, if extended | BQa-019, BQa-020 respectively |
| Committed establishment identity and created owned records | BQa-022 |
| Recurrence/episode identity or scheduling markers if not derivable | BQa-024 |
| New escalation/lifecycle markers if required | BQa-025 |
| Stable voice assignment/version | BQa-028 |
| Semantic acknowledgment/idempotence records needed across interruption | BQa-029; BQa-032 extends only missing continuation semantics |
| Verified site/arrival manifests if extended | BQa-033–BQa-035, existing site/travel owners |
| New commission terms or earned engagement evidence if needed | BQa-038, BQa-039 respectively |

Pressures, actor-local pressure views, effect metadata, opportunity scores, proposals, temporary
claims, native objects, session cursors, rendered lines and journal pages are derived/transient.
Retain only minimal durable semantic outcomes needed for continuity; do not turn this table into
a mandate to persist an already derivable value. Temporary reservations never enter history.

---


# Phase BQa-L1 — Causal foundations and actionable motives


## BQa-001 — Causal identity and durable provenance foundation

Harden causal references before goals, feedback and recurring execution consume them. Reuse `WorldEvent.Id`, `Fact.OriginEvent`, source-event references, `NarrativeWorldState.Record`, the queued ledger and existing ID minter. Event IDs already exist: this is not a second ledger or a universal event-sourcing rewrite.

Give newly recorded transitions explicit typed causal references sufficient to distinguish triggering events, actor belief/goal motivation, affected entities and the actual result. Allocate or reserve an event identity through its owner before constructing facts that reference it; never predict the next ID or recover cause from list position, timestamp, neighboring events or prose tags. Carry multiple causes where necessary; unknown historical provenance stays unknown. Do not rewrite historical events to manufacture links.

This step owns serializer/default/migration coverage for this new provenance seam, including old saves and references to missing/quarantined records. A bounded diagnostic decision record may retain the inputs/reason codes needed to explain a committed decision after the world changes; it must not copy entire snapshots, pressure lists or every rejected candidate into history. Keep this commit about causal identity and provenance. BQa-002 separately audits and enforces simulation/expression RNG and decision-key isolation.

**Depends:** BQ-001, BQ-002, BQ-105, BQ-106; the BQ phase start gate above.

**Done when:** a nested reaction records unambiguous causes without relying on event adjacency; new fact origins resolve to the intended event after nested recording and reload; old fixtures retain unknown provenance without replay; rejected read-only provenance inspection consumes no IDs; and an inspector can distinguish motive evidence from objective cause and outcome. A repeated-transfer/repeated-accusation test over one physical item must distinguish the original theft, a later theft attempt, recovery, transfer back and an accusation about the original theft across reload. Existing facts/beliefs, action bindings and history must retain the intended occurrence reference; future goal/dialogue consumers extend that reference rather than substituting item identity for event identity.

**Do not:** rewind an ID minter, redispatch restored events, treat the ledger as the sole owner of every store, or persist derived state simply for convenient querying.

**Implementation/evidence** `WorldEvent.Provenance` carries typed `Trigger`/`About`/`Motive`/`Outcome`
links and an optional bounded decision record; `Record` takes it, `ReserveEvent` hands out an identity
for a record that must name its origin first, and `CausalHistory` resolves links back - reporting a
reference whose record has gone as missing rather than dropping it. `Related`/`Evidence`/`Witnesses`
keep their existing meaning, so affected entities and causes stay distinguishable without a second
store. Schema 12 persists provenance and migrates older events to explicit unknown. The durable rule
is [`D077`](agent/decisions.md); the reading surface is `NarrativeInspector.DescribeCausality`, which
prints objective cause, motive evidence and outcome separately and mints nothing.
`CausalProvenanceTests` covers the repeated-transfer/repeated-accusation case over one object across
reload, nested reaction after intervening history, reserved identity, rejected read-only inspection,
missing references and an old save gaining no inferred cause. Semantic defect fixed in scope: a
pickpocket's theft fact was built with no origin event at all, so nothing linked the claim to the
occurrence it came from.

**Live verification still required** headless Core only. The changed recorders (`report`,
`pickpocket`, `plant_evidence`, `return_item`) have not been exercised in a live Elin session, and
the Plugin was not compiled here because the Elin assemblies are not redistributable.

**Authority / proof route:** [owning source and representative tests](systems/state.md#history), [neighbor contract](systems/integration.md#persistence), [validation](agent/validation.md#state).

**Sequence:** BQ phase start gate → BQa-001 → BQa-002.

---


## BQa-002 — Simulation/expression RNG isolation

Audit and enforce the boundary between authoritative simulation randomness and optional expression
before new checks, goals and recurring execution consume it. Reuse `DeterministicRng.Fork`: it
already derives independent streams without advancing its parent, and `StoryletRouter` already
uses separate check and line labels. This is a call-site/stream-ownership contract and regression
proof, not an assumption that RNG separation is absent or that a second RNG system is needed.

Trace simulation choices/checks, wording, voice assignment, scene search and delivery-related
decision keys. Define stable stream labels and occurrence keys so optional rendering, changed
expression draw counts and delivery-event ID allocation cannot perturb simulation draws or
tie-break keys. Repeated genuine attempts must have the intended distinct occurrence identity;
reopening a surface must not grant another simulation roll. Preserve BQa-001's event identity
authority rather than creating a parallel causal-ID store.

Only introduce persistent stream state or versioned key data if existing saved seed/state and
stable references cannot provide the required continuation. This step owns any resulting explicit
defaults/migration and old-save replay contract; separate persistence is not mandatory merely
because streams are separate. Do not reset an advancing stream by repeatedly forking the same
label and inadvertently repeating outcomes.

**Depends:** BQa-001; BQ-001, BQ-004, BQ-105, BQ-106.

**Done when:** production call-site tests with identical external observations and semantic player
actions produce the same subsequent simulation choices, checks and tie-break results with optional
expression on/off, extra wording draws and delivery records present/absent. Rejected read-only
inspection/selection consumes no simulation draws or authoritative IDs. Repeated genuine
attempts use the declared occurrence policy; UI reopening gains no roll; save/reload and old saves
preserve the documented continuation. Existing independent forks remain reused, and any newly
persisted data has its own migration/default proof. Actual communication or different player actions
may change state and are not falsely treated as expression-only differences.

**Do not:** replace the RNG algorithm, rebalance checks, create another simulation scheduler, force
new saved streams, or weaken causal provenance to make an independence comparison pass.

**Authority / proof route:** [existing RNG](../src/BrilliantQuesting.Core/Foundation/DeterministicRng.cs),
[foundation tests](../tests/BrilliantQuesting.Core.Tests/FoundationTests.cs),
[routed check/line consumers](../src/BrilliantQuesting.Core/Storylets/StoryletRouter.cs),
[persistence](systems/integration.md#persistence), [validation](agent/validation.md#persistence).

**Sequence:** BQa-001 → BQa-002 → BQa-003.

---

## BQa-003 — Check-family classification and native-reference audit

Classify every BQ ability check by the kind of uncertainty it represents before changing its mathematics.

```text
opposed
→ actor capability against an opposing actor/state

absolute/world
→ actor capability against a fixed challenge in the world

feasibility/certainty
→ no roll when the attempted outcome is impossible or when uncertainty is not semantically present
```

Audit current `CheckProfile`, `VanillaStyleCheckResolver`, `ElinCheckResolver`, Elin `Check`/DC behavior and representative action families. Record which parts intentionally resemble vanilla and which deliberately differ for BQ's replay-authoritative narrative simulation.

The current production contract must be stated explicitly: composite BQ checks remain deterministic and authoritative on the portable BQ path. Native `Check` rows may provide presentation text, reference behavior and future evidence, but `Check.Perform` is **not** automatically the target authority for composite BQ checks because native RNG and single-element rows do not currently match the replay contract.

**Depends:** BQ-004, BQ-023–BQ-029; BQa-001, BQa-002.

**Done when:** every production check family used by BQ is explicitly classified; representative current portable DC calculations are covered by tests; native final-DC behavior, compression and row limitations are documented where observed; and no production contract implies that composite BQ checks must migrate to native RNG to be "correct."

**Do not:** change progression scaling yet, silently classify ambiguous checks as opposed, force replay-authoritative checks through native RNG, or make every challenge scale against the actor.

**Audit refinement:** include every registered profile and beat-declared check, fixed challenges, composite opposition and missing-stat reads. Record source observation separately from a runtime DC sample; absence of a live sample is not a reason to change authority. Existing distribution tests and profile-specific critical windows remain regression constraints.


**Authority / proof route:** [owning source and representative tests](systems/actions.md#checks), [neighbor contract](systems/actions.md#availability), [validation](agent/validation.md#actions).

**Sequence:** BQa-002 → BQa-003 → BQa-004.

---


## BQa-004 — Progression-safe opposed power-band scaling

Replace raw linear opposed-stat pressure with continuous relative power scaling that remains useful across Elin's effectively open-ended progression.

For an **opposed** check, define two positive capability composites from the profile's explicitly classified opposing terms:

```text
actorPower  = weighted composite of actor-side opposed capability
 targetPower = weighted composite of target-side opposed capability
powerBands  ≈ log2(stabilizedActorPower / stabilizedTargetPower)
raw DC adjustment = powerBands × 3
Final DC = BaseDC - wholeNumber(raw DC adjustment) + situational modifiers
```

`BaseDC` and situational modifiers are not part of either power composite. Target level contributes to `targetPower` only where BQa-003 explicitly classified that profile's level term as part of the opposition; there is no hidden universal level scaling. Zero and near-zero values must use one documented deterministic stabilizer so the logarithm never invents infinities or asymmetry.

Each doubling of relative power is one internal power band worth about 3 DC. Do not cap mastery by default: extreme advantage should be able to trivialize weak opposition, while fixed world challenges do not rise automatically with the player.

Before production behavior changes, choose and name the whole-number policy for the negative direction as well as the positive direction. It must be intentional and symmetric in design, not an accidental consequence of C# truncation versus mathematical floor. Tests pin that decision.

**Depends:** BQa-003.

**Done when:** opposed checks scale predictably at parity and across ratios such as 2:1, 4:1, 1:2 and 1:4; mixed weighted stats, zero/near-zero inputs and explicit target-level inclusion/exclusion are covered; extreme mastery remains possible; and tests pin the chosen whole-number rule in both directions.

**Do not:** apply relative-power scaling to absolute challenges, put situational modifiers inside the power ratio, or introduce hidden level scaling.

**Acceptance refinement:** test invariance when both opposing composites are scaled equally, monotonicity, negative/invalid inputs, deterministic rounding at boundaries, and high-level overflow. Preserve profile dice/critical windows: mastery can lower DC without abolishing an explicitly retained fumble. Explain any deliberate rebalance against recorded baseline distributions. Do not silently change absolute checks.


**Authority / proof route:** [owning source and representative tests](systems/actions.md#checks), [neighbor contract](systems/actions.md#availability), [validation](agent/validation.md#actions).

**Sequence:** BQa-003 → BQa-004 → BQa-005.

---


## BQa-005 — Feasibility before difficulty

Determine whether an action is possible before asking whether any genuine uncertainty remains, and only then ask a check resolver to model that uncertainty.

```text
intent
→ action availability / authoritative preconditions
→ impossible: refuse with reason
→ available
   → semantic certainty classification
      → certain/no meaningful uncertainty: resolve without an artificial roll
      → genuinely uncertain: resolve through the classified check family
```

Existing `NarrativeAction.GetAvailability`, actor scope, semantic bindings and capability evidence remain the authority for whether an attempt is legal/possible. The certainty decision is narrower: it says whether a feasible attempt actually contains uncertainty worth rolling.

High statistics must never invent missing facts, resources, physical access or authority. Conversely, a very favorable DC is **not by itself** proof that uncertainty disappeared; an uncertain action may remain rollable even when mastery makes failure rare. No-roll certainty should come from the action's semantics and authoritative state, as with a transfer that simply succeeds once the required owned item and valid recipient are present.

**Depends:** BQa-003, BQa-004; BQ-093, BQ-137.

**Done when:** representative actions demonstrate impossible, semantically certain and genuinely uncertain paths; impossible cases never reach RNG; certainty bypasses meaningless checks only where the action contract justifies it; and uncertain checks still route to the correct family even at extreme advantage/disadvantage.

**Do not:** conflate unlikely with impossible, equate low DC with certainty, or let check success override authoritative-world constraints.

**Authority / proof route:** [owning source and representative tests](systems/actions.md#availability), [neighbor contract](systems/actions.md#checks), [validation](agent/validation.md#actions).

**Sequence:** BQa-004 → BQa-005 → BQa-006.

---


## BQa-006 — General pressure synthesis

Extend the existing derived `Development`/pressure seam over authoritative state.

A pressure is not a quest, storylet, event, or second state store. It is:

> **a current condition that gives one or more actors reason to act.**

Inputs may include only authoritative or safely derived information such as:

```text
ownership mismatch
unfulfilled obligation
shortage / local demand
damaged property
missing actor
travel failure
evidence conflict
relationship hostility
unmet need
business/service interruption
danger
authority conflict
resource contention
unresolved crime
organization loss or opportunity
site access or damage
```

The detector remains deterministic, side-effect-free and derived: a pressure disappears when the authoritative condition that produced it disappears. New rules extend the current `Development` authority rather than adding a parallel pressure store.

Broader coverage also needs a **stable aggregation contract**. Equivalent readings of the same standing condition must deduplicate to the same derived pressure identity; distinct causes must remain distinguishable where they can matter later. Detection should operate over affected threads/actors/sites or another bounded work set where production code can supply one. A convenient full-world scan in a Lab harness must not become the live per-tick implementation after BQ-107/BQ-108 introduced bounded simulation work.

**Depends:** BQa-001; BQ-069, BQ-050, BQ-051, BQ-107, BQ-108.

**Done when:** several distinct authoritative state families produce stable generic pressures without naming or selecting a storylet/situation; equivalent conditions deduplicate deterministically; repeated detection mutates nothing; and production detection can be bounded to an inspectable affected work set rather than requiring an unbounded world scan.

**Do not:** create one detector class per quest plot, persist derived pressures merely to make them easier to query, teach actors facts by detecting them, or bypass existing simulation budgets.

**Required coverage:** the first rules must cover property/crime, economic/service continuity, social obligation/belief conflict and existing organization stakes, with source/default/refusal defined for each. Unsupported native facts remain unknown; the beta can use honest BQ-owned obligations/business continuity, but not fabricated native stock. Include positive opportunities and recovery/quiet states so the cycle does not only generate crises. Source changes without a thread remain detectable. Initial rules use bounded supplied work sets; BQa-012 and BQa-016 connect invalidation and recurring execution, then BQa-017 supplies live hooks.


**Authority / proof route:** [owning source and representative tests](systems/actions.md#developments), [neighbor contract](systems/world.md#organizations), [validation](agent/validation.md#actions).

**Sequence:** BQa-005 → BQa-006 → BQa-007.

---


## BQa-007 — Actor-local pressure interpretation and stake projection

Project objective/derived pressure into the **specific pressure an actor can legitimately experience** before any goal is formed.

The world may contain a true condition that an actor does not know about. Conversely, an actor may hold a sincere false belief that gives them a real reason to act. Goal formation therefore cannot consume `Development` as omniscient truth.

A side-effect-free actor-local reading may use only evidence legitimately available to that actor, such as:

```text
knowledge and belief records
proof/confidence they hold
memories they actually have
relationships / obligations / roles
personal needs and values
owned property / business responsibility
organization membership where the actor knows the relevant matter
explicitly observed local state
```

The projection should answer questions like:

```text
does this actor perceive/believe there is a problem?
what are they personally or institutionally staked in?
which parts are unknown, disputed or merely suspected?
how urgent is it from their point of view?
```

It is a derived view, not another saved pressure store, and it must never teach the actor a fact merely because the objective detector found it.

**Depends:** BQa-006; BQ-017–BQ-022, BQ-064, BQ-145.

**Done when:** one objective pressure produces different actor-local readings for different people; a hidden true fact creates no pressure for an actor who has no legitimate route to it; a false but sincerely held belief can create a real actor-local pressure; and repeated interpretation is deterministic and mutates nothing.

**Do not:** equate authoritative truth with actor knowledge, collapse belief into truth, or make relationship/stake alone reveal the hidden content of a pressure.

**Required false-belief path:** candidate enumeration must include actor-held claims/needs even when the objective detector emits no matching pressure. The current detector filters unproven claims to `TruthState.True`; merely filtering that output cannot implement sincere error. A test with no objectively true matching condition must still produce local concern from an existing false belief. Correcting evidence reaches belief revision through existing knowledge/inference owners; objective truth never silently corrects the actor.


**Authority / proof route:** [owning source and representative tests](systems/state.md#belief-and-proof), [neighbor contract](systems/state.md#character-state), [validation](agent/validation.md#state).

**Sequence:** BQa-006 → BQa-007 → BQa-008.

---


## BQa-008 — Goal condition, provenance and lifecycle contract

Strengthen `NpcGoal` before automatic goal generation so later systems can reason about goals without parsing prose or guessing why they exist.

An automatically evolving goal needs machine-readable answers to at least:

```text
what desired world-state condition would satisfy it?
what actor/matter/subject is it about?
which actor-local pressure or authoritative source caused it?
what state is the goal in now?
```

Preserve `Kind`, `Subject`, weight and inspector-facing reason where useful, but add the smallest durable contract necessary for:

```text
stable deduplication of equivalent goals
source/provenance references
satisfaction tests against authoritative state
abandonment
supersession/substitution
retirement without deleting history
```

`Reason` remains an explanation for people, not an input language the simulation parses. Lifecycle/provenance must survive save/reload and use the repository's normal migration discipline if the persisted schema changes.

**Depends:** BQa-001, BQa-006, BQa-007; BQ-062, BQ-105, BQ-106.

**Done when:** equivalent actor-local pressure cannot accumulate duplicate goals across repeated passes; a goal can be distinguished as active, satisfied, abandoned or superseded (or an equivalent explicit lifecycle); its desired-state condition and causal source survive save/reload; and no production decision depends on parsing the human-readable `Reason` string.

**Do not:** turn goals into quest objectives, store a copy of the pressure itself, or rewrite old history when a goal is retired.

**Persistence and extension refinement:** condition evaluators use a validated typed/registered vocabulary and concrete bindings, not arbitrary expressions or a central goal-name switch. Unknown legacy goals remain inspectable unsupported desires, never guessed actions. Store only needed lifecycle/provenance/actor-assessment fields with explicit old-save defaults and no event replay; bound active goals and preserve meaningful retired-goal history without one durable record per unchanged evaluation. Objective satisfaction and the actor's justified assessment remain distinct, as required by BQa-011.


**Authority / proof route:** [owning source and representative tests](systems/state.md#character-state), [neighbor contract](systems/integration.md#persistence), [validation](agent/validation.md#persistence).

**Sequence:** BQa-007 → BQa-008 → BQa-009.

---


## BQa-009 — Pressure-to-goal evolution

Generalize actor-local goal formation so goals increasingly arise from **actor-interpreted current pressure** and character state rather than being assigned only when a situation fixture is established.

Use BQa-007's actor-local pressure view and BQa-008's machine-readable goal contract. Existing personality, values, needs, prohibitions, relationships and problem-solving tendencies remain the authorities that make two actors react differently to the same perceived condition. The pressure detector itself never chooses a goal.

Support:

```text
goal creation
goal reweighting
goal satisfaction
goal abandonment
goal substitution
goal escalation
counter-goal creation
```

Examples:

```text
property believed stolen
→ recover property / replace it / abandon claim

public accusation the actor learns about
→ clear name / intimidate witness / leave town / reconcile

business shortage the actor is responsible for
→ procure supply / borrow / pressure supplier / close temporarily

failed revenge
→ retaliate / recruit help / give up / reconcile
```

Different actors facing the same objective pressure may form different goals because their **knowledge/beliefs/stakes differ first**, and because values, relationships, personality, resources and prohibitions differ after that.

**Depends:** BQa-006, BQa-007, BQa-008; BQ-056–BQ-064.

**Done when:** repeated production goal-evolution calls over a month of supplied time/state changes create, revise, abandon/supersede and satisfy goals from authoritative state transitions without fixture-authored follow-up goals being required for the tested chains; hidden truth alone never creates an actor goal; and a false but sincerely held belief can create a goal when the actor-local pressure view supports it.

**Do not:** let the pressure detector choose the goal, make goals global story directives, or create goals from omniscient access to authoritative truth.

**Boundary:** this step proves the goal owner, not a scheduler or an autonomous month; the integrated headless autonomous proof belongs to BQa-016, followed by BQa-017 live integration. Pin different decisions from differing values/prohibitions with knowledge held constant, and different knowledge with character held constant. Cap/reweight competing desires and preserve a legitimate wait/abandon result rather than escalating every unfulfilled goal.


**Authority / proof route:** [owning source and representative tests](systems/state.md#character-state), [neighbor contract](systems/actions.md#developments), [validation](agent/validation.md#state).

**Sequence:** BQa-008 → BQa-009 → BQa-010.

---


## BQa-010 — Registered action semantic-effect contract

Give each reusable `NarrativeAction` a side-effect-free machine-readable description of the **kinds of state change it can potentially advance** and the semantic bindings/context it requires.

Current declarations such as `Family`, `ActorScope`, `Embodiment` and `SettlesMatters` remain useful but are too coarse for generic goal discovery. The new contract should express potential effects at the semantic level, for example:

```text
transfer possession / return property
obtain or reveal information
change access / location / safety state
satisfy a resource need
alter an obligation or debt
change relationship/standing pressure
remove/create evidence
restrain/release/protect a person
repair/damage a relevant object or route
```

The exact vocabulary should be small and derived from real registered verbs, not designed as an exhaustive world ontology in advance.

This is **potential capability metadata**, not outcome prediction. `GetAvailability` still decides whether the verb makes sense now. `Perform` plus checks/native writes still decide what actually happens. Failure may produce a different effect than success. `ActionBinding` continues to carry the concrete proposition/item/destination/purpose.

**Depends:** BQa-005, BQa-008; BQ-093, BQ-137.

**Done when:** representative action families expose effect descriptors through the registry without a parallel hand-maintained action table; those descriptors are sufficient for generic goal matching; adding a new verb with an existing effect vocabulary requires no central goal-name switch; and effect inspection mutates nothing.

**Do not:** infer effects from labels/prose, make effect metadata execute consequences, duplicate availability rules, or promise that a potential effect will occur merely because an action is attempted.

**Required coverage:** register effects for the real verbs needed by property, economic/resource, information, protection and obligation goals; expose missing coverage explicitly. Candidate binding search is bounded and reads actor-accessible possibilities. Metadata must name potential effects and required evidence, including delegated/refused operations, rather than advertising every illustrative effect listed here.


**Authority / proof route:** [owning source and representative tests](systems/actions.md#actions), [neighbor contract](systems/actions.md#availability), [validation](agent/validation.md#actions).

**Sequence:** BQa-009 → BQa-010 → BQa-011.

---


## BQa-011 — Goal satisfaction and action-semantic bridge

Bridge machine-readable desired conditions to effects declared by registered actions. Construct concrete candidate bindings from actor-accessible targets, resources and information; rank using the existing goal/personality choice authority. Reuse `ActionRegistry`, `ActionBinding`, `ActorContexts` and `ActionAttempt`, including a legitimate null/wait result when nothing matches.

Replace `OffScreenSchemes` goal-kind substring matching and its current success-plus-any-event satisfaction shortcut for the covered goals. A successful conversation is not recovery of property. A goal may become objectively satisfied through another actor's action, yet its owner cannot change plans on hidden truth alone: distinguish objective condition evaluation from the actor's belief that the condition is met. Unknown native state is not failure or satisfaction. Keep stale intent until legitimate learning/review justifies revision, with execution still revalidating real preconditions.

**Depends:** BQa-008, BQa-009, BQa-010.

**Done when:** recovery, information and obligation/resource conditions discover multiple semantically appropriate registered routes where available; a successful irrelevant action does not satisfy a goal; another actor's deed can satisfy its objective condition without teaching the owner; false-belief goals remain actionable without requiring their premise to be objectively true; missing effects/bindings yield explicit unsupported/wait rather than an unrelated fallback; and save/reload preserves these distinctions. Adding a verb using an existing effect needs no central goal-kind switch.

**Threadless binding:** a goal/action pair must execute against its owned subject/resources without a `NarrativeThread` when its semantics do not require one. Do not mint a matter solely to make `ActionContext` valid. Test the same supported condition with and without an established matter.

**Communication bridge:** supported information/reporting goals must discover an actor-to-actor semantic communication action through the same registry. Reuse existing disclosure, speech-act, belief and commitment owners; actual receipt is causal even when no storylet is running. Rendering a conversation is not the only way NPCs can communicate. Pin refusal, sincere error and correction without an optional scene supplying the facts.

**Do not:** parse `Kind`, `Reason` or display labels as free text; mistake a registered stable vocabulary key for prose; let action metadata execute effects; make objective completion an omniscient actor notification.


**Authority / proof route:** [owning source and representative tests](systems/actions.md#actions), [neighbor contract](systems/world.md#autonomy), [validation](agent/validation.md#actions).

**Sequence:** BQa-010 → BQa-011 → BQa-012.

---


## BQa-012 — Consequence-to-pressure feedback and observation intake

Connect real state changes back to the derived work set. Actions/native observations record what occurred; `ConsequenceEngine` retains reaction ownership; facts, beliefs, businesses, demand, obligations, travel and relationships retain their own mutations. The detector reads these owners and never chooses goals.

Cover property/crime, business/service and social-belief changes, including state changes with no thread. For each, name the live observation or BQ-owned transition that supplies it, the legitimate notice/report route, and its effect on later pressure. Extend `VanillaActionRecorder` or existing bounded reconciliation only for outcomes actually readable; do not infer production, theft, witnesses or service failure from a generic act callback. An asleep/off-shift/unknown operator is not a failed business. Coarse BQ business continuity can progress without pretending to control vanilla stock or schedules.

Pressure invalidation must cover non-event-owned changes as well as event listeners, and wake affected actors/organizations even if they have no goal yet. A bounded rotating inspection covers elapsed-time conditions and missed invalidations without scanning historical actors every frame.

**Depends:** BQa-001, BQa-006–BQa-011; BQ-014, BQ-015, BQ-050, BQ-051.

**Done when:** production transitions in all three families alter the next derived pressure and local readings; supplier loss changes an existing business/demand record and can affect a customer, worker or rival beyond that business; undetected property change does not fabricate a culprit or witness; legitimate later notice/report can wake a response; unrelated actors remain unaware; quiet/sleeping-service controls create no distress; and replay/reconciliation does not duplicate native effects. Tests arrange initial state and observations only, never inject follow-up goals or incidents.

**Do not:** add a second truth store, a goal engine inside `ConsequenceEngine`, routine consumption simulation, or rely on a situation constructor to supply missing causes.


**Authority / proof route:** [owning source and representative tests](systems/actions.md#reactions), [neighbor contract](systems/flow.md#live-host-joins), [validation](agent/validation.md#world).

**Sequence:** BQa-011 → BQa-012 → BQa-013.

---


## BQa-013 — Failure outcomes that preserve causal meaning

Audit the high-leverage registered action families used by the beta chains against actual postconditions. Classify failed attempts as no material change, information revealed, resource/time cost, harm, or a justified transformation of later options. Preserve the successful outcome contract as well: an event label cannot substitute for the state change it claims.

Failed sabotage may expose interference; a caught lie may affect trust; a failed transfer can simply leave ownership unchanged. Which happens depends on the attempted act, observers and its check outcome, never a rule that every failure must add drama. Audit native refusal separately from a performed-but-unsuccessful act.

**Depends:** BQa-005, BQa-010–BQa-012.

**Done when:** every action family selected for the property, economic and false-belief chains has classified success/failure/refusal postconditions; production tests prove at least one failure changes a later pressure/option and one legitimately changes nothing; unsupported native writes cannot be narrated as successful actions; and hidden failures cannot create witnesses or trust loss without a knowledge route.

**Do not:** force complications, count a `failed` label as sufficient simulation, or use a failed capability probe as an in-world deed.


**Authority / proof route:** [owning source and representative tests](systems/actions.md#reactions), [neighbor contract](systems/actions.md#actions), [validation](agent/validation.md#actions).

**Sequence:** BQa-012 → BQa-013 → BQa-014.

---


# Phase BQa-L2 — A running world with legitimate agency


## BQa-014 — Spatial and temporal opportunity model

Upgrade current activity observation from a loose weighting input into a richer action-opportunity reading without simulating a parallel schedule system.

Separate two evidence modes explicitly:

```text
observed local opportunity
→ may use verified co-location, witnesses, privacy, object presence and active-zone state

coarse off-screen opportunity
→ may use only evidence actually available off screen; cannot invent exact meetings, witnesses or physical observation
```

An action may care about:

```text
co-location
privacy
witnesses
target availability
object accessibility
work/service state
routine/activity hints
travel state
danger
elapsed time
```

Unknown remains unknown. A coarse opportunity score can make an attempt more or less plausible without turning plausibility into a claim that two actors physically met or that somebody witnessed an act.

**Depends:** BQa-011, BQa-012; BQ-135.

**Done when:** the same actor and same goal produce different legal/plausible opportunities because observed Elin state changed; every refusal/bonus names the observation behind it; and an equivalent off-screen run never upgrades unknown co-location/witness information into authoritative physical evidence.

**Do not:** build a parallel schedule simulator, treat coarse activity hints as exact location history, or use opportunity weighting to manufacture witnesses.

**Commit boundary:** implement the pure opportunity contract over production `ActorContexts` before the recurring host. Model an eligible coarse communication through an established contact/report channel without claiming a precise meeting; unknown exact co-location still refuses actions that require it. Cover Active/Warm/Cold changes, inventory removal and vanilla movement ownership. Live evidence is required for any newly claimed native observation, not for semantic coarse eligibility.


**Authority / proof route:** [owning source and representative tests](systems/world.md#activity), [neighbor contract](systems/actions.md#availability), [validation](agent/validation.md#native).

**Sequence:** BQa-013 → BQa-014 → BQa-015.

---


## BQa-015 — Action competition and transient claims

Allow multiple actors to respond to the same pressure or compete over the same indivisible opportunity.

Possible relations:

```text
cooperate
race
interfere
exploit
conceal
investigate
profit
resolve
worsen
```

Add only the transient claim/reservation needed to prevent a coarse scheduler from letting several actors simultaneously complete the same exclusive opportunity.

Claims do not become authoritative history until an action actually occurs.

The player participates under the same matter state rather than a privileged parallel resolver.

**Depends:** BQa-009, BQa-011, BQa-014.

**Done when:** one pressure attracts several plausible actors and final outcomes vary according to motive, timing, opportunity and successful action rather than stable iteration order.

**Required arbitration contract:** gather intentions from one immutable batch input, group by contested object/resource/opportunity, then choose with deterministic stable keyed tie-breaking independent of collection enumeration; ties may vary by seed/time, not by dictionary order. Revalidate each winner against current state immediately before execution. Claims expire on refusal, exception, cancellation and batch end; save only committed outcomes, never reservations. Test one object/two actors, player-versus-NPC conflict, loser retry, reordered candidate input and reload at a boundary. Core arbitration is consumed by BQa-016 before BQa-017 supplies its live host.


**Authority / proof route:** [owning source and representative tests](systems/actions.md#actions), [neighbor contract](systems/world.md#autonomy), [validation](agent/validation.md#world).

**Sequence:** BQa-014 → BQa-015 → BQa-016.

---


## BQa-016 — Core production-cycle runner

Establish one bounded Core cycle that coordinates the existing owners. The Lab runner must call
this production implementation through `ProductionSystemRegistry`, not reproduce its scheduling
logic in a fixture. Plugin integration follows in BQa-017; this step leaves existing live hooks
unchanged while proving the Core contract they will consume.

```text
supplied authoritative observations/reconciliation inputs
→ bounded affected work set
→ derived pressures and actor-local interpretation
→ goal evolution and feasible intentions
→ opportunity/arbitration through existing owners
→ shared attempts and consequences
→ changed state for the next defined causal pass
```

Define the batch boundary, work budgets, fairness and execution ownership before wiring native
callbacks. Immediate event listeners retain their queued reaction behavior; they cannot recursively
start another pressure/goal/action pass. Reuse the simulation index and existing autonomy, scheme,
adventurer and travel owners. Define and test deduplication here so one actor cannot spend the same
exclusive opening through multiple passes. Native outcomes supplied as already observed must not
be simulated again. Threadless conditions and newly eligible actors with no staged goals must enter
bounded evaluation. No presentation or Director becomes the world scheduler.

This step owns consumed opening/batch markers, deterministic queue reconstruction and old-save
defaults/migrations where needed. Unselected work must remain eligible without starvation; replaying
an already-consumed interval must be harmless. Remove permanent autonomy protection merely because
the player once acted in a matter; only an active, bounded interaction may temporarily defer a
conflicting attempt. Transient claims expire through BQa-015's existing arbitration contract.

**Depends:** BQa-001–BQa-015; BQ-093–BQ-098, BQ-107, BQ-108.

**Done when:** the shared production runner evolves all three core pressure families for 30+ days
from authoritative initial conditions with no injected follow-up goals or incidents. It covers
ignored and previously engaged matters, no running storylet, periods with no live thread, supplied
off-screen observations and save/reload midway. Tests prove bounded per-pass work, fair resume,
stable replay, no same-stack causal avalanche, no duplicate exclusive openings across existing
subsystem passes and no replay of supplied native outcomes. Lab invokes this exact Core cycle;
headless success is explicitly not evidence that Elin hooks advance it. BQa-040 later expands range
measurement rather than providing the first integrated causal proof.

**Do not:** add a Lab-only scheduler, poll every actor, make generation mandatory on every pass,
persist transient reservations, or claim unloaded native observations from headless doubles.

**Authority / proof route:** [autonomy and tier owners](systems/world.md#autonomy),
[current host boundaries](systems/flow.md#live-host-joins),
[Lab production registry](../tools/BrilliantQuesting.Lab/ProductionSystemRegistry.cs),
[persistence](systems/integration.md#persistence), [validation](agent/validation.md#world).

**Sequence:** BQa-015 → BQa-016 → BQa-017.

---

## BQa-017 — Plugin recurring-cycle integration

Connect BQa-016's exact Core runner to real Elin observation, time, zone and save/load boundaries.
The Plugin supplies current native inputs and invokes the cycle; it must not recreate Core batching,
fairness, deduplication, goal selection or action resolution. Preserve the established Core semantics
and their headless proofs while replacing overlapping live calls.

Audit `AdvanceThreads`, autonomy/scheme/adventurer/travel calls, attach/day advancement and zone
reconciliation as one call-site handoff. Remove or route overlapping calls through the shared owner
so hook repetition cannot spend an opening twice. Reconciliation must occur before consuming
elapsed work and must respect vanilla catch-up, active-zone embodiment and movement ownership.
Read unavailable native state as unknown; do not infer that Elin stopped simulating an unloaded area.

Restore the runner using BQa-016's persisted markers and transient service/queue reconstruction;
do not introduce a competing Plugin clock or saved cursor. Guard callback failures so one refused
operation cannot break the game or replay completed work. Expression, UI callbacks and debug
telemetry cannot be required to advance the world.

**Depends:** BQa-016; BQ-093–BQ-098, BQ-107, BQ-108, BQ-110.

**Done when:** the Plugin builds and a real save demonstrates recurring advancement during ordinary
play, resting and travel, through zone leave/return and save/reload. Record the actual hook/time and
reconciliation evidence, including advancement while the player ignores BQ and no storylet runs.
Repeated/overlapping callbacks and a previously consumed interval do not duplicate actions or
vanilla catch-up. The same Core cycle and persisted markers are used by Plugin and Lab; the Core
30-day/reload proofs remain green. Missing capabilities and callback failures degrade safely,
without a new goal engine or a second scheduler. Headless tests alone do not complete this step.

**Do not:** copy the runner into the Plugin, leave old scheduling calls active beside it, promote
headless timing to runtime evidence, or rerun physical work already performed by Elin.

**Authority / proof route:** [live host joins](systems/flow.md#live-host-joins),
[Plugin](../src/BrilliantQuesting.Plugin/BrilliantQuestingPlugin.cs),
[native evidence](elin/capabilities.md), [validation](agent/validation.md#native).

**Sequence:** BQa-016 → BQa-017 → BQa-018.

---

## BQa-018 — Organization pressure interpretation and goal evolution

Give organizations a legitimate way to notice pressures and revise organization goals before the live Plugin schedules organization activity.

Organizations do not need personality profiles, but they also must not be omniscient. Their interpreted pressure may come from explicit institutional channels such as:

```text
knowledge held/reported by relevant members or leaders
organization-owned sites/resources/obligations
standing policy / authority role
member harm or family/crew stakes
recorded supply, loss or opportunity affecting the group
facts the organization has legitimately received through an existing information route
```

Use the existing organization state and `OrganizationGoal` authority where possible. Add only the smallest machine-readable provenance/lifecycle/desired-state support necessary to let an organization goal form, change and retire for the same causal reasons individual goals do.

An organization may act on policy and shared institutional state without pretending that every member knows every fact. Where knowledge must propagate through a member/report route, preserve that distinction.

**Depends:** BQa-001, BQa-006–BQa-009, BQa-017; BQ-053, BQ-054.

**Done when:** at least three organization types derive different legitimate responses to current pressures; an organization cannot react to a hidden fact merely because Core knows it; organization goals form/revise/retire from inspectable institutional evidence; and repeated interpretation is deterministic and side-effect-free until the normal goal owner records a change.

**Do not:** create an omniscient collective mind, synthesize fake spokesperson NPCs, or duplicate individual relationship/knowledge systems wholesale onto organizations.

**Required information contract:** an organization-owned loss is observable only through an identified accounting, member or holding observation channel; ownership and role alone reveal no hidden culprit or private belief. Reuse knowledge records with explicit institutional recipient/report provenance where valid; do not union every member's private knowledge. Reports may be false. This step owns any institutional receipt and goal-condition/lifecycle persistence with old-save defaults. Tests cover unreported member knowledge, reported error, corrected report and loss of the responsible member.


**Authority / proof route:** [owning source and representative tests](systems/world.md#organizations), [neighbor contract](systems/state.md#belief-and-proof), [validation](agent/validation.md#persistence).

**Sequence:** BQa-017 → BQa-018 → BQa-019.

---


## BQa-019 — Organization intention execution through shared semantics

Adapt existing `OrganizationActivity` before it becomes live. Reuse BQa-010/BQa-011 condition/effect discovery and BQa-015 resource competition. Institutional operations retain their organization state owner; represent their effects, prerequisites and costs explicitly. Embodied deeds go through an eligible real member's shared `ActionAttempt`; bookkeeping has no synthetic NPC body and no invented physical presence.

Replace current unknown-goal and failed-recruit/protection fallbacks to `BuildWealth`. Unsupported intent waits/refuses. Reserves require a named BQ-owned inflow/cost rather than free resource creation per tick; recruitment needs eligible, legitimately reachable participants, consent/authority as applicable and sufficient resources. Bound work and test leader loss, depleted resources, shared member contention and goal satisfaction against actual state.

**Depends:** BQa-010, BQa-011, BQa-015, BQa-018.

**Done when:** three organization kinds with existing legitimate state execute different supported responses through production Core; unknown/blocked goals do not earn wealth; members and resources cannot be spent twice; a false institutional report can motivate an action without becoming truth; actual consequences affect individual or rival pressures; and save/reload cannot repeat an operation.

**Do not:** create a parallel organization action ontology, economy, hidden member or native guild authority. This step adapts execution; live enrollment/host wiring follows in BQa-020.


**Authority / proof route:** [owning source and representative tests](systems/world.md#organizations), [neighbor contract](systems/actions.md#actions), [validation](agent/validation.md#world).

**Sequence:** BQa-018 → BQa-019 → BQa-020.

---


## BQa-020 — Live organization agency

Bring existing organization simulation into the same bounded production cycle rather than leaving it stronger in Core/Lab than in the Plugin.

BQa-018 decides what an organization can legitimately notice and what machine-readable organization goal follows. BQa-019 already owns execution semantics. This step enrolls legitimate production groups and schedules their eligible intentions through that tested owner, routing resulting changes through the same authoritative consequence/world-state owners used elsewhere.

Examples include guards investigating, guilds supporting supply, families protecting members, adventurers taking rescue work, authorities sanctioning, and caravans rerouting around danger.

Organizations do not need personality profiles or synthetic spokesperson NPCs to act as organizations. Where a physical/member action is required, use a legitimate member/actor seam rather than inventing an invisible body.

**Depends:** BQa-017, BQa-018, BQa-019.

**Done when:** at least three organization types independently notice and act on pressures through the live Plugin production cycle; equivalent headless runs use the same Core authority; organization actions can create ordinary authoritative consequences/pressures; and no organization acts on information BQa-018 says it could not know.

**Do not:** create synthetic "guild NPCs," a second economy, organization-only consequence rules, or an omniscient organization scheduler.

**Live enrollment boundary:** first establish how production obtains organizations at all. Reuse existing explicitly generated groups or verified membership/holding observations with provenance and deduplication; do not depend on Lab-created crews, equate vanilla guild IDs with BQ-owned wealth, or infer institutions from a job token. This commit wires already-tested execution and enrollment; it does not redesign organization actions. Its three-type live acceptance may use safely established BQ groups, but their later reports/goals/actions cannot be fixture scripts.


**Authority / proof route:** [owning source and representative tests](systems/world.md#organizations), [neighbor contract](systems/flow.md#live-host-joins), [validation](agent/validation.md#native).

**Sequence:** BQa-019 → BQa-020 → BQa-021.

---


# Phase BQa-L3 — Recurring situations from existing causes


## BQa-021 — General situation proposal ecology

Broaden the producer side of the existing side-effect-free proposal/conservation seam.

Current proposal selection already knows how to compare reuse and hypothetical creation. BQa now needs multiple **pressure-derived proposal producers** feeding it.

A proposal may combine existing:

```text
pressure
actor-local/organization stakes
actors
facts
relationships
obligations
organizations
sites
resources
goals
known opportunities
```

and explicitly state what new actors, premises or sites would be required before anything is created.

Proposal production is read-only. It may describe requirements and expected bindings, but it does not satisfy them; BQa-022 owns fulfillment after a proposal has actually won selection/admission.

At minimum, generic live proposal production must no longer be effectively limited to one settlement pressure family.

**Depends:** BQa-006–BQa-020; BQ-103, BQ-152.

**Done when:** several distinct situation families can be proposed from the same settlement/world pass, ranked through the existing proposal/director authorities, and inspected without mutating state; proposals with hypothetical requirements remain pure descriptions until BQa-022.

**Do not:** create a universal quest generator, let proposal ranking establish authoritative state, or satisfy hypothetical requirements during scoring.

**Founding incident boundary:** existing `SettlementSituationGenerator.TryGenerateSelected` performs the theft transfer before creating its matter. Do not generalize that behavior as recognition: recurring BQa producers must bind a theft already committed by a native or registered autonomous action. Separate explicit bootstrap/staging from ongoing recognition. A proposed new actor, site or premise must have an independently justified current establishment purpose; it cannot invent the theft, shortage, witness, backstory or motive needed to make its own proposal valid. Enumerate supported property, business and social/institutional producers over shared state, including threadless conditions; each has stable cause/binding identity and revalidation requirements.


**Authority / proof route:** [owning source and representative tests](systems/world.md#generation), [neighbor contract](systems/actions.md#developments), [validation](agent/validation.md#world).

**Sequence:** BQa-020 → BQa-021 → BQa-022.

---


## BQa-022 — Selected-proposal fulfillment and atomic establishment

Close the seam between a side-effect-free winning proposal and the authoritative state that proposal explicitly says it still requires.

BQ-152/BQ-103 allow proposals to compare reuse against hypothetical new actors/premises without creating them. Current live owners correctly refuse requirements they cannot satisfy. This step defines **who may fulfill a selected proposal and how that happens safely**.

Target contract:

```text
read-only proposals
→ ranking/admission chooses one
→ selected proposal returned to its owning generator
→ owner validates every declared requirement/capability/budget
→ owner prepares required actors/premises/sites through existing authorities
→ atomic establishment commits the situation and its new state
```

Only an admitted/selected proposal may be fulfilled. The Director never becomes a spawner. Requirement keys remain proposal-local until the owning generator turns them into real stable entities/state.

If any required creation or native capability cannot be satisfied, the establishment fails closed. Do not leave behind half-created actors, facts, sites, thread slots or inventory changes. Preflight alone does not make native multi-write operations atomic. For beta establishment, use a Core atomic commit or an operation with verified compensation/reconciliation; refuse unsupported multi-write creation before the first write. Never rewind published IDs: failed execution may burn an ID without creating a world entity, while rejected read-only ranking must allocate none. If a native operation can leave ambiguous physical results, it is not admitted to the atomic establishment path until recovery is proved.

**Depends:** BQa-021; BQ-103, BQ-152.

**Done when:** a proposal requiring at least one new declared requirement can be selected and fulfilled through its existing owner; a mixed reuse/create proposal preserves stable bindings; rejected proposals allocate nothing; a deliberately failed fulfillment leaves gameplay state unchanged, with only explicit diagnostic evidence and monotonically consumed allocation IDs permitted; and successful establishment is replay/save stable.

**Do not:** create actors/premises during proposal scoring, let the Director directly mutate the world, silently drop unmet requirements, or approximate a failed creation with a different undeclared premise.

**Scope and proof:** establish a matter around existing causes, optionally creating one independently warranted BQ-owned record (for example a new commission/obligation), with all declared bindings validated. Do not require new physical NPC/site spawning to pass. No constructor may mint a fictional historical incident. Fault-inject before/after each permitted commit boundary, including reentrant listeners and stale ownership; show no partial durable state or unaccounted native effects, and retry idempotently. Successful creation emits its own present-time provenance, never backdates a premise. This step owns any durable establishment identity/defaults; staging tables and requirement keys stay transient.


**Authority / proof route:** [owning source and representative tests](systems/world.md#generation), [neighbor contract](systems/integration.md#native-boundary), [validation](agent/validation.md#persistence).

**Sequence:** BQa-021 → BQa-022 → BQa-023.

---


## BQa-023 — Cross-matter situation composition

Allow distinct matters to share actors, facts, obligations, goals, organizations, sites or resources without merging into one giant quest graph.

Examples:

```text
debtor + caravan
→ debtor joins caravan
→ caravan fails
→ creditor now has reason to locate debtor

shortage + theft pressure
→ theft opportunity becomes more attractive

romantic tie + witness
→ disclosure incentives change

guild membership + accusation
→ institutional response becomes possible
```

Threads remain distinct matters sharing authoritative state.

**Depends:** BQa-012, BQa-015, BQa-021, BQa-022.

**Done when:** one causal history crosses at least two independently established matters and each matter remains separately inspectable while sharing state.

**Acceptance refinement:** one committed shared-resource or shared-fact change must affect both matters without duplicate rewards, knowledge, goals or effects; resolution of one cannot resolve the other automatically or lock the shared actor forever. Include a new matter after a previous matter has resolved, and preserve causal links without merging histories. Merely sharing a participant ID is not cross-matter gameplay.


**Authority / proof route:** [owning source and representative tests](systems/actions.md#threads), [neighbor contract](systems/actions.md#reactions), [validation](agent/validation.md#world).

**Sequence:** BQa-022 → BQa-023 → BQa-024.

---


## BQa-024 — Recurring situation discovery and director admission

Replace bootstrap-only live situation creation with bounded recurring discovery.

The world-development cycle should periodically produce/read side-effect-free situation proposals from **changed world state**, then pass them through the existing attention, diversity, repetition and conservation authorities.

Target:

```text
changed state
→ pressures/goals/opportunities
→ situation proposals
→ director/admission
→ selected proposal owner
→ atomic requirement fulfillment where needed
→ situation establishment
→ later consequences
→ changed state
```

The Director/admission layer curates **which proposal is worth establishing or surfacing**. It does not schedule NPC motives/actions, create actors, satisfy requirements, or become a universal world scheduler. BQa-022 and the selected proposal's owning generator own only fulfillment and establishment; autonomous motives/actions remain with BQa-008–BQa-020's owners.

Remove "the world already has any thread" as a global reason no future situation can ever emerge. Existing live matters, attention budgets, repetition rules, locality and simulation tier should instead govern admission.

Generation must not become a function of repeatedly entering/leaving a zone to reroll.

**Depends:** BQa-017, BQa-021–BQa-023; BQ-099–BQ-103.

**Done when:** both the shared production runner and a live run can establish an initial matter, resolve or evolve it, then later admit a causally justified different matter from the resulting state without reload tricks or manually invoking an archetype constructor; rejected proposals create nothing; and the Director never directly spawns/edits authoritative world state.

**Do not:** guarantee that every pass creates content, allow unlimited simultaneous matters, bypass the owner responsible for committing a selected proposal, or turn director scoring into NPC decision-making.

**Live origin proof:** start from ordinary enrolled actors and native/BQ state with debug staging off. At least one initial incident must come from a real observation or supported registered autonomous action; subsequent recognition must not call a fixture constructor to found it. A quiet control with no actionable pressure is allowed to stay quiet.

**Admission boundary:** separate simulation capacity/conservation from player exposure preference. Reuse existing attention authorities, but presentation disable/engagement/quiet preferences cannot suppress motives, consequences or causal continuation. A matter capacity refusal leaves its source pressure actionable; threadless action remains valid. Stable proposal identity derives from causes and bindings, with bounded retries, capacity, revalidation and fair inspection across zones. Never globally suppress a cause forever after resolution: a materially new episode can produce a distinct matter. Same unchanged state plus save/reload/zone entry grants no fresh roll, RNG draw or duplicate establishment. Extend the production runner to compare expression-off/on authoritative projections and later re-enable discovery.


**Authority / proof route:** [owning source and representative tests](systems/world.md#generation), [neighbor contract](systems/expression.md#attention), [validation](agent/validation.md#world).

**Sequence:** BQa-023 → BQa-024 → BQa-025.

---


## BQa-025 — Dynamic escalation and de-escalation

Reduce reliance on authored `day +N` escalation while preserving useful existing thread scheduling infrastructure.

Tension should change because the world changed:

- problem persists;
- new harm occurs;
- more people learn;
- evidence disappears;
- authority becomes involved;
- actors reconcile;
- disputed resource disappears;
- distance grows;
- a goal becomes impossible;
- a substitute becomes available.

Elapsed time can itself become a legitimate pressure or eligibility condition. Existing `ThreadEngine`/scheduled checkpoints therefore do not need to be deleted; they should increasingly **ask what the world is like at the checkpoint** rather than hard-code that escalation must occur merely because N days passed.

Authored escalation templates/storylets may interpret the current state. The world owns why it changed and whether the matter actually escalates, stabilizes, resolves or fades.

**Depends:** BQa-012, BQa-016–BQa-024; BQ-052.

**Done when:** the same matter can escalate, stabilize, resolve or fade under different world-state trajectories without changing its authored storylet definition; a timed checkpoint can legitimately do nothing or de-escalate when state no longer supports escalation; and existing thread lifecycle/scheduling remains one authority rather than being replaced by a second timer system.

**Do not:** delete useful scheduling merely to appear more emergent, encode outcomes directly into elapsed-day thresholds, or let a storylet become the authority for why world tension changed.

**Acceptance refinement:** audit existing archetype handlers invoked by `ThreadEngine`, not just add a new optional policy. Keep deadlines as elapsed-world facts but route any new harm, institutional intervention or transfer through its legitimate owner and prerequisites. A retained timer may make a problem overdue; it cannot author a robbery to sustain a scene. Player inaction is not a penalty trigger; decline adds no bespoke harm even though independent world events continue.


**Authority / proof route:** [owning source and representative tests](systems/actions.md#threads), [neighbor contract](systems/actions.md#reactions), [validation](agent/validation.md#world).

**Sequence:** BQa-024 → BQa-025 → BQa-026.

---


# Phase BQa-L4 — Expression and live interaction


## BQa-026 — Semantic payload contract

A realized speech act must actually communicate the meaning its semantic representation says was communicated.

For proposition-bearing acts:

```text
SpeechAct
→ required semantic payload
→ mandatory semantic core
→ optional discourse framing
→ optional personality/tone
→ realized utterance
```

Unacceptable failures include an accusation that realizes only as hesitation, an inform act that merely says "You should know something," or a refusal of terms that realizes as refusal to answer.

Tone, hesitation, relationship coloring and style may decorate the payload. They may not replace it.

**Depends:** BQa-001; BQ-070–BQ-074, BQ-133, BQ-146, BQ-147.

**Done when:** a generated dialogue corpus mechanically verifies that every proposition-bearing realized act either contains the required payload or participates in a multi-fragment construction that unambiguously communicates it.

**Do not:** solve this by simply adding more fragments.

**Acceptance refinement:** retaining `RealizedLine.Meaning` unchanged is insufficient proof about emitted words. Validate required payload slots/bindings and omission/refusal cases in the compiled corpus, then review representative rendered output for accusation, denial, source attribution and refusal-of-terms. Missing wording fails closed without applying an undelivered speech act. This is a contract/validation commit, not an expansion of content volume.


**Authority / proof route:** [owning source and representative tests](systems/expression.md#realization), [neighbor contract](systems/expression.md#semantic-communication), [validation](agent/validation.md#expression).

**Sequence:** BQa-025 → BQa-026 → BQa-027.

---


## BQa-027 — Discourse realization grammar

Split dialogue realization into explicit discourse functions rather than selecting from one broad compatible pool.

Suggested slots:

```text
payload
preface
stance
qualification
source attribution
relationship coloring
closing
```

Contexts include first-hand claims, hearsay, inference, reluctant disclosure, dangerous/public accusations, private warnings, corrections, denials, conditional offers, refusal of terms, and refusal to answer.

The grammar must continue to accept speaker voice as a **wording constraint only**. For a given assigned `VoiceProfile`, repeated realizations should preserve that speaker's stable linguistic habits while discourse function, mood and relationship coloring can still vary. This step does not create an identity-derived voice table: race, occupation, species, role or archetype must not silently choose how somebody talks.

**Depends:** BQa-026; BQ-074–BQ-079, BQ-142.

**Done when:** discourse role, act semantics, optional style and an assigned stable voice can vary independently without producing semantically incomplete or contradictory lines, and identical semantic input can be worded differently by different assigned voices without changing meaning.

**Do not:** make discourse slots a second speech-act ontology, derive voice from identity labels, or let style manufacture semantic content.

**Acceptance refinement:** reuse existing fragment eligibility, compiler and idiolect controls. The smallest discourse composition grammar must retain an eligible semantic core under repetition pressure and reject contradictory source/commitment framing. Compiler and human sample review supplement, rather than replace, semantic payload checks.


**Authority / proof route:** [owning source and representative tests](systems/expression.md#expression-controls), [neighbor contract](systems/expression.md#content), [validation](agent/validation.md#expression).

**Sequence:** BQa-026 → BQa-027 → BQa-028.

---


## BQa-028 — Stable actor voice assignment

Give recurring actors one stable `VoiceProfile` assignment through existing actor identity and expression vocabulary before any live routed host consumes it. Assignment is independent of race, job, species and scene role. Reuse a versioned deterministic assignment keyed by canonical `EntityId` (not identity facets) only if stability across vocabulary updates is guaranteed; otherwise persist the minimal assigned profile on its existing owner. Do not maintain a parallel character database.

**Depends:** BQa-001, BQa-002, BQa-026, BQa-027; BQ-075, BQ-105, BQ-106, BQ-142.

**Done when:** repeated scenes, actor alias reconciliation, save/reload and an old save give a recurring actor the same valid voice; assignment never consumes simulation RNG; unknown/retired vocabulary degrades without re-rolling the actor; different voices can express the same semantic payload; and any new stored assignment has migration/default coverage.

**Do not:** infer personality from identity labels, persist rendered dialogue, or leave persistence to the later live host.


**Authority / proof route:** [owning source and representative tests](systems/expression.md#expression-controls), [neighbor contract](systems/state.md#identity), [validation](agent/validation.md#persistence).

**Sequence:** BQa-027 → BQa-028 → BQa-029.

---


## BQa-029 — Incremental storylet execution and delivery acknowledgment

Refactor the current whole-scene `StoryletRouter.Play` behavior behind a resumable execution seam suitable for a live Elin conversation host.

A native interaction cannot safely assume all beats occur synchronously. The execution contract should be able to advance one meaningful boundary at a time:

```text
begin/revalidate opportunity
→ prepare next beat and actor decision
→ expose line/action/intersection to host
→ await native delivery or player choice where required
→ acknowledge what was actually delivered/chosen
→ resolve check/action/consequence for that boundary
→ choose/revalidate next route
→ repeat or stop
```

No later beat may apply its consequences merely because an earlier UI node was opened. Player knowledge changes only through an existing legitimate delivery/observation route; closing/cancelling a UI surface is not delivery acknowledgment.

This step should **not** create a second router. Keep the existing headless `Play` API as a convenience loop over the same incremental mechanism so seeded headless routing and live routing cannot drift semantically.

The resumable execution object is transient session state, not long-term narrative authority. BQa-032 handles leaving/reloading by rediscovering from semantic world state rather than serializing a beat cursor.

**Depends:** BQa-001, BQa-026–BQa-028; BQ-083, BQ-146.

**Done when:** the same seeded storylet produces equivalent routes/consequences under the refactored headless whole-play and repeated incremental advancement, with identical acknowledgment/input schedules; a live-like harness can pause between every beat/input boundary; cancelling before acknowledgment does not teach the player or pre-apply later consequences; and route requirements are revalidated before each resumed advance.

**Do not:** persist a beat cursor as world truth, duplicate storylet routing for the Plugin, or equate `RealizedLine` creation with successful native delivery.

**Execution refinement:** classify semantic communication versus an independent world action. NPC-to-NPC communication resolves through a legitimate production action/channel even with rendering disabled; delivery acknowledgment for the player remains mandatory. A purely expressive NPC scene may present those committed interactions, not execute them again or schedule extra autonomous deeds solely because rendering is enabled. Actual player communication/intervention is a new world input and can legitimately change the comparison. Storylet consequences may record the speech actually conveyed or dispatch the shared action, but may not emit a generic `WorldEvent` claiming unperformed harm/transfer. Ending a route cannot resolve a matter unless authoritative resolution conditions hold. Pin checks once, preserve prepared meaning across acknowledgment, reject duplicate/stale tokens, and release claims on cancellation. Preparation must not consume simulation randomness twice after a failed render. This refactors one router; no independent scene scheduler is introduced.


**Authority / proof route:** [owning source and representative tests](systems/expression.md#storylets), [neighbor contract](systems/integration.md#discovery), [validation](agent/validation.md#expression).

**Sequence:** BQa-028 → BQa-029 → BQa-030.

---


## BQa-030 — Executable player intersections

Convert declarative player intersections into real gameplay interruption points.

Examples:

```text
question
persuade
lie
search
compare testimony
report
return item
invoke authority
intervene physically
decline / walk away
```

The player can enter an ongoing matter without becoming its mandatory protagonist. NPC agency continues when the player does nothing.

Every presented intersection is a projection, not a reserved outcome. On click/selection, rebuild/revalidate the current `ActionContext`, semantic binding, actor scope, availability and required capability exactly as contextual actions already do. If the world changed while the UI was open, fail closed or refresh rather than executing stale state.

**Depends:** BQa-005, BQa-011, BQa-015, BQa-029; BQ-134, BQ-137.

**Done when:** the same authoritative starting state can proceed through NPC-only, player-intervened and player-ignored branches with all three returning consequences to the same world loop; stale intersections cannot execute after their preconditions disappear; and declining/closing the interaction does not grant hidden knowledge or freeze NPC continuation.

**Do not:** reserve an outcome merely because an option was rendered, create player-only duplicate verbs, or make player involvement mandatory for matter progression.

**Commit boundary:** establish the Core executable intersection/response contract and test it through the incremental runner before BQa-031 wires native controls. Reuse `ContextualActionProjection`; no live player-button acceptance is implied by this Core proof. Physical intervention stays subject to normal capability/evidence and native ownership.


**Authority / proof route:** [owning source and representative tests](systems/actions.md#actions), [neighbor contract](systems/expression.md#storylets), [validation](agent/validation.md#actions).

**Sequence:** BQa-029 → BQa-030 → BQa-031.

---


## BQa-031 — Live routed-storylet host

Connect the existing routed storylet engine to actual Elin conversation/presentation surfaces **through BQa-029's incremental execution seam**.

Target:

```text
current development
→ relevant storylet opportunity
→ incremental StoryletRouter session
→ actor-local SpeechAct/action
→ DialogueRealizer with the actor's stable assigned voice
→ native dialogue surface
→ delivery/player acknowledgment
→ action/check/consequence as appropriate
→ world-development cycle
```

There must be no second simulation layer for live presentation. The live host may interpret and expose the current world, but it must not create hidden causes merely because a scene needs material.

Consume BQa-028's already stable voice assignment; this host owns neither its allocation nor its persistence.

**Depends:** BQa-017, BQa-024, BQa-026–BQa-030; BQ-005, BQ-146.

**Done when:** a routed multi-beat storylet is encountered, paused/resumed through real native delivery, and resolved in a live Elin save with no Lab involvement; no later beat is pre-applied before its delivery/input boundary; resulting consequences enter the same authoritative causal loop as NPC-only actions; and the same live actor keeps the same assigned voice across separate encounters in that session.

**Do not:** call the old whole-scene path directly from a native UI hook, teach the player undelivered information, or maintain a second live-only route engine.

**Live proof:** wire `DevelopmentExpression` opportunity search as well as the router; do not require a manually chosen fixture/storylet ID. Exercise real player buttons, two successive beats, failed delivery, death/departure/combat, duplicate callbacks and leaving the surface. Keep the generic `_chara/main` scope and authored-dialogue isolation. Unsupported delivery stops the scene safely while the world cycle continues.


**Authority / proof route:** [owning source and representative tests](systems/flow.md#core-and-lab-expression-joins), [neighbor contract](systems/integration.md#presentation), [validation](agent/validation.md#native).

**Sequence:** BQa-030 → BQa-031 → BQa-032.

---


## BQa-032 — Persistent cross-scene continuation

A storylet play is not a quest-script cursor.

Do **not** persist `beat = 4` as authoritative continuation state.

Persist or derive enough semantic state to rediscover the next relevant interaction:

```text
matter/thread
valid role bindings
meaningful prior acts
commitments
open uncertainty
outstanding consequences
latest development
```

Leaving town ends the immediate scene, not the narrative process. On return the system may produce continuation, consequence scene, callback, changed-circumstances scene, re-cast scene, or nothing.

Stable voice is already owned by BQa-028. This step consumes that assignment and owns only any missing durable semantic continuation/acknowledgment data, with old-save defaults.

**Depends:** BQa-025, BQa-028–BQa-031; BQ-081–BQ-083, BQ-105, BQ-106.

**Done when:** the player can leave mid-matter, advance days, reload, return, and encounter an interaction appropriate to current state rather than a frozen scripted scene; no beat cursor is authoritative; and recurring actors retain deterministic stable voice across scene and reload boundaries without identity-derived assignment.

**Do not:** serialize native UI/session objects, persist a route cursor as world truth, or re-roll a speaker's stable voice every time they are encountered.

**Acceptance refinement:** reload after an acknowledged beat but before its next route cannot repeat commitments, transfers or learning; interruption before acknowledgment cannot silently deliver. Recasting must respect current knowledge/life state and prior participants; declining, leaving and returning can legitimately yield no scene. Content revisions or missing storylet IDs preserve prior semantic history and abandon stale sessions safely.


**Authority / proof route:** [owning source and representative tests](systems/expression.md#storylets), [neighbor contract](systems/state.md#memory-and-continuity), [validation](agent/validation.md#persistence).

**Sequence:** BQa-031 → BQa-032 → BQa-033.

---


# Phase BQa-L5 — Verified physical consequences


## BQa-033 — Native site capability decision and realization

Resolve the remaining live `BuildPlaceStructure`-class capability questions needed for BQ-created sites **without making unsafe native mutation a prerequisite for unrelated causal work**.

Required supported-path live sequence:

```text
create
enter
leave
save
reload
re-enter
disable BQ
re-enable BQ
```

All must behave acceptably before the capability can be promoted.

If the current Elin build cannot safely support the required operation, that is a valid evidence result: mark the operation unsupported/unresolved at the correct grade, make dependent callers fail closed or remain semantic/coarse, and continue unrelated BQa work. Do not force a native write simply to make this step "pass."

**Depends:** BQa-022, BQa-024; BQ-087–BQ-092, BQ-139–BQ-143.

**Done when:** either (A) at least one causally generated BQ site is physically created and revisited in a real save, persists correctly, and fails safely if the capability disappears, **or** (B) a bounded operation audit records refusal/unsupported status on the current adapter/build without asserting that Elin can never support the operation and all production consumers demonstrably degrade/refuse without claiming the site physically exists.

**Do not:** upgrade a capability from headless/source evidence alone, block the causal beta loop on a genuinely unsupported native write, or present planned geometry as physical fact.

**Bound:** one representative site contract and operation decision, not a general dungeon platform. Existing-location semantic play remains a sufficient beta path. Capability promotion still requires the complete real-save sequence; an unavailable safe operation may close with documented refusal tests, never with an invented runtime success or a mandatory unsafe experiment.


**Authority / proof route:** [owning source and representative tests](systems/world.md#sites), [neighbor contract](systems/integration.md#native-boundary), [validation](agent/validation.md#native).

**Sequence:** BQa-032 → BQa-033 → BQa-034.

---


## BQa-034 — Native site contents and additive mutation

Verify and safely expose, where the current build supports them:

- item placement;
- NPC placement;
- fixtures;
- barriers/openings;
- additive site changes;
- player-modified-ground detection.

BQ-143's conservative mutation rules remain authoritative. Treat each operation independently: support for one native mutation does not imply support for all of them.

**Depends:** BQa-033; BQ-143.

**Done when:** on a supported path, a previously created BQ site receives one verified-safe additive change without regeneration, overwriting player changes, duplicating actors/items or corrupting persistence; operations that remain unsupported are individually gated and fail closed with evidence rather than being guessed.

**Do not:** infer capability families from one successful write, regenerate a site to simulate an additive update, or overwrite player-modified ground.

**Scope/unsupported exit:** audit the listed operations independently but implement at most the one representative supported additive change required by the existing site contract; do not build six native subsystems in this commit. If structure or safe ground inspection is unavailable, refusal for every dependent write plus the retained semantic path completes the step. Supported operations need actual readback, revisit/reload idempotence and capability-loss proof; unavailable operations need explicit refusal, not a guessed implementation.


**Authority / proof route:** [owning source and representative tests](systems/world.md#sites), [neighbor contract](systems/integration.md#native-boundary), [validation](agent/validation.md#native).

**Sequence:** BQa-033 → BQa-034 → BQa-035.

---


## BQa-035 — Consequence embodiment

Close representative physical consequence joins:

```text
NPC arrives
NPC leaves
visitor waits at Home
caravan travels/fails
person disappears
returned object physically returns
site condition changes
```

BQ must never present a physical claim stronger than its evidence.

If only semantic/coarse state is available, presentation remains at that grade.

**Depends:** BQa-014, BQa-020, BQa-033, BQa-034; BQ-097, BQ-098.

**Done when:** representative physical consequences occur in real Elin and the inspector distinguishes verified live embodiment from semantic fallback.

**Scope:** verify existing arrival/travel/item/site owners and close only the representative consequence needed for beta. The list is a coverage matrix, not seven new movement systems. The beta requires at least one real, visible mechanical consequence and honest semantic continuation for unsupported families; creating new maps or teleporting NPCs is not mandatory. Any delegated vanilla work needs one ownership/reconciliation contract and readback, never simultaneous BQ simulation.


**Authority / proof route:** [owning source and representative tests](systems/world.md#travel), [neighbor contract](systems/world.md#sites), [validation](agent/validation.md#native).

**Sequence:** BQa-034 → BQa-035 → BQa-036.

---


# Phase BQa-L6 — Native discovery and tracking


## BQa-036 — Journal information architecture

Extend the existing native BQ journal as a narrative-memory interface using the **already-established single top-level Elin journal mount and BQ-owned page registry/rendering shell**. Do not rebuild or clone another journal shell.

Recommended BQ-owned pages:

```text
Overview
Matters
People
Chronicle
```

Implement new information architecture primarily as page descriptors/projections over authoritative state and player knowledge. Evidence/details should be contextual where possible.

Never expose normal-player-facing internals such as storylet IDs, actor-intent/director scores, tone metadata, raw capability diagnostics or raw event IDs.

BQ-138 owns acceptance of its corrective BQ-138h build before the BQa phase starts. This step rechecks that foundation under the new projections: no inherited vanilla quest placeholders, correct internal navigation, reopening, scrolling, save/reload behavior and no damage to the vanilla quest journal.

**Depends:** BQa-024, BQa-032; BQ-033, BQ-034, BQ-138.

**Done when:** a live save shows current learned matters, known people and Chronicle/history through the existing native-feeling BQ journal surface; Overview/Matters/People/Chronicle are projections rather than state stores; the corrected shell passes its visual/lifecycle checklist; and no duplicate top-level BQ mount or inherited quest-placeholder UI exists.

**Do not:** rebuild the native shell, persist UI state as narrative authority, or reveal information the player has not learned.

**Authority / proof route:** [owning source and representative tests](systems/integration.md#presentation), [neighbor contract](systems/integration.md#discovery), [validation](agent/validation.md#native).

**Sequence:** BQa-035 → BQa-036 → BQa-037.

---


## BQa-037 — Diegetic discovery router

One development may be discoverable through several plausible surfaces:

```text
overhear
ask directly
NPC conversation
guild/organization contact
notice/board
letter
visitor
found object
site
direct observation
journal after learning
```

The router chooses a plausible **delivery route**, not whether the fact happened.

BQ-099/BQ-100 remain attention/pacing authorities for unsolicited exposure.

**Depends:** BQa-029, BQa-031, BQa-036; BQ-035, BQ-036, BQ-099, BQ-100.

**Done when:** the same underlying development can be learned through different legitimate surfaces depending on current state, without teaching the player information they did not actually receive.

**Scope and proof:** implement a routing policy over existing verified adapters, not every illustrative delivery surface. At least two materially different live routes (such as requested conversation and an acknowledged ambient delivery) must converge on the same claim without duplicate learning or claims of objective truth. A physical object/visitor/letter route needs its own evidence; unsupported routes remain unavailable. Reopening the journal alone teaches nothing.


**Authority / proof route:** [owning source and representative tests](systems/integration.md#discovery), [neighbor contract](systems/expression.md#attention), [validation](agent/validation.md#native).

**Sequence:** BQa-036 → BQa-037 → BQa-038.

---


## BQa-038 — Formal quest projection

Formal quests become a presentation mode over existing matters when somebody actually commissions the player.

Valid:

```text
existing shortage
+ shopkeeper wants player help
+ explicit offer/contract
→ formal job
```

Invalid:

```text
quest generator
→ invent shortage
→ make world pretend it existed
```

**Depends:** BQa-030, BQa-037; BQ-083, BQ-137.

**Done when:** the same underlying matter can remain informal, become a formal commission, or resolve without the player depending on social/world state rather than archetype-specific quest scripting.

**Commit boundary:** reuse recorded offers/acceptance and obligations for commission terms and payment/access, projecting them through existing verified surfaces. Native quest-engine integration is optional if unsupported; an explicit Drama commission is sufficient. Acceptance/decline/withdrawal and an NPC resolving the commissioned matter must reconcile the same world state without duplicate payout or fabricated work.


**Authority / proof route:** [owning source and representative tests](systems/expression.md#semantic-communication), [neighbor contract](systems/state.md#social-state), [validation](agent/validation.md#native).

**Sequence:** BQa-037 → BQa-038 → BQa-039.

---


## BQa-039 — Player attention feedback

Extend director **presentation weighting** with actual engagement history while preserving simulation independence.

Distinguish:

```text
never surfaced
surfaced and ignored
investigated
actively engaged
resolved
abandoned
frequently revisited
```

BQ-119 is debug telemetry and may measure/validate these distinctions, but it must not become the runtime authority the world depends on. Production weighting should derive from durable information already owned by delivery history, player knowledge, recorded player actions, threads and other authoritative/derived state wherever possible. If a new durable datum is truly required, give it one explicit owner and persistence contract rather than reading debug counters.

Use engagement only for presentation and pacing, never world outcomes.

**Depends:** BQa-037; BQ-100, BQ-119 (debug validation only).

**Done when:** two otherwise similar developments are presented differently because of legitimately derived engagement history while a headless simulation with presentation disabled evolves identically; disabling BQ-119/debug telemetry does not alter production selection behavior.

**Do not:** make telemetry counters authoritative, reward engagement by changing NPC/world outcomes, or treat "not surfaced" as "ignored."

**Comparison boundary:** with the same authoritative external inputs and player actions, engagement-derived exposure weighting changes only presentation. Different actual player choices can legitimately change the world. New durable engagement evidence, only if absent from existing owners, must have this step's explicit migration/default coverage; neither frame/UI counters nor debug telemetry is a prerequisite for simulation.


**Authority / proof route:** [owning source and representative tests](systems/expression.md#attention), [neighbor contract](systems/integration.md#discovery), [validation](agent/validation.md#expression).

**Sequence:** BQa-038 → BQa-039 → BQa-040.

---


# Phase BQa-L7 — Prove causal gameplay


## BQa-040 — Production world-sweep harness

Build the successor to isolated proof fixtures.

Run controlled worlds through many deterministic seeds using the production check-family/scaling rules, actual BQa world-development cycle and the production semantic action/goal contracts.

Report at least:

```text
distinct causal-history signatures
check-family / outcome distribution
pressure distribution
actor-local pressure divergence
goal creation/revision/abandonment/satisfaction
actor/action distribution
resolution distribution
failure semantics / transformations
knowledge divergence
false-belief creation
organization involvement
matter lifetime
cross-matter interactions
situation proposal/admission distribution
proposal fulfillment/refusal distribution
new actors/facts/sites created
reuse ratio
player vs NPC resolution ratio
solution-family diversity
dialogue-act topology
site topology
```

The key metric is:

> **How many meaningfully different causal histories occurred?**

**Depends:** BQa-001–BQa-032, BQa-037; BQ-104, BQ-141.

**Done when:** CI can produce a deterministic expressive-range report and identify when a change collapses many seeds into the same causal history, including collapses caused by check scaling, omniscient pressure interpretation, goal/action matching or proposal fulfillment.

**Do not:** count mere text variation as causal diversity, use fixture-only substitute schedulers, or interpret missing coverage as automatically requiring more authored content.

**Measurement refinement:** extend the minimal production runner from BQa-016; do not defer regression coverage until this step. Use real registered checks and operators after initial-state setup; doubles supply native observations/refusals only. Include baseline seed sets with structural assertions (not just reports) for property, business, false belief, institutions and cross-matter continuation, plus a negative control with no supported cause. Reports distinguish live evidence, observed input, headless adapter capability and missing coverage.


**Authority / proof route:** [owning source and representative tests](systems/expression.md#attention), [neighbor contract](systems/flow.md#live-host-joins), [validation](agent/validation.md#world).

**Sequence:** BQa-039 → BQa-040 → BQa-041.

---


## BQa-041 — Ecology coverage report

Extend BQ-133's coverage philosophy beyond dialogue content.

Report cells such as:

```text
pressure × response family
pressure × actor-local interpretation
pressure × actor type
pressure × goal family
goal condition × action effect
pressure × resolution
pressure × failure transformation
storylet × pressure
organization × interpreted pressure × response
proposal requirement × fulfillment/refusal
surface × information source
site kind × objective
```

Zero coverage is information, not automatically a build error.

**Depends:** BQa-040; BQ-133.

**Done when:** the report exposes overrepresented and underrepresented world interactions without prescribing content as a hard quota.

**Acceptance refinement:** count attempted and achieved effects separately from declared coverage; include unreachable goals, refused bindings and repeated identical histories. Use this evidence to adjust existing rules/weights or the smallest missing supported action in its owning system before beta, not to mandate one authored story per empty cell.


**Authority / proof route:** [owning source and representative tests](systems/expression.md#content), [neighbor contract](systems/actions.md#actions), [validation](agent/validation.md#world).

**Sequence:** BQa-040 → BQa-041 → BQa-042.

---


## BQa-042 — Long-horizon stress simulation

Simulate months and years of activity.

Measure:

```text
event growth
live thread count
unresolved pressure
goal accumulation / retired-goal growth
rumour population
false-belief population
actor-memory size
site count
organization goal/activity growth
CPU cost
save size
duplicate histories
deadlocks
causal loops
proposal/admission/fulfillment backlog
```

Actively search for pathological worlds rather than merely measuring speed.

**Depends:** BQa-016, BQa-017, BQa-040; BQ-107, BQ-108.

**Done when:** a large synthetic world can run for a stated horizon under a stated performance budget without runaway narrative-state growth, starvation or systemic deadlock.

**Retention contract:** state the seed set, actor count, horizon, machine and budgets before measuring, then enforce regression limits. Bound active working sets, retries, goals and proposal queues while preserving meaningful causal history. An append-only ledger may grow with actual events; require measured growth per simulated day, not impossible constant-size history or deletion of causal links. Diagnose and correct discovered pathological scheduling/retention behavior within the responsible owner before claiming acceptance. Include reload, capability loss, exhausted resources, quiescent worlds and adversarial contention.


**Authority / proof route:** [owning source and representative tests](systems/world.md#autonomy), [neighbor contract](systems/integration.md#persistence), [validation](agent/validation.md#world).

**Sequence:** BQa-041 → BQa-042 → BQa-043.

---


## BQa-043 — Causal-chain audit

Sample random developments and reconstruct:

```text
why did this objective pressure exist?
which actor/organization could legitimately perceive or believe it?
why did this actor form this goal?
what desired state would satisfy that goal?
why did this registered action match it?
why was the action available?
why did this actor win or lose the opportunity?
what exact event/state transition changed?
what new pressure resulted?
why was a later proposal produced?
if it required new state, who fulfilled it and why was that atomic/safe?
why was the later situation admitted?
why did the player learn?
what happened next?
```

Every answer must point to an authoritative source, a named derived reading/decision, or explicitly say unknown. The trace must distinguish objective state from actor-local belief/stake and distinguish proposal ranking from proposal fulfillment.

**Depends:** BQa-001, BQa-024, BQa-037, BQa-040, BQa-042.

**Done when:** sampled long chains can be reconstructed entirely from authoritative state, observations, actor/organization-local interpretation, machine-readable goals, registered action effects, recorded consequences, proposal fulfillment, explicit director decisions and explicit delivery decisions, with unknowns preserved rather than silently inferred.

**Historical explanation:** use retained cause/decision provenance from BQa-001 and owner-specific records, not today's recomputed motives as though they explained yesterday. Unknown legacy links are acceptable; missing causal explanations for new beta-chain decisions fail this gate. Player reports are knowledge-filtered projections of this inspector evidence, not raw audit dumps.


**Authority / proof route:** [owning source and representative tests](systems/state.md#history), [neighbor contract](systems/flow.md#live-host-joins), [validation](agent/validation.md#state).

**Sequence:** BQa-042 → BQa-043 → BQa-044.

---


## BQa-044 — Counterfactual divergence and expression-independence proof

Prove two properties the ordinary sweep cannot establish by itself.

### Counterfactual divergence

Clone an initial state and alter one early authoritative outcome while keeping subsequent deterministic conditions comparable.

Later worlds should diverge through causal state:

```text
different pressures / actor-local interpretations
→ different goals
→ different matched actions
→ different relationships/organizations/sites
→ different later situations
```

not merely different Chronicle text.

### Expression independence

Run equivalent simulations with storylet/dialogue/presentation delivery disabled.

The underlying world-development cycle must continue evolving. Re-enabling expression should reveal/interpret the world, not retroactively create the causes.

**Depends:** BQa-001, BQa-002, BQa-016, BQa-017, BQa-040, BQa-043.

**Done when:** automated fixtures demonstrate meaningful downstream divergence from one changed early outcome and demonstrate that disabling narrative expression does not halt authoritative world evolution.

**Strict proof:** compare authoritative simulation projections with expression on/off under identical external observations and player-action schedule, excluding only expression/delivery records and the player knowledge those deliveries legitimately change. NPC communication and autonomous choices must continue without a running storylet. Then restore presentation and verify that only delivered existing claims are learned. Counterfactual pairs may diverge causally; expression-only RNG draws may not diverge world decisions. Exercise save/reload in both modes and use the production cycle, never a substitute fixture scheduler.


**Authority / proof route:** [owning source and representative tests](systems/flow.md#live-host-joins), [neighbor contract](systems/expression.md#storylets), [validation](agent/validation.md#world).

**Sequence:** BQa-043 → BQa-044 → BQa-045.

---


## BQa-045 — Live public-beta acceptance suite

Run long human playtests across different player priorities:

- farmer/homebody;
- dungeon explorer;
- criminal;
- merchant/town builder;
- social character;
- player who mostly ignores BQ;
- player who actively seeks BQ situations.

The beta gate is:

> **In several independent sessions, a tester can recount a multi-event story that nobody authored as a sequence, and every important event in that story can be reconstructed from the simulation's causal record.**

Additional acceptance requirements:

- ordinary Elin play remains viable and not constantly interrupted;
- a player can ignore BQ without punishment;
- multiple matters can resolve without the player;
- one resolved matter can naturally contribute to later distinct matters;
- hidden authoritative truth does not create omniscient actor goals;
- false sincere beliefs can create divergent but causally explainable behavior;
- the same broad pressure can produce structurally different histories;
- goal satisfaction follows authoritative desired state rather than merely successful action labels;
- live dialogue expresses its actual semantic payload;
- recurring characters retain stable non-stereotyped voice across scenes/reload;
- Chronicle/journal reflects only what the player could know;
- physical claims match verified embodiment and unsupported native capabilities degrade honestly;
- save/reload preserves causal continuity;
- at least one cross-matter chain emerges without authored sequence logic;
- at least one organization-driven chain occurs in live play;
- at least one 30+ day ignored-player period still produces an intelligible causal history;
- rejected proposals and failed fulfillment do not leak partial authoritative state;
- counterfactual and expression-independence proofs remain green.

**Depends:** BQa-001–BQa-044.

**Done when:** the acceptance suite passes on real saves and the resulting stories are inspectably emergent rather than scripted vertical slices.

**Required acceptance matrix:** execute all seven chains in the end-to-end matrix below, not only a memorable theft. Record build, seed/save, elapsed days, native capabilities, relevant causal IDs and observed gameplay. Require at least one equivalent initial matter to support three distinct mechanical solution families, plus different routes across the beta chains, contrasting actor decisions, quiet periods, recovery, bounded exposition and an hour of ordinary play without forced interruption. An ignored 30+ day matter must change or resolve and contribute to a later distinct matter; prior brief player engagement must not freeze it. Failure of a gameplay criterion requires correction before release; reports and noun changes alone are not acceptance.


**Authority / proof route:** [owning source and representative tests](systems/integration.md#native-boundary), [neighbor contract](systems/integration.md#discovery), [validation](agent/validation.md#native).

**Sequence:** BQa-044 → BQa-045 → public-beta release decision.

---


# End-to-end acceptance matrix

These are reusable behavioral proofs, not seven authored plots. Each begins from authoritative
initial state or actual observed input, then uses production selection/owners without injected
follow-up goals, constructors or guaranteed outcomes. A new fact in the setup is explicitly a
fixture input, never proof the live intake exists. BQa-016 starts the core traces; BQa-017 proves live integration; later owners
extend them; BQa-040–BQa-045 retain regression and live evidence.

| Chain | Required trace and accountable joins | Failure that must be excluded |
|---|---|---|
| Property / crime | BQa-012 observes changed possession; BQa-007 permits notice/suspicion without inventing culprit; BQa-008–BQa-015 form recovery/concealment/investigation/retaliation alternatives and arbitrate one object; BQa-016 executes consequences through existing owners, hosted live by BQa-017; BQa-021–BQa-025 recognize a later distinct matter | Situation owner commits the theft in order to recognize it; success of unrelated rapport closes recovery; invented witnesses |
| Economic / service | BQa-012 records a justified supplier/service disruption in existing business/demand owners; BQa-007–BQa-011 derive responsible actors' supply/help/closure options; BQa-016 changes supported resource/obligation state through existing owners, with BQa-017 supplying native inputs; BQa-018–BQa-024 propagate effects to workers/customers/rivals/groups | Sleeping implies failure; fixtures manually close/reopen a shop; organization creates unearned reserves; simulated stock substitutes for Elin stock |
| Social false belief | BQa-007 enumerates sincere false beliefs without true objective pressure; BQa-008–BQa-011 preserve motive and subjective assessment; BQa-012/BQa-016 record what actually occurs, with live inputs from BQa-017, and enable legitimate counterevidence, correction or reinforcement; BQa-023 links later matters | True-fact detector filters the case out; objective goal satisfaction secretly tells the actor the truth; another actor's reaction is scripted |
| Organization | BQa-018 requires institutional receipt/accounting evidence, then revises a goal; BQa-019 selects supported operations using members/resources and claims; BQa-020 enrolls and ticks real production groups; consequences feed individuals and other institutions via legitimate reports | Union of members' private beliefs; unknown goal falls through to wealth; Lab crews are mistaken for live population |
| Player ignored | BQa-016 proves 30+ days through the Core cycle and BQa-017 connects live advancement away, including no live storylet/thread and a formerly engaged matter; BQa-024/BQa-025 permit resolution, escalation and a later distinct episode; BQa-032/BQa-037 expose the changed state on return through actual learning | One catch-up action called a living month; permanent player-engaged immunity; zone entry rerolls; vanilla Home catch-up duplicated |
| Player intervention | BQa-037 discovers an existing matter; BQa-030/BQa-031 expose a revalidated shared verb and deliver its real meaning; its consequence changes NPC options on BQa-016's next pass through BQa-017's live host; BQa-038 may project an actual commission | Choice label grants a result; scene blocks autonomous continuation; duplicate input spends an item twice; decline becomes punishment |
| Expression independence | BQa-002 separates optional RNG; BQa-016/BQa-017/BQa-024 continue causes without expression; BQa-029 distinguishes actual communication from wording; BQa-044 compares runs and restores knowledge-gated expression of existing history | Scene route invents needed harm; silent rendering counts as player learning; attention preference becomes simulation admission; restored UI retroactively creates causes |

# Scope and release judgment

The required scope is BQa-001 through BQa-045. There is no optional omission of a core loop, group
agency, discovery, intervention or continuity gate. Operation-specific unsupported exits for new
physical sites/additions are deliberate: the beta must still demonstrate visible mechanical
consequences through supported existing Elin mechanics and honest coarse BQ state. An all-log world
does not pass. Three organization kinds and the seven traces are coverage of the vision, not a
mandate to simulate every guild or every example verb.

Keep deferred: broad generated settlements, a full commodity economy, regional political simulation,
runtime LLM prose, multiplayer, a public mod API, a content workbench, a general UI framework,
parallel NPC schedules and dozens of extra archetypes. The BQ post-launch register retains their
rationale. BQa adds only the contracts and limited existing-content adaptations necessary to prove
the causal loop; observed coverage failures are fixed in the owning rules/actions before release.

**Sufficiency judgment:** yes, implementing every contract and passing every acceptance condition
here closes the intended self-propelling loop. This is a design judgment, not a claim that unbuilt
code or native operations have passed. Remaining risks are native support, tuning, meaningful
diversity, long-horizon cost and player comprehension. They have explicit refusal, regression,
measurement and live acceptance owners; none justifies an unowned additional feature now.

The world need not always be in crisis. Quiescence after resolution is valid. Later independent
changes and surviving stakes must be able to restart activity without a quest factory or a player
visit. Stories become possible through consequences; the plan does not guarantee a dramatic event
on every tick.
