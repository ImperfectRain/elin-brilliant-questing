# Mutation Map

All writes pass through `VanillaStateBase` policy gates unless marked as withdrawal.

| BQ write | Elin API | Policy class | Evidence | Fallback |
|---|---|---|---|---|
| `ChangeAffinity` | `Chara.ModAffinity(EClass.pc, delta, true, false)` | `Social` | `VERIFIED-RUNTIME` zero-delta | No-op/refuse |
| `ChangeKarma` | `Player.ModKarma(delta)` | `Social` | `VERIFIED-RUNTIME` zero-delta | No-op |
| `ChangeFame` | `Player.ModFame(delta)` | `Social` | `VERIFIED-RUNTIME` zero-delta | No-op |
| `ChangeInfluence` | `Card.ModCurrency(delta,"influence")` | `Social` | `VERIFIED-RUNTIME` zero-delta | Clamp/no-op |
| `TrySpendMoney` | `ModCurrency(-amount,"money")` and optional payee credit | `Inventory` | `VERIFIED-RUNTIME` zero-delta | False |
| `TryTransferItem` | `destination.Pick(thing,false,true)` | `Inventory` | `VERIFIED-METADATA`, `UNRESOLVED` behavior | False |
| `TryDestroyItem` | `thing.Destroy()` | `Inventory` | `VERIFIED-METADATA`, `UNRESOLVED` behavior | False |
| `TryAdmitResident` | unresolved branch method | `Relocate` | `UNRESOLVED` | False/capability unsupported |
| `TrySendAway` | unresolved zone move | `TemporaryAbsence` | `UNRESOLVED` | False/capability unsupported |
| `TryBringBack` | same unresolved zone move | `VanillaWithdrawal` | `UNRESOLVED` | False but not policy-gated |
| Prototype staging | `Zone.AddChara`, `Zone.AddCard`, `Chara.Pick` | Config-gated staging, generated actors | prior `VERIFIED-RUNTIME`, current metadata | Logs/refuses |

No current BQ code writes vanilla Home metrics, resident jobs, actor timetable/global goals, or vanilla quest state (`INFERRED` from plugin audit).
