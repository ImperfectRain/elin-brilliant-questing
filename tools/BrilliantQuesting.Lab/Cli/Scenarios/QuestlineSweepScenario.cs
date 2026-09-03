using System.Collections.Generic;

namespace BrilliantQuesting.Lab.Cli.Scenarios
{
    /// <summary>
    /// The same player policy across many seeds. One transcript shows that the machinery works; a
    /// sweep shows whether the same situation actually produces different stories.
    ///
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- run questline-sweep --count 60
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- --questline-sweep 60
    /// </summary>
    internal sealed class QuestlineSweepScenario : LabScenario
    {
        private const int DefaultCount = 50;

        public override string Id => "questline-sweep";

        public override string Summary => "the questline policy over seeds 1..count, tallying how the stories end";

        public override string Description =>
            "Sweeps its own seeds, so --seed does not apply. Reports the ending tally plus how often\n"
            + "the player learned who did it, could prove it, and how often the victim accused someone\n"
            + "without proof.";

        public override IReadOnlyList<string> Aliases => new[] { "--questline-sweep" };

        public override bool UsesSeed => false;

        public override IReadOnlyList<LabOption> Options => new[]
        {
            new LabOption("count", "n", "how many seeds to sweep, starting at 1", DefaultCount.ToString())
        };

        public override int Run(LabRunContext context)
        {
            Questline.Sweep(context.Arguments.IntOrPositional("count", 0, DefaultCount));
            return LabExit.Success;
        }
    }
}
