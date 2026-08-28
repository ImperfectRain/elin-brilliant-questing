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

---

## 6. Coverage, not line count

The measure of the library is **which cells are filled**, not how many lines exist.

The compiler emits a coverage report over the axes that actually gate selection: semantic act ×
position × tone × formality, with the personality and emotion ranges each cell covers. A cell with
one fragment is a repetition bug waiting for a player to find. A cell with none is a hole the
realizer will fall through at runtime.

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
