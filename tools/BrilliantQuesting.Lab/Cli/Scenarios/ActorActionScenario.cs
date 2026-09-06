using System.Collections.Generic;

namespace BrilliantQuesting.Lab.Cli.Scenarios
{
    /// <summary>
    /// BQ-093 with no game attached: one NPC's need becomes a registered verb, resolves through
    /// the shared action system, and lands in the same ledger the player's acts do.
    ///
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- run actor-action --seed 11
    /// </summary>
    internal sealed class ActorActionScenario : LabScenario
    {
        public override string Id => "actor-action";

        public override string Summary => "an NPC's goal becomes a registered verb and resolves the way the player's does";

        public override string Description =>
            "Nessa's goat is gone. The run walks her from a need to a recorded consequence and prints\n"
            + "every joint: the goal her values formed, the approaches she scored, which registered verb\n"
            + "the chosen approach binds to, whether that verb was available to her, how it rolled, and\n"
            + "what went into the ledger. Then it runs the same verb for the player against the same\n"
            + "person, and asks three verbs she may not take why not. Nothing ticks and nobody moves:\n"
            + "the intention is asked for once, by the run, because autonomy is BQ-094.";

        public override IReadOnlyList<string> Aliases => new[] { "--actor-action" };

        public override ulong DefaultSeed => ActorActionRun.DefaultSeed;

        public override int Run(LabRunContext context)
        {
            ActorActionRun.Run(context.Output, context.Seed);
            return LabExit.Success;
        }
    }
}
