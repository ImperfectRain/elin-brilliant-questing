using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Consequences;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
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
        private ActionRegistry _actions;
        private DramaChoiceProjector _drama;

        private bool _live;
        private ConfigEntry<bool> _stageTestScenario;

        private void Awake()
        {
            _log = Logger;

            // Off by default and deliberately awkward to turn on. It spawns three people into the
            // player's current zone and moves their affinity, which is not something to do to a
            // save by accident.
            _stageTestScenario = Config.Bind(
                "Testing",
                "StageScenarioOnLoad",
                false,
                "Stage the three-NPC theft scenario into the current zone on the next load, and "
                + "offer its verbs through dialogue. Writes to the save: spawns Charas and creates "
                + "procedural world state. Use a throwaway save. Runs once per load, and only when "
                + "no thread exists yet.");

            // Elin publishes its own lifecycle. Subscribing to it beats both polling in Update and
            // Harmony-patching the load path: it is the same route the game's bundled Scripting
            // Kit uses for exactly this job, so it is as stable as anything in Early Access gets.
            BaseModManager.SubscribeEvent<GameIOContext>(EVENT.PostLoad, OnPostLoad);
            BaseModManager.SubscribeEvent<GameIOContext>(EVENT.PreSave, OnPreSave);
            BaseModManager.SubscribeEvent(EVENT.NewGame, OnNewGame);
            DramaChoiceProjector.Install(_log);

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

            _world = Load(context);
            _bindings.BindSavedRefs(_world);
            _stager = new ElinSituationStager(_bindings, _log, _world);

            EntityId playerId = EntityId.Parse("npc_player");
            _vanilla.BindPlayer(playerId);
            _vanilla.DetectCapabilities();

            _checks = new ElinCheckResolver(_bindings, new VanillaStyleCheckResolver(_vanilla), _log);
            _actions = StandardActions.CreateRegistry();
            _drama = new DramaChoiceProjector(_world, _vanilla, _bindings, _checks, _actions, _log);
            DramaChoiceProjector.Current = _drama;

            _consequences = new ConsequenceEngine(_world, _vanilla);
            _consequences.Attach();

            _log.LogInfo("Simulation attached: " + _world.Registry.Npcs.Count + " people, "
                         + _world.Ledger.Count + " events, " + _world.Threads.Count + " threads.");

            ReportPlayerState();
            MaybeStageTestScenario();
        }

        /// <summary>
        /// Reads the player through the adapter and prints what came back.
        ///
        /// This is the read half of Gate A, and it is deliberately noisy on first attach: numbers
        /// that match the character sheet prove the whole chain - alias to element id to
        /// ElementContainer - rather than merely proving the calls did not throw.
        /// </summary>
        private void ReportPlayerState()
        {
            EntityId me = _vanilla.PlayerId;
            _log.LogInfo("player: " + (EClass.pc?.Name ?? "?") + "  level " + _vanilla.GetLevel(me)
                         + "  karma " + _vanilla.Karma + "  fame " + _vanilla.Fame
                         + "  " + _vanilla.GetMoney(me) + " orens"
                         + "  " + _vanilla.GetInventory(me).Count + " items");

            _log.LogInfo("attributes: STR " + _vanilla.GetAttribute(me, VanillaAttribute.Strength)
                         + "  END " + _vanilla.GetAttribute(me, VanillaAttribute.Endurance)
                         + "  DEX " + _vanilla.GetAttribute(me, VanillaAttribute.Dexterity)
                         + "  PER " + _vanilla.GetAttribute(me, VanillaAttribute.Perception)
                         + "  LER " + _vanilla.GetAttribute(me, VanillaAttribute.Learning)
                         + "  WIL " + _vanilla.GetAttribute(me, VanillaAttribute.Will)
                         + "  MAG " + _vanilla.GetAttribute(me, VanillaAttribute.Magic)
                         + "  CHA " + _vanilla.GetAttribute(me, VanillaAttribute.Charisma));

            _log.LogInfo("skills: negotiation " + _vanilla.GetSkill(me, VanillaSkill.Negotiation)
                         + "  stealth " + _vanilla.GetSkill(me, VanillaSkill.Stealth)
                         + "  pickpocket " + _vanilla.GetSkill(me, VanillaSkill.Pickpocket)
                         + "  spotHidden " + _vanilla.GetSkill(me, VanillaSkill.SpotHidden)
                         + "  literacy " + _vanilla.GetSkill(me, VanillaSkill.Literacy)
                         + "  appraising " + _vanilla.GetSkill(me, VanillaSkill.Appraising));

            _log.LogInfo("standing: deity '" + _vanilla.GetWorshippedDeity(me) + "'  piety "
                         + _vanilla.GetPiety(me)
                         + "  guilds F/M/T/Me " + _vanilla.IsGuildMember(GuildId.Fighters)
                         + "/" + _vanilla.IsGuildMember(GuildId.Mages)
                         + "/" + _vanilla.IsGuildMember(GuildId.Thieves)
                         + "/" + _vanilla.IsGuildMember(GuildId.Merchants)
                         + "  influence " + _vanilla.GetInfluence(EntityId.None)
                         + "  contribution " + _vanilla.GetContribution());
        }

        /// <summary>
        /// Runs the in-game scenario when the config asks for it, the world is still empty, and
        /// the adapter can actually read the stats the scenario depends on. Any of those missing
        /// is a reason to say so rather than half-stage something.
        /// </summary>
        private void MaybeStageTestScenario()
        {
            if (_stageTestScenario == null || !_stageTestScenario.Value)
            {
                return;
            }

            if (_world.Threads.Count > 0)
            {
                _log.LogInfo("Test scenario skipped: this world already has "
                             + _world.Threads.Count + " thread(s). Use a fresh save.");
                return;
            }

            if (!_vanilla.Supports(VanillaCapability.ReadAttributes) || !_vanilla.Supports(VanillaCapability.ReadSkills))
            {
                _log.LogWarning("Test scenario skipped: attributes or skills are unavailable, so every "
                                + "check would read zero and the result would mean nothing.");
                return;
            }

            try
            {
                new ProceduralQuestTest(_world, _vanilla, _stager, _checks, _actions, _log).Run();
            }
            catch (Exception ex)
            {
                _log.LogError("Test scenario failed: " + ex);
            }
        }

        private void End()
        {
            _bindings?.Clear();
            if (DramaChoiceProjector.Current == _drama)
            {
                DramaChoiceProjector.Current = null;
            }

            _drama = null;
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
