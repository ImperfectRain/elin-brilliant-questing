# Implementation Roadmap

The single ordered plan for Brilliant Questing, audited against the canonical design documents and
the code as it stands. Every other document describes *what* to build and *why*; this one says *in
what order*, *how you know a step is finished*, and *where every idea went*.

If you are an agent picking this project up cold, read [`AGENTS.md`](../AGENTS.md) first. It says
how to establish current state from Git and how to retrieve only what a task needs.

**This file is queried, not read front to back.** For an ordinary implementation task, search for the
step you are on (`rg -n "BQ-XXX" docs/implementation-roadmap.md`) and read that section plus any
standing rule or track it cites. Read it whole only when planning, auditing, or deciding where new
work belongs — §5 is the route, §7 the definition of launch, §8 what is deferred, §9 proves every
design idea has a home, §10 the standing rules that bind every step.

See [`docs/README.md`](README.md) for the reading order of the design corpus, which is likewise
retrieved on demand rather than preloaded.

---

## 1. How to use this document

**One step, one commit.** Every step below is sized to be a single commit that leaves the mod
installable and no worse than before. Commit subjects begin with the step id:

```
BQ-XXX Short imperative description of the finished step
```

So the project's real status is always:

```bash
git log --oneline | grep -oE '^[a-f0-9]+ BQ-[0-9]+' | head
```

**Never leave the mod unplayable between commits.** A step that cannot be completed in one commit
is too big and must be split. If a step turns out to need a spike, the spike is its own step.

**Status lives in git, not in this file.** Do not maintain a checkbox column here that drifts from
reality. To see what is done, read the log. This file only says what should happen and in what
order.

**Done-when is a test or a log line.** Every step states a verifiable completion condition. "It
compiles" is never one. Prefer, in order: a passing headless test; a specific line in
`BepInEx/LogOutput.log`; an observable change in game confirmed by a human tester.

**Reordering is allowed; dropping is not.** If evidence says a later step should come first, move
it and note why in the commit. If a step should be abandoned, move it to §8 Post-launch register
with a reason rather than deleting it — the whole point of this document is that nothing is lost
because its system got quiet for a while.

---

## 2. Lifecycle states

Every system in §4 sits in exactly one of these. This is what "finished until after launch" means.

| State | Meaning |
|---|---|
| **Absent** | Not built. May exist only as design text. |
| **Spiked** | Proven possible against the real game or a test, but not wired into play. |
| **Prototype** | Works on one path. Not persisted, not hardened, not safe to ship enabled. |
| **Playable** | A player can encounter it, use it, and see the result. Persists across save/load. |
| **Hardened** | Survives interruption, missing capabilities, malformed state and game updates without corrupting a save. Has debug explainability. |
| **Complete-until-launch** | Hardened, content-complete for 1.0, and deliberately frozen. Further ideas go to §8. |

A system may only be declared **Complete-until-launch** when its remaining ideas have been
explicitly moved to §8. That is the mechanism that stops good ideas from being silently dropped.

---

## 3. Last Verified Runtime Baseline

This section is not an implementation status snapshot. Use Git history, current code, and tests to
determine ordinary BQ progress. Keep only runtime evidence and risks that are expensive or
impossible to reconstruct from the repository alone.

Verified against `BepInEx/LogOutput.log` from a live game on 28 Aug 2026.

**Working in game.** Plugin loads through the Package Chainloader. Attaches on `EVENT.PostLoad`,
persists to the save's `brilliantQuesting` chunk on `EVENT.PreSave`. Reads real character state
through the adapter — all 23 element aliases resolve, 12 of 13 capabilities available. A staged
three-NPC theft can be entered through default talk, with procedural choices injected into Drama.

**Latest live playtest.** On 28 Aug 2026 a tester killed the perpetrator after earlier prototype
runs had failed to spawn the physical ring. The compatibility repair spawned the missing evidence
loose near the player, `search` recovered it as provable evidence, `return_item` handed it to the
victim, and the thread resolved as `property_returned`. The log saved 13 events into the
`brilliantQuesting` chunk. Remaining jank from that pass: Elin's material prefix produced
`a bronze silver ring`, the dead perpetrator was no longer bound to a live Chara, and the tracking
surface is still dialogue/case-notes rather than a journal tab.

**Runtime risks carried forward.** `StageScenarioOnLoad` and `GatherPrototypeNpcsNearPlayer` are
the two flags that write to or move things in a save; both default to off and must stay opt-in until
§S9. The Drama projector itself is **not** flag-gated — it installs in `Awake` and its three
Harmony patches are live for every player — so its safety boundary is scope and failure-tolerance,
not configuration: patching is all-or-nothing with a diagnostic (BQ-005), every callback is
guarded, and it only reads or rewrites Elin's generic conversation (book `_chara`, step `main`),
never authored dialogue. Those three patches are the project's main version-drift exposure.

**Still requiring real-game verification.** Headless tests and adapter metadata do not prove every
capability works in game. Treat unverified capability behavior as runtime risk until reproduced in
Elin and recorded here or in `docs/elin-api-notes.md`.

---

## 4. System ledger

Every system named across the canonical design documents, its state today, and the steps that move it.
No system is allowed to disappear from this table.

| System | Now | Target for 1.0 | Steps |
|---|---|---|---|
| Foundation: identity, RNG, time | Hardened | Complete-until-launch | BQ-001 |
| Event ledger & consequences | Playable | Complete-until-launch | BQ-002, BQ-016 |
| Facts, belief, evidence, provability | Playable | Complete-until-launch | BQ-017, BQ-018 |
| Rumor propagation | Hardened | Hardened | BQ-019, BQ-020 |
| Memory & consolidation | Playable | Hardened | BQ-021 |
| Relationships | Playable | Hardened | BQ-022, BQ-055 |
| Vanilla adapter (`IVanillaState`) | Playable | Complete-until-launch | BQ-003, BQ-011, BQ-030, BQ-135, BQ-144 |
| Checks (native + portable) | Playable | Complete-until-launch | BQ-004 |
| Action library | Playable | Complete-until-launch (coverage per §7, not a count) | BQ-023 … BQ-029 |
| Threads & escalation | Playable | Hardened | BQ-013, BQ-052 |
| Situation archetypes | Prototype | Complete-until-launch (7) | BQ-041 … BQ-047 |
| Situation generation from world state | Prototype | Playable | BQ-039, BQ-040 |
| Drama projection | Prototype | Hardened | BQ-005 … BQ-010 |
| Contextual interaction projection | Absent | Playable | BQ-134, BQ-137 |
| Journal / Chronicle | Absent | Playable | BQ-033, BQ-034, BQ-138 |
| Ambient delivery (barks, talk, leads) | Absent | Playable | BQ-035, BQ-036 |
| Crime & witness observation | Absent | Playable | BQ-014, BQ-015, BQ-136 |
| Personality → decisions | Prototype (weights only) | Playable | BQ-056 … BQ-060 |
| Character identity & affordances | Absent (placeholder intake) | Playable | BQ-144, BQ-145 |
| Values, needs, goal formation | Absent | Playable | BQ-061, BQ-062 |
| Emotion & interpretation | Absent | Playable | BQ-063, BQ-064 |
| Storylets & casting | Absent | Playable | BQ-065 … BQ-069 |
| Speech acts & disclosure | Absent | Playable | BQ-070 … BQ-073 |
| Dialogue realization & voice | Absent | Playable | BQ-074 … BQ-078 |
| Elin tone & weirdness budget | Absent | Playable | BQ-079, BQ-080 |
| Callbacks & continuity | Absent | Playable | BQ-081, BQ-082 |
| Conversation state & commitments | Absent | Playable | BQ-083 |
| Social practices | Absent | Playable | BQ-084 |
| Home integration | Prototype | Playable | BQ-030, BQ-048, BQ-049 |
| Economy & demand | Absent | Playable | BQ-050, BQ-051 |
| Guild information networks | Absent | Playable | BQ-037, BQ-038 |
| Organizations | Modelled, unsimulated | Playable | BQ-053, BQ-054 |
| Item & location provenance | Absent | Playable | BQ-085, BQ-086 |
| Social obligations & favors | Absent | Playable | BQ-055 |
| Sites, scenario dungeons & location grammars | Absent | Playable | BQ-087 ... BQ-092, BQ-139 ... BQ-141 |
| BQ-owned additive site mutation proof | Absent | Spiked | BQ-143 |
| Safe vanilla mutation policy | Prototype | Hardened | BQ-031, BQ-032 |
| NPC autonomy | Absent | Playable | BQ-093 … BQ-096 |
| Traveling groups | Absent | Playable | BQ-097, BQ-098 |
| Narrative director | Absent | Playable | BQ-099 … BQ-103 |
| Debug & explainability | Prototype | Hardened | BQ-012, BQ-104 |
| Save migration & integrity | Playable | Complete-until-launch | BQ-105, BQ-106 |
| Performance & simulation tiers | Absent | Hardened | BQ-107, BQ-108 |
| Compatibility & version drift | Prototype | Hardened | BQ-109, BQ-110 |
| Player configuration | Prototype | Complete-until-launch | BQ-111, BQ-120 |
| Engagement & reward | Absent | Complete-until-launch | BQ-112 … BQ-119 |
| Setting fidelity & player culture | Absent | Complete-until-launch | BQ-121 … BQ-128 |
| Content pipeline (authored data → bundle) | Absent | Complete-until-launch | BQ-129 … BQ-133 |
| Generated settlements | Absent | Post-launch | §8 |
| Mod interoperability API | Absent | Post-launch | §8 |
| Multiplayer | Absent | Post-launch, explicitly unsupported | §8 |
| Optional LLM prose (runtime) | Absent | Post-launch | §8 |
| Content authoring workbench | Absent | Post-launch | §8 |

---

## 5. The route

Ten stages. Each ends in a checkpoint that must hold before the next begins. Within a stage, steps
may be reordered if evidence justifies it.

Notation: **Depends** is a hard prerequisite. **Done when** is the completion test. **Sources**
cites the design documents — `MD` master-design, `PM` post-master-findings, `LW`
living-world-priorities, `PP` procedural-places-and-spatial-history, `CD`
character-dialogue-system, `SP` setting-and-player-culture; `CP` content-pipeline; `VS`
vanilla-simulation-integration; `engagement` is engagement-and-reward. **Unblocks** is what waits
on it.

---

### Stage S0 — Trust the foundation

Nothing new is built. The existing core is frozen so that everything after it can be believed.
Cheap, and it prevents the whole plan resting on assumptions.

#### BQ-001 — Freeze the foundation layer
Audit `EntityId`, `IdMinter`, `DeterministicRng`, `GameTime` against their invariants and add the
missing property tests: ids never reissue after reload, forked streams are reproducible, day
arithmetic survives long saves.
- **Done when** tests cover each invariant and pass; `Foundation` is declared Complete-until-launch.
- **Sources** MD §4.1, §23.1; PM §77.
- **Unblocks** everything — every later system keys on these.

#### BQ-002 — Complete the consequence table
Fill the gaps in `ConsequenceProfiles`: several `WorldEventType` values have no profile and silently
do nothing. Add profiles or explicitly document each omission in code.
- **Done when** a test asserts every `WorldEventType` either has a profile or is on a named exemption list.
- **Sources** MD §17.1; PM §12.
- **Unblocks** BQ-016, and every action added in S3.

#### BQ-003 — Capability audit and honest reporting
Verify each `VanillaCapability` actually reflects a working call, not an optimistic assumption.
`ReadHomeState` and `ObserveCrimeWitnesses` must stay off until their steps land.
- **Done when** a log line lists each capability with the concrete call that proves it.
- **Sources** LW §2.1, §2.4; PM §56.
- **Unblocks** BQ-030, BQ-014.

#### BQ-004 — Freeze the check layer
Confirm `CheckProfile`'s dice/crit/fumble fields are honoured on both resolvers, and that the native
path and the portable path produce the same outcome distribution for a single-element profile.
- **Done when** a test compares both resolvers over a fixed seed set and they agree within tolerance.
- **Sources** MD §10; PM §3.3; `elin-api-notes.md`.
- **Unblocks** all checked actions.

> **Checkpoint S0.** The simulation core is trustworthy and frozen. Any later bug is in new code.

---

### Stage S1 — One playable situation, end to end

Completes Gate A. The goal is not more content; it is that a single situation is genuinely
*playable* and survives everything a sandbox does to it.

#### BQ-005 — Harden Drama choice injection
Review the three Harmony patches in `DramaChoiceProjector` for failure behaviour: a patch that
cannot apply must disable procedural dialogue with a diagnostic, never break vanilla talk.
- **Depends** none (code exists).
- **Done when** forcing each patch to fail leaves vanilla dialogue fully working, with one warning each.
- **Sources** LW §2.2, §2.4; MD §19.3.
- **Unblocks** all later dialogue work.

#### BQ-006 — Difficulty in the game's own words
Route every offered choice's difficulty through `Check.GetText`, never a raw number or `[CHA 30]`.
- **Done when** offered options show vanilla difficulty wording in game.
- **Sources** MD §10.2; LW §3.3; CD §29.
- **Unblocks** BQ-070.

#### BQ-007 — Choices resolve once, against the right actor
Guard against double-resolution and against a choice resolving on a different NPC after the actor
list changes mid-conversation.
- **Done when** a scripted double-click and an actor swap both produce exactly one consequence.
- **Sources** LW §14 P0; CD §28.
- **Unblocks** BQ-083.

#### BQ-008 — Scene revalidation on interruption
Before each beat, revalidate participants, facts and available actions. Handle actor death, actor
departure, combat start, player departure and inventory change.
- **Done when** each interruption is exercised in game and none leaves a stuck or lying dialogue.
- **Sources** CD §28; LW §13.
- **Unblocks** BQ-065.

#### BQ-009 — Consequences visible in the world, not just the log
Every resolution the player can see must change something they can see: affinity emote, item moving,
an NPC reacting. No silent successes.
- **Done when** each of the twelve verbs has an observable in-game effect confirmed by a tester.
- **Sources** LW §3.2; MD §17; PM §12.
- **Unblocks** BQ-033.

#### BQ-010 — Full round trip under save/load
Make a choice, save, quit to desktop, reload, continue. State must be identical and consequences
must not re-apply.
- **Done when** a tester confirms the round trip and the log shows no duplicated events.
- **Sources** PM §3.5; LW §13; MD §19.
- **Unblocks** Checkpoint S1.

#### BQ-011 — Adapter write-safety review
Audit every write in `ElinVanillaState`: bounds, null actors, dead actors, missing capability. A
refused write must be reported, never partially applied.
- **Done when** a test drives each write with hostile inputs and none corrupts state.
- **Sources** LW §5; PM §13, §80.
- **Unblocks** BQ-031.

#### BQ-012 — In-game "why" inspector
A debug command or config-gated panel that answers, for the situation in front of the player: why it
exists, who is involved, what each knows, why each option is or is not available, and what check ran.
- **Done when** every question in `LW §12` is answerable in game without reading source.
- **Sources** MD §23.2; PM §76; LW §12; CD §42.
- **Unblocks** everything after — tuning without this is guesswork.

#### BQ-013 — Escalation visible in a live save
Let the staged theft escalate over real in-game days and confirm each milestone fires once, in
order, with world-visible effects.
- **Done when** a tester sees day-2 through day-14 milestones across a real play session.
- **Sources** MD §18; PM §72 stage 2.
- **Unblocks** BQ-039.

> **Checkpoint S1 — Gate A complete.** A player encounters one procedural situation in real Elin,
> understands it through native presentation, makes a meaningful choice using their actual build,
> sees the world change, saves, reloads, returns, and finds the world remembers correctly.
> This is `LW §19`'s definition of success. Do not start S2 until a human confirms it.

---

### Stage S2 — The world observes itself

Until now the mod only knows what it did. This stage makes ordinary Elin play produce narrative
facts. It is the single highest-leverage stage in the plan: it converts the whole base game into
content.

#### BQ-014 — Observe vanilla actions
Subscribe to `EVENT.ActPerformed` and translate the acts that matter (`ActSteal`, `AI_Steal`,
`ActPick`, `AI_OpenLock`, `ActChat`, combat resolution) into `WorldEvent`s.
- **Depends** BQ-003.
- **Done when** stealing something in normal play appends a `Theft` event with the real item.
- **Sources** PM §3.7, §25; LW §14 P1; `elin-api-notes.md`.
- **Unblocks** BQ-015, BQ-050, BQ-093.

#### BQ-015 — Witnesses from the real world
Derive witness lists from actual proximity, line of sight and Stealth rather than a caller-supplied
list. Nobody learns anything they could not have perceived.
- **Depends** BQ-014.
- **Done when** a theft with nobody present teaches nobody, and the same theft in a crowd teaches exactly the people present.
- **Sources** MD §5.3; PM §27; LW §3.1.
- **Unblocks** BQ-019, BQ-032, BQ-044.

#### BQ-016 — Consequences from observed events
Route observed vanilla events through `ConsequenceEngine` so affinity, Karma and memory respond to
what the player actually did, not only to procedural verbs.
- **Depends** BQ-014, BQ-002.
- **Done when** an ordinary murder in front of witnesses produces the same memory and affinity shape as the procedural `attack` verb.
- **Sources** PM §11, §25; MD §17.1.
- **Unblocks** BQ-022.

#### BQ-017 — Evidence is a real object
Bind evidence to actual `Thing` uids with provenance, so proof can be carried, shown, sold, planted
or destroyed. Retire abstract evidence tokens.
- **Done when** every fact's provability traces to an item that exists in the world or to a witness.
- **Sources** MD §5.1; PM §64; LW §17 rule 6.
- **Unblocks** BQ-085.

#### BQ-018 — Provability rules for authorities
Define what a guard, a guild or a court will act on: witnessed and provable, believed but
unprovable, or rumour. Unprovable accusations rebound.
- **Depends** BQ-017.
- **Done when** the same accusation produces different outcomes at each provability level, verified in game.
- **Sources** MD §5.2; PM §20, §64; CD §14.5.
- **Unblocks** BQ-044.

#### BQ-019 — Rumour circulation in a live town
Run `RumorSystem` against real town populations on a bounded schedule, with decay and no proof
transmission.
- **Depends** BQ-015.
- **Done when** a fact known to one witness reaches other townspeople over days, at falling confidence, without any of them being able to prove it.
- **Sources** MD §5.2; PM §18; LW §6.
- **Unblocks** BQ-035, BQ-037.

#### BQ-020 — Rumour distortion and false belief
Allow low-confidence retellings to mutate, and allow actors to deliberately spread claims they know
to be false.
- **Depends** BQ-019.
- **Done when** a false belief can propagate and be acted on, and the world still records the truth separately.
- **Sources** PM §18; CD §14, §14.5.
- **Unblocks** BQ-044, BQ-073.

#### BQ-021 — Memory consolidation in a long save
Exercise consolidation and decay against a save with thousands of events. Routine memories fold;
Defining memories never do.
- **Done when** a synthetic 200-hour ledger stays within a stated size budget and no defining memory is lost.
- **Sources** MD §19.2; CD §43.5; LW §13.
- **Unblocks** BQ-107.

#### BQ-022 — Relationships react to harm
Harm to an NPC propagates through the relationship graph: family, employers, guildmates and friends
respond according to their ties.
- **Depends** BQ-016.
- **Done when** hurting a shopkeeper measurably changes her brother's disposition without a scripted rule for that pair.
- **Sources** MD §6; PM §28; LW §6.1.
- **Unblocks** BQ-055, BQ-095.

> **Checkpoint S2.** Ordinary Elin play generates narrative facts. Witnesses know only what they
> could perceive. Rumours spread and decay. This is `PM §25`'s "convert Elin chaos into history".

---

### Stage S3 — Action library by mechanical leverage

Twelve verbs to roughly forty. Not to hit a number: each verb must unlock an Elin playstyle that
currently has no procedural route. A verb is not done until some situation can be solved *only*
through it.

Standing rule for every step here: preconditions gate impossibility, never low odds
(`MD §10.1`, `PM §62`). Each verb declares all four outcomes (`PM §46`).

#### BQ-023 — Economic verbs
`buy`, `sell`, `invest`, `pay_debt`, `purchase_debt`, `finance`, `hire`, `commission`,
`purchase_information`. Use real orens, real shop investment, real contracts.
- **Done when** a debt situation can be resolved by money alone, with no social check involved.
- **Sources** MD §9.1; PM §61; LW §8.
- **Unblocks** BQ-045, BQ-050.

#### BQ-024 — Investigation verbs
`inspect`, `track`, `read`, `translate`, `compare_testimony`, `examine_corpse`, `identify_substance`,
`search_records`, `eavesdrop`, `follow`.
- **Depends** BQ-017.
- **Done when** a situation can be solved entirely by evidence with no NPC ever telling the player anything.
- **Sources** MD §9.1, §12.1; PM §61; LW §8.
- **Unblocks** BQ-044, BQ-064.

#### BQ-025 — Crime verbs
`trespass`, `forge`, `fence`, `smuggle`, `sabotage`, `extort`, `destroy_evidence`, `impersonate`.
Thieves Guild membership and Karma gate access to contacts, not to the attempt.
- **Depends** BQ-015.
- **Done when** a criminal build has a complete route through a situation that a lawful build cannot take.
- **Sources** MD §9.1, §13.1; PM §61, §67.
- **Unblocks** BQ-044, BQ-046.

#### BQ-026 — Crafting and production verbs
`cook`, `brew`, `alchemy`, `repair`, `build`, `craft_to_property`. Prefer detecting real vanilla
production completion over abstract rolls.
- **Done when** a shortage can be answered by actually producing goods, and quality thresholds matter.
- **Sources** MD §9.1; PM §8, §23; LW §8.
- **Unblocks** BQ-050, BQ-051.

#### BQ-027 — Home and community verbs
`shelter`, `host`, `recruit_specialist`, `provide_supplies`, `assign_protection`, `store_evidence`.
- **Depends** BQ-030.
- **Done when** sheltering someone at Home changes both the world's disposition toward the player and Home's own state.
- **Sources** MD §13.7; PM §30, §49; LW §6.4.
- **Unblocks** BQ-048.

#### BQ-028 — Faith and magic verbs
Prefer actual spells, deity identity, piety, offerings and altars over a generic magic roll.
- **Done when** a Kumiromi worshipper has a route through an agricultural situation that another deity's follower does not.
- **Sources** MD §13.6; PM §69; LW §8.
- **Unblocks** BQ-047.

#### BQ-029 — Physical and world verbs
`clear_obstruction`, `carry`, `rescue`, `mine_bypass`, `break_barrier`, `transport`, `capture`,
`restrain`, `escort`.
- **Done when** a physical build can bypass a barrier that a social build must talk through.
- **Sources** MD §9.1, §12.1; PM §61; LW §8.
- **Unblocks** BQ-087.

> **Checkpoint S3.** The coverage matrix (`PM §6`, `LW §8`) shows every primary attribute and every
> major skill family with at least one real route. A skill counts as covered only when it changes
> what the player can *do*, never when it merely tags a dialogue line.

---

### Stage S4 — Vanilla surfaces and safety

Read the rest of the game, and establish the rules for changing it before anything starts changing
it. `LW §5` is the most safety-critical design text in the project; this stage implements it.

#### BQ-030 — Read Home state
Read residents, jobs, capacity, and the Home Skill elements (`fSafety`, `fMoral`, `fFood`, `fSoil`,
`fPromo`, `fAdmin`). Read-only.
- **Depends** BQ-003.
- **Done when** the adapter logs the player's real Home state and `ReadHomeState` reports available.
- **Sources** MD §13.7; PM §2; LW §14 P2; `elin-element-aliases.md`.
- **Unblocks** BQ-027, BQ-048.

#### BQ-144 — Read vanilla character identity *(stage S4, alongside BQ-030)*
Expose Elin's own answer to *who is this character* through the seam, as one read-only observation
with six separately typed facets: **character archetype** (the `SourceChara` kind — Little Sister,
Punk, Bunny), **race/species**, **work/occupation**, **hobby**, **service/commercial role**, and
**institutional role** (authority, guild, faction). This is the identity counterpart of what BQ-030
did for the Home and BQ-135 does for activity. It creates **no** BQ identity vocabulary and decides
nothing about what any facet means.
- **Depends** BQ-003.
- **Done when** `IVanillaState` returns a character identity observation for a live actor; each of the six facets is its own typed field carrying the game's own id verbatim, never a merged tag list and never an id BQ minted; a facet the build did not answer is `Unknown` rather than an empty string, `false`, `"local"` or a default occupation (`D017`); no Elin type name reaches Core (`D001`); the read has no side effects, registers nobody and mutates nothing; the existing guard/guild trait read becomes the institutional facet rather than a second write path into `NarrativeNpc.Roles`; a live diagnostic logs the full facet set for the loaded population of one real town — including at least one shopkeeper and one guard — and names every facet it could not read; and tests prove an unavailable member degrades only its own facet.
- **Current implementation** `IVanillaState.GetCharacterIdentity(EntityId)` returns a
  `CharacterIdentity`: `CharacterArchetype`, `Race`, `Work`, `Hobbies`, `Service` and
  `Institutions`, each its own typed field built through `CharacterIdentityBuilder` so that "not
  read" means the same thing in the live adapter and in `SandboxVanillaState`. A facet carries
  Elin's own id verbatim in `IdentityFacet.VanillaId` and nothing normalises, maps or mints one; an
  id the sheet did not give cannot become a known facet at all, because `IdentityFacet.FromVanilla`
  answers `Unknown` for an empty id. Hobbies and institutions distinguish *read and empty* from
  *never read*, since the sheet listing no hobbies is a fact and the adapter failing to look is not,
  and a service that is known carries `ServiceAvailability` — `Unknown` on a build with no
  open-state member, never a claim that the shop is shut.
  `ElinCharacterIdentity` does the reading: the `SourceChara` row id and `aka` for the character
  archetype, `Chara.idRace` and the race row, source `job`/`idJob`, source `hobbies`, the service
  trait subclasses (the trait type name carried through verbatim, so an unrecognised shop trait is
  still a service) and `TraitGuard`/`TraitGuildPersonnel`/`TraitGuildDoorman` plus `Chara.faction`
  for the institutional facet. Every facet is read inside its own guard, so a member this build
  renamed costs that facet and leaves the other five standing. No Elin type name reaches Core, the
  whole file is reads, and it registers, mints and materialises nobody.
  **The placeholder is gone rather than duplicated.** Registration no longer writes
  `Occupation = "local"` for every townsperson, a saved `"local"` is dropped on load, and
  `NarrativeNpc.Occupation` now means only what BQ itself authored. The guard/guild trait read that
  used to write `NarrativeNpc.Roles` directly is the institutional facet now:
  `ElinAuthorityRoles` interprets the observed office into an authority word once, and
  `AuthorityPolicy.Reconcile` is the single intake — it grants and withdraws on a facet that was
  read, and changes nothing at all on one that was not, so an off-map actor is never mistaken for a
  dismissed guard. Nothing about the observation is persisted.
  Identity is not an input to `NarrativeActorClass` or the mutation policy, and the seam test that
  pins every member as a read or a classified write lists it as a read.
  A live diagnostic logs the full facet set per loaded character on attach, then tallies the facets
  no character answered — "no shopkeeper in this town" and "this build cannot see shops" are
  different problems and look identical without it.
  Unproven until a live run: which source columns ordinary townspeople actually carry, whether a
  service trait exposes an open state, and per-character institutional rank, which stays unknown
  rather than zero because `FactionRelation.rank` is the player's own standing (`ELIN-Q-0025`,
  `ELIN-Q-0028`).
- **Out of scope** what a facet *means* (BQ-145), any write, persisting the observation, personality generation, mutation policy, and settlement residency — a populated `job` field does not answer ELIN-Q-0027 and must not be made to.
- **Sources** VS §4.2, §4.3, §4.4, §4.5, §7; CD §6.1; `docs/elin/bq-integration/world-affordances.md`; `docs/elin/api/actors.md`; D001, D004, D017, D019, D021.
- **Unblocks** BQ-145, and the identity reads in BQ-039, BQ-049, BQ-051, BQ-064, BQ-067, BQ-068, BQ-076, BQ-084, BQ-123.
- **Why its own step** one implementation site at the seam, with a completion test nothing else can run: the adapter answers or it does not. Every step in *Unblocks* is a different system reading the same answer, and folding the read into any one of them is exactly how four private reflective probes into `Chara` get written.
- **Retrofit, not greenfield.** `NarrativeNpc.Occupation` and `Roles` already exist and are persisted; the plugin registers every real townsperson with `Occupation = "local"`, and only the authority-trait read ever populates `Roles`. This step replaces that intake — it does not add a parallel one — and the persisted fields degrade to unknown and are re-read rather than being kept in sync (`VS §4.4`).
- **Risk** the evidence is `SOURCE-DATA` and `VERIFIED-METADATA`, not runtime. Which source columns are actually populated on ordinary townspeople is `VS §7` item 9, and the character-archetype handle is the least evidenced of the six.

#### BQ-031 — Mutation policy classification
Implement `NarrativeMutationPolicy` and classify every actor the mod can touch: story-critical,
unique service, ordinary citizen, generated.
- **Depends** BQ-011.
- **Done when** every mutation call site consults a policy, and story-critical NPCs are provably unkillable and unmovable by the mod.
- **Sources** LW §5, §5.1; PM §80.
- **Unblocks** BQ-032, BQ-093.

#### BQ-032 — Absence grades A and B
Grade A: a shop or service becomes unavailable while the actor remains. Grade B: the actor is
physically absent and represented in procedural state.
- **Depends** BQ-031.
- **Done when** a Grade B absence survives citizen refresh, zone unload/reload and save/load with **no duplication**, proven by a hostile test on a disposable save; the first live Grade-B subject is a disposable ordinary citizen demonstrably *not* participating in vanilla global travel; save/load and zone revisit reconcile against the actor's vanilla `currentZone` and materialization rather than against BQ binding state alone; and a candidate whose global or transition status cannot be read safely is refused Grade B rather than made the occasion for speculative integration.
- **Sources** LW §5.2, §5.3; PM §72 stage 3; VS §2.4, §3.3.
- **Unblocks** BQ-053, BQ-097.
- **Risk** the only step in this plan that can corrupt a save. Do not ship it enabled until it has survived a deliberately adversarial test.
- **Not this step.** Elin advances a `GlobalGoal` hourly for eligible global actors outside the active zone and can move them between zones on its own (`VS §2.4`), so BQ is not the only writer of a character's whereabouts. That is a reason to reconcile and to pick a subject outside it, not a reason to integrate with it here. Reading `GlobalGoal`, `GlobalData.transition` and `ZoneTransition` belongs to BQ-135 and BQ-097 unless this step proves it strictly required for safety. Keep BQ-032 about proving one absence is safe.

#### BQ-033 — Journal of known state
A journal that lists what the *player* knows, tagged Known / Reported / Suspected / Disputed /
Rumour. It never reveals hidden truth.
- **Depends** BQ-009.
- **Done when** a tester can read the journal and cannot deduce anything their character has not learned.
- **Sources** MD §3.2; PM §5; LW §3.3.
- **Unblocks** BQ-034, BQ-036.

#### BQ-034 — Chronicle of resolved history
A permanent record of what happened, distinct from open threads: who did what, what the player did
about it, what changed.
- **Depends** BQ-033.
- **Done when** a resolved situation leaves a readable history entry that survives save/load.
- **Sources** LW §3.3; PM §40, §41; CD §24.
- **Unblocks** BQ-082, BQ-086.

#### BQ-035 — Ambient rumour delivery
Surface known developments through barks, default talk and overheard conversation rather than
notifications. Sources speak only about what they actually know.
- **Depends** BQ-019.
- **Done when** the player can learn about a situation without any UI element announcing it.
- **Sources** PM §36, §37; LW §3.2, §3.3; CD §44.
- **Unblocks** BQ-039.

#### BQ-036 — "What's been happening?"
An optional dialogue topic on tavernkeepers, guards, residents and guild contacts exposing one to
three high-salience developments *they* know about.
- **Depends** BQ-033, BQ-035.
- **Done when** two NPCs in the same town give different answers because they know different things.
- **Sources** PM §37; LW §3.3.
- **Unblocks** BQ-038.

#### BQ-037 — Guild information routing
Route facts through guild membership: the same event reaches Fighters, Mages, Thieves and Merchants
differently, or not at all.
- **Depends** BQ-019.
- **Done when** a caravan robbery reads as a bounty to one guild and a fencing opportunity to another.
- **Sources** MD §13.5; PM §9, §50; LW §6.5.
- **Unblocks** BQ-045, BQ-046.

#### BQ-038 — Guild authority in dialogue
Guild rank becomes a usable social lever where the guild is relevant, using real membership and
contribution.
- **Depends** BQ-037, BQ-036.
- **Done when** a Fighters Guild member can resolve a monster situation through authority that a non-member cannot.
- **Sources** MD §13.5; PM §9.
- **Unblocks** BQ-047.

> **Checkpoint S4.** The mod can read the whole game, has explicit written rules for what it may
> change, and the player has a native, non-omniscient way to find out what is going on.

---

### Stage S5 — Situations the world generates

Until now every situation is staged by a debug flag. This stage makes the world produce them from
its own state, and adds the six archetypes that stress the parts of the architecture theft does not.

#### Playtest consolidation before generative expansion
Live S4 testing proved the simulation/action/knowledge primitives are viable, including staged
petty theft and observed ambient Gossip retellings, but also exposed defects that would multiply if
BQ-039 generated many more situations now: raw verb projection, context-free action semantics,
indiscriminate witness affinity, and player tracking that is still too log-centric. The route before
generation is therefore:

BQ-038 -> BQ-134 -> BQ-136 -> BQ-137 -> BQ-138 -> BQ-039 -> BQ-040 -> BQ-041 ...

This consolidation does not pull forward the deeper relationship/favor layer (BQ-055+),
personality/values/emotion/interpretation (BQ-056 ... BQ-064), speech/disclosure/dialogue
realization (BQ-070 ... BQ-078), or conversation state (BQ-083). Those remain later systems.

#### BQ-134 — Project verbs through contextual affordances *(moved forward after live S4 testing)*
Generalize player-facing action projection so registered verbs become contextual candidates grouped
by interaction surface and semantic intent family, not a flat Drama list or a hand-maintained switch
over action ids. BQ choices augment ordinary Elin interaction; they do not replace vanilla
Talk/service/trade/recruit/gift surfaces where vanilla would normally provide them.
- **Depends** BQ-008, BQ-024, BQ-025.
- **Done when** registered `NarrativeAction`s expose or can be mapped to a presentation surface and
  intent family; action discovery produces contextual candidates independently of UI; the
  presentation layer groups candidates into shallow nested menus; `Talk` no longer exposes the
  complete social/investigation/crime registry as one flat list; player-facing labels describe or
  imply intent, target and subject/object where relevant; at least one non-dialogue action projects
  through a non-`Talk` surface; menu contents depend on player knowledge and cannot reveal unknown
  facts or subjects; empty families are omitted; unnecessary single-option nesting is collapsed;
  large candidate sets remain navigable without silently discarding meaningful routes; selection
  revalidates changed world state before execution; the existing petty-theft Drama projection is
  migrated onto the generalized mechanism; focused tests prove grouping, knowledge filtering,
  empty-family suppression, single-option collapse and stale-state revalidation.
- **Out of scope** final authored prose, journal redesign, controller-specific visual polish,
  animation, personality, values, emotion, full social interpretation, complete speech acts,
  dialogue realization, and complete taxonomy coverage for every future verb.
- **Sources** CD §29, §29.5, §30; D016; PM §61; LW §3.4; live S4 playtest.
- **Unblocks** BQ-136, BQ-137, BQ-138, BQ-070, BQ-083, BQ-093.

#### BQ-136 — Relationship-aware witnessed consequences
Gate witnessed social consequences by relevance so witnesses may react when an event matters to
them, but mere presence does not automatically impose the same affinity loss on every observer.
- **Depends** BQ-015, BQ-022, BQ-134.
- **Done when** threatening a stranger in front of an unrelated stranger normally produces no witness
  affinity effect; a witness with a meaningful relationship/tie, thread participation or direct
  stake can react; direct-target consequences remain intact; and the petty-theft regression no
  longer produces universal affinity loss merely from presence.
- **Out of scope** personality, values, morality, ideology, emotional state, cultural norms, and the
  full later social interpretation layer.
- **Sources** PM §38; live S4 playtest.
- **Unblocks** BQ-137, BQ-039.

#### BQ-137 — Purpose-bearing action bindings / semantic action requirements
Ensure player-facing action invocations carry the semantic data needed to mean something and produce
a real postcondition: propositions for persuasion, demands for intimidation, destination or
protective purpose for escort, meaningful objectives for restrain/capture, concessions for bribes,
matter plus authority for reports, and item plus claimant for returns.
- **Depends** BQ-023 ... BQ-029, BQ-134, BQ-136.
- **Done when** no purpose-bearing verb is projected without required semantic data; successful
  persuasion records what was agreed to; escort has a destination/purpose or is not offered;
  restraint/capture has meaningful persistent state or is not offered; self-incriminating direct
  admission is not represented as ordinary hearsay; culprit questioning no longer trivially reveals
  hidden truth without an explicit disclosure path; and staged theft still has multiple viable
  solution routes.
- **Out of scope** the future full disclosure/personality system, physical movement fakery, and the
  complete BQ-070+ speech-act/dialogue stack.
- **Sources** CD §17, §29; PM §38, §61; live S4 playtest.
- **Unblocks** BQ-138, BQ-039, BQ-070.

#### BQ-138 — Native BQ journal surface
Advance the player-facing journal from log-centric notifications to a bounded native Elin journal
surface if the verified `LayerJournal`/`Window` API supports it safely, while preserving BQ-033 and
BQ-034 Core projections as authoritative.
- **Depends** BQ-033, BQ-034, BQ-134, BQ-137.
- **Done when** opening Elin's journal exposes BQ content for active matters and resolved history;
  active theft appears; only player-known information appears; save/reload recreates the same
  projection; reopening the journal does not duplicate tabs/content; vanilla journal tabs still
  work; failed BQ initialization falls back to existing log/Msg routes; and no duplicate quest
  database or persisted prose is introduced.
- **Fail-safe** if native integration cannot be completed against the current verified API, produce
  a bounded spike that leaves vanilla journal untouched, preserves the fallback route, documents the
  remaining runtime question, and does not guess reflection signatures.
- **Current implementation** BQ-138 consumes the successful `JournalShapeProbe` run and injects
  one native Brilliant Questing tab before `Window.BuildTabs(int)`. The tab derives from existing
  BQ-033 journal and BQ-034 Chronicle state, hides dialogue Journal/Chronicle fallbacks while the
  native patch is available, and fails closed back to log/Msg diagnostics if native setup breaks.
- **Sources** BQ-033, BQ-034; `docs/elin/api/journal-ui.md`;
  `docs/elin/bq-integration/ui-surfaces.md`; `docs/elin/verification/api-status.json`; live S4
  playtest.
- **Unblocks** BQ-039.

#### BQ-039 — Situations arise from world state
Replace debug staging with generation from actual pressure: an actor with a motive, means,
opportunity and target. No situation exists because the player needs one.

Candidate input is a **local affordance profile** derived from vanilla state already readable, not
just an actor and a motive: occupation, job and hobby; service role; authority, guild and faction
role; who is currently present; sites and work infrastructure; recent events and recorded history;
and current local pressures. A place earns its situations from what it actually supports.
- **Depends** BQ-013, BQ-035.
- **Done when** a situation appears in a save the player has never staged anything into, and the inspector can name the world state that caused it; **and** two structurally different settlements yield different candidate distributions, with no town id, zone name or hand-tuned per-place weighting anywhere in the generator.
- **Sources** MD §8.1; PM §0, §27; LW §1; VS §5.2.
- **Unblocks** all archetypes.
- **Identity intake is BQ-144's, not this step's.** The affordance profile's "occupation, job and hobby; service role; authority, guild and faction role" are the six facets of `VS §4.2`, read once at the seam. This step shipped ahead of that read with a placeholder intake — `ActorAffordances.Occupation` is the literal string `"local"` for every registered vanilla actor, and `Roles` carries only the guard/guild traits — so its identity terms are weaker than the design says, not absent. When BQ-144 lands, this generator consumes the typed facets in place of the placeholder and its scoring shape does not change. It must not grow its own read of `Chara`, its own occupation vocabulary, or a second copy of what a job implies (that is BQ-145). **Retrofitted with BQ-145:** `ActorAffordances` carries the derived `IdentityAffordances` and reads `IsCommercial` off its service capability; `ReadsAsCommercial` and its list of trade words are gone, and BQ-115's early-contact pass reads the same derivation, so the two cannot disagree about who a shopkeeper is.
- **Not a dependency.** This step must *not* wait on BQ-135, and it does not wait on BQ-145 either: generation consumes identity as observation — who could plausibly be involved, what can be lost, who is entitled to act — not as derived character meaning. Stable world affordances are enough to generate from; transient timetable and current-goal data is later enrichment. The affordance profile is a product requirement rather than an adapter detail — it is what makes a palace-and-merchant city produce authority and fraud stories because its state supports them, instead of because somebody wrote the city's name into a table.
- **Current implementation / BQ-039a hardening.** The first cut met the done-when criteria with a
  structure shaped entirely like theft, which BQ-040 through BQ-047 would have inherited. BQ-039a
  keeps the model — world state → candidate evaluation → vanilla mutation → narrative situation —
  and generalises what sits inside it.
  - *Local affordances are structured.* `LocalAffordanceProfile` was a resident list plus summary
    strings; it now carries a typed `ActorAffordances` per local — money, occupation, roles,
    commercial relevance, carried items, the skills and attributes in `AffordanceReads`, and
    presence — plus local aggregates (median purse, total carried value). It observes and never
    mutates, and holds no archetype's thresholds.
  - *Generic candidates are no longer theft-shaped.* `SituationCandidate` was
    actor/target/witness/item fields. It is now named role bindings (actor, target, witness, stake,
    place), named pressure terms that sum to the score, and causes — so a shortage naming a supplier
    and a town, or a caravan naming a route, needs no second redesign. `PettyTheftCandidate` is a
    lens over those bindings for readability.
  - *Theft scoring is separate from affordance extraction.* `PettyTheftPressure` holds every theft
    weight, each named and commented, in one place ready to become data. The generator orchestrates
    and owns no arithmetic.
  - *Opportunity is derived rather than constant.* It was a flat `+12`. It is now read from
    bystander count, the bound witness's Perception and SpotHidden, and whether the mark is occupied
    with trade — all things the game can be asked for today. There is deliberately no schedule,
    room-visibility or current-goal term, because BQ cannot read those reliably yet.
  - *Unwitnessed theft is supported.* A witness binding is optional and the role takes several, so
    multi-witness needs no redesign. Nobody is taught a fact they could not have seen, and the
    `witness_talks` escalation only exists when somebody was there. Where bystanders exist, the
    witness is ranked by attention with a world-seed tie-break rather than taken from collection
    order.
  - *Duplicate causal configurations are suppressed.* Same archetype, perpetrator, victim and stake
    is refused while an unresolved thread holds those two, or while the ledger records that theft
    within `RepetitionWindowDays`. It reads existing threads and the event ledger — no second
    history — and each refusal carries its reason for the inspector. A distinct pairing stays
    eligible. This is repetition suppression, not the BQ-099 director.
  - *Generation is still bootstrap-triggered.* It fires once, on attach, in whichever zone the save
    resumed in, guarded so a reload cannot roll for another. Reactive triggers on world change are
    the director's work, not this step's; firing on every zone entry would make generation a
    function of where the player walks. This is a known temporary limitation, not final behaviour.
  - *Known limitation.* Participant eligibility is still only `NarrativeActorClass`, which answers
    how far the mod may reach into somebody and returns `OrdinaryCitizen` for anything the story
    flags do not claim — wildlife and passing hostiles included. No verified read distinguishes a
    settled townsperson; see ELIN-Q-0027. Nothing is guessed here in the meantime.

#### BQ-040 — Four content classes
Distinguish Request, Situation, Opportunity and Event in code and in presentation. Not everything
becomes a quest entry.
- **Depends** BQ-033.
- **Done when** one causal chain produces all four, and only the Request appears on a board.
- **Current implementation** BQ-040 derives `Situation`, `Request`, `Opportunity` and `Event`
  entries from authoritative threads, facts and ledger events. Request-board projection filters the
  same derived content to requests only; no persisted quest entry/prose database is introduced.
- **Sources** PM §5; LW §4; VS §5.3.
- **Unblocks** BQ-041 … BQ-047.
- **Note** an observed vanilla change is allowed to stay an Event or an Opportunity. A traveling actor arriving is an Event; an old debt with a local merchant may make it an Opportunity; only persistent or conflicting pressure earns promotion to a Situation. Nothing is promoted because it was noticed.

#### BQ-041 — Archetype: shortage / supplier failure
Economy, demand, production, investment, caravans.
- **Depends** BQ-023, BQ-026, BQ-050.
- **Done when** a town shortage can be resolved by producing, buying, investing, escorting or ignoring, with different world outcomes.
- **Current implementation** BQ-041 uses the same `needs` facts established by BQ-026 for
  active solutions. Producing or home supplies cover the symptom; buying spends real money;
  investment funds and supersedes the damaged supplier cause; escorting a bound caravan resolves
  delivery without creating goods; ignoring the shortage lets the archetype escalation record
  harm and civic deterioration while demands remain unmet.
- **Sources** PM §7, §73; LW §9.1.

#### BQ-042 — Archetype: fugitive / sanctuary
Home, Karma, witnesses, trust, consequences arriving at the player.
- **Depends** BQ-027, BQ-048.
- **Done when** sheltering a fugitive at Home can later bring guards or creditors to the player's land.
- **Current implementation** the hunted-witness sanctuary archetype now includes a guard actor with
  existing authority standing. When low or unread Home Public Safety lets a resident undertaking
  leak, the escalation teaches the pursuer and guard where the witness went, moves the generated
  guard to the player's Home through the existing relocation seam, and records an `InquiryOpened`
  event at Home only if the move succeeds. Broader arrest, Karma, creditor and legal judgement
  behavior remains later authority/economy work.
- **Sources** PM §30, §74; LW §9.2.

#### BQ-043 — Archetype: missing person / failed caravan
Travel groups, sites, evidence, rescue. The findable place may begin as a persistent micro-site; once
BQ-139 exists, it should be expressible as a procedural scenario dungeon/investigation site whose
layout follows the caravan's true history rather than a generic camp.
- **Depends** BQ-087, BQ-097.
- **Done when** a caravan that actually failed off-screen produces a findable site with real cargo.
- **Sources** MD §24.2; PM §34, §73; LW §9.3; PP §4.

#### BQ-044 — Archetype: false accusation
Truth versus belief, testimony, rumour, framing. The archetype that most exercises the knowledge graph.
- **Depends** BQ-018, BQ-020, BQ-024, BQ-025.
- **Done when** an innocent NPC can be convicted on planted evidence, and the truth remains recoverable afterwards.
- **Current implementation** the false-accusation archetype composes existing crime,
  investigation and authority verbs. `frame` now upgrades a planted item into a false `stole`
  claim when that item is tied to a true theft fact, while preserving the older false-possession
  fallback for ordinary objects. A guard can act on the planted physical proof against the
  innocent NPC, and the original true theft fact stays in the graph and can later be recovered
  through `search` and reported. This records institutional action, not physical arrest or
  autonomous enforcement.
- **Sources** PM §20, §27, §64; LW §9.4; CD §14.

#### BQ-045 — Archetype: debt / distressed business
Money, investment, coercion, shop continuity.
- **Depends** BQ-023, BQ-037, BQ-051.
- **Done when** a failing shop can be saved, bought, extorted or allowed to fail, and its later state reflects which.
- **Sources** PM §17, §29; LW §9.5, §6.3.

#### BQ-046 — Archetype: bounty / recognized violence
Combat context versus combat result, authority, witnesses, guilds.
- **Depends** BQ-016, BQ-025, BQ-037.
- **Done when** the same death is read as murder, self-defence or lawful bounty depending on who saw it.
- **Sources** PM §11, §20; LW §9.7.

#### BQ-047 — Archetype: festival / competition
Public events, ordinary skills, NPC participation, non-crisis play. The tonal counterweight.
- **Depends** BQ-026, BQ-028, BQ-038.
- **Done when** an NPC can win a competition the player entered, and that result is referenced later.
- **Sources** PM §15, §47, §75; LW §9.6, §10.4.

> **Checkpoint S5.** Seven archetypes exist, the world generates them unprompted, and each exercises
> a different part of the architecture. `PM §70`'s rule holds: every archetype exposes at least three
> genuinely different solution families.

---

### Stage S6 — Home, economy, organizations

The systems that make situations connect to each other rather than standing alone.

#### BQ-048 — Home as a consequence surface
Home capacity, beds, food, Public Safety and Public Morality gate what the player can offer, and
what it costs them.
- **Depends** BQ-030, BQ-027.
- **Done when** low Public Safety measurably raises the risk of a sheltered fugitive being discovered.
- **Current implementation** sheltering already spent Elin's resident roll through BQ-027. BQ-048
  now makes the Home's own Public Safety decide whether that undertaking closes cleanly or remains
  a later consequence: a safe Home resolves the hunted-witness thread, while a low-safety or
  unread-safety Home keeps the thread live and schedules a discovery step. The escalation handler
  records the hunter learning where the witness went, creates a fresh `at_risk` condition, and
  records a threat at the Home without writing Home metrics, moving actors, or adding a parallel
  settlement safety system.
- **Sources** MD §13.7; PM §30, §49; LW §6.4.

#### BQ-049 — Residents as narrative actors
Home residents carry goals, bring problems, and can be the origin of situations.
- **Depends** BQ-048.
- **Done when** a resident generates a situation without any external trigger.
- **Sources** MD §13.7; PM §10; LW §6.4.
- **Identity comes from BQ-144.** A resident's work, hobby, race, character archetype and any institutional standing are read through the one identity observation, never inferred from the fact that they live here and never stored on the resident. Their *doing* the work stays vanilla's (`VS §2.2`, §2.3); what BQ adds is what that work makes plausible and what its loss would cost. BQ-123 extends this step to pets and companions and shares the same read.

#### BQ-050 — Coarse local demand
Track Food, Alcohol, Medicine, Lumber, Textiles, Weapons, Luxury, Labor and Safety as pressures, not
as a commodity simulation. Categories, never quest-only item ids.
- **Depends** BQ-014, BQ-023.
- **Done when** delivering goods during a shortage shortens the shortage rather than only completing a counter.
- **Sources** PM §7, §7.1, §7.2; LW §6.6; VS §6.
- **Note** pressure is narrative, not a commodity simulation. Routine daily consumption is not modelled and must not be inferred from vanilla activity — a town that eats breakfast is not a town under pressure.

#### BQ-051 — Shop and service continuity
Businesses occupy states — Normal, Struggling, ShortOnStock, OwnerAbsent, TemporarilyClosed,
ReplacementOperator, Recovered, Failed, Inherited — projected through real stock and dialogue.
- **Depends** BQ-050, BQ-032.
- **Done when** a shop the player let fail is still failed a month later, with a visible consequence, **and** an operator who is merely asleep, at a hobby or off-shift produces at most ordinary temporary unavailability rather than a business state.
- **Sources** PM §17; LW §6.3; VS §5.1, §6.
- **Note** the visible surface is real service presence and the operator's actual state. A sleeping shopkeeper is not a distressed business; the states in this step represent continuity problems, not the working day.
- **The service/commercial role is BQ-144's facet**, and this step reads it rather than maintaining its own list of who runs what. Being a shopkeeper by kind and having a usable service right now are different answers and the facet keeps them apart; where the build cannot tell them apart the answer is unknown, and unknown is not a business state.

#### BQ-052 — Thread lifecycle hardening
Threads merge, go dormant, reactivate, and are inherited when a participant dies. A malformed thread
is quarantined, never poisons the save.
- **Depends** BQ-039.
- **Done when** killing every participant of a live thread leaves a coherent world and a quarantined or inherited thread.
- **Sources** MD §8.2; PM §32; LW §13.

#### BQ-053 — Organizations act
Generated organizations gain resources, leadership, goals and the ability to change state off-screen.
- **Depends** BQ-032, BQ-052.
- **Done when** an organization's wealth or membership changes because of something it did, not something the player did.
- **Sources** MD §7; PM §26; LW §6.8.

#### BQ-054 — Inter-organization relations
Organizations hold relationships with each other and with vanilla guilds, and those change from events.
- **Depends** BQ-053.
- **Done when** a raid by one group measurably worsens its standing with another, with no player involvement.
- **Sources** MD §7; PM §23.

#### BQ-055 — Social obligations and favors
Debts, promises, sponsorships, sanctuary and grudges as first-class records — not a second affinity meter.
- **Depends** BQ-022.
- **Done when** an NPC does something for the player they would otherwise refuse, explicitly because of a recorded favor.
- **Sources** PM §22, §29; LW §6.2; CD §28.5.

> **Checkpoint S6.** Situations connect. An economic pressure can cause a crime; a crime can close a
> shop; a closed shop can displace a person; a displaced person can arrive at the player's Home.

---

### Stage S7 — The character expression layer

`character-dialogue-system.md` in full. The order matters more here than anywhere else: personality
must change **decisions** before it changes **wording**, or the whole layer becomes adjectives on
boilerplate. Its Phase A–J maps onto BQ-056 … BQ-086, with BQ-134 inserted before semantic speech
acts to turn the growing verb library into a player-facing contextual interaction grammar.

Note on naming: `Scene` collides with an Elin type. Use `NarrativeScene`. Check every new type with
`dotnet run --project tools/ApiDump -- --find <Name>` before adopting it.

Note on reuse: `NarrativeNpc`, `KnowledgeRecord` and `RumorSystem` already exist and are persisted.
Extend them; do not build parallel stores.

**Read the content-pipeline track before starting BQ-065.** BQ-129 … BQ-133 sit immediately ahead of
the storylet engine and decide the form its content takes. Storylets, fragments and speech acts are
authored data, not C#; writing the first five storylets in code and extracting them later is a
migration this plan does not budget for.

#### BQ-056 — Behavioural dimensions
Replace the nine ad-hoc weights with the independent continuous dimensions in `CD §5.1`
(Bold↔Timid, Warm↔Aloof, Honest↔Deceptive, …), migrating existing saves.
- **Done when** a save migration test proves old personalities map forward without loss.
- **Sources** CD §5.1; MD §22.

#### BQ-057 — Problem-solving style
Weighted preferences: Confront, Avoid, AskAuthority, PaySomeone, Manipulate, UseViolence, Conceal, Wait…
- **Depends** BQ-056.
- **Done when** the same missing-goat problem causes five personalities to choose five different responses, asserted in a headless test.
- **Sources** CD §5.3, §48 M1.
- **This is Milestone 1.** Do not proceed to dialogue work until it passes.

#### BQ-058 — Sensitivities
What a person reacts strongly to: public embarrassment, unpaid debt, threats to family, animals, status.
- **Depends** BQ-056.
- **Done when** two otherwise identical NPCs react differently to the same event because of one sensitivity.
- **Sources** CD §5.4.

#### BQ-059 — Contradictions
One durable contradiction on significant NPCs — honest except about family, greedy but refuses to profit from medicine.
- **Depends** BQ-058.
- **Done when** a contradiction produces a decision the personality alone would not predict.
- **Sources** CD §5.5, §10.5.

#### BQ-060 — Quirks, sparse and sticky
Rare, persistent, never rerolled. Distribution target roughly 55 / 25 / 15 / 5 ordinary to unforgettable.
- **Depends** BQ-056.
- **Done when** an NPC's quirk is still the same one after save, reload and thirty in-game days.
- **Sources** CD §5.6, §23; MD §22.

#### BQ-061 — Values and needs
`ValueProfile` — family, wealth, law, faith, status, animals, knowledge, freedom — with importance
and flexibility, plus ordinary needs (hungry, bored, jealous, wants promotion).
- **Depends** BQ-057.
- **Done when** an actor's goal changes because a value was threatened, traceable in the inspector.
- **Sources** CD §10.5, §10.6; VS §6.
- **Identity is a prior, not a value profile (BQ-145).** An observed work, service or institutional role supplies plausible stakes — a livelihood, a rank to lose, a dependant, a shop that could close — which this pipeline then weighs against everything else the actor is. It never sets a value's importance directly, and two actors with the same job must be able to hold opposite values. **Checked with BQ-145:** this step reads no occupation at all — `ValueProfile`, the sensitivities and the goal pipeline take no identity input, and `IdentityAffordanceTests` pins that structurally rather than leaving it to inspection.
- **Not this** vanilla bodily and activity needs — hunger, bladder, sleep, routine work and hobby — are Elin's and are already simulated by `GoalNeeds` and the timetable. The needs here are narrative: safety, belonging, debt relief, status, loyalty, justice, secrecy, revenge, protection, material shortage, obligation. Do not build a second hunger or sleep model.

#### BQ-062 — Goal formation pipeline
World state → need → values and sensitivities → desires → candidate goals → candidate actions →
personality weights → chosen action. Every link inspectable.
- **Depends** BQ-061.
- **Done when** the inspector shows the full chain for one NPC's chosen action.
- **Sources** CD §10.5; MD §21; VS §3.1.
- **Unblocks** BQ-093.
- **Note** a BQ goal may be *informed* by physical state where it is readable — a starving actor is more persuadable — but must not mirror it. The pipeline produces narrative intention; embodiment is vanilla's (`D021`).

#### BQ-063 — Emotional state
Transient anger, fear, shame, grief, relief, suspicion, affection, stress — biasing action selection
and disclosure, then decaying.
- **Depends** BQ-062.
- **Done when** the same NPC answers the same question differently when angry, and returns to baseline over time.
- **Sources** CD §8.

#### BQ-064 — Actor-local interpretation
The same event means different things to different observers, by occupation, values and knowledge.
A dead crop is soil trouble to a farmer and contamination to an alchemist.
- **Depends** BQ-063, BQ-024.
- **Done when** three observers derive three different facts from one piece of evidence.
- **Sources** CD §9, §39; PM §39; LW §3.4.
- **Occupational interpretation reads BQ-145.** "A dead crop is soil trouble to a farmer and contamination to an alchemist" is an identity read, and this is the step where identity earns the most for the least risk — interpretation is already actor-local and already expected to disagree. It consumes the derived affordances, not `Chara` and not a private occupation table. **Retrofitted with BQ-145:** `ActorLocalInterpreter` weighs `IdentityAffordances.PlausibleKnowledgeOf` and `IsEligibleFor`, its occupation-substring table and role list are gone, and each identity-derived score term names the facet behind it.

#### BQ-065 — Storylet engine
`StoryletDefinition` with preconditions, roles, beats and consequence hooks. Storylets dramatize
facts; they never author truth.
- **Depends** BQ-008, BQ-064.
- **Done when** a storylet fires on the existing theft with no new facts invented, and refuses to fire when its preconditions lapse.
- **Sources** CD §11, §36.5.

#### BQ-066 — First five storylets
`PublicAccusation`, `PrivateConfrontation`, `RequestForHelp`, `Confession`, `Gossip`.
- **Depends** BQ-065.
- **Done when** all five can fire on the theft scenario and produce structurally different scenes.
- **Current implementation** the five live only as content (BQ-131) and are cast semantically
  (BQ-067); this step is the proof that they are five scenes rather than one scene with five
  vocabularies. `FirstFiveStoryletTests` plays all five against the laboratory theft from a cast
  taken from the place alone — no `Actor`, no `Target`, so nothing is proved about a caller's own
  arithmetic — and holds the difference where Core can see it: no two ask for the same set of
  roles, no two share a beat, no two share a consequence hook, and each is filed under its own
  situation and tone. The difference is load-bearing rather than decorative because the engine
  refuses on it: with the ownership record lapsed, the two scenes built around somebody's loss
  cannot cast an injured party and stop, while the three that are not about the loss still play.
  The other half is the rule the storylets exist under — they dramatize what the world already
  holds and never author it. A theft no longer true, a focus no longer in the thread and the only
  witness two towns away each stop every one of the five, and playing all five adds no fact, no
  event and no new knower of the theft.
- **Sources** CD §12, §38 Phase D.

#### BQ-067 — Casting engine
Temporary roles — Accuser, Accused, Witness, Mediator, Confidant — cast from who actually qualifies.
Roles are never identities.
- **Depends** BQ-065.
- **Done when** the same storylet casts different actors in two different towns and both read correctly.
- **Current implementation** `StoryletCasting` fills each declared role from the people the thread
  and the place actually hold, against the requirement the role names — knowledge, proof, ownership
  of what is at issue, standing — and rejects the dead, the absent, the socially incapable, anything
  the registry does not know as a person, and anybody already holding another role in the same
  scene (`D026`). Bindings live on the firing, never on an actor.
  Because this step was skipped past, the storylet content authored at BQ-131 had been written
  against positional binding and carried two defects it could not have caught: the corroborating
  knower of `public_accusation` and `gossip` resolved to the accused, and the injured party of
  `request_for_help` and `confession` resolved to the stolen item rather than its owner — which
  `BQ-105`'s save integrity check would have quarantined the thread over. The five storylets now
  name qualified sources, and a corroborator the world may not have is optional rather than
  required. Selection among the qualified stays unscored here; that is BQ-068.
  **Retrofitted with BQ-068:** this step's requirements, negative checks and pool order are
  unchanged and still run first — chemistry only chooses among groups every rule above has
  already accepted, and the group it falls back to is this step's first-in-a-stable-order
  answer. The one behavioural addition is backtracking: where taking the obvious person for
  one role left a later role with nobody, the search now tries the next qualified person
  instead of reporting the scene uncastable. Nobody enters a role they do not qualify for by
  that route.
- **Identity is eligibility only (BQ-144, BQ-145).** A role may *require* an observed facet where it genuinely needs one — an Authority who is actually entitled to act, a Service operator who really runs the shop — checked against the game's answer and failing closed when the facet is unread (`D017`). It is never a preference for a character kind: nobody is a better Accuser for being a Punk. Roles remain temporary and are never identities (`CD §3` principle 1), and the requirement lives on the role, not on the actor.
- **Sources** CD §13, §3 principle 1; CD §6.1.

#### BQ-145 — Identity affordances: one derivation of what identity implies *(stage S7, immediately before BQ-068)*
Turn BQ-144's observed facets into the typed, BQ-owned affordances the rest of the simulation
consumes — plausible knowledge domains, plausible interests, role eligibility, service capability,
and the stakes an identity exposes — in **one** place, so that "a brewer plausibly knows who buys
ale here" is written once rather than re-derived inside generation, interpretation, casting and
vocabulary.
- **Depends** BQ-144, BQ-062.
- **Done when** a single Core component derives identity affordances from a BQ-144 observation and every identity-consuming system reads that component rather than the raw observation or the game; the derivation adds no fact to the knowledge graph and grants nobody knowledge — it says what is plausible to ask and to be at stake, never what is true or known; a headless test proves two actors with identical identity and opposite personalities choose different actions, **and** two actors with identical personality and different identity differ only in what is plausible, eligible and at risk; a test asserts that BQ-056 … BQ-060 and the BQ-031 mutation policy take no identity input; an unknown facet contributes nothing rather than a default; and the inspector names the facet behind every identity-derived weight.
- **Current implementation** `IdentityAffordances` (Core, `World/`) is the one derivation.
  `Derive(CharacterIdentity)` answers from a BQ-144 observation alone;
  `Of(NarrativeNpc, IVanillaState)` is the canonical read for a live pass and adds only what BQ
  itself authored about somebody *where the game said nothing*, marked
  `IdentityOrigin.Authored` everywhere it appears so a report never passes BQ's own authorship off
  as Elin's answer. The output is five typed things and nothing else: plausible knowledge and
  plausible interests over a closed `IdentityDomain` (cultivation, alchemy, craft, trade, public
  order — one member per consumer that exists, not a taxonomy), `IdentityRole` eligibility
  (authority, guild standing, service operator), `IdentityStake` exposure (livelihood, business,
  standing), and a derived `IdentityServiceCapability` that keeps *is a provider* and *can serve
  right now* apart on the same terms the seam does. Every one of them carries the
  `IdentityFacetReference` — facet kind, origin, the id verbatim — that produced it, and
  `ExplainKnowledge`/`ExplainEligibility` name the facet even for a weight of zero, so a term that
  did not fire is as attributable as one that did.
  **Race and character archetype derive nothing at all.** They are the two facets a stereotype
  arrives through, and neither answers what somebody can do, is entitled to, or would lose; a
  character described only as a Punk and a fairy derives `IdentityAffordances.Nothing`, which is a
  complete answer. An unread facet likewise contributes nothing and costs only its own affordances.
  **Consumers moved onto it rather than keeping private copies.** `ActorLocalInterpreter` (BQ-064)
  lost its occupation-substring table and its role list and now weighs
  `PlausibleKnowledgeOf`/`IsEligibleFor`, naming the facet in every score term.
  `ActorAffordances.IsCommercial` (BQ-039) and `EarlyContacts` (BQ-115) lost `ReadsAsCommercial`
  and read the derived service capability, so the generator and the early-contact pass cannot
  disagree about who a shopkeeper is. `AuthorityPolicy.RoleWordsFor` translates derived eligibility
  into the role words the policy already speaks, and `ElinAuthorityRoles` is plumbing now — the
  adapter holds no identity vocabulary at all, and which office counts as which standing is decided
  once, in Core, where it can be exercised with no game attached. `NarrativeInspector`
  `DescribeCharacter` prints the derived affordances with their facets, or says none were derived.
  Nothing derived is persisted, nothing here touches the knowledge graph, and
  `IdentityAffordanceTests` pins the gate in both directions: identical identity with opposite
  personalities chooses different actions, identical personality with different identity differs in
  plausibility, eligibility and stakes and in nothing else, and a structural test walks BQ-056 …
  BQ-060 and the BQ-031 mutation policy for any member that takes, holds or exposes an identity
  type.
  Deferred: no domain arrives without a consumer, so the vocabulary is deliberately five domains
  matched by substring over Elin's own ids — an unrecognised trade derives nothing rather than
  being mapped onto the nearest familiar thing — and institutional rank, settlement-level facet
  distribution (`VS §5.2`) and identity deltas as events (`VS §5.3`) are not derived here.
- **Out of scope** persisting anything derived, dialogue wording (BQ-076 consumes this, it is not this), and any new read of Elin — this step consumes BQ-144's observation and nothing else.
- **Sources** CD §6.1, §9, §13.1, §17.6; VS §4.3; D017, D021.
- **Unblocks** the identity slices of BQ-068, BQ-076, BQ-084, and the retrofits named in BQ-061, BQ-064 and BQ-067.
- **Why its own step** the observation and its meaning fail differently. BQ-144 is wrong when the adapter reports the wrong facet; this is wrong when a facet is allowed to dictate a decision. Its done-when is the anti-stereotype test, which cannot be written against an adapter and must not be scattered across six consumers.
- **This is the anti-stereotype gate.** Identity affects plausibility, eligibility and pressure; it never dictates personality. A Punk is not aggressive because they are a Punk. Any later step that reads a facet and concludes what somebody is *like* is wrong even if it works.

#### BQ-068 — Role chemistry
Score groups, not individuals: goal conflict, shared history, knowledge asymmetry, power asymmetry.
- **Depends** BQ-067, BQ-145.
- **Done when** a proud debtor and proud former friend outscore an indifferent pairing, and the resulting scene is visibly better.
- **Current implementation** `StoryletCasting` forms whole groups before anything is scored, and
  `StoryletChemistry` chooses among them. Eligibility is untouched and runs first: each searched
  role gets a shortlist of the people who meet its requirement, in BQ-067's own pool order, and a
  group is a complete assignment of those shortlists with the one-role-per-person rule intact — so
  no score can put an ineligible actor into a role, and a storylet nobody qualifies for stays uncast
  however good the chemistry would have been. Group formation adds exactly one thing to BQ-067:
  backtracking, so that taking the obvious person for the first role no longer leaves a later role
  with nobody it could have had. The search is depth-first in the unscored engine's order, which
  makes the first group reached identical to what BQ-067 would have cast — it is always weighed, it
  wins every tie (within an epsilon, so the last bit of a double decides nothing), and it is the
  fallback in a town where nothing distinguishes anybody. Both bounds are stated on the constants:
  a shortlist is at least one longer than the number of searched roles, so the fallback group is
  provably reachable, and at most 128 complete groups are weighed per pass.
  The model is four dimensions and no fifth term. **Goal conflict** reads BQ-056 … BQ-060's own
  goals — one aimed at the other person, or two unsatisfied goals over the same subject, worth more
  when the aims differ than when they merely coincide. **Shared history** reads the relationship
  graph — kind plus charge, plus the two terms that make a scene rather than a fact sheet: a tie the
  two of them read differently (the proud former friend is a `Friend` edge one of them has stopped
  meaning) and one that is not returned at all. **Knowledge asymmetry** reads the knowledge graph in
  its own order of sharpness: knowing against not knowing, proving against merely saying, and the
  confidence gap between two people who both know. **Power asymmetry** reads the personal leverage
  the graph already carries (a debt, an employment) and the institutional standing BQ-145 derives.
  No term is one actor's property: `ChemistryReason` names two roles and a dimension, and the type
  cannot express a per-person bonus — so "this character type makes a better accuser" is not a
  sentence the model can say. Every identity term is a **difference**, so two guards score exactly
  what two nobodies score and swapping which of the pair holds the office changes nothing; race and
  character archetype derive nothing at BQ-145 and so reach no weight at any size. A shared trade
  only fires on a tie that is already charged — it is what a quarrel is about, never a reason to
  expect one. The total is the plain sum of the reasons and nothing else, so
  `NarrativeInspector.DescribeCasting` accounts for the whole number: what qualified each person
  (BQ-067) and why these people rather than the others who also qualified (BQ-068), with a flat
  score printed as flat rather than omitted. `StoryletChemistryTests` isolates each dimension
  against a fixture where nothing else can move the score, pins the done-when pairing, pins tie
  stability and repeat-run determinism, and pins the two things chemistry may never do: cast
  somebody who does not qualify, and score an archetype.
  Deferred: nothing derived is persisted — a firing stores who held a role, and why this group
  is re-derivable from the same authoritative state — and BQ-069's developments, speech acts and
  wording are not touched here.
- **Identity enters as asymmetry between the pair, never as a label on one of them.** Institutional power over somebody who has none, a service one party depends on, a shared trade that gives rivalry something to be about — those are relations, and they belong here. A character archetype, race or job on a single actor is not chemistry and must not be scored as though it were; that is the stereotype failure BQ-145 gates against.
- **Sources** CD §13.1, §6.1, §48 M3.
- **This is Milestone 3** once the theft yields five structurally distinct scenes.

#### BQ-069 — Development layer
Keep `Event`, `Development`, `NarrativeThread`, `Storylet` and `NarrativeScene` distinct, so the
storylet system cannot quietly become a quest generator.
- **Depends** BQ-065.
- **Done when** a Development exists that never becomes a scene and never becomes a quest, and the world is still coherent.
- **Current implementation** `Development` is a derived reading of the present, not a record. It
  has no public constructor, no setter and no lifecycle: the only way to obtain one is
  `DevelopmentDetector.Detect`, and it stops existing when the state behind it changes rather than
  being resolved. Nothing is persisted and nothing needs to be — `WorldStateSerializer` has no
  development node and `NarrativeWorldState` has no property to hang one off — because the save
  already holds every input, so the same world derives the same pressures, in the same order, with
  the same urgency, before and after a round trip. A development carries ids and no contents: the
  events it originates in, the fact it is about, the thread that carries it, who is implicated,
  where — so it cannot disagree with the history it reads. Detection is two rules over two
  different stores, and its shape is the argument rather than its size. `UnprovenKnowledge` is
  keyed by the **fact**: two people who saw the same theft are one pressure with two names on it,
  a public fact is no pressure at all, and a fact only its own subject believes is nobody's matter
  — which is what stops this from wrapping the ledger. `UnmetObligation` is keyed by an open
  `SocialObligation` and reads its thread and place back off its source event rather than storing
  them twice. The two do not stand one-to-one with threads: one thread holds one pressure though it
  holds two facts, a resolved thread still holds an unproven secret, and an obligation is a
  pressure whether or not a thread ever carried it — which is what stops it from being a second
  thread system. `DevelopmentExpression.Opportunities` is the one seam to the dramatic layer (CD
  §37, step 7 to step 8): it hands `StoryletEngine` the thread and focus the pressure already
  names and adds no selection of its own, so what comes back is exactly what the engine finds when
  asked directly. `NarrativeInspector.DescribeDevelopments` prints each pressure, its urgency, what
  it was derived from, and whether a storylet could be looked for at all. `DevelopmentLayerTests`
  pins the done-when with a real pressure rather than a hidden one — an open favour that names a
  live thread, is handed straight to the engine, and still produces nothing because a storylet
  builds roles around a claim and this is not about one — and pins the four boundaries: no event
  is appended, no fact authored, no thread opened, no firing recorded. The distinguishability test
  kills the thief and the witness: the scene becomes unplayable and the event, the thread, the
  storylet definition and the pressure are all exactly as they were.
  Deferred: nothing decides *which* pressure to surface (that is director work), a development is
  not a speech act and carries no wording, and no further detection rule arrives without a
  consumer — later steps add rules to the detector, not fields to `Development`.
- **Sources** CD §36.5, §37.

#### BQ-134 — Project verbs through contextual affordances *(moved forward)*
Moved forward to the playtest consolidation section before BQ-039 because live S4 testing showed
raw verb projection would multiply defects during generative expansion. Do not implement a second
BQ-134 here; preserve the later BQ-070+, BQ-083 and BQ-093 dependencies on the moved step.

#### BQ-070 — Semantic speech acts
Ask, Answer, Accuse, Deny, Admit, Request, Refuse, Threaten, Apologize, Gossip — meaning before wording.
- **Depends** BQ-006, BQ-066, BQ-134.
- **Done when** a speech act is produced with no text attached, and the log shows its full semantic content.
- **Current implementation** `SpeechAct` carries a type, a speaker, an audience, an
  `ActionBinding` of content, the person that content is about and the act it responds to — and
  nothing else. There is no text field anywhere on the contract, which a reflection test enforces
  rather than trusts, and `NarrativeInspector.DescribeSpeechAct` prints the whole of what was
  communicated with `wording: none` stated on its own line. `SpeechActProfile` holds what is true
  of every instance of an act type — its stance toward the proposition, which way it moves, what
  content and which participants it cannot do without, and what it may respond to — so a consumer
  reasons about meaning from a table rather than from a sentence. Composition refuses rather than
  repairs: an accusation naming nobody, an admission about somebody else, gossip told to its own
  subject and an answer to nothing are not weaker acts but different ones or none.
  The seam onto BQ-134 is `SpeechActMeaning`, and it runs one way. It reads an intent the
  projection already produced and says what saying it would amount to; it never consults
  availability, resolves a check or writes anything, so the vocabulary cannot become a second
  action system. The mapping is many-to-one and partial on purpose — telling a neighbour and
  reporting to a guard are one act, most verbs communicate nothing, and six of the ten have no
  player verb at all because they are moves inside a conversation.
  Deferred: emotion, urgency, publicity and social practice are readings of state the world
  already holds and are not copied onto the act; acts are transient and have no save entry,
  because the durable record of having spoken is an event, a belief or an obligation; storylet
  beats do not yet name acts, and `lie` maps to nothing, because a lie is a stance held against
  the speaker's own belief rather than an act type — BQ-073 decides which act carries one, and
  BQ-070 owes it only the fixed stance that makes the contradiction computable.
- **Sources** CD §17, §17.1, §38 Phase B.

#### BQ-071 — Disclosure decisions
Knowing a fact does not imply telling it. Compute disclosure pressure from privacy, relationship,
fear, loyalty, leverage, legal risk and social practice.
- **Depends** BQ-070, BQ-063.
- **Done when** the same NPC answers directly, hedges, deflects and refuses across four relationship levels.
- **Current implementation** `Disclosure.Decide` returns a `DisclosureDecision` for one speaker,
  one listener and one claim: a four-rung ladder — `Disclose`, `Hedge`, `Deflect`, `Refuse` — plus
  `NothingToDisclose`, which is not a willingness answer at all. The first thing it does is ask the
  knowledge graph for a belief, and no belief ends it before a single pressure is weighed; identity
  says what somebody would *plausibly* know and that never becomes one. Ten signed pressures are
  read from state that already exists — belief confidence and source, personality honesty, trust,
  loyalty and vengefulness, the tie to whoever is asking and the tie to whoever the claim is about,
  the fact's own secrecy, the obligation ledger, `ValueConcern.Law`/`Family`/`Status`,
  `SensitivityProfile` and the decaying `EmotionalStateProfile`. Nothing is stored: the balance is
  arithmetic performed on the spot and thrown away, so no second social score can drift out of
  agreement with the state it describes, and deciding writes nothing at all. It is a character
  decision and not a difficulty check — no resolver, no `ActionContext`, no rng, enforced
  structurally — so the same state always answers the same way and what changes an answer is the
  world changing. `NarrativeInspector.DescribeDisclosure` prints every pressure with its sign, size
  and the state behind it, and names the decisive ones by the only definition that needs no theory:
  those whose removal would have produced a different strategy. `Disclosure.Compose` turns a
  decision into BQ-070's vocabulary where the vocabulary has an act for it — `Answer` for both
  forthcoming rungs, `Refuse` for the refusal.
  Deferred by design: a deflection composes to *no act*, because there is no `Evade` in the
  vocabulary and adding one is BQ-073's call — which BQ-073 made, so a deflection now composes to
  one and the ladder here is unchanged; there is no lie strategy and no way to express one, so a
  refusal cannot silently become a falsehood, and BQ-073 adds falsification as a separate axis
  rather than a fifth rung; a hedge is a weaker commitment to the whole
  claim rather than a smaller part of it, and graduated depth — added by BQ-072 as a second axis on
  the same decision, leaving the ladder and every pressure here untouched — is what says how much
  of the claim comes with it. Social practice is the one pressure on CD §17.5's list that is absent, because
  §16's norms are not state yet and a pressure derived from nothing would be a number pretending to
  be a reason.
- **Sources** CD §17.5; PM §38.

#### BQ-072 — Relationship-dependent depth
Affinity changes not just willingness but how much is revealed, from "nothing to say" to
"something I didn't tell the guards".
- **Depends** BQ-071.
- **Done when** raising affinity on one NPC unlocks strictly more of one fact, in stages.
- **Current implementation** a second axis on `DisclosureDecision`, not a fifth rung of BQ-071's
  ladder: `DisclosureDepth` runs `Nothing`, `Gist`, `Detail`, `InConfidence` — the claim bare, its
  particulars, and how the speaker comes to hold it (their own part in it, who told them, what they
  can produce), which is the "something I didn't tell the guards" end. BQ-071 is unchanged and
  still decides whether anything is said; depth decides how much comes with it, and the two vary
  independently — a hedge can carry every particular and a committed answer can be bare. Depth is
  the lowest of three ceilings, kept as ceilings rather than terms in a sum so that nothing buys
  its way past another. `KnownDepth` is what the belief supports: particulars need the fact to have
  any, provenance needs a first-hand source, a named teller or a proof plus the conviction to stand
  behind it, so hearsay from nobody in particular stops at `Detail` however close the listener is.
  `StandingDepth` bands a `Standing` read from the whole relationship rather than from affinity —
  sentiment, what the tie is (BQ-071's own `KindBonus`, so there is no second opinion about what a
  spouse is), the obligation ledger between the two (a kept promise, a shelter still standing, a
  broken promise or an open grudge, bounded so a ledger of small favours cannot outweigh the tie
  itself) and whether the listener holds a tie back. The third is `Restraint`: the magnitudes of the
  weighed pressures against saying it, the relationship excluded, so a frightened witness who
  answers her husband anyway still does not tell him how she knows. `Limit` names which ceiling
  bound it and `NarrativeInspector.DescribeDisclosure` prints all three, because a shallow answer
  from a friend is otherwise indistinguishable from a bug. Nothing is stored, nothing is written,
  and the reading is arithmetic over the graph and the ledger performed on the spot.
  Deferred by design: `Compose` is untouched — depth changes what a realizer has to say, never
  which act it is or which claim it names, and rendering the rungs as words is BQ-074's. Every rung
  is the truth, less of it; there is still no way to express a falsehood, which stays BQ-073's —
  and BQ-073 leaves these rungs alone, so an incomplete answer remains an honest one rather than
  becoming a fifth way of misleading somebody.
- **Sources** PM §38; LW §3.4; CD §17.5.

#### BQ-073 — Lying and evasion as outcomes
`Lie`, `Evade`, `Deflect` are results of a disclosure decision, not decorative alternatives. A lie
records that the speaker's statement differs from their belief.
- **Depends** BQ-071, BQ-020.
- **Done when** an NPC lies, the world records the lie, and the player can later catch the contradiction.
- **Current implementation** a third axis on `DisclosureDecision` and a classifier that reads
  assertions against the belief graph. `DisclosureTactic` — `Decline`, `ChangeSubject`,
  `AnswerElsewhere`, `Falsify` — says what is done *instead* of answering, and is unordered
  because these are kinds rather than degrees: a lie is not more than a refusal. BQ-071's ladder
  and BQ-072's depth are untouched, so "no" is still one rung and what somebody did with it is a
  separate reading. The five outcomes the step owes stay five: declining, letting the question go,
  answering a neighbouring question, answering incompletely (`HeldBack` — BQ-072's depth below
  what the speaker holds, every word of it true) and asserting a falsehood.
  `AnswerElsewhere` requires actually holding something else about the same person that is not
  itself kept, so the distinction is a fact about the speaker rather than decoration.
  Falsifying needs three things at once and refusal is never promoted into it: severe pressure
  (`FalsifyAt`, deliberately past `DeflectAt`, because while an open refusal still costs less than
  the claim somebody takes that), a belief held at conviction (you cannot knowingly deny what you
  do not hold) and low honesty as a hard condition rather than a weight, so no amount of pressure
  makes an honest character a liar.
  BQ-070's vocabulary gains the one act it named in advance, `Evade`: stance `None`, no
  proposition, so nothing downstream can read an evasion as having asserted anything. There is
  still no `Lie` act. `Disclosure.Compose` maps a falsification onto `Deny` — the ordinary
  vocabulary, because a lie is a stance against belief rather than a way of speaking.
  `Deception.Assess` returns a `Veracity`: `Sincerity` (`NotAsserted`, `Sincere`, `Unfounded`,
  `Insincere`) decided from the belief graph alone, and the world's own `Accuracy` reported
  beside it and never consulted. So an honest mistake and a lie can assert the identical false
  claim and stay distinguishable, asserting something true against your own belief is still a
  lie, and no omniscient truth is invented to classify anybody. Insincerity has exactly two
  shapes: denying a claim you hold, and putting a rival version forward — rival being structural
  (`Fact.DistortionOf`, which BQ-020 already maintains) rather than a similarity judgement.
  Asserting with no belief either way is `Unfounded` and is not a lie.
  `Deception.Record` writes two things and nothing else: the shared `lied_about` fact
  (`DeceptionRecord`, now used by `RumorSystem.Lie` too, so a seeded rumour and a lie told to
  somebody's face leave one trace per speaker and claim) and a `Deceived` event carrying the claim,
  the audience and the stance as `EventTags.Affirmed`/`Denied`. `StatementOf` reads that back as a
  `RecordedStatement` so no consumer depends on the layout, and an entry without a stance is
  unrecognized rather than guessed at. Deciding, assessing and reading contradictions write
  nothing; `Record` is the one call that does. `Contradictions` is a reading of one character's
  knowledge — they must have been there and must now hold something firm against what they were
  told — so catching a liar is never a hint from an omniscient narrator, and no save entry was
  added because a statement is an event and a lie is a fact.
  Deferred by design: no conversation state, no commitments, no who-owes-whom-an-answer — BQ-083's,
  and the only bookkeeping here is what was said. A falsification composes a denial rather than
  minting a rival claim to name, because minting is a write and deciding writes nothing;
  `RumorDistortion.Blame` already makes such a claim and `RumorSystem.Lie` already tells it, and
  the classifier scores both routes identically. `Deceived` events written by the action library
  carry no stance and so are not read as testimony. No wording anywhere: which of a hundred ways a
  denial is said is BQ-074's, and none of them may change what it did.
- **Sources** CD §17.5, §14.5.

#### BQ-074 — Fragment schema and realizer
`DialogueFragment` with tags, requirements, tone, position; assembly from opener, core, modifier,
callback, context and closer.
- **Depends** BQ-070.
- **Done when** one semantic act renders three recognizably different ways from the same data.
- **Current implementation** a compositional fragment model, not a grammar. `DialogueFragment`
  carries a position, a phrase, conditions, tone tags, free tags and a repetition group;
  `DialogueRealizer` fills the six slots in the order CD §18 lists them, drawing exactly one core
  and at most one of each other slot, each optional slot drawn against an implicit "say nothing"
  so not every line has every part. Fragments are content rather than code — a `dialogueFragments`
  record kind compiled out of `content/fragments/` into the same bundle as storylets, validated by
  the same compiler — so there is no second authored-text system in Core and adding a way of
  saying something is a content change.
  Three things hold "wording may express meaning and may never create it" as structure rather than
  as a promise. The realizer takes no world: a fragment library and a request, neither of which
  can be written to, so *realization writes no world state* is a fact about the signature. What a
  fragment may be chosen on is a closed vocabulary of *readings* of the act and the decision behind
  it — `DialogueReadings`, all of them derived, none of them new — and what it may name is a closed
  set of placeholders resolving to people the caller put on stage and the label the claim already
  carries. There is no placeholder for the claim itself, because phrasing a proposition needs a
  predicate lexicon and a lexicon would be a second place where what a fact says gets decided; a
  fragment that wants to word a kind of claim conditions on `claim_predicate` and writes the
  sentence. A placeholder nothing fills makes its fragment ineligible, so nobody is ever "someone".
  Refusal is the failure mode throughout. An act nothing in the library says comes back unrealized
  with a reason and no text — never a vaguer line assembled from the trimmings — and a request
  whose parts describe a situation the semantic layer never produced (a decision by another
  speaker, a claim that is not the act's) is refused rather than reconciled. Every core fragment
  must declare which act it says, enforced at load, so a refusal can never be worded as an answer.
  Selection is deterministic in the semantic state and the seed: choices come from streams forked
  off the caller's rather than from its running state, so a line does not change because a
  different conversation happened earlier in the tick.
  The one architectural refusal: **wording is never told that the speaker is lying.** A decision
  whose tactic is `Falsify` reaches realization as though no decision had been given, so a liar's
  denial draws from the fragments an honest denial draws from and at the same seed says the
  identical words; `tactic: falsify` is not a value content may name, and the loader rejects it.
  The decision itself is untouched — `WillLie` still reads true, `Deception.Assess` still
  classifies from the belief graph — which is the point: a lie stays a lie by its relation to
  belief, and a fragment pool that shifted when somebody lied would put the tell in the words and
  make lies catchable by ear rather than by what the listener knows.
- **Deferred by design** voice profiles (BQ-075), occupational vocabulary (BQ-076), negative space
  (BQ-077), repetition control (BQ-078) and the weirdness budget (BQ-079) are not implemented here;
  what they get is a seam each — a tone request that narrows choice and cannot change meaning, free
  tags carried and unread, and a declared repetition group with no consumer. The callback slot is
  filled only from the act's own antecedent, because the relationship and history callbacks CD §18
  sketches need conversation state (BQ-083) that does not exist and must not be invented here. The
  vocabulary is deliberately small: enough to prove the contract and to make the later extensions
  additions rather than rewrites.
- **Sources** CD §18, §38 Phase C.

#### BQ-075 — Voice profiles
Sentence length, formality, directness, hedging, sarcasm, metaphor use — constraining fragment
selection without creating meaning.
- **Depends** BQ-074.
- **Done when** two NPCs with identical personalities but different voices sound different saying the same thing.
- **Sources** CD §19, §5.2.

#### BQ-076 — Occupational vocabulary
Metaphors and nouns drawn from lived context — farmers speak of weather, thieves of heat and marks.
Subtle, metadata-driven, not every line.
- **Depends** BQ-075, BQ-145.
- **Done when** occupation is guessable from dialogue in a blind test, without it being stated.
- **Sources** CD §17.6, §6.1.
- **The pool comes from the observed work and hobby facets** (BQ-144 through BQ-145), not from a label BQ assigned. A character whose work could not be read gets no occupational pool rather than a default one, and wording is the *last* consumer of identity, never the first — a voice that leans on the job harder than the decisions do is the stereotype failure arriving through the back door.

#### BQ-077 — Negative-space personality
What an actor will *not* do — never begs, never lies directly, never speaks badly of family —
constraining both action selection and wording, breakable only under documented pressure.
- **Depends** BQ-075, BQ-057.
- **Done when** a prohibition visibly costs an NPC an otherwise optimal action.
- **Sources** CD §17.7.

#### BQ-078 — Repetition control
Track recent use by fragment, repetition group, opener, semantic act, metaphor family and cadence.
- **Depends** BQ-074.
- **Done when** a 100-line synthetic conversation set contains no opener more than twice.
- **Sources** CD §21, §35.

#### BQ-079 — Weirdness budget
Levels 0–4 with most content at 0–2; the tone formula of ordinary problem plus one absurd premise
plus real mechanical consequence plus understated response.
- **Depends** BQ-074.
- **Done when** a generated set measurably matches the target distribution and no scene stacks two absurd premises.
- **Sources** CD §22, §22.2, §23; MD §20.

#### BQ-080 — Reactions reveal personality
The same absurdity draws different reactions by character — the pragmatist, the zealot, the merchant.
- **Depends** BQ-079, BQ-064.
- **Done when** one absurd event produces five in-character reactions with no bespoke text for that event.
- **Sources** CD §22.3, §22.4; MD §20.
- **This is Milestone 4.**

#### BQ-081 — Callback hooks
Store reusable narrative material from events: embarrassment, promise, injury, nickname, scandal,
lost object, weird incident.
- **Depends** BQ-034.
- **Done when** a scene references an event from at least ten in-game days earlier, unprompted.
- **Sources** CD §24, §25; PM §51.
- **This is Milestone 5.**

#### BQ-082 — Continuity humour
Absurd history gains weight by recurring — in gossip, taxes, inheritance, festivals — rather than by
new jokes.
- **Depends** BQ-081, BQ-047.
- **Done when** one absurd incident resurfaces in a second, unrelated context.
- **Sources** CD §25; PM §42, §43.

#### BQ-083 — Conversation state and commitments
Short-term discourse memory: topics raised, claims made, questions unanswered, lies told, promises
made. Commitments that matter become durable world events.
- **Depends** BQ-007, BQ-073.
- **Done when** an NPC says a version of "that is not what you said five minutes ago" from recorded state.
- **Sources** CD §28.5.

#### BQ-084 — Social practices
Shop, street, Home, guild, funeral, festival: context changes how an action is interpreted.
- **Depends** BQ-064.
- **Done when** theft during a funeral produces a different social response than the same theft from an empty warehouse.
- **Sources** CD §16; PM §5; VS §5.1.
- **Note** vanilla activity context — what the people present were doing — is one input to interpretation, and a weak one. Use it to make a response sharper, never to make the player consult a schedule before acting. This step does **not** wait on BQ-135: practice is decided by place, occasion and who is present, and activity is later enrichment on top of it.
- **Who is present is an identity read (BQ-144 through BQ-145), and it is the stronger input.** A guild meeting is a practice because guild members are here; a funeral changes what theft means partly because of who is standing in the room. Consume the derived affordances for that, not a private list of who counts as clergy, and let an unread facet simply not contribute — a practice is never asserted because BQ guessed somebody's role.

#### BQ-085 — Item provenance
Notable objects carry structured history: crafted by, owned by, stolen from, recovered at, evidence in.
- **Depends** BQ-017.
- **Done when** showing a recovered object to the right NPC reopens a thread months later.
- **Sources** PM §21, §51; LW §15.

#### BQ-086 — Location history and legends
Sites accumulate notable events; repeated or high-salience history compresses into local legend.
History is semantic input to later spatial reuse and decoration, but this step does not write maps
or mutate places physically.
- **Depends** BQ-034, BQ-087.
- **Done when** a site the player cleared a year earlier is described by its history when reused.
- **Sources** PM §40, §41; LW §7.7; PP §6.

> **Checkpoint S7.** `CD §39`'s canonical test: generate the same objective theft one hundred times
> with varied actors, personalities, relationships, knowledge and settings. The results must include
> private confrontation, public accusation, bribery, avoidance, false accusation, family coverup,
> blackmail, forgiveness, revenge, guild involvement, and cases where nobody asks the player for
> anything. If it still reads as "who stole the item ×100", fix the building blocks before adding
> archetypes.

---

### Stage S8 — Places and autonomy

Two systems that both depend on everything before them: locations that mean something, and a world
that acts without the player.

**Standing rule:** do not build a general-purpose random dungeon generator (`LW §7`, `PP`). Elin
supplies spatial substrate; curated grammars supply spatial meaning; scenario state and history
supply identity. Procedural scenario dungeons are a first-class BQ site system, but they are built
from semantic plans, authored atoms, systemic Elin verbs, candidate validation and persistence
proofs, not from raw geometry. Full generated settlements and vanilla-town physical development stay
post-launch.

#### BQ-087 — First site proof
One small site through native zone infrastructure: one thread binding, three to five actors, real
cargo and evidence, two meaningful approaches. This proves genesis for one BQ-owned place; it does
not prove later physical development. Unload, save, reload, return, verify exact persistence.
- **Depends** BQ-029, BQ-032.
- **Done when** the return visit finds the same site, same actors, same cargo, and the log proves nothing regenerated or redispatched historical events.
- **Sources** MD §15; PM §72 stage 5; LW §7.9; PP §6, §7.
- **Do not generalize until this passes.**

#### BQ-088 — Location reuse policy
Before generating anything: can a vanilla location host it, can an existing procedural site be
recontextualized, can an older site be reused? Generate only as a last resort.
- **Depends** BQ-087.
- **Done when** a situation needing a site reuses an existing one, and the inspector explains why.
- **Sources** LW §7.2; PM §14.

#### BQ-089 — Curated location grammars
Bandit camp, collapsed mine, smuggler cellar, occupied farmhouse, warehouse, makeshift prison —
specifying semantic requirements, route relationships, required affordances and authored-piece
sockets, not geometry.
- **Depends** BQ-088.
- **Done when** two sites from the same grammar are recognizably the same kind of place and clearly not the same place, and the inspector can explain every required node/edge in the abstract plan.
- **Sources** LW §7.3, §7.1; PP §2, §3.

#### BQ-090 — Spatial affordances
LockedBarrier, BreakableBarrier, DiggableBypass, HiddenPassage, GuardedThreshold, EvidenceCache,
PrisonCell, ObservationPoint, AlternateExit — so builds have real routes.
- **Depends** BQ-089, BQ-029.
- **Done when** one site is completed three ways: front gate, side lock, mined wall.
- **Sources** LW §7.4; MD §12; PP §4.
- **Evidence gate** each affordance records whether it is runtime-verified, source-observed, metadata-only, or BQ-authored. Do not promise a route whose Elin primitive has not passed the appropriate evidence level.

#### BQ-091 — Scenario decoration and causal contents
Wealth, hunger, recent attack, prisoners and cargo change what is placed. Enemies reflect the actual
group; loot is stolen cargo and possessions, never filler.
- **Depends** BQ-090.
- **Done when** a site's contents are derivable from its situation's state, with no template chest.
- **Sources** LW §7.5, §7.8; PM §14; PP §2, §4.

#### BQ-092 — Candidate generation and scoring
Generate several site candidates and select on route diversity, objective separation, evidence
distribution, loop quality, supported mechanic vocabulary and reachability. Expose scores in the
inspector.
- **Depends** BQ-091.
- **Done when** rejected candidates' reasons are readable in the inspector, including at least one each for unreachable objective, access/key ordering failure, nominal alternate routes collapsing into the same play, useless loop or trivial shortcut, pathological backtracking or low-information corridor, inaccessible evidence, and a route promise refused because the required Elin primitive was unsupported or unverified.
- **Sources** LW §7.6, §12; PP §2, §8.

#### BQ-139 — Scenario-dungeon plan representation
Introduce the abstract plan for bounded adventure sites: scenario graph, route cycles, required
affordances, objective/evidence anchors, occupant regions, authored-piece sockets and validation
requirements. No Elin map writes.
- **Depends** BQ-092, BQ-086.
- **Done when** at least two grammars produce deterministic abstract plans whose inspector output explains every node, edge, requirement and rejection reason, and a seed replay reproduces the same selected plan.
- **Sources** PP §3, §4; LW §7.1, §7.6.
- **Not this step.** No general Nefia replacement, no settlement generator, no tile placement, no custom puzzle mechanics.

#### BQ-140 — First procedural scenario dungeon
Build one BQ-owned bounded site whose scenario graph is generated and whose physical realization uses
authored atoms plus verified Elin substrate. A good first proof is an abandoned/occupied mine with a
meaningful loop, locked or guarded threshold, diggable/breakable or otherwise systemic bypass,
hazardous route, optional hidden route, controlled descent/exit, causal evidence and causal rewards.
- **Depends** BQ-139.
- **Done when** roughly 8-12 authored pieces can produce at least three meaningfully different valid navigation/problem structures across seeds before rendering; the selected site contains a meaningful loop, locked or guarded threshold, diggable/breakable or otherwise systemic bypass, hazardous or trapped route, optional hidden route, scenario objective, controlled descent or exit condition, environmental evidence, and causal rewards; distinct occupant regions exist only where vanilla hostility/faction behavior supports the promise; the selected site survives unload/reload and save/quit/reload; and unsupported mechanics are omitted rather than simulated by implication.
- **Sources** PP §4, §7, §8; LW §7.9.
- **Evidence gate** `GenBounds.TryAddMapPiece`, `PartialMap.Apply`, native site registration, locked exits, traps, locks, diggable/breakable bypasses and any custom `Trait`/`Zone` hooks used by this step must be rechecked against the exact installed build and recorded in the Elin evidence docs before being treated as runtime-semantic proof.
- **Tier-2 rule** a generic Trigger -> Condition -> Effect scenario state machine may be spiked here only if Tier-1 Elin verbs and BQ-090 affordances cannot express the proof. It is not a prerequisite architecture for the first good scenario dungeon.

#### BQ-143 — Additive BQ-owned site mutation proof
On a disposable BQ-owned site, prove one bounded authored physical addition can be applied after
genesis without destructively regenerating the visited map. This is an evidence spike for future
development, not settlement evolution.
- **Depends** BQ-140.
- **Done when** one authored addition is applied only into verified free/compatible space; the site preserves preexisting actors, items, evidence and player-visible history; save/quit/reload, leave/re-enter, elapsed in-game days, and a second save/quit/reload all retain the addition exactly once; NPC/path/service behavior around the addition is recorded; disabling BQ is tested where feasible; and the exact Elin build and evidence level are written into the Elin evidence docs.
- **Sources** PP §6, §7, §8; `docs/elin/api/world-and-zones.md`; `docs/elin/verification/runtime-probes.md`.
- **Not this step.** No hamlet growth, district expansion, vanilla-town mutation, player construction collision solver, or ongoing development scheduler.

#### BQ-135 — Read vanilla actor activity *(stage S8, immediately before BQ-093)*
Expose transient vanilla actor activity through the seam as one read-only semantic snapshot, so the
autonomy systems share an adapter instead of each growing its own reflective probe into `Chara`.
This is the activity counterpart of what BQ-030 did for the Home, and it creates **no** BQ schedule
system.
- **Depends** BQ-003, BQ-031.
- **Done when** `IVanillaState` returns an actor activity snapshot for a live actor; current zone and
  physical presence are readable; timetable id and current semantic span are readable where the build
  supports them; the current AI goal is projected into a small semantic family (sleep, work, hobby,
  needs, combat, task, idle, other) without leaking game class names into Core; `UseGlobalGoal`, a
  pending zone transition and the global-goal category are readable where supported; every field the
  build did not answer is `Unknown` rather than `false` or zero (`D017`); the read has no side effects
  and registers no actors; a live diagnostic logs the snapshot for at least one ordinary resident and
  one eligible global actor; and tests prove an unavailable member degrades only its own fields.
- **Out of scope** setting a timetable, setting an AI goal, writing a `GlobalGoal`, any pathfinding,
  persisting the snapshot, and any autonomous decision made from it. No new `VanillaCapability` is
  granted for a member nobody has watched work.
- **Not this step: stable identity.** Character archetype, race/species, work, hobby, service role and institutional role are BQ-144's single observation (`VS §4.2`). This step reads only what an actor is *doing now*. The two are separate because they have different lifetimes and different consumers — an actor at a work goal is activity, the job they hold is identity, and the second is still true while they sleep. Neither step reads the other's fields, and no third read of `Chara` is created for either.
- **Sources** VS §2, §4, §7; LW §2.2; D017, D019, D021.
- **Unblocks** BQ-093, BQ-094, BQ-095, BQ-097, and activity-aware BQ-084.
- **Risk** the observation surface, not the read. Anything that has to be sampled per actor per turn
  means patching a hot loop; establish a low-frequency event or hook first (`VS §7`).

#### BQ-093 — NPCs use the same action resolver
One `NarrativeAction` vocabulary for player and NPC. No `PlayerBribe` / `NpcBribe` split.

Execution has two stages. BQ chooses a semantic `NarrativeAction`; then, if the action needs
physical embodiment and a verified vanilla action or goal can perform it, physical execution is
delegated to vanilla and the result observed. Where it cannot, and coarse resolution is permitted,
resolve abstractly. Core does not acquire movement, pathfinding or routine-task logic in either
branch.
- **Depends** BQ-062, BQ-031, BQ-135.
- **Done when** an NPC performs an existing verb through the same code path, with the same four outcomes, **and** an action needing embodiment either delegates to a verified vanilla path or resolves coarsely, with the choice visible in the inspector and no movement controller in Core.
- **Sources** CD §47.5; PM §35; LW §6.8; VS §3.1, §3.2; D021.

#### BQ-094 — First autonomous intervention
One NPC pursues one situation off-screen and can succeed, fail, or make it worse.
- **Depends** BQ-093.
- **Done when** a situation the player ignored is resolved by somebody else, and the player can find out how, with only the causally meaningful steps recorded — not the actor's working day.
- **Sources** PM §33, §72 stage 8; LW §14 P7; VS §5.3, §5.4.
- **Note** activity constrains opportunity where it is readable, and normally as a plausibility weight rather than a hard gate. Coarse co-location, an overlapping timetable or a shared workplace mean opportunity only: none of them may produce eyewitness testimony, proof, a location claim or recognition of a person.

#### BQ-095 — Off-screen schemes
NPCs steal, court, invest, flee debt, hire help, hide evidence and seek revenge on a coarse schedule.
- **Depends** BQ-094, BQ-022.
- **Done when** returning to a town after a month shows changes attributable to named actors and recorded events, **and** an actor Elin is already moving through its own global travel is left to it rather than scheduled twice.
- **Sources** PM §26; LW §6.8; VS §3.2, §3.3, §5.5.
- **Note** an off-screen scheme says what was attempted and what it meant, never where anybody stood (`D021`). Vanilla moving an actor on its own is a fact to interpret — an arrival that intersects an old debt is content BQ did not have to invent.

#### BQ-096 — Adventurer ecology
Other adventurers pursue situations, and can die, fail, take credit, or become recurring rivals.
- **Depends** BQ-094.
- **Done when** an adventuring party attempts a rescue the player declined, and their outcome is discoverable.
- **Sources** PM §33; LW §6.8.

#### BQ-097 — Traveling groups
Caravans, adventurers, pilgrims, refugees, bandits, patrols: origin, destination, departure,
expected arrival, route risk, cargo, members. Resolved at milestones, never pathfound.
- **Depends** BQ-032, BQ-053, BQ-135.
- **Done when** a caravan that never arrives is a caravan that actually failed, with a findable cause, **and** a bounded spike has recorded against the live build whether `GlobalGoal`, `GlobalGoalVisitTown`, `GlobalGoalVisitAndStay`, `MoveZone` and `ZoneTransition` are safe and useful at materialization boundaries — a negative answer being an equally valid result, written up in `elin-api-notes.md`.
- **Sources** PM §34; LW §6.7; MD §18; VS §2.4, §3.3, §7.
- **Note** milestone travel stays the design; the spike lives inside this step and does not become a system. If the spike does not prove them safe, BQ travel stays fully abstract and reconciles on arrival. **Direct `GlobalGoal` writes are not a 1.0 dependency** and may only become one if runtime evidence shows they make travel more reliable, not merely more native.

#### BQ-098 — Situations come to the player
Consequences arrive at Home and at the player: creditors, refugees, accusers, guild emergencies,
former enemies.
- **Depends** BQ-048, BQ-095.
- **Done when** a consequence of an earlier choice arrives without the player travelling to it.
- **Sources** PM §10; LW §6.4.

> **Checkpoint S8.** The world is no longer waiting. Situations begin, change and end without the
> player, in places that persist and remember.

---

### Stage S9 — Director, hardening, launch

Nothing new is invented. Everything is made safe, paced, explainable and shippable.

#### BQ-099 — Salience and exposure budget
Limit how many threads are live and how many reach the player at once. Protect ordinary Elin play —
farming, building, exploring — from constant interruption.
- **Depends** BQ-039.
- **Done when** a tester plays an hour of ordinary Elin without being interrupted, and still finds situations when they look.
- **Sources** LW §10.7, §11; MD §8.3.

#### BQ-100 — Director scoring
Score developments by tension, proximity, recurrence, unresolved history, underused mechanics and
consequence visibility, penalizing repetition and recent exposure.
- **Depends** BQ-099.
- **Done when** the inspector explains why one development was surfaced and another was not.
- **Sources** MD §8.3; PM §54; LW §11.

#### BQ-101 — Situation fingerprinting
Track experiential shape — violent/social/economic, urgent/slow, public/secret, new/recurring — and
penalize repeated topology even when the nouns differ.
- **Depends** BQ-100.
- **Done when** two consecutive situations of the same shape are demonstrably less likely.
- **Sources** CD §33, §34.

#### BQ-102 — Quality-diversity selection
Prefer good candidates that occupy underrepresented niches over the highest raw drama score.
- **Depends** BQ-101.
- **Done when** a mundane festival rivalry is chosen over a third consecutive violent situation, and the log says why.
- **Sources** CD §36.

#### BQ-103 — Narrative conservation
Apply creation costs — reuse a fact or actor free, a new significant NPC expensive, a new weird
premise most expensive — so the world deepens before it grows.
- **Depends** BQ-102.
- **Done when** the director measurably prefers reusing an existing actor over generating a new one.
- **Sources** CD §33.6, §33.7; PM §19; LW §10.6.

#### BQ-104 — Anti-template test harness
Generate many synthetic runs; measure repetition of causal skeleton, roles, storylets, openers,
solution families, rewards and sites.
- **Depends** BQ-101.
- **Done when** the harness runs headless in CI and reports a repetition profile.
- **Sources** CD §35; MD §23.3.

#### BQ-141 — Spatial expressive-range harness
Extend the anti-template/debug approach to site plans before scaling content: measure route topology,
cycle count, objective separation, route/mechanic diversity, evidence distribution, encounter ecology
and history readability over many generated site plans.
- **Depends** BQ-104, BQ-139.
- **Done when** the harness runs headless, reports spatial repetition metrics, and demonstrates that two sites with different nouns but the same experiential topology are counted as repetition.
- **Sources** PP §8; CD §35; LW §12.

#### BQ-105 — Save integrity and quarantine
A malformed thread, a missing actor or a failed migration must degrade one feature, never poison a save.
- **Depends** BQ-052.
- **Done when** a deliberately corrupted chunk loads with a warning and a playable world.
- **Sources** LW §13; MD §19; PM §78.

#### BQ-106 — Migration fixtures
Keep a save fixture per schema version and migrate them all in CI.
- **Depends** BQ-105.
- **Done when** every historical schema version loads in a test.
- **Sources** MD §19.1, §23.1; PM §78.

#### BQ-107 — Simulation tiers
Active, Warm, Cold, Archived. Off-screen cost must not scale with total historical NPC count.

The tiers are BQ's narrative fidelity, not the game's. **Active** means the highest narrative
fidelity and the *least* duplicated physical simulation, because Elin is live and owns embodiment.
**Warm** is named actors, live situations and coarse social, economic and travel decisions, plus
observed vanilla global state where it is readable. **Cold** is sparse developments. **Archived** is
history only.
- **Depends** BQ-095, BQ-021.
- **Done when** a synthetic world with thousands of historical actors ticks within a stated budget, **and** a Home the player has been away from is not advanced twice — what `Zone.Simulate()` catches up on revisit is reconciled against, not re-run.
- **Sources** MD §18.1, §26; PM §53; LW §5.4, §10; VS §2.3, §2.5; D021.
- **Note** Elin runs four fidelity mechanisms of its own (`VS §2.5`), and vanilla `GlobalGoal` advancement may continue for eligible actors regardless of what tier BQ has put them in. Tiering is a budget for BQ's own work, never a claim that nothing else is simulating.

#### BQ-108 — Performance guardrails
Event-driven updates, bounded rumour propagation, provenance only for notable objects, lazy dialogue
realization, director budgets.
- **Depends** BQ-107.
- **Done when** frame time impact is measured and documented against a large save.
- **Sources** PM §79; CD §45; LW §10.

#### BQ-109 — Capability degradation drill
Disable each capability in turn and confirm the mod loses exactly one feature, with a diagnostic,
and never breaks.
- **Depends** BQ-003.
- **Done when** every capability has been switched off in a live game and the result documented.
- **Sources** LW §2.4, §16; PM §56.

#### BQ-110 — Update smoke test
A documented checklist to run after every Elin update: aliases resolve, patches apply, chunk loads,
one situation plays.
- **Depends** BQ-109.
- **Done when** the checklist exists and has been run once against a real game update.
- **Sources** PM §56; LW §2.4.

#### BQ-111 — Player configuration
Narrative activity, off-screen autonomy, Home intrusion, criminal weighting, lethality, competitions,
rumour verbosity, debug causality. Weights and presentation only — never authored outcomes.
- **Depends** BQ-099.
- **Done when** each setting demonstrably changes generation weighting, and none of them scripts a result.
- **Sources** PM §55.3, §59; LW §10.7.

> **Checkpoint S9 — Launch.** See §7.

---

### Engagement track — added after the reward audit

Nine steps from [`design/engagement-and-reward.md`](design/engagement-and-reward.md), which answers a
question the other four documents leave open: not whether the simulation is good, but why a player
would engage with it rather than treat it as scenery. Each is placed in an existing stage by
dependency rather than run as its own stage — engagement is not a feature to bolt on at the end.

#### BQ-112 — Reward vocabulary audit *(stage S3, alongside the action library)*
Every resolution grants access, a relationship, standing, information, property or a favour owed.
No resolution grants a loot payout.
- **Depends** BQ-009.
- **Done when** no verb or archetype produces an item reward that is not a real recovered object, and a test asserts it.
- **Sources** engagement §3 Tier 2; PM §12; SDT overjustification.
- **Why** a payout attached to a story converts it into a chore with a fee, and measurably weakens the motivation the rest of the design builds.

#### BQ-113 — Favours are callable *(stage S6, extends BQ-055)*
A recorded favour becomes a player-usable action: call it in, and the NPC does something they would
otherwise refuse.
- **Depends** BQ-055.
- **Done when** the player can spend a favour from dialogue and the world honours it.
- **Current implementation** `call_favor` is a verb like any other, so it reaches the Drama node
  through the ordinary contextual projection and is offered only to somebody with an open favour
  recorded against them. It does not roll: a stored option is worth having because the player knows
  what it buys. Because BQ-113 was skipped when BQ-055 landed, two defects had to be repaired with
  it — persuasion used to spend an open favour by itself the moment its roll failed, which is now
  removed, and nothing in play ever recorded a favour, so the consequence engine now derives one
  from any `Helped` event of magnitude 0.5 or more that the player is the actor of, capped at one
  open favour per person and deliberately left unbound to any matter.
- **Sources** engagement §3 Tier 2; PM §22.
- **Why** a stored option the player chooses when to spend is the most autonomy-supporting reward available.

#### BQ-114 — Situations cast people the player already knows *(stage S5)*
Casting prefers actors the player has actually met, traded with, or lives beside, over strangers.
- **Depends** BQ-039, BQ-067.
- **Done when** the first situation in a fresh save casts an NPC the player has already interacted with, in the majority of test runs.
- **Current implementation** `PlayerFamiliarity` is the one answer to "how well does the player
  know this person", read from four grounds the world already holds: residency on the player's own
  land, **vanilla affinity**, the event ledger's record of what the two of them have done to each
  other, and a relationship edge either way. Affinity carries the step in practice, because ordinary
  talking, trading and gift-giving happen entirely in vanilla and leave no BQ event behind — in a
  save the mod has only just attached to, Elin's own number is the only history there is (`D010`),
  and reading it rather than keeping a private acquaintance table is also what keeps this step out
  of the save file. Every ground only raises the reading, an unreadable one contributes nothing
  rather than zero (`D017`), and none of it is affection: somebody the player wronged is not a
  stranger. Two casting surfaces consume it. The settlement generator adds it as the generic
  `player_familiarity` pressure **after** eligibility has been decided on the world's own pressure,
  so a familiar face can decide which of the situations a settlement already supports is told first
  and can never be the reason one exists (`D027`); the storylet caster orders its searched pool by
  it, which changes who is found first without scoring anybody's fitness for a role, so `D026`
  holds. Measured on the done-when fixture — four equally pressured marks, one of them a face the
  player buys from — casting went from 25 familiar faces in 100 runs to 100.
  Not in scope here: pets and companions as cast members, and surviving their being sold, married
  off or killed, which is BQ-123's work and still depends on this.
- **Identity and familiarity are different questions and stay different fields.** How well the player knows somebody is `PlayerFamiliarity`; who that person is, is BQ-144's observation. Do not merge them, and do not let a facet raise familiarity — a guard the player has never met is not a familiar face because guards are recognisable. Both are live reads that stay out of the save, and both feed casting for different reasons: familiarity decides which of the situations a settlement already supports is told first (`D027`); identity decides who is eligible for a role at all.
- **Sources** engagement §4; CD §13.1.
- **Why** attachment must precede stakes; a threat to a stranger is an errand.

#### BQ-115 — Seed familiars early *(stage S5)*
A handful of low-stakes recurring contacts appear in the first hours — a shopkeeper who remembers
you, a neighbour with a small complaint — before any crisis exists.
- **Depends** BQ-114.
- **Done when** a new save produces recognisable recurring faces before it produces its first situation.
- **Current implementation** `EarlyContacts` elects a handful — three — of the people a settlement
  already holds, on grounds the save already carries: standing here and living on the player's land
  is a **neighbour**, handling goods and money with strangers here is a **shopkeeper**, anybody else
  present is a **regular**. It elects and elects only. It records no meeting the player did not have,
  writes no event, mints no relationship and moves no affinity, because manufacturing history to
  make a face familiar would corrupt the very reading BQ-114 exists to take. Nothing is stored
  either: election is a pure reading of the settlement, so the same save names the same faces on
  every pass and across a reload, and the recurrence *is* the determinism rather than a roster in
  the save (`D022`). The one write is `Promote(NarrativeImportance.Recurring)`, which is a statement
  about the mod's own attention and cannot lie about the player. That rung was previously reachable
  only *after* a high-weight memory, i.e. only after something had already happened to somebody,
  which is the backwards ladder `engagement §4` and `PM §19` both name.
  Both casting surfaces read it beside familiarity and after eligibility, so `D027` holds: a
  settlement with no pressure stays quiet however many faces were elected in it. History wins
  wherever there is any — an elected face is capped below `PlayerFamiliarity.HouseholdWeight`, and a
  candidate records `player_familiarity` or `recurring_contact` but never both, so the inspector
  says which evidence carried the decision. Somebody who lives on the player's land but is standing
  elsewhere is deliberately *not* elected: BQ-114 already reads them as the strongest tie the game
  has, so a slot spent on them would buy nothing and cost the settlement one of its three.
  Because BQ-115 was skipped, the defect it was holding up was measured before repairing it: on a
  genuinely fresh save — empty ledger, no relationships, zero affinity — BQ-114 read *every* face in
  town as a stranger, and the first situation cast the intended acquaintance in 25 runs of 100,
  which is one mark in four, which is chance. BQ-114's own done-when only reached 100/100 because
  its fixture writes an affinity of 70 by hand, a premise nothing in the mod produced. The same
  inertness sat in the storylet caster, where a fresh save's pool fell back to id order. Both now
  order on recognisability; no role requirement was relaxed, so `D026` is unchanged.
  Not in scope here: authored low-stakes beats for these faces to actually recur *through*, which is
  content and belongs with BQ-131.
- **Sources** engagement §4; PM §19.
- **Why** cheap to build, disproportionate in effect: it is what makes the first real situation land.

#### BQ-116 — Supply-line coupling *(stage S6, alongside BQ-050)*
Situations become the source of things Elin already makes players want: specialists, stock,
investment, materials, labour, safety, land.
- **Depends** BQ-048, BQ-050, BQ-051.
- **Done when** a player pursuing only town development finds engaging with situations the shortest route to a specialist they need.
- **Sources** engagement §3 Tier 1; Kenshi dependency model.
- **Why** this is the single highest-leverage engagement step. It converts the mod from a distraction into a supply line.

#### BQ-117 — Chronicle as trophy case *(stage S4, extends BQ-034)*
The history is readable as a narrative of who this character became — feuds, rescues, businesses
saved, places that carry their name — and exportable as text.
- **Depends** BQ-034, BQ-086.
- **Done when** a tester reads their own chronicle and can retell it to someone else without the game open.
- **Sources** engagement §3 Tier 3; Dwarf Fortress Legends.
- **Why** DF players generate worlds purely to read history. A shareable chronicle is the mod's best advertisement for itself.

#### BQ-118 — Standing sheet
A single readable view of earned access: contacts, safehouses, discounts, introductions, favours
owed and owing, standing with organizations.
- **Depends** BQ-055, BQ-113.
- **Done when** the player can see everything they have earned that is not money or an item.
- **Current implementation** `StandingSheet` is a derived projection in the same class as the
  journal and the Chronicle (`D022`): every line is read from state the save already carries — the
  obligation ledger in both directions, a site's admitted list, an organization's membership, and
  the game's own standing numbers read live through `IVanillaState` — so nothing is stored beside
  the truth and nothing can drift from it. It reports what is *held*, never a replay of what
  happened: open records only, and finished business stays the Chronicle's. It obeys `D008` like
  every other player surface, listing a record only where the player was a party to the event that
  created it, so a grudge formed off-screen cannot be handed to them as though they had been told.
  It reaches the player through the native journal tab and the `why?` inspector.
  Two things the engagement material names are deliberately absent because nothing records them
  yet: a **discount**, and per-town **Influence**, which Core has no way to enumerate.
  Because BQ-118 was skipped, one defect had to be repaired with it: BQ-112's reward audit could
  reach `FavorOwed` only through a `FavorOwed` event, and nothing in the mod records one — BQ-113
  mints the debt straight into the obligation ledger — so a save in which the player had genuinely
  earned the strongest reward in the vocabulary reported a vocabulary that did not contain it. The
  audit now reads the ledger, in the same idiom it already reads the knowledge graph.
- **Sources** engagement §3 Tier 2; Fallen London qualities.
- **Why** access-as-reward only motivates if the player can see it accumulating.

#### BQ-119 — Engagement telemetry *(debug only)*
Count situations generated, surfaced, engaged, ignored, and resolved by others. Local, debug-gated,
never transmitted.
- **Depends** BQ-100.
- **Done when** a play session reports its engagement profile in the inspector.
- **Sources** engagement §6; CD §35.
- **Why** the engagement test in engagement §6 cannot be answered by opinion.

#### BQ-120 — Intensity presets *(stage S9, extends BQ-111)*
Presets from "background texture" to "the world is busy", changing frequency and arrival, never
outcomes.
- **Depends** BQ-111, BQ-119.
- **Done when** the quietest preset leaves ordinary Elin play untouched and the loudest never scripts a result.
- **Sources** engagement §4; LW §10.7.
- **Why** autonomy is the strongest of the three motivational needs in a sandbox; the player must own the dial.

> **Engagement checkpoint.** The seven questions in `engagement-and-reward.md §6` must all answer
> yes. Question 1 is the one that matters: *can a player who wants nothing but a better town find
> the mod useful?* If not, the mod is a narrative distraction however good the simulation is.

---

### Setting-fidelity track — added after the player-culture research

Eight steps from [`design/setting-and-player-culture.md`](design/setting-and-player-culture.md),
which asks a question none of the other design documents ask: not what the mod should build, but what
this specific playerbase — English, Japanese and Chinese — already loves about Irva, so the mod
amplifies it instead of competing with it. Cited as `SP` below.

These are cheap. Almost none of them is a new system; most are a constraint or a weighting on a
system already in the plan. They are recorded as their own steps because a constraint that lives
only in a doctrine list gets forgotten, and because each has a real completion test.

#### BQ-121 — Everything is declinable *(stage S1, hardened through S9)*
No situation, arrival, conversation or thread can be entered involuntarily, and declining is never
penalised — no affinity loss, no karma, no closed door that would otherwise have been open.
- **Depends** BQ-005, BQ-009.
- **Done when** an automated pass declines every surfaced situation for an in-game month and the resulting save is indistinguishable from an unmodded one in player-facing state; and a test asserts no decline path writes a penalty.
- **Sources** SP §1; LW §10.7; standing rules §10 rule 21.
- **Why** 自由度 — degree of freedom — is the first thing all three language communities name about this game. Pressure breaks the one quality the entire playerbase agrees on. This outranks any engagement metric.

#### BQ-122 — Situations route into existing playstyles *(stage S3, extends the action library)*
Verb coverage extends past crafting and economy to the sidetracks players actually organise their
playthroughs around: **performing**, **museum donation**, **the ranch and breeding**, **fishing**,
and **farming as a supply answer**.
- **Depends** BQ-023, BQ-026, BQ-027, BQ-029.
- **Done when** a performance can resolve a social problem, a museum donation can settle a debt of honour, a bred animal can be a gift that changes a relationship, and a fishing haul can answer a shortage — each as a real mechanical route, not a dialogue tag.
- **Sources** SP §2; MD §26; PM §6.
- **Why** the Japanese material describes players who specialise in growing vegetables, in performing at parties, or in stealing from everyone. The design's solution-family thesis is not a hypothesis here — the playerbase has already sorted itself into those families. Meeting them there costs a verb each.

#### BQ-123 — The player's own pets and residents are narrative actors *(stage S6, extends BQ-049)*
Casting may draw on the player's pets, residents and adventurers-turned-companions: as witness,
victim, suspect, the subject of another actor's grudge, or the thing somebody else wants.
- **Depends** BQ-049, BQ-114.
- **Done when** a situation casts a named pet or resident of the player's own household, correctly, and survives that character being sold, married off, or killed.
- **Identity for pets and companions is the same BQ-144 read, not a second one.** A pet has a race and a character archetype and usually no work, service or institutional role, and that is a complete and correct answer rather than a gap to fill — the unread facets stay unknown and simply make it ineligible for the roles that need them. Personhood stays `NarrativeActorKind`/`SocialAgency`'s answer, and mutation safety stays `NarrativeActorClass`'s (BQ-031): a companion is not more or less protected because of what species the game says they are. Ownership boundary: **BQ-049** owns residents as situation origins, **BQ-114** owns how well the player knows them, **BQ-144** owns what the game says they are, and **this step** owns only their admission to casting and their survival of being sold, married off or killed.
- **Current implementation** `PlayerHousehold.Read(world, vanilla)` is the one place that says
  whose household this is. Two grounds, both the game's: the Home roll
  (`IVanillaState.GetHomeState`) and the party, read through a new seam member
  `GetPlayerCompanions()` paired with `VanillaCapability.ReadPlayerCompanions`. An actor may hold
  both ties and is then one member with the stronger of them — residency outranks the party,
  because only one of the two survives the player leaving somebody at home for a season.
  `HouseholdBond` says *how* somebody belongs and deliberately never what they are: species, work
  and character archetype stay BQ-144's `CharacterIdentity`, personhood stays
  `NarrativeActorKind`/`SocialAgency`, and reach stays `NarrativeActorClass`. There is no second
  pet model, and nothing about the household is stored — it is a live read on the same terms as the
  Home snapshot and the identity observation (`D004`, `D005`).
  **Admission was one wrong gate.** Social agency used to be a filter on the whole casting pool, so
  the player's own chicken was gone before any role could ask for one. It is now a requirement of
  the roles that need somebody to *speak* — testimony, proof, standing — and unknown agency still
  fails closed for every one of them. The pool is everybody present the registry knows as an actor
  and the game says is alive. One searched source is added, `HouseholdMemberHere`, and it is the
  only one that does not ask for agency, because being the subject of a scene is not something an
  actor does: a role written against it is who was hurt, whose loss is at issue, what somebody else
  wants or bears a grudge against. A household member who is to *say* something asks for the thing
  that says it (`AnyoneWhoKnowsFocus`); belonging to the household is what puts them first in that
  search, via BQ-114, and never what qualifies them.
  **Lifecycle is the absence of stored membership.** Sold, married off, dismissed, removed, dead —
  the game stops listing them or stops answering `Alive`, the next read simply does not include
  them, and nothing has to be cleaned up. `PlayerHousehold` treats `Dead` and `Unknown` alike, so
  an actor the adapter can no longer resolve is not described as living in the player's home. What
  a scene already recorded stays true: bindings live on the firing, the registry keeps its entries
  after the game has stopped answering, and a save reloaded after the whole household has turned
  over still finds every role holder (the quarantine rule that would otherwise throw the thread
  away is what the regression test asserts against).
  Unproven until a live run: which member of the player actually lists the party on the shipped
  build, and therefore whether companions are readable at all in game (`ELIN-Q-0029`). The Home
  roll half needs nothing new and is as proven as BQ-049 left it.
- **Sources** SP §3; LW §6.2; engagement §4.
- **Why** attachment is the precondition for stakes (BQ-114), and in Elin the attachment already exists — it lives in pets and residents, in every language community. A generated stranger has to earn what the player's own chicken already has.

#### BQ-124 — Losses are recoverable at a price *(stage S5, constrains every archetype)*
A dead contact, a burned relationship, a lost heirloom or a razed shop is a priced setback, not
deleted content. Every irreversible-looking loss has at least one expensive, uncertain route back.
- **Depends** BQ-002, BQ-052.
- **Done when** each archetype's worst outcome has a documented recovery route, and a test walks one situation to its worst end and back.
- **Sources** SP §3; MD §12; standing rules §10 rule 11.
- **Why** Elin already teaches this register: a dead pet is resurrected at a bartender for money, with its equipment and abilities intact. Loss that costs is native. Loss that deletes is not.

#### BQ-125 — Family and dependents weighted in generation *(stage S5, alongside BQ-039)*
Situation generation weights Family and Spouse edges heavily when choosing who is at risk, and
prefers a sibling, parent, child or dependent over a business relationship where both are available.
- **Depends** BQ-022, BQ-039.
- **Done when** across 100 generated situations, the person at risk is a family member or dependent in a clear majority of those where the relationship graph offered the choice.
- **Sources** SP §5; PM §32.
- **Why** every piece of Elona's emotional content the community records is domestic: a dying sister, a brother's memorial, a mother's incurable disease, the sister of Noyel. Not epics. `false accusation`, `fugitive` and `debt` all become stronger when the person at risk is somebody's sister rather than a merchant.

#### BQ-126 — Irva's own furniture as situation seeds *(stage S5, extends BQ-040)*
Generation draws premises from the setting's existing material — the gods, the guilds, the towns,
Nefia, the ether — rather than a parallel invented mythology. **Ether disease** is modelled
explicitly as a seed: an NPC who has it, a family hiding it, a cure that is expensive and uncertain.
- **Depends** BQ-039, BQ-040, BQ-028.
- **Done when** a majority of generated premises name something that exists in vanilla Irva, and an ether-disease situation runs end to end without inventing a cure the setting does not have.
- **Sources** SP §6; MD §2; standing rules §10 rule 7.
- **Why** both the Japanese and Chinese communities frame Elin as the legitimate evolution of Elona, and the affection extends to Noa personally. Players are not visiting a sandbox; they are returning to a world they have known for years. A Nefia that went quiet is of this world. A generic bandit conspiracy is not.

#### BQ-127 — Sincerity budget *(stage S7, pairs with the weirdness budget)*
The director tracks sincere content the way `CD §22.2` tracks weirdness: rare, rationed, and
surrounded by the ordinary and the absurd.
- **Depends** BQ-065, BQ-099, BQ-100.
- **Done when** the director can report the sincerity rate of a session, and the rate holds under long play without a hand-tuned exception.
- **Sources** SP §4; CD §22.2.
- **Why** Pael's mother and the Strange Diary are remembered *because* they sit in a game that wants you to die smiling and does not comment on cannibalism. Frequency would destroy the exact thing that makes them land.

#### BQ-128 — Tone is never signposted *(review gate, applies from S7 onward)*
No sincere situation is marked as one — no framing line, no tonal cue, no journal category, no
difference in presentation. The player discovers the register from the content.
- **Depends** BQ-074, BQ-127.
- **Done when** a reviewer given a mixed set of surfaced situations cannot sort them by intended register from presentation alone.
- **Sources** SP §4, §8; CD §21.
- **Why** `character-dialogue-system.md` already forbids explaining the joke. The same rule protects the sincerity, and for the same reason: the contrast is the mechanism, and announcing it spends it.

> **Setting checkpoint.** Before launch: BQ-121 holds under adversarial testing (there is no path
> into the mod a player did not choose), and §8 of `setting-and-player-culture.md` — do not moralise,
> do not out-write the game, do not make sincerity frequent, do not compete with the town — reads as
> a description of the shipped mod rather than an aspiration.

---

### Content-pipeline track — added after the authoring-format audit

Five steps from [`design/content-pipeline.md`](design/content-pipeline.md), which decides the
serialization question `CD §41` explicitly left open. Cited as `CP` below.

**These are not new systems. They are the shape three planned systems arrive in.** BQ-065's
storylets, BQ-074's fragments and BQ-070's speech-act vocabulary are all authored content whichever
way they are written; the only question is whether they are written as C# and extracted later, or
as data on the first day. Extraction is a migration nobody has budgeted.

So the track **opens immediately before BQ-065** — BQ-129 and BQ-130 build the format and the
compiler, and BQ-066's five storylets are the first artefact to use them — and its remaining three
steps interleave with the S7 steps whose content they carry: BQ-131 with BQ-065 and BQ-066, BQ-132
with BQ-074, BQ-133 with BQ-078.

This reverses one §8 deferral and leaves the other standing — see §8 and §11. It does not change
any content *target*; §11's fragment-volume resolution is unaffected.

#### BQ-129 — Content bundle format and loader *(stage S7, immediately before BQ-065)*
A flat, versioned, already-resolved bundle shipped beside the DLL and read by Core with no external
parser. Stable content ids; a malformed or missing bundle disables the affected content with a
diagnostic and never throws into Elin's frame.
- **Depends** BQ-005 (the degrade-to-diagnostic pattern), BQ-105.
- **Done when** Core loads a bundle with no package dependency added; a bundle with a truncated tail, an unknown version and a missing file each produce a logged diagnostic and a running game; and a headless test asserts the shipped assembly references no serialization package.
- **Sources** CP §3, §4; CD §41; standing rules §10 rule 5a.
- **Why** `Json.cs` was hand-written to keep Core dependency-free. A YAML parser in the shipped assembly would be the project's first external dependency, taken for a job that only happens at authoring time.

#### BQ-130 — Content compiler *(stage S7, with BQ-129)*
`tools/ContentCompiler`: reads authored YAML under `content/`, resolves ids and references, emits the
BQ-129 bundle. Build-time only — it is never referenced by the plugin and never ships.
- **Depends** BQ-129.
- **Done when** `dotnet run --project tools/ContentCompiler` turns `content/` into a bundle the loader accepts, the plugin build fails if the bundle is stale, and the compiler is absent from the shipped `Package/` output.
- **Sources** CP §3; CD §41.
- **Why** authoring wants comments, block text and reviewable diffs; the runtime wants a structure it does not have to interpret. A compiler is how both get what they want, and it is where authoring mistakes turn into build errors with a file and a line.

#### BQ-131 — Storylets are authored content *(stage S7, absorbs the content half of BQ-065 and BQ-066)*
`StoryletDefinition` is deserialized from the bundle, not constructed in C#. Preconditions, roles and
beats are data; the engine that evaluates them is code.
- **Depends** BQ-130, BQ-065.
- **Done when** all five of BQ-066's storylets exist only as files under `content/storylets/`, adding a sixth requires no C# change, and a storylet referencing an undefined role fails the build rather than the game.
- **Sources** CP §2, §5; CD §41, §11.
- **Why** the storylet library is the largest planned body of content in the project. Writing the first five in C# sets the pattern for the rest, and the pattern would then have to be undone.

#### BQ-132 — Fragments are authored content *(stage S7, with BQ-074)*
The `DialogueFragment` table is the bundle's fragment section, filed by semantic act and position
rather than by speaker.
- **Depends** BQ-130, BQ-074.
- **Done when** the realizer draws every fragment from the bundle, a new fragment reaches the game with no rebuild of the plugin assembly, and no fragment file is organised per-character.
- **Sources** CP §5; CD §18, §41.
- **Why** filing by speaker produces lines only one character can say — the reuse failure `engagement-and-reward.md` exists to prevent, arriving through a directory layout. Filing by meaning is the difference between a library and a script.

#### BQ-133 — Content validation and coverage report *(stage S7, gate before content scale)*
The compiler refuses dangling references, unknown semantic acts, duplicate or retired ids and
unreachable content, and emits a coverage report over act × position × tone × formality.
- **Depends** BQ-130, BQ-078.
- **Done when** each malformed-content class fails the build with a located message, and the report names cells with zero and with exactly one fragment — the holes and the repetition bugs — without prescribing what to write next.
- **Sources** CP §6; PM §6; §11 "Coverage as mandate versus report".
- **Why** a cell with one fragment is a repetition bug a player will find before we do. Counting cells measures the library; counting lines measures the effort.

> **Content checkpoint.** Before S7's 100-thefts test: no storylet, fragment or speech act is defined
> in C#; the save chunk contains content ids and events but no authored text; and loading a save made
> against an older bundle yields the current wording of the same history. If any authored text is
> reachable from the save, the separation has already failed and no amount of content will fix it.

---

## 6. Critical path and parallel work

Most of the plan is sequential by dependency, but three tracks can run in parallel once S2 is done,
which matters if more than one agent is working.

```
S0 → S1 → S2 ─┬─→ S3 → S5 ─┬─→ S6 ──┬─→ S8 → S9
              │            │        │
              ├─→ S4 ──────┘        │
              │                     │
              └─→ S7 ───────────────┘
```

- **Track A (systems):** S3 → S4 → S5 → S6. Actions, surfaces, archetypes, connections.
- **Track B (expression):** S7. Depends on S2 plus BQ-008, BQ-024 and — from BQ-145 onward — BQ-144, which sits in S4. That is the one place the expression track reaches back into Track A, and it is deliberate: the identity read is a seam change, and S7 consuming identity before the seam answers is how private probes into `Chara` get written.
- **Track C (places):** S8's site steps depend on BQ-029 and BQ-032, so they start once S3 and S4 land.
- **Content pipeline:** BQ-129 and BQ-130 depend on nothing in S3–S6 and can be built at any point once S2 is done. They must land *before* BQ-065.

Four steps are hard gates that block everything downstream and should never be deferred:
**BQ-012** (without the inspector, all later tuning is guesswork), **BQ-014** (the base game stops
being content without it), **BQ-032** (the only save-corrupting risk in the plan), and **BQ-057**
(personality that does not change decisions makes the whole of S7 cosmetic).

---

## 7. Definition of launch

1.0 ships when all of the following hold. Nothing else is required, and nothing here is negotiable.

**Playable loop.** A player encounters a situation the world generated, learns about it through
native presentation, solves it using their actual build through at least three genuinely different
routes, sees the world change, and finds it correctly remembered after save and reload.

**Breadth.** Seven situation archetypes. Roughly forty verbs covering every primary attribute and
every major skill family with real gameplay routes, not dialogue tags. Five or more storylets. The
100-thefts test produces structurally varied results.

**Autonomy.** Situations begin, escalate and can resolve without the player. At least one other
actor can solve a problem the player declined.

**Safety.** No configuration of the mod corrupts a save. Story-critical NPCs cannot be killed or
permanently relocated by the mod. Every capability can disappear without breaking the game. A
malformed thread is quarantined. Migration fixtures cover every schema version.

**Explainability.** The debug inspector answers every question in `LW §12` in game.

**Restraint.** A player can farm, build and explore for an hour without procedural interruption.

**Content is fixable after release.** No storylet, fragment or speech act is defined in C#, and no
authored text is reachable from a save. A wording fix ships as a new bundle and changes the past
tense of every existing save without a migration.

**Setting fidelity.** Nothing the mod offers is compulsory, and declining costs nothing. Generated
premises are recognisably of Irva. Sincere content is rare and unannounced. The mod does not
moralise, does not out-write the game's terseness, and does not compete with the player's town.

At launch, every system in §4 is **Complete-until-launch** and every unbuilt idea is in §8.

---

## 8. Post-launch register

Deliberately deferred. Recorded so that no idea is lost to a system moving on without it. Each entry
names why it is not in 1.0.

| Idea | Source | Why deferred |
|---|---|---|
| Mod interoperability API (`RegisterAction`, `RegisterSituationArchetype`, …) | PM §58 | Internal abstractions must stabilize first; a public API frozen too early becomes a cage. |
| Multiplayer support | PM §81; LW §81 | The save-chunk design assumes one authoritative world. Presently incompatible, not merely unproven. |
| Optional LLM prose rendering **at runtime** | MD §16.2; PM §45; CD §46 | The deterministic realizer is the baseline and must be good on its own first. Distinct from authoring-time drafting, which is permitted now under CP §7 because its output is reviewed, committed and static. |
| Full commodity economy | MD §14.3; PM §7.2 | Coarse pressure (BQ-050) is sufficient for narrative purposes; a real economy is a different project. |
| Regional-scale simulation (trade route safety, migration, settlement prosperity) | MD §25 | Requires the tiering in BQ-107 to be proven at scale first. |
| Writing vanilla `GlobalGoal` to embody BQ travel | VS §2.4, §3.3 | Elin already moves eligible global actors on its own, so a BQ write is a second author of the same state. The BQ-097 spike may prove it safe; until runtime evidence shows it makes travel *more reliable* rather than merely more native, milestone travel is the design and this is not a 1.0 dependency. |
| Apprentices and protégés | PM §31 | Emerges naturally from BQ-081 callbacks plus BQ-095 autonomy; revisit once both are live. |
| Marriage, inheritance and family succession beyond BQ-052 | PM §32 | Touches vanilla relationship systems the mod does not yet safely mutate. |
| Dynamic bounty ecosystem beyond BQ-046 | PM §20 | The single archetype proves the mechanism; a full bounty economy is content scale, not architecture. |
| Content authoring **workbench** (GUI editor, live preview) | CD §41 | Needed when non-programmers author fragments; before that it is overhead. The *format* is no longer deferred with it — see BQ-129 … BQ-133 and §11. |
| Signature lines and rare set-piece dialogue | CD §20 | Depends on a mature fragment library; premature investment while the realizer is young. |
| Weirdness level 4 (fever-dream events) | CD §22.2 | Deliberately rare; add only once levels 0–2 read as reliably Elin. |
| Cosmic absurdity category | CD §22.1 | Same reason. |
| Procedural festivals beyond BQ-047 | PM §15, §47 | One festival archetype proves the pattern; a calendar of them is content. |
| Guild-specific progression content | PM §55.7 | Vanilla guild loops must be integrated (BQ-037, BQ-038) before extending them. |
| Site states beyond persistence (Ruined → Repurposed → Forgotten → Rediscovered) | LW §7.7 | BQ-086 proves history accumulates; the full lifecycle is polish. |
| Tiny generated hamlets | PP §5 | Requires S6 economy/business/organization state, BQ-086 location history, BQ-139/BQ-143 site planning/mutation evidence, and S9 diversity/debug tools. Do not pull full settlements into 1.0. |
| Functional district graphs for villages/towns | PP §5 | Adds scale after tiny hamlets prove semantic structure; premature district generation would be visual variety without civic meaning. |
| Ongoing additive physical development of BQ-owned sites | PP §6, §7 | BQ-143 may prove one bounded mutation before launch; recurring physical development still needs scheduling, collision policy, repair behavior and broader save-compatibility hardening. |
| Satellite settlement lifecycle | PP §5, §6 | Safer than mutating vanilla towns, but still depends on hardened BQ-owned site genesis and additive development. |
| Vanilla-town additive physical mutation | PP §6, §7 | Highest collision risk with player construction, vanilla ownership, services and save-owned maps; only after BQ-owned site mutation is hardened and fail-closed. |
| Full settlement lifecycle (founding, growth, decline, abandonment, reuse) | PP §5, §6 | Requires generated hamlets, satellite lifecycle, additive development, autonomy/travel and director diversity to be proven together. |
| Player-facing Chronicle presentation polish | LW §3.3 | BQ-034 stores the history; making it beautiful is a UI project. |
| Human testing protocol formalization | PM §4 | Adopt the report template now informally; formalize when there are external testers. |

---

## 9. Idea coverage index

Every substantive idea in the canonical design documents, mapped to where it lives in this plan. This is
the audit artifact: if an idea is not here, it was missed.

### From `master-design.md`

| Idea | Where |
|---|---|
| Vanilla-first, situation-first, persistent entities | Standing rules §10 |
| Failure creates state | BQ-002, and every verb in S3 |
| Build diversity, three-plus solution families | Checkpoint S3, S5 |
| World knowledge is local | BQ-015, BQ-019 |
| Selective depth, importance ladder | BQ-021, BQ-107 |
| Explainability | BQ-012, BQ-104 |
| Entity registry, event ledger, fact graph, knowledge index | Built; frozen at BQ-001, BQ-002 |
| Evidence as real objects | BQ-017 |
| Rumour propagation, distortion | BQ-019, BQ-020 |
| Crime and witnesses | BQ-014, BQ-015 |
| NPC goals, personality, relationships | BQ-056 … BQ-062, BQ-022 |
| Organizations | BQ-053, BQ-054 |
| Situations, threads, director | BQ-039, BQ-052, BQ-099 … BQ-103 |
| Universal action framework, eight families | S3 |
| Capability discovery | BQ-003, BQ-109 |
| Four requirement kinds; impossibility not difficulty | Standing rules §10; S3 preamble |
| Check profiles, four outcomes, criticals | BQ-004; every S3 verb |
| Branching graphs, failure mutates the graph | BQ-002, BQ-052 |
| Attributes and skills as routes; training as a decision | Checkpoint S3 |
| Karma, fame, affinity, Influence, guilds, religion, Home | BQ-011, BQ-030, BQ-037, BQ-028, BQ-048 |
| Real items, real actions before abstract checks | BQ-017, S3 preamble |
| Procedural locations, site descriptors, persistence policy | S8 |
| Drama presentation, no storybook mode | BQ-005 … BQ-009, BQ-074 |
| Rewards and consequence propagation | BQ-002, BQ-009 |
| Time, escalation, off-screen tiers | BQ-013, BQ-107 |
| Save, migration, memory consolidation, adapter boundary | BQ-021, BQ-105, BQ-106 |
| Source-sheet strategy, data-driven vs coded | BQ-004; §8 (authoring tools) |
| Situation archetypes without branch explosion | S5 |
| Semantic tag ontology | BQ-069 |
| Deterministic testing, seed replay | BQ-001, BQ-104 |
| Quality metrics: route diversity, mechanical density, recurrence | Checkpoints S3, S5, S7 |
| Example questlines (brewer, grain route, chicken) | BQ-041, BQ-043, BQ-047 |
| Anti-patterns | Standing rules §10 |

### From `post-master-findings.md`

| Idea | Where |
|---|---|
| Gate A subitems | S1 |
| Four content classes | BQ-040 |
| Mechanical coverage metric and matrix | Checkpoint S3 |
| Mass shipments, category-based demand | BQ-050 |
| Coarse economic pressure | BQ-050 |
| Property-driven crafting commissions | BQ-026 |
| Guilds as information networks | BQ-037, BQ-038 |
| Situations arriving at the player | BQ-098 |
| Socially recognized conflict | BQ-046 |
| Systemic reward philosophy | BQ-009 |
| Inventory transfer safety | BQ-011 |
| Avoid custom arenas and artificial loot | BQ-088, BQ-091 |
| Activities, competitions, festivals | BQ-047 |
| Non-heroic stakeholder generation | Standing rules §10 |
| Distressed sellers and opportunities | BQ-040, BQ-045 |
| Rumour chains | BQ-019, BQ-020 |
| Emergent NPC importance | BQ-021, BQ-103 |
| Dynamic bounties | BQ-046; scale deferred §8 |
| Item provenance | BQ-085 |
| Social favors and obligations | BQ-055 |
| Profession-based solutions | BQ-064, BQ-026 |
| Generated local mysteries | BQ-039, BQ-044 |
| Consequential vanilla accidents | BQ-014, BQ-016 |
| Off-screen NPC schemes | BQ-095 |
| Witness disagreement | BQ-015, BQ-018 |
| Generated social networks | BQ-022 |
| Debt as a causal primitive | BQ-023, BQ-045 |
| Home as sanctuary | BQ-042, BQ-048 |
| Apprentices | §8 |
| Situation inheritance after death | BQ-052 |
| Adventurer ecology | BQ-096 |
| Traveling groups | BQ-097 |
| Shared player/NPC resolver | BQ-093 |
| Routine activities as delivery | BQ-035 |
| "What's been happening?" | BQ-036 |
| Relationship-dependent disclosure | BQ-072 |
| Occupation-dependent interpretation | BQ-064 |
| Persistent location history | BQ-086 |
| Regional legends | BQ-086 |
| Trivial grudges, absurd escalation | BQ-082 |
| Bad solutions are valid | Standing rules §10 |
| Simulation first, language second | Standing rules §10; BQ-070 |
| Failure-forward four outcomes | S3 preamble |
| Player production connected to demand | BQ-050 |
| Organization information routing | BQ-037, BQ-054 |
| Provenance + rumour + recurrence synergy | BQ-085 with BQ-081 |
| Demand + traveling groups synergy | BQ-050 with BQ-097 |
| Simulation tiers | BQ-107 |
| Director prioritizes attention | BQ-100 |
| Mod ecosystem lessons | BQ-011, BQ-088, BQ-009 |
| EA compatibility practice | BQ-109, BQ-110 |
| Runtime project boundary | Standing rules §10 |
| Interoperability API | §8 |
| Player configuration | BQ-111 |
| Proposed data structures | BQ-040, BQ-050, BQ-097, BQ-085, BQ-055 |
| Action expansion priorities | S3 |
| Information as gameplay inventory | BQ-071, BQ-073 |
| Evidence is not truth | BQ-017, BQ-018 |
| Fame changes visibility | BQ-039, BQ-100 |
| Affinity plus structured reasons | BQ-022, BQ-055 |
| Karma as legal status | BQ-025, BQ-046 |
| Influence as political currency | BQ-011, BQ-038 |
| Religion uses real state | BQ-028 |
| Archetype review checklist | Standing rules §10 |
| Vertical slices | BQ-041, BQ-042, BQ-047 |
| Determinism and replay | BQ-001, BQ-104 |
| Save migration rules | BQ-105, BQ-106 |
| Performance guardrails | BQ-108 |
| Compatibility guardrails | BQ-109 |
| Multiplayer | §8 |

### From `living-world-priorities.md`

| Idea | Where |
|---|---|
| Evidence ladder: discovered → … → stress verified | Standing rules §10; every Done-when |
| Native events over patches | BQ-014; BQ-005 |
| Source vs shipping architecture | Standing rules §10 |
| Version drift as first-class | BQ-109, BQ-110 |
| Simulation knows more than the player | BQ-033 |
| World changes over messages | BQ-009, BQ-035 |
| UI surfaces: board, journal, tracker, drama, log, ambient | BQ-033, BQ-034, BQ-035, BQ-040 |
| Information access as progression | BQ-064, BQ-072 |
| Mutation policy and actor classes | BQ-031 |
| Missing shopkeeper grades A/B/C | BQ-032 |
| Abstract off-screen relocation | BQ-097 |
| Selective simulation | BQ-107 |
| Recurring NPC continuity | BQ-103 |
| Social obligations | BQ-055 |
| Shop and service continuity states | BQ-051 |
| Home as consequence surface | BQ-048, BQ-049 |
| Coarse local economy | BQ-050 |
| Traveling groups | BQ-097 |
| NPC autonomy | BQ-093 … BQ-096 |
| Three-layer location model | BQ-089, BQ-091 |
| Reuse locations first | BQ-088 |
| Curated grammars | BQ-089 |
| Spatial affordances | BQ-090 |
| Generate, validate, score | BQ-092 |
| Persistent sites over disposable | BQ-087, BQ-086 |
| Causal enemies and loot | BQ-091 |
| First location proof | BQ-087 |
| Action expansion by leverage | S3 |
| Seven-archetype ecology | S5 |
| Conflicting good reasons; ugly solutions; failure as toys; tonal range | Standing rules §10 |
| Player not cosmologically central | BQ-094, BQ-096 |
| Reuse history aggressively | BQ-103 |
| Protect ordinary Elin play | BQ-099 |
| Director built late | S9 |
| Debugging scales with complexity | BQ-012, BQ-104 |
| Ten-question persistence checklist | BQ-105; applied per step |
| Priority roadmap P0–P10 | Absorbed into S1–S9 |
| Features to retain list | Mapped above or in §8 |
| Anti-patterns | Standing rules §10 |

### From `engagement-and-reward.md`

| Idea | Where |
|---|---|
| Supply-line coupling; simulation as source of wanted things | BQ-116 |
| Access, relationship and option rewards over payouts | BQ-112 |
| Favour as a stored, callable option | BQ-113 |
| Attachment before stakes | BQ-114, BQ-115 |
| Chronicle as trophy case, exportable history | BQ-117 |
| Standing sheet | BQ-118 |
| Earned, never random, arriving consequences | BQ-098 |
| Ignoring costs availability, not power | BQ-051, standing rules §10 |
| First situation matched to the player's build | BQ-114 |
| Small stakes as on-ramp | BQ-047, BQ-115 |
| Engagement test and telemetry | BQ-119 |
| Overjustification anti-pattern | BQ-112; standing rules §10 |

### From `character-dialogue-system.md`

| Idea | Where |
|---|---|
| Character model record | BQ-056 (extends existing `NarrativeNpc`) |
| Behavioural dimensions | BQ-056 |
| Conversational tendencies | BQ-075 |
| Problem-solving style | BQ-057 |
| Sensitivities | BQ-058 |
| Contradictions | BQ-059 |
| Quirks | BQ-060 |
| Identity anchors | BQ-059, BQ-060 |
| Vanilla identity is an input, not a template | BQ-144, BQ-145 |
| Identity as plausible knowledge, stakes, eligibility and vocabulary | BQ-145; BQ-064, BQ-067, BQ-068, BQ-076 |
| Topic model | BQ-076 |
| Emotional state | BQ-063 |
| Actor-local interpretation | BQ-064 |
| Character development over time | BQ-063; long-arc polish §8 |
| Values, needs, goal formation | BQ-061, BQ-062 |
| Mundane needs | BQ-061 |
| Storylet architecture | BQ-065 |
| Storylet library (36) | BQ-066 first five, authored as data at BQ-131; remainder is content within S7 |
| Casting engine | BQ-067 |
| Role chemistry | BQ-068 |
| Knowledge asymmetry | BQ-064, BQ-073 |
| Belief confidence and provenance | Built; projected at BQ-070 |
| Secrets require motives | BQ-071 |
| Social practices | BQ-084 |
| Semantic speech acts | BQ-070 |
| Disclosure and information control | BQ-071 |
| Occupational vocabulary | BQ-076 |
| Negative-space personality | BQ-077 |
| Dialogue realization and fragments | BQ-074; authored as data at BQ-132 |
| Voice profiles | BQ-075 |
| Four dialogue scales | BQ-074; signature lines §8 |
| Repetition control | BQ-078 |
| Weirdness taxonomy, budget, reactions, tone bible | BQ-079, BQ-080 |
| Weirdness distribution | BQ-060, BQ-079 |
| Callback system | BQ-081 |
| Humour through continuity | BQ-082 |
| Scene architecture | BQ-065 (as `NarrativeScene`) |
| Beat system | BQ-065, BQ-066 |
| Scene interruption | BQ-008 |
| Conversation state and commitments | BQ-083 |
| Player options from semantic state | BQ-134, BQ-070, BQ-006 |
| Nested contextual interaction menus | BQ-134 |
| Dialogue and world actions coexist | Standing rules §10 |
| Scene discovery channels | BQ-035, BQ-036 |
| Direction without markers | BQ-033, BQ-085 |
| Situation fingerprinting | BQ-101 |
| Familiarity over novelty | BQ-101, BQ-102 |
| Narrative conservation costs | BQ-103 |
| Escalation ownership | BQ-103; Standing rules §10 |
| Experience topology | BQ-101 |
| Anti-template testing | BQ-104 |
| Quality-diversity selection | BQ-102 |
| Development layer | BQ-069 |
| 22-step expression pipeline | S7 as a whole |
| Phases A–J | Mapped onto BQ-056 … BQ-086 |
| 100-thefts test | Checkpoint S7 |
| Content production strategy | S7 content work; coverage report BQ-133; workbench §8 |
| Mature building-block targets | §7 launch definition |
| Authoring tools | Format BQ-129 … BQ-133; workbench §8 |
| Debug tooling | BQ-012, BQ-104 |
| Save and persistence rules | BQ-105; content/save separation at BQ-129 |
| Memory decay and semantic GC | BQ-021 |
| Elin presentation integration | BQ-035, BQ-040 |
| Performance | BQ-108 |
| LLM policy | Standing rules §10; authoring-time drafting CP §7; runtime rendering §8 |
| Failure modes to reject | Standing rules §10 |
| Player/NPC action symmetry | BQ-093 |
| Milestones 1–7 | BQ-057, BQ-076, BQ-068, BQ-080, BQ-081, BQ-094, BQ-103 |
| Inspiration translation | Standing rules §10 |

### From `setting-and-player-culture.md`

| Idea | Where |
|---|---|
| 自由度 / freedom as the one universal community value | BQ-121; standing rules §10 rule 21z |
| Players self-sort into farmer, performer, thief | BQ-122; checkpoint S3 |
| Route situations into performing, museum, ranch, fishing, farming | BQ-122 |
| Attachment lives in pets and residents | BQ-123 |
| Resurrection economy: loss is priced, not deleted | BQ-124; standing rules §10 rule 21d |
| Domestic loss and family weighting | BQ-125 |
| Use Irva's own lore before inventing mythology | BQ-126; standing rules §10 rule 21e |
| Ether disease as a situation seed | BQ-126 |
| Cruelty as the setting for rare mercy; sincerity budget | BQ-127 |
| Never signpost tone | BQ-128; standing rules §10 rule 21c |
| Do not moralise | Standing rules §10; BQ-025, BQ-046 |
| Do not out-write the game's terseness | BQ-070, BQ-074; §7 launch definition |
| Do not compete with the town | BQ-116, BQ-121; §7 launch definition |
| Elin as the legitimate evolution of Elona | BQ-126; §7 launch definition |

### From `content-pipeline.md`

| Idea | Where |
|---|---|
| Behaviour / content / history separation | Standing rules §10 rule 5a; BQ-129 |
| Content never enters the save | BQ-129; content checkpoint |
| Authored YAML, compiled bundle, no runtime parser | BQ-129, BQ-130 |
| Build-time compiler that never ships | BQ-130 |
| Stable, non-reusable content ids | BQ-129, BQ-133 |
| Storylets and fragments as data from the first artefact | BQ-131, BQ-132 |
| File by meaning, never by speaker | BQ-132 |
| Coverage report over act × position × tone × formality | BQ-133 |
| Coverage measures, never mandates | BQ-133; §11 |
| Authoring-time model drafting, human promotion | CP §7; standing rules §10 rule 5 |
| Volume targets rejected | CP §8; §11 fragment volume |
| Workbench GUI deferred | CP §8; §8 |
| Combinatorial output counts rejected as a metric | CP §6, §8 |

### From `vanilla-simulation-integration.md`

| Idea | Where |
|---|---|
| Vanilla owns embodiment; BQ owns narrative meaning | D021; BQ-093, BQ-107 |
| Timetables, spans and `GetGoalFromTimeTable` are real state | BQ-135; `elin-api-notes.md` |
| Work, hobby and bodily needs are vanilla AI | BQ-061; BQ-135 |
| `Zone.Simulate()` catch-up on revisit | BQ-107; BQ-048, BQ-049 |
| Hourly `GlobalGoal` advancement for eligible global actors | BQ-032, BQ-095, BQ-097 |
| Four vanilla fidelity mechanisms, not two | BQ-107 |
| Actor activity snapshot as a read-only seam primitive | BQ-135 |
| Local affordance profile drives generation | BQ-039 |
| Character identity observation as a seam primitive | BQ-144 |
| Six identity facets stay separately typed, never a tag bag | BQ-144 |
| Observation versus BQ-derived interpretation | BQ-144 reads, BQ-145 derives |
| Identity is a live read, never persisted into the save | BQ-144; D004, D005 |
| Unknown identity degrades without defaults | BQ-144, BQ-145; D017 |
| Identity never generates personality or mutation permission | BQ-145; standing rules §10 rule 6a |
| Routine as plausibility weight, not an appointment gate | BQ-084, BQ-094 |
| Notable deltas only; routine acts unrecorded | BQ-040, BQ-094; BQ-035 |
| Opportunity is not eyewitness proof | BQ-094, BQ-095; D011 |
| Vanilla surprise is interpreted, not overridden | BQ-095 |
| No parallel needs, schedule, pathfinding or commodity simulation | BQ-050, BQ-061; standing rules §10 rule 6 |
| `GlobalGoal` writes deferred pending runtime proof | BQ-097; §8 |
| Runtime research list before any integration | BQ-135; `elin-api-notes.md` |
| Product-value test: causal continuity per unit of machinery | §7 launch definition |

### From `procedural-places-and-spatial-history.md`

| Idea | Where |
|---|---|
| Semantic/deep structure before geometry | BQ-089, BQ-139; standing rules §10 |
| Authored spatial atoms plus procedural composition | BQ-089, BQ-140 |
| World/history state to spatial requirements to physical realization | BQ-086, BQ-091, BQ-139, BQ-140 |
| Generate, score and validate candidates | BQ-092, BQ-141 |
| Procedural scenario dungeons as first-class sites | BQ-139, BQ-140 |
| Procedural problems before custom puzzle mechanics | BQ-090, BQ-140; standing rules §10 |
| Trigger/Condition/Effect scenario language | BQ-140; richer state machines deferred §8 unless needed for the proof |
| Causal dungeon ecology and occupancy | BQ-091, BQ-140 |
| Generated investigations as spatial scenarios | BQ-043, BQ-139, BQ-140 |
| Genesis separate from Development | BQ-087 proves genesis; BQ-143 proves one bounded additive mutation; ongoing development deferred §8 |
| Visited places are never destructively regenerated | BQ-087, BQ-086; standing rules §10 |
| Additive settlement evolution | §8 |
| Satellite settlements before vanilla-town mutation | §8 |
| Tiny generated hamlets before full settlements | §8 |
| Functional district graphs | §8 |
| Full settlement lifecycle | §8 |
| Spatial affordances feeding narrative generation | BQ-039, BQ-090 |
| Spatial realization feeds later generation | BQ-039, BQ-090, BQ-143; standing rules §10 |
| Spatial expressive-range and anti-template metrics | BQ-141 |
| Runtime evidence gate for map pieces/site insertion | BQ-087, BQ-140; `docs/elin/` |
| No duplicate economy, organization, pathfinding, routine or simulation systems | BQ-050, BQ-053, BQ-093, BQ-107; standing rules §10 |

---

## 10. Standing rules

The design documents each end in a doctrine list. Merged and deduplicated, they come to this. These
bind every step above; a step that violates one is wrong even if it works.

**What is true**

1. State before story. Cause before quest.
2. Dialogue expresses state; it never creates it. Storylets dramatize facts; they never author truth.
3. Evidence is not truth. Actors can be certain and wrong.
4. Knowledge is actor-local. Nobody knows what they could not have perceived, been told or inferred.
5. Optional prose may describe authoritative state; it may never manufacture it.
5a. C# is behaviour, authored data is content, the save is history. Content never enters the save; the save stores ids and events. Authored data selects, gates and words — it never computes.

**What to build with**

6. Vanilla mechanic before custom mechanic. Actual object before abstract evidence point. Real world interaction before dialogue abstraction.
6a. Vanilla identity is observed through the seam and kept typed by facet. It constrains plausibility, eligibility, stakes and pressure — never personality, never permission. Where the game did not answer, the answer is unknown.
7. Existing actor before new actor. Existing location before new location. Existing history before new backstory.
8. Curated grammar before unconstrained generation. Persistent site before disposable dungeon.
8a. Spatial meaning before geometry. Generate purpose, history, route constraints and affordances before physical realization.
8b. Genesis and Development are separate. A visited place is never destructively regenerated; later physical change is additive and fail-closed.
8c. Spatial realization feeds later generation. Verified physical facts project semantic affordances back into world-state generation; geometry is not only output.
9. Deepen the world before enlarging it.
10. Observed game behaviour beats API assumption. "Found" is not "works".

**How it should play**

11. Failure creates playable state; it never deletes content.
12. Preconditions gate impossibility, never low odds.
13. Every checked action has four outcomes, and the critical failure creates a new problem.
14. A valid ugly solution is still a solution.
15. Not everything interesting is a quest.
16. NPCs are actors, not dispensers. The player is important, not cosmologically privileged.
17. The world may solve, worsen or transform its own problems.
18. Mundane content preserves tonal range and makes major events feel major.
19. Weirdness is one absurd premise treated sincerely with real mechanical consequence — not a joke announced.
20. Drama must have an owner and a cause. Never manufacture escalation because a thread went quiet.
21. Protect ordinary Elin play. The player must be able to farm, build and explore uninterrupted.
21z. Everything is declinable, always, without penalty. Freedom is not a quality to preserve alongside the simulation; it is what the simulation exists to serve.
21a. Reward with access, relationships, standing, information and options — never with a payout attached to a story.
21b. Engaging must be the shortest path to something the player already wanted, not a detour from it.
21c. Sincerity is rationed like weirdness, and neither is ever announced. The contrast is the mechanism.
21d. Loss is priced, not deleted. Every setback has an expensive, uncertain route back.
21e. Use Irva's own furniture — its gods, guilds, towns, Nefia and ether — before inventing a parallel mythology.

**How to keep it alive**

22. World change before explanatory popup. Player knowledge before omniscient journal.
23. Mechanical coverage means actual gameplay use, never a dialogue tag.
24. Simulation depth follows relevance; performance comes from selective simulation, not less causality.
25. Safe mutation before dramatic mutation. One broken thread must never poison a save.
26. Debugging must explain every procedural decision.
27. One complete vertical slice before broad content.
28. Do not expand breadth faster than runtime evidence and playtesting justify.

---

## 11. Where the documents disagreed

Resolved here so nobody has to relitigate them.

**Three competing roadmaps.** `PM §72` stages 1–10, `LW §14` priorities 0–10 and `CD §38` phases A–J
all claim to be the next plan. **This document supersedes all three.** Their content is preserved in
§9; their sequencing is not.

**Verb count versus verb leverage.** `MD §26` targets "30–100+ actions"; `LW §8` says never add verbs
to hit a count. Resolved in favour of leverage: roughly forty, each justified by a playstyle it
unlocks, checked against the coverage matrix.

**Coverage as mandate versus report.** `PM §6` reads as an obligation to cover every skill; `LW §17`
rule 25 forbids breadth outrunning evidence. Resolved: the coverage matrix is a *report* that
measures what exists. It never drives what gets built next.

**Minimal Harmony versus the Drama patches.** `MD §19.3` calls for minimal patching; the shipped
projector uses three patches. Resolved: patches are acceptable at the presentation seam where no
event exists, and each must degrade to disabled-with-a-diagnostic (BQ-005). Everywhere else, native
events first.

**Project boundary versus shipping constraint.** `PM §57` implies separate assemblies. The chainloader
requires one DLL. Resolved: source separation is preserved, the shipped assembly is merged, and Core
still may not reference Elin. Consequence: Core types must avoid the game's generic names.

**Type names that collide with the game.** Elin puts a great deal in the global namespace, and the
game's type wins at any call site inside the plugin. Resolved by renaming on our side rather than
qualifying every use: `Goal` became `NpcGoal`, `Scene` became `NarrativeScene`, and `WorldInspector`
became `NarrativeInspector` (BQ-012, found only because the collision is silent until a member
lookup fails). `MD §23.2`'s `WorldInspector` name is superseded.

**Character model conflict.** `CD §4` proposes a `NarrativeNpc` that differs from the one that exists
and is persisted. Resolved: extend the existing type; migrate rather than replace.

**Journal philosophy.** `MD §3.2` describes a journal of objectives; `LW §3.3` refines it into five
knowledge states. The refinement wins (BQ-033).

**Fragment volume.** `CD §40` proposes two thousand fragments. Resolved: fragment authoring grows
with the storylets that need them, never ahead of them. The count is a direction, not a milestone.
The content pipeline (BQ-129 … BQ-133) does not disturb this: it makes content cheap and checkable,
never large.

**Authoring format as tooling versus as foundation.** §8 originally deferred the whole of `CD §41` —
tools and serialization together — as overhead until non-programmers were writing content. Resolved
by splitting it. The **workbench** is tooling and stays deferred on the original reasoning. The
**format** is not tooling: it is the shape BQ-066's storylets and BQ-074's fragments take on the day
they are written, and writing them as C# first makes their eventual extraction a migration nobody
budgeted. The format moves into S7 ahead of BQ-065; see `design/content-pipeline.md` §1.

---

## 12. Maintaining this document

- A new idea goes into a stage, or into §8 with a reason. It does not go into a chat log.
- A step that grows past one commit gets split, and the split is recorded here.
- When a system reaches Complete-until-launch, its remaining ideas move to §8 in the same commit.
- If reality contradicts this plan, reality wins and the plan is edited — with the reason in the
  commit message, so the next reader can see why the route changed.
