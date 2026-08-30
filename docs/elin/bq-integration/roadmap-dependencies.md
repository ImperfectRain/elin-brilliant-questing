# Roadmap Dependencies

Current live HEAD is `BQ-036`. Important next dependencies:

- `BQ-037/BQ-038` guild networks: guild membership works, numeric rank does not; treat rank as unresolved/binary until a better API is found (`VERIFIED-RUNTIME`, `UNRESOLVED`).
- `BQ-039/BQ-040` generation from world state: SourceData indexes exist, but no runtime affordance adapter yet (`SOURCE-DATA`, `UNRESOLVED`).
- `BQ-041` onward situation archetypes: use existing SourceData ids for religions, skills, zones, things, and categories, but avoid relying on actor classification until `ActorClassificationProbe` resolves flags (`SOURCE-DATA`, `UNRESOLVED`).
- `BQ-048/BQ-049` Home integration: residents/elements are metadata-visible, capacity/admission/recompute remain unresolved (`VERIFIED-METADATA`, `UNRESOLVED`).
- `BQ-050/BQ-051` economy/demand: item inventory and currency reads work; zone items, production payloads, and item quality semantics need probes (`VERIFIED-RUNTIME`, `UNRESOLVED`).
- `BQ-135` actor activity snapshot must precede any routine/global-goal-dependent logic (`VERIFIED-METADATA`, `UNRESOLVED`).
