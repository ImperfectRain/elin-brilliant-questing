using System;
using System.Collections.Generic;
using System.IO;
using BrilliantQuesting.Content;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Threads;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-089. Curated location grammars: a kind of place specified as requirements, and the
    /// abstract plans composed from it.
    ///
    /// The done-when is two claims, and the first two tests are those two claims. Two places from
    /// one grammar have to be recognisably the same kind - which here means the required parts and
    /// the required routes are the same every time, at every seed - and clearly not the same
    /// place, which means the optional ones actually differ. The inspector has to be able to
    /// explain every required node and edge, which is checked by asking it to and then looking for
    /// each of them in what it said, rather than by matching a fixed block of text.
    ///
    /// The rest hold the edges that would make those claims hollow: a plan that reaches a room it
    /// has no route to, a composition that is not reproducible, a place whose ways in collapse
    /// into one, and the authoring mistakes that must fail the content build instead of shipping.
    /// </summary>
    public class SiteGrammarTests
    {
        /// <summary>
        /// The first half of the done-when. Across many seeds of one grammar, every place has
        /// every part and every route that every place of that kind has - and no two of them are
        /// alike, because what is optional is drawn per place.
        /// </summary>
        [Fact]
        public void TwoPlacesFromOneGrammarShareItsCoreAndDifferInWhatIsOptional()
        {
            foreach (SiteGrammar grammar in Library().Grammars)
            {
                HashSet<string> shapes = new HashSet<string>(StringComparer.Ordinal);
                for (ulong seed = 1; seed <= 24; seed++)
                {
                    SiteLayout layout = grammar.Compose(seed);

                    for (int i = 0; i < grammar.Nodes.Count; i++)
                    {
                        SiteNodeSpec node = grammar.Nodes[i];
                        if (node.Required)
                        {
                            Assert.True(layout.Has(node.Id), grammar.Id + " lost " + node.Id + " at seed " + seed);
                        }
                    }

                    for (int i = 0; i < grammar.Routes.Count; i++)
                    {
                        SiteRouteSpec route = grammar.Routes[i];
                        if (grammar.IsRequired(route))
                        {
                            Assert.Contains(layout.Routes, held => ReferenceEquals(held.Spec, route));
                        }
                    }

                    Assert.Equal(grammar.SiteType, layout.SiteType);
                    shapes.Add(Shape(layout));
                }

                // Same kind of place is not the same place. A grammar every seed answers
                // identically is a template, which is the failure this step exists to avoid.
                Assert.True(shapes.Count > 1, grammar.Id + " makes the same place at every seed");
            }
        }

        /// <summary>
        /// Two grammars are two kinds of place. Nothing enforces this in the loader - a library
        /// where every kind demanded the same parts would load - so it is checked against the
        /// authored library, which is where it would actually go wrong.
        /// </summary>
        [Fact]
        public void DifferentGrammarsRequireDifferentPlaces()
        {
            IReadOnlyList<SiteGrammar> grammars = Library().Grammars;
            Assert.True(grammars.Count > 1);

            HashSet<string> cores = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < grammars.Count; i++)
            {
                List<string> required = new List<string>();
                for (int j = 0; j < grammars[i].Nodes.Count; j++)
                {
                    if (grammars[i].Nodes[j].Required)
                    {
                        required.Add(grammars[i].Nodes[j].Id);
                    }
                }

                required.Sort(StringComparer.Ordinal);
                Assert.True(cores.Add(string.Join(",", required)), grammars[i].Id + " is another kind's twin");
            }
        }

        /// <summary>
        /// The second half of the done-when. Every part and every route the plan holds is named in
        /// the trace, each said to be one every place of the kind has or one this place drew, and
        /// every optional part left out is accounted for rather than silently missing.
        /// </summary>
        [Fact]
        public void TheInspectorExplainsEveryNodeAndRouteInThePlan()
        {
            foreach (SiteGrammar grammar in Library().Grammars)
            {
                for (ulong seed = 1; seed <= 6; seed++)
                {
                    SiteLayout layout = grammar.Compose(seed);
                    string trace = NarrativeInspector.DescribeSiteLayout(layout);

                    Assert.Contains(grammar.Id, trace);

                    for (int i = 0; i < layout.Nodes.Count; i++)
                    {
                        Assert.Contains(layout.Nodes[i].Id, trace);
                    }

                    for (int i = 0; i < layout.Routes.Count; i++)
                    {
                        SiteLayoutRoute route = layout.Routes[i];
                        Assert.Contains(route.From + " -> " + route.To, trace);

                        for (int j = 0; j < route.Affordances.Count; j++)
                        {
                            Assert.Contains(route.Affordances[j].ToString(), trace);
                        }
                    }

                    for (int i = 0; i < layout.Omitted.Count; i++)
                    {
                        Assert.Contains(layout.Omitted[i].Id, trace);
                    }

                    // "Required" is the half of the explanation that carries the claim: without it
                    // the trace lists a place, but never says which of it is the kind.
                    Assert.Contains("every one", trace);
                }
            }
        }

        /// <summary>
        /// A plan never holds a part with no way to it. Optional rooms hang off other optional
        /// rooms, so a place that drew the inner one and not the outer one would otherwise carry a
        /// node the inspector could describe and nobody could reach.
        /// </summary>
        [Fact]
        public void EveryPartOfAPlanIsReachedByRoutesThePlanHolds()
        {
            foreach (SiteGrammar grammar in Library().Grammars)
            {
                for (ulong seed = 1; seed <= 24; seed++)
                {
                    SiteLayout layout = grammar.Compose(seed);
                    HashSet<string> reached = new HashSet<string>(StringComparer.Ordinal) { SiteGrammar.Outside };

                    bool grew = true;
                    while (grew)
                    {
                        grew = false;
                        for (int i = 0; i < layout.Routes.Count; i++)
                        {
                            SiteLayoutRoute route = layout.Routes[i];
                            if (reached.Contains(route.From) && reached.Add(route.To))
                            {
                                grew = true;
                            }
                        }
                    }

                    for (int i = 0; i < layout.Nodes.Count; i++)
                    {
                        Assert.True(
                            reached.Contains(layout.Nodes[i].Id),
                            grammar.Id + " seed " + seed + " cannot reach " + layout.Nodes[i].Id);
                    }
                }
            }
        }

        /// <summary>
        /// A place composed from a seed is that place every time. This is what lets a site store
        /// the grammar and the seed instead of the plan.
        /// </summary>
        [Fact]
        public void ThePlanForASeedIsAlwaysTheSamePlan()
        {
            foreach (SiteGrammar grammar in Library().Grammars)
            {
                Assert.Equal(Shape(grammar.Compose(99)), Shape(grammar.Compose(99)));
                Assert.Equal(Shape(grammar.Compose(99)), Shape(Library().Compose(grammar.Id, 99)));
            }
        }

        /// <summary>
        /// Two verbs that both wait on the same person's permission are one approach spelled
        /// twice (`D058`). A grammar has to hold that for every place it makes, not for most of
        /// them, so it is checked over seeds rather than once.
        /// </summary>
        [Fact]
        public void EveryPlaceAGrammarMakesHasTwoRealWaysIn()
        {
            foreach (SiteGrammar grammar in Library().Grammars)
            {
                for (ulong seed = 1; seed <= 24; seed++)
                {
                    IReadOnlyList<SiteApproach> approaches = grammar.Compose(seed).Approaches;
                    Assert.True(approaches.Count >= SiteGenesis.MinimumApproaches);

                    bool admitted = false;
                    bool uninvited = false;
                    HashSet<string> verbs = new HashSet<string>(StringComparer.Ordinal);
                    for (int i = 0; i < approaches.Count; i++)
                    {
                        admitted |= approaches[i].NeedsAdmission;
                        uninvited |= !approaches[i].NeedsAdmission;
                        Assert.True(verbs.Add(approaches[i].ActionId));
                    }

                    Assert.True(admitted && uninvited, grammar.Id + " seed " + seed + " has one way in twice");
                }
            }
        }

        /// <summary>
        /// The step's own done-when says *sites*, not plans. Two places of one kind go through
        /// genesis unaltered, come back out of a save, and each still knows the kind it is and the
        /// plan it was made from - which is what makes the difference between them durable rather
        /// than a fact about one session.
        /// </summary>
        [Fact]
        public void TwoSitesMadeFromOneGrammarSurviveASaveKnowingWhichPlanTheyAre()
        {
            SiteGrammarLibrary library = Library();
            SiteGrammar grammar = library.Get("site.bandit_camp");
            Assert.NotNull(grammar);

            NarrativeWorldState world = new NarrativeWorldState(11);
            SandboxVanillaState vanilla = new SandboxVanillaState(EntityId.Parse("npc_player"));
            SandboxStager stager = new SandboxStager(vanilla);

            EntityId first = Establish(world, stager, vanilla, grammar, 3, "the camp above Rill");
            EntityId second = Establish(world, stager, vanilla, grammar, 8, "the camp in the elder wood");

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(world));
            NarrativeSite one = reloaded.Registry.GetSite(first);
            NarrativeSite two = reloaded.Registry.GetSite(second);

            Assert.Equal(grammar.Id, one.GrammarId);
            Assert.Equal(grammar.Id, two.GrammarId);
            Assert.Equal(grammar.SiteType, one.SiteType);
            Assert.Equal(one.SiteType, two.SiteType);

            SiteLayout onePlan = library.LayoutOf(one);
            SiteLayout twoPlan = library.LayoutOf(two);
            Assert.NotNull(onePlan);
            Assert.NotNull(twoPlan);
            Assert.NotEqual(Shape(onePlan), Shape(twoPlan));

            // And the site's own trace says which plan it is, so a tester looking at two camps can
            // tell why they differ without reading the save.
            Assert.Contains(grammar.Id, NarrativeInspector.DescribeSite(reloaded, first, vanilla));
        }

        /// <summary>
        /// A place written down before grammars existed reads back planned by nobody, and nothing
        /// tries to compose a plan for it. That is the truth about it rather than a gap (`D017`).
        /// </summary>
        [Fact]
        public void APlaceNobodyPlannedFromAGrammarHasNoPlan()
        {
            NarrativeWorldState world = new NarrativeWorldState(4);
            NarrativeSite site = world.Registry.Add(new NarrativeSite(world.NewId("zone"), "the mill", "workshop"));

            Assert.Equal(string.Empty, site.GrammarId);
            Assert.Null(Library().LayoutOf(site));
            Assert.DoesNotContain("planned from", NarrativeInspector.DescribeSite(world, site.Id, null));
        }

        /// <summary>The authored library is real content, and it loads clean.</summary>
        [Fact]
        public void TheAuthoredGrammarsLoadWithoutDiagnostics()
        {
            IReadOnlyList<ContentDiagnostic> diagnostics;
            IReadOnlyList<SiteGrammar> grammars = SiteGrammarContent.LoadGrammars(Bundle(), out diagnostics);

            Assert.Empty(diagnostics);
            Assert.NotEmpty(grammars);
            for (int i = 0; i < grammars.Count; i++)
            {
                Assert.NotEqual(string.Empty, grammars[i].SiteType);
            }
        }

        // -- authoring mistakes are build errors ----------------------------------------------

        /// <summary>A grammar may require an affordance; it may not invent one.</summary>
        [Fact]
        public void AnAffordanceNobodyDefinedIsRefused()
        {
            Assert.Contains("no such spatial affordance", Refusal(Payload().Replace(
                "\"affordances\": [\"guarded_threshold\"]",
                "\"affordances\": [\"moat_of_bees\"]")).ToLowerInvariant());
        }

        /// <summary>A route joins parts this grammar declares.</summary>
        [Fact]
        public void ARouteToAPartTheGrammarNeverDeclaredIsRefused()
        {
            Assert.Contains("declares", Refusal(Payload().Replace("\"to\": \"stores\"", "\"to\": \"treasury\"")));
        }

        /// <summary>
        /// A required part nothing required reaches is a room every place of this kind has and no
        /// place of this kind can get to - the one thing the inspector could not honestly explain.
        /// </summary>
        [Fact]
        public void ARequiredPartNothingRequiredReachesIsRefused()
        {
            Assert.Contains(
                "no route reaches it",
                Refusal(Payload().Replace("{\"id\": \"commons\"},", "{\"id\": \"commons\"}, {\"id\": \"strongroom\"},")));
        }

        /// <summary>Ways in that all wait on the same permission are one way in.</summary>
        [Fact]
        public void AGrammarWhoseWaysInAllWaitOnSomebodyIsRefused()
        {
            Assert.Contains(
                "waits on somebody",
                Refusal(Payload().Replace(
                    "{\"from\": \"outside\", \"to\": \"stores\", \"via\": \"trespass\"}",
                    "{\"from\": \"outside\", \"to\": \"stores\", \"via\": \"trespass\", \"admission\": true}")));
        }

        /// <summary>A way in may not promise a verb nobody built.</summary>
        [Fact]
        public void AWayInTakenWithAVerbNobodyBuiltIsRefused()
        {
            Assert.Contains("no such verb", Refusal(Payload().Replace("\"trespass\"", "\"climb_the_wall\"")).ToLowerInvariant());
        }

        /// <summary>
        /// A grammar names meaning and never words it. What a place is called comes from the
        /// matter that needed it, so a sentence anywhere in the payload is either wording that
        /// belongs to the content pipeline or a typo.
        /// </summary>
        [Fact]
        public void AuthoredWordingInAGrammarIsRefused()
        {
            Assert.Contains("never words it", Refusal(Payload().Replace("\"commons\"", "\"the sleeping tents\"")));
        }

        /// <summary>A kind of place with nothing every one of them has is not a kind of place.</summary>
        [Fact]
        public void AGrammarWithNoRequiredPartIsRefused()
        {
            Assert.Contains("is not a kind of place", Refusal(Payload().Replace("\"requiredNodes\"", "\"optionalNodes\"")));
        }

        // -- fixture ---------------------------------------------------------------------------

        /// <summary>
        /// Everything a place's identity is made of, flattened: which parts it has and which routes
        /// join them. Two plans with the same shape are the same place.
        /// </summary>
        private static string Shape(SiteLayout layout)
        {
            List<string> parts = new List<string>();
            for (int i = 0; i < layout.Nodes.Count; i++)
            {
                parts.Add("node:" + layout.Nodes[i].Id);
            }

            for (int i = 0; i < layout.Routes.Count; i++)
            {
                parts.Add("route:" + layout.Routes[i]);
            }

            parts.Sort(StringComparer.Ordinal);
            return string.Join("|", parts);
        }

        private static EntityId Establish(
            NarrativeWorldState world,
            ISituationStager stager,
            SandboxVanillaState vanilla,
            SiteGrammar grammar,
            ulong seed,
            string name)
        {
            NarrativeThread thread = new NarrativeThread(world.NewId("thread"), "bandits", GameTime.Zero)
            {
                State = ThreadState.Active
            };
            world.Threads.Add(thread);

            SitePlan plan = grammar.Compose(seed).NewPlan(world.NewId("zone"), name, thread.Id);
            for (int i = 0; i < SiteGenesis.MinimumOccupants; i++)
            {
                NarrativeNpc npc = new NarrativeNpc(world.NewId("npc"), "bandit " + i);
                plan.Occupants.Add(new SiteOccupantPlan(npc, "bandit", new CharacterBlueprint(npc.Name)));
            }

            plan.Cargo.Add(new SiteCargoPlan(
                new ItemDescriptor(world.NewId("item"), "a strongbox", "box", 200, "box"),
                plan.Occupants[0].Npc.Id));

            SiteGenesisResult result = SiteGenesis.Establish(world, plan, stager, vanilla.Now);
            Assert.True(result.Created, string.Join("; ", result.Reasons));
            return plan.SiteId;
        }

        /// <summary>
        /// A minimal camp, as JSON rather than YAML, because these tests are about what the reader
        /// refuses rather than about how a file is spelled.
        /// </summary>
        private static string Payload()
        {
            return "{"
                   + "\"siteType\": \"camp\", \"restricted\": true,"
                   + "\"requiredNodes\": ["
                   + "{\"id\": \"approach\", \"affordances\": [\"guarded_threshold\"]},"
                   + "{\"id\": \"commons\"},"
                   + "{\"id\": \"stores\"}],"
                   + "\"optionalNodes\": [{\"id\": \"lookout\"}],"
                   + "\"routes\": ["
                   + "{\"from\": \"outside\", \"to\": \"approach\", \"via\": \"persuade\", \"admission\": true},"
                   + "{\"from\": \"outside\", \"to\": \"stores\", \"via\": \"trespass\"},"
                   + "{\"from\": \"approach\", \"to\": \"commons\"},"
                   + "{\"from\": \"commons\", \"to\": \"stores\"},"
                   + "{\"from\": \"approach\", \"to\": \"lookout\"}]"
                   + "}";
        }

        private static string Refusal(string payload)
        {
            ContentBundle bundle = new ContentBundle(
                ContentBundle.CurrentVersion,
                new[] { new ContentRecord("site.probe", SiteGrammarContent.Kind, JsonValue.Parse(payload)) });

            IReadOnlyList<ContentDiagnostic> diagnostics;
            IReadOnlyList<SiteGrammar> grammars = SiteGrammarContent.LoadGrammars(bundle, out diagnostics);

            Assert.Empty(grammars);
            Assert.Single(diagnostics);
            return diagnostics[0].Message;
        }

        /// <summary>The unaltered payload is accepted, so every refusal above is about its edit.</summary>
        [Fact]
        public void TheProbeGrammarItselfLoads()
        {
            ContentBundle bundle = new ContentBundle(
                ContentBundle.CurrentVersion,
                new[] { new ContentRecord("site.probe", SiteGrammarContent.Kind, JsonValue.Parse(Payload())) });

            IReadOnlyList<ContentDiagnostic> diagnostics;
            Assert.Single(SiteGrammarContent.LoadGrammars(bundle, out diagnostics));
            Assert.Empty(diagnostics);
        }

        private static SiteGrammarLibrary Library()
        {
            IReadOnlyList<ContentDiagnostic> diagnostics;
            SiteGrammarLibrary library = SiteGrammarContent.CreateLibrary(Bundle(), out diagnostics);
            Assert.Empty(diagnostics);
            Assert.NotEmpty(library.Grammars);
            return library;
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
