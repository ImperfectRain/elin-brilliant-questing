using System;
using System.Collections.Generic;
using System.IO;
using BrilliantQuesting.Lab.Cli;

namespace BrilliantQuesting.Lab.Playground.Sweep
{
    /// <summary>
    /// Runs a family and prints it.
    ///
    /// The order is the order the question is asked in: what is held still, what each row moved,
    /// what the semantic layers made of it, what the wording layers made of it, what any of it left
    /// behind - and then the counts, which are the part somebody watching for regressions reads.
    ///
    /// <b>Nothing here runs a system.</b> The rows are finished before the first line is written,
    /// exactly as <see cref="PlaygroundReporter"/> requires of the single-run reporters, and for
    /// the same reason: a report that could still change an answer would make the report part of
    /// the experiment.
    /// </summary>
    public static class PlaygroundSweepReport
    {
        private const int LabelWidth = 24;

        /// <summary>Builds the rows, checks the invariants, and hands back the finished family.</summary>
        public static PlaygroundSweepResult Evaluate(PlaygroundSweepAxis axis, ulong seed)
        {
            if (axis == null)
            {
                throw new ArgumentNullException(nameof(axis));
            }

            IReadOnlyList<PlaygroundSweepRow> rows = axis.Rows(seed);
            List<PlaygroundSweepViolation> violations = new List<PlaygroundSweepViolation>();

            Check(axis, PlaygroundSweepInvariant.Universal, rows, violations);
            Check(axis, axis.Invariants, rows, violations);

            return new PlaygroundSweepResult(axis, rows, violations);
        }

        public static void Write(TextWriter output, PlaygroundSweepResult result, ulong seed)
        {
            PlaygroundSweepAxis axis = result.Axis;
            LabText.Header(output, "sweep: " + axis.Id);
            output.WriteLine(axis.Question);
            output.WriteLine("held still: " + axis.Held);
            output.WriteLine("seed " + seed);

            if (axis.PrintsRowTable)
            {
                WriteRows(output, result);
            }

            axis.WriteTail(output, result);
            WriteSummary(output, result);
        }

        private static void WriteRows(TextWriter output, PlaygroundSweepResult result)
        {
            for (int i = 0; i < result.Rows.Count; i++)
            {
                PlaygroundSweepRow row = result.Rows[i];
                output.WriteLine();

                if (!row.Evaluated)
                {
                    output.WriteLine("- " + row.Label + "   UNSUPPORTED");
                    output.WriteLine("    " + Wrap(row.Unsupported, "    "));
                    continue;
                }

                PlaygroundSweepRow control = PlaygroundSweepResult.ControlFor(result.Rows, row, result.Baseline);
                bool moved = control == null
                    || ReferenceEquals(control, row)
                    || !string.Equals(row.ObservedSignature, control.ObservedSignature, StringComparison.Ordinal);

                bool weighedDifferently = control != null
                    && !ReferenceEquals(control, row)
                    && !string.Equals(row.WeighingSignature, control.WeighingSignature, StringComparison.Ordinal);

                output.WriteLine((moved ? "* " : "= ") + row.Label
                    + (row.IsBaseline ? "   (baseline)" : string.Empty)
                    + (moved || row.IsBaseline
                        ? string.Empty
                        : "   same outcome as " + control.Label
                          + (weighedDifferently ? ", though the weighing moved" : ", and the weighing did not move")));

                WriteChanges(output, row);
                Field(output, "strategy", row.Strategy);
                Field(output, "depth", row.Depth);
                Field(output, "tactic", row.Tactic);
                Field(output, "act", row.Act);
                Field(output, "balance", row.Balance);
                Field(output, "decisive", row.Decisive);
                Field(output, "lines held", row.Rulings);
                Field(output, "callback", row.Callback);
                Field(output, "recurrence", row.Recurrence);
                Field(output, "veracity", row.Veracity);
                Field(output, "reaction", row.Reaction);
                Field(output, "tone", row.Tone);
                Field(output, "vocabulary", row.Vocabulary);
                Field(output, "forbidden", row.Forbidden);
                Field(output, "eligible", row.EligibleBySlot);
                Field(output, "said", row.Line);
                Field(output, "fragments", row.Fragments);
                Field(output, "meaning", row.Meaning);
                Field(output, "conversation", row.Conversation);
                Field(output, "world", row.World);
            }
        }

        private static void WriteChanges(TextWriter output, PlaygroundSweepRow row)
        {
            if (row.Changed.Count == 0)
            {
                Field(output, "input", row.IsBaseline ? "the state this family is read against" : "nothing moved");
                return;
            }

            Field(output, "input", row.Changed[0]);
            for (int i = 1; i < row.Changed.Count; i++)
            {
                Field(output, string.Empty, row.Changed[i]);
            }
        }

        private static void WriteSummary(TextWriter output, PlaygroundSweepResult result)
        {
            output.WriteLine();
            output.WriteLine("summary");
            Field(output, "rows evaluated", result.Evaluated.ToString()
                + (result.Unsupported == 0 ? string.Empty : ", plus " + result.Unsupported + " unsupported"));
            Field(output, "distinct semantic", result.DistinctSemantics.ToString());
            Field(output, "distinct lines", result.DistinctLines.ToString());
            Field(output, "unrealized", result.Unrealized == 0
                ? "none - every act the simulation composed had words"
                : result.Unrealized + " act(s) the shipped content had no wording for");
            Field(output, "no-effect inputs", result.NoEffect.Count == 0
                ? "none - every row that changed an input moved at least the weighing"
                : Labels(result.NoEffect));
            Field(output, "weighed, unchanged", result.WeighedButUnchanged.Count == 0
                ? "none"
                : Labels(result.WeighedButUnchanged));
            Field(output, "world mutations", result.Mutating == 0
                ? "none - no conversation in this family left anything durable"
                : result.Mutating + " row(s) left something durable behind");
            Field(output, "invariants", result.Violations.Count == 0
                ? "all held"
                : result.Violations.Count + " VIOLATED");

            for (int i = 0; i < result.Violations.Count; i++)
            {
                output.WriteLine("  VIOLATION  " + result.Violations[i].Invariant);
                output.WriteLine("             " + result.Violations[i].Detail);
            }
        }

        private static void Check(
            PlaygroundSweepAxis axis,
            IReadOnlyList<PlaygroundSweepInvariant> invariants,
            IReadOnlyList<PlaygroundSweepRow> rows,
            List<PlaygroundSweepViolation> into)
        {
            for (int i = 0; i < invariants.Count; i++)
            {
                IReadOnlyList<string> broken = invariants[i].Check(rows);
                for (int j = 0; j < broken.Count; j++)
                {
                    into.Add(new PlaygroundSweepViolation(axis.Id, invariants[i].Name, broken[j]));
                }
            }
        }

        private static string Labels(IReadOnlyList<PlaygroundSweepRow> rows)
        {
            List<string> labels = new List<string>();
            for (int i = 0; i < rows.Count; i++)
            {
                labels.Add(rows[i].Label);
            }

            return rows.Count + ": " + string.Join(" | ", labels);
        }

        private static void Field(TextWriter output, string name, string value)
        {
            output.WriteLine("    " + LabText.Column(name.Length == 0 ? string.Empty : name + ":", LabelWidth) + value);
        }

        /// <summary>Folds a long explanation onto the report's own indent. Presentation only.</summary>
        private static string Wrap(string text, string indent)
        {
            return (text ?? string.Empty).Replace("\n", "\n" + indent);
        }
    }
}
