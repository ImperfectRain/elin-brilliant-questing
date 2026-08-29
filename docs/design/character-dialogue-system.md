# Elin Brilliant Questing — Procedural Character, Dialogue & Scene System

**Status:** Primary implementation blueprint for procedural characters, dialogue, scenes, personality expression, tone, and player-facing narrative delivery  
**Date:** 28 August 2026  
**Relationship to other documents:** Companion to `master-design.md`, `post-master-findings.md`, and `elin-brilliant-questing-living-world-player-experience-priorities.md`  
**Primary inspiration:** Elona/Elin, Façade, Wildermyth, Versu, Talk of the Town, Dwarf Fortress, Shadows of Doubt

---

# 0. Purpose

This document defines Brilliant Questing's **procedural character expression layer**. The goal is not to generate random dialogue around random quests. The goal is to make simulated people feel like specific inhabitants of Elin who have durable personalities, interpret events differently, want different things, speak differently, remember history, form grudges and obligations, participate in social scenes, reveal information selectively, react to context, and remain recognizable across months or years of play.

Core rule:

> **Procedural generation should assemble authored expressive building blocks around authoritative simulated state. It should not invent arbitrary truth, motivations, personality or plot merely to fill text.**

A character line should be the final projection of:

```text
WHAT ACTUALLY HAPPENED
+ WHAT THIS CHARACTER KNOWS OR BELIEVES
+ WHAT THIS CHARACTER WANTS
+ HOW THIS CHARACTER FEELS
+ WHO THEY ARE TALKING TO
+ WHERE THE CONVERSATION IS HAPPENING
+ WHAT SOCIAL PRACTICE IS ACTIVE
+ WHO THIS CHARACTER IS
+ WHAT HAS HAPPENED BETWEEN THESE PEOPLE BEFORE
+ ELIN'S WORLD TONE
```

not:

```text
quest template + random noun + personality adjective
```

---

## 0a. Notes on filing

*Added on commit; not part of the original document.*

Three integration points, from checking the proposals against the code and the game's assemblies.

**`Scene` collides with an Elin type.** `EClass.scene` is a `Scene`, and the shipped plugin compiles
Core's sources into an assembly that references `Elin.dll`, so the game's global namespace is in
scope. `Goal` already had to be renamed `NpcGoal` for exactly this reason. §26's `NarrativeScene` is
correctly named; anything shortened to `Scene` will not compile. The other proposed names in this
document were checked and are clear. Check new ones with
`dotnet run --project tools/ApiDump -- --find <Name>` before adopting them.

**`NarrativeNpc` already exists** in `src/BrilliantQuesting.Core/World/NarrativeNpc.cs`, with
`Personality`, `Goals`, `OrganizationIds`, `Importance` and `VanillaCharaRef`. §4 should be read as
an extension of that record, not a replacement - it is persisted, migrated and covered by tests.

**§14.5's belief provenance is partly built.** `KnowledgeGraph` and `KnowledgeRecord` already carry
`Confidence`, `KnowledgeSource` (Witnessed / Hearsay / Document / Inference / Participant),
`CanProve` and `ToldBy`, and `RumorSystem` already decays confidence per retelling while refusing to
transmit proof. `BeliefExpressionContext` should be a projection of those, not a second store.

---

# 1. Product thesis

Elin remains authoritative for stats, skills, affinity, Karma, Fame, guilds, faith, inventory, combat, items, shops, Home, zones, physical interaction, time, money and crime.

Brilliant Questing adds persistent semantic identity, goals and needs, actor-local knowledge, memories, social interpretation, durable personality, voice profile, social obligations, storylet/scene selection, context-sensitive dialogue realization, narrative continuity and cross-event callbacks.

The target reaction is:

> “This person had a reason to say that.”

not:

> “The generator picked a line.”

---

# 2. High-level architecture

```text
ELIN WORLD STATE
    ↓
EVENT OBSERVATION / BQ WORLD EVENTS
    ↓
FACTS + KNOWLEDGE + MEMORIES
    ↓
CHARACTER INTERPRETATION
    ↓
DEVELOPMENT / DRAMATIC OPPORTUNITY DETECTION
    ↓
STORYLET CASTING
    ↓
SCENE / SPEECH ACT GENERATION
    ↓
DIALOGUE REALIZATION
    ↓
ELIN PRESENTATION
    ↓
PLAYER OR NPC ACTION
    ↓
ACTION RESOLVER
    ↓
NEW AUTHORITATIVE EVENTS
```

Dialogue is not flavor floating above the simulation. It is a view into the simulation.

---

# 3. Core principles

1. **Characters are not quest dispensers.** `Witness`, `Victim`, `Accuser`, `Debtor`, `FestivalJudge` and `GuildInformant` are temporary roles, not identities.
2. **Personality affects decisions before prose.** Traits change what actors want, tolerate, reveal, forgive and attempt.
3. **Dialogue never creates authoritative truth.** It may state truth, false belief, lies, speculation or rumor.
4. **Authored microstructure beats authored full plots.** Invest in storylets, speech acts, beats, reactions, callbacks and fragments.
5. **Recurrence beats novelty.** Reusing a known actor is often stronger than generating a better new one.
6. **Absurdity should be causal.** Elin weirdness is usually an absurd premise treated sincerely with real mechanical consequences.
7. **Mundane dialogue is necessary.** If every line tries to be quirky, the generator becomes obvious.

---

# 4. Character model

Suggested durable record:

```csharp
NarrativeNpc
{
    EntityId;
    VanillaCharaUid;

    NameHistory[];
    Occupation;
    SocialRoles[];

    CorePersonality;
    VoiceProfile;

    Sensitivities[];
    Contradictions[];
    Quirks[];
    TopicPreferences[];
    TopicAversions[];

    Goals[];
    Needs[];
    Fears[];
    Desires[];

    ProblemSolvingStyle;

    KnownFacts[];
    Beliefs[];
    Memories[];

    Relationships[];
    Obligations[];

    EmotionalState;

    NarrativeImportance;
    LastInteractionTick;
    RecurrenceScore;
}
```

Use selective depth. Ordinary background actors can remain shallow until they become relevant.

---

# 5. Personality system

## 5.1 Behavioral dimensions

Use independent continuous dimensions rather than one archetype:

```text
Bold ↔ Timid
Patient ↔ Impulsive
Warm ↔ Aloof
Earnest ↔ Playful
Optimistic ↔ Fatalistic
Orderly ↔ Chaotic
Merciful ↔ Vindictive
Honest ↔ Deceptive
Generous ↔ Greedy
Loyal ↔ Self-serving
Trusting ↔ Suspicious
Humble ↔ Proud
Curious ↔ Indifferent
Conventional ↔ Eccentric
Status-blind ↔ Status-conscious
```

These are private decision weights, not player-facing RPG stats.

## 5.2 Conversational tendencies

Separate verbal style from behavior:

```text
Terse ↔ Verbose
Direct ↔ Evasive
Formal ↔ Casual
Literal ↔ Figurative
Serious ↔ Teasing
Confident ↔ Hedging
Private ↔ Oversharing
Polite ↔ Abrasive
Plainspoken ↔ Elaborate
```

Two greedy characters should not automatically sound alike.

## 5.3 Problem-solving style

Each actor should have weighted preferences such as:

```text
Confront
Avoid
AskAuthority
AskFriends
PaySomeone
DoItSelf
Manipulate
UseViolence
SeekGuild
SeekReligiousHelp
Wait
Flee
Publicize
Conceal
```

This creates systemic variety. A missing goat can cause one NPC to report to guards, another to accuse a rival, another to offer payment, another to steal a replacement, another to pray, and another to do nothing but complain.

## 5.4 Sensitivities

Sensitivities define what a person reacts strongly to: public embarrassment, unpaid debt, threats to siblings, animals, adventurers, divine punishment, poverty, contracts, interruption, theft, violence, dishonesty, status, etc.

They modify interpretation, emotional reaction, action choice and dialogue emphasis.

## 5.5 Contradictions

Important characters should often have one durable contradiction:

```text
cowardly but protective
greedy but refuses to profit from medicine
honest except about family
violent but hates theft
deeply religious but habitual gambler
criminal who respects contracts
arrogant but desperate for approval
kind to strangers, cruel to friends
```

## 5.6 Quirks

Quirks should be sparse, sticky and recognizable:

- compares people to fish;
- apologizes to corpses;
- never discusses money indoors;
- collects spoiled food;
- thinks furniture remembers insults;
- refuses to use names;
- is extremely polite while threatening;
- believes younger sisters bring luck;
- greets doors;
- distrusts circular tables.

Do not reroll quirks on every interaction. If an NPC becomes “the woman who thinks furniture remembers grudges,” that can become part of her lifelong identity.

---

# 6. Identity anchors

Each significant NPC should develop roughly 3–5 durable anchors.

Example:

```text
Mira
Occupation: brewer
Core tendency: practical
Contradiction: generous, but obsessive about written debt
Relationship anchor: protective of younger brother
Quirk: hates mushrooms
History anchor: lost first shop in a fire
```

Future scenes should reference anchors only when relevant. The goal is: “Of course Mira cares about the missing ledger.”

---

# 7. Topic model

NPCs should have persistent topic interests and aversions.

```csharp
TopicPreference
{
    TopicId;
    Interest;
    Expertise;
    Privacy;
    EmotionalCharge;
}
```

Examples: farming, gossip, guild politics, religion, cats, weapons, food, Home, a particular mine, a sibling, an old fire, chairs.

This gives ordinary `Let's talk` interactions real character value without requiring a formal quest.

---

# 8. Emotional state

Personality is stable. Emotion is transient.

Track a small useful set: anger, fear, shame, grief, relief, excitement, suspicion, affection, stress, confidence.

Emotion should bias action selection, disclosure and dialogue realization, then decay or transform over time.

Example:

```text
proud + angry + public setting → confrontation

timid + ashamed + private setting → evasive admission
```

---

# 9. Actor-local interpretation

The same event should mean different things to different people.

A theft can be interpreted as criminal violation, desperation, competence, sacrilege, scandal or personal betrayal depending on the observer.

Suggested representation:

```csharp
EventInterpretation
{
    ObserverId;
    EventId;

    PerceivedIntent;
    PerceivedSeverity;

    MoralTags[];
    SocialTags[];

    PersonalRelevance;
    EmotionalReaction;
    DesiredResponses[];
}
```

This is not a duplicate Karma system. It is actor-local social meaning.

---

# 10. Character development over time

Allow slow, meaningful personality shifts after repeated or severe history.

```text
repeated danger survived → slightly bolder
repeated betrayal → more suspicious
successful business rescue → more optimistic
family killed → vengeance sensitivity rises
```

Do not constantly mutate identity. The character should remain recognizable.

---


## 10.5 Values, needs and goal formation

Personality should modify decision-making, but it should not be the source of motivation by itself. Actors need a separate model for **what they value**, what they currently need, and what goals follow from those conditions.

Suggested value profile:

```csharp
ValueProfile
{
    ValueId;       // family, wealth, law, faith, status, animals, knowledge, freedom...
    Importance;    // durable preference
    Flexibility;   // how willing the actor is to compromise it
}
```

Useful value domains include family, wealth, status, guild, faith, animals, community, law, personal freedom, contracts, tradition, knowledge, adventure, comfort, reputation and Home.

Goal formation should follow a traceable pipeline:

```text
WORLD STATE
→ NEED / PRESSURE
→ RELEVANT VALUES + SENSITIVITIES
→ DESIRES
→ CANDIDATE GOALS
→ CANDIDATE ACTIONS
→ PERSONALITY / PROBLEM-SOLVING WEIGHTS
→ CHOSEN ACTION
```

Example:

```text
Fact:
Mira is owed 4,000 orens.

Need:
money

Values:
friendship 0.72
contracts 0.88

Sensitivity:
unpaid debt

Personality:
proud + merciful

Candidate goals:
recover money          0.82
preserve friendship    0.71
avoid public shame     0.48
punish debtor          0.16
```

`ProblemSolvingStyle` should bias **how** an actor pursues a goal, not create the goal by itself.

Contradictions can emerge from competing values as well as authored contradiction tags. A character may sincerely value both honesty and family, then lie to protect a sibling. This is stronger than simply labeling the actor "honest except about family."

## 10.6 Mundane needs

Not every goal should come from tragedy or major narrative pressure.

Actors should also react to ordinary needs:

```text
hungry
late
bored
embarrassed
wants a better bed
needs money
wants promotion
jealous
dislikes neighbor
likes a particular food
avoids work
wants someone to visit
```

Mundane motivations make the world feel inhabited. Major situations become more believable when they collide with ordinary life.

---

# 11. Storylet architecture

A quest template specifies a complete player task. A storylet specifies a reusable social/dramatic pattern.

Example: `PublicAccusation`.

Requirements:

```text
A believes B caused harm
A can meet B
A is willing to confront B
optional observers may be present
```

It does not care whether the cause was theft, debt, a dead goat, forged letter, missing wine, religious insult or stolen furniture. The world supplies the meaning.

Suggested structure:

```csharp
StoryletDefinition
{
    Id;
    SituationTags[];
    ToneTags[];
    Preconditions[];
    RequiredRoles[];
    OptionalRoles[];
    KnowledgeConstraints[];
    RelationshipConstraints[];
    PersonalityPreferences[];
    BeatGraph;
    AllowedSpeechActs[];
    AllowedWorldActions[];
    ConsequenceHooks[];
    RepetitionClass;
    Cooldown;
}
```

**Storylets never create facts.** They expose, contest, conceal or dramatize facts.

---

# 12. Initial storylet library

Start with a compact but expressive set:

- PublicAccusation
- PrivateConfrontation
- RequestForHelp
- RequestForPayment
- OfferOfPayment
- Confession
- PartialConfession
- Denial
- CounterAccusation
- Ultimatum
- Threat
- Apology
- Refusal
- Bargain
- BribeOffer
- BribeSolicitation
- Gossip
- Warning
- PleaForSecrecy
- FavorCollection
- FavorRepayment
- Introduction
- Vouching
- WitnessStatement
- TestimonyDisagreement
- Intervention
- ReconciliationAttempt
- Taunt
- Boast
- Complaint
- Invitation
- Farewell
- PublicAnnouncement
- Celebration
- Mourning
- EmbarrassingCorrection

A small library like this can dramatize a very large number of causal states.

---

# 13. Casting engine

Storylets define temporary roles such as Accuser, Accused, Defender, Witness, Mediator, Authority, Victim, Beneficiary, Rival, Confidant and Observer.

Positive requirements should reward relevant knowledge, relationship, loyalty, personality and presence. Negative requirements should reject actors who are dead, absent, ignorant of required facts, already committed to the opposite stance or unsafe to mutate.

## 13.1 Role chemistry

Score groups, not only individuals:

```text
goal conflict
relationship history
knowledge asymmetry
personality contrast
power asymmetry
shared memories
social stakes
```

A proud debtor and proud creditor who used to be friends are usually a better scene than a timid debtor and indifferent creditor.

---

# 14. Knowledge asymmetry

Deliberately value situations where actors know different things.

```text
A knows truth
B believes lie
C knows A lied
D distrusts C
```

This naturally generates gossip, investigation, disagreement, manipulation, accusations and revelations.

The Director should treat interesting distributions of knowledge as dramatic opportunity.

---


## 14.5 Belief confidence and information provenance

Knowledge asymmetry only works if dialogue preserves **how an actor knows something** and **how strongly they believe it**.

The expression layer should receive more than a proposition ID.

```csharp
BeliefExpressionContext
{
    PropositionId;
    Confidence;
    Provenance;        // witnessed, told-by-X, document, rumor, inference...
    IsFirsthand;
    IsDeliberateLie;
    SpeakerCertainty;
}
```

This should materially affect wording.

```text
firsthand:
"I saw Haron sell it."

secondhand:
"His daughter says Haron sold it."

rumor:
"I heard Haron sold it."

inference:
"It had to be Haron."

deliberate lie:
semantic proposition intentionally differs from speaker belief
```

Information provenance is itself socially useful information. A player should be able to notice whether a claim is firsthand, hearsay, inference or obvious evasion.

Do not normalize every belief into equally confident declarative prose.

---

# 15. Secrets

Never generate “random secret.” A secret is:

```text
fact + actor knows fact + actor has reason to conceal it
```

Motives include legal danger, embarrassment, protecting someone, profit, taboo, promise, blackmail, status and fear.

Revealing a secret should have social or mechanical consequences.

---

# 16. Social practices

Inspired by Versu, use lightweight contextual norms.

## Shop
Pay, do not steal, merchant controls trade, haggling is acceptable.

## Funeral
Respect corpse, avoid obvious looting, reduced joking tolerance.

## Festival
Boasting and competition are acceptable; minor cheating may be culturally tolerated.

## Guild meeting
Rank matters; outsider access may be restricted.

## Home dinner
Host obligations, food sharing, guest etiquette, residents have standing.

Actions are interpreted through context. Theft during a funeral should not be socially equivalent to theft from an unattended warehouse.

---

# 17. Dialogue architecture

## 17.1 Semantic first

The system should request something like:

```text
SpeechAct: AskForRepayment
Speaker: Mira
Listener: Player
Proposition: Player owes 4000 orens
Emotion: controlled anger
Relationship: friendly but strained
Context: private shop conversation
```

not “write a debt line.”

Suggested schema:

```csharp
SpeechActInstance
{
    ActType;
    SpeakerId;
    ListenerIds[];
    PropositionIds[];
    TopicIds[];
    Intent;
    DesiredEffect;
    Emotion;
    Urgency;
    Publicity;
    SocialPractice;
    RelatedMemoryIds[];
    RelatedFactIds[];
}
```

Initial semantic acts:

Ask, Answer, Accuse, Deny, Admit, Confess, Threaten, Warn, Offer, Request, Refuse, Accept, Bargain, Apologize, Forgive, Insult, Praise, Boast, Gossip, RevealSecret, Deflect, ChangeSubject, Joke, Console, Challenge, Command, Beg, Introduce, Vouch, Remind, Correct, Speculate, Lie, Evade, Farewell, Greet.

---


## 17.5 Disclosure, conversational avoidance and information control

Knowing a fact does not imply willingness to disclose it.

Before answering a question or volunteering information, calculate disclosure pressure from:

```text
knowledge confidence
privacy
relationship
fear
loyalty
social practice
current emotion
leverage
legal risk
status risk
personality
speaker goals
```

Possible responses include:

```text
answer directly
answer partially
hedge
change subject
answer a different question
counter-question
joke to deflect
lie
remain silent
become hostile
end conversation
```

The semantic acts `Deflect`, `ChangeSubject`, `Evade` and `Lie` should therefore be outcomes of an explicit disclosure decision, not merely decorative dialogue alternatives.

Suggested conceptual result:

```csharp
DisclosureDecision
{
    FactId;
    WillDisclose;
    MaximumDetail;
    Strategy;        // direct, partial, evade, lie, refuse...
    ReasonTags[];
}
```

This is especially important for investigation, interrogation, blackmail, friendship and secrets.

## 17.6 Occupational and cultural vocabulary

Voice profiles should control more than punctuation and sentence length.

Characters should preferentially draw metaphors, nouns and comparisons from their lived context:

```text
farmer      → crops, weather, soil, livestock
merchant    → price, debt, margin, contracts
priest      → offerings, doctrine, gods, sin/taboo
thief       → risk, heat, marks, fences, guards
mage        → books, mana, experiments, artifacts
adventurer  → danger, Nefia, monsters, loot
```

This should be metadata-driven and subtle. Not every sentence needs occupational flavor.

## 17.7 Negative-space personality

What an actor **will not do** can be more recognizable than another positive trait.

Examples:

```text
never begs
never threatens children
never lies directly
never admits uncertainty
never discusses religion
never apologizes publicly
never accepts charity
never speaks badly of family
```

Represent strong behavioral prohibitions explicitly where useful. They should constrain action selection and dialogue realization unless extraordinary pressure causes a documented break in character.

---

# 18. Dialogue realization

Separate meaning from voice.

```csharp
DialogueFragment
{
    Id;
    SemanticAct;
    Position; // opener/core/modifier/closer
    ToneTags[];
    PersonalityTags[];
    RequiredFacts[];
    ForbiddenFacts[];
    RelationshipRange;
    EmotionRange[];
    Formality;
    WeirdnessLevel;
    TopicTags[];
    RepetitionGroup;
    Cooldown;
}
```

A line can be assembled from:

```text
opening
+ core semantic statement
+ personality modifier
+ relationship callback
+ context modifier
+ optional quirk fragment
+ closing
```

Not every line needs every slot.

Example intent: `ASK_FOR_REPAYMENT`.

Core variants:

```text
“You owe me {amount}.”
“There’s still the matter of {amount}.”
“I need the money back.”
```

Proud modifier:

```text
“I won’t beg for what is already mine.”
```

Timid modifier:

```text
“I don’t want trouble.”
```

History callback:

```text
“After what I did for your brother, I expected better.”
```

Context modifier:

```text
“Not here. The guards are watching.”
```

Quirk:

```text
“And don’t pay me in furniture again.”
```

The semantic meaning stays stable while expression varies.

---

# 19. Voice profiles

```csharp
VoiceProfile
{
    SentenceLength;
    Formality;
    Directness;
    Hedging;
    Sarcasm;
    MetaphorUse;
    Swearing;
    DirectAddressFrequency;
    EllipsisUse;
    ExclamationUse;
    WeirdnessTolerance;
    TopicFixations[];
    AvoidedTopics[];
}
```

Voice constrains fragment selection. It does not create the underlying meaning.

---

# 20. Dialogue scales

Use four scales:

**Micro-lines:** “Maybe.” “No.” “Who told you that?”

**Characterized fragments:** “Not for that price.” “Not while the guards are here.”

**Storylet lines:** “You accuse me in my own shop?”

**Signature lines:** rare lines strongly tied to recurring identity, quirk or history.

Most authored content should live in the first three categories.

---

# 21. Repetition control

Track recent use by fragment ID, repetition group, sentence opener, semantic act, metaphor family, weirdness motif and emotional cadence.

The player should not hear “I need you to...” ten times in one town.

---

# 22. What makes Elin/Elona weird

The useful tonal formula is:

```text
ORDINARY HUMAN PROBLEM
+ ONE ABSURD PREMISE ACCEPTED AS NORMAL
+ REAL MECHANICAL CONSEQUENCE
+ UNDERSTATED RESPONSE
```

The humor comes from the world mechanically accepting the absurdity, not from every NPC announcing that it is funny.

## 22.1 Weirdness taxonomy

**Bureaucratic absurdity:** impossible permits, bizarre guild law, legal classifications, inheritance technicalities.

**Biological absurdity:** mutation, bizarre diet, eggs, corpses, body-part logic.

**Religious absurdity:** odd offerings, divine loopholes, conflicting doctrine.

**Domestic absurdity:** sisters, pets, furniture, food, household grudges.

**Criminal absurdity:** bizarre contraband, ridiculous blackmail, criminal honor codes.

**Economic absurdity:** commodity bubbles in worthless objects, strange scarcity, merchant fixation.

**Adventurer absurdity:** heroic overreaction, lethal incompetence, pointless danger.

**Cosmic absurdity:** rare; use sparingly.

## 22.2 Weirdness budget

```text
0 mundane
1 one odd detail
2 distinctly Elin
3 absurd premise central
4 rare fever-dream event
```

Most content should remain 0–2.

## 22.3 Reactions to weirdness

The same absurdity should reveal personality.

Speaking cow:

```text
Pragmatist: “It still gives milk.”
Zealot: “Ask which god did it.”
Merchant: “Can it negotiate?”
Coward: “I’m moving.”
Scholar: “Don’t feed it anything.”
Farmer: “It has always been opinionated.”
```

## 22.4 Tone bible

1. Characters usually treat absurd mechanics sincerely.
2. Do not explain the joke.
3. Mundane concerns coexist with cosmic nonsense.
4. Dark and silly material can coexist.
5. Mechanics are allowed to create the punchline.
6. Dialogue is often drier than the event.
7. Avoid excessive modern meme/reference humor.
8. Not every NPC is eccentric.
9. Weird traits are sticky.
10. Callbacks are funnier than endless new jokes.

---

# 23. Character weirdness distribution

Conceptual target:

```text
~55% mostly ordinary
~25% noticeably distinctive
~15% weird
~5% unforgettable freak
```

Tune through playtesting. Ordinary people create contrast.

---

# 24. Callback system

When an event creates reusable narrative material, store callback hooks.

```csharp
CallbackHook
{
    EventId;
    Tags[];
    RelatedActorIds[];
    RelatedObjectIds[];
    RelatedLocationIds[];
    EmotionalWeight;
    Embarrassment;
    Publicity;
    ValidContexts[];
}
```

Useful hooks include embarrassment, promise, injury, nickname, scandal, lost object, weird incident, relationship change and location association.

Callbacks should be opportunistic, not spammed.

---

# 25. Procedural humor through continuity

Do not constantly generate new jokes.

Example:

```text
festival registration error
→ NPC accidentally marries chicken
```

Later this can affect gossip, taxes, inheritance, relationship dialogue, the chicken's death or remarriage. The absurdity gains weight because it becomes history.

---

# 26. Scene architecture

A scene is a player-facing presentation of one or more storylets.

```csharp
NarrativeScene
{
    SceneId;
    LocationId;
    Participants[];
    ActiveStorylets[];
    SocialPractice;
    EntryConditions[];
    ExitConditions[];
    BeatState;
    PlayerOptions[];
    AmbientBarks[];
}
```

Scenes should remain interruptible and world-aware, not turn Elin into a separate visual novel.

---

# 27. Beat system inspired by Façade

Use lightweight beats rather than giant dialogue trees:

```text
OPEN
ESTABLISH_TOPIC
CLAIM
CHALLENGE
REVEAL
ESCALATE
PLAYER_INTERVENTION
REACTION
RESOLUTION
AFTERMATH
```

Example `PublicAccusation`:

```text
OPEN
→ CLAIM
→ accused responds
→ player may intervene
→ evidence may be shown
→ defender may speak
→ escalation or withdrawal
→ aftermath
```

The player can disrupt the sequence. Not every beat must occur.

---

# 28. Scene interruption

Because Elin is a sandbox, a scene must tolerate:

- actor leaves;
- actor dies;
- combat begins;
- player leaves;
- another NPC enters;
- inventory changes;
- crime occurs;
- new evidence is produced;
- affinity changes.

Before important beats:

```text
revalidate participants
revalidate facts
revalidate available actions
```

Never assume the world still matches scene-start state.

---


## 28.5 Conversation state and commitments

Scenes need short-term discourse memory in addition to long-term world memory.

Suggested transient state:

```csharp
ConversationState
{
    TopicsRaised[];
    ClaimsMade[];
    QuestionsAsked[];
    QuestionsUnanswered[];

    LiesTold[];
    ContradictionsExposed[];

    PromisesMade[];
    ThreatsMade[];
    SecretsRevealed[];

    SpeakerCommitments[];
}
```

This enables reactions such as:

```text
"You already asked me that."
"That's not what you said five minutes ago."
"So now you're saying you never saw him?"
"You promised you wouldn't tell her."
```

A conversational commitment should become a durable world event when it matters outside the immediate scene, for example a promise, confession, threat, accusation or agreement.

This layer is important for Façade-like reactivity without requiring a fully authored dialogue tree.

---

# 29. Player options

Options derive from semantic state, known facts, inventory, skills, relationships, guild/faith/Home authority, current actors and actual world affordances.

Examples:

```text
Ask what happened
Show the ring
Call her a liar
Offer to pay the debt
[Merchant Guild] Ask to see the contract
Threaten to tell the guards
Keep quiet
Leave
```

Prefer natural language over exposed raw probabilities.

---

# 29.5 Contextual interaction and nested intent menus

The action library is larger than the menu the player should see. The player-facing projection should organize available actions through this grammar:

```text
Interaction surface
-> Intent family
-> Contextual action
-> Subject, only when the action needs one
```

Examples:

```text
Talk
+-- Inquire
|   +-- Ask what they know
|   +-- Ask about <known person>
|   +-- Ask about <known event>
|   +-- Ask about <known object>
+-- Persuade
|   +-- Reassure
|   +-- Appeal to reason
|   +-- Offer incentive
+-- Aggressive
|   +-- Intimidate
|   +-- Threaten
|   +-- Extort
+-- Deception
|   +-- Lie
|   +-- Bluff
|   +-- Impersonate
+-- Other
    +-- Report
    +-- Expose secret
    +-- Leave
```

This is an illustrative taxonomy, not a hardcoded permanent tree. The categories are stable enough for the player to learn as interaction grammar:

```text
Inquire    = information gathering
Persuade   = cooperative influence
Aggressive = coercion
Deception  = dishonest manipulation
```

The hierarchy should normally be no deeper than surface -> family -> action/subject. Avoid file-browser trees such as `Talk -> Inquire -> People -> Criminal contacts -> Vurl -> Ask about employment`.

Collapse unnecessary levels:

```text
1 relevant option:   show it directly
2-4 relevant options: show them flat
5+ relevant options: introduce a semantic submenu
```

Families with no relevant actions disappear. If there is no meaningful aggressive action against the target, `Aggressive` is absent. If there is nothing specific to ask, `Inquire` may disappear or contain only a broad option like `Ask what they know`.

Filter every option by player knowledge, current target, actor state, thread state, inventory, relationship, guild/faith/Home authority, current world state and concrete affordance. Unknown subjects must never appear as menu choices. The menu can evolve as the player learns:

```text
Talk -> Inquire -> Ask what happened
Talk -> Inquire -> Ask about Vurl
Talk -> Inquire -> Ask about Dassen
```

The subject step does not always need to be a separate menu. If only one subject is relevant, show `Ask about Vurl`; if several subjects are relevant, use a subject picker.

Non-social verbs belong on their natural world surfaces rather than being forced into Talk:

```text
Object
  Examine
    Inspect
    Read
    Identify
    Compare

Corpse
  Examine
    Inspect body
    Examine wounds
    Search belongings
    Identify substance

Location
  Investigate
    Search
    Search records
    Look for tracks
    Eavesdrop

Suspicious NPC
  Interact
    Talk
    Observe
    Follow
    Crime
      Pickpocket
      Plant evidence
      Sabotage
      Attack

Criminal contact
  Talk
    Services
      Forge
      Fence
      Smuggle
```

Option text should describe what the player means to do, not expose internal action ids. This is wrong:

```text
Talk
+-- SocialActions
+-- InvestigationActions
+-- CrimeActions
```

This is better:

```text
Talk
+-- Inquire
+-- Persuade
+-- Aggressive
+-- Deception
+-- Business
```

Every action must be classifiable for contextual projection without the UI maintaining a growing switch statement keyed on action ids. The storage mechanism may be metadata on `NarrativeAction`, a separate descriptor, or an affordance registry, but the property must hold:

```text
intimidate: surface = Talk, family = Aggressive
question:   surface = Talk, family = Inquire
lie:        surface = Talk, family = Deception
read:       surface = Examine, family = Study
forge:      surface = UnderworldService, family = Deception
```

The system may rank actions inside each family. Unavailable actions are normally hidden, except when the reason for unavailability itself communicates useful gameplay information. Projection is presentation only: selection must revalidate state before execution, because the world may change between showing a menu and resolving an action.

---

# 30. Dialogue and world actions coexist

Do not convert all gameplay into dialogue choices.

Preferred flow:

```text
NPC says archive is locked
→ player physically lockpicks archive
→ reads actual document
→ later dialogue recognizes that knowledge
```

Likewise, steal actual evidence, mine actual walls, fight actual actors, cook actual food, invest actual money and shelter actual NPCs at Home.

Dialogue contextualizes Elin mechanics. It should not replace them.

---

# 31. Scene discovery channels

Scenes can enter play through default talk, request board, direct approach, ambient barks, guild contacts, Home residents, world objects, overheard conversations, festivals, shops, crime observations and site entry.

The Director should prefer believable channels.

---

# 32. Player direction without marker dependence

Guide with information and consequence:

- explicit requests;
- dialogue leads;
- rumors;
- known locations;
- item provenance;
- visible absence;
- shop changes;
- guild information;
- witness statements;
- tracks/evidence;
- Chronicle memory.

The player should usually know enough to make a choice without being given one mandatory route.

---

# 33. Situation fingerprinting

Track recent experiential features:

```text
violent/social/economic/investigative
serious/mundane/absurd
urgent/slow
local/travel
new/recurring NPC
new/reused site
public/secret
request/situation/opportunity/event
high/low stakes
```

The Director should penalize repeated shapes.

---


## 33.5 Familiarity, novelty and narrative conservation

Procedural narrative should not maximize novelty.

Players need recognizable social grammar:

```text
"This is a dispute."
"This is a negotiation."
"This is someone hiding something."
```

They should not recognize:

```text
"This is quest template #17."
```

The desired mixture is:

```text
familiar human/social grammar
+
different causal context
+
different actors
+
different relationships
+
different mechanical affordances
+
occasional surprising complication
```

## 33.6 Narrative conservation

Prefer **deepening existing state** over creating more state.

Before inventing a new actor, secret, object, location or weird premise, ask whether an existing element can carry the development.

Conceptual creation cost:

```text
reuse existing fact             cost 0
reuse existing actor            cost 0
reuse existing location         cost 0

new interpretation              cost 1
new obligation                  cost 1
new rumor                       cost 1

new relationship                cost 2
new important object            cost 2

new significant NPC             cost 4
new persistent location         cost 5
new major secret                cost 5
new weird premise               cost 6
```

These are illustrative weights, not final tuning.

The Director should prefer candidates that create more dramatic/player value from less invented state.

This is one of the strongest defenses against procedural garbage:

> **make the world deeper before making it larger.**

## 33.7 Escalation ownership

The Director may identify dramatic opportunity, but it should not fabricate arbitrary escalation solely because "the story needs excitement."

Every meaningful escalation should be attributable to:

- an actor goal and action;
- a vanilla-world event;
- an existing systemic pressure;
- a direct consequence of prior state.

Bad:

```text
thread is quiet
→ Director invents kidnapping
```

Good:

```text
creditor becomes desperate
→ hires intimidation
→ intimidation goes wrong
→ disappearance follows
```

Drama should have an owner and a cause.

---

# 34. Experience topology

Different nouns can still produce the same experience.

Repeated topology:

```text
request board
→ talk to one NPC
→ new dungeon
→ kill target
→ money
```

Treat this as repetition even if the nouns differ.

Diversity should exist in entry channel, required knowledge, mechanical routes, scene structure, site use, relationships, resolutions and consequence types.

---

# 35. Anti-template testing

Generate many synthetic runs and measure:

- causal skeleton repetition;
- role repetition;
- storylet repetition;
- dialogue opener repetition;
- weirdness-category repetition;
- solution-family repetition;
- reward repetition;
- emotional-tone repetition;
- site repetition.

If too many situations collapse to one topology, improve the building blocks before adding more content.

---

# 36. Quality-diversity selection

Do not always choose the highest raw drama score. Prefer high-quality candidates that also occupy underrepresented niches in recent play.

Example:

```text
recent: violent, urgent, new NPC / violent, travel / secret social
candidate A: violent kidnapping
candidate B: mundane festival rivalry with recurring NPC
```

Candidate B may be better because it improves the player's overall experience distribution.

---


## 36.5 Development layer

`Event`, `Development`, `NarrativeThread`, `Storylet` and `Scene` should remain distinct concepts.

```text
Event
= something happened

Development
= that event/state created unresolved pressure

NarrativeThread
= persistent causal continuity around related developments

Storylet
= reusable dramatic/social pattern that can express or react to a development

Scene
= one concrete player-facing presentation of one or more storylets
```

Suggested representation:

```csharp
Development
{
    Id;
    OriginEventIds[];

    SubjectIds[];
    LocationIds[];

    PressureTags[];
    Stakes[];
    OpenQuestions[];

    AffectedActors[];
    CandidateResponders[];

    Visibility;
    Urgency;
    Persistence;

    PossibleStoryletTags[];
    PossibleWorldActions[];

    State;
}
```

A Development does not need to become a scene, quest or player-facing event. It is the bridge between raw causal state and dramatic opportunity.

This boundary prevents the Storylet system from becoming a hidden quest generator.

---

# 37. Procedural expression pipeline

Recommended exact pipeline:

```text
1. World event occurs.
2. Append authoritative event.
3. Update facts.
4. Update actor-local knowledge.
5. Build interpretations.
6. Update goals/emotions/obligations.
7. Detect developments.
8. Detect storylet opportunities.
9. Cast temporary roles.
10. Score role chemistry.
11. Choose presentation channel.
12. Instantiate scene or ambient interaction.
13. Generate semantic speech acts.
14. Select fragments using voice/personality/context.
15. Apply repetition and weirdness constraints.
16. Render through Elin.
17. Player/NPC acts.
18. Revalidate state.
19. Resolve through Elin/native/hybrid mechanics.
20. Record consequences.
21. Create memories/callback hooks.
22. Return to simulation.
```

---

# 38. Development phases

## Phase A — Personality foundation
Implement personality dimensions, voice profile, sensitivities, contradiction, problem-solving style and topic preferences. Prove different personalities choose different responses to the same problem before writing rich dialogue.

## Phase B — Semantic speech acts
Implement a small core: Ask, Answer, Accuse, Deny, Admit, Request, Refuse, Threaten, Apologize, Gossip.

## Phase C — Metadata-driven realizer
Implement fragment schema, compatibility filtering, selection, repetition control and basic voice profiles.

## Phase D — Storylet engine
Implement five strong storylets: PublicAccusation, PrivateConfrontation, RequestForHelp, Confession, Gossip. Exercise them on the current petty theft scenario.

## Phase E — Casting and chemistry
Implement temporary roles and pair/group scoring. The same theft should generate structurally different social scenes.

## Phase F — Elin tone layer
Implement weirdness budget, motif taxonomy, dry realization rules, sticky quirks and callback hooks. Do not start with a giant joke library.

## Phase G — Social practices
Implement shop, public street, Home and guild contexts first.

## Phase H — Scene/beat orchestration
Add lightweight beats and interruption/revalidation.

## Phase I — Long-term character arcs
Add slow personality shifts, recurring topics, obligation history and callbacks.

## Phase J — Director integration
Only after enough storylets and simultaneous developments exist to create a real pacing problem.

---

# 39. Canonical test: 100 thefts

Before broadening, generate the same objective theft 100 times with varied thief, victim, witness, personality, relationships, motives, secrets, quirks, knowledge distribution, social setting and weirdness budget.

The result should not feel like “Who stole the item?” ×100.

It should include private confrontation, public accusation, bribery, avoidance, false accusation, family coverup, opportunistic blackmail, forgiveness, revenge, bizarre restitution, guild involvement, player exploitation and cases where nobody asks the player for anything.

If one objective fact can generate this breadth, the expression layer is ready for more archetypes.

---

# 40. Dialogue content production strategy

Do not author full conversations for every case.

Suggested growth:

```text
First 500 fragments:
core speech acts + neutral voice

Next 500:
relationship, emotion, formality variation

Next 500:
personality and sensitivity variants

Next 500:
Elin-specific occupational/context/weirdness variation

Ongoing:
callbacks, rare signature lines, scene-specific fragments
```

Long-term value lies in many small tagged fragments, not hundreds of rigid scenes.

---


## 40.5 Mature building-block target

The mature library should aim for breadth in reusable primitives rather than thousands of quests.

Conceptual long-term target:

```text
~40–60 shared player/NPC actions
~30–50 social storylets
~20–30 investigation/evidence developments
~20 economic developments
~15 travel developments
~15 Home/community developments
~20 conflict developments
~20 mundane/absurd micro-events
~15 location grammars
hundreds to thousands of tagged dialogue fragments
dozens of motivations, sensitivities, contradictions and quirks
```

These are direction-setting numbers, not release requirements.

The goal is a large combinatorial surface built from understandable, testable pieces.

---

# 41. Authoring tools

Dialogue and storylets should eventually be data-authored without simulation-code edits.

Illustrative fragment:

```yaml
id: debt_request_proud_01
act: AskForRepayment
position: core
requires:
  emotion: [anger]
  personality:
    proud: ">0.6"
text:
  - "I won't beg for what is already mine."
```

Illustrative storylet:

```yaml
id: public_accusation
roles:
  accuser:
    requires_belief: target_caused_harm
  accused: {}
  observer:
    optional: true
beats:
  - open
  - claim
  - response
  - intervention
  - aftermath
```

Exact serialization can be decided later. Designers must be able to add expression content without touching core simulation code.

---

# 42. Debug tooling

The character/dialogue inspector should expose:

```text
NPC identity
personality
voice
sensitivities
contradictions
quirks
emotion
goals
known facts
false beliefs
interpretations
problem-solving weights

storylet candidates
casting score
role chemistry
selected storylet
current beat

speech act
candidate fragments
rejected fragments + reason
selected fragment
repetition penalties
weirdness score
callback used
social practice
available actions
```

Without this, procedural narrative tuning will become unmanageable.

---

# 43. Save and persistence rules

Persist durable personality, voice, quirks, sensitivities, contradictions, topic preferences, memories, obligations, callback hooks and long-term shifts.

Do not persist transient candidate lists, runtime pointers or reconstructible caches.

Scene save/load should be conservative: reconstruct from authoritative world state rather than insisting an exact pre-save sentence sequence resume.

---


## 43.5 Memory decay and semantic garbage collection

Long saves cannot preserve every minor rumor and emotional interpretation at full fidelity forever.

Different information classes need different retention rules.

Suggested policy:

```text
major historical fact
→ permanent

relationship-defining memory
→ permanent or compressed summary

major promise/debt/obligation
→ persists until resolved/superseded

important public scandal
→ long-lived, may become legend/history

minor embarrassment
→ decays unless reinforced

ordinary rumor
→ decays, mutates or becomes archival

temporary emotional interpretation
→ expires

low-value conversational detail
→ discard after relevance window
```

Compression should preserve causal meaning.

Example:

```text
12 low-level memories:
player repeatedly helped shop during shortages

→ compressed durable memory:
"player reliably supported my business during hardship"
```

Garbage collection must never delete information required by an active thread, obligation, relationship explanation or persisted callback.

---

# 44. Elin presentation integration

Use Elin's existing grammar:

**Barks:** ambient personality, rumors, complaints, reactions.

**Default talk:** mundane topics, relationship color, soft leads.

**Drama:** structured scenes, choices, confrontations and high-context conversation.

**Request board:** explicit jobs only.

**Journal / Chronicle:** player memory of known state.

**World:** primary source of physical truth.

The player should not feel that Brilliant Questing replaces Elin with a separate narrative UI.

---

# 45. Performance

Do not generate dialogue for every NPC every tick.

```text
off-screen → coarse goals/decisions
on awareness → development/storylet candidate
on presentation → lazily realize dialogue
```

Dialogue realization is presentation work, not simulation work.

---

# 46. Optional LLM policy

An optional LLM may eventually paraphrase authoritative semantic acts, but it must never decide what happened, who knows what, actor motivation, available actions, success/failure or consequences.

The authored deterministic offline realizer remains the baseline.

---

# 47. Failure modes to reject

Do not build:

- one personality adjective attached to boilerplate;
- random quirks on every NPC;
- five global voice archetypes;
- dialogue that exposes hidden truth;
- personality that changes prose but not decisions;
- scenes that ignore actual world actions;
- rigid plots that freeze NPC behavior;
- every event becoming conversation;
- every NPC being witty;
- constant absurdity;
- modern meme spam;
- exposition dumps;
- generated prose without semantic grounding;
- noun-swapped quest templates;
- permanent `QuestGiver` roles;
- LLM text as authoritative state.

---


## 47.5 Player/NPC action symmetry

When practical, player and NPC narrative behavior should resolve through the same action definitions.

Avoid:

```text
PlayerBribe()
NpcBribe()
PlayerThreaten()
NpcThreaten()
```

Prefer:

```text
NarrativeAction: Bribe
actor = player or NPC

NarrativeAction: Threaten
actor = player or NPC
```

Shared actions may include:

- Bribe
- Threaten
- Accuse
- Ask
- Lie
- Steal
- RevealSecret
- SpreadRumor
- PayDebt
- OfferHelp
- Attack
- Negotiate
- Apologize

The same precondition/consequence vocabulary should be reused wherever Elin mechanics allow it.

This is the foundation for autonomous NPC drama. NPC autonomy should not be a parallel scripted simulation that only resembles player interaction.

---

# 48. Milestones

**Milestone 1 — Behavioral identity:** the same problem causes five personalities to choose five meaningfully different responses.

**Milestone 2 — Recognizable voice:** testers can sometimes identify recurring NPCs from dialogue without seeing the name.

**Milestone 3 — Social scenes:** the theft scenario can produce at least five structurally distinct scenes.

**Milestone 4 — Elin tone:** absurd events feel like Ylva rather than generic procedural comedy.

**Milestone 5 — Callbacks:** later scenes naturally reuse earlier generated history.

**Milestone 6 — Non-player drama:** NPCs confront, bargain, accuse, forgive or escalate without waiting for the player.

**Milestone 7 — Long-save identity:** recurring generated NPCs feel like people with histories rather than recycled content.

---

# 49. North-star example

A butcher named Tilda is proud, superstitious, generous, terrified of embarrassment and hates mushrooms. A farmer named Haron is warm, greedy, gossipy and conflict-avoidant.

Haron accidentally sells Tilda's prized goat and lies that it escaped. His daughter saw the sale. The buyer slaughtered the goat. The collar now physically exists in another butcher's inventory.

No quest is generated.

Tilda casually asks the player if they have seen her goat.

Later, Haron's daughter trusts the player enough to say:

> “Dad didn't lose it.”

A `PublicAccusation` storylet eventually casts Tilda and Haron because Tilda's suspicion rises.

> Tilda: “You were watching her.”  
> Haron: “I watch twenty goats.”  
> Tilda: “Mine wore a bell.”  
> Haron: “So do priests.”

The last line is not random comedy. It is selected because Haron is evasive and uses playfulness under stress.

The player later finds the collar. Tilda's fear of embarrassment now matters more than anger because she has publicly blamed wolves for days. She asks privately:

> “Don't tell anyone yet.”

The goat had also been registered as a Home worker through some absurd bureaucratic edge case. Its death therefore creates a real vacancy inherited by a chicken. Nobody considers this especially strange.

The player can expose Haron, protect him, demand repayment, blackmail him, tell Tilda privately, spread the story, exploit the vacant Home role or ignore everything.

Later, the “goat worker” scandal can recur in taxes, guild gossip, inheritance or a festival.

This is the target: authored-feeling character drama assembled from real state, persistent people, Elin mechanics and reusable expressive systems.

---


## 49.5 Inspiration translated into implementation rules

The cited inspirations should remain design references, not feature checklists.

**Wildermyth:** borrow casting, opportunistic variation, recurrence and character attachment. Do not reduce the system to authored event cards.

**Façade:** borrow dramatic beats, short-term discourse state, interruption and reactive scene structure. Do not force Elin into a tightly controlled drama arc.

**Versu:** borrow social practices, actor-local motivations and context-sensitive norms. Keep the implementation lightweight enough for a sandbox RPG.

**Talk of the Town:** borrow semantic-to-language separation and metadata-driven authored realization. Preserve Elin's concise voice rather than producing verbose grammar demonstrations.

**Shadows of Doubt / Dwarf Fortress:** borrow causal world state, selective simulation and stories that emerge from persistent actors. Do not require full-fidelity simulation of every NPC.

The synthesis remains:

```text
causal world
+ motivated actors
+ reusable social microstructures
+ dynamic casting
+ semantic dialogue
+ authored voice realization
+ Elin mechanics
+ persistent callbacks
```

---

# 50. Final implementation doctrine

1. Characters are persistent people, not narrative functions.
2. Personality changes choices before wording.
3. Voice is separate from personality.
4. Dialogue expresses semantic state; it does not create state.
5. Storylets dramatize facts; they do not author truth.
6. Temporary roles are cast dynamically.
7. Role chemistry matters.
8. Knowledge asymmetry is a feature.
9. Secrets require motives.
10. Social context changes interpretation.
11. The same event means different things to different people.
12. Mundane dialogue is necessary.
13. Weirdness is structured, sparse and sincere.
14. Elin absurdity is usually one impossible premise treated normally.
15. Quirks are rare and sticky.
16. Callbacks beat endless novelty.
17. Existing actors beat disposable new actors.
18. Existing history beats random backstory.
19. The player is not required for drama to occur.
20. Scenes tolerate interruption.
21. Elin world interactions remain authoritative.
22. Repetition is measured by experiential topology, not noun diversity.
23. Quality-diversity influences selection.
24. Debugging must explain every procedural choice.
25. Authored offline realization remains the baseline.
26. A successful system should feel authored without requiring authored plots.
