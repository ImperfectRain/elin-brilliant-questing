# Roadmap Dependencies

Current live HEAD is `BQ-036`. Important next dependencies:

- `BQ-037/BQ-038` guild networks: guild membership works; numeric rank/progression exists on `FactionRelation.rank/exp/ExpToNext`, but current BQ returns binary rank (`SOURCE-OBSERVED`, `FIX-ELIN-007`).
- `BQ-039/BQ-040` generation from world state: SourceData indexes and active-zone APIs now provide a first affordance map, but no runtime affordance adapter exists (`SOURCE-DATA`, `SOURCE-OBSERVED`, `UNRESOLVED` integration).
- `BQ-041` onward situation archetypes: use existing SourceData ids for religions, skills, zones, things, recipes, factions, jobs, hobbies, and categories; use conservative actor classification until runtime samples refine thresholds (`SOURCE-DATA`, `SOURCE-OBSERVED`, `UNRESOLVED` runtime).
- `BQ-048/BQ-049` Home integration: vanilla residents/capacity/admission/recompute are statically identified, but current BQ misses `MaxPopulation` and `AddMemeber` and needs disposable-save validation (`SOURCE-OBSERVED`, `FIX-ELIN-002`).
- `BQ-050/BQ-051` economy/demand: item inventory and currency reads work; production quality should use total quality rather than rarity, transfers must handle stack-merged identity, and production provenance needs hooks outside `ActPerformed` (`SOURCE-OBSERVED`, `FIX-ELIN-004` through `FIX-ELIN-006`).
- `BQ-135` actor activity snapshot can be implemented read-only from timetable/global-goal fields after runtime sampling confirms ordinary actor populations (`SOURCE-OBSERVED`, `UNRESOLVED` runtime).
