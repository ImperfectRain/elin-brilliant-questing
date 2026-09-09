# Elin Integration Knowledge Base

This directory records the version-matched Elin API, SourceData, and Brilliant Questing integration evidence for the installed build checked on 2026-08-30.

Evidence tags used here:

- `VERIFIED-RUNTIME`: observed successfully in the installed game.
- `VERIFIED-METADATA`: exact symbol/signature exists in installed assemblies.
- `SOURCE-OBSERVED`: behavior established by inspecting installed implementation/decompiled method body.
- `SOURCE-DATA`: exact value or relationship from installed `-exportsource`.
- `STUB-VERIFIED`: BQ code works against a test stub only.
- `EXTERNAL-DOC`: supported only by external wiki/public decompile.
- `INFERRED`: reasonable but not directly established.
- `UNRESOLVED`: evidence is insufficient.

Local generated indexes are intentionally not committed:

- `reference/elin/metadata-index.json`
- `reference/elin/metadata-index.txt`
- `reference/elin/source-data-index.json`
- `reference/elin/source-data-index.txt`
- `reference/elin/history-uncertainty.txt`

Regenerate with:

```powershell
dotnet build tools\ApiDump\ApiDump.csproj
tools\ApiDump\bin\Debug\net8.0\ApiDump.exe --game-root "E:\SteamLibrary\steamapps\common\Elin" --index --json reference\elin\metadata-index.json --text reference\elin\metadata-index.txt
tools\ApiDump\bin\Debug\net8.0\ApiDump.exe --source-index --source-root "E:\SteamLibrary\steamapps\common\Elin\SourceExport\EA 23.338 Patch 2" --json reference\elin\source-data-index.json --text reference\elin\source-data-index.txt SourceElement SourceCheck SourceChara SourceCharaText SourceRace SourceJob SourceHobby SourceFaction SourceReligion SourceZone SourcePerson SourceHomeResource SourceThing SourceFood SourceRecipe SourceSpawnList SourceCategory SourceQuest LangGame LangGeneral
```

Start Elin-facing work with [capability routing](capabilities.md), then read only the owning API page
or question. [verification/api-status.json](verification/api-status.json) is a historical Phase 2
research snapshot, including its counts and commit tips; it does not track later implementation or
evidence corrections. The [detailed matrix](verification/matrix.md) and operation-specific API pages
carry later evidence. In particular, [journal UI](api/journal-ui.md) supersedes early visible-content
claims. Do not treat metadata or successful lifecycle logs as runtime behavior/visual proof.

Phase 2 follow-up entry points:

- `verification/recommended-fixes.md`: implementation-ready defects and stale assumptions found by static analysis.
- `bq-integration/world-affordances.md`: BQ-039 available vanilla affordance map.
- `verification/runtime-probes.md`: reduced three-session runtime validation plan.
