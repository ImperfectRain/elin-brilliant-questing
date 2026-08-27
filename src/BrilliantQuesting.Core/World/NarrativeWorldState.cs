using System.Collections.Generic;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Memory;
using BrilliantQuesting.Relationships;
using BrilliantQuesting.Threads;

namespace BrilliantQuesting.World
{
    /// <summary>
    /// The authoritative procedural database.
    ///
    /// Vanilla objects stay authoritative for vanilla facts (real stats, real inventory, real shop
    /// level). This owns identity, causal history, belief, relationship context and narrative
    /// state - the things Elin has no place to put.
    /// </summary>
    public sealed class NarrativeWorldState
    {
        /// <summary>Bumped whenever the persisted shape changes; drives save migration.</summary>
        public const int CurrentSchemaVersion = 1;

        public NarrativeWorldState(ulong worldSeed)
        {
            WorldSeed = worldSeed;
            SchemaVersion = CurrentSchemaVersion;
            Ids = new IdMinter();
            Registry = new EntityRegistry();
            Ledger = new EventLedger();
            Knowledge = new KnowledgeGraph();
            Memories = new MemoryLedger();
            Relationships = new RelationshipGraph();
            Threads = new List<NarrativeThread>();
            Rng = new DeterministicRng(worldSeed);
        }

        public int SchemaVersion { get; set; }

        public ulong WorldSeed { get; }

        public IdMinter Ids { get; }

        public EntityRegistry Registry { get; }

        public EventLedger Ledger { get; }

        public KnowledgeGraph Knowledge { get; }

        public MemoryLedger Memories { get; }

        public RelationshipGraph Relationships { get; }

        public List<NarrativeThread> Threads { get; }

        /// <summary>World-level stream. Subsystems should Fork() rather than draw from this.</summary>
        public DeterministicRng Rng { get; }

        public EntityId NewId(string kind) => Ids.Next(kind);

        /// <summary>
        /// Appends an event and dispatches it to every listener. All consequence handling hangs
        /// off this one call, which is what keeps causality inspectable.
        /// </summary>
        public WorldEvent Record(
            WorldEventType type,
            EntityId actor,
            EntityId target,
            GameTime now,
            double magnitude = 0.5,
            EntityId zone = default,
            IReadOnlyList<EntityId> related = null,
            IReadOnlyList<EntityId> witnesses = null,
            IReadOnlyList<EntityId> evidence = null,
            IReadOnlyList<string> tags = null,
            EntityId threadId = default)
        {
            WorldEvent worldEvent = new WorldEvent(
                NewId("evt"), type, actor, target, now, magnitude, zone, related, witnesses, evidence, tags, threadId);
            Ledger.Append(worldEvent);
            return worldEvent;
        }

        public NarrativeThread GetThread(EntityId id)
        {
            for (int i = 0; i < Threads.Count; i++)
            {
                if (Threads[i].Id == id)
                {
                    return Threads[i];
                }
            }

            return null;
        }
    }
}
