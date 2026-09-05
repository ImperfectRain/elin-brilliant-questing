using System;
using System.Collections.Generic;
using System.IO;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Consequences;
using BrilliantQuesting.Content;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Situations;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-090. Spatial affordances: turning a place's requirements into ways somebody can actually
    /// take, and refusing the ones this build cannot keep.
    ///
    /// The done-when is one site completed three ways - the front gate, the side lock and the
    /// mined wall - so the first test is that claim twice over: the plan is read as three ways
    /// through, and the three verbs those ways name each get somebody into the same place in a
    /// running headless world. The rest hold the evidence gate, which is the half that decides
    /// what a player is allowed to be offered: a route is promised because the build was asked and
    /// said yes, or because nothing had to be asked, and never because a primitive seems likely to
    /// work.
    /// </summary>
    public class SiteRoutesTests
    {
        // -- the done-when ------------------------------------------------------------------

        /// <summary>
        /// One place, three ways in: talked past whoever is deciding, let in through a lock
        /// nobody opened, and in through the wall a pick makes. The plan says all three; the world
        /// proves all three end with the same place open to whoever took them.
        /// </summary>
        [Fact]
        public void OneSiteIsCompletedThreeWays()
        {
            SiteLayout mine = Library().Compose("site.collapsed_mine", seed: 7);
            SiteRouteProjection ways = SiteRoutes.Project(
                mine, WhatItKeeps(mine), StandardActions.CreateRegistry(), new SandboxVanillaState(EntityId.None));

            HashSet<string> entries = new HashSet<string>(StringComparer.Ordinal);
            foreach (SiteWayThrough way in ways.Promised)
            {
                foreach (string verb in way.Entry.PromisedVerbs())
                {
                    entries.Add(verb);
                }
            }

            Assert.Contains("persuade", entries);
            Assert.Contains("trespass", entries);
            Assert.Contains("mine_bypass", entries);

            // A way that waits on somebody and a way that does not are two different plays, and
            // the plan has to hold both or the three ways are one way spelled three times.
            Assert.Contains(ways.Promised, way => way.NeedsAdmission);
            Assert.Contains(ways.Promised, way => !way.NeedsAdmission);

            // And each of the three actually opens the place, in a world rather than in a plan.
            Assert.True(RouteLab.Create().TalkedIn().MineIsOpen);
            Assert.True(RouteLab.Create().LetThemselvesIn().MineIsOpen);
            Assert.True(RouteLab.Create().MinedIn().MineIsOpen);
        }

        /// <summary>
        /// The three are three, not one: a build that can only talk is not handed the pick, and a
        /// build that can only dig is not handed the doorman.
        /// </summary>
        [Fact]
        public void TheThreeWaysAreDifferentPlaysRatherThanOneRenamed()
        {
            SiteLayout mine = Library().Compose("site.collapsed_mine", seed: 7);
            SiteRouteProjection ways = SiteRoutes.Project(
                mine, WhatItKeeps(mine), StandardActions.CreateRegistry(), new SandboxVanillaState(EntityId.None));

            HashSet<string> plays = new HashSet<string>(StringComparer.Ordinal);
            foreach (SiteWayThrough way in ways.Promised)
            {
                plays.Add(string.Join(" then ", way.Vocabulary().ToArray()));
            }

            Assert.True(plays.Count >= 3, "the plan offers " + plays.Count + " distinct plays");
        }

        // -- the evidence gate ---------------------------------------------------------------

        /// <summary>
        /// The gate that matters in game. The mined wall leans on reading the obstruction standing
        /// in the place, which this build cannot do; the route is refused by name rather than
        /// offered and then silently empty, and the ways that lean on nothing are unaffected.
        /// </summary>
        [Fact]
        public void ARouteIsRefusedWhenTheBuildCannotDoWhatItLeansOn()
        {
            SandboxVanillaState build = new SandboxVanillaState(EntityId.None);
            build.SetCapability(VanillaCapability.ReadPlaceContents, false);

            SiteLayout mine = Library().Compose("site.collapsed_mine", seed: 7);
            SiteRouteProjection ways = SiteRoutes.Project(
                mine, WhatItKeeps(mine), StandardActions.CreateRegistry(), build);

            Assert.NotEmpty(ways.Promised);
            foreach (SiteWayThrough way in ways.Promised)
            {
                Assert.DoesNotContain("mine_bypass", way.Entry.PromisedVerbs());
            }

            SiteWayThrough refused = null;
            foreach (SiteWayThrough way in ways.Ways)
            {
                if (!way.Promised && way.Entry.Route.ActionId == "mine_bypass")
                {
                    refused = way;
                }
            }

            Assert.NotNull(refused);
            Assert.Contains("ReadPlaceContents", refused.Refusal);

            // Talking your way in and letting yourself in lean on nothing this build has to have.
            HashSet<string> stillOffered = new HashSet<string>(StringComparer.Ordinal);
            foreach (SiteWayThrough way in ways.Promised)
            {
                foreach (string verb in way.Entry.PromisedVerbs())
                {
                    stillOffered.Add(verb);
                }
            }

            Assert.Contains("persuade", stillOffered);
            Assert.Contains("trespass", stillOffered);
        }

        /// <summary>
        /// A grade is not a promise. Something read in the assemblies and never exercised, with no
        /// capability anybody can be asked about, is refused on every build - including one that
        /// advertises everything, because advertising everything is not the same as having been
        /// asked about this.
        /// </summary>
        [Fact]
        public void AnUnexercisedPrimitiveNothingCanBeAskedAboutIsNeverPromised()
        {
            SandboxVanillaState generous = new SandboxVanillaState(EntityId.None);

            SpatialRouteClaim unexercised = new SpatialRouteClaim(
                new[] { SiteAffordance.HiddenPassage }, RouteEvidence.SourceObserved, "a passage nobody has opened");
            SpatialRouteClaim symbolOnly = new SpatialRouteClaim(
                new[] { SiteAffordance.HiddenPassage }, RouteEvidence.MetadataOnly, "a name in a metadata dump");
            SpatialRouteClaim ours = new SpatialRouteClaim(
                new[] { SiteAffordance.LockedBarrier }, RouteEvidence.BqAuthored, string.Empty);

            Assert.False(unexercised.CanPromise(generous, out string first));
            Assert.Contains("a passage nobody has opened", first);
            Assert.False(symbolOnly.CanPromise(generous, out string second));
            Assert.Contains("MetadataOnly", second);
            Assert.True(ours.CanPromise(generous, out string none));
            Assert.Equal(string.Empty, none);
        }

        /// <summary>
        /// A build nobody has asked is not a build that can do everything. With no adapter to
        /// answer for it, a route needing a capability is refused rather than assumed.
        /// </summary>
        [Fact]
        public void AnUnaskedBuildIsNotATakenPromise()
        {
            SpatialRouteClaim needsAread = new SpatialRouteClaim(
                new[] { SiteAffordance.SocialCheckpoint },
                RouteEvidence.RuntimeVerified,
                "reading what somebody carries",
                VanillaCapability.ReadInventory);

            Assert.False(needsAread.CanPromise(null, out string refusal));
            Assert.Contains("ReadInventory", refusal);
        }

        // -- what the library can and cannot answer -------------------------------------------

        /// <summary>
        /// A requirement nothing in the action library answers cannot be a way through, and the
        /// refusal says which requirement it was rather than that the place is somehow wrong.
        /// </summary>
        [Fact]
        public void ARequirementNobodyAnswersIsNotAWayThrough()
        {
            SiteLayout layout = Sketch(
                new SiteRouteSpec(SiteGrammar.Outside, "yard", "trespass", false, new SiteAffordance[0]),
                new SiteRouteSpec("yard", "vault", string.Empty, false, new[] { SiteAffordance.TrapCluster }));

            SiteRouteProjection ways = SiteRoutes.Project(
                layout, "vault", StandardActions.CreateRegistry(), new SandboxVanillaState(EntityId.None));

            Assert.Empty(ways.Promised);
            Assert.Single(ways.Ways);
            Assert.Contains("TrapCluster", ways.Ways[0].Refusal);
        }

        /// <summary>
        /// A leg can still be takeable with a requirement outstanding - a trap does not shut a
        /// door - and the gap is reported rather than quietly dropped, because it is the same gap
        /// a later step has to close.
        /// </summary>
        [Fact]
        public void ARequirementNobodyAnswersIsReportedEvenWhereTheRouteStillWorks()
        {
            SiteLayout layout = Sketch(
                new SiteRouteSpec(SiteGrammar.Outside, "yard", "trespass", false, new[] { SiteAffordance.TrapCluster }),
                new SiteRouteSpec("yard", "vault", string.Empty, false, new SiteAffordance[0]));

            SiteRouteProjection ways = SiteRoutes.Project(
                layout, "vault", StandardActions.CreateRegistry(), new SandboxVanillaState(EntityId.None));

            Assert.Single(ways.Promised);
            Assert.Contains(SiteAffordance.TrapCluster, ways.Promised[0].Entry.Unanswered);
            Assert.Contains("trespass", ways.Promised[0].Entry.PromisedVerbs());
        }

        /// <summary>
        /// A verb that leaves the place exactly as shut as it was is not a way in, however well it
        /// reads, and a verb nobody registered is not one either.
        /// </summary>
        [Fact]
        public void OnlyVerbsThatActuallyGetSomebodyThroughAreARoutePromise()
        {
            SiteLayout looksLikeAnAnswer = Sketch(
                new SiteRouteSpec(SiteGrammar.Outside, "vault", "search", false, new SiteAffordance[0]));
            SiteLayout inventedVerb = Sketch(
                new SiteRouteSpec(SiteGrammar.Outside, "vault", "sing_it_open", false, new SiteAffordance[0]));

            ActionRegistry actions = StandardActions.CreateRegistry();
            SandboxVanillaState build = new SandboxVanillaState(EntityId.None);

            SiteRouteProjection searched = SiteRoutes.Project(looksLikeAnAnswer, "vault", actions, build);
            SiteRouteProjection sung = SiteRoutes.Project(inventedVerb, "vault", actions, build);

            Assert.Empty(searched.Promised);
            Assert.Contains("does not take anybody through", searched.Ways[0].Refusal);
            Assert.Empty(sung.Promised);
            Assert.Contains("registered", sung.Ways[0].Refusal);
        }

        /// <summary>
        /// Every verb that claims to be a way through has to be one the library actually
        /// registers, and has to say what it answers. A claim nobody can reach is the drift this
        /// step exists to make impossible.
        /// </summary>
        [Fact]
        public void EveryRoutePromiseInTheLibraryIsRegisteredAndSaysWhatItAnswers()
        {
            ActionRegistry actions = StandardActions.CreateRegistry();
            int claims = 0;

            foreach (NarrativeAction action in actions.Actions)
            {
                ISpatialRouteVerb verb = action as ISpatialRouteVerb;
                if (verb == null)
                {
                    continue;
                }

                claims++;
                Assert.NotNull(verb.SpatialRoute);
                Assert.NotEmpty(verb.SpatialRoute.Answers);
                Assert.Same(action, actions.Get(action.Id));

                // A verb leaning on the live build has to name what it leans on, or a refusal
                // could not say anything useful about it.
                if (verb.SpatialRoute.Evidence != RouteEvidence.BqAuthored)
                {
                    Assert.NotEqual(string.Empty, verb.SpatialRoute.LeansOn);
                }
            }

            Assert.True(claims > 0, "no verb in the library claims to be a way through a place");
        }

        // -- reading the plan -----------------------------------------------------------------

        /// <summary>The same plan read twice says the same thing, ways and refusals alike.</summary>
        [Fact]
        public void TheSamePlanProjectsTheSameWaysTwice()
        {
            SiteGrammarLibrary library = Library();
            ActionRegistry actions = StandardActions.CreateRegistry();
            SandboxVanillaState build = new SandboxVanillaState(EntityId.None);

            foreach (SiteGrammar grammar in library.Grammars)
            {
                SiteLayout first = grammar.Compose(11);
                SiteLayout second = grammar.Compose(11);
                string objective = WhatItKeeps(first);
                if (objective.Length == 0)
                {
                    continue;
                }

                Assert.Equal(
                    NarrativeInspector.DescribeSiteRoutes(SiteRoutes.Project(first, objective, actions, build)),
                    NarrativeInspector.DescribeSiteRoutes(SiteRoutes.Project(second, objective, actions, build)));
            }
        }

        /// <summary>
        /// Asking about a part the place does not have is answered rather than guessed at, and
        /// everywhere-that-is-not-this-place is not a destination.
        /// </summary>
        [Fact]
        public void APartThePlaceDoesNotHaveIsNotProjected()
        {
            SiteLayout mine = Library().Compose("site.collapsed_mine", seed: 7);
            ActionRegistry actions = StandardActions.CreateRegistry();
            SandboxVanillaState build = new SandboxVanillaState(EntityId.None);

            SiteRouteProjection missing = SiteRoutes.Project(mine, "throne_room", actions, build);
            SiteRouteProjection nowhere = SiteRoutes.Project(mine, SiteGrammar.Outside, actions, build);

            Assert.Empty(missing.Ways);
            Assert.Contains("throne_room", missing.Refusal);
            Assert.Empty(nowhere.Ways);
            Assert.NotEqual(string.Empty, nowhere.Refusal);
        }

        /// <summary>
        /// The inspector has to be able to say why a way is not on the table, naming the leg, the
        /// verb and what it leans on. A refusal nobody can read is a refusal nobody can fix.
        /// </summary>
        [Fact]
        public void TheInspectorExplainsWhatWasRefusedAndWhy()
        {
            SandboxVanillaState build = new SandboxVanillaState(EntityId.None);
            build.SetCapability(VanillaCapability.ReadPlaceContents, false);

            SiteLayout mine = Library().Compose("site.collapsed_mine", seed: 7);
            string report = NarrativeInspector.DescribeSiteRoutes(
                SiteRoutes.Project(mine, WhatItKeeps(mine), StandardActions.CreateRegistry(), build));

            Assert.Contains("mine_bypass", report);
            Assert.Contains("ReadPlaceContents", report);
            Assert.Contains("SourceObserved", report);
            Assert.Contains("refused", report);
            Assert.Contains("offered", report);
        }

        // -- helpers ---------------------------------------------------------------------------

        /// <summary>Where the place keeps what it keeps, which is what a way through leads to.</summary>
        private static string WhatItKeeps(SiteLayout layout)
        {
            for (int i = 0; i < layout.Nodes.Count; i++)
            {
                for (int a = 0; a < layout.Nodes[i].Affordances.Count; a++)
                {
                    if (layout.Nodes[i].Affordances[a] == SiteAffordance.EvidenceCache)
                    {
                        return layout.Nodes[i].Id;
                    }
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// A place sketched in code rather than authored, for the cases no shipped grammar should
        /// be bent into: a requirement nobody answers, a verb that opens nothing, a verb nobody
        /// registered.
        /// </summary>
        private static SiteLayout Sketch(params SiteRouteSpec[] routes)
        {
            List<SiteNodeSpec> nodes = new List<SiteNodeSpec>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (SiteRouteSpec route in routes)
            {
                foreach (string id in new[] { route.From, route.To })
                {
                    if (!string.Equals(id, SiteGrammar.Outside, StringComparison.Ordinal) && seen.Add(id))
                    {
                        nodes.Add(new SiteNodeSpec(id, true, new SiteAffordance[0], string.Empty));
                    }
                }
            }

            return new SiteGrammar("sketch", "hideout", true, nodes, routes).Compose(1);
        }

        private static SiteGrammarLibrary Library()
        {
            ContentBundleLoadResult loaded = ContentBundleLoader.LoadFile(
                Path.Combine(RepositoryRoot(), "Package", "content.bqc"));
            Assert.Empty(loaded.Diagnostics);

            IReadOnlyList<ContentDiagnostic> diagnostics;
            SiteGrammarLibrary library = SiteGrammarContent.CreateLibrary(loaded.Bundle, out diagnostics);
            Assert.Empty(diagnostics);
            return library;
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

        /// <summary>
        /// A mine shut by a rockfall, a foreman who can wave somebody through, and a lock on what
        /// the place keeps: the smallest world in which all three ways in are real. It is the
        /// BQ-029 laboratory, used here for the question BQ-090 asks of it - whether the three
        /// vocabularies the plan names each end with the same place open.
        /// </summary>
        private sealed class RouteLab
        {
            private RouteLab()
            {
            }

            private NarrativeWorldState World { get; set; }

            private SandboxVanillaState Vanilla { get; set; }

            private ActionRegistry Actions { get; set; }

            private FixedCheckResolver Checks { get; set; }

            private EntityId Player { get; set; }

            private BlockedPassageSituation Situation { get; set; }

            public bool MineIsOpen => World.Registry.GetSite(Situation.MineZoneId).Admits(Player);

            public static RouteLab Create()
            {
                RouteLab lab = new RouteLab();
                NarrativeWorldState world = new NarrativeWorldState(9090);
                EntityId player = world.NewId("npc");
                EntityId trail = world.NewId("zone");

                SandboxVanillaState vanilla = new SandboxVanillaState(player);
                vanilla.Define(player, level: 8, money: 50, zone: trail);
                vanilla.SetSkill(player, VanillaSkill.Mining, 18);
                vanilla.SetSkill(player, VanillaSkill.Lockpicking, 16);
                vanilla.SetSkill(player, VanillaSkill.Negotiation, 16);
                world.Registry.Add(new NarrativeNpc(player, "You") { Importance = NarrativeImportance.Major });

                lab.World = world;
                lab.Vanilla = vanilla;
                lab.Player = player;
                lab.Actions = StandardActions.CreateRegistry();
                lab.Checks = new FixedCheckResolver(CheckOutcome.Pass);

                SandboxStager stager = new SandboxStager(vanilla);
                lab.Situation = BlockedPassageSituation.Create(world, stager, player, trail, vanilla.Now);
                lab.Situation.StockThePlayer(world, stager, player);
                new ConsequenceEngine(world, vanilla).Attach();
                return lab;
            }

            /// <summary>The front gate: the person who decides waves you through.</summary>
            public RouteLab TalkedIn()
            {
                Run("persuade", Situation.TrailZoneId, Situation.ForemanId);
                return this;
            }

            /// <summary>The side lock: nobody opens it and you are inside anyway.</summary>
            public RouteLab LetThemselvesIn()
            {
                Run("trespass", Situation.MineZoneId, EntityId.None);
                return this;
            }

            /// <summary>The mined wall: the way in a pick makes.</summary>
            public RouteLab MinedIn()
            {
                Run("mine_bypass", Situation.TrailZoneId, EntityId.None);
                return this;
            }

            private void Run(string actionId, EntityId zone, EntityId target)
            {
                Vanilla.SetZone(Player, zone);
                ActionContext context = new ActionContext(World, Vanilla, Checks, World.Rng, Player, target)
                {
                    Thread = Situation.Thread
                };

                Actions.Get(actionId).Perform(context);
            }
        }
    }
}
