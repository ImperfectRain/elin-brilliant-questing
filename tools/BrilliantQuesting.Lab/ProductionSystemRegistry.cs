using System;
using System.Collections.Generic;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Autonomy;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Consequences;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Memory;
using BrilliantQuesting.Relationships;
using BrilliantQuesting.Situations;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Lab
{
    public enum HarnessPhase
    {
        Initialize,
        Daily
    }

    public sealed class HarnessRuntime
    {
        public ConsequenceEngine Consequences { get; set; }
        public RumorSystem Rumors { get; set; }
        public RumorCirculation RumorCirculation { get; set; }
        public ThreadEngine Threads { get; set; }
        public SettlementSituationGenerator SettlementGenerator { get; set; }
        public AbsenceLifecycle Absences { get; set; }
        public OffScreenSchemes Schemes { get; set; }

        /// <summary>BQa-016. The production runner itself, not a Lab copy of its scheduling.</summary>
        public ProductionCycle Cycle { get; set; }

        public int CyclePasses { get; set; }
        public int CycleGoalChanges { get; set; }
        public int CycleIntentions { get; set; }
        public int CycleCommits { get; set; }
        public int RumorTells { get; set; }
        public int RumorRoutes { get; set; }
        public int ThreadEscalations { get; set; }
        public int GeneratedSituations { get; set; }

        /// <summary>BQ-115. Faces the settlement elected to keep bringing back.</summary>
        public int EarlyContacts { get; set; }
        public int OrganizationActions { get; set; }
        public int SchemeActions { get; set; }
        public int AbsenceReturns { get; set; }
        public int AbsenceEnforcements { get; set; }
        public int MemoriesCompacted { get; set; }
    }

    public sealed class ProductionSystemDescriptor
    {
        public ProductionSystemDescriptor(
            string id,
            HarnessPhase phase,
            Action<HarnessState, HarnessRuntime> run,
            string provenance,
            string limitation = "")
        {
            Id = id;
            Phase = phase;
            Run = run;
            Provenance = provenance;
            Limitation = limitation;
        }

        public string Id { get; }

        public HarnessPhase Phase { get; }

        public Action<HarnessState, HarnessRuntime> Run { get; }

        public string Provenance { get; }

        public string Limitation { get; }
    }

    public static class ProductionSystemRegistry
    {
        public static IReadOnlyList<ProductionSystemDescriptor> Descriptors()
        {
            return new[]
            {
                new ProductionSystemDescriptor("consequence_engine", HarnessPhase.Initialize, AttachConsequences, "production Core"),
                new ProductionSystemDescriptor("rumor_system", HarnessPhase.Initialize, BuildRumors, "production Core"),
                new ProductionSystemDescriptor("thread_engine", HarnessPhase.Initialize, BuildThreads, "production Core"),

                // BQa-016. Built here and rebuilt after a reload, exactly as the consequence
                // engine is, because the collector it owns is an event listener: attaching after
                // the load is what keeps restored history from being replayed into a work set.
                new ProductionSystemDescriptor(
                    "production_cycle",
                    HarnessPhase.Initialize,
                    BuildProductionCycle,
                    "production Core",
                    "Constructing and attaching writes no event, so coverage reads Available until a pass commits something."),
                // BQ-115 runs ahead of both generation passes, exactly as the plugin's attach path
                // orders them. The ordering is the step: faces have to be elected before there is a
                // situation for them to be cast into.
                new ProductionSystemDescriptor(
                    "early_contacts",
                    HarnessPhase.Daily,
                    EstablishEarlyContacts,
                    "production Core",
                    "Electing writes no event and no thread, so coverage reads Available rather than Exercised even on a pass that elected a full cast."),
                new ProductionSystemDescriptor("settlement_generation", HarnessPhase.Daily, GenerateSettlementSituation, "production Core"),
                new ProductionSystemDescriptor("home_resident_pressure", HarnessPhase.Daily, GenerateHomeResidentPressure, "production Core"),
                new ProductionSystemDescriptor("thread_lifecycle", HarnessPhase.Daily, ReviewThreadLifecycle, "production Core"),
                new ProductionSystemDescriptor("thread_escalation", HarnessPhase.Daily, AdvanceThreads, "production Core"),
                // BQa-016. The Lab calls the production runner; it does not reproduce its
                // scheduling. Ahead of the other autonomy owners so that a pass's committed
                // openings are already closed when the scheme pass looks at the same day.
                new ProductionSystemDescriptor(
                    "production_cycle_pass",
                    HarnessPhase.Daily,
                    AdvanceProductionCycle,
                    "production Core",
                    "Headless Core only: a pass here is no evidence that any Elin hook advances one (BQa-017); its enrolled organization half is BQa-020's and equally unobserved in a running game."),
                new ProductionSystemDescriptor("off_screen_schemes", HarnessPhase.Daily, AdvanceSchemes, "production Core"),
                new ProductionSystemDescriptor("absence_lifecycle", HarnessPhase.Daily, ReconcileAbsences, "production Core"),
                new ProductionSystemDescriptor("rumor_circulation", HarnessPhase.Daily, CirculateRumors, "production Core"),
                new ProductionSystemDescriptor("memory_compaction", HarnessPhase.Daily, CompactMemories, "production Core"),
                new ProductionSystemDescriptor("persistence_reload", HarnessPhase.Daily, ReloadCheckpoint, "production serializer")
            };
        }

        public static IReadOnlyList<HarnessCoverageEntry> PluginOnlyLimitations()
        {
            return new[]
            {
                new HarnessCoverageEntry("harmony_callbacks", HarnessCoverageState.PluginOnly, "Elin runtime", "EVENT hooks and game load callbacks require a loaded game."),
                new HarnessCoverageEntry("live_witness_los", HarnessCoverageState.PluginOnly, "Elin runtime", "Map LOS, stealth, and observer discovery are sampled in plugin code."),
                new HarnessCoverageEntry("unity_object_lifecycle", HarnessCoverageState.PluginOnly, "Elin runtime", "Chara, Thing, Zone creation/destruction and pathing are Unity/game side effects."),
                new HarnessCoverageEntry("native_dialogue_journal", HarnessCoverageState.PluginOnly, "Elin runtime", "Drama projection and native journal UI cannot be validated offline."),
                new HarnessCoverageEntry("actor_activity_snapshot", HarnessCoverageState.Future, "BQ-135", "Transient actor activity remains intentionally unavailable until the production seam exists."),
                new HarnessCoverageEntry("character_identity", HarnessCoverageState.PluginOnly, "Elin runtime", "The six identity facets come off SourceChara rows, the race row and trait subclasses; headless runs can only prove how an unread facet degrades."),
                new HarnessCoverageEntry("player_companions", HarnessCoverageState.PluginOnly, "Elin runtime", "The party half of the player's household is read off EClass.pc; headless runs author it, so only the Home-roll half and the lifecycle rules are proven offline.")
            };
        }

        public static bool IsEnabled(IntegrationHarnessConfig config, ProductionSystemDescriptor descriptor)
        {
            return !config.DisabledSystems.Contains(descriptor.Id);
        }

        private static void AttachConsequences(HarnessState state, HarnessRuntime runtime)
        {
            runtime.Consequences = new ConsequenceEngine(state.World, state.Vanilla);
            runtime.Consequences.Attach();
        }

        private static void BuildRumors(HarnessState state, HarnessRuntime runtime)
        {
            runtime.Rumors = new RumorSystem(state.World.Knowledge, state.World.Ledger, state.World.Ids);
            runtime.RumorCirculation = new RumorCirculation(runtime.Rumors);
        }

        private static void BuildThreads(HarnessState state, HarnessRuntime runtime)
        {
            runtime.Threads = new ThreadEngine();
            RumorSystem rumors = runtime.Rumors ?? new RumorSystem(state.World.Knowledge, state.World.Ledger, state.World.Ids);
            RumorDistortion distortion = new RumorDistortion();
            runtime.Threads.Register(PettyTheftSituation.ArchetypeId, new PettyTheftEscalation(state.Vanilla, rumors, distortion));
            runtime.Threads.Register(ShortageSituation.ArchetypeId, new ShortageEscalation(state.Vanilla));
            runtime.Threads.Register(HuntedWitnessSituation.ArchetypeId, new HuntedWitnessEscalation(state.Vanilla));
            runtime.Threads.Register(HomeResidentSituation.ArchetypeId, new HomeResidentEscalation());
        }

        private static void EstablishEarlyContacts(HarnessState state, HarnessRuntime runtime)
        {
            runtime.EarlyContacts = EarlyContacts
                .Establish(state.World, state.Vanilla, state.PrimaryZoneId)
                .Count;
        }

        private static void GenerateSettlementSituation(HarnessState state, HarnessRuntime runtime)
        {
            if (state.Vanilla.Now.TotalDays % 7 != 0 || state.PrimaryZoneId.IsNone)
            {
                return;
            }

            runtime.SettlementGenerator ??= new SettlementSituationGenerator();
            PettyTheftSituation situation = runtime.SettlementGenerator.TryGenerate(
                state.World,
                state.Vanilla,
                state.PrimaryZoneId,
                state.Vanilla.Now);
            if (situation != null)
            {
                runtime.GeneratedSituations++;
            }
        }

        private static void GenerateHomeResidentPressure(HarnessState state, HarnessRuntime runtime)
        {
            if (state.Vanilla.Now.TotalDays % 7 == 0
                && HomeResidentSituation.TryGenerate(state.World, state.Vanilla, state.Vanilla.Now) != null)
            {
                runtime.GeneratedSituations++;
            }
        }

        private static void ReviewThreadLifecycle(HarnessState state, HarnessRuntime runtime)
        {
            ThreadLifecycle.Review(state.World, state.Vanilla, state.Vanilla.Now);
        }

        private static void AdvanceThreads(HarnessState state, HarnessRuntime runtime)
        {
            if (runtime.Threads != null)
            {
                runtime.ThreadEscalations += runtime.Threads.Advance(state.World, state.Vanilla.Now);
            }
        }

        private static void BuildProductionCycle(HarnessState state, HarnessRuntime runtime)
        {
            runtime.Cycle = new ProductionCycle(
                state.World,
                state.Vanilla,
                new VanillaStyleCheckResolver(state.Vanilla),
                StandardActions.CreateRegistry());
            runtime.Cycle.Attach();
        }

        private static void AdvanceProductionCycle(HarnessState state, HarnessRuntime runtime)
        {
            if (runtime.Cycle == null)
            {
                return;
            }

            ProductionCyclePass pass = runtime.Cycle.Run(state.Vanilla.Now);
            if (!pass.Ran)
            {
                return;
            }

            runtime.CyclePasses++;
            runtime.CycleGoalChanges += pass.GoalChanges.Count;
            runtime.CycleIntentions += pass.IntentionsGathered;
            runtime.CycleCommits += pass.Committed;

            // BQa-020. Organization activity is no longer a second daily system beside the cycle:
            // the cycle enrolls the bodies and runs the same owner the Lab used to call, so the
            // harness counts what that pass did rather than driving one of its own. The Plugin
            // reaches it through the identical runner.
            if (pass.Institutional != null)
            {
                runtime.OrganizationActions += pass.Institutional.Committed;
            }
        }

        private static void AdvanceSchemes(HarnessState state, HarnessRuntime runtime)
        {
            runtime.Schemes ??= new OffScreenSchemes();
            ActionRegistry actions = StandardActions.CreateRegistry();
            ICheckResolver checks = new VanillaStyleCheckResolver(state.Vanilla);
            runtime.SchemeActions += runtime.Schemes.Advance(
                state.World,
                state.Vanilla,
                checks,
                actions,
                state.Vanilla.Now);
        }

        private static void ReconcileAbsences(HarnessState state, HarnessRuntime runtime)
        {
            runtime.Absences ??= new AbsenceLifecycle(state.World, state.Vanilla);
            AbsenceRound round = runtime.Absences.Reconcile();
            runtime.AbsenceReturns += round.Returned;
            runtime.AbsenceEnforcements += round.Enforced;
        }

        private static void CirculateRumors(HarnessState state, HarnessRuntime runtime)
        {
            if (runtime.RumorCirculation == null)
            {
                return;
            }

            RumorRound round = runtime.RumorCirculation.Run(state.World, state.Vanilla, state.Vanilla.Now);
            runtime.RumorTells += round.Tells;
            runtime.RumorRoutes += round.Routed;
        }

        private static void CompactMemories(HarnessState state, HarnessRuntime runtime)
        {
            if (state.Vanilla.Now.TotalDays % 14 == 0)
            {
                MemoryCompactionReport report = state.World.Memories.Compact(state.Vanilla.Now);
                runtime.MemoriesCompacted += report.Removed;
            }
        }

        private static void ReloadCheckpoint(HarnessState state, HarnessRuntime runtime)
        {
            // The runner owns the configured day; the descriptor exists so persistence is reported
            // through the same registry as every other production-capable phase.
        }
    }
}
