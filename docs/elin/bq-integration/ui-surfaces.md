# UI Surfaces

- Generic Drama talk is the current interactive surface. It injects BQ choices only for `_chara` / `main` (`VERIFIED-METADATA`, `SOURCE-OBSERVED`).
- BQ journal, chronicle, case notes, and debug report currently write to BepInEx log and notify the player via `Msg.SayRaw` (`VERIFIED-METADATA`, `VERIFIED-RUNTIME` for log route).
- Ambient rumors and town news use `ElinBark`, which should bind inherited `Card.SayRaw(string,string,string)`/`TalkRaw(string,string,string,bool)` before falling back to `Msg.SayRaw` (`VERIFIED-METADATA`, `SOURCE-OBSERVED`; open-Drama visual ordering `UNRESOLVED`).
- Native `LayerJournal` integration is not built. Static Phase 2 identified the real extension surface on `Plugins.UI.Window`: append tabs via `AddTab`, build them through `BuildTabs`, and switch `UIContent` through `SwitchContent`/`SetContent` (`VERIFIED-METADATA`, `SOURCE-OBSERVED`; final content layout `UNRESOLVED`).
