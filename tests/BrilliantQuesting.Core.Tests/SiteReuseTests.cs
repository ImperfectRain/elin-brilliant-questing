using System.Collections.Generic;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-088. Before anything is generated: can somewhere that already exists host this matter?
    ///
    /// The step's done-when is a behaviour and an explanation, so these are mostly behaviours -
    /// a matter that needed a place ends up in one the world already had, nothing new appears on
    /// the map, and the trace says which places were passed over and why. The rest hold the edges
    /// that would make reuse a bad answer: a place another live matter is still using, a place
    /// whose contents are reachable when the matter turns on getting past somebody, and a reuse
    /// that quietly re-staged the plan's contents over a place's own history.
    /// </summary>
    public class SiteReuseTests
    {
        /// <summary>
        /// The done-when. A matter needs a mine; the world already has one; nothing is generated,
        /// the matter is bound to the place that was already there, and the inspector says so.
        /// </summary>
        [Fact]
        public void AMatterNeedingAPlaceReusesOneTheWorldAlreadyHad()
        {
            Fixture fixture = Fixture.Build();
            int placesBefore = fixture.World.Registry.Sites.Count;

            SitePlan plan = fixture.PlanFor(fixture.SecondMatter, "mine", restricted: true);

            // The explanation is read where the decision is made, before the matter has been bound
            // to anything - afterwards the honest answer is the cheaper one, that it already uses
            // the place.
            string trace = NarrativeInspector.DescribeSiteChoice(fixture.World, plan);
            SiteProvision provision = fixture.Provide(plan);

            Assert.True(provision.Reused);
            Assert.False(provision.Generated);
            Assert.Equal(fixture.MineId, provision.Site.Id);
            Assert.Equal(placesBefore, fixture.World.Registry.Sites.Count);
            Assert.Contains(fixture.MineId, fixture.SecondMatter.SiteIds);

            Assert.Contains("reuse", trace);
            Assert.Contains(provision.Site.Name, trace);
            Assert.Contains(provision.Choice.Reason, trace);
            Assert.Contains(fixture.World.Registry.GetSite(fixture.MarketId).Name, trace);
        }

        /// <summary>
        /// Once a matter is using a place, the reuse answer for the same need is that it already
        /// has one. Not a special case: it is the cheapest tier doing its job, and it is what stops
        /// a second pass over the same settlement making a second place for a matter that has one.
        /// </summary>
        [Fact]
        public void AskingAgainAfterAMatterHasBeenPlacedAnswersWithThePlaceItHas()
        {
            Fixture fixture = Fixture.Build();
            SitePlan plan = fixture.PlanFor(fixture.SecondMatter, "mine", restricted: true);
            fixture.Provide(plan);

            SiteChoice again = SiteReuse.Choose(
                fixture.World, fixture.PlanFor(fixture.SecondMatter, "mine", restricted: true));

            Assert.True(again.Reused);
            Assert.Equal(SiteReuseTier.Bound, again.Tier);
            Assert.Equal(fixture.MineId, again.Site.Id);
        }

        /// <summary>
        /// Generating is the last answer, not the first. When nothing that exists is the kind of
        /// place the matter needs, genesis runs - and the choice can name a reason against every
        /// place it looked at, which is what makes a wrong refusal findable.
        /// </summary>
        [Fact]
        public void GeneratingHappensOnlyWhenNothingExistingCanHostTheMatter()
        {
            Fixture fixture = Fixture.Build();

            SitePlan plan = fixture.PlanFor(fixture.SecondMatter, "smuggler_cache", restricted: true);
            SiteProvision provision = fixture.Provide(plan);

            Assert.False(provision.Reused);
            Assert.True(provision.Generated);
            Assert.Equal(plan.SiteId, provision.Site.Id);

            Assert.NotEmpty(provision.Choice.Considered);
            for (int i = 0; i < provision.Choice.Considered.Count; i++)
            {
                SiteCandidateReading reading = provision.Choice.Considered[i];
                Assert.False(reading.CanHost);
                Assert.NotEmpty(reading.Refusals);
            }
        }

        /// <summary>
        /// A place the world already had beats one this mod made, when both could host the matter.
        /// Reusing the town's own mine adds nothing to the map; reusing a generated cache leaves a
        /// generated cache standing where a player already found one.
        /// </summary>
        [Fact]
        public void APlaceTheWorldAlreadyHadOutranksOneThisModMade()
        {
            Fixture fixture = Fixture.Build();
            fixture.EstablishOldCache("mine");
            fixture.CloseTheOldMatter();

            SiteChoice choice = SiteReuse.Choose(
                fixture.World, fixture.PlanFor(fixture.SecondMatter, "mine", restricted: true));

            Assert.True(choice.Reused);
            Assert.Equal(SiteReuseTier.WorldsOwn, choice.Tier);
            Assert.Equal(fixture.MineId, choice.Site.Id);
        }

        /// <summary>A place this matter already uses beats everything, because using it changes
        /// nothing about the world at all.</summary>
        [Fact]
        public void APlaceTheMatterAlreadyUsesOutranksEveryOtherPlace()
        {
            Fixture fixture = Fixture.Build();
            NarrativeSite second = fixture.AddWorldsOwnSite("the flooded adit", "mine", restricted: true);
            fixture.SecondMatter.SiteIds.Add(second.Id);

            SiteChoice choice = SiteReuse.Choose(
                fixture.World, fixture.PlanFor(fixture.SecondMatter, "mine", restricted: true));

            Assert.Equal(SiteReuseTier.Bound, choice.Tier);
            Assert.Equal(second.Id, choice.Site.Id);
        }

        /// <summary>
        /// A place this mod made exists because one matter needed it. While that matter can still
        /// surface the place is spoken for, and a second matter gets its own; once the matter is
        /// over the same place is the cheapest answer there is.
        /// </summary>
        [Fact]
        public void AGeneratedPlaceIsRecycledOnlyOnceItsOwnMatterCanNoLongerSurface()
        {
            Fixture fixture = Fixture.Build();
            EntityId cache = fixture.EstablishOldCache("smuggler_cache");

            SitePlan plan = fixture.PlanFor(fixture.SecondMatter, "smuggler_cache", restricted: true);
            Assert.False(SiteReuse.Choose(fixture.World, plan).Reused);
            Assert.True(SiteReuse.SpokenFor(fixture.World, cache, EntityId.None));

            fixture.CloseTheOldMatter();

            SiteChoice after = SiteReuse.Choose(fixture.World, plan);
            Assert.True(after.Reused);
            Assert.Equal(SiteReuseTier.Generated, after.Tier);
            Assert.Equal(cache, after.Site.Id);
        }

        /// <summary>
        /// A dormant matter can wake up, so its place is still its own. Only a matter that can
        /// never surface again - resolved, inherited, quarantined - releases one.
        /// </summary>
        [Fact]
        public void ADormantMattersPlaceIsStillSpokenFor()
        {
            Fixture fixture = Fixture.Build();
            EntityId cache = fixture.EstablishOldCache("smuggler_cache");
            fixture.FirstMatter.State = ThreadState.Dormant;

            Assert.True(SiteReuse.SpokenFor(fixture.World, cache, EntityId.None));
            Assert.False(SiteReuse.Choose(
                fixture.World, fixture.PlanFor(fixture.SecondMatter, "smuggler_cache", restricted: true)).Reused);
        }

        /// <summary>
        /// The world's own places are shared infrastructure. A town's mine is not a claim, so a
        /// second live matter may happen in the same place a first one is already using.
        /// </summary>
        [Fact]
        public void APlaceTheWorldAlreadyHadIsNotRefusedForBeingBusy()
        {
            Fixture fixture = Fixture.Build();
            fixture.FirstMatter.SiteIds.Add(fixture.MineId);

            SiteChoice choice = SiteReuse.Choose(
                fixture.World, fixture.PlanFor(fixture.SecondMatter, "mine", restricted: true));

            Assert.True(choice.Reused);
            Assert.Equal(fixture.MineId, choice.Site.Id);
            Assert.True(fixture.FirstMatter.IsLive);
        }

        /// <summary>
        /// A matter that turns on getting past somebody cannot happen where there is nobody to get
        /// past, and a matter that never planned for a lock is not handed one.
        /// </summary>
        [Fact]
        public void ReachHasToMatchInBothDirections()
        {
            Fixture fixture = Fixture.Build();

            SiteChoice needsALock = SiteReuse.Choose(
                fixture.World, fixture.PlanFor(fixture.SecondMatter, "market", restricted: true));
            SiteChoice needsNone = SiteReuse.Choose(
                fixture.World, fixture.PlanFor(fixture.SecondMatter, "mine", restricted: false));

            Assert.False(needsALock.Reused);
            Assert.False(needsNone.Reused);
            Assert.NotEqual(Refusals(needsALock, fixture.MarketId), Refusals(needsNone, fixture.MineId));
        }

        /// <summary>
        /// Reuse is a binding and nothing else: no event, nobody staged, and none of the plan's
        /// cargo put into a place that already has its own. A reuse that staged the plan's contents
        /// would be genesis under another name, and would overwrite what made the place worth
        /// reusing.
        /// </summary>
        [Fact]
        public void ReusingAPlaceStagesNothingAndWritesNoHistory()
        {
            Fixture fixture = Fixture.Build();
            NarrativeSite mine = fixture.World.Registry.GetSite(fixture.MineId);
            List<EntityId> occupantsBefore = new List<EntityId>(mine.OccupantIds);
            List<EntityId> cargoBefore = new List<EntityId>(mine.ImportantObjectIds);
            int historyBefore = fixture.World.Ledger.Count;

            SitePlan plan = fixture.PlanFor(fixture.SecondMatter, "mine", restricted: true);
            SiteProvision provision = fixture.Provide(plan);

            Assert.True(provision.Reused);
            Assert.Equal(occupantsBefore, mine.OccupantIds);
            Assert.Equal(cargoBefore, mine.ImportantObjectIds);
            Assert.Equal(historyBefore, fixture.World.Ledger.Count);
            Assert.False(mine.Established);

            // Nobody from the plan was pushed into the place either.
            for (int i = 0; i < plan.Occupants.Count; i++)
            {
                Assert.DoesNotContain(plan.Occupants[i].Npc.Id, mine.OccupantIds);
            }
        }

        /// <summary>
        /// The same world and the same need choose the same place after a save and a load. The
        /// registry is a dictionary, and a policy whose answer depended on how it happened to be
        /// filled would give a player a different place every time they reloaded.
        /// </summary>
        [Fact]
        public void TheSameNeedChoosesTheSamePlaceAcrossASaveAndLoad()
        {
            Fixture fixture = Fixture.Build();
            fixture.AddWorldsOwnSite("the drowned shaft", "mine", restricted: true);
            fixture.AddWorldsOwnSite("the upper gallery", "mine", restricted: true);

            SitePlan plan = fixture.PlanFor(fixture.SecondMatter, "mine", restricted: true);
            SiteChoice before = SiteReuse.Choose(fixture.World, plan);

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(fixture.World));
            SiteChoice after = SiteReuse.Choose(reloaded, plan);

            Assert.True(before.Reused);
            Assert.Equal(before.Site.Id, after.Site.Id);
            Assert.Equal(before.Tier, after.Tier);
        }

        /// <summary>A plan naming no kind of place is not fussy about kind - it still has to reach
        /// what the place keeps the same way.</summary>
        [Fact]
        public void APlanNamingNoKindWillTakeAnyKind()
        {
            Fixture fixture = Fixture.Build();

            SiteChoice choice = SiteReuse.Choose(
                fixture.World, fixture.PlanFor(fixture.SecondMatter, string.Empty, restricted: true));

            Assert.True(choice.Reused);
            Assert.True(choice.Site.Restricted);
        }

        /// <summary>
        /// A matter that does not exist gets no place. Genesis already refuses a plan with no
        /// thread behind it, and a reuse that bound one anyway would put a place into play on terms
        /// genesis would have rejected.
        /// </summary>
        [Fact]
        public void APlanWithNoMatterBehindItIsPlacedNowhere()
        {
            Fixture fixture = Fixture.Build();
            SitePlan orphan = fixture.PlanFor(fixture.SecondMatter, "mine", restricted: true);
            fixture.World.Threads.Remove(fixture.SecondMatter);

            SiteProvision provision = fixture.Provide(orphan);

            Assert.False(provision.Choice.Reused);
            Assert.False(provision.Placed);
            Assert.Equal(SiteGenesisOutcome.PlanRejected, provision.Genesis.Outcome);
            Assert.NotEmpty(provision.Genesis.Reasons);
        }

        private static IReadOnlyList<string> Refusals(SiteChoice choice, EntityId siteId)
        {
            for (int i = 0; i < choice.Considered.Count; i++)
            {
                if (choice.Considered[i].SiteId == siteId)
                {
                    return choice.Considered[i].Refusals;
                }
            }

            return new string[0];
        }

        // -- fixture ------------------------------------------------------------------------

        /// <summary>
        /// A town that already has places in it, and two matters. Nothing here is an archetype:
        /// the world's own places are written down the way every situation writes them down, which
        /// is what a reuse policy has to work against.
        /// </summary>
        private sealed class Fixture
        {
            private Fixture(NarrativeWorldState world, SandboxVanillaState vanilla)
            {
                World = world;
                Vanilla = vanilla;
                Stager = new SandboxStager(vanilla);
            }

            internal NarrativeWorldState World { get; }

            internal SandboxVanillaState Vanilla { get; }

            internal SandboxStager Stager { get; }

            internal NarrativeThread FirstMatter { get; private set; }

            internal NarrativeThread SecondMatter { get; private set; }

            internal EntityId MineId { get; private set; }

            internal EntityId MarketId { get; private set; }

            internal static Fixture Build()
            {
                NarrativeWorldState world = new NarrativeWorldState(88);
                Fixture fixture = new Fixture(world, new SandboxVanillaState(EntityId.Parse("npc_player")));

                fixture.MarketId = fixture.AddWorldsOwnSite("the market row", "market", restricted: false).Id;
                fixture.MineId = fixture.AddWorldsOwnSite("the old garnet mine", "mine", restricted: true).Id;
                fixture.FirstMatter = fixture.AddMatter("blocked_passage");
                fixture.SecondMatter = fixture.AddMatter("stolen_cargo");
                return fixture;
            }

            internal NarrativeSite AddWorldsOwnSite(string name, string siteType, bool restricted)
            {
                return World.Registry.Add(new NarrativeSite(World.NewId("zone"), name, siteType)
                {
                    Restricted = restricted
                });
            }

            internal NarrativeThread AddMatter(string archetypeId)
            {
                NarrativeThread thread = new NarrativeThread(World.NewId("thread"), archetypeId, GameTime.Zero)
                {
                    State = ThreadState.Active
                };
                World.Threads.Add(thread);
                return thread;
            }

            /// <summary>A place this mod made for the first matter, which now holds it.</summary>
            internal EntityId EstablishOldCache(string siteType)
            {
                SitePlan plan = PlanFor(FirstMatter, siteType, restricted: true);
                SiteGenesisResult result = SiteGenesis.Establish(World, plan, Stager, Vanilla.Now);
                Assert.True(result.Created, string.Join("; ", result.Reasons));
                return result.Site.Id;
            }

            internal void CloseTheOldMatter()
            {
                FirstMatter.State = ThreadState.Resolved;
            }

            internal SiteProvision Provide(SitePlan plan)
            {
                return SiteReuse.Provide(World, plan, Stager, Vanilla.Now);
            }

            /// <summary>
            /// A plan a caller would hand genesis: the matter it belongs to, the kind of place, who
            /// would be in it, what it would keep, and the two ways in. The reuse policy reads only
            /// the first three; the rest are here because a caller that might have to generate has
            /// to have built them anyway.
            /// </summary>
            internal SitePlan PlanFor(NarrativeThread matter, string siteType, bool restricted)
            {
                SitePlan plan = new SitePlan(World.NewId("zone"), "a place for " + matter.ArchetypeId, siteType, matter.Id)
                {
                    Restricted = restricted
                };

                plan.Occupants.Add(Occupant("Vetch", "keeper"));
                plan.Occupants.Add(Occupant("Dob", "guard"));
                plan.Occupants.Add(Occupant("Ilsa", "runner"));
                plan.Cargo.Add(new SiteCargoPlan(
                    new ItemDescriptor(World.NewId("item"), "a tally book", "book", 40, "book"),
                    plan.Occupants[0].Npc.Id));
                plan.Approaches.Add(new SiteApproach("persuade", true));
                plan.Approaches.Add(new SiteApproach("pick_lock", false));
                return plan;
            }

            private SiteOccupantPlan Occupant(string name, string role)
            {
                NarrativeNpc npc = new NarrativeNpc(World.NewId("npc"), name);
                return new SiteOccupantPlan(npc, role, new CharacterBlueprint(name).With(VanillaAttribute.Will, 10));
            }
        }
    }
}
