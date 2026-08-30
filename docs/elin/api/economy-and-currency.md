# Economy And Currency

- Currency reads/writes use `Card.GetCurrency(string)` and `Card.ModCurrency(int,string)` (`VERIFIED-METADATA`).
- Runtime probes preserved `money` at `208162` and `influence` at `27` with zero-delta writes (`VERIFIED-RUNTIME`).
- BQ treats influence as player-held currency, not `Player.expInfluence` (`VERIFIED-RUNTIME` from earlier notes and current code).
- Guild contribution is read as `Card.GetCurrency("contribution")`; current debug log reported `0` (`VERIFIED-RUNTIME`).
- Guild membership is `Guild.IsMember`, implemented as `Faction.relation.type == 2` (`SOURCE-OBSERVED`). Numeric guild progression is real: `FactionRelation` has `rank`, `exp`, `ExpToNext`, `GetSalary()`, `TextTitle`, and `Promote()`; vanilla guild quest/detail UI displays rank, contribution, salary, and rank-gated benefits (`VERIFIED-METADATA`, `SOURCE-OBSERVED`). BQ's current `GetGuildRank` binary fallback is stale and should read `guild.relation.rank` when `IsMember` is true.
- `TrySpendMoney` resolves payer and named payee before changing either balance and treats `EntityId.None` payee as an intentional sink (`STUB-VERIFIED`, `VERIFIED-METADATA`).
