using System.Collections.Generic;
using System.Linq;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Autonomy;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Consequences;
using BrilliantQuesting.Developments;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQa-020. Bodies in the same bounded pass the people are in, and how production comes by a
    /// body at all.
    ///
    /// Two failures this fixture exists to catch. The first is the one the step names: organization
    /// simulation that is stronger in Core and the Lab than in the game, because the Lab authored
    /// the crews and called the pass itself while the host did neither. Every case here therefore
    /// drives <see cref="ProductionCycle"/> and never <see cref="OrganizationActivity"/> directly -
    /// the runner the live host already holds is the only way in, so anything proved here is proved
    /// of the same route the Plugin takes.
    ///
    /// The second is enrollment quietly becoming generosity: a registry row treated as an
    /// institution, one town's watch founded again every morning, or a vanilla office turned into a
    /// BQ purse. Half of these cases assert on what was <em>not</em> raised, admitted or paid.
    ///
    /// Headless throughout. A green pass here says nothing about an Elin hook advancing one.
    /// </summary>
    public class LiveOrganizationAgencyTests
    {
        private static readonly EntityId Town = EntityId.Parse("zone_town");
        private static readonly EntityId Yard = EntityId.Parse("site_yard");
        private static readonly EntityId Carters = EntityId.Parse("org_carters");
        private static readonly EntityId Crew = EntityId.Parse("org_crew");
        private static readonly EntityId Household = EntityId.Parse("org_household");
        private static readonly EntityId Ghost = EntityId.Parse("org_ghost");
        private static readonly EntityId Carter = EntityId.Parse("npc_carter");
        private static readonly EntityId Fence = EntityId.Parse("npc_fence");
        private static readonly EntityId Boss = EntityId.Parse("npc_boss");
        private static readonly EntityId Daughter = EntityId.Parse("npc_daughter");
        private static readonly EntityId Thief = EntityId.Parse("npc_thief");
        private static readonly EntityId Stranger = EntityId.Parse("npc_stranger");
        private static readonly EntityId Warden = EntityId.Parse("npc_warden");
        private static readonly EntityId Sergeant = EntityId.Parse("npc_sergeant");
        private static readonly EntityId Player = EntityId.Parse("npc_player");
        private static readonly EntityId Cart = EntityId.Parse("item_cart");
        private static readonly EntityId Occurrence = EntityId.Parse("evt_market_day");

        // -- the done-when -------------------------------------------------------------------------

        /// <summary>
        /// Three kinds of body notice three different matters and act on them inside one production
        /// pass - the pass a host runs, not one a test drives.
        ///
        /// Each of them is answering something filed with it through a BQa-018 channel, each
        /// arrived at its own end through the charter for its kind, and each operation is carried
        /// by one of that body's own people through the shared batch.
        ///
        /// The people's half of the same pass is bounded to nobody here, and it is the only thing
        /// in this file that is not the runner's own defaults. It is not a way of making the bodies
        /// win: what happens when a person reaches the same opening first is the next case, and the
        /// people's half has its own fixture in <c>ProductionCycleTests</c>. Left running, whichever
        /// of the two owners the day reached first would decide what this case asserted.
        /// </summary>
        [Fact]
        public void ThreeKindsOfBodyNoticeAndActOnPressuresInsideOneProductionPass()
        {
            Bench bench = Bench.Create();
            GameTime now = GameTime.FromDays(1);
            bench.Cycle.Budget.MostActorsPerPass = 0;

            bench.Reports(Carters, Carter, bench.Shortage, now);
            bench.Reports(Crew, Fence, bench.Theft, now);
            bench.Reports(Household, Daughter, bench.Harm, now);

            ProductionCyclePass pass = bench.Day(1);

            Assert.True(pass.Ran);
            Assert.Contains(Carters, pass.Enrollment.Roster);
            Assert.Contains(Crew, pass.Enrollment.Roster);
            Assert.Contains(Household, pass.Enrollment.Roster);

            // Their own ends, formed in this pass, by charter rather than by one shared default.
            Assert.Equal(OrganizationGoalKinds.RelieveShortage, bench.Sole(Carters).Kind);
            Assert.Equal(OrganizationGoalKinds.RecoverHolding, bench.Sole(Crew).Kind);
            Assert.Equal(OrganizationGoalKinds.ProtectMember, bench.Sole(Household).Kind);
            Assert.Equal(
                3,
                pass.InstitutionalGoalChanges
                    .Where(change => change.Kind == GoalChangeKind.Created)
                    .Select(change => change.OrganizationId)
                    .Distinct()
                    .Count());

            // Three bodies, different responses, every one of them one of their own people's deeds
            // settled in the same batch anybody else's intention would be.
            Assert.NotNull(pass.Institutional);
            List<OrganizationOperation> planned = pass.Institutional.Planned;
            Assert.Equal(3, planned.Select(operation => operation.OrganizationId).Distinct().Count());
            Assert.True(planned.Select(operation => operation.Effects[0]).Distinct().Count() >= 3);
            Assert.All(planned, operation =>
            {
                Organization body = bench.Body(operation.OrganizationId);
                Assert.Contains(operation.Agent, body.MemberIds);
                Assert.NotEqual(Player, operation.Agent);
                Assert.True(bench.World.Registry.GetNpc(operation.Agent).Alive);
            });

            Assert.NotNull(pass.Institutional.Arbitration);
            Assert.Equal(planned.Count, pass.Institutional.Arbitration.Decisions.Count);
            Assert.True(pass.Institutional.Committed > 0);
        }

        /// <summary>
        /// One pass, one ledger. A townsman who reaches an opening this morning has taken it before
        /// his guild sends anybody for it, and the body's deed is dropped where the drop is visible
        /// rather than performed a second time.
        ///
        /// This is the ordering the cycle commits to - the people's half first, the bodies' half
        /// after - and the case exists because the alternative is invisible: two owners reaching one
        /// contest through two batches, with nothing saying which of them got there.
        /// </summary>
        [Fact]
        public void APersonWhoGetsThereFirstTakesTheOpeningTheirBodyWouldHaveSentThemFor()
        {
            Bench bench = Bench.Create();
            bench.Reports(Carters, Carter, bench.Shortage, GameTime.FromDays(1));

            ProductionCyclePass pass = bench.Day(1);

            string opening = Assert.Single(pass.OpeningsSpent);
            Assert.Equal(OrganizationGoalKinds.RelieveShortage, bench.Sole(Carters).Kind);
            Assert.Contains(opening, pass.Institutional.OpeningsSkipped);
            Assert.DoesNotContain(
                pass.Institutional.Planned, operation => operation.OrganizationId == Carters);
            Assert.Equal(0, bench.Sole(Carters).Progress);
        }

        /// <summary>
        /// The same pass, run by the same object. The cycle calls BQa-019's owner rather than
        /// holding a copy of its scheduling, which is what makes a headless run and a hosted one
        /// the same run.
        /// </summary>
        [Fact]
        public void TheCycleCallsTheTestedOwnerRatherThanASecondOne()
        {
            Bench bench = Bench.Create();
            bench.Reports(Crew, Fence, bench.Theft, GameTime.FromDays(1));

            ProductionCyclePass pass = bench.Day(1);

            Assert.Same(bench.Cycle.Organizations.LastPass, pass.Institutional);
            Assert.Equal(pass.Day, pass.Institutional.Day);
        }

        /// <summary>
        /// The BQa-018 line, asserted from inside the production pass. The theft is on the record,
        /// the detector reads it, one of the crew even holds it - and until somebody files it the
        /// body wants nothing and does nothing about it.
        /// </summary>
        [Fact]
        public void ABodyNobodyToldNoticesNothingHoweverMuchTheWorldKnows()
        {
            Bench bench = Bench.Create();
            bench.World.Knowledge.Teach(Fence, bench.Theft, KnowledgeSource.Witnessed, 1.0, GameTime.Zero, canProve: true);

            ProductionCyclePass pass = bench.Day(1);

            Assert.Contains(Crew, pass.Enrollment.Roster);
            Assert.Empty(bench.Body(Crew).Goals);
            Assert.Empty(pass.InstitutionalGoalChanges.Where(change => change.OrganizationId == Crew));
            Assert.DoesNotContain(
                pass.Institutional.Planned, operation => operation.OrganizationId == Crew);

            // And the filing is the whole difference: the same belief, told to the body, is noticed.
            bench.Reports(Crew, Fence, bench.Theft, GameTime.FromDays(2));
            ProductionCyclePass told = bench.Day(2);

            Assert.Equal(OrganizationGoalKinds.RecoverHolding, bench.Sole(Crew).Kind);
            Assert.Contains(told.Institutional.Planned, operation => operation.OrganizationId == Crew);
        }

        /// <summary>
        /// What a body does becomes ordinary history, reaches the ordinary consequence listener and
        /// is in the next pass's work set - no organization ledger, no organization-only rule.
        /// </summary>
        [Fact]
        public void WhatABodyDoesBecomesOrdinaryHistoryAndPressesOnTheNextPass()
        {
            Bench bench = Bench.Create();
            bench.Cycle.Budget.MostActorsPerPass = 0;
            bench.Reports(Carters, Carter, bench.Shortage, GameTime.FromDays(1));
            int before = bench.World.Ledger.Count;

            ProductionCyclePass pass = bench.Day(1);

            Assert.True(pass.Institutional.Committed > 0);
            Assert.True(bench.World.Ledger.Count > before);
            Assert.Contains(
                bench.World.Ledger.Events,
                recorded => recorded.Type == WorldEventType.OrganizationActed);

            // The cycle's own collector heard it, exactly as it hears a person's attempt, so the
            // body and what its deed named are read again tomorrow rather than needing a nudge.
            Assert.True(bench.Cycle.Feedback.PendingCount + bench.Cycle.Feedback.WokenCount > 0);

            ProductionCyclePass next = bench.Day(2);
            Assert.True(next.Ran);
            Assert.True(next.Scope.Count > 0 || next.Evaluated.Count > 0);
        }

        // -- enrollment: what production may act on ------------------------------------------------

        /// <summary>
        /// A registry row with nobody on it and no ground is a name, not an institution, and is
        /// refused by name however loudly it wants something.
        /// </summary>
        [Fact]
        public void ARowWithNobodyAndNothingIsNotAnInstitution()
        {
            Bench bench = Bench.Create();
            Organization ghost = bench.Body(Ghost);
            ghost.Wealth = 30;
            ghost.Goals.Add(new OrganizationGoal(OrganizationActivity.BuildReserves, Yard, 90));

            ProductionCyclePass pass = bench.Day(1);

            Assert.DoesNotContain(Ghost, pass.Enrollment.Roster);
            Assert.Contains(pass.Enrollment.Refusals, why => why.StartsWith(Ghost.Value));
            Assert.DoesNotContain(pass.Institutional?.Bodies ?? new EntityId[0], body => body == Ghost);
            Assert.Equal(30, ghost.Wealth);
            Assert.Equal(0, ghost.Goals[0].Progress);
        }

        /// <summary>
        /// The route that gives a real game any bodies at all: people the game says hold an office
        /// in one place are that place's body, raised once and refreshed thereafter.
        /// </summary>
        [Fact]
        public void ProductionRaisesOneBodyFromObservedOfficesAndNeverASecond()
        {
            Bench bench = Bench.Create();
            bench.Post(Warden);
            bench.Post(Sergeant);

            ProductionCyclePass first = bench.Day(1);

            EntityId raised = Assert.Single(first.Enrollment.Raised);
            Organization watch = bench.Body(raised);
            Assert.Equal(OrganizationEnrollment.WatchType, watch.Type);
            Assert.Equal(OrganizationSource.ObservedMembership, watch.Source);
            Assert.Equal(OrganizationEnrollment.WatchType + "@" + Town.Value, watch.ExternalRef);
            Assert.Equal(new[] { Sergeant, Warden }.OrderBy(id => id.Value), watch.MemberIds.OrderBy(id => id.Value));
            Assert.Contains(raised, bench.World.Registry.GetNpc(Warden).OrganizationIds);

            // A vanilla office is not a BQ treasury, and the town is not ground the watch keeps.
            Assert.Equal(0, watch.Wealth);
            Assert.Empty(watch.SiteIds);
            Assert.True(watch.LeaderId.IsNone);

            // Deduplicated across passes and across a save, which is the difference between an
            // enrollment owner and a founder that runs every morning.
            Assert.Empty(bench.Day(2).Enrollment.Raised);
            Assert.Empty(bench.Reload(2).Day(3).Enrollment.Raised);
            Assert.Single(bench.World.Registry.Organizations.Values.Where(
                body => body.Source == OrganizationSource.ObservedMembership));
        }

        /// <summary>
        /// The roll is the observation rather than a record of it: somebody who no longer holds the
        /// office is off it, on both sides.
        /// </summary>
        [Fact]
        public void ARaisedBodyDropsSomebodyWhoNoLongerHoldsTheOffice()
        {
            Bench bench = Bench.Create();
            bench.Post(Warden);
            bench.Post(Sergeant);

            EntityId raised = Assert.Single(bench.Day(1).Enrollment.Raised);
            bench.World.Registry.GetNpc(Sergeant).Roles.Remove(AuthorityPolicy.GuardRole);

            bench.Day(2);

            Organization watch = bench.Body(raised);
            Assert.Equal(new[] { Warden }, watch.MemberIds);
            Assert.DoesNotContain(raised, bench.World.Registry.GetNpc(Sergeant).OrganizationIds);
        }

        /// <summary>
        /// An office nobody holds any more. The record stays, because history was written under it,
        /// and the body stops being one production acts on.
        /// </summary>
        [Fact]
        public void ARaisedBodyWhoseOfficeWentAwayStopsBeingAnInstitution()
        {
            Bench bench = Bench.Create();
            bench.Post(Warden);
            EntityId raised = Assert.Single(bench.Day(1).Enrollment.Raised);

            bench.World.Registry.GetNpc(Warden).Roles.Remove(AuthorityPolicy.GuardRole);
            ProductionCyclePass pass = bench.Day(2);

            Assert.NotNull(bench.Body(raised));
            Assert.Empty(bench.Body(raised).MemberIds);
            Assert.DoesNotContain(raised, pass.Enrollment.Roster);
            Assert.Contains(pass.Enrollment.Refusals, why => why.StartsWith(raised.Value));
            Assert.Empty(pass.Enrollment.Raised);
        }

        /// <summary>
        /// A body raised from an office has been told nothing, and a town full of trouble does not
        /// change that. This is the honest state of a newly enrolled institution, not a gap.
        /// </summary>
        [Fact]
        public void ARaisedBodyHasBeenToldNothingAndSoWantsNothing()
        {
            Bench bench = Bench.Create();
            bench.Post(Warden);

            ProductionCyclePass pass = bench.Day(1);
            Organization watch = bench.Body(Assert.Single(pass.Enrollment.Raised));

            Assert.Empty(watch.Receipts);
            Assert.Empty(watch.Goals);
            Assert.DoesNotContain(
                pass.Institutional.Planned, operation => operation.OrganizationId == watch.Id);

            // Told through one of its own about one of its own, it answers: the watch's charter
            // reaches harm to a member, and being on the roll is what makes the claim its business.
            EntityId leaned = bench.Claim(Thief, FactPredicates.Extorted, Warden, "leaned on him");
            Assert.NotNull(bench.Reports(watch.Id, Warden, leaned, GameTime.FromDays(2)));

            bench.Day(2);
            Assert.Equal(OrganizationGoalKinds.ProtectMember, Assert.Single(watch.Goals).Kind);
        }

        // -- the pass's own rules still hold for bodies ----------------------------------------------

        /// <summary>
        /// One interval, one institutional pass. A reload onto the same morning does not let the
        /// bodies act again, because the gate is the save's marker rather than a host's cursor.
        /// </summary>
        [Fact]
        public void AReloadOntoTheSameDayDoesNotLetTheBodiesActTwice()
        {
            Bench bench = Bench.Create();
            bench.Cycle.Budget.MostActorsPerPass = 0;
            bench.Reports(Carters, Carter, bench.Shortage, GameTime.FromDays(1));

            ProductionCyclePass first = bench.Day(1);
            Assert.True(first.Institutional.Committed > 0);
            int progress = bench.Sole(Carters).Progress;

            Bench reloaded = bench.Reload(1);
            ProductionCyclePass again = reloaded.Day(1);

            Assert.False(again.Ran);
            Assert.Null(again.Enrollment);
            Assert.Equal(progress, reloaded.Sole(Carters).Progress);
        }

        /// <summary>
        /// The same inputs, twice, reach the same bodies in the same order and plan the same
        /// operations - enrollment included, which is why its ordering is ordinal rather than
        /// whatever the registry enumerates.
        /// </summary>
        [Fact]
        public void RepeatedPassesAreDeterministic()
        {
            Assert.Equal(Run(), Run());

            string Run()
            {
                Bench bench = Bench.Create();
                bench.Post(Warden);
                bench.Reports(Carters, Carter, bench.Shortage, GameTime.FromDays(1));
                bench.Reports(Crew, Fence, bench.Theft, GameTime.FromDays(1));

                ProductionCyclePass pass = bench.Day(1);
                return string.Join(",", pass.Enrollment.Roster.Select(id => id.Value))
                       + "//" + string.Join(";", pass.Enrollment.Refusals)
                       + "//" + string.Join(
                           ";",
                           pass.Institutional.Planned.Select(
                               operation => operation.OrganizationId.Value + "|" + operation.Kind));
            }
        }

        /// <summary>
        /// The enrollment record is persisted, and a save written before it existed comes back as
        /// what it is: bodies somebody established, whose membership the enrollment pass may not
        /// rewrite.
        /// </summary>
        [Fact]
        public void EnrollmentProvenanceSurvivesASaveAndAnOldSaveReadsAsEstablished()
        {
            Bench bench = Bench.Create();
            bench.Post(Warden);
            EntityId raised = Assert.Single(bench.Day(1).Enrollment.Raised);

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(bench.World));
            Organization watch = reloaded.Registry.GetOrganization(raised);
            Assert.Equal(OrganizationSource.ObservedMembership, watch.Source);
            Assert.Equal(OrganizationEnrollment.WatchType + "@" + Town.Value, watch.ExternalRef);

            Organization carters = reloaded.Registry.GetOrganization(Carters);
            Assert.Equal(OrganizationSource.Established, carters.Source);
            Assert.Equal(string.Empty, carters.ExternalRef);

            // The old-save default, read off a document that never carried the fields.
            NarrativeWorldState old = WorldStateSerializer.Load(Without(
                WorldStateSerializer.Save(bench.World), "source", "externalRef"));
            Assert.All(
                old.Registry.Organizations.Values,
                body => Assert.Equal(OrganizationSource.Established, body.Source));
        }

        /// <summary>Rewrites a save's organizations without the named members, as an older one was.</summary>
        private static string Without(string save, params string[] members)
        {
            JsonValue root = JsonValue.Parse(save);
            JsonValue rewritten = JsonValue.Array();
            foreach (JsonValue body in root.GetArray("organizations"))
            {
                JsonValue kept = JsonValue.Object();
                foreach (KeyValuePair<string, JsonValue> member in body.Members)
                {
                    if (!members.Contains(member.Key))
                    {
                        kept.Set(member.Key, member.Value);
                    }
                }

                rewritten.Add(kept);
            }

            return root.Set("organizations", rewritten).ToJson();
        }

        // -- fixture -----------------------------------------------------------------------------

        private sealed class Bench
        {
            private Bench(NarrativeWorldState world, SandboxVanillaState vanilla)
            {
                World = world;
                Vanilla = vanilla;
                Consequences = new ConsequenceEngine(world, vanilla);
                Consequences.Attach();
                Cycle = new ProductionCycle(
                    world, vanilla, new VanillaStyleCheckResolver(vanilla), StandardActions.CreateRegistry());
                Cycle.Attach();
            }

            public NarrativeWorldState World { get; }

            public SandboxVanillaState Vanilla { get; }

            public ConsequenceEngine Consequences { get; }

            public ProductionCycle Cycle { get; }

            public EntityId Shortage { get; private set; }

            public EntityId Theft { get; private set; }

            public EntityId Harm { get; private set; }

            public Organization Body(EntityId id) => World.Registry.GetOrganization(id);

            public OrganizationGoal Sole(EntityId bodyId) => Assert.Single(Body(bodyId).Goals);

            public ProductionCyclePass Day(long day)
            {
                Vanilla.Now = GameTime.FromDays(day);
                return Cycle.Run(Vanilla.Now);
            }

            /// <summary>
            /// Gives somebody the one standing a live character can acquire: the office the
            /// authority policy reads off an observed institutional facet, and nothing else.
            /// </summary>
            public void Post(EntityId who)
            {
                World.Registry.GetNpc(who).Roles.Add(AuthorityPolicy.GuardRole);
            }

            /// <summary>BQa-018's member channel, unchanged, so what a body knows it was told.</summary>
            public InstitutionalReceipt Reports(EntityId bodyId, EntityId memberId, EntityId claimId, GameTime when)
            {
                World.Knowledge.Teach(memberId, claimId, KnowledgeSource.Witnessed, 1.0, GameTime.Zero, canProve: true);
                InstitutionalReceipt receipt = InstitutionalReports.FileMemberReport(
                    World, Body(bodyId), memberId, claimId, when);
                Assert.NotNull(receipt);
                return receipt;
            }

            public Bench Reload(long afterDay)
            {
                NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(World));
                Bench bench = new Bench(reloaded, Build(reloaded))
                {
                    Shortage = Shortage,
                    Theft = Theft,
                    Harm = Harm
                };
                bench.Vanilla.Now = GameTime.FromDays(afterDay);
                return bench;
            }

            public static Bench Create(ulong seed = 41)
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
                world.Registry.Add(new NarrativeNpc(Warden, "Hald") { HomeSiteId = Town });
                world.Registry.Add(new NarrativeNpc(Sergeant, "Eik") { HomeSiteId = Town });
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

                // A name in the registry and nothing else. Never enrolled, and here to prove it.
                world.Registry.Add(new Organization(Ghost, "the blueglass company", "merchant_association"));

                foreach (NarrativeNpc npc in world.Registry.Npcs.Values)
                {
                    foreach (EntityId bodyId in new[] { Carters, Crew, Household })
                    {
                        if (world.Registry.GetOrganization(bodyId).MemberIds.Contains(npc.Id))
                        {
                            npc.OrganizationIds.Add(bodyId);
                        }
                    }
                }

                Bench bench = new Bench(world, Build(world));

                // The cart is the crew's on the record, which is what makes recovering it theirs to
                // want - and is deliberately not a way of learning that it went missing.
                world.Knowledge.AddFact(new Fact(world.NewId("fact"), Crew, FactPredicates.Possesses, Cart, "a cart"));

                Fact need = new Fact(world.NewId("fact"), Stranger, FactPredicates.Needs, EntityId.None, "grain");
                world.Knowledge.AddFact(need);
                world.Demands.AddOrUpdate(
                    Town, LocalDemandCategory.Food, 80, GameTime.Zero, GameTime.FromDays(60), need.Id);

                bench.Shortage = need.Id;
                bench.Theft = bench.Claim(world, Thief, FactPredicates.Stole, Cart, "a cart");
                bench.Harm = bench.Claim(world, Thief, FactPredicates.Extorted, Daughter, "leaned on her");
                return bench;
            }

            public EntityId Claim(EntityId subject, string predicate, EntityId obj, string wording)
            {
                return Claim(World, subject, predicate, obj, wording);
            }

            private EntityId Claim(
                NarrativeWorldState world, EntityId subject, string predicate, EntityId obj, string wording)
            {
                Fact claim = new Fact(
                    world.NewId("fact"), subject, predicate, obj, wording, TruthState.True,
                    secrecy: 0, originEvent: Occurrence);
                if (!obj.IsNone)
                {
                    claim.EvidenceIds.Add(obj);
                }

                world.Knowledge.AddFact(claim);
                return claim.Id;
            }

            private static SandboxVanillaState Build(NarrativeWorldState world)
            {
                SandboxVanillaState vanilla = new SandboxVanillaState(Player);
                foreach (NarrativeNpc npc in world.Registry.Npcs.Values)
                {
                    // Only the carters' own man can cover a shortage out of pocket. Everybody else
                    // in this town is broke, so the opening the body reaches for is not one a
                    // passer-by takes first - which keeps the case about the body rather than
                    // about who happened to be up earlier.
                    vanilla.Define(npc.Id, zone: Town, money: npc.Id == Carter ? 300 : 0);
                }

                vanilla.GiveItem(Thief, new ItemDescriptor(Cart, "a handcart", "cart", 200));
                vanilla.SetCapability(VanillaCapability.SpendMoney, true);
                vanilla.SetCapability(VanillaCapability.DestroyItems, true);
                return vanilla;
            }
        }
    }
}
