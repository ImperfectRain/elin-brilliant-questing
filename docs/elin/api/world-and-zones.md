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

## Procedural Scenario Dungeons (BQ-140)

A BQ-owned site can now carry a *physical structure*: authored pieces placed on a bounded grid, joined by connectors derived from the plan's own affordances (`BrilliantQuesting.World.SiteStructure`). It reaches the adapter on `SiteBlueprint.Structure`.

Nothing on this build can apply one. `VanillaCapability.BuildPlaceStructure` names the write - create a zone this mod owns and apply authored map pieces into it - and `ElinVanillaState` reports it `unsupported` with `ELIN-Q-0032` as the reason: `Region.CreateRandomSite(...)`, `addMap` for a predeclared mod zone, `GenBounds.TryAddMapPiece`, `PartialMap.Apply(...)` and visited-zone map persistence are all unexercised. Consequently:

- `SiteRealization.Realize` refuses to plan a structure on a build that does not advertise the capability, before any piece is chosen;
- `ElinSituationStager.StageSite` refuses any blueprint that carries a structure and returns the empty string, on which `SiteGenesis` already fails closed;
- so a scenario dungeon on the live build is a place that cannot be made, never a place made out of pieces the game did not apply.

Everything BQ-140 claims is therefore headless. Turning it on means exercising the four calls above on the exact installed build, recording them here and in [`../verification/unresolved.md`](../verification/unresolved.md), and then advertising the capability from a probe rather than from intent.

## Additive Change To A Place That Already Exists (BQ-143)

A place that already exists can be given one more authored piece: `SiteMutation.Apply` puts one
`SitePiece` on ground beside a part the place already has, joined to it by an opening, and the site
records it in `NarrativeSite.Additions`. That record is the only physical thing about a site written
into a save, because it is the only physical thing that cannot be derived from the grammar and the
seed - it happened after both.

Nothing on this build can apply one, and this is a harder refusal than BQ-140's rather than the same
one. `VanillaCapability.AddPlaceFixture` names both halves of the write: adding a piece into a map
Elin has already generated and saved, and reading whether a patch of that map's ground is free
first. Neither has been exercised (`ELIN-Q-0033`), and the read half is the same gap BQ-090 waits on
(`ELIN-Q-0008`). Consequently:

- `ElinVanillaState` reports the capability `unsupported` and answers `InspectGround` with
  `VanillaGround.Unknown` on every call - it has no read that could say otherwise, and unknown
  ground is refused exactly like occupied ground (`D017`);
- `SiteMutation` refuses on the capability before it chooses any ground, so nothing is looked at;
- `ElinSituationStager.ApplySiteAddition` refuses every blueprint and returns the empty string, on
  which `SiteMutation` fails closed and writes no record;
- so on the live build a BQ-owned place cannot be added to at all, never added to on top of terrain
  the player has dug, built on or left things standing in.

Everything BQ-143 claims is therefore headless (`SiteMutationTests`,
`dotnet run --project tools/BrilliantQuesting.Lab -- run site-addition`). Turning it on means
exercising, on the exact installed build: a write into an already-generated zone's map, a read of
what stands on a given tile of it (`EClass._map.things` at minimum), and whether both survive
save/quit/reload and elapsed in-game days - then recording that here and in
[`../verification/unresolved.md`](../verification/unresolved.md) before the capability is advertised.
The additional live questions BQ-143's done-when asks and nothing here answers are NPC pathing and
service behaviour around the new piece, and what a save containing an addition does when BQ is
disabled.

## Grade-B Absence / Movement

Current BQ implementation: `ElinPresence.ResolveMove` searches for `Chara.MoveZone(Zone, ZoneTransition.EnterState)` and `ElinPresence.ResolveFindZone` searches for `EClass.game.spatials.Find(int)`. The adapter refuses movement when the actor is not already global, and Grade-B absence remains configuration-gated pending disposable-save runtime validation (`SOURCE-OBSERVED`, `STUB-VERIFIED`, `UNRESOLVED` runtime).

Actual vanilla mechanism: installed `Chara` exposes `MoveZone(string)`, `MoveZone(Zone, ZoneTransition.EnterState)`, and `MoveZone(Zone, ZoneTransition)` (`VERIFIED-METADATA`). `MoveZone(Zone, EnterState)` constructs a `ZoneTransition` and delegates to `MoveZone(Zone, ZoneTransition)` (`SOURCE-OBSERVED`). `MoveZone(string)` resolves the destination through `EClass.game.spatials.Find(string)` and uses `EnterState.Auto` (`SOURCE-OBSERVED`).

`ZoneTransition.EnterState` values include `Auto`, cardinal edges, `Exact`, `RandomVisit`, `Return`, `Teleport`, `Region`, and other travel states (`VERIFIED-METADATA`). Vanilla callers use `MoveZone(Zone, EnterState)` for meetings, expeditions, quest scenes, party handling, revives, banishment, global goals, waystones, and day-advance transitions (`SOURCE-OBSERVED`).

Important precondition: the non-player offscreen path requires `Chara.global != null`. If a non-PC actor is not global, `MoveZone(Zone, ZoneTransition)` logs and returns before moving (`SOURCE-OBSERVED`). For inactive destinations, vanilla calls `Zone.AddCard(chara)` and leaves transition data on `chara.global.transition`; for active destinations it computes a spawn position, clears `global.transition`, and calls `Zone.AddCard(chara, pos)` (`SOURCE-OBSERVED`).

BQ guidance: do not call `SetGlobal()` as a generic relocation fix. It changes global registration, faction/home state interactions, and save shape. Grade-B absence should first support already-global actors with `MoveZone(Zone, EnterState)` and should refuse ordinary non-global citizens until a disposable-save probe proves a safe promotion or different vanilla travel mechanism. Save/load persistence for a moved ordinary NPC remains runtime-unverified.
