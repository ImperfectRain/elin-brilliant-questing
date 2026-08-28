> **The task described here has landed.** Drama projection is implemented. This brief is kept
> because its "traps this project already hit" section still applies to every new contributor, and
> because it records what the project looked like immediately before that work. For what to do next,
> see [`implementation-roadmap.md`](implementation-roadmap.md).

# Handoff: make the procedural scenario playable through dialogue

For an agent continuing this work in a fresh session. Everything referenced here is committed on
`claude/elin-procedural-narrative-tkctw9`.

## Where the project stands

The simulation is done and tested (46 tests, `dotnet test`). The Elin adapter is written and
**verified running in game**. What does not exist is any way for a player to *make* a choice: the
verbs resolve, but nothing presents them.

Confirmed working in a live game, not merely compiling:

- The plugin loads through Elin's Package Chainloader and attaches on `EVENT.PostLoad`.
- `GameIOContext.Save/Load` persists the world into the save's own `brilliantQuesting` chunk.
- The adapter reads real character state: level 32, karma 100, fame 1197, STR 52 … CHA 24,
  negotiation 58, deity `harvest`, piety 54, three of four guilds. All 23 element aliases resolve.
- `ProceduralQuestTest` stages the three-NPC theft into the live zone and plays three verbs.

Read these first, in order: `docs/README.md`, `docs/elin-api-notes.md` (authoritative on runtime
facts), `docs/design/master-design.md`, `docs/design/post-master-findings.md`.

## The task

Replace `ProceduralQuestTest`'s scripted sequence with real player choice, through Elin's Drama
system. Concretely: talking to a staged NPC should offer the verbs the world currently permits, and
choosing one should resolve it through the existing `ActionResolver` path.

## What you have to work with

`EVENT.DramaParseAction` is on `BaseModManager`'s event bus and is the intended hook — the same bus
already delivers `PostLoad`/`PreSave` for this mod, so the mechanism is proven. Start there rather
than with Harmony.

`Check.GetText(Chara p, Card tg, bool inDialog)` renders vanilla's own difficulty wording. Use it,
so procedural options describe difficulty in the game's voice rather than an invented vocabulary.

`ElinCheckResolver.DescribeDifficulty` already wraps it.

The option list already exists. `ActionRegistry.Discover(context, includeUnavailable: true)` returns
every verb with an `Availability` carrying a reason. Show the available ones; the reasons are for
the log, not the player.

## Rules that are not negotiable

These come from the master design and have already been enforced throughout the code:

1. **Drama presents; it never decides.** Authoritative state changes happen in the action library
   and `ConsequenceEngine`. A dialogue node must not write affinity, karma, facts or inventory.
2. **Hide an option only for impossibility, never for low odds.** A hopeless liar still sees "Lie";
   someone with nothing to lie about does not. `Availability.Impossible` versus `NotRelevant`
   already encodes this — do not add a skill threshold.
3. **Every check has four outcomes.** `CriticalPass`, `Pass`, `Fail`, `CriticalFail`. A critical
   failure must create a new problem, not print a refusal.
4. **No new stats.** Everything reads through `IVanillaState`. If you need a value the interface
   lacks, add it to the interface and implement it in both `ElinVanillaState` and
   `SandboxVanillaState`, so the headless tests keep working.
5. **Core stays Elin-free.** `src/BrilliantQuesting.Core` must never reference Elin, BepInEx or
   Unity. Presentation code belongs in `src/BrilliantQuesting.Plugin`.

## Traps this project already hit

Each of these cost a round trip. Do not rediscover them.

- **`package.xml`'s `<version>` is a compatibility number, not a release version.** Below
  `BaseCore.versionMod`, Elin drops the package with a `continue` that runs before any logging —
  the log is byte-identical to the mod not being installed. Keep it at `0.23.326` or higher.
- **Ship one DLL.** Core's sources compile *into* the plugin assembly. A sibling assembly the
  chainloader cannot resolve makes the package report zero plugins, silently.
- **The merged assembly sees Elin's global namespace.** `Goal` had to become `NpcGoal`. Avoid
  `Zone`, `Map`, `World`, `Check`, `Element`, `Faction`, `Religion`, `Quest` for new Core types.
- **Element aliases are data, not code.** They cannot be read from `Elin.dll`. The verified table is
  `docs/elin-element-aliases.md`. Look them up via `SourceData.alias`, never `GetRow(string)`.
- **`Player.expInfluence` is not town Influence.** Influence is a currency:
  `GetCurrency("influence")`. A plausible API returning a plausible wrong number is the failure mode
  to watch for — check values against a character sheet you can see.

## Building and testing

`lib/` is gitignored and must be populated from a local Elin install — see `docs/plugin-build.md`
for the exact file list. Then:

```bash
dotnet test                                                        # 46 tests, no game needed
dotnet build src/BrilliantQuesting.Plugin/BrilliantQuesting.Plugin.csproj -c Release
dotnet run --project tools/ApiDump -- --type Drama                 # read the API, do not guess
```

`tools/ApiDump` prints the game's public surface without executing it. Every vanilla call in the
adapter was chosen by reading its output; use it before writing any new one. For method bodies,
`ilspycmd` works (`DOTNET_ROLL_FORWARD=LatestMajor`), and reading `ModManager.ActivatePackages` is
what solved the silent-load bug that three rounds of guessing did not.

## Definition of done

Talking to the staged victim in game offers a short list of verbs; picking one rolls a check whose
difficulty is described in vanilla's wording; the outcome changes affinity, knowledge or inventory
through the existing consequence path; the log explains the whole thing; and saving and reloading
brings the result back out of the chunk unchanged.
