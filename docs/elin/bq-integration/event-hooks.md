# Event Hooks

- `PostLoad`: BQ loads save chunk, restores bindings, installs procedural check rows, detects capabilities, and attaches simulation (`VERIFIED-RUNTIME`).
- `PreSave`: BQ writes bindings and save chunk (`VERIFIED-RUNTIME`).
- `NewGame`: BQ reset path is subscribed; current runtime proof is limited to subscription metadata (`VERIFIED-METADATA`).
- `ActPerformed`: BQ subscribes and `ElinActionObserver` inspects action payloads reflectively. Installed `Act.Perform` publishes the exact `Act` payload only after successful perform (`VERIFIED-METADATA`, `SOURCE-OBSERVED`). `Act.CC`, `Act.TC`, `Act.TOOL`, `Act.TP`, `AIAct.owner`, and `AI_TargetCard.target` are valid payload surfaces; `ActChat` returns false and representative production creation paths do not publish through this hook (`SOURCE-OBSERVED`, runtime samples still `UNRESOLVED`).
- Drama hooks are Harmony patches, not event bus hooks: `DramaManager.ParseLine`, `DramaEventTalk.InitDialog`, `DialogDrama.SetText` (`VERIFIED-METADATA`, `VERIFIED-RUNTIME` installed).

Remaining hook/UI behavior probes are consolidated in `../verification/runtime-probes.md`.
