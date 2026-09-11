# Checks Elements Skills

- `Check.Get(string,float)`, `Check.GetText(Chara,Card,bool)`, and `Check.Perform(...)` exist in installed metadata (`VERIFIED-METADATA`).
- Runtime log confirmed 38 BQ procedural check rows are available through `Check.Get` (`VERIFIED-RUNTIME`).
- `Check.Perform(Chara,Card)` computes final DC from `Check.GetDC`, actor elements, target level/elements, and subfactors, then rolls `Dice.Roll(1,20,0,null)`. Natural 20 is critical pass, natural 1 is critical fail, otherwise roll >= final DC passes. No side effect beyond optional callback was found (`SOURCE-OBSERVED`).
- BQ keeps authoritative procedural check resolution on its deterministic resolver because native `Check.Perform` uses Elin RNG, not BQ's persisted RNG stream (`SOURCE-OBSERVED`, `INFERRED`). Native rows remain useful for `Check.GetText` presentation; no current BQ composite check should call `Check.Perform` for authoritative resolution.
- `SourceCheck.csv` is present in the version-matched SourceExport (`SOURCE-DATA`).
- **Row compression.** A `SourceCheck` row carries one actor element with a factor, one target
  element with a factor, and a level modifier; `Check.Get(id, dcMod)` folds everything situational
  into that single `dcMod` (`SOURCE-OBSERVED`, `SOURCE-DATA`). Most BQ profiles read two to four
  actor terms against one or two target terms, so they do not compress into a row without discarding
  the composition that distinguishes them - intimidation reading Strength beside Charisma is the
  point of that profile, not an accident of it. `ElinCheckResolver.CanResolveNatively` therefore
  returns false for every profile, and the installed rows are used for `Check.GetText` wording only.
- **No live final-DC sample.** `Check.GetFinalDC(Chara,Card)` is reachable in installed metadata
  (`VERIFIED-METADATA`) and its composition is read from source (`SOURCE-OBSERVED`), but because the
  native path is diagnostic opt-in and refuses composite profiles, no runtime final-DC value has been
  captured from a live build for any `proc_*` row (`UNRESOLVED`). A sample would compare vanilla's
  arithmetic against BQ's on a single-element row; it would not transfer authority. Absence of the
  sample is not a reason to move composite BQ checks onto native RNG.
- **Classification.** BQ profiles declare an explicit `CheckFamily` (opposed / absolute, with the
  no-roll case carrying no profile at all). Vanilla rows carry no equivalent field, so the family is
  BQ's own reading of its own checks and is not evidence about Elin behavior - see
  [check ownership](../../systems/actions.md#checks) and [D079](../../agent/decisions.md#d079--a-check-declares-which-kind-of-uncertainty-it-is-and-the-portable-resolver-is-the-authority-rather-than-a-stand-in).
- Key SourceElement ids: attributes `70 STR` through `77 CHA`, `85 piety`, `152 stealth`, `210 spotting`, `220 mining`, `240 travel`, `255 carpentry`, `261 handicraft`, `280 lockpicking`, `281 stealing`, `285 reading`, `288 building`, `289 appraising`, `291 negotiation`, `293 disarmTrap`, `306 faith` (`SOURCE-DATA`, `VERIFIED-RUNTIME` all current aliases resolved).
