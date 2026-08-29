# Building and installing the plugin

`BrilliantQuesting.Plugin` is the only project that touches Elin. It is deliberately **not** in
`ElinBrilliantQuesting.sln`, so `dotnet build` and `dotnet test` at the repository root stay green
on a machine that has never seen the game. Build it explicitly:

```bash
dotnet build src/BrilliantQuesting.Plugin/BrilliantQuesting.Plugin.csproj
```

## Local references

Game and BepInEx assemblies are not ours to redistribute, so `lib/` is gitignored in full and has
to be populated from your own install. Copy these out of the Elin folder:

From `Elin_Data\Managed\` into `lib/Elin/`:

```
Elin.dll                 <- the game. Assembly-CSharp.dll is a 6 KB VFX stub, not this.
Plugins.BaseCore.dll
Plugins.Modding.dll
UnityEngine.dll
UnityEngine.CoreModule.dll
Newtonsoft.Json.dll
mscorlib.dll             <- only needed by tools/ApiDump
```

From `BepInEx\core\` into `lib/BepInEx/BepInEx/core/`:

```
BepInEx.Core.dll
BepInEx.Unity.dll
0Harmony.dll
```

Both paths can be overridden: `dotnet build ... -p:ElinLib=/path -p:BepInExLib=/path`. The build
fails with a readable message rather than a wall of missing-type errors if either is absent.

## Installing

Copy `BrilliantQuesting.Plugin.dll` and `package.xml` into a folder under the game's `Package\`
directory. Nothing goes in `BepInEx\plugins`.

`package.xml`'s `<version>` is a **compatibility number, not a release version**. Elin drops any
package whose version is below `BaseCore.versionMod`, with a `continue` that runs before any
logging - the resulting log is identical to one where the mod was never installed. Keep it at or
above the game's mod version; copy the number from a mod that currently loads.

Then **enable it in the game's Mods menu**. Dropping the folder in only gets the package discovered;
`BaseModPackage` distinguishes `installed` from `activated`, and the Scripting Kit filters on
`activated && !builtin`. Activation is what writes the trailing `,1` in `loadorder.txt`. Installing
is two steps, and the first log from a discovered-but-inactive package looks identical to one that
was never copied.

### One assembly, on purpose

The simulation is compiled into the plugin DLL rather than shipped beside it. Elin's Package
Chainloader scans a package folder for BepInPlugin types; if enumerating them needs a sibling
assembly it does not resolve, the type load fails and the package reports **zero plugins with no
error at all** - indistinguishable in the log from a package that contains nothing. Every working
example in `Package/` ships a single DLL.

A consequence worth knowing: the merged assembly sees Elin's global namespace, and **the game's
type wins**, so a Core type sharing a name with a game type is silently shadowed. Three renames so
far: `Goal` to `NpcGoal`, `Scene` to `NarrativeScene`, and `WorldInspector` to
`NarrativeInspector`. The last one is the instructive failure - it did not present as a name clash
at all, but as `'WorldInspector' does not contain a definition for 'Explain'`, because the compiler
had resolved the name to Elin's class and was reporting truthfully about that one. Anything added
to Core should avoid the game's very generic names: `Zone`, `Map`, `World`, `Check`, `Element`,
`Scene`, `Goal`, and anything ending in `Inspector`.

## Reading the game's API

`tools/ApiDump` prints the public surface of whatever is in `lib/Elin/` without executing any of
it, which is how every vanilla call in the adapter was chosen:

```bash
dotnet run --project tools/ApiDump -- --assemblies
dotnet run --project tools/ApiDump -- --find Check Guild
dotnet run --project tools/ApiDump -- --type Check
dotnet run --project tools/ApiDump -- --members Chara affinity
```

## First run: fix the element aliases

`ElementAliases.cs` maps the mod's stat vocabulary onto Elin element aliases, and **those strings
are unverified guesses**. Anything that fails to resolve is logged by name and its capability is
switched off, so a wrong guess costs a missing route rather than a silent zero that would quietly
make every check trivial.

Read `BepInEx\LogOutput.log` after the first launch. Correct the table, which is the only place in
the mod where those strings appear. `ElementAliases.DumpKnownAliases` writes every alias the game
knows, if the log is not enough.

## Configuration

The first launch writes `BepInEx/config/elin.brilliant.questing.cfg`. Three flags, **all off by
default**, and each one is off for a different reason:

| Flag | Section | Why it is off |
|---|---|---|
| `StageScenarioOnLoad` | `Testing` | Writes to the save: spawns Charas and creates procedural world state. Throwaway saves only. |
| `GatherPrototypeNpcsNearPlayer` | `Testing` | Relocates characters in the loaded save. A playtest aid, not a feature. |
| `ExplainInDialogue` | `Debug` | Adds a "why?" option to Brilliant Questing dialogue that writes the full BQ-012 report to the log. Reads only, changes nothing, but it is developer text. |

The Drama projector itself has no flag. It installs on `Awake` and its patches are live for every
player, so its safety boundary is scope rather than configuration: it reads or rewrites only
Elin's generic conversation (book `_chara`, step `main`), never authored dialogue, patching is
all-or-nothing with a diagnostic, and every callback is guarded.

## What the plugin does today

- Resolves element aliases and reports which vanilla capabilities are genuinely available.
- Implements `IVanillaState` against live objects: attributes, skills, level, affinity, Karma,
  fame, Influence, guild membership, deity, money, inventory, item transfers, zone occupants.
- Resolves checks through vanilla `Check` where a matching source row exists, and through the
  portable resolver where it does not. Both paths produce the same four outcomes.
- Stages generated characters and items as ordinary Chara and Thing objects.
- Persists the whole procedural world into the save as the `brilliantQuesting` chunk.
- Observes theft-like vanilla actions from `EVENT.ActPerformed` and appends `Theft` events with
  the real item id as evidence and witnesses derived from proximity, line of sight and stealth.
- Observes hostile vanilla acts as `Attacked` or `Killed`, so ordinary combat outcomes produce
  the same memories, affinity, Karma and Fame consequences as procedural actions.
- Stores proof as explicit links to physical evidence or witness testimony, and keeps those proof
  links across save/load so accusations can explain what backs them.
- Adds a `Report it` authority route for guard, guild and court-style NPCs. Physical proof,
  witness-backed proof, unprovable belief and rumor produce different outcomes.
- Compacts long-save memory with an explicit budget: repeated routine memories fold, old trivia
  decays, and Defining memories are preserved.
- Offers its verbs through ordinary Elin dialogue. Talking to a staged NPC adds Brilliant
  Questing options to the generic conversation, each labelled with vanilla's own difficulty
  wording, resolving through the action library and changing the world.

## What it does not do yet

No situation is **generated** by the world - the one that exists is staged by a config flag, and
generation arrives at BQ-039. Vanilla theft, combat/death outcomes and their witnesses are
observed, but there is no Home state, economy, sites, NPC autonomy or director, and one situation
archetype. Personality exists as nine decision weights and is not yet expressed in dialogue.

## Running the in-game scenario test

**Use a throwaway save.** This writes: it spawns three Charas into your current zone, transfers an
item between inventories, and changes vanilla affinity.

1. Launch once with the mod installed so BepInEx writes
   `BepInEx/config/elin.brilliant.questing.cfg`.
2. Set `StageScenarioOnLoad = true` under `[Testing]`.
3. Load the throwaway save.

It runs on `PostLoad`, once, and only when the world has no threads yet — so a save that already
ran it will not run it again. It also refuses to run when attributes or skills are unavailable,
because every check would read zero and the result would mean nothing.

The log then contains, in order: the generated truth and who knows it; the three staged NPCs read back through the adapter (level, affinity, PER, WIL, inventory
count); and every verb the world permits against each of them plus the reason for each it refuses.

The verbs are no longer played by the test. **Walk up to a staged NPC and talk to them** - the
options are in the conversation, and choosing one resolves it. Turn on
`GatherPrototypeNpcsNearPlayer` if you cannot find them; the log line
`procedural people in this save:` reports where each one is.

What to check in that output:

- The three NPCs report **real stats**, not zeros — the stager set them and the adapter can read them.
- **Solution families open** is 3 or more per target. Fewer means the situation is not offering
  genuinely different routes.
- The **blocked** list gives reasons of the right kind: "you cannot reveal something you do not
  know", not "your skill is too low".
- Talking to a staged NPC **offers Brilliant Questing options**, and the log says
  `Projected N Brilliant Questing option(s)`. Talking to anyone else, or opening a quest or shop
  conversation with a staged NPC, offers none and leaves the vanilla text alone.
- Choosing one changes something you can see, and affinity or inventory **differs** afterwards.
- Save, quit fully, reload: the thread, events and facts come back out of the chunk unchanged.
