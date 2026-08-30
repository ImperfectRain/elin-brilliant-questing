# Time AI And Global Goals

- `World.date.GetRaw()` is used for BQ `GameTime` (`VERIFIED-RUNTIME` through attached debug output).
- `Chara.idTimeTable`, `Chara.GetGoalFromTimeTable(int)`, `GetGoalWork()`, `GetGoalHobby()`, `Chara.global`, `GlobalData.goal`, `GlobalData.transition`, `TraitChara.UseGlobalGoal`, and `GlobalGoal.AdvanceHour()` exist (`VERIFIED-METADATA`).
- `Zone.Simulate()` and zone simulate hooks exist (`VERIFIED-METADATA`).
- `GameDate.AdvanceHour()` iterates `EClass.game.cards.globalCharas.Values`; for each non-party actor outside `EClass.game.activeZone`, if `chara.trait.UseGlobalGoal` is true, vanilla creates a `GlobalGoalAdv` for non-PC-faction actors without a goal and then calls `global.goal.AdvanceHour()` (`SOURCE-OBSERVED`).
- `GlobalGoal.AdvanceHour()` increments the goal hour counter and calls virtual `OnAdvanceHour()` (`SOURCE-OBSERVED`). `GlobalGoalAdv` and `GlobalGoalVisitAndStay` are vanilla travel/stay goals that can call `owner.MoveZone(...)` (`SOURCE-OBSERVED`).
- Read-only BQ-135 mapping should expose: `currentZone.uid`, whether `global` exists, `global.goal` type, `global.transition` state/coordinates/last-zone uid, `idTimeTable`, current timetable span/goal type from `GetGoalFromTimeTable`, work/hobby goal type, and `TraitChara.UseGlobalGoal` (`SOURCE-OBSERVED`, `VERIFIED-METADATA`). Do not mutate `GlobalData` or `GlobalGoal`.
- Which concrete ordinary town citizens use global goals, and the practical resource effects of `Zone.Simulate()` on player Homes, remain runtime questions (`UNRESOLVED`). Do not implement BQ needs/schedule/pathfinding simulation over these until `ActivityProbe` has live samples (D021).
