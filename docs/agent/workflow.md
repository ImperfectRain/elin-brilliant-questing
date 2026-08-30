# Context-Efficient Agent Workflow

The objective is not minimum token use at any cost. The objective is minimum irrelevant context while preserving implementation quality.

## 1. Start from live truth

At the start of a task, establish only what you need:

```bash
git status --short
git log --oneline -10
```

Read fuller commit messages only for the recent BQ work relevant to the task.

Do not maintain a prose snapshot of the entire current implementation. It becomes stale faster than Git.

## 2. Identify the task boundary

Normal unit of work: one BQ roadmap step or one tightly scoped defect.

Find the step with targeted search, for example:

```bash
rg -n "BQ-XXX" docs/implementation-roadmap.md
```

Read the surrounding section only. Expand outward only when the section explicitly depends on another rule/track.

## 3. Retrieve code progressively

Use this escalation order:

1. symbol/search result;
2. relevant file;
3. neighboring tests/callers;
4. subsystem;
5. architecture/API notes;
6. design corpus.

Do not reverse this order by reading architecture/design material first unless the task itself is architectural.

## 4. Keep tool output bounded

Prefer:
- `git diff --stat` before a full diff;
- `git diff -- path/to/relevant/file`;
- targeted `rg`;
- targeted tests during iteration;
- filtered/tail log excerpts;
- full test/build once the change is coherent.

Avoid placing giant build logs, full repository listings, or unrelated diffs into the conversation.

## 5. Preserve reasoning quality

Never optimize away:
- invariants;
- acceptance/done-when criteria;
- save compatibility;
- runtime evidence;
- failure modes;
- API uncertainty;
- semantic regression tests.

Reduce repeated discovery and irrelevant material instead.

## 6. Session boundaries

A completed BQ step is normally a context boundary.

Start a fresh context for the next unrelated step. Repository state is the persistent memory.

Continue the same context when:
- debugging the change just made;
- completing a still-open BQ step;
- the unresolved reasoning itself is expensive to reconstruct.

If a long task must survive a reset, temporarily fill `docs/agent/active-task.md`.

## 7. Branch flow

The authoritative primary development branch is `master`. Every BQ step since the branches were
consolidated has landed there, and it is the only long-lived branch on the remote.

Start normal development from the current primary development branch. Temporary agent/task
branches are disposable workspaces, not independent long-lived development lines, and old task
branches should not become the starting point for future work after their commits are integrated.

One coherent BQ step should normally produce one commit. Once validated, a completed linear task
branch should be fast-forwarded into the primary development branch before starting the next
unrelated BQ step whenever the environment permits.

Never force-update the central branch unless explicitly required and reviewed. Never discard unique
commits merely to simplify branch topology.

## 8. Handoffs

Use `active-task.md` only when Git and the user prompt are insufficient to resume unfinished work.

Record:
- goal;
- current status;
- changed files;
- evidence already gathered;
- failed approaches;
- next action;
- important symbols;
- things not to repeat.

Keep it short. Reset/delete the filled handoff when the task is complete. Never treat it as authoritative current project state.

## 9. Audits and subagents

Do not perform a global audit on every implementation step.

Use broad audits at meaningful checkpoints, before major architectural changes, or when explicitly requested.

Use subagents only for separable investigations such as:
- independent runtime/API research;
- external compatibility research;
- isolated subsystem analysis with non-overlapping context.

Do not split Core, Plugin, tests, docs, and synthesis across agents merely for parallelism if each must reconstruct the same repository context.

## 10. Model/effort routing

Use the least expensive reasoning level that reliably preserves quality.

Routine scoped implementation, mechanical refactors, tests, and cleanup generally do not require maximum reasoning.

Escalate for:
- ambiguous architecture;
- cross-cutting save/state changes;
- difficult runtime integration;
- subtle systemic bugs;
- design decisions with large downstream cost.

A strong model can decide the architecture; implementation can often proceed with a cheaper model once the decision is recorded.

## 11. Completion

Before declaring completion:
- check the relevant done-when criterion;
- run appropriate tests/build;
- identify runtime-only proof still outstanding;
- inspect the focused diff;
- keep the final report concise.

Do not write a new permanent state summary after every commit.

## 12. Commit messages

Routine BQ commit bodies should normally be concise: roughly 100-250 words, or equivalent. Include
the behavior changed, important constraints, validation run, and non-obvious decisions. Use longer
bodies only for genuinely important architectural findings. Do not duplicate the full implementation
diff or the design rationale already present in docs.
