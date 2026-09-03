using System;
using System.Collections.Generic;
using System.IO;
using BrilliantQuesting.Lab.Playground;

namespace BrilliantQuesting.Lab.Cli.Scenarios
{
    /// <summary>
    /// The same claim, the same seed, two worlds - and the difference laid out side by side.
    ///
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- run playground-contrast
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- run playground-contrast --left loyal-liar --right principled-refuser
    ///     dotnet run --project tools/BrilliantQuesting.Lab -- run playground-contrast --left neutral-witness --right trusted-confidant
    ///
    /// The question it exists to answer is the one a reader of a single run cannot settle: did that
    /// come out of the state, or is it what this speaker always says? Two presets over one subject
    /// answer it, because the only thing that differs between the columns is what each preset wrote
    /// into its world.
    /// </summary>
    internal sealed class PlaygroundContrastScenario : LabScenario
    {
        private const int LabelWidth = 12;
        private const int ColumnWidth = 34;

        private const string DefaultLeft = "loyal-liar";
        private const string DefaultRight = "principled-refuser";

        public override string Id => "playground-contrast";

        public override string Summary => "two authored states over one claim, decided side by side";

        public override string Description =>
            "Runs two presets against the same seed, the same claim and the same pair of people, then\n"
            + "prints their decisions, depths, tactics and lines beside each other with the differences\n"
            + "marked. The default pair holds identical pressures and differs only in the speaker's own\n"
            + "honesty and the line they keep, which is the cheapest demonstration that the tactic is a\n"
            + "reading of state rather than authored prose.\n"
            + "\n"
            + PlaygroundScenario.PresetHelp();

        public override IReadOnlyList<LabOption> Options => new[]
        {
            new LabOption("left", "preset", "the state in the left column", DefaultLeft),
            new LabOption("right", "preset", "the state in the right column", DefaultRight),
            new LabOption("speaker", "role", "who answers, in both columns", "the preset's own"),
            new LabOption("listener", "role", "who asks, in both columns", "the preset's own"),
            new LabOption("turns", "1-3", "how many exchanges to play", "the preset's own")
        };

        public override int Run(LabRunContext context)
        {
            PlaygroundRun left = Column(context, context.Arguments.String("left", DefaultLeft));
            PlaygroundRun right = Column(context, context.Arguments.String("right", DefaultRight));

            LabText.Header(context.Output, "contrast");
            context.WriteLine("seed " + context.Seed + ", one claim: " + PlaygroundText.Claim(left.Stage, left.Stage.SubjectFactId));
            context.WriteLine();

            Row(context.Output, "preset", left.Preset.Id, right.Preset.Id);
            Row(context.Output, "speaker", left.Stage.Describe(left.Speaker), right.Stage.Describe(right.Speaker));
            Row(context.Output, "listener", left.Stage.Describe(left.Listener), right.Stage.Describe(right.Listener));
            Row(context.Output, "voice", left.VoiceName, right.VoiceName);
            Row(context.Output, "tie", Tie(left), Tie(right));
            Row(context.Output, "belief", Belief(left), Belief(right));

            int turns = left.Exchange.Turns.Count < right.Exchange.Turns.Count
                ? left.Exchange.Turns.Count
                : right.Exchange.Turns.Count;

            for (int i = 0; i < turns; i++)
            {
                PlaygroundTurn a = left.Exchange.Turns[i];
                PlaygroundTurn b = right.Exchange.Turns[i];

                context.WriteLine();
                context.WriteLine("exchange " + a.Number + " - " + a.Kind);
                Row(context.Output, "strategy", Strategy(a), Strategy(b));
                Row(context.Output, "depth", Depth(a), Depth(b));
                Row(context.Output, "tactic", Tactic(a), Tactic(b));
                Row(context.Output, "balance", Balance(a), Balance(b));
                Row(context.Output, "decisive", Decisive(a), Decisive(b));
                Row(context.Output, "act", Act(a), Act(b));
                Row(context.Output, "callback", Callback(left, a), Callback(right, b));
                Row(context.Output, "line", Line(a), Line(b));
                Row(context.Output, "meaning", Meaning(a), Meaning(b));
                Row(context.Output, "durable", Durable(a), Durable(b));
            }

            context.WriteLine();
            context.WriteLine("Rows marked * differ. Both columns ran the identical code over the identical claim,");
            context.WriteLine("so every difference above is a difference in the state each preset wrote.");
            return LabExit.Success;
        }

        private PlaygroundRun Column(LabRunContext context, string preset)
        {
            PlaygroundOptions options = PlaygroundScenario.ReadOptions(context.Arguments, context.Seed);
            options.Preset = preset;
            options.Voice = context.Arguments.String("voice", null);
            return PlaygroundRun.Begin(options, PlaygroundPresets.Default());
        }

        /// <summary>
        /// One row of the comparison, marked when the two columns differ.
        ///
        /// Side by side while both values fit, stacked when a differing pair does not - a realized
        /// line is longer than any sensible column, and wrapping it into an unreadable pair of
        /// stumps would hide the exact thing the row exists to show - and printed once when the two
        /// columns agree.
        /// </summary>
        private static void Row(TextWriter output, string label, string left, string right)
        {
            left = left ?? string.Empty;
            right = right ?? string.Empty;

            // A row the two columns agree on is one value, however long it is. Printing it twice
            // would spend three lines saying that nothing differs.
            if (string.Equals(left, right, StringComparison.Ordinal))
            {
                output.WriteLine("  " + LabText.Column(label, LabelWidth) + left);
                return;
            }

            if (left.Length <= ColumnWidth && right.Length <= ColumnWidth)
            {
                output.WriteLine("* " + LabText.Column(label, LabelWidth)
                    + LabText.Column(left, ColumnWidth) + "| " + right);
                return;
            }

            output.WriteLine("* " + label);
            output.WriteLine("    left:  " + left);
            output.WriteLine("    right: " + right);
        }

        private static string Tie(PlaygroundRun run)
        {
            return PlaygroundText.Tie(run.Stage, run.Speaker, run.Listener);
        }

        private static string Belief(PlaygroundRun run)
        {
            return PlaygroundText.Belief(run.Stage, run.Speaker, run.Stage.SubjectFactId);
        }

        private static string Strategy(PlaygroundTurn turn)
        {
            return turn.Decision == null ? "no decision weighed" : turn.Decision.Strategy.ToString();
        }

        private static string Depth(PlaygroundTurn turn)
        {
            return turn.Decision == null
                ? "-"
                : turn.Decision.Depth + " of " + turn.Decision.KnownDepth + " (" + turn.Decision.Limit + ")";
        }

        private static string Tactic(PlaygroundTurn turn)
        {
            return turn.Decision == null ? "-" : turn.Decision.Tactic.ToString();
        }

        private static string Balance(PlaygroundTurn turn)
        {
            return turn.Decision == null ? "-" : turn.Decision.Balance.ToString("0.00");
        }

        private static string Decisive(PlaygroundTurn turn)
        {
            if (turn.Decision == null || turn.Decision.Decisive.Count == 0)
            {
                return "nothing on its own";
            }

            List<string> tags = new List<string>();
            for (int i = 0; i < turn.Decision.Decisive.Count; i++)
            {
                tags.Add(turn.Decision.Decisive[i].Tag);
            }

            return PlaygroundText.Join(tags, "nothing on its own");
        }

        private static string Act(PlaygroundTurn turn)
        {
            return turn.Reply == null ? "no act" : turn.Reply.Type.ToString();
        }

        private static string Callback(PlaygroundRun run, PlaygroundTurn turn)
        {
            if (turn.Callback != null)
            {
                return "allowed: " + turn.Callback.Hook.EventType;
            }

            return turn.WithheldCallback == null
                ? "none available"
                : "withheld: " + turn.WithheldCallback.Hook.EventType;
        }

        private static string Line(PlaygroundTurn turn)
        {
            if (turn.Line == null)
            {
                return "nothing worded";
            }

            return turn.Line.Rendered ? "\"" + turn.Line.Text + "\"" : "(unrealized: " + turn.Line.Refusal + ")";
        }

        private static string Meaning(PlaygroundTurn turn)
        {
            return turn.Line == null
                ? (turn.Reply == null ? "-" : turn.Reply.Signature)
                : turn.Line.Meaning;
        }

        private static string Durable(PlaygroundTurn turn)
        {
            return turn.WroteToTheLedger
                ? turn.LedgerBefore + "->" + turn.LedgerAfter + " events, "
                  + turn.ObligationsBefore + "->" + turn.ObligationsAfter + " obligations"
                : "nothing";
        }
    }
}
