# Build Manifest

Checked: 2026-08-30, local workspace `C:\Users\loplo\Documents\ChatGPT\Elin-brilliant-questing`.

## Repository

- Root: `C:\Users\loplo\Documents\ChatGPT\Elin-brilliant-questing` (`VERIFIED-METADATA`, shell `Get-Location`).
- Branch: `master` (`VERIFIED-METADATA`, `git status --short --branch`).
- HEAD: `e58434a58ffa5bc697e938bb0778e5567a96b981` / `BQ-036 "What's been happening?"` (`VERIFIED-METADATA`, `git log`).
- Live branch tips: `master`, `origin/master`, and `origin/HEAD` all point to `e58434a58ffa5bc697e938bb0778e5567a96b981` (`VERIFIED-METADATA`, `git branch --all --verbose --no-abbrev`).
- Worktree before edits: clean except branch tracking line (`VERIFIED-METADATA`).

## Installed Elin

- Game root: `E:\SteamLibrary\steamapps\common\Elin` (`VERIFIED-METADATA`, filesystem discovery).
- Installed SourceExport version: `EA 23.338 Patch 2` at `E:\SteamLibrary\steamapps\common\Elin\SourceExport\EA 23.338 Patch 2` (`SOURCE-DATA`).
- `Elin.dll`: `E:\SteamLibrary\steamapps\common\Elin\Elin_Data\Managed\Elin.dll`, length `3432960`, modified `2026-08-23 23:14:25`, file/product version `0.0.0.0` (`VERIFIED-METADATA`).
- `Elin.exe`: `E:\SteamLibrary\steamapps\common\Elin\Elin.exe`, modified `2025-10-18 20:33:36` (`VERIFIED-METADATA`).
- Source export generated/modified: `2026-08-29 22:22:11-12` (`SOURCE-DATA`).
- Installed load order includes `E:\SteamLibrary\steamapps\common\Elin\Package\BrilliantQuesting,1` plus many active workshop packages (`VERIFIED-METADATA`).

## Relevant Assemblies

- BepInEx core path: `E:\SteamLibrary\steamapps\common\Elin\BepInEx\core` (`VERIFIED-METADATA`).
- Relevant BepInEx assemblies: `BepInEx.Core.dll`, `BepInEx.Unity.dll`, `BepInEx.Preloader.Core.dll`, `BepInEx.Preloader.Unity.dll`, `0Harmony.dll`, `Mono.Cecil*.dll`, `MonoMod.RuntimeDetour.dll`, `MonoMod.Utils.dll` (`VERIFIED-METADATA`).
- Unity managed path: `E:\SteamLibrary\steamapps\common\Elin\Elin_Data\Managed` (`VERIFIED-METADATA`).
- Relevant Unity assemblies include `UnityEngine.dll`, `UnityEngine.CoreModule.dll`, `UnityEngine.UI.dll`, `Unity.TextMeshPro.dll`, `UnityEngine.InputLegacyModule.dll`, and other Unity module DLLs in Managed (`VERIFIED-METADATA`).
- Shipped modding kit: `E:\SteamLibrary\steamapps\common\Elin\Package\_ModdingKit`; `package.xml` title `Elin Modding Kit`, id `elin.plugins.modding`, version `0.23.317`; source is present under `Package\_ModdingKit\Source` (`VERIFIED-METADATA`).
- Core game package: `Package\_Elona\package.xml`, id `elin_core1`, version `0.22.1` (`VERIFIED-METADATA`).

## Runtime Evidence

- Current BepInEx log: `E:\SteamLibrary\steamapps\common\Elin\BepInEx\LogOutput.log`, length `24362`, modified `2026-08-29 22:23:06` (`VERIFIED-RUNTIME` for its logged observations).
- Existing probe output in that log: BQ loaded, Drama choice projector installed, all 32 element aliases resolved, 38 procedural check rows available through `Check.Get`, 13 of 14 capabilities available, Home unreadable/no Home on save, item quality source selected as `Thing.rarity`, player faith `harvest`, piety `54`, influence `27`, fame `1205`, money `208162` (`VERIFIED-RUNTIME`).

## Local Reference Outputs

Generated but deliberately uncommitted under ignored `reference/elin/`:

- `metadata-index.json` and `.txt`: 33,303 types across 156 installed managed assemblies; Elin assembly has 3,827 types (`VERIFIED-METADATA`).
- `source-data-index.json` and `.txt`: 20 BQ-relevant SourceData sheets from `EA 23.338 Patch 2` (`SOURCE-DATA`).
- `history-uncertainty.txt`: raw full-history uncertainty-language hits from `git rev-list --reverse --all` (`VERIFIED-METADATA`).
