using System.Collections.Generic;

namespace BrilliantQuesting.Lab.Cli.Scenarios
{
    /// <summary>
    /// BQ-037 with no game attached: one robbery on the road, four guilds, five places.
    ///
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- run guilds --seed 15
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- --guilds 15
    /// </summary>
    internal sealed class GuildsScenario : LabScenario
    {
        private const int DefaultDays = 3;

        public override string Id => "guilds";

        public override string Summary => "one robbery reaches four guild networks through their own interests";

        public override string Description =>
            "Nothing here is written per guild. The robbery is stated as what happened - a guard\n"
            + "killed, a shipment taken, a tavern left short - and each network picks up the half of it\n"
            + "that its own interest table reads. The two things to look at are what reached each hall,\n"
            + "none of which anybody there could have overheard, and what the contacts in the square say\n"
            + "to a player who carries a card versus one who does not.";

        public override IReadOnlyList<string> Aliases => new[] { "--guilds" };

        public override IReadOnlyList<LabOption> Options => new[]
        {
            new LabOption("days", "n", "in-game days of circulation before the square is read", DefaultDays.ToString())
        };

        public override int Run(LabRunContext context)
        {
            GuildRun.Run(context.Seed, context.Arguments.Int("days", DefaultDays));
            return LabExit.Success;
        }
    }
}
