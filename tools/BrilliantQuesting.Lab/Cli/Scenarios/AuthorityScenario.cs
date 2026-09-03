using System.Collections.Generic;

namespace BrilliantQuesting.Lab.Cli.Scenarios
{
    /// <summary>
    /// BQ-038 with no game attached: the same hall, the same beast, three players.
    ///
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- run authority --seed 3
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- --authority 3
    /// </summary>
    internal sealed class AuthorityScenario : LabScenario
    {
        public override string Id => "authority";

        public override string Summary => "who a guild hall listens to: card and rank, card alone, or neither";

        public override string Description =>
            "One player carries the Fighters' card and rank enough to be listened to, one carries it\n"
            + "and is nobody in the guild yet, and one carries nothing. Nothing about the road is\n"
            + "written for the Fighters - the situation says a carter is not safe from a thing, and the\n"
            + "network's own interest table makes that a bounty - so the run also asks the Merchants\n"
            + "officer standing in the same hall, who reads nothing in any of it.";

        public override IReadOnlyList<string> Aliases => new[] { "--authority" };

        /// <summary>
        /// Its own default rather than the laboratory's, so the demo shows the route working. The
        /// shared seed lands on an ordinary refusal, which is a real outcome and a poor first
        /// impression of a mechanic; either can be seen by naming a seed.
        /// </summary>
        public override ulong DefaultSeed => AuthorityRun.DefaultSeed;

        public override int Run(LabRunContext context)
        {
            AuthorityRun.Run(context.Seed);
            return LabExit.Success;
        }
    }
}
