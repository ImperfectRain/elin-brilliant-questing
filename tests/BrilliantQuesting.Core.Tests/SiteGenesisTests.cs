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
    /// BQ-087. One small BQ-owned place: one thread binding, three to five actors, real cargo and
    /// evidence, two ways in - and, months and a reload later, the same place.
    ///
    /// The step's done-when is a comparison, so these tests are mostly comparisons. The first is
    /// the whole of it: leave, save, load, come back, and find the same site with the same actors
    /// and the same cargo, with nothing regenerated and no historical event dispatched a second
    /// time. The rest hold the two edges that would make that claim hollow - a second genesis over
    /// a place somebody has already been, and a plan describing a place this step is not willing
    /// to make.
    /// </summary>
    public class SiteGenesisTests
    {
        private const string SmugglerCache = "smuggler_cache";

        /// <summary>
        /// The done-when. Nothing here is a fixture shortcut: the site is established through the
        /// seam, the departure is the whole cast being read somewhere else, and the return reads
        /// the world back out of its own save file.
        /// </summary>
        [Fact]
        public void AReturnVisitAfterAReloadFindsTheSameSiteActorsAndCargo()
        {
            Fixture fixture = Fixture.Establish();
            SiteVisit first = SiteGenesis.Visit(fixture.World, fixture.SiteId, fixture.Vanilla);
            Assert.True(first.Intact);

            int historyBefore = fixture.World.Ledger.Count;

            // Leave. A year passes, the save is written and read back in a different session.
            fixture.Vanilla.AdvanceDays(365);
            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(fixture.World));

            SiteVisit returned = SiteGenesis.Visit(reloaded, fixture.SiteId, fixture.Vanilla);

            Assert.True(returned.Intact);
            Assert.Equal(first.Site.Name, returned.Site.Name);
            Assert.Equal(first.Site.VanillaZoneRef, returned.Site.VanillaZoneRef);
            Assert.Equal(first.Site.EstablishedAt, returned.Site.EstablishedAt);
            Assert.Equal(first.Site.OccupantIds, returned.Site.OccupantIds);
            Assert.Equal(first.Site.ImportantObjectIds, returned.Site.ImportantObjectIds);

            // Nothing regenerated, and nothing historical was dispatched a second time: genesis
            // appends no events at all, so the count that survives the reload is the count that
            // went into it.
            Assert.Equal(historyBefore, reloaded.Ledger.Count);
        }

        /// <summary>
        /// Genesis is not a write to history. A place existing is not something that happened to
        /// anybody - the in-world events about a site are somebody finding it and somebody
        /// clearing it - and this is what makes "no redispatch on return" true by construction
        /// rather than by a listener being careful.
        /// </summary>
        [Fact]
        public void GenesisAppendsNothingToTheLedger()
        {
            NarrativeWorldState world = new NarrativeWorldState(7);
            SandboxVanillaState vanilla = new SandboxVanillaState(EntityId.Parse("npc_player"));
            SandboxStager stager = new SandboxStager(vanilla);
            NarrativeThread thread = Thread(world);

            Assert.Equal(0, world.Ledger.Count);
            SiteGenesisResult result = SiteGenesis.Establish(world, Plan(world, thread), stager, GameTime.Zero);

            Assert.True(result.Created);
            Assert.Equal(0, world.Ledger.Count);
        }

        /// <summary>
        /// A visited place is never destructively regenerated (`PP §6`). Running genesis again for
        /// the same place hands back what is already there, stages nobody, and does not move the
        /// time it says the place was made.
        /// </summary>
        [Fact]
        public void GenesisRunsOnceAndASecondAttemptStagesNothing()
        {
            Fixture fixture = Fixture.Establish();
            List<EntityId> occupants = new List<EntityId>(fixture.Site.OccupantIds);
            GameTime made = fixture.Site.EstablishedAt;

            fixture.Vanilla.AdvanceDays(40);
            SiteGenesisResult again = SiteGenesis.Establish(
                fixture.World, fixture.Replan(), fixture.Stager, fixture.Vanilla.Now);

            Assert.Equal(SiteGenesisOutcome.AlreadyEstablished, again.Outcome);
            Assert.Same(fixture.Site, again.Site);
            Assert.Equal(occupants, fixture.Site.OccupantIds);
            Assert.Equal(made, fixture.Site.EstablishedAt);
        }

        /// <summary>The refusal survives a reload, which is the case that actually matters: a
        /// player who saves inside a generated place and loads back into it must not walk into a
        /// second cast of it.</summary>
        [Fact]
        public void ASiteReloadedFromASaveStillRefusesToBeGeneratedOver()
        {
            Fixture fixture = Fixture.Establish();
            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(fixture.World));

            SiteGenesisResult again = SiteGenesis.Establish(
                reloaded, fixture.Replan(), fixture.Stager, fixture.Vanilla.Now);

            Assert.Equal(SiteGenesisOutcome.AlreadyEstablished, again.Outcome);
            Assert.True(reloaded.Registry.GetSite(fixture.SiteId).Established);
        }

        /// <summary>
        /// The cargo is real: it is in somebody's inventory in the game, and the evidence among it
        /// is attached to the fact it proves rather than copied onto the place.
        /// </summary>
        [Fact]
        public void CargoIsHeldInTheGameAndEvidenceHangsOnTheFactItProves()
        {
            Fixture fixture = Fixture.Establish();

            Assert.Contains(fixture.Vanilla.GetInventory(fixture.KeeperId), i => i.Id == fixture.LedgerBookId);
            Assert.Contains(fixture.LedgerBookId, fixture.World.Knowledge.GetFact(fixture.SmugglingFactId).EvidenceIds);
            Assert.Contains(fixture.LedgerBookId, fixture.Site.ImportantObjectIds);
        }

        /// <summary>
        /// The manifest is a comparison, not a decoration. A keeper who has walked off and cargo
        /// that has left with them both show up as drift, and the visit says which.
        /// </summary>
        [Fact]
        public void AVisitReportsWhoAndWhatIsNoLongerThere()
        {
            Fixture fixture = Fixture.Establish();
            fixture.Vanilla.SetZone(fixture.KeeperId, EntityId.Parse("zone_elsewhere"));

            SiteVisit visit = SiteGenesis.Visit(fixture.World, fixture.SiteId, fixture.Vanilla);

            Assert.False(visit.Intact);
            Assert.Contains(fixture.KeeperId, visit.MissingOccupants);
            Assert.Contains(fixture.LedgerBookId, visit.MissingCargo);
            Assert.Contains("gone", NarrativeInspector.DescribeSite(fixture.World, fixture.SiteId, fixture.Vanilla));
        }

        /// <summary>
        /// Cargo that changed hands inside the place has not gone anywhere. The manifest is about
        /// the site, not about one person's pockets.
        /// </summary>
        [Fact]
        public void CargoHandedToAnotherOccupantIsStillAtTheSite()
        {
            Fixture fixture = Fixture.Establish();
            Assert.True(fixture.Vanilla.TryTransferItem(fixture.LedgerBookId, fixture.KeeperId, fixture.GuardId));
            Assert.DoesNotContain(fixture.Vanilla.GetInventory(fixture.KeeperId), i => i.Id == fixture.LedgerBookId);

            SiteVisit visit = SiteGenesis.Visit(fixture.World, fixture.SiteId, fixture.Vanilla);

            Assert.True(visit.Intact);
            Assert.Empty(visit.MissingCargo);
        }

        /// <summary>
        /// Two ways in, and they have to differ in the way that matters. Two verbs that both wait
        /// on the keeper's permission are one approach spelled twice.
        /// </summary>
        [Fact]
        public void EveryWayInWaitingOnSomebodyIsRefusedAsOneApproach()
        {
            NarrativeWorldState world = new NarrativeWorldState(11);
            NarrativeThread thread = Thread(world);
            SitePlan plan = Plan(world, thread);
            plan.Approaches.Clear();
            plan.Approaches.Add(new SiteApproach("persuade", true));
            plan.Approaches.Add(new SiteApproach("bribe", true));

            IReadOnlyList<string> refusals = SiteGenesis.Refusals(world, plan);

            Assert.Contains(refusals, r => r.Contains("one approach, not two"));
        }

        [Fact]
        public void APlanWithOneWayInIsRefused()
        {
            NarrativeWorldState world = new NarrativeWorldState(12);
            SitePlan plan = Plan(world, Thread(world));
            plan.Approaches.Clear();
            plan.Approaches.Add(new SiteApproach("pick_lock", false));

            Assert.Contains(SiteGenesis.Refusals(world, plan), r => r.Contains("ways in"));
        }

        /// <summary>Three to five. A place with one person in it is a prop.</summary>
        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(6)]
        public void APlaceIsPopulatedByThreeToFiveActors(int count)
        {
            NarrativeWorldState world = new NarrativeWorldState(13);
            SitePlan plan = Plan(world, Thread(world));
            while (plan.Occupants.Count > count)
            {
                plan.Occupants.RemoveAt(plan.Occupants.Count - 1);
            }

            while (plan.Occupants.Count < count)
            {
                plan.Occupants.Add(Occupant(world, "extra" + plan.Occupants.Count, "hand"));
            }

            Assert.Contains(SiteGenesis.Refusals(world, plan), r => r.Contains("actors"));
        }

        [Fact]
        public void APlaceThatKeepsNothingIsRefused()
        {
            NarrativeWorldState world = new NarrativeWorldState(14);
            SitePlan plan = Plan(world, Thread(world));
            plan.Cargo.Clear();

            Assert.Contains(SiteGenesis.Refusals(world, plan), r => r.Contains("keeps nothing"));
        }

        [Fact]
        public void CargoNobodyAtThePlaceHoldsIsRefused()
        {
            NarrativeWorldState world = new NarrativeWorldState(15);
            SitePlan plan = Plan(world, Thread(world));
            plan.Cargo.Clear();
            plan.Cargo.Add(new SiteCargoPlan(
                new ItemDescriptor(world.NewId("item"), "a strongbox", "container", 200, "chest"),
                EntityId.Parse("npc_somebody_else")));

            Assert.Contains(SiteGenesis.Refusals(world, plan), r => r.Contains("nobody at the place holds"));
        }

        /// <summary>A site with no matter behind it is scenery, and this step does not make
        /// scenery.</summary>
        [Fact]
        public void APlaceBelongingToNoThreadIsRefused()
        {
            NarrativeWorldState world = new NarrativeWorldState(16);
            SitePlan plan = Plan(world, Thread(world));
            SitePlan orphan = new SitePlan(plan.SiteId, "the cache", SmugglerCache, EntityId.Parse("thread_nowhere"));
            orphan.Occupants.AddRange(plan.Occupants);
            orphan.Cargo.AddRange(plan.Cargo);
            orphan.Approaches.AddRange(plan.Approaches);

            Assert.Contains(SiteGenesis.Refusals(world, orphan), r => r.Contains("no thread"));
        }

        /// <summary>
        /// Fail closed. An adapter that cannot give the place a body leaves nothing behind: no
        /// site in the save, nobody staged, and a reason that says which half failed.
        /// </summary>
        [Fact]
        public void AnAdapterThatCannotPlaceTheSiteLeavesNothingBehind()
        {
            NarrativeWorldState world = new NarrativeWorldState(17);
            SandboxVanillaState vanilla = new SandboxVanillaState(EntityId.Parse("npc_player"));
            NarrativeThread thread = Thread(world);
            SitePlan plan = Plan(world, thread);

            SiteGenesisResult result = SiteGenesis.Establish(world, plan, new BodilessStager(vanilla), GameTime.Zero);

            Assert.Equal(SiteGenesisOutcome.NotEmbodied, result.Outcome);
            Assert.Null(world.Registry.GetSite(plan.SiteId));
            Assert.Empty(world.Registry.AllNpcs);
            Assert.DoesNotContain(plan.SiteId, thread.SiteIds);
        }

        /// <summary>The one thread binding is a real binding: the matter names the place.</summary>
        [Fact]
        public void TheThreadTheSiteBelongsToNamesIt()
        {
            Fixture fixture = Fixture.Establish();
            Assert.Contains(fixture.SiteId, fixture.Thread.SiteIds);
        }

        /// <summary>Genesis never builds over a place the world already has, however it got there -
        /// an archetype that wrote one down directly is not a site waiting to be generated.</summary>
        [Fact]
        public void APlaceTheWorldAlreadyKnowsIsNotGeneratedOver()
        {
            NarrativeWorldState world = new NarrativeWorldState(18);
            SandboxVanillaState vanilla = new SandboxVanillaState(EntityId.Parse("npc_player"));
            NarrativeThread thread = Thread(world);
            SitePlan plan = Plan(world, thread);
            world.Registry.Add(new NarrativeSite(plan.SiteId, "somebody else's mine", "mine"));

            SiteGenesisResult result = SiteGenesis.Establish(world, plan, new SandboxStager(vanilla), GameTime.Zero);

            Assert.Equal(SiteGenesisOutcome.PlanRejected, result.Outcome);
            Assert.Equal("somebody else's mine", world.Registry.GetSite(plan.SiteId).Name);
        }

        /// <summary>The ways in survive the save. A route that only exists until the next load is
        /// not a route.</summary>
        [Fact]
        public void TheWaysInSurviveASave()
        {
            Fixture fixture = Fixture.Establish();
            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(fixture.World));

            IReadOnlyList<SiteApproach> approaches = reloaded.Registry.GetSite(fixture.SiteId).Approaches;

            Assert.Equal(fixture.Site.Approaches.Count, approaches.Count);
            Assert.Contains(approaches, a => a.NeedsAdmission);
            Assert.Contains(approaches, a => !a.NeedsAdmission);
        }

        /// <summary>
        /// A save written before generated places existed has none of the three new nodes. It
        /// loads, the place keeps everything it did have, and it reads back as a place nobody
        /// generated - which is the truth about it, and leaves the flag meaning exactly one thing.
        /// </summary>
        [Fact]
        public void ASaveWithNoGenesisNodesStillLoadsAsAPlaceNobodyGenerated()
        {
            Fixture fixture = Fixture.Establish();
            string legacy = WorldStateSerializer.Save(fixture.World)
                .Replace("\"established\"", "\"gone_established\"")
                .Replace("\"establishedAt\"", "\"gone_establishedAt\"")
                .Replace("\"approaches\"", "\"gone_approaches\"");

            NarrativeSite site = WorldStateSerializer.Load(legacy).Registry.GetSite(fixture.SiteId);

            Assert.False(site.Established);
            Assert.Empty(site.Approaches);
            Assert.Equal(fixture.Site.Name, site.Name);
            Assert.Equal(fixture.Site.VanillaZoneRef, site.VanillaZoneRef);
            Assert.Equal(fixture.Site.OccupantIds, site.OccupantIds);
            Assert.Equal(fixture.Site.ImportantObjectIds, site.ImportantObjectIds);
        }

        // -- fixture ------------------------------------------------------------------------

        private static NarrativeThread Thread(NarrativeWorldState world)
        {
            NarrativeThread thread = new NarrativeThread(world.NewId("thread"), SmugglerCache, GameTime.Zero)
            {
                State = ThreadState.Active
            };
            world.Threads.Add(thread);
            return thread;
        }

        private static SiteOccupantPlan Occupant(NarrativeWorldState world, string name, string role)
        {
            NarrativeNpc npc = new NarrativeNpc(world.NewId("npc"), name);
            return new SiteOccupantPlan(npc, role, new CharacterBlueprint(name).With(VanillaAttribute.Will, 10));
        }

        private static SitePlan Plan(NarrativeWorldState world, NarrativeThread thread)
        {
            SitePlan plan = new SitePlan(world.NewId("zone"), "the cache under the boathouse", SmugglerCache, thread.Id)
            {
                DangerLevel = 3,
                Seed = 4242
            };

            SiteOccupantPlan keeper = Occupant(world, "Vetch", "keeper");
            plan.Occupants.Add(keeper);
            plan.Occupants.Add(Occupant(world, "Dob", "guard"));
            plan.Occupants.Add(Occupant(world, "Ilsa", "runner"));

            Fact smuggling = new Fact(
                world.NewId("fact"),
                keeper.Npc.Id,
                "smuggles_through",
                plan.SiteId,
                string.Empty,
                TruthState.True);
            world.Knowledge.AddFact(smuggling);

            plan.Cargo.Add(new SiteCargoPlan(
                new ItemDescriptor(world.NewId("item"), "a tally book", "book", 40, "book"),
                keeper.Npc.Id,
                smuggling.Id));
            plan.Cargo.Add(new SiteCargoPlan(
                new ItemDescriptor(world.NewId("item"), "a bale of untaxed cloth", "cloth", 300, "cloth"),
                plan.Occupants[1].Npc.Id));

            plan.Approaches.Add(new SiteApproach("persuade", true));
            plan.Approaches.Add(new SiteApproach("pick_lock", false));
            return plan;
        }

        private sealed class Fixture
        {
            private Fixture(NarrativeWorldState world, SandboxVanillaState vanilla, SandboxStager stager, SitePlan plan, NarrativeThread thread)
            {
                World = world;
                Vanilla = vanilla;
                Stager = stager;
                Thread = thread;
                SiteId = plan.SiteId;
                KeeperId = plan.Occupants[0].Npc.Id;
                GuardId = plan.Occupants[1].Npc.Id;
                LedgerBookId = plan.Cargo[0].Item.Id;
                SmugglingFactId = plan.Cargo[0].EvidenceForFact;
            }

            internal NarrativeWorldState World { get; }

            internal SandboxVanillaState Vanilla { get; }

            internal SandboxStager Stager { get; }

            internal NarrativeThread Thread { get; }

            internal EntityId SiteId { get; }

            internal EntityId KeeperId { get; }

            internal EntityId GuardId { get; }

            internal EntityId LedgerBookId { get; }

            internal EntityId SmugglingFactId { get; }

            internal NarrativeSite Site => World.Registry.GetSite(SiteId);

            internal static Fixture Establish()
            {
                NarrativeWorldState world = new NarrativeWorldState(2026);
                SandboxVanillaState vanilla = new SandboxVanillaState(EntityId.Parse("npc_player"));
                SandboxStager stager = new SandboxStager(vanilla);
                NarrativeThread thread = Thread(world);
                SitePlan plan = Plan(world, thread);

                SiteGenesisResult result = SiteGenesis.Establish(world, plan, stager, vanilla.Now);
                Assert.True(result.Created, string.Join("; ", result.Reasons));
                return new Fixture(world, vanilla, stager, plan, thread);
            }

            /// <summary>The same place, planned a second time, as a later pass over the same
            /// settlement would produce it.</summary>
            internal SitePlan Replan()
            {
                SitePlan again = new SitePlan(SiteId, "the cache under the boathouse", SmugglerCache, Thread.Id);
                again.Occupants.Add(Occupant(World, "Vetch", "keeper"));
                again.Occupants.Add(Occupant(World, "Dob", "guard"));
                again.Occupants.Add(Occupant(World, "Ilsa", "runner"));
                again.Cargo.Add(new SiteCargoPlan(
                    new ItemDescriptor(World.NewId("item"), "a tally book", "book", 40, "book"),
                    again.Occupants[0].Npc.Id));
                again.Approaches.Add(new SiteApproach("persuade", true));
                again.Approaches.Add(new SiteApproach("pick_lock", false));
                return again;
            }
        }

        /// <summary>A build with nowhere to put a place. Everything else about it works.</summary>
        private sealed class BodilessStager : ISituationStager
        {
            private readonly SandboxStager _inner;

            internal BodilessStager(SandboxVanillaState vanilla)
            {
                _inner = new SandboxStager(vanilla);
            }

            public void StageCharacter(EntityId id, CharacterBlueprint blueprint, EntityId zone)
            {
                _inner.StageCharacter(id, blueprint, zone);
            }

            public void StageItem(EntityId owner, ItemDescriptor item)
            {
                _inner.StageItem(owner, item);
            }

            public string StageSite(SiteBlueprint blueprint) => string.Empty;
        }
    }
}
