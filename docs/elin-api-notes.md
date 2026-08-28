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
| influence | `Player.expInfluence`, `AddExpInfluence(int)`, `MaxExpInfluence` |
| attributes / skills | `ElementContainer.Value(int ele)`, `ValueWithoutLink(string alias)`; `Chara.elements` |
| inventory | `Card.things` (`ThingContainer`), `Chara.Pick(Thing, bool, bool)`, `Chara.DropThing(Thing, int)` |
| money | `Card.GetCurrency(string id)`, `Card.ModCurrency(int a, string id)` |
| zone occupants | `Zone.FindChara(int uid)`, `Zone.FindChara(string id)`, `Zone.AddChara(string, Point)` |

`Chara` carries a `uid`; that is the handle `EntityId` should map to, not a name.

Beware name collisions when reading these: `Zone` is both an Elin class and
`System.Security.Policy.Zone`. `ApiDump` resolves the game's assembly first.

## Mod save data

```
ElinGameIOPropertyAttribute : ElinGameIOEventAttribute
    string ChunkName { get; }
    void   Register(PropertyInfo property)

GameIO : EClass
    static void SaveFile(string path, object obj)
    static T    LoadFile<T>(string path)
```

Decorating a property with `[ElinGameIOProperty(chunkName)]` attaches it to the save as a named
chunk. That is where `WorldStateSerializer` output belongs — no separate sidecar file, and the
procedural world travels with the save it belongs to.

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
- Drama choice injection — not yet examined.
- Crime witness hooks (`Point.TryWitnessCrime` and friends) — not yet examined.
- Whether `[ElinGameIOProperty]` works on a type the base game has never seen.
