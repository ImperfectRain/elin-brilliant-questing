# Compatibility Risks

Highest-risk surfaces:

- Drama Harmony patches (`DramaManager.ParseLine`, `DramaEventTalk.InitDialog`, `DialogDrama.SetText`) are version-drift sensitive. They are metadata-verified and installed in the current log, but they remain the highest risk because UI lifecycle behavior is not fully runtime-probed (`VERIFIED-METADATA`, `VERIFIED-RUNTIME`, `UNRESOLVED`).
- Actor classification signals are now metadata/source verified, but live population thresholds are not. The fallback protects saves by returning `Unknown`, but may close relocation/absence routes for ordinary citizens (`VERIFIED-METADATA`, `SOURCE-OBSERVED`, `UNRESOLVED` runtime samples).
- Grade-B absence current implementation is incorrect for the installed build: BQ searches for `MoveZone(Zone)`, while vanilla exposes `MoveZone(Zone, EnterState)` and `MoveZone(Zone, ZoneTransition)`. Leave disabled except for disposable-save testing until `FIX-ELIN-001` is implemented and probed (`SOURCE-OBSERVED`, `UNRESOLVED` runtime).
- Home capacity and resident admission current implementation is incorrect for the installed build: vanilla uses `MaxPopulation` and misspelled `AddMemeber(Chara)`. Writes should remain unavailable until `FIX-ELIN-002` is implemented and probed (`SOURCE-OBSERVED`, `UNRESOLVED` runtime).
- `ActPerformed` payload shape is statically clearer, but production/chat assumptions are wrong: `ActChat` returns false and representative production creation paths do not publish `ActPerformed` (`SOURCE-OBSERVED`). BQ-050 needs separate production hooks.
- Item quality currently reads from `Thing.rarity`; static analysis shows production quality should use `Card.GetTotalQuality(true)`/`Card.Quality`, not rarity (`SOURCE-OBSERVED`).

Risks affecting the next five roadmap steps after current `BQ-036`:

- `BQ-037/BQ-038` guild networks and guild authority read `FactionRelation.rank` and `.exp` since `BQ-038` took `FIX-ELIN-007`. Neither has been read on a running member save (`SOURCE-OBSERVED`), and the rank scale the authority threshold is set against is therefore unverified; an unread number is 0 and refuses the route. Which guild a live NPC belongs to is still unread (`ELIN-Q-0025`), so in a real game an officer only speaks for a guild where a situation or generator granted the membership role.
- `BQ-039/BQ-040` situation generation needs settlement/world affordances; SourceData and active-zone signals are mapped, but no runtime affordance adapter exists (`SOURCE-DATA`, `SOURCE-OBSERVED`, `UNRESOLVED` integration).
- `BQ-041` and early S5 archetypes will lean on existing actors/locations/items; actor classification live thresholds, zone item inventory, and SourceData semantic mappings are the main risks (`SOURCE-OBSERVED`, `SOURCE-DATA`, `UNRESOLVED` runtime).
