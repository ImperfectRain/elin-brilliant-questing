# UI Surfaces

- Generic Drama talk is the current interactive surface. It injects BQ choices only for `_chara` / `main` (`VERIFIED-METADATA`, `SOURCE-OBSERVED`).
- BQ journal and chronicle project into one native Brilliant Questing journal tab when the
  `Window.BuildTabs(int)` patch is available. Case notes, debug report, and failed native journal
  setup still write to BepInEx log and notify the player via `Msg.SayRaw` as fallback/debug
  surfaces (`VERIFIED-METADATA`, `VERIFIED-RUNTIME` for log route).
- Ambient rumors and town news use `ElinBark`, which should bind inherited `Card.SayRaw(string,string,string)`/`TalkRaw(string,string,string,bool)` before falling back to `Msg.SayRaw` (`VERIFIED-METADATA`, `SOURCE-OBSERVED`; open-Drama visual ordering `UNRESOLVED`).
- Native `LayerJournal` integration uses the real extension surface on `Plugins.UI.Window`: append
  one BQ tab via `AddTab` before `BuildTabs`, then render through a cloned existing `UIContent`
  layout and `UINote` helpers (`VERIFIED-METADATA`, `SOURCE-OBSERVED`, `VERIFIED-RUNTIME` probe;
  final clipping/scrolling acceptance still needs live visual confirmation).
