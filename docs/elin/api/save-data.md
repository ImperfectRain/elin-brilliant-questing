# Save Data

- BQ persists through `GameIOContext.Load<T>`, `Save<T>`, and `Compress` on the game's `PostLoad`/`PreSave` events (`VERIFIED-METADATA`, `VERIFIED-RUNTIME`).
- Current log: `Restored 38 object binding(s) from the save` and later `Saved 2 events into chunk 'brilliantQuesting'` (`VERIFIED-RUNTIME`).
- `GameIOContext.GetPersistentModContext(string)` exists for non-save persistent mod state but current BQ save state uses save chunk context (`VERIFIED-METADATA`, `INFERRED`).
- Save/load must not redispatch historical events or reapply consequences; this is a BQ invariant, not an Elin API fact (`STUB-VERIFIED`).
