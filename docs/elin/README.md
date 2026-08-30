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

Start future Elin-facing work with `verification/api-status.json`, then open `verification/matrix.md` only for detail. Do not treat metadata as runtime behavior.

Phase 2 follow-up entry points:

- `verification/recommended-fixes.md`: implementation-ready defects and stale assumptions found by static analysis.
- `bq-integration/world-affordances.md`: BQ-039 available vanilla affordance map.
- `verification/runtime-probes.md`: reduced three-session runtime validation plan.
