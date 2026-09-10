# Home And Settlements

- `EClass.Branch` is the active zone branch, not a remote Home lookup. The September capture reads Home at Meadow and no Home away from a branch; see the operation-specific evidence below.
- Installed metadata has `FactionBranch.members`, `FactionBranch.elements`, `FactionBranch.owner`, `FactionBranch.CountMembers(FactionMemberType,bool)`, `FactionBranch.MaxPopulation`, `FactionBranch.AddMemeber(Chara)`, `Recruit(Chara)`, `ChangeMemberType`, `RemoveMemeber`, and `RefreshEfficiency()` (`VERIFIED-METADATA`). The vanilla method names are misspelled as `Memeber`.
- Resident capacity is `FactionBranch.MaxPopulation`, implemented as `5 + Evalue(2204)` where SourceElement `2204` is `fFood` (`SOURCE-OBSERVED`, `SOURCE-DATA`). BQ now reads this property directly and keeps `CapacityKnown == false` when it cannot be read (`STUB-VERIFIED`). No current evidence proves an absolute `fFood` value is a resident hunger or pantry threshold, so Home resident generation only derives pressure when the resident count is at or above known food-supported capacity.
- Vanilla resident admission is `FactionBranch.AddMemeber(Chara)` (`VERIFIED-METADATA`, `SOURCE-OBSERVED`). It removes the character from any prior Home branch, removes reserved status, calls `chara.SetGlobal()`, sets faction to Home, sets the Home zone, normalizes hostility/member type, adds to `members`, calls `OnAddMemeber`, `RefreshEfficiency()`, and `chara.RefreshWorkElements(branch.elements)` (`SOURCE-OBSERVED`). BQ now resolves this misspelled method and verifies by re-reading the Home roll; nonzero live admission still requires disposable-save runtime validation.
- `FactionBranch.Recruit(Chara)` wraps `AddMemeber`, clears restraint, may physically place the character in the current zone, refreshes efficiency/work elements again, and emits a hire message (`SOURCE-OBSERVED`). For BQ shelter/residency mutation, `AddMemeber` is the narrower vanilla Home state operation; `Recruit` is a gameplay/hire presentation operation.
- Home metrics are read through `ElementContainer.Value(int)` using SourceElement ids `2115 fAdmin`, `2200 fSoil`, `2202 fPromo`, `2203 fMoral`, `2204 fFood`, `2205 fSafety` (`SOURCE-DATA`, `VERIFIED-METADATA`).
- BQ mutation policy has only one Home write, `TryAdmitResident`; it resolves `AddMemeber(Chara)`, re-reads the resident list, and keeps disposable-save runtime validation before treating nonzero live Home admission as safe (`SOURCE-OBSERVED`, `STUB-VERIFIED`, `UNRESOLVED` mutation persistence).

## September 2026 identity and membership correction

Research cross-checked the public [FactionBranch source](https://github.com/Elin-Modding-Resources/Elin-Decompiled/blob/main/Elin/FactionBranch.cs)
and [EClass source](https://github.com/Elin-Modding-Resources/Elin-Decompiled/blob/main/Elin/EClass.cs)
against locally decompiled installed `Elin.dll` on 2026-09-09. Installed and build-reference DLLs
have SHA-256 `75100d0f8f04c19b1f0e8573c533da229efecf882b1b7c0b5db28d8429c4ddf1`.
This is source/signature evidence, not a successful live run of the corrected adapter.

- `EClass.Branch` returns `core.game.activeZone.branch`. BQ retains this active-zone-only scope.
- `FactionBranch.owner` is a `Zone`, set by `SetOwner`. Identity/name are `owner.uid`/`owner.Name`.
  The old branch `uidZone` probe was wrong. The reader now follows `owner`; missing/throwing owner
  remains unknown and is reported. `ElinHomeStateTests` executes the actual reader against native-shaped
  doubles, then feeds its snapshot through reconciliation/reload.
- `members` is the full roll. `CountMembers(Default, onlyAlive)` filters `memberType`, optional death,
  and `trait.IsCountAsResident`; livestock and other member types are distinct. The user confirmed
  22 members including livestock. Supplied screenshots show 14/24; a later user report describes
  14/22. Neither warrants discarding the full roll. BQ preserves all living members. Full membership
  count and vanilla's filtered population count must not be described as identical measurements.

The supplied diagnostic log (SHA-256
`aefe5f165ea8e12ca660747ce1c8cfacd297cc3e3ade9401a5d599a4df4218e6`, module
`f3313f5d-b81c-4daf-b2d8-3d165ae32d41`) has blank `home_zone` at lines 826-827 and 853-854.
At reload, lines 952-953 show active resident clocks still zero before/after reconciliation.
This is VERIFIED-RUNTIME failure of the old probe. Corrected identity is SOURCE-OBSERVED and
HEADLESS-ONLY until a new capture. The full membership/capacity/skills read worked in that sample.

Screenshots across 11/27/489 to 12/7/489 show fertility 82 to 245 and pasture 8 to 0, with resident
UI count unchanged. These screen values do not prove how often catch-up ran. Home Skill Food is
capacity, not production stock. Admission/mutation evidence is unchanged. Use the
[capture procedure](../verification/runtime-probes.md#performance-and-home-capture) for the next gate.

## September 10 follow-up: attach verified, return hook incomplete

The follow-up log SHA-256 is `8239684172bc337cdc728c85070e79115ba327994d9fd99704a673cba4ffb790`,
module `4d797bc8-eee8-4aa5-b50d-1d7da83e10b4`. This supersedes the corrected-identity runtime
uncertainty above **for this save**: lines 378-379 report Meadow/zone_7 and all 22 active members'
clocks advancing to 254027229, with events staying at 99. Lines 1001-1002 show another successful
attach at 254034236 with events staying at 157. Identity/readback and attach-clock reconciliation
are VERIFIED-RUNTIME for these operations; no native production-delta claim follows.

Return remains incomplete: the pre-save at line 898 is back in zone_7 at minute 254034104, but all
clocks still equal 254027229. No Home zone-change bracket appears before reload. Act callback counts
are zero in the return period, so the action-driven trigger did not provide the required observation.
A reliable post-visit trigger needs investigation. The entire run spans about 4.87 game days,
not a seven-day Warm interval. Preserve full membership; this is not a resident-count failure.

## Post-visit reconciliation correction

Installed `Zone.OnVisit()` source ends after native `Simulate()`, zone and branch `OnAfterSimulate()`
and `lastActive` assignment. BQ now observes that completion with a failure-tolerant Harmony
postfix and reuses canonical intake/reconciliation, including same-zone round trips without
`ActPerformed`. Loading is excluded; attach remains the load owner. This repairs the trigger gap
identified above without changing the 22-member snapshot or rerunning native production.

Evidence: SOURCE-OBSERVED ordering, successful Plugin compilation, HEADLESS-ONLY notification,
retry and detachment checks. Actual post-change visit execution remains UNRESOLVED. The next
capture must show `zone-visit` before/after readback at Home before a save/reload or ordinary action,
then unchanged history/resource consequences on reload. An attached or compiled hook is not proof
that this happened in game.
