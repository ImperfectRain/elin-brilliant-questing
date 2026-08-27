using System.Collections.Generic;
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
        {
            ProfileId = profileId;
            BaseDifficulty = baseDifficulty;
            Terms = terms;
            FinalDifficulty = finalDifficulty;
            Roll = roll;
            Outcome = outcome;
        }

        public string ProfileId { get; }

        public int BaseDifficulty { get; }

        public IReadOnlyList<CheckTerm> Terms { get; }

        public int FinalDifficulty { get; }

        public int Roll { get; }

        public CheckOutcome Outcome { get; }

        public bool Succeeded => Outcome.IsSuccess();

        /// <summary>Human-readable trace for the debug inspector and the test output.</summary>
        public string Explain()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("check ").Append(ProfileId).Append(": base ").Append(BaseDifficulty);
            foreach (CheckTerm term in Terms)
            {
                sb.Append(term.Delta >= 0 ? " +" : " ").Append(term.Delta).Append(" (").Append(term.Label).Append(')');
            }

            sb.Append(" => DC ").Append(FinalDifficulty);
            sb.Append("; rolled ").Append(Roll).Append(" => ").Append(Outcome);
            return sb.ToString();
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
