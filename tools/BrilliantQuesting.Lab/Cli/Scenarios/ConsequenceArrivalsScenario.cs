namespace BrilliantQuesting.Lab.Cli.Scenarios
{
    /// <summary>BQ-098 proof that earned consequences can reach the player and Home.</summary>
    internal sealed class ConsequenceArrivalsScenario : LabScenario
    {
        public override string Id => "consequence-arrivals";

        public override string Summary => "bring earned consequences to the player and Home";

        public override string Description =>
            "Creates an unresolved debt thread, keeps the player elsewhere, then brings the\n"
            + "creditor to the player's current zone and a guard to Home through the gated\n"
            + "arrival surface; prints the ledger tags and save/reload idempotency.";

        public override ulong DefaultSeed => ConsequenceArrivalsRun.DefaultSeed;

        public override int Run(LabRunContext context)
        {
            ConsequenceArrivalsRun.Run(System.Console.Out, context.Seed);
            return LabExit.Success;
        }
    }
}
