namespace BrilliantQuesting.Lab.Cli.Scenarios
{
    /// <summary>BQ-096 proof for adventuring-party rescue ecology.</summary>
    internal sealed class AdventurerEcologyScenario : LabScenario
    {
        public override string Id => "adventurer-ecology";

        public override string Summary => "an adventuring party attempts a rescue the player left alone";

        public override string Description =>
            "Creates a missing-brewer rescue matter and a persistent adventuring-party organization;\n"
            + "leaves the player out of it; advances the party ecology pass; prints the ordinary\n"
            + "rescue action attempt, party event, and claims that make the outcome discoverable.";

        public override ulong DefaultSeed => AdventurerEcologyRun.DefaultSeed;

        public override int Run(LabRunContext context)
        {
            AdventurerEcologyRun.Run(context.Output, context.Seed);
            return LabExit.Success;
        }
    }
}
