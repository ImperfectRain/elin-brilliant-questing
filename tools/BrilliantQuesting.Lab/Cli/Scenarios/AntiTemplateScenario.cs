using System.Collections.Generic;

namespace BrilliantQuesting.Lab.Cli.Scenarios
{
    internal sealed class AntiTemplateScenario : LabScenario
    {
        public override string Id => "anti-template";
        public override string Summary => "seeded synthetic scene runs and a seven-axis repetition profile";
        public override string Description => "Prints a deterministic JSON repetition profile over all production scene fixtures. "
            + "--runs is the number of consecutive seeds (1..1000, default 20). Repetition is diagnostic; broken runs fail.";
        public override IReadOnlyList<LabOption> Options => new[] { new LabOption("runs", "n", "number of seeds", "20") };
        public override int Run(LabRunContext context)
        {
            int seeds = context.Arguments.Int("runs", 20);
            if (seeds < 1 || seeds > 1000 || context.Seed > ulong.MaxValue - (ulong)(seeds - 1))
                throw new LabArgumentException("Use 1..1000 runs and a seed range that does not overflow.");
            var report = AntiTemplateHarness.Run(AntiTemplateHarness.LoadBundle(), context.Seed, seeds);
            context.WriteLine(report.ToJson());
            return report.Failures.Count == 0 ? LabExit.Success : LabExit.ScenarioFailure;
        }
    }
}
