using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BrilliantQuesting.Content;
using BrilliantQuesting.Dialogue;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Persistence;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-142. Persistent idiolect: the habits that make two speakers who have reached the identical
    /// semantic state sound like two people rather than one.
    ///
    /// BQ-075 proved a voice can narrow wording through tone. This proves the same seam carries a
    /// second, orthogonal vocabulary - length, cadence and figuration - and that adding it moved
    /// nothing on the meaning side. The tests are therefore mostly the same shape as BQ-075's, run
    /// against the new axes, plus the two BQ-075 never needed: what happens when a voice asks for
    /// more than the library can supply, and what the loader does with a mark nobody could satisfy.
    ///
    /// <list type="bullet">
    /// <item><see cref="VoiceProfile.RequestedIdiolect"/> is a pure function of three axes;</item>
    /// <item>two voices that differ in nothing but idiolect say the same act differently;</item>
    /// <item>no voice, however specified, changes <see cref="RealizedLine.Meaning"/> or which act a
    /// core says;</item>
    /// <item>a neutral voice and an unmarked fragment both behave exactly as they did before the
    /// vocabulary existed;</item>
    /// <item>a constraint nothing satisfies refuses with a reason rather than saying the wrong
    /// thing;</item>
    /// <item>and metadata a voice could never satisfy is rejected at load.</item>
    /// </list>
    /// </summary>
    public class VoiceIdiolectTests
    {
        // -- RequestedIdiolect is a pure mapping ------------------------------------------------------

        [Fact]
        public void ANeutralVoiceRequestsNoIdiolect()
        {
            Assert.Empty(VoiceProfile.Neutral.RequestedIdiolect());
        }

        [Fact]
        public void EachAxisRequestsItsOwnPoleAtItsExtremes()
        {
            Assert.Equal(new[] { DialogueIdiolect.Terse }, new VoiceProfile { Verbosity = 0.0 }.RequestedIdiolect());
            Assert.Equal(new[] { DialogueIdiolect.Expansive }, new VoiceProfile { Verbosity = 1.0 }.RequestedIdiolect());

            Assert.Equal(new[] { DialogueIdiolect.Clipped }, new VoiceProfile { Cadence = 0.0 }.RequestedIdiolect());
            Assert.Equal(new[] { DialogueIdiolect.Flowing }, new VoiceProfile { Cadence = 1.0 }.RequestedIdiolect());

            Assert.Equal(new[] { DialogueIdiolect.Literal }, new VoiceProfile { Figuration = 0.0 }.RequestedIdiolect());
            Assert.Equal(new[] { DialogueIdiolect.Figurative }, new VoiceProfile { Figuration = 1.0 }.RequestedIdiolect());
        }

        [Fact]
        public void AMiddlingValueOnAnAxisRequestsNothingOnThatAxis()
        {
            Assert.Empty(new VoiceProfile { Verbosity = 0.5, Cadence = 0.5, Figuration = 0.5 }.RequestedIdiolect());
            Assert.Equal(
                new[] { DialogueIdiolect.Terse },
                new VoiceProfile { Verbosity = 0.0, Cadence = 0.5, Figuration = 0.5 }.RequestedIdiolect());
        }

        [Fact]
        public void RequestedIdiolectIsDeterministic()
        {
            VoiceProfile voice = new VoiceProfile { Verbosity = 0.1, Cadence = 0.9, Figuration = 0.2 };
            Assert.Equal(voice.RequestedIdiolect(), voice.RequestedIdiolect());
        }

        /// <summary>
        /// The two vocabularies are independent requests, not one list split in two. A voice with
        /// habits and no tonal position asks for no tone at all, and the other way about - which is
        /// what lets the laboratory isolate idiolect by holding tone still.
        /// </summary>
        [Fact]
        public void ToneAndIdiolectAreRequestedSeparately()
        {
            VoiceProfile habitsOnly = new VoiceProfile { Verbosity = 0.0, Cadence = 0.0, Figuration = 0.0 };
            Assert.Empty(habitsOnly.RequestedTone());
            Assert.Equal(3, habitsOnly.RequestedIdiolect().Count);

            VoiceProfile toneOnly = new VoiceProfile { Formality = 1.0, Warmth = 0.0 };
            Assert.Empty(toneOnly.RequestedIdiolect());
            Assert.Equal(2, toneOnly.RequestedTone().Count);
        }

        [Fact]
        public void EveryIdiolectMarkPairsWithTheOtherEndOfItsOwnAxis()
        {
            Assert.Equal(DialogueIdiolect.Expansive, DialogueIdiolect.Opposite(DialogueIdiolect.Terse));
            Assert.Equal(DialogueIdiolect.Flowing, DialogueIdiolect.Opposite(DialogueIdiolect.Clipped));
            Assert.Equal(DialogueIdiolect.Figurative, DialogueIdiolect.Opposite(DialogueIdiolect.Literal));
            Assert.Null(DialogueIdiolect.Opposite("not_an_idiolect"));

            // Unlike tone, the pairing is total: there is no unmarked baseline pole here, so every
            // mark can be contradicted and no mark quietly suits everybody.
            foreach (string tag in DialogueIdiolect.Vocabulary)
            {
                string opposite = DialogueIdiolect.Opposite(tag);
                Assert.NotNull(opposite);
                Assert.True(DialogueIdiolect.IsIdiolect(opposite));
                Assert.Equal(tag, DialogueIdiolect.Opposite(opposite));
            }
        }

        // -- narrowing only, and never below the floor ------------------------------------------------

        [Fact]
        public void NamingMoreAxesNeverWidensThePool()
        {
            DialogueFragment[] pool =
            {
                Marked(),
                Marked(DialogueIdiolect.Terse),
                Marked(DialogueIdiolect.Expansive),
                Marked(DialogueIdiolect.Clipped),
                Marked(DialogueIdiolect.Flowing),
                Marked(DialogueIdiolect.Literal),
                Marked(DialogueIdiolect.Figurative),
                Marked(DialogueIdiolect.Terse, DialogueIdiolect.Clipped),
                Marked(DialogueIdiolect.Expansive, DialogueIdiolect.Flowing, DialogueIdiolect.Figurative),
            };

            VoiceProfile[] widening =
            {
                VoiceProfile.Neutral,
                new VoiceProfile { Verbosity = 0.0 },
                new VoiceProfile { Verbosity = 0.0, Cadence = 0.0 },
                new VoiceProfile { Verbosity = 0.0, Cadence = 0.0, Figuration = 0.0 },
            };

            int previous = int.MaxValue;
            foreach (VoiceProfile voice in widening)
            {
                IReadOnlyList<string> idiolect = voice.RequestedIdiolect();
                int admitted = pool.Count(fragment => fragment.FitsIdiolect(idiolect));
                Assert.True(
                    admitted <= previous,
                    "specifying " + idiolect.Count + " axes admitted " + admitted + ", up from " + previous);
                previous = admitted;
            }

            Assert.True(previous < pool.Length);
        }

        /// <summary>
        /// A mark contradicting one axis cannot be re-admitted by another axis that matches - the
        /// defect D043 found in tone, checked here before it can be reintroduced in a second
        /// vocabulary.
        /// </summary>
        [Fact]
        public void AFragmentContradictingOneAxisCannotPassBecauseAnotherAxisMatches()
        {
            DialogueFragment terseAndFigurative = Marked(DialogueIdiolect.Terse, DialogueIdiolect.Figurative);
            IReadOnlyList<string> terseAndLiteral =
                new VoiceProfile { Verbosity = 0.0, Figuration = 0.0 }.RequestedIdiolect();

            Assert.Equal(new[] { DialogueIdiolect.Terse, DialogueIdiolect.Literal }, terseAndLiteral);
            Assert.False(terseAndFigurative.FitsIdiolect(terseAndLiteral));

            // And it is fine for a voice that never took a position on figuration.
            Assert.True(terseAndFigurative.FitsIdiolect(new VoiceProfile { Verbosity = 0.0 }.RequestedIdiolect()));
        }

        /// <summary>
        /// The floor, and the reason a corpus can be migrated a few fragments at a time: an
        /// unmarked fragment is safe wording for every voice, and a voice with no habits narrows
        /// nothing at all.
        /// </summary>
        [Fact]
        public void UnmarkedFragmentsStayUsableUnderEveryVoiceAndNeutralNarrowsNothing()
        {
            DialogueFragment unmarked = Marked();
            VoiceProfile[] voices =
            {
                VoiceProfile.Neutral,
                new VoiceProfile { Verbosity = 1.0, Cadence = 1.0, Figuration = 1.0 },
                new VoiceProfile { Verbosity = 0.0, Cadence = 0.0, Figuration = 0.0 },
            };

            foreach (VoiceProfile voice in voices)
            {
                Assert.True(unmarked.FitsIdiolect(voice.RequestedIdiolect()));
            }

            IReadOnlyList<string> neutral = VoiceProfile.Neutral.RequestedIdiolect();
            foreach (string tag in DialogueIdiolect.Vocabulary)
            {
                Assert.True(Marked(tag).FitsIdiolect(neutral));
            }
        }

        /// <summary>
        /// A mark is read by the idiolect check and by nothing else. The vocabularies that share
        /// <see cref="DialogueFragment.Tags"/> - occupational flavour and forbidden manners - do
        /// not see it, so migrating a fragment cannot silently change what BQ-076 or BQ-077 does
        /// with it.
        /// </summary>
        [Fact]
        public void AnIdiolectMarkIsInvisibleToTheOtherNarrowings()
        {
            DialogueFragment terse = Marked(DialogueIdiolect.Terse);

            Assert.Empty(terse.Tags);
            Assert.True(terse.FitsVocabulary(null));
            Assert.True(terse.FitsVocabulary(new[] { DialogueVocabulary.Trade }));
            Assert.True(terse.FitsManner(new[] { DialogueManners.Pleading }));
            Assert.True(terse.FitsTone(new VoiceProfile { Formality = 1.0 }.RequestedTone()));
        }

        // -- the done-when ----------------------------------------------------------------------------

        /// <summary>
        /// The step's condition. Two speakers at the identical semantic state - same act, same
        /// disclosure decision, same personality, and here even the same tone - sound perceptibly
        /// different because of nothing but their habits.
        /// </summary>
        [Fact]
        public void TheIdenticalRefusalSoundsDifferentThroughTwoIdiolectsAtTheSameTone()
        {
            FragmentRealizationTests.Scene scene = FragmentRealizationTests.Scene.Create();
            RealizationRequest request = scene.ThiefRefuses();
            string meaning = request.Act.Signature;

            VoiceProfile terse = new VoiceProfile { Verbosity = 0.0, Cadence = 0.0, Figuration = 0.0 };
            VoiceProfile expansive = new VoiceProfile { Verbosity = 1.0, Cadence = 1.0, Figuration = 1.0 };

            Assert.Empty(terse.RequestedTone());
            Assert.Empty(expansive.RequestedTone());

            HashSet<string> clipped = Rendered(scene, request, terse, meaning);
            HashSet<string> ornate = Rendered(scene, request, expansive, meaning);

            Assert.NotEmpty(clipped);
            Assert.NotEmpty(ornate);
            Assert.True(
                clipped.Except(ornate).Any() || ornate.Except(clipped).Any(),
                "the two idiolects produced the identical set of lines: " + string.Join(" / ", clipped));
        }

        /// <summary>
        /// The other half, and the invariant the whole layer rests on: a habit narrows which
        /// fragment says the point and never which point is said. Every core a specified voice can
        /// reach still declares the act BQ-074 required of it, and every rendering carries the
        /// meaning the act started with.
        /// </summary>
        [Fact]
        public void NoIdiolectChangesWhatIsMeantOrWhichActTheCoreSays()
        {
            FragmentRealizationTests.Scene scene = FragmentRealizationTests.Scene.Create();
            RealizationRequest request = scene.ThiefRefuses();
            string meaning = request.Act.Signature;

            foreach (VoiceProfile voice in EveryCorner())
            {
                request.Idiolect = voice.RequestedIdiolect();

                IReadOnlyList<DialogueFragment> cores = scene.Realizer.Candidates(FragmentPosition.Core, request);
                Assert.NotEmpty(cores);
                foreach (DialogueFragment fragment in cores)
                {
                    Assert.Contains(
                        fragment.Requires,
                        requirement => requirement.Key == DialogueReadings.Act && requirement.IsMetBy("refuse"));
                }

                foreach (RealizedLine line in scene.Renderings(request, 10))
                {
                    Assert.True(line.Rendered, line.Refusal);
                    Assert.Equal(meaning, line.Meaning);
                }
            }
        }

        /// <summary>
        /// The default stays what it was. A request that names no habit reaches exactly the pool
        /// BQ-074 built, so every caller written before this vocabulary existed - which is all of
        /// them outside the laboratory - behaves identically.
        /// </summary>
        [Fact]
        public void ARequestThatNamesNoHabitSeesTheWholePool()
        {
            FragmentRealizationTests.Scene scene = FragmentRealizationTests.Scene.Create();
            RealizationRequest request = scene.ThiefRefuses();

            Assert.Empty(request.Idiolect);
            int unconstrained = scene.Realizer.Candidates(FragmentPosition.Core, request).Count;

            request.Idiolect = VoiceProfile.Neutral.RequestedIdiolect();
            Assert.Equal(unconstrained, scene.Realizer.Candidates(FragmentPosition.Core, request).Count);

            request.Idiolect = new VoiceProfile { Verbosity = 0.0 }.RequestedIdiolect();
            Assert.True(
                scene.Realizer.Candidates(FragmentPosition.Core, request).Count < unconstrained,
                "a specified habit narrowed nothing, so the corpus does not support the axis");
        }

        // -- what happens when nothing satisfies the constraint ---------------------------------------

        /// <summary>
        /// An optional slot narrowed to nothing falls silent, exactly as a slot with no eligible
        /// candidate always has. The line is shorter; it is not wrong, and it is not a refusal.
        /// </summary>
        [Fact]
        public void AnOptionalSlotWithNothingLeftIsSimplySkipped()
        {
            SpeechAct act = FragmentRealizationTests.Scene.Create().ThiefRefuses().Act;
            DialogueFragmentLibrary library = new DialogueFragmentLibrary();
            library.Register(Core("core.only", DialogueIdiolect.Terse));
            library.Register(Closer("close.ornate", DialogueIdiolect.Expansive));

            RealizedLine line = new DialogueRealizer(library).Realize(new RealizationRequest(act)
            {
                Idiolect = new VoiceProfile { Verbosity = 0.0 }.RequestedIdiolect(),
                Rng = new DeterministicRng(7UL)
            });

            Assert.True(line.Rendered, line.Refusal);
            Assert.Equal("The point.", line.Text);
            Assert.DoesNotContain("close.ornate", line.Fragments);
        }

        /// <summary>
        /// A required slot with nothing left refuses, with a reason and no text. The alternative -
        /// dropping the constraint and saying something the voice had ruled out - would make a
        /// habit a suggestion, and BQ-074's "refuses rather than repairs" is the rule that keeps a
        /// wording layer from inventing its way out of trouble.
        /// </summary>
        [Fact]
        public void AVoiceNoCoreCanSatisfyRefusesRatherThanSayingSomethingElse()
        {
            SpeechAct act = FragmentRealizationTests.Scene.Create().ThiefRefuses().Act;
            DialogueFragmentLibrary library = new DialogueFragmentLibrary();
            library.Register(Core("core.terse", DialogueIdiolect.Terse));
            library.Register(Core("core.clipped", DialogueIdiolect.Clipped));

            RealizedLine line = new DialogueRealizer(library).Realize(new RealizationRequest(act)
            {
                Idiolect = new VoiceProfile { Verbosity = 1.0, Cadence = 1.0 }.RequestedIdiolect(),
                Rng = new DeterministicRng(7UL)
            });

            Assert.False(line.Rendered);
            Assert.Equal(string.Empty, line.Text);
            Assert.NotEqual(string.Empty, line.Refusal);

            // The act is untouched by the failure: a line nobody had words for still knows what it
            // would have meant.
            Assert.Equal(act.Signature, line.Meaning);
        }

        // -- the corpus, and the metadata that reaches it ---------------------------------------------

        /// <summary>
        /// A vocabulary the corpus does not support is a voice axis that does nothing. Both poles
        /// of all three axes have to be authored somewhere, or a speaker specified on that axis
        /// sounds exactly like a speaker who was not.
        /// </summary>
        [Fact]
        public void TheShippedCorpusSupportsBothPolesOfEveryAxis()
        {
            IReadOnlyList<DialogueFragment> fragments = Shipped();

            foreach (string tag in DialogueIdiolect.Vocabulary)
            {
                Assert.True(
                    fragments.Any(fragment => fragment.IdiolectTags.Contains(tag)),
                    "no shipped fragment is marked " + tag);
            }

            // And the migration stayed a cross-section: most of the library is still wording every
            // voice can reach, which is what makes an unmarked fragment a safe default rather than
            // a gap.
            Assert.True(fragments.Count(fragment => fragment.IdiolectTags.Count == 0) > fragments.Count / 2);
        }

        /// <summary>No shipped fragment carries a mark and its own opposite. Enforced at load.</summary>
        [Fact]
        public void NoShippedFragmentContradictsItself()
        {
            foreach (DialogueFragment fragment in Shipped())
            {
                foreach (string tag in fragment.IdiolectTags)
                {
                    Assert.True(DialogueIdiolect.IsIdiolect(tag), fragment.Id + " carries " + tag);
                    Assert.DoesNotContain(DialogueIdiolect.Opposite(tag), fragment.IdiolectTags);
                }
            }
        }

        /// <summary>
        /// A voice can only narrow, never invent, so the corpus's job is to keep at least one
        /// candidate standing for every act at every extreme a <see cref="VoiceProfile"/> can
        /// take - in the pool that needs no optional reading to be eligible at all, the same
        /// unconditional pool <c>FragmentSemanticHonestyTests.EveryActCanStillBeSaidFromTheActAlone</c>
        /// already protects from going empty. That test guards the pool's existence; this one
        /// guards it from being narrowed to nothing by idiolect alone. A content pass once
        /// marked every unconditional core for `answer`, `deny` and `refuse` `terse` or
        /// `literal` and left none `expansive` or `figurative`, so a voice built from
        /// <see cref="VoiceProfile.Verbosity"/>=1, <see cref="VoiceProfile.Cadence"/>=1 and
        /// <see cref="VoiceProfile.Figuration"/>=1 - exactly BQ-142's own "isolate the axis"
        /// voice - could no longer answer, deny or refuse without an optional reading to fall
        /// back on, which a mundane conversation does not always supply.
        /// </summary>
        [Fact]
        public void NoVoiceExtremeStarvesAnActOfEveryUnconditionalCore()
        {
            List<DialogueFragment> unconditional = Shipped()
                .Where(f => f.Position == FragmentPosition.Core)
                .Where(f => f.Requires.Count == 1 && f.Requires[0].Key == DialogueReadings.Act)
                .ToList();

            HashSet<string> acts = new HashSet<string>(StringComparer.Ordinal);
            foreach (DialogueFragment fragment in unconditional)
            {
                foreach (string act in fragment.Requires[0].Values)
                {
                    acts.Add(act);
                }
            }

            double[] extremes = { 0.0, 1.0 };
            foreach (string act in acts)
            {
                List<DialogueFragment> pool = unconditional.Where(f => f.Requires[0].IsMetBy(act)).ToList();

                foreach (double verbosity in extremes)
                {
                    foreach (double cadence in extremes)
                    {
                        foreach (double figuration in extremes)
                        {
                            VoiceProfile voice = new VoiceProfile
                            {
                                Verbosity = verbosity,
                                Cadence = cadence,
                                Figuration = figuration
                            };

                            IReadOnlyList<string> idiolect = voice.RequestedIdiolect();
                            Assert.True(
                                pool.Any(f => f.FitsIdiolect(idiolect)),
                                act + " has no unconditional core left for a voice requesting " + string.Join("/", idiolect));
                        }
                    }
                }
            }
        }

        [Fact]
        public void AnUnknownIdiolectMarkIsRejected()
        {
            Assert.Contains(
                "Unknown idiolect tag",
                Rejects(Fragment("mod.bad").Set("idiolect", JsonValue.Array().Add(JsonValue.String("laconic")))),
                StringComparison.Ordinal);
        }

        [Fact]
        public void AnIdiolectFieldThatIsNotAnArrayIsRejected()
        {
            Assert.Contains(
                "idiolect must be an array",
                Rejects(Fragment("mod.bad").Set("idiolect", JsonValue.String(DialogueIdiolect.Terse))),
                StringComparison.Ordinal);
        }

        /// <summary>
        /// Both poles of one axis is a contradiction rather than a refinement, and the reason to
        /// reject it rather than to let it through is that it would not fail loudly: the fragment
        /// would simply vanish from every pool a voice with an opinion about that axis draws from.
        /// </summary>
        [Fact]
        public void AFragmentMarkedWithBothPolesOfOneAxisIsRejected()
        {
            Assert.Contains(
                "cannot be both",
                Rejects(Fragment("mod.bad").Set(
                    "idiolect",
                    JsonValue.Array()
                        .Add(JsonValue.String(DialogueIdiolect.Terse))
                        .Add(JsonValue.String(DialogueIdiolect.Expansive)))),
                StringComparison.Ordinal);
        }

        [Fact]
        public void AWellFormedIdiolectMarkLoads()
        {
            IReadOnlyList<ContentDiagnostic> diagnostics;
            IReadOnlyList<DialogueFragment> fragments = DialogueFragmentContent.LoadFragments(
                Bundle(Fragment("mod.good").Set(
                    "idiolect",
                    JsonValue.Array()
                        .Add(JsonValue.String(DialogueIdiolect.Terse))
                        .Add(JsonValue.String(DialogueIdiolect.Literal)))),
                out diagnostics);

            Assert.Empty(diagnostics);
            Assert.Equal(
                new[] { DialogueIdiolect.Terse, DialogueIdiolect.Literal },
                Assert.Single(fragments).IdiolectTags);
        }

        [Fact]
        public void AFragmentThatNamesNoIdiolectLoadsUnmarked()
        {
            IReadOnlyList<ContentDiagnostic> diagnostics;
            IReadOnlyList<DialogueFragment> fragments =
                DialogueFragmentContent.LoadFragments(Bundle(Fragment("mod.plain")), out diagnostics);

            Assert.Empty(diagnostics);
            Assert.Empty(Assert.Single(fragments).IdiolectTags);
        }

        // -- helpers ----------------------------------------------------------------------------------

        private static IEnumerable<VoiceProfile> EveryCorner()
        {
            yield return VoiceProfile.Neutral;
            yield return new VoiceProfile { Verbosity = 0.0 };
            yield return new VoiceProfile { Verbosity = 1.0 };
            yield return new VoiceProfile { Cadence = 0.0, Figuration = 0.0 };
            yield return new VoiceProfile { Cadence = 1.0, Figuration = 1.0 };
            yield return new VoiceProfile { Verbosity = 0.0, Cadence = 0.0, Figuration = 0.0 };
            yield return new VoiceProfile { Verbosity = 1.0, Cadence = 1.0, Figuration = 1.0 };

            // Tone and habits together, because they are separate requests and both narrow.
            yield return new VoiceProfile { Formality = 1.0, Verbosity = 0.0, Figuration = 0.0 };
        }

        private static HashSet<string> Rendered(
            FragmentRealizationTests.Scene scene,
            RealizationRequest request,
            VoiceProfile voice,
            string expectedMeaning)
        {
            request.Tone = voice.RequestedTone();
            request.Idiolect = voice.RequestedIdiolect();
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

        private static DialogueFragment Marked(params string[] idiolect)
        {
            return new DialogueFragment(
                "test.idiolect." + string.Join(".", idiolect),
                FragmentPosition.Modifier,
                "Text.",
                requires: null,
                forbids: null,
                toneTags: null,
                idiolectTags: idiolect,
                tags: null,
                repetitionGroup: null,
                slots: null,
                memorability: DialogueMemorability.Utility);
        }

        private static DialogueFragment Core(string id, params string[] idiolect)
        {
            return Authored(id, FragmentPosition.Core, "The point.", idiolect);
        }

        private static DialogueFragment Closer(string id, params string[] idiolect)
        {
            return Authored(id, FragmentPosition.Closer, "And so on.", idiolect);
        }

        private static DialogueFragment Authored(string id, FragmentPosition position, string text, string[] idiolect)
        {
            return new DialogueFragment(
                id,
                position,
                text,
                new[] { new FragmentRequirement(DialogueReadings.Act, new[] { "refuse" }) },
                forbids: null,
                toneTags: null,
                idiolectTags: idiolect,
                tags: null,
                repetitionGroup: id,
                slots: null,
                memorability: DialogueMemorability.Utility);
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

        private static IReadOnlyList<DialogueFragment> Shipped()
        {
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
            return fragments;
        }
    }
}
