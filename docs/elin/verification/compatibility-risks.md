# Compatibility Risks

Highest-risk surfaces:

- Drama Harmony patches (`DramaManager.ParseLine`, `DramaEventTalk.InitDialog`, `DialogDrama.SetText`) are version-drift sensitive. They are metadata-verified and installed in the current log, but they remain the highest risk because UI lifecycle behavior is not fully runtime-probed (`VERIFIED-METADATA`, `VERIFIED-RUNTIME`, `UNRESOLVED`).
- Actor classification candidate flags are unresolved. The fallback protects saves by returning `Unknown`, but that can close relocation/absence routes for ordinary citizens (`UNRESOLVED`, `STUB-VERIFIED`).
- Grade-B absence depends on an exact one-argument zone move and spatial lookup that the installed metadata did not confirm. Leave disabled except for disposable-save testing (`UNRESOLVED`).
- Home capacity and resident admission are unresolved on the current installed build. Reads may partially work via `FactionBranch.members/elements`, but writes should be treated as unavailable until probed (`VERIFIED-METADATA`, `UNRESOLVED`).
- `ActPerformed` is available, but action payload field names are reflective and unverified for real theft/craft/combat events (`VERIFIED-METADATA`, `UNRESOLVED`).
- Item quality currently reads from `Thing.rarity`; that was selected in runtime, but whether it means production quality is unresolved (`VERIFIED-RUNTIME`, `UNRESOLVED`).

Risks affecting the next five roadmap steps after current `BQ-036`:

- `BQ-037/BQ-038` guild information networks depend on guild membership/rank; membership is runtime-read, numeric rank is binary/fallback only (`VERIFIED-RUNTIME`, `UNRESOLVED` rank).
- `BQ-039/BQ-040` situation generation needs settlement/world affordances; SourceData is indexed but no runtime affordance adapter exists (`SOURCE-DATA`, `UNRESOLVED`).
- `BQ-041` and early S5 archetypes will lean on existing actors/locations/items; actor classification, zone item inventory, and SourceData semantic mappings are the main risks (`UNRESOLVED`, `SOURCE-DATA`).
