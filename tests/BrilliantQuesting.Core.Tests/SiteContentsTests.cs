using System;
using System.Collections.Generic;
using System.IO;
using BrilliantQuesting.Content;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-091. What a place holds comes from what happened, not from what places of that kind
    /// usually hold.
    ///
    /// The step's done-when is a negative as much as a positive - contents derivable from the
    /// situation's state, *with no template chest* - so these tests hold both edges. The positive
    /// ones prove the goods in the camp are the goods that were taken and the people in it are the
    /// crew that took them. The negative ones prove the derivation would rather come up short than
    /// invent: an object nobody is carrying is not there, a pen with nobody to put in it stays
    /// empty, a crew the player has killed is not refilled, and a matter that leaves nothing behind
    /// cannot furnish a place at all.
    /// </summary>
    public class SiteContentsTests
    {
        private const string CampGrammar = "site.bandit_camp";

        /// <summary>
        /// The done-when. Every occupant and every object in the place is traceable to something
        /// the world recorded, and the place ends up holding exactly what the matter left there.
        /// </summary>
        [Fact]
        public void ThePlaceHoldsWhatTheMatterLeftThereAndNothingElse()
        {
            Fixture fixture = Fixture.ATheftByACrew();
            SiteContentsReading contents = fixture.Derive();

            Assert.True(contents.Furnished, string.Join("; ", contents.Refusals));

            // Everything in it is something the world already had. Nothing was minted to fill the
            // place out, which is the whole of "no template chest".
            for (int i = 0; i < contents.Cargo.Count; i++)
            {
                Assert.Contains(contents.Cargo[i].Item.Id, fixture.KnownObjects);
                Assert.False(contents.Cargo[i].Because.IsNone);
            }

            for (int i = 0; i < contents.Occupants.Count; i++)
            {
                Assert.NotNull(fixture.World.Registry.GetNpc(contents.Occupants[i].Id));
                Assert.False(contents.Occupants[i].Because.IsNone);
            }

            // The one object this matter's history says was taken and never given back.
            Assert.Equal(new[] { fixture.StolenId }, Ids(contents.Cargo));
            Assert.Equal(SiteKeeping.Taken, contents.Cargo[0].Keeping);
            Assert.Equal(fixture.ThiefId, contents.Cargo[0].HolderId);
            Assert.Equal(fixture.TheftFactId, contents.Cargo[0].EvidenceForFact);

            // And the place ends up with that manifest, through the ordinary genesis path.
            NarrativeSite site = fixture.Establish(contents);
            Assert.Contains(fixture.StolenId, site.ImportantObjectIds);
            Assert.Contains(fixture.ThiefId, site.OccupantIds);
        }

        /// <summary>
        /// The people this matter happened *to* are not in the hideout. They are on the thread, the
        /// ledger names them, and none of that puts them behind the camp's gate.
        /// </summary>
        [Fact]
        public void ThePeopleTheMatterWasDoneToAreNotAtThePlace()
        {
            Fixture fixture = Fixture.ATheftByACrew();
            SiteContentsReading contents = fixture.Derive();

            Assert.Contains(fixture.VictimId, fixture.Thread.ParticipantIds);
            Assert.Contains(fixture.WitnessId, fixture.Thread.ParticipantIds);
            Assert.DoesNotContain(fixture.VictimId, Ids(contents.Occupants));
            Assert.DoesNotContain(fixture.WitnessId, Ids(contents.Occupants));
        }

        /// <summary>
        /// The crew is the crew. Kill two of them and the camp has two fewer people in it; nothing
        /// is generated to keep the numbers up (`LW §7.8`).
        /// </summary>
        [Fact]
        public void AClearedGroupIsNotRefilled()
        {
            Fixture fixture = Fixture.ATheftByACrew();
            int before = fixture.Derive().Occupants.Count;

            fixture.Vanilla.Kill(fixture.CrewIds[1]);
            fixture.Vanilla.Kill(fixture.CrewIds[2]);
            SiteContentsReading after = fixture.Derive();

            Assert.Equal(before - 2, after.Occupants.Count);
            Assert.DoesNotContain(fixture.CrewIds[1], Ids(after.Occupants));
            Assert.Contains(after.Omitted, o => o.Id == fixture.CrewIds[1] && o.Reason.Contains("dead"));
        }

        /// <summary>
        /// An unread life state is not a death. A build that cannot answer for somebody leaves them
        /// where the matter put them rather than quietly emptying the place (`D017`).
        /// </summary>
        [Fact]
        public void SomebodyTheBuildCannotAnswerForIsStillAtThePlace()
        {
            Fixture fixture = Fixture.ATheftByACrew();

            // The crew member nobody has ever staged: the sandbox has no record of them at all, so
            // its life state reads Unknown rather than Alive.
            Assert.Equal(VanillaLifeState.Unknown, fixture.Vanilla.GetLifeState(fixture.UnbodiedCrewId));
            Assert.Contains(fixture.UnbodiedCrewId, Ids(fixture.Derive().Occupants));
        }

        /// <summary>
        /// History's claim about an object is not proof the object is here. If the one who took it
        /// is not carrying it any more, the place does not keep it.
        /// </summary>
        [Fact]
        public void AnObjectNobodyAtThePlaceIsCarryingIsNotHere()
        {
            Fixture fixture = Fixture.ATheftByACrew();
            fixture.Vanilla.TryTransferItem(fixture.StolenId, fixture.ThiefId, fixture.VictimId);

            SiteContentsReading contents = fixture.Derive();

            Assert.Empty(contents.Cargo);
            Assert.Contains(contents.Omitted, o => o.Id == fixture.StolenId);
            Assert.False(contents.Furnished);
            Assert.Contains(contents.Refusals, r => r.Contains("leaves nothing here"));
        }

        /// <summary>
        /// Contents track state rather than freezing it. Give the goods back and the camp has
        /// nothing worth keeping, which is a refusal rather than a place full of substitutes.
        /// </summary>
        [Fact]
        public void GivingItBackLeavesThePlaceWithNothingToKeep()
        {
            Fixture fixture = Fixture.ATheftByACrew();
            Assert.True(fixture.Derive().Furnished);

            fixture.World.Record(
                WorldEventType.ItemReturned,
                fixture.ThiefId,
                fixture.VictimId,
                fixture.Vanilla.Now,
                evidence: new[] { fixture.StolenId },
                threadId: fixture.Thread.Id);

            SiteContentsReading contents = fixture.Derive();
            Assert.Empty(contents.Cargo);
            Assert.False(contents.Furnished);
        }

        /// <summary>
        /// Somebody held is kept where the plan can hold them - and where it cannot, they are not
        /// kept here at all rather than being filed into a room that does not exist.
        /// </summary>
        [Fact]
        public void SomebodyHeldGoesWhereThePlanCanHoldThemAndNowhereElse()
        {
            Fixture fixture = Fixture.ATheftByACrew();
            fixture.Capture(fixture.WitnessId);

            SiteLayout withCell = fixture.PlanWith(SiteAffordance.PrisonCell, true);
            SiteContentsReading kept = fixture.Derive(withCell);
            SiteOccupancy captive = Single(kept.Occupants, fixture.WitnessId);

            Assert.Equal(SitePresence.Held, captive.Presence);
            Assert.NotEqual(string.Empty, captive.NodeId);
            Assert.Contains(
                withCell.Nodes,
                n => n.Id == captive.NodeId && Contains(n.Affordances, SiteAffordance.PrisonCell));

            SiteContentsReading nowhere = fixture.Derive(fixture.PlanWith(SiteAffordance.PrisonCell, false));
            Assert.DoesNotContain(fixture.WitnessId, Ids(nowhere.Occupants));
            Assert.Contains(nowhere.Omitted, o => o.Id == fixture.WitnessId && o.Reason.Contains("nowhere to hold"));
        }

        /// <summary>
        /// The template chest, refused by name. A plan that can hold somebody and a matter that
        /// holds nobody produce an empty room and a line saying so.
        /// </summary>
        [Fact]
        public void APartOfThePlanNothingFillsStaysEmpty()
        {
            Fixture fixture = Fixture.ATheftByACrew();
            SiteLayout withCell = fixture.PlanWith(SiteAffordance.PrisonCell, true);

            SiteContentsReading contents = fixture.Derive(withCell);

            Assert.DoesNotContain(contents.Occupants, o => o.Presence == SitePresence.Held);
            Assert.Contains(contents.Vacant, v => v.Affordance == SiteAffordance.PrisonCell);
            Assert.Contains("empty", NarrativeInspector.DescribeSiteContents(fixture.World, contents));
        }

        /// <summary>
        /// Genesis binds somebody the game already has instead of building a second one, and builds
        /// the one it does not have. Vanilla owns embodiment (`D021`); so does the inventory the
        /// stolen goods are already in.
        /// </summary>
        [Fact]
        public void GenesisBindsWhoTheGameHasAndBuildsWhoItDoesNot()
        {
            Fixture fixture = Fixture.ATheftByACrew();
            SiteContentsReading contents = fixture.Derive();

            EntityId thiefWas = fixture.Vanilla.GetZoneOf(fixture.ThiefId);
            int carriedBefore = fixture.Vanilla.GetInventory(fixture.ThiefId).Count;

            NarrativeSite site = fixture.Establish(contents);

            // Bound, not rebuilt: the thief is where they were, holding one copy of the goods.
            Assert.Equal(thiefWas, fixture.Vanilla.GetZoneOf(fixture.ThiefId));
            Assert.Equal(carriedBefore, fixture.Vanilla.GetInventory(fixture.ThiefId).Count);

            // Built: the crew member the game never had is now standing in the place.
            Assert.Equal(SiteGenesis.ZoneOf(site), fixture.Vanilla.GetZoneOf(fixture.UnbodiedCrewId));
        }

        /// <summary>With nothing to ask about the world, nothing is claimed to be in the place.</summary>
        [Fact]
        public void WithNoReadOfTheWorldNothingIsClaimedToBeHere()
        {
            Fixture fixture = Fixture.ATheftByACrew();
            SiteContentsReading contents = SiteContents.Derive(
                fixture.World, fixture.Thread.Id, fixture.Layout, null);

            Assert.False(contents.Furnished);
            Assert.Empty(contents.Occupants);
            Assert.Empty(contents.Cargo);
        }

        /// <summary>A matter nobody has is not a place. Genesis is never asked.</summary>
        [Fact]
        public void AMatterTheWorldDoesNotHaveFurnishesNothing()
        {
            Fixture fixture = Fixture.ATheftByACrew();
            SiteContentsReading contents = SiteContents.Derive(
                fixture.World, EntityId.Parse("thread_nowhere"), fixture.Layout, fixture.Vanilla);

            Assert.False(contents.Furnished);
            Assert.Null(contents.Thread);
        }

        /// <summary>
        /// The same state derives the same contents in the same order. A place whose cast depended
        /// on dictionary order would come back differently after a reload.
        /// </summary>
        [Fact]
        public void TheSameStateDerivesTheSameContents()
        {
            Fixture fixture = Fixture.ATheftByACrew();

            Assert.Equal(Ids(fixture.Derive().Occupants), Ids(fixture.Derive().Occupants));
            Assert.Equal(Ids(fixture.Derive().Cargo), Ids(fixture.Derive().Cargo));
        }

        /// <summary>
        /// A place stays small (BQ-087). A crew bigger than the place takes is reported down to
        /// the people history implicates rather than trimmed wherever the list happened to end.
        /// </summary>
        [Fact]
        public void ACrewBiggerThanThePlaceIsReportedRatherThanTruncatedSilently()
        {
            Fixture fixture = Fixture.ATheftByACrew();
            fixture.EnlargeCrew(SiteGenesis.MaximumOccupants + 3);

            SiteContentsReading contents = fixture.Derive();

            Assert.Equal(SiteGenesis.MaximumOccupants, contents.Occupants.Count);
            Assert.Equal(fixture.ThiefId, contents.Occupants[0].Id);
            Assert.Contains(contents.Omitted, o => o.Reason.Contains("at most"));
        }

        // -- fixture -------------------------------------------------------------------------

        private static List<EntityId> Ids(IReadOnlyList<SiteOccupancy> occupants)
        {
            List<EntityId> ids = new List<EntityId>();
            for (int i = 0; i < occupants.Count; i++)
            {
                ids.Add(occupants[i].Id);
            }

            return ids;
        }

        private static List<EntityId> Ids(IReadOnlyList<SiteHolding> cargo)
        {
            List<EntityId> ids = new List<EntityId>();
            for (int i = 0; i < cargo.Count; i++)
            {
                ids.Add(cargo[i].Item.Id);
            }

            return ids;
        }

        private static SiteOccupancy Single(IReadOnlyList<SiteOccupancy> occupants, EntityId who)
        {
            for (int i = 0; i < occupants.Count; i++)
            {
                if (occupants[i].Id == who)
                {
                    return occupants[i];
                }
            }

            throw new InvalidOperationException(who.Value + " is not at the place");
        }

        private static bool Contains(IReadOnlyList<SiteAffordance> affordances, SiteAffordance affordance)
        {
            for (int i = 0; i < affordances.Count; i++)
            {
                if (affordances[i] == affordance)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// A theft a crew committed: the goods are still in the thief's hands, the crew is real,
        /// and the people it was done to are elsewhere. Everything the derivation reads is written
        /// through the ordinary authorities - the ledger, the knowledge graph, the registry and a
        /// live inventory - so nothing here is a shortcut past the thing being tested.
        /// </summary>
        private sealed class Fixture
        {
            private readonly SiteGrammar _grammar;
            private Organization _crew;

            private Fixture(NarrativeWorldState world, SandboxVanillaState vanilla, SiteGrammar grammar)
            {
                World = world;
                Vanilla = vanilla;
                _grammar = grammar;
                CrewIds = new List<EntityId>();
                KnownObjects = new List<EntityId>();
            }

            internal NarrativeWorldState World { get; private set; }

            internal SandboxVanillaState Vanilla { get; }

            internal NarrativeThread Thread { get; private set; }

            internal SiteLayout Layout { get; private set; }

            internal EntityId ThiefId { get; private set; }

            internal EntityId VictimId { get; private set; }

            internal EntityId WitnessId { get; private set; }

            internal EntityId StolenId { get; private set; }

            internal EntityId TheftFactId { get; private set; }

            /// <summary>A crew member the game has never built. Their life state reads Unknown.</summary>
            internal EntityId UnbodiedCrewId { get; private set; }

            internal List<EntityId> CrewIds { get; }

            internal List<EntityId> KnownObjects { get; }

            internal static Fixture ATheftByACrew()
            {
                NarrativeWorldState world = new NarrativeWorldState(91);
                SandboxVanillaState vanilla = new SandboxVanillaState(EntityId.Parse("npc_player"));
                Fixture fixture = new Fixture(world, vanilla, Grammar());

                EntityId town = world.NewId("zone");

                fixture.ThiefId = fixture.Person("Renn", town);
                fixture.VictimId = fixture.Person("Mab", town);
                fixture.WitnessId = fixture.Person("Coll", town);

                // The crew: two the game has built, one it has not.
                fixture.CrewIds.Add(fixture.ThiefId);
                fixture.CrewIds.Add(fixture.Person("Bryn", town));
                fixture.UnbodiedCrewId = fixture.Unbodied("Tace");
                fixture.CrewIds.Add(fixture.UnbodiedCrewId);

                fixture._crew = world.Registry.Add(
                    new Organization(world.NewId("org"), "the road crew", "criminal_crew")
                    {
                        LeaderId = fixture.ThiefId
                    });
                for (int i = 0; i < fixture.CrewIds.Count; i++)
                {
                    fixture._crew.MemberIds.Add(fixture.CrewIds[i]);
                    world.Registry.GetNpc(fixture.CrewIds[i]).OrganizationIds.Add(fixture._crew.Id);
                }

                // What was taken, and one thing that was not: the derivation has to tell them apart.
                fixture.StolenId = fixture.Object(fixture.ThiefId, "a banded strongbox", 400);
                fixture.Object(fixture.VictimId, "a bill of lading", 5);

                WorldEvent theft = world.Record(
                    WorldEventType.Theft,
                    fixture.ThiefId,
                    fixture.VictimId,
                    vanilla.Now,
                    magnitude: 0.6,
                    zone: town,
                    witnesses: new[] { fixture.WitnessId },
                    evidence: new[] { fixture.StolenId });

                Fact stole = new Fact(
                    world.NewId("fact"),
                    fixture.ThiefId,
                    "stole",
                    fixture.StolenId,
                    string.Empty,
                    TruthState.True,
                    secrecy: 60,
                    originEvent: theft.Id);
                stole.EvidenceIds.Add(fixture.StolenId);
                world.Knowledge.AddFact(stole);
                fixture.TheftFactId = stole.Id;

                NarrativeThread thread = new NarrativeThread(world.NewId("thread"), "road_crew", vanilla.Now)
                {
                    State = ThreadState.Active,
                    OriginEventId = theft.Id
                };
                thread.FactIds.Add(stole.Id);
                thread.ParticipantIds.Add(fixture.ThiefId);
                thread.ParticipantIds.Add(fixture.VictimId);
                thread.ParticipantIds.Add(fixture.WitnessId);
                world.Threads.Add(thread);
                fixture.Thread = thread;

                fixture.Layout = fixture._grammar.Compose(3);
                return fixture;
            }

            internal SiteContentsReading Derive()
            {
                return Derive(Layout);
            }

            internal SiteContentsReading Derive(SiteLayout layout)
            {
                return SiteContents.Derive(World, Thread.Id, layout, Vanilla);
            }

            /// <summary>
            /// The first seed whose plan does, or does not, have a part answering this requirement.
            /// Searched rather than hardcoded, so a corrected grammar moves the seed instead of
            /// breaking the test's claim.
            /// </summary>
            internal SiteLayout PlanWith(SiteAffordance affordance, bool wanted)
            {
                for (ulong seed = 0; seed < 200; seed++)
                {
                    SiteLayout layout = _grammar.Compose(seed);
                    bool has = false;
                    for (int i = 0; i < layout.Nodes.Count; i++)
                    {
                        has |= Contains(layout.Nodes[i].Affordances, affordance);
                    }

                    if (has == wanted)
                    {
                        return layout;
                    }
                }

                throw new InvalidOperationException(
                    _grammar.Id + " has no seed " + (wanted ? "with " : "without ") + affordance);
            }

            internal void Capture(EntityId who)
            {
                World.Record(
                    WorldEventType.Captured,
                    ThiefId,
                    who,
                    Vanilla.Now,
                    magnitude: 0.7,
                    threadId: Thread.Id);
            }

            internal void EnlargeCrew(int size)
            {
                while (_crew.MemberIds.Count < size)
                {
                    EntityId id = Unbodied("hand " + _crew.MemberIds.Count);
                    _crew.MemberIds.Add(id);
                    World.Registry.GetNpc(id).OrganizationIds.Add(_crew.Id);
                }
            }

            internal NarrativeSite Establish(SiteContentsReading contents)
            {
                SitePlan plan = Layout.NewPlan(World.NewId("zone"), "the camp off the drove road", Thread.Id);
                contents.ApplyTo(plan);

                SiteGenesisResult result = SiteGenesis.Establish(
                    World, plan, new SandboxStager(Vanilla), Vanilla.Now);
                Assert.True(result.Created, string.Join("; ", result.Reasons));
                return result.Site;
            }

            private EntityId Person(string name, EntityId zone)
            {
                NarrativeNpc npc = World.Registry.Add(new NarrativeNpc(World.NewId("npc"), name));
                Vanilla.Define(npc.Id, level: 3, money: 20, zone: zone);
                return npc.Id;
            }

            private EntityId Unbodied(string name)
            {
                return World.Registry.Add(new NarrativeNpc(World.NewId("npc"), name)).Id;
            }

            private EntityId Object(EntityId holder, string name, int value)
            {
                ItemDescriptor item = new ItemDescriptor(World.NewId("item"), name, "goods", value, "chest");
                Vanilla.GiveItem(holder, item);
                KnownObjects.Add(item.Id);
                return item.Id;
            }

            private static SiteGrammar Grammar()
            {
                ContentBundleLoadResult loaded = ContentBundleLoader.LoadFile(
                    Path.Combine(RepositoryRoot(), "Package", "content.bqc"));
                Assert.Empty(loaded.Diagnostics);

                IReadOnlyList<ContentDiagnostic> diagnostics;
                SiteGrammarLibrary library = SiteGrammarContent.CreateLibrary(loaded.Bundle, out diagnostics);
                Assert.Empty(diagnostics);

                SiteGrammar grammar = library.Get(CampGrammar);
                Assert.NotNull(grammar);
                return grammar;
            }

            private static string RepositoryRoot()
            {
                DirectoryInfo directory = new DirectoryInfo(Directory.GetCurrentDirectory());
                while (directory != null && !File.Exists(Path.Combine(directory.FullName, "ElinBrilliantQuesting.sln")))
                {
                    directory = directory.Parent;
                }

                if (directory == null)
                {
                    throw new InvalidOperationException("Could not locate repository root.");
                }

                return directory.FullName;
            }
        }
    }
}
