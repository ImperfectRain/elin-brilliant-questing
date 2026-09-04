using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BrilliantQuesting.Content;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Lab.Scenes;
using BrilliantQuesting.Storylets;
using Xunit;

namespace BrilliantQuesting.Lab.Tests
{
    /// <summary>
    /// The <c>scene</c> scenario's situation fixtures: that each one builds, that each one is a
    /// starting world the production engine can actually find a scene in, and that an id nobody
    /// registered is refused with something a reader can act on.
    ///
    /// What is deliberately *not* asserted is which storylet plays, who is cast or what anybody
    /// says. A fixture authors starting state and stops; everything after that is the engine's, and
    /// a test that pinned the outcome would be pinning the simulation to today's dice.
    /// </summary>
    public class SceneFixtureTests
    {
        /// <summary>
        /// Each registered situation names the predicate its focus fact actually carries. That is
        /// the whole of why a fixture is listed: `--list-situations` prints the predicate as the
        /// thing a storylet's FocusPredicate has to match, and a fixture whose focus said something
        /// else would be advertising scenes it cannot reach.
        /// </summary>
        [Fact]
        public void EveryRegisteredSituationBuildsAWorldWhoseFocusIsThePredicateItAdvertises()
        {
            Assert.NotEmpty(SceneSituations.All);

            foreach (SceneSituation situation in SceneSituations.All)
            {
                SceneFixture fixture = situation.Build(15UL);

                Assert.NotNull(fixture.World);
                Assert.NotNull(fixture.Vanilla);
                Assert.NotNull(fixture.Thread);
                Assert.False(fixture.Player.IsNone, situation.Id + " has no player in the world");

                Fact focus = fixture.Focus;
                Assert.True(focus != null, situation.Id + " built no focus fact");
                Assert.Equal(situation.Predicate, focus.Predicate);
                Assert.Equal(TruthState.True, focus.Truth);

                // The focus has to belong to the thread, because every shipped storylet says so
                // with a FactBelongsToThread precondition.
                Assert.Contains(fixture.FocusFactId, fixture.Thread.FactIds);
                Assert.True(fixture.Thread.IsLive, situation.Id + " starts on a settled thread");
            }
        }

        /// <summary>
        /// The claim the whole probe rests on: from each fixture's starting state alone, the
        /// production engine finds at least one routed storylet it can cast. A fixture that could
        /// never produce a scene is a menu entry that wastes the reader's time.
        /// </summary>
        [Fact]
        public void EveryRegisteredSituationYieldsAtLeastOneRoutedStoryletTheEngineCanCast()
        {
            StoryletEngine engine = ShippedEngine();

            foreach (SceneSituation situation in SceneSituations.All)
            {
                SceneFixture fixture = situation.Build(15UL);

                List<StoryletOpportunity> routed = engine
                    .Find(new StoryletCastingContext(
                        fixture.World, fixture.Vanilla, fixture.Thread, fixture.FocusFactId))
                    .Where(o => o.Definition.IsRouted)
                    .ToList();

                Assert.True(routed.Count > 0, situation.Id + " supports no routed storylet at all");
                Assert.All(routed, o => Assert.True(o.RoleBindings.Count >= 2, o.Definition.Id + " is a scene with nobody in it"));
            }
        }

        /// <summary>
        /// The situations the pass was asked for, named. Registering them is the deliverable, so a
        /// silent rename or removal should fail here rather than in somebody's terminal.
        /// </summary>
        [Theory]
        [InlineData("theft", FactPredicates.Stole)]
        [InlineData("debt", FactPredicates.Owes)]
        [InlineData("shortage", FactPredicates.Needs)]
        [InlineData("extortion", FactPredicates.Extorted)]
        [InlineData("danger", FactPredicates.AtRisk)]
        public void TheShippedSituationsAreRegisteredUnderTheirOwnPredicate(string id, string predicate)
        {
            SceneSituation situation = SceneSituations.Find(id);

            Assert.True(situation != null, "no situation registered as " + id);
            Assert.Equal(predicate, situation.Predicate);
            Assert.False(string.IsNullOrWhiteSpace(situation.Summary), id + " has no one-line description");
        }

        /// <summary>
        /// Each fixture is expected to reach the storylet family it was chosen for. Asserted as
        /// "this one is among what the engine offers", never as "this one plays": which scene a
        /// caller then chooses is the caller's business.
        /// </summary>
        [Theory]
        [InlineData("theft", "storylet.public_accusation")]
        [InlineData("debt", "storylet.debt_called_in")]
        [InlineData("shortage", "storylet.shortage_appeal")]
        [InlineData("extortion", "storylet.extortion_pressure")]
        [InlineData("danger", "storylet.endangered_neighbour")]
        public void EachSituationReachesTheStoryletFamilyItWasChosenFor(string id, string storyletId)
        {
            SceneFixture fixture = SceneSituations.Find(id).Build(15UL);

            IReadOnlyList<StoryletOpportunity> opportunities = ShippedEngine().Find(new StoryletCastingContext(
                fixture.World, fixture.Vanilla, fixture.Thread, fixture.FocusFactId));

            Assert.Contains(storyletId, opportunities.Select(o => o.Definition.Id));
        }

        /// <summary>The same fixture, the same seed, the same world - a probe nobody can replay is not one.</summary>
        [Fact]
        public void AFixtureBuiltTwiceFromOneSeedIsTheSameWorld()
        {
            foreach (SceneSituation situation in SceneSituations.All)
            {
                SceneFixture first = situation.Build(99UL);
                SceneFixture second = situation.Build(99UL);

                Assert.Equal(first.FocusFactId, second.FocusFactId);
                Assert.Equal(
                    Names(first),
                    Names(second));
            }
        }

        [Fact]
        public void AnUnknownSituationIsRefusedAndTheKnownOnesAreNamed()
        {
            Assert.Null(SceneSituations.Find("volcano"));
            Assert.Null(SceneSituations.Find(null));
            Assert.Null(SceneSituations.Find("  "));

            string known = SceneSituations.KnownIds();
            foreach (SceneSituation situation in SceneSituations.All)
            {
                Assert.Contains(situation.Id, known);
            }
        }

        [Fact]
        public void SituationIdsAreMatchedWithoutRegardToCaseAndSurroundingSpace()
        {
            Assert.Equal("theft", SceneSituations.Find("THEFT").Id);
            Assert.Equal("debt", SceneSituations.Find("  debt  ").Id);
        }

        private static string Names(SceneFixture fixture)
        {
            return string.Join(",", fixture.Thread.ParticipantIds.Select(id => fixture.World.Registry.NameOf(id)));
        }

        private static StoryletEngine ShippedEngine()
        {
            ContentBundleLoadResult loaded = ContentBundleLoader.LoadFile(
                Path.Combine(RepositoryRoot(), "Package", "content.bqc"));
            Assert.Empty(loaded.Diagnostics);

            IReadOnlyList<ContentDiagnostic> diagnostics;
            StoryletEngine engine = StoryletContent.CreateEngine(loaded.Bundle, out diagnostics);
            Assert.Empty(diagnostics);
            return engine;
        }

        private static string RepositoryRoot()
        {
            DirectoryInfo directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "ElinBrilliantQuesting.sln")))
            {
                directory = directory.Parent;
            }

            Assert.NotNull(directory);
            return directory.FullName;
        }
    }
}
