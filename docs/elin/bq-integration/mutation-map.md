# Mutation Map

All writes pass through `VanillaStateBase` policy gates unless marked as withdrawal.

| BQ write | Elin API | Policy class | Evidence | Fallback |
|---|---|---|---|---|
| `ChangeAffinity` | `Chara.ModAffinity(EClass.pc, delta, true, false)` | `Social` | `VERIFIED-RUNTIME` zero-delta | No-op/refuse |
| `ChangeKarma` | `Player.ModKarma(delta)` | `Social` | `VERIFIED-RUNTIME` zero-delta | No-op |
| `ChangeFame` | `Player.ModFame(delta)` | `Social` | `VERIFIED-RUNTIME` zero-delta | No-op |
| `ChangeInfluence` | `Card.ModCurrency(delta,"influence")` | `Social` | `VERIFIED-RUNTIME` zero-delta | Clamp/no-op |
| `TrySpendMoney` | `ModCurrency(-amount,"money")` and optional payee credit | `Inventory` | `VERIFIED-RUNTIME` zero-delta | False |
| `TryTransferItem` | `destination.Pick(thing,false,true)`; may merge stacks and return a different `Thing` | `Inventory` | `VERIFIED-METADATA`, `SOURCE-OBSERVED`, `UNRESOLVED` nonzero runtime | False; current uid verification can be wrong after stack merge |
| `TryDestroyItem` | `thing.Destroy()` / `Card.Destroy()` | `Inventory` | `VERIFIED-METADATA`, `SOURCE-OBSERVED`, `UNRESOLVED` nonzero runtime | False |
| `TryAdmitResident` | vanilla `FactionBranch.AddMemeber(Chara)`; current BQ resolver misses it | `Relocate` | `VERIFIED-METADATA`, `SOURCE-OBSERVED`, `UNRESOLVED` nonzero runtime | False/capability unsupported |
| `TrySendAway` | vanilla `Chara.MoveZone(Zone, EnterState)`; current BQ resolver misses it | `TemporaryAbsence` | `VERIFIED-METADATA`, `SOURCE-OBSERVED`, `UNRESOLVED` ordinary actor/save runtime | False/capability unsupported |
| `TryBringBack` | same `MoveZone` overload; withdrawal is not policy-gated | `VanillaWithdrawal` | `VERIFIED-METADATA`, `SOURCE-OBSERVED`, `UNRESOLVED` runtime | False but not policy-gated |
| Prototype staging | `Zone.AddChara`, `Zone.AddCard`, `Chara.Pick` | Config-gated staging, generated actors | prior `VERIFIED-RUNTIME`, current metadata | Logs/refuses |

No current BQ code writes vanilla Home metrics directly, resident jobs directly, actor timetable/global goals, or vanilla quest state (`INFERRED` from plugin audit). `AddMemeber` would indirectly refresh Home efficiency and work elements if enabled (`SOURCE-OBSERVED`).
