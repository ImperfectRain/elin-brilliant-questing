using System;
using System.Collections.Generic;
using System.IO;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Actions.Library;
using BrilliantQuesting.Content;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-092. Drawing several plans for a place, refusing the ones that are wrong for the errand,
    /// and taking the best of what is left.
    ///
    /// The done-when is a readable refusal, so the first block is one test per reason a plan can be
    /// refused, each asserting that the inspector says which plan, which part and why. Two of the
    /// reasons come out of the shipped catalogue - a place with nowhere to answer the errand, and a
    /// place whose uninvited way in reaches nothing the errand wants - and the rest come from plans
    /// sketched in code, because they are shapes a shipped grammar should never be bent into and a
    /// test that bent one would be testing the content instead of the rule.
    ///
    /// The second block is what selection is for: the same seed draws the same batch, the best
    /// score wins, a refused plan is never chosen, and the same grammar is a good answer to one
    /// errand and a refused one to another.
    /// </summary>
    public class SiteCandidatesTests
    {
        // -- the done-when: every reason is readable -------------------------------------------

        /// <summary>
        /// A place with nowhere that answers what the matter came for. The mine keeps things and
        /// holds nobody, so an errand after somebody being held has nowhere to happen - which is
        /// the honest form of an unreachable objective, since a part with nothing leading to it is
        /// something composition drops rather than composes (BQ-089).
        /// </summary>
        [Fact]
        public void APlaceWithNowhereToAnswerTheErrandIsRefused()
        {
            SiteCandidateSelection selection = Draw("site.collapsed_mine", SiteAffordance.PrisonCell);

            Assert.False(selection.Selected);
            AssertReadable(selection, SiteFlaw.UnreachableObjective, "PrisonCell");
        }

        /// <summary>
        /// The prison can be dug into and its ledger cannot: every way to it waits on the gate,
        /// while the plan still advertises a way in that waits on nobody. The second approach is
        /// then decoration, and BQ-087's two ways in are true of the place and false of the errand.
        /// </summary>
        [Fact]
        public void AWayInThatReachesNothingTheErrandWantsIsRefused()
        {
            SiteCandidateSelection selection = Draw("site.makeshift_prison", SiteAffordance.EvidenceCache);

            Assert.False(selection.Selected);
            AssertReadable(selection, SiteFlaw.AccessOrderingFailure, "ledger_nook");
        }

        /// <summary>Two ways to the strongroom, and both of them are the doorman.</summary>
        [Fact]
        public void AlternateRoutesThatAreOneRouteRenamedAreRefused()
        {
            SiteCandidateSelection selection = Weigh(
                Sketch(
                    Node("hall"),
                    Node("vault", SiteAffordance.EvidenceCache)),
                Entry("hall", "persuade", admitted: true),
                Entry("vault", "persuade", admitted: true),
                Route("hall", "vault"));

            AssertReadable(selection, SiteFlaw.RoutesCollapseIntoOnePlay, "vault");
        }

        /// <summary>What the place keeps, lying where anybody can walk in and pick it up.</summary>
        [Fact]
        public void AWayToTheObjectiveThatAsksNothingIsRefused()
        {
            SiteCandidateSelection selection = Weigh(
                Sketch(
                    Node("hall"),
                    Node("vault", SiteAffordance.EvidenceCache)),
                Entry("hall", "persuade", admitted: true),
                Route("outside", "vault"),
                Route("hall", "vault", SiteAffordance.LockedBarrier));

            AssertReadable(selection, SiteFlaw.UselessLoopOrTrivialShortcut, "trivial shortcut");
        }

        /// <summary>The same step twice, one of them free: nobody ever takes the other.</summary>
        [Fact]
        public void ARouteThatDuplicatesOneAlreadyThereForFreeIsRefused()
        {
            SiteCandidateSelection selection = Weigh(
                Sketch(
                    Node("hall"),
                    Node("vault", SiteAffordance.EvidenceCache)),
                Entry("hall", "persuade", admitted: true),
                Route("hall", "vault", SiteAffordance.LockedBarrier),
                Route("hall", "vault"));

            AssertReadable(selection, SiteFlaw.UselessLoopOrTrivialShortcut, "useless loop");
        }

        /// <summary>Four parts in a line: walking, with nothing decided anywhere along it.</summary>
        [Fact]
        public void APlanWithNoChoiceAnywhereInItIsRefused()
        {
            SiteCandidateSelection selection = Weigh(
                Sketch(
                    Node("hall"),
                    Node("passage"),
                    Node("stair"),
                    Node("vault", SiteAffordance.EvidenceCache)),
                Entry("hall", "persuade", admitted: true),
                Route("hall", "passage"),
                Route("passage", "stair"),
                Route("stair", "vault", SiteAffordance.LockedBarrier));

            AssertReadable(selection, SiteFlaw.BacktrackingOrLowInformation, "corridor");
        }

        /// <summary>Rooms with nothing in them and nowhere onward: walked into, walked back out of.</summary>
        [Fact]
        public void APlanMadeMostlyOfEmptyDeadEndsIsRefused()
        {
            SiteCandidateSelection selection = Weigh(
                Sketch(
                    Node("hall"),
                    Node("vault", SiteAffordance.EvidenceCache),
                    Node("first"),
                    Node("second"),
                    Node("third"),
                    Node("fourth")),
                Entry("hall", "persuade", admitted: true),
                Route("hall", "vault", SiteAffordance.LockedBarrier),
                Route("hall", "first"),
                Route("hall", "second"),
                Route("hall", "third"),
                Route("hall", "fourth"));

            AssertReadable(selection, SiteFlaw.BacktrackingOrLowInformation, "backtracking");
        }

        /// <summary>
        /// The place keeps something somewhere nothing can get to. Distinct from the errand's own
        /// objective, which is reached perfectly well here.
        /// </summary>
        [Fact]
        public void EvidenceNoPromisedWayReachesIsRefused()
        {
            SiteCandidateSelection selection = Weigh(
                Sketch(
                    Node("hall"),
                    Node("vault", SiteAffordance.EvidenceCache),
                    Node("strongroom", SiteAffordance.EvidenceCache)),
                Entry("hall", "persuade", admitted: true),
                Route("hall", "vault", SiteAffordance.LockedBarrier),
                Route("hall", "strongroom", SiteAffordance.HiddenPassage));

            AssertReadable(selection, SiteFlaw.InaccessibleEvidence, "strongroom");
        }

        /// <summary>
        /// The evidence gate, applied to a whole plan (`D067`). The only way to what the place
        /// keeps leans on reading the obstruction standing in it, and a build that cannot do that
        /// is not offered the plan.
        /// </summary>
        [Fact]
        public void APlanWhoseOnlyWayThisBuildCannotPromiseIsRefused()
        {
            SandboxVanillaState build = new SandboxVanillaState(EntityId.None);
            build.SetCapability(VanillaCapability.ReadPlaceContents, false);

            SiteCandidateSelection selection = Weigh(
                build,
                Sketch(
                    Node("shaft"),
                    Node("vault", SiteAffordance.EvidenceCache)),
                Entry("shaft", "mine_bypass", admitted: false, SiteAffordance.DiggableBypass),
                Route("shaft", "vault", SiteAffordance.DiggableBypass));

            AssertReadable(selection, SiteFlaw.RoutePromiseUnsupported, "ReadPlaceContents");
        }

        /// <summary>
        /// A requirement nobody has written a verb for is its own answer, and not the build's
        /// fault. The plan is still refused, and the report says which of the two it was.
        /// </summary>
        [Fact]
        public void APlanDemandingSomethingNoVerbAnswersIsRefused()
        {
            SiteCandidateSelection selection = Weigh(
                Sketch(
                    Node("front"),
                    Node("vault", SiteAffordance.EvidenceCache)),
                Entry("front", "persuade", admitted: true),
                Route("front", "vault", SiteAffordance.HiddenPassage));

            AssertReadable(selection, SiteFlaw.RequirementUnanswered, "HiddenPassage");
        }

        /// <summary>
        /// Every way a plan can be refused has somewhere it is demonstrated and printed. A reason
        /// the inspector never prints is a reason nobody can act on, and adding one without a case
        /// that produces it should fail here rather than pass quietly.
        /// </summary>
        [Fact]
        public void EveryReasonAPlanCanBeRefusedIsReadableSomewhere()
        {
            HashSet<SiteFlaw> printed = new HashSet<SiteFlaw>();
            foreach (SiteCandidateSelection selection in EveryRefusal())
            {
                string report = NarrativeInspector.DescribeSiteCandidates(selection);
                foreach (SiteFlaw flaw in Enum.GetValues(typeof(SiteFlaw)))
                {
                    if (report.Contains(flaw.ToString()))
                    {
                        printed.Add(flaw);
                    }
                }
            }

            List<string> missing = new List<string>();
            foreach (SiteFlaw flaw in Enum.GetValues(typeof(SiteFlaw)))
            {
                // Checked separately below, because only one of its two forms can be produced: a
                // place with nowhere to answer the errand is a draw, and a part nothing leads to is
                // something composition drops rather than composes (BQ-089).
                if (flaw != SiteFlaw.UnreachableObjective && !printed.Contains(flaw))
                {
                    missing.Add(flaw.ToString());
                }
            }

            Assert.True(missing.Count == 0, "no case prints " + string.Join(", ", missing.ToArray()));
            Assert.Contains(SiteFlaw.UnreachableObjective, printed);
        }

        // -- what selection is for --------------------------------------------------------------

        /// <summary>
        /// The same grammar, seed, batch and build draw the same plans, the same scores and the
        /// same refusals (`PP §8`). A selected place that did not replay would be a place a save
        /// could not describe.
        /// </summary>
        [Fact]
        public void TheSameSeedDrawsTheSameBatchTwice()
        {
            foreach (SiteGrammar grammar in Library().Grammars)
            {
                Assert.Equal(
                    NarrativeInspector.DescribeSiteCandidates(Draw(grammar, SiteAffordance.EvidenceCache, 19)),
                    NarrativeInspector.DescribeSiteCandidates(Draw(grammar, SiteAffordance.EvidenceCache, 19)));
            }
        }

        /// <summary>
        /// A different seed is a different batch. Selection that returned the same plan whatever it
        /// was asked would be composition with extra steps.
        /// </summary>
        [Fact]
        public void ADifferentSeedDrawsADifferentBatch()
        {
            HashSet<ulong> seeds = new HashSet<ulong>();
            for (ulong seed = 1; seed <= 12; seed++)
            {
                SiteCandidateSelection selection = Draw("site.collapsed_mine", SiteAffordance.EvidenceCache, seed);
                Assert.True(selection.Selected);
                seeds.Add(selection.Chosen.Seed);
            }

            Assert.True(seeds.Count > 1, "twelve batches chose one plan between them");
        }

        /// <summary>The best of what is left wins, and nothing refused is ever chosen.</summary>
        [Fact]
        public void TheChosenPlanIsTheBestOneThatWasNotRefused()
        {
            foreach (SiteGrammar grammar in Library().Grammars)
            {
                foreach (SiteAffordance objective in Objectives)
                {
                    SiteCandidateSelection selection = Draw(grammar, objective, 5);
                    Assert.NotEmpty(selection.Considered);

                    foreach (SitePlanCandidate candidate in selection.Considered)
                    {
                        if (!candidate.Usable)
                        {
                            Assert.False(candidate.Chosen);
                            Assert.NotEmpty(candidate.Flaws);
                            continue;
                        }

                        Assert.True(
                            selection.Selected,
                            grammar.Id + " had a usable plan and chose nothing");
                        Assert.True(
                            selection.Chosen.Score.Total >= candidate.Score.Total,
                            grammar.Id + " passed over a plan that scored higher");
                    }

                    if (selection.Selected)
                    {
                        Assert.True(selection.Chosen.Usable);
                        Assert.Empty(selection.Chosen.Flaws);
                    }
                    else
                    {
                        Assert.NotEqual(string.Empty, selection.Refusal);
                        Assert.Null(selection.Layout);
                        Assert.Null(selection.NewPlan(EntityId.Parse("site.1"), "somewhere", EntityId.Parse("thread.1")));
                    }
                }
            }
        }

        /// <summary>
        /// The errand decides, not the kind of place. The prison is a refused plan for an errand
        /// after its ledger and a good one for an errand after the people in it, which is why the
        /// objective is a requirement passed in rather than a room the grammar names.
        /// </summary>
        [Fact]
        public void OnePlaceIsAGoodPlanForOneErrandAndARefusedOneForAnother()
        {
            SiteCandidateSelection ledger = Draw("site.makeshift_prison", SiteAffordance.EvidenceCache);
            SiteCandidateSelection cells = Draw("site.makeshift_prison", SiteAffordance.PrisonCell);

            Assert.False(ledger.Selected);
            Assert.True(cells.Selected);
            Assert.Equal("cells", cells.Chosen.ObjectiveNodeId);
        }

        /// <summary>
        /// A build that can do less is offered less. Withdrawing the read the physical routes lean
        /// on costs the mine its dug ways, and the score says so rather than the plan quietly
        /// staying as good as it was.
        /// </summary>
        [Fact]
        public void APlanIsWorthLessOnABuildThatCannotKeepItsPromises()
        {
            SandboxVanillaState cannotRead = new SandboxVanillaState(EntityId.None);
            cannotRead.SetCapability(VanillaCapability.ReadPlaceContents, false);

            SiteCandidateSelection whole = Draw("site.collapsed_mine", SiteAffordance.EvidenceCache);
            SiteCandidateSelection reduced = Weigh(
                cannotRead, Library().Get("site.collapsed_mine"), SiteAffordance.EvidenceCache);

            Assert.True(whole.Selected);
            Assert.True(reduced.Selected);
            Assert.True(
                reduced.Chosen.Score.Total < whole.Chosen.Score.Total,
                "a build that cannot dig scored the mine as highly as one that can");
            Assert.True(reduced.Chosen.Score.Mechanics < whole.Chosen.Score.Mechanics);
        }

        /// <summary>
        /// A score is measured, not asserted: every part is between nothing and one, and the total
        /// is what the parts average to. The counts printed beside them are the same reading.
        /// </summary>
        [Fact]
        public void AScoreIsBoundedAndIsWhatItsPartsAverageTo()
        {
            foreach (SiteGrammar grammar in Library().Grammars)
            {
                foreach (SiteAffordance objective in Objectives)
                {
                    foreach (SitePlanCandidate candidate in Draw(grammar, objective, 3).Considered)
                    {
                        SiteCandidateScore score = candidate.Score;
                        double[] parts =
                        {
                            score.Reachability, score.RouteDiversity, score.ObjectiveSeparation,
                            score.EvidenceDistribution, score.LoopQuality, score.MechanicVocabulary
                        };

                        double sum = 0.0;
                        foreach (double part in parts)
                        {
                            Assert.InRange(part, 0.0, 1.0);
                            sum += part;
                        }

                        Assert.Equal(sum / parts.Length, score.Total, 9);
                        Assert.InRange(score.ReachableNodes, 0, score.Nodes);
                        Assert.InRange(score.ReachableKeepNodes, 0, score.KeepNodes);
                        Assert.InRange(score.DistinctPlays, 0, Math.Max(score.PromisedWays, 0));
                    }
                }
            }
        }

        /// <summary>
        /// Selection hands back the one vocabulary the rest of the codebase already has for what a
        /// place must be, so genesis and reuse weigh a chosen plan exactly as they weigh a composed
        /// one (BQ-087, BQ-088, BQ-089).
        /// </summary>
        [Fact]
        public void TheChosenPlanIsHandedOverInTheShapeGenesisValidates()
        {
            SiteCandidateSelection selection = Draw("site.collapsed_mine", SiteAffordance.EvidenceCache);
            SitePlan plan = selection.NewPlan(
                EntityId.Parse("site.mine"), "the old workings", EntityId.Parse("thread.1"));

            Assert.Equal(selection.Chosen.Layout.GrammarId, plan.GrammarId);
            Assert.Equal(selection.Chosen.Seed, plan.Seed);
            Assert.Equal(selection.Chosen.Layout.SiteType, plan.SiteType);
            Assert.Contains(plan.Approaches, approach => approach.NeedsAdmission);
            Assert.Contains(plan.Approaches, approach => !approach.NeedsAdmission);
        }

        /// <summary>Nothing to draw from, and nothing invented to cover for it.</summary>
        [Fact]
        public void NothingIsDrawnWithoutAGrammarOrABatch()
        {
            SiteCandidateSelection noGrammar = SiteCandidates.Select(
                null, SiteAffordance.EvidenceCache, 1, 3, Actions(), Build());
            SiteCandidateSelection noBatch = SiteCandidates.Select(
                Library().Get("site.warehouse"), SiteAffordance.EvidenceCache, 1, 0, Actions(), Build());

            Assert.False(noGrammar.Selected);
            Assert.Empty(noGrammar.Considered);
            Assert.NotEqual(string.Empty, noGrammar.Refusal);
            Assert.False(noBatch.Selected);
            Assert.Empty(noBatch.Considered);
            Assert.NotEqual(string.Empty, noBatch.Refusal);
        }

        // -- helpers ----------------------------------------------------------------------------

        private static readonly SiteAffordance[] Objectives =
        {
            SiteAffordance.EvidenceCache, SiteAffordance.PrisonCell
        };

        /// <summary>Every refusal this file demonstrates, gathered for the coverage check.</summary>
        private static IEnumerable<SiteCandidateSelection> EveryRefusal()
        {
            yield return Draw("site.collapsed_mine", SiteAffordance.PrisonCell);
            yield return Draw("site.makeshift_prison", SiteAffordance.EvidenceCache);

            yield return Weigh(
                Sketch(Node("hall"), Node("vault", SiteAffordance.EvidenceCache)),
                Entry("hall", "persuade", admitted: true),
                Entry("vault", "persuade", admitted: true),
                Route("hall", "vault"));

            yield return Weigh(
                Sketch(Node("hall"), Node("vault", SiteAffordance.EvidenceCache)),
                Entry("hall", "persuade", admitted: true),
                Route("outside", "vault"),
                Route("hall", "vault", SiteAffordance.LockedBarrier));

            yield return Weigh(
                Sketch(Node("hall"), Node("passage"), Node("stair"), Node("vault", SiteAffordance.EvidenceCache)),
                Entry("hall", "persuade", admitted: true),
                Route("hall", "passage"),
                Route("passage", "stair"),
                Route("stair", "vault", SiteAffordance.LockedBarrier));

            yield return Weigh(
                Sketch(
                    Node("hall"), Node("vault", SiteAffordance.EvidenceCache),
                    Node("first"), Node("second"), Node("third"), Node("fourth")),
                Entry("hall", "persuade", admitted: true),
                Route("hall", "vault", SiteAffordance.LockedBarrier),
                Route("hall", "first"),
                Route("hall", "second"),
                Route("hall", "third"),
                Route("hall", "fourth"));

            yield return Weigh(
                Sketch(
                    Node("hall"), Node("vault", SiteAffordance.EvidenceCache),
                    Node("strongroom", SiteAffordance.EvidenceCache)),
                Entry("hall", "persuade", admitted: true),
                Route("hall", "vault", SiteAffordance.LockedBarrier),
                Route("hall", "strongroom", SiteAffordance.HiddenPassage));

            SandboxVanillaState cannotRead = new SandboxVanillaState(EntityId.None);
            cannotRead.SetCapability(VanillaCapability.ReadPlaceContents, false);
            yield return Weigh(
                cannotRead,
                Sketch(Node("shaft"), Node("vault", SiteAffordance.EvidenceCache)),
                Entry("shaft", "mine_bypass", admitted: false, SiteAffordance.DiggableBypass),
                Route("shaft", "vault", SiteAffordance.DiggableBypass));

            yield return Weigh(
                Sketch(Node("front"), Node("vault", SiteAffordance.EvidenceCache)),
                Entry("front", "persuade", admitted: true),
                Route("front", "vault", SiteAffordance.HiddenPassage));
        }

        private static void AssertReadable(SiteCandidateSelection selection, SiteFlaw flaw, string detail)
        {
            string report = NarrativeInspector.DescribeSiteCandidates(selection);

            Assert.Contains(flaw.ToString(), report);
            Assert.Contains(detail, report);
            Assert.Contains("refused", report);

            bool found = false;
            foreach (SitePlanCandidate candidate in selection.Considered)
            {
                foreach (SiteCandidateFlaw reason in candidate.Flaws)
                {
                    if (reason.Kind == flaw)
                    {
                        found = true;
                        Assert.NotEqual(string.Empty, reason.Reason);
                    }
                }
            }

            Assert.True(found, "no plan was refused for " + flaw);
        }

        private static SiteCandidateSelection Draw(string grammarId, SiteAffordance objective, ulong seed = 7)
        {
            return Draw(Library().Get(grammarId), objective, seed);
        }

        private static SiteCandidateSelection Draw(SiteGrammar grammar, SiteAffordance objective, ulong seed)
        {
            Assert.NotNull(grammar);
            return SiteCandidates.Select(
                grammar, objective, seed, SiteCandidates.DefaultBatch, Actions(), Build());
        }

        private static SiteCandidateSelection Weigh(
            IVanillaState build, SiteGrammar grammar, SiteAffordance objective)
        {
            return SiteCandidates.Select(
                grammar, objective, 7, SiteCandidates.DefaultBatch, Actions(), build);
        }

        private static SiteCandidateSelection Weigh(SiteGrammar sketch, params SiteRouteSpec[] routes)
        {
            return Weigh(Build(), sketch, routes);
        }

        /// <summary>
        /// One sketched plan, weighed on its own. A batch of one, because a sketch has no optional
        /// parts to draw and a second seed would compose the same place again.
        /// </summary>
        private static SiteCandidateSelection Weigh(
            IVanillaState build, SiteGrammar sketch, params SiteRouteSpec[] routes)
        {
            SiteGrammar grammar = new SiteGrammar(
                sketch.Id, sketch.SiteType, sketch.Restricted, sketch.Nodes, routes);
            return SiteCandidates.Select(grammar, SiteAffordance.EvidenceCache, 7, 1, Actions(), build);
        }

        /// <summary>
        /// A place sketched in code rather than authored, for the shapes no shipped grammar should
        /// be bent into. Everything is required, so composition draws the whole of it.
        /// </summary>
        private static SiteGrammar Sketch(params SiteNodeSpec[] nodes)
        {
            return new SiteGrammar("sketch", "hideout", true, nodes, new SiteRouteSpec[0]);
        }

        private static SiteNodeSpec Node(string id, params SiteAffordance[] affordances)
        {
            return new SiteNodeSpec(id, true, affordances, string.Empty);
        }

        private static SiteRouteSpec Entry(
            string to, string actionId, bool admitted, params SiteAffordance[] affordances)
        {
            return new SiteRouteSpec(SiteGrammar.Outside, to, actionId, admitted, affordances);
        }

        private static SiteRouteSpec Route(string from, string to, params SiteAffordance[] affordances)
        {
            return new SiteRouteSpec(from, to, string.Empty, false, affordances);
        }

        private static ActionRegistry Actions() => StandardActions.CreateRegistry();

        private static IVanillaState Build() => new SandboxVanillaState(EntityId.None);

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
    }
}
