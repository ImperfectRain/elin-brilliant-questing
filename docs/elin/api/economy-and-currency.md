# Economy And Currency

- Currency reads/writes use `Card.GetCurrency(string)` and `Card.ModCurrency(int,string)` (`VERIFIED-METADATA`).
- Runtime probes preserved `money` at `208162` and `influence` at `27` with zero-delta writes (`VERIFIED-RUNTIME`).
- BQ treats influence as player-held currency, not `Player.expInfluence` (`VERIFIED-RUNTIME` from earlier notes and current code).
- Guild contribution is read as `Card.GetCurrency("contribution")`; current debug log reported `0` (`VERIFIED-RUNTIME`).
- Guild membership is `Guild.IsMember`, implemented as `Faction.relation.type == 2` (`SOURCE-OBSERVED`). Numeric guild progression is real: `FactionRelation` has `rank`, `exp`, `ExpToNext`, `GetSalary()`, `TextTitle`, and `Promote()`; vanilla guild quest/detail UI displays rank, contribution, salary, and rank-gated benefits (`VERIFIED-METADATA`, `SOURCE-OBSERVED`). `GetGuildRank` reads `guild.relation.rank` and `GetGuildContribution` reads `guild.relation.exp` when `IsMember` is true, since `BQ-038` took `FIX-ELIN-007`; both report 0 when the relation cannot be read, and no runtime read of either on a member save exists yet. The player-wide `contribution` currency is a separate number covering all four guilds and is not what either accessor returns.
- `TrySpendMoney` resolves payer and named payee before changing either balance and treats `EntityId.None` payee as an intentional sink (`STUB-VERIFIED`, `VERIFIED-METADATA`).
