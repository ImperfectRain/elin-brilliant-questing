# Element aliases

Read from a live game on 28 Aug 2026 (Elin EA, Unity 2021.3.45). The sheet holds **1099 rows, all
aliased**. Look them up through `SourceData.alias` — `GetRow(string)` keys on the row id and never
matches an alias, which is the mistake that made every lookup fail at once.

```csharp
EClass.sources.elements.alias.TryGetValue("negotiation", out SourceElement.Row row);
int value = chara.elements.Value(row.id);
```

Aliases are **data, not code**: they live in the Element sheet, so they cannot be recovered from
`Elin.dll` and have to be read from a running game. Re-verify after a game update.

Recorded here so the project never spends another launch asking. The `50000+` block is omitted —
it is the generated cross-product of projectile shapes and damage elements
(`ball_Fire`, `bolt_Cold`, `comet_Void`, …), derivable from the two lists below it.

## Attributes

```
70:STR   71:END   72:DEX   73:PER   74:LER   75:WIL   76:MAG   77:CHA
78:LUC   79:SPD   80:INT   85:piety
```

`piety` being an ordinary element is worth noting — it needs no special accessor.

## Skills

```
100:martial          101:weaponSword     102:weaponAxe       103:weaponStaff
104:weaponBow        105:weaponGun       106:weaponPolearm   107:weaponDagger
108:throwing         109:weaponCrossbow  110:weaponScythe    111:weaponBlunt
120:armorLight       122:armorHeavy      123:shield          130:twohand
131:twowield         132:tactics         133:marksman        134:eyeofmind
135:strategy         150:evasion         152:stealth         200:swimming
207:weightlifting    210:spotting        220:mining          225:lumberjack
226:riding           230:digging         235:milking         237:taming
240:travel           241:music           242:climbing        245:fishing
250:gathering        255:carpentry       256:blacksmith      257:alchemy
258:sculpture        259:jewelry         260:weaving         261:handicraft
280:lockpicking      281:stealing        285:reading         286:farming
287:cooking          288:building        289:appraising      290:anatomy
291:negotiation      292:investing       293:disarmTrap      300:regeneration
301:meditation       302:controlmana     303:manaCapacity    304:casting
305:magicDevice      306:faith           307:memorization
```

Three names differ from what the wiki vocabulary suggests, and all three cost this project a
launch: **Spot Hidden is `spotting`**, **Literacy is `reading`**, **Appraising is `appraising`**
(not `identify` — that alias belongs to the spell `8230:SpIdentify`). **Pickpocket is `stealing`**.

## Home and settlement

Not yet used by the adapter. This is what `ReadHomeState` would be built on, and it maps directly
onto the Home Skills the design document expects.

```
2115:fAdmin      2116:fEducation  2117:fLoyal      2118:fLuck
2119:fTaxEvasion 2120:fHeirloom   2200:fSoil       2201:fElec
2202:fPromo      2203:fMoral      2204:fFood       2205:fSafety
2206:fAttraction 2207:fRation     2003:fConstruction
```

Public Safety is `fSafety`, Public Morality `fMoral`, Food Supply `fFood`, Publicity `fPromo`,
Administration `fAdmin`, Soil `fSoil`.

## Policies

`2500`–`2828`, including `prohibition`, `resident_tax`, `resident_wanted`, `human_right`,
`inquisition`, `border_watch`, `open_business`, `license_stolen`, `store_ripoff`. Relevant later to
Home-as-sanctuary and criminal routes.

## Other ranges worth knowing

```
1200-1750   feats (featThief, featMurderer, featDisguise, featGoodKarma, featBadKarma, …)
1510-1565   mutations and ether afflictions
3500-3900   biome and landmark flags (bfCave, bfRuin, bfLandmark1, …)
4000-6666   acts and AI goals (AI_Steal, ActPick, TaskTalk, AI_OpenLock, ActChat, …)
8200-9503   spells (SpTeleport, SpIdentify, SpIncognito, SpMagicMap, …)
400-499     traits and modifiers (levitation, invisibility, searchRange, negateSteal, …)
```

`8780:SpIncognito` is the Karma-hiding spell the design document mentions; `6011:AI_Steal` and
`6640:ActSteal` are the acts a crime observer would watch for.
