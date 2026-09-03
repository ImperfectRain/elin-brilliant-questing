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

        // -- a request is a set of axis positions, not a list of alternatives -------------------------

        /// <summary>
        /// The defect: with tone read as alternatives, a fragment that took the opposite position
        /// on one axis was re-admitted by a second axis that happened to match, so a voice that had
        /// explicitly asked to sound plain could be handed a formal line.
        /// </summary>
        [Fact]
        public void AFragmentContradictingOneAxisCannotPassBecauseAnotherAxisMatches()
        {
            DialogueFragment formalAndCurt = Marked(DialogueTones.Formal, DialogueTones.Curt);
            IReadOnlyList<string> plainAndBlunt = new VoiceProfile { Formality = 0.0, Directness = 1.0 }.RequestedTone();

            Assert.Equal(new[] { DialogueTones.Plain, DialogueTones.Curt }, plainAndBlunt);
            Assert.False(formalAndCurt.FitsTone(plainAndBlunt));

            // The same fragment is fine for a voice that never took a position on formality.
            Assert.True(formalAndCurt.FitsTone(new VoiceProfile { Directness = 1.0 }.RequestedTone()));
        }

        /// <summary>
        /// The property the defect broke: naming another axis can only ever remove candidates. A
        /// voice specified on four axes is never a less constrained one than a voice specified on
        /// one.
        /// </summary>
        [Fact]
        public void NamingMoreAxesNeverWidensThePool()
        {
            DialogueFragment[] pool =
            {
                Marked(),
                Marked(DialogueTones.Plain),
                Marked(DialogueTones.Formal),
                Marked(DialogueTones.Curt),
                Marked(DialogueTones.Wary),
                Marked(DialogueTones.Warm),
                Marked(DialogueTones.Cold),
                Marked(DialogueTones.Wry),
                Marked(DialogueTones.Formal, DialogueTones.Cold),
            };

            VoiceProfile[] widening =
            {
                VoiceProfile.Neutral,
                new VoiceProfile { Formality = 1.0 },
                new VoiceProfile { Formality = 1.0, Directness = 1.0 },
                new VoiceProfile { Formality = 1.0, Directness = 1.0, Warmth = 0.0 },
                new VoiceProfile { Formality = 1.0, Directness = 1.0, Warmth = 0.0, Sarcasm = 1.0 },
            };

            int previous = int.MaxValue;
            foreach (VoiceProfile voice in widening)
            {
                IReadOnlyList<string> tone = voice.RequestedTone();
                int admitted = pool.Count(fragment => fragment.FitsTone(tone));
                Assert.True(
                    admitted <= previous,
                    "specifying " + tone.Count + " axes admitted " + admitted + ", up from " + previous);
                previous = admitted;
            }

            // And it really does constrain: the four-axis voice is strictly narrower than neutral.
            Assert.True(previous < pool.Length);
        }

        /// <summary>
        /// The floor the narrowing must not cross: an unmarked fragment is safe fallback material
        /// for every voice, however strongly specified, and a neutral voice narrows nothing at all.
        /// </summary>
        [Fact]
        public void UnmarkedFragmentsStayUsableUnderEveryVoiceAndNeutralNarrowsNothing()
        {
            DialogueFragment unmarked = Marked();
            VoiceProfile[] voices =
            {
                VoiceProfile.Neutral,
                new VoiceProfile { Formality = 1.0, Directness = 1.0, Warmth = 1.0, Sarcasm = 1.0 },
                new VoiceProfile { Formality = 0.0, Directness = 0.0, Warmth = 0.0, Sarcasm = 0.0 },
            };

            foreach (VoiceProfile voice in voices)
            {
                Assert.True(unmarked.FitsTone(voice.RequestedTone()));
            }

            IReadOnlyList<string> neutral = VoiceProfile.Neutral.RequestedTone();
            foreach (string tag in DialogueTones.Vocabulary)
            {
                Assert.True(Marked(tag).FitsTone(neutral));
            }
        }

        [Fact]
        public void EveryToneTagPairsWithTheOtherEndOfItsOwnAxis()
        {
            Assert.Equal(DialogueTones.Plain, DialogueTones.Opposite(DialogueTones.Formal));
            Assert.Equal(DialogueTones.Formal, DialogueTones.Opposite(DialogueTones.Plain));
            Assert.Equal(DialogueTones.Wary, DialogueTones.Opposite(DialogueTones.Curt));
            Assert.Equal(DialogueTones.Curt, DialogueTones.Opposite(DialogueTones.Wary));
            Assert.Equal(DialogueTones.Cold, DialogueTones.Opposite(DialogueTones.Warm));
            Assert.Equal(DialogueTones.Warm, DialogueTones.Opposite(DialogueTones.Cold));

            // Sincerity is the unmarked baseline, so nothing contradicts a wry fragment.
            Assert.Null(DialogueTones.Opposite(DialogueTones.Wry));
            Assert.Null(DialogueTones.Opposite("not_a_tone"));

            // The pairing is an involution over everything it claims, and claims nothing else.
            foreach (string tag in DialogueTones.Vocabulary)
            {
                string opposite = DialogueTones.Opposite(tag);
                if (opposite != null)
                {
                    Assert.True(DialogueTones.IsTone(opposite));
                    Assert.Equal(tag, DialogueTones.Opposite(opposite));
                }
            }
        }

        private static DialogueFragment Marked(params string[] tone)
        {
            return new DialogueFragment(
                "test.tone." + string.Join(".", tone),
                FragmentPosition.Modifier,
                "Text.",
                requires: null,
                forbids: null,
                toneTags: tone,
                tags: null,
                repetitionGroup: null,
                slots: null);
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
