using System.Collections.Generic;

namespace BrilliantQuesting.Lab.Cli.Scenarios
{
    /// <summary>
    /// BQ-094 with no game attached: a matter the player ignored is ended by somebody else, and
    /// the player finds out only when the town gets round to mentioning it.
    ///
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- run autonomy --seed 94
    /// </summary>
    internal sealed class AutonomyScenario : LabScenario
    {
        public override string Id => "autonomy";

        public override string Summary => "a situation the player left alone is solved by somebody else";

        public override string Description =>
            "Marla is short of food and the player has done nothing about it for five days. Time moves,\n"
            + "and the world gets a turn: the run prints who was weighed, what vanilla's activity read was\n"
            + "worth as an opening, which routes could have ended the matter, which were not asked for off\n"
            + "screen, and how the attempt went. Then it shows the two halves of the done-when - the matter\n"
            + "ended without the player, and their chronicle stays empty until somebody actually tells them.";

        public override IReadOnlyList<string> Aliases => new[] { "--autonomy" };

        public override ulong DefaultSeed => AutonomyRun.DefaultSeed;

        public override int Run(LabRunContext context)
        {
            AutonomyRun.Run(context.Output, context.Seed);
            return LabExit.Success;
        }
    }
}
