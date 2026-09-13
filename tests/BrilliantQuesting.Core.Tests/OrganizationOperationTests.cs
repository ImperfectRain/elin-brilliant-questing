using System.Collections.Generic;
using System.Linq;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Consequences;
using BrilliantQuesting.Developments;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Relationships;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQa-019. What a body does about what it wants, and what it does when it can do nothing.
    ///
    /// The failure this fixture exists to catch is the one the old owner had everywhere: a body
    /// that could not reach a recruit, could not find the rival, held no yard or wanted something
    /// nothing had ever heard of came out of the pass richer. Wealth from a blocked end is not a
    /// small arithmetic bug - it is an economy nobody owns, paid out for failure, and it is why
    /// half of these cases assert on what did <em>not</em> change.
    ///
    /// Every case runs the production owner over supplied state and supplied time. Headless
    /// throughout: a green pass here says nothing about any Elin hook scheduling one, which is
    /// BQa-020's to show.
    /// </summary>
    public class OrganizationOperationTests
    {
        private static readonly EntityId Town = EntityId.Parse("zone_town");
        private static readonly EntityId Yard = EntityId.Parse("site_yard");
        private static readonly EntityId Carters = EntityId.Parse("org_carters");
        private static readonly EntityId Crew = EntityId.Parse("org_crew");
        private static readonly EntityId Household = EntityId.Parse("org_household");
        private static readonly EntityId Rivals = EntityId.Parse("org_rivals");
        private static readonly EntityId Carter = EntityId.Parse("npc_carter");
        private static readonly EntityId Fence = EntityId.Parse("npc_fence");
        private static readonly EntityId Boss = EntityId.Parse("npc_boss");
        private static readonly EntityId Daughter = EntityId.Parse("npc_daughter");
        private static readonly EntityId Thief = EntityId.Parse("npc_thief");
        private static readonly EntityId Stranger = EntityId.Parse("npc_stranger");
        private static readonly EntityId Player = EntityId.Parse("npc_player");
        private static readonly EntityId Cart = EntityId.Parse("item_cart");
        private static readonly EntityId Occurrence = EntityId.Parse("evt_market_day");

        // -- the done-when -------------------------------------------------------------------------

        /// <summary>
        /// Three kinds of body, three legitimate ends they arrived at on their own, and three
        /// different operations - each one carried by one of that body's own people through the
        /// same registered verbs anybody else would use.
        /// </summary>
        [Fact]
        public void ThreeKindsOfBodyExecuteThreeDifferentSupportedOperationsThroughProductionCore()
        {
            Bench bench = Bench.Create();
            GameTime now = GameTime.FromDays(1);

            bench.Reports(Carters, Carter, bench.Shortage, now);
            bench.Reports(Crew, Fence, bench.Theft, now);
            bench.Reports(Household, Daughter, bench.Harm, now);
            bench.Interpret(now);

            Assert.Equal(OrganizationGoalKinds.RelieveShortage, bench.Sole(Carters).Kind);
            Assert.Equal(OrganizationGoalKinds.RecoverHolding, bench.Sole(Crew).Kind);
            Assert.Equal(OrganizationGoalKinds.ProtectMember, bench.Sole(Household).Kind);

            OrganizationActivityPass pass = bench.Act(now);

            List<OrganizationOperation> planned = pass.Planned;
            Assert.Equal(3, planned.Select(operation => operation.OrganizationId).Distinct().Count());

            // Three different responses, not three names for one. The kinds of change differ
            // because the ends want different things, and the verbs differ with them - which is
            // the BQa-010/BQa-011 bridge doing the work rather than a table of organization verbs.
            Assert.Equal(3, planned.Select(operation => operation.Effects[0]).Distinct().Count());
            Assert.True(planned.Select(operation => operation.Kind).Distinct().Count() >= 3);

            // Every one of them is a real member of that body, and none of them is the player.
            Assert.All(planned, operation =>
            {
                Assert.Equal(OrganizationMeans.Delegated, operation.Means);
                Organization body = bench.World.Registry.GetOrganization(operation.OrganizationId);
                Assert.Contains(operation.Agent, body.MemberIds);
                Assert.NotEqual(Player, operation.Agent);
                Assert.True(bench.World.Registry.GetNpc(operation.Agent).Alive);
                Assert.NotNull(bench.Actions.Get(operation.Kind));
            });

            // And they were settled together, in one batch, against each other - with something
            // actually performed, and no reservation left standing afterwards.
            Assert.NotNull(pass.Arbitration);
            Assert.Equal(planned.Count, pass.Arbitration.Decisions.Count);
            Assert.Equal(0, pass.Arbitration.OutstandingClaims);
            Assert.Contains(pass.Arbitration.Decisions, d => d.Verdict == ArbitrationVerdict.Attempted);
            Assert.True(pass.Committed > 0);
        }

        /// <summary>
        /// The whole of what this step removes. An end nothing in this build can execute used to
        /// pay the body; now it is a refusal, and the record is exactly as it was.
        /// </summary>
        [Fact]
        public void AnEndNothingSupportsEarnsTheBodyNothing()
        {
            Bench bench = Bench.Create();
            Organization crew = bench.Body(Crew);
            crew.Wealth = 30;
            crew.Goals.Add(new OrganizationGoal("court_the_governor", EntityId.None, 90));

            OrganizationActivityPass pass = bench.Act(GameTime.FromDays(1));

            Assert.Empty(pass.Planned);
            Assert.Equal(0, pass.Committed);
            Assert.Equal(30, crew.Wealth);
            Assert.Equal(0, crew.Goals[0].Progress);
            Assert.Empty(bench.World.Ledger.Events.Where(e => e.Type == WorldEventType.OrganizationActed));
            Assert.Contains(pass.Refusals, why => why.Contains("court_the_governor"));
        }

        /// <summary>
        /// The same rule for a blocked one rather than an unknown one: the body wants a thing this
        /// build understands perfectly well and simply cannot get at today.
        /// </summary>
        [Fact]
        public void ABlockedEndEarnsTheBodyNothingEither()
        {
            Bench bench = Bench.Create();
            Organization crew = bench.Body(Crew);
            crew.Wealth = 30;

            // A rival that is not in the registry, and a recruit nobody can reach because the crew
            // keeps nowhere. Both used to fall through to building wealth.
            crew.Goals.Add(new OrganizationGoal(OrganizationActivity.RaidOrganization, EntityId.Parse("org_ghost"), 90));
            crew.Goals.Add(new OrganizationGoal(OrganizationActivity.ExpandMembership, EntityId.None, 80));

            OrganizationActivityPass pass = bench.Act(GameTime.FromDays(1));

            Assert.Empty(pass.Planned);
            Assert.Equal(30, crew.Wealth);
            Assert.Equal(2, pass.Refusals.Count);
            Assert.Empty(bench.World.Ledger.Events.Where(e => e.Type == WorldEventType.OrganizationActed));
        }

        /// <summary>
        /// Reserves come from a holding the body's own record keeps, and a body that keeps nothing
        /// collects nothing however long it waits.
        /// </summary>
        [Fact]
        public void ReservesComeFromANamedHoldingRatherThanFromNowhere()
        {
            Bench bench = Bench.Create();
            Organization crew = bench.Body(Crew);
            crew.Wealth = 10;
            crew.Goals.Add(new OrganizationGoal(OrganizationActivity.BuildReserves, EntityId.None, 70));

            Assert.Empty(bench.Act(GameTime.FromDays(1)).Planned);
            Assert.Equal(10, crew.Wealth);

            crew.SiteIds.Add(Yard);
            OrganizationActivityPass pass = bench.Act(GameTime.FromDays(2));

            OrganizationOperation collected = Assert.Single(pass.Planned);
            Assert.Equal(OrganizationActivity.BuildReserves, collected.Kind);
            Assert.Equal(Yard, collected.Subject);
            Assert.Equal(10 + OrganizationOperations.ReserveYield, crew.Wealth);

            // The record names the holding it came out of, which is what "a named inflow" means.
            WorldEvent action = Assert.Single(
                bench.World.Ledger.Events, e => e.Type == WorldEventType.OrganizationActed);
            Assert.Contains(Yard, action.Related);
            Assert.Equal(Crew, action.Actor);

            // A yard another body's record calls its own is not this body's inflow, whatever its
            // own list says.
            bench.World.Registry.GetSite(Yard).ControllingOrganizationId = Rivals;
            Assert.Empty(bench.Act(GameTime.FromDays(3)).Planned);
            Assert.Equal(10 + OrganizationOperations.ReserveYield, crew.Wealth);
        }

        /// <summary>
        /// Somebody the body could never reach, and somebody who would not come. Neither is a
        /// recruit, and failing to find one is still not an income.
        /// </summary>
        [Fact]
        public void RecruitmentNeedsSomebodyReachableWillingAndPaidFor()
        {
            Bench bench = Bench.Create();
            Organization crew = bench.Body(Crew);
            crew.SiteIds.Add(Yard);
            crew.Wealth = 2;
            crew.Goals.Add(new OrganizationGoal(OrganizationActivity.ExpandMembership, EntityId.None, 70));
            bench.World.Registry.GetNpc(Stranger).HomeSiteId = Yard;

            // Too poor.
            Assert.Empty(bench.Act(GameTime.FromDays(1)).Planned);
            Assert.Empty(crew.MemberIds.Where(id => id == Stranger));

            // Rich enough, but the stranger will not have them.
            crew.Wealth = 40;
            bench.World.Relationships.Connect(Stranger, Crew, RelationKind.Enemy, -80);
            Assert.Empty(bench.Act(GameTime.FromDays(2)).Planned);
            Assert.Equal(40, crew.Wealth);

            // Reachable, willing, affordable.
            bench.World.Relationships.Find(Stranger, Crew).Kind = RelationKind.Acquaintance;
            bench.World.Relationships.Find(Stranger, Crew).Sentiment = 10;
            OrganizationActivityPass pass = bench.Act(GameTime.FromDays(3));

            Assert.Equal(Stranger, Assert.Single(pass.Planned).Subject);
            Assert.Contains(Stranger, crew.MemberIds);
            Assert.Contains(Crew, bench.World.Registry.GetNpc(Stranger).OrganizationIds);
            Assert.Equal(40 - OrganizationOperations.RecruitCost, crew.Wealth);
        }

        /// <summary>
        /// A purse is spent once. The second end is planned against what the first one left, not
        /// against what the record said when the pass began.
        /// </summary>
        [Fact]
        public void ReservesCannotBeSpentTwiceInOnePass()
        {
            Bench bench = Bench.Create();
            Organization crew = bench.Body(Crew);
            crew.SiteIds.Add(Yard);
            crew.Legitimacy = 0;
            crew.Wealth = OrganizationOperations.RecruitCost + 1;
            bench.World.Registry.GetNpc(Stranger).HomeSiteId = Yard;

            crew.Goals.Add(new OrganizationGoal(OrganizationActivity.ExpandMembership, EntityId.None, 90));
            crew.Goals.Add(new OrganizationGoal(OrganizationActivity.RaidOrganization, Rivals, 80));

            int rivalWealth = bench.Body(Rivals).Wealth;
            OrganizationActivityPass pass = bench.Act(GameTime.FromDays(1));

            // The recruit went through; the blow did not, because the coins are gone. Read against
            // the record as it stood when the pass began, both would have looked affordable.
            Assert.True(OrganizationOperations.RaidCost(crew) > 1);
            Assert.Equal(OrganizationActivity.ExpandMembership, Assert.Single(pass.Planned).Kind);
            Assert.Equal(1, crew.Wealth);
            Assert.Equal(rivalWealth, bench.Body(Rivals).Wealth);
            Assert.Contains(pass.Refusals, why => why.Contains(OrganizationActivity.RaidOrganization));
        }

        /// <summary>
        /// One person does one thing a day, and it does not matter that two bodies both hold them.
        /// A body whose only free hand has already gone somewhere waits.
        /// </summary>
        [Fact]
        public void OnePersonIsSpentOncePerPassHoweverManyBodiesWantThem()
        {
            Bench bench = Bench.Create();
            GameTime now = GameTime.FromDays(1);

            // The fence belongs to both bodies, and is the only person either of them has free.
            Organization rivals = bench.Body(Rivals);
            rivals.MemberIds.Clear();
            rivals.MemberIds.Add(Fence);
            rivals.LeaderId = Fence;
            rivals.SiteIds.Add(Yard);
            bench.World.Registry.GetNpc(Fence).OrganizationIds.Add(Rivals);
            bench.Body(Crew).MemberIds.Remove(Boss);
            bench.Body(Crew).LeaderId = Fence;

            // Both want something today, and neither of them has anybody else.
            bench.Reports(Crew, Fence, bench.Theft, now);
            bench.Interpret(now);
            rivals.Goals.Add(new OrganizationGoal(OrganizationActivity.BuildReserves, Yard, 60));
            Assert.Equal(OrganizationGoalKinds.RecoverHolding, bench.Sole(Crew).Kind);

            int reserves = rivals.Wealth;
            OrganizationActivityPass pass = bench.Act(now);

            Assert.All(pass.Planned, operation => Assert.Equal(Crew, operation.OrganizationId));
            Assert.All(pass.Planned, operation => Assert.Equal(Fence, operation.Agent));
            Assert.Contains(pass.Refusals, why => why.Contains("nobody of its own is free"));
            Assert.Equal(reserves, rivals.Wealth);
        }

        /// <summary>
        /// A body whose leader is gone is not a body that has stopped existing - and a body with
        /// nobody left does nothing rather than acting through somebody who is not there.
        /// </summary>
        [Fact]
        public void ABodyCarriesOnWithoutItsLeaderAndStopsWithoutAnybodyAtAll()
        {
            Bench bench = Bench.Create();
            GameTime now = GameTime.FromDays(1);
            bench.Reports(Crew, Fence, bench.Theft, now);
            bench.Interpret(now);

            bench.World.Registry.GetNpc(Boss).Alive = false;

            Assert.All(bench.Act(now).Planned, operation => Assert.Equal(Fence, operation.Agent));
            Assert.NotEmpty(bench.Activity.LastPass.Planned);

            bench.World.Registry.GetNpc(Fence).Alive = false;
            OrganizationActivityPass stopped = bench.Act(GameTime.FromDays(2));

            Assert.Empty(stopped.Planned);
            Assert.Contains(stopped.Refusals, why => why.Contains("nobody of its own is free"));
        }

        /// <summary>
        /// A report that is wrong moves the body exactly as a true one would, and nothing about
        /// acting on it makes it true. The claim is as false afterwards as it was before, nobody
        /// was taught it, and no second record was minted to agree with it.
        /// </summary>
        [Fact]
        public void AFalseReportCanMotivateAnOperationWithoutBecomingTrue()
        {
            Bench bench = Bench.Create();
            GameTime now = GameTime.FromDays(1);

            Fact invented = bench.Claim(Thief, FactPredicates.Extorted, Daughter, "leaned on her", TruthState.False);
            bench.Reports(Household, Daughter, invented.Id, now);
            bench.Interpret(now);

            int factsBefore = bench.World.Knowledge.Facts.Count();
            string believersBefore = bench.Believers(invented.Id);

            OrganizationOperation operation = bench.Act(now).Planned.FirstOrDefault();

            Assert.NotNull(operation);
            Assert.Equal(OrganizationGoalKinds.ProtectMember, operation.Goal.Kind);

            // It reached execution rather than stopping at a plan: the body directed somebody and
            // the batch put the question to the world on a filing that is simply wrong.
            Assert.NotNull(bench.Activity.LastPass.Arbitration);
            Assert.NotNull(bench.Activity.LastPass.Arbitration.For(operation.Agent));
            Assert.Equal(GoalSourceKind.InstitutionalReading, operation.Goal.Origin.Kind);
            Assert.Equal(invented.Id, operation.Goal.Origin.RecordId);

            // The body moved on it, and what it moved on is as untrue as it was. Nothing minted a
            // second record to agree with the filing, and nobody came to hold it by being acted on.
            Assert.Equal(TruthState.False, bench.World.Knowledge.GetFact(invented.Id).Truth);
            Assert.Equal(factsBefore, bench.World.Knowledge.Facts.Count());
            Assert.Equal(believersBefore, bench.Believers(invented.Id));
            Assert.False(bench.Body(Household).Receipts.Standing(invented.Id).Provable
                         && bench.World.Knowledge.GetFact(invented.Id).Truth == TruthState.True);
        }

        /// <summary>
        /// What a body's operation actually does is ordinary history, so it reaches the people it
        /// touched and the next pass reads it - rather than being an organization-only change
        /// nothing else can see.
        /// </summary>
        [Fact]
        public void WhatABodyDoesBecomesOrdinaryPressureForSomebodyElse()
        {
            Bench bench = Bench.Create();
            Organization crew = bench.Body(Crew);
            crew.Aggression = 80;
            crew.Legitimacy = 100;
            crew.Wealth = 50;
            crew.Goals.Add(new OrganizationGoal(OrganizationActivity.RaidOrganization, Rivals, 90));

            int rivalWealth = bench.Body(Rivals).Wealth;
            bench.Feedback.Attach();
            bench.Act(GameTime.FromDays(1));

            // The rival is poorer and thinks worse of them, on the shared relationship authority.
            Assert.True(bench.Body(Rivals).Wealth < rivalWealth);
            Assert.True(bench.World.Relationships.Find(Rivals, Crew).Sentiment < 0);

            // And the deed is in the ledger as an ordinary event, which is what carries it into
            // the next causal pass rather than leaving it inside the organization record.
            WorldEvent deed = Assert.Single(
                bench.World.Ledger.Events, e => e.Type == WorldEventType.OrganizationActed);
            Assert.Contains(Rivals, deed.Related);
            Assert.True(bench.Feedback.WokenCount > 0);
        }

        /// <summary>
        /// Effort is not evidence, and neither is a condition that quietly came true.
        ///
        /// An end closes here on one thing only: the body's own operation happened and the world
        /// now holds what it wanted. The counter-case in the same test is the one BQa-018 is
        /// careful about - a body whose end is objectively met, whose effort counter is enormous,
        /// and which nothing has done anything about, is still wanting it.
        /// </summary>
        [Fact]
        public void AnEndIsClosedByTheWorldsAnswerToTheBodysOwnOperation()
        {
            Bench bench = Bench.Create();
            GameTime now = GameTime.FromDays(1);
            bench.Reports(Carters, Carter, bench.Shortage, now);
            bench.Reports(Household, Daughter, bench.Harm, now);
            bench.Interpret(now);

            OrganizationGoal supply = bench.Sole(Carters);
            OrganizationGoal protect = bench.Sole(Household);
            protect.Progress = 500;

            Assert.Equal(GoalConditionState.Unmet, supply.Evaluate(bench.World));
            Assert.Equal(GoalConditionState.Met, protect.Evaluate(bench.World));

            OrganizationActivityPass pass = bench.Act(now);
            Assert.True(pass.Committed > 0);

            // The carters sent somebody, they bought the grain, and the shortage is answered.
            Assert.Equal(GoalConditionState.Met, supply.Evaluate(bench.World));
            Assert.Equal(GoalLifecycle.Satisfied, supply.Lifecycle);
            Assert.Equal("condition_met", supply.RetirementCode);
            Assert.Equal(1, pass.Satisfied);

            // The household's end is objectively met and has five hundred units of effort behind
            // it. Nothing of theirs succeeded, so it is still wanted - retiring it is BQa-018's,
            // once the filing it came off has left the body's view.
            Assert.Equal(GoalLifecycle.Active, protect.Lifecycle);
            Assert.Equal(500, protect.Progress);
        }

        /// <summary>
        /// A pass costs what a pass costs. The bodies it could not reach keep the clock they had,
        /// so they are first next time rather than losing a day to a batch that was already full.
        /// </summary>
        [Fact]
        public void APassIsBoundedAndWhatItCouldNotReachIsLateRatherThanLost()
        {
            Bench bench = Bench.Create();
            GameTime now = GameTime.FromDays(1);
            bench.Activity.Budget = new OrganizationActivityBudget { MostDelegationsPerPass = 1 };

            bench.Reports(Carters, Carter, bench.Shortage, now);
            bench.Reports(Crew, Fence, bench.Theft, now);
            bench.Reports(Household, Daughter, bench.Harm, now);
            bench.Interpret(now);

            OrganizationActivityPass first = bench.Act(now);

            Assert.Single(first.Planned);
            Assert.Single(first.Bodies);

            // Only the body that had its turn moved its clock on.
            Assert.Equal(now, bench.Body(first.Bodies[0]).LastActedAt);
            Assert.All(
                new[] { Carters, Crew, Household }.Where(id => id != first.Bodies[0]),
                id => Assert.Equal(GameTime.Zero, bench.Body(id).LastActedAt));

            // And on the next pass they are the ones in front.
            OrganizationActivityPass second = bench.Act(GameTime.FromDays(2));
            Assert.NotEqual(first.Bodies[0], Assert.Single(second.Bodies));
        }

        // -- persistence ---------------------------------------------------------------------------

        /// <summary>
        /// A reload onto the same morning does not run the morning again, and the indivisible
        /// opening a committed operation took is not offered a second time on the day after.
        /// </summary>
        [Fact]
        public void ASaveReloadedCannotRepeatAnOperation()
        {
            Bench bench = Bench.Create();
            GameTime now = GameTime.FromDays(1);
            bench.Reports(Crew, Fence, bench.Theft, now);
            bench.Interpret(now);

            OrganizationActivityPass first = bench.Act(now);
            Assert.NotEmpty(first.Planned);
            int deeds = bench.World.Ledger.Events.Count(e => e.Type == WorldEventType.OrganizationActed);

            Bench reloaded = bench.Reload();

            // The same day again: the body has already had its turn, and the clock says so.
            Assert.Empty(reloaded.Act(now).Planned);
            Assert.Equal(deeds, reloaded.World.Ledger.Events.Count(e => e.Type == WorldEventType.OrganizationActed));

            // And no reservation was written. What a save carries is committed outcomes only.
            Assert.DoesNotContain("\"claims\"", WorldStateSerializer.Save(bench.World));
        }

        /// <summary>
        /// An opening a committed deed already took is dropped where it is visible rather than
        /// offered again the next day - which is the only marker that survives the reload.
        /// </summary>
        [Fact]
        public void AnOpeningAlreadySpentIsNotOfferedAgainAfterAReload()
        {
            Bench bench = Bench.Create();
            GameTime now = GameTime.FromDays(1);
            bench.Reports(Crew, Fence, bench.Theft, now);
            bench.Interpret(now);

            OrganizationOperation operation = bench.Act(now).Planned[0];
            bench.World.ProductionCycle.Spend(
                ActionContest.Exclusive(ContestedThing.Object, Cart).Key, Fence, now, ConsumedOpening.Committed);

            Bench reloaded = bench.Reload();
            reloaded.Interpret(GameTime.FromDays(2));
            OrganizationActivityPass next = reloaded.Act(GameTime.FromDays(2));

            Assert.Equal(SemanticEffects.PossessionTransferred, operation.Effects[0]);
            Assert.Empty(next.Planned);
            Assert.Contains(ActionContest.Exclusive(ContestedThing.Object, Cart).Key, next.OpeningsSkipped);
        }

        /// <summary>
        /// The same world, twice, decides the same way. Nothing in the pass rests on the order a
        /// dictionary handed its bodies over in.
        /// </summary>
        [Fact]
        public void TheSamePassOverTheSameWorldDecidesTheSameWay()
        {
            Assert.Equal(Summarise(), Summarise());
        }

        private static string Summarise()
        {
            Bench bench = Bench.Create();
            GameTime now = GameTime.FromDays(1);
            bench.Reports(Carters, Carter, bench.Shortage, now);
            bench.Reports(Crew, Fence, bench.Theft, now);
            bench.Reports(Household, Daughter, bench.Harm, now);
            bench.Interpret(now);

            OrganizationActivityPass pass = bench.Act(now);
            return string.Join(
                ";",
                pass.Planned.Select(o => o.OrganizationId.Value + "|" + o.Kind + "|" + o.Agent.Value))
                + "//" + string.Join(";", pass.Refusals);
        }

        // -- fixture -----------------------------------------------------------------------------

        private sealed class Bench
        {
            private Bench(NarrativeWorldState world, SandboxVanillaState vanilla)
            {
                World = world;
                Vanilla = vanilla;
                Checks = new VanillaStyleCheckResolver(vanilla);
                Actions = StandardActions.CreateRegistry();
                Consequences = new ConsequenceEngine(world, vanilla);
                Consequences.Attach();
                Feedback = new PressureFeedback(world);
                Activity = new OrganizationActivity(world, vanilla, Checks, Actions);
            }

            public NarrativeWorldState World { get; }

            public SandboxVanillaState Vanilla { get; }

            public VanillaStyleCheckResolver Checks { get; }

            public ActionRegistry Actions { get; }

            public ConsequenceEngine Consequences { get; }

            public PressureFeedback Feedback { get; }

            public OrganizationActivity Activity { get; }

            public EntityId Shortage { get; private set; }

            public EntityId Theft { get; private set; }

            public EntityId Harm { get; private set; }


            public Organization Body(EntityId id) => World.Registry.GetOrganization(id);

            public OrganizationGoal Sole(EntityId bodyId) => Assert.Single(Body(bodyId).Goals);

            public OrganizationActivityPass Act(GameTime now)
            {
                Vanilla.Now = now;
                Activity.Advance(now);
                return Activity.LastPass;
            }

            /// <summary>BQa-018's owners, unchanged, so the ends acted on are ones bodies reached.</summary>
            public void Interpret(GameTime now)
            {
                IReadOnlyList<Development> objective = DevelopmentDetector.Detect(World);
                foreach (EntityId bodyId in new[] { Carters, Crew, Household, Rivals })
                {
                    OrganizationGoalEvolution.Advance(
                        World, bodyId, OrganizationPressureView.Of(World, bodyId, objective), now);
                }
            }

            public InstitutionalReceipt Reports(EntityId bodyId, EntityId memberId, EntityId claimId, GameTime when)
            {
                World.Knowledge.Teach(memberId, claimId, KnowledgeSource.Witnessed, 1.0, GameTime.Zero, canProve: true);
                InstitutionalReceipt receipt = InstitutionalReports.FileMemberReport(
                    World, Body(bodyId), memberId, claimId, when);
                Assert.NotNull(receipt);
                return receipt;
            }

            public Fact Claim(EntityId subject, string predicate, EntityId obj, string wording, TruthState truth)
            {
                Fact claim = new Fact(
                    World.NewId("fact"), subject, predicate, obj, wording, truth, secrecy: 0, originEvent: Occurrence);
                World.Knowledge.AddFact(claim);
                return claim;
            }

            /// <summary>Everybody who holds this claim, in a stable order, as one string.</summary>
            public string Believers(EntityId claimId)
            {
                List<string> held = new List<string>();
                foreach (NarrativeNpc npc in World.Registry.Npcs.Values)
                {
                    foreach (KnowledgeRecord belief in World.Knowledge.BeliefsOf(npc.Id))
                    {
                        if (belief.FactId == claimId)
                        {
                            held.Add(npc.Id.Value);
                        }
                    }
                }

                held.Sort(System.StringComparer.Ordinal);
                return string.Join(",", held);
            }

            /// <summary>Takes the ownership record off the cart, so nothing says whose it is.</summary>
            public void Untrack(EntityId item)
            {
                Fact ownership = World.Knowledge.FindFact(Crew, FactPredicates.Possesses);
                if (ownership != null && ownership.Object == item)
                {
                    ownership.Truth = TruthState.False;
                }
            }

            public Bench Reload()
            {
                NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(World));
                SandboxVanillaState vanilla = Build(reloaded);
                Bench bench = new Bench(reloaded, vanilla)
                {
                    Shortage = Shortage,
                    Theft = Theft,
                    Harm = Harm
                };
                return bench;
            }

            public static Bench Create(ulong seed = 17)
            {
                NarrativeWorldState world = new NarrativeWorldState(seed);
                world.Registry.Add(new NarrativeSite(Town, "Kell's Ford", "village"));
                world.Registry.Add(new NarrativeSite(Yard, "the carters' yard", "yard"));
                world.Registry.Add(new NarrativeNpc(Carter, "Mira") { Occupation = "carter", HomeSiteId = Town });
                world.Registry.Add(new NarrativeNpc(Fence, "Orvo") { HomeSiteId = Town });
                world.Registry.Add(new NarrativeNpc(Boss, "Ketil") { HomeSiteId = Town });
                world.Registry.Add(new NarrativeNpc(Daughter, "Sela") { HomeSiteId = Town });
                world.Registry.Add(new NarrativeNpc(Thief, "Bran") { HomeSiteId = Town });
                world.Registry.Add(new NarrativeNpc(Stranger, "Vela") { HomeSiteId = Town });
                world.Registry.Add(new NarrativeNpc(Player, "You") { HomeSiteId = Town });

                Organization carters = world.Registry.Add(
                    new Organization(Carters, "the carters", "merchant_association") { LeaderId = Carter });
                carters.MemberIds.Add(Carter);

                Organization crew = world.Registry.Add(
                    new Organization(Crew, "the quiet hand", "criminal_crew") { LeaderId = Boss });
                crew.MemberIds.Add(Boss);
                crew.MemberIds.Add(Fence);

                Organization household = world.Registry.Add(
                    new Organization(Household, "the Sela household", "family") { LeaderId = Daughter });
                household.MemberIds.Add(Daughter);

                Organization rivals = world.Registry.Add(
                    new Organization(Rivals, "the blueglass company", "merchant_association") { LeaderId = Thief });
                rivals.MemberIds.Add(Thief);
                rivals.Wealth = 40;

                foreach (NarrativeNpc npc in world.Registry.Npcs.Values)
                {
                    npc.OrganizationIds.AddRange(
                        new[] { Carters, Crew, Household, Rivals }
                            .Where(id => world.Registry.GetOrganization(id).MemberIds.Contains(npc.Id)));
                }

                Bench bench = new Bench(world, Build(world));

                // The cart is the crew's, on the record, which is what makes recovering it theirs
                // to want - and is deliberately not a way of learning that it went missing.
                Fact ownership = new Fact(world.NewId("fact"), Crew, FactPredicates.Possesses, Cart, "a cart");
                world.Knowledge.AddFact(ownership);

                // The shortage presses on somebody in particular. A demand recorded against a
                // zone and nobody else is a want no verb can be pointed at, which is a fixture
                // limitation rather than a fact about shortages.
                Fact need = new Fact(world.NewId("fact"), Stranger, FactPredicates.Needs, EntityId.None, "grain");
                world.Knowledge.AddFact(need);
                world.Demands.AddOrUpdate(
                    Town, LocalDemandCategory.Food, 20, GameTime.Zero, GameTime.FromDays(60), need.Id);

                bench.Shortage = need.Id;
                bench.Theft = bench.Claim(Thief, FactPredicates.Stole, Cart, "a cart", TruthState.True).Id;
                bench.Harm = bench.Claim(Thief, FactPredicates.Extorted, Daughter, "leaned on her", TruthState.True).Id;
                return bench;
            }

            private static SandboxVanillaState Build(NarrativeWorldState world)
            {
                SandboxVanillaState vanilla = new SandboxVanillaState(Player);
                foreach (NarrativeNpc npc in world.Registry.Npcs.Values)
                {
                    vanilla.Define(npc.Id, zone: Town, money: 120);
                }

                vanilla.GiveItem(Thief, new ItemDescriptor(Cart, "a handcart", "cart", 200));
                vanilla.SetCapability(VanillaCapability.SpendMoney, true);
                vanilla.SetCapability(VanillaCapability.DestroyItems, true);
                return vanilla;
            }
        }
    }
}
