using System.Collections.Generic;

namespace BrilliantQuesting.Lab.Cli.Scenarios
{
    /// <summary>
    /// Finds seeds whose unscripted playthrough exercises the interesting path. Used once when
    /// choosing the demo default; kept because "which seed showed that bug" is a question the
    /// project will ask again.
    ///
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- run find-seed
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- --find-seed
    /// </summary>
    internal sealed class SeedProbeScenario : LabScenario
    {
        public override string Id => "find-seed";

        public override string Summary => "scan seeds for a run where the investigative route lands";

        public override string Description =>
            "Scans its own range of seeds rather than taking one, and prints each seed whose scripted\n"
            + "question/pickpocket/return sequence both passes the question and returns the property.";

        public override IReadOnlyList<string> Aliases => new[] { "--find-seed" };

        public override bool UsesSeed => false;

        public override int Run(LabRunContext context)
        {
            SeedProbe.Run();
            return LabExit.Success;
        }
    }
}
