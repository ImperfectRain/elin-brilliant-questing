# Quests And Events

- `BaseModManager.SubscribeEvent` overloads and `EVENT.ActPerformed`, `CharaCreated`, `DramaParseAction`, `PostLoad`, `PreSave`, `NewGame` exist (`VERIFIED-METADATA`).
- Current runtime log confirms BQ received enough lifecycle events to load, attach, restore bindings, and save a chunk (`VERIFIED-RUNTIME`).
- `Act.Perform(Chara,Card,Point)` publishes `EVENT.ActPerformed` with the exact `Act` payload after a successful perform (`SOURCE-OBSERVED`). Static actor/item surfaces include `Act.CC`, `Act.TC`, `Act.TOOL`, `Act.TP`, `AIAct.owner`, and `AI_TargetCard.target` (`VERIFIED-METADATA`, `SOURCE-OBSERVED`, `STUB-VERIFIED`).
- `ActChat.Perform()` returns false, and representative production/crafting/harvest creation paths do not publish through `ActPerformed`; BQ does not rely on this event for chat or production output (`SOURCE-OBSERVED`, live action samples still `UNRESOLVED`).
- `SourceQuest.csv` is indexed for current SourceExport; no current BQ gameplay code mutates vanilla quest state (`SOURCE-DATA`, `INFERRED` from plugin audit).
