# Vanilla State Map

`IVanillaState` is the only core seam to Elin. Current member mapping:

| `IVanillaState` member | Elin dependency | Evidence | Notes |
|---|---|---|---|
| `Now` | `EClass.world.date.GetRaw()` | `VERIFIED-RUNTIME` | Zero fallback if unreadable |
| `PlayerId` | bound from `EClass.pc.uid` | `VERIFIED-RUNTIME` | Stable BQ id for player |
| `Supports` | BQ capability probes | `VERIFIED-RUNTIME` | 13/14 current log |
| `GetActorClass` | `Card.IsUnique`, `IsImportant`, `c_uniqueData`, `c_isImportant`, traits/source/quest flags | `VERIFIED-METADATA`, `SOURCE-OBSERVED`, `STUB-VERIFIED`, `UNRESOLVED` live thresholds | Safe fallback `Unknown` |
| `IsAlive` | `Chara.isDead` | `VERIFIED-METADATA` | False if unresolved |
| `GetAttribute/GetSkill` | `Chara.elements.Value(int)` | `VERIFIED-RUNTIME` | Alias table resolved |
| `GetLevel` | `Chara.LV` | `VERIFIED-METADATA` | Default 1 |
| `GetAffinity/ChangeAffinity` | `Chara._affinity`, `ModAffinity` | `VERIFIED-RUNTIME` zero-delta | Social mutation |
| `Karma/ChangeKarma` | `EClass.player.karma`, `ModKarma` | `VERIFIED-RUNTIME` zero-delta | Social mutation |
| `Fame/ChangeFame` | `EClass.player.fame`, `ModFame` | `VERIFIED-RUNTIME` zero-delta | Social mutation |
| `GetInfluence/ChangeInfluence` | `Card.GetCurrency/ModCurrency("influence")` | `VERIFIED-RUNTIME` | Social mutation |
| `IsGuildMember/GetGuildRank/GetGuildContribution` | `Guild.IsMember`, `FactionRelation.rank`, `FactionRelation.exp` | `VERIFIED-RUNTIME` membership, `SOURCE-OBSERVED` rank/exp | Reads the real numbers since `BQ-038`; both 0 when unread, and 0 refuses |
| `GetWorshippedDeity/GetPiety` | `Chara.idFaith`, element `85` | `VERIFIED-RUNTIME`, `SOURCE-DATA` | PC faith `harvest` |
| `GetMoney/TrySpendMoney` | `money` currency | `VERIFIED-RUNTIME` | Inventory mutation |
| `GetInventory` | `Chara.things`; active zone loose items via `EClass._map.things` not yet integrated | `VERIFIED-RUNTIME`, `SOURCE-OBSERVED` | Character inventories only today |
| `TryTransferItem` | `Chara.Pick` + re-read; returned uid may differ after stack merge | `VERIFIED-METADATA`, `SOURCE-OBSERVED`, `UNRESOLVED` runtime | Inventory mutation |
| `TryDestroyItem` | `Thing.Destroy`/`Card.Destroy` + re-read | `VERIFIED-METADATA`, `SOURCE-OBSERVED`, `UNRESOLVED` runtime | Inventory mutation |
| `GetHomeState` | `EClass.Branch`, `FactionBranch.members/elements/owner/MaxPopulation` | `VERIFIED-METADATA`, `SOURCE-OBSERVED`, `SOURCE-DATA` | Null when no answer |
| `TryAdmitResident` | vanilla `FactionBranch.AddMemeber(Chara)`; current resolver misses it | `VERIFIED-METADATA`, `SOURCE-OBSERVED`, `UNRESOLVED` runtime | Relocation mutation |
| `TrySendAway/TryBringBack` | `Chara.MoveZone(Zone, EnterState)` and `SpatialManager.Find(int)`; current resolver misses move overload | `VERIFIED-METADATA`, `SOURCE-OBSERVED`, `UNRESOLVED` runtime | Temporary absence/withdrawal |
| `GetZoneOf` | `Chara.currentZone`, `Zone.uid` | `VERIFIED-METADATA`, partial `VERIFIED-RUNTIME` | Unknown returns `None` |
| `GetCharactersInZone` | `EClass._map.charas` | `VERIFIED-RUNTIME` current map | Loaded zone only |
