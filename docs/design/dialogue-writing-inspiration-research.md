# Brilliant Questing — Dialogue Writing Inspiration Research

**Status:** Writing and content-design reference for authored dialogue. Not an architecture.
**Date:** 5 September 2026
**Primary references:** Final Fantasy IX, Final Fantasy XII, Disco Elysium, Pathologic 2, Final Fantasy VII, Tactics Ogre / FFT: War of the Lions
**Secondary references:** Pentiment, Morrowind, Wildermyth, Baldur's Gate 3

**Relationship to other documents.** This supplements `character-dialogue-system.md`; it does not
replace it. `character-dialogue-system.md` owns the procedural character/dialogue architecture and
the semantic model — `SpeechAct`, disclosure, belief authority, fragment realization, voice
profiles, repetition control, occupational vocabulary, callbacks, weirdness budgeting. This document
owns researched prose, voice, register, dialogue-authoring and inspiration guidance.
`content-pipeline.md` owns how authored data enters the project and is validated.
`implementation-roadmap.md` owns ordered implementation work. `agent/decisions.md` owns durable
architectural decisions.

**When to read it.** Only when the task at hand is prose or content quality: writing or auditing
dialogue fragments, voice/idiolect marks, storylet prose, callbacks, relationship/emotion wording,
occupational vocabulary, narrative content generation, or a dialogue-quality audit. An ordinary
implementation task still starts at `AGENTS.md`, the relevant roadmap step, and the code and tests.
Do not preload this document.

**Nothing here is a runtime quantity.** The influence weights in §1.1 are editorial guidance for a
human or agent deciding how to write a line. They are not selection probabilities, not quotas, and
must never be compiled into anything.

---

# 0. Executive conclusion

Brilliant Questing should not imitate the surface prose of any one reference game.

The strongest composite target is:

- **Disco Elysium:** make apparently nonessential dialogue reveal worldview, history, and highly
  specific local state; use micro-reactivity so the world notices small prior choices.
- **Pathologic 2:** preserve ambiguity, let characters color information with attitude, and respect
  the player's attention by making almost every line do more than one job.
- **Tactics Ogre / Final Fantasy Tactics:** build diction from the setting outward. Register,
  vocabulary, social hierarchy, and institutional language should feel native to the fictional
  society rather than like generic modern English dressed in fantasy nouns.
- **Pentiment:** allow education, occupation, class, and social experience to leave persistent
  linguistic traces without reducing characters to gimmicks.
- **Morrowind:** exploit conditioned generic dialogue. A small pool becomes convincing when answers
  are filtered through location, faction, trade, status, knowledge, and circumstance, with specific
  responses preferred over generic fallbacks.
- **Wildermyth:** procedural stories should emphasize ongoing character identities rather than
  procedural plot complexity; vividness and brevity matter more than decorative prose.
- **Baldur's Gate 3:** reward unusual player histories with recognition, let stable characters react
  differently to the same event, and test dialogue in context rather than trusting prose that only
  reads well in a file.

The combined lesson is not "write more elaborate dialogue." It is:

> **Give ordinary lines more specific causes. Give important lines more specific voices. Give
> memorable lines stricter reasons to exist.**

BQ's current architecture is unusually well suited to this because semantic truth, actor-local
belief, intention, disclosure, context, voice, callbacks, and final wording are already separate
concerns.

---

# 1. Canonical influence blend and weighting

This guide does not ask Brilliant Questing to imitate one game's prose. Each reference owns a
different writing problem. The blend should preserve those differences instead of averaging them
into one prestige-RPG voice.

## 1.1 Recommended influence weights

These are design weights, not quotas, and never runtime percentages.

| Influence | Weight | Primary contribution | Do not copy wholesale |
|---|---|---|---|
| **Final Fantasy IX** | 18% | cast readability; warmth; sincerity; comic/tragic range; persistent individual voice | theatricality or catchphrase caricature everywhere |
| **Final Fantasy XII / Ivalice** | 18% | world-native register; institutional language; class/faction texture; compressed elevated speech | universal archaism or ornate ordinary speech |
| **Disco Elysium** | 17% | worldview leakage; micro-reactivity; surplus human detail; character-specific attention | every line being brilliant, surreal, philosophical, or quotable |
| **Pathologic 2** | 15% | perspectival truth; dialogue as conflict; sincere cultural strangeness; ambiguity under information pressure | obscurity for its own sake or relentless metaphor |
| **Final Fantasy VII** | 12% | immediacy; conversational contrast; economical characterization; roughness beside tenderness | dated stereotype-based dialect or slang-as-identity |
| **Tactics Ogre / FFT: War of the Lions** | 10% | laconic dramatic register; political vocabulary; class without caricature; direct emotion under formal diction | uniformly elevated speech or ornamental wordplay |
| **Pentiment** | 4% | education/class/occupation as subtle linguistic fingerprints | encoding class into every sentence |
| **Morrowind** | 3% | shared cultural and institutional vocabulary | topic-menu stiffness and repeated exposition |
| **Wildermyth** | 2% | authored procedural economy; traits affecting variants; character development over procedural plot | recognizable template voice |
| **Baldur's Gate 3** | 1% | relationship-conditioned delivery; recognition of history; scene continuity | cinematic verbosity or dependence on acting |

The practical center is:

> **FF9 character readability + FF12 world register + Disco worldview/reactivity + Pathologic
> perspectival information + FF7 conversational immediacy.**

Tactics Ogre/FFT restrains elevated dialogue. The remaining references are specialist tools.

## 1.2 Final Fantasy VII — social friction and immediacy

FFVII's useful quality is not its period slang. It puts incompatible speech energies beside each
other. Characters can be blunt, guarded, awkward, theatrical, tender, angry, or unserious without
the scene forcing them into one literary register.

For BQ:

- allow fragments, interruptions, plain emotion, rough edges, and socially imperfect wording;
- let vulnerability emerge through an established voice rather than replacing it;
- preserve contrast between speakers in the same scene;
- do not equate grammatical polish with writing quality;
- make memorable wording selective.

**Role in the blend:** FFVII is the strongest counterweight to BQ becoming too polished.

## 1.3 Final Fantasy IX — identity that survives tonal change

FFIX is the primary cast-differentiation reference. Its characters remain legible across comedy,
embarrassment, grief, romance, philosophy, and ordinary party talk. Voice is more than verbal tics:
priorities, confidence, social assumptions, and emotional habits remain recognizable as tone
changes.

For BQ:

- persistent NPCs should remain themselves while angry, affectionate, frightened, or ashamed;
- relationship and emotion modify voice rather than replace it;
- important recurring characters need only one or two persistent linguistic tendencies;
- permit unprotected sincerity;
- keep class/status contrasts readable rather than turning them into caricature.

**The FFIX test:** if names disappear, can the player still distinguish important recurring speakers
across both comic and serious scenes?

## 1.4 Final Fantasy XII — the world has a voice

FFXII's English localization deliberately rebuilt Ivalice for English-speaking players. Alexander
O. Smith and Joseph Reeder used register/accent distinctions to support political and social groups,
emphasized transparent writing that embeds information naturally, and gave non-dialogue surfaces
such as the bestiary their own registers.

For BQ:

- organizations, occupations, regions, and institutions may share lexical fields without sharing
  personalities;
- guards can think in watch/boundary/order/procedure; merchants in price/debt/stock/exchange;
- elevated speakers should compress thought rather than merely add archaic words;
- world terminology should sound possessed rather than explained;
- conversation, case notes, chronicles, rumor, and provenance may use different prose registers.

**Role in the blend:** FFXII is the strongest reference for setting-wide linguistic cohesion.

## 1.5 Tactics Ogre / FFT — elevated language under restraint

Alexander O. Smith described Tactics Ogre's localization target as laconic and sparse, with subtle
characterization rather than easy category voices; the team later filtered dialogue by individual
character to check consistency.

For BQ:

- prefer short, decisive political language over ornamental pseudo-archaism;
- distinguish background through assumptions, rhetoric, and word choice before phonetic dialect;
- audit corpus output by speaker profile as well as fragment pack;
- institutional actors should sound as though they understand their institution from inside;
- keep direct emotion beneath formal language.

**Rule:** elevated register is strongest when the sentence is carrying conflict, not displaying
vocabulary.

This influence prevents FFXII-inspired register from becoming purple prose.

## 1.6 Disco Elysium — surplus humanity and selective attention

Disco's most transferable principle is that NPCs contain material that does not visibly serve their
quest function. Justin Keenan describes dialogue as particular, strange, true to character, and
supported by industrial-scale micro-reactivity to small player choices.

BQ translation:

- recurring NPCs notice small authoritative facts about the player and shared history;
- people have interests, grudges, tastes, memories, and opinions outside their mechanical role;
- callbacks to minor events create the feeling that the world listened;
- personality changes what part of an event attracts attention;
- some lines may reveal a person without advancing a thread.

But BQ should borrow **the structure of attention**, not Disco's constant rhetorical fireworks.

**Rule:** Disco determines what a person notices; FFVII/FFIX usually determine how economically they
say it.

## 1.7 Pathologic 2 — information is spoken from somewhere

Ice-Pick Lodge rebuilt Pathologic's text for the remake and explicitly described dialogue as
conflict rather than monologue. Its characters can lie, err, speak metaphorically, and treat
unfamiliar cultural premises as ordinary.

For BQ:

- witnessed fact, rumor, inference, suspicion, and self-serving account should not sound identical;
- answers can expose what a speaker values or fears;
- uncertainty should be concrete rather than generic;
- local cultural assumptions should be spoken sincerely without narrator-like explanation;
- disagreement often comes from incompatible interpretation rather than a single wrong quest answer;
- genuine uncertainty should remain when simulation state does not justify certainty.

**Rule:** Pathologic determines the angle from which information is understood; BQ semantics
determine what the speaker is entitled to assert.

Do not use Pathologic as permission for unsupported vagueness.

## 1.8 Pentiment — biography leaves fingerprints

Pentiment makes education, profession, literacy, and social station perceptible while grounding
drama in ordinary community relationships.

For BQ:

- education can affect vocabulary and rhetorical structure;
- occupation supplies conceptual metaphors sparingly;
- social position can affect what a speaker assumes may be said directly;
- plausible expertise remains separate from actual knowledge;
- the same occupation must support many personalities.

Pentiment is a modifier, not the base prose style.

## 1.9 Morrowind — culture through shared vocabulary

Morrowind demonstrates how shared names for institutions, practices, places, factions, and social
categories can make generic speech feel embedded in a culture.

For BQ:

- maintain small controlled lexicons by observed occupation/faction/culture;
- let terminology imply familiarity;
- avoid defining concepts speakers expect listeners to know;
- vary surrounding syntax so shared terminology does not create shared voice.

Morrowind contributes world vocabulary, not sentence construction.

## 1.10 Wildermyth — authored procedural economy

Wildermyth explicitly focuses procedural storytelling on character development rather than
procedural plot, with traits and relationships affecting authored dialogue variants.

Use it for:

- conditional authored variants;
- traits selecting among semantically valid expressions;
- long-lived relationships personalizing later scenes;
- small expressive units safe to recombine.

Its warning is template visibility. BQ's richer semantic state, idiolect, expression history, and
callbacks should specifically prevent different characters from revealing the same authored skeleton
too quickly.

## 1.11 Baldur's Gate 3 — scene continuity

BG3 is a minor reference because BQ cannot depend on bespoke cinematics, performance, or huge
branch-specific scripts. Its useful lesson is continuity:

- acknowledge consequential player history;
- relationship changes alter delivery without replacing identity;
- reactions make sense in sequence, not only as isolated valid lines;
- important emotional beats receive enough space to land.

BQ should produce those effects through persistent state and callbacks.

## 1.12 Combined authoring equation

When writing a fragment, ask which influence owns the current problem:

```text
SEMANTIC AUTHORITY              = Brilliant Questing
WHAT THE SPEAKER NOTICES        = Disco Elysium
HOW INFORMATION IS INTERPRETED  = Pathologic 2
WHO SURVIVES ACROSS MOODS       = Final Fantasy IX
IMMEDIATE CONVERSATIONAL ENERGY = Final Fantasy VII
WORLD / INSTITUTION REGISTER    = Final Fantasy XII
POLITICAL COMPRESSION           = Tactics Ogre / FFT
BIOGRAPHICAL LINGUISTIC COLOR   = Pentiment
SHARED CULTURAL LEXICON         = Morrowind
PROCEDURAL VARIANT DISCIPLINE   = Wildermyth
SCENE / RELATIONSHIP CONTINUITY = Baldur's Gate 3
```

## 1.13 Situational weight shifts

| Situation | Increase | Decrease |
|---|---|---|
| ordinary town conversation | FFVII, FFIX, Pentiment | FFXII, Disco |
| intimate recurring-NPC scene | FFIX, Disco, BG3 | Morrowind |
| testimony / rumor / uncertain claim | Pathologic, Disco | BG3 |
| guard/guild/institutional business | FFXII, Tactics Ogre, Morrowind | Disco |
| political confrontation | Tactics Ogre, FFXII, Pathologic | Wildermyth |
| comic relief | FFIX, FFVII | Pathologic, FFXII |
| Elin-specific absurdity | FFIX warmth + Pathologic sincerity | constant Disco cleverness |
| occupational flavour | Pentiment, Morrowind, FFXII | BG3 |
| callback to old behavior | Disco, FFIX, BG3 | Tactics Ogre |
| low-importance procedural NPC | FFVII, Morrowind, Pentiment | Disco, BG3 |
| high-importance recurring NPC | FFIX, Disco, Pathologic | generic Morrowind topic style |

## 1.14 Anti-homogenization rules

1. Do not elevate every sentence. FFXII/Tactics register is contextual.
2. Do not make every NPC eccentric. Disco/Pathologic distinctiveness belongs mainly in attention and
   interpretation.
3. Do not make ordinary NPCs generic. FFVII/FFIX economy can still carry attitude.
4. Do not encode class as caricature. Prefer lexical, rhetorical, and knowledge differences before
   phonetic dialect.
5. Do not use occupational vocabulary as costume.
6. Do not confuse ambiguity with missing semantics.
7. Do not let relationship overwrite idiolect.
8. Do not turn callbacks into fan-service; recognition should feel incidental and earned.
9. Do not make every line memorable. Signature prose needs mundane prose around it.
10. Never copy surface diction merely to signal an influence. Borrow the writing function.

---

# 2. How this research should affect BQ, and how it should not

## 2.1 Preserve the existing authority boundary

Nothing in these references justifies moving truth into prose.

Continue to require:

```text
world state
→ actor-local knowledge / belief
→ interpretation / intent
→ semantic speech act
→ disclosure choice
→ realization reading
→ authored wording
```

The writing layer may make a fact sound frightened, formal, defensive, vulgar, poetic, embarrassed,
weary, evasive, or mundane. It may not create the fact.

This is especially important when borrowing from Pathologic or Disco Elysium, whose handcrafted
writing can freely imply facts because a human author controls the whole scene. BQ's authored
fragments must encode every factual implication in their eligibility conditions or avoid asserting
it.

## 2.2 Do not create seven imitation voices

Do not add `DiscoStyle`, `PathologicStyle`, `MatsunoStyle`, or any equivalent.

These games are reference libraries for *techniques*. BQ characters should remain products of Elin
state, persistent personality, social context, occupation, relationships, emotional state, and voice
profile.

## 2.3 The quality target is recognizability, not literary density

A player should eventually be able to infer who is speaking from the shape of a line even when the
speaker's name is hidden.

That does **not** mean every speaker needs a conspicuous verbal tic.

A durable voice can come from a few recurring preferences:

- sentence length;
- clause structure;
- directness;
- level of formality;
- favorite conceptual domain;
- willingness to qualify statements;
- figurative vs literal language;
- contraction / colloquial tendencies;
- how often they name people directly;
- whether they explain reasons before or after conclusions.

Use one or two strong tendencies plus a baseline, not a pile of gimmicks.

---

# 3. Disco Elysium — primary lessons

## 3.1 The useful lesson is micro-reactivity, not maximalist prose

ZA/UM writer Justin Keenan described Disco Elysium's "micro-reactivity" as producing large numbers
of small narrative ripples: apparently minor player states or choices are noticed later, even where
the reaction has little instrumental gameplay value.

The important design effect is **recognition**. The game repeatedly tells the player: the world
noticed what you did.

For BQ, this maps directly onto:

- callback eligibility;
- event history;
- relationship memory;
- object provenance;
- prior promises;
- prior embarrassment;
- old favors or injuries;
- whether an NPC witnessed, heard, inferred, or participated in something;
- whether the player repeatedly behaves in a recognizable pattern.

### Writing rule

A callback does not need to advance a quest to justify itself.

It can exist simply to alter the texture of a current line:

```text
NEUTRAL:
"I'll tell you what I know."

WITH OLD KINDNESS:
"You helped me when you did not have to. I'll tell you what I know."

WITH OLD HUMILIATION:
"You already made a spectacle of me once. Ask carefully."
```

The second clauses may be mechanically ordinary. Their value is continuity.

### BQ application

Prefer **small callback insertions** over dedicated bespoke scenes whenever the old event is not
important enough to own a scene.

Good callback fragments should frequently be removable without breaking the semantic content of the
line. Their role is recognition, not factual load-bearing.

## 3.2 Make NPCs deeper than their game function

Keenan's discussion of the pawnbroker Roy is particularly relevant: the point was to let a shopkeeper
possess personal history, interests, and opinions unrelated to being a shopkeeper.

BQ already treats temporary narrative roles as non-identities. Dialogue writing should reinforce
that.

A shopkeeper should not only possess "merchant dialogue." A guard should not only discuss law. A
farmer should not only discuss crops.

### Authoring rule

For recurring or important NPCs, maintain at least three conceptual sources of dialogue:

1. **current function / circumstance** — what the player came here about;
2. **durable identity** — trade, faith, group, education, social position, habits;
3. **irrelevant humanity** — preference, grievance, curiosity, old experience, local fixation,
   private interpretation.

The third category makes the first two believable.

Do not manufacture arbitrary biography at realization time. It must come from authored identity
affordances, persistent memories, traits, topics, or other authoritative state.

## 3.3 Worldview should affect what a character notices

Disco Elysium is most useful when treated as a model of *selective attention*.

Different voices do not merely substitute adjectives. Different minds foreground different aspects
of an event.

Consider one theft:

- a merchant notices replacement cost and trust;
- a guard notices access and procedure;
- a priest notices obligation or transgression;
- a paranoid character notices opportunity and collusion;
- a sentimental character notices who was hurt;
- a status-conscious character notices public embarrassment;
- an impatient character wants the shortest actionable conclusion.

BQ should let personality / sensitivities / occupation affect **emphasis**, provided they do not
change the authoritative semantic claim.

### Fragment-writing technique

Write multiple realizations of the same meaning whose difference lies in framing:

```text
Claim: Rowan took the ring.

PLAIN:
"Rowan took the ring."

PROCEDURAL:
"Rowan was the one who left with it. Start there."

MATERIAL:
"Rowan took it. The ring is the part that matters."

SOCIAL:
"Rowan took it, and now everyone in the room knows it."
```

The last two require corresponding context. Do not let stylistic framing smuggle unsupported
propositions.

## 3.4 "Meaningless" choices can be valuable when they express the player

A Disco Elysium conversation often offers choices whose main function is expression rather than
optimization.

BQ should not turn every dialogue option into a skill check, branch, reward, or irreversible
outcome.

Where the existing conversation system allows it, some options can differ primarily in:

- politeness;
- bluntness;
- emotional posture;
- willingness to reveal the player's knowledge;
- whether the player pushes, comforts, jokes, threatens, or simply asks;

provided those differences are represented semantically and are not fake choices pretending to
produce consequences they do not.

### Principle

**Expression is a valid consequence when the world can remember the expression.**

If nobody can perceive or remember the distinction, avoid multiplying choices solely for cosmetic
wording.

## 3.5 What not to copy from Disco Elysium

Do not make every NPC:

- intellectually theatrical;
- ideologically exhaustive;
- metaphor-heavy;
- self-consciously strange;
- unusually articulate;
- capable of producing a quotable line every turn.

BQ's simulation will repeat conversational situations far more often than a handcrafted narrative
RPG. High literary density would expose the fragment library quickly and homogenize the cast.

Use Disco's **specificity, worldview, and reactivity**. Do not use its average prose intensity as
the baseline.

---

# 4. Pathologic 2 — primary lessons

## 4.1 Protect the player's attention

Pathologic 2 narrative designer Alexandra Golubeva described the game as containing an enormous
amount of text and therefore needing to avoid wasting player attention.

This applies even more strongly to a procedural system, because BQ can potentially generate dialogue
indefinitely.

### Writing rule: every optional fragment should earn its slot

An opener, modifier, callback, context fragment, or closer should do at least one of the following:

- identify voice;
- reveal emotional state;
- reveal relationship;
- recall meaningful history;
- clarify provenance or uncertainty;
- establish immediate social context;
- convey actionable information;
- create necessary rhythm before or after a dense core;
- deliver setting texture unavailable elsewhere.

If it merely makes the line longer, remove it.

This aligns with BQ's current silence-weighted optional slots. Authoring should support the system
rather than fighting it by filling every slot with decorative material.

## 4.2 Replace instructions with character-owned thoughts

Pathologic 2's mindmap deliberately avoids anonymous imperative quest language. Information is
framed through the protagonist's mental voice rather than an invisible game administrator.

The transferable BQ principle is broader:

> **Always know who owns the wording.**

For every piece of narrative text ask:

- Is this the NPC speaking?
- Is this the player's inferred case note?
- Is this a neutral UI fact?
- Is this remembered dialogue?
- Is this an authoritative system label?

Do not blur them.

If BQ later expands journal/Chronicle text, avoid phrasing semantic state as if an omniscient
narrator is assigning objectives unless that surface is intentionally authoritative.

## 4.3 Information should carry attitude without becoming unreliable by accident

Golubeva notes that Pathologic's mindmap wording does more than state facts; it expresses the
protagonist's attitude toward them.

BQ can do the same in dialogue realization:

```text
FACTUAL MEANING:
Mara owes Sibylla money.

NEUTRAL:
"Mara owes Sibylla."

RESENTFUL:
"Mara still owes Sibylla."

DISMISSIVE:
"It is a debt. Mara's, Sibylla's. Nothing unusual."

ANXIOUS:
"Mara owes Sibylla. Keep that between us."
```

But words like `still`, `again`, `finally`, `only`, `everyone`, `nobody`, `of course`, and
`as usual` are semantically dangerous. They imply duration, recurrence, exclusivity, universality,
expectation, or prior pattern.

### BQ authoring rule

Treat **discourse particles as propositions** when they materially imply world state.

A stylistic line should not gain `again` merely because it sounds more natural. `again` needs
history.

## 4.4 Ambiguity is stronger when it comes from perspective, not arbitrary obscurity

Pathologic frequently presents partial, contradictory, emotionally loaded understandings of events.

BQ already has the machinery needed to do this more safely than most games:

- facts vs beliefs;
- confidence;
- source/provenance;
- proof;
- rumor;
- deception;
- disclosure depth;
- actor-local knowledge.

Use those systems to produce uncertainty.

Do **not** create ambiguity by writing unclear sentences when the underlying speaker is certain and
direct.

### Prefer

```text
"That's what I heard. I didn't see it myself."
```

or

```text
"I think it was Mara. Think, not know."
```

when the semantics support them.

### Avoid

Cryptic wording that forces the player to guess whether the NPC is uncertain, evasive, poetic, or
simply badly written.

## 4.5 Let multiple strands inhabit the same conversation

Pathologic's narrative design intentionally connects stories rather than segregating "main" and
"side" material.

BQ callbacks can create the same texture economically.

A conversation about a missing item may also contain:

- a remembered favor;
- a guild tension;
- fear created by an unrelated recent death;
- resentment from a previous accusation;
- a location-specific social practice;

without turning any of those into a new quest.

### Constraint

One line should usually have **one semantic center**.

Cross-thread texture belongs mainly in optional callbacks/modifiers/context, not in core fragments
that try to communicate three unrelated facts at once.

## 4.6 What not to copy from Pathologic 2

Do not make opacity a universal tone.

Pathologic's setting supports ritual language, elliptical conversation, philosophical abstraction,
cultural estrangement, and deliberate discomfort.

Elin supports weirdness, but its tonal range is broader and frequently lighter, more mundane, comic,
practical, or absurdly sincere.

Use Pathologic's **perspectival uncertainty and information discipline**, not permanent obscurity.

---

# 5. Tactics Ogre / Final Fantasy Tactics — primary lessons

## 5.1 Build language from the world, not from "fantasy voice"

Alexander O. Smith's discussion of localizing Matsuno's games is one of the most useful references
for BQ. His stated approach emphasizes understanding the world behind the words and finding an
internally coherent English equivalent rather than simply applying English surface text to a foreign
fictional structure.

The core lesson:

> **Diction is worldbuilding infrastructure.**

Fantasy register should emerge from institutions, social hierarchy, religion, professions, law,
military structures, geography, commerce, inherited terminology, and cultural expectations.

Not from randomly inserting `aye`, `shall`, `tis`, or archaic contractions.

## 5.2 Register is relational

A believable formal register is not just "everyone uses longer words."

Characters change register based on:

- who outranks whom;
- whether they are speaking publicly;
- institutional context;
- familiarity;
- accusation vs negotiation;
- ritual/social practice;
- education;
- whether they are attempting to sound authoritative.

BQ's voice profile should provide a baseline. Situation and relationship can modulate within a
bounded range.

### Example

Same guard, same semantic request:

```text
TO A STRANGER:
"State your business."

TO A COLLEAGUE:
"What have you got?"

TO A SUPERIOR:
"What do you require?"
```

Do not give each context a completely unrelated idiolect. The stable voice remains underneath the
social adjustment.

## 5.3 Institutions should possess shared lexical fingerprints

Matsuno-style settings are convincing partly because political and military structures feel
linguistically real.

BQ should build small shared vocabularies around Elin-readable identities and social structures.

```text
GUARD / WATCH:  watch  post  route  report  order  witness  entry  search  hold  release
COMMERCE:       stock  price  loss  credit  debt  trade  weight  cost  worth
FARMING:        season rot  seed  yield  weather  harvest  soil
```

These should be **conceptual domains**, not Mad Lib substitutions.

A merchant does not need to say `price` in every sentence. Instead, a merchant may naturally
conceptualize obligations as accounts, shortages as inventory, or trust as credit when a figurative
voice permits it.

## 5.4 Social conflict benefits from precise, restrained language

Tactics Ogre and FFT often deal with loyalty, class, legitimacy, betrayal, war, duty, and political
violence. Their best dialogue does not require every exchange to become a speech.

For BQ, high-stakes dialogue should often become **more precise**, not more ornate.

Useful traits:

- explicit nouns rather than vague emotional language;
- clear accusations;
- named obligations;
- concise statements of allegiance;
- concrete stakes;
- controlled repetition when a character is forcing a point.

### Better BQ escalation style

```text
"You promised him shelter. He died outside your door."
```

rather than a generic dramatic line about honor, fate, betrayal, and darkness.

Specific history supplies drama for free.

## 5.5 Elevated diction must remain legible

Smith's localization work is effective because its language feels distinct without becoming
incomprehensible.

BQ should use a **legibility ceiling**.

Even highly formal characters should usually communicate the semantic core immediately. Ornament
belongs around it, not instead of it.

### Rule

If a line must be reread to determine the basic claim, request, threat, or refusal, it is probably
too stylized for a procedural corpus unless obscurity is itself the intended semantic tactic.

## 5.6 Final Fantasy Tactics as a localization warning

The original English FFT localization became infamous for awkward and erroneous phrasing, while
later Matsuno localizations became celebrated for their strong, coherent English register.

The lesson for BQ is not simply "use better prose."

It is that **consistency of register requires editorial ownership**.

Fragment authorship should maintain a shared style reference covering:

- contraction conventions;
- acceptable archaism;
- titles/forms of address;
- institutional terms;
- vocabulary complexity bands;
- how Elin-specific absurdity interacts with formal speech.

Without this, a large authored corpus will drift even when individual fragments are good.

---

# 6. Pentiment — supporting lessons

## 6.1 Social position and education can be visible without exposition

Pentiment uses typography to convey education, vocation, literacy, and Andreas's perception of
speakers. The exact mechanism is visual, but the writing lesson transfers directly.

A character should not have to say "As an educated person...". Their linguistic behavior can reveal
it.

Possible BQ signals:

- clause complexity;
- vocabulary range;
- confidence with institutional terminology;
- willingness to abstract;
- precision vs approximation;
- use of analogy;
- reading/document vocabulary;
- formality under stress.

These are tendencies, not intelligence scores.

## 6.2 Education should not equal intelligence or moral worth

Pentiment's social differentiation works because literacy and education are contextual
characteristics rather than a simple hierarchy of who is "better."

BQ should avoid:

```text
educated   = articulate, correct, wise
uneducated = stupid, comic, broken grammar
```

A plainspoken character can be perceptive and exact. A highly educated character can be pompous,
evasive, confused, or foolish.

Separate knowledge, cognitive disposition, education/register, personality, and truthfulness.

## 6.3 Perceived identity can evolve

Pentiment changes typographic representation as Andreas learns more about some people. The BQ
equivalent should not generally be changing an NPC's real voice profile because the player learned
something.

Instead, this suggests a useful distinction for presentation systems:

- **speaker's durable language behavior**;
- **player's recognized interpretation of that behavior**.

If BQ ever exposes descriptive UI around NPC identity, discovery can reveal existing traits without
rewriting them retroactively.

---

# 7. Morrowind — supporting lessons

## 7.1 Conditional generic dialogue scales extremely well

Morrowind's topic system selects responses through filters, with more specific responses able to
override generic responses.

That structure is conceptually close to BQ's authored fragment eligibility.

The major lesson is that **generic content is not the enemy**. Poorly conditioned generic content
is.

A line can be broadly reusable if it becomes specific through eligibility such as location,
faction/group, occupation, current situation, relationship, knowledge, recent event, player status,
or social practice.

### BQ hierarchy

When multiple eligible fragments exist, content design should tend toward:

```text
highly specific but uncommon
↓
contextually specific
↓
voice-specific
↓
neutral universal fallback
```

This does not require hard-coded priority if the current selection architecture uses another
mechanism, but the corpus should contain these layers.

## 7.2 Local knowledge makes places feel inhabited

Morrowind's generic topics often communicate local people, landmarks, politics, trade, rumors,
dangers, and services.

BQ can improve ambient conversation by asking:

> What does this person know because they live *here*?

not merely:

> What generic line fits this SpeechAct?

Location provenance, local events, organizations, home state, social practices, and identity
affordances can eventually provide this grounding.

Again, plausible knowledge must not become actual knowledge automatically. The actor must possess
the relevant knowledge before wording can expose it.

## 7.3 Repeated utility topics need many boring variants

Morrowind demonstrates both the strength and weakness of reusable dialogue. Once players recognize
exact stock lines across dozens of NPCs, inhabitants blur together.

For BQ, high-frequency acts need **more plain variants than signature variants**.

The corpus should spend disproportionate authoring effort on:

- simple yes/no answers;
- uncertainty;
- ordinary questions;
- requests;
- greetings;
- refusals;
- basic information;
- transitions;
- acknowledgements.

Players hear those much more often than climactic accusations.

---

# 8. Wildermyth — procedural writing lessons

## 8.1 Generate character development more readily than procedural plot

Wildermyth developer Nate Austin has described the project's emphasis as character development
rather than procedural plot. Its event system searches authored events that fit current
circumstances and character roles.

This validates BQ's storylet/microstructure direction.

Do not ask dialogue fragments to create story. Let story emerge from persistent characters, events,
relationships, transformations, consequences, and recurrence. Fragments should make those things
legible.

## 8.2 Particularity to the cast is a first-class writing requirement

Wildermyth's published event-design guidance says events should feel particular to the characters
involved and emphasize ongoing personalities.

For BQ, an authored fragment is successful when the player thinks:

> "That sounds like Sibylla reacting to this."

not merely:

> "That is a good line for an angry NPC."

This is why generic emotional tags alone are insufficient. Strong emotional fragments should
intersect with durable voice/personality where they imply temperament.

## 8.3 Momentous brevity

Wildermyth explicitly values accomplishing a great deal in few words and recommends clear, concrete
language with vivid strangeness rather than flowery prose.

This is an excellent default for BQ.

A fragment should ideally contribute one vivid thing rather than three decorative ones.

### Useful BQ target

```text
plain semantic core
+ one character-specific pressure point
```

Example:

```text
"Mara took it. Don't make me say it twice."
```

when impatience/relationship supports the second clause.

---

# 9. Baldur's Gate 3 — supporting lessons

## 9.1 Reward improbable player paths with recognition

BG3 writing director Adam Smith has emphasized the pleasure of finding dialogue reachable only
because of a peculiar sequence of player choices. Larian treats recognition of player history as a
major part of role-playing.

BQ can achieve a procedural version through its event ledger and callbacks.

Not every rare state deserves a new scene. Many deserve a rare line.

### Authoring priority

When adding callback content, prioritize states that make players think:

> "I didn't expect the game to remember that."

Examples include:

- returning an object long after its original thread ended;
- speaking to someone after embarrassing them publicly;
- asking help from a former enemy;
- breaking a small promise and later needing trust;
- repeatedly refusing the same kind of obligation;
- showing an item to someone with a personal connection to it.

## 9.2 A stable character should answer new situations quickly

Larian's character-writing process often assigns substantial ownership of companions to particular
writers, allowing a writer to develop an intuitive sense of how that person reacts in unusual
situations.

BQ cannot assign a human writer to every procedural NPC, so the system must approximate the
**constraints that make such intuition possible**.

A useful character definition should make unexpected situations predictable from values,
disposition, sensitivities, relationships, goals, current emotion, and stable verbal tendencies.

If a character requires a bespoke exception for every new scene, the model is too shallow.

## 9.3 Test dialogue in play, not only in isolation

Larian writers have repeatedly discussed how text that seems fine in a file can feel wrong once
placed in the actual game and scene.

This maps directly to BQ's Lab tooling.

Future corpus work should include:

1. validator/tests for semantic legality;
2. controlled playground sweeps for contrast;
3. scene-level playback for rhythm;
4. eventually live Elin testing for frequency and fatigue.

A fragment can be individually excellent and still damage the system if it:

- fires too often;
- makes every speaker sound polished;
- stacks badly with openers/callbacks/closers;
- duplicates information already obvious from the scene;
- becomes absurd when repeated across many NPCs.

---

# 10. Composite BQ dialogue-writing model

Future authors should think in the following order.

## Step 1 — What must this line mean?

Start with semantic authority: SpeechAct, claim/proposition, disclosure depth/tactic, commitment,
relationship to previous turn, required provenance or uncertainty.

Write no prose yet.

## Step 2 — What state is worth making visible?

Choose at most one or two major expressive signals: emotion, relationship, callback, occupation,
social practice, location, sensitivity, worldview emphasis.

Do not cram every eligible state into one line.

## Step 3 — How does this speaker habitually construct language?

Apply stable voice tendencies: terse/verbose, register, cadence/syntax, literal/figurative,
direct/hedged, contractions/careful speech, vocabulary domain.

## Step 4 — Does every word remain semantically legal?

Audit loaded words:

```text
again   still    always   never   nobody   everyone   only   finally
obviously   of course   remember   heard   saw   know   promise   owe
```

These often encode more state than authors realize.

## Step 5 — Is this line allowed to be memorable?

Most lines should not be.

Ask whether the situation contains enough weight to justify a line with a strong image, joke,
aphorism, unusual rhythm, or signature metaphor.

If not, use the plain version.

## Step 6 — Read the assembled line, not just the fragment

Test it with likely opener/modifier/callback/context/closer combinations.

A good fragment can become overwrought when surrounded by four other good fragments.

---

# 11. Recommended prose tiers

These are authoring categories. Where the runtime carries a matching vocabulary, it is
`DialogueMemorability` — see `content-pipeline.md` for how a tier is declared and what it changes.

## Tier A — Utility / mundane

Purpose: move information cleanly and invisibly.

```text
"Yes."
"No."
"I don't know."
"Ask Mara."
"I saw him leave."
"What do you need?"
"Not here."
```

Needs the largest library because it appears constantly.

## Tier B — Character-colored

Purpose: reveal stable voice, mood, relationship, or occupation without stealing the scene.

```text
"I saw him leave. Quickly, if that matters."
"Ask Mara. She keeps better track of these things than I do."
"Not here. Walls are cheaper than loyalty."
```

The last example requires a figurative/cynical voice and appropriate secrecy context.

## Tier C — Signature / memorable

Purpose: reward unusual state, recurrence, major emotion, high stakes, or character-specific
context.

These should be scarce and repetition-controlled aggressively.

The writing may become more lyrical, funny, strange, or cutting here, but should still arise from
real state.

### The floor this implies

A tier B or C line is an *alternative*, never the only way a situation can be worded. Any condition
a memorable line is written for needs a plain line written for the same condition, or the memorable
line stops being the exception and becomes the way that situation always sounds.

---

# 12. Cadence library for authored variation

Do not diversify only by vocabulary. Diversify sentence *shape*.

```text
FRAGMENTARY                       "Mara."  /  "Before sunset."  /  "Not willingly."
SIMPLE DECLARATIVE                "Mara took it."
QUALIFICATION AFTER CONCLUSION    "Mara took it, as far as I know."
QUALIFICATION BEFORE CONCLUSION   "If what I heard is right, Mara took it."
TWO-BEAT CORRECTION               "Mara had it. No — she took it."
ACCUMULATION                      "She came late, left early, and took the ring with her."
QUESTION AS PRESSURE              "You want me to accuse Mara to her face?"
CONCLUSION + PRACTICAL CONSEQUENCE "Mara took it. Check the south road."
CONCLUSION + SOCIAL CONSEQUENCE   "Mara took it. Say that loudly and you'll have a different problem."
```

Only the semantic/contextual requirements that are actually supported should permit each form.

---

# 13. Character voice construction

A procedural NPC should usually be assigned a **baseline plus one or two salient tendencies**.

## Plain, terse shopkeeper

```text
register: common          length: short        syntax: simple
figuration: literal       secondary tendency: commerce-domain framing
```

```text
"Mara took it. That's a loss, whoever pays for it."
```

## Educated but anxious healer

```text
register: educated        length: medium       syntax: qualified
figuration: mostly literal  secondary tendency: hedging under uncertainty
```

```text
"I believe Mara took it, but belief is all I can give you."
```

## Warm, figurative farmer

```text
register: common          length: medium       syntax: simple-compound
figuration: idiomatic     secondary tendency: seasonal/agricultural domain
```

```text
"Mara took it. Bad seed has a way of showing itself eventually."
```

That final clause should be reserved for sufficiently figurative characters and should not imply
unsupported facts about Mara's habitual character unless treated purely as the speaker's evaluation.

---

# 14. Occupational vocabulary rules

## Do

Use occupation to influence preferred nouns, practical concerns, analogies, what consequences are
foregrounded, institutional vocabulary, and likely areas of precision.

## Do not

Turn professions into comedy dialects.

Bad:

```text
Merchant: "That's a high price to pay" every conversation.
Farmer:   every metaphor involves crops.
Guard:    every line says "by the law".
```

Better: occupation has a low but persistent probability of coloring language when context permits.

The player should notice it over ten conversations, not every sentence.

---

# 15. Weirdness and humor

The reference games support two especially useful rules.

From Disco Elysium: strange details become convincing when characters treat them as part of lived
reality.

From Wildermyth: vivid strangeness works better when language remains concrete rather than florid.

For Elin/BQ:

> **Put the weirdness in the premise or consequence first; let most characters speak about it
> sincerely.**

If a tax collector is arguing over ownership of a sentient mushroom, the mushroom is already the
joke. The tax collector does not also need five jokes in the sentence.

Humor can come from:

- mismatch between bizarre premise and practical concern;
- callback to an old embarrassment;
- personality-consistent understatement;
- literal treatment of an absurd social rule;
- a rare well-timed figurative comparison.

Avoid "procedural quirk voice" where every NPC competes to be the funniest person in the room.

---

# 16. Relationship writing

Relationship context should change **permission, stakes, familiarity, and assumptions**, not
overwrite personality.

A friend can be terse, formal, awkward, sarcastic, solemn, affectionate, or emotionally restrained.

Therefore avoid fragments whose only requirement is `relationship: friend` when the prose
additionally requires playfulness, intimacy, protectiveness, or emotional openness.

### Better model

```text
relationship = friend + voice = terse + emotion = worried
→ "You're late. I was worried."

relationship = friend + voice = figurative/playful + emotion = worried
→ "There you are. I was running out of disasters to imagine."
```

Same relationship. Different person.

---

# 17. Uncertainty, lies, rumor, and proof

This is an area where BQ can surpass all of the reference games because its semantics already
distinguish these states.

Writers should maintain clearly different linguistic families for:

| Family | Example | Requires |
|---|---|---|
| Witnessed | "I saw Mara take it." | actual witnessed provenance |
| Participant knowledge | "Mara took it from me." | appropriate participation/relationship to the event |
| Hearsay | "I heard it was Mara." | hearsay source |
| Inference | "The signs point to Mara." | inference provenance |
| Uncertain belief | "I think it was Mara." | appropriate confidence/commitment |
| Cannot prove | "I know what I saw. Proving it is another matter." | the belief, and lack of proof |

**Deliberate deception.** The wording can be confident while the semantic layer knows the speaker is
lying. Do not mark lies through mandatory villainous wording; good lies often sound ordinary.

These distinctions create far more useful variety than adjective swapping.

---

# 18. Editing checklist for every new fragment pack

Before shipping a fragment, ask:

1. What exact semantic meaning can this line express?
2. Does the line assert anything not guaranteed by `requires`?
3. Is its voice metadata describing linguistic behavior rather than personality?
4. Could a relationship/emotion tag be accidentally supplying temperament?
5. Is occupation being used as a vocabulary domain rather than a gimmick?
6. Does this line duplicate an existing cadence even if the nouns differ?
7. Is it too memorable for how frequently it may fire?
8. Does it still work when preceded by an opener?
9. Does it still work when followed by a callback or closer?
10. Would it sound absurd if three unrelated NPCs used it in one play session?
11. Is there a plainer version in the pool?
12. Does uncertainty/provenance match the speaker's actual knowledge?
13. Are loaded words such as `again`, `still`, `everyone`, or `remember` justified?
14. Is the central information understandable on first read?
15. Does the line sound like someone in Elin rather than someone demonstrating that the writer can
    write?

---

# 19. Corpus-development priorities

When expanding content, prioritize in this order:

1. **Plain high-frequency cores** — answers, asks, informs, requests, refusals, denials, admissions,
   uncertainty.
2. **Cadence variants** — same semantic coverage with genuinely different sentence structures.
3. **Stable voice variants** — enough material for contrasting register/length/syntax/figuration
   profiles.
4. **Provenance/uncertainty variants** — witnessed, heard, inferred, remembered, unproven.
5. **Relationship-aware variants** — only where relationship genuinely changes wording.
6. **Occupational/contextual framing** — sparse but persistent.
7. **Callbacks** — broad coverage of meaningful recurrence.
8. **Signature lines** — last, rarest, most repetition-controlled.

Do not expand signature content faster than mundane content.

**This applies per slot, not only to the library as a whole.** The optional slots — openers,
modifiers, callbacks, context, closers — fire in every line of every act, so a memorable-heavy
optional family is heard far more often than a memorable-heavy core file. A tie, a mood, an
audience or a kind of recalled history that has only memorable wording is a situation the world
can only ever say strikingly.

---

# 20. Suggested generation brief for future dialogue-authoring agents

When an agent is asked to author new fragments, the task prompt should include this compact brief:

```text
Author against authoritative semantic state, never prose-generated facts.
Prefer plain dialogue; memorable lines are scarce.
Create cadence diversity, not synonym diversity.
Voice describes stable linguistic behavior; personality controls decisions.
Relationship/emotion/context may color a voice but must not replace it.
Occupational language should be sparse conceptual framing, not profession catchphrases.
Every factual implication in wording must be guaranteed by fragment requirements.
Use callbacks for recognition even when they have no instrumental quest effect.
Keep the semantic core legible on first read.
Test assembled output, not fragments in isolation.
```

---

# 21. Reference-specific takeaways at a glance

| Reference | Borrow | Avoid |
|---|---|---|
| Disco Elysium | micro-reactivity, worldview, irrelevant humanity, player recognition | universal maximalism, constant quotability |
| Pathologic 2 | attention discipline, perspectival information, ambiguity from knowledge state | universal crypticism |
| Tactics Ogre / FFT | world-native register, institutional diction, precise stakes | generic fake-archaic fantasy speech |
| Pentiment | class/education/occupation visible through linguistic behavior | equating education with intelligence or virtue |
| Morrowind | conditionally specific generic dialogue, local knowledge, fallback layers | repeated stock lines across the whole population |
| Wildermyth | character-first procedural writing, vivid brevity, authored event fitting | trying to procedurally manufacture grand plot through dialogue |
| Baldur's Gate 3 | recognition of unusual player histories, stable reactions, in-context iteration | combinatorial bespoke branching where existing systemic callbacks suffice |

---

# 22. Research sources

These sources were used for transferable design/writing principles rather than as templates to
imitate verbatim.

## Final Fantasy / Matsuno lineage

- Square Enix — FINAL FANTASY VII REMAKE Localization Team Interview, Parts 1–2
- Game Studies — *Computer Games Have Words, Too: Dialogue Conventions in Final Fantasy VII*
- Time Extension — *The Incredible Story Behind Final Fantasy IX's Epic Translation*
- GameSpot — *Final Fantasy XII vets talk game localization*
- Eludamos — interview with Alexander O. Smith, Steven Anderson, and Matthew Alt
- Game Developer — *In-Depth: Kajiya Productions On The Art Of Localizing Tactics Ogre*
  <https://www.gamedeveloper.com/game-platforms/in-depth-kajiya-productions-on-the-art-of-localizing-i-tactics-ogre-i->
- Game Developer — *Interview: Kajiya Productions on Translating Final Fantasy*
  <https://www.gamedeveloper.com/game-platforms/interview-kajiya-productions-on-translating-i-final-fantasy-i->
- RPG Site — *Tactics Ogre: Reborn Interview*
  <https://www.rpgsite.net/interview/13682-tactics-ogre-reborn-interview-how-square-enix-approached-revisiting-a-beloved-classic-rpg>

## Disco Elysium

- Game Developer — *Understanding the meaningless, micro-reactive, and marvellous writing of Disco
  Elysium* — Justin Keenan's GDC discussion of micro-reactivity, particularity, non-instrumental
  dialogue, and NPC depth.
  <https://www.gamedeveloper.com/business/understanding-the-meaningless-micro-reactive-and-marvellous-writing-of-i-disco-elysium-i->
- Game Developer — *Player agency, politics, and narrative design in Disco Elysium* — Keenan on
  character creation, world density, research, and worldviews.
  <https://www.gamedeveloper.com/design/player-agency-politics-and-narrative-design-in-disco-elysium->

## Pathologic 2

- Game Developer / gamedev.world transcript — *Pathologic 2 Mindmap: a Questlog People Actually
  Read* — Alexandra Golubeva on attention, character voice, integrating information surfaces,
  narrative connections, and replacing anonymous quest imperatives with thoughts.
  <https://www.gamedeveloper.com/design/pathologic-2-mindmap-a-questlog-people-actually-read>

## Pentiment

- WIRED — *The Director of Pentiment Wants You to Know How His Characters Ate* — Josh Sawyer on
  language, ambiguity, historical context, social background, and typography as characterization.
  <https://www.wired.com/story/pentiment-josh-sawyer-interview/>
- Lettermatic — Pentiment custom typeface case study — typography as a substitute for vocal
  characterization and a signal of education/social position.
  <https://lettermatic.com/custom/pentiment>
- Pentiment official site / making-of materials. <https://pentiment.obsidian.net/>

## Morrowind

- Project Tamriel — *Writing and Dialogue Guidelines* — topic-based conditional response structure,
  specific-before-generic filtering, and world/local-lore writing practices.
  <https://wiki.project-tamriel.com/wiki/Writing_and_Dialogue_Guidelines>
- Tamriel Rebuilt — *Guidelines for NPC Claims* and related dialogue documentation — local generic
  topics and differentiated NPC knowledge.
  <https://www.tamriel-rebuilt.org/content/guidelines-npc-claims>

These are community continuation/modding documents rather than original Bethesda design documents;
they are used here because they closely document the actual Morrowind dialogue system and the
long-term practical lessons of authoring large amounts of content against it.

## Wildermyth

- Wildermyth Wiki — *Event design philosophy* — particularity to characters, clear concrete
  language, vivid strangeness, immediate context, and "momentous brevity."
  <https://wildermyth.com/wiki/Event_design_philosophy>
- Wildermyth Wiki — *Event Types* — procedural selection of authored stories according to situation
  constraints. <https://wildermyth.com/wiki/Event_Types>
- Turn Based Lovers — *Interview with Wildermyth developer Nate Austin* — emphasis on procedural
  character development rather than attempting fully procedural plot.
  <https://turnbasedlovers.com/10-turns-interview/with-wildermyth-developer/>

## Baldur's Gate 3

- CGMagazine — *Writing the Future of Baldur's Gate — An Interview with Senior Writer Adam Smith* —
  rare-path dialogue, recognizing player choices, character/world memory, and RPG storytelling as
  play. <https://www.cgmagonline.com/interviews/writing-the-future-of-baldurs-gate/>
- British GQ — *Baldur's Gate 3 development interview* — writer ownership of companions, intuitive
  character consistency, iteration, and Early Access feedback.
  <https://www.gq-magazine.co.uk/article/baldurs-gate-3-interview>
- PC Gamer — *Larian on BG3 reactivity / "weird Dungeon Masters"* — systemic recognition and
  combinatorial reactivity.
  <https://www.pcgamer.com/games/rpg/larian-gave-baldurs-gate-3-its-acclaimed-reactivity-by-approaching-it-as-weird-dungeon-masters/>

---

# 23. Final target

Brilliant Questing should ultimately produce dialogue where:

- the **truth** comes from the simulation;
- the **claim** comes from the speaker's belief;
- the **decision to speak** comes from motive and context;
- the **focus** comes from worldview and sensitivity;
- the **register** comes from identity and social circumstance;
- the **cadence** comes from persistent voice;
- the **texture** comes from emotion, occupation, place, and relationships;
- the **continuity** comes from history;
- and the **memorable line** appears only when enough of those things align to deserve one.

The desired player reaction is not:

> "The dialogue generator has a lot of lines."

It is:

> **"Of course that person said it that way."**
