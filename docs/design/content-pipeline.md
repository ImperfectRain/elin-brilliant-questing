# Content Pipeline

**Status:** Design reference, adopted from an external proposal after audit
**Date:** 28 August 2026
**Relationship:** Answers a question `character-dialogue-system.md` deliberately left open. `CD §41`
says dialogue and storylets "should eventually be data-authored without simulation-code edits" and
that "exact serialization can be decided later". This document decides it, and decides *when*.

---

## 1. What this document changes

The roadmap's §8 register previously deferred the whole of `CD §41` under one line: *authoring tools
/ YAML content pipeline — needed when non-programmers author fragments; before that it is overhead.*

That reasoning was right about **tools** and wrong about **format**. A GUI workbench genuinely is
overhead until somebody who cannot read C# is writing content. But the *format* is not a tool. It is
the shape the first authored artefact takes, and the first authored artefact is BQ-066's five
storylets. Content written as C# and later extracted into data is a migration; content written as
data on the first day is not.

So the register is split. The pipeline moves into Stage S7 immediately before the storylet engine.
The workbench GUI stays deferred, with its original reasoning intact.

**This document does not raise the content target.** `§11` of the roadmap already resolved fragment
volume: authoring grows with the storylets that need it, never ahead of it. Nothing here reopens
that. A pipeline exists to make content *cheap and checkable*, not to make it *large*.

---

## 2. The three-way separation

The single load-bearing idea, and the reason this is worth a document:

| | Lives in | Changes when | Survives |
|---|---|---|---|
| **Behaviour** | C# under `src/` | the simulation's rules change | ships in the DLL |
| **Content** | authored source under `content/`, compiled to a bundle | a writer adds or edits a line | ships beside the DLL, replaced wholesale on update |
| **History** | the save chunk | the world happens | belongs to the player, never to us |

Everything follows from keeping those three apart.

**Content is never written into the save.** The save records that a storylet fired, by its stable
id, and what it did to the world. It never records the storylet's beats, its preconditions, or the
text of a line. A save made against version 3 of the content and loaded against version 4 must
produce the *new* wording of the *same* history — because the history is the ids and the events, and
the wording was never history in the first place.

This is not a nicety. Without it, every content edit is a save-migration problem, and the project
would be unable to fix a typo after release. The roadmap states this nowhere else; it is the single
most valuable thing in the proposal this document came from.

**Behaviour is never written into content.** Authored data selects, gates and words. It does not
compute. A storylet may require a belief; it may not decide what a belief is. A fragment may be
gated on `anger > 0.6`; it may not define anger. The moment authored data starts carrying logic, it
becomes a scripting language nobody designed, and the compiler cannot check it any more.

---

## 3. Authored source is YAML. The runtime never sees YAML.

Authoring wants YAML: comments, block text, diffs a human can review. The runtime wants none of
that.

`BrilliantQuesting.Core` has **no external package dependencies**, on purpose — `Json.cs` was
hand-written rather than take one. A YAML parser in the shipped assembly would be the first, for a
job the game never needs done: parsing a format only an author writes.

So the seam is a compiler:

```
content/**/*.yaml   →   tools/ContentCompiler   →   Package/content.bqc   →   read by Core
   (authored)            (build-time only)           (compiled bundle)        (no parser)
```

The compiler is a `dotnet run` tool in this repository. It never ships. The bundle is a flat,
versioned, already-validated structure the loader reads without interpreting anything — the same
posture as `elin-element-aliases.md`: data, not code.

Consequences worth stating plainly:

- **Authoring errors are build errors.** A dangling role reference or an unknown semantic act fails
  the build with a file and line, not at 2 a.m. in somebody's save.
- **The runtime loader is dumb and therefore fast.** It does no resolution, because resolution
  already happened.
- **A malformed bundle degrades to a diagnostic, never a crash.** Same rule as BQ-005: the mod
  disables the affected content and says so in the log. Missing content is missing content; it is
  never an exception thrown into Elin's frame.

---

## 4. Stable ids are the contract

Every authored thing carries an id that is chosen once and never reused for something else:
`storylet.public_accusation`, `fragment.debt_request_proud_01`, `act.accuse`.

The id is what the save stores, what the coverage report counts, what a callback in BQ-081 points
at, and what a bug report names. Renaming an id is a breaking change and is treated as one:
retire it, do not repurpose it. The compiler enforces uniqueness and refuses a bundle that
reintroduces a retired id with new meaning.

Deleting authored content is allowed. A save referring to deleted content reads as an event that
happened with no line left to say about it — which is exactly what it is, and is survivable.

---

## 5. Organise by meaning, not by speaker

The obvious filing system — a folder per NPC, a file per quest — is the one that kills reuse. It
produces content that only one character can say in only one situation, which is the Radiant failure
`engagement-and-reward.md` exists to prevent, arriving through the back door of a directory layout.

Content is filed by what it *means*:

```
content/
  storylets/       by social situation      public-accusation.yaml
  fragments/       by semantic act          accuse.yaml, refuse.yaml, apologize.yaml
  acts/            the speech-act vocabulary itself
```

A fragment belongs to an act and a position, and is selected by tags, requirements and voice — never
by whose file it was in. If a line can only ever be said by one named character, that is a signature
line, and `CD §20` already defers those.

### 5.1 What a fragment file says (BQ-132, BQ-146, BQ-142, BQ-149)

```yaml
- id: core.accuse.stop.looking.past      # globally unique; a long-term contract
  position: core                          # opener | core | modifier | callback | context | closer
  text: "If you want the thief, stop looking past {referent}."
  requires:                               # a closed reading, any-of within a key, all-of across keys
    act: accuse                           # every core fragment must declare one
    claim_predicate: stole
    referent: other
  forbids: {}                             # the same vocabulary, negated
  tone: [curt]                            # marked poles of four axes; unmarked fits every voice
  idiolect: [terse, literal]              # marked poles of three habit axes; unmarked fits every voice
  voice: [wry]                            # traits the speaker must actually have; never on a core
  tags: [trade]                           # lived-context vocabulary, manners, weirdness category/level/premise
  repetitionGroup: accuse
  memorability: signature                 # utility | voiced | signature | protected
```

The readings a fragment may be selected on are `DialogueReadings`', and every one of them is
something the simulation already decided: the act and its stance, the disclosure decision behind it,
the claim's predicate, who the referent is relative to the room, whether one person is being spoken
to or several, what old business is to hand and how the speaker comes by it, what they are audibly
feeling, and what they are to the listener. Adding a key means the simulation grew a distinction, not
that an author wanted a label. Placeholders are `DialogueSlots`' six and no more; a placeholder that
cannot be filled makes its fragment ineligible rather than resolving to "someone".

`idiolect` is the second closed vocabulary a voice narrows on (`BQ-142`, `D060`), and it is separate
from `tone` because it answers a separate question: `tone` is the pitch this line is taken at, and
`idiolect` is the habit that holds across every line a speaker says — length (`terse`/`expansive`),
cadence (`clipped`/`flowing`) and figuration (`literal`/`figurative`). Register is not among them
because `tone`'s `formal`/`plain` axis already is register. Marking is optional and most of the
library is unmarked; an unmarked fragment is wording every voice can still reach, so the corpus is
migrated a cross-section at a time. Both poles of one axis on one fragment is rejected at load.

`voice` is the same two vocabularies asked in the stricter direction (`BQ-149`, `D062`): a tag in
`tone` or `idiolect` says what this line *is*, and a tag in `voice` says what its speaker must *be*
for the line to be theirs at all. The difference is the default. A mark is narrowed on by
contradiction, so a voice that took no position on an axis leaves a marked fragment eligible — and
`wry` has no opposite pole at all, so a wry mark is one nothing can ever refuse. A demand is
`FitsVocabulary`'s rule instead: unrequested excludes. A fragment that demands a tag gains nothing
from also marking it.

**Reach for `voice` only where the sentence encodes a temperament its conditions do not.** A
`relationship` or an `emotion` condition says what is between two people or what state one of them is
in; if the wording also decides that the speaker is playful, prickly or fond about it, then without a
demand the tie is choosing the personality — every rival playful, every friend roguish. Three rules
keep this from becoming a second personality system:

- **a demand names a way of speaking, never a disposition.** What a line may require is how somebody
  talks. Wanting, risking, forgiving and refusing are `PersonalityWeights`' and the decision layer's,
  and they have already been spent by the time there is a sentence;
- **a demanding line always has an undemanding sibling for the same tie or mood.** A demand should
  narrow which reading of a relationship is available, never remove the relationship's wording
  altogether. Pinned by test over the shipped corpus, not by care;
- **a core may not demand at all**, and the loader refuses one that does. The core is the only slot
  that cannot fall silent, so a demand on one would turn a temperament into a refused act.

Demands stay a small minority of the library on purpose — the coverage report prints the count,
because a corpus where most lines are reserved is a corpus deciding personalities for people the
simulation described only as rivals.

**Occupational flavour is a domain of thought, not the name of a job.** A `tags` entry buys wording
that is eligible only for an identity `BQ-145` actually read (`D035`), and the way to spend it is the
concepts somebody who does that work thinks in — weather, season and rot; proportion and reaction;
grain, join and repair; price, debt and balance; routes, procedure and boundaries — rather than a
sentence that announces the trade. A line that names the job can only be said by somebody announcing
what they are, so it caricatures on its second appearance; a metaphor drawn from the work is said by
somebody who simply thinks that way, and it still has to win an ordinary draw against every plain
modifier in the same pool. Flavour never changes what is meant, so it lives on modifiers.

`memorability` is the one axis that is purely about wear: it changes how quickly a fragment counts as
stale, so a line somebody would quote is spent when it lands and "No." is not (`D055`).

**An act's unconditional core pool - the fragments reachable with no optional reading at all - has to
keep both poles of every `idiolect` axis reachable, or one pole.** A voice can only narrow a pool, never
widen it, so if every fragment answering an act from `act` alone happens to carry the same pole (every
one `terse`/`literal`, say), a voice built from the opposite pole is left with nothing to say for that
act the moment no optional reading (mood, tie, depth, ...) is available to fall back on - which a plain,
early conversation often is. When adding plain alternatives to a well-covered act, mark most of them and
leave at least one of the act's unconditional cores unmarked, or mark one toward the opposite pole,
rather than tagging every one of them the same way.

**A memorable line is an alternative, never the only wording a situation has (BQ-151).** `memorability`
prices wear per fragment; it says nothing about how a *situation* is worded, and the two came apart
badly in the optional slots. Eight of thirteen relationship modifiers, grief, and most of the context
slot had no `utility` wording at all — so a friend, a grieving speaker or a private room was worded
strikingly every single time it was worded, which is a catchphrase nobody authored. The rule is per
value of the reading that gates the slot: **any tie, mood, audience, kind of recalled history, or act
that some line is written for keeps a line written for the same value at `utility`.** Pinned by test
over the shipped corpus, and printed by `--coverage`'s always-on table and its "worded, but never
plainly" holes list — which counts *declarations* rather than eligibility, because a modifier with no
opinion about the tie is available to a friend but is not wording for having one.

This matters more here than in a core file for a structural reason: a core is drawn once per act, and
an opener, modifier, callback, context line or closer is drawn once per *line*. Getting the ratio
wrong in an optional family is heard several times a scene.

Two smaller rules fell out of the same pass:

- **a fragment marked `wry` in an optional slot must also demand it.** `wry` has no opposite pole
  (`D034`), so the mark alone refuses nobody and the line reaches every voice that simply took no
  position on sarcasm. Cores are exempt because a core may not demand at all;
- **a context fragment should be about the room.** Wording that is really about the speaker's patience
  or mood belongs on a modifier; put it in the context slot and it becomes eligible for every private
  line, which is how one courtesy ended up in three beats of one scene.

The writing side of all of this — cadence rather than synonym, prose tiers, what an optional fragment
has to earn, and the loaded words that quietly assert world state — is
[`dialogue-writing-inspiration-research.md`](dialogue-writing-inspiration-research.md) §11, §12, §18
and §19. Read it when authoring or auditing prose; it decides nothing about the pipeline.

### 5.2 What a storylet file says (BQ-131, BQ-146)

A storylet is a dramatic structure. **It contains no wording, and cannot** — every string in one is
an id, a tag or a member of a closed vocabulary, and the compiler refuses anything with whitespace
or sentence punctuation in it (`D051`).

```yaml
requiredRoles:  [{id: accuser, source: AnyoneWhoKnowsFocus}, {id: accused, source: FactSubject}]
optionalRoles:  [{id: knower,  source: AnyoneWhoKnowsFocus}]
preconditions:  [{kind: FocusPredicate, value: stole}, {kind: RoleKnowsFocus, role: accuser}]
resolutions:    [charge_pressed, charge_dismissed, taken_aside, owned_in_public]
beats:
  - id: name_charge
    speaker: accuser
    listener: accused
    intentions:                     # what could sensibly be said; the actor picks
      - {act: accuse, referent: accused}
      - {act: ask}
    requires: []                    # the storylet precondition vocabulary, per beat
    check:                          # an uncertainty, named, over a profile that exists
      {profile: proc_credibility, actor: accuser, target: accused, question: does_the_room_take_the_charge}
    playerIntersections: [compare_testimony, persuade, intimidate]
    consequences:                   # applied only when the thing they record happened
      - {hook: charge_named_in_public, event: AccusationMade, actor: accuser, target: accused, act: accuse}
    routes:                         # first match wins, in authored order
      - {when: check_pass, act: accuse, to: demand_answer}
      - {when: check_fail, act: accuse, to: invite_witness}
      - {when: always, ends: taken_aside}
```

**Adding a storylet needs no C# change**, and adding prose needs no storylet change. The two are
authored against each other only through `SpeechActType`, which both already speak.

Two rules an author will feel:

- *List the moves the situation makes sensible, not the move you want.* `ActorIntent` picks from the
  speaker's own state, so a beat offering only `accuse` is a beat where nobody may hesitate.
- *A check answers a question.* `question` is required and is a slug, so a roll for atmosphere cannot
  be written; if nothing is genuinely in doubt, leave the check out.

---

## 6. Coverage, not line count

The measure of the library is **which cells are filled**, not how many lines exist.

The compiler emits a coverage report over the axes that actually gate selection —
`ContentCompiler --coverage [path]`. Act × position first, because an act with no core has no words
at all whatever else the library holds; then act × commitment, disclosure depth, audience, audible
emotion, relationship and requested tone over cores; then the distinctiveness distribution and a line
per storylet (beats, whether it routes, how many endings, how many distinct acts, how many checks).
A cell with one fragment is a repetition bug waiting for a player to find. A cell with none is a hole
the realizer will fall through at runtime. The report ends with exactly those two lists.

This is deliberately *not* a combinatorial argument. Multiplying axis sizes together to claim a
large number of possible outputs measures nothing: most of the product is unreachable, and the
player experiences the cells they actually hit. The report counts real cells and their real
occupancy, and the launch definition asks for the hot cells to be full — not for a total.

The report is the same kind of artefact as the mechanical coverage matrix, and inherits the same
rule from roadmap §11: **it is a report, not a mandate.** It measures what exists. It never decides
what gets built next.

---

## 7. Drafting with a model, promoting by hand

Fragment writing is the one part of this project where volume is genuinely the problem, and it is a
reasonable place to use a language model — at **authoring time**, in this repository, producing text
a human reads before it is committed.

That is categorically different from the LLM prose rendering deferred in §8, and the difference is
worth being precise about, because conflating them would smuggle a deferred decision back in:

- **Authoring-time drafting** produces static data. It is reviewed, edited, committed, diffed, and
  reproducible from the repository forever. The shipped mod contains no model and makes no calls.
- **Runtime prose rendering** produces text the player sees that nobody reviewed. It remains
  deferred, and the deterministic realizer remains the baseline that must be good on its own.

The rules for drafting are the same rules that bind every other line in the project: a draft may
word authoritative state, never invent it; it is checked against the tone bible and the weirdness
budget before promotion; and nothing enters `content/` that a person has not read. Unreviewed
generated text is not content. It is filler with a plausible shape, which is worse than a hole,
because a hole shows up in the coverage report.

---

## 8. What was rejected, and why

The proposal this document draws on was adopted in substance. Four things in it were not, and are
recorded here so they are not quietly re-proposed:

**Volume targets as milestones.** Numbers of fragments and storylets presented as things to reach.
Roadmap §11 already resolved this against `CD §40`; the pipeline does not get to reopen it by
arriving with a spreadsheet. Content grows with the storylets that need it.

**A GUI content workbench.** Stays in §8 on its original reasoning. Text files under version control
are a better authoring surface than a bespoke editor for as long as the authors can read a diff, and
a workbench built before the schema settles would be built twice.

**YAML at runtime.** Covered in §3. The dependency is the objection, not the format.

**Combinatorial output counts as a quality claim.** Covered in §6. "Millions of possible lines" is
arithmetic, not a player experience.

---

## 9. Where this lands in the plan

Roadmap steps **BQ-129 … BQ-133**, placed in Stage S7 immediately before BQ-065, so the storylet
engine's first five storylets (BQ-066) and the fragment schema (BQ-074) are authored data from the
day they exist rather than C# awaiting extraction.

| Step | What |
|---|---|
| BQ-129 | Content bundle format and runtime loader |
| BQ-130 | The build-time compiler |
| BQ-131 | Storylets authored as content |
| BQ-132 | Fragments authored as content |
| BQ-133 | Validation and the coverage report |
