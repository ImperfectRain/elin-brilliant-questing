using BrilliantQuesting.Lab.Playground;

namespace BrilliantQuesting.Lab.Cli.Scenarios
{
    /// <summary>
    /// What the playground exercises, what it feeds through the headless seam, what it authors for
    /// want of a production authority, and what it will not pretend to at all.
    ///
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- run playground-systems
    ///
    /// A scenario rather than a footer on every run, because it is a statement about the mod rather
    /// than about one conversation, and because a developer wanting the edges wants them without
    /// reading past a transcript to find them.
    /// </summary>
    internal sealed class PlaygroundSystemsScenario : LabScenario
    {
        public override string Id => "playground-systems";

        public override string Summary => "which systems the playground really runs, and which need a live game";

        public override string Description =>
            "An authored ledger of every system the conversation playground touches, in four columns:\n"
            + "production logic run over real state; production logic fed by the headless sandbox seam;\n"
            + "choices no Core system makes yet, which the laboratory therefore makes and labels; and\n"
            + "systems that need a running Elin and are neither simulated nor mocked here.";

        public override bool UsesSeed => false;

        public override int MaxPositionalArguments => 0;

        public override int Run(LabRunContext context)
        {
            PlaygroundAvailability.Write(context.Output);
            return LabExit.Success;
        }
    }
}
