# Brilliant Questing — Agent Bootstrap

This is the default entry point for coding agents. Keep context retrieval progressive: do not build a complete mental model of the repository before starting a scoped task.

## Mission

Brilliant Questing is a persistent, simulation-driven questing layer for Elin.

Core doctrine:
- Generate persistent situations, not disposable quests.
- Prefer existing Elin mechanics as solutions instead of inventing parallel mechanics.
- Keep authoritative narrative state deterministic, inspectable, persistent, and independent of runtime LLM output.
- Treat the event ledger as history. Facts, beliefs, memories, relationships, and consequences derive from events rather than silently rewriting history.
- Keep truth, belief, proof, and institutional judgment distinct.

## Cold start

For an ordinary implementation task:

1. Read this file.
2. Establish the current state from a small Git query, not a maintained state summary:
   - inspect `git status`;
   - inspect recent commit subjects only if needed; do not reconstruct settled architecture from history;
   - identify one BQ step or scoped defect and its done-when condition.
3. Locate only that step in `docs/implementation-roadmap.md`; use the affected row in
   [the architecture/authority router](docs/architecture.md) to find the owning source and tests.
4. Read only the affected subsystem card, implementation and neighboring tests, not the entire map corpus.
5. Read `docs/agent/decisions.md` if an architectural question arises.
6. For native dependencies, route through [capability evidence](docs/elin/capabilities.md) to the
   relevant canonical API page/question; newer operation-specific evidence supersedes early API notes.
7. Read cited sections of `docs/design/` only when the task still has an unanswered design question.
8. If — and only if — the task is authoring or auditing narrative *prose*, read
   `docs/design/dialogue-writing-inspiration-research.md`. Dialogue fragments, voice/idiolect marks,
   storylet prose, callbacks, relationship/emotion wording, occupational vocabulary, narrative content
   generation, dialogue-quality audits. It answers how a line should be written; it decides nothing
   about the semantic model, which stays `docs/design/character-dialogue-system.md`'s.

Do not read `docs/implementation-roadmap.md` front to back for ordinary implementation.
Do not preload the design corpus or all subsystem/validation documents.
Do not generate or maintain a permanent `current-state.md`.

## Repository map

- `src/BrilliantQuesting.Core/` — deterministic simulation; no Elin, Unity, or BepInEx dependencies.
- `src/BrilliantQuesting.Plugin/` — live Elin adapter and presentation/integration.
- `tests/` — headless specifications and regression tests.
- `tools/BrilliantQuesting.Lab/` — headless simulation/probes.
- `tools/ApiDump/` — shipped-assembly metadata inspection.
- `docs/implementation-roadmap.md` — ordered BQ steps and done-when criteria; query by step.
- `docs/architecture.md` — authority lookup and subsystem source/test/Lab router, not status.
- `docs/agent/validation.md` — affected subsystem to targeted/full/native validation.
- `docs/agent/documentation.md` — exact documentation owners for contract changes.
- `docs/elin/capabilities.md` — navigation to canonical Elin evidence; early API notes remain historical where superseded.
- `docs/design/` — long-form design archive; retrieve selectively.
- `docs/design/dialogue-writing-inspiration-research.md` — the writing reference for authored
  dialogue. Prose only, and only for prose tasks; it is not part of an ordinary cold start.
- `docs/agent/decisions.md` — durable architectural decisions.
- `docs/agent/workflow.md` — context-efficient development procedure.
- `docs/agent/active-task.md` — optional ephemeral handoff template, not project status.

## Authority order

When sources disagree, prefer:

1. Current reproducible runtime observation or test.
2. Current code and tests.
3. Canonical `docs/elin/` API/evidence pages and unsuperseded `docs/elin-api-notes.md` findings
   for Elin facts; metadata never outranks reproducible runtime observation.
4. `docs/implementation-roadmap.md` for planned order and done-when criteria.
5. Design documents for intent/rationale.
6. Old commits and handoffs for historical context only.

`Found` in metadata is not the same as `works in game`.

## Context discipline

- Search before reading.
- Read the smallest useful file/range.
- Do not reread unchanged files already represented adequately in the current context.
- Do not conduct repo-wide audits unless explicitly requested or required by a checkpoint.
- Filter logs, diffs, searches, and test output.
- Use targeted tests during iteration and appropriate full validation once the change is coherent.
- Avoid overlapping subagents. Parallelize only genuinely independent investigations.
- Treat a completed BQ step as a natural context boundary.
- Preserve invariants, acceptance criteria, save compatibility, runtime evidence, API uncertainties, and failure cases even when optimizing context.

## Architectural invariants

- Core remains headless and must not reference Elin/Unity/BepInEx types.
- Vanilla systems own vanilla outcomes where possible; observe rather than duplicate their resolution.
- Vanilla owns embodiment; the mod owns narrative meaning — before building anything that moves, schedules, feeds or occupies an actor, read decision `D021`.
- Stable `EntityId` identity survives disappearance/reload of vanilla objects.
- Save/load must not redispatch historical events or reapply consequences.
- A stale external binding is not proof that physical evidence exists.
- Background simulation must not grant the player omniscient knowledge.
- Hide a procedural option only when genuinely impossible, not merely unlikely to succeed.
- Generic Drama projection stays narrowly scoped and failure-tolerant; do not rewrite authored Elin dialogue.
- Native integration fails closed with a diagnostic/fallback; a located API or saved handle is not proof of capability.
- No runtime LLM decides authoritative state, checks, facts, or consequences.

## Implementation contract

For a scoped BQ step:

1. Determine the concrete done-when condition.
2. Inspect only relevant implementation, neighboring tests, and required dependencies.
3. Make the smallest coherent change satisfying the requirement without weakening invariants.
4. Add regression coverage for semantic defects discovered while implementing it.
5. Use the affected route in [validation](docs/agent/validation.md), then its appropriate full/native gates.
6. Ask: did this alter an authority, subsystem contract, public seam, persisted state, native evidence,
   validation route or navigation path? If yes, update the exact [documentation owners](docs/agent/documentation.md)
   in the same feature commit. If no, do not churn architecture docs. Internal helpers alone do not qualify.
7. Report concisely: what changed, what was proved, and what still requires runtime verification.

Status lives in Git/code/tests. Never declare a step complete because an agent handoff says so.
