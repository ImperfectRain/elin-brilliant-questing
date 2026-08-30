# Event Hooks

- `PostLoad`: BQ loads save chunk, restores bindings, installs procedural check rows, detects capabilities, and attaches simulation (`VERIFIED-RUNTIME`).
- `PreSave`: BQ writes bindings and save chunk (`VERIFIED-RUNTIME`).
- `NewGame`: BQ reset path is subscribed; current runtime proof is limited to subscription metadata (`VERIFIED-METADATA`).
- `ActPerformed`: BQ subscribes and `ElinActionObserver` inspects action payloads reflectively (`VERIFIED-METADATA`, `UNRESOLVED` payload behavior).
- Drama hooks are Harmony patches, not event bus hooks: `DramaManager.ParseLine`, `DramaEventTalk.InitDialog`, `DialogDrama.SetText` (`VERIFIED-METADATA`, `VERIFIED-RUNTIME` installed).

Every unresolved hook payload has a probe in `../verification/runtime-probes.md`.
