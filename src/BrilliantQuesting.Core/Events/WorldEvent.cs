using System.Collections.Generic;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.Events
{
    /// <summary>
    /// One immutable entry in world history. Everything downstream - affinity changes, memories,
    /// rumors, thread escalation - is derived from these, which is what makes consequences
    /// traceable and saves migratable.
    /// </summary>
    public sealed class WorldEvent
    {
        public WorldEvent(
            EntityId id,
            WorldEventType type,
            EntityId actor,
            EntityId target,
            GameTime time,
            double magnitude = 0.5,
            EntityId zone = default,
            IReadOnlyList<EntityId> related = null,
            IReadOnlyList<EntityId> witnesses = null,
            IReadOnlyList<EntityId> evidence = null,
            IReadOnlyList<string> tags = null,
            EntityId threadId = default)
        {
            Id = id;
            Type = type;
            Actor = actor;
            Target = target;
            Time = time;
            Magnitude = magnitude;
            Zone = zone;
            Related = related ?? Empty;
            Witnesses = CleanWitnesses(witnesses, actor, target);
            Evidence = evidence ?? Empty;
            Tags = tags ?? EmptyTags;
            ThreadId = threadId;
        }

        private static readonly EntityId[] Empty = new EntityId[0];
        private static readonly string[] EmptyTags = new string[0];

        public EntityId Id { get; }

        public WorldEventType Type { get; }

        public EntityId Actor { get; }

        public EntityId Target { get; }

        public GameTime Time { get; }

        /// <summary>0..1 severity. Drives memory weight, affinity swing and thread tension.</summary>
        public double Magnitude { get; }

        public EntityId Zone { get; }

        public IReadOnlyList<EntityId> Related { get; }

        /// <summary>
        /// Who plausibly saw it. Not "everyone in town" - knowledge spreads from here through the
        /// rumor system, which is what makes bribing a witness or faking an alibi worth doing.
        /// </summary>
        public IReadOnlyList<EntityId> Witnesses { get; }

        public IReadOnlyList<EntityId> Evidence { get; }

        public IReadOnlyList<string> Tags { get; }

        public EntityId ThreadId { get; }

        public override string ToString()
        {
            return Time + " " + Type + " " + Actor + " -> " + Target;
        }

        private static IReadOnlyList<EntityId> CleanWitnesses(
            IReadOnlyList<EntityId> witnesses,
            EntityId actor,
            EntityId target)
        {
            if (witnesses == null || witnesses.Count == 0)
            {
                return Empty;
            }

            List<EntityId> cleaned = new List<EntityId>();
            for (int i = 0; i < witnesses.Count; i++)
            {
                EntityId witness = witnesses[i];
                if (witness.IsNone || witness == actor || witness == target || cleaned.Contains(witness))
                {
                    continue;
                }

                cleaned.Add(witness);
            }

            return cleaned.Count == 0 ? Empty : cleaned.ToArray();
        }
    }
}
