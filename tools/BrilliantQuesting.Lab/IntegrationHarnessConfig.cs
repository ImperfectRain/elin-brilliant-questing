using System;
using System.Collections.Generic;

namespace BrilliantQuesting.Lab
{
    public enum IntegrationHarnessMode
    {
        Synthetic,
        Captured,
        Compare
    }

    public sealed class IntegrationHarnessConfig
    {
        public IntegrationHarnessMode Mode { get; set; } = IntegrationHarnessMode.Synthetic;

        public ulong Seed { get; set; } = 42UL;

        public int Days { get; set; } = 30;

        public int Population { get; set; } = 24;

        public int? SaveReloadDay { get; set; } = 15;

        public string SnapshotPath { get; set; }

        public string JsonOutputPath { get; set; }

        public bool Watch { get; set; }

        public bool WatchAll { get; set; }

        public bool Quiet { get; set; }

        public HashSet<string> DisabledSystems { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public void Validate()
        {
            if (Days < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(Days), "Days cannot be negative.");
            }

            if (Population < 3)
            {
                Population = 3;
            }

            if (SaveReloadDay.HasValue && (SaveReloadDay.Value <= 0 || SaveReloadDay.Value >= Days))
            {
                SaveReloadDay = null;
            }

            if ((Mode == IntegrationHarnessMode.Captured || Mode == IntegrationHarnessMode.Compare)
                && string.IsNullOrWhiteSpace(SnapshotPath))
            {
                throw new InvalidOperationException("Captured and compare modes require --snapshot <path>.");
            }
        }
    }
}
