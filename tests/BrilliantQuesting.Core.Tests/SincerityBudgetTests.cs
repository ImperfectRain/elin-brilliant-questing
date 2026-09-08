using System;
using System.Collections.Generic;
using System.Linq;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Content;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Situations;
using BrilliantQuesting.Storylets;
using Xunit;

namespace BrilliantQuesting.Tests
{
    public class SincerityBudgetTests
    {
        private static StoryletDefinition Scene(bool sincere, string id = "storylet.test")
        {
            var scene = new StoryletDefinition(id);
            if (sincere) scene.ToneTags.Add(SincerityBudget.RareSincerityTag);
            return scene;
        }

        [Fact]
        public void LongSessionHoldsCeilingAtEveryPrefixAndSpacesSincereScenes()
        {
            var lab = TheftLaboratory.Create();
            var engine = new StoryletEngine();
            engine.Register(Scene(true, "storylet.tender"));
            engine.Register(Scene(false, "storylet.ordinary"));
            var context = new StoryletCastingContext(lab.World, lab.Vanilla,
                lab.Situation.Thread, lab.Situation.TheftFactId);
            string before = WorldStateSerializer.Save(lab.World);
            int lastSincere = -1;
            for (int i = 0; i < 1000; i++)
            {
                var choices = engine.FindForPresentation(context);
                var chosen = choices.First(); // Always prefer sincere when the director admits it.
                Assert.True(engine.TryPresent(chosen, () => true));
                if (SincerityBudget.IsRareSincerity(chosen.Definition))
                {
                    Assert.True(i - lastSincere >= 10);
                    lastSincere = i;
                }
                Assert.True(engine.SincerityBudget.Rate <= 0.1);
            }
            Assert.Equal(100, engine.SincerityBudget.SincereScenes);
            Assert.Equal(before, WorldStateSerializer.Save(lab.World));
        }

        [Fact]
        public void OrdinarySurplusCannotBankABurstAndStaleProposalIsRechecked()
        {
            var engine = new StoryletEngine();
            var ordinary = Scene(false);
            var sincere = StoryletOpportunity.Available(Scene(true), EntityId.None, new Dictionary<string, EntityId>());
            for (int i = 0; i < 100; i++) Assert.True(engine.SincerityBudget.TryPresent(ordinary, () => true));
            Assert.True(engine.TryPresent(sincere, () => true));
            Assert.False(engine.TryPresent(sincere, () => throw new Exception("must not present")));
            Assert.Equal(101, engine.SincerityBudget.PresentedScenes);
        }

        [Fact]
        public void InspectionFailureAndExceptionsNeitherEarnNorSpendAllowance()
        {
            var budget = new SincerityBudget();
            var ordinary = Scene(false);
            Assert.Null(budget.Rate);
            Assert.Contains("unobserved", NarrativeInspector.DescribeSincerityBudget(budget));
            for (int i = 0; i < 20; i++)
            {
                Assert.True(budget.Allows(ordinary));
                Assert.False(budget.TryPresent(ordinary, () => false));
                Assert.Throws<InvalidOperationException>(() => budget.TryPresent(ordinary, () => throw new InvalidOperationException()));
            }
            Assert.Equal(0, budget.PresentedScenes);
            Assert.False(budget.Allows(Scene(true)));
            for (int i = 0; i < 9; i++) budget.TryPresent(ordinary, () => true);
            Assert.False(budget.TryPresent(Scene(true), () => false));
            Assert.True(budget.Allows(Scene(true)));
            string report = NarrativeInspector.DescribeSincerityBudget(budget);
            Assert.Equal(report, NarrativeInspector.DescribeSincerityBudget(budget));
            Assert.True(budget.TryPresent(Scene(true), () => true));
            Assert.Contains("1/10; rate=10.0", NarrativeInspector.DescribeSincerityBudget(budget));
        }

        [Fact]
        public void NestedPresentationCannotSpendTheSameAllowanceTwice()
        {
            var budget = new SincerityBudget();
            var ordinary = Scene(false);
            Assert.True(budget.TryPresent(ordinary, () =>
            {
                Assert.False(budget.TryPresent(ordinary, () => true));
                return true;
            }));
            Assert.Equal(1, budget.PresentedScenes);
        }

        [Fact]
        public void SemanticFiringDoesNotCountAsPresentationAndNewSessionHasNoAssumedExposure()
        {
            var lab = TheftLaboratory.Create();
            var engine = new StoryletEngine();
            engine.Register(Scene(true));
            var context = new StoryletCastingContext(lab.World, lab.Vanilla,
                lab.Situation.Thread, lab.Situation.TheftFactId);
            var opportunity = Assert.Single(engine.Find(context));
            Assert.Empty(engine.FindForPresentation(context));
            engine.Fire(opportunity, lab.Situation.Thread, lab.Vanilla.Now);
            Assert.Equal(0, engine.SincerityBudget.PresentedScenes);
            var restored = WorldStateSerializer.Load(WorldStateSerializer.Save(lab.World));
            Assert.Single(restored.Threads.Single().StoryletFirings);
            Assert.Null(new StoryletEngine().SincerityBudget.Rate);
        }

        [Fact]
        public void AuthoredSceneMetadataReachesTheDirectorThroughTheExistingBundle()
        {
            var bundle = new ContentBundle(ContentBundle.CurrentVersion, new[]
            {
                new ContentRecord("storylet.authored", "storylet", JsonValue.Parse(
                    "{\"toneTags\":[\"rare_sincerity\"],\"requiredRoles\":[{\"id\":\"speaker\",\"source\":\"Actor\"}],\"beats\":[{\"id\":\"opening\"}]}"))
            });
            var definitions = StoryletContent.LoadDefinitions(bundle, out var diagnostics);
            Assert.Empty(diagnostics);
            var scene = Assert.Single(definitions);
            Assert.True(SincerityBudget.IsRareSincerity(scene));
            Assert.False(new SincerityBudget().Allows(scene));
        }

        [Fact]
        public void UnavailableScenesCannotBePresentedOrEarnAllowance()
        {
            var engine = new StoryletEngine();
            var unavailable = StoryletOpportunity.Refused(Scene(false), EntityId.None, "no cast");
            Assert.False(engine.TryPresent(unavailable, () => throw new Exception("must not present")));
            Assert.Equal(0, engine.SincerityBudget.PresentedScenes);
        }

        [Fact]
        public void OrdinarySincereSpeechIsNotRareEmotionalContent()
        {
            var scene = Scene(false);
            scene.ToneTags.Add("sincere");
            scene.ToneTags.Add("somber");
            Assert.False(SincerityBudget.IsRareSincerity(scene));
            Assert.True(new SincerityBudget().Allows(scene));
        }
    }
}
