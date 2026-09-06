using System;
using System.Collections.Generic;
using System.IO;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Content;
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
    /// BQ-143. One place that already exists is given one more piece, once.
    ///
    /// The claim under test is narrow and every half of it matters: a mine the crew are holding
    /// gains a powder store off its workings, and that store is there after a save, after a reload,
    /// after a fortnight in game and after a second save - exactly once, however many times anybody
    /// asks for it. Nothing else about the place changes: not who is in it, not what it keeps, not
    /// how it is reached, not what it is, and not what any other place is.
    ///
    /// The refusals are the other half of the proof and there are more tests of them than of the
    /// success, deliberately. An additive mutation that cannot be refused is a mutation that will
    /// eventually be applied on top of somebody's cellar.
    /// </summary>
    public class SiteMutationTests
    {
        private const SiteAffordance Errand = SiteAffordance.EvidenceCache;

        /// <summary>The part of a mine that every seed has, and that the crew cut their store off.</summary>
        private const string Workings = "workings";

        private const string StoreId = "mine.powder_store";

        private const string StorePiece = "piece.mine.tool_niche";

        // -- applying it once --------------------------------------------------------------------

        /// <summary>
        /// The store is cut, it stands on ground nothing was on, and the place says it has it.
        /// </summary>
        [Fact]
        public void OneAdditionIsAppliedToAPlaceThatAlreadyExists()
        {
            Fixture fixture = Fixture.AMineTheCrewAreHolding();
            SiteStructure before = fixture.Shape();

            SiteMutationResult result = fixture.AddStore();

            Assert.Equal(SiteMutationOutcome.Applied, result.Outcome);
            Assert.True(result.Changed);
            Assert.Empty(result.Refusals);

            SiteAddition addition = result.Addition;
            Assert.Equal(StoreId, addition.AdditionId);
            Assert.Equal(StorePiece, addition.PieceId);
            Assert.Equal(Workings, addition.AnchorNodeId);
            Assert.Equal(fixture.Vanilla.Now, addition.AppliedAt);
            Assert.NotEqual(string.Empty, addition.ExternalRef);

            // The adapter was asked to add exactly one thing, and it was this one.
            Assert.Single(fixture.Stager.Added);

            // The ground it took is ground the place was not already standing on.
            for (int i = 0; i < before.Placements.Count; i++)
            {
                SitePlacement placement = before.Placements[i];
                Assert.False(
                    addition.X < placement.Right && placement.X < addition.X + addition.Width
                    && addition.Y < placement.Bottom && placement.Y < addition.Y + addition.Height,
                    "the store was cut into " + placement.NodeId);
            }
        }

        /// <summary>
        /// The shape of the place afterwards is the shape it had plus one part, reachable from the
        /// part it opens off, and with nothing else moved, dropped or made harder to get to.
        /// </summary>
        [Fact]
        public void TheAdditionIsOneMorePartAndEverythingElseIsWhereItWas()
        {
            Fixture fixture = Fixture.AMineTheCrewAreHolding();
            SiteStructure before = fixture.Shape();
            SiteTopology wasLike = before.Topology();

            fixture.AddStore();
            SiteStructure after = fixture.Shape();

            Assert.Equal(before.Placements.Count + 1, after.Placements.Count);
            Assert.Equal(Ground(before), Intersect(Ground(before), Ground(after)));

            SitePlacement store = after.PlacementOf("added:" + StoreId);
            Assert.NotNull(store);
            Assert.Equal(StorePiece, store.Piece.Id);
            Assert.Contains("added:" + StoreId, after.Walkable());

            // What the place asks of somebody walking to what it keeps has not changed. An addition
            // that made the mine easier or harder would be a redesign, not an addition.
            SiteTopology isLike = after.Topology();
            Assert.Equal(wasLike.Obstacles, isLike.Obstacles);
            Assert.Equal(wasLike.WaysToObjective, isLike.WaysToObjective);
            Assert.Equal(wasLike.DistinctProblems, isLike.DistinctProblems);
        }

        // -- attempting it twice -----------------------------------------------------------------

        /// <summary>
        /// Asked for the same store again, the place says it already has it, the adapter is not
        /// asked, and there is still one store.
        /// </summary>
        [Fact]
        public void AskingForTheSameAdditionAgainChangesNothing()
        {
            Fixture fixture = Fixture.AMineTheCrewAreHolding();
            SiteAddition first = fixture.AddStore().Addition;

            fixture.Vanilla.AdvanceDays(3);
            SiteMutationResult again = fixture.AddStore();

            Assert.Equal(SiteMutationOutcome.AlreadyApplied, again.Outcome);
            Assert.False(again.Changed);
            Assert.Same(first, again.Addition);
            Assert.Single(fixture.Site.Additions);
            Assert.Single(fixture.Stager.Added);

            // The record still says when it was actually cut, not when somebody last asked.
            Assert.Equal(first.AppliedAt, fixture.Site.Additions[0].AppliedAt);
        }

        /// <summary>
        /// Asked ten times, in a loop, the way a caller with no memory would ask. Still one store,
        /// and the adapter was still asked once.
        /// </summary>
        [Fact]
        public void AskingRepeatedlyStillLeavesOneAddition()
        {
            Fixture fixture = Fixture.AMineTheCrewAreHolding();
            for (int i = 0; i < 10; i++)
            {
                fixture.AddStore();
                fixture.Vanilla.AdvanceDays(1);
            }

            Assert.Single(fixture.Site.Additions);
            Assert.Single(fixture.Stager.Added);
            Assert.Single(fixture.Shape().Placements, p => p.NodeId == "added:" + StoreId);
        }

        /// <summary>
        /// The idempotency check comes before the build is asked anything, so a save that moves to
        /// a build which has lost the capability still knows the store is there and still refuses
        /// to cut a second one.
        /// </summary>
        [Fact]
        public void AnAdditionAlreadyAppliedIsStillAlreadyAppliedOnABuildThatCouldNotApplyIt()
        {
            Fixture fixture = Fixture.AMineTheCrewAreHolding();
            fixture.AddStore();

            fixture.Vanilla.SetCapability(VanillaCapability.AddPlaceFixture, false);
            SiteMutationResult again = fixture.AddStore();

            Assert.Equal(SiteMutationOutcome.AlreadyApplied, again.Outcome);
            Assert.Single(fixture.Site.Additions);
            Assert.Single(fixture.Stager.Added);
        }

        // -- save and reload ---------------------------------------------------------------------

        /// <summary>
        /// The done-when, in one test: save, reload, a fortnight in game, save and reload again.
        /// The store is there after each, once, on the same ground, and asking for it again after
        /// all of that still adds nothing.
        /// </summary>
        [Fact]
        public void TheAdditionSurvivesSaveReloadElapsedDaysAndASecondSaveReload()
        {
            Fixture fixture = Fixture.AMineTheCrewAreHolding();
            SiteAddition cut = fixture.AddStore().Addition;

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(fixture.World));
            AssertOneStore(fixture, reloaded, cut);

            fixture.Vanilla.AdvanceDays(14);

            NarrativeWorldState twice = WorldStateSerializer.Load(WorldStateSerializer.Save(reloaded));
            AssertOneStore(fixture, twice, cut);

            // And a caller that asks again on the reloaded world still adds nothing, because the
            // record it reads is the one that came out of the save.
            SiteMutationResult again = fixture.AddStore(twice);
            Assert.Equal(SiteMutationOutcome.AlreadyApplied, again.Outcome);
            Assert.Single(twice.Registry.GetSite(fixture.SiteId).Additions);
            Assert.Single(fixture.Stager.Added);
        }

        /// <summary>
        /// A save written before anything could be added to a place has no additions node. It loads,
        /// the place is unchanged, and it reads back as a place nobody has added to - which is the
        /// truth about it.
        /// </summary>
        [Fact]
        public void ASaveWithNoAdditionsNodeLoadsAsAPlaceNobodyHasAddedTo()
        {
            Fixture fixture = Fixture.AMineTheCrewAreHolding();
            fixture.AddStore();

            string legacy = WorldStateSerializer.Save(fixture.World).Replace("\"additions\"", "\"gone_additions\"");
            NarrativeSite site = WorldStateSerializer.Load(legacy).Registry.GetSite(fixture.SiteId);

            Assert.Empty(site.Additions);
            Assert.True(site.Established);
            Assert.Equal(fixture.Site.Name, site.Name);
            Assert.Equal(fixture.Site.VanillaZoneRef, site.VanillaZoneRef);
            Assert.Equal(fixture.Site.OccupantIds, site.OccupantIds);
            Assert.Equal(fixture.Site.ImportantObjectIds, site.ImportantObjectIds);
        }

        // -- mutation identity -------------------------------------------------------------------

        /// <summary>
        /// The name is the identity. A second addition with a different name off the same part is
        /// refused, because renaming a thing does not make it a different thing to cut.
        /// </summary>
        [Fact]
        public void ASecondAdditionOffTheSamePartIsRefusedHoweverItIsNamed()
        {
            Fixture fixture = Fixture.AMineTheCrewAreHolding();
            fixture.AddStore();

            SiteMutationResult renamed = fixture.Add("mine.second_store", StorePiece, Workings);

            Assert.Equal(SiteMutationOutcome.Refused, renamed.Outcome);
            Assert.Contains(renamed.Refusals, r => r.Contains("already carries the addition " + StoreId));
            Assert.Single(fixture.Site.Additions);
            Assert.Single(fixture.Stager.Added);
        }

        /// <summary>An addition nobody can name is refused before anything else is looked at.</summary>
        [Fact]
        public void AnAdditionWithNoNameIsRefused()
        {
            Fixture fixture = Fixture.AMineTheCrewAreHolding();

            SiteMutationResult result = fixture.Add(string.Empty, StorePiece, Workings);

            Assert.Equal(SiteMutationOutcome.Refused, result.Outcome);
            Assert.Contains(result.Refusals, r => r.Contains("no name"));
            Assert.Empty(fixture.Site.Additions);
            Assert.Empty(fixture.Stager.Added);
        }

        /// <summary>
        /// An addition's part id cannot collide with a part the grammar named, so a grammar that
        /// later grows a node called after somebody's addition does not silently absorb it.
        /// </summary>
        [Fact]
        public void AnAdditionIsNeverMistakenForAPartOfThePlan()
        {
            Fixture fixture = Fixture.AMineTheCrewAreHolding();
            fixture.Add(Workings, StorePiece, Workings);

            SiteStructure shape = fixture.Shape();

            Assert.Equal(Workings, shape.PlacementOf(Workings).NodeId);
            Assert.NotEqual(StorePiece, shape.PlacementOf(Workings).Piece.Id);
            Assert.NotNull(shape.PlacementOf("added:" + Workings));
        }

        /// <summary>
        /// The same addition applied to two different places is two additions, one per place. The
        /// name identifies the mutation; the site identifies which place it happened to.
        /// </summary>
        [Fact]
        public void TheSameAdditionCanBeAppliedToTwoDifferentPlaces()
        {
            Fixture fixture = Fixture.AMineTheCrewAreHolding();
            fixture.AddStore();
            NarrativeSite other = fixture.ASecondMine();

            SiteMutationResult elsewhere = fixture.Add(StoreId, StorePiece, Workings, other.Id);

            Assert.Equal(SiteMutationOutcome.Applied, elsewhere.Outcome);
            Assert.Single(fixture.Site.Additions);
            Assert.Single(other.Additions);
            Assert.Equal(2, fixture.Stager.Added.Count);
        }

        // -- everything the mutation must not touch ----------------------------------------------

        /// <summary>
        /// The place is the same place afterwards: same identity, same body, same people, same
        /// cargo, same ways in, same establishment, and no history written. An addition is not
        /// something that happened to anybody.
        /// </summary>
        [Fact]
        public void AddingToAPlaceChangesNothingElseAboutIt()
        {
            Fixture fixture = Fixture.AMineTheCrewAreHolding();
            NarrativeSite site = fixture.Site;

            EntityId id = site.Id;
            string zoneRef = site.VanillaZoneRef;
            string grammar = site.GrammarId;
            ulong seed = site.GenerationSeed;
            string objective = site.Objective;
            GameTime established = site.EstablishedAt;
            List<EntityId> occupants = new List<EntityId>(site.OccupantIds);
            List<EntityId> cargo = new List<EntityId>(site.ImportantObjectIds);
            List<string> approaches = Describe(site.Approaches);
            int events = fixture.World.Ledger.Count;
            int staged = fixture.Stager.Built.Count;

            fixture.Vanilla.AdvanceDays(9);
            fixture.AddStore();

            Assert.Equal(id, site.Id);
            Assert.Equal(zoneRef, site.VanillaZoneRef);
            Assert.Equal(grammar, site.GrammarId);
            Assert.Equal(seed, site.GenerationSeed);
            Assert.Equal(objective, site.Objective);
            Assert.Equal(established, site.EstablishedAt);
            Assert.True(site.Established);
            Assert.Equal(occupants, site.OccupantIds);
            Assert.Equal(cargo, site.ImportantObjectIds);
            Assert.Equal(approaches, Describe(site.Approaches));

            // No event, and no second act of building: the place was made once and added to once.
            Assert.Equal(events, fixture.World.Ledger.Count);
            Assert.Equal(staged, fixture.Stager.Built.Count);
        }

        /// <summary>
        /// Who is standing in the mine and what they are holding is untouched, including a thing
        /// the player moved between two of them after it was made. The mutation reads the world and
        /// writes one piece; it does not reconcile anybody.
        /// </summary>
        [Fact]
        public void ThePeopleInThePlaceAndWhatTheyHoldAreUntouched()
        {
            Fixture fixture = Fixture.AMineTheCrewAreHolding();
            NarrativeSite site = fixture.Site;

            // Something the player did after the place was made: the strongbox changed hands
            // between two people who are both still there.
            EntityId newHolder = EntityId.None;
            for (int i = 0; i < site.OccupantIds.Count && newHolder.IsNone; i++)
            {
                if (site.OccupantIds[i] != fixture.ThiefId)
                {
                    newHolder = site.OccupantIds[i];
                }
            }

            Assert.True(fixture.Vanilla.TryTransferItem(fixture.StolenId, fixture.ThiefId, newHolder));

            // And the crew are standing in the mine, so a visit is a real reading of the place
            // rather than a reading of a zone nobody is in.
            fixture.GatherTheCrew();
            SiteVisit before = SiteGenesis.Visit(fixture.World, site.Id, fixture.Vanilla);
            Assert.True(before.Intact, "the mine was not intact before anything was added to it");

            fixture.AddStore();

            SiteVisit after = SiteGenesis.Visit(fixture.World, site.Id, fixture.Vanilla);
            Assert.True(after.Intact);
            Assert.Empty(after.MissingOccupants);
            Assert.Empty(after.MissingCargo);

            // Including the hand-over the player caused: cargo that changed hands between two
            // people who are both still there has not moved, and nothing re-staged the original.
            Assert.Contains(
                fixture.Vanilla.GetInventory(newHolder),
                item => item.Id == fixture.StolenId);
            Assert.DoesNotContain(
                fixture.Vanilla.GetInventory(fixture.ThiefId),
                item => item.Id == fixture.StolenId);
        }

        /// <summary>Another place in the same world is not touched by an addition to this one.</summary>
        [Fact]
        public void AnUnrelatedPlaceIsUntouched()
        {
            Fixture fixture = Fixture.AMineTheCrewAreHolding();
            NarrativeSite other = fixture.ASecondMine();

            string before = Describe(other);
            SiteStructure shapeBefore = fixture.Shape(other.Id);

            fixture.AddStore();

            Assert.Equal(before, Describe(other));
            Assert.Empty(other.Additions);
            Assert.Equal(
                Ground(shapeBefore),
                Ground(fixture.Shape(other.Id)));
        }

        // -- refusing when safe placement cannot be established ----------------------------------

        /// <summary>
        /// The build says the player has changed the ground on every side of the workings. Nothing
        /// is added, the adapter is never asked, and every patch that was looked at is reported.
        /// </summary>
        [Fact]
        public void GroundThePlayerHasChangedIsNeverBuiltOn()
        {
            Fixture fixture = Fixture.AMineTheCrewAreHolding();
            fixture.PaveAround(Workings, VanillaGround.PlayerChanged);

            SiteMutationResult result = fixture.AddStore();

            Assert.Equal(SiteMutationOutcome.Refused, result.Outcome);
            Assert.Contains(result.Refusals, r => r.Contains("no ground beside " + Workings));
            Assert.Contains(result.Considered, c => c.Contains("the player has changed this ground"));
            Assert.Empty(fixture.Site.Additions);
            Assert.Empty(fixture.Stager.Added);
        }

        /// <summary>
        /// Ground the build cannot read refuses exactly like ground it says is taken. An unread
        /// patch is not an empty one.
        /// </summary>
        [Fact]
        public void GroundTheBuildCannotReadIsNeverBuiltOn()
        {
            Fixture fixture = Fixture.AMineTheCrewAreHolding();
            fixture.PaveAround(Workings, VanillaGround.Unknown);

            SiteMutationResult result = fixture.AddStore();

            Assert.Equal(SiteMutationOutcome.Refused, result.Outcome);
            Assert.Contains(result.Considered, c => c.Contains("cannot tell what is there"));
            Assert.Empty(fixture.Site.Additions);
        }

        /// <summary>
        /// Ground something already stands on is skipped rather than refused: the store goes on the
        /// next side that is free, and the parts of the mine keep the ground they had.
        /// </summary>
        [Fact]
        public void AnOccupiedSideIsSkippedForOneThatIsFree()
        {
            Fixture fixture = Fixture.AMineTheCrewAreHolding();

            // The deep cut is the part of this mine with room on more than one side of it, so the
            // first side can be taken without that being the same thing as having nowhere to go.
            SitePlacement cut = fixture.Shape().PlacementOf("deeper_cut");
            fixture.Pave(cut.Right + 1, cut.Y, VanillaGround.Occupied);

            SiteMutationResult result = fixture.Add("mine.spoil_heap", StorePiece, "deeper_cut");

            Assert.Equal(SiteMutationOutcome.Applied, result.Outcome);
            Assert.Contains(result.Considered, c => c.Contains("something already stands there"));
            Assert.NotEqual(cut.Right + 1, result.Addition.X);
        }

        /// <summary>A build that cannot add a fixture is told so before anything is chosen.</summary>
        [Fact]
        public void ABuildThatCannotAddAFixtureIsRefusedBeforeAnythingIsChosen()
        {
            Fixture fixture = Fixture.AMineTheCrewAreHolding();
            fixture.Vanilla.SetCapability(VanillaCapability.AddPlaceFixture, false);

            SiteMutationResult result = fixture.AddStore();

            Assert.Equal(SiteMutationOutcome.Refused, result.Outcome);
            Assert.Contains(result.Refusals, r => r.Contains("AddPlaceFixture"));
            Assert.Empty(result.Considered);
            Assert.Empty(fixture.Site.Additions);
            Assert.Empty(fixture.Stager.Added);
        }

        /// <summary>
        /// An adapter that will not make the addition costs an addition that did not happen. The
        /// record is written after the adapter answers, never before, so nothing in the save claims
        /// a piece the map does not have.
        /// </summary>
        [Fact]
        public void AnAdapterThatRefusesLeavesNoRecordOfAnAddition()
        {
            Fixture fixture = Fixture.AMineTheCrewAreHolding();

            SiteMutationResult result = SiteMutation.Apply(
                fixture.World,
                fixture.SiteId,
                new SiteAdditionPlan(StoreId, StorePiece, Workings),
                fixture.Grammars,
                fixture.Pieces,
                fixture.Actions,
                fixture.Vanilla,
                new RefusingStager(),
                fixture.Vanilla.Now);

            Assert.Equal(SiteMutationOutcome.Refused, result.Outcome);
            Assert.Contains(result.Refusals, r => r.Contains("would not add"));
            Assert.Empty(fixture.Site.Additions);
        }

        /// <summary>A part the place does not have is nothing to cut an addition off.</summary>
        [Fact]
        public void APartThePlaceDoesNotHaveIsRefused()
        {
            Fixture fixture = Fixture.AMineTheCrewAreHolding();

            SiteMutationResult result = fixture.Add(StoreId, StorePiece, "counting_house");

            Assert.Equal(SiteMutationOutcome.Refused, result.Outcome);
            Assert.Contains(result.Refusals, r => r.Contains("no counting_house"));
            Assert.Empty(fixture.Site.Additions);
        }

        /// <summary>A piece the bundle does not carry is not built out of something similar.</summary>
        [Fact]
        public void APieceTheBundleDoesNotHaveIsRefused()
        {
            Fixture fixture = Fixture.AMineTheCrewAreHolding();

            SiteMutationResult result = fixture.Add(StoreId, "piece.mine.counting_house", Workings);

            Assert.Equal(SiteMutationOutcome.Refused, result.Outcome);
            Assert.Contains(result.Refusals, r => r.Contains("no authored piece"));
            Assert.Empty(fixture.Site.Additions);
        }

        /// <summary>
        /// A piece leaning on something this build cannot do is refused by the same gate that
        /// refuses it during realization - a hidden stope is not cut after the fact either.
        /// </summary>
        [Fact]
        public void APieceThisBuildCannotBuildIsRefused()
        {
            Fixture fixture = Fixture.AMineTheCrewAreHolding();

            // The stope leans on revealing a passage that has to be found, which is a read no
            // build here has (`ELIN-Q-0008`). It is refused during realization for this reason and
            // it is refused after the fact for the same one.
            fixture.Vanilla.SetCapability(VanillaCapability.ReadPlaceContents, false);

            SiteMutationResult result = fixture.Add(StoreId, "piece.mine.blind_stope", Workings);

            Assert.Equal(SiteMutationOutcome.Refused, result.Outcome);
            Assert.Contains(result.Refusals, r => r.Contains("cannot be built on this build"));
            Assert.Empty(fixture.Site.Additions);
        }

        /// <summary>
        /// A place genesis never made cannot be added to. Adding to one would be genesis under
        /// another name, without the plan genesis validates.
        /// </summary>
        [Fact]
        public void APlaceGenesisNeverMadeCannotBeAddedTo()
        {
            Fixture fixture = Fixture.AMineTheCrewAreHolding();
            NarrativeSite scenery = fixture.World.Registry.Add(
                new NarrativeSite(fixture.World.NewId("zone"), "the old drove road", "road")
                {
                    Persistence = SitePersistence.Persistent
                });

            SiteMutationResult result = fixture.Add(StoreId, StorePiece, Workings, scenery.Id);

            Assert.Equal(SiteMutationOutcome.Refused, result.Outcome);
            Assert.Contains(result.Refusals, r => r.Contains("never established"));
            Assert.Empty(scenery.Additions);
        }

        /// <summary>
        /// A throwaway interior is refused: it is rebuilt from nothing when it is next needed, so
        /// an addition to one is either lost or made again every time.
        /// </summary>
        [Fact]
        public void AThrowawayInteriorIsRefused()
        {
            Fixture fixture = Fixture.AMineTheCrewAreHolding();
            fixture.Site.Persistence = SitePersistence.Ephemeral;

            SiteMutationResult result = fixture.AddStore();

            Assert.Equal(SiteMutationOutcome.Refused, result.Outcome);
            Assert.Contains(result.Refusals, r => r.Contains("throwaway interior"));
            Assert.Empty(fixture.Site.Additions);
        }

        /// <summary>
        /// A place whose shape cannot be derived is a place where no patch of ground is known to be
        /// free, so nothing is added to it - the same refusal, for the same reason, as unreadable
        /// ground.
        /// </summary>
        [Fact]
        public void APlaceWhoseShapeCannotBeReadIsNeverAddedTo()
        {
            Fixture fixture = Fixture.AMineTheCrewAreHolding();
            fixture.Site.Objective = string.Empty;

            SiteMutationResult result = fixture.AddStore();

            Assert.Equal(SiteMutationOutcome.Refused, result.Outcome);
            Assert.Contains(result.Refusals, r => r.Contains("cannot be read"));
            Assert.Empty(fixture.Site.Additions);
            Assert.Empty(fixture.Stager.Added);
        }

        /// <summary>
        /// A place that has been added to still knows which of its ground the addition took after a
        /// bundle that no longer carries the piece is loaded - so a later addition cannot be cut
        /// into the store because content moved underneath the save.
        /// </summary>
        [Fact]
        public void GroundAnAdditionTookStaysSpokenForWhenTheBundleLosesThePiece()
        {
            Fixture fixture = Fixture.AMineTheCrewAreHolding();
            SiteAddition cut = fixture.AddStore().Addition;

            SitePlacement stranded = fixture.Shape(fixture.SiteId, fixture.PiecesWithout(StorePiece))
                .PlacementOf("added:" + StoreId);

            Assert.NotNull(stranded);
            Assert.Equal(cut.X, stranded.X);
            Assert.Equal(cut.Y, stranded.Y);
            Assert.Equal(cut.Width, stranded.Piece.Width);
            Assert.Equal(cut.Height, stranded.Piece.Height);
        }

        // -- helpers -------------------------------------------------------------------------------

        private static void AssertOneStore(Fixture fixture, NarrativeWorldState world, SiteAddition cut)
        {
            NarrativeSite site = world.Registry.GetSite(fixture.SiteId);

            Assert.Single(site.Additions);
            SiteAddition read = site.Additions[0];
            Assert.Equal(cut.AdditionId, read.AdditionId);
            Assert.Equal(cut.PieceId, read.PieceId);
            Assert.Equal(cut.AnchorNodeId, read.AnchorNodeId);
            Assert.Equal(cut.X, read.X);
            Assert.Equal(cut.Y, read.Y);
            Assert.Equal(cut.Width, read.Width);
            Assert.Equal(cut.Height, read.Height);
            Assert.Equal(cut.AppliedAt, read.AppliedAt);
            Assert.Equal(cut.ExternalRef, read.ExternalRef);

            // And the derived shape has it exactly once, on the ground the record names.
            SiteStructure shape = fixture.Shape(fixture.SiteId, fixture.Pieces, world);
            SitePlacement store = Assert.Single(shape.Placements, p => p.NodeId == "added:" + cut.AdditionId);
            Assert.Equal(cut.X, store.X);
            Assert.Equal(cut.Y, store.Y);
        }

        private static List<string> Ground(SiteStructure structure)
        {
            List<string> ground = new List<string>();
            for (int i = 0; i < structure.Placements.Count; i++)
            {
                SitePlacement placement = structure.Placements[i];
                ground.Add(placement.NodeId + "=" + placement.Piece.Id + "@" + placement.X + "," + placement.Y);
            }

            ground.Sort(StringComparer.Ordinal);
            return ground;
        }

        private static List<string> Intersect(List<string> first, List<string> second)
        {
            List<string> both = new List<string>();
            for (int i = 0; i < first.Count; i++)
            {
                if (second.Contains(first[i]))
                {
                    both.Add(first[i]);
                }
            }

            return both;
        }

        private static List<string> Describe(IReadOnlyList<SiteApproach> approaches)
        {
            List<string> described = new List<string>();
            for (int i = 0; i < approaches.Count; i++)
            {
                described.Add(approaches[i].ToString());
            }

            return described;
        }

        private static string Describe(NarrativeSite site)
        {
            return site.Id + "|" + site.Name + "|" + site.VanillaZoneRef + "|" + site.GrammarId + "|"
                   + site.GenerationSeed + "|" + site.Objective + "|" + site.Established + "|"
                   + site.EstablishedAt.TotalMinutes + "|" + string.Join(",", Describe(site.Approaches))
                   + "|" + site.OccupantIds.Count + "|" + site.ImportantObjectIds.Count;
        }

        /// <summary>An adapter that will not add anything, whatever it is asked for.</summary>
        private sealed class RefusingStager : ISituationStager
        {
            public void StageCharacter(EntityId id, CharacterBlueprint blueprint, EntityId zone)
            {
            }

            public void StageItem(EntityId owner, ItemDescriptor item)
            {
            }

            public string StageSite(SiteBlueprint blueprint) => string.Empty;

            public string ApplySiteAddition(SiteAdditionBlueprint blueprint) => string.Empty;
        }

        /// <summary>
        /// A theft by a crew, and the worked-out mine they took it to - established the way
        /// BQ-140 establishes one, and then left alone.
        /// </summary>
        private sealed class Fixture
        {
            private const ulong Seed = 4;

            private Fixture(NarrativeWorldState world, SandboxVanillaState vanilla)
            {
                World = world;
                Vanilla = vanilla;
                Stager = new SandboxStager(vanilla);
                Actions = StandardActions.CreateRegistry();

                ContentBundle bundle = Bundle();
                IReadOnlyList<ContentDiagnostic> diagnostics;
                Grammars = SiteGrammarContent.CreateLibrary(bundle, out diagnostics);
                Assert.Empty(diagnostics);
                Pieces = SitePieceContent.CreateCatalogue(bundle, ScenarioDungeon.Family, out diagnostics);
                Assert.Empty(diagnostics);
            }

            internal NarrativeWorldState World { get; }

            internal SandboxVanillaState Vanilla { get; }

            internal SandboxStager Stager { get; }

            internal ActionRegistry Actions { get; }

            internal SiteGrammarLibrary Grammars { get; }

            internal SitePieceCatalogue Pieces { get; }

            internal EntityId SiteId { get; private set; }

            internal EntityId ThiefId { get; private set; }

            internal EntityId StolenId { get; private set; }

            internal NarrativeThread Thread { get; private set; }

            internal NarrativeSite Site => World.Registry.GetSite(SiteId);

            internal static Fixture AMineTheCrewAreHolding()
            {
                NarrativeWorldState world = new NarrativeWorldState(143);
                SandboxVanillaState vanilla = new SandboxVanillaState(EntityId.Parse("npc_player"));
                Fixture fixture = new Fixture(world, vanilla);

                EntityId town = world.NewId("zone");
                fixture.ThiefId = fixture.Person("Renn", town);
                EntityId victim = fixture.Person("Mab", town);

                Organization crew = world.Registry.Add(
                    new Organization(world.NewId("org"), "the road crew", "criminal_crew")
                    {
                        LeaderId = fixture.ThiefId
                    });
                foreach (EntityId member in new[]
                         {
                             fixture.ThiefId, fixture.Person("Bryn", town), fixture.Person("Tace", town)
                         })
                {
                    crew.MemberIds.Add(member);
                    world.Registry.GetNpc(member).OrganizationIds.Add(crew.Id);
                }

                ItemDescriptor strongbox = new ItemDescriptor(
                    world.NewId("item"), "a banded strongbox", "goods", 400, "chest");
                vanilla.GiveItem(fixture.ThiefId, strongbox);
                fixture.StolenId = strongbox.Id;

                WorldEvent theft = world.Record(
                    WorldEventType.Theft,
                    fixture.ThiefId,
                    victim,
                    vanilla.Now,
                    magnitude: 0.6,
                    zone: town,
                    evidence: new[] { strongbox.Id });

                Fact stole = new Fact(
                    world.NewId("fact"),
                    fixture.ThiefId,
                    "stole",
                    strongbox.Id,
                    string.Empty,
                    TruthState.True,
                    secrecy: 60,
                    originEvent: theft.Id);
                stole.EvidenceIds.Add(strongbox.Id);
                world.Knowledge.AddFact(stole);

                NarrativeThread thread = new NarrativeThread(world.NewId("thread"), "road_crew", vanilla.Now)
                {
                    State = ThreadState.Active,
                    OriginEventId = theft.Id
                };
                thread.FactIds.Add(stole.Id);
                thread.ParticipantIds.Add(fixture.ThiefId);
                thread.ParticipantIds.Add(victim);
                world.Threads.Add(thread);
                fixture.Thread = thread;

                fixture.SiteId = fixture.Establish("the workings above the drove road", Seed).Site.Id;
                vanilla.AdvanceDays(2);
                return fixture;
            }

            /// <summary>A second mine, made the same way, that nothing in these tests adds to.</summary>
            internal NarrativeSite ASecondMine()
            {
                return Establish("the workings under the scarp", 9).Site;
            }

            internal SiteMutationResult AddStore()
            {
                return Add(StoreId, StorePiece, Workings);
            }

            internal SiteMutationResult AddStore(NarrativeWorldState world)
            {
                return SiteMutation.Apply(
                    world,
                    SiteId,
                    new SiteAdditionPlan(StoreId, StorePiece, Workings),
                    Grammars,
                    Pieces,
                    Actions,
                    Vanilla,
                    Stager,
                    Vanilla.Now);
            }

            internal SiteMutationResult Add(string additionId, string pieceId, string anchor)
            {
                return Add(additionId, pieceId, anchor, SiteId);
            }

            internal SiteMutationResult Add(string additionId, string pieceId, string anchor, EntityId siteId)
            {
                return SiteMutation.Apply(
                    World,
                    siteId,
                    new SiteAdditionPlan(additionId, pieceId, anchor),
                    Grammars,
                    Pieces,
                    Actions,
                    Vanilla,
                    Stager,
                    Vanilla.Now);
            }

            internal SiteStructure Shape()
            {
                return Shape(SiteId);
            }

            internal SiteStructure Shape(EntityId siteId)
            {
                return Shape(siteId, Pieces, World);
            }

            internal SiteStructure Shape(EntityId siteId, SitePieceCatalogue pieces)
            {
                return Shape(siteId, pieces, World);
            }

            internal SiteStructure Shape(EntityId siteId, SitePieceCatalogue pieces, NarrativeWorldState world)
            {
                SiteRealizationResult shape = ScenarioDungeon.StructureOf(
                    world.Registry.GetSite(siteId), Grammars, pieces, Actions, Vanilla);
                Assert.True(shape.Built, string.Join("; ", shape.Refusals));
                return shape.Structure;
            }

            /// <summary>The same catalogue with one piece taken out of it, as a later bundle might.</summary>
            internal SitePieceCatalogue PiecesWithout(string pieceId)
            {
                List<SitePiece> kept = new List<SitePiece>();
                for (int i = 0; i < Pieces.Pieces.Count; i++)
                {
                    if (!string.Equals(Pieces.Pieces[i].Id, pieceId, StringComparison.Ordinal))
                    {
                        kept.Add(Pieces.Pieces[i]);
                    }
                }

                return new SitePieceCatalogue(Pieces.Family, kept);
            }

            /// <summary>
            /// Puts the mine's occupants in the mine. Genesis binds people the world already has
            /// rather than moving them (`BQ-091`, `D021`), so headless they are still standing where
            /// the situation left them; a return visit in play means they are here.
            /// </summary>
            internal void GatherTheCrew()
            {
                EntityId zone = SiteGenesis.ZoneOf(Site);
                for (int i = 0; i < Site.OccupantIds.Count; i++)
                {
                    Vanilla.SetZone(Site.OccupantIds[i], zone);
                }
            }

            /// <summary>Says what the build finds on every patch an addition off this part could take.</summary>
            internal void PaveAround(string nodeId, VanillaGround ground)
            {
                SitePlacement anchor = Shape().PlacementOf(nodeId);
                for (int x = anchor.X - 8; x < anchor.Right + 8; x++)
                {
                    for (int y = anchor.Y - 8; y < anchor.Bottom + 8; y++)
                    {
                        Pave(x, y, ground);
                    }
                }
            }

            internal void Pave(int x, int y, VanillaGround ground)
            {
                Vanilla.SetGround(SiteGenesis.ZoneOf(Site), x, y, ground);
            }

            private ScenarioDungeonResult Establish(string name, ulong seed)
            {
                ScenarioDungeonResult result = ScenarioDungeon.Establish(
                    World,
                    World.NewId("zone"),
                    name,
                    Thread.Id,
                    Errand,
                    seed,
                    Grammars,
                    Pieces,
                    Actions,
                    Vanilla,
                    Stager,
                    Vanilla.Now);

                Assert.True(result.Created, string.Join("; ", result.Refusals));
                return result;
            }

            private EntityId Person(string name, EntityId zone)
            {
                NarrativeNpc npc = World.Registry.Add(new NarrativeNpc(World.NewId("npc"), name));
                Vanilla.Define(npc.Id, level: 3, money: 20, zone: zone);
                return npc.Id;
            }
        }

        private static ContentBundle Bundle()
        {
            ContentBundleLoadResult loaded = ContentBundleLoader.LoadFile(
                Path.Combine(RepositoryRoot(), "Package", "content.bqc"));
            Assert.Empty(loaded.Diagnostics);
            return loaded.Bundle;
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
