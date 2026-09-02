# Elin Brilliant Questing --- Vanilla Simulation Integration

**Status:** Design reference, written after the vanilla actor-simulation research pass\
**Date:** 29 August 2026\
**Relationship:** Addendum to `docs/design/living-world-priorities.md`, which it corrects on one
point: BQ is not the only off-screen simulator in this game, and the tiers in `LW §5.4` have to be
designed around mechanisms Elin already runs.

**Evidence level.** Everything in §2 was read off the decompiled Elin documentation, not off the
installed build and not off a running game. Under the authority order in `AGENTS.md` that ranks
below `elin-api-notes.md`, which is read from the shipped assemblies, and far below a live log
line. Nothing here is a verified API alias, and nothing here justifies a `VanillaCapability` until
somebody has watched it work. `docs/elin-api-notes.md` carries the member-level record and the
list of what remains open; this document carries the reasoning.

------------------------------------------------------------------------

## 1. The one boundary this document draws

> **Vanilla owns embodiment; BQ owns narrative meaning.**

Recorded durably as decision `D021`. The rest of this document is why, and what it costs to get
it wrong in either direction.

The existing architecture is directionally right and this research does not replace it. World-state
situation generation (`BQ-039`), coarse off-screen autonomy (`BQ-094`, `BQ-095`), one action
vocabulary for player and NPC (`BQ-093`), abstract traveling groups rather than world-scale
pathfinding (`BQ-097`), the Active/Warm/Cold/Archived tiers (`BQ-107`), persistent causal history
and vanilla-owned physical mechanics (`D002`, `D010`) all survive intact.

What changes is that one seam becomes explicit. BQ simulates meaning, motivation, knowledge, social
causality and high-level intention. Elin stays authoritative for embodiment wherever it has a real
mechanic — and it has more of them than the mod has been assuming. So the highest-value result of
this research is not more NPC simulation. It is **better coupling between Elin's simulation and
BQ's interpretation of it**: more causal continuity per unit of new machinery.

------------------------------------------------------------------------

## 2. What Elin already simulates

Source-observed against the decompiled documentation on 29 August 2026. Runtime-unverified.

### 2.1 Timetables are real state, not flavour

`Chara` exposes `idTimeTable`, `CurrentSpan` and `GetGoalFromTimeTable(int hour)`. The timetable
maps an hour onto at least `Sleep`, `Eat`, `Work` and `Free`, and `GetGoalFromTimeTable` turns
that span into a vanilla goal: Sleep can become `GoalSleep`, Eat returns `GoalIdle`, Work returns
`GetGoalWork()`, Free returns `GetGoalHobby()`. Guests are handled differently during Work and
Free and idle instead. At least `default` and `owl` exist as timetable ids.

There is therefore already an answer, in the game, to "was this person plausibly awake, and were
they at work?". BQ does not need to invent one.

### 2.2 Work, hobby, needs and local tasks are real AI

The goal hierarchy includes `GoalIdle`, `GoalSleep`, `GoalNeeds`, `GoalWork`, `GoalHobby`,
`GoalTask`, `GoalCombat`, `GoalSiege` and visitor goals, alongside a large library of physical
actions. `GoalNeeds` dispatches from actual `Chara` state — hunger reaching `AI_Eat`, bladder
reaching `AI_Bladder`.

A BQ hunger model would be a second, worse copy of a system the player can already see running.

### 2.3 Home residents get catch-up simulation on revisit

`Zone.OnVisit()` calls `Simulate()`. `Zone.Simulate()` measures the time since the zone was last
active and, when enough has passed, runs catch-up processing; for player-faction zones it can put
residents through work and hobby goals via `AIAct.SimulateZone(...)`, restore an ordinary goal and
simulate the resulting position.

Elin already owns part of "what did the residents do while I was away?". This is the finding with
the most immediate consequence for `BQ-048`, `BQ-049` and `BQ-107`: BQ must not advance the same
processes a revisit will advance anyway, or the player gets a day of work counted twice.

### 2.4 There is a separate off-screen global-actor mechanism

`GameDate.AdvanceHour()` walks `game.cards.globalCharas`. For a non-party global actor outside the
active zone whose trait reports `UseGlobalGoal`, Elin advances that actor's `GlobalGoal` once per
game hour, and can assign `GlobalGoalAdv` to an eligible non-player-faction actor that has none.
`GlobalGoalAdv` can eventually move an actor to a random town through `Chara.MoveZone(...)`, gated
on conditions including alive, not player faction, not in the active zone, not `StayHomeZone`,
enough elapsed global-goal time, and a random trigger.

So **some actors genuinely change zone off-screen without BQ doing anything**. That is the finding
that bears directly on `BQ-032` and `BQ-097`, and it cuts both ways: it is free world motion the
mod can interpret, and it is a second writer of the state Grade B absence depends on.

It does *not* prove that ordinary town citizens are global actors, and it is not a licence to take
the system over. Which traits actually report `UseGlobalGoal` is one of the open runtime questions
in §7.

### 2.5 The consequence: four fidelity mechanisms, not two

Elin does not fit "loaded means simulated, unloaded means frozen":

1. **Active-zone local AI** — moment-to-moment physical behaviour.
2. **Zone catch-up on revisit** — including Home work and hobby.
3. **Hourly `GlobalGoal` advancement** — for eligible global actors outside the active zone.
4. **Zone/hour/day/month hooks** — broader world and Home processes.

`LW §5.4`'s tiers were written as though BQ were the only off-screen simulator. They are not wrong,
but they have to be reconciled against these four rather than layered on top of them. See §5.

------------------------------------------------------------------------

## 3. The division of authority

### 3.1 A loaded actor

**Elin owns** position, local pathfinding, idle behaviour, sleep, work, hobby, bodily needs,
combat, local task execution, and ordinary movement and obstruction.

**BQ owns** facts and beliefs, memory, relationships, obligations, interpretation, narrative goals,
social decisions, situation pressure, high-level intention, causal history, and what is salient
enough to record.

BQ may ask for a physical act through a verified vanilla path. It must not build a second one.

### 3.2 An unloaded actor

For a BQ actor no suitable vanilla mechanism is handling, BQ may resolve **coarse narrative
activity**: attempting to learn a fact, persuading, threatening, investing, stealing, fleeing a
debt, hiding evidence, seeking revenge, asking for help, deciding to travel, intervening in a
situation.

It may not fabricate physical detail nobody simulated. The line is the same one `D011` draws for
proof and `BQ-095` will have to hold:

Legitimate:

> Nessa met with Garron in Mysilia and learned the caravan was missing.

Not legitimate without real physical observation:

> Nessa stood behind the tavern at tile 42,18 and watched Garron hide the ledger.

### 3.3 A global vanilla actor

Where Elin is running an actor's `GlobalGoal`:

1. Observe first.
2. Do not silently replace or fight the vanilla goal.
3. Treat current zone, pending transition and global-goal state as part of whether that actor is
   *available* to a BQ intention at all.
4. Only after a dedicated runtime proof consider steering a compatible vanilla goal as the
   embodiment of a BQ travel intention.

This matters most where `BQ-032` absence and `BQ-097` travel would otherwise both be writing an
actor's location, with Elin as a third writer neither of them knows about.

------------------------------------------------------------------------

## 4. The integration primitives

Two read-only projections cross the seam, and they are deliberately separate because they answer
different questions and have different lifetimes. **Activity** is what an actor is doing right now
and is worthless a minute later. **Identity** is who the game says that actor *is*, and it changes
only when the game changes it. §4.1 is the first, §4.2 the second, §4.3 the rule that stops them
collapsing into each other.

### 4.1 An actor activity snapshot (transient)

BQ needs one read-only projection of transient vanilla activity, crossing the seam the same way
`HomeState` does, so that the S8 systems do not each grow their own reflective probe into `Chara`.
That is the argument for `BQ-135`, and it is the argument that made `HomeState` worth formalising
before the Home verbs.

The shape below is semantic intent, not an API claim; the real members follow whatever the live
build actually answers.

```csharp
ActorActivitySnapshot
{
    EntityId Actor;

    bool     IsPhysicallyLoaded;
    EntityId CurrentZone;

    string       TimeTableId;
    ActivitySpan CurrentSpan;      // Sleep / Eat / Work / Free / Unknown
    ActivityGoal CurrentGoal;      // Sleep / Work / Hobby / Needs / Combat / Task / Idle / Other / Unknown

    bool IsSleeping;
    bool IsInCombat;
    bool CanWitness;

    bool                 UsesGlobalGoal;
    GlobalActivityKind   GlobalGoal;   // observe-only
    bool                 HasPendingZoneTransition;
}
```

Rules, all of which are existing project doctrine rather than new invention:

- A datum the game did not answer is **Unknown**, never `false` and never zero (`D017`). A citizen
  whose span could not be read must not therefore read as awake, at work, and available.
- The snapshot is transient and is **not persisted**. The save holds meaning, not a mirror of a
  `Chara` (`D005`, `D004`).
- Vanilla class names stay inside the plugin adapter; Core sees the semantic enum (`D001`).
- A readable member is not a reason to add a write (`D019`).

### 4.2 A character identity observation (stable)

The activity snapshot answers *what is this person doing*. Nothing at the seam yet answers *who
does the game say this person is* — and BQ has been quietly guessing at it. `NarrativeNpc` carries
an `Occupation` string and an untyped `Roles` set; the live plugin registers every real townsperson
with `Occupation = "local"`, and the only thing that ever populates `Roles` is the authority-trait
read that grants guard and guild standing. So `LocalAffordanceProfile`'s "occupation, job and
hobby; service role; authority, guild and faction role" (§5.2) is today a sentence in a design
document with one trait read behind it.

Elin holds the real answer. A `Chara` reaches its `SourceChara` row, and the source rows carry
race, job, hobbies, works, faction, faith, tags and category; `SourceRace`, `SourceJob`,
`SourceHobby` and `SourceFaction` are separate sheets with their own ids; trait subclasses mark
guards, guild personnel and shop staff; `FactionManager`, `Guild.IsMember` and
`FactionRelation.rank` answer guild membership and standing. That is enough to observe identity
rather than invent it, and the roadmap owns the read as **BQ-144**.

**Six facets, separately typed.** The one thing this observation must not become is a bag of
strings. Identity is read as distinct fields because the simulation asks distinct questions of
them, and a tag list makes every one of those questions unanswerable:

| Facet | Question it answers | Vanilla ground (evidence level in `docs/elin/`) |
|---|---|---|
| **Character archetype** | What kind of character is this — Little Sister, Punk, Bunny | the `SourceChara` row itself: its id, `tag`, `category`, `aka` |
| **Race / species** | What are they, where that is not the same as what kind of character they are | `Chara.idRace`, `source.race`, `SourceRace` |
| **Work / occupation** | What do they do for a living | `source.job` / `idJob`, `SourceJob`, plus Home branch work assignment where the branch is readable |
| **Hobby / interest** | What do they do when they are not working | `source.hobbies`, `SourceHobby` |
| **Service / commercial role** | What can the player or another actor *get* from them | shop and service trait subclasses, stock, and whether a service is actually being offered now |
| **Institutional role** | What are they entitled to do, and on whose behalf | guard and guild-personnel traits, `Chara.faction`, `SourceFaction`, `Guild.IsMember`, `FactionRelation.rank` |

Evidence level for that right-hand column is `SOURCE-DATA` and `VERIFIED-METADATA` — the sheets and
the installed assembly, recorded in `docs/elin/bq-integration/world-affordances.md` and
`docs/elin/api/actors.md`. No live game has been watched answering any of it, which is why §7 gains
an identity research list and why the character-archetype facet is the weakest of the six.

They stay distinct because they behave differently. Work is what somebody is entitled to know and
be asked about; hobby is what they care about off duty and is the weakest of the six; the character
archetype is a presentation and role expectation and is not a job at all; race is embodiment and is
frequently orthogonal to every other facet; a service role is an affordance the world can lose
without the person changing; an institutional role is revocable authority that belongs to a body
rather than to a person. Flatten those into one list and a shopkeeper who is also a guild member
becomes indistinguishable from someone whose hobby is shopping.

**A terminology warning.** "Archetype" in the roadmap already means a *situation* archetype
(BQ-041 … BQ-047). The identity facet is always written **character archetype** and never
shortened.

**Adjacent reads that are not part of this observation, and stay where they are.** Actor class and
mutation policy (`NarrativeActorClass`, BQ-031), narrative kind and social agency
(`NarrativeActorKind`, `SocialAgency`), faith (`Chara.idFaith`), player familiarity
(`PlayerFamiliarity`, BQ-114), and transient activity (§4.1, BQ-135). They answer different
questions, several of them safety questions, and merging them into identity would let a costume
decide what the mod is allowed to do to somebody.

**Shape.** Semantic intent, not an API claim — the real members follow whatever the live build
answers:

```csharp
CharacterIdentityObservation
{
    EntityId Actor;

    IdentityFacet  Archetype;      // character archetype: the SourceChara kind
    IdentityFacet  Race;
    IdentityFacet  Work;
    IdentityFacet  Hobby;          // zero or more
    ServiceRole    Service;        // kind + whether it is currently on offer
    InstitutionalRole[] Institutions;  // body, role, rank where readable
}

IdentityFacet
{
    bool     IsKnown;      // false means unread, never "none"
    string   VanillaId;    // the game's own id, carried verbatim
    string   DisplayName;  // the game's own text where readable
}
```

`VanillaId` is Elin's vocabulary, not BQ's. BQ never mints an identity id, never normalises one it
does not recognise into a familiar one, and never maps an unknown row onto a default. An
unrecognised id is carried through *as unrecognised* — readable, usable as a stable discriminator,
and not pretended to mean anything.

### 4.3 Observation versus interpretation

The seam carries observations. Everything that turns an observation into narrative expectation is
BQ's, lives in Core, and is derived rather than read:

| Authoritative observation (Elin, through the adapter) | BQ-derived interpretation (Core) |
|---|---|
| this actor's `SourceChara` kind, race, job, hobbies | what they plausibly know about, and what they plausibly care about |
| this actor staffs a guild / holds a rank | which institutional roles they are *eligible* for in a situation or a storylet |
| this actor runs a shop, and it is open | what pressure a lost service puts on a settlement |
| the settlement's distribution of the six facets | which situations that settlement can support at all (§5.2) |
| an identity facet changed since the last read | whether that change is worth recording as an event (§5.3) |

The derivation has exactly one owner (roadmap **BQ-145**) so that "a brewer plausibly knows about
ale" is written once rather than re-derived inside generation, casting, interpretation and
vocabulary. Downstream systems consume the derived affordances; they do not consume `Chara`, and
they do not each grow a private table of what a job means.

Two hard prohibitions, because they are the failure modes this whole seam exists to prevent:

- **Identity never generates personality.** BQ-056 … BQ-060 do not read it. A Punk is not
  aggressive because they are a Punk; a Little Sister is not timid because she is one. Identity
  changes what is *plausible*, *available* and *at stake* — never what somebody is like. The
  interesting characters are the ones where the two disagree.
- **Identity never feeds mutation policy.** How far the mod may reach into somebody is
  `NarrativeActorClass`'s answer and only ever will be (`BQ-031`). A costume is not a permission.

### 4.4 Persistence and refresh

Identity is a **live read**, on the same terms as the activity snapshot and for the same reason
(`D004`, `D005`): the save holds meaning, not a mirror of a `Chara`. Concretely:

- Nothing in §4.2 is persisted into the `brilliantQuesting` chunk. A save that has been away from
  the game for a patch cycle must not carry a stale claim about who somebody is.
- A consumer may cache the observation for the duration of one pass — one generation run, one
  casting decision, one scene — and must not hold it across a zone change, a save or a load.
- The existing persisted `NarrativeNpc.Occupation` and `Roles` fields are the thing this replaces,
  not a second copy to keep in sync. Where a migration cannot simply drop them, they degrade to
  unknown and are re-read; nothing downstream may treat the persisted value as authoritative once
  the observation is available.
- What *is* persistent is what BQ itself concluded and what the player learned: an event recording
  that a guard was dismissed, a fact recording that the player found out who the guild clerk is, an
  obligation owed to somebody in a role. Those are knowledge and history, they already belong to the
  ledger and the knowledge graph, and they survive the person's identity changing underneath them —
  which is exactly the point.
- Identity changes are observed as **deltas** and obey §5.3: a promotion, a dismissal, a shop
  closing or a change of trade is worth recording; the same six facets reading the same way on the
  next tick is not.

### 4.5 How unknown identity degrades

`D017` applies in full and is worth spelling out here because identity is the place where guessing
feels harmless:

- An unread facet is **unknown**, never "none", never "ordinary citizen", never a default job.
- Unknown never grants anything: no service, no authority, no guild standing, no eligibility for a
  role that names the facet.
- Unknown never *blocks* anything that did not ask for the facet. A situation that needs somebody
  present and social does not additionally need to know their job.
- A facet the build stops answering after an update degrades only itself. Race going unreadable must
  not take work, hobby and institutional standing with it.
- Where every facet is unknown, the actor is still a full participant on the strength of the reads
  that do work — presence, class, kind, agency, relationships, history. They are simply somebody the
  world has not told BQ anything else about, which is a true statement and a usable one.

------------------------------------------------------------------------

## 5. What this buys the player

More simulation is not automatically better. These are the parts that pay.

### 5.1 Routine as plausibility and characterisation, not as an appointment gate

Timetables answer who was plausibly awake, who was at work, who could have met whom, why a service
is shut, and whether what the player just watched was unusual for that person. Those are inputs to
interpretation.

They should normally **weight or contextualise** an opportunity, not force the player to stand
around until 14:00 to use a quest verb. A hard schedule gate is legitimate only where the physical
situation genuinely requires one. `LW §10.7` — protect ordinary Elin play — is the constraint here,
and a schedule the player has to obey is exactly the nuisance mechanic it warns about.

### 5.2 Towns take their identity from their actual affordances

This is the highest-value change in this document and it lands on `BQ-039`.

Generation should not ask only "which character archetype may spawn here?". It should derive a
**local affordance profile** from state the game already holds: occupations, jobs and hobbies; services;
authority, guild and faction roles; shops; work infrastructure; current population and who is
present; readable Home or town state; recent events; existing relationships; physical sites; current
shortages and pressures; visiting or global actors.

A palace-and-merchant city then produces authority, fraud, reputation, service and information
stories because its world state supports them — not because anybody wrote
`if (zone == Mysilia) intrigue += 30`. Two structurally different settlements must yield different
candidate distributions with no town id anywhere in the generator. That is a product requirement,
not an adapter detail, and it is the difference between a generated situation and a reskinned one.

Most of that profile is the six identity facets of §4.2 aggregated over who is actually here, which
is why the identity read is a settlement-generation prerequisite and not a character-writing luxury.
A town whose readable population is guards, a guild clerk and two shopkeepers supports authority,
membership and service stories; a town of farmers and hunters supports supply, land and absence
stories. Neither needed its name written anywhere. Generation consumes the facets as observations —
who could plausibly be involved, what can be lost, who is entitled to act — and never as personality.

### 5.3 Ordinary vanilla events become narrative seeds

BQ observes **notable deltas**, not AI ticks.

Worth recording: an actor changes zone unexpectedly; an actor is absent when continuity says they
matter; a service becomes unavailable; combat or death intersects a relationship that means
something; a notable object changes hands; a resident joins or leaves; a global visitor arrives; a
vanilla act fulfils or breaks a standing BQ obligation.

Not worth recording: wandering, breakfast, routine work, routine hobby, ordinary sleep, position
changes nobody asked about.

> **Record state only when it changes the answer to a narrative question.**

Failing that rule spams the journal and the rumour mill with weather, which is `BQ-035` and
`BQ-019` degraded into noise.

### 5.4 Opportunity is not evidence

Where the world is loaded and BQ can verify a physical observation, it may use it. Off-screen
co-location, an overlapping timetable or a shared workplace mean **opportunity and nothing more**.
None of them may produce eyewitness testimony, proof, an exact location claim or recognition of a
person. `D011` and the witness model in `elin-api-notes.md` already say naming a name is the
third and hardest piece of knowledge; a schedule overlap is not even the first.

### 5.5 Vanilla surprise is content

If Elin moves an eligible global actor on its own, BQ interpreting that after the fact is worth
more than BQ having decided it:

```text
vanilla moves a traveling actor to Mysilia
    ↓
BQ notices the location change
    ↓
an old debt, relationship or live situation makes the arrival mean something
    ↓
a rumour, a meeting, an opportunity or a consequence follows
```

This is `LW §10.5` — the player is not cosmologically central — extended to the mod itself. Not
every meaningful movement in the world has to be BQ's idea.

### 5.6 Failure transforms the world

Already project doctrine (`LW §10.3`), and activity strengthens it: the NPC missed the player
because they were elsewhere; the suspect left town; the authority is occupied; the merchant's
absence became a service problem; somebody else got there first; the witness is no longer
available. Those are new states, not retry prompts.

------------------------------------------------------------------------

## 6. What BQ must not build

No parallel system for NPC hunger, bladder or sleepiness; no BQ-owned daily timetable; no routine
work or hobby execution; no local pathfinding; no idle wandering; no ordinary combat AI; no Home
resident daily production; no exact off-screen coordinates; no BQ global travel for vanilla actors
Elin is already moving safely; no per-loaf town economy.

And no second identity taxonomy. BQ does not author its own races, jobs, hobbies, character kinds
or guild structures for characters the game already describes, does not normalise Elin's ids into a
tidier private vocabulary, and does not keep a hand-maintained table of who is a guard. Where the
game has no answer, the answer is unknown (§4.5) — not a BQ-invented one. Authored BQ identity data
is legitimate only for characters BQ itself creates, and even then it uses the game's vocabulary.

Coarse economic *pressure* (`BQ-050`) stays, because it is narrative pressure rather than a second
physical economy. The distinction is the same one `D014` draws about crafting: reading what the
game produced is integration, rolling your own number over it is a competing mechanic that will
disagree with the visible one.

------------------------------------------------------------------------

## 7. Runtime research still required

Decompiled documentation tracks a nightly build and is not proof of the player's install. Before
`BQ-135` or any direct integration:

1. Log `idTimeTable` and the semantic current span for a few actors.
2. Confirm `GetGoalFromTimeTable` behaviour without mutating AI.
3. Identify which real traits report `UseGlobalGoal == true`.
4. Observe one global actor's `currentZone`, `global.goal` and `transition`.
5. Establish whether ordinary town citizens are global actors at all, or only traveller and
   adventurer classes.
6. Observe `Zone.Simulate()` catch-up on a player Home after an absence.
7. Establish whether a low-frequency event or hook can observe these changes safely.
8. Do **not** Harmony-patch `Chara.Tick` or any per-actor hot loop unless no lower-frequency
   observation surface exists. `LW §2.2` — native events over patches — applies with extra force
   to something running once per actor per turn.

And before `BQ-144`, on the identity read specifically:

9. Sample `Chara.source` on ordinary townspeople and record which of `race`, `job`/`idJob`,
   `hobbies`, `works`, `tag`, `category` and `faction` are actually populated, rather than assuming
   the sheet columns are filled in play.
10. Establish whether the `SourceChara` row id is the right handle for the character archetype, or
    whether a tag or `aka` carries it — that is the facet with the least direct evidence today.
11. Establish whether a service trait implies a service the player can actually use right now, or
    only that the character is a shopkeeper by kind (this is already an open item in
    `docs/elin/bq-integration/world-affordances.md`).
12. Establish whether guild membership and rank are readable for an NPC at all, or only for the
    player's own factions through `FactionManager`.
13. Confirm that reading identity is genuinely free of side effects, including the source-row
    lookups, and cheap enough to do for every loaded actor in a generation pass.
14. Identity does not answer `ELIN-Q-0027`. Knowing somebody's job does not establish that they
    belong to the settlement they are standing in; do not let a populated `job` field quietly
    become the residency read that question is still waiting for.

------------------------------------------------------------------------

## 8. The product-value test

A successful integration makes this possible:

> The player returns to Mysilia after several days. A merchant who had a reason to travel is there
> — because vanilla moved them, or because BQ resolved their travel coarsely. Their arrival
> intersects an old debt and a current shortage. The merchant looks for a local contact. A rumour
> spreads. A shop problem gets better or worse. The player finds out through conversation,
> observation or records. No fake tile history was invented, no generic quest spawned, and no
> second schedule, economy or pathfinder was running behind Elin's.

If the visible result is instead debug counters, a schedule menu or more interruptions, the
research was implemented and its point was missed.
