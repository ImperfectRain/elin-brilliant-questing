using System.Linq;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Situations;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    public class FestivalCompetitionSituationTests
    {
        private static readonly EntityId Player = EntityId.Parse("npc_player");
        private static readonly EntityId Green = EntityId.Parse("zone_green");

        [Fact]
        public void AnNpcCanWinACompetitionThePlayerEntered()
        {
            Lab lab = new Lab();
            lab.Checks
                .Then(CheckOutcome.Fail)
                .Then(CheckOutcome.CriticalPass)
                .Then(CheckOutcome.Pass);

            CompetitionResult result = lab.Situation.Resolve(
                lab.World,
                lab.Vanilla,
                lab.Checks,
                new DeterministicRng(71),
                Player,
                lab.Vanilla.Now);

            Assert.Equal(lab.Situation.BakerId, result.WinnerId);
            Assert.Equal(ThreadState.Resolved, lab.Situation.Thread.State);
            Assert.Contains(Player, lab.Situation.Thread.ParticipantIds);
            Assert.Contains(lab.World.Ledger.Events, e => e.Type == WorldEventType.CompetitionWon
                && e.Actor == lab.Situation.BakerId
                && e.Target == lab.Situation.JudgeId
                && e.ThreadId == lab.Situation.Thread.Id);

            Fact fact = lab.World.Knowledge.GetFact(result.ResultFactId);
            Assert.NotNull(fact);
            Assert.Equal(FactPredicates.WonCompetition, fact.Predicate);
            Assert.Equal(lab.Situation.BakerId, fact.Subject);
            Assert.Equal(Green, fact.Object);
            Assert.Equal(lab.Situation.ContestName, fact.Value);
            Assert.Contains(result.ResultFactId, lab.Situation.Thread.FactIds);
        }

        [Fact]
        public void CompetitionWinnerAndJudgeAreNotAlsoUnrelatedWitnesses()
        {
            Lab lab = new Lab();
            lab.Checks
                .Then(CheckOutcome.Fail)
                .Then(CheckOutcome.CriticalPass)
                .Then(CheckOutcome.Pass);

            lab.Situation.Resolve(
                lab.World,
                lab.Vanilla,
                lab.Checks,
                new DeterministicRng(71),
                Player,
                lab.Vanilla.Now);

            WorldEvent won = lab.World.Ledger.Events.Single(e => e.Type == WorldEventType.CompetitionWon);
            Assert.Equal(lab.Situation.BakerId, won.Actor);
            Assert.Equal(lab.Situation.JudgeId, won.Target);
            Assert.DoesNotContain(won.Actor, won.Witnesses);
            Assert.DoesNotContain(won.Target, won.Witnesses);
            Assert.DoesNotContain(Player, won.Witnesses);
            Assert.DoesNotContain(lab.Situation.FarmerId, won.Witnesses);
            Assert.Equal(won.Witnesses.Count, won.Witnesses.Distinct().Count());
        }

        [Fact]
        public void ThePublicResultCanBeReferencedLater()
        {
            Lab lab = new Lab();
            lab.Checks
                .Then(CheckOutcome.Fail)
                .Then(CheckOutcome.CriticalPass)
                .Then(CheckOutcome.Pass)
                .Then(CheckOutcome.CriticalPass);

            CompetitionResult result = lab.Situation.Resolve(
                lab.World,
                lab.Vanilla,
                lab.Checks,
                new DeterministicRng(72),
                Player,
                lab.Vanilla.Now);

            Assert.True(lab.World.Knowledge.Knows(lab.Situation.JudgeId, result.ResultFactId));
            Assert.False(lab.World.Knowledge.Knows(Player, result.ResultFactId));

            ActionContext context = new ActionContext(
                lab.World,
                lab.Vanilla,
                lab.Checks,
                new DeterministicRng(73),
                Player,
                lab.Situation.JudgeId)
            {
                SubjectFact = result.ResultFactId,
                Thread = lab.Situation.Thread
            };

            ActionOutcome asked = StandardActions.CreateRegistry().Get("question").Perform(context);

            Assert.Contains("won competition", asked.Notes.Single());
            Assert.True(lab.World.Knowledge.TryGetBelief(Player, result.ResultFactId, out KnowledgeRecord learned));
            Assert.Equal(KnowledgeSource.Hearsay, learned.Source);
            Assert.Contains(lab.World.Ledger.Events, e => e.Type == WorldEventType.Conversed
                && e.Actor == Player
                && e.Target == lab.Situation.JudgeId
                && e.Related.Contains(result.ResultFactId));
        }

        private sealed class Lab
        {
            public Lab()
            {
                World = new NarrativeWorldState(107);
                Vanilla = new SandboxVanillaState(Player);
                Vanilla.Define(Player, zone: Green);
                Vanilla.SetAttribute(Player, VanillaAttribute.Dexterity, 7);
                Vanilla.SetAttribute(Player, VanillaAttribute.Charisma, 7);
                Vanilla.SetSkill(Player, VanillaSkill.Cooking, 2);
                Vanilla.SetSkill(Player, VanillaSkill.Negotiation, 2);
                Checks = new FixedCheckResolver(CheckOutcome.Fail);
                Situation = FestivalCompetitionSituation.Create(
                    World,
                    new SandboxStager(Vanilla),
                    Player,
                    Green,
                    Vanilla.Now);
            }

            public NarrativeWorldState World { get; }

            public SandboxVanillaState Vanilla { get; }

            public FixedCheckResolver Checks { get; }

            public FestivalCompetitionSituation Situation { get; }
        }
    }
}
