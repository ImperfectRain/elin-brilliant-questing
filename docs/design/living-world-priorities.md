# Elin Brilliant Questing --- Living World, Player Experience & Implementation Priorities

**Status:** Third design/implementation reference\
**Date:** 28 August 2026\
**Relationship:** Addendum to `docs/design/master-design.md` and
`docs/design/post-master-findings.md`.

## 0. Purpose

This document records the design and implementation conclusions that
became clear after the master design and first addendum, especially
after the Elin runtime adapter began working. It focuses on what remains
unimplemented, how to build those systems safely, and how to optimize
the finished mod for player enjoyment rather than raw simulation
complexity.

Read the references in this order:

1.  `master-design.md`: thesis, world model, vanilla mechanics, original
    architecture.
2.  `post-master-findings.md`: requests/situations/opportunities/events,
    mechanical coverage, economy, guild networks, Home, autonomy,
    traveling groups, provenance, favors and broader systemic design.
3.  **This document**: runtime-grounded priorities, player-facing UX,
    vanilla-world mutation, procedural location quality, fun-first
    design and the route from the current prototype to a living-world
    release.

Where documents disagree, reproducible behavior from the current local
Elin build wins. `docs/elin-api-notes.md` and in-game tests outrank
stale assumptions.

------------------------------------------------------------------------

## 0a. Notes on filing

*Added on commit; not part of the original document.*

Two things a reader should know before following this.

**Its §1 is ahead of the repository's own roadmap.** A working single-scenario in-game demo exists,
built outside the session that produced `docs/roadmap.md`. Where this document and the roadmap
disagree about what is proven, believe whichever is backed by a log.

**Its §5 is corrected by a later pass.** `vanilla-simulation-integration.md` establishes that Elin
runs off-screen and catch-up simulation of its own, which §5.3, §5.4 and §6.8 below were written
without. The amendments are marked in place; the successor document holds the detail.

**Its §14 roadmap and `character-dialogue-system.md`'s Phase A-J compete for the same next slot.**
Neither document references the other's sequencing. They are not in conflict on substance - the
expression layer needs situations to express, and the priorities here need something to present
them through - but somebody has to decide the interleaving before both are handed to an agent as
"the plan". The safest reading: §14's Priority 0 (finish Gate A) precedes everything in either
document, and the dialogue system's Phase D is where the two meet, since it exercises storylets on
the existing theft scenario.

------------------------------------------------------------------------

# 1. Current inflection point

Brilliant Questing is no longer primarily a proposed quest architecture.
The simulation core, deterministic identity, event ledger, fact/belief
separation, save migration and three-NPC laboratory exist. The Elin
plugin now loads in game, reads real player state, uses the game's save
lifecycle, and has reached live Drama integration work.

The new risk is **expanding breadth faster than the mod proves that its
systems are enjoyable, legible, safe to vanilla state and causally
coherent in a real save.**

From this point, optimize for:

-   complete vertical slices before feature counts;
-   player-visible consequences before invisible sophistication;
-   vanilla mechanics before bespoke abstractions;
-   safe mutation before dramatic mutation;
-   curated procedural grammars before unconstrained generation;
-   continuity before raw novelty;
-   simulation that creates choices rather than simulation for its own
    sake.

North-star framing:

> **Elin should increasingly behave as though its people, businesses,
> places, possessions, guilds, homes, rumors and local problems continue
> to exist for reasons even when the player is not looking.**

------------------------------------------------------------------------

# 2. Runtime rules for future development

## 2.1 Evidence levels

"Found" is not "works." Every Elin-facing feature should move through:

``` text
DISCOVERED
→ COMPILES
→ OBSERVED IN GAME
→ SEMANTICALLY VERIFIED AGAINST VANILLA STATE/UI
→ PERSISTENCE VERIFIED
→ STRESS VERIFIED
```

Recent integration work already showed why this matters: plausible
Element aliases were wrong, `expInfluence` was not spendable town
Influence, and exposed Drama/event surfaces did not always behave as
naming suggested.

## 2.2 Prefer native events and narrow adapters

Use Elin lifecycle/action events before Harmony patches or polling.
Patch only the smallest missing seam.

``` text
native event observation
→ adapter
→ structured BQ event
```

This should be the preferred path for save/load, character creation,
observed player actions, crime and future event capture.

## 2.3 Source architecture may differ from shipping architecture

Keep the source separation:

``` text
BrilliantQuesting.Core
BrilliantQuesting.Plugin
BrilliantQuesting.Lab
```

but allow the shipped plugin to compile core sources into one assembly
if Elin's package chainloader requires it. Packaging is a deployment
constraint, not a reason to contaminate the simulation core with Elin
dependencies.

Use collision-resistant core names because Elin exposes many generic
global types.

## 2.4 Version drift is a first-class constraint

Elin is active Early Access. Fragile game access belongs behind
capabilities/adapters. A renamed field should disable one feature with a
diagnostic, not break the whole save.

------------------------------------------------------------------------

# 3. Player-facing doctrine

## 3.1 The simulation knows more than the player

> **The player sees only what their character could reasonably perceive,
> learn, remember, infer or be told.**

Do not normally expose hidden goals, utility scores, thread tension,
rumor confidence numbers, simulation tiers, future escalation schedules,
objective guilt, hidden graph edges or internal IDs.

## 3.2 Prefer world changes to messages

> **Whenever the world itself can communicate a consequence, change the
> world rather than displaying a message about the change.**

Prefer:

-   missing merchant over "merchant unavailable";
-   nervous witness over a suspicion meter;
-   stolen ring over Evidence +1;
-   closed service over an economy-state popup;
-   refugee physically arriving at Home over "refugee event triggered";
-   altered stock and dialogue over a raw shortage number.

## 3.3 UI surfaces

### Request board

Only for actual requests where an actor deliberately asks for an
outcome.

### Journal / future Chronicle

A memory aid for player-known unresolved state. Distinguish:

-   **Known**
-   **Reported**
-   **Suspected**
-   **Disputed**
-   **Rumor**

Never reveal hidden truth merely because the simulation contains it.

### Quest tracker

Keep minimal: a thread name and one current player-understood lead or
public progress value.

### Drama/dialogue

Primary surface for social information, leverage, accusations, favors,
confessions and contextual actions. Prefer natural language over
`[CHA 30]` or exact success percentages. Use Elin's own difficulty
wording where practical.

### Message log

Immediate feedback only, not exposition or simulation telemetry.

### Ambient world

Barks, absent actors, visitors, inventory, corpses, objects, shop state,
Home residents and guild contacts should carry as much narrative
information as possible.

## 3.4 Information access is progression

Different builds should interpret the same evidence differently:

-   farmer: crop stress;
-   alchemist: contamination;
-   investigator: deliberate contamination;
-   merchant: who profits;
-   Fighters Guild: violent threat;
-   Thieves Guild: stolen cargo;
-   high-affinity friend: sensitive personal truth.

This is stronger than adding more stat-tagged dialogue.

------------------------------------------------------------------------

# 4. Keep the four content classes distinct

**Request:** someone asks the player to do something.\
**Situation:** a persistent conflict/problem exists.\
**Opportunity:** a temporary state can be exploited/helped/traded
around.\
**Event:** something happened.

Example chain:

``` text
caravan robbery = Event
shortage = Situation
premium buying price = Opportunity
tavernkeeper asks for 100 meals = Request
```

Do not collapse all four into `Quest`.

------------------------------------------------------------------------

# 5. Safe vanilla-world mutation

A living world needs real consequences, but indiscriminate mutation
risks vanilla quests, services, respawns and save integrity.

> **Amended.** Elin has real timetables, work/hobby/needs AI, revisit catch-up simulation and
> hourly off-screen `GlobalGoal` advancement for eligible global actors. So the mod is not the only
> thing that moves a character or advances a day, and "mutation" is not the only way vanilla state
> changes underneath it. See `vanilla-simulation-integration.md` §2 and decision `D021`.

Use an explicit policy concept:

``` csharp
enum NarrativeMutationPolicy
{
    ObserveOnly,
    DialogueOnly,
    SocialMutable,
    InventoryMutable,
    Relocatable,
    TemporarilyRemovable,
    FullyMutable
}
```

Exact code can differ; the classification should not.

## 5.1 Suggested actor policy

**Story-critical vanilla NPC:** observe/dialogue/social only by default.
Never procedurally kill or permanently relocate.

**Unique merchant/service NPC:** social changes are reasonable;
service/inventory/temporary absence require dedicated lifecycle testing.

**Ordinary vanilla citizen/guard/generic merchant:** candidate for
inventory mutation, relocation and temporary removal after verification.

**Brilliant Questing generated actor:** fully mutable by default. These
are safest for death, relocation, inheritance, role changes and long
causal histories.

## 5.2 Missing shopkeeper grades

**Grade A: service/social unavailability.** Actor remains but
shop/service changes. Safest.

**Grade B: physical temporary absence.** Actor is removed/suppressed
locally and represented elsewhere in procedural state. Must prove no
duplication across citizen refresh, zone unload/reload and save/load.

**Grade C: unique/story NPC removal.** Protected unless individually
proven safe.

## 5.3 Relocation should be abstract off-screen

``` text
present in town
→ Traveling
→ absent locally
→ off-screen route resolution
→ Arrived
→ materialize/bind at destination
```

Do not pathfind every traveler across the world.

> **Amended.** Some actors already travel without the mod: Elin can advance an eligible global
> actor's `GlobalGoal` hourly and move it to another town on its own. Where that is happening, the
> mod observes and interprets rather than issuing a competing move, and reconciles against the
> game's own answer for where somebody is. Vanilla moving a debtor into town is content, not a
> contradiction. See `vanilla-simulation-integration.md` §2.4 and §3.3.

## 5.4 Simulate selectively

Eventually:

``` text
ACTIVE   full relevant simulation
WARM     goals + occasional social/economic/travel activity
COLD     identity + coarse state + sparse developments
ARCHIVED history retained; no routine simulation
```

The player needs causal consistency, not literal full-time simulation of
every citizen.

> **Amended.** These are the mod's narrative fidelity tiers, not the game's. ACTIVE means the
> *least* duplicated physical simulation, because Elin is live and owns embodiment. And nothing
> here may double-advance what `Zone.Simulate()` catches up when the player revisits a zone, or
> what vanilla continues advancing for a global actor regardless of its BQ tier. See
> `vanilla-simulation-integration.md` §2.3, §2.5 and §5.

------------------------------------------------------------------------

# 6. Living-world systems worth prioritizing

## 6.1 Recurring NPC continuity

Reuse plausible existing actors before generating replacements.
Continuity is the strongest source of emergent attachment.

A merchant can become important because the player saved a sibling,
later supplies a shortage, later loses a caravan, later vouches for the
player. None of that requires the actor to have been authored as
important.

## 6.2 Social obligations

Formalize debts, favors, promises, sponsorships, sanctuary and grudges.
They create future options without becoming a second affinity meter.

## 6.3 Shop/service continuity

Businesses can occupy lightweight states:

``` text
Normal
Struggling
ShortOnStock
OwnerAbsent
TemporarilyClosed
ReplacementOperator
Recovered
Failed
Inherited
```

Project these through actual NPC presence, stock, dialogue, requests and
opportunities rather than a separate business-management UI.

> **Amended.** Read the operator's actual state before assigning one of these. A shopkeeper asleep
> or off-shift on a vanilla timetable is ordinary temporary unavailability; these states are for
> continuity problems. See `vanilla-simulation-integration.md` §5.1.

## 6.4 Home as consequence surface

Home should eventually support sheltering fugitives/refugees/witnesses,
recruiting displaced specialists, storing evidence or contraband,
hosting meetings, emergency supply, visitors, creditors and enemy
consequences.

## 6.5 Guilds as information and authority networks

Guilds should expose different views of the same world, not merely
separate random quest pools.

## 6.6 Coarse local economy

Track only useful narrative pressures such as Food, Alcohol, Medicine,
Lumber, Textiles, Weapons, Luxury, Labor and Safety. Use them to drive
stock, demand, contracts, caravans and visible shortages. Do not
simulate every loaf consumed off-screen.

## 6.7 Traveling groups

High-value connector system:

``` csharp
TravelGroup {
    Id;
    Kind;
    Members;
    Origin;
    Destination;
    DepartureTick;
    ExpectedArrivalTick;
    RouteRisk;
    Cargo;
    Goal;
    State;
}
```

Kinds: merchant caravans, adventurers, pilgrims, refugees, hunters,
bandits, expeditions, guild patrols.

## 6.8 NPC autonomy

This is the largest conceptual transition after runtime integration.

Problems do not belong to the player. NPCs should eventually use the
same conceptual Action Resolver where practical and can solve, worsen or
transform situations without player involvement.

> **Amended.** The resolver chooses intention; embodiment is delegated to a verified vanilla path
> where one exists, and resolved coarsely otherwise — the mod does not become a movement
> controller. Off-screen, a shared workplace or an overlapping timetable is *opportunity* and
> nothing more: it never yields eyewitness proof, an exact location or recognition of a person.
> See `vanilla-simulation-integration.md` §3 and §5.4.

------------------------------------------------------------------------

# 7. Procedural locations without a garbage generator

Do **not** build a general-purpose random dungeon generator.

The preferred model is:

> **Elin supplies spatial substrate. Curated grammars supply spatial
> meaning. Scenario state and history supply identity.**

Current Elin zone tooling supports procedural profiles, custom Zone
types, first-generation hooks, random sites and hybrid
handcrafted/procedural floors. Use those strengths instead of replacing
them.

## 7.1 Three layers

``` text
SPATIAL SUBSTRATE
Elin procgen or curated shell

+ SCENARIO GRAMMAR
what the situation requires spatially

+ HISTORICAL DRESSING
what happened before the player arrived
```

Example:

``` text
Mine
+ bandit prison
+ abandoned last year / bandits arrived 18 days ago /
  caravan attacked 3 days ago / another adventurer attacked yesterday
```

## 7.2 Reuse locations first

Before generating a new site:

1.  Can an existing vanilla location host it?
2.  Can an existing procedural site be recontextualized?
3.  Can an older BQ site be reused?
4.  Only then generate a new site.

This reduces clutter and increases continuity.

## 7.3 Curated location grammars

Candidate library:

-   bandit camp;
-   collapsed mine;
-   occupied farmhouse;
-   smuggler cellar;
-   ruined caravan stop;
-   cult hideout;
-   warehouse;
-   abandoned workshop;
-   forest encampment;
-   sewer refuge;
-   cursed manor;
-   makeshift prison;
-   research site.

Each grammar specifies requirements rather than exact geometry.

Example:

``` text
Bandit camp
Required: defensible approach, communal/sleeping area, storage, leader space
Optional: prisoner area, stolen-goods cache, alternate exit, lookout
```

## 7.4 Spatial affordances should support actual builds

Reusable affordances:

``` text
LockedBarrier
BreakableBarrier
DiggableBypass
HiddenPassage
GuardedThreshold
EvidenceCache
PrisonCell
ObservationPoint
Hazard
TrapCluster
SocialCheckpoint
AlternateExit
```

A site can expose:

``` text
front gate → combat/intimidation/negotiation
side door → lockpicking
weak wall → mining
rear route → stealth/perception
guard key → pickpocket
ledger → literacy/evidence
prisoner → rescue/social
```

## 7.5 Scenario state decorates the location

Wealth, hunger, recent attack, prisoners, injuries, escape preparation
and cargo should materially change what is placed.

## 7.6 Generate, validate, score

When practical, generate several candidates and choose the best.

Score generic qualities such as route diversity, objective distance,
room utility, alternate approaches, evidence distribution, reachability
and excessive dead ends. Add scenario-specific scoring: a burglary
values boundaries and stealth routes; a hostage site values objective
separation; an investigation values distributed evidence.

## 7.7 Persistent sites beat disposable sites

Significant sites should usually remain as history:

``` text
Active → Abandoned → Occupied → Ruined → Repurposed → Forgotten → Rediscovered
```

Reusing an old mine three years later is more memorable than spawning
another technically better random dungeon.

## 7.8 Causal enemies and loot

Enemies should reflect actual group state. Loot should be stolen cargo,
actor possessions, organization reserves, evidence and supplies where
possible. Do not refill a cleared group because a dungeon template
expects enemies, or add treasure solely because "dungeon needs chest."

## 7.9 First location proof

One small site only:

-   native zone creation;
-   one thread binding;
-   3--5 actors;
-   real cargo/evidence;
-   two meaningful approaches;
-   unload;
-   save;
-   reload;
-   return;
-   verify exact persistence.

Generalize only after this works.

------------------------------------------------------------------------

# 8. Action-library expansion by mechanical leverage

Do not add verbs just to hit a target count. Add verbs that unlock Elin
playstyles.

**Economic:** supply, invest, finance, hire, commission, pay debt,
arrange credit, fence, buy/sell information, negotiate contract.

**Home/Community:** shelter, host, recruit specialist, provide
beds/food, assign protection, relocate resident, store
evidence/contraband, sponsor displaced actor.

**Investigation:** inspect, compare testimony, examine corpse, identify
substance, search records, follow, eavesdrop, track, authenticate
document.

**Crafting/Production:** fulfill category demand, quality/property
commission, repair, provision/cook, alchemical treatment, build
requested object.

**Faith/Magic:** prefer actual spells, deity state, piety, offerings and
abilities.

**Physical/World:** clear obstruction, carry/rescue, mine bypass, break
barrier, disarm, transport.

Maintain a coverage matrix:

``` text
Elin mechanic
→ reusable actions
→ situation families
→ actual-world interaction or dialogue abstraction
```

A skill is not covered merely because a dialogue option mentions it.

------------------------------------------------------------------------

# 9. Situation ecology to build next

Build archetypes that stress different systems and can later interact.

1.  **Shortage / supplier failure** --- economy, mass shipment,
    crafting, investment, caravans.
2.  **Fugitive / sanctuary** --- Home, Karma, witnesses, trust,
    consequences arriving at player.
3.  **Missing person / failed caravan** --- travel groups, sites,
    evidence, rescue, relocation.
4.  **False accusation** --- truth vs belief, testimony, rumor, framing.
5.  **Debt / distressed business** --- money, investment, contracts,
    coercion, shop continuity.
6.  **Festival / competition** --- public event, ordinary skills, NPC
    participation, non-crisis play.
7.  **Bounty / recognized violence** --- combat context, authority,
    capture/kill, guilds, witnesses.

These seven test more of the architecture than many variations of theft.

------------------------------------------------------------------------

# 10. Fun-first procedural rules

## 10.1 Conflicting good reasons

Avoid obvious good-client/evil-villain structures. Give stakeholders
understandable interests that collide.

## 10.2 Ugly solutions work

Bribery, theft, blackmail, murder, framing, profiteering, abandonment,
sheltering criminals and selling evidence should be allowed when
mechanically valid. They solve immediate problems while creating later
state.

## 10.3 Failure produces new toys

Good failure changes access, suspicion, witnesses, legal state,
relationships or opportunities. Bad failure simply removes an option.

## 10.4 Preserve tonal range

Not every thread should be kidnapping, murder or conspiracy. Include
competitions, petty grudges, livestock, odd commissions, food shortages,
business mistakes, embarrassing rumors, travel mishaps, religious
weirdness and Elin-style accidents.

The mundane makes major events feel major.

## 10.5 Player is not cosmologically central

Situations begin without the player, change while ignored, can resolve
without them, and can be discovered only after resolution.

## 10.6 Reuse history aggressively

Before generating new content, look for an old NPC, rival, debtor, guild
contact, shop, site, object, favor, rumor or organization that plausibly
fits.

## 10.7 Protect ordinary Elin play

The player must be able to farm, decorate, explore, craft or fish
without constant procedural interruption. Use passive rumors, optional
requests, limited active threads, delayed escalation, salience
thresholds and configurable narrative activity.

------------------------------------------------------------------------

# 11. Narrative Director: build late

The Director should manage attention, not manufacture quests.

``` text
many developments
→ which deserve simulation budget?
→ which deserve player exposure?
→ through what believable channel?
→ how many simultaneous threads are tolerable?
```

Reward callbacks, nearby consequences, known NPCs, underused mechanics,
unresolved history and tonal contrast.

Penalize repeated archetypes, repeated roles, repeated locations, too
many urgent threads and too much violence.

------------------------------------------------------------------------

# 12. Debugging must scale with complexity

Normal UI hides internals. Debug UI must expose them.

It should answer:

-   Why does this situation exist?
-   Which event caused it?
-   Why is this NPC involved?
-   What do they know or falsely believe?
-   Why is an action available/unavailable?
-   What check runs?
-   Why did an NPC choose an action?
-   Who witnessed it?
-   What consequences were emitted?
-   Why did a rumor propagate?
-   Why did a shop close or NPC disappear?
-   Why was a site selected/generated?

For sites, also expose grammar, candidate scores, rejection reasons,
placed affordances, important-object bindings and history.

------------------------------------------------------------------------

# 13. Persistence/lifecycle checklist for every new system

Every persistent subsystem must answer:

1.  What is authoritative in Elin?
2.  What is authoritative in BQ?
3.  What stable ID links them?
4.  What happens on unload?
5.  What happens on destruction?
6.  What happens if Elin recreates the object?
7.  What happens on save/load?
8.  What happens on migration?
9.  What happens if the capability disappears after an update?
10. Can old events accidentally be applied twice?

Never serialize transient runtime references as identity. Never
redispatch history on load. Quarantine malformed threads instead of
poisoning the save. Define deadline/tick semantics explicitly and prefer
idempotent central simulation ticks over fragile per-hour patches.

------------------------------------------------------------------------

# 14. Priority roadmap from the current state

## Priority 0 --- Finish Gate A

One real procedural dialogue choice must resolve against the correct
actor/thread, apply consequences once, survive full quit/reload and
continue correctly.

## Priority 1 --- Observe one real vanilla action

Ideal proof: controlled theft/crime.

``` text
vanilla action
→ BQ observes authoritative result
→ legitimate witnesses learn
→ no omniscient leak
→ later dialogue can reference it
```

This proves Elin itself can create narrative facts.

## Priority 2 --- Map Home read-only

Read residents and relevant Home/public-skill state before writing Home
situations.

## Priority 3 --- NPC lifecycle experiment

On a disposable save: bind one nonessential NPC, remove/relocate,
unload/reload zone, save/reload game, restore/return, and explicitly
test duplication/citizen refresh.

## Priority 4 --- Generated-site proof

One small persistent site through native infrastructure. No generalized
dungeon generator yet.

## Priority 5 --- Expand actions by mechanical leverage

Economic, Home/Community, Investigation, Crafting/Production,
Faith/Magic and physical interaction first.

## Priority 6 --- Add shortage and sanctuary archetypes

These force the architecture beyond theft.

## Priority 7 --- First autonomous NPC intervention

One NPC pursues one situation action off-screen using compatible
event/consequence structures.

## Priority 8 --- Traveling-group prototype

One caravan with origin, destination, cargo, members, risk and arrival.

## Priority 9 --- Reuse old history

A later thread deliberately reuses a previously resolved actor or
location.

## Priority 10 --- Narrative Director

Only when multiple threads create a genuine attention-management
problem.

------------------------------------------------------------------------

# 15. Features to retain for future implementation

Do not lose these ideas, but do not let them distract from the
priorities above:

-   procedural festivals and competitions;
-   relationship-dependent information disclosure;
-   routine activities as rumor delivery;
-   adventuring parties;
-   caravans, pilgrims, refugees, hunters and expeditions;
-   local legends;
-   item and location provenance;
-   social debts and favors;
-   sanctuary;
-   apprentices/protégés;
-   inheritance of obligations/situations;
-   recurring rivals;
-   bounty ecosystems;
-   profession-specific evidence interpretation;
-   false testimony and witness disagreement;
-   coverups and alibis;
-   blackmail;
-   forged evidence;
-   dynamic business succession;
-   replacement shopkeepers;
-   displaced craftspeople becoming Home residents;
-   town shortages and mass shipments;
-   property/quality-driven crafting commissions;
-   guild-mediated information;
-   public competitions with NPC participation;
-   rumors during meals, travel, shopping, worship, fishing or work;
-   organizations pursuing their own interests;
-   lightweight interoperability API for other mods;
-   optional richer prose that only describes authoritative state.

------------------------------------------------------------------------

# 16. Anti-patterns

Reject:

-   thousands of bespoke quest templates;
-   duplicate reputation/morality/social stats;
-   omniscient journal entries;
-   every event becoming a quest;
-   every skill becoming a dialogue tag;
-   every problem waiting forever for the player;
-   disposable generated sites;
-   random geometry + random enemies + random objective;
-   fake evidence counters when real objects can exist;
-   scripted correct morality;
-   failure deleting content;
-   simulation that never changes visible gameplay;
-   unsafe mutation of essential vanilla NPCs;
-   full-fidelity simulation of distant actors;
-   LLM-generated authoritative facts/actions;
-   normal UI exposing simulation internals;
-   feature expansion while core runtime/persistence gates remain
    unproven.

------------------------------------------------------------------------

# 17. Updated doctrine

1.  State before story.
2.  Cause before quest.
3.  Vanilla mechanic before custom mechanic.
4.  World change before explanatory popup.
5.  Player knowledge before omniscient journal truth.
6.  Actual object before abstract evidence point.
7.  Existing actor before unnecessary new actor.
8.  Existing location before unnecessary new location.
9.  Curated grammar before unconstrained procgen.
10. Persistent site before disposable dungeon.
11. Observed Elin behavior before API assumption.
12. Safe mutation before dramatic mutation.
13. One complete vertical slice before broad content.
14. Failure creates playable state.
15. NPCs are actors, not dispensers.
16. Player is important, not cosmologically privileged.
17. World may solve its own problems.
18. Mechanical coverage means actual gameplay use.
19. Simulation depth follows relevance.
20. Continuity beats raw novelty.
21. Mundane events preserve tonal range.
22. Ugly valid solutions remain valid.
23. Debugging explains important causal decisions.
24. One broken thread must never poison a save.
25. Do not expand breadth faster than runtime evidence and playtesting
    justify.

------------------------------------------------------------------------

# 18. North-star example

A player returns to town only to buy supplies. A familiar brewer is
absent. The tavern's alcohol stock is poor and a replacement worker says
the brewer's caravan never arrived. A Merchant Guild contact knows the
route had become unprofitable. A Fighters Guild member heard that an
adventuring party went looking. The player ignores it.

Days later the adventurers return without one member. A fence elsewhere
is selling bottles with the brewer's mark. The cargo points toward an
old mine the player cleared a year earlier. The mine is now occupied by
a different group because its abandonment made it useful shelter.

Inside, the physical scene reflects what happened: broken cargo, an
injured captor, a dead guard, a locked room, a weak wall that can be
mined through, and a ledger showing that the brewer was deeply in debt.
The player can rescue him, kill everyone, bargain, steal the cargo,
expose the debt, sell the evidence or leave.

If rescued, he eventually returns and the shop reopens. If he dies,
another actor may inherit or replace the business. Sheltering a
surviving criminal at Home can later bring guards or creditors. If
nobody intervenes, another merchant may resolve the shortage at a higher
price and the town remembers a different history.

The important part is not prose. The important part is that the same
actors, cargo, debt, guild knowledge, old location, player mechanics and
later consequences remain causally connected.

------------------------------------------------------------------------

# 19. Immediate definition of success

For the next development cycle, success is not the number of new
systems.

> **A player can encounter one procedural situation inside real Elin,
> understand it through native-feeling presentation, make a meaningful
> choice using their actual build, see the vanilla world change, leave,
> save, reload, return later, and find that the world correctly
> remembers what happened.**

Once that loop is reliable and fun, scale it outward.
