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
| money | `Card.GetCurrency(string id)`, `Card.ModCurrency(int a, string id)` |
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
- Whether the element aliases in `ElementAliases.cs` are correct. Nothing else is guessed.
