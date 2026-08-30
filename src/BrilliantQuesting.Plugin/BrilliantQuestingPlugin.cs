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
        private ElinActionObserver _actionObserver;
        private RumorCirculation _gossip;
        private AmbientTalk _ambient;
        private AbsenceLifecycle _absences;
        private long _lastAdvancedDay = long.MinValue;
        private long _lastAmbientCheck = long.MinValue;
        private EntityId _lastReconciledZone;

        private bool _live;
        private ConfigEntry<bool> _stageTestScenario;
        private ConfigEntry<bool> _gatherPrototypeNpcs;
        private ConfigEntry<bool> _explainInDialogue;
        private ConfigEntry<bool> _offscreenAbsence;

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

            // The roadmap's own condition for BQ-032: do not ship it enabled until it has survived
            // a deliberately adversarial test on a save nobody minds losing. Off means the
            // capability probe reports MoveCharaBetweenZones unsupported, so a physical absence is
            // refused before anything is written and situations fall back to the service grade,
            // which touches nothing in the game.
            _offscreenAbsence = Config.Bind(
                "Testing",
                "AllowOffscreenAbsence",
                false,
                "Let the simulation move a character to another zone to represent them being away, "
                + "and move them back when they return. Writes to the save: it changes where the "
                + "game keeps a person. Only ordinary citizens and characters this mod created are "
                + "eligible; story-critical, unique-service and unclassifiable characters are "
                + "refused either way. Use a throwaway save.");

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
            BaseModManager.SubscribeEvent<object>(EVENT.ActPerformed, OnActPerformed);
            DramaChoiceProjector.Install(_log);
            NativeJournalSurface.Install(_log);

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

        private void OnActPerformed(object payload)
        {
            if (!_live || _actionObserver == null)
            {
                return;
            }

            _actionObserver.Observe(payload);
            AdvanceThreadsIfTheDayTurned();
            ReconcileIfTheZoneChanged();
            LetSomebodyMentionSomething();
        }

        /// <summary>
        /// The narrative heartbeat.
        ///
        /// BQ-013 wired escalation to loading a save and to opening a Brilliant Questing
        /// conversation, which is not a clock: a player could go a fortnight without speaking to
        /// anyone in the cast and the theft would sit at day zero, then pay out five milestones at
        /// once when they finally did. Order was preserved, but nothing that happened in between
        /// ever got the chance to happen while it mattered.
        ///
        /// ActPerformed is now proven live - BQ-014 is reading real acts through it - so it can
        /// carry the tick. It fires constantly, so this only does the work when the calendar day
        /// actually turns; escalation is measured in days, and anything finer would be spending
        /// effort to arrive at the same answer.
        /// </summary>
        private void AdvanceThreadsIfTheDayTurned()
        {
            long today = _vanilla.Now.TotalDays;
            if (today == _lastAdvancedDay)
            {
                return;
            }

            _lastAdvancedDay = today;
            AdvanceThreads();
            CirculateRumors();
        }

        /// <summary>
        /// Makes the game agree with the absence ledger, at every point the game can have disagreed
        /// with it.
        ///
        /// Called on load, when the calendar day turns, and whenever the player has changed zone -
        /// which is the cheap way to notice a zone that was unloaded and rebuilt, and a citizen
        /// refresh along with it. It is safe to call more often than any of those: with nobody away
        /// it returns immediately, and with somebody away it costs one read each, so the zone check
        /// hangs off the same constant ActPerformed tick as everything else.
        /// </summary>
        private void ReconcileAbsences()
        {
            if (_world == null || _absences == null)
            {
                return;
            }

            try
            {
                AbsenceRound round = _absences.Reconcile();
                if (!round.DidAnything)
                {
                    return;
                }

                _log.LogInfo("Absences: " + round + ".");
                foreach (string note in round.Notes)
                {
                    _log.LogInfo("  " + note);
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning("Absence reconciliation skipped after an exception: " + ex.Message);
            }
        }

        /// <summary>
        /// Notices that the player has walked into a different zone. The one event that matters for
        /// absences and that Elin does not publish: a zone is repopulated when it is entered, and
        /// anybody the mod sent away can be standing in it again.
        /// </summary>
        private void ReconcileIfTheZoneChanged()
        {
            if (_world == null)
            {
                return;
            }

            EntityId here = _vanilla.GetZoneOf(_vanilla.PlayerId);
            if (here == _lastReconciledZone)
            {
                return;
            }

            _lastReconciledZone = here;
            ReconcileAbsences();
        }

        /// <summary>
        /// Lets the town talk.
        ///
        /// Called on the same day boundary as escalation, and once on load to collect whatever the
        /// calendar owes. It is safe to call more often than that - `RumorCirculation` keeps its
        /// own day counter on the world and does nothing twice - which is the point: a reload must
        /// not be a way to re-roll what the neighbours started saying.
        /// </summary>
        private void CirculateRumors()
        {
            if (_world == null || _gossip == null)
            {
                return;
            }

            try
            {
                RumorRound round = _gossip.Run(_world, _vanilla, _vanilla.Now);
                if (!round.DidAnything)
                {
                    return;
                }

                _log.LogInfo("Gossip: " + round.Tells + " retelling(s) and " + round.Routed
                             + " guild report(s) over " + round.DaysRun
                             + " day(s)" + (round.DaysOwed > round.DaysRun ? " (of " + round.DaysOwed + " owed)" : "")
                             + "; " + round.Faded + " belief(s) faded.");

                foreach (string note in round.Notes)
                {
                    _log.LogInfo("  " + note);
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning("Rumour circulation skipped after an exception: " + ex.Message);
            }
        }

        /// <summary>
        /// Lets somebody standing near the player mention what the town has been saying.
        ///
        /// The order is the whole of BQ-035's safety: the line is spoken first, and only a line
        /// that actually reached the player is allowed to teach them anything. A belief that
        /// arrived because the bark route was missing would be knowledge from nowhere, which is
        /// what standing rule 22 exists to stop.
        ///
        /// `AmbientTalk` keeps the in-game cooldown, on the world, so it survives a reload and a
        /// player cannot walk in and out of a market to empty it. The check here is a second,
        /// cheaper gate on a hot event: ActPerformed fires constantly, and scanning the zone's
        /// beliefs on every act would cost more as the save grows, for an answer that cannot
        /// change in a single step.
        /// </summary>
        private void LetSomebodyMentionSomething()
        {
            if (_world == null || _ambient == null)
            {
                return;
            }

            // Never subtract from the sentinel: on the first act, and on a clock that has gone
            // backwards, ask rather than overflow into silence.
            long minute = _vanilla.Now.TotalMinutes;
            if (_lastAmbientCheck != long.MinValue && minute >= _lastAmbientCheck && minute - _lastAmbientCheck < 15)
            {
                return;
            }

            _lastAmbientCheck = minute;

            try
            {
                SpokenRemark remark = _ambient.Next(_world, _vanilla, _vanilla.Now);
                if (remark == null || !ElinBark.Speak(_bindings, remark, _log))
                {
                    return;
                }

                bool took = _ambient.Deliver(_world, _vanilla, remark, _vanilla.Now);
                _log.LogInfo("Ambient: " + remark.SpeakerName + " mentioned " + remark.FactId
                             + (took ? "; the player now half-believes it." : "; it did not take."));
            }
            catch (Exception ex)
            {
                _log.LogWarning("Ambient remark skipped after an exception: " + ex.Message);
            }
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
            _vanilla = new ElinVanillaState(
                _bindings, _log, _offscreenAbsence != null && _offscreenAbsence.Value);

            _world = Load(context);
            NativeJournalSurface.Bind(_world, _vanilla);
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
            RumorSystem rumors = new RumorSystem(_world.Knowledge, _world.Ledger, _world.Ids);

            // One policy, shared. A story that garbles in the market and a thief who names
            // somebody in a back room have to produce the same claim, or the town ends up
            // holding two different beliefs about who took the ring.
            RumorDistortion distortion = new RumorDistortion();
            _threads.Register(
                PettyTheftSituation.ArchetypeId,
                new PettyTheftEscalation(_vanilla, rumors, distortion));
            _gossip = new RumorCirculation(rumors) { Distortion = distortion };
            _ambient = new AmbientTalk(rumors);
            _drama.AdvanceThreads = AdvanceThreads;

            // The asked half of the same route. One rumour layer serves both, so what somebody
            // will volunteer in the street and what they will say when asked cannot drift apart.
            _drama.News = new TownNews(rumors);
            _actionObserver = new ElinActionObserver(_world, _vanilla, _bindings, _log);
            ElinAuthorityRoles.RefreshAll(_world, _bindings, _log);

            _consequences = new ConsequenceEngine(_world, _vanilla);
            _consequences.Attach();

            // Built after the bindings are restored, because reconciling before the save's
            // identity map is back would ask the game about characters it cannot resolve yet.
            _absences = new AbsenceLifecycle(_world, _vanilla);

            _log.LogInfo("Simulation attached: " + _world.Registry.Npcs.Count + " people, "
                         + _world.Ledger.Count + " events, " + _world.Threads.Count + " threads.");

            EnsurePrototypeEvidenceExists();
            _lastAdvancedDay = _vanilla.Now.TotalDays;
            AdvanceThreads();
            CirculateRumors();

            // Loading a save is the first and worst of the three ways the game undoes an absence:
            // it puts everybody back where it last wrote them. Reconcile now, and take the zone the
            // player is standing in as the baseline, so the very next act is not a second pass over
            // the same answer.
            ReconcileAbsences();
            _lastReconciledZone = _vanilla.GetZoneOf(_vanilla.PlayerId);
            RegisterLocalVanillaActors(_lastReconciledZone);
            MaybeGenerateLocalSituation(_lastReconciledZone);
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

            ReportHomeState();
        }

        /// <summary>
        /// Prints the player's real Home, or says plainly that there is none.
        ///
        /// The read half of BQ-030, and the same shape as the attribute dump above: numbers that
        /// match the Home board prove the chain - branch, member list, alias, element container -
        /// rather than merely proving the calls did not throw. A datum this build would not answer
        /// prints as "?", never as zero, so the line cannot be mistaken for a measurement.
        /// </summary>
        private void ReportHomeState()
        {
            HomeState home = _vanilla.GetHomeState();
            if (home == null)
            {
                _log.LogInfo("home: none readable on this save.");
                return;
            }

            _log.LogInfo("home: " + home.Describe());
            foreach (HomeResident resident in home.Residents)
            {
                _log.LogInfo("  resident " + resident.Name + " [" + resident.Id + "]"
                             + (resident.HasJob ? " - " + resident.Job : " - no job read"));
            }
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

        private void RegisterLocalVanillaActors(EntityId zoneId)
        {
            if (zoneId.IsNone || EClass._map?.charas == null)
            {
                return;
            }

            int registered = 0;
            foreach (Chara chara in EClass._map.charas)
            {
                if (chara == null || chara.isDead || chara.IsPC)
                {
                    continue;
                }

                EntityId id = ElinBindings.MintCharaId(chara, _vanilla.PlayerId);
                if (id.IsNone)
                {
                    continue;
                }

                _bindings.Bind(id, chara.uid);
                NarrativeNpc npc = _world.Registry.GetNpc(id);
                if (npc == null)
                {
                    npc = _world.Registry.Add(new NarrativeNpc(id, chara.Name)
                    {
                        Occupation = "local",
                        HomeSiteId = zoneId
                    });
                    registered++;
                }
                else
                {
                    npc.Name = chara.Name;
                    if (npc.HomeSiteId.IsNone)
                    {
                        npc.HomeSiteId = zoneId;
                    }
                }

                npc.VanillaCharaRef = chara.uid.ToString();
            }

            if (registered > 0)
            {
                _log.LogInfo("Registered " + registered + " local vanilla actor(s) for situation generation.");
            }
        }

        /// <summary>
        /// Gives the settlement the save resumed in one chance to produce a situation from its own
        /// state.
        ///
        /// Bootstrap only, and knowingly so: this fires once, on attach, in whichever zone the
        /// player is standing in. The reactive triggers that would let any settlement produce
        /// something when its state actually changes are the director's work at BQ-099, not this
        /// step's, and pretending otherwise by firing on every zone entry would make generation a
        /// function of where the player walks. Guarding on "this world already has a thread" is what
        /// keeps a reload from being a way to roll for another one.
        /// </summary>
        private void MaybeGenerateLocalSituation(EntityId zoneId)
        {
            if (zoneId.IsNone || _world.Threads.Count > 0)
            {
                return;
            }

            if (!_vanilla.Supports(VanillaCapability.ReadInventory)
                || !_vanilla.Supports(VanillaCapability.TransferItems)
                || !_vanilla.Supports(VanillaCapability.ReadSkills)
                || !_vanilla.Supports(VanillaCapability.ReadAttributes))
            {
                _log.LogInfo("Local situation generation skipped: required vanilla reads/writes are unavailable.");
                return;
            }

            try
            {
                SettlementSituationGenerator generator = new SettlementSituationGenerator();
                SettlementSituationPlan plan = generator.Evaluate(_world, _vanilla, zoneId);
                for (int i = 0; i < plan.Suppressed.Count; i++)
                {
                    _log.LogInfo("  not repeated: " + plan.Suppressed[i].Reason);
                }

                if (plan.Candidates.Count == 0)
                {
                    _log.LogInfo("Local situation generation found no eligible pressure: "
                                 + string.Join("; ", plan.Profile.Features));
                    return;
                }

                // The plan that was read is the plan that is acted on. Evaluating a second time
                // inside TryGenerate meant a doubled pass over every inventory in the zone, and let
                // the causes reported here name a candidate other than the one actually built.
                PettyTheftSituation situation = generator.TryGenerate(_world, _vanilla, plan, zoneId, _vanilla.Now);
                if (situation == null)
                {
                    _log.LogInfo("Local situation generation found pressure, but vanilla refused the founding item transfer.");
                    return;
                }

                _log.LogInfo("Generated " + situation.Thread.ArchetypeId + " from local world state.");
                for (int i = 0; i < situation.Thread.GenerationCauses.Count; i++)
                {
                    _log.LogInfo("  cause: " + situation.Thread.GenerationCauses[i]);
                }
            }
            catch (Exception ex)
            {
                _log.LogError("Local situation generation failed: " + ex);
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

        /// <summary>
        /// The staged scenario's pointer at who to talk to.
        ///
        /// This used to be shown to everybody with a live theft in their town, and BQ-035 is the
        /// reason it no longer is: a message naming the participants and the open question is
        /// precisely the UI element announcing a situation that this step exists to do without,
        /// and it hands the player three names their character has not been told. The world says
        /// it now, in somebody's voice, when they are standing near somebody who is repeating it.
        ///
        /// It stays behind the staging switch because a playtester who deliberately staged the
        /// prototype into a throwaway save wants to be pointed at it. The log line is unconditional:
        /// a log is evidence, not a surface the player reads.
        /// </summary>
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
            if (_stageTestScenario != null && _stageTestScenario.Value)
            {
                Msg.SayRaw(message);
            }

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
            _actionObserver = null;
            _lastAdvancedDay = long.MinValue;
            _bindings?.Clear();
            if (DramaChoiceProjector.Current == _drama)
            {
                DramaChoiceProjector.Current = null;
            }

            _drama = null;
            NativeJournalSurface.Bind(null, null);
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
                _bindings?.WriteSavedRefs(_world);
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
