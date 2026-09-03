using System;
using System.Collections.Generic;

namespace BrilliantQuesting.Lab.Cli
{
    /// <summary>
    /// One registered laboratory experiment.
    ///
    /// A scenario describes itself (id, summary, options, default seed) and runs once against a
    /// resolved <see cref="LabRunContext"/>. Everything the command line has in common - seed
    /// resolution, option parsing, option validation, exit status - is handled by
    /// <see cref="LabCommandLine"/>, so a new experiment is a new subclass plus one line in
    /// <see cref="LabCatalog.Default"/>.
    /// </summary>
    public abstract class LabScenario
    {
        /// <summary>Canonical name used by <c>run</c> and <c>describe</c>. Lower case, hyphenated.</summary>
        public abstract string Id { get; }

        /// <summary>One line for <c>list</c>. No trailing full stop.</summary>
        public abstract string Summary { get; }

        /// <summary>Longer prose for <c>describe</c>. Defaults to the summary.</summary>
        public virtual string Description => Summary;

        /// <summary>Historic flags that still select this scenario, for example <c>--ambient</c>.</summary>
        public virtual IReadOnlyList<string> Aliases => LabText.NoStrings;

        /// <summary>Declared options, excluding <c>--seed</c>, which the runner supplies.</summary>
        public virtual IReadOnlyList<LabOption> Options => LabOption.None;

        /// <summary>
        /// False when the scenario is not seeded through the shared mechanism - either because it
        /// sweeps seeds itself, or because it parses its own arguments.
        /// </summary>
        public virtual bool UsesSeed => true;

        /// <summary>Seed used when the caller does not supply one.</summary>
        public virtual ulong DefaultSeed => LabDefaults.Seed;

        /// <summary>
        /// True when the scenario owns its own argument parsing and the runner must forward the
        /// remaining tokens verbatim instead of validating them. Only the integration harness does
        /// this: it is a production-faithful harness with its own established command line.
        /// </summary>
        public virtual bool ForwardsRawArguments => false;

        /// <summary>How many bare (non <c>--option</c>) arguments the scenario accepts.</summary>
        public virtual int MaxPositionalArguments => 1;

        /// <summary>Runs once. Return 0 for success; a non-zero value becomes the process exit code.</summary>
        public abstract int Run(LabRunContext context);
    }

    /// <summary>A scenario option as advertised by <c>describe</c> and validated by the runner.</summary>
    public sealed class LabOption
    {
        public static readonly IReadOnlyList<LabOption> None = new LabOption[0];

        public LabOption(string name, string valueName, string description, string defaultValue = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("An option needs a name.", nameof(name));
            }

            Name = name;
            ValueName = valueName;
            Description = description;
            DefaultValue = defaultValue;
        }

        /// <summary>Bare name without the leading dashes, for example <c>days</c>.</summary>
        public string Name { get; }

        /// <summary>Placeholder shown in help, for example <c>n</c>. Null for a flag.</summary>
        public string ValueName { get; }

        public string Description { get; }

        /// <summary>Printed by <c>describe</c>. Null when the scenario has no fixed default.</summary>
        public string DefaultValue { get; }

        public string Usage => ValueName == null ? "--" + Name : "--" + Name + " <" + ValueName + ">";
    }

    /// <summary>Seeds shared by the laboratory scenarios that have no reason to differ.</summary>
    public static class LabDefaults
    {
        /// <summary>Chosen with the find-seed probe so the demo shows a run that goes somewhere.</summary>
        public const ulong Seed = 15UL;
    }

    /// <summary>
    /// A command line the caller got wrong. The runner turns it into a message and
    /// <see cref="LabExit.UsageError"/> rather than a stack trace.
    /// </summary>
    public sealed class LabArgumentException : Exception
    {
        public LabArgumentException(string message)
            : base(message)
        {
        }
    }

    /// <summary>Process exit codes the laboratory uses.</summary>
    public static class LabExit
    {
        public const int Success = 0;

        /// <summary>The scenario ran and reported failure - the integration harness uses this.</summary>
        public const int ScenarioFailure = 1;

        /// <summary>The command line could not be understood.</summary>
        public const int UsageError = 2;
    }
}
