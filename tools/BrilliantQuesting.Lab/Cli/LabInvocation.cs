using System.Collections.Generic;

namespace BrilliantQuesting.Lab.Cli
{
    /// <summary>What the laboratory was asked to do.</summary>
    public enum LabCommand
    {
        Help,
        List,
        Describe,
        Run
    }

    /// <summary>
    /// The result of reading a command line: which command, which scenario, with which seed and
    /// options - or the one message explaining why it could not be read.
    ///
    /// Resolution is separated from execution so dispatch can be tested without running a
    /// simulation.
    /// </summary>
    public sealed class LabInvocation
    {
        private LabInvocation(LabCommand command, LabScenario scenario, LabArguments arguments, ulong seed, string error)
        {
            Command = command;
            Scenario = scenario;
            Arguments = arguments;
            Seed = seed;
            Error = error;
        }

        public LabCommand Command { get; }

        /// <summary>The selected scenario. Null for <c>help</c> and <c>list</c>, and on failure.</summary>
        public LabScenario Scenario { get; }

        public LabArguments Arguments { get; }

        /// <summary>The resolved seed for a <c>run</c> of a seeded scenario; otherwise the default.</summary>
        public ulong Seed { get; }

        /// <summary>Null when the command line was understood.</summary>
        public string Error { get; }

        public bool IsValid => Error == null;

        public IReadOnlyList<string> RawArguments => Arguments.Tokens;

        internal static LabInvocation ForRun(LabScenario scenario, LabArguments arguments, ulong seed)
        {
            return new LabInvocation(LabCommand.Run, scenario, arguments, seed, null);
        }

        internal static LabInvocation ForDescribe(LabScenario scenario)
        {
            return new LabInvocation(LabCommand.Describe, scenario, LabArguments.Empty, scenario.DefaultSeed, null);
        }

        internal static LabInvocation ForCommand(LabCommand command)
        {
            return new LabInvocation(command, null, LabArguments.Empty, 0UL, null);
        }

        /// <summary>A command line that could not be read. Only <see cref="Error"/> is meaningful.</summary>
        internal static LabInvocation Failed(string error)
        {
            return new LabInvocation(LabCommand.Help, null, LabArguments.Empty, 0UL, error);
        }
    }
}
