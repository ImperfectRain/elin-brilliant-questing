# Actors

Current BQ actor integration centers on `Chara` and stable `Card.uid` bindings.

- `Chara.uid`, `Card.uid`, `Chara.source`, `Chara.isDead`, `Chara.currentZone`, `Chara.idFaith`, `Chara.elements`, `Chara.things`, `Chara.LV`, and `Chara._affinity` exist in the installed assembly (`VERIFIED-METADATA`).
- Player binding through `EClass.pc.uid` and debug reporting of the loaded PC succeeded in the current runtime log (`VERIFIED-RUNTIME`).
- BQ stores `EntityId <-> uid` mappings in `ElinBindings`; unresolved or stale bindings do not prove physical presence (`INFERRED`, backed by BQ code and D004).
- Actor classification remains unresolved: candidate unique/story flags named by `ElinActorClasses` were not confirmed in the targeted metadata dump, so current code safely returns `Unknown` when it cannot read both flags (`UNRESOLVED`, `STUB-VERIFIED`).
- `Chara.idTimeTable`, `CurrentSpan`, `GetGoalFromTimeTable(int)`, `GetGoalWork()`, `GetGoalHobby()`, `Chara.global`, `GlobalData.goal`, `GlobalData.transition`, `TraitChara.UseGlobalGoal`, and `GlobalGoal.AdvanceHour()` exist (`VERIFIED-METADATA`). Which actors use them in ordinary play is unresolved (`UNRESOLVED`).

Current consumers: `ElinVanillaState`, `ElinBindings`, `ElinActorClasses`, `ElinActionObserver`, `DramaChoiceProjector`, `ElinSituationStager`.
