# World And Zones

Design guidance for procedural scenario sites, later settlement generation, and additive spatial
development lives in [`../../design/procedural-places-and-spatial-history.md`](../../design/procedural-places-and-spatial-history.md). This file remains authoritative for what the current Elin build exposes.

- `EClass._zone`, `EClass._map`, `Map.charas`, `Map.things`, `Zone.uid`, `Zone.FindChara(int)`, `Zone.FindChara(string)`, `Zone.AddChara(string, Point)`, `Zone.AddChara(string, int, int)`, `Zone.Simulate()`, `Zone.OnVisit()`, and simulation hooks exist (`VERIFIED-METADATA`).
- Current BQ `GetZoneOf` reads `Chara.currentZone` and mints `zone_<uid>` ids; unresolved non-player entities return `EntityId.None` (`VERIFIED-METADATA`, `STUB-VERIFIED`).
- `GetCharactersInZone` currently scans the loaded `EClass._map.charas`, not arbitrary saved zones (`VERIFIED-METADATA`, `INFERRED` from implementation).
- Zone loose items are exposed for the active loaded map through `EClass._map.things`; current `IVanillaState.GetInventory(zone)` still only resolves `Chara` inventories, so arbitrary zone inventory remains an adapter gap (`VERIFIED-METADATA`, `SOURCE-OBSERVED`, `UNRESOLVED` runtime coverage).

## BQ-Owned Sites (BQ-087)

`ElinSituationStager.StageSite` gives a generated place a body by binding it to the loaded zone and reading its uid through `ElinPresence.IdOf`, returning the same `zone_<uid>` handle everything else is keyed on. It creates no zone. Native site creation - random zone profiles, `addMap` for a predeclared mod zone, `Region.CreateRandomSite(...)`, `.mp` map pieces, and whether a created site's map survives a save - is `UNRESOLVED` and was not written blind (`ELIN-Q-0032`, `PP §7`). With no zone loaded the stager answers with nothing and `SiteGenesis` fails closed, so the failure direction is a place that cannot be made rather than a site in the save the game does not agree exists.

Consequence for return visits: `GetCharactersInZone` still reads the loaded `EClass._map.charas` rather than an arbitrary saved zone (`ELIN-Q-0008`), so `SiteGenesis.Visit` is accurate for the place the player is standing in and reports drift it cannot see for one they are not.

## Grade-B Absence / Movement

Current BQ implementation: `ElinPresence.ResolveMove` searches for `Chara.MoveZone(Zone, ZoneTransition.EnterState)` and `ElinPresence.ResolveFindZone` searches for `EClass.game.spatials.Find(int)`. The adapter refuses movement when the actor is not already global, and Grade-B absence remains configuration-gated pending disposable-save runtime validation (`SOURCE-OBSERVED`, `STUB-VERIFIED`, `UNRESOLVED` runtime).

Actual vanilla mechanism: installed `Chara` exposes `MoveZone(string)`, `MoveZone(Zone, ZoneTransition.EnterState)`, and `MoveZone(Zone, ZoneTransition)` (`VERIFIED-METADATA`). `MoveZone(Zone, EnterState)` constructs a `ZoneTransition` and delegates to `MoveZone(Zone, ZoneTransition)` (`SOURCE-OBSERVED`). `MoveZone(string)` resolves the destination through `EClass.game.spatials.Find(string)` and uses `EnterState.Auto` (`SOURCE-OBSERVED`).

`ZoneTransition.EnterState` values include `Auto`, cardinal edges, `Exact`, `RandomVisit`, `Return`, `Teleport`, `Region`, and other travel states (`VERIFIED-METADATA`). Vanilla callers use `MoveZone(Zone, EnterState)` for meetings, expeditions, quest scenes, party handling, revives, banishment, global goals, waystones, and day-advance transitions (`SOURCE-OBSERVED`).

Important precondition: the non-player offscreen path requires `Chara.global != null`. If a non-PC actor is not global, `MoveZone(Zone, ZoneTransition)` logs and returns before moving (`SOURCE-OBSERVED`). For inactive destinations, vanilla calls `Zone.AddCard(chara)` and leaves transition data on `chara.global.transition`; for active destinations it computes a spawn position, clears `global.transition`, and calls `Zone.AddCard(chara, pos)` (`SOURCE-OBSERVED`).

BQ guidance: do not call `SetGlobal()` as a generic relocation fix. It changes global registration, faction/home state interactions, and save shape. Grade-B absence should first support already-global actors with `MoveZone(Zone, EnterState)` and should refuse ordinary non-global citizens until a disposable-save probe proves a safe promotion or different vanilla travel mechanism. Save/load persistence for a moved ordinary NPC remains runtime-unverified.
