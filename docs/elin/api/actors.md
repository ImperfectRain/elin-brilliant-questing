# Actors

Current BQ actor integration centers on `Chara` and stable `Card.uid` bindings.

- `Chara.uid`, `Card.uid`, `Chara.source`, `Chara.isDead`, `Chara.currentZone`, `Chara.idFaith`, `Chara.elements`, `Chara.things`, `Chara.LV`, and `Chara._affinity` exist in the installed assembly (`VERIFIED-METADATA`).
- Player binding through `EClass.pc.uid` and debug reporting of the loaded PC succeeded in the current runtime log (`VERIFIED-RUNTIME`).
- BQ stores `EntityId <-> uid` mappings in `ElinBindings`; unresolved or stale bindings do not prove physical presence (`INFERRED`, backed by BQ code and D004).
- Actor classification is partially resolved. Installed `Card` exposes `IsUnique`, `IsImportant`, `c_uniqueData`, and `c_isImportant` (`VERIFIED-METADATA`). `IsUnique` is `rarity == Rarity.Artifact`; `IsImportant` is `sourceCard.HasTag(CTAG.important)`; `c_isImportant` is persisted int flag 109; `c_uniqueData` is persisted object slot 6 (`SOURCE-OBSERVED`). `Chara` also exposes `quest`, `memberType`, `faction`, `IsHomeMember`, `IsBranchMember`, `IsPC`, `IsPCParty`, and `IsGlobal` (`VERIFIED-METADATA`).
- Conservative relocation classification guidance: treat PC/party as never relocatable; treat `TraitUniqueChara`, `IsUnique`, `c_uniqueData != null`, adventurers, `IsImportant`, `c_isImportant`, active quest holders, and unknown trait/story rows as unsafe; treat BQ-generated actors as generated/safe only when BQ owns their creation; treat ordinary loaded non-global town citizens as not safe for Grade-B absence until movement probes prove a vanilla-safe path (`SOURCE-OBSERVED`, `INFERRED`, `UNRESOLVED` runtime samples).
- `Chara.idTimeTable`, `CurrentSpan`, `GetGoalFromTimeTable(int)`, `GetGoalWork()`, `GetGoalHobby()`, `Chara.global`, `GlobalData.goal`, `GlobalData.transition`, `TraitChara.UseGlobalGoal`, and `GlobalGoal.AdvanceHour()` exist (`VERIFIED-METADATA`). Which actors use them in ordinary play is unresolved (`UNRESOLVED`).

- Actor *classification* and settlement *residency* are different questions and only the first is answered today. `NarrativeActorClass` says how far the mod may reach into somebody; it does not say whether they belong to the place they are standing in, and it returns `OrdinaryCitizen` for anything the story flags do not claim. BQ-039a generation consequently admits any classified live actor in the zone, wildlife and passing hostiles included. No verified read closes this, and none was guessed at (ELIN-Q-0027).

Current consumers: `ElinVanillaState`, `ElinBindings`, `ElinActorClasses`, `ElinActionObserver`, `DramaChoiceProjector`, `ElinSituationStager`.
