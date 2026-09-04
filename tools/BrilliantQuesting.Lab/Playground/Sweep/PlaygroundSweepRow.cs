using System;
using System.Collections.Generic;
using System.Text;
using BrilliantQuesting.Continuity;
using BrilliantQuesting.Dialogue;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Lab.Cli;

namespace BrilliantQuesting.Lab.Playground.Sweep
{
    /// <summary>
    /// One evaluated point of a sweep: a state, the exchange it produced, and the four layers of
    /// difference a reader needs to tell where a change actually landed.
    ///
    /// <b>Every reading is taken off a production object.</b> The strategy is the decision's, the
    /// act is the act's, the pool is the realizer's own candidate listing, the line is the realized
    /// line and the world figures are the ledger's. Nothing here parses prose, and nothing here
    /// re-derives an answer that a system already gave - a row that computed its own idea of
    /// "would they have said it" would be the second dialogue engine this whole layer exists not to
    /// become.
    ///
    /// A row that could not be built at all carries <see cref="Unsupported"/> and no run. That is
    /// the honest shape for an axis point current state cannot express: the sweep reports it as
    /// unsupported rather than approximating it with a state that means something else.
    /// </summary>
    public sealed class PlaygroundSweepRow
    {
        private static readonly string[] NoChanges = new string[0];

        private PlaygroundSweepRow(
            string label,
            bool baseline,
            IReadOnlyList<string> changed,
            PlaygroundRun run,
            int readAt,
            string against,
            string unsupported)
        {
            Label = label ?? string.Empty;
            IsBaseline = baseline;
            Changed = changed ?? NoChanges;
            Run = run;
            ReadAt = readAt;
            Against = against;
            Unsupported = unsupported;
        }

        /// <summary>Short name for the row, which is what the table's first column prints.</summary>
        public string Label { get; }

        /// <summary>Whether this is the row every other row in the family is read against.</summary>
        public bool IsBaseline { get; }

        /// <summary>Exactly which state this row moved relative to the baseline. Empty for the baseline.</summary>
        public IReadOnlyList<string> Changed { get; }

        public PlaygroundRun Run { get; }

        /// <summary>Which exchange the row's readings are taken from, one-based.</summary>
        public int ReadAt { get; }

        /// <summary>
        /// The label of the row this one's change is measured against, or null for the family's
        /// baseline.
        ///
        /// A family that sets up more than one controlled situation - a lying line and a kinship
        /// line have nothing to say to each other - needs each row read against its own control,
        /// and a no-effect count taken against the wrong control would be nonsense rather than a
        /// finding.
        /// </summary>
        public string Against { get; }

        /// <summary>Why this axis point cannot be represented by current state, or null when it can.</summary>
        public string Unsupported { get; }

        public bool Evaluated => Run != null;

        public static PlaygroundSweepRow Of(
            string label,
            bool baseline,
            IReadOnlyList<string> changed,
            PlaygroundRun run,
            int readAt = 1,
            string against = null)
        {
            return new PlaygroundSweepRow(label, baseline, changed, run, readAt, against, null);
        }

        /// <summary>A point of the axis current production state has no way to express.</summary>
        public static PlaygroundSweepRow NotSupported(string label, string because)
        {
            return new PlaygroundSweepRow(label, false, NoChanges, null, 1, null, because);
        }

        /// <summary>The exchange this row reports on, or null when the run played fewer.</summary>
        public PlaygroundTurn Turn
        {
            get
            {
                if (Run == null)
                {
                    return null;
                }

                IReadOnlyList<PlaygroundTurn> turns = Run.Exchange.Turns;
                int index = ReadAt - 1;
                return index >= 0 && index < turns.Count ? turns[index] : null;
            }
        }

        // -- semantic difference ---------------------------------------------------------------

        public string Strategy => Turn?.Decision == null ? "-" : Turn.Decision.Strategy.ToString();

        public string Depth
        {
            get
            {
                DisclosureDecision decision = Turn?.Decision;
                return decision == null
                    ? "-"
                    : decision.Depth + " of " + decision.KnownDepth + " (" + decision.Limit + ")";
            }
        }

        public string Tactic => Turn?.Decision == null ? "-" : Turn.Decision.Tactic.ToString();

        public string Act
        {
            get
            {
                SpeechAct reply = Turn?.Reply;
                return reply == null ? "no act" : reply.Type + "/" + reply.Stance + "/" + reply.Direction;
            }
        }

        /// <summary>The decision's arithmetic, and the standing the depth ceiling was banded from.</summary>
        public string Balance
        {
            get
            {
                DisclosureDecision decision = Turn?.Decision;
                return decision == null
                    ? "-"
                    : decision.Balance.ToString("+0.00;-0.00;0.00")
                      + " standing " + decision.Standing.ToString("+0.00;-0.00;0.00");
            }
        }

        /// <summary>The pressures whose removal on its own would have changed the strategy.</summary>
        public string Decisive
        {
            get
            {
                DisclosureDecision decision = Turn?.Decision;
                if (decision == null || decision.Decisive.Count == 0)
                {
                    return "nothing on its own";
                }

                List<string> tags = new List<string>();
                for (int i = 0; i < decision.Decisive.Count; i++)
                {
                    tags.Add(decision.Decisive[i].Tag);
                }

                return string.Join(", ", tags);
            }
        }

        /// <summary>Every line that bore on the decision, and what it did. Empty when none did.</summary>
        public string Rulings
        {
            get
            {
                DisclosureDecision decision = Turn?.Decision;
                if (decision == null || decision.Prohibitions.Count == 0)
                {
                    return "no line bore on this";
                }

                List<string> rulings = new List<string>();
                for (int i = 0; i < decision.Prohibitions.Count; i++)
                {
                    rulings.Add(decision.Prohibitions[i].ToString());
                }

                return string.Join("; ", rulings);
            }
        }

        /// <summary>Recall and permission kept apart, which is BQ-081's own boundary.</summary>
        public string Callback
        {
            get
            {
                PlaygroundTurn turn = Turn;
                if (turn == null)
                {
                    return "-";
                }

                if (turn.Callback != null)
                {
                    return "cleared " + turn.Callback.Hook.PrimaryKind + " via " + turn.Callback.Hook.Route;
                }

                return turn.WithheldCallback == null
                    ? "no material"
                    : "withheld " + turn.WithheldCallback.Hook.PrimaryKind + " via " + turn.WithheldCallback.Hook.Route;
            }
        }

        public string Recurrence
        {
            get
            {
                CallbackPermit recurrence = Turn?.Recurrence;
                return recurrence == null
                    ? "none"
                    : recurrence.Hook.PrimaryKind + " via " + recurrence.Hook.Route
                      + (recurrence.Allowed ? " (allowed)" : " (withheld)");
            }
        }

        /// <summary>What BQ-073 makes of the act, asked of Deception rather than guessed.</summary>
        public string Veracity
        {
            get
            {
                PlaygroundTurn turn = Turn;
                if (turn?.Reply == null || Run == null)
                {
                    return "-";
                }

                BrilliantQuesting.Dialogue.Veracity assessed = Deception.Assess(Run.Stage.World, turn.Reply);
                return assessed.Sincerity + ", what was put forward reads " + assessed.Accuracy;
            }
        }

        public string Reaction
        {
            get
            {
                ActorReaction reaction = Turn?.Reaction;
                return reaction == null
                    ? "not derived for this exchange"
                    : reaction.Concern + " -> " + reaction.Response
                      + " at " + reaction.Intensity.ToString("0.00");
            }
        }

        // -- expression difference -------------------------------------------------------------

        public string Tone => PlaygroundText.Join(Turn?.Request?.Tone, "none requested");

        public string Idiolect => PlaygroundText.Join(Turn?.Request?.Idiolect, "none requested");

        public string Vocabulary => PlaygroundText.Join(Turn?.Request?.Vocabulary, "none requested");

        public string Forbidden => PlaygroundText.Join(Turn?.Request?.Forbidden, "nothing ruled out");

        public string EligibleBySlot => Turn?.Eligible == null ? "nothing worded" : Turn.Eligible.Describe();

        public string Line
        {
            get
            {
                RealizedLine line = Turn?.Line;
                if (line == null)
                {
                    return Turn?.Reply == null ? "nothing said" : "nothing worded";
                }

                return line.Rendered ? "\"" + line.Text + "\"" : "(unrealized: " + line.Refusal + ")";
            }
        }

        public string Core => Turn?.Line == null ? "-" : (Turn.Line.Core.Length == 0 ? "-" : Turn.Line.Core);

        public string Fragments => PlaygroundText.Join(Turn?.Line?.Fragments, "none");

        /// <summary>What the act means, which is the act's own signature and never the wording's.</summary>
        public string Meaning
        {
            get
            {
                if (Turn?.Line != null)
                {
                    return Turn.Line.Meaning;
                }

                return Turn?.Reply == null ? "-" : Turn.Reply.Signature;
            }
        }

        /// <summary>Whether the act reached words at all: the unrealized-state count reads this.</summary>
        public bool Unrealized => Turn?.Reply != null && (Turn.Line == null || !Turn.Line.Rendered);

        // -- world difference ------------------------------------------------------------------

        /// <summary>Everything durable that moved across the whole exchange, or an empty list.</summary>
        public IReadOnlyList<string> WorldMoved =>
            Run == null ? NoChanges : Run.Exchange.After.Since(Run.Exchange.Before);

        public bool MutatedTheWorld => WorldMoved.Count > 0;

        public string World
        {
            get
            {
                IReadOnlyList<string> moved = WorldMoved;
                return moved.Count == 0 ? "nothing durable" : string.Join(", ", moved);
            }
        }

        public string Conversation
        {
            get
            {
                PlaygroundTurn turn = Turn;
                if (turn == null || Run == null)
                {
                    return "-";
                }

                StringBuilder sb = new StringBuilder();
                sb.Append(turn.ActsNoted).Append(" act(s), ").Append(turn.Unanswered).Append(" unanswered");
                if (turn.AlreadyAsked)
                {
                    sb.Append(", repeated question");
                }

                if (turn.Contradiction != null)
                {
                    sb.Append(", contradiction: ").Append(turn.Contradiction.Value.Because);
                }

                if (turn.RecordedDeception != null)
                {
                    sb.Append(", deception filed");
                }

                if (turn.Committed != null)
                {
                    sb.Append(", promise promoted");
                }

                return sb.ToString();
            }
        }

        // -- distinctness ----------------------------------------------------------------------

        /// <summary>
        /// Everything the semantic layers settled, as one string.
        ///
        /// Used only for counting how many genuinely different answers a family produced, and
        /// deliberately excluding wording: two rows that reached the same decision and said it two
        /// ways are one semantic outcome, and a summary that counted them as two would report
        /// expressive variety as if it were semantic range.
        /// </summary>
        public string SemanticSignature =>
            Strategy + "|" + Depth + "|" + Tactic + "|" + Act + "|" + Callback + "|" + Recurrence + "|" + Veracity;

        /// <summary>Everything a row could observe, for deciding whether an input changed the outcome.</summary>
        public string ObservedSignature =>
            SemanticSignature + "|" + Tone + "|" + Idiolect + "|" + Vocabulary + "|" + Forbidden + "|" + EligibleBySlot
            + "|" + Line + "|" + World + "|" + Conversation;

        /// <summary>
        /// The arithmetic behind the outcome, kept apart from it.
        ///
        /// An input that moved the weighing without moving the answer is a different finding from
        /// one that moved nothing at all - the first is a pressure the model reads and this
        /// situation was not close enough to be turned by, and the second is an input nothing
        /// reads. A summary that called both "no effect" would hide the more interesting one.
        /// </summary>
        public string WeighingSignature =>
            Turn?.Decision == null ? "-" : Balance + "|" + Rulings;

        public override string ToString() => Label + ": " + SemanticSignature;
    }
}
