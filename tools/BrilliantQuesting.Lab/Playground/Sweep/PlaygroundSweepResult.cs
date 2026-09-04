using System;
using System.Collections.Generic;

namespace BrilliantQuesting.Lab.Playground.Sweep
{
    /// <summary>
    /// One family, evaluated: its rows, what its invariants made of them, and the counts that say
    /// at a glance whether the family found anything.
    ///
    /// The counts are the diagnostic the whole layer is for. Rows and distinct semantic outcomes
    /// together say how much of the state space this family actually reaches; no-effect changes say
    /// which inputs are inert; unrealized states say where the simulation can mean something the
    /// shipped content cannot say; world mutations say which of these conversations wrote history.
    /// </summary>
    public sealed class PlaygroundSweepResult
    {
        public PlaygroundSweepResult(
            PlaygroundSweepAxis axis,
            IReadOnlyList<PlaygroundSweepRow> rows,
            IReadOnlyList<PlaygroundSweepViolation> violations)
        {
            Axis = axis ?? throw new ArgumentNullException(nameof(axis));
            Rows = rows ?? throw new ArgumentNullException(nameof(rows));
            Violations = violations ?? new PlaygroundSweepViolation[0];

            HashSet<string> semantics = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> lines = new HashSet<string>(StringComparer.Ordinal);
            List<PlaygroundSweepRow> inert = new List<PlaygroundSweepRow>();
            List<PlaygroundSweepRow> weighed = new List<PlaygroundSweepRow>();

            PlaygroundSweepRow baseline = null;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].IsBaseline)
                {
                    baseline = rows[i];
                    break;
                }
            }

            Baseline = baseline;

            for (int i = 0; i < rows.Count; i++)
            {
                PlaygroundSweepRow row = rows[i];
                if (!row.Evaluated)
                {
                    Unsupported++;
                    continue;
                }

                Evaluated++;
                semantics.Add(row.SemanticSignature);
                if (row.Turn?.Line != null && row.Turn.Line.Rendered)
                {
                    lines.Add(row.Turn.Line.Text);
                }

                if (row.Unrealized)
                {
                    Unrealized++;
                }

                if (row.MutatedTheWorld)
                {
                    Mutating++;
                }

                PlaygroundSweepRow control = ControlFor(rows, row, baseline);
                if (control == null
                    || ReferenceEquals(control, row)
                    || row.Changed.Count == 0
                    || !string.Equals(row.ObservedSignature, control.ObservedSignature, StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.Equals(row.WeighingSignature, control.WeighingSignature, StringComparison.Ordinal))
                {
                    inert.Add(row);
                }
                else
                {
                    weighed.Add(row);
                }
            }

            DistinctSemantics = semantics.Count;
            DistinctLines = lines.Count;
            NoEffect = inert;
            WeighedButUnchanged = weighed;
        }

        /// <summary>The row a given row's change is read against: its own named control, else the baseline.</summary>
        public static PlaygroundSweepRow ControlFor(
            IReadOnlyList<PlaygroundSweepRow> rows, PlaygroundSweepRow row, PlaygroundSweepRow baseline)
        {
            if (row.Against == null)
            {
                return baseline;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                if (string.Equals(rows[i].Label, row.Against, StringComparison.Ordinal))
                {
                    return rows[i];
                }
            }

            return baseline;
        }

        public PlaygroundSweepAxis Axis { get; }

        public IReadOnlyList<PlaygroundSweepRow> Rows { get; }

        /// <summary>The row every other row was read against, or null for a family with none.</summary>
        public PlaygroundSweepRow Baseline { get; }

        public IReadOnlyList<PlaygroundSweepViolation> Violations { get; }

        public int Evaluated { get; }

        /// <summary>Axis points current state cannot express, reported rather than approximated.</summary>
        public int Unsupported { get; }

        public int DistinctSemantics { get; }

        public int DistinctLines { get; }

        /// <summary>Acts the simulation composed that the shipped content had no words for.</summary>
        public int Unrealized { get; }

        /// <summary>Rows whose conversation left something durable behind.</summary>
        public int Mutating { get; }

        /// <summary>
        /// Rows that changed an input and observably changed nothing at all.
        ///
        /// Not a failure. An input with no effect is one of the four things this layer exists to
        /// find, and a family in which every row moved something would be more suspicious than one
        /// in which some did not.
        /// </summary>
        public IReadOnlyList<PlaygroundSweepRow> NoEffect { get; }

        /// <summary>
        /// Rows whose change was read - the weighing or a ruling moved - and did not carry the
        /// answer anywhere. The more interesting half of "nothing happened": the pressure exists
        /// and this situation simply was not close enough to a boundary for it to matter.
        /// </summary>
        public IReadOnlyList<PlaygroundSweepRow> WeighedButUnchanged { get; }

        public bool Failed => Violations.Count > 0;
    }
}
