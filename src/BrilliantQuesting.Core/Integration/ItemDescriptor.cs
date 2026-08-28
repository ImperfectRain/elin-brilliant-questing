using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.Integration
{
    /// <summary>
    /// A flattened view of a real Elin Thing. The simulation deliberately reasons about ordinary
    /// items - a ring, a ledger, a bottle - rather than a private "quest item" category, so that
    /// evidence, bribes and loot are all just things the player can actually carry, sell or lose.
    /// </summary>
    public sealed class ItemDescriptor
    {
        public ItemDescriptor(EntityId id, string name, string categoryTag, int value, string sourceId = null)
        {
            Id = id;
            Name = name;
            CategoryTag = categoryTag ?? string.Empty;
            Value = value;
            SourceId = sourceId ?? string.Empty;
        }

        public EntityId Id { get; }

        public string Name { get; }

        /// <summary>Coarse vanilla-ish category ("ring", "book", "food", "weapon", "ore").</summary>
        public string CategoryTag { get; }

        /// <summary>Vanilla value in orens, used for bribes, fencing and appraisal.</summary>
        public int Value { get; }

        /// <summary>
        /// The Thing source id this was or should be built from. Empty for a descriptor read back
        /// out of a live inventory, where the object already exists; set when a generator wants
        /// one created.
        /// </summary>
        public string SourceId { get; }

        public override string ToString() => Name + " (" + CategoryTag + ", " + Value + "g)";
    }
}
