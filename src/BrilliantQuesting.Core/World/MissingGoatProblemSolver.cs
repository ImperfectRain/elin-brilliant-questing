using System;

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
        public MissingGoatDecision(ProblemSolvingStyle style, MissingGoatResponse response, double score)
        {
            Style = style;
            Response = response;
            Score = score;
        }

        public ProblemSolvingStyle Style { get; }

        public MissingGoatResponse Response { get; }

        public double Score { get; }
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

            return new MissingGoatDecision(best.Style, best.Response, bestScore);
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
