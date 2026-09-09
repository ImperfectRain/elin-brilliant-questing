# Validation routing

Use the affected route, not the whole table. Primary tests are linked in the [subsystem cards](../architecture.md).
Start with one class or relevant test method; add neighbors when crossing their contract. A Lab
scenario is an inspection tool, not proof of native behavior. Discover options from its actual catalog:

```powershell
dotnet test tests/BrilliantQuesting.Core.Tests --filter FullyQualifiedName~KnowledgeTests
dotnet run --project tools/BrilliantQuesting.Lab -- describe actor-action
dotnet run --project tools/BrilliantQuesting.Lab -- run actor-action --seed 15
```

Core test class namespace is `BrilliantQuesting.Tests`; filter by class name as above. Join multiple
class filters with `|` inside quotes. Lab tests live in the separate
[Lab test project](../../tests/BrilliantQuesting.Lab.Tests/BrilliantQuesting.Lab.Tests.csproj).

## State

| Change | Smallest useful class(es) | Neighbors / Lab |
|---|---|---|
| Event/ID/RNG | `FoundationTests` | `ConsequenceTests`, `PersistenceTests`; `theft` |
| Truth/belief/proof | `KnowledgeTests` or `EvidenceTraceTests` | `RumorCirculationTests`, `AuthorityActionTests`, `NarrativeJournalTests`; `guilds`, `authority` |
| Memory/callback/provenance | `MemoryTests`, affected `Callback*Tests`, `ItemProvenanceTests` or `LocationHistoryTests` | `CallbackDisclosureTests`, `ChronicleNarrativeTests`; `scene`, `questline` |
| Ties/obligations/standing | `RelationshipHarmTests` or `SocialObligationTests` | `DisclosureDecisionTests`, `StandingSheetTests`, `DevelopmentLayerTests`; `authority` |
| Character/goals/emotion | `ProblemSolvingStyleTests`, `EmotionalStateTests`, affected profile tests | `ActorLocalInterpretationTests`, `DisclosureDecisionTests`, `ActorActionScenarioTests` (Lab project); `actor-action`, `playground` |
| Identity/affordances | `IdentityAffordanceTests`, `ActorIdentityIntakeTests` | `CharacterIdentityTests`, `HouseholdActorCastingTests`, persistence; `playground` plus native attach diagnostic |

For any saved semantics, add [Persistence](#persistence). Native identity, standing and physical proof
changes also require [Native](#native).

## Actions

| Change | Smallest useful class(es) | Neighbors / Lab |
|---|---|---|
| Verb/binding/availability | Affected family, e.g. `CrimeActionTests`, plus `ActionBindingTests` or `ActionAvailabilityTests` | `ContextualActionProjectionTests`, `PlayerNpcActionSymmetryTests`, `ResolutionScopeTests`; `theft`, `actor-action` |
| Check arithmetic/profile | `CheckTests` | Affected verb family, `SemanticConversationIntegrationTests`; `actor-action` |
| Reactions/observed deeds | `ConsequenceTests` or `VanillaActionRecorderTests` | `RecognizedViolenceTests`, `RelationshipHarmTests`, `VisibleConsequenceTests`; `theft` |
| Thread lifecycle/ending | `ThreadLifecycleTests` | `ResolutionScopeTests`, `ChronicleTests`, persistence; `questline` |
| Development rules | `DevelopmentLayerTests` | `StoryletEngineTests`, `DevelopmentScoringTests`; `playground` |

New semantic/profile vocabulary consumed by content requires the [content gate](#content-and-full-gate).
Native mechanics/choices require Plugin build and live [Native](#native) verification.

## Expression

| Change | Smallest useful class(es) | Neighbors / Lab |
|---|---|---|
| Storylet/casting/chemistry | `StoryletEngineTests`, `StoryletCastingTests` or `StoryletChemistryTests` | `StoryletRoutingTests`, `StoryletSearchBoundsTests`; `scene` |
| Intent/disclosure/speech | `DisclosureDecisionTests`, `DisclosureDepthTests` or `SemanticSpeechActTests` | `LyingAndEvasionTests`, `SemanticConversationIntegrationTests`; `playground` |
| Conversation/commitments | `ConversationStateTests` | `SocialObligationTests`, `StoryletRoutingTests`; `playground` |
| Wording/voice/vocabulary | `FragmentRealizationTests`, `VoiceIdiolectTests`, affected vocabulary tests | `FragmentSemanticHonestyTests`, `MundaneWordingTests`; `playground-sweep` |
| Repetition/weirdness/sincerity | `RepetitionControlTests`, `WeirdnessBudgetTests` or `SincerityBudgetTests` | `StoryletRoutingTests`, `TonePresentationTests` (Lab project); `playground-sweep`, `scene` |
| Attention/director/fingerprints | `NarrativeAttentionBudgetTests`, `DevelopmentScoringTests` or `SituationFingerprintTests` | `AmbientTalkTests`, `TownNewsTests`, `SettlementSituationGeneratorTests`; `ambient`, `integration` |
| Anti-template measurements | `AntiTemplateHarnessTests` (Lab project) | `SceneFixtureTests`, `LabCommandLineTests`, `SituationFingerprintTests`; `anti-template` |

All content/vocabulary changes run the compiler and bundle checks below. Core-only expression work
does not require a Plugin build unless a Plugin-consumed public contract changes. Live hosting is
not implied by a passing Lab scene.

Run `dotnet run --no-build --project tools/BrilliantQuesting.Lab -- run anti-template --seed 15 --runs 20`
after building the Lab. This emits JSON for 20 consecutive seeds over each registered scene fixture.
CI retains the profile as an artifact. Inspect missing samples and unpresented runs alongside rates;
repetition itself is diagnostic, not a content-quality pass/fail threshold.

## World

| Change | Smallest useful class(es) | Neighbors / Lab |
|---|---|---|
| Activity/embodiment | `ActorActivityTests` | `PlayerNpcActionSymmetryTests`, `MutationPolicyTests`; `actor-action`, native facet diagnostic |
| Generation/archetypes | `SettlementSituationGeneratorTests` or affected archetype tests | `RecoveryRouteTests`, `NarrativeAttentionBudgetTests`; `integration`, `failed-caravan` for that archetype |
| Autonomy/schemes/ecology | `AutonomousInterventionTests`, `OffScreenSchemeTests` or `AdventurerEcologyTests` | `PlayerNpcActionSymmetryTests`, discovery, persistence; `autonomy`, `off-screen-schemes`, `adventurer-ecology` |
| Simulation tiers | `SimulationTierTests`, `OffScreenSchemeTests` | Persistence gates, `ActorIdentityIntakeTests`, `PlayerNpcActionSymmetryTests`; `off-screen-schemes`, `integration`; Plugin build and live Home revisit/readback |
| Travel/absence | `TravelingGroupTests` or `AbsenceTests` | `FailedCaravanTests`, `ConsequenceArrivalTests`; `traveling-groups`, `consequence-arrivals` |
| Organizations/demand/business | `OrganizationActivityTests`, `BusinessContinuityTests` or `ProductionActionTests` | Relevant archetype, persistence; `integration` |
| Sites/planning/content | `SiteGenesisTests`, `ScenarioPlanTests`, `ScenarioDungeonTests` or `SiteMutationTests` | `SiteRoutesTests`, `SiteContentsTests`, `SiteCandidatesTests`, persistence; `dungeon`, `site-addition` |
| Spatial expressive-range diagnostics | `SpatialRangeHarnessTests` (Lab project) | `ScenarioPlanTests`, `AntiTemplateHarnessTests`, `LabCommandLineTests`; `spatial-range --seed 15 --runs 20` |

Sites using grammars/pieces require the content gate. World mechanics using native writes/activity
require native evidence. Every changed schedule/once-only marker requires reload/idempotence coverage.

## Native

| Change | Headless / build | Live acceptance |
|---|---|---|
| Adapter read/write/capability | `VanillaApiReflectionTests`, `VanillaWriteSafetyTests`, `MutationPolicyTests`, affected observation tests; Plugin build | Exact readback/nonzero operation, failure/refusal, zone transition and save/reload as relevant; record build and outcome in owning [API evidence](../elin/capabilities.md) |
| Runtime evidence instrumentation | `RuntimeEvidenceTests`; Plugin build | [Performance and Home capture](../elin/verification/runtime-probes.md#performance-and-home-capture); actual measurements remain unverified until returned |
| Journal/UI projection | `JournalPagesTests`, `NarrativeJournalTests`, `NativeJournalSurfaceTests`, `DynamicTabMemoryPolicyTests`; Plugin build | [Corrective journal checklist](../elin/api/journal-ui.md#live-verification--all-items-unverified-for-this-corrective-build), including vanilla isolation, long lists and UI scale |
| Drama/actions/discovery | `ContextualActionProjectionTests`, `SceneStatusTests`, `AmbientTalkTests`, `TownNewsTests`; Plugin build | Generic versus authored dialogue, click once/revalidate, interruption, visibility-before-learning, fallback; [probes](../elin/verification/runtime-probes.md) |
| Assembly/API tooling | Build [ApiDump](../../tools/ApiDump/ApiDump.csproj), run relevant metadata/source-data query | Metadata establishes signatures only; follow [knowledge-base procedure](../elin/README.md) |

```powershell
dotnet build src/BrilliantQuesting.Plugin/BrilliantQuesting.Plugin.csproj
```

Plugin is **not in the root solution**. Supply local game libraries using [plugin-build](../plugin-build.md);
report missing libraries/live access as an unproved gate, not a passing test. Do not install or mutate
a live save just to validate documentation.

## Persistence

Run `MigrationFixtureTests`, `PersistenceTests` and `FoundationTests` plus the affected subsystem's save tests. Exercise old
input/defaults or migration, round trip, stable IDs/RNG, no event redispatch, no duplicate consequences,
and relevant native reattachment. See [save contract](../systems/integration.md#persistence).
The [historical fixtures](../../tests/BrilliantQuesting.Core.Tests/Fixtures/Saves/README.md) are frozen
outputs from each schema's serializer. The ordinary Core CI test job loads every version through
the production migration chain and checks preserved state, defaults, repeated reload and IDs/RNG.
Adding a schema requires a new fixture; missing versions fail the test rather than disappearing
from file-based discovery. Retain older fixtures unchanged.

## Content and full gate

After targeted checks pass and the change is coherent:

```powershell
dotnet build ElinBrilliantQuesting.sln
dotnet test ElinBrilliantQuesting.sln --no-build
dotnet run --project tools/ContentCompiler -- --check
```

Compiler `--check` validates the authored input and checks the shipped bundle is current. If content
changed, first regenerate with `dotnet run --project tools/ContentCompiler`, then check; never
regenerate merely to hide unexplained drift. ContentCompiler is separate from the root solution.
Use `--coverage -` when changing coverage semantics; coverage mode does not write the bundle.
For cross-system scheduling/persistence changes also run:

```powershell
dotnet run --project tools/BrilliantQuesting.Lab -- run integration --days 30 --quiet
```

These supplement, not replace, Plugin/live requirements above. A docs-only pass needs link validation
and focused review, not unrelated simulation tests. The [documentation checker](../../tools/validate_docs.py)
has its own dependency-free checks:

```powershell
python tools/validate_docs.py
python -m unittest discover -s tools/tests -p test_validate_docs.py
```

The checker validates local inline Markdown links/anchors in agent entry points and navigation
documents, including source/test/Lab paths. It does not validate C# symbols, code examples, remote
URLs, reference-style links or semantic correctness. Lab IDs/classes in this table are human routes;
their primary source links live in cards and [LabCatalog](../../tools/BrilliantQuesting.Lab/Cli/LabCatalog.cs).
