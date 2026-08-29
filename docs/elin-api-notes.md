# Elin API notes — Phase 0 spike

Findings from reading the shipped assemblies with `tools/ApiDump`. Nothing here was executed;
this is metadata only, from one specific install:

| | |
|---|---|
| Elin | `Elin.dll`, 3827 types (the real game code — `Assembly-CSharp.dll` is a 6 KB VFX stub) |
| Unity | 2021.3.45 |
| BepInEx | 6.0.0-pre.1 (`BepInEx.Core` / `BepInEx.Unity`, **not** the BepInEx 5 layout) |
| Runtime | CLR 4.0.30319 — Mono, .NET Framework 4.x profile |
| Bundled | Newtonsoft.Json 12.0.0 |

Re-verify against the player's build before trusting any of it; Elin is in active Early Access.

## Checks — the design's central assumption holds

```
Check : EClass
    static Check  Get(string id, float dcMod)
    int           GetDC(Card tg)
    int           GetFinalDC(Chara p, Card tg)
    string        GetText(Chara p, Card tg, bool inDialog)
    Result        Perform(Chara p, Card tg)
    Result        Perform(Chara p, Card tg, Action<Result> action)

Check.Result : CriticalFail | Fail | Pass | CriticalPass
```

`ICheckResolver` can be implemented natively rather than staying a reimplementation.
`GetText` is a bonus: vanilla already renders difficulty wording for dialogue, so procedural
options can describe difficulty in the game's own voice instead of inventing a vocabulary.

The sheet row is richer than `CheckProfile` was built for:

```
SourceCheck.Row : BaseRow
    string id
    int    baseDC, dice, critRange, fumbleRange
    int    element,       float subFactor
    int    targetElement, float targetSubFactor
    float  lvMod
```

`dice`, `critRange` and `fumbleRange` are **per row**. `VanillaStyleCheckResolver` hardcodes d20
with crit on 20 and fumble on 1; that is a default, not the rule, and `CheckProfile` should carry
all three so the fallback resolver and native rows cannot drift apart.

Note the shape: one `element` and one `targetElement` per row, each with a float factor, plus a
level modifier. Vanilla checks are single-element. The procedural profiles compose several
skills and attributes, so a profile is not always one row — either pick a primary element per
profile and carry the rest as situational modifiers, or keep composition on our side and use
vanilla rows only where they map cleanly.

## Adapter targets for IVanillaState

| Contract member | Vanilla |
|---|---|
| entry points | `EClass.pc`, `.player`, `.game`, `.world`, `._zone`, `.Home`, `.Branch` |
| affinity | `Chara.ModAffinity(Chara c, int a, bool show, bool showOnlyEmo)`, `Chara._affinity` |
| karma / fame | `Player.karma`, `Player.fame`, `ModKarma(int)`, `ModFame(int)` |
| influence | `Card.GetCurrency("influence")` / `ModCurrency(a, "influence")` - **not** `Player.expInfluence` |
| attributes / skills | `ElementContainer.Value(int ele)`, `ValueWithoutLink(string alias)`; `Chara.elements` |
| inventory | `Card.things` (`ThingContainer`), `Chara.Pick(Thing, bool, bool)`, `Chara.DropThing(Thing, int)` |
| destroying a thing | `Card.Destroy()` — **not runtime-verified**, see below |
| money | `Card.GetCurrency(string id)`, `Card.ModCurrency(int a, string id)` |
| deity / piety | `Chara.idFaith` (string), `Chara.elements.Value(85)` — the id **vocabulary** is unread, see below |
| zone occupants | `Zone.FindChara(int uid)`, `Zone.FindChara(string id)`, `Zone.AddChara(string, Point)` |

`Chara` carries a `uid`; that is the handle `EntityId` should map to, not a name.

`Player.expInfluence` is a trap. It looks like town Influence and is not: it is experience toward
an influence level-up, wrapping at 1000 and announcing "DingInfluence". It read zero on a character
with fame 1197 and three guild memberships, which is what exposed it. The spendable resource is a
**currency**, alongside `money`, `contribution` (guild contribution), `medal`, `plat`, `casino_coin`
and `deed`.

Beware name collisions when reading these: `Zone` is both an Elin class and
`System.Security.Policy.Zone`. `ApiDump` resolves the game's assembly first.

## Elements: verified alias mapping

Read from a live game on 28 Aug 2026. The sheet has **1099 rows, all aliased**, and
`SourceData.alias` is the dictionary to look them up in - `GetRow(string)` keys on the row id and
never matches an alias.

All eight attributes confirmed:

```
70:STR  71:END  72:DEX  73:PER  74:LER  75:WIL  76:MAG  77:CHA   (78:LUC, unused so far)
```

Skills confirmed by resolving successfully: `negotiation`, `investing`, `stealing` (Pickpocket),
`stealth`, `lockpicking`, `disarmTrap`, `anatomy`, `alchemy`, `cooking`, `faith`, `travel`,
`mining`.

`carpentry`, `building` and `handicraft` were added later for the crafting verbs. They are read
off the same verified table below (255, 288 and 261) rather than guessed, but unlike the twelve
above they have not themselves been resolved against a running game.

All twenty-three now resolve. The three that were wrong: **Spot Hidden is `spotting`**, **Literacy
is `reading`**, **Appraising is `appraising`** (`identify` is the spell `8230:SpIdentify`). Piety is
not a separate accessor either - it is element `85`.

The complete table is recorded in [`elin-element-aliases.md`](elin-element-aliases.md), including
the Home Skill elements (`fSafety`, `fMoral`, `fFood`, `fSoil`, `fPromo`, `fAdmin`) that a future
`ReadHomeState` would use. The plugin still dumps the whole table whenever anything fails to
resolve, so a rename after a game update reports itself.

## The event bus — the most useful thing found so far

`BaseModManager` publishes the game's own lifecycle, and the bundled Scripting Kit uses it for
exactly the jobs this mod needs. This is better than both polling in `Update` and Harmony-patching
the load path.

```
BaseModManager.SubscribeEvent<T>(string eventId, Action<T> handler)

EVENT: PreLoad, PostLoad, PreSave, PostSave, NewGame, ModsActivated,
       PreSceneInit, PostSceneInit, CharaCreated, ActPerformed,
       DramaParseAction, FeatApply, ReligionImporting
```

Three of those are roadmap items answered before they were started:

- **`ActPerformed`** — observe what the player actually did. The intended route for crime and
  witness observation, with no patching.
- **`DramaParseAction`** — present as a constant/event-args type, but the 28 Aug 2026 installed
  build did not publish it from `DramaManager.ParseLine` or `CustomDramaExpansion.ParseAction`.
  The current prototype therefore uses a narrow Harmony postfix on `DramaManager.ParseLine` when
  the built-in `_choices` action is processed.
- **`CharaCreated`** — bind a generated Chara to its `EntityId` at the moment it exists.

## The witness model is ours, not Elin's

Worth stating plainly, because the step that introduced it is called "derive witnesses from the
real world" and that overstates what happens.

Elin has its own crime and witness handling, but it is written to *do* things - raise hostility,
call guards, apply karma - rather than to answer "who could see this?" as a question. So the mod
asks the game only for facts it can read without side effects (`map.charas`, `Chara.Dist`,
`Chara.CanSeeLos`, sight radius, Perception, Spot Hidden, the actor's Stealth) and applies its own
rule to them.

That rule is a **Brilliant Questing model built from real Elin state**, not Elin's own verdict. If
the two ever disagree, neither is wrong; they are answering different questions.

Two limits worth knowing:

- **Stealth stands in for visibility.** The same number that decides whether somebody is sneaking
  is used to decide whether an act was noticed. A very stealthy character can in principle strike
  somebody in plain view and go unseen. Attacks and thefts probably deserve different exposure.
- **Witnessing is currently all-or-nothing.** A witness either testifies "Haron stole the ring" or
  saw nothing. Real investigation wants the middle: seeing that *something happened*, seeing *a
  person* do it, and *recognising* that person are three different pieces of knowledge, and only
  the third lets somebody name a name. Line of sight to the actor is now required precisely
  because the record produced claims the third.

## Why procedural checks resolve portably

`Check.Get` reads all nine `proc_*` rows the mod installs, and yet every check in a live log is
resolved by the portable resolver. That is the design, not a fault, and it confused two rounds of
log reading before it was written down.

A vanilla `SourceCheck.Row` is **single-element**: one actor element with a factor, one target
element with a factor, one level modifier. Every procedural profile is deliberately composite -
intimidation reads Negotiation, Charisma *and* Strength against Will, so that a mute bruiser has a
social route - and composing that is the whole point of `CheckProfile`. So `ElinCheckResolver`
refuses the native path for any profile with more than one actor element, which in practice is all
of them.

The rows still earn their place: `Check.GetText` gives the player **vanilla's own difficulty
wording** on the dialogue option, in the game's language, rather than a number of ours.

If that ever needs revisiting, the decision is one method - `CanResolveNatively` - and the log now
names the resolver and the reason once per profile.

## Identifying the generic conversation

Read off `Chara.ShowDialog` / `Chara._ShowDialog` / `LayerDrama.Activate` in the 28 Aug 2026 build.
Every Drama is opened with a **book**, a **sheet** and a **step**, reachable at runtime as
`DramaManager.setup.book` / `.sheet` / `.step`.

The ordinary "talk to someone" conversation is **book `_chara`, step `main`** — `ShowDialog`'s
defaults. Everything authored passes something else:

| What | Book | Step |
|---|---|---|
| Generic conversation | `_chara` | `main` |
| Talking to the player | `_chara` | `pc` |
| Sleeping, invisible, escort, strain, gift, lunch | `_chara` | `sleep`, `invisible`, `escort`, … |
| Hiring from the board | `_chara` | `4-1` |
| Quest success / failure | `_chara` | `quest_success` / `quest_fail` |
| Arena bout result | `_chara` | `bout_win` / `bout_lose` |
| Maid meeting | `_chara` | `meeting` |
| A character with its own sheet | its `id` | `main` |
| Guild doorman / clerk | `guild_doorman` / `guild_clerk` | `main` |
| Marriage, wedding, worship | `_adv` | `marry`, `wedding`, `worship` |
| Main story | `_main` | per row |
| Quest with its own drama | `source.drama[0]` | `source.drama[1]` |

**Why this matters.** A mod that injects into dialogue must gate on book *and* step. Gating on the
NPC alone means overwriting authored quest, guild and wedding dialogue for anyone the mod is
tracking — `_chara` is itself reused for a dozen non-generic situations, so the book alone is not
enough either.

## Mod save data

```
GameIOContext
    bool Load<T>(string chunkName, out T data, JsonSerializerSettings settings)
    void Save<T>(string chunkName, T data, JsonSerializerSettings settings)
    bool Compress(string chunkName, bool deleteOld)
    static GameIOContext GetPersistentModContext(string path)
```

A `GameIOContext` arrives as the argument of `PostLoad` / `PreSave`, so the world is read and
written on the game's own schedule, into the save's own chunk store, with compression available and
Newtonsoft doing the work. `GetPersistentModContext` covers anything that should outlive a single
save.

An attribute route also exists (`ElinGameIOPropertyAttribute`, carrying a `ChunkName`) and compiles
fine, but the context API is what shipped code uses and it does not require a static property.

## How a package is loaded

Three gates, and the second one fails silently.

```csharp
// ModManager.ActivatePackages
foreach (ModPackage package in packages)
{
    if ((disableMod && !package.builtin) || !package.IsValidVersion()) continue;  // <- no log
    package.Activate();
    if (package.activated) BaseModManager.listChainLoad.Add(package.dirInfo.FullName);
}

// BaseModPackage
public bool IsValidVersion() => !Version.Get(version).IsBelow(BaseCore.Instance.versionMod);
public void Activate() { if (!hasPublishedPackage && installed && dirInfo.Exists && willActivate) ... }
```

1. **`package.xml` must exist** or `Init()` returns false.
2. **`<version>` must be at or above `BaseCore.versionMod`.** This is a compatibility number, not
   the mod's release version, and every shipped package uses the game's own `0.23.x`. A package
   below it is discovered, listed in `loadorder.txt`, activated by the user, and then dropped by a
   `continue` that runs before any logging - so the log is byte-identical to one where the mod was
   never installed. `versionMod` is a serialized Unity field, so its value cannot be read from
   metadata; take it from a mod that currently loads.
3. **The user must activate it**, which is what the trailing `,1` in `loadorder.txt` records.

`BaseModPackage` carries `builtin`, `installed`, `activated` and `willActivate`, and the Scripting
Kit filters on `p.activated && !p.builtin`.

## Packaging

Mods live in `Package/`, loaded by "Package Chainloader 2.0.0", each with a `package.xml`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Meta>
  <title>Elin Modding Kit</title>
  <id>elin.plugins.modding</id>
  <author>DK</author>
  <builtin>true</builtin>
  <loadPriority>-90</loadPriority>
  <version>0.23.317</version>
  <description />
</Meta>
```

The plugin itself follows the ordinary BepInEx shape, per the bundled Scripting Kit source:

```csharp
[BepInPlugin(ModInfo.Guid, ModInfo.Name, ModInfo.Version)]
internal partial class EModdingKit : BaseUnityPlugin
{
    private void Awake() { ... }
}
```

`EMod : BaseModPackage` carries `HashSet<BaseRow> sourceRows`, so a package contributing source
rows is a first-class concept rather than something to patch in. The community "Custom Whatever
Loader" also auto-loads sheets from a mod folder without a DLL. Which route we take for the
procedural `Check` rows is worth testing at runtime before committing to either.

## Still unverified

Everything above is metadata. None of it proves behaviour. Specifically open:

- Whether `Check.Perform` is safe to call outside the contexts vanilla calls it from.
- Whether a mod-supplied `Check` row is picked up without CWL.
- Drama choice injection — `EVENT.DramaParseAction` located, not yet used.
- Crime witness observation — `EVENT.ActPerformed` located, not yet used.
- Whether the element aliases in `ElementAliases.cs` are correct.
- Whether `Card.Destroy()` actually removes a thing from its holder's `things`, and whether it is
  safe on an object a quest or a container still references. `TryDestroyItem` therefore reads the
  holder's inventory back afterwards and reports false unless the object really went, exactly as
  the transfer path does — a destructive verb that lied would leave a case unprovable while the
  evidence sat in the player's pack. `DestroyItems` is probed separately from `TransferItems`
  because a build where moving works and unmaking does not is an ordinary thing to find.
- Whether a finished cook, brew, compound or build reaches `EVENT.ActPerformed` at all. The
  observer matches act type and source ids containing `craft`, `cook`, `brew`, `build` and `mix`,
  and reads the produced Thing off the same `TC`/`TOOL`/`target` fields theft observation uses.
  None of that is confirmed, and Elin's crafting may not go through `Act` at any point. Being
  wrong costs a route rather than breaking one: no match means no provenance record, the crafting
  verbs still work from raw stock, and only the branch that hands over ready-made goods with no
  roll is lost. A live run should be read for the one-time
  "Production-like act ... carried no readable product" line, and for whether nothing at all is
  logged during a cook.
- Which member of `Thing` carries "how well was this made". The adapter searches `quality`,
  `encLv`, `rarity` and `LV` in that order, uses the first that resolves to a number, and logs
  which once. Not one of them is verified, and reading the wrong one would quietly make
  property-constrained demands accept the wrong objects. Nothing found means quality reads zero,
  which every threshold refuses - the safe direction, and the same degraded behaviour described
  above. The candidate list is the first thing to correct off a live inventory dump.
- What `Chara.idFaith` actually contains. The member resolves and the plugin reads it, but no
  running game has been read for the *value*, so whether a Kumiromi worshipper reports
  `"Kumiromi"`, a religion id such as `"harvest"`, or something else again is unknown. This is
  now load-bearing: the faith routes compare that string against the deity a situation names, and
  a vocabulary mismatch costs the whole route rather than degrading it. `DevotionSpec.SameDeity`
  is therefore containment-tolerant and case-insensitive in both directions, so `"Kumiromi"`
  matches `"godKumiromi"`, but it cannot bridge `"Kumiromi"` to `"harvest"`. The one-line fix, once
  a live `idFaith` is read, is in the situation that names the god - not in the verb. An unread
  deity is the empty string, which matches nobody including another empty string, so a build that
  cannot report faith loses the family rather than being handed everyone's routes.
- Whether piety (element `85`) reads non-zero on a devout character in play. A demand for piety is
  a precondition rather than a modifier, so an element that silently reads zero would close the
  faith routes for everybody rather than making them easier - visible, and the safe direction.
- **`GetInventory` resolves a `Chara` and nothing else.** Things standing loose in a place are
  invisible to the live adapter: `ElinBindings.ResolveChara` looks the id up as a character, so
  `GetInventory(zone)` returns empty in game where the headless reference implementation returns
  the room's contents. `repair` reaches a broken object that way and would find nothing in play.
  Consecrated ground is therefore modelled as a fact about the *zone* rather than as an altar
  `Thing` to be found standing in it, which keeps the faith verbs off that path entirely. Reading
  a zone's things (`Zone.things` / the map's card list) is the fix when a step needs it.
- Elin's `Thing.category.id` vocabulary. The adapter reads it into `ItemDescriptor.CategoryTag`,
  but the actual ids for corpses, documents and drinkables have not been read off a running game.
  The investigation verbs therefore match category *and* item name against a keyword list, and the
  generalist `inspect` reads anything, so an unrecognised tag costs a specialist route rather than
  the whole investigation. Worth replacing with the real ids once a live inventory is dumped.
