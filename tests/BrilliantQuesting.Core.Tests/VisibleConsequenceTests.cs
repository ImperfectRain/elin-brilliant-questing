using System.Collections.Generic;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Situations;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-009: no silent successes, and no narrated ones either.
    ///
    /// The rule has two halves and the first live run failed the second. Every resolution must
    /// change something, *and* what it says must be what it did — intimidation reported "Ansel
    /// tells you what you want to know" and then, one line later, "they had nothing to give up".
    /// A player reading the first sentence has been told something untrue about their own world.
    /// </summary>
    public class VisibleConsequenceTests
    {
        /// <summary>
        /// Every verb, when it is offered and its check passes, must leave a trace: a ledger
        /// event, knowledge the actor did not have, or an item that moved. A verb that can be
        /// chosen, succeeds, and changes nothing is a dead option dressed as a live one.
        ///
        /// The roll is forced rather than rolled. This is about consequences, not dice, and a
        /// verb whose success is invisible should fail the test every run rather than one in
        /// three. Preconditions are set up per verb because most of them are legitimately
        /// unavailable in a pristine laboratory - that is the availability rules working.
        /// </summary>
        [Theory]
        [InlineData("rapport")]
        [InlineData("question")]
        [InlineData("persuade")]
        [InlineData("lie")]
        [InlineData("intimidate")]
        [InlineData("bribe")]
        [InlineData("search")]
        [InlineData("expose")]
        [InlineData("pickpocket")]
        [InlineData("frame")]
        [InlineData("return_item")]
        [InlineData("keep_item")]
        [InlineData("attack")]
        public void EverySuccessfulVerbLeavesATrace(string actionId)
        {
            // Two fixtures, because the preconditions pull in opposite directions: questioning
            // and pickpocketing need a player who does not yet know and does not yet hold, while
            // exposing and keeping need one who does.
            foreach (TheftLaboratory lab in new[] { Pristine(), Prepared() })
            {
                lab.Checks = new FixedCheckResolver(CheckOutcome.Pass);
                NarrativeAction action = lab.Actions.Get(actionId);

                foreach (EntityId target in Everyone(lab))
                {
                    ActionContext context = FullContext(lab, target);
                    if (!action.GetAvailability(context).IsAvailable)
                    {
                        continue;
                    }

                    int eventsBefore = lab.World.Ledger.Count;
                    int knownBefore = CountKnown(lab, lab.Player);
                    int carriedBefore = lab.Vanilla.GetInventory(lab.Player).Count;

                    ActionOutcome outcome = action.Perform(context);

                    bool recorded = lab.World.Ledger.Count > eventsBefore;
                    bool learned = CountKnown(lab, lab.Player) > knownBefore;
                    bool moved = lab.Vanilla.GetInventory(lab.Player).Count != carriedBefore;

                    Assert.True(
                        recorded || learned || moved,
                        actionId + " succeeded against " + lab.World.Registry.NameOf(target)
                        + " and changed nothing: \"" + outcome.Narration + "\"");
                    return;
                }
            }

            Assert.Fail(actionId + " was never available against anyone, in either laboratory.");
        }

        /// <summary>
        /// A laboratory where every verb has something to bite on: the player knows the claim and
        /// can prove it, and is carrying both the stolen item and something of their own to plant.
        /// </summary>
        private static TheftLaboratory Pristine() => TheftLaboratory.Create();

        private static TheftLaboratory Prepared()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.World.Knowledge.Teach(
                lab.Player, lab.Situation.TheftFactId, KnowledgeSource.Witnessed, 1.0, lab.Vanilla.Now, canProve: true);
            lab.Vanilla.TryTransferItem(lab.Situation.ItemId, lab.Situation.ThiefId, lab.Player);
            lab.Vanilla.GiveItem(lab.Player, new ItemDescriptor(
                lab.World.NewId("item"), "bent knife", "weapon", 30));
            return lab;
        }

        /// <summary>
        /// The regression itself. Intimidating somebody who has nothing you do not already know
        /// still succeeds — they are frightened and they remember it — but it must not claim to
        /// have told you anything.
        /// </summary>
        [Fact]
        public void ASuccessfulThreatAgainstSomeoneWithNothingToSayDoesNotClaimTheyTalked()
        {
            TheftLaboratory lab = TheftLaboratory.Create();

            // The player already knows the theft, so the thief has nothing left to concede.
            lab.World.Knowledge.Teach(
                lab.Player, lab.Situation.TheftFactId, KnowledgeSource.Hearsay, 0.9, lab.Vanilla.Now, canProve: false);

            ActionContext context = FullContext(lab, lab.Situation.ThiefId);
            ActionOutcome outcome = lab.Actions.Get("intimidate").Perform(context);

            if (outcome.Check.Outcome == CheckOutcome.Pass || outcome.Check.Outcome == CheckOutcome.CriticalPass)
            {
                Assert.Contains("nothing", outcome.Narration);
                Assert.DoesNotContain("tells you what you want to know", outcome.Narration);
            }

            // Either way the threat itself happened and the world recorded it.
            Assert.Contains(outcome.Events, e => e.Type == WorldEventType.Threatened);
        }

        /// <summary>
        /// The same shape in the bribe verb, where getting it wrong also costs the player money.
        /// </summary>
        [Fact]
        public void PayingSomeoneWithNothingToSellDoesNotClaimTheyTalked()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.World.Knowledge.Teach(
                lab.Player, lab.Situation.TheftFactId, KnowledgeSource.Hearsay, 0.9, lab.Vanilla.Now, canProve: false);

            ActionContext context = FullContext(lab, lab.Situation.ThiefId);
            ActionOutcome outcome = lab.Actions.Get("bribe").Perform(context);

            if (outcome.Check != null && outcome.Check.Outcome == CheckOutcome.Pass)
            {
                Assert.DoesNotContain("pockets it and talks", outcome.Narration);
            }
        }

        /// <summary>
        /// Keeping stolen property ends the situation. It must be in history even when nobody
        /// saw - tagged unnoticed, so the world does not react to something it has not noticed,
        /// because affinity moving is itself information.
        /// </summary>
        [Fact]
        public void KeepingSomethingQuietlyIsStillRecorded()
        {
            TheftLaboratory lab = Prepared();
            ActionContext context = FullContext(lab, lab.Situation.VictimId);
            context.Witnesses.Clear();
            int affinityBefore = lab.Vanilla.GetAffinity(lab.Situation.VictimId);

            ActionOutcome outcome = lab.Actions.Get("keep_item").Perform(context);

            WorldEvent kept = outcome.Events.Find(e => e.Type == WorldEventType.Theft);
            Assert.NotNull(kept);
            Assert.Equal(lab.Player, kept.Actor);
            Assert.Contains(EventTags.Unnoticed, kept.Tags);

            // Nobody saw, so nobody reacts. Affinity moving would itself be information.
            Assert.Equal(affinityBefore, lab.Vanilla.GetAffinity(lab.Situation.VictimId));
        }

        /// <summary>Doing it in front of people is a different matter, and is not tagged quiet.</summary>
        [Fact]
        public void KeepingSomethingInFrontOfPeopleIsNotQuiet()
        {
            TheftLaboratory lab = Prepared();
            ActionContext context = FullContext(lab, lab.Situation.VictimId);
            Assert.NotEmpty(context.Witnesses);

            ActionOutcome outcome = lab.Actions.Get("keep_item").Perform(context);

            WorldEvent kept = outcome.Events.Find(e => e.Type == WorldEventType.Theft);
            Assert.NotNull(kept);
            Assert.DoesNotContain(EventTags.Unnoticed, kept.Tags);
            Assert.NotEmpty(kept.Witnesses);
        }

        /// <summary>
        /// A tester holding proof was told twice to "try again with evidence" - the one piece of
        /// advice that could not possibly help. Failing with proof is a different failure and
        /// needs a different way out.
        /// </summary>
        [Fact]
        public void BeingDisbelievedWhileHoldingProofDoesNotAdviseFindingProof()
        {
            TheftLaboratory lab = Prepared();
            lab.Checks = new FixedCheckResolver(CheckOutcome.Fail);

            ActionContext context = FullContext(lab, lab.Situation.VictimId);
            Assert.True(lab.World.Knowledge.CanProve(lab.Player, lab.Situation.TheftFactId));

            ActionOutcome outcome = lab.Actions.Get("expose").Perform(context);

            Assert.DoesNotContain("try again with evidence", string.Join(" ", outcome.Notes));
            Assert.DoesNotContain("does not take your word for it", outcome.Narration);
        }

        /// <summary>Without proof the original advice is the right advice.</summary>
        [Fact]
        public void BeingDisbelievedOnYourWordAloneStillAdvisesFindingProof()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.World.Knowledge.Teach(
                lab.Player, lab.Situation.TheftFactId, KnowledgeSource.Hearsay, 0.6, lab.Vanilla.Now, canProve: false);
            lab.Checks = new FixedCheckResolver(CheckOutcome.Fail);

            ActionOutcome outcome = lab.Actions.Get("expose").Perform(FullContext(lab, lab.Situation.VictimId));

            Assert.Contains("try again with evidence", string.Join(" ", outcome.Notes));
        }

        /// <summary>The offer warns before the money is spent, not after.</summary>
        [Fact]
        public void TheBribeOfferSaysWhenThereMayBeNothingToBuy()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.World.Knowledge.Teach(
                lab.Player, lab.Situation.TheftFactId, KnowledgeSource.Hearsay, 0.9, lab.Vanilla.Now, canProve: false);

            Availability availability = lab.Actions.Get("bribe")
                .GetAvailability(FullContext(lab, lab.Situation.ThiefId));

            Assert.True(availability.IsAvailable);
            Assert.Contains("nothing to sell", availability.Reason);
        }

        private static IEnumerable<EntityId> Everyone(TheftLaboratory lab)
        {
            yield return lab.Situation.WitnessId;
            yield return lab.Situation.ThiefId;
            yield return lab.Situation.VictimId;
        }

        private static ActionContext FullContext(TheftLaboratory lab, EntityId target)
        {
            ActionContext context = lab.Context(target);
            context.SubjectFact = lab.Situation.TheftFactId;
            context.SubjectItem = lab.Situation.ItemId;
            context.ThirdParty = target == lab.Situation.ThiefId
                ? lab.Situation.WitnessId
                : lab.Situation.ThiefId;
            return context;
        }

        private static int CountKnown(TheftLaboratory lab, EntityId who)
        {
            int count = 0;
            foreach (KnowledgeRecord _ in lab.World.Knowledge.BeliefsOf(who))
            {
                count++;
            }

            return count;
        }
    }
}
