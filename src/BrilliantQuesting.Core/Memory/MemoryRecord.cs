using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.Memory
{
    /// <summary>
    /// How much of an impression an event left. Trivial and Routine memories are allowed to be
    /// consolidated away into a trait; Defining memories are never forgotten, which is why
    /// murdering someone's sibling still matters two hundred hours later.
    /// </summary>
    public enum MemoryWeight
    {
        Trivial = 0,
        Routine = 1,
        Notable = 2,
        Important = 3,
        Defining = 4
    }

    /// <summary>
    /// One character's recollection of something. Affinity says how close an NPC feels to the
    /// player; this says why, which is what dialogue, willingness and revenge are built from.
    /// </summary>
    public sealed class MemoryRecord
    {
        public MemoryRecord(EntityId id, EntityId owner, EntityId about, WorldEventType eventType, MemoryWeight weight, GameTime when, int affinityContribution, string summaryTag)
        {
            Id = id;
            Owner = owner;
            About = about;
            EventType = eventType;
            Weight = weight;
            When = when;
            AffinityContribution = affinityContribution;
            SummaryTag = summaryTag ?? string.Empty;
            Occurrences = 1;
        }

        public EntityId Id { get; }

        public EntityId Owner { get; }

        /// <summary>Who the memory is about - usually the player, sometimes another NPC.</summary>
        public EntityId About { get; }

        public WorldEventType EventType { get; }

        public MemoryWeight Weight { get; }

        public GameTime When { get; set; }

        /// <summary>
        /// The affinity this memory accounts for. Vanilla affinity stays the single player-facing
        /// number; this records which slice of it came from where.
        /// </summary>
        public int AffinityContribution { get; set; }

        /// <summary>Stable tag for dialogue and consolidation ("player_returned_property").</summary>
        public string SummaryTag { get; }

        /// <summary>How many times this has now happened. Consolidation increments it.</summary>
        public int Occurrences { get; set; }

        public bool IsConsolidatable => Weight <= MemoryWeight.Routine;

        public override string ToString()
        {
            string count = Occurrences > 1 ? " x" + Occurrences : string.Empty;
            return Owner + " remembers " + SummaryTag + count + " (" + Weight + ", " + AffinityContribution + " affinity)";
        }
    }
}
