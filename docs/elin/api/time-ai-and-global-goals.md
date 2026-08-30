# Time AI And Global Goals

- `World.date.GetRaw()` is used for BQ `GameTime` (`VERIFIED-RUNTIME` through attached debug output).
- `Chara.idTimeTable`, `Chara.GetGoalFromTimeTable(int)`, `GetGoalWork()`, `GetGoalHobby()`, `Chara.global`, `GlobalData.goal`, `GlobalData.transition`, `TraitChara.UseGlobalGoal`, and `GlobalGoal.AdvanceHour()` exist (`VERIFIED-METADATA`).
- `Zone.Simulate()` and zone simulate hooks exist (`VERIFIED-METADATA`).
- Which concrete actors use global goals, when unloaded actors move, and what `Zone.Simulate()` mutates in player Homes remains unresolved (`UNRESOLVED`).
- Do not implement BQ needs/schedule/pathfinding simulation over these until `ActivityProbe` has runtime evidence (`UNRESOLVED`, D021).
