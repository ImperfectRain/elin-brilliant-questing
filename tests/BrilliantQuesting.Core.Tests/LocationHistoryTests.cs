using System.Collections.Generic;
using BrilliantQuesting.Continuity;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Events;
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
    /// BQ-086. A place accumulates history, and what keeps happening there becomes what the place
    /// is known for.
    ///
    /// The step's done-when is the first test: a site the player cleared a year earlier is
    /// described by its history when they come back to it. The rest hold the edges that would make
    /// that claim hollow - traffic is not history, a coincidence of zone is not a connection, a
    /// legend is a compression of the ledger rather than a second record, and nobody learns what
    /// happened somewhere by standing in it.
    /// </summary>
    public class LocationHistoryTests
    {
        private static readonly EntityId Player = EntityId.Parse("npc_player");
        private static readonly EntityId Elsewhere = EntityId.Parse("zone_elsewhere");

        /// <summary>
        /// The done-when. The clearing is a year old, the world has been through a save and a
        /// load, and the place still answers for it - both as derived history and in the trace a
        /// return visit prints.
        /// </summary>
        [Fact]
        public void ASiteClearedAYearEarlierIsDescribedByItsHistoryWhenItIsReused()
        {
            Fixture fixture = Fixture.Establish();
            fixture.Clear();

            fixture.Vanilla.AdvanceDays(365);
            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(fixture.World));

            IReadOnlyList<SiteHistoryEntry> history =
                LocationHistory.Of(reloaded, fixture.SiteId, fixture.Vanilla.Now);

            SiteHistoryEntry cleared = Assert.Single(history, e => e.Role == SiteHistoryRole.Cleared);
            Assert.Equal(365, cleared.AgeInDays);

            string described = NarrativeInspector.DescribeSite(reloaded, fixture.SiteId, fixture.Vanilla);
            Assert.Contains("Cleared", described);
            Assert.Contains("365d ago", described);
        }

        /// <summary>
        /// A place's history survives a reload because the ledger does, not because anything was
        /// written down twice. The reloaded world derives the same history from the same events.
        /// </summary>
        [Fact]
        public void HistoryIsDerivedFromTheLedgerRatherThanStoredOnThePlace()
        {
            Fixture fixture = Fixture.Establish();
            fixture.Clear();
            fixture.Hurt(fixture.KeeperId, 0.6);

            IReadOnlyList<SiteHistoryEntry> before = LocationHistory.Of(fixture.World, fixture.SiteId, fixture.Vanilla.Now);
            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(fixture.World));
            IReadOnlyList<SiteHistoryEntry> after = LocationHistory.Of(reloaded, fixture.SiteId, fixture.Vanilla.Now);

            Assert.Equal(before.Count, after.Count);
            for (int i = 0; i < before.Count; i++)
            {
                Assert.Equal(before[i].EventId, after[i].EventId);
                Assert.Equal(before[i].Role, after[i].Role);
                Assert.Equal(before[i].At, after[i].At);
            }
        }

        /// <summary>
        /// Traffic is not history. Meeting somebody in a place, talking there and the thread
        /// engine's own bookkeeping happened in the place and left nothing the place is remembered
        /// by - which is how "track only notable events" holds without a notable flag or a budget.
        /// </summary>
        [Fact]
        public void RoutineTrafficInAPlaceIsNotThatPlacesHistory()
        {
            Fixture fixture = Fixture.Establish();
            fixture.Record(WorldEventType.Met, Player, fixture.KeeperId);
            fixture.Record(WorldEventType.Conversed, Player, fixture.KeeperId);
            fixture.Record(WorldEventType.ThreadEscalated, EntityId.None, EntityId.None);

            Assert.Empty(LocationHistory.Of(fixture.World, fixture.SiteId, fixture.Vanilla.Now));
        }

        /// <summary>Something that happened somewhere else is not this place's history, however
        /// notable it was.</summary>
        [Fact]
        public void WhatHappenedSomewhereElseIsNotThisPlacesHistory()
        {
            Fixture fixture = Fixture.Establish();
            fixture.World.Record(
                WorldEventType.Killed,
                Player,
                fixture.KeeperId,
                fixture.Vanilla.Now,
                0.9,
                Elsewhere);

            Assert.Empty(LocationHistory.Of(fixture.World, fixture.SiteId, fixture.Vanilla.Now));
        }

        /// <summary>
        /// A place-naming event is the place's history even when the zone it recorded is the one
        /// around it. Clearing a cache under a boathouse is the cache's history, not the town's,
        /// and the ledger says which by naming the site.
        /// </summary>
        [Fact]
        public void ClearingAPlaceIsItsHistoryEvenWhenTheZoneRecordedIsTheSurroundingOne()
        {
            Fixture fixture = Fixture.Establish();
            fixture.World.Record(
                WorldEventType.SiteCleared,
                Player,
                fixture.SiteId,
                fixture.Vanilla.Now,
                0.6,
                Elsewhere);

            IReadOnlyList<SiteHistoryEntry> history = LocationHistory.Of(fixture.World, fixture.SiteId, fixture.Vanilla.Now);

            Assert.Single(history);
            Assert.Equal(SiteHistoryRole.Cleared, history[0].Role);
        }

        /// <summary>
        /// A place-naming event names a place, not a second person. Reporting the site id as the
        /// other party would hand every consumer somewhere dressed up as somebody.
        /// </summary>
        [Fact]
        public void TheOtherPartyToAClearingIsNobodyRatherThanThePlace()
        {
            Fixture fixture = Fixture.Establish();
            fixture.Clear();

            SiteHistoryEntry cleared = Assert.Single(
                LocationHistory.Of(fixture.World, fixture.SiteId, fixture.Vanilla.Now));

            Assert.True(cleared.Other.IsNone);
            Assert.Empty(cleared.Kinds);
        }

        /// <summary>
        /// The compression. Three separate maulings in one place are one thing the place is known
        /// for, not three - and the legend holds the events it compresses rather than a sentence
        /// about them.
        /// </summary>
        [Fact]
        public void ThingsThatKeepHappeningInAPlaceBecomeWhatThePlaceIsKnownFor()
        {
            Fixture fixture = Fixture.Establish();
            fixture.Hurt(fixture.KeeperId, 0.5);
            fixture.Vanilla.AdvanceDays(30);
            fixture.Hurt(fixture.GuardId, 0.5);
            fixture.Vanilla.AdvanceDays(30);
            fixture.Hurt(fixture.RunnerId, 0.5);

            IReadOnlyList<SiteLegend> legends = Legends(fixture);

            SiteLegend injury = Assert.Single(legends, l => l.Subject == CallbackKind.Injury);
            Assert.Equal(3, injury.Occurrences);
            Assert.True(injury.Repeated);
            Assert.Equal(3, injury.Entries.Count);
            Assert.True(injury.First < injury.Last);
        }

        /// <summary>
        /// One event can make a legend, but only if it was bad enough on its own. An ordinary
        /// scuffle in the same room is history and nothing more, which is what stops every place
        /// anything ever happened in from being famous for it.
        /// </summary>
        [Fact]
        public void OneSevereEventIsALegendWhereOneOrdinaryEventIsOnlyHistory()
        {
            Fixture ordinary = Fixture.Establish();
            ordinary.Hurt(ordinary.KeeperId, 0.4);
            Assert.NotEmpty(LocationHistory.Of(ordinary.World, ordinary.SiteId, ordinary.Vanilla.Now));
            Assert.Empty(Legends(ordinary));

            Fixture severe = Fixture.Establish();
            severe.Record(WorldEventType.Killed, Player, severe.KeeperId, 0.95);

            SiteLegend legend = Assert.Single(Legends(severe), l => l.Subject == CallbackKind.Injury);
            Assert.Equal(1, legend.Occurrences);
            Assert.False(legend.Repeated);
            Assert.True(legend.Salience >= LocationHistory.HighSalience);
        }

        /// <summary>
        /// Being found and being emptied are the place's own standing, not stories it is known
        /// for. Nothing in the simulation calls either a kind of tale, and minting one here would
        /// be this layer inventing the interpretation it then reports.
        /// </summary>
        [Fact]
        public void FindingAndClearingAPlaceAreItsHistoryWithoutBeingItsLegend()
        {
            Fixture fixture = Fixture.Establish();
            fixture.Clear();
            fixture.Vanilla.AdvanceDays(10);
            fixture.Clear();

            Assert.Equal(2, LocationHistory.Of(fixture.World, fixture.SiteId, fixture.Vanilla.Now).Count);
            Assert.Empty(Legends(fixture));
        }

        /// <summary>
        /// Standing somewhere teaches you nothing about what happened there. A stranger who was
        /// never near any of it derives no history and therefore no legend, so background
        /// simulation cannot hand anybody a past they were not part of.
        /// </summary>
        [Fact]
        public void SomebodyWithNoRouteToWhatHappenedHereCanTellNoneOfIt()
        {
            Fixture fixture = Fixture.Establish();
            fixture.Hurt(fixture.KeeperId, 0.6);
            fixture.Hurt(fixture.GuardId, 0.6);
            EntityId stranger = fixture.AddStranger();

            Assert.NotEmpty(LocationHistory.Of(fixture.World, fixture.SiteId, fixture.Vanilla.Now));
            Assert.Empty(LocationHistory.KnownTo(fixture.World, fixture.SiteId, stranger, fixture.Vanilla.Now));
            Assert.Empty(LocationHistory.Legends(
                LocationHistory.KnownTo(fixture.World, fixture.SiteId, stranger, fixture.Vanilla.Now)));
        }

        /// <summary>
        /// The gate is the callback gate, not a second one. Whoever did a thing can tell it;
        /// somebody it was done to without their noticing cannot, so an unnoticed act is history
        /// the world has and the victim does not.
        /// </summary>
        [Fact]
        public void AnUnnoticedActIsHistoryTheWorldHasAndTheVictimDoesNot()
        {
            Fixture fixture = Fixture.Establish();
            fixture.World.Record(
                WorldEventType.Theft,
                Player,
                fixture.KeeperId,
                fixture.Vanilla.Now,
                0.5,
                fixture.Zone,
                tags: new[] { EventTags.Unnoticed });

            Assert.Single(LocationHistory.Of(fixture.World, fixture.SiteId, fixture.Vanilla.Now));
            Assert.Single(LocationHistory.KnownTo(fixture.World, fixture.SiteId, Player, fixture.Vanilla.Now));
            Assert.Empty(LocationHistory.KnownTo(fixture.World, fixture.SiteId, fixture.KeeperId, fixture.Vanilla.Now));
        }

        /// <summary>
        /// The same compression answers both questions. What a witness can tell you the place is
        /// known for is derived from what they have a route to, with no second implementation to
        /// fall out of step with the first.
        /// </summary>
        [Fact]
        public void WhatOnePersonCanTellYouThePlaceIsKnownForFollowsFromWhatTheySaw()
        {
            Fixture fixture = Fixture.Establish();
            fixture.Hurt(fixture.KeeperId, 0.5, witnesses: new[] { fixture.GuardId });
            fixture.Hurt(fixture.RunnerId, 0.5, witnesses: new[] { fixture.GuardId });
            fixture.Hurt(fixture.GuardId, 0.5);

            IReadOnlyList<SiteHistoryEntry> guardsView =
                LocationHistory.KnownTo(fixture.World, fixture.SiteId, fixture.GuardId, fixture.Vanilla.Now);
            IReadOnlyList<SiteHistoryEntry> runnersView =
                LocationHistory.KnownTo(fixture.World, fixture.SiteId, fixture.RunnerId, fixture.Vanilla.Now);

            Assert.Equal(3, Assert.Single(LocationHistory.Legends(guardsView), l => l.Subject == CallbackKind.Injury).Occurrences);
            Assert.Empty(LocationHistory.Legends(runnersView));
        }

        /// <summary>Nothing depends on ledger walk order: the same world derives the same legends
        /// in the same order, and so does the same world read back out of its own save.</summary>
        [Fact]
        public void LegendsComeBackInTheSameOrderAcrossDerivationsAndReloads()
        {
            Fixture fixture = Fixture.Establish();
            fixture.Hurt(fixture.KeeperId, 0.5);
            fixture.Hurt(fixture.GuardId, 0.5);
            fixture.Record(WorldEventType.Theft, Player, fixture.KeeperId, 0.6);

            IReadOnlyList<SiteLegend> first = Legends(fixture);
            IReadOnlyList<SiteLegend> again = Legends(fixture);
            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(fixture.World));
            IReadOnlyList<SiteLegend> loaded = LocationHistory.Legends(
                LocationHistory.Of(reloaded, fixture.SiteId, fixture.Vanilla.Now));

            Assert.NotEmpty(first);
            Assert.Equal(Subjects(first), Subjects(again));
            Assert.Equal(Subjects(first), Subjects(loaded));
        }

        /// <summary>A place the world does not know has no history, and asking for it is not an
        /// error.</summary>
        [Fact]
        public void APlaceTheWorldDoesNotKnowHasNoHistory()
        {
            Fixture fixture = Fixture.Establish();
            fixture.Clear();

            Assert.Empty(LocationHistory.Of(fixture.World, EntityId.Parse("zone_nowhere"), fixture.Vanilla.Now));
            Assert.Empty(LocationHistory.Of(fixture.World, EntityId.None, fixture.Vanilla.Now));
        }

        /// <summary>
        /// The trace separates the two reasons somebody knows nothing about a place: the ledger
        /// recorded nothing of the kind, or they have no route to what it did record.
        /// </summary>
        [Fact]
        public void TheTraceSaysWhichEntriesAreNotTheirsToKnow()
        {
            Fixture fixture = Fixture.Establish();
            fixture.Hurt(fixture.KeeperId, 0.6);
            EntityId stranger = fixture.AddStranger();

            string trace = NarrativeInspector.DescribeSiteHistory(
                fixture.World, fixture.SiteId, stranger, fixture.Vanilla.Now);

            Assert.Contains("not theirs to know", trace);
            Assert.Contains(
                "history recorded nothing here",
                NarrativeInspector.DescribeSiteHistory(
                    fixture.World, EntityId.Parse("zone_nowhere"), stranger, fixture.Vanilla.Now));
        }

        // -- helpers ------------------------------------------------------------------------

        private static IReadOnlyList<SiteLegend> Legends(Fixture fixture)
        {
            return LocationHistory.Legends(
                LocationHistory.Of(fixture.World, fixture.SiteId, fixture.Vanilla.Now));
        }

        private static List<CallbackKind> Subjects(IReadOnlyList<SiteLegend> legends)
        {
            List<CallbackKind> subjects = new List<CallbackKind>();
            for (int i = 0; i < legends.Count; i++)
            {
                subjects.Add(legends[i].Subject);
            }

            return subjects;
        }

        // -- fixture ------------------------------------------------------------------------

        /// <summary>The BQ-087 proof site, established through the seam, with nothing scripted
        /// about its history.</summary>
        private sealed class Fixture
        {
            private const string SmugglerCache = "smuggler_cache";
            private int _strangers;

            private Fixture(NarrativeWorldState world, SandboxVanillaState vanilla, SitePlan plan)
            {
                World = world;
                Vanilla = vanilla;
                SiteId = plan.SiteId;
                KeeperId = plan.Occupants[0].Npc.Id;
                GuardId = plan.Occupants[1].Npc.Id;
                RunnerId = plan.Occupants[2].Npc.Id;
            }

            internal NarrativeWorldState World { get; }

            internal SandboxVanillaState Vanilla { get; }

            internal EntityId SiteId { get; }

            internal EntityId KeeperId { get; }

            internal EntityId GuardId { get; }

            internal EntityId RunnerId { get; }

            internal EntityId Zone => SiteGenesis.ZoneOf(World.Registry.GetSite(SiteId));

            internal static Fixture Establish()
            {
                NarrativeWorldState world = new NarrativeWorldState(86);
                SandboxVanillaState vanilla = new SandboxVanillaState(Player);
                NarrativeThread thread = new NarrativeThread(world.NewId("thread"), SmugglerCache, GameTime.Zero)
                {
                    State = ThreadState.Active
                };
                world.Threads.Add(thread);

                SitePlan plan = Plan(world, thread);
                SiteGenesisResult result = SiteGenesis.Establish(world, plan, new SandboxStager(vanilla), vanilla.Now);
                Assert.True(result.Created, string.Join("; ", result.Reasons));
                return new Fixture(world, vanilla, plan);
            }

            /// <summary>The player gets past whatever was holding the place shut.</summary>
            internal void Clear()
            {
                World.Record(WorldEventType.SiteCleared, Player, SiteId, Vanilla.Now, 0.6, Zone);
            }

            internal void Hurt(EntityId who, double magnitude, IReadOnlyList<EntityId> witnesses = null)
            {
                World.Record(WorldEventType.Harmed, Player, who, Vanilla.Now, magnitude, Zone, witnesses: witnesses);
            }

            internal void Record(WorldEventType type, EntityId actor, EntityId target, double magnitude = 0.5)
            {
                World.Record(type, actor, target, Vanilla.Now, magnitude, Zone);
            }

            /// <summary>Somebody who has never been near the place.</summary>
            internal EntityId AddStranger()
            {
                EntityId id = EntityId.Parse("npc_stranger" + _strangers++);
                World.Registry.Add(new NarrativeNpc(id, "a stranger"));
                Vanilla.Define(id, zone: Elsewhere);
                return id;
            }

            private static SitePlan Plan(NarrativeWorldState world, NarrativeThread thread)
            {
                SitePlan plan = new SitePlan(world.NewId("zone"), "the cache under the boathouse", SmugglerCache, thread.Id)
                {
                    DangerLevel = 3,
                    Seed = 86
                };

                plan.Occupants.Add(Occupant(world, "Vetch", "keeper"));
                plan.Occupants.Add(Occupant(world, "Dob", "guard"));
                plan.Occupants.Add(Occupant(world, "Ilsa", "runner"));

                Fact smuggling = new Fact(
                    world.NewId("fact"),
                    plan.Occupants[0].Npc.Id,
                    "smuggles_through",
                    plan.SiteId,
                    string.Empty,
                    TruthState.True);
                world.Knowledge.AddFact(smuggling);

                plan.Cargo.Add(new SiteCargoPlan(
                    new ItemDescriptor(world.NewId("item"), "a tally book", "book", 40, "book"),
                    plan.Occupants[0].Npc.Id,
                    smuggling.Id));

                plan.Approaches.Add(new SiteApproach("persuade", true));
                plan.Approaches.Add(new SiteApproach("pick_lock", false));
                return plan;
            }

            private static SiteOccupantPlan Occupant(NarrativeWorldState world, string name, string role)
            {
                NarrativeNpc npc = new NarrativeNpc(world.NewId("npc"), name);
                return new SiteOccupantPlan(npc, role, new CharacterBlueprint(name).With(VanillaAttribute.Will, 10));
            }
        }
    }
}
