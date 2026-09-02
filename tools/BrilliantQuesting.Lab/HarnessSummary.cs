using System.Collections.Generic;
using BrilliantQuesting.Events;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;

namespace BrilliantQuesting.Lab
{
    public sealed class HarnessRunResult
    {
        public string Mode { get; set; }
        public string Source { get; set; }
        public ulong Seed { get; set; }
        public int Days { get; set; }
        public int Events { get; set; }
        public int Facts { get; set; }
        public int Beliefs { get; set; }
        public int Memories { get; set; }
        public int Threads { get; set; }
        public int ActiveThreads { get; set; }
        public int DormantThreads { get; set; }
        public int ResolvedThreads { get; set; }
        public int Npcs { get; set; }
        public int Sites { get; set; }
        public int Relationships { get; set; }
        public int Demands { get; set; }
        public int Businesses { get; set; }
        public int Organizations { get; set; }
        public int RumorTells { get; set; }
        public int RumorRoutes { get; set; }
        public int ThreadEscalations { get; set; }
        public int GeneratedSituations { get; set; }

        /// <summary>BQ-115. Faces the primary settlement elected to keep bringing back.</summary>
        public int EarlyContacts { get; set; }
        public int OrganizationActions { get; set; }
        public int AbsenceReturns { get; set; }
        public int AbsenceEnforcements { get; set; }
        public int MemoriesCompacted { get; set; }
        public HarnessCoverage Coverage { get; } = new HarnessCoverage();
        public Dictionary<string, int> EventsByType { get; } = new Dictionary<string, int>();
        public Dictionary<string, int> ThreadsByArchetype { get; } = new Dictionary<string, int>();
        public List<string> Warnings { get; } = new List<string>();
        public List<string> Failures { get; } = new List<string>();
        public string FinalWorldJson { get; set; }
        public bool Passed => Failures.Count == 0;

        public static HarnessRunResult From(HarnessState state, HarnessRuntime runtime, IntegrationHarnessConfig config)
        {
            HarnessRunResult result = new HarnessRunResult
            {
                Mode = config.Mode.ToString(),
                Source = state.Source,
                Seed = state.World.WorldSeed,
                Days = (int)state.Vanilla.Now.TotalDays,
                Events = state.World.Ledger.Count,
                Facts = state.World.Knowledge.Facts.Count,
                Memories = state.World.Memories.Count,
                Threads = state.World.Threads.Count,
                Npcs = state.World.Registry.Npcs.Count,
                Sites = state.World.Registry.Sites.Count,
                Demands = state.World.Demands.Pressures.Count,
                Businesses = state.World.Businesses.Count,
                Organizations = state.World.Registry.Organizations.Count,
                RumorTells = runtime.RumorTells,
                RumorRoutes = runtime.RumorRoutes,
                ThreadEscalations = runtime.ThreadEscalations,
                GeneratedSituations = runtime.GeneratedSituations,
                EarlyContacts = runtime.EarlyContacts,
                OrganizationActions = runtime.OrganizationActions,
                AbsenceReturns = runtime.AbsenceReturns,
                AbsenceEnforcements = runtime.AbsenceEnforcements,
                MemoriesCompacted = runtime.MemoriesCompacted,
                FinalWorldJson = WorldStateSerializer.Save(state.World, indented: false)
            };

            foreach (NarrativeThread thread in state.World.Threads)
            {
                Count(result.ThreadsByArchetype, thread.ArchetypeId);
                if (thread.State == ThreadState.Resolved) result.ResolvedThreads++;
                else if (thread.State == ThreadState.Dormant) result.DormantThreads++;
                else result.ActiveThreads++;
            }

            foreach (WorldEvent evt in state.World.Ledger.Events)
            {
                Count(result.EventsByType, evt.Type.ToString());
            }

            foreach (KeyValuePair<BrilliantQuesting.Foundation.EntityId, NarrativeNpc> pair in state.World.Registry.Npcs)
            {
                foreach (BrilliantQuesting.Knowledge.KnowledgeRecord ignored in state.World.Knowledge.BeliefsOf(pair.Key))
                {
                    result.Beliefs++;
                }
            }

            foreach (KeyValuePair<BrilliantQuesting.Foundation.EntityId, List<BrilliantQuesting.Relationships.RelationshipEdge>> pair in state.World.Relationships.All)
            {
                result.Relationships += pair.Value.Count;
            }

            foreach (HarnessCoverageEntry entry in state.Coverage.Entries.Values)
            {
                result.Coverage.Mark(entry.Id, entry.State, entry.Provenance, entry.Note);
            }

            foreach (HarnessCoverageEntry entry in ProductionSystemRegistry.PluginOnlyLimitations())
            {
                result.Coverage.Mark(entry.Id, entry.State, entry.Provenance, entry.Note);
            }

            return result;
        }

        public JsonValue ToJson()
        {
            JsonValue root = JsonValue.Object()
                .Set("mode", Mode)
                .Set("source", Source)
                .Set("seed", Seed.ToString())
                .Set("days", Days)
                .Set("passed", Passed)
                .Set("events", Events)
                .Set("facts", Facts)
                .Set("beliefs", Beliefs)
                .Set("memories", Memories)
                .Set("threads", Threads)
                .Set("activeThreads", ActiveThreads)
                .Set("dormantThreads", DormantThreads)
                .Set("resolvedThreads", ResolvedThreads)
                .Set("npcs", Npcs)
                .Set("sites", Sites)
                .Set("relationships", Relationships)
                .Set("demands", Demands)
                .Set("businesses", Businesses)
                .Set("organizations", Organizations)
                .Set("rumorTells", RumorTells)
                .Set("rumorRoutes", RumorRoutes)
                .Set("threadEscalations", ThreadEscalations)
                .Set("generatedSituations", GeneratedSituations)
                .Set("earlyContacts", EarlyContacts)
                .Set("organizationActions", OrganizationActions)
                .Set("absenceReturns", AbsenceReturns)
                .Set("absenceEnforcements", AbsenceEnforcements)
                .Set("memoriesCompacted", MemoriesCompacted)
                .Set("eventsByType", Map(EventsByType))
                .Set("threadsByArchetype", Map(ThreadsByArchetype))
                .Set("coverage", CoverageJson())
                .Set("warnings", Strings(Warnings))
                .Set("failures", Strings(Failures));
            return root;
        }

        private JsonValue CoverageJson()
        {
            JsonValue array = JsonValue.Array();
            foreach (HarnessCoverageEntry entry in Coverage.Entries.Values)
            {
                array.Add(JsonValue.Object()
                    .Set("id", entry.Id)
                    .Set("state", entry.State.ToString())
                    .Set("provenance", entry.Provenance)
                    .Set("note", entry.Note));
            }

            return array;
        }

        private static JsonValue Map(Dictionary<string, int> values)
        {
            JsonValue json = JsonValue.Object();
            foreach (KeyValuePair<string, int> pair in values)
            {
                json.Set(pair.Key, pair.Value);
            }

            return json;
        }

        private static JsonValue Strings(List<string> values)
        {
            JsonValue array = JsonValue.Array();
            foreach (string value in values)
            {
                array.Add(JsonValue.String(value));
            }

            return array;
        }

        private static void Count(Dictionary<string, int> counts, string key)
        {
            counts.TryGetValue(key, out int value);
            counts[key] = value + 1;
        }
    }
}
