using System.Collections.Generic;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Storylets;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-068's two bounds, and what a reader is entitled to know about them.
    ///
    /// The bounds themselves are deliberate and stay: a shortlist holds at most a handful of the
    /// people a role admits, and one pass weighs at most a fixed number of complete groups, because
    /// finding scenes must not cost more than playing them. What was wrong was not the size of
    /// either number but two things the search said about them.
    ///
    /// **It could spend the group bound on branches that could never be scored.** The walk
    /// enumerated every assignment the shortlists allowed, including the ones where an early role
    /// had taken the only person a later required role could have had. Those assignments are not
    /// groups - nothing scores them - but they counted against the bound just the same, and with
    /// enough qualified people they could exhaust it before the walk reached a single complete
    /// cast. The scene then came back "required role ... cannot be cast", which is the sentence
    /// the engine uses when *nobody qualified*, about a role somebody was standing right there to
    /// fill. <see cref="AStoryletWithAQualifiedCastIsCastEvenWhenTheDeadBranchesAreLegion"/> is
    /// that case.
    ///
    /// **It could not say it had stopped early.** `GroupsConsidered` counted assignments walked
    /// rather than groups scored, and the inspector printed the number as "over N qualified
    /// groups" whether the search had exhausted them or given up at the bound. A reader could not
    /// tell "these were all the ways to cast this" from "these were the first hundred and
    /// twenty-eight", and the two support very different conclusions about why this cast won.
    ///
    /// Nothing here asks the search to be exhaustive. Truncation is still allowed; it is now
    /// reported, and it can no longer be mistaken for nobody qualifying.
    /// </summary>
    public class StoryletSearchBoundsTests
    {
        // -- the correctness half ----------------------------------------------------------------

        /// <summary>
        /// Twelve people who saw the theft, one of whom can prove it, and a scene wanting four
        /// witnesses and the prover. Every ordering that opens by handing the prover to a witness
        /// role is a dead branch, and there are hundreds of them - far more than the group bound.
        ///
        /// Before the repair the bound ran out inside them and the scene was reported uncastable.
        /// The cast that exists is unremarkable: four witnesses who are not the prover, and the
        /// prover.
        /// </summary>
        [Fact]
        public void AStoryletWithAQualifiedCastIsCastEvenWhenTheDeadBranchesAreLegion()
        {
            Town town = Town.Of(knowers: 12);

            StoryletOpportunity opportunity = town.Cast(witnessRoles: 4);

            Assert.True(opportunity.IsAvailable, opportunity.RefusalReason);
            Assert.Equal(town.Prover, opportunity.RoleBindings["prover"]);
            Assert.Equal(5, opportunity.RoleBindings.Count);
            Assert.DoesNotContain(town.Prover, WitnessesOf(opportunity, 4));
        }

        /// <summary>
        /// The same scene at every size the bound can bite at, so the repair is a property rather
        /// than one arranged number. From four witnesses upward there is always a cast, and the
        /// prover always holds the role only they qualify for.
        /// </summary>
        [Theory]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        [InlineData(8)]
        [InlineData(12)]
        [InlineData(20)]
        public void EverySizeThatHasACastGetsOne(int knowers)
        {
            StoryletOpportunity opportunity = Town.Of(knowers).Cast(witnessRoles: 4);

            Assert.True(opportunity.IsAvailable, opportunity.RefusalReason);
            Assert.Equal(5, opportunity.RoleBindings.Count);
        }

        /// <summary>
        /// And a scene that genuinely cannot be cast still fails, in the same words, naming the
        /// same role. Skipping branches must not turn "nobody qualifies" into a different answer:
        /// with three witnesses in town, four witness roles take everybody including the prover,
        /// and the prover's own role is the one left empty.
        /// </summary>
        [Fact]
        public void AStoryletNobodyCanFillStillFailsOnTheSameRole()
        {
            StoryletOpportunity tooFewForTheProver = Town.Of(knowers: 3).Cast(witnessRoles: 4);

            Assert.False(tooFewForTheProver.IsAvailable);
            Assert.Equal("required role prover cannot be cast", tooFewForTheProver.RefusalReason);
            Assert.Empty(tooFewForTheProver.RoleBindings);

            StoryletOpportunity tooFewForTheWitnesses = Town.Of(knowers: 2).Cast(witnessRoles: 4);

            Assert.False(tooFewForTheWitnesses.IsAvailable);
            Assert.Equal("required role k3 cannot be cast", tooFewForTheWitnesses.RefusalReason);
        }

        // -- the observability half --------------------------------------------------------------

        /// <summary>
        /// The reported number is the number of groups a score was taken from.
        ///
        /// Five people who know, four witness roles and the prover's role: the prover is barred
        /// from a witness role by the other required role only they can fill, so the four
        /// witnesses are the four non-provers in some order and there are exactly 4! of those. The
        /// dead branches are not among them, which is the point - a group nothing scored is not a
        /// group the winner was preferred to.
        /// </summary>
        [Fact]
        public void TheGroupCountIsGroupsScoredRatherThanBranchesWalked()
        {
            StoryletOpportunity opportunity = Town.Of(knowers: 4).Cast(witnessRoles: 4);

            Assert.True(opportunity.IsAvailable, opportunity.RefusalReason);
            Assert.Equal(24, opportunity.GroupsConsidered);
            Assert.False(opportunity.SearchTruncated);
        }

        /// <summary>
        /// A search that stopped on its bound says so, and a search that ran out of groups says
        /// that instead. Both in the casting report, because that is where the number they qualify
        /// is printed.
        /// </summary>
        [Fact]
        public void TruncationIsReportedAndExhaustionIsReportedAsExhaustion()
        {
            StoryletOpportunity truncated = Town.Of(knowers: 12).Cast(witnessRoles: 4);

            Assert.True(truncated.SearchTruncated);
            Assert.Contains(
                "search: truncated at the group bound",
                NarrativeInspector.DescribeCasting(truncated));

            StoryletOpportunity exhausted = Town.Of(knowers: 4).Cast(witnessRoles: 4);

            Assert.False(exhausted.SearchTruncated);
            Assert.Contains(
                "search: exhausted; every group these shortlists allow was weighed",
                NarrativeInspector.DescribeCasting(exhausted));
        }

        /// <summary>
        /// The other bound, one level down. Thirty people qualify for a role that shortlists a
        /// handful, so the groups weighed were built from a prefix of who qualified - and the
        /// report that claims to say "why these people rather than the others who also qualified"
        /// admits that some of those others were never grouped at all.
        ///
        /// A small town reaches no bound and says nothing, which is what keeps the line meaningful.
        /// </summary>
        [Fact]
        public void ReachingTheCandidateBoundIsReported()
        {
            StoryletOpportunity crowded = Town.Of(knowers: 30).Cast(witnessRoles: 2);

            Assert.True(crowded.CandidateBoundReached);
            Assert.Contains(
                "shortlist: a role reached its candidate bound",
                NarrativeInspector.DescribeCasting(crowded));

            StoryletOpportunity village = Town.Of(knowers: 4).Cast(witnessRoles: 2);

            Assert.False(village.CandidateBoundReached);
            Assert.DoesNotContain("candidate bound", NarrativeInspector.DescribeCasting(village));
        }

        /// <summary>
        /// A truncated pass is still a deterministic pass. The bound is part of the search, not an
        /// accident of it, so the same town cast twice is cast the same way - which is what lets a
        /// scene that went strangely be replayed.
        /// </summary>
        [Fact]
        public void ATruncatedSearchIsStillDeterministic()
        {
            StoryletOpportunity first = Town.Of(knowers: 12).Cast(witnessRoles: 4);
            StoryletOpportunity second = Town.Of(knowers: 12).Cast(witnessRoles: 4);

            Assert.Equal(first.GroupsConsidered, second.GroupsConsidered);
            Assert.Equal(first.SearchTruncated, second.SearchTruncated);
            Assert.Equal(first.Chemistry.Total, second.Chemistry.Total);
            Assert.Equal(
                NarrativeInspector.DescribeCasting(first),
                NarrativeInspector.DescribeCasting(second));
        }

        // -- scaffolding -------------------------------------------------------------------------

        private static List<EntityId> WitnessesOf(StoryletOpportunity opportunity, int roles)
        {
            List<EntityId> cast = new List<EntityId>();
            for (int i = 0; i < roles; i++)
            {
                cast.Add(opportunity.RoleBindings["k" + i]);
            }

            return cast;
        }

        /// <summary>
        /// A theft everybody saw and one person can prove.
        ///
        /// The prover is added to the pool first on purpose: it is the arrangement where the
        /// greedy order hands them to the first role that will take anybody, which is exactly the
        /// case group formation exists to recover from.
        /// </summary>
        private sealed class Town
        {
            private const double Confidence = 0.8;

            private readonly EntityId _zone;

            private Town(NarrativeWorldState world, SandboxVanillaState vanilla, NarrativeThread thread, Fact focus, EntityId zone)
            {
                World = world;
                Vanilla = vanilla;
                Thread = thread;
                Focus = focus;
                _zone = zone;
            }

            public NarrativeWorldState World { get; }

            public SandboxVanillaState Vanilla { get; }

            public NarrativeThread Thread { get; }

            public Fact Focus { get; }

            public EntityId Prover { get; private set; }

            public static Town Of(int knowers)
            {
                NarrativeWorldState world = new NarrativeWorldState(20260903UL);
                EntityId player = EntityId.Parse("npc_player");
                EntityId zone = EntityId.Parse("zone_town");
                SandboxVanillaState vanilla = new SandboxVanillaState(player);
                vanilla.Define(player, zone: zone);
                world.Registry.Add(new NarrativeNpc(player, "You"));

                EntityId thief = EntityId.Parse("npc_thief");
                vanilla.Define(thief, zone: zone);
                world.Registry.Add(new NarrativeNpc(thief, "thief"));

                Fact focus = new Fact(
                    EntityId.Parse("fact_theft"), thief, FactPredicates.Stole,
                    EntityId.Parse("item_ring"), "a silver ring");
                world.Knowledge.AddFact(focus);

                NarrativeThread thread = new NarrativeThread(
                    EntityId.Parse("thread_theft"), "petty_theft", GameTime.Zero);
                thread.SiteIds.Add(zone);
                thread.FactIds.Add(focus.Id);
                thread.ParticipantIds.Add(thief);

                Town town = new Town(world, vanilla, thread, focus, zone);
                town.Prover = town.Add("prover", canProve: true);
                for (int i = 0; i < knowers; i++)
                {
                    town.Add("knower" + i.ToString("00"), canProve: false);
                }

                return town;
            }

            public StoryletOpportunity Cast(int witnessRoles)
            {
                StoryletDefinition definition = new StoryletDefinition("storylet.test.bounds");
                for (int i = 0; i < witnessRoles; i++)
                {
                    definition.RequiredRoles.Add(new StoryletRole("k" + i, StoryletRoleSource.AnyoneWhoKnowsFocus));
                }

                definition.RequiredRoles.Add(new StoryletRole("prover", StoryletRoleSource.AnyoneWhoCanProveFocus));
                definition.Beats.Add(new StoryletBeat("open"));

                return StoryletEngine.Evaluate(
                    definition,
                    new StoryletCastingContext(World, Vanilla, Thread, Focus.Id));
            }

            private EntityId Add(string name, bool canProve)
            {
                EntityId id = EntityId.Parse("npc_" + name);
                Vanilla.Define(id, zone: _zone);
                World.Registry.Add(new NarrativeNpc(id, name));
                World.Knowledge.Teach(
                    id, Focus.Id, KnowledgeSource.Witnessed, Confidence, GameTime.Zero,
                    canProve: canProve,
                    proofs: canProve ? new[] { new ProofLink(ProofKind.WitnessTestimony, id) } : null);
                Thread.ParticipantIds.Add(id);
                return id;
            }
        }
    }
}
