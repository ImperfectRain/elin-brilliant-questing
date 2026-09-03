using System.Collections.Generic;

namespace BrilliantQuesting.Lab.Cli.Scenarios
{
    /// <summary>
    /// BQ-035 end to end: one person watches a theft, the town talks for a fortnight, and a player
    /// who asks nobody anything and opens no menu still finds out that something happened.
    ///
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- run ambient --seed 15
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- --ambient 15
    /// </summary>
    internal sealed class AmbientScenario : LabScenario
    {
        private const int DefaultDays = 14;
        private const int DefaultBystanders = 12;

        public override string Id => "ambient";

        public override string Summary => "overheard talk alone tells a passive player that something happened";

        public override string Description =>
            "The player does exactly one thing - stand in the market and let time pass. Every line\n"
            + "under \"heard\" is somebody speaking within earshot; nothing announces a situation, names\n"
            + "a thread or offers an objective. What the journal holds at the end is the whole of what\n"
            + "the player has to go on.";

        public override IReadOnlyList<string> Aliases => new[] { "--ambient" };

        public override IReadOnlyList<LabOption> Options => new[]
        {
            new LabOption("days", "n", "in-game days to let pass", DefaultDays.ToString()),
            new LabOption("bystanders", "n", "townspeople who might speak in earshot", DefaultBystanders.ToString())
        };

        public override int Run(LabRunContext context)
        {
            AmbientRun.Run(
                context.Seed,
                context.Arguments.Int("days", DefaultDays),
                context.Arguments.Int("bystanders", DefaultBystanders));
            return LabExit.Success;
        }
    }
}
