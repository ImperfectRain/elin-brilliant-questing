# Documentation ownership and maintenance

## Completion contract

At feature/rework completion ask:

> Did this change alter an authority, subsystem contract, public seam, persisted state, native
> capability evidence, validation route, or documentation navigation path?

**No:** do not churn architecture docs. **Yes:** update the exact owners below in the same feature
commit. Internal helpers and implementation-only renames do not require architecture edits unless
a navigation link breaks. Read the affected source and a representative test before asserting a
contract; do not infer ownership from filenames or old roadmap prose.

## Maintenance matrix

| Trigger | Owning document to review/update | Do not duplicate |
|---|---|---|
| Authority/public subsystem boundary | Affected card linked by [architecture](../architecture.md); authority matrix only if its answer changes; [flow](../systems/flow.md) for changed joins | Class inventory or implementation history |
| Durable architectural reason changed | Relevant [decision](decisions.md), linked from owning card | Full rationale in every card/commit |
| Action/binding/check semantics | [Actions](../systems/actions.md), affected semantic contract in [character design](../design/character-dialogue-system.md) only if intent changes | Per-verb copies of availability or resolver rules |
| Persisted field/schema/default/ownership | [Persistence contract](../systems/integration.md#persistence), affected storage card; migration/round-trip tests | Manually maintained schema number or every serialized key |
| New native observation or changed support | Exact [Elin API page/question](../elin/capabilities.md), affected detailed matrix row and capability routing row | Another API notebook; refresh of old snapshot counts |
| Test/build/live validation route changes | [Validation](validation.md), owning card's proof link if entry point changed | Volatile pass counts, timings or last green hash |
| New/removed Lab diagnostic | Owning card and validation route when it becomes a primary inspection path; [LabCatalog](../../tools/BrilliantQuesting.Lab/Cli/LabCatalog.cs) remains executable discovery | A second exhaustive scenario inventory |
| Storylet/content/fragment/grammar vocabulary | [Expression](../systems/expression.md) or [sites](../systems/world.md#sites); [content pipeline](../design/content-pipeline.md) for authoring contract changes | Authored content or prose copied into architecture/save |
| Player knowledge/presentation contract | [Integration](../systems/integration.md#discovery); native visual evidence in [journal UI](../elin/api/journal-ui.md) when relevant | Inspector state in player projections |
| Missing join closed / new demonstrated gap | [Flow](../systems/flow.md) and affected [extension seam](../systems/extension-seams.md); evidence stays with owner | Speculative requirements or unfinalized step numbering |
| Roadmap order/dependency/done-when changed | [BQ roadmap](../implementation-roadmap.md) for launch/hardening, [BQa roadmap](../living-world-roadmap.md) for the subsequent phase; extension routing only if navigation changes | Settled architecture copied back into Current implementation essays |
| Deleted/moved entry point | Links in affected router/card; run doc checker | Architecture edits for an unreferenced internal rename |

## Document authority and graph

| Document | Owns |
|---|---|
| [AGENTS](../../AGENTS.md), [CLAUDE](../../CLAUDE.md) | Shared bootstrap; Claude-specific delta only |
| [Architecture](../architecture.md) → subsystem cards | Current contract navigation and ownership, subordinate to reproducible code/test/runtime evidence |
| [Flow](../systems/flow.md) | Joins and host boundaries; links to contracts rather than restating them |
| [Elin capabilities](../elin/capabilities.md) → API/evidence pages | Operation-level evidence navigation; exact evidence remains canonical in Elin docs |
| [Validation](validation.md) | Change-to-check routing |
| This file | Change-to-document routing |
| [Decisions](decisions.md) | Durable why |
| [BQ implementation roadmap](../implementation-roadmap.md) | Unfinished BQ launch/hardening order, dependency, done-when and evidence references |
| [BQa living-world roadmap](../living-world-roadmap.md) | Authoritative post-BQ sequence, start gate, causal contracts and beta acceptance |
| Design archive | Intent/rationale; superseded statements visibly marked |
| Git/tests/runtime | Status, historical diffs and current proof |

Backlinks are navigation, not circular authority: a roadmap link does not make planned behavior
implemented; a card link does not upgrade native evidence. Do not create `current-state.md` or
maintain hashes, test counts, line counts, branch status, active-task counts or progress snapshots.
The ephemeral handoff template remains only for unfinished work that cannot resume from Git/prompt.

## Roadmap policy

Keep existing Current implementation sections as historical context unless a specific claim is
wrong; mark supersession and link to the corrected owner. Do not perform bulk destructive migration.
For new work, prefer a short implementation/evidence reference under the step. Put settled ownership
in its card, durable reasons in decisions, and implementation history in Git. The roadmap remains
authoritative for planning, not a compulsory reconstruction archive.

Future proposals are provisional until a committed document explicitly designates their authority.
Consult proposals as input without copying numbers/dependencies into system truth. The [adopted BQa roadmap](../living-world-roadmap.md) is authoritative only for the post-BQ phase.
Adoption updates the [planning seam](../systems/extension-seams.md), not implemented subsystem truth.

## Drift check

Run `python tools/validate_docs.py` when navigation changes. It checks local inline links and
Markdown headings in the living graph; pass explicit repository-relative Markdown paths to check
additional files. Its scope is deliberately small; no generated prose, symbol parser, network or
new build dependency. Human review must still compare ownership/evidence against source/tests.
Run its unit tests if changing the checker. See [validation](validation.md#content-and-full-gate).
