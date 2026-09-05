using System.Collections.Generic;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Continuity;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Obligations;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Relationships;
using BrilliantQuesting.Situations;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-117. The Chronicle read as who this character became, and as one piece of text somebody
    /// can carry away from the game.
    ///
    /// The step's done-when is a person: a tester reads their own chronicle and can retell it
    /// without the game open. The first test stands in for that as far as a headless test can -
    /// everything a retelling needs is in the exported text, by the names the world gave it. The
    /// rest hold the edges that would make the claim hollow: the trophy case is a reading rather
    /// than a record, ordinary traffic never reaches it, nothing the player did not notice does,
    /// and it never claims a rescue the world stopped holding.
    /// </summary>
    public class ChronicleNarrativeTests
    {
        /// <summary>
        /// The done-when, as far as a headless test reaches it. One document, and the feud, the
        /// person helped, the place and the finished matter are all in it under the names the
        /// world gave them - so it can be read by somebody who has never seen the save.
        /// </summary>
        [Fact]
        public void TheExportedChronicleTellsTheWholeLifeByNameWithoutTheGame()
        {
            Fixture fixture = Fixture.Establish();
            fixture.Feud();
            fixture.Vanilla.Now = fixture.Vanilla.Now.PlusDays(12);
            fixture.Rescue();
            fixture.Vanilla.Now = fixture.Vanilla.Now.PlusDays(9);
            fixture.Clear();
            fixture.SaveTheShop();
            fixture.Vanilla.Now = fixture.Vanilla.Now.PlusDays(4);
            fixture.FinishAMatter();

            string text = ChronicleNarrative.Export(fixture.World, fixture.Player, fixture.Vanilla.Now);

            Assert.Contains(Fixture.PlayerName, text);
            Assert.Contains(Fixture.RivalName, text);
            Assert.Contains(Fixture.KeeperName, text);
            Assert.Contains(Fixture.SiteName, text);
            Assert.Contains("recovered", text);
            Assert.Contains("smuggling settled", text);

            // A life, not an instant: the days the events carry are the days the text reports.
            Assert.Contains("day 21", text);
            Assert.Contains("day 25", text);

            // Names, days and words - never a raw minted handle, which a reader outside the game
            // has no way to resolve.
            Assert.DoesNotContain("npc_", text);
            Assert.DoesNotContain("evt_", text);
            Assert.DoesNotContain("zone_", text);
        }

        /// <summary>
        /// Where the world never named something, the export says nothing rather than printing a
        /// minted handle a reader outside the game cannot resolve.
        /// </summary>
        [Fact]
        public void SomethingTheWorldNeverNamedIsLeftOutRatherThanPrintedAsAHandle()
        {
            Fixture fixture = Fixture.Establish();
            fixture.World.Registry.Add(new NarrativeNpc(fixture.Player, string.Empty));
            fixture.SaveTheShop();

            string text = ChronicleNarrative.Export(fixture.World, fixture.Player, fixture.Vanilla.Now);

            Assert.Contains("Businesses you changed", text);
            Assert.DoesNotContain("business_", text);
            Assert.DoesNotContain("npc_", text);
        }

        [Fact]
        public void AFreshLifeHasNothingToTell()
        {
            Fixture fixture = Fixture.Establish();

            ChronicleLife life = ChronicleNarrative.Read(fixture.World, fixture.Player, fixture.Vanilla.Now);

            Assert.True(life.IsEmpty);
            Assert.Contains("Nothing to tell yet", ChronicleNarrative.Export(fixture.World, fixture.Player, fixture.Vanilla.Now));
        }

        /// <summary>
        /// The bar for a figure is BQ-086's bar for a legend, applied to a person: once is an
        /// incident, twice is a pattern.
        /// </summary>
        [Fact]
        public void OneOrdinaryDealingIsNotYetSomebodyTheLifeIsAbout()
        {
            Fixture fixture = Fixture.Establish();
            fixture.Record(WorldEventType.Helped, fixture.Player, fixture.KeeperId, 0.4);

            Assert.Empty(ChronicleNarrative.Read(fixture.World, fixture.Player, fixture.Vanilla.Now).Figures);

            fixture.Record(WorldEventType.Helped, fixture.Player, fixture.KeeperId, 0.4);

            ChronicleFigure figure = Assert.Single(
                ChronicleNarrative.Read(fixture.World, fixture.Player, fixture.Vanilla.Now).Figures);
            Assert.Equal(fixture.KeeperId, figure.Actor);
            Assert.Equal(2, figure.Dealings);
            Assert.Contains(CallbackKind.Kindness, figure.Kinds);
        }

        /// <summary>The other half of the same bar: one thing heavy enough stands on its own.</summary>
        [Fact]
        public void OneHeavyEnoughDealingIsSomebodyTheLifeIsAbout()
        {
            Fixture fixture = Fixture.Establish();
            fixture.Record(WorldEventType.Rescued, fixture.Player, fixture.KeeperId, 0.95);

            ChronicleFigure figure = Assert.Single(
                ChronicleNarrative.Read(fixture.World, fixture.Player, fixture.Vanilla.Now).Figures);

            Assert.Equal(fixture.KeeperId, figure.Actor);
            Assert.Equal(1, figure.Dealings);
        }

        /// <summary>
        /// Traffic is not a life. Meeting and talking leave nothing anybody could bring up
        /// afterwards, and the trophy case reads that from the callback table rather than from a
        /// list of its own.
        /// </summary>
        [Fact]
        public void MeetingAndTalkingToSomebodyForeverDoesNotMakeThemPartOfTheStory()
        {
            Fixture fixture = Fixture.Establish();
            for (int i = 0; i < 20; i++)
            {
                fixture.Record(WorldEventType.Met, fixture.Player, fixture.KeeperId, 0.5);
                fixture.Record(WorldEventType.Conversed, fixture.Player, fixture.KeeperId, 0.5);
            }

            Assert.Empty(ChronicleNarrative.Read(fixture.World, fixture.Player, fixture.Vanilla.Now).Figures);
        }

        /// <summary>
        /// A chronicle is what the player could know. Somebody who has been quietly robbing them
        /// is not in it, because nothing told them it was happening - the same gate BQ-081 applies
        /// to a callback, not a second rule.
        /// </summary>
        [Fact]
        public void SomebodyWhoWrongedThePlayerUnnoticedIsNotInTheirChronicle()
        {
            Fixture fixture = Fixture.Establish();
            fixture.Unnoticed(WorldEventType.Theft, fixture.RivalId, fixture.Player);
            fixture.Unnoticed(WorldEventType.Theft, fixture.RivalId, fixture.Player);
            fixture.Unnoticed(WorldEventType.Theft, fixture.RivalId, fixture.Player);

            Assert.Empty(ChronicleNarrative.Read(fixture.World, fixture.Player, fixture.Vanilla.Now).Figures);

            // The same acts, once the player is there for them. Three unnoticed thefts counted
            // for nothing, so it takes two noticed ones to reach the bar rather than none.
            fixture.Record(WorldEventType.Theft, fixture.RivalId, fixture.Player, 0.6);
            fixture.Record(WorldEventType.Theft, fixture.RivalId, fixture.Player, 0.6);

            ChronicleFigure figure = Assert.Single(
                ChronicleNarrative.Read(fixture.World, fixture.Player, fixture.Vanilla.Now).Figures);
            Assert.Equal(2, figure.Dealings);
        }

        /// <summary>
        /// A figure reports what the ledger and the relationship graph hold, and stops there. It
        /// is the reader who calls a run of injuries beside an enemy edge a feud; nothing in Core
        /// decides that a history meant something.
        /// </summary>
        [Fact]
        public void AFigureCarriesTheTieTheWorldRecordsWithoutNamingWhatItMeans()
        {
            Fixture fixture = Fixture.Establish();
            fixture.Feud();

            ChronicleFigure figure = Assert.Single(
                ChronicleNarrative.Read(fixture.World, fixture.Player, fixture.Vanilla.Now).Figures,
                f => f.Actor == fixture.RivalId);

            Assert.Contains(CallbackKind.Injury, figure.Kinds);
            Assert.NotNull(figure.Tie);
            Assert.Equal(RelationKind.Enemy, figure.Tie.Kind);
            Assert.True(figure.Tie.Sentiment < 0);
        }

        /// <summary>
        /// A place is in the chronicle because the player made that history. Somewhere they only
        /// stood while somebody else made it is that person's mark, not theirs.
        /// </summary>
        [Fact]
        public void APlaceCarriesThePlayersNameOnlyWhereTheyLeftTheMark()
        {
            Fixture fixture = Fixture.Establish();
            fixture.Clear();

            ChroniclePlace place = Assert.Single(
                ChronicleNarrative.Read(fixture.World, fixture.Player, fixture.Vanilla.Now).Places);
            Assert.Equal(fixture.SiteId, place.SiteId);
            Assert.True(place.Cleared);

            Fixture other = Fixture.Establish();
            other.World.Record(
                WorldEventType.SiteCleared, other.KeeperId, other.SiteId, other.Vanilla.Now, 0.6, other.Zone);

            Assert.Empty(ChronicleNarrative.Read(other.World, other.Player, other.Vanilla.Now).Places);
        }

        /// <summary>
        /// What the place is known for because of the player is BQ-086's own compression of their
        /// own marks, not a second motif vocabulary invented here.
        /// </summary>
        [Fact]
        public void WhatAPlaceIsKnownForIsTheCompressionOfTheMarksThePlayerLeft()
        {
            Fixture fixture = Fixture.Establish();
            fixture.Hurt(fixture.KeeperId, 0.7);
            fixture.Hurt(fixture.GuardId, 0.7);
            fixture.Hurt(fixture.RunnerId, 0.7);

            ChroniclePlace place = Assert.Single(
                ChronicleNarrative.Read(fixture.World, fixture.Player, fixture.Vanilla.Now).Places);

            Assert.False(place.Cleared);
            Assert.Contains(place.Legends, legend => legend.Subject == CallbackKind.Injury && legend.Repeated);
        }

        /// <summary>
        /// The trophy case is a reading of the ledger, so a reload produces the same life for the
        /// same reason the events do. Nothing about it is stored, indexed or migrated.
        /// </summary>
        [Fact]
        public void TheWholeLifeSurvivesSaveAndLoadBecauseNoneOfItIsStored()
        {
            Fixture fixture = Fixture.Establish();
            fixture.Feud();
            fixture.Rescue();
            fixture.Clear();
            fixture.SaveTheShop();

            string before = ChronicleNarrative.Export(fixture.World, fixture.Player, fixture.Vanilla.Now);
            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(fixture.World));
            string after = ChronicleNarrative.Export(reloaded, fixture.Player, fixture.Vanilla.Now);

            Assert.Equal(before, after);
        }

        /// <summary>
        /// A shop the player put back on its feet is theirs to retell, and the chronicle says so
        /// because the state change recorded who made it.
        /// </summary>
        [Fact]
        public void AShopThePlayerPutBackOnItsFeetIsPartOfTheirHistory()
        {
            Fixture fixture = Fixture.Establish();
            fixture.SaveTheShop();

            ChronicleWork work = Assert.Single(
                ChronicleNarrative.Read(fixture.World, fixture.Player, fixture.Vanilla.Now).Works);

            Assert.Equal(BusinessContinuityState.Recovered, work.Left);
            Assert.True(work.Holds);
            Assert.Equal(fixture.KeeperId, work.OperatorId);
        }

        /// <summary>
        /// And it never claims a rescue the world stopped holding: what the player did and what
        /// the shop is today are two readings, and the second one is read live.
        /// </summary>
        [Fact]
        public void AShopThatFailedAgainStillReportsWhatItIsNow()
        {
            Fixture fixture = Fixture.Establish();
            fixture.SaveTheShop();
            fixture.Vanilla.Now = fixture.Vanilla.Now.PlusDays(40);
            new BusinessContinuity(fixture.World).TryChangeState(
                fixture.BusinessId, BusinessContinuityState.Failed, fixture.Vanilla.Now, actor: fixture.RivalId);

            ChronicleWork work = Assert.Single(
                ChronicleNarrative.Read(fixture.World, fixture.Player, fixture.Vanilla.Now).Works);

            Assert.Equal(BusinessContinuityState.Recovered, work.Left);
            Assert.Equal(BusinessContinuityState.Failed, work.Now);
            Assert.False(work.Holds);
            Assert.Contains("today it is failed", ChronicleNarrative.Export(fixture.World, fixture.Player, fixture.Vanilla.Now));
        }

        /// <summary>Somebody else's doing is somebody else's chronicle.</summary>
        [Fact]
        public void AShopSomebodyElseChangedIsNotThePlayersDoing()
        {
            Fixture fixture = Fixture.Establish();
            new BusinessContinuity(fixture.World).TryChangeState(
                fixture.BusinessId, BusinessContinuityState.Recovered, fixture.Vanilla.Now, actor: fixture.RivalId);

            Assert.Empty(ChronicleNarrative.Read(fixture.World, fixture.Player, fixture.Vanilla.Now).Works);
        }

        /// <summary>
        /// What is finished stays BQ-034's reading. The trophy case arranges it; it does not keep
        /// a second copy that could disagree with the first.
        /// </summary>
        [Fact]
        public void WhatIsFinishedIsTheChroniclesOwnReadingRatherThanASecondCopy()
        {
            TheftLaboratory lab = TheftLaboratory.Create();
            lab.Checks = new FixedCheckResolver(CheckOutcome.Pass);
            lab.Perform("pickpocket", lab.Situation.ThiefId);
            lab.Perform("return_item", lab.Situation.VictimId);

            ChronicleLife life = ChronicleNarrative.Read(lab.World, lab.Player, lab.Vanilla.Now);

            Assert.Equal(Chronicle.Entries(lab.World, lab.Player).Count, life.Matters.Count);
            Assert.Equal("property_returned", Assert.Single(life.Matters).Outcome);
            Assert.Contains("property returned", ChronicleNarrative.Export(lab.World, lab.Player, lab.Vanilla.Now));
        }

        /// <summary>
        /// Open standing is BQ-118's sheet and stays there. A favour still owed is something the
        /// player holds, not something they finished, and a trophy case that listed both would be
        /// a to-do list again.
        /// </summary>
        [Fact]
        public void WhatIsStillOwedBelongsToTheStandingSheetRatherThanTheChronicle()
        {
            Fixture fixture = Fixture.Establish();
            fixture.Rescue();
            WorldEvent helped = fixture.Record(WorldEventType.Helped, fixture.Player, fixture.KeeperId, 0.8);
            fixture.World.Obligations.Add(new SocialObligation(
                fixture.World.NewId("obligation"),
                SocialObligationKind.Favor,
                fixture.KeeperId,
                fixture.Player,
                EntityId.None,
                string.Empty,
                fixture.Vanilla.Now,
                helped.Id));

            string chronicle = ChronicleNarrative.Export(fixture.World, fixture.Player, fixture.Vanilla.Now);
            string standing = StandingSheet.Describe(fixture.World, fixture.Vanilla);

            Assert.Contains("owes you", standing);
            Assert.DoesNotContain("owes you", chronicle);
        }

        private sealed class Fixture
        {
            internal const string SiteName = "the cache under the boathouse";
            internal const string KeeperName = "Vetch";
            internal const string RivalName = "Haron";
            internal const string PlayerName = "Wren";
            private const string SmugglerCache = "smuggler_cache";

            private Fixture(NarrativeWorldState world, SandboxVanillaState vanilla, SitePlan plan, EntityId rival)
            {
                World = world;
                Vanilla = vanilla;
                SiteId = plan.SiteId;
                KeeperId = plan.Occupants[0].Npc.Id;
                GuardId = plan.Occupants[1].Npc.Id;
                RunnerId = plan.Occupants[2].Npc.Id;
                RivalId = rival;
                BusinessId = world.NewId("business");
                new BusinessContinuity(world).TryRegister(BusinessId, SiteId, KeeperId, vanilla.Now);
            }

            internal NarrativeWorldState World { get; }

            internal SandboxVanillaState Vanilla { get; }

            internal EntityId Player { get; } = ChronicleNarrativeTests.Player;

            internal EntityId SiteId { get; }

            internal EntityId KeeperId { get; }

            internal EntityId GuardId { get; }

            internal EntityId RunnerId { get; }

            internal EntityId RivalId { get; }

            internal EntityId BusinessId { get; }

            internal EntityId Zone => SiteGenesis.ZoneOf(World.Registry.GetSite(SiteId));

            internal static Fixture Establish()
            {
                NarrativeWorldState world = new NarrativeWorldState(117);
                SandboxVanillaState vanilla = new SandboxVanillaState(ChronicleNarrativeTests.Player);
                NarrativeThread thread = new NarrativeThread(world.NewId("thread"), SmugglerCache, GameTime.Zero)
                {
                    State = ThreadState.Active
                };
                world.Threads.Add(thread);

                SitePlan plan = Plan(world, thread);
                SiteGenesisResult result = SiteGenesis.Establish(world, plan, new SandboxStager(vanilla), vanilla.Now);
                Assert.True(result.Created, string.Join("; ", result.Reasons));

                world.Registry.Add(new NarrativeNpc(ChronicleNarrativeTests.Player, PlayerName));
                NarrativeNpc rival = world.Registry.Add(new NarrativeNpc(world.NewId("npc"), RivalName));
                vanilla.Define(rival.Id, zone: SiteGenesis.ZoneOf(world.Registry.GetSite(plan.SiteId)));
                return new Fixture(world, vanilla, plan, rival.Id);
            }

            internal WorldEvent Record(WorldEventType type, EntityId actor, EntityId target, double magnitude)
            {
                return World.Record(type, actor, target, Vanilla.Now, magnitude, Zone);
            }

            /// <summary>An act nobody, the player included, was there for.</summary>
            internal void Unnoticed(WorldEventType type, EntityId actor, EntityId target)
            {
                World.Record(type, actor, target, Vanilla.Now, 0.6, Zone, tags: new[] { EventTags.Unnoticed });
            }

            /// <summary>Repeated violence between the player and one person, and the tie it left.</summary>
            internal void Feud()
            {
                Record(WorldEventType.Attacked, RivalId, Player, 0.7);
                Record(WorldEventType.Harmed, Player, RivalId, 0.7);
                Record(WorldEventType.Threatened, RivalId, Player, 0.6);
                World.Relationships.Connect(RivalId, Player, RelationKind.Enemy, -60);
            }

            /// <summary>Somebody the player pulled out of something.</summary>
            internal void Rescue()
            {
                Record(WorldEventType.Rescued, Player, KeeperId, 0.9);
            }

            internal void Hurt(EntityId who, double magnitude)
            {
                Record(WorldEventType.Harmed, Player, who, magnitude);
            }

            /// <summary>The player gets past whatever was holding the place shut.</summary>
            internal void Clear()
            {
                World.Record(WorldEventType.SiteCleared, Player, SiteId, Vanilla.Now, 0.6, Zone);
            }

            internal void SaveTheShop()
            {
                Assert.True(new BusinessContinuity(World).TryChangeState(
                    BusinessId, BusinessContinuityState.Recovered, Vanilla.Now, actor: Player));
            }

            /// <summary>An ending the player carried out themselves, which is what BQ-034 records.</summary>
            internal void FinishAMatter()
            {
                NarrativeThread thread = World.Threads[0];
                Assert.NotNull(ThreadResolution.Resolve(World, thread, "smuggling_settled", Player, Vanilla.Now));
            }

            private static SitePlan Plan(NarrativeWorldState world, NarrativeThread thread)
            {
                SitePlan plan = new SitePlan(world.NewId("zone"), SiteName, SmugglerCache, thread.Id)
                {
                    DangerLevel = 3,
                    Seed = 117
                };

                plan.Occupants.Add(Occupant(world, KeeperName, "keeper"));
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

        private static readonly EntityId Player = EntityId.Parse("npc_player");
    }
}
