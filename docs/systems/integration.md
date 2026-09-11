# Persistence and player/native boundaries

[Router](../architecture.md) · [Native validation](../agent/validation.md#native)
· [maintenance](../agent/documentation.md).

## Discovery

**Owns:** controlled learning through `AmbientTalk.Next`/`Deliver`, requested `TownNews` and action
inquiry. Inputs: a speaker's eligible claims, proximity, player knowledge, attention and confirmed
delivery. Outputs: player belief/history and saved delivery cooldown. Selection is pure/transient;
delivery changes owned records. **Does not own:** truth, omniscient quest announcements, or teaching
on a failed render. NPC `RumorCirculation` excludes player learning; an autonomous resolution can
exist without appearing in the journal. Consumers: journal, news, attention, player actions.

Source: [AmbientTalk](../../src/BrilliantQuesting.Core/Knowledge/AmbientTalk.cs),
[TownNews](../../src/BrilliantQuesting.Core/Knowledge/TownNews.cs),
[BrilliantQuestingPlugin](../../src/BrilliantQuesting.Plugin/BrilliantQuestingPlugin.cs).
Proof: [AmbientTalkTests](../../tests/BrilliantQuesting.Core.Tests/AmbientTalkTests.cs),
[NarrativeAttentionBudgetTests](../../tests/BrilliantQuesting.Core.Tests/NarrativeAttentionBudgetTests.cs).
Lab: [ambient](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/AmbientScenario.cs),
[news](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/NewsScenario.cs).
Native bark visibility remains a [runtime question](../elin/capabilities.md).

## Presentation

**Owns:** player-filtered derived views (`NarrativeJournal`, `ChronicleNarrative`, `JournalPageRegistry`)
and native mounting (`NativeJournalSurface`, `NativeJournalRenderer`). Inputs: beliefs, history,
standing and current native reads. Outputs: Overview/Chronicle DTOs/widgets or dialogue/log fallback.
DTOs, widgets and page selection are transient. **Does not own:** additional world state, exposure
events from opening a page, raw inspector visibility or vanilla quest controls. Journal tags preserve
uncertainty. Native BQ pages own their widget hierarchy; do not clone `ContentQuest` children.
`DramaChoiceProjector` owns narrow generic-dialogue action/news projection, not authored Elin dialogue.

Source: [NarrativeJournal](../../src/BrilliantQuesting.Core/Diagnostics/NarrativeJournal.cs),
[JournalPages](../../src/BrilliantQuesting.Core/Presentation/JournalPages.cs),
[NativeJournalSurface](../../src/BrilliantQuesting.Plugin/NativeJournalSurface.cs),
[NativeJournalRenderer](../../src/BrilliantQuesting.Plugin/NativeJournalRenderer.cs),
[DramaChoiceProjector](../../src/BrilliantQuesting.Plugin/DramaChoiceProjector.cs).
Proof: [NarrativeJournalTests](../../tests/BrilliantQuesting.Core.Tests/NarrativeJournalTests.cs),
[JournalPagesTests](../../tests/BrilliantQuesting.Core.Tests/JournalPagesTests.cs),
[NativeJournalSurfaceTests](../../tests/BrilliantQuesting.Core.Tests/NativeJournalSurfaceTests.cs).
Lab: [questline](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/QuestlineScenario.cs) for projections only.
Evidence: [journal UI](../elin/api/journal-ui.md) — mounting observed, corrective page visuals still unverified.

## Persistence

**Owns:** explicit BQ save shape, upgrades, load diagnostics and restoration without dispatch.
`WorldStateSerializer` writes `NarrativeWorldState`; `SaveMigrations` upgrades older versions and
refuses newer/unsupported ones. Inputs: saved JSON or world aggregate. Outputs: restored records,
RNG state, ID counters, diagnostics. Plugin GameIO saves the chunk and rebuilds bindings/services.
**Does not own:** Elin saves, content definitions, rendered dialogue, transient native observations
or UI/decision caches. Save compatibility is a behavior, not only a version number.

| Stored families | Restoration rule |
|---|---|
| IDs, external refs, RNG/counters | Preserve identity and deterministic continuation; handles require native readback |
| NPC profiles/goals, organizations, sites | Restore explicit owned fields; re-read live identity/activity instead of mirroring them; no persisted voice assignment yet |
| Events, facts, beliefs/proofs, memories, ties, obligations | Restore stores directly; never redispatch old events or repeat standing writes |
| Threads/firings, absences, travel, demands, businesses | Restore lifecycle/manifests and their one-time markers; rebuild handlers in host |
| Rumor/ambient stamps | Preserve pacing across reload; policy objects themselves are not saved |

Changing a persisted contract: inspect `ToJson` and the matching reader, older-save defaults,
`SaveMigrations` and neighboring round-trip tests. Versioned incompatible shape changes need a
migration chain; existing additive optional nodes use explicit defaults without a bump. Prove the
chosen strategy with an old fixture, round trip, stable IDs/RNG and no replay; do not bump a version
without behavior coverage or assume every new field is automatically serialized. Review native
reattach/reconciliation separately from JSON restoration.

Source: [WorldStateSerializer](../../src/BrilliantQuesting.Core/Persistence/WorldStateSerializer.cs),
[SaveMigrations](../../src/BrilliantQuesting.Core/Persistence/SaveMigrations.cs),
[NarrativeWorldState](../../src/BrilliantQuesting.Core/World/NarrativeWorldState.cs).
Proof: [PersistenceTests](../../tests/BrilliantQuesting.Core.Tests/PersistenceTests.cs),
[MigrationFixtureTests](../../tests/BrilliantQuesting.Core.Tests/MigrationFixtureTests.cs) with
[historical serializer fixtures](../../tests/BrilliantQuesting.Core.Tests/Fixtures/Saves/README.md),
[FoundationTests](../../tests/BrilliantQuesting.Core.Tests/FoundationTests.cs), affected subsystem round trips.
Lab: [integration](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/IntegrationScenario.cs) includes reload.
Native: [save API](../elin/api/save-data.md). Validation: [Persistence](../agent/validation.md#persistence).

## Native boundary

**Owns:** `IVanillaState` reads/capabilities, `VanillaStateBase` gated mutations,
`NarrativeMutationPolicy` reach, `ISituationStager` generated embodiment and Plugin adapters/bindings.
Inputs: stable BQ IDs and explicit read/write requests. Outputs: typed observations, success/refusal,
handles and diagnostics. Native instances/capability probes are transient; opaque refs persist for
reattachment, not as evidence of existence. **Does not own:** BQ truth/meaning, native simulation
reimplementation or treating metadata as runtime proof. Unknown closes risky writes; allowed writes
must still verify success. Staging has its own refusal path, not a guarantee inherited from the
ordinary mutation gate. Core has no Elin/Unity/BepInEx references. Native hooks/projection stay narrow
and fail closed with diagnostic/fallback.

The Plugin's transient `VanillaCapabilityReport` owns probe results and the opt-in, single-capability
disable drill. It can suppress support but cannot grant it. Direct read fallbacks and witness
collection honor the selected disable; ordinary Home snapshots still refresh independently of the
attach-time probe. [Drill procedure and evidence](../elin/verification/runtime-probes.md#drill-implementation-and-procedure).

Source: [IVanillaState](../../src/BrilliantQuesting.Core/Integration/IVanillaState.cs),
[VanillaStateBase](../../src/BrilliantQuesting.Core/Integration/VanillaStateBase.cs),
[ISituationStager](../../src/BrilliantQuesting.Core/Integration/ISituationStager.cs),
[ElinVanillaState](../../src/BrilliantQuesting.Plugin/ElinVanillaState.cs),
[ElinSituationStager](../../src/BrilliantQuesting.Plugin/ElinSituationStager.cs).
Proof: [VanillaWriteSafetyTests](../../tests/BrilliantQuesting.Core.Tests/VanillaWriteSafetyTests.cs),
[MutationPolicyTests](../../tests/BrilliantQuesting.Core.Tests/MutationPolicyTests.cs).
Lab: [playground-systems](../../tools/BrilliantQuesting.Lab/Cli/Scenarios/PlaygroundSystemsScenario.cs)
reports headless/live boundaries; sandbox success is not native proof. [Capability evidence](../elin/capabilities.md).
