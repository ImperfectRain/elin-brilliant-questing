using System.Collections.Generic;

namespace BrilliantQuesting.Foundation
{
    /// <summary>
    /// Hands out stable ids per kind. Counters are part of the save so that a reloaded world
    /// never reissues an id that history already refers to.
    /// </summary>
    public sealed class IdMinter
    {
        private readonly Dictionary<string, ulong> _counters = new Dictionary<string, ulong>();

        public EntityId Next(string kind)
        {
            _counters.TryGetValue(kind, out ulong current);
            current++;
            _counters[kind] = current;
            return EntityId.Mint(kind, current);
        }

        public IReadOnlyDictionary<string, ulong> Counters => _counters;

        public void Restore(string kind, ulong counter)
        {
            _counters.TryGetValue(kind, out ulong current);
            _counters[kind] = counter > current ? counter : current;
        }
    }
}
