# Durable Agent Decisions

This file contains only decisions expected to remain useful across many commits. It is not a status log, changelog, or substitute for design documents.

Keep entries terse. If a decision stops being durable, update or remove it rather than preserving obsolete history here.

## D001 — Core is headless

`BrilliantQuesting.Core` must not depend on Elin, Unity, or BepInEx. Runtime-specific state crosses the boundary through adapter abstractions.

Reason: deterministic headless testing, simulation tooling, compatibility, and separation from game-version churn.

## D002 — Observe vanilla outcomes

Where Elin already resolves an action, Brilliant Questing should normally observe the resulting world state/event rather than implement a competing resolution system.

Reason: avoid double consequences and preserve compatibility with vanilla/modded mechanics.

## D003 — Epistemic layers stay separate

Truth, a person's belief, available proof, and an authority's judgment are different things and must not be collapsed into one state.

Reason: investigations, rumors, lies, false accusations, correction, and consequences depend on the distinctions.

## D004 — Narrative identity outlives vanilla object presence

Stable `EntityId` identity must survive unloaded, missing, dead, destroyed, or otherwise unavailable vanilla objects.

A binding records identity correspondence; it does not prove current existence.

## D005 — History is append-oriented

The event ledger represents what happened. Later interpretation should derive from recorded events rather than rewriting historical events to fit current knowledge.

## D006 — Runtime evidence outranks metadata discovery

Finding a class, method, field, trait, or hook in assemblies proves availability, not behavior.

Record runtime verification in `docs/elin-api-notes.md` when behavior matters.

## D007 — No authoritative runtime LLM

Runtime LLM output may not determine authoritative checks, facts, world state, persistence, or consequences.

## D008 — Player knowledge is not world omniscience

Background simulation may update NPC/world beliefs without silently giving the player those beliefs.

## D009 — Authored content, behavior, and save history are separate

C# expresses behavior. Authored data expresses content. Saves store history/content identifiers rather than treating authored prose as immutable save state.

## D010 — Existing Elin mechanics are preferred solution surfaces

If an existing Elin mechanic can constitute a valid solution, use/integrate it instead of creating a Brilliant Questing-only duplicate.

## D011 — Physical proof stays attached to a physical object

Reading an object for what it proves requires having that object: examination verbs work on the actor's own inventory, and a search recovers only what is in the actor's current zone.

Reason: keeps evidence something a player can carry, lose, sell or have taken, and keeps acquiring it — searching, following, lifting a pocket — real gameplay rather than a formality. Knowing an unprovable thing is a legitimate and distinct state.

## D012 — Standing gates contacts, never attempts

Thieves Guild rank, Karma and personal standing decide which characters will do criminal work for the player — receivers, forgers, carriers. They never gate a verb the player performs with their own hands.

Reason: a build should differ by which routes exist for it, not by rolling worse on the same route. Gating an attempt on membership would also collapse into the "low odds are a reason to hide the option" mistake the whole availability model exists to avoid. A contact who will not deal with a stranger is a genuine impossibility, in the same class as invoking guild authority without membership.

## D013 — Losing an object and unmaking it are different revocations

Proof revocation is keyed by the object, not by the claim. Destroying a thing strips it from everybody's proofs; parting with it strips it only from the person who parted with it. Neither touches belief.

Reason: a claim resting on two objects should keep the second when the first burns, and a fact-keyed revocation cannot express that. Selling evidence must leave the buyer able to produce it. Passing an item id to the fact-keyed call matches nothing and silently revokes nothing at all.

## D014 — Vanilla owns production; the simulation reads quality and never rolls it

Where an object already exists, the procedural layer reads what the game made and rolls nothing. A demand is a property constraint - kind, quality, worth - and goods that meet it are handed over without a check. A check is run only when there is no such object and the actor is working from raw materials, and even then it produces no new object: the stock is consumed and the demand is answered.

Reason: Elin already has cooking, brewing, alchemy and building. Rolling procedural dice over a finished Thing would be a second, worse crafting mechanic disagreeing with the first about what the player just made. Stating demand as a constraint rather than as a named item is what keeps it answerable by any route that produces the right thing, and keeps a shoddy object a wrong object rather than a bad roll. An unread quality is zero, which every threshold refuses - a build that cannot report quality loses the hand-over route and keeps the rest, rather than accepting goods on the strength of a field nobody filled in.

## D015 — A power you petition is a contact, not a tool

A deity gates whether the attempt exists. Which god a matter is in the gift of, the piety he asks of whoever asks him, and what is lying on his altar are preconditions: a follower of another god is refused by name, not given worse odds.

Reason: this looks like an exception to D012 and is not one. The god is the party doing the work, so declining to hear a stranger is exactly a contact who will not deal with you — the same class as a receiver who will not fence for an unknown, and not the same class as forbidding somebody to pick a lock with their own hands. Read the other way round, D012 would force every faith route into a modifier on one generic magic roll, which is what `PM 69` and `MD 13.6` exist to prevent. The test is whether the standing belongs to the actor's own hands or to somebody else's willingness.

Which god a matter belongs to is stated by the situation as a fact, never by a table of deities and their portfolios held in Core. Nothing in the simulation knows what Kumiromi is the god of, which is what keeps the routes generatable and survives Elin adding a religion.

## D016 — The verb registry is not the player-facing menu

Narrative verbs are simulation capabilities, not global UI entries. Player-facing actions are projected through contextual interaction surfaces and semantic intent families, then filtered by actor, target, knowledge state, world state and affordance.

Prefer shallow semantic paths such as `Talk -> Inquire -> Ask about Vurl`, `Talk -> Aggressive -> Intimidate`, or `Examine -> Study -> Read ledger` over a flat list of registered verbs.

The UI hierarchy must describe the player's intent, not the code's class hierarchy. Adding a new verb must not require exposing that verb everywhere, and the presentation layer must not reveal subjects or facts the player does not know.

Reason: the internal action vocabulary must grow without increasing cognitive load, leaking hidden information, or turning Brilliant Questing into a debug command menu.

## D017 — A datum the game did not answer is absent, not zero

Where a read can partly fail, the snapshot says which parts it got. `HomeState` carries `CapacityKnown` and `TryGetMetric` beside the values, and the adapter leaves an unread element out rather than defaulting it.

Reason: capability honesty (a whole integration reports unsupported) is too coarse once one read returns several numbers. A Home whose capacity silently defaulted to zero would look permanently full and close every shelter route; one whose Public Safety defaulted to zero would look like a slum and open the wrong ones. Callers with a threshold must ask whether the number was read, and the refusing direction is the safe one when it was not.

## D018 — The mod admits residents; vanilla keeps the settlement's numbers

The Home has exactly one write, `IVanillaState.TryAdmitResident`, and it puts a person on Elin's own resident roll. Nothing above the seam sets a resident's job, and nothing sets Public Safety, Public Morality, Food Supply, Soil, Publicity or Administration. Those are read.

The write is verified rather than trusted: the adapter tells the branch and then asks it who lives there, and reports false if the person is not on the roll afterwards.

Reason: the six Home Skill elements are vanilla's arithmetic over who lives at the settlement and what they do, and they are what the player watches on the Home board. A procedural layer that wrote them directly would be a second settlement economy disagreeing with the visible one, in the same way a procedural crafting roll would disagree with Elin's (D014). Taking somebody in changes the settlement because the game recomputes it from the new resident, not because the mod decided what a refugee is worth. And an unverified write would leave a `sheltered_by` fact standing over a Home that never took anybody - the stale-binding failure D011 exists to prevent, wearing a different hat.

## D019 — The mutation gate is the seam, and an unclassified actor never gains a reach

Every write into Elin is declared on `IVanillaState` with `[VanillaMutation]`, naming the `MutationKind` it performs and which parameters are the actor it performs it on. The public writes are implemented once, on `VanillaStateBase`, which checks `MutationPolicies` and only then calls the implementation's unguarded half. Both `SandboxVanillaState` and `ElinVanillaState` derive from it, so the verbs and the consequence engine consult the policy by construction rather than by remembering to.

The classification comes from the game (`IVanillaState.GetActorClass`), never from the world model, and a build that cannot answer reports `Unknown`. `Unknown` keeps the reversible reaches - dialogue, standing, things and money - and is refused relocation, absence and death. So an unreadable build costs no content and still cannot move or remove a story-critical NPC.

Reason: threading a check through forty call sites means forty places to forget, and the risk is precisely the write nobody remembered. Putting the check where the call sites converge makes "every mutation consults a policy" a property of the contract, testable by walking the seam rather than by enumerating verbs. Failing towards the actor's protection rather than towards the mod's convenience is the same rule as D017, applied to permission instead of to data: an answer the game did not give must never be read as consent.

The gate is a floor, not a substitute for a precondition. A verb whose write the policy will certainly refuse should be absent rather than offered and declined, which is why the bed-spending Home verbs ask `MayMutate` in `GetAvailability`.

## D020 — An absence is intent that is re-derived, and coming back is never refused

A Grade B absence is stored as intent - who is away, where they went, where they left from, when
they are due - and nothing anywhere records that the game has been told. Whether Elin currently
agrees is re-derived by `AbsenceLifecycle.Reconcile`, which runs on load, on the day turn and
whenever the player changes zone. Where the game positively reports somebody somewhere other than
their away zone, the move is re-issued; where the game cannot say where they are, nothing happens,
because an unanswered question is not evidence.

Absence is expressed as travel, not removal. The seam has no member that takes a character out of
the world; `TrySendAway` and `TryBringBack` are one move with two permissions, so the same one
character exists throughout and there is nothing to duplicate. The ledger holds one record per
person, which is where "no duplication" is enforced for the load path as well as the live one.

The return is a `[VanillaWithdrawal]` and is never gated. Every other write into Elin passes the
mutation ladder; a withdrawal only takes back a reach this mod already made.

Reason: a persisted "already applied" flag is exactly the fact a reload invalidates - the game puts
everybody where it last wrote them, so a save that remembered the absence had been enforced would
never enforce it again, and the simulation would describe a town the player is not looking at. And
gating the return would let a classification that changed while somebody was away - a build
updated, a flag that starts reading differently, a character a vanilla quest line has since claimed
- strand that person wherever the mod left them for the rest of the save. That is the save
corruption this step is written to avoid, so protection must not be the thing that causes it. Where
the mod can no longer keep somebody away, the record is demoted to Grade A rather than kept:
the smaller true claim beats the larger false one.

## D021 — Vanilla owns embodiment; BQ owns narrative meaning

Where a live Elin mechanic already governs an actor's transient physical behaviour, the mod reads
it or delegates to it rather than running a copy. Loaded movement and pathfinding, the routine
timetable, bodily needs, work and hobby execution, and ordinary combat stay vanilla-owned wherever
the build actually provides them.

What BQ decides is intention and what a result means. It persists narratively meaningful
consequences rather than mirroring transient `Chara` state, and an off-screen BQ resolution stays
coarse: it may say two people met and what was learned, never where they stood. Elin's own
`GlobalGoal` advancement and `Zone.Simulate()` catch-up are reconciled against, not double-run.

Detailed rationale, the four vanilla fidelity mechanisms this has to reconcile with, and the
runtime questions still open are in
[`../design/vanilla-simulation-integration.md`](../design/vanilla-simulation-integration.md).

Reason: Elin has real timetables, needs, work and hobby AI, revisit catch-up simulation and hourly
off-screen global-goal advancement. A second copy of any of them would be a competing simulation
disagreeing with the one the player can see - the same failure `D014` prevents for crafting and
`D018` for the Home's numbers - while costing the maintenance of a life simulator the mod does not
need. Reading instead is also cheaper to keep working across game updates, and it lets vanilla's
own surprises become content rather than contradictions.

## D022 — A situation's ending is an event; the record of it is a projection

Closing a thread goes through one primitive (`ThreadResolution.Resolve`), which writes the state,
the outcome name, and a `ThreadResolved` event carrying who ended it and when. Resolving an
already-resolved thread is a no-op rather than a second ending.

Player-facing history - journal, Chronicle, and whatever later reads them - is derived from the
ledger, the threads and the player's own beliefs. It is never a separately stored narrative, so it
survives save/load for the same reason the ledger does and cannot drift from what happened.

The resolution event names no target and no facts: the deed that ended the matter was already
recorded with its own witnesses, affinity and evidence, and repeating it there would pay the same
act twice and teach bystanders the facts the thread rested on.

Reason: a resolution held only in a thread's fields has no time, no author, and nothing to say if
the thread is ever reopened, and a stored history is a second copy of the truth to keep in step
with the first.

Add a new entry only when the decision is both load-bearing and durable.
