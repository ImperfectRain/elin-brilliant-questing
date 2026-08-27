using System.Collections.Generic;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.Memory
{
    /// <summary>
    /// Per-character memory with consolidation. Without this, a long save accumulates tens of
    /// thousands of "player bought bread" rows; with it, repetition becomes a single trait memory
    /// and only genuinely notable events survive individually.
    /// </summary>
    public sealed class MemoryLedger
    {
        private readonly Dictionary<EntityId, List<MemoryRecord>> _byOwner = new Dictionary<EntityId, List<MemoryRecord>>();

        public MemoryRecord Add(MemoryRecord record)
        {
            List<MemoryRecord> memories = ListFor(record.Owner);

            if (record.IsConsolidatable)
            {
                // Repeats of a routine interaction fold into the existing memory rather than
                // growing the list. The occurrence count is what later becomes "regular customer".
                for (int i = 0; i < memories.Count; i++)
                {
                    MemoryRecord existing = memories[i];
                    if (existing.SummaryTag == record.SummaryTag && existing.About == record.About && existing.IsConsolidatable)
                    {
                        existing.Occurrences++;
                        existing.When = record.When;
                        existing.AffinityContribution += record.AffinityContribution;
                        return existing;
                    }
                }
            }

            memories.Add(record);
            return record;
        }

        public IReadOnlyList<MemoryRecord> MemoriesOf(EntityId owner)
        {
            return _byOwner.TryGetValue(owner, out List<MemoryRecord> memories) ? memories : Empty;
        }

        /// <summary>Memories one character holds about another. The dialogue layer's main query.</summary>
        public IEnumerable<MemoryRecord> MemoriesAbout(EntityId owner, EntityId about)
        {
            foreach (MemoryRecord memory in MemoriesOf(owner))
            {
                if (memory.About == about)
                {
                    yield return memory;
                }
            }
        }

        /// <summary>The affinity an NPC's remembered history with someone accounts for.</summary>
        public int AccountedAffinity(EntityId owner, EntityId about)
        {
            int total = 0;
            foreach (MemoryRecord memory in MemoriesAbout(owner, about))
            {
                total += memory.AffinityContribution;
            }

            return total;
        }

        public IEnumerable<MemoryRecord> Strongest(EntityId owner, EntityId about, int limit)
        {
            List<MemoryRecord> candidates = new List<MemoryRecord>();
            foreach (MemoryRecord memory in MemoriesAbout(owner, about))
            {
                candidates.Add(memory);
            }

            candidates.Sort((a, b) =>
            {
                int byWeight = b.Weight.CompareTo(a.Weight);
                return byWeight != 0 ? byWeight : b.When.CompareTo(a.When);
            });

            for (int i = 0; i < candidates.Count && i < limit; i++)
            {
                yield return candidates[i];
            }
        }

        /// <summary>
        /// Drops trivia that nobody has thought about in a long time. Anything Notable or above is
        /// untouched: the point of the ledger is that important history is permanent.
        /// </summary>
        public int Forget(GameTime now, long olderThanDays)
        {
            int removed = 0;
            foreach (List<MemoryRecord> memories in _byOwner.Values)
            {
                for (int i = memories.Count - 1; i >= 0; i--)
                {
                    MemoryRecord memory = memories[i];
                    if (memory.Weight == MemoryWeight.Trivial && now.DaysSince(memory.When) > olderThanDays)
                    {
                        memories.RemoveAt(i);
                        removed++;
                    }
                }
            }

            return removed;
        }

        public IEnumerable<KeyValuePair<EntityId, List<MemoryRecord>>> All => _byOwner;

        private List<MemoryRecord> ListFor(EntityId owner)
        {
            if (!_byOwner.TryGetValue(owner, out List<MemoryRecord> memories))
            {
                memories = new List<MemoryRecord>();
                _byOwner[owner] = memories;
            }

            return memories;
        }

        private static readonly MemoryRecord[] Empty = new MemoryRecord[0];
    }
}
