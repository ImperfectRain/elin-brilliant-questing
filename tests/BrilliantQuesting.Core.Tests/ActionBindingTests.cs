using System.Collections.Generic;
using System.Linq;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Situations;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    public class ActionBindingTests
    {
        [Fact]
        public void PurposeBearingVerbsAreUnavailableWithoutSemanticBinding()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            ActionContext context = new ActionContext(
                lab.World,
                lab.Vanilla,
                lab.Checks,
                lab.World.Rng,
                lab.Player,
                lab.Situation.ThiefId);

            Assert.False(lab.Actions.Get("persuade").GetAvailability(context).IsAvailable);
            Assert.False(lab.Actions.Get("intimidate").GetAvailability(context).IsAvailable);
            Assert.False(lab.Actions.Get("escort").GetAvailability(context).IsAvailable);
            Assert.False(lab.Actions.Get("capture").GetAvailability(context).IsAvailable);
            Assert.False(lab.Actions.Get("restrain").GetAvailability(context).IsAvailable);
        }

        [Fact]
        public void ProjectionDoesNotOfferUnboundPhysicalPersonActions()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            ActionContext context = Focus(lab, lab.Situation.ThiefId);

            List<ActionOffer> available = lab.Actions.Discover(context);
            List<ActionIntentOption> projected = ContextualActionProjection.Project(available, context, 20);

            Assert.DoesNotContain(projected, o => o.Action.Id == "escort");
            Assert.DoesNotContain(projected, o => o.Action.Id == "capture");
            Assert.DoesNotContain(projected, o => o.Action.Id == "restrain");
        }

        [Fact]
        public void SuccessfulPersuasionRecordsWhatWasAgreedTo()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Checks = new FixedCheckResolver(CheckOutcome.Pass);
            ActionContext context = Focus(lab, lab.Situation.VictimId);

            ActionOutcome outcome = lab.Actions.Get("persuade").Perform(context);

            Assert.Contains("agrees to help with the missing ", outcome.Narration);
            WorldEvent promise = Assert.Single(outcome.Events, e => e.Type == WorldEventType.PromiseMade);
            Assert.Contains(lab.Situation.TheftFactId, promise.Related);
            Assert.Equal(lab.Situation.Thread.Id, promise.ThreadId);
        }

        [Fact]
        public void PromisePurposeSurvivesSaveReload()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Checks = new FixedCheckResolver(CheckOutcome.Pass);

            lab.Actions.Get("persuade").Perform(Focus(lab, lab.Situation.VictimId));

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(lab.World));
            WorldEvent promise = Assert.Single(reloaded.Ledger.Events, e => e.Type == WorldEventType.PromiseMade);
            Assert.Contains(lab.Situation.TheftFactId, promise.Related);
            Assert.Equal(lab.Situation.Thread.Id, promise.ThreadId);
        }

        [Fact]
        public void CulpritQuestioningDoesNotTriviallyDiscloseSelfIncriminatingTruth()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            ActionContext context = Focus(lab, lab.Situation.ThiefId);

            Availability question = lab.Actions.Get("question").GetAvailability(context);

            Assert.False(question.IsAvailable);
            Assert.False(lab.World.Knowledge.Knows(lab.Player, lab.Situation.TheftFactId));
        }

        [Fact]
        public void CulpritPressureRefusalDoesNotRevealHiddenTruth()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Checks = new FixedCheckResolver(CheckOutcome.Pass);

            ActionOutcome outcome = lab.Actions.Get("intimidate").Perform(Focus(lab, lab.Situation.ThiefId));

            Assert.Contains("refuses", outcome.Narration);
            Assert.False(lab.World.Knowledge.Knows(lab.Player, lab.Situation.TheftFactId));
            WorldEvent pressure = Assert.Single(outcome.Events, e => e.Type == WorldEventType.Threatened);
            Assert.Contains(lab.Situation.TheftFactId, pressure.Related);
            Assert.Contains(EventTags.Withheld, pressure.Tags);
        }

        [Fact]
        public void ExplicitCulpritAdmissionHasAdmissionProvenance()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Checks = new FixedCheckResolver(CheckOutcome.CriticalPass);

            ActionOutcome outcome = lab.Actions.Get("intimidate").Perform(Focus(lab, lab.Situation.ThiefId));

            Assert.Contains("admits", outcome.Narration);
            KnowledgeRecord learned = Assert.Single(lab.World.Knowledge.BeliefsOf(lab.Player), b => b.FactId == lab.Situation.TheftFactId);
            Assert.Equal(KnowledgeSource.Admission, learned.Source);
            Assert.Equal(lab.Situation.ThiefId, learned.ToldBy);
            WorldEvent pressure = Assert.Single(outcome.Events, e => e.Type == WorldEventType.Threatened);
            Assert.Contains(EventTags.Admission, pressure.Tags);
        }

        [Fact]
        public void PressureOverKnownEvidenceStillNamesTheMatter()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Checks = new FixedCheckResolver(CheckOutcome.Pass);
            lab.World.Knowledge.Teach(lab.Player, lab.Situation.TheftFactId, KnowledgeSource.Witnessed, 1.0, lab.Vanilla.Now, true);

            ActionOutcome outcome = lab.Actions.Get("intimidate").Perform(Focus(lab, lab.Situation.ThiefId));

            Assert.Contains("missing ", outcome.Narration);
            Assert.DoesNotContain("nothing you did not already know", outcome.Narration);
            WorldEvent pressure = Assert.Single(outcome.Events, e => e.Type == WorldEventType.Threatened);
            Assert.Contains(lab.Situation.TheftFactId, pressure.Related);
        }

        [Fact]
        public void DirectSelfDisclosureIsNotFiledAsOrdinaryHearsay()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            EntityId factId = lab.World.NewId("fact");
            Fact worry = new Fact(factId, lab.Situation.VictimId, FactPredicates.AtRisk, lab.Situation.ThiefId, "harassed", TruthState.True);
            lab.World.Knowledge.AddFact(worry);
            lab.World.Knowledge.Teach(lab.Situation.VictimId, factId, KnowledgeSource.Participant, 1.0, lab.Vanilla.Now, true);
            lab.Checks = new FixedCheckResolver(CheckOutcome.Pass);

            ActionContext context = lab.Context(lab.Situation.VictimId);
            context.SubjectFact = factId;

            lab.Actions.Get("question").Perform(context);

            KnowledgeRecord learned = lab.World.Knowledge.BeliefsOf(lab.Player).Single(b => b.FactId == factId);
            Assert.Equal(KnowledgeSource.Admission, learned.Source);
            Assert.Equal(lab.Situation.VictimId, learned.ToldBy);
        }

        private static ActionContext Focus(TheftLaboratory lab, EntityId target)
        {
            ActionContext context = lab.Context(target);
            context.SubjectFact = lab.Situation.TheftFactId;
            context.SubjectItem = lab.Situation.ItemId;
            return context;
        }
    }
}
