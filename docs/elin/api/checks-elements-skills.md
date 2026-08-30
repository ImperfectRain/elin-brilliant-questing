# Checks Elements Skills

- `Check.Get(string,float)`, `Check.GetText(Chara,Card,bool)`, and `Check.Perform(...)` exist in installed metadata (`VERIFIED-METADATA`).
- Runtime log confirmed 38 BQ procedural check rows are available through `Check.Get` (`VERIFIED-RUNTIME`).
- BQ still resolves most composite checks portably; native rows primarily provide vanilla difficulty wording (`VERIFIED-RUNTIME`, `INFERRED` from code).
- `SourceCheck.csv` is present in the version-matched SourceExport (`SOURCE-DATA`).
- Key SourceElement ids: attributes `70 STR` through `77 CHA`, `85 piety`, `152 stealth`, `210 spotting`, `220 mining`, `240 travel`, `255 carpentry`, `261 handicraft`, `280 lockpicking`, `281 stealing`, `285 reading`, `288 building`, `289 appraising`, `291 negotiation`, `293 disarmTrap`, `306 faith` (`SOURCE-DATA`, `VERIFIED-RUNTIME` all current aliases resolved).
