# UI Surfaces

- Generic Drama talk is the current interactive surface. It injects BQ choices only for `_chara` / `main` (`VERIFIED-METADATA`, `SOURCE-OBSERVED`).
- BQ journal, chronicle, case notes, and debug report currently write to BepInEx log and notify the player via `Msg.SayRaw` (`VERIFIED-METADATA`, `VERIFIED-RUNTIME` for log route).
- Ambient rumors and town news use `ElinBark`, which tries Chara raw speech and falls back to `Msg.SayRaw` (`UNRESOLVED` actor balloon; fallback established in code).
- Native `LayerJournal` integration is not built; installed metadata only confirms a small surface (`VERIFIED-METADATA`, `UNRESOLVED`).
