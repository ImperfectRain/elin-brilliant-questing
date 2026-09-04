using System;
using System.Collections.Generic;
using System.Text;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Content;
using BrilliantQuesting.Continuity;
using BrilliantQuesting.Dialogue;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// The seam between BQ-070's vocabulary of meaning and BQ-074's vocabulary of wording, and the
    /// one thing that has to be true of it: they say the same words.
    ///
    /// BQ-083 proved they could stop doing so silently. It added
    /// <see cref="SpeechActType.Promise"/> and <see cref="SpeechActDirection.CommitsToAction"/> to
    /// the semantic layer, where they were correct and complete, and
    /// <see cref="DialogueReadings"/> kept a hand-written copy of the older vocabulary that nobody
    /// updated. The result was a well-formed promise the simulation was happy to produce, a
    /// reading that correctly read it as <c>promise</c>, and a content layer that would refuse to
    /// let anybody author the words - so the act could never be said, and the refusal named a
    /// missing wording rather than the missing vocabulary entry that caused it.
    ///
    /// These tests are the structural version of "do not let that happen again". They do not check
    /// that <c>promise</c> is present; they check that every value the semantic layer holds is
    /// present, so the next act, direction, tactic or callback kind added anywhere upstream is
    /// covered the moment it is declared.
    ///
    /// The slug rule is written out again here on purpose. A test that asked the production helper
    /// what a name should look like would agree with it by construction and prove nothing; this is
    /// an independent statement of the same rule, so the two have to actually match.
    /// </summary>
    public class SemanticRealizationVocabularyTests
    {
        // -- the derived vocabulary ------------------------------------------------------------------

        /// <summary>
        /// Every semantic value wording is allowed to see has a reading value it can be named by.
        ///
        /// The whole audit in one assertion, run over the enums rather than over a list somebody
        /// typed: a value added to any of these and not reachable from content would fail here
        /// before it could fail in a scene.
        /// </summary>
        [Fact]
        public void EverySemanticValueRealizationExposesIsInTheReadingVocabulary()
        {
            AssertEveryValueReads<SpeechActType>(DialogueReadings.Act);
            AssertEveryValueReads<SpeechActStance>(DialogueReadings.Stance);
            AssertEveryValueReads<SpeechActDirection>(DialogueReadings.Direction);
            AssertEveryValueReads<SpeechActType>(DialogueReadings.Reply);
            AssertEveryValueReads<DisclosureStrategy>(DialogueReadings.Strategy);
            AssertEveryValueReads<DisclosureDepth>(DialogueReadings.Depth);
            AssertEveryValueReads<CallbackKind>(DialogueReadings.Callback);
            AssertEveryValueReads<CallbackRoute>(DialogueReadings.CallbackRoute);
        }

        /// <summary>
        /// The two BQ-083 actually dropped, named where a regression would be unmissable.
        /// </summary>
        [Fact]
        public void ThePromiseVocabularyReachesWording()
        {
            Assert.True(DialogueReadings.IsValue(DialogueReadings.Act, "promise"));
            Assert.True(DialogueReadings.IsValue(DialogueReadings.Reply, "promise"));
            Assert.True(DialogueReadings.IsValue(DialogueReadings.Direction, "commits_to_action"));
        }

        /// <summary>
        /// Every tactic but one, and the one is still the one. Derivation must not have quietly
        /// readmitted <see cref="DisclosureTactic.Falsify"/>: wording is never selected on whether
        /// the speaker is lying, and that subtraction is the single deliberate gap between the
        /// semantic vocabulary and the readable one.
        /// </summary>
        [Fact]
        public void EveryTacticButFalsificationReachesWording()
        {
            foreach (DisclosureTactic tactic in Enum.GetValues(typeof(DisclosureTactic)))
            {
                bool readable = DialogueReadings.IsValue(DialogueReadings.Tactic, Slug(tactic.ToString()));
                Assert.Equal(tactic != DisclosureTactic.Falsify, readable);
            }
        }

        /// <summary>
        /// Keys and values stay one table. <see cref="DialogueReadings.Vocabulary"/> is what a
        /// caller enumerating the readings gets, and a key it named that validation did not accept
        /// - or the other way about - would be the same drift in a different place.
        /// </summary>
        [Fact]
        public void EveryKeyInTheVocabularyIsAKeyValidationAccepts()
        {
            Assert.NotEmpty(DialogueReadings.Vocabulary);
            foreach (string key in DialogueReadings.Vocabulary)
            {
                Assert.True(DialogueReadings.IsKey(key), key + " is named in the vocabulary and unknown to validation");
            }
        }

        // -- the reading agrees with it ----------------------------------------------------------------

        /// <summary>
        /// The end-to-end form, and the one that would have caught BQ-083 without anybody knowing
        /// what to look for: one well-formed act of every type in the vocabulary, every reading key
        /// it produces, and every value accepted by the key that produced it.
        ///
        /// An act whose reading says something content may not name is an act that cannot be
        /// worded, whatever the library holds.
        /// </summary>
        [Fact]
        public void EveryActReadsAsValuesItsOwnVocabularyAccepts()
        {
            Exchange scene = Exchange.Create();

            foreach (SpeechAct act in scene.OneOfEveryAct())
            {
                RealizationReading reading = RealizationReading.Of(act, null, scene.Claim, scene.Cast);
                foreach (string key in DialogueReadings.Vocabulary)
                {
                    string value = reading.Value(key);
                    Assert.True(
                        DialogueReadings.IsValue(key, value),
                        act.Type + " reads " + key + " as '" + value + "', which no fragment may name");
                }
            }
        }

        /// <summary>
        /// Naming every act type is not the same as covering every act type: a vocabulary that grew
        /// a row and a test that did not would pass the loop above while proving less than it
        /// claims.
        /// </summary>
        [Fact]
        public void TheseTestsExerciseEveryActInTheVocabulary()
        {
            Exchange scene = Exchange.Create();
            HashSet<SpeechActType> covered = new HashSet<SpeechActType>();
            foreach (SpeechAct act in scene.OneOfEveryAct())
            {
                covered.Add(act.Type);
            }

            foreach (SpeechActType type in Enum.GetValues(typeof(SpeechActType)))
            {
                Assert.Contains(type, covered);
            }
        }

        // -- the content boundary ----------------------------------------------------------------------

        /// <summary>
        /// A wording may be authored for every act the simulation can produce.
        ///
        /// This is the failure BQ-083 shipped, stated as a rule rather than as a case: content
        /// validation rejected <c>act: promise</c>, so the only way to say a promise was barred at
        /// load time and the layer above reported a missing fragment for an act nobody could have
        /// written one for.
        /// </summary>
        [Fact]
        public void AWordingMayBeAuthoredForEveryActInTheVocabulary()
        {
            foreach (SpeechActType type in Enum.GetValues(typeof(SpeechActType)))
            {
                string slug = Slug(type.ToString());

                IReadOnlyList<ContentDiagnostic> diagnostics;
                IReadOnlyList<DialogueFragment> fragments = DialogueFragmentContent.LoadFragments(
                    Bundle(Core("core." + slug, "Something.", slug)), out diagnostics);

                Assert.Empty(diagnostics);
                DialogueFragment fragment = Assert.Single(fragments);
                Assert.Equal("core." + slug, fragment.Id);
            }
        }

        /// <summary>
        /// A promise said, from a wording authored for it, through the ordinary path: content
        /// validation, library, realizer. The line is words and the meaning is untouched.
        /// </summary>
        [Fact]
        public void APromiseIsSaidFromAWordingAuthoredForIt()
        {
            Exchange scene = Exchange.Create();
            DialogueRealizer realizer = Realizer(
                Core("core.promise", "You will have it back by the week's end.", "promise")
                    .Set("requires", JsonValue.Object()
                        .Set(DialogueReadings.Act, "promise")
                        .Set(DialogueReadings.Direction, "commits_to_action")));

            SpeechAct promise = scene.Promise();
            RealizedLine line = realizer.Realize(new RealizationRequest(promise) { Cast = scene.Cast });

            Assert.True(line.Rendered, line.Refusal);
            Assert.Equal("core.promise", line.Core);
            Assert.Contains("You will have it back by the week's end.", line.Text, StringComparison.Ordinal);
            Assert.Equal(promise.Signature, line.Meaning);
            Assert.Equal(SpeechActType.Promise, promise.Type);
            Assert.Equal(SpeechActDirection.CommitsToAction, promise.Direction);
        }

        /// <summary>
        /// And is not said for anything else. A wording authored for a commitment is not a wording
        /// for an answer, however well the sentence would scan - the point of conditioning on the
        /// act is that the words can never drift onto a different meaning.
        /// </summary>
        [Fact]
        public void AWordingAuthoredForAPromiseIsNotSaidForAnythingElse()
        {
            Exchange scene = Exchange.Create();
            DialogueRealizer realizer = Realizer(
                Core("core.promise", "You will have it back by the week's end.", "promise"));

            RealizedLine line = realizer.Realize(new RealizationRequest(scene.Answer()) { Cast = scene.Cast });

            Assert.False(line.Rendered);
            Assert.Contains("answer", line.Refusal, StringComparison.Ordinal);
        }

        /// <summary>
        /// Honest failure survives the repair. A promise nobody has written words for is still
        /// unsaid with a reason, rather than approximated out of a wording meant for something
        /// else: widening the vocabulary widened what may be authored, not what may be said
        /// without being authored.
        /// </summary>
        [Fact]
        public void APromiseNobodyHasWordsForIsStillUnsaid()
        {
            Exchange scene = Exchange.Create();
            DialogueRealizer realizer = Realizer(Core("core.answer", "He took it.", "answer"));

            RealizedLine line = realizer.Realize(new RealizationRequest(scene.Promise()) { Cast = scene.Cast });

            Assert.False(line.Rendered);
            Assert.Equal(string.Empty, line.Text);
            Assert.Contains("promise", line.Refusal, StringComparison.Ordinal);
        }

        /// <summary>
        /// A value outside the semantic layer is still refused. The vocabulary is derived, not
        /// opened: content may name every meaning that exists and no meaning that does not.
        /// </summary>
        [Fact]
        public void AValueTheSemanticLayerDoesNotHoldIsStillRejected()
        {
            IReadOnlyList<ContentDiagnostic> diagnostics;
            DialogueFragmentContent.LoadFragments(
                Bundle(Core("core.vow", "I so swear.", "vow")), out diagnostics);

            ContentDiagnostic diagnostic = Assert.Single(diagnostics);
            Assert.Contains("cannot read as vow", diagnostic.Message, StringComparison.Ordinal);
        }

        // -- scaffolding -------------------------------------------------------------------------------

        private static void AssertEveryValueReads<TEnum>(string key)
            where TEnum : struct
        {
            foreach (object value in Enum.GetValues(typeof(TEnum)))
            {
                string slug = Slug(value.ToString());
                Assert.True(
                    DialogueReadings.IsValue(key, slug),
                    typeof(TEnum).Name + "." + value + " reads as '" + slug + "', which " + key + " does not accept");
            }
        }

        /// <summary>
        /// The slug rule, stated independently of the implementation it is checking.
        /// <c>CommitsToAction</c> becomes <c>commits_to_action</c>.
        /// </summary>
        private static string Slug(string name)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]))
                {
                    sb.Append('_');
                }

                sb.Append(char.ToLowerInvariant(name[i]));
            }

            return sb.ToString();
        }

        private static DialogueRealizer Realizer(params JsonValue[] fragments)
        {
            IReadOnlyList<ContentDiagnostic> diagnostics;
            DialogueFragmentLibrary library = DialogueFragmentContent.CreateLibrary(Bundle(fragments), out diagnostics);
            Assert.Empty(diagnostics);
            return new DialogueRealizer(library);
        }

        private static JsonValue Core(string id, string text, string act)
        {
            return JsonValue.Object()
                .Set("id", id)
                .Set("position", "core")
                .Set("text", text)
                .Set("requires", JsonValue.Object().Set(DialogueReadings.Act, act));
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

        /// <summary>
        /// One well-formed act of every type in the vocabulary, built through
        /// <see cref="SpeechAct.Compose"/> so nothing here can assert a shape the semantic layer
        /// would have refused.
        /// </summary>
        private sealed class Exchange
        {
            private Exchange(NarrativeWorldState world)
            {
                World = world;
            }

            internal NarrativeWorldState World { get; }

            internal DialogueCast Cast { get; private set; }

            internal EntityId Speaker { get; private set; }

            internal EntityId Listener { get; private set; }

            /// <summary>Talked about, never present. Gossip needs somebody who is not in the room.</summary>
            internal EntityId Absent { get; private set; }

            internal Fact Claim { get; private set; }

            internal static Exchange Create()
            {
                Exchange scene = new Exchange(new NarrativeWorldState(20260903UL));
                scene.Speaker = scene.Person("Kip");
                scene.Listener = scene.Person("Wren");
                scene.Absent = scene.Person("Tovar");

                Fact theft = new Fact(
                    scene.World.NewId("fact"), scene.Speaker, FactPredicates.Stole, EntityId.None,
                    "silver ring", TruthState.True, secrecy: 40);
                scene.World.Knowledge.AddFact(theft);
                scene.Claim = theft;

                scene.Cast = DialogueCast.From(scene.World, scene.Speaker, scene.Listener, scene.Absent);
                return scene;
            }

            internal SpeechAct Promise()
            {
                return Composed(SpeechActType.Promise, Matter("bring back the ring"));
            }

            internal SpeechAct Answer()
            {
                return Composed(SpeechActType.Answer, Matter("where it went"), inReplyTo: Asked());
            }

            internal IEnumerable<SpeechAct> OneOfEveryAct()
            {
                yield return Composed(SpeechActType.Ask, Matter("where it went"));
                yield return Answer();
                yield return Composed(SpeechActType.Accuse, Proposition(), referent: Listener);
                yield return Composed(SpeechActType.Deny, Proposition());
                yield return Composed(SpeechActType.Admit, Proposition());
                yield return Composed(SpeechActType.Request, Matter("the ring back"));
                yield return Composed(SpeechActType.Refuse, ActionBinding.Empty, inReplyTo: Requested());
                yield return Composed(SpeechActType.Threaten, Matter("the guard"));
                yield return Composed(SpeechActType.Apologize, Matter("the ring"));
                yield return Composed(SpeechActType.Gossip, Proposition(), referent: Absent);
                yield return Composed(SpeechActType.Evade, ActionBinding.Empty, inReplyTo: Asked());
                yield return Promise();
                yield return Composed(SpeechActType.Inform, Proposition());
                yield return Composed(SpeechActType.Warn, Matter("the north road"));
                yield return Composed(SpeechActType.Offer, Matter("the price of it"));
                yield return Composed(SpeechActType.Forgive, ActionBinding.Empty, referent: Listener);
            }

            /// <summary>The listener's question, which is what a reply has to have something to be.</summary>
            private SpeechAct Asked()
            {
                SpeechAct asked = SpeechAct.Compose(
                    SpeechActType.Ask, Listener, Speaker, Matter("where it went"));
                Assert.NotNull(asked);
                return asked;
            }

            private SpeechAct Requested()
            {
                SpeechAct requested = SpeechAct.Compose(
                    SpeechActType.Request, Listener, Speaker, Matter("the ring back"));
                Assert.NotNull(requested);
                return requested;
            }

            private SpeechAct Composed(
                SpeechActType type,
                ActionBinding content,
                EntityId referent = default,
                SpeechAct inReplyTo = null)
            {
                SpeechAct act = SpeechAct.Compose(type, Speaker, Listener, content, referent, inReplyTo);
                Assert.NotNull(act);
                return act;
            }

            private ActionBinding Proposition() => new ActionBinding { PropositionFact = Claim.Id };

            private static ActionBinding Matter(string purpose) => new ActionBinding { Purpose = purpose };

            private EntityId Person(string name)
            {
                EntityId id = World.NewId("npc");
                World.Registry.Add(new NarrativeNpc(id, name));
                return id;
            }
        }
    }
}
