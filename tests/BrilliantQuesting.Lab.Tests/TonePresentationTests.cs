using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BrilliantQuesting.Checks;
using BrilliantQuesting.Content;
using BrilliantQuesting.Dialogue;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Lab.Cli;
using BrilliantQuesting.Lab.Scenes;
using BrilliantQuesting.Storylets;
using Xunit;

namespace BrilliantQuesting.Lab.Tests
{
    public class TonePresentationTests
    {
        [Theory]
        [InlineData("theft")]
        [InlineData("debt")]
        [InlineData("shortage")]
        [InlineData("extortion")]
        public void IntendedRegisterCannotChangePresentationOfTheSameScene(string situation)
        {
            string ordinary = Present(situation, false);
            string sincere = Present(situation, true);
            Assert.False(string.IsNullOrWhiteSpace(ordinary));
            Assert.Equal(ordinary, sincere);
        }

        [Fact]
        public void UnwordedScenesEmitNoFramingOrReplacementLineInEitherRegister()
        {
            Assert.Equal(string.Empty, Present("danger", false));
            Assert.Equal(string.Empty, Present("danger", true));
        }

        [Fact]
        public void ReviewCommandKeepsDiagnosticsOutOfTheReviewPacket()
        {
            var output = new StringWriter();
            var error = new StringWriter();
            Assert.Equal(LabExit.Success, LabCommandLine.Execute(
                new[] { "run", "scene", "--seed", "15", "--dry", "--presentation" }, output, error));
            Assert.Equal(string.Empty, error.ToString());
            string text = output.ToString();
            Assert.False(string.IsNullOrWhiteSpace(text));
            Assert.DoesNotContain("storylet.", text);
            Assert.DoesNotContain("sincerity", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("focus:", text);
            Assert.DoesNotContain("act:", text);
            Assert.DoesNotContain("No presentation acknowledged", text);
        }

        private static string Present(string situation, bool sincere)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "ElinBrilliantQuesting.sln")))
                directory = directory.Parent;
            Assert.NotNull(directory);
            var loaded = ContentBundleLoader.LoadFile(Path.Combine(directory.FullName, "Package", "content.bqc"));
            Assert.Empty(loaded.Diagnostics);
            IReadOnlyList<ContentDiagnostic> diagnostics;
            var engine = StoryletContent.CreateEngine(loaded.Bundle, out diagnostics);
            Assert.Empty(diagnostics);
            var library = DialogueFragmentContent.CreateLibrary(loaded.Bundle, out diagnostics);
            Assert.Empty(diagnostics);
            var fixture = SceneSituations.Find(situation).Build(15UL);
            var opportunities = engine.Find(new StoryletCastingContext(
                fixture.World, fixture.Vanilla, fixture.Thread, fixture.FocusFactId));
            var output = new StringWriter();
            var router = new StoryletRouter(new DialogueRealizer(library), new VanillaStyleCheckResolver(fixture.Vanilla));
            foreach (var opportunity in opportunities.Where(o => o.Definition.IsRouted))
            {
                // The controlled variable is metadata, never guessed from the authored words.
                opportunity.Definition.ToneTags.Remove(SincerityBudget.RareSincerityTag);
                if (sincere) opportunity.Definition.ToneTags.Add(SincerityBudget.RareSincerityTag);
                // Earn admission independently for each comparison; pacing is BQ-127's contract.
                var ordinary = new StoryletDefinition("review.allowance");
                for (int i = 0; i < 9; i++) engine.SincerityBudget.TryPresent(ordinary, () => true);
                int before = output.GetStringBuilder().Length;
                bool acknowledged = engine.TryPresent(opportunity, () =>
                {
                    var play = router.Play(opportunity, new StoryletPlayContext(fixture.World, fixture.Vanilla, fixture.Thread)
                    {
                        Rng = new DeterministicRng(15UL),
                        InPublic = opportunity.Definition.ToneTags.Contains("public"),
                        ApplyConsequences = false
                    });
                    var repeat = new StringWriter();
                    bool delivered = ScenePresentation.Write(output, fixture.World, play);
                    ScenePresentation.Write(repeat, fixture.World, play);
                    Assert.Equal(output.ToString().Substring(before), repeat.ToString());
                    return delivered;
                });
                Assert.Equal(output.GetStringBuilder().Length > before, acknowledged);
            }
            return output.ToString();
        }
    }
}
