# Home And Settlements

- `EClass.Branch` is the current entry point used by BQ (`VERIFIED-METADATA`; current runtime save reported no readable Home/no Home).
- Installed metadata has `FactionBranch.members` and `FactionBranch.elements` (`VERIFIED-METADATA`).
- Installed metadata did not confirm BQ's candidate capacity fields (`maxResident`, `maxMember`, `capacity`) on `FactionBranch` (`UNRESOLVED`).
- Installed metadata did not confirm BQ's candidate admission methods (`AddMember`, `AddResident`, `AddChara`) on `FactionBranch` (`UNRESOLVED`).
- Home metrics are read through `ElementContainer.Value(int)` using SourceElement ids `2115 fAdmin`, `2200 fSoil`, `2202 fPromo`, `2203 fMoral`, `2204 fFood`, `2205 fSafety` (`SOURCE-DATA`, `VERIFIED-METADATA`).
- BQ mutation policy has only one Home write, `TryAdmitResident`; it must remain gated and verified by re-reading residents after the call (`STUB-VERIFIED`, `UNRESOLVED` live behavior).
