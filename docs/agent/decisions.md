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

## D023 — Ambient talk carries gossip, and a belief never arrives without a line

The player learns things by being present only through `AmbientTalk`, and two rules bind it.

**It carries what the town repeats, never what somebody knows first-hand.** A speaker may mention
a belief only when they hold it as hearsay. First-hand knowledge - witnessed, participated in, read
- is testimony, and testimony is asked for: questioning and eavesdropping reach it, cost a check,
and expose the player socially. So the witness who watched a theft says nothing about it in the
street, and the neighbour who heard about it secondhand is the one who mentions it. That is also
what makes this step rest on circulation rather than merely follow it: with no gossip in the town,
an honest ambient layer has nothing to say.

**The line comes first, and only a line the player heard may teach them anything.** Choosing the
remark and delivering it are separate calls; the presentation layer speaks it, and the caller
teaches the player only once that succeeded. A belief that arrived because a bark route was missing
is knowledge from nowhere.

It never draws from the world RNG. It runs whenever the player acts, which is not a schedule a save
can reproduce, so a die drawn here would make every downstream roll depend on how many steps the
player took. Who speaks and what they say is a deterministic read of who is standing where; the
pacing is a cooldown stamped on the world, which is what stops a reload from being a way to hear the
whole town at once.

**BQ-036 extends the first rule to every free way of getting somebody talking.** Asking a person
what has been happening reaches the same repertoire the street does - `TalkRepertoire` decides what
anybody is willing to say, and both `AmbientTalk` and `TownNews` read it - so the two can differ
only in how forthcoming the speaker is, never in what class of knowledge they will part with. A verb
that hands over testimony must cost a check and expose the player, as `question` and `eavesdrop` do.
Anything free carries gossip, or the investigation verbs are decoration.

Reason: this is the one route by which the player gains knowledge without choosing to, so it has to
be the most conservative one in the mod, not the most convenient.

## D024 — A guild is a channel and a reading, and access gates the reading

Guild membership **carries** claims between members wherever they
are standing - a route the street does not have - and it **reads** what it carries: the same
robbery is a bounty to the Fighters and stock to the Thieves. What a network carries is its
interest in the predicate ontology, so one event reaches four guilds differently, or not at all,
without anything having been written per scenario.

Access decides who is told what a matter means, never who may hear that it happened. A non-member
gets the claim, hedged exactly as a member gets it, and is left believing it just as firmly; what
membership buys is the contact who adds the reading and brings it up first. Routing itself never
touches the player in either direction - a card is not a subscription, and a guild reaches them
through somebody who says something.

Reason: a network that withheld facts would close routes on a build rather than open them, which is
D012's line and the availability model's. Framing the interest as a table over predicates rather
than over situations is what keeps four guilds from becoming four quest pools; keeping the routing
inside the day the gossip scheduler already governs is what stops a reload from running it twice.

What a guild does with what it reads is `D025`.

## D025 — A guild also acts on its reading, on conditions only, and spends its own willingness

A member with standing can commit a guild to a matter its own interest table already reads as its
business. Three rules bound it.

**The same table decides, and it decides alone.** Which guild a matter belongs to is
`GuildNetworks.Reads` over the predicate ontology, never a per-situation or per-guild authoring, so
one verb commits the Fighters to somebody in danger and the Merchants to a town that is short
without either being written anywhere as a guild's quest.

**Only a standing trouble can be answered.** A guild may end a condition that is wrong now - somebody
exposed, a thing broken, a road shut, a demand unmet - and never a thing that happened. History does
not become answerable because a network takes an interest in it.

**Membership is a precondition; rank and contribution are read and never written.** A non-member is
refused by name and keeps every route that is his own hands, which is `D012`'s line with the guild
in the role the receiver and the deity already hold: the party doing the work declines a stranger.
Rank is a threshold against the size of the matter and contribution moves the odds; both are read
from vanilla, because the guild's opinion of its members is vanilla's to keep. What the asking
spends is the guild's willingness - a hall that refuses will not be asked the same thing again by
the same member, and a botched asking puts the matter beyond that guild for everybody.

What is superseded is the claim, not a body. An answered matter records that the guild took it on
and never removes an Elin creature or moves an actor (`D021`); whoever states a claim owns whether a
live character stands behind it.

Reason: without the first rule four networks become four quest pools, which is what `D024` exists to
prevent, and the mechanic would not generalise past whichever situation it was written for. Without
the second a guild becomes a way to edit the past. Without the third it is either a wish - free,
repeatable, and worth nothing to have earned - or a second progression system quietly disagreeing
with the one the player can see on the guild board.

## D026 — A storylet role is a requirement, not a position, and nobody is their role

A storylet names temporary roles - Accuser, Accused, Witness, Mediator, Confidant - and
`StoryletCasting` fills each from whoever qualifies in the place the scene happens in. Three rules
bind it.

**A role holder is found, not handed over.** Positive requirements ask for knowledge, proof,
ownership or standing; negative requirements reject the dead, the absent, anybody without social
agency, anything the registry does not know as a person, and the player, who is written into a role
only when the caller names them. Resolving a role by the slot somebody happened to occupy - the
first participant who knew the fact, the object half of the focus - is what produced an accusation
whose corroborating witness was the accused and a theft whose injured party was the ring.

**One person, one role per scene.** Named sources bind before searched ones and required roles
before optional ones, so a role that already knows who it wants cannot lose them, and an optional
corroborator never takes the accuser. A role nobody is left for is uncast: required means the
storylet refuses, optional means the scene plays without it.

**Casting writes nothing.** Bindings live on the firing the thread keeps, never on a
`NarrativeNpc`, so the next scene re-casts against the world as it is then, and a save carries who
held a role rather than who somebody became.

Choosing among the qualified stays deliberately unscored - first in a stable order - because
preferring the better group is `BQ-068`'s role chemistry, and a score here would be a second one to
keep in step with it. `BQ-114` changed what that stable order *is* - the faces the player already
knows come before the strangers - and nothing else: a role still takes the first candidate that
meets its requirement, and being known is never part of meeting it.

Reason: a role that is a position makes every storylet a template for the situation it was written
against, which is the failure the whole storylet layer exists to avoid; and a role holder who is not
a person is state a later save load throws the thread away over.

## D027 — Familiarity chooses between situations the world already supports, and never creates one

`PlayerFamiliarity` reads how well the player knows somebody from residency on their land, vanilla
affinity, the event ledger and a recorded relationship edge. It is a reading, never stored, and
every ground only raises it; a ground the build cannot read contributes nothing rather than zero
(`D017`).

**Eligibility first, preference second.** The settlement generator decides whether a proposal is
eligible on the world's own pressure alone, and only then adds the generic `player_familiarity`
term. A settlement that would have stayed quiet stays quiet however many friends the player has in
it, and the score is still the sum of terms that were each named with the world state behind them.

**A preference, not a gate.** Familiarity orders casts. It never opens or closes a route, never
changes a check, and is never read as affection - somebody the player robbed is exactly the person
a situation about them should be cast from. The affinity thresholds the action library already
holds (`impersonate` refusing somebody who knows your face, an underworld contact who will vouch)
answer a different question and stay where they are.

Reason: attachment before stakes is an engagement requirement, and the failure it invites is a
world that manufactures pressure around whoever the player likes. Separating the two means the
simulation still decides what happens and the player's history only decides which of the things
that could happen is worth telling first.

Add a new entry only when the decision is both load-bearing and durable.
