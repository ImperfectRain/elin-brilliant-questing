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
fragment carrying no recognized vocabulary tag is unaffected either way, which is what keeps this
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

## D036 — A prohibition removes an option before the choice, and breaks only for a pressure already on the record

BQ-077 gives a character `NegativeSpaceProfile`, a small set of `PersonalProhibition` lines held
beside `ContradictionProfile` and `CharacterQuirkProfile` and saved with them. Four rules keep it
from becoming either a second personality model or a wording filter.

**A prohibition names a move, never a disposition.** Every member of the vocabulary points at
something this simulation already selects: `NeverBegs` and `NeverInvolvesAuthority` at
`ProblemSolvingStyle` candidates the goal pipeline scores, `NeverLiesDirectly` at BQ-073's
`DisclosureTactic.Falsify`, `NeverSpeaksBadlyOfFamily` at putting a discrediting claim about kin
forward at all. That is what separates a line from a trait, and it is the whole reason the list is
four entries rather than one per `PersonalityWeights` axis: `Honesty` is a slope that makes lying
less likely as it rises, and `NeverLiesDirectly` is a declaration that survives a low slope. A
character who trades sharply and still will not tell you a flat untruth is not expressible as a
number, and turning every trait into a prohibition would lose the distinction in the other
direction.

**It removes an option; it never moves a number.** The goal pipeline still scores every candidate,
including the forbidden one, and then chooses the best-scoring *permitted* one — so the trace shows
the action that was lost, still scoring highest, beside the line that took it. `DisclosureDecision`
gains `Prohibitions` and not another `DisclosurePressure`: a line is not summed into `Balance`, and
the inspector prints the two apart, because a prohibition rendered as a large weight is
indistinguishable from a strong preference and "will not" collapses back into "would rather not".
This is also why the disclosure balance is byte-identical whether or not the speaker holds a line
about the claim.

**Breaking needs pressure the surrounding decision already established.** `NegativeSpaceProfile.Rule`
takes the pressure as an argument and computes none: the goal pipeline passes the need pressure it
already derived from the threatened value, and `Disclosure` passes how far its own weighing ran past
the threshold the forbidden move needed. A line breaks when that reaches its firmness, and the
ruling carries the reason in words — which is what makes a break an event with a cause rather than a
fallthrough. An unbreakable line is a fact about the character and still not a fact about the world:
the move stays available to everybody else, so a prohibition is never a physical impossibility.

**Wording carries out a constraint; it never decides or hides one.** A prohibited move is refused
where it would have been selected, so it never reaches a `RealizationRequest` at all — a speaker who
will not speak badly of her brother has already composed a `Refuse`, not a gently-worded answer.
What remains for realization is the register: `RealizationRequest.Forbidden` carries
`DialogueManners` tags and `DialogueFragment.FitsManner` excludes fragments in them, and it is fed
*rulings* rather than a profile so a line that broke stops constraining the words too. Only
`NeverBegs` has a manner, because it is the only line that leaves a sentence behind to filter — it
takes an action off the table and takes the appealing register out of the questions the same person
is still willing to ask. Pairing a manner with `NeverSpeaksBadlyOfFamily` would author a rule that
could never fire.

**Nothing derives a line from identity.** There is no constructor from `CharacterIdentity`,
`IdentityAffordances`, race, character archetype or occupation anywhere in this step's surface, held
structurally by test rather than by convention. A guard may hold `NeverInvolvesAuthority` and a
thief may not.

Reason: what somebody will not do is more recognizable than another positive trait (`CD §17.7`), but
only if it costs them — a prohibition enforced after selection, or only in wording, is a character
who does the thing and describes it differently, which is worse than not modelling it at all.

## D037 — Repetition avoidance only removes candidates, and a required slot reuses where an optional one falls silent

BQ-078 gives `DialogueRealizer` a consumer for the `RepetitionGroup` BQ-074 declared and left
unread: `DialogueExpressionHistory`, tallying recent fragment ids, repetition groups, BQ-076
vocabulary tags and tone tags against a small cap, and `RealizationRequest.History` as the seam a
caller opts into or leaves null.

**Narrowing only ever shrinks a pool `Candidates` already built.** `IsFresh` is asked only of
fragments `Fits`, `FitsTone`, `FitsVocabulary` and `FitsManner` already admitted, and the narrowed
set is always a subset of that pool, never a superset. Repetition avoidance cannot be the reason a
line says something it should not, because it is never in a position to add a candidate — the
existing eligibility checks are the only place a fragment enters consideration at all.

**A required slot degrades by reuse; an optional one degrades by silence.** The core slot cannot be
skipped, so when every fresh core candidate is spent it falls back to the full eligible pool and
says a valid line again rather than inventing an ineligible one — the same "refuse rather than
repair" instinct BQ-074 already held, aimed at variety instead of meaning. Every other slot could
already say nothing (D033's "not every line has every part"), so an exhausted optional slot needs
no fallback of its own: it is simply skipped, the same way a slot with zero eligible candidates has
always been. This split, not a uniform cap, is what makes "no opener more than twice" a guarantee
rather than a likelihood — an opener can never be reused past the cap, because reuse was never its
degrade path.

**Semantic-act tallies are kept but never chosen on.** CD §21 names the act as a repetition axis, so
`NoteAct` records it, but nothing narrows a slot on it: every core candidate left at a slot already
answers the one act the request carries, so the axis cannot distinguish between them without
tracking a sequence of acts across a conversation — a form of state this step does not add, because
it borders BQ-083's ground rather than BQ-074's.

**The history is not conversation state.** It holds counts of wording already spoken, nothing a
character believes, is owed, or remembers; it is not attached to `NarrativeNpc`, not saved, and
built and discarded per conversation by whichever caller owns one. BQ-083's conversation state,
when it exists, is a different structure answering a different question, and nothing here should be
mistaken for an early draft of it.

Reason: a repetition fix that could occasionally choose a fragment nothing licensed, or that could
leave a required slot silent when the simulation needs a line, would trade a correctness guarantee
for smoother-sounding dialogue — exactly backward for a layer whose whole job is to never invent.

## D038 — A reaction is what an interpretation is worth to somebody, and it has no words

BQ-080. `ActorReaction` sits between BQ-064's interpretation and BQ-074's realization and answers
two questions BQ-064 does not: what about the event is *mine* (`Concern`) and what would I *do*
about it (`Response`), plus how odd the premise strikes me (`Registers`). CD §22.3's pragmatist,
zealot and merchant are not three wordings of one remark — they are three different things noticed
and three different next moves — so a layer that made them differ by adjective would have satisfied
the sentence and missed the requirement.

**Every axis is a vocabulary the simulation already had.** `ValueConcern` (BQ-061),
`ProblemSolvingStyle` (BQ-062), `WeirdnessLevel` (BQ-079), and BQ-064's own trace carried whole. The
step adds no reaction ontology and no second personality model, because the actor state that
decides what somebody wants already exists and a parallel copy of it would be a second opinion
about who they are. Where two of those vocabularies had to be put in correspondence — which
sensitivities and which identity domains bear on which concern, what answering a concern reaches
for — the correspondence says what a concern is *about*, never what a person of some kind believes,
and the actor's own profile outweighs it every time.

**Concern pressure keeps one formula.** `engagement × importance × (1 − flexibility/2)` is
`MissingGoatProblemSolver`'s, unchanged, so "how hard does a threatened value press" has one answer
in the simulation rather than two that drift apart.

**Nothing in a reaction reads the event's prose.** Not `Fact.Value`, not a label, not a tag written
for one occasion. Retitling an event therefore changes no reaction to it, which is what makes "no
bespoke text for that event" a property of the type rather than a discipline about content — there
is nowhere for such text to be read even if somebody wrote it. The event itself is carried by id and
never rewritten: two actors reacting produce two reactions and one unchanged fact.

**A reaction is a meaning, so it stops before wording.** Saying one aloud needs fragments authored
for reactions, which are content (BQ-132) and do not exist; authoring them here to make five
reactions audible would have been exactly the bespoke text the step forbids. `Registers` is the seam
a later step hands `RealizationRequest.WeirdnessBudget`, so a reaction stays drier than the event it
is to (CD §22.4).

Reason: the difference between characters has to live in what they conclude and intend, where the
rest of the simulation can act on it, rather than in how a line reads. A reaction layer that
produced text would have put personality in the one place nothing downstream can consume, and would
have made every new event need new writing to be reacted to at all.

## D039 — A callback is a reference to history somebody is entitled to make, never a second copy of it

BQ-081. When a scene refers back to something that happened, the material it refers to is derived
from `EventLedger` at the moment it is asked for. There is no callback store: no ledger, no index,
no cache, no save entry. This is BQ-034's own shape — `Chronicle` is a reading of history rather
than a second copy — applied to the other direction of travel, and it is what makes the persistence
question have no answer to give. Everything a hook names is already in the save, so it survives a
reload for the reason the ledger does, and a retracted or corrected event takes every hook to it
with it.

**A hook is a reference, and the wording layer never gets more than one.** `CallbackHook` carries an
event id, participants, objects, place and thread as stable ids, and readings computed from the
event's own recorded fields. No prose, no summary sentence, no phrase written for the occasion —
so retitling an event changes every callback to it and there is nowhere for a detached retelling to
live. Realization is told three things about a hook and no more: which kind of material it is, where
its other party is standing, and which way round it went. Referring to an event and asserting what
happened in it stay different acts.

**It is derived per recaller, so the knowledge gate is the type rather than a rule.** There is no
callback the world holds — only one somebody is entitled to make. `CallbackRoute` admits the actor,
the target of anything not tagged `unnoticed`, the event's own listed witnesses, and a confident
believer in a claim the event is the `OriginEvent` of; a garbled version of that claim is knowledge
of a story, not of what happened, and is refused. An event nobody has a route to produces no hook,
and `RealizationRequest.WhyNot` refuses a hook belonging to somebody other than the speaker, so
there is no path from history somebody could not know to a line about it even for a caller
assembling a request by hand. This is the same rule that keeps background simulation from granting
omniscience, read at the point where history becomes speech. It is a gate on *knowing* and only on
knowing; whether the speaker would say it to the person opposite is a second question, and D044
settles that one.

**Honest about people who are no longer there — which is not the same as silent about them.**
`CallbackParty` reports what the world can still produce of the other side, and two different
questions are read off it. Whether somebody can still be *referred to* fails only for a party the
registry cannot produce at all (`IsReferable`); whether they can still be *staged* fails for the
dead as well (`IsStageable`). Selection admits the referable by default, because the dead and the
departed are exactly what a settlement keeps talking about and dropping them would delete the most
durable callback there is. `CallbackSelection.Parties` is how the narrower question is asked, by the
caller whose use of the hook needs the person themself. Selection is deterministic — salience
descending, ties on event id — because a save reloaded mid-conversation has to offer the same
material in the same order.

**A second context has to be proved, never assumed (BQ-082).** `CallbackRecurrence.IsUnrelatedContext`
rules a hook out on any dimension the two sides share, and rules it *in* only where at least one
dimension is known on both sides and differs. Unrecorded context establishes nothing in either
direction: "neither of us recorded a thread" is not the claim "our threads differ", and reading it
as one made an event with nothing recorded about where it happened resurface everywhere, including
where it actually happened.

Reason: the alternative is a second history. A stored hook is a copy of an event that can outlive
it, disagree with it, need migrating, and — worst — be written for one occasion in words nobody can
check against what happened. Deriving instead means callbacks cost nothing to keep, cannot lie about
the past, and cannot be made available to somebody who was not there.

## D040 — Conversation state compares statements to statements, and a commitment survives only when something outside the conversation says so

BQ-083. `ConversationState` holds one conversation's transcript - every `SpeechAct` exchanged,
which questions are still hanging, and every lie BQ-073 already recorded - and answers "have I
heard this before" and "does this square with what they said earlier" from nothing but that
transcript. Two boundaries keep it from becoming the second belief graph, event ledger or
obligation system the step explicitly rules out.

**Self-contradiction is not `Deception.Contradictions`.** BQ-073 already catches a liar: an
observer holds a belief a recorded statement cannot be squared with, read from durable history and
needing the observer to have been there. `ConversationState.Contradicts` answers a narrower,
cheaper question - does this speaker's new statement conflict with an earlier statement of their
own, both already inside this conversation - and needs no belief graph and no event ledger to do
it, only the transcript. The two shapes it checks are deliberately the same two `Deception.Assess`
already treats as insincere against belief (the same claim reversed, or a rival version of it via
`Fact.DistortionOf`), read statement-against-statement instead of statement-against-belief, and the
private `Rivals` test BQ-073 used for the second shape is now `internal` so both read one
definition of what counts as a rival claim rather than growing a second opinion about it. Reaching
further back than the current conversation, or catching a lie one person tells another behind a
third party's back, stays `Deception.Contradictions`' job.

**A promise is a speech act, not a shadow structure conversation state invents.** BQ-070's
vocabulary gains `SpeechActType.Promise` the same way it gained `Evade` for BQ-073: a consumer
needed a distinction the ten-act table could not express, so the table grows by one row rather than
by conversation state keeping its own list of "things that sounded like commitments." Stance is
`None` and direction is the new `CommitsToAction` - a promise is not true or false at the moment it
is spoken, so `Deception` correctly reads it as asserting nothing, and whether it was kept is a
question about later behaviour, answered by `PromiseBroken` and the standing sheet, never by this
step.

**Promotion to durable is a call, never a consequence.** `ConversationState.Commit` writes a
`WorldEventType.PromiseMade` event and a `SocialObligation(Kind.Promise)` naming it as source - the
same event-then-obligation shape `ConsequenceEngine.AccrueFavor` already uses for a kept favour,
into the same ledger BQ-071's disclosure pressure, BQ-077's negative-space lines and the standing
sheet already read. Nothing about noting a promise triggers this: every act is noted for the
transcript regardless of type, and only a promise a caller explicitly hands to `Commit` becomes
durable, guarded against being committed twice. A conversation that never calls it leaves no trace
once it ends, which is the whole of how transient debris stays transient.

**Nothing here is saved.** `ConversationState` has no schema, is never attached to `NarrativeNpc`,
and is built and discarded per conversation exactly as `DialogueExpressionHistory` (BQ-078) already
is - `docs/agent/decisions.md`'s own D037 said this in advance, and this type is what it was said
about.

Reason: a conversation-state layer that read or wrote durable state directly would be a second
opinion about belief, history or obligation that could drift from the one the rest of the mod
already trusts; deriving from statements already in hand and writing through the one obligation
ledger that exists keeps there being exactly one authority for each of the three.

## D041 — The realization vocabulary is derived from the semantic one, never kept alongside it

BQ-083 added `SpeechActType.Promise` and `SpeechActDirection.CommitsToAction` to the vocabulary of
meaning and did not add them to `DialogueReadings`, which held a hand-written copy of the same
vocabulary for content validation to check against. Nothing failed loudly. A promise composed
correctly, read correctly as `act: promise`, and could not be authored a wording at all — content
validation rejected the only condition a core fragment for it could declare — so the realizer
refused the line, naming a fragment that was missing rather than the vocabulary entry that made it
unwritable. Two vocabularies of the same thing had drifted, and the layer whose whole purpose is to
express meaning without defining it could no longer express one of the meanings.

**The values come off the enums.** `act`, `stance`, `direction`, `reply`, `strategy`, `depth`,
`tactic`, `callback` and `callback_route` are built by enumerating the semantic type each of them
reads, through the one slug rule `RealizationReading` itself uses (`DialogueSlug`). The keys whose
values are not a semantic enum — `referent`, `claim`, `audience`, `commitment`, `held_back`,
`callback_party` — stay written out, because they are readings computed about the shape of an act
rather than names the semantic layer already holds, and there is nothing to derive them from.

**Deriving is not collapsing.** The arrow points one way and gains no second head: the semantic
layer still decides what acts exist and what they mean, and wording is told what those are in
exactly the words they already have. Nothing in `DialogueReadings` can add a value, remove a
meaning, or repair an act — a value outside the semantic layer is rejected at load exactly as
before, and the realizer still refuses an act nobody has authored words for rather than
approximating one.

**The one deliberate gap stays deliberate.** `DisclosureTactic.Falsify` is subtracted from the
derived set by name, because wording is never selected on whether the speaker is lying (D-pipeline
and BQ-073). Subtracting it is different in kind from never having listed it: every other tactic
the semantic layer grows arrives in content's vocabulary the moment it is declared, and the only
value that does not is the one an explicit line of code removes.

Reason: a hand-kept copy of a vocabulary is a copy somebody has to remember to update, and the
failure when they do not is silent, distant from its cause, and looks like missing content. A
derived vocabulary cannot drift, so "expression may express meaning and may never create it" stops
depending on two lists agreeing.

## D042 — A conversation promotes its own promises, and a promise to several names who is owed

Two holes on the same call, both of which let `ConversationState.Commit` write a durable
`SocialObligation` on a caller's say-so rather than on what the conversation heard.

**Only a noted act may be promoted.** `Commit` accepted any well-formed promise, including one that
was composed and never said, or one belonging to an exchange that had already ended. The durable
ledger would then carry an obligation whose only witness was the call itself. Being `Note`d is what
makes an act this conversation's, and it is now what `Commit` requires — identity, not equivalence,
because two promises of the same thing by the same person are two promises and only the one this
conversation actually heard is its to vouch for. This does not add a second commitment system: it
is the existing transcript being consulted before the existing doorway opens.

**A creditor is named, never taken from the audience order.** `SpeechAct` sorts its addressees by id
and says outright that the order is staging rather than meaning, so `Addressees[0]` is not a fact
about a promise — it is a fact about how two ids happen to sort. Taking the creditor from it made
"who is owed this" depend on an accident, silently. A promise to one person still needs nothing from
the caller: that person is the creditor. A promise made in front of several must say which of them
it is to, and anybody else addressed is recorded as a witness to the event, which is what they were.
Naming somebody who was not spoken to is refused.

Refusing the unnamed multi-addressee case rather than picking is the point. `SocialObligation` has
one debtor and one creditor; a promise owed to a group is an obligation kind the ledger does not
model, and inventing one here to avoid returning null would be conversation state growing the
obligation system rather than using it.

**Filing a lie twice files one lie.** `NoteDeception` appended whatever it was handed, so a caller
that noted a deception where it happened and again while sweeping the ledger recorded one event as
two — `LiesTold` would have counted method calls rather than lies. Identity is the ledger entry's
own id, the only identity a statement read back out of history has. Two separate deceptions remain
two, because they are.

Reason: everything durable this type can cause comes out of one call, so that call is where the
conversation's authority has to end. What it heard, it can promote; what it did not hear, and who
it cannot say was owed, are not its to decide.

## D043 — A tone request is a position on axes, and a premise is named by content rather than by genre

Three defects in how BQ-075 through BQ-079 narrow the fragment pool, all of the same shape: a filter
that looked like a constraint but did not actually constrain.

**Tone tags are the marked poles of four axes, not seven alternatives.** `FitsTone` admitted a
fragment when any of its tone tags matched any requested one, so naming more axes *widened* the pool
— a voice specified as formal, curt, cold and wry admitted the union of four tone pools, leaving a
strongly specified voice less constrained than a one-axis one. Worse, a fragment marked `formal` and
`curt` passed a voice that had explicitly asked for `plain`: one axis matching re-admitted a fragment
the other axis contradicted. `VoiceProfile` already read the tags as four axes — one axis requests
one tag — and this is the half the tags themselves were missing. `DialogueTones.Opposite` pairs
formal with plain, curt with wary, warm with cold, and a fragment is now refused exactly when one of
its own marks takes the opposite pole on an axis the caller took a position on. Marks on axes the
caller said nothing about are left alone, which is the same "requesting nothing narrows nothing" rule
`VoiceProfile.Neutral` relies on, applied one axis at a time instead of only to the empty request.
Naming an axis can therefore only ever remove candidates, and unmarked fragments stay safe fallback
material for every voice.

Sincerity has no tag, so nothing contradicts a wry fragment. That is a gap in the authored
vocabulary, not in the reading, and closing it means shipping a tag content would have to start
using — which BQ-075 declined to do for sentence length and metaphor for the same reason.

**One absurd premise, not one absurd genre.** `WeirdnessBudget` committed to a *category*, so two
unrelated bizarre tax premises — both bureaucratic — stacked in one scene while the rule that was
supposed to stop them reported success. A category is a taxonomy; what distinguishes follow-on
material about the scene's premise from the start of a second is which premise it is about. A
`premise_` tag in the existing free tag list names it, disjoint from the category and level families
and read from authored content exactly as they are, and untagged premise-level content falls back to
the fragment's own id — unnamed is not shared, so a second unnamed premise never passes as more of
the first. The category is still recorded, and is still what a distribution check reads; it simply is
not what the anti-stacking rule compares. Nothing here decides what an absurd premise *is*: the tag
is opaque, and its only meaning is "same string, same premise".

**The core is entitled to the premise before an optional slot can spend it.** The realizer computed
the core pool once, before the loop, then spoke the opener first — and the opener could `Note` a
premise of its own while the core still chose from a pool computed before that opener existed. A line
could open on one absurd premise and make its actual point on a second, against the invariant the
budget exists to hold. The core is the fragment that has to be said, so it is now chosen and noted
first; it is still spoken, and still noted into the expression history, in its own place in the line.
The pick comes from the same forked stream over the same candidate list, so no output changes on
account of the reordering alone.

Reason: a narrowing that can be satisfied by an accident — another axis happening to match, another
premise happening to share a genre, a pool computed before the thing that should have shrunk it —
is not a constraint. Semantic correctness outranks stylistic variety, so each of these gives up some
breadth of pool to say something true.

## D044 — Remembering is not telling: a callback carries a clearance for one listener, not a hook

BQ-081 derives a hook per recaller, which makes "they could not possibly know that" structurally
impossible: material nobody had a route to does not exist to be spoken. That gate has no listener in
it, and the gap it left was real. A claim held at secrecy 100, whose holder `Disclosure` would refuse
to state if asked outright, could still be handed to `DialogueRealizer` as old business and come out
as "I know what {recalled} is said to have done". `RealizationRequest.WhyNot` checked that the hook
belonged to the speaker and nothing else, so recall permission was being spent as disclosure
permission by any caller who did not remember a convention nothing enforced.

**The seam is a permit, and `RealizationRequest` takes one instead of a hook.** `CallbackPermit` has
an internal constructor, so the only thing that produces one is `CallbackDisclosure.Permit`, and the
only way it produces one is by asking `Disclosure`. A request carrying a permit that was withheld, or
one cleared for anybody but the single person the act addresses, is refused the way the other
malformed requests are — refused rather than quietly worded without the callback, because a line that silently
lost its reference would leave the caller believing a permission question had been answered when it
had only been discarded. The shape is BQ-081's own, applied to the gate BQ-081 left to convention.

**It adds no authority.** Every answer comes from the same `Disclosure.Decide` that settles
willingness for every other claim, asked about the claims the recalled event already named.
`CallbackHook.Claims` carries those as ids — the event's own `Related`, filtered to what the
knowledge graph resolves — so the permit needs no rescan of the ledger and, more to the point,
invents nothing to ask about. There is no callback-specific willingness, no second secrecy model and
no fact minted to stand for "what the callback is about", which would be exactly the second fact
system a hook exists not to be. Holding no belief about a named claim is not withholding it:
`NothingToDisclose` passes, so a witness who was there without forming a view is not silenced.

**What it does not reach is empty rather than open.** An event that named no claim has nothing for
disclosure to weigh and is always permitted. That is honest: with no claim recorded and notice
suppressed, the only surviving route is `FirstHand` — `unnoticed` closes `Involved` and `Witnessed`,
and `Heard` requires a claim to have been believed — so the speaker is the one it happened by,
talking about themselves. There is no third party whose secret could pass through a gap of that
shape.

**Selection had to move with it.** `CallbackHooks.Best` returning the most salient hook and the
caller then finding it unsayable would lose every perfectly sayable callback standing behind it, so
`CallbackDisclosure.Best` and `BestRecurrence` walk the same salience order and return the first the
speaker would actually spend on this listener. Order is not re-sorted by willingness; withheld
material is stepped over.

Reason: knowing and telling are different questions with different answers — that is the whole of
BQ-071 — and a callback is the one place the simulation had them collapsed. Keeping the answer where
`Disclosure` already lives means a mended tie buys a callback the same way it buys an answer, and
realization still reads no world state: the clearance is taken where the world is in hand and merely
honoured where the words are chosen.

## D045 — Elin's work column is an observation, and only a recognized trade is evidence of a livelihood

`SourceChara.job` is a build column before it is an occupation. Live diagnostics have it reporting
`predator` for shopkeeper-like NPCs *and* for horses, `tourist` for nuns, and combat job templates for
bartenders. BQ-144 was reading it correctly and BQ-145 was over-reading it: knowledge, interest and
role eligibility were all gated on the domain vocabulary, but the livelihood stake fired for any
known work id, so a horse acquired a trade to lose.

**The observation is unchanged and stays verbatim.** The facet is named `Work` — the column — and
never `Occupation`, and it says so where a consumer would otherwise assume. An unrecognized id is
still carried through as itself, because it is a stable discriminator and it is what Elin said.

**A livelihood is derived only where the id reads as a lived trade**, under the same vocabulary the
domains already use, so there is one answer to "does this read as work" rather than two that can
disagree. Observed service derives a business independently and off the trait subclass, so a
shopkeeper whose work column says `predator` is still a shopkeeper; an office derives standing
independently, so a guard is still a guard. Unrecognized work costs the livelihood and nothing else.

Reason: the anti-stereotype gate is about not letting a label decide what somebody is, and asserting
a livelihood from a job template is the same failure as asserting a temperament from a race. Race
and character archetype were gated from the start; this was the one facet still ungated, and the
game's own data is what proved it needed to be.

## D046 — One live Chara uid is one participating BQ actor, and a superseded id is retired rather than erased

Live diagnostics found one physical character registered under two BQ ids at once — an authored
`npc_...` for somebody the mod staged, and an `npc_vanilla_<uid>` minted the next time the zone was
walked. Zone registration minted an id before asking whether the character already had one. Casting,
familiarity, beliefs, callbacks, relationships and history all assume an `EntityId` names one person,
so all six were wrong at once and each looked like a different bug.

**Canonicalisation happens at intake, not in consumers.** `ElinBindings.CanonicalIdFor` is the one
way a live character becomes an id: the id they already have, or a new one bound to them now. The
uid→id map keeps its incumbent rather than being overwritten, so the answer to "who is this
character" cannot change underfoot. The two pure reads that must not register — the Home roll and the
party — still derive an id, and still ask for the existing binding first.

**A superseded id is retired, never deleted or repointed.** `EntityRegistry.Retire` marks one record
as an alias of another. Both survive, both are saved, and `GetNpc` still returns either — the events,
beliefs and threads written under the alias are true, and rewriting them would invent a past in which
somebody else did those things. What the alias loses is participation: `Registry.Npcs` is the actors
and `Registry.AllNpcs` is the records, so nothing that asks who is in the world can cast or simulate
it as a second person, while save/load and existence checks over history see everything.

Reason: identity stability is the assumption six systems rest on, so it is enforced once where ids
are minted rather than checked six times; and history is evidence, so a duplicate is resolved by
saying which id is the actor, not by editing what was recorded.

## D047 — A bounded search spends its budget only on branches that could be chosen, and says when it stopped early

`StoryletCasting`'s group search keeps both of BQ-068's bounds: a role shortlists at most a handful
of the people it admits, and one pass weighs at most a fixed number of complete groups. Finding
scenes must not cost more than playing them, and neither number is up for negotiation on the
strength of a preference nobody would notice.

**The budget buys scoring, not walking.** The search used to count every assignment it enumerated,
including the ones where an early role had already taken the only person a later required role could
have had. Those assignments are not groups — nothing scores them, and no cast was ever preferred to
one — but they spent the bound just the same, so with enough qualified people the search could run
out inside them before reaching a single complete cast. What came back was `required role ... cannot
be cast`: the sentence the engine uses when *nobody qualified*, about a role somebody was standing
there to fill. A branch that has starved a required role is now skipped rather than walked, which
removes no group any score was taken from and restores the one thing group formation exists to add
over BQ-067 — backtracking that actually reaches the cast behind the obvious wrong first choice.

**The fallback is taken outright rather than found.** BQ-067's answer is each role filled, in binding
order, with the first candidate nobody ahead of it took, and that used to be read off whichever leaf
the walk happened to reach first. Skipping branches would have taken the fallback with them, so it is
now computed directly. It is the same group either way, which is what keeps an uncastable storylet
naming the same role it always named.

**Reaching a bound is reported, never inferred.** `GroupsConsidered` counts groups scored, which is
what its name always claimed; `SearchTruncated` says the walk stopped on the group bound rather than
running out of groups; `CandidateBoundReached` says a shortlist filled up with people still
unexamined. `NarrativeInspector.DescribeCasting` prints all three. Without them the report read
`over N qualified groups` whether it had weighed all of them or the first hundred and twenty-eight,
and "why these people rather than the others who also qualified" quietly claimed an exhaustiveness a
bounded search does not have.

Reason: a bound is a legitimate answer and an unspoken bound is not. The cost of the bound must fall
on preference — a slightly worse cast — and never on correctness, and a reader must be able to tell
a search that finished from one that gave up.

## D048 — The laboratory has one dispatch authority, and the integration harness keeps its own command line

`tools/BrilliantQuesting.Lab` is a set of registered scenarios behind one command line. A scenario
declares its id, summary, description, aliases, options and default seed, and runs once against a
resolved context; `LabCatalog` is the only place that knows which scenarios exist, and
`LabCommandLine` is the only place that reads a command line. `Program.Main` is one call into it.

**Registration is local, dispatch is not.** Adding an experiment is a new `LabScenario` subclass plus
one line in `LabCatalog.Default`. The chain of `args[0] == "--flag"` checks that used to live in
`Program.Main` re-implemented seed parsing, argument parsing and exit status once per mode, and grew
by one branch per experiment; the shared concerns now sit in the runner, so a scenario contains only
what is particular to it. Historic flags survive as registered aliases on the scenario they select,
which is what keeps `--ambient 15` and a bare seed working with no branch anywhere.

**A scenario that owns its command line says so.** `IntegrationHarness` is a production-faithful
harness with an established argument surface — modes, snapshots, watch levels, JSON output, and an
exit status that reports whether the run held its invariants. It is not fitted to the option model;
its scenario declares that it forwards raw arguments, and the runner hands them over unvalidated.
The adapter parses and validates before running so a mistyped harness argument reports a usage
failure rather than an unhandled exception, and the harness itself is unchanged apart from splitting
its parse from its run.

Reason: the laboratory is where BQ systems are proved without a game, so it will keep gaining
experiments. One authority for discovery and dispatch keeps that growth localized; forcing a
production-faithful harness into the same option model would buy uniformity by weakening the
semantics the harness exists to have.

## D049 — A playground writes state and reports; it never decides, and it names what it had to author

`tools/BrilliantQuesting.Lab/Playground` puts two people in one exchange over one claim and prints
the whole path. Its rule is that it may write authoritative state and read the result, and may do
nothing else. A preset is a list of facts about the world - a tie, a belief, an emotion, a personal
line, an obligation, an action the action layer actually performed - and there is no way for one to
set a strategy, a depth, a tactic, a permit or a line, because the stage exposes the stores and not
the decisions. The command line inherits the same rule: `--tie`, `--knowledge`, `--voice`,
`--speaker` and `--turns` name state, and a test asserts that no option ever names an outcome.

**Every semantic answer comes from production.** `SpeechAct.Compose` makes the acts, `Disclosure`
decides and composes the reply, `CallbackDisclosure` clears old business, `DialogueRealizer` finds
the words, `ConversationState` remembers the exchange and promotes a promise, `Deception` records a
falsehood. The playground holds only what a conversation transiently holds - one `ConversationState`,
one `DialogueExpressionHistory`, one `WeirdnessBudget` - and reporting is a read: running every
reporter twice produces identical text and moves neither the ledger nor the obligations.

**Inspection reuses the inspector.** `NarrativeInspector` already accounts for casting, disclosure,
speech acts, veracity, reactions, callbacks and a conversation, so the playground calls it rather
than holding a second opinion about how to print any of them. The one adapter it adds is the
recurrence explanation, because `CallbackRecurrence.Best` returning null cannot distinguish "not the
kind of history that recurs" from "memorable, and it happened right here"; both halves are read off
that class's own public predicates. No diagnostic state was added to Core for the playground's sake.

**What it had to author, it labels.** Nothing in Core assigns a `VoiceProfile` and nothing in Core
selects a `Promise`, so the laboratory supplies both — and says so in the run's own output and in
`playground-systems`, which sorts every system into production logic, production logic fed by the
headless sandbox seam, choices the laboratory makes for want of a production authority, and systems
that need a running Elin. That last column shares the integration harness's word, `PLUGIN ONLY`,
because the honest answer to "can this be shown offline" must not depend on which laboratory command
was run. It is an authored ledger kept beside the code, and the headline is the load-bearing part:
no part of the plugin consumes a `SpeechAct`, a `DisclosureDecision` or a `RealizedLine`, so the last
thing the playground can honestly show is a line and its meaning, never a line a player read.

Reason: a demonstration that could author its own answers would prove nothing, and one that quietly
filled a gap with a mock would prove something false. Making state the only input and labelling
every choice the simulation does not yet make keeps the playground evidence rather than decoration.

## D050 — A sweep moves one input and reports four differences; an invariant it breaks fails the run

`tools/BrilliantQuesting.Lab/Playground/Sweep` turns the conversation playground from a set of
hand-picked examples into a diagnostic instrument. A family holds a state still, moves one named
piece of it, and prints four differences for every row: which authoritative or actor-local state
changed, what the semantic layers decided, what constrained and produced the wording, and what the
exchange left behind. The question it answers that a single run cannot is which input state a
conversation is actually a function of.

**A sweep manipulates input only, structurally.** A row is a preset plus a list of
`PlaygroundInput`s, and an input is handed the `PlaygroundStage` - which exposes stores and not
decisions - so it can reach a tie, a belief, a route, a confidence, a claim's truth or secrecy, a
personality weight, an emotion, a personal line, an obligation, an identity facet, history in the
ledger, the number of exchanges and the seed, and cannot reach a strategy, a depth, a tactic, a
speech act, a permit, a fragment or a line. `--axis` is the only thing that selects anything, and
tests assert both that no option names a derived outcome and that no input any shipped family
declares does either.

**Three answers, not two.** "Changed the outcome" and "changed nothing" are not exhaustive: an input
that moved the weighing without carrying the answer anywhere is a pressure the model reads and this
situation was not close enough to be turned by, and it is reported separately from an input nothing
reads at all. A summary that collapsed the two would hide the more interesting one - and a row that
observably changes nothing is a finding, not a failure.

**An axis point current state cannot express is reported as unsupported.** Three exist today: a
distorted rival claim needs a second `Fact` and minting one is a write no input makes; the `Heard`
callback route reads `WorldEvent.Related` and no history this stage can produce puts a claim there;
and a self-contradiction inside one conversation needs two assertions with opposite stances, which
no state the playground can write reaches. Naming them costs nothing and approximating them would
put a false row in a table people read to find gaps.

**Invariants fail the process.** Meaning never moves with wording; depth never exceeds what is
known; a speaker holding no belief composes no act; a withheld memory never reaches the realizer; a
required core always finds words. Each family adds its own. A violation is printed with the row that
broke it and exits with the laboratory's existing scenario-failure code, because a table nobody
checks stops being read. Where a counter-example is constructible from real runs the check is
mutation-tested against one; where it is not - `RealizedLine.Meaning` is defined as the act's own
signature - the test asserts the check was not vacuous instead of doctoring a reading nobody could
produce.

Reason: proving each system in isolation and once in composition leaves the interesting questions
unanswered - where two systems collapse to the same behaviour, where one has no observable effect,
where the shipped content cannot say what the simulation can mean. Those are answerable only by
moving state systematically, and only worth answering if the instrument cannot author the answers it
is measuring.

## D051 — A storylet routes; it never speaks

`StoryletBeat` carries who might speak, what they might be trying to communicate, what is in doubt,
what history should record and where the scene goes next. Every one of those is a reference: a role
id, a `SpeechActType`, a check profile the action library already ships, a `WorldEventType`, another
beat's id. There is no field on the schema a line of dialogue could be written into, and
`StoryletContent` refuses any string in a storylet payload containing whitespace or sentence
punctuation, plus the keys somebody would reach for (`text`, `line`, `say`, `dialogue`, …).

**The rule is structural rather than reviewed for.** "Storylets reference meaning, they do not
contain wording" is the kind of rule that erodes one convenient exception at a time, and the
convenient exception is always the scene where the generic line reads slightly worse. Making it a
build error costs an author nothing they should have been doing and removes the argument.

**A beat lists what could be said and never what is said.** `ActorIntent` chooses among a beat's
declared intentions from the speaker's own personality, problem-solving preference, mood, tie to the
listener, open obligations, conviction in the claim and exposure to the room, with a bounded jitter
that separates genuine ties and never overturns a preference. That is what makes two castings of one
storylet two scenes: a merciful creditor reaches for a release where a vindictive one reaches for a
threat, and neither sentence is written anywhere.

**Nothing in the runtime names a storylet.** `StoryletRouter` walks beats and delegates every
decision inside the walk — casting, intent, act composition, checks, wording, consequences all stay
where they already were. The moment the router mentions a storylet by name, the forty-first storylet
has stopped being cheaper than the sixth, which is the whole reason the layer exists.

Reason: the alternative to routed data is a bespoke C# class per scene or a script per scene, and
both were reachable from a beat that was only an id.

## D052 — One question has one decider

A beat whose speaker has just been asked about the focus does not decide what to do about it in
`ActorIntent`. `Disclosure` (BQ-071 … BQ-073) already weighs privacy, relationship, fear, loyalty,
leverage and legal risk for exactly that question and already composes the answer, the refusal, the
evasion or the falsehood; the router asks it first and takes its act when the beat offers that move.
Everything else — what to open with, whether to accuse or ask, whether to forgive or press — is
`ActorIntent`'s, because nothing else decides it.

Two deciders for one question do not merely duplicate work. They disagree: a scene could route on an
intent that said *answer* while the disclosure decision behind the wording said *refuse*, and the
line would then be worded from a decision the act contradicts. Choosing one owner per question is
cheaper than reconciling two.

Reason: the failure is silent and looks like a content bug. It is not one.

## D053 — A consequence records what happened, and records it as an event

A beat's consequences carry a trigger in the same vocabulary its routes do, so a beat that offers a
charge and a question files an accusation only when the charge was actually made. Without it the
ledger fills with accusations nobody made, and every later reading of history — affinity, memory,
rumour, thread tension — is downstream of that.

**A consequence is a `WorldEventType` or it is a marker.** A hook that names an event is applied by
appending to the event ledger through `NarrativeWorldState.Record`, which is where every consequence
in this codebase already comes from: `ConsequenceEngine` then does what it already does. A hook that
names no event is written onto the firing and nothing else happens, which is what every storylet hook
was before this. There is no third shape, and in particular no way for a storylet to state an effect
that is not an event — the vocabulary is `WorldEventType`'s, the arithmetic is `ConsequenceProfiles`'
and the participants are roles the scene already cast.

Reason: a second consequence system would be a second history, and the ledger's whole value is being
the only one.

## D054 — Wording may read a mood and a tie, and may still not read a lie

`DialogueReadings` gains `emotion` and `relationship`, derived from `EmotionalState` and
`RelationKind` rather than listed again. Both are authoritative state that already exists, already
decays or persists on its own terms, and already biases decisions; what is new is only that a
fragment may be conditioned on them. A `VoiceProfile` is a constant and a `DisclosureDecision` is
about one claim, so neither could carry "the person answering is still angry" or "this is her
brother", and both of those are audible in a way no depth or strategy stands in for.

Only one emotion reads, and only above a floor: somebody faintly several things at once is not
visibly any of them. `none` and `absent` stay apart on the relationship axis, because a stranger's
line said to somebody's spouse because nobody looked would be wording asserting a tie the world never
held — and a tie read against somebody the act does not address is refused rather than quietly used.

What has not changed is the one thing that must not: wording is still never told that the speaker is
lying, and a denial of something true draws from exactly the pool a denial of something false draws
from.

Reason: the axes a corpus is actually written along are act, mood and relationship. Two of the three
had nowhere to land.

## D055 — A memorable line is protected from repetition more strongly than a plain one

`DialogueMemorability` is four values on a fragment — utility, voiced, signature, protected — and the
only thing it changes is how quickly `DialogueExpressionHistory` considers that fragment stale, both
on its own count and on its repetition group's. It is not a quality rating, not a selection weight
and not a second weirdness axis: an absurd premise is `DialogueWeirdness`' to price, and the most
memorable sentence in a library can be entirely mundane.

A flat cap is right for "No." and wrong for a line somebody would quote, and the difference is not
small: hearing a joke twice in one exchange does more damage than hearing a plain utility line five
times. An unmarked fragment is utility, which is the behaviour every fragment had before the
vocabulary existed, so it costs nothing to ignore — and the coverage report tracks the distribution,
because a library that is mostly signature is a library of catchphrases however good each line is.

Reason: the corpus this pass promoted is full of lines that are excellent once and irritating twice.

## D056 — A practice is read from the place, modulates a reaction, and is never a verdict

CD §16's contextual norms are derived per pass by `SocialPractices.Read` from three things the world
already holds — where this is, what has lately happened here, and who is standing in it — and are
never set by a caller, attached to a zone by hand, or named after a town. Where those reads say
nothing the answer is no practice, which is the honest description of most of the map.

Three boundaries hold, and each is a failure mode rather than a preference.

**The vocabulary names the norm, not the ceremony.** Elin has no funeral to ask about. `Mourning` is
a death recorded *here* plus somebody standing here who cared, because those are the two questions a
funeral is the answer to and they are readable; a member named for an occasion nothing can detect
would be a category with no derivation behind it.

**A practice modulates a reaction; it never invents one.** It changes how hard the room takes
something the room already reacts to. An event the consequence table gives witnesses no reaction to
stays unwitnessed however solemn the room is — the alternative is context minting consequences on
its own, which is the same defect as a norm asserted from an unread facet.

**A practice is not a verdict and not a loss.** Karma and fame are the law's answer and stay
BQ-046's; what an act cost the person it happened to is theirs and does not depend on the company
they were in. What a practice changes is what the *room* made of it.

Nothing derived is persisted, and the practice is not written onto the event: history records what
happened, and what the place made of it is recomputed from the same reads next time.

Reason: the cheap versions of this step are a zone flag somebody sets, a severity multiplier that
quietly doubles as a legal judgment, and a stereotype standing in for an unread role. All three
survive a demo and none survives a save.

## D057 — An object's history is the ledger read through the object, and a matter is reached only through a recorded link

BQ-085. `ItemProvenance` derives what a thing has been through by reading the events that carry it
as `WorldEvent.Evidence`. There is no provenance store, no field on the object and no save entry:
`D039`'s reasoning about callbacks, applied to things instead of to people. An object's history
therefore survives a reload because the ledger does, a corrected event corrects every reading of it,
and `PM §21`'s "track only notable objects" needs no notable bit — nothing is tracked, so an object
history never mentioned costs nothing and answers with an empty list.

**One field means it.** Only `Evidence` says "this object was part of what happened". `Related` is a
general list of ids whose meaning changes from verb to verb, so reading it as provenance would mean
inventing the relationship it then reported.

**A role exists only where something records it.** Made, given, returned, stolen, destroyed, kept,
cited — and nothing else. "Found on the corpse of", "inherited by" and "recovered at" have no
recorder and are absent rather than guessed from a death, a will nobody wrote, or the zone a search
happened in. "Owned by" is absent for a different reason: who holds a thing now is Elin's inventory,
read live, and a history that answered it would be a staler second claim about the same question.
The vocabulary grows when a recorder appears, on `CallbackKind`'s terms.

**Recognition is the callback route, not a second gate and not a roll.** Whether somebody knows an
object on sight is `CallbackHooks.TryRoute`'s question — the same one that decides whether they may
bring that history up at all — so nothing can hand somebody a past they were never part of, and
asking twice cannot produce two answers. Showing a thing to a stranger tells them nothing, and there
is no check to retry.

**Producing an object asserts nothing.** `ObjectRecognized` names no claim, so the consequence layer
has none to teach the room: a ring surfacing does not tell its owner who took it and does not tell
the bystanders either. Nobody's standing moves for it — handing it back is `ItemReturned`, and that
is where the credit for giving it back lives.

**A matter is reopened only through a link something wrote down.** A thread is reached from an entry
when the event names the thread, the thread names the event as its origin, or a claim the thread
rests on was begun by it. Never by shared time, place or subject: a coincidence is not a connection,
and reopening is `ThreadLifecycle.Reactivate` — the existing primitive — rather than a second way
for a matter to come back.

Reason: the cheap version is a `History` list on the item, written at each verb. It is a second copy
of the ledger that can outlive it, disagree with it and need migrating, and the first thing it does
is let anybody holding the object read a past they were never part of.

## D058 — Genesis runs once and is not history; a return visit is a read

A place the mod makes is created by `SiteGenesis` exactly once, and the fact that it was created is
kept on the place rather than in the event ledger.

**Genesis is bookkeeping, not an event.** Nothing happened to anybody when a smugglers' cache came
into existence — the in-world events about a site are somebody finding it and somebody clearing it,
and both already existed. So genesis appends nothing. That is what makes the site proof's
"no historical event was redispatched on return" true by construction instead of by a listener being
careful: there is nothing to redispatch, and a return that moved the ledger count moved it for some
other reason the trace can name.

**A visited place is never regenerated over.** `NarrativeSite.Established` persists, and it is a
refusal rather than a note: a plan whose site id the world already knows is rejected outright, and
an established place is handed straight back with nobody staged and its establishment time
untouched. Across a reload as well as within a session, because the failure that matters is a player
who saves inside a generated place and loads back into a second cast of it.

**The manifest is the site, not a copy of it.** `OccupantIds` and `ImportantObjectIds` already
persisted and are already what the place is; `SiteGenesis.Visit` reads them against the game and
reports which occupant and which object is missing. Nothing is written down twice, so nothing can
disagree, and coming back is a read that appends nothing — a reconciliation that recorded a visit
would make walking through a door history.

**Two ways in means one of them does not need permission.** A plan must carry at least one approach
that waits on somebody letting you in and at least one that does not. Two verbs that both wait on
the keeper are one approach with two spellings, and the distinction reuses
`NarrativeSite.Restricted`/`Admits` — an owner admits people, a burglar admits themselves — rather
than adding a taxonomy of route kinds.

**A place with no body is not created at all.** `ISituationStager.StageSite` answers with the
adapter's handle or with nothing, and nothing means genesis registers no site, stages nobody and
binds no thread. Half a place is worse than none: it would sit in the save, be named by its matter,
and answer questions about somewhere nobody can walk into.

Reason: the cheap version stamps a site into the registry at generation time and re-runs the
generator when the zone reloads, because the generator is where the occupants come from. That is
exactly the destructive regeneration `PP §6` forbids, and it is invisible until a player comes back
to a place they emptied and finds it staffed again.


## D059 — A place's history is a rule about which events count, and a legend is that history compressed

BQ-086. `LocationHistory` derives what has happened in a place from the ledger, on the terms `D039`
and `D057` already set for people and things: no history store on a site, no index, no save entry.
A place's past survives a reload because the events do, and a corrected event corrects every
reading of it.

**Notability had to become a rule, because every event records a zone.** An object's provenance can
be defined by a field being populated at all — `Evidence` is sparse, and a berry nobody wrote
anything about simply has none. A place cannot: everything that happens happens somewhere, so
without a rule a mine's history would be its footfall. The rule is that the event either names the
place — `SiteDiscovered` and `SiteCleared`, the only two verbs whose subject is somewhere rather
than somebody — or left material somebody could bring up afterwards, which is
`CallbackHooks.KindsOf`. That is why `PM §40`'s "track only notable events" needs no notable bit, no
per-site budget and no pruning pass: meeting somebody in a mine and talking there are not the mine's
history, and were never admitted to be discarded later.

**Belonging is read off two recorded fields, because the ledger writes two shapes.** Most events say
which zone they happened in, keyed on `SiteGenesis.ZoneOf` like every other read of a place. A
place-naming event instead carries the site as its *target* and records whatever zone surrounds it,
so clearing a cache under a boathouse is the cache's history even though the zone on the event is
the town's. Nothing is matched on name, type or nearness in time: a coincidence is not a connection.

**A legend's subject is a `CallbackKind`, not a new vocabulary.** That enum already says what sort of
story an event leaves and already groups what a legend must group — three separate maulings in one
place are one thing the place is known for, not three. Minting legend motifs here would mean
maintaining two answers to "what kind of thing was that". Being found and being emptied stay history
without becoming legend: nothing in the simulation calls either a kind of tale, and inventing one
would be this layer minting the interpretation it then reports.

**One compression answers both questions.** `Legends` takes entries rather than a world, the way
`ItemProvenance.OpenMatters` does. Hand it the world's own history and it says what the place is;
hand it one person's and it says what that person could tell you the place is — and a legend derived
from what a settlement actually knows is what that settlement tells. There is no second
implementation to fall out of step, and no separate publicity rule to forget: knowing what happened
somewhere is gated on `CallbackHooks.TryRoute`, so standing in a place teaches nobody its past.

Reason: the cheap version hangs a list of notable events on `NarrativeSite` and appends to it as
things happen. It needs a notability flag, a cap, a pruning pass and a save migration; it can
outlive, contradict and double-count the ledger it was copied from; and because it is written at
dispatch time it cannot be gated per person afterwards, so the first consumer that asks what an NPC
knows about a place gets the omniscient answer.

## D060 — Idiolect is a second closed voice vocabulary, and an unmarked line is wording every voice can reach

BQ-142 gives a speaker habits that hold across every line they say — length, cadence, figuration —
and the whole decision is about where they live and what an unmarked fragment means once they exist.

**A second vocabulary beside tone, not more tags in it.** `DialogueTones` is affect: warm on Tuesday
and cold on Wednesday without becoming a different person. Idiolect is habit, which is what makes two
speakers who have reached the identical `SpeechAct`, the identical `DisclosureDecision` and the
identical tone still sound like two people. Folding the new poles into `DialogueTones` would have
been fewer moving parts and would have silently changed what every existing reader of
`RealizationRequest.Tone` — the sweep's invariants, the coverage report, the plugin — was asking for.
Two lists, requested separately by `VoiceProfile.RequestedTone` and `RequestedIdiolect`, read
separately by `FitsTone` and `FitsIdiolect`, keep both questions answerable and let a caller supply
either, both or neither. It is a second vocabulary and not a second system: the seam, the request,
the narrowing and the guarantee are all BQ-075's, used twice.

**Register is not one of the axes, because `Formality` already is.** CD §19's list names formality
and metaphor use in one breath, and formality *is* register — it has requested `formal` against
`plain` since BQ-075. A word-stock axis beside it would be two names for one question, and the
second one would be the beginning of the parallel personality system this layer is written to avoid.
So the three are the two BQ-075 named as its own deferral, plus cadence, which the corpus separates
from length on its own: a terse line can be figurative, and an expansive one can be clipped.

**An unmarked fragment fits every voice — the tone rule, not the vocabulary rule.** `D035` inverts
the default for occupational tags, so an unrequested flavour *excludes*, because a lived-context line
let through by an unread identity is a claim about somebody's life nobody derived. That reasoning
does not transfer. A length or a cadence is a property of the sentence, visible in the sentence, and
true whether or not a voice was ever supplied — it asserts nothing about the speaker that the words
do not already assert. Inverting the default here would make the untagged majority of a 521-fragment
library unspeakable by any specified voice, which forces the migration into a single commit and makes
every future tag a breaking change rather than an addition.

**A voice no core can satisfy refuses.** The narrowing can empty a required slot, and when it does
the line comes back unrealized with a reason and no text, exactly as BQ-074 refuses an act nothing
has words for. Dropping the constraint to get a sentence out would make a habit a suggestion, and
would produce the one failure this layer exists to prevent: words the simulation's own constraints
had ruled out, said anyway. An optional slot narrowed to nothing simply falls silent, which is what
an optional slot has always done.

**Both poles of one axis on one fragment is rejected at load.** It is a contradiction rather than a
refinement — the reading `FragmentRequirement` already takes of two conditions on one key — and it
fails quietly rather than loudly: such a fragment is refused by every voice with an opinion about
that axis and admitted only by voices that were never going to narrow on it, so it disappears from
exactly the pools it was written for and nobody notices.

Reason: the cheap version is three more strings in `DialogueTones` and a `tags` entry for each. It
costs nothing on the day and takes the meaning of "the caller asked for a tone" with it, leaves
`Opposite` a partial function over a vocabulary that now mixes affect with habit, and gives the
coverage report no way to say which of the two a hole is in.

## D061 — Wording may express a meaning and may never assert one, and provenance is a reading rather than a rewrite

BQ-147 audits the shipped fragment corpus against its own eligibility, and the decision is about
what to do when an authored line asserts something its conditions do not entail. Three answers, and
which one applies is not a judgement call.

**Where authoritative state already answers the question, add the reading.** How somebody came to
hold a claim is `KnowledgeRecord.Source`. It is saved, it is already read by disclosure, by witness
disclosure and by the investigation layer, and it is the exact thing "I saw it" asserts. So
`claim_source` and `claim_proof` are derived from that one record the way `callback_route` is derived
from a hook and `relationship` from an edge: passed into the request rather than read inside
realization, absent when nobody looked, and refused when measured against a different claim. This is
the same arrow BQ-146 drew — wording is *told* what the simulation holds, in the vocabulary the
simulation holds it in — and it points one way only.

The alternative was to delete the provenance wording, and it was worse than it looks. "I watched it
happen", "I have it secondhand" and "nobody told me, the pieces point that way" are the sentences
that make an investigation an investigation; a library that cannot say them can only say the claim.
And the wording was not the broken part. What was broken is that it was being chosen on
`commitment` — how firmly somebody will stand behind what they say — which is a different question
with a different answer: a confident believer of a fence-side rumour reads `committed` and a
hesitant eyewitness reads `hedged`, so each sentence was available to precisely the wrong speaker.

**Where the proposition is about another person's mind, reword and never ground.** "You already
knew", "you have not heard it yet", "whoever told you that" and "apparently you do not [remember]"
are claims about somebody else's beliefs, and there is no reading to reach for because there must
not be one. Beliefs are the knowledge graph's, they are held per person with a source and a
confidence, and a wording layer that could assert one would be a second belief system with no ledger
behind it — the duplicate-history failure the whole event-sourced model is arranged to prevent. The
same holds for who moved where and for what has already been said to whom. A line that needs one of
those says less instead.

**Where an existing reading already answers it, tighten rather than invent.** Every backward-pointing
line — "ask again", "that story is wrong", "mind who you are accusing" — is grounded by `reply`,
which has existed since BQ-074. Every line that aims a feeling at the person opposite is grounded by
`relationship`, because `EmotionalStateProfile` holds one number per state and no target: it says
somebody is feeling affection and cannot say who for. Reaching for new state where an existing
reading answers the question is how a vocabulary stops being closed.

**A directed tie names the role of the party the edge runs from.** `ActorIntent` reads
`RelationKind.Creditor` as "is owed" and `Debtor` as "owes"; `StoryletChemistry` gives the creditor
the leverage. So `relationship: creditor` is the *speaker* who is owed, and sixteen shipped lines had
it the other way round — "You still owe me" eligible only for the speaker who owes, "I owe you. Ask."
only for the one who is owed. Nothing about the wording was wrong and nothing about the graph was
wrong; the axis was read backwards, silently, in the one place nothing else looks. It is pinned by
test rather than by comment, because a comment is what it had.

**One rule is mechanical, and only one.** A fragment that names `{referent}`, `{subject}` or
`{recalled}` must declare the reading that places that person, refused at load beside "a core
fragment must declare which act it says". A name placeholder resolves to a name, and a name in the
third person claims its owner is not the person being spoken to, so an unplaced one is eligible for
the conversation it is nonsense in. It requires the question to be answered and never dictates the
answer: a line written to be said to the person it names is a real line.

Nothing further was made mechanical, and that is the decision rather than an omission. Whether a
sentence asserts something is recognisable to a person and not to a rule: a checker over English
would refuse a perfectly grounded line for containing "saw" and would still miss the assertion
carried by "either" in "{recalled} never got theirs back either". So the rest of the audit lives in
data-driven tests over the shipped corpus, which say "these exact claims came back" and claim nothing
about sentences nobody has written yet.

Reason: the cheap version reworded the twenty-five provenance lines into vagueness and shipped. It
costs nothing on the day, deletes the register that makes a witness sound like a witness, and leaves
the actual defect in place — provenance chosen on confidence — for the next author to walk into with
the next sentence.

Add a new entry only when the decision is both load-bearing and durable.
