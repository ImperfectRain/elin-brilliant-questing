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
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Situations;
using BrilliantQuesting.Threads;
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
        private ThreadEngine _threads;

        private bool _live;
        private ConfigEntry<bool> _stageTestScenario;
        private ConfigEntry<bool> _gatherPrototypeNpcs;
        private ConfigEntry<bool> _explainInDialogue;

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

            _gatherPrototypeNpcs = Config.Bind(
                "Testing",
                "GatherPrototypeNpcsNearPlayer",
                false,
                "Move live NPCs participating in the current petty-theft prototype near the player "
                + "on load. Relocates characters in the loaded save, so it is off by default and "
                + "is a playtest aid for a throwaway save, not a feature.");

            _explainInDialogue = Config.Bind(
                "Debug",
                "ExplainInDialogue",
                false,
                "Add a 'why?' option to Brilliant Questing dialogue that writes the full "
                + "explanation - why the situation exists, who knows what, why each option is or "
                + "is not offered, and which check it rolls - to BepInEx/LogOutput.log. Reads "
                + "only; it changes nothing in the world.");

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
            ProceduralCheckRows.Install(_log);

            _bindings = new ElinBindings();
            _vanilla = new ElinVanillaState(_bindings, _log);

            _world = Load(context);
            _bindings.BindSavedRefs(_world, _log);
            _stager = new ElinSituationStager(_bindings, _log, _world);

            EntityId playerId = EntityId.Parse("npc_player");
            _vanilla.BindPlayer(playerId);
            _vanilla.DetectCapabilities();
            RegisterPlayer(playerId);

            _checks = new ElinCheckResolver(_bindings, new VanillaStyleCheckResolver(_vanilla), _log);
            _actions = StandardActions.CreateRegistry();
            _drama = new DramaChoiceProjector(_world, _vanilla, _bindings, _checks, _actions, _log)
            {
                ExplainInDialogue = _explainInDialogue != null && _explainInDialogue.Value
            };
            DramaChoiceProjector.Current = _drama;

            _threads = new ThreadEngine();
            _threads.Register(
                PettyTheftSituation.ArchetypeId,
                new PettyTheftEscalation(_vanilla, new RumorSystem(_world.Knowledge, _world.Ledger, _world.Ids)));
            _drama.AdvanceThreads = AdvanceThreads;

            _consequences = new ConsequenceEngine(_world, _vanilla);
            _consequences.Attach();

            _log.LogInfo("Simulation attached: " + _world.Registry.Npcs.Count + " people, "
                         + _world.Ledger.Count + " events, " + _world.Threads.Count + " threads.");

            EnsurePrototypeEvidenceExists();
            AdvanceThreads();
            GatherPrototypeParticipantsNearPlayer();
            ReportPlayerState();
            ReportProceduralParticipants();
            AnnouncePrototypeObjective();
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
        /// Moves live threads forward to the current in-game date.
        ///
        /// ThreadEngine has always known how to do this - fire each step once, in order, and let
        /// a thread go dormant when it runs out - and nothing in the plugin had ever called it, so
        /// in a real save the staged theft sat at day zero however many days passed. The first
        /// playtest log showed every milestone unfired and it read like a short session rather
        /// than like a system that was never wired up.
        ///
        /// It ticks on the two hooks that are known to work: loading a save, which catches
        /// everything owed since the last one, and opening a Brilliant Questing conversation, so
        /// what the player is shown is current. That is not a clock. A real heartbeat wants
        /// EVENT.ActPerformed, which BQ-014 has to observe running before anything should depend
        /// on it - a milestone that fires late is a smaller problem than one that fires from an
        /// event nobody has watched behave.
        /// </summary>
        private void AdvanceThreads()
        {
            if (_world == null || _threads == null)
            {
                return;
            }

            try
            {
                if (_threads.Advance(_world, _vanilla.Now) == 0)
                {
                    return;
                }

                foreach (string applied in _threads.LastApplied)
                {
                    _log.LogInfo("Thread escalated: " + applied + " at " + _vanilla.Now + ".");
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning("Thread escalation skipped after an exception: " + ex.Message);
            }
        }

        /// <summary>
        /// Puts the player into the entity registry.
        ///
        /// The player is a character in the graph like anyone else - they can be lied to,
        /// remembered, believed things about, and owed favours. `TheftLaboratory` has always
        /// registered them, so every headless test ran with a player in the graph and nothing
        /// caught that the plugin never did the same. The first live run showed what that costs:
        /// the inspector answered "what the player knows" with `npc_player is not a known
        /// character`, and every log line naming the player printed the raw id instead of a name.
        ///
        /// The name is refreshed from the live character each load, because the player can rename
        /// themselves; identity stays the EntityId, which is the whole point of the registry.
        /// </summary>
        private void RegisterPlayer(EntityId playerId)
        {
            string name = EClass.pc?.Name;
            if (string.IsNullOrEmpty(name))
            {
                name = "You";
            }

            NarrativeNpc player = _world.Registry.GetNpc(playerId);
            if (player == null)
            {
                player = _world.Registry.Add(new NarrativeNpc(playerId, name)
                {
                    Importance = NarrativeImportance.Major
                });
                _log.LogInfo("Registered the player in the world as " + name + " [" + playerId + "].");
            }
            else
            {
                player.Name = name;
            }

            Chara pc = EClass.pc;
            if (pc != null)
            {
                player.VanillaCharaRef = pc.uid.ToString();
            }
        }

        private void ReportProceduralParticipants()
        {
            if (_world.Registry.Npcs.Count == 0)
            {
                return;
            }

            _log.LogInfo("procedural people in this save:");
            foreach (NarrativeNpc npc in _world.Registry.Npcs.Values)
            {
                Chara chara = _bindings.ResolveChara(npc.Id);
                if (chara == null)
                {
                    _log.LogInfo("  " + npc.Name + " [" + npc.Id + "] is not bound to a loaded Chara.");
                    continue;
                }

                Zone zone = chara.currentZone ?? EClass._zone;
                string zoneText = zone == null ? "unknown zone" : "zone_" + zone.uid;
                string relative = DescribeRelativeToPlayer(chara);
                _log.LogInfo("  " + npc.Name + " [" + npc.Id + "] uid " + chara.uid
                             + " at " + zoneText + " pos " + chara.pos + relative);
            }
        }

        private void GatherPrototypeParticipantsNearPlayer()
        {
            if (_gatherPrototypeNpcs == null || !_gatherPrototypeNpcs.Value
                                             || EClass.pc?.pos == null || EClass._zone == null)
            {
                return;
            }

            NarrativeThread thread = FindLivePettyTheftThread();
            if (thread == null)
            {
                return;
            }

            int slot = 0;
            foreach (EntityId participant in thread.ParticipantIds)
            {
                Chara chara = _bindings.ResolveChara(participant);
                if (chara == null || chara == EClass.pc || chara.currentZone != EClass._zone)
                {
                    continue;
                }

                Point destination = FindNearbyPrototypeSpot(slot++);
                if (destination == null)
                {
                    _log.LogWarning("Could not find a nearby playtest spot for " + chara.Name + ".");
                    continue;
                }

                try
                {
                    chara.MoveByForce(destination, null, false);
                    _log.LogInfo("Gathered prototype NPC " + chara.Name + " near player at " + destination + ".");
                }
                catch (Exception ex)
                {
                    _log.LogWarning("Could not gather prototype NPC " + chara.Name + ": " + ex.Message);
                }
            }
        }

        private NarrativeThread FindLivePettyTheftThread()
        {
            for (int i = 0; i < _world.Threads.Count; i++)
            {
                NarrativeThread thread = _world.Threads[i];
                if (thread.ArchetypeId == "petty_theft" && thread.IsLive)
                {
                    return thread;
                }
            }

            return null;
        }

        private void EnsurePrototypeEvidenceExists()
        {
            NarrativeThread thread = FindLivePettyTheftThread();
            if (thread == null)
            {
                return;
            }

            for (int i = 0; i < thread.FactIds.Count; i++)
            {
                Fact fact = _world.Knowledge.GetFact(thread.FactIds[i]);
                if (fact == null || fact.Predicate != FactPredicates.Stole || fact.Object.IsNone)
                {
                    continue;
                }

                if (_bindings.TryGetUid(fact.Object, out int itemUid) && EClass.game?.cards?.Find(itemUid) != null)
                {
                    return;
                }

                Chara thief = _bindings.ResolveChara(fact.Subject);
                if (thief == null || thief.isDead)
                {
                    RepairMissingEvidenceNearPlayer(fact);
                    return;
                }

                ItemDescriptor evidence = new ItemDescriptor(
                    fact.Object,
                    string.IsNullOrEmpty(fact.Value) ? "stolen ring" : fact.Value,
                    "jewelry",
                    400,
                    "ring");
                _stager.StageItem(fact.Subject, evidence);
                _log.LogInfo("Repaired missing prototype evidence '" + evidence.Name + "' on "
                             + _world.Registry.NameOf(fact.Subject) + ".");
                return;
            }
        }

        private void RepairMissingEvidenceNearPlayer(Fact fact)
        {
            if (EClass._zone == null || EClass.pc?.pos == null)
            {
                _log.LogWarning("Prototype evidence '" + fact.Value + "' is missing, but there is no loaded zone to repair it in.");
                return;
            }

            Thing thing;
            try
            {
                thing = ThingGen.Create("ring", -1, 4);
            }
            catch (Exception ex)
            {
                _log.LogWarning("Could not repair missing prototype evidence '" + fact.Value + "': " + ex.Message);
                return;
            }

            if (thing == null)
            {
                _log.LogWarning("Could not repair missing prototype evidence '" + fact.Value + "': ThingGen returned nothing.");
                return;
            }

            if (!string.IsNullOrEmpty(fact.Value))
            {
                thing.c_altName = fact.Value;
            }

            Point spot = FindNearbyPrototypeSpot(8) ?? EClass.pc.pos;
            EClass._zone.AddCard(thing, spot);
            _bindings.Bind(fact.Object, thing.uid);
            _log.LogInfo("Repaired missing prototype evidence '" + thing.Name
                         + "' as a loose item near the player at " + spot + ".");
        }

        private static Point FindNearbyPrototypeSpot(int slot)
        {
            Point origin = EClass.pc?.pos;
            if (origin == null)
            {
                return null;
            }

            int[,] offsets =
            {
                { 1, 0 },
                { -1, 0 },
                { 0, 1 },
                { 0, -1 },
                { 1, 1 },
                { -1, 1 },
                { 1, -1 },
                { -1, -1 },
                { 2, 0 },
                { -2, 0 },
                { 0, 2 },
                { 0, -2 }
            };

            int length = offsets.GetLength(0);
            for (int i = 0; i < length; i++)
            {
                int index = (slot + i) % length;
                Point point = new Point(origin.x + offsets[index, 0], origin.z + offsets[index, 1]);
                if (point.IsInBounds && !point.IsBlocked && !point.HasChara)
                {
                    return point;
                }
            }

            return origin.GetNearestPoint(false, false, true, false, 4);
        }

        private static string DescribeRelativeToPlayer(Chara chara)
        {
            Point player = EClass.pc?.pos;
            if (player == null || chara?.pos == null)
            {
                return string.Empty;
            }

            int dx = chara.pos.x - player.x;
            int dz = chara.pos.z - player.z;
            return "  player offset (" + dx + " / " + dz + "), distance " + chara.pos.Distance(player);
        }

        private void AnnouncePrototypeObjective()
        {
            NarrativeThread thread = FindLivePettyTheftThread();
            if (thread == null || thread.ParticipantIds.Count == 0)
            {
                return;
            }

            string names = string.Empty;
            for (int i = 0; i < thread.ParticipantIds.Count; i++)
            {
                if (i > 0)
                {
                    names += i == thread.ParticipantIds.Count - 1 ? " or " : ", ";
                }

                names += _world.Registry.NameOf(thread.ParticipantIds[i]);
            }

            string question = thread.OpenQuestions.Count == 0 ? "Find out what happened." : thread.OpenQuestions[0];
            string message = "Brilliant Questing: a local theft is active. Talk to "
                             + names + ". " + question;
            Msg.SayRaw(message);
            _log.LogInfo(message);
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
            _threads = null;
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
