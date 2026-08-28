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
mscorlib.dll             <- only needed by tools/ApiDump
Newtonsoft.Json.dll      <- only needed by tools/ApiDump
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

Copy `BrilliantQuesting.Plugin.dll`, `BrilliantQuesting.Core.dll` and `package.xml` into a folder
under the game's `Package\` directory. Elin's Package Chainloader picks it up from there; nothing
goes in `BepInEx\plugins`.

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
