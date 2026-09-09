# Event Hooks

- `PostLoad`: BQ loads save chunk, restores bindings, installs procedural check rows, detects capabilities, and attaches simulation (`VERIFIED-RUNTIME`).
- `PreSave`: BQ writes bindings and save chunk (`VERIFIED-RUNTIME`).
- `NewGame`: BQ reset path is subscribed; current runtime proof is limited to subscription metadata (`VERIFIED-METADATA`).
- `ActPerformed`: BQ subscribes and `ElinActionObserver` inspects action payloads reflectively. Installed `Act.Perform` publishes the exact `Act` payload only after successful perform (`VERIFIED-METADATA`, `SOURCE-OBSERVED`). `Act.CC`, `Act.TC`, `Act.TOOL`, `Act.TP`, `AIAct.owner`, and `AI_TargetCard.target` are valid payload surfaces; `ActChat` returns false and representative production creation paths do not publish through this hook (`SOURCE-OBSERVED`, runtime samples still `UNRESOLVED`).
- Drama hooks are Harmony patches, not event bus hooks: `DramaManager.ParseLine`, `DramaEventTalk.InitDialog`, `DialogDrama.SetText` (`VERIFIED-METADATA`, `VERIFIED-RUNTIME` installed).

Remaining hook/UI behavior probes are consolidated in `../verification/runtime-probes.md`.

## September capture: combat admission

The September 9 capture records `ActMelee`/`ActRanged` combat after reload; these are live samples,
not proof of every action family or witness rule. Passive local registration caused the old
`IsAlreadyKnown` test (binding plus registry presence) to admit background crab/tentacle combat.
The corrected gate uses existing narrative importance through
`VanillaActionRecorder.ShouldObserveViolence`, before witness work or enrollment. Player actions
remain eligible. Existing events remain intact. Headless tests prove the gate survives save/reload;
reduced native callback cost remains unmeasured until another run. Earlier recorded attacks can
already have promoted targets through `ConsequenceEngine`; those saved promotions are retained,
not downgraded to erase the old observation defect. Tests preserve such history and importance.

The Plugin now runs canonical local intake and Home reconciliation on zone change before daily
scheduling, matching attach order. Failed intake defers the callback with a diagnostic and retries
later. Installed `Zone.OnVisit` brackets `Simulate` with `isSimulating`; the observer skips callbacks
inside that native catch-up span. This guard is source/build-verified, not live-verified.
No `Zone.Simulate` call or native write was added.
