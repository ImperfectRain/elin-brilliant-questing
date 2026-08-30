# Quests And Events

- `BaseModManager.SubscribeEvent` overloads and `EVENT.ActPerformed`, `CharaCreated`, `DramaParseAction`, `PostLoad`, `PreSave`, `NewGame` exist (`VERIFIED-METADATA`).
- Current runtime log confirms BQ received enough lifecycle events to load, attach, restore bindings, and save a chunk (`VERIFIED-RUNTIME`).
- `EVENT.ActPerformed` payload shape for real theft, combat, chat, and production remains unresolved. Current `ElinActionObserver` reflects `Act.CC`, `owner`, `TC`, `TOOL`, and `target` and ignores acts it cannot interpret (`UNRESOLVED`).
- `SourceQuest.csv` is indexed for current SourceExport; no current BQ gameplay code mutates vanilla quest state (`SOURCE-DATA`, `INFERRED` from plugin audit).
