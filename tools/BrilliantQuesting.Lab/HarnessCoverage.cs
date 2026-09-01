using System.Collections.Generic;

namespace BrilliantQuesting.Lab
{
    public enum HarnessCoverageState
    {
        Available,
        Exercised,
        Disabled,
        Skipped,
        PluginOnly,
        Future
    }

    public sealed class HarnessCoverageEntry
    {
        public HarnessCoverageEntry(string id, HarnessCoverageState state, string provenance, string note)
        {
            Id = id;
            State = state;
            Provenance = provenance ?? string.Empty;
            Note = note ?? string.Empty;
        }

        public string Id { get; }

        public HarnessCoverageState State { get; }

        public string Provenance { get; }

        public string Note { get; }
    }

    public sealed class HarnessCoverage
    {
        private readonly Dictionary<string, HarnessCoverageEntry> _entries =
            new Dictionary<string, HarnessCoverageEntry>();

        public IReadOnlyDictionary<string, HarnessCoverageEntry> Entries => _entries;

        public void Mark(string id, HarnessCoverageState state, string provenance, string note = "")
        {
            if (_entries.TryGetValue(id, out HarnessCoverageEntry existing)
                && Rank(existing.State) > Rank(state))
            {
                return;
            }

            _entries[id] = new HarnessCoverageEntry(id, state, provenance, note);
        }

        private static int Rank(HarnessCoverageState state)
        {
            switch (state)
            {
                case HarnessCoverageState.Exercised:
                    return 5;
                case HarnessCoverageState.Available:
                    return 4;
                case HarnessCoverageState.Skipped:
                    return 3;
                case HarnessCoverageState.Disabled:
                    return 2;
                case HarnessCoverageState.PluginOnly:
                    return 1;
                default:
                    return 0;
            }
        }
    }
}
