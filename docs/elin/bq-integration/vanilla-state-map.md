# Vanilla State Map

`IVanillaState` is the only core seam to Elin. Current member mapping:

| `IVanillaState` member | Elin dependency | Evidence | Notes |
|---|---|---|---|
| `Now` | `EClass.world.date.GetRaw()` | `VERIFIED-RUNTIME` | Zero fallback if unreadable |
| `PlayerId` | bound from `EClass.pc.uid` | `VERIFIED-RUNTIME` | Stable BQ id for player |
| `Supports` | BQ capability probes | `VERIFIED-RUNTIME` | 13/14 current log |
| `GetActorClass` | `ElinActorClasses` candidate flags/tags | `UNRESOLVED`, `STUB-VERIFIED` | Safe fallback `Unknown` |
| `IsAlive` | `Chara.isDead` | `VERIFIED-METADATA` | False if unresolved |
| `GetAttribute/GetSkill` | `Chara.elements.Value(int)` | `VERIFIED-RUNTIME` | Alias table resolved |
| `GetLevel` | `Chara.LV` | `VERIFIED-METADATA` | Default 1 |
| `GetAffinity/ChangeAffinity` | `Chara._affinity`, `ModAffinity` | `VERIFIED-RUNTIME` zero-delta | Social mutation |
| `Karma/ChangeKarma` | `EClass.player.karma`, `ModKarma` | `VERIFIED-RUNTIME` zero-delta | Social mutation |
| `Fame/ChangeFame` | `EClass.player.fame`, `ModFame` | `VERIFIED-RUNTIME` zero-delta | Social mutation |
| `GetInfluence/ChangeInfluence` | `Card.GetCurrency/ModCurrency("influence")` | `VERIFIED-RUNTIME` | Social mutation |
| `IsGuildMember/GetGuildRank` | `EClass.game.factions.*.IsMember` | `VERIFIED-RUNTIME`, `UNRESOLVED` rank | Rank is binary fallback |
| `GetWorshippedDeity/GetPiety` | `Chara.idFaith`, element `85` | `VERIFIED-RUNTIME`, `SOURCE-DATA` | PC faith `harvest` |
| `GetMoney/TrySpendMoney` | `money` currency | `VERIFIED-RUNTIME` | Inventory mutation |
| `GetInventory` | `Chara.things` | `VERIFIED-RUNTIME` | Character inventories only |
| `TryTransferItem` | `Chara.Pick` + re-read | `VERIFIED-METADATA`, `UNRESOLVED` behavior | Inventory mutation |
| `TryDestroyItem` | `Thing.Destroy`/`Card.Destroy` + re-read | `VERIFIED-METADATA`, `UNRESOLVED` behavior | Inventory mutation |
| `GetHomeState` | `EClass.Branch`, `FactionBranch.members/elements` | `VERIFIED-METADATA`, `UNRESOLVED` save shape | Null when no answer |
| `TryAdmitResident` | candidate branch admission method | `UNRESOLVED` | Relocation mutation |
| `TrySendAway/TryBringBack` | zone move/spatial lookup | `UNRESOLVED` | Temporary absence/withdrawal |
| `GetZoneOf` | `Chara.currentZone`, `Zone.uid` | `VERIFIED-METADATA`, partial `VERIFIED-RUNTIME` | Unknown returns `None` |
| `GetCharactersInZone` | `EClass._map.charas` | `VERIFIED-RUNTIME` current map | Loaded zone only |
