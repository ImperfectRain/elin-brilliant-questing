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
   - inspect roughly the latest 5–10 commit subjects/messages as needed;
   - identify the relevant BQ step.
3. Locate only the relevant BQ step in `docs/implementation-roadmap.md`.
4. Inspect the implementation and tests directly related to that step.
5. Read `docs/agent/decisions.md` if an architectural question arises.
6. Read `docs/elin-api-notes.md` only for relevant Elin/runtime facts.
7. Read cited sections of `docs/design/` only when the task still has an unanswered design question.

Do not read `docs/implementation-roadmap.md` front to back for ordinary implementation.
Do not preload the design corpus.
Do not generate or maintain a permanent `current-state.md`.

## Repository map

- `src/BrilliantQuesting.Core/` — deterministic simulation; no Elin, Unity, or BepInEx dependencies.
- `src/BrilliantQuesting.Plugin/` — live Elin adapter and presentation/integration.
- `tests/` — headless specifications and regression tests.
- `tools/BrilliantQuesting.Lab/` — headless simulation/probes.
- `tools/ApiDump/` — shipped-assembly metadata inspection.
- `docs/implementation-roadmap.md` — ordered BQ steps and done-when criteria; query by step.
- `docs/elin-api-notes.md` — discovered and verified Elin runtime/API facts.
- `docs/design/` — long-form design archive; retrieve selectively.
- `docs/agent/decisions.md` — durable architectural decisions.
- `docs/agent/workflow.md` — context-efficient development procedure.
- `docs/agent/active-task.md` — optional ephemeral handoff template, not project status.

## Authority order

When sources disagree, prefer:

1. Current reproducible runtime observation or test.
2. Current code and tests.
3. `docs/elin-api-notes.md` for Elin API/runtime facts.
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
- No runtime LLM decides authoritative state, checks, facts, or consequences.

## Implementation contract

For a scoped BQ step:

1. Determine the concrete done-when condition.
2. Inspect only relevant implementation, neighboring tests, and required dependencies.
3. Make the smallest coherent change satisfying the requirement without weakening invariants.
4. Add regression coverage for semantic defects discovered while implementing it.
5. Run targeted validation, then the appropriate full build/test suite.
6. Report concisely: what changed, what was proved, and what still requires runtime verification.

Status lives in Git/code/tests. Never declare a step complete because an agent handoff says so.
