using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace BrilliantQuesting.Lab.Cli
{
    /// <summary>
    /// The laboratory's single dispatch authority: it reads a command line, resolves it against a
    /// <see cref="LabCatalog"/>, and either prints discovery output or runs one scenario.
    ///
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- list
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- describe ambient
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- run ambient --seed 42
    ///
    /// The historic forms still work and resolve to the same scenarios: no arguments or a bare seed
    /// runs the theft laboratory, and flags such as <c>--ambient 42</c> are registered aliases.
    /// </summary>
    public static class LabCommandLine
    {
        private const string HelpCommand = "help";
        private const string ListCommand = "list";
        private const string DescribeCommand = "describe";
        private const string RunCommand = "run";

        public static int Execute(string[] args, TextWriter output, TextWriter error)
        {
            return Execute(args, LabCatalog.Default(), output, error);
        }

        public static int Execute(string[] args, LabCatalog catalog, TextWriter output, TextWriter error)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (error == null)
            {
                throw new ArgumentNullException(nameof(error));
            }

            LabInvocation invocation = Resolve(catalog, args);
            if (!invocation.IsValid)
            {
                error.WriteLine(invocation.Error);
                error.WriteLine("Run 'list' to see the registered scenarios, or 'help' for usage.");
                return LabExit.UsageError;
            }

            switch (invocation.Command)
            {
                case LabCommand.List:
                    PrintList(catalog, output);
                    return LabExit.Success;
                case LabCommand.Describe:
                    PrintDescription(invocation.Scenario, output);
                    return LabExit.Success;
                case LabCommand.Run:
                    return RunScenario(invocation, output, error);
                default:
                    PrintHelp(catalog, output);
                    return LabExit.Success;
            }
        }

        /// <summary>
        /// Reads a command line without running anything. Never throws for caller error: an
        /// unreadable command line comes back as an invocation carrying <see cref="LabInvocation.Error"/>.
        /// </summary>
        public static LabInvocation Resolve(LabCatalog catalog, string[] args)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            IReadOnlyList<string> tokens = args ?? new string[0];
            if (tokens.Count == 0)
            {
                return ResolveRun(catalog.DefaultScenario, new string[0]);
            }

            string head = tokens[0];
            IReadOnlyList<string> rest = Skip(tokens, 1);

            if (IsHelp(head))
            {
                return LabInvocation.ForCommand(LabCommand.Help);
            }

            if (Matches(head, ListCommand))
            {
                return LabInvocation.ForCommand(LabCommand.List);
            }

            if (Matches(head, DescribeCommand))
            {
                if (rest.Count == 0)
                {
                    return LabInvocation.Failed("describe needs a scenario name.");
                }

                LabScenario described = catalog.Find(rest[0]);
                return described == null
                    ? LabInvocation.Failed("Unknown scenario '" + rest[0] + "'.")
                    : LabInvocation.ForDescribe(described);
            }

            if (Matches(head, RunCommand))
            {
                if (rest.Count == 0)
                {
                    return LabInvocation.Failed("run needs a scenario name.");
                }

                LabScenario chosen = catalog.Find(rest[0]);
                return chosen == null
                    ? LabInvocation.Failed("Unknown scenario '" + rest[0] + "'.")
                    : ResolveRun(chosen, Skip(rest, 1));
            }

            // Historic form: a bare seed runs the default laboratory.
            if (ulong.TryParse(head, NumberStyles.None, CultureInfo.InvariantCulture, out ulong _))
            {
                return ResolveRun(catalog.DefaultScenario, tokens);
            }

            // Historic form: --ambient, --questline-sweep and friends are registered aliases.
            LabScenario aliased = catalog.Find(head);
            if (aliased != null)
            {
                return ResolveRun(aliased, rest);
            }

            return LabInvocation.Failed("Unknown command '" + head + "'.");
        }

        private static LabInvocation ResolveRun(LabScenario scenario, IReadOnlyList<string> tokens)
        {
            if (scenario == null)
            {
                return LabInvocation.Failed("The laboratory has no default scenario registered.");
            }

            LabArguments arguments;
            try
            {
                arguments = LabArguments.Parse(tokens);
            }
            catch (LabArgumentException failure)
            {
                return LabInvocation.Failed(failure.Message);
            }

            if (scenario.ForwardsRawArguments)
            {
                return LabInvocation.ForRun(scenario, arguments, scenario.DefaultSeed);
            }

            string rejected = FirstUndeclaredOption(scenario, arguments);
            if (rejected != null)
            {
                return LabInvocation.Failed("Scenario '" + scenario.Id + "' has no option --" + rejected + ".");
            }

            if (arguments.Positionals.Count > scenario.MaxPositionalArguments)
            {
                return LabInvocation.Failed(
                    "Scenario '" + scenario.Id + "' takes at most " + scenario.MaxPositionalArguments
                    + " bare argument(s), got " + arguments.Positionals.Count + ".");
            }

            ulong seed = scenario.DefaultSeed;
            if (scenario.UsesSeed)
            {
                try
                {
                    seed = arguments.UInt64OrPositional("seed", 0, scenario.DefaultSeed);
                }
                catch (LabArgumentException failure)
                {
                    return LabInvocation.Failed(failure.Message);
                }
            }

            return LabInvocation.ForRun(scenario, arguments, seed);
        }

        private static int RunScenario(LabInvocation invocation, TextWriter output, TextWriter error)
        {
            LabRunContext context = new LabRunContext(
                invocation.Scenario,
                invocation.Seed,
                invocation.Arguments,
                output,
                error);

            try
            {
                return invocation.Scenario.Run(context);
            }
            catch (LabArgumentException failure)
            {
                error.WriteLine(failure.Message);
                return LabExit.UsageError;
            }
        }

        private static string FirstUndeclaredOption(LabScenario scenario, LabArguments arguments)
        {
            foreach (string name in arguments.OptionNames)
            {
                if (scenario.UsesSeed && string.Equals(name, "seed", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                bool declared = false;
                foreach (LabOption option in scenario.Options)
                {
                    if (string.Equals(option.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        declared = true;
                        break;
                    }
                }

                if (!declared)
                {
                    return name;
                }
            }

            return null;
        }

        private static void PrintHelp(LabCatalog catalog, TextWriter output)
        {
            output.WriteLine("Brilliant Questing laboratory - headless probes over the production simulation.");
            output.WriteLine();
            output.WriteLine("usage:");
            output.WriteLine("  dotnet run --project tools/BrilliantQuesting.Lab -- <command> [options]");
            output.WriteLine();
            output.WriteLine("commands:");
            output.WriteLine("  list                        the registered scenarios");
            output.WriteLine("  describe <scenario>         a scenario's options, defaults and purpose");
            output.WriteLine("  run <scenario> [options]    run one scenario");
            output.WriteLine("  help                        this text");
            output.WriteLine();
            output.WriteLine("common options:");
            output.WriteLine("  --seed <n>                  seed for the run; also accepted as a bare number");
            output.WriteLine();
            output.WriteLine("With no command the '" + LabCatalog.DefaultScenarioId + "' scenario runs, so a bare");
            output.WriteLine("seed still works. The historic flags are registered aliases and keep working.");
            output.WriteLine();
            PrintList(catalog, output);
        }

        private static void PrintList(LabCatalog catalog, TextWriter output)
        {
            output.WriteLine("scenarios:");
            foreach (LabScenario scenario in catalog.Scenarios)
            {
                output.WriteLine("  " + LabText.Column(scenario.Id, 18) + scenario.Summary);
                if (scenario.Aliases.Count > 0)
                {
                    output.WriteLine("  " + LabText.Column(string.Empty, 18) + "alias: " + string.Join(" ", scenario.Aliases));
                }
            }
        }

        private static void PrintDescription(LabScenario scenario, TextWriter output)
        {
            output.WriteLine("scenario  " + scenario.Id);
            output.WriteLine("summary   " + scenario.Summary);
            if (scenario.Aliases.Count > 0)
            {
                output.WriteLine("aliases   " + string.Join(" ", scenario.Aliases));
            }

            output.WriteLine("invoke    run " + scenario.Id + (scenario.UsesSeed ? " --seed <n>" : string.Empty));
            if (scenario.UsesSeed)
            {
                output.WriteLine("seed      default " + scenario.DefaultSeed + ", or a bare first argument");
            }
            else if (scenario.ForwardsRawArguments)
            {
                output.WriteLine("seed      parsed by the scenario itself, which owns its command line");
            }
            else
            {
                output.WriteLine("seed      not seeded through the runner");
            }

            if (scenario.Options.Count > 0)
            {
                output.WriteLine();
                output.WriteLine("options:");
                foreach (LabOption option in scenario.Options)
                {
                    string line = "  " + LabText.Column(option.Usage, 22) + option.Description;
                    if (option.DefaultValue != null)
                    {
                        line += " (default " + option.DefaultValue + ")";
                    }

                    output.WriteLine(line);
                }
            }

            if (!string.IsNullOrWhiteSpace(scenario.Description) && scenario.Description != scenario.Summary)
            {
                output.WriteLine();
                output.WriteLine(scenario.Description);
            }
        }

        private static bool IsHelp(string token)
        {
            return Matches(token, HelpCommand)
                   || Matches(token, "--help")
                   || Matches(token, "-h")
                   || Matches(token, "-?");
        }

        private static bool Matches(string token, string command)
        {
            return string.Equals(token, command, StringComparison.OrdinalIgnoreCase);
        }

        private static IReadOnlyList<string> Skip(IReadOnlyList<string> tokens, int count)
        {
            if (count >= tokens.Count)
            {
                return new string[0];
            }

            string[] rest = new string[tokens.Count - count];
            for (int i = 0; i < rest.Length; i++)
            {
                rest[i] = tokens[count + i];
            }

            return rest;
        }
    }
}
