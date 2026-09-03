using System;
using System.Collections.Generic;

namespace BrilliantQuesting.Lab.Cli.Scenarios
{
    /// <summary>
    /// The production-faithful integration harness, wrapped only enough to be discoverable.
    ///
    /// The harness owns its own command line - modes, snapshots, watch levels, JSON output and the
    /// pass/fail exit status - and keeps it: this adapter forwards the remaining tokens verbatim
    /// and turns an unreadable one into the laboratory's usual usage failure.
    ///
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- run integration --days 30
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- --integration --days 30
    /// </summary>
    internal sealed class IntegrationScenario : LabScenario
    {
        public override string Id => "integration";

        public override string Summary => "every registered production system over one simulated world, with invariants";

        public override string Description =>
            "Forwards its arguments to the integration harness, which parses them itself:\n"
            + "  --mode <synthetic|captured|compare>   which world the run is built from\n"
            + "  --captured / --compare                shorthand for the modes above\n"
            + "  --snapshot <path>                     captured world snapshot, required by those modes\n"
            + "  --seed <n>                            seed (default 42); a bare number works too\n"
            + "  --days <n>                            simulated days (default 30)\n"
            + "  --population <n>                      inhabitants (default 24)\n"
            + "  --reload-day <n> / --no-reload        save/reload point inside the run\n"
            + "  --watch / --watch-all                 per-day chronicle\n"
            + "  --json <path>                         write the machine-readable result\n"
            + "  --quiet                               suppress the printed report\n"
            + "  --disable <system>                    leave one production system out\n"
            + "Exits 0 when the run passes its invariants and 1 when it does not.";

        public override IReadOnlyList<string> Aliases => new[] { "--integration" };

        public override bool UsesSeed => false;

        public override bool ForwardsRawArguments => true;

        public override int Run(LabRunContext context)
        {
            string[] forwarded = new string[context.RawArguments.Count];
            for (int i = 0; i < forwarded.Length; i++)
            {
                forwarded[i] = context.RawArguments[i];
            }

            IntegrationHarnessConfig config;
            try
            {
                config = IntegrationHarness.Parse(forwarded);
                config.Validate();
            }
            catch (Exception failure) when (failure is InvalidOperationException || failure is FormatException
                                            || failure is OverflowException || failure is ArgumentException)
            {
                throw new LabArgumentException(failure.Message);
            }

            return IntegrationHarness.Execute(config);
        }
    }
}
