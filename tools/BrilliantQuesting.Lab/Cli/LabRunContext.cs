using System;
using System.Collections.Generic;
using System.IO;

namespace BrilliantQuesting.Lab.Cli
{
    /// <summary>
    /// Everything a scenario needs for one deterministic run: the resolved seed, the parsed
    /// options, and where its report goes.
    ///
    /// <see cref="Output"/> is <see cref="Console.Out"/> for a normal invocation, so a scenario
    /// that still writes to the console directly behaves exactly as it did before.
    /// </summary>
    public sealed class LabRunContext
    {
        public LabRunContext(LabScenario scenario, ulong seed, LabArguments arguments, TextWriter output, TextWriter error)
        {
            Scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
            Arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
            Output = output ?? throw new ArgumentNullException(nameof(output));
            Error = error ?? throw new ArgumentNullException(nameof(error));
            Seed = seed;
        }

        public LabScenario Scenario { get; }

        /// <summary>The seed the runner resolved: <c>--seed</c>, a bare number, or the default.</summary>
        public ulong Seed { get; }

        public LabArguments Arguments { get; }

        /// <summary>The scenario's own tokens, for a scenario that forwards its arguments.</summary>
        public IReadOnlyList<string> RawArguments => Arguments.Tokens;

        public TextWriter Output { get; }

        public TextWriter Error { get; }

        public void WriteLine(string line = "") => Output.WriteLine(line);

        public void Header(string title) => LabText.Header(Output, title);
    }
}
