using System;
using System.Collections.Generic;
using BrilliantQuesting.Foundation;

namespace BrilliantQuesting.World
{
    public enum MissingGoatResponse
    {
        ReportToGuards,
        AskNeighbors,
        OfferPayment,
        AccuseRival,
        StealReplacement,
        PrayForReturn,
        ComplainAndWait
    }

    public sealed class MissingGoatProblem
    {
        public static readonly MissingGoatProblem OrdinaryLoss = new MissingGoatProblem(
            isAnimalAtRisk: true,
            isPubliclyEmbarrassing: true,
            threatensStatus: true,
            threatensFamily: false);

        public MissingGoatProblem(bool isAnimalAtRisk, bool isPubliclyEmbarrassing, bool threatensStatus)
            : this(isAnimalAtRisk, isPubliclyEmbarrassing, threatensStatus, false)
        {
        }

        public MissingGoatProblem(bool isAnimalAtRisk, bool isPubliclyEmbarrassing, bool threatensStatus, bool threatensFamily)
        {
            IsAnimalAtRisk = isAnimalAtRisk;
            IsPubliclyEmbarrassing = isPubliclyEmbarrassing;
            ThreatensStatus = threatensStatus;
            ThreatensFamily = threatensFamily;
        }

        public bool IsAnimalAtRisk { get; }

        public bool IsPubliclyEmbarrassing { get; }

        public bool ThreatensStatus { get; }

        public bool ThreatensFamily { get; }
    }

    public sealed class MissingGoatDecision
    {
        public MissingGoatDecision(ProblemSolvingStyle style, MissingGoatResponse response, double score, NpcGoal goal)
            : this(style, response, score, goal, ProhibitionRuling.NotHeld(default(PersonalProhibition)))
        {
        }

        public MissingGoatDecision(
            ProblemSolvingStyle style,
            MissingGoatResponse response,
            double score,
            NpcGoal goal,
            ProhibitionRuling ruling)
        {
            Style = style;
            Response = response;
            Score = score;
            Goal = goal;
            Ruling = ruling;
        }

        public ProblemSolvingStyle Style { get; }

        public MissingGoatResponse Response { get; }

        public double Score { get; }

        public NpcGoal Goal { get; }

        /// <summary>
        /// What a personal line did to the action that was chosen (BQ-077). A not-held ruling for
        /// an actor with no line bearing on it, and a broken one when the actor holds a line
        /// against this very action and the need pressure carried it. Never a forbidding ruling:
        /// a forbidden candidate is not chosen.
        /// </summary>
        public ProhibitionRuling Ruling { get; }
    }

    public static class MissingGoatProblemSolver
    {
        private static readonly Candidate[] Candidates =
        {
            new Candidate(ProblemSolvingStyle.AskAuthority, MissingGoatResponse.ReportToGuards),
            new Candidate(ProblemSolvingStyle.AskFriends, MissingGoatResponse.AskNeighbors),
            new Candidate(ProblemSolvingStyle.PaySomeone, MissingGoatResponse.OfferPayment),
            new Candidate(ProblemSolvingStyle.Manipulate, MissingGoatResponse.AccuseRival),
            new Candidate(ProblemSolvingStyle.Conceal, MissingGoatResponse.StealReplacement),
            new Candidate(ProblemSolvingStyle.SeekReligiousHelp, MissingGoatResponse.PrayForReturn),
            new Candidate(ProblemSolvingStyle.Wait, MissingGoatResponse.ComplainAndWait)
        };

        public static MissingGoatDecision Choose(NarrativeNpc actor)
        {
            return Choose(actor, MissingGoatProblem.OrdinaryLoss);
        }

        public static MissingGoatDecision Choose(NarrativeNpc actor, MissingGoatProblem problem)
        {
            GoalFormationTrace trace = Trace(actor, problem, EntityId.None);
            return new MissingGoatDecision(
                trace.ChosenAction.Style,
                (MissingGoatResponse)Enum.Parse(typeof(MissingGoatResponse), trace.ChosenAction.Outcome),
                trace.ChosenAction.Score,
                trace.CandidateGoal,
                trace.ChosenAction.Ruling);
        }

        public static GoalFormationTrace Trace(NarrativeNpc actor, MissingGoatProblem problem, EntityId subject)
        {
            if (actor == null)
            {
                throw new ArgumentNullException(nameof(actor));
            }

            if (problem == null)
            {
                throw new ArgumentNullException(nameof(problem));
            }

            ValueConcern concern = DominantConcern(actor, problem);
            NarrativeNeed need = NeedFor(concern);
            double pressure = Pressure(actor, problem, concern);

            // BQ-077. Every candidate is still scored, because the cost of a personal line is only
            // visible beside the score of the action it took away; what a line changes is which
            // candidates are eligible to win, not what any of them is worth. The pressure a
            // breakable line is weighed against is the need pressure already derived above from
            // the threatened value - the actor's own stake in this problem, not a second reading
            // of it invented for prohibitions.
            string stake = "need " + NeedName(need) + " from threatened value " + ConcernName(concern);

            int bestIndex = -1;
            double bestTotal = 0.0;
            int bestOverall = 0;
            double bestOverallTotal = double.NegativeInfinity;
            List<GoalActionTrace> actions = new List<GoalActionTrace>();

            for (int i = 0; i < Candidates.Length; i++)
            {
                ScoreBreakdown score = Score(actor, problem, Candidates[i].Style);
                ProhibitionRuling ruling = NegativeSpace.Rule(
                    actor.NegativeSpace,
                    Candidates[i].Style,
                    pressure,
                    stake);
                actions.Add(ActionTrace(Candidates[i], score, ruling));

                if (score.Total > bestOverallTotal)
                {
                    bestOverall = i;
                    bestOverallTotal = score.Total;
                }

                if (ruling.Forbids)
                {
                    continue;
                }

                if (bestIndex < 0 || score.Total > bestTotal)
                {
                    bestIndex = i;
                    bestTotal = score.Total;
                }
            }

            // Unreachable with this vocabulary: `Wait` is always a candidate and no prohibition
            // bears on it, so something is always permitted. Kept so that widening either list
            // degrades to the pre-BQ-077 choice rather than to no choice at all.
            GoalActionTrace chosen = actions[bestIndex < 0 ? bestOverall : bestIndex];
            GoalFormationTrace trace = new GoalFormationTrace(
                actor.Id,
                ProblemSummary(problem),
                need,
                concern,
                pressure,
                "answer " + NeedName(need) + " pressure caused by " + ConcernName(concern),
                PreviewGoal(actor, problem, subject),
                chosen);

            trace.CandidateActions.AddRange(actions);
            return trace;
        }

        public static NpcGoal FormGoal(NarrativeNpc actor, MissingGoatProblem problem, EntityId subject)
        {
            if (actor == null)
            {
                throw new ArgumentNullException(nameof(actor));
            }

            if (problem == null)
            {
                throw new ArgumentNullException(nameof(problem));
            }

            ValueConcern concern = DominantConcern(actor, problem);
            NpcGoal formed = PreviewGoal(actor, problem, subject);
            NarrativeNeed need = NeedFor(concern);
            double pressure = Pressure(actor, problem, concern);

            actor.Needs.Set(need, Math.Max(actor.Needs.Get(need), pressure));

            NpcGoal existing = FindOpenGoal(actor, formed.Kind, subject);
            if (existing != null)
            {
                existing.Weight = formed.Weight;
                existing.Reason = formed.Reason;
                return existing;
            }

            NpcGoal goal = new NpcGoal(formed.Kind, subject, formed.Weight, formed.Reason);
            actor.Goals.Add(goal);
            return goal;
        }

        private static NpcGoal PreviewGoal(NarrativeNpc actor, MissingGoatProblem problem, EntityId subject)
        {
            ValueConcern concern = DominantConcern(actor, problem);
            NarrativeNeed need = NeedFor(concern);
            double pressure = Pressure(actor, problem, concern);
            int weight = Math.Min(100, Math.Max(0, (int)Math.Round(pressure * 100.0)));
            string reason = "need " + NeedName(need) + " rose from threatened value "
                            + ConcernName(concern) + " (importance "
                            + actor.Values.Get(concern).Importance.ToString("0.00") + ", flexibility "
                            + actor.Values.Get(concern).Flexibility.ToString("0.00") + ")";

            return new NpcGoal(GoalKindFor(concern, need), subject, weight, reason);
        }

        private static ScoreBreakdown Score(NarrativeNpc actor, MissingGoatProblem problem, ProblemSolvingStyle style)
        {
            ScoreBreakdown score = new ScoreBreakdown();
            score.Add("style preference " + style, actor.ProblemSolving.Get(style));
            PersonalityWeights personality = actor.Personality;
            SensitivityProfile sensitivities = actor.Sensitivities;
            ContradictionProfile contradiction = actor.Contradiction;
            switch (style)
            {
                case ProblemSolvingStyle.AskAuthority:
                    score.Add("personality orderliness", personality.Orderliness * 0.2);
                    score.Add("personality trust", personality.Trust * 0.1);
                    score.Add("sensitivity status", If(problem.ThreatensStatus, sensitivities.Status * 0.15));
                    score.Add("contradiction", ContradictionBias(contradiction, problem, style));
                    return score;
                case ProblemSolvingStyle.AskFriends:
                    score.Add("personality warmth", personality.Warmth * 0.2);
                    score.Add("personality trust", personality.Trust * 0.1);
                    score.Add("sensitivity animals", If(problem.IsAnimalAtRisk, sensitivities.Animals * 0.35));
                    score.Add("contradiction", ContradictionBias(contradiction, problem, style));
                    return score;
                case ProblemSolvingStyle.PaySomeone:
                    score.Add("personality generosity", personality.Generosity * 0.2);
                    score.Add("personality patience", personality.Patience * 0.1);
                    score.Add("sensitivity animals", If(problem.IsAnimalAtRisk, sensitivities.Animals * 0.2));
                    score.Add("contradiction", ContradictionBias(contradiction, problem, style));
                    return score;
                case ProblemSolvingStyle.Manipulate:
                    score.Add("personality low honesty", (1.0 - personality.Honesty) * 0.25);
                    score.Add("personality low humility", (1.0 - personality.Humility) * 0.05);
                    score.Add("sensitivity status", If(problem.ThreatensStatus, sensitivities.Status * 0.3));
                    score.Add("sensitivity public embarrassment", If(problem.IsPubliclyEmbarrassing, sensitivities.PublicEmbarrassment * 0.2));
                    score.Add("contradiction", ContradictionBias(contradiction, problem, style));
                    return score;
                case ProblemSolvingStyle.Conceal:
                    score.Add("personality low honesty", (1.0 - personality.Honesty) * 0.15);
                    score.Add("personality low generosity", (1.0 - personality.Generosity) * 0.15);
                    score.Add("sensitivity public embarrassment", If(problem.IsPubliclyEmbarrassing, sensitivities.PublicEmbarrassment * 0.3));
                    score.Add("contradiction", ContradictionBias(contradiction, problem, style));
                    return score;
                case ProblemSolvingStyle.SeekReligiousHelp:
                    score.Add("personality conventionality", personality.Conventionality * 0.2);
                    score.Add("personality patience", personality.Patience * 0.1);
                    score.Add("contradiction", ContradictionBias(contradiction, problem, style));
                    return score;
                case ProblemSolvingStyle.Wait:
                    score.Add("personality patience", personality.Patience * 0.2);
                    score.Add("personality low boldness", (1.0 - personality.Boldness) * 0.1);
                    score.Add("sensitivity animals", -If(problem.IsAnimalAtRisk, sensitivities.Animals * 0.2));
                    score.Add("sensitivity public embarrassment", -If(problem.IsPubliclyEmbarrassing, sensitivities.PublicEmbarrassment * 0.1));
                    score.Add("contradiction", ContradictionBias(contradiction, problem, style));
                    return score;
                default:
                    score.Add("contradiction", ContradictionBias(contradiction, problem, style));
                    return score;
            }
        }

        private static GoalActionTrace ActionTrace(Candidate candidate, ScoreBreakdown score, ProhibitionRuling ruling)
        {
            return new GoalActionTrace(
                candidate.Style,
                "missing_goat." + candidate.Style,
                candidate.Response.ToString(),
                score.Total,
                score.Terms,
                ruling);
        }

        private static string ProblemSummary(MissingGoatProblem problem)
        {
            return "missing_goat animal_at_risk=" + problem.IsAnimalAtRisk
                   + " public_embarrassment=" + problem.IsPubliclyEmbarrassing
                   + " threatens_status=" + problem.ThreatensStatus
                   + " threatens_family=" + problem.ThreatensFamily;
        }

        private static ValueConcern DominantConcern(NarrativeNpc actor, MissingGoatProblem problem)
        {
            ValueConcern best = ValueConcern.Status;
            double bestPressure = Pressure(actor, problem, best);

            if (problem.IsAnimalAtRisk)
            {
                Consider(actor, problem, ValueConcern.Animals, ref best, ref bestPressure);
            }

            if (problem.ThreatensFamily)
            {
                Consider(actor, problem, ValueConcern.Family, ref best, ref bestPressure);
            }

            if (problem.ThreatensStatus || problem.IsPubliclyEmbarrassing)
            {
                Consider(actor, problem, ValueConcern.Status, ref best, ref bestPressure);
            }

            return best;
        }

        private static void Consider(
            NarrativeNpc actor,
            MissingGoatProblem problem,
            ValueConcern concern,
            ref ValueConcern best,
            ref double bestPressure)
        {
            double pressure = Pressure(actor, problem, concern);
            if (pressure > bestPressure)
            {
                best = concern;
                bestPressure = pressure;
            }
        }

        private static double Pressure(NarrativeNpc actor, MissingGoatProblem problem, ValueConcern concern)
        {
            ValueConcernProfile value = actor.Values.Get(concern);
            double threat = Threat(problem, concern);
            return Math.Max(0.0, Math.Min(1.0, threat * value.Importance * (1.0 - (value.Flexibility * 0.5))));
        }

        private static double Threat(MissingGoatProblem problem, ValueConcern concern)
        {
            switch (concern)
            {
                case ValueConcern.Animals:
                    return problem.IsAnimalAtRisk ? 1.0 : 0.0;
                case ValueConcern.Family:
                    return problem.ThreatensFamily ? 1.0 : 0.0;
                case ValueConcern.Status:
                    return problem.ThreatensStatus ? 1.0 : problem.IsPubliclyEmbarrassing ? 0.75 : 0.0;
                default:
                    return 0.0;
            }
        }

        private static NarrativeNeed NeedFor(ValueConcern concern)
        {
            switch (concern)
            {
                case ValueConcern.Animals:
                    return NarrativeNeed.Protection;
                case ValueConcern.Family:
                    return NarrativeNeed.Safety;
                case ValueConcern.Status:
                    return NarrativeNeed.Status;
                default:
                    return NarrativeNeed.Obligation;
            }
        }

        private static string GoalKindFor(ValueConcern concern, NarrativeNeed need)
        {
            switch (concern)
            {
                case ValueConcern.Animals:
                    return "protect_animal";
                case ValueConcern.Family:
                    return "protect_family";
                case ValueConcern.Status:
                    return "restore_status";
                default:
                    return "answer_need_" + NeedName(need);
            }
        }

        private static NpcGoal FindOpenGoal(NarrativeNpc actor, string kind, EntityId subject)
        {
            for (int i = 0; i < actor.Goals.Count; i++)
            {
                NpcGoal goal = actor.Goals[i];
                if (!goal.Satisfied && goal.Kind == kind && goal.Subject == subject)
                {
                    return goal;
                }
            }

            return null;
        }

        private static string ConcernName(ValueConcern concern) => concern.ToString().ToLowerInvariant();

        private static string NeedName(NarrativeNeed need) => need.ToString().ToLowerInvariant();

        private static double ContradictionBias(
            ContradictionProfile contradiction,
            MissingGoatProblem problem,
            ProblemSolvingStyle style)
        {
            if (contradiction == null || !contradiction.HasContradiction)
            {
                return 0.0;
            }

            double strength = contradiction.Strength;
            switch (contradiction.Kind)
            {
                case PersonalityContradiction.CowardlyButProtective:
                    if (!problem.IsAnimalAtRisk && !problem.ThreatensFamily)
                    {
                        return 0.0;
                    }

                    if (style == ProblemSolvingStyle.AskFriends)
                    {
                        return 0.45 * strength;
                    }

                    if (style == ProblemSolvingStyle.Wait)
                    {
                        return -0.25 * strength;
                    }

                    return 0.0;
                case PersonalityContradiction.HonestExceptAboutFamily:
                    if (!problem.ThreatensFamily)
                    {
                        return 0.0;
                    }

                    if (style == ProblemSolvingStyle.Conceal)
                    {
                        return 0.45 * strength;
                    }

                    if (style == ProblemSolvingStyle.AskAuthority)
                    {
                        return -0.2 * strength;
                    }

                    return 0.0;
                default:
                    return 0.0;
            }
        }

        private static double If(bool condition, double value) => condition ? value : 0.0;

        private struct Candidate
        {
            public Candidate(ProblemSolvingStyle style, MissingGoatResponse response)
            {
                Style = style;
                Response = response;
            }

            public ProblemSolvingStyle Style { get; }

            public MissingGoatResponse Response { get; }
        }

        private sealed class ScoreBreakdown
        {
            private readonly List<string> _terms = new List<string>();

            public IReadOnlyList<string> Terms => _terms;

            public double Total { get; private set; }

            public void Add(string name, double value)
            {
                Total += value;
                _terms.Add(name + " " + value.ToString("0.00"));
            }
        }
    }
}
