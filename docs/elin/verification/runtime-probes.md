# Runtime Probes

Static metadata is not enough for the following. All probes should log only the minimal fields needed to answer the question.

## Safe Read-Only Probes

1. `ActorClassificationProbe` (`ELIN-Q-0013`): on `PostLoad`, sample PC, nearby NPCs, shop/service NPCs, and any obvious story/guild NPCs. Log source id/name, source tags, candidate unique/story members present, and resulting `NarrativeActorClass`. No mutation.
2. `HomeShapeProbe` (`ELIN-Q-0009`, `ELIN-Q-0010`): on a save with a Home, log `EClass.Branch.GetType().FullName`, public fields/properties/methods containing `member`, `resident`, `chara`, `capacity`, `max`, `zone`, `element`, `add`; log current member count and Home metric reads. No mutation.
3. `ActivityProbe` (`ELIN-Q-0014`, `ELIN-Q-0015`): sample nearby residents/adventurers before and after hour advance/zone revisit. Log `idTimeTable`, `CurrentSpan`, current `ai` type, `global != null`, `global.goal` type, trait `UseGlobalGoal`, and zone uid. No mutation.
4. `BarkProbe` (`ELIN-Q-0016`, `ELIN-Q-0017`): when a BQ ambient line is selected, log chosen speech route and whether it returned. During a generic conversation, deliver one harmless test line only when debug config is enabled. No state teaching unless speech succeeds.
5. `ActPerformedPayloadProbe` (`ELIN-Q-0021`): for each `EVENT.ActPerformed`, log act type full name and public/instance field names with shallow type names for matching theft/craft/combat/chat candidates. No mutation.
6. `JournalShapeProbe` (`ELIN-Q-0020`, `ELIN-Q-0023`): when `LayerJournal` opens, log layer type, child component names/types, tab ids, and switch callback parameters. No UI mutation.
7. `ZoneItemProbe` (`ELIN-Q-0008`): log `EClass._map.things` count and first few item uid/name/category/source ids. No mutation.

## Destructive Or Save-Affecting Probes

1. `DestroyItemProbe` (`ELIN-Q-0004`): create or select a disposable mundane item, record holder inventory, call `Thing.Destroy()`, re-read holder inventory and global card lookup. Mark `DISPOSABLE SAVE REQUIRED`.
2. `TransferItemProbe` (`API-020`): move a disposable item between two safe/generated holders with `Chara.Pick`, then re-read both inventories. Mark `DISPOSABLE SAVE REQUIRED`.
3. `HomeAdmissionProbe` (`ELIN-Q-0010`, `ELIN-Q-0011`): on a throwaway Home save, admit a generated/test Chara through any resolved vanilla method, re-read members/jobs/metrics before and after. Mark `DISPOSABLE SAVE REQUIRED`.
4. `AbsenceMoveProbe` (`ELIN-Q-0012`): on a throwaway save, move a generated/test Chara to a known zone and back, then save/reload and verify same uid/currentZone. Mark `DISPOSABLE SAVE REQUIRED`.

Do not upgrade any question to `VERIFIED-RUNTIME` unless the log shows the probe ran successfully in the installed game.
