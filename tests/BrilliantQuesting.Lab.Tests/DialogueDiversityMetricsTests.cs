using System;
using System.Collections.Generic;
using System.IO;
using BrilliantQuesting.Dialogue;
using BrilliantQuesting.Lab.Cli;
using BrilliantQuesting.Lab.Playground.Sweep;
using Xunit;

namespace BrilliantQuesting.Lab.Tests
{
    /// <summary>
    /// The diversity metrics, held to fixed fixtures rather than to the shipped corpus.
    ///
    /// Every fixture here is built by hand: no <see cref="Playground.PlaygroundRun"/>, no fragment
    /// library and no content file. That is deliberate and it is the property this file exists to
    /// prove - <see cref="DialogueDiversityMetrics.Compute"/> takes samples, not rows, precisely so
    /// its arithmetic can be checked against inputs nobody had to run a conversation to build and
    /// nobody has to keep in sync with whatever the corpus says next week. A test that asserted a
    /// specific count off the shipped library would be exactly the brittle threshold this
    /// instrument itself is built to avoid.
    /// </summary>
    public class DialogueDiversityMetricsTests
    {
        [Fact]
        public void ThreeIdenticalLinesReportNoDiversityAtAll()
        {
            List<DialogueDiversitySample> samples = new List<DialogueDiversitySample>();
            for (int i = 0; i < 3; i++)
            {
                samples.Add(Sample("voice-" + i, "No. Please ask someone else.", "core.refuse", Core("core.refuse")));
            }

            DialogueDiversityReport report = DialogueDiversityMetrics.Compute(samples);

            Assert.Equal(3, report.Samples);
            Assert.Equal(3, report.Realized);
            Assert.Equal(0.0, report.UnrealizedRate);
            Assert.Equal(1, report.DistinctCores);
            Assert.True(report.DistinctCoreRate < 0.5, "three identical cores should read as low distinctness");
            Assert.Equal(1.0, report.FragmentOverlapRate);
            Assert.Equal(1.0, report.AverageTextualOverlap);
            Assert.Equal(1.0, report.MaxTextualOverlap);
        }

        [Fact]
        public void ThreeUnrelatedLinesReportFullOverlapFreedom()
        {
            List<DialogueDiversitySample> samples = new List<DialogueDiversitySample>
            {
                Sample("formal", "Zephyr crimson lantern falls quietly beyond.", "core.refuse.formal", Core("core.refuse.formal")),
                Sample("warm", "Wolves gather beneath broken windmill towers.", "core.refuse.warm", Core("core.refuse.warm")),
                Sample("blunt", "No.", "core.refuse.blunt", Core("core.refuse.blunt"))
            };

            DialogueDiversityReport report = DialogueDiversityMetrics.Compute(samples);

            Assert.Equal(3, report.DistinctCores);
            Assert.Equal(1.0, report.DistinctCoreRate);
            Assert.Equal(0, report.FragmentsSharedAcrossProfiles);
            Assert.Equal(0.0, report.FragmentOverlapRate);
            Assert.Equal(0.0, report.AverageTextualOverlap);
            Assert.Equal(0.0, report.MaxTextualOverlap);
        }

        [Fact]
        public void AnUnrealizedSampleCountsTowardTheFallbackRateAndNothingElse()
        {
            List<DialogueDiversitySample> samples = new List<DialogueDiversitySample>
            {
                Sample("spoke", "Right. Please ask someone else.", "core.refuse.polite", Core("core.refuse.polite")),
                new DialogueDiversitySample("silent", realized: false, text: null, core: null, fragments: null)
            };

            DialogueDiversityReport report = DialogueDiversityMetrics.Compute(samples);

            Assert.Equal(2, report.Samples);
            Assert.Equal(1, report.Realized);
            Assert.Equal(0.5, report.UnrealizedRate);
            Assert.Equal("50% (1 of 2)", report.UnrealizedSummary);

            // The one realized line is its own core, on its own, and the pairwise-overlap
            // arithmetic has nothing to compare it against.
            Assert.Equal(1, report.DistinctCores);
            Assert.Equal(0.0, report.AverageTextualOverlap);
            Assert.Equal(0.0, report.MaxTextualOverlap);
        }

        [Fact]
        public void ASignatureFragmentSpokenByTwoProfilesIsNamedAsReused()
        {
            DialogueDiversityFragment shared = new DialogueDiversityFragment(
                "close.a.line.worth.quoting", FragmentPosition.Closer, DialogueMemorability.Signature, "closer.done");
            DialogueDiversityFragment onlyUtility = new DialogueDiversityFragment(
                "open.plain", FragmentPosition.Opener, DialogueMemorability.Utility, string.Empty);

            List<DialogueDiversitySample> samples = new List<DialogueDiversitySample>
            {
                Sample("rival", "Say what you like.", "core.a", new[] { onlyUtility, Core("core.a"), shared }),
                Sample("friend", "Fine, say what you like.", "core.b", new[] { Core("core.b"), shared })
            };

            DialogueDiversityReport report = DialogueDiversityMetrics.Compute(samples);

            Assert.Contains("close.a.line.worth.quoting", report.ReusedMemorableFragments);
            Assert.Equal(2, report.MemorableFragmentUses); // one memorable slot filled per profile that spoke it
            Assert.Contains("reused across profiles", report.MemorableSummary);
            Assert.Contains("close.a.line.worth.quoting", report.MemorableSummary);
        }

        [Fact]
        public void AUtilityFragmentSharedByEveryoneIsNeverReportedAsMemorable()
        {
            DialogueDiversityFragment ordinary = new DialogueDiversityFragment(
                "mod.plain", FragmentPosition.Modifier, DialogueMemorability.Utility, "plain.group");

            List<DialogueDiversitySample> samples = new List<DialogueDiversitySample>
            {
                Sample("a", "One thing.", "core.a", new[] { Core("core.a"), ordinary }),
                Sample("b", "Another thing.", "core.b", new[] { Core("core.b"), ordinary })
            };

            DialogueDiversityReport report = DialogueDiversityMetrics.Compute(samples);

            Assert.Empty(report.ReusedMemorableFragments);
            Assert.Equal(0, report.MemorableFragmentUses);
            Assert.Equal(1, report.FragmentsSharedAcrossProfiles); // the fragment itself is still counted as shared
        }

        [Fact]
        public void AStructuralGroupSharedAcrossProfilesIsFlagged()
        {
            DialogueDiversityFragment closerA = new DialogueDiversityFragment(
                "close.a", FragmentPosition.Closer, DialogueMemorability.Utility, "closer.done");
            DialogueDiversityFragment closerB = new DialogueDiversityFragment(
                "close.b", FragmentPosition.Closer, DialogueMemorability.Utility, "closer.done");

            List<DialogueDiversitySample> samples = new List<DialogueDiversitySample>
            {
                Sample("terse", "No.", "core.a", new[] { Core("core.a"), closerA }),
                Sample("warm", "I would rather not, sorry.", "core.b", new[] { Core("core.b"), closerB })
            };

            DialogueDiversityReport report = DialogueDiversityMetrics.Compute(samples);

            // Different fragment ids, so no fragment overlap - but the same repetition group, so
            // the line's shape recurred even though its content did not.
            Assert.Equal(0, report.FragmentsSharedAcrossProfiles);
            Assert.Contains("closer.done", report.ReusedStructuralGroups);
        }

        [Fact]
        public void ASharedGroupOutsideOpenerCoreCloserIsNotCountedAsStructural()
        {
            DialogueDiversityFragment modifierA = new DialogueDiversityFragment(
                "mod.a", FragmentPosition.Modifier, DialogueMemorability.Utility, "shared.group");
            DialogueDiversityFragment modifierB = new DialogueDiversityFragment(
                "mod.b", FragmentPosition.Modifier, DialogueMemorability.Utility, "shared.group");

            List<DialogueDiversitySample> samples = new List<DialogueDiversitySample>
            {
                Sample("one", "One thing.", "core.a", new[] { Core("core.a"), modifierA }),
                Sample("two", "Another thing.", "core.b", new[] { Core("core.b"), modifierB })
            };

            DialogueDiversityReport report = DialogueDiversityMetrics.Compute(samples);

            Assert.Empty(report.ReusedStructuralGroups);
        }

        [Fact]
        public void TextualOverlapIgnoresCaseAndPunctuationButNotWordChoice()
        {
            List<DialogueDiversitySample> samples = new List<DialogueDiversitySample>
            {
                Sample("a", "Take the answer and let me return to my day.", "core.a", Core("core.a")),
                Sample("b", "TAKE THE ANSWER, AND LET ME RETURN TO MY DAY!", "core.b", Core("core.b")),
                Sample("c", "Come back if you learn more.", "core.c", Core("core.c"))
            };

            DialogueDiversityReport report = DialogueDiversityMetrics.Compute(samples);

            Assert.Equal(1.0, report.MaxTextualOverlap);
            Assert.True(report.AverageTextualOverlap < 1.0, "the unrelated third line should pull the average down");
        }

        [Fact]
        public void LineLengthIsWordsNotCharacters()
        {
            List<DialogueDiversitySample> samples = new List<DialogueDiversitySample>
            {
                Sample("short", "No.", "core.a", Core("core.a")),
                Sample("long", "I would rather not answer that particular question today.", "core.b", Core("core.b"))
            };

            DialogueDiversityReport report = DialogueDiversityMetrics.Compute(samples);

            Assert.Equal(1, report.LineLengthMin);
            Assert.Equal(9, report.LineLengthMax);
            Assert.Equal(5.0, report.LineLengthMean);
        }

        [Fact]
        public void EmptyInputProducesAZeroedReportRatherThanDividingByZero()
        {
            DialogueDiversityReport report = DialogueDiversityMetrics.Compute(new DialogueDiversitySample[0]);

            Assert.Equal(0, report.Samples);
            Assert.Equal(0, report.Realized);
            Assert.Equal(0.0, report.UnrealizedRate);
            Assert.Equal(0.0, report.DistinctCoreRate);
            Assert.Equal(0.0, report.FragmentOverlapRate);
            Assert.Equal(0.0, report.MemorableFragmentShare);
            Assert.Equal(0.0, report.AverageTextualOverlap);
            Assert.NotNull(report.LineLengthSummary);
        }

        [Fact]
        public void NullSamplesBehaveExactlyLikeAnEmptyList()
        {
            DialogueDiversityReport report = DialogueDiversityMetrics.Compute(null);

            Assert.Equal(0, report.Samples);
            Assert.Equal("nothing realized", report.CoreSummary);
        }

        // -- wiring: the same figures reach the report and the JSON without recomputing anything --

        [Fact]
        public void TheReportPrintsADiversitySectionForEveryFamily()
        {
            string report = Report("run", "playground-sweep", "--axis", "voice");

            Assert.Contains("dialogue diversity", report, StringComparison.Ordinal);
            Assert.Contains("unrealized rate:", report, StringComparison.Ordinal);
            Assert.Contains("distinct cores:", report, StringComparison.Ordinal);
            Assert.Contains("fragment overlap:", report, StringComparison.Ordinal);
            Assert.Contains("memorable fragments:", report, StringComparison.Ordinal);
            Assert.Contains("structural reuse:", report, StringComparison.Ordinal);
            Assert.Contains("textual overlap:", report, StringComparison.Ordinal);
            Assert.Contains("line length:", report, StringComparison.Ordinal);
        }

        [Fact]
        public void TheJsonFormCarriesTheSameDiversityFigures()
        {
            string json = Report("run", "playground-sweep", "--axis", "voice", "--json");

            Assert.Contains("\"diversity\": {", json, StringComparison.Ordinal);
            Assert.Contains("\"samples\":", json, StringComparison.Ordinal);
            Assert.Contains("\"distinctCoreRate\":", json, StringComparison.Ordinal);
            Assert.Contains("\"reusedMemorableFragments\":", json, StringComparison.Ordinal);
            Assert.Contains("\"reusedStructuralGroups\":", json, StringComparison.Ordinal);
        }

        [Fact]
        public void TheDiversityBlockIsReproducibleLikeEveryOtherReading()
        {
            PlaygroundSweepResult first = PlaygroundSweepReport.Evaluate(
                PlaygroundSweepAxes.Default().Find("voice"), 15UL);
            PlaygroundSweepResult second = PlaygroundSweepReport.Evaluate(
                PlaygroundSweepAxes.Default().Find("voice"), 15UL);

            Assert.Equal(first.Diversity.Samples, second.Diversity.Samples);
            Assert.Equal(first.Diversity.DistinctCores, second.Diversity.DistinctCores);
            Assert.Equal(first.Diversity.FragmentOverlapRate, second.Diversity.FragmentOverlapRate);
            Assert.Equal(first.Diversity.AverageTextualOverlap, second.Diversity.AverageTextualOverlap);
        }

        /// <summary>
        /// The voice family's own fix (BQ-150): a signature closer marked for an expansive,
        /// flowing speaker now actually demands one, so the same "loud" farewell can no longer
        /// turn up for contrasting voices that never asked for it.
        /// </summary>
        [Fact]
        public void NoMemorableFragmentIsSharedAcrossContrastingVoicesInTheShippedCorpus()
        {
            PlaygroundSweepResult voices = PlaygroundSweepReport.Evaluate(
                PlaygroundSweepAxes.Default().Find("voice"), 15UL);

            Assert.Empty(voices.Diversity.ReusedMemorableFragments);
        }

        // -- scaffolding -------------------------------------------------------------------------

        private static DialogueDiversityFragment Core(string id)
        {
            return new DialogueDiversityFragment(id, FragmentPosition.Core, DialogueMemorability.Utility, string.Empty);
        }

        private static DialogueDiversitySample Sample(string profile, string text, string coreId, DialogueDiversityFragment core)
        {
            return Sample(profile, text, coreId, new[] { core });
        }

        private static DialogueDiversitySample Sample(
            string profile, string text, string coreId, IReadOnlyList<DialogueDiversityFragment> fragments)
        {
            return new DialogueDiversitySample(profile, realized: true, text: text, core: coreId, fragments: fragments);
        }

        private static string Report(params string[] args)
        {
            StringWriter output = new StringWriter();
            StringWriter error = new StringWriter();
            Assert.Equal(LabExit.Success, LabCommandLine.Execute(args, output, error));
            return output.ToString();
        }
    }
}
