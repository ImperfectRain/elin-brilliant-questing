using System;

namespace BrilliantQuesting.Foundation
{
    /// <summary>
    /// Stable identity for anything the simulation must be able to recognise again:
    /// people, organizations, sites, important objects, facts, events, threads.
    ///
    /// Display names change. Vanilla runtime references (Chara instances, Thing instances,
    /// Zone instances) are destroyed and rebuilt. An EntityId never changes and is the only
    /// thing the persistent database is allowed to key on.
    /// </summary>
    public readonly struct EntityId : IEquatable<EntityId>, IComparable<EntityId>
    {
        public static readonly EntityId None = default;

        private readonly string _value;

        private EntityId(string value)
        {
            _value = value;
        }

        public string Value => _value ?? string.Empty;

        public bool IsNone => string.IsNullOrEmpty(_value);

        /// <summary>Rehydrates an id from save data. Never generates a new one.</summary>
        public static EntityId Parse(string value)
        {
            return string.IsNullOrEmpty(value) ? None : new EntityId(value);
        }

        /// <summary>
        /// Mints a deterministic id from a seeded sequence. Ids look like "npc_0000001a".
        /// Determinism matters: replaying a seed must rebuild the same world, and test
        /// fixtures must be diffable.
        /// </summary>
        public static EntityId Mint(string kind, ulong sequence)
        {
            if (string.IsNullOrEmpty(kind))
            {
                throw new ArgumentException("Entity ids need a kind prefix.", nameof(kind));
            }

            return new EntityId(kind + "_" + sequence.ToString("x8"));
        }

        public string Kind
        {
            get
            {
                int split = Value.IndexOf('_');
                return split <= 0 ? string.Empty : Value.Substring(0, split);
            }
        }

        public bool Equals(EntityId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is EntityId other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public int CompareTo(EntityId other) => string.CompareOrdinal(Value, other.Value);

        public override string ToString() => Value;

        public static bool operator ==(EntityId left, EntityId right) => left.Equals(right);

        public static bool operator !=(EntityId left, EntityId right) => !left.Equals(right);
    }
}
