using System.Collections.Generic;
using System.Linq;
using BrilliantQuesting.Developments;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Obligations;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQa-018. What a body can legitimately notice, and the ends that follow from it.
    ///
    /// The failure this fixture exists to catch reads plausibly in any dump: a guild acting on
    /// something true that nobody ever put in front of it. Its mirror is here too - a guild that
    /// goes on acting after the report it acted on was corrected - because both look like a working
    /// institution until you ask which filing the body was reading.
    ///
    /// Every case runs the production entry points over supplied state and supplied time, and
    /// asserts on the goals that result rather than on one call's return value.
    /// </summary>
    public class OrganizationPressureInterpretationTests
    {
        private static readonly EntityId Town = EntityId.Parse("zone_town");
        private static readonly EntityId Yard = EntityId.Parse("site_yard");
        private static readonly EntityId Carters = EntityId.Parse("org_carters");
        private static readonly EntityId Crew = EntityId.Parse("org_crew");
        private static readonly EntityId Household = EntityId.Parse("org_household");
        private static readonly EntityId Reeve = EntityId.Parse("npc_reeve");
        private static readonly EntityId Carter = EntityId.Parse("npc_carter");
        private static readonly EntityId Fence = EntityId.Parse("npc_fence");
        private static readonly EntityId Boss = EntityId.Parse("npc_boss");
        private static readonly EntityId Daughter = EntityId.Parse("npc_daughter");
        private static readonly EntityId Thief = EntityId.Parse("npc_thief");
        private static readonly EntityId Cart = EntityId.Parse("item_cart");
        private static readonly EntityId Occurrence = EntityId.Parse("evt_market_day");

        // -- the done-when -----------------------------------------------------------------------

        /// <summary>
        /// Three kinds of body, one town, the same pressures in front of all of them - and three
        /// different legitimate ends, because what a body answers with is its charter's question
        /// and not the pressure's.
        /// </summary>
        [Fact]
        public void ThreeOrganizationTypesDeriveDifferentLegitimateResponsesToTheSamePressures()
        {
            NarrativeWorldState world = Village();
            GameTime now = GameTime.FromDays(1);

            EntityId shortage = ShortageAt(world, Town, severity: 60);
            Fact theft = Theft(world, TruthState.True);
            Fact harm = Harm(world, Daughter);

            // Each body is told what its own people brought it. Nothing is told to all three.
            Reports(world, Carters, Carter, shortage, now);
            Reports(world, Crew, Fence, theft.Id, now);
            Reports(world, Household, Daughter, harm.Id, now);

            Pass(world, Carters, now);
            Pass(world, Crew, now);
            Pass(world, Household, now);

            Assert.Equal(OrganizationGoalKinds.RelieveShortage, Sole(world, Carters).Kind);
            Assert.Equal(OrganizationGoalKinds.RecoverHolding, Sole(world, Crew).Kind);
            Assert.Equal(OrganizationGoalKinds.ProtectMember, Sole(world, Household).Kind);

            // Different ends, and each bound to the record that answers it rather than to a label.
            Assert.Equal(GoalConditionKinds.DemandRelieved, Sole(world, Carters).Condition.Kind);
            Assert.Equal(Cart, Sole(world, Crew).Condition.Reference("item"));
            Assert.Equal(Daughter, Sole(world, Household).Condition.Reference("person"));
            Assert.All(
                new[] { Sole(world, Carters), Sole(world, Crew), Sole(world, Household) },
                goal => Assert.Equal(GoalSourceKind.InstitutionalReading, goal.Origin.Kind));
        }

        /// <summary>
        /// A true matter one of its own people holds, that nobody filed. The body does not have it,
        /// and cannot act on it, however much the save knows.
        /// </summary>
        [Fact]
        public void UnreportedMemberKnowledgeIsNotTheBodysKnowledge()
        {
            NarrativeWorldState world = Village();
            Fact theft = Theft(world, TruthState.True);

            // The fence saw it and can prove it. He has told nobody.
            world.Knowledge.Teach(Fence, theft.Id, KnowledgeSource.Witnessed, 1.0, GameTime.Zero, canProve: true);

            Assert.Empty(Read(world, Crew));
            Pass(world, Crew, GameTime.FromDays(1));
            Assert.Empty(world.Registry.GetOrganization(Crew).Goals);

            // Owning the cart is not a channel either: the ownership fact is in the graph the whole
            // time, and the body still has nothing on file.
            Assert.Equal(Cart, world.Knowledge.FindFact(Crew, FactPredicates.Possesses).Object);

            // He files it, and only then does the body have it.
            Assert.NotNull(InstitutionalReports.FileMemberReport(
                world, world.Registry.GetOrganization(Crew), Fence, theft.Id, GameTime.FromDays(2)));
            Pass(world, Crew, GameTime.FromDays(2));
            Assert.Equal(OrganizationGoalKinds.RecoverHolding, Sole(world, Crew).Kind);
        }

        /// <summary>
        /// A report that is wrong. The body acts on it exactly as it would act on a true one, and
        /// nothing tells it which it has.
        /// </summary>
        [Fact]
        public void AWrongReportProducesARealInstitutionalGoalAndTheBodyIsNeverToldItIsWrong()
        {
            NarrativeWorldState world = Village();
            Fact theft = Theft(world, TruthState.False);
            Reports(world, Crew, Fence, theft.Id, GameTime.FromDays(1));

            IReadOnlyList<OrganizationPressure> readings = Read(world, Crew);
            OrganizationPressure reading = Assert.Single(readings);
            Assert.True(reading.Misinformed);
            Assert.Equal(InstitutionalChannel.MemberReport, reading.Channel);
            Assert.Equal(Fence, world.Registry.GetOrganization(Crew).Receipts.Standing(theft.Id).FiledBy);

            Pass(world, Crew, GameTime.FromDays(1));
            OrganizationGoal end = Sole(world, Crew);
            Assert.Equal(OrganizationGoalKinds.RecoverHolding, end.Kind);
            Assert.Equal(GoalLifecycle.Active, end.Lifecycle);

            // The goal carries the claim it came off, not a verdict about it.
            Assert.Equal(theft.Id, end.Origin.RecordId);
        }

        /// <summary>
        /// The report is corrected. The old filing closes, the body stops reading it, and the end it
        /// formed retires - on the world's answer to what it wanted, asked only once the body had
        /// stopped having the matter in front of it.
        /// </summary>
        [Fact]
        public void ACorrectedReportClosesTheFilingAndRetiresTheEndItProduced()
        {
            NarrativeWorldState world = Village();
            Fact theft = Theft(world, TruthState.False);
            Organization crew = world.Registry.GetOrganization(Crew);

            Reports(world, Crew, Fence, theft.Id, GameTime.FromDays(1));
            Pass(world, Crew, GameTime.FromDays(1));
            OrganizationGoal end = Sole(world, Crew);
            InstitutionalReceipt filed = crew.Receipts.Standing(theft.Id);

            // The cart was never gone. Somebody files the correction.
            Fact found = new Fact(world.NewId("fact"), Cart, FactPredicates.LocatedAt, Yard);
            world.Knowledge.AddFact(found);
            world.Knowledge.Teach(Carter, found.Id, KnowledgeSource.Witnessed, 1.0, GameTime.FromDays(3), canProve: true);
            crew.Receipts.Retract(theft.Id, GameTime.FromDays(3), "mistaken");

            Assert.Equal(ReceiptStanding.Retracted, filed.Standing);
            Assert.Equal("mistaken", filed.ClosureCode);
            Assert.Empty(Read(world, Crew));

            Pass(world, Crew, GameTime.FromDays(3));

            // The ownership record never said otherwise, so what it wanted was already so.
            Assert.Equal(GoalLifecycle.Satisfied, end.Lifecycle);
            Assert.Equal("condition_met", end.RetirementCode);
            Assert.Single(crew.Goals);
        }

        /// <summary>
        /// A correcting filing that replaces rather than withdraws: the body reads the later one,
        /// the earlier one is closed against it, and both stay on the record.
        /// </summary>
        [Fact]
        public void ALaterFilingTakesOverFromTheOneItCorrectsAndBothStayOnTheRecord()
        {
            NarrativeWorldState world = Village();
            Fact theft = Theft(world, TruthState.False);
            Organization crew = world.Registry.GetOrganization(Crew);

            InstitutionalReceipt first = Reports(world, Crew, Fence, theft.Id, GameTime.FromDays(1));

            // The reeve looks into it and files the same matter with proof, which is a correction of
            // the standing of the report rather than of its subject.
            world.Knowledge.Teach(Reeve, theft.Id, KnowledgeSource.Document, 1.0, GameTime.FromDays(2), canProve: true);
            InstitutionalReceipt second = crew.Receipts.Correct(
                theft.Id, InstitutionalChannel.ExternalReport, Reeve, 1.0, provable: true, when: GameTime.FromDays(2));

            Assert.Equal(ReceiptStanding.Corrected, first.Standing);
            Assert.Equal(second.Id, first.SupersededBy);
            Assert.Same(second, crew.Receipts.Standing(theft.Id));
            Assert.Equal(2, crew.Receipts.Count);

            OrganizationPressure reading = Assert.Single(Read(world, Crew));
            Assert.Equal(InstitutionalChannel.ExternalReport, reading.Channel);
            Assert.True(reading.Provable);
            Assert.Empty(reading.Unknown);
        }

        /// <summary>
        /// The member who filed it dies. The institution's record is not that member's memory: what
        /// it was told stays on file, and the end it formed stands.
        /// </summary>
        [Fact]
        public void LosingTheMemberWhoFiledAReportDoesNotUnfileIt()
        {
            NarrativeWorldState world = Village();
            Fact theft = Theft(world, TruthState.True);
            Reports(world, Crew, Fence, theft.Id, GameTime.FromDays(1));
            Pass(world, Crew, GameTime.FromDays(1));
            OrganizationGoal end = Sole(world, Crew);

            // The one who filed it is gone: dead, and struck off the roll the body keeps.
            world.Registry.GetNpc(Fence).Alive = false;
            world.Registry.GetOrganization(Crew).MemberIds.Remove(Fence);
            world.Registry.GetOrganization(Crew).LeaderId = Boss;

            OrganizationPressure reading = Assert.Single(Read(world, Crew));
            Assert.Equal(Fence, world.Registry.GetOrganization(Crew).Receipts.Standing(theft.Id).FiledBy);
            Assert.Equal(InstitutionalChannel.MemberReport, reading.Channel);

            Pass(world, Crew, GameTime.FromDays(4));
            Assert.Equal(GoalLifecycle.Active, end.Lifecycle);
            Assert.Same(end, Sole(world, Crew));

            // And he cannot file anything more, because he is no longer one of theirs.
            Assert.Null(InstitutionalReports.FileMemberReport(
                world, world.Registry.GetOrganization(Crew), Fence, theft.Id, GameTime.FromDays(5)));
        }

        /// <summary>
        /// Reading is a read. Two calls agree exactly, and neither one leaves the save different -
        /// the goal owner is the only thing that records a change.
        /// </summary>
        [Fact]
        public void RepeatedInterpretationIsDeterministicAndChangesNothingUntilTheGoalOwnerRuns()
        {
            NarrativeWorldState world = Village();
            ShortageAt(world, Town, severity: 55);
            Fact theft = Theft(world, TruthState.True);
            Reports(world, Crew, Fence, theft.Id, GameTime.FromDays(1));

            string before = WorldStateSerializer.Save(world);

            IReadOnlyList<OrganizationPressure> first = Read(world, Crew);
            IReadOnlyList<OrganizationPressure> second = Read(world, Crew);
            Assert.NotEmpty(first);
            Assert.Equal(
                first.Select(reading => reading.Id + "|" + reading.Urgency + "|" + reading.Route + "|" + reading.ReceiptId),
                second.Select(reading => reading.Id + "|" + reading.Urgency + "|" + reading.Route + "|" + reading.ReceiptId));
            Assert.Equal(before, WorldStateSerializer.Save(world));

            OrganizationGoalEvolution.Advance(world, Crew, first, GameTime.FromDays(1));
            Assert.NotEqual(before, WorldStateSerializer.Save(world));
        }

        // -- the information contract --------------------------------------------------------------

        /// <summary>
        /// An owned loss reaches the body through a channel or not at all. The detector is holding
        /// the whole matter, and the body that owns the cart still has nothing until its books,
        /// one of its people, or somebody watching its yard puts it there.
        /// </summary>
        [Fact]
        public void AnOwnedLossNeedsAChannelAndOwnershipAloneIsNotOne()
        {
            NarrativeWorldState world = Village();
            Fact theft = Theft(world, TruthState.True);
            world.Registry.GetOrganization(Crew).SiteIds.Add(Yard);

            IReadOnlyList<Development> objective = DevelopmentDetector.Detect(world);
            Assert.Contains(objective, development => development.FocusFactId == theft.Id);
            Assert.Empty(OrganizationPressureView.Of(world, Crew, objective));

            // Somebody watching the yard holds it. Now there is a channel, and a route.
            world.Knowledge.Teach(Carter, theft.Id, KnowledgeSource.Witnessed, 0.9, GameTime.FromDays(1), canProve: false);
            Assert.NotNull(InstitutionalReports.FileHoldingObservation(
                world, world.Registry.GetOrganization(Crew), Carter, Yard, theft.Id, GameTime.FromDays(1)));

            OrganizationPressure reading = Assert.Single(OrganizationPressureView.Of(world, Crew, objective));
            Assert.Equal(InstitutionalChannel.HoldingObservation, reading.Channel);
            Assert.Equal(OrganizationRouteKind.Receipt, reading.Route);
        }

        /// <summary>
        /// A report the reporter cannot demonstrate does not hand the body the names in it. The
        /// detector knows who did it; the body was told that something happened.
        /// </summary>
        [Fact]
        public void AnUnprovableReportDoesNotNameTheCulpritToTheBody()
        {
            NarrativeWorldState world = Village();
            Fact theft = Theft(world, TruthState.True);
            world.Knowledge.Teach(Fence, theft.Id, KnowledgeSource.Hearsay, 0.6, GameTime.Zero, canProve: false);
            InstitutionalReports.FileMemberReport(
                world, world.Registry.GetOrganization(Crew), Fence, theft.Id, GameTime.FromDays(1));

            OrganizationPressure reading = Assert.Single(Read(world, Crew));
            Assert.DoesNotContain(Thief, reading.SubjectIds);
            Assert.Contains("implicated party", reading.Unknown);

            // With proof behind the filing, the same matter names him.
            world.Knowledge.Teach(Reeve, theft.Id, KnowledgeSource.Document, 1.0, GameTime.FromDays(2), canProve: true);
            world.Registry.GetOrganization(Crew).Receipts.Correct(
                theft.Id, InstitutionalChannel.ExternalReport, Reeve, 1.0, provable: true, when: GameTime.FromDays(2));
            Assert.Contains(Thief, Assert.Single(Read(world, Crew)).SubjectIds);
        }

        /// <summary>
        /// A shortage at a place a body keeps is plainly there, and a crime at the same place is
        /// not. Holding the deed to the yard is not a way of learning what happened in it.
        /// </summary>
        [Fact]
        public void AnOpenConditionOfItsHoldingIsARouteAndACrimeAtTheSamePlaceIsNot()
        {
            NarrativeWorldState world = Village();
            Organization carters = world.Registry.GetOrganization(Carters);
            carters.SiteIds.Add(Town);
            ShortageAt(world, Town, severity: 50);
            Theft(world, TruthState.True);

            IReadOnlyList<OrganizationPressure> readings = Read(world, Carters);
            OrganizationPressure reading = Assert.Single(readings);
            Assert.Equal(OrganizationRouteKind.OwnHolding, reading.Route);
            Assert.True(reading.HasPressure(DevelopmentPressures.Shortage));
            Assert.True(reading.HasStake(OrganizationStakeKind.Holding));

            Pass(world, Carters, GameTime.FromDays(1));
            Assert.Equal(OrganizationGoalKinds.RelieveShortage, Sole(world, Carters).Kind);
        }

        /// <summary>
        /// A charter that does not answer a matter forms nothing, and says so. A body with no
        /// registered charter does not inherit a trade guild's appetite for the town's troubles.
        /// </summary>
        [Fact]
        public void ACharterThatAdmitsNothingFormsNothingAndSaysSo()
        {
            NarrativeWorldState world = Village();
            Organization choir = world.Registry.Add(new Organization(EntityId.Parse("org_choir"), "the choir", "choir")
            {
                LeaderId = Reeve
            });
            choir.MemberIds.Add(Reeve);

            EntityId shortage = ShortageAt(world, Town, severity: 70);
            world.Knowledge.Teach(Reeve, shortage, KnowledgeSource.Witnessed, 1.0, GameTime.Zero, canProve: true);
            InstitutionalReports.FileMemberReport(world, choir, Reeve, shortage, GameTime.FromDays(1));

            IReadOnlyList<OrganizationGoalChange> changes = Pass(world, choir.Id, GameTime.FromDays(1));
            Assert.Equal(GoalChangeKind.NoResponse, Assert.Single(changes).Kind);
            Assert.Equal("charter_admits_nothing", changes[0].Because);
            Assert.Empty(choir.Goals);
            Assert.False(OrganizationPolicy.Admits("choir", OrganizationGoalKinds.RelieveShortage));
        }

        /// <summary>
        /// A month of passes over one shortage: the end appears, follows the pressure, and closes
        /// when what it wanted comes about. Nothing authors a second goal anywhere in the chain.
        /// </summary>
        [Fact]
        public void AMonthOfPassesFormsRevisesAndClosesOneEndWithoutAuthoredFollowUp()
        {
            NarrativeWorldState world = Village();
            world.Registry.GetOrganization(Carters).SiteIds.Add(Town);
            EntityId source = ShortageAt(world, Town, severity: 40);

            List<GoalChangeKind> seen = new List<GoalChangeKind>();
            for (long day = 0; day <= 30; day++)
            {
                GameTime now = GameTime.FromDays(day);
                if (day == 8)
                {
                    world.Demands.AddOrUpdate(
                        Town, LocalDemandCategory.Food, 90, GameTime.Zero, GameTime.FromDays(60), source);
                }

                if (day == 20)
                {
                    world.Demands.Relieve(Town, LocalDemandCategory.Food, source, 100, 0, now);
                }

                foreach (OrganizationGoalChange change in Pass(world, Carters, now))
                {
                    seen.Add(change.Kind);
                }
            }

            Assert.Equal(GoalChangeKind.Created, seen.First());
            Assert.Contains(GoalChangeKind.Reweighted, seen);
            Assert.Contains(GoalChangeKind.Satisfied, seen);

            OrganizationGoal end = Sole(world, Carters);
            Assert.Equal(GoalLifecycle.Satisfied, end.Lifecycle);
            Assert.Equal("condition_met", end.RetirementCode);
            Assert.Equal(GoalConditionKinds.DemandRelieved, end.Condition.Kind);
        }

        /// <summary>
        /// A goal whose cause is still on file is not closed by a background pass, however true its
        /// condition has quietly become. Closing it here would hand the institution the save's
        /// knowledge for free.
        /// </summary>
        [Fact]
        public void AnEndWhoseCauseIsStillOnFileIsNotClosedByTheWorldQuietlyAgreeing()
        {
            NarrativeWorldState world = Village();
            Fact harm = Harm(world, Daughter);
            Reports(world, Household, Daughter, harm.Id, GameTime.FromDays(1));
            Pass(world, Household, GameTime.FromDays(1));

            OrganizationGoal end = Sole(world, Household);
            Assert.Equal(OrganizationGoalKinds.ProtectMember, end.Kind);

            // She is alive, so what the body wants is already so - and the body has not been told
            // that the matter is over, so nothing closes.
            Assert.Equal(GoalConditionState.Met, end.Evaluate(world));
            Pass(world, Household, GameTime.FromDays(9));
            Assert.Equal(GoalLifecycle.Active, end.Lifecycle);

            world.Registry.GetOrganization(Household).Receipts.Retract(harm.Id, GameTime.FromDays(10), "passed");
            Pass(world, Household, GameTime.FromDays(10));
            Assert.Equal(GoalLifecycle.Satisfied, end.Lifecycle);
        }

        /// <summary>
        /// Regression. A goal can now end without its condition holding, and the existing activity
        /// owner used to treat "not satisfied" as "still wanted" - which would have had a body
        /// spending its days on an end it had already given up or handed on.
        /// </summary>
        [Fact]
        public void TheActivityOwnerActsOnActiveEndsRatherThanMerelyUnsatisfiedOnes()
        {
            NarrativeWorldState world = Village();
            Organization crew = world.Registry.GetOrganization(Crew);
            crew.SiteIds.Add(Yard);

            OrganizationGoal given = new OrganizationGoal(OrganizationActivity.ProtectHolding, Yard, 90);
            given.Abandon(GameTime.FromDays(1), "lapsed");
            crew.Goals.Add(given);

            int before = world.Registry.GetSite(Yard).DangerLevel;
            Assert.Equal(0, new OrganizationActivity(world).Advance(GameTime.FromDays(2)));
            Assert.Equal(before, world.Registry.GetSite(Yard).DangerLevel);
            Assert.Equal(0, given.Progress);

            // The same end, still wanted, is acted on exactly as it always was.
            given.Reopen();
            Assert.Equal(1, new OrganizationActivity(world).Advance(GameTime.FromDays(3)));
            Assert.True(world.Registry.GetSite(Yard).DangerLevel > before);
        }

        // -- persistence ---------------------------------------------------------------------------

        /// <summary>
        /// What the body was told and what it decided both survive a reload, and the pass after the
        /// reload lands on the goal it already had rather than on a second copy of it.
        /// </summary>
        [Fact]
        public void ReceiptsAndInstitutionalEndsSurviveAReloadAndDoNotStackOnTheNextPass()
        {
            NarrativeWorldState world = Village();
            Fact theft = Theft(world, TruthState.True);
            Reports(world, Crew, Fence, theft.Id, GameTime.FromDays(1));
            Pass(world, Crew, GameTime.FromDays(1));
            OrganizationGoal before = Sole(world, Crew);
            InstitutionalReceipt filed = world.Registry.GetOrganization(Crew).Receipts.Standing(theft.Id);

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(world));
            Organization crew = reloaded.Registry.GetOrganization(Crew);
            InstitutionalReceipt restored = crew.Receipts.Standing(theft.Id);

            Assert.Equal(filed.Id, restored.Id);
            Assert.Equal(filed.Channel, restored.Channel);
            Assert.Equal(filed.FiledBy, restored.FiledBy);
            Assert.Equal(filed.Confidence, restored.Confidence, 8);
            Assert.Equal(filed.Provable, restored.Provable);
            Assert.Equal(filed.FiledAt.TotalMinutes, restored.FiledAt.TotalMinutes);

            OrganizationGoal after = Assert.Single(crew.Goals);
            Assert.Equal(before.Identity, after.Identity);
            Assert.Equal(before.Origin.SourceId, after.Origin.SourceId);
            Assert.Equal(before.Origin.RecordId, after.Origin.RecordId);
            Assert.Equal(before.Origin.FormedAt, after.Origin.FormedAt);

            Pass(reloaded, Crew, GameTime.FromDays(2));
            Assert.Single(crew.Goals);
            Assert.Equal(GoalLifecycle.Active, after.Lifecycle);

            // And the whole document is stable across a second round trip.
            string canonical = WorldStateSerializer.Save(reloaded);
            Assert.Equal(canonical, WorldStateSerializer.Save(WorldStateSerializer.Load(canonical)));
        }

        /// <summary>
        /// An organization node written before this step. The lifecycle comes from the one thing it
        /// recorded and nothing else is invented: no condition, no cause, and nothing on file.
        /// </summary>
        [Fact]
        public void AnOrganizationSavedBeforeThisStepLoadsWithHonestDefaults()
        {
            NarrativeWorldState world = Village();
            JsonValue document = WorldStateSerializer.ToJson(world);

            // The node exactly as a serializer older than BQa-018 wrote it: no receipts key at all,
            // and goals carrying nothing but the boolean.
            JsonValue body = JsonValue.Object()
                .Set("id", Crew.Value)
                .Set("name", "the quiet hand")
                .Set("type", "criminal_crew")
                .Set("leader", Boss.Value)
                .Set("wealth", 30)
                .Set("legitimacy", 20)
                .Set("aggression", 60)
                .Set("lastActed", 0.0)
                .Set("goals", JsonValue.Array()
                    .Add(JsonValue.Object()
                        .Set("kind", OrganizationActivity.BuildReserves)
                        .Set("subject", EntityId.None.Value)
                        .Set("weight", 40)
                        .Set("progress", 25)
                        .Set("satisfied", false))
                    .Add(JsonValue.Object()
                        .Set("kind", OrganizationActivity.ProtectHolding)
                        .Set("subject", Yard.Value)
                        .Set("weight", 60)
                        .Set("progress", 100)
                        .Set("satisfied", true)))
                .Set("members", JsonValue.Array().Add(JsonValue.String(Fence.Value)))
                .Set("sites", JsonValue.Array().Add(JsonValue.String(Yard.Value)));

            document.Set("organizations", JsonValue.Array().Add(body));
            Assert.Null(body["receipts"]);

            Organization loaded = WorldStateSerializer.Load(document.ToJson()).Registry.GetOrganization(Crew);

            Assert.Empty(loaded.Receipts);
            Assert.Equal(GoalLifecycle.Active, loaded.Goals[0].Lifecycle);
            Assert.Equal(GoalLifecycle.Satisfied, loaded.Goals[1].Lifecycle);
            Assert.All(loaded.Goals, goal =>
            {
                Assert.False(goal.HasCondition);
                Assert.Equal(GoalSourceKind.Unknown, goal.Origin.Kind);
                Assert.Equal(string.Empty, goal.SupersededBy);
            });
            Assert.Equal(25, loaded.Goals[0].Progress);
            Assert.Equal(GoalConditionState.Unsupported, loaded.Goals[0].Evaluate(world));

            // The existing activity owner still reads them exactly as it always did.
            Assert.Equal(string.Empty, loaded.Goals[1].RetirementCode);
            Assert.True(loaded.Goals[1].Satisfied);
        }

        /// <summary>
        /// Closed filings are kept to a bound, and everything the body still reads is kept whatever
        /// the bound is. A save node that grows without limit is a save node that eventually breaks.
        /// </summary>
        [Fact]
        public void ClosedFilingsAreBoundedAndStandingOnesAreNever()
        {
            NarrativeWorldState world = Village();
            Organization crew = world.Registry.GetOrganization(Crew);

            for (int i = 0; i < InstitutionalReceiptLedger.MaxClosedRetained + 10; i++)
            {
                Fact claim = new Fact(world.NewId("fact"), Thief, FactPredicates.Stole, Cart, "a cart");
                world.Knowledge.AddFact(claim);
                crew.Receipts.File(
                    InstitutionalChannel.Accounting, claim.Id, EntityId.None, 1.0, false, GameTime.FromDays(i));
                crew.Receipts.Retract(claim.Id, GameTime.FromDays(i), "withdrawn");
            }

            Fact standing = Theft(world, TruthState.True);
            crew.Receipts.File(
                InstitutionalChannel.Accounting, standing.Id, EntityId.None, 1.0, false, GameTime.FromDays(50));

            Assert.Equal(InstitutionalReceiptLedger.MaxClosedRetained + 1, crew.Receipts.Count);
            Assert.Single(crew.Receipts.StandingReceipts());
            Assert.NotNull(crew.Receipts.Standing(standing.Id));
        }

        // -- fixture -----------------------------------------------------------------------------

        private static IReadOnlyList<OrganizationPressure> Read(NarrativeWorldState world, EntityId bodyId) =>
            OrganizationPressureView.Of(world, bodyId, DevelopmentDetector.Detect(world));

        private static IReadOnlyList<OrganizationGoalChange> Pass(
            NarrativeWorldState world, EntityId bodyId, GameTime now) =>
            OrganizationGoalEvolution.Advance(world, bodyId, Read(world, bodyId), now);

        private static OrganizationGoal Sole(NarrativeWorldState world, EntityId bodyId) =>
            Assert.Single(world.Registry.GetOrganization(bodyId).Goals);

        private static InstitutionalReceipt Reports(
            NarrativeWorldState world, EntityId bodyId, EntityId memberId, EntityId claimId, GameTime when)
        {
            world.Knowledge.Teach(memberId, claimId, KnowledgeSource.Witnessed, 1.0, GameTime.Zero, canProve: true);
            InstitutionalReceipt receipt = InstitutionalReports.FileMemberReport(
                world, world.Registry.GetOrganization(bodyId), memberId, claimId, when);
            Assert.NotNull(receipt);
            return receipt;
        }

        private static EntityId ShortageAt(NarrativeWorldState world, EntityId place, int severity)
        {
            Fact source = new Fact(world.NewId("fact"), place, FactPredicates.Needs, EntityId.None, "grain");
            world.Knowledge.AddFact(source);
            world.Demands.AddOrUpdate(
                place, LocalDemandCategory.Food, severity, GameTime.Zero, GameTime.FromDays(60), source.Id);
            return source.Id;
        }

        private static Fact Theft(NarrativeWorldState world, TruthState truth)
        {
            Fact claim = new Fact(
                world.NewId("fact"), Thief, FactPredicates.Stole, Cart, "a cart",
                truth, secrecy: 0, originEvent: Occurrence);
            world.Knowledge.AddFact(claim);
            return claim;
        }

        private static Fact Harm(NarrativeWorldState world, EntityId person)
        {
            Fact claim = new Fact(
                world.NewId("fact"), Thief, FactPredicates.Extorted, person, "leaned on her",
                TruthState.True, secrecy: 0, originEvent: Occurrence);
            world.Knowledge.AddFact(claim);
            return claim;
        }

        private static NarrativeWorldState Village()
        {
            NarrativeWorldState world = new NarrativeWorldState(17);
            world.Registry.Add(new NarrativeSite(Town, "Kell's Ford", "village"));
            world.Registry.Add(new NarrativeSite(Yard, "the carters' yard", "yard"));
            world.Registry.Add(new NarrativeNpc(Reeve, "Hald") { Occupation = "reeve", HomeSiteId = Town });
            world.Registry.Add(new NarrativeNpc(Carter, "Mira") { Occupation = "carter", HomeSiteId = Town });
            world.Registry.Add(new NarrativeNpc(Fence, "Orvo") { HomeSiteId = Town });
            world.Registry.Add(new NarrativeNpc(Boss, "Ketil") { HomeSiteId = Town });
            world.Registry.Add(new NarrativeNpc(Daughter, "Sela") { HomeSiteId = Town });
            world.Registry.Add(new NarrativeNpc(Thief, "Bran") { HomeSiteId = Town });

            Organization carters = world.Registry.Add(
                new Organization(Carters, "the carters", "merchant_association") { LeaderId = Carter });
            carters.MemberIds.Add(Carter);

            Organization crew = world.Registry.Add(
                new Organization(Crew, "the quiet hand", "criminal_crew") { LeaderId = Boss });
            crew.MemberIds.Add(Fence);
            crew.MemberIds.Add(Boss);

            Organization household = world.Registry.Add(
                new Organization(Household, "the Sela household", "family") { LeaderId = Daughter });
            household.MemberIds.Add(Daughter);

            // The cart is the crew's, on the record, which is what makes recovering it theirs to
            // want - and is deliberately not a way of learning that it went missing.
            Fact ownership = new Fact(world.NewId("fact"), Crew, FactPredicates.Possesses, Cart, "a cart");
            world.Knowledge.AddFact(ownership);
            return world;
        }
    }
}
