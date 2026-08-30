# Journal UI

Current BQ journal and chronicle presentation is log/message based, not native journal UI.

- `LayerJournal` exists with `IdTabLocation`, `IdTabReligion`, and `OnSwitchContent(Window)` (`VERIFIED-METADATA`).
- No installed metadata inspection established a safe tab creation/content injection API (`UNRESOLVED`).
- `DramaChoiceProjector.ShowJournal`, `ShowChronicle`, and case notes use `Msg.SayRaw` plus BepInEx log output (`VERIFIED-METADATA`, `VERIFIED-RUNTIME` for logging route generally).

Probe `JournalShapeProbe` before implementing BQ journal tabs.
