using System;
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
        {
            Style = style;
            Response = response;
            Score = score;
            Goal = goal;
        }

        public ProblemSolvingStyle Style { get; }

        public MissingGoatResponse Response { get; }

        public double Score { get; }

        public NpcGoal Goal { get; }
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
            if (actor == null)
            {
                throw new ArgumentNullException(nameof(actor));
            }

            if (problem == null)
            {
                throw new ArgumentNullException(nameof(problem));
            }

            Candidate best = Candidates[0];
            double bestScore = Score(actor, problem, best.Style);
            for (int i = 1; i < Candidates.Length; i++)
            {
                double score = Score(actor, problem, Candidates[i].Style);
                if (score > bestScore)
                {
                    best = Candidates[i];
                    bestScore = score;
                }
            }

            return new MissingGoatDecision(best.Style, best.Response, bestScore, PreviewGoal(actor, problem, EntityId.None));
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

        private static double Score(NarrativeNpc actor, MissingGoatProblem problem, ProblemSolvingStyle style)
        {
            double score = actor.ProblemSolving.Get(style);
            PersonalityWeights personality = actor.Personality;
            SensitivityProfile sensitivities = actor.Sensitivities;
            ContradictionProfile contradiction = actor.Contradiction;
            switch (style)
            {
                case ProblemSolvingStyle.AskAuthority:
                    return score + (personality.Orderliness * 0.2) + (personality.Trust * 0.1)
                        + If(problem.ThreatensStatus, sensitivities.Status * 0.15)
                        + ContradictionBias(contradiction, problem, style);
                case ProblemSolvingStyle.AskFriends:
                    return score + (personality.Warmth * 0.2) + (personality.Trust * 0.1)
                        + If(problem.IsAnimalAtRisk, sensitivities.Animals * 0.35)
                        + ContradictionBias(contradiction, problem, style);
                case ProblemSolvingStyle.PaySomeone:
                    return score + (personality.Generosity * 0.2) + (personality.Patience * 0.1)
                        + If(problem.IsAnimalAtRisk, sensitivities.Animals * 0.2)
                        + ContradictionBias(contradiction, problem, style);
                case ProblemSolvingStyle.Manipulate:
                    return score + ((1.0 - personality.Honesty) * 0.25) + ((1.0 - personality.Humility) * 0.05)
                        + If(problem.ThreatensStatus, sensitivities.Status * 0.3)
                        + If(problem.IsPubliclyEmbarrassing, sensitivities.PublicEmbarrassment * 0.2)
                        + ContradictionBias(contradiction, problem, style);
                case ProblemSolvingStyle.Conceal:
                    return score + ((1.0 - personality.Honesty) * 0.15) + ((1.0 - personality.Generosity) * 0.15)
                        + If(problem.IsPubliclyEmbarrassing, sensitivities.PublicEmbarrassment * 0.3)
                        + ContradictionBias(contradiction, problem, style);
                case ProblemSolvingStyle.SeekReligiousHelp:
                    return score + (personality.Conventionality * 0.2) + (personality.Patience * 0.1)
                        + ContradictionBias(contradiction, problem, style);
                case ProblemSolvingStyle.Wait:
                    return score + (personality.Patience * 0.2) + ((1.0 - personality.Boldness) * 0.1)
                        - If(problem.IsAnimalAtRisk, sensitivities.Animals * 0.2)
                        - If(problem.IsPubliclyEmbarrassing, sensitivities.PublicEmbarrassment * 0.1)
                        + ContradictionBias(contradiction, problem, style);
                default:
                    return score + ContradictionBias(contradiction, problem, style);
            }
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
    }
}
