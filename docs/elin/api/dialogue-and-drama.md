# Dialogue And Drama

- Current BQ patches `DramaManager.ParseLine(Dictionary<string,string>)`, `DramaEventTalk.InitDialog()`, and `DialogDrama.SetText(string,bool)` (`VERIFIED-METADATA`).
- The 2026-08-29 runtime log says `Drama choice projector installed` (`VERIFIED-RUNTIME`).
- The generic talk guard checks `DramaManager.setup.book == "_chara"` and `step == "main"` (`VERIFIED-METADATA`; generic semantics are `SOURCE-OBSERVED` from previous installed implementation reading).
- `DramaEventTalk.choices`, `funcText`, `text`, and `AddChoice(DramaChoice)` exist (`VERIFIED-METADATA`).
- `EVENT.DramaParseAction` exists as a constant, but current evidence does not prove it is published for the needed lifecycle point (`VERIFIED-METADATA`, `UNRESOLVED`).
- Choice count/scrolling/clipping with BQ's fixed entries plus up to seven action choices remains untested (`UNRESOLVED`).
- Raw NPC speech remains unresolved. `ElinBark` searches `Chara.SayRaw`/`TalkRaw` and falls back to `Msg.SayRaw`; no installed metadata hit confirmed a Chara raw-text method in the targeted dump (`UNRESOLVED`).
