namespace BrilliantQuesting.Lab.Cli.Scenarios
{
    internal sealed class TravelingGroupsScenario : LabScenario
    {
        public override string Id => "traveling-groups";

        public override string Summary => "advance semantic travel groups through coarse milestones";

        public override string Description =>
            "Creates semantic, BQ-relocated, vanilla-owned and failed travel groups; advances\n"
            + "departure, arrival and interruption milestones; prints ownership, members, cargo,\n"
            + "events, delay reasons and save/reload idempotency.";

        public override ulong DefaultSeed => TravelingGroupsRun.DefaultSeed;

        public override int Run(LabRunContext context)
        {
            TravelingGroupsRun.Run(System.Console.Out, context.Seed);
            return LabExit.Success;
        }
    }
}
