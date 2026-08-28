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

A consequence worth knowing: the merged assembly sees Elin's global namespace, so a Core type
sharing a name with a game type becomes ambiguous. `Goal` was renamed `NpcGoal` for exactly this
reason. Anything added to Core should avoid the game's very generic names - `Zone`, `Map`, `World`,
`Check`, `Element`.

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

## What the plugin does today

- Resolves element aliases and reports which vanilla capabilities are genuinely available.
- Implements `IVanillaState` against live objects: attributes, skills, level, affinity, Karma,
  fame, Influence, guild membership, deity, money, inventory, item transfers, zone occupants.
- Resolves checks through vanilla `Check` where a matching source row exists, and through the
  portable resolver where it does not. Both paths produce the same four outcomes.
- Stages generated characters and items as ordinary Chara and Thing objects.
- Persists the whole procedural world into the save as the `brilliantQuesting` chunk.

## What it does not do yet

No situation is generated in game, and no dialogue is presented — the verb library has no UI to be
offered through. Drama choice injection and the crime-witness hooks are still unexamined. What
exists is the seam, proven to compile against the real assemblies; none of it has been run inside
Elin yet, which is the next thing to do and the only thing that can confirm any of it.
