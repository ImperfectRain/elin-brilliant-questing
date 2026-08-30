# Journal UI

Current BQ journal and chronicle presentation has one native journal tab plus log/message
diagnostics.

- `LayerJournal` exists with `IdTabLocation`, `IdTabReligion`, `HeaderIsListOf(int)`, `SwitchPopulation(int)`, and `OnSwitchContent(Window)` (`VERIFIED-METADATA`).
- The tab API is on `Plugins.UI.Window`, not `Elin.dll`'s `LayerJournal`: `Window.setting.tabs`, `AddTab(string idLang, UIContent content, Action action, Sprite sprite, string langTooltip)`, `BuildTabs(int)`, `SwitchContent(int/string/UIContent/Tab)`, and `SetContent(int, UIContent)` (`VERIFIED-METADATA`, `SOURCE-OBSERVED`).
- `Window.AddTab` appends a `Window.Setting.Tab` to `setting.tabs`. `BuildTabs` instantiates tab buttons from `moldTab`, localizes `idLang`, wires click handlers, and initializes the selected index. `SwitchContent(int)` treats tab ids as list indices, toggles content objects, instantiates content if needed, calls `UIContent.OnInstantiate()`, then `UIContent.OnSwitchContent(idTab)` and layer/controller switch hooks (`SOURCE-OBSERVED`).
- Safe extension point: append BQ tabs once before `Window.BuildTabs(int)` runs for a `LayerJournal` window, or patch an earlier journal setup point if one is later identified. Tab ids should be treated as unstable indices; BQ should identify its content by object/name and tolerate failure by falling back to the current log/message journal (`SOURCE-OBSERVED`, `INFERRED`).
- Runtime probe result: `LayerJournal` was reached safely before `BuildTabs`; the live window had
  ten configured slots, including enabled quest/key item/location/faction/religion/codex/gallery/
  hall of fame content and disabled null log/story tabs (`VERIFIED-RUNTIME`).
- `NativeJournalSurface` appends one Brilliant Questing tab before `Window.BuildTabs(int)`. It
  clones an existing enabled journal `UIContent` object for layout fields, renders derived
  BQ-033/BQ-034 state through `UINote` helpers, and disables itself on setup failure so the
  dialogue/log fallback remains available (`SOURCE-OBSERVED`, `INFERRED` content reuse).
- `DramaChoiceProjector.ShowJournal`, `ShowChronicle`, and case notes use `Msg.SayRaw` plus
  BepInEx log output as fallback/debug surfaces (`VERIFIED-METADATA`, `VERIFIED-RUNTIME` for
  logging route generally).

`BQ-138` replaced the read-only `JournalShapeProbe` with bounded native tab injection. Final
scrolling/clipping polish still requires live visual acceptance with enough BQ content to exceed
one page.
