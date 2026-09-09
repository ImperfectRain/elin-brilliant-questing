using System.Collections.Generic;

namespace BrilliantQuesting.Lab.Cli.Scenarios
{
    internal sealed class SpatialRangeScenario : LabScenario
    {
        public override string Id => "spatial-range";
        public override string Summary => "generated scenario plans and noun-independent spatial repetition metrics";
        public override string Description => "Prints a headless JSON spatial profile across shipped grammars. --runs selects consecutive seeds (1..1000, default 20). Rejected plans are reported separately.";
        public override IReadOnlyList<LabOption> Options => new[] { new LabOption("runs", "n", "number of seeds", "20") };
        public override int Run(LabRunContext context)
        {
            int seeds = context.Arguments.Int("runs", 20);
            if (seeds < 1 || seeds > 1000 || context.Seed > ulong.MaxValue - (ulong)(seeds - 1))
                throw new LabArgumentException("Use 1..1000 runs and a seed range that does not overflow.");
            var report = SpatialRangeHarness.Run(AntiTemplateHarness.LoadBundle(), context.Seed, seeds);
            context.WriteLine(report.ToJson());
            return report.Accepted > 0 ? LabExit.Success : LabExit.ScenarioFailure;
        }
    }
}
