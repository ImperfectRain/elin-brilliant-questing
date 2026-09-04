using System;
using System.Collections.Generic;
using System.IO;
using BrilliantQuesting.Lab.Playground;
using BrilliantQuesting.Lab.Playground.Sweep;

namespace BrilliantQuesting.Lab.Cli.Scenarios
{
    /// <summary>
    /// The playground as a diagnostic instrument rather than a demonstration: one family of
    /// controlled comparisons at a time, over the same production path a single run uses.
    ///
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- run playground-sweep --list-axes
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- run playground-sweep --axis relationship
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- run playground-sweep --axis callbacks --seed 15
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- run playground-sweep --axis all --json
    ///
    /// The question a single run cannot answer is which input state a conversation is actually a
    /// function of. A family answers it by holding everything still and moving one named piece of
    /// state, then printing the input difference beside the semantic, expression and world
    /// differences it produced - so a row that moved nothing is as informative as one that did.
    ///
    /// <b>Nothing here chooses an outcome.</b> There is no option that names a strategy, a depth,
    /// a tactic, a speech act, a callback, a fragment or a line, and there is no way to add one:
    /// a family composes <see cref="PlaygroundInput"/>s over a preset, and an input is handed the
    /// stage, which exposes stores rather than decisions. Everything in the semantic and expression
    /// columns comes back out of production Core.
    /// </summary>
    internal sealed class PlaygroundSweepScenario : LabScenario
    {
        private const string AllAxes = "all";
        private const string DefaultAxis = "relationship";

        public override string Id => "playground-sweep";

        public override string Summary => "one input moved at a time, and where in the conversation it lands";

        public override string Description =>
            "Runs a bounded family of controlled comparisons over the same conversation path a single\n"
            + "playground run uses, and prints four differences for every row: which authoritative or\n"
            + "actor-local state changed, what the semantic layers decided, what constrained and produced\n"
            + "the wording, and what the exchange left behind. A row that changed an input and changed\n"
            + "nothing observable is reported as such - an inert input is one of the things this exists\n"
            + "to find - and an axis point current state cannot express is reported as unsupported rather\n"
            + "than approximated.\n"
            + "\n"
            + "Each family ends in a count of rows, distinct semantic outcomes, distinct realized lines,\n"
            + "unrealized acts, no-effect inputs, world mutations and invariant violations. A violation\n"
            + "fails the run.\n"
            + "\n"
            + AxisHelp();

        public override IReadOnlyList<LabOption> Options => new[]
        {
            new LabOption("axis", "id", "which family to run, or 'all'", DefaultAxis),
            new LabOption("list-axes", null, "print the families and stop"),
            new LabOption("json", null, "also print the rows as JSON, for later regression analysis")
        };

        public override int MaxPositionalArguments => 1;

        public override int Run(LabRunContext context)
        {
            PlaygroundSweepAxes axes = PlaygroundSweepAxes.Default();

            if (context.Arguments.Flag("list-axes"))
            {
                context.Output.Write(AxisHelp());
                return LabExit.Success;
            }

            string chosen = context.Arguments.String("axis", null)
                ?? (context.Arguments.Positionals.Count > 0 ? context.Arguments.Positionals[0] : DefaultAxis);

            List<PlaygroundSweepResult> results = new List<PlaygroundSweepResult>();
            if (string.Equals(chosen, AllAxes, StringComparison.OrdinalIgnoreCase))
            {
                IReadOnlyList<PlaygroundSweepAxis> all = axes.All;
                for (int i = 0; i < all.Count; i++)
                {
                    results.Add(PlaygroundSweepReport.Evaluate(all[i], context.Seed));
                }
            }
            else
            {
                PlaygroundSweepAxis axis = axes.Find(chosen)
                    ?? throw new LabArgumentException(
                        "Unknown axis '" + chosen + "'. Run 'run playground-sweep --list-axes' for the list.");

                results.Add(PlaygroundSweepReport.Evaluate(axis, context.Seed));
            }

            for (int i = 0; i < results.Count; i++)
            {
                PlaygroundSweepReport.Write(context.Output, results[i], context.Seed);
            }

            if (results.Count > 1)
            {
                WriteTotals(context.Output, results);
            }

            if (context.Arguments.Flag("json"))
            {
                LabText.Header(context.Output, "machine-readable");
                PlaygroundSweepJson.Write(context.Output, results, context.Seed);
            }

            return AnyFailed(results) ? LabExit.ScenarioFailure : LabExit.Success;
        }

        private static bool AnyFailed(IReadOnlyList<PlaygroundSweepResult> results)
        {
            for (int i = 0; i < results.Count; i++)
            {
                if (results[i].Failed)
                {
                    return true;
                }
            }

            return false;
        }

        private static void WriteTotals(TextWriter output, IReadOnlyList<PlaygroundSweepResult> results)
        {
            int rows = 0;
            int unsupported = 0;
            int unrealized = 0;
            int inert = 0;
            int weighed = 0;
            int mutating = 0;
            int violations = 0;

            for (int i = 0; i < results.Count; i++)
            {
                rows += results[i].Evaluated;
                unsupported += results[i].Unsupported;
                unrealized += results[i].Unrealized;
                inert += results[i].NoEffect.Count;
                weighed += results[i].WeighedButUnchanged.Count;
                mutating += results[i].Mutating;
                violations += results[i].Violations.Count;
            }

            LabText.Header(output, "every family");
            output.WriteLine("  families            " + results.Count);
            output.WriteLine("  rows evaluated      " + rows);
            output.WriteLine("  unsupported points  " + unsupported);
            output.WriteLine("  unrealized acts     " + unrealized);
            output.WriteLine("  no-effect inputs    " + inert);
            output.WriteLine("  weighed, unchanged  " + weighed);
            output.WriteLine("  world mutations     " + mutating);
            output.WriteLine("  invariants          " + (violations == 0 ? "all held" : violations + " VIOLATED"));
        }

        private static string AxisHelp()
        {
            StringWriter help = new StringWriter();
            help.WriteLine("axes:");
            foreach (PlaygroundSweepAxis axis in PlaygroundSweepAxes.Default().All)
            {
                help.WriteLine("  " + LabText.Column(axis.Id, 16) + axis.Summary);
            }

            help.WriteLine("  " + LabText.Column(AllAxes, 16) + "every family above, in order");
            return help.ToString();
        }
    }
}
