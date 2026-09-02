using System.Collections.Generic;
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
    /// <summary>
    /// BQ-055 records the debt; BQ-113 makes it a reward the player owns. The distinction these
    /// tests exist to hold is that a favour is a *stored option*: the world writes it down when it
    /// is earned, the player picks the moment it is spent, and nothing else may spend it for them.
    /// </summary>
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

        // -- the done-when -----------------------------------------------------------------------

        /// <summary>
        /// BQ-113's condition: the player spends a favour from the dialogue surface, and the world
        /// honours it. The option is taken from the same projection the Drama node draws, at the
        /// same seven-choice cap, so passing here means it is genuinely reachable in a conversation
        /// rather than only through the registry.
        /// </summary>
        [Fact]
        public void AFavorIsSpentFromDialogueAndTheWorldHonoursIt()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Checks = new FixedCheckResolver(CheckOutcome.Fail);
            SocialObligation favor = RecordFavor(lab, lab.Situation.VictimId, lab.Player);

            ActionIntentOption offered = Dialogue(lab, lab.Situation.VictimId)
                .SingleOrDefault(option => option.Action.Id == "call_favor");
            Assert.NotNull(offered);
            Assert.Contains("Call in the favour", offered.Label);
            Assert.Contains(lab.World.Registry.NameOf(lab.Situation.VictimId), offered.Label);

            ActionOutcome outcome = lab.Perform("call_favor", lab.Situation.VictimId);

            Assert.True(outcome.Succeeded);
            Assert.Equal(SocialObligationStatus.Fulfilled, favor.Status);
            Assert.Contains("would have refused", outcome.Narration);
            Assert.Contains(outcome.Notes, note => note.Contains("spent recorded favor " + favor.Id));
            Assert.Contains(outcome.Events, e => e.Type == WorldEventType.FavorRedeemed
                                                 && e.Related.Contains(favor.Id));
            Assert.Contains(outcome.Events, e => e.Type == WorldEventType.PromiseMade
                                                 && e.Actor == lab.Situation.VictimId
                                                 && e.Target == lab.Player);
        }

        /// <summary>Nothing is owed, so there is nothing to call in and the option is not drawn.</summary>
        [Fact]
        public void CallingInAFavorIsNotOfferedWhenNothingIsOwed()
        {
            TheftLaboratory lab = TheftLaboratory.Create();

            Availability availability = lab.Actions.Get("call_favor").GetAvailability(lab.Context(lab.Situation.VictimId));

            Assert.False(availability.IsAvailable);
            Assert.Contains("owe you nothing", availability.Reason);
            Assert.DoesNotContain(Dialogue(lab, lab.Situation.VictimId), o => o.Action.Id == "call_favor");
        }

        /// <summary>A favour is spent once. The second ask has nothing left behind it.</summary>
        [Fact]
        public void AFavorCanOnlyBeCalledInOnce()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            SocialObligation favor = RecordFavor(lab, lab.Situation.VictimId, lab.Player);

            lab.Perform("call_favor", lab.Situation.VictimId);
            ActionOutcome again = lab.Perform("call_favor", lab.Situation.VictimId);

            Assert.Equal(SocialObligationStatus.Fulfilled, favor.Status);
            Assert.Contains("owe you nothing", again.Narration);
            Assert.DoesNotContain(again.Events, e => e.Type == WorldEventType.FavorRedeemed);
        }

        // -- the skip repair ---------------------------------------------------------------------

        /// <summary>
        /// The regression BQ-113 exists to prevent, and the defect the step being skipped left in
        /// place: persuasion used to reach into the ledger and spend an open favour the instant its
        /// roll failed. The player never chose it, was never asked, and the strongest reward in the
        /// vocabulary could be consumed by an ask they would happily have taken a refusal on.
        /// </summary>
        [Fact]
        public void AFailedPersuasionNeverSpendsAFavorOnThePlayersBehalf()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Checks = new FixedCheckResolver(CheckOutcome.Fail);
            SocialObligation favor = RecordFavor(lab, lab.Situation.VictimId, lab.Player);

            ActionOutcome outcome = lab.Perform("persuade", lab.Situation.VictimId);

            Assert.False(outcome.Succeeded);
            Assert.Contains("turns you down", outcome.Narration);
            Assert.Equal(SocialObligationStatus.Open, favor.Status);
            Assert.DoesNotContain(outcome.Events, e => e.Type == WorldEventType.FavorRedeemed);
            Assert.True(lab.Actions.Get("call_favor").GetAvailability(lab.Context(lab.Situation.VictimId)).IsAvailable);
        }

        [Fact]
        public void ASuccessfulAskDoesNotSpendAFavor()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Checks = new FixedCheckResolver(CheckOutcome.Pass);
            SocialObligation favor = RecordFavor(lab, lab.Situation.VictimId, lab.Player);

            ActionOutcome outcome = lab.Perform("persuade", lab.Situation.VictimId);

            Assert.True(outcome.Succeeded);
            Assert.Equal(SocialObligationStatus.Open, favor.Status);
            Assert.DoesNotContain(outcome.Events, e => e.Type == WorldEventType.FavorRedeemed);
        }

        // -- earning one ------------------------------------------------------------------------

        /// <summary>
        /// The other half of the historical gap: with nothing in play ever recording a favour, a
        /// spendable favour was unreachable in a real save. Substantive help is what earns one, and
        /// it is derived from the event every helping verb already records rather than from any
        /// particular verb.
        /// </summary>
        [Fact]
        public void SubstantialHelpLeavesSomebodyOwingAFavor()
        {
            TheftLaboratory lab = TheftLaboratory.Create();

            Help(lab, lab.Situation.VictimId, 0.7);

            SocialObligation owed = Assert.Single(lab.World.Obligations.Records);
            Assert.Equal(SocialObligationKind.Favor, owed.Kind);
            Assert.Equal(lab.Situation.VictimId, owed.Debtor);
            Assert.Equal(lab.Player, owed.Creditor);
            Assert.True(owed.IsOpen);

            // Unbound on purpose: the player decides later what to spend it on.
            Assert.True(owed.Subject.IsNone);
            Assert.Equal(string.Empty, owed.Purpose);
            Assert.True(lab.Actions.Get("call_favor").GetAvailability(lab.Context(lab.Situation.VictimId)).IsAvailable);
        }

        /// <summary>Warmth is not a debt. Small talk records 0.2, and must not mint anything.</summary>
        [Fact]
        public void SmallHelpAndPleasantriesEarnNothing()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Checks = new FixedCheckResolver(CheckOutcome.CriticalPass);

            lab.Perform("rapport", lab.Situation.VictimId);
            Help(lab, lab.Situation.WitnessId, 0.4);

            Assert.Empty(lab.World.Obligations.Records);
        }

        /// <summary>
        /// Somebody owes you a favour, not a column of them. Without this, the cheapest helping
        /// verb repeated is a favour printer.
        /// </summary>
        [Fact]
        public void HelpingTheSamePersonAgainDoesNotStackFavors()
        {
            TheftLaboratory lab = TheftLaboratory.Create();

            Help(lab, lab.Situation.VictimId, 0.7);
            Help(lab, lab.Situation.VictimId, 0.9);

            Assert.Single(lab.World.Obligations.Records);

            // Once it has been spent, helping them again can earn a new one.
            lab.Perform("call_favor", lab.Situation.VictimId);
            Help(lab, lab.Situation.VictimId, 0.7);

            Assert.Equal(2, lab.World.Obligations.Records.Count);
            Assert.Single(lab.World.Obligations.Records.Where(o => o.IsOpen));
        }

        /// <summary>Help nobody registered as a person is not a debt either.</summary>
        [Fact]
        public void HelpTowardsNobodyEarnsNothing()
        {
            TheftLaboratory lab = TheftLaboratory.Create();

            Help(lab, lab.World.NewId("npc"), 0.9);

            Assert.Empty(lab.World.Obligations.Records);
        }

        // -- persistence ------------------------------------------------------------------------

        [Fact]
        public void ObligationsSurviveTheSave()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            SocialObligation favor = RecordFavor(lab, lab.Situation.VictimId, lab.Player, lab.Situation.TheftFactId);
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

        /// <summary>
        /// A favour earned in play and left unspent is still there after a reload, and is still
        /// exactly one favour: loading must not redispatch the help that earned it and mint a
        /// second.
        /// </summary>
        [Fact]
        public void AnEarnedFavorIsStillSpendableAfterAReload()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            Help(lab, lab.Situation.VictimId, 0.7);

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(lab.World));
            SocialObligation restored = Assert.Single(reloaded.Obligations.Records);

            Assert.True(restored.IsOpen);
            Assert.NotNull(reloaded.Obligations.FindOpenFavor(lab.Situation.VictimId, lab.Player, ActionBinding.Empty));
        }

        // -- helpers ----------------------------------------------------------------------------

        /// <summary>
        /// The dialogue surface: the same projection the Drama node draws, at the same cap.
        /// </summary>
        private static List<ActionIntentOption> Dialogue(TheftLaboratory lab, EntityId target)
        {
            ActionContext context = lab.Context(target);
            return ContextualActionProjection.Project(lab.Actions.Discover(context), context, 7);
        }

        /// <summary>
        /// A good turn, recorded the way every helping verb in the library records one. Going
        /// through the ledger rather than a particular verb is deliberate: the accrual rule reads
        /// the event, so this is the seam all of them share.
        /// </summary>
        private static void Help(TheftLaboratory lab, EntityId who, double magnitude)
        {
            lab.World.Record(
                WorldEventType.Helped,
                lab.Player,
                who,
                lab.Vanilla.Now,
                magnitude,
                lab.Zone,
                threadId: lab.Situation.Thread.Id);
        }

        private static SocialObligation RecordFavor(
            TheftLaboratory lab,
            EntityId debtor,
            EntityId creditor,
            EntityId subject = default)
        {
            WorldEvent source = lab.World.Record(
                WorldEventType.FavorOwed,
                debtor,
                creditor,
                lab.Vanilla.Now,
                0.5,
                lab.Zone,
                related: subject.IsNone ? null : new[] { subject },
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
