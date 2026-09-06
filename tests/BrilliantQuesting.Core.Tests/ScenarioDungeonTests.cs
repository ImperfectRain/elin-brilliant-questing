using System;
using System.Collections.Generic;
using System.IO;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Content;
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
    /// BQ-140. One procedural scenario dungeon: a worked-out mine somebody is holding, planned
    /// semantically and then actually built out of authored pieces.
    ///
    /// The done-when has two halves and they pull against each other, so the tests are in two
    /// blocks. The first is that seeds produce genuinely different places - measured off the
    /// navigation and problem structure of what was built, with piece names deliberately excluded,
    /// because "a different gallery in the same corridor" is decoration and must not be allowed to
    /// count. The second is that nothing is different about a place that should not be: the plan's
    /// objective, contents, evidence, occupants, ways in and affordances all survive being given a
    /// body, an unsupported feature is dropped and said to be dropped rather than swapped for
    /// something the build can do, and a place that has been made is never made twice.
    /// </summary>
    public class ScenarioDungeonTests
    {
        private const SiteAffordance Errand = SiteAffordance.EvidenceCache;

        // -- the done-when: different seeds, different problems ---------------------------------

        /// <summary>
        /// Thirteen authored pieces - twelve of which any build can be handed - sixteen seeds, and
        /// the places they make are different places to find your way through: different parts,
        /// different loops, different things to get past.
        ///
        /// Measured off <see cref="SiteTopology"/>, which counts the walkable graph and the things
        /// a walk to the objective asks of somebody, and which never sees a piece id. So this
        /// cannot pass because the seeds picked different rooms for the same corridor, which is
        /// exactly what the roadmap means by "not merely different decoration".
        /// </summary>
        [Fact]
        public void DifferentSeedsAreDifferentPlacesToFindYourWayThrough()
        {
            IReadOnlyList<SiteStructure> drawn = Drawn(16);
            Assert.True(drawn.Count >= 12, "only " + drawn.Count + " of 16 seeds produced a mine");

            HashSet<string> shapes = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> problems = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < drawn.Count; i++)
            {
                SiteTopology topology = drawn[i].Topology();
                shapes.Add(topology.Signature);
                problems.Add(string.Join(" | ", topology.Obstacles));

                // A signature that carried the pieces would make every reshuffle look like variety.
                Assert.DoesNotContain("piece.mine", topology.Signature);
            }

            Assert.True(shapes.Count >= 3, "sixteen seeds made " + shapes.Count + " distinct navigation shape(s)");
            Assert.True(
                problems.Count >= 3,
                "sixteen seeds made " + problems.Count + " distinct set(s) of things to get past");
        }

        /// <summary>
        /// The variety is in the graph rather than in the furniture: at least three seeds differ
        /// in what a walk to what the place keeps actually asks of somebody, and at least three
        /// differ in how many independent loops the place has.
        /// </summary>
        [Fact]
        public void TheVarietyIsNavigationalRatherThanDecorative()
        {
            IReadOnlyList<SiteStructure> drawn = Drawn(16);

            HashSet<int> loops = new HashSet<int>();
            HashSet<int> parts = new HashSet<int>();
            for (int i = 0; i < drawn.Count; i++)
            {
                SiteTopology topology = drawn[i].Topology();
                loops.Add(topology.Loops);
                parts.Add(topology.Parts.Count);

                // Every one of them is a place with a loop and more than one way to what it keeps,
                // which is what makes the differences differences of degree rather than of whether
                // the mine is a corridor.
                Assert.True(topology.Loops >= 1, "seed " + drawn[i].Seed + " made a place with no loop");
                Assert.True(topology.DistinctProblems >= 2, "seed " + drawn[i].Seed + " sets one problem");
            }

            Assert.True(loops.Count >= 2, "every seed made a place with the same number of loops");
            Assert.True(parts.Count >= 3, "sixteen seeds made " + parts.Count + " distinct size(s) of mine");
        }

        /// <summary>The same seed builds the same place, piece for piece and way for way.</summary>
        [Fact]
        public void TheSameSeedBuildsTheSamePlace()
        {
            for (ulong seed = 1; seed <= 6; seed++)
            {
                SiteStructure first = Draw(seed);
                SiteStructure again = Draw(seed);

                Assert.Equal(first.Topology().Signature, again.Topology().Signature);
                Assert.Equal(Pieces(first), Pieces(again));
                Assert.Equal(Ground(first), Ground(again));
            }
        }

        // -- what a body must not change about a plan --------------------------------------------

        /// <summary>
        /// Every check the built place is put through holds, on every seed: the parts the kind
        /// requires are there, no two pieces stand in the same ground, no piece carries more ways
        /// than it has, everything built can be walked to, and what the matter came for is one of
        /// the things that can.
        /// </summary>
        [Fact]
        public void EveryBuiltPlaceCanBeWalkedAndIsCheckedForIt()
        {
            Bench bench = Bench.Create();
            for (ulong seed = 1; seed <= 16; seed++)
            {
                SiteRealizationResult realization = bench.Realize(seed);
                if (realization.Structure == null)
                {
                    continue;
                }

                for (int i = 0; i < realization.Checks.Count; i++)
                {
                    Assert.True(
                        realization.Checks[i].Held,
                        "seed " + seed + ": " + realization.Checks[i]);
                }

                HashSet<string> walkable = realization.Structure.Walkable();
                Assert.Contains(realization.Structure.ObjectiveNodeId, walkable);
                for (int i = 0; i < realization.Structure.Placements.Count; i++)
                {
                    Assert.Contains(realization.Structure.Placements[i].NodeId, walkable);
                }
            }
        }

        /// <summary>
        /// The body says what the plan said. Every part of the plan that survived has a piece that
        /// affords what it required, every route between two surviving parts has a way, the ways in
        /// are still one that waits on somebody and one that does not, and the objective is still
        /// the part that answers the errand.
        /// </summary>
        [Fact]
        public void ThePlanSurvivesBeingGivenABody()
        {
            Bench bench = Bench.Create();
            for (ulong seed = 1; seed <= 8; seed++)
            {
                SiteRealizationResult realization = bench.Realize(seed);
                SiteStructure structure = realization.Structure;
                Assert.NotNull(structure);

                SiteLayout layout = structure.Layout;
                for (int i = 0; i < layout.Nodes.Count; i++)
                {
                    SiteLayoutNode node = layout.Nodes[i];
                    SitePlacement placement = structure.PlacementOf(node.Id);
                    if (node.Required)
                    {
                        Assert.NotNull(placement);
                    }

                    if (placement == null)
                    {
                        // Nothing is dropped silently: an optional part the place does not have is
                        // one the place says it does not have.
                        Assert.Contains(structure.Omitted, omission => omission.What == node.Id);
                        continue;
                    }

                    for (int a = 0; a < node.Affordances.Count; a++)
                    {
                        Assert.True(
                            placement.Piece.Affords(node.Affordances[a]),
                            node.Id + " requires " + node.Affordances[a] + " and got " + placement.Piece.Id);
                    }
                }

                for (int i = 0; i < layout.Routes.Count; i++)
                {
                    SiteLayoutRoute route = layout.Routes[i];
                    if (!Standing(structure, route.From) || !Standing(structure, route.To))
                    {
                        continue;
                    }

                    Assert.Contains(
                        structure.Connectors,
                        connector => connector.Route == route);
                }

                Assert.Equal(NodeAnswering(layout, Errand), structure.ObjectiveNodeId);

                bool admitted = false;
                bool uninvited = false;
                for (int i = 0; i < structure.Connectors.Count; i++)
                {
                    SiteConnector connector = structure.Connectors[i];
                    if (connector.IsEntry && connector.Passable)
                    {
                        admitted |= connector.NeedsAdmission;
                        uninvited |= !connector.NeedsAdmission;
                    }
                }

                Assert.True(admitted && uninvited, "seed " + seed + " lost one of the two ways in");
            }
        }

        /// <summary>
        /// The mine's hidden stope is authored, and nothing on any build this repository can
        /// describe reveals a way that has to be found. So the part is dropped with the reason -
        /// never quietly built as an ordinary drift, which would be the place claiming a hidden way
        /// exists where the plan's requirement was never met.
        /// </summary>
        [Fact]
        public void AnUnsupportedFeatureIsDroppedAndSaidToBeDroppedRatherThanSubstituted()
        {
            Bench bench = Bench.Create();
            bool everDrawn = false;

            for (ulong seed = 1; seed <= 32; seed++)
            {
                SiteRealizationResult realization = bench.Compose(seed);
                SiteStructure structure = realization.Structure;
                if (structure == null || !structure.Layout.Has("blind_stope"))
                {
                    continue;
                }

                everDrawn = true;

                // Not there, said to be not there, and the reason names what shut it.
                Assert.Null(structure.PlacementOf("blind_stope"));

                SiteStructureOmission omission = null;
                for (int i = 0; i < structure.Omitted.Count; i++)
                {
                    if (structure.Omitted[i].What == "blind_stope")
                    {
                        omission = structure.Omitted[i];
                    }
                }

                Assert.NotNull(omission);
                Assert.Contains("HiddenPassage", omission.Reason);
                Assert.Contains("blind_stope", NarrativeInspector.DescribeSiteStructure(realization));

                // And nothing was put in its place: no way of any other kind leads there.
                for (int i = 0; i < structure.Connectors.Count; i++)
                {
                    Assert.NotEqual("blind_stope", structure.Connectors[i].To);
                }
            }

            Assert.True(everDrawn, "no seed in the range drew the hidden stope at all");
        }

        /// <summary>
        /// A piece that leans on something this build cannot do is not built out of. The stope's
        /// own piece needs the read the live adapter does not have (`ELIN-Q-0008`), so on a build
        /// without it the piece is refused by name before reachability is even asked.
        /// </summary>
        [Fact]
        public void APieceLeaningOnSomethingTheBuildCannotDoIsRefusedByName()
        {
            Bench bench = Bench.Create();
            bench.Build.SetCapability(VanillaCapability.ReadPlaceContents, false);

            for (ulong seed = 1; seed <= 32; seed++)
            {
                SiteRealizationResult realization = bench.Compose(seed);
                SiteStructure structure = realization.Structure;
                if (structure == null || !structure.Layout.Has("blind_stope"))
                {
                    continue;
                }

                for (int i = 0; i < structure.Omitted.Count; i++)
                {
                    if (structure.Omitted[i].What != "blind_stope")
                    {
                        continue;
                    }

                    Assert.Contains("piece.mine.blind_stope", structure.Omitted[i].Reason);
                    Assert.Contains("ReadPlaceContents", structure.Omitted[i].Reason);
                    return;
                }
            }

            throw new InvalidOperationException("no seed drew the hidden stope");
        }

        /// <summary>
        /// A build that cannot make a place with a shape makes no place at all. The failure
        /// direction BQ-087 chose, kept: nothing is staged, nothing is registered, and the reason
        /// names the capability rather than the site.
        /// </summary>
        [Fact]
        public void ABuildThatCannotMakeAShapeMakesNothing()
        {
            Fixture fixture = Fixture.ATheftByACrew();
            fixture.Vanilla.SetCapability(VanillaCapability.BuildPlaceStructure, false);

            ScenarioDungeonResult result = fixture.Establish(seed: 4);

            Assert.False(result.Created);
            Assert.Null(result.Site);
            Assert.Empty(fixture.Stager.Built);
            Assert.Empty(fixture.World.Registry.Sites);
            Assert.Contains(result.Refusals, reason => reason.Contains("BuildPlaceStructure"));
        }

        /// <summary>
        /// A required part nobody authored a piece for refuses the whole place. There is nothing to
        /// degrade to: every mine has workings, so a mine without them is not a mine.
        /// </summary>
        [Fact]
        public void ARequiredPartWithNoAuthoredPieceRefusesThePlace()
        {
            Fixture fixture = Fixture.ATheftByACrew();
            List<SitePiece> without = new List<SitePiece>();
            for (int i = 0; i < fixture.Pieces.Pieces.Count; i++)
            {
                if (fixture.Pieces.Pieces[i].Socket != "mine_workings")
                {
                    without.Add(fixture.Pieces.Pieces[i]);
                }
            }

            ScenarioDungeonResult result = fixture.Establish(
                seed: 4, pieces: new SitePieceCatalogue(ScenarioDungeon.Family, without));

            Assert.False(result.Created);
            Assert.Null(result.Site);
            Assert.Empty(fixture.Stager.Built);
            Assert.Contains(result.Refusals, reason => reason.Contains("workings"));
        }

        // -- what the matter left, where it is ---------------------------------------------------

        /// <summary>
        /// The place holds what the matter left in it, and the physical shape puts it somewhere
        /// somebody can get to. The strongbox the crew took is anchored in the part of the mine
        /// that keeps things, and that part is reachable from outside.
        /// </summary>
        [Fact]
        public void WhatTheMatterLeftIsSomewhereSomebodyCanGetTo()
        {
            Fixture fixture = Fixture.ATheftByACrew();
            ScenarioDungeonResult result = fixture.Establish(seed: 4);

            Assert.True(result.Created, string.Join("; ", result.Refusals));

            SiteStructure structure = result.Structure;
            Assert.Contains(structure.Anchors, anchor => anchor.Id == fixture.StolenId);
            Assert.Contains(
                structure.Anchors,
                anchor => anchor.Id == fixture.StolenId && anchor.NodeId == structure.ObjectiveNodeId);

            HashSet<string> walkable = structure.Walkable();
            for (int i = 0; i < structure.Anchors.Count; i++)
            {
                Assert.Contains(structure.Anchors[i].NodeId, walkable);
            }

            // And the place the world knows holds the same people and the same object the plan did.
            Assert.Contains(fixture.StolenId, result.Site.ImportantObjectIds);
            Assert.True(result.Site.OccupantIds.Count >= SiteGenesis.MinimumOccupants);
            Assert.Contains(fixture.ThiefId, result.Site.OccupantIds);

            // Evidence stays attached to the fact rather than being copied into the site (`D011`).
            Assert.Contains(fixture.StolenId, fixture.World.Knowledge.GetFact(fixture.TheftFactId).EvidenceIds);
        }

        // -- made once -----------------------------------------------------------------------------

        /// <summary>
        /// A second call over the same place builds nothing. Genesis already refuses to make a
        /// place twice (BQ-087); this proves the physical half of it - the stager is asked to build
        /// exactly one structure, and the second call never even draws a plan.
        /// </summary>
        [Fact]
        public void APlaceThatExistsIsNeverBuiltASecondTime()
        {
            Fixture fixture = Fixture.ATheftByACrew();
            ScenarioDungeonResult first = fixture.Establish(seed: 4);
            Assert.True(first.Created, string.Join("; ", first.Refusals));

            ScenarioDungeonResult again = fixture.Establish(seed: 4);

            Assert.False(again.Created);
            Assert.True(again.AlreadyThere);
            Assert.Null(again.Realization);
            Assert.Null(again.Selection);
            Assert.Same(first.Site, again.Site);
            Assert.Single(fixture.Stager.Built);
            Assert.Equal(first.Site.EstablishedAt.TotalMinutes, again.Site.EstablishedAt.TotalMinutes);
        }

        /// <summary>
        /// A different seed offered for a place that already exists does not change it either. The
        /// shape a place has is the shape it was made with, and a caller that has forgotten which
        /// seed that was cannot rebuild the mine around the player.
        /// </summary>
        [Fact]
        public void AnEstablishedPlaceIgnoresALaterSeed()
        {
            Fixture fixture = Fixture.ATheftByACrew();
            ScenarioDungeonResult first = fixture.Establish(seed: 4);
            ulong made = first.Site.GenerationSeed;

            ScenarioDungeonResult again = fixture.Establish(seed: 99);

            Assert.True(again.AlreadyThere);
            Assert.Equal(made, again.Site.GenerationSeed);
            Assert.Single(fixture.Stager.Built);
        }

        // -- across a save ---------------------------------------------------------------------------

        /// <summary>
        /// BQ-087's identity boundary, inherited. A save and a reload find the same place, made for
        /// the same errand, from the same grammar and seed - and the shape reads back identical,
        /// because a structure is derived from those three rather than written into the save.
        /// </summary>
        [Fact]
        public void TheSamePlaceComesBackAfterASaveAndAReload()
        {
            Fixture fixture = Fixture.ATheftByACrew();
            ScenarioDungeonResult made = fixture.Establish(seed: 4);
            Assert.True(made.Created, string.Join("; ", made.Refusals));

            string before = made.Structure.Topology().Signature;
            int events = fixture.World.Ledger.Events.Count;

            NarrativeWorldState reloaded = WorldStateSerializer.Load(
                WorldStateSerializer.Save(fixture.World));

            NarrativeSite site = reloaded.Registry.GetSite(made.Site.Id);
            Assert.NotNull(site);
            Assert.True(site.Established);
            Assert.Equal(ScenarioDungeon.GrammarId, site.GrammarId);
            Assert.Equal(made.Site.GenerationSeed, site.GenerationSeed);
            Assert.Equal(Errand.ToString(), site.Objective);
            Assert.Equal(made.Site.OccupantIds.Count, site.OccupantIds.Count);
            Assert.Equal(made.Site.ImportantObjectIds.Count, site.ImportantObjectIds.Count);

            // Reading the shape back is a read: it stages nothing and records nothing.
            SiteRealizationResult read = ScenarioDungeon.StructureOf(
                site, fixture.Grammars, fixture.Pieces, fixture.Actions, fixture.Vanilla);

            Assert.True(read.Built, string.Join("; ", read.Refusals));
            Assert.Equal(before, read.Structure.Topology().Signature);
            Assert.Single(fixture.Stager.Built);
            Assert.Equal(events, reloaded.Ledger.Events.Count);
        }

        /// <summary>
        /// And the reloaded place is still the one that exists: asking for it again after a reload
        /// hands it back rather than building a second mine over the one the player has walked
        /// through.
        /// </summary>
        [Fact]
        public void AReloadedPlaceIsStillNeverBuiltASecondTime()
        {
            Fixture fixture = Fixture.ATheftByACrew();
            ScenarioDungeonResult made = fixture.Establish(seed: 4);

            NarrativeWorldState reloaded = WorldStateSerializer.Load(
                WorldStateSerializer.Save(fixture.World));
            SandboxStager stager = new SandboxStager(fixture.Vanilla);

            ScenarioDungeonResult again = ScenarioDungeon.Establish(
                reloaded,
                made.Site.Id,
                made.Site.Name,
                fixture.Thread.Id,
                Errand,
                4,
                fixture.Grammars,
                fixture.Pieces,
                fixture.Actions,
                fixture.Vanilla,
                stager,
                fixture.Vanilla.Now);

            Assert.True(again.AlreadyThere);
            Assert.Empty(stager.Built);
        }

        // -- the inspector -----------------------------------------------------------------------------

        /// <summary>Everything the place is, and everything it is not, is printable.</summary>
        [Fact]
        public void TheInspectorExplainsThePlaceAndWhatItDoesNotHave()
        {
            Fixture fixture = Fixture.ATheftByACrew();
            ScenarioDungeonResult result = fixture.Establish(seed: 4);
            string report = NarrativeInspector.DescribeSiteStructure(result.Realization);

            Assert.Contains("site structure site.collapsed_mine [mine]", report);
            Assert.Contains("piece.mine.", report);
            Assert.Contains("EvidenceCache", report);
            Assert.Contains("checked", report);
            Assert.Contains("shape parts[", report);
            Assert.Contains(fixture.StolenId.Value, report);

            SiteRealizationResult refused = SiteRealization.Realize(
                ScenarioDungeon.Family,
                null,
                Errand,
                fixture.Pieces,
                null,
                fixture.Actions,
                fixture.Vanilla);

            Assert.Contains("refused", NarrativeInspector.DescribeSiteStructure(refused));
        }

        // -- helpers -----------------------------------------------------------------------------------

        private static IReadOnlyList<SiteStructure> Drawn(int seeds)
        {
            Bench bench = Bench.Create();
            List<ulong> range = new List<ulong>();
            for (ulong seed = 1; seed <= (ulong)seeds; seed++)
            {
                range.Add(seed);
            }

            return ScenarioDungeon.Draw(range, Errand, bench.Grammars, bench.Pieces, bench.Actions, bench.Build);
        }

        private static SiteStructure Draw(ulong seed)
        {
            SiteRealizationResult realization = Bench.Create().Realize(seed);
            Assert.True(realization.Built, string.Join("; ", realization.Refusals));
            return realization.Structure;
        }

        /// <summary>The part of a plan that answers the errand, worked out here rather than read
        /// off the structure, so the structure is checked against something and not against itself.</summary>
        private static string NodeAnswering(SiteLayout layout, SiteAffordance objective)
        {
            for (int i = 0; i < layout.Nodes.Count; i++)
            {
                for (int a = 0; a < layout.Nodes[i].Affordances.Count; a++)
                {
                    if (layout.Nodes[i].Affordances[a] == objective)
                    {
                        return layout.Nodes[i].Id;
                    }
                }
            }

            return string.Empty;
        }

        private static bool Standing(SiteStructure structure, string nodeId)
        {
            return string.Equals(nodeId, SiteGrammar.Outside, StringComparison.Ordinal) || structure.Has(nodeId);
        }

        private static string Pieces(SiteStructure structure)
        {
            List<string> pieces = new List<string>();
            for (int i = 0; i < structure.Placements.Count; i++)
            {
                pieces.Add(structure.Placements[i].NodeId + "=" + structure.Placements[i].Piece.Id);
            }

            pieces.Sort(StringComparer.Ordinal);
            return string.Join(",", pieces.ToArray());
        }

        private static string Ground(SiteStructure structure)
        {
            List<string> ground = new List<string>();
            for (int i = 0; i < structure.Placements.Count; i++)
            {
                SitePlacement placement = structure.Placements[i];
                ground.Add(placement.NodeId + "@" + placement.X + "," + placement.Y);
            }

            ground.Sort(StringComparer.Ordinal);
            return string.Join(",", ground.ToArray());
        }

        /// <summary>The content and the build, with no world: everything a plan needs to be drawn.</summary>
        private sealed class Bench
        {
            private Bench(SiteGrammarLibrary grammars, SitePieceCatalogue pieces, SandboxVanillaState build)
            {
                Grammars = grammars;
                Pieces = pieces;
                Build = build;
                Actions = StandardActions.CreateRegistry();
            }

            internal SiteGrammarLibrary Grammars { get; }

            internal SitePieceCatalogue Pieces { get; }

            internal SandboxVanillaState Build { get; }

            internal ActionRegistry Actions { get; }

            internal static Bench Create()
            {
                ContentBundle bundle = Bundle();
                IReadOnlyList<ContentDiagnostic> diagnostics;
                SiteGrammarLibrary grammars = SiteGrammarContent.CreateLibrary(bundle, out diagnostics);
                Assert.Empty(diagnostics);

                SitePieceCatalogue pieces = SitePieceContent.CreateCatalogue(
                    bundle, ScenarioDungeon.Family, out diagnostics);
                Assert.Empty(diagnostics);
                Assert.True(pieces.Pieces.Count >= 8, "the mine family has " + pieces.Pieces.Count + " piece(s)");
                Assert.True(pieces.Pieces.Count <= 14, "the mine family has grown to " + pieces.Pieces.Count);

                return new Bench(grammars, pieces, new SandboxVanillaState(EntityId.None));
            }

            /// <summary>
            /// One composition, realized without going through selection.
            ///
            /// Selection is right to pass over a plan with a part nothing on this build can reach -
            /// it scores worse for exactly that reason (`BQ-092`) - but that means the shipped mine
            /// never carries the hidden stope, and the omission path would go untested through the
            /// ordinary pipeline. So the plans that would need it are composed directly.
            /// </summary>
            internal SiteRealizationResult Compose(ulong seed)
            {
                return SiteRealization.Realize(
                    ScenarioDungeon.Family,
                    Grammars.Get(ScenarioDungeon.GrammarId).Compose(seed),
                    Errand,
                    Pieces,
                    null,
                    Actions,
                    Build);
            }

            internal SiteRealizationResult Realize(ulong seed)
            {
                SiteCandidateSelection selection = SiteCandidates.Select(
                    Grammars.Get(ScenarioDungeon.GrammarId),
                    Errand,
                    seed,
                    SiteCandidates.DefaultBatch,
                    Actions,
                    Build);

                Assert.True(selection.Selected, selection.Refusal);
                return SiteRealization.Realize(
                    ScenarioDungeon.Family, selection.Layout, Errand, Pieces, null, Actions, Build);
            }
        }

        /// <summary>
        /// A theft by a crew, and the mine they are holding. Small on purpose: three people, one
        /// thing, one matter - the least a place can be furnished from (`BQ-091`).
        /// </summary>
        private sealed class Fixture
        {
            private Fixture(NarrativeWorldState world, SandboxVanillaState vanilla, Bench bench)
            {
                World = world;
                Vanilla = vanilla;
                Grammars = bench.Grammars;
                Pieces = bench.Pieces;
                Actions = bench.Actions;
                Stager = new SandboxStager(vanilla);
            }

            internal NarrativeWorldState World { get; }

            internal SandboxVanillaState Vanilla { get; }

            internal SiteGrammarLibrary Grammars { get; }

            internal SitePieceCatalogue Pieces { get; }

            internal ActionRegistry Actions { get; }

            internal SandboxStager Stager { get; }

            internal NarrativeThread Thread { get; private set; }

            internal EntityId ThiefId { get; private set; }

            internal EntityId StolenId { get; private set; }

            internal EntityId TheftFactId { get; private set; }

            internal EntityId SiteId { get; private set; }

            internal static Fixture ATheftByACrew()
            {
                NarrativeWorldState world = new NarrativeWorldState(140);
                SandboxVanillaState vanilla = new SandboxVanillaState(EntityId.Parse("npc_player"));
                Fixture fixture = new Fixture(world, vanilla, Bench.Create());

                EntityId town = world.NewId("zone");
                fixture.ThiefId = fixture.Person("Renn", town);
                EntityId victim = fixture.Person("Mab", town);
                EntityId hand = fixture.Person("Bryn", town);

                Organization crew = world.Registry.Add(
                    new Organization(world.NewId("org"), "the road crew", "criminal_crew")
                    {
                        LeaderId = fixture.ThiefId
                    });
                foreach (EntityId member in new[] { fixture.ThiefId, hand, fixture.Person("Tace", town) })
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
                fixture.TheftFactId = stole.Id;

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
                fixture.SiteId = world.NewId("zone");
                return fixture;
            }

            internal ScenarioDungeonResult Establish(ulong seed, SitePieceCatalogue pieces = null)
            {
                return ScenarioDungeon.Establish(
                    World,
                    SiteId,
                    "the workings above the drove road",
                    Thread.Id,
                    Errand,
                    seed,
                    Grammars,
                    pieces ?? Pieces,
                    Actions,
                    Vanilla,
                    Stager,
                    Vanilla.Now);
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
