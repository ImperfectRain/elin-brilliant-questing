using System.Collections.Generic;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Situations;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// The rule under test throughout: options are hidden only for impossibility, never for
    /// incompetence. A bad liar sees "lie"; someone with nothing to lie about does not.
    /// </summary>
    public class ActionAvailabilityTests
    {
        [Fact]
        public void YouCannotRevealSomethingYouDoNotKnow()
        {
            TheftLaboratory lab = TheftLaboratory.Create();

            ActionContext context = lab.Context(lab.Situation.VictimId);
            context.SubjectFact = lab.Situation.TheftFactId;

            Availability availability = lab.Actions.Get("expose").GetAvailability(context);

            Assert.False(availability.IsAvailable);
            Assert.Contains("do not know", availability.Reason);
        }

        [Fact]
        public void OnceYouKnowIt_YouCanTryToRevealIt()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.World.Knowledge.Teach(lab.Player, lab.Situation.TheftFactId, Knowledge.KnowledgeSource.Hearsay, 0.6, lab.Vanilla.Now, canProve: false);

            ActionContext context = lab.Context(lab.Situation.VictimId);
            context.SubjectFact = lab.Situation.TheftFactId;

            Assert.True(lab.Actions.Get("expose").GetAvailability(context).IsAvailable);
        }

        [Fact]
        public void AHopelessLiarIsStillOfferedTheLie()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Vanilla.SetSkill(lab.Player, VanillaSkill.Negotiation, 0);
            lab.Vanilla.SetAttribute(lab.Player, VanillaAttribute.Charisma, 3);
            lab.World.Knowledge.Teach(lab.Player, lab.Situation.TheftFactId, Knowledge.KnowledgeSource.Hearsay, 0.6, lab.Vanilla.Now, canProve: false);

            ActionContext context = lab.Context(lab.Situation.VictimId);
            context.SubjectFact = lab.Situation.TheftFactId;

            Assert.True(lab.Actions.Get("lie").GetAvailability(context).IsAvailable);
        }

        [Fact]
        public void YouCannotOfferMoneyYouDoNotHave()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Vanilla.Define(lab.Player, level: 5, money: 0, zone: lab.Zone);

            Availability availability = lab.Actions.Get("bribe").GetAvailability(lab.Context(lab.Situation.WitnessId));

            Assert.False(availability.IsAvailable);
            Assert.Contains("orens you do not have", availability.Reason);
        }

        [Fact]
        public void PickpocketingNeedsSomethingToTake()
        {
            TheftLaboratory lab = TheftLaboratory.Create();

            Assert.True(lab.Actions.Get("pickpocket").GetAvailability(lab.Context(lab.Situation.ThiefId)).IsAvailable);
            Assert.False(lab.Actions.Get("pickpocket").GetAvailability(lab.Context(lab.Situation.WitnessId)).IsAvailable);
        }

        [Fact]
        public void TheStolenItemHasAVanillaSourceId()
        {
            TheftLaboratory lab = TheftLaboratory.Create();

            IReadOnlyList<ItemDescriptor> carried = lab.Vanilla.GetInventory(lab.Situation.ThiefId);

            Assert.Single(carried);
            Assert.Equal(lab.Situation.ItemId, carried[0].Id);
            Assert.False(string.IsNullOrEmpty(carried[0].SourceId));
        }

        [Fact]
        public void BuildingRapportIsASoftAvailableAction()
        {
            TheftLaboratory lab = TheftLaboratory.Create();

            ActionOutcome outcome = lab.Perform("rapport", lab.Situation.WitnessId);

            Assert.Equal("rapport", outcome.ActionId);
            Assert.True(lab.Actions.Get("rapport").GetAvailability(lab.Context(lab.Situation.WitnessId)).IsAvailable);
            Assert.Contains(lab.World.Ledger.Events, e => e.Type == WorldEventType.Helped);
        }

        [Fact]
        public void AMissingVanillaCapabilityBlocksTheRouteInsteadOfFailingMidway()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Vanilla.SetCapability(VanillaCapability.TransferItems, false);

            Availability availability = lab.Actions.Get("pickpocket").GetAvailability(lab.Context(lab.Situation.ThiefId));

            Assert.False(availability.IsAvailable);
            Assert.Contains("unavailable on this build", availability.Reason);
        }

        [Fact]
        public void TheSituationOffersSeveralDistinctSolutionFamilies()
        {
            TheftLaboratory lab = TheftLaboratory.Create();

            HashSet<ActionFamily> families = new HashSet<ActionFamily>();
            foreach (EntityId target in new[] { lab.Situation.VictimId, lab.Situation.ThiefId, lab.Situation.WitnessId })
            {
                families.UnionWith(lab.Actions.AvailableFamilies(lab.Context(target)));
            }

            // The design target is at least three distinct routes into any major situation.
            Assert.True(families.Count >= 3, "expected 3+ solution families, got " + families.Count);
        }

        [Fact]
        public void RejectedOptionsKeepTheirReasonForTheInspector()
        {
            TheftLaboratory lab = TheftLaboratory.Create();

            List<ActionOffer> all = lab.Actions.Discover(lab.Context(lab.Situation.WitnessId), includeUnavailable: true);

            Assert.Equal(lab.Actions.Actions.Count, all.Count);
            foreach (ActionOffer offer in all)
            {
                if (!offer.Availability.IsAvailable)
                {
                    Assert.False(string.IsNullOrEmpty(offer.Availability.Reason));
                }
            }
        }
    }
}
