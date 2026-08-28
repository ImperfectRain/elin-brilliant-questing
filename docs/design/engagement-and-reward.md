# Engagement and Reward

**Status:** Design reference, grounded in comparator research
**Date:** 28 August 2026
**Relationship:** Answers a question the other four documents leave open. They establish that the
simulation should be causal, persistent and explainable. None of them establish why a player would
*engage* with it rather than treat it as scenery.

---

## 1. The failure this document exists to prevent

The mod can be architecturally excellent and still be ignored.

Skyrim's Radiant system is the reference failure: infinite contracts, finite stories. Critics
described it as quests with no actual stories or characters, gameplay amounting to following a
marker, and — the line that should worry this project most — developers doing a great deal of work
that players could not or would not appreciate. The structural diagnosis is exact: *an infinite
number of contracts, but not an infinite number of stories associated with those contracts*.

Brilliant Questing is more sophisticated than Radiant in every dimension. That is not protection.
A causal simulation that produces genuinely different situations is still ignorable if engaging
with it costs the player time and returns nothing they already wanted.

**The design question is not "what reward do we attach?" It is "why is engaging the shortest path
to something the player already wants?"**

---

## 2. What the comparators actually do

### RimWorld — drama is paced, and it comes to you

The storyteller generates events based on the player's progress and choices, pacing highs and lows
rather than emitting content uniformly. Two lessons. First, engagement is not optional: raids,
disease and weather arrive whether or not the player sought them. Second, attachment precedes
stakes — the player must already care about a colonist before a threat to them means anything.

### Dwarf Fortress — history is the artifact

Legends mode records every historical event and every figure. A subset of players generate worlds
*purely to read the history*. Boatmurdered is a shared cultural object. The most quoted explanation
of player attachment is not about mechanics at all: *"I care about my dwarves because the stories I
make up about their lives are also the ones I make up about my own."*

The lesson is that **legible, retrievable history is itself a reward**, and a shareable one.

### Kenshi — no quests, and it does not need them

No main quest, no levels, no classes. Motivation comes from interlocking necessity: to progress you
must survive, so survival matters; to survive you need a base, so building matters; to hold a base
you need allies, so recruitment matters. *Every mechanic feeds the core vision.*

The lesson: **the strongest engagement hook is dependency, not reward.**

### Amazing Cultivation Simulator — two layers that collide

A physical settlement layer and a per-disciple progression layer, with emergent narrative arising
where they interfere — a thunderstorm shifting local Qi and ruining a breakthrough. Individual
long-term investment plus systemic interference produces stories nobody wrote.

### Elin itself — the loop already exists

Skill-by-use, a player town as the long-term project that generates passive income, and a
clear → upgrade → push-harder loop. Retention of 50–200+ hours is driven by town building, breeding,
crafting and dungeon progression.

**This is the decisive observation for the mod.** Elin's players already want specific things:
residents, specialists, shop stock, investment, materials, safety, fame, land. The mod does not need
to invent desire. It needs to become a *supplier of things already desired*.

### Fallen London — access is the reward

Quality-based narrative gates storylets behind qualities, where a quality is simultaneously
inventory, skill and story progress. The reward for engaging with content is more content becoming
reachable. Content unlocking content.

### Self-determination theory — the warning

Intrinsic motivation rests on competence, autonomy and relatedness. Crucially, **controlled**
extrinsic motivation undermines intrinsic motivation through the overjustification effect, while
**autonomous** extrinsic motivation enhances it.

Translated: attaching a loot chest to a resolution is a controlled extrinsic reward and it makes the
narrative feel like a chore with a payout. Granting *access, relationships and options* is autonomous
extrinsic reward and it compounds.

---

## 3. Three tiers of engagement design

### Tier 1 — Make the simulation the supply line, not a distraction

The mod should become the source of scarce things Elin already makes the player want.

| The player wants | The simulation supplies it |
|---|---|
| A specialist resident | A displaced craftsman who needs somewhere to go |
| Shop stock and better prices | A merchant whose supplier failed, or whose rival you helped |
| Investment returns | A struggling business that survives because you financed it |
| Safety at Home | A resolved bandit problem, or a resident who owes you |
| Rare materials | Cargo that went missing and can be recovered |
| Land, property, a workshop | An estate whose owner died, fled, or owes you |
| Labour | People with nowhere else to go |
| Fame and standing | Public resolutions, witnessed and remembered |

When this holds, the player is not being interrupted by a story. They are being told **where the
carpenter is**. Engaging is the shortest path to the town they were already building.

This is the Kenshi lesson applied: every mechanic feeds the core vision. It is also why
`BQ-050` (demand), `BQ-048` (Home) and `BQ-051` (shop continuity) are load-bearing for engagement,
not merely for realism.

### Tier 2 — Reward with access and options, never with payouts

The reward vocabulary should be things that *change what the player can do next*:

- **A person** — a recruited specialist with a real job at Home.
- **A supply line** — a shop that now stocks what you need, at a price only you get.
- **Access** — a fence, a safehouse, a guild introduction, a door that is now open.
- **Standing** — real Karma, fame, Influence, guild contribution.
- **Information** — leverage, a route, an alibi, knowledge of who profits.
- **Property** — a deed, a workshop, land.
- **A favour owed** — the strongest reward in the entire vocabulary, because it is a *stored
  option*. It costs nothing until spent, the player decides when and on what, and it is
  autonomy-supporting in exactly the sense SDT describes.

And explicitly *not*: a chest of high-tier loot for resolving a situation. `PM §12` already says
avoid attaching loot to every resolution; the research says why, and the reason is stronger than
"it's inelegant". It actively corrodes the motivation the rest of the design is trying to build.

### Tier 3 — Make the history legible, retrievable and shareable

This is the largest under-designed area in the current plan. `BQ-034` treats the Chronicle as a
memory aid. Dwarf Fortress demonstrates it should be the **trophy case**.

A player who can read back who their character became — the feuds, the people they saved, the shop
they rescued, the place that carries their name — has a reason to keep engaging that no reward
schedule can manufacture. And a player who can *export or screenshot* that history creates the
Boatmurdered effect: the mod's best advertisement is its own output.

---

## 4. Specific mechanisms

**Attachment before stakes.** The importance ladder currently promotes NPCs *after* the player gets
involved. That is backwards for a first encounter: the player must already recognise someone before
a threat to them lands. Seed a small number of low-stakes recurring contacts early — a shopkeeper
who remembers you, a neighbour with a small recurring complaint — so that when a real situation
casts them, it lands on a familiar face. Cheap to implement, disproportionate in effect.

**Consequences that arrive.** `BQ-098` is the RimWorld raid equivalent and the counterweight to "all
content is optional". It must be strictly *earned* — caused by the player's own recorded history,
never random — and it must be configurable. Earned arrival supports autonomy; random arrival
destroys it.

**Ignoring changes availability, not power.** The cost of ignoring a situation should be that the
world offers something different afterwards — the carpenter left, the shop closed, someone else took
the credit — never that the player is weaker or behind. Kenshi loses you a town; it does not dock
your stats.

**The first situation must be winnable with the build the player has.** A player whose first
procedural encounter demands a skill they never trained learns that the system is not for them. The
generator should bias the *first* situation in a save toward the player's actual strengths, and only
then widen.

**Small stakes are the on-ramp.** A missing chicken that a Charisma build mediates, a perceptive
build tracks, and a thief solves by stealing a replacement, teaches the whole grammar in five
minutes and costs nothing to fail.

---

## 5. Anti-patterns, named

| Anti-pattern | Why it fails |
|---|---|
| Infinite contracts, finite stories | The Radiant diagnosis. Variety of nouns is not variety of experience. |
| Loot chest per resolution | Overjustification. Converts a story into a chore with a payout. |
| Quest markers replacing knowledge | Removes the competence loop; the player stops thinking. |
| Forced engagement | Undermines autonomy, the strongest of the three SDT needs in a sandbox. |
| Rewards that need a new currency | Elin already has money, Karma, fame, Influence, contribution. A new meter is a new chore. |
| Story that does not touch the town | If resolutions never affect what the player is building, the simulation is scenery. |
| History that is only a log | A log is a memory aid. A chronicle is a trophy case. |

---

## 6. The engagement test

Alongside the existing checkpoints, a build should be able to answer yes to all of these:

1. Can a player who wants **nothing but a better town** find the mod useful?
2. Does the first situation in a save land on someone the player has already met?
3. Is the first situation solvable with the build the player actually has?
4. Does at least one reward change **what the player can do**, not what they own?
5. Can a player read their own history back and want to tell someone about it?
6. Can the player ignore everything for an hour and lose nothing but opportunity?
7. Does any arriving consequence trace to something the player actually did?

Question 1 is the important one. If the answer is no, the mod is a narrative distraction no matter
how good the simulation is.
