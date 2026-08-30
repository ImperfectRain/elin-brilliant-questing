# Semantic Mappings

Current compact mappings that BQ may rely on:

- Deity route names should use `SourceReligion.id`; `harvest` is the installed id for Kumiromi of Harvest (`SOURCE-DATA`, `VERIFIED-RUNTIME`).
- Home metric aliases map to SourceElement faction tech ids, not ordinary character skills (`SOURCE-DATA`).
- BQ's `VanillaSkill.SpotHidden` maps to SourceElement alias `spotting`, Literacy maps to `reading`, Pickpocket maps to `stealing`, Appraising maps to `appraising` (`SOURCE-DATA`, `VERIFIED-RUNTIME`).
- SourceThing categories and item names are both needed for evidence matching until category vocabulary is fully mapped in live inventories. For active-zone loose items, pair `EClass._map.things` with `Thing.source` and `Thing.category` (`SOURCE-DATA`, `SOURCE-OBSERVED`, runtime adapter pending).
- `SourceChara` tags can help avoid impossible generation candidates, but actor protection must come from live `Chara`/source flags: `IsUnique`, `IsImportant`, `c_uniqueData`, `c_isImportant`, trait type, quest state, Home/party/faction state, and BQ ownership (`SOURCE-DATA`, `SOURCE-OBSERVED`).
- Settlement affordance profiles should be derived from static rows plus current loaded state: `SourceZone`, `SourceChara`, `SourceJob`, `SourceHobby`, `SourceFaction`, `SourceReligion`, `SourceThing`, `SourceRecipe`, `SourceCategory`, `SourceHomeResource`, and `SourceSpawnList` are the main sheets for BQ-039 (`SOURCE-DATA`).

Do not add raw SourceData rows here. Keep only mappings that BQ code or design currently depends on.
