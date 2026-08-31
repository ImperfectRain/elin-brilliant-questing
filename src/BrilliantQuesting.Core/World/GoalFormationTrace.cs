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
        {
            Style = style;
            Action = action;
            Outcome = outcome;
            Score = score;
            ScoreTerms = scoreTerms;
        }

        public ProblemSolvingStyle Style { get; }

        public string Action { get; }

        public string Outcome { get; }

        public double Score { get; }

        public IReadOnlyList<string> ScoreTerms { get; }
    }
}
