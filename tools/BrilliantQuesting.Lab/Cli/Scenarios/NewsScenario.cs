using System.Collections.Generic;

namespace BrilliantQuesting.Lab.Cli.Scenarios
{
    /// <summary>
    /// BQ-036 with no game attached: two things happen in one town, gossip carries them unevenly,
    /// and then the player walks round asking people what has been going on.
    ///
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- run news --seed 15
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- --news 15
    /// </summary>
    internal sealed class NewsScenario : LabScenario
    {
        private const int DefaultDays = 5;
        private const int DefaultTownspeople = 12;

        public override string Id => "news";

        public override string Summary => "two events circulate unevenly, then the player asks the town what happened";

        public override string Description =>
            "The point of the probe is the column of answers. Nobody was assigned a line; each person\n"
            + "says what the circulation happened to leave in their head, so the tavern's answer and the\n"
            + "answer three doors down are different reports of the same week. The player's journal at\n"
            + "the end is the sum of what two of them said, not of what the town knows.";

        public override IReadOnlyList<string> Aliases => new[] { "--news" };

        public override IReadOnlyList<LabOption> Options => new[]
        {
            new LabOption("days", "n", "in-game days of circulation before the player asks", DefaultDays.ToString()),
            new LabOption("townspeople", "n", "how many people the news may reach", DefaultTownspeople.ToString())
        };

        public override int Run(LabRunContext context)
        {
            NewsRun.Run(
                context.Seed,
                context.Arguments.Int("days", DefaultDays),
                context.Arguments.Int("townspeople", DefaultTownspeople));
            return LabExit.Success;
        }
    }
}
