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

`BQ-123` moved one of those negative requirements without weakening it: social agency is asked of
the roles that need somebody to speak rather than of the pool, and the pool rejects whatever the
registry does not know as an *actor* rather than as a person. See `D029`.

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

## D028 — An early contact is a casting decision, never manufactured history

BQ-115 elects a handful of the people a settlement already holds as the faces a save will keep
bringing back. It is what makes BQ-114 mean anything in a new save, where the ledger is empty,
nobody lives on the player's land and Elin's affinity is zero for the whole town, so every reading
`PlayerFamiliarity` can take says "stranger".

**Elect; never fabricate.** The mod may decide who a story is about. It may never record that the
player met, helped or dealt with somebody they did not - writing a `Met` event to make a face
familiar would corrupt the very reading BQ-114 exists to take, and would put a lie in the ledger
that memory, rumour and the Chronicle would all go on to repeat. So election writes no event, mints
no relationship and moves no affinity.

**Recurrence is determinism, not a roster.** Election is a pure reading of the settlement, so the
same save names the same faces on every pass and after every reload without anything being stored
to make it so (`D022`). Nothing about it enters the save.

**Both grounds answer one question, and history wins.** Familiarity and election both say whether
the player will recognise a cast. A candidate records `player_familiarity` or `recurring_contact`,
never both, and an elected face is capped below `PlayerFamiliarity.HouseholdWeight` so it can never
outrank history the player actually made. Election sits exactly where familiarity sits: after
eligibility, so `D027` holds unchanged - a quiet settlement stays quiet however many faces were
elected in it.

**Only faces that are here.** Somebody living on the player's land but standing elsewhere is not
elected: BQ-114 already reads them as the strongest tie the game has, and a slot spent on them buys
nothing while costing the settlement one of its three.

**The importance ladder is allowed a rung before the crisis.** `NarrativeImportance.Recurring` used
to be reachable only after a high-weight memory - only after something had already happened to
somebody. Being a face the save keeps is now its own ground for it, which is what `engagement §4`
means by attachment before stakes and what `PM §19` means by importance that is emergent rather
than designated.

Reason: a threat to a stranger is an errand, and the first situation a save produces is the one
that decides whether the player thinks the system is for them. The honest way to make that first
cast land is to choose who it is about, not to invent a history for them.

## D029 — Speaking is a requirement of the role; belonging to the household is read, never kept

BQ-123 admits the player's own pets, residents and companions to casting. Three rules bind it.

**Social agency gates roles, not the pool.** It used to be a negative requirement applied to every
candidate before any role saw them, which is why the player's own chicken could not be the victim
of anything. Testimony, proof and standing are things an actor *does* and still require
`SocialAgency` - unknown fails closed, as the seam says it must - but being the subject of a scene
is not, so the household requirement does not ask. The casting pool is now everybody present the
registry knows as an actor and the game says is alive; who may speak is decided per role. `D026`
is otherwise unchanged: a role is still a requirement, still takes the first candidate in a stable
order, and casting still writes nothing.

**One household reading, and it is the game's.** `PlayerHousehold` reads the Home roll and the
party and nothing else. `HouseholdBond` says how somebody belongs; it never says what they are.
What species a companion is, what work a resident does and what standing either holds stay
`CharacterIdentity`'s answer (`D017`, BQ-144); whether they can carry a social role stays
`SocialAgency`'s; how far the mod may reach into them stays `NarrativeActorClass`'s (`D019`). A
companion is not more or less protected for being a chicken, and there is no second pet or
companion model anywhere in the mod.

**Membership is never stored, which is the whole of the lifecycle.** A pet sold, a resident married
out of the settlement, a companion dismissed or killed, a character the adapter can no longer
resolve - each of them stops being household because the next read says so, not because anything
was told. `Dead` and `Unknown` are treated alike, so the mod never goes on describing somebody it
cannot resolve as living in the player's home. What a scene already recorded stays true: the
binding lives on the firing, the registry keeps entries after the game stops answering for the
character (`D004`), and a save reloaded after the household has turned over completely still finds
every role holder.

Reason: attachment is the precondition for stakes, and in Elin the attachment already exists in the
player's own animals - a generated stranger has to earn what their chicken already has. The two
ways to get that wrong are both worse than not doing it: gating admission on personhood keeps the
attachment out, and recording who is household turns every sale, marriage and death into a stale
claim that outlives the thing it described.

## D030 — A speech act is a meaning, never an authority

BQ-070 adds a communicative vocabulary. Three rules keep it from becoming a second gameplay
system, which is the failure the step is most exposed to.

**Meaning decides nothing.** A speech act has no availability, no check, no outcome and no
`Perform`. It never decides whether a choice is offered, whether an attempt succeeds, what it costs
or what history records — all of that stays with `NarrativeAction` and the registry (`D016`).
`SpeechActMeaning` reads an intent BQ-134 has already projected and says what communicating it
amounts to; the dependency runs one way, and nothing in the action library asks it for permission.
The mapping is deliberately many-to-one and partial: two attempts with different consequences can
be one act, most verbs communicate nothing, and half the vocabulary has no player verb at all
because answering, denying, owning up, refusing, apologizing and passing something on are moves
inside a conversation rather than options on a menu.

**Speech has no private model of what things are about.** An act's content is an `ActionBinding` —
the same proposition, object, destination or undertaking the action layer already infers — so
nothing the simulation says can disagree with what the player was offered. What an act adds beyond
that binding is only what the binding cannot express: who is speaking, who is being spoken to, and
who the content is *about*. That last one is separate on purpose and is never reconciled against
the claim: an accusation that names somebody the fact does not is a well-formed false accusation,
and correcting it here would delete the move investigation exists to make catchable.

**An act is transient, and stance is fixed by type.** There is no save entry, because the durable
record of having spoken is an event, a belief, a memory or an obligation, and those layers already
own it; a stored act would be a second history racing the ledger (`D005`). Emotion, urgency,
publicity and social practice are likewise readings of state the world already holds and are not
copied onto the act. What the type does fix is stance — an answer affirms, a denial denies, an
apology cannot be for something the speaker says they did not do — which is the whole of what
BQ-070 owes BQ-073: "what was put forward" and "what the speaker believes" become two separately
readable things, so a lie is the gap between them rather than a fourth kind of sentence.

Reason: a dialogue layer that starts from wording can never afterwards say what was communicated,
and a speech vocabulary that starts from authority becomes the verb registry wearing a different
name. Keeping meaning downstream of the action system and upstream of every word is what lets
disclosure, lying, conversation state and realization all consume the same small contract.

## D031 — Disclosure is a decision over authoritative state, and withholding is never lying

BQ-071 decides whether somebody who holds a belief will say it to the person in front of them. Four
rules keep it from becoming either a second character sheet or a second dice layer.

**Belief is the gate, and identity is not belief.** Nothing unbelieved is ever disclosed: the
knowledge graph is asked first, and no belief ends the decision as `NothingToDisclose` before any
pressure is weighed. That state is deliberately distinct from `Refuse` — "I will not tell you" and
"I do not know" are different facts about the world. `IdentityAffordances.PlausibleKnowledgeOf` says
what a character would plausibly know and remains a casting and interpretation input (BQ-145,
BQ-064); a watchman who ought to know about a theft in his town does not thereby know about it, and
a disclosure layer that filled that gap would be inventing a fact at the exact moment the player was
told they were learning one — `D008` wearing a different hat.

**Every pressure is a reading of state that already exists.** Belief confidence and source,
`PersonalityWeights`, `RelationshipGraph` ties to the listener *and* to whoever the claim is about,
`Fact.Secrecy`, the obligation ledger, `ValueProfile`, `SensitivityProfile` and the decaying
`EmotionalStateProfile`. There is no disclosure profile on a character, no per-topic willingness
table and no accumulated standing: the balance is arithmetic performed on the spot and discarded, so
there is no second social score that can disagree with the relationships and beliefs it was read
from, and deciding writes nothing. A pressure whose state is absent contributes nothing rather than
a default, so an unmodelled character is neutral instead of quietly secretive (`D017`).

**It is a character decision, not a difficulty check.** No resolver, no `ActionContext`, no rng —
enforced structurally, not by convention. The same speaker asked the same thing in the same state
answers the same way every time, and what changes the answer is the world changing: a tie mended, a
fear decayed, a leverage spent. Persuasion stays the action layer's (`D016`) and may change the
state this reads, never this reading of it. A decision nobody can interrogate is indistinguishable
from a roll, so the inspector prints every pressure with its sign, its size and the state behind it,
and names the decisive ones by the only definition that needs no theory: those whose removal would
have produced a different strategy.

**Withholding is never lying.** The ladder has four rungs — `Disclose`, `Hedge`, `Deflect`,
`Refuse` — and none of them asserts anything false; there is no rung that could. A refusal composes
to BQ-070's `Refuse`, which carries no proposition, and a deflection composes to *no act at all*,
because the vocabulary has no `Evade` and reaching for `Refuse` instead would delete the difference
between letting a question go and turning it down. Which act carries an untruth, and how the world
records one so it can be caught, is BQ-073's. Hedging is a weaker commitment to the *whole* claim
rather than a smaller *part* of it, and the decision has nowhere to put a depth, because how much of
one fact comes out in stages is BQ-072's.

Reason: a dialogue layer that asks only "does this character know?" spends every secret in the world
on one conversation, and information stops being something the player earns from a person. Making
willingness a separate, explainable reading of state the simulation already holds is what lets
interrogation, blackmail, friendship and secrets all be the same mechanism seen from different
sides — and keeping falsehood out of it is what leaves BQ-073 a lie that is catchable rather than a
refusal that quietly became one.

## D032 — Depth is a second axis over the same decision, and it is capped rather than bought

BQ-072 asks how much of a fact comes out, not whether it does. That is deliberately not a fifth rung
of `D031`'s ladder: willingness and depth are independent, and a hedge that carried fewer
particulars than a confident answer would quietly make "less sure" and "less forthcoming" the same
thing. `DisclosureDecision` therefore gained `Depth` — `Nothing`, `Gist`, `Detail`, `InConfidence` —
beside the strategy BQ-071 already computed, and nothing about that computation changed.

**Three ceilings, and the lowest wins.** Depth is the least of what the speaker knows, what the
relationship reaches, and what their own restraint leaves them free to give. Ceilings rather than
terms in a sum, because a sum is exactly what lets a deep enough tie buy its way past a fear or past
the edge of what somebody actually holds, and neither is a thing affection does. So a frightened
witness who answers her husband anyway speaks and then stops short of how she knows: her willingness
was never in question and the fear still took the last rung.

**Knowledge is the hard cap.** Particulars require the claim to have any; provenance requires the
speaker to be able to give one — their own part in it, a teller they can name, or something they can
produce — and to hold it firmly enough to stand behind. Hearsay from nobody in particular stops at
`Detail` for everyone in the world, however close. This is `D008` and `D031`'s belief gate applied a
second time: a relationship may fail to reach knowledge and may never invent it, and a depth ladder
that filled the gap would be manufacturing detail at exactly the moment the player believed they
had earned it.

**The relationship is not the affinity number.** `Standing` reads sentiment, what the tie is (via
the same `KindBonus` willingness uses, so there is no second opinion about what a spouse is), the
obligation ledger between the two — a kept promise, a shelter still standing, a broken promise, an
open grudge, bounded so a ledger of small favours cannot outweigh what the two people are to each
other — and whether the listener holds a tie back at all. It is derived on the spot and stored
nowhere, exactly as `Balance` is, so there is still no standing that can drift out of agreement with
the ties and debts it was read from.

**Every rung is the truth.** A shallower answer is a smaller true answer, never a shaded one; there
is no rung on which somebody misleads, and shading remains BQ-073's. `Compose` is untouched, because
depth changes what a realizer has to say and never which act it is or which claim it names — that is
BQ-074's.

Reason: without depth, a relationship only decides whether a fact is spent, and every informant is a
switch — the stranger gets nothing, the friend gets everything, and there is nothing left to earn
from somebody who already answers. Making depth a capped reading of knowledge, tie and restraint is
what turns a mended relationship into more of a fact while keeping fear, loyalty and privacy as
things that hold, and keeping the world's knowledge as the thing nobody can talk their way past.

## D033 — Wording is a reading of meaning, and the words never learn that somebody is lying

BQ-074 adds the first layer in the mod that produces English, and everything about its shape is
chosen so that producing English cannot produce meaning. The realizer takes a fragment library and a
request and no world at all — not a read-only one, not a scoped one — so "realization writes no
world state" is a fact about its signature rather than a discipline about its body. What a fragment
may be selected on is a closed vocabulary of *readings* of the act and the disclosure decision
behind it, every one of them derived from state that existed before any of this ran. What a fragment
may name is a closed set of placeholders resolving to people the caller put on stage and the label
the claim already carries; a placeholder nothing fills makes its fragment ineligible, because
referring to somebody the caller did not name is the smallest possible version of inventing a fact
about them.

**There is no placeholder for the claim.** Putting a proposition into words needs a predicate
lexicon, and a lexicon that phrased predicates would be a second place where what a fact says gets
decided — one that could disagree with `FactPredicates` and with the ledger. A fragment that wants
to word a particular kind of claim conditions on `claim_predicate` and writes the sentence itself,
which keeps the English in content and the ontology in Core.

**Fragments are content, not code.** There is already one authored-content pipeline with a compiler,
a bundle, freshness checking and diagnostics that point at a file; a table of English in a headless
simulation assembly would be a second way to add a line and a second way to break one. Loading is
strict for the same reason composing an act is: a fragment with a misspelt condition is not a
fragment that says slightly less, it is one that says the wrong thing in the wrong situation forever
and nobody finds it.

**Refusal, never repair.** An act nothing has words for comes back unrealized with a reason and no
text, and a request whose parts describe a situation the semantic layer never produced is refused
rather than reconciled. A line that says less than it should is a content bug and recoverable; a
line that says something nobody decided is a world bug and is not.

**The words never learn that the speaker is lying.** A decision whose tactic is `Falsify` reaches
realization as though no decision had been given, and `tactic: falsify` is not a value content may
name. So a liar's denial draws from exactly the fragments an honest denial of the same claim draws
from and, at the same seed, says the identical words. This narrows what wording may read; it does
not rewrite what was decided — the `DisclosureDecision` is untouched, `WillLie` still reads true on
it, and `Deception` still classifies from the belief graph. `D031` holds that withholding is never
lying and BQ-073 holds that a lie is a stance against the speaker's own belief rather than a way of
speaking; a fragment pool that shifted when somebody falsified would contradict both, by putting the
tell in the words and making a lie catchable by ear instead of from what the listener knows.

Reason: the expression layer is where every earlier guarantee is cheapest to lose. Meaning is
authoritative, proof is separate from belief, and a lie is a relation between what was said and what
was held — and all three survive exactly as long as the sentence that finally gets spoken is a
choice among authored ways of saying what was already decided, rather than a place where something
new can be said.

## D034 — Voice is a source for tone, never a second personality or a rewrite of meaning

BQ-075 gives BQ-074's `RealizationRequest.Tone` its first producer. `VoiceProfile` carries four
0..1 axes — `Formality`, `Directness`, `Sarcasm`, `Warmth` — and one pure function, `RequestedTone`,
that maps them onto `DialogueTones` tags: `formal`/`plain` from `Formality`, `curt`/`wary` from
`Directness`, `warm`/`cold` from `Warmth`, `wry` from high `Sarcasm`, and nothing from low `Sarcasm`
because sincerity has no tag of its own and does not need one. Between the four axes, every tag in
BQ-074's seven-tag vocabulary has exactly one axis that can ask for it, so voice is a stance on the
vocabulary BQ-074 already shipped rather than a second one layered beside it.

**Voice narrows through the tone check BQ-074 already had, so it inherits that check's guarantee for
free.** `RequestedTone` only ever reaches `DialogueFragment.FitsTone`, the mechanism that already
could only shrink or hold a fragment's eligibility, never touch `Requires`, `Forbids` or any
`DialogueReadings` key. A voice therefore cannot move `RealizedLine.Meaning` for the same reason a
raw `Tone` list already could not — this step needed no new guarantee, only a caller for the one
that existed.

**Voice does not read personality, and personality does not read voice.** `VoiceProfile` has no
field, constructor or method that touches `PersonalityWeights`, `DisclosureDecision` or `SpeechAct`.
Two speakers who want the identical thing can sound nothing alike, and two who want opposite things
can sound the same, because nothing wires the two together. This is the literal reading of "voice
and personality are related consumers, not interchangeable concepts": both narrow choices made
elsewhere, and neither is allowed to narrow the other.

**No profile is ever derived from who a character is.** There is no factory from race, archetype,
occupation or hobby to a `VoiceProfile` — that lookup table is the stereotype BQ-076 is written to
avoid by reading only work actually observed, and voice sits a layer below where any of those labels
live. A profile is simply given to whoever is speaking; what assigns one is a later, authoring-side
concern this step does not reach.

**Two axes named in the roadmap line are not fields.** Sentence length and metaphor use appear in
CD §19's struct and in this step's own summary, but no shipped fragment carries a length or
figuration marker for either to select between. A field that could never narrow anything is a
seam pretending to be a mechanism; leaving them undeclared until content exists to read them is the
same call BQ-074 made keeping `DialogueTones` to seven tags in the first place.

Reason: BQ-074 built the guarantee that expression cannot become meaning once and put the seam for a
speaker-level source of tone in plain sight (`RealizationRequest.Tone`'s own doc comment names
BQ-075 as the filler). Reusing that seam rather than adding a parallel one keeps "voice narrows,
never creates" a single mechanism instead of two that could disagree, and keeps personality and
voice from collapsing into each other the moment someone reaches for a stereotype as a shortcut.

## D035 — Occupational vocabulary reads only BQ-145's derivation, and an unrequested tag excludes rather than admits

BQ-076 adds `RealizationRequest.Vocabulary`, `DialogueFragment.FitsVocabulary` and
`OccupationalVocabulary.RequestedVocabulary(IdentityAffordances)`. The derivation takes an
`IdentityAffordances` — BQ-145's own output — and nothing else: it reads `PlausibleKnowledge` and
`PlausibleInterests` for the domains BQ-145 already attributed to a work, hobby, service or
institutional facet, and maps each to one `DialogueVocabulary` tag. There is no second read of
`CharacterIdentity`, no substring table over vanilla ids, and no path from `Race` or
`CharacterArchetype` to a tag, because `IdentityAffordances` already derives nothing from either —
this step inherits BQ-145's anti-stereotype gate rather than re-implementing it.

**A vocabulary tag excludes by default; a tone tag does not.** `FitsTone` treats an empty request as
"no constraint," so an unmarked *and* a tone-marked fragment both stay eligible when nobody asked
for a tone. `FitsVocabulary` cannot use that rule: a fragment carrying a `DialogueVocabulary` tag is
eligible only when the request names that tag, and an empty or null request excludes it. Reusing
tone's "ask for nothing, get everything" semantics here would let a flavoured fragment through for
every unread identity, which is the guessed vocabulary D017 and BQ-145 both already refuse. A
fragment carrying no recognised vocabulary tag is unaffected either way, which is what keeps this
free tags field — `DialogueFragment.Tags`, shared with BQ-077's future negative-space tags — usable
by more than one consumer without either one having to know about the other's vocabulary.

**Presence, not magnitude, drives what gets requested.** Every domain BQ-145 derived at all is
requested, whatever its plausibility; subtlety comes only from a flavoured fragment joining the same
selection pool a plain one already fits, at the same odds as any other candidate, never from
replacing the plain wording or from a threshold gating whether the tag fires at all.

Reason: BQ-145 is "the one derivation of what identity implies," so a second place that decided which
jobs sound like what — even indirectly, by re-reading `CharacterIdentity` or by admitting an
occupational fragment when nothing was asked for — would reopen the anti-stereotype gate BQ-145 was
its own step specifically to close.

Add a new entry only when the decision is both load-bearing and durable.
