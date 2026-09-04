using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Content;
using BrilliantQuesting.Dialogue;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Relationships;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-074. The first layer in the mod that produces English, and the whole of its job is to
    /// say what the simulation already decided.
    ///
    /// BQ-070 through BQ-073 built a speaker who can answer, refuse, let a question go, answer
    /// incompletely or assert something they do not believe, and did all of it without a word of
    /// dialogue. What that leaves is a world of meanings nobody can hear. This step adds the
    /// wording and has to add it without adding meaning, so the tests here are mostly tests that
    /// nothing was added:
    ///
    /// <list type="bullet">
    /// <item>one act says three recognizably different things and means the identical one;</item>
    /// <item>wording writes nothing and changes no decision;</item>
    /// <item>a refusal is never worded as an answer and an evasion never names what it slid past;</item>
    /// <item>a liar's denial is worded exactly as an honest one, because a lie is a stance against
    /// belief and never a turn of phrase;</item>
    /// <item>and an act nobody has words for comes back unsaid, with a reason, rather than
    /// approximated out of openers and closers.</item>
    /// </list>
    /// </summary>
    public class FragmentRealizationTests
    {
        // -- the done-when -------------------------------------------------------------------------

        /// <summary>
        /// The step's condition: one semantic act, one set of semantic data, three recognizably
        /// different lines.
        ///
        /// Recognizably different is taken at its word - the renderings must differ in the
        /// fragment that carries the point, not merely in whether somebody said "Right" first.
        /// And every one of them carries the same <see cref="RealizedLine.Meaning"/>, which is the
        /// half that matters: three ways of saying it, one thing said.
        /// </summary>
        [Fact]
        public void OneSemanticActRendersThreeRecognizablyDifferentWaysFromTheSameData()
        {
            Scene scene = Scene.Create();
            RealizationRequest request = scene.WitnessAnswers();

            HashSet<string> lines = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> cores = new HashSet<string>(StringComparer.Ordinal);
            foreach (RealizedLine line in scene.Renderings(request, 40))
            {
                Assert.True(line.Rendered, line.Refusal);
                Assert.Equal(request.Act.Signature, line.Meaning);
                lines.Add(line.Text);
                cores.Add(line.Core);
            }

            Assert.True(cores.Count >= 3, "only " + cores.Count + " ways of making the point: " + string.Join(" / ", cores));
            Assert.True(lines.Count >= 3, "only " + lines.Count + " lines: " + string.Join(" / ", lines));
        }

        /// <summary>
        /// The other half of the same claim, stated where it cannot be missed: the act is
        /// untouched by having been said. Same instance, same signature, same everything a
        /// consumer downstream would read.
        /// </summary>
        [Fact]
        public void SayingItThreeWaysLeavesTheActExactlyAsItWas()
        {
            Scene scene = Scene.Create();
            RealizationRequest request = scene.WitnessAnswers();
            string before = request.Act.Signature;

            foreach (RealizedLine line in scene.Renderings(request, 12))
            {
                Assert.Same(request.Act, line.Act);
            }

            Assert.Equal(before, request.Act.Signature);
        }

        // -- determinism ---------------------------------------------------------------------------

        [Fact]
        public void TheSameSemanticStateAndSeedAlwaysSaysTheSameWords()
        {
            Scene scene = Scene.Create();
            RealizationRequest first = scene.WitnessAnswers();
            RealizationRequest second = scene.WitnessAnswers();

            for (ulong seed = 1; seed <= 8; seed++)
            {
                first.Rng = new DeterministicRng(seed);
                second.Rng = new DeterministicRng(seed);
                Assert.Equal(scene.Realizer.Realize(first).Text, scene.Realizer.Realize(second).Text);
            }
        }

        /// <summary>
        /// And says it whatever else was said first. Choices are drawn from streams forked off the
        /// caller's rather than from its running state, so a line does not change because a
        /// different conversation happened earlier in the tick.
        /// </summary>
        [Fact]
        public void RealizationDoesNotDependOnHowManyLinesCameBefore()
        {
            Scene scene = Scene.Create();
            RealizationRequest request = scene.WitnessAnswers();
            DeterministicRng shared = new DeterministicRng(7UL);
            request.Rng = shared;

            string first = scene.Realizer.Realize(request).Text;
            for (int i = 0; i < 20; i++)
            {
                shared.NextUInt64();
            }

            Assert.Equal(first, scene.Realizer.Realize(request).Text);
        }

        // -- wording adds no meaning ---------------------------------------------------------------

        /// <summary>
        /// The invariant BQ-073 hands up to this layer, and the one it would be easiest to break by
        /// being helpful.
        ///
        /// The thief's falsification composes an ordinary <c>Deny</c>. Realized, it says exactly
        /// what the same denial says when no decision is passed at all - the wording layer is
        /// never told that a speaker is lying, so there is no phrasing a listener could learn to
        /// hear. The lie is still a lie: <c>Deception.Assess</c> reads it against his belief and
        /// says so, which is where a lie lives and always was.
        /// </summary>
        [Fact]
        public void WordingNeverLearnsThatTheSpeakerIsLying()
        {
            Scene scene = Scene.Create();
            RealizationRequest lying = scene.ThiefDenies();
            Assert.True(lying.Decision.WillLie);

            RealizationRequest bare = new RealizationRequest(lying.Act)
            {
                Claim = lying.Claim,
                Cast = lying.Cast
            };

            for (ulong seed = 1; seed <= 12; seed++)
            {
                lying.Rng = new DeterministicRng(seed);
                bare.Rng = new DeterministicRng(seed);
                RealizedLine said = scene.Realizer.Realize(lying);
                Assert.True(said.Rendered, said.Refusal);
                Assert.Equal(scene.Realizer.Realize(bare).Text, said.Text);
                Assert.False(SaysSoOutLoud(said.Text), said.Text);
            }

            Veracity veracity = Deception.Assess(scene.World, lying.Act);
            Assert.Equal(Sincerity.Insincere, veracity.Sincerity);
        }

        /// <summary>
        /// An evasion asserts nothing, and neither does any wording of one. The act carries no
        /// proposition (BQ-073), so nothing that would have named the claim's subject can be
        /// filled, and the thief's name never reaches the player's ear through a shrug.
        /// </summary>
        [Fact]
        public void AnEvasionNeverNamesTheClaimItSlidPast()
        {
            Scene scene = Scene.Create();
            RealizationRequest request = scene.WitnessEvades();
            Assert.Equal(SpeechActType.Evade, request.Act.Type);

            foreach (RealizedLine line in scene.Renderings(request, 40))
            {
                Assert.True(line.Rendered, line.Refusal);
                Assert.DoesNotContain(Scene.ThiefName, line.Text, StringComparison.Ordinal);
                Assert.DoesNotContain(Scene.Matter, line.Text, StringComparison.Ordinal);
            }
        }

        /// <summary>
        /// Every core fragment declares which act it says, so there is no wording a refusal and an
        /// answer both draw from. Withholding an answer never comes out sounding like giving one.
        /// </summary>
        [Fact]
        public void ARefusalIsNeverSaidAsAnAnswer()
        {
            Scene scene = Scene.Create();
            HashSet<string> answers = new HashSet<string>(
                scene.Renderings(scene.WitnessAnswers(), 40).Select(line => line.Core), StringComparer.Ordinal);
            HashSet<string> refusals = new HashSet<string>(
                scene.Renderings(scene.ThiefRefuses(), 40).Select(line => line.Core), StringComparer.Ordinal);

            Assert.NotEmpty(answers);
            Assert.NotEmpty(refusals);
            Assert.Empty(answers.Intersect(refusals, StringComparer.Ordinal));
        }

        /// <summary>
        /// Tone narrows which of the ways is chosen and can never change what is said. A curt
        /// speaker and an unconstrained one are the same act with the same meaning throughout.
        /// </summary>
        [Fact]
        public void ToneNarrowsTheChoiceWithoutChangingTheMeaning()
        {
            Scene scene = Scene.Create();
            RealizationRequest request = scene.ThiefRefuses();
            request.Tone = new[] { DialogueTones.Curt };

            IReadOnlyList<DialogueFragment> curt = scene.Realizer.Candidates(FragmentPosition.Closer, request);
            Assert.NotEmpty(curt);
            for (int i = 0; i < curt.Count; i++)
            {
                // Unmarked fragments suit any tone; a marked one may not contradict the axis this
                // request took a position on. Asking for curt rules out the wary end of directness
                // and says nothing about formality, warmth or sarcasm.
                Assert.DoesNotContain(DialogueTones.Wary, curt[i].ToneTags);
            }

            foreach (RealizedLine line in scene.Renderings(request, 20))
            {
                Assert.Equal(request.Act.Signature, line.Meaning);
            }
        }

        // -- realization writes nothing ------------------------------------------------------------

        /// <summary>
        /// The strongest form the claim can take: the whole saved world, byte for byte, before and
        /// after every act in the scene has been said several ways. No event, no fact, no belief,
        /// no relationship - realization has no world to write to and this is what that buys.
        /// </summary>
        [Fact]
        public void RealizationWritesNothingToTheWorld()
        {
            Scene scene = Scene.Create();

            // Deciding to speak is the world's business and setting the scene writes to it; the
            // snapshot is taken once every act exists, so what is measured is the saying alone.
            List<RealizationRequest> everything = scene.EveryKindOfLine().ToList();
            string before = WorldStateSerializer.Save(scene.World);

            foreach (RealizationRequest request in everything)
            {
                foreach (RealizedLine line in scene.Renderings(request, 12))
                {
                    Assert.Equal(request.Act.Signature, line.Meaning);
                }
            }

            Assert.Equal(before, WorldStateSerializer.Save(scene.World));
        }

        /// <summary>
        /// And the decision behind the line is an input, not a draft. Strategy, depth and tactic
        /// read the same after wording as before it.
        /// </summary>
        [Fact]
        public void RealizationLeavesTheDisclosureDecisionAsItFoundIt()
        {
            Scene scene = Scene.Create();
            RealizationRequest request = scene.WitnessAnswers();
            string before = request.Decision.ToString();

            scene.Renderings(request, 12).ToList();

            Assert.Equal(before, request.Decision.ToString());
        }

        // -- honest failure ------------------------------------------------------------------------

        /// <summary>
        /// An act the library has no words for is unsaid, with a reason. Not a vaguer line
        /// assembled from the trimmings: a line that says less than it should is a content bug and
        /// recoverable, and a line that says something nobody decided is a world bug and is not.
        ///
        /// Stated against a library built for the test rather than against the shipped one. It
        /// used to name an act the library happened not to cover yet, which made it a test about
        /// how much content exists - so authoring an apology broke it, and the invariant it is
        /// actually about was never in question.
        /// </summary>
        [Fact]
        public void AnActNobodyHasWordsForIsUnrealizedRatherThanApproximated()
        {
            Scene scene = Scene.Create();
            SpeechAct apology = SpeechAct.Compose(
                SpeechActType.Apologize,
                scene.Thief,
                scene.Player,
                new ActionBinding { Purpose = "the ring" });
            Assert.NotNull(apology);

            // A library with words in it, and none of them for this act.
            DialogueFragmentLibrary elsewhere = new DialogueFragmentLibrary();
            elsewhere.Register(new DialogueFragment(
                "test.core.ask",
                FragmentPosition.Core,
                "What do you know about it?",
                new[] { new FragmentRequirement(DialogueReadings.Act, new[] { "ask" }) },
                null, null, null, "ask", null));
            elsewhere.Register(new DialogueFragment(
                "test.opener.right",
                FragmentPosition.Opener,
                "Right.",
                null, null, null, null, "filler", null));

            RealizedLine line = new DialogueRealizer(elsewhere)
                .Realize(new RealizationRequest(apology) { Cast = scene.Cast });

            Assert.False(line.Rendered);
            Assert.Equal(string.Empty, line.Text);
            Assert.Empty(line.Fragments);
            Assert.Contains("apologize", line.Refusal, StringComparison.Ordinal);
        }

        [Fact]
        public void ThereIsNothingToSayWhenThereIsNoAct()
        {
            Scene scene = Scene.Create();

            Assert.False(scene.Realizer.Realize(new RealizationRequest(null)).Rendered);
            Assert.False(scene.Realizer.Realize(null).Rendered);
            Assert.Equal(string.Empty, scene.Realizer.Realize(null).Text);
        }

        /// <summary>
        /// A request whose parts describe a situation the semantic layer never produced is refused
        /// rather than reconciled. Picking one of two speakers, or wording a decision about one
        /// claim as an act about another, is exactly the invention this layer must not perform.
        /// </summary>
        [Fact]
        public void ARequestWhosePartsDisagreeIsRefused()
        {
            Scene scene = Scene.Create();
            RealizationRequest answer = scene.WitnessAnswers();

            RealizedLine wrongSpeaker = scene.Realizer.Realize(new RealizationRequest(scene.ThiefDenies().Act)
            {
                Decision = answer.Decision,
                Cast = scene.Cast
            });
            Assert.False(wrongSpeaker.Rendered);
            Assert.Contains("speaker", wrongSpeaker.Refusal, StringComparison.Ordinal);

            RealizedLine wrongClaim = scene.Realizer.Realize(new RealizationRequest(answer.Act)
            {
                Decision = answer.Decision,
                Claim = scene.World.Knowledge.GetFact(scene.OtherFact),
                Cast = scene.Cast
            });
            Assert.False(wrongClaim.Rendered);
            Assert.Contains("claim", wrongClaim.Refusal, StringComparison.Ordinal);
        }

        /// <summary>
        /// A fragment that would have named somebody the caller did not put on stage is not used,
        /// and nothing falls back to "someone". An unfilled placeholder never reaches the text.
        /// </summary>
        [Fact]
        public void AFragmentThatWouldNameSomebodyUnnamedIsNotUsed()
        {
            Scene scene = Scene.Create();
            RealizationRequest anonymous = scene.WitnessAnswers();
            anonymous.Cast = DialogueCast.Anonymous;

            foreach (RealizedLine line in scene.Renderings(anonymous, 30))
            {
                Assert.True(line.Rendered, line.Refusal);
                Assert.DoesNotContain("{", line.Text, StringComparison.Ordinal);
                Assert.DoesNotContain(Scene.ThiefName, line.Text, StringComparison.Ordinal);
            }
        }

        // -- the schema is content, and it is strict -----------------------------------------------

        [Fact]
        public void TheShippedFragmentLibraryLoadsCleanly()
        {
            IReadOnlyList<ContentDiagnostic> diagnostics;
            IReadOnlyList<DialogueFragment> fragments = DialogueFragmentContent.LoadFragments(ShippedBundle(), out diagnostics);

            Assert.Empty(diagnostics);
            Assert.NotEmpty(fragments);
            foreach (FragmentPosition position in Enum.GetValues(typeof(FragmentPosition)))
            {
                Assert.Contains(fragments, fragment => fragment.Position == position);
            }
        }

        /// <summary>
        /// Every shipped core fragment says which act it is for. Enforced at load, so a wording
        /// cannot drift onto an act it was never written for.
        /// </summary>
        [Fact]
        public void EveryShippedCoreFragmentDeclaresWhichActItSays()
        {
            IReadOnlyList<ContentDiagnostic> diagnostics;
            IReadOnlyList<DialogueFragment> fragments = DialogueFragmentContent.LoadFragments(ShippedBundle(), out diagnostics);

            foreach (DialogueFragment fragment in fragments.Where(f => f.Position == FragmentPosition.Core))
            {
                Assert.Contains(fragment.Requires, requirement => requirement.Key == DialogueReadings.Act);
            }
        }

        [Fact]
        public void ACoreFragmentWithoutAnActIsRejected()
        {
            Assert.Contains(
                "which act",
                Rejects(Fragment("core.loose", "core", "It was him.")),
                StringComparison.Ordinal);
        }

        [Fact]
        public void AnUnknownConditionOrValueIsRejected()
        {
            JsonValue unknownKey = Fragment("core.bad.key", "core", "No.")
                .Set("requires", JsonValue.Object().Set("mood", "grim"));
            Assert.Contains("Unknown fragment condition", Rejects(unknownKey), StringComparison.Ordinal);

            JsonValue unknownValue = Fragment("core.bad.value", "core", "No.")
                .Set("requires", JsonValue.Object().Set(DialogueReadings.Act, "grovel"));
            Assert.Contains("cannot read as", Rejects(unknownValue), StringComparison.Ordinal);
        }

        /// <summary>
        /// The architectural refusal, enforced where content is read: no fragment may be selected
        /// on whether the speaker is falsifying. There is no way to author a tell.
        /// </summary>
        [Fact]
        public void WordingMayNotBeSelectedOnFalsification()
        {
            JsonValue tell = Fragment("core.tell", "core", "No.")
                .Set("requires", JsonValue.Object()
                    .Set(DialogueReadings.Act, "deny")
                    .Set(DialogueReadings.Tactic, "falsify"));

            Assert.Contains("whether the speaker is lying", Rejects(tell), StringComparison.Ordinal);
        }

        [Fact]
        public void AnUnknownPlaceholderIsRejected()
        {
            JsonValue invented = Fragment("core.invented", "core", "It was {culprit}.")
                .Set("requires", JsonValue.Object().Set(DialogueReadings.Act, "answer"));

            Assert.Contains("unknown placeholder", Rejects(invented), StringComparison.Ordinal);
        }

        [Fact]
        public void ADuplicateFragmentIdIsRejected()
        {
            JsonValue one = Fragment("core.twice", "core", "No.")
                .Set("requires", JsonValue.Object().Set(DialogueReadings.Act, "refuse"));
            JsonValue two = Fragment("core.twice", "core", "Never.")
                .Set("requires", JsonValue.Object().Set(DialogueReadings.Act, "refuse"));

            IReadOnlyList<ContentDiagnostic> diagnostics;
            DialogueFragmentContent.LoadFragments(Bundle(one, two), out diagnostics);

            Assert.Single(diagnostics);
            Assert.Contains("duplicated", diagnostics[0].Message, StringComparison.Ordinal);
        }

        // -- scaffolding ---------------------------------------------------------------------------

        /// <summary>
        /// Whether the words themselves give it away. Checked on word boundaries, because
        /// "believe" contains "lie" and a test that could not tell the two apart would be
        /// guarding nothing.
        /// </summary>
        private static bool SaysSoOutLoud(string text)
        {
            string[] tells = { "lie", "lied", "lies", "lying", "liar", "false", "untrue", "dishonest" };
            foreach (string word in text.Split(new[] { ' ', '.', ',', '?', '!', ';', ':' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (tells.Contains(word.ToLowerInvariant()))
                {
                    return true;
                }
            }

            return false;
        }

        private static string Rejects(JsonValue fragment)
        {
            IReadOnlyList<ContentDiagnostic> diagnostics;
            IReadOnlyList<DialogueFragment> fragments = DialogueFragmentContent.LoadFragments(Bundle(fragment), out diagnostics);

            Assert.Empty(fragments);
            Assert.Single(diagnostics);
            return diagnostics[0].Message;
        }

        private static JsonValue Fragment(string id, string position, string text)
        {
            return JsonValue.Object().Set("id", id).Set("position", position).Set("text", text);
        }

        private static ContentBundle Bundle(params JsonValue[] fragments)
        {
            JsonValue array = JsonValue.Array();
            for (int i = 0; i < fragments.Length; i++)
            {
                array.Add(fragments[i]);
            }

            return new ContentBundle(
                ContentBundle.CurrentVersion,
                new[]
                {
                    new ContentRecord("fragments.test", DialogueFragmentContent.Kind, JsonValue.Object().Set("fragments", array))
                });
        }

        private static ContentBundle ShippedBundle()
        {
            ContentBundleLoadResult bundle = ContentBundleLoader.LoadFile(
                Path.Combine(RepositoryRoot(), "Package", "content.bqc"));
            Assert.Empty(bundle.Diagnostics);
            return bundle.Bundle;
        }

        private static string RepositoryRoot()
        {
            DirectoryInfo directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "ElinBrilliantQuesting.sln")))
            {
                directory = directory.Parent;
            }

            Assert.NotNull(directory);
            return directory.FullName;
        }

        /// <summary>
        /// The BQ-073 interrogation, said out loud. The same theft, the same four people and the
        /// same disclosure machinery; the only thing this file adds is that somebody hears it.
        /// </summary>
        /// <summary>
        /// Internal rather than private so BQ-075's voice tests can render the identical acts and
        /// decisions this step already proved out, instead of re-deriving a second scene.
        /// </summary>
        internal sealed class Scene
        {
            internal const string ThiefName = "Kip";
            internal const string Matter = "silver ring";

            private Scene(NarrativeWorldState world)
            {
                World = world;
            }

            internal NarrativeWorldState World { get; }

            internal DialogueRealizer Realizer { get; private set; }

            internal DialogueCast Cast { get; private set; }

            internal EntityId Thief { get; private set; }

            internal EntityId Witness { get; private set; }

            internal EntityId Player { get; private set; }

            internal EntityId TheftFact { get; private set; }

            /// <summary>Something true about the same man that is nobody's secret.</summary>
            internal EntityId OtherFact { get; private set; }

            internal GameTime Now => GameTime.Zero;

            internal static Scene Create()
            {
                Scene scene = new Scene(new NarrativeWorldState(20260902UL));
                scene.Thief = scene.Person(ThiefName);
                scene.Witness = scene.Person("Mira");
                scene.Player = scene.Person("Wren");

                Fact theft = new Fact(
                    scene.World.NewId("fact"),
                    scene.Thief,
                    FactPredicates.Stole,
                    EntityId.None,
                    Matter,
                    TruthState.True,
                    secrecy: 40);
                scene.World.Knowledge.AddFact(theft);
                scene.TheftFact = theft.Id;

                Fact market = new Fact(
                    scene.World.NewId("fact"),
                    scene.Thief,
                    FactPredicates.LocatedAt,
                    EntityId.None,
                    "the north market",
                    TruthState.True,
                    secrecy: 0);
                scene.World.Knowledge.AddFact(market);
                scene.OtherFact = market.Id;

                scene.World.Knowledge.Teach(scene.Thief, theft.Id, KnowledgeSource.Participant, 1.0, scene.Now, false);
                scene.World.Knowledge.Teach(scene.Witness, theft.Id, KnowledgeSource.Witnessed, 0.9, scene.Now, false);
                scene.World.Knowledge.Teach(scene.Witness, market.Id, KnowledgeSource.Witnessed, 0.9, scene.Now, false);

                foreach (EntityId person in new[] { scene.Thief, scene.Witness })
                {
                    scene.World.Registry.GetNpc(person).Emotions.Set(EmotionalState.Fear, 0.0);
                }

                IReadOnlyList<ContentDiagnostic> diagnostics;
                scene.Realizer = new DialogueRealizer(DialogueFragmentContent.CreateLibrary(ShippedBundle(), out diagnostics));
                Assert.Empty(diagnostics);
                scene.Cast = DialogueCast.From(scene.World, scene.Thief, scene.Witness, scene.Player);
                return scene;
            }

            /// <summary>The witness, who likes the player enough to say what she saw and how.</summary>
            internal RealizationRequest WitnessAnswers()
            {
                World.Relationships.Connect(Witness, Player, RelationKind.Friend, 70);
                World.Registry.GetNpc(Witness).Personality.Honesty = 0.9;
                return Line(Disclosure.Decide(World, Witness, Player, TheftFact, Now), Question(Witness), TheftFact);
            }

            /// <summary>The witness who will not have this conversation and does not say so.</summary>
            internal RealizationRequest WitnessEvades()
            {
                World.Relationships.Connect(Witness, Player, RelationKind.Rival, -20);
                return Line(Disclosure.Decide(World, Witness, Player, TheftFact, Now), Question(Witness), TheftFact);
            }

            /// <summary>The thief, honest enough not to lie about it and unwilling to say it.</summary>
            internal RealizationRequest ThiefRefuses()
            {
                World.Registry.GetNpc(Thief).Personality.Honesty = 0.9;
                return Line(Disclosure.Decide(World, Thief, Player, TheftFact, Now), Question(Thief), TheftFact);
            }

            /// <summary>The thief who denies what he knows he did.</summary>
            internal RealizationRequest ThiefDenies()
            {
                World.Registry.GetNpc(Thief).Personality.Honesty = 0.1;
                World.Registry.GetNpc(Thief).Emotions.Set(EmotionalState.Fear, 0.8);
                return Line(Disclosure.Decide(World, Thief, Player, TheftFact, Now), Question(Thief), TheftFact);
            }

            /// <summary>The player's own question, which is an act like any other.</summary>
            internal RealizationRequest PlayerAsks()
            {
                return new RealizationRequest(Question(Witness))
                {
                    Claim = World.Knowledge.GetFact(TheftFact),
                    Cast = Cast
                };
            }

            internal IEnumerable<RealizationRequest> EveryKindOfLine()
            {
                yield return PlayerAsks();
                yield return WitnessAnswers();
                yield return WitnessEvades();
                yield return ThiefRefuses();
                yield return ThiefDenies();
            }

            internal List<RealizedLine> Renderings(RealizationRequest request, int seeds)
            {
                List<RealizedLine> lines = new List<RealizedLine>();
                for (ulong seed = 1; seed <= (ulong)seeds; seed++)
                {
                    request.Rng = new DeterministicRng(seed);
                    lines.Add(Realizer.Realize(request));
                }

                return lines;
            }

            private RealizationRequest Line(DisclosureDecision decision, SpeechAct question, EntityId claim)
            {
                SpeechAct act = Disclosure.Compose(decision, question);
                Assert.NotNull(act);
                return new RealizationRequest(act)
                {
                    Decision = decision,
                    Claim = World.Knowledge.GetFact(claim),
                    Cast = Cast
                };
            }

            private SpeechAct Question(EntityId asked)
            {
                return SpeechAct.Compose(
                    SpeechActType.Ask,
                    Player,
                    asked,
                    new ActionBinding { PropositionFact = TheftFact });
            }

            private EntityId Person(string name)
            {
                EntityId id = World.NewId("npc");
                World.Registry.Add(new NarrativeNpc(id, name));
                return id;
            }
        }
    }
}
