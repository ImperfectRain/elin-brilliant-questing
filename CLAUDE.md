# Claude Code Entry Point

Read `AGENTS.md` first and follow it as the shared repository instruction set.

Do not duplicate the repository model in this file. Its purpose is to keep Claude-specific startup context small.

Additional Claude Code rules:
- Use `/clear` or a fresh session when moving to an unrelated BQ step.
- Use compaction only when continuing the same long task.
- Prefer bare paths plus selective reads over injecting whole large files into context.
- Inspect `/context` when a fresh session has unexpectedly high baseline context.
- Disable or avoid irrelevant MCP/tool surfaces when practical.
- Do not use subagents for redundant repository discovery.
- If a task is interrupted and cannot be resumed from Git alone, use `docs/agent/active-task.md` as a temporary handoff and reset it after the task is complete.

Git, current code, tests, and runtime evidence are authoritative for current implementation state.
