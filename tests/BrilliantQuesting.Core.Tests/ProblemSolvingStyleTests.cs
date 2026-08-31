using System.Collections.Generic;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
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

        [Fact]
        public void OneSensitivityChangesReactionToTheSameMissingGoatEvent()
        {
            NarrativeNpc animalSensitive = NeutralActor("animal-sensitive");
            NarrativeNpc statusSensitive = NeutralActor("status-sensitive");

            animalSensitive.Sensitivities.Set(SensitivityTopic.Animals, 1.0);
            statusSensitive.Sensitivities.Set(SensitivityTopic.Status, 1.0);

            MissingGoatProblem sameEvent = MissingGoatProblem.OrdinaryLoss;

            Assert.Equal(MissingGoatResponse.AskNeighbors, MissingGoatProblemSolver.Choose(animalSensitive, sameEvent).Response);
            Assert.Equal(MissingGoatResponse.AccuseRival, MissingGoatProblemSolver.Choose(statusSensitive, sameEvent).Response);
        }

        [Fact]
        public void ContradictionCanOverridePersonalityAlone()
        {
            NarrativeNpc timidWatcher = NeutralActor("timid-watcher");
            timidWatcher.Personality.Boldness = 0.0;
            timidWatcher.Personality.Patience = 1.0;

            MissingGoatProblem animalInDanger = new MissingGoatProblem(
                isAnimalAtRisk: true,
                isPubliclyEmbarrassing: false,
                threatensStatus: false);

            Assert.Equal(MissingGoatResponse.ComplainAndWait, MissingGoatProblemSolver.Choose(timidWatcher, animalInDanger).Response);

            timidWatcher.Contradiction.Kind = PersonalityContradiction.CowardlyButProtective;

            Assert.Equal(MissingGoatResponse.AskNeighbors, MissingGoatProblemSolver.Choose(timidWatcher, animalInDanger).Response);
        }

        [Fact]
        public void ThreatenedValueChangesGoalAndIsTraceableInInspector()
        {
            NarrativeWorldState world = new NarrativeWorldState(42);
            SandboxVanillaState vanilla = new SandboxVanillaState(EntityId.Parse("player"));
            NarrativeNpc caretaker = NeutralActor("caretaker");
            caretaker.Values.Animals.Importance = 1.0;
            caretaker.Values.Animals.Flexibility = 0.0;
            caretaker.Values.Status.Importance = 0.0;
            world.Registry.Add(caretaker);
            vanilla.Define(caretaker.Id);

            MissingGoatProblem problem = new MissingGoatProblem(
                isAnimalAtRisk: true,
                isPubliclyEmbarrassing: true,
                threatensStatus: true);

            NpcGoal goal = MissingGoatProblemSolver.FormGoal(caretaker, problem, EntityId.Parse("goat"));

            Assert.Equal("protect_animal", goal.Kind);
            Assert.Equal(EntityId.Parse("goat"), goal.Subject);
            Assert.True(goal.Weight > 80);
            Assert.Equal(goal.Weight / 100.0, caretaker.Needs.Protection, 2);
            Assert.Contains("threatened value animals", goal.Reason);

            string report = NarrativeInspector.DescribeCharacter(world, vanilla, caretaker.Id);
            Assert.Contains("narrative needs:", report);
            Assert.Contains("protection", report);
            Assert.Contains("protect_animal", report);
            Assert.Contains("threatened value animals", report);
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

        private static NarrativeNpc NeutralActor(string key)
        {
            NarrativeNpc npc = new NarrativeNpc(EntityId.Parse("npc_" + key), key);
            foreach (ProblemSolvingStyle style in AllStyles())
            {
                npc.ProblemSolving.Set(style, 0.5);
            }

            foreach (SensitivityTopic topic in AllSensitivityTopics())
            {
                npc.Sensitivities.Set(topic, 0.0);
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

        private static IEnumerable<SensitivityTopic> AllSensitivityTopics()
        {
            yield return SensitivityTopic.PublicEmbarrassment;
            yield return SensitivityTopic.UnpaidDebt;
            yield return SensitivityTopic.FamilyThreat;
            yield return SensitivityTopic.Animals;
            yield return SensitivityTopic.Status;
            yield return SensitivityTopic.Theft;
            yield return SensitivityTopic.Violence;
            yield return SensitivityTopic.Dishonesty;
        }
    }
}
