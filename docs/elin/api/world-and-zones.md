# World And Zones

- `EClass._zone`, `EClass._map`, `Map.charas`, `Map.things`, `Zone.uid`, `Zone.FindChara(int)`, `Zone.FindChara(string)`, `Zone.AddChara(string, Point)`, `Zone.AddChara(string, int, int)`, `Zone.Simulate()`, `Zone.OnVisit()`, and simulation hooks exist (`VERIFIED-METADATA`).
- Current BQ `GetZoneOf` reads `Chara.currentZone` and mints `zone_<uid>` ids; unresolved non-player entities return `EntityId.None` (`VERIFIED-METADATA`, `STUB-VERIFIED`).
- `GetCharactersInZone` currently scans the loaded `EClass._map.charas`, not arbitrary saved zones (`VERIFIED-METADATA`, `INFERRED` from implementation).
- Zone loose items are not exposed through `IVanillaState.GetInventory(zone)` today because live inventory resolution only resolves `Chara`; `Map.things` gives a likely read surface but is not integrated (`VERIFIED-METADATA`, `UNRESOLVED`).
- Grade-B absence is unresolved. The installed `Chara` exposes `MoveZone(String)`, `MoveZone(Zone, EnterState)`, and `MoveZone(Zone, ZoneTransition)`, but current reflective resolver looks for one-argument `MoveZone(Zone)`/`SetZone(Zone)`/`ChangeZone(Zone)` and a spatial uid lookup (`VERIFIED-METADATA`, `UNRESOLVED`).
