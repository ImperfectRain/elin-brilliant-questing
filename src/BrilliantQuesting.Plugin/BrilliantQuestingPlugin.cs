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

        private void Awake()
        {
            _log = Logger;

            // Elin publishes its own lifecycle. Subscribing to it beats both polling in Update and
            // Harmony-patching the load path: it is the same route the game's bundled Scripting
            // Kit uses for exactly this job, so it is as stable as anything in Early Access gets.
            BaseModManager.SubscribeEvent<GameIOContext>(EVENT.PostLoad, OnPostLoad);
            BaseModManager.SubscribeEvent<GameIOContext>(EVENT.PreSave, OnPreSave);
            BaseModManager.SubscribeEvent(EVENT.NewGame, OnNewGame);

            _log.LogInfo(ModInfo.Name + " " + ModInfo.Version + " loaded. Waiting for a game.");
        }

        private void OnNewGame()
        {
            Restart(null);
        }

        private void OnPostLoad(GameIOContext context)
        {
            Restart(context);
        }

        private void OnPreSave(GameIOContext context)
        {
            Persist(context);
        }

        private void Restart(GameIOContext context)
        {
            try
            {
                Begin(context);
                _live = true;
            }
            catch (Exception ex)
            {
                // A broken procedural layer must never take the player's game with it.
                _log.LogError("Failed to start: " + ex);
                _live = false;
            }
        }

        private void Begin(GameIOContext context)
        {
            ElementAliases.Resolve(_log);

            _bindings = new ElinBindings();
            _vanilla = new ElinVanillaState(_bindings, _log);
            _stager = new ElinSituationStager(_bindings, _log);

            _world = Load(context);

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
            _bindings?.Clear();
            _world = null;
            _log.LogInfo("Simulation detached.");
        }

        /// <summary>
        /// Reads the procedural world out of the save's own chunk store.
        ///
        /// A chunk rather than a file beside the save: a world model that can desync from the save
        /// it describes is worse than no world model. Roll a save back and the history rolls back
        /// with it.
        /// </summary>
        private NarrativeWorldState Load(GameIOContext context)
        {
            string json = null;
            if (context != null && context.Load(ModInfo.SaveChunk, out string stored, null))
            {
                json = stored;
            }

            if (string.IsNullOrEmpty(json))
            {
                ulong seed = (ulong)(EClass.game?.seed ?? Environment.TickCount);
                _log.LogInfo("No saved world; starting a new one from seed " + seed + ".");
                return new NarrativeWorldState(seed);
            }

            try
            {
                return WorldStateSerializer.Load(json);
            }
            catch (Exception ex)
            {
                // Never silently discard a player's history. The chunk is left untouched so it can
                // be recovered by hand, and this session carries on with an empty world rather
                // than refusing to run.
                _log.LogError("Saved world could not be read (" + ex.Message + "). The chunk has "
                              + "been left alone; starting empty for this session.");
                return new NarrativeWorldState((ulong)(EClass.game?.seed ?? 0));
            }
        }

        private void Persist(GameIOContext context)
        {
            if (_world == null || context == null)
            {
                return;
            }

            try
            {
                context.Save(ModInfo.SaveChunk, WorldStateSerializer.Save(_world, indented: false), null);
                _log.LogInfo("Saved " + _world.Ledger.Count + " events into chunk '" + ModInfo.SaveChunk + "'.");
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
