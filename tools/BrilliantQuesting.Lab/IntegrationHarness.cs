using System;
using System.Collections.Generic;
using System.IO;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Threads;

namespace BrilliantQuesting.Lab
{
    public static class IntegrationHarness
    {
        public static HarnessRunResult Run(IntegrationHarnessConfig config)
        {
            config.Validate();
            if (config.Mode == IntegrationHarnessMode.Compare)
            {
                return Compare(config);
            }

            HarnessState state = BuildState(config);
            return RunSingle(config, state);
        }

        public static int RunCli(string[] args)
        {
            IntegrationHarnessConfig config = Parse(args);
            HarnessRunResult result = Run(config);
            string json = result.ToJson().ToJson(indented: true);
            if (!string.IsNullOrWhiteSpace(config.JsonOutputPath))
            {
                File.WriteAllText(config.JsonOutputPath, json);
            }

            if (!config.Quiet)
            {
                Print(result);
            }

            return result.Passed ? 0 : 1;
        }

        public static IntegrationHarnessConfig Parse(string[] args)
        {
            IntegrationHarnessConfig config = new IntegrationHarnessConfig();
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                switch (arg)
                {
                    case "--integration":
                        break;
                    case "--mode":
                        config.Mode = ParseMode(Next(args, ref i, "--mode"));
                        break;
                    case "--captured":
                        config.Mode = IntegrationHarnessMode.Captured;
                        break;
                    case "--compare":
                        config.Mode = IntegrationHarnessMode.Compare;
                        break;
                    case "--snapshot":
                        config.SnapshotPath = Next(args, ref i, "--snapshot");
                        break;
                    case "--days":
                        config.Days = int.Parse(Next(args, ref i, "--days"));
                        break;
                    case "--population":
                        config.Population = int.Parse(Next(args, ref i, "--population"));
                        break;
                    case "--seed":
                        config.Seed = ulong.Parse(Next(args, ref i, "--seed"));
                        break;
                    case "--reload-day":
                        config.SaveReloadDay = int.Parse(Next(args, ref i, "--reload-day"));
                        break;
                    case "--no-reload":
                        config.SaveReloadDay = null;
                        break;
                    case "--watch":
                        config.Watch = true;
                        break;
                    case "--watch-all":
                        config.Watch = true;
                        config.WatchAll = true;
                        break;
                    case "--json":
                        config.JsonOutputPath = Next(args, ref i, "--json");
                        break;
                    case "--quiet":
                        config.Quiet = true;
                        break;
                    case "--disable":
                        config.DisabledSystems.Add(Next(args, ref i, "--disable"));
                        break;
                    default:
                        if (ulong.TryParse(arg, out ulong seed))
                        {
                            config.Seed = seed;
                        }
                        else
                        {
                            throw new InvalidOperationException("Unknown integration harness argument: " + arg);
                        }

                        break;
                }
            }

            return config;
        }

        private static HarnessRunResult RunSingle(IntegrationHarnessConfig config, HarnessState state)
        {
            HarnessRuntime runtime = new HarnessRuntime();
            IReadOnlyList<ProductionSystemDescriptor> descriptors = ProductionSystemRegistry.Descriptors();
            HarnessChronicle chronicle = config.Watch ? new HarnessChronicle(config.WatchAll) : null;
            Dictionary<EntityId, ThreadState> threadStates = new Dictionary<EntityId, ThreadState>();

            foreach (ProductionSystemDescriptor descriptor in descriptors)
            {
                if (descriptor.Phase != HarnessPhase.Initialize)
                {
                    continue;
                }

                RunDescriptor(config, state, runtime, descriptor);
            }

            int eventCursor = state.World.Ledger.Count;
            int threadCursor = state.World.Threads.Count;
            chronicle?.Print(state.World, 0, 0, 0);

            for (int day = 1; day <= config.Days; day++)
            {
                state.Vanilla.AdvanceDays(1);
                foreach (ProductionSystemDescriptor descriptor in descriptors)
                {
                    if (descriptor.Phase != HarnessPhase.Daily)
                    {
                        continue;
                    }

                    if (descriptor.Id == "persistence_reload")
                    {
                        if (config.SaveReloadDay.HasValue && config.SaveReloadDay.Value == day)
                        {
                            state.RoundTripWorld();
                            runtime = Reinitialize(config, state, descriptors, runtime);
                            state.Coverage.Mark(descriptor.Id, HarnessCoverageState.Exercised, descriptor.Provenance, "WorldStateSerializer round-trip checkpoint.");
                        }
                        else
                        {
                            state.Coverage.Mark(descriptor.Id, HarnessCoverageState.Available, descriptor.Provenance, "Configured checkpoint did not fall on this day.");
                        }

                        continue;
                    }

                    RunDescriptor(config, state, runtime, descriptor);
                }

                HarnessRunResult interim = HarnessRunResult.From(state, runtime, config);
                HarnessInvariants.Check(state, interim, threadStates);
                chronicle?.Print(state.World, eventCursor, threadCursor, day);
                eventCursor = state.World.Ledger.Count;
                threadCursor = state.World.Threads.Count;
            }

            HarnessRunResult result = HarnessRunResult.From(state, runtime, config);
            HarnessInvariants.Check(state, result, threadStates);
            return result;
        }

        private static HarnessRuntime Reinitialize(
            IntegrationHarnessConfig config,
            HarnessState state,
            IReadOnlyList<ProductionSystemDescriptor> descriptors,
            HarnessRuntime previous)
        {
            HarnessRuntime runtime = new HarnessRuntime
            {
                RumorTells = previous.RumorTells,
                RumorRoutes = previous.RumorRoutes,
                ThreadEscalations = previous.ThreadEscalations,
                GeneratedSituations = previous.GeneratedSituations,
                EarlyContacts = previous.EarlyContacts,
                OrganizationActions = previous.OrganizationActions,
                AbsenceReturns = previous.AbsenceReturns,
                AbsenceEnforcements = previous.AbsenceEnforcements,
                MemoriesCompacted = previous.MemoriesCompacted
            };
            foreach (ProductionSystemDescriptor descriptor in descriptors)
            {
                if (descriptor.Phase == HarnessPhase.Initialize)
                {
                    RunDescriptor(config, state, runtime, descriptor);
                }
            }

            return runtime;
        }

        private static void RunDescriptor(
            IntegrationHarnessConfig config,
            HarnessState state,
            HarnessRuntime runtime,
            ProductionSystemDescriptor descriptor)
        {
            if (!ProductionSystemRegistry.IsEnabled(config, descriptor))
            {
                state.Coverage.Mark(descriptor.Id, HarnessCoverageState.Disabled, descriptor.Provenance);
                return;
            }

            int beforeEvents = state.World.Ledger.Count;
            int beforeThreads = state.World.Threads.Count;
            descriptor.Run(state, runtime);
            HarnessCoverageState coverageState = state.World.Ledger.Count != beforeEvents || state.World.Threads.Count != beforeThreads
                ? HarnessCoverageState.Exercised
                : HarnessCoverageState.Available;
            state.Coverage.Mark(descriptor.Id, coverageState, descriptor.Provenance, descriptor.Limitation);
        }

        private static HarnessState BuildState(IntegrationHarnessConfig config)
        {
            if (config.Mode == IntegrationHarnessMode.Captured)
            {
                return CapturedWorldSnapshot.Load(config.SnapshotPath).Hydrate();
            }

            return SyntheticHarnessSource.Build(config);
        }

        private static HarnessRunResult Compare(IntegrationHarnessConfig config)
        {
            IntegrationHarnessConfig syntheticConfig = new IntegrationHarnessConfig
            {
                Mode = IntegrationHarnessMode.Synthetic,
                Seed = config.Seed,
                Days = config.Days,
                Population = config.Population,
                SaveReloadDay = config.SaveReloadDay,
                Quiet = true
            };

            foreach (string disabled in config.DisabledSystems)
            {
                syntheticConfig.DisabledSystems.Add(disabled);
            }

            IntegrationHarnessConfig capturedConfig = new IntegrationHarnessConfig
            {
                Mode = IntegrationHarnessMode.Captured,
                SnapshotPath = config.SnapshotPath,
                Days = config.Days,
                SaveReloadDay = config.SaveReloadDay,
                Quiet = true
            };

            foreach (string disabled in config.DisabledSystems)
            {
                capturedConfig.DisabledSystems.Add(disabled);
            }

            HarnessRunResult synthetic = RunSingle(syntheticConfig, SyntheticHarnessSource.Build(syntheticConfig));
            HarnessRunResult captured = RunSingle(capturedConfig, CapturedWorldSnapshot.Load(config.SnapshotPath).Hydrate());
            HarnessRunResult result = new HarnessRunResult
            {
                Mode = "Compare",
                Source = "synthetic_vs_captured",
                Seed = config.Seed,
                Days = config.Days,
                Events = captured.Events - synthetic.Events,
                Facts = captured.Facts - synthetic.Facts,
                Memories = captured.Memories - synthetic.Memories,
                Threads = captured.Threads - synthetic.Threads,
                Npcs = captured.Npcs - synthetic.Npcs,
                Sites = captured.Sites - synthetic.Sites,
                RumorTells = captured.RumorTells - synthetic.RumorTells,
                RumorRoutes = captured.RumorRoutes - synthetic.RumorRoutes,
                ThreadEscalations = captured.ThreadEscalations - synthetic.ThreadEscalations,
                GeneratedSituations = captured.GeneratedSituations - synthetic.GeneratedSituations
            };

            foreach (HarnessCoverageEntry entry in captured.Coverage.Entries.Values)
            {
                result.Coverage.Mark(entry.Id, entry.State, entry.Provenance, entry.Note);
            }

            if (!synthetic.Passed)
            {
                result.Failures.Add("synthetic comparison run failed");
            }

            if (!captured.Passed)
            {
                result.Failures.Add("captured comparison run failed");
            }

            result.Warnings.Add("Comparison metrics are captured minus synthetic; they are diagnostic deltas, not equivalence assertions.");
            return result;
        }

        private static IntegrationHarnessMode ParseMode(string value)
        {
            if (Enum.TryParse(value, true, out IntegrationHarnessMode mode))
            {
                return mode;
            }

            throw new InvalidOperationException("Unknown integration harness mode: " + value);
        }

        private static string Next(string[] args, ref int index, string option)
        {
            if (index + 1 >= args.Length)
            {
                throw new InvalidOperationException(option + " requires a value.");
            }

            index++;
            return args[index];
        }

        private static void Print(HarnessRunResult result)
        {
            Console.WriteLine("Brilliant Questing integration harness");
            Console.WriteLine("mode=" + result.Mode + " source=" + result.Source + " days=" + result.Days);
            Console.WriteLine("events=" + result.Events + " facts=" + result.Facts + " memories=" + result.Memories
                              + " threads=" + result.Threads + " npcs=" + result.Npcs);
            Console.WriteLine("early contacts=" + result.EarlyContacts);
            Console.WriteLine("generated=" + result.GeneratedSituations + " escalations=" + result.ThreadEscalations
                              + " rumors=" + result.RumorTells + "/" + result.RumorRoutes);
            Console.WriteLine("coverage:");
            foreach (HarnessCoverageEntry entry in result.Coverage.Entries.Values)
            {
                Console.WriteLine("  " + entry.Id.PadRight(24) + entry.State + " [" + entry.Provenance + "]"
                                  + (entry.Note.Length == 0 ? string.Empty : " " + entry.Note));
            }

            foreach (string warning in result.Warnings)
            {
                Console.WriteLine("WARN: " + warning);
            }

            foreach (string failure in result.Failures)
            {
                Console.WriteLine("FAIL: " + failure);
            }

            Console.WriteLine("invariants=" + (result.Passed ? "PASS" : "FAIL"));
        }
    }
}
