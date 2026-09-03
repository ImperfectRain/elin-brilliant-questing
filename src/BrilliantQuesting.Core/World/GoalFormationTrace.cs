using System.Collections.Generic;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.World
{
    public sealed class GoalFormationTrace
    {
        public GoalFormationTrace(
            EntityId actorId,
            string problem,
            NarrativeNeed need,
            ValueConcern valueConcern,
            double needPressure,
            string desire,
            NpcGoal candidateGoal,
            GoalActionTrace chosenAction)
        {
            ActorId = actorId;
            Problem = problem;
            Need = need;
            ValueConcern = valueConcern;
            NeedPressure = needPressure;
            Desire = desire;
            CandidateGoal = candidateGoal;
            ChosenAction = chosenAction;
            CandidateActions = new List<GoalActionTrace>();
        }

        public EntityId ActorId { get; }

        public string Problem { get; }

        public NarrativeNeed Need { get; }

        public ValueConcern ValueConcern { get; }

        public double NeedPressure { get; }

        public string Desire { get; }

        public NpcGoal CandidateGoal { get; }

        public List<GoalActionTrace> CandidateActions { get; }

        public GoalActionTrace ChosenAction { get; }
    }

    public sealed class GoalActionTrace
    {
        public GoalActionTrace(
            ProblemSolvingStyle style,
            string action,
            string outcome,
            double score,
            IReadOnlyList<string> scoreTerms)
            : this(style, action, outcome, score, scoreTerms, ProhibitionRuling.NotHeld(default(PersonalProhibition)))
        {
        }

        public GoalActionTrace(
            ProblemSolvingStyle style,
            string action,
            string outcome,
            double score,
            IReadOnlyList<string> scoreTerms,
            ProhibitionRuling ruling)
        {
            Style = style;
            Action = action;
            Outcome = outcome;
            Score = score;
            ScoreTerms = scoreTerms;
            Ruling = ruling;
        }

        public ProblemSolvingStyle Style { get; }

        public string Action { get; }

        public string Outcome { get; }

        /// <summary>
        /// What this action scored. Computed for every candidate, including one a personal line
        /// forbids: the score is what makes the cost of a prohibition visible, so it is never
        /// suppressed for a candidate that was taken off the table (BQ-077).
        /// </summary>
        public double Score { get; }

        public IReadOnlyList<string> ScoreTerms { get; }

        /// <summary>
        /// What the actor's negative space did to this candidate (BQ-077), or a not-held ruling
        /// when no line bears on it.
        ///
        /// <see cref="ProhibitionRuling.Forbids"/> means this candidate was not eligible to be
        /// chosen however well it scored, and <see cref="ProhibitionRuling.Broke"/> means a line
        /// that bore on it gave way under the need pressure this trace already reports.
        /// </summary>
        public ProhibitionRuling Ruling { get; }

        /// <summary>Whether a line took this candidate off the table.</summary>
        public bool Forbidden => Ruling.Forbids;
    }
}
