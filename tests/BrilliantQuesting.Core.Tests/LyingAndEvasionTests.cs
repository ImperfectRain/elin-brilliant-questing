using System.Collections.Generic;
using System.Linq;
using BrilliantQuesting.Actions;
using BrilliantQuesting.Diagnostics;
using BrilliantQuesting.Dialogue;
using BrilliantQuesting.Events;
using BrilliantQuesting.Foundation;
using BrilliantQuesting.Knowledge;
using BrilliantQuesting.Persistence;
using BrilliantQuesting.Relationships;
using BrilliantQuesting.World;
using Xunit;

namespace BrilliantQuesting.Tests
{
    /// <summary>
    /// BQ-073. Lying and evading are outcomes of a disclosure decision, not ways of wording one.
    ///
    /// BQ-071 gave a character four ways to be more or less forthcoming and every one of them was
    /// the truth; BQ-072 added how much of it came out and every rung of that was the truth too.
    /// The world that leaves is one where an interrogation is won by noticing who declined to
    /// answer, and where the rumour layer can seed a false belief (BQ-020) although no character
    /// can assert one to your face.
    ///
    /// So this file guards the distinctions that make the addition worth having, and they are all
    /// distinctions this layer could easily have collapsed:
    ///
    /// <list type="bullet">
    /// <item>a lie is measured against the speaker's belief, never against the world;</item>
    /// <item>an honest mistake is not a lie, and the two can assert the identical claim;</item>
    /// <item>refusing, changing the subject, answering a neighbouring question, answering
    /// incompletely and asserting a falsehood stay five different things;</item>
    /// <item>the world records what was said, not merely that somebody was deceived;</item>
    /// <item>and catching the liar is a reading of one character's knowledge rather than an
    /// announcement from an omniscient narrator.</item>
    /// </list>
    /// </summary>
    public class LyingAndEvasionTests
    {
        // -- the done-when -------------------------------------------------------------------------

        /// <summary>
        /// The step's condition, end to end: an NPC lies, the world records the lie, and the player
        /// can later catch the contradiction.
        ///
        /// The thief is asked about his own theft by somebody who does not yet know anything. He
        /// holds the claim at full conviction - he was there - so his denial is not a mistake he
        /// could ever have made honestly, and that is exactly what makes it recordable. The player
        /// learns the truth afterwards from somewhere else entirely, and only then is the
        /// contradiction theirs to hold.
        /// </summary>
        [Fact]
        public void AnNpcLiesTheWorldRecordsItAndThePlayerCanLaterCatchTheContradiction()
        {
            Interrogation scene = Interrogation.Create();
            scene.MakeDishonest(scene.Thief);

            DisclosureDecision decision = scene.AskThief();
            Assert.Equal(DisclosureTactic.Falsify, decision.Tactic);
            Assert.True(decision.WillLie);

            // What he says. A denial of the very claim he holds - and it is the vocabulary's
            // ordinary Deny, because a lie is a stance against belief rather than an act of its
            // own.
            SpeechAct denial = Disclosure.Compose(decision, scene.QuestionToThief);
            Assert.NotNull(denial);
            Assert.Equal(SpeechActType.Deny, denial.Type);
            Assert.Equal(SpeechActStance.Denies, denial.Stance);
            Assert.Equal(scene.TheftFact, denial.About);
            Assert.Equal(scene.Thief, denial.Referent);

            Veracity veracity = Deception.Assess(scene.World, denial);
            Assert.True(veracity.IsLie);
            Assert.Equal(Sincerity.Insincere, veracity.Sincerity);
            Assert.Equal(scene.TheftFact, veracity.Contradicts);
            Assert.Equal(1.0, veracity.Conviction, 9);

            // The world records it: a durable fact that he lied about this claim, and an event
            // saying what he said to whom.
            WorldEvent recorded = Deception.Record(scene.World, denial, scene.Now);
            Assert.NotNull(recorded);
            Assert.Equal(WorldEventType.Deceived, recorded.Type);
            Assert.Equal(scene.Thief, recorded.Actor);
            Assert.Equal(scene.Player, recorded.Target);
            Assert.Contains(EventTags.Denied, recorded.Tags);

            Fact onRecord = scene.LieRecord();
            Assert.NotNull(onRecord);
            Assert.Equal(scene.Thief, onRecord.Subject);
            Assert.Equal(scene.TheftFact, onRecord.Object);

            // And only the liar knows he did it. A world that told anybody else would have skipped
            // the part the player is supposed to earn.
            Assert.True(scene.World.Knowledge.Knows(scene.Thief, onRecord.Id));
            Assert.False(scene.World.Knowledge.Knows(scene.Player, onRecord.Id));

            // Nothing to catch yet: the player was told a thing and holds nothing against it.
            Assert.Empty(Deception.Contradictions(scene.World, scene.Player));

            // Later, from the witness, they learn what actually happened.
            scene.World.Knowledge.Teach(
                scene.Player, scene.TheftFact, KnowledgeSource.Hearsay, 0.8, scene.Now, canProve: false);

            IReadOnlyList<Contradiction> caught = Deception.Contradictions(scene.World, scene.Player);
            Contradiction contradiction = Assert.Single(caught);
            Assert.Equal(scene.Thief, contradiction.Liar);
            Assert.Equal(scene.TheftFact, contradiction.ContradictingBelief);
            Assert.Equal(SpeechActStance.Denies, contradiction.Statement.Stance);
            Assert.Equal(recorded.Id, contradiction.Statement.EventId);

            // Being sure and being able to show it stay separate, as everywhere else in the
            // knowledge layer.
            Assert.False(contradiction.CanProve);
        }

        // -- truth is relative to the speaker's belief ---------------------------------------------

        /// <summary>
        /// The distinction the whole step turns on, isolated: two characters assert the identical
        /// false claim, and only one of them is lying.
        ///
        /// The garbled story blaming Tovar is false in the world either way. The neighbour who
        /// heard it and repeats it has done nothing dishonest; the witness who watched Kip take
        /// the ring and repeats it anyway has. Nothing separates them except what they hold, which
        /// is the point - a model that read the world's own truth would have called both of them
        /// liars and put an innocent neighbour on record as one.
        /// </summary>
        [Fact]
        public void TheSameFalseClaimIsALieFromOneSpeakerAndAnHonestMistakeFromAnother()
        {
            Interrogation scene = Interrogation.Create();

            SpeechAct fromTheNeighbour = scene.Blames(scene.Neighbour);
            SpeechAct fromTheWitness = scene.Blames(scene.Witness);
            Assert.Equal(fromTheNeighbour.About, fromTheWitness.About);

            Veracity mistake = Deception.Assess(scene.World, fromTheNeighbour);
            Veracity lie = Deception.Assess(scene.World, fromTheWitness);

            Assert.Equal(Sincerity.Sincere, mistake.Sincerity);
            Assert.True(mistake.IsHonestMistake);
            Assert.False(mistake.IsLie);

            Assert.Equal(Sincerity.Insincere, lie.Sincerity);
            Assert.True(lie.IsLie);
            Assert.False(lie.IsHonestMistake);

            // Both assertions are untrue in the world, and the world says so identically for both.
            Assert.Equal(TruthState.False, mistake.Accuracy);
            Assert.Equal(TruthState.False, lie.Accuracy);

            // What the liar spoke against is named, and it is the true version of the story.
            Assert.Equal(scene.TheftFact, lie.Contradicts);
            Assert.True(lie.Contradicts.IsNone == false);
            Assert.True(mistake.Contradicts.IsNone);

            // And only the lie is written down.
            Assert.Null(Deception.Record(scene.World, fromTheNeighbour, scene.Now));
            Assert.NotNull(Deception.Record(scene.World, fromTheWitness, scene.Now));
        }

        /// <summary>
        /// The converse, and the reason the classifier may not reach for world truth even as a
        /// tie-breaker: somebody who asserts a true claim while believing otherwise is lying, and
        /// nobody's statement is scored by whether it happened to land.
        /// </summary>
        [Fact]
        public void AssertingSomethingTrueAgainstYourOwnBeliefIsStillALie()
        {
            Interrogation scene = Interrogation.Create();

            // The neighbour holds the garbled version firmly, and puts the true one forward
            // anyway. That is a person saying what they do not believe, which is the definition,
            // and the fact that the world agrees with the words does not change what they did.
            SpeechAct trueClaim = SpeechAct.Compose(
                SpeechActType.Gossip,
                scene.Neighbour,
                scene.Player,
                new ActionBinding { PropositionFact = scene.TheftFact },
                scene.Thief);

            Veracity veracity = Deception.Assess(scene.World, trueClaim);

            Assert.Equal(Sincerity.Insincere, veracity.Sincerity);
            Assert.True(veracity.IsLie);
            Assert.Equal(TruthState.True, veracity.Accuracy);
            Assert.Equal(scene.BlameFact, veracity.Contradicts);
        }

        /// <summary>
        /// Asserting something with no belief behind it either way is reckless and is not a lie.
        ///
        /// The gap that would otherwise be filled by an assumption: somebody who repeats what they
        /// were handed without forming a view has decided nothing, and recording a deception there
        /// would be recording one with no deceiver. Denying a claim you have never held is
        /// different again and is ordinary - it is what everybody says about everything they have
        /// not heard of.
        /// </summary>
        [Fact]
        public void AssertingWithoutABeliefIsUnfoundedAndDenyingWithoutOneIsOrdinary()
        {
            Interrogation scene = Interrogation.Create();

            SpeechAct assertion = SpeechAct.Compose(
                SpeechActType.Gossip,
                scene.Bystander,
                scene.Player,
                new ActionBinding { PropositionFact = scene.TheftFact },
                scene.Thief);
            SpeechAct denial = SpeechAct.Compose(
                SpeechActType.Deny,
                scene.Bystander,
                scene.Player,
                new ActionBinding { PropositionFact = scene.TheftFact },
                scene.Thief);

            Veracity asserted = Deception.Assess(scene.World, assertion);
            Veracity denied = Deception.Assess(scene.World, denial);

            Assert.Equal(Sincerity.Unfounded, asserted.Sincerity);
            Assert.False(asserted.IsLie);
            Assert.Equal(Sincerity.Sincere, denied.Sincerity);
            Assert.False(denied.IsLie);

            Assert.Null(Deception.Record(scene.World, assertion, scene.Now));
            Assert.Null(Deception.Record(scene.World, denial, scene.Now));
        }

        // -- the five outcomes stay five things -----------------------------------------------------

        /// <summary>
        /// Refusing, changing the subject, answering a neighbouring question, answering
        /// incompletely and asserting a falsehood are five different things to have done, and each
        /// comes out of the model as its own outcome rather than as a differently worded version
        /// of "would not say".
        ///
        /// Asserted on what a consumer can actually read: the tactic, the act composed from it and
        /// whether that act puts a claim forward. Four of the five assert nothing at all.
        /// </summary>
        [Fact]
        public void RefusalEvasionDeflectionIncompleteAnswerAndLieStayDistinguishable()
        {
            Interrogation scene = Interrogation.Create();

            DisclosureDecision declines = scene.HonestThiefAsked();
            DisclosureDecision lies = scene.DishonestThiefAsked();
            DisclosureDecision changesSubject = scene.WitnessAskedBy(RelationKind.Rival, -20);
            DisclosureDecision answersElsewhere = scene.WitnessWithSomethingElseToSay();
            DisclosureDecision partial = scene.WitnessAskedBy(RelationKind.Acquaintance, 35);

            Assert.Equal(DisclosureTactic.Decline, declines.Tactic);
            Assert.Equal(DisclosureTactic.Falsify, lies.Tactic);
            Assert.Equal(DisclosureTactic.ChangeSubject, changesSubject.Tactic);
            Assert.Equal(DisclosureTactic.AnswerElsewhere, answersElsewhere.Tactic);
            Assert.Equal(DisclosureTactic.None, partial.Tactic);

            // The incomplete answer is the one that says the claim and still holds part of it
            // back. It is not an evasion and it is not a lie: every word of it is true.
            Assert.True(partial.WillDisclose);
            Assert.True(partial.HeldBack);
            Assert.True(partial.Depth < partial.KnownDepth);

            SpeechAct refusal = Disclosure.Compose(declines, scene.QuestionToThief);
            SpeechAct falsehood = Disclosure.Compose(lies, scene.QuestionToThief);
            SpeechAct evasion = Disclosure.Compose(changesSubject, scene.QuestionToWitness);
            SpeechAct sidestep = Disclosure.Compose(answersElsewhere, scene.QuestionToWitness);
            SpeechAct incomplete = Disclosure.Compose(partial, scene.QuestionToWitness);

            Assert.Equal(SpeechActType.Refuse, refusal.Type);
            Assert.Equal(SpeechActType.Deny, falsehood.Type);
            Assert.Equal(SpeechActType.Evade, evasion.Type);
            Assert.Equal(SpeechActType.Evade, sidestep.Type);
            Assert.Equal(SpeechActType.Answer, incomplete.Type);

            // Exactly one of the five asserts anything, and it is the lie.
            SpeechAct[] said = { refusal, falsehood, evasion, sidestep, incomplete };
            Assert.Equal(
                new[] { false, true, false, false, true },
                said.Select(act => act.Content.HasProposition).ToArray());
            Assert.Equal(
                new[] { false, true, false, false, false },
                said.Select(act => Deception.Assess(scene.World, act).IsLie).ToArray());

            // The two evasions are the same act and different decisions: what separates them is
            // whether something true was offered instead, which is on the decision where a
            // realizer can read it, not on the act.
            Assert.Equal(evasion.Signature, sidestep.Signature);
            Assert.NotEqual(changesSubject.Tactic, answersElsewhere.Tactic);
        }

        /// <summary>
        /// An evasion cannot become a lie by accident, and the guarantee is structural rather than
        /// a convention anybody has to keep.
        ///
        /// The act type carries stance <see cref="SpeechActStance.None"/> and the composed act
        /// carries no proposition, so there is nothing on it for a scorer to read as an assertion
        /// - which is why the whole vocabulary can be enumerated and the deception layer will
        /// never call an evasion insincere.
        /// </summary>
        [Fact]
        public void EvadingAssertsNothingByConstruction()
        {
            Interrogation scene = Interrogation.Create();

            Assert.Equal(SpeechActStance.None, SpeechActProfile.Of(SpeechActType.Evade).Stance);
            Assert.Equal(
                SpeechActDirection.WithholdsInformation,
                SpeechActProfile.Of(SpeechActType.Evade).Direction);

            SpeechAct evasion = Disclosure.Compose(
                scene.WitnessAskedBy(RelationKind.Rival, -20), scene.QuestionToWitness);

            Assert.True(evasion.About.IsNone);
            Assert.Equal(Sincerity.NotAsserted, Deception.Assess(scene.World, evasion).Sincerity);

            // And an evasion of nothing is not an evasion: it needs something to slide away from,
            // the same rule an answer and a refusal are held to.
            Assert.Null(SpeechAct.Compose(
                SpeechActType.Evade, scene.Witness, scene.Player, ActionBinding.Empty));
        }

        /// <summary>
        /// Refusal and omission are never promoted into lies. The same thief under the same
        /// unbearable pressure declines when he is an honest man, and the world holds no deception
        /// afterwards.
        ///
        /// Character is a condition rather than a weight for exactly this reason: a large enough
        /// number must not be able to make anybody a liar, or the model would say that everybody
        /// lies eventually and pressure is all there is to a person.
        /// </summary>
        [Fact]
        public void AnHonestSpeakerUnderTheSamePressureRefusesRatherThanLies()
        {
            Interrogation scene = Interrogation.Create();

            DisclosureDecision honest = scene.HonestThiefAsked();
            DisclosureDecision dishonest = scene.DishonestThiefAsked();

            // The same claim, the same asker, the same conviction, the same reluctance.
            Assert.Equal(DisclosureStrategy.Refuse, honest.Strategy);
            Assert.Equal(DisclosureStrategy.Refuse, dishonest.Strategy);
            Assert.False(honest.WillDisclose);
            Assert.False(dishonest.WillDisclose);

            Assert.False(honest.WillLie);
            Assert.True(dishonest.WillLie);

            SpeechAct refusal = Disclosure.Compose(honest, scene.QuestionToThief);
            Assert.Equal(SpeechActType.Refuse, refusal.Type);
            Assert.Equal(Sincerity.NotAsserted, Deception.Assess(scene.World, refusal).Sincerity);
            Assert.Null(Deception.Record(scene.World, refusal, scene.Now));
            Assert.Null(scene.LieRecord());
        }

        // -- what the record has to keep -------------------------------------------------------------

        /// <summary>
        /// The recorded deception keeps the claim, the stance and the audience - not merely that
        /// somebody was deceived.
        ///
        /// This is the part later systems live on. Contradiction, memory, rumour and conversation
        /// state all need to ask what a person actually committed to; an entry saying only "a lie
        /// happened here" cannot be argued with later, and the difference between "he said Kip
        /// took it" and "he said Kip did not take it" is the whole content of the exchange.
        /// </summary>
        [Fact]
        public void TheRecordKeepsWhatWasSaidAndNotMerelyThatSomebodyWasDeceived()
        {
            Interrogation scene = Interrogation.Create();
            scene.MakeDishonest(scene.Thief);

            SpeechAct denial = Disclosure.Compose(scene.AskThief(), scene.QuestionToThief);
            WorldEvent recorded = Deception.Record(scene.World, denial, scene.Now);

            RecordedStatement statement = Deception.StatementOf(recorded);
            Assert.True(statement.Recognized);
            Assert.Equal(scene.Thief, statement.Speaker);
            Assert.Equal(scene.Player, statement.Audience);
            Assert.Equal(scene.TheftFact, statement.AssertedClaim);
            Assert.Equal(SpeechActStance.Denies, statement.Stance);
            Assert.Equal(scene.TheftFact, statement.Contradicts);
            Assert.True(statement.WasHeardBy(scene.Player));
            Assert.False(statement.WasHeardBy(scene.Bystander));

            // A substituted version records the other shape: what was put forward and, separately,
            // what the speaker actually held.
            WorldEvent substituted = Deception.Record(scene.World, scene.Blames(scene.Witness), scene.Now);
            RecordedStatement blaming = Deception.StatementOf(substituted);

            Assert.Equal(SpeechActStance.Affirms, blaming.Stance);
            Assert.Equal(scene.BlameFact, blaming.AssertedClaim);
            Assert.Equal(scene.TheftFact, blaming.Contradicts);
            Assert.NotEqual(blaming.AssertedClaim, blaming.Contradicts);
        }

        /// <summary>
        /// A deception recorded without its stance is left alone rather than guessed at.
        ///
        /// There are entries in history that say a deception occurred and cannot say what was
        /// said, and reading a stance into one would be inventing testimony - the claim named
        /// might have been affirmed or denied, and those are opposite statements. So such an entry
        /// is unrecognized, and nobody is ever caught out on it.
        /// </summary>
        [Fact]
        public void ADeceptionWithNoRecordedStanceIsNotReadAsTestimony()
        {
            Interrogation scene = Interrogation.Create();
            WorldEvent untagged = scene.World.Record(
                WorldEventType.Deceived,
                scene.Thief,
                scene.Player,
                scene.Now,
                related: new[] { scene.TheftFact });

            Assert.False(Deception.StatementOf(untagged).Recognized);

            scene.World.Knowledge.Teach(
                scene.Player, scene.TheftFact, KnowledgeSource.Witnessed, 0.9, scene.Now, canProve: true);
            Assert.Empty(Deception.Contradictions(scene.World, scene.Player));
        }

        /// <summary>
        /// One lie per speaker and claim, however often it is told and through whichever channel.
        ///
        /// A rumour seeded deliberately (BQ-020) and a lie told to somebody's face are the same
        /// thing about the world - this person has lied about this - and the two subsystems share
        /// the primitive that says so. Otherwise "has Kip lied about the theft" would have one
        /// answer per layer that happened to notice.
        /// </summary>
        [Fact]
        public void ConversationalAndSeededLiesLeaveOneSharedRecord()
        {
            Interrogation scene = Interrogation.Create();
            scene.MakeDishonest(scene.Witness);

            Deception.Record(scene.World, scene.Blames(scene.Witness), scene.Now);
            Deception.Record(scene.World, scene.Blames(scene.Witness), scene.Now);

            RumorSystem rumors = new RumorSystem(scene.World.Knowledge, scene.World.Ledger, scene.World.Ids);
            Assert.True(rumors.Lie(
                scene.Witness, scene.Player, scene.TheftFact, scene.BlameFact, scene.Now, 0.7));

            IEnumerable<Fact> records = scene.World.Knowledge.Facts.Values.Where(
                fact => fact.Predicate == FactPredicates.LiedAbout && fact.Subject == scene.Witness);
            Fact single = Assert.Single(records);
            Assert.Equal(scene.TheftFact, single.Object);

            // And the seeded lie is catchable by the same route as the spoken one, because it is
            // recorded with the same meaning: what was put forward, and what it runs against.
            scene.World.Knowledge.Teach(
                scene.Player, scene.TheftFact, KnowledgeSource.Witnessed, 0.9, scene.Now, canProve: false);

            IReadOnlyList<Contradiction> caught = Deception.Contradictions(scene.World, scene.Player);
            Assert.All(caught, c => Assert.Equal(scene.Witness, c.Liar));
            Assert.All(caught, c => Assert.Equal(scene.BlameFact, c.Statement.AssertedClaim));
            Assert.Contains(caught, c => c.Statement.EventId == scene.World.Ledger.Events[scene.World.Ledger.Count - 1].Id);
        }

        /// <summary>
        /// Catching somebody out is a reading of one person's knowledge and never a hint from the
        /// world.
        ///
        /// Two ways it could have leaked, and both are closed: a bystander who knows the truth but
        /// was not there catches nothing, and being told the lie is not itself enough - the
        /// listener must come to hold something firm against it. Doubt is not a conviction, and a
        /// model that treated it as one would let a suspicious player convict anybody.
        /// </summary>
        [Fact]
        public void OnlySomebodyWhoWasThereAndNowKnowsBetterCatchesTheLie()
        {
            Interrogation scene = Interrogation.Create();
            scene.MakeDishonest(scene.Thief);

            SpeechAct denial = Disclosure.Compose(scene.AskThief(), scene.QuestionToThief);
            Deception.Record(scene.World, denial, scene.Now);

            // The witness saw the theft and holds it at 0.9 - and was not in the room.
            Assert.Empty(Deception.Contradictions(scene.World, scene.Witness));

            // The player was in the room, and a doubt is not knowing better.
            scene.World.Knowledge.Teach(
                scene.Player, scene.TheftFact, KnowledgeSource.Hearsay, 0.3, scene.Now, canProve: false);
            Assert.Empty(Deception.Contradictions(scene.World, scene.Player));

            scene.World.Knowledge.TryGetBelief(scene.Player, scene.TheftFact, out KnowledgeRecord belief);
            belief.Confidence = 0.85;
            Assert.Single(Deception.Contradictions(scene.World, scene.Player));

            // The liar does not catch himself, though he holds the contradicting belief by
            // definition.
            Assert.Empty(Deception.Contradictions(scene.World, scene.Thief));
        }

        /// <summary>
        /// Deciding and assessing are readings, not writes. The whole path from question to
        /// verdict can be walked with nothing at all changing in the world, so a caller may look
        /// before it commits and an inspector may run whenever it likes.
        ///
        /// <see cref="Deception.Record"/> is the one call that writes, and it is a separate call
        /// for exactly that reason.
        /// </summary>
        [Fact]
        public void DecidingAndAssessingWriteNothing()
        {
            Interrogation scene = Interrogation.Create();
            scene.MakeDishonest(scene.Thief);

            int events = scene.World.Ledger.Count;
            int facts = scene.World.Knowledge.Facts.Count;
            int beliefs = scene.World.Registry.Npcs.Sum(
                npc => scene.World.Knowledge.BeliefsOf(npc.Key).Count());

            DisclosureDecision decision = scene.AskThief();
            SpeechAct denial = Disclosure.Compose(decision, scene.QuestionToThief);
            Deception.Assess(scene.World, denial);
            Deception.Contradictions(scene.World, scene.Player);
            NarrativeInspector.DescribeVeracity(scene.World, denial);
            NarrativeInspector.DescribeDisclosure(scene.World, decision);

            Assert.Equal(events, scene.World.Ledger.Count);
            Assert.Equal(facts, scene.World.Knowledge.Facts.Count);
            Assert.Equal(beliefs, scene.World.Registry.Npcs.Sum(
                npc => scene.World.Knowledge.BeliefsOf(npc.Key).Count()));
        }

        /// <summary>
        /// Both readings are explainable, and the inspector states which one it used.
        ///
        /// The world's own view of the claim is printed beside the verdict and marked as reported
        /// rather than consulted, because somebody reading a dump that showed a false claim next
        /// to the word "lie" would reasonably conclude the two were connected - and the entire
        /// step depends on their not being.
        /// </summary>
        [Fact]
        public void TheInspectorSeparatesWhatWasBelievedFromWhatIsSo()
        {
            Interrogation scene = Interrogation.Create();
            scene.MakeDishonest(scene.Thief);

            DisclosureDecision decision = scene.AskThief();
            SpeechAct denial = Disclosure.Compose(decision, scene.QuestionToThief);

            string verdict = NarrativeInspector.DescribeVeracity(scene.World, denial);
            Assert.Contains("veracity: Insincere", verdict);
            Assert.Contains("as not so", verdict);
            Assert.Contains("reported, not used to decide sincerity", verdict);
            Assert.Contains("a deliberate falsehood", verdict);
            Assert.Contains("wording:     none", verdict);

            string mistake = NarrativeInspector.DescribeVeracity(scene.World, scene.Blames(scene.Neighbour));
            Assert.Contains("an honest mistake", mistake);

            // And the decision says what was done instead of answering, which the ladder alone
            // cannot: "no" is the same rung for a refusal and for a lie.
            string decided = NarrativeInspector.DescribeDisclosure(scene.World, decision);
            Assert.Contains("instead:     says something they do not believe", decided);
            Assert.Contains("disclosure: Refuse", decided);

            string evaded = NarrativeInspector.DescribeDisclosure(
                scene.World, scene.WitnessAskedBy(RelationKind.Rival, -20));
            Assert.Contains("instead:     lets the question go", evaded);
        }

        // -- durable history ---------------------------------------------------------------------

        /// <summary>
        /// The record is history, so it survives the save - and survives it with its meaning
        /// intact, which is the part that matters. A reloaded world in which the statement's
        /// stance had been lost would be a world where a liar quietly stopped being catchable.
        ///
        /// No new save entry was added for any of this: the statement is an event and the lie is a
        /// fact, and both formats already carried everything needed.
        /// </summary>
        [Fact]
        public void TheRecordedLieAndTheCatchSurviveASaveRoundTrip()
        {
            Interrogation scene = Interrogation.Create();
            scene.MakeDishonest(scene.Thief);

            SpeechAct denial = Disclosure.Compose(scene.AskThief(), scene.QuestionToThief);
            WorldEvent recorded = Deception.Record(scene.World, denial, scene.Now);
            scene.World.Knowledge.Teach(
                scene.Player, scene.TheftFact, KnowledgeSource.Witnessed, 0.9, scene.Now, canProve: true);

            Contradiction before = Assert.Single(Deception.Contradictions(scene.World, scene.Player));

            NarrativeWorldState reloaded = WorldStateSerializer.Load(WorldStateSerializer.Save(scene.World));

            Contradiction after = Assert.Single(Deception.Contradictions(reloaded, scene.Player));
            Assert.Equal(before.Statement.EventId, after.Statement.EventId);
            Assert.Equal(before.Statement.Stance, after.Statement.Stance);
            Assert.Equal(before.Statement.AssertedClaim, after.Statement.AssertedClaim);
            Assert.Equal(before.Liar, after.Liar);
            Assert.True(after.CanProve);

            // The durable trace of having lied comes back too, still known only to the liar.
            Fact onRecord = reloaded.Knowledge.Facts.Values.Single(
                fact => fact.Predicate == FactPredicates.LiedAbout);
            Assert.Equal(scene.Thief, onRecord.Subject);
            Assert.True(reloaded.Knowledge.Knows(scene.Thief, onRecord.Id));
            Assert.False(reloaded.Knowledge.Knows(scene.Player, onRecord.Id));

            // And the round trip did not duplicate the statement.
            Assert.Equal(
                1,
                reloaded.Ledger.OfType(WorldEventType.Deceived).Count(
                    e => Deception.StatementOf(e).Recognized));
            Assert.Equal(recorded.Id, after.Statement.EventId);
        }

        // -- the scene -------------------------------------------------------------------------------

        /// <summary>
        /// One theft, one true account of it and one garbled account blaming somebody else - which
        /// is the shape BQ-020 already produces - and five people who stand in different relations
        /// to all three.
        /// </summary>
        private sealed class Interrogation
        {
            private Interrogation(NarrativeWorldState world)
            {
                World = world;
            }

            internal NarrativeWorldState World { get; }

            /// <summary>Took the ring, and knows it.</summary>
            internal EntityId Thief { get; private set; }

            /// <summary>Watched him do it.</summary>
            internal EntityId Witness { get; private set; }

            /// <summary>Heard the garbled version and believes it. Wrong, not dishonest.</summary>
            internal EntityId Neighbour { get; private set; }

            /// <summary>Holds no belief about any of it.</summary>
            internal EntityId Bystander { get; private set; }

            internal EntityId Player { get; private set; }

            internal EntityId TheftFact { get; private set; }

            /// <summary>The same story with the wrong name in it, false and linked to the true one.</summary>
            internal EntityId BlameFact { get; private set; }

            internal GameTime Now => GameTime.Zero;

            internal static Interrogation Create()
            {
                Interrogation scene = new Interrogation(new NarrativeWorldState(20260902UL));
                scene.Thief = scene.Person("Kip");
                scene.Witness = scene.Person("Mira");
                scene.Neighbour = scene.Person("Nel");
                scene.Bystander = scene.Person("Tovar");
                scene.Player = scene.Person("You");

                Fact theft = new Fact(
                    scene.World.NewId("fact"),
                    scene.Thief,
                    FactPredicates.Stole,
                    EntityId.None,
                    "silver ring",
                    TruthState.True,
                    secrecy: 40);
                scene.World.Knowledge.AddFact(theft);
                scene.TheftFact = theft.Id;

                Fact blame = new Fact(
                    scene.World.NewId("fact"),
                    scene.Bystander,
                    FactPredicates.Stole,
                    EntityId.None,
                    "silver ring",
                    TruthState.False,
                    secrecy: 40)
                {
                    DistortionOf = theft.Id
                };
                scene.World.Knowledge.AddFact(blame);
                scene.BlameFact = blame.Id;

                scene.World.Knowledge.Teach(scene.Thief, theft.Id, KnowledgeSource.Participant, 1.0, scene.Now, false);
                scene.World.Knowledge.Teach(scene.Witness, theft.Id, KnowledgeSource.Witnessed, 0.9, scene.Now, false);
                scene.World.Knowledge.Teach(scene.Neighbour, blame.Id, KnowledgeSource.Hearsay, 0.8, scene.Now, false);

                foreach (EntityId person in new[] { scene.Thief, scene.Witness, scene.Neighbour, scene.Bystander })
                {
                    scene.Npc(person).Emotions.Set(EmotionalState.Fear, 0.0);
                }

                return scene;
            }

            internal NarrativeNpc Npc(EntityId id) => World.Registry.GetNpc(id);

            internal void MakeDishonest(EntityId person) => Npc(person).Personality.Honesty = 0.1;

            internal SpeechAct QuestionToThief => Question(Thief);

            internal SpeechAct QuestionToWitness => Question(Witness);

            /// <summary>The thief asked about his own theft: legal risk, privacy, and his own skin.</summary>
            internal DisclosureDecision AskThief()
            {
                return Disclosure.Decide(World, Thief, Player, TheftFact, Now);
            }

            internal DisclosureDecision HonestThiefAsked()
            {
                Npc(Thief).Personality.Honesty = 0.9;
                return AskThief();
            }

            internal DisclosureDecision DishonestThiefAsked()
            {
                MakeDishonest(Thief);
                return AskThief();
            }

            internal DisclosureDecision WitnessAskedBy(RelationKind kind, int sentiment)
            {
                World.Relationships.Connect(Witness, Player, kind, sentiment);
                return Disclosure.Decide(World, Witness, Player, TheftFact, Now);
            }

            /// <summary>
            /// The same deflecting witness, given one public thing about the thief she is happy to
            /// talk about instead. Having a substitute is what makes answering a different
            /// question available at all.
            /// </summary>
            internal DisclosureDecision WitnessWithSomethingElseToSay()
            {
                Fact elsewhere = new Fact(
                    World.NewId("fact"),
                    Thief,
                    FactPredicates.LocatedAt,
                    EntityId.None,
                    "the north market",
                    TruthState.True,
                    secrecy: 0);
                World.Knowledge.AddFact(elsewhere);
                World.Knowledge.Teach(Witness, elsewhere.Id, KnowledgeSource.Witnessed, 0.9, Now, false);

                return WitnessAskedBy(RelationKind.Rival, -20);
            }

            /// <summary>This person puts the garbled version forward, naming the wrong man.</summary>
            internal SpeechAct Blames(EntityId speaker)
            {
                return SpeechAct.Compose(
                    SpeechActType.Gossip,
                    speaker,
                    Player,
                    new ActionBinding { PropositionFact = BlameFact },
                    Bystander);
            }

            internal Fact LieRecord()
            {
                foreach (Fact fact in World.Knowledge.Facts.Values)
                {
                    if (fact.Predicate == FactPredicates.LiedAbout)
                    {
                        return fact;
                    }
                }

                return null;
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
