# Native capability and evidence router

This is a navigation map, not a second API notebook. Follow the linked canonical evidence for
build, exact signatures, observations and repro steps. Runtime evidence is scoped to the operation
observed: a zero-delta write does not verify nonzero mutation; mounting a tab does not verify pixels.
Current adapter support and evidence strength are separate columns.

## Grades

| Grade | Means |
|---|---|
| VERIFIED-RUNTIME | Observed in the named installed game/build and scenario |
| SOURCE-OBSERVED | Installed method body/call site inspected; no runtime guarantee |
| VERIFIED-METADATA | Symbol/signature exists; no behavioral guarantee |
| HEADLESS-ONLY | BQ contract tested without game; includes existing STUB-VERIFIED evidence |
| UNRESOLVED | Required behavior or population has not been established |
| UNSUPPORTED ON CURRENT BUILD | Current BQ adapter refuses the capability; not proof Elin can never support it |

`SOURCE-DATA`, `EXTERNAL-DOC` and `INFERRED` retain their [existing meanings](README.md).
Use the operation's weakest necessary evidence, not the strongest tag anywhere on its row.

## Capability routing

| Capability / consumers | Support and evidence limit | Canonical evidence |
|---|---|---|
| Stable actor identity; all state | UID binding VERIFIED-RUNTIME; duplicate retirement implemented; recurrence/UID reuse UNRESOLVED | [unresolved](verification/unresolved.md) ELIN-Q-0031; [actors](api/actors.md) |
| Character facets/affordances; casting/goals/services | Work-column sample VERIFIED-RUNTIME (often mechanical templates); other facets/service-open population UNRESOLVED | [identity notes](../elin-api-notes.md#character-identity--what-the-six-facets-are-worth-in-play); [unresolved](verification/unresolved.md) 0028/0030 |
| Actor activity; contexts/autonomy/travel | SOURCE-OBSERVED + HEADLESS-ONLY projection; facet probes fail independently; live population UNRESOLVED | [time/AI](api/time-ai-and-global-goals.md); [matrix](verification/matrix.md) API-048 |
| Life state; lifecycle/availability | `isDead` VERIFIED-METADATA on resolved actor; unresolved binding → Unknown, never proof of death | [matrix](verification/matrix.md) API-006; [actors](api/actors.md) |
| Zone/location/loaded population; witnesses/generation | Current-map scan VERIFIED-RUNTIME; unloaded actor/place assumptions require separate evidence | [world/zones](api/world-and-zones.md); [matrix](verification/matrix.md) API-027/028 |
| Inventory descriptors; crime/evidence/production | PC inventory/quality VERIFIED-RUNTIME; transfer/destroy source-observed/stub-tested, nonzero runtime UNRESOLVED | [inventory](api/items-and-inventory.md); [matrix](verification/matrix.md) API-017–021 |
| Affinity/karma/fame/influence/money; reactions/actions | VERIFIED-RUNTIME reads/zero-delta probes only where noted; nonzero mutation/readback still needs proof | [matrix](verification/matrix.md) API-009–016; [economy](api/economy-and-currency.md) |
| Guild standing; information/authority | Player membership read VERIFIED-RUNTIME; rank/exp SOURCE-OBSERVED; scales and live NPC guild identity UNRESOLVED | [unresolved](verification/unresolved.md) 0025/0026; [matrix](verification/matrix.md) API-014/050 |
| Home state/admission; household/shelter | September 10 owner identity and attach clocks VERIFIED-RUNTIME for sampled Home; return trigger incomplete, production deltas unresolved; admission unchanged | [Home](api/home-and-settlements.md); [unresolved](verification/unresolved.md) 0009–0011 |
| Player companions; household casting | Enumeration UNRESOLVED; adapter name probes may refuse. `IsPCParty` metadata does not prove enumeration | [unresolved](verification/unresolved.md) 0029 |
| Move character between zones; absence/travel | SOURCE-OBSERVED + HEADLESS-ONLY; gated/configured path, non-global actors refused; nonzero/save runtime UNRESOLVED | [world/zones](api/world-and-zones.md); [unresolved](verification/unresolved.md) 0012 |
| Read place contents; evidence/spatial routes | UNSUPPORTED ON CURRENT BUILD (`ReadPlaceContents`); active-map `.things` SOURCE-OBSERVED, adapter not implemented | [unresolved](verification/unresolved.md) 0008; [world affordances](bq-integration/world-affordances.md) |
| Create structured site; scenario dungeons | UNSUPPORTED ON CURRENT BUILD (`BuildPlaceStructure`); Core planning HEADLESS-ONLY; plain StageSite binds loaded zone | [unresolved](verification/unresolved.md) 0032 |
| Inspect ground; additive sites | UNSUPPORTED ON CURRENT BUILD; `InspectGround` returns Unknown | [unresolved](verification/unresolved.md) 0033 |
| Additive site mutation | UNSUPPORTED ON CURRENT BUILD (`AddPlaceFixture`); `ApplySiteAddition` refuses; idempotence/footprints HEADLESS-ONLY | [unresolved](verification/unresolved.md) 0033 |
| Checks/elements; action uncertainty/text | Aliases/rows VERIFIED-RUNTIME; native Check SOURCE-OBSERVED, deterministic portable authority retained | [checks](api/checks-elements-skills.md); [matrix](verification/matrix.md) API-007/008/037–039 |
| Action hooks/witnesses; observed history | Exact Act payload SOURCE-OBSERVED + HEADLESS-ONLY recognition; no production inferred from ActPerformed; witness policy live samples UNRESOLVED | [event hooks](bq-integration/event-hooks.md); [matrix](verification/matrix.md) API-033–036 |
| Generic Drama projection | Patch installation VERIFIED-RUNTIME; `_chara/main` guard SOURCE-OBSERVED; choice count/visibility and open-Drama ordering UNRESOLVED | [Drama](api/dialogue-and-drama.md); [matrix](verification/matrix.md) API-040–046 |
| Native journal | Tab lifecycle VERIFIED-RUNTIME; corrective owned pages SOURCE-OBSERVED/build-tested + HEADLESS-ONLY mounting; visual acceptance UNRESOLVED | [journal checklist](api/journal-ui.md), which supersedes earlier visible-content claims |
| Save chunk | GameIO attach/load/save VERIFIED-RUNTIME baseline; Core no-replay/migrations HEADLESS-ONLY; reverify changed native behavior | [save](api/save-data.md); [matrix](verification/matrix.md) API-001/002 |
| Vanilla catch-up/global goals | SOURCE-OBSERVED; Home deltas and general BQ reconciliation UNRESOLVED | [time/AI](api/time-ai-and-global-goals.md); [unresolved](verification/unresolved.md) 0014/0015 |

Implementation: [ElinVanillaState](../../src/BrilliantQuesting.Plugin/ElinVanillaState.cs),
[ElinSituationStager](../../src/BrilliantQuesting.Plugin/ElinSituationStager.cs).
Validation: [native route](../agent/validation.md#native), [runtime probes](verification/runtime-probes.md),
[build procedure](../plugin-build.md).

## Evidence maintenance

New observation → update the owning API page/question with build, operation and result, then change
only affected routing rows. Update the detailed verification matrix if its claim changed. The
[api-status.json](verification/api-status.json) file is a historical Phase 2 research snapshot;
its counts and commit tips are not a living status authority. Do not refresh those metrics per feature.
Later operation-specific evidence, especially the journal correction, takes precedence over it.
