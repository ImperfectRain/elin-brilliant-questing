<!-- Generated mirror of Elin_Procedural_Questing_Master_Design_Document.docx.
     The .docx in this folder is the source of truth; regenerate this file rather than
     editing it directly. -->

ELIN PROCEDURAL QUESTING SYSTEM
Master Game Design, Systems Architecture, Research & Implementation Blueprint
A persistent, simulation-driven quest layer for Elin built on vanilla RPG mechanics
Research snapshot: 27 August 2026Target documentation snapshot: Elin EA 23.338.2 Stable / contemporary community documentation

## Executive Summary

This document defines the ideal end-state and implementation path for a procedural questing mod for Elin. The mod is not conceived as a text generator, a collection of radiant quest templates, or a parallel roleplaying ruleset. It is a persistent world-simulation layer that creates actors, situations, knowledge, relationships, conflicts, sites and consequences, then exposes them to the player through Elin's existing mechanical grammar: attributes, skills, Checks, affinity, Karma, prestige/fame, town Influence, guild progression, religion, inventory, crime detection, combat, crafting, magic, home development and procedural zones.
Core product thesisGenerate persistent situations, not disposable quests. Let vanilla Elin mechanics determine what the player can attempt, how difficult it is, who notices, what it costs, and what changes afterward. The quest journal is a projection of unresolved world state - not the source of truth.

The desired player experience is closer to a lightweight Dwarf Fortress-style causal world simulation translated into Elin's playful systemic RPG structure. The system should routinely produce stories such as: a random shopkeeper becomes important because the player saved her sibling; a burglary creates a witness; that witness informs a guild; the guild retaliates; a town loses a service; a resident is recruited to the player's land; months later that resident becomes a new source of work. The important property is not procedural prose. It is procedural continuity.

## Contents

- 1. Product Vision and Non-Negotiable Design Principles
- 2. Research Snapshot: Vanilla Elin Mechanics
- 3. Player-Facing Gameplay Loop
- 4. World Model and Persistent Memory
- 5. Facts, Knowledge, Evidence, Rumors and Witnesses
- 6. NPC Goals, Personality and Relationships
- 7. Organizations, Factions and Social Networks
- 8. Situations, Narrative Threads and the Narrative Director
- 9. Universal Action / RPG Verb Framework
- 10. Mechanical Checks and Branch Resolution
- 11. Branching Quest Graphs and Failure-Forward Design
- 12. Integration of Attributes, Skills and Builds
- 13. Karma, Fame, Affinity, Influence, Guilds, Religion and Home
- 14. Inventory, Economy, Crafting, Magic, Combat and World Interaction
- 15. Procedural Locations and Map Handling
- 16. Dialogue, Drama and Presentation
- 17. Rewards and Consequence Propagation
- 18. Time, Escalation and Off-Screen Simulation
- 19. Save Data, Persistence, Compatibility and Performance
- 20. Technical Architecture and Elin Integration
- 21. Content Authoring Model and Data Schemas
- 22. Development Roadmap and Prototype Gates
- 23. Testing, Telemetry and Quality Standards
- 24. Example End-to-End Procedural Questlines
- 25. Risks, Anti-Patterns and Scope Control
- 26. Ideal End-State Feature Set
- 27. Research Sources and References

## 1. Product Vision and Non-Negotiable Design Principles

Principle
Operational meaning
Vanilla-first mechanics
The mod should read and manipulate Elin's real stats and systems whenever possible. Avoid duplicate persuasion, reputation, morality, housing or guild systems.
Situation-first generation
Generate a causal problem state first. Quests, rumors, requests and dialogue emerge from that state.
Persistent entities
Generated NPCs, sites, organizations, facts and important objects receive stable IDs and can recur indefinitely.
Player agency through systems
Most branches should be achievable by interacting with normal Elin mechanics, not by selecting bespoke story buttons.
Failure creates new state
Failed checks, missed deadlines and ignored situations should advance or mutate the simulation rather than simply stop content.
Build diversity
Combat, social, stealth, economic, crafting, religious, magical, investigative and home-management builds should all have legitimate routes.
World knowledge is local
NPCs know what they witnessed, were told, inferred, or can prove. The world itself may know a fact, but characters should not become omniscient.
Humor through causality
Elin's weirdness should emerge from mechanical interactions and critical failures, not from constant hand-written joke text.
Selective depth
Simulate many entities cheaply but deepen the few the player repeatedly touches.
Explainability
A debug mode must be able to answer: Why did this quest exist? Why was this option available? Why did this NPC react this way?

### Success criteria

- A player can approach the same generated problem through materially different builds and actual game actions.
- A random NPC encountered early in a save can become a recurring character dozens of hours later without author-scripted identity.
- Ignoring a situation can produce visible consequences, including altered NPC availability, relationships, shop state, safety, crime or follow-up events.
- The player can discover, exploit, conceal, falsify, trade or reveal information rather than merely unlock exposition.
- Generated content feels native to Elin because it uses the same currencies, risks, skills, rewards and absurd failure texture as the base game.
- The system remains useful without any external LLM or online service.

## 2. Research Snapshot: Vanilla Elin Mechanics

Research statusElin is in active Early Access. Mechanics and APIs can change. This document records a 27 Aug 2026 research snapshot and explicitly marks proposed behavior separately from observed vanilla behavior. Version-sensitive integrations should be wrapped behind adapters.

### 2.1 Modding surface and data-driven systems

Current community documentation describes script mods as C# class libraries loaded through BepInEx, with Elin.dll inspected through a .NET decompiler for runtime integration. Elin also exposes Source Sheets for Chara, Thing, Element, Stat, Check, Faction, Religion, Zone, Quest, HomeResource, Person and other game data. This is a strong fit for a hybrid design: stable declarative content in Source Sheets, simulation logic and runtime integration in C#, and Drama as a presentation layer. [R1][R2][R3]
Relevant Elin surface
Use in this mod
Element
Reference attributes, skills, feats, spells and abilities; define only minimal mod-specific elements if unavoidable.
Check
Drive native-feeling contested checks and difficulty text.
Quest
Integrate journal/request-facing content where useful, rather than making Quest rows authoritative world state.
Zone / Area
Define procedural site profiles and reusable map archetypes.
Chara / Person
Define reusable procedural archetypes, actors and presentation bridges.
Faction / Religion
Read vanilla affiliations and religious context; avoid overriding hardcoded behavior.
HomeResource
Query or integrate home-state consequences.
Drama
Present choices, conditional dialogue and checked interactions; runtime C# should still own state.

### 2.2 Native Check system

The decompiled Check class is especially important. It exposes CriticalPass, Pass, Fail and CriticalFail results. Check.Get(id, dcMod) constructs a check; GetDC can incorporate a target's level and target element; GetFinalDC subtracts the acting character's relevant element and an optional parent/sub-factor; Perform rolls 1d20, with natural 20 and natural 1 producing critical outcomes. DramaActor.SetChoice recognizes check-based choices and can obtain a Check by ID. [R4][R5]

```
Vanilla-style resolution model (observed conceptually):base check DC+ target level contribution+ optional target element contribution- player relevant element/skill- optional related parent element factor= final DCroll 1d2020 -> CriticalPass1  -> CriticalFailroll >= final DC -> Passotherwise -> Fail
```

Design implication: use the vanilla Check framework as the default resolver wherever it maps cleanly. Hard gates should be reserved for impossibility or explicit vanilla restrictions (for example, a lock genuinely beyond the player's Lockpicking range), not as the default form of dialogue design.

### 2.3 Attributes and skill progression

Elin uses eight primary attributes: Strength, Endurance, Dexterity, Perception, Learning, Will, Magic and Charisma. Skills are tied to related attributes, and using skills contributes attribute experience. Class and race influence starting attributes, skills and potential, while trainers teach additional skills. [R6][R7]
Attribute
Observed vanilla role
Procedural quest role to expose
Strength
Carry weight, HP, throwing; related physical weapon families.
Force, haul, demolish, physically intimidate, rescue/carry, mining-assisted routes.
Endurance
Carry weight, HP, stamina.
Long pursuits, environmental endurance, sustained labor, attrition protection.
Dexterity
Accuracy/defense contributions; thief-oriented skills often pair naturally.
Lockpicking, pickpocketing, trap work, sleight-of-hand, planting/sabotage.
Perception
Rod power and evasion contributions; detection-oriented play.
Search, track, observe, detect lies/clues, surveillance, stealth contests.
Learning
Mana and status/debuff success; supports learned skills.
Literacy, research, appraisal, anatomy, evidence analysis, technical plans.
Will
HP/MP contributions, debuff resistance, cane accuracy.
Resist coercion, faith-driven options, mental endurance, high-pressure interrogation.
Magic
Core magical aptitude.
Prefer actual spells/abilities as solutions; use raw Magic only when no direct magical verb exists.
Charisma
Socially relevant attribute and recruitment capacity.
Negotiation, deception, leadership, public appeals, recruiting, social authority.

### 2.4 Negotiation, Affinity and recruitment

Negotiation affects shop prices and how easily NPCs gain or lose affinity through social interaction. Higher Charisma and Negotiation improve outcomes from ordinary conversation. Affinity is a persistent NPC-specific closeness parameter that unlocks benefits such as recruitment and relationship features. [R8][R9]
Design implication: the mod should not add a second NPC disposition meter. Vanilla affinity is the player-facing relationship value. Procedural memory stores *why* that affinity changed and determines what kinds of actions an NPC is psychologically willing to attempt.

### 2.5 Pickpocket, Stealth, Spot Hidden and Lockpicking

Pickpocket already combines the player's Pickpocket skill and Dexterity with the target's Perception and visibility, while Strength helps determine the maximum item weight that can be stolen. Stealth influences NPC detection and whether civilians notice crimes, with proximity and target Perception affecting detection. Spot Hidden finds traps in line of sight; discovered traps can then be avoided or disarmed, while alternative routes such as mining, levitation and teleportation can bypass them. Lockpicking has an actual capability ceiling: sufficiently difficult locks are not attempted until the player reaches an adequate range, with lockpicks improving capability and success. [R10][R11][R12][R13]
Design implication: procedural infiltration should reuse these concrete world mechanics. A locked archive should not merely present a dialogue option labeled [Lockpicking]. It should ideally contain an actual lock, actual witnesses, alternate wall routes, possible magical bypasses, and actual evidence to recover.

### 2.6 Karma, prestige/fame, requests and Influence

Karma measures lawfulness. Negative Karma makes the player a criminal, causes guards to attack, restricts ordinary commerce and makes Derphy an important haven; Incognito can temporarily restore normal interaction. High or low extremes can produce Good/Evil feats. [R14]
Vanilla requests reward Karma, prestige, local Influence, money/tokens and client affinity. Higher prestige increases the chance of higher-difficulty requests and gates some request types. Local Influence can be spent to reroll requests. Dedicated request maps are already procedurally generated and ephemeral. [R15]
Design implication: Karma is not a morality alignment. Treat it primarily as legal status and public lawfulness. Fame/prestige should change the scale and visibility of situations. Influence is already a credible local political resource and should be considered for civic favors and access.

### 2.7 Guilds

Elin currently supports Fighters, Mages, Thieves and Merchants guilds. Guild rank advances through contribution and provides benefits. Their contribution loops are mechanically distinct: elite monster kills for Fighters, ancient book deciphering for Mages, criminal/stolen-goods-oriented progression for Thieves, and direct shop investment for Merchants. [R16]
Design implication: guild membership and rank should alter access, authority, contacts, quest generation weights and available procedural actions. It should not be a cosmetic dialogue tag.

### 2.8 Faith and religion

Worship provides deity-specific attribute/skill bonuses that scale with worship duration and piety. Offerings increase piety, and faith can grant deity-specific abilities. [R17][R18]
Design implication: religious solutions should preferably invoke actual deity, piety, Faith, altar, offering or deity-granted ability state rather than generic “religion checks.”

### 2.9 Home, residents, jobs and home skills

My Home is a substantial economic and community system. Residents can be recruited, assigned jobs/hobbies and contribute production or Home Skills. Home Skills include Administration, Food Supply, Local Patriotism, Studious, Soil, Public Safety, Public Morality, Emergency Ration, Natural Power Generation, Publicity, Luck and more. Resident jobs/hobbies can increase resources such as Public Safety, Publicity, Public Morality, Administration and Soil. Housing also tracks population capacity, civility, fertility, electricity and other settlement-like parameters. [R19][R20][R21]
Design implication: the player's land can be a quest solution surface: shelter refugees, hide fugitives, recruit specialists, improve safety, produce supplies, absorb a displaced merchant, host witnesses, or create downstream risks when Public Safety/Morality is poor.

### 2.10 Investing and economy

Investing raises individual shop level and therefore available stock, while town development contributes to shop scale. Direct investment is also the Merchant Guild contribution loop. Investing in a standard shop grants local Influence. [R22]
Design implication: economic storylines can be solved through actual investment rather than abstract “fund business” dialogue. Shops themselves can persist as world actors whose service quality changes because of player behavior.

## 3. Player-Facing Gameplay Loop

```
WORLD SIMULATES A SITUATION        ↓Rumor / request / encounter / visible world change exposes it        ↓Player investigates or ignores it        ↓System discovers currently plausible actions from vanilla state        ↓Player acts through dialogue, inventory, world interaction, crime,combat, crafting, magic, economy, home systems or travel        ↓Action resolver produces success/failure/cost/exposure        ↓Consequences update vanilla + procedural world state        ↓NPC memories, knowledge, relationships and organizations react        ↓Narrative thread escalates, resolves, mutates or becomes dormant        ↓Later situations can reference the history
```

### 3.1 How quests enter the player's life

- Vanilla-like request board entries generated from active local situations.
- Direct NPC approaches driven by affinity, fame, faction, urgency or prior history.
- Rumors heard in dialogue or generated ambient barks.
- Physical evidence discovered during normal exploration.
- Generated sites appearing as consequences of world activity.
- Guild or religious channels exposing opportunities relevant to membership.
- Home residents bringing problems, contacts or consequences to the player.
- World changes with no explicit quest marker: missing vendor, closed service, hostile patrol, displaced NPC, new camp, altered relationship.

### 3.2 Journal philosophy

The journal should summarize known objectives and open questions, not reveal hidden truth. It should distinguish facts the player knows from hypotheses and rumors. A thread can remain active even when no formal quest is accepted. “Failure” means the world changed beyond a requested outcome, not that the content has been deleted.

## 4. World Model and Persistent Memory

The persistent database is the authoritative procedural state. Vanilla objects remain authoritative for vanilla facts such as actual inventory, actual character stats or actual shop level. The procedural database stores identity links, causal history, semantic facts, goals, knowledge, relationship context and narrative state.

```
NarrativeWorldState├── schemaVersion├── worldSeed├── entityRegistry│   ├── NPC records│   ├── organization records│   ├── location/site records│   └── important-object records├── eventLedger├── factGraph├── knowledgeIndex├── relationshipGraph├── narrativeThreads├── pendingDevelopments├── archivedSummaries└── directorState
```

### 4.1 Stable identity

Every procedurally important entity receives a stable GUID independent of display name. If an Elin Chara/Thing/Zone has a stable runtime/save identifier, the adapter stores that reference. If the object is destroyed or unloaded, the procedural record can remain as history. Names can change; identity must not.

### 4.2 Event ledger

Every narratively meaningful action is appended as a structured event. Events should be compact, immutable wherever practical, and reference stable entity IDs.

```
WorldEvent {  id,  eventType,          // Theft, PromiseBroken, Rescue, Death, DebtPaid...  actorId,  targetId,  relatedEntityIds[],  zoneId,  gameTime,  magnitude,  witnessedBy[],  evidenceCreated[],  tags[],  sourceThreadId}
```

Derived state such as “NPC distrusts player because of three broken promises” may be cached, but it must be reproducible from structured state where possible. This makes save migration, debugging and consequence tracing feasible.

## 5. Facts, Knowledge, Evidence, Rumors and Witnesses

This subsystem is the highest-leverage differentiator. The world needs a distinction between what is objectively true and what any given actor believes.

```
Fact {  id,  subjectId,  predicate,  object/value,  truthState,          // true / false / uncertain / superseded  secrecy,  evidenceIds[],  originEventId}KnowledgeRecord {  knowerId,  factId,  confidence,  sourceId,            // witnessed, told-by-X, document, inference  learnedAt,  canProve}
```

### 5.1 Evidence

Evidence should often correspond to actual Elin objects or world features: a stolen ring, a corpse, a ledger, an item material, a key, a letter, a witness, a shop transaction or a generated container. Procedural “evidence tokens” should be a fallback, not the norm.

### 5.2 Rumor propagation

When a character shares a fact, create a communication event. The receiver gains a belief with source and confidence. Repeated transmission may distort low-confidence facts according to a controlled rumor mutation system. Important legal facts should generally require evidence or credible witnesses before authorities act strongly.

### 5.3 Crime and witnesses

Stealth already influences whether civilians notice crimes. The procedural layer should record who plausibly witnessed a narratively relevant act and propagate knowledge from those witnesses rather than broadcasting crime details to every NPC. This enables intimidation, bribery, false testimony, alibis, framing, coverups and revenge without requiring bespoke quest scripts.

## 6. NPC Goals, Personality and Relationships

NPCs should be simulated as agents with goals and relationships, not repositories of quest flags. Vanilla Chara stats and affinity remain mechanical truth; procedural traits influence decision weighting.

```
NarrativeNpc {  id,  vanillaCharaRef,  narrativeImportance,  goals[], needs[], fears[],  personalityWeights,  relationshipEdges[],  memories[],  knownFacts[],  loyalties[],  homeLocationId,  organizationIds[],  alive,  lastSimulationTime}
```

Personality weight
What it changes
Greed
Value placed on money, bribes, profit, theft and material risk.
Mercy
Likelihood to spare, forgive, rescue or reduce punishment.
Courage
Tolerance for dangerous plans, threats and combat.
Honesty
Likelihood to lie, conceal, confess or honor agreements.
Ambition
Likelihood to seek promotion, power, wealth or expansion.
Loyalty
Resistance to betraying friends, family, guilds or factions.
Sociability
Likelihood to share information, form ties and seek help.
Curiosity
Likelihood to investigate, travel or interact with unusual facts.
Vengefulness
Persistence and severity of retaliation after harm.

### 6.1 Affinity is not memory

Affinity answers “how close is this NPC to the player?” Memory answers “why?” The mod should change vanilla affinity through meaningful outcomes while retaining causal memory for later dialogue and decision-making.

### 6.2 Importance promotion

Use a lightweight importance ladder: Background → Known → Recurring → Important → Major. Repeated player contact, cross-thread participation, strong relationships, unique knowledge or major events promote an NPC. Higher importance grants richer memory retention, more frequent simulation and more willingness by the director to reuse the character.

## 7. Organizations, Factions and Social Networks

Generated organizations should overlay - not replace - vanilla guilds and world factions. Each organization has resources, goals, leadership, membership, territory/sites, relationships and knowledge.

```
Organization {  id, type,  members[], leaderId,  goals[], resources,  relationshipEdges[],  knownFacts[],  sites[],  legitimacy,  aggression,  wealth,  currentProblems[]}
```

Examples: local merchant association, smuggler ring, farming family, mercenary band, cult, research circle, neighborhood clique, criminal crew. Their simulation should remain abstract unless activated by player proximity or narrative importance.

## 8. Situations, Narrative Threads and the Narrative Director

### 8.1 Generate situations, not quest templates

A situation is a piece of unstable world state created by interacting goals, scarcity, crime, danger, social conflict or chance. It contains a cause, participants, resources, known/hidden facts, stakes and plausible developments.

```
Situation: Missing CaravanCause: Bandit group needs moneyAction: Caravan raidedState: Driver captured, cargo stolenKnown by: bandits; partial knowledge held by spouse and guildSites: road ambush, hidden campOpen pressures: ransom, trade disruption, family fear, guild retaliationPotential developments: execution, ransom, escape, relocation, revenge
```

### 8.2 Narrative threads

A thread tracks an unresolved causal chain over time. Multiple quests or encounters can project from one thread. A thread may resolve without the player, become dormant, merge into another thread, or reactivate months later.

```
NarrativeThread {  id,  originEventId,  participantIds[],  locationIds[],  factIds[],  tension,  importance,  urgency,  state,  openQuestions[],  possibleDevelopments[],  lastAdvancedAt}
```

### 8.3 Narrative Director

The director decides what deserves simulation budget and player exposure. It should score candidate developments by relevance, novelty, player proximity, unresolved tension, participant importance, cooldown, build diversity and consequence visibility.

```
Example score =  thread tension+ relevance to recent player actions+ recurring-character bonus+ local proximity+ underused-mechanic bonus+ consequence visibility- repetition penalty- recent exposure penalty- simulation cost penalty
```

The director must not optimize solely for drama. It should preserve quieter opportunities: trade, favors, domestic problems, weird accidents, local disputes, research, crafting needs, recruitment, home issues and absurd low-stakes incidents.

## 9. Universal Action / RPG Verb Framework

The mod should invest content-authoring effort in reusable actions rather than hundreds of fixed quest templates. Each action declares when it is meaningful, what vanilla state it consumes, how it resolves and what consequences it can emit.

```
NarrativeActionDefinition {  id,  family,  semanticPreconditions,  requiredCapabilities,  targetTypes,  checkProfile,  costs,  exposureRules,  resultEffects { CriticalPass, Pass, Fail, CriticalFail },  eventOutputs[],  worldInteractionMode}
```

### 9.1 Recommended action families

Family
Representative verbs
Social
ask, persuade, lie, bluff, intimidate, flatter, appeal, bribe, negotiate, threaten, blackmail, beg, accuse, confess, apologize, vouch, provoke
Information
question, observe, eavesdrop, follow, inspect, search, track, compare testimony, research, read, appraise, examine corpse, identify material
Crime
steal, pickpocket, trespass, lockpick, plant item/evidence, destroy evidence, sabotage, smuggle, extort, kidnap, assassinate, jailbreak, impersonate
Economic
buy, sell, invest, pay debt, finance, hire, commission, supply, transport, purchase information, offer reward
Physical
fight, challenge, capture, restrain, escort, guard, rescue, carry, mine, dig, chop, build, repair, break, destroy, harvest
Crafting
cook, brew, alchemy, forge/craft, weave, carpentry, blacksmith, prepare tool or evidence solution
Magic/Faith
cast actual spell, use deity ability, offer, pray, convert altar, heal, teleport, conceal, detect, transform where supported
Home/Community
shelter, recruit resident, assign work, provide supplies, use settlement capacity, improve safety/morality/publicity, host meeting or hideout

### 9.2 Capability discovery

At any decision point, the resolver asks which action definitions make semantic sense and whether the player currently possesses the capabilities to try them. The system must not show every action in every dialogue. It should surface a curated subset plus environmental routes that remain discoverable in the world.

## 10. Mechanical Checks and Branch Resolution

### 10.1 Four kinds of requirement

Type
Behavior
Example
Hard impossibility
Do not offer or do not permit.
Cannot blackmail without leverage; cannot reveal a fact the player does not know.
Vanilla hard capability
Follow vanilla restriction.
Lock too difficult to pick; missing required tool/item.
Contested check
Player may attempt and fail.
Lie to guard; persuade witness; intimidate merchant.
Systemic world action
No abstract roll if actual gameplay can resolve it.
Sneak into archive, cook requested food, fight captors, invest in shop.

### 10.2 Check profiles

A check profile maps a semantic action to vanilla Elements/skills and target resistance. The first version should rely heavily on Check SourceData and native Check.Perform rather than creating a second dice engine.
Profile
Suggested inputs
Deception
Negotiation + Charisma; resisted by target Perception/Will and situational knowledge.
Intimidation
Negotiation + Charisma, with Strength/Fame/party power situational modifiers; resisted by courage/level/relationships.
Forensic analysis
Anatomy + Learning + Perception; evidence quality affects DC.
Forgery
Literacy + Learning + relevant crafting; target Literacy/knowledge affects detection.
Tracking
Spot Hidden + Perception + Travel; terrain/time/weather as situational modifiers where accessible.
Disguise/impersonation
Charisma/Negotiation + contextual costume/identity evidence; target Perception/knowledge.
Interrogation
Negotiation + Will/Charisma, relationship and leverage; target Will/loyalty/courage.
Technical diagnosis
Relevant crafting skill + Learning; object complexity as target DC.

### 10.3 Avoid “Charisma is the quest stat”

Dialogue should not monopolize resolution. A socially weak player should often be able to steal evidence, bribe, fight, craft, recruit an intermediary, improve affinity organically, invoke guild authority, manipulate a shop, use magic, change the home situation, or simply accept the consequences.

### 10.4 Critical results

CriticalPass should often create an extra advantage, shortcut, bonus information or relationship gain. CriticalFail should create new problems rather than arbitrary punishment. Elin's tone favors mechanically plausible absurdity: a failed intimidation becomes an accidental duel; a bad forgery implicates the wrong person; a botched stealth action knocks over valuable property and creates an additional crime witness.

## 11. Branching Quest Graphs and Failure-Forward Design

Internally, each active problem should be represented as state transitions rather than a linear script.

```
[Merchant missing]      ↓ investigate[Evidence found] ── social pressure ──> [Suspect confesses]      │                                   │      ├─ stealth route -> [Ledger stolen] ┤      │                                   ↓      ├─ combat route  -> [Gang defeated] [Truth known]      │                                   │      └─ ignore ----------------------> [Victim moved/killed]                                          ↓                                 [New feud / revenge thread]
```

### 11.1 Failure states should mutate the graph

- Failed lie → suspicion fact created, affinity drops, witness may tell others.
- Failed theft → crime/witness state, pursuit, evidence moved or secured.
- Missed rescue window → hostage moved, injured or killed; family/faction reacts.
- Player flees combat → opponents gain confidence, relocate, pursue or retaliate.
- Player ignores debt → interest/escalation, shop closure, criminal collection or flight.
- Player exposes secret without proof → credibility loss, possible counter-accusation.

## 12. Integration of Attributes, Skills and Builds

Every major generated situation should ideally support at least three distinct solution families and commonly four to seven. The generator should not guarantee that the player can currently perform all of them.

### 12.1 Skill-aware route examples

Vanilla skill/system
Procedural gameplay use
Negotiation
Persuasion, deception, bargaining, affinity manipulation, social mediation.
Pickpocket
Steal keys, letters, evidence, valuables or leverage from actual inventories.
Stealth
Avoid witnesses, tail NPCs, infiltrate sites, escape notice after illegal entry.
Spot Hidden
Find traps, hidden clues, concealed passages, planted evidence.
Lockpicking
Open actual secured containers/doors when mechanically appropriate.
Literacy
Read difficult documents, decode records, identify textual inconsistencies, create document routes.
Appraising
Identify valuable/stolen/suspicious objects; distinguish genuine vs questionable goods.
Anatomy
Interpret corpses/injuries/creature evidence; support medical/forensic inference.
Alchemy
Produce substances, antidotes, poisons, reagents or investigative tests using actual crafting.
Cooking
Produce gifts, requested supplies, ceremonial food or affinity-based solutions.
Investing
Rescue, expand, influence or gain leverage through actual shops and town economy.
Faith
Religious authority, deity-specific actions, piety/altar/offerings, supernatural resolution.
Mining/Digging
Bypass walls, recover buried evidence, reach trapped actors, physically alter sites.
Crafting professions
Repair, build, commission, analyze or produce objects instead of abstract skill dialogue.
Combat skills
Threat elimination, capture/escort, duels, bodyguard routes, forceful intervention.

### 12.2 Training can become a quest decision

Because Elin skills are trainable, an inaccessible route can itself generate direction: the player learns that better Lockpicking, Literacy, Negotiation or Faith would solve future problems. The system should not over-scale checks to player level; specialization should feel rewarding.

## 13. Karma, Fame, Affinity, Influence, Guilds, Religion and Home

### 13.1 Karma = lawfulness and legal access

Use Karma to shape authorities, vendors, criminal opportunities, witness credibility and how exposed the player is. Do not equate negative Karma with evil intent. A criminal can be beloved by specific NPCs or communities while remaining legally hunted.

### 13.2 Fame/prestige = scale and notoriety

Fame band (conceptual)
Situation weighting
Low
Personal favors, lost property, small debts, missing animals/people, local monster trouble.
Moderate
Merchant conflicts, criminal networks, kidnappings, dangerous expeditions, guild disputes.
High
Regional trade disruption, leadership disputes, major conspiracies, important hunts, faction-level crises.
Very high
The player's identity itself changes situations: challengers, imitators, pre-emptive defenses, powerful petitioners, inability to remain anonymous.

### 13.3 Affinity = willingness and relationship access

Affinity should unlock trust-sensitive actions such as borrowing, vouching, confiding, hiding, recruitment or taking significant personal risks. Memory and personality decide what those thresholds mean for a particular NPC.

### 13.4 Influence = local political capital

Because Influence is earned locally through requests and investing, it is an excellent candidate for optional civic favors. Proposed uses include accelerating an audience, requesting municipal help, sponsoring a local investigation or influencing procedural town responses. Exact costs must be balance-tested and should not break vanilla request rerolls.

### 13.5 Guilds = access networks

Guild
Procedural identity
Fighters
Bounties, monster intelligence, protection contracts, duels, mercenary contacts, authority around dangerous targets.
Mages
Research, ancient texts, occult incidents, magical evidence, specialist identification and containment.
Thieves
Fences, criminal contacts, blackmail, stolen-goods intelligence, smuggling, forgery, jailbreaks, underground shelter.
Merchants
Investment, contracts, caravans, trade disputes, debt, supply disruption, commercial introductions and economic leverage.

### 13.6 Religion = actual devotional state

Offer deity-specific routes only when relevant to the deity, player Faith/piety, available altar/offering, or granted abilities. Generated religious organizations can have relationships with vanilla religions without pretending to be core game factions.

### 13.7 My Home = a playable resolution surface

- Shelter or refuse displaced NPCs according to population capacity and resources.
- Recruit a specialist and meaningfully alter future production/services.
- Hide a criminal, witness or protected target, creating safety and legal risks.
- Use Public Safety / Public Morality / Publicity as state inputs for generated incidents.
- Generate resident-driven threads from jobs, hobbies, affinity and shortages.
- Allow a business or organization to relocate to player land when supported by actual resident/shop mechanics.
- Use food supply, beds, production and settlement infrastructure to solve crises rather than paying abstract resource counters.

## 14. Inventory, Economy, Crafting, Magic, Combat and World Interaction

### 14.1 Use real items

Quest systems commonly become artificial because they only understand “quest items.” This mod should query ordinary item categories, materials, traits, value, ownership and provenance wherever possible. If a problem needs food, medicine, valuables, contraband, weapons or evidence, normal Elin items should satisfy it when semantically appropriate.

### 14.2 Use actual actions before abstract checks

- If a wall can be mined, do not replace it with a [Mining] dialogue roll.
- If an NPC can be recruited through affinity and Charisma, use that system rather than a special follower flag.
- If a shop can be invested in, use actual investment rather than “fund the merchant.”
- If a prisoner can be escorted through the map, prefer actual escort state where feasible.
- If a target can be fought, captured or killed, let combat state feed the narrative engine.
- If a spell materially solves the obstacle, detect that outcome instead of requiring a bespoke magic branch.

### 14.3 Economic simulation limits

Do not attempt a full commodity economy in version 1. Use bounded abstractions: organization wealth bands, shop level, local availability, debt records, important cargo and explicit shortages. Only simulate price/supply effects where Elin exposes stable mechanisms worth integrating.

## 15. Procedural Locations and Map Handling

Elin Source Sheets support Zone data, and vanilla requests already use automatically generated dedicated maps. The first versions should therefore lean on native/random zone generation and decorate or configure those spaces with narrative content rather than inventing a new terrain generator. [R23]

### 15.1 Narrative site descriptor

```
NarrativeSiteDescriptor {  id,  siteType,            // hideout, ruin, camp, workshop, shrine, estate...  biome/profile,  dangerLevel,  controllingOrgId,  occupants[],  importantObjects[],  cluePlacements[],  accessConstraints[],  persistenceMode,     // ephemeral / persistent / revisitable  generationSeed}
```

### 15.2 Persistence policy

Not every generated site should remain on the world map forever. Important or player-modified sites become persistent; throwaway combat/interior spaces can be ephemeral but must write their consequential state back to the thread before unloading. Revisitability should be a narrative decision.

## 16. Dialogue, Drama and Presentation

Elin Drama provides rich dialogue, steps, conditional choices and actions; C# can invoke dialogue and override drama routing. Decompilation shows DramaActor choices can use Check.Get for checked options. [R24][R5]

### 16.1 Presentation architecture

```
Player talks to procedural NPC        ↓Dialogue presenter requests current conversation model        ↓Narrative system supplies:  relevant known facts  NPC goals and mood  relationship/memories  actionable open threads  currently sensible player actions        ↓Drama/UI presents a bounded set of choices        ↓Action resolver executes result        ↓State updates and dialogue continues
```

### 16.2 Text generation strategy

Start with deterministic grammar, authored phrase banks, tagged templates and variable substitution. The simulation must never require an LLM. If optional AI dialogue is added later, it may render structured facts into prose but must not decide authoritative world state, checks or consequences.

### 16.3 Avoid storybook mode

Long generated dialogue is not the goal. Use concise conversation to reveal playable information and choices, then send the player back into Elin. The strongest content is a clue that changes what the player can physically do, not a paragraph explaining a fictional history.

## 17. Rewards and Consequence Propagation

Reward vocabulary should primarily consist of things Elin already values.
Reward / consequence
Use
Orens / items
Direct material reward, bribe recovery, cargo, compensation.
Karma
Legal/moralized consequences consistent with vanilla lawfulness.
Prestige/fame
Public recognition or disgrace; drives future situation scale.
Town Influence
Local civic capital.
Affinity
Persistent personal relationship consequence.
Guild contribution/rank interaction
Professional recognition where APIs safely permit.
Shop level / investment
Persistent commercial improvement.
Resident recruitment
A new person and production/social node at My Home.
Information / evidence
Unlocks future actions, blackmail, investigation and prediction.
Faction/organization relationship
Changes access, hostility and future events.
Access
Safehouse, site, service, contact, vendor or route.
World state
NPC alive/dead/missing, site destroyed, business closed/open, leadership changed.

### 17.1 Consequence propagation

Each resolution emits events. Event listeners update affected systems: affinity, facts/knowledge, organization resources, goals, thread tension, site state and possible vanilla values. Secondary reactions should be queued rather than recursively executed without limits.

## 18. Time, Escalation and Off-Screen Simulation

Situations should evolve because time passes, but the player should not feel that every quest is an arbitrary countdown. Prefer state escalation milestones over opaque timers.

```
Example hostage threadDay 0: victim capturedDay 2: family organizes searchDay 4: captors relocate if pressure risesDay 7: ransom demand appearsDay 10: injury / escape attempt / execution riskDay 14: family or guild retaliatesDay 20: conflict may become a persistent feud
```

### 18.1 Simulation tiers

Tier
Who
Update style
Active
Current zone and directly involved important entities.
Full event/reactive simulation.
Warm
Recent, nearby or recurring entities/threads.
Daily or event-triggered coarse simulation.
Cold
Minor persistent entities with unresolved relevance.
Monthly/large-time-step probabilistic updates.
Archived
Resolved/minor historical entities.
No active simulation; retained summary and facts until referenced.
This compression is necessary. A mod should imitate Dwarf Fortress causal richness, not its total simulation volume.

## 19. Save Data, Persistence, Compatibility and Performance

### 19.1 Save design requirements

- Versioned schema with explicit migrations.
- Stable IDs and no reliance on display names.
- Atomic-ish save writes or recoverable backup strategy.
- Compact event storage with summarization for old trivial history.
- Graceful handling of missing vanilla/modded entities after mod list changes.
- No requirement for external network service.
- Debug export to human-readable JSON for save auditing.

### 19.2 Memory consolidation

Routine events should decay or consolidate. Repeated purchases can become “regular customer” memory; rescuing a spouse or murdering a relative remains defining. Recommended memory classes: Trivial, Routine, Notable, Important, Defining.

### 19.3 Compatibility boundary

Create a VanillaStateAdapter layer so the simulation never scatters direct calls to Elin internals. All version-sensitive reads/writes should live behind capability interfaces. Harmony patches should be narrow, documented and used only when no stable public/runtime path exists.

## 20. Technical Architecture and Elin Integration

```
Elin Procedural Questing Mod│├── Integration│   ├── VanillaStateAdapter│   ├── CheckAdapter│   ├── QuestJournalAdapter│   ├── DramaAdapter│   ├── ZoneAdapter│   ├── CharaAdapter│   ├── CrimeWitnessAdapter│   ├── HomeAdapter│   └── SaveAdapter│├── Simulation Core│   ├── EntityRegistry│   ├── EventLedger│   ├── FactGraph│   ├── KnowledgeSystem│   ├── RelationshipSystem│   ├── OrganizationSystem│   ├── SituationFactory│   ├── ThreadEngine│   └── WorldClock│├── Gameplay│   ├── NarrativeDirector│   ├── ActionRegistry│   ├── CapabilityResolver│   ├── ActionResolver│   ├── ConsequenceEngine│   └── RewardResolver│├── Generation│   ├── NPCGenerator│   ├── OrganizationGenerator│   ├── SiteGenerator│   ├── ProblemGenerator│   └── TextTemplateEngine│└── Debug / Tooling    ├── WorldInspector    ├── ThreadGraphView    ├── WhyOptionInspector    ├── EventLogViewer    ├── DeterministicSeedReplay    └── SaveMigrationTests
```

### 20.1 Data-driven vs coded responsibilities

Data-driven
Runtime C#
Action definitions and tags
Entity identity and persistence
Check profile rows where viable
Capability discovery
Phrase banks and dialogue templates
World simulation and director
Situation archetype parameters
Fact/knowledge/relationship graph
Zone/site archetypes
Elin runtime object integration
Reward/consequence mappings
Save/migration/version adapters

### 20.2 Source Sheets strategy

Use Source Sheets where Elin expects declarative content. Avoid generating huge spreadsheets at runtime. Define reusable procedural archetypes and native Check definitions statically, then populate specific generated entities/state in the mod save database.

## 21. Content Authoring Model and Data Schemas

### 21.1 Situation archetype

```
SituationArchetype {  id: "debt_default",  requiredRoles: [debtor, creditor],  optionalRoles: [collector, family, rival],  seedsFacts: [...],  seedsGoals: [...],  compatibleLocations: [...],  actionFamilies: [social, economic, crime, violence, investigation],  developmentRules: [...],  consequenceWeights: [...],  toneTags: [mundane, tense, comic-compatible]}
```

### 21.2 No authored branch explosion

Archetypes should specify causes and affordances, not every branch. “Debt default” can create a creditor, evidence, collateral and escalation rules; the universal action framework supplies pay, negotiate, steal the ledger, threaten, expose corruption, recruit, flee, kill, invest, or seek third-party help when context allows.

### 21.3 Semantic tags

Define a controlled ontology early: person roles, crimes, obligations, relationship types, site roles, item/evidence functions, organization types, action families and consequence categories. Procedural generation becomes reliable when systems share a common vocabulary.

## 22. Development Roadmap and Prototype Gates

### Phase 0 - Reverse-engineering spike

1. Confirm current runtime access to player elements/skills, Chara affinity, Karma, prestige, town Influence, guild state, religion, Home state and inventory.
1. Confirm practical creation/persistence of generated Chara and zones.
1. Confirm how best to attach mod save data and migration version.
1. Prototype custom Check rows and Check.Perform from dialogue/runtime.
1. Prototype Drama choice invocation and dynamic choice injection or generic procedural Drama bridge.
1. Confirm crime/witness hooks and the safest way to observe relevant vanilla events.
Gate ADo not build procedural generation until a tiny hard-coded scenario can read vanilla stats, perform a native-style check, update affinity/Karma as appropriate, save state, reload, and continue correctly.

### Phase 1 - Three-NPC simulation laboratory

Create three persistent NPCs. NPC A steals an actual important item from B; C witnesses it. Implement question, search, pickpocket, bribe, intimidate, persuade, lie, return, keep, expose, frame and attack routes using as many real Elin mechanics as possible.
Gate BLet 10+ in-game days pass through multiple outcomes. If the resulting state is explainable, persistent, replayable and fun without hand-authored prose, the core architecture is viable.

### Phase 2 - Persistent thread + generated site

- Generate one causal situation archetype.
- Generate one site using native zone infrastructure.
- Persist NPCs, organization and important objects.
- Project the situation into journal/dialogue.
- Advance the situation when ignored.
- Revisit consequences after site unload/reload.

### Phase 3 - Universal action library

Expand toward 30 high-quality verbs before adding broad content volume. Each verb needs testable preconditions, native mechanics, four-result behavior where checked, exposure rules and consequence outputs.

### Phase 4 - Director and multiple archetypes

Add 10-15 situation archetypes spanning personal, criminal, economic, exploration, combat, crafting, home and religious play. Introduce director pacing and repetition controls.

### Phase 5 - Organizations and off-screen development

Introduce organization resources, leadership, inter-organization relations and coarse time simulation. Ensure the player can see consequences without opening a debug panel.

### Phase 6 - Scale, polish and optional advanced text

Only after systemic gameplay is proven should the project invest heavily in richer generated dialogue, large archetype catalogs, sophisticated rumor distortion, broader map decorators or optional AI prose.

## 23. Testing, Telemetry and Quality Standards

### 23.1 Deterministic testing

- Every generated situation records its seed and generation decisions.
- Headless/unit tests replay action resolution using fixed seeds.
- Save migration fixtures preserve old versions.
- Property tests verify invariants: dead NPCs do not perform normal actions, unknown facts cannot be truthfully revealed, destroyed evidence cannot be physically presented, etc.

### 23.2 “Why?” tooling

A developer inspector should show why an action appeared and why a result occurred: semantic preconditions, vanilla stats read, final DC, situational modifiers, roll, witnesses, emitted events and downstream listeners. Procedural systems become unmaintainable without causality inspection.

### 23.3 Player-facing quality metrics

Metric
Target behavior
Route diversity
Major threads routinely expose 3+ distinct solution families.
Mechanical density
Most resolutions use actual Elin systems rather than pure text choices.
Consequence visibility
Important choices create noticeable later state.
Recurrence
Some generated NPCs/sites meaningfully recur.
Failure productivity
A failed check often opens/changes play rather than ending it.
Repetition control
Same archetype does not feel identical due to actor/goal/site/evidence variation.
Performance
Off-screen simulation remains bounded and does not scale linearly with total historical NPC count.

## 24. Example End-to-End Procedural Questlines

### 24.1 The Missing Brewer

Generated truth: a brewer owes 12,000 orens to merchant Varik. Varik hired two thugs to frighten him. The thugs accidentally killed the brewer. Varik does not yet know he is dead. One thug buried the body; the other stole the brewer's ring. The brewer's daughter suspects Varik. A local inn is losing supply.
Route
Actual systems involved
Possible consequences
Investigation
Spot Hidden/Perception, Anatomy, Appraising, physical evidence.
Discover body and cause; identify ring/weapon link.
Social
Affinity, Negotiation, Charisma, knowledge contradictions.
Varik reveals hired thugs or becomes suspicious.
Criminal
Pickpocket, Stealth, Lockpicking, Thieves Guild contacts.
Steal note/ledger; fence identifies ring; crime witnesses may appear.
Economic
Orens, Investing, debt ownership/settlement abstraction.
Pay/buy debt, obtain leverage, rescue business.
Combat
Threat, duel, assault, capture/kill.
Rapid truth through force but legal/relationship fallout.
Fame/authority
Prestige and town/guild standing.
Authorities accept testimony with less evidence or request player intervention.
Home
Recruit displaced daughter or relocate business if actual resident/vendor mechanics support it.
Persistent service at player land; future resident threads.
Possible endings: expose Varik; prove he ordered intimidation but not murder; blackmail him; protect him; turn in the thugs; kill them; frame a rival; tell or lie to the daughter; save or lose the brewery; recruit the daughter; leave the case unresolved. Months later the surviving actors should remember and react to whichever state actually occurred.

### 24.2 The Grain Route

A baker's grain supply stops. The direct cause is a missing caravan; the caravan was seized by a generated outlaw group occupying a native-generated forest site. The guild wants the route cleared, the driver's spouse wants the driver alive, and local farmers reveal that the merchant guild has been underpaying them.
- Combat route: clear or capture the gang.
- Stealth route: free the driver and recover documents without wiping the site.
- Negotiation route: broker ransom or redirect trade.
- Economic route: finance a new supply chain or invest in affected merchant capacity.
- Criminal route: join the outlaws, sell information, sabotage the guild or steal cargo.
- Political route: use Influence/guild standing to pressure local response.
- Home route: temporarily produce/supply relevant food/resources if the player economy supports it.
- Ignore route: prices/services deteriorate abstractly, driver outcome changes, guild/farm relations worsen, later retaliation occurs.

### 24.3 Tiny comedic thread: The Wrong Chicken

A resident accuses a neighbor of stealing a valuable chicken. The chicken actually wandered into a third NPC's area. A high-Charisma player can mediate; a perceptive player can find tracks; a thief can steal a different chicken and “solve” the problem; a fighter can threaten everyone and accidentally create a feud; the player can recruit the chicken if vanilla systems permit. Critical failures should let a trivial dispute spiral into ridiculous but mechanically coherent consequences without pretending it is an epic storyline.

## 25. Risks, Anti-Patterns and Scope Control

Risk / anti-pattern
Why it is dangerous
Countermeasure
LLM as world authority
Hallucinated state, nondeterminism, online dependency.
Structured deterministic simulation; AI only optional rendering.
Charisma-dialogue dominance
Makes most Elin builds irrelevant to quests.
Require route-family diversity and systemic world actions.
Every NPC fully simulated
Performance collapse and unmanageable saves.
Importance tiers, cold simulation, archival summaries.
Thousands of quest templates
Authoring burden with shallow variation.
Situation archetypes + universal actions + causal state.
Duplicate reputation systems
Feels detached from Elin and creates conflicting truth.
Use affinity/Karma/fame/Influence/guild state first.
Invisible consequences
Players stop believing choices matter.
Prioritize world-visible changes and recurring actors.
Every problem is urgent
Creates task-list anxiety.
Mix passive, low-stakes and self-resolving threads with genuine crises.
Hard skill gates everywhere
Encourages reload/optimization and blocks improvisation.
Prefer checks/failure-forward except true impossibility.
Abstract checks replacing gameplay
Turns mod into a storybook.
Use actual locks, items, maps, witnesses, combat, shops, crafting and spells.
Patch-heavy architecture
Fragile against Early Access updates.
Adapter layer; minimal Harmony; version capability checks.

## 26. Ideal End-State Feature Set

- Persistent procedural NPCs with families, friends, enemies, goals, jobs, affiliations and memory.
- Generated organizations with leaders, resources, sites, relationships and changing agendas.
- Persistent fact/knowledge/evidence graph supporting lies, rumors, witnesses, blackmail and investigations.
- Procedural situations across crime, economy, exploration, combat, home, religion, guilds, crafting and social play.
- 30-100+ robust universal actions that understand real Elin mechanics.
- Native Check-based contested resolution with four outcomes and visible difficulty language.
- Procedural sites built primarily on Elin zone generation plus narrative decoration.
- Failure-forward thread evolution and meaningful ignored-content consequences.
- Fame-sensitive scale, Karma-sensitive legality, affinity-sensitive trust, Influence-sensitive local power.
- Guild and religion routes that use actual membership, rank/piety/abilities.
- My Home as a genuine social/economic quest solution platform.
- Selective long-term off-screen simulation with archival history.
- Recurring procedurally generated characters whose relevance emerges from play.
- Debug/inspection tooling capable of explaining every generated decision.
- Optional richer text generation that never controls authoritative state.
North-star experienceAfter a long save, the player should be able to tell stories that no author explicitly scripted: “Those bandits were originally a random caravan problem; I spared their leader, later used them against a merchant, his daughter moved to my settlement, and years later she hired me to investigate her father’s murder.” The mod succeeds when that chain is produced by remembered causality and normal Elin play.

## 27. Research Sources and References

Sources below were used to ground the vanilla-mechanics and modding claims in this document. Elin is in active Early Access; re-verify version-sensitive facts during implementation. Community wiki pages can also lag current builds, so code-facing work should prioritize the current Elin.dll/decompiled reference and official/community modding source documentation.
R1. Elin Modding Wiki - Setup Script Mod Project - https://elin-modding.net/articles/2_Getting%20Started/Script%20Mods/script_mod
R2. Elin Modding Wiki - Basic Source Sheet Modding - https://elin-modding-resources.github.io/Elin.Docs/articles/2_Getting%20Started/sourcesheet_setup
R3. Elin Decompiled Documentation - project/version landing page - https://code.elin-modding.net/
R4. Elin Decompiled Documentation - Check Class - https://elin-modding-resources.github.io/Elin-Decompiled/classCheck.html
R5. Elin Decompiled Documentation - DramaActor Class - https://elin-modding-resources.github.io/Elin-Decompiled/classDramaActor.html
R6. Ylvapedia - Attributes - https://ylvapedia.wiki/wiki/Elin%3A%E4%B8%BB%E8%83%BD%E5%8A%9B
R7. Ylvapedia - Skills - https://ylvapedia.wiki/wiki/Elin%3ASkills
R8. Ylvapedia - Negotiation - https://ylvapedia.wiki/wiki/Elin%3ANegotiation
R9. Ylvapedia - Affinity - https://ylvapedia.wiki/wiki/Elin%3AAffinity
R10. Ylvapedia - Pickpocket - https://ylvapedia.wiki/wiki/Elin%3APickpocket
R11. Ylvapedia - Stealth - https://ylvapedia.wiki/wiki/Elin%3AStealth
R12. Ylvapedia - Spot Hidden - https://ylvapedia.wiki/wiki/Elin%3ASpot_Hidden
R13. Ylvapedia - Lockpicking - https://ylvapedia.wiki/wiki/Elin%3ALockpicking
R14. Ylvapedia - Karma - https://ylvapedia.wiki/wiki/Elin%3AKarma
R15. Ylvapedia - Requests - https://ylvapedia.wiki/wiki/Elin%3A%E4%BE%9D%E9%A0%BC/en
R16. Ylvapedia - Guilds - https://ylvapedia.wiki/wiki/Elin%3AGuilds
R17. Ylvapedia - Gods - https://ylvapedia.wiki/wiki/Elin%3AGods
R18. Ylvapedia - Abilities (faith section) - https://ylvapedia.wiki/wiki/Elin%3AAbilities
R19. Ylvapedia - Residents - https://ylvapedia.wiki/wiki/Elin%3AResidents
R20. Ylvapedia - Home Skills & Policies - https://ylvapedia.wiki/wiki/Elin%3AHome_Skills_%26_Policies
R21. Ylvapedia - Housing - https://ylvapedia.wiki/wiki/Elin%3AHousing
R22. Ylvapedia - Investing - https://ylvapedia.wiki/wiki/Elin%3AInvesting
R23. Elin Modding Wiki - Zone Source Sheet - https://elin-modding-resources.github.io/Elin.Docs/articles/10_Source%20Sheets/zone
R24. Elin Modding Wiki - Drama - https://elin-modding-resources.github.io/Elin.Docs/articles/10_Source%20Sheets/drama
R25. Elin Modding Wiki - Element Source Sheet - https://elin-modding-resources.github.io/Elin.Docs/articles/10_Source%20Sheets/element
R26. Elin Modding Wiki - Modding Cheatsheet / source export - https://elin-modding-resources.github.io/Elin.Docs/articles/2_Getting%20Started/modding_cheatsheet
R27. Ylvapedia - Appraising - https://ylvapedia.wiki/wiki/Elin%3AAppraising
R28. Ylvapedia - Literacy - https://ylvapedia.wiki/wiki/Elin%3ALiteracy
R29. Ylvapedia - My Home - https://ylvapedia.wiki/wiki/Elin%3AMy_Home
R30. Ylvapedia - Factions (noting feature status) - https://ylvapedia.wiki/wiki/Elin%3AFactions

## Appendix A - Initial Implementation Backlog

Priority
Work item
P0
VanillaStateAdapter skeleton and version detection
P0
Mod save/load + schema migration
P0
Stable entity registry
P0
Event ledger
P0
Fact + knowledge model
P0
Native Check spike
P0
Generic procedural Drama spike
P0
Three-NPC theft scenario
P1
Relationship/memory reasons
P1
Witness capture and crime event observation
P1
Action registry + 10 actions
P1
Thread engine + time escalation
P1
Generated site prototype
P1
Journal projection
P1
Debug world inspector
P2
Organization model
P2
Director scoring
P2
30-action library
P2
10 situation archetypes
P2
Guild route adapters
P2
Home route adapters
P2
Faith route adapters
P3
Rumor distortion
P3
Cold/off-screen simulation
P3
Persistent business/service consequences
P3
Advanced map decorators
P3
Optional richer text generation

## Appendix B - Design Review Checklist for Every New Situation Archetype

- Does this situation arise from a causal world state rather than a random objective?
- Are the important actors persistent and motivated?
- What facts are true, and who initially knows each fact?
- What actual Elin objects, NPCs, zones or services embody the problem?
- Does it support at least three distinct solution families?
- Does it over-rely on Charisma/Negotiation?
- Can a non-dialogue Elin action solve or transform it?
- What happens on Fail and CriticalFail?
- What happens if the player ignores it?
- Which vanilla values change: affinity, Karma, fame, Influence, inventory, shop/home/guild state?
- What consequences can be noticed later without reading a log?
- Can one participant or consequence recur in another thread?
- What is the simulation cost when the thread is off-screen?
- Can the system explain why it generated this situation and each available action?
