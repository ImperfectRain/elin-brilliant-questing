# SourceData Sheets

| Sheet | BQ relevance | Preserved fields in local index | Evidence |
|---|---|---|---|
| `SourceElement` | attributes, skills, piety, Home metrics | id, alias, parent/category/tag/detail | `SOURCE-DATA` |
| `SourceCheck` | procedural check row compatibility | id, baseDC, element, targetElement, dice/crit/fumble | `SOURCE-DATA` |
| `SourceChara` | actor tags, jobs/races/faith defaults | id, name, race, job, tag, category, faith | `SOURCE-DATA` |
| `SourceCharaText` | raw character text vocabulary | id and text columns | `SOURCE-DATA` |
| `SourceRace` | actor classification/generation context | id/name/tag/elements | `SOURCE-DATA` |
| `SourceJob` / `SourceHobby` | Home activity and future affordances | id/name/elements/tags | `SOURCE-DATA` |
| `SourceFaction` | guild/settlement identity | id/name/tags | `SOURCE-DATA` |
| `SourceReligion` | deity mapping and faith routes | id/name/name2/domain/elements/rewards | `SOURCE-DATA` |
| `SourceZone` / `SourcePerson` | location/person generation | id/name/area/faction/tags | `SOURCE-DATA` |
| `SourceHomeResource` | Home resources and policy semantics | id/name/group/type | `SOURCE-DATA` |
| `SourceThing` / `SourceFood` / `SourceRecipe` | evidence, production, demand matching | id/name/category/value/LV/quality/components/tags | `SOURCE-DATA` |
| `SourceSpawnList` / `SourceCategory` | generation affordances and item categories | id/aliases/relationships | `SOURCE-DATA` |
| `SourceQuest` | avoid clobbering vanilla authored quests | id/name/person/zone | `SOURCE-DATA` |
| `LangGame` / `LangGeneral` | presentation vocabulary checks | key/text | `SOURCE-DATA` |
