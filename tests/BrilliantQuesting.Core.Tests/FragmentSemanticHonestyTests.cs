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
    /// BQ-147. The rule the whole wording layer is built on, checked against the wording that
    /// actually shipped: an authored line may not assert anything its eligibility does not
    /// guarantee.
    ///
    /// BQ-074 made that structural for the two obvious cases - a fragment carries no proposition
    /// and can name nobody the act did not already involve - and every step since has widened what
    /// a fragment may be *chosen* on. What none of them checked is the quiet half: an English
    /// sentence asserts a great deal more than the proposition it carries. "I saw it" claims a
    /// route to knowing. "You already knew" claims something about somebody else's head. "You
    /// still owe me" claims a debt, and claims it in a direction. None of those is the act's
    /// meaning, all of them are heard as fact by whoever is listening, and a line that says one
    /// its conditions do not entail is the wording layer inventing world state one sentence at a
    /// time - which is exactly the failure mode the layer exists to make impossible.
    ///
    /// <b>The classes are the tests.</b> The audit that produced this file found seven kinds of
    /// hidden assertion rather than a list of unlucky sentences, so the coverage is per class and
    /// data-driven: each table names the fragments that were found asserting something and the
    /// reading that now grounds them, so removing the grounding is a failing test rather than a
    /// line nobody reads again. Two of the classes also get a behavioural test over shipped
    /// content, because a pin on a requirement proves the requirement is written down and not that
    /// it does any work.
    ///
    /// <b>What is deliberately not here.</b> There is no checker that reads the English. A line
    /// that asserts something is recognisable to a person and not to a rule, and a heuristic over
    /// prose would fail in both directions - it would miss the assertion carried by "either", and
    /// it would refuse a perfectly grounded sentence for containing a word. The one rule that is
    /// mechanical - a fragment naming somebody must say where they are standing - lives at load in
    /// <see cref="DialogueFragmentContent"/>, and is checked here against the shipped corpus.
    /// </summary>
    public class FragmentSemanticHonestyTests
    {
        // -- provenance: how the speaker comes to know ---------------------------------------------

        /// <summary>
        /// The largest class, and the one that needed a reading rather than a rewrite.
        ///
        /// Every one of these lines names a route to knowing, and before BQ-147 the strongest
        /// thing any of them could be conditioned on was <see cref="DialogueReadings.Commitment"/>
        /// - how firmly the speaker would stand behind the claim, which is a different question
        /// with a different answer. A confident believer of a fence-side rumour reads
        /// <c>committed</c>, so "I watched it happen" was available to them; a hesitant eyewitness
        /// reads <c>hedged</c>, so "I have it secondhand" was available to them. The route is on
        /// the knowledge record the graph already holds, and now so is the wording.
        /// </summary>
        [Theory]
        [InlineData("core.answer.saw.it", "witnessed", "participant")]
        [InlineData("core.answer.was.there", "witnessed", "participant")]
        [InlineData("core.answer.not.a.story", "witnessed")]
        [InlineData("core.answer.theft.leaving", "witnessed")]
        [InlineData("core.answer.theft.hurry", "witnessed")]
        [InlineData("core.answer.secondhand", "hearsay")]
        [InlineData("core.answer.fingerprints", "hearsay")]
        [InlineData("core.answer.source.gone", "hearsay")]
        [InlineData("core.answer.worked.it.out", "inference")]
        [InlineData("core.answer.in.the.record", "document")]
        [InlineData("core.accuse.know.what.i.saw", "witnessed")]
        [InlineData("core.inform.theft.saw", "witnessed")]
        [InlineData("core.inform.talking", "hearsay")]
        [InlineData("core.inform.travelled", "hearsay")]
        [InlineData("core.inform.still.here", "hearsay")]
        [InlineData("core.deny.rumor.told.one.person", "witnessed")]
        [InlineData("core.gossip.heard.something", "hearsay")]
        [InlineData("core.gossip.people.talking", "hearsay")]
        [InlineData("core.gossip.theft.matter", "hearsay")]
        [InlineData("core.gossip.theft.pockets", "hearsay")]
        [InlineData("core.gossip.better.clothes", "hearsay")]
        [InlineData("core.gossip.suspicion.arrived.neatly", "hearsay")]
        [InlineData("core.gossip.would.not.repeat", "hearsay")]
        [InlineData("core.gossip.not.the.only.one", "hearsay")]
        [InlineData("mod.deny.not.there.anyway", "witnessed", "participant")]
        public void ALineThatNamesARouteToKnowingIsConditionedOnThatRoute(string id, params string[] routes)
        {
            AssertGroundedBy(id, DialogueReadings.ClaimSource, routes);
        }

        /// <summary>
        /// Being able to demonstrate a claim to a third party is <c>KnowledgeRecord.CanProve</c>'s
        /// answer, and it is not a degree of confidence. A witness with empty hands cannot prove
        /// what they saw; somebody handed a signed note can prove a thing they never watched. Both
        /// of those were being worded off <see cref="DialogueReadings.Depth"/> and
        /// <see cref="DialogueReadings.Commitment"/>, neither of which knows.
        /// </summary>
        [Theory]
        [InlineData("core.accuse.can.prove", "yes")]
        [InlineData("core.accuse.not.just.word", "yes")]
        [InlineData("core.accuse.trust.the.evidence", "yes")]
        [InlineData("mod.answer.only.my.word", "no")]
        [InlineData("mod.answer.no.receipt", "no")]
        [InlineData("core.gossip.not.the.only.one", "no")]
        public void ALineThatClaimsEvidenceOrTheLackOfItIsConditionedOnWhetherThereIsAny(string id, string proof)
        {
            AssertGroundedBy(id, DialogueReadings.ClaimProof, proof);
        }

        /// <summary>
        /// The gate doing its work, over shipped content and the production path.
        ///
        /// Two speakers, the same theft, the same question, the same act and the same disclosure
        /// machinery. One watched it and one was told, and that is the only difference between
        /// them - so if provenance wording were still riding on commitment, both pools would carry
        /// both sentences.
        /// </summary>
        [Fact]
        public void AnEyewitnessMaySayTheySawItAndSomebodyWhoWasToldMayNot()
        {
            Scene scene = Scene.Create();

            IReadOnlyList<string> seen = scene.AnswerCores(scene.Witness, KnowledgeSource.Witnessed);
            IReadOnlyList<string> told = scene.AnswerCores(scene.Hearer, KnowledgeSource.Hearsay);

            Assert.Contains("core.answer.saw.it", seen);
            Assert.Contains("core.answer.was.there", seen);
            Assert.DoesNotContain("core.answer.secondhand", seen);

            Assert.Contains("core.answer.secondhand", told);
            Assert.DoesNotContain("core.answer.saw.it", told);
            Assert.DoesNotContain("core.answer.was.there", told);
        }

        /// <summary>
        /// And a caller who never looked gets neither, rather than a default. An unread route is
        /// not a route, exactly as an unread relationship is not "stranger".
        /// </summary>
        [Fact]
        public void ACallerWhoDidNotReadTheGroundsGetsNoProvenanceWordingAtAll()
        {
            Scene scene = Scene.Create();
            IReadOnlyList<string> unread = scene.AnswerCores(scene.Witness, null);

            Assert.DoesNotContain("core.answer.saw.it", unread);
            Assert.DoesNotContain("core.answer.secondhand", unread);
            Assert.DoesNotContain("core.answer.in.the.record", unread);
            Assert.NotEmpty(unread);
        }

        /// <summary>
        /// Grounds belong to one claim. Wording one claim's provenance onto another would let "I
        /// saw it" be said about a fact the speaker was only told, which is the same borrowed
        /// permission BQ-081's callback refusals exist to prevent - so it is refused rather than
        /// quietly dropped.
        /// </summary>
        [Fact]
        public void GroundsReadForAnotherClaimAreRefusedRatherThanWorded()
        {
            Scene scene = Scene.Create();
            RealizationRequest request = scene.Answering(scene.Witness);
            request.Grounds = SpeakerGrounds.Held(KnowledgeSource.Witnessed, true, scene.OtherFact);

            Assert.Contains("claim other than", request.WhyNot(), StringComparison.Ordinal);
            Assert.False(scene.Realizer.Realize(request).Rendered);
        }

        // -- other people's heads ------------------------------------------------------------------

        /// <summary>
        /// Nothing in the reading vocabulary says what the listener knows, remembers, has been
        /// told or has already worked out, and nothing should: beliefs are the knowledge graph's,
        /// they are held per person, and a wording layer that could assert one would be a second
        /// belief system with no ledger behind it. So these lines were reworded rather than
        /// grounded - there is no reading to reach for, and inventing one would be the mistake.
        /// </summary>
        [Theory]
        [InlineData("core.admit.you.knew")]
        [InlineData("core.inform.close.tie.ugly.part")]
        [InlineData("core.inform.happened")]
        [InlineData("open.mood.shame.rather.not")]
        [InlineData("core.refuse.anger.mistaken.patience")]
        [InlineData("mod.tie.enemy.remember.what.you.did")]
        [InlineData("core.deny.wrong")]
        [InlineData("core.deny.bad.information")]
        public void NoShippedLineTellsTheListenerWhatIsInTheirOwnHead(string id)
        {
            string text = Text(id);
            foreach (string claim in ListenerMindClaims)
            {
                Assert.False(
                    text.IndexOf(claim, StringComparison.OrdinalIgnoreCase) >= 0,
                    id + " asserts what the listener knows: \"" + text + "\"");
            }
        }

        /// <summary>
        /// The phrasings the audit actually found, kept as a table rather than as a rule over
        /// English. A regression guard on a fixed corpus, not a checker: it says "these exact
        /// claims came back" and nothing about sentences nobody has written yet.
        /// </summary>
        private static readonly string[] ListenerMindClaims =
        {
            "you already knew",
            "you already know",
            "you have not heard",
            "i know why you are here",
            "you have mistaken",
            "whoever told you",
            "you have been given",
            "apparently you do not"
        };

        // -- where people are and where they have been ---------------------------------------------

        /// <summary>
        /// Presence, arrival and departure are the world's to say. A tie says two people know each
        /// other, never that one of them walked here; an act says who is being addressed, never
        /// that they came. What the speaker's own route does establish is where the
        /// <em>speaker</em> was, which is why the one line here that still claims a location asks
        /// for the route that grants it.
        /// </summary>
        [Theory]
        [InlineData("core.inform.still.here")]
        [InlineData("mod.tie.friend.came.on.purpose")]
        [InlineData("open.greet.friend.came.back")]
        [InlineData("call.reply.inform")]
        [InlineData("core.deny.fear.never.there")]
        [InlineData("core.deny.was.with.me")]
        public void NoShippedLineMovesSomebodyThroughSpaceOnItsOwnAuthority(string id)
        {
            string text = Text(id);
            foreach (string claim in MovementClaims)
            {
                Assert.False(
                    text.IndexOf(claim, StringComparison.OrdinalIgnoreCase) >= 0,
                    id + " asserts a movement nothing read: \"" + text + "\"");
            }
        }

        private static readonly string[] MovementClaims =
        {
            "you came", "you left", "you were still not there", "i was never there", "was with me"
        };

        // -- what has already been said ------------------------------------------------------------

        /// <summary>
        /// "Again", "still", "once more" and "the last time" are claims about a sequence, and the
        /// only sequence wording can see is the act's own antecedent. A refusal that says "ask
        /// again" is false unless something was asked; a correction that says "that story is
        /// wrong" is false unless a story was told. Both readings already existed
        /// (<see cref="DialogueReadings.Reply"/>), which is why this whole class is a tightening
        /// rather than a rewrite.
        /// </summary>
        [Theory]
        [InlineData("core.refuse.abrasive.same.answer.louder", "ask")]
        [InlineData("core.refuse.abrasive.new.question", "ask")]
        [InlineData("core.ask.what.next", "answer", "inform")]
        [InlineData("core.ask.angry.leave.nothing", "answer", "inform")]
        [InlineData("core.inform.rumor.wrong", "gossip")]
        [InlineData("core.inform.rumor.better.ending", "gossip")]
        [InlineData("core.inform.rumor.first.part", "gossip")]
        [InlineData("core.inform.rumor.horns", "gossip")]
        [InlineData("core.accuse.suspicion.learned.your.name", "gossip")]
        [InlineData("core.deny.could.not.have", "accuse")]
        [InlineData("close.mind.who.you.accuse", "accuse")]
        [InlineData("call.history.injury.same.stove", "ask", "request")]
        public void ALineThatPointsBackwardIsConditionedOnWhatItPointsAt(string id, params string[] antecedents)
        {
            AssertGroundedBy(id, DialogueReadings.Reply, antecedents);
        }

        /// <summary>
        /// The tightening doing its work: an informing that nothing prompted has no story to
        /// correct, so the corrections are simply not in its pool.
        /// </summary>
        [Fact]
        public void AnUnpromptedInformingHasNoRumourToCorrect()
        {
            Scene scene = Scene.Create();

            IReadOnlyList<string> unprompted = scene.InformCores(null);
            IReadOnlyList<string> afterGossip = scene.InformCores(SpeechActType.Gossip);

            Assert.DoesNotContain("core.inform.rumor.wrong", unprompted);
            Assert.DoesNotContain("core.inform.rumor.horns", unprompted);
            Assert.Contains("core.inform.rumor.wrong", afterGossip);
            Assert.NotEmpty(unprompted);
        }

        // -- which way round a tie runs ------------------------------------------------------------

        /// <summary>
        /// A directed tie names the role of the person the edge runs <em>from</em> - the speaker.
        /// <c>ActorIntent</c> reads <see cref="RelationKind.Creditor"/> as "is owed" and
        /// <see cref="RelationKind.Debtor"/> as "owes", and <c>StoryletChemistry</c> gives the
        /// creditor the leverage; the tests that build a debt connect
        /// <c>(creditor -> debtor, Creditor)</c> and <c>(debtor -> creditor, Debtor)</c>.
        ///
        /// Every reciprocal-role line in the shipped corpus had that backwards, which made each of
        /// them assert a debt or an employment in the wrong direction: "You still owe me" was
        /// authored for the speaker who owes, and "I owe you. Ask." for the speaker who is owed.
        /// The wording was right and the eligibility was inverted, which is the hardest version of
        /// this bug to see and the easiest to reintroduce - so the direction is pinned here rather
        /// than left to whoever next reads the enum.
        /// </summary>
        [Theory]
        [InlineData("core.request.still.owe", "creditor")]
        [InlineData("core.request.ledger.unconvinced", "creditor")]
        [InlineData("core.request.count.against.debt", "creditor")]
        [InlineData("mod.tie.creditor.not.disappeared", "creditor")]
        [InlineData("mod.tie.creditor.something.useful", "creditor")]
        [InlineData("core.forgive.tearing.the.page", "creditor")]
        [InlineData("core.accuse.creditor.mine", "creditor")]
        [InlineData("open.greet.creditor.at.last", "creditor")]
        [InlineData("core.offer.debtor.owe.you.ask", "debtor")]
        [InlineData("mod.tie.debtor.not.forgotten", "debtor")]
        [InlineData("mod.tie.debtor.inconvenient", "debtor")]
        [InlineData("open.greet.debtor.hoping.forgotten", "debtor")]
        [InlineData("open.greet.debtor.favour.home", "debtor")]
        [InlineData("mod.tie.employer.do.the.work", "employer")]
        [InlineData("mod.tie.employee.your.position", "employee")]
        [InlineData("open.greet.employer", "employee")]
        public void AReciprocalRoleLineAsksForTheSpeakersOwnSideOfTheTie(string id, string role)
        {
            AssertGroundedBy(id, DialogueReadings.Relationship, role);
        }

        /// <summary>
        /// And the direction is a fact about the graph rather than about the table above: the tie
        /// read for a speaker who is owed says <c>creditor</c>, so the line that says so is theirs
        /// and never the other party's.
        /// </summary>
        [Fact]
        public void OnlyTheSpeakerWhoIsOwedMaySayTheListenerStillOwesThem()
        {
            Scene scene = Scene.Create();
            scene.World.Relationships.Connect(scene.Witness, scene.Player, RelationKind.Creditor, 10);
            scene.World.Relationships.Connect(scene.Player, scene.Witness, RelationKind.Debtor, 10);

            IReadOnlyList<string> owed = scene.RequestCores(scene.Witness, scene.Player);
            IReadOnlyList<string> owing = scene.RequestCores(scene.Player, scene.Witness);

            Assert.Contains("core.request.still.owe", owed);
            Assert.DoesNotContain("core.request.still.owe", owing);
            Assert.Contains("core.offer.debtor.owe.you.ask", scene.OfferCores(scene.Player, scene.Witness));
            Assert.DoesNotContain("core.offer.debtor.owe.you.ask", scene.OfferCores(scene.Witness, scene.Player));
        }

        // -- naming somebody in the room -----------------------------------------------------------

        /// <summary>
        /// The one rule mechanical enough to live at load. A name placeholder resolves to a name,
        /// and a name in the third person is a claim that its owner is not the person being spoken
        /// to - so "{referent} took it" with nothing said about where the referent is standing was
        /// eligible for an accusation made to the referent's face, and rendered as the listener's
        /// own name.
        /// </summary>
        [Fact]
        public void EveryShippedLineThatNamesSomebodySaysWhereTheyAreStanding()
        {
            foreach (DialogueFragment fragment in Shipped())
            {
                Check(fragment, DialogueSlots.Referent, DialogueReadings.Referent);
                Check(fragment, DialogueSlots.Subject, DialogueReadings.Subject);
                Check(fragment, DialogueSlots.Recalled, DialogueReadings.CallbackParty);
            }
        }

        [Theory]
        [InlineData("It was {referent}.", "referent")]
        [InlineData("{subject} took it.", "subject")]
        [InlineData("After what I did for {recalled}.", "callback_party")]
        public void AFragmentThatNamesSomebodyWithoutPlacingThemIsRejected(string text, string reading)
        {
            JsonValue unplaced = JsonValue.Object()
                .Set("id", "core.unplaced")
                .Set("position", "core")
                .Set("text", text)
                .Set("requires", JsonValue.Object().Set(DialogueReadings.Act, "accuse"));

            IReadOnlyList<ContentDiagnostic> diagnostics;
            IReadOnlyList<DialogueFragment> fragments = DialogueFragmentContent.LoadFragments(
                Bundle(unplaced), out diagnostics);

            Assert.Empty(fragments);
            Assert.Single(diagnostics);
            Assert.Contains("must declare " + reading, diagnostics[0].Message, StringComparison.Ordinal);
        }

        /// <summary>
        /// Placing them is all that is asked. A line written to be said to the person it names is
        /// a real line - "You took it" is exactly that - so the rule requires the question to be
        /// answered and never dictates the answer.
        /// </summary>
        [Fact]
        public void PlacingTheNameOnTheListenerIsAccepted()
        {
            JsonValue placed = JsonValue.Object()
                .Set("id", "core.placed")
                .Set("position", "core")
                .Set("text", "It was {referent}.")
                .Set("requires", JsonValue.Object()
                    .Set(DialogueReadings.Act, "accuse")
                    .Set(DialogueReadings.Referent, "listener"));

            IReadOnlyList<ContentDiagnostic> diagnostics;
            Assert.Single(DialogueFragmentContent.LoadFragments(Bundle(placed), out diagnostics));
            Assert.Empty(diagnostics);
        }

        // -- feelings are the speaker's, and are not aimed at anybody -------------------------------

        /// <summary>
        /// <c>EmotionalStateProfile</c> is one person's weather. It says somebody is feeling
        /// affection; it does not say who they are feeling it about, and it cannot - it holds one
        /// number per state and no target. A line that turns that into a feeling for the person
        /// opposite is asserting a direction nothing read, so where the corpus wanted one it now
        /// asks for the tie that supplies it.
        /// </summary>
        [Theory]
        [InlineData("mod.emotion.affection.less.temporary")]
        [InlineData("mod.emotion.affection.inventing.reasons")]
        [InlineData("open.mood.affection.good.cup")]
        [InlineData("core.inform.affection.came.to.tell.you")]
        [InlineData("core.warn.affection.come.back")]
        public void ALineThatAimsAFeelingAtTheListenerAsksForATieToThem(string id)
        {
            DialogueFragment fragment = Find(id);
            Assert.Contains(fragment.Requires, r => r.Key == DialogueReadings.Emotion);

            FragmentRequirement tie = fragment.Requires.FirstOrDefault(r => r.Key == DialogueReadings.Relationship);
            Assert.True(tie != null, id + " aims an undirected feeling at the listener with no tie read");
            Assert.All(tie.Values, value => Assert.Contains(value, new[] { "friend", "family", "spouse" }));
        }

        // -- the guarantee that has to keep holding ------------------------------------------------

        /// <summary>
        /// Everything above narrows pools, and narrowing pools is the one operation that can
        /// silently break this layer: a slot narrowed to nothing is a line nobody can say. Core is
        /// the slot where that matters, because a core is the only one a line cannot do without.
        /// </summary>
        [Fact]
        public void EveryActStillHasWordsForItAfterTheNarrowing()
        {
            IReadOnlyList<DialogueFragment> cores = Shipped()
                .Where(f => f.Position == FragmentPosition.Core)
                .ToList();

            foreach (SpeechActType act in Enum.GetValues(typeof(SpeechActType)))
            {
                string slug = Slug(act.ToString());
                Assert.True(
                    cores.Any(f => Says(f, slug)),
                    "no core fragment says " + slug + " any more");
            }
        }

        /// <summary>
        /// And the plainest way of saying each act survives with nothing optional read - no mood,
        /// no tie, no route, no decision, no old business. A library that only speaks once seven
        /// optional readings have been supplied is a library that falls silent in production, and
        /// every tightening in this file narrows a pool, so this is the invariant most at risk
        /// from the change that produced it. It caught one: closing the third-person hole on
        /// <c>core.accuse.named</c> left a charge made to the accused's face with no plain wording
        /// at all, which is why <c>core.accuse.direct.plain</c> exists.
        ///
        /// "Optional" is the honest line rather than "unconditional". <see cref="ActIntrinsic"/>
        /// is the set of readings <see cref="RealizationReading"/> computes from the act itself,
        /// so they are answered for every act ever composed and asking for one costs a caller
        /// nothing. Everything else depends on something a caller may or may not have supplied,
        /// and a library whose only wording for an act needs one of those has a hole in it.
        /// </summary>
        [Fact]
        public void EveryActCanStillBeSaidFromTheActAlone()
        {
            IReadOnlyList<DialogueFragment> bare = Shipped()
                .Where(f => f.Position == FragmentPosition.Core)
                .Where(f => f.Requires.All(r => ActIntrinsic.Contains(r.Key)))
                .ToList();

            foreach (SpeechActType act in Enum.GetValues(typeof(SpeechActType)))
            {
                string slug = Slug(act.ToString());
                Assert.True(
                    bare.Any(f => Says(f, slug)),
                    slug + " can no longer be said without something optional being read");
            }
        }

        /// <summary>
        /// The readings that come off the <see cref="SpeechAct"/> and nothing else, so they are
        /// never <see cref="DialogueReadings.Absent"/> for want of a caller having looked.
        /// </summary>
        private static readonly HashSet<string> ActIntrinsic = new HashSet<string>(StringComparer.Ordinal)
        {
            DialogueReadings.Act,
            DialogueReadings.Stance,
            DialogueReadings.Direction,
            DialogueReadings.Referent,
            DialogueReadings.Claim,
            DialogueReadings.Reply,
            DialogueReadings.Audience
        };

        // -- scaffolding ---------------------------------------------------------------------------

        private static void AssertGroundedBy(string id, string key, params string[] values)
        {
            DialogueFragment fragment = Find(id);
            FragmentRequirement requirement = fragment.Requires.FirstOrDefault(r => r.Key == key);

            Assert.True(requirement != null, id + " no longer declares " + key);
            Assert.Equal(
                values.OrderBy(v => v, StringComparer.Ordinal).ToArray(),
                requirement.Values.OrderBy(v => v, StringComparer.Ordinal).ToArray());
        }

        private static void Check(DialogueFragment fragment, string slot, string reading)
        {
            if (!fragment.Slots.Contains(slot))
            {
                return;
            }

            Assert.True(
                fragment.Requires.Any(r => r.Key == reading),
                fragment.Id + " names {" + slot + "} without declaring " + reading);
        }

        private static bool Says(DialogueFragment fragment, string act)
        {
            FragmentRequirement requirement = fragment.Requires.FirstOrDefault(r => r.Key == DialogueReadings.Act);
            return requirement != null && requirement.Values.Contains(act, StringComparer.Ordinal);
        }

        private static string Slug(string name)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
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

        private static string Text(string id) => Find(id).Text;

        private static DialogueFragment Find(string id)
        {
            DialogueFragment fragment = Shipped().FirstOrDefault(f => f.Id == id);
            Assert.True(fragment != null, "no shipped fragment " + id);
            return fragment;
        }

        private static IReadOnlyList<DialogueFragment> _shipped;

        private static IReadOnlyList<DialogueFragment> Shipped()
        {
            if (_shipped == null)
            {
                IReadOnlyList<ContentDiagnostic> diagnostics;
                _shipped = DialogueFragmentContent.LoadFragments(ShippedBundle(), out diagnostics);
                Assert.Empty(diagnostics);
                Assert.NotEmpty(_shipped);
            }

            return _shipped;
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
        /// One theft, three people who know about it differently, and the shipped library. Its own
        /// scene rather than <c>FragmentRealizationTests.Scene</c>, because what this file varies
        /// is exactly what that one holds fixed: who knows how.
        /// </summary>
        private sealed class Scene
        {
            private Scene(NarrativeWorldState world)
            {
                World = world;
            }

            internal NarrativeWorldState World { get; }

            internal DialogueRealizer Realizer { get; private set; }

            internal DialogueCast Cast { get; private set; }

            internal EntityId Thief { get; private set; }

            /// <summary>Saw it happen.</summary>
            internal EntityId Witness { get; private set; }

            /// <summary>Was told, and holds the identical claim just as firmly.</summary>
            internal EntityId Hearer { get; private set; }

            internal EntityId Player { get; private set; }

            internal EntityId TheftFact { get; private set; }

            internal EntityId OtherFact { get; private set; }

            internal GameTime Now => GameTime.Zero;

            internal static Scene Create()
            {
                Scene scene = new Scene(new NarrativeWorldState(20260904UL));
                scene.Thief = scene.Person("Varik");
                scene.Witness = scene.Person("Mira");
                scene.Hearer = scene.Person("Sella");
                scene.Player = scene.Person("Wren");

                Fact theft = new Fact(
                    scene.World.NewId("fact"), scene.Thief, FactPredicates.Stole, EntityId.None,
                    "silver ring", TruthState.True, secrecy: 10);
                scene.World.Knowledge.AddFact(theft);
                scene.TheftFact = theft.Id;

                Fact market = new Fact(
                    scene.World.NewId("fact"), scene.Thief, FactPredicates.LocatedAt, EntityId.None,
                    "the north market", TruthState.True, secrecy: 0);
                scene.World.Knowledge.AddFact(market);
                scene.OtherFact = market.Id;

                // The same claim, held to the same degree, by two people who came by it
                // differently. Confidence is deliberately identical: if provenance wording were
                // still riding on how firmly somebody holds a belief, these two would be
                // indistinguishable to it.
                scene.World.Knowledge.Teach(scene.Witness, theft.Id, KnowledgeSource.Witnessed, 0.9, scene.Now, false);
                scene.World.Knowledge.Teach(scene.Hearer, theft.Id, KnowledgeSource.Hearsay, 0.9, scene.Now, false);

                IReadOnlyList<ContentDiagnostic> diagnostics;
                scene.Realizer = new DialogueRealizer(DialogueFragmentContent.CreateLibrary(ShippedBundle(), out diagnostics));
                Assert.Empty(diagnostics);
                scene.Cast = DialogueCast.From(scene.World, scene.Thief, scene.Witness, scene.Hearer, scene.Player);
                return scene;
            }

            /// <summary>
            /// The cores available to this speaker answering the player, with their grounds read
            /// as the graph holds them or not read at all.
            /// </summary>
            internal IReadOnlyList<string> AnswerCores(EntityId speaker, KnowledgeSource? source)
            {
                RealizationRequest request = Answering(speaker);
                request.Grounds = source == null
                    ? SpeakerGrounds.Unread
                    : SpeakerGrounds.Of(World.Knowledge, speaker, TheftFact);

                Assert.Equal(string.Empty, request.WhyNot());
                return Ids(Realizer.Candidates(FragmentPosition.Core, request));
            }

            /// <summary>This speaker answering the player about the theft, grounds unread.</summary>
            internal RealizationRequest Answering(EntityId speaker)
            {
                World.Relationships.Connect(speaker, Player, RelationKind.Friend, 70);
                World.Registry.GetNpc(speaker).Personality.Honesty = 0.9;

                SpeechAct question = SpeechAct.Compose(
                    SpeechActType.Ask, Player, speaker, new ActionBinding { PropositionFact = TheftFact });
                DisclosureDecision decision = Disclosure.Decide(World, speaker, Player, TheftFact, Now);
                SpeechAct answer = Disclosure.Compose(decision, question);
                Assert.NotNull(answer);

                return new RealizationRequest(answer)
                {
                    Decision = decision,
                    Claim = World.Knowledge.GetFact(TheftFact),
                    Cast = Cast
                };
            }

            internal IReadOnlyList<string> InformCores(SpeechActType? antecedent)
            {
                SpeechAct prior = antecedent == null
                    ? null
                    : SpeechAct.Compose(
                        antecedent.Value, Player, Witness,
                        new ActionBinding { PropositionFact = TheftFact }, Thief);

                SpeechAct inform = SpeechAct.Compose(
                    SpeechActType.Inform, Witness, Player,
                    new ActionBinding { PropositionFact = TheftFact }, EntityId.None, prior);
                Assert.NotNull(inform);

                return Ids(Realizer.Candidates(
                    FragmentPosition.Core,
                    new RealizationRequest(inform)
                    {
                        Claim = World.Knowledge.GetFact(TheftFact),
                        Cast = Cast
                    }));
            }

            internal IReadOnlyList<string> RequestCores(EntityId speaker, EntityId listener)
            {
                return TieCores(SpeechActType.Request, speaker, listener);
            }

            internal IReadOnlyList<string> OfferCores(EntityId speaker, EntityId listener)
            {
                return TieCores(SpeechActType.Offer, speaker, listener);
            }

            private IReadOnlyList<string> TieCores(SpeechActType type, EntityId speaker, EntityId listener)
            {
                SpeechAct act = SpeechAct.Compose(
                    type, speaker, listener, new ActionBinding { Purpose = "the debt" });
                Assert.NotNull(act);

                RealizationRequest request = new RealizationRequest(act)
                {
                    Cast = Cast,
                    Tie = SpeakerTie.Of(World.Relationships, speaker, listener)
                };

                Assert.Equal(string.Empty, request.WhyNot());
                return Ids(Realizer.Candidates(FragmentPosition.Core, request));
            }

            private static IReadOnlyList<string> Ids(IReadOnlyList<DialogueFragment> fragments)
            {
                List<string> ids = new List<string>();
                for (int i = 0; i < fragments.Count; i++)
                {
                    ids.Add(fragments[i].Id);
                }

                return ids;
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
