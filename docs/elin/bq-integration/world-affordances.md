# BQ-039 World Affordance Map

This is an evidence-backed input map for later settlement/world generation. It is not a BQ-039 design and does not hardcode town ids.

## Available Now

- Active zone identity: `EClass._zone`, `Zone.uid`, source row id/name/type/tags from `SourceZone.csv` (`VERIFIED-METADATA`, `SOURCE-DATA`).
- Loaded population sample: `EClass._map.charas`, each `Chara.source`, `source.job`, `source.hobbies`, `source.trait`, `source.race`, `idFaith`, and `faction` (`VERIFIED-METADATA`, `SOURCE-DATA`).
- Loose active-zone item sample: `EClass._map.things` with `Thing.source`, `Thing.category`, value/level/quality fields (`VERIFIED-METADATA`, `SOURCE-OBSERVED`).
- Home branch surface when present: `EClass.Branch.owner`, `members`, `elements`, `MaxPopulation`, `CountMembers`, and efficiency/work refresh methods (`VERIFIED-METADATA`, `SOURCE-OBSERVED`).
- Guild/faction membership and rank: `FactionManager.Fighter/Mage/Thief/Merchant`, `Guild.IsMember`, `FactionRelation.rank/exp/ExpToNext`, `SourceFaction.csv` (`VERIFIED-METADATA`, `SOURCE-OBSERVED`, `SOURCE-DATA`).

## Available With Adapter

- Population and service profile: aggregate loaded actors by SourceData `job`, `hobbies`, `trait`, `race`, `faith`, guild personnel traits, guard traits, shop traits, and authority roles (`SOURCE-DATA`, `VERIFIED-METADATA`, runtime sample still useful).
- Production infrastructure: aggregate loaded `Thing`/zone object rows by `SourceThing.workTag`, `trait`, recipe/category links, and relevant `SourceRecipe.factory/type` (`SOURCE-DATA`, adapter not built).
- Religious infrastructure: loaded actors with faith plus `SourceReligion.domain/elements/cat_offer/rewards` and zone/object traits for altars or clergy (`SOURCE-DATA`, runtime adapter not built).
- Crime/security affordances: loaded guards/guild personnel, `Chara.CanSeeLos`, perception/stealth elements, zone population density, and authority role classifier (`VERIFIED-METADATA`, runtime witness semantics unresolved).
- Item availability: active-zone loose items plus loaded merchant/shop actors and SourceData category/recipe mappings (`SOURCE-DATA`, active-zone item adapter pending).
- Settlement scale: loaded population count, `SourceZone.type/LV/chance/cost` fields, Home branch rank/resources where present, and ranked-zone/faction UI sources (`SOURCE-DATA`, `SOURCE-OBSERVED`, runtime calibration pending).

## Static SourceData Only

- Jobs and hobbies: `SourceJob.csv` and `SourceHobby.csv` expose elements, area/destination traits, resources, tax, things, work tags, and details (`SOURCE-DATA`).
- Zones: `SourceZone.csv` exposes id, parent, type, level, faction, profile/file/biome/generator ids, playlist, tags, travel costs, quest tags, and text flavor (`SOURCE-DATA`).
- Characters/persons: `SourceChara.csv` and `SourcePerson.csv` expose ids, actor ids, names/akas, faction, level, race, job, tactics, idle/combat acts, elements, equipment/loot, faith, works, hobbies, recruit items, and details (`SOURCE-DATA`).
- Things/foods/recipes/categories: `SourceThing.csv`, `SourceFood.csv`, `SourceRecipe.csv`, and `SourceCategory.csv` expose source ids, categories, recipe keys, factories, components, materials, tiers, values, levels, qualities, traits, elements, tags, stack sizes, gifts/deliverables/offers/tickets (`SOURCE-DATA`).
- Factions/religions/Home resources/spawn lists: `SourceFaction.csv`, `SourceReligion.csv`, `SourceHomeResource.csv`, and `SourceSpawnList.csv` expose ids and static relationships useful for generation constraints (`SOURCE-DATA`).

## Runtime-Unverified

- Whether loaded actor/job/hobby samples are representative of unloaded settlement population.
- Which service traits imply actual usable service availability for the player at a given moment.
- Practical Home resource deltas after `Zone.Simulate()`/`FactionBranch.OnAfterSimulate`.
- Whether `Map.things` enumeration covers all loose items relevant to evidence/repair routes after zone transitions.
- Visual/native UI availability for presenting an affordance profile in the journal.

## Not Available

- A single installed API that returns a complete settlement affordance profile.
- A proven safe adapter for arbitrary unloaded-zone loose items.
- Runtime-verified path for moving ordinary non-global citizens to synthesize world affordances.
- A native BQ-specific world generation hook; BQ should build a read-only profile from existing vanilla state and SourceData.
