using System.Linq;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Obligations;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Situations;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    public class SocialObligationTests
    {
        [Fact]
        public void AFailedAskIsRefusedWithoutAFavor()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Checks = new FixedCheckResolver(CheckOutcome.Fail);

            ActionOutcome outcome = lab.Perform("persuade", lab.Situation.VictimId);

            Assert.False(outcome.Succeeded);
            Assert.Contains("turns you down", outcome.Narration);
            Assert.DoesNotContain(outcome.Events, e => e.Type == WorldEventType.PromiseMade);
        }

        [Fact]
        public void ARecordedFavorMakesSomebodyHelpWhenTheyWouldRefuse()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Checks = new FixedCheckResolver(CheckOutcome.Fail);
            SocialObligation favor = RecordFavor(
                lab,
                lab.Situation.VictimId,
                lab.Player,
                lab.Situation.TheftFactId);

            ActionOutcome outcome = lab.Perform("persuade", lab.Situation.VictimId);

            Assert.True(outcome.Succeeded);
            Assert.Equal(SocialObligationStatus.Fulfilled, favor.Status);
            Assert.Contains("honours the favor", outcome.Narration);
            Assert.Contains(outcome.Notes, note => note.Contains("recorded favor " + favor.Id));
            Assert.Contains(outcome.Events, e => e.Type == WorldEventType.FavorRedeemed
                                                && e.Related.Contains(favor.Id));
            Assert.Contains(outcome.Events, e => e.Type == WorldEventType.PromiseMade);
        }

        [Fact]
        public void ASuccessfulAskDoesNotSpendAFavor()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Checks = new FixedCheckResolver(CheckOutcome.Pass);
            SocialObligation favor = RecordFavor(
                lab,
                lab.Situation.VictimId,
                lab.Player,
                lab.Situation.TheftFactId);

            ActionOutcome outcome = lab.Perform("persuade", lab.Situation.VictimId);

            Assert.True(outcome.Succeeded);
            Assert.Equal(SocialObligationStatus.Open, favor.Status);
            Assert.DoesNotContain(outcome.Events, e => e.Type == WorldEventType.FavorRedeemed);
        }

        [Fact]
        public void ObligationsSurviveTheSave()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            SocialObligation favor = RecordFavor(
                lab,
                lab.Situation.VictimId,
                lab.Player,
                lab.Situation.TheftFactId);
            favor.Fulfill(GameTime.FromDays(2));

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(lab.World));
            SocialObligation restored = Assert.Single(reloaded.Obligations.Records);

            Assert.Equal(favor.Id, restored.Id);
            Assert.Equal(SocialObligationKind.Favor, restored.Kind);
            Assert.Equal(lab.Situation.VictimId, restored.Debtor);
            Assert.Equal(lab.Player, restored.Creditor);
            Assert.Equal(lab.Situation.TheftFactId, restored.Subject);
            Assert.Equal(SocialObligationStatus.Fulfilled, restored.Status);
            Assert.Equal(GameTime.FromDays(2), restored.ResolvedAt);
            Assert.Equal(favor.SourceEventId, restored.SourceEventId);
        }

        private static SocialObligation RecordFavor(
            TheftLaboratory lab,
            EntityId debtor,
            EntityId creditor,
            EntityId subject)
        {
            WorldEvent source = lab.World.Record(
                WorldEventType.FavorOwed,
                debtor,
                creditor,
                lab.Vanilla.Now,
                0.5,
                lab.Zone,
                related: new[] { subject },
                threadId: lab.Situation.Thread.Id);

            return lab.World.Obligations.Add(new SocialObligation(
                lab.World.NewId("obl"),
                SocialObligationKind.Favor,
                debtor,
                creditor,
                subject,
                string.Empty,
                lab.Vanilla.Now,
                source.Id));
        }
    }
}
