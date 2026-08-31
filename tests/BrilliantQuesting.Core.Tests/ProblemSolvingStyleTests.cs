using System.Collections.Generic;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    public class ProblemSolvingStyleTests
    {
        [Fact]
        public void SameMissingGoatProblemCausesFivePersonalitiesToChooseDifferentResponses()
        {
            MissingGoatResponse[] responses =
            {
                MissingGoatProblemSolver.Choose(Actor("reeve", ProblemSolvingStyle.AskAuthority)).Response,
                MissingGoatProblemSolver.Choose(Actor("neighbour", ProblemSolvingStyle.AskFriends)).Response,
                MissingGoatProblemSolver.Choose(Actor("patron", ProblemSolvingStyle.PaySomeone)).Response,
                MissingGoatProblemSolver.Choose(Actor("schemer", ProblemSolvingStyle.Manipulate)).Response,
                MissingGoatProblemSolver.Choose(Actor("penitent", ProblemSolvingStyle.SeekReligiousHelp)).Response
            };

            Assert.Equal(5, new HashSet<MissingGoatResponse>(responses).Count);
            Assert.Contains(MissingGoatResponse.ReportToGuards, responses);
            Assert.Contains(MissingGoatResponse.AskNeighbors, responses);
            Assert.Contains(MissingGoatResponse.OfferPayment, responses);
            Assert.Contains(MissingGoatResponse.AccuseRival, responses);
            Assert.Contains(MissingGoatResponse.PrayForReturn, responses);
        }

        private static NarrativeNpc Actor(string key, ProblemSolvingStyle favoredStyle)
        {
            NarrativeNpc npc = new NarrativeNpc(EntityId.Parse("npc_" + key), key);
            foreach (ProblemSolvingStyle style in AllStyles())
            {
                npc.ProblemSolving.Set(style, style == favoredStyle ? 1.0 : 0.0);
            }

            return npc;
        }

        private static IEnumerable<ProblemSolvingStyle> AllStyles()
        {
            yield return ProblemSolvingStyle.Confront;
            yield return ProblemSolvingStyle.Avoid;
            yield return ProblemSolvingStyle.AskAuthority;
            yield return ProblemSolvingStyle.AskFriends;
            yield return ProblemSolvingStyle.PaySomeone;
            yield return ProblemSolvingStyle.DoItSelf;
            yield return ProblemSolvingStyle.Manipulate;
            yield return ProblemSolvingStyle.UseViolence;
            yield return ProblemSolvingStyle.SeekGuild;
            yield return ProblemSolvingStyle.SeekReligiousHelp;
            yield return ProblemSolvingStyle.Wait;
            yield return ProblemSolvingStyle.Flee;
            yield return ProblemSolvingStyle.Publicize;
            yield return ProblemSolvingStyle.Conceal;
        }
    }
}
