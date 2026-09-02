# Vanilla State Map

`IVanillaState` is the only core seam to Elin. Current member mapping:

| `IVanillaState` member | Elin dependency | Evidence | Notes |
|---|---|---|---|
| `Now` | `EClass.world.date.GetRaw()` | `VERIFIED-RUNTIME` | Zero fallback if unreadable |
| `PlayerId` | bound from `EClass.pc.uid` | `VERIFIED-RUNTIME` | Stable BQ id for player |
| `Supports` | BQ capability probes | `VERIFIED-RUNTIME` | 13/14 in the last live log, before `ReadCharacterIdentity` was added |
| `GetActorClass` | `IsPC`, `IsPCParty`, `Card.IsUnique`, `IsImportant`, `c_uniqueData`, `c_isImportant`, trait/quest/Home/branch flags | `VERIFIED-METADATA`, `SOURCE-OBSERVED`, `STUB-VERIFIED`, `UNRESOLVED` live thresholds | Safe fallback `Unknown` |
| `GetCharacterIdentity` | `Chara.source` (`id`/`aka`, `job`/`idJob`, `hobbies`), `Chara.idRace` and the race row, service trait subclasses, `TraitGuard`/`TraitGuildPersonnel`/`TraitGuildDoorman`, `Chara.faction` | `VERIFIED-METADATA` traits, `SOURCE-DATA` source columns, `UNRESOLVED` runtime | BQ-144. Six separately typed facets carrying Elin's own ids; each facet fails on its own and an unread one is `Unknown`, never `""`/`"local"`/a default job. Never persisted; not an input to mutation policy |
| `GetLifeState` / `IsAlive` | `Chara.isDead` after `ResolveChara` succeeds | `VERIFIED-METADATA` | `Unknown` if unresolved; lifecycle only treats `Dead` as death evidence |
| `GetAttribute/GetSkill` | `Chara.elements.Value(int)` | `VERIFIED-RUNTIME` | Alias table resolved |
| `GetLevel` | `Chara.LV` | `VERIFIED-METADATA` | Default 1 |
| `GetAffinity/ChangeAffinity` | `Chara._affinity`, `ModAffinity` | `VERIFIED-RUNTIME` zero-delta | Social mutation |
| `Karma/ChangeKarma` | `EClass.player.karma`, `ModKarma` | `VERIFIED-RUNTIME` zero-delta | Social mutation |
| `Fame/ChangeFame` | `EClass.player.fame`, `ModFame` | `VERIFIED-RUNTIME` zero-delta | Social mutation |
| `GetInfluence/ChangeInfluence` | `Card.GetCurrency/ModCurrency("influence")` | `VERIFIED-RUNTIME` | Social mutation |
| `IsGuildMember/GetGuildRank/GetGuildContribution` | `Guild.IsMember`, `FactionRelation.rank`, `FactionRelation.exp` | `VERIFIED-RUNTIME` membership, `SOURCE-OBSERVED` rank/exp, `STUB-VERIFIED` rank shape | Reads the real numbers since `BQ-038`; both 0 when unread, and 0 refuses |
| `GetWorshippedDeity/GetPiety` | `Chara.idFaith`, element `85` | `VERIFIED-RUNTIME`, `SOURCE-DATA` | PC faith `harvest` |
| `GetMoney/TrySpendMoney` | `money` currency | `VERIFIED-RUNTIME` | Inventory mutation |
| `GetInventory` | `Chara.things`; active zone loose items via `EClass._map.things` not yet integrated | `VERIFIED-RUNTIME`, `SOURCE-OBSERVED` | Character inventories only today |
| `TryTransferItem` | `Chara.Pick` + returned `Thing` binding update + re-read; returned uid may differ after stack merge | `VERIFIED-METADATA`, `SOURCE-OBSERVED`, `STUB-VERIFIED`, `UNRESOLVED` runtime | Inventory mutation |
| `TryDestroyItem` | `Thing.Destroy`/`Card.Destroy` + re-read | `VERIFIED-METADATA`, `SOURCE-OBSERVED`, `UNRESOLVED` runtime | Inventory mutation |
| `GetHomeState` | `EClass.Branch`, `FactionBranch.members/elements/owner/MaxPopulation` | `VERIFIED-METADATA`, `SOURCE-OBSERVED`, `SOURCE-DATA` | Null when no answer; `fFood` is verified as `MaxPopulation` input, not a hunger threshold |
| `GetPlayerCompanions` | `EClass.pc.party` and its member list (`members`/`Members`/`charas`) | `VERIFIED-METADATA` (`Chara.IsPCParty`), `UNRESOLVED` party member list | BQ-123. Live read, never persisted. Gated on `ReadPlayerCompanions`: a build that names no member list reports the capability unsupported rather than an empty party, so `PlayerHousehold.CompanionsRead` distinguishes a silence from a player who travels alone |
| `TryAdmitResident` | vanilla `FactionBranch.AddMemeber(Chara)` with resident readback | `VERIFIED-METADATA`, `SOURCE-OBSERVED`, `STUB-VERIFIED`, `UNRESOLVED` runtime | Relocation mutation |
| `TrySendAway/TryBringBack` | `Chara.MoveZone(Zone, EnterState)` and `SpatialManager.Find(int)`; requires existing global record | `VERIFIED-METADATA`, `SOURCE-OBSERVED`, `STUB-VERIFIED`, `UNRESOLVED` runtime | Temporary absence/withdrawal |
| `GetZoneOf` | `Chara.currentZone`, `Zone.uid` | `VERIFIED-METADATA`, partial `VERIFIED-RUNTIME` | Unknown returns `None` |
| `GetCharactersInZone` | `EClass._map.charas` | `VERIFIED-RUNTIME` current map | Loaded zone only |
