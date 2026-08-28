# Elin Brilliant Questing --- Post-Master-Design Findings & Implementation Addendum

**Status:** Working implementation reference\
**Date:** 27 August 2026\
**Relationship to existing documentation:** Addendum to
`docs/design/master-design.md`. Intentional overlap is retained where
later findings reinforce or sharpen the master design. Read this
alongside `docs/architecture.md` and `docs/roadmap.md`.

## 0a. Status when filed into the repository

*Added on commit; not part of the original document.*

This was written before the runtime adapter existed. Its design reasoning stands, and this is now
the second reference a coding agent should read after the master design. Three factual claims were
overtaken by work done the same week and have been corrected in place, each marked
**[superseded]**: §1's statement that the adapter is absent, §2.1's list of files still needed, and
§57's project boundary.

For what the game actually exposes - the `Check` surface, the event bus, the save chunk API, and
the element-alias problem - see [`docs/elin-api-notes.md`](../elin-api-notes.md), which is derived
from the shipped assemblies rather than from documentation. Where this document and those notes
disagree, the notes win: they were read off `Elin.dll`.

## 0. Purpose and updated thesis

This document captures the design conclusions, implementation
implications, community suggestions, mod-ecosystem observations,
integration requirements, testing requirements, and feature ideas
developed after the original master design was assembled. Its purpose is
to preserve reasoning that would otherwise remain in chat history and to
give coding agents an implementation-oriented reference explaining not
only what to build, but why, what existing Elin systems should be
preferred, and what anti-patterns to avoid.

The core thesis remains:

> **Generate persistent situations, not disposable quests. Let vanilla
> Elin mechanics determine what actors can attempt, how difficult it is,
> who notices, what it costs, and what changes afterward. The quest
> journal is a projection of unresolved world state, not the source of
> truth.**

The strongest new conclusion is that Brilliant Questing should
increasingly be understood not as a procedural quest generator, but as a
**lightweight causal simulation in which the player and NPCs can use a
shared vocabulary of actions to change persistent world state**.

A robbery is not generated because the player needs a quest. A robbery
happens because an actor has a motive, means, opportunity, and target.
The victim reacts. Witnesses learn fragments. Other actors may
intervene. A request, rumor, opportunity, bounty, investigation, feud,
or rescue mission may then emerge.

------------------------------------------------------------------------

# 1. Current repository reality

At the time of this addendum, the repository has moved beyond a
design-only scaffold.

Repository: `ImperfectRain/elin-brilliant-questing`

Observed current state:

-   Simulation core implemented.
-   Three-NPC procedural laboratory implemented.
-   Save/load and schema migration implemented.
-   Stable procedural identity and deterministic seeded generation
    implemented.
-   Event-ledger-driven consequence derivation implemented.
-   Facts and beliefs are separated.
-   False beliefs are possible.
-   Evidence/proof is distinct from belief.
-   Rumor propagation can degrade confidence/proof.
-   Twelve reusable actions/verbs exist across multiple solution
    families.
-   Options are generally hidden only for actual impossibility rather
    than low probability.
-   Four-outcome vanilla-shaped checks are modeled.
-   Situation escalation over time is implemented in the laboratory.
-   Restored events are not redispatched on load.
-   The same seed can reproduce the same causal story.
-   **[superseded]** The Elin/BepInEx runtime adapter now exists as
    `src/BrilliantQuesting.Plugin`: `IVanillaState`, `ICheckResolver` and
    `ISituationStager` implemented against the real assemblies, compiling
    clean, persisting into the save's own chunk store. It has not yet been
    observed running inside Elin, so treat it as unproven rather than
    working.
-   Combat resolution remains external: an `attack` action can record
    intent, while authoritative combat results should come back from
    Elin.
-   No LLM exists in the authoritative simulation path.
-   No duplicate persuasion/reputation/morality system has been
    introduced.

The repository roadmap correctly treats runtime integration against a
current Elin build as the immediate technical priority before broadening
the abstract simulation substantially.

## 1.1 Existing laboratory scenario

The current three-NPC laboratory remains the canonical small-scale
integration scenario:

-   A stole an item from B.
-   C witnessed it.
-   The world contains an objective fact about the theft.
-   Different actors can hold different knowledge/beliefs.
-   Player actions can question, persuade, lie, intimidate, bribe,
    search, expose, pickpocket, frame, return, keep, or attack.
-   Ignoring the situation causes autonomous escalation.
-   Early resolution prevents later escalation.
-   Unresolved events can lead to evidence movement, rumor spread, false
    accusation, and social rupture.
-   Every memory should correspond to an event that actually occurred.

------------------------------------------------------------------------

# 2. Immediate runtime integration requirements

The design is mature enough that the highest-value new information is
confirmation of what the current Elin build actually exposes.

## 2.1 Game files needed

**[superseded]** — all of the following were supplied and are staged under the gitignored `lib/`.
`Assembly-CSharp.dll` turned out to be a 6 KB stub; the game is `Elin.dll`. Kept for the next time
this list is needed.

Ideal integration package:

-   `Elin_Data/Managed/Elin.dll` --- highest priority.
-   Preferably the complete `Elin_Data/Managed/` directory.
-   Relevant Unity and Elin-specific managed dependencies.
-   `BepInEx/core/`.
-   A known-working current Elin script mod, if available, as a
    loader/reference example.
-   Current Source Sheet/modding data present in the installation.
-   Exact Elin version/build identifier.

Do not require personal Steam credentials, account information, or
unrelated user data.

## 2.2 Recommended disposable test save

Useful state includes:

-   varied attributes and skills;
-   nonzero fame/prestige;
-   Karma that can be safely modified;
-   town Influence;
-   guild membership;
-   religion/deity;
-   established Home and residents;
-   NPCs with differing affinity;
-   money/inventory sufficient for transfer, bribery, and economic
    tests.

Use a disposable or backed-up save while serialization/lifecycle hooks
remain unproven.

------------------------------------------------------------------------

# 3. Gate A: required in-game proof

The headless simulation is not sufficient evidence for Elin integration.
Gate A is truly passed only when the same principles operate against a
running game.

## 3.1 Plugin boot

Confirm game starts, plugin loads once, no loader/runtime exceptions
occur, and diagnostic logs are available.

## 3.2 Read-only vanilla state

Prove access to player attributes, skills, feats where relevant,
fame/prestige, Karma, town Influence, guild state, faith/deity,
inventory, Home state, residents, and target NPC affinity.

## 3.3 Native Check path

Determine whether current `Check`, `SourceCheck`, and related APIs can
be used directly. Prove final DC calculation and all four outcomes:
`CriticalPass`, `Pass`, `Fail`, `CriticalFail`.

`VanillaStyleCheckResolver` should remain a test double/fallback. Native
Elin resolution is preferable where it maps cleanly.

## 3.4 Controlled vanilla mutation

Modify one NPC's affinity and Karma by tiny known amounts. Verify
vanilla UI/state and persistence agree.

## 3.5 Brilliant Questing persistence

Create one procedural event/fact, save, fully quit, relaunch, load, and
confirm it exists exactly once and derived state is not double-applied.

## 3.6 Stable NPC binding

Bind procedural `EntityId` to real `Chara`. Verify identity survives
zone unload/re-entry and save/load without rebinding to a different
actor.

## 3.7 Crime/witness hook

Perform a controlled crime and determine actual witnesses, civilian vs
guard/legal response, Karma/hostility changes, and whether
`Point.TryWitnessCrime` or equivalent can be observed without replacing
vanilla crime resolution.

## 3.8 Drama/UI

Project at least one procedural choice into a real interaction. It must
use native state and update procedural state without making Drama
authoritative.

## 3.9 Generated/persistent site

Eventually prove a native Elin zone can be generated, associated with
procedural actors/objects, unloaded/reloaded, and persisted across
saves.

------------------------------------------------------------------------

# 4. Human testing protocol

Useful reports should contain:

``` text
Elin version:
Brilliant Questing commit/build:
Save used:
Zone/location:

Steps:
1.
2.
3.

Expected:
Actual:

Attached:
- full log
- save made after reproduction when relevant
- screenshot/video for visual/state mismatch
```

Full logs are preferable to screenshots of the final exception.
Video/screenshots are valuable for duplicate dialogue, wrong NPC
consequences, unexpected site regeneration, journal/world disagreement,
omniscient witnesses, or missing/invalid options.

------------------------------------------------------------------------

# 5. Four player-facing content classes

Not every simulated development should become a formal quest.

## 5.1 Requests

An actor deliberately asks the player to accomplish something. Usually
has an explicit client and desired outcome, often compensation, and may
enter the journal. The world continues regardless of acceptance.

## 5.2 Situations

A persistent problem exists and the player becomes aware of it. No
client or single intended solution is required. It may expose
conflicting stakeholders and evolves while ignored.

Examples: missing caravan, merchant disappearance, feud, false
accusation, food shortage.

## 5.3 Opportunities

A temporary world state can be exploited, helped, investigated, or
ignored.

Examples: bankrupt noble selling heirlooms, merchant paying premium for
lumber, traveler seeking passage, criminal seeking locksmith, town
needing medicine.

Often no `Quest Complete` is necessary. The transaction/state change is
the content.

## 5.4 Events

Things simply happen: marriage, caravan attack, shop closure, resident
death, guild member killing a target, conversion, etc. Events can create
requests, situations, and opportunities.

**Implementation rule:** do not automatically create a `Quest` object
for every narratively relevant event.

------------------------------------------------------------------------

# 6. Mechanical Coverage as a formal metric

> **If Elin considers a skill/system worth leveling or developing,
> Brilliant Questing should eventually create circumstances where that
> investment can matter.**

This does not mean every situation supports every skill. Track coverage
across the action library and situation ecology.

Example internal matrix:

``` text
Mechanic             Actions     Situation families     Coverage
Farming              6           4                      healthy
Cooking              8           6                      healthy
Negotiation          14          9                      high
Lockpicking          5           4                      healthy
Weightlifting        2           1                      thin
Investing            4           3                      improving
Faith                 3           2                      thin
Home/Public Safety   3           3                      improving
```

Crucial distinction: **mechanic, not dialogue tag**.

Bad: `[Farming 35] I know what happened to your crops.`

Better: inspect actual field; Farming contributes to diagnosis;
Learning/Perception refine it; Alchemy can identify contamination;
actual food production can solve the shortage; Home can become a
supplier.

Likewise, Mining should clear a real obstruction where practical,
Lockpicking should operate on a real lock, Pickpocket should obtain the
actual object, Investing should use actual investment, Cooking should
produce actual food, Home should physically support people, and Magic
should prefer actual spells/abilities over a generic Magic roll.

------------------------------------------------------------------------

# 7. Mass shipments and systemic demand

A community suggestion for mass shipment requests is highly compatible
with the project.

Simple template:

> Deliver 50 bottles of wine.

Brilliant Questing version:

``` text
Tavern loses supplier
 -> alcohol pressure rises
 -> owner seeks replacement supply
 -> local actors react
 -> player may learn about shortage
```

Possible solutions include producing wine, buying elsewhere and
arbitraging, negotiating another supplier, financing the business,
investigating the missing supplier, stealing a shipment, selling illicit
goods, arranging credit, or ignoring the problem.

## 7.1 Prefer categories/properties over exact IDs

Useful demand categories:

-   preserved food;
-   alcoholic drinks;
-   medicine;
-   lumber;
-   textiles;
-   furniture above quality threshold;
-   weapons;
-   livestock;
-   fertilizer;
-   emergency food;
-   luxury goods.

Avoid unnecessary quest-only item IDs.

## 7.2 Coarse economic pressure

A lightweight model is enough:

``` csharp
SettlementEconomicState
{
    Food;
    Alcohol;
    Medicine;
    Lumber;
    Textiles;
    Weapons;
    Luxury;
    Labor;
}
```

Events modify pressure:

``` text
crop failure      -> Food -25
festival          -> Alcohol demand +20
bandit activity   -> Weapons demand +15
epidemic          -> Medicine demand +30
construction      -> Lumber demand +20
refugee influx    -> Food/Textiles/Beds demand
```

This is not intended to become a full economic grand-strategy
simulation. Delivering 300 food during a shortage should make the
underlying shortage end sooner rather than only increment a quest
counter.

------------------------------------------------------------------------

# 8. Property-driven crafting commissions

Existing quest-expansion mods demonstrate demand for high-quality
crafting objectives. Generalize this into property-driven demand:

-   noble wants expensive/high-quality chair;
-   wounded adventurer needs medicine above efficacy threshold;
-   guild wants weapon above quality/material threshold;
-   ceremony requires valuable clothing;
-   settlement needs durable tools;
-   religious actor wants appropriate offering.

This rewards mastery of Elin's crafting systems without reducing
crafting to `hasRecipe(X)`.

------------------------------------------------------------------------

# 9. Guilds as information networks

Guilds should alter which parts of the simulated world reach the
player's attention, not merely provide separate random quest pools.

**Fighters:** dangerous monsters, bounties, escorts, rescues, violent
disputes, elite targets, missing fighters, defense contracts.

**Mages:** occult evidence, magical incidents, strange books, research,
reagents, artifacts, anomalous sites.

**Thieves:** fences, valuable shipments, stolen property, smuggling,
blackmail, security weaknesses, wanted criminals, kidnapping,
protection/extortion, forged identities.

**Merchants:** shortages, bankruptcies, caravans, investment, supply
contracts, trade disputes, rivalry, economic rescue.

The same event can be visible through multiple networks with different
framing. Avoid inventing parallel guild currencies unless necessary;
prefer vanilla contribution/rank/rewards.

------------------------------------------------------------------------

# 10. Situations should sometimes come to the player

The player should not always travel to a quest marker. Home, fame,
relationships, enemies, sheltered NPCs, wealth, and unresolved history
can bring consequences to them.

Examples:

-   sheltered fugitive is discovered;
-   bandits raid Home;
-   creditor arrives;
-   merchant comes to negotiate;
-   accused NPC demands satisfaction;
-   displaced family asks for shelter;
-   rival sends an agent;
-   guild requests emergency assistance;
-   former enemy seeks help;
-   witness arrives to confess.

This makes Home a narrative location rather than only downtime
infrastructure.

------------------------------------------------------------------------

# 11. Socially recognized conflict

The event ledger must distinguish combat context from combat result.
`NPC died` can mean duel, lawful bounty, arrest attempt, self-defense,
assassination, faction battle, robbery gone wrong, murder, or defense of
Home.

Brilliant Questing should not replace combat. Record intent/context,
observe authoritative vanilla outcome, identify witnesses/evidence, then
derive consequences.

------------------------------------------------------------------------

# 12. Reward philosophy

Prefer systemic/emergent rewards:

-   actual orens;
-   Karma;
-   fame/prestige;
-   Influence;
-   affinity;
-   guild contribution;
-   shop investment/service;
-   recruitment/residency;
-   access;
-   information/evidence;
-   debt forgiveness;
-   discounts;
-   training;
-   safe house;
-   contact;
-   ownership of object;
-   persistent site access;
-   organization relationship.

If player keeps artifact, artifact itself is reward. If player rescues
craftsman, recruitment/access may be reward. If player finances shop,
its survival may be reward. If player blackmails someone, continued
information/payments may be reward.

Avoid attaching a high-tier loot chest to every resolution.

------------------------------------------------------------------------

# 13. Inventory safety and compatibility

Quest mods have exposed compatibility hazards around broad quest-item
confiscation.

Rules:

-   Do not broadly scan/delete matching inventory categories unless
    unavoidable.
-   Prefer explicit player selection/transfer.
-   Maintain stable ownership/provenance for narratively important
    objects.
-   Do not assume any category match is safe to consume as payment.
-   Avoid deleting third-party mod items accidentally.
-   Log exactly what transferred and why.

For 40 herbs, explicit transfer is safer than silently deleting 40
matching objects.

------------------------------------------------------------------------

# 14. Avoid unnecessary custom arenas and artificial loot

Prefer locations in this order:

1.  existing persistent location;
2.  existing town/zone;
3.  existing wilderness/Nefia;
4.  player's Home;
5.  generated native persistent site when the situation genuinely
    requires one.

Do not create a bespoke arena merely because "quests need quest maps."

Likewise, generated targets should use ordinary Elin
equipment/generation rules wherever practical. Do not manufacture
unusually valuable loot solely to make a procedural target rewarding.

------------------------------------------------------------------------

# 15. Activities, competitions, festivals, and contracts

Quest Board Expansion demonstrates a useful pattern: temporarily making
ordinary sandbox activity more meaningful.

Potential contexts:

-   fishing tournament;
-   harvest competition;
-   cooking festival;
-   monster-hunting season;
-   mining contract;
-   religious pilgrimage;
-   merchant trade challenge;
-   exploration society expedition;
-   guild recruitment drive;
-   charitable food drive;
-   construction effort;
-   town beautification;
-   craft fair;
-   strongman competition;
-   market week.

Not every procedural thread should be murder, kidnapping, or conspiracy.

NPCs should participate where practical. The player can lose. Results
become history: `Won Palmia Harvest Festival, Year 4`,
`Defeated player in public duel`, etc.

------------------------------------------------------------------------

# 16. Do not assume the player is heroic

Generate stakeholder interests, not moral verdicts.

Example state:

`Merchant wants rival shipment delayed.`

Possible player behavior: sabotage it, warn rival, blackmail requester,
steal shipment, expose scheme, negotiate settlement, accept payment and
betray requester, discover a third party caused conflict, or ignore it.

Karma remains legal/public status rather than universal authorial
morality.

------------------------------------------------------------------------

# 17. Procedural opportunities and distressed sellers

The "desperate for money" seller concept is valuable precisely because
it barely behaves like a quest.

Generalize into:

-   jeweler liquidating workshop;
-   indebted noble selling heirlooms;
-   farmer selling resources cheaply;
-   traveler seeking paid passage;
-   merchant paying premium for scarce category;
-   criminal paying for discreet service;
-   family selling treasured object to cover debt;
-   closing shop clearing inventory.

No formal completion event is necessary.

------------------------------------------------------------------------

# 18. Rumor chains

Rumors are transformations of knowledge, not flavor text.

Track source, confidence, attached evidence, transmission count,
distortion, secrecy, emotional bias, and recipient trust where useful.

Possible rules:

-   high Fame causes player-related rumors to travel farther/faster;
-   affinity affects willingness to believe favorable/unfavorable
    claims;
-   organization membership affects routing;
-   witnesses have stronger confidence than hearsay recipients;
-   retellings may preserve proposition while losing proof;
-   actors can deliberately spread false rumors.

Rumors can cause false accusations, gratitude, hostility, requests,
investigations, opportunities, bounty interest, and defensive
preparation.

------------------------------------------------------------------------

# 19. Recurring NPC importance should be emergent

Do not designate all important characters at generation. Minor actors
can gain importance through repeated causal intersections.

Candidates: failed thief, irritating merchant, cowardly adventurer,
escaped bandit, recurring witness, persistent guard, unusually useful
resident.

Importance can consider event count, relationship strength, unresolved
goals, network connectivity, important knowledge, direct player
interaction, survival of major events, ownership of important
object/site, and organization position.

------------------------------------------------------------------------

# 20. Dynamic bounties and manhunts

Bounties can emerge from murder, theft, kidnapping, escape, monster
attack, guild conflict, or banditry.

Targets may be guilty, falsely accused, already dead, hiding, protected,
related to player, or someone player previously helped.

Therefore a bounty is a **projection of belief/legal state**, not
omniscient truth.

------------------------------------------------------------------------

# 21. Item provenance

Notable objects can carry structured provenance:

-   crafted by;
-   owned by;
-   gifted by;
-   stolen from;
-   recovered at;
-   used in;
-   found on corpse of;
-   sold by;
-   evidence in;
-   inherited by.

A silver ring found months later may connect to a missing merchant.
Showing it to the daughter can reopen an old thread.

Track only notable objects; do not preserve every berry's biography.

------------------------------------------------------------------------

# 22. NPC favors as concrete social debt

Affinity remains vanilla-facing, but event-derived obligations can add
specificity without becoming another reputation currency.

Possible favors: hide someone, provide testimony, lend equipment,
manipulate shop, introduce player, provide information, ignore minor
offense, arrange transport, vouch for player, assist a scheme.

Favors arise from events and relationships.

------------------------------------------------------------------------

# 23. Profession-based solutions

Ordinary professions/skills should create agency:

-   farmer diagnoses crop disease;
-   cook prepares favorite meal or feeds refugees;
-   investor rescues/acquires failing business;
-   alchemist identifies poison/contamination;
-   carpenter repairs structure;
-   miner clears collapse;
-   anatomist examines corpse/wounds;
-   literate actor reads/compares records;
-   appraiser identifies suspicious goods;
-   musician affects social gathering where vanilla supports it;
-   traveler understands routes;
-   fisher contributes to food drive/tournament;
-   crafter satisfies property-constrained commission.

------------------------------------------------------------------------

# 24. Generated local mysteries

Small mysteries fit Elin well:

-   missing livestock;
-   repeated food theft;
-   strange objects in a house;
-   resident refusing to work;
-   unexplained death;
-   counterfeit goods;
-   damaged crops;
-   disappearing shipments;
-   recurring trespass;
-   missing tools.

Explanations may be mundane, criminal, interpersonal, economic,
magical/supernatural where supported, or absurd but mechanically
grounded. Generate facts first, mystery presentation second.

------------------------------------------------------------------------

# 25. Consequential vanilla accidents

Observe noteworthy vanilla events where practical. If a town NPC dies to
a random monster, potential consequences include relatives remembering
it, service loss, inheritance, fear, revenge, vacancy, or future
references.

Brilliant Questing should convert Elin chaos into history rather than
requiring all interesting events to originate inside the mod.

------------------------------------------------------------------------

# 26. Off-screen NPC schemes

NPCs can pursue goals while player is elsewhere: steal, court, invest,
flee debt, join organization, seek revenge, open/close business, hire
thugs, move, sell property, investigate, spread rumor, hide evidence.

Off-screen simulation should be compressed and explainable, not literal
full pathfinding/action simulation.

------------------------------------------------------------------------

# 27. Witness disagreement

Multiple witnesses can interpret the same event differently.

Example: Witness A sees player kill NPC and believes murder. Witness B
saw the NPC attack first and believes self-defense. Guard initially
hears only A. Finding B becomes meaningful testimony.

This makes evidence, affinity, Fame, testimony, bribery, intimidation,
and rumor mechanically relevant.

------------------------------------------------------------------------

# 28. Generated social networks

Useful relations include siblings, parent/child, lovers/spouses,
business partners, creditor/debtor, rivals, mentor/apprentice, guild
contacts, friends, enemies.

Five connected actors generate more causal possibilities than five
independent quest givers. Relationships affect goals, information flow,
risk tolerance, and consequences.

------------------------------------------------------------------------

# 29. Debt as a causal primitive

Debt can connect economy, crime, social relationships, and Home.

An actor may owe a merchant, guild, criminal, landlord, family member,
or friend.

Player can pay debt, buy debt, negotiate, steal/destroy records,
intimidate creditor, help debtor earn money, expose fraud, recruit
debtor, profit from distress, enforce debt, or hide debtor.

------------------------------------------------------------------------

# 30. Home as sanctuary

NPCs may need shelter because they are refugees, fugitives, injured,
broke, displaced, threatened, witnesses, or wanted criminals.

Actual Home state should matter: beds, food, Public Safety, Public
Morality, population capacity, resident jobs, wealth/resources,
policies.

Sheltering someone may create downstream risk.

------------------------------------------------------------------------

# 31. Apprentices and protégés

An NPC repeatedly helped by player may imitate them, train related
skills, seek recruitment, join adventures, adopt compatible
religion/guild tendencies where plausible, become competent, or become
hilariously incompetent.

This should emerge from repeated relationship/history rather than a
scripted protégé questline.

------------------------------------------------------------------------

# 32. Situation inheritance

Death/disappearance of a participant should not automatically delete a
thread. Goals, assets, debts, and secrets may transfer to spouse, heir,
rival, creditor, killer, or organization.

Examples: creditor pursues heir, child inherits shop, rival acquires
property, guild continues investigation, murderer steals victim's
evidence.

------------------------------------------------------------------------

# 33. Adventurer ecology: the world can solve its own problems

One of the strongest post-master conclusions:

> **Situations do not belong to the player. Other actors should be able
> to pursue them.**

A kidnapping occurs. Player ignores it. Another adventurer attempts
rescue. They may succeed, fail, be captured, die, kill hostage
accidentally, steal ransom, discover another problem, become famous,
join captors, or retreat and spread information.

The world should occasionally solve its own quests. The player is
important because of accumulated capability and relationships, not
because simulation freezes until `Accept Quest`.

------------------------------------------------------------------------

# 34. Simulated traveling groups

Persistent groups can include adventuring parties, merchant caravans,
pilgrims, refugees, bandits, hunters, expeditions, guild patrols.

Off-screen groups do not require tile-by-tile simulation.

``` csharp
CaravanJourney
{
    origin = Palmia;
    destination = Noyel;
    departureDay = 142;
    expectedArrivalDay = 147;
    routeDanger = 0.36;
    cargo;
    members;
}
```

At milestones, resolve meaningful events. Then "the merchant never
arrived" can mean a simulated caravan actually failed to arrive rather
than a quest generator inventing one solely for the player.

------------------------------------------------------------------------

# 35. NPCs should use the same Action Resolver

Avoid separate conceptual systems for player action and NPC story
simulation. Prefer:

> **Actor attempts action against world state.**

The player is one actor with direct human control. NPCs should use
actual vanilla stats/capabilities where available.

Examples: incompetent courier may lose item; excellent mercenary
performs better; bad investigator may accuse wrong person; skilled thief
steals more effectively; capable merchant negotiates better.

Actions should increasingly support both player and NPC execution:

``` csharp
NarrativeAction
{
    Actor;
    Target;
    Preconditions;
    MechanicalCheck;
    Costs;
    Exposure;
    ImmediateEffects;
    WorldEffects;
    MemoriesCreated;
    KnowledgeTransferred;
    AIUtilityInputs;
}
```

------------------------------------------------------------------------

# 36. Routine activities as narrative delivery

Interesting state should not live only in menus.

Information can surface while player eats at Home, travels with
companion, shops, worships, sleeps at inn, fishes beside NPC, works on
farm, visits guild, interacts with residents, or passes through town.

Examples: resident mentions rumor over dinner; companion recognizes NPC;
merchant complains about supplier; worshipper approaches after prayer;
inn guests argue; farmer points out crop problem.

------------------------------------------------------------------------

# 37. Optional "What's been happening?" information surface

Tavernkeepers, guards, residents, merchants, companions, and guild
contacts can expose 1--3 high-salience developments they actually know.

Responses may include rumors with uncertainty, local economic pressure,
missing persons, recent conflict, or opportunities.

Do not make this an omniscient world-news terminal unless the source
legitimately has broad information access.

------------------------------------------------------------------------

# 38. Relationship-dependent disclosure

Affinity should influence not only willingness but how much an NPC
reveals.

Conceptual progression:

``` text
Low:       "I don't have anything to say."
Moderate:  "I've been having trouble with someone."
Friendly:  "It's my brother. He's in debt."
High:      "He borrowed money from Varik."
Very high: "I think Varik is threatening him."
Trusted:   "There's something I didn't tell the guards..."
```

Exact thresholds require balancing. Personality, secrecy, fear, loyalty,
evidence, Fame, organization, and history can modify disclosure.

------------------------------------------------------------------------

# 39. Occupation/skill-dependent interpretation

Different actors/builds can derive different semantic facts from the
same evidence.

Dead crops example:

-   guard: crops died;
-   farmer: soil/disease problem;
-   alchemist: chemical contamination;
-   perceptive investigator: deliberate contamination;
-   merchant: someone profits from shortage.

Possible architecture:

``` csharp
Observation
{
    RawEvidence;
    Observer;
    DerivedFacts[];
    Confidence;
}
```

This is stronger than alternate colored dialogue choices because
capability changes what the actor can infer.

------------------------------------------------------------------------

# 40. Persistent location history

Procedural sites should accumulate history rather than remain disposable
maps.

Example: abandoned mine -\> bandits occupy it -\> bandits killed -\>
necromancer exploits corpses -\> town sends adventurers -\> one
disappears.

The location can become
`Old Beryl Mine — former Red Knives hideout; site of the Beryl massacre; last known destination of Taris the Younger.`

Track only notable events to avoid data explosion.

------------------------------------------------------------------------

# 41. Regional legends

Repeated/high-salience events can create legends attached to locations,
NPCs, groups, or objects: mine where three caravans vanished, goat that
killed two adventurers, innkeeper who shelters criminals, sword
associated with several murders.

Legends are compressed history and can influence rumor, fear, demand, or
future generation weights.

------------------------------------------------------------------------

# 42. Procedural grudges over trivial things

Elin's tone supports disproportionate consequences from mundane
interactions: repeatedly waking NPC, buying out shop, stealing chair,
killing favorite animal, winning competition, romantic jealousy, public
embarrassment, refusing minor favor.

Most stay minor. Occasionally one becomes a recurring rivalry through
subsequent causal events. Humor should emerge from escalation, not
forced joke writing.

------------------------------------------------------------------------

# 43. Absurd causal escalation

Do not artificially isolate "serious" and "silly" premises. A stolen pie
can lead to accusation, family feud, arrest, shop closure, criminal
recruitment, flight to a mine, generated hideout, and later
bounty/rescue.

The comedy comes from every step being mechanically explicable.

------------------------------------------------------------------------

# 44. Bad solutions work

Do not reject mechanically valid outcomes because they were not authored
resolutions.

If player resolves a hostage crisis by killing everyone, observe
destruction/deaths, determine survivors, update facts/witnesses, let
vanilla legal state change, update relationships, close/mutate the
original problem, and generate downstream consequences.

A client may consider the request failed, but the world must not pretend
nothing happened.

------------------------------------------------------------------------

# 45. Simulation first, language second

AI-dialogue mods expose a common failure mode: NPCs can say they will do
things without the world changing.

Brilliant Questing must preserve the inverse:

``` text
Goal
 -> actor decision
 -> simulated/native action
 -> actual event
 -> witnesses/knowledge
 -> dialogue rendering
```

Never:

``` text
LLM dialogue
 -> implied action
 -> no authoritative state change
```

Optional richer prose may render established state. It may not decide
authoritative facts, checks, inventory changes, deaths, ownership, or
consequences.

------------------------------------------------------------------------

# 46. Failure-forward reinforcement

Every checked action should consider all four result classes.

Example intimidation:

``` text
CriticalPass: target yields and may reveal extra information
Pass:         target complies
Fail:         target refuses and suspicion increases
CriticalFail: target interprets threat as challenge / alerts ally / causes public scene
```

Critical failures should be consequential, explainable, sometimes funny,
and not arbitrary punishment.

------------------------------------------------------------------------

# 47. Festivals as historical generators

Competition results can create persistent relationships and history. If
player loses strongman contest to an NPC, that NPC may gain local fame,
rivalry may form, next year's event can reference prior winner, and
sponsorship/rumor opportunities can emerge.

This is low-cost continuity.

------------------------------------------------------------------------

# 48. Player production should connect to society

Optimized production should not become economically abstract. Large
farms, wine production, workshops, ranching, cooking, and Home
infrastructure should matter because simulated actors need what player
produces.

Demand should be caused by world conditions when possible.

------------------------------------------------------------------------

# 49. Home/settlement integration expansion

Home can house refugees, hide fugitive, recruit displaced specialist,
host witness, store contraband, produce emergency supplies, suffer
retaliation, attract merchant, become meeting site, or serve as guild
staging area.

Home quality/resources should alter feasibility/outcomes. Low Public
Safety plus hidden fugitive may increase discovery risk; high food
supply may support refugees; overpopulation should matter.

------------------------------------------------------------------------

# 50. Organization/network information routing

Information can propagate through household, guild, caravan, criminal
network, religious community, workplace/business, friendship,
witness-to-guard, or tavern rumor.

The same fact should spread unevenly. The Thieves Guild may learn that a
stolen ring is being fenced before the victim's family learns who has
it.

------------------------------------------------------------------------

# 51. High-priority synergy: provenance + rumors + recurring NPCs

These three systems together make ordinary Elin events historical.

Example: minor thief steals ring -\> witness sees thief -\> thief sells
ring -\> fence mentions it -\> player later finds ring -\> daughter
recognizes ring -\> thief has become recurring criminal -\> old witness
now belongs to guild -\> months-old theft matters again.

No large authored quest chain is required.

------------------------------------------------------------------------

# 52. High-priority synergy: demand + traveling groups

``` text
Palmia alcohol shortage
 -> merchant contracts caravan
 -> caravan carries alcohol
 -> bandits attack caravan
 -> shipment fails
 -> shortage worsens
 -> tavern raises demand
 -> stolen wine appears in criminal market
 -> Fighters hear bounty
 -> Thieves hear fence opportunity
 -> player learns whichever network they access
```

This single chain touches economy, travel, combat, crime, guild
information, trade, rumor, opportunity, and requests.

------------------------------------------------------------------------

# 53. Simulation tiers remain essential

As NPC autonomy expands, simulation compression becomes more important.

**ACTIVE:** current/nearby area, detailed interactions, live native
state.

**WARM:** important/recent NPCs, daily/coarse updates, active threads,
traveling groups.

**COLD:** minor persistent entities, infrequent/coarse updates,
aggregate changes.

**ARCHIVED:** historical entities/events, no active simulation until
referenced.

The goal is causal richness, not simulating every citizen every turn.

------------------------------------------------------------------------

# 54. Narrative Director should prioritize attention, not author reality

Director responsibilities:

``` text
Observe world
Score tensions/opportunities
Allocate simulation/attention budget
Select presentation channel
Possibly instantiate constrained developments
Never overwrite established causal truth
```

The director decides what deserves attention and what reaches player,
not arbitrary truth needed for pacing.

------------------------------------------------------------------------

# 55. Mod ecosystem findings

## 55.1 RandomQuest & GuildQuest Expansion Pack --- Workshop 3579277196

Useful concepts observed/reported:

-   general request expansion;
-   guild-exclusive requests;
-   duels;
-   Nefia raids;
-   rescues;
-   item recovery;
-   farmland conflicts;
-   urban extermination;
-   high-quality crafting;
-   flyer distribution;
-   investment;
-   base shipment/sales;
-   stolen-goods sales;
-   guild defense.

Primary lesson: ordinary Elin activities are viable quest gameplay.
Brilliant Questing should create causal reasons those activities matter
rather than copying template structure.

Community-feedback cautions: broad quest-item confiscation can interact
badly with other mods; custom map routing can break; guaranteed
high-tier rewards can damage balance.

## 55.2 Quest Board Expansion --- Workshop 3661956141

Useful concepts observed/reported:

-   assassination;
-   outlaw hunts;
-   food charity;
-   weightlifting competition;
-   exploration competition;
-   religious violence;
-   flyer distribution;
-   Karma challenges;
-   distressed-seller opportunity.

Primary lessons: ordinary play can gain temporary contextual value;
generated objectives need not be heroic; distressed economic states can
create opportunities rather than quests.

## 55.3 Omega's Quest Picker --- Workshop 3384541453

Can detect compatible random quests added by other quest mods and expose
configuration. Long-term implication: consider discoverable registration
and player control over procedural content categories.

Potential future settings: situation-family enable/disable,
frequency/intensity, criminal weighting, tragedy/lethality, economic
content, competitions, Home intrusion, off-screen autonomy. Do not
overbuild configuration before core simulation is proven.

## 55.4 KK Caravan

Observed concept: companion caravan/standby group whose members remain
integrated with travel/affinity-related systems and other gameplay
contexts.

Derived idea: simulated traveling groups, including caravans,
adventurers, refugees, pilgrims, and bandits.

## 55.5 Elin with AI

Observed concept: AI-driven conversation and interest in quest/context
generation.

Derived lesson: language without authoritative action is hollow. Keep
Brilliant Questing simulation-first.

## 55.6 AutoExplore / Skill Helper ecosystem

AutoExplore coordinates ordinary Elin actions including combat, trap
disarming, looting, gathering, mining, meditation, sleep, shrine
interaction, and food consumption.

This is primarily implementation precedent, but it reinforces that
Elin's normal action vocabulary is broad enough to serve as quest
gameplay without parallel minigames.

## 55.7 SkyreaderGuild

A public mod repository describes custom guild content around meteor
tracking, cleansing affected NPCs, procedural Astral Rifts, bosses,
progression, crafting, furniture, and monsters.

Potential inspiration: persistent/generated sites can support
organization content and procedural locations can integrate with
progression. Still apply the vanilla-first rule.

## 55.8 Elin Together

Elin Together is a substantial public multiplayer mod and useful build
precedent. Its documentation expects local references under:

``` text
ElinGamePath/
├─ BepInEx/core/*.dll
└─ Elin_Data/Managed/*.dll
```

This independently supports keeping game assembly references isolated in
the runtime integration project.

It also demonstrates that multiplayer/client authority is complex.
Brilliant Questing should not claim multiplayer compatibility until
state ownership and synchronization are explicitly designed/tested.

------------------------------------------------------------------------

# 56. Early Access compatibility

Elin remains active Early Access; stable updates can break mods.

Implementation consequences:

-   keep Elin-specific code behind adapters;
-   no game assembly references in `BrilliantQuesting.Core`;
-   isolate Harmony patches;
-   document tested Elin build;
-   fail gracefully when hook cannot resolve;
-   add startup diagnostics for expected methods/fields;
-   centralize reflection/version-sensitive access;
-   maintain procedural save migration versions;
-   create smoke-test checklist for game updates.

------------------------------------------------------------------------

# 57. Proposed runtime project boundary

``` text
BrilliantQuesting.Core
    no Elin.dll
    no Unity
    no BepInEx
    deterministic simulation
    persistence model
    actions/facts/events/threads

BrilliantQuesting.Elin
    Elin.dll references
    BepInEx
    Harmony
    IVanillaState implementation
    save lifecycle bridge
    Chara/Thing/Zone identity bridge
    Drama/UI bridge
    crime/witness hooks
    native Check adapter

BrilliantQuesting.Lab
    headless scenario runner
```

The runtime project should be replaceable/repairable when Elin changes
without rewriting simulation.

**[superseded, in detail]** The boundary is exactly right and is what was built; two specifics
differ from the sketch above.

The runtime project is named `BrilliantQuesting.Plugin`, not `BrilliantQuesting.Elin`.

More importantly, the *source* boundary and the *shipped* boundary are not the same thing. Elin's
Package Chainloader scans a package folder for BepInPlugin types, and if enumerating them needs a
sibling assembly it does not resolve, the type load fails and the package reports zero plugins with
no error - indistinguishable in the log from an empty folder. So Core's sources are compiled *into*
the plugin assembly and one DLL ships. Core remains a separate project with its own tests and is
still forbidden from referencing Elin; it simply is not a separate file at runtime.

That merge has a consequence for everything proposed in §60 and §61: the shipped assembly sees
Elin's global namespace, so a Core type sharing a name with a game type becomes ambiguous at
compile time. `Goal` had to be renamed `NpcGoal`. Anything new must avoid the game's generic names -
`Zone`, `Map`, `World`, `Check`, `Element`, `Faction`, `Religion`, `Quest`.

There is also a fourth project not in the sketch: `tools/ApiDump`, which prints the public surface
of the game assemblies without executing them. Every vanilla call in the adapter was chosen by
reading its output.

------------------------------------------------------------------------

# 58. Long-term interoperability API

Preserve room for other mods to register capabilities later:

``` csharp
RegisterNarrativeCapability(...)
RegisterSituationArchetype(...)
RegisterAction(...)
RegisterEvidenceType(...)
RegisterOrganization(...)
RegisterEventObserver(...)
```

Potential uses: modded skill solves procedural problem, custom race has
capability, caravan reports journey, custom dungeon reports event,
modded organization becomes information network.

Do not build a large public API before internal abstractions stabilize.

------------------------------------------------------------------------

# 59. Player configuration as content preference

Potential future settings should control generation/presentation
weights, not manually author outcomes:

``` text
Narrative activity: Low / Normal / High
Off-screen NPC autonomy: Low / Normal / High
Home intrusion events: On / Off
Criminal situations: Low / Normal / High
Economic situations: Low / Normal / High
Competitions/festivals: Low / Normal / High
Lethal escalation: Reduced / Normal
Rumor verbosity: Low / Normal / High
Debug causality display: Off / On
```

------------------------------------------------------------------------

# 60. Suggested additional data structures

Conceptual only; not final API contracts.

## Opportunity

``` csharp
public sealed class NarrativeOpportunity
{
    public EntityId Id;
    public OpportunityType Type;
    public EntityId SourceEntity;
    public EntityId? Location;
    public GameTime CreatedAt;
    public GameTime? ExpiresAt;
    public List<FactId> SupportingFacts;
    public List<ActionId> PlausibleActions;
    public float Salience;
}
```

## Economic pressure

``` csharp
public sealed class LocalDemandState
{
    public EntityId LocationId;
    public Dictionary<ResourceCategory, float> SupplyPressure;
    public List<EventId> Causes;
    public GameTime LastUpdated;
}
```

## Traveling group

``` csharp
public sealed class TravelingGroup
{
    public EntityId Id;
    public GroupType Type;
    public List<EntityId> Members;
    public EntityId Origin;
    public EntityId Destination;
    public GameTime Departure;
    public GameTime ExpectedArrival;
    public float RouteDanger;
    public List<EntityId> Cargo;
    public GroupState State;
}
```

## Provenance

``` csharp
public sealed class ProvenanceRecord
{
    public EntityId ObjectId;
    public List<ProvenanceEvent> Events;
}
```

## Social obligation

``` csharp
public sealed class SocialObligation
{
    public EntityId Debtor;
    public EntityId Creditor;
    public ObligationType Type;
    public float Strength;
    public EventId Origin;
    public bool Resolved;
}
```

------------------------------------------------------------------------

# 61. Action library expansion priorities

The current twelve verbs should expand according to mechanical coverage
and runtime proof, not arbitrary count.

**Economic:** buy, sell, invest, pay debt, purchase debt, finance,
commission, supply, transport, hire, negotiate price, purchase
information.

**Home/community:** shelter, host, recruit, assign resident, supply
settlement, hide fugitive, protect witness.

**Investigation:** inspect, track, appraise, read, translate, compare
testimony, examine corpse, examine tracks, identify substance,
eavesdrop, follow.

**Crime:** trespass, plant evidence, destroy evidence, forge, fence,
smuggle, sabotage, extort, kidnap, impersonate.

**Crafting/production:** prefer actual vanilla production completion
events; commission, repair, build, cook, brew, alchemy, farm supply,
craft to property threshold.

**Faith/magic:** prefer actual abilities, spells, offerings, altars, and
deity state.

------------------------------------------------------------------------

# 62. Preconditions: impossibility vs difficulty

Continue the existing rule: low chance is not a reason to hide an
action.

Hard gate only genuine impossibility/unknown information.

Impossible examples: blackmail without leverage; reveal unknown secret;
offer item not owned; invoke guild authority without membership;
identify unknown person when identity is required.

Difficult-but-possible examples: lie to perceptive NPC; intimidate
stronger opponent; persuade hostile merchant; sneak past observant
guard.

------------------------------------------------------------------------

# 63. Information is gameplay inventory

Facts can be learned, traded, revealed, concealed, falsified,
corroborated, contradicted, weaponized, or used as leverage.

Examples: knowing shipment route enables robbery; knowing affair enables
blackmail; knowing alibi prevents false conviction; knowing buyer
enables fencing; knowing family relationship changes persuasion.

Knowledge remains actor-local.

------------------------------------------------------------------------

# 64. Evidence is not truth

Keep explicit separation.

Truth: A stole ring.

Evidence: witness testimony, ring found in A's room, ledger entry,
footprints.

Evidence can be genuine, misleading, planted, destroyed, weak, strong,
or inaccessible. Actors can believe false facts based on convincing
evidence.

------------------------------------------------------------------------

# 65. Fame changes visibility and preparation

Fame is more than difficulty scaling. As Fame rises, strangers recognize
player more often, rumors travel farther, intimidation may become
easier, anonymity becomes harder, powerful actors seek player, enemies
prepare specifically, and player testimony/endorsement may carry more
weight.

Fame should not be universally positive.

------------------------------------------------------------------------

# 66. Affinity = vanilla value + structured reasons

Do not duplicate affinity. Store event reasons, memories, obligations,
secrets, and other structured context. Vanilla affinity remains
player-facing relationship magnitude; procedural state explains why and
what it enables.

------------------------------------------------------------------------

# 67. Karma remains legal/public status

Negative Karma can create alternate content: fences, smuggling,
blackmail, prison breaks, criminal contacts, safe houses. Criminal
players should get a different social landscape, not simply fewer
quests.

------------------------------------------------------------------------

# 68. Town Influence as political currency

Influence is a plausible vanilla resource for audiences, guard
assistance, civic favors, request manipulation, public projects, or
settlement intervention. Exact costs/APIs require runtime verification.
Prefer actual Influence over new political points.

------------------------------------------------------------------------

# 69. Religion uses actual religious state

Procedural religious actions should prefer deity, Faith, piety if
accessible, offerings, altar access, deity abilities, and religious
attitudes over generic `[Religion]` checks.

------------------------------------------------------------------------

# 70. Build diversity review rule

Every major situation archetype should normally expose at least three
genuinely different solution families, ideally more. A missing caravan
might support combat/rescue, investigation/tracking, ransom/economy,
social negotiation, criminal collaboration, guild authority, and actual
magical abilities.

Do not force every route into every scenario. Require meaningful
plurality, not checkbox symmetry.

------------------------------------------------------------------------

# 71. Situation archetype review checklist

Before accepting a new archetype, answer:

1.  What underlying world state caused it?
2.  Which actors have goals?
3.  What happens if player never learns about it?
4.  What happens if player learns and ignores it?
5.  What are at least three distinct solution families?
6.  Which vanilla mechanics are used directly?
7.  Which facts are objective?
8.  Who initially knows each fact?
9.  What evidence exists?
10. What can be falsified/destroyed?
11. What does a critical failure look like?
12. What persistent entities can survive resolution?
13. Can another NPC resolve or worsen it?
14. What systemic rewards/consequences can result?
15. Does it create a future hook without scripted sequel?
16. Does it require custom map? Why?
17. Does it require quest-only item? Why?
18. Does it duplicate an Elin system?
19. Can debug mode explain its existence?
20. Can it survive save/load deterministically?

------------------------------------------------------------------------

# 72. Recommended near-term development sequence

Do not implement all ideas immediately.

**Stage 1 --- Runtime seam:** obtain current assemblies; create
`BrilliantQuesting.Elin`; minimum `IVanillaState`; plugin boot/read
state; affinity/Karma mutation; native Check proof; save attachment.

**Stage 2 --- Gate A in game:** run existing three-NPC scenario against
actual Elin actors/state.

**Stage 3 --- Crime + knowledge:** prove witness hook and local
knowledge.

**Stage 4 --- Drama projection:** expose actions through real
UI/dialogue.

**Stage 5 --- Persistent entity/site:** prove stable Chara/site binding.

**Stage 6 --- Mechanical coverage:** add a few high-value economic,
investigation, Home, and production routes.

**Stage 7 --- Systemic demand:** implement shortage/mass-shipment
situation with multiple solution families.

**Stage 8 --- Autonomous NPC:** allow NPC to pursue one existing
situation/action through same resolver. This is smallest proof that the
world can solve its own quests.

**Stage 9 --- Traveling group:** one caravan with
origin/destination/deadline/danger and coarse outcome.

**Stage 10 --- Presentation diversification:** distinguish Request /
Situation / Opportunity / Event.

Only then expand content count aggressively.

------------------------------------------------------------------------

# 73. Vertical slice: shortage -\> caravan -\> theft

Initial state:

``` text
Town tavern has alcohol shortage.
Merchant offers to source wine.
Caravan is created carrying wine.
Bandit group needs money.
```

Progression:

``` text
shortage
 -> demand opportunity
 -> caravan departs
 -> bandits attack
 -> cargo stolen
 -> caravan late
 -> shortage worsens
 -> merchant requests help
 -> stolen wine enters criminal network
 -> guilds learn different fragments
```

Player can produce replacement wine, buy elsewhere, rescue caravan, hunt
bandits, buy stolen wine, negotiate return, expose fence, join bandits,
finance tavern, or ignore everything.

This exercises economy, group travel, provenance, crime, knowledge,
guild information, production, NPC autonomy, and persistence.

------------------------------------------------------------------------

# 74. Vertical slice: Home sanctuary

Initial state: NPC is accused/wanted, trusts player, requests shelter.
Accusation may be true or false; evidence exists; guards/others know
fragments; Home has capacity/safety/resources.

Actions: shelter, refuse, turn in, investigate, bribe pursuer, move NPC,
recruit, expose truth, hide evidence, betray later.

Consequences: Home risk, affinity, Karma/legal response, new resident,
raid/search, future favor, reputation.

------------------------------------------------------------------------

# 75. Vertical slice: festival

Town schedules harvest festival. NPCs enter. Temporary food/alcohol
demand rises. Competition accepts qualifying produce.

Player can compete, sell into demand, cook, sponsor, sabotage, steal
rival entry, or ignore. NPCs can win and history persists.

This is a low-stakes counterweight to crime-heavy scenarios.

------------------------------------------------------------------------

# 76. Debugging and explainability

Debug mode must answer:

``` text
Why does this situation exist?
Which event caused it?
Which actors are involved?
What does each actor want?
What does each actor know?
What evidence exists?
Why is this action available/hidden?
What check/DC is used?
Why did this NPC choose this action?
Which consequence changed affinity/Karma/etc.?
Why did this rumor reach this NPC?
Why did this thread escalate?
```

Without causal introspection, emergent simulation becomes impossible to
debug.

------------------------------------------------------------------------

# 77. Determinism and replayability

Maintain reproducibility for situation generation, scheduled
developments, NPC decision selection where feasible in headless tests,
rumor propagation choices, and derived state.

Live Elin combat/check RNG may not be under core control. Record
authoritative results returned by Elin as events so load/replay does not
reroll them.

------------------------------------------------------------------------

# 78. Save migration rules

-   schema version every persistent representation change;
-   one-way tested migrations;
-   old events remain interpretable;
-   fail gracefully on new/unknown fields where format permits;
-   derived caches are not sole source of truth;
-   never redispatch historical events on load;
-   archive rather than delete history where possible.

New provenance/economic/group data follows these rules.

------------------------------------------------------------------------

# 79. Performance guardrails

Do not let "Dwarf Fortress-lite" become "simulate every thought every
turn."

Use event-driven updates, coarse off-screen ticks, salience-based
promotion, bounded rumor propagation, provenance only for important
objects, archived-history compression, no distant full pathfinding, no
full cold-NPC inventory simulation, lazy summaries, and director
budgets.

------------------------------------------------------------------------

# 80. Compatibility guardrails

Prefer observation, adapters, narrow Harmony patches, vanilla APIs,
explicit transfers, stable IDs, graceful failure.

Avoid broad inventory deletion, replacing core managers, global
transpilers unless unavoidable, over-assuming vanilla faction maturity,
assuming every modded Chara follows vanilla lifecycle, scattering EA
field assumptions throughout code, and duplicating systems owned by
game/other mods.

------------------------------------------------------------------------

# 81. Multiplayer note

Elin Together demonstrates active multiplayer development but also
complex quest/map authority and synchronization.

Treat multiplayer as unsupported/unproven until explicitly implemented.

Future questions: host ownership of procedural state, client
projections, check authority, event deduplication, synchronized IDs,
simultaneous action ordering, save migration.

------------------------------------------------------------------------

# 82. Source/reference notes

These references informed post-master findings and should be revisited
because Elin and its mod ecosystem are changing.

## Brilliant Questing

-   `https://github.com/ImperfectRain/elin-brilliant-questing`
-   `docs/design/master-design.md`
-   `docs/architecture.md`
-   `docs/roadmap.md`

## Elin modding/game

-   `https://elin-modding.net/`
-   `https://elin-modding-resources.github.io/Elin.Docs/`
-   `https://code.elin-modding.net/`
-   `https://github.com/Elin-Modding-Resources/Elin-Decompiled`

## Workshop inspiration

-   RandomQuest & GuildQuest Expansion Pack ---
    `https://steamcommunity.com/sharedfiles/filedetails/?id=3579277196`
-   Quest Board Expansion ---
    `https://steamcommunity.com/sharedfiles/filedetails/?id=3661956141`
-   Omega's Quest Picker ---
    `https://steamcommunity.com/sharedfiles/filedetails/?id=3384541453`
-   KK Caravan --- verify current Workshop page/source before
    implementation decisions.
-   Elin with AI --- verify current Workshop page/source before
    implementation decisions.

## Public implementation references

-   Elin Together --- `https://github.com/ElinTogether/ElinTogether`
-   Yuof Elin Mods / AutoExplore --- `https://github.com/Yuof/Elin-Mods`
-   swarmdog ElinMods / SkyreaderGuild ---
    `https://github.com/swarmdog/ElinMods`

Workshop descriptions, community wikis, and mod source are
inspiration/precedent, not authoritative guarantees about current
runtime.

> **The locally installed current `Elin.dll` is authoritative for
> integration.**

------------------------------------------------------------------------

# 83. Consolidated new feature backlog

## High-priority conceptual additions

-   Requests / Situations / Opportunities / Events as distinct
    presentation classes.
-   Mechanical Coverage matrix.
-   Mass shipment / category-based supply requests.
-   Coarse local economic demand/shortage state.
-   Property-driven crafting commissions.
-   Guilds as information networks.
-   Situations that arrive at Home.
-   Socially recognized combat context.
-   Systemic/emergent reward philosophy.
-   Explicit inventory transfer safety.
-   Activities, competitions, festivals.
-   Non-heroic stakeholder generation.
-   Distressed-seller opportunities.
-   Rumor chains.
-   Emergent recurring NPC importance.
-   Dynamic bounties based on belief/legal state.
-   Important-item provenance.
-   Concrete social favors/obligations.
-   Profession-based solutions.
-   Small generated mysteries.
-   Observation of consequential vanilla accidents.
-   Off-screen NPC schemes.
-   Witness disagreement.
-   Generated social networks.
-   Debt as causal primitive.
-   Home sanctuary/refuge.
-   Apprentices/protégés.
-   Situation inheritance after death.
-   Adventurer ecology.
-   Other NPCs can resolve situations.
-   Simulated traveling groups.
-   Shared player/NPC Action Resolver.
-   Routine activities as information delivery.
-   "What's been happening?" dialogue surface.
-   Relationship-dependent disclosure.
-   Occupation-dependent interpretation.
-   Persistent location history.
-   Regional legends.
-   Trivial grudges.
-   Absurd causal escalation.
-   Bad solutions are valid world outcomes.
-   Simulation-first language constraint.
-   NPC participation in competitions.
-   Player production connected to social demand.
-   Organization/network information routing.
-   Long-term mod interoperability API.
-   Procedural-content preference configuration.
-   Multiplayer explicitly unproven.

## High-priority implementation work

-   Current Elin assembly integration.
-   Runtime `IVanillaState`.
-   Native Check verification.
-   Save attachment.
-   Stable Chara binding.
-   Witness/crime hook.
-   Drama/UI projection.
-   Persistent site proof.
-   Gate A in actual game.
-   First economic supply action.
-   First Home action.
-   First NPC-autonomous action.
-   First coarse traveling group.

------------------------------------------------------------------------

# 84. Updated north-star example

A tavern in Palmia begins running short on alcohol because its usual
supplier is struggling financially. The supplier borrows money from a
merchant and hires a caravan to move remaining stock. A small bandit
group, itself in debt, learns the route from a corrupt employee and
attacks the caravan.

The player may never see any of this directly.

A surviving guard reaches town and tells the Fighters Guild that bandits
attacked. The tavernkeeper tells the Merchants Guild that the shipment
failed. A stolen bottle and supplier's signet ring reach a Thieves Guild
fence. A traveling adventurer hears the guard's story and decides to
pursue the bandits without the player.

The player's first contact might be a mass order for alcohol, Fighters
Guild bounty, cheap stolen shipment, rumor at dinner, missing-person
request, investment opportunity, finding the adventurer captured at the
bandit site, or nothing until the tavern closes.

A farmer/brewer can solve shortage through production. A merchant can
source elsewhere. An investor can rescue supplier. A thief can buy/steal
cargo. An investigator can reconstruct route leak. A fighter can hunt
bandits. A social character can negotiate. A Home-focused player might
shelter a survivor.

If player does nothing, the other adventurer may succeed or fail. Tavern
may recover or close. Supplier may lose business. Bandits may become
richer. Corrupt employee may become recurring criminal. Signet ring may
resurface months later.

No authored `Part I -> Part II -> Part III` chain is required. Each
event changes state.

------------------------------------------------------------------------

# 85. Final implementation doctrine

When coding agents make design decisions, prefer the option that best
satisfies these rules:

1.  **State before story.**
2.  **Cause before quest.**
3.  **Vanilla mechanic before custom mechanic.**
4.  **Actor-local knowledge before omniscience.**
5.  **Actual world interaction before dialogue abstraction.**
6.  **Persistent consequence before disposable completion.**
7.  **Failure creates state.**
8.  **NPCs are actors, not quest dispensers.**
9.  **The player is important, not cosmologically privileged.**
10. **The world may solve, worsen, or transform its own problems.**
11. **Rewards should emerge from what happened whenever possible.**
12. **Elin's absurdity should emerge from causal interaction.**
13. **Simulation must remain explainable and testable.**
14. **Performance comes from selective simulation, not reducing
    causality.**
15. **Optional prose may describe truth; it may not manufacture
    authoritative truth.**
16. **A valid ugly solution is still a solution.**
17. **Not everything interesting is a quest.**
18. **If an Elin skill exists, seek situations where using it can
    matter.**
19. **Current local `Elin.dll` beats assumptions from stale
    documentation.**
20. **Do not expand breadth faster than runtime evidence.**

The project succeeds when players stop thinking, "the mod generated a
quest," and start thinking:

> **"This happened because of what happened earlier."**
