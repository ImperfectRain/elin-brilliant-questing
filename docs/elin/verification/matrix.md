# Verification Matrix

Every current Elin-facing BQ dependency is listed here. `Last checked` is `EA 23.338 Patch 2` unless stated otherwise. `SOURCE-OBSERVED` means installed method bodies or vanilla call sites were inspected; it is not runtime verification.

| ID | BQ member / call site | Elin type/member | Read or mutation | Evidence | Failure / fallback | Consumers | Risk |
|---|---|---|---|---|---|---|---|
| API-001 | `BrilliantQuestingPlugin.Awake` | `BaseModManager.SubscribeEvent<GameIOContext>(EVENT.PostLoad/PreSave)`, `SubscribeEvent(EVENT.NewGame)`, `SubscribeEvent<object>(EVENT.ActPerformed)` | Read/subscribe | `VERIFIED-METADATA`, `VERIFIED-RUNTIME` load/save attach | Plugin waits/detaches | Save/load, observer | Medium |
| API-002 | `Load/Persist` | `GameIOContext.Load<T>`, `Save<T>`, `Compress` | Save read/write | `VERIFIED-METADATA`, `VERIFIED-RUNTIME` saved 2 events | New world on load failure | Persistence | High |
| API-003 | `ElinVanillaState.Now` | `EClass.world.date.GetRaw()` | Read | `VERIFIED-RUNTIME` | `GameTime.Zero` | Thread escalation, actions | Medium |
| API-004 | `BindPlayer` | `EClass.pc.uid` | Read | `VERIFIED-RUNTIME` | No binding until attached | All player-centered reads | High |
| API-005 | `ResolveChara` | `EClass._zone.FindChara(int)`, `EClass.game.cards.Find(int)` | Read | `VERIFIED-METADATA`, `VERIFIED-RUNTIME` loaded PC | Returns null | Most adapter reads/writes | High |
| API-006 | `IsAlive` | `Chara.isDead` | Read | `VERIFIED-METADATA` | False on unresolved | Scene validation, actions | Medium |
| API-007 | `GetAttribute/GetSkill/GetPiety` | `Chara.elements.Value(int)` | Read | `VERIFIED-METADATA`, `VERIFIED-RUNTIME`, `SOURCE-DATA` | Returns 0 | Checks, faith, reports | Medium |
| API-008 | `ElementAliases` | `EClass.sources.elements.rows`, `SourceData.alias` | Read | `SOURCE-DATA`, `VERIFIED-RUNTIME` all 32 aliases | Logs missing aliases | Checks, Home metrics | Medium |
| API-009 | `ChangeAffinity` | `Chara.ModAffinity(Chara,int,bool,bool)`, `Chara._affinity` | Mutation, `Social` | `VERIFIED-METADATA`, `VERIFIED-RUNTIME` zero-delta | Refuses if dead/unbound/out-of-band | Consequences, actions | Medium |
| API-010 | `Karma/ChangeKarma` | `EClass.player.karma`, `Player.ModKarma(int)` | Mutation, `Social` | `VERIFIED-RUNTIME` zero-delta | No-op if unsupported/out-of-band | Consequences | Medium |
| API-011 | `Fame/ChangeFame` | `EClass.player.fame`, `Player.ModFame(int)` | Mutation, `Social` | `VERIFIED-RUNTIME` zero-delta | No-op if unsupported/out-of-band | Consequences | Medium |
| API-012 | `GetInfluence/ChangeInfluence` | `Card.GetCurrency("influence")`, `ModCurrency(int,"influence")` | Mutation, `Social` | `VERIFIED-RUNTIME` zero-delta | Clamps spend | Civic/underworld actions | Medium |
| API-013 | `GetContribution` | `Card.GetCurrency("contribution")` | Read | `VERIFIED-RUNTIME` debug line | Returns 0 | Debug/standing only; player-wide, not per guild | Low |
| API-014 | `IsGuildMember/GetGuildRank` | `Guild.IsMember`, `FactionRelation.rank` | Read | `VERIFIED-RUNTIME` membership, `VERIFIED-METADATA`, `SOURCE-OBSERVED` rank | 0 when unread, which every threshold refuses | Underworld/guild gating, guild authority | Medium |
| API-050 | `GetGuildContribution` | `Guild.IsMember`, `FactionRelation.exp` | Read | `VERIFIED-METADATA`, `SOURCE-OBSERVED`; no runtime read of a member save | 0 when unread; contributes to odds, never to a gate | Guild authority difficulty | Medium |
| API-015 | `GetWorshippedDeity` | `Chara.idFaith` | Read | `VERIFIED-METADATA`, `VERIFIED-RUNTIME`, `SOURCE-DATA` | Empty string | Faith actions | Medium |
| API-016 | `GetMoney/TrySpendMoney` | `Card.GetCurrency("money")`, `ModCurrency` | Mutation, `Inventory` | `VERIFIED-RUNTIME` zero-delta | Refuses unresolved/insufficient funds | Bribery, payments | Medium |
| API-017 | `GetInventory` | `Chara.things`, `ThingContainer` enumeration | Read | `VERIFIED-RUNTIME` PC inventory count 33 | Empty list | Crime, evidence, production | High |
| API-018 | `GetInventory` descriptor | `Thing.Name`, `Thing.category.id`, `Thing.GetPrice(...)`, `Card.uid` | Read | `VERIFIED-METADATA`, `VERIFIED-RUNTIME`, `SOURCE-DATA` | Skips unreadable item | Item matching, evidence | High |
| API-019 | `QualityOf` | Current `Thing.rarity`; recommended `Card.GetTotalQuality(true)`/`Card.Quality` | Read | `VERIFIED-RUNTIME`, `SOURCE-OBSERVED` | Current quality semantics wrong for production | Production demands | High |
| API-020 | `TryTransferItem` | `Chara.Pick(Thing,bool,bool)`, `Thing.TryStackTo`, `Card.AddThing` | Mutation, `Inventory` | `VERIFIED-METADATA`, `SOURCE-OBSERVED`, `UNRESOLVED` nonzero runtime | False on unresolved/not arrived; original uid may disappear on stack | Return/steal/plant/fence | High |
| API-021 | `TryDestroyItem` | `Card.Destroy()` / `Thing.Destroy()` / `Card.RemoveThing` | Mutation, `Inventory` | `VERIFIED-METADATA`, `SOURCE-OBSERVED`, `UNRESOLVED` nonzero runtime | False if still present | Burn/destroy evidence, repair failure | High |
| API-022 | `GetHomeState` | `EClass.Branch` | Read | `VERIFIED-METADATA`; current save no readable Home | Null | Home actions | High |
| API-023 | `ReadResidents` | `FactionBranch.members`, `CountMembers(FactionMemberType,bool)` | Read | `VERIFIED-METADATA`, `SOURCE-OBSERVED` | Residents absent | Home routes/generation | High |
| API-024 | `ReadMetrics` | `FactionBranch.elements.Value(int)` | Read | `VERIFIED-METADATA`, `SOURCE-DATA` | Metrics absent | Home modifiers | High |
| API-025 | `Capacity` | `FactionBranch.MaxPopulation = 5 + Evalue(2204 fFood)` | Read | `VERIFIED-METADATA`, `SOURCE-OBSERVED`, `SOURCE-DATA` | Current BQ misses member; reports unknown | Shelter routes | High |
| API-026 | `TryAdmitResident` | `FactionBranch.AddMemeber(Chara)`; not `AddMember/AddResident/AddChara` | Mutation, `Relocate` | `VERIFIED-METADATA`, `SOURCE-OBSERVED`, `UNRESOLVED` nonzero runtime | Current BQ marks unsupported | Shelter/recruit specialist | High |
| API-027 | `GetZoneOf` | `Chara.currentZone`, `Zone.uid`, `EClass._zone` fallback | Read | `VERIFIED-METADATA`, partial `VERIFIED-RUNTIME` PC zone | `EntityId.None` | Absence, follow, context | High |
| API-028 | `GetCharactersInZone` | `EClass._map.charas` | Read | `VERIFIED-METADATA`, `VERIFIED-RUNTIME` current map scan | Empty list | Witnesses, dialogue context | High |
| API-029 | `TrySendAway/TryBringBack` | `Chara.MoveZone(Zone, EnterState)`, `MoveZone(Zone, ZoneTransition)`, `global.transition` | Mutation, `TemporaryAbsence` | `VERIFIED-METADATA`, `SOURCE-OBSERVED`, `UNRESOLVED` ordinary actor/save runtime | Current BQ resolver definitely fails | Grade-B absence | Very high |
| API-030 | `ElinActorClasses.Classify` | `Card.IsUnique`, `IsImportant`, `c_uniqueData`, `c_isImportant`, trait/source/quest flags | Read | `VERIFIED-METADATA`, `SOURCE-OBSERVED`, `STUB-VERIFIED`, `UNRESOLVED` live samples | `Unknown` closes risky mutations | Mutation policy | Very high |
| API-031 | `ElinSituationStager.StageCharacter` | `Zone.AddChara`, `Zone.AddCard`, `Zone.GetSpawnPos`, `Chara.c_altName`, `Chara.ModAffinity` | Generated mutation | `VERIFIED-METADATA`, prior `VERIFIED-RUNTIME` staged scenario | Logs/refuses | Prototype scenario only | Medium |
| API-032 | `ElinSituationStager.StageItem` | `ThingGen.Create`, holder `Pick`, zone `AddCard` | Generated mutation | `VERIFIED-RUNTIME` repair spawned loose item, `SOURCE-OBSERVED` item APIs | Logs/refuses | Prototype repair/staging | High |
| API-033 | `ElinActionObserver` subscribe | `EVENT.ActPerformed` published by `Act.Perform` with exact `Act` payload | Observe | `VERIFIED-METADATA`, `SOURCE-OBSERVED`, `UNRESOLVED` live action samples | Ignores unknown act | BQ-014/BQ-050 provenance | High |
| API-034 | `ActorOf` | Static `Act.CC`, instance `AIAct.owner` | Reflective read | `VERIFIED-METADATA`, `SOURCE-OBSERVED` | Ignores act | Observed actions | High |
| API-035 | `ItemOf` | Static `Act.TC`, `Act.TOOL`, instance `AI_TargetCard.target`; production needs separate hooks | Reflective read | `VERIFIED-METADATA`, `SOURCE-OBSERVED`, `UNRESOLVED` production runtime | Logs no product | Production/theft observation | High |
| API-036 | `WitnessesOf` | `Map.charas`, `Card.Dist`, `Card.GetSightRadius`, `Chara.CanSeeLos`, perception/stealth | Read | `VERIFIED-METADATA`, `SOURCE-OBSERVED`, `UNRESOLVED` live witness policy | Empty/filtered witnesses | Crime witness model | High |
| API-037 | `ProceduralCheckRows` | `EClass.sources.checks`, `SourceCheck.Row`, `Check.Get` | Source mutation/read | `VERIFIED-RUNTIME` 38 rows | Logs skipped rows | Difficulty text | Medium |
| API-038 | `DescribeDifficulty` | `Check.GetText(Chara,Card,bool)` | Read/presentation | `VERIFIED-METADATA`, `SOURCE-OBSERVED`; in-dialog text not isolated | Falls back to action label | Drama choices | Medium |
| API-039 | `Resolve` native path | `Check.Perform`; `Dice.Roll(1,20,0,null)` | Check resolution | `VERIFIED-METADATA`, `SOURCE-OBSERVED` | Portable resolver | Future native checks | Medium |
| API-040 | `DramaChoiceProjector.Install` | `DramaManager.ParseLine(Dictionary<string,string>)` | Harmony patch | `VERIFIED-METADATA`, `VERIFIED-RUNTIME` projector installed | Unpatches self if missing | Dialogue projection | Very high |
| API-041 | `DramaChoiceProjector.Install` | `DramaEventTalk.InitDialog()` | Harmony patch | `VERIFIED-METADATA`, `VERIFIED-RUNTIME` projector installed | Same | Dialogue projection | Very high |
| API-042 | `DramaChoiceProjector.Install` | `DialogDrama.SetText(string,bool)` | Harmony patch | `VERIFIED-METADATA`, `VERIFIED-RUNTIME` projector installed | Same | Situation text | Very high |
| API-043 | `IsDefaultTalk` | `DramaManager.setup.book/step == "_chara"/"main"` | Read | `VERIFIED-METADATA`, `SOURCE-OBSERVED` | No injection outside match | Dialogue safety | High |
| API-044 | `ProjectChoices` | `DramaEventTalk.choices/AddChoice`, `DramaChoice.SetOnClick` | Presentation mutation | `VERIFIED-METADATA`, `SOURCE-OBSERVED`, `UNRESOLVED` visual count | AlreadyProjected guard, action `MaxChoices` | Dialogue actions/news | High |
| API-045 | `ShowJournal/Chronicle/CaseNotes/News` | `Msg.SayRaw`, `DramaManager.sequence.Exit()` | Presentation | `VERIFIED-METADATA`, `UNRESOLVED` open-Drama visibility | Log output remains | Journal/news delivery | Medium |
| API-046 | `ElinBark.Speak` | `Card.SayRaw`, `Card.TalkRaw`, fallback `Msg.SayRaw` | Presentation | `VERIFIED-METADATA`, `SOURCE-OBSERVED`, `UNRESOLVED` live UI | Fallback message log | Ambient rumors/town news | Medium |
| API-047 | Future journal native UI | `LayerJournal` plus `Plugins.UI.Window.AddTab/BuildTabs/SwitchContent/SetContent` | Presentation | `VERIFIED-METADATA`, `SOURCE-OBSERVED`, `UNRESOLVED` live UI | Current log-only journal | BQ-033+ UI | Medium |
| API-048 | Future actor activity | `idTimeTable`, timetable goal methods, `GlobalData.goal/transition`, `TraitChara.UseGlobalGoal`, `GameDate.AdvanceHour` | Read | `VERIFIED-METADATA`, `SOURCE-OBSERVED`, `UNRESOLVED` live samples | No adapter yet | BQ-135/BQ-093+ | High |
| API-049 | Zone catch-up | `Zone.OnVisit`, `Zone.Simulate`, `FactionBranch.OnAfterSimulate` | Observe/read | `VERIFIED-METADATA`, `SOURCE-OBSERVED`, `UNRESOLVED` live deltas | No integration yet | BQ-107/Home | High |

No `VERIFIED-RUNTIME` tag in this matrix means no live game action proved the behavior, even if the symbol and implementation exist.
