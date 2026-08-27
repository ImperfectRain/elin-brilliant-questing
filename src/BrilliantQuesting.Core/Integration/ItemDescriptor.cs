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
        public ItemDescriptor(EntityId id, string name, string categoryTag, int value)
        {
            Id = id;
            Name = name;
            CategoryTag = categoryTag ?? string.Empty;
            Value = value;
        }

        public EntityId Id { get; }

        public string Name { get; }

        /// <summary>Coarse vanilla-ish category ("ring", "book", "food", "weapon", "ore").</summary>
        public string CategoryTag { get; }

        /// <summary>Vanilla value in orens, used for bribes, fencing and appraisal.</summary>
        public int Value { get; }

        public override string ToString() => Name + " (" + CategoryTag + ", " + Value + "g)";
    }
}
