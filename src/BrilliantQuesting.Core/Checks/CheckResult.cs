using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BrilliantQuesting.Checks
{
    /// <summary>
    /// A resolved check together with every term that produced it.
    ///
    /// The design document's explainability requirement lives here: a debug view must be able to
    /// answer "why did that happen" without re-running the simulation, so the resolver records
    /// its arithmetic instead of just its verdict.
    /// </summary>
    public sealed class CheckResult
    {
        public CheckResult(string profileId, int baseDifficulty, IReadOnlyList<CheckTerm> terms, int finalDifficulty, int roll, CheckOutcome outcome)
            : this(profileId, baseDifficulty, terms, finalDifficulty, roll, outcome, null)
        {
        }

        public CheckResult(
            string profileId,
            int baseDifficulty,
            IReadOnlyList<CheckTerm> terms,
            int finalDifficulty,
            int roll,
            CheckOutcome outcome,
            OpposedPowerTrace opposition)
        {
            ProfileId = profileId;
            BaseDifficulty = baseDifficulty;
            Terms = terms;
            FinalDifficulty = finalDifficulty;
            Roll = roll;
            Outcome = outcome;
            Opposition = opposition;
        }

        public string ProfileId { get; }

        public int BaseDifficulty { get; }

        public IReadOnlyList<CheckTerm> Terms { get; }

        /// <summary>
        /// How relative power moved an opposed check's difficulty, or null where it did not apply
        /// (BQa-004). Present on every opposed resolution and on no absolute one, because an
        /// opposed DC is a ratio the list of deltas above cannot show on its own. A resolver that
        /// did not compute a ratio - the native diagnostic path, a test double - leaves it null.
        /// </summary>
        public OpposedPowerTrace Opposition { get; }

        public int FinalDifficulty { get; }

        /// <summary>
        /// Roll value meaning "resolved elsewhere". Vanilla's Check hands back an outcome without
        /// the face it rolled, and printing that as a zero would read as a roll of zero.
        /// </summary>
        public const int UnknownRoll = -1;

        public int Roll { get; }

        public bool RollIsKnown => Roll >= 0;

        public CheckOutcome Outcome { get; }

        public bool Succeeded => Outcome.IsSuccess();

        /// <summary>Human-readable trace for the debug inspector and the test output.</summary>
        public string Explain()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("check ").Append(ProfileId).Append(": base ").Append(BaseDifficulty);
            AppendOpposition(sb);
            foreach (CheckTerm term in Terms)
            {
                sb.Append(term.Delta >= 0 ? " +" : " ").Append(term.Delta).Append(" (").Append(term.Label).Append(')');
            }

            sb.Append(" => DC ").Append(FinalDifficulty);
            sb.Append(RollIsKnown ? "; rolled " + Roll : "; rolled by the game");
            sb.Append(" => ").Append(Outcome);
            return sb.ToString();
        }

        /// <summary>
        /// Spells the power composites out inside the trace, so an opposed DC reads as the ratio
        /// it is rather than as one unexplained "opposed power" number.
        /// </summary>
        private void AppendOpposition(StringBuilder sb)
        {
            if (Opposition == null)
            {
                return;
            }

            sb.Append(" [power ");
            AppendSide(sb, false, Opposition.ActorPower, Opposition.StabilizedActorPower);
            sb.Append(" vs ");
            AppendSide(sb, true, Opposition.TargetPower, Opposition.StabilizedTargetPower);
            sb.Append(" = ").Append(Opposition.PowerBands >= 0 ? "+" : string.Empty)
                .Append(Number(Opposition.PowerBands)).Append(" bands]");
        }

        private void AppendSide(StringBuilder sb, bool opposing, double power, double stabilized)
        {
            sb.Append(Number(power));

            bool opened = false;
            foreach (CheckPowerTerm term in Opposition.Contributions)
            {
                if (term.IsOpposing != opposing)
                {
                    continue;
                }

                sb.Append(opened ? ", " : " (").Append(term.Label).Append(' ').Append(Number(term.Value));
                opened = true;
            }

            if (opened)
            {
                sb.Append(')');
            }

            // Said out loud, because otherwise "3.8 vs 0" reads as arithmetic that does not add
            // up: the ratio saw the floor, not the zero, and a trace that hid that would be
            // explaining a number nobody could reproduce from it.
            if (stabilized != power)
            {
                sb.Append(" floored to ").Append(Number(stabilized));
            }
        }

        /// <summary>Invariant so a trace reads the same whatever locale the game is running in.</summary>
        private static string Number(double value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }

    public readonly struct CheckTerm
    {
        public CheckTerm(string label, int delta)
        {
            Label = label;
            Delta = delta;
        }

        public string Label { get; }

        public int Delta { get; }
    }
}
