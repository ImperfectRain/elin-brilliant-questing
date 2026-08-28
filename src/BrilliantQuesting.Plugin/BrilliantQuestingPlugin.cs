using System;
using BepInEx;
using BepInEx.Logging;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Consequences;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Plugin
{
    internal static class ModInfo
    {
        internal const string Guid = "elin.brilliant.questing";
        internal const string Name = "Brilliant Questing";
        internal const string Version = "0.1.0";

        /// <summary>Name of the chunk this mod's world state occupies inside an Elin save.</summary>
        internal const string SaveChunk = "brilliantQuesting";
    }

    /// <summary>
    /// Entry point. Deliberately thin: it wires the simulation to the game and then gets out of
    /// the way. All the interesting behaviour lives in BrilliantQuesting.Core, which has never
    /// heard of Elin and is covered by tests that run without it.
    /// </summary>
    [BepInPlugin(ModInfo.Guid, ModInfo.Name, ModInfo.Version)]
    public class BrilliantQuestingPlugin : BaseUnityPlugin
    {
        private static ManualLogSource _log;

        private ElinBindings _bindings;
        private ElinVanillaState _vanilla;
        private ElinCheckResolver _checks;
        private ElinSituationStager _stager;
        private NarrativeWorldState _world;
        private ConsequenceEngine _consequences;

        private bool _live;

        /// <summary>
        /// The procedural world, serialised, living inside the player's save as a named chunk.
        ///
        /// This is why it is a chunk rather than a file beside the save: a world model that can
        /// desync from the save it describes is worse than no world model. Roll back a save and
        /// the history rolls back with it.
        /// </summary>
        [ElinGameIOProperty(ModInfo.SaveChunk)]
        public static string SavedWorld { get; set; }

        private void Awake()
        {
            _log = Logger;
            _log.LogInfo(ModInfo.Name + " " + ModInfo.Version + " loaded. Waiting for a game.");
        }

        private void Update()
        {
            bool gameIsLive = EClass.game != null && EClass.pc != null && EClass.sources != null;

            if (gameIsLive && !_live)
            {
                try
                {
                    Begin();
                    _live = true;
                }
                catch (Exception ex)
                {
                    // A broken procedural layer must never take the player's game with it.
                    _log.LogError("Failed to start: " + ex);
                    enabled = false;
                }
            }
            else if (!gameIsLive && _live)
            {
                End();
                _live = false;
            }
        }

        /// <summary>
        /// Lazy start rather than a patch on the game's own load path. It costs one comparison a
        /// frame and it cannot break when Early Access renames a method.
        /// </summary>
        private void Begin()
        {
            ElementAliases.Resolve(_log);

            _bindings = new ElinBindings();
            _vanilla = new ElinVanillaState(_bindings, _log);
            _stager = new ElinSituationStager(_bindings, _log);

            _world = Load();

            EntityId playerId = EntityId.Parse("npc_player");
            _vanilla.BindPlayer(playerId);
            _vanilla.DetectCapabilities();

            _checks = new ElinCheckResolver(_bindings, new VanillaStyleCheckResolver(_vanilla), _log);

            _consequences = new ConsequenceEngine(_world, _vanilla);
            _consequences.Attach();

            _log.LogInfo("Simulation attached: " + _world.Registry.Npcs.Count + " people, "
                         + _world.Ledger.Count + " events, " + _world.Threads.Count + " threads.");
        }

        private void End()
        {
            Persist();
            _bindings?.Clear();
            _world = null;
            _log.LogInfo("Simulation detached.");
        }

        private NarrativeWorldState Load()
        {
            if (string.IsNullOrEmpty(SavedWorld))
            {
                ulong seed = (ulong)(EClass.game?.seed ?? Environment.TickCount);
                _log.LogInfo("No saved world; starting a new one from seed " + seed + ".");
                return new NarrativeWorldState(seed);
            }

            try
            {
                return WorldStateSerializer.Load(SavedWorld);
            }
            catch (Exception ex)
            {
                // Never silently discard a player's history. Keep the raw text so it can be
                // recovered by hand, and carry on with an empty world rather than refusing to run.
                _log.LogError("Saved world could not be read (" + ex.Message + "). Keeping the raw "
                              + "chunk; starting empty for this session.");
                return new NarrativeWorldState((ulong)(EClass.game?.seed ?? 0));
            }
        }

        private void Persist()
        {
            if (_world == null)
            {
                return;
            }

            try
            {
                SavedWorld = WorldStateSerializer.Save(_world, indented: false);
            }
            catch (Exception ex)
            {
                _log.LogError("Could not serialise the world: " + ex);
            }
        }

        private void OnDestroy()
        {
            if (_live)
            {
                End();
            }
        }
    }
}
