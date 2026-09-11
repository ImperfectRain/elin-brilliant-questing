# Historical save fixtures

These are frozen outputs of historical `WorldStateSerializer.Save` implementations, not current
saves with their version numbers edited. They are headless BQ chunks, not Elin saves or live runtime
evidence. Schema 0 was only a fictional migration-mechanism test; the first real schema is 1.

Each serializer ran the same [generator](Generator.cs.txt): theft seed 31337, passing checks,
question the witness, pickpocket the thief, return the item, advance six days, consume one world RNG
value, then save. The records include actors, events, facts, beliefs/proofs, memories, obligations
and a resolved thread. Later schema fields reflect what that historical scenario actually produced.
The collection does not claim to exercise every optional feature; neighboring subsystem tests do that.

| Fixture | Serializer source commit |
|---|---|
| schema-01.json | 630860fcac8e2b68c2863da65afe4f27114b09a9 (parent of BQ-056) |
| schema-02.json | 1dc717d (BQ-056) |
| schema-03.json | be307ba (BQ-057) |
| schema-04.json | 50b32da (BQ-058) |
| schema-05.json | fe13c57 (BQ-059) |
| schema-06.json | e17afd9 (BQ-060) |
| schema-07.json | c50d224 (BQ-061) |
| schema-08.json | 3d64595 (BQ-063) |
| schema-09.json | 6c3205b (BQ-065) |
| schema-10.json | 5564cec (BQ-077) |
| schema-11.json | fa7b874 (BQ-097) |
| schema-12.json | BQa-001, the commit that introduced this schema |

To reproduce a fixture, export that commit's `src/BrilliantQuesting.Core` and `Directory.Build.props`
with `git archive` into a temporary directory. Beside `src`, create a net8.0 console project referencing
the exported Core project, copy `Generator.cs.txt` as `Program.cs`, and run it with the output JSON
path as its sole argument. Do not regenerate old fixtures using current code or during tests.

When a new schema lands, add its serializer's fixture and retain the old files unchanged.
[MigrationFixtureTests](../../MigrationFixtureTests.cs) enumerates all versions through the current
schema constant, so a missing fixture fails the ordinary Core test job in
[CI](../../../../.github/workflows/core.yml). Every fixture goes through the production load API,
diagnostics, migration defaults, field preservation, repeated reload and deterministic continuation.
The only replaced shape is the schema-1 personality, asserted by its semantic mapping; absent
optional text follows the reader's existing normalization.
