# Checks Elements Skills

- `Check.Get(string,float)`, `Check.GetText(Chara,Card,bool)`, and `Check.Perform(...)` exist in installed metadata (`VERIFIED-METADATA`).
- Runtime log confirmed 38 BQ procedural check rows are available through `Check.Get` (`VERIFIED-RUNTIME`).
- `Check.Perform(Chara,Card)` computes final DC from `Check.GetDC`, actor elements, target level/elements, and subfactors, then rolls `Dice.Roll(1,20,0,null)`. Natural 20 is critical pass, natural 1 is critical fail, otherwise roll >= final DC passes. No side effect beyond optional callback was found (`SOURCE-OBSERVED`).
- BQ keeps authoritative procedural check resolution on its deterministic resolver because native `Check.Perform` uses Elin RNG, not BQ's persisted RNG stream (`SOURCE-OBSERVED`, `INFERRED`). Native rows remain useful for `Check.GetText` presentation; no current BQ composite check should call `Check.Perform` for authoritative resolution.
- `SourceCheck.csv` is present in the version-matched SourceExport (`SOURCE-DATA`).
- Key SourceElement ids: attributes `70 STR` through `77 CHA`, `85 piety`, `152 stealth`, `210 spotting`, `220 mining`, `240 travel`, `255 carpentry`, `261 handicraft`, `280 lockpicking`, `281 stealing`, `285 reading`, `288 building`, `289 appraising`, `291 negotiation`, `293 disarmTrap`, `306 faith` (`SOURCE-DATA`, `VERIFIED-RUNTIME` all current aliases resolved).
