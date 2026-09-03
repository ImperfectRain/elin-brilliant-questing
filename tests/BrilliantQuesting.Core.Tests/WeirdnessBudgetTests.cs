using System;
using System.Collections.Generic;
using System.Linq;
using BrilliantQuesting.Dialogue;
using BrilliantQuesting.Foundation;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-079. A weirdness budget adds no new way of choosing a fragment - like BQ-078's history,
    /// it only narrows the pool <see cref="DialogueFragment.Fits"/> and its neighbours already
    /// built. The step's own done-when is two claims: a generated set of ceilings measurably lands
    /// mostly at Mundane through DistinctlyElin (CD §22.2), and nothing ever admits two unrelated
    /// absurd premises into the same budget.
    /// </summary>
    public class WeirdnessBudgetTests
    {
        // -- DialogueWeirdness reads only its own tags, and reads them correctly ---------------------

        [Fact]
        public void AFragmentWithNoWeirdnessTagReadsAsMundane()
        {
            Assert.Equal(WeirdnessLevel.Mundane, DialogueWeirdness.LevelOf(Array.Empty<string>()));
            Assert.Equal(WeirdnessLevel.Mundane, DialogueWeirdness.LevelOf(new[] { DialogueVocabulary.Cultivation, DialogueManners.Pleading }));
            Assert.Null(DialogueWeirdness.CategoryOf(new[] { DialogueVocabulary.Cultivation }));
        }

        [Fact]
        public void LevelOfTakesTheHighestLevelTagPresent()
        {
            Assert.Equal(
                WeirdnessLevel.AbsurdPremiseCentral,
                DialogueWeirdness.LevelOf(new[] { DialogueWeirdness.Level1, DialogueWeirdness.Level3 }));
        }

        [Fact]
        public void CategoryOfReadsTheCategoryTagAndIgnoresEverythingElse()
        {
            string[] tags = { DialogueVocabulary.Trade, DialogueWeirdness.Domestic, DialogueWeirdness.Level3 };
            Assert.Equal(DialogueWeirdness.Domestic, DialogueWeirdness.CategoryOf(tags));
        }

        // -- FitsWeirdness only ever narrows an already-eligible pool --------------------------------

        [Fact]
        public void AnUnmarkedFragmentIsAlwaysAdmissibleWhateverTheBudget()
        {
            DialogueFragment plain = Fragment(Array.Empty<string>());

            Assert.True(plain.FitsWeirdness(null));
            Assert.True(plain.FitsWeirdness(new WeirdnessBudget(WeirdnessLevel.Mundane)));
            Assert.True(plain.FitsWeirdness(new WeirdnessBudget(WeirdnessLevel.FeverDream)));
        }

        [Fact]
        public void ANullBudgetAdmitsAnyWeirdnessTag()
        {
            DialogueFragment feverDream = Fragment(new[] { DialogueWeirdness.Cosmic, DialogueWeirdness.Level4 });
            Assert.True(feverDream.FitsWeirdness(null));
        }

        [Fact]
        public void ATaggedFragmentAboveTheCeilingIsNotAdmissible()
        {
            DialogueFragment distinctlyElin = Fragment(new[] { DialogueWeirdness.Level2 });
            WeirdnessBudget budget = new WeirdnessBudget(WeirdnessLevel.OddDetail);

            Assert.False(distinctlyElin.FitsWeirdness(budget));
        }

        [Fact]
        public void ATaggedFragmentAtOrBelowTheCeilingIsAdmissible()
        {
            DialogueFragment oddDetail = Fragment(new[] { DialogueWeirdness.Level1 });
            WeirdnessBudget budget = new WeirdnessBudget(WeirdnessLevel.DistinctlyElin);

            Assert.True(oddDetail.FitsWeirdness(budget));
        }

        [Fact]
        public void ASceneCappedAtMundaneAdmitsNoWeirdnessTagAtAll()
        {
            WeirdnessBudget ordinary = new WeirdnessBudget(WeirdnessLevel.Mundane);

            Assert.True(Fragment(Array.Empty<string>()).FitsWeirdness(ordinary));
            Assert.False(Fragment(new[] { DialogueWeirdness.Level1 }).FitsWeirdness(ordinary));
            Assert.False(Fragment(new[] { DialogueWeirdness.Domestic, DialogueWeirdness.Level3 }).FitsWeirdness(ordinary));
        }

        // -- the anti-stacking done-when: no budget ever admits two unrelated absurd premises --------

        [Fact]
        public void ASecondUnrelatedAbsurdPremiseIsRefusedOnceOneHasBeenAdmitted()
        {
            WeirdnessBudget budget = new WeirdnessBudget(WeirdnessLevel.FeverDream);
            DialogueFragment domestic = Fragment(new[] { DialogueWeirdness.Domestic, DialogueWeirdness.Level3 });
            DialogueFragment bureaucratic = Fragment(new[] { DialogueWeirdness.Bureaucratic, DialogueWeirdness.Level3 });

            Assert.True(budget.IsAdmissible(domestic));
            budget.Note(domestic);

            Assert.False(budget.IsAdmissible(bureaucratic));
        }

        [Fact]
        public void FurtherContentFromTheSameAlreadyCommittedPremiseStaysAdmissible()
        {
            WeirdnessBudget budget = new WeirdnessBudget(WeirdnessLevel.FeverDream);
            DialogueFragment first = Fragment(new[] { DialogueWeirdness.Domestic, DialogueWeirdness.Level3 });
            DialogueFragment second = Fragment(new[] { DialogueWeirdness.Domestic, DialogueWeirdness.Level4 });

            budget.Note(first);

            Assert.True(budget.IsAdmissible(second));
        }

        [Fact]
        public void OrdinaryAndOddDetailContentIsNeverCountedAsAPremiseForStacking()
        {
            WeirdnessBudget budget = new WeirdnessBudget(WeirdnessLevel.FeverDream);
            budget.Note(Fragment(new[] { DialogueWeirdness.Domestic, DialogueWeirdness.Level1 }));
            budget.Note(Fragment(Array.Empty<string>()));

            // Neither an odd detail nor plain content ever rises to AbsurdPremiseCentral, so no
            // category has been committed to yet and a genuine premise is still free to start.
            Assert.Null(budget.AdmittedCategory);
            Assert.True(budget.IsAdmissible(Fragment(new[] { DialogueWeirdness.Bureaucratic, DialogueWeirdness.Level3 })));
        }

        // -- the distribution done-when: a generated set of ceilings lands mostly at 0-2 (CD §22.2) --

        [Fact]
        public void AGeneratedSetOfCeilingsMostlyLandsAtMundaneThroughDistinctlyElin()
        {
            int[] counts = new int[5];
            const int Draws = 4000;
            for (ulong seed = 1; seed <= Draws; seed++)
            {
                WeirdnessLevel level = WeirdnessBudget.SelectLevel(new DeterministicRng(seed));
                counts[(int)level]++;
            }

            int low = counts[0] + counts[1] + counts[2];
            double lowFraction = (double)low / Draws;
            Assert.True(lowFraction >= 0.80, "only " + lowFraction.ToString("P1") + " of ceilings landed at 0-2");

            // "Rare fever-dream event": the top tier is the least common of the five, by a wide margin.
            Assert.True(counts[4] < counts[3], "FeverDream (" + counts[4] + ") was not rarer than AbsurdPremiseCentral (" + counts[3] + ")");
            Assert.True(counts[4] < counts[0], "FeverDream (" + counts[4] + ") was not rare relative to Mundane (" + counts[0] + ")");

            // Every level is reachable - a budget that could never reach FeverDream would not be a
            // 0-4 scale, just a smaller one wearing its label.
            Assert.True(counts.All(count => count > 0), "not every weirdness level appeared across " + Draws + " draws");
        }

        [Fact]
        public void SelectingALevelIsDeterministic()
        {
            WeirdnessLevel first = WeirdnessBudget.SelectLevel(new DeterministicRng(777));
            WeirdnessLevel second = WeirdnessBudget.SelectLevel(new DeterministicRng(777));

            Assert.Equal(first, second);
        }

        // -- wired into the realizer's own candidate pool, exactly where BQ-078's history is ---------

        [Fact]
        public void TheRealizersCandidatePoolDropsASecondPremiseOnceTheFirstIsNoted()
        {
            FragmentRealizationTests.Scene scene = FragmentRealizationTests.Scene.Create();
            DialogueFragment domesticPremise = Fragment(new[] { DialogueWeirdness.Domestic, DialogueWeirdness.Level3 }, "test.weird.domestic");
            DialogueFragment bureaucraticPremise = Fragment(new[] { DialogueWeirdness.Bureaucratic, DialogueWeirdness.Level3 }, "test.weird.bureaucratic");
            Assert.True(scene.Realizer.Library.Register(domesticPremise));
            Assert.True(scene.Realizer.Library.Register(bureaucraticPremise));

            RealizationRequest request = scene.WitnessAnswers();
            WeirdnessBudget budget = new WeirdnessBudget(WeirdnessLevel.FeverDream);
            request.WeirdnessBudget = budget;

            IReadOnlyList<DialogueFragment> before = scene.Realizer.Candidates(FragmentPosition.Modifier, request);
            Assert.Contains(domesticPremise, before);
            Assert.Contains(bureaucraticPremise, before);

            budget.Note(domesticPremise);

            IReadOnlyList<DialogueFragment> after = scene.Realizer.Candidates(FragmentPosition.Modifier, request);
            Assert.Contains(domesticPremise, after);
            Assert.DoesNotContain(bureaucraticPremise, after);
        }

        [Fact]
        public void ARequestWithNoWeirdnessBudgetBehavesExactlyAsBeforeThisStep()
        {
            FragmentRealizationTests.Scene scene = FragmentRealizationTests.Scene.Create();
            RealizationRequest request = scene.WitnessAnswers();
            Assert.Null(request.WeirdnessBudget);

            for (ulong seed = 1; seed <= 10; seed++)
            {
                request.Rng = new DeterministicRng(seed);
                RealizedLine line = scene.Realizer.Realize(request);
                Assert.True(line.Rendered, line.Refusal);
                Assert.Equal(request.Act.Signature, line.Meaning);
            }
        }

        private static DialogueFragment Fragment(IReadOnlyList<string> tags, string id = "test.fragment")
        {
            return new DialogueFragment(
                id,
                FragmentPosition.Modifier,
                "Text.",
                requires: null,
                forbids: null,
                toneTags: null,
                tags: tags,
                repetitionGroup: null,
                slots: null);
        }
    }
}
