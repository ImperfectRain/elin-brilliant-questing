namespace BrilliantQuesting.Lab.Cli.Scenarios
{
    /// <summary>BQ-095 month-away proof for coarse off-screen NPC continuation.</summary>
    internal sealed class OffScreenSchemesScenario : LabScenario
    {
        public override string Id => "off-screen-schemes";

        public override string Summary => "advance named NPC goals for a month while the player is elsewhere";

        public override string Description =>
            "Creates a town with named NPC goals, relationships, debts, evidence and a supplier\n"
            + "failure; moves the player elsewhere; advances a month through the coarse scheduler;\n"
            + "prints each actor's goal, opportunity, selected NarrativeAction, outcome and ledger\n"
            + "events; then proves vanilla-travel ownership and save/reload idempotency.";

        public override ulong DefaultSeed => OffScreenSchemesRun.DefaultSeed;

        public override int Run(LabRunContext context)
        {
            OffScreenSchemesRun.Run(System.Console.Out, context.Seed);
            return LabExit.Success;
        }
    }
}
