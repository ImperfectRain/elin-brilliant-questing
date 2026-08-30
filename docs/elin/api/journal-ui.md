# Journal UI

Current BQ journal and chronicle presentation is log/message based, not native journal UI.

- `LayerJournal` exists with `IdTabLocation`, `IdTabReligion`, `HeaderIsListOf(int)`, `SwitchPopulation(int)`, and `OnSwitchContent(Window)` (`VERIFIED-METADATA`).
- The tab API is on `Plugins.UI.Window`, not `Elin.dll`'s `LayerJournal`: `Window.setting.tabs`, `AddTab(string idLang, UIContent content, Action action, Sprite sprite, string langTooltip)`, `BuildTabs(int)`, `SwitchContent(int/string/UIContent/Tab)`, and `SetContent(int, UIContent)` (`VERIFIED-METADATA`, `SOURCE-OBSERVED`).
- `Window.AddTab` appends a `Window.Setting.Tab` to `setting.tabs`. `BuildTabs` instantiates tab buttons from `moldTab`, localizes `idLang`, wires click handlers, and initializes the selected index. `SwitchContent(int)` treats tab ids as list indices, toggles content objects, instantiates content if needed, calls `UIContent.OnInstantiate()`, then `UIContent.OnSwitchContent(idTab)` and layer/controller switch hooks (`SOURCE-OBSERVED`).
- Safe extension point: append BQ tabs once before `Window.BuildTabs(int)` runs for a `LayerJournal` window, or patch an earlier journal setup point if one is later identified. Tab ids should be treated as unstable indices; BQ should identify its content by object/name and tolerate failure by falling back to the current log/message journal (`SOURCE-OBSERVED`, `INFERRED`).
- Required runtime probe: verify that a custom or reused `UIContent` can render dynamic list/detail content without clipping or lifecycle leaks. Static analysis establishes the tab mechanism but not final visual layout (`UNRESOLVED` runtime UI).
- `DramaChoiceProjector.ShowJournal`, `ShowChronicle`, and case notes use `Msg.SayRaw` plus BepInEx log output (`VERIFIED-METADATA`, `VERIFIED-RUNTIME` for logging route generally).

`BQ-138` added `JournalShapeProbe`, a read-only `Window.BuildTabs(int)` prefix that logs
`LayerJournal` tab count, ids, disabled state, selected `idTab`, and content component types once
per journal window instance. Native BQ journal tab injection remains disabled until that probe is
run in game and the content lifecycle/clipping question is resolved.
