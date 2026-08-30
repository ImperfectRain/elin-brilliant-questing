# Mod Loading And Packaging

- Installed BepInEx is the BepInEx 6 layout under `BepInEx\core` with `BepInEx.Core.dll` and `BepInEx.Unity.dll` (`VERIFIED-METADATA`).
- The installed package chainloader has local `Package\BrilliantQuesting` enabled in `loadorder.txt` (`VERIFIED-METADATA`).
- Shipped `_ModdingKit` package has version `0.23.317` and source files under `Package\_ModdingKit\Source` (`VERIFIED-METADATA`).
- BQ package `package.xml` must keep its game compatibility version aligned with the installed build's expectations; old notes warn that package version below `BaseCore.versionMod` is silently skipped (`SOURCE-OBSERVED` from prior installed implementation reading, not reverified in this pass).
