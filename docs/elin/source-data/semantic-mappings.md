# Semantic Mappings

Current compact mappings that BQ may rely on:

- Deity route names should use `SourceReligion.id`; `harvest` is the installed id for Kumiromi of Harvest (`SOURCE-DATA`, `VERIFIED-RUNTIME`).
- Home metric aliases map to SourceElement faction tech ids, not ordinary character skills (`SOURCE-DATA`).
- BQ's `VanillaSkill.SpotHidden` maps to SourceElement alias `spotting`, Literacy maps to `reading`, Pickpocket maps to `stealing`, Appraising maps to `appraising` (`SOURCE-DATA`, `VERIFIED-RUNTIME`).
- SourceThing categories and item names are both needed for evidence matching until category vocabulary is fully mapped in live inventories (`SOURCE-DATA`, `UNRESOLVED`).
- `SourceChara` tags can help avoid impossible generation candidates, but actor protection must come from live `Chara`/source flags once resolved, not from a static row guess (`SOURCE-DATA`, `UNRESOLVED`).

Do not add raw SourceData rows here. Keep only mappings that BQ code or design currently depends on.
