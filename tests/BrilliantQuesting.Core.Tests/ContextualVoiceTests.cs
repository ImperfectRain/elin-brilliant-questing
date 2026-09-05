using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BrilliantQuesting.Content;
using BrilliantQuesting.Dialogue;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Integration;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Relationships;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-149. A contextual condition may say what is between two people or what state one of them
    /// is in; it may not decide what sort of person they are. These are the four properties that
    /// separation has to keep, plus the loader's refusals and a sparseness guard over the shipped
    /// corpus.
    ///
    /// <list type="bullet">
    /// <item>the same tie, said by two voices, reaches two different sets of words;</item>
    /// <item>walking the tie under one voice never admits wording that voice did not ask for, so a
    /// relationship cannot overwrite a stable temperament;</item>
    /// <item>a lived-context vocabulary still changes only which fragment says the point; and</item>
    /// <item>a tie or a mood whose strongest line the speaker cannot say falls back to the plain
    /// alternative rather than to silence or to the strong line anyway.</item>
    /// </list>
    ///
    /// The mechanism under all four is one field and one check. <see cref="DialogueFragment.VoiceDemands"/>
    /// names persistent traits the speaker must actually have, and
    /// <see cref="DialogueFragment.FitsVoice"/> reads it out of the two lists a
    /// <see cref="VoiceProfile"/> already produces - so the narrowing is BQ-075's seam used a third
    /// time, and nothing here can reach an act, a decision or a meaning.
    /// </summary>
    public class ContextualVoiceTests
    {
        private static readonly EntityId Nobody = EntityId.Parse("npc_nobody");

        // -- the demand itself ------------------------------------------------------------------------

        [Fact]
        public void AFragmentThatDemandsNothingIsSayableByEveryVoice()
        {
            DialogueFragment plain = Demanding();

            foreach (VoiceProfile voice in Voices())
            {
                Assert.True(plain.FitsVoice(voice.RequestedTone(), voice.RequestedIdiolect()));
            }
        }

        [Fact]
        public void ADemandedTraitMustBeOneTheVoiceActuallyAsksFor()
        {
            DialogueFragment wry = Demanding(DialogueTones.Wry);

            Assert.False(wry.FitsVoice(
                VoiceProfile.Neutral.RequestedTone(), VoiceProfile.Neutral.RequestedIdiolect()));

            VoiceProfile sardonic = new VoiceProfile { Sarcasm = 1.0 };
            Assert.True(wry.FitsVoice(sardonic.RequestedTone(), sardonic.RequestedIdiolect()));
        }

        /// <summary>
        /// The whole reason the field exists. A mark is narrowed on by contradiction, so a voice
        /// that took no position on an axis leaves a marked fragment eligible - and wryness has no
        /// opposite pole at all, so a wry <em>mark</em> is a fragment nothing can ever refuse. That
        /// is how a rival came to be automatically playful: the tie chose the line and no voice was
        /// in a position to object.
        /// </summary>
        [Fact]
        public void ADemandIsStrictlyNarrowerThanTheSameMark()
        {
            IReadOnlyList<string> nothingRequested = VoiceProfile.Neutral.RequestedTone();

            Assert.Null(DialogueTones.Opposite(DialogueTones.Wry));
            Assert.True(Marked(DialogueTones.Wry).FitsTone(nothingRequested));
            Assert.False(Demanding(DialogueTones.Wry).FitsVoice(nothingRequested, null));

            // And it holds on an axis that does have an opposite: a cold voice already refused a
            // warm mark, but a voice with no opinion about warmth did not.
            VoiceProfile indifferent = new VoiceProfile { Warmth = 0.5 };
            Assert.True(Marked(DialogueTones.Warm).FitsTone(indifferent.RequestedTone()));
            Assert.False(Demanding(DialogueTones.Warm).FitsVoice(
                indifferent.RequestedTone(), indifferent.RequestedIdiolect()));
        }

        [Fact]
        public void EveryDemandHasToBeMetRatherThanAnyOfThem()
        {
            DialogueFragment both = Demanding(DialogueTones.Wry, DialogueTones.Formal);
            VoiceProfile halfway = new VoiceProfile { Sarcasm = 1.0 };

            Assert.False(both.FitsVoice(halfway.RequestedTone(), halfway.RequestedIdiolect()));

            VoiceProfile all = new VoiceProfile { Sarcasm = 1.0, Formality = 1.0 };
            Assert.True(both.FitsVoice(all.RequestedTone(), all.RequestedIdiolect()));
        }

        /// <summary>
        /// A demand names a tag in one of the two vocabularies a voice requests from, and neither
        /// list is privileged - a habit is as much a persistent trait as a pitch is.
        /// </summary>
        [Fact]
        public void ADemandReadsBothOfTheVocabulariesAVoiceRequestsFrom()
        {
            DialogueFragment imagistic = Demanding(DialogueIdiolect.Figurative);
            VoiceProfile pictorial = new VoiceProfile { Figuration = 1.0 };

            Assert.Empty(pictorial.RequestedTone());
            Assert.True(imagistic.FitsVoice(pictorial.RequestedTone(), pictorial.RequestedIdiolect()));
            Assert.False(imagistic.FitsVoice(
                new VoiceProfile { Figuration = 0.0 }.RequestedTone(),
                new VoiceProfile { Figuration = 0.0 }.RequestedIdiolect()));
        }

        /// <summary>
        /// What a line may demand is exactly what a voice can be asked to be, and nothing wider.
        /// The union is the two closed vocabularies rather than a third one, so a demand can never
        /// name a disposition, a domain or a manner.
        /// </summary>
        [Fact]
        public void OnlyATraitAVoiceCanRequestIsDemandable()
        {
            foreach (string tag in DialogueTones.Vocabulary)
            {
                Assert.True(DialogueVoiceTraits.IsTrait(tag));
            }

            foreach (string tag in DialogueIdiolect.Vocabulary)
            {
                Assert.True(DialogueVoiceTraits.IsTrait(tag));
            }

            Assert.False(DialogueVoiceTraits.IsTrait(DialogueVocabulary.Trade));
            Assert.False(DialogueVoiceTraits.IsTrait(DialogueManners.Pleading));
            Assert.False(DialogueVoiceTraits.IsTrait("honest"));
            Assert.False(DialogueVoiceTraits.IsTrait(null));

            Assert.Equal(DialogueTones.Cold, DialogueVoiceTraits.Opposite(DialogueTones.Warm));
            Assert.Equal(DialogueIdiolect.Terse, DialogueVoiceTraits.Opposite(DialogueIdiolect.Expansive));
            Assert.Null(DialogueVoiceTraits.Opposite(DialogueTones.Wry));
            Assert.Null(DialogueVoiceTraits.Opposite("honest"));
        }

        // -- what the loader refuses ------------------------------------------------------------------

        [Fact]
        public void AnUnknownVoiceTraitIsRejected()
        {
            Assert.Contains(
                "Unknown voice trait",
                Rejects(Fragment("mod.bad").Set("voice", JsonValue.Array().Add(JsonValue.String("possessive")))),
                StringComparison.Ordinal);
        }

        [Fact]
        public void AVoiceFieldThatIsNotAnArrayIsRejected()
        {
            Assert.Contains(
                "voice must be an array",
                Rejects(Fragment("mod.bad").Set("voice", JsonValue.String(DialogueTones.Wry))),
                StringComparison.Ordinal);
        }

        [Fact]
        public void AFragmentDemandingBothPolesOfOneAxisIsRejected()
        {
            Assert.Contains(
                "cannot demand",
                Rejects(Fragment("mod.bad").Set(
                    "voice",
                    JsonValue.Array()
                        .Add(JsonValue.String(DialogueTones.Warm))
                        .Add(JsonValue.String(DialogueTones.Cold)))),
                StringComparison.Ordinal);
        }

        /// <summary>
        /// A line marked warm that only a cold speaker may say is a rule that fires never, and it
        /// fails quietly: the fragment simply disappears from every pool it was written for.
        /// </summary>
        [Fact]
        public void AFragmentDemandingTheOppositeOfItsOwnMarkIsRejected()
        {
            Assert.Contains(
                "cannot demand",
                Rejects(Fragment("mod.bad")
                    .Set("tone", JsonValue.Array().Add(JsonValue.String(DialogueTones.Warm)))
                    .Set("voice", JsonValue.Array().Add(JsonValue.String(DialogueTones.Cold)))),
                StringComparison.Ordinal);

            Assert.Contains(
                "cannot demand",
                Rejects(Fragment("mod.bad")
                    .Set("idiolect", JsonValue.Array().Add(JsonValue.String(DialogueIdiolect.Literal)))
                    .Set("voice", JsonValue.Array().Add(JsonValue.String(DialogueIdiolect.Figurative)))),
                StringComparison.Ordinal);
        }

        /// <summary>
        /// The refusal that makes "an unsupported combination falls back to neutral material"
        /// structural rather than a property of how carefully the corpus was authored. The core is
        /// the one slot that cannot fall silent, so a demand on one would turn a temperament into a
        /// refused act.
        /// </summary>
        [Fact]
        public void ACoreFragmentMayNotDemandAVoiceTrait()
        {
            JsonValue core = JsonValue.Object()
                .Set("id", "core.bad")
                .Set("position", "core")
                .Set("text", "No.")
                .Set("requires", JsonValue.Object().Set("act", JsonValue.String("refuse")))
                .Set("voice", JsonValue.Array().Add(JsonValue.String(DialogueTones.Curt)));

            Assert.Contains("may not demand a voice trait", Rejects(core), StringComparison.Ordinal);
        }

        [Fact]
        public void AWellFormedDemandLoads()
        {
            IReadOnlyList<ContentDiagnostic> diagnostics;
            IReadOnlyList<DialogueFragment> fragments = DialogueFragmentContent.LoadFragments(
                Bundle(Fragment("mod.good").Set(
                    "voice",
                    JsonValue.Array()
                        .Add(JsonValue.String(DialogueTones.Wry))
                        .Add(JsonValue.String(DialogueIdiolect.Terse)))),
                out diagnostics);

            Assert.Empty(diagnostics);
            Assert.Equal(
                new[] { DialogueTones.Wry, DialogueIdiolect.Terse },
                Assert.Single(fragments).VoiceDemands);
        }

        [Fact]
        public void AFragmentThatDemandsNothingLoadsWithoutDemands()
        {
            IReadOnlyList<ContentDiagnostic> diagnostics;
            IReadOnlyList<DialogueFragment> fragments =
                DialogueFragmentContent.LoadFragments(Bundle(Fragment("mod.plain")), out diagnostics);

            Assert.Empty(diagnostics);
            Assert.Empty(Assert.Single(fragments).VoiceDemands);
        }

        // -- what the shipped corpus has to keep true --------------------------------------------------

        [Fact]
        public void NoShippedCoreDemandsAVoiceTrait()
        {
            foreach (DialogueFragment fragment in Shipped())
            {
                if (fragment.Position == FragmentPosition.Core)
                {
                    Assert.Empty(fragment.VoiceDemands);
                }
            }
        }

        /// <summary>
        /// The corpus-side half of the fallback guarantee: for every tie and every mood some
        /// authored line reserves for a particular temperament, another authored line for the same
        /// tie or mood reserves nothing. Without this the demand would not narrow a pool, it would
        /// empty one, and a rival with an ordinary voice would lose the relationship entirely
        /// rather than lose the playful reading of it.
        /// </summary>
        [Fact]
        public void EveryTieAndMoodADemandingLineIsWrittenForKeepsAnUndemandingAlternative()
        {
            foreach (string key in new[] { DialogueReadings.Relationship, DialogueReadings.Emotion })
            {
                HashSet<string> demanded = new HashSet<string>(StringComparer.Ordinal);
                HashSet<string> free = new HashSet<string>(StringComparer.Ordinal);

                foreach (DialogueFragment fragment in Shipped())
                {
                    foreach (string value in Declared(fragment, key))
                    {
                        (fragment.VoiceDemands.Count == 0 ? free : demanded).Add(value);
                    }
                }

                Assert.NotEmpty(demanded);
                Assert.Empty(demanded.Except(free));
            }
        }

        /// <summary>
        /// A demand is the only narrowing in the library that a middling voice fails rather than
        /// passes, so every demanding line is wording most speakers never reach. That is the point
        /// for a line whose temperament <em>is</em> the line, and caricature the moment it becomes
        /// the ordinary way a tie or a mood is worded.
        /// </summary>
        [Fact]
        public void DemandingLinesStayASmallMinorityOfTheCorpus()
        {
            IReadOnlyList<DialogueFragment> shipped = Shipped();
            int demanding = shipped.Count(fragment => fragment.VoiceDemands.Count != 0);

            Assert.InRange(demanding, 1, shipped.Count / 20);
        }

        // -- the done-when ----------------------------------------------------------------------------

        /// <summary>
        /// The first property: relationship supplies the context and the voice decides how it comes
        /// out. Three speakers who are all strangers to the person opposite, refusing the identical
        /// thing for the identical reason, reach three different sets of words - and the one thing
        /// none of them can do is change what was refused.
        /// </summary>
        [Fact]
        public void TheSameTieProducesDifferentWordingForContrastingVoices()
        {
            FragmentRealizationTests.Scene scene = FragmentRealizationTests.Scene.Create();
            RealizationRequest request = scene.ThiefRefuses();
            request.Tie = SpeakerTie.Stranger(scene.Player);

            HashSet<string> plain = TieModifiers(scene, request, VoiceProfile.Neutral);
            HashSet<string> composed = TieModifiers(scene, request, new VoiceProfile { Formality = 1.0 });
            HashSet<string> blunt = TieModifiers(scene, request, new VoiceProfile { Directness = 1.0 });

            // The undemanding line is everybody's; each demanded one is exactly one voice's.
            Assert.Contains("mod.tie.stranger.not.well.enough", plain);
            Assert.DoesNotContain("mod.tie.stranger.trust.first", plain);
            Assert.DoesNotContain("mod.tie.stranger.earn.the.second", plain);

            Assert.Contains("mod.tie.stranger.trust.first", composed);
            Assert.DoesNotContain("mod.tie.stranger.earn.the.second", composed);

            Assert.Contains("mod.tie.stranger.earn.the.second", blunt);
            Assert.DoesNotContain("mod.tie.stranger.trust.first", blunt);
        }

        /// <summary>
        /// The same property on the mood axis, which is the other half of BQ-146's contribution: a
        /// speaker's anger says what state they are in, and the wry reading of being angry belongs
        /// to whoever was already wry.
        /// </summary>
        [Fact]
        public void TheSameMoodProducesDifferentWordingForContrastingVoices()
        {
            FragmentRealizationTests.Scene scene = FragmentRealizationTests.Scene.Create();
            RealizationRequest request = scene.ThiefRefuses();
            request.Feeling = SpeakerFeeling.Felt(EmotionalState.Anger, 0.8);

            HashSet<string> plain = Modifiers(scene, request, VoiceProfile.Neutral);
            HashSet<string> sardonic = Modifiers(scene, request, new VoiceProfile { Sarcasm = 1.0 });

            Assert.Contains("mod.emotion.anger.plain", plain);
            Assert.DoesNotContain("mod.emotion.anger.cooled", plain);

            Assert.Contains("mod.emotion.anger.plain", sardonic);
            Assert.Contains("mod.emotion.anger.cooled", sardonic);
        }

        /// <summary>
        /// The second property, and the one the audit was for. A voice is a fact about the speaker;
        /// a tie is a fact about a pair. Walking the tie from stranger to spouse under one fixed
        /// voice must never produce a line that voice had ruled out - by taking the opposite pole
        /// on an axis it named, or by reserving itself for a temperament it does not have.
        /// </summary>
        [Fact]
        public void ChangingTheTieAloneNeverOverwritesAStableVoice()
        {
            FragmentRealizationTests.Scene scene = FragmentRealizationTests.Scene.Create();
            VoiceProfile warmAndPlain = new VoiceProfile { Warmth = 1.0, Formality = 0.0 };
            IReadOnlyList<string> tone = warmAndPlain.RequestedTone();
            IReadOnlyList<string> idiolect = warmAndPlain.RequestedIdiolect();

            int reached = 0;
            foreach (SpeakerTie tie in EveryTie(scene.Player))
            {
                RealizationRequest request = scene.ThiefRefuses();
                request.Tone = tone;
                request.Idiolect = idiolect;
                request.Tie = tie;

                Assert.Equal(string.Empty, request.WhyNot());

                foreach (FragmentPosition position in Positions())
                {
                    foreach (DialogueFragment fragment in scene.Realizer.Candidates(position, request))
                    {
                        Assert.True(fragment.FitsTone(tone), fragment.Id + " contradicts the voice's tone");
                        Assert.True(fragment.FitsIdiolect(idiolect), fragment.Id + " contradicts the voice's habits");
                        Assert.True(
                            fragment.FitsVoice(tone, idiolect),
                            fragment.Id + " is reserved for a temperament this speaker was never given");
                        reached++;
                    }
                }

                // Reading a tie is reading, and reading writes nothing back: the voice this speaker
                // was given asks for exactly the same thing at every rung of the walk.
                Assert.True(tone.SequenceEqual(request.Tone));
                Assert.True(idiolect.SequenceEqual(request.Idiolect));
            }

            // Non-vacuity, and the concrete case the audit was about. This speaker is a stranger
            // with a warm and plain voice: the tie still has something to say, and the two lines
            // reserved for a composed speaker and a blunt one are not it.
            Assert.NotEqual(0, reached);

            RealizationRequest stranger = scene.ThiefRefuses();
            stranger.Tie = SpeakerTie.Stranger(scene.Player);
            HashSet<string> ids = TieModifiers(scene, stranger, warmAndPlain);
            Assert.Contains("mod.tie.stranger.not.well.enough", ids);
            Assert.DoesNotContain("mod.tie.stranger.trust.first", ids);
            Assert.DoesNotContain("mod.tie.stranger.earn.the.second", ids);
        }

        /// <summary>
        /// The third property. An occupational vocabulary reaches which words say the point and
        /// never the point: every rendering under every domain carries the identical meaning, and
        /// every core still declares the act it was authored for.
        /// </summary>
        [Fact]
        public void OccupationalVocabularyNeverChangesWhatWasMeant()
        {
            FragmentRealizationTests.Scene scene = FragmentRealizationTests.Scene.Create();

            foreach (RealizationRequest request in scene.EveryKindOfLine())
            {
                string meaning = request.Act.Signature;
                request.Vocabulary = OccupationalVocabulary.RequestedVocabulary(IdentityAffordances.Nothing);
                HashSet<string> unflavoured = new HashSet<string>(
                    scene.Realizer.Candidates(FragmentPosition.Core, request).Select(core => core.Id),
                    StringComparer.Ordinal);
                Assert.NotEmpty(unflavoured);

                foreach (IReadOnlyList<string> vocabulary in EveryLivedContext())
                {
                    request.Vocabulary = vocabulary;

                    // The sentence that carries the point is the same set of sentences whatever the
                    // speaker does for a living: lived context reaches the flourish around the point
                    // and never the point.
                    Assert.True(
                        unflavoured.SetEquals(
                            scene.Realizer.Candidates(FragmentPosition.Core, request).Select(core => core.Id)),
                        "a lived context changed which sentence carries the point");

                    foreach (DialogueFragment core in scene.Realizer.Candidates(FragmentPosition.Core, request))
                    {
                        Assert.NotEmpty(Declared(core, DialogueReadings.Act));
                    }

                    foreach (RealizedLine line in scene.Renderings(request, 8))
                    {
                        Assert.True(line.Rendered, line.Refusal);
                        Assert.Equal(meaning, line.Meaning);
                    }
                }
            }
        }

        /// <summary>
        /// The fourth property, which is the one a reader should be able to check without reading
        /// the loader. A tie whose strong line this speaker cannot say is not a tie with nothing to
        /// say: the plain alternative is still there, the core is untouched, and the line still
        /// comes out meaning what it meant.
        /// </summary>
        [Fact]
        public void AnUnsupportedCombinationFallsBackToNeutralMaterial()
        {
            FragmentRealizationTests.Scene scene = FragmentRealizationTests.Scene.Create();
            RealizationRequest request = scene.ThiefRefuses();
            request.Tie = SpeakerTie.Stranger(scene.Player);
            request.Feeling = SpeakerFeeling.Felt(EmotionalState.Anger, 0.8);
            request.Tone = VoiceProfile.Neutral.RequestedTone();
            request.Idiolect = VoiceProfile.Neutral.RequestedIdiolect();

            IReadOnlyList<DialogueFragment> modifiers = scene.Realizer.Candidates(FragmentPosition.Modifier, request);
            Assert.NotEmpty(modifiers);
            foreach (DialogueFragment fragment in modifiers)
            {
                Assert.Empty(fragment.VoiceDemands);
            }

            Assert.NotEmpty(scene.Realizer.Candidates(FragmentPosition.Core, request));
            foreach (RealizedLine line in scene.Renderings(request, 20))
            {
                Assert.True(line.Rendered, line.Refusal);
                Assert.Equal(request.Act.Signature, line.Meaning);
            }
        }

        // -- helpers ----------------------------------------------------------------------------------

        private static IEnumerable<VoiceProfile> Voices()
        {
            yield return VoiceProfile.Neutral;
            yield return new VoiceProfile { Formality = 1.0, Directness = 1.0, Warmth = 1.0, Sarcasm = 1.0 };
            yield return new VoiceProfile { Formality = 0.0, Directness = 0.0, Warmth = 0.0, Sarcasm = 0.0 };
            yield return new VoiceProfile { Verbosity = 1.0, Cadence = 1.0, Figuration = 1.0 };
            yield return new VoiceProfile { Verbosity = 0.0, Cadence = 0.0, Figuration = 0.0 };
        }

        private static IEnumerable<SpeakerTie> EveryTie(EntityId listener)
        {
            yield return SpeakerTie.Unread;
            yield return SpeakerTie.Stranger(listener);
            foreach (RelationKind kind in Enum.GetValues(typeof(RelationKind)))
            {
                yield return SpeakerTie.Tied(kind, listener);
            }
        }

        private static IEnumerable<IReadOnlyList<string>> EveryLivedContext()
        {
            yield return OccupationalVocabulary.RequestedVocabulary(IdentityAffordances.Nothing);
            yield return Context(builder => builder.WithWork("farmer", "Farmer"));
            yield return Context(builder => builder.WithWork("merchant", "Merchant"));
            yield return Context(builder => builder.AddInstitution("city_of_yowyn", "TraitGuard"));
        }

        private static IReadOnlyList<string> Context(Action<CharacterIdentityBuilder> describe)
        {
            CharacterIdentityBuilder builder = new CharacterIdentityBuilder(Nobody);
            describe(builder);
            return OccupationalVocabulary.RequestedVocabulary(IdentityAffordances.Derive(builder.Build()));
        }

        private static IEnumerable<FragmentPosition> Positions()
        {
            yield return FragmentPosition.Opener;
            yield return FragmentPosition.Core;
            yield return FragmentPosition.Modifier;
            yield return FragmentPosition.Callback;
            yield return FragmentPosition.Context;
            yield return FragmentPosition.Closer;
        }

        private static HashSet<string> Modifiers(
            FragmentRealizationTests.Scene scene, RealizationRequest request, VoiceProfile voice)
        {
            request.Tone = voice.RequestedTone();
            request.Idiolect = voice.RequestedIdiolect();
            return new HashSet<string>(
                scene.Realizer.Candidates(FragmentPosition.Modifier, request).Select(fragment => fragment.Id),
                StringComparer.Ordinal);
        }

        private static HashSet<string> TieModifiers(
            FragmentRealizationTests.Scene scene, RealizationRequest request, VoiceProfile voice)
        {
            HashSet<string> ids = Modifiers(scene, request, voice);
            Assert.Contains(ids, id => id.StartsWith("mod.tie.", StringComparison.Ordinal));
            return ids;
        }

        private static IReadOnlyList<string> Declared(DialogueFragment fragment, string key)
        {
            for (int i = 0; i < fragment.Requires.Count; i++)
            {
                if (string.Equals(fragment.Requires[i].Key, key, StringComparison.Ordinal))
                {
                    return fragment.Requires[i].Values;
                }
            }

            return new string[0];
        }

        private static DialogueFragment Demanding(params string[] demands)
        {
            return new DialogueFragment(
                "test.demand." + string.Join(".", demands),
                FragmentPosition.Modifier,
                "Text.",
                requires: null,
                forbids: null,
                toneTags: null,
                idiolectTags: null,
                tags: null,
                repetitionGroup: null,
                slots: null,
                memorability: DialogueMemorability.Utility,
                voiceDemands: demands);
        }

        private static DialogueFragment Marked(params string[] tone)
        {
            return new DialogueFragment(
                "test.mark." + string.Join(".", tone),
                FragmentPosition.Modifier,
                "Text.",
                requires: null,
                forbids: null,
                toneTags: tone,
                tags: null,
                repetitionGroup: null,
                slots: null);
        }

        private static JsonValue Fragment(string id)
        {
            return JsonValue.Object().Set("id", id).Set("position", "modifier").Set("text", "Text.");
        }

        private static ContentBundle Bundle(JsonValue fragment)
        {
            return new ContentBundle(
                ContentBundle.CurrentVersion,
                new[]
                {
                    new ContentRecord(
                        "fragments.test",
                        DialogueFragmentContent.Kind,
                        JsonValue.Object().Set("fragments", JsonValue.Array().Add(fragment)))
                });
        }

        private static string Rejects(JsonValue fragment)
        {
            IReadOnlyList<ContentDiagnostic> diagnostics;
            IReadOnlyList<DialogueFragment> fragments =
                DialogueFragmentContent.LoadFragments(Bundle(fragment), out diagnostics);

            Assert.Empty(fragments);
            return Assert.Single(diagnostics).Message;
        }

        private static IReadOnlyList<DialogueFragment> _shipped;

        private static IReadOnlyList<DialogueFragment> Shipped()
        {
            if (_shipped != null)
            {
                return _shipped;
            }

            DirectoryInfo directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "ElinBrilliantQuesting.sln")))
            {
                directory = directory.Parent;
            }

            Assert.NotNull(directory);
            ContentBundleLoadResult bundle = ContentBundleLoader.LoadFile(
                Path.Combine(directory.FullName, "Package", "content.bqc"));
            Assert.Empty(bundle.Diagnostics);

            IReadOnlyList<ContentDiagnostic> diagnostics;
            IReadOnlyList<DialogueFragment> fragments =
                DialogueFragmentContent.LoadFragments(bundle.Bundle, out diagnostics);
            Assert.Empty(diagnostics);
            Assert.NotEmpty(fragments);
            _shipped = fragments;
            return fragments;
        }
    }
}
