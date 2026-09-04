using System;
using System.Collections.Generic;

namespace BrilliantQuesting.Lab.Playground.Sweep
{
    /// <summary>
    /// A statement about the rows that has to be true, checked over a finished family.
    ///
    /// The reason the sweep is a scenario that can fail rather than a report somebody reads: a
    /// table nobody checks is a table that stops being read, and the properties worth holding here
    /// - meaning does not move with wording, a withheld memory never reaches words, no belief never
    /// becomes knowledge - are exactly the ones a quiet regression would break without changing the
    /// shape of the output.
    ///
    /// A violation is reported with the row that broke it and fails the run with
    /// <see cref="Cli.LabExit.ScenarioFailure"/>.
    /// </summary>
    public sealed class PlaygroundSweepInvariant
    {
        public static readonly IReadOnlyList<PlaygroundSweepInvariant> None = new PlaygroundSweepInvariant[0];

        private readonly Func<IReadOnlyList<PlaygroundSweepRow>, IReadOnlyList<string>> _check;

        public PlaygroundSweepInvariant(
            string name, Func<IReadOnlyList<PlaygroundSweepRow>, IReadOnlyList<string>> check)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _check = check ?? throw new ArgumentNullException(nameof(check));
        }

        /// <summary>What is being asserted, in the words the report prints.</summary>
        public string Name { get; }

        /// <summary>Every way these rows broke it, or an empty list when they did not.</summary>
        public IReadOnlyList<string> Check(IReadOnlyList<PlaygroundSweepRow> rows)
        {
            return _check(rows) ?? new string[0];
        }

        /// <summary>The check every family is held to, whatever it sweeps.</summary>
        public static IReadOnlyList<PlaygroundSweepInvariant> Universal { get; } = new[]
        {
            new PlaygroundSweepInvariant(
                "a realized line means exactly what its act meant",
                rows => Each(rows, row =>
                {
                    if (row.Turn?.Line == null || row.Turn.Reply == null)
                    {
                        return null;
                    }

                    return row.Turn.Line.Meaning == row.Turn.Reply.Signature
                        ? null
                        : "wording reported meaning '" + row.Turn.Line.Meaning
                          + "' for act '" + row.Turn.Reply.Signature + "'";
                })),

            new PlaygroundSweepInvariant(
                "disclosure never goes deeper than the speaker's own belief",
                rows => Each(rows, row =>
                {
                    if (row.Turn?.Decision == null)
                    {
                        return null;
                    }

                    return row.Turn.Decision.Depth <= row.Turn.Decision.KnownDepth
                        ? null
                        : "depth " + row.Turn.Decision.Depth + " above known depth " + row.Turn.Decision.KnownDepth;
                })),

            new PlaygroundSweepInvariant(
                "a speaker who holds no belief discloses nothing and composes no act",
                rows => Each(rows, row =>
                {
                    if (row.Run == null || row.Turn?.Decision == null)
                    {
                        return null;
                    }

                    bool holds = row.Run.Stage.World.Knowledge.Knows(row.Run.Speaker, row.Run.Stage.SubjectFactId);
                    if (holds)
                    {
                        return null;
                    }

                    if (row.Turn.Decision.Strategy != BrilliantQuesting.Dialogue.DisclosureStrategy.NothingToDisclose)
                    {
                        return "no belief, yet the decision came out " + row.Turn.Decision.Strategy;
                    }

                    return row.Turn.Reply == null ? null : "no belief, yet an act was composed: " + row.Turn.Reply.Type;
                })),

            new PlaygroundSweepInvariant(
                "a withheld memory never reaches the wording layer",
                rows => Each(rows, row =>
                {
                    if (row.Turn?.Request == null)
                    {
                        return null;
                    }

                    BrilliantQuesting.Dialogue.CallbackPermit permit = row.Turn.Request.Callback;
                    if (permit == null || permit.Allowed)
                    {
                        return null;
                    }

                    return "a withheld permit was handed to the realizer: " + permit.Because;
                })),

            new PlaygroundSweepInvariant(
                "a required core always found words, however often it had been said",
                rows => Each(rows, row =>
                {
                    if (row.Turn?.Reply == null || row.Turn.Request == null)
                    {
                        return null;
                    }

                    if (row.Turn.Line != null && row.Turn.Line.Rendered)
                    {
                        return null;
                    }

                    // A pool that was empty from the start is a content-coverage fact and is
                    // reported as one; what this refuses is a line lost to repetition alone.
                    int cores = row.Turn.Eligible == null
                        ? 0
                        : row.Turn.Eligible.CountAt(BrilliantQuesting.Dialogue.FragmentPosition.Core);

                    return cores == 0 ? null : "a core pool of " + cores + " produced no line";
                }))
        };

        /// <summary>Runs a predicate over every evaluated row and gathers what it complained about.</summary>
        public static IReadOnlyList<string> Each(
            IReadOnlyList<PlaygroundSweepRow> rows, Func<PlaygroundSweepRow, string> check)
        {
            List<string> broken = new List<string>();
            for (int i = 0; i < rows.Count; i++)
            {
                if (!rows[i].Evaluated)
                {
                    continue;
                }

                string complaint = check(rows[i]);
                if (complaint != null)
                {
                    broken.Add(rows[i].Label + ": " + complaint);
                }
            }

            return broken;
        }
    }

    /// <summary>One broken invariant, with the family and the rows that broke it.</summary>
    public sealed class PlaygroundSweepViolation
    {
        public PlaygroundSweepViolation(string axis, string invariant, string detail)
        {
            Axis = axis;
            Invariant = invariant;
            Detail = detail;
        }

        public string Axis { get; }

        public string Invariant { get; }

        public string Detail { get; }

        public override string ToString() => Axis + " / " + Invariant + " - " + Detail;
    }
}
