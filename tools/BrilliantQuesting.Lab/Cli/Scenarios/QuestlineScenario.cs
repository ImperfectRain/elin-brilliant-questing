using System.Collections.Generic;

namespace BrilliantQuesting.Lab.Cli.Scenarios
{
    /// <summary>
    /// One seeded situation played end to end with the real dice, one in-game day at a time, by a
    /// policy that asks the world what is currently possible and what the player currently knows.
    ///
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- run questline --seed 15
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- --questline 15
    /// </summary>
    internal sealed class QuestlineScenario : LabScenario
    {
        private const int DefaultDays = 16;

        public override string Id => "questline";

        public override string Summary => "one seeded situation played day by day by an unscripted player policy";

        public override string Description =>
            "The player is not a script: each day the policy asks what the world currently offers and\n"
            + "what the player currently knows, and picks the most sensible move available, so the\n"
            + "player's moves and the situation's own escalation interleave. On an idle day it prints\n"
            + "what the player wanted and what the world said about it.";

        public override IReadOnlyList<string> Aliases => new[] { "--questline" };

        public override IReadOnlyList<LabOption> Options => new[]
        {
            new LabOption("days", "n", "in-game days to play", DefaultDays.ToString())
        };

        public override int Run(LabRunContext context)
        {
            Questline.Run(context.Seed, context.Arguments.Int("days", DefaultDays));
            return LabExit.Success;
        }
    }
}
