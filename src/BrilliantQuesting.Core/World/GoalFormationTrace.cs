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
            : this(style, action, outcome, score, scoreTerms, ruling, string.Empty, string.Empty)
        {
        }

        public GoalActionTrace(
            ProblemSolvingStyle style,
            string action,
            string outcome,
            double score,
            IReadOnlyList<string> scoreTerms,
            ProhibitionRuling ruling,
            string registeredActionId,
            string unboundBecause)
        {
            Style = style;
            Action = action;
            Outcome = outcome;
            Score = score;
            ScoreTerms = scoreTerms;
            Ruling = ruling;
            RegisteredActionId = registeredActionId ?? string.Empty;
            UnboundBecause = unboundBecause ?? string.Empty;
        }

        public ProblemSolvingStyle Style { get; }

        /// <summary>
        /// The candidate's own name inside the problem that produced it, such as
        /// <c>missing_goat.AskAuthority</c>.
        ///
        /// A style label and a trace key, never a verb: two candidates named here may mean the
        /// same registered verb, and one of them may mean no verb at all. What the actor would
        /// actually attempt is <see cref="RegisteredActionId"/> and only that (BQ-093).
        /// </summary>
        public string Action { get; }

        public string Outcome { get; }

        /// <summary>
        /// The registered <see cref="Actions.NarrativeAction"/> this candidate would be attempted
        /// as, or empty when no registered verb means it.
        ///
        /// This is the whole of the selection/resolution join. A candidate is an *approach* -
        /// "ask authority", "pay somebody" - and is deliberately more abstract than any one verb;
        /// what it is bound to here is the concrete thing the shared library already has, so that
        /// "what did the NPC choose" has one long-term answer rather than two. Empty is a real
        /// answer with a reason in <see cref="UnboundBecause"/>: forcing an approximate verb would
        /// make the trace lie about what was attempted, and inventing an NPC-only verb would be
        /// the second vocabulary this step exists to prevent (`CD 47.5`, `PM 35`).
        /// </summary>
        public string RegisteredActionId { get; }

        /// <summary>Why no registered verb means this candidate. Empty when one does.</summary>
        public string UnboundBecause { get; }

        /// <summary>Whether this candidate can be carried out through the shared action library.</summary>
        public bool IsAttemptable => RegisteredActionId.Length > 0;

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
