using System;
using System.Collections.Generic;
using System.Linq;
using BrilliantQuesting.Dialogue;
using BrilliantQuesting.Foundation;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-075. Voice profiles constrain fragment selection the same way a caller-supplied tone
    /// already did (BQ-074); what this step adds is a per-speaker source for that tone that is not
    /// the disclosure decision, the act or the personality weights, so the tests here mirror
    /// BQ-074's own: mostly that a voice narrows without ever creating.
    ///
    /// <list type="bullet">
    /// <item><see cref="VoiceProfile.RequestedTone"/> is a pure, deterministic function of its four
    /// axes and nothing else;</item>
    /// <item>the identical act, decision and personality render recognizably differently through
    /// two different voices;</item>
    /// <item>and every one of those renderings still carries the identical meaning.</item>
    /// </list>
    /// </summary>
    public class VoiceProfileTests
    {
        // -- RequestedTone is a pure mapping ---------------------------------------------------------

        [Fact]
        public void ANeutralVoiceRequestsNoTone()
        {
            Assert.Empty(VoiceProfile.Neutral.RequestedTone());
        }

        [Fact]
        public void EachAxisRequestsItsOwnTagAtItsExtremes()
        {
            Assert.Equal(new[] { DialogueTones.Formal }, new VoiceProfile { Formality = 1.0 }.RequestedTone());
            Assert.Equal(new[] { DialogueTones.Plain }, new VoiceProfile { Formality = 0.0 }.RequestedTone());

            Assert.Equal(new[] { DialogueTones.Curt }, new VoiceProfile { Directness = 1.0 }.RequestedTone());
            Assert.Equal(new[] { DialogueTones.Wary }, new VoiceProfile { Directness = 0.0 }.RequestedTone());

            Assert.Equal(new[] { DialogueTones.Warm }, new VoiceProfile { Warmth = 1.0 }.RequestedTone());
            Assert.Equal(new[] { DialogueTones.Cold }, new VoiceProfile { Warmth = 0.0 }.RequestedTone());

            Assert.Equal(new[] { DialogueTones.Wry }, new VoiceProfile { Sarcasm = 1.0 }.RequestedTone());

            // Sincerity is the unmarked baseline: nothing in DialogueTones says "sincere", so the
            // low end of Sarcasm asks for nothing rather than inventing a tag to carry it.
            Assert.Empty(new VoiceProfile { Sarcasm = 0.0 }.RequestedTone());
        }

        [Fact]
        public void AMiddlingValueOnAnAxisRequestsNothingOnThatAxis()
        {
            VoiceProfile middling = new VoiceProfile { Formality = 0.5, Directness = 0.5, Warmth = 0.5, Sarcasm = 0.5 };
            Assert.Empty(middling.RequestedTone());
        }

        [Fact]
        public void SeveralExtremeAxesRequestSeveralTags()
        {
            VoiceProfile blunt = new VoiceProfile { Directness = 1.0, Warmth = 0.0 };
            IReadOnlyList<string> tone = blunt.RequestedTone();

            Assert.Contains(DialogueTones.Curt, tone);
            Assert.Contains(DialogueTones.Cold, tone);
            Assert.Equal(2, tone.Count);
        }

        [Fact]
        public void RequestedToneIsDeterministic()
        {
            VoiceProfile voice = new VoiceProfile { Formality = 0.8, Directness = 0.1, Warmth = 0.9, Sarcasm = 0.7 };
            Assert.Equal(voice.RequestedTone(), voice.RequestedTone());
        }

        // -- the done-when ----------------------------------------------------------------------------

        /// <summary>
        /// The step's condition, in this codebase's terms: the identical refusal - same act, same
        /// disclosure decision, same speaker personality throughout - said through two voices comes
        /// out recognizably different, and every rendering still means the refusal it started as.
        /// </summary>
        [Fact]
        public void TheIdenticalRefusalSoundsDifferentThroughTwoVoices()
        {
            FragmentRealizationTests.Scene scene = FragmentRealizationTests.Scene.Create();
            RealizationRequest request = scene.ThiefRefuses();
            string meaning = request.Act.Signature;

            VoiceProfile blunt = new VoiceProfile { Directness = 1.0 };
            VoiceProfile gentle = new VoiceProfile { Warmth = 1.0 };

            HashSet<string> bluntLines = Rendered(scene, request, blunt, meaning);
            HashSet<string> gentleLines = Rendered(scene, request, gentle, meaning);

            Assert.NotEmpty(bluntLines);
            Assert.NotEmpty(gentleLines);
            Assert.True(
                bluntLines.Except(gentleLines).Any() || gentleLines.Except(bluntLines).Any(),
                "the two voices produced the identical set of lines: " + string.Join(" / ", bluntLines));
        }

        /// <summary>
        /// The other half: a voice narrows which fragment says the point, and never which point is
        /// said. Every candidate a blunt voice would use for the refusal's core still declares the
        /// same act BQ-074 already required of it.
        /// </summary>
        [Fact]
        public void AVoiceNeverChangesWhichActTheCoreFragmentSays()
        {
            FragmentRealizationTests.Scene scene = FragmentRealizationTests.Scene.Create();
            RealizationRequest request = scene.ThiefRefuses();
            request.Tone = new VoiceProfile { Directness = 1.0 }.RequestedTone();

            IReadOnlyList<DialogueFragment> cores = scene.Realizer.Candidates(FragmentPosition.Core, request);
            Assert.NotEmpty(cores);
            foreach (DialogueFragment fragment in cores)
            {
                Assert.Contains(fragment.Requires, requirement => requirement.Key == DialogueReadings.Act
                    && requirement.IsMetBy("refuse"));
            }
        }

        [Fact]
        public void EveryRenderingUnderEveryVoiceCarriesTheUnchangedMeaning()
        {
            FragmentRealizationTests.Scene scene = FragmentRealizationTests.Scene.Create();
            RealizationRequest request = scene.WitnessAnswers();
            string meaning = request.Act.Signature;

            VoiceProfile[] voices =
            {
                VoiceProfile.Neutral,
                new VoiceProfile { Formality = 1.0 },
                new VoiceProfile { Formality = 0.0, Directness = 1.0 },
                new VoiceProfile { Warmth = 1.0, Sarcasm = 1.0 },
            };

            foreach (VoiceProfile voice in voices)
            {
                request.Tone = voice.RequestedTone();
                foreach (RealizedLine line in scene.Renderings(request, 10))
                {
                    Assert.True(line.Rendered, line.Refusal);
                    Assert.Equal(meaning, line.Meaning);
                }
            }
        }

        private static HashSet<string> Rendered(
            FragmentRealizationTests.Scene scene,
            RealizationRequest request,
            VoiceProfile voice,
            string expectedMeaning)
        {
            request.Tone = voice.RequestedTone();
            HashSet<string> lines = new HashSet<string>(StringComparer.Ordinal);
            for (ulong seed = 1; seed <= 30; seed++)
            {
                request.Rng = new DeterministicRng(seed);
                RealizedLine line = scene.Realizer.Realize(request);
                Assert.True(line.Rendered, line.Refusal);
                Assert.Equal(expectedMeaning, line.Meaning);
                lines.Add(line.Text);
            }

            return lines;
        }
    }
}
